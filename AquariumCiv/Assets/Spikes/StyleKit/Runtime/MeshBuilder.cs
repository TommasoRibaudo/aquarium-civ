using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace AquariumCiv.Spikes.StyleKit
{
    /// <summary>
    /// Flat-shaded low-poly mesh writer for the style-kit spike. Every face gets its own vertices (crisp facets), a vertex
    /// colour from the palette (alpha = baked AO, 1 = open), and UV0 = (wind sway weight, emission). One material draws
    /// everything. Shapes are written through <see cref="M"/>, so callers place parts in local space.
    /// </summary>
    public class MeshBuilder
    {
        public readonly List<Vector3> verts = new();
        public readonly List<Vector3> normals = new();
        public readonly List<Color> colors = new();
        public readonly List<Vector2> uvs = new();
        public readonly List<int> tris = new();
        public Matrix4x4 M = Matrix4x4.identity;
        /// <summary>Per-face brightness jitter (0.04 = ±4 %), so flat faces don't look machine-made.</summary>
        public float jitter = 0.035f;
        public float sway, emission;
        readonly System.Random rng;

        public MeshBuilder(int seed = 1) { rng = new System.Random(seed); }

        public void Tri(Vector3 a, Vector3 b, Vector3 c, Color col)
        {
            a = M.MultiplyPoint3x4(a); b = M.MultiplyPoint3x4(b); c = M.MultiplyPoint3x4(c);
            var n = Vector3.Cross(b - a, c - a);
            if (n.sqrMagnitude < 1e-12f) return;
            n.Normalize();
            float j = 1f + ((float)rng.NextDouble() * 2f - 1f) * jitter;
            var lin = new Color(col.r * j, col.g * j, col.b * j).linear;
            lin.a = 1f;
            int i = verts.Count;
            verts.Add(a); verts.Add(b); verts.Add(c);
            for (int k = 0; k < 3; k++) { normals.Add(n); colors.Add(lin); uvs.Add(new Vector2(sway, emission)); }
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
            verts.Add(a); verts.Add(b); verts.Add(c);
            foreach (var col in new[] { ca, cb, cc })
            {
                var lin = col.linear; lin.a = 1f;
                normals.Add(n); colors.Add(lin); uvs.Add(new Vector2(sway, emission));
            }
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
            Quaternion? rot = null, bool capTop = true, bool capBottom = false, Color? topCol = null, float twist = 0f)
        {
            var r = rot ?? Quaternion.identity;
            var top = topCol ?? col;
            for (int i = 0; i < sides; i++)
            {
                float a0 = (i / (float)sides) * Mathf.PI * 2f + twist, a1 = ((i + 1) / (float)sides) * Mathf.PI * 2f + twist;
                Vector3 d0 = new(Mathf.Cos(a0), 0, Mathf.Sin(a0)), d1 = new(Mathf.Cos(a1), 0, Mathf.Sin(a1));
                Vector3 b0 = baseCentre + r * (d0 * rBottom), b1 = baseCentre + r * (d1 * rBottom);
                Vector3 t0 = baseCentre + r * (d0 * rTop + Vector3.up * height), t1 = baseCentre + r * (d1 * rTop + Vector3.up * height);
                if (rTop > 0.0001f) Quad(b0, t0, t1, b1, col);
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

        public Mesh ToMesh(string name)
        {
            var m = new Mesh { name = name, indexFormat = verts.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            m.SetVertices(verts);
            m.SetNormals(normals);
            m.SetColors(colors);
            m.SetUVs(0, uvs);
            m.SetTriangles(tris, 0);
            m.RecalculateBounds();
            return m;
        }
    }
}
