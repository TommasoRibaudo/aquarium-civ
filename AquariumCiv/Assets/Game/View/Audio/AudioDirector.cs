using System;
using System.Collections.Generic;
using System.Globalization;
using AquariumCiv.Audio;
using AquariumCiv.Game;
using AquariumCiv.GameUI;
using AquariumCiv.Life;
using AquariumCiv.Sim;
using AquariumCiv.View.Moments;
using UnityEngine;
using AudioSettings = AquariumCiv.Audio.AudioSettings;

namespace AquariumCiv.View
{
    /// <summary>
    /// The box's sound (#96): plays every <see cref="Sfx"/> cue through <see cref="CueGate"/> (cooldowns, voices, jitter),
    /// mixes by bus with plain source volumes (<see cref="MixLevels"/>: no AudioMixer asset, so nothing to wire in the
    /// scene), ducks the music under moments and cheers (<see cref="Ducker"/> → <see cref="MusicDirector.ExternalDuck"/>),
    /// runs the five ambience loops (<see cref="Ambience"/>), plays the era moments' cue timelines (<see cref="MomentCues"/>,
    /// with the rebuild wave's rising knocks) and sends the buzzes (<see cref="HapticGate"/>). Follows GameSettings
    /// (Sound, SfxVolume, AmbienceVolume, MusicVolume, Haptics) and the phone's silent switch (silent = no sound at all).
    /// Created on its own when a game clock is in the scene. Debug: the Sound section of the slice debug panel.
    /// </summary>
    public class AudioDirector : MonoBehaviour, Sfx.ISink
    {
        public static bool AutoCreate = true;
        public static AudioDirector Instance { get; private set; }
        /// <summary>Log each cue played to the console (on in the Editor: nobody can listen to the agents' runs).</summary>
        public static bool LogPlays = Application.isEditor;

        const int VoiceCount = CueGate.MaxTotalVoices;

        CueGate gate;
        readonly Ducker ducker = new();
        readonly HapticGate haptics = new();
        readonly AmbienceFader fader = new();
        Voice[] voices;
        AudioSource[] amb;
        readonly Dictionary<string, AudioClip> clips = new();
        readonly HashSet<string> warned = new();
        readonly List<(float at, string cue, float semitones, float volume)> delayed = new();
        MomentWatch eraWatch, lastWatch;
        AudioSettings settings = AudioSettings.Default;
        AmbienceLevels ambTarget;
        bool ambSnapped;

        class Voice { public AudioSource src; public int id; public CueDef cue; public float baseVolume; }

        /// <summary>Debug: force an ambience layer (0 = follow the box, 1 = on at full, 2 = off).</summary>
        public readonly int[] LayerForce = new int[AmbienceLevels.Count];

        // ---------- what happened (tests, the debug panel) ----------
        public int PlayedCount { get; private set; }
        public int MutedCount { get; private set; }
        public int DroppedCount { get; private set; }
        public int HapticsFired { get; private set; }
        public HapticKind LastHaptic { get; private set; }
        public string LastPlayed { get; private set; } = "";
        /// <summary>The last cues played, oldest first: "t cue clip".</summary>
        public readonly List<string> Recent = new();
        /// <summary>Era-moment cues played, in order (cue ids; tests).</summary>
        public readonly List<string> MomentLog = new();
        public float MusicDuck => ducker.Gain;
        public AudioSettings Settings => settings;
        public AmbienceLevels AmbienceTarget => ambTarget;
        public float AmbienceLevel(int layer) => fader.Level(layer);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (!AutoCreate || Instance != null || FindAnyObjectByType<GameClock>() == null) return;
            new GameObject("AudioDirector").AddComponent<AudioDirector>();
        }

        /// <summary>Makes one now if there is none (tests that build their own scene).</summary>
        public static AudioDirector Ensure() => Instance != null ? Instance : new GameObject("AudioDirector").AddComponent<AudioDirector>();

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            gate = new CueGate(Environment.TickCount);
            voices = new Voice[VoiceCount];
            for (int i = 0; i < VoiceCount; i++) voices[i] = new Voice { src = Source("Voice" + i, false) };
            amb = new AudioSource[AmbienceLevels.Count];
            for (int i = 0; i < amb.Length; i++)
            {
                amb[i] = Source("Amb_" + AmbienceLevels.CueIds[i], true);
                var c = CueLibrary.Get(AmbienceLevels.CueIds[i]);
                amb[i].clip = Clip(c.clips[0]);
                if (AmbienceLevels.CueIds[i] == CueLibrary.AmbWind) amb[i].pitch = 0.8f; // the storm's wind: the calm loop, lower and louder
            }
            eraWatch = new MomentWatch(this, () => MomentPlayer.Instance != null ? MomentPlayer.Instance.Director : null, () => MomentPlayer.Instance != null ? MomentPlayer.Instance.Wave : null);
            lastWatch = new MomentWatch(this, () => BoxCompleteController.Instance != null ? BoxCompleteController.Instance.Director : null, () => null);
            var missing = CheckClips();
            if (missing.Count > 0) Debug.LogWarning("[Audio] missing clips: " + string.Join(", ", missing));
            Sfx.Sink = this;
            ReadSettings();
        }

        void OnDestroy()
        {
            if (Instance != this) return;
            Instance = null;
            if (Sfx.Sink == (Sfx.ISink)this) Sfx.Sink = null;
            MusicDirector.ExternalDuck = 1f;
        }

        AudioSource Source(string name, bool loop)
        {
            var go = new GameObject(name) { hideFlags = HideFlags.DontSave };
            go.transform.SetParent(transform, false);
            var s = go.AddComponent<AudioSource>();
            s.playOnAwake = false; s.loop = loop; s.spatialBlend = 0f; s.volume = 0f;
            return s;
        }

        // ================================================================== Sfx.ISink
        public void Play(string cue, float semitones, float volume, float delay)
        {
            if (delay > 0f) { delayed.Add((Time.unscaledTime + delay, cue, semitones, volume)); return; }
            PlayNow(cue, semitones, volume);
        }

        public void Haptic(HapticKind kind) => Buzz(kind);

        public float Gain(SoundBus bus) => MixLevels.Bus(bus, settings, ducker.Gain);

        /// <summary>Plays a cue now; false when the gate drops it (cooldown, voices) or it is unknown.</summary>
        public bool PlayNow(string cueId, float semitones = 0f, float volume = 1f)
        {
            var cue = CueLibrary.Get(cueId);
            if (cue == null) { WarnOnce("cue:" + cueId, $"[Audio] unknown cue '{cueId}'"); return false; }
            float now = Time.unscaledTime;
            if (!gate.TryPlay(cueId, now, out var req, semitones, volume)) { DroppedCount++; return false; }
            if (req.haptic != HapticKind.None) Buzz(req.haptic);
            float bus = Gain(cue.bus);
            if (bus <= 0f) { MutedCount++; gate.Stop(req.voice); Note(now, cue.id, req.clip + " (muted)"); return true; }
            var clip = Clip(req.clip);
            if (clip == null) { gate.Stop(req.voice); return false; }
            if (req.stolenVoice > 0) foreach (var v in voices) if (v.id == req.stolenVoice) { v.src.Stop(); v.id = 0; }
            var voice = FreeVoice();
            voice.id = req.voice; voice.cue = cue; voice.baseVolume = req.volume;
            voice.src.clip = clip;
            voice.src.pitch = req.pitch;
            voice.src.volume = req.volume * bus;
            voice.src.Play();
            PlayedCount++;
            Note(now, cue.id, $"{req.clip} vol {req.volume * bus:0.00} pitch {req.pitch:0.00}");
            return true;
        }

        Voice FreeVoice()
        {
            Voice best = null;
            foreach (var v in voices)
            {
                if (!v.src.isPlaying) return v;
                if (best == null || v.src.time > best.src.time) best = v; // the one furthest through its clip
            }
            best.src.Stop();
            return best;
        }

        void Note(float now, string cue, string what)
        {
            LastPlayed = cue;
            Recent.Add(string.Format(CultureInfo.InvariantCulture, "{0:0.00} {1} {2}", now, cue, what));
            if (Recent.Count > 40) Recent.RemoveAt(0);
            if (LogPlays) Debug.Log($"[Audio] {cue}: {what}");
        }

        // ================================================================== clips
        AudioClip Clip(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            if (clips.TryGetValue(path, out var c)) return c;
            c = path.StartsWith("proc:") ? CareSound.MakeProc(path.Substring(5)) : Resources.Load<AudioClip>(path);
            if (c == null) WarnOnce(path, "[Audio] missing clip " + path);
            clips[path] = c;
            return c;
        }

        /// <summary>Every clip the cue library names that does not load (empty = all good).</summary>
        public List<string> CheckClips()
        {
            var missing = new List<string>();
            foreach (var cue in CueLibrary.All.Values)
                foreach (var p in cue.clips)
                {
                    if (clips.TryGetValue(p, out var c) ? c == null : (p.StartsWith("proc:") ? CareSound.MakeProc(p.Substring(5)) : Resources.Load<AudioClip>(p)) == null)
                        if (!missing.Contains(p)) missing.Add(p);
                }
            return missing;
        }

        void WarnOnce(string key, string msg) { if (warned.Add(key)) Debug.LogWarning(msg); }

        // ================================================================== every frame
        void ReadSettings()
        {
            settings = new AudioSettings
            {
                sound = GameSettings.Sound,
                sfx = GameSettings.SfxVolume,
                ambience = GameSettings.AmbienceVolume,
                music = GameSettings.MusicVolume,
                deviceSilent = DeviceSilent.IsSilent,
            };
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime, now = Time.unscaledTime;
            ReadSettings();
            if (Sfx.Sink != (Sfx.ISink)this) Sfx.Sink = this;

            for (int i = delayed.Count - 1; i >= 0; i--)
                if (delayed[i].at <= now) { var d = delayed[i]; delayed.RemoveAt(i); PlayNow(d.cue, d.semitones, d.volume); }

            eraWatch.Tick();
            lastWatch.Tick();
            bool inMoment = eraWatch.Playing || lastWatch.Playing;
            ducker.Tick(dt, gate.AnyDucking(now), inMoment);
            MusicDirector.ExternalDuck = ducker.Gain;

            // one-shots follow the settings while they sound (Sound off mid-cheer silences it)
            foreach (var v in voices)
                if (v.cue != null && v.src.isPlaying) v.src.volume = v.baseVolume * Gain(v.cue.bus);

            UpdateAmbience(dt, inMoment);
        }

        void UpdateAmbience(float dt, bool inMoment)
        {
            var sim = GameClock.Instance != null ? GameClock.Instance.Sim : null;
            var pv = PeopleView.Find();
            var planner = pv != null ? pv.Planner : null;
            var input = new AmbienceInput
            {
                era = sim != null ? sim.State.era : Era.Camp,
                hour = pv != null ? pv.Now.hour : 12f,
                weather = planner != null ? planner.WeatherOn(pv.Now.day) : Weather.Clear,
                season = planner != null ? planner.SeasonOn(pv.Now.day) : Season.Summer,
                mess01 = sim != null ? Mathf.Clamp01(sim.MessLevel) : 0f,
                inMoment = inMoment,
                festival = sim != null && sim.FestivalOn && pv != null && pv.Now.hour >= LifeClock.Evening,
            };
            ambTarget = Ambience.Targets(input);
            var target = ambTarget;
            for (int i = 0; i < LayerForce.Length; i++)
                if (LayerForce[i] != 0) target.Set(i, LayerForce[i] == 1 ? 1f : 0f);
            if (!ambSnapped) { fader.Snap(target); ambSnapped = true; } else fader.Tick(dt, target);

            float bus = Gain(SoundBus.Ambience);
            for (int i = 0; i < amb.Length; i++)
            {
                var src = amb[i];
                if (src.clip == null) continue;
                float v = fader.Level(i) * CueLibrary.Get(AmbienceLevels.CueIds[i]).volume * bus;
                src.volume = v;
                if (v > 0.001f && !src.isPlaying)
                {
                    if (src.time <= 0f) src.time = UnityEngine.Random.Range(0f, src.clip.length * 0.9f); // layers sharing a clip never line up
                    src.Play();
                }
                else if (v <= 0.001f && src.isPlaying) src.Pause();
            }
        }

        /// <summary>A layer's source is sounding (tests).</summary>
        public bool LayerPlaying(int i) => amb != null && amb[i].isPlaying && amb[i].volume > 0.001f;
        /// <summary>The loudest source volume right now, one-shots and ambience (tests: Sound off → 0).</summary>
        public float LoudestSource()
        {
            float m = 0f;
            foreach (var v in voices) if (v.src.isPlaying) m = Mathf.Max(m, v.src.volume);
            foreach (var a in amb) if (a.isPlaying) m = Mathf.Max(m, a.volume);
            return m;
        }

        // ================================================================== era moments
        /// <summary>Follows one MomentDirector: a fresh <see cref="MomentCues"/> for each moment it starts, the wave's
        /// knocks once the wave beat has built them, and the cues due each frame.</summary>
        class MomentWatch
        {
            readonly AudioDirector owner;
            readonly Func<MomentDirector> director;
            readonly Func<RebuildWave> wave;
            MomentDef def;
            MomentCues cues;
            RebuildWave waveAdded;
            float t, lastReal;
            public bool Playing { get; private set; }

            public MomentWatch(AudioDirector owner, Func<MomentDirector> director, Func<RebuildWave> wave)
            { this.owner = owner; this.director = director; this.wave = wave; }

            public void Tick()
            {
                var d = director();
                Playing = d != null && d.State == MomentDirector.Phase.Playing;
                if (!Playing) { cues = null; def = null; return; }
                if (cues == null || d.Def != def || d.RealTime < lastReal)
                {
                    def = d.Def;
                    cues = new MomentCues(def, d.Context);
                    waveAdded = null;
                    t = -0.001f;
                    owner.MomentLog.Clear();
                    foreach (var u in cues.Unknown) owner.WarnOnce("moment:" + u, $"[Audio] moment {def.id}: no cue for audio id '{u}'");
                    Debug.Log($"[Audio] moment {def.id}: {cues.Entries.Count} cues");
                }
                lastReal = d.RealTime;
                var w = wave();
                if (w != null && w != waveAdded)
                {
                    var beat = def.beats.Find(b => b.Has(BeatAction.RebuildWave) && d.Context.Plays(b));
                    cues.AddWave(w, beat);
                    waveAdded = w;
                }
                foreach (var e in cues.Due(t, d.Time))
                {
                    owner.MomentLog.Add(e.cue);
                    owner.PlayNow(e.cue, e.semitones);
                }
                t = d.Time;
            }
        }

        // ================================================================== haptics
        void Buzz(HapticKind kind)
        {
            if (!haptics.TryBuzz(kind, Time.unscaledTime, GameSettings.Haptics, out var pattern)) return;
            HapticsFired++;
            LastHaptic = kind;
            HapticOut.Fire(kind, pattern);
        }

        // ================================================================== debug (the slice debug panel, `script eval`)
        public static string Status()
        {
            var a = Instance;
            if (a == null) return "audio: no AudioDirector";
            var s = a.settings;
            var t = a.ambTarget;
            return string.Format(CultureInfo.InvariantCulture,
                "audio: sound {0}{1} · sfx {2:0.00} amb {3:0.00} music {4:0.00} · duck {5:0.00}\nbuses: ui/care/world {6:0.00} amb {7:0.00} music {8:0.00}\namb now: calm {9:0.00} birds {10:0.00} crickets {11:0.00} rain {12:0.00} wind {13:0.00} flies {19:0.00}\nplayed {14} (muted {15}, dropped {16}) · buzzes {17} · last {18}",
                s.sound ? "on" : "OFF", s.deviceSilent ? " (phone silent)" : "", s.sfx, s.ambience, s.music, a.ducker.Gain,
                a.Gain(SoundBus.Care), a.Gain(SoundBus.Ambience), a.Gain(SoundBus.Music),
                a.fader.Level(0), a.fader.Level(1), a.fader.Level(2), a.fader.Level(3), a.fader.Level(4),
                a.PlayedCount, a.MutedCount, a.DroppedCount, a.HapticsFired, a.LastPlayed, a.fader.Level(5));
        }

        public static string DebugPlay(string cue)
        {
            var a = Instance;
            if (a == null) return "no AudioDirector";
            bool ok = a.PlayNow(cue);
            return ok ? $"played {cue}" : $"{cue} dropped (cooldown/voices) or unknown";
        }

        /// <summary>Cycles a layer: follow the box → on → off.</summary>
        public static string DebugCycleLayer(int i)
        {
            var a = Instance;
            if (a == null) return "no AudioDirector";
            a.LayerForce[i] = (a.LayerForce[i] + 1) % 3;
            return $"{AmbienceLevels.CueIds[i]}: {LayerForceName(a.LayerForce[i])}";
        }

        public static string LayerForceName(int f) => f == 1 ? "on" : f == 2 ? "off" : "auto";

        public static string DebugCheckClips()
        {
            var a = Instance;
            if (a == null) return "no AudioDirector";
            var m = a.CheckClips();
            return m.Count == 0 ? $"all {CueLibrary.All.Count} cues' clips load" : "missing: " + string.Join(", ", m);
        }

        public static string DebugBuzz(HapticKind k)
        {
            var a = Instance;
            if (a == null) return "no AudioDirector";
            int before = a.HapticsFired;
            a.Buzz(k);
            return a.HapticsFired > before ? $"buzz {k}" : $"buzz {k} not sent (haptics {(GameSettings.Haptics ? "on" : "off")}, gap)";
        }
    }
}
