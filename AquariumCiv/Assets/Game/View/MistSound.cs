using AquariumCiv.Audio;
using UnityEngine;

namespace AquariumCiv.View
{
    /// <summary>
    /// The mister's "fsss" (#266): a soft looping spray, made in code like the wipe rub (<see cref="CareSound"/>), so there is no
    /// recording to license. It fades in over a tenth of a second when the spray starts and out over a third when the finger
    /// lifts, and follows the Care bus (<see cref="Sfx.Gain"/>: Sound off and the SFX volume apply). Logged in docs/assets-log.md.
    /// </summary>
    public class MistSound : MonoBehaviour
    {
        /// <summary>The recorded loop (CC0, #280); <see cref="Generate"/> is the fallback if it is ever missing.</summary>
        public const string RecordedPath = "Ambience/mist_spray_loop";
        public const int Rate = 24000;
        public const float LoopSeconds = 2.4f;
        /// <summary>The level at full spray before the bus gain (soft: it is heard hundreds of times).</summary>
        public const float Peak = 0.32f;
        /// <summary>The recorded loop is quieter (about -24 dBFS RMS against the code-made loop's -11.5), so it plays at full.</summary>
        public const float RecordedPeak = 1f;
        /// <summary>True: the recorded CC0 spray (the A/B pick, #280); false: the code-made one. A debug toggle flips it to compare by ear.</summary>
        public static bool UseRecording = true;
        bool usingRecording;

        AudioSource src;
        float level, target;

        /// <summary>The spray's level now, 0..1, before the bus gain (tests).</summary>
        public float Level => level;
        public bool Audible => src != null && src.isPlaying && src.volume > 0.001f;
        public AudioClip Clip => src != null ? src.clip : null;

        /// <summary>1 while the spray is held (louder with a wider spray, 0.6..1), 0 to fade out.</summary>
        public void SetSpray(float amount01) => target = Mathf.Clamp01(amount01);

        void Awake()
        {
            var go = new GameObject("MistSpray") { hideFlags = HideFlags.DontSave };
            go.transform.SetParent(transform, false);
            src = go.AddComponent<AudioSource>();
            src.playOnAwake = false; src.loop = true; src.spatialBlend = 0f;
            src.clip = MakeClip(out usingRecording);
            src.volume = 0f;
            src.Play(); // #296: the clip goes to the mixer now, silent (Update pauses it next frame), not on the first spray
        }

        void Update() => Tick(Time.unscaledDeltaTime);

        /// <summary>The source's volume now (tests).</summary>
        public float Volume => src != null ? src.volume : 0f;

        /// <summary>Advances the fade by dt seconds (Update calls it; tests step it).</summary>
        public void Tick(float dt)
        {
            float rate = target > level ? 10f : 3f; // in over ~0.1 s, out over ~0.33 s
            level = Mathf.MoveTowards(level, target, dt * rate);
            src.volume = level * (usingRecording ? RecordedPeak : Peak) * Sfx.Gain(SoundBus.Care);
            if (level > 0.001f && !src.isPlaying) src.Play();
            else if (level <= 0.001f && src.isPlaying) src.Pause();
        }

        /// <summary>The spray as samples: white noise through a high-pass at about 2.2 kHz and a low-pass at about 7 kHz (a band
        /// of air, no tone), a slow swell (two sines that make whole cycles over the loop) and a few soft breaths, then the end
        /// cross-faded into the start so the loop has no seam.</summary>
        public static float[] Generate(int rate = Rate, float seconds = LoopSeconds, int seed = 266)
        {
            int n = Mathf.RoundToInt(rate * seconds), fade = Mathf.RoundToInt(rate * 0.25f);
            var raw = new float[n + fade];
            var rng = new System.Random(seed);
            // one-pole filters: hp ~ 2.2 kHz, lp ~ 7 kHz
            float aHp = Mathf.Exp(-2f * Mathf.PI * 2200f / rate), aLp = 1f - Mathf.Exp(-2f * Mathf.PI * 7000f / rate);
            float lp = 0f, hpIn = 0f, hpOut = 0f, lp2 = 0f;
            for (int i = 0; i < raw.Length; i++)
            {
                float w = (float)(rng.NextDouble() * 2.0 - 1.0);
                lp += aLp * (w - lp);
                float hp = aHp * (hpOut + lp - hpIn);
                hpIn = lp; hpOut = hp;
                lp2 += 0.7f * (hp - lp2);
                raw[i] = lp2;
            }
            var d = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)n;
                float swell = 0.82f + 0.12f * Mathf.Sin(t * Mathf.PI * 2f * 2f) + 0.06f * Mathf.Sin(t * Mathf.PI * 2f * 5f + 1.3f);
                float v = raw[i];
                if (i < fade) // the start fades in against the tail we generated past the end, so the wrap has no seam
                {
                    float k = i / (float)fade;
                    v = raw[i] * Mathf.Sin(k * Mathf.PI * 0.5f) + raw[n + i] * Mathf.Cos(k * Mathf.PI * 0.5f);
                }
                d[i] = v * swell * 2.4f;
            }
            float peak = 0f;
            foreach (float s in d) peak = Mathf.Max(peak, Mathf.Abs(s));
            if (peak > 0f) for (int i = 0; i < n; i++) d[i] = d[i] / peak * 0.9f;
            return d;
        }

        /// <summary>Debug: swaps between the recorded and the code-made spray (A/B by ear); returns which one plays now.</summary>
        public string ToggleSource()
        {
            UseRecording = !UseRecording;
            bool was = src.isPlaying;
            src.clip = MakeClip(out usingRecording);
            if (was) src.Play();
            return usingRecording ? "mist: recorded" : "mist: code-made";
        }

        static AudioClip MakeClip(out bool recorded)
        {
            var rec = UseRecording ? Resources.Load<AudioClip>(RecordedPath) : null;
            recorded = rec != null;
            if (rec != null) return rec;
            var d = Generate();
            var c = AudioClip.Create("MistSpray", d.Length, 1, Rate, false);
            c.SetData(d, 0);
            return c;
        }
    }
}
