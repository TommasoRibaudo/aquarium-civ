using System.Collections.Generic;
using AquariumCiv.Game;
using AquariumCiv.Life;
using AquariumCiv.Sim;
using AquariumCiv.Sim.Settle;
using UnityEngine;

namespace AquariumCiv.View
{
    /// <summary>
    /// P8 #286 (keepers-hands 2 beat 6): the pond's frogs and ducks, low poly and pooled (six frogs, two ducks, four lily pads,
    /// all made once and shown or hidden).
    /// <list type="bullet">
    /// <item>Frogs sit on the bank and on the lily pads: two or three by day, five or six at dusk and night (as the frog sound
    /// grows at night), none in winter. They puff their throat now and then (more at night), and hop into the water when a person
    /// comes near; a while later another sits somewhere else.</item>
    /// <item>Ducks: once the pond is <see cref="DuckPondCells"/> cells or more, one duck paddles on it (two from 40 cells), dabbles
    /// (tail up) now and then, and sometimes waddles ashore for a while. A person coming close sends it paddling off.</item>
    /// </list>
    /// </summary>
    public class PondLife : MonoBehaviour
    {
        public static bool AutoCreate = true;
        public static PondLife Instance { get; private set; }

        public const int DuckPondCells = 20, TwoDucksPondCells = 40, MaxFrogs = 6, MaxDucks = 2, MaxPads = 4;
        /// <summary>A person closer than this (box units) sends a frog into the water, and a duck away.</summary>
        public const float FrogFlee = 0.32f, DuckFlee = 0.30f;

        sealed class Frog
        {
            public Transform root, throat; public GameObject go;
            public Vector3 at, from, to; public int state; // 0 hidden, 1 sit, 2 hop in, 3 gone (waiting)
            public float t, timer, puff; public bool onPad;
        }
        sealed class Duck
        {
            public Transform root, head, tail; public GameObject go;
            public Vector2 at, to; public float yaw, timer, dabble; public int state; // 0 hidden, 1 paddle, 2 dabble, 3 ashore (walk), 4 stand ashore, 5 back
            public Vector2 shore;
            /// <summary>#364: seconds it keeps paddling off fast after a splash (the fisher's yank).</summary>
            public float flee;
        }

        BoxView box;
        Transform holder, rootSeen;
        readonly List<Frog> frogs = new();
        readonly List<Duck> ducks = new();
        readonly List<Transform> pads = new();
        readonly List<int> water = new(), bank = new();
        int poolKey = int.MinValue, poolCells = -1;
        float forceFrogsUntil = -1f, forceDucksUntil = -1f;
        readonly List<PersonRef> whoBuf = new(); readonly List<float> px = new(), pz = new();
        static Mesh frogBody, frogThroat, padMesh, duckBody, duckHead, duckTail;
        int tick;

        /// <summary>For tests and the panel.</summary>
        public int FrogsShown { get; private set; }
        public int DucksShown { get; private set; }
        public int PadsShown { get; private set; }
        public int FrogHops { get; private set; }
        public int ShoreVisits { get; private set; }
        public int FrogsSitting { get { int n = 0; foreach (var f in frogs) if (f.state == 1) n++; return n; } }
        public int DucksAshore { get { int n = 0; foreach (var d in ducks) if (d.state == 3 || d.state == 4) n++; return n; } }
        /// <summary>Extra spots (box-local x, z) that scare the animals like a person would (tests).</summary>
        public readonly List<Vector2> Scarers = new();
        public int WantedFrogs { get; private set; }
        public int WantedDucks { get; private set; }

        SimCore Sim => GameClock.Instance != null ? GameClock.Instance.Sim : null;

        public static PondLife Ensure(BoxView b)
        {
            if (b == null) return null;
            var v = b.GetComponent<PondLife>();
            return v != null ? v : b.gameObject.AddComponent<PondLife>();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (!AutoCreate || Instance != null || FindAnyObjectByType<GameClock>() == null) return;
            Ensure(FindAnyObjectByType<BoxView>());
        }

        void OnEnable() { if (Instance != null && Instance != this) { enabled = false; return; } Instance = this; box = GetComponent<BoxView>(); }
        void OnDisable()
        {
            if (Instance == this) Instance = null;
            if (holder != null) Destroy(holder.gameObject);
            holder = null; frogs.Clear(); ducks.Clear(); pads.Clear(); poolKey = int.MinValue;
        }

        // ---------------------------------------------------------------- cheats
        /// <summary>"Frogs now" (debug): the night's frogs show for a minute whatever the hour or season.</summary>
        public string FrogsNow()
        {
            if (box == null || box.layout.eco.pondCells <= 0) return "no pond yet (try 'water breaks the surface now')";
            forceFrogsUntil = Time.unscaledTime + 60f;
            return $"frogs: {MaxFrogs} for a minute";
        }

        /// <summary>"Ducks now" (debug): the ducks show for a minute whatever the pond's size (they need water to paddle on).</summary>
        public string DucksNow()
        {
            if (box == null || box.layout.eco.pondCells <= 0) return "no pond yet (try 'water breaks the surface now')";
            forceDucksUntil = Time.unscaledTime + 60f;
            return $"ducks: {MaxDucks} for a minute (pond is {box.layout.eco.pondCells} cells; they come by themselves from {DuckPondCells})";
        }

        /// <summary>
        /// #315: a croak is about to sound: one sitting frog puffs its throat with it (a sound goes with something on screen) and its
        /// world position comes back. False when no frog sits on the bank or a pad, so no croak plays.
        /// </summary>
        public bool TryCroak(out Vector3 world)
        {
            world = default;
            int n = 0;
            foreach (var f in frogs) if (f.state == 1) n++;
            if (n == 0) return false;
            int k = Random.Range(0, n);
            foreach (var f in frogs)
            {
                if (f.state != 1 || k-- > 0) continue;
                f.puff = 1f; f.timer = Mathf.Max(f.timer, 3f); // the call is the puff: its next idle puff is not right behind it
                world = f.root.position;
                return true;
            }
            return false;
        }

        /// <summary>Where the sitting frogs are (box-local), and the ducks (x, z), for tests.</summary>
        public void FrogSpots(List<Vector3> into) { into.Clear(); foreach (var f in frogs) if (f.state == 1) into.Add(f.at); }
        public void DuckSpots(List<Vector2> into) { into.Clear(); foreach (var d in ducks) if (d.state != 0) into.Add(d.at); }

        // ---------------------------------------------------------------- update
        void Update()
        {
            var sim = Sim;
            if (sim == null || !sim.EcologyOn || box == null || box.Root == null || box.layout.eco.pondCells <= 0) { HideAll(); return; }
            if (rootSeen != box.Root) { holder = null; frogs.Clear(); ducks.Clear(); pads.Clear(); rootSeen = box.Root; poolKey = int.MinValue; }
            var eco = box.layout.eco;
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.1f);
            EnsurePool();
            Rebuild(eco);

            var pv = PeopleView.Find();
            float hour = pv != null ? pv.Now.hour : 12f;
            if (pv != null && pv.Planner != null) pv.StandingNow(whoBuf, px, pz); else { whoBuf.Clear(); px.Clear(); pz.Clear(); }
            foreach (var s in Scarers) { px.Add(s.x); pz.Add(s.y); }

            int season = (int)((sim.State.month / 3 + 3) % 4);
            bool night = hour >= 17.5f || hour < 6f;
            int wantF = water.Count == 0 ? 0 : season == 3 ? 0 : night ? 5 : 2;
            wantF = Mathf.Min(wantF + (night ? 1 : 0) * (hour >= 20f || hour < 5f ? 1 : 0), MaxFrogs);
            wantF = Mathf.Min(wantF, 1 + water.Count / 3);
            if (Time.unscaledTime < forceFrogsUntil) wantF = MaxFrogs;
            int wantD = water.Count >= TwoDucksPondCells ? 2 : water.Count >= DuckPondCells ? 1 : 0;
            if (Time.unscaledTime < forceDucksUntil) wantD = MaxDucks;
            WantedFrogs = wantF; WantedDucks = wantD;

            UpdatePads(eco);
            UpdateFrogs(eco, wantF, night, dt);
            UpdateDucks(eco, wantD, dt);
        }

        void HideAll()
        {
            if (holder != null) holder.gameObject.SetActive(false);
            FrogsShown = DucksShown = PadsShown = 0;
        }

        // ---------------------------------------------------------------- the pools
        void EnsurePool()
        {
            if (holder == null)
            {
                holder = new GameObject("PondLife") { hideFlags = HideFlags.DontSave }.transform;
                holder.SetParent(box.Root, false);
            }
            holder.gameObject.SetActive(true);
            if (frogs.Count == 0) { BuildMeshes(); for (int i = 0; i < MaxFrogs; i++) frogs.Add(MakeFrog(i)); }
            if (ducks.Count == 0) for (int i = 0; i < MaxDucks; i++) ducks.Add(MakeDuck(i));
            if (pads.Count == 0) for (int i = 0; i < MaxPads; i++) pads.Add(MakePad(i));
        }

        static GameObject Part(Transform parent, string name, Mesh m, Material mat, Vector3 at)
        {
            var g = new GameObject(name) { hideFlags = HideFlags.DontSave };
            g.transform.SetParent(parent, false);
            g.transform.localPosition = at;
            g.AddComponent<MeshFilter>().sharedMesh = m;
            var r = g.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return g;
        }

        static void BuildMeshes()
        {
            if (frogBody != null) return;
            var green = new Color(0.30f, 0.52f, 0.18f); var belly = new Color(0.78f, 0.82f, 0.50f); var dark = new Color(0.07f, 0.08f, 0.05f);
            var mb = new MeshBuilder(61) { jitter = 0.02f };
            mb.Ico(new Vector3(0f, 0.012f, 0f), 0.014f, new Vector3(1f, 0.8f, 1.2f), green, 0.05f, belly);              // body
            mb.Ico(new Vector3(0f, 0.020f, 0.015f), 0.009f, new Vector3(1.2f, 0.8f, 1f), green, 0.04f);                   // head
            mb.Ico(new Vector3(0.006f, 0.027f, 0.018f), 0.0045f, Vector3.one, belly, 0.03f);                              // eyes
            mb.Ico(new Vector3(-0.006f, 0.027f, 0.018f), 0.0045f, Vector3.one, belly, 0.03f);
            mb.Ico(new Vector3(0.006f, 0.029f, 0.021f), 0.0022f, Vector3.one, dark, 0.02f);
            mb.Ico(new Vector3(-0.006f, 0.029f, 0.021f), 0.0022f, Vector3.one, dark, 0.02f);
            mb.Ico(new Vector3(0.012f, 0.005f, -0.010f), 0.006f, new Vector3(0.7f, 0.6f, 1.6f), green, 0.04f);            // folded hind legs
            mb.Ico(new Vector3(-0.012f, 0.005f, -0.010f), 0.006f, new Vector3(0.7f, 0.6f, 1.6f), green, 0.04f);
            frogBody = mb.ToMesh("FrogBody");
            var tb = new MeshBuilder(62) { jitter = 0f };
            tb.Ico(Vector3.zero, 0.007f, Vector3.one, belly, 0.02f);
            frogThroat = tb.ToMesh("FrogThroat");
            var pb = new MeshBuilder(63) { jitter = 0.02f };
            pb.Prism(Vector3.zero, 0.035f, 0.030f, 0.004f, 9, new Color(0.22f, 0.45f, 0.20f));
            pb.Ico(new Vector3(0.012f, 0.005f, 0.01f), 0.008f, new Vector3(1f, 0.3f, 1f), new Color(0.95f, 0.78f, 0.85f), 0.03f); // a pink bloom
            padMesh = pb.ToMesh("LilyPad");
            var db = new MeshBuilder(64) { jitter = 0.02f };
            var brown = new Color(0.50f, 0.38f, 0.24f); var cream = new Color(0.86f, 0.80f, 0.66f);
            db.Ico(new Vector3(0f, 0.016f, 0f), 0.026f, new Vector3(0.85f, 0.75f, 1.35f), brown, 0.05f, cream);           // body
            duckBody = db.ToMesh("DuckBody");
            var hb = new MeshBuilder(65) { jitter = 0.01f };
            hb.Ico(Vector3.zero, 0.014f, Vector3.one, new Color(0.14f, 0.40f, 0.22f), 0.04f);                              // green head
            hb.Ico(new Vector3(0f, -0.003f, 0.017f), 0.007f, new Vector3(1.1f, 0.5f, 1.5f), new Color(0.95f, 0.65f, 0.15f), 0.03f); // beak
            hb.Ico(new Vector3(0.008f, 0.004f, 0.008f), 0.0025f, Vector3.one, dark, 0.02f);
            hb.Ico(new Vector3(-0.008f, 0.004f, 0.008f), 0.0025f, Vector3.one, dark, 0.02f);
            duckHead = hb.ToMesh("DuckHead");
            var ab = new MeshBuilder(66) { jitter = 0.01f };
            ab.Ico(Vector3.zero, 0.012f, new Vector3(0.7f, 0.9f, 1.1f), cream, 0.03f);
            duckTail = ab.ToMesh("DuckTail");
        }

        Frog MakeFrog(int i)
        {
            var f = new Frog();
            f.go = new GameObject("Frog") { hideFlags = HideFlags.DontSave };
            f.root = f.go.transform; f.root.SetParent(holder, false);
            f.root.localScale = Vector3.one * 1.8f; // small animals must still read at the default zoom
            Part(f.root, "Body", frogBody, box.SolidMaterial, Vector3.zero);
            f.throat = Part(f.root, "Throat", frogThroat, box.SolidMaterial, new Vector3(0f, 0.012f, 0.022f)).transform;
            f.throat.localScale = Vector3.zero;
            f.go.SetActive(false);
            f.timer = i * 0.9f;
            return f;
        }

        Duck MakeDuck(int i)
        {
            var d = new Duck();
            d.go = new GameObject("Duck") { hideFlags = HideFlags.DontSave };
            d.root = d.go.transform; d.root.SetParent(holder, false);
            Part(d.root, "Body", duckBody, box.SolidMaterial, Vector3.zero);
            d.head = Part(d.root, "Head", duckHead, box.SolidMaterial, new Vector3(0f, 0.040f, 0.030f)).transform;
            d.tail = Part(d.root, "Tail", duckTail, box.SolidMaterial, new Vector3(0f, 0.028f, -0.034f)).transform;
            d.go.SetActive(false);
            d.timer = 1f + i;
            return d;
        }

        Transform MakePad(int i)
        {
            var g = Part(holder, "LilyPad", padMesh, box.SolidMaterial, Vector3.zero);
            g.SetActive(false);
            return g.transform;
        }

        // ---------------------------------------------------------------- cells
        /// <summary>The pond's water cells and the bank cells beside them, rebuilt when the pond changes.</summary>
        void Rebuild(EcoLayer eco)
        {
            int key = eco.pondCells * 31 + Mathf.RoundToInt(eco.waterY * 1000f);
            if (key == poolKey && water.Count > 0) return;
            poolKey = key;
            water.Clear(); bank.Clear();
            for (int c = 0; c < SiteMap.N; c++) if (eco.WaterCell(c)) water.Add(c);
            for (int c = 0; c < SiteMap.N; c++)
            {
                if (eco.WaterCell(c) || eco.At(c) == EcoLayer.Tree) continue;
                int i = SiteMap.I(c), j = SiteMap.J(c); bool next = false;
                for (int dj = -1; dj <= 1 && !next; dj++)
                    for (int di = -1; di <= 1; di++)
                    {
                        int a = i + di, b = j + dj;
                        if (a < 0 || b < 0 || a >= SiteMap.NX || b >= SiteMap.NZ) continue;
                        if (eco.WaterCell(SiteMap.Index(a, b))) { next = true; break; }
                    }
                if (next) bank.Add(c);
            }
        }

        bool IsWaterAt(EcoLayer eco, float x, float z) => eco.WaterCell(SiteMap.CellAt(x, z));
        Vector3 OnWater(EcoLayer eco, float x, float z, float up = 0f) => new Vector3(x, eco.waterY + up, z);
        Vector3 OnBank(EcoLayer eco, float x, float z) => new Vector3(x, Mathf.Max(box.land.Height(x, z), eco.waterY) , z);

        float NearestPerson(float x, float z)
        {
            float best = float.MaxValue;
            for (int i = 0; i < px.Count; i++) { float dx = px[i] - x, dz = pz[i] - z; float d = dx * dx + dz * dz; if (d < best) best = d; }
            return Mathf.Sqrt(best);
        }

        static float H(int a, int b) { uint h = (uint)(a * 73856093) ^ (uint)(b * 19349663); h ^= h >> 13; h *= 0x5bd1e995; h ^= h >> 15; return (h & 0xFFFF) / 65535f; }

        // ---------------------------------------------------------------- pads
        readonly List<Vector3> padAt = new();
        void UpdatePads(EcoLayer eco)
        {
            // a few lily pads on water cells that have water all round them (not at the edge)
            padAt.Clear();
            int want = Mathf.Min(MaxPads, water.Count / 5);
            for (int k = 0; k < water.Count && padAt.Count < want; k++)
            {
                int c = water[(k * 7 + 3) % water.Count];
                int i = SiteMap.I(c), j = SiteMap.J(c);
                bool inner = true;
                for (int dj = -1; dj <= 1 && inner; dj++)
                    for (int di = -1; di <= 1; di++)
                    {
                        int a = i + di, b = j + dj;
                        if (a < 0 || b < 0 || a >= SiteMap.NX || b >= SiteMap.NZ || !eco.WaterCell(SiteMap.Index(a, b))) { inner = false; break; }
                    }
                if (!inner) continue;
                bool far = true;
                foreach (var p in padAt) if ((p.x - SiteMap.CXc(c)) * (p.x - SiteMap.CXc(c)) + (p.z - SiteMap.CZc(c)) * (p.z - SiteMap.CZc(c)) < 0.09f) far = false;
                if (far) padAt.Add(new Vector3(SiteMap.CXc(c), eco.waterY + 0.003f, SiteMap.CZc(c)));
            }
            PadsShown = padAt.Count;
            for (int i = 0; i < pads.Count; i++)
            {
                bool on = i < padAt.Count;
                if (on) { pads[i].localPosition = padAt[i]; pads[i].localRotation = Quaternion.Euler(0f, i * 70f, 0f); }
                pads[i].gameObject.SetActive(on);
            }
        }

        // ---------------------------------------------------------------- frogs
        /// <summary>The splash radius (box units) in which a frog hops in and a duck paddles off (#364: the fisher's yank; the spec says 0.5 u).</summary>
        public const float StartleRadius = 0.5f;
        public int Startles { get; private set; }

        /// <summary>
        /// #364: a splash at <paramref name="world"/> (the fish leaves the water): every sitting frog within <paramref name="radius"/> hops in, and every
        /// duck on the water or the bank within it paddles off the other way, fast, for a few seconds. Returns how many animals moved off.
        /// </summary>
        public int Startle(Vector3 world, float radius = StartleRadius)
        {
            if (box == null || box.Root == null || !isActiveAndEnabled) return 0;
            var eco = box.layout.eco;
            if (eco.pondCells <= 0) return 0;
            var l = box.Root.InverseTransformPoint(world);
            var from = new Vector2(l.x, l.z);
            float r2 = radius * radius;
            int n = 0;
            foreach (var f in frogs)
                if (f.state == 1 && f.go.activeSelf && (new Vector2(f.at.x, f.at.z) - from).sqrMagnitude < r2) { Hop(f, eco); n++; }
            foreach (var d in ducks)
            {
                if (d.state == 0 || d.state == 3 || d.state == 4 || water.Count == 0 || (d.at - from).sqrMagnitude >= r2) continue;
                // the farthest water from the splash among a few cells
                Vector2 best = d.at; float bestD = -1f;
                for (int k = 0; k < 8; k++)
                {
                    int c = water[(int)((uint)(d.at.x * 977f + d.at.y * 131f + k * 7919f) % (uint)water.Count)];
                    var q = new Vector2(SiteMap.CXc(c), SiteMap.CZc(c));
                    float dist = (q - from).sqrMagnitude;
                    if (dist > bestD) { bestD = dist; best = q; }
                }
                d.to = best; d.state = 5; d.timer = 4f; d.flee = 2.5f;
                n++;
            }
            Startles += n;
            return n;
        }

        void UpdateFrogs(EcoLayer eco, int want, bool night, float dt)
        {
            int active = 0;
            foreach (var f in frogs) if (f.state != 0) active++;
            int shown = 0;
            for (int i = 0; i < frogs.Count; i++)
            {
                var f = frogs[i];
                bool wanted = i < want;
                if (f.state == 0)
                {
                    if (!wanted) { f.go.SetActive(false); continue; }
                    f.timer -= dt;
                    if (f.timer <= 0f) Place(f, eco, i);
                    continue;
                }
                if (!wanted && f.state == 1) { f.go.SetActive(false); f.state = 0; f.timer = 1f; continue; }
                switch (f.state)
                {
                    case 1: // sitting
                        if (NearestPerson(f.at.x, f.at.z) < FrogFlee) { Hop(f, eco); break; }
                        f.timer -= dt;
                        if (f.timer <= 0f) { f.puff = 1f; f.timer = night ? Random.Range(2.5f, 6f) : Random.Range(7f, 16f); }
                        f.puff = Mathf.Max(0f, f.puff - dt * 1.4f);
                        float pk = Mathf.Sin(Mathf.Clamp01(f.puff) * Mathf.PI);
                        f.throat.localScale = Vector3.one * (pk * 1.6f);
                        break;
                    case 2: // hopping into the water
                        f.t += dt / 0.55f;
                        float u = Mathf.Clamp01(f.t);
                        var p = Vector3.Lerp(f.from, f.to, u);
                        p.y += Mathf.Sin(u * Mathf.PI) * 0.05f;
                        f.root.localPosition = p;
                        f.throat.localScale = Vector3.zero;
                        if (u >= 1f) { f.state = 3; f.go.SetActive(false); f.timer = Random.Range(9f, 16f); }
                        break;
                    case 3: break;
                }
                if (f.state == 3) { f.timer -= dt; if (f.timer <= 0f) { f.state = 0; f.timer = 0f; } }
                if (f.state == 1 || f.state == 2) shown++;
            }
            FrogsShown = shown;
        }

        void Place(Frog f, EcoLayer eco, int idx)
        {
            // half the frogs take a lily pad when there is one; the rest a bank cell away from people
            bool pad = padAt.Count > 0 && (idx % 2 == 1);
            Vector3 p;
            if (pad) { p = padAt[(idx / 2) % padAt.Count] + Vector3.up * 0.004f; f.onPad = true; }
            else
            {
                if (bank.Count == 0) { f.timer = 2f; return; }
                int pick = -1;
                for (int tries = 0; tries < 8; tries++)
                {
                    int c = bank[Random.Range(0, bank.Count)];
                    if (NearestPerson(SiteMap.CXc(c), SiteMap.CZc(c)) < FrogFlee * 1.5f) continue;
                    bool taken = false;
                    foreach (var o in frogs) if (o != f && o.state == 1 && (o.at - new Vector3(SiteMap.CXc(c), o.at.y, SiteMap.CZc(c))).sqrMagnitude < 0.0036f) taken = true;
                    if (!taken) { pick = c; break; }
                }
                if (pick < 0) { f.timer = 2f; return; }
                float x = SiteMap.CXc(pick) + (H(pick, idx) - 0.5f) * 0.06f, z = SiteMap.CZc(pick) + (H(idx, pick) - 0.5f) * 0.06f;
                p = OnBank(eco, x, z); f.onPad = false;
            }
            if (pad && NearestPerson(p.x, p.z) < FrogFlee) { f.timer = 3f; return; }
            f.at = p; f.state = 1; f.t = 0f; f.timer = Random.Range(1.5f, 5f); f.puff = 0f;
            f.root.localPosition = p;
            f.root.localRotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            f.go.SetActive(true);
        }

        void Hop(Frog f, EcoLayer eco)
        {
            // into the nearest water cell
            Vector3 best = f.at; float bd = float.MaxValue;
            if (f.onPad) { best = f.at + new Vector3(0.02f, -0.01f, 0.02f); bd = 0f; }
            else
                for (int k = 0; k < water.Count; k++)
                {
                    int c = water[k]; float dx = SiteMap.CXc(c) - f.at.x, dz = SiteMap.CZc(c) - f.at.z, d = dx * dx + dz * dz;
                    if (d < bd) { bd = d; best = new Vector3(SiteMap.CXc(c), eco.waterY - 0.004f, SiteMap.CZc(c)); }
                }
            f.from = f.at; f.to = best; f.t = 0f; f.state = 2; FrogHops++;
            var dir = best - f.at; if (dir.sqrMagnitude > 1e-6f) f.root.localRotation = Quaternion.Euler(0f, Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg, 0f);
        }

        // ---------------------------------------------------------------- ducks
        void UpdateDucks(EcoLayer eco, int want, float dt)
        {
            int shown = 0;
            for (int i = 0; i < ducks.Count; i++)
            {
                var d = ducks[i];
                if (i >= want) { if (d.state != 0) { d.go.SetActive(false); d.state = 0; } continue; }
                if (d.state == 0)
                {
                    int c = water[(i * 13 + 5) % water.Count];
                    d.at = new Vector2(SiteMap.CXc(c), SiteMap.CZc(c)); d.to = d.at; d.state = 1; d.timer = Random.Range(2f, 5f);
                    d.go.SetActive(true);
                }
                bool scared = NearestPerson(d.at.x, d.at.y) < DuckFlee;
                float speed = 0.05f;
                if (scared && d.state != 1) { d.state = 5; }
                switch (d.state)
                {
                    case 1: // paddling
                    case 5:
                        if (scared || (d.at - d.to).sqrMagnitude < 0.0004f || d.timer <= 0f) PickTarget(d, eco, scared);
                        d.timer -= dt;
                        if (scared || d.flee > 0f) speed = 0.12f;
                        d.flee = Mathf.Max(0f, d.flee - dt);
                        Move(d, eco, speed, dt);
                        if (!scared && (d.at - d.to).sqrMagnitude < 0.0009f && Random.value < dt * 0.25f) { d.state = 2; d.dabble = 0f; }
                        else if (!scared && d.state == 5) d.state = 1;
                        break;
                    case 2: // dabbling
                        d.dabble += dt;
                        if (scared) { d.state = 5; break; }
                        if (d.dabble > 3.2f)
                        {
                            d.state = 1; d.timer = 0f;
                            if (bank.Count > 0 && Random.value < 0.3f) StartShore(d, eco);
                        }
                        break;
                    case 3: // waddling ashore
                        Move(d, eco, 0.03f, dt, true);
                        if ((d.at - d.shore).sqrMagnitude < 0.0004f) { d.state = 4; d.dabble = 0f; ShoreVisits++; }
                        break;
                    case 4: // standing on the bank
                        d.dabble += dt;
                        if (d.dabble > 7f) { d.state = 5; d.timer = 0f; }
                        break;
                }
                Pose(d, eco);
                shown++;
            }
            DucksShown = shown;
        }

        void StartShore(Duck d, EcoLayer eco)
        {
            int c = bank[Random.Range(0, bank.Count)];
            d.shore = new Vector2(SiteMap.CXc(c), SiteMap.CZc(c));
            if (NearestPerson(d.shore.x, d.shore.y) < DuckFlee * 1.5f) return;
            d.to = d.shore; d.state = 3;
        }

        void PickTarget(Duck d, EcoLayer eco, bool scared)
        {
            d.timer = Random.Range(4f, 9f);
            Vector2 best = d.at; float bestScore = float.MinValue;
            for (int k = 0; k < 6; k++)
            {
                int c = water[Random.Range(0, water.Count)];
                var q = new Vector2(SiteMap.CXc(c), SiteMap.CZc(c));
                float dist = (q - d.at).magnitude;
                if (dist > 0.6f && !scared) continue;
                float score = scared ? NearestPerson(q.x, q.y) : -Mathf.Abs(dist - 0.25f);
                if (score > bestScore) { bestScore = score; best = q; }
            }
            d.to = best;
        }

        void Move(Duck d, EcoLayer eco, float speed, float dt, bool ashore = false)
        {
            var dir = d.to - d.at;
            float len = dir.magnitude;
            if (len < 0.004f) return;
            dir /= len;
            float step = Mathf.Min(len, speed * dt);
            var next = d.at + dir * step;
            // a paddling duck stays on the water: if the next cell is land, stop here and choose again
            if (!ashore && !IsWaterAt(eco, next.x, next.y)) { d.to = d.at; return; }
            d.at = next;
            float yaw = Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg;
            d.yaw = Mathf.LerpAngle(d.yaw, yaw, 1f - Mathf.Exp(-6f * dt));
        }

        void Pose(Duck d, EcoLayer eco)
        {
            bool onWater = IsWaterAt(eco, d.at.x, d.at.y);
            float y = onWater ? eco.waterY + 0.004f + Mathf.Sin(Time.time * 2f + d.at.x * 20f) * 0.0015f
                              : Mathf.Max(box.land.Height(d.at.x, d.at.y), eco.waterY);
            d.root.localPosition = new Vector3(d.at.x, y, d.at.y);
            d.root.localRotation = Quaternion.Euler(0f, d.yaw, 0f);
            float dab = d.state == 2 ? Mathf.Sin(Mathf.Clamp01(d.dabble / 3.2f) * Mathf.PI) : 0f;
            d.root.localRotation *= Quaternion.Euler(dab * 55f, 0f, 0f); // tail up, head down
            d.head.localRotation = Quaternion.Euler(d.state == 4 ? Mathf.Sin(Time.time * 1.3f) * 12f : 0f, 0f, 0f);
        }
    }
}
