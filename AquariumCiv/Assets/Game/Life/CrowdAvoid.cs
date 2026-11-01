using System;
using System.Collections.Generic;

namespace AquariumCiv.Life
{
    /// <summary>A solid thing on the ground people go round: a circle on the box floor.</summary>
    public struct Solid
    {
        public float x, z, r;
        public Solid(float x, float z, float r) { this.x = x; this.z = z; this.r = r; }
    }

    /// <summary>
    /// #189: people walk round the fire and don't walk through each other, pure (no engine types, no sim writes, nothing the
    /// planner plans changes: a person's planned point stays what it was and only the doll drawn is eased aside).
    ///
    /// Two small rules, both continuous in the planned point so nobody ever jumps:
    /// <list type="bullet">
    /// <item><b>Solids</b> (<see cref="KeepOut"/>): a point inside a solid's circle is pushed straight out to its rim, so a
    /// walk straight at the fire slides round its edge. Standing people at the fire's ring (0.3 out) are outside it already.</item>
    /// <item><b>Space</b> (<see cref="Push"/>, <see cref="Ease"/>): a walker closer than <see cref="Personal"/> to anyone is
    /// eased away from them (a standing person doesn't move, two walkers share it). Two on exactly one point split to either
    /// side of their way by key, so a group heading for one spot fans out instead of sharing a line.</item>
    /// </list>
    /// Per frame, the view adds everyone's drawn point (<see cref="Add"/>), calls <see cref="Build"/>, then per walker asks
    /// <see cref="Push"/> and eases that walker's offset with <see cref="Ease"/>. Cost is linear: a uniform grid with one
    /// reused set of arrays, no allocation after warm-up.
    /// </summary>
    public sealed class CrowdAvoid
    {
        /// <summary>Nobody walks closer than this to anyone (a doll is about 0.1 wide).</summary>
        public const float Personal = 0.11f;
        /// <summary>The fire: nobody stands or walks within this of its middle (the log seats are further out).</summary>
        public const float FireRadius = 0.2f;
        /// <summary>The most a walker is eased aside from their planned point.</summary>
        public const float MaxOffset = 0.3f;
        /// <summary>Easing a walker's offset: how fast it grows (per second, of the overlap) and fades back when clear.</summary>
        public const float Grow = 14f, Fade = 1.2f;

        float[] xs = new float[64], zs = new float[64], hx = new float[64], hz = new float[64];
        int[] keys = new int[64];
        bool[] moving = new bool[64];
        int n;
        const int Cells = 256;
        readonly int[] head = new int[Cells];
        int[] next = new int[64];

        public int Count => n;

        public void Clear() { n = 0; }

        /// <summary>Add a person at the point they are drawn. headX/headZ is the way they walk (any length; 0 for
        /// standing). Returns their index for <see cref="Push"/>.</summary>
        public int Add(int key, float x, float z, bool walking, float headX = 0f, float headZ = 0f)
        {
            if (n == xs.Length)
            {
                int m = n * 2;
                Array.Resize(ref xs, m); Array.Resize(ref zs, m); Array.Resize(ref hx, m); Array.Resize(ref hz, m);
                Array.Resize(ref keys, m); Array.Resize(ref moving, m); Array.Resize(ref next, m);
            }
            xs[n] = x; zs[n] = z; hx[n] = headX; hz[n] = headZ; keys[n] = key; moving[n] = walking;
            return n++;
        }

        static int CellOf(int cx, int cz)
        {
            unchecked { return (int)(((uint)cx * 73856093u) ^ ((uint)cz * 19349663u)) & (Cells - 1); }
        }

        /// <summary>Index everyone added since <see cref="Clear"/>.</summary>
        public void Build()
        {
            for (int c = 0; c < Cells; c++) head[c] = -1;
            for (int i = 0; i < n; i++)
            {
                int c = CellOf((int)Math.Floor(xs[i] / Personal), (int)Math.Floor(zs[i] / Personal));
                next[i] = head[c]; head[c] = i;
            }
        }

        /// <summary>How far to move walker i now to clear everyone nearer than <see cref="Personal"/> (box units; (0, 0)
        /// when clear or standing). Not a walk: the view eases its offset toward this with <see cref="Ease"/>.</summary>
        public void Push(int i, out float px, out float pz)
        {
            px = pz = 0f;
            if (!moving[i]) return;
            int cx = (int)Math.Floor(xs[i] / Personal), cz = (int)Math.Floor(zs[i] / Personal);
            for (int ox = -1; ox <= 1; ox++)
                for (int oz = -1; oz <= 1; oz++)
                    for (int j = head[CellOf(cx + ox, cz + oz)]; j >= 0; j = next[j])
                    {
                        if (j == i) continue;
                        float dx = xs[i] - xs[j], dz = zs[i] - zs[j];
                        float d2 = dx * dx + dz * dz;
                        if (d2 >= Personal * Personal) continue;
                        float d = (float)Math.Sqrt(d2);
                        float share = moving[j] ? 0.5f : 1f;
                        if (d < 1e-4f)
                        {
                            // On top of each other: split to either side of the way, the lower key to the left.
                            float ax = hx[i] + hx[j], az = hz[i] + hz[j];
                            float al = (float)Math.Sqrt(ax * ax + az * az);
                            if (al < 1e-4f) { ax = 1f; az = 0f; } else { ax /= al; az /= al; }
                            float side = keys[i] < keys[j] ? 1f : -1f;
                            dx = -az * side; dz = ax * side; d = 0f;
                        }
                        else
                        {
                            dx /= d; dz /= d;
                            // Head-on, straight back would only hold them up: ease half of it into a sidestep, to the side
                            // they are already on (the key's side when dead ahead).
                            float hl = (float)Math.Sqrt(hx[i] * hx[i] + hz[i] * hz[i]);
                            if (hl > 1e-4f)
                            {
                                float ux = hx[i] / hl, uz = hz[i] / hl, lx = -uz, lz = ux;
                                float lat = dx * lx + dz * lz;
                                float side = lat > 0.15f ? 1f : lat < -0.15f ? -1f : keys[i] < keys[j] ? 1f : -1f;
                                dx += lx * side * 0.9f; dz += lz * side * 0.9f;
                                float dl = (float)Math.Sqrt(dx * dx + dz * dz);
                                dx /= dl; dz /= dl;
                            }
                        }
                        float over = (Personal - d) * share;
                        px += dx * over; pz += dz * over;
                    }
        }

        /// <summary>One frame of a walker's offset (ox, oz from their planned point): grows toward the push, fades back when
        /// nothing presses, never more than <see cref="MaxOffset"/>.</summary>
        public static void Ease(ref float ox, ref float oz, float pushX, float pushZ, float dt)
        {
            if (pushX * pushX + pushZ * pushZ > 1e-8f)
            {
                float k = Math.Min(1f, Grow * dt);
                ox += pushX * k; oz += pushZ * k;
            }
            else
            {
                float k = (float)Math.Exp(-Fade * dt);
                ox *= k; oz *= k;
            }
            float len = (float)Math.Sqrt(ox * ox + oz * oz);
            if (len > MaxOffset) { ox *= MaxOffset / len; oz *= MaxOffset / len; }
        }

        /// <summary>Move a point outside every solid: one inside is pushed straight out to the rim (continuous along any
        /// walk, so nobody jumps). The heading picks the side at the very middle.</summary>
        public static void KeepOut(ref float x, ref float z, IReadOnlyList<Solid> solids, float headX = 0f, float headZ = 0f)
        {
            for (int s = 0; s < solids.Count; s++)
            {
                var o = solids[s];
                float dx = x - o.x, dz = z - o.z;
                float d2 = dx * dx + dz * dz;
                if (d2 >= o.r * o.r) continue;
                float d = (float)Math.Sqrt(d2);
                if (d < 1e-5f)
                {
                    float hl = (float)Math.Sqrt(headX * headX + headZ * headZ);
                    if (hl < 1e-5f) { dx = 1f; dz = 0f; } else { dx = -headZ / hl; dz = headX / hl; }
                    d = 1f;
                }
                x = o.x + dx / d * o.r;
                z = o.z + dz / d * o.r;
            }
        }
    }
}
