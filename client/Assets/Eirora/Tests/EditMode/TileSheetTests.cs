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
    /// A known tile's ring and card: what it is special for on the ring, everything on the card, and the forest's timber
    /// (TASK-037, economy-v1.md, section 3.1; ui-prototypes.html, direction C).
    /// </summary>
    public sealed class TileSheetTests
    {
        private VisualElement _root = null!;
        private GameSession _game = null!;
        private Hud _hud = null!;

        [SetUp]
        public void SetUp()
        {
            var clock = new FixedClock(ScriptedWeek.At("2026-10-14 14:00"));
            _game = new GameSession(clock, null, () => PrototypeHome.Map); // The tiles below are Hemön's.
            _game.Begin(new RestSchedule(23 * 60, 8 * 60));
            _root = new VisualElement();
            _hud = new Hud(_root, command => _game.Apply(command));
            _hud.Show(_game.World!, _game.SinceDawn);
        }

        [Test]
        public void TheRingHoldsWhatTheTileIsSpecialForAndTheWayToTheCard()
        {
            _hud.Select(new Hex(-1, 1)); // A meadow by the forest.

            Assert.That(Choices(), Is.EqualTo(new[] { "Skogshuggare", "Mer" }));
            Assert.That(_root.Query<Button>(className: "action").ToList(), Is.Empty, "The card waits behind the ring.");
        }

        [Test]
        public void TheCardKeepsWhatTheTileIsSpecialForAtTheTop()
        {
            _hud.Select(new Hex(-1, 1));

            _hud.ShowMore();

            Assert.That(Actions(), Is.EqualTo(new[] { "Skogshuggare", "Långhus", "Stuga", "Förråd", "Sågverk", "Väveri", "Väveri med handkraft", "Utkikstorn", "Lägg väg" }));
        }

        [Test]
        public void AnotherTileStartsWithItsRingAgain()
        {
            _hud.Select(new Hex(-1, 1));
            _hud.ShowMore();

            _hud.Select(new Hex(2, 1)); // A meadow by the forest and the shore.

            Assert.That(Choices(), Is.EqualTo(new[] { "Skogshuggare", "Brygga", "Mer" }));
        }

        [Test]
        public void TheRingsLastChoiceUnfoldsTheCardWithTheRoad()
        {
            _hud.Select(new Hex(-1, 1));

            _hud.Choices.Last().Perform();

            Assert.That(Actions(), Does.Contain("Lägg väg").And.Contain("Väveri med handkraft"));
        }

        [Test]
        public void ARingsChoiceShowsItsWordsFirstAndBuildsWhenChosenAgain()
        {
            _hud.Select(new Hex(-1, 1));
            var woodcutter = _root.Query<Button>(className: "choice").ToList().First();
            Assert.That(_root.Q<Label>(className: "caption-title").text, Is.EqualTo("Äng"));

            Press(woodcutter);

            Assert.That(_root.Q<Label>(className: "caption-title").text, Is.EqualTo("Skogshuggare"));
            Assert.That(_root.Query<Label>(className: "caption-text").ToList().Select(l => l.text), Is.EqualTo(new[] { "0,5 timmer/h per skogsruta, högst två", "6 timmer · 8 h arbete" }));
            Assert.That(_game.World!.Projects, Is.Empty);

            Press(_root.Query<Button>(className: "choice").ToList().First());

            Assert.That(_game.World!.Projects.Select(p => p.Building), Is.EqualTo(new[] { (BuildingKind?)BuildingKind.Woodcutter }));
        }

        [Test]
        public void AForestTileSaysHowMuchTimberIsLeft()
        {
            _hud.Select(new Hex(-2, 2));

            Assert.That(Labels().Any(l => l.EndsWith("40 timmer att fälla.")), Is.True, "The ring's caption says it.");

            _hud.ShowMore();

            Assert.That(Labels(), Does.Contain("40 timmer att fälla."));
        }

        /// <summary>Clicks a button as a tap would, through its <see cref="Clickable"/>; the root is in no panel to send events to.</summary>
        private static void Press(Button button)
        {
            var invoke = typeof(Clickable).GetMethod("Invoke", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic, null, new[] { typeof(EventBase) }, null);
            using var tap = NavigationSubmitEvent.GetPooled();
            invoke!.Invoke(button.clickable, new object[] { tap });
        }

        private string[] Labels() => _root.Query<Label>().ToList().Select(l => l.text).ToArray();

        private string[] Choices() => _hud.Choices.Select(c => c.Title).ToArray();

        private string[] Actions() => _root.Query<Button>(className: "action").ToList().Select(b => b.Q<Label>(className: "action-title").text).ToArray();
    }
}
