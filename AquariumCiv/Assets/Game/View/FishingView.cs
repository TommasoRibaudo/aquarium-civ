using AquariumCiv.Game;
using System.Collections.Generic;
using AquariumCiv.Life;
using UnityEngine;

namespace AquariumCiv.View
{
    /// <summary>
    /// #364 B8 Fishing pays off: the view side of <see cref="FishBeats"/>. Listens to <see cref="PeopleView.Fishing"/> (one call a frame per standing fisher: the
    /// block's seconds, the seconds left, the line's end on the water) and plays each bite's world:
    /// <list type="bullet">
    /// <item>the nibble: a tiny ring at the line's end at the bottom of each of the two dips;</item>
    /// <item>the yank (the pose's Contact; <see cref="HitFx"/> already puts droplets and a ring at the line's end on the same frame): a catch is a small fish
    /// (code-built, one pooled mesh, palette colours) that flips twice on a kit arc (<see cref="ArcPool"/>, h 0.3 u, 0.6 s) into the basket on the ground beside the
    /// fisher, which Pops and shows a fish (two at most), and a frog or duck within 0.5 u of the splash moves off (<see cref="PondLife.Startle"/>); a miss
    /// is a fish that only clears the water (h 0.05 u) and drops back into a ring.</item>
    /// </list>
    /// The sim has no per-fish count (a fisher's output is a rate in the job's food), so the view never shows more than the plan allows: at most one fish per
    /// 9 s per fisher, 2 in 3 of the bites, and the basket never shows more than two.
    /// </summary>
    [DefaultExecutionOrder(7)]
    public sealed class FishingView : MonoBehaviour
    {
        public static bool AutoCreate = true;
        public static FishingView Instance { get; private set; }

        public const int PoolSize = 8;
        /// <summary>The fish is 0.06 u long (a doll is 0.22 u tall); it is built once and scaled with the dolls' zoom.</summary>
        public const float FishLength = 0.06f;

        sealed class Fisher
        {
            public bool init; public float lastS, seen, left; public int shown;
            public Transform basket; public readonly Transform[] fish = new Transform[FishBeats.MaxShown]; public readonly float[] pop = { -1f, -1f };
            public Vector3 top;
        }
        sealed class Flyer { public GameObject go; public ArcHandle arc; public float landAt; public bool miss; public Vector3 end, away; public int key; }

        PeopleView people;
        BoxView box;
        float findT, clock;
        readonly ArcPool pool = new(PoolSize);
        readonly List<Flyer> flying = new();
        readonly Dictionary<int, Fisher> fishers = new();
        readonly Dictionary<int, float> shift = new();
        readonly HashSet<int> wantBite = new();
        Mesh fishMesh;

        // for tests and the debug panel
        public int NibblesSeen { get; private set; }
        public int CatchesLaunched { get; private set; }
        public int MissesLaunched { get; private set; }
        public int Landed { get; private set; }
        public int Startled { get; private set; }
        /// <summary>#543: catches that landed in a fish basket and were counted into the camp's chain.</summary>
        public int FishKept { get; private set; }
        public int LiveFlyers => flying.Count;
        public int FishersShown { get { int n = 0; foreach (var f in fishers.Values) if (clock - f.seen < 0.25f) n++; return n; } }

        /// <summary>Fish the basket beside this fisher shows now (tests).</summary>
        public int BasketCount(PersonRef who) => fishers.TryGetValue(who.Key, out var f) && clock - f.seen < 0.25f ? f.shown : 0;

        /// <summary>The cheat's shift of a fisher's block seconds (PeopleView adds it to the block's own seconds).</summary>
        public static float ShiftOf(int key) => Instance != null && Instance.shift.TryGetValue(key, out var s) ? s : 0f;

        public static FishingView Ensure()
        {
            if (Instance != null) return Instance;
            var b = FindAnyObjectByType<BoxView>();
            return b != null ? (b.GetComponent<FishingView>() ?? b.gameObject.AddComponent<FishingView>()) : null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (AutoCreate && Instance == null && FindAnyObjectByType<GameClock>() != null) Ensure();
        }

        void OnEnable()
        {
            if (Instance != null && Instance != this) { enabled = false; return; }
            Instance = this;
            box = GetComponent<BoxView>();
        }

        void OnDisable()
        {
            if (Instance != this) return;
            Instance = null;
            if (people != null) people.Fishing -= OnFisher;
            people = null;
            foreach (var f in flying) if (f.go != null) Destroy(f.go);
            flying.Clear();
            foreach (var f in fishers.Values) if (f.basket != null) Destroy(f.basket.gameObject);
            fishers.Clear(); shift.Clear(); wantBite.Clear();
            if (fishMesh != null) Destroy(fishMesh);
            if (holder != null) Destroy(holder.gameObject);
            holder = null;
        }

        void Update()
        {
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.1f);
            clock += dt;
            findT -= dt;
            if (findT <= 0f)
            {
                findT = 0.5f;
                var pv = PeopleView.Find();
                if (pv != people)
                {
                    if (people != null) people.Fishing -= OnFisher;
                    people = pv;
                    if (people != null) people.Fishing += OnFisher;
                }
            }
            StepFlyers();
            StepBaskets(dt);
        }

        // ---------------------------------------------------------------- the fisher's frame
        void OnFisher(PersonRef who, PegDollView v, float s, float left, Vector3 line)
        {
            if (v == null) return;
            int key = who.Key;
            if (!fishers.TryGetValue(key, out var st)) fishers[key] = st = new Fisher();
            bool fresh = !st.init || clock - st.seen > 0.5f || s < st.lastS - 0.25f; // a new block (or the first frame we see)
            if (fresh && s < st.lastS - 0.25f) shift.Remove(key); // the cheat's shift belonged to the block that ended
            st.seen = clock; st.left = left;
            if (wantBite.Remove(key) && left > FishBeats.BiteSeconds + FishBeats.EndSeconds + 0.5f)
            {   // "Fish bite now": move this block's clock so the next catch's nibble starts in 0.3 s
                int k = FishBeats.NextBite(key, s, true);
                float delta = FishBeats.BiteAt(key, k) - 0.3f - s;
                shift[key] = (shift.TryGetValue(key, out var old) ? old : 0f) + delta;
                s += delta;
            }
            float from = fresh ? s : st.lastS;
            st.lastS = s;
            var away = line - v.transform.position; away.y = 0f;
            away = away.sqrMagnitude > 1e-6f ? away.normalized : Vector3.forward;
            PlaceBasket(st, v);
            if (fresh)
            {   // what the plan says is already in the basket stays there, no Pop
                st.init = true; st.shown = FishBeats.Shown(key, s, left);
                for (int i = 0; i < st.pop.Length; i++) st.pop[i] = -1f;
            }
            else if (s - from <= 1f)
            {
                int cur = FishBeats.BiteIndexAt(key, s);
                for (int k = Mathf.Max(0, cur - 1); k <= cur; k++) BiteEvents(key, k, from, s, left, st, line, away);
            }
            int shownNow = FishBeats.Shown(key, s, left);
            if (shownNow > st.shown) st.pop[Mathf.Clamp(shownNow - 1, 0, st.pop.Length - 1)] = 0.0001f;
            st.shown = shownNow;
        }

        /// <summary>What happens in the world during (from, to] of bite <paramref name="k"/>: the nibble's rings, the yank (the fish leaves the water).</summary>
        void BiteEvents(int key, int k, float from, float to, float left, Fisher st, Vector3 line, Vector3 away)
        {
            float b = FishBeats.BiteAt(key, k);
            foreach (float at in FishBeats.NibbleRingAt)
                if (from < b + at && b + at <= to) { NibblesSeen++; Ring(line, away, 0.5f); }
            if (!(from < b + FishBeats.ContactAt && b + FishBeats.ContactAt <= to) || FishBeats.Ending(left)) return;
            // the yank: the pond's droplets and ring are HitFx's (the pose's hit, at the line's end); the fish and the animals are ours
            var pl = PondLife.Instance;
            if (pl != null) Startled += pl.Startle(line);
            float zoom = people != null ? people.DollZoom : 1f;
            if (FishBeats.Miss(key, k))
            {
                var end = line + away * 0.04f * zoom;
                Launch(line, end, FishBeats.MissApex * zoom, FishBeats.MissFlight, Landing.Splash, true, away, key);
                MissesLaunched++;
            }
            else
            {
                Launch(line, st.top, FishBeats.Apex * zoom, FishBeats.Flight, Landing.DropIn, false, away, key);
                CatchesLaunched++;
            }
        }

        void Launch(Vector3 from, Vector3 to, float apex, float flight, Landing landing, bool miss, Vector3 away, int key)
        {
            var go = NewPiece("Fish", FishMesh());
            var shot = ArcShot.Throw(from, to, apex, flight, landing, clock, FishBeats.Spin, 0.02f);
            flying.Add(new Flyer { go = go, arc = pool.Launch(shot), landAt = shot.LandAt, miss = miss, end = to, away = away, key = key });
            go.transform.position = from;
        }

        void Ring(Vector3 at, Vector3 away, float size)
        {
            var o = PuffOptions.Default;
            o.groundY = at.y;
            HitFx.Puff(PuffKind.Ring, at, away, size, o);
        }

        void StepFlyers()
        {
            for (int i = flying.Count - 1; i >= 0; i--)
            {
                var f = flying[i];
                if (f.go == null || !pool.Sample(f.arc, clock, out var s)) { Remove(f, i); continue; }
                float zoom = people != null ? people.DollZoom : 1f;
                f.go.transform.SetPositionAndRotation(s.position, s.rotation);
                f.go.transform.localScale = Vector3.one * zoom;
                if (clock >= f.landAt)
                {
                    Landed++;
                    if (f.miss) Ring(f.end, f.away, 0.7f); // it drops back: one ring
                    else if (people != null && people.Planner != null && people.Planner.FishCaught(f.key)) FishKept++; // #543: it is in the fish basket, and the sim's
                    Remove(f, i);
                }
            }
        }

        void Remove(Flyer f, int i)
        {
            if (f.go != null) Destroy(f.go);
            pool.Release(f.arc);
            flying.RemoveAt(i);
        }

        // ---------------------------------------------------------------- the basket on the ground beside the fisher
        static readonly Vector3[] FishSpots = { new(0.012f, 0.014f, 0.004f), new(-0.008f, 0.022f, -0.006f) };
        static readonly float[] FishYaw = { 70f, -20f };

        void PlaceBasket(Fisher st, PegDollView v)
        {
            float zoom = people != null ? people.DollZoom : 1f;
            if (st.basket == null)
            {
                var go = NewPiece("FishBasket", PegDoll.PropMesh(PropKind.FishBasket)); // #543: its own look, not the berry basket's
                st.basket = go.transform;
                for (int i = 0; i < st.fish.Length; i++)
                {
                    var f = NewPiece("BasketFish", FishMesh()).transform;
                    f.SetParent(st.basket, false);
                    f.localPosition = FishSpots[i];
                    f.localRotation = Quaternion.Euler(0f, FishYaw[i], 0f);
                    f.localScale = Vector3.zero;
                    f.gameObject.SetActive(false);
                    st.fish[i] = f;
                }
            }
            // on the ground 0.08 u to the free hand's side (the doll's left, local -x), where the ground is
            var side = -v.transform.right; side.y = 0f;
            side = side.sqrMagnitude > 1e-6f ? side.normalized : Vector3.left;
            var p = v.transform.position + side * (FishBeats.BasketSide * zoom);
            if (box != null && box.Root != null)
            {
                var l = box.Root.InverseTransformPoint(p);
                p = box.Root.TransformPoint(new Vector3(l.x, box.SurfaceHeight(l.x, l.z), l.z));
            }
            st.basket.SetPositionAndRotation(p + Vector3.up * (0.03f * zoom), Quaternion.identity);
            st.basket.localScale = Vector3.one * zoom;
            if (!st.basket.gameObject.activeSelf) st.basket.gameObject.SetActive(true);
            st.top = p + Vector3.up * (0.05f * zoom);
        }

        /// <summary>A fisher unseen this long has packed up: their basket is destroyed (batch 3: one hidden basket root and three
        /// renderers stayed for every person who ever fished). A block seen again starts fresh anyway (<c>init</c> is false).</summary>
        const float PackUpSeconds = 2f;
        readonly List<int> packedUp = new List<int>();

        void StepBaskets(float dt)
        {
            packedUp.Clear();
            foreach (var kv in fishers)
                if (clock - kv.Value.seen > PackUpSeconds) packedUp.Add(kv.Key);
            foreach (int key in packedUp)
            {
                var gone = fishers[key];
                if (gone.basket != null) Destroy(gone.basket.gameObject);
                fishers.Remove(key);
            }
            foreach (var st in fishers.Values)
            {
                if (st.basket == null) continue;
                bool here = clock - st.seen < 0.25f;
                if (st.basket.gameObject.activeSelf != here) st.basket.gameObject.SetActive(here);
                if (!here) { st.init = false; continue; }
                for (int i = 0; i < st.fish.Length; i++)
                {
                    float sc;
                    if (i >= st.shown) sc = 0f;
                    else if (st.pop[i] > 0f)
                    {
                        st.pop[i] += dt;
                        float u = st.pop[i] / 0.15f; // Easing.Pop: 0 -> 1.2 -> 1 over 0.15 s: the fish drops in and the basket jumps a little
                        sc = u >= 1f ? 1f : Easing.Pop(u);
                        if (u >= 1f) st.pop[i] = -1f;
                    }
                    else sc = 1f;
                    st.fish[i].localScale = Vector3.one * sc;
                    if (st.fish[i].gameObject.activeSelf != (sc > 0f)) st.fish[i].gameObject.SetActive(sc > 0f);
                }
            }
        }

        // ---------------------------------------------------------------- the pieces
        Transform holder;

        /// <summary>Batch 3: every piece (the flying fish, the baskets) under one child of the box, at world scale 1, so none of them is a
        /// root of the scene of its own (pieces are placed in world space).</summary>
        Transform Holder()
        {
            if (holder == null)
            {
                holder = new GameObject("FishingPieces").transform;
                holder.SetParent(transform, false);
            }
            var ls = transform.lossyScale;
            holder.localScale = new Vector3(ls.x != 0f ? 1f / ls.x : 1f, ls.y != 0f ? 1f / ls.y : 1f, ls.z != 0f ? 1f / ls.z : 1f);
            return holder;
        }

        GameObject NewPiece(string name, Mesh mesh)
        {
            var go = new GameObject(name);
            go.transform.SetParent(Holder(), false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = box != null ? box.SolidMaterial : null;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }

        /// <summary>A small silver-blue fish, 0.06 u long, its long axis local +z (a kit arc's heading), the tail at -z.</summary>
        Mesh FishMesh()
        {
            if (fishMesh == null)
            {
                var body = Color.Lerp(Palette.Water, Palette.Plaster, 0.5f);
                var mb = new MeshBuilder(364) { jitter = 0f };
                mb.Ico(Vector3.zero, 0.016f, new Vector3(0.5f, 0.75f, 1.85f), body, 0.04f);
                mb.Ico(new Vector3(0f, 0f, -0.032f), 0.011f, new Vector3(0.18f, 1.0f, 0.7f), Palette.Water, 0.02f);
                fishMesh = mb.ToMesh("CaughtFish");
                fishMesh.hideFlags = HideFlags.DontSave;
            }
            return fishMesh;
        }

        // ---------------------------------------------------------------- debug (SliceDebugPanel's #364 block)
        /// <summary>"Fish bite now": the fishers at the pond get their next catch's nibble in 0.3 s; with nobody fishing, two grown-ups are sent to the pond for 2 view hours and bite on arrival.</summary>
        public static string DebugBiteNow()
        {
            var fv = Ensure();
            var pv = PeopleView.Find();
            if (fv == null || pv == null || pv.Planner == null) return "no people";
            int n = 0;
            foreach (var kv in fv.fishers)
                if (fv.clock - kv.Value.seen < 0.3f && kv.Value.left > FishBeats.BiteSeconds + FishBeats.EndSeconds + 0.5f) { fv.wantBite.Add(kv.Key); n++; }
            if (n > 0) return $"{n} fishing: the next catch bites in 0.3 s";
            int sent = pv.Planner.DebugFishNow(pv.Now);
            if (sent < 0) return "no water to fish (or nobody free)";
            foreach (int k in pv.Planner.DebugFisherKeys) fv.wantBite.Add(k);
            return $"{sent} sent to fish: the first catch bites on arrival";
        }
    }
}
