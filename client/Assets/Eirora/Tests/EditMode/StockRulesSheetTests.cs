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
            _hud.ShowMore();

            Assert.That(Labels(), Does.Contain("Minimilager"));
            Assert.That(Labels(), Does.Contain("20 timmer"));
            Assert.That(Labels(), Does.Contain("Sparas till byggen"));

            Press(Stepper("Minimilager", "+"));
            Assert.That(_game.World!.BuildingAt(ScriptedWeek.Sawmill)!.Reserve, Is.EqualTo(25));
            Assert.That(Labels(), Does.Contain("25 timmer"));

            for (int i = 0; i < 5; i++)
            {
                Press(Stepper("Minimilager", "−"));
            }

            Assert.That(_game.World!.BuildingAt(ScriptedWeek.Sawmill)!.Reserve, Is.EqualTo(0));
            Assert.That(Stepper("Minimilager", "−").enabledSelf, Is.False);
            Assert.That(Labels(), Does.Contain("Sågverket tar allt"));
        }

        // Review of #167: a demolition (TASK-047) can leave the minimum stock above what the store holds; "−" brings it down
        // to that at once, and from there a step at a time.
        [Test]
        public void TheMinimumStockAboveTheStoreAfterADemolitionComesDownToTheStore()
        {
            var tile = ScriptedWeek.Sawmill;
            var world = EditedWorld.Edit(_game.World!, lines =>
            {
                int at = lines.FindIndex(l => l.StartsWith($"building Sawmill {tile.Q} {tile.R} ", System.StringComparison.Ordinal));
                lines[at] = lines[at].Substring(0, lines[at].LastIndexOf(' ')) + " 400";
            });
            int capacity = world.StorageUnits(world.HomeIsland);
            Assert.That(capacity, Is.LessThan(400));
            int key = 0;
            var hud = new Hud(_root = new VisualElement(), command => world.Apply($"reserve-{key++}", command));
            hud.Show(world, System.Array.Empty<Simulation.Events.GameEvent>());
            hud.Select(tile);
            hud.ShowMore();
            Assert.That(Labels(), Does.Contain("400 timmer"));
            Assert.That(Stepper("Minimilager", "+").enabledSelf, Is.False);

            Press(Stepper("Minimilager", "−"));
            Assert.That(world.BuildingAt(tile)!.Reserve, Is.EqualTo(capacity));

            hud.Show(world, System.Array.Empty<Simulation.Events.GameEvent>());
            Press(Stepper("Minimilager", "−"));
            Assert.That(world.BuildingAt(tile)!.Reserve, Is.EqualTo(capacity - 5));
        }

        [Test]
        public void TheSawmillsSheetSaysWhenTheIslandsReservationKeepsMore()
        {
            _game.Apply(new Simulation.Commands.SetReservation(_game.World!.HomeIsland, Resource.Timber, 30));

            _hud.Select(ScriptedWeek.Sawmill);
            _hud.ShowMore();

            Assert.That(Labels(), Does.Contain("30 reserverade på Hemön går före"));
        }

        [Test]
        public void NyonsStoreListsTheGoodsInTheShipsOrderAndMovesOneFirst()
        {
            _hud.Select(Jetty(PrototypeHome.Nyon));
            _hud.ShowMore();
            Press(Action("Lagret på Nyön"));

            Assert.That(Labels(), Does.Contain("Lagret på Nyön"));
            // Game and hide wait for step 1, honey, mead and ale for step 2 and tar for step 3, which the scripted week has not
            // reached (TASK-061, TASK-062, TASK-063).
            var met = System.Enum.GetValues(typeof(Resource)).Cast<Resource>()
                .Where(r => r is not Resource.Game and not Resource.Hide and not Resource.Honey and not Resource.Mead and not Resource.Ale and not Resource.Tar).ToArray();
            Assert.That(_game.World!.Step, Is.EqualTo(0));
            Assert.That(Goods(), Is.EqualTo(met.Select(r => Capital(Texts.Name(r))).ToArray()));
            Assert.That(_root.Query<Button>(className: "good-button").ToList(), Has.Count.EqualTo(met.Length - 1)); // All but the first.

            Press(First("Ull"));

            Assert.That(_game.World!.ImportOrder(PrototypeHome.Nyon)[0], Is.EqualTo(Resource.Wool));
            Assert.That(Goods()[0], Is.EqualTo("Ull"));
        }

        [Test]
        public void NyonsStoreReservesTheChosenGoodAndTheShipsLeaveIt()
        {
            // The good that waits most on Nyön at the end of the tour's week, of those its villagers do not eat.
            var world = _game.World!;
            var good = System.Enum.GetValues(typeof(Resource)).Cast<Resource>().Where(r => r is not (Resource.Fish or Resource.Grain))
                .OrderByDescending(r => world.Stock(PrototypeHome.Nyon, r)).First();
            long stock = world.Stock(PrototypeHome.Nyon, good);
            Assert.That(stock, Is.GreaterThan(0), "Nothing waits on Nyön at the end of the tour's week.");
            string name = Texts.Name(good);
            _hud.OpenStore(PrototypeHome.Nyon);
            Press(Pick(Capital(name)));
            Assert.That(Labels(), Does.Contain($"Reservera {name}"));

            Press(Stepper($"Reservera {name}", "+"));
            Press(Stepper($"Reservera {name}", "+"));

            Assert.That(world.ReservationOf(PrototypeHome.Nyon, good), Is.EqualTo(10));
            Assert.That(Labels(), Does.Contain("10"));
            Assert.That(_root.Query(className: "good-chosen").ToList().Single().Q<Label>(className: "good-name").text, Is.EqualTo(Capital(name)));
            Assert.That(Labels(), Does.Contain(Texts.InStore(stock, 10)));

            // What is reserved does not wait for the ships.
            Assert.That(world.ForTheShips(PrototypeHome.Nyon, good), Is.EqualTo(System.Math.Max(stock - 10, 0)));
            _hud.Select(Jetty(PrototypeHome.Nyon));
            _hud.ShowMore();
            var waiting = Waiting(world);
            Assert.That(Labels(), Does.Contain(waiting.Length == 0 ? "Inget väntar på skeppen just nu." : $"Väntar på skeppen: {Texts.List(waiting)}."));
        }

        [Test]
        public void TheHomeJettyOpensTheStoreOfEachIslandItsShipsSailFrom()
        {
            _hud.Select(Jetty(_game.World!.HomeIsland));
            _hud.ShowMore();

            Press(Action("Skeppen från Nyön"));

            Assert.That(Labels(), Does.Contain("Lagret på Nyön"));
        }

        [Test]
        public void TheHomeStoreReservesButHasNoOrderForTheShips()
        {
            _hud.Select(Hex.Origin);
            _hud.ShowMore();
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

        /// <summary>The stepper button of the row with <paramref name="label"/>.</summary>
        private Button Stepper(string label, string text) =>
            _root.Query(className: "row").ToList().Single(r => r.Query<Label>().ToList().Any(l => l.text == label))
                .Query<Button>(className: "stepper-button").ToList().Single(b => b.text == text);

        /// <summary>What waits for the ships on Nyön, as the jetty lists it: in the ships' order, without the food it keeps and what is reserved.</summary>
        private static string[] Waiting(Simulation.World world) => world.ImportOrder(PrototypeHome.Nyon)
            .Select(r => (Resource: r, Left: world.ForTheShips(PrototypeHome.Nyon, r)))
            .Where(g => g.Left > 0)
            .Select(g => $"{g.Left} {Texts.Name(g.Resource, g.Left)}")
            .ToArray();

        private Button Pick(string good) =>
            _root.Query<Button>(className: "good-pick").ToList().Single(b => b.Q<Label>(className: "good-name").text == good);

        private Button First(string good) =>
            _root.Query(className: "good").ToList().Single(g => g.Q<Label>(className: "good-name").text == good).Q<Button>(className: "good-button");

        private string[] Goods() => _root.Query<Label>(className: "good-name").ToList().Select(l => l.text).ToArray();

        private Hex Jetty(string island) =>
            _game.World!.Buildings.First(b => b.Kind == BuildingKind.Jetty && b.Island == island).Tile;

        private static string Capital(string text) => char.ToUpperInvariant(text[0]) + text.Substring(1);

        private string[] Labels() => _root.Query<Label>().ToList().Select(l => l.text).ToArray();
    }
}
