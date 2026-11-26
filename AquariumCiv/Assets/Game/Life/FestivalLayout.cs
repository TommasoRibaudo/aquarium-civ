// #770 round 3: where the era festival's dressing stands. Pure (Sim + System), so Tools/SimTests checks it against the lots.
using System;
using System.Collections.Generic;
using AquariumCiv.Sim;
using AquariumCiv.Sim.Settle;

namespace AquariumCiv.Life
{
    /// <summary>
    /// The bunting poles and the two long tables, laid round the festival's dance ring: the ring the staged festival picked (its centre and
    /// radius, <see cref="Ring"/>) or else the green, moved by the same search to ground clear of every building lot. Poles stand just outside the
    /// ring, tables farther out, none on a lot rectangle (<see cref="LifePlanner.FootprintBlocked"/>).
    /// </summary>
    public sealed class FestivalLayout
    {
        public const int Poles = 7;
        public const float TableLen = 0.34f, TableW = 0.07f, DanceR = 0.6f, PoleGap = 0.08f;

        /// <summary>The ring a staged festival chose (set when it is staged, dropped when the festival year ends); null: not staged.</summary>
        public static (float x, float z, float r)? Ring;

        public float cx, cz, ringR, poleR;
        public readonly List<(float x, float z)> poles = new List<(float, float)>();
        public readonly List<(float x, float z)> tables = new List<(float, float)>();

        static bool RectFree(Func<float, float, bool> blocked, float x, float z, float hx, float hz)
        {
            for (int i = -2; i <= 2; i++)
                for (int j = -1; j <= 1; j++)
                    if (blocked(x + hx * i / 2f, z + hz * j)) return false;
            return true;
        }

        public static FestivalLayout Compute(SimCore sim)
        {
            var map = sim.Settlement.Map;
            var blocked = LifePlanner.FootprintBlocked(sim) ?? ((x, z) => false);
            var L = new FestivalLayout();
            float gx = Ring.HasValue ? Ring.Value.x : map.greenX, gz = Ring.HasValue ? Ring.Value.z : map.greenZ;
            L.ringR = Ring.HasValue ? Ring.Value.r : DanceR;
            L.poleR = Math.Max(SiteMap.GreenR + 0.06f, L.ringR + PoleGap);
            bool OkC(float x, float z) => Math.Abs(x) <= Terrain.HX - 0.45f && Terrain.InZ(z, 0.45f);
            // a staged ring is already clear; the green's is searched (poles' ring plus the tables' reach out of it)
            var c = Ring.HasValue ? (gx, gz, L.ringR) : OpenGround.RingFit(gx, gz, L.poleR, SiteMapMin, blocked, OkC, 1.6f);
            L.cx = c.Item1; L.cz = c.Item2;
            if (!Ring.HasValue) { L.ringR = Math.Min(L.ringR, c.Item3 - PoleGap); L.poleR = Math.Max(c.Item3, L.ringR + PoleGap); }
            for (int i = 0; i < Poles; i++)
            {
                float a = (-20f + 220f * i / (Poles - 1)) * (float)Math.PI / 180f;
                float x = L.cx + (float)Math.Cos(a) * L.poleR, z = L.cz + (float)Math.Sin(a) * L.poleR;
                if (!blocked(x, z)) L.poles.Add((x, z));
            }
            // tables: out past the poles, the first two angles whose whole table (and benches) is on open ground
            float td = L.poleR + 0.28f;
            foreach (float deg in new[] { 160f, 200f, 120f, 240f, 20f, 340f, 90f, 270f, 60f, 300f, 0f, 180f })
            {
                if (L.tables.Count == 2) break;
                float a = deg * (float)Math.PI / 180f;
                for (float d = td; d <= td + 0.5f; d += 0.1f)
                {
                    float x = L.cx + (float)Math.Cos(a) * d, z = L.cz + (float)Math.Sin(a) * d;
                    if (!OkC(x, z) || !RectFree(blocked, x, z, TableLen * 0.5f + 0.05f, TableW * 0.5f + 0.06f)) continue;
                    bool apart = true;
                    foreach (var t in L.tables) if (Math.Abs(t.x - x) < TableLen + 0.1f && Math.Abs(t.z - z) < 0.3f) apart = false;
                    if (!apart) continue;
                    L.tables.Add((x, z));
                    break;
                }
            }
            return L;
        }

        const float SiteMapMin = 0.42f;
    }
}
