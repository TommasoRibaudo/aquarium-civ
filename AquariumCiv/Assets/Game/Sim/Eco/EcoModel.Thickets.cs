using System;
using System.Collections.Generic;
using AquariumCiv.Sim.Settle;

namespace AquariumCiv.Sim.Eco
{
    /// <summary>
    /// #418 (placed-life spec 5.1; decisions 2026-11-07), reversed by #724/#728 (decision 2026-11-14 "A3 bushes", landscaping.md 8.2): the
    /// berry thickets are placed by the Keeper and are the only bush life in the box. No bush grows before the first is placed, and none
    /// ever grows outside a thicket's area: a disc round a placed spot (<see cref="Thicket"/>: a cell and a radius, a ragged edge) that holds
    /// about <see cref="ThicketTarget"/> bushes. The first comes from the thicket jar; every further one is a bush from the pouch (#726, see
    /// <see cref="PlaceEarnedBush"/>). Up to <see cref="EcoTuning.thicketMax"/> thickets, discs never overlap. Each starts with
    /// <see cref="EcoTuning.thicketFounders"/> bushes and fills a bush a season, then stops. #385's rules stay: no bush in the ring round the
    /// fire, the thickets choke tree seedlings, and the settlement keeps off every one (Settlement.SyncEco reserves them). An old save's
    /// wild bushes outside the areas stay but never spread, and thin by #385's rule. Hash dice only, as the rest of the land.
    /// </summary>
    public sealed partial class EcoModel
    {
        /// <summary>How many bushes one thicket holds when it is full (the count settles here): <see cref="ThicketTarget"/> for each
        /// thicket, so the box's total. Before any thicket is placed it is the one-thicket figure.</summary>
        public int BushCap => Math.Max(1, ThicketTarget() * Math.Max(1, Ts.Count));

        /// <summary>The thickets, as the save keeps them. An older save has only <see cref="EcoState.thicketA"/> / <see cref="EcoState.thicketRadius"/>:
        /// it is read as one thicket here (v1 loads as one full thicket). Those two fields stay in step with the first thicket.</summary>
        List<Thicket> Ts
        {
            get
            {
                var l = st.thickets;
                if (l == null) l = st.thickets = new List<Thicket>();
                if (l.Count == 0 && st.thicketA >= 0 && st.thicketRadius > 0f) l.Add(new Thicket { cell = st.thicketA, radius = st.thicketRadius });
                return l;
            }
        }

        /// <summary>The thickets placed, in the order they were placed (the first is the jar's).</summary>
        public IReadOnlyList<Thicket> ThicketList => Ts;

        /// <summary>How many thickets stand (0 before the first is placed).</summary>
        public int ThicketCount => Ts.Count;

        /// <summary>At least one thicket is placed (its centre and radius are set).</summary>
        public bool ThicketPlaced => Ts.Count > 0;

        /// <summary>The bushes one thicket holds when full, with <see cref="ThicketCount"/> thickets standing. #729 makes it fall as they crowd;
        /// until then every thicket holds the full <see cref="EcoTuning.thicketBushes"/>.</summary>
        public int ThicketTarget() => Math.Max(1, t.thicketBushes);

        /// <summary>The disc radius a thicket has with <paramref name="n"/> thickets standing (#729 shrinks it; until then the full radius).</summary>
        float ThicketRadiusFor(int n) => t.thicketRadius;

        /// <summary>The widest reach of a thicket's ragged edge (box units).</summary>
        float ReachOf(Thicket th) => th.radius * (1f + t.thicketEdge * 0.5f);

        /// <summary>Keeps the first-thicket fields of the save (<see cref="EcoState.thicketA"/>, <see cref="EcoState.thicketRadius"/>) in step with
        /// the list, so views and older readers of the one thicket still see it. Call after every change to the list.</summary>
        void SyncFirstThicket()
        {
            var l = st.thickets;
            if (l != null && l.Count > 0) { st.thicketA = l[0].cell; st.thicketRadius = l[0].radius; }
            else { st.thicketA = -1; st.thicketRadius = 0f; }
            st.thicketB = -1;
        }

        /// <summary>Every thicket fills: one bush a season next to a standing one, heart first, until it holds its target.
        /// #605: where the mister's water fell on it, now and then a second one comes up (never past the target, never outside its area).
        /// Runs before the season's succession.</summary>
        void Thickets()
        {
            var ts = Ts;
            if (ts.Count == 0) return;
            int target = ThicketTarget();
            for (int i = 0; i < ts.Count; i++)
            {
                var th = ts[i];
                if (BushesIn(th) >= target) continue;
                AddThicketBush(th);
                if (BushesIn(th) >= target || t.thicketWatered <= 0f) continue;
                float w = ThicketWatered(th);
                if (w > 0f && Chance(31, i) < t.thicketWatered * w) AddThicketBush(th);
            }
        }

        /// <summary>Fills <paramref name="cells"/> with the cells of a thicket's area (zone above 0), in cell order, and <paramref name="zones"/> with their zones.</summary>
        void CellsOf(Thicket th, List<int> cells, List<float> zones)
        {
            cells.Clear(); zones.Clear();
            float R = ReachOf(th) + 1e-4f;
            int reach = (int)MathF.Ceiling(R / SiteMap.Cell) + 1;
            int i0 = SiteMap.I(th.cell), j0 = SiteMap.J(th.cell);
            for (int j = Math.Max(0, j0 - reach); j <= Math.Min(SiteMap.NZ - 1, j0 + reach); j++)
                for (int i = Math.Max(0, i0 - reach); i <= Math.Min(SiteMap.NX - 1, i0 + reach); i++)
                {
                    int c = SiteMap.Index(i, j);
                    float z = ZoneIn(th, c);
                    if (z > 0f) { cells.Add(c); zones.Add(z); }
                }
        }

        readonly List<int> thCells = new List<int>();
        readonly List<float> thZones = new List<float>();

        /// <summary>#605: how watered the thickets are, 0..1: the mean of the mister's water over the bushes in any thicket.</summary>
        public float ThicketWatered()
        {
            if (st.watered == null) return 0f;
            float sum = 0f; int n = 0;
            for (int c = 0; c < EcoLand.N; c++)
                if (Cv(c) == EcoCover.Bush && ThicketZone(c) > 0f) { sum += st.watered[c]; n++; }
            return n == 0 ? 0f : sum / n;
        }

        /// <summary>#605: how watered one thicket is, 0..1: the mean of the mister's water over its bushes.</summary>
        float ThicketWatered(Thicket th)
        {
            if (st.watered == null) return 0f;
            CellsOf(th, thCells, thZones);
            float sum = 0f; int n = 0;
            foreach (var c in thCells) if (Cv(c) == EcoCover.Bush) { sum += st.watered[c]; n++; }
            return n == 0 ? 0f : sum / n;
        }

        /// <summary>One more bush in a thicket: next to a standing one in its area, its heart first (the best score; ties by the lowest cell).</summary>
        void AddThicketBush(Thicket th)
        {
            CellsOf(th, thCells, thZones);
            int best = -1; float bestScore = float.MinValue;
            for (int k = 0; k < thCells.Count; k++)
            {
                int c = thCells[k];
                if (!BushGround(c)) continue;
                int n = EcoLand.Neighbours8(c, nb), bushes = 0;
                for (int q = 0; q < n; q++) if (Cv(nb[q]) == EcoCover.Bush && ZoneIn(th, nb[q]) > 0f) bushes++;
                if (bushes == 0) continue;
                float score = thZones[k] + 0.35f * Chance(30, c);
                if (score > bestScore) { bestScore = score; best = c; }
            }
            if (best >= 0) st.cover[best] = (byte)EcoCover.Bush;
        }

        /// <summary>Bushes standing in one thicket's area now.</summary>
        public int BushesIn(Thicket th)
        {
            CellsOf(th, thCells, thZones);
            int n = 0;
            foreach (var c in thCells) if (Cv(c) == EcoCover.Bush) n++;
            return n;
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

        /// <summary>#418: the Keeper placed a thicket at <paramref name="cell"/> (the jar's first one, or an earned bush: #728): its area is set
        /// and the founders come up there (the cell itself and the nearest open ground round it). The new thicket is the last in the list.
        /// False, and no thicket is made, when not one founder could come up (an earned bush; the jar's first always stands).</summary>
        bool FoundThicket(int cell, long month)
        {
            var list = Ts;
            var th = new Thicket { cell = cell, radius = ThicketRadiusFor(list.Count + 1) };
            list.Add(th);
            SyncFirstThicket();
            float x = SiteMap.CXc(cell), z = SiteMap.CZc(cell);
            int made = 0;
            while (made < t.thicketFounders)
            {
                CellsOf(th, thCells, thZones);
                int best = -1; float bd = float.MaxValue;
                foreach (var c in thCells)
                {
                    if (!BushGround(c)) continue;
                    float d = Terrain.Dist(SiteMap.CXc(c), SiteMap.CZc(c), x, z);
                    if (d < bd) { bd = d; best = c; }
                }
                if (best < 0) break;
                st.cover[best] = (byte)EcoCover.Bush; made++;
            }
            if (made == 0 && list.Count > 1) { list.RemoveAt(list.Count - 1); SyncFirstThicket(); return false; }
            if (list.Count == 1 && Cv(cell) == EcoCover.Bush) st.firstBushCell = cell;
            if (made > 0) Reach(EcoMilestones.FirstBush, month);
            return true;
        }

        /// <summary>#728 (landscaping.md 8.2): the Keeper places an earned bush (from the pouch, #726) at <paramref name="cell"/>: it founds a
        /// small thicket of its own there. Needs the first thicket placed (the jar's), a bush in the pouch and valid ground
        /// (<see cref="ThicketRefusal"/> is null). Nothing is spent otherwise.</summary>
        public bool PlaceEarnedBush(int cell, long month, Func<int, bool> settled = null)
        {
            if (!IsPlaced(LifeSpecies.Thicket) || st.bushesPouch <= 0 || ThicketRefusal(cell, settled) != null) return false;
            if (!FoundThicket(cell, month)) return false;
            TakeBush();
            Count();
            return true;
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
            x = Math.Clamp(x, -Terrain.HX + 0.3f, Terrain.HX - 0.3f); z = Terrain.ClampZ(z, 0.3f);
            int p = SiteMap.CellAt(x, z);
            return p >= 0 ? p : c;
        }

        /// <summary>How much a cell is in a thicket: 1 in the heart, falling to 0 at a ragged edge; the most of any thicket; 0 everywhere
        /// before the first is placed.</summary>
        public float ThicketZone(int c)
        {
            var ts = Ts;
            float best = 0f;
            for (int i = 0; i < ts.Count; i++) { float z = ZoneIn(ts[i], c); if (z > best) best = z; }
            return best;
        }

        /// <summary>How much a cell is in one thicket (0..1).</summary>
        float ZoneIn(Thicket th, int c) => Zone(c, th.cell, th.radius, 1);

        /// <summary>The thicket whose area a cell is most in (an index into <see cref="ThicketList"/>), or -1.</summary>
        public int ThicketAt(int c)
        {
            var ts = Ts;
            int at = -1; float best = 0f;
            for (int i = 0; i < ts.Count; i++) { float z = ZoneIn(ts[i], c); if (z > best) { best = z; at = i; } }
            return at;
        }

        float Zone(int c, int centre, float r, int salt)
        {
            float x = SiteMap.CXc(c), z = SiteMap.CZc(c);
            float dx = x - SiteMap.CXc(centre), dz = z - SiteMap.CZc(centre);
            float dist = MathF.Sqrt(dx * dx + dz * dz);
            if (dist >= r * (1f + t.thicketEdge * 0.5f)) return 0f; // beyond the widest the ragged edge ever goes
            // A ragged edge: the radius wobbles by direction, in coarse steps, so the thicket isn't a disc.
            int sector = (int)((MathF.Atan2(dz, dx) + MathF.PI) / (MathF.PI * 2f) * 7f) % 7;
            float rr = r * (1f - t.thicketEdge * 0.5f + t.thicketEdge * Terrain.Hash3(st.seed * 53 + salt, centre, sector));
            float d = dist / rr;
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
