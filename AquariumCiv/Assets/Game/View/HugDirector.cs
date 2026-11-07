// User 2026-11-06 "Hugs are fine. Add them." (animation-specs B7.d; decisions.md 2026-11-06): where hugs happen in the game, sparingly so
// they stay special. The wedding's hug is the staged moment's own (MomentBeats, C3 step 6, right after the vow). This adds three more, each
// read off the people's own plans (no sim dice, no sim writes):
//   - a reunion: a hunter home with a catch (the hunt's CarryBack turns to Skin at the fire) hugs their partner, or a kid of theirs, if near;
//   - a reconcile: two rivals whose bicker the leader stepped into (Argue + Mediate) hug as it ends;
//   - a parent and a kid now and then at dawn or on the evening green (HugRules.ParentKidToday).
// The book (Life/HugRules.cs) keeps it rare: a pair once a view day, a few a day in the box by population, never two within 25 s.
// The hug itself is B7's PairStage.Hug: PeopleView stands the two on the pair's clock and lays the beat on them.
using System.Collections.Generic;
using AquariumCiv.Game;
using AquariumCiv.Life;
using AquariumCiv.Sim;
using UnityEngine;

namespace AquariumCiv.View
{
    [DefaultExecutionOrder(45)]
    public sealed class HugDirector : MonoBehaviour
    {
        public static bool AutoCreate = true;
        public static HugDirector Instance { get; private set; }
        /// <summary>How often it looks (real seconds): a hunter's arrival or a bicker's last half second is never missed.</summary>
        public const float LookEvery = 0.25f;

        readonly HugBook book = new HugBook();
        readonly Dictionary<int, HuntPhase> lastHunt = new Dictionary<int, HuntPhase>();
        readonly Dictionary<int, PersonActivity> byKey = new Dictionary<int, PersonActivity>();
        readonly HashSet<int> mediated = new HashSet<int>();
        PeopleView pv;
        SimCore boundSim;
        float clock, nextLook;

        public HugBook Book => book;
        public int Started { get; private set; }
        public HugReason LastReason { get; private set; }
        public string LastWhy { get; private set; } = "";

        static SimCore Sim
        {
            get
            {
                var c = GameClock.Instance;
                return c != null && c.Sim != null && ReferenceEquals(c.Sim, c.ActiveSim) ? c.ActiveSim : null;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (!AutoCreate || Instance != null || FindAnyObjectByType<GameClock>() == null) return;
            var b = FindAnyObjectByType<BoxView>();
            if (b != null && b.GetComponent<HugDirector>() == null) b.gameObject.AddComponent<HugDirector>();
        }

        void OnEnable()
        {
            if (Instance != null && Instance != this) { enabled = false; return; }
            Instance = this;
        }

        void OnDisable()
        {
            if (Instance != this) return;
            Instance = null;
            lastHunt.Clear();
        }

        void Update()
        {
            clock += Time.unscaledDeltaTime;
            if (clock < nextLook) return;
            nextLook = clock + LookEvery;
            Look();
        }

        /// <summary>One look now (tests; <see cref="Update"/> calls it every <see cref="LookEvery"/> s). Returns the reason of the hug it started, or null.</summary>
        public HugReason? Look()
        {
            var sim = Sim;
            if (!ReferenceEquals(sim, boundSim)) { boundSim = sim; book.Reset(); lastHunt.Clear(); }
            if (sim == null) { LastWhy = "no living box"; return null; }
            if (pv == null) pv = PeopleView.Find();
            if (pv == null || pv.Planner == null) { LastWhy = "no people"; return null; }
            var acts = pv.Activities;
            byKey.Clear();
            mediated.Clear();
            foreach (var a in acts)
            {
                byKey[a.who.Key] = a;
                if (a.kind == ActivityKind.Mediate && !a.partner.IsNone) mediated.Add(a.partner.Key);
            }
            HugReason? started = null;
            if (!BeatFlags.Hugs) { LastWhy = "hugs are off"; Remember(acts); return null; }
            string busy = HeldMomentStage.BusyReason();
            if (busy != null) { LastWhy = busy; Remember(acts); return null; }
            var planner = pv.Planner;
            var now = pv.Now;
            int pop = sim.State.population;

            foreach (var a in acts)
            {
                // A reunion: the hunter home with the catch (CarryBack → Skin at the fire).
                if (a.hunt == HuntPhase.Skin && lastHunt.TryGetValue(a.who.Key, out var was) && was == HuntPhase.CarryBack)
                {
                    var partner = planner.PartnerOf(a.who);
                    if (!partner.IsNone && TryHug(a.who, partner, HugReason.Reunion, now, pop)) { started = HugReason.Reunion; break; }
                    bool kidHug = false;
                    foreach (var k in acts)
                        if (planner.StageOf(k.who) == LifeStage.Kid && planner.ParentOf(k.who).Key == a.who.Key && TryHug(a.who, k.who, HugReason.Reunion, now, pop)) { kidHug = true; break; }
                    if (kidHug) { started = HugReason.Reunion; break; }
                }
                // A reconcile: the leader stepped into their bicker; as it ends, they hug (the lower key asks, once).
                if (a.kind == ActivityKind.Argue && !a.partner.IsNone && a.who.Key < a.partner.Key && !a.moving && a.end > now.hour
                    && LifeClock.RealSecondsBetween(now.hour, a.end) < 0.7f && (mediated.Contains(a.who.Key) || mediated.Contains(a.partner.Key))
                    && TryHug(a.who, a.partner, HugReason.Reconcile, now, pop)) { started = HugReason.Reconcile; break; }
                // A parent and their kid at dawn or on the evening green, now and then.
                if (HugRules.ParentKidHours(now.hour, out int part) && planner.StageOf(a.who) == LifeStage.Kid)
                {
                    var parent = planner.ParentOf(a.who);
                    if (!parent.IsNone && HugRules.ParentKidToday(parent.Key, a.who.Key, now.day, part, out float from) && now.hour >= from
                        && TryHug(parent, a.who, HugReason.ParentKid, now, pop)) { started = HugReason.ParentKid; break; }
                }
            }
            Remember(acts);
            return started;
        }

        void Remember(IReadOnlyList<PersonActivity> acts)
        {
            foreach (var a in acts) lastHunt[a.who.Key] = a.hunt;
        }

        /// <summary>Can this person be borrowed for a hug now: drawn, standing (not walking off), awake and out, hands not full of baby,
        /// not in a game, a pair beat, a staged moment or a tent raising; a hunter only at the fire with the catch.</summary>
        bool Free(PersonRef who, float t)
        {
            if (!byKey.TryGetValue(who.Key, out var a)) return false;
            var stage = pv.Planner.StageOf(who);
            if (stage is not (LifeStage.Kid or LifeStage.Adult or LifeStage.Elder)) return false;
            if (!pv.TryGetDollPose(who, out var pose) || pose.hidden || pose.lying) return false;
            if (a.moving || a.enRoute || !a.carrying.IsNone || !a.carriedBy.IsNone || a.tent != TentPhase.None) return false;
            if (a.hunt != HuntPhase.None && a.hunt != HuntPhase.Skin) return false;
            if (a.source == ActivitySource.Scene || a.source == ActivitySource.Reaction) return false;
            if (a.kind is ActivityKind.Sleep or ActivityKind.Nap or ActivityKind.Indoors or ActivityKind.None or ActivityKind.Swim) return false;
            if (a.hunt == HuntPhase.None && a.IsBusy) return false;
            if (PairStage.TryCue(who.Key, t, out _, out _)) return false;
            if (pv.KidPlay.TryCue(who.Key, t, out _, out _, out _)) return false;
            return true;
        }

        bool TryHug(PersonRef a, PersonRef b, HugReason reason, ViewTime now, int pop)
        {
            float t = pv.PoseClock;
            if (!book.Can(now.day, a.Key, b.Key, clock, pop, reason, out string why)) { LastWhy = $"{reason}: {why}"; return false; }
            if (!Free(a, t) || !Free(b, t)) { LastWhy = $"{reason}: not free"; return false; }
            if (!pv.TryGetDollPosition(a, out var pa) || !pv.TryGetDollPosition(b, out var pb)) return false;
            if ((pa - pb).sqrMagnitude > HugRules.Reach(reason) * HugRules.Reach(reason)) { LastWhy = $"{reason}: too far apart"; return false; }
            var ev = Stage(a, b, pa, pb, now, t, false);
            if (ev == null) { LastWhy = $"{reason}: hugs are off"; return false; }
            book.Took(now.day, a.Key, b.Key, clock, reason);
            Started++;
            LastReason = reason;
            LastWhy = "";
            return true;
        }

        /// <summary>The grown-up is the first of the pair, so a kid is the second and is lifted 0.02 u while held.</summary>
        PairEvent Stage(PersonRef a, PersonRef b, Vector2 pa, Vector2 pb, ViewTime now, float t, bool force)
        {
            var sa = pv.Planner.StageOf(a); var sb = pv.Planner.StageOf(b);
            if (sa == LifeStage.Kid && sb != LifeStage.Kid) { (a, b) = (b, a); (pa, pb) = (pb, pa); (sa, sb) = (sb, sa); }
            var ev = PairStage.Hug(PairBeats.PairSeed(a.Key, b.Key) ^ (int)now.day, t, a.Key, b.Key, pa, pb, force, sb == LifeStage.Kid && sa != LifeStage.Kid);
            if (ev != null) PairStage.Add(ev);
            return ev;
        }

        // ---------------------------------------------------------------- the panel

        public string Status() => $"hugs {(BeatFlags.Hugs ? "on" : "off")}: {book.Today} today, {Started} started, last {LastReason}" + (string.IsNullOrEmpty(LastWhy) ? "" : $" ({LastWhy})");

        public static string DebugStatus() => Instance != null ? Instance.Status() : "no hug director";

        /// <summary>The cheat "Hug: parent and kid now": the nearest parent and kid on view hug (past the book, like "Hug now").</summary>
        public static string DebugParentKid()
        {
            var i = Instance;
            if (i == null) return "no hug director";
            if (i.pv == null) i.pv = PeopleView.Find();
            if (i.pv == null || i.pv.Planner == null) return "no people";
            var planner = i.pv.Planner;
            float t = i.pv.PoseClock;
            i.byKey.Clear();
            foreach (var a in i.pv.Activities) i.byKey[a.who.Key] = a;
            PersonRef bestP = PersonRef.None, bestK = PersonRef.None; float best = float.MaxValue;
            foreach (var a in i.pv.Activities)
            {
                if (planner.StageOf(a.who) != LifeStage.Kid) continue;
                var p = planner.ParentOf(a.who);
                if (p.IsNone || !i.Free(a.who, t) || !i.Free(p, t)) continue;
                if (!i.pv.TryGetDollPosition(a.who, out var pk) || !i.pv.TryGetDollPosition(p, out var pp)) continue;
                float d = (pk - pp).sqrMagnitude;
                if (d < best) { best = d; bestP = p; bestK = a.who; }
            }
            if (bestP.IsNone) return "no parent and kid free on view (a notable family)";
            i.pv.TryGetDollPosition(bestP, out var a0); i.pv.TryGetDollPosition(bestK, out var b0);
            var ev = i.Stage(bestP, bestK, a0, b0, i.pv.Now, t, true);
            return ev != null ? $"hug: {bestP} and their kid {bestK}" : "hug: too far apart";
        }
    }
}
