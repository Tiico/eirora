using Eirora.Simulation;
using Eirora.Simulation.Time;
using UnityEngine;

namespace Eirora.Client.Look
{
    /// <summary>
    /// Sun, sky, fog and night lights. The light follows the player's game day, not the local sun
    /// (art-direction.md, section 2): night during rest, dawn for an hour after it, dusk for an hour before it.
    /// </summary>
    public sealed class Atmosphere
    {
        private static readonly int s_skyLight = Shader.PropertyToID("_EiroraSkyLight");
        private static readonly int s_groundLight = Shader.PropertyToID("_EiroraGroundLight");
        private static readonly int s_glow = Shader.PropertyToID("_EiroraGlow");

        private readonly Light _sun;
        private readonly Mesh _sky;
        private readonly MeshRenderer _stars;
        private readonly MeshRenderer _aurora;
        private readonly Material _nightGlow;

        public Atmosphere(Camera camera, Light sun, Material skyMaterial, Material glowMaterial)
        {
            _sun = sun;
            _sun.type = LightType.Directional;
            _sun.shadows = LightShadows.Soft;
            _sun.shadowStrength = 0.55f;

            // The sky is a gradient quad at the back of the camera's view, with stars and aurora in front of it.
            _sky = new Mesh { name = "Sky" };
            var sky = Attach(camera, "Sky", skyMaterial, 0.98f);
            sky.GetComponent<MeshFilter>().sharedMesh = _sky;

            _nightGlow = new Material(glowMaterial);
            _stars = Attach(camera, "Stars", _nightGlow, 0.97f);
            _stars.GetComponent<MeshFilter>().sharedMesh = Stars();
            _aurora = Attach(camera, "Aurora", _nightGlow, 0.965f);
            _aurora.GetComponent<MeshFilter>().sharedMesh = Aurora();

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = 55f;
            RenderSettings.fogEndDistance = 110f;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        }

        public LightMood Mood { get; private set; } = LightMood.Day;

        public void Apply(World world, Camera camera)
        {
            var (mood, azimuth) = MoodOf(world);
            Apply(mood, azimuth, camera);
        }

        /// <summary>Applies a mood directly, such as daylight on the first-launch screen before a world exists.</summary>
        public void Apply(LightMood mood, float azimuth, Camera camera)
        {
            Mood = mood;

            // The diorama is lit from at least 30° so flat hex tops keep their colour at dawn and dusk, while the low
            // mood still shows in the sky, the light's colour and a stronger fill from the sky (art-direction.md).
            float lowSun = 1f - Mathf.InverseLerp(12f, 55f, Mood.Elevation);
            _sun.transform.rotation = Quaternion.Euler(Mathf.Max(Mood.Elevation, 30f), azimuth, 0f);
            _sun.color = Mood.Sun;
            _sun.intensity = Mathf.Lerp(Mathf.Lerp(1.15f, 1.3f, lowSun), 0.32f, Mood.Darkness);

            float fill = Mathf.Lerp(0.55f, 0.8f, lowSun * (1f - Mood.Darkness));
            Shader.SetGlobalVector(s_skyLight, (Vector4)(Mood.SkyLight.linear * Mathf.Lerp(fill, 0.42f, Mood.Darkness)));
            Shader.SetGlobalVector(s_groundLight, (Vector4)(Color.Lerp(Palette.Grass, Mood.SkyLight, 0.6f).linear * 0.28f));
            Shader.SetGlobalFloat(s_glow, Mathf.Lerp(0f, 1.6f, Mathf.Clamp01((Mood.Darkness - 0.15f) / 0.85f)));
            _nightGlow.SetFloat(s_glow, Mood.Darkness * 0.85f);

            RenderSettings.fogColor = Mood.Horizon;
            camera.backgroundColor = Mood.Horizon;
            Gradient(Mood.Zenith, Mood.Horizon);
            _stars.enabled = Mood.Darkness > 0.05f;
            _aurora.enabled = Mood.Darkness > 0.05f;
        }

        /// <summary>
        /// The mood for the world's current moment of its game day, and the sun's compass direction: east at dawn,
        /// south by day and west at dusk, with the camera looking north.
        /// </summary>
        public static (LightMood Mood, float Azimuth) MoodOf(World world)
        {
            const float East = 110f, South = 170f, West = 250f, Moon = 200f;
            if (world.Phase == Phase.Resting)
            {
                return (LightMood.Night, Moon);
            }

            var schedule = world.RestSchedule;
            int local = (int)((((world.Now.LocalSeconds / 60) % 1440) + 1440) % 1440);
            int restEnd = (schedule.StartMinuteOfDay + schedule.DurationMinutes) % 1440;
            int sinceDawn = (local - restEnd + 1440) % 1440;
            int untilRest = (schedule.StartMinuteOfDay - local + 1440) % 1440;

            if (untilRest < 90)
            {
                float t = Mathf.Clamp01((untilRest - 60) / 30f);
                return (LightMood.Lerp(LightMood.Dusk, LightMood.Day, t), Mathf.Lerp(West, South, t));
            }

            if (sinceDawn < 90)
            {
                float t = Mathf.Clamp01((sinceDawn - 60) / 30f);
                return (LightMood.Lerp(LightMood.Dawn, LightMood.Day, t), Mathf.Lerp(East, South, t));
            }

            return (LightMood.Day, South);
        }

        private void Gradient(Color zenith, Color horizon)
        {
            var z = zenith.linear;
            var h = horizon.linear;
            _sky.SetVertices(new[] { new Vector3(-1, -1, 0), new Vector3(-1, 1, 0), new Vector3(1, 1, 0), new Vector3(1, -1, 0), new Vector3(-1, 0.1f, 0), new Vector3(1, 0.1f, 0) });
            _sky.SetColors(new[] { h, z, z, h, h, h });
            _sky.SetTriangles(new[] { 4, 1, 2, 4, 2, 5, 0, 4, 5, 0, 5, 3 }, 0);
            _sky.bounds = new Bounds(Vector3.zero, Vector3.one * 1000f);
        }

        private static MeshRenderer Attach(Camera camera, string name, Material material, float depth)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(camera.transform, false);
            float distance = camera.farClipPlane * depth;
            float height = 2f * distance * Mathf.Tan(camera.fieldOfView * 0.5f * Mathf.Deg2Rad) * 1.1f;
            go.transform.localPosition = new Vector3(0f, 0f, distance);
            go.transform.localScale = new Vector3(height * 1.2f, height / 2f, 1f);
            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return renderer;
        }

        private static Mesh Stars()
        {
            var mesh = new MeshBuilder();
            var random = new System.Random(61);
            for (int i = 0; i < 90; i++)
            {
                var p = new Vector3(((float)random.NextDouble() * 2f) - 1f, 0.15f + ((float)random.NextDouble() * 0.85f), 0f);
                float s = 0.0025f + ((float)random.NextDouble() * 0.004f);
                var color = Color.Lerp(Color.white, Palette.AuroraViolet, (float)random.NextDouble() * 0.3f) * (0.35f + (0.65f * (float)random.NextDouble()));
                mesh.Polygon(color, p + new Vector3(-s, 0, 0), p + new Vector3(0, s * 2f, 0), p + new Vector3(s, 0, 0), p + new Vector3(0, -s * 2f, 0));
            }

            return mesh.ToMesh("Stars");
        }

        /// <summary>A dimmed aurora close to the horizon that barely moves (D-61, art-direction.md, section 11).</summary>
        private static Mesh Aurora()
        {
            var mesh = new MeshBuilder();
            const int Steps = 24;
            for (int band = 0; band < 2; band++)
            {
                float baseY = 0.16f + (band * 0.09f);
                var color = band == 0 ? Palette.AuroraGreen * 0.35f : Palette.AuroraViolet * 0.22f;
                for (int i = 0; i < Steps; i++)
                {
                    float x0 = -1f + (2f * i / Steps), x1 = -1f + (2f * (i + 1) / Steps);
                    float y0 = baseY + (Mathf.Sin((x0 * 5f) + band) * 0.025f), y1 = baseY + (Mathf.Sin((x1 * 5f) + band) * 0.025f);
                    var fade = color * Mathf.Sin(Mathf.PI * (i + 0.5f) / Steps);
                    mesh.Polygon(fade, new Vector3(x0, y0, 0), new Vector3(x0, y0 + 0.08f, 0), new Vector3(x1, y1 + 0.08f, 0), new Vector3(x1, y1, 0));
                }
            }

            return mesh.ToMesh("Aurora");
        }
    }
}
