using System.Linq;
using System.Reflection;
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
    /// The player's stock rules on the sheets (TASK-046, LOG-06): the sawmill's minimum stock, and an island's store with
    /// its reservations and the order the ships take its goods home in. The tour's week ends with the sea route from
    /// Nyön open and goods waiting there.
    /// </summary>
    public sealed class StockRulesSheetTests
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
            _game.Changed += () => _hud.Show(_game.World!, _game.SinceDawn);
            _hud.Show(_game.World!, _game.SinceDawn);
        }

        [Test]
        public void TheSawmillsSheetSetsItsMinimumStockInStepsOfFive()
        {
            _hud.Select(ScriptedWeek.Sawmill);

            Assert.That(Labels(), Does.Contain("Minimilager"));
            Assert.That(Labels(), Does.Contain("20 timmer"));
            Assert.That(Labels(), Does.Contain("Sågverket lämnar alltid 20 timmer i lagret till byggen."));

            Press(Stepper("+"));
            Assert.That(_game.World!.BuildingAt(ScriptedWeek.Sawmill)!.Reserve, Is.EqualTo(25));
            Assert.That(Labels(), Does.Contain("25 timmer"));

            for (int i = 0; i < 5; i++)
            {
                Press(Stepper("−"));
            }

            Assert.That(_game.World!.BuildingAt(ScriptedWeek.Sawmill)!.Reserve, Is.EqualTo(0));
            Assert.That(Stepper("−").enabledSelf, Is.False);
            Assert.That(Labels(), Does.Contain("Sågverket tar allt timmer som finns i lagret."));
        }

        [Test]
        public void TheSawmillsSheetSaysWhenTheIslandsReservationKeepsMore()
        {
            _game.Apply(new Simulation.Commands.SetReservation(_game.World!.HomeIsland, Resource.Timber, 30));

            _hud.Select(ScriptedWeek.Sawmill);

            Assert.That(Labels(), Does.Contain("Hemön har 30 timmer reserverade, så sågverket lämnar 30 i lagret."));
        }

        [Test]
        public void NyonsStoreListsTheGoodsInTheShipsOrderAndMovesOneFirst()
        {
            _hud.Select(Jetty(PrototypeHome.Nyon));
            Press(Action("Lagret på Nyön"));

            Assert.That(Labels(), Does.Contain("Lagret på Nyön"));
            Assert.That(Goods(), Is.EqualTo(new[] { "Fisk", "Säd", "Timmer", "Plank", "Sten", "Ull", "Vadmal" }));
            Assert.That(_root.Query<Button>(className: "good-button").ToList(), Has.Count.EqualTo(6)); // All but the first.

            Press(First("Ull"));

            Assert.That(_game.World!.ImportOrder(PrototypeHome.Nyon)[0], Is.EqualTo(Resource.Wool));
            Assert.That(Goods()[0], Is.EqualTo("Ull"));
        }

        [Test]
        public void NyonsStoreReservesTheChosenGoodAndTheShipsLeaveIt()
        {
            var world = _game.World!;
            _hud.OpenStore(PrototypeHome.Nyon);
            Press(Pick("Sten"));
            Assert.That(Labels(), Does.Contain("Reservera sten"));

            Press(Stepper("+"));
            Press(Stepper("+"));

            Assert.That(world.ReservationOf(PrototypeHome.Nyon, Resource.Stone), Is.EqualTo(10));
            Assert.That(_root.Query(className: "good-chosen").ToList().Single().Q<Label>(className: "good-name").text, Is.EqualTo("Sten"));
            Assert.That(Labels(), Does.Contain(Texts.InStore(world.Stock(PrototypeHome.Nyon, Resource.Stone), 10)));

            // What is reserved does not wait for the ships.
            _hud.Select(Jetty(PrototypeHome.Nyon));
            long stone = world.Stock(PrototypeHome.Nyon, Resource.Stone);
            Assert.That(stone, Is.GreaterThan(0), "No stone waits on Nyön at the end of the tour's week.");
            Assert.That(Labels().Single(l => l.StartsWith("Väntar på skeppen: ") || l == "Inget väntar på skeppen just nu."), Does.Not.Contain($"{stone} sten"));
        }

        [Test]
        public void TheHomeJettyOpensTheStoreOfEachIslandItsShipsSailFrom()
        {
            _hud.Select(Jetty(_game.World!.HomeIsland));

            Press(Action("Skeppen från Nyön"));

            Assert.That(Labels(), Does.Contain("Lagret på Nyön"));
        }

        [Test]
        public void TheHomeStoreReservesButHasNoOrderForTheShips()
        {
            _hud.Select(Hex.Origin);
            Press(Action("Lagret på Hemön"));

            Assert.That(Labels(), Does.Contain("Det som är reserverat tar förädlingen inte. Byggen betalar ändå ur det."));
            Assert.That(_root.Query<Button>(className: "good-button").ToList(), Is.Empty);
        }

        /// <summary>Clicks a button as a tap would, through its <see cref="Clickable"/>; the root is in no panel to send events to.</summary>
        private static void Press(Button button)
        {
            var invoke = typeof(Clickable).GetMethod("Invoke", BindingFlags.Instance | BindingFlags.NonPublic, null, new[] { typeof(EventBase) }, null);
            Assert.That(invoke, Is.Not.Null, "Clickable.Invoke is gone; find another way to press a button.");
            using var tap = NavigationSubmitEvent.GetPooled();
            invoke!.Invoke(button.clickable, new object[] { tap });
        }

        private Button Action(string title) =>
            _root.Query<Button>(className: "action").ToList().Single(b => b.Q<Label>(className: "action-title").text == title);

        private Button Stepper(string text) => _root.Query<Button>(className: "stepper-button").ToList().Single(b => b.text == text);

        private Button Pick(string good) =>
            _root.Query<Button>(className: "good-pick").ToList().Single(b => b.Q<Label>(className: "good-name").text == good);

        private Button First(string good) =>
            _root.Query(className: "good").ToList().Single(g => g.Q<Label>(className: "good-name").text == good).Q<Button>(className: "good-button");

        private string[] Goods() => _root.Query<Label>(className: "good-name").ToList().Select(l => l.text).ToArray();

        private Hex Jetty(string island) =>
            _game.World!.Buildings.First(b => b.Kind == BuildingKind.Jetty && b.Island == island).Tile;

        private string[] Labels() => _root.Query<Label>().ToList().Select(l => l.text).ToArray();
    }
}
