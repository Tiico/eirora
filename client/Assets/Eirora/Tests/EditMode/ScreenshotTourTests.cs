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

        [Test]
        public void TheTourOrdersTheWorkWithoutARefusal()
        {
            var (session, clock) = Begin();
            ScriptedWeek.Play(session, clock);
            clock.Current = ScriptedWeek.At("2026-10-23 13:00");
            session.CatchUp();

            Assert.That(ScriptedWeek.OrderTheWork(session), Is.Empty);

            var world = session.World!;
            Assert.That(world.Projects.Count, Is.GreaterThanOrEqualTo(2), "Shot 19 shows a queue with a project to move first.");
            Assert.That(world.Projects[0].Building, Is.EqualTo(BuildingKind.Cottage));
            Assert.That(world.BuildingAt(ScriptedWeek.FishingHut)!.Paused, Is.True);
        }

        [Test]
        public void TheTourWeekHasABuildingStandingStillABottleneckAndAFarBuildingToShow()
        {
            var (session, clock) = Begin();
            ScriptedWeek.Play(session, clock);

            // Shot 04-standing-still, on the seventh afternoon.
            clock.Current = ScriptedWeek.At("2026-10-21 13:00");
            session.CatchUp();
            Assert.That(session.World!.Diagnose().Buildings.Any(b => b.StandsStill), Is.True);

            // Shots 13-bottleneck and 13-long-way, two days later.
            clock.Current = ScriptedWeek.At("2026-10-23 13:00");
            session.CatchUp();
            var diagnosis = session.World!.Diagnose();
            Assert.That(diagnosis.BottleneckOn(IslandId.Home), Is.Not.Null);
            Assert.That(diagnosis.Buildings.Any(b => b.Hindrances.OfType<LongWay>().Any()), Is.True);
        }

        private static (GameSession Session, FixedClock Clock) Begin()
        {
            var clock = new FixedClock(ScriptedWeek.At("2026-10-14 14:00"));
            var session = new GameSession(clock, null, () => PrototypeHome.Map);
            session.Begin(new RestSchedule(23 * 60, 8 * 60));
            return (session, clock);
        }
    }
}
