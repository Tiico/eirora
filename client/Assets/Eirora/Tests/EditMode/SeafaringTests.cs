using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Eirora.Client.App;
using Eirora.Client.UI;
using Eirora.Simulation;
using Eirora.Simulation.Economy;
using Eirora.Simulation.Events;
using Eirora.Simulation.Map;
using Eirora.Simulation.Time;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace Eirora.Client.Tests
{
    /// <summary>
    /// Step 3, Seafaring, in the client (TASK-063): introduction 9a after 8a, at most one a game day; the shipyard's sheet
    /// with its knarrs, the next one's hours and what it waits for; the jetty's sea route with its knarrs; and the
    /// report's lines and the new buildings' and goods' names.
    /// </summary>
    public sealed class SeafaringTests
    {
        private static readonly RestSchedule s_night = new(23 * 60, 8 * 60);
        private static readonly Hex[] s_start = { new(0, 0), new(1, 0), new(0, 1), new(1, -1) };
        private string _folder = string.Empty;

        [SetUp]
        public void CreateFolder()
        {
            _folder = Path.Combine(Path.GetTempPath(), "eirora-introductions-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_folder);
        }

        [TearDown]
        public void DeleteFolder() => Directory.Delete(_folder, recursive: true);

        // Acceptance: once step 3 is unlocked introduction 9a is shown, after 6a and 8a and never the same game day.
        [Test]
        public void TheSeafaringIntroductionComesTheDayAfterTheCraftsmen()
        {
            var root = new VisualElement();
            var hud = new Hud(root, _ => throw new InvalidOperationException("Nothing is played here."), introductions: new Introductions(Path.Combine(_folder, Introductions.File)));

            foreach (var (morning, title) in new[] { ("2026-10-15 09:00", "Bönder"), ("2026-10-16 09:00", "Hantverkare") })
            {
                hud.Show(Village(morning: morning), Array.Empty<GameEvent>());
                Assert.That(Card(root), Has.Member(title));
                Close(root);
            }

            hud.Show(Village(morning: "2026-10-16 09:00"), Array.Empty<GameEvent>());
            Assert.That(root.Q(className: "introduction"), Is.Null, "Two introductions came the same game day.");

            hud.Show(Village(morning: "2026-10-17 09:00"), Array.Empty<GameEvent>());
            Assert.That(Card(root), Has.Member("Sjöfart").And.Some.StartsWith("Med knarrar bär sjövägen mer."));
        }

        // The shipyard says what it does, how many knarrs the island has and when the next is done, and that it can pause.
        [Test]
        public void TheShipyardSaysItsKnarrsAndWhenTheNextIsDone()
        {
            var tile = Harbour(PrototypeHome.Home);
            var world = Village(shipyard: true, knarrs: 1, knarrWork: 20_000);
            var texts = Sheet(world, tile, out var actions);

            Assert.That(texts, Does.Contain("Bygger knarrar av 10 plankor och 5 tjära, en i taget på 32 timmar. Varje knarr bär 10 varor mer per dag på sjövägarna till och från ön, högst fyra på varje sjöväg."));
            Assert.That(texts, Does.Contain("Hemön har en knarr."));
            Assert.That(texts, Does.Contain("Nästa knarr är klar om 20 timmar med vakna bybor."));
            Assert.That(texts, Does.Contain("Knarrarna bär först när en sjöväg går hit."));
            Assert.That(actions, Has.Member("Pausa"));
        }

        // Without a knarr's materials the shipyard says what the store lacks; with four knarrs that the route has room for no more.
        [Test]
        public void TheShipyardSaysWhatTheNextKnarrWaitsFor()
        {
            var tile = Harbour(PrototypeHome.Home);

            Assert.That(Sheet(Village(shipyard: true), tile, out _), Does.Contain("Nästa knarr väntar på 5 tjära till i lagret."));
            Assert.That(Sheet(Village(shipyard: true, knarrs: 4), tile, out _), Does.Contain("Sjövägen rymmer inte fler knarrar härifrån."));
        }

        // The shipyard leaves its minimum stock of planks and tar, as processing does, and its sheet says so and sets it
        // (produktägaren 2026-10-25, PR #185).
        [Test]
        public void TheShipyardSaysItLeavesItsMinimumStock()
        {
            var tile = Harbour(PrototypeHome.Home);
            var root = new VisualElement();
            var hud = new Hud(root, _ => throw new InvalidOperationException("Nothing is played here."));
            hud.Show(Village(shipyard: true, reserve: 20), Array.Empty<GameEvent>());
            Close(root);

            hud.Select(tile);

            var texts = root.Query<Label>(className: "sheet-text").ToList().Select(l => l.text).ToArray();
            Assert.That(texts, Does.Contain("Nästa knarr väntar på 10 plankor och 25 tjära till i lagret, utöver minimilagret."));
            Assert.That(root.Query<Label>(className: "row-title").ToList().Select(l => l.text), Has.Member("Minimilager"));
            Assert.That(root.Query<Label>(className: "stepper-value").ToList().Select(l => l.text), Has.Member("20 av varje"));
        }

        // The jetty at home tells the route's capacity with the knarrs that carry on it (economy-v2.md, section 4).
        [Test]
        public void TheJettyTellsTheSeaRoutesCapacityWithItsKnarrs()
        {
            var world = Village(seaRoute: true, knarrs: 2);
            Assert.That(world.SeaRoutes.Single().PerGameDay, Is.EqualTo(60));

            var texts = Sheet(world, Harbour(PrototypeHome.Home), out _);

            Assert.That(texts, Does.Contain("Sjövägen till Nyön är öppen. Skeppen bär 60 varor per dag med två knarrar, åt båda hållen tillsammans."));
        }

        [Test]
        public void TheReportSaysWhatTheStepUnlockedAndWhenAKnarrIsLaunched()
        {
            var world = Village();

            var lines = Eirora.Client.UI.Texts.Report(world, new GameEvent[]
            {
                new StepUnlocked(1, 3),
                new KnarrLaunched(1, world.HomeIsland, 1),
                new KnarrLaunched(1, world.HomeIsland, 2),
            }).ToArray();

            Assert.That(lines, Is.EqualTo(new[]
            {
                "Byn har blivit en hamn. Tjärdal och varv kan byggas.",
                "Varvet på Hemön har byggt sin första knarr. Alla sjövägar bär mer.",
                "Varvet på Hemön har byggt en knarr till. Alla sjövägar bär mer.",
            }));
        }

        [Test]
        public void TheNewBuildingsAndGoodsHaveTheirNames()
        {
            Assert.That(new[] { BuildingKind.TarKiln, BuildingKind.Shipyard }.Select(Eirora.Client.UI.Texts.Name), Is.EqualTo(new[] { "Tjärdal", "Varv" }));
            Assert.That(Eirora.Client.UI.Texts.Name(Resource.Tar), Is.EqualTo("tjära"));
            Assert.That(
                Eirora.Client.UI.Texts.Effect(BuildingKind.Shipyard, ContentCatalog.Current.For(BuildingKind.Shipyard)),
                Is.EqualTo("Knarrar: +10 per dag på sjövägarna, högst fyra"));
        }

        /// <summary>
        /// The start village in the morning at step 3, with two craftsmen among its twelve villagers and, as asked, a staffed
        /// shipyard at home with its minimum stock and the knarr it builds, a sea route to Nyön and knarrs at home; read back from a snapshot, as
        /// a save is.
        /// </summary>
        private static World Village(string morning = "2026-10-15 09:00", bool shipyard = false, bool seaRoute = false, int knarrs = 0, int knarrWork = 0, int reserve = 0)
        {
            var world = World.Create(PrototypeHome.Map, ScriptedWeek.At("2026-10-14 14:00"), s_night);
            world.AdvanceTo(ScriptedWeek.At(morning));
            return EditedWorld.Edit(world, lines =>
            {
                int economy = lines.FindIndex(l => l.StartsWith("economy ", StringComparison.Ordinal));
                lines[economy] = $"{lines[economy].Substring(0, lines[economy].LastIndexOf(' '))} 3";
                int settlement = lines.FindIndex(l => l.StartsWith("settlement home ", StringComparison.Ordinal));
                var counts = lines[settlement].Split(' ');
                lines[settlement] = $"settlement home {int.Parse(counts[2]) + int.Parse(counts[3]) + int.Parse(counts[4]) - 2} 0 2";
                if (seaRoute)
                {
                    lines.Insert(settlement + 1, $"settlement {PrototypeHome.Nyon} 1 0 0");
                    lines.InsertRange(
                        lines.FindLastIndex(l => l.StartsWith("stock ", StringComparison.Ordinal)) + 1,
                        Enum.GetValues(typeof(Resource)).Cast<Resource>().Select(r => $"stock {PrototypeHome.Nyon} {r} {(r == Resource.Fish ? 4 * Quantity.PerUnit : 0)}"));
                }

                if (knarrs > 0)
                {
                    lines.Insert(lines.FindIndex(l => l.StartsWith("building ", StringComparison.Ordinal)), $"knarrs home {knarrs}");
                }

                var last = lines.FindLastIndex(l => l.StartsWith("building ", StringComparison.Ordinal)) + 1;
                if (shipyard)
                {
                    var tile = Harbour(PrototypeHome.Home);
                    lines.Insert(last++, $"building Shipyard {tile.Q} {tile.R} 1 1 0 0 0 0 0 0 0 0 0 0 {reserve}");
                    if (knarrWork > 0)
                    {
                        lines.Insert(last++, $"knarr {knarrWork}");
                    }
                }

                if (seaRoute)
                {
                    var home = Harbour(PrototypeHome.Home);
                    var nyon = Harbour(PrototypeHome.Nyon);
                    lines.Insert(last++, $"building Jetty {home.Q} {home.R} 1 0 0 0 0 0 4 0 0 0 0 0 0");
                    lines.Insert(last, $"building Jetty {nyon.Q} {nyon.R} 1 1 0 0 0 1 4 0 0 0 0 0 0");
                }
            });
        }

        /// <summary>A free harbour spot on an island of the prototype's map, past the start's buildings.</summary>
        private static Hex Harbour(string island)
        {
            var map = PrototypeHome.Map;
            return map.Features.LandOf(island).First(h => map.Features.IsHarborSpot(h) && !s_start.Contains(h));
        }

        /// <summary>The texts of a building's sheet, and the titles of its actions.</summary>
        private static string[] Sheet(World world, Hex tile, out string[] actions)
        {
            var root = new VisualElement();
            var hud = new Hud(root, _ => throw new InvalidOperationException("Nothing is played here."));
            hud.Show(world, Array.Empty<GameEvent>());
            Close(root);
            hud.Select(tile);
            actions = root.Query<Button>(className: "action").ToList().Select(a => a.Q<Label>(className: "action-title")?.text ?? string.Empty).ToArray();
            return root.Query<Label>(className: "sheet-text").ToList().Select(l => l.text).ToArray();
        }

        private static string[] Card(VisualElement root) =>
            root.Q(className: "introduction") is { } card ? card.Query<Label>().ToList().Select(l => l.text).ToArray() : Array.Empty<string>();

        /// <summary>Closes an introduction card, so that the sheet underneath is what the test reads.</summary>
        private static void Close(VisualElement root)
        {
            if (root.Q(className: "introduction") is { } card)
            {
                Press(card.Q<Button>());
            }
        }

        private static void Press(Button button)
        {
            var invoke = typeof(Clickable).GetMethod("Invoke", BindingFlags.Instance | BindingFlags.NonPublic, null, new[] { typeof(EventBase) }, null);
            Assert.That(invoke, Is.Not.Null, "Clickable.Invoke is gone; find another way to press a button.");
            using var tap = NavigationSubmitEvent.GetPooled();
            invoke!.Invoke(button.clickable, new object[] { tap });
        }
    }
}
