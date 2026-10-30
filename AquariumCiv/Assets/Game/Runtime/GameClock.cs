using System;
using System.Globalization;
using System.IO;
using AquariumCiv.Sim;
using UnityEngine;

namespace AquariumCiv.Game
{
    /// <summary>
    /// Drives the slice's time (vertical-slice-plan §5.2, game definition §9): loads the box (or starts one), runs the
    /// offline catch-up, then advances the sim by unscaled real time (1 real hour = 1 game year) and autosaves every
    /// <see cref="autosaveSeconds"/> and on pause, focus loss and quit. The decisions live in <see cref="ClockLogic"/>.
    /// Debug hooks (until the debug panel arrives with the UI): <see cref="DebugSkipHours"/>, <see cref="DebugAway"/>.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public class GameClock : MonoBehaviour
    {
        public static GameClock Instance { get; private set; }

        public SimConfig config = new();
        public float autosaveSeconds = 30f;
        /// <summary>A hitch or a debugger stop never fast-forwards the live world by more than this.</summary>
        public float maxFrameSeconds = 1f;

        /// <summary>The box on screen: a visited museum, else the active box.</summary>
        public SimCore Sim => Session?.ViewSim;
        /// <summary>The shelf's active box: the one that lives, saves and gets the offline catch-up.</summary>
        public SimCore ActiveSim => Session?.sim;
        public ShelfState Shelf => Session?.shelf;
        /// <summary>The box on screen changed (a visit began or ended, or box 2 arrived). The view rebuilds.</summary>
        public event Action ViewChanged;
        /// <summary>How the box opened (load result, away hours, catch-up). For the return chronicle.</summary>
        public ClockStart Session { get; private set; }
        /// <summary>The latest catch-up (the open, a resume from the background, or <see cref="DebugAway"/>).</summary>
        public CatchUpResult LastAway { get; private set; }
        public event Action<CatchUpResult> CaughtUp;
        public string SavePath { get; private set; }

        /// <summary>The sim clock holds still (an era moment plays, era-moment.md §3: no months pass, mess doesn't grow).
        /// Real time is not banked: the held seconds are simply not lived.</summary>
        public bool Held { get; set; }

        /// <summary>Tests: the seed of the next new world this clock starts (used once, then back to 0 = random). Play tests
        /// that assert on what a box's people do pin it, so a run doesn't depend on a random world.</summary>
        public static int DebugNextSeed;

        // ---------- telemetry (#81, #100) ----------
        /// <summary>The event log (null until Awake, or when logging could not start). Safe to use through <see cref="Tel"/>.</summary>
        public TelemetryLog Telemetry { get; private set; }
        /// <summary>Null-safe shortcut for the hooks: <c>GameClock.Tel.Cleaned(...)</c> works with no clock running (the extension methods accept null).</summary>
        public static TelemetryLog Tel => Instance != null ? Instance.Telemetry : null;
        /// <summary>Debug: whole days the telemetry clock runs ahead of the phone's (fakes day 3 for the recall question and the retention metrics).</summary>
        public static double DebugDayOffset;
        public static DateTime TelemetryNow => DateTime.UtcNow.AddDays(DebugDayOffset);
        public string TelemetryDir { get; private set; }
        float sessionStart;
        double startMs;

        float sinceSave;
        bool paused;
        DateTime pausedUtc;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            SavePath = Path.Combine(Application.persistentDataPath, "game", "box.json");
            int seed = DebugNextSeed != 0 ? DebugNextSeed : new System.Random().Next(1, int.MaxValue);
            DebugNextSeed = 0; // one box only
            var sw = System.Diagnostics.Stopwatch.StartNew();
            Session = ClockLogic.Start(SavePath, DateTime.UtcNow, config, seed);
            startMs = sw.Elapsed.TotalMilliseconds;
            LastAway = Session.catchUp;
            if (Session.load == SaveStore.LoadResult.TooNew)
                Debug.LogWarning($"[GameClock] {SavePath} is from a newer build: playing a fresh box without saving; the file is left untouched.");
            if (Session.corruptKeptAs != null)
                Debug.LogWarning($"[GameClock] the save was broken: kept aside as {Session.corruptKeptAs}, started a new box.");
            Save();
            Debug.Log("[GameClock] " + Session.Summary());
            StartTelemetry();
        }

        void StartTelemetry()
        {
            try
            {
                TelemetryDir = Path.Combine(Application.persistentDataPath, "game", "telemetry");
                Telemetry = new TelemetryLog(new FileTelemetryStorage(TelemetryDir), () => TelemetryNow, BoxTelemetryId());
                BeginSession("open", Session.awayHours);
                if (!Session.newWorld)
                    Telemetry.CaughtUp(Session.catchUp.realHours, Session.catchUp.simHours, Session.catchUp.months, startMs);
            }
            catch (Exception ex) { Telemetry = null; Debug.LogWarning($"[GameClock] telemetry off: {ex.Message}"); }
        }

        string BoxTelemetryId() => "b" + (Session?.shelf?.activeIndex ?? 0);

        void BeginSession(string reason, double awayHours)
        {
            sessionStart = Time.realtimeSinceStartup;
            Telemetry.StartSession(reason, awayHours, Application.version, Application.platform.ToString(),
                (int)TimeZoneInfo.Local.GetUtcOffset(DateTime.UtcNow).TotalMinutes);
        }

        void EndTelemetrySession(string reason)
        {
            if (Telemetry == null) return;
            Telemetry.EndSession(reason, Time.realtimeSinceStartup - sessionStart);
        }

        /// <summary>Seconds since this session (an open, or a return after a real absence) began.</summary>
        public double SessionSeconds => Time.realtimeSinceStartup - sessionStart;

        void OnDestroy() { if (Instance == this) Instance = null; }

        void Update()
        {
            if (ActiveSim == null) return;
            if (!Held) ActiveSim.Advance(Math.Min(Time.unscaledDeltaTime, maxFrameSeconds) / 3600.0);
            sinceSave += Time.unscaledDeltaTime;
            if (sinceSave >= autosaveSeconds) Save();
            Telemetry?.Tick();
        }

        void OnApplicationPause(bool pause)
        {
            if (pause) GoAway();
            else Return(); // also fires once at startup (tooling.md): Return() ignores it unless we went away
        }

        void OnApplicationFocus(bool focus) { if (!focus) Save(); }

        void OnApplicationQuit() { Save(); EndTelemetrySession("quit"); }

        void GoAway()
        {
            if (paused) return;
            paused = true;
            pausedUtc = DateTime.UtcNow;
            Save();
            EndTelemetrySession("pause");
        }

        void Return()
        {
            if (!paused || ActiveSim == null) return;
            paused = false;
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var back = ClockLogic.Resume(ActiveSim, pausedUtc, DateTime.UtcNow);
            double ms = sw.Elapsed.TotalMilliseconds;
            if (Telemetry != null && Telemetry.ShouldStartNewSession((DateTime.UtcNow - pausedUtc).TotalSeconds)) BeginSession("return", back.realHours);
            Telemetry.CaughtUp(back.realHours, back.simHours, back.months, ms);
            Report(back, "back from the background");
            Save();
        }

        void Report(CatchUpResult r, string why)
        {
            LastAway = r;
            CaughtUp?.Invoke(r);
            Debug.Log(string.Format(CultureInfo.InvariantCulture, "[GameClock] {0}: away {1:0.00} h → lived {2:0.00} h ({3} months) · year {4:0.0} · pop {5} · mess {6:0.00}",
                why, r.realHours, r.simHours, r.months, Sim.State.Year, Sim.State.population, Sim.MessLevel));
        }

        public bool Save()
        {
            sinceSave = 0;
            if (Session == null) return false;
            try { return ClockLogic.Save(SavePath, Session, DateTime.UtcNow); }
            catch (Exception ex) { Debug.LogError($"[GameClock] save failed: {ex.Message}"); return false; }
        }

        // ---------- the shelf (#93) ----------
        public bool Visiting => Session != null && Session.visit != null;

        /// <summary>Completes the box: the finished box becomes a museum, box 2 is the active one, and the file is written at
        /// once (one atomic write holds both boxes: a kill before it keeps the old box, after it both). The box on screen
        /// stays the finished one (<see cref="Visiting"/>) until <see cref="ShowActive"/>. Returns box 2, or null.</summary>
        public SimCore CompleteBox(string setting = BoxSetting.DesertOasis, int seed = 0, bool force = false)
        {
            if (Session == null || ActiveSim == null || Visiting) return null;
            var done = ActiveSim;
            var completion = force ? done.ForceCompleteBox() : done.CompleteBox();
            if (completion == null) return null;
            int boxSeed = seed != 0 ? seed : new System.Random().Next(1, int.MaxValue);
            ClockLogic.StartNextBox(Session, config, completion, setting, boxSeed, DateTime.UtcNow);
            Telemetry?.SwitchBox(BoxTelemetryId());
            Session.visit = done; Session.visitIndex = Session.shelf.boxes.Find(b => b.state == done.State)?.index ?? -1;
            Save();
            return Session.sim;
        }

        /// <summary>Looks at a museum box (see <see cref="ClockLogic.Visit"/>).</summary>
        public bool VisitBox(int index, out MuseumResult result)
        {
            result = default;
            if (Session == null || !ClockLogic.Visit(Session, index, config, DateTime.UtcNow, out result)) return false;
            Save();
            ViewChanged?.Invoke();
            return true;
        }

        /// <summary>Back to the active box.</summary>
        public void ShowActive()
        {
            if (Session == null || !Visiting) return;
            ClockLogic.EndVisit(Session);
            Save();
            ViewChanged?.Invoke();
        }

        // ---------- debug (callable from `unity command script eval`) ----------
        /// <summary>Live time jump: the civ lives <paramref name="hours"/> at full speed, mess as in live play.</summary>
        public static string DebugSkipHours(double hours)
        {
            var c = Instance;
            if (c == null || c.ActiveSim == null) return "no GameClock running";
            int months = c.ActiveSim.Advance(hours);
            c.Save();
            return string.Format(CultureInfo.InvariantCulture, "skipped {0:0.00} h ({1} months) · year {2:0.0} · pop {3}", hours, months, c.Sim.State.Year, c.Sim.State.population);
        }

        /// <summary>As if the player had been away <paramref name="hours"/>: the offline catch-up with the slow tail.</summary>
        public static string DebugAway(double hours)
        {
            var c = Instance;
            if (c == null || c.Sim == null) return "no GameClock running";
            var r = c.Sim.CatchUp(Math.Clamp(hours, 0, SaveStore.MaxAwayHours));
            c.Report(r, "debug away");
            c.Save();
            return string.Format(CultureInfo.InvariantCulture, "away {0:0.00} h → lived {1:0.00} h ({2} months) · year {3:0.0} · mess {4:0.00}", r.realHours, r.simHours, r.months, c.Sim.State.Year, c.Sim.MessLevel);
        }
    }
}
