using UnityEngine;

namespace AquariumCiv.Prototypes
{
    /// <summary>
    /// Shared IMGUI look + scaling for throwaway prototype UI: layouts are authored on a 720x1280 portrait canvas.
    /// One skin for every panel: rounded deep-water panels, soft teal buttons, a warm primary button, chips for picks.
    /// </summary>
    public static class ProtoGui
    {
        public static float Scale => Mathf.Min(Screen.width / 720f, Screen.height / 1280f);
        public static float Width => Screen.width / Scale;
        public static float Height => Screen.height / Scale;
        /// <summary>Canvas units reserved at the bottom (home indicator); bottom-anchored UI sits above it.</summary>
        public static float SafeBottom => Screen.safeArea.y / Scale;

        // Palette: deep water, glass teal, warm sand accent.
        static readonly Color Ink = new(0.96f, 0.93f, 0.86f);
        static readonly Color InkDim = new(0.96f, 0.93f, 0.86f, 0.6f);
        static readonly Color InkDark = new(0.12f, 0.09f, 0.06f);
        static readonly Color PanelFill = new(0.06f, 0.15f, 0.19f, 0.98f);
        static readonly Color PanelEdge = new(0.45f, 0.75f, 0.78f, 0.35f);
        static readonly Color ButtonFill = new(0.16f, 0.35f, 0.40f, 0.96f);
        static readonly Color ButtonHover = new(0.21f, 0.43f, 0.48f, 0.98f);
        static readonly Color ButtonDown = new(0.11f, 0.26f, 0.30f, 1f);
        static readonly Color ButtonEdge = new(0.55f, 0.85f, 0.86f, 0.45f);
        static readonly Color Accent = new(0.96f, 0.68f, 0.38f, 1f);
        static readonly Color AccentHover = new(1f, 0.76f, 0.48f, 1f);
        static readonly Color AccentDown = new(0.86f, 0.58f, 0.30f, 1f);
        static readonly Color FieldFill = new(0.03f, 0.09f, 0.12f, 0.95f);
        static readonly Color FieldEdge = new(0.45f, 0.75f, 0.78f, 0.5f);

        static GUISkin skin;
        static GUIStyle box, wrap, title, hint, hintRight, primary, disabled, chip, chipOn;
        public static GUIStyle Box => box;
        public static GUIStyle Wrap => wrap;
        public static GUIStyle Title => title;
        public static GUIStyle Hint => hint;
        public static GUIStyle HintRight => hintRight;
        /// <summary>Warm main action (Send, Start).</summary>
        public static GUIStyle Primary => primary;
        /// <summary>Opaque greyed-out button (GUI.enabled = false makes buttons see-through).</summary>
        public static GUIStyle Disabled => disabled;
        public static GUIStyle Chip => chip;
        public static GUIStyle ChipOn => chipOn;

        public static void Begin()
        {
            GUI.matrix = Matrix4x4.Scale(new Vector3(Scale, Scale, 1));
            if (skin == null) Build();
            GUI.skin = skin;
        }

        /// <summary>A rounded panel background (no content).</summary>
        public static void Panel(Rect r) => GUI.Box(r, GUIContent.none, skin.window);

        /// <summary>Main action button that stays opaque while unavailable; returns true only when tapped and enabled.</summary>
        public static bool PrimaryButton(string label, bool enabled, params GUILayoutOption[] options) =>
            GUILayout.Button(label, enabled ? primary : disabled, options) && enabled;

        /// <summary>A chip that shows its on-state; returns true when tapped.</summary>
        public static bool ChipButton(string label, bool on, params GUILayoutOption[] options) =>
            GUILayout.Button(label, on ? chipOn : chip, options);

        static void Build()
        {
            skin = Object.Instantiate(GUI.skin);
            skin.hideFlags = HideFlags.HideAndDontSave;
            var font = skin.font;

            var panelTex = Rounded(PanelFill, PanelEdge, 18);
            skin.window = new GUIStyle
            {
                normal = { background = panelTex, textColor = Ink }, border = new RectOffset(20, 20, 20, 20),
            };
            skin.box = new GUIStyle(skin.window)
            {
                font = font, fontSize = 22, alignment = TextAnchor.MiddleCenter, padding = new RectOffset(14, 14, 10, 10),
                richText = true, wordWrap = false,
            };
            skin.label = new GUIStyle
            {
                font = font, fontSize = 22, richText = true, normal = { textColor = Ink }, margin = new RectOffset(0, 0, 4, 4),
                padding = new RectOffset(2, 2, 2, 2), alignment = TextAnchor.MiddleLeft,
            };

            skin.button = ButtonStyle(font, ButtonFill, ButtonHover, ButtonDown, ButtonEdge, Ink);
            primary = ButtonStyle(font, Accent, AccentHover, AccentDown, new Color(1f, 0.9f, 0.75f, 0.6f), InkDark);

            chip = ButtonStyle(font, new Color(0.08f, 0.2f, 0.24f, 0.9f), new Color(0.12f, 0.27f, 0.31f, 0.95f),
                ButtonDown, ButtonEdge, Ink);
            chipOn = ButtonStyle(font, Accent, AccentHover, AccentDown, new Color(1f, 0.9f, 0.75f, 0.8f), InkDark);
            foreach (var c in new[] { chip, chipOn })
            {
                c.fontSize = 21; c.fontStyle = FontStyle.Normal; c.padding = new RectOffset(6, 6, 6, 6);
            }
            disabled = ButtonStyle(font, new Color(0.10f, 0.20f, 0.23f, 1f), new Color(0.10f, 0.20f, 0.23f, 1f),
                new Color(0.10f, 0.20f, 0.23f, 1f), new Color(0.45f, 0.75f, 0.78f, 0.2f), new Color(0.96f, 0.93f, 0.86f, 0.35f));

            var fieldTex = Rounded(FieldFill, FieldEdge, 12);
            skin.textField = new GUIStyle
            {
                font = font, fontSize = 22, normal = { background = fieldTex, textColor = Ink },
                hover = { background = fieldTex, textColor = Ink }, focused = { background = Rounded(FieldFill, Accent, 12), textColor = Ink },
                border = new RectOffset(14, 14, 14, 14), padding = new RectOffset(16, 16, 12, 12),
                margin = new RectOffset(0, 0, 6, 6), alignment = TextAnchor.MiddleLeft, clipping = TextClipping.Clip,
            };
            skin.textArea = new GUIStyle(skin.textField) { wordWrap = true, alignment = TextAnchor.UpperLeft };
            skin.settings.cursorColor = Accent;
            skin.settings.selectionColor = new Color(Accent.r, Accent.g, Accent.b, 0.35f);

            box = new GUIStyle(skin.box) { alignment = TextAnchor.UpperLeft, padding = new RectOffset(18, 18, 14, 14), wordWrap = true };
            wrap = new GUIStyle(skin.label) { wordWrap = true, alignment = TextAnchor.UpperLeft };
            title = new GUIStyle(wrap) { fontSize = 30, fontStyle = FontStyle.Bold };
            hint = new GUIStyle(wrap) { fontSize = 20, normal = { textColor = InkDim } };
            hintRight = new GUIStyle(hint) { alignment = TextAnchor.UpperRight };
        }

        static GUIStyle ButtonStyle(Font font, Color fill, Color hover, Color down, Color edge, Color text) => new()
        {
            font = font, fontSize = 23, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter,
            normal = { background = Rounded(fill, edge, 16), textColor = text },
            hover = { background = Rounded(hover, edge, 16), textColor = text },
            active = { background = Rounded(down, edge, 16), textColor = text },
            border = new RectOffset(18, 18, 18, 18), padding = new RectOffset(12, 12, 8, 8),
            margin = new RectOffset(5, 5, 5, 5),
        };

        /// <summary>Anti-aliased rounded rect with a 2px edge, for 9-slicing.</summary>
        static Texture2D Rounded(Color fill, Color edge, int radius)
        {
            int size = radius * 2 + 4;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.HideAndDontSave,
            };
            var px = new Color[size * size];
            float c = size / 2f, half = c - radius;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                // signed distance to the rounded rect edge (negative inside)
                float dx = Mathf.Max(Mathf.Abs(x + 0.5f - c) - half, 0), dy = Mathf.Max(Mathf.Abs(y + 0.5f - c) - half, 0);
                float d = Mathf.Sqrt(dx * dx + dy * dy) - radius + 1;
                float cover = Mathf.Clamp01(0.5f - d);
                float edgeMix = Mathf.Clamp01(d + 2.5f); // ~2px rim
                var col = Color.Lerp(fill, edge, edgeMix * edge.a);
                col.a = Mathf.Max(fill.a, edgeMix * edge.a) * cover;
                px[y * size + x] = col;
            }
            tex.SetPixels(px);
            tex.Apply();
            return tex;
        }

        /// <summary>Register a canvas-space rect so world input under it is ignored.</summary>
        public static void Block(Rect canvasRect)
        {
            float k = Scale;
            GuiBlocker.Add(new Rect(canvasRect.x * k, canvasRect.y * k, canvasRect.width * k, canvasRect.height * k));
        }

        public static Vector2 WorldToCanvas(Vector3 world)
        {
            var cam = Camera.main;
            if (cam == null) return new Vector2(-999, -999);
            var sp = cam.WorldToScreenPoint(world);
            if (sp.z < 0) return new Vector2(-999, -999);
            return new Vector2(sp.x / Scale, (Screen.height - sp.y) / Scale);
        }
    }
}
