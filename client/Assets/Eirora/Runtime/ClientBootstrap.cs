using System;
using System.Collections;
using Eirora.Client.App;
using Eirora.Client.Look;
using Eirora.Client.UI;
using Eirora.Simulation.Commands;
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

        private void Start()
        {
            Application.targetFrameRate = 30;
            _camera = Camera.main != null ? Camera.main : new GameObject("Main Camera").AddComponent<Camera>();
            var sun = FindFirstObjectByType<Light>() ?? new GameObject("Sun").AddComponent<Light>();

            var flat = new Material(Shader.Find("Eirora/Flat"));
            var glow = new Material(Shader.Find("Eirora/Glow"));
            var sky = new Material(Shader.Find("Eirora/Sky"));
            _island = new IslandView(new GameObject("Island").transform, flat, glow);
            _atmosphere = new Atmosphere(_camera, sun, sky, glow);
            _atmosphere.Apply(LightMood.Dawn, 110f, _camera);

            var document = gameObject.AddComponent<UIDocument>();
            document.panelSettings = PanelSettings();
            _hud = new Hud(document.rootVisualElement, command => _session.Apply(command));
            _hud.SelectionChanged += site => _island.Select(site is null ? null : IslandMap.HexOf(site));

            _rig = new CameraRig(_camera, _hud.IsOverInterface);
            _rig.Tapped += point => _hud.Select(_island.Map.SiteAt(Hex.FromWorld(point)));

            string? screenshots = Argument("-eirora-screenshots");
            if (screenshots is not null)
            {
                StartCoroutine(ScreenshotTour.Run(this, screenshots));
                return;
            }

            Begin(new GameSession(new DeviceClock(), GameSession.DefaultSavePath));
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
            else if (_session.LoadProblem is not null)
            {
                _hud.ShowUnreadableSave(() =>
                {
                    _session.SetAsideSave();
                    _hud.ShowSetup(schedule => _session.Begin(schedule));
                });
            }
            else
            {
                _hud.ShowSetup(schedule => _session.Begin(schedule));
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
            }
        }

        private void OnApplicationQuit() => _session?.Save();

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
