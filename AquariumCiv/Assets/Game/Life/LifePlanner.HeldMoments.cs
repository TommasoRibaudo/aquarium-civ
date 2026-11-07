using System;
using System.Collections.Generic;
using AquariumCiv.Sim;
using AquariumCiv.Sim.Settle;

namespace AquariumCiv.Life
{
    /// <summary>#370: a staged moment the planner is playing: its plan and when (view hours of one view day).</summary>
    public sealed class HeldMomentScene
    {
        public MomentKind kind;
        public int momentId;
        public MomentStagePlan plan;
        public long day;
        public float start, end;
        /// <summary>The moment's place (the planner walks the cast there).</summary>
        public Place place;
        public int CastCount => plan?.members.Count ?? 0;
        /// <summary>Batch 3: people a choice scene took out of this moment (the Keeper's ask holds its cast): no longer in it.</summary>
        public readonly HashSet<int> released = new HashSet<int>();
    }

    /// <summary>
    /// #370: the staged moments (visit-loop.md §2.1, §4.3; animation-specs C3) as the planner plays them. The view's director
    /// (#369 <c>HeldMomentStage</c>) hands over a held ceremony; <see cref="StageHeldMoment"/> casts it from whoever is in the box
    /// now (the principals from the sim's moment, the rest nearest first, nobody asleep, hunting or raising a tent), builds the
    /// pure plan (<see cref="MomentStagePlan"/>) on the real fire and glass, and lays every member's steps as a live scene, like
    /// the relic moments: the planner walks them there along the paths, and once it ends (or <see cref="EndHeldMoment"/>) they
    /// walk back into their day. The view places and poses them from the same steps (<see cref="HeldMomentAt"/>).
    /// The moment's line no longer also plays as this month's scene (<see cref="QueuedForTheKeeper"/>): it plays once, staged.
    /// </summary>
    public sealed partial class LifePlanner
    {
        /// <summary>A staged moment starts between these view hours, and must end by <see cref="HeldLatest"/> (the evening, before
        /// the view day turns and drops it). Outside them the view waits for the morning.</summary>
        public const float HeldEarliest = 6f, HeldLatest = 21.5f;

        readonly List<HeldMomentScene> heldScenes = new List<HeldMomentScene>();
        readonly Dictionary<HeldMomentScene, Staging> heldStaging = new Dictionary<HeldMomentScene, Staging>();

        /// <summary>The staged moments playing now (or not yet ended) (tests, the panel).</summary>
        public IReadOnlyList<HeldMomentScene> HeldScenes => heldScenes;

        /// <summary>Batch 3: is this person standing in the choice scene (a side, the asker) at <paramref name="t"/>?</summary>
        bool InChoice(int key, ViewTime t)
        {
            var st = choice;
            if (st == null || t.day != st.day || t.hour >= st.end) return false;
            foreach (var (who, _, _) in st.Pins()) if (who.Key == key) return true;
            return false;
        }

        /// <summary>
        /// Batch 3: the choice scene holds its cast. Staged at <paramref name="at"/> while a moment plays (the welcome on a return),
        /// it takes its people out of that moment: their steps are cut there and the view no longer places or poses them by it
        /// (<see cref="HeldMomentAt"/>), so they walk to their spot and stay on it. The moment plays on with the rest.
        /// </summary>
        void ReleaseChoiceCastFromHeldMoments(ChoiceStage st, ViewTime at)
        {
            if (st == null || heldScenes.Count == 0) return;
            bool changed = false;
            float h = at.hour;
            foreach (var sc in heldScenes)
            {
                if (sc.day != at.day || h >= sc.end || !heldStaging.TryGetValue(sc, out var stg)) continue;
                foreach (var (who, _, _) in st.Pins())
                {
                    int key = who.Key;
                    if (sc.plan?.Member(key) == null || !sc.released.Add(key)) continue;
                    changed = true;
                    if (!stg.segs.TryGetValue(key, out var list)) continue;
                    for (int j = list.Count - 1; j >= 0; j--)
                    {
                        var s = list[j];
                        if (s.start >= h) { list.RemoveAt(j); continue; }
                        if (s.end > h) { s.end = h; list[j] = s; }
                    }
                }
            }
            if (changed) LiveChanged();
        }

        /// <summary>Can a staged moment of <paramref name="seconds"/> start at <paramref name="at"/>: daytime, and over by the evening.</summary>
        public static bool HeldMomentFits(ViewTime at, float seconds)
        {
            if (at.hour < HeldEarliest || at.hour >= HeldLatest) return false;
            var end = LifeClock.Advance(at, seconds);
            return end.day == at.day && end.hour <= HeldLatest;
        }

        /// <summary>t4-fixes: the buildings standing on their lots (tents, huts...) as x, z, radius triples, for the wedding's vow point to
        /// keep out of their shadow; the fire's own pit is not one.</summary>
        float[] HeldBlockers(float fx, float fz)
        {
            var list = new List<float>();
            foreach (var b in sim.State.buildings)
            {
                if (b.lotId == 0 || Dist(b.x, b.z, fx, fz) < 0.3f) continue;
                float r = 0.5f * Math.Max(b.w, b.d);
                list.Add(b.x); list.Add(b.z); list.Add(r > 0.05f ? r : 0.25f);
            }
            return list.ToArray();
        }

        /// <summary>About how long a staged moment of this kind takes (real seconds), to check <see cref="HeldMomentFits"/> before casting.</summary>
        public static float HeldMomentSeconds(MomentKind kind) => kind switch
        {
            MomentKind.Wedding => 42f, MomentKind.Newborn => 22f, MomentKind.NewLeader => 27f, MomentKind.Festival => 50f, _ => 10f,
        };

        /// <summary>
        /// Stage the held moment <paramref name="m"/> now. Null when it can't be: its people are gone, the principals can't get
        /// there in time, no fire or glass to stage it at, or the kind isn't staged here (a callback).
        /// </summary>
        public HeldMomentScene StageHeldMoment(HeldMoment m, ViewTime at)
        {
            LastHeldWhyNot = "";
            if (m == null) return NoHeld("no moment");
            Sync();
            EnsureDay(at.day);
            var place = m.kind switch
            {
                MomentKind.Wedding => gather.IsNone ? fire : gather,
                MomentKind.Newborn => GlassPlace,
                _ => fire.IsNone ? gather : fire,
            };
            if (place.IsNone) return NoHeld("no place to hold it");
            float fx, fz;
            if (!positions.TryGetPosition(fire, out fx, out fz) && !positions.TryGetPosition(gather, out fx, out fz)) return NoHeld("no fire on the land");
            if (!positions.TryGetPosition(GlassPlace, out float gx, out float gz)) return NoHeld("no glass spot");
            var n = GlassNormalOf(glassSide);
            var geo = new MomentGeometry { fireX = fx, fireZ = fz, glassX = gx, glassZ = gz, nx = n.x, nz = n.z, halfX = Terrain.HX, halfZ = Terrain.HZ, blockers = HeldBlockers(fx, fz) };

            var cast = new MomentCast();
            var used = new HashSet<int>();
            Person A = Alive(m.personA), B = Alive(m.personB);
            switch (m.kind)
            {
                case MomentKind.Wedding:
                    if (A == null || B == null || !Free(A, at) || !Free(B, at)) return NoHeld("the couple isn't here");
                    cast.a = Who(A, place, at); cast.hasA = true; used.Add(A.key);
                    cast.b = Who(B, place, at); cast.hasB = true; used.Add(B.key);
                    var off = leader != null && !used.Contains(leader.key) && Free(leader, at) ? leader : EldestFree(at, used);
                    if (off != null) { cast.officiant = Who(off, place, at); cast.hasOfficiant = true; used.Add(off.key); }
                    Crowd(cast, place, at, used, q => true, MomentStagePlan.MaxWeddingGuests + 2);
                    break;
                case MomentKind.Newborn:
                {
                    var baby = A;
                    var parent = B ?? baby?.parent;
                    if (baby == null || parent == null || !parent.Grown || !Free(parent, at)) return NoHeld("nobody to hold the baby up");
                    cast.a = Who(parent, place, at); cast.hasA = true; cast.babyKey = baby.key;
                    used.Add(parent.key); used.Add(baby.key);
                    // The family first: the other parent, the grandparents, the baby's brothers and sisters; then the nearest.
                    bool Family(Person q) => q == parent.partner || q == parent.parent || q.parent == parent || (parent.partner != null && q.parent == parent.partner)
                        || (q.n != null && parent.n != null && !string.IsNullOrEmpty(parent.n.family) && q.n.family == parent.n.family) || q == parent.friend;
                    Crowd(cast, place, at, used, q => Family(q), MomentStagePlan.MaxFamily + MomentStagePlan.MaxGlassKids);
                    if (cast.grown.Count < 2) Crowd(cast, place, at, used, q => q.Grown, 2 - cast.grown.Count, kidsToo: false);
                    if (cast.kids.Count < 2) Crowd(cast, place, at, used, q => q.stage == LifeStage.Kid, 2 - cast.kids.Count);
                    break;
                }
                case MomentKind.NewLeader:
                {
                    if (A == null || !A.Grown || !Free(A, at)) return NoHeld("the new chief isn't here");
                    cast.a = Who(A, place, at); cast.hasA = true; used.Add(A.key);
                    var old = OldChiefOf(m);
                    if (old != null && old != A && old.Grown && Free(old, at)) { cast.b = Who(old, place, at); cast.hasB = true; used.Add(old.key); }
                    var elder = EldestFree(at, used);
                    if (elder != null) { cast.officiant = Who(elder, place, at); cast.hasOfficiant = true; used.Add(elder.key); }
                    Crowd(cast, place, at, used, q => true, MomentStagePlan.MaxChiefRing);
                    break;
                }
                case MomentKind.Festival:
                {
                    var speaker = leader != null && Free(leader, at) ? leader : EldestFree(at, used);
                    if (speaker != null) { cast.officiant = Who(speaker, place, at); cast.hasOfficiant = true; used.Add(speaker.key); }
                    Crowd(cast, place, at, used, q => true, MomentStagePlan.MaxFestival);
                    break;
                }
                default:
                    return NoHeld("not a staged kind");
            }
            int seed = unchecked(sim.State.seed * 31 + m.id * 7919 + 370);
            var plan = MomentStagePlan.Build(m.kind, cast, geo, seed);
            if (plan == null) return NoHeld(MomentStagePlan.LastWhyNot);
            float end = EndAfter(at, plan.Duration);
            var st = new Staging { glass = GlassPlace, spot = place, start = at.hour, end = end };
            foreach (var mem in plan.members)
            {
                if (!byKey.TryGetValue(mem.key, out var p)) continue;
                bool walks = mem.steps.Count > 0 && mem.steps[0].travel;
                var list = new List<Seg>(mem.steps.Count);
                foreach (var s in mem.steps)
                {
                    float h0 = HourOf(at, LifeClock.Advance(at, s.t0)), h1 = HourOf(at, LifeClock.Advance(at, s.t1));
                    if (h1 <= h0 + 1e-5f) continue;
                    // The planner walks the travellers to the place; the rest (the spotter) are placed by the plan where they stand.
                    var seg = Mk(s.kind, h0, h1, walks ? place : Place.None, s.prop, ActivitySource.Scene, s.partner != 0 && byKey.TryGetValue(s.partner, out var with) ? with.who : PersonRef.None);
                    seg.faces = s.faceGlass;
                    seg.carries = s.carries;
                    list.Add(seg);
                }
                if (list.Count == 0) continue;
                st.segs[p.key] = list;
                st.roles[p.key] = mem.role is MomentRole.Couple or MomentRole.Parent or MomentRole.Chief ? RelicCast.Owner : mem.kid ? RelicCast.Kids : RelicCast.Grown;
                st.order.Add(p.key);
            }
            if (st.segs.Count == 0) return NoHeld("nobody to play it");
            AddLive(SceneKind.HeldMoment, at, end, st);
            var sc = new HeldMomentScene { kind = m.kind, momentId = m.id, plan = plan, day = at.day, start = at.hour, end = end, place = place };
            heldScenes.Add(sc);
            heldStaging[sc] = st;
            return sc;
        }

        /// <summary>Why <see cref="StageHeldMoment"/> last returned null.</summary>
        public string LastHeldWhyNot { get; private set; } = "";

        HeldMomentScene NoHeld(string why) { LastHeldWhyNot = why; return null; }

        /// <summary>Is the scene still on (its live scene is there, the view day hasn't turned, its time isn't over)?</summary>
        public bool HeldMomentPlaying(HeldMomentScene sc, ViewTime t)
        {
            if (sc == null || !heldStaging.TryGetValue(sc, out var st)) return false;
            if (t.day != sc.day || t.hour >= sc.end) return false;
            foreach (var s in live) if (ReferenceEquals(s.staging, st)) return true;
            return false;
        }

        /// <summary>End a staged moment now (the player left, the box changed): everyone walks back into their day from where they stand.</summary>
        public void EndHeldMoment(HeldMomentScene sc)
        {
            if (sc == null) return;
            if (heldStaging.TryGetValue(sc, out var st))
            {
                live.RemoveAll(s => ReferenceEquals(s.staging, st));
                heldStaging.Remove(sc);
            }
            heldScenes.Remove(sc);
            LiveChanged();
        }

        /// <summary>
        /// The view's question each frame: is <paramref name="who"/> in a staged moment at <paramref name="t"/>, and in which step
        /// (<paramref name="secs"/>: real seconds since the moment began)? False before they join and once they are released.
        /// </summary>
        public bool HeldMomentAt(PersonRef who, ViewTime t, out HeldMomentScene scene, out MomentMember member, out int step, out float secs)
        {
            scene = null; member = null; step = -1; secs = 0f;
            if (heldScenes.Count == 0) return false;
            for (int i = heldScenes.Count - 1; i >= 0; i--)
            {
                var sc = heldScenes[i];
                if (t.day != sc.day || t.hour < sc.start || t.hour >= sc.end) continue;
                if (sc.released.Count > 0 && sc.released.Contains(who.Key)) continue; // batch 3: a choice scene took them
                float s = LifeClock.RealSecondsBetween(sc.start, t.hour);
                if (!sc.plan.StepAt(who.Key, s, out member, out step)) continue;
                if (!HeldMomentPlaying(sc, t)) continue;
                scene = sc; secs = s;
                return true;
            }
            return false;
        }

        /// <summary>#370: the newborn's parent has the baby in the arms for the whole moment (a hook in <see cref="PlanPerson"/>).</summary>
        void HeldMomentCarry(Person p, ref PersonActivity a, ViewTime t)
        {
            if (heldScenes.Count == 0) return;
            foreach (var sc in heldScenes)
            {
                var plan = sc.plan;
                if (sc.kind != MomentKind.Newborn || plan.babyKey == 0 || t.day != sc.day || t.hour < sc.start || t.hour >= sc.end) continue;
                if (p.key != plan.carrierKey && p.key != plan.babyKey) continue;
                float s = LifeClock.RealSecondsBetween(sc.start, t.hour);
                if (s >= plan.Duration || !HeldMomentPlaying(sc, t)) continue;
                if (p.key == plan.carrierKey && byKey.TryGetValue(plan.babyKey, out var baby)) { a.carrying = baby.who; a.carryOnBack = false; a.carryOnFoot = false; }
                else if (p.key == plan.babyKey && byKey.TryGetValue(plan.carrierKey, out var carer)) { a.carriedBy = carer.who; a.carryOnBack = false; a.carryOnFoot = false; }
                return;
            }
        }

        /// <summary>#370: a line whose ceremony is held for the Keeper doesn't also play as this month's scene: C2's queue stages it
        /// once, live (a wedding made while watching would otherwise play twice). A callback stays both (it is a card too).</summary>
        bool QueuedForTheKeeper(ChronicleEntry e)
        {
            if (e == null || !sim.MomentsOn) return false;
            var m = sim.MomentOfEntry(e.id);
            return m != null && m.state != HeldMomentState.Carded && m.kind != MomentKind.Callback;
        }

        // ---------------------------------------------------------------- casting

        static (float x, float z) GlassNormalOf(int side) => (side & 3) switch { 0 => (0f, -1f), 1 => (1f, 0f), 2 => (0f, 1f), _ => (-1f, 0f) };

        bool Free(Person p, ViewTime at)
        {
            if (p == null || p.stage == LifeStage.Baby) return false;
            if (DayOf(p)[Find(DayOf(p), at.hour)].kind == ActivityKind.Sleep) return false; // nobody is woken for it
            if (HuntActivityAt(p, at, out _) || TentActivityAt(p, at, out _)) return false;  // the hunt and the tent go on
            if (InChoice(p.key, at)) return false; // batch 3: the Keeper's ask holds its cast
            foreach (var sc in heldScenes) if (sc.day == at.day && at.hour < sc.end && sc.plan.Member(p.key) != null) return false; // a busy day: one moment each
            return true;
        }

        MomentPerson Who(Person p, Place place, ViewTime at)
        {
            if (!PositionAt(p.who, at, out float x, out float z)) positions.TryGetPosition(StopPlace(p, at.hour), out x, out z);
            return MomentPerson.Of(p.key, p.stage == LifeStage.Kid, x, z, WalkSecs(WalkLen(StopPlace(p, at.hour), place)));
        }

        Person EldestFree(ViewTime at, HashSet<int> used)
        {
            Person e = null;
            foreach (var p in people) if (p.n != null && p.Grown && !used.Contains(p.key) && Free(p, at) && (e == null || p.age > e.age)) e = p;
            return e;
        }

        /// <summary>The nearest free people who pass <paramref name="who"/>, up to <paramref name="most"/>, into the cast's kids and grown.</summary>
        void Crowd(MomentCast cast, Place place, ViewTime at, HashSet<int> used, Func<Person, bool> who, int most, bool kidsToo = true)
        {
            if (most <= 0) return;
            var cands = new List<(Person q, MomentPerson mp)>();
            foreach (var q in people)
            {
                if (used.Contains(q.key) || q.stage == LifeStage.Baby || (!kidsToo && q.stage == LifeStage.Kid) || !who(q) || !Free(q, at)) continue;
                cands.Add((q, Who(q, place, at)));
            }
            cands.Sort((a, b) => a.mp.walk != b.mp.walk ? a.mp.walk.CompareTo(b.mp.walk) : a.q.key.CompareTo(b.q.key));
            for (int i = 0; i < cands.Count && i < most; i++)
            {
                used.Add(cands[i].q.key);
                if (cands[i].q.stage == LifeStage.Kid) cast.kids.Add(cands[i].mp); else cast.grown.Add(cands[i].mp);
            }
        }

        /// <summary>The one who held the seat before, when the line says so and they live: a successor or an heir took it from them
        /// (not a contest's loser or a challenged chief, who doesn't hand anything over).</summary>
        Person OldChiefOf(HeldMoment m)
        {
            var ch = sim.State.chronicle;
            for (int i = ch.Count - 1; i >= 0; i--)
            {
                if (ch[i].id != m.entryId) continue;
                return ch[i].kind is "role_succession" or "role_inherited" ? Alive(ch[i].otherNotableId) : null;
            }
            return null;
        }
    }
}
