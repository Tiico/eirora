using System.Linq;
using System.Reflection;
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
    /// The player's say over the work (TASK-044, economy-v1.md, section 4.1): a building's place in the staffing order
    /// and its pause on its sheet, and the building queue, where a project is moved first.
    /// </summary>
    public sealed class StaffingSheetTests
    {
        private static readonly Hex Storehouse = new(0, 0);
        private static readonly Hex FishingHut = new(1, 0);
        private static readonly Hex L1 = new(-1, 0);
        private static readonly Hex L2 = new(0, -1);

        private FixedClock _clock = null!;
        private VisualElement _root = null!;
        private GameSession _game = null!;
        private Hud _hud = null!;

        [SetUp]
        public void SetUp()
        {
            _clock = new FixedClock(ScriptedWeek.At("2026-10-14 14:00"));
            _game = new GameSession(_clock, null, () => PrototypeHome.Map);
            _game.Begin(new RestSchedule(23 * 60, 8 * 60));
            _root = new VisualElement();
            _hud = new Hud(_root, command => _game.Apply(command));
            _game.Changed += () => _hud.Show(_game.World!, _game.SinceDawn);
            _hud.Show(_game.World!, _game.SinceDawn);
        }

        [Test]
        public void AProducingBuildingsSheetShowsItsPlaceInTheStaffingAndAPause()
        {
            _hud.Select(FishingHut);
            _hud.ShowMore();

            Assert.That(Labels().Any(l => l.EndsWith(" · går för fullt · 1,25 fisk/h")), Is.True);
            Assert.That(Labels(), Does.Contain("Plats i bemanningen"));
            Assert.That(Labels(), Does.Contain("2 av 4"));
            Assert.That(Actions(), Does.Contain("Pausa"));
            Assert.That(Stepper("−").enabledSelf && Stepper("+").enabledSelf, Is.True);
        }

        [Test]
        public void PausingFreesTheVillagersAndTheSheetOffersToResume()
        {
            _hud.Select(FishingHut);
            _hud.ShowMore();

            Press(Action("Pausa"));

            Assert.That(_game.World!.BuildingAt(FishingHut)!.Paused, Is.True);
            Assert.That(_game.World.FreeVillagers, Is.EqualTo(7));
            Assert.That(Labels().Any(l => l.EndsWith(" · pausad")), Is.True);
            Assert.That(Labels(), Does.Contain("Pausad: byborna bygger i stället. Nivån och lagret står kvar."));
            Assert.That(Actions(), Does.Contain("Återuppta").And.Not.Contain("Pausa"));

            Press(Action("Återuppta"));

            Assert.That(_game.World.BuildingAt(FishingHut)!.Staffed, Is.True);
        }

        [Test]
        public void MovingABuildingUpTheStaffingGivesItTheFirstPlace()
        {
            _hud.Select(FishingHut);
            _hud.ShowMore();

            Press(Stepper("−"));

            Assert.That(_game.World!.StaffingOrder[0].Tile, Is.EqualTo(FishingHut));
            Assert.That(Labels(), Does.Contain("1 av 4"));
            Assert.That(Stepper("−").enabledSelf, Is.False);
        }

        [Test]
        public void AStorehouseHasAPlaceInTheStaffingButNothingToPause()
        {
            _hud.Select(Storehouse);
            _hud.ShowMore();

            Assert.That(Labels(), Does.Contain("1 av 4"));
            Assert.That(Actions(), Does.Not.Contain("Pausa"));
        }

        [Test]
        public void TheQueueListsItsProjectsInOrderAndMovesOneFirst()
        {
            _game.Apply(new Build(BuildingKind.Cottage, L1));
            _game.Apply(new Build(BuildingKind.Cottage, L2));
            Assert.That(_root.Q<Button>(name: "Byggkö").Q<Label>(className: "badge").text, Is.EqualTo("2"));

            _hud.OpenQueue();

            Assert.That(Labels(), Does.Contain("Fem lediga bybor bygger på det första projektet."));
            Assert.That(QueueItems(), Is.EqualTo(new[] { "Stuga | Byggs nu · 32 h arbete kvar", "Stuga | 32 h arbete kvar" }));
            Assert.That(_root.Query<Button>(className: "queue-button").ToList(), Has.Count.EqualTo(1));
            Assert.That(Actions(), Is.EqualTo(new[] { "Skynda på" }));

            Press(_root.Q<Button>(className: "queue-button"));

            Assert.That(_game.World!.Projects.Select(p => p.Tile), Is.EqualTo(new[] { L2, L1 }));
        }

        [Test]
        public void AProjectsSheetSaysItsPlaceInTheQueueAndMovesItFirst()
        {
            _game.Apply(new Build(BuildingKind.Cottage, L1));
            _game.Apply(new Build(BuildingKind.Cottage, L2));

            _hud.Select(L2);
            _hud.ShowMore();

            Assert.That(Labels(), Does.Contain("Nummer 2 i byggkön. Lediga bybor bygger på det första projektet."));
            Press(Action("Bygg först"));

            Assert.That(_game.World!.Projects[0].Tile, Is.EqualTo(L2));
            Assert.That(Labels(), Does.Contain("Först i byggkön. Lediga bybor bygger här medan de är vakna."));
            Assert.That(Actions(), Does.Not.Contain("Bygg först"));
        }

        [Test]
        public void DuringTheRestTheQueueIsOnlyShown()
        {
            _game.Apply(new Build(BuildingKind.Cottage, L1));
            _game.Apply(new Build(BuildingKind.Cottage, L2));
            _game.Apply(new Build(BuildingKind.Storehouse, new Hex(2, -1)));
            _clock.Current = ScriptedWeek.At("2026-10-14 23:30");
            _game.CatchUp();

            _hud.OpenQueue();

            Assert.That(QueueItems(), Has.Length.EqualTo(2));
            Assert.That(_root.Query<Button>(className: "queue-button").ToList(), Is.Empty);
            Assert.That(Actions(), Is.Empty);
            Assert.That(Labels(), Does.Contain("Byborna sover. Kön går att ordna efter gryningen."));
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

        private string[] Labels() => _root.Query<Label>().ToList().Select(l => l.text).ToArray();

        private string[] Actions() => _root.Query<Button>(className: "action").ToList().Select(b => b.Q<Label>(className: "action-title").text).ToArray();

        private string[] QueueItems() => _root.Query(className: "queue-item").ToList()
            .Select(i => $"{i.Q<Label>(className: "queue-title").text} | {i.Q<Label>(className: "queue-detail").text}")
            .ToArray();
    }
}
