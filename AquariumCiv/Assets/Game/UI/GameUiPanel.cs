using UnityEngine;
using UnityEngine.UIElements;

namespace AquariumCiv.GameUI
{
    /// <summary>
    /// What every screen-UI layer shares (UI spike, docs/design/spikes/ui-automation.md): the one panel (scale with
    /// screen size, 1080 × 1920 reference, match width, the game theme), the safe-area insets in panel units, and the
    /// "is this screen point on my UI" test the box camera asks through <see cref="AquariumCiv.Game.UiInput"/>.
    /// </summary>
    public static class GameUiPanel
    {
        public const float ReferenceWidth = 1080f;
        /// <summary>The lanes of the bottom of the screen, up from the safe area's edge (panel units). Toasts sit at
        /// <see cref="ToastBottom"/> (Game.uss .hud-toast, three lines reach about 420); prompts (Watch, Finish, the first run's
        /// button) sit above them at <see cref="PromptBottom"/>, so a prompt and a toast never share a spot (tester batch 3).</summary>
        public const float ToastBottom = 240f, PromptBottom = 430f;
        /// <summary>48 dp on a 360 dp-wide phone (1080 px at 3×), which also covers 44 pt on a 375 pt iPhone.</summary>
        public const float MinTarget = 144f;

        static PanelSettings settings;

        /// <summary>The shared panel (Resources/GameUI/GamePanel); every UIDocument on it draws into the same panel.</summary>
        public static PanelSettings Settings
        {
            get
            {
                if (settings != null) return settings;
                settings = Resources.Load<PanelSettings>("GameUI/GamePanel");
                if (settings == null)
                {
                    // Fallback with the same values, so a missing asset never leaves the game without UI.
                    settings = ScriptableObject.CreateInstance<PanelSettings>();
                    settings.themeStyleSheet = Resources.Load<ThemeStyleSheet>("GameUI/GameTheme");
                    settings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
                    settings.referenceResolution = new Vector2Int((int)ReferenceWidth, 1920);
                    settings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
                    settings.match = 0f;
                }
                return settings;
            }
        }

        /// <summary>
        /// Safe-area insets (left, top, right, bottom) in panel units for a panel that matches the screen width to
        /// <see cref="ReferenceWidth"/>. <paramref name="safe"/> is Unity's <c>Screen.safeArea</c> (pixels, y up).
        /// </summary>
        public static Vector4 SafeInsets(Rect safe, float screenWidth, float screenHeight)
        {
            float s = ReferenceWidth / Mathf.Max(1f, screenWidth);
            return new Vector4(
                Mathf.Max(0f, safe.xMin) * s,
                Mathf.Max(0f, screenHeight - safe.yMax) * s,
                Mathf.Max(0f, screenWidth - safe.xMax) * s,
                Mathf.Max(0f, safe.yMin) * s);
        }

        /// <summary>Is there a pickable element of <paramref name="root"/> (not the root itself) under a screen point (pixels, y up)?</summary>
        public static bool Over(VisualElement root, Vector2 screenPos)
        {
            if (root == null || root.panel == null || root.resolvedStyle.display == DisplayStyle.None) return false;
            var p = RuntimePanelUtils.ScreenToPanel(root.panel, new Vector2(screenPos.x, Screen.height - screenPos.y));
            var hit = root.panel.Pick(p);
            return hit != null && hit != root && root.Contains(hit);
        }

        /// <summary>Screen point (pixels, y up) at the centre of an element: what a test or the tester agent touches.</summary>
        public static Vector2 ScreenCenter(VisualElement e)
        {
            var c = e.worldBound.center; // panel space: y down
            float scale = Screen.width / e.panel.visualTree.worldBound.width;
            return new Vector2(c.x * scale, Screen.height - c.y * scale);
        }

        public static Vector2 ScreenCenter(VisualElement root, string name) =>
            ScreenCenter(root.Q(name) ?? throw new System.ArgumentException($"No element named '{name}'"));

        /// <summary>
        /// Play tests and agents in the Editor: on Windows and macOS UI Toolkit drops every pointer event while the app isn't
        /// focused (DefaultEventSystem.ShouldIgnoreEventsOnAppNotFocused), so a test's touches miss every button whenever the
        /// Editor sits behind another window, and "a tap on Watch / Finish / Clean" fails at random. On: the panels take
        /// input unfocused, as they do with Unity Remote connected (the same internal switch). Editor only; play mode's
        /// domain reload resets it.
        /// </summary>
        public static void AcceptInputUnfocused(bool on)
        {
#if UNITY_EDITOR
            var t = typeof(PanelSettings).Assembly.GetType("UnityEngine.UIElements.DefaultEventSystem");
            var f = t?.GetField("IsEditorRemoteConnected", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
            if (f == null || f.FieldType != typeof(System.Func<bool>))
            {
                if (on) Debug.LogWarning("[GameUiPanel] UI Toolkit's focus switch moved: taps from tests need the Editor focused");
                return;
            }
            if (on && remoteWas == null) { remoteWas = (System.Func<bool>)f.GetValue(null); f.SetValue(null, (System.Func<bool>)(() => true)); }
            else if (!on && remoteWas != null) { f.SetValue(null, remoteWas); remoteWas = null; }
#endif
        }

#if UNITY_EDITOR
        static System.Func<bool> remoteWas;
#endif
    }
}
