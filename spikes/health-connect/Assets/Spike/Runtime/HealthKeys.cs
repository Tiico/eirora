using System;

namespace Eirora.Spike.HealthConnect
{
    /// <summary>The names the Kotlin bridge uses for habits and permissions, and the permission strings Health Connect reports.</summary>
    public static class HealthKeys
    {
        public static string Of(Habit habit) => habit switch
        {
            Habit.Steps => "steps",
            Habit.Exercise => "exercise",
            Habit.Sleep => "sleep",
            _ => throw new ArgumentOutOfRangeException(nameof(habit), habit, null),
        };

        public static string Of(ReadPermission permission) => permission switch
        {
            ReadPermission.Steps => "steps",
            ReadPermission.Exercise => "exercise",
            ReadPermission.Sleep => "sleep",
            ReadPermission.Background => "background",
            ReadPermission.History => "history",
            _ => throw new ArgumentOutOfRangeException(nameof(permission), permission, null),
        };

        /// <summary>The Android permission string, as returned in <see cref="HealthStatus.granted"/>.</summary>
        public static string AndroidPermission(ReadPermission permission) => permission switch
        {
            ReadPermission.Steps => "android.permission.health.READ_STEPS",
            ReadPermission.Exercise => "android.permission.health.READ_EXERCISE",
            ReadPermission.Sleep => "android.permission.health.READ_SLEEP",
            ReadPermission.Background => "android.permission.health.READ_HEALTH_DATA_IN_BACKGROUND",
            ReadPermission.History => "android.permission.health.READ_HEALTH_DATA_HISTORY",
            _ => throw new ArgumentOutOfRangeException(nameof(permission), permission, null),
        };

        public static ReadPermission PermissionFor(Habit habit) => habit switch
        {
            Habit.Steps => ReadPermission.Steps,
            Habit.Exercise => ReadPermission.Exercise,
            Habit.Sleep => ReadPermission.Sleep,
            _ => throw new ArgumentOutOfRangeException(nameof(habit), habit, null),
        };
    }
}
