using System.Collections.Generic;
using System.Text;
using Eirora.Simulation.Forces;

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

        public void Record(int day, Habit habit, ContributionSource source, int total) => _totals[(day, habit, source)] = total;

        /// <summary>The game days whose force can change when <paramref name="habit"/> changes on <paramref name="day"/>.</summary>
        public static IEnumerable<int> AffectedDays(int day, Habit habit, int currentDay)
        {
            int last = habit == Habit.Workouts ? day + WorkoutWindowDays - 1 : day;
            for (int d = day; d <= last && d <= currentDay; d++)
            {
                yield return d;
            }
        }

        /// <summary>The force a game day's habit is worth now, and the force that day has been credited.</summary>
        public (Force Force, int Expected, int Credited) Reconcile(int day, Habit habit)
        {
            int expected = habit switch
            {
                Habit.Steps => ForceRules.Exploration(Effective(day, Habit.Steps) ?? 0),
                Habit.Workouts => ForceRules.BuildPower(SessionsBefore(day), Effective(day, Habit.Workouts) ?? 0),
                _ => ForceRules.DawnPower(Effective(day, Habit.Sleep)),
            };

            _credited.TryGetValue((day, habit), out int credited);
            _credited[(day, habit)] = expected;
            return (ForceOf(habit), expected, credited);
        }

        /// <summary>Marks a day's force as already credited, such as the guided tour's Dawn Power on day 0.</summary>
        public void MarkCredited(int day, Habit habit, int amount) => _credited[(day, habit)] = amount;

        public void AppendFingerprint(StringBuilder text)
        {
            text.Append("selfcare:");
            foreach (var entry in _totals)
            {
                text.Append(entry.Key.Day).Append('/').Append((int)entry.Key.Habit).Append('/')
                    .Append((int)entry.Key.Source).Append('=').Append(entry.Value).Append(',');
            }

            text.Append("credited:");
            foreach (var entry in _credited)
            {
                text.Append(entry.Key.Day).Append('/').Append((int)entry.Key.Habit).Append('=').Append(entry.Value).Append(',');
            }

            text.Append(';');
        }

        private int? Effective(int day, Habit habit)
        {
            bool hasVerified = _totals.TryGetValue((day, habit, ContributionSource.Verified), out int verified);
            bool hasSelfReported = _totals.TryGetValue((day, habit, ContributionSource.SelfReported), out int selfReported);
            if (!hasVerified && !hasSelfReported)
            {
                return null;
            }

            return verified > selfReported ? verified : selfReported;
        }

        private int SessionsBefore(int day)
        {
            int sessions = 0;
            for (int d = day - WorkoutWindowDays + 1; d < day; d++)
            {
                sessions += Effective(d, Habit.Workouts) ?? 0;
            }

            return sessions;
        }

        private static Force ForceOf(Habit habit) => habit switch
        {
            Habit.Steps => Force.Exploration,
            Habit.Workouts => Force.BuildPower,
            _ => Force.DawnPower,
        };
    }
}
