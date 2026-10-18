using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Eirora.Spike.HealthConnect.Editor;
using NUnit.Framework;

namespace Eirora.Spike.HealthConnect.Tests
{
    /// <summary>Raw health data never leaves the device (HLT-10): no network, read-only, and a report without values.</summary>
    public sealed class PrivacyTests
    {
        private static readonly XNamespace Android = "http://schemas.android.com/apk/res/android";

        [Test]
        public void TheAdapterDeclaresExactlyTheReadPermissions()
        {
            var manifest = XDocument.Load(SpikeBuild.ManifestPath);
            var permissions = manifest.Root!.Elements("uses-permission").Select(e => (string)e.Attribute(Android + "name")!).ToList();

            Assert.That(permissions, Is.EquivalentTo(SpikeBuild.HealthPermissions));
        }

        [Test]
        public void TheAdapterDeclaresTheRationaleScreensHealthConnectRequires()
        {
            var actions = XDocument.Load(SpikeBuild.ManifestPath).Descendants("action").Select(e => (string)e.Attribute(Android + "name")!).ToList();

            Assert.That(actions, Does.Contain("androidx.health.ACTION_SHOW_PERMISSIONS_RATIONALE"));
            Assert.That(actions, Does.Contain("android.intent.action.VIEW_PERMISSION_USAGE"));
        }

        [Test]
        public void TheBuildRejectsNetworkAccessAndWrites()
        {
            var safe = SpikeBuild.HealthPermissions.Append("android.permission.WAKE_LOCK").ToList();

            Assert.That(SpikeBuild.PermissionProblems(safe), Is.Empty);
            Assert.That(SpikeBuild.PermissionProblems(safe.Append("android.permission.INTERNET").ToList()), Is.Not.Empty);
            Assert.That(SpikeBuild.PermissionProblems(safe.Append("android.permission.health.WRITE_STEPS").ToList()), Is.Not.Empty);
        }

        // Unity skips Kotlin in an .androidlib without an error unless the module applies the Kotlin plugin itself.
        [Test]
        public void TheBuildNoticesWhenTheKotlinBridgeIsMissing()
        {
            var apk = Path.Combine(Path.GetTempPath(), $"spike-{Guid.NewGuid():N}.apk");
            try
            {
                using (var archive = ZipFile.Open(apk, ZipArchiveMode.Create))
                {
                    using var writer = new StreamWriter(archive.CreateEntry("classes2.dex").Open());
                    writer.Write("dex 035 ... Lcom/eirora/spike/healthconnect/bridge/HealthConnectBridge; ...");
                }

                Assert.That(SpikeBuild.ApkContainsClass(apk, SpikeBuild.BridgeClass), Is.True);
                Assert.That(SpikeBuild.ApkContainsClass(apk, "com.eirora.spike.healthconnect.bridge.Missing"), Is.False);
            }
            finally
            {
                File.Delete(apk);
            }
        }

        [Test]
        public void TheReportHoldsNoValuesTimesOrTitles()
        {
            var now = new DateTimeOffset(2026, 10, 17, 21, 0, 0, TimeSpan.FromHours(2));
            var source = new FakeHealthConnectSource(now);
            source.RequestPermissionsAsync(Enum.GetValues(typeof(ReadPermission)).Cast<ReadPermission>().ToList()).Wait();
            var readings = new Dictionary<Habit, HabitReading>();
            foreach (var habit in new[] { Habit.Steps, Habit.Exercise, Habit.Sleep })
            {
                var page = source.ReadRecordsAsync(habit, now.AddDays(-30), now).Result;
                readings[habit] = new HabitReading(habit, 30, page, source.AggregateByDayAsync(habit, now.AddDays(-30), now).Result);
            }

            source.ScheduleBackgroundRead();
            source.StartChangeTrackingAsync(new[] { Habit.Steps }).Wait();
            var changes = source.ReadChangesAsync().Result;

            var report = SpikeReport.Build(source.GetStatusAsync().Result, readings, source.GetBackgroundRunsAsync().Result, changes, now, TimeZoneInfo.Utc);
            var body = string.Join("\n", report.Split('\n').Where(line => !line.StartsWith("- Datum:", StringComparison.Ordinal)));

            Assert.That(Regex.IsMatch(body, @"\d{1,2}:\d{2}"), Is.False, "a clock time");
            Assert.That(Regex.IsMatch(body, @"\d{4,}"), Is.False, "a number of four or more digits, like a step count");
            Assert.That(body, Does.Not.Contain("RUNNING").And.Not.Contain("YOGA"), "an exercise type");
            Assert.That(body, Does.Contain("AUTOMATICALLY_RECORDED"));
            Assert.That(body, Does.Contain(FakeHealthConnectSource.Watch));
        }
    }
}
