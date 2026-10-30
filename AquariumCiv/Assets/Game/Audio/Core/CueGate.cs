using System;
using System.Collections.Generic;

namespace AquariumCiv.Audio
{
    /// <summary>
    /// Decides whether a cue plays now, and how (#96): the cooldown, the cue's voice limit (drop or steal the oldest),
    /// the whole mix's voice limit (a lower-priority voice gives way), a random clip that isn't the last one, and the
    /// pitch/volume jitter. Pure: the view passes its own clock (unscaled seconds) and plays what comes back.
    /// </summary>
    public class CueGate
    {
        /// <summary>One-shot voices sounding at once across the whole mix (mobile: keep it small) [est].</summary>
        public const int MaxTotalVoices = 12;

        readonly Func<string, CueDef> lookup;
        readonly Random rng;
        readonly Dictionary<string, float> lastPlay = new();
        readonly Dictionary<string, int> lastClip = new();
        readonly List<Voice> voices = new();
        int nextVoice = 1;

        struct Voice { public int id; public CueDef cue; public float start, end; }

        public CueGate(int seed = 96, Func<string, CueDef> lookup = null)
        {
            rng = new Random(seed);
            this.lookup = lookup ?? CueLibrary.Get;
        }

        /// <summary>Voices still sounding at <paramref name="now"/> (all cues, or one cue).</summary>
        public int Active(float now, string cueId = null)
        {
            int n = 0;
            foreach (var v in voices) if (v.end > now && (cueId == null || v.cue.id == cueId)) n++;
            return n;
        }

        /// <summary>Any cue that ducks the music still sounding at <paramref name="now"/> (feed <see cref="Ducker"/>).</summary>
        public bool AnyDucking(float now)
        {
            foreach (var v in voices) if (v.end > now && v.cue.ducksMusic) return true;
            return false;
        }

        /// <summary>
        /// Asks to play <paramref name="cueId"/> at <paramref name="now"/>. <paramref name="semitones"/> shifts the pitch
        /// (the wave's rising knocks); <paramref name="volumeScale"/> scales the cue's volume (a soft chime on one pane,
        /// a full one on the whole box). False when the cue is unknown, cooling down, or out of voices.
        /// </summary>
        public bool TryPlay(string cueId, float now, out PlayRequest play, float semitones = 0f, float volumeScale = 1f)
        {
            play = default;
            var cue = lookup(cueId);
            if (cue == null || cue.clips.Count == 0) return false;
            voices.RemoveAll(v => v.end <= now);

            if (cue.cooldown > 0f && lastPlay.TryGetValue(cue.id, out float last) && now - last < cue.cooldown) return false;

            int stolen = -1;
            int mine = Active(now, cue.id);
            if (mine >= Math.Max(1, cue.maxVoices))
            {
                if (!cue.steal) return false;
                stolen = Oldest(cue.id);
                voices.RemoveAll(v => v.id == stolen);
            }
            else if (voices.Count >= MaxTotalVoices)
            {
                int lowest = Lowest();
                if (lowest < 0 || Find(lowest).cue.priority > cue.priority) return false; // ties: the new one wins
                stolen = lowest;
                voices.RemoveAll(v => v.id == stolen);
            }

            string clip = PickClip(cue);
            float pitch = Pitch.FromSemitones(semitones + Spread(cue.pitchJitter));
            float vol = Clamp01(cue.volume * volumeScale * (1f + Spread(cue.volumeJitter)));
            int id = nextVoice++;
            // A loop holds its voice until the view calls Stop; a one-shot frees it when the clip ends.
            float end = cue.loop ? float.MaxValue : now + Math.Max(0.05f, cue.length / pitch);
            voices.Add(new Voice { id = id, cue = cue, start = now, end = end });
            lastPlay[cue.id] = now;
            play = new PlayRequest(cue, clip, vol, pitch, cue.haptic, id, stolen);
            return true;
        }

        /// <summary>The view stopped a voice early (a loop faded out, a panel closed mid-sound).</summary>
        public void Stop(int voice) => voices.RemoveAll(v => v.id == voice);

        /// <summary>Forget every voice and cooldown (a scene reload, tests).</summary>
        public void Reset() { voices.Clear(); lastPlay.Clear(); lastClip.Clear(); }

        string PickClip(CueDef cue)
        {
            int n = cue.clips.Count, i = 0;
            if (n > 1)
            {
                int prev = lastClip.TryGetValue(cue.id, out int p) ? p : -1;
                i = rng.Next(prev >= 0 ? n - 1 : n);
                if (prev >= 0 && i >= prev) i++; // never the same clip twice in a row
            }
            lastClip[cue.id] = i;
            return cue.clips[i];
        }

        float Spread(float amount) => amount <= 0f ? 0f : ((float)rng.NextDouble() * 2f - 1f) * amount;

        int Oldest(string cueId)
        {
            int id = -1; float t = float.MaxValue;
            foreach (var v in voices) if (v.cue.id == cueId && v.start < t) { t = v.start; id = v.id; }
            return id;
        }

        int Lowest()
        {
            int id = -1, prio = int.MaxValue; float t = float.MaxValue;
            foreach (var v in voices)
                if (v.cue.priority < prio || (v.cue.priority == prio && v.start < t)) { prio = v.cue.priority; t = v.start; id = v.id; }
            return id;
        }

        Voice Find(int id) => voices.Find(v => v.id == id);

        static float Clamp01(float x) => x < 0f ? 0f : x > 1f ? 1f : x;
    }
}
