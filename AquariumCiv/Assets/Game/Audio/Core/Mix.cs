using System;

namespace AquariumCiv.Audio
{
    /// <summary>
    /// The player's audio settings as plain numbers (#96), so the mix maths stays pure. Phase 2 fills it from
    /// <c>GameSettings</c> (Sound, SfxVolume, AmbienceVolume, MusicVolume; #95 shows them) and the device's silent switch.
    /// </summary>
    public struct AudioSettings
    {
        public bool sound;          // the master switch (GameSettings.Sound)
        public float sfx;           // UI, care, world and moment cues, 0..1
        public float ambience;      // the ambience loops, 0..1
        public float music;         // background music, 0..1 (GameSettings.MusicVolume)
        public bool deviceSilent;   // the phone's silent/ring switch: everything stays quiet (user, 2026-10-30); haptics may still buzz

        public static AudioSettings Default => new() { sound = true, sfx = 1f, ambience = 0.8f, music = 0.6f };
    }

    /// <summary>
    /// Each bus's volume from the settings and the duck (#96): the master switch and the phone's silent switch both
    /// silence everything (the user's call, 2026-10-30: a phone on silent makes no sound; haptics are separate).
    /// </summary>
    public static class MixLevels
    {
        public static float Bus(SoundBus bus, in AudioSettings s, float duck = 1f)
        {
            if (!s.sound || s.deviceSilent) return 0f;
            switch (bus)
            {
                case SoundBus.Music: return Clamp01(s.music) * duck;
                case SoundBus.Ambience: return Clamp01(s.ambience) * AmbienceUnder(duck);
                default: return Clamp01(s.sfx);
            }
        }

        /// <summary>Ambience dips less than music under a moment: the box keeps breathing.</summary>
        public static float AmbienceUnder(float musicDuck) => 1f - (1f - musicDuck) * 0.5f;

        /// <summary>A 0..1 gain as decibels for an AudioMixer parameter (-80 dB = off).</summary>
        public static float Decibels(float gain) => gain <= 0.0001f ? -80f : Math.Max(-80f, 20f * (float)Math.Log10(gain));

        static float Clamp01(float x) => x < 0f ? 0f : x > 1f ? 1f : x;
    }

    /// <summary>
    /// The music duck (#96, era-moment.md §10): while an era moment plays, or a cue that ducks sounds (the cheer, the
    /// chord, the bell), the music sinks to <see cref="DuckedGain"/>, quickly (<see cref="Attack"/>), and comes back
    /// slowly after (<see cref="Release"/>, with <see cref="Hold"/> first so a run of knocks doesn't pump it).
    /// </summary>
    public class Ducker
    {
        /// <summary>The music's level under a cue (0.25 ≈ -12 dB) [est].</summary>
        public const float DuckedGain = 0.25f;
        /// <summary>The music's level for the whole of an era moment: nearly gone, the moment's cues are its music [est].</summary>
        public const float MomentGain = 0.1f;
        public const float Attack = 0.25f, Release = 1.5f, Hold = 0.6f; // seconds

        public float Gain { get; private set; } = 1f;
        float sinceDuck = float.MaxValue;

        /// <param name="dt">Unscaled seconds since the last call.</param>
        /// <param name="cueDucking">A ducking cue sounds (<see cref="CueGate.AnyDucking"/>).</param>
        /// <param name="inMoment">An era moment plays (MomentDirector.State == Playing).</param>
        public float Tick(float dt, bool cueDucking, bool inMoment)
        {
            float target = 1f;
            if (inMoment) target = MomentGain;
            else if (cueDucking) target = DuckedGain;

            if (target < 1f) sinceDuck = 0f; else if (sinceDuck < float.MaxValue) sinceDuck += dt;
            if (target >= 1f && sinceDuck < Hold) target = Gain; // hold where it is for a moment

            float rate = target < Gain ? 1f / Attack : 1f / Release; // full range in Attack / Release seconds
            Gain = target < Gain ? Math.Max(target, Gain - rate * dt) : Math.Min(target, Gain + rate * dt);
            return Gain;
        }
    }
}
