using System;
using System.Collections.Generic;
using AquariumCiv.Sim;
using AquariumCiv.Sim.Eco;

namespace AquariumCiv.Life
{
    /// <summary>One beat of a planned hunt, as data the view plays. Times are view hours of <see cref="HuntPlan.day"/>.
    /// Coordinates are world x/z (the site grid of <see cref="Sim.Settle.SiteMap"/>); <see cref="hasXZ"/> is false when an end is a
    /// building place the view resolves itself (the walk to the fire from wherever the hunter was, the walk home after).</summary>
    public struct HuntStep
    {
        public HuntPhase phase;
        public float start, end;
        public Place from, to;
        public bool hasXZ;
        public float x0, z0, x1, z1;
        public ActivityKind kind;
        public Prop prop;
        public bool moving;
        /// <summary>The person faces the rabbit (its spot is <see cref="HuntPlan.targetX"/>, <see cref="HuntPlan.targetZ"/>) rather than their direction of travel.</summary>
        public bool faceRabbit;
    }

    /// <summary>
    /// #283 phase 1: one hunt, planned. A hunter takes a spear at the fire, walks into the meadow toward a real rabbit from the
    /// herd (<see cref="rabbitId"/>, at <see cref="targetX"/>, <see cref="targetZ"/> when the spear flies), crouches and stalks,
    /// throws, and either hits (<see cref="catches"/>: the rabbit is gone, carried back over the shoulder to the fire, skinned,
    /// cooked) or misses (the rabbit bolts, the hunter walks back). The outcome is seeded, so the view and the sim agree; the
    /// sim is settled once, at <see cref="resolveHour"/>, by <see cref="LifePlanner.ResolveHunts"/>.
    /// </summary>
    public sealed class HuntPlan
    {
        public long day;
        /// <summary>Position in the day's list, in the order the hunts settle.</summary>
        public int index;
        public PersonRef hunter, kid;
        public int rabbitId;
        public bool catches;
        /// <summary>True once the sim has settled it (the catch, if any, is in the herd's count and the hide is drying).</summary>
        public bool resolved;
        /// <summary>The rabbit really was taken (a planned catch is refused at the herd's floor).</summary>
        public bool caught;
        public bool forced, debugStarted;
        public float start, end, strikeStart, resolveHour;
        public float targetX, targetZ, stalkX, stalkZ, lungeX, lungeZ, fireX, fireZ;
        internal Place hunterBase, hunterBaseEnd, kidBase, kidBaseEnd;
        public readonly List<HuntStep> steps = new List<HuntStep>();
        public readonly List<HuntStep> kidSteps = new List<HuntStep>();

        public bool HasKid => !kid.IsNone;
        public double ResolveAbsolute => day * 24.0 + resolveHour;
    }

    /// <summary>The rabbit field's clock: real seconds of the view day clock (what the view feeds <c>Field.Sample</c>).</summary>
    public static class HuntClock
    {
        /// <summary>Real seconds since the start of view day 0 at this view time (a view day is <see cref="LifeClock.RealSecondsPerDay"/>).</summary>
        public static double FieldSeconds(long day, float hour) => day * (double)LifeClock.RealSecondsPerDay + LifeClock.RealSecondsBetween(0f, hour);
    }

    /// <summary>#283 phase 1: the hunt as planner data (see <see cref="HuntPlan"/>).</summary>
    public sealed partial class LifePlanner
    {
        // ---------------------------------------------------------------- tuning [est]
        /// <summary>How often a throw finds its mark.</summary>
        public const float HuntCatchChance = 0.62f;
        /// <summary>The share of days a hunter goes out, and a second time that day.</summary>
        public const float HuntDayChance = 0.9f, HuntSecondChance = 0.4f;
        /// <summary>A kid tags along on this share of hunts.</summary>
        public const float HuntKidChance = 0.3f;
        /// <summary>How close the hunter stalks from, and the beat lengths in view hours.</summary>
        public const float StalkRange = 0.5f, TakeSpearHours = 0.12f, StalkHours = 0.3f, StrikeHours = 0.06f, BoltHours = 0.12f,
            SkinHours = 0.3f, CookHours = 0.3f, CheerHours = 0.12f;
        /// <summary>Start windows: share of hunts that start in the morning (7 to 9:30), toward dusk (the last two hours of the
        /// afternoon), or otherwise in the working day.</summary>
        public const float HuntMorningShare = 0.45f, HuntDuskShare = 0.40f;

        /// <summary>The rabbits the hunters see. The view sets its field here (and keeps it synced to the sim count); without it
        /// nothing is hunted. Setting it forgets the plans made so far.</summary>
        public RabbitHerd.Field Rabbits { get => huntField; set { huntField = value; huntDays.Clear(); } }
        RabbitHerd.Field huntField;
        readonly Dictionary<long, List<HuntPlan>> huntDays = new Dictionary<long, List<HuntPlan>>();
        bool huntBuilding;
        ViewTime huntNow;
        bool? huntForceNext;

        static readonly List<HuntPlan> NoHunts = new List<HuntPlan>();

        // ---------------------------------------------------------------- when

        /// <summary>The hour a hunt starts, from two rolls in 0..1: mornings (7 to 9:30) and the end of the afternoon are the
        /// hunters' hours; the rest are scattered through the working day.</summary>
        public static float HuntStartHour(float whichRoll, float whereRoll, Season season)
        {
            float end = LifeClock.AfternoonEnd(season);
            if (whichRoll < HuntMorningShare) return 7f + whereRoll * 2.5f;
            if (whichRoll < HuntMorningShare + HuntDuskShare) return end - 1.9f + whereRoll * 1.2f;
            // otherwise the late morning or the early afternoon
            return whereRoll < 0.5f ? 9.5f + whereRoll * 2f * 1.8f : 13f + (whereRoll - 0.5f) * 2f * Math.Max(0.5f, end - 3f - 13f);
        }

        // ---------------------------------------------------------------- the day's hunts

        /// <summary>The hunts of a view day, in the order they settle. Empty unless the ecology is on, the era is the tribe's or
        /// the camp's, the hunters have <see cref="Rabbits"/> to see, and the herd has more than its floor. Made once per day
        /// and kept (a hunt is never re-rolled mid-day).</summary>
        public IReadOnlyList<HuntPlan> HuntsOn(long day)
        {
            Sync();
            return HuntsOf(day);
        }

        List<HuntPlan> HuntsOf(long day)
        {
            if (huntField == null || !sim.EcologyOn || sim.State.era != Era.Camp) return NoHunts;
            if (huntDays.TryGetValue(day, out var list)) return list;
            EnsureDay(day);
            PruneHuntDays(day);
            list = new List<HuntPlan>();
            huntDays[day] = list;
            PlanHuntDay(day, list);
            return list;
        }

        void PruneHuntDays(long day)
        {
            List<long> drop = null;
            foreach (var k in huntDays.Keys) if (k < day - 2) (drop ??= new List<long>()).Add(k);
            if (drop != null) foreach (var k in drop) huntDays.Remove(k);
        }

        void PlanHuntDay(long day, List<HuntPlan> list)
        {
            if (LifeClock.IsRestDay(day) || dayInfo.weather == Weather.Storm) return;
            if (huntField.Alive.Count == 0 || sim.HuntSurplus <= 0) return;
            BuildCarers();
            var season = dayInfo.season;
            var tries = new List<(float start, Person p, int slot)>();
            foreach (var p in people)
            {
                if (p.job != JobType.Hunter || !p.Adult || carerBabies.ContainsKey(p.key)) continue;
                if (R(p.key, day, 300) >= HuntDayChance) continue;
                tries.Add((HuntStartHour(R(p.key, day, 301), R(p.key, day, 302), season), p, 0));
                if (R(p.key, day, 303) < HuntSecondChance) tries.Add((HuntStartHour(R(p.key, day, 304), R(p.key, day, 305), season), p, 1));
            }
            tries.Sort((a, b) => { int c = a.start.CompareTo(b.start); return c != 0 ? c : a.p.key.CompareTo(b.p.key); });
            int allowed = (sim.HuntSurplus + 1) / 2; // the hunt takes half the surplus a day at most
            int catchesPlanned = 0;
            var taken = new HashSet<int>();
            var usedKids = new HashSet<int>();
            var lastEnd = new Dictionary<int, float>();
            float limit = LifeClock.AfternoonEnd(season) + 0.5f;
            foreach (var t in tries)
            {
                float start = t.start;
                if (lastEnd.TryGetValue(t.p.key, out float le)) start = Math.Max(start, le + 0.3f);
                bool wantCatch = R(t.p.key, day, 310 + t.slot) < HuntCatchChance && catchesPlanned < allowed;
                var plan = MakeHunt(t.p, day, start, wantCatch, taken, usedKids, t.slot);
                if (plan == null) continue;
                if (plan.end > limit)
                {   // too late in the day: start earlier, if the morning allows, else drop it
                    float shift = plan.end - limit;
                    if (start - shift < 7f) continue;
                    plan = MakeHunt(t.p, day, start - shift, wantCatch, taken, usedKids, t.slot);
                    if (plan == null || plan.end > limit + 0.05f) continue;
                }
                if (plan.catches) catchesPlanned++;
                taken.Add(plan.rabbitId);
                if (plan.HasKid) usedKids.Add(plan.kid.Key);
                lastEnd[t.p.key] = plan.end;
                list.Add(plan);
            }
            Reindex(list);
        }

        static void Reindex(List<HuntPlan> list)
        {
            list.Sort((a, b) => { int c = a.ResolveAbsolute.CompareTo(b.ResolveAbsolute); return c != 0 ? c : a.hunter.Key.CompareTo(b.hunter.Key); });
            for (int i = 0; i < list.Count; i++) list[i].index = i;
        }

        // ---------------------------------------------------------------- making one hunt

        HuntPlan MakeHunt(Person hunter, long day, float start, bool wantCatch, HashSet<int> taken, HashSet<int> usedKids, int slot, bool useHeldForce = false)
        {
            var ground = sim.EcoGround;
            float fx = ground.campX, fz = ground.campZ;
            var plan = new HuntPlan { day = day, hunter = hunter.who, fireX = fx, fireZ = fz, start = start };

            // the rabbit: one of the two nearest the fire that nobody else is after
            double t0 = HuntClock.FieldSeconds(day, start + 0.8f);
            var cands = new List<(float d, int id)>();
            foreach (int id in huntField.Alive)
            {
                if (taken.Contains(id)) continue;
                var pose = huntField.Sample(id, t0);
                float dx = pose.x - fx, dz = pose.z - fz;
                cands.Add((dx * dx + dz * dz, id));
            }
            if (cands.Count == 0) return null;
            cands.Sort((a, b) => { int c = a.d.CompareTo(b.d); return c != 0 ? c : a.id.CompareTo(b.id); });
            int pick = cands.Count > 1 && R(hunter.key, day, 320 + slot) < 0.25f ? 1 : 0;
            plan.rabbitId = cands[pick].id;

            plan.hunterBase = BasePlace(hunter, day, start);
            float toFire = HuntWalkHours(plan.hunterBase, fx, fz);

            // where the spear flies: the rabbit's spot at the strike, a fixed point of the iteration start -> strike time -> spot
            float tx = 0f, tz = 0f;
            float strikeAt = start + toFire + TakeSpearHours + 0.4f + StalkHours;
            for (int it = 0; it < 4; it++)
            {
                huntField.PositionOf(plan.rabbitId, HuntClock.FieldSeconds(day, strikeAt), out tx, out tz);
                Geometry(plan, fx, fz, tx, tz);
                strikeAt = start + toFire + TakeSpearHours + WalkOutHours(plan) + StalkHours;
            }
            huntField.PositionOf(plan.rabbitId, HuntClock.FieldSeconds(day, strikeAt), out tx, out tz);
            Geometry(plan, fx, fz, tx, tz);
            plan.strikeStart = strikeAt;
            plan.catches = wantCatch && sim.HuntSurplus > 0;
            if (useHeldForce && huntForceNext.HasValue) { plan.catches = huntForceNext.Value && sim.HuntSurplus > 0; plan.forced = true; huntForceNext = null; }

            if (R(hunter.key, day, 330 + slot) < HuntKidChance) PickKid(plan, hunter, day, slot, usedKids);
            BuildSteps(plan);
            return plan;
        }

        static void Geometry(HuntPlan plan, float fx, float fz, float tx, float tz)
        {
            plan.targetX = tx; plan.targetZ = tz;
            float dx = tx - fx, dz = tz - fz, len = (float)Math.Sqrt(dx * dx + dz * dz);
            float ux = 1f, uz = 0f;
            if (len > 0.001f) { ux = dx / len; uz = dz / len; }
            float reach = Math.Min(StalkRange, len * 0.6f);
            plan.stalkX = tx - ux * reach; plan.stalkZ = tz - uz * reach;
            plan.lungeX = plan.stalkX + (tx - plan.stalkX) * 0.75f; plan.lungeZ = plan.stalkZ + (tz - plan.stalkZ) * 0.75f;
        }

        static float Dist(float ax, float az, float bx, float bz) { float dx = ax - bx, dz = az - bz; return (float)Math.Sqrt(dx * dx + dz * dz); }
        static float WalkH(float d, float speed = WalkSpeed) => Math.Max(0.1f, d / speed);
        float WalkOutHours(HuntPlan plan) => WalkH(Dist(plan.fireX, plan.fireZ, plan.stalkX, plan.stalkZ));

        /// <summary>The hours of a walk between a building place and a world point (the straight line when the place has a spot,
        /// else a flat quarter hour).</summary>
        float HuntWalkHours(Place a, float x, float z, float speed = WalkSpeed)
        {
            if (a.IsNone) return 0.25f;
            if (PositionOfPlace(a, out float ax, out float az)) return Math.Max(MinWalk, Math.Min(0.7f, Dist(ax, az, x, z) / speed));
            return 0.25f;
        }

        bool PositionOfPlace(Place a, out float x, out float z)
        {
            if (a == fire || a == stash || a.kind == PlaceKind.Stash) { x = sim.EcoGround.campX; z = sim.EcoGround.campZ; return true; }
            return positions.TryGetPosition(a, out x, out z);
        }

        /// <summary>Where a person is meant to be at an hour of the day without a hunt (what they walk back to).</summary>
        Place BasePlace(Person p, long day, float hour)
        {
            huntBuilding = true;
            try
            {
                var a = PlanBase(p, new ViewTime(day, Math.Min(23.9f, hour)));
                return IsValidPlace(a.place) ? a.place : ValidHome(p);
            }
            finally { huntBuilding = false; }
        }

        void PickKid(HuntPlan plan, Person hunter, long day, int slot, HashSet<int> usedKids)
        {
            var kids = new List<Person>();
            foreach (var q in people) if (q.stage == LifeStage.Kid && !usedKids.Contains(q.key)) kids.Add(q);
            if (kids.Count == 0) return;
            var kid = kids[(int)(H(hunter.key, day, 340 + slot) % (uint)kids.Count)];
            plan.kid = kid.who;
            plan.kidBase = BasePlace(kid, day, plan.start);
        }

        // ---------------------------------------------------------------- the beats

        void BuildSteps(HuntPlan plan)
        {
            plan.steps.Clear(); plan.kidSteps.Clear();
            float fx = plan.fireX, fz = plan.fireZ;
            float t = plan.start;
            var meadowStalk = FieldPlace(plan.stalkX, plan.stalkZ);
            var meadowLunge = FieldPlace(plan.lungeX, plan.lungeZ);

            float toFire = HuntWalkHours(plan.hunterBase, fx, fz);
            AddStep(plan, ref t, toFire, HuntPhase.ToFire, ActivityKind.Walk, Prop.None, plan.hunterBase, fire, true, false);
            AddStep(plan, ref t, TakeSpearHours, HuntPhase.TakeSpear, ActivityKind.Prepare, Prop.Spear, fire, fire, false, false, fx, fz, fx, fz);
            AddStep(plan, ref t, WalkOutHours(plan), HuntPhase.WalkOut, ActivityKind.Walk, Prop.Spear, fire, meadowStalk, true, false, fx, fz, plan.stalkX, plan.stalkZ);
            AddStep(plan, ref t, StalkHours, HuntPhase.Stalk, ActivityKind.Stalk, Prop.Spear, meadowStalk, meadowLunge, false, true, plan.stalkX, plan.stalkZ, plan.lungeX, plan.lungeZ);
            AddStep(plan, ref t, StrikeHours, HuntPhase.Strike, ActivityKind.Stalk, Prop.Spear, meadowLunge, meadowLunge, false, true, plan.lungeX, plan.lungeZ, plan.lungeX, plan.lungeZ);
            plan.resolveHour = t;
            float afterStrike = t;
            if (plan.catches)
            {
                float carry = WalkH(Dist(plan.lungeX, plan.lungeZ, fx, fz), WalkSpeed * 0.8f);
                AddStep(plan, ref t, carry, HuntPhase.CarryBack, ActivityKind.Carry, Prop.Rabbit, meadowLunge, stash, true, false, plan.lungeX, plan.lungeZ, fx, fz);
                AddStep(plan, ref t, SkinHours, HuntPhase.Skin, ActivityKind.Prepare, Prop.Hide, fire, fire, false, false, fx, fz, fx, fz);
                AddStep(plan, ref t, CookHours, HuntPhase.Cook, ActivityKind.Cook, Prop.Stick, fire, fire, false, false, fx, fz, fx, fz);
            }
            else
            {
                AddStep(plan, ref t, BoltHours, HuntPhase.Bolt, ActivityKind.Stalk, Prop.Spear, meadowLunge, meadowLunge, false, true, plan.lungeX, plan.lungeZ, plan.lungeX, plan.lungeZ);
                float back = WalkH(Dist(plan.lungeX, plan.lungeZ, fx, fz));
                AddStep(plan, ref t, back, HuntPhase.WalkBack, ActivityKind.Walk, Prop.Spear, meadowLunge, fire, true, false, plan.lungeX, plan.lungeZ, fx, fz);
            }
            // the walk back into the day
            var hunter = FindPerson(plan.hunter);
            plan.hunterBaseEnd = hunter != null ? BasePlace(hunter, plan.day, t + 0.3f) : fire;
            float ret = HuntWalkHours(plan.hunterBaseEnd, fx, fz);
            AddStep(plan, ref t, ret, HuntPhase.Return, ActivityKind.Walk, Prop.None, fire, plan.hunterBaseEnd, true, false);
            plan.end = t;

            if (!plan.kid.IsNone) BuildKidSteps(plan, afterStrike);
        }

        void BuildKidSteps(HuntPlan plan, float afterStrike)
        {
            var kid = FindPerson(plan.kid);
            if (kid == null) { plan.kid = PersonRef.None; return; }
            float fx = plan.fireX, fz = plan.fireZ;
            // the kid trails the hunter and stops short of the stalk point, on the line back to the fire
            float dx = fx - plan.stalkX, dz = fz - plan.stalkZ, len = (float)Math.Sqrt(dx * dx + dz * dz);
            float ux = len > 0.001f ? dx / len : 0f, uz = len > 0.001f ? dz / len : 1f;
            float back = Math.Min(0.5f, len);
            float kx = plan.stalkX + ux * back, kz = plan.stalkZ + uz * back;
            var kidSpot = FieldPlace(kx, kz);
            float t = plan.steps.Count > 1 ? plan.steps[1].start : plan.start;   // leaves as the hunter takes the spear
            float follow = Math.Max(HuntWalkHours(plan.kidBase, kx, kz), plan.steps[2].end - t + 0.05f);
            AddKid(plan, ref t, follow, HuntPhase.KidFollow, ActivityKind.Walk, plan.kidBase, kidSpot, true, false, kx, kz);
            AddKid(plan, ref t, Math.Max(0.05f, afterStrike - t), HuntPhase.KidWatch, ActivityKind.Watch, kidSpot, kidSpot, false, true, kx, kz);
            if (plan.catches) AddKid(plan, ref t, CheerHours, HuntPhase.KidCheer, ActivityKind.Cheer, kidSpot, kidSpot, false, true, kx, kz);
            plan.kidBaseEnd = BasePlace(kid, plan.day, t + 0.5f);
            float home = Math.Max(HuntWalkHours(plan.kidBaseEnd, kx, kz), 0.15f);
            AddKid(plan, ref t, home, HuntPhase.KidBack, ActivityKind.Walk, kidSpot, plan.kidBaseEnd, true, false, kx, kz);
        }

        Person FindPerson(PersonRef who) { byKey.TryGetValue(who.Key, out var p); return p; }

        Place FieldPlace(float x, float z) => Place.Land(PlaceKind.Meadow, AquariumCiv.Sim.Settle.SiteMap.CellAt(x, z));


        void AddStep(HuntPlan plan, ref float t, float hours, HuntPhase phase, ActivityKind kind, Prop prop, Place from, Place to,
            bool moving, bool faceRabbit, float x0 = 0f, float z0 = 0f, float x1 = 0f, float z1 = 0f)
        {
            bool xz = phase != HuntPhase.ToFire && phase != HuntPhase.Return;
            if (phase == HuntPhase.ToFire) { x1 = plan.fireX; z1 = plan.fireZ; }
            if (phase == HuntPhase.Return) { x0 = plan.fireX; z0 = plan.fireZ; }
            plan.steps.Add(new HuntStep
            {
                phase = phase, start = t, end = t + hours, from = from, to = to, hasXZ = xz, x0 = x0, z0 = z0, x1 = x1, z1 = z1,
                kind = kind, prop = prop, moving = moving, faceRabbit = faceRabbit,
            });
            t += hours;
        }

        void AddKid(HuntPlan plan, ref float t, float hours, HuntPhase phase, ActivityKind kind, Place from, Place to,
            bool moving, bool faceRabbit, float spotX, float spotZ)
        {
            bool fromKnown = phase != HuntPhase.KidFollow, toKnown = phase != HuntPhase.KidBack;
            plan.kidSteps.Add(new HuntStep
            {
                phase = phase, start = t, end = t + hours, from = from, to = to, hasXZ = fromKnown && toKnown, kind = kind, prop = Prop.None,
                moving = moving, faceRabbit = faceRabbit,
                x0 = fromKnown ? spotX : plan.fireX, z0 = fromKnown ? spotZ : plan.fireZ, x1 = toKnown ? spotX : plan.fireX, z1 = toKnown ? spotZ : plan.fireZ,
            });
            t += hours;
        }

        // ---------------------------------------------------------------- what the view asks

        bool HuntActivityAt(Person p, ViewTime t, out PersonActivity a)
        {
            a = default;
            if (huntBuilding || huntField == null) return false;
            var plans = HuntsOf(t.day);
            huntNow = t;
            for (int i = 0; i < plans.Count; i++)
            {
                var plan = plans[i];
                bool isHunter = plan.hunter == p.who, isKid = !plan.kid.IsNone && plan.kid == p.who;
                if (!isHunter && !isKid) continue;
                var steps = isHunter ? plan.steps : plan.kidSteps;
                if (steps.Count == 0 || t.hour < steps[0].start || t.hour >= steps[steps.Count - 1].end) continue;
                for (int s = 0; s < steps.Count; s++)
                {
                    var st = steps[s];
                    if (t.hour >= st.end && s < steps.Count - 1) continue;
                    float len = Math.Max(1e-4f, st.end - st.start), into = Math.Max(0f, t.hour - st.start);
                    int n = ActivityInfo.LoopSteps(st.kind);
                    a = new PersonActivity
                    {
                        who = p.who, kind = st.kind, place = st.to, from = st.moving ? st.from : st.to, moving = st.moving && st.from != st.to,
                        prop = st.prop, start = st.start, end = st.end, progress = Math.Min(1f, into / len),
                        step = n <= 1 ? 0 : (int)(into / StepHours) % n, source = ActivitySource.Scene, pace = dayInfo.pace, chronicleId = -1,
                        hunt = st.phase,
                    };
                    return true;
                }
            }
            return false;
        }

        /// <summary>The hunt beat a person is in at a time, with its coordinates and the plan it belongs to (false when they are not in one).</summary>
        public bool HuntStepAt(PersonRef who, ViewTime t, out HuntStep step, out HuntPlan plan)
        {
            step = default; plan = null;
            Sync();
            var plans = HuntsOf(t.day);
            foreach (var pl in plans)
            {
                var steps = pl.hunter == who ? pl.steps : !pl.kid.IsNone && pl.kid == who ? pl.kidSteps : null;
                if (steps == null || steps.Count == 0 || t.hour < steps[0].start || t.hour >= steps[steps.Count - 1].end) continue;
                foreach (var st in steps) if (t.hour < st.end) { step = st; plan = pl; return true; }
            }
            return false;
        }

        // ---------------------------------------------------------------- settling the sim

        /// <summary>Settle every hunt whose strike is over at <paramref name="now"/>. The view calls this every frame: each hunt is
        /// settled once (the sim's mark is saved, so not twice across a reload either). A catch lowers the herd's count by one
        /// (<see cref="SimCore.HuntCatch"/>), leaves a hide drying by the fire and takes the rabbit out of <see cref="Rabbits"/>; a
        /// planned catch at the herd's floor turns into a miss. Returns how many settled now.</summary>
        public int ResolveHunts(ViewTime now, List<HuntPlan> settled = null)
        {
            Sync();
            if (huntField == null || !sim.EcologyOn) return 0;
            int count = 0;
            var days = new List<long>(huntDays.Keys);
            days.Sort();
            foreach (long day in days)
            {
                var list = huntDays[day];
                for (int i = 0; i < list.Count; i++)
                {
                    var plan = list[i];
                    if (plan.resolved) continue;
                    if (plan.ResolveAbsolute > now.Absolute) break;
                    plan.resolved = true;
                    if (plan.index < sim.HuntsSettled(day)) continue; // settled before a reload
                    if (plan.catches && sim.HuntCatch())
                    {
                        plan.caught = true;
                        huntField.Remove(plan.rabbitId);
                    }
                    sim.HuntSettled(day);
                    settled?.Add(plan);
                    count++;
                }
            }
            return count;
        }

        // ---------------------------------------------------------------- debug (the panel's cheats)

        /// <summary>Debug ("start a hunt now"): the first free hunter (else any grown-up who is not the toolmaker) takes a spear
        /// and goes after the nearest rabbit, starting now. False when the ecology is off, the view has set no
        /// <see cref="Rabbits"/>, none are shown, or nobody is free.</summary>
        public bool DebugStartHunt(ViewTime now, out HuntPlan plan)
        {
            plan = null;
            Sync();
            if (huntField == null || !sim.EcologyOn || huntField.Alive.Count == 0) return false;
            EnsureDay(now.day);
            var list = HuntsOf(now.day);
            if (ReferenceEquals(list, NoHunts)) { list = new List<HuntPlan>(); huntDays[now.day] = list; }
            Person pick = null;
            for (int pass = 0; pass < 2 && pick == null; pass++)
                foreach (var p in people)
                {
                    if (!p.Adult || p.job == JobType.Toolmaker || (pass == 0 && p.job != JobType.Hunter)) continue;
                    bool busy = false;
                    foreach (var pl in list) if (pl.hunter == p.who && pl.end + 0.3f > now.hour && pl.start - 0.3f < now.hour + 1.8f) busy = true;
                    if (!busy) { pick = p; break; }
                }
            if (pick == null) return false;
            var taken = new HashSet<int>();
            foreach (var pl in list) if (!pl.resolved) taken.Add(pl.rabbitId);
            plan = MakeHunt(pick, now.day, now.hour + 0.02f, R(pick.key, now.day, 350) < HuntCatchChance, taken, new HashSet<int>(), 9, true);
            if (plan == null) return false;
            plan.debugStarted = true;
            list.Add(plan);
            Reindex(list);
            return true;
        }

        /// <summary>Debug ("hunt succeeds" / "hunt misses"): fixes how the next hunt that has not struck yet ends. A hunt already
        /// planned and still ahead of its strike changes now; with none, the next hunt started by <see cref="DebugStartHunt"/> takes it. A catch is refused (it stays a
        /// miss) while the herd is at its floor. Returns false for that.</summary>
        public bool DebugForceHunt(bool catches)
        {
            Sync();
            bool ok = !catches || sim.HuntSurplus > 0;
            HuntPlan target = null;
            if (huntDays.TryGetValue(huntNow.day, out var list))
                foreach (var pl in list)
                    if (!pl.resolved && pl.strikeStart > huntNow.hour && (target == null || pl.strikeStart < target.strikeStart)) target = pl;
            if (target == null) { huntForceNext = catches; return ok; }
            target.catches = catches && ok; target.forced = true;
            BuildSteps(target);
            Reindex(huntDays[huntNow.day]);
            return ok;
        }
    }
}
