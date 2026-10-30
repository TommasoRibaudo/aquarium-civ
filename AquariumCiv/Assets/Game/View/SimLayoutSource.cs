using System.Collections.Generic;
using System.Globalization;
using AquariumCiv.Game;
using AquariumCiv.GameUI;
using AquariumCiv.Sim;
using AquariumCiv.Sim.Settle;
using UnityEngine;

namespace AquariumCiv.View
{
    /// <summary>
    /// Feeds the <see cref="BoxView"/> from the live sim (#33): in play mode the <see cref="GameClock"/>'s SimCore, in
    /// edit mode a preview sim grown from <see cref="previewSeed"/> for <see cref="previewYears"/>. Whenever the sim's
    /// month changes the box refreshes, and only the chunks the month touched rebuild (BoxView logs each rebuild with its
    /// time). When the era moves on, the box rebuilds in place as a wave from the centre outward
    /// (<see cref="Settlement.RebuildOrder"/>), one lot per <see cref="waveStepSeconds"/> (era-moment.md: lots never
    /// move; the moment's UI comes later). Debug hooks for `script eval` and <see cref="SliceDebugPanel"/>:
    /// <see cref="DebugSkipYears"/>, <see cref="DebugNextEra"/>, <see cref="DebugReplayWave"/>, <see cref="DebugBuild"/>,
    /// <see cref="DebugPreview"/>, <see cref="DebugWaveNow"/>.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(BoxView))]
    [AddComponentMenu("AquariumCiv/Sim Layout Source")]
    public class SimLayoutSource : MonoBehaviour, IBoxLayoutSource
    {
        [Header("Edit-mode preview (play mode shows the GameClock's live sim)")]
        public int previewSeed = 20261029;
        public float previewYears = 10f;
        [Tooltip("When the preview or a debug skip reaches an era's threshold, cross it (no era moment UI yet).")]
        public bool autoAdvanceEras = true;
        [Header("Era rebuild wave")]
        public bool waveOnEraChange = true;
        public float waveStepSeconds = 0.3f;
        [System.NonSerialized] public string lastWaveInfo = "";

        SimCore preview;
        BoxView box;
        long seenMonth = -1;
        Era seenEra;
        bool seenAny;
        readonly List<int> waveOrder = new();
        Era waveFrom;
        int waveStep = -1;
        double waveNext, waveMaxMs, waveTotalMs;

        public static SimLayoutSource Find() => FindAnyObjectByType<SimLayoutSource>();

        /// <summary>The sim on show: the GameClock's in play mode, else the preview.</summary>
        public SimCore Sim
        {
            get
            {
                if (Application.isPlaying && GameClock.Instance != null && GameClock.Instance.Sim != null) return GameClock.Instance.Sim;
                return preview ??= MakePreview();
            }
        }

        /// <summary>A past year (or the blend of two) the box shows instead of the live sim (#78: <see cref="TimeLapseView"/>
        /// sets it and refreshes). Null = the live box.</summary>
        [System.NonSerialized] public ReplayFrame Replay;

        public bool IsLive => Application.isPlaying && GameClock.Instance != null && GameClock.Instance.Sim != null;
        public bool WaveRunning => waveStep >= 0;
        BoxView Box => box != null ? box : box = GetComponent<BoxView>();
        static double Now => Time.realtimeSinceStartupAsDouble;

        SimCore MakePreview()
        {
            var clock = FindAnyObjectByType<GameClock>();
            var cfg = clock != null ? clock.config : new SimConfig();
            var core = new SimCore(cfg, SimCore.NewWorld(previewSeed, cfg));
            Skip(core, previewYears, autoAdvanceEras);
            return core;
        }

        /// <summary>Lives <paramref name="years"/> a year at a time, crossing each era as soon as it is ready if asked
        /// (the era moment's UI is not in yet). Returns the months stepped.</summary>
        public static int Skip(SimCore sim, float years, bool advanceEras)
        {
            int months = 0;
            double yearHours = sim.Config.StepHours * SimConfig.MonthsPerYear;
            for (float left = years; left > 1e-4f; left -= 1f)
            {
                months += sim.Advance(yearHours * Mathf.Min(1f, left));
                if (advanceEras && sim.State.eraReady) sim.AdvanceEra();
            }
            return months;
        }

        /// <summary>The box on screen is another world now (a museum visit, box 2): rebuild it whole, with no era wave.</summary>
        public void WorldChanged()
        {
            waveStep = -1; Replay = null;
            MomentLook = false;
            seenAny = false;
            FillLayout(Box.layout); // Rebuild() builds from the layout as it is: refill it from the new world first
            Box.Rebuild();
            seenMonth = Sim.State.month; seenEra = Sim.State.era; seenAny = true;
        }

        public void FillLayout(BoxLayout layout)
        {
            var sim = Sim;
            Box.land = SimLayout.LandFor(sim.TerrainPreset);
            if (Replay != null)
            {
                SimLayout.Fill(layout, sim, Replay.EraOf, Cobbled(sim), Replay);
                return; // a replay does not move what the live view has seen
            }
            SimLayout.Fill(layout, sim, MomentLook ? MomentEra : WaveRunning ? WaveEra : null, Cobbled(sim));
            seenMonth = sim.State.month; seenEra = sim.State.era; seenAny = true;
        }

        // ---------------- the era moment's look (#89) ----------------
        // While an era moment plays, the box shows each lot in the next era's look as the moment's wave reaches it, before
        // the sim moves the era at the cheer (the director drives it: MomentGreen, MomentLot). After the cheer the sim's
        // era is the new one and nothing rebuilds again (no second wave).
        Era momentFrom, momentTo;
        bool momentGreen;
        readonly HashSet<int> momentDone = new();
        public bool MomentLook { get; private set; }
        public int MomentLotsDone => momentDone.Count;

        public void BeginMomentLook(Era from, Era to)
        {
            if (WaveRunning) waveStep = -1;
            MomentLook = true; momentFrom = from; momentTo = to; momentGreen = false;
            CobbleReach = 0f;
            momentDone.Clear();
            // A replay on an older box: what was built after the era began never had the older look.
            var st = Sim.Settlement;
            if (st != null && Sim.State.era >= to)
                foreach (var l in st.Lots) if (l.foundedMonth > Sim.State.eraStartMonth) momentDone.Add(l.id);
            Box.Refresh();
        }

        public void MomentGreen() { if (!MomentLook || momentGreen) return; momentGreen = true; Box.Refresh(); }

        public void MomentLot(int lotId) { if (MomentLook && momentDone.Add(lotId)) Box.Refresh(); }

        /// <summary>Every lot and the green in the new look (the cheer: nothing is left behind).</summary>
        public void MomentAll()
        {
            if (!MomentLook) return;
            momentGreen = true;
            CobbleReach = float.MaxValue;
            var st = Sim.Settlement;
            if (st != null) foreach (var l in st.Lots) momentDone.Add(l.id);
            Box.Refresh();
        }

        public void EndMomentLook()
        {
            if (!MomentLook) return;
            MomentLook = false;
            momentDone.Clear();
            CobbleReach = -1f;
            Box.Refresh();
        }

        Era MomentEra(Lot l)
        {
            if (l == null) return momentGreen ? momentTo : momentFrom;
            return momentDone.Contains(l.id) ? momentTo : momentFrom;
        }

        /// <summary>The era a lot shows now (tests: no lot left in the old look).</summary>
        public Era ShownEra(Lot l) => MomentLook ? MomentEra(l) : WaveRunning ? WaveEra(l) : Sim.State.era;

        // ---------------- the era wave ----------------
        /// <summary>Step s of the wave: the green turns at step 1, lot i (in rebuild order) at step i + 2.</summary>
        Era WaveEra(Lot l)
        {
            var now = Sim.State.era;
            if (l == null) return waveStep >= 1 ? now : waveFrom;
            int i = waveOrder.IndexOf(l.id);
            return i < 0 || i < waveStep - 1 ? now : waveFrom;
        }

        public void StartWave(Era from)
        {
            var st = Sim.Settlement;
            if (st == null) return;
            waveOrder.Clear();
            // Only what stood when the era began rebuilds (a replay on an older box leaves later buildings alone).
            long start = Sim.State.eraStartMonth;
            foreach (var l in st.RebuildOrder()) if (l.foundedMonth <= start) waveOrder.Add(l.id);
            waveFrom = from;
            waveStep = 0;
            waveMaxMs = waveTotalMs = 0;
            waveNext = Now + waveStepSeconds;
            Box.Refresh(); // everything still in the old era
#if UNITY_EDITOR
            if (!Application.isPlaying) { UnityEditor.EditorApplication.update -= EditorTick; UnityEditor.EditorApplication.update += EditorTick; }
#endif
        }

        /// <summary>One lot (or the green) rebuilds. Returns false when the wave is over.</summary>
        bool StepWave()
        {
            waveStep++;
            bool more = waveStep <= waveOrder.Count + 1;
            if (!more) waveStep = -1;
            Box.Refresh();
            waveMaxMs = System.Math.Max(waveMaxMs, Box.lastRefreshMs);
            waveTotalMs += Box.lastRefreshMs;
            if (!more) lastWaveInfo = string.Format(CultureInfo.InvariantCulture, "wave {0} → {1}: {2} lots, refresh max {3:0.0} ms, total {4:0} ms",
                waveFrom, Sim.State.era, waveOrder.Count, waveMaxMs, waveTotalMs);
            return more;
        }

#if UNITY_EDITOR
        void EditorTick()
        {
            if (this == null || !WaveRunning) { UnityEditor.EditorApplication.update -= EditorTick; return; }
            if (Now < waveNext) return;
            waveNext = Now + waveStepSeconds;
            StepWave();
            UnityEditor.SceneView.RepaintAll();
            UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
        }

        void OnDisable() => UnityEditor.EditorApplication.update -= EditorTick;
#endif

        const string MusicHold = "era-wave";

        void OnDestroy() => MusicDirector.Release(MusicHold);

        void Update()
        {
            // The era's rebuild wave is a moment: the music ducks out and no track starts until it is done.
            if (WaveRunning) MusicDirector.Hold(MusicHold); else MusicDirector.Release(MusicHold);
            var sim = Sim;
            if (sim == null || Box == null || !seenAny || Replay != null) return;
            if (WaveRunning)
            {
                if (Application.isPlaying && Now >= waveNext) { waveNext = Now + waveStepSeconds; StepWave(); }
                return;
            }
            if (sim.State.era > seenEra && waveOnEraChange && !MomentLook) { StartWave(seenEra); return; }
            if (sim.State.month != seenMonth || sim.State.era != seenEra) Box.Refresh();
        }

        /// <summary>Which site cells are laid with cobbles beyond the roads (#90 sets this when cobbles spread along
        /// the main path). Null = only roads.</summary>
        [System.NonSerialized] public System.Func<int, bool> cobbled;

        /// <summary>Debug: cobble every path cell (path tier and up), to preview #90's look.</summary>
        public static bool CobbleAllPaths;

        /// <summary>The town's main paths are cobbled (era-moment.md §5, #90): every path cell (path tier and up) once the
        /// box shows the town; during the village → town moment the cobbles unroll from the square out to
        /// <see cref="CobbleReach"/> (world units from the green's centre). Roads are cobbled anyway.</summary>
        public float CobbleReach { get; private set; } = -1f;

        public void SetCobbleReach(float reach)
        {
            if (Mathf.Abs(reach - CobbleReach) < 1e-4f) return;
            CobbleReach = reach;
            if (MomentLook) Box.Refresh();
        }

        System.Func<int, bool> Cobbled(SimCore sim)
        {
            var st = sim.Settlement;
            var tier = Replay != null ? Replay.tiers : st?.Map.tier;
            if (tier == null) return cobbled;
            bool town = Replay != null ? Replay.era == Era.Town : MomentLook ? momentTo == Era.Town && momentGreen : sim.State.era == Era.Town;
            if (!CobbleAllPaths && !town) return cobbled;
            float reach = MomentLook && !CobbleAllPaths ? CobbleReach : float.MaxValue;
            float gx = st.Map.greenX, gz = st.Map.greenZ;
            return c => (tier[c] >= AquariumCiv.Sim.Settle.PathNet.PathTier
                         && AquariumCiv.Sim.Settle.Terrain.Dist(SiteMap.CXc(c), SiteMap.CZc(c), gx, gz) <= reach)
                        || (cobbled != null && cobbled(c));
        }

        /// <summary>Whether a site cell shows cobbles now (tests).</summary>
        public bool CellCobbled(int cell) => Cobbled(Sim)?.Invoke(cell) ?? false;

        // ---------------- debug ----------------
        public string Status()
        {
            var s = Sim.State;
            int lots = Sim.Settlement?.Lots.Count ?? 0;
            return string.Format(CultureInfo.InvariantCulture, "{0} · year {1:0.0} · {2} · pop {3} · {4} lots · {5}",
                IsLive ? "live" : "preview", s.Year, s.era, s.population, lots, Box != null ? Box.lastRefreshInfo : "");
        }

        static SimLayoutSource Need(out string error)
        {
            var s = Find();
            error = s == null ? "no SimLayoutSource in the scene" : null;
            return s;
        }

        /// <summary>Debug: the sim lives <paramref name="years"/> (eras crossed when ready, no wave), the box refreshes.</summary>
        public static string DebugSkipYears(float years)
        {
            var s = Need(out var err); if (s == null) return err;
            Skip(s.Sim, years, s.autoAdvanceEras);
            if (s.IsLive) GameClock.Instance.Save();
            s.Box.Refresh();
            return s.Status();
        }

        /// <summary>Debug: cobbles on every path (on) or only on the roads (off); the box refreshes.</summary>
        public static string DebugCobblePaths(bool on)
        {
            var s = Need(out var err); if (s == null) return err;
            CobbleAllPaths = on;
            return (on ? "cobbles on every path · " : "cobbles on roads only · ") + s.Box.Refresh();
        }

        /// <summary>Debug: cross into the next era now and play the rebuild wave (or rebuild at once).</summary>
        public static string DebugNextEra(bool wave = true)
        {
            var s = Need(out var err); if (s == null) return err;
            var from = s.Sim.State.era;
            if (!s.Sim.ForceNextEra()) return "already in the last era · " + s.Status();
            if (s.IsLive) GameClock.Instance.Save();
            if (wave) s.StartWave(from); else s.Box.Refresh();
            return s.Status();
        }

        /// <summary>Debug: play the last era's rebuild wave again (from the previous era's looks).</summary>
        public static string DebugReplayWave()
        {
            var s = Need(out var err); if (s == null) return err;
            if (s.Sim.State.era == Era.Camp) return "camp: no wave to replay";
            s.StartWave(s.Sim.State.era - 1);
            return "wave started · " + s.Status();
        }

        /// <summary>Debug: run the whole wave now, synchronously (edit mode measurements and stills); returns its timing.
        /// <paramref name="stopAfter"/> &gt; 0 stops mid-wave after that many steps (for a still of the wave).</summary>
        public static string DebugWaveNow(int stopAfter = 0)
        {
            var s = Need(out var err); if (s == null) return err;
            if (s.Sim.State.era == Era.Camp) return "camp: no wave";
            s.StartWave(s.Sim.State.era - 1);
            int steps = 0;
            while (s.StepWave()) if (stopAfter > 0 && ++steps >= stopAfter) return $"stopped after {steps} steps · {s.Box.lastRefreshInfo}";
            return s.lastWaveInfo;
        }

        /// <summary>Debug: the economy builds one building of this type now (BuildingType: Hut, Farm, Well…); the box
        /// refreshes and the timing of that rebuild comes back.</summary>
        public static string DebugBuild(string type)
        {
            var s = Need(out var err); if (s == null) return err;
            var b = s.Sim.DebugBuild(type);
            string info = s.Box.Refresh();
            return $"{type} → lot {b.lotId} at ({b.x:0.00}, {b.z:0.00}) · {info}";
        }

        /// <summary>Debug: the newest building of this type is gone (its lot is cleared); the box refreshes.</summary>
        public static string DebugRemoveBuilding(string type)
        {
            var s = Need(out var err); if (s == null) return err;
            if (!s.Sim.DebugRemoveBuilding(type)) return "no " + type + " to remove";
            return $"{type} removed · " + s.Box.Refresh();
        }

        /// <summary>Debug (edit mode): grow a new preview box from <paramref name="seed"/> for <paramref name="years"/>.</summary>
        public static string DebugPreview(int seed, float years)
        {
            var s = Need(out var err); if (s == null) return err;
            if (s.IsLive) return "play mode shows the live sim; use DebugSkipYears";
            s.previewSeed = seed; s.previewYears = years; s.preview = null;
            s.waveStep = -1;
            s.Box.Refresh();
            return s.Status();
        }
    }
}
