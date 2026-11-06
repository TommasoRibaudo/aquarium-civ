using System.Collections.Generic;
using AquariumCiv.Game;
using AquariumCiv.Sim.Settle;
using UnityEngine;
using Site = AquariumCiv.Sim.Settle.SiteMap;

namespace AquariumCiv.View
{
    /// <summary>
    /// #357 cheats for the debug panel's "Hit puffs (#357)" section (and `eval`): "Puffs on/off", "Shake all targets once", "Puff each kind here". Each returns a
    /// line for the panel. Everything goes through <see cref="HitFx"/>'s public calls, so what these show is what a later animation gets.
    /// </summary>
    public static class HitFxCheats
    {
        /// <summary>The panel block: three buttons and a live count. Returns a status line when one was pressed, else null.</summary>
        public static string Gui()
        {
            string line = null;
            var fx = HitFx.Instance;
            GUILayout.Label(fx == null ? "HitFx: not running" : $"puffs {fx.LivePuffs} ({fx.LiveBits} bits) · shakes {fx.ShakesLive} · hits seen {fx.HitsSeen}");
            if (GUILayout.Button("Puffs: " + (HitFx.Enabled ? "on" : "off") + " (tap to switch)")) line = Toggle();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Shake all targets once")) line = ShakeAll();
            if (GUILayout.Button("Puff each kind here")) line = PuffRow();
            GUILayout.EndHorizontal();
            return line;
        }

        public static string Toggle()
        {
            HitFx.Enabled = !HitFx.Enabled;
            if (!HitFx.Enabled && HitFx.Instance != null) HitFx.Instance.ClearAll();
            return "puffs " + (HitFx.Enabled ? "on" : "off");
        }

        static bool Ground(Camera cam, float vx, float vy, out Vector3 p)
        {
            var box = Object.FindAnyObjectByType<BoxView>();
            float y = box != null && box.Root != null ? box.Root.position.y + Land.G : Land.G;
            var plane = new Plane(Vector3.up, new Vector3(0f, y, 0f));
            var ray = cam.ViewportPointToRay(new Vector3(vx, vy, 0f));
            if (!plane.Raycast(ray, out float t)) { p = default; return false; }
            p = ray.GetPoint(t);
            if (box != null && box.Root != null)
            {
                var l = box.Root.InverseTransformPoint(p);
                p = box.Root.TransformPoint(new Vector3(l.x, box.SurfaceHeight(l.x, l.z), l.z));
            }
            return true;
        }

        /// <summary>Every puff kind in a row across the middle of the view (left to right: chips, dust, sparks, droplets, ring, grass, leaves, steam, snow),
        /// 0.06 u above the ground, so the AnimLab and the tester can look at each one at any zoom.</summary>
        public static string PuffRow()
        {
            var cam = Camera.main;
            if (cam == null) return "no camera";
            if (!Ground(cam, 0.15f, 0.45f, out var a) || !Ground(cam, 0.85f, 0.45f, out var b)) return "the view misses the ground";
            HitFx.Ensure();
            var kinds = (PuffKind[])System.Enum.GetValues(typeof(PuffKind));
            int n = 0;
            for (int i = 0; i < kinds.Length; i++)
            {
                var at = Vector3.Lerp(a, b, (i + 0.5f) / kinds.Length) + Vector3.up * 0.06f;
                var o = PuffOptions.Default;
                o.groundY = at.y - 0.06f;
                var away = cam.transform.right;
                if (kinds[i] == PuffKind.Leaves) at += Vector3.up * 0.15f;
                if (HitFx.Puff(kinds[i], at, away, 1f, o, true)) n++;
            }
            return $"{n} puffs in a row";
        }

        /// <summary>Every bush and tree in view sways once, staggered 0–0.4 s; the nearest to the middle of the view first claim the (16) shader slots.</summary>
        public static string ShakeAll()
        {
            var cam = Camera.main;
            var box = Object.FindAnyObjectByType<BoxView>();
            if (cam == null || box == null || box.Root == null) return "no box or camera";
            HitFx.Ensure();
            var cands = new List<(float d, Vector3 pos, ShakeTarget what)>();
            void Consider(Vector3 local, ShakeTarget what)
            {
                var w = box.Root.TransformPoint(local);
                var v = cam.WorldToViewportPoint(w);
                if (v.z <= 0f || v.x < 0f || v.x > 1f || v.y < 0f || v.y > 1f) return;
                cands.Add((new Vector2(v.x - 0.5f, v.y - 0.5f).sqrMagnitude, w, what));
            }
            foreach (var kv in box.BushSpots) Consider(kv.Value.pos, ShakeTarget.Bush);
            var cover = box.layout != null ? box.layout.eco.cover : null;
            if (cover != null)
                for (int c = 0; c < Site.N; c++)
                {
                    if (cover[c] != EcoLayer.Tree) continue;
                    float x = Site.CXc(c), z = Site.CZc(c);
                    Consider(new Vector3(x, box.SurfaceHeight(x, z), z), ShakeTarget.Tree);
                }
            cands.Sort((p, q) => p.d.CompareTo(q.d));
            int started = 0;
            for (int i = 0; i < cands.Count && started < ShakeSet.Max; i++)
            {
                float delay = HitFxMath.Hash01(31, i, 5) * 0.4f;
                if (HitFx.Shake(cands[i].what, cands[i].pos, cam.transform.right, delay, true)) started++;
            }
            return $"{started} of {cands.Count} targets in view shaken (up to {ShakeSet.Max} at once)";
        }
    }
}
