using UnityEngine;

namespace AquariumCiv.Prototypes
{
    /// <summary>
    /// Shared feedback for cleaning: synthesized sounds (no audio assets yet) and glint particles.
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
        AudioSource oneShot, swish, squeak;
        AudioClip pop, chime, whoosh;
        ParticleSystem glints;
        float swishTarget, squeakTarget;

        void Awake()
        {
            instance = this;
            if (FindAnyObjectByType<AudioListener>() == null && Camera.main != null) Camera.main.gameObject.AddComponent<AudioListener>();
            oneShot = gameObject.AddComponent<AudioSource>();
            swish = Loop(MakeSwish());
            squeak = Loop(MakeSqueak());
            pop = MakePop();
            chime = MakeChime();
            whoosh = MakeWhoosh();
            if (particleMaterial != null)
            {
                SoftMaterial = new Material(particleMaterial) { name = "Soft (runtime)" };
                SoftMaterial.SetTexture("_BaseMap", MakeSprite(64, false));
                particleMaterial = new Material(particleMaterial) { name = "Sparkle (runtime)" };
                particleMaterial.SetTexture("_BaseMap", MakeSprite(64, true));
            }
            glints = MakeGlints();
        }

        AudioSource Loop(AudioClip clip)
        {
            var s = gameObject.AddComponent<AudioSource>();
            s.clip = clip;
            s.loop = true;
            s.volume = 0;
            s.Play();
            return s;
        }

        void Update()
        {
            float k = 1f - Mathf.Exp(-18f * Time.unscaledDeltaTime);
            swish.volume = Mathf.Lerp(swish.volume, swishTarget * volume, k);
            squeak.volume = Mathf.Lerp(squeak.volume, squeakTarget * volume, k);
            swishTarget = squeakTarget = 0; // callers refresh every frame while wiping
        }

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
            instance.oneShot.pitch = pitch * Random.Range(0.93f, 1.07f);
            instance.oneShot.PlayOneShot(instance.pop, instance.volume);
        }

        public static void Chime()
        {
            if (instance == null) return;
            instance.oneShot.pitch = 1f;
            instance.oneShot.PlayOneShot(instance.chime, instance.volume * 0.7f);
        }

        public static void Whoosh()
        {
            if (instance == null) return;
            instance.oneShot.pitch = Random.Range(0.9f, 1.15f);
            instance.oneShot.PlayOneShot(instance.whoosh, instance.volume * 0.8f);
        }

        /// <summary>Call every frame while a finger rubs the glass. Dirty glass swishes; clean glass squeaks.</summary>
        public static void Rub(float speed01, float grimeUnder)
        {
            if (instance == null) return;
            instance.swishTarget = Mathf.Max(instance.swishTarget, speed01 * Mathf.Clamp01(grimeUnder * 1.5f));
            instance.squeakTarget = Mathf.Max(instance.squeakTarget, speed01 * Mathf.Clamp01(1f - grimeUnder * 3f) * 0.5f);
            instance.squeak.pitch = 0.85f + 0.35f * speed01;
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

        /// <summary>Cloth on grimy glass: soft band-limited noise with a slow wobble. Loops.</summary>
        static AudioClip MakeSwish()
        {
            int n = Rate; // 1 s loop
            var d = new float[n];
            var rng = new System.Random(3);
            float lp = 0, lp2 = 0;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)n;
                lp += ((float)(rng.NextDouble() * 2 - 1) - lp) * 0.22f;
                lp2 += (lp - lp2) * 0.5f;
                d[i] = (lp - lp2 * 0.6f) * (0.75f + 0.25f * Mathf.Sin(t * Mathf.PI * 2 * 6)) * 0.8f;
            }
            return Clip("Swish", d);
        }

        /// <summary>Rubber on clean glass: a wobbling tone. Integer cycles per loop so it repeats without a click.</summary>
        static AudioClip MakeSqueak()
        {
            int n = Rate / 2; // 0.5 s loop
            var d = new float[n];
            const float f = 1100f, vib = 22f; // 550 and 11 whole cycles per loop
            double phase = 0;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float inst = f * (1f + 0.06f * Mathf.Sin(2 * Mathf.PI * vib * t));
                phase += 2 * Mathf.PI * inst / Rate;
                float s = Mathf.Sin((float)phase) * 0.5f + Mathf.Sin((float)phase * 2f) * 0.2f + Mathf.Sin((float)phase * 3f) * 0.1f;
                d[i] = s * 0.35f;
            }
            return Clip("Squeak", d);
        }
    }
}
