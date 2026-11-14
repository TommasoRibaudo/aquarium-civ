using System.Collections.Generic;
using AquariumCiv.Game;
using AquariumCiv.Life;
using AquariumCiv.Sim;
using AquariumCiv.Sim.Settle;
using UnityEngine;

namespace AquariumCiv.View
{
    /// <summary>
    /// #656/#677 (the user, 2026-11-14; docs/overnight/new-start-spec.md): the new start, as the Keeper sees it.
    /// <list type="bullet">
    /// <item><b>The arrival.</b> On a new living-land box the founders stand together at the glass's edge while the glass is still misted,
    /// then walk in to where the camp will be: one carries the basket of food, one hugs the bundle of sticks, the rest their rolled things
    /// on their backs. At the camp spot the two crouch and set them down beside the pile, and that is <see cref="SimCore.SetDownStock"/>:
    /// the stock shows from here (<see cref="StashProps"/> keeps the pile hidden until then). A line in their words, then they go about
    /// their day. Tests that skip the first run turn <see cref="AutoArrival"/> off: the stock is simply set down.</item>
    /// <item><b>The fire</b> as the land lets it burn (<see cref="SimCore.FireStage"/>): none before the first bush, a small fire of dry
    /// grass and twigs, the log fire from the first wild wood (<see cref="SimLayout.FireStageShown"/>; the box is refreshed when it changes).</item>
    /// <item><b>The first wild wood</b> (<see cref="SimCore.FirstWildWoodPending"/>), once, watched: two grown-ups walk out to the wild tree,
    /// pick up fallen wood and carry the first logs home over their shoulders; they set them on the pile beside the bundle (the pile held
    /// its logs back till then) and the fire is built up as a log fire. A line, a short camera lean, and
    /// <see cref="SimCore.MarkFirstWildWoodSeen"/>. If it came while the Keeper was away it plays on the next visit.</item>
    /// <item><b>Hunger</b> (<see cref="SimCore.Hungry"/>): shown, never punished. Now and then someone standing stops and looks up at the sky
    /// (and the planner walks them a little slower, <see cref="LifePlanner.HungryPace"/>).</item>
    /// </list>
    /// </summary>
    [DefaultExecutionOrder(8)]
    public class NewStartView : MonoBehaviour
    {
        public static bool AutoCreate = true;
        /// <summary>False: the stock is set down at once, with no walk-in (tests that skip the first run).</summary>
        public static bool AutoArrival = true;
        public static NewStartView Instance { get; private set; }

        /// <summary>Proposed lines, in the people's words (the user signs them off). Closed world: the box is all there is.</summary>
        public static readonly string[] ArrivalLines =
        {
            "Here. Set the basket down, and the sticks with it.",
            "This is the spot. Put our things down.",
        };
        public static readonly string[] FirstWoodLines =
        {
            "Real wood, from the wild tree. Tonight the fire gets logs.",
            "Logs! Now the fire can be a big one.",
        };

        /// <summary>Real seconds: the set-down crouch, the line's wait, the first-wood pick-up and the longest either scene may run.</summary>
        public const float SetDownSeconds = 1.4f, SettleSeconds = 2.2f, PickUpSeconds = 1.3f, MaxArrivalSeconds = 40f, MaxFirstWoodSeconds = 40f;
        /// <summary>Hungry: every person looks up for <see cref="LookUpSeconds"/> once in each <see cref="LookUpEvery"/> real seconds.</summary>
        public const float LookUpEvery = 9f, LookUpSeconds = 2.2f;

        sealed class Member
        {
            public PersonRef who;
            public readonly List<Vector2> path = new();
            public float along, delay, speed;
            public Vector2 cur, faceTo;
            public PropKind prop; public PropSlot slot;
            public DollStyle style = DollStyle.Stand;
            public int leg; // first wood: 0 out to the tree, 1 the pick-up, 2 home, 3 the set-down, 4 done
            public float legT;
            public bool carrier, arrived;
        }

        enum ArrivalPhase { None, Wait, Walk, SetDown, Settle }

        BoxView box;
        readonly Dictionary<int, Member> held = new();
        readonly List<Member> arrival = new(), wood = new();
        ArrivalPhase aPhase;
        float aT, fwT = -1f, setDownAt = -1f;
        Vector2 fire, pile;
        bool fwSetDown, lined;
        CampFireStage drawnStage = (CampFireStage)(-1);
        SimCore bound;
        readonly List<PersonRef> whoBuf = new(); readonly List<float> px = new(), pz = new();

        // ---------------------------------------------------------------- for tests and the panel
        public int ArrivalsPlayed { get; private set; }
        public int FirstWoodPlayed { get; private set; }
        public bool ArrivalPlaying => aPhase != ArrivalPhase.None;
        public bool ArrivalWaiting => aPhase == ArrivalPhase.Wait;
        public bool FirstWoodPlaying => fwT >= 0f;
        public string LastLine { get; private set; } = "";
        public int Leans { get; private set; }
        /// <summary>The fire stage the box draws now.</summary>
        public CampFireStage FireDrawn => drawnStage;
        /// <summary>Who is held by a scene now, and what they hold (tests).</summary>
        public int HeldCount => held.Count;
        public bool Holds(PersonRef who, out PropKind prop) { prop = PropKind.None; if (!held.TryGetValue(who.Key, out var m)) return false; prop = m.prop; return true; }
        public IEnumerable<PersonRef> HeldPeople { get { foreach (var m in held.Values) yield return m.who; } }

        SimCore Sim => GameClock.Instance != null ? GameClock.Instance.Sim : null;

        public static NewStartView Ensure(BoxView b)
        {
            if (b == null) return null;
            var v = b.GetComponent<NewStartView>();
            return v != null ? v : b.gameObject.AddComponent<NewStartView>();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (!AutoCreate || Instance != null || FindAnyObjectByType<GameClock>() == null) return;
            Ensure(FindAnyObjectByType<BoxView>());
        }

        void OnEnable()
        {
            if (Instance != null && Instance != this) { enabled = false; return; }
            Instance = this;
            box = GetComponent<BoxView>();
            PeopleView.StartHook = Hook;
            PeopleView.HungryLook = LookingUp;
            SimLayout.FireStageShown = StageShown;
        }

        void OnDisable()
        {
            if (Instance == this) Instance = null;
            if (caughtUpBound && GameClock.Instance != null) GameClock.Instance.CaughtUp -= OnReturn;
            caughtUpBound = false;
            if (PeopleView.StartHook == (PeopleView.StartCueHook)Hook) PeopleView.StartHook = null;
            if (PeopleView.HungryLook == (System.Func<PersonRef, float, bool>)LookingUp) PeopleView.HungryLook = null;
            if (SimLayout.FireStageShown == (System.Func<SimCore, CampFireStage>)StageShown) SimLayout.FireStageShown = null;
            StashProps.HoldLogs = false;
            EndArrival(); EndFirstWood();
            drawnStage = (CampFireStage)(-1);
        }

        // ---------------------------------------------------------------- the hooks
        bool Hook(in PersonActivity a, out PeopleView.StartCue cue)
        {
            cue = default;
            if (!held.TryGetValue(a.who.Key, out var m)) return false;
            cue.pos = m.cur;
            cue.face = m.faceTo;
            cue.style = m.style;
            cue.prop = m.prop;
            cue.slot = m.slot;
            return true;
        }

        bool LookingUp(PersonRef who, float time)
        {
            var sim = Sim;
            if (sim == null || !sim.NewStartOn || !sim.Hungry) return false;
            float off = ((uint)(who.Key * 7919 + 104729) % 1000u) / 1000f * LookUpEvery;
            return Mathf.Repeat(Time.unscaledTime + off, LookUpEvery) < LookUpSeconds;
        }

        /// <summary>The fire the box draws: the sim's, except while the first logs are on their way (and not yet seen): the grass fire.</summary>
        CampFireStage StageShown(SimCore sim)
        {
            var st = sim.FireStage;
            if (st == CampFireStage.Logs && sim.NewStartOn && sim.State.newStart.fireOverride < 0 && (sim.FirstWildWoodPending && !fwSetDown))
                return sim.BerriesGrow ? CampFireStage.GrassAndTwig : CampFireStage.None;
            return st;
        }

        // ---------------------------------------------------------------- the frame
        void Update()
        {
            var sim = Sim;
            if (sim != bound) { EndArrival(); EndFirstWood(); bound = sim; drawnStage = (CampFireStage)(-1); }
            if (sim == null || !sim.EcologyOn || box == null || box.Root == null) return;
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.1f);
            var pv = PeopleView.Find();

            // the fire: refresh the box when the stage it draws changes (a bush came, the first logs, the cheat)
            var stage = sim.State.era == Era.Camp ? StageShown(sim) : CampFireStage.Logs;
            if (stage != drawnStage)
            {
                bool first = (int)drawnStage < 0;
                drawnStage = stage;
                if (!first || sim.NewStartOn) box.Refresh(); // the first look too: the box may have been built before this hook was in
            }

            if (!sim.NewStartOn) return;
            if (!sim.StockSetDown && aPhase == ArrivalPhase.None)
            {
                if (!AutoArrival || sim.State.era != Era.Camp) sim.SetDownStock();
                else BeginArrival(sim, pv);
            }
            if (aPhase != ArrivalPhase.None) StepArrival(sim, pv, dt);

            StepArrivalLine(sim, dt);
            // the first logs lie on the pile only once they are carried home and set down (tester 2026-11-14: they were there before the beat)
            StashProps.HoldLogs = sim.FirstWildWoodPending && !fwSetDown && sim.State.newStart.fireOverride < 0;
            StepHunger(sim, pv, dt);
            if (fwT < 0f && sim.FirstWildWoodPending && !sim.CatchingUp && aPhase == ArrivalPhase.None && sim.StockSetDown && !PromptUp() && !StageBusy(sim))
            {
                if (sim.State.era != Era.Camp || pv == null || pv.Planner == null || !pv.Planner.TryGetStash(out _)) sim.MarkFirstWildWoodSeen(); // past the camp: nothing to show at a pile
                else BeginFirstWood(sim, pv);
            }
            if (fwT >= 0f) StepFirstWood(sim, pv, dt);
        }

        /// <summary>The first run still has the glass misted or its own mist prompt up (tester 2026-11-14: the "Hold to mist" ring sat
        /// on the walking group): the founders wait at the edge until the first mist is done.</summary>
        static bool FirstRunBusy()
        {
            var fr = FirstRunView.Instance;
            return fr != null && fr.Active && fr.Step is FirstRunStep.Arrive or FirstRunStep.Wipe or FirstRunStep.Rain; // (Name comes after Rain on a living land, but is a lower number)
        }

        /// <summary>A first-run prompt or sheet is on screen: a line waits for it to clear.</summary>
        static bool PromptUp()
        {
            var fr = FirstRunView.Instance;
            return fr != null && fr.Active;
        }

        /// <summary>Any other moment or beat has the stage (a wedding, a staged moment, a placed-life arrival, Show me): the first logs wait.</summary>
        static bool StageBusy(SimCore sim)
        {
            var mp = MomentPlayer.Instance;
            if (mp != null && mp.Playing) return true;
            if (StagedMoments.Playing > 0 || StagedMoments.PlayingMoment(sim) != null) return true;
            var show = ShowMeDirector.Instance;
            if (show != null && show.Busy) return true;
            var life = LifeArrivalView.Instance;
            if (life != null && life.AnyPlaying) return true;
            var water = FirstWaterView.Instance;
            return water != null && water.Playing;
        }

        // ---------------------------------------------------------------- the arrival
        /// <summary>Starts the walk-in (also the panel's "arrival again", after <see cref="SimCore.DebugArrivalAgain"/>).</summary>
        public bool BeginArrival(SimCore sim, PeopleView pv)
        {
            EndArrival();
            if (pv == null || pv.Planner == null || !pv.Planner.TryGetStash(out var look) || pv.Activities.Count == 0) return false;
            pile = new Vector2(look.x, look.z);
            fire = pile + new Vector2(look.faceX, look.faceZ);
            // in from the glass behind the camp, as the camera sees it, so they walk towards the Keeper
            var cam = Camera.main;
            Vector2 away = new Vector2(0f, 1f);
            if (cam != null) { var f = box.transform.InverseTransformDirection(cam.transform.forward); away = new Vector2(f.x, f.z); if (away.sqrMagnitude < 1e-4f) away = Vector2.up; away.Normalize(); }
            var entry = fire + away * 1.5f;
            entry.x = Mathf.Clamp(entry.x, -Land.HX + 0.25f, Land.HX - 0.25f);
            entry.y = Mathf.Clamp(entry.y, -Land.HZ + 0.25f, Land.HZ - 0.25f);
            var along = (fire - entry); float len = along.magnitude;
            var dir = len > 1e-3f ? along / len : Vector2.down;
            var side = new Vector2(-dir.y, dir.x);
            int n = 0, grown = 0;
            foreach (var a in pv.Activities)
            {
                var st = pv.Planner.StageOf(a.who);
                if (st == LifeStage.Baby || st == LifeStage.None || n >= 9) continue;
                bool adult = st != LifeStage.Kid;
                var m = new Member { who = a.who, speed = LifePlanner.RealWalkSpeed * (adult ? 1f : 1.05f) };
                if (adult && a.carrying.IsNone && grown < 2)
                {
                    m.carrier = true;
                    if (grown == 0) { m.prop = PropKind.FoodBasket; m.slot = PropSlot.Hand; }
                    else { m.prop = PropKind.StickBundle; m.slot = PropSlot.Arms; }
                    grown++;
                }
                else if (adult) { m.prop = PropKind.Bundle; m.slot = PropSlot.Back; }
                // a loose file: the two carriers lead, the rest follow, a little to either side
                float back = n * 0.14f, sideways = (n % 2 == 0 ? 1f : -1f) * (0.05f + 0.03f * (n % 3));
                var start = entry - dir * back + side * sideways;
                Vector2 end;
                if (m.carrier)
                {
                    // to the pile's side, where the things are set down (StashProps.Carried*Spot)
                    var rot = Quaternion.Euler(0f, Mathf.Atan2(look.faceX, look.faceZ) * Mathf.Rad2Deg, 0f);
                    var spot = rot * (m.prop == PropKind.FoodBasket ? StashProps.CarriedBasketSpot : StashProps.CarriedBundleSpot);
                    var thing = pile + new Vector2(spot.x, spot.z);
                    var outward = (thing - fire).normalized;
                    end = thing + outward * 0.09f;
                    m.faceTo = thing;
                }
                else
                {
                    float ang = Mathf.Atan2(-dir.y, -dir.x) + (n - 2) * 0.55f;
                    end = fire + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * 0.34f;
                    m.faceTo = fire;
                }
                m.path.Add(start); m.path.Add(end);
                m.cur = start;
                m.delay = n * 0.12f;
                arrival.Add(m);
                held[a.who.Key] = m;
                n++;
            }
            if (arrival.Count == 0) { EndArrival(); return false; }
            aPhase = ArrivalPhase.Wait; aT = 0f; lined = false; setDownAt = -1f;
            ArrivalsPlayed++;
            return true;
        }

        void StepArrival(SimCore sim, PeopleView pv, float dt)
        {
            aT += dt;
            if (aT > MaxArrivalSeconds + (aPhase == ArrivalPhase.Wait ? 600f : 0f)) { sim.SetDownStock(); EndArrival(); return; }
            switch (aPhase)
            {
                case ArrivalPhase.Wait:
                    foreach (var m in arrival) { m.style = DollStyle.Stand; m.faceTo = m.path[m.path.Count - 1]; }
                    if (!FirstRunBusy()) { aPhase = ArrivalPhase.Walk; aT = 0f; LeanArrival(); }
                    break;
                case ArrivalPhase.Walk:
                {
                    bool all = true;
                    foreach (var m in arrival)
                    {
                        if (aT < m.delay) { all = false; continue; }
                        if (!Advance(m, dt)) all = false;
                        else { m.style = m.carrier ? DollStyle.Stand : DollStyle.Stand; }
                    }
                    if (all) { aPhase = ArrivalPhase.SetDown; aT = 0f; }
                    break;
                }
                case ArrivalPhase.SetDown:
                    foreach (var m in arrival) if (m.carrier) m.style = DollStyle.Crouch;
                    if (aT >= SetDownSeconds * 0.6f && setDownAt < 0f)
                    {
                        setDownAt = aT;
                        foreach (var m in arrival) if (m.carrier) { m.prop = PropKind.None; m.slot = PropSlot.Hand; }
                        sim.SetDownStock();
                        var stash = box.GetComponent<StashProps>();
                        if (pv != null && pv.Planner != null && stash != null) { stash.Sync(pv.Planner); stash.Bump(); }
                    }
                    if (aT >= SetDownSeconds) { aPhase = ArrivalPhase.Settle; aT = 0f; foreach (var m in arrival) { m.style = DollStyle.Stand; m.faceTo = fire; } }
                    break;
                case ArrivalPhase.Settle:
                    if (!lined) { lined = true; lineWho = arrival.Count > 0 ? arrival[0].who : default; foreach (var m in arrival) if (m.who.IsNotable) { lineWho = m.who; break; } lineDue = 60f; }
                    if (aT >= SettleSeconds) EndArrival();
                    break;
            }
        }

        void EndArrival()
        {
            foreach (var m in arrival) held.Remove(m.who.Key);
            arrival.Clear();
            aPhase = ArrivalPhase.None;
        }

        /// <summary>Walks a member along their path at their pace; true when they are at its end.</summary>
        static bool Advance(Member m, float dt)
        {
            float total = 0f;
            for (int i = 1; i < m.path.Count; i++) total += (m.path[i] - m.path[i - 1]).magnitude;
            m.along = Mathf.Min(total, m.along + m.speed * dt);
            float left = m.along;
            for (int i = 1; i < m.path.Count; i++)
            {
                float seg = (m.path[i] - m.path[i - 1]).magnitude;
                if (left <= seg || i == m.path.Count - 1) { m.cur = Vector2.Lerp(m.path[i - 1], m.path[i], seg > 1e-5f ? Mathf.Clamp01(left / seg) : 1f); break; }
                left -= seg;
            }
            m.arrived = m.along >= total - 1e-4f;
            return m.arrived;
        }

        // ---------------------------------------------------------------- the first wild wood
        /// <summary>Plays the first-logs beat (also the panel's cheat once <see cref="SimCore.DebugFirstWildWoodNow"/> made it due). False
        /// when there is nobody to carry or no pile.</summary>
        public bool BeginFirstWood(SimCore sim, PeopleView pv)
        {
            EndFirstWood();
            if (pv == null || pv.Planner == null || !pv.Planner.TryGetStash(out var look)) return false;
            pile = new Vector2(look.x, look.z);
            fire = pile + new Vector2(look.faceX, look.faceZ);
            int cell = sim.DeadfallTreeCell(fire.x, fire.y);
            if (cell < 0) cell = sim.WoodsTreeCell(fire.x, fire.y);
            Vector2 tree = cell >= 0 ? new Vector2(SiteMap.CXc(cell), SiteMap.CZc(cell)) : fire + (fire.sqrMagnitude > 1e-3f ? -fire.normalized : Vector2.right) * 1.2f;
            var toCamp = (fire - tree); float far = toCamp.magnitude;
            var dir = far > 1e-3f ? toCamp / far : Vector2.right;
            var side = new Vector2(-dir.y, dir.x);
            var rot = Quaternion.Euler(0f, Mathf.Atan2(look.faceX, look.faceZ) * Mathf.Rad2Deg, 0f);
            pv.StandingNow(whoBuf, px, pz);
            // two grown-ups, the nearest to the fire who stand out in the box
            var picks = new List<(float d, int i)>();
            for (int i = 0; i < whoBuf.Count; i++)
            {
                var st = pv.Planner.StageOf(whoBuf[i]);
                if (st != LifeStage.Adult && st != LifeStage.Elder) continue;
                if (held.ContainsKey(whoBuf[i].Key)) continue;
                picks.Add(((new Vector2(px[i], pz[i]) - fire).sqrMagnitude, i));
            }
            picks.Sort((a, b) => a.d.CompareTo(b.d));
            for (int k = 0; k < picks.Count && k < 2; k++)
            {
                int i = picks[k].i;
                var m = new Member { who = whoBuf[i], speed = LifePlanner.RealWalkSpeed, carrier = true };
                var at = new Vector2(px[i], pz[i]);
                var under = tree + dir * 0.16f + side * (k == 0 ? 0.06f : -0.06f);
                var s = rot * (StashProps.LogSpots[0] + new Vector3(k == 0 ? 0.03f : -0.03f, 0f, 0f));
                var home = pile + new Vector2(s.x, s.z) + (pile - fire).normalized * 0.11f;
                m.path.Add(at); m.path.Add(under);
                m.cur = at; m.faceTo = tree;
                m.leg = 0; m.prop = PropKind.None; m.slot = PropSlot.Hand;
                wood.Add(m);
                held[m.who.Key] = m;
            }
            if (wood.Count == 0) { EndFirstWood(); return false; }
            StashProps.HoldLogs = true;
            fwT = 0f; fwSetDown = false; lined = false;
            FirstWoodPlayed++;
            LeanHold(Vector2.Lerp(tree, fire, 0.5f), 0.4f, () => FirstWoodPlaying);
            return true;
        }

        void StepFirstWood(SimCore sim, PeopleView pv, float dt)
        {
            fwT += dt;
            bool done = true;
            foreach (var m in wood)
            {
                m.legT += dt;
                switch (m.leg)
                {
                    case 0: // out to the tree, empty-handed
                        m.style = DollStyle.Stand;
                        if (Advance(m, dt)) { m.leg = 1; m.legT = 0f; m.style = DollStyle.Crouch; }
                        done = false; break;
                    case 1: // the pick-up: bend, and come up with a log on the shoulder
                        m.style = m.legT < PickUpSeconds * 0.7f ? DollStyle.Crouch : DollStyle.Stand;
                        if (m.legT >= PickUpSeconds * 0.5f) { m.prop = PropKind.Log; m.slot = PropSlot.Shoulder; }
                        if (m.legT >= PickUpSeconds)
                        {
                            var from = m.cur;
                            m.path.Clear(); m.path.Add(from);
                            var rot = Quaternion.Euler(0f, Mathf.Atan2(fire.x - pile.x, fire.y - pile.y) * Mathf.Rad2Deg, 0f);
                            var s = rot * StashProps.LogSpots[0];
                            var stand = pile + new Vector2(s.x, s.z) + (pile - fire).normalized * 0.11f + (wood.IndexOf(m) == 0 ? 1f : -1f) * new Vector2(-(pile - fire).normalized.y, (pile - fire).normalized.x) * 0.05f;
                            m.path.Add(stand); m.along = 0f; m.faceTo = pile;
                            m.leg = 2; m.legT = 0f;
                        }
                        done = false; break;
                    case 2: // home with it, at a laden walk
                        m.speed = LifePlanner.RealWalkSpeed * 0.85f;
                        m.style = DollStyle.Stand;
                        if (Advance(m, dt)) { m.leg = 3; m.legT = 0f; m.faceTo = pile; }
                        done = false; break;
                    case 3: // set down on the pile
                        m.style = DollStyle.Crouch;
                        if (m.legT >= SetDownSeconds * 0.6f) { m.prop = PropKind.None; m.slot = PropSlot.Hand; }
                        if (m.legT >= SetDownSeconds) { m.leg = 4; m.legT = 0f; m.style = DollStyle.Stand; m.faceTo = fire; }
                        else done = false;
                        break;
                }
            }
            bool allDown = true;
            foreach (var m in wood) if (m.leg < 3 || (m.leg == 3 && m.legT < SetDownSeconds * 0.6f)) allDown = false;
            if (allDown && !fwSetDown)
            {
                fwSetDown = true; // the logs on the pile; the fire is built up as a log fire (the next frame's stage check refreshes the box)
                StashProps.HoldLogs = false;
                var stash = box.GetComponent<StashProps>();
                if (pv != null && pv.Planner != null && stash != null) { stash.Sync(pv.Planner); stash.Bump(); }
            }
            if (fwSetDown && !lined && done) { lined = true; Say(sim, wood, FirstWoodLines); fwT = Mathf.Max(fwT, 0f); doneAt = fwT; }
            if ((lined && fwT - doneAt >= SettleSeconds) || fwT > MaxFirstWoodSeconds)
            {
                if (!fwSetDown) { fwSetDown = true; StashProps.HoldLogs = false; }
                sim.MarkFirstWildWoodSeen();
                EndFirstWood();
            }
        }
        float doneAt;
        PersonRef lineWho; float lineDue = -1f; // the arrival's line, waiting for the first run's prompts to clear

        /// <summary>The arrival's line once nothing of the first run is on screen (gives up after a minute).</summary>
        void StepArrivalLine(SimCore sim, float dt)
        {
            if (lineDue < 0f) return;
            lineDue -= dt;
            if (lineDue < 0f) return;
            if (PromptUp()) return;
            lineDue = -1f;
            var show = ShowMeDirector.Instance;
            if (show == null || lineWho.IsNone) return;
            string line = ArrivalLines[(sim.State.seed & int.MaxValue) % ArrivalLines.Length];
            show.SayLine(line, lineWho, 4f);
            LastLine = line;
        }

        void EndFirstWood()
        {
            foreach (var m in wood) held.Remove(m.who.Key);
            wood.Clear();
            if (fwT >= 0f) StashProps.HoldLogs = false;
            fwT = -1f;
        }

        // ---------------------------------------------------------------- hunger's way out
        /// <summary>Tester 2026-11-14: hungry on bare land, with nothing placed, the way out must show (nothing big comes by itself, 2026-11-08):
        /// once a visit a person says what they lack. With the bushes' jar ready (unplaced) the line wants something that grows food (and the
        /// Kit's dot and the life slot's stay on, GameHud.FollowLife); with no jar ready yet, the line looks to the land greening first.</summary>
        public static readonly string[] HungryJarLines =
        {
            "Nothing here grows anything we can eat. Not yet.",
            "If only something with berries would come up out of this grass.",
        };
        public static readonly string[] HungryLandLines =
        {
            "The ground is bare. When it greens, maybe something we can eat will grow.",
            "Nothing grows here yet. We wait for the green.",
        };
        /// <summary>Real seconds into a visit before the hunger line (so it never lands on the return's own beats).</summary>
        public const float HungerLineAfter = 6f;
        public int HungerLinesSaid { get; private set; }
        public string LastHungerLine { get; private set; } = "";
        bool hungerSaid; float visitT; bool caughtUpBound;

        void OnReturn(CatchUpResult _) { hungerSaid = false; visitT = 0f; }
        /// <summary>Tests and the panel: a new visit begins (the hunger line may be said again).</summary>
        public void DebugNewVisit() => OnReturn(default);

        /// <summary>The line a hungry camp says now, or null when none is due (not hungry, the land gives food, the bushes are placed).</summary>
        public static string HungerLineFor(SimCore sim)
        {
            if (sim == null || !sim.NewStartOn || !sim.Hungry || sim.LandGivesFood) return null;
            var thicket = AquariumCiv.Sim.Eco.LifeSpecies.Thicket;
            if (sim.LifeIsPlaced(thicket)) return null;
            var lines = sim.JarReady(thicket) ? HungryJarLines : HungryLandLines;
            return lines[(sim.State.seed & int.MaxValue) % lines.Length];
        }

        void StepHunger(SimCore sim, PeopleView pv, float dt)
        {
            if (!caughtUpBound && GameClock.Instance != null) { GameClock.Instance.CaughtUp += OnReturn; caughtUpBound = true; }
            visitT += dt;
            if (hungerSaid || visitT < HungerLineAfter || PromptUp() || StageBusy(sim) || aPhase != ArrivalPhase.None || fwT >= 0f) return;
            string line = HungerLineFor(sim);
            if (line == null || pv == null || pv.Planner == null) return;
            var show = ShowMeDirector.Instance;
            if (show == null) return;
            pv.StandingNow(whoBuf, px, pz);
            PersonRef who = default;
            foreach (var w in whoBuf) { var st = pv.Planner.StageOf(w); if (st != LifeStage.Adult && st != LifeStage.Elder) continue; who = w; if (w.IsNotable) break; }
            if (who.IsNone) return;
            hungerSaid = true;
            show.SayLine(line, who, 4.5f);
            LastHungerLine = line; HungerLinesSaid++;
        }

        // ---------------------------------------------------------------- shared
        void Say(SimCore sim, List<Member> cast, string[] lines)
        {
            var show = ShowMeDirector.Instance;
            if (show == null || cast.Count == 0) return;
            string line = lines[(sim.State.seed & int.MaxValue) % lines.Length];
            var who = cast[0].who;
            foreach (var m in cast) if (m.who.IsNotable) { who = m.who; break; }
            show.SayLine(line, who, 4f);
            LastLine = line;
        }

        /// <summary>Tester 2026-11-14: the walk-in at the default zoom is ~20 px dolls: the camera leans in on the way they walk (like the
        /// placed-life arrivals: eases back to the player's view after the set-down; a two-finger move or a pan cancels it).</summary>
        void LeanArrival()
        {
            if (arrival.Count == 0) return;
            var a = arrival[0].path[0]; var b = arrival[0].path[arrival[0].path.Count - 1];
            LeanHold(Vector2.Lerp(a, b, 0.55f), 0.42f, () => ArrivalPlaying);
        }

        void LeanHold(Vector2 at, float zoom, System.Func<bool> hold)
        {
            var cam = Camera.main != null ? Camera.main.GetComponent<BoxCamera>() : null;
            if (cam == null || box == null || box.land == null) return;
            if (cam.LeanIn(box.transform.TransformPoint(box.land.OnGround(at.x, at.y)), zoom, hold)) Leans++;
        }

        void Lean(Vector2 at)
        {
            var cam = Camera.main != null ? Camera.main.GetComponent<BoxCamera>() : null;
            if (cam == null || !GlassVoice.CameraFree() || box == null || box.land == null) return;
            if (cam.NudgeToward(box.transform.TransformPoint(box.land.OnGround(at.x, at.y)))) Leans++;
        }

        // ---------------------------------------------------------------- debug (the panel)
        /// <summary>"Arrival again": the basket and the bundle are picked up and carried in again.</summary>
        public static string DebugArrivalAgain()
        {
            var sim = GameClock.Instance != null ? GameClock.Instance.Sim : null;
            if (sim == null || !sim.NewStartOn) return "not a new-start box (living land off, or an old box)";
            sim.DebugArrivalAgain();
            var v = Instance;
            if (v == null) return "no new-start view";
            bool was = AutoArrival; AutoArrival = true;
            bool ok = v.BeginArrival(sim, PeopleView.Find());
            AutoArrival = was;
            if (!ok) { sim.SetDownStock(); return "no camp spot or nobody about: set down"; }
            return "the arrival again: " + v.arrival.Count + " walking in";
        }

        /// <summary>"First wild wood now": the sim's cheat, then the beat plays (watched).</summary>
        public static string DebugFirstWildWoodNow()
        {
            var sim = GameClock.Instance != null ? GameClock.Instance.Sim : null;
            if (sim == null) return "no sim";
            var src = SimLayoutSource.Find(); var bv = src != null ? src.GetComponent<BoxView>() : null;
            string r = sim.DebugFirstWildWoodNow(bv == null ? null : (x, z, rr) => bv.FreeForProp(new Vector2(x, z), rr));
            if (bv != null) bv.Refresh();
            return r;
        }
    }
}
