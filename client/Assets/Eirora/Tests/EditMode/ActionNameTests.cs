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
    /// The actions are named in everyday Swedish (D-109), and the names reach the screen as written: an encoding slip
    /// once showed "Skynda pÃ¥" on the bar.
    /// </summary>
    public sealed class ActionNameTests
    {
        [Test]
        public void TheBarAndABuildingSheetNameTheActionsInEverydaySwedish()
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

            var bar = root.Query<Button>(className: "button").ToList()
                .Select(b => b.Q<Label>(className: "button-title")?.text ?? b.text)
                .Where(text => text.Length > 0);
            Assert.That(bar, Is.EqualTo(new[] { "Egenvård", "Kartlagt", "Skynda på", "Rapport" }));

            hud.Select(ScriptedWeek.FishingHut);
            var actions = root.Query<Button>(className: "action").ToList().Select(a => a.Q<Label>(className: "action-title")?.text);
            Assert.That(actions, Is.EqualTo(new[] { "Arbeta extra", "Uppgradera till nivå 2" }));
        }
    }
}
