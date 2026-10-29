using UnityEngine;

namespace AquariumCiv.Prototypes
{
    /// <summary>
    /// Shared feedback for cleaning: synthesized sounds (no audio assets yet) and glint particles.
    /// Wiping is a live grain synth (<see cref="RubSynth"/>); grabs, flicks and crunches are layered one-shots with variants.
    /// Every cleaning gesture uses the same palette, so A/B variants get the same juice budget.
    /// </summary>
    public class Juice : MonoBehaviour
    {
        const int Rate = 44100;

        public Material particleMaterial;
        [Range(0, 1)] public float volume = 0.8f;

        static Juice instance;
        /// <summary>Particle material with a soft round sprite (puffs). Glints use a four-point sparkle.</summary>
        public static Material SoftMaterial { get; private set; }
        // A small pool, so each one-shot keeps its own pitch (one shared source re-pitched sounds still playing).
        readonly AudioSource[] voices = new AudioSource[8];
        int nextVoice;
        RubSynth rub;
        AudioClip pop, chime, whoosh;
        AudioClip[] grab, flick, crunchTrash, crunchVine;
        ParticleSystem glints;
        float rubSpeed, rubGrime;

        void Awake()
        {
            instance = this;
            if (FindAnyObjectByType<AudioListener>() == null && Camera.main != null) Camera.main.gameObject.AddComponent<AudioListener>();
            for (int i = 0; i < voices.Length; i++)
            {
                voices[i] = gameObject.AddComponent<AudioSource>();
                voices[i].playOnAwake = false;
            }
            var rubGo = new GameObject("RubSynth"); // own object: its filter must not touch the one-shot voices
            rubGo.transform.SetParent(transform, false);
            rubGo.AddComponent<AudioSource>();
            rub = rubGo.AddComponent<RubSynth>();
            pop = MakePop();
            chime = MakeChime();
            whoosh = MakeWhoosh();
            grab = Variants(4, MakeGrab);
            flick = Variants(4, MakeFlick);
            crunchTrash = Variants(4, r => MakeCrunch(r, false));
            crunchVine = Variants(4, r => MakeCrunch(r, true));
            if (particleMaterial != null)
            {
                SoftMaterial = new Material(particleMaterial) { name = "Soft (runtime)" };
                SoftMaterial.SetTexture("_BaseMap", MakeSprite(64, false));
                particleMaterial = new Material(particleMaterial) { name = "Sparkle (runtime)" };
                particleMaterial.SetTexture("_BaseMap", MakeSprite(64, true));
            }
            glints = MakeGlints();
        }

        void Update()
        {
            rub.Set(rubSpeed, rubGrime, volume);
            rubSpeed = 0; // callers refresh every frame while wiping
        }

        static void Play(AudioClip clip, float pitch, float vol)
        {
            var v = instance.voices[instance.nextVoice];
            instance.nextVoice = (instance.nextVoice + 1) % instance.voices.Length;
            v.Stop();
            v.pitch = pitch;
            v.PlayOneShot(clip, vol * instance.volume);
        }

        static AudioClip Pick(AudioClip[] set) => set[Random.Range(0, set.Length)];

        // ---------- API ----------

        /// <summary>World size of the full screen height at this point, so effects and brushes stay finger-sized at any zoom.</summary>
        public static float WorldPerScreen(Vector3 at)
        {
            var cam = Camera.main;
            if (cam == null) return 1f;
            float dist = Vector3.Dot(at - cam.transform.position, cam.transform.forward);
            return 2f * Mathf.Max(dist, 0.01f) * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
        }

        public static void Pop(float pitch = 1f)
        {
            if (instance == null) return;
            Play(instance.pop, pitch * Random.Range(0.93f, 1.07f), 1f);
        }

        public static void Chime()
        {
            if (instance == null) return;
            Play(instance.chime, 1f, 0.7f);
        }

        /// <summary>Soft airy swoosh (page turns). Flicking mess uses <see cref="Flick"/>.</summary>
        public static void Whoosh()
        {
            if (instance == null) return;
            Play(instance.whoosh, Random.Range(0.9f, 1.15f), 0.8f);
        }

        /// <summary>A finger picks up a piece of mess: a small gritty pluck.</summary>
        public static void Grab()
        {
            if (instance == null) return;
            Play(Pick(instance.grab), Random.Range(0.92f, 1.1f), 0.8f);
        }

        /// <summary>A piece is thrown: a snappy rising "thwip". Faster flicks (0..1) pitch it up.</summary>
        public static void Flick(float strength01 = 0.5f)
        {
            if (instance == null) return;
            Play(Pick(instance.flick), Mathf.Lerp(0.9f, 1.2f, strength01) * Random.Range(0.95f, 1.05f), 0.9f);
        }

        /// <summary>Mess gone: a crunchy pop. Weeds snap leafy and low; junk crinkles.</summary>
        public static void Crunch(bool vine)
        {
            if (instance == null) return;
            Play(Pick(vine ? instance.crunchVine : instance.crunchTrash), Random.Range(0.9f, 1.1f), 1f);
        }

        /// <summary>Call every frame while a finger rubs the glass. Dirty glass crunches; clean glass squeaks.</summary>
        public static void Rub(float speed01, float grimeUnder)
        {
            if (instance == null) return;
            if (speed01 < instance.rubSpeed) return;
            instance.rubSpeed = speed01;
            instance.rubGrime = grimeUnder;
        }

        public static void Glints(Vector3 at, int count, float radius)
        {
            if (instance == null || count <= 0) return;
            float s = WorldPerScreen(at);
            var p = new ParticleSystem.EmitParams();
            for (int i = 0; i < count; i++)
            {
                p.position = at + Random.insideUnitSphere * radius;
                p.velocity = (Random.insideUnitSphere * 0.04f + Vector3.up * 0.025f) * s;
                p.startSize = Random.Range(0.006f, 0.014f) * s;
                p.startLifetime = Random.Range(0.3f, 0.6f);
                p.startColor = Color.Lerp(Color.white, new Color(0.75f, 0.95f, 1f), Random.value);
                instance.glints.Emit(p, 1);
            }
        }

        /// <summary>Coloured confetti bursting upward (citizens celebrating a spotless tank).</summary>
        public static void Confetti(Vector3 at, int count)
        {
            if (instance == null) return;
            float s = WorldPerScreen(at);
            var p = new ParticleSystem.EmitParams();
            for (int i = 0; i < count; i++)
            {
                p.position = at;
                p.velocity = (Random.insideUnitSphere * 0.06f + Vector3.up * 0.12f) * s;
                p.startSize = Random.Range(0.016f, 0.026f) * s;
                p.startLifetime = Random.Range(1.2f, 1.8f);
                p.startColor = Color.HSVToRGB(Random.value, 0.65f, 1f);
                instance.glints.Emit(p, 1);
            }
        }

        /// <summary>A ring of glints, for a tap that pops a patch clean.</summary>
        public static void GlintRing(Vector3 at, Vector3 normal, float radius, int count)
        {
            if (instance == null) return;
            var rot = Quaternion.LookRotation(normal);
            float s = WorldPerScreen(at);
            var p = new ParticleSystem.EmitParams();
            for (int i = 0; i < count; i++)
            {
                float a = i / (float)count * Mathf.PI * 2f;
                var dir = rot * new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0);
                p.position = at + dir * radius * 0.5f;
                p.velocity = dir * radius * 2.2f;
                p.startSize = Random.Range(0.008f, 0.014f) * s;
                p.startLifetime = 0.35f;
                p.startColor = Color.white;
                instance.glints.Emit(p, 1);
            }
        }

        // ---------- particles ----------

        ParticleSystem MakeGlints()
        {
            var go = new GameObject("Glints");
            go.transform.SetParent(transform);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = false;
            main.playOnAwake = false;
            main.maxParticles = 600;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.useUnscaledTime = true;
            var emission = ps.emission;
            emission.enabled = false;
            var shape = ps.shape;
            shape.enabled = false;
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0, 0.3f), new Keyframe(0.25f, 1f), new Keyframe(1, 0f)));
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                      new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(1, 0.6f), new GradientAlphaKey(0, 1) });
            col.color = g;
            var r = go.GetComponent<ParticleSystemRenderer>();
            if (particleMaterial != null) r.sharedMaterial = particleMaterial;
            ps.Play();
            return ps;
        }

        /// <summary>White sprite with alpha: a soft dot, or a dot with a four-point star for glints.</summary>
        static Texture2D MakeSprite(int size, bool star)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp, name = star ? "Sparkle" : "SoftDot" };
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f) / size * 2f - 1f, dy = (y + 0.5f) / size * 2f - 1f;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.Clamp01(1f - r);
                a *= a;
                if (star)
                {
                    float rays = Mathf.Clamp01(1f - Mathf.Abs(dx) * 12f) + Mathf.Clamp01(1f - Mathf.Abs(dy) * 12f);
                    a = Mathf.Clamp01(a * 0.8f + rays * Mathf.Clamp01(1f - r) * 0.9f);
                }
                px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
            tex.SetPixels32(px);
            tex.Apply(true);
            return tex;
        }

        // ---------- synthesized clips ----------

        static AudioClip Clip(string name, float[] data)
        {
            var c = AudioClip.Create(name, data.Length, 1, Rate, false);
            c.SetData(data, 0);
            return c;
        }

        /// <summary>Bubbly pop: a fast downward pitch sweep with a soft click.</summary>
        static AudioClip MakePop()
        {
            int n = (int)(Rate * 0.14f);
            var d = new float[n];
            double phase = 0;
            var rng = new System.Random(1);
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float f = Mathf.Lerp(950f, 260f, Mathf.Sqrt(t / 0.14f));
                phase += 2 * Mathf.PI * f / Rate;
                float env = Mathf.Exp(-t * 32f) * Mathf.Min(1f, t * 2000f);
                float click = t < 0.004f ? (float)(rng.NextDouble() * 2 - 1) * 0.3f : 0f;
                d[i] = (Mathf.Sin((float)phase) * 0.6f + click) * env;
            }
            return Clip("Pop", d);
        }

        /// <summary>Three bell partials, arpeggiated: the "sparkling clean" cue.</summary>
        static AudioClip MakeChime()
        {
            int n = (int)(Rate * 0.9f);
            var d = new float[n];
            float[] freqs = { 1318.5f, 1760f, 2637f };
            for (int k = 0; k < freqs.Length; k++)
            {
                int start = (int)(Rate * 0.07f * k);
                for (int i = start; i < n; i++)
                {
                    float t = (i - start) / (float)Rate;
                    float env = Mathf.Exp(-t * 5f) * Mathf.Min(1f, t * 400f);
                    d[i] += (Mathf.Sin(2 * Mathf.PI * freqs[k] * t) + 0.25f * Mathf.Sin(2 * Mathf.PI * freqs[k] * 2.01f * t)) * env * 0.22f;
                }
            }
            return Clip("Chime", d);
        }

        /// <summary>Airy flick: noise through a sweeping low-pass, fast attack, slow-ish release.</summary>
        static AudioClip MakeWhoosh()
        {
            int n = (int)(Rate * 0.28f);
            var d = new float[n];
            var rng = new System.Random(2);
            float lp = 0;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)n;
                float cutoff = Mathf.Lerp(0.05f, 0.35f, Mathf.Sin(t * Mathf.PI));
                lp += ((float)(rng.NextDouble() * 2 - 1) - lp) * cutoff;
                float env = Mathf.Sin(Mathf.Pow(t, 0.6f) * Mathf.PI);
                d[i] = lp * env * 0.9f;
            }
            return Clip("Whoosh", d);
        }

        // ---------- crunchy one-shots: layers of thump, snap, sweep and crackle grains ----------

        static AudioClip[] Variants(int count, System.Func<System.Random, AudioClip> make)
        {
            var set = new AudioClip[count];
            for (int i = 0; i < count; i++) set[i] = make(new System.Random(100 + i * 7919));
            return set;
        }

        static float Rnd(System.Random r, float lo, float hi) => lo + (float)r.NextDouble() * (hi - lo);

        /// <summary>Peak-normalise, then soft-saturate: loud and dense without clipping.</summary>
        static AudioClip Finish(string name, float[] d, float peak = 0.9f)
        {
            float max = 1e-5f;
            foreach (var x in d) max = Mathf.Max(max, Mathf.Abs(x));
            for (int i = 0; i < d.Length; i++) d[i] = (float)System.Math.Tanh(d[i] / max * 1.6f) / (float)System.Math.Tanh(1.6) * peak;
            return Clip(name, d);
        }

        /// <summary>A low sine thump with a quick pitch drop: the "body" under a hit.</summary>
        static void Thump(float[] d, float at, float f0, float dur, float amp)
        {
            int s = (int)(at * Rate), n = (int)(dur * Rate);
            double ph = 0;
            for (int i = 0; i < n && s + i < d.Length; i++)
            {
                float t = i / (float)Rate, x = i / (float)n;
                ph += 2 * Mathf.PI * f0 * (1f + 1.5f * Mathf.Exp(-t * 60f)) / Rate;
                d[s + i] += Mathf.Sin((float)ph) * amp * (1f - x) * (1f - x) * Mathf.Min(1f, t * 3000f);
            }
        }

        /// <summary>Crackle: <paramref name="count"/> short band-passed noise grains scattered over a window, thinning out.</summary>
        static void Crackle(float[] d, System.Random r, float at, float window, int count, float fLo, float fHi, float gLo, float gHi, float amp)
        {
            for (int k = 0; k < count; k++)
            {
                float u = (float)r.NextDouble();
                int s = (int)((at + window * u * u) * Rate); // denser at the start
                int n = (int)(Rnd(r, gLo, gHi) * Rate);
                float f = 2f * Mathf.Sin(Mathf.PI * Rnd(r, fLo, fHi) / Rate), q = 1f / Rnd(r, 1.5f, 6f);
                float hit = (float)r.NextDouble();
                float a = amp * (0.3f + 0.7f * hit * hit) * (1f - 0.6f * u);
                float low = 0, band = 0;
                for (int i = 0; i < n && s + i < d.Length; i++)
                {
                    float x = i / (float)n, env = Mathf.Min(1f, i / (0.0004f * Rate)) * (1f - x) * (1f - x);
                    low += f * band;
                    float high = (float)(r.NextDouble() * 2 - 1) - low - q * band;
                    band += f * high;
                    d[s + i] += band * env * a;
                }
            }
        }

        /// <summary>Picking a piece up: a tiny thump and a few grit grains.</summary>
        static AudioClip MakeGrab(System.Random r)
        {
            var d = new float[(int)(Rate * 0.09f)];
            Thump(d, 0, Rnd(r, 150f, 190f), 0.05f, 0.5f);
            Crackle(d, r, 0, 0.035f, 6, 1500f, 4500f, 0.002f, 0.008f, 1.2f);
            return Finish("Grab", d, 0.7f);
        }

        /// <summary>Throwing it: a sharp snap, then a band-passed noise sweep rising fast (thwip).</summary>
        static AudioClip MakeFlick(System.Random r)
        {
            int n = (int)(Rate * 0.16f);
            var d = new float[n];
            Crackle(d, r, 0, 0.004f, 3, 2500f, 5500f, 0.001f, 0.003f, 1.5f);
            float low = 0, band = 0, fFrom = Rnd(r, 500f, 800f), fTo = Rnd(r, 2800f, 4200f);
            for (int i = 0; i < n; i++)
            {
                float x = i / (float)n;
                float f = 2f * Mathf.Sin(Mathf.PI * Mathf.Lerp(fFrom, fTo, Mathf.Sqrt(x)) / Rate);
                low += f * band;
                float high = (float)(r.NextDouble() * 2 - 1) - low - 0.3f * band;
                band += f * high;
                d[i] += band * Mathf.Sin(Mathf.PI * Mathf.Pow(x, 0.45f)) * 0.9f;
            }
            return Finish("Flick", d, 0.8f);
        }

        /// <summary>Mess gone. Junk: a bubble pop, a thump and a crinkly crackle tail.
        /// Weeds: a woody snap and a lower, leafier crackle.</summary>
        static AudioClip MakeCrunch(System.Random r, bool vine)
        {
            var d = new float[(int)(Rate * 0.3f)];
            Thump(d, 0, vine ? Rnd(r, 95f, 120f) : Rnd(r, 80f, 100f), 0.08f, 0.9f);
            if (vine)
            {
                Crackle(d, r, 0, 0.006f, 4, 1800f, 4000f, 0.001f, 0.004f, 1.6f); // the snap
                Crackle(d, r, 0.008f, 0.2f, 16, 600f, 2600f, 0.004f, 0.018f, 1.0f);
            }
            else
            {
                double ph = 0;
                for (int i = 0; i < (int)(Rate * 0.07f); i++) // bubble: fast downward sweep
                {
                    float t = i / (float)Rate;
                    ph += 2 * Mathf.PI * Mathf.Lerp(1300f, 320f, Mathf.Sqrt(t / 0.07f)) / Rate;
                    d[i] += Mathf.Sin((float)ph) * 0.5f * Mathf.Exp(-t * 45f) * Mathf.Min(1f, t * 2000f);
                }
                Crackle(d, r, 0, 0.16f, 26, 1800f, 6000f, 0.002f, 0.009f, 1.0f);
            }
            return Finish(vine ? "CrunchVine" : "CrunchTrash", d);
        }
    }
}
