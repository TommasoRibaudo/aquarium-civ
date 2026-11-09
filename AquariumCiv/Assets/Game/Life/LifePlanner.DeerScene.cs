// P9 #426 (animation-specs B13): the deer hunt as a scene the view plays. The plan (LifePlanner.DeerHunt.cs, #420) says who goes, when
// and whether the arrow hits; this lays each hunter's beats over it: to the fire for a bow, out in a line, the crouch at the wood's edge
// where the lead points, the stalk side by side (everyone freezes when the deer lifts its head), the shot, and then the pole carry home
// (a hit) or the shrug and the arrow pulled out of the ground (a miss). Every coordinate is box units on the site grid; every time a
// view hour of the plan's day; the beats' lengths are real seconds (DeerTiming), so the view and the planner agree.
using System;
using System.Collections.Generic;
using AquariumCiv.Sim;
using AquariumCiv.Sim.Eco;

namespace AquariumCiv.Life
{
    public sealed partial class LifePlanner
    {
        /// <summary>#426: the deer the hunters see. The view (DeerView) hands its herd here; without it a deer hunt has no scene (the sim
        /// still settles it, as before #426). Setting it forgets the deer hunts planned so far (the sim's marks keep them from settling twice).</summary>
        public DeerHerd.Field DeerField { get => deerField; set { if (ReferenceEquals(deerField, value)) return; deerField = value; deerDays.Clear(); } }
        DeerHerd.Field deerField;

        /// <summary>What the herd may give the hunters now (the floor, the shyness and the spring ruling, #422).</summary>
        public int DeerHuntSurplus => sim != null ? sim.DeerHuntSurplus : 0;

        /// <summary>The deer a hunt goes after at field second <paramref name="t"/>: a stag while the sim would take one (two stags or more:
        /// SimCore.DeerHuntCatch), else a doe; the nearest to the fire of those, never one within <see cref="DeerTiming.WaterKeep"/> of the
        /// water (B14.a: nobody hunts at the water). -1 when none will do.</summary>
        int DeerTarget(double t, float hour, float fx, float fz, out float tx, out float tz, out bool stag)
        {
            tx = tz = 0f; stag = false;
            if (deerField == null) return -1;
            bool wantStag = sim.EcoDeerStags >= 2;
            int best = -1, any = -1; float bd = float.MaxValue, ad = float.MaxValue;
            foreach (int id in deerField.Alive)
            {
                var p = deerField.Sample(id, t, hour);
                if (NearWater(p.x, p.z, DeerTiming.WaterKeep)) continue;
                float d = Dist(p.x, p.z, fx, fz);
                if (d < ad) { ad = d; any = id; }
                if (deerField.IsStag(id) == wantStag && d < bd) { bd = d; best = id; }
            }
            int pick = best >= 0 ? best : any;
            if (pick < 0) return -1;
            var at = deerField.Sample(pick, t, hour);
            tx = at.x; tz = at.z; stag = deerField.IsStag(pick);
            return pick;
        }

        /// <summary>Is there open water within <paramref name="r"/> of (x, z)?</summary>
        bool NearWater(float x, float z, float r)
        {
            if (!sim.EcologyOn) return false;
            int reach = (int)Math.Ceiling(r / AquariumCiv.Sim.Settle.SiteMap.Cell);
            int c0 = AquariumCiv.Sim.Settle.SiteMap.CellAt(x, z);
            if (c0 < 0) return false;
            int i0 = AquariumCiv.Sim.Settle.SiteMap.I(c0), j0 = AquariumCiv.Sim.Settle.SiteMap.J(c0);
            for (int j = Math.Max(0, j0 - reach); j <= Math.Min(AquariumCiv.Sim.Settle.SiteMap.NZ - 1, j0 + reach); j++)
                for (int i = Math.Max(0, i0 - reach); i <= Math.Min(AquariumCiv.Sim.Settle.SiteMap.NX - 1, i0 + reach); i++)
                {
                    int c = AquariumCiv.Sim.Settle.SiteMap.Index(i, j);
                    if (sim.EcoCoverAt(c) == EcoCover.Water && Dist(AquariumCiv.Sim.Settle.SiteMap.CXc(c), AquariumCiv.Sim.Settle.SiteMap.CZc(c), x, z) <= r) return true;
                }
            return false;
        }

        /// <summary>Is (x, z) within <paramref name="r"/> of open water? (B14.a's rule, for tests and the view.)</summary>
        public bool DeerSpotAtWater(float x, float z, float r = DeerTiming.WaterKeep) { Sync(); return NearWater(x, z, r); }

        /// <summary>
        /// #426: lay the hunters' beats over <paramref name="plan"/> (B13.a, B13.b). Needs <see cref="DeerField"/> (else the plan stays
        /// sim-only) and a deer not at the water. The white stag's hunt (#427) passes its spot in <paramref name="whiteX"/>/<paramref name="whiteZ"/>.
        /// </summary>
        void BuildDeerScene(DeerHuntPlan plan, float whiteX = float.NaN, float whiteZ = float.NaN)
        {
            plan.steps.Clear();
            if (plan.hunters.Count < DeerHuntersMin) return;
            bool white = !float.IsNaN(whiteX);
            if (!white && deerField == null) return;
            var crew = new List<Person>();
            foreach (var h in plan.hunters) { var p = FindPerson(h); if (p == null) return; crew.Add(p); }
            var ground = sim.EcoGround;
            float fx = ground.campX, fz = ground.campZ;
            plan.fireX = fx; plan.fireZ = fz;
            int n = crew.Count;
            // to the fire for the bows: everyone is there by 'gather'
            var bases = new Place[n]; var toFire = new float[n];
            float gather = plan.start;
            for (int i = 0; i < n; i++)
            {
                bases[i] = BasePlace(crew[i], plan.day, plan.start);
                toFire[i] = HuntWalkHours(bases[i], fx, fz);
                gather = Math.Max(gather, plan.start + toFire[i]);
            }
            float bowEnd = gather + Hrs(gather, 2.2f);
            // the deer, where it is about when they get near
            float tx, tz;
            if (white) { tx = whiteX; tz = whiteZ; plan.targetId = -1; plan.targetStag = true; plan.white = true; }
            else
            {
                float near = bowEnd + 0.35f;
                int id = DeerTarget(HuntClock.FieldSeconds(plan.day, near), near, fx, fz, out tx, out tz, out bool stag);
                if (id < 0) return;
                plan.targetId = id; plan.targetStag = stag;
            }
            plan.targetX = tx; plan.targetZ = tz;
            plan.playsHit = white || (plan.hits && sim.DeerHuntSurplus > 0);
            float dx = tx - fx, dz = tz - fz, len = (float)Math.Sqrt(dx * dx + dz * dz);
            float ux = len > 1e-3f ? dx / len : 1f, uz = len > 1e-3f ? dz / len : 0f;
            float px = -uz, pz = ux; // side by side
            plan.dirX = ux; plan.dirZ = uz;
            float crouchD = Math.Min(DeerTiming.CrouchRange, len * 0.75f), shotD = Math.Min(DeerTiming.ShotRange, crouchD * 0.6f);
            plan.shotX = tx - ux * shotD; plan.shotZ = tz - uz * shotD;
            // the stalk's rhythm, shared: the deer lifts its head and they all freeze
            int key = plan.hunters[0].Key;
            plan.freezeA = 0.8f + 0.7f * R(key, plan.day, 430);
            plan.freezeB = R(key, plan.day, 431) < 0.5f ? 0.8f + 0.7f * R(key, plan.day, 432) : 0f;
            // where the arrow comes down: the deer (a hit) or past it into the wood's edge (a miss)
            if (plan.playsHit) { plan.landX = tx; plan.landZ = tz; }
            else
            {
                float side = (R(key, plan.day, 433) < 0.5f ? -1f : 1f) * DeerTiming.MissSide;
                plan.landX = tx + ux * DeerTiming.MissPast + px * side; plan.landZ = tz + uz * DeerTiming.MissPast + pz * side;
            }
            // the set-down by the fire, on the deer's side
            plan.setX = fx + ux * DeerTiming.SetDownFromFire; plan.setZ = fz + uz * DeerTiming.SetDownFromFire;

            float Off(int i) => i == 0 ? 0f : (i == 1 ? 1f : -1f) * DeerTiming.SideGap; // the shooter in the middle, the others either side
            var lists = new List<HuntStep>[n];
            for (int i = 0; i < n; i++) { lists[i] = new List<HuntStep>(); plan.steps.Add(lists[i]); }
            void Add(int i, float h0, float h1, HuntPhase ph, ActivityKind k, Prop prop, float x0, float z0, float x1, float z1, bool moving, bool faceDeer, Place from = default, Place to = default)
            {
                if (h1 <= h0 + 1e-5f) return;
                lists[i].Add(new HuntStep
                {
                    phase = ph, start = h0, end = h1, from = from, to = to, hasXZ = ph != HuntPhase.ToFire && ph != HuntPhase.Return,
                    x0 = x0, z0 = z0, x1 = x1, z1 = z1, kind = k, prop = prop, moving = moving, faceRabbit = faceDeer,
                });
            }
            float ringR = 0.3f;
            (float x, float z) AtFire(int i) { double a = (i * 2.1 + 0.4); return (fx + (float)Math.Cos(a) * ringR, fz + (float)Math.Sin(a) * ringR); }
            // 1. to the fire, (wait), take a bow
            for (int i = 0; i < n; i++)
            {
                var f = AtFire(i);
                float h0 = plan.start, h1 = plan.start + toFire[i];
                Add(i, h0, h1, HuntPhase.ToFire, ActivityKind.Walk, Prop.None, 0f, 0f, f.x, f.z, true, false, bases[i], fire);
                Add(i, h1, gather, HuntPhase.DeerWait, ActivityKind.Watch, Prop.None, f.x, f.z, f.x, f.z, false, false);
                Add(i, gather, bowEnd, HuntPhase.DeerTakeBow, ActivityKind.Prepare, Prop.Bow, f.x, f.z, f.x, f.z, false, false);
            }
            // 2. out in a line, bows on their backs, to the crouch at the wood's edge
            float walkOut = WalkH(Dist(fx, fz, tx - ux * crouchD, tz - uz * crouchD));
            float lag = DeerTiming.LineGap / WalkSpeed;
            plan.walkOutHour = bowEnd;
            float arrived = bowEnd + walkOut + lag * (n - 1);
            for (int i = 0; i < n; i++)
            {
                var f = AtFire(i);
                float cx = tx - ux * crouchD + px * Off(i), cz = tz - uz * crouchD + pz * Off(i);
                float h0 = bowEnd + lag * i, h1 = h0 + walkOut;
                Add(i, bowEnd, h0, HuntPhase.DeerWait, ActivityKind.Watch, Prop.Bow, f.x, f.z, f.x, f.z, false, false);
                Add(i, h0, h1, HuntPhase.DeerWalkOut, ActivityKind.Walk, Prop.Bow, f.x, f.z, cx, cz, true, false);
                Add(i, h1, arrived, HuntPhase.DeerCrouch, ActivityKind.Stalk, Prop.Bow, cx, cz, cx, cz, false, true);
            }
            // 3. the crouch (the lead points), the stalk side by side, the shot
            float crouchEnd = arrived + Hrs(arrived, DeerTiming.CrouchSeconds);
            plan.stalkStart = crouchEnd;
            float stalkEnd = crouchEnd + Hrs(crouchEnd, plan.StalkSeconds);
            plan.shootStart = stalkEnd;
            float shootEnd = stalkEnd + Hrs(stalkEnd, DeerTiming.ShootSeconds);
            plan.releaseHour = stalkEnd + Hrs(stalkEnd, DeerTiming.ReleaseAt);
            plan.landHour = plan.resolveHour = stalkEnd + Hrs(stalkEnd, DeerTiming.LandAt);
            var sx = new float[n]; var sz = new float[n];
            for (int i = 0; i < n; i++)
            {
                float cx = tx - ux * crouchD + px * Off(i), cz = tz - uz * crouchD + pz * Off(i);
                sx[i] = tx - ux * shotD + px * Off(i); sz[i] = tz - uz * shotD + pz * Off(i);
                Add(i, arrived, crouchEnd, HuntPhase.DeerCrouch, ActivityKind.Stalk, Prop.Bow, cx, cz, cx, cz, false, true);
                Add(i, crouchEnd, stalkEnd, HuntPhase.DeerStalk, ActivityKind.Stalk, Prop.Bow, cx, cz, sx[i], sz[i], false, true);
                Add(i, stalkEnd, shootEnd, i == 0 ? HuntPhase.DeerShoot : HuntPhase.DeerWatchShot, ActivityKind.Stalk, Prop.Bow, sx[i], sz[i], sx[i], sz[i], false, true);
            }
            float t = shootEnd;
            var endX = new float[n]; var endZ = new float[n]; var endAt = new float[n];
            if (plan.playsHit)
            {
                // 4. to the deer; the shooter kneels (a hand on its neck); the runner fetches the pole; the two tie it on; the carry home
                var kx = new float[n]; var kz = new float[n];
                float toKill = 0f;
                for (int i = 0; i < n; i++)
                {
                    kx[i] = tx - ux * 0.13f + px * Off(i) * 0.7f; kz[i] = tz - uz * 0.13f + pz * Off(i) * 0.7f;
                    toKill = Math.Max(toKill, WalkH(Dist(sx[i], sz[i], kx[i], kz[i])));
                }
                for (int i = 0; i < n; i++) Add(i, t, t + toKill, HuntPhase.DeerToKill, ActivityKind.Walk, Prop.Bow, sx[i], sz[i], kx[i], kz[i], true, true);
                t += toKill;
                int run = Math.Min(plan.poleRunner, n - 1);
                // the pole's two ends: the back one (the shooter) on the deer's far side, the front one (the runner) toward home
                float backX = tx + ux * DeerTiming.PoleHalf, backZ = tz + uz * DeerTiming.PoleHalf;
                float frontX = tx - ux * DeerTiming.PoleHalf, frontZ = tz - uz * DeerTiming.PoleHalf;
                var f1 = AtFire(run);
                float runOut = WalkH(Dist(kx[run], kz[run], f1.x, f1.z), WalkSpeed * DeerTiming.RunPace);
                float take0 = t + runOut, take1 = take0 + Hrs(take0, DeerTiming.TakePoleSeconds);
                float runBack = WalkH(Dist(f1.x, f1.z, frontX, frontZ), WalkSpeed * DeerTiming.RunPace);
                float back = take1 + runBack;
                float thank = t + Hrs(t, DeerTiming.ThankSeconds);
                Add(run, t, take0, HuntPhase.DeerPoleRun, ActivityKind.RunOver, Prop.None, kx[run], kz[run], f1.x, f1.z, true, false);
                Add(run, take0, take1, HuntPhase.DeerTakePole, ActivityKind.Prepare, Prop.Pole, f1.x, f1.z, f1.x, f1.z, false, false);
                Add(run, take1, back, HuntPhase.DeerPoleBack, ActivityKind.RunOver, Prop.Pole, f1.x, f1.z, frontX, frontZ, true, false);
                Add(0, t, thank, HuntPhase.DeerThank, ActivityKind.Prepare, Prop.None, kx[0], kz[0], kx[0], kz[0], false, true);
                float toTie = WalkH(Dist(kx[0], kz[0], backX, backZ));
                float tieAt = Math.Max(back, thank + toTie);
                Add(0, thank, Math.Max(thank, tieAt - toTie), HuntPhase.DeerWait, ActivityKind.Watch, Prop.None, kx[0], kz[0], kx[0], kz[0], false, true);
                Add(0, Math.Max(thank, tieAt - toTie), tieAt, HuntPhase.DeerToTie, ActivityKind.Walk, Prop.None, kx[0], kz[0], backX, backZ, true, true);
                Add(run, back, tieAt, HuntPhase.DeerWait, ActivityKind.Watch, Prop.Pole, frontX, frontZ, frontX, frontZ, false, true);
                for (int i = 0; i < n; i++) if (i != 0 && i != run) Add(i, t, tieAt, HuntPhase.DeerWait, ActivityKind.Watch, Prop.Bow, kx[i], kz[i], kx[i], kz[i], false, true);
                float tieEnd = tieAt + Hrs(tieAt, DeerTiming.TieSeconds);
                Add(0, tieAt, tieEnd, HuntPhase.DeerTie, ActivityKind.Prepare, Prop.None, backX, backZ, backX, backZ, false, true);
                Add(run, tieAt, tieEnd, HuntPhase.DeerTie, ActivityKind.Prepare, Prop.None, frontX, frontZ, frontX, frontZ, false, true);
                for (int i = 0; i < n; i++) if (i != 0 && i != run) Add(i, tieAt, tieEnd, HuntPhase.DeerWait, ActivityKind.Watch, Prop.Bow, kx[i], kz[i], kx[i], kz[i], false, true);
                plan.takeHour = tieEnd;
                // the carry: the two in step, the deer under the pole, the third ahead with the bows
                float carry = WalkH(Dist(tx, tz, plan.setX, plan.setZ), WalkSpeed * DeerTiming.CarryPace);
                float carryEnd = tieEnd + carry;
                float sBackX = plan.setX + ux * DeerTiming.PoleHalf, sBackZ = plan.setZ + uz * DeerTiming.PoleHalf;
                float sFrontX = plan.setX - ux * DeerTiming.PoleHalf, sFrontZ = plan.setZ - uz * DeerTiming.PoleHalf;
                Add(0, tieEnd, carryEnd, HuntPhase.DeerCarry, ActivityKind.Carry, Prop.None, backX, backZ, sBackX, sBackZ, true, false);
                Add(run, tieEnd, carryEnd, HuntPhase.DeerCarry, ActivityKind.Carry, Prop.None, frontX, frontZ, sFrontX, sFrontZ, true, false);
                float aheadX = sFrontX - ux * 0.22f + px * 0.08f, aheadZ = sFrontZ - uz * 0.22f + pz * 0.08f;
                for (int i = 0; i < n; i++) if (i != 0 && i != run) Add(i, tieEnd, carryEnd, HuntPhase.DeerAhead, ActivityKind.Walk, Prop.Bow, kx[i], kz[i], aheadX, aheadZ, true, false);
                plan.setDownHour = carryEnd;
                float setEnd = carryEnd + Hrs(carryEnd, DeerTiming.SetDownSeconds);
                Add(0, carryEnd, setEnd, HuntPhase.DeerSetDown, ActivityKind.Prepare, Prop.None, sBackX, sBackZ, sBackX, sBackZ, false, false);
                Add(run, carryEnd, setEnd, HuntPhase.DeerSetDown, ActivityKind.Prepare, Prop.None, sFrontX, sFrontZ, sFrontX, sFrontZ, false, false);
                for (int i = 0; i < n; i++) if (i != 0 && i != run) Add(i, carryEnd, setEnd, HuntPhase.DeerWait, ActivityKind.Watch, Prop.Bow, aheadX, aheadZ, aheadX, aheadZ, false, false);
                for (int i = 0; i < n; i++)
                {
                    endAt[i] = setEnd;
                    (endX[i], endZ[i]) = i == 0 ? (sBackX, sBackZ) : i == run ? (sFrontX, sFrontZ) : (aheadX, aheadZ);
                }
            }
            else
            {
                // 4'. the shrug together; the shooter walks to the arrow and pulls it out; then home
                float shrugEnd = t + Hrs(t, DeerTiming.ShrugSeconds);
                for (int i = 0; i < n; i++) Add(i, t, shrugEnd, HuntPhase.DeerShrug, ActivityKind.Stalk, Prop.Bow, sx[i], sz[i], sx[i], sz[i], false, true);
                float ax = plan.landX - ux * DeerTiming.StandOff, az = plan.landZ - uz * DeerTiming.StandOff;
                float toArrow = WalkH(Dist(sx[0], sz[0], ax, az));
                float pull0 = shrugEnd + toArrow, pull1 = pull0 + Hrs(pull0, DeerTiming.PullSeconds);
                plan.arrowTakeHour = pull0 + Hrs(pull0, DeerTiming.PullTakeAt);
                Add(0, shrugEnd, pull0, HuntPhase.DeerToArrow, ActivityKind.Walk, Prop.Bow, sx[0], sz[0], ax, az, true, false);
                Add(0, pull0, pull1, HuntPhase.DeerPullArrow, ActivityKind.Prepare, Prop.Bow, ax, az, ax, az, false, false);
                for (int i = 1; i < n; i++) Add(i, shrugEnd, pull1, HuntPhase.DeerWait, ActivityKind.Watch, Prop.Bow, sx[i], sz[i], sx[i], sz[i], false, false);
                for (int i = 0; i < n; i++) { endAt[i] = pull1; endX[i] = i == 0 ? ax : sx[i]; endZ[i] = i == 0 ? az : sz[i]; }
                plan.takeHour = plan.setDownHour = pull1;
            }
            // 5. back into the day
            float last = 0f;
            for (int i = 0; i < n; i++)
            {
                var home = BasePlace(crew[i], plan.day, endAt[i] + 0.3f);
                float ret = HuntWalkHours(home, endX[i], endZ[i]);
                Add(i, endAt[i], endAt[i] + ret, HuntPhase.Return, ActivityKind.Walk, Prop.None, endX[i], endZ[i], 0f, 0f, true, false, fire, home);
                last = Math.Max(last, endAt[i] + ret);
            }
            plan.end = last;
            if (plan.end >= 23.9f || plan.landHour <= plan.start) { plan.steps.Clear(); return; } // the day ends first: sim-only
        }

        bool DeerHuntActivityAt(Person p, ViewTime t, out PersonActivity a)
        {
            a = default;
            if (huntBuilding || deerField == null && whiteHunts.Count == 0) return false;
            if (!DeerStepOf(p.who, t, out var st, out _)) return false;
            float len = Math.Max(1e-4f, st.end - st.start), into = Math.Max(0f, t.hour - st.start);
            int n = ActivityInfo.LoopSteps(st.kind);
            a = new PersonActivity
            {
                who = p.who, kind = st.kind, place = st.hasXZ ? FieldPlace(st.x1, st.z1) : st.to, from = st.moving ? (st.hasXZ ? FieldPlace(st.x0, st.z0) : st.from) : (st.hasXZ ? FieldPlace(st.x1, st.z1) : st.to),
                moving = st.moving, prop = st.prop, start = st.start, end = st.end, progress = Math.Min(1f, into / len),
                step = n <= 1 ? 0 : (int)(into / StepHours) % n, source = ActivitySource.Scene, pace = dayInfo.pace, chronicleId = -1,
                hunt = st.phase,
            };
            return true;
        }

        bool DeerStepOf(PersonRef who, ViewTime t, out HuntStep step, out DeerHuntPlan plan)
        {
            step = default; plan = null;
            if (TryDeerStep(DeerHuntsOf(t.day), who, t, out step, out plan)) return true;
            return TryDeerStep(whiteHunts, who, t, out step, out plan);
        }

        static bool TryDeerStep(List<DeerHuntPlan> plans, PersonRef who, ViewTime t, out HuntStep step, out DeerHuntPlan plan)
        {
            step = default; plan = null;
            foreach (var pl in plans)
            {
                if (pl.day != t.day || !pl.HasScene) continue;
                int i = pl.hunters.IndexOf(who);
                if (i < 0 || i >= pl.steps.Count) continue;
                var steps = pl.steps[i];
                if (steps.Count == 0 || t.hour < steps[0].start || t.hour >= steps[steps.Count - 1].end) continue;
                foreach (var st in steps) if (t.hour < st.end) { step = st; plan = pl; return true; }
            }
            return false;
        }

        /// <summary>#426: the deer-hunt beat a person is in at a time, and its plan (false when they are not in one).</summary>
        public bool DeerHuntStepAt(PersonRef who, ViewTime t, out HuntStep step, out DeerHuntPlan plan)
        {
            Sync();
            return DeerStepOf(who, t, out step, out plan);
        }

        /// <summary>The deer hunts with a scene that touch view day <paramref name="day"/> (the day's own and the white stag's).</summary>
        public void DeerScenesOn(long day, List<DeerHuntPlan> into)
        {
            into.Clear();
            Sync();
            foreach (var p in DeerHuntsOf(day)) if (p.HasScene) into.Add(p);
            foreach (var p in whiteHunts) if (p.day == day && p.HasScene) into.Add(p);
        }

        // ---------------------------------------------------------------- #427 the white stag's hunt
        readonly List<DeerHuntPlan> whiteHunts = new List<DeerHuntPlan>();

        /// <summary>#427 "Hunt it": two or three hunters go after the white stag standing at (x, z) from <paramref name="now"/> (B13.a with
        /// the white stag; it always hits: the sim already settled it, SimCore.ResolveWhiteStag). False when fewer than two can go or the
        /// day ends first.</summary>
        public bool StartWhiteStagHunt(ViewTime now, float x, float z, out DeerHuntPlan plan)
        {
            plan = null;
            Sync();
            if (sim == null || !sim.EcologyOn) return false;
            EnsureDay(now.day);
            whiteHunts.RemoveAll(p => p.day < now.day - 1);
            var made = MakeDeerHunt(now.day, now.hour + 0.02f, true);
            if (made == null) return false;
            made.hits = true; made.forced = true; made.debugStarted = true;
            BuildDeerScene(made, x, z);
            if (!made.HasScene) return false;
            whiteHunts.Add(made);
            plan = made;
            return true;
        }

        /// <summary>Settle the white stag's hunts whose arrow has landed (no sim change: the Keeper's answer settled it). How many.</summary>
        public int ResolveWhiteHunts(ViewTime now, List<DeerHuntPlan> settled = null)
        {
            int count = 0;
            foreach (var p in whiteHunts)
            {
                if (p.resolved || p.ResolveAbsolute > now.Absolute) continue;
                p.resolved = true; p.caught = true; p.stag = true;
                settled?.Add(p);
                count++;
            }
            return count;
        }
    }
}
