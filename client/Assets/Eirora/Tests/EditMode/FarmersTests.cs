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
    /// Step 1, Farmers, in the client (TASK-061): introduction 6a once the step is unlocked, at most one a game day; the
    /// farmstead's sheet with the farmers and what they lack; the report's lines and the new buildings' and goods' names.
    /// </summary>
    public sealed class FarmersTests
    {
        private static readonly RestSchedule s_night = new(23 * 60, 8 * 60);
        private static readonly Hex s_farmstead = new(-1, 0); // Land beside the start village's storehouse.
        private string _folder = string.Empty;

        [SetUp]
        public void CreateFolder()
        {
            _folder = Path.Combine(Path.GetTempPath(), "eirora-introductions-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_folder);
        }

        [TearDown]
        public void DeleteFolder() => Directory.Delete(_folder, recursive: true);

        // Acceptance: once step 1 is unlocked the introduction is shown, and at most one a game day.
        [Test]
        public void TheFarmersIntroductionIsShownOnceTheStepIsUnlockedAndOnlyOnce()
        {
            string path = Path.Combine(_folder, Introductions.File);
            var root = new VisualElement();
            var hud = new Hud(root, _ => throw new InvalidOperationException("Nothing is played here."), introductions: new Introductions(path));

            hud.Show(Village(step: 0), Array.Empty<GameEvent>());
            Assert.That(root.Q(className: "introduction"), Is.Null, "The introduction came before the step.");

            var unlocked = Village(step: 1);
            hud.Show(unlocked, Array.Empty<GameEvent>());
            var card = root.Q(className: "introduction");
            Assert.That(card, Is.Not.Null);
            Assert.That(card!.Query<Label>().ToList().Select(l => l.text), Has.Member("Bönder").And.Some.StartsWith("Byn har vuxit. Med kläder kan torpare bli bönder."));

            Press(card.Q<Button>());
            hud.Show(unlocked, Array.Empty<GameEvent>());
            Assert.That(root.Q(className: "introduction"), Is.Null, "The introduction came twice.");

            // Seen is kept on the device, so a new start does not show it again.
            var again = new VisualElement();
            new Hud(again, _ => throw new InvalidOperationException("Nothing is played here."), introductions: new Introductions(path)).Show(unlocked, Array.Empty<GameEvent>());
            Assert.That(again.Q(className: "introduction"), Is.Null, "The introduction came again after a restart.");
        }

        // The introduction stays on the island while the world goes on and the HUD is drawn again, until the player goes on
        // from it; the card is not lost under it, so its button still closes it and the next one can come (PRG-02).
        [Test]
        public void TheIntroductionStaysUntilThePlayerGoesOnFromIt()
        {
            var root = new VisualElement();
            var hud = new Hud(root, _ => throw new InvalidOperationException("Nothing is played here."), introductions: new Introductions(null));
            var unlocked = Village(step: 1);
            hud.Show(unlocked, Array.Empty<GameEvent>());

            hud.Show(unlocked, Array.Empty<GameEvent>());

            var card = root.Q(className: "introduction");
            Assert.That(card, Is.Not.Null, "The introduction was cleared away when the HUD was drawn again.");
            Assert.That(hud.ShowsIntroduction, Is.True);
            Press(card!.Q<Button>());
            Assert.That(root.Q(className: "introduction"), Is.Null);
            Assert.That(hud.ShowsIntroduction, Is.False);
        }

        // At most one introduction a game day: one shown today keeps the next waiting until tomorrow (PRG-02).
        [Test]
        public void AtMostOneIntroductionIsDueAGameDay()
        {
            var introductions = new Introductions(null);
            var unlocked = Village(step: 1);
            Assert.That(introductions.Due(unlocked), Is.EqualTo(Introduction.Farmers));

            introductions.Shown(Introduction.Farmers, unlocked);

            Assert.That(introductions.Due(unlocked), Is.Null);
        }

        // The farmstead's sheet says how many farmers live there, and what they lack (TASK-043).
        [Test]
        public void TheFarmsteadsSheetSaysWhatTheFarmersLack()
        {
            var world = Village(step: 1, farmers: 4, wadmal: 1, farmstead: true);
            var root = new VisualElement();
            var hud = new Hud(root, _ => throw new InvalidOperationException("Nothing is played here."));
            hud.Show(world, Array.Empty<GameEvent>());
            Close(root);

            hud.Select(s_farmstead);
            hud.ShowMore();

            var texts = root.Query<Label>(className: "sheet-text").ToList().Select(l => l.text).ToArray();
            Assert.That(texts, Does.Contain("Hemön har 4 bönder i bostäder för 6."));
            Assert.That(texts, Does.Contain("Bönderna saknar kläder (tyg eller skinn). De förblir bönder, men inga fler blir bönder förrän det finns."));
        }

        // Without clothes for the new ones the farmstead's sheet says what would let crofters rise.
        [Test]
        public void TheFarmsteadsSheetSaysWhatWouldLetCroftersRise()
        {
            var world = Village(step: 1, farmstead: true);
            var root = new VisualElement();
            var hud = new Hud(root, _ => throw new InvalidOperationException("Nothing is played here."));
            hud.Show(world, Array.Empty<GameEvent>());
            Close(root);

            hud.Select(s_farmstead);
            hud.ShowMore();

            var texts = root.Query<Label>(className: "sheet-text").ToList().Select(l => l.text).ToArray();
            Assert.That(texts, Does.Contain("Hemön har inga bönder än, bostäder för sex."));
            Assert.That(texts, Does.Contain("Med kläder (tyg eller skinn) i lagret kan torpare bli bönder i gryningen."));
        }

        [Test]
        public void TheReportSaysWhoRoseAndWhatTheStepUnlocked()
        {
            var world = Village(step: 1);

            var lines = Texts.Report(world, new GameEvent[] { new VillagersRose(1, 2, PopulationTier.Farmers, world.HomeIsland), new StepUnlocked(1, 1) }).ToArray();

            Assert.That(lines, Is.EqualTo(new[] { "Två torpare blev bönder.", "Byn har vuxit. Bondgård, jaktstuga och garveri kan byggas." }));
        }

        // Every building and good has a name of its own, never another's.
        [Test]
        public void EveryBuildingAndGoodHasItsOwnName()
        {
            var buildings = Enum.GetValues(typeof(BuildingKind)).Cast<BuildingKind>().ToArray();
            var goods = Enum.GetValues(typeof(Resource)).Cast<Resource>().ToArray();

            Assert.That(buildings.Select(Texts.Name), Is.Unique);
            Assert.That(buildings.Select(Texts.Definite), Is.Unique);
            Assert.That(goods.Select(g => Texts.Name(g)), Is.Unique);
            Assert.That(Texts.Name(BuildingKind.HuntingLodge), Is.EqualTo("Jaktstuga"));
            Assert.That(Texts.Name(Resource.Hide), Is.EqualTo("skinn"));
        }

        // Game and hide join the bar with step 1, not before (economy-v2.md, section 5).
        [Test]
        public void GameAndHideJoinTheBarWithStepOne()
        {
            string[] Bar(World world)
            {
                // The goods the village has met are the store's (ui-prototypes.html, direction C).
                var root = new VisualElement();
                var hud = new Hud(root, _ => throw new InvalidOperationException("Nothing is played here."));
                hud.Show(world, Array.Empty<GameEvent>());
                hud.OpenStore(world.HomeIsland);
                return root.Query<Label>(className: "good-name").ToList().Select(l => l.text).ToArray();
            }

            Assert.That(Bar(Village(step: 0)), Has.None.EqualTo("Vilt").And.None.EqualTo("Skinn"));
            Assert.That(Bar(Village(step: 1)), Has.Member("Vilt").And.Member("Skinn"));
        }

        /// <summary>
        /// The start village in the morning at a step, with farmers among its twelve villagers, wadmal in the store and a
        /// farmstead beside the storehouse; read back from a snapshot, as a save is.
        /// </summary>
        private static World Village(int step, int farmers = 0, int wadmal = 0, bool farmstead = false)
        {
            var world = World.Create(PrototypeHome.Map, ScriptedWeek.At("2026-10-14 14:00"), s_night);
            world.AdvanceTo(ScriptedWeek.At("2026-10-15 09:00"));
            return EditedWorld.Edit(world, lines =>
            {
                int economy = lines.FindIndex(l => l.StartsWith("economy ", StringComparison.Ordinal));
                lines[economy] = $"{lines[economy].Substring(0, lines[economy].LastIndexOf(' '))} {step}";
                int settlement = lines.FindIndex(l => l.StartsWith("settlement home ", StringComparison.Ordinal));
                lines[settlement] = $"settlement home {12 - farmers} {farmers} 0";
                int stock = lines.FindIndex(l => l.StartsWith("stock home Wadmal ", StringComparison.Ordinal));
                lines[stock] = $"stock home Wadmal {wadmal * Quantity.PerUnit}";
                if (farmstead)
                {
                    lines.Insert(lines.FindLastIndex(l => l.StartsWith("building ", StringComparison.Ordinal)) + 1, $"building Farmstead {s_farmstead.Q} {s_farmstead.R} 1 0 0 0 0 0 0 0 0 0 0 0 0");
                }
            });
        }

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
