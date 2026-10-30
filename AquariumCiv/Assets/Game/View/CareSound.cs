using UnityEngine;

namespace AquariumCiv.View
{
    /// <summary>
    /// The care layer's sounds, made in code so no audio asset is imported (placeholder until the sound pass): a looped
    /// cloth-on-glass rub driven by finger speed and dirt, a three-note chime (a clean pane, a full clean), a flick swish
    /// and a soft pop (a wobble, a heap going). Everything respects the Sound switch through <c>AudioListener.volume</c>.
    /// </summary>
    public class CareSound : MonoBehaviour
    {
        AudioSource rub, shot;
        AudioClip chime, swish, pop;
        float rubVol, rubTarget;

        const int Rate = 24000;

        void Awake()
        {
            rub = Source("CareRub", true);
            rub.clip = MakeRub();
            rub.volume = 0f;
            shot = Source("CareShot", false);
            chime = MakeChime();
            swish = MakeSwish();
            pop = MakePop();
        }

        AudioSource Source(string name, bool loop)
        {
            var go = new GameObject(name) { hideFlags = HideFlags.DontSave };
            go.transform.SetParent(transform, false);
            var s = go.AddComponent<AudioSource>();
            s.playOnAwake = false; s.loop = loop; s.spatialBlend = 0f;
            return s;
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
            rub.volume = rubVol;
            if (rubVol > 0.001f && !rub.isPlaying) rub.Play();
            else if (rubVol <= 0.001f && rub.isPlaying) rub.Pause();
        }

        public void Chime(float volume = 0.7f) => shot.PlayOneShot(chime, volume);
        public void Swish(float pitchUp = 0f) { shot.pitch = 1f + pitchUp * 0.25f; shot.PlayOneShot(swish, 0.8f); }
        public void Pop() { shot.pitch = 1f; shot.PlayOneShot(pop, 0.7f); }

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
