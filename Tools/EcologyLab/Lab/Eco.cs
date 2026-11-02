using System;
using System.Collections.Generic;
using AquariumCiv.Sim.Settle;

namespace EcologyLab
{
    public enum Cover : byte { Bare, Grass, Tall, Bush, Seedling, Young, Tree, Reeds, Water }

    /// <summary>What the Keeper does in one visit (one season). Filled by a <see cref="Player"/>.</summary>
    public sealed class Visit
    {
        public readonly List<(float x, float z, float amount)> mists = new List<(float, float, float)>();
        public bool wipe, snip;
        public (float x, float z)? plant;
        public void Clear() { mists.Clear(); wipe = snip = false; plant = null; }
    }

    /// <summary>The Keeper's one tree (keepers-hands §4).</summary>
    public sealed class KeeperTree
    {
        public int cell = -1;
        public float progress, weeds;
        public int stage;            // 0 seed, 1 sprout, 2 sapling, 3 young, 4 grown
        public int keeperMistings;   // the people learn from these
        public bool wilting;
        public bool Planted => cell >= 0;
        public bool Grown => stage >= 4;
        public static readonly string[] StageNames = { "SEED", "SPROUT", "SAPLING", "YOUNG", "GROWN" };
    }

    /// <summary>
    /// One box's ecology, stepped once a game season. Deterministic: every chance is a hash of (seed, cell, season),
    /// so a run is a pure function of (land, player, tuning).
    /// </summary>
    public sealed class Eco
    {
        public readonly Land land;
        public readonly Tuning t;
        public readonly int seed;

        public readonly float[] soil = new float[Land.N], moist = new float[Land.N];
        public readonly Cover[] cover = new Cover[Land.N];
        public float humidity, grime, droplets;
        /// <summary>The water table (terrain height units) and the pond's level (the table's peak, capped at the spill).</summary>
        public float table, tablePeak, pondLevel;
        public float rabbits, birds;
        public readonly KeeperTree tree = new KeeperTree();

        public int season;                       // steps done; season % 4: 0 spring .. 3 winter
        public float Year => season / 4f;

        // Milestones (game years, -1 = not yet)
        public float campYear = -1, bushYear = -1, seedYear = -1, plantedYear = -1, keeperGrownYear = -1, firstTreeYear = -1,
            surfacedYear = -1, villageYear = -1, rabbitYear = -1, peopleWaterYear = -1;

        // Per-step stats
        public int grass, tall, bush, seedlings, young, grownTrees, reeds, pondCells, wildSeeds;
        public float soakedThisStep, lastShowerX = float.NaN, lastShowerZ;
        public bool showerThisStep;
        public bool SeedAvailable => campYear >= 0 && bushYear >= 0;
        public bool CampStarted => campYear >= 0;

        readonly float[] scratch = new float[Land.N];
        readonly int[] nb = new int[8];

        public Eco(Land land, int seed, Tuning tuning = null)
        {
            this.land = land; this.seed = seed; t = tuning ?? new Tuning();
            for (int c = 0; c < Land.N; c++)
            {
                cover[c] = Cover.Bare;
                soil[c] = t.soilStart + t.soilStartBase * land.baseSoil[c];
                moist[c] = 0.04f;
            }
            humidity = t.humidBase;
            table = tablePeak = land.minHeight - t.tableStartBelow;
            pondLevel = float.NegativeInfinity;
            Count();
        }

        float Chance(int kind, int cell) => Terrain.Hash3(seed * 7919 + kind, cell, season);
        float Chance(int kind, int cell, int extra) => Terrain.Hash3(seed * 7919 + kind * 131 + extra, cell, season);

        public float Light => MathF.Max(t.lightMin, 1f - t.lightGrime * grime - t.lightDroplets * droplets);
        public float LandFrac(int cells) => cells / (float)Land.N;
        public float MeadowFrac => LandFrac(grass + tall);
        public float BushFrac => LandFrac(bush);
        public bool TooHumid => humidity > t.tooHumid;
        public bool TooDry => humidity < t.tooDryHumid;

        /// <summary>One game season: the Keeper's visit (may be empty), then the land.</summary>
        public void Step(Visit visit)
        {
            int s = season % 4;
            soakedThisStep = 0f;
            if (visit != null) ApplyVisit(visit);
            Natural(s);
            Water(s);
            Air();
            Succession(s);
            GrowTree(s);
            Birds(s);
            Animals();
            season++;
            Count();
            Gates();
        }

        // ---------------------------------------------------------------- the Keeper's hands
        void ApplyVisit(Visit v)
        {
            if (v.plant.HasValue && !tree.Planted && SeedAvailable)
            {
                int c = Land.CellAt(v.plant.Value.x, v.plant.Value.z);
                if (c >= 0 && !land.InBasin(c) && cover[c] != Cover.Water)
                {
                    tree.cell = c; cover[c] = Cover.Seedling; plantedYear = Year;
                }
            }
            foreach (var m in v.mists) Mist(m.x, m.z, m.amount);
            if (v.wipe) { grime = 0f; droplets = 0f; }
            if (v.snip && tree.Planted) tree.weeds = 0f;
        }

        /// <summary>The mister: wets a spot (green follows), the air, and some of it soaks to the water table.</summary>
        public void Mist(float x, float z, float amount)
        {
            float r = t.mistRadius;
            int i0 = Math.Max(0, (int)((x - r - Land.X0) / Land.Cell)), i1 = Math.Min(Land.NX - 1, (int)((x + r - Land.X0) / Land.Cell));
            int j0 = Math.Max(0, (int)((z - r - Land.Z0) / Land.Cell)), j1 = Math.Min(Land.NZ - 1, (int)((z + r - Land.Z0) / Land.Cell));
            for (int j = j0; j <= j1; j++)
                for (int i = i0; i <= i1; i++)
                {
                    int c = Land.Index(i, j);
                    float d = Terrain.Dist(Land.CX(i), Land.CZ(j), x, z);
                    if (d > r) continue;
                    float f = 1f - d / r;
                    moist[c] = MathF.Min(1f, moist[c] + t.mistWet * amount * f);
                    if (f >= t.mistGreensAt && cover[c] == Cover.Bare) cover[c] = Cover.Grass;
                }
            humidity = MathF.Min(1f, humidity + t.mistAir * amount);
            soakedThisStep += t.mistDeep * amount;
            if (tree.Planted && Terrain.Dist(Land.CXc(tree.cell), Land.CZc(tree.cell), x, z) < r * 0.5f) tree.keeperMistings++;
        }

        // ---------------------------------------------------------------- weather the people remember
        void Natural(int s)
        {
            float dew = t.dew + t.dewHumid * humidity;
            for (int c = 0; c < Land.N; c++) moist[c] = MathF.Min(1f, moist[c] + dew);
            showerThisStep = false;
            if (Chance(1, 0) < t.showerChance)
            {
                float x = Land.X0 + Chance(2, 0) * Land.NX * Land.Cell, z = Land.Z0 + Chance(3, 0) * Land.NZ * Land.Cell;
                showerThisStep = true; lastShowerX = x; lastShowerZ = z;
                for (int c = 0; c < Land.N; c++)
                {
                    float d = Terrain.Dist(Land.CXc(c), Land.CZc(c), x, z);
                    if (d < t.showerRadius) moist[c] = MathF.Min(1f, moist[c] + t.showerAmount * (1f - d / t.showerRadius));
                }
                humidity = MathF.Min(1f, humidity + 0.08f);
            }
        }

        // ---------------------------------------------------------------- moisture, the water table, the pond
        void Water(int s)
        {
            // Downhill drain: moisture above the floor runs to the lowest neighbour (the lowlands collect it).
            Array.Copy(moist, scratch, Land.N);
            for (int c = 0; c < Land.N; c++)
            {
                float ex = moist[c] - t.drainFloor;
                if (ex <= 0f) continue;
                int n = Land.Neighbours8(c, nb), low = -1;
                float lh = land.height[c];
                for (int k = 0; k < n; k++) if (land.height[nb[k]] < lh) { lh = land.height[nb[k]]; low = nb[k]; }
                if (low < 0) continue;
                float mv = ex * t.drainShare;
                scratch[c] -= mv; scratch[low] += mv;
            }
            float soaked = soakedThisStep;
            float dry = t.drySeason[s] * (1f - t.humidSlowsDrying * humidity);
            for (int c = 0; c < Land.N; c++)
            {
                float m = scratch[c];
                if (m > t.fieldCap) { float sk = (m - t.fieldCap) * t.soakShare; m -= sk; soaked += sk; }
                bool veg = cover[c] != Cover.Bare;
                m *= 1f - dry * (veg ? t.vegHolds : 1f);
                moist[c] = Terrain.Clamp01(m);
            }
            soaked = MathF.Min(soaked, t.soakCapPerSeason); // the soil takes only so much a season; the rest fogs the air
            // The table: rises with what soaks down, a bigger pond takes more per unit, a little lost in summer.
            table += soaked * t.tablePerSoak / (1f + pondCells / t.tablePondDamp);
            if (s == 1) table -= t.tableSummerLoss;
            float cap = land.main.spill - 0.005f;
            if (table > cap) table = cap;
            tablePeak = MathF.Max(tablePeak, table);
            table = MathF.Max(table, tablePeak - t.tableSwing); // never below what the box already has
            pondLevel = MathF.Max(pondLevel, MathF.Min(tablePeak, cap));
            // The pond: basin cells under the level are water (an established pond never shrinks). Near the table, wet soil.
            for (int c = 0; c < Land.N; c++)
            {
                float h = land.height[c];
                if (land.InBasin(c) && h < pondLevel && h < land.fill[c]) { cover[c] = Cover.Water; moist[c] = 1f; continue; }
                float depth = h - table;
                if (depth < t.capillaryWet) moist[c] = MathF.Max(moist[c], t.fieldCap - 0.03f); // wet from below, but never soaks back (no loop)
                else if (depth < t.capillaryDamp) moist[c] = MathF.Max(moist[c], 0.45f);
            }
            soakedThisStep = soaked;
        }

        void Air()
        {
            float veg = 1f - LandFrac(CountCover(Cover.Bare));
            float eq = t.humidBase + t.humidVeg * veg + t.humidPond * MathF.Min(0.3f, LandFrac(pondCells));
            humidity += (eq - humidity) * t.humidToRoom;
            humidity = Terrain.Clamp01(humidity);
            float target = Terrain.Smooth(t.dropletsFrom, t.dropletsFull, humidity);
            droplets += target > droplets ? (target - droplets) * t.dropletRise : (target - droplets) * t.dropletFall;
            grime = MathF.Min(t.grimeCap, grime + t.grimePerSeason);
        }

        // ---------------------------------------------------------------- cover
        float Graze => Terrain.Clamp01(rabbits / ((grass + tall) / 25f + 1f));

        void Succession(int s)
        {
            float g = t.growSeason[s] * (TooDry ? t.tooDryGrowth : 1f);
            float graze = Graze;
            Array.Copy(soil, scratch, Land.N);
            var next = (Cover[])cover.Clone();
            for (int c = 0; c < Land.N; c++)
            {
                var cv = cover[c];
                if (cv == Cover.Water) continue;
                float m = moist[c];
                int n = Land.Neighbours8(c, nb), green = 0, bushes = 0, water = 0;
                for (int k = 0; k < n; k++)
                {
                    var o = cover[nb[k]];
                    if (o != Cover.Bare && o != Cover.Water) green++;
                    if (o == Cover.Bush) bushes++;
                    if (o == Cover.Water) water++;
                }
                // Soil builds under plants (Equilinox's feedback).
                if (s != 3)
                {
                    float add = cv switch { Cover.Grass => t.soilGrass, Cover.Tall => t.soilTall, Cover.Bush => t.soilBush, Cover.Reeds => t.soilTall, Cover.Bare => 0f, _ => t.soilTree };
                    scratch[c] = MathF.Min(1f, soil[c] + add);
                }
                if (c == tree.cell) continue;
                bool basinEdge = land.InBasin(c) && water > 0 && land.height[c] < pondLevel + t.reedBand;
                if (basinEdge && cv != Cover.Reeds && Chance(10, c) < t.reedRate * g) { next[c] = Cover.Reeds; continue; }
                switch (cv)
                {
                    case Cover.Bare:
                        if (m < t.grassAt) break;
                        float nbf = t.grassAlone + (1f - t.grassAlone) * MathF.Min(1f, green / 2f);
                        if (Chance(11, c) < t.grassRate * Terrain.Smooth(t.grassAt, t.grassFull, m) * nbf * g) next[c] = Cover.Grass;
                        break;
                    case Cover.Grass:
                        if (land.yard[c]) break; // the camp keeps its yard short
                        if (m >= t.tallAt && soil[c] >= t.tallSoil && Chance(12, c) < t.tallRate * Terrain.Smooth(t.tallAt, 0.7f, m) * (1f - t.grazeTall * graze) * g) next[c] = Cover.Tall;
                        else if (TryBush(c, m, bushes, graze, g, 1f)) next[c] = Cover.Bush;
                        break;
                    case Cover.Tall:
                        if (TryBush(c, m, bushes, graze, g, 1.5f)) next[c] = Cover.Bush;
                        break;
                    case Cover.Seedling:
                        if (Chance(14, c) < t.seedlingRate * t.treeSeason[s] * Terrain.Smooth(0.15f, 0.45f, m) * (1f - t.grazeSeedling * graze)) next[c] = Cover.Young;
                        break;
                    case Cover.Young:
                        if (Chance(15, c) < t.youngRate * t.treeSeason[s] * Terrain.Smooth(0.15f, 0.45f, m)) next[c] = Cover.Tree;
                        break;
                }
            }
            Array.Copy(scratch, soil, Land.N);
            Array.Copy(next, cover, Land.N);
        }

        bool TryBush(int c, float m, int bushes, float graze, float g, float mult)
        {
            if (land.InBasin(c) || m < t.bushAt || soil[c] < t.bushSoil) return false;
            float p = t.bushRate * mult * (1f + t.bushNeighbour * bushes / 8f) * (1f - t.grazeBush * graze) * g;
            return Chance(13, c) < p;
        }

        // ---------------------------------------------------------------- the one tree
        void GrowTree(int s)
        {
            if (!tree.Planted) return;
            int c = tree.cell;
            // The people learn: after a few mistings a child carries water to it; later someone rings it with sticks.
            if (tree.keeperMistings >= t.learnWater && s != 3)
            {
                if (peopleWaterYear < 0) peopleWaterYear = Year;
                moist[c] = MathF.Min(1f, moist[c] + t.peopleWater);
            }
            // Weeds from the meadow crowd its foot.
            int n = Land.Neighbours8(c, nb), meadow = 0;
            for (int k = 0; k < n; k++) if (cover[nb[k]] == Cover.Grass || cover[nb[k]] == Cover.Tall) meadow++;
            if (s <= 1 && meadow >= t.weedNeighbours && !tree.Grown)
                tree.weeds = MathF.Min(1f, tree.weeds + t.weedGrow * (tree.keeperMistings >= t.learnRing ? t.ringSlowsWeeds : 1f));
            float m = moist[c];
            float water = Terrain.Smooth(t.treeWetAt, t.treeWetFull, m);
            tree.wilting = s != 3 && water < 0.35f;
            if (tree.Grown) return;
            float d = t.treeRate * t.treeSeason[s] * water * Light * (1f - t.weedSlows * tree.weeds) * (TooDry ? 0.5f : 1f);
            tree.progress += d;
            while (tree.stage < 4 && tree.progress >= t.treeStages[tree.stage]) tree.stage++;
            cover[c] = tree.stage >= 4 ? Cover.Tree : tree.stage >= 2 ? Cover.Young : Cover.Seedling;
            if (tree.Grown && keeperGrownYear < 0) keeperGrownYear = Year + 0.25f;
        }

        // ---------------------------------------------------------------- birds spread the forest
        readonly List<int> grownList = new List<int>();

        void Birds(int s)
        {
            // A rare seed dropped by a passing bird: how a box with no Keeper still gets its first tree.
            if (s == 0 && campYear >= 0 && BushFrac >= t.wildSeedAfterBush && Chance(20, 0) < t.wildSeedChance)
            {
                for (int k = 0; k < 8; k++) // it lands somewhere it can take
                {
                    int c = (int)(Chance(21, 0, k) * Land.N) % Land.N;
                    if (TakesSeed(c, Chance(26, 0, k))) { cover[c] = Cover.Seedling; wildSeeds++; break; }
                }
            }
            if (s != 0 && s != 2) return; // spring and autumn
            grownList.Clear();
            for (int c = 0; c < Land.N; c++) if (cover[c] == Cover.Tree) grownList.Add(c);
            if (grownList.Count == 0) return;
            int seeds = Math.Min(t.maxSeedsPerSeason, (int)MathF.Round(birds * t.seedsPerBird));
            for (int k = 0; k < seeds; k++)
            {
                int src = grownList[(int)(Chance(22, k) * grownList.Count) % grownList.Count];
                // Most seeds fall near the tree, a few far (jays cache at the wood's edge): r = reach * u^falloff.
                float ang = Chance(23, k) * MathF.PI * 2f, rr = t.seedReach * MathF.Pow(Chance(24, k), t.seedFalloff);
                int di = (int)MathF.Round(MathF.Cos(ang) * rr), dj = (int)MathF.Round(MathF.Sin(ang) * rr);
                int i = Land.I(src) + di, j = Land.J(src) + dj;
                if (i < 0 || j < 0 || i >= Land.NX || j >= Land.NZ) continue;
                int c = Land.Index(i, j);
                if (TakesSeed(c, Chance(25, k))) cover[c] = Cover.Seedling;
            }
        }

        /// <summary>Jays cache in open ground and at edges; bushes shelter the seedling; never in the flood line or the yard.</summary>
        bool TakesSeed(int c, float roll)
        {
            if (land.InBasin(c) || land.yard[c] || c == tree.cell) return false;
            float p = cover[c] switch
            {
                Cover.Grass => t.seedTakeGrass, Cover.Tall => t.seedTakeTall, Cover.Bush => t.seedTakeBush,
                Cover.Bare => moist[c] > 0.3f ? t.seedTakeBare : 0f, _ => 0f,
            };
            return roll < p;
        }

        void Animals()
        {
            float meadow = grass + tall + 0.3f * bush;
            if (rabbits <= 0f && MeadowFrac >= t.rabbitsAtMeadow) { rabbits = 2f; if (rabbitYear < 0) rabbitYear = Year; }
            if (rabbits > 0f)
            {
                float k = meadow / t.meadowPerRabbit;
                rabbits += k > rabbits ? (k - rabbits) * t.rabbitGrow : (k - rabbits) * t.rabbitFall;
                rabbits = MathF.Max(rabbits, 2f);
            }
            float kb = bush * t.birdsPerBush + grownTrees * t.birdsPerTree;
            birds += (kb - birds) * 0.3f;
        }

        // ---------------------------------------------------------------- counts and gates
        int CountCover(Cover cv) { int n = 0; for (int c = 0; c < Land.N; c++) if (cover[c] == cv) n++; return n; }

        void Count()
        {
            grass = tall = bush = seedlings = young = grownTrees = reeds = pondCells = 0;
            for (int c = 0; c < Land.N; c++)
                switch (cover[c])
                {
                    case Cover.Grass: grass++; break;
                    case Cover.Tall: tall++; break;
                    case Cover.Bush: bush++; break;
                    case Cover.Seedling: seedlings++; break;
                    case Cover.Young: young++; break;
                    case Cover.Tree: grownTrees++; break;
                    case Cover.Reeds: reeds++; break;
                    case Cover.Water: pondCells++; break;
                }
        }

        void Gates()
        {
            float y = Year;
            if (campYear < 0 && MeadowFrac >= t.campMeadow && rabbits >= t.campRabbits) campYear = y;
            if (bushYear < 0 && campYear >= 0 && BushFrac >= t.seedUnlockBush) { bushYear = y; seedYear = y; }
            if (firstTreeYear < 0 && grownTrees > 0) firstTreeYear = y;
            if (surfacedYear < 0 && pondCells > 0) surfacedYear = y;
            if (villageYear < 0 && campYear >= 0 && pondCells >= t.pondGateCells && grownTrees >= t.villageTrees) villageYear = y;
        }

        /// <summary>A stable fingerprint of the whole state (determinism tests).</summary>
        public ulong Fingerprint()
        {
            unchecked
            {
                ulong h = 1469598103934665603UL;
                void Mix(ulong v) { h ^= v; h *= 1099511628211UL; }
                for (int c = 0; c < Land.N; c++) { Mix((ulong)cover[c]); Mix((ulong)BitConverter.SingleToInt32Bits(moist[c])); Mix((ulong)BitConverter.SingleToInt32Bits(soil[c])); }
                Mix((ulong)BitConverter.SingleToInt32Bits(table)); Mix((ulong)BitConverter.SingleToInt32Bits(humidity));
                Mix((ulong)BitConverter.SingleToInt32Bits(rabbits)); Mix((ulong)BitConverter.SingleToInt32Bits(tree.progress));
                return h;
            }
        }
    }
}
