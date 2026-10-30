using AquariumCiv.Game;
using AquariumCiv.GameUI;
using UnityEngine;

namespace AquariumCiv.View
{
    /// <summary>
    /// TEMPORARY shared layout for the IMGUI debug panels (#40): one "dbg" toggle at the top left of the safe area (the
    /// HUD's title is centred and Feedback sits top right), one panel open at a time (dbg or sim tab), nothing drawn
    /// while a HUD sheet is open, and the covered rects go to <see cref="UiInput.DebugBlocks"/> so neither the camera nor
    /// the HUD reacts under them.
    /// </summary>
    public static class DebugDock
    {
        public const int None = 0, Dbg = 1, Sim = 2;
        public static int Active;
        public const float BarH = 32f, Gap = 8f;
        static Rect bar, panel;

        public static bool Hidden =>
            (GameHud.Instance != null && (GameHud.Instance.SheetOpen || GameHud.Instance.CameraMode)) || (FeedbackUI.Instance != null && FeedbackUI.Instance.IsOpen);

        /// <summary>Sets the IMGUI matrix and returns the safe area in IMGUI units.</summary>
        public static Rect Begin(out float sc)
        {
            sc = Mathf.Max(1f, Screen.height / 1100f);
            GUI.matrix = Matrix4x4.Scale(new Vector3(sc, sc, 1f));
            var sa = Screen.safeArea;
            return new Rect(sa.x / sc, (Screen.height - sa.yMax) / sc, sa.width / sc, sa.height / sc);
        }

        /// <summary>Where the open panel goes: under the toggle bar, left aligned. Height is capped to the safe area.</summary>
        public static Rect PanelArea(Rect safe, float width, float height)
        {
            float w = Mathf.Min(safe.width - 2 * Gap, width);
            float maxH = safe.height - BarH - 3 * Gap;
            return new Rect(safe.x + Gap, safe.y + BarH + 2 * Gap, w, Mathf.Clamp(height, 60f, maxH));
        }

        /// <summary>The toggle bar; call once per OnGUI. Returns false when nothing may be drawn (a HUD sheet is open).</summary>
        public static bool DrawBar(Rect safe, float sc)
        {
            if (Hidden) { Publish(sc, false); return false; }
            bool open = Active != None;
            bar = new Rect(safe.x + Gap, safe.y + Gap, open ? 3 * 58f : 56f, BarH);
            if (!open)
            {
                if (GUI.Button(new Rect(bar.x, bar.y, 56, BarH), "dbg")) Active = Dbg;
            }
            else
            {
                if (GUI.Toggle(new Rect(bar.x, bar.y, 56, BarH), Active == Dbg, "dbg", GUI.skin.button)) Active = Dbg;
                if (GUI.Toggle(new Rect(bar.x + 58, bar.y, 56, BarH), Active == Sim, "sim", GUI.skin.button)) Active = Sim;
                if (GUI.Button(new Rect(bar.x + 116, bar.y, 56, BarH), "x")) Active = None;
            }
            Publish(sc, true);
            return true;
        }

        /// <summary>The panel reports the rect it covers (IMGUI units); an empty rect = none.</summary>
        public static void SetPanel(Rect r) => panel = r;

        static void Publish(float sc, bool show)
        {
            var list = UiInput.DebugBlocks;
            list.Clear();
            if (!show) return;
            list.Add(ToScreen(bar, sc));
            if (Active != None && panel.width > 0f) list.Add(ToScreen(panel, sc));
        }

        static Rect ToScreen(Rect r, float sc) => new(r.x * sc, r.y * sc, r.width * sc, r.height * sc);
    }
}
