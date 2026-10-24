using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Eirora.Client.Look;
using Eirora.Client.UI;
using Eirora.Simulation;
using Eirora.Simulation.Map;
using Eirora.Simulation.Time;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace Eirora.Client.Tests
{
    /// <summary>
    /// The bottleneck diagnosis in the client (TASK-043): a building's sheet says why it stands still or what holds it
    /// back, and the island marks the buildings that stand still with a sign of its own shape (UX-05).
    /// </summary>
    public sealed class DiagnosisTests
    {
        private static readonly RestSchedule s_night = new(23 * 60, 8 * 60);
        private static readonly Hex s_beside = new(-1, 0); // Land beside the start village's storehouse.

        [Test]
        public void ASawmillWithoutTimberAboveTheReserveSaysItWaitsForTimberAndHowMuchIsMissing()
        {
            var world = Edited(Morning(), "stock home Timber 18000000", "building Sawmill -1 0 1 1 0 0 0 0 0 0 0 1 0 0");

            var sheet = SheetOf(world, s_beside);

            Assert.That(sheet.Subtitle, Is.EqualTo("Äng · står still"));
            Assert.That(sheet.Texts, Is.EqualTo(new[] { "Väntar på timmer: 4 till behövs för en timmes arbete. Minimilagret på 20 sparas till byggen." }));
            Assert.That(sheet.Marked, Is.True);
        }

        [Test]
        public void AFarFishingHutWhoseCarriersCannotKeepUpSaysHowMuchReachesTheStoreAndWhatARoadWouldGive()
        {
            var (world, far) = BehindTheCarriers();

            var sheet = SheetOf(world, far[0]);

            // It fishes at full pace; what does not reach the store waits at the hut (D-110).
            Assert.That(sheet.Subtitle, Does.Contain("· går långsammare · "));
            Assert.That(sheet.Texts.Single(), Does.Match("^Bärarna hinner inte: [0-9]+ % når fram\\. Fyra steg till förrådet\\. En väg ger [0-9,]+ fisk/h, en bärare till [0-9,]+ och ett förråd intill [0-9,]+\\.$"));
            Assert.That(sheet.Actions, Has.Some.Match("^Väg till förrådet \\| [0-9]+ sten och [0-9]+ h arbete · [0-9,]+ fisk/h i stället för [0-9,]+$"));
            Assert.That(sheet.Marked, Is.False);
        }

        [Test]
        public void TheStorehousesSheetShowsItsCarriersTheCapAndWhatWouldHelpThem()
        {
            var (world, _) = BehindTheCarriers();

            var sheet = SheetOf(world, Hex.Origin);

            Assert.That(sheet.Texts[0], Is.EqualTo("Rymmer 150 av varje vara. Bärarna hämtar det byggnaderna närmast gör."));
            Assert.That(sheet.Texts[1], Does.Match("^En bärare\\. Byggnaderna behöver [0-9,]+ bärartimmar i timmen, så [0-9]+ % når fram och resten väntar vid byggnaderna\\.$"));
            Assert.That(sheet.Texts[2], Does.Match("^Förrådet tar en bärare till vid gryningen, som ger \\+[0-9,]+ fisk/h(, \\+[0-9,]+ [a-zä]+/h)* och \\+[0-9,]+ [a-zä]+/h\\.$"));
            Assert.That(sheet.Actions, Has.Some.Match("^Väg till fiskebod \\| [0-9]+ sten och [0-9]+ h arbete · \\+[0-9,]+ fisk/h"));
            Assert.That(sheet.Actions, Has.Some.Match("^Förråd intill fiskebod \\| 10 timmer · 5 sten · 16 h arbete · \\+[0-9,]+ fisk/h"));
            Assert.That(sheet.Actions, Has.Some.EqualTo("Dagsverke | 30 Gryningskraft · bärarna bär hälften mer i dag"));
            Assert.That(sheet.Stepper, Is.EqualTo("4 av 4"));
        }

        // Review of #166: a higher cap is taken up at dawn, and only by a free villager, so the advice says when it gives.
        [TestCase(3, "vid gryningen")]
        [TestCase(0, "när en bybo blir ledig")]
        public void AtItsCapTheStorehouseSaysWhenOneMoreCarrierWouldGiveMore(int free, string when)
        {
            var (world, _) = BehindTheCarriers(free);
            Assert.That(world.Apply("cap-1", new Simulation.Commands.SetCarrierCap(Hex.Origin, 1)).Accepted, Is.True);

            var sheet = SheetOf(world, Hex.Origin);

            Assert.That(sheet.Actions, Has.Some.Match($"^En bärare till \\| Höj taket · ingen kraft · \\+[0-9,]+ fisk/h.* {when}$"));
        }

        [Test]
        public void TheCapOnTheCarriersIsSetOnTheStorehousesSheet()
        {
            var (world, _) = BehindTheCarriers();
            var root = new VisualElement();
            var played = new List<Simulation.Commands.GameCommand>();
            var hud = new Hud(root, command => { played.Add(command); return world.Apply($"cap-{played.Count}", command); });
            hud.Show(world, Array.Empty<Simulation.Events.GameEvent>());
            hud.Select(Hex.Origin);

            Press(root.Query<Button>(className: "stepper-button").ToList().First());

            Assert.That(played.Single(), Is.TypeOf<Simulation.Commands.SetCarrierCap>());
            Assert.That(world.BuildingAt(Hex.Origin)!.CarrierCap, Is.EqualTo(3));
        }

        [Test]
        public void ABuildingWithoutStaffIsMarkedOnTheIslandAndItsSheetSaysHowManyVillagersAreMissing()
        {
            // Eight villagers: seven run the start buildings, so one is free for a sawmill that takes two.
            var world = Edited(Morning(), "settlement home 8", "building Sawmill -1 0 1 0 0 0 0 0 0 0 0 1 0 0");

            var sheet = SheetOf(world, s_beside);

            Assert.That(sheet.Texts, Is.EqualTo(new[] { "Det saknas en bybo här. Flytta upp den i bemanningen eller pausa en byggnad före den." }));
            Assert.That(sheet.Marked, Is.True);
            Assert.That(sheet.Icon, Is.True, "The sheet shows the island's sign beside the status.");
        }

        [Test]
        public void ABuildingThatProducesFullySaysSoAndIsNotMarked()
        {
            var sheet = SheetOf(Morning(), new Hex(1, 0));

            Assert.That(sheet.Subtitle, Does.Contain("· går för fullt · 1,25 fisk/h"));
            Assert.That(sheet.Texts, Is.Empty);
            Assert.That(sheet.Marked, Is.False);
            Assert.That(sheet.Icon, Is.False);
        }

        [Test]
        public void AHouseSaysNothingOfVillagersItNeverNeeds()
        {
            var world = Edited(Morning(), null, "building Longhouse -1 0 1 0 0 0 0 0 0 0 0 0 0 0");

            var sheet = SheetOf(world, s_beside);

            Assert.That(sheet.Subtitle, Is.EqualTo("Äng"));
            Assert.That(sheet.Marked, Is.False);
        }

        [Test]
        public void AStorehouseWithoutStaffStillStoresAndIsNotMarked()
        {
            var world = Edited(Morning(), "settlement home 7", "building Storehouse -1 0 1 0 0 0 0 0 4 0 0 0 0 0");

            var sheet = SheetOf(world, s_beside);

            Assert.That(sheet.Subtitle, Is.EqualTo("Äng · väntar på bybor"));
            Assert.That(sheet.Texts, Is.EqualTo(new[] { "Rymmer 150 av varje vara. Bärarna hämtar det byggnaderna närmast gör.", "Ingen bybo bär här ännu. Förrådet tar en bärare när en blir ledig." }));
            Assert.That(sheet.Marked, Is.False);
            Assert.That(sheet.Icon, Is.False);
        }

        [Test]
        public void APausedBuildingIsMarkedAndItsSheetSaysItIsPaused()
        {
            var world = Edited(Morning(), null, "building Sawmill -1 0 1 0 0 0 1 0 0 0 0 1 0 0");

            var sheet = SheetOf(world, s_beside);

            Assert.That(sheet.Subtitle, Is.EqualTo("Äng · pausad"));
            Assert.That(sheet.Texts, Is.EqualTo(new[] { "Pausad: byborna bygger i stället. Nivån och lagret står kvar." }));
            Assert.That(sheet.Marked, Is.True);
            Assert.That(sheet.Icon, Is.True);
        }

        [Test]
        public void WhileTheVillageRestsTheSheetSaysSoAndMarksNothingAsTheIslandDoes()
        {
            var world = Edited(Morning(), "settlement home 8", "building Sawmill -1 0 1 0 0 0 0 0 0 0 0 1 0 0");
            world.AdvanceTo(At("2026-10-16 01:00"));

            var sheet = SheetOf(world, s_beside);

            Assert.That(sheet.Subtitle, Is.EqualTo("Äng · vilar"));
            Assert.That(sheet.Texts, Is.Empty);
            Assert.That(sheet.Icon, Is.False);
            Assert.That(sheet.Marked, Is.False);
        }

        [Test]
        public void NoBuildingIsMarkedWhileTheVillageRests()
        {
            var world = Edited(Morning(), "settlement home 8", "building Sawmill -1 0 1 0 0 0 0 0 0 0 0 1 0 0");
            world.AdvanceTo(At("2026-10-16 01:00"));

            WithIsland(world, island => Assert.That(island.MarkedStandingStill, Is.Empty));
        }

        [Test]
        public void TheReportPointsToTheIslandsBottleneckAndWhereAWorkdayEasesIt()
        {
            var world = Edited(Morning(), "stock home Timber 18000000", "building Sawmill -1 0 1 1 0 0 0 0 0 0 0 1 0 0");
            var root = new VisualElement();
            var hud = new Hud(root, _ => throw new InvalidOperationException("Nothing is played here."));
            hud.Show(world, Array.Empty<Simulation.Events.GameEvent>());

            hud.OpenReport();
            var bottleneck = root.Query<Button>(className: "action").ToList().Single();

            Assert.That(Label(bottleneck, "action-title"), Is.EqualTo("Flaskhals just nu"));
            Assert.That(Label(bottleneck, "action-cost"), Is.EqualTo("Sågverket väntar på timmer. Ett dagsverke på huggarkojan ger mer timmer."));

            Hex? selected = null;
            hud.SelectionChanged += tile => selected = tile;
            Press(bottleneck);
            Assert.That(selected, Is.EqualTo(s_beside), "The bottleneck leads to the building.");
            Assert.That(root.Q<Label>(className: "sheet-title").text, Is.EqualTo("Sågverk"));
        }

        [Test]
        public void ABottleneckOfAFullStoreSaysWhetherTheBuildingStandsStillOrGoesSlower()
        {
            // The fishing hut's quarter hour of fish fits only in part, so it goes slower rather than standing still.
            var world = Edited(Morning(), "stock home Fish 149800000");
            var root = new VisualElement();
            var hud = new Hud(root, _ => throw new InvalidOperationException("Nothing is played here."));
            hud.Show(world, Array.Empty<Simulation.Events.GameEvent>());

            hud.OpenReport();

            Assert.That(Label(root.Query<Button>(className: "action").ToList().Single(), "action-cost"), Is.EqualTo("Lagret är fullt av fisk, så fiskeboden går långsammare."));
        }

        [Test]
        public void AReportWithNothingHeldBackPointsToNoBottleneck()
        {
            var root = new VisualElement();
            var hud = new Hud(root, _ => throw new InvalidOperationException("Nothing is played here."));
            hud.Show(Morning(), Array.Empty<Simulation.Events.GameEvent>());

            hud.OpenReport();

            Assert.That(root.Query<Button>(className: "action").ToList(), Is.Empty);
        }

        /// <summary>
        /// The start village with three fishing huts four steps out, more than its one carrier can keep up with, and sixteen
        /// villagers, three of them free.
        /// </summary>
        private static (World World, Hex[] Far) BehindTheCarriers(int free = 3)
        {
            var world = Morning();
            var far = Enumerable.Range(0, world.Map.Disc.Count).Select(i => world.Map.Disc[i])
                .Where(h => (world.Map.Features.UsesOf(h) & TileUse.FishingWater) != 0 && world.BuildingAt(h) is null && world.TripFrom(h).Steps == 4)
                .Take(3).ToArray();
            world = Edited(world, $"settlement home {13 + free}", string.Join("\n", far.Select(h => $"building FishingHut {h.Q} {h.R} 1 1 0 0 0 0 0 0 0 1 0 0")));
            Assert.That(world.FreeVillagers, Is.EqualTo(free));
            return (world, far);
        }

        private static (string Subtitle, string[] Texts, string[] Actions, bool Icon, bool Marked, string? Stepper) SheetOf(World world, Hex tile)
        {
            var root = new VisualElement();
            var hud = new Hud(root, _ => throw new InvalidOperationException("Nothing is played here."));
            hud.Show(world, Array.Empty<Simulation.Events.GameEvent>());
            hud.Select(tile);
            bool marked = false;
            WithIsland(world, island => marked = island.MarkedStandingStill.Contains(tile));
            return (
                root.Q<Label>(className: "sheet-subtitle").text,
                root.Query<Label>(className: "sheet-text").ToList().Select(l => l.text).ToArray(),
                root.Query<Button>(className: "action").ToList().Select(a => $"{Label(a, "action-title")} | {Label(a, "action-cost")}").ToArray(),
                root.Q(className: "standing-still") is not null,
                marked,
                root.Q<Label>(className: "stepper-value")?.text);
        }

        private static void WithIsland(World world, Action<IslandView> look)
        {
            var parent = new GameObject("Island");
            var flat = new Material(Shader.Find("Eirora/Flat"));
            var glow = new Material(Shader.Find("Eirora/Glow"));
            var island = new IslandView(parent.transform, flat, glow, Looks.Load());
            try
            {
                island.Refresh(world);
                look(island);
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

        /// <summary>
        /// The world with a line of its state replaced by one that begins the same way, such as a stock or the villagers,
        /// and buildings added at the end of the line; read back from a snapshot, as a save is. A snapshot restores only
        /// the state it holds, so a building that makes something is written with its depot, the storehouse on 0 0.
        /// </summary>
        private static World Edited(World world, string? replace, string? buildings = null)
        {
            string text = Encoding.UTF8.GetString(SimulationSnapshot.Take(world).ToBytes());
            int stateStart = text.IndexOf('\n', text.IndexOf("state-sha256 ", StringComparison.Ordinal)) + 1;
            var lines = text.Substring(stateStart).TrimEnd('\n').Split('\n').ToList();
            if (replace is not null)
            {
                string[] words = replace.Split(' ');
                string key = string.Join(" ", words.Take(words[0] switch { "stock" => 3, "settlement" => 2, _ => 1 })) + " ";
                int at = lines.FindIndex(l => l.StartsWith(key, StringComparison.Ordinal));
                var kept = lines[at].Split(' ').Skip(words.Length);
                lines[at] = string.Join(" ", words.Concat(kept));
            }

            if (buildings is not null)
            {
                lines.Insert(lines.FindLastIndex(l => l.StartsWith("building ", StringComparison.Ordinal)) + 1, buildings);
            }

            string state = string.Join("\n", lines) + "\n";
            var header = text.Substring(0, stateStart).TrimEnd('\n').Split('\n').ToList();
            header[header.Count - 1] = "state-sha256 " + Sha256(state);
            var bytes = Encoding.UTF8.GetBytes(string.Join("\n", header) + "\n" + state);
            return SimulationSnapshot.FromBytes(bytes).Restore(world.Map);
        }

        private static string Sha256(string text)
        {
            using var sha = SHA256.Create();
            return string.Concat(sha.ComputeHash(Encoding.UTF8.GetBytes(text)).Select(b => b.ToString("x2")));
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
