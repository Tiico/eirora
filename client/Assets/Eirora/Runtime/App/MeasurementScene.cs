using System.Collections;
using System.Diagnostics;
using System.Globalization;
using Eirora.CloudSaving;
using Eirora.Simulation.Map;
using Eirora.Simulation.Saving;
using Eirora.Simulation.Time;
using UnityEngine;

namespace Eirora.Client.App
{
    /// <summary>
    /// Holds the island after the scripted week in the day or the night light, or a built-out world a year after it was
    /// left, without saving, so that device measurements compare the same view from run to run
    /// (docs/design/device-measurements.md).
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
        /// The built-out world the year scene catches up, a save under Resources that the .NET tests time a year away in
        /// (FastForwardTests), so that the phone's catch-up can be set against theirs.
        /// </summary>
        public const string YearAwaySave = "Measurement/AYearAway";

        private const long Year = 365 * 24 * 60 * 60;

        /// <summary>The scripted week's seventh day at 13:00, or the night after it at 01:00 during rest.</summary>
        public static TimeReading? MomentOf(string light) => light switch
        {
            "day" => ScriptedWeek.At("2026-10-21 13:00"),
            "night" => ScriptedWeek.At("2026-10-22 01:00"),
            _ => null,
        };

        /// <summary>
        /// A year after the player left, at the same time of day, in one reading as the fixed clock gives it; the tests
        /// read the year the same way.
        /// </summary>
        public static TimeReading AYearAfter(TimeReading left) => new(left.UtcSeconds + Year, left.ZoneId, left.OffsetSeconds);

        /// <summary>
        /// Plays the scripted week in <paramref name="session"/> and stops the clock at <paramref name="moment"/>.
        /// Returns the milliseconds the world took to catch up from the week's last morning to the moment.
        /// </summary>
        public static long Play(GameSession session, FixedClock clock, TimeReading moment)
        {
            session.Begin(new RestSchedule(23 * 60, 8 * 60));
            ScriptedWeek.Play(session, clock);
            clock.Current = moment;
            return CatchUp(session);
        }

        /// <summary>
        /// Makes <paramref name="save"/> the world in <paramref name="session"/> and lets a year pass since it was left.
        /// Returns the milliseconds the world took to catch up.
        /// </summary>
        public static long PlayAYearAway(GameSession session, FixedClock clock, Save save)
        {
            var left = save.World.Now;
            clock.Current = left;
            session.Replace(save, ReplaceReason.Downloaded);
            clock.Current = AYearAfter(left);
            return CatchUp(session);
        }

        internal static bool TryBegin(ClientBootstrap client, string light)
        {
            long catchUp;
            if (light == "year")
            {
                var save = Save.Load(Resources.Load<TextAsset>(YearAwaySave).bytes);
                var clock = new FixedClock(save.World.Now);
                var session = new GameSession(clock, null, () => PrototypeHome.Map);
                client.Begin(session);
                catchUp = PlayAYearAway(session, clock, save);
            }
            else if (MomentOf(light) is TimeReading moment)
            {
                var clock = new FixedClock(ScriptedWeek.At("2026-10-14 14:00"));
                var session = new GameSession(clock, null, () => PrototypeHome.Map); // The scripted week is played on Hemön.
                client.Begin(session);
                catchUp = Play(session, clock, moment);
            }
            else
            {
                return false;
            }

            client.Refresh();
            client.StartCoroutine(LogFirstFrame(light, catchUp));
            return true;
        }

        private static long CatchUp(GameSession session)
        {
            var watch = Stopwatch.StartNew();
            session.CatchUp();
            return watch.ElapsedMilliseconds;
        }

        private static IEnumerator LogFirstFrame(string light, long catchUpMilliseconds)
        {
            yield return new WaitForEndOfFrame();
            UnityEngine.Debug.Log($"{FirstFrameLog} of the {light} scene, caught up in {catchUpMilliseconds.ToString(CultureInfo.InvariantCulture)} ms");
        }
    }
}
