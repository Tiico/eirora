using System;
using System.Linq;
using Eirora.Client.Look;
using Eirora.Client.UI;
using Eirora.Simulation;
using Eirora.Simulation.Economy;
using Eirora.Simulation.Events;
using Eirora.Simulation.Map;
using Eirora.Simulation.Time;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Eirora.Client.Tests
{
    /// <summary>The look of the prototype: render setup, the day-cycle light and the island map (art-direction.md, D-61).</summary>
    public sealed class ClientLookTests
    {
        private static readonly RestSchedule s_night = new(23 * 60, 8 * 60);

        [Test]
        public void TheClientRendersWithUrpInLinearColour()
        {
            Assert.That(GraphicsSettings.defaultRenderPipeline, Is.InstanceOf<UniversalRenderPipelineAsset>());
            Assert.That(PlayerSettings.colorSpace, Is.EqualTo(ColorSpace.Linear));
        }

        [Test]
        public void TheDailyReportGathersTheLiftedFogInOneLine()
        {
            // A day can lift tens of tiles; the report says how many where the first was lifted (TASK-032).
            var events = new GameEvent[]
            {
                new DawnBroke(0, 2), new FogLifted(1, 6, 0), new TilesRevealed(2, new[] { new Hex(3, -1) }), new FogLifted(3, 7, 0),
                new FogLifted(4, 7, -1),
            };

            Assert.That(Texts.Report(events), Is.EqualTo(new[]
            {
                "Byn vaknade till spelardygn 2.", "Dimman lättade över 3 brickor.", "Kartläggningen lyfte dimman över nya brickor.",
            }));
            Assert.That(Texts.Report(new GameEvent[] { new FogLifted(1, 6, 0) }), Is.EqualTo(new[] { "Dimman lättade över en bricka." }));
        }

        // Only the prototype's map has Nyön in the east; a generated world's island has no name and any direction.
        [Test]
        public void TheExpeditionNamesNyonOnlyOnThePrototypesMap()
        {
            Assert.That(Texts.Describe(new ExpeditionReturned(1, PrototypeHome.Nyon)), Is.EqualTo("Expeditionen kom hem. Den hittade Nyön i öster."));
            Assert.That(Texts.Describe(new ExpeditionReturned(1, "first")), Is.EqualTo("Expeditionen kom hem. Den hittade en ny ö."));
            Assert.That(Texts.IslandName("first"), Is.EqualTo("den nya ön"));
        }

        [Test]
        public void TheGameIsHeldUpright()
        {
            Assert.That(PlayerSettings.defaultInterfaceOrientation, Is.EqualTo(UIOrientation.Portrait));
        }

        [Test]
        public void TheShadersAreIncludedInBuilds()
        {
            foreach (var name in new[] { "Eirora/Flat", "Eirora/Sky", "Eirora/Glow" })
            {
                Assert.That(Shader.Find(name), Is.Not.Null, name);
            }
        }

        [TestCase("2026-10-15 01:00", 1f)]
        [TestCase("2026-10-15 07:30", 0f)]
        [TestCase("2026-10-15 13:00", 0f)]
        [TestCase("2026-10-15 22:30", 0.25f)]
        public void TheLightFollowsTheGameDay(string local, float darkness)
        {
            var world = World.Create(At("2026-10-14 14:00"), s_night);
            world.AdvanceTo(At(local));

            Assert.That(Atmosphere.MoodOf(world).Mood.Darkness, Is.EqualTo(darkness).Within(0.001f));
        }

        [Test]
        public void DawnLightGreetsTheWorldWhenRestEnds()
        {
            var world = World.Create(At("2026-10-14 14:00"), s_night);
            world.AdvanceTo(At("2026-10-15 07:10"));

            var (mood, _) = Atmosphere.MoodOf(world);

            Assert.That(mood.Horizon, Is.EqualTo(LightMood.Dawn.Horizon));
        }

        [Test]
        public void TheIslandIsLandExactlyWhereTheMapIs()
        {
            var map = new IslandMap(PrototypeHome.Map);

            foreach (var tile in map.Tiles)
            {
                Assert.That(map.IsLand(tile.Key), Is.EqualTo(PrototypeHome.Map.TerrainAt(tile.Key).IsLand()), tile.Key.ToString());
            }
        }

        [Test]
        public void TappingAHexCentreFindsThatHex()
        {
            // Every tile drawn is a tile the player can tap (D-79).
            foreach (var hex in new IslandMap(PrototypeHome.Map).Tiles.Keys)
            {
                Assert.That(HexLayout.FromWorld(hex.Center()), Is.EqualTo(hex), hex.ToString());
            }
        }

        [Test]
        public void RedrawingTheIslandReusesItsMeshes()
        {
            var world = World.Create(At("2026-10-14 14:00"), s_night);
            var parent = new GameObject("Island");
            var flat = new Material(Shader.Find("Eirora/Flat"));
            var glow = new Material(Shader.Find("Eirora/Glow"));
            var island = new IslandView(parent.transform, flat, glow, Looks.Load());
            try
            {
                var selected = Hex.Origin;
                island.Refresh(world);
                island.Select(selected);
                int meshes = Resources.FindObjectsOfTypeAll<Mesh>().Length;

                for (int i = 0; i < 5; i++)
                {
                    island.Refresh(world);
                    island.Select(i % 2 == 0 ? null : selected);
                }

                Assert.That(Resources.FindObjectsOfTypeAll<Mesh>().Length, Is.EqualTo(meshes));
                var settlement = parent.GetComponentsInChildren<MeshFilter>().Single(f => f.name == "Settlement");
                Assert.That(settlement.sharedMesh.vertexCount, Is.Positive, "The reused mesh still holds the settlement.");
            }
            finally
            {
                island.Dispose();
                UnityEngine.Object.DestroyImmediate(parent);
                UnityEngine.Object.DestroyImmediate(flat);
                UnityEngine.Object.DestroyImmediate(glow);
            }
        }

        private static TimeReading At(string local)
        {
            var wallClock = DateTime.ParseExact(local, "yyyy-MM-dd HH:mm", System.Globalization.CultureInfo.InvariantCulture);
            return new TimeReading(new DateTimeOffset(wallClock, TimeSpan.FromHours(2)).ToUnixTimeSeconds(), "Europe/Stockholm", 7200);
        }
    }
}
