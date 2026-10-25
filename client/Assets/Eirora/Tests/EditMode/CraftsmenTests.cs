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
    /// Step 2, Craftsmen, in the client (TASK-062): introduction 8a after 6a, at most one a game day; the craftsmen's house
    /// with the craftsmen and the drink they lack; the meadow named once an apiary can stand there; the queue's word on
    /// the craftsmen's pace; and the report's lines and the new buildings' and goods' names.
    /// </summary>
    public sealed class CraftsmenTests
    {
        private static readonly RestSchedule s_night = new(23 * 60, 8 * 60);
        private static readonly Hex s_meadow = new(-1, 0); // Grassland beside the start village's storehouse.
        private string _folder = string.Empty;

        [SetUp]
        public void CreateFolder()
        {
            _folder = Path.Combine(Path.GetTempPath(), "eirora-introductions-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_folder);
        }

        [TearDown]
        public void DeleteFolder() => Directory.Delete(_folder, recursive: true);

        // Acceptance: once step 2 is unlocked introduction 8a is shown, after 6a and never the same game day.
        [Test]
        public void TheCraftsmenIntroductionComesTheDayAfterTheFarmers()
        {
            var root = new VisualElement();
            var hud = new Hud(root, _ => throw new InvalidOperationException("Nothing is played here."), introductions: new Introductions(Path.Combine(_folder, Introductions.File)));

            var today = Village(step: 2);
            hud.Show(today, Array.Empty<GameEvent>());
            Assert.That(Card(root), Has.Member("Bönder"));
            Close(root);
            hud.Show(today, Array.Empty<GameEvent>());
            Assert.That(root.Q(className: "introduction"), Is.Null, "Two introductions came the same game day.");

            hud.Show(Village(step: 2, morning: "2026-10-16 09:00"), Array.Empty<GameEvent>());
            Assert.That(Card(root), Has.Member("Hantverkare").And.Some.StartsWith("Bönderna har det gott. Med dryck kan några bli hantverkare."));
        }

        // The craftsmen's house says how many craftsmen live there and that they lack drink (TASK-062).
        [Test]
        public void TheCraftsmensHouseSaysTheCraftsmenLackDrink()
        {
            var world = Village(step: 2, farmers: 4, craftsmen: 2, wadmal: 10, house: true);
            var root = new VisualElement();
            var hud = new Hud(root, _ => throw new InvalidOperationException("Nothing is played here."));
            hud.Show(world, Array.Empty<GameEvent>());
            Close(root);

            hud.Select(s_meadow);

            var texts = Texts(root);
            Assert.That(texts, Does.Contain("Ger plats åt åtta hantverkare. Bönder blir hantverkare här när de får det de behöver."));
            Assert.That(texts, Does.Contain("Hemön har 2 hantverkare i bostäder för 8."));
            Assert.That(texts, Does.Contain("Hantverkarna saknar dryck (mjöd eller öl). De förblir hantverkare, men inga fler blir hantverkare förrän det finns."));
        }

        // Without drink for the new ones the house says what would let farmers rise.
        [Test]
        public void TheCraftsmensHouseSaysWhatWouldLetFarmersRise()
        {
            var world = Village(step: 2, farmers: 4, wadmal: 10, house: true);
            var root = new VisualElement();
            var hud = new Hud(root, _ => throw new InvalidOperationException("Nothing is played here."));
            hud.Show(world, Array.Empty<GameEvent>());
            Close(root);

            hud.Select(s_meadow);

            Assert.That(Texts(root), Does.Contain("Med dryck (mjöd eller öl) i lagret kan bönder bli hantverkare i gryningen."));
        }

        // A meadow is named, and offers an apiary among what only its kind of tile allows, only once the world has reached step 2
        // (economy-v2.md, section 5).
        [Test]
        public void AMeadowIsNamedOnceAnApiaryCanStandThere()
        {
            string Subtitle(int step)
            {
                var root = new VisualElement();
                var hud = new Hud(root, _ => throw new InvalidOperationException("Nothing is played here."));
                hud.Show(Village(step: step), Array.Empty<GameEvent>());
                Close(root);
                hud.Select(s_meadow);
                return root.Query<Label>(className: "sheet-subtitle").ToList().Select(l => l.text).First();
            }

            Assert.That(Subtitle(1), Does.Not.Contain("äng"));
            Assert.That(Subtitle(2), Does.Contain("äng"));

            var meadow = new VisualElement();
            var sheet = new Hud(meadow, _ => throw new InvalidOperationException("Nothing is played here."));
            sheet.Show(Village(step: 2), Array.Empty<GameEvent>());
            Close(meadow);
            sheet.Select(s_meadow);
            var special = meadow.Query<Button>(className: "action").ToList()
                .Select(a => $"{a.Q<Label>(className: "action-title")?.text} | {a.Q<Label>(className: "action-effect")?.text}")
                .TakeWhile(a => !a.StartsWith("Fler", StringComparison.Ordinal));
            Assert.That(special, Has.Member("Bigård | 0,4 honung/h dygnet runt"));
        }

        // The queue says when free craftsmen build, and that they build twice as fast (D-116).
        [Test]
        public void TheQueueSaysTheCraftsmenBuildTwiceAsFast()
        {
            var world = Village(step: 2, craftsmen: 2, house: true, queued: true);
            var root = new VisualElement();
            var hud = new Hud(root, _ => throw new InvalidOperationException("Nothing is played here."));
            hud.Show(world, Array.Empty<GameEvent>());
            Close(root);

            hud.OpenQueue();

            Assert.That(Texts(root), Does.Contain("Två av dem är hantverkare och bygger dubbelt så fort."));
        }

        [Test]
        public void TheReportSaysWhoRoseAndWhatTheStepUnlocked()
        {
            var world = Village(step: 2);

            var lines = Eirora.Client.UI.Texts.Report(world, new GameEvent[]
            {
                new VillagersRose(1, 1, PopulationTier.Craftsmen, world.HomeIsland),
                new VillagersRose(1, 2, PopulationTier.Craftsmen, world.HomeIsland),
                new VillagersRose(1, 1, PopulationTier.Farmers, world.HomeIsland),
                new StepUnlocked(1, 2),
            }).ToArray();

            Assert.That(lines, Is.EqualTo(new[]
            {
                "En bonde blev hantverkare.",
                "Två bönder blev hantverkare.",
                "En torpare blev bonde.",
                "Bönderna har det gott. Hantverkargård, bigård, bryggeri och mjöderi kan byggas.",
            }));
        }

        [Test]
        public void TheNewBuildingsAndGoodsHaveTheirNames()
        {
            Assert.That(
                new[] { BuildingKind.CraftsmensHouse, BuildingKind.Apiary, BuildingKind.Brewery, BuildingKind.Meadery }.Select(Eirora.Client.UI.Texts.Name),
                Is.EqualTo(new[] { "Hantverkargård", "Bigård", "Bryggeri", "Mjöderi" }));
            Assert.That(new[] { Resource.Honey, Resource.Mead, Resource.Ale }.Select(r => Eirora.Client.UI.Texts.Name(r)), Is.EqualTo(new[] { "honung", "mjöd", "öl" }));
            Assert.That(Eirora.Client.UI.Texts.Name(PopulationTier.Craftsmen), Is.EqualTo("hantverkare"));
        }

        // Honey, mead and ale join the bar with step 2, not before (economy-v2.md, section 5).
        [Test]
        public void TheDrinkJoinsTheBarWithStepTwo()
        {
            string[] Bar(World world)
            {
                var root = new VisualElement();
                new Hud(root, _ => throw new InvalidOperationException("Nothing is played here.")).Show(world, Array.Empty<GameEvent>());
                return root.Query<Label>(className: "resource").ToList().Select(l => l.text.Split(' ')[0]).ToArray();
            }

            Assert.That(Bar(Village(step: 1)), Has.None.EqualTo("Honung").And.None.EqualTo("Mjöd").And.None.EqualTo("Öl"));
            Assert.That(Bar(Village(step: 2)), Has.Member("Honung").And.Member("Mjöd").And.Member("Öl"));
        }

        /// <summary>
        /// The start village in the morning at a step, with farmers and craftsmen among its twelve villagers, wadmal in the
        /// store, a craftsmen's house on the meadow beside the storehouse and a cottage in the queue; read back from a
        /// snapshot, as a save is.
        /// </summary>
        private static World Village(int step, int farmers = 0, int craftsmen = 0, int wadmal = 0, bool house = false, bool queued = false, string morning = "2026-10-15 09:00")
        {
            var world = World.Create(PrototypeHome.Map, ScriptedWeek.At("2026-10-14 14:00"), s_night);
            world.AdvanceTo(ScriptedWeek.At(morning));
            return EditedWorld.Edit(world, lines =>
            {
                int economy = lines.FindIndex(l => l.StartsWith("economy ", StringComparison.Ordinal));
                lines[economy] = $"{lines[economy].Substring(0, lines[economy].LastIndexOf(' '))} {step}";
                int settlement = lines.FindIndex(l => l.StartsWith("settlement home ", StringComparison.Ordinal));
                var counts = lines[settlement].Split(' ');
                lines[settlement] = $"settlement home {int.Parse(counts[2]) + int.Parse(counts[3]) + int.Parse(counts[4]) - farmers - craftsmen} {farmers} {craftsmen}";
                int stock = lines.FindIndex(l => l.StartsWith("stock home Wadmal ", StringComparison.Ordinal));
                lines[stock] = $"stock home Wadmal {wadmal * Quantity.PerUnit}";
                if (house)
                {
                    lines.Insert(lines.FindLastIndex(l => l.StartsWith("building ", StringComparison.Ordinal)) + 1, $"building CraftsmensHouse {s_meadow.Q} {s_meadow.R} 1 0 0 0 0 0 0 0 0 0 0 0 0");
                }

                if (queued)
                {
                    lines.RemoveAll(l => l.StartsWith("project ", StringComparison.Ordinal));
                    lines.Insert(lines.FindLastIndex(l => l.StartsWith("building ", StringComparison.Ordinal)) + 1, "project Build Cottage 2 -2 32000");
                }
            });
        }

        private static string[] Texts(VisualElement root) => root.Query<Label>(className: "sheet-text").ToList().Select(l => l.text).ToArray();

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
