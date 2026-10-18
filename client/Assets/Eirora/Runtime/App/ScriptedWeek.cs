using System;
using System.Collections.Generic;
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

        /// <summary>
        /// Plays game days 1–7 with self-care and the Havet path of economy-v1.md, section 7, from the first survey to a
        /// sheep farm and a quarry on Nyön, leaving the clock on the seventh morning. Returns the commands the world
        /// refused, which should be none.
        /// </summary>
        public static IReadOnlyList<string> Play(GameSession session, FixedClock clock)
        {
            var refused = new List<string>();
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
                    var result = session.Apply(command);
                    if (!result.Accepted)
                    {
                        refused.Add($"Day {day}: {Describe(command)} was refused ({result.Rejection}).");
                    }
                }
            }

            return refused;
        }

        /// <summary>A reading of Stockholm summer time, as the tour's player would see it.</summary>
        public static TimeReading At(string local)
        {
            var wallClock = DateTime.ParseExact(local, "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
            var moment = new DateTimeOffset(wallClock, TimeSpan.FromHours(2));
            return new TimeReading(moment.ToUnixTimeSeconds(), Zone, 7200);
        }

        // Timber and planks are what holds the week back, so the woodcutter works extra days and Dawn Power rushes the
        // longhouse, the cottage and Nyön's jetty; the jetty must stand before anything else is built on Nyön.
        private static GameCommand[] Plan(int day) => day switch
        {
            0 => new GameCommand[] { new Survey(), new Build(BuildingKind.Woodcutter, "F3"), new Build(BuildingKind.Quarry, "M2"), new Build(BuildingKind.Jetty, "H1"), new Workday("K1") },
            1 => new GameCommand[] { new Build(BuildingKind.Sawmill, "E2"), new Build(BuildingKind.Storehouse, "E1"), new Rush(), new Workday("F3") },
            2 => new GameCommand[] { new Workday("M2"), new Workday("F3") },
            3 => new GameCommand[] { new Build(BuildingKind.Longhouse, "L1"), new Build(BuildingKind.Cottage, "L2"), new SendExpedition(), new Workday("F3"), new Rush(), new Rush(), new Rush(), new Rush() },
            4 => new GameCommand[] { new Survey(), new Upgrade("E2"), new Build(BuildingKind.FishingHut, "K2"), new Rush(), new Rush(), new Rush() },
            5 => new GameCommand[] { new Build(BuildingKind.Jetty, "N0"), new Rush(), new Rush(), new Rush(), new Build(BuildingKind.Lookout, "L3") },
            _ => new GameCommand[] { new Build(BuildingKind.SheepFarm, "N1"), new Build(BuildingKind.Quarry, "N2") },
        };

        private static string Describe(GameCommand command) => command switch
        {
            Build build => $"Build {build.Building} on {build.SiteId}",
            Upgrade upgrade => $"Upgrade {upgrade.SiteId}",
            Workday workday => $"Workday {workday.SiteId}",
            _ => command.GetType().Name,
        };
    }
}
