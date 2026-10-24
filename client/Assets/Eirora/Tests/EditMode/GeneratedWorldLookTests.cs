using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Eirora.Client.App;
using Eirora.Client.Look;
using Eirora.Simulation;
using Eirora.Simulation.Commands;
using Eirora.Simulation.Economy;
using Eirora.Simulation.Map;
using Eirora.Simulation.SelfCare;
using Eirora.Simulation.Time;
using Eirora.WorldGeneration;
using NUnit.Framework;
using UnityEngine;

namespace Eirora.Client.Tests
{
    /// <summary>
    /// The client draws any world's map (TASK-033): the prototype's world looks exactly as before, and generated worlds
    /// are drawn whole, with the islands an expedition has yet to find hidden under the fog.
    /// </summary>
    public sealed class GeneratedWorldLookTests
    {
        private static readonly RestSchedule s_night = new(23 * 60, 8 * 60);
        private static readonly ProceduralArchipelagoV1 s_generator = new();
        private static int s_key;

        [Test]
        public void ThePrototypesWorldLooksAsBefore()
        {
            var world = World.Create(At("2026-10-14 14:00"), s_night);
            world.AdvanceTo(At("2026-10-15 13:00"));

            Assert.That(Picture(world), Is.EqualTo(PrototypeAtStart));
        }

        [Test]
        public void ThePrototypesWorldLooksAsBeforeOnceNyonIsFound()
        {
            var world = WithNyonFound();

            Assert.That(Picture(world), Is.EqualTo(PrototypeWithNyon));
        }

        [TestCase(1UL)]
        [TestCase(7UL)]
        [TestCase(42UL)]
        public void AGeneratedWorldIsDrawnWholeWithItsIslandsHiddenUntilFound(ulong seed)
        {
            var map = s_generator.Generate(seed);
            var world = World.Create(map, At("2026-10-14 14:00"), s_night);
            var drawn = new IslandMap(map);

            Assert.That(drawn.Tiles.Count, Is.EqualTo(map.Disc.Count));
            foreach (var hex in drawn.Tiles.Keys)
            {
                Assert.That(HexLayout.FromWorld(hex.Center()), Is.EqualTo(hex), hex.ToString());
            }

            WithIsland(world, (parent, _) =>
            {
                var away = map.Islands.Where(i => i.Id != map.IslandAt(map.Start)).Select(i => i.Id).ToList();
                var islands = parent.GetComponentsInChildren<MeshFilter>(includeInactive: true).Where(f => away.Contains(f.name)).ToList();
                Assert.That(islands.Select(f => f.name), Is.EquivalentTo(away), "Every other island has a mesh of its own.");
                Assert.That(islands.All(f => !f.gameObject.activeSelf), "No island is drawn before an expedition finds it.");
            });
        }

        [Test]
        public void TheIslandTheFirstExpeditionFindsAppears()
        {
            var map = s_generator.Generate(42);
            var world = Expedition(World.Create(map, At("2026-10-14 14:00"), s_night));

            WithIsland(world, (parent, _) =>
            {
                string found = Founding.For(map).ExpeditionIsland!;
                var shown = parent.GetComponentsInChildren<MeshFilter>(includeInactive: false).Select(f => f.name).ToList();
                Assert.That(shown, Does.Contain(found));
                Assert.That(shown, Has.None.StartsWith("skerry-"), "The skerries wait under the fog.");
            });
        }

        [Test]
        public void TheLargestGeneratedMapStaysWithinTheTrianglesTheMapFormatPlannedForWithARoadOnEveryLandTile()
        {
            // The map with the most land of fifty seeds; every map has the same disc (D-80), so land is what grows. Roads
            // lie on land, so a road on every land tile is the most the map can carry (D-112).
            var largest = Enumerable.Range(0, 50).Select(seed => s_generator.Generate((ulong)seed))
                .OrderByDescending(m => Enumerable.Range(0, m.Disc.Count).Count(i => m.TerrainAt(m.Disc[i]).IsLand())).First();
            var land = Enumerable.Range(0, largest.Disc.Count).Select(i => largest.Disc[i]).Where(h => largest.TerrainAt(h).IsLand()).ToList();
            var world = EditedWorld.WithRoads(World.Create(largest, At("2026-10-14 14:00"), s_night), land);
            Assert.That(world.Roads.Count, Is.EqualTo(land.Count));

            WithIsland(world, (parent, _) =>
            {
                var filters = parent.GetComponentsInChildren<MeshFilter>(includeInactive: true)
                    .Where(f => f.name != "Settlement" && f.name != "Lights" && f.name != "Selection" && f.name != "Standing still")
                    .ToList();
                int roads = filters.Single(f => f.name == "Roads").sharedMesh.triangles.Length / 3;
                int map = filters.Sum(f => f.sharedMesh.triangles.Length / 3);

                // ADR-0010: at most 1 801 tiles of 12–40 triangles, under 75 000 for the whole map, roads and all.
                Assert.That(roads, Is.GreaterThan(4 * land.Count));
                Assert.That(map, Is.LessThan(75_000));
            });
        }

        [Test]
        public void ANewSessionMakesItsWorldOnAGeneratedMap()
        {
            var session = new GameSession(new FixedClock(At("2026-10-14 14:00")), null);

            session.Begin(s_night);

            Assert.That(session.World!.Map.Source, Is.EqualTo(MapSource.Procedural));
            Assert.That(session.World.Map.GeneratorId, Is.EqualTo(ProceduralArchipelagoV1.GeneratorId));
        }

        [Test]
        public void TheCameraFramesTheSettlementOfAGeneratedWorld()
        {
            var map = s_generator.Generate(7);
            var world = World.Create(map, At("2026-10-14 14:00"), s_night);
            var camera = new GameObject("Camera").AddComponent<Camera>();
            try
            {
                camera.aspect = 1080f / 2340f;
                var rig = new CameraRig(camera, _ => false);
                var storehouse = world.Buildings.First(b => b.Kind == BuildingKind.Storehouse).Tile.Center();

                rig.Frame(new IslandMap(map), storehouse);

                var seen = camera.WorldToViewportPoint(storehouse);
                Assert.That(seen.x, Is.InRange(0.3f, 0.7f));
                Assert.That(seen.y, Is.InRange(0.3f, 0.7f));
                Assert.That(seen.z, Is.Positive);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(camera.gameObject);
            }
        }

        // Fingerprints of the prototype's world as drawn before TASK-033, with the storehouse's carrier on its way (TASK-051).
        private const string PrototypeAtStart = "12466:8C42ACF15F08FE31";
        private const string PrototypeWithNyon = "12507:0E79300D3FEB1565";

        /// <summary>A fingerprint of every visible triangle and its colour, whatever mesh or order it is drawn in.</summary>
        internal static string Picture(World world)
        {
            string picture = string.Empty;
            WithIsland(world, (parent, _) =>
            {
                var triangles = new List<string>();
                foreach (var filter in parent.GetComponentsInChildren<MeshFilter>(includeInactive: false))
                {
                    var mesh = filter.sharedMesh;
                    var vertices = mesh.vertices;
                    var colours = mesh.colors;
                    var indices = mesh.triangles;
                    for (int i = 0; i < indices.Length; i += 3)
                    {
                        var corners = new[] { indices[i], indices[i + 1], indices[i + 2] }
                            .Select(v => string.Format(CultureInfo.InvariantCulture, "{0:F3},{1:F3},{2:F3}/{3:F3},{4:F3},{5:F3}", vertices[v].x, vertices[v].y, vertices[v].z, colours[v].r, colours[v].g, colours[v].b));
                        triangles.Add(filter.GetComponent<MeshRenderer>().sharedMaterial.shader.name + " " + string.Join(" ", corners));
                    }
                }

                triangles.Sort(StringComparer.Ordinal);
                using var sha = SHA256.Create();
                var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(string.Join("\n", triangles)));
                picture = triangles.Count.ToString(CultureInfo.InvariantCulture) + ":" + BitConverter.ToString(hash, 0, 8).Replace("-", string.Empty);
            });
            return picture;
        }

        /// <summary>Draws the world and hands the drawing to <paramref name="look"/>, then frees it.</summary>
        private static void WithIsland(World world, Action<GameObject, IslandView> look)
        {
            var parent = new GameObject("Island");
            var flat = new Material(Shader.Find("Eirora/Flat"));
            var glow = new Material(Shader.Find("Eirora/Glow"));
            var island = new IslandView(parent.transform, flat, glow, Looks.Load());
            try
            {
                island.Refresh(world);
                look(parent, island);
            }
            finally
            {
                island.Dispose();
                UnityEngine.Object.DestroyImmediate(parent);
                UnityEngine.Object.DestroyImmediate(flat);
                UnityEngine.Object.DestroyImmediate(glow);
            }
        }

        private static World WithNyonFound()
        {
            var world = Expedition(World.Create(At("2026-10-14 14:00"), s_night), new Hex(1, 1), sawmill: new Hex(-1, 0));
            Assert.That(world.IsKnown(PrototypeHome.NyonCentre));
            return world;
        }

        /// <summary>A jetty, two days of walking and an expedition that finds the first island, as in the simulation's tests.</summary>
        private static World Expedition(World world, Hex? jetty = null, Hex? sawmill = null)
        {
            world.AdvanceTo(At("2026-10-15 07:00"));
            var harbour = jetty ?? world.Map.Features.LandOf(world.Map.IslandAt(world.Map.Start)!).First(h => world.CheckBuild(BuildingKind.Jetty, h) == Rejection.None);
            Assert.That(Apply(world, new Build(BuildingKind.Jetty, harbour)).Accepted);
            if (sawmill is Hex mill)
            {
                Assert.That(Apply(world, new Build(BuildingKind.Sawmill, mill)).Accepted);
            }

            world.AdvanceTo(At("2026-10-15 13:00"));
            Assert.That(Apply(world, new RecordSelfCare(world.GameDay, Habit.Steps, ContributionSource.Verified, 50_000)).Accepted);
            world.AdvanceTo(At("2026-10-16 08:00"));
            Assert.That(Apply(world, new RecordSelfCare(world.GameDay, Habit.Steps, ContributionSource.Verified, 50_000)).Accepted);
            Assert.That(Apply(world, new SendExpedition()).Accepted);
            world.AdvanceTo(At("2026-10-18 13:00"));
            Assert.That(world.IsKnown(world.ExpeditionFinds!.Value));
            return world;
        }

        private static CommandResult Apply(World world, GameCommand command) => world.Apply($"look-{++s_key}", command);

        private static TimeReading At(string local)
        {
            var wallClock = DateTime.ParseExact(local, "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
            var moment = new DateTimeOffset(wallClock, TimeSpan.FromHours(2));
            return new TimeReading(moment.ToUnixTimeSeconds(), "Europe/Stockholm", 7200);
        }
    }
}
