using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Eirora.Spike.HealthConnect
{
    /// <summary>One local day: the naive sum of every record against Health Connect's deduplicated aggregate.</summary>
    public sealed class DayComparison
    {
        public DayComparison(string day, long rawTotal, IReadOnlyDictionary<string, long> rawByOrigin, DayAggregate? aggregate)
        {
            Day = day;
            RawTotal = rawTotal;
            RawByOrigin = rawByOrigin;
            Aggregate = aggregate;
        }

        /// <summary>yyyy-MM-dd in the device's time zone, as Health Connect groups it.</summary>
        public string Day { get; }

        public long RawTotal { get; }

        public IReadOnlyDictionary<string, long> RawByOrigin { get; }

        public DayAggregate? Aggregate { get; }

        public bool HasMultipleOrigins => RawByOrigin.Count > 1;

        /// <summary>Raw total minus aggregate: what a naive import would have counted twice. Null without an aggregate.</summary>
        public long? Surplus => Aggregate is { hasValue: true } aggregate ? RawTotal - aggregate.value : (long?)null;
    }

    /// <summary>Two records whose intervals overlap.</summary>
    public sealed class Overlap
    {
        public Overlap(HealthRecord first, HealthRecord second)
        {
            First = first;
            Second = second;
        }

        public HealthRecord First { get; }

        public HealthRecord Second { get; }

        public bool SameOrigin => First.origin == Second.origin;

        public TimeSpan Duration => TimeSpan.FromMilliseconds(Math.Min(First.endMs, Second.endMs) - Math.Max(First.startMs, Second.startMs));
    }

    /// <summary>Shows how much a naive import would double count (HLT-04, INV-B1).</summary>
    public static class DuplicateAnalysis
    {
        /// <summary>
        /// Compares per local day. A record counts fully on the day it starts, whereas Health Connect splits records that
        /// cross midnight, so sessions over midnight can differ without being duplicates.
        /// </summary>
        public static IReadOnlyList<DayComparison> CompareByDay(IEnumerable<HealthRecord> records, IEnumerable<DayAggregate> aggregates, TimeZoneInfo zone)
        {
            var aggregateByDay = aggregates.GroupBy(a => a.day).ToDictionary(g => g.Key, g => g.First());
            var rawByDay = new SortedDictionary<string, Dictionary<string, long>>(StringComparer.Ordinal);
            foreach (var record in records)
            {
                var day = LocalDay(record.Start, zone);
                if (!rawByDay.TryGetValue(day, out var byOrigin))
                {
                    byOrigin = new Dictionary<string, long>();
                    rawByDay[day] = byOrigin;
                }

                byOrigin.TryGetValue(record.origin, out var sum);
                byOrigin[record.origin] = sum + record.Value;
            }

            var days = rawByDay.Keys.Union(aggregateByDay.Where(a => a.Value.hasValue).Select(a => a.Key)).OrderByDescending(d => d, StringComparer.Ordinal);
            return days.Select(day =>
            {
                var byOrigin = rawByDay.TryGetValue(day, out var found) ? found : new Dictionary<string, long>();
                return new DayComparison(day, byOrigin.Values.Sum(), byOrigin, aggregateByDay.TryGetValue(day, out var aggregate) ? aggregate : null);
            }).ToList();
        }

        /// <summary>Every pair of records whose intervals overlap by more than zero time, by a sweep over start times.</summary>
        public static IReadOnlyList<Overlap> FindOverlaps(IEnumerable<HealthRecord> records)
        {
            var sorted = records.Where(r => r.endMs > r.startMs).OrderBy(r => r.startMs).ThenBy(r => r.endMs).ToList();
            var overlaps = new List<Overlap>();
            var active = new List<HealthRecord>();
            foreach (var record in sorted)
            {
                active.RemoveAll(a => a.endMs <= record.startMs);
                overlaps.AddRange(active.Select(a => new Overlap(a, record)));
                active.Add(record);
            }

            return overlaps;
        }

        public static string LocalDay(DateTimeOffset instant, TimeZoneInfo zone) =>
            TimeZoneInfo.ConvertTime(instant, zone).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    }
}
