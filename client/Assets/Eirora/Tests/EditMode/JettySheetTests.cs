using System.Linq;
using Eirora.Client.App;
using Eirora.Client.UI;
using Eirora.Simulation.Commands;
using Eirora.Simulation.Economy;
using Eirora.Simulation.Map;
using Eirora.Simulation.Time;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace Eirora.Client.Tests
{
    /// <summary>
    /// The jetties' sheets show every island's settlement, store and sea route (TASK-045, TASK-050, economy-v2.md,
    /// section 6): the tour's week ends with the sea route from Nyön open, villagers of Nyön's own at work there and goods
    /// waiting for the ships.
    /// </summary>
    public sealed class JettySheetTests
    {
        private VisualElement _root = null!;
        private GameSession _game = null!;
        private Hud _hud = null!;

        [SetUp]
        public void SetUp()
        {
            var clock = new FixedClock(ScriptedWeek.At("2026-10-14 14:00"));
            _game = new GameSession(clock, null, () => PrototypeHome.Map);
            _game.Begin(new RestSchedule(23 * 60, 8 * 60));
            ScriptedWeek.Play(_game, clock);
            clock.Current = ScriptedWeek.At("2026-10-23 13:00");
            _game.CatchUp();
            _root = new VisualElement();
            _hud = new Hud(_root, command => _game.Apply(command));
            _hud.Show(_game.World!, _game.SinceDawn);
        }

        [Test]
        public void TheHomeJettyShowsEverySeaRouteAndWhatWaitsOnEachIsland()
        {
            _hud.Select(Jetty(_game.World!.HomeIsland));

            Assert.That(Labels(), Does.Contain("Sjövägen till Nyön är öppen. Skeppen bär 40 varor per dygn, åt båda hållen tillsammans."));
            Assert.That(Labels().Where(l => l.StartsWith("Nyön: ")).ToArray(), Is.EqualTo(new[] { Settlement(), $"Nyön: {Waiting()} väntar på skeppen." }));
        }

        // Introduction 6 shows Nyön's own villagers with the sea route (D-118).
        [Test]
        public void NyonsJettyShowsItsOwnVillagersItsRouteAndItsStore()
        {
            _hud.Select(Jetty(PrototypeHome.Nyon));

            var nyon = _game.World!.SettlementOn(PrototypeHome.Nyon)!;
            Assert.That(nyon.Villagers, Is.GreaterThan(0), "No villager has moved to Nyön by the end of the tour's week.");
            Assert.That(Labels(), Does.Contain(Settlement()));
            Assert.That(Labels(), Does.Contain("Skeppen seglar mellan Nyön och Hemön med 40 varor per dygn, åt båda hållen tillsammans."));
            Assert.That(Labels(), Does.Contain($"Väntar på skeppen: {Waiting()}."));
        }

        // The report lists every settlement once there is more than one.
        [Test]
        public void TheReportListsEverySettlement()
        {
            _hud.OpenReport();

            Assert.That(Labels(), Does.Contain(Texts.Settlement(_game.World!, _game.World!.SettlementOn(_game.World!.HomeIsland)!) + "."));
            Assert.That(Labels(), Does.Contain(Settlement()));
        }

        // With more than one settlement the free villagers build in the queue's order on the islands they reach, not
        // all on the first project (D-113); the subtitle stays on one line.
        [Test]
        public void TheQueueSaysTheFreeVillagersBuildInItsOrder()
        {
            var world = _game.World!;
            var meadow = Enumerable.Range(0, world.Map.Disc.Count).Select(i => world.Map.Disc[i])
                .First(h => world.CheckBuild(BuildingKind.Cottage, h) == Rejection.None);
            Assert.That(_game.Apply(new Build(BuildingKind.Cottage, meadow)).Accepted, Is.True);
            Assume.That(world.FreeVillagers, Is.GreaterThan(1));
            _hud.OpenQueue();

            Assert.That(Labels(), Does.Contain($"{Texts.FreeVillagers(world.FreeVillagers).Substring(0, 1).ToUpperInvariant()}{Texts.FreeVillagers(world.FreeVillagers).Substring(1)} bygger i köns ordning."));
        }

        /// <summary>Nyön's settlement, as the sheets show it.</summary>
        private string Settlement()
        {
            var world = _game.World!;
            string line = Texts.Settlement(world, world.SettlementOn(PrototypeHome.Nyon)!) + ".";
            Assert.That(line, Does.Match("^Nyön: [1-4] av 4 bybor, (alla i arbete|en ledig|[a-z]+ lediga)\\.$"));
            return line;
        }

        /// <summary>What waits on Nyön for the ships, as the sheets list it: its store but the two days' food it keeps.</summary>
        private string Waiting()
        {
            var world = _game.World!;
            var goods = System.Enum.GetValues(typeof(Resource)).Cast<Resource>()
                .Select(r => (Resource: r, Units: world.ForTheShips(PrototypeHome.Nyon, r)))
                .Where(g => g.Units > 0)
                .ToList();
            Assert.That(goods, Is.Not.Empty, "Nothing waits on Nyön at the end of the tour's week.");
            return Texts.List(goods.Select(g => $"{g.Units} {Texts.Name(g.Resource)}"));
        }

        private Hex Jetty(string island) =>
            _game.World!.Buildings.First(b => b.Kind == BuildingKind.Jetty && b.Island == island).Tile;

        private string[] Labels() => _root.Query<Label>().ToList().Select(l => l.text).ToArray();
    }
}
