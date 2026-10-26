using Eirora.Client.Look;
using Eirora.Simulation.Economy;
using UnityEngine;
using UnityEngine.UIElements;

namespace Eirora.Client.UI
{
    /// <summary>The interface's signs for what is not a force or a building (ui-prototypes.html, direction C).</summary>
    public enum GlyphKind
    {
        /// <summary>Self-care: a sprout, as the slogan has the world growing.</summary>
        Care,
        /// <summary>The building queue and work.</summary>
        Hammer,
        /// <summary>The daily report.</summary>
        Report,
        /// <summary>The account: a person, head and shoulders.</summary>
        Account,
        /// <summary>A villager: head and coat.</summary>
        Villager,
        /// <summary>A carrier: a villager with a sack.</summary>
        Carrier,
        /// <summary>Time: a clock face.</summary>
        Clock,
        /// <summary>The rest: a crescent moon.</summary>
        Moon,
        /// <summary>The dawn: a sun with rays.</summary>
        Sun,
        /// <summary>A road: a track with a dashed middle.</summary>
        Road,
        /// <summary>An upgrade: a house with an arrow up.</summary>
        Upgrade,
        /// <summary>A pause: two bars.</summary>
        Pause,
        /// <summary>Resuming: a triangle.</summary>
        Resume,
        /// <summary>Everything else: three dots.</summary>
        More,
        /// <summary>An expedition: a boat with a sail.</summary>
        Boat,
        /// <summary>Moving first: an arrow up to a line.</summary>
        First,
        /// <summary>A house, where a building has no picture.</summary>
        House,
    }

    /// <summary>
    /// A sign drawn in code, so that it scales with the panel and takes the colour it is given; the forces keep their own
    /// shapes in <see cref="Hud"/> and a building is shown as itself (<see cref="Pictograms"/>).
    /// </summary>
    public sealed class Glyph : VisualElement
    {
        private readonly GlyphKind _kind;
        private readonly Color _colour;

        public Glyph(GlyphKind kind, float size = 48f, Color? colour = null)
        {
            _kind = kind;
            _colour = colour ?? Palette.Text;
            AddToClassList("glyph");
            style.width = size;
            style.height = size;
            pickingMode = PickingMode.Ignore;
            generateVisualContent += Draw;
        }

        public GlyphKind Kind => _kind;

        private void Draw(MeshGenerationContext context)
        {
            var p = context.painter2D;
            float s = Mathf.Min(contentRect.width, contentRect.height) / 24f;
            Vector2 At(float x, float y) => new(x * s, y * s);
            p.fillColor = _colour;
            p.strokeColor = _colour;
            p.lineCap = LineCap.Round;
            p.lineJoin = LineJoin.Round;
            p.lineWidth = 2.2f * s;
            switch (_kind)
            {
                case GlyphKind.Care:
                    Line(p, At(12f, 23f), At(12f, 11f));
                    Fill(p, At(12f, 12f), At(10f, 7f), At(6f, 4f), At(2f, 3f), At(3f, 7f), At(6f, 10.5f), At(10f, 12f));
                    Fill(p, At(12f, 15f), At(13.5f, 10.5f), At(17f, 8f), At(21f, 7f), At(20f, 11f), At(17f, 14f), At(13.5f, 15f));
                    break;
                case GlyphKind.Hammer:
                    Fill(p, At(3f, 20.5f), At(12.5f, 11f), At(15.3f, 13.8f), At(5.8f, 23.3f), At(3.5f, 23.3f), At(2.3f, 22f));
                    Fill(p, At(11f, 5f), At(15f, 2f), At(22f, 9f), At(19f, 13f), At(17f, 11f), At(15.5f, 12.5f), At(11.5f, 8.5f), At(13f, 7f));
                    break;
                case GlyphKind.Report:
                    Fill(p, At(4f, 3f), At(16f, 3f), At(20f, 7f), At(20f, 21f), At(4f, 21f));
                    p.strokeColor = Palette.Panel;
                    p.lineWidth = 1.8f * s;
                    Line(p, At(7f, 11f), At(17f, 11f));
                    Line(p, At(7f, 15f), At(17f, 15f));
                    Line(p, At(7f, 7f), At(12f, 7f));
                    break;
                case GlyphKind.Account:
                    Disc(p, At(12f, 8f), 4.5f * s);
                    p.BeginPath();
                    p.MoveTo(At(3f, 22f));
                    p.Arc(At(12f, 22f), 9f * s, Angle.Degrees(180f), Angle.Degrees(360f));
                    p.ClosePath();
                    p.Fill();
                    break;
                case GlyphKind.Villager:
                    Disc(p, At(12f, 5f), 3.4f * s);
                    Fill(p, At(12f, 9.5f), At(15.5f, 12f), At(18f, 22.5f), At(6f, 22.5f), At(8.5f, 12f));
                    break;
                case GlyphKind.Carrier:
                    Disc(p, At(9f, 5f), 3f * s);
                    Fill(p, At(9f, 9f), At(12f, 11.5f), At(14f, 22f), At(4f, 22f), At(6f, 11.5f));
                    Fill(p, At(13f, 10f), At(22f, 10f), At(22f, 18f), At(13f, 18f));
                    break;
                case GlyphKind.Clock:
                    Disc(p, At(12f, 12f), 10f * s);
                    p.strokeColor = Palette.Panel;
                    p.lineWidth = 2f * s;
                    Line(p, At(12f, 6f), At(12f, 12f));
                    Line(p, At(12f, 12f), At(16f, 14.5f));
                    break;
                case GlyphKind.Moon:
                    p.BeginPath();
                    p.Arc(At(12f, 12f), 10f * s, Angle.Degrees(50f), Angle.Degrees(310f));
                    p.Arc(At(15.5f, 12f), 8f * s, Angle.Degrees(295f), Angle.Degrees(65f), ArcDirection.CounterClockwise);
                    p.ClosePath();
                    p.Fill();
                    break;
                case GlyphKind.Sun:
                    Disc(p, At(12f, 12f), 5f * s);
                    for (int i = 0; i < 8; i++)
                    {
                        float a = i * Mathf.PI / 4f;
                        Line(p, At(12f + (Mathf.Cos(a) * 8f), 12f + (Mathf.Sin(a) * 8f)), At(12f + (Mathf.Cos(a) * 11f), 12f + (Mathf.Sin(a) * 11f)));
                    }

                    break;
                case GlyphKind.Road:
                    Fill(p, At(2f, 22f), At(8f, 2f), At(10f, 2f), At(6f, 22f));
                    Fill(p, At(22f, 22f), At(16f, 2f), At(14f, 2f), At(18f, 22f));
                    Line(p, At(12f, 4f), At(12f, 7f));
                    Line(p, At(12f, 10.5f), At(12f, 13.5f));
                    Line(p, At(12f, 17f), At(12f, 20f));
                    break;
                case GlyphKind.Upgrade:
                    p.fillColor = new Color(_colour.r, _colour.g, _colour.b, 0.3f);
                    Fill(p, At(12f, 1.5f), At(22.5f, 11f), At(19.7f, 11f), At(19.7f, 22.5f), At(4.3f, 22.5f), At(4.3f, 11f), At(1.5f, 11f));
                    p.fillColor = _colour;
                    Fill(p, At(12f, 8f), At(17f, 13f), At(14f, 13f), At(14f, 19f), At(10f, 19f), At(10f, 13f), At(7f, 13f));
                    break;
                case GlyphKind.Pause:
                    Fill(p, At(5f, 3f), At(10f, 3f), At(10f, 21f), At(5f, 21f));
                    Fill(p, At(14f, 3f), At(19f, 3f), At(19f, 21f), At(14f, 21f));
                    break;
                case GlyphKind.Resume:
                    Fill(p, At(6f, 3f), At(20f, 12f), At(6f, 21f));
                    break;
                case GlyphKind.More:
                    Disc(p, At(5f, 12f), 2.4f * s);
                    Disc(p, At(12f, 12f), 2.4f * s);
                    Disc(p, At(19f, 12f), 2.4f * s);
                    break;
                case GlyphKind.Boat:
                    Fill(p, At(2f, 15f), At(22f, 15f), At(19f, 21f), At(5f, 21f));
                    Fill(p, At(12f, 2f), At(12f, 13f), At(4f, 13f));
                    Fill(p, At(13.5f, 4f), At(20f, 13f), At(13.5f, 13f));
                    break;
                case GlyphKind.First:
                    Fill(p, At(4f, 3f), At(20f, 3f), At(20f, 6f), At(4f, 6f));
                    Fill(p, At(12f, 8f), At(19f, 15f), At(15f, 15f), At(15f, 21f), At(9f, 21f), At(9f, 15f), At(5f, 15f));
                    break;
                default:
                    Fill(p, At(12f, 2f), At(23f, 12f), At(20f, 12f), At(20f, 22f), At(4f, 22f), At(4f, 12f), At(1f, 12f));
                    p.fillColor = Palette.Panel;
                    Fill(p, At(10f, 15f), At(14f, 15f), At(14f, 22f), At(10f, 22f));
                    break;
            }
        }

        internal static void Fill(Painter2D p, params Vector2[] points)
        {
            p.BeginPath();
            p.MoveTo(points[0]);
            for (int i = 1; i < points.Length; i++)
            {
                p.LineTo(points[i]);
            }

            p.ClosePath();
            p.Fill();
        }

        internal static void Line(Painter2D p, Vector2 from, Vector2 to)
        {
            p.BeginPath();
            p.MoveTo(from);
            p.LineTo(to);
            p.Stroke();
        }

        internal static void Disc(Painter2D p, Vector2 centre, float radius)
        {
            p.BeginPath();
            p.Arc(centre, radius, Angle.Degrees(0f), Angle.Degrees(360f));
            p.ClosePath();
            p.Fill();
        }
    }

    /// <summary>
    /// A good's own shape, so that a stock or a cost is read without its name (UX-05): a fish, an ear of grain, a log, a
    /// stack of planks, a rock, a fleece and so on, one per <see cref="Resource"/>.
    /// </summary>
    public sealed class ResourceIcon : VisualElement
    {
        private readonly Resource _resource;
        private readonly Color _colour;

        public ResourceIcon(Resource resource, float size = 40f, Color? colour = null)
        {
            _resource = resource;
            _colour = colour ?? Palette.Text;
            name = resource.ToString();
            AddToClassList("resource-icon");
            style.width = size;
            style.height = size;
            pickingMode = PickingMode.Ignore;
            generateVisualContent += Draw;
        }

        public Resource Resource => _resource;

        private void Draw(MeshGenerationContext context)
        {
            var p = context.painter2D;
            float s = Mathf.Min(contentRect.width, contentRect.height) / 24f;
            Vector2 At(float x, float y) => new(x * s, y * s);
            p.fillColor = _colour;
            p.strokeColor = _colour;
            p.lineCap = LineCap.Round;
            p.lineJoin = LineJoin.Round;
            p.lineWidth = 2.2f * s;
            var light = Palette.Panel;
            switch (_resource)
            {
                case Resource.Fish:
                    p.BeginPath();
                    p.MoveTo(At(2f, 12f));
                    p.BezierCurveTo(At(6f, 5f), At(14f, 5f), At(17f, 10.5f));
                    p.LineTo(At(22f, 7f));
                    p.LineTo(At(22f, 17f));
                    p.LineTo(At(17f, 13.5f));
                    p.BezierCurveTo(At(14f, 19f), At(6f, 19f), At(2f, 12f));
                    p.ClosePath();
                    p.Fill();
                    p.fillColor = light;
                    Glyph.Disc(p, At(6.5f, 11f), 1.3f * s);
                    break;
                case Resource.Grain:
                    Glyph.Line(p, At(11f, 23f), At(11f, 9f));
                    Ear(p, At(11f, 4f), 0f, s);
                    Ear(p, At(6.5f, 9f), -35f, s);
                    Ear(p, At(15.5f, 9f), 35f, s);
                    Ear(p, At(6f, 15f), -35f, s);
                    Ear(p, At(16f, 15f), 35f, s);
                    break;
                case Resource.Timber:
                    Glyph.Fill(p, At(3f, 7f), At(17f, 7f), At(17f, 17f), At(3f, 17f));
                    Glyph.Disc(p, At(17f, 12f), 5f * s);
                    p.fillColor = Color.Lerp(light, _colour, 0.25f);
                    Glyph.Disc(p, At(17f, 12f), 3.6f * s);
                    p.fillColor = _colour;
                    Glyph.Disc(p, At(17f, 12f), 1.6f * s);
                    break;
                case Resource.Planks:
                    for (int i = 0; i < 3; i++)
                    {
                        float y = 3f + (i * 6.8f);
                        Glyph.Fill(p, At(2f, y), At(22f, y), At(22f, y + 4.4f), At(2f, y + 4.4f));
                    }

                    break;
                case Resource.Stone:
                    Glyph.Fill(p, At(4f, 20f), At(2f, 12f), At(8f, 4f), At(17f, 5f), At(22f, 12f), At(19f, 20f));
                    p.strokeColor = new Color(light.r, light.g, light.b, 0.5f);
                    p.lineWidth = 1f * s;
                    Glyph.Line(p, At(8f, 4f), At(10f, 12f));
                    Glyph.Line(p, At(10f, 12f), At(4f, 12f));
                    Glyph.Line(p, At(10f, 12f), At(17f, 5f));
                    Glyph.Line(p, At(10f, 12f), At(17f, 20f));
                    break;
                case Resource.Wool:
                    p.BeginPath();
                    p.Arc(At(7f, 15f), 4f * s, Angle.Degrees(90f), Angle.Degrees(270f));
                    p.Arc(At(10f, 10f), 4.5f * s, Angle.Degrees(200f), Angle.Degrees(320f));
                    p.Arc(At(15.5f, 10.5f), 4f * s, Angle.Degrees(220f), Angle.Degrees(340f));
                    p.Arc(At(18f, 15f), 4f * s, Angle.Degrees(270f), Angle.Degrees(90f));
                    p.ClosePath();
                    p.Fill();
                    break;
                case Resource.Wadmal:
                    Glyph.Fill(p, At(3f, 5f), At(21f, 5f), At(21f, 19f), At(3f, 19f));
                    p.strokeColor = light;
                    p.lineWidth = 1.5f * s;
                    Glyph.Line(p, At(3f, 10f), At(21f, 10f));
                    Glyph.Line(p, At(3f, 14f), At(21f, 14f));
                    Glyph.Line(p, At(8f, 5f), At(8f, 19f));
                    Glyph.Line(p, At(16f, 5f), At(16f, 19f));
                    break;
                case Resource.Game:
                    // A hare: body, head and two long ears.
                    p.BeginPath();
                    p.Arc(At(11f, 15f), 6.5f * s, Angle.Degrees(0f), Angle.Degrees(360f));
                    p.ClosePath();
                    p.Fill();
                    Glyph.Disc(p, At(18f, 11f), 3.2f * s);
                    Glyph.Fill(p, At(16.5f, 9f), At(15f, 1.5f), At(18.5f, 8f));
                    Glyph.Fill(p, At(19f, 8.5f), At(20.5f, 1.5f), At(21.5f, 8.5f));
                    break;
                case Resource.Hide:
                    Glyph.Fill(p, At(4f, 4f), At(8f, 7f), At(16f, 7f), At(20f, 4f), At(21f, 9f), At(18f, 12f), At(21f, 16f), At(20f, 21f), At(16f, 18f), At(8f, 18f), At(4f, 21f), At(3f, 16f), At(6f, 12f), At(3f, 9f));
                    break;
                case Resource.Honey:
                    Drop(p, At(12f, 12f), s);
                    break;
                case Resource.Mead:
                    // A drinking horn.
                    p.BeginPath();
                    p.MoveTo(At(3f, 6f));
                    p.LineTo(At(13f, 4f));
                    p.BezierCurveTo(At(22f, 6f), At(23f, 14f), At(18f, 21f));
                    p.BezierCurveTo(At(17f, 15f), At(12f, 11f), At(3f, 10f));
                    p.ClosePath();
                    p.Fill();
                    break;
                case Resource.Ale:
                    Glyph.Fill(p, At(4f, 7f), At(16f, 7f), At(16f, 21f), At(4f, 21f));
                    p.lineWidth = 2.5f * s;
                    p.BeginPath();
                    p.MoveTo(At(16f, 10f));
                    p.BezierCurveTo(At(22f, 10f), At(22f, 18f), At(16f, 18f));
                    p.Stroke();
                    p.fillColor = light;
                    Glyph.Disc(p, At(7f, 6.5f), 2.2f * s);
                    Glyph.Disc(p, At(11f, 5f), 2.6f * s);
                    Glyph.Disc(p, At(14.5f, 6.5f), 2f * s);
                    break;
                case Resource.Tar:
                    Drop(p, At(12f, 12f), s);
                    p.fillColor = light;
                    Glyph.Disc(p, At(9.5f, 14f), 1.4f * s);
                    break;
                default:
                    Glyph.Disc(p, At(12f, 12f), 8f * s);
                    break;
            }
        }

        private static void Ear(Painter2D p, Vector2 centre, float degrees, float s)
        {
            float a = degrees * Mathf.Deg2Rad;
            var up = new Vector2(Mathf.Sin(a), -Mathf.Cos(a)) * 3.4f * s;
            var side = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 2.4f * s;
            Glyph.Fill(p, centre + up, centre + side, centre - up, centre - side);
        }

        private static void Drop(Painter2D p, Vector2 c, float s)
        {
            p.BeginPath();
            p.MoveTo(c + new Vector2(0f, -10f * s));
            p.BezierCurveTo(c + new Vector2(2f * s, -4f * s), c + new Vector2(8f * s, 0f), c + new Vector2(8f * s, 4f * s));
            p.Arc(c + new Vector2(0f, 4f * s), 8f * s, Angle.Degrees(0f), Angle.Degrees(180f));
            p.BezierCurveTo(c + new Vector2(-8f * s, 0f), c + new Vector2(-2f * s, -4f * s), c + new Vector2(0f, -10f * s));
            p.ClosePath();
            p.Fill();
        }
    }
}
