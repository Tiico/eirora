using System.Collections.Generic;
using UnityEngine;

namespace Eirora.Spike.HealthConnect
{
    /// <summary>
    /// IMGUI styles for the spike, scaled to density-independent pixels. Colours come from the night palette and the
    /// forces' colours in docs/design/art-direction.md, so the habits look like the forces they become.
    /// </summary>
    public sealed class SpikeStyles
    {
        public static readonly Color Background = Hex("#111737");
        public static readonly Color Card = Hex("#1c2452");
        public static readonly Color CardRaised = Hex("#283166");
        public static readonly Color Text = Hex("#e8eeff");
        public static readonly Color Muted = Hex("#a9b3d6");
        public static readonly Color Accent = Hex("#86bfc2");
        public static readonly Color Good = Hex("#9db58a");
        public static readonly Color Warning = Hex("#d9a441");
        public static readonly Color Error = Hex("#ef916f");

        private readonly Dictionary<Habit, GUIStyle> _habitButtons = new Dictionary<Habit, GUIStyle>();
        private readonly Dictionary<Habit, GUIStyle> _habitHeadings = new Dictionary<Habit, GUIStyle>();

        public SpikeStyles(float dp)
        {
            Dp = dp;
            BackgroundTexture = Solid(Background);
            Title = Label(22, FontStyle.Bold, Text);
            Heading = Label(17, FontStyle.Bold, Text);
            Body = Label(15, FontStyle.Normal, Text);
            Small = Label(13, FontStyle.Normal, Muted);
            BodyMuted = Label(15, FontStyle.Normal, Muted);
            GoodText = Label(15, FontStyle.Bold, Good);
            WarningText = Label(15, FontStyle.Bold, Warning);
            ErrorText = Label(15, FontStyle.Bold, Error);
            CardStyle = Box(Card, 14, 10);
            RaisedCardStyle = Box(CardRaised, 12, 8);
            Button = ButtonStyle(Accent, Background);
            SecondaryButton = ButtonStyle(CardRaised, Text);
            Tab = TabStyle(Card, Muted);
            SelectedTab = TabStyle(CardRaised, Text);
            foreach (var habit in new[] { Habit.Steps, Habit.Exercise, Habit.Sleep })
            {
                _habitButtons[habit] = ButtonStyle(HabitColor(habit), Background);
                _habitHeadings[habit] = Label(17, FontStyle.Bold, HabitColor(habit));
            }
        }

        public float Dp { get; }

        public Texture2D BackgroundTexture { get; }

        public GUIStyle Title { get; }

        public GUIStyle Heading { get; }

        public GUIStyle Body { get; }

        public GUIStyle Small { get; }

        public GUIStyle BodyMuted { get; }

        public GUIStyle GoodText { get; }

        public GUIStyle WarningText { get; }

        public GUIStyle ErrorText { get; }

        public GUIStyle CardStyle { get; }

        public GUIStyle RaisedCardStyle { get; }

        public GUIStyle Button { get; }

        public GUIStyle SecondaryButton { get; }

        public GUIStyle Tab { get; }

        public GUIStyle SelectedTab { get; }

        public static Color HabitColor(Habit habit) => habit switch
        {
            Habit.Steps => Hex("#4ba3b0"),
            Habit.Exercise => Hex("#d08a4e"),
            _ => Hex("#f2a0a8"),
        };

        public int Px(float dpValue) => Mathf.RoundToInt(dpValue * Dp);

        /// <summary>A filled button in a habit's colour.</summary>
        public GUIStyle HabitButton(Habit habit) => _habitButtons[habit];

        /// <summary>A heading in a habit's colour.</summary>
        public GUIStyle HabitHeading(Habit habit) => _habitHeadings[habit];

        private static Color Hex(string hex) => ColorUtility.TryParseHtmlString(hex, out var color) ? color : Color.magenta;

        private static Texture2D Solid(Color color)
        {
            var texture = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
            texture.SetPixel(0, 0, color);
            texture.Apply();
            return texture;
        }

        private GUIStyle Label(float size, FontStyle fontStyle, Color color)
        {
            var style = new GUIStyle(GUI.skin.label) { fontSize = Px(size), fontStyle = fontStyle, wordWrap = true, richText = false };
            style.normal.textColor = color;
            style.margin = new RectOffset(0, 0, Px(2), Px(2));
            style.padding = new RectOffset(0, 0, 0, 0);
            return style;
        }

        private GUIStyle Box(Color color, float padding, float marginBottom)
        {
            var radius = Px(10);
            var style = new GUIStyle
            {
                padding = new RectOffset(Px(padding), Px(padding), Px(padding), Px(padding)),
                margin = new RectOffset(0, 0, 0, Px(marginBottom)),
                border = new RectOffset(radius, radius, radius, radius),
            };
            style.normal.background = Rounded(color, radius);
            return style;
        }

        private GUIStyle ButtonStyle(Color fill, Color text)
        {
            var radius = Px(10);
            var pressed = Color.Lerp(fill, Color.black, 0.2f);
            var style = new GUIStyle
            {
                fontSize = Px(15),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true,
                padding = new RectOffset(Px(14), Px(14), Px(12), Px(12)),
                margin = new RectOffset(0, 0, Px(4), Px(4)),
                border = new RectOffset(radius, radius, radius, radius),
                fixedHeight = 0,
                stretchWidth = true,
            };
            style.normal.background = Rounded(fill, radius);
            style.hover.background = style.normal.background;
            style.active.background = Rounded(pressed, radius);
            style.normal.textColor = text;
            style.hover.textColor = text;
            style.active.textColor = text;
            return style;
        }

        private GUIStyle TabStyle(Color fill, Color text)
        {
            var style = ButtonStyle(fill, text);
            style.fontSize = Px(13);
            style.padding = new RectOffset(Px(4), Px(4), Px(10), Px(10));
            return style;
        }

        /// <summary>A nine-sliced rounded rectangle with antialiased corners.</summary>
        private static Texture2D Rounded(Color color, int radius)
        {
            var size = (radius * 2) + 2;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave, wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color[size * size];
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var dx = Mathf.Max(0f, Mathf.Abs(x + 0.5f - (size / 2f)) - ((size / 2f) - radius));
                    var dy = Mathf.Max(0f, Mathf.Abs(y + 0.5f - (size / 2f)) - ((size / 2f) - radius));
                    var coverage = Mathf.Clamp01(radius - Mathf.Sqrt((dx * dx) + (dy * dy)) + 0.5f);
                    pixels[(y * size) + x] = new Color(color.r, color.g, color.b, color.a * coverage);
                }
            }

            texture.SetPixels(pixels);
            texture.Apply();
            return texture;
        }
    }
}
