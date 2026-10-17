using System;

namespace Eirora.Simulation.Time
{
    /// <summary>
    /// The player's recurring rest period in local wall-clock time (GLOSSARY: Viloperiod, D-22, D-37).
    /// </summary>
    public readonly struct RestSchedule : IEquatable<RestSchedule>
    {
        public const int MinimumMinutes = 6 * 60;
        public const int MaximumMinutes = 10 * 60;
        public const int MinutesPerDay = 24 * 60;

        public RestSchedule(int startMinuteOfDay, int durationMinutes)
        {
            StartMinuteOfDay = startMinuteOfDay;
            DurationMinutes = durationMinutes;
        }

        /// <summary>Local start, in minutes after midnight.</summary>
        public int StartMinuteOfDay { get; }

        /// <summary>Length in local wall-clock minutes, 6–10 hours (INV-T6).</summary>
        public int DurationMinutes { get; }

        public bool IsValid =>
            StartMinuteOfDay >= 0 && StartMinuteOfDay < MinutesPerDay
            && DurationMinutes >= MinimumMinutes && DurationMinutes <= MaximumMinutes;

        public bool Equals(RestSchedule other) =>
            StartMinuteOfDay == other.StartMinuteOfDay && DurationMinutes == other.DurationMinutes;

        public override bool Equals(object? obj) => obj is RestSchedule other && Equals(other);

        public override int GetHashCode() => (StartMinuteOfDay * MinutesPerDay) + DurationMinutes;

        public static bool operator ==(RestSchedule left, RestSchedule right) => left.Equals(right);

        public static bool operator !=(RestSchedule left, RestSchedule right) => !left.Equals(right);
    }
}
