using System;
using System.Collections.Generic;
using AquariumCiv.Sim.Settle;

namespace AquariumCiv.Sim.Eco
{
    /// <summary>
    /// The land model, stepped once a game season (Ecology Lab's Eco.cs, #228, ported close to verbatim). It works on an
    /// <see cref="EcoState"/> (the save) over an <see cref="EcoLand"/> (the static land). Deterministic and dice-free:
    /// every chance is a hash of (seed, cell, season). The Keeper's hands (<see cref="Mist"/>, <see cref="Plant"/>,
    /// <see cref="Wipe"/>, <see cref="Snip"/>) act at once, between steps; the next step carries them on.
    /// </summary>
    public sealed partial class EcoModel
    {
        public readonly EcoState st;
        public readonly EcoLand land;
        public readonly EcoTuning t;

        /// <summary>Milestones and gates reached since the last <see cref="Drain"/>, for the view and the chronicle.</summary>
        public readonly List<string> newMilestones = new List<string>(), newGates = new List<string>();

        readonly float[] scratch = new float[EcoLand.N];
        readonly int[] nb = new int[8];
        readonly List<int> grownList = new List<int>();
        readonly EcoLiveCounts live = new EcoLiveCounts();

        public EcoModel(EcoState state, EcoLand land, EcoTuning tuning)
        {
            st = state; this.land = land; t = tuning;
            if (st.on && st.cover != null) { EnsureLayers(); EnsureJars(); }
        }

        /// <summary>An old save has no wet, puddle or sprout layers: start them empty.</summary>
        void EnsureLayers()
        {
            if (st.wet == null || st.wet.Length != EcoLand.N) st.wet = new float[EcoLand.N];
            if (st.puddle == null || st.puddle.Length != EcoLand.N) st.puddle = new float[EcoLand.N];
            if (st.sprout == null || st.sprout.Length != EcoLand.N) st.sprout = new float[EcoLand.N];
            if (st.watered == null || st.watered.Length != EcoLand.N) st.watered = new float[EcoLand.N]; // #569
        }

        /// <summary>#569: how much of the mister's water the ground at <paramref name="c"/> still remembers, 0..1.</summary>
        public float Watered(int c) => st.watered == null ? 0f : st.watered[c];

        /// <summary>The barren start: bare ground, a thin soil, the table below the lowest ground, no tree.</summary>
        public static void Init(EcoState st, EcoLand land, EcoTuning t, int seed)
        {
            st.on = true; st.seed = seed; st.season = 0;
            st.cover = new byte[EcoLand.N];
            st.soil = new float[EcoLand.N];
            st.moisture = new float[EcoLand.N];
            st.wet = new float[EcoLand.N]; st.puddle = new float[EcoLand.N]; st.sprout = new float[EcoLand.N]; st.watered = new float[EcoLand.N];
            for (int c = 0; c < EcoLand.N; c++)
            {
                st.cover[c] = (byte)EcoCover.Bare;
                st.soil[c] = t.soilStart + t.soilStartBase * land.baseSoil[c];
                st.moisture[c] = 0.04f;
            }
            st.humidity = t.humidBase; st.grime = 0f; st.droplets = 0f; st.soaked = 0f;
            st.table = st.tablePeak = land.minHeight - t.tableStartBelow;
            st.pondLevel = -1000f;
            st.rabbits = st.birds = 0f; st.wildSeeds = 0;
            st.treeCell = -1; st.treeProgress = 0f; st.treeWeeds = 0f; st.treeStage = 0; st.keeperMistings = 0; st.treeWilting = false;
            st.campGateMonth = st.villageGateMonth = -1;
            st.milestoneKeys = new List<string>(); st.milestoneMonths = new List<long>();
            st.jarOn = true; st.jars = new List<LifeJarEntry>(); st.thicketA = st.thicketB = -1; st.thicketRadius = 0f; // #417: every jar starts shut
            new EcoModel(st, land, t).Count();
        }

        EcoCover Cv(int c) => (EcoCover)st.cover[c];
        float Chance(int kind, int cell) => Terrain.Hash3(st.seed * 7919 + kind, cell, st.season);
        float Chance(int kind, int cell, int extra) => Terrain.Hash3(st.seed * 7919 + kind * 131 + extra, cell, st.season);

        // ---------------------------------------------------------------- readings
        public float Light => MathF.Max(t.lightMin, 1f - t.lightGrime * st.grime - t.lightDroplets * st.droplets);
        public bool TooHumid => st.humidity > t.tooHumid;
        public bool TooDry => st.humidity < t.tooDryHumid;
        static float Frac(int cells) => cells / (float)EcoLand.N;
        // The step's own counts (taken at the end of the last step; the lab's model reads them mid-step).
        float MeadowFrac => Frac(st.cGrass + st.cTall);
        float BushFrac => Frac(st.cBush);
        public bool SeedAvailable => st.campGateMonth >= 0 && st.Reached(EcoMilestones.SeedUnlocked);

        /// <summary>Counts of the cover as it is right now (the view and the sound read these).</summary>
        public EcoLiveCounts Live()
        {
            live.Clear();
            for (int c = 0; c < EcoLand.N; c++)
                switch (Cv(c))
                {
                    case EcoCover.Bare: live.bare++; break;
                    case EcoCover.Grass: live.grass++; break;
                    case EcoCover.Tall: live.tall++; break;
                    case EcoCover.Bush: live.bush++; break;
                    case EcoCover.Seedling: live.seedlings++; break;
                    case EcoCover.Young: live.young++; break;
                    case EcoCover.Tree: live.trees++; break;
                    case EcoCover.Reeds: live.reeds++; break;
                    case EcoCover.Water: live.pond++; break;
                }
            return live;
        }

        public bool Reach(string key, long month)
        {
            if (st.Reached(key)) return false;
            st.milestoneKeys.Add(key); st.milestoneMonths.Add(month);
            newMilestones.Add(key);
            return true;
        }

        // ---------------------------------------------------------------- one season
        /// <summary>One game season (<paramref name="s"/>: 0 spring .. 3 winter), at game month <paramref name="month"/>.</summary>
        public void Step(int s, long month)
        {
            Natural(s);
            Water(s);
            Air();
            Thickets();
            Succession(s);
            GrowTree(s, month);
            Birds(s);
            Animals(month);
            PlacedSeason(s, month); // #420, #421: the deer, the ducks and the fish the Keeper placed
            Vines(s);
            FadeWatered(); // #569
            st.season++;
            Count();
            Gates(month);
            UpdateJars(month); // #417: the jars the land is ready for open
        }

        // ---------------------------------------------------------------- the Keeper's hands
        /// <summary>The mister: wets a spot (green follows at once), the air, and some of it soaks to the water table.
        /// Fills <paramref name="greened"/> with the cells that turned to grass.</summary>
        public void Mist(float x, float z, float amount, long month, List<int> greened, float holdAmount = -1f)
        {
            EnsureLayers();
            float rAmt = holdAmount >= 0f ? Math.Max(amount, holdAmount) : amount; // the reach and the puddles go by the hold, the wetting by this pulse
            float r = MistRadius(rAmt);
            int i0 = Math.Max(0, (int)((x - r - SiteMap.X0) / SiteMap.Cell)), i1 = Math.Min(SiteMap.NX - 1, (int)((x + r - SiteMap.X0) / SiteMap.Cell));
            int j0 = Math.Max(0, (int)((z - r - SiteMap.Z0) / SiteMap.Cell)), j1 = Math.Min(SiteMap.NZ - 1, (int)((z + r - SiteMap.Z0) / SiteMap.Cell));
            int nearGreen = 0;
            var bare = new List<(float d, int c)>();
            for (int j = j0; j <= j1; j++)
                for (int i = i0; i <= i1; i++)
                {
                    int c = SiteMap.Index(i, j);
                    float d = Terrain.Dist(SiteMap.CX(i), SiteMap.CZ(j), x, z);
                    if (d > r) continue;
                    float f = 1f - d / r;
                    st.moisture[c] = MathF.Min(1f, st.moisture[c] + t.mistWet * amount * f);
                    if (Cv(c) == EcoCover.Water) continue;
                    st.watered[c] = MathF.Max(st.watered[c], t.wateredAt * MathF.Sqrt(f) * Terrain.Clamp01(0.5f + amount * 0.5f)); // #569: the ground remembers
                    st.wet[c] = MathF.Min(1f, MathF.Max(st.wet[c], t.mistShow * MathF.Sqrt(f) * Terrain.Clamp01(0.5f + amount * 0.5f)));
                    float soak = rAmt * f - t.puddleAt;
                    if (soak > 0f && land.slope[c] <= t.puddleMaxSlope && !land.yard[c])
                        st.puddle[c] = MathF.Min(1f, st.puddle[c] + soak * t.puddleGain);
                    if (Cv(c) == EcoCover.Bare)
                    {
                        if (f >= t.mistGreensAt) { st.cover[c] = (byte)EcoCover.Grass; greened?.Add(c); st.sprout[c] = 0f; Reach(EcoMilestones.FirstGreen, month); }
                        else
                        {
                            bare.Add((d, c));
                            if (f >= t.mistSproutAt) st.sprout[c] = MathF.Max(st.sprout[c], t.sproutCommit + (t.mistSprout - t.sproutCommit) * (f - t.mistSproutAt) / (t.mistGreensAt - t.mistSproutAt));
                        }
                    }
                    else if (d <= r * 0.5f) nearGreen++;
                }
            // Every mist shows a little green at once: if the heart of this one is bare, its nearest bare cells turn now.
            int have = nearGreen + (greened?.Count ?? 0);
            if (have < t.mistMinGreen && bare.Count > 0)
            {
                bare.Sort((p, q) => p.d.CompareTo(q.d));
                for (int k = 0; k < bare.Count && have < t.mistMinGreen; k++)
                {
                    int c = bare[k].c;
                    st.cover[c] = (byte)EcoCover.Grass; st.sprout[c] = 0f; greened?.Add(c); have++;
                    Reach(EcoMilestones.FirstGreen, month);
                }
            }
            st.humidity = MathF.Min(1f, st.humidity + t.mistAir * amount);
            st.soaked += t.mistDeep * amount;
            if (st.treeCell >= 0 && Terrain.Dist(SiteMap.CXc(st.treeCell), SiteMap.CZc(st.treeCell), x, z) < r * 0.5f) st.keeperMistings++;
        }

        /// <summary>How far a mist reaches after being held this much (<paramref name="amount"/>: 1 = the lab's "spot", capped at
        /// <see cref="EcoTuning.mistMaxAmount"/>). A short mist reaches mistRadius; a full hold, mistRadiusMax.</summary>
        public float MistRadius(float amount)
        {
            float k = t.mistMaxAmount <= 0f ? 1f : Terrain.Clamp01(amount / t.mistMaxAmount);
            return t.mistRadius + (t.mistRadiusMax - t.mistRadius) * k;
        }

        /// <summary>#569: once a season the watered ground forgets a little.</summary>
        void FadeWatered()
        {
            var w = st.watered;
            if (w == null) return;
            for (int c = 0; c < EcoLand.N; c++)
            {
                float v = w[c] * t.wateredKeep;
                w[c] = v < 0.02f ? 0f : v;
            }
        }

        /// <summary>The ground's dampness for growth: the soil's own, or the water the mister left, whichever is more.</summary>
        float Damp(int c) => st.watered == null ? st.moisture[c] : MathF.Max(st.moisture[c], t.wateredMoist * st.watered[c]);

        // ---------------------------------------------------------------- the wet area, the shoots, the puddles (monthly)
        float ChanceM(int kind, int cell, long month) => Terrain.Hash3(st.seed * 7919 + kind * 977, cell, (int)(month & 0x7fffffff));

        /// <summary>One game month of the surface: the wet area dries, puddles dry or sink, shoots grow where it is wet and
        /// along a green edge, and an odd sprout follows dew. <paramref name="s"/> is the season this month is in.</summary>
        public void Month(int s, long month)
        {
            EnsureLayers();
            var wet = st.wet; var pud = st.puddle; var sp = st.sprout;
            float keep = 1f - t.wetDry * (1f - t.wetDryHumid * st.humidity);
            float dryS = t.drySeason[s] / 0.2f;
            for (int c = 0; c < EcoLand.N; c++)
            {
                var cv = Cv(c);
                if (cv == EcoCover.Water) { wet[c] = 0f; pud[c] = 0f; sp[c] = 0f; continue; }
                float w = wet[c] * keep;
                if (w < t.wetGone) w = 0f;
                float p = pud[c];
                if (p > 0f)
                {
                    float lost = MathF.Min(p, t.puddleDry * dryS);
                    float sunk = MathF.Min(p - lost, p * t.puddleSink);
                    p -= lost + sunk;
                    w = MathF.Max(w, MathF.Min(1f, 0.4f + p)); // a puddle's edge stays dark
                    st.moisture[c] = MathF.Min(1f, st.moisture[c] + sunk * 0.2f);
                    if (p < t.puddleGone) p = 0f;
                }
                wet[c] = w; pud[c] = p;
                if (cv != EcoCover.Bare) { sp[c] = 0f; continue; }
                // Bare ground: shoots come where it is wet, along a green edge, and the odd one after dew.
                float prog = sp[c] * t.sproutKeep;
                int n = EcoLand.Neighbours8(c, nb), green = 0;
                for (int k = 0; k < n; k++) { var o = Cv(nb[k]); if (o != EcoCover.Bare && o != EcoCover.Water) green++; }
                float m = st.moisture[c];
                if (prog >= t.sproutCommit && m >= t.grassAt) prog += t.sproutRate * (1f + 0.5f * MathF.Min(1f, green / 2f)); // shoots that came up grow on
                else if (w >= t.sproutWetAt && m >= t.grassAt) prog += t.sproutWetRate * w;                       // wet ground alone, slowly
                if (green > 0 && m >= t.creepMoist && ChanceM(1, c, month) < t.creepChance * t.growSeason[s] * MathF.Min(1f, green / 2f + 0.5f)) prog += t.creepStep;
                if (m >= t.dewSproutMoist && ChanceM(2, c, month) < t.dewSprout * t.growSeason[s]) prog += t.creepStep;
                if (prog >= 1f)
                {
                    st.cover[c] = (byte)EcoCover.Grass; prog = 0f;
                    Reach(EcoMilestones.FirstGreen, month);
                }
                sp[c] = MathF.Min(prog, 1f);
            }
        }

        /// <summary>The cells holding a puddle of at least <paramref name="minStrength"/>, strongest first (into <paramref name="into"/>).</summary>
        public void PuddleCells(List<int> into, float minStrength = 0.05f)
        {
            into.Clear();
            if (st.puddle == null) return;
            for (int c = 0; c < EcoLand.N; c++) if (st.puddle[c] >= minStrength) into.Add(c);
            var p = st.puddle;
            into.Sort((a, b) => p[b].CompareTo(p[a]));
        }

        /// <summary>The nearest puddle strong enough to drink from, within <paramref name="reach"/> of (x, z), or -1.</summary>
        public int NearestPuddle(float x, float z, float reach)
        {
            if (st.puddle == null) return -1;
            int best = -1; float bd = reach;
            for (int c = 0; c < EcoLand.N; c++)
            {
                if (st.puddle[c] < t.puddleDrinkAt) continue;
                float d = Terrain.Dist(SiteMap.CXc(c), SiteMap.CZc(c), x, z);
                if (d <= bd) { bd = d; best = c; }
            }
            return best;
        }

        /// <summary>Someone drinks from a puddle: it shrinks by <paramref name="amount"/>. Returns what was drawn.</summary>
        public float DrawPuddle(int c, float amount)
        {
            if (st.puddle == null || c < 0 || c >= EcoLand.N) return 0f;
            float d = MathF.Min(amount, st.puddle[c]);
            st.puddle[c] -= d;
            return d;
        }

        /// <summary>Debug: soak the ground round (x, z) now: a wide wet area and puddles on the low ground in it.</summary>
        public int DebugPuddles(float x, float z, float radius)
        {
            EnsureLayers();
            int made = 0;
            for (int c = 0; c < EcoLand.N; c++)
            {
                if (Cv(c) == EcoCover.Water) continue;
                float d = Terrain.Dist(SiteMap.CXc(c), SiteMap.CZc(c), x, z);
                if (d > radius) continue;
                float f = 1f - d / radius;
                st.wet[c] = MathF.Max(st.wet[c], MathF.Sqrt(f));
                st.moisture[c] = MathF.Max(st.moisture[c], 0.5f + 0.4f * f);
                if (land.slope[c] <= t.puddleMaxSlope && !land.yard[c] && f > 0.25f) { st.puddle[c] = MathF.Max(st.puddle[c], 0.35f + 0.65f * f); made++; }
            }
            return made;
        }

        /// <summary>#378 (playtest #342): the Keeper's tree is not planted in the bed's outer cells. Its crown is about a quarter
        /// unit across, so a tree in the last cells pokes through the glass and hangs over the edge; this many cells from each wall stay clear.</summary>
        public const int TreeEdgeMargin = 3;

        /// <summary>True when the cell lies inside the tree's bed: at least <see cref="TreeEdgeMargin"/> cells from every wall.</summary>
        public static bool InTreeBed(int c)
        {
            if (c < 0 || c >= SiteMap.N) return false;
            int i = SiteMap.I(c), j = SiteMap.J(c);
            return i >= TreeEdgeMargin && i < SiteMap.NX - TreeEdgeMargin && j >= TreeEdgeMargin && j < SiteMap.NZ - TreeEdgeMargin;
        }

        /// <summary>Where a seed pressed at (x, z) really goes: that cell when it is dry ground inside the tree's bed, else the
        /// nearest such cell (within a few cells), else -1 (refused). The view shows the answer, so nothing jumps silently.</summary>
        public int TreeCellFor(float x, float z)
        {
            int c0 = SiteMap.CellAt(x, z);
            if (c0 < 0) return -1;
            bool Good(int c) => InTreeBed(c) && !land.InBasin(c) && Cv(c) != EcoCover.Water;
            if (Good(c0)) return c0;
            int i0 = SiteMap.I(c0), j0 = SiteMap.J(c0), best = -1;
            float bestD = (TreeEdgeMargin + 2) * (TreeEdgeMargin + 2) + 0.5f;
            for (int j = Math.Max(0, j0 - TreeEdgeMargin - 2); j <= Math.Min(SiteMap.NZ - 1, j0 + TreeEdgeMargin + 2); j++)
                for (int i = Math.Max(0, i0 - TreeEdgeMargin - 2); i <= Math.Min(SiteMap.NX - 1, i0 + TreeEdgeMargin + 2); i++)
                {
                    int c = SiteMap.Index(i, j);
                    if (!Good(c)) continue;
                    float d = (i - i0) * (i - i0) + (j - j0) * (j - j0);
                    if (d < bestD) { bestD = d; best = c; }
                }
            return best;
        }

        /// <summary>The seed goes into open ground (not the hollow, not the water), once the seed is unlocked.</summary>
        public bool Plant(float x, float z, long month)
        {
            if (st.treeCell >= 0 || !SeedAvailable) return false;
            int c = TreeCellFor(x, z);
            if (c < 0) return false;
            st.treeCell = c; st.cover[c] = (byte)EcoCover.Seedling;
            Reach(EcoMilestones.TreePlanted, month);
            return true;
        }

        /// <summary>Wiping the glass: grime and droplets come off.</summary>
        public void Wipe(float amount)
        {
            st.grime = MathF.Max(0f, st.grime - amount);
            st.droplets = MathF.Max(0f, st.droplets - amount);
        }

        /// <summary>Snipping the weeds at the tree's foot.</summary>
        public void Snip(float amount)
        {
            if (st.treeCell >= 0) st.treeWeeds = MathF.Max(0f, st.treeWeeds - amount);
        }

        // ---------------------------------------------------------------- weather the people remember
        void Natural(int s)
        {
            var m = st.moisture;
            float dew = t.dew + t.dewHumid * st.humidity;
            for (int c = 0; c < EcoLand.N; c++) m[c] = MathF.Min(1f, m[c] + dew);
            if (Chance(1, 0) < t.showerChance)
            {
                float x = SiteMap.X0 + Chance(2, 0) * SiteMap.NX * SiteMap.Cell, z = SiteMap.Z0 + Chance(3, 0) * SiteMap.NZ * SiteMap.Cell;
                for (int c = 0; c < EcoLand.N; c++)
                {
                    float d = Terrain.Dist(SiteMap.CXc(c), SiteMap.CZc(c), x, z);
                    if (d < t.showerRadius)
                    {
                        float f = 1f - d / t.showerRadius;
                        m[c] = MathF.Min(1f, m[c] + t.showerAmount * f);
                        if (Cv(c) != EcoCover.Water && st.wet != null) st.wet[c] = MathF.Max(st.wet[c], t.showerWet * f);
                    }
                }
                st.humidity = MathF.Min(1f, st.humidity + 0.08f);
            }
        }

        // ---------------------------------------------------------------- moisture, the water table, the pond
        void Water(int s)
        {
            var moist = st.moisture;
            // Downhill drain: moisture above the floor runs to the lowest neighbour (the lowlands collect it).
            Array.Copy(moist, scratch, EcoLand.N);
            for (int c = 0; c < EcoLand.N; c++)
            {
                float ex = moist[c] - t.drainFloor;
                if (ex <= 0f) continue;
                int n = EcoLand.Neighbours8(c, nb), low = -1;
                float lh = land.height[c];
                for (int k = 0; k < n; k++) if (land.height[nb[k]] < lh) { lh = land.height[nb[k]]; low = nb[k]; }
                if (low < 0) continue;
                float mv = ex * t.drainShare;
                scratch[c] -= mv; scratch[low] += mv;
            }
            float soaked = st.soaked;
            float dry = t.drySeason[s] * (1f - t.humidSlowsDrying * st.humidity);
            for (int c = 0; c < EcoLand.N; c++)
            {
                float m = scratch[c];
                if (m > t.fieldCap) { float sk = (m - t.fieldCap) * t.soakShare; m -= sk; soaked += sk; }
                bool veg = Cv(c) != EcoCover.Bare;
                m *= 1f - dry * (veg ? t.vegHolds : 1f);
                moist[c] = Terrain.Clamp01(m);
            }
            soaked = MathF.Min(soaked, t.soakCapPerSeason); // the soil takes only so much a season; the rest fogs the air
            // The table: rises with what soaks down, a bigger pond takes more per unit, a little lost in summer.
            st.table += soaked * t.tablePerSoak / (1f + st.cPond / t.tablePondDamp);
            if (s == 1) st.table -= t.tableSummerLoss;
            float cap = land.main.spill - 0.005f;
            if (st.table > cap) st.table = cap;
            st.tablePeak = MathF.Max(st.tablePeak, st.table);
            st.table = MathF.Max(st.table, st.tablePeak - t.tableSwing); // never below what the box already has
            st.pondLevel = MathF.Max(st.pondLevel, MathF.Min(st.tablePeak, cap));
            // The pond: basin cells under the level are water (an established pond never shrinks). Near the table, wet soil.
            for (int c = 0; c < EcoLand.N; c++)
            {
                float h = land.height[c];
                if (land.InBasin(c) && h < st.pondLevel && h < land.fill[c]) { st.cover[c] = (byte)EcoCover.Water; moist[c] = 1f; continue; }
                float depth = h - st.table;
                if (depth < t.capillaryWet) moist[c] = MathF.Max(moist[c], t.fieldCap - 0.03f); // wet from below, but never soaks back (no loop)
                else if (depth < t.capillaryDamp) moist[c] = MathF.Max(moist[c], 0.45f);
            }
            st.soaked = 0f; // taken: what the Keeper's mist sends down counts once
        }

        void Air()
        {
            int bare = 0;
            for (int c = 0; c < EcoLand.N; c++) if (Cv(c) == EcoCover.Bare) bare++;
            float veg = 1f - Frac(bare);
            float eq = t.humidBase + t.humidVeg * veg + t.humidPond * MathF.Min(0.3f, Frac(st.cPond));
            st.humidity += (eq - st.humidity) * t.humidToRoom;
            st.humidity = Terrain.Clamp01(st.humidity);
            float target = Terrain.Smooth(t.dropletsFrom, t.dropletsFull, st.humidity);
            st.droplets += target > st.droplets ? (target - st.droplets) * t.dropletRise : (target - st.droplets) * t.dropletFall;
            st.grime = MathF.Min(t.grimeCap, st.grime + t.grimePerSeason);
        }

        // ---------------------------------------------------------------- cover
        float Graze => Terrain.Clamp01(st.rabbits / ((st.cGrass + st.cTall) / 25f + 1f));

        void Succession(int s)
        {
            var moist = st.moisture;
            var soil = st.soil;
            float g = t.growSeason[s] * (TooDry ? t.tooDryGrowth : 1f);
            float graze = Graze;
            Array.Copy(soil, scratch, EcoLand.N);
            var next = (byte[])st.cover.Clone();
            for (int c = 0; c < EcoLand.N; c++)
            {
                var cv = Cv(c);
                if (cv == EcoCover.Water) continue;
                float m = moist[c];
                float wd = Watered(c); // #569
                int n = EcoLand.Neighbours8(c, nb), green = 0, bushes = 0, water = 0;
                for (int k = 0; k < n; k++)
                {
                    var o = Cv(nb[k]);
                    if (o != EcoCover.Bare && o != EcoCover.Water) green++;
                    if (o == EcoCover.Bush) bushes++;
                    if (o == EcoCover.Water) water++;
                }
                // Soil builds under plants (Equilinox's feedback).
                if (s != 3)
                {
                    float add;
                    switch (cv)
                    {
                        case EcoCover.Grass: add = t.soilGrass; break;
                        case EcoCover.Tall: add = t.soilTall; break;
                        case EcoCover.Bush: add = t.soilBush; break;
                        case EcoCover.Reeds: add = t.soilTall; break;
                        case EcoCover.Bare: add = 0f; break;
                        default: add = t.soilTree; break;
                    }
                    scratch[c] = MathF.Min(1f, soil[c] + add);
                }
                if (c == st.treeCell) continue;
                bool basinEdge = land.InBasin(c) && water > 0 && land.height[c] < st.pondLevel + t.reedBand;
                if (basinEdge && cv != EcoCover.Reeds && Chance(10, c) < t.reedRate * g * DuckReedBoost(c)) { next[c] = (byte)EcoCover.Reeds; continue; } // #421: first where the ducks were placed
                switch (cv)
                {
                    case EcoCover.Bare:
                        if (m < t.grassAt) break;
                        float nbf = t.grassAlone + (1f - t.grassAlone) * MathF.Min(1f, green / 2f);
                        if (Chance(11, c) < t.grassRate * Terrain.Smooth(t.grassAt, t.grassFull, m) * nbf * g * (1f + t.wateredGreen * wd)) next[c] = (byte)EcoCover.Grass;
                        break;
                    case EcoCover.Grass:
                        if (land.yard[c]) break; // the camp keeps its yard short
                        if (m >= t.tallAt && soil[c] >= t.tallSoil && Chance(12, c) < t.tallRate * Terrain.Smooth(t.tallAt, 0.7f, m) * (1f - t.grazeTall * graze) * g * (1f + t.wateredGreen * wd)) next[c] = (byte)EcoCover.Tall;
                        break; // #418: no bush comes up by itself; the placed thicket fills (Thickets)
                    case EcoCover.Bush:
                        if (ThinBush(c)) next[c] = (byte)(m >= t.tallAt && !land.yard[c] ? EcoCover.Tall : EcoCover.Grass); // #385
                        break;
                    case EcoCover.Seedling:
                        if (Chance(14, c) < t.seedlingRate * t.treeSeason[s] * Terrain.Smooth(0.15f, 0.45f, Damp(c)) * (1f - t.grazeSeedling * graze) * (1f + t.wateredGrow * wd)) next[c] = (byte)EcoCover.Young;
                        break;
                    case EcoCover.Young:
                        if (Chance(15, c) < t.youngRate * t.treeSeason[s] * Terrain.Smooth(0.15f, 0.45f, Damp(c)) * (1f + t.wateredGrow * wd)) next[c] = (byte)EcoCover.Tree;
                        break;
                }
            }
            Array.Copy(scratch, soil, EcoLand.N);
            Array.Copy(next, st.cover, EcoLand.N);
        }

        // ---------------------------------------------------------------- the one tree
        void GrowTree(int s, long month)
        {
            if (st.treeCell < 0) return;
            int c = st.treeCell;
            // The people learn: after a few mistings a child carries water to it; later someone rings it with sticks.
            if (st.keeperMistings >= t.learnWater && s != 3)
            {
                Reach(EcoMilestones.PeopleWater, month);
                st.moisture[c] = MathF.Min(1f, st.moisture[c] + t.peopleWater);
            }
            // Weeds from the meadow crowd its foot.
            int n = EcoLand.Neighbours8(c, nb), meadow = 0;
            for (int k = 0; k < n; k++) if (Cv(nb[k]) == EcoCover.Grass || Cv(nb[k]) == EcoCover.Tall) meadow++;
            bool grown = st.treeStage >= 4;
            if (s <= 1 && meadow >= t.weedNeighbours && !grown)
                st.treeWeeds = MathF.Min(1f, st.treeWeeds + t.weedGrow * (st.keeperMistings >= t.learnRing ? t.ringSlowsWeeds : 1f));
            float m = Damp(c);
            float water = Terrain.Smooth(t.treeWetAt, t.treeWetFull, m);
            st.treeWilting = s != 3 && water < 0.35f;
            if (grown) return;
            float d = t.treeRate * t.treeSeason[s] * water * (1f + t.wateredKeeper * Watered(c)) * Light * (1f - t.weedSlows * st.treeWeeds) * (TooDry ? 0.5f : 1f);
            st.treeProgress += d;
            while (st.treeStage < 4 && st.treeProgress >= t.treeStages[st.treeStage])
            {
                st.treeStage++;
                Reach(EcoMilestones.TreeStages[st.treeStage - 1], month);
            }
            st.cover[c] = (byte)(st.treeStage >= 4 ? EcoCover.Tree : st.treeStage >= 2 ? EcoCover.Young : EcoCover.Seedling);
        }

        // ---------------------------------------------------------------- birds spread the forest
        void Birds(int s)
        {
            // A rare seed dropped by a passing bird: how a box with no Keeper still gets its first tree.
            if (s == 0 && st.campGateMonth >= 0 && BushFrac >= t.wildSeedAfterBush && Chance(20, 0) < t.wildSeedChance)
            {
                for (int k = 0; k < 8; k++) // it lands somewhere it can take
                {
                    int c = (int)(Chance(21, 0, k) * EcoLand.N) % EcoLand.N;
                    if (TakesSeed(c, Chance(26, 0, k))) { st.cover[c] = (byte)EcoCover.Seedling; st.wildSeeds++; break; }
                }
            }
            if (s != 0 && s != 2) return; // spring and autumn
            grownList.Clear();
            for (int c = 0; c < EcoLand.N; c++) if (Cv(c) == EcoCover.Tree) grownList.Add(c);
            if (grownList.Count == 0) return;
            int seeds = Math.Min(t.maxSeedsPerSeason, (int)MathF.Round(st.birds * t.seedsPerBird));
            for (int k = 0; k < seeds; k++)
            {
                int src = grownList[(int)(Chance(22, k) * grownList.Count) % grownList.Count];
                // Most seeds fall near the tree, a few far (jays cache at the wood's edge): r = reach * u^falloff.
                float ang = Chance(23, k) * MathF.PI * 2f, rr = t.seedReach * MathF.Pow(Chance(24, k), t.seedFalloff);
                int di = (int)MathF.Round(MathF.Cos(ang) * rr), dj = (int)MathF.Round(MathF.Sin(ang) * rr);
                int i = SiteMap.I(src) + di, j = SiteMap.J(src) + dj;
                if (i < 0 || j < 0 || i >= SiteMap.NX || j >= SiteMap.NZ) continue;
                int c = SiteMap.Index(i, j);
                if (TakesSeed(c, Chance(25, k))) st.cover[c] = (byte)EcoCover.Seedling;
            }
        }

        /// <summary>Jays cache in open ground and at edges; bushes shelter the seedling (not in a thicket's heart); never in the flood line or the yard.</summary>
        bool TakesSeed(int c, float roll)
        {
            if (land.InBasin(c) || land.yard[c] || c == st.treeCell) return false;
            float p;
            switch (Cv(c))
            {
                case EcoCover.Grass: p = t.seedTakeGrass; break;
                case EcoCover.Tall: p = t.seedTakeTall; break;
                case EcoCover.Bush: p = t.seedTakeBush; break;
                case EcoCover.Bare: p = Damp(c) > 0.3f ? t.seedTakeBare : 0f; break;
                default: p = 0f; break;
            }
            if (Cv(c) != EcoCover.Bush) p *= 1f + t.wateredTake * Watered(c); // #569: a seed takes where the mister's water fell (not on the berry thicket: it stays at its cap)
            if (st.thicketA >= 0) p *= 1f - t.thicketChoke * ThicketZone(c); // #385: a berry thicket chokes the seedlings, so it stays a thicket
            return roll < p;
        }

        void Animals(long month)
        {
            float meadow = st.cGrass + st.cTall + 0.3f * st.cBush;
            // #419 (placed-life spec §5.2): rabbits never arrive by themselves; the Keeper places a pair (FoundRabbits) and the herd
            // grows from there toward the meadow's capacity, as before.
            if (st.rabbits > 0f)
            {
                float k = meadow / t.meadowPerRabbit;
                st.rabbits += k > st.rabbits ? (k - st.rabbits) * t.rabbitGrow : (k - st.rabbits) * t.rabbitFall;
                st.rabbits = MathF.Max(st.rabbits, 2f);
            }
            float kb = st.cBush * t.birdsPerBush + st.cTrees * t.birdsPerTree;
            st.birds += (kb - st.birds) * 0.3f;
        }

        // ---------------------------------------------------------------- counts and gates
        /// <summary>Takes the step's counts from the cover (the end of every step, and at the start).</summary>
        public void Count()
        {
            int grass = 0, tall = 0, bush = 0, seedlings = 0, young = 0, trees = 0, reeds = 0, pond = 0;
            for (int c = 0; c < EcoLand.N; c++)
                switch (Cv(c))
                {
                    case EcoCover.Grass: grass++; break;
                    case EcoCover.Tall: tall++; break;
                    case EcoCover.Bush: bush++; break;
                    case EcoCover.Seedling: seedlings++; break;
                    case EcoCover.Young: young++; break;
                    case EcoCover.Tree: trees++; break;
                    case EcoCover.Reeds: reeds++; break;
                    case EcoCover.Water: pond++; break;
                }
            st.cGrass = grass; st.cTall = tall; st.cBush = bush; st.cSeedlings = seedlings; st.cYoung = young; st.cTrees = trees; st.cReeds = reeds; st.cPond = pond;
        }

        void Gates(long month)
        {
            if (st.cGrass + st.cTall + st.cBush > 0) Reach(EcoMilestones.FirstGreen, month);
            if (st.cBush > 0) Reach(EcoMilestones.FirstBush, month);
            if (st.birds >= t.firstBirdAt) Reach(EcoMilestones.FirstBird, month);
            // #419 (spec §3.1): the camp waits on the rabbits the Keeper placed
            if (st.campGateMonth < 0 && MeadowFrac >= t.campMeadow && IsPlaced(LifeSpecies.Rabbits) && st.rabbits >= t.campRabbits)
            {
                st.campGateMonth = (int)month; Reach(EcoMilestones.CampGate, month); newGates.Add("camp");
            }
            if (st.campGateMonth >= 0 && IsPlaced(LifeSpecies.Thicket)) Reach(EcoMilestones.SeedUnlocked, month); // #418: after the thicket is placed (was: the first wild bushes)
            // #493: the milestone is the first WILD grown tree (the woodcutter's work); the Keeper's own tree never counts.
            bool keeperGrown = st.treeCell >= 0 && st.treeCell < EcoLand.N && Cv(st.treeCell) == EcoCover.Tree;
            if (st.cTrees - (keeperGrown ? 1 : 0) > 0) Reach(EcoMilestones.FirstWoodTree, month);
            if (st.cPond > 0) Reach(EcoMilestones.WaterSurfaced, month);
            // #421 (placed-life spec §3.1): the village also waits on the ducks and the fish the Keeper placed (deer and wolves are not needed)
            if (st.villageGateMonth < 0 && st.campGateMonth >= 0 && st.cPond >= t.pondGateCells && st.cTrees >= t.villageTrees
                && IsPlaced(LifeSpecies.Ducks) && IsPlaced(LifeSpecies.Fish))
            {
                st.villageGateMonth = (int)month; Reach(EcoMilestones.VillageGate, month); newGates.Add("village");
            }
        }

        /// <summary>Debug: opens a gate ("camp" or "village") now.</summary>
        public void ForceGate(string gate, long month)
        {
            if (gate == "camp") { st.campGateMonth = (int)month; Reach(EcoMilestones.CampGate, month); }
            else { st.villageGateMonth = (int)month; Reach(EcoMilestones.VillageGate, month); }
            newGates.Add(gate);
        }

        /// <summary>Debug: open ground near the camp for a tree (the moistest of a ring 0.8-1.0 u out, above the hollow, off the
        /// yard and the slopes), or -1.</summary>
        public int DebugPlantCell()
        {
            float best = float.MinValue; int bc = -1;
            for (int k = 0; k < 24; k++)
            {
                float a = k / 24f * MathF.PI * 2f, r = 0.8f + 0.2f * (k % 2);
                int c = SiteMap.CellAt(land.campX + MathF.Cos(a) * r, land.campZ + MathF.Sin(a) * r);
                if (c < 0 || land.InBasin(c) || land.yard[c] || land.slope[c] > 0.3f) continue;
                float score = st.moisture[c] - 0.1f * Terrain.Dist(SiteMap.CXc(c), SiteMap.CZc(c), 0f, 0f);
                if (score > best) { best = score; bc = c; }
            }
            return bc;
        }

        /// <summary>A stable fingerprint of the whole state (determinism tests).</summary>
        public ulong Fingerprint()
        {
            unchecked
            {
                ulong h = 1469598103934665603UL;
                void Mix(ulong v) { h ^= v; h *= 1099511628211UL; }
                for (int c = 0; c < EcoLand.N; c++) { Mix(st.cover[c]); Mix((ulong)BitConverter.SingleToInt32Bits(st.moisture[c])); Mix((ulong)BitConverter.SingleToInt32Bits(st.soil[c])); if (st.sprout != null) { Mix((ulong)BitConverter.SingleToInt32Bits(st.sprout[c])); Mix((ulong)BitConverter.SingleToInt32Bits(st.puddle[c])); } }
                if (st.watered != null) for (int c = 0; c < EcoLand.N; c++) Mix((ulong)BitConverter.SingleToInt32Bits(st.watered[c]));
                Mix((ulong)BitConverter.SingleToInt32Bits(st.table)); Mix((ulong)BitConverter.SingleToInt32Bits(st.humidity));
                Mix((ulong)BitConverter.SingleToInt32Bits(st.rabbits)); Mix((ulong)BitConverter.SingleToInt32Bits(st.treeProgress));
                Mix((ulong)(st.deerStags * 1000 + st.deerDoes)); Mix((ulong)(st.ducks * 1000 + st.fish)); // #420, #421
                Mix((ulong)st.season); Mix((ulong)(long)st.campGateMonth); Mix((ulong)(long)st.villageGateMonth);
                return h;
            }
        }
    }

    /// <summary>Cover counts as the land is right now (EcoModel.Live).</summary>
    public sealed class EcoLiveCounts
    {
        public int bare, grass, tall, bush, seedlings, young, trees, reeds, pond;
        public void Clear() { bare = grass = tall = bush = seedlings = young = trees = reeds = pond = 0; }
    }
}
