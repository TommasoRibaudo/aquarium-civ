using System;
using AquariumCiv.Sim.Settle;

namespace AquariumCiv.Sim.Eco
{
    /// <summary>
    /// #385 (playtest #341, the user 2026-11-07): the berry bushes grow in one or two thickets, not a carpet over the meadow.
    /// The first thicket stands where the first bushes came up (pushed out from the camp so it never sits on the tents); a second
    /// one takes hold where a stray bush comes up far from the first, once the first has filled in. Inside a thicket bushes spread
    /// as they always did; outside, a stray now and then, which dies back. The count settles at a cap that scales with the meadow
    /// (about 60 in a grown box); above it bushes thin, the ones outside the thickets first, so a carpeted old save thins over a few
    /// years. Hash dice only, as the rest of the land.
    /// </summary>
    public sealed partial class EcoModel
    {
        /// <summary>How many bushes the land holds now: a share of the meadow, at most <see cref="EcoTuning.bushCap"/>.</summary>
        public int BushCap => Math.Max(1, Math.Min(t.bushCap, (int)MathF.Round(t.bushPerMeadow * (st.cGrass + st.cTall + st.cBush))));

        /// <summary>Room for new bushes: 1 well under the cap, 0 at it.</summary>
        float BushRoom { get { float f = st.cBush / (float)BushCap; return Terrain.Clamp01(1f - f * f); } }

        /// <summary>Sets the thickets from the bushes that stand (once each). Runs before the season's succession.</summary>
        void Thickets()
        {
            if (st.cBush <= 0) return;
            float cx = land.campX, cz = land.campZ;
            if (st.thicketA < 0)
            {
                int c = NearestBush(cx, cz, -1, 0f);
                if (c >= 0) st.thicketA = PushedFromCamp(c);
            }
            else if (st.thicketB < 0 && st.cBush >= t.thicketBAfter)
            {
                int c = NearestBush(cx, cz, st.thicketA, t.thicketApart);
                if (c >= 0) st.thicketB = PushedFromCamp(c);
            }
        }

        /// <summary>The bush nearest (x, z) at least <paramref name="apart"/> from <paramref name="away"/>'s centre, or -1. Ties by cell.</summary>
        int NearestBush(float x, float z, int away, float apart)
        {
            int best = -1; float bestD = float.MaxValue;
            for (int c = 0; c < EcoLand.N; c++)
            {
                if (Cv(c) != EcoCover.Bush) continue;
                if (away >= 0 && Terrain.Dist(SiteMap.CXc(c), SiteMap.CZc(c), SiteMap.CXc(away), SiteMap.CZc(away)) < apart) continue;
                float d = Terrain.Dist(SiteMap.CXc(c), SiteMap.CZc(c), x, z);
                if (d < bestD) { bestD = d; best = c; }
            }
            return best;
        }

        /// <summary>The thicket's centre for a bush: the bush's own cell, or out along the line from the camp so the thicket's edge
        /// stays off the green and the tents. Kept on the land (not in the glass's margin).</summary>
        int PushedFromCamp(int c)
        {
            float x = SiteMap.CXc(c), z = SiteMap.CZc(c), dx = x - land.campX, dz = z - land.campZ, d = MathF.Sqrt(dx * dx + dz * dz);
            if (d >= t.thicketFromCamp) return c;
            if (d < 1e-3f) { dx = 1f; dz = 0f; d = 1f; }
            x = land.campX + dx / d * t.thicketFromCamp; z = land.campZ + dz / d * t.thicketFromCamp;
            x = Math.Clamp(x, -Terrain.HX + 0.3f, Terrain.HX - 0.3f); z = Math.Clamp(z, -Terrain.HZ + 0.3f, Terrain.HZ - 0.3f);
            int p = SiteMap.CellAt(x, z);
            return p >= 0 ? p : c;
        }

        /// <summary>How much a cell is in a thicket: 1 in the heart, falling to 0 at a ragged edge. Everywhere 1 before the first
        /// thicket (so the first bushes come as they always did).</summary>
        public float ThicketZone(int c)
        {
            if (st.thicketA < 0) return 1f;
            float z = Zone(c, st.thicketA, t.thicketR1, 1);
            if (st.thicketB >= 0) z = MathF.Max(z, Zone(c, st.thicketB, t.thicketR2, 2));
            return z;
        }

        float Zone(int c, int centre, float r, int salt)
        {
            float x = SiteMap.CXc(c), z = SiteMap.CZc(c);
            // A ragged edge: the radius wobbles by direction, in coarse steps, so the thicket isn't a disc.
            float dx = x - SiteMap.CXc(centre), dz = z - SiteMap.CZc(centre);
            int sector = (int)((MathF.Atan2(dz, dx) + MathF.PI) / (MathF.PI * 2f) * 7f) % 7;
            float rr = r * (1f - t.thicketEdge * 0.5f + t.thicketEdge * Terrain.Hash3(st.seed * 53 + salt, centre, sector));
            float d = MathF.Sqrt(dx * dx + dz * dz) / rr;
            return d >= 1f ? 0f : d <= 0.5f ? 1f : 1f - Terrain.Smooth(0.5f, 1f, d);
        }

        /// <summary>A bush goes back to meadow this season: strays outside a thicket die back now and then, and above the cap
        /// the bushes thin, outside the thickets and at their edges first.</summary>
        bool ThinBush(int c)
        {
            if (st.thicketA < 0) return false;
            float zone = ThicketZone(c), die = zone <= 0f ? t.bushStrayDie : 0f;
            int cap = BushCap;
            if (st.cBush > cap) die += t.bushThin * MathF.Min(1f, (st.cBush - cap) / (float)cap) * (1f - 0.85f * zone);
            return die > 0f && Chance(16, c) < die;
        }
    }
}
