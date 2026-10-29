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

        public SimCore Sim => Session?.sim;
        /// <summary>How the box opened (load result, away hours, catch-up). For the return chronicle.</summary>
        public ClockStart Session { get; private set; }
        /// <summary>The latest catch-up (the open, a resume from the background, or <see cref="DebugAway"/>).</summary>
        public CatchUpResult LastAway { get; private set; }
        public event Action<CatchUpResult> CaughtUp;
        public string SavePath { get; private set; }

        float sinceSave;
        bool paused;
        DateTime pausedUtc;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            SavePath = Path.Combine(Application.persistentDataPath, "game", "box.json");
            Session = ClockLogic.Start(SavePath, DateTime.UtcNow, config, new System.Random().Next(1, int.MaxValue));
            LastAway = Session.catchUp;
            if (Session.load == SaveStore.LoadResult.TooNew)
                Debug.LogWarning($"[GameClock] {SavePath} is from a newer build: playing a fresh box without saving; the file is left untouched.");
            if (Session.corruptKeptAs != null)
                Debug.LogWarning($"[GameClock] the save was broken: kept aside as {Session.corruptKeptAs}, started a new box.");
            Save();
            Debug.Log("[GameClock] " + Session.Summary());
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        void Update()
        {
            if (Sim == null) return;
            Sim.Advance(Math.Min(Time.unscaledDeltaTime, maxFrameSeconds) / 3600.0);
            sinceSave += Time.unscaledDeltaTime;
            if (sinceSave >= autosaveSeconds) Save();
        }

        void OnApplicationPause(bool pause)
        {
            if (pause) GoAway();
            else Return(); // also fires once at startup (tooling.md): Return() ignores it unless we went away
        }

        void OnApplicationFocus(bool focus) { if (!focus) Save(); }

        void OnApplicationQuit() => Save();

        void GoAway()
        {
            if (paused) return;
            paused = true;
            pausedUtc = DateTime.UtcNow;
            Save();
        }

        void Return()
        {
            if (!paused || Sim == null) return;
            paused = false;
            Report(ClockLogic.Resume(Sim, pausedUtc, DateTime.UtcNow), "back from the background");
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

        // ---------- debug (callable from `unity command script eval`) ----------
        /// <summary>Live time jump: the civ lives <paramref name="hours"/> at full speed, mess as in live play.</summary>
        public static string DebugSkipHours(double hours)
        {
            var c = Instance;
            if (c == null || c.Sim == null) return "no GameClock running";
            int months = c.Sim.Advance(hours);
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
