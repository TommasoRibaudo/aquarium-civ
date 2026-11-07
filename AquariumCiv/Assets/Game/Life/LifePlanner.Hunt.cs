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
        /// <summary>#362: the person faces the spear in the ground (<see cref="HuntPlan.landX"/>, <see cref="HuntPlan.landZ"/>).</summary>
        public bool faceSpear;
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
        /// <summary>t1-huntgrass: the stalk, throw and stand have no tall grass or bush between them and the default camera (false when no open approach existed).</summary>
        public bool openLine;
        public float start, end, strikeStart, resolveHour;
        /// <summary>lungeX/Z is the THROW point (#362: 0.4 u from the rabbit; it was the lunge point 0.125 u off it).</summary>
        public float targetX, targetZ, stalkX, stalkZ, lungeX, lungeZ, fireX, fireZ;
        /// <summary>#362: where the spear comes down (the rabbit on a catch, past or beside it on a miss), the spot the hunter stands to
        /// lift the catch or pull the spear, and the hours the spear leaves the hand, lands, and is taken up.</summary>
        /// <summary>t3-hunt: where the hunter holds the catch up: a few steps back toward the camp on open ground (the rabbit fell where it fell; the payoff is shown where the camera looks).</summary>
        public float holdX, holdZ;
        public bool holdOpen;
        public float landX, landZ, standX, standZ, releaseHour, landHour, takeHour, stalkStart;
        /// <summary>#362: the stalk's rhythm: the two freezes (seconds; 0 = none) and the wait for the rabbit to stop hopping.</summary>
        public float freezeA, freezeB, stalkExtra;
        public float StalkSeconds => HuntStalk.Duration(freezeA, freezeB, stalkExtra);
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
        public const float StalkRange = HuntTiming.StalkRange, TakeSpearHours = 0.12f, SkinHours = 0.3f, CookHours = 0.3f;
        /// <summary>Start windows: share of hunts that start in the morning (7 to 9:30), toward dusk (the last two hours of the
        /// afternoon), or otherwise in the working day.</summary>
        public const float HuntMorningShare = 0.45f, HuntDuskShare = 0.40f;

        /// <summary>t3-hunt: the camp frame the default camera shows (CampFrame.Frame: centre and radius, box units). The planner prefers rabbits and
        /// holds that stay inside it plus <see cref="HuntFrameMargin"/>; the centre is the fire unless <see cref="SetHuntFrame"/> says otherwise.</summary>
        public const float HuntFrameDefaultRadius = 1.4f, HuntFrameMargin = 0.3f;
        float huntFrameX, huntFrameZ, huntFrameR = HuntFrameDefaultRadius;
        bool huntFrameSet;
        public void SetHuntFrame(float x, float z, float radius) { huntFrameX = x; huntFrameZ = z; huntFrameR = radius; huntFrameSet = true; }   // applies to the days planned from now on (a day's hunts are never re-rolled mid-day)
        public void ClearHuntFrame() { huntFrameSet = false; huntFrameR = HuntFrameDefaultRadius; }
        /// <summary>Is the world point inside the camp frame (plus the margin)?</summary>
        public bool InHuntFrame(float x, float z)
        {
            float cx = huntFrameSet ? huntFrameX : sim.EcoGround.campX, cz = huntFrameSet ? huntFrameZ : sim.EcoGround.campZ;
            return Dist(x, z, cx, cz) <= huntFrameR + HuntFrameMargin;
        }

        /// <summary>t4-fixes (tester run 3: the strike at the screen's edge): the strike point keeps this far inside the camp frame.</summary>
        public const float HuntStrikeInset = 0.25f;
        /// <summary>t4-fixes: is the strike point well inside the camp frame (by <see cref="HuntStrikeInset"/>), so the throw is not at the edge?</summary>
        public bool StrikeWellInFrame(float x, float z)
        {
            float cx = huntFrameSet ? huntFrameX : sim.EcoGround.campX, cz = huntFrameSet ? huntFrameZ : sim.EcoGround.campZ;
            return Dist(x, z, cx, cz) <= huntFrameR - HuntStrikeInset;
        }

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
            plan.hunterBase = BasePlace(hunter, day, start);
            float toFire = HuntWalkHours(plan.hunterBase, fx, fz);

            // t1-huntgrass: the hunt is played on open ground (tall grass and bushes hide the doll at the default camera). Try the
            // picked rabbit first, then the next nearest (up to four); the first whose line is open wins, else the picked one is played.
            // t3-hunt: and in the camp frame (the strike and the rabbit where the camera looks): open + in frame, then open, then in frame.
            var order = new List<int> { cands[pick].id };
            for (int k = 0; k < cands.Count && order.Count < 10; k++) if (k != pick) order.Add(cands[k].id);
            float strikeAt = 0f;
            int bestRank = -1, bestIdx = 0;
            for (int oi = 0; oi < order.Count; oi++)
            {
                plan.rabbitId = order[oi];
                strikeAt = SolveAim(plan, hunter, day, start, slot, toFire, fx, fz);
                bool inFrame = InHuntFrame(plan.lungeX, plan.lungeZ) && InHuntFrame(plan.targetX, plan.targetZ);
                // t4-fixes: the old order (in the frame, then an open line), then a strike point well inside the frame (not at the edge)
                int rank = (inFrame ? 4 : 0) + (plan.openLine ? 2 : 0) + (StrikeWellInFrame(plan.lungeX, plan.lungeZ) ? 1 : 0);
                if (rank > bestRank) { bestRank = rank; bestIdx = oi; }
                if (rank == 7) break;
            }
            if (plan.rabbitId != order[bestIdx]) { plan.rabbitId = order[bestIdx]; strikeAt = SolveAim(plan, hunter, day, start, slot, toFire, fx, fz); }
            plan.strikeStart = strikeAt;
            plan.catches = wantCatch && sim.HuntSurplus > 0;
            if (useHeldForce && huntForceNext.HasValue) { plan.catches = huntForceNext.Value && sim.HuntSurplus > 0; plan.forced = true; huntForceNext = null; }

            if (R(hunter.key, day, 330 + slot) < HuntKidChance) PickKid(plan, hunter, day, slot, usedKids);
            BuildSteps(plan);
            return plan;
        }

        /// <summary>The stalk's rhythm and where the spear flies for the plan's rabbit; returns the strike hour (the old body of MakeHunt).</summary>
        float SolveAim(HuntPlan plan, Person hunter, long day, float start, int slot, float toFire, float fx, float fz)
        {
            // #362: the stalk's rhythm (the two freezes), then where the spear flies: the rabbit's spot at the strike, a fixed point of
            // the iteration start -> strike time -> spot. The strike waits for a moment the rabbit is not hopping (it stands still for the throw).
            plan.freezeA = 0.8f + 0.7f * R(hunter.key, day, 360 + slot);
            plan.freezeB = R(hunter.key, day, 362 + slot) < 0.5f ? 0.8f + 0.7f * R(hunter.key, day, 364 + slot) : 0f;
            plan.stalkExtra = 0f;
            float tx = 0f, tz = 0f, strikeAt = start + toFire + TakeSpearHours + 0.4f;
            for (int wait = 0; wait < 14; wait++)
            {
                strikeAt = start + toFire + TakeSpearHours + 0.4f + StalkHoursOf(plan, start);
                for (int it = 0; it < 4; it++)
                {
                    huntField.PositionOf(plan.rabbitId, HuntClock.FieldSeconds(day, strikeAt), out tx, out tz);
                    Geometry(plan, fx, fz, tx, tz);
                    float stalkAt = start + toFire + TakeSpearHours + WalkOutHours(plan);   // exactly as BuildSteps lays the steps out
                    strikeAt = stalkAt + Hrs(stalkAt, plan.StalkSeconds);
                }
                // Batch 1: the geometry is re-aimed at the strike below; the strike must not move with it (a rabbit that hopped in
                // between left the spear 0.4 off and the rabbit hopping through the throw: EcoHuntTests/HuntThrowTests).
                huntField.PositionOf(plan.rabbitId, HuntClock.FieldSeconds(day, strikeAt), out tx, out tz);
                Geometry(plan, fx, fz, tx, tz);
                float stalkAt2 = start + toFire + TakeSpearHours + WalkOutHours(plan);
                bool settled = Math.Abs(stalkAt2 + Hrs(stalkAt2, plan.StalkSeconds) - strikeAt) < 1e-4f;
                if (settled && !RabbitHopsBetween(plan.rabbitId, day, strikeAt, HuntTiming.StrikeSeconds + 0.3f)) break;
                plan.stalkExtra += 0.4f;
            }
            huntField.PositionOf(plan.rabbitId, HuntClock.FieldSeconds(day, strikeAt), out tx, out tz);
            Geometry(plan, fx, fz, tx, tz);
            return strikeAt;
        }

        /// <summary>Does something that hides a doll at the default camera (tall grass, a bush, reeds, a young or grown tree) stand on this world point's cell?</summary>
        bool HidesDoll(float x, float z)
        {
            if (!sim.EcologyOn) return false;
            var e = sim.State.eco;
            if (e == null || !e.on || e.cover == null) return false;
            int c = AquariumCiv.Sim.Settle.SiteMap.CellAt(x, z);
            if (c < 0 || c >= e.cover.Length) return false;
            var cv = (EcoCover)e.cover[c];
            return cv == EcoCover.Tall || cv == EcoCover.Bush || cv == EcoCover.Reeds || cv == EcoCover.Young || cv == EcoCover.Tree;
        }

        /// <summary>Approach turns tried round the rabbit when the straight line from the fire is hidden (radians, the camera-side-first order is seeded by the sign).</summary>
        static readonly float[] ApproachTurns = { 0f, 0.5f, -0.5f, 1.0f, -1.0f, 1.5f, -1.5f, 2.1f, -2.1f };

        /// <summary>The stalk, throw and stand spots for a rabbit at (tx, tz). The line from the fire is used when it is open; else the
        /// approach turns round the rabbit (t1-huntgrass) until the stalk, the throw and the stand have no tall grass or bush between them
        /// and the camera. <see cref="HuntPlan.openLine"/> says whether one was found.</summary>
        void Geometry(HuntPlan plan, float fx, float fz, float tx, float tz)
        {
            plan.targetX = tx; plan.targetZ = tz;
            float dx = tx - fx, dz = tz - fz, len = (float)Math.Sqrt(dx * dx + dz * dz);
            float ux = 1f, uz = 0f;
            if (len > 0.001f) { ux = dx / len; uz = dz / len; }
            float reach = Math.Min(StalkRange, len * 0.6f);
            float throwAt = Math.Min(HuntTiming.ThrowRange, reach * 0.55f);   // #362: the throw point, not a lunge
            plan.openLine = false;
            int best = int.MaxValue;
            for (int k = 0; k < ApproachTurns.Length; k++)
            {
                float a = ApproachTurns[k], ca = (float)Math.Cos(a), sa = (float)Math.Sin(a);
                float vx = ux * ca - uz * sa, vz = ux * sa + uz * ca;
                float sx = tx - vx * reach, sz = tz - vz * reach, lx = tx - vx * throwAt, lz = tz - vz * throwAt;
                int hidden = HuntOpen.Hidden(sx, sz, lx, lz, tx, tz, tx - vx * HuntTiming.StandOff, tz - vz * HuntTiming.StandOff, HidesDoll);
                if (k > 0 && !(PointInBox(sx, sz) && PointInBox(lx, lz))) continue;
                if (hidden < best)
                {   // the least hidden line so far (the straight one on a tie: it is tried first)
                    best = hidden;
                    plan.stalkX = sx; plan.stalkZ = sz; plan.lungeX = lx; plan.lungeZ = lz;
                    if (hidden == 0) { plan.openLine = true; return; }
                }
            }
        }

        static bool PointInBox(float x, float z) => AquariumCiv.Sim.Settle.SiteMap.CellAt(x, z) >= 0;

        /// <summary>#362: the hours the stalk lasts if it starts at about <paramref name="start"/>.</summary>
        static float StalkHoursOf(HuntPlan plan, float start) => LifeClock.ViewHoursFor(Math.Min(23.5f, start + 0.8f), plan.StalkSeconds);

        /// <summary>Hours that <paramref name="seconds"/> last from <paramref name="hour"/>, across the day's phases.</summary>
        static float Hrs(float hour, float seconds)
        {
            float lo = 0f, hi = LifeClock.ViewHoursFor(hour, seconds) * 3f + 0.01f;   // the phases' speeds differ, so bisect
            for (int i = 0; i < 24; i++) { float mid = (lo + hi) * 0.5f; if (LifeClock.RealSecondsBetween(hour, hour + mid) < seconds) lo = mid; else hi = mid; }
            return (lo + hi) * 0.5f;
        }

        /// <summary>Is the rabbit hopping at any moment of the window (field seconds from <paramref name="hour"/>)?</summary>
        bool RabbitHopsBetween(int id, long day, float hour, float seconds)
        {
            double t0 = HuntClock.FieldSeconds(day, hour);
            for (float s = -0.3f; s <= seconds; s += 0.25f) if (huntField.Sample(id, t0 + s).act == RabbitAct.Hop) return true;
            return false;
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
            var meadowThrow = FieldPlace(plan.lungeX, plan.lungeZ);

            // #362: where the spear comes down. A catch: on the rabbit. A miss: past it or beside it (seeded), and the spot the hunter
            // stands to pull it out is a step short of it, on the side he comes from.
            float ux = plan.targetX - plan.lungeX, uz = plan.targetZ - plan.lungeZ, ul = (float)Math.Sqrt(ux * ux + uz * uz);
            if (ul > 1e-4f) { ux /= ul; uz /= ul; } else { ux = 1f; uz = 0f; }
            if (plan.catches) { plan.landX = plan.targetX; plan.landZ = plan.targetZ; }
            else
            {
                float past = HuntTiming.MissPast + HuntTiming.MissPastMore * R(plan.hunter.Key, plan.day, 370);
                float side = (R(plan.hunter.Key, plan.day, 371) < 0.5f ? -1f : 1f) * HuntTiming.MissSide * (0.6f + 0.8f * R(plan.hunter.Key, plan.day, 372));
                plan.landX = plan.targetX + ux * past - uz * side; plan.landZ = plan.targetZ + uz * past + ux * side;
            }
            float sx = plan.landX - plan.lungeX, sz = plan.landZ - plan.lungeZ, sl = (float)Math.Sqrt(sx * sx + sz * sz);
            if (sl > 1e-4f) { sx /= sl; sz /= sl; }
            plan.standX = plan.landX - sx * HuntTiming.StandOff; plan.standZ = plan.landZ - sz * HuntTiming.StandOff;
            HoldSpot(plan);

            float toFire = HuntWalkHours(plan.hunterBase, fx, fz);
            AddStep(plan, ref t, toFire, HuntPhase.ToFire, ActivityKind.Walk, Prop.None, plan.hunterBase, fire, true, false);
            AddStep(plan, ref t, TakeSpearHours, HuntPhase.TakeSpear, ActivityKind.Prepare, Prop.Spear, fire, fire, false, false, fx, fz, fx, fz);
            AddStep(plan, ref t, WalkOutHours(plan), HuntPhase.WalkOut, ActivityKind.Walk, Prop.Spear, fire, meadowStalk, true, false, fx, fz, plan.stalkX, plan.stalkZ);
            plan.stalkStart = t;
            AddStep(plan, ref t, Hrs(t, plan.StalkSeconds), HuntPhase.Stalk, ActivityKind.Stalk, Prop.Spear, meadowStalk, meadowThrow, false, true, plan.stalkX, plan.stalkZ, plan.lungeX, plan.lungeZ);
            float strike0 = t;
            plan.strikeStart = strike0;
            AddStep(plan, ref t, Hrs(t, HuntTiming.StrikeSeconds), HuntPhase.Strike, ActivityKind.Stalk, Prop.Spear, meadowThrow, meadowThrow, false, true, plan.lungeX, plan.lungeZ, plan.lungeX, plan.lungeZ);
            plan.releaseHour = strike0 + Hrs(strike0, HuntTiming.ReleaseAt);
            plan.landHour = plan.resolveHour = strike0 + Hrs(strike0, HuntTiming.LandAt);   // settled when the spear comes down
            float afterStrike = t;
            if (plan.catches)
            {
                var atRabbit = FieldPlace(plan.standX, plan.standZ);
                AddStep(plan, ref t, WalkH(Dist(plan.lungeX, plan.lungeZ, plan.standX, plan.standZ)), HuntPhase.ToCatch, ActivityKind.Walk, Prop.None, meadowThrow, atRabbit, true, true, plan.lungeX, plan.lungeZ, plan.standX, plan.standZ);
                float pick0 = t;
                AddStep(plan, ref t, Hrs(t, HuntTiming.PickUpSeconds), HuntPhase.PickUp, ActivityKind.Prepare, Prop.None, atRabbit, atRabbit, false, true, plan.standX, plan.standZ, plan.standX, plan.standZ);
                plan.takeHour = pick0 + Hrs(pick0, HuntTiming.PickUpSeconds * HuntTiming.PickTakeAt);
                // t3-hunt: a few steps toward the camp onto open ground, then the hold-up there, then home
                var atHold = FieldPlace(plan.holdX, plan.holdZ);
                if (Dist(plan.standX, plan.standZ, plan.holdX, plan.holdZ) > 0.05f)
                    AddStep(plan, ref t, WalkH(Dist(plan.standX, plan.standZ, plan.holdX, plan.holdZ), WalkSpeed * HuntTiming.ProudWalk), HuntPhase.CarryBack, ActivityKind.Carry, Prop.Rabbit, atRabbit, atHold, true, false, plan.standX, plan.standZ, plan.holdX, plan.holdZ);
                float carry = WalkH(Dist(plan.holdX, plan.holdZ, fx, fz), WalkSpeed * HuntTiming.ProudWalk);
                // t4-fixes (tester run 3: the hunter showed his back): the hold-up faces the camera (PeopleView), not the rabbit's old spot
                AddStep(plan, ref t, Hrs(t, HuntTiming.HoldUpSeconds), HuntPhase.HoldUp, ActivityKind.HoldUp, Prop.Rabbit, atHold, atHold, false, false, plan.holdX, plan.holdZ, plan.holdX, plan.holdZ);
                AddStep(plan, ref t, carry, HuntPhase.CarryBack, ActivityKind.Carry, Prop.Rabbit, atHold, stash, true, false, plan.holdX, plan.holdZ, fx, fz);
                AddStep(plan, ref t, SkinHours, HuntPhase.Skin, ActivityKind.Prepare, Prop.Hide, fire, fire, false, false, fx, fz, fx, fz);
                AddStep(plan, ref t, CookHours, HuntPhase.Cook, ActivityKind.Cook, Prop.Stick, fire, fire, false, false, fx, fz, fx, fz);
            }
            else
            {
                // the shrug and the stamp; then to the spear, bend and pull it out; then home with it
                var atSpear = FieldPlace(plan.standX, plan.standZ);
                AddStep(plan, ref t, Hrs(t, HuntTiming.BoltSeconds), HuntPhase.Bolt, ActivityKind.Stalk, Prop.None, meadowThrow, meadowThrow, false, true, plan.lungeX, plan.lungeZ, plan.lungeX, plan.lungeZ);
                AddStep(plan, ref t, WalkH(Dist(plan.lungeX, plan.lungeZ, plan.standX, plan.standZ)), HuntPhase.FetchSpear, ActivityKind.Walk, Prop.None, meadowThrow, atSpear, true, false, plan.lungeX, plan.lungeZ, plan.standX, plan.standZ);
                float pull0 = t;
                AddStep(plan, ref t, Hrs(t, HuntTiming.PullSeconds), HuntPhase.PullSpear, ActivityKind.Prepare, Prop.Spear, atSpear, atSpear, false, false, plan.standX, plan.standZ, plan.standX, plan.standZ);
                plan.takeHour = pull0 + Hrs(pull0, HuntTiming.PullSeconds * HuntTiming.PullTakeAt);
                float back = WalkH(Dist(plan.standX, plan.standZ, fx, fz));
                AddStep(plan, ref t, back, HuntPhase.WalkBack, ActivityKind.Walk, Prop.Spear, atSpear, fire, true, false, plan.standX, plan.standZ, fx, fz);
            }
            // the walk back into the day
            var hunter = FindPerson(plan.hunter);
            plan.hunterBaseEnd = hunter != null ? BasePlace(hunter, plan.day, t + 0.3f) : fire;
            float ret = HuntWalkHours(plan.hunterBaseEnd, fx, fz);
            AddStep(plan, ref t, ret, HuntPhase.Return, ActivityKind.Walk, Prop.None, fire, plan.hunterBaseEnd, true, false);
            plan.end = t;

            if (!plan.kid.IsNone) BuildKidSteps(plan, afterStrike);
        }

        /// <summary>t3-hunt: the hold-up spot: a few steps from where the rabbit fell back toward the camp (never past it), on open ground
        /// (<see cref="HuntOpen.SpotClear"/>) inside the camp frame when one can be found; else where the hunter stands.</summary>
        void HoldSpot(HuntPlan plan)
        {
            plan.holdX = plan.standX; plan.holdZ = plan.standZ; plan.holdOpen = HuntOpen.SpotClear(plan.standX, plan.standZ, HidesDoll);
            float dx = plan.fireX - plan.standX, dz = plan.fireZ - plan.standZ, len = (float)Math.Sqrt(dx * dx + dz * dz);
            if (len < 0.5f) return;
            float ux = dx / len, uz = dz / len;
            float[] dists = { 0.55f, 0.7f, 0.4f, 0.85f, 0.3f };
            float[] turns = { 0f, 0.45f, -0.45f, 0.9f, -0.9f };
            int bestRank = -1;
            bool found = false; float bx = 0f, bz = 0f; bool bestOpen = false;
            foreach (float d0 in dists)
            {
                float d = Math.Min(d0, len * 0.6f);
                foreach (float a in turns)
                {
                    float ca = (float)Math.Cos(a), sa = (float)Math.Sin(a);
                    float x = plan.standX + (ux * ca - uz * sa) * d, z = plan.standZ + (ux * sa + uz * ca) * d;
                    if (!PointInBox(x, z)) continue;
                    bool open = HuntOpen.SpotClear(x, z, HidesDoll), inF = InHuntFrame(x, z);
                    int rank = (open ? 2 : 0) + (inF ? 1 : 0);
                    if (rank > bestRank) { bestRank = rank; bx = x; bz = z; bestOpen = open; found = true; }
                    if (rank == 3) goto done;
                }
            }
            done:
            if (found) { plan.holdX = bx; plan.holdZ = bz; plan.holdOpen = bestOpen; }
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
            if (plan.catches)
            {
                // watches until the spear comes down, cheers while the hunter walks to the rabbit, runs over as he lifts it, walks home beside him
                AddKid(plan, ref t, Math.Max(0.02f, plan.landHour - t), HuntPhase.KidWatch, ActivityKind.Watch, kidSpot, kidSpot, false, true, kx, kz);
                var toCatch = plan.steps.Find(s => s.phase == HuntPhase.ToCatch);
                var hold = plan.steps.Find(s => s.phase == HuntPhase.HoldUp);
                var carry = plan.steps.FindLast(s => s.phase == HuntPhase.CarryBack);
                AddKid(plan, ref t, Math.Max(0.05f, toCatch.end - t), HuntPhase.KidCheer, ActivityKind.Cheer, kidSpot, kidSpot, false, true, kx, kz);
                // the spot beside the hunter, a little to the side of the line home
                float sideX = -uz * 0.12f, sideZ = ux * 0.12f;
                float bx = plan.holdX + sideX, bz = plan.holdZ + sideZ;
                AddKidXY(plan, ref t, Math.Max(0.05f, hold.end - t), HuntPhase.KidRun, ActivityKind.RunOver, kidSpot, FieldPlace(bx, bz), true, true, kx, kz, bx, bz);
                AddKidXY(plan, ref t, Math.Max(0.05f, carry.end - t), HuntPhase.KidEscort, ActivityKind.Walk, FieldPlace(bx, bz), stash, true, false, bx, bz, fx + sideX, fz + sideZ);
            }
            else
            {
                var bolt = plan.steps.Find(s => s.phase == HuntPhase.Bolt);
                AddKid(plan, ref t, Math.Max(0.05f, bolt.end - t), HuntPhase.KidWatch, ActivityKind.Watch, kidSpot, kidSpot, false, true, kx, kz);
            }
            plan.kidBaseEnd = BasePlace(kid, plan.day, t + 0.5f);
            float home = Math.Max(HuntWalkHours(plan.kidBaseEnd, plan.catches ? fx : kx, plan.catches ? fz : kz), 0.15f);
            if (plan.catches) AddKid(plan, ref t, home, HuntPhase.KidBack, ActivityKind.Walk, stash, plan.kidBaseEnd, true, false, fx, fz);
            else AddKid(plan, ref t, home, HuntPhase.KidBack, ActivityKind.Walk, kidSpot, plan.kidBaseEnd, true, false, kx, kz);
        }

        void AddKidXY(HuntPlan plan, ref float t, float hours, HuntPhase phase, ActivityKind kind, Place from, Place to, bool moving, bool faceRabbit,
            float x0, float z0, float x1, float z1)
        {
            plan.kidSteps.Add(new HuntStep
            {
                phase = phase, start = t, end = t + hours, from = from, to = to, hasXZ = true, kind = kind, prop = Prop.None,
                moving = moving, faceRabbit = faceRabbit, x0 = x0, z0 = z0, x1 = x1, z1 = z1,
            });
            t += hours;
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
                faceSpear = phase == HuntPhase.FetchSpear || phase == HuntPhase.PullSpear,
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
                    // Batch 2: a live block (a staged moment, a callback, an errand) beats a hunt in PlanBase: don't start one under it
                    // (HuntViewPlayTests: the hunter danced at a staged wedding and the hunt ran unseen behind it).
                    foreach (var bl in BlocksOf(p)) if (bl.be > now.hour && bl.bs < now.hour + 1.8f) busy = true;
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
