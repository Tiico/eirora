using System.Linq;
using Eirora.Client.App;
using Eirora.Simulation.Economy;
using Eirora.Simulation.Map;
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

            Assert.That(ScriptedWeek.Play(session, clock), Is.Empty);
        }

        [Test]
        public void TheTourWeekEndsWithTheUpgradedSawmillAndNyonInUse()
        {
            var (session, clock) = Begin();
            ScriptedWeek.Play(session, clock);

            clock.Current = ScriptedWeek.At("2026-10-23 13:00");
            session.CatchUp();
            var world = session.World!;

            Assert.That(world.BuildingAt(ScriptedWeek.Sawmill)!.Level, Is.EqualTo(2));
            Assert.That(world.SeaRouteOpen, Is.True);
            Assert.That(
                world.Buildings.Select(b => $"{b.Kind}@{b.Tile}"),
                Is.SupersetOf(new[] { "Longhouse@(-1, 0)", "Cottage@(0, -1)", "Lookout@(-1, 1)", "Jetty@(10, 0)", "SheepFarm@(10, 1)", "Quarry@(11, 0)" }));

            // Shot 03 shows the farmland as an empty building site.
            var farmland = ScriptedWeek.EmptyFarmland;
            Assert.That(world.CheckBuild(BuildingKind.Field, farmland), Is.EqualTo(Simulation.Commands.Rejection.None));
            Assert.That(world.Projects.Any(p => p.Tile == farmland), Is.False);
        }

        private static (GameSession Session, FixedClock Clock) Begin()
        {
            var clock = new FixedClock(ScriptedWeek.At("2026-10-14 14:00"));
            var session = new GameSession(clock, null);
            session.Begin(new RestSchedule(23 * 60, 8 * 60));
            return (session, clock);
        }
    }
}
