using System;
using System.Collections.Generic;
using AquariumCiv.Sim.Eco;

namespace AquariumCiv.Sim
{
    /// <summary>
    /// The Keeper's hands on the land (keepers-hands.md 2, 2.1, 5; Sim/Eco, #243). With <see cref="SimConfig.ecology"/> off
    /// (the default) none of this runs and the box plays exactly as before. With it on, the box starts as barren ground: the
    /// Keeper's mist greens it at once, a season of weather carries it on, and two gates open as it comes alive. Until
    /// the camp gate the tribe has one tent and a fire; the village needs its pond and its wood too. The land steps once a
    /// season (every 3rd month) and draws no dice: its chances are hashes of (seed, cell, season).
    /// </summary>
    public partial class SimCore
    {
        EcoModel ecoModel;

        /// <summary>The Keeper's mist greened these cells (site-grid indices) at once, for the view to animate.</summary>
        public event Action<IReadOnlyList<int>> EcoGreened;
        /// <summary>A beat of the land's arc was reached: an <see cref="EcoMilestones"/> key.</summary>
        public event Action<string> EcoMilestone;
        /// <summary>A gate opened: "camp" or "village".</summary>
        public event Action<string> EcoGateOpened;

        public bool EcologyOn => State.eco != null && State.eco.on;
        /// <summary>The ecology is on and the camp gate is still closed: the tribe is one tent and a fire (the view's jobs wait on this, #246).</summary>
        public bool BeforeCamp => EcologyOn && State.eco.campGateMonth < 0;
        public bool CampGateOpen => EcologyOn && State.eco.campGateMonth >= 0;
        public bool VillageGateOpen => EcologyOn && State.eco.villageGateMonth >= 0;

        /// <summary>With the ecology on, the village also needs its pond and its wood; off, nothing extra.</summary>
        bool EcoAllowsNextEra => !EcologyOn || State.era != Era.Camp || State.eco.villageGateMonth >= 0;

        EcoModel EcoM
        {
            get
            {
                if (ecoModel != null) return ecoModel;
                Config.eco ??= new EcoTuning();
                return ecoModel = new EcoModel(State.eco, EcoLand.For(TerrainPreset), Config.eco);
            }
        }

        /// <summary>The static land under the ecology (heights, hollows, the flood line, the cells nobody builds on).</summary>
        public EcoLand EcoGround => EcoLand.For(TerrainPreset);

        /// <summary>Starts the ecology on a new box: barren ground, the water table below the lowest ground, no tree, both
        /// gates closed. Called by <see cref="NewWorld"/> when <see cref="SimConfig.ecology"/> is on.</summary>
        public void StartEcology()
        {
            Config.eco ??= new EcoTuning();
            State.eco ??= new EcoState();
            EcoModel.Init(State.eco, EcoLand.For(TerrainPreset), Config.eco, State.seed);
            ecoModel = null;
        }

        // ---------- the season ----------
        /// <summary>Called from <see cref="Step"/> every month; the land moves once a season. Game months 2-4 are spring, 5-7
        /// summer, 8-10 autumn and 11, 0, 1 winter (as the economy's winter), so a step at month % 12 = 3, 6, 9, 0 is the
        /// middle of spring, summer, autumn, winter.</summary>
        void EcoStep()
        {
            var e = State.eco;
            if (e == null || !e.on) return;
            var s = State;
            int season = (int)((s.month / 3 + 3) % 4);
            EcoM.Month(season, s.month); // the surface, every month: the wet area dries, shoots grow (#266, #271)
            if (s.month % 3 == 0)
            {
                EcoM.Step(season, s.month);
                if (Config.settle) Settlement.SyncEco(); // flags and pond into the land, before the month's placing
                EcoTell();
            }
            PullTroddenVine(); // #523: vine on trodden ground is pulled the same step it grows there (SimCore.Trodden.cs)
            WolvesMonth(season); // #437/#438: the pack's month (a chase, the thief, the pen) and the dogs' (hash dice only)
        }

        /// <summary>Raises what the land reached since last time, and writes the gates and the water into the chronicle.</summary>
        void EcoTell()
        {
            var m = EcoM;
            JarsTell(); // #417: the jars that opened
            PlacedLifeTell(); // #420-#422: the white stag, the fletched arrows, the rulings
            if (m.newMilestones.Count == 0 && m.newGates.Count == 0) return;
            var keys = m.newMilestones.ToArray(); var gates = m.newGates.ToArray();
            m.newMilestones.Clear(); m.newGates.Clear();
            foreach (var key in keys)
            {
                if (key == EcoMilestones.WaterSurfaced)
                    Chronicle("eco_water", "Water rose in the hollow and stayed. The people came to look at it.", 0.8f);
                // The signs between the gates (keepers-hands §2.1): small, in the people's words.
                else if (key == EcoMilestones.FirstGreen)
                    Chronicle("eco_green", "Water fell from above onto the dry ground, and green came up where it fell. The people stood in it.", 0.8f);
                else if (key == EcoMilestones.RabbitTracks)
                {
                    PickRabbitTracks();
                    if (LifePlacedMonth(LifeSpecies.Rabbits) != State.month) // #419: the placing has its own history line
                    Chronicle("eco_tracks", "Small prints crossed the edge of the grass, and stopped at the green. The people crouched over them.", 0.5f);
                }
                else if (key == EcoMilestones.FirstRabbit)
                {
                    PickRabbitArrival();
                    if (LifePlacedMonth(LifeSpecies.Rabbits) != State.month) // #419: the placing has its own history line
                    Chronicle("eco_rabbit", "A rabbit was seen in the grass. Nobody had seen one before.", 0.6f);
                }
                else if (key == EcoMilestones.FirstBush)
                    FirstBushTell(); // P8 #288: the chronicle line and the sign
                else if (key == EcoMilestones.FirstBird)
                    Chronicle("eco_bird", "A bird came down onto the bushes and sang. The children copied it all day.", 0.6f);
                EcoMilestone?.Invoke(key);
            }
            foreach (var g in gates)
            {
                if (g == "camp") { Chronicle("eco_camp", "Rabbits ran in the new grass. The people said they could stay and raise more tents.", 0.9f); ArmFirstTent(); }
                else if (g == "village") Chronicle("eco_village", "There was water to stand by and trees to build with. The people began to talk of a village.", 0.9f);
                EcoGateOpened?.Invoke(g);
            }
        }

        // ---------- the Keeper's hands ----------
        /// <summary>Mist a spot for <paramref name="seconds"/> (x, z in box units). The land reacts at once: the ground wets,
        /// the air gets a little humid, and every cell the core of the mist reaches turns to grass now. Returns those cells
        /// (also raised as <see cref="EcoGreened"/>); empty when the ecology is off. Counts as a Keeper action. <paramref name="holdSeconds"/> (#266 view): how long a finger has lingered there, when the mist comes in short pulses; it sets the reach and the puddles while <paramref name="seconds"/> sets the wetting (default: the same as seconds).</summary>
        public IReadOnlyList<int> Mist(float x, float z, float seconds, float holdSeconds = -1f)
        {
            var none = Array.Empty<int>();
            if (!EcologyOn || seconds <= 0f) return none;
            var cfg = Config.eco ?? (Config.eco = new EcoTuning());
            float amount = Math.Min(cfg.mistMaxAmount, seconds / cfg.mistSecondsPerUnit);
            var greened = new List<int>();
            float holdAmount = holdSeconds < 0f ? -1f : Math.Min(cfg.mistMaxAmount, holdSeconds / cfg.mistSecondsPerUnit);
            EcoM.Mist(x, z, amount, State.month, greened, holdAmount);
            CountKeeperAction(KeeperAction.Mist, true);
            EcoTell();
            if (greened.Count > 0) EcoGreened?.Invoke(greened);
            return greened;
        }

        /// <summary>How far a mist held for <paramref name="seconds"/> reaches (box units): about 0.7 for a short one, growing to
        /// 1.0 at the full hold. The view sizes its spray by this.</summary>
        public float EcoMistRadius(float seconds)
        {
            var cfg = Config.eco ?? (Config.eco = new EcoTuning());
            return EcologyOn ? EcoM.MistRadius(Math.Min(cfg.mistMaxAmount, Math.Max(0f, seconds) / cfg.mistSecondsPerUnit)) : 0f;
        }

        /// <summary>The seed is unlocked (the first bushes came after the camp) and no tree stands yet.</summary>
        public bool CanPlantTree => EcologyOn && State.eco.treeCell < 0 && EcoM.SeedAvailable;

        /// <summary>Plant the Keeper's one tree at (x, z). False when the seed isn't unlocked, a tree stands already, or the
        /// spot is in the hollow or the water.</summary>
        /// <summary>#378: the cell the Keeper's tree would really take for a press at (x, z): the cell itself, or the nearest cell
        /// clear of the glass and the hollow; -1 when there is none.</summary>
        public int TreeCellFor(float x, float z) => EcologyOn ? EcoM.TreeCellFor(x, z) : -1;

        public bool PlantTree(float x, float z)
        {
            if (!EcologyOn) return false;
            if (!EcoM.Plant(x, z, State.month)) return false;
            CountKeeperAction(KeeperAction.Plant, false);
            EcoTell();
            return true;
        }

        void EcoWipe(float amount) { if (EcologyOn) EcoM.Wipe(amount); }
        void EcoSnip(float amount) { if (EcologyOn) EcoM.Snip(amount); }

        // ---------- the tweezers: what overgrows (#311, #314; EcoModel.Overgrowth.cs) ----------
        /// <summary>Each cell of vine counts this much as overgrowth in <see cref="MessLevel"/> (at most <see cref="VineLoadMax"/> together).</summary>
        public const float VineLoadPerCell = 0.02f, VineLoadMax = 0.5f;
        /// <summary>The creeping vine's share of the overgrowth (0 with no vine).</summary>
        public float VineLoad => EcologyOn && State.eco.vine != null ? Math.Min(VineLoadMax, State.eco.vine.Count * VineLoadPerCell) : 0f;
        /// <summary>The cells the vine covers (empty with the ecology off).</summary>
        public IReadOnlyList<int> EcoVine => EcologyOn && State.eco.vine != null ? State.eco.vine : (IReadOnlyList<int>)Array.Empty<int>();
        /// <summary>Tufts of weed round the Keeper's tree, 0..3.</summary>
        public int EcoTreeWeedPieces => EcologyOn ? EcoM.TreeWeedPieces : 0;

        /// <summary>Plucks one tuft of the weeds round the Keeper's tree. The people nearby react as to a prune (the view asks them).</summary>
        public bool EcoPluckTreeWeed()
        {
            if (!EcologyOn || !EcoM.PluckTreeWeed()) return false;
            CountKeeperAction(KeeperAction.Prune, true);
            return true;
        }

        /// <summary>Plucks the wild sapling nearest a point (within <paramref name="radius"/> box units); never the Keeper's tree, a grown tree or the heart of a wood. The cell, or -1.</summary>
        public int EcoPluckSapling(float x, float z, float radius)
        {
            if (!EcologyOn) return -1;
            int c = EcoM.NearestSapling(x, z, radius);
            if (c < 0 || !EcoM.PluckSapling(c)) return -1;
            CountKeeperAction(KeeperAction.Prune, true);
            return c;
        }

        /// <summary>The wild sapling the tweezers could take nearest a point (within <paramref name="radius"/>), without plucking it; -1 if none.</summary>
        public int EcoNearestSapling(float x, float z, float radius) => EcologyOn ? EcoM.NearestSapling(x, z, radius) : -1;

        /// <summary>A wild sapling the tweezers could take stands on this cell.</summary>
        public bool EcoCanPluckSapling(int cell) => EcologyOn && EcoM.CanPluckSapling(cell);

        /// <summary>Plucks the vine on a cell.</summary>
        public bool EcoPluckVine(int cell)
        {
            if (!EcologyOn || !EcoM.PluckVine(cell)) return false;
            CountKeeperAction(KeeperAction.Prune, true);
            return true;
        }

        /// <summary>Debug: thick weeds round the tree. False when no tree stands.</summary>
        public bool DebugGrowTreeWeeds() => EcologyOn && EcoM.GrowTreeWeeds();
        /// <summary>Debug: seedlings creep in near the trees and the yard. How many came.</summary>
        public int DebugCreepForestEdge(int count = 5) => EcologyOn ? EcoM.CreepEdge(count) : 0;
        /// <summary>Debug: a strand of vine from a point. Cells added.</summary>
        public int DebugGrowVine(float x, float z, int len = 6) => EcologyOn ? EcoM.GrowVineAt(x, z, len) : 0;

        // ---------- what the view and the sound read ----------
        public EcoCell EcoAt(float x, float z)
        {
            var r = new EcoCell { cell = -1 };
            if (!EcologyOn) return r;
            int c = Settle.SiteMap.CellAt(x, z);
            if (c < 0) return r;
            var land = EcoM.land;
            r.cell = c; r.cover = (EcoCover)State.eco.cover[c]; r.moisture = State.eco.moisture[c]; r.soil = State.eco.soil[c];
            r.height = land.height[c]; r.belowFloodLine = land.noBuild[c];
            return r;
        }

        /// <summary>The water table, in terrain height units (0 when the ecology is off).</summary>
        public float EcoTable => EcologyOn ? State.eco.table : 0f;
        /// <summary>The water table scaled for sound: 0 at its start (below the lowest ground) to 1 at the flood line (the main hollow's spill); 0 when off.</summary>
        public float EcoTable01
        {
            get
            {
                if (!EcologyOn) return 0f;
                float start = EcoM.land.minHeight - (Config.eco ?? new EcoTuning()).tableStartBelow, top = EcoM.land.FloodLine;
                return top > start ? Math.Max(0f, Math.Min(1f, (State.eco.table - start) / (top - start))) : 0f;
            }
        }
        /// <summary>The pond's level (the water table's peak, capped at the spill); -1000 until water surfaces.</summary>
        public float EcoPondLevel => EcologyOn ? State.eco.pondLevel : -1000f;
        /// <summary>The flood line: the main hollow's spill level (the people never build below it).</summary>
        public float EcoFloodLine => EcologyOn ? EcoM.land.FloodLine : -1000f;
        /// <summary>The box's air, 0..1; above/below <see cref="EcoTuning.tooHumid"/> / <see cref="EcoTuning.tooDryHumid"/> it is too humid / too dry.</summary>
        public float EcoHumidity => EcologyOn ? State.eco.humidity : 0f;
        /// <summary>Droplets on the glass, 0..1.</summary>
        public float EcoDroplets => EcologyOn ? State.eco.droplets : 0f;
        public float EcoGrime => EcologyOn ? State.eco.grime : 0f;
        /// <summary>Light reaching the plants, 0..1 (the glass's grime and droplets dim it).</summary>
        public float EcoLight => EcologyOn ? EcoM.Light : 1f;
        public bool EcoTooHumid => EcologyOn && EcoM.TooHumid;
        public bool EcoTooDry => EcologyOn && EcoM.TooDry;
        public float EcoRabbits => EcologyOn ? State.eco.rabbits : 0f;
        public float EcoBirds => EcologyOn ? State.eco.birds : 0f;
        /// <summary>Share of the land that is bare ground right now, 0..1 (the sound's barren bed uses this); 0 when the ecology is off.</summary>
        public float EcoBareShare => EcologyOn ? EcoM.Live().bare / (float)EcoLand.N : 0f;
        /// <summary>Share of the land in grass or tall grass right now.</summary>
        public float EcoMeadowShare { get { if (!EcologyOn) return 0f; var l = EcoM.Live(); return (l.grass + l.tall) / (float)EcoLand.N; } }
        /// <summary>Cells of open water, and of grown trees, right now.</summary>
        public int EcoPondCells => EcologyOn ? EcoM.Live().pond : 0;
        public int EcoGrownTrees => EcologyOn ? EcoM.Live().trees : 0;
        /// <summary>A stable hash of the whole ecology (determinism and catch-up tests); 0 when it is off.</summary>
        public ulong EcoFingerprint => EcologyOn ? EcoM.Fingerprint() : 0UL;
        /// <summary>#573: the cell woodcutters chop at: the grown wild tree nearest (<paramref name="nearX"/>, <paramref name="nearZ"/>) that is inside the glass
        /// margin and at least <c>woodsClearRadius</c> from the Keeper's tree (a sapling, a young tree and the Keeper's own tree never count), or -1.</summary>
        public int WoodsTreeCell(float nearX, float nearZ, int rank = 0)
        {
            if (!EcologyOn) return -1;
            var e = State.eco; var cfg = Config.eco ?? (Config.eco = new EcoTuning());
            const float mx = Settle.Terrain.HX - Settle.Terrain.GlassMargin, mz = Settle.Terrain.HZ - Settle.Terrain.GlassMargin;
            bool kt = e.treeCell >= 0 && e.treeCell < EcoLand.N;
            float kx = kt ? Settle.SiteMap.CXc(e.treeCell) : 0f, kz = kt ? Settle.SiteMap.CZc(e.treeCell) : 0f;
            var found = woodsScratch; found.Clear();
            for (int c = 0; c < EcoLand.N; c++)
            {
                if ((EcoCover)e.cover[c] != EcoCover.Tree || c == e.treeCell) continue;
                float x = Settle.SiteMap.CXc(c), z = Settle.SiteMap.CZc(c);
                if (Math.Abs(x) > mx || Math.Abs(z) > mz) continue;
                if (kt && Settle.Terrain.Dist(x, z, kx, kz) < cfg.woodsClearRadius) continue;
                float d = (x - nearX) * (x - nearX) + (z - nearZ) * (z - nearZ);
                found.Add((d, c));
            }
            if (found.Count == 0 || rank >= found.Count) return -1;
            found.Sort((a, b) => a.d != b.d ? a.d.CompareTo(b.d) : a.c.CompareTo(b.c));
            return found[Math.Max(0, rank)].c;
        }
        readonly List<(float d, int c)> woodsScratch = new();
        /// <summary>#573: how many grown wild trees away from the Keeper's tree there are (the woods: at most two choppers to a tree).</summary>
        public int WoodsTreeCount { get { if (!EcologyOn) return 0; WoodsTreeCell(0f, 0f, 1 << 20); return woodsScratch.Count; } }
        /// <summary>#573: a woods spot exists (off the ecology there is always a forest).</summary>
        public bool WoodsExist => !EcologyOn || WoodsTreeCell(0f, 0f) >= 0;

        public EcoCover EcoCoverAt(int cell) => EcologyOn && cell >= 0 && cell < EcoLand.N ? (EcoCover)State.eco.cover[cell] : EcoCover.Bare;
        /// <summary>The game month a milestone was reached (an <see cref="EcoMilestones"/> key), or -1.</summary>
        public long EcoMilestoneMonth(string key) => EcologyOn ? State.eco.MonthOf(key) : -1;

        // ---- the wet area, shoots and puddles (#266 sim half; the view draws them) ----
        /// <summary>How wet the ground looks at a site cell, 0..1 (the mist's wet area; dries over a few game months). 0 when off.</summary>
        public float EcoWetAt(int cell) => EcologyOn && State.eco.wet != null && cell >= 0 && cell < EcoLand.N ? State.eco.wet[cell] : 0f;
        /// <summary>The whole wet layer (site cells, 0..1), or null when the ecology is off. Read only.</summary>
        public IReadOnlyList<float> EcoWet => EcologyOn ? State.eco.wet : null;
        /// <summary>Shoots on bare ground at a cell, 0..1 (1 turns to grass); 0 where the ground is covered.</summary>
        public float EcoSproutAt(int cell) => EcologyOn && State.eco.sprout != null && cell >= 0 && cell < EcoLand.N ? State.eco.sprout[cell] : 0f;
        /// <summary>Water standing at a cell, 0..1.</summary>
        public float EcoPuddleAt(int cell) => EcologyOn && State.eco.puddle != null && cell >= 0 && cell < EcoLand.N ? State.eco.puddle[cell] : 0f;
        /// <summary>Fills <paramref name="into"/> with the cells holding a puddle of at least <paramref name="minStrength"/>, strongest first.</summary>
        public void EcoPuddleCells(List<int> into, float minStrength = 0.05f) { if (EcologyOn) EcoM.PuddleCells(into, minStrength); else into.Clear(); }
        public bool EcoHasPuddles { get { if (!EcologyOn || State.eco.puddle == null) return false; foreach (var p in State.eco.puddle) if (p >= Config.eco.puddleDrinkAt) return true; return false; } }
        /// <summary>The nearest puddle worth drinking from (a water source while there is no pond; #267 uses it), or -1.</summary>
        public int EcoNearestPuddle(float x, float z, float reach = 99f) => EcologyOn ? EcoM.NearestPuddle(x, z, reach) : -1;
        /// <summary>Someone drinks from a puddle: it shrinks. Returns what was drawn.</summary>
        public float EcoDrawPuddle(int cell, float amount) => EcologyOn ? EcoM.DrawPuddle(cell, amount) : 0f;

        /// <summary>The Keeper's tree and what it needs.</summary>
        public EcoTreeInfo EcoTree
        {
            get
            {
                var r = new EcoTreeInfo { cell = -1 };
                if (!EcologyOn || State.eco.treeCell < 0) return r;
                var e = State.eco; var cfg = Config.eco;
                r.planted = true; r.cell = e.treeCell; r.stage = e.treeStage; r.grown = e.treeStage >= 4;
                r.x = Settle.SiteMap.CXc(e.treeCell); r.z = Settle.SiteMap.CZc(e.treeCell);
                r.progress = e.treeProgress; r.wilting = e.treeWilting; r.keeperMistings = e.keeperMistings;
                r.water = Settle.Terrain.Smooth(cfg.treeWetAt, cfg.treeWetFull, e.moisture[e.treeCell]);
                r.light = EcoM.Light; r.weeds = e.treeWeeds;
                float lo = e.treeStage == 0 ? 0f : cfg.treeStages[e.treeStage - 1];
                r.stageProgress = r.grown ? 1f : Settle.Terrain.Clamp01((e.treeProgress - lo) / (cfg.treeStages[e.treeStage] - lo));
                return r;
            }
        }

        // ---------- debug (for the debug panel; CLAUDE.md "debug panel stays current") ----------
        /// <summary>Debug ("mist big here"): a full-length mist at (x, z), through the same path as a held finger.</summary>
        public IReadOnlyList<int> DebugMistBig(float x, float z)
        {
            var cfg = Config.eco ?? (Config.eco = new EcoTuning());
            return Mist(x, z, cfg.mistMaxAmount * cfg.mistSecondsPerUnit);
        }

        /// <summary>Debug ("puddles now"): soak the ground round (x, z) so wet ground and puddles show. Returns the puddle cells made.</summary>
        public int DebugPuddlesNow(float x, float z, float radius = 0.9f) => EcologyOn ? EcoM.DebugPuddles(x, z, radius) : 0;

        /// <summary>Debug: open the camp gate now, whatever the meadow says.</summary>
        public bool DebugOpenCampGate()
        {
            if (!EcologyOn || State.eco.campGateMonth >= 0) return false;
            EcoM.ForceGate("camp", State.month);
            EcoTell();
            return true;
        }

        /// <summary>Debug: open the village gate now (and the camp gate, if still shut).</summary>
        public bool DebugOpenVillageGate()
        {
            if (!EcologyOn || State.eco.villageGateMonth >= 0) return false;
            DebugOpenCampGate();
            EcoM.ForceGate("village", State.month);
            EcoTell();
            return true;
        }

        /// <summary>Debug: live <paramref name="n"/> seasons now (3 months each), through the same path as play and catch-up.</summary>
        public int DebugSkipSeasons(int n)
        {
            if (n <= 0) return 0;
            return Advance(n * 3 * Config.StepHours);
        }

        /// <summary>Debug: set the water table (it rises to the new level; the pond follows at the next season).</summary>
        public void DebugSetTable(float h)
        {
            if (!EcologyOn) return;
            var e = State.eco;
            float cap = EcoM.land.FloodLine - 0.005f;
            e.table = Math.Min(h, cap);
            e.tablePeak = Math.Max(e.tablePeak, e.table);
        }

        /// <summary>Debug: set the box's humidity (0..1) and let the glass follow at the next season.</summary>
        public void DebugSetHumidity(float v)
        {
            if (EcologyOn) State.eco.humidity = Math.Clamp(v, 0f, 1f);
        }

        /// <summary>#573 debug: a grown wild tree and a young one (sapling) right beside the Keeper's tree (<paramref name="beside"/>), or one grown wild tree
        /// well away from it (the woods spot). Plants the Keeper's grown tree first if there is none. Returns a one-line answer.</summary>
        public string DebugWildTrees(bool beside, int count = 1)
        {
            if (!EcologyOn) return "living land off";
            var e = State.eco; var cfg = Config.eco ?? (Config.eco = new EcoTuning());
            if (e.treeCell < 0 && !DebugTreeStage(4)) return "no Keeper's tree can be planted";
            var g = EcoGround;
            float kx = Settle.SiteMap.CXc(e.treeCell), kz = Settle.SiteMap.CZc(e.treeCell);
            const float mx = Settle.Terrain.HX - Settle.Terrain.GlassMargin - 0.1f, mz = Settle.Terrain.HZ - Settle.Terrain.GlassMargin - 0.1f;
            int Pick(float rMin, float rMax, int skip, float apart = 0f)
            {
                int best = -1; float bd = float.MaxValue;
                for (int c = 0; c < EcoLand.N; c++)
                {
                    float x = Settle.SiteMap.CXc(c), z = Settle.SiteMap.CZc(c);
                    var cv = (EcoCover)e.cover[c];
                    if (c == e.treeCell || c == skip || (cv != EcoCover.Bare && cv != EcoCover.Grass && cv != EcoCover.Tall)) continue;
                    if (g.InBasin(c) || g.yard[c] || Math.Abs(x) > mx || Math.Abs(z) > mz) continue; // (even cells only: the view draws a grown crown per 2 x 2 block, so a tree on an odd cell would stand in the sim and not on screen)
                    if ((Settle.SiteMap.I(c) & 1) != 0 || (Settle.SiteMap.J(c) & 1) != 0) continue;
                    float dk = Settle.Terrain.Dist(x, z, kx, kz);
                    if (dk < rMin || dk > rMax) continue;
                    if (apart > 0f)
                    {
                        bool crowded = false;
                        for (int o = 0; o < EcoLand.N && !crowded; o++)
                            if (o != c && (EcoCover)e.cover[o] == EcoCover.Tree && o != e.treeCell && Settle.Terrain.Dist(x, z, Settle.SiteMap.CXc(o), Settle.SiteMap.CZc(o)) < apart) crowded = true;
                        if (crowded) continue;
                    }
                    float dc = Settle.Terrain.Dist(x, z, g.campX, g.campZ);
                    if (dc < bd) { bd = dc; best = c; }
                }
                return best;
            }
            if (beside)
            {
                int a = Pick(0.25f, 0.5f, -1), b = Pick(0.25f, 0.5f, a);
                if (a < 0 || b < 0) return "no room beside the Keeper's tree";
                e.cover[a] = (byte)EcoCover.Tree; e.cover[b] = (byte)EcoCover.Young;
                EcoM.Reach(EcoMilestones.FirstWoodTree, State.month);
                EcoTell();
                return $"a grown wild tree (cell {a}) and a sapling (cell {b}) beside the Keeper's tree";
            }
            // count > 1: trees at least 0.8 apart (a real wood is spread out; two choppers to a tree)
            int far = -1;
            for (int k = 0; k < Math.Max(1, count); k++)
            {
                far = Pick(cfg.woodsClearRadius + 0.3f, 99f, -1, 0.8f);
                if (far < 0) { if (k == 0) return "no room away from the Keeper's tree"; break; }
                e.cover[far] = (byte)EcoCover.Tree;
            }
            EcoM.Reach(EcoMilestones.FirstWoodTree, State.month);
            EcoTell();
            return $"a grown wild tree (cell {far}) away from the Keeper's tree";
        }

        /// <summary>Debug: set the Keeper's tree to stage 0 (seed) .. 4 (grown), planting it near the camp if there is none.</summary>
        public bool DebugTreeStage(int stage)
        {
            if (!EcologyOn) return false;
            var e = State.eco; var cfg = Config.eco;
            stage = Math.Clamp(stage, 0, 4);
            if (e.treeCell < 0)
            {
                int c = EcoM.DebugPlantCell();
                if (c < 0) return false;
                e.treeCell = c;
                EcoM.Reach(EcoMilestones.TreePlanted, State.month);
            }
            e.treeStage = stage;
            e.treeProgress = stage == 0 ? 0f : cfg.treeStages[stage - 1];
            e.cover[e.treeCell] = (byte)(stage >= 4 ? EcoCover.Tree : stage >= 2 ? EcoCover.Young : EcoCover.Seedling);
            for (int k = 1; k <= stage; k++) EcoM.Reach(EcoMilestones.TreeStages[k - 1], State.month);
            EcoTell();
            return true;
        }
    }
}
