using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Eirora.Client.UI;
using Eirora.Simulation;
using Eirora.Simulation.Economy;
using Eirora.Simulation.Map;
using Eirora.Simulation.Time;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace Eirora.Client.Tests
{
    /// <summary>
    /// Demolition from beside a building's sheet's close button (TASK-047, D-38, D-108): a confirmation shows exactly what
    /// comes back to the store and what does not before anything happens, and a building that cannot be demolished yet says
    /// why.
    /// </summary>
    public sealed class DemolitionSheetTests
    {
        private static readonly RestSchedule s_night = new(23 * 60, 8 * 60);
        private static readonly Hex s_storehouse = new(0, 0);
        private static readonly Hex s_woodcutter = new(1, -1);
        private static readonly Hex s_east = new(2, -1);
        private static readonly Hex s_west = new(-1, 0);

        private VisualElement _root = null!;
        private World _world = null!;
        private Hud _hud = null!;
        private int _key;

        [Test]
        public void TheConfirmationShowsWhatComesBackAndTheBuildingGoesOnlyWhenConfirmed()
        {
            Open(NewWorld());
            _hud.Select(s_woodcutter);

            Press(Demolish());

            Assert.That(Title(), Is.EqualTo("Riva skogshuggaren?"));
            Assert.That(Texts(), Is.EqualTo(new[]
            {
                "Tillbaka till lagret: 3 timmer.",
                "Två bybor blir lediga och bygger.",
                "Rutan blir ledig, och skogen på den går att fälla igen.",
            }));
            Assert.That(_world.BuildingAt(s_woodcutter), Is.Not.Null);

            Press(Choice("Riv"));

            Assert.That(_world.BuildingAt(s_woodcutter), Is.Null);
            Assert.That(_world.Stock(Resource.Timber), Is.EqualTo(43));
            Assert.That(_root.Q<Label>(className: "toast").text, Is.EqualTo("Skogshuggaren revs. 3 timmer kom tillbaka till lagret."));
            Assert.That(Title(), Is.Not.EqualTo("Riva skogshuggaren?"));
        }

        [Test]
        public void CancellingLeavesTheBuildingStanding()
        {
            Open(NewWorld());
            _hud.Select(s_woodcutter);
            Press(Demolish());

            Press(Choice("Avbryt"));

            Assert.That(Title(), Is.EqualTo("Skogshuggare"));
            Assert.That(_world.BuildingAt(s_woodcutter), Is.Not.Null);
        }

        // Nothing disappears that the confirmation has not shown (D-108).
        [Test]
        public void WhatDoesNotFitInTheStoreIsShownBeforehand()
        {
            Open(Edited(NewWorld(), lines => Stock(lines, "Timber", 148)));
            _hud.Select(s_woodcutter);

            Press(Demolish());

            Assert.That(Texts().Take(2), Is.EqualTo(new[] { "Tillbaka till lagret: 2 timmer.", "Ryms inte i lagret och kommer inte tillbaka: 1 timmer." }));
        }

        [Test]
        public void TheForceAndTheGrowthThatDoNotComeBackAreShown()
        {
            Open(Edited(NewWorld(), lines =>
            {
                Settlement(lines, 20);
                Buildings(lines, "building Cottage -1 0 1 0 0 0 0 0 0 0 0 0 0 0", "building Sawmill 0 -1 2 0 0 0 0 0 0 0 0 1 0 0");
            }));

            _hud.Select(s_west);
            Press(Demolish());
            Assert.That(Texts(), Does.Contain("Sedan finns bostäder för 16 av 20 bybor. Ingen flyttar, men byn växer inte förrän det finns plats."));

            Press(Choice("Avbryt"));
            _hud.Select(new Hex(0, -1));
            Press(Demolish());
            Assert.That(Texts().First(), Is.EqualTo("Tillbaka till lagret: 7 timmer, 5 plankor och 5 sten."));
            Assert.That(Texts(), Does.Contain("80 Styrka kommer inte tillbaka."));
        }

        [Test]
        public void TheHomeIslandsLastStorehouseSaysWhyItStands()
        {
            Open(NewWorld());
            _hud.Select(s_storehouse);

            Press(Demolish());

            Assert.That(Title(), Is.EqualTo("Förrådet kan inte rivas"));
            Assert.That(Texts(), Is.EqualTo(new[] { "Hemöns sista förråd står kvar, eftersom allt betalas ur dess lager." }));
            Assert.That(_root.Query<Button>(className: "button-primary").ToList(), Is.Empty);

            Press(Choice("Tillbaka"));

            Assert.That(Title(), Is.EqualTo("Förråd"));
        }

        [Test]
        public void AStorehouseWhoseGoodsWouldNotFitSaysHowMuchToUseFirst()
        {
            Open(Edited(NewWorld(), lines =>
            {
                Stock(lines, "Fish", 200);
                Buildings(lines, $"building Storehouse {s_east.Q} {s_east.R} 1 1 0 0 0 1 4 0 0 0 0 0");
            }));
            _hud.Select(s_east);

            Press(Demolish());

            Assert.That(Title(), Is.EqualTo("Förrådet kan inte rivas än"));
            Assert.That(Texts(), Is.EqualTo(new[] { "Använd eller skeppa först: 50 fisk.", "Ingen vara får försvinna när något rivs." }));
            Assert.That(_root.Query<Button>(className: "button-primary").ToList(), Is.Empty);
        }

        [Test]
        public void DuringTheRestNothingIsOffered()
        {
            var world = NewWorld();
            world.AdvanceTo(App.ScriptedWeek.At("2026-10-14 23:30"));
            Open(world);

            _hud.Select(s_woodcutter);

            Assert.That(_root.Query<Button>(className: "sheet-demolish").ToList(), Is.Empty);
        }

        private static World NewWorld() => World.Create(PrototypeHome.Map, App.ScriptedWeek.At("2026-10-14 14:00"), s_night);

        private static World Edited(World world, Action<List<string>> edit) => EditedWorld.Edit(world, edit);

        private static void Stock(List<string> lines, string resource, int units) =>
            lines[lines.FindIndex(l => l.StartsWith($"stock home {resource} ", StringComparison.Ordinal))] = $"stock home {resource} {units * Quantity.PerUnit}";

        private static void Settlement(List<string> lines, int villagers) =>
            lines[lines.FindIndex(l => l.StartsWith("settlement ", StringComparison.Ordinal))] = $"settlement {villagers} 0 0";

        private static void Buildings(List<string> lines, params string[] buildings) =>
            lines.InsertRange(lines.FindLastIndex(l => l.StartsWith("building ", StringComparison.Ordinal)) + 1, buildings);

        private void Open(World world)
        {
            _world = world;
            _root = new VisualElement();
            _hud = new Hud(_root, command => _world.Apply($"demolition-{_key++}", command));
            _hud.Show(world, Array.Empty<Simulation.Events.GameEvent>());
        }

        /// <summary>Clicks a button as a tap would, through its <see cref="Clickable"/>; the root is in no panel to send events to.</summary>
        private static void Press(Button button)
        {
            var invoke = typeof(Clickable).GetMethod("Invoke", BindingFlags.Instance | BindingFlags.NonPublic, null, new[] { typeof(EventBase) }, null);
            Assert.That(invoke, Is.Not.Null, "Clickable.Invoke is gone; find another way to press a button.");
            using var tap = NavigationSubmitEvent.GetPooled();
            invoke!.Invoke(button.clickable, new object[] { tap });
        }

        private Button Demolish() => _root.Q<Button>(className: "sheet-demolish");

        private Button Choice(string text) => _root.Query<Button>(className: "button").ToList().Single(b => b.text == text);

        private string Title() => _root.Q<Label>(className: "sheet-title").text;

        private string[] Texts() => _root.Query<Label>(className: "sheet-text").ToList().Select(l => l.text).ToArray();
    }
}
