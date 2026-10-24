using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Eirora.Simulation.Commands;
using Eirora.Simulation.Economy;
using Eirora.Simulation.Map;
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

        /// <summary>The start fishing hut, which the tour shows with its sheet.</summary>
        public static readonly Hex FishingHut = new(1, 0);

        /// <summary>Farmland the second survey finds and the week leaves empty, which the tour shows with its sheet.</summary>
        public static readonly Hex EmptyFarmland = new(-1, 2);

        /// <summary>The sawmill the week upgrades.</summary>
        public static readonly Hex Sawmill = new(2, -2);

        /// <summary>The fishing hut at the rich water, the farthest from a storehouse, which the tour gives a road.</summary>
        public static readonly Hex FarFishingHut = new(-3, 3);

        // The tiles the week builds on, by the names the economy model gives them (economy_v1.py, NAMES).
        private static readonly Hex F3 = new(-2, 0), M2 = new(3, -1), H1 = new(1, 1), E1 = new(2, -1), L1 = new(-1, 0), L2 = new(0, -1);
        private static readonly Hex L3 = new(-1, 1), K2 = new(-3, 3), N0 = new(10, 0), N1 = new(10, 1), N2 = new(11, 0);

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

        /// <summary>
        /// The player's say over the work, for the tour's shots of the queue and a paused building (TASK-044): a field on
        /// the empty farmland and a cottage on the nearest free meadow, the cottage moved first, and the start fishing
        /// hut paused. Returns the commands the world refused, which should be none.
        /// </summary>
        public static IReadOnlyList<string> OrderTheWork(GameSession session)
        {
            var world = session.World!;
            var meadow = Enumerable.Range(0, world.Map.Disc.Count).Select(i => world.Map.Disc[i])
                .Where(h => h != EmptyFarmland && world.CheckBuild(BuildingKind.Cottage, h) == Rejection.None)
                .OrderBy(h => h.DistanceTo(Hex.Origin)).ThenBy(h => h).First();
            var refused = new List<string>();
            foreach (var command in new GameCommand[]
            {
                new Build(BuildingKind.Field, EmptyFarmland), new Build(BuildingKind.Cottage, meadow),
                new QueueFirst(ProjectKind.Build, meadow), new PauseBuilding(FishingHut),
            })
            {
                var result = session.Apply(command);
                if (!result.Accepted)
                {
                    refused.Add($"{Describe(command)} was refused ({result.Rejection}).");
                }
            }

            return refused;
        }

        /// <summary>
        /// The player's stock rules, for the tour's shot of Nyön's store (TASK-046): stone from the vein shipped before the
        /// wool, and wool reserved on Nyön. Returns the commands the world refused, which should be none.
        /// </summary>
        public static IReadOnlyList<string> SetStockRules(GameSession session)
        {
            var refused = new List<string>();
            foreach (var command in new GameCommand[]
            {
                new MoveImport(PrototypeHome.Nyon, Resource.Wool, 0), new MoveImport(PrototypeHome.Nyon, Resource.Stone, 0),
                new SetReservation(PrototypeHome.Nyon, Resource.Wool, 10),
            })
            {
                var result = session.Apply(command);
                if (!result.Accepted)
                {
                    refused.Add($"{Describe(command)} was refused ({result.Rejection}).");
                }
            }

            return refused;
        }

        /// <summary>
        /// The road to the storehouse from the fishing hut at the rich water, first in the queue, for the tour's shots of
        /// roads and carriers (D-112). Returns the commands the world refused, which should be none.
        /// </summary>
        public static IReadOnlyList<string> LayARoad(GameSession session)
        {
            var tiles = session.World!.RoadToDepot(FarFishingHut).ToList();
            var refused = new List<string>();
            foreach (var command in new GameCommand[] { new BuildRoad(FarFishingHut) }.Concat(tiles.AsEnumerable().Reverse().Select(t => new QueueFirst(ProjectKind.Road, t))))
            {
                var result = session.Apply(command);
                if (!result.Accepted)
                {
                    refused.Add($"{Describe(command)} was refused ({result.Rejection}).");
                }
            }

            return tiles.Count == 0 ? new[] { $"No road can be laid to {FarFishingHut}." } : refused;
        }

        /// <summary>A reading of Stockholm summer time, as the tour's player would see it.</summary>
        public static TimeReading At(string local)
        {
            var wallClock = DateTime.ParseExact(local, "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
            var moment = new DateTimeOffset(wallClock, TimeSpan.FromHours(2));
            return new TimeReading(moment.ToUnixTimeSeconds(), Zone, 7200);
        }

        // Timber and planks are what holds the week back, so the woodcutter works extra days and Dawn Power rushes the
        // longhouse, the cottage and Nyön's jetty; the jetty must stand before anything else is built on Nyön. The
        // storehouses keep one carrier each, so that the villagers build rather than carry (D-109).
        private static GameCommand[] Plan(int day) => day switch
        {
            0 => new GameCommand[] { new Survey(), new Build(BuildingKind.Woodcutter, F3), new Build(BuildingKind.Quarry, M2), new Build(BuildingKind.Jetty, H1), new Workday(FishingHut), new SetCarrierCap(Hex.Origin, 1) },
            1 => new GameCommand[] { new Build(BuildingKind.Sawmill, Sawmill), new Build(BuildingKind.Storehouse, E1), new Rush(), new Workday(F3) },
            2 => new GameCommand[] { new Workday(M2), new Workday(F3) },
            3 => new GameCommand[] { new SetCarrierCap(E1, 1), new Build(BuildingKind.Cottage, L2), new SendExpedition(), new Workday(F3), new Rush(), new Rush(), new Rush(), new Rush() },
            4 => new GameCommand[] { new Survey(), new Build(BuildingKind.Longhouse, L1), new Upgrade(Sawmill), new Build(BuildingKind.FishingHut, K2), new Rush(), new Rush(), new Rush() },
            5 => new GameCommand[] { new Build(BuildingKind.Jetty, N0), new QueueFirst(ProjectKind.Build, N0), new Rush(), new Rush(), new Rush(), new Build(BuildingKind.Lookout, L3) },
            _ => new GameCommand[] { new Build(BuildingKind.SheepFarm, N1), new Build(BuildingKind.Quarry, N2) },
        };

        private static string Describe(GameCommand command) => command switch
        {
            Build build => $"Build {build.Building} on {build.Tile}",
            Upgrade upgrade => $"Upgrade {upgrade.Tile}",
            Workday workday => $"Workday {workday.Tile}",
            _ => command.GetType().Name,
        };
    }
}
