using System.Collections.Generic;
using AquariumCiv.Sim.Eco;
using UnityEngine;

namespace AquariumCiv.View
{
    /// <summary>
    /// P9 #424 / #423: what placed life looks like before its everyday view exists. The founding groups' layout (shared by the ghost and
    /// the arrival, so the ghost shows where they will land), pale silhouettes of the real models for the ghost (bushes, the rabbit doll,
    /// the pond's ducks, fish shadows, and a placeholder four-legged doll for deer and wolves), and that placeholder doll itself.
    /// <para><b>Placeholder:</b> <see cref="QuadDoll"/> stands in for P0's shared quadruped (#425): boxes and icos posed by hand, a walk,
    /// a trot, head down to graze, an alert, lying down and a howl. #425 replaces it.</para>
    /// </summary>
    public static class LifeLooks
    {
        /// <summary>One member of a founding group: where it stands round the group's centre (box units, the group facing +x), which way
        /// it faces (degrees about y) and what it is (for the four-legged ones: <see cref="QuadKind"/>).</summary>
        public struct Member { public Vector2 at; public float yaw; public QuadKind quad; public float size; }

        /// <summary>B12: the founding groups. The thicket: three bushes 0.15 u apart; rabbits a pair; deer a stag and two does; ducks a
        /// pair; fish a small school; wolves two.</summary>
        public static Member[] Group(LifeSpecies s) => s switch
        {
            LifeSpecies.Thicket => new[] { M(-0.075f, -0.045f, 0f, size: 0.075f), M(0.075f, -0.045f, 0f, size: 0.07f), M(0f, 0.085f, 0f, size: 0.08f) },
            LifeSpecies.Rabbits => new[] { M(-0.05f, -0.01f, 80f), M(0.05f, 0.02f, 110f) },
            LifeSpecies.Deer => new[] { M(-0.02f, 0.06f, 90f, QuadKind.Stag), M(-0.13f, -0.06f, 75f, QuadKind.Doe), M(0.12f, -0.07f, 100f, QuadKind.Doe) },
            LifeSpecies.Ducks => new[] { M(-0.04f, 0f, 80f), M(0.05f, 0.03f, 110f) },
            LifeSpecies.Fish => new[] { M(-0.06f, 0f, 70f), M(0.0f, 0.04f, 95f), M(0.05f, -0.03f, 85f), M(0.09f, 0.03f, 60f), M(-0.01f, -0.05f, 100f) },
            _ => new[] { M(-0.08f, 0.0f, 85f, QuadKind.Wolf), M(0.08f, 0.03f, 95f, QuadKind.Wolf) },
        };

        static Member M(float x, float z, float yaw, QuadKind q = QuadKind.None, float size = 0f) => new Member { at = new Vector2(x, z), yaw = yaw, quad = q, size = size };

        // ---------------------------------------------------------------- the ghost's silhouettes
        static readonly Dictionary<LifeSpecies, Mesh> ghosts = new(), edges = new();

        /// <summary>The group as one mesh of pale silhouettes (white vertex colours: the material tints them), centred on the origin with
        /// the ground at y 0; <paramref name="edge"/>: the same shapes a little larger, drawn behind in a darker tint, so each reads as an
        /// outlined shape on any ground.</summary>
        public static Mesh Ghost(LifeSpecies s, BoxView box, bool edge)
        {
            var cache = edge ? edges : ghosts;
            if (cache.TryGetValue(s, out var m) && m != null) return m;
            var mb = new MeshBuilder(424) { jitter = 0f };
            foreach (var g in Group(s))
            {
                var c = new Vector3(g.at.x, 0f, g.at.y);
                var grow = edge ? new Vector3(1.22f, 1.12f, 1.22f) : Vector3.one;
                mb.M = Matrix4x4.TRS(c, Quaternion.Euler(0f, g.yaw, 0f), grow);
                BuildMember(mb, s, g, box);
            }
            mb.M = Matrix4x4.identity;
            for (int i = 0; i < mb.colors.Count; i++) mb.colors[i] = Color.white;
            m = mb.ToMesh($"LifeGhost_{s}{(edge ? "_edge" : "")}");
            cache[s] = m;
            return m;
        }

        /// <summary>One member's shape, in its own space (origin on the ground, facing +z).</summary>
        static void BuildMember(MeshBuilder mb, LifeSpecies s, Member g, BoxView box)
        {
            switch (s)
            {
                case LifeSpecies.Thicket: Pieces.Bush(mb, Vector3.zero, g.size, false); break;
                case LifeSpecies.Rabbits: Append(mb, RabbitShape(box)); break;
                case LifeSpecies.Ducks:
                    PondLife.DuckMeshes(out var body, out var head, out var tail);
                    Append(mb, body, Vector3.zero); Append(mb, head, PondLife.DuckHeadAt); Append(mb, tail, PondLife.DuckTailAt);
                    break;
                case LifeSpecies.Fish: FishShape(mb, 1f); break;
                default: QuadDoll.Shape(mb, g.quad); break;
            }
        }

        /// <summary>A small fish seen from above: a flat body and a tail fork, lying on the water (y 0), facing +z.</summary>
        public static void FishShape(MeshBuilder mb, float scale, Color? col = null)
        {
            var c = col ?? Color.white;
            float L = 0.045f * scale, W = 0.014f * scale;
            const int n = 10;
            var ring = new Vector3[n];
            for (int i = 0; i < n; i++)
            {
                float a = i / (float)n * Mathf.PI * 2f;
                float z = Mathf.Cos(a) * L * 0.5f, x = Mathf.Sin(a) * W * (Mathf.Cos(a) > 0f ? 1f : 0.8f);
                ring[i] = new Vector3(x, 0.001f, z);
            }
            var mid = new Vector3(0f, 0.001f, 0f);
            for (int i = 0; i < n; i++) mb.Tri(mid, ring[(i + 1) % n], ring[i], c);
            var t0 = new Vector3(0f, 0.001f, -L * 0.45f);
            mb.Tri(t0, new Vector3(W * 1.1f, 0.001f, -L * 0.85f), new Vector3(-W * 1.1f, 0.001f, -L * 0.85f), c);
        }

        static Mesh rabbitShape;

        /// <summary>The game's rabbit doll, sitting up, baked into one mesh (its shadow left out).</summary>
        static Mesh RabbitShape(BoxView box)
        {
            if (rabbitShape != null) return rabbitShape;
            var holder = new GameObject("RabbitBake") { hideFlags = HideFlags.HideAndDontSave };
            try
            {
                var d = RabbitDoll.Create(box, holder.transform, 0);
                d.transform.localPosition = Vector3.zero;
                d.Face(0f, 0f, true);
                d.Show(RabbitAct.SitUp, 0f, 0f);
                rabbitShape = Bake(d.transform, "Shade");
            }
            finally { Object.DestroyImmediate(holder); }
            return rabbitShape;
        }

        /// <summary>Every mesh under <paramref name="root"/> in the root's own space, as one mesh (parts named in <paramref name="skip"/>
        /// left out).</summary>
        public static Mesh Bake(Transform root, params string[] skip)
        {
            var parts = new List<CombineInstance>();
            foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null || System.Array.IndexOf(skip, mf.name) >= 0) continue;
                parts.Add(new CombineInstance { mesh = mf.sharedMesh, transform = root.worldToLocalMatrix * mf.transform.localToWorldMatrix * Matrix4x4.Scale(Vector3.one) });
            }
            var m = new Mesh { name = root.name + "Baked" };
            m.CombineMeshes(parts.ToArray(), true, true);
            // the doll's own scale is on its root: put it back in
            var v = m.vertices; var s = root.localScale;
            for (int i = 0; i < v.Length; i++) v[i] = Vector3.Scale(v[i], s);
            m.vertices = v;
            m.RecalculateBounds();
            return m;
        }

        static void Append(MeshBuilder mb, Mesh m, Vector3 at = default)
        {
            var v = m.vertices; var t = m.triangles;
            for (int i = 0; i < t.Length; i += 3) mb.Tri(v[t[i]] + at, v[t[i + 1]] + at, v[t[i + 2]] + at, Color.white);
        }
    }

    /// <summary>The placeholder four-legged kinds (P0, #425 replaces them).</summary>
    public enum QuadKind { None, Stag, Doe, Wolf }

    /// <summary>
    /// <b>Placeholder</b> for P0's shared quadruped (#425): a deer or a wolf from boxes and icos, posed by a few numbers. Parts: the body
    /// (with the neck), the head, four legs hinged at the hip and shoulder, the tail. Sizes from the P0 table (deer shoulder 0.15 u, stag
    /// 0.16, body 0.20; wolf shoulder 0.10, body 0.16), with the 1.5–2× of every animal in the box.
    /// </summary>
    public sealed class QuadDoll
    {
        public readonly GameObject go;
        readonly Transform body, head, tail;
        readonly Transform[] legs = new Transform[4];
        readonly QuadKind kind;
        readonly float leg;

        static readonly Color Fawn = new(0.66f, 0.47f, 0.30f), Belly = new(0.90f, 0.84f, 0.72f), Antler = new(0.86f, 0.78f, 0.62f);
        static readonly Color Grey = new(0.52f, 0.52f, 0.50f), Saddle = new(0.33f, 0.32f, 0.31f), Pale = new(0.80f, 0.79f, 0.75f);
        static readonly Color Eye = new(0.08f, 0.06f, 0.05f);

        static readonly Dictionary<(QuadKind, int), Mesh> meshes = new();

        /// <summary>Leg length (box units): the body's underside is this high when standing.</summary>
        public static float LegOf(QuadKind k) => k == QuadKind.Wolf ? 0.055f : k == QuadKind.Stag ? 0.085f : 0.08f;
        static float BodyLen(QuadKind k) => k == QuadKind.Wolf ? 0.15f : 0.18f;
        static float BodyH(QuadKind k) => k == QuadKind.Wolf ? 0.05f : 0.06f;

        public QuadDoll(QuadKind kind, Transform parent, Material mat)
        {
            this.kind = kind;
            leg = LegOf(kind);
            go = new GameObject(kind.ToString()) { hideFlags = HideFlags.DontSave };
            go.transform.SetParent(parent, false);
            body = Part(go.transform, "Body", MeshOf(kind, 0), mat, new Vector3(0f, leg, 0f));
            float L = BodyLen(kind), H = BodyH(kind);
            head = Part(body, "Head", MeshOf(kind, 1), mat, new Vector3(0f, H * (kind == QuadKind.Wolf ? 0.55f : 1.25f), L * 0.5f));
            tail = Part(body, "Tail", MeshOf(kind, 2), mat, new Vector3(0f, H * 0.35f, -L * 0.5f));
            for (int i = 0; i < 4; i++)
            {
                float x = (i % 2 == 0 ? -1f : 1f) * 0.018f, z = (i < 2 ? 1f : -1f) * L * 0.36f;
                legs[i] = Part(body, "Leg" + i, MeshOf(kind, 3), mat, new Vector3(x, 0f, z));
            }
        }

        static Transform Part(Transform parent, string name, Mesh m, Material mat, Vector3 at)
        {
            var g = new GameObject(name) { hideFlags = HideFlags.DontSave };
            g.transform.SetParent(parent, false);
            g.transform.localPosition = at;
            g.AddComponent<MeshFilter>().sharedMesh = m;
            var r = g.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return g.transform;
        }

        /// <summary>The kind's part meshes: 0 body (with neck), 1 head, 2 tail, 3 leg (hanging down from 0 to -leg).</summary>
        static Mesh MeshOf(QuadKind k, int part)
        {
            if (meshes.TryGetValue((k, part), out var m) && m != null) return m;
            var mb = new MeshBuilder(425 + part) { jitter = 0.02f };
            BuildPart(mb, k, part);
            m = mb.ToMesh($"Quad{k}{part}");
            meshes[(k, part)] = m;
            return m;
        }

        static void BuildPart(MeshBuilder mb, QuadKind k, int part)
        {
            bool wolf = k == QuadKind.Wolf;
            float L = BodyLen(k), H = BodyH(k), leg = LegOf(k);
            var coat = wolf ? Grey : Fawn;
            switch (part)
            {
                case 0:
                    mb.Ico(new Vector3(0f, H * 0.5f, 0f), L * 0.5f, new Vector3(0.36f * (wolf ? 0.85f : 0.8f), H / L * 1.05f, 1f), coat, 0.06f, wolf ? Pale : Belly);
                    if (wolf) mb.Ico(new Vector3(0f, H * 0.95f, -L * 0.05f), L * 0.3f, new Vector3(0.5f, 0.25f, 1f), Saddle, 0.05f); // the darker saddle
                    else mb.Ico(new Vector3(0f, H * 0.7f, -L * 0.47f), 0.022f, new Vector3(1f, 1f, 0.6f), Belly, 0.05f);          // the white rump
                    // the neck, up and forward
                    mb.Ico(new Vector3(0f, H * (wolf ? 0.7f : 1.0f), L * 0.42f), wolf ? 0.026f : 0.022f, new Vector3(1f, wolf ? 1.1f : 1.9f, 1.1f), coat, 0.05f);
                    break;
                case 1:
                    if (wolf)
                    {
                        mb.Ico(new Vector3(0f, 0.012f, 0.012f), 0.026f, new Vector3(1f, 0.9f, 1.1f), Grey, 0.05f, Pale);
                        mb.Ico(new Vector3(0f, 0.004f, 0.04f), 0.016f, new Vector3(0.8f, 0.65f, 1.5f), Pale, 0.04f);       // the long muzzle
                        mb.Ico(new Vector3(0f, 0.006f, 0.064f), 0.005f, Vector3.one, Eye, 0.02f);                          // nose
                        foreach (int sd in new[] { -1, 1 })
                        {
                            mb.Prism(new Vector3(sd * 0.012f, 0.028f, 0.006f), 0.008f, 0.0005f, 0.022f, 4, Saddle);        // pointed ears
                            mb.Ico(new Vector3(sd * 0.011f, 0.019f, 0.03f), 0.0035f, Vector3.one, Eye, 0.02f);
                        }
                    }
                    else
                    {
                        mb.Ico(new Vector3(0f, 0.012f, 0.012f), 0.022f, new Vector3(0.9f, 0.9f, 1.3f), Fawn, 0.05f, Belly); // long, slim
                        mb.Ico(new Vector3(0f, 0.004f, 0.036f), 0.009f, Vector3.one, Eye, 0.02f);                           // nose
                        foreach (int sd in new[] { -1, 1 })
                        {
                            mb.Ico(new Vector3(sd * 0.022f, 0.026f, 0.0f), 0.012f, new Vector3(1.2f, 0.45f, 0.7f), Fawn, 0.04f); // wide ears
                            mb.Ico(new Vector3(sd * 0.012f, 0.017f, 0.024f), 0.0035f, Vector3.one, Eye, 0.02f);
                            if (k == QuadKind.Stag)
                            {
                                // antlers: a beam up and back with two tines
                                var b0 = new Vector3(sd * 0.008f, 0.03f, 0.0f);
                                mb.Box(b0 + new Vector3(sd * 0.012f, 0.03f, -0.006f), new Vector3(0.006f, 0.065f, 0.006f), Antler, Quaternion.Euler(-15f, 0f, -sd * 28f));
                                mb.Box(b0 + new Vector3(sd * 0.02f, 0.05f, 0.01f), new Vector3(0.005f, 0.03f, 0.005f), Antler, Quaternion.Euler(35f, 0f, -sd * 10f));
                                mb.Box(b0 + new Vector3(sd * 0.03f, 0.066f, -0.004f), new Vector3(0.005f, 0.028f, 0.005f), Antler, Quaternion.Euler(-10f, 0f, -sd * 40f));
                            }
                        }
                    }
                    break;
                case 2:
                    if (wolf) mb.Ico(new Vector3(0f, -0.03f, -0.012f), 0.014f, new Vector3(0.8f, 2.6f, 0.9f), Grey, 0.05f, Saddle); // long, hanging
                    else mb.Ico(new Vector3(0f, 0.004f, -0.008f), 0.011f, new Vector3(0.9f, 1.3f, 0.6f), Belly, 0.05f);          // the short white flag
                    break;
                default:
                    mb.Box(new Vector3(0f, -leg * 0.5f, 0f), new Vector3(wolf ? 0.012f : 0.009f, leg, wolf ? 0.012f : 0.009f), coat);
                    mb.Box(new Vector3(0f, -leg + 0.004f, 0.002f), new Vector3(0.011f, 0.008f, 0.013f), wolf ? Saddle : Eye);
                    break;
            }
        }

        /// <summary>The standing doll as one mesh (the ghost's silhouette), facing +z, feet at y 0.</summary>
        public static void Shape(MeshBuilder mb, QuadKind k)
        {
            var root = mb.M;
            float L = BodyLen(k), H = BodyH(k), leg = LegOf(k);
            mb.M = root * Matrix4x4.Translate(new Vector3(0f, leg, 0f));
            BuildPart(mb, k, 0);
            var bodyM = mb.M;
            mb.M = bodyM * Matrix4x4.Translate(new Vector3(0f, H * (k == QuadKind.Wolf ? 0.55f : 1.25f), L * 0.5f)); BuildPart(mb, k, 1);
            mb.M = bodyM * Matrix4x4.Translate(new Vector3(0f, H * 0.35f, -L * 0.5f)); BuildPart(mb, k, 2);
            for (int i = 0; i < 4; i++)
            {
                float x = (i % 2 == 0 ? -1f : 1f) * 0.018f, z = (i < 2 ? 1f : -1f) * L * 0.36f;
                mb.M = bodyM * Matrix4x4.Translate(new Vector3(x, 0f, z)); BuildPart(mb, k, 3);
            }
            mb.M = root;
        }

        /// <summary>
        /// Pose: where (parent-local) and which way, the gait phase (radians; NaN = standing still), how fast the legs swing (0 walk, 1
        /// trot), the head's pitch (degrees, + = down to graze, − = up to howl), the head's yaw, how far it has lain down (0..1) and sat
        /// (0..1, the howl's sit), the tail's angle (degrees up).
        /// </summary>
        public void Pose(Vector3 at, float yawDeg, float phase, float trot, float headPitch, float headYaw, float lie, float sit, float tailUp, float scale = 1f)
        {
            var t = go.transform;
            t.localPosition = at;
            t.localRotation = Quaternion.Euler(0f, yawDeg, 0f);
            t.localScale = Vector3.one * scale;
            bool moving = !float.IsNaN(phase);
            float swing = moving ? Mathf.Lerp(26f, 34f, trot) : 0f;
            float bob = moving ? Mathf.Abs(Mathf.Sin(phase)) * Mathf.Lerp(0.004f, 0.008f, trot) : 0f;
            float down = Mathf.Max(lie, sit * 0.55f);
            body.localPosition = new Vector3(0f, leg * (1f - 0.8f * lie) - sit * leg * 0.25f + bob, 0f);
            body.localRotation = Quaternion.Euler(-sit * 32f, 0f, 0f);
            for (int i = 0; i < 4; i++)
            {
                bool front = i < 2;
                // walk: four beats (each leg a quarter apart); trot: diagonal pairs together
                float off = Mathf.Lerp(i * Mathf.PI * 0.5f, (i == 0 || i == 3) ? 0f : Mathf.PI, trot);
                float a = moving ? Mathf.Sin(phase + off) * swing : 0f;
                float fold = front ? lie * 95f : Mathf.Max(lie, sit) * -100f;
                legs[i].localRotation = Quaternion.Euler(a + fold, 0f, 0f);
            }
            head.localRotation = Quaternion.Euler(headPitch * (1f - down * 0.5f), headYaw, 0f);
            tail.localRotation = Quaternion.Euler(-tailUp, 0f, 0f);
        }

        public void Destroy() { if (go != null) Object.Destroy(go); }
    }
}
