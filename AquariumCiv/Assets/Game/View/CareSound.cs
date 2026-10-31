using AquariumCiv.Audio;
using UnityEngine;

namespace AquariumCiv.View
{
    /// <summary>
    /// The wipe rub (#37): a looped cloth-on-glass noise driven by finger speed and dirt, made in code. Its level follows
    /// the Care bus (#96: <see cref="Sfx.Gain"/>, so Sound off and the SFX volume apply). The other care sounds are cues
    /// (<see cref="CueLibrary"/>) the AudioDirector plays; the ones still made in code come from <see cref="MakeProc"/>
    /// (<c>proc:swish</c>, <c>proc:pop</c>, <c>proc:chime</c>, <c>proc:cheer</c>).
    /// </summary>
    public class CareSound : MonoBehaviour
    {
        AudioSource rub;
        float rubVol, rubTarget;

        const int Rate = 24000;

        /// <summary>The rub's level now, before the bus gain (tests).</summary>
        public float RubLevel => rubVol;

        void Awake()
        {
            var go = new GameObject("CareRub") { hideFlags = HideFlags.DontSave };
            go.transform.SetParent(transform, false);
            rub = go.AddComponent<AudioSource>();
            rub.playOnAwake = false; rub.loop = true; rub.spatialBlend = 0f;
            rub.clip = MakeRub();
            rub.volume = 0f;
        }

        /// <summary>Finger speed 0..1 and dirt under it 0..1: louder and higher when fast on dirty glass; 0 stops the rub.</summary>
        public void SetRub(float speed01, float dirt01)
        {
            rubTarget = speed01 <= 0.01f ? 0f : Mathf.Lerp(0.04f, 0.5f, speed01) * Mathf.Lerp(0.25f, 1f, dirt01);
            rub.pitch = Mathf.Lerp(0.85f, 1.35f, speed01);
        }

        void Update()
        {
            rubVol = Mathf.MoveTowards(rubVol, rubTarget, Time.unscaledDeltaTime * 4f);
            rub.volume = rubVol * Sfx.Gain(SoundBus.Care);
            if (rubVol > 0.001f && !rub.isPlaying) rub.Play();
            else if (rubVol <= 0.001f && rub.isPlaying) rub.Pause();
        }

        /// <summary>A sound made in code, by its <c>proc:</c> name (null if unknown).</summary>
        public static AudioClip MakeProc(string name) => name switch
        {
            "rub" => MakeRub(),
            "chime" => MakeChime(),
            "swish" => MakeSwish(),
            "pop" => MakePop(),
            "cheer" => MakeCheer(),
            "flies" => MakeFlies(),
            _ => null,
        };

        /// <summary>The mess ambience (#96): three flies circling, an 8 s seamless loop. Each fly is a buzzy tone (the
        /// fundamental plus odd partials) whose pitch wanders and whose level swells as it passes; every frequency and
        /// wobble makes whole cycles over the loop, so the end meets the start.</summary>
        static AudioClip MakeFlies()
        {
            const float L = 8f;
            int n = (int)(Rate * L);
            var d = new float[n];
            var flies = new (float f0, float dev, int wob, int pass, float phase)[] { (184f, 9f, 5, 2, 0f), (212f, 12f, 7, 3, 1.7f), (241f, 8f, 4, 1, 3.1f) };
            foreach (var fly in flies)
            {
                float f0 = Mathf.Round(fly.f0 * L) / L; // whole cycles in the loop
                double ph = 0;
                for (int i = 0; i < n; i++)
                {
                    float t = i / (float)Rate;
                    float f = f0 + fly.dev * Mathf.Sin(2f * Mathf.PI * fly.wob * t / L + fly.phase);
                    ph += 2.0 * System.Math.PI * f / Rate;
                    float s = (float)(System.Math.Sin(ph) + 0.33 * System.Math.Sin(3 * ph) + 0.2 * System.Math.Sin(5 * ph) + 0.14 * System.Math.Sin(7 * ph));
                    float near = 0.5f + 0.5f * Mathf.Sin(2f * Mathf.PI * fly.pass * t / L + fly.phase * 2f); // passing close, then away
                    d[i] += s * (0.04f + 0.12f * near * near);
                }
            }
            return Clip("CareFlies", d);
        }

        /// <summary>A placeholder cheer (no CC0 crowd cheer found yet): a dozen small voices rising "hey!" at slightly
        /// different times and pitches, with a little breath noise.</summary>
        static AudioClip MakeCheer()
        {
            int n = (int)(Rate * 1.3f);
            var d = new float[n];
            var rng = new System.Random(96);
            for (int v = 0; v < 12; v++)
            {
                float f0 = 330f + (float)rng.NextDouble() * 330f, onset = (float)rng.NextDouble() * 0.18f;
                float len = 0.45f + (float)rng.NextDouble() * 0.5f, vib = 5f + (float)rng.NextDouble() * 2f, ph = 0f;
                for (int i = (int)(onset * Rate); i < n; i++)
                {
                    float t = i / (float)Rate - onset;
                    if (t > len) break;
                    float f = f0 * (1f + 0.35f * Mathf.Min(1f, t / 0.12f)) * (1f + 0.012f * Mathf.Sin(2f * Mathf.PI * vib * t));
                    ph += 2f * Mathf.PI * f / Rate;
                    float env = Mathf.Min(1f, t / 0.03f) * Mathf.Exp(-t * 2.5f) * (1f - t / len);
                    // a soft vowel: the fundamental and two quieter partials
                    d[i] += (Mathf.Sin(ph) + 0.4f * Mathf.Sin(2f * ph) + 0.15f * Mathf.Sin(3f * ph)) * env * 0.07f;
                }
            }
            float lp = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                lp += (((float)rng.NextDouble() * 2f - 1f) - lp) * 0.2f;
                d[i] += lp * 0.05f * Mathf.Exp(-t * 3f);
            }
            return Clip("CareCheer", d);
        }

        // ---------- clips ----------
        static AudioClip MakeRub()
        {
            int n = Rate; // 1 s loop of low-passed noise with a slow wobble
            var d = new float[n];
            var rng = new System.Random(11);
            float lp = 0f, lp2 = 0f;
            for (int i = 0; i < n; i++)
            {
                float w = (float)rng.NextDouble() * 2f - 1f;
                lp += (w - lp) * 0.35f; lp2 += (lp - lp2) * 0.5f;
                float wob = 0.75f + 0.25f * Mathf.Sin(i / (float)n * Mathf.PI * 2f * 7f);
                d[i] = (lp - lp2) * 3.2f * wob;
            }
            return Clip("CareRub", d);
        }

        static AudioClip MakeChime()
        {
            float[] notes = { 784f, 988f, 1175f };
            int n = (int)(Rate * 1.1f);
            var d = new float[n];
            for (int k = 0; k < notes.Length; k++)
            {
                int start = (int)(Rate * 0.11f * k);
                for (int i = start; i < n; i++)
                {
                    float t = (i - start) / (float)Rate;
                    d[i] += Mathf.Sin(2f * Mathf.PI * notes[k] * t) * Mathf.Exp(-t * 4.5f) * 0.28f
                          + Mathf.Sin(2f * Mathf.PI * notes[k] * 2.01f * t) * Mathf.Exp(-t * 9f) * 0.08f;
                }
            }
            return Clip("CareChime", d);
        }

        static AudioClip MakeSwish()
        {
            int n = (int)(Rate * 0.28f);
            var d = new float[n];
            var rng = new System.Random(5);
            float lp = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)n;
                float k = Mathf.Lerp(0.5f, 0.08f, t); // the pass-by falls in pitch
                lp += (((float)rng.NextDouble() * 2f - 1f) - lp) * k;
                d[i] = lp * Mathf.Sin(t * Mathf.PI) * 1.6f;
            }
            return Clip("CareSwish", d);
        }

        static AudioClip MakePop()
        {
            int n = (int)(Rate * 0.12f);
            var d = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                d[i] = Mathf.Sin(2f * Mathf.PI * (260f - 900f * t) * t) * Mathf.Exp(-t * 38f) * 0.8f;
            }
            return Clip("CarePop", d);
        }

        static AudioClip Clip(string name, float[] data)
        {
            var c = AudioClip.Create(name, data.Length, 1, Rate, false);
            c.SetData(data, 0);
            return c;
        }
    }
}
