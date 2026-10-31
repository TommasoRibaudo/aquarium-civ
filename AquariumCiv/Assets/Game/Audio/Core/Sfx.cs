using System.Collections.Generic;

namespace AquariumCiv.Audio
{
    /// <summary>
    /// Where the game asks for a sound (#96): every emitter (care, shaping, the glass, the HUD, cards, the first run) calls
    /// <see cref="Play"/> with a <see cref="CueLibrary"/> id and never touches an AudioSource. The AudioDirector on the
    /// box is the <see cref="Sink"/>; with no sink (EditMode tests, a scene without a box) the ask is only counted.
    /// </summary>
    public static class Sfx
    {
        public interface ISink
        {
            void Play(string cue, float semitones, float volume, float delay);
            void Haptic(HapticKind kind);
            /// <summary>The bus's current gain (settings × duck), for sounds the view drives itself (the wipe rub).</summary>
            float Gain(SoundBus bus);
        }

        public static ISink Sink;

        /// <summary>Asks so far (all of them, played or not) and the last few ids, newest last (tests, the debug panel).</summary>
        public static int Asked { get; private set; }
        public static readonly List<string> LastAsked = new();
        const int Keep = 24;

        /// <summary>Plays <paramref name="cue"/> now (or after <paramref name="delay"/> seconds), shifted by
        /// <paramref name="semitones"/>, at <paramref name="volume"/> × the cue's own volume.</summary>
        public static void Play(string cue, float semitones = 0f, float volume = 1f, float delay = 0f)
        {
            if (string.IsNullOrEmpty(cue)) return;
            Asked++;
            LastAsked.Add(cue);
            if (LastAsked.Count > Keep) LastAsked.RemoveAt(0);
            Sink?.Play(cue, semitones, volume, delay);
        }

        /// <summary>A buzz on its own (no sound): goes through the director's gate and the Haptics setting.</summary>
        public static void Haptic(HapticKind kind) { if (kind != HapticKind.None) Sink?.Haptic(kind); }

        public static float Gain(SoundBus bus) => Sink != null ? Sink.Gain(bus) : 1f;

        public static void ResetLog() { Asked = 0; LastAsked.Clear(); }
    }
}
