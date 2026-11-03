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
        /// <summary>How many times faster than real time the live box runs (1 = an hour a year). The P7 lab runs at
        /// <see cref="LabSpeed"/> (debug panel, #225). Only the live clock: the catch-up after an absence is unchanged.</summary>
        public float speed = 1f;
        public const float LabSpeed = 60f;

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
        /// <summary>Play tests send the save to a folder of their own (null = the player's), so a clock still alive after a test
        /// (autosave, focus, quit) can't write a test box over the real one. Read at Awake: a clock keeps its path to the end.</summary>
        public static string SaveFolderOverride;
        public static string DefaultSavePath => Path.Combine(SaveFolderOverride ?? Path.Combine(Application.persistentDataPath, "game"), "box.json");

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
            SavePath = DefaultSavePath;
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
            Notifier = new EraNotifier(PlatformOverride ?? EraNotifier.MakePlatform());
            Notifier.Open();
        }

        void StartTelemetry()
        {
            try
            {
                TelemetryDir = Path.Combine(Path.GetDirectoryName(SavePath), "telemetry");
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
            Telemetry.SetVariant(DilemmaSurvey.VariantOf(config.dilemmas)); // P7 (#225): A = dilemmas off, B = on, in every session_start
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

        void OnDestroy()
        {
            Notifier?.Stop();
            if (Instance == this) Instance = null;
        }

        void Update()
        {
            if (ActiveSim == null) return;
            if (!Held) ActiveSim.Advance(Math.Min(Time.unscaledDeltaTime, maxFrameSeconds) * Math.Max(0f, speed) / 3600.0);
            if (Telemetry != null) DilemmaTracker()?.Observe(ActiveSim, Telemetry);
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

        void OnApplicationQuit()
        {
            Save();
            EndTelemetrySession("quit");
            if (!paused) Notifier?.Close(ActiveSim, DateTime.UtcNow, EraNotifier.Enabled); // a pause before it has done this already
        }

        void GoAway()
        {
            if (paused) return;
            paused = true;
            pausedUtc = DateTime.UtcNow;
            Save();
            EndTelemetrySession("pause");
            Notifier?.Close(ActiveSim, pausedUtc, EraNotifier.Enabled);
        }

        void Return()
        {
            if (!paused || ActiveSim == null) return;
            paused = false;
            Notifier?.Open();
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

        // ---------- the notification (#97) ----------
        /// <summary>The era notification (#97): run ahead at close, cancelled at open.</summary>
        public EraNotifier Notifier { get; private set; }
        /// <summary>Tests: the phone a new clock talks to (null = the real one, or a log).</summary>
        public static ILocalNotifications PlatformOverride;

        /// <summary>The player said yes: this asks the phone, the only place that does, and keeps its answer as the setting.</summary>
        public void AskNotifications(Action<bool> answered = null)
        {
            StartCoroutine(Notifier.Phone.Ask(granted =>
            {
                EraNotifier.Enabled = granted;
                answered?.Invoke(granted);
            }));
        }

        // ---------- reset (#95) ----------
        /// <summary>The player confirmed Reset: the active box is gone and a new world takes its place, with its first run. Finished boxes stay on the shelf.</summary>
        public void ResetBox(int seed = 0)
        {
            if (Session == null) return;
            ClockLogic.ResetActive(Session, config, seed != 0 ? seed : new System.Random().Next(1, int.MaxValue));
            LastAway = default;
            Notifier?.Open();
            Save();
            ViewChanged?.Invoke();
            Debug.Log("[GameClock] reset: " + Session.Summary());
        }

        // ---------- P7 dilemma telemetry (#225) ----------
        DilemmaTelemetryTracker dilemmaTel;
        SimCore dilemmaTelSim;

        /// <summary>The dilemma event tracker of the active box (a new one when the active box changes; null with no box). A visited museum
        /// box has none: its dilemmas are history.</summary>
        public DilemmaTelemetryTracker DilemmaTracker()
        {
            var sim = ActiveSim;
            if (sim == null) return null;
            if (dilemmaTel == null || dilemmaTelSim != sim) { dilemmaTel = new DilemmaTelemetryTracker(sim); dilemmaTelSim = sim; }
            return dilemmaTel;
        }

        /// <summary>The tracker for <paramref name="sim"/> when it is the active box, else null (the card stack asks with its own box).</summary>
        public static DilemmaTelemetryTracker DilemmaTrackerFor(SimCore sim) =>
            Instance != null && sim != null && Instance.ActiveSim == sim ? Instance.DilemmaTracker() : null;

        static double RealNow => Time.realtimeSinceStartupAsDouble;

        /// <summary>The question is in front of the player (the box scene opened, or a cheat staged it). Once per dilemma.</summary>
        public static void DilemmaShown(Dilemma d) { var t = Instance != null ? Instance.DilemmaTracker() : null; t?.Shown(Tel, d, RealNow); }

        /// <summary>Call right after a successful <see cref="SimCore.AnswerDilemma"/> made outside the card stack (the box scene's own
        /// fallback, a cheat). Once per dilemma.</summary>
        public static void DilemmaAnswered(Dilemma d) { var t = Instance != null ? Instance.DilemmaTracker() : null; t?.Answered(Tel, d, RealNow); }

        /// <summary>Debug (#225): dilemmas on (variant B) or off (A). Logged as a variant change. Off leaves an open dilemma open (it waits).</summary>
        public static string DebugDilemmas(bool on)
        {
            var c = Instance;
            if (c == null) return "no GameClock running";
            c.config.dilemmas = on;
            c.Telemetry?.SetVariant(DilemmaSurvey.VariantOf(on));
            return c.DilemmaStatus();
        }

        /// <summary>Debug (#225): the P7 lab preset: the lab schedule (the first at year 2, then about every 3 years) and the live clock at
        /// <see cref="LabSpeed"/>. Off: the normal schedule (6 to 10 years) at real time. The next dilemma is scheduled afresh.</summary>
        public static string DebugLab(bool on)
        {
            var c = Instance;
            if (c == null) return "no GameClock running";
            c.config.dilemmaLab = on;
            c.speed = on ? LabSpeed : 1f;
            if (c.ActiveSim != null && c.ActiveSim.OpenDilemma == null) c.ActiveSim.State.dilemmaNextMonth = -1;
            return c.DilemmaStatus();
        }

        public string DilemmaStatus() => string.Format(CultureInfo.InvariantCulture, "dilemmas {0} (variant {1}) · {2} · clock x{3:0} · next due month {4} (now {5})",
            config.dilemmas ? "on" : "off", DilemmaSurvey.VariantOf(config.dilemmas), config.dilemmaLab ? "lab schedule" : "normal schedule", speed,
            ActiveSim?.State.dilemmaNextMonth ?? -1, ActiveSim?.State.month ?? 0);

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

        /// <summary>As if the player left now: what a close would schedule. The box goes on living.</summary>
        public static string DebugClose()
        {
            var c = Instance;
            if (c == null || c.ActiveSim == null) return "no GameClock running";
            c.Notifier.Close(c.ActiveSim, DateTime.UtcNow, true);
            c.Notifier.WaitForRun(30000);
            return c.Notifier.Last?.ToString() ?? "nothing";
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
