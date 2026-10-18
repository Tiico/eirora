using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Eirora.Spike.HealthConnect
{
    /// <summary>
    /// Made-up data for the Editor and desktop builds, shaped like what two overlapping sources write: a watch and a phone
    /// both counting steps, a workout logged twice and one entered by hand, and a night recorded by two apps.
    /// </summary>
    public sealed class FakeHealthConnectSource : IHealthConnectSource
    {
        public const string Watch = "com.sec.android.app.shealth";
        public const string Phone = "com.google.android.apps.fitness";
        public const string HealthConnectApp = "com.google.android.apps.healthdata";

        private readonly HashSet<ReadPermission> _granted = new HashSet<ReadPermission>();
        private readonly List<BackgroundRun> _runs = new List<BackgroundRun>();
        private readonly DateTimeOffset _now;
        private bool _tracking;

        public FakeHealthConnectSource(DateTimeOffset now)
        {
            _now = now;
        }

        public string Name => "Fejkdata (inte Health Connect)";

        public Task<HealthStatus> GetStatusAsync() => Task.FromResult(new HealthStatus
        {
            sdkStatus = "SDK_AVAILABLE",
            androidSdk = 36,
            androidRelease = "16",
            deviceModel = "Editor",
            providerVersions = new[] { "fake" },
            granted = _granted.Select(HealthKeys.AndroidPermission).ToArray(),
            backgroundFeature = "AVAILABLE",
            historyFeature = "AVAILABLE",
        });

        public Task<PermissionResult> RequestPermissionsAsync(IReadOnlyCollection<ReadPermission> permissions)
        {
            _granted.UnionWith(permissions);
            var requested = permissions.Select(HealthKeys.AndroidPermission).ToArray();
            return Task.FromResult(new PermissionResult { requested = requested, grantedNow = requested, granted = _granted.Select(HealthKeys.AndroidPermission).ToArray() });
        }

        public Task<RecordPage> ReadRecordsAsync(Habit habit, DateTimeOffset from, DateTimeOffset to)
        {
            Require(HealthKeys.PermissionFor(habit));
            var records = Generate(habit).Where(r => r.endMs > from.ToUnixTimeMilliseconds() && r.startMs < to.ToUnixTimeMilliseconds()).OrderByDescending(r => r.startMs).ToArray();
            return Task.FromResult(new RecordPage { records = records });
        }

        public Task<DayAggregate[]> AggregateByDayAsync(Habit habit, DateTimeOffset from, DateTimeOffset to)
        {
            Require(HealthKeys.PermissionFor(habit));

            // Health Connect keeps the highest-priority source where sources overlap; here the watch always wins.
            var zone = TimeZoneInfo.Local;
            var records = Generate(habit).Where(r => r.endMs > from.ToUnixTimeMilliseconds() && r.startMs < to.ToUnixTimeMilliseconds()).ToList();
            var kept = records.Where(r => r.origin == Watch || !records.Any(w => w.origin == Watch && w.startMs < r.endMs && r.startMs < w.endMs));
            var days = kept
                .GroupBy(r => DuplicateAnalysis.LocalDay(r.Start, zone))
                .Select(g => new DayAggregate { day = g.Key, hasValue = true, value = g.Sum(r => r.Value), origins = g.Select(r => r.origin).Distinct().OrderBy(o => o).ToArray() })
                .ToArray();
            return Task.FromResult(days);
        }

        public Task StartChangeTrackingAsync(IReadOnlyCollection<Habit> habits)
        {
            _tracking = true;
            return Task.CompletedTask;
        }

        public Task<ChangeSet> ReadChangesAsync()
        {
            if (!_tracking)
            {
                throw new HealthConnectException("unavailable", "IllegalStateException: Change tracking is not started");
            }

            var edited = Generate(Habit.Steps).First();
            edited.count += 120;
            edited.lastModifiedMs = _now.ToUnixTimeMilliseconds();
            return Task.FromResult(new ChangeSet { upserts = new[] { edited }, deletions = new[] { "fake-deleted-record" } });
        }

        public void ScheduleBackgroundRead() => _runs.Add(new BackgroundRun { atMs = _now.ToUnixTimeMilliseconds(), outcome = "ok", recordCount = 42, importance = 300 });

        public void RunBackgroundReadOnce(TimeSpan delay) => _runs.Add(new BackgroundRun { atMs = _now.Add(delay).ToUnixTimeMilliseconds(), outcome = "permission_denied", recordCount = -1, importance = 300 });

        public void CancelBackgroundRead()
        {
        }

        public Task<BackgroundRun[]> GetBackgroundRunsAsync() => Task.FromResult(_runs.ToArray());

        public void ClearBackgroundRuns() => _runs.Clear();

        public bool OpenHealthConnectSettings() => false;

        public bool OpenProviderInstall() => false;

        public string AppLabel(string packageName) => packageName switch
        {
            Watch => "Samsung Health",
            Phone => "Fit",
            HealthConnectApp => "Health Connect",
            _ => packageName,
        };

        private void Require(ReadPermission permission)
        {
            if (!_granted.Contains(permission))
            {
                throw new HealthConnectException("permission_denied", $"SecurityException: Caller doesn't have {HealthKeys.AndroidPermission(permission)}");
            }
        }

        private IEnumerable<HealthRecord> Generate(Habit habit)
        {
            var today = new DateTimeOffset(_now.Date, _now.Offset);
            for (var daysAgo = 0; daysAgo < 45; daysAgo++)
            {
                var day = today.AddDays(-daysAgo);
                var seed = daysAgo * 7919;
                switch (habit)
                {
                    case Habit.Steps:
                        for (var hour = 8; hour < 20; hour++)
                        {
                            var start = day.AddHours(hour);
                            if (start > _now)
                            {
                                break;
                            }

                            var steps = 200 + ((seed + (hour * 131)) % 900);
                            yield return Steps($"w-{daysAgo}-{hour}", Watch, start, start.AddHours(1), steps, RecordingMethods.AutomaticallyRecorded, "WATCH");
                            yield return Steps($"p-{daysAgo}-{hour}", Phone, start.AddMinutes(10), start.AddMinutes(50), steps * 8 / 10, RecordingMethods.AutomaticallyRecorded, "PHONE");
                        }

                        if (daysAgo == 2)
                        {
                            yield return Steps("manual-steps", HealthConnectApp, day.AddHours(21), day.AddHours(22), 1500, RecordingMethods.ManualEntry, null);
                        }

                        break;

                    case Habit.Exercise:
                        if (daysAgo % 2 == 0)
                        {
                            var start = day.AddHours(17).AddMinutes(30);
                            yield return Session("exercise", $"run-w-{daysAgo}", Watch, start, start.AddMinutes(42), RecordingMethods.ActivelyRecorded, "WATCH", 56, "RUNNING");
                            yield return Session("exercise", $"run-p-{daysAgo}", Phone, start.AddMinutes(1), start.AddMinutes(41), RecordingMethods.AutomaticallyRecorded, "PHONE", 56, "RUNNING");
                        }

                        if (daysAgo == 3)
                        {
                            yield return Session("exercise", "yoga-manual", HealthConnectApp, day.AddHours(7), day.AddHours(7).AddMinutes(30), RecordingMethods.ManualEntry, null, 83, "YOGA");
                        }

                        break;

                    case Habit.Sleep:
                        var bedtime = day.AddHours(-1).AddMinutes(-((seed % 50) + 5));
                        var wake = day.AddHours(6).AddMinutes(seed % 70);
                        if (wake <= _now)
                        {
                            yield return Session("sleep", $"sleep-w-{daysAgo}", Watch, bedtime, wake, RecordingMethods.AutomaticallyRecorded, "WATCH", 0, "");
                            if (daysAgo % 3 == 0)
                            {
                                yield return Session("sleep", $"sleep-p-{daysAgo}", Phone, bedtime.AddMinutes(20), wake.AddMinutes(-15), RecordingMethods.Unknown, "PHONE", 0, "");
                            }
                        }

                        break;
                }
            }
        }

        private static HealthRecord Steps(string id, string origin, DateTimeOffset start, DateTimeOffset end, long count, int method, string? device)
        {
            var record = Base("steps", id, origin, start, end, method, device);
            record.count = count;
            return record;
        }

        private static HealthRecord Session(string type, string id, string origin, DateTimeOffset start, DateTimeOffset end, int method, string? device, int exerciseType, string exerciseTypeName)
        {
            var record = Base(type, id, origin, start, end, method, device);
            record.exerciseType = exerciseType;
            record.exerciseTypeName = exerciseTypeName;
            if (type == "sleep")
            {
                var light = start.AddMinutes(25);
                var deep = light.AddMinutes(150);
                var rem = deep.AddMinutes(90);
                record.stages = new[]
                {
                    Stage(1, "AWAKE", start, light),
                    Stage(4, "LIGHT", light, deep),
                    Stage(5, "DEEP", deep, rem),
                    Stage(6, "REM", rem, end),
                };
            }

            return record;
        }

        private static SleepStage Stage(int stage, string name, DateTimeOffset start, DateTimeOffset end) =>
            new SleepStage { stage = stage, stageName = name, startMs = start.ToUnixTimeMilliseconds(), endMs = end.ToUnixTimeMilliseconds() };

        private static HealthRecord Base(string type, string id, string origin, DateTimeOffset start, DateTimeOffset end, int method, string? device) => new HealthRecord
        {
            type = type,
            id = id,
            origin = origin,
            startMs = start.ToUnixTimeMilliseconds(),
            endMs = end.ToUnixTimeMilliseconds(),
            hasStartOffset = true,
            startOffsetSec = (int)start.Offset.TotalSeconds,
            hasEndOffset = true,
            endOffsetSec = (int)end.Offset.TotalSeconds,
            recordingMethod = method,
            recordingMethodName = RecordingMethods.Name(method),
            lastModifiedMs = end.AddMinutes(5).ToUnixTimeMilliseconds(),
            hasDevice = device != null,
            deviceType = device == "WATCH" ? 1 : 2,
            deviceTypeName = device ?? "",
            deviceManufacturer = device == null ? "" : "Exempel",
            deviceModel = device == "WATCH" ? "Klocka" : device == "PHONE" ? "Telefon" : "",
        };
    }
}
