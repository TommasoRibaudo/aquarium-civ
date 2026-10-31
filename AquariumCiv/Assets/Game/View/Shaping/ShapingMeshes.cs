using System.Collections.Generic;
using AquariumCiv.View.Shaping;
using UnityEngine;
using UnityEngine.Rendering;

namespace AquariumCiv.View
{
    /// <summary>
    /// The Keeper's shapings' meshes in the style kit (#85; game-definition §6): low-poly, palette colours, the same
    /// builders as the town (<see cref="MeshBuilder"/>, <see cref="Pieces"/>). Solid parts are built once and shared; the
    /// see-through parts (<see cref="FxMesh"/>) are rewritten by <see cref="ShapingView"/> while they move.
    /// </summary>
    public static class ShapingMeshes
    {
        static readonly Dictionary<string, Mesh> Cache = new();

        static Mesh Cached(string key, System.Action<MeshBuilder> build, int seed = 1)
        {
            if (Cache.TryGetValue(key, out var m) && m != null) return m;
            var mb = new MeshBuilder(seed) { jitter = 0.02f };
            build(mb);
            m = mb.ToMesh("Shaping_" + key);
            m.hideFlags = HideFlags.DontSave;
            Cache[key] = m;
            return m;
        }

        // ---------------------------------------------------------------- rain

        /// <summary>A rain cloud: a flat-bottomed cluster of puffs (white; a storm's is slate grey), centred on the origin.</summary>
        public static Mesh Cloud(bool storm) => Cached(storm ? "cloud_storm" : "cloud", mb =>
        {
            var top = storm ? Color.Lerp(Palette.Slate, Palette.Plaster, 0.35f) : Palette.Plaster;
            var under = storm ? Palette.Slate : Color.Lerp(Palette.Plaster, Palette.Slate, 0.3f);
            mb.Ico(new Vector3(0f, 0f, 0f), 0.2f, new Vector3(1.5f, 0.62f, 1.1f), top, 0.15f, under);
            mb.Ico(new Vector3(0.17f, 0.05f, 0.05f), 0.15f, new Vector3(1.1f, 0.8f, 1f), top, 0.15f, under);
            mb.Ico(new Vector3(-0.16f, 0.04f, -0.04f), 0.14f, new Vector3(1.1f, 0.8f, 1f), top, 0.15f, under);
            mb.Ico(new Vector3(0.02f, 0.1f, -0.06f), 0.13f, Vector3.one, top, 0.15f, under);
            mb.Ico(new Vector3(-0.05f, 0.03f, 0.13f), 0.12f, new Vector3(1.2f, 0.7f, 1f), top, 0.15f, under);
        }, storm ? 7 : 3);

        /// <summary>A tuft of new sprouts, about 0.05 u tall at full growth.</summary>
        public static Mesh Sprouts(int variant) => Cached("sprouts" + variant, mb =>
        {
            for (int k = 0; k < 3; k++)
            {
                float a = k * 2.2f + variant;
                var g = new Vector3(Mathf.Cos(a) * 0.018f, 0f, Mathf.Sin(a) * 0.018f);
                Pieces.GrassTuft(mb, g, 0.045f + 0.012f * k, k == 1 ? Palette.LeafC : Palette.Sprout);
            }
        }, 11 + variant);

        // ---------------------------------------------------------------- grove

        /// <summary>A grove tree at full size (scaled by its growth). Woods: the town's broadleaf; blossom: pale pink canopy;
        /// orchard: a smaller round fruit tree, with red fruit when <paramref name="fruit"/>.</summary>
        public static Mesh Tree(GroveStyle style, bool blossom, bool fruit, int variant) =>
            Cached($"tree_{style}_{blossom}_{fruit}_{variant}", mb =>
            {
                if (style == GroveStyle.Woods && !blossom) { Pieces.Tree(mb, Vector3.zero, 0.85f, variant == 3); return; }
                bool orchard = style == GroveStyle.Orchard;
                float s = orchard ? 0.7f : 0.85f;
                var g = Vector3.down * 0.02f;
                mb.Prism(g, 0.04f * s, 0.03f * s, 0.3f * s, 6, Palette.Bark);
                var leaf = variant % 2 == 0 ? Palette.LeafA : Palette.LeafC;
                var crown = blossom ? Color.Lerp(Palette.Flowers[2], Palette.Plaster, 0.35f) : leaf;
                mb.sway = 1f;
                mb.Ico(g + Vector3.up * 0.4f * s, 0.21f * s, new Vector3(1.15f, 0.9f, 1.15f), crown, 0.16f, blossom ? Palette.Flowers[2] : Palette.LeafB);
                mb.Ico(g + new Vector3(0.09f, 0.5f, 0.05f) * s, 0.13f * s, Vector3.one, blossom ? Palette.Plaster : crown, 0.16f);
                if (fruit)
                    for (int k = 0; k < 7; k++)
                    {
                        float a = k * 0.9f + variant;
                        var p = g + new Vector3(Mathf.Cos(a) * 0.2f, 0.33f + (k % 3) * 0.06f, Mathf.Sin(a) * 0.2f) * s;
                        mb.Ico(p, 0.024f, Vector3.one, k % 3 == 0 ? Palette.Accents[1] : Palette.Accents[0], 0.1f);
                    }
                mb.sway = 0f;
            }, 101 + variant * 13 + (int)style * 7);

        // ---------------------------------------------------------------- channel

        /// <summary>A little undershot water wheel for the mill race (spins about local x), axle at the origin.</summary>
        public static Mesh WaterWheel() => Cached("wheel", mb =>
        {
            var rot = Quaternion.Euler(0f, 0f, 90f);
            mb.Prism(new Vector3(-0.025f, 0f, 0f), 0.075f, 0.075f, 0.05f, 10, Palette.WoodLight, rot, true, true, Palette.Timber);
            for (int k = 0; k < 8; k++)
            {
                var r = Quaternion.Euler(k * 45f, 0f, 0f);
                mb.Box(r * new Vector3(0f, 0.085f, 0f), new Vector3(0.06f, 0.035f, 0.01f), Palette.Timber, r);
            }
            mb.Box(Vector3.zero, new Vector3(0.11f, 0.012f, 0.012f), Palette.Iron);
        });

        /// <summary>A sluice board across a ditch (the village's irrigation).</summary>
        public static Mesh Sluice() => Cached("sluice", mb =>
        {
            mb.Box(new Vector3(0f, 0.015f, 0f), new Vector3(0.09f, 0.03f, 0.012f), Palette.WoodLight);
            foreach (int s in new[] { -1, 1 }) mb.Box(new Vector3(s * 0.05f, 0.03f, 0f), new Vector3(0.012f, 0.06f, 0.012f), Palette.Timber);
        });

        // ---------------------------------------------------------------- herd

        /// <summary>An animal facing +z, feet at the origin, in the scale of the peg dolls.</summary>
        public static Mesh Animal(HerdSpecies s) => Cached("animal_" + s, mb =>
        {
            switch (s)
            {
                case HerdSpecies.Deer:
                {
                    var body = Palette.Hide; var dark = Palette.Bark;
                    mb.Ico(new Vector3(0f, 0.075f, 0f), 0.04f, new Vector3(0.85f, 0.8f, 1.5f), body, 0.12f, Palette.Canvas);
                    foreach (int x in new[] { -1, 1 }) foreach (int z in new[] { -1, 1 })
                        mb.Box(new Vector3(x * 0.017f, 0.028f, z * 0.035f), new Vector3(0.009f, 0.056f, 0.009f), dark);
                    mb.Box(new Vector3(0f, 0.11f, 0.06f), new Vector3(0.018f, 0.05f, 0.018f), body, Quaternion.Euler(30f, 0f, 0f));
                    mb.Ico(new Vector3(0f, 0.135f, 0.082f), 0.019f, new Vector3(0.9f, 0.9f, 1.4f), body, 0.1f);
                    foreach (int x in new[] { -1, 1 }) // antlers
                        mb.Box(new Vector3(x * 0.012f, 0.165f, 0.074f), new Vector3(0.005f, 0.04f, 0.005f), Palette.Thatch, Quaternion.Euler(0f, 0f, x * 25f));
                    mb.Ico(new Vector3(0f, 0.09f, -0.06f), 0.01f, Vector3.one, Palette.Plaster, 0.1f); // the white tail
                    break;
                }
                case HerdSpecies.Goat:
                case HerdSpecies.Sheep:
                {
                    bool sheep = s == HerdSpecies.Sheep;
                    var coat = sheep ? Palette.Plaster : Color.Lerp(Palette.Canvas, Palette.Hide, 0.45f);
                    mb.Ico(new Vector3(0f, 0.055f, 0f), sheep ? 0.045f : 0.036f, sheep ? new Vector3(1f, 0.85f, 1.3f) : new Vector3(0.85f, 0.8f, 1.4f), coat, sheep ? 0.25f : 0.12f);
                    foreach (int x in new[] { -1, 1 }) foreach (int z in new[] { -1, 1 })
                        mb.Box(new Vector3(x * 0.016f, 0.017f, z * 0.03f), new Vector3(0.009f, 0.034f, 0.009f), Palette.Door);
                    mb.Ico(new Vector3(0f, 0.075f, 0.058f), 0.018f, new Vector3(0.9f, 0.9f, 1.3f), sheep ? Palette.Door : coat, 0.1f);
                    if (!sheep) foreach (int x in new[] { -1, 1 }) mb.Box(new Vector3(x * 0.008f, 0.098f, 0.05f), new Vector3(0.005f, 0.025f, 0.005f), Palette.RockDark, Quaternion.Euler(-30f, 0f, 0f));
                    break;
                }
                default: // fish: a little silver-orange body and a tail, just under the surface
                {
                    var col = Color.Lerp(Palette.Accents[5], Palette.RockLight, 0.35f);
                    mb.Ico(Vector3.zero, 0.022f, new Vector3(0.55f, 0.6f, 1.4f), col, 0.08f, Palette.RockLight);
                    mb.Tri(new Vector3(0f, 0f, -0.026f), new Vector3(0f, 0.012f, -0.045f), new Vector3(0f, -0.012f, -0.045f), col);
                    mb.Tri(new Vector3(0f, 0f, -0.026f), new Vector3(0f, -0.012f, -0.045f), new Vector3(0f, 0.012f, -0.045f), col);
                    break;
                }
            }
        }, 31 + (int)s);

        // ---------------------------------------------------------------- see-through, rewritten each frame

        /// <summary>A dynamic see-through mesh (unlit, vertex colour × the renderer's tint, alpha-blended).</summary>
        public sealed class FxMesh
        {
            public readonly List<Vector3> v = new();
            public readonly List<Color> c = new();
            public readonly List<int> t = new();
            public readonly Mesh mesh;

            public FxMesh(string name) { mesh = new Mesh { name = name, hideFlags = HideFlags.DontSave }; mesh.MarkDynamic(); }

            public void Clear() { v.Clear(); c.Clear(); t.Clear(); }

            public void Quad(Vector3 a, Vector3 b, Vector3 d, Vector3 e, Color col)
            {
                int o = v.Count;
                var lin = MeshBuilder.Linear(col);
                v.Add(a); v.Add(b); v.Add(d); v.Add(e);
                c.Add(lin); c.Add(lin); c.Add(lin); c.Add(lin);
                t.Add(o); t.Add(o + 1); t.Add(o + 2); t.Add(o); t.Add(o + 2); t.Add(o + 3);
            }

            /// <summary>A flat ring (annulus) on the xz plane at height y.</summary>
            public void Ring(Vector3 centre, float r0, float r1, int sides, Color inner, Color outer)
            {
                int o = v.Count;
                var li = MeshBuilder.Linear(inner); var lo = MeshBuilder.Linear(outer);
                for (int i = 0; i <= sides; i++)
                {
                    float a = i * Mathf.PI * 2f / sides;
                    var d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                    v.Add(centre + d * r0); c.Add(li);
                    v.Add(centre + d * r1); c.Add(lo);
                }
                for (int i = 0; i < sides; i++)
                {
                    int k = o + i * 2;
                    t.Add(k); t.Add(k + 1); t.Add(k + 3); t.Add(k); t.Add(k + 3); t.Add(k + 2);
                }
            }

            /// <summary>An open cylinder (a soft beam): <paramref name="bottom"/> colour at the base fading to <paramref name="top"/>.</summary>
            public void Beam(Vector3 baseCentre, float r, float h, int sides, Color bottom, Color top)
            {
                int o = v.Count;
                var lb = MeshBuilder.Linear(bottom); var lt = MeshBuilder.Linear(top);
                for (int i = 0; i <= sides; i++)
                {
                    float a = i * Mathf.PI * 2f / sides;
                    var d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * r;
                    v.Add(baseCentre + d); c.Add(lb);
                    v.Add(baseCentre + d * 0.7f + Vector3.up * h); c.Add(lt);
                }
                for (int i = 0; i < sides; i++)
                {
                    int k = o + i * 2;
                    t.Add(k); t.Add(k + 1); t.Add(k + 3); t.Add(k); t.Add(k + 3); t.Add(k + 2);
                }
            }

            public void Apply()
            {
                mesh.Clear();
                mesh.SetVertices(v);
                mesh.SetColors(c);
                mesh.SetTriangles(t, 0);
                mesh.RecalculateBounds();
            }
        }

        /// <summary>A renderer for an <see cref="FxMesh"/> under <paramref name="parent"/>.</summary>
        public static MeshRenderer FxRenderer(Transform parent, string name, FxMesh fx, Material mat)
        {
            var go = new GameObject(name) { hideFlags = HideFlags.DontSave };
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = fx.mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            return r;
        }

        /// <summary>A solid shared-mesh part under <paramref name="parent"/>.</summary>
        public static Transform Solid(Transform parent, string name, Mesh mesh, Material mat, bool shadows = true)
        {
            var go = new GameObject(name) { hideFlags = HideFlags.DontSave };
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            return go.transform;
        }
    }
}
