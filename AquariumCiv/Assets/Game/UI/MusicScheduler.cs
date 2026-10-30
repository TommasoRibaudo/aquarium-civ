using System;

namespace AquariumCiv.GameUI
{
    /// <summary>How often music plays (docs/research/11-music-playback.md). Off never starts a track.</summary>
    public enum MusicFrequency { Off = 0, Rare = 1, Normal = 2 }

    /// <summary>
    /// When a background track starts, Minecraft-style: rare on purpose. On opening a session there is a short random
    /// delay and a chance of no music at all; after a track ends a long random gap follows; the same track never plays
    /// twice in a row; and nothing starts while the game is <c>blocked</c> (a moment is running). Pure logic with no
    /// Unity types, so it is tested in EditMode; <see cref="MusicDirector"/> owns the audio.
    /// </summary>
    public sealed class MusicScheduler
    {
        public struct Settings
        {
            public float OpenDelayMin, OpenDelayMax, OpenChance, GapMin, GapMax;

            public static Settings Default => new Settings
            {
                OpenDelayMin = 5f, OpenDelayMax = 20f, OpenChance = 0.5f, GapMin = 180f, GapMax = 480f,
            };
        }

        readonly Random rng;
        readonly int trackCount;
        readonly Settings cfg;
        float countdown = -1f; // < 0 = nothing scheduled
        int last = -1;

        public MusicFrequency Frequency = MusicFrequency.Normal;
        public bool Playing { get; private set; }
        public float SecondsToNext => countdown;

        public MusicScheduler(Random rng, int trackCount, Settings cfg)
        {
            this.rng = rng;
            this.trackCount = trackCount;
            this.cfg = cfg;
        }

        /// <summary>The session opens (app start or return from away): maybe schedule a first track.</summary>
        public void OnSessionStart()
        {
            countdown = -1f;
            if (Playing || trackCount == 0 || Frequency == MusicFrequency.Off) return;
            if (rng.NextDouble() < cfg.OpenChance) countdown = Range(cfg.OpenDelayMin, cfg.OpenDelayMax);
            else countdown = Range(cfg.GapMin, cfg.GapMax) * GapScale;
        }

        /// <summary>The track that was playing ended (or was stopped): schedule the next one after a long gap.</summary>
        public void OnTrackFinished()
        {
            Playing = false;
            countdown = trackCount == 0 || Frequency == MusicFrequency.Off ? -1f : Range(cfg.GapMin, cfg.GapMax) * GapScale;
        }

        /// <summary>Start a track right now (debug cheat); the caller plays the returned index.</summary>
        public int StartNow()
        {
            if (trackCount == 0) return -1;
            countdown = -1f;
            return Begin();
        }

        /// <summary>Advance by <paramref name="dt"/> seconds. Returns the track index to start, or -1 for none.</summary>
        public int Tick(float dt, bool blocked)
        {
            if (Playing || countdown < 0f || blocked) return -1;
            if (Frequency == MusicFrequency.Off) { countdown = -1f; return -1; }
            countdown -= dt;
            if (countdown > 0f) return -1;
            countdown = -1f;
            return Begin();
        }

        float GapScale => Frequency == MusicFrequency.Rare ? 2f : 1f;

        int Begin()
        {
            int pick = rng.Next(trackCount);
            if (trackCount > 1 && pick == last) pick = (pick + 1 + rng.Next(trackCount - 1)) % trackCount;
            last = pick;
            Playing = true;
            return pick;
        }

        float Range(float min, float max) => min + (float)rng.NextDouble() * (max - min);
    }
}
