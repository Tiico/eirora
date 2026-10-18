using System.Collections.Generic;
using System.Threading.Tasks;
using Eirora.Simulation.SelfCare;

namespace Eirora.Client.Health
{
    /// <summary>
    /// A health store on the device: Health Connect now, HealthKit in M6, behind one interface (HLT-02, ADR-0007).
    /// Only totals cross this boundary; raw records stay in the platform adapter (HLT-10).
    /// </summary>
    public interface IHealthSource
    {
        Task<HealthStatus> StatusAsync();

        /// <summary>Asks for read access to <paramref name="habits"/>; answers with every habit granted afterwards.</summary>
        Task<IReadOnlyCollection<Habit>> RequestAccessAsync(IReadOnlyCollection<Habit> habits);

        /// <summary>
        /// The store's total for a habit between two UTC times (seconds) and the part it marks as manually entered:
        /// steps, workout sessions of at least ten minutes, or sleep minutes.
        /// </summary>
        Task<HealthTotal> ReadAsync(Habit habit, long fromUtc, long toUtc);

        /// <summary>Opens the store's settings, where access can be granted again after the dialog stops appearing.</summary>
        bool OpenSettings();
    }

    /// <summary>A total from the health store and its manually entered part (D-68).</summary>
    public readonly struct HealthTotal
    {
        public HealthTotal(long total, long manual)
        {
            Total = total;
            Manual = manual < total ? manual : total;
        }

        public long Total { get; }

        public long Manual { get; }

        /// <summary>What the store recorded by itself, counted as verified (D-68).</summary>
        public long Verified => Total - Manual;
    }

    /// <summary>Whether the store can be used, and which habits the player has let the game read.</summary>
    public sealed class HealthStatus
    {
        public HealthStatus(HealthAvailability availability, IReadOnlyCollection<Habit> granted)
        {
            Availability = availability;
            Granted = granted;
        }

        public static HealthStatus Unavailable { get; } = new(HealthAvailability.Unavailable, new Habit[0]);

        public HealthAvailability Availability { get; }

        public IReadOnlyCollection<Habit> Granted { get; }
    }

    public enum HealthAvailability
    {
        Available,

        /// <summary>Health Connect needs installing or updating from the Play Store (Android 13 and lower).</summary>
        UpdateRequired,

        Unavailable,
    }
}
