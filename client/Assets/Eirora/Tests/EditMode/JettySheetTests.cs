using System.Linq;
using Eirora.Client.App;
using Eirora.Client.UI;
using Eirora.Simulation.Economy;
using Eirora.Simulation.Map;
using Eirora.Simulation.Time;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace Eirora.Client.Tests
{
    /// <summary>
    /// The jetties' sheets show every island's store and sea route (TASK-045, economy-v1.md, section 5): the tour's week
    /// ends with the sea route from Nyön open and goods waiting there.
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

            Assert.That(Labels(), Does.Contain("Sjövägen till Nyön är öppen. Skeppen tar hem 40 varor per dygn."));
            Assert.That(Labels().Where(l => l.StartsWith("Nyön: ")).ToArray(), Is.EqualTo(new[] { $"Nyön: {Waiting()} väntar på skeppen." }));
        }

        [Test]
        public void NyonsJettyShowsItsRouteAndItsStore()
        {
            _hud.Select(Jetty(PrototypeHome.Nyon));

            Assert.That(Labels(), Does.Contain("Skeppen tar hem varorna från Nyön, 40 per dygn."));
            Assert.That(Labels(), Does.Contain($"Väntar på skeppen: {Waiting()}."));
        }

        /// <summary>What the world holds on Nyön, as the sheets list it.</summary>
        private string Waiting()
        {
            var world = _game.World!;
            var goods = System.Enum.GetValues(typeof(Resource)).Cast<Resource>().Where(r => world.Stock(PrototypeHome.Nyon, r) > 0).ToList();
            Assert.That(goods, Is.Not.Empty, "Nothing waits on Nyön at the end of the tour's week.");
            return Texts.List(goods.Select(r => $"{world.Stock(PrototypeHome.Nyon, r)} {Texts.Name(r)}"));
        }

        private Hex Jetty(string island) =>
            _game.World!.Buildings.First(b => b.Kind == BuildingKind.Jetty && b.Island == island).Tile;

        private string[] Labels() => _root.Query<Label>().ToList().Select(l => l.text).ToArray();
    }
}
