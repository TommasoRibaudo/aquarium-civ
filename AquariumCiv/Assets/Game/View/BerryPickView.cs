using System.Collections.Generic;
using AquariumCiv.Game;
using AquariumCiv.Life;
using UnityEngine;

namespace AquariumCiv.View
{
    /// <summary>
    /// #361 B5 berries you can see. Listens to <see cref="PeopleView.WorkHit"/> for the berry moves (<see cref="DollStyle.PickBerry"/>,
    /// <see cref="DollStyle.DigRoot"/>, <see cref="DollStyle.TasteBerry"/>, poses in BerryBeats.cs) and answers each contact:
    /// <list type="bullet">
    /// <item>Pick: the bush loses one dot (<see cref="BushView.PickOneDot"/>: the dots on screen stay the sim's count), shakes (<see cref="BushView.Shake"/>)
    /// with a leaf puff, and a tiny berry hops in a kit arc (h 0.06, 0.3 s) from the hand into the basket, which Pops and shows one more berry on top (up to 5).</item>
    /// <item>Taste: the same dot and shake (a lighter one); the kid eats it, no arc.</item>
    /// <item>Dig: dust at the ground (small on the paw strokes, big on the pull); on the pull a root comes up into the hand, is held high 0.5 s, then arcs into the basket.</item>
    /// </list>
    /// <para>The end beat (<see cref="DollStyle.TipBasket"/>, #361 follow-up): at the stash pile a gatherer lifts the basket, rolls it forward, and 3 to 5 berries leave its rim
    /// 0.06 s apart in kit arcs (h 0.05, 0.25 s) onto the pile; the first landing is the pose's hit: the pile Squashes (<see cref="StashProps.Bump"/>) with a small
    /// puff of dust. The basket then shows empty. The basket sits on the chest (it is not on an arm), so this view turns the prop itself while <see cref="PeopleView.BasketTip"/> fires.</para>
    /// One pooled berry mesh and one root mesh; the arcs run on <see cref="ArcPool"/>. Nothing here touches the sim except the one dot a pick takes.
    /// </summary>
    [DefaultExecutionOrder(7)]
    public sealed class BerryPickView : MonoBehaviour
    {
        public static bool AutoCreate = true;
        public static BerryPickView Instance { get; private set; }

        public const float BerryRadius = 0.022f, BerryHeight = 0.06f, BerryFlight = 0.3f, RootHeight = 0.05f, RootFlight = 0.25f;
        public const int PoolSize = 16;

        sealed class Flyer
        {
            public GameObject go; public ArcHandle arc; public bool root;
            public PersonRef who; public float landAt; public bool landed;
            /// <summary>#361: a berry tipped onto the stash pile (not into a basket).</summary>
            public bool pile;
        }
        /// <summary>#361: one tip in progress: the basket's resting transform (put back when the tip stops), how many berries have left it.</summary>
        sealed class Tip
        {
            public PegDollView v; public Transform prop, parent; public Vector3 restPos; public Quaternion restRot;
            public float lastSeen, lastSince; public int launched, total;
        }
        /// <summary>A root coming up out of the ground and held in the hand, until its arc leaves.</summary>
        sealed class Held { public GameObject go; public PersonRef who; public float upAt, throwAt; public bool thrown; }
        /// <summary>The berries on one basket (its prop; a new prop is a new, empty basket).</summary>
        sealed class Fill { public Transform prop; public Transform[] dots = new Transform[BerryMotion.BasketMax]; public int count; public float[] pop = new float[BerryMotion.BasketMax]; }

        PeopleView people;
        BoxView box;
        float findT, clock;
        readonly ArcPool pool = new(PoolSize);
        readonly List<Flyer> flying = new();
        readonly List<Held> held = new();
        readonly Dictionary<int, Fill> fills = new();
        readonly Dictionary<int, Tip> tips = new();
        readonly List<int> tipDone = new();
        StashProps stash;
        Mesh berryMesh, rootMesh;

        // for tests and the debug panel
        public int ArcsLaunched { get; private set; }
        public int RootsPulled { get; private set; }
        public int HitsSeen { get; private set; }
        public int LiveFlyers => flying.Count;
        /// <summary>#361: tips begun, berries sent to the pile, and the pile's answers (the tip's hit frames).</summary>
        public int TipsSeen { get; private set; }
        public int PileArcs { get; private set; }
        public int PileHits { get; private set; }
        public int TipsInProgress => tips.Count;

        /// <summary>The berries shown in this person's basket now (tests).</summary>
        public int BasketCount(PersonRef who) => fills.TryGetValue(who.Key, out var f) && f.prop != null ? f.count : 0;

        public static BerryPickView Ensure()
        {
            if (Instance != null) return Instance;
            var b = FindAnyObjectByType<BoxView>();
            return b != null ? (b.GetComponent<BerryPickView>() ?? b.gameObject.AddComponent<BerryPickView>()) : null;
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
            if (people != null) { people.WorkHit -= OnWorkHit; people.BasketTip -= OnBasketTip; }
            people = null;
            foreach (var t in tips.Values) RestoreBasket(t);
            tips.Clear();
            foreach (var f in flying) if (f.go != null) Destroy(f.go);
            foreach (var h in held) if (h.go != null) Destroy(h.go);
            flying.Clear(); held.Clear(); fills.Clear();
            if (berryMesh != null) Destroy(berryMesh);
            if (rootMesh != null) Destroy(rootMesh);
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
                    if (people != null) { people.WorkHit -= OnWorkHit; people.BasketTip -= OnBasketTip; }
                    people = pv;
                    if (people != null) { people.WorkHit += OnWorkHit; people.BasketTip += OnBasketTip; }
                }
                if (stash == null) stash = FindAnyObjectByType<StashProps>();
            }
            StepTips();
            StepHeld();
            StepFlyers();
            StepFills(dt);
        }

        // ---------------------------------------------------------------- the contact
        void OnWorkHit(PersonRef who, Place place, DollStyle style, Vector3 at)
        {
            if (!BerryMotion.IsBerryStyle(style) || people == null) return;
            HitsSeen++;
            if (!people.TryGetBerryDoll(who, out var v, out float poseClock)) return;
            var away = Vector3.forward;
            if (people.TryGetDollFeet(who, out var feet)) { var d = new Vector3(at.x - feet.x, 0f, at.z - feet.z); if (d.sqrMagnitude > 1e-6f) away = d.normalized; }
            switch (style)
            {
                case DollStyle.PickBerry: Pick(who, place, v, at, away, true); break;
                case DollStyle.TasteBerry: Pick(who, place, v, at, away, false); break;
                case DollStyle.DigRoot: Dig(who, v, at, away, BerryMotion.IsRootPull(poseClock)); break;
                case DollStyle.TipBasket: PileAnswers(at, away); break;
            }
        }

        void Pick(PersonRef who, Place place, PegDollView v, Vector3 hand, Vector3 away, bool toBasket)
        {
            var bush = BushView.Instance;
            if (bush == null || place.kind != PlaceKind.BushCell) return;
            bush.Shake(place.index, away); // the bush answers on the contact frame
            HitFx.Puff(PuffKind.Leaves, hand, away, toBasket ? 1f : 0.7f);
            if (!bush.PickOneDot(place.index, who, out var dot)) return; // nothing left to take: just the shake
            if (!toBasket) return;
            var f = FillOf(who.Key, v);
            if (f == null) return;
            Launch(who, hand, BasketTop(f.prop), BerryHeight, BerryFlight, false);
        }

        void Dig(PersonRef who, PegDollView v, Vector3 hand, Vector3 away, bool pull)
        {
            var o = PuffOptions.Default;
            if (box != null && box.Root != null)
            {
                var local = box.Root.InverseTransformPoint(hand);
                o.groundY = box.Root.TransformPoint(new Vector3(local.x, box.SurfaceHeight(local.x, local.z), local.z)).y;
            }
            HitFx.Puff(PuffKind.Dust, hand, away, pull ? 1.5f : 1f, o);
            if (!pull) return;
            RootsPulled++;
            var go = NewPiece("Root", RootMesh());
            held.Add(new Held { go = go, who = who, upAt = clock + BerryMotion.RootUpAfterSeconds, throwAt = clock + BerryMotion.RootHeldSeconds });
            go.SetActive(false);
        }

        // ---------------------------------------------------------------- the things in flight
        // ---------------------------------------------------------------- the tip at the stash (#361)
        static readonly Vector2[] PileSpread = { new(0f, 0f), new(0.022f, 0.012f), new(-0.02f, 0.014f), new(0.008f, -0.022f), new(-0.014f, -0.012f) };

        void OnBasketTip(PersonRef who, PegDollView v, float since)
        {
            if (v == null || v.prop == null || v.propKind != PropKind.Basket) return;
            if (!tips.TryGetValue(who.Key, out var t) || t.prop != v.prop || since < t.lastSince - 0.05f)
            {
                if (t != null) RestoreBasket(t);
                var f0 = FillOf(who.Key, v);
                if (f0 == null) return;
                t = new Tip { v = v, prop = v.prop, parent = v.prop.parent, restPos = v.prop.localPosition, restRot = v.prop.localRotation };
                t.total = BerryMotion.TipBerries(f0.count);
                if (f0.count < t.total) { for (int i = f0.count; i < t.total; i++) f0.pop[i] = 0.0001f; f0.count = t.total; } // a basket we never saw filled: the tip still pours
                tips[who.Key] = t;
                TipsSeen++;
            }
            t.lastSeen = clock; t.lastSince = since;
            // the basket comes up off the chest and rolls forward over the pile
            float lift = BerryMotion.TipLift(since), roll = BerryMotion.TipRoll(since);
            t.prop.localPosition = t.restPos + new Vector3(0f, BerryMotion.TipLiftHeight * lift * v.scale, 0f);
            // in the left hand the basket is kept upright against the arm every frame (PegDollView.ApplyArms): roll from that
            var upright = v.BasketIsInLeft ? Quaternion.Inverse(v.armL.localRotation) : t.restRot;
            t.prop.localRotation = Quaternion.Euler(-roll, 0f, 0f) * upright;
            if (t.launched >= t.total) return;
            var fill = FillOf(who.Key, v);
            while (t.launched < t.total && since >= BerryMotion.TipLaunchAt(t.launched))
            {
                int i = t.launched++;
                var rim = BasketTop(t.prop);
                var to = PileSpot(rim, i, v);
                if (fill != null && fill.count > 0) fill.count--; // that dot leaves the basket
                var go = NewPiece("Berry", BerryMesh());
                var shot = ArcShot.Throw(rim, to, 0.05f, BerryMotion.TipFlight, Landing.DropIn, clock, 360f, BerryRadius);
                var fl = new Flyer { go = go, arc = pool.Launch(shot), who = who, landAt = shot.LandAt, pile = true };
                flying.Add(fl);
                go.transform.position = rim;
                ArcsLaunched++; PileArcs++;
            }
        }

        /// <summary>Where berry <paramref name="i"/> lands: on the pile's top (a little spread), else a hand's length in front of the basket.</summary>
        Vector3 PileSpot(Vector3 rim, int i, PegDollView v)
        {
            var spread = PileSpread[i % PileSpread.Length] * DollScale();
            if (stash != null && stash.TryGetPileTop(out var top)) return top + new Vector3(spread.x, 0f, spread.y);
            return rim + v.transform.forward * 0.1f + Vector3.down * 0.05f;
        }

        /// <summary>The first berry lands: the pile squashes and a small puff of dust goes up from its top.</summary>
        void PileAnswers(Vector3 hand, Vector3 away)
        {
            PileHits++;
            var at = hand;
            var o = PuffOptions.Default;
            if (stash != null)
            {
                stash.Bump();
                if (stash.TryGetPileTop(out var top)) { at = top; o.groundY = top.y - 0.04f; }
            }
            HitFx.Puff(PuffKind.Dust, at, away, 0.6f, o);
        }

        void StepTips()
        {
            if (tips.Count == 0) return;
            tipDone.Clear();
            foreach (var kv in tips) if (clock - kv.Value.lastSeen > 0.12f || kv.Value.prop == null) tipDone.Add(kv.Key); // the tip stopped (the block ended, the doll walked off)
            foreach (var k in tipDone) { RestoreBasket(tips[k]); tips.Remove(k); }
        }

        static void RestoreBasket(Tip t)
        {
            if (t == null || t.prop == null || t.prop.parent != t.parent) return; // moved to the other hand since: PegDollView put it right
            t.prop.localPosition = t.restPos; t.prop.localRotation = t.restRot;
        }

        void Launch(PersonRef who, Vector3 from, Vector3 to, float h, float flight, bool root)
        {
            var go = NewPiece(root ? "Root" : "Berry", root ? RootMesh() : BerryMesh());
            var shot = ArcShot.Throw(from, to, h, flight, Landing.DropIn, clock, root ? 0f : 360f, BerryRadius);
            var fl = new Flyer { go = go, arc = pool.Launch(shot), root = root, who = who, landAt = shot.LandAt };
            flying.Add(fl);
            if (!root) ArcsLaunched++;
            go.transform.position = from;
        }

        void StepHeld()
        {
            for (int i = held.Count - 1; i >= 0; i--)
            {
                var h = held[i];
                if (h.go == null || people == null || !people.TryGetBerryDoll(h.who, out var v, out _) || v.armR == null) { Drop(h, i); continue; }
                var hand = WorkHitPoint.HitPoint(v);
                if (clock >= h.throwAt)
                {
                    var f = FillOf(h.who.Key, v);
                    Vector3 end = f != null ? BasketTop(f.prop) : hand;
                    Destroy(h.go); held.RemoveAt(i);
                    Launch(h.who, hand, end, RootHeight, RootFlight, true);
                    continue;
                }
                if (clock < h.upAt) continue;
                if (!h.go.activeSelf) h.go.SetActive(true);
                float u = Mathf.Clamp01((clock - h.upAt) / 0.3f); // it comes up out of the ground into the held-up hand
                h.go.transform.position = hand + Vector3.up * 0.03f * u;
                h.go.transform.rotation = Quaternion.Euler(0f, clock * 40f, 8f);
                h.go.transform.localScale = Vector3.one * (Mathf.Lerp(0.3f, 1f, u) * (people != null ? v.transform.localScale.x : 1f));
            }
        }

        void Drop(Held h, int i) { if (h.go != null) Destroy(h.go); held.RemoveAt(i); }

        void StepFlyers()
        {
            for (int i = flying.Count - 1; i >= 0; i--)
            {
                var f = flying[i];
                if (f.go == null || !pool.Sample(f.arc, clock, out var s)) { Remove(f, i); continue; }
                float zoom = DollScale();
                f.go.transform.position = s.position;
                f.go.transform.rotation = s.rotation;
                f.go.transform.localScale = Vector3.one * zoom;
                if (!f.landed && clock >= f.landAt)
                {
                    f.landed = true;
                    Landed(f);
                    Remove(f, i);
                }
            }
        }

        void Landed(Flyer f)
        {
            if (f.pile) { if (stash != null) stash.Bump(0.08f); return; } // a tipped berry lands on the pile, not in a basket
            if (people == null || !people.TryGetBerryDoll(f.who, out var v, out _)) return;
            var fill = FillOf(f.who.Key, v);
            if (fill == null) return;
            if (fill.count < BerryMotion.BasketMax) { fill.pop[fill.count] = 0.0001f; fill.count++; }
            fill.pop[Mathf.Clamp(fill.count - 1, 0, BerryMotion.BasketMax - 1)] = 0.0001f; // a Pop on the newest dot
            if (f.root) fill.pop[Mathf.Clamp(fill.count - 1, 0, BerryMotion.BasketMax - 1)] = 0.0001f;
        }

        void Remove(Flyer f, int i)
        {
            if (f.go != null) Destroy(f.go);
            pool.Release(f.arc);
            flying.RemoveAt(i);
        }

        // ---------------------------------------------------------------- the basket
        static readonly Vector3[] DotSpots =
        {
            new(0.000f, 0.0f, 0.000f), new(0.016f, 0.0f, 0.008f), new(-0.014f, 0.0f, 0.010f), new(0.004f, 0.0f, -0.016f), new(-0.010f, 0.0f, -0.010f),
        };

        Fill FillOf(int key, PegDollView v)
        {
            if (v == null || v.prop == null || v.propKind != PropKind.Basket) return null;
            if (!fills.TryGetValue(key, out var f)) fills[key] = f = new Fill();
            if (f.prop != v.prop)
            {
                // a new basket (or the first sight of one): empty, with its dots hung on it
                f.prop = v.prop; f.count = 0;
                for (int i = 0; i < f.dots.Length; i++)
                {
                    var t = NewPiece("BasketBerry", BerryMesh()).transform;
                    t.SetParent(v.prop, false);
                    t.localPosition = DotSpots[i] + new Vector3(0f, 0.034f + (i == 0 ? 0.008f : 0f), 0f);
                    t.localScale = Vector3.zero;
                    f.dots[i] = t; f.pop[i] = -1f;
                }
            }
            return f;
        }

        void StepFills(float dt)
        {
            foreach (var kv in fills)
            {
                var f = kv.Value;
                if (f.prop == null) continue;
                for (int i = 0; i < f.dots.Length; i++)
                {
                    if (f.dots[i] == null) continue;
                    float s;
                    if (i >= f.count) s = 0f;
                    else if (f.pop[i] > 0f)
                    {
                        f.pop[i] += dt;
                        float u = f.pop[i] / 0.15f; // Easing.Pop: 0 -> 1.2 -> 1 over 0.15 s
                        s = u >= 1f ? 1f : Easing.Pop(u);
                        if (u >= 1f) f.pop[i] = -1f;
                    }
                    else s = 1f;
                    f.dots[i].localScale = Vector3.one * (s * 0.6f); // dots are built at the prop's scale: a basket-sized berry is ~0.6 of the flying one
                    if (f.dots[i].gameObject.activeSelf != (s > 0f)) f.dots[i].gameObject.SetActive(s > 0f);
                }
            }
        }

        static Vector3 BasketTop(Transform prop) => prop.TransformPoint(new Vector3(0f, 0.04f, 0f));

        // ---------------------------------------------------------------- the pieces
        float DollScale() => people != null ? people.DollZoom : 1f;

        GameObject NewPiece(string name, Mesh mesh)
        {
            var go = new GameObject(name) { hideFlags = HideFlags.DontSave };
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = box != null ? box.SolidMaterial : null;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }

        Mesh BerryMesh()
        {
            if (berryMesh == null)
            {
                var mb = new MeshBuilder(361) { jitter = 0f };
                mb.Ico(Vector3.zero, BerryRadius, Vector3.one, new Color(0.78f, 0.1f, 0.15f), 0.1f);
                berryMesh = mb.ToMesh("PickedBerry");
            }
            return berryMesh;
        }

        Mesh RootMesh()
        {
            if (rootMesh == null)
            {
                var mb = new MeshBuilder(362) { jitter = 0f };
                // a tan carrot 0.06 long, thick at the top, a leaf tuft at the crown
                mb.Prism(new Vector3(0f, -0.03f, 0f), 0.003f, 0.014f, 0.06f, 6, new Color(0.82f, 0.6f, 0.34f));
                mb.Ico(new Vector3(0f, 0.035f, 0f), 0.012f, new Vector3(0.8f, 1.2f, 0.8f), new Color(0.35f, 0.55f, 0.25f), 0.1f);
                rootMesh = mb.ToMesh("PickedRoot");
            }
            return rootMesh;
        }

        // ---------------------------------------------------------------- debug (SliceDebugPanel's #361 block)
        /// <summary>"Pickers to the nearest bush now": three grown-ups and a kid go to the bush nearest the camp for 0.8 view hours.</summary>
        public static string DebugPickersToBush()
        {
            var pv = PeopleView.Find();
            if (pv == null || pv.Planner == null) return "no PeopleView";
            int cell = pv.Planner.DebugPickersNow(pv.Now);
            return cell < 0 ? "no bush (or nobody free)" : $"pickers to bush {cell}";
        }

        /// <summary>"Tip at the stash now": three grown-ups walk to the camp's stash pile and tip a basket onto it (camp only).</summary>
        public static string DebugTipNow()
        {
            var pv = PeopleView.Find();
            if (pv == null || pv.Planner == null) return "no PeopleView";
            int n = pv.Planner.DebugTipNow(pv.Now);
            return n < 0 ? "no stash pile (camp era only)" : n == 0 ? "nobody free" : $"{n} tipping baskets at the stash";
        }

        /// <summary>"Root dig now": the same, at the dry stream bed: three grown-ups dig roots for 0.8 view hours.</summary>
        public static string DebugRootDigNow()
        {
            var pv = PeopleView.Find();
            if (pv == null || pv.Planner == null) return "no PeopleView";
            int n = pv.Planner.DebugRootDigNow(pv.Now);
            return n <= 0 ? "nobody free" : $"{n} digging roots";
        }
    }
}
