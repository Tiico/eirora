using Eirora.Simulation.Map;
using System;
using System.Linq;
using Eirora.Client.Look;
using Eirora.Simulation;
using Eirora.Simulation.Economy;
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
        public void FishingWaterIsWaterAndEveryOtherPlaceIsLand()
        {
            var map = new IslandMap();

            foreach (var site in PrototypeIsland.Sites)
            {
                bool water = (site.Kinds & SiteKind.FishingWater) != 0;
                Assert.That(map.IsLand(IslandMap.HexOf(site)), Is.EqualTo(!water), site.Id);
            }
        }

        [Test]
        public void TappingAHexCentreFindsThatHex()
        {
            foreach (var site in PrototypeIsland.Sites)
            {
                var hex = IslandMap.HexOf(site);
                Assert.That(HexLayout.FromWorld(hex.Center()), Is.EqualTo(hex), site.Id);
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
                var selected = IslandMap.HexOf(PrototypeIsland.Sites.First());
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
