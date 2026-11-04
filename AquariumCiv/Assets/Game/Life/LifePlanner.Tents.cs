using System;
using System.Collections.Generic;
using AquariumCiv.Sim;

namespace AquariumCiv.Life
{
    /// <summary>#284: which beat of a tent raising a <see cref="PersonActivity"/> is (None = not one). The builders' beats come in this
    /// order: ToFrame, TakeLoad, CarryOut, Raise, then (the camp's first tent only) Stand, then Return. A kid who watches plays
    /// ToFrame (the walk over), KidWatch, KidCheer, Return.</summary>
    public enum TentPhase : byte { None, ToFrame, TakeLoad, CarryOut, Raise, Stand, Return, KidWatch, KidCheer }

    /// <summary>One beat of a tent raising. Times are hours since <see cref="TentPlan.startTime"/>; coordinates are world x/z.
    /// <see cref="hasXZ"/> is false when an end is a place the view resolves (the walk from where they were, the walk home).</summary>
    public struct TentStep
    {
        public TentPhase phase;
        public float start, end;
        public Place from, to;
        public bool hasXZ;
        public float x0, z0, x1, z1;
        public ActivityKind kind;
        public Prop prop;
        public bool moving;
    }

    /// <summary>One person's part in a tent raising.</summary>
    public sealed class TentWorker
    {
        public PersonRef who;
        /// <summary>What they carry out from the frame: <see cref="Prop.Log"/> (the poles) or <see cref="Prop.Hide"/> (the hides).</summary>
        public Prop load;
        public bool isKid;
        public Place baseStart, baseEnd;
        public readonly List<TentStep> steps = new List<TentStep>();
    }

    /// <summary>
    /// #284 phase 1: a tent raised from hides, as data the view plays. Builders walk to the frame by the fire (where the hides dry),
    /// take poles and hides, carry them to the lot and raise the tent there. The first new tent after the camp gate is the camp's
    /// beginning (<see cref="first"/>): its people stand back a moment at the end, and a kid watches and cheers. The sim has already
    /// built the tent and taken the hides; the view shows the tent growing from <see cref="raiseStart"/> to full at
    /// <see cref="raiseEnd"/> and the frame losing <see cref="hides"/> hides at <see cref="takeAt"/>.
    /// </summary>
    public sealed class TentPlan
    {
        public int buildingId;
        public bool first;
        public ViewTime startTime;
        /// <summary>Hours since <see cref="startTime"/>.</summary>
        public float end, takeAt, raiseStart, raiseEnd;
        public int hides;
        public float frameX, frameZ, lotX, lotZ, doorX, doorZ;
        public readonly List<TentWorker> workers = new List<TentWorker>();
        public double StartAbsolute => startTime.Absolute;
        public double EndAbsolute => startTime.Absolute + end;
    }

    /// <summary>#284 phase 1: the hide frame by the fire and the tent raising (see <see cref="TentPlan"/>).</summary>
    public sealed partial class LifePlanner
    {
        // ---------------------------------------------------------------- tuning [est]
        /// <summary>Hours of the beats; the walk beats follow the distances.</summary>
        public const float TentTakeHours = 0.15f, TentRaiseHours = 0.6f, TentStandHours = 0.2f;
        /// <summary>How far from the fire the hide frame stands (world units) and how far apart its hides hang.</summary>
        public const float HideFrameDistance = 0.34f, HideFrameSpacing = 0.075f;
        /// <summary>The frame shows this many hides at most.</summary>
        public const int HideFrameMax = 8;
        /// <summary>The builders of a tent: the builder named by the sim, then others, up to this many grown-ups.</summary>
        public const int TentBuilders = 3;

        readonly Dictionary<int, TentPlan> tentPlans = new Dictionary<int, TentPlan>();

        // ---------------------------------------------------------------- the frame

        /// <summary>Where the fire is now (follows it when the fire moves), world x/z.</summary>
        public bool FirePoint(out float x, out float z)
        {
            Sync();
            if (positions.TryGetPosition(fire, out x, out z)) return true;
            var g = sim.EcoGround; x = g.campX; z = g.campZ; return sim.EcologyOn;
        }

        /// <summary>The hide frame beside the fire: its centre and the direction along its bar (unit x/z). It stands a fixed distance from the
        /// fire on a side the seed picks, so it moves with the fire. False without an ecology.</summary>
        public bool HideFrame(out float x, out float z, out float dirX, out float dirZ)
        {
            x = z = dirX = dirZ = 0f;
            if (!sim.EcologyOn || !FirePoint(out float fx, out float fz)) return false;
            float a = (H(0, 0, 410) >> 8) * (1f / 16777216f) * 6.2831853f;
            float ux = (float)Math.Cos(a), uz = (float)Math.Sin(a);
            x = fx + ux * HideFrameDistance; z = fz + uz * HideFrameDistance;
            dirX = -uz; dirZ = ux; // the bar runs across the line from the fire
            return true;
        }

        /// <summary>Where the hides hang on the frame: one point per hide on the frame now (at most <see cref="HideFrameMax"/>), evenly along
        /// the bar. Returns the true count (see <see cref="HidesOnFrame"/>).</summary>
        public int HideFramePoints(ViewTime now, List<(float x, float z)> into)
        {
            into?.Clear();
            if (!HideFrame(out float cx, out float cz, out float dx, out float dz)) return 0;
            int n = HidesOnFrame(now), shown = Math.Min(n, HideFrameMax);
            if (into != null)
                for (int i = 0; i < shown; i++)
                {
                    float o = (i - (shown - 1) * 0.5f) * HideFrameSpacing;
                    into.Add((cx + dx * o, cz + dz * o));
                }
            return n;
        }

        /// <summary>Hides on the frame at a time: the sim's drying hides plus those a tent in the making has not taken off yet.</summary>
        public int HidesOnFrame(ViewTime now)
        {
            int n = sim.HidesDrying;
            foreach (var p in tentPlans.Values)
                if (now.Absolute < p.StartAbsolute + p.takeAt) n += p.hides;
            return n;
        }

        // ---------------------------------------------------------------- making one raising

        /// <summary>Plan the raising of a tent the sim has just built (<see cref="SimCore.TentRaised"/>), starting at <paramref name="now"/>.
        /// False when the building is unknown, is not placed on a lot yet (try again next frame), or nobody can build it. Planning twice
        /// for the same building returns the same plan.</summary>
        public bool BeginTentRaising(int buildingId, ViewTime now, bool first, out TentPlan plan)
        {
            Sync();
            if (tentPlans.TryGetValue(buildingId, out plan)) return true;
            plan = null;
            Building b = null;
            foreach (var x in sim.State.buildings) if (x.id == buildingId) { b = x; break; }
            if (b == null || b.lotId == 0 || !HideFrame(out float frx, out float frz, out _, out _)) return false;
            EnsureDay(now.day);
            BuildCarers();

            plan = new TentPlan
            {
                buildingId = buildingId, first = first, startTime = now, hides = SimCore.HidesPerTent,
                frameX = frx, frameZ = frz, lotX = b.x, lotZ = b.z, doorX = b.doorX, doorZ = b.doorZ,
            };
            var chosen = ChooseBuilders(b, now);
            if (chosen.Count == 0) { plan = null; return false; }
            float hours = 0f;
            for (int i = 0; i < chosen.Count; i++)
            {
                var w = new TentWorker { who = chosen[i].who, load = i == 1 ? Prop.Hide : Prop.Log };
                w.baseStart = BasePlace(chosen[i], now.day, now.hour);
                hours = Math.Max(hours, BuildWorkerSteps(plan, w, chosen[i], now, i, chosen.Count));
                plan.workers.Add(w);
            }
            plan.end = hours;
            if (first) AddTentKid(plan, now);
            tentPlans[buildingId] = plan;
            return true;
        }

        List<Person> ChooseBuilders(Building b, ViewTime now)
        {
            var list = new List<Person>();
            if (b.builderId > 0)
                foreach (var p in people) if (p.n != null && p.n.id == b.builderId && p.Adult && !InHunt(p, now)) { list.Add(p); break; }
            var rest = new List<Person>();
            foreach (var p in people)
                if (p.Adult && !list.Contains(p) && p.job != JobType.Toolmaker && !carerBabies.ContainsKey(p.key) && !InHunt(p, now)) rest.Add(p);
            rest.Sort((a, c) =>
            {
                int k = H(a.key, b.id, 420).CompareTo(H(c.key, b.id, 420));
                return k != 0 ? k : a.key.CompareTo(c.key);
            });
            foreach (var p in rest) { if (list.Count >= TentBuilders) break; list.Add(p); }
            return list;
        }

        bool InHunt(Person p, ViewTime now) => HuntStepAt(p.who, now, out _, out _);

        float BuildWorkerSteps(TentPlan plan, TentWorker w, Person p, ViewTime now, int index, int count)
        {
            float t = 0f;
            float fx = plan.frameX, fz = plan.frameZ;
            // each builder stands at their own spot along the lot's front, so they don't stack
            float side = (index - (count - 1) * 0.5f) * 0.09f;
            float ddx = plan.doorX - plan.lotX, ddz = plan.doorZ - plan.lotZ, dl = (float)Math.Sqrt(ddx * ddx + ddz * ddz);
            float ux = dl > 0.001f ? ddx / dl : 0f, uz = dl > 0.001f ? ddz / dl : 1f;
            float sx = plan.doorX - uz * side, sz = plan.doorZ + ux * side;
            var lot = new Place(PlaceKind.Home, plan.buildingId);
            AddT(w, ref t, HuntWalkHours(w.baseStart, fx, fz), TentPhase.ToFrame, ActivityKind.Walk, Prop.None, w.baseStart, fire, true, false, true, fx, fz);
            AddT(w, ref t, TentTakeHours, TentPhase.TakeLoad, ActivityKind.Prepare, w.load, fire, fire, false, true, true, fx, fz, fx, fz);
            if (index == 0) plan.takeAt = t;
            AddT(w, ref t, WalkH(Dist(fx, fz, sx, sz), WalkSpeed * 0.8f), TentPhase.CarryOut, ActivityKind.Carry, w.load, fire, lot, true, true, true, fx, fz, sx, sz);
            if (index == 0) plan.raiseStart = t;
            AddT(w, ref t, TentRaiseHours, TentPhase.Raise, ActivityKind.Hammer, Prop.Hammer, lot, lot, false, true, true, sx, sz, sx, sz);
            if (index == 0) plan.raiseEnd = t;
            if (plan.first) AddT(w, ref t, TentStandHours, TentPhase.Stand, ActivityKind.Watch, Prop.None, lot, lot, false, true, true, sx, sz, sx, sz);
            w.baseEnd = BasePlace(p, now.day, now.hour + t + 0.3f);
            AddT(w, ref t, HuntWalkHours(w.baseEnd, sx, sz), TentPhase.Return, ActivityKind.Walk, Prop.None, lot, w.baseEnd, true, false, false, sx, sz);
            return t;
        }

        void AddTentKid(TentPlan plan, ViewTime now)
        {
            Person kid = null; uint best = uint.MaxValue;
            foreach (var q in people)
            {
                if (q.stage != LifeStage.Kid || InHunt(q, now)) continue;
                uint h = H(q.key, plan.buildingId, 421);
                if (h < best) { best = h; kid = q; }
            }
            if (kid == null) return;
            var w = new TentWorker { who = kid.who, isKid = true, baseStart = BasePlace(kid, now.day, now.hour) };
            float kx = plan.doorX, kz = plan.doorZ + 0.18f;
            var lot = new Place(PlaceKind.Home, plan.buildingId);
            float t = 0f;
            float arrive = Math.Max(HuntWalkHours(w.baseStart, kx, kz), plan.raiseStart * 0.8f);
            AddT(w, ref t, arrive, TentPhase.ToFrame, ActivityKind.Walk, Prop.None, w.baseStart, lot, true, false, false, kx, kz);
            AddT(w, ref t, Math.Max(0.05f, plan.raiseEnd - t), TentPhase.KidWatch, ActivityKind.Watch, Prop.None, lot, lot, false, true, true, kx, kz, kx, kz);
            AddT(w, ref t, TentStandHours, TentPhase.KidCheer, ActivityKind.Cheer, Prop.None, lot, lot, false, true, true, kx, kz, kx, kz);
            w.baseEnd = BasePlace(kid, now.day, now.hour + t + 0.3f);
            AddT(w, ref t, Math.Max(0.15f, HuntWalkHours(w.baseEnd, kx, kz)), TentPhase.Return, ActivityKind.Walk, Prop.None, lot, w.baseEnd, true, false, false, kx, kz);
            plan.workers.Add(w);
            plan.end = Math.Max(plan.end, t);
        }

        /// <summary>A step. With <paramref name="both"/> true both ends have coordinates (x0, z0, x1, z1); false: a walk from a place the
        /// view resolves (a ToFrame walk: only x1, z1 given as the first pair) or home (only x0, z0 given as the first pair).</summary>
        static void AddT(TentWorker w, ref float t, float hours, TentPhase phase, ActivityKind kind, Prop prop, Place from, Place to,
            bool moving, bool face, bool both, float a, float b, float c = 0f, float d = 0f)
        {
            var st = new TentStep { phase = phase, start = t, end = t + hours, from = from, to = to, kind = kind, prop = prop, moving = moving };
            if (both) { st.hasXZ = true; st.x0 = a; st.z0 = b; st.x1 = c; st.z1 = d; }
            else if (phase == TentPhase.Return) { st.x0 = a; st.z0 = b; }   // from a spot to a place the view resolves
            else { st.x1 = a; st.z1 = b; }                                  // from a place the view resolves to a spot
            w.steps.Add(st);
            t += hours;
        }

        // ---------------------------------------------------------------- what the view asks

        /// <summary>The raising planned for a building (null = none, or the view has not begun it).</summary>
        public TentPlan TentPlanOf(int buildingId) { tentPlans.TryGetValue(buildingId, out var p); return p; }

        /// <summary>How far a tent is raised at a time, 0 (not started) to 1 (standing). The view grows the tent with this; a tent with no plan is 1.</summary>
        public float TentRaised(int buildingId, ViewTime now)
        {
            if (!tentPlans.TryGetValue(buildingId, out var p)) return 1f;
            double t = now.Absolute - p.StartAbsolute;
            if (t <= p.raiseStart) return 0f;
            if (t >= p.raiseEnd) return 1f;
            return (float)((t - p.raiseStart) / Math.Max(1e-4f, p.raiseEnd - p.raiseStart));
        }

        /// <summary>The tent beat a person is in at a time, with its plan (false when they are not building one).</summary>
        public bool TentStepAt(PersonRef who, ViewTime t, out TentStep step, out TentPlan plan)
        {
            step = default; plan = null;
            foreach (var pl in tentPlans.Values)
            {
                double into = t.Absolute - pl.StartAbsolute;
                if (into < 0 || into >= pl.end) continue;
                foreach (var w in pl.workers)
                {
                    if (w.who != who || w.steps.Count == 0) continue;
                    if (into >= w.steps[w.steps.Count - 1].end) continue;
                    foreach (var st in w.steps) if (into < st.end) { step = st; plan = pl; return true; }
                }
            }
            return false;
        }

        bool TentActivityAt(Person p, ViewTime t, out PersonActivity a)
        {
            a = default;
            if (huntBuilding || tentPlans.Count == 0) return false;
            if (!TentStepAt(p.who, t, out var st, out var plan)) return false;
            double into = t.Absolute - plan.StartAbsolute;
            float len = Math.Max(1e-4f, st.end - st.start), inStep = Math.Max(0f, (float)into - st.start);
            int n = ActivityInfo.LoopSteps(st.kind);
            a = new PersonActivity
            {
                who = p.who, kind = st.kind, place = st.to, from = st.moving ? st.from : st.to, moving = st.moving && st.from != st.to,
                prop = st.prop, start = t.hour - inStep, end = t.hour - inStep + len, progress = Math.Min(1f, inStep / len),
                step = n <= 1 ? 0 : (int)(inStep / StepHours) % n, source = ActivitySource.Scene, pace = dayInfo.pace, chronicleId = -1,
                tent = st.phase,
            };
            return true;
        }

        /// <summary>Forget finished raisings (the view calls this now and then; plans a day old are dropped).</summary>
        public void PruneTents(ViewTime now)
        {
            List<int> drop = null;
            foreach (var kv in tentPlans) if (now.Absolute > kv.Value.EndAbsolute + 24.0) (drop ??= new List<int>()).Add(kv.Key);
            if (drop != null) foreach (var k in drop) tentPlans.Remove(k);
        }
    }
}
