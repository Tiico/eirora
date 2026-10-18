using System.Collections;
using System.IO;
using System.Linq;
using Eirora.Client.Health;
using Eirora.Client.Look;
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
        public static IEnumerator Run(ClientBootstrap client, string folder)
        {
            // ScreenCapture resolves a relative path against the player's data folder, not the working directory.
            folder = Path.GetFullPath(folder);
            Directory.CreateDirectory(folder);

            var clock = new FixedClock(ScriptedWeek.At("2026-10-14 14:00"));
            var session = new GameSession(clock, null);
            client.Begin(session);
            yield return client.Frames(5);
            yield return Capture(client, folder, "01-first-launch");

            session.Begin(new RestSchedule(23 * 60, 8 * 60));
            ScriptedWeek.Play(session, clock);

            clock.Current = ScriptedWeek.At("2026-10-21 13:00");
            session.CatchUp();
            client.Refresh();
            yield return Capture(client, folder, "02-day");

            client.Hud.Select(PrototypeIsland.Sites.First(s => s.Id == "L3"));
            yield return Capture(client, folder, "03-site");

            client.Hud.Select(PrototypeIsland.Sites.First(s => s.Id == "K1"));
            yield return Capture(client, folder, "04-building");
            client.Hud.Select(null);

            clock.Current = ScriptedWeek.At("2026-10-21 22:20");
            session.CatchUp();
            client.Refresh();
            yield return Capture(client, folder, "05-dusk");

            clock.Current = ScriptedWeek.At("2026-10-22 01:00");
            session.CatchUp();
            client.Refresh();
            yield return Capture(client, folder, "06-night");

            clock.Current = ScriptedWeek.At("2026-10-22 07:20");
            session.CatchUp();
            client.Refresh();
            yield return Capture(client, folder, "07-dawn");

            client.Hud.OpenReport();
            yield return Capture(client, folder, "08-report");

            client.Hud.OpenSelfCare();
            yield return Capture(client, folder, "09-self-care");
            client.Hud.Select(null);

            // Health Connect on a phone where it is installed but not yet connected (TASK-013).
            client.UseHealthSource(new FakeHealthSource(new[] { Habit.Steps, Habit.Workouts, Habit.Sleep }));
            yield return client.Frames(3);
            client.Hud.OpenSelfCare();
            yield return Capture(client, folder, "11-health-connect");
            client.Hud.Select(null);
            client.Hud.OpenHealthExplanation();
            yield return Capture(client, folder, "12-health-explanation");
            client.Hud.Select(null);

            // Nyön, found by the expedition and joined by the sea route.
            clock.Current = ScriptedWeek.At("2026-10-23 13:00");
            session.CatchUp();
            client.Refresh();
            client.Focus(IslandMap.NyonCentre.Center + new Vector3(0.9f, 0f, -0.4f));
            yield return Capture(client, folder, "10-nyon");

            Application.Quit();
        }

        private static IEnumerator Capture(ClientBootstrap client, string folder, string name)
        {
            yield return client.Frames(3);
            ScreenCapture.CaptureScreenshot(Path.Combine(folder, name + ".png"));
            yield return client.Frames(3);
        }
    }
}
