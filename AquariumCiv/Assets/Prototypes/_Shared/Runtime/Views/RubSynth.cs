using UnityEngine;

namespace AquariumCiv.Prototypes
{
    /// <summary>
    /// Live finger-on-glass sound, built from tiny grains on the audio thread so speed and dirt shape it as you wipe.
    /// Grimy glass crunches: a stream of filtered-noise crackles over a low cloth rub, denser the faster you go.
    /// Clean glass squeaks in short stick-slip chirps. One voice blends both by dirt, so they never stack as two loops.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class RubSynth : MonoBehaviour
    {
        struct Grain
        {
            public bool on, squeak;
            public int left, len;
            public float amp, f, q, low, band, phase, freq, glide;
        }

        const int MaxGrains = 48;
        readonly Grain[] grains = new Grain[MaxGrains];
        float sr = 48000f;
        uint seed = 0x9E3779B9u;

        // Written by the main thread each frame, read by the audio thread.
        volatile float speedTarget, grimeTarget, gain;
        float speed, grime, crunchWait, squeakWait, rubLp, rubLp2;

        /// <summary>Main thread: current finger speed (0..1), grime under the finger (0..1), master volume.</summary>
        public void Set(float speed01, float grime01, float volume)
        {
            speedTarget = speed01;
            grimeTarget = grime01;
            gain = volume;
        }

        void Awake()
        {
            sr = AudioSettings.outputSampleRate;
            var src = GetComponent<AudioSource>();
            src.clip = AudioClip.Create("Silence", 1024, 1, (int)sr, false); // the filter needs a playing source
            src.loop = true;
            src.spatialBlend = 0;
            src.Play();
        }

        float Rand() // 0..1, xorshift: no allocations or locks on the audio thread
        {
            seed ^= seed << 13;
            seed ^= seed >> 17;
            seed ^= seed << 5;
            return (seed & 0xFFFFFF) / 16777216f;
        }

        float Noise() => Rand() * 2f - 1f;

        void Spawn(bool squeak, float grit, float spd)
        {
            for (int i = 0; i < MaxGrains; i++)
            {
                if (grains[i].on) continue;
                ref var g = ref grains[i];
                g.on = true;
                g.squeak = squeak;
                g.low = g.band = g.phase = 0;
                float r = Rand();
                if (squeak)
                {
                    // Rubber on clean glass: a short rising "eek", pitched by speed.
                    g.len = (int)(sr * (0.045f + 0.09f * r));
                    g.freq = Mathf.Lerp(420f, 1050f, spd) * (0.9f + 0.2f * Rand());
                    g.glide = 0.15f + 0.2f * Rand();
                    g.f = 2f * Mathf.Sin(Mathf.PI * Mathf.Min(g.freq * (2f + g.glide * 0.5f), 6000f) / sr); // on the 2nd harmonic, mid-glide
                    g.q = 0.5f;
                    g.amp = 1.1f + 0.6f * spd; // a band-pass on one harmonic passes little energy: more gain than the crunch
                }
                else
                {
                    // Grit under cloth: a 2-12 ms crackle. Dirtier glass is lower and chunkier; a few grains hit hard.
                    g.len = (int)(sr * (0.002f + 0.01f * r * r));
                    float fc = Mathf.Lerp(1100f, 5200f, Rand()) * (1f - 0.35f * grit);
                    g.f = 2f * Mathf.Sin(Mathf.PI * fc / sr);
                    g.q = 1f / Mathf.Lerp(1.5f, 6f, Rand());
                    float hit = Rand();
                    g.amp = (0.25f + 0.75f * hit * hit * hit) * (0.55f + 0.45f * spd);
                }
                g.left = g.len;
                return;
            }
        }

        void OnAudioFilterRead(float[] data, int channels)
        {
            float st = speedTarget, gt = grimeTarget, vol = gain;
            float smooth = 1f - Mathf.Exp(-1f / (0.02f * sr)); // ~20 ms glide, so lifting the finger isn't a click
            for (int n = 0; n < data.Length; n += channels)
            {
                speed += (st - speed) * smooth;
                grime += (gt - grime) * smooth;
                float grit = Mathf.Clamp01(grime * 1.5f);
                float clean = Mathf.Clamp01(1f - grime * 3f);

                // Poisson triggers whose rate (per second) follows speed live: the wait is counted in events, not
                // seconds, so a grain spawned during a slow moment doesn't stall the next fast stroke.
                float crunchRate = Mathf.Pow(speed, 0.8f) * grit * 240f;
                float squeakRate = speed > 0.15f ? speed * clean * 9f : 0f;
                if (crunchRate > 0f && (crunchWait -= crunchRate / sr) <= 0f)
                {
                    Spawn(false, grit, speed);
                    crunchWait = -Mathf.Log(Mathf.Max(Rand(), 1e-4f));
                }
                if (squeakRate > 0f && (squeakWait -= squeakRate / sr) <= 0f)
                {
                    Spawn(true, grit, speed);
                    squeakWait = -Mathf.Log(Mathf.Max(Rand(), 1e-4f)) + 0.35f;
                }

                // Cloth bed under the crunch: band-limited noise, only on grime.
                float w = Noise();
                rubLp += (w - rubLp) * 0.12f;
                rubLp2 += (rubLp - rubLp2) * 0.02f;
                float s = (rubLp - rubLp2) * speed * grit * 0.5f;

                for (int i = 0; i < MaxGrains; i++)
                {
                    ref var g = ref grains[i];
                    if (!g.on) continue;
                    float x = 1f - g.left / (float)g.len; // 0 -> 1 over the grain
                    float input, env;
                    if (g.squeak)
                    {
                        // Stick-slip: a narrow pulse train with jitter, rung through a band-pass: a rubbery squeal.
                        g.phase += g.freq * (1f + g.glide * x) * (1f + 0.03f * Noise()) / sr;
                        if (g.phase >= 1f) g.phase -= 1f;
                        input = (g.phase < 0.1f ? 1f : -0.11f) + 0.05f * Noise();
                        env = Mathf.Sin(Mathf.PI * Mathf.Sqrt(x));
                    }
                    else
                    {
                        input = Noise();
                        float a = Mathf.Min(1f, g.left > 0 ? (g.len - g.left) / (0.0004f * sr) : 1f);
                        float d = 1f - x;
                        env = a * d * d;
                    }
                    // Chamberlin state-variable band-pass.
                    g.low += g.f * g.band;
                    float high = input - g.low - g.q * g.band;
                    g.band += g.f * high;
                    s += g.band * env * g.amp;
                    if (--g.left <= 0) g.on = false;
                }

                float o = (float)System.Math.Tanh(s * 1.8f) * vol * 0.9f; // gentle saturation: juicier, never clips
                for (int c = 0; c < channels; c++) data[n + c] += o;
            }
        }
    }
}
