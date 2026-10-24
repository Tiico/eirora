using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Eirora.Client.Look;
using Eirora.Client.UI;
using Eirora.Simulation;
using Eirora.Simulation.Commands;
using Eirora.Simulation.Economy;
using Eirora.Simulation.Map;
using Eirora.Simulation.Time;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace Eirora.Client.Tests
{
    /// <summary>
    /// Roads and carriers in the client (TASK-051, D-112): the island draws each road as a track to the roads and
    /// buildings beside it, and the carriers on their way while the villagers are awake; the player lays roads tile by
    /// tile from a tile's sheet.
    /// </summary>
    public sealed class RoadTests
    {
        private static readonly RestSchedule s_night = new(23 * 60, 8 * 60);
        private static readonly Hex s_meadow = new(0, -1); // Beside the storehouse and the start woodcutter.
        private static readonly Hex s_beyond = new(0, -2);

        [Test]
        public void ARoadIsATrackFromItsTilesCentreToEachRoadAndBuildingBesideIt()
        {
            var world = EditedWorld.WithRoads(Morning(), new[] { s_meadow, s_beyond });

            WithIsland(world, roads =>
            {
                // A patch of four triangles at each centre, and two for each track: the meadow leads to the storehouse,
                // the woodcutter and the road beyond it, and the road beyond back to the meadow.
                int leads = Enumerable.Range(0, Hex.Directions).Select(s_meadow.Neighbour).Count(n => world.HasRoad(n) || world.BuildingAt(n) is not null)
                    + Enumerable.Range(0, Hex.Directions).Select(s_beyond.Neighbour).Count(n => world.HasRoad(n) || world.BuildingAt(n) is not null);
                Assert.That(leads, Is.GreaterThanOrEqualTo(4));
                Assert.That(roads.triangles.Length / 3, Is.EqualTo((2 * 4) + (2 * leads)));
            });
        }

        [Test]
        public void ARoadInTheQueueIsDrawnPaleUntilItIsLaid()
        {
            var world = Morning();
            Assert.That(world.Apply("road", new LayRoad(s_meadow)).Accepted, Is.True);

            Color planned = default;
            WithIsland(world, roads => planned = roads.colors[0]);
            world.AdvanceTo(At("2026-10-15 12:00"));
            Assert.That(world.HasRoad(s_meadow), Is.True);
            Color laid = default;
            WithIsland(world, roads => laid = roads.colors[0]);

            Assert.That(laid, Is.EqualTo(Palette.Road.linear));
            Assert.That(planned.grayscale, Is.GreaterThan(laid.grayscale));
        }

        [Test]
        public void TheCarriersWalkWhileTheVillagersAreAwakeAndRestWithThem()
        {
            var world = Morning();
            int awake = 0, resting = 0;
            WithIsland(world, _ => { }, settlement => awake = settlement.triangles.Length);
            world.AdvanceTo(At("2026-10-16 01:00"));
            WithIsland(world, _ => { }, settlement => resting = settlement.triangles.Length);

            // By night the villagers and the carriers are indoors; the buildings are the same.
            Assert.That(awake, Is.GreaterThan(resting));
        }

        [Test]
        public void LayingRoadsLaysOneOnEachTileTappedUntilThePlayerIsDone()
        {
            var world = Morning();
            var root = new VisualElement();
            var hud = new Hud(root, command => world.Apply($"hud-{world.Projects.Count}-{command.GetType().Name}", command));
            hud.Show(world, Array.Empty<Simulation.Events.GameEvent>());
            hud.Select(s_meadow);
            hud.ShowMoreBuildings(); // The meadow by the forest takes a woodcutter first; a road waits behind the fold.
            var lay = root.Query<Button>(className: "action").ToList().Single(a => Label(a, "action-title") == "Lägg väg");
            Assert.That(Label(lay, "action-cost"), Is.EqualTo("2 sten och 4 h arbete per bricka · bärarna går dubbelt så fort"));

            Press(lay);
            Assert.That(hud.IsLayingRoads, Is.True);
            Assert.That(root.Q<Label>(className: "sheet-title").text, Is.EqualTo("Lägg väg"));
            hud.Select(s_beyond);
            hud.Select(Hex.Origin); // The storehouse stands there: no road, and the sheet stays open.

            Assert.That(world.Projects.Select(p => (p.Kind, p.Tile)), Is.EqualTo(new[] { (ProjectKind.Road, s_meadow), (ProjectKind.Road, s_beyond) }));
            Assert.That(root.Q<Label>(className: "toast").text, Is.EqualTo("Här kan ingen väg ligga."));
            Assert.That(hud.IsLayingRoads, Is.True);

            Press(root.Query<Button>(className: "action").ToList().Single(a => Label(a, "action-title") == "Klar"));
            Assert.That(hud.IsLayingRoads, Is.False);
        }

        [Test]
        public void ARoadInTheQueueHasASheetOfItsOwn()
        {
            var world = Morning();
            Assert.That(world.Apply("road", new LayRoad(s_meadow)).Accepted, Is.True);
            var root = new VisualElement();
            var hud = new Hud(root, _ => throw new InvalidOperationException("Nothing is played here."));
            hud.Show(world, Array.Empty<Simulation.Events.GameEvent>());

            hud.Select(s_meadow);

            Assert.That(root.Q<Label>(className: "sheet-title").text, Is.EqualTo("Väg byggs"));
            Assert.That(root.Q<Label>(className: "sheet-subtitle").text, Does.EndWith("· 4 h arbete kvar"));
        }

        private static void WithIsland(World world, Action<Mesh> roads, Action<Mesh>? settlement = null)
        {
            var parent = new GameObject("Island");
            var flat = new Material(Shader.Find("Eirora/Flat"));
            var glow = new Material(Shader.Find("Eirora/Glow"));
            var island = new IslandView(parent.transform, flat, glow, Looks.Load());
            try
            {
                island.Refresh(world);
                var filters = parent.GetComponentsInChildren<MeshFilter>(includeInactive: true);
                roads(filters.Single(f => f.name == "Roads").sharedMesh);
                settlement?.Invoke(filters.Single(f => f.name == "Settlement").sharedMesh);
            }
            finally
            {
                island.Dispose();
                UnityEngine.Object.DestroyImmediate(parent);
                UnityEngine.Object.DestroyImmediate(flat);
                UnityEngine.Object.DestroyImmediate(glow);
            }
        }

        private static World Morning()
        {
            var world = World.Create(PrototypeHome.Map, At("2026-10-14 14:00"), s_night);
            world.AdvanceTo(At("2026-10-15 09:00"));
            return world;
        }

        private static void Press(Button button)
        {
            var invoke = typeof(Clickable).GetMethod("Invoke", BindingFlags.Instance | BindingFlags.NonPublic, null, new[] { typeof(EventBase) }, null);
            Assert.That(invoke, Is.Not.Null, "Clickable.Invoke is gone; find another way to press a button.");
            using var tap = NavigationSubmitEvent.GetPooled();
            invoke!.Invoke(button.clickable, new object[] { tap });
        }

        private static string Label(VisualElement action, string className) => action.Q<Label>(className: className)?.text ?? "–";

        private static TimeReading At(string local) => App.ScriptedWeek.At(local);
    }
}
