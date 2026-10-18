using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Eirora.Spike.HealthConnect
{
    /// <summary>
    /// What the spike needs from Health Connect. The Android implementation calls the Kotlin bridge; the fake runs in the
    /// Editor. Failures surface as <see cref="HealthConnectException"/>; a denied permission is a result, not a failure.
    /// </summary>
    public interface IHealthConnectSource
    {
        /// <summary>Shown in the UI so that fake data is never mistaken for real data.</summary>
        string Name { get; }

        Task<HealthStatus> GetStatusAsync();

        Task<PermissionResult> RequestPermissionsAsync(IReadOnlyCollection<ReadPermission> permissions);

        Task<RecordPage> ReadRecordsAsync(Habit habit, DateTimeOffset from, DateTimeOffset to);

        Task<DayAggregate[]> AggregateByDayAsync(Habit habit, DateTimeOffset from, DateTimeOffset to);

        Task StartChangeTrackingAsync(IReadOnlyCollection<Habit> habits);

        Task<ChangeSet> ReadChangesAsync();

        void ScheduleBackgroundRead();

        void RunBackgroundReadOnce(TimeSpan delay);

        void CancelBackgroundRead();

        Task<BackgroundRun[]> GetBackgroundRunsAsync();

        void ClearBackgroundRuns();

        /// <summary>Opens the app's Health Connect permissions, where a denial can be undone. False if nothing could be opened.</summary>
        bool OpenHealthConnectSettings();

        /// <summary>Opens the Play Store to install or update Health Connect. False if nothing could be opened.</summary>
        bool OpenProviderInstall();

        /// <summary>The app name behind a data origin, or the package name if it is not known.</summary>
        string AppLabel(string packageName);
    }
}
