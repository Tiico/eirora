using System;

namespace Eirora.Spike.HealthConnect
{
    // Data transfer objects for the JSON the Kotlin bridge returns (RecordJson.kt, HealthConnectBridge.kt).
    // Public fields because JsonUtility only fills fields. They can hold health data: keep them in memory only.

    /// <summary>The three habits of HLT-01.</summary>
    public enum Habit
    {
        Steps,
        Exercise,
        Sleep,
    }

    /// <summary>A Health Connect read permission the spike can ask for.</summary>
    public enum ReadPermission
    {
        Steps,
        Exercise,
        Sleep,
        Background,
        History,
    }

    [Serializable]
    public sealed class HealthStatus
    {
        public string sdkStatus = "";
        public int androidSdk;
        public string androidRelease = "";
        public string deviceModel = "";
        public string[] providerVersions = Array.Empty<string>();
        public string[] granted = Array.Empty<string>();
        public string backgroundFeature = "";
        public string historyFeature = "";
    }

    [Serializable]
    public sealed class PermissionResult
    {
        public string[] requested = Array.Empty<string>();
        public string[] grantedNow = Array.Empty<string>();
        public string[] granted = Array.Empty<string>();
    }

    [Serializable]
    public sealed class HealthRecord
    {
        public string type = "";
        public string id = "";
        public long startMs;
        public long endMs;
        public bool hasStartOffset;
        public int startOffsetSec;
        public bool hasEndOffset;
        public int endOffsetSec;

        public string origin = "";
        public int recordingMethod;
        public string recordingMethodName = "";
        public long lastModifiedMs;
        public string clientRecordId = "";
        public long clientRecordVersion;
        public bool hasDevice;
        public int deviceType;
        public string deviceTypeName = "";
        public string deviceManufacturer = "";
        public string deviceModel = "";

        public long count;

        public int exerciseType;
        public string exerciseTypeName = "";
        public string title = "";
        public int segmentCount;
        public int lapCount;

        public SleepStage[] stages = Array.Empty<SleepStage>();

        public Habit Habit => type switch
        {
            "steps" => Habit.Steps,
            "exercise" => Habit.Exercise,
            "sleep" => Habit.Sleep,
            _ => throw new InvalidOperationException($"Unknown record type '{type}'"),
        };

        public DateTimeOffset Start => DateTimeOffset.FromUnixTimeMilliseconds(startMs);

        public DateTimeOffset End => DateTimeOffset.FromUnixTimeMilliseconds(endMs);

        /// <summary>Steps for a steps record, otherwise the duration in seconds.</summary>
        public long Value => Habit == Habit.Steps ? count : (endMs - startMs) / 1000;
    }

    [Serializable]
    public sealed class SleepStage
    {
        public int stage;
        public string stageName = "";
        public long startMs;
        public long endMs;
    }

    [Serializable]
    public sealed class RecordPage
    {
        public HealthRecord[] records = Array.Empty<HealthRecord>();
        public bool truncated;
    }

    /// <summary>Health Connect's deduplicated total for one local day: steps, or seconds for exercise and sleep.</summary>
    [Serializable]
    public sealed class DayAggregate
    {
        public string day = "";
        public bool hasValue;
        public long value;
        public string[] origins = Array.Empty<string>();
    }

    [Serializable]
    public sealed class DayAggregates
    {
        public DayAggregate[] days = Array.Empty<DayAggregate>();
    }

    [Serializable]
    public sealed class ChangeSet
    {
        public bool expired;
        public HealthRecord[] upserts = Array.Empty<HealthRecord>();
        public string[] deletions = Array.Empty<string>();
    }

    /// <summary>The outcome of one background read. Holds no health value.</summary>
    [Serializable]
    public sealed class BackgroundRun
    {
        public long atMs;
        public string outcome = "";
        public int recordCount;
        public int importance;

        /// <summary>ActivityManager importance above IMPORTANCE_VISIBLE (200) means no part of the app was on screen.</summary>
        public bool InBackground => importance > 200;
    }

    [Serializable]
    public sealed class BackgroundRuns
    {
        public BackgroundRun[] runs = Array.Empty<BackgroundRun>();
    }

    /// <summary>A bridge call that failed. <see cref="Code"/> is one of the codes in HealthConnectBridge.errorCode.</summary>
    public sealed class HealthConnectException : Exception
    {
        public HealthConnectException(string code, string message)
            : base(message)
        {
            Code = code;
        }

        public string Code { get; }

        public bool PermissionDenied => Code == "permission_denied";
    }
}
