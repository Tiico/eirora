using System;
using System.Collections;
using System.Globalization;
using System.IO;
using System.Linq;
using Eirora.Simulation.Commands;
using Eirora.Simulation.Economy;
using Eirora.Simulation.SelfCare;
using Eirora.Simulation.Time;
using UnityEngine;

namespace Eirora.Client.App
{
    /// <summary>
    /// Plays a scripted week on a fixed clock and saves screenshots of the main views, for pixel-level review of
    /// the interface and the light without a device (AGENTS.md: be picky about the UI).
    /// </summary>
    /// <remarks>Run a player with: -eirora-screenshots &lt;folder&gt;. It quits when done.</remarks>
    internal static class ScreenshotTour
    {
        private const string Zone = "Europe/Stockholm";

        public static IEnumerator Run(ClientBootstrap client, string folder)
        {
            Directory.CreateDirectory(folder);

            var clock = new FixedClock(At("2026-10-14 14:00"));
            var session = new GameSession(clock, null);
            client.Begin(session);
            yield return client.Frames(5);
            yield return Capture(client, folder, "01-first-launch");

            session.Begin(new RestSchedule(23 * 60, 8 * 60));
            PlayAWeek(session, clock);

            clock.Current = At("2026-10-20 13:00");
            session.CatchUp();
            client.Refresh();
            yield return Capture(client, folder, "02-day");

            client.Hud.Select(PrototypeIsland.Sites.First(s => s.Id == "L3"));
            yield return Capture(client, folder, "03-site");

            client.Hud.Select(PrototypeIsland.Sites.First(s => s.Id == "K1"));
            yield return Capture(client, folder, "04-building");
            client.Hud.Select(null);

            clock.Current = At("2026-10-20 22:20");
            session.CatchUp();
            client.Refresh();
            yield return Capture(client, folder, "05-dusk");

            clock.Current = At("2026-10-21 01:00");
            session.CatchUp();
            client.Refresh();
            yield return Capture(client, folder, "06-night");

            clock.Current = At("2026-10-21 07:20");
            session.CatchUp();
            client.Refresh();
            yield return Capture(client, folder, "07-dawn");

            client.Hud.OpenReport();
            yield return Capture(client, folder, "08-report");

            client.Hud.OpenSelfCare();
            yield return Capture(client, folder, "09-self-care");

            Application.Quit();
        }

        private static void PlayAWeek(GameSession session, FixedClock clock)
        {
            for (int day = 0; day < 6; day++)
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

        private static GameCommand[] Plan(int day) => day switch
        {
            0 => new GameCommand[] { new Survey(), new Build(BuildingKind.Woodcutter, "F3"), new Build(BuildingKind.Quarry, "M2"), new Workday("K1") },
            1 => new GameCommand[] { new Build(BuildingKind.Sawmill, "E2"), new Build(BuildingKind.Storehouse, "E1"), new Rush() },
            2 => new GameCommand[] { new Build(BuildingKind.Longhouse, "L1"), new Workday("M2") },
            3 => new GameCommand[] { new Build(BuildingKind.Cottage, "L2"), new Build(BuildingKind.Jetty, "H1") },
            4 => new GameCommand[] { new Survey(), new Build(BuildingKind.FishingHut, "K2"), new Upgrade("E2") },
            _ => new GameCommand[] { new Build(BuildingKind.Lookout, "S"), new BuildRoad("F2"), new Build(BuildingKind.SheepFarm, "P1") },
        };

        private static IEnumerator Capture(ClientBootstrap client, string folder, string name)
        {
            yield return client.Frames(3);
            ScreenCapture.CaptureScreenshot(Path.Combine(folder, name + ".png"));
            yield return client.Frames(3);
        }

        private static TimeReading At(string local)
        {
            var wallClock = DateTime.ParseExact(local, "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
            var moment = new DateTimeOffset(wallClock, TimeSpan.FromHours(2));
            return new TimeReading(moment.ToUnixTimeSeconds(), Zone, 7200);
        }
    }
}
