using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Eirora.Spike.HealthConnect
{
    /// <summary>What has been read for one habit, as far as the report needs it.</summary>
    public sealed class HabitReading
    {
        public HabitReading(Habit habit, int rangeDays, RecordPage page, DayAggregate[]? aggregates)
        {
            Habit = habit;
            RangeDays = rangeDays;
            Page = page;
            Aggregates = aggregates;
        }

        public Habit Habit { get; }

        public int RangeDays { get; }

        public RecordPage Page { get; }

        public DayAggregate[]? Aggregates { get; }
    }

    /// <summary>
    /// The text the product owner pastes into the issue. It describes sources, recording methods, permissions and
    /// behaviour, and deliberately leaves out every health value, time and title (HLT-10).
    /// </summary>
    public static class SpikeReport
    {
        public static string Build(
            HealthStatus? status,
            IReadOnlyDictionary<Habit, HabitReading> readings,
            BackgroundRun[]? backgroundRuns,
            ChangeSet? changes,
            DateTimeOffset now,
            TimeZoneInfo zone)
        {
            var report = new StringBuilder();
            report.AppendLine("### Eirora HC-spike: rapport (utan hälsovärden)");
            report.AppendLine();
            report.AppendLine($"- Datum: {now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}");
            if (status == null)
            {
                report.AppendLine("- Status: inte läst");
            }
            else
            {
                report.AppendLine($"- Telefon: {status.deviceModel}, Android {status.androidRelease} (API {status.androidSdk})");
                report.AppendLine($"- Health Connect: {status.sdkStatus}; {string.Join(", ", status.providerVersions.DefaultIfEmpty("version okänd"))}");
                report.AppendLine($"- Funktioner: bakgrundsläsning {Feature(status.backgroundFeature)}, historik {Feature(status.historyFeature)}");
                report.AppendLine($"- Beviljat: {Granted(status)}");
            }

            foreach (var habit in new[] { Habit.Steps, Habit.Exercise, Habit.Sleep })
            {
                report.AppendLine();
                report.AppendLine($"#### {SpikeText.HabitName(habit)}");
                if (!readings.TryGetValue(habit, out var reading))
                {
                    report.AppendLine("- Inte läst");
                    continue;
                }

                AppendReading(report, reading, now, zone);
            }

            report.AppendLine();
            report.AppendLine("#### Bakgrund");
            if (backgroundRuns == null || backgroundRuns.Length == 0)
            {
                report.AppendLine("- Inga körningar");
            }
            else
            {
                var background = backgroundRuns.Where(r => r.InBackground).ToList();
                report.AppendLine($"- Körningar: {backgroundRuns.Length}, varav {background.Count} med appen i bakgrunden");
                foreach (var outcome in background.GroupBy(r => r.outcome).OrderBy(g => g.Key, StringComparer.Ordinal))
                {
                    var empty = outcome.Count(r => r.recordCount == 0);
                    report.AppendLine($"  - i bakgrunden, {outcome.Key}: {outcome.Count()}{(outcome.Key == "ok" ? $", varav {empty} utan poster" : "")}");
                }

                var foreground = backgroundRuns.Where(r => !r.InBackground && r.outcome == "ok").ToList();
                if (foreground.Count > 0)
                {
                    report.AppendLine($"  - med appen öppen, ok: {foreground.Count}, varav {foreground.Count(r => r.recordCount == 0)} utan poster");
                }

                var gaps = backgroundRuns.OrderBy(r => r.atMs).Select(r => r.atMs).ToList();
                if (gaps.Count > 1)
                {
                    var longest = gaps.Zip(gaps.Skip(1), (a, b) => TimeSpan.FromMilliseconds(b - a)).Max();
                    report.AppendLine($"- Längsta glapp mellan körningar: {SpikeText.Duration(longest)}");
                }
            }

            report.AppendLine();
            report.AppendLine("#### Ändringar");
            report.AppendLine(changes == null
                ? "- Inte hämtade"
                : changes.expired
                    ? "- Ändringstoken hade gått ut"
                    : $"- Senaste hämtning: {changes.upserts.Length} nya eller ändrade, {changes.deletions.Length} raderade");
            return report.ToString();
        }

        private static void AppendReading(StringBuilder report, HabitReading reading, DateTimeOffset now, TimeZoneInfo zone)
        {
            var records = reading.Page.records;
            report.AppendLine($"- Läst intervall: {reading.RangeDays} dagar, {records.Length} poster{(reading.Page.truncated ? " (avkortat)" : "")}");
            if (records.Length == 0)
            {
                return;
            }

            var oldest = records.Min(r => r.startMs);
            var oldestDays = (int)Math.Floor((now - DateTimeOffset.FromUnixTimeMilliseconds(oldest)).TotalDays);
            report.AppendLine($"- Äldsta post: {oldestDays} dagar gammal");
            foreach (var origin in records.GroupBy(r => r.origin).OrderByDescending(g => g.Count()))
            {
                var methods = string.Join(", ", origin.GroupBy(r => r.recordingMethod).OrderBy(g => g.Key).Select(g => $"{RecordingMethods.Name(g.Key)} {g.Count()}"));
                var devices = string.Join(", ", origin.Select(r => r.hasDevice ? r.deviceTypeName : "ingen enhet").Distinct().OrderBy(d => d, StringComparer.Ordinal));
                report.AppendLine($"- Källa `{origin.Key}`: {origin.Count()} poster; {methods}; enhet: {devices}");
            }

            var overlaps = DuplicateAnalysis.FindOverlaps(records);
            report.AppendLine($"- Överlappande par: {overlaps.Count(o => !o.SameOrigin)} mellan olika källor, {overlaps.Count(o => o.SameOrigin)} inom samma källa");
            if (reading.Aggregates != null)
            {
                var days = DuplicateAnalysis.CompareByDay(records, reading.Aggregates, zone);
                var withSurplus = days.Count(d => d.Surplus > 0);
                var multiple = days.Count(d => d.HasMultipleOrigins);
                report.AppendLine($"- Dagar med flera källor: {multiple} av {days.Count}; dagar där rå summa är större än Health Connects aggregat: {withSurplus}");
                var readWindowStart = DuplicateAnalysis.LocalDay(now.AddDays(-30), zone);
                var older = reading.Aggregates.Count(a => a.hasValue && string.CompareOrdinal(a.day, readWindowStart) < 0);
                report.AppendLine($"- Aggregatdagar äldre än 30 dagar: {older}");
            }
        }

        private static string Granted(HealthStatus status)
        {
            var all = (ReadPermission[])Enum.GetValues(typeof(ReadPermission));
            return string.Join(", ", all.Select(p => $"{SpikeText.PermissionName(p)} {(status.granted.Contains(HealthKeys.AndroidPermission(p)) ? "ja" : "nej")}"));
        }

        private static string Feature(string status) => status switch
        {
            "AVAILABLE" => "finns",
            "UNAVAILABLE" => "saknas",
            "" => "okänd",
            _ => status,
        };
    }
}
