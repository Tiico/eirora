using System;

namespace Eirora.Simulation.Forces
{
    /// <summary>
    /// Converts one game day's self-care into forces, rules v1 (docs/design/forces.md).
    /// </summary>
    /// <remarks>
    /// Each curve is piecewise linear with non-increasing slopes, so it is increasing, concave and bounded (INV-F3).
    /// Curves are evaluated exactly as an integer numerator over a fixed denominator and rounded down once (INV-D3).
    /// </remarks>
    public static class ForceRules
    {
        /// <summary>Most Exploration one game day of steps can give.</summary>
        public const int ExplorationCap = 110;

        /// <summary>Dawn Power for a game day without sleep data; no logged sleep gives less (D-16, INV-F4).</summary>
        public const int DawnPowerBaseline = 40;

        /// <summary>Most Dawn Power one game day of sleep can give.</summary>
        public const int DawnPowerCap = 100;

        /// <summary>Most Build Power that workouts within seven game days can give.</summary>
        public const int BuildPowerWeeklyCap = 345;

        /// <summary>Shortest workout that counts as a session (D-42).</summary>
        public const int MinimumWorkoutMinutes = 10;

        /// <summary>Steps credited per manually entered active minute (D-41).</summary>
        public const int StepsPerActiveMinute = 100;

        // Exploration per step: 15, 7.5, 2.5 and 1.25 per 1 000 steps, i.e. 12, 6, 2 and 1 per 800 steps.
        internal const int ExplorationDenominator = 800;
        private static readonly int[] s_explorationSegmentEnds = { 4_000, 8_000, 12_000, 20_000 };
        private static readonly int[] s_explorationSlopes = { 12, 6, 2, 1 };

        // Dawn Power per minute of sleep: 8 per hour up to 7 h, then 2 per hour up to 9 h.
        internal const int DawnPowerDenominator = 60;
        private static readonly int[] s_sleepSegmentEnds = { 420, 540 };
        private static readonly int[] s_sleepSlopes = { 8, 2 };

        // Build Power accumulated by the n-th workout within seven game days.
        private static readonly int[] s_cumulativeBuildPower = { 0, 100, 190, 270, 320, BuildPowerWeeklyCap };

        /// <summary>Exploration for a game day with <paramref name="steps"/> steps in total.</summary>
        public static int Exploration(int steps) => (int)(ExplorationNumerator(steps) / ExplorationDenominator);

        /// <summary>
        /// Dawn Power for a game day; <paramref name="sleepMinutes"/> is null when there is no sleep data.
        /// </summary>
        public static int DawnPower(int? sleepMinutes) =>
            sleepMinutes is int minutes ? (int)(DawnPowerNumerator(minutes) / DawnPowerDenominator) : DawnPowerBaseline;

        /// <summary>
        /// Build Power for a game day's workouts, given the sessions in the six game days before it.
        /// Diminishing returns count per rolling week, so rest days never cost anything.
        /// </summary>
        public static int BuildPower(int sessionsInPreviousSixDays, int sessionsToday)
        {
            RequireNonNegative(sessionsInPreviousSixDays, nameof(sessionsInPreviousSixDays));
            RequireNonNegative(sessionsToday, nameof(sessionsToday));
            return CumulativeBuildPowerAt(sessionsInPreviousSixDays + (long)sessionsToday)
                - CumulativeBuildPowerAt(sessionsInPreviousSixDays);
        }

        /// <summary>Whether a workout of <paramref name="durationMinutes"/> counts as a session (D-42).</summary>
        public static bool CountsAsWorkout(int durationMinutes)
        {
            RequireNonNegative(durationMinutes, nameof(durationMinutes));
            return durationMinutes >= MinimumWorkoutMinutes;
        }

        /// <summary>Steps credited for manually entered active minutes (D-41).</summary>
        public static int StepsFromActiveMinutes(int activeMinutes)
        {
            RequireNonNegative(activeMinutes, nameof(activeMinutes));
            return checked(activeMinutes * StepsPerActiveMinute);
        }

        /// <summary>Exploration before rounding, in units of 1/<see cref="ExplorationDenominator"/>.</summary>
        internal static long ExplorationNumerator(int steps)
        {
            RequireNonNegative(steps, nameof(steps));
            return PiecewiseLinear(steps, s_explorationSegmentEnds, s_explorationSlopes);
        }

        /// <summary>Dawn Power before rounding, in units of 1/<see cref="DawnPowerDenominator"/>.</summary>
        internal static long DawnPowerNumerator(int sleepMinutes)
        {
            RequireNonNegative(sleepMinutes, nameof(sleepMinutes));
            return ((long)DawnPowerBaseline * DawnPowerDenominator) + PiecewiseLinear(sleepMinutes, s_sleepSegmentEnds, s_sleepSlopes);
        }

        private static int CumulativeBuildPowerAt(long sessions) =>
            s_cumulativeBuildPower[sessions < s_cumulativeBuildPower.Length ? (int)sessions : s_cumulativeBuildPower.Length - 1];

        private static long PiecewiseLinear(int x, int[] segmentEnds, int[] slopes)
        {
            long total = 0;
            int start = 0;
            for (int i = 0; i < segmentEnds.Length && x > start; i++)
            {
                int end = x < segmentEnds[i] ? x : segmentEnds[i];
                total += (long)(end - start) * slopes[i];
                start = segmentEnds[i];
            }

            return total;
        }

        private static void RequireNonNegative(int value, string name)
        {
            if (value < 0)
            {
                throw new ArgumentOutOfRangeException(name, value, "Must not be negative.");
            }
        }
    }
}
