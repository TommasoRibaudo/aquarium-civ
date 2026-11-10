using System.Collections.Generic;
using AquariumCiv.Sim.Eco;
using UnityEngine;

namespace AquariumCiv.View
{
    /// <summary>
    /// P9 #424 / #423: what placed life looks like before its everyday view exists. The founding groups' layout (shared by the ghost and
    /// the arrival, so the ghost shows where they will land), pale silhouettes of the real models for the ghost (bushes, the rabbit doll,
    /// the pond's ducks, fish shadows, and P0's quadruped for deer and wolves).
    /// <para>Deer and wolves are <see cref="QuadDoll"/>, P0's shared quadruped (#425).</para>
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

        /// <summary>The group as one mesh of pale models (#516: their own colours washed pale and softly shaded, <see cref="GhostLook.Pale"/>),
        /// centred on the origin with the ground at y 0; <paramref name="edge"/>: the same shapes a little larger in white, drawn behind in a
        /// darker tint, so each reads as an outlined shape on any ground.</summary>
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
            if (edge) GhostLook.Flat(mb); else GhostLook.Pale(mb);
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
                case LifeSpecies.Fish: FishShape(mb, 1f, FishGhost); break;
                default: QuadDoll.Shape(mb, g.quad); break;
            }
        }

        /// <summary>#516 the fish's ghost colour (the pond's fish are shadows; a pale slate reads as fish on the water).</summary>
        static readonly Color FishGhost = new(0.42f, 0.52f, 0.58f);

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

        /// <summary>A mesh's triangles into the builder, keeping its vertex colours (#516: the ghost shows them, washed pale).</summary>
        static void Append(MeshBuilder mb, Mesh m, Vector3 at = default)
        {
            var v = m.vertices; var t = m.triangles; var col = m.colors;
            bool has = col != null && col.Length == v.Length;
            for (int i = 0; i < t.Length; i += 3)
            {
                int a = t[i], b = t[i + 1], c = t[i + 2];
                if (has) mb.TriC(v[a] + at, v[b] + at, v[c] + at, col[a].gamma, col[b].gamma, col[c].gamma); // the builder stores linear: undo it, it adds it back
                else mb.Tri(v[a] + at, v[b] + at, v[c] + at, Color.white);
            }
        }
    }
}
