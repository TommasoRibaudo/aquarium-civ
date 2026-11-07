using System;
using System.Collections.Generic;
using AquariumCiv.Sim;

namespace AquariumCiv.Life
{
    /// <summary>t3-welcome: a greeting the planner is playing (the view polls <see cref="End"/> to know it is over).</summary>
    public sealed class GreetingInfo
    {
        public WelcomeKind kind;
        public long day;
        public float start, end;
        public int count;
        /// <summary>What is true in the box that makes this greeting honest ("a hide is drying by the fire"), for the log and tests.</summary>
        public string premise = "";
        public bool night;
        public readonly List<int> cast = new List<int>();
        public readonly List<ActivityKind> kinds = new List<ActivityKind>();
        public Prop prop;
    }

    /// <summary>
    /// t3-welcome (visit-loop.md 2.1-2.2): the small live greeting a return gets when no ceremony waits. A few people, a few seconds
    /// (<see cref="GreetingSeconds"/>), one at a time, from what is true in the box: the kids run to the glass and wave, the people
    /// by the fire look up and wave, someone holds up a hide that is drying or a basket from the pile, a pair shows a building
    /// raised since the last visit. Nobody asleep, eating, hunting, raising a tent or in a moment or choice is pulled out. At night
    /// one awake person waves with the lantern. It is a live scene like the glass gathering (<see cref="SceneKind.GlassGathering"/>):
    /// the people walk back into their day when it ends. No dice.
    /// </summary>
    public sealed partial class LifePlanner
    {
        /// <summary>The greeting lasts this long in real seconds (walking there included; the brief: at most 10).</summary>
        public const float GreetingSeconds = 10f;
        /// <summary>Only people who can walk to their spot in this many seconds come.</summary>
        public const float GreetingMaxWalkSeconds = 3.5f;
        /// <summary>The cast, at the most.</summary>
        public const int GreetingMaxCast = 5;

        /// <summary>The last greeting staged (tests, the panel).</summary>
        public GreetingInfo LastGreeting { get; private set; }
        public string LastGreetingWhyNot { get; private set; } = "";

        GreetingInfo Skip(string why) { LastGreetingWhyNot = why; return null; }

        /// <summary>A greeting is on stage at <paramref name="t"/> (the choice, another moment or a second greeting waits for it).</summary>
        public bool GreetingPlaying(ViewTime t) => LastGreeting != null && LastGreeting.day == t.day && t.hour < LastGreeting.end;

        bool GreetingFree(Person p, ViewTime at)
        {
            if (!Free(p, at)) return false;
            var k = DayOf(p)[Find(DayOf(p), at.hour)].kind;
            return k != ActivityKind.Eat && k != ActivityKind.Nap && k != ActivityKind.Indoors && k != ActivityKind.Sleep;
        }

        /// <summary>
        /// Stage the greeting <paramref name="kind"/> now. Null when it cannot be (nobody free to play it, what it shows is not true in
        /// the box, a moment or another greeting is on, the day is nearly over); <see cref="LastGreetingWhyNot"/> says why.
        /// </summary>
        public GreetingInfo StageGreeting(WelcomeKind kind, ViewTime at)
        {
            LastGreetingWhyNot = "";
            if (kind == WelcomeKind.None) return Skip("no greeting");
            Sync();
            EnsureDay(at.day);
            if (GreetingPlaying(at)) return Skip("a greeting is playing");
            foreach (var sc in heldScenes) if (sc.day == at.day && at.hour < sc.end) return Skip("a moment is playing");
            if (choice != null && choice.day == at.day && at.hour < choice.end) return Skip("the choice scene is up");
            foreach (var sc in live) if (sc.kind == SceneKind.GlassGathering && sc.day == at.day && at.hour >= sc.start && at.hour < sc.end) return Skip("people are at the glass");
            var endT = LifeClock.Advance(at, GreetingSeconds);
            if (endT.day != at.day) return Skip("the day is turning");
            float end = endT.hour;
            bool night = LifeClock.PhaseOf(at.hour) == DayPhase.Night;

            var info = new GreetingInfo { kind = kind, day = at.day, start = at.hour, end = end, night = night };
            var st = new Staging { glass = GlassPlace };

            if (night) // a lamp and one wave: whoever is awake and near the fire
            {
                var one = NearestFireCandidate(at, null, p => p.stage != LifeStage.Kid, false);
                if (one == null) return Skip("everyone sleeps");
                var here = StopPlace(one, at.hour);
                var s = Mk(ActivityKind.Wave, at.hour, end, here, Prop.Lantern, ActivitySource.Scene);
                s.faces = true; s.still = true;
                Add(st, info, one, new List<Seg> { s });
                info.premise = "someone is awake with a lamp";
                info.kind = WelcomeKind.WaveFromFire;
                return Finish(info, st, at, end);
            }

            switch (kind)
            {
                case WelcomeKind.KidsAtGlass:
                    CastToGlass(st, info, at, end, p => p.stage == LifeStage.Kid, 3, ActivityKind.Wave, ActivityKind.PressHands, 0.55f);
                    info.premise = "kids live here";
                    break;
                case WelcomeKind.GlassWave:
                    CastToGlass(st, info, at, end, p => p.Grown, 3, ActivityKind.Gaze, ActivityKind.Wave, 0.3f);
                    info.premise = "grown-ups live here";
                    break;
                case WelcomeKind.WaveFromFire:
                {
                    var used = new HashSet<int>();
                    for (int i = 0; i < GreetingMaxCast; i++)
                    {
                        var p = NearestFireCandidate(at, used, q => true, true);
                        if (p == null) break;
                        used.Add(p.key);
                        var here = StopPlace(p, at.hour);
                        float cut = at.hour + (end - at.hour) * (0.3f + 0.05f * i); // not in step
                        var a = Mk(ActivityKind.LookUp, at.hour, cut, here, Prop.None, ActivitySource.Scene);
                        var b = Mk(ActivityKind.Wave, cut, end, here, Prop.None, ActivitySource.Scene);
                        a.faces = b.faces = true; a.still = b.still = true;
                        Add(st, info, p, new List<Seg> { a, b });
                    }
                    info.premise = "people are in the box";
                    break;
                }
                case WelcomeKind.ShowHide:
                    if (sim.HidesDrying <= 0) return Skip("no hide is drying");
                    CastShow(st, info, at, end, fire, Prop.Hide);
                    info.premise = "a hide is drying by the fire";
                    break;
                case WelcomeKind.ShowBasket:
                    if (era != Era.Camp || CampStash.StockOf(sim.State.food, sim.State.wood).baskets <= 0) return Skip("no basket on the pile");
                    CastShow(st, info, at, end, fire, Prop.Basket);
                    info.premise = "a basket stands on the pile";
                    break;
                case WelcomeKind.ShowHut:
                {
                    Building nb = null;
                    int id = sim.GreetingFacts.newBuildingId;
                    foreach (var b in sim.State.buildings) if (b.id == id) nb = b;
                    if (nb == null) return Skip("nothing new was raised");
                    var place = PlaceOf(nb);
                    if (place.IsNone || !IsValidPlace(place)) return Skip("the new building has no place");
                    CastShow(st, info, at, end, place, Prop.None, true);
                    info.premise = "a " + nb.type.ToLowerInvariant() + " was raised since the last visit";
                    break;
                }
            }
            if (st.segs.Count == 0) return Skip("nobody free to play " + kind);
            return Finish(info, st, at, end);
        }

        GreetingInfo Finish(GreetingInfo info, Staging st, ViewTime at, float end)
        {
            info.count = st.segs.Count;
            AddLive(SceneKind.GlassGathering, at, end, st);
            LastGreeting = info;
            return info;
        }

        void Add(Staging st, GreetingInfo info, Person p, List<Seg> segs)
        {
            st.segs[p.key] = segs;
            st.order.Add(p.key);
            info.cast.Add(p.key);
            foreach (var s in segs) if (!info.kinds.Contains(s.kind)) info.kinds.Add(s.kind);
            foreach (var s in segs) if (s.prop != Prop.None) info.prop = s.prop;
        }

        void CastToGlass(Staging st, GreetingInfo info, ViewTime at, float end, Func<Person, bool> who, int max, ActivityKind first, ActivityKind second, float split)
        {
            var glass = GlassPlace;
            var cands = new List<(Person p, float len)>();
            foreach (var p in people)
            {
                if (!who(p) || !GreetingFree(p, at)) continue;
                float len = WalkLen(StopPlace(p, at.hour), glass);
                if (WalkSecs(len) > GreetingMaxWalkSeconds) continue;
                cands.Add((p, len));
            }
            cands.Sort((a, b) => a.len != b.len ? a.len.CompareTo(b.len) : a.p.key.CompareTo(b.p.key));
            for (int i = 0; i < cands.Count && st.segs.Count < max; i++)
            {
                var p = cands[i].p;
                float cut = at.hour + (end - at.hour) * (split + 0.06f * i); // neighbours never in step
                var a = Mk(first, at.hour, cut, glass, Prop.None, ActivitySource.Scene);
                var b = Mk(second, cut, end, glass, Prop.None, ActivitySource.Scene);
                a.faces = b.faces = true;
                Add(st, info, p, new List<Seg> { a, b });
            }
        }

        /// <summary>One holds something up (or points at a thing), the others near cheer or wave: all facing the glass.</summary>
        void CastShow(Staging st, GreetingInfo info, ViewTime at, float end, Place spot, Prop prop, bool pair = false)
        {
            var used = new HashSet<int>();
            Person holder = null;
            float best = float.MaxValue;
            foreach (var p in people)
            {
                if (!p.Grown || !GreetingFree(p, at)) continue;
                float len = WalkLen(StopPlace(p, at.hour), spot);
                if (WalkSecs(len) > GreetingMaxWalkSeconds) continue;
                if (len < best || (len == best && holder != null && p.key < holder.key)) { best = len; holder = p; }
            }
            if (holder == null) return;
            used.Add(holder.key);
            var show = Mk(pair ? ActivityKind.Point : ActivityKind.HoldUp, at.hour, end, spot, prop, ActivitySource.Scene);
            show.faces = !pair; show.carries = prop != Prop.None; // walks there carrying it, then holds it up to the glass (a pair points at the thing)
            Add(st, info, holder, new List<Seg> { show });

            // The others: the nearest two (a kid first) cheer, or wave back at the Keeper.
            var rest = new List<(Person p, float len)>();
            foreach (var p in people)
            {
                if (used.Contains(p.key) || !GreetingFree(p, at)) continue;
                float len = WalkLen(StopPlace(p, at.hour), spot);
                if (WalkSecs(len) > GreetingMaxWalkSeconds) continue;
                rest.Add((p, p.stage == LifeStage.Kid ? len - 100f : len)); // kids first
            }
            rest.Sort((a, b) => a.len != b.len ? a.len.CompareTo(b.len) : a.p.key.CompareTo(b.p.key));
            int others = pair ? 1 : 2;
            for (int i = 0; i < rest.Count && i < others; i++)
            {
                var p = rest[i].p;
                float cut = at.hour + (end - at.hour) * (0.45f + 0.08f * i);
                var a = Mk(ActivityKind.WatchWork, at.hour, cut, spot, Prop.None, ActivitySource.Scene);
                var b = Mk(pair ? ActivityKind.Wave : ActivityKind.Cheer, cut, end, spot, Prop.None, ActivitySource.Scene);
                b.faces = true;
                Add(st, info, p, new List<Seg> { a, b });
            }
        }

        /// <summary>The nearest to the fire of those free to play (not mid-walk when <paramref name="still"/>).</summary>
        Person NearestFireCandidate(ViewTime at, HashSet<int> used, Func<Person, bool> ok, bool still)
        {
            Person best = null;
            float bd = float.MaxValue;
            bool haveFire = positions.TryGetPosition(fire, out float fx, out float fz);
            foreach (var p in people)
            {
                if (p.stage == LifeStage.Baby || (used != null && used.Contains(p.key)) || !ok(p)) continue;
                var k = DayOf(p)[Find(DayOf(p), at.hour)];
                bool nightOk = k.kind == ActivityKind.Sleep || k.kind == ActivityKind.Nap;
                if (!Free(p, at) || nightOk || k.kind == ActivityKind.Eat || k.kind == ActivityKind.Indoors) continue;
                if (still && k.Moving) continue;
                float d = 0f;
                if (haveFire && PositionAt(p.who, at, out float px, out float pz)) d = (px - fx) * (px - fx) + (pz - fz) * (pz - fz);
                if (d < bd || (d == bd && best != null && p.key < best.key)) { bd = d; best = p; }
            }
            return best;
        }
    }

}
