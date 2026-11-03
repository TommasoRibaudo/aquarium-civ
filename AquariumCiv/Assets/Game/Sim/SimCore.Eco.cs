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
            if (s.month % 3 != 0) return;
            int season = (int)((s.month / 3 + 3) % 4);
            EcoM.Step(season, s.month);
            if (Config.settle) Settlement.SyncEco(); // flags and pond into the land, before the month's placing
            EcoTell();
        }

        /// <summary>Raises what the land reached since last time, and writes the gates and the water into the chronicle.</summary>
        void EcoTell()
        {
            var m = EcoM;
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
                else if (key == EcoMilestones.FirstRabbit)
                    Chronicle("eco_rabbit", "A rabbit was seen in the grass. Nobody had seen one before.", 0.6f);
                else if (key == EcoMilestones.FirstBird)
                    Chronicle("eco_bird", "A bird came down onto the bushes and sang. The children copied it all day.", 0.6f);
                EcoMilestone?.Invoke(key);
            }
            foreach (var g in gates)
            {
                if (g == "camp") Chronicle("eco_camp", "Rabbits ran in the new grass. The people said they could stay and raise more tents.", 0.9f);
                else if (g == "village") Chronicle("eco_village", "There was water to stand by and trees to build with. The people began to talk of a village.", 0.9f);
                EcoGateOpened?.Invoke(g);
            }
        }

        // ---------- the Keeper's hands ----------
        /// <summary>Mist a spot for <paramref name="seconds"/> (x, z in box units). The land reacts at once: the ground wets,
        /// the air gets a little humid, and every cell the core of the mist reaches turns to grass now. Returns those cells
        /// (also raised as <see cref="EcoGreened"/>); empty when the ecology is off. Counts as a Keeper action.</summary>
        public IReadOnlyList<int> Mist(float x, float z, float seconds)
        {
            var none = Array.Empty<int>();
            if (!EcologyOn || seconds <= 0f) return none;
            var cfg = Config.eco ?? (Config.eco = new EcoTuning());
            float amount = Math.Min(cfg.mistMaxAmount, seconds / cfg.mistSecondsPerUnit);
            var greened = new List<int>();
            EcoM.Mist(x, z, amount, State.month, greened);
            CountKeeperAction(KeeperAction.Mist, true);
            EcoTell();
            if (greened.Count > 0) EcoGreened?.Invoke(greened);
            return greened;
        }

        /// <summary>The seed is unlocked (the first bushes came after the camp) and no tree stands yet.</summary>
        public bool CanPlantTree => EcologyOn && State.eco.treeCell < 0 && EcoM.SeedAvailable;

        /// <summary>Plant the Keeper's one tree at (x, z). False when the seed isn't unlocked, a tree stands already, or the
        /// spot is in the hollow or the water.</summary>
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
        public EcoCover EcoCoverAt(int cell) => EcologyOn && cell >= 0 && cell < EcoLand.N ? (EcoCover)State.eco.cover[cell] : EcoCover.Bare;
        /// <summary>The game month a milestone was reached (an <see cref="EcoMilestones"/> key), or -1.</summary>
        public long EcoMilestoneMonth(string key) => EcologyOn ? State.eco.MonthOf(key) : -1;

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
