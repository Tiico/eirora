using System.Linq;
using System.Reflection;
using Eirora.Client.App;
using Eirora.Client.UI;
using Eirora.Simulation.Commands;
using Eirora.Simulation.Economy;
using Eirora.Simulation.Forces;
using Eirora.Simulation.Map;
using Eirora.Simulation.Time;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace Eirora.Client.Tests
{
    /// <summary>
    /// Building by hand (D-65, TASK-065): the build sheet offers it beside the build with Build Power, as large, also when
    /// the Build Power does not reach, and the queue and the site tell that it takes no villagers' work and when it stands.
    /// </summary>
    public sealed class HandBuildTests
    {
        private static readonly Hex L1 = new(-1, 0);
        private static readonly Hex L2 = new(0, -1);
        private static readonly Hex L3 = new(-1, 1);
        private static readonly Hex H1 = new(1, 1);

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
        public void WithoutBuildPowerTheWeaveryCanStillBeBuiltByHand()
        {
            WithoutBuildPower();
            _hud.Select(L1);
            _hud.ShowMoreBuildings();

            Assert.That(Action("Väveri").ClassListContains("action-unavailable"), Is.True);
            var byHand = Action("Väveri med handkraft");
            Assert.That(byHand.ClassListContains("action-unavailable"), Is.False);
            Assert.That(byHand.Q<Label>(className: "action-cost").text, Is.EqualTo("10 plankor · 5 sten · 7 dygn"));
            Assert.That(Actions().Where(a => a.EndsWith(" med handkraft")), Is.EqualTo(new[] { "Väveri med handkraft" }), "Only what the catalog marks is offered by hand.");

            Press(byHand);

            var project = _game.World!.Projects.Single();
            Assert.That((project.Building!.Value, project.ByHand, project.DaysLeft), Is.EqualTo((BuildingKind.Weavery, true, 7)));
        }

        [Test]
        public void TheSiteOfABuildingByHandTellsTheDawnItStands()
        {
            WithoutBuildPower();
            _game.Apply(new BuildByHand(BuildingKind.Weavery, L1));

            _hud.Select(L1);

            Assert.That(Labels(), Does.Contain("Väveri byggs med handkraft"));
            Assert.That(Labels().Any(l => l.EndsWith(" · klar om 7 dygn")), Is.True);
            Assert.That(Labels(), Does.Contain("Byborna bygger på sin fritid, utan Styrka och utan de lediga byborna i byggkön. Den står klar i gryningen dygn 7."));
            Assert.That(Actions(), Is.Empty);
        }

        [Test]
        public void TheQueueShowsWhatIsBuiltByHandWithoutAPlaceOrARush()
        {
            WithoutBuildPower();
            _game.Apply(new BuildByHand(BuildingKind.Weavery, L1));
            _hud.OpenQueue();

            Assert.That(QueueItems(), Is.EqualTo(new[] { "Väveri | Med handkraft · klar om 7 dygn" }));
            Assert.That(_root.Query<Button>(className: "queue-button").ToList(), Is.Empty);
            Assert.That(Actions(), Is.Empty);

            _game.Apply(new Build(BuildingKind.Cottage, L2));

            Assert.That(QueueItems(), Is.EqualTo(new[] { "Väveri | Med handkraft · klar om 7 dygn", "Stuga | Byggs nu · 32 h arbete kvar" }));
            Assert.That(_root.Query<Button>(className: "queue-button").ToList(), Is.Empty);
            Assert.That(Actions(), Is.EqualTo(new[] { "Skynda på" }));
        }

        [Test]
        public void TheWeaveryBuiltByHandStandsAtTheSeventhDawnAndTheReportSaysSo()
        {
            WithoutBuildPower();
            _game.Apply(new BuildByHand(BuildingKind.Weavery, L1));

            _clock.Current = ScriptedWeek.At("2026-10-21 06:00");
            _game.CatchUp();
            Assert.That(_game.World!.BuildingAt(L1), Is.Null);

            _clock.Current = ScriptedWeek.At("2026-10-21 08:00");
            _game.CatchUp();
            Assert.That(_game.World.BuildingAt(L1)!.Kind, Is.EqualTo(BuildingKind.Weavery));
            Assert.That(_game.World.GameDay, Is.EqualTo(7));
            Assert.That(_game.SinceDawn.Select(e => Texts.Describe(_game.World, e)), Does.Contain("Väveri är färdig."));
        }

        /// <summary>The guided tour's 100 Build Power spent on a lookout and a jetty, both built by the evening of game day 0.</summary>
        private void WithoutBuildPower()
        {
            Assert.That(_game.Apply(new Build(BuildingKind.Lookout, L3)).Accepted, Is.True);
            Assert.That(_game.Apply(new Build(BuildingKind.Jetty, H1)).Accepted, Is.True);
            Assert.That(_game.World!.Available(Force.BuildPower), Is.Zero);
            _clock.Current = ScriptedWeek.At("2026-10-14 22:45");
            _game.CatchUp();
            Assert.That(_game.World.Projects, Is.Empty);
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

        private string[] Labels() => _root.Query<Label>().ToList().Select(l => l.text).ToArray();

        private string[] Actions() => _root.Query<Button>(className: "action").ToList().Select(b => b.Q<Label>(className: "action-title").text).ToArray();

        private string[] QueueItems() => _root.Query(className: "queue-item").ToList()
            .Select(i => $"{i.Q<Label>(className: "queue-title").text} | {i.Q<Label>(className: "queue-detail").text}")
            .ToArray();
    }
}
