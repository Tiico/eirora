using System;
using System.Collections.Generic;
using Eirora.Simulation.Events;
using Eirora.Simulation.Serialization;

namespace Eirora.Simulation.Time
{
    /// <summary>
    /// The rest period state machine and game-day counter (INV-T1–T8, ADR-0005).
    /// </summary>
    /// <remarks>
    /// Time arrives as <see cref="TimeReading"/>s whose zone and offset hold for the whole span since the previous
    /// reading; the client splits a span where the offset changes. A rest period starts at the schedule's local
    /// start time and ends at its local end time while the zone stays the same, so daylight saving time moves the
    /// real end (D-37). If the zone changes during rest, the end stays where it was in UTC (INV-T8).
    /// </remarks>
    internal sealed class DayCycle
    {
        private const long SecondsPerDay = 24 * 60 * 60;
        private const long ScheduleChangeDelaySeconds = SecondsPerDay;

        // The starts of the most recent game days, enough to read health data for the last seven (D-53).
        private const int RememberedDays = 9;
        private readonly List<long> _dayStarts = new();
        private int _firstStoredDay;

        private RestSchedule _schedule;
        private RestSchedule? _pendingSchedule;
        private long _pendingEffectiveUtc;
        private long _restEndLocal;
        private string _restZoneId = string.Empty;
        private int _restLastOffset;

        private DayCycle(TimeReading now, RestSchedule schedule)
        {
            Now = now;
            _schedule = schedule;
        }

        public TimeReading Now { get; private set; }

        public Phase Phase { get; private set; }

        /// <summary>Game day 0 begins at local midnight on the day the world is created (D-51); each dawn starts the next.</summary>
        public int GameDay { get; private set; }

        /// <summary>When the world was created; self-care from local midnight that day counts (D-51).</summary>
        public long CreatedUtc { get; private set; }

        public RestSchedule Schedule => _schedule;

        public RestSchedule? PendingSchedule => _pendingSchedule;

        /// <summary>Creates the cycle; installing during the rest window starts the world resting (D-50).</summary>
        public static DayCycle Create(TimeReading install, RestSchedule schedule)
        {
            var cycle = new DayCycle(install, schedule);
            cycle.CreatedUtc = install.UtcSeconds;
            cycle._dayStarts.Add((FloorDiv(install.LocalSeconds, SecondsPerDay) * SecondsPerDay) - install.OffsetSeconds);
            long start = MostRecentStartLocal(install.LocalSeconds, schedule);
            if (install.LocalSeconds < start + (schedule.DurationMinutes * 60L))
            {
                cycle.BeginRest(install, start);
            }

            return cycle;
        }

        /// <summary>Schedules a change that applies to rest periods starting 24 hours from now or later (INV-T7).</summary>
        public void RequestScheduleChange(RestSchedule schedule)
        {
            _pendingSchedule = schedule;
            _pendingEffectiveUtc = Now.UtcSeconds + ScheduleChangeDelaySeconds;
        }

        /// <summary>
        /// Moves time forward to <paramref name="target"/>, reporting each dusk and dawn on the way.
        /// <paramref name="onDawn"/> runs at each dawn after the game day has advanced.
        /// </summary>
        public void AdvanceTo(TimeReading target, List<GameEvent> events, Action<long> onDawn)
        {
            if (target.UtcSeconds < Now.UtcSeconds)
            {
                return; // Simulation time never runs backwards (INV-D8).
            }

            while (true)
            {
                if (Phase == Phase.Active)
                {
                    long start = NextRestStartUtc(target);
                    if (start > target.UtcSeconds)
                    {
                        break;
                    }

                    var at = new TimeReading(start, target.ZoneId, target.OffsetSeconds);
                    BeginRest(at, at.LocalSeconds);
                    events.Add(new DuskFell(start));
                }
                else
                {
                    if (string.Equals(target.ZoneId, _restZoneId, StringComparison.Ordinal))
                    {
                        _restLastOffset = target.OffsetSeconds;
                    }

                    long end = _restEndLocal - _restLastOffset;
                    if (end < Now.UtcSeconds)
                    {
                        end = Now.UtcSeconds;
                    }

                    if (end > target.UtcSeconds)
                    {
                        break;
                    }

                    Now = new TimeReading(end, target.ZoneId, target.OffsetSeconds);
                    Phase = Phase.Active;
                    GameDay++;
                    RememberDayStart(end);
                    events.Add(new DawnBroke(end, GameDay));
                    onDawn(end);
                }
            }

            Now = target;
        }

        /// <summary>
        /// When the rest under way ends, for a span whose zone and offset are <paramref name="span"/>'s: the moment
        /// <see cref="AdvanceTo"/> would break the dawn. Only while resting.
        /// </summary>
        public long RestEndUtc(TimeReading span)
        {
            int offset = string.Equals(span.ZoneId, _restZoneId, StringComparison.Ordinal) ? span.OffsetSeconds : _restLastOffset;
            long end = _restEndLocal - offset;
            return end < Now.UtcSeconds ? Now.UtcSeconds : end;
        }

        /// <summary>When a recent game day began: local midnight for day 0, otherwise its dawn; null if too old.</summary>
        public long? GameDayStartUtc(int day)
        {
            int index = day - _firstStoredDay;
            return index >= 0 && index < _dayStarts.Count ? _dayStarts[index] : (long?)null;
        }

        public void Write(StateWriter writer)
        {
            writer.Line("cycle").Long(Now.UtcSeconds).Text(Now.ZoneId).Long(Now.OffsetSeconds).Enum(Phase).Long(GameDay)
                .Long(_schedule.StartMinuteOfDay).Long(_schedule.DurationMinutes);
            writer.Line("created").Long(CreatedUtc);
            for (int i = 0; i < _dayStarts.Count; i++)
            {
                writer.Line("day-start").Long(_firstStoredDay + i).Long(_dayStarts[i]);
            }

            if (_pendingSchedule is RestSchedule pending)
            {
                writer.Line("pending-schedule").Long(pending.StartMinuteOfDay).Long(pending.DurationMinutes).Long(_pendingEffectiveUtc);
            }

            // The rest period's end is only kept while resting; the next dusk sets it anew.
            if (Phase == Phase.Resting)
            {
                writer.Line("rest").Long(_restEndLocal).Text(_restZoneId).Long(_restLastOffset);
            }
        }

        public static DayCycle Read(StateReader reader)
        {
            reader.Expect("cycle");
            var now = new TimeReading(reader.Long(), reader.Text(), reader.Int());
            var phase = reader.Enum<Phase>();
            int gameDay = reader.Int();
            var cycle = new DayCycle(now, ValidSchedule(reader.Int(), reader.Int())) { Phase = phase, GameDay = gameDay };
            reader.Expect("created");
            cycle.CreatedUtc = reader.Long();
            while (reader.Next("day-start"))
            {
                int day = reader.Int();
                if (cycle._dayStarts.Count == 0)
                {
                    cycle._firstStoredDay = day;
                }
                else if (day != cycle._firstStoredDay + cycle._dayStarts.Count)
                {
                    throw new FormatException("Game day starts follow each other day by day.");
                }

                cycle._dayStarts.Add(reader.Long());
            }

            if (reader.Next("pending-schedule"))
            {
                cycle._pendingSchedule = ValidSchedule(reader.Int(), reader.Int());
                cycle._pendingEffectiveUtc = reader.Long();
            }

            if (phase == Phase.Resting)
            {
                reader.Expect("rest");
                cycle._restEndLocal = reader.Long();
                cycle._restZoneId = reader.Text();
                cycle._restLastOffset = reader.Int();
            }

            return cycle;
        }

        private void RememberDayStart(long utcSeconds)
        {
            _dayStarts.Add(utcSeconds);
            if (_dayStarts.Count > RememberedDays)
            {
                _dayStarts.RemoveAt(0);
                _firstStoredDay++;
            }
        }

        private void BeginRest(TimeReading at, long startLocal)
        {
            Now = at;
            Phase = Phase.Resting;
            _restEndLocal = startLocal + (_schedule.DurationMinutes * 60L);
            _restZoneId = at.ZoneId;
            _restLastOffset = at.OffsetSeconds;
        }

        /// <summary>
        /// The next rest start at or after now, in UTC, using the offset of the span being advanced.
        /// </summary>
        /// <remarks>
        /// The coming night is anchored to the current schedule's next start. A pending schedule takes over that night
        /// only if its own start nearest the anchor also lies at or after the effective time; otherwise the current
        /// schedule rests one more night, so a change never skips a night (D-62). The decision depends on the night,
        /// not on when it is evaluated, so advancing time in parts gives the same nights (INV-D4).
        /// </remarks>
        private long NextRestStartUtc(TimeReading span)
        {
            long from = Now.UtcSeconds;
            long start = NextStartUtc(from, span.OffsetSeconds, _schedule);
            if (_pendingSchedule is RestSchedule pending && start >= _pendingEffectiveUtc)
            {
                long pendingStart = NearestStartUtc(start, span.OffsetSeconds, pending);
                if (pendingStart >= _pendingEffectiveUtc)
                {
                    _schedule = pending;
                    _pendingSchedule = null;
                    start = pendingStart > from ? pendingStart : from;
                }
            }

            return start;
        }

        /// <summary>The schedule's start within twelve hours either side of <paramref name="anchorUtc"/>.</summary>
        private static long NearestStartUtc(long anchorUtc, int offsetSeconds, RestSchedule schedule) =>
            MostRecentStartLocal(anchorUtc + offsetSeconds + (SecondsPerDay / 2), schedule) - offsetSeconds;

        private static long NextStartUtc(long fromUtc, int offsetSeconds, RestSchedule schedule)
        {
            long local = fromUtc + offsetSeconds;
            long start = MostRecentStartLocal(local, schedule);
            if (start < local)
            {
                start += SecondsPerDay;
            }

            return start - offsetSeconds;
        }

        private static long MostRecentStartLocal(long local, RestSchedule schedule)
        {
            long midnight = FloorDiv(local, SecondsPerDay) * SecondsPerDay;
            long start = midnight + (schedule.StartMinuteOfDay * 60L);
            return start > local ? start - SecondsPerDay : start;
        }

        private static RestSchedule ValidSchedule(int startMinuteOfDay, int durationMinutes)
        {
            var schedule = new RestSchedule(startMinuteOfDay, durationMinutes);
            return schedule.IsValid ? schedule : throw new FormatException("The rest schedule must be 6–10 hours and start within the day.");
        }

        private static long FloorDiv(long value, long divisor)
        {
            long quotient = value / divisor;
            return (value % divisor != 0 && (value < 0) != (divisor < 0)) ? quotient - 1 : quotient;
        }
    }
}
