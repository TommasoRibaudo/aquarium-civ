using System;
using System.Collections.Generic;
using AquariumCiv.Life;
using UnityEngine;

namespace AquariumCiv.View
{
    /// <summary>Where a washing line stands (box-local x, z): the cord's middle on the ground, the unit direction along it (from the near stake by
    /// the door to the far one) and the unit direction out from the wall, which is where the doer stands.</summary>
    public struct LaundryAnchor
    {
        public Vector2 centre, along, outDir;
    }

    /// <summary>Everything the mesh needs to draw one line at one moment (<see cref="LaundryLines.Build"/>).</summary>
    public sealed class LineLook
    {
        public int cloths = 4, seed;
        /// <summary>0-1 per cloth: how much of it is on the line (a pop in, a pull out). Length at least <see cref="cloths"/>.</summary>
        public float[] pop = new float[Laundry.BigCapacity];
        /// <summary>0-1 per cloth: still bunched where it was pinned (0) to smoothed flat (1).</summary>
        public float[] flat = new float[Laundry.BigCapacity];
        /// <summary>0-1 per cloth: seconds ago it latched (a one-frame jolt along the out direction).</summary>
        public float[] jolt = new float[Laundry.BigCapacity];
        /// <summary>Stakes and cord: 0 none, 1 standing.</summary>
        public float stakes = 1f;
        /// <summary>Brightness: 0.7 wet to 1 dry.</summary>
        public float dry = 1f;
        /// <summary>Sway amplitude (degrees) and the clock it swings on.</summary>
        public float sway, time;
        /// <summary>The quiet fade: the cloths fold up (0 none, 1 folded to a fifth).</summary>
        public float fade;
        /// <summary>The wicker basket at the near stake, with this many cloths still in it.</summary>
        public bool basket; public int inBasket;
    }

    /// <summary>
    /// #705 (B28.a): the washing line's look. A cord 0.55 u long between two dark-wood stakes 0.17 u high; cloths 0.11 x 0.13 u hung from it
    /// 0.03 u apart (one small, a kid's shirt 0.08 x 0.09), muted colours (ochre, faded blue, undyed linen, dull rust: never white, never saturated),
    /// a pin at each top corner, a 0.012 u fold at the top; a low wicker basket at the near stake while the chore runs. Empty lines draw nothing.
    /// Pure geometry (MeshBuilder), so AnimLab draws the same thing; <see cref="LaundryView"/> drives it from the planner's lines.
    /// </summary>
    public static class LaundryLines
    {
        public const float ClothW = Laundry.ClothW, ClothH = Laundry.ClothH, Gap = Laundry.Gap, Pitch = Laundry.Pitch, StakeH = 0.17f, StakeThick = 0.014f, CordY = 0.152f, StandOut = Laundry.StandOut;
        public const float SmallW = 0.08f, SmallH = 0.09f;

        /// <summary>Muted colours, never white: ochre, faded blue, undyed linen, dull rust.</summary>
        public static readonly Color[] ClothColours =
        {
            new Color(0.74f, 0.56f, 0.26f), new Color(0.42f, 0.53f, 0.64f), new Color(0.78f, 0.72f, 0.58f), new Color(0.62f, 0.34f, 0.24f),
        };
        static readonly Color Wood = new Color(0.24f, 0.16f, 0.10f), Cord = new Color(0.72f, 0.64f, 0.46f), Pin = new Color(0.16f, 0.11f, 0.08f), Wicker = new Color(0.66f, 0.5f, 0.3f);

        public static float Length(int cloths, float scale = 1f) => Laundry.Length(cloths, scale);

        /// <summary>Where along the cord (from its middle, toward the far stake) cloth <paramref name="j"/> hangs.</summary>
        public static float SlotAlong(int j, int cloths, float scale = 1f) => Laundry.SlotAlong(j, cloths, scale);

        public static Color ColourOf(int seed, int j) => ClothColours[(int)(((((uint)seed * 2654435761u) >> 8) + (uint)j) % (uint)ClothColours.Length)]; // a negative int index threw for half the seeds

        /// <summary>Which cloth is the small one, a kid's shirt: one per line, hashed.</summary>
        public static int SmallOne(int seed, int cloths) => cloths < 3 ? -1 : (int)((uint)(seed * 40503u >> 4) % (uint)cloths);

        public static (float w, float h) SizeOf(int seed, int j, int cloths) => SmallOne(seed, cloths) == j ? (SmallW, SmallH) : (ClothW, ClothH);

        /// <summary>Appends the line to <paramref name="mb"/> in the parent's space. <paramref name="origin"/>: the cord's middle on the ground;
        /// <paramref name="along"/> and <paramref name="outDir"/>: unit directions (box x, z); <paramref name="scale"/>: the doll zoom scale.</summary>
        public static void Build(MeshBuilder mb, Vector3 origin, Vector2 along, Vector2 outDir, LineLook L, float scale = 1f)
        {
            mb.jitter = 0f;
            var A = new Vector3(along.x, 0f, along.y); var O = new Vector3(outDir.x, 0f, outDir.y);
            var up = Vector3.up;
            float s = scale, half = Length(L.cloths, s) * 0.5f;
            var rot = Quaternion.LookRotation(O, up);
            var rotA = Quaternion.LookRotation(A, up);
            if (L.stakes > 0.01f)
            {
                float sh = StakeH * s * Mathf.Clamp01(L.stakes) * (L.stakes > 1f ? 1f : 1f);
                for (int e = -1; e <= 1; e += 2)
                    mb.Box(origin + A * (half * e) + up * (sh * 0.5f), new Vector3(StakeThick * s, sh, StakeThick * s), Wood, rot);
                float cordY = CordY * s * Mathf.Clamp01(L.stakes);
                mb.Box(origin + up * cordY, new Vector3(0.005f * s, 0.005f * s, half * 2f), Cord, rotA);
            }
            for (int j = 0; j < L.cloths; j++)
            {
                float pop = Mathf.Clamp01(L.pop[j]);
                if (pop <= 0.01f) continue;
                var (w0, h0) = SizeOf(L.seed, j, L.cloths);
                float flat = Mathf.Clamp01(L.flat[j]);
                float w = w0 * s * (0.9f + 0.1f * flat) * pop;
                float h = h0 * s * (0.86f + 0.14f * flat) * pop * (1f - 0.8f * L.fade);
                float a = SlotAlong(j, L.cloths, s);
                float top = CordY * s;
                float ang = L.sway * Mathf.Sin((L.time * 0.6f + j * 0.37f) * Mathf.PI * 2f) * Mathf.Deg2Rad;
                // bunched and heavy until smoothed: it hangs a little crooked
                ang += (1f - flat) * 0.09f * (j % 2 == 0 ? 1f : -1f);
                var shift = O * (L.jolt[j] * 0.01f * s);
                var c = origin + A * a + shift;
                var tl = c + A * (-w * 0.5f) + up * top;
                var tr = c + A * (w * 0.5f) + up * top;
                var dropOut = O * (Mathf.Sin(ang) * h);
                var bl = tl - up * (Mathf.Cos(ang) * h) + dropOut;
                var br = tr - up * (Mathf.Cos(ang) * h) + dropOut;
                var col = ColourOf(L.seed, j) * L.dry; col.a = 1f;
                var back = col * 0.88f; back.a = 1f;
                mb.Quad(tl, tr, br, bl, col);
                mb.Quad(tl, bl, br, tr, back);
                // the 0.012 fold at the top, and a pin at each top corner
                float fold = Mathf.Lerp(0.012f, 0.004f, flat) * s;
                var fc = col * 0.8f; fc.a = 1f;
                mb.Box(c + up * (top - fold * 0.5f) + O * (0.004f * s), new Vector3(w, fold, 0.006f * s), fc, rot);
                for (int e = -1; e <= 1; e += 2)
                    mb.Box(c + A * (e * (w * 0.5f - 0.012f * s)) + up * (top - 0.004f * s) + O * (0.008f * s), new Vector3(0.015f * s, 0.015f * s, 0.008f * s), Pin, rot);
            }
            if (L.basket)
            {
                var at = origin + A * (-half - 0.075f * s) + O * (0.05f * s);
                mb.Prism(at, 0.032f * s, 0.044f * s, 0.05f * s, 8, Wicker);
                for (int k = 0; k < Mathf.Min(3, L.inBasket); k++)
                {
                    var col = ColourOf(L.seed, L.cloths - 1 - k) * 0.85f; col.a = 1f;
                    mb.Ico(at + up * (0.052f * s) + A * ((k - 1) * 0.02f * s), 0.022f * s, new Vector3(1f, 0.55f, 1f), col, 0.1f);
                }
            }
        }
    }

    /// <summary>
    /// #705: draws the planner's lines of washing (<see cref="LifePlanner.LaundryOn"/>) at their homes, one mesh each, rebuilt while it moves.
    /// The line's state is a function of the day's clock, so a box looked at later finds it where the clock says (B28.d "not seen"). The
    /// doll's own beats (<see cref="LaundryBeats"/>) share the cloth timings through <see cref="Laundry"/>.
    /// </summary>
    public sealed class LaundryView
    {
        sealed class Drawn { public GameObject go; public Mesh mesh; public MeshBuilder mb; }
        readonly Dictionary<(Place, int), Drawn> drawn = new();
        /// <summary>Destroy in play, DestroyImmediate in edit mode (the edit-mode tests tick the people too: Destroy there logs an error).</summary>
        static void Gone(UnityEngine.Object o) { if (Application.isPlaying) UnityEngine.Object.Destroy(o); else UnityEngine.Object.DestroyImmediate(o); }
        readonly List<(Place, int)> stale = new();
        readonly HashSet<(Place, int)> live = new();

        /// <summary>Lines drawn this frame (tests and the cheat's report).</summary>
        public int Visible => live.Count;
        /// <summary>Cloths drawn on the first visible line (tests).</summary>
        public int ClothsShown { get; private set; }
        public bool StakesUp { get; private set; }

        public delegate bool AnchorOf(Place home, int ordinal, int cloths, out LaundryAnchor anchor);

        public void Tick(IReadOnlyList<LaundryLine> lines, ViewTime now, Transform root, Material mat, AnchorOf anchorOf, Func<float, float, float> groundY, float scale, float time)
        {
            live.Clear();
            ClothsShown = 0; StakesUp = false;
            for (int i = 0; i < lines.Count; i++)
            {
                var l = lines[i];
                if (!Look(l, now, time, out var look)) continue;
                if (!anchorOf(l.home, l.ordinal, l.cloths, out var an)) continue;
                var key = (l.home, l.ordinal);
                live.Add(key);
                if (!drawn.TryGetValue(key, out var d))
                {
                    d = new Drawn { mb = new MeshBuilder(705) { jitter = 0f } };
                    d.go = new GameObject("Washing line " + l.home) { hideFlags = HideFlags.DontSave };
                    d.go.transform.SetParent(root, false);
                    d.go.AddComponent<MeshFilter>(); var mr = d.go.AddComponent<MeshRenderer>(); mr.sharedMaterial = mat;
                    drawn[key] = d;
                }
                d.mb.verts.Clear(); d.mb.normals.Clear(); d.mb.colors.Clear(); d.mb.uvs.Clear(); d.mb.lamps.Clear(); d.mb.tris.Clear();
                look.seed = l.home.buildingId * 31 + 7;
                LaundryLines.Build(d.mb, new Vector3(an.centre.x, groundY(an.centre.x, an.centre.y), an.centre.y), an.along, an.outDir, look, scale);
                if (d.mesh != null) Gone(d.mesh);
                d.mesh = d.mb.ToMesh("Washing"); d.mesh.hideFlags = HideFlags.DontSave;
                d.go.GetComponent<MeshFilter>().sharedMesh = d.mesh;
                d.go.SetActive(true);
                if (ClothsShown == 0) { for (int j = 0; j < look.cloths; j++) if (look.pop[j] > 0.5f) ClothsShown++; StakesUp = look.stakes > 0.5f; }
            }
            stale.Clear();
            foreach (var kv in drawn) if (!live.Contains(kv.Key)) stale.Add(kv.Key);
            foreach (var k in stale) { var d = drawn[k]; if (d.go != null) Gone(d.go); if (d.mesh != null) Gone(d.mesh); drawn.Remove(k); }
        }

        /// <summary>The line's look at <paramref name="now"/>, or false when nothing is drawn (before the chore, after the take-in or the fade).</summary>
        public static bool Look(LaundryLine l, ViewTime now, float time, out LineLook look)
        {
            look = null;
            if (now.hour < l.start) return false;
            float rs = LifeClock.RealSecondsBetween(l.start, now.hour);
            return LookReal(rs, LifeClock.RealSecondsBetween(l.start, l.leave), LifeClock.RealSecondsBetween(l.leave, l.takeAt),
                LifeClock.RealSecondsBetween(l.start, l.fadeAt), l.cloths, l.Taken, time, out look);
        }

        /// <summary>The same in real seconds, so AnimLab can draw a clip: <paramref name="rs"/> seconds since the chore began, the chore lasting
        /// <paramref name="chore"/>, the stay <paramref name="stay"/> after it, the quiet fade at <paramref name="fadeAt"/> seconds from the start (when
        /// nobody <paramref name="taken"/> it in).</summary>
        public static bool LookReal(float rs, float chore, float stay, float fadeAt, int cloths, bool taken, float time, out LineLook look)
        {
            look = new LineLook { cloths = cloths, time = time };
            if (rs < 0f) return false;
            for (int j = 0; j < cloths; j++) { look.pop[j] = 0f; look.flat[j] = 0f; look.jolt[j] = 0f; }
            if (rs < chore)
            {   // going up: stakes and cord pop in, one cloth at each pin
                look.stakes = Easing.Apply(Ease.Back, Mathf.Clamp01(rs / 0.25f));
                look.dry = Laundry.WetBrightness;
                int pinned = Laundry.PinnedBy(rs, cloths);
                for (int j = 0; j < cloths; j++)
                {
                    look.pop[j] = rs >= Laundry.PinnedAt(j) ? Mathf.Clamp01((rs - Laundry.PinnedAt(j)) / 0.1f + 0.01f) : 0f;
                    look.flat[j] = Mathf.Clamp01((rs - Laundry.FlatAt(j)) / 0.45f);
                    float since = rs - Laundry.PinnedAt(j);
                    look.jolt[j] = since >= 0f && since < 0.2f ? 1f - since / 0.2f : 0f;
                }
                look.basket = true; look.inBasket = cloths - pinned;
                look.sway = pinned > 0 ? 3f * (pinned / (float)cloths) : 0f;
                return true;
            }
            for (int j = 0; j < cloths; j++) { look.pop[j] = 1f; look.flat[j] = 1f; }
            look.stakes = 1f;
            look.dry = Mathf.Lerp(Laundry.WetBrightness, 1f, Mathf.Clamp01((rs - chore) / Mathf.Max(0.01f, stay)));
            look.sway = 4f;
            if (taken)
            {
                float ts = rs - chore - stay;
                if (ts < 0f) return true;
                float len = Laundry.TakeInSeconds(cloths);
                if (ts >= len) return false;
                for (int j = 0; j < cloths; j++)
                    look.pop[j] = 1f - Mathf.Clamp01((ts - (Laundry.WalkOut + (j + 0.6f) * Laundry.PluckSeconds)) / 0.12f);
                look.stakes = 1f - Mathf.Clamp01((ts - (Laundry.WalkOut + cloths * Laundry.PluckSeconds)) / Laundry.StakesOut);
                look.sway = 4f * Mathf.Max(0.2f, 1f - ts / len);
                return look.stakes > 0.01f;
            }
            float fs = rs - fadeAt;
            if (fs < 0f) return true;
            if (fs >= Laundry.FadeSeconds) return false;
            look.fade = Mathf.SmoothStep(0f, 1f, fs / Laundry.FadeSeconds);
            return true;
        }

        public void Clear()
        {
            foreach (var d in drawn.Values) { if (d.go != null) Gone(d.go); if (d.mesh != null) Gone(d.mesh); }
            drawn.Clear(); live.Clear();
        }
    }

    /// <summary>#705 debug (SliceDebugPanel's "// #705" block): "Laundry now" and "Take in the washing".</summary>
    public static class LaundryCheats
    {
        /// <summary>The home nearest the middle of what the camera looks at does the chore at once; the stay is shown at 16 s.</summary>
        public static string LaundryNow()
        {
            var pv = PeopleView.Find();
            if (pv == null || pv.Planner == null) return "no people";
            float x = 0f, z = 0f;
            var cam = Camera.main;
            if (cam != null)
            {
                var ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
                var plane = new Plane(Vector3.up, pv.transform.position);
                if (plane.Raycast(ray, out float enter)) { var l = pv.transform.InverseTransformPoint(ray.GetPoint(enter)); x = l.x; z = l.z; }
            }
            int key = pv.Planner.DebugLaundryPick(x, z);
            if (key < 0) return "nobody has a home yet";
            if (!pv.Planner.DebugLaundryNow(pv.Now, key)) return "no home for the washing";
            return $"laundry: person {key} hangs the washing now ({Laundry.ChoreHours:0.0} view hours), 16 s on the line, then taken in";
        }

        /// <summary>Ends the stay now: the doer takes the washing in.</summary>
        public static string TakeInNow()
        {
            var pv = PeopleView.Find();
            if (pv == null || pv.Planner == null) return "no people";
            return pv.Planner.DebugTakeInNow(pv.Now) ? "the washing comes in now" : "no cheat washing on the line (press Laundry now first)";
        }
    }
}
