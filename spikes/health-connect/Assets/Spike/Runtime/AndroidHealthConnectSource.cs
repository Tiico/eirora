#if UNITY_ANDROID && !UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

namespace Eirora.Spike.HealthConnect
{
    /// <summary>
    /// Calls the Kotlin HealthConnectBridge over JNI. The bridge answers on a background thread; awaiting the returned
    /// tasks resumes on Unity's main thread through its SynchronizationContext.
    /// </summary>
    public sealed class AndroidHealthConnectSource : IHealthConnectSource
    {
        private const string BridgeClass = "com.eirora.spike.healthconnect.bridge.HealthConnectBridge";
        private const string CallbackInterface = "com.eirora.spike.healthconnect.bridge.BridgeCallback";

        private readonly AndroidJavaClass _bridge = new AndroidJavaClass(BridgeClass);
        private readonly AndroidJavaObject _activity;
        private readonly Dictionary<string, string> _labels = new Dictionary<string, string>();

        public AndroidHealthConnectSource()
        {
            using var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            _activity = player.GetStatic<AndroidJavaObject>("currentActivity");
        }

        public string Name => "Health Connect";

        public async Task<HealthStatus> GetStatusAsync() => JsonUtility.FromJson<HealthStatus>(await Call("getStatus", _activity));

        public async Task<PermissionResult> RequestPermissionsAsync(IReadOnlyCollection<ReadPermission> permissions)
        {
            var keys = string.Join(",", permissions.Select(HealthKeys.Of));
            return JsonUtility.FromJson<PermissionResult>(await Call("requestPermissions", _activity, keys));
        }

        public async Task<RecordPage> ReadRecordsAsync(Habit habit, DateTimeOffset from, DateTimeOffset to)
        {
            var json = await Call("readRecords", _activity, HealthKeys.Of(habit), from.ToUnixTimeMilliseconds(), to.ToUnixTimeMilliseconds());
            return JsonUtility.FromJson<RecordPage>(json);
        }

        public async Task<DayAggregate[]> AggregateByDayAsync(Habit habit, DateTimeOffset from, DateTimeOffset to)
        {
            var json = await Call("aggregateByDay", _activity, HealthKeys.Of(habit), from.ToUnixTimeMilliseconds(), to.ToUnixTimeMilliseconds());
            return JsonUtility.FromJson<DayAggregates>(json).days;
        }

        public async Task StartChangeTrackingAsync(IReadOnlyCollection<Habit> habits) =>
            await Call("startChangeTracking", _activity, string.Join(",", habits.Select(HealthKeys.Of)));

        public async Task<ChangeSet> ReadChangesAsync() => JsonUtility.FromJson<ChangeSet>(await Call("readChanges", _activity));

        public void ScheduleBackgroundRead() => _bridge.CallStatic("scheduleBackgroundRead", _activity);

        public void RunBackgroundReadOnce(TimeSpan delay) => _bridge.CallStatic("runBackgroundReadOnce", _activity, (long)delay.TotalSeconds);

        public void CancelBackgroundRead() => _bridge.CallStatic("cancelBackgroundRead", _activity);

        public async Task<BackgroundRun[]> GetBackgroundRunsAsync() => JsonUtility.FromJson<BackgroundRuns>(await Call("getBackgroundRuns", _activity)).runs;

        public void ClearBackgroundRuns() => _bridge.CallStatic("clearBackgroundRuns", _activity);

        public bool OpenHealthConnectSettings() => _bridge.CallStatic<bool>("openHealthConnectSettings", _activity);

        public bool OpenProviderInstall() => _bridge.CallStatic<bool>("openProviderInstall", _activity);

        public string AppLabel(string packageName)
        {
            if (!_labels.TryGetValue(packageName, out var label))
            {
                label = _bridge.CallStatic<string>("appLabel", _activity, packageName);
                _labels[packageName] = label;
            }

            return label;
        }

        private Task<string> Call(string method, params object[] arguments)
        {
            var callback = new BridgeCallback();
            var withCallback = new object[arguments.Length + 1];
            arguments.CopyTo(withCallback, 0);
            withCallback[arguments.Length] = callback;
            _bridge.CallStatic(method, withCallback);
            return callback.Result;
        }

        /// <summary>Implements the Kotlin BridgeCallback interface. Kept alive until Java has answered.</summary>
        private sealed class BridgeCallback : AndroidJavaProxy
        {
            private static readonly HashSet<BridgeCallback> Pending = new HashSet<BridgeCallback>();

            private readonly TaskCompletionSource<string> _result = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

            public BridgeCallback()
                : base(CallbackInterface)
            {
                lock (Pending)
                {
                    Pending.Add(this);
                }
            }

            public Task<string> Result => _result.Task;

            // Dispatching here instead of by reflection keeps IL2CPP stripping from removing the callback methods.
            public override AndroidJavaObject? Invoke(string methodName, object[] args)
            {
                switch (methodName)
                {
                    case "onSuccess":
                        Release();
                        _result.TrySetResult((string)args[0]);
                        return null;
                    case "onError":
                        Release();
                        _result.TrySetException(new HealthConnectException((string)args[0], (string)args[1]));
                        return null;
                    default:
                        return base.Invoke(methodName, args);
                }
            }

            private void Release()
            {
                lock (Pending)
                {
                    Pending.Remove(this);
                }
            }
        }
    }
}
#endif
