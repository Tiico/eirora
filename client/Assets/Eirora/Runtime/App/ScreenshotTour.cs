using System;
using System.Collections;
using System.IO;
using System.Linq;
using Eirora.Client.Accounts;
using Eirora.Client.Health;
using Eirora.Client.Look;
using Eirora.Simulation.Map;
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

            // The account against a fake server, so the bar looks as in a build with sign-in (TASK-022).
            var account = new AccountSession(
                FakeAccountServer.Url,
                new FakeAccountServer(() => DateTimeOffset.UtcNow),
                new MemorySecureStore(),
                new ILoginProvider[] { new FakeLogin(Contracts.LoginProviders.Apple, "tour"), new FakeLogin(Contracts.LoginProviders.Google, "tour") });
            client.Hud.UseAccount(account);

            session.Begin(new RestSchedule(23 * 60, 8 * 60));
            foreach (string refusal in ScriptedWeek.Play(session, clock))
            {
                Debug.LogError(refusal);
            }

            clock.Current = ScriptedWeek.At("2026-10-21 13:00");
            session.CatchUp();
            client.Refresh();
            yield return Capture(client, folder, "02-day");

            client.Hud.Select(ScriptedWeek.EmptyFarmland);
            yield return Capture(client, folder, "03-site");
            client.Hud.ShowHousingAndWorkshops();
            yield return Capture(client, folder, "03-site-housing");

            client.Hud.Select(ScriptedWeek.FishingHut);
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

            // The account: signed out, signed in with Google, and the confirmation before deleting it (TASK-022).
            client.Hud.OpenAccount();
            yield return Capture(client, folder, "14-account");
            client.Hud.Select(null);
            _ = account.SignInAsync(Contracts.LoginProviders.Google);
            client.Hud.OpenAccount();
            yield return Capture(client, folder, "15-account-signed-in");
            client.Hud.Select(null);
            client.Hud.OpenAccountDeletion();
            yield return Capture(client, folder, "16-account-deletion");
            client.Hud.Select(null);

            // The sawmill upgraded on day 4, next to the level-1 storehouse: how a level reads on the island (ADR-0009).
            clock.Current = ScriptedWeek.At("2026-10-23 13:00");
            session.CatchUp();
            client.Refresh();
            client.Focus(ScriptedWeek.Sawmill.Center());
            client.Hud.Select(ScriptedWeek.Sawmill);
            yield return Capture(client, folder, "13-upgraded");
            client.Hud.Select(null);

            // Nyön, found by the expedition and joined by the sea route.
            client.Focus(IslandMap.NyonCentre.Center() + new Vector3(0.9f, 0f, -0.4f));
            yield return Capture(client, folder, "10-nyon");

            // The fog at the edge of the known, which Exploration lifts tile by tile (TASK-032).
            var world = session.World!;
            var edge = Enumerable.Range(0, world.Map.Disc.Count).Select(i => world.Map.Disc[i])
                .Where(world.CanLiftFog).OrderBy(h => h.DistanceTo(Hex.Origin)).ThenBy(h => h).First();
            client.Focus(edge.Center());
            client.Hud.SelectFog(edge);
            yield return Capture(client, folder, "14-fog");

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
