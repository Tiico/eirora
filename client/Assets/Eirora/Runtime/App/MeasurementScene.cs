using System.Collections;
using System.Diagnostics;
using System.Globalization;
using Eirora.Simulation.Map;
using Eirora.Simulation.Time;
using UnityEngine;

namespace Eirora.Client.App
{
    /// <summary>
    /// Holds the island after the scripted week in the day or the night light, or a year later in daylight, without
    /// saving, so that device measurements compare the same view from run to run (docs/design/device-measurements.md).
    /// </summary>
    /// <remarks>
    /// Run a player with: -eirora-measure day|night|year. It runs until stopped. When it has drawn its first frame it
    /// logs how long catching up with the clock took, which <c>measure.sh cold-start</c> reads.
    /// </remarks>
    public static class MeasurementScene
    {
        /// <summary>What the client logs when the first frame with the world is drawn; measure.sh looks for it.</summary>
        public const string FirstFrameLog = "Eirora measure: first frame";

        /// <summary>
        /// The scripted week's seventh day at 13:00, the night after it at 01:00 during rest, or the same day a year
        /// later, as when the player comes back after a year away (TASK-049).
        /// </summary>
        public static TimeReading? MomentOf(string light) => light switch
        {
            "day" => ScriptedWeek.At("2026-10-21 13:00"),
            "night" => ScriptedWeek.At("2026-10-22 01:00"),
            "year" => ScriptedWeek.At("2027-10-21 13:00"),
            _ => null,
        };

        /// <summary>
        /// Plays the scripted week in <paramref name="session"/> and stops the clock at <paramref name="moment"/>.
        /// Returns the milliseconds the world took to catch up from the week's last morning to the moment.
        /// </summary>
        public static long Play(GameSession session, FixedClock clock, TimeReading moment)
        {
            session.Begin(new RestSchedule(23 * 60, 8 * 60));
            ScriptedWeek.Play(session, clock);
            clock.Current = moment;
            var watch = Stopwatch.StartNew();
            session.CatchUp();
            return watch.ElapsedMilliseconds;
        }

        internal static bool TryBegin(ClientBootstrap client, string light)
        {
            if (MomentOf(light) is not TimeReading moment)
            {
                return false;
            }

            var clock = new FixedClock(ScriptedWeek.At("2026-10-14 14:00"));
            var session = new GameSession(clock, null, () => PrototypeHome.Map); // The scripted week is played on Hemön.
            client.Begin(session);
            long catchUp = Play(session, clock, moment);
            client.Refresh();
            client.StartCoroutine(LogFirstFrame(light, catchUp));
            return true;
        }

        private static IEnumerator LogFirstFrame(string light, long catchUpMilliseconds)
        {
            yield return new WaitForEndOfFrame();
            UnityEngine.Debug.Log($"{FirstFrameLog} of the {light} scene, caught up in {catchUpMilliseconds.ToString(CultureInfo.InvariantCulture)} ms");
        }
    }
}
