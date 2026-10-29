using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace AquariumCiv.Prototypes
{
    /// <summary>
    /// Shows the sim's mess as trash and overgrowth props (the glass is GlassGrime). Props clean on tap, or get
    /// flicked away by a swipe when tapCleansProps is off (P1 variant A).
    /// </summary>
    public class MessView : MonoBehaviour
    {
        public DioramaView diorama;
        public CrowdView crowd;
        public bool tapCleansProps = true;
        public GameObject[] trashPrefabs;
        public float trashScale = 0.55f;
        public GameObject[] vinePrefabs;
        public float vineScale = 0.6f;
        [Tooltip("Tap/flick hit sphere radius as a share of a building slot's radius.")] public float hitRadius = 0.55f;
        public int maxVines = 14;
        public Material puffMaterial;

        readonly Dictionary<int, GameObject> trash = new();
        readonly List<GameObject> vines = new();
        Vector3[] vineSpots;
        int vineSeed = 99, generation;

        /// <summary>A prop was tapped while taps don't clean it (so the gesture can hint at the right one).</summary>
        public event Action<TapToClean> PropTapRefused;
        /// <summary>Any cleaning happened (glass, prop, debug).</summary>
        public event Action Cleaned;

        float messySince = -1; // when the tank last became messy (≥ 0.15), for "time to full clean"; -1 = clean since

        void Start()
        {
            var rng = new System.Random(vineSeed);
            vineSpots = new Vector3[maxVines];
            for (int i = 0; i < maxVines; i++)
            {
                var slot = diorama.slotPositions[rng.Next(diorama.slotPositions.Length)];
                float a = (float)rng.NextDouble() * Mathf.PI * 2, d = (float)rng.NextDouble() * diorama.slotRadius;
                d = diorama.slotRadius * (1f + 0.2f * d / diorama.slotRadius); // hex rim: never inside a building
                vineSpots[i] = slot + new Vector3(Mathf.Cos(a) * d, 0, Mathf.Sin(a) * d);
            }
        }

        void Update()
        {
            var sim = SimClock.Instance.Sim;
            var s = sim.State;
            if (SimClock.Instance.Generation != generation)
            {
                generation = SimClock.Instance.Generation;
                foreach (var go in trash.Values) Destroy(go);
                trash.Clear();
            }
            if (sim.MessLevel >= 0.15f && messySince < 0) messySince = Time.unscaledTime;

            // Trash
            var alive = new HashSet<int>();
            foreach (var t in s.trash)
            {
                alive.Add(t.id);
                if (trash.ContainsKey(t.id)) continue;
                // On the hex rim, in the direction of the sim's offset, so trash never hides inside a building.
                var dir = new Vector3(t.offsetX, 0, t.offsetZ);
                dir = dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector3.forward;
                var pos = diorama.SlotPosition(t.slot) + dir * diorama.slotRadius * 1.1f;
                var go = Instantiate(trashPrefabs[t.id % trashPrefabs.Length], pos, Quaternion.Euler(0, t.id * 47 % 360, 0), transform);
                go.transform.localScale = Vector3.one * trashScale;
                go.name = $"Trash_{t.id}";
                DioramaView.AddBoundsCollider(go);
                AddHitArea(go);
                LogCategory.Tag(go, "trash", t.id.ToString());
                go.AddComponent<TapToClean>().Init(this, "trash", t.id);
                trash[t.id] = go;
            }
            foreach (var id in new List<int>(trash.Keys))
                if (!alive.Contains(id)) { Destroy(trash[id]); trash.Remove(id); }

            // Overgrowth
            int wantVines = Mathf.RoundToInt(s.overgrowth * maxVines);
            while (vines.Count < wantVines)
            {
                int i = vines.Count;
                var go = Instantiate(vinePrefabs[i % vinePrefabs.Length], vineSpots[i], Quaternion.Euler(0, i * 73 % 360, 0), transform);
                go.transform.localScale = Vector3.one * vineScale;
                go.name = $"Vine_{i}";
                DioramaView.AddBoundsCollider(go);
                AddHitArea(go);
                LogCategory.Tag(go, "vine", i.ToString());
                go.AddComponent<TapToClean>().Init(this, "vine", i);
                vines.Add(go);
            }
            while (vines.Count > wantVines) { Destroy(vines[^1]); vines.RemoveAt(vines.Count - 1); }
        }

        /// <summary>Small props get a generous invisible hit sphere (touch-target size at default zoom).</summary>
        void AddHitArea(GameObject go)
        {
            var hit = new GameObject("HitArea");
            hit.transform.SetParent(go.transform, false);
            var sphere = hit.AddComponent<SphereCollider>();
            float s = Mathf.Max(go.transform.lossyScale.x, 0.0001f);
            sphere.radius = diorama.slotRadius * hitRadius / s;
            sphere.center = Vector3.up * sphere.radius * 0.5f;
        }

        internal void Clean(string kind, int id, GameObject piece, Vector3 at, string gesture)
        {
            var sim = SimClock.Instance.Sim;
            float before = sim.MessLevel;
            if (kind == "trash") sim.RemoveTrash(id);
            else if (kind == "vine")
            {
                sim.Prune(1f / maxVines + 0.001f);
                vines.Remove(piece); // remove the tapped vine, not whichever is last
                Destroy(piece);
            }
            EventLog.Instance?.Clean(kind, 1, gesture, before);
            Puff(at);
            Juice.Crunch(kind == "vine");
            AfterClean(before);
        }

        internal void RefuseTap(TapToClean piece) => PropTapRefused?.Invoke(piece);

        /// <summary>Debug / P1 hook: wipe some grime off the glass.</summary>
        public void WipeGlass(float amount, string gesture)
        {
            var sim = SimClock.Instance.Sim;
            float before = sim.MessLevel, grimeBefore = sim.State.grime;
            sim.CleanGrime(amount);
            EventLog.Instance?.Clean("glass", grimeBefore - sim.State.grime, gesture, grimeBefore);
            AfterClean(before);
        }

        public void CleanEverything(string gesture)
        {
            var sim = SimClock.Instance.Sim;
            float before = sim.MessLevel;
            sim.CleanAll();
            EventLog.Instance?.Clean("all", before, gesture, before);
            AfterClean(before);
        }

        /// <summary>Call after any cleaning: a tank that goes from messy to spotless gets a cheer and a logged time.</summary>
        public void AfterClean(float messBefore)
        {
            Cleaned?.Invoke();
            float now = SimClock.Instance.Sim.MessLevel;
            if (now >= 0.05f || !GlassClear() || messySince < 0) return; // not spotless yet, or nothing to celebrate since the last full clean
            if (crowd != null)
            {
                crowd.CheerAll();
                foreach (var a in crowd.Agents) Juice.Confetti(a.transform.position + Vector3.up * crowd.characterScale * 2f, 12);
            }
            Juice.Chime();
            if (messySince >= 0)
            {
                float secs = Time.unscaledTime - messySince;
                EventLog.Instance?.Log("full_clean", ("seconds", secs), ("mess_from", messBefore));
                EventLog.Instance?.Count("full_cleans");
                EventLog.Instance?.Count("full_clean_s", secs);
            }
            messySince = -1;
        }

        /// <summary>Spotless means every pane, not just the average (a smudged back pane shouldn't get a cheer).</summary>
        static bool GlassClear()
        {
            var glass = GlassGrime.Instance;
            if (glass == null) return SimClock.Instance.Sim.State.grime < 0.05f;
            foreach (var p in glass.Panes) if (p.Mean > 0.06f) return false;
            return true;
        }

        /// <summary>Debug / lab start: the same messy tank every time (fresh even grime, vines, trash).</summary>
        public void MakeMessy(float grime = 0.85f, float overgrowth = 0.5f, int trashCount = 6)
        {
            var sim = SimClock.Instance.Sim;
            sim.CleanAll();
            var rng = sim.State.rng;
            sim.State.rng = new Sim.SimRandom(777); // same trash spots every time (an A/B confound otherwise)
            sim.AddMess(grime, overgrowth, trashCount);
            sim.State.rng = rng;
            if (GlassGrime.Instance != null) GlassGrime.Instance.ResetTo(sim.State.grime);
            messySince = Time.unscaledTime;
        }

        void Puff(Vector3 at)
        {
            var go = new GameObject("Puff");
            go.transform.position = at;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.duration = 0.3f;
            main.loop = false;
            main.startLifetime = 0.5f;
            main.startSpeed = 0.8f;
            main.startSize = 0.07f;
            main.startColor = new Color(1f, 0.97f, 0.85f, 0.9f);
            main.gravityModifier = -0.1f;
            main.stopAction = ParticleSystemStopAction.Destroy;
            var emission = ps.emission;
            emission.rateOverTime = 0;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0, 14) });
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.08f;
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                      new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(0, 1) });
            col.color = g;
            var mat = Juice.SoftMaterial != null ? Juice.SoftMaterial : puffMaterial;
            if (mat != null) go.GetComponent<ParticleSystemRenderer>().sharedMaterial = mat;
            ps.Play();
        }
    }

    /// <summary>A piece of mess: pops with a little hop when tapped, or flies off when flicked by a swipe.</summary>
    public class TapToClean : MonoBehaviour, ITappable
    {
        /// <summary>Uncleaned pieces in the scene, for screen-space picking.</summary>
        public static readonly List<TapToClean> Active = new();

        MessView owner;
        string kind;
        int id;
        bool done, wiggling;

        public string Kind => kind;
        public bool Done => done;

        public void Init(MessView view, string k, int i) { owner = view; kind = k; id = i; }

        void OnEnable() => Active.Add(this);
        void OnDisable() => Active.Remove(this);

        /// <summary>Center of the visible piece (world).</summary>
        public Vector3 Center
        {
            get
            {
                var r = GetComponentInChildren<Renderer>();
                return r != null ? r.bounds.center : transform.position;
            }
        }

        public void OnTap(RaycastHit hit)
        {
            if (done) return;
            if (!owner.tapCleansProps)
            {
                if (!wiggling) StartCoroutine(Wiggle());
                owner.RefuseTap(this);
                return;
            }
            done = true;
            StartCoroutine(Vanish());
        }

        bool held;
        Vector3 restPos;
        Quaternion restRot;
        Coroutine lifting;

        /// <summary>Picked up by a finger: rises a little and tilts, so a grab reads as physical before the toss.
        /// Letting go snaps it back to rest (a wobble usually follows).</summary>
        public void SetHeld(bool on)
        {
            if (done || on == held) return;
            held = on;
            if (on)
            {
                restPos = transform.position;
                restRot = transform.localRotation;
                lifting = StartCoroutine(Lift());
                return;
            }
            if (lifting != null) StopCoroutine(lifting);
            transform.position = restPos;
            transform.localRotation = restRot;
        }

        IEnumerator Lift()
        {
            var t = transform;
            var r = GetComponentInChildren<Renderer>();
            float rise = r != null ? Mathf.Max(0.02f, r.bounds.size.y * 0.35f) : 0.05f;
            var tilt = restRot * Quaternion.Euler(10f, 0, -8f);
            for (float k = 0; k < 1f; k += Time.unscaledDeltaTime * 12f)
            {
                float e = 1f - (1f - k) * (1f - k);
                t.position = restPos + Vector3.up * rise * e;
                t.localRotation = Quaternion.Slerp(restRot, tilt, e);
                yield return null;
            }
            t.position = restPos + Vector3.up * rise;
            t.localRotation = tilt;
        }

        /// <summary>A little shake: touched but not cleaned.</summary>
        public void Wobble()
        {
            if (!done && !wiggling) StartCoroutine(Wiggle());
        }

        /// <summary>Send it flying (world velocity), spinning, shrinking, then clean it. Strength (0..1) pitches the throw sound.</summary>
        public void Flick(Vector3 velocity, float strength01 = 0.5f)
        {
            if (done) return;
            done = true;
            StopAllCoroutines();
            Juice.Flick(strength01);
            StartCoroutine(Fly(velocity));
        }

        IEnumerator Vanish()
        {
            var t = transform;
            var start = t.localScale;
            var p0 = t.position;
            for (float k = 0; k < 1f; k += Time.deltaTime * 5f)
            {
                t.position = p0 + Vector3.up * Mathf.Sin(k * Mathf.PI) * 0.15f;
                t.localScale = start * (1f - k);
                yield return null;
            }
            owner.Clean(kind, id, gameObject, p0 + Vector3.up * 0.1f, "tap");
        }

        IEnumerator Fly(Vector3 velocity)
        {
            var t = transform;
            var start = t.localScale;
            var spin = UnityEngine.Random.onUnitSphere * 720f;
            float gravity = velocity.magnitude * 2f; // arcs look the same at any tank scale
            for (float k = 0; k < 1f; k += Time.deltaTime * 2.5f)
            {
                t.position += velocity * Time.deltaTime;
                velocity += Vector3.down * gravity * Time.deltaTime;
                t.Rotate(spin * Time.deltaTime, Space.World);
                t.localScale = start * (1f - k * k);
                yield return null;
            }
            owner.Clean(kind, id, gameObject, t.position, "flick");
        }

        IEnumerator Wiggle()
        {
            wiggling = true;
            var t = transform;
            var r0 = t.localRotation;
            for (float k = 0; k < 1f; k += Time.deltaTime * 3f)
            {
                t.localRotation = r0 * Quaternion.Euler(0, 0, Mathf.Sin(k * Mathf.PI * 6f) * 12f * (1f - k));
                yield return null;
            }
            t.localRotation = r0;
            wiggling = false;
        }
    }
}
