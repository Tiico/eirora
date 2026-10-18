using System.Collections.Generic;
using Eirora.Simulation.Forces;
using Eirora.Simulation.Serialization;

namespace Eirora.Simulation.SelfCare
{
    /// <summary>
    /// Daily self-care totals per game day and habit, and the force each day is worth (forces.md).
    /// </summary>
    /// <remarks>
    /// Contributions are daily totals, so recording the same total again changes nothing (INV-B1). When both a
    /// verified and a self-reported total exist for the same day and habit, the higher one counts, never the sum.
    /// In the player's own world the source never changes the force (INV-F7).
    /// </remarks>
    internal sealed class SelfCareLedger
    {
        private const int WorkoutWindowDays = 7;

        private readonly SortedDictionary<(int Day, Habit Habit, ContributionSource Source), int> _totals = new();

        // Force each (day, habit) has been credited so far, before the soft cap.
        private readonly SortedDictionary<(int Day, Habit Habit), int> _credited = new();

        // Half forces each (day, habit) actually added to the balance, after the soft cap (D-36).
        private readonly SortedDictionary<(int Day, Habit Habit), long> _gainedHalves = new();

        // How many times the force had been spent when each (day, habit) was credited while it held no credit (D-74).
        private readonly SortedDictionary<(int Day, Habit Habit), long> _spendsAtCredit = new();

        // Build Power each game day with workouts is worth, before the soft cap (D-63). Derived from the totals, so it
        // is not part of the state that is written; values from _buildPowerStaleFrom on are recomputed before they are
        // read, which recording the totals again on reading sets up.
        private readonly SortedDictionary<int, int> _buildPower = new();
        private int _buildPowerStaleFrom = int.MaxValue;

        public void Record(int day, Habit habit, ContributionSource source, int total)
        {
            _totals[(day, habit, source)] = total;
            if (habit == Habit.Workouts && day < _buildPowerStaleFrom)
            {
                _buildPowerStaleFrom = day;
            }
        }

        /// <summary>The game days whose force can change when <paramref name="habit"/> changes on <paramref name="day"/>.</summary>
        public IEnumerable<int> AffectedDays(int day, Habit habit, int currentDay)
        {
            yield return day;
            if (habit != Habit.Workouts)
            {
                yield break;
            }

            // Each day's Build Power depends on what the six days before it gave, so a change can ripple through
            // every later day with workouts.
            for (int d = day + 1; d <= currentDay; d++)
            {
                if (Sessions(d) > 0)
                {
                    yield return d;
                }
            }
        }

        /// <summary>The force a game day's habit is worth now, and the force that day has been credited.</summary>
        public (Force Force, int Expected, int Credited) Reconcile(int day, Habit habit)
        {
            int expected = habit switch
            {
                Habit.Steps => ForceRules.Exploration(Effective(day, Habit.Steps) ?? 0),
                Habit.Workouts => BuildPower(day),
                _ => ForceRules.DawnPower(Effective(day, Habit.Sleep)),
            };

            _credited.TryGetValue((day, habit), out int credited);
            _credited[(day, habit)] = expected;
            return (ForceOf(habit), expected, credited);
        }

        /// <summary>Marks a day's force as already credited, such as the guided tour's Dawn Power on day 0.</summary>
        public void MarkCredited(int day, Habit habit, int amount) => _credited[(day, habit)] = amount;

        /// <summary>The half forces a game day's habit has added to the balance so far.</summary>
        public long GainedHalves(int day, Habit habit) => _gainedHalves.TryGetValue((day, habit), out long halves) ? halves : 0;

        /// <summary>
        /// Adds <paramref name="halves"/> to what a game day's habit has added; negative after a correction. A
        /// correction before any spending can take back more than the habit added, but never leaves it below zero.
        /// </summary>
        public void AddGainedHalves(int day, Habit habit, long halves)
        {
            long gained = GainedHalves(day, habit) + halves;
            _gainedHalves[(day, habit)] = gained > 0 ? gained : 0;
        }

        /// <summary>
        /// Notes that a game day's habit was credited when its force had been spent <paramref name="spends"/> times,
        /// unless it still holds older credit.
        /// </summary>
        public void NoteCredit(int day, Habit habit, long spends) => _spendsAtCredit.TryAdd((day, habit), spends);

        /// <summary>
        /// Whether the force has been spent since the oldest credit a game day's habit still holds, so that a
        /// correction can take back no more than the habit added (D-62, D-74). Credit not made through
        /// <see cref="NoteCredit"/>, such as the guided tour's, counts as spent.
        /// </summary>
        public bool SpentSinceCredit(int day, Habit habit, long spends) =>
            !_spendsAtCredit.TryGetValue((day, habit), out long atCredit) || atCredit != spends;

        /// <summary>Forgets a game day's habit's credit once a correction has taken all of it back.</summary>
        public void ForgetCredit(int day, Habit habit)
        {
            _gainedHalves.Remove((day, habit));
            _spendsAtCredit.Remove((day, habit));
        }

        public void Write(StateWriter writer)
        {
            foreach (var entry in _totals)
            {
                writer.Line("selfcare").Long(entry.Key.Day).Enum(entry.Key.Habit).Enum(entry.Key.Source).Long(entry.Value);
            }

            foreach (var entry in _credited)
            {
                writer.Line("credited").Long(entry.Key.Day).Enum(entry.Key.Habit).Long(entry.Value);
            }

            foreach (var entry in _gainedHalves)
            {
                writer.Line("gained").Long(entry.Key.Day).Enum(entry.Key.Habit).Long(entry.Value);
            }

            foreach (var entry in _spendsAtCredit)
            {
                writer.Line("spends-at-credit").Long(entry.Key.Day).Enum(entry.Key.Habit).Long(entry.Value);
            }
        }

        public static SelfCareLedger Read(StateReader reader)
        {
            var ledger = new SelfCareLedger();
            while (reader.Next("selfcare"))
            {
                int day = reader.Int();
                var habit = reader.Enum<Habit>();
                ledger.Record(day, habit, reader.Enum<ContributionSource>(), reader.Int());
            }

            while (reader.Next("credited"))
            {
                ledger._credited[(reader.Int(), reader.Enum<Habit>())] = reader.Int();
            }

            while (reader.Next("gained"))
            {
                ledger._gainedHalves[(reader.Int(), reader.Enum<Habit>())] = reader.Long();
            }

            while (reader.Next("spends-at-credit"))
            {
                ledger._spendsAtCredit[(reader.Int(), reader.Enum<Habit>())] = reader.Long();
            }

            return ledger;
        }

        /// <summary>The total recorded for a game day, habit and source, or null when nothing is recorded.</summary>
        public int? Total(int day, Habit habit, ContributionSource source) =>
            _totals.TryGetValue((day, habit, source), out int total) ? total : (int?)null;

        /// <summary>
        /// The health store's total (its verified and manually entered parts) or the player's own input, whichever is
        /// higher; never their sum, so a day completed by hand is not counted twice (forces.md, section 6).
        /// </summary>
        private int? Effective(int day, Habit habit)
        {
            int? verified = Total(day, habit, ContributionSource.Verified);
            int? storeManual = Total(day, habit, ContributionSource.HealthStoreManualEntry);
            int? selfReported = Total(day, habit, ContributionSource.SelfReported);
            if (verified is null && storeManual is null && selfReported is null)
            {
                return null;
            }

            int store = (verified ?? 0) + (storeManual ?? 0);
            int own = selfReported ?? 0;
            return store > own ? store : own;
        }

        private int Sessions(int day) => Effective(day, Habit.Workouts) ?? 0;

        /// <summary>
        /// The Build Power a game day is worth, computed forwards from the first day that may have changed, since each
        /// day depends on the six before it.
        /// </summary>
        private int BuildPower(int day)
        {
            for (int d = _buildPowerStaleFrom; d <= day; d++)
            {
                int sessionsToday = Sessions(d);
                if (sessionsToday == 0)
                {
                    _buildPower.Remove(d);
                    continue;
                }

                int sessionsBefore = 0, buildPowerBefore = 0;
                for (int before = d - WorkoutWindowDays + 1; before < d; before++)
                {
                    sessionsBefore += Sessions(before);
                    buildPowerBefore += _buildPower.TryGetValue(before, out int gave) ? gave : 0;
                }

                _buildPower[d] = ForceRules.BuildPower(sessionsBefore, sessionsToday, buildPowerBefore);
            }

            if (day >= _buildPowerStaleFrom)
            {
                _buildPowerStaleFrom = day + 1;
            }

            return _buildPower.TryGetValue(day, out int value) ? value : 0;
        }

        private static Force ForceOf(Habit habit) => habit switch
        {
            Habit.Steps => Force.Exploration,
            Habit.Workouts => Force.BuildPower,
            _ => Force.DawnPower,
        };
    }
}
