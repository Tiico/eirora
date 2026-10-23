using System;
using Eirora.Client.App;
using Eirora.Simulation.Map;
using Eirora.Simulation.Saving;
using Eirora.Simulation.Time;
using NUnit.Framework;
using UnityEngine;

namespace Eirora.Client.Tests
{
    /// <summary>The scene device measurements run in (TASK-015, docs/design/device-measurements.md).</summary>
    public sealed class MeasurementSceneTests
    {
        [TestCase("day", Phase.Active)]
        [TestCase("night", Phase.Resting)]
        public void TheSceneHoldsTheLightItWasAskedFor(string light, Phase phase)
        {
            var session = Played(light);

            Assert.That(session.World!.Phase, Is.EqualTo(phase));
        }

        [Test]
        public void EveryRunMeasuresTheSameWorld() => Assert.That(StateOf(Played("night")), Is.EqualTo(StateOf(Played("night"))));

        [Test]
        public void AnUnknownLightStartsNoScene() => Assert.That(MeasurementScene.MomentOf("dusk"), Is.Null);

        // The year scene catches up the built-out world the .NET tests time a year away in (TASK-049).
        [Test]
        public void TheYearSceneCatchesUpABuiltOutWorldAYearAfterItWasLeft()
        {
            var save = Save.Load(Resources.Load<TextAsset>(MeasurementScene.YearAwaySave).bytes);
            var left = save.World.Now;
            var clock = new FixedClock(left);
            var session = new GameSession(clock, null, () => PrototypeHome.Map);

            MeasurementScene.PlayAYearAway(session, clock, save);

            Assert.That(session.World!.Now.UtcSeconds - left.UtcSeconds, Is.EqualTo(365L * 24 * 60 * 60));
            Assert.That(session.World.Phase, Is.EqualTo(Phase.Active));
            Assert.That(session.World.Buildings.Count, Is.GreaterThanOrEqualTo(30));
        }

        // The session gives each command a fresh idempotency key, so the used keys differ while the world does not.
        // The fingerprint writes the used commands last, one "command" line each.
        private static string StateOf(GameSession session)
        {
            string fingerprint = session.World!.Fingerprint();
            int commands = fingerprint.IndexOf("\ncommand ", StringComparison.Ordinal);
            return commands < 0 ? fingerprint : fingerprint.Substring(0, commands);
        }

        private static GameSession Played(string light)
        {
            var clock = new FixedClock(ScriptedWeek.At("2026-10-14 14:00"));
            var session = new GameSession(clock, null, () => PrototypeHome.Map);
            MeasurementScene.Play(session, clock, MeasurementScene.MomentOf(light)!.Value);
            return session;
        }
    }
}
