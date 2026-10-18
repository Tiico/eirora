using System;
using Eirora.Client.App;
using Eirora.Simulation.Time;
using NUnit.Framework;

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

        // The session gives each command a fresh idempotency key, so the used keys differ while the world does not.
        private static string StateOf(GameSession session)
        {
            string fingerprint = session.World!.Fingerprint();
            return fingerprint.Substring(0, fingerprint.IndexOf(";commands:", StringComparison.Ordinal));
        }

        private static GameSession Played(string light)
        {
            var clock = new FixedClock(ScriptedWeek.At("2026-10-14 14:00"));
            var session = new GameSession(clock, null);
            MeasurementScene.Play(session, clock, MeasurementScene.MomentOf(light)!.Value);
            return session;
        }
    }
}
