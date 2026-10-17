using System;
using System.Linq;
using NUnit.Framework;

namespace Eirora.Spike.HealthConnect.Tests
{
    public sealed class DuplicateAnalysisTests
    {
        private static readonly DateTimeOffset Morning = new DateTimeOffset(2026, 10, 14, 8, 0, 0, TimeSpan.Zero);

        [Test]
        public void FindsOverlapsBetweenSourcesAndWithinOne()
        {
            var watch = Steps("a", Morning, 60, 1000);
            var phone = Steps("b", Morning.AddMinutes(30), 60, 800);
            var watchAgain = Steps("a", Morning.AddMinutes(45), 10, 100);
            var later = Steps("b", Morning.AddHours(3), 10, 50);

            var overlaps = DuplicateAnalysis.FindOverlaps(new[] { later, watchAgain, phone, watch });

            Assert.That(overlaps.Count(o => !o.SameOrigin), Is.EqualTo(2));
            Assert.That(overlaps.Count(o => o.SameOrigin), Is.EqualTo(1));
            Assert.That(overlaps.Single(o => o.First == watch && o.Second == phone).Duration, Is.EqualTo(TimeSpan.FromMinutes(30)));
        }

        [Test]
        public void TouchingIntervalsDoNotOverlap()
        {
            var first = Steps("a", Morning, 60, 1000);
            var second = Steps("b", Morning.AddMinutes(60), 60, 1000);

            Assert.That(DuplicateAnalysis.FindOverlaps(new[] { first, second }), Is.Empty);
        }

        [Test]
        public void ComparesTheNaiveSumWithTheAggregatePerDay()
        {
            var records = new[]
            {
                Steps("watch", Morning, 60, 1000),
                Steps("phone", Morning.AddMinutes(10), 40, 800),
                Steps("watch", Morning.AddDays(-1), 60, 500),
            };
            var aggregates = new[]
            {
                new DayAggregate { day = "2026-10-14", hasValue = true, value = 1000, origins = new[] { "watch" } },
                new DayAggregate { day = "2026-10-13", hasValue = true, value = 500, origins = new[] { "watch" } },
            };

            var days = DuplicateAnalysis.CompareByDay(records, aggregates, TimeZoneInfo.Utc);

            Assert.That(days.Select(d => d.Day), Is.EqualTo(new[] { "2026-10-14", "2026-10-13" }));
            Assert.That(days[0].RawTotal, Is.EqualTo(1800));
            Assert.That(days[0].Surplus, Is.EqualTo(800));
            Assert.That(days[0].HasMultipleOrigins, Is.True);
            Assert.That(days[1].Surplus, Is.EqualTo(0));
        }

        [Test]
        public void ADayWithOnlyAnAggregateIsStillShown()
        {
            var aggregates = new[] { new DayAggregate { day = "2026-10-12", hasValue = true, value = 300 } };

            var days = DuplicateAnalysis.CompareByDay(Array.Empty<HealthRecord>(), aggregates, TimeZoneInfo.Utc);

            Assert.That(days.Single().RawTotal, Is.EqualTo(0));
            Assert.That(days.Single().Surplus, Is.EqualTo(-300));
        }

        private static HealthRecord Steps(string origin, DateTimeOffset start, int minutes, long count) => new HealthRecord
        {
            type = "steps",
            origin = origin,
            startMs = start.ToUnixTimeMilliseconds(),
            endMs = start.AddMinutes(minutes).ToUnixTimeMilliseconds(),
            count = count,
        };
    }
}
