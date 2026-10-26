using System;
using System.Collections;
using System.IO;
using Eirora.Client.Accounts;
using Eirora.Client.App;
using Eirora.Client.Health;
using Eirora.Client.Look;
using Eirora.Client.UI;
using Eirora.Simulation.Commands;
using Eirora.Simulation.Map;
using UnityEngine;
using UnityEngine.UIElements;

namespace Eirora.Client
{
    /// <summary>
    /// Entry point of the client: loads or begins the world, keeps it in step with the clock and wires the island,
    /// the light, the interface and the camera together. The world itself lives in the shared simulation.
    /// </summary>
    public sealed class ClientBootstrap : MonoBehaviour
    {
        private const float CatchUpSeconds = 1f;

        private GameSession _session = null!;
        private IslandView _island = null!;
        private Atmosphere _atmosphere = null!;
        private Hud _hud = null!;
        private CameraRig _rig = null!;
        private Camera _camera = null!;
        private float _nextCatchUp;
        private bool _changed;
        private IHealthSource? _healthSource;
        private HealthSync? _healthSync;
        private HealthStatus? _healthStatus;
        private CloudSaveSync? _cloud;

        private void Start()
        {
            var setup = System.Diagnostics.Stopwatch.StartNew();
            Application.targetFrameRate = 30;
            _camera = Camera.main != null ? Camera.main : new GameObject("Main Camera").AddComponent<Camera>();
            var sun = FindFirstObjectByType<Light>() ?? new GameObject("Sun").AddComponent<Light>();

            var flat = new Material(Shader.Find("Eirora/Flat"));
            var glow = new Material(Shader.Find("Eirora/Glow"));
            var sky = new Material(Shader.Find("Eirora/Sky"));
            _island = new IslandView(new GameObject("Island").transform, flat, glow, Looks.Load());
            StagedLook.Edited += LooksEdited;
            _atmosphere = new Atmosphere(_camera, sun, sky, glow);
            _atmosphere.Apply(LightMood.Dawn, Atmosphere.East, _camera);

            var document = gameObject.AddComponent<UIDocument>();
            document.panelSettings = PanelSettings();
            var introductions = new Introductions(Path.Combine(Application.persistentDataPath, Introductions.File));
            _hud = new Hud(document.rootVisualElement, command => _session.Apply(command), new HealthControls(this), introductions);
            _hud.SelectionChanged += hex => _island.Select(hex);

            _rig = new CameraRig(_camera, _hud.IsOverInterface);
            _rig.Tapped += point =>
            {
                // Every tile can be tapped: under the fog to lift it, otherwise to see what it is good for (D-79).
                var hex = HexLayout.FromWorld(point);
                if (_island.Map is null || !_island.Map.Tiles.ContainsKey(hex))
                {
                    _hud.Select(null);
                }
                else if (_hud.IsFogged(hex))
                {
                    _hud.SelectFog(hex);
                }
                else
                {
                    _hud.Select(hex);
                }
            };

            string? screenshots = Argument("-eirora-screenshots");
            if (screenshots is not null)
            {
                StartCoroutine(ScreenshotTour.Run(this, screenshots));
                return;
            }

            string? measure = Argument("-eirora-measure");
            if (measure is not null && MeasurementScene.TryBegin(this, measure, setup))
            {
                return;
            }

#if UNITY_ANDROID && !UNITY_EDITOR
            _healthSource = new AndroidHealthSource();
            _healthSync = new HealthSync(_healthSource);
#endif
            // Only offered where the build has a server and a provider; the world never waits for it (ACC-04).
            var session = new GameSession(new DeviceClock(), GameSession.DefaultSavePath);
            var account = DeviceAccounts.Create(AccountConfig.Load());
            _hud.UseAccount(account);
            if (account is not null)
            {
                var storage = new DeviceCloudSyncStorage(account.Store, Path.Combine(Application.persistentDataPath, DeviceCloudSyncStorage.StateFile));
                _cloud = new CloudSaveSync(account, session, storage, backup: DeviceAccounts.KeyBackup());
                _hud.UseCloudSync(_cloud, new WebCamQrScanner()); // The camera, for sending the key to a new phone (TASK-023b).
            }

            Begin(session);
            if (account is { IsSignedIn: true })
            {
                _ = _cloud!.ReconcileAsync();
            }

            SyncHealth();
        }

        /// <summary>Reads the health store now, as when the player opens the game (D-70).</summary>
        internal async void SyncHealth()
        {
            if (_healthSource is null)
            {
                return;
            }

            try
            {
                _healthStatus = await _healthSource.StatusAsync();
                await _session.SyncHealthAsync(_healthSync);
                Refresh();
            }
            catch (HealthSourceException)
            {
                _healthStatus = HealthStatus.Unavailable; // The player can always enter self-care by hand (HLT-12).
            }
        }

        /// <summary>Uses another health store, such as the fake one in the screenshot tour.</summary>
        internal void UseHealthSource(IHealthSource source)
        {
            _healthSource = source;
            _healthSync = new HealthSync(source);
            SyncHealth();
        }

        /// <summary>What the self-care sheet needs to connect Health Connect.</summary>
        private sealed class HealthControls : IHealthControls
        {
            private readonly ClientBootstrap _client;

            public HealthControls(ClientBootstrap client) => _client = client;

            public HealthStatus? Status => _client._healthStatus;

            public async void Connect(System.Action<int> done)
            {
                if (_client._healthSource is not { } source)
                {
                    done(0);
                    return;
                }

                try
                {
                    var granted = await source.RequestAccessAsync(new[] { Simulation.SelfCare.Habit.Steps, Simulation.SelfCare.Habit.Workouts, Simulation.SelfCare.Habit.Sleep });
                    _client.SyncHealth();
                    done(granted.Count);
                }
                catch (HealthSourceException)
                {
                    done(0);
                }
            }

            public void OpenSettings() => _client._healthSource?.OpenSettings();
        }

        /// <summary>Starts showing a session: the saved world, or the first-launch setup.</summary>
        internal void Begin(GameSession session)
        {
            _session = session;
            _session.Changed += () => _changed = true;
            if (_session.TryLoad())
            {
                Refresh();
            }
            else if (_session.LoadProblem is { } problem)
            {
                _hud.ShowUnreadableSave(problem, () =>
                {
                    _session.SetAsideSave();
                    _hud.ShowSetup(schedule => _session.Begin(schedule));
                });
            }
            else
            {
                _hud.ShowSetup(schedule =>
                {
                    _session.Begin(schedule);
                    SyncHealth();
                });
            }
        }

        internal GameSession Session => _session;

        internal Hud Hud => _hud;

        /// <summary>
        /// Draws the world now. A change to the world is otherwise drawn once at the end of the frame, however many
        /// commands and catch-ups it took: rebuilding the island for each of them held the first frame back (#227).
        /// </summary>
        internal void Refresh()
        {
            _changed = false;
            if (_session.World is { } world)
            {
                bool newMap = _island.Map?.WorldMap.Hash != world.Map.Hash;
                var diagnostics = IslandView.DiagnoseWhileAwake(world);
                _island.Refresh(world, diagnostics);
                if (newMap)
                {
                    _rig.Frame(_island.Map!, SettlementOf(world));
                }

                _hud.Show(world, _session.SinceDawn, diagnostics);
                _atmosphere.Apply(world, _camera);
            }
        }

        private void Update()
        {
            _rig.Update();
            if (_session?.World is not { } world)
            {
                return;
            }

            if (Time.unscaledTime >= _nextCatchUp)
            {
                _nextCatchUp = Time.unscaledTime + CatchUpSeconds;
                _session.CatchUp();
            }

            _atmosphere.Apply(world, _camera);
        }

        private void LateUpdate()
        {
            if (_changed)
            {
                Refresh();
            }
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused)
            {
                _session?.Save();
                _ = _cloud?.FlushAsync();
            }
            else
            {
                _session?.CatchUp();
                SyncHealth();
            }
        }

        private void OnApplicationQuit() => _session?.Save();

        private void OnDestroy()
        {
            StagedLook.Edited -= LooksEdited;
            _island?.Dispose();
        }

        /// <summary>A designer changed a look while playing in the editor: show it at once.</summary>
        private void LooksEdited()
        {
            if (_session?.World is not null)
            {
                Refresh();
            }
        }

        private static PanelSettings PanelSettings()
        {
            var settings = ScriptableObject.CreateInstance<PanelSettings>();
            settings.themeStyleSheet = Resources.Load<ThemeStyleSheet>("UI/EiroraTheme");
            settings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            settings.referenceResolution = new Vector2Int(1080, 2340);
            settings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            settings.match = 0f;
            return settings;
        }

        private static string? Argument(string name)
        {
            var args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, name);
            return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
        }

        internal IEnumerator Frames(int count)
        {
            for (int i = 0; i < count; i++)
            {
                yield return null;
            }
        }

        internal void Apply(GameCommand command) => _session.Apply(command);

        internal void Focus(Vector3 point) => _rig.Focus(point);

        internal void ZoomTo(int level) => _rig.ZoomTo(level);

        /// <summary>Where the world's first storehouse stands, the heart of the settlement.</summary>
        private static Vector3 SettlementOf(Simulation.World world)
        {
            foreach (var building in world.Buildings)
            {
                if (building.Kind == Simulation.Economy.BuildingKind.Storehouse)
                {
                    return building.Tile.Center();
                }
            }

            return world.Map.Start.Center();
        }
    }
}
