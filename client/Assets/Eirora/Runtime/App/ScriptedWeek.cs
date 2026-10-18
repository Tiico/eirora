using System;
using System.Globalization;
using Eirora.Simulation.Commands;
using Eirora.Simulation.Economy;
using Eirora.Simulation.SelfCare;
using Eirora.Simulation.Time;

namespace Eirora.Client.App
{
    /// <summary>
    /// The same played week on a fixed clock, for views that must look alike every time: the screenshot tour and the
    /// device measurements (docs/design/device-measurements.md).
    /// </summary>
    public static class ScriptedWeek
    {
        private const string Zone = "Europe/Stockholm";

        /// <summary>Plays game days 1–7 with self-care and a plan, leaving the clock on the seventh morning.</summary>
        public static void Play(GameSession session, FixedClock clock)
        {
            for (int day = 0; day < 7; day++)
            {
                clock.Current = At($"2026-10-{15 + day} 08:00");
                session.CatchUp();
                int today = session.World!.GameDay;
                session.Apply(new RecordSelfCare(today, Habit.Steps, ContributionSource.Verified, 6_000 + (day * 900)));
                session.Apply(new RecordSelfCare(today, Habit.Sleep, ContributionSource.Verified, 420));
                session.Apply(new RecordSelfCare(today, Habit.Workouts, ContributionSource.Verified, day % 2));
                foreach (var command in Plan(day))
                {
                    session.Apply(command);
                }
            }
        }

        /// <summary>A reading of Stockholm summer time, as the tour's player would see it.</summary>
        public static TimeReading At(string local)
        {
            var wallClock = DateTime.ParseExact(local, "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
            var moment = new DateTimeOffset(wallClock, TimeSpan.FromHours(2));
            return new TimeReading(moment.ToUnixTimeSeconds(), Zone, 7200);
        }

        private static GameCommand[] Plan(int day) => day switch
        {
            0 => new GameCommand[] { new Survey(), new Build(BuildingKind.Woodcutter, "F3"), new Build(BuildingKind.Quarry, "M2"), new Build(BuildingKind.Jetty, "H1"), new Workday("K1") },
            1 => new GameCommand[] { new Build(BuildingKind.Sawmill, "E2"), new Build(BuildingKind.Storehouse, "E1"), new Rush() },
            2 => new GameCommand[] { new Build(BuildingKind.Longhouse, "L1"), new Workday("M2") },
            3 => new GameCommand[] { new Build(BuildingKind.Cottage, "L2"), new SendExpedition() },
            4 => new GameCommand[] { new Survey(), new Build(BuildingKind.FishingHut, "K2"), new Upgrade("E2") },
            5 => new GameCommand[] { new Build(BuildingKind.Jetty, "N0"), new Build(BuildingKind.Lookout, "S") },
            _ => new GameCommand[] { new Build(BuildingKind.Jetty, "N0"), new Build(BuildingKind.SheepFarm, "N1"), new Build(BuildingKind.Quarry, "N2") },
        };
    }
}
