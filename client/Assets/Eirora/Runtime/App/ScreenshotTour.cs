using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Eirora.Client.Accounts;
using Eirora.Client.Health;
using Eirora.Client.Look;
using Eirora.Simulation.Economy;
using Eirora.Simulation.Map;
using Eirora.Simulation.SelfCare;
using Eirora.Simulation.Time;
using Eirora.WorldGeneration;
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
        // A building's sheet with its staffing and actions reaches half up the screen; looking this much further south keeps
        // the building it tells of in view above it.
        private static readonly Vector3 AboveTheSheet = new(0f, 0f, -1.6f);

        public static IEnumerator Run(ClientBootstrap client, string folder)
        {
            // A macOS player pauses when its window loses focus, which another app in front takes; the tour plays on.
            Application.runInBackground = true;

            // ScreenCapture resolves a relative path against the player's data folder, not the working directory.
            folder = Path.GetFullPath(folder);
            Directory.CreateDirectory(folder);

            var clock = new FixedClock(ScriptedWeek.At("2026-10-14 14:00"));
            var session = new GameSession(clock, null, () => PrototypeHome.Map); // The scripted week is played on Hemön.
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

            // The cloud save, which asks about the self-care before its first upload (TASK-060).
            var cloud = new CloudSaveSync(account, session, new DeviceCloudSyncStorage(new MemorySecureStore(), null), () => Task.CompletedTask);
            client.Hud.UseCloudSync(cloud);

            session.Begin(new RestSchedule(23 * 60, 8 * 60));
            foreach (string refusal in ScriptedWeek.Play(session, clock))
            {
                Debug.LogError(refusal);
            }

            // The seventh morning, while the sawmill waits for timber that the storehouse's one carrier has yet to bring.
            clock.Current = ScriptedWeek.At("2026-10-21 10:00");
            session.CatchUp();
            client.Refresh();
            yield return Capture(client, folder, "02-day");

            client.Hud.Select(ScriptedWeek.EmptyFarmland);
            yield return Capture(client, folder, "03-site");
            client.Hud.ShowMoreBuildings();
            yield return Capture(client, folder, "03-site-more");

            client.Hud.Select(ScriptedWeek.FishingHut);
            yield return Capture(client, folder, "04-building");
            client.Hud.Select(null);

            // A building that stands still (TASK-043): the sign above it on the island, and its sheet saying why.
            if (session.World!.Diagnose().Buildings.FirstOrDefault(b => b.StandsStill) is { } still)
            {
                client.Hud.Select(still.Building.Tile);
                yield return Capture(client, folder, "04-standing-still");
                client.Hud.Select(null);
            }

            // A road to the far fishing hut, laid by the time of shot 22 (TASK-051).
            foreach (string refusal in ScriptedWeek.LayARoad(session))
            {
                Debug.LogError(refusal);
            }

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

            // The account: signed out, the question about the self-care at sign-in (TASK-060), signed in with Google, the
            // confirmation before the self-care leaves the server, and the one before deleting the account (TASK-022).
            client.Hud.OpenAccount();
            yield return Capture(client, folder, "14-account");
            client.Hud.Select(null);
            _ = account.SignInAsync(Contracts.LoginProviders.Google);
            yield return Capture(client, folder, "15-consent-question");
            _ = cloud.AnswerConsentAsync(true);
            client.Hud.OpenAccount();
            yield return Capture(client, folder, "15-account-signed-in");
            client.Hud.Select(null);
            client.Hud.OpenConsentWithdrawal();
            yield return Capture(client, folder, "16-consent-withdrawal");
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

            // What holds the island back most, as the report points to it, and a building far from the storehouse with
            // what a road would give (TASK-043).
            var diagnosis = session.World!.Diagnose();
            if (diagnosis.BottleneckOn(session.World!.HomeIsland) is { } bottleneck)
            {
                client.Focus(bottleneck.Building.Building.Tile.Center() + AboveTheSheet);
                client.Hud.Select(bottleneck.Building.Building.Tile);
                yield return Capture(client, folder, "13-bottleneck");
                client.Hud.Select(null);
            }

            if (diagnosis.Buildings.FirstOrDefault(b => b.Hindrances.Any(h => h is CarriersBehind)) is { } behind)
            {
                client.Focus(behind.Building.Tile.Center() + AboveTheSheet);
                client.Hud.Select(behind.Building.Tile);
                yield return Capture(client, folder, "13-carriers-behind");
                client.Hud.Select(null);
            }

            // The carriers of the storehouse that cannot keep up, or else the first one's, and the player's cap on them, and
            // the road laid to the far fishing hut (TASK-051).
            var carrying = diagnosis.Buildings.FirstOrDefault(b => b.Hindrances.Any(h => h is CarriersBehind))?.Building.Depot ?? Hex.Origin;
            client.Focus(carrying.Center() + AboveTheSheet);
            client.Hud.Select(carrying);
            yield return Capture(client, folder, "21-storehouse-carriers");
            client.Hud.Select(null);
            client.Focus(ScriptedWeek.FarFishingHut.Center() + new Vector3(1.4f, 0f, 0.6f));
            yield return Capture(client, folder, "22-road");
            client.Focus(ScriptedWeek.FarFishingHut.Center() + new Vector3(1.4f, 0f, 0.6f) + AboveTheSheet);
            client.Hud.LayRoads();
            yield return Capture(client, folder, "23-laying-roads");
            client.Hud.Select(null);

            // Nyön, found by the expedition and joined by the sea route.
            client.Focus(PrototypeHome.NyonCentre.Center() + new Vector3(0.9f, 0f, -0.4f));
            yield return Capture(client, folder, "10-nyon");

            // The jetties' sheets: the sea route with the goods waiting on Nyön, from home and from Nyön (TASK-045).
            var world = session.World!;
            foreach (var (island, name) in new[] { (world.HomeIsland, "19-jetty-home"), (PrototypeHome.Nyon, "20-jetty-nyon") })
            {
                var jetty = world.Buildings.First(b => b.Kind == BuildingKind.Jetty && b.Island == island).Tile;
                client.Focus(jetty.Center() + AboveTheSheet);
                client.Hud.Select(jetty);
                yield return Capture(client, folder, name);
            }

            // Nyön's store with stone shipped first and wool reserved (TASK-046).
            foreach (string refusal in ScriptedWeek.SetStockRules(session))
            {
                Debug.LogError(refusal);
            }

            client.Focus(PrototypeHome.NyonCentre.Center() + new Vector3(0.9f, 0f, -0.4f) + AboveTheSheet);
            client.Hud.OpenStore(PrototypeHome.Nyon, Resource.Wool);
            yield return Capture(client, folder, "24-store-nyon");
            client.Hud.Select(null);

            // The fog at the edge of the known, which Exploration lifts tile by tile (TASK-032).
            var edge = Enumerable.Range(0, world.Map.Disc.Count).Select(i => world.Map.Disc[i])
                .Where(world.CanLiftFog).OrderBy(h => h.DistanceTo(Hex.Origin)).ThenBy(h => h).First();
            client.Focus(edge.Center());
            client.Hud.SelectFog(edge);
            yield return Capture(client, folder, "14-fog");
            client.Hud.Select(null);

            // The player's say over the work (TASK-044): the queue with the cottage moved first, and the paused fishing hut.
            foreach (string refusal in ScriptedWeek.OrderTheWork(session))
            {
                Debug.LogError(refusal);
            }

            client.Focus(Hex.Origin.Center());
            client.Hud.OpenQueue();
            yield return Capture(client, folder, "19-queue");
            client.Hud.Select(ScriptedWeek.FishingHut);
            yield return Capture(client, folder, "20-paused");
            client.Hud.Select(null);

            // A new world on a generated map (TASK-033): the village at dawn, then the whole archipelago from afar.
            var generatedClock = new FixedClock(ScriptedWeek.At("2026-10-14 14:00"));
            var generated = new GameSession(generatedClock, null, () => new ProceduralArchipelagoV1().Generate(42));
            client.Begin(generated);
            generated.Begin(new RestSchedule(23 * 60, 8 * 60));
            generatedClock.Current = ScriptedWeek.At("2026-10-15 09:00");
            generated.CatchUp();
            client.Refresh();
            yield return Capture(client, folder, "17-generated-world");
            client.ZoomTo(2);
            yield return Capture(client, folder, "18-generated-archipelago");

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
