using UnityEngine;

namespace Statecraft.Player
{
    /// <summary>
    /// Minimal IMGUI drawing helpers.
    ///
    /// The prototype's whole interface is IMGUI on purpose: it needs no Canvas, no
    /// prefabs, no TextMeshPro import, and no scene wiring, so the repo runs the moment
    /// it is cloned. It is also genuinely good for a data-dense strategy HUD while the
    /// numbers are still moving every day. Port to UI Toolkit when the layout settles —
    /// see docs/ROADMAP.md.
    /// </summary>
    public static class HudPaint
    {
        static Texture2D _px;

        public static Texture2D Pixel
        {
            get
            {
                if (_px == null)
                {
                    _px = new Texture2D(1, 1);
                    _px.SetPixel(0, 0, Color.white);
                    _px.Apply();
                    _px.hideFlags = HideFlags.HideAndDontSave;
                }
                return _px;
            }
        }

        public static void Fill(Rect r, Color c)
        {
            var old = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, Pixel);
            GUI.color = old;
        }

        public static void Frame(Rect r, Color c, float t = 1f)
        {
            Fill(new Rect(r.x, r.y, r.width, t), c);
            Fill(new Rect(r.x, r.yMax - t, r.width, t), c);
            Fill(new Rect(r.x, r.y, t, r.height), c);
            Fill(new Rect(r.xMax - t, r.y, t, r.height), c);
        }

        public static void Panel(Rect r)
        {
            Fill(r, new Color(0.06f, 0.07f, 0.09f, 0.90f));
            Frame(r, new Color(0.35f, 0.40f, 0.48f, 0.55f));
        }

        public static void Bar(Rect r, float fill01, Color tint)
        {
            Fill(r, new Color(0.14f, 0.15f, 0.18f, 0.9f));
            var inner = new Rect(r.x + 1f, r.y + 1f, (r.width - 2f) * Mathf.Clamp01(fill01), r.height - 2f);
            Fill(inner, tint);
        }

        /// <summary>A tiny price history line. Cheap, readable, no charting library.</summary>
        public static void Sparkline(Rect r, float[] values, Color tint, float baseline)
        {
            if (values == null || values.Length < 2) return;

            float min = float.MaxValue, max = float.MinValue;
            for (int i = 0; i < values.Length; i++) { min = Mathf.Min(min, values[i]); max = Mathf.Max(max, values[i]); }
            min = Mathf.Min(min, baseline);
            max = Mathf.Max(max, baseline);
            if (max - min < 0.0001f) max = min + 1f;

            // Fair-value reference line.
            float by = r.yMax - (baseline - min) / (max - min) * r.height;
            Fill(new Rect(r.x, by, r.width, 1f), new Color(1f, 1f, 1f, 0.18f));

            float step = r.width / (values.Length - 1);
            for (int i = 0; i < values.Length; i++)
            {
                float h = (values[i] - min) / (max - min);
                float y = r.yMax - h * r.height;
                Fill(new Rect(r.x + i * step, y - 1f, Mathf.Max(1f, step), 2f), tint);
            }
        }

        static GUIStyle _label, _small, _header, _button;

        public static GUIStyle Label
        {
            get
            {
                if (_label == null)
                    _label = new GUIStyle(GUI.skin.label) { fontSize = 12, richText = true, wordWrap = false };
                return _label;
            }
        }

        public static GUIStyle Small
        {
            get
            {
                if (_small == null)
                    _small = new GUIStyle(GUI.skin.label) { fontSize = 10, richText = true, wordWrap = true };
                return _small;
            }
        }

        public static GUIStyle Header
        {
            get
            {
                if (_header == null)
                    _header = new GUIStyle(GUI.skin.label) { fontSize = 13, fontStyle = FontStyle.Bold, richText = true };
                return _header;
            }
        }

        public static GUIStyle Button
        {
            get
            {
                if (_button == null)
                    _button = new GUIStyle(GUI.skin.button) { fontSize = 11, richText = true, alignment = TextAnchor.MiddleLeft };
                return _button;
            }
        }

        public static string Hex(Color c) => ColorUtility.ToHtmlStringRGB(c);

        public static string Tinted(string text, Color c) => $"<color=#{Hex(c)}>{text}</color>";

        public static string Signed(float v, string format = "0.0")
            => (v >= 0f ? "+" : "") + v.ToString(format);
    }
}
