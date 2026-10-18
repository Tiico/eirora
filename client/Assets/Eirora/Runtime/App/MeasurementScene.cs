using Eirora.Simulation.Time;

namespace Eirora.Client.App
{
    /// <summary>
    /// Holds the island after the scripted week in the day or the night light, without saving, so that device
    /// measurements compare the same view from run to run (docs/design/device-measurements.md).
    /// </summary>
    /// <remarks>Run a player with: -eirora-measure day|night. It runs until stopped.</remarks>
    public static class MeasurementScene
    {
        /// <summary>The scripted week's seventh day at 13:00, or the night after it at 01:00 during rest.</summary>
        public static TimeReading? MomentOf(string light) => light switch
        {
            "day" => ScriptedWeek.At("2026-10-21 13:00"),
            "night" => ScriptedWeek.At("2026-10-22 01:00"),
            _ => null,
        };

        /// <summary>Plays the scripted week in <paramref name="session"/> and stops the clock at <paramref name="moment"/>.</summary>
        public static void Play(GameSession session, FixedClock clock, TimeReading moment)
        {
            session.Begin(new RestSchedule(23 * 60, 8 * 60));
            ScriptedWeek.Play(session, clock);
            clock.Current = moment;
            session.CatchUp();
        }

        internal static bool TryBegin(ClientBootstrap client, string light)
        {
            if (MomentOf(light) is not TimeReading moment)
            {
                return false;
            }

            var clock = new FixedClock(ScriptedWeek.At("2026-10-14 14:00"));
            var session = new GameSession(clock, null);
            client.Begin(session);
            Play(session, clock, moment);
            client.Refresh();
            return true;
        }
    }
}
