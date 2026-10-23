using System.Linq;
using Eirora.Client.App;
using Eirora.Client.UI;
using Eirora.Simulation.Map;
using Eirora.Simulation.Time;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace Eirora.Client.Tests
{
    /// <summary>The sheet of a known tile: what it is special for first, and the forest's timber (TASK-037, economy-v1.md, section 3.1).</summary>
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
        public void BuildingsForAnyLandWaitBehindOneRowWhereTheTileIsSpecialForSomething()
        {
            _hud.Select(new Hex(-1, 1)); // A meadow by the forest.

            Assert.That(Actions(), Is.EqualTo(new[] { "Skogshuggare", "Fler byggnader" }));
        }

        [Test]
        public void MoreBuildingsKeepsWhatTheTileIsSpecialForAtTheTop()
        {
            _hud.Select(new Hex(-1, 1));

            _hud.ShowMoreBuildings();

            Assert.That(Actions(), Is.EqualTo(new[] { "Skogshuggare", "Långhus", "Stuga", "Förråd", "Sågverk", "Väveri", "Utkikstorn" }));
        }

        [Test]
        public void AnotherTileStartsFoldedAgain()
        {
            _hud.Select(new Hex(-1, 1));
            _hud.ShowMoreBuildings();

            _hud.Select(new Hex(2, 1)); // A meadow by the forest and the shore.

            Assert.That(Actions(), Is.EqualTo(new[] { "Skogshuggare", "Brygga", "Fler byggnader" }));
        }

        [Test]
        public void AForestTileSaysHowMuchTimberIsLeft()
        {
            _hud.Select(new Hex(-2, 2));

            Assert.That(Labels(), Does.Contain("40 timmer att fälla."));
        }

        private string[] Labels() => _root.Query<Label>().ToList().Select(l => l.text).ToArray();

        private string[] Actions() => _root.Query<Button>(className: "action").ToList().Select(b => b.Q<Label>(className: "action-title").text).ToArray();
    }
}
