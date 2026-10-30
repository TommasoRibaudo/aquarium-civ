using System.Collections.Generic;
using AquariumCiv.Game;
using UnityEngine;
using Random = System.Random;

namespace AquariumCiv.GameUI
{
    /// <summary>
    /// Plays the background tracks from <c>Resources/Music</c> when <see cref="MusicScheduler"/> says so (rare on
    /// purpose, see docs/research/11-music-playback.md). Fades in and out, ducks while a moment holds it
    /// (<see cref="Hold"/>), and pauses with the app. Created on its own when a game clock is in the scene.
    /// </summary>
    public class MusicDirector : MonoBehaviour
    {
        public const float FadeIn = 4f, FadeOut = 3f, DuckFade = 1f;

        public static MusicDirector Instance { get; private set; }
        static readonly HashSet<string> holds = new HashSet<string>();

        AudioClip[] clips;
        AudioSource source;
        MusicScheduler scheduler;
        float duck = 1f; // 1 = full, 0 = ducked out
        bool appPaused;
        string nowPlaying = "";

        public string Status =>
            nowPlaying.Length > 0 ? $"music: {nowPlaying} ({source.time:0}/{source.clip.length:0}s)"
            : scheduler == null || scheduler.SecondsToNext < 0f ? "music: none scheduled"
            : $"music: next in {scheduler.SecondsToNext:0}s";

        /// <summary>A moment (era, evening talk, mourning, ...) holds the music: it fades down and nothing starts until released.</summary>
        public static void Hold(string who) => holds.Add(who);
        /// <summary>#96: the AudioDirector's duck (era moments, the cheer, the chord): 1 = full, set every frame.</summary>
        public static float ExternalDuck = 1f;
        public static void Release(string who) => holds.Remove(who);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            holds.Clear();
            ExternalDuck = 1f;
            if (Instance != null || FindAnyObjectByType<GameClock>() == null) return;
            new GameObject("MusicDirector").AddComponent<MusicDirector>();
        }

        void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
            clips = Resources.LoadAll<AudioClip>("Music");
            source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 0f;
            scheduler = new MusicScheduler(new Random(), clips.Length, MusicScheduler.Settings.Default)
            {
                Frequency = GameSettings.MusicFrequency,
            };
            scheduler.OnSessionStart();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void OnApplicationPause(bool paused)
        {
            // Unity fires OnApplicationPause(false) at startup too; only a real resume is "back from away".
            bool resumed = !paused && appPaused;
            appPaused = paused;
            if (paused) { if (source != null) source.Pause(); }
            else
            {
                if (source != null) source.UnPause();
                if (!resumed) return;
                // Back from away: a fresh chance of music, unless a track is already going.
                if (scheduler != null && !scheduler.Playing) scheduler.OnSessionStart();
            }
        }

        void Update()
        {
            if (scheduler == null || appPaused) return;
            float dt = Time.unscaledDeltaTime;
            scheduler.Frequency = GameSettings.MusicFrequency;
            bool held = holds.Count > 0 || DeviceSilent.IsSilent; // silent phone: no new tracks, a playing one fades out and stays held

            int start = scheduler.Tick(dt, held);
            if (start >= 0) Play(start);

            if (!scheduler.Playing) return;
            duck = Mathf.MoveTowards(duck, held ? 0f : 1f, dt / DuckFade);
            float len = source.clip.length;
            float fade = Mathf.Min(source.time / FadeIn, Mathf.Max(0f, (len - source.time) / FadeOut), 1f);
            if (scheduler.Frequency == MusicFrequency.Off) fade = Mathf.MoveTowards(source.volume, 0f, dt / FadeOut);
            source.volume = fade * duck * GameSettings.MusicVolume * Mathf.Clamp01(ExternalDuck);

            bool ended = !source.isPlaying || scheduler.Frequency == MusicFrequency.Off && source.volume <= 0.001f;
            if (ended) Stop();
        }

        void Play(int index)
        {
            source.clip = clips[index];
            source.volume = 0f;
            source.time = 0f;
            source.Play();
            nowPlaying = clips[index].name;
        }

        void Stop()
        {
            source.Stop();
            nowPlaying = "";
            scheduler.OnTrackFinished();
        }

        /// <summary>Debug cheat: start a track now (fades in).</summary>
        public static string DebugPlayNow()
        {
            if (Instance == null || Instance.clips.Length == 0) return "no music director or no tracks";
            if (Instance.scheduler.Playing) Instance.Stop();
            Instance.Play(Instance.scheduler.StartNow());
            return "playing " + Instance.nowPlaying;
        }

        /// <summary>Debug cheat: stop the current track (the next one follows after the usual gap).</summary>
        public static string DebugStop()
        {
            if (Instance == null || !Instance.scheduler.Playing) return "nothing playing";
            Instance.Stop();
            return "stopped";
        }
    }
}
