using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace AquariumCiv.View
{
    /// <summary>
    /// Flat-shaded low-poly mesh writer (ported from the style-kit spike). Every face gets its own vertices (crisp facets),
    /// a vertex colour from the palette (alpha = baked AO, 1 = open), UV0 = (wind sway weight, emission) and
    /// UV1 = (lamp flag, lamp phase). One material (<c>AquariumCiv/View/Stylized</c>) draws everything. Shapes are written
    /// through <see cref="M"/>, so callers place parts in local space.
    /// </summary>
    public class MeshBuilder
    {
        public readonly List<Vector3> verts = new();
        public readonly List<Vector3> normals = new();
        public readonly List<Color> colors = new();
        public readonly List<Vector2> uvs = new();
        public readonly List<Vector2> lamps = new();
        public readonly List<int> tris = new();
        public Matrix4x4 M = Matrix4x4.identity;
        /// <summary>Per-face brightness jitter (0.04 = ±4 %), so flat faces don't look machine-made.</summary>
        public float jitter = 0.035f;
        /// <summary>Written into UV0 of every new face: wind sway weight and emission (0 = lit, 1 = full glow).</summary>
        public float sway, emission;
        /// <summary>
        /// Below 0: not a lamp. 0..1: the face is a lamp (a window, a lantern) with this phase; the shader lights it with
        /// the global night level (<see cref="NightLamps"/>), each lamp at its own moment, flickering as it comes on.
        /// </summary>
        public float lamp = -1f;
        /// <summary>
        /// Flame vertices (the fire pit): with <see cref="flameHeight"/> above 0 every new vertex gets UV1 = (2 + height
        /// weight, <see cref="flamePhase"/>); the weight is 0 at <see cref="flameBaseY"/> and ~1 at flameBaseY + flameHeight.
        /// The Stylized shader stretches and wobbles them by it (no extra vertex stream, no CPU cost).
        /// </summary>
        public float flameBaseY, flameHeight, flamePhase;
        System.Random rng;

        public MeshBuilder(int seed = 1) { rng = new System.Random(seed); }

        // Builders with their list capacity kept: a chunk rebuild makes thousands of throwaway buffers otherwise, and the GC
        // pause they bring lands inside a time-lapse step (#78, user 2026-10-30: a scrub step never hitches).
        static readonly System.Collections.Concurrent.ConcurrentBag<MeshBuilder> Pool = new();
        const int PoolMax = 256;

        /// <summary>A clean builder from the pool (same as <c>new MeshBuilder(seed)</c>); hand it back with <see cref="Free"/>.
        /// Safe from worker threads.</summary>
        public static MeshBuilder Rent(int seed)
        {
            if (!Pool.TryTake(out var b)) return new MeshBuilder(seed);
            b.rng = new System.Random(seed);
            return b;
        }

        /// <summary>Back to the pool: don't use this builder again.</summary>
        public void Free()
        {
            verts.Clear(); normals.Clear(); colors.Clear(); uvs.Clear(); lamps.Clear(); tris.Clear();
            M = Matrix4x4.identity;
            jitter = 0.035f; sway = emission = 0f; lamp = -1f;
            flameBaseY = flameHeight = flamePhase = 0f;
            if (Pool.Count < PoolMax) Pool.Add(this);
        }

        /// <summary><see cref="Append"/>, then <see cref="Free"/>s <paramref name="src"/>.</summary>
        public void AppendFree(MeshBuilder src) { Append(src); src.Free(); }

        /// <summary>sRGB to linear in managed code (Color.linear is a native call), so builders can run on worker
        /// threads (BoxView builds terrain bands in parallel).</summary>
        static float Lin(float c) => c <= 0.04045f ? c / 12.92f : Mathf.Pow((c + 0.055f) / 1.055f, 2.4f);
        public static Color Linear(Color c) => new(Lin(c.r), Lin(c.g), Lin(c.b), c.a);

        public int VertexCount => verts.Count;
        public int TriangleCount => tris.Count / 3;

        void Add(Vector3 p, Vector3 n, Color linear)
        {
            verts.Add(p); normals.Add(n); colors.Add(linear);
            uvs.Add(new Vector2(sway, emission));
            if (flameHeight > 0f) lamps.Add(new Vector2(2f + Mathf.Clamp01((p.y - flameBaseY) / flameHeight) * 0.99f, flamePhase));
            else lamps.Add(lamp < 0f ? Vector2.zero : new Vector2(1f, lamp));
        }

        /// <summary>A triangle wound so it faces away from <paramref name="inside"/> (a point inside the convex part),
        /// for hand-built wedges where the winding is easy to get wrong.</summary>
        public void TriOut(Vector3 a, Vector3 b, Vector3 c, Vector3 inside, Color col)
        {
            var n = Vector3.Cross(b - a, c - a);
            if (Vector3.Dot(n, (a + b + c) / 3f - inside) < 0f) Tri(a, c, b, col); else Tri(a, b, c, col);
        }

        /// <summary><see cref="TriOut"/> for a quad.</summary>
        public void QuadOut(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 inside, Color col)
        {
            TriOut(a, b, c, inside, col);
            TriOut(a, c, d, inside, col);
        }

        public void Tri(Vector3 a, Vector3 b, Vector3 c, Color col)
        {
            a = M.MultiplyPoint3x4(a); b = M.MultiplyPoint3x4(b); c = M.MultiplyPoint3x4(c);
            var n = Vector3.Cross(b - a, c - a);
            if (n.sqrMagnitude < 1e-12f) return;
            n.Normalize();
            float j = 1f + ((float)rng.NextDouble() * 2f - 1f) * jitter;
            var lin = Linear(new Color(col.r * j, col.g * j, col.b * j));
            lin.a = 1f;
            int i = verts.Count;
            Add(a, n, lin); Add(b, n, lin); Add(c, n, lin);
            tris.Add(i); tris.Add(i + 1); tris.Add(i + 2);
        }

        /// <summary>A triangle with its own colour per corner (gradients: the wall, water depth).</summary>
        public void TriC(Vector3 a, Vector3 b, Vector3 c, Color ca, Color cb, Color cc)
        {
            a = M.MultiplyPoint3x4(a); b = M.MultiplyPoint3x4(b); c = M.MultiplyPoint3x4(c);
            var n = Vector3.Cross(b - a, c - a);
            if (n.sqrMagnitude < 1e-12f) return;
            n.Normalize();
            int i = verts.Count;
            var la = Linear(ca); la.a = 1f; var lb = Linear(cb); lb.a = 1f; var lc = Linear(cc); lc.a = 1f;
            Add(a, n, la); Add(b, n, lb); Add(c, n, lc);
            tris.Add(i); tris.Add(i + 1); tris.Add(i + 2);
        }

        /// <summary>Quad a-b-c-d, clockwise seen from the front (Unity's front-face winding).</summary>
        public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color col)
        {
            Tri(a, b, c, col);
            Tri(a, c, d, col);
        }

        /// <summary>A box centred on <paramref name="c"/> (local), optionally rotated, without its bottom face by default.</summary>
        public void Box(Vector3 c, Vector3 size, Color col, Quaternion? rot = null, bool bottom = false)
        {
            var r = rot ?? Quaternion.identity;
            var h = size * 0.5f;
            Vector3 P(float x, float y, float z) => c + r * new Vector3(x * h.x, y * h.y, z * h.z);
            Vector3 p000 = P(-1, -1, -1), p100 = P(1, -1, -1), p110 = P(1, 1, -1), p010 = P(-1, 1, -1);
            Vector3 p001 = P(-1, -1, 1), p101 = P(1, -1, 1), p111 = P(1, 1, 1), p011 = P(-1, 1, 1);
            Quad(p000, p010, p110, p100, col); // front (-z)
            Quad(p101, p111, p011, p001, col); // back (+z)
            Quad(p001, p011, p010, p000, col); // left (-x)
            Quad(p100, p110, p111, p101, col); // right (+x)
            Quad(p010, p011, p111, p110, col); // top
            if (bottom) Quad(p000, p100, p101, p001, col);
        }

        /// <summary>A prism / frustum / cone standing on <paramref name="baseCentre"/> along local up (rotated by rot).</summary>
        public void Prism(Vector3 baseCentre, float rBottom, float rTop, float height, int sides, Color col,
            Quaternion? rot = null, bool capTop = true, bool capBottom = false, Color? topCol = null, float twist = 0f, bool ramp = false)
        {
            var r = rot ?? Quaternion.identity;
            var top = topCol ?? col;
            for (int i = 0; i < sides; i++)
            {
                float a0 = (i / (float)sides) * Mathf.PI * 2f + twist, a1 = ((i + 1) / (float)sides) * Mathf.PI * 2f + twist;
                Vector3 d0 = new(Mathf.Cos(a0), 0, Mathf.Sin(a0)), d1 = new(Mathf.Cos(a1), 0, Mathf.Sin(a1));
                Vector3 b0 = baseCentre + r * (d0 * rBottom), b1 = baseCentre + r * (d1 * rBottom);
                Vector3 t0 = baseCentre + r * (d0 * rTop + Vector3.up * height), t1 = baseCentre + r * (d1 * rTop + Vector3.up * height);
                if (ramp)
                {
                    // Colour ramp up the sides: col at the base, topCol at the top (flames).
                    if (rTop > 0.0001f) { TriC(b0, t0, t1, col, top, top); TriC(b0, t1, b1, col, top, col); }
                    else TriC(b0, t0, b1, col, top, col);
                }
                else if (rTop > 0.0001f) Quad(b0, t0, t1, b1, col);
                else Tri(b0, t0, b1, col);
                if (capTop && rTop > 0.0001f) Tri(baseCentre + r * (Vector3.up * height), t1, t0, top);
                if (capBottom) Tri(baseCentre, b0, b1, col);
            }
        }

        static readonly Vector3[] IcoV;
        static readonly int[] IcoF =
        {
            0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11, 1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
            3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9, 4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1,
        };

        static MeshBuilder()
        {
            float t = (1f + Mathf.Sqrt(5f)) / 2f;
            IcoV = new[]
            {
                new Vector3(-1, t, 0), new Vector3(1, t, 0), new Vector3(-1, -t, 0), new Vector3(1, -t, 0),
                new Vector3(0, -1, t), new Vector3(0, 1, t), new Vector3(0, -1, -t), new Vector3(0, 1, -t),
                new Vector3(t, 0, -1), new Vector3(t, 0, 1), new Vector3(-t, 0, -1), new Vector3(-t, 0, 1),
            };
            for (int i = 0; i < IcoV.Length; i++) IcoV[i] = IcoV[i].normalized;
        }

        /// <summary>A jittered icosahedron (rocks, canopies, heads). <paramref name="scale"/> squashes it.</summary>
        public void Ico(Vector3 c, float radius, Vector3 scale, Color col, float rough = 0.12f, Color? bottomCol = null)
        {
            var p = new Vector3[12];
            for (int i = 0; i < 12; i++)
            {
                float k = 1f + ((float)rng.NextDouble() * 2f - 1f) * rough;
                p[i] = c + Vector3.Scale(IcoV[i] * radius * k, scale);
            }
            for (int f = 0; f < IcoF.Length; f += 3)
            {
                var a = p[IcoF[f]]; var b = p[IcoF[f + 1]]; var d = p[IcoF[f + 2]];
                bool under = bottomCol.HasValue && (a.y + b.y + d.y) / 3f < c.y - radius * 0.2f;
                Tri(a, b, d, under ? bottomCol.Value : col);
            }
        }

        public float Range(float a, float b) => a + (float)rng.NextDouble() * (b - a);
        public int Range(int a, int b) => rng.Next(a, b);

        /// <summary>Appends another builder's faces (already in their final space) to this one.</summary>
        public void Append(MeshBuilder src)
        {
            int o = verts.Count;
            verts.AddRange(src.verts); normals.AddRange(src.normals); colors.AddRange(src.colors);
            uvs.AddRange(src.uvs); lamps.AddRange(src.lamps);
            foreach (int t in src.tris) tris.Add(t + o);
        }

        public Mesh ToMesh(string name)
        {
            var m = new Mesh { name = name, indexFormat = verts.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            m.SetVertices(verts);
            m.SetNormals(normals);
            m.SetColors(colors);
            m.SetUVs(0, uvs);
            m.SetUVs(1, lamps);
            m.SetTriangles(tris, 0);
            m.RecalculateBounds();
            return m;
        }
    }
}
