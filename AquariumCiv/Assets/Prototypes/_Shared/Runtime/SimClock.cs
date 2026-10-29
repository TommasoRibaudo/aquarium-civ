using System;
using System.Globalization;
using System.IO;
using AquariumCiv.Prototypes.Sim;
using UnityEngine;

namespace AquariumCiv.Prototypes
{
    /// <summary>
    /// Owns the sim, drives it from real time (x debug speed), persists it, and runs offline catch-up on launch/resume.
    /// The "away" buttons go through the same catch-up path as a real resume.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class SimClock : MonoBehaviour
    {
        [Serializable]
        class SaveFile
        {
            public string lastSeenUtc;
            public SimState state;
        }

        public SimConfig config = new();
        public int seed = 1234;
        [Tooltip("Debug speed multiplier on real time. 1 = design speed (1 real hour = 1 game year).")]
        public float speed = 1f;

        public SimCore Sim { get; private set; }
        /// <summary>Bumped when the world is replaced, so views drop everything they spawned.</summary>
        public int Generation { get; private set; }
        public static SimClock Instance { get; private set; }

        /// <summary>Raised after catch-up on launch, resume, or a debug away button: (real hours away, game years elapsed).</summary>
        public event Action<double, float> Returned;

        float saveTimer;
        string SavePath => Path.Combine(Application.persistentDataPath, "proto", $"{PrototypeConfig.Current.protoId}.json");

        void Awake()
        {
            Instance = this;
            Load();
        }

        void Start() => CatchUpFromLastSeen();

        void Update()
        {
            Sim.Advance(Time.unscaledDeltaTime / 3600.0 * speed);
            if ((saveTimer += Time.unscaledDeltaTime) > 30f) { saveTimer = 0; Save(); }
        }

        void OnApplicationPause(bool paused)
        {
            if (Sim == null) return;
            if (paused) { EventLog.Instance?.EndSession("background"); Save(); }
            else CatchUpFromLastSeen();
        }

        void OnApplicationQuit()
        {
            EventLog.Instance?.EndSession("quit");
            Save();
        }

        /// <summary>Debug: pretend the player was away for this many real hours.</summary>
        public void SimulateAway(double hours)
        {
            EventLog.Instance?.Log("debug", ("away_button_h", hours));
            EventLog.Instance?.EndSession("away_button");
            Return(hours);
        }

        public void ResetWorld()
        {
            Sim = new SimCore(config, SimCore.NewWorld(seed, config));
            Generation++;
            Save();
        }

        void CatchUpFromLastSeen()
        {
            double away = 0;
            if (PlayerPrefs.HasKey(LastSeenKey) &&
                DateTime.TryParse(PlayerPrefs.GetString(LastSeenKey), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var last))
                away = Math.Max(0, (DateTime.UtcNow - last).TotalHours); // ignore clocks moved backwards
            Return(away);
        }

        void Return(double hours)
        {
            long monthBefore = Sim.State.month;
            Sim.Advance(hours);
            float years = (Sim.State.month - monthBefore) / (float)SimConfig.MonthsPerYear;
            Save();
            EventLog.Instance?.StartSession(hours, years, Sim.MessLevel);
            Returned?.Invoke(hours, years);
        }

        string LastSeenKey => $"last_seen_{PrototypeConfig.Current.protoId}";

        void Load()
        {
            try
            {
                if (File.Exists(SavePath))
                {
                    var save = JsonUtility.FromJson<SaveFile>(File.ReadAllText(SavePath));
                    if (save?.state?.rng != null) { Sim = new SimCore(config, save.state); return; }
                }
            }
            catch (Exception e) { Debug.LogWarning($"[SimClock] Could not load save, starting fresh: {e.Message}"); }
            Sim = new SimCore(config, SimCore.NewWorld(seed, config));
        }

        public void Save()
        {
            var now = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
            Directory.CreateDirectory(Path.GetDirectoryName(SavePath));
            File.WriteAllText(SavePath, JsonUtility.ToJson(new SaveFile { lastSeenUtc = now, state = Sim.State }));
            PlayerPrefs.SetString(LastSeenKey, now);
            PlayerPrefs.Save();
        }
    }
}
