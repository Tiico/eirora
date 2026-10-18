#if UNITY_ANDROID && !UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Eirora.Simulation.SelfCare;
using UnityEngine;

namespace Eirora.Client.Health
{
    /// <summary>
    /// Health Connect through the Kotlin HealthBridge (ADR-0007). The bridge answers on a background thread; awaiting
    /// the returned tasks resumes on Unity's main thread through its SynchronizationContext.
    /// </summary>
    public sealed class AndroidHealthSource : IHealthSource
    {
        private const string BridgeClass = "com.eirora.health.HealthBridge";
        private const string CallbackInterface = "com.eirora.health.BridgeCallback";

        private readonly AndroidJavaClass _bridge = new(BridgeClass);
        private readonly AndroidJavaObject _activity;

        public AndroidHealthSource()
        {
            using var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            _activity = player.GetStatic<AndroidJavaObject>("currentActivity");
        }

        public async Task<HealthStatus> StatusAsync()
        {
            var status = JsonUtility.FromJson<StatusJson>(await Call("getStatus", _activity));
            var availability = status.sdkStatus switch
            {
                "available" => HealthAvailability.Available,
                "update_required" => HealthAvailability.UpdateRequired,
                _ => HealthAvailability.Unavailable,
            };
            return new HealthStatus(availability, Habits(status.granted));
        }

        public async Task<IReadOnlyCollection<Habit>> RequestAccessAsync(IReadOnlyCollection<Habit> habits)
        {
            var json = await Call("requestPermissions", _activity, string.Join(",", habits.Select(Key)));
            return Habits(JsonUtility.FromJson<StatusJson>(json).granted);
        }

        public async Task<HealthTotal> ReadAsync(Habit habit, long fromUtc, long toUtc)
        {
            var json = await Call("readTotal", _activity, Key(habit), fromUtc * 1000, toUtc * 1000);
            var total = JsonUtility.FromJson<TotalJson>(json);
            return new HealthTotal(total.total, total.manual);
        }

        public bool OpenSettings() =>
            _bridge.CallStatic<bool>("openHealthConnectSettings", _activity) || _bridge.CallStatic<bool>("openProviderInstall", _activity);

        private static string Key(Habit habit) => habit switch
        {
            Habit.Steps => "steps",
            Habit.Workouts => "exercise",
            _ => "sleep",
        };

        private static Habit[] Habits(string[]? keys) => (keys ?? new string[0]).Select(key => key switch
        {
            "steps" => Habit.Steps,
            "exercise" => Habit.Workouts,
            _ => Habit.Sleep,
        }).ToArray();

        private Task<string> Call(string method, params object[] arguments)
        {
            var callback = new BridgeCallback();
            var withCallback = new object[arguments.Length + 1];
            arguments.CopyTo(withCallback, 0);
            withCallback[arguments.Length] = callback;
            _bridge.CallStatic(method, withCallback);
            return callback.Result;
        }

        [Serializable]
        private sealed class StatusJson
        {
#pragma warning disable CS0649 // Filled in by JsonUtility.
            public string sdkStatus = string.Empty;
            public string[]? granted;
#pragma warning restore CS0649
        }

        [Serializable]
        private sealed class TotalJson
        {
#pragma warning disable CS0649 // Filled in by JsonUtility.
            public long total;
            public long manual;
#pragma warning restore CS0649
        }

        /// <summary>Implements the Kotlin BridgeCallback interface. Kept alive until Java has answered.</summary>
        private sealed class BridgeCallback : AndroidJavaProxy
        {
            private static readonly HashSet<BridgeCallback> s_pending = new();

            private readonly TaskCompletionSource<string> _result = new(TaskCreationOptions.RunContinuationsAsynchronously);

            public BridgeCallback()
                : base(CallbackInterface)
            {
                lock (s_pending)
                {
                    s_pending.Add(this);
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
                        _result.TrySetException(new HealthSourceException((string)args[0], (string)args[1]));
                        return null;
                    default:
                        return base.Invoke(methodName, args);
                }
            }

            private void Release()
            {
                lock (s_pending)
                {
                    s_pending.Remove(this);
                }
            }
        }
    }
}
#endif
