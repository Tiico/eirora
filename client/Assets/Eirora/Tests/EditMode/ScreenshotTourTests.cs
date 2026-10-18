using System.Linq;
using Eirora.Client.App;
using Eirora.Simulation.Economy;
using Eirora.Simulation.Time;
using NUnit.Framework;

namespace Eirora.Client.Tests
{
    /// <summary>The screenshot tour's week plays as written, so its shots show what their names say.</summary>
    public sealed class ScreenshotTourTests
    {
        [Test]
        public void TheWorldAcceptsEveryCommandOfTheTourWeek()
        {
            var (session, clock) = Begin();

            Assert.That(ScreenshotTour.PlayAWeek(session, clock), Is.Empty);
        }

        [Test]
        public void TheTourWeekEndsWithTheUpgradedSawmillAndNyonInUse()
        {
            var (session, clock) = Begin();
            ScreenshotTour.PlayAWeek(session, clock);

            clock.Current = ScreenshotTour.At("2026-10-23 13:00");
            session.CatchUp();
            var world = session.World!;

            Assert.That(world.Buildings.Single(b => b.Site.Id == "E2").Level, Is.EqualTo(2));
            Assert.That(world.SeaRouteOpen, Is.True);
            Assert.That(
                world.Buildings.Select(b => $"{b.Kind}@{b.Site.Id}"),
                Is.SupersetOf(new[] { "Longhouse@L1", "Cottage@L2", "Lookout@L3", "Jetty@N0", "SheepFarm@N1", "Quarry@N2" }));
            Assert.That(world.IsRevealed("O2"), Is.True, "Shot 03 shows O2 as an empty site.");
            Assert.That(world.Buildings.Any(b => b.Site.Id == "O2") || world.Projects.Any(p => p.Site.Id == "O2"), Is.False, "Shot 03 shows O2 as an empty site.");
        }

        private static (GameSession Session, FixedClock Clock) Begin()
        {
            var clock = new FixedClock(ScreenshotTour.At("2026-10-14 14:00"));
            var session = new GameSession(clock, null);
            session.Begin(new RestSchedule(23 * 60, 8 * 60));
            return (session, clock);
        }
    }
}
