using System;
using System.Collections.Generic;

namespace AquariumCiv.Audio
{
    /// <summary>
    /// Where a sound goes in the mix (#96). Each bus is one mixer group in phase 2; its volume comes from the settings
    /// (<see cref="MixLevels"/>) and the ducking rules (<see cref="Ducker"/>). UI sounds stay apart from world sounds
    /// (game-design-practices §10: a consistent audio language, UI apart from world).
    /// </summary>
    public enum SoundBus
    {
        Ui,        // HUD taps, panels, cards, the book
        Care,      // the keeper's own hands on the glass: wipe, flick, prune, rain, shape
        World,     // the people and the box: knocks, bells, cheers (outside a moment)
        Moment,    // era-moment cues (flute, fiddle, the chord): they duck the music
        Ambience,  // the calm loop, birds, crickets, rain, wind
        Music,     // background music (#72, MusicDirector)
    }

    /// <summary>A phone buzz (#96). Mapped per platform in <see cref="HapticMap"/>; off with <c>GameSettings.Haptics</c>.</summary>
    public enum HapticKind
    {
        None,
        Selection, // the lightest tick: a toggle, a scroll stop, a lot rebuilt in the wave
        Light,     // a tap on the glass, a person, a card
        Medium,    // a flick that throws mess out, a shaping stroke landing
        Heavy,     // reserved (nothing heavy in a calm game for now)
        Success,   // a full clean, the cheer
        Warning,   // a shaping stroke that can't happen (no charge, locked): soft, never alarming
    }

    /// <summary>
    /// One sound event as data: which clips (a random one each time, never the same twice in a row when there are
    /// several), how loud, how much the pitch and volume wander, how often it may play and how many at once, and which
    /// buzz goes with it. Clip paths are under <c>Assets/Audio/Resources/</c> (load with <c>Resources.Load</c>), or
    /// <c>proc:&lt;name&gt;</c> for a sound CareSound makes in code.
    /// </summary>
    [Serializable]
    public class CueDef
    {
        public string id;
        public SoundBus bus = SoundBus.World;
        public List<string> clips = new();
        /// <summary>0..1, before the bus volume.</summary>
        public float volume = 0.7f;
        /// <summary>± semitones of random pitch per play (0 = always the same pitch).</summary>
        public float pitchJitter;
        /// <summary>± share of random volume per play (0.1 = ±10%).</summary>
        public float volumeJitter;
        /// <summary>Seconds after a play during which the same cue is dropped (rapid taps don't machine-gun).</summary>
        public float cooldown;
        /// <summary>At most this many of this cue sounding at once; a new one past the limit is dropped (or steals the
        /// oldest when <see cref="steal"/>).</summary>
        public int maxVoices = 2;
        public bool steal;
        /// <summary>Higher wins when the whole mix is out of voices (<see cref="CueGate.MaxTotalVoices"/>).</summary>
        public int priority = 50;
        /// <summary>Roughly how long one play lasts (for voice counting); the view can pass the real clip length instead.</summary>
        public float length = 0.5f;
        /// <summary>The music (and ambience a little) dips under this cue (<see cref="Ducker"/>).</summary>
        public bool ducksMusic;
        public HapticKind haptic = HapticKind.None;
        /// <summary>A looped cue (the wipe rub, the festival tune): the view keeps one source running and sets its
        /// volume; the gate only says whether it may start.</summary>
        public bool loop;

        public CueDef() { }
        public CueDef(string id, SoundBus bus, float volume, params string[] clips)
        { this.id = id; this.bus = bus; this.volume = volume; this.clips.AddRange(clips); }

        public CueDef Jitter(float pitchSemitones, float volumeShare = 0.08f) { pitchJitter = pitchSemitones; volumeJitter = volumeShare; return this; }
        public CueDef Limit(float cooldownSeconds, int voices, bool stealOldest = false) { cooldown = cooldownSeconds; maxVoices = voices; steal = stealOldest; return this; }
        public CueDef Prio(int p) { priority = p; return this; }
        public CueDef Len(float seconds) { length = seconds; return this; }
        public CueDef Buzz(HapticKind h) { haptic = h; return this; }
        public CueDef Ducks() { ducksMusic = true; return this; }
        public CueDef Looped() { loop = true; return this; }
    }

    /// <summary>What the view plays: the clip, the volume (before the bus), the pitch (1 = as recorded) and the buzz.</summary>
    public readonly struct PlayRequest
    {
        public readonly CueDef cue;
        public readonly string clip;
        public readonly float volume, pitch;
        public readonly HapticKind haptic;
        /// <summary>A voice this play replaces (stop it), or -1.</summary>
        public readonly int stolenVoice;
        /// <summary>The voice id to pass to <see cref="CueGate.Stop"/> if the view stops the sound early.</summary>
        public readonly int voice;
        public PlayRequest(CueDef cue, string clip, float volume, float pitch, HapticKind haptic, int voice, int stolenVoice)
        { this.cue = cue; this.clip = clip; this.volume = volume; this.pitch = pitch; this.haptic = haptic; this.voice = voice; this.stolenVoice = stolenVoice; }
        public override string ToString() => $"{cue?.id} {clip} vol {volume:0.00} pitch {pitch:0.000}";
    }

    /// <summary>Semitones ↔ pitch ratio (Unity's <c>AudioSource.pitch</c> is a playback-rate ratio).</summary>
    public static class Pitch
    {
        public static float FromSemitones(float st) => (float)Math.Pow(2.0, st / 12.0);
    }
}
