using System.Linq;
using Eirora.Client.App;
using Eirora.CloudSaving;
using Eirora.Simulation.Economy;
using Eirora.Simulation.Map;
using Eirora.Simulation.Saving;
using Eirora.Simulation.Time;
using NUnit.Framework;
using UnityEngine;

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
            Assert.That(world.SeaRoutes.Select(r => r.From), Is.EqualTo(new[] { PrototypeHome.Nyon }));
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
        public void TheTourSetsTheStockRulesWithoutARefusal()
        {
            var (session, clock) = Begin();
            ScriptedWeek.Play(session, clock);
            clock.Current = ScriptedWeek.At("2026-10-23 13:00");
            session.CatchUp();

            Assert.That(ScriptedWeek.SetStockRules(session), Is.Empty);

            var world = session.World!;
            Assert.That(world.ImportOrder(PrototypeHome.Nyon).Take(2), Is.EqualTo(new[] { Resource.Stone, Resource.Wool }), "Shot 24 shows stone shipped first.");
            Assert.That(world.ReservationOf(PrototypeHome.Nyon, Resource.Wool), Is.EqualTo(10));
        }

        [Test]
        public void TheTourWeekHasABuildingStandingStillABottleneckCarriersAndARoadToShow()
        {
            var (session, clock) = Begin();
            ScriptedWeek.Play(session, clock);

            // Shot 04-standing-still, on the seventh morning, after which the tour lays a road to the far fishing hut.
            clock.Current = ScriptedWeek.At("2026-10-21 10:00");
            session.CatchUp();
            Assert.That(session.World!.Diagnose().Buildings.Any(b => b.StandsStill), Is.True);
            Assert.That(ScriptedWeek.LayARoad(session), Is.Empty);

            // Shots 13-bottleneck and 21-storehouse-carriers, two days later.
            clock.Current = ScriptedWeek.At("2026-10-23 13:00");
            session.CatchUp();
            var diagnosis = session.World!.Diagnose();
            Assert.That(diagnosis.BottleneckOn(session.World!.HomeIsland), Is.Not.Null);
            var behind = diagnosis.Buildings.First(b => b.Hindrances.Any(h => h is CarriersBehind));
            Assert.That(diagnosis.DepotAt(behind.Building.Depot!.Value)!.Depot.Carriers, Is.GreaterThanOrEqualTo(1));

            // Shot 22-road: the road to the far fishing hut, laid by then.
            Assert.That(session.World!.Roads, Is.Not.Empty);
            Assert.That(session.World!.Projects.Where(p => p.Kind == ProjectKind.Road), Is.Empty, "Every road tile is laid.");
        }

        // Shots 26 and 27 (TASK-052): the grown village has farmers and craftsmen, step 3 and one of each building the steps
        // unlock, staffed, so that every new building and tier is shown with its look.
        [Test]
        public void TheGrownVillageShowsEveryTierAndEveryBuildingTheStepsUnlock()
        {
            var save = Save.Load(Resources.Load<TextAsset>(ScreenshotTour.GrownVillageSave).bytes);
            var session = new GameSession(new FixedClock(save.World.Now), null, () => PrototypeHome.Map);
            session.Replace(save, ReplaceReason.Downloaded);
            var world = session.World!;

            Assert.That(world.Step, Is.EqualTo(3));
            Assert.That(world.Settlements.Sum(s => s.VillagersOf(PopulationTier.Farmers)), Is.GreaterThan(0));
            Assert.That(world.Settlements.Sum(s => s.VillagersOf(PopulationTier.Craftsmen)), Is.GreaterThan(0));
            var shown = ScreenshotTour.GrownBuildings(world);
            Assert.That(
                shown.Select(b => b.Kind),
                Is.EquivalentTo(world.Content.Buildings.Where(k => world.Content.For(k).Step > 0)));
            Assert.That(shown.Where(b => world.Content.For(b.Kind).Staff > 0), Has.All.Matches<Building>(b => b.Staffed));
            Assert.That(world.Islands.Sum(world.Knarrs), Is.GreaterThan(0));
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
