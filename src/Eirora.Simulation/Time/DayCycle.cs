using System;
using System.Collections.Generic;
using System.Text;
using Eirora.Simulation.Events;

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

        public RestSchedule Schedule => _schedule;

        public RestSchedule? PendingSchedule => _pendingSchedule;

        /// <summary>Creates the cycle; installing during the rest window starts the world resting (D-50).</summary>
        public static DayCycle Create(TimeReading install, RestSchedule schedule)
        {
            var cycle = new DayCycle(install, schedule);
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
                    events.Add(new DawnBroke(end, GameDay));
                    onDawn(end);
                }
            }

            Now = target;
        }

        public void AppendFingerprint(StringBuilder text)
        {
            text.Append("cycle:").Append(Now.UtcSeconds).Append(',').Append(Now.ZoneId).Append(',').Append(Now.OffsetSeconds)
                .Append(',').Append((int)Phase).Append(',').Append(GameDay)
                .Append(',').Append(_schedule.StartMinuteOfDay).Append(',').Append(_schedule.DurationMinutes);
            if (_pendingSchedule is RestSchedule pending)
            {
                text.Append(",pending:").Append(pending.StartMinuteOfDay).Append(',').Append(pending.DurationMinutes)
                    .Append(',').Append(_pendingEffectiveUtc);
            }

            if (Phase == Phase.Resting)
            {
                text.Append(",rest:").Append(_restEndLocal).Append(',').Append(_restZoneId).Append(',').Append(_restLastOffset);
            }

            text.Append(';');
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
        /// A pending schedule takes over once both its next start and the current schedule's lie at or after its
        /// effective time. When only the current one does, it rests one more night, so a change never skips a night.
        /// </summary>
        private long NextRestStartUtc(TimeReading span)
        {
            long from = Now.UtcSeconds;
            long start = NextStartUtc(from, span.OffsetSeconds, _schedule);
            if (_pendingSchedule is RestSchedule pending && start >= _pendingEffectiveUtc)
            {
                long pendingStart = NextStartUtc(from, span.OffsetSeconds, pending);
                if (pendingStart >= _pendingEffectiveUtc)
                {
                    _schedule = pending;
                    _pendingSchedule = null;
                    start = pendingStart;
                }
            }

            return start;
        }

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

        private static long FloorDiv(long value, long divisor)
        {
            long quotient = value / divisor;
            return (value % divisor != 0 && (value < 0) != (divisor < 0)) ? quotient - 1 : quotient;
        }
    }
}
