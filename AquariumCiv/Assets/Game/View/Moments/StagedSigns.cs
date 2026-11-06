using System;
using AquariumCiv.Life;
using AquariumCiv.Sim;
using UnityEngine;

namespace AquariumCiv.View
{
    /// <summary>
    /// t1-moments: the signs a staged moment puts in the box so it reads in three seconds with the sound off (tester run 1: "a crowd
    /// at the fire with note bubbles"). The wedding: a flower arch the people set up at the vow point (it rises as they gather) and
    /// petals strewn under it on the join. Code-built from the palette, one mesh each, dropped when the moment ends. The heart
    /// bubbles over the couple and the lifted newborn are <see cref="RitualBubbles.ForHeldMoment"/>'s.
    /// </summary>
    public sealed class StagedSign : MonoBehaviour
    {
        public const float ArchHalf = 0.17f, ArchH = 0.2f, PostH = 0.19f;
        /// <summary>The seconds (from the moment's start) the arch begins to rise, and the petals fall.</summary>
        public const float RiseAt = 3f, RiseSeconds = 1.4f, PetalSeconds = 0.6f;

        public Func<float> Seconds;
        public float petalsAt = 12f;
        Transform arch, petals;
        Mesh archMesh, petalMesh;

        /// <summary>The wedding's arch and petals at the vow point (box x, z), the plan's geometry giving the way the couple face.</summary>
        public static StagedSign Wedding(BoxView box, MomentStagePlan plan, Func<float> seconds)
        {
            if (box == null || plan == null) return null;
            float cx = plan.centreX, cz = plan.centreZ;
            var go = new GameObject("WeddingSign") { hideFlags = HideFlags.DontSave };
            go.transform.SetParent(box.transform, false);
            go.transform.localPosition = new Vector3(cx, box.GroundHeight(cx, cz), cz);
            go.transform.localRotation = Quaternion.LookRotation(new Vector3(plan.geo.nx, 0f, plan.geo.nz), Vector3.up); // local +z towards the glass, +x across it
            var s = go.AddComponent<StagedSign>();
            s.Seconds = seconds;
            foreach (var m in plan.members)
                foreach (var st in m.steps)
                    if (st.beat == StageBeat.Join) { s.petalsAt = st.t0; break; }
            s.archMesh = BuildArch();
            s.petalMesh = BuildPetals(plan.seed);
            s.arch = Child(go.transform, "Arch", s.archMesh, box.SolidMaterial);
            s.petals = Child(go.transform, "Petals", s.petalMesh, box.SolidMaterial);
            s.arch.localScale = Vector3.zero;
            s.petals.gameObject.SetActive(false);
            return s;
        }

        static Transform Child(Transform parent, string name, Mesh mesh, Material mat)
        {
            var c = new GameObject(name) { hideFlags = HideFlags.DontSave };
            c.transform.SetParent(parent, false);
            c.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = c.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return c.transform;
        }

        /// <summary>Two timber posts, a leafy half-ring over the couple, flowers all along it and a bunch at the crown. It stands in the
        /// plane across the view, so it frames the two of them as they say their vows.</summary>
        public static Mesh BuildArch()
        {
            var mb = new MeshBuilder(370) { jitter = 0f };
            var leaf = new[] { Palette.LeafA, Palette.LeafC, Palette.LeafB };
            var fl = Palette.Flowers;
            float cy = PostH; // the ring's centre sits at the posts' tops
            for (int side = -1; side <= 1; side += 2)
                mb.Box(new Vector3(side * ArchHalf, PostH * 0.5f, 0f), new Vector3(0.03f, PostH, 0.03f), Palette.Timber);
            const int n = 14;
            for (int i = 0; i < n; i++)
            {
                float a0 = Mathf.PI * i / n, a1 = Mathf.PI * (i + 1) / n, am = (a0 + a1) * 0.5f;
                var p0 = new Vector3(Mathf.Cos(a0) * ArchHalf, cy + Mathf.Sin(a0) * ArchH, 0f);
                var p1 = new Vector3(Mathf.Cos(a1) * ArchHalf, cy + Mathf.Sin(a1) * ArchH, 0f);
                var mid = (p0 + p1) * 0.5f;
                float len = (p1 - p0).magnitude * 1.15f;
                float deg = Mathf.Atan2(p1.y - p0.y, p1.x - p0.x) * Mathf.Rad2Deg;
                mb.Box(mid, new Vector3(len, 0.032f, 0.032f), leaf[i % 3], Quaternion.Euler(0f, 0f, deg));
                // a big flower every other step, alternating colours, bulging out front so it faces the camera
                if (i % 2 == 0) mb.Ico(mid + new Vector3(0f, 0f, 0.012f), 0.026f, Vector3.one, fl[(i / 2) % fl.Length], 0.1f);
                else mb.Ico(mid + new Vector3(0f, 0.012f, 0.014f), 0.017f, Vector3.one, fl[(i / 2 + 2) % fl.Length], 0.1f);
            }
            // the bunch where the two halves meet, and a bloom low on each post
            mb.Ico(new Vector3(0f, cy + ArchH + 0.012f, 0.014f), 0.034f, Vector3.one, fl[2], 0.1f);
            mb.Ico(new Vector3(0f, cy + ArchH + 0.034f, 0.014f), 0.022f, Vector3.one, fl[1], 0.1f);
            for (int side = -1; side <= 1; side += 2)
                mb.Ico(new Vector3(side * ArchHalf, 0.035f, 0.02f), 0.026f, Vector3.one, fl[side < 0 ? 0 : 3], 0.1f);
            return mb.ToMesh("WeddingArch");
        }

        /// <summary>Petals flat on the ground in a ring round the vow point (the couple stand inside it).</summary>
        public static Mesh BuildPetals(int seed)
        {
            var mb = new MeshBuilder(371) { jitter = 0f };
            var fl = Palette.Flowers;
            var rng = new System.Random(seed ^ 0x370);
            for (int i = 0; i < 46; i++)
            {
                float a = (float)(rng.NextDouble() * Math.PI * 2.0), r = 0.04f + (float)rng.NextDouble() * 0.2f;
                var c = new Vector3(Mathf.Cos(a) * r * 1.2f, 0.006f, Mathf.Sin(a) * r * 0.9f);
                float rot = (float)(rng.NextDouble() * Math.PI), w = 0.022f + (float)rng.NextDouble() * 0.014f;
                var u = new Vector3(Mathf.Cos(rot), 0f, Mathf.Sin(rot)) * w;
                var v = new Vector3(-Mathf.Sin(rot), 0f, Mathf.Cos(rot)) * (w * 0.6f);
                var col = fl[i % fl.Length];
                mb.Tri(c + u, c + v, c - v, col);
                mb.Tri(c - u, c - v, c + v, col);
            }
            return mb.ToMesh("WeddingPetals");
        }

        void Update()
        {
            float t = Seconds != null ? Seconds() : 0f;
            // The arch rises as the guests gather, with a little overshoot, and stays.
            float k = Mathf.Clamp01((t - RiseAt) / RiseSeconds);
            float e = k <= 0f ? 0f : 1f + 1.7f * Mathf.Pow(k - 1f, 3f) + 0.7f * Mathf.Pow(k - 1f, 2f); // easeOutBack
            if (arch != null) arch.localScale = new Vector3(Mathf.Max(0.001f, e), Mathf.Max(0.001f, e), Mathf.Max(0.001f, e));
            // The petals fall on the join, once, and lie there.
            if (petals != null)
            {
                bool on = t >= petalsAt;
                if (petals.gameObject.activeSelf != on) petals.gameObject.SetActive(on);
                if (on)
                {
                    float p = Mathf.Clamp01((t - petalsAt) / PetalSeconds);
                    float s = 1f - Mathf.Pow(1f - p, 3f);
                    petals.localScale = new Vector3(0.4f + 0.6f * s, Mathf.Max(0.01f, s), 0.4f + 0.6f * s);
                    petals.localPosition = new Vector3(0f, (1f - s) * 0.2f, 0f); // they drift down from above
                }
            }
        }

        void OnDestroy()
        {
            if (archMesh != null) Destroy(archMesh);
            if (petalMesh != null) Destroy(petalMesh);
        }
    }
}
