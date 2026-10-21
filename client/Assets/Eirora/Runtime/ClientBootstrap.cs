using System;
using System.Collections;
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
        private IHealthSource? _healthSource;
        private HealthSync? _healthSync;
        private HealthStatus? _healthStatus;

        private void Start()
        {
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
            _hud = new Hud(document.rootVisualElement, command => _session.Apply(command), new HealthControls(this));
            _hud.SelectionChanged += hex => _island.Select(hex);

            _rig = new CameraRig(_camera, _hud.IsOverInterface);
            _rig.Tapped += point =>
            {
                var hex = HexLayout.FromWorld(point);
                if (_island.Map.SiteAt(hex) is null && _island.Map.Tiles.ContainsKey(hex) && _hud.IsFogged(hex))
                {
                    _hud.SelectFog(hex);
                }
                else
                {
                    _hud.Select(_island.Map.SiteAt(hex));
                }
            };

            string? screenshots = Argument("-eirora-screenshots");
            if (screenshots is not null)
            {
                StartCoroutine(ScreenshotTour.Run(this, screenshots));
                return;
            }

            string? measure = Argument("-eirora-measure");
            if (measure is not null && MeasurementScene.TryBegin(this, measure))
            {
                return;
            }

#if UNITY_ANDROID && !UNITY_EDITOR
            _healthSource = new AndroidHealthSource();
            _healthSync = new HealthSync(_healthSource);
#endif
            // Only offered where the build has a server and a provider; the world never waits for it (ACC-04).
            _hud.UseAccount(DeviceAccounts.Create(AccountConfig.Load()));
            Begin(new GameSession(new DeviceClock(), GameSession.DefaultSavePath));
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
            _session.Changed += Refresh;
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

        internal void Refresh()
        {
            if (_session.World is { } world)
            {
                _island.Refresh(world);
                _hud.Show(world, _session.SinceDawn);
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

        private void OnApplicationPause(bool paused)
        {
            if (paused)
            {
                _session?.Save();
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
    }
}
