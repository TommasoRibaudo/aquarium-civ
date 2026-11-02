using System.Collections.Generic;
using AquariumCiv.Game;
using UnityEngine;
using UnityEngine.UIElements;

namespace AquariumCiv.GameUI
{
    /// <summary>
    /// The second tap of a double tap on a closing control (tester run 5). A leaving sheet takes no taps on purpose, so a tool under it
    /// works at once; that also let a repeat fall through to whatever sat under the button. A repeat lands on the same spot, a new tap
    /// doesn't: so for a moment a clear patch stands on the spot of the tap that closed, and swallows a press there. Everything else on
    /// screen stays live. It stands over every panel layer and tells <see cref="UiInput"/>, so the world doesn't take the press either.
    /// Off under <see cref="GlassMotion.Instant"/>: play tests tap in sequence, and nothing is on its way out.
    /// </summary>
    public static class GlassTapGuard
    {
        /// <summary>How long the spot stays shut. A deliberate second tap at the same place is slower than a double tap.</summary>
        public const float Seconds = 0.35f;
        /// <summary>Half the patch, panel px: 28 pt each way, a fingertip's wander. The next tool along is further than that.</summary>
        public const float Reach = 28f * GlassType.Pt;
        /// <summary>A finger that came down on the patch and stayed: it's let go of at this age regardless.</summary>
        const long HeldLimitMs = 3000;

        static readonly List<VisualElement> live = new();
        public static IReadOnlyList<VisualElement> Live => live;

        /// <summary>Shuts the spot <paramref name="center"/> (panel px) of <paramref name="anyInPanel"/>'s panel for <see cref="Seconds"/>.</summary>
        public static void Arm(VisualElement anyInPanel, Vector2 center)
        {
            if (GlassMotion.Instant || anyInPanel?.panel == null) return;
            UiInput.Add(Over);
            var guard = new VisualElement { name = "kg-tap-guard", pickingMode = PickingMode.Position };
            guard.style.position = Position.Absolute;
            guard.style.left = center.x - Reach;
            guard.style.top = center.y - Reach;
            guard.style.width = guard.style.height = Reach * 2f;
            bool held = false, due = false;
            void Done()
            {
                live.Remove(guard);
                guard.RemoveFromHierarchy();
            }
            guard.RegisterCallback<PointerDownEvent>(e =>
            {
                held = true;
                guard.CapturePointer(e.pointerId); // the lift has to end here too, or a scrim under it reads it as a tap outside
                e.StopPropagation();
            });
            guard.RegisterCallback<PointerUpEvent>(e =>
            {
                held = false;
                guard.ReleasePointer(e.pointerId);
                e.StopPropagation();
                if (due) Done();
            });
            anyInPanel.panel.visualTree.Add(guard);
            live.Add(guard);
            guard.schedule.Execute(() => { due = true; if (!held) Done(); }).ExecuteLater((long)(Seconds * 1000f));
            guard.schedule.Execute(Done).ExecuteLater(HeldLimitMs);
        }

        /// <summary>The spot of a control, for a closer that isn't a sheet.</summary>
        public static void Arm(VisualElement control) { if (control != null) Arm(control, control.worldBound.center); }

        static bool Over(Vector2 screenPos)
        {
            for (int i = live.Count - 1; i >= 0; i--)
            {
                var g = live[i];
                if (g.panel == null) { live.RemoveAt(i); continue; } // the scene went with it
                var p = RuntimePanelUtils.ScreenToPanel(g.panel, new Vector2(screenPos.x, Screen.height - screenPos.y));
                if (g.worldBound.Contains(p)) return true;
            }
            return false;
        }
    }
}
