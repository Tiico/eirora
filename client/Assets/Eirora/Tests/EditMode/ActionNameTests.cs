using System.Linq;
using Eirora.Client.App;
using Eirora.Client.UI;
using Eirora.Simulation.Map;
using Eirora.Simulation.Time;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace Eirora.Client.Tests
{
    /// <summary>
    /// The actions are named in everyday Swedish (D-120), and the names reach the screen as written: an encoding slip
    /// once showed "Skynda pÃ¥" on a button.
    /// </summary>
    public sealed class ActionNameTests
    {
        [Test]
        public void TheQueueAndABuildingSheetNameTheActionsInEverydaySwedish()
        {
            var clock = new FixedClock(ScriptedWeek.At("2026-10-14 14:00"));
            var game = new GameSession(clock, null, () => PrototypeHome.Map);
            game.Begin(new RestSchedule(23 * 60, 8 * 60));
            Assert.That(ScriptedWeek.Play(game, clock), Is.Empty);
            clock.Current = ScriptedWeek.At("2026-10-21 13:00");
            game.CatchUp();
            var root = new VisualElement();
            var hud = new Hud(root, command => game.Apply(command));
            hud.Show(game.World!, game.SinceDawn);

            hud.OpenQueue();
            Assert.That(Actions(root), Does.Contain("Skynda på"));

            hud.Select(ScriptedWeek.FishingHut);
            Assert.That(Actions(root), Does.Contain("Arbeta extra"));
        }

        private static string[] Actions(VisualElement root) =>
            root.Query<Button>(className: "action").ToList().Select(a => a.Q<Label>(className: "action-title")?.text ?? "–").ToArray();
    }
}
