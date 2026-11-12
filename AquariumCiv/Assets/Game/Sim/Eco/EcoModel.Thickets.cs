using System;
using AquariumCiv.Sim.Settle;

namespace AquariumCiv.Sim.Eco
{
    /// <summary>
    /// #418 (placed-life spec §5.1; decisions 2026-11-07 "1 thicket ... it stays in that own area"): the berry thicket is placed by
    /// the Keeper, once per box, and it is the only bush life in the box. No bush grows before it is placed, and none ever grows
    /// outside its area: a disc round the placed spot (<see cref="EcoState.thicketRadius"/>, a ragged edge) that holds about
    /// <see cref="EcoTuning.thicketBushes"/>. It starts with <see cref="EcoTuning.thicketFounders"/> bushes and fills a bush a
    /// season, then stops. #385's rules stay: no bush in the ring round the fire, the thicket chokes tree seedlings, and the
    /// settlement keeps off it (Settlement.SyncEco reserves the area). An old save's wild bushes outside the area stay but never
    /// spread, and thin by #385's rule. Hash dice only, as the rest of the land.
    /// </summary>
    public sealed partial class EcoModel
    {
        /// <summary>How many bushes the thicket holds (the count settles here).</summary>
        public int BushCap => Math.Max(1, t.thicketBushes);

        /// <summary>The thicket is placed (its centre and radius are set).</summary>
        public bool ThicketPlaced => st.thicketA >= 0 && st.thicketRadius > 0f;

        /// <summary>The placed thicket fills: one bush a season next to a standing one, heart first, until it holds the cap.
        /// #605: where the mister's water fell on it, now and then a second one comes up (never past the cap, never outside the area).
        /// Runs before the season's succession.</summary>
        void Thickets()
        {
            if (!ThicketPlaced || BushesInThicket() >= BushCap) return;
            AddThicketBush();
            if (BushesInThicket() >= BushCap || t.thicketWatered <= 0f) return;
            float w = ThicketWatered();
            if (w > 0f && Chance(31, 0) < t.thicketWatered * w) AddThicketBush();
        }

        /// <summary>#605: how watered the thicket is, 0..1: the mean of the mister's water over its bushes.</summary>
        public float ThicketWatered()
        {
            if (st.watered == null) return 0f;
            float sum = 0f; int n = 0;
            for (int c = 0; c < EcoLand.N; c++)
                if (Cv(c) == EcoCover.Bush && ThicketZone(c) > 0f) { sum += st.watered[c]; n++; }
            return n == 0 ? 0f : sum / n;
        }

        void AddThicketBush()
        {
            int best = -1; float bestScore = float.MinValue;
            for (int c = 0; c < EcoLand.N; c++)
            {
                float zone = ThicketZone(c);
                if (zone <= 0f || !BushGround(c)) continue;
                int n = EcoLand.Neighbours8(c, nb), bushes = 0;
                for (int k = 0; k < n; k++) if (Cv(nb[k]) == EcoCover.Bush && ThicketZone(nb[k]) > 0f) bushes++;
                if (bushes == 0) continue;
                float score = zone + 0.35f * Chance(30, c);
                if (score > bestScore) { bestScore = score; best = c; }
            }
            if (best >= 0) st.cover[best] = (byte)EcoCover.Bush;
        }

        /// <summary>Bushes standing in the thicket's area now.</summary>
        public int BushesInThicket()
        {
            int n = 0;
            for (int c = 0; c < EcoLand.N; c++) if (Cv(c) == EcoCover.Bush && ThicketZone(c) > 0f) n++;
            return n;
        }

        /// <summary>A bush may come up here: open ground (meadow or bare) off the hollow, the yard, the fire's ring and the Keeper's tree.</summary>
        bool BushGround(int c)
        {
            var cv = Cv(c);
            if (cv != EcoCover.Grass && cv != EcoCover.Tall && cv != EcoCover.Bare) return false;
            return !land.InBasin(c) && !land.yard[c] && !NearFire(c) && c != st.treeCell;
        }

        /// <summary>#418: the Keeper placed the thicket at <paramref name="cell"/>: its area is set and the founders come up there
        /// (the cell itself and the nearest open ground round it).</summary>
        void FoundThicket(int cell, long month)
        {
            st.thicketA = cell; st.thicketB = -1; st.thicketRadius = t.thicketRadius;
            float x = SiteMap.CXc(cell), z = SiteMap.CZc(cell);
            int made = 0;
            while (made < t.thicketFounders)
            {
                int best = -1; float bd = float.MaxValue;
                for (int c = 0; c < EcoLand.N; c++)
                {
                    if (ThicketZone(c) <= 0f || !BushGround(c)) continue;
                    float d = Terrain.Dist(SiteMap.CXc(c), SiteMap.CZc(c), x, z);
                    if (d < bd) { bd = d; best = c; }
                }
                if (best < 0) break;
                st.cover[best] = (byte)EcoCover.Bush; made++;
            }
            if (Cv(cell) == EcoCover.Bush) st.firstBushCell = cell;
            if (made > 0) Reach(EcoMilestones.FirstBush, month);
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

        /// <summary>An old save's thicket centre for a bush (#385): the bush's own cell, or out along the line from the camp so the
        /// thicket's edge stays off the green and the tents. Kept on the land (not in the glass's margin).</summary>
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

        /// <summary>How much a cell is in the thicket: 1 in the heart, falling to 0 at a ragged edge; 0 everywhere before it is placed.</summary>
        public float ThicketZone(int c)
        {
            if (st.thicketA < 0) return 0f;
            return Zone(c, st.thicketA, st.thicketRadius > 0f ? st.thicketRadius : t.thicketR1, 1);
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

        /// <summary>The ring round the camp's fire where no bush grows (the user 2026-11-07: thickets a little away from the centre).</summary>
        public bool NearFire(int c) => Terrain.Dist(SiteMap.CXc(c), SiteMap.CZc(c), land.campX, land.campZ) < t.bushCampMargin;

        /// <summary>A bush goes back to meadow this season: in the ring round the fire; outside the thicket (an old save's wild bushes)
        /// now and then; and above the cap the bushes thin, outside the thicket and at its edges first.</summary>
        bool ThinBush(int c)
        {
            if (NearFire(c)) return Chance(16, c) < t.bushMarginDie; // the people keep the ground round the fire clear
            float zone = ThicketZone(c), die = zone <= 0f ? t.bushStrayDie : 0f;
            int cap = BushCap;
            if (st.cBush > cap) die += t.bushThin * MathF.Min(1f, (st.cBush - cap) / (float)cap) * (1f - 0.85f * zone);
            return die > 0f && Chance(16, c) < die;
        }
    }
}
