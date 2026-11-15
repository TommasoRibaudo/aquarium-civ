using System;
using System.Collections.Generic;
using AquariumCiv.Sim;

namespace AquariumCiv.Life
{
    /// <summary>
    /// #700 (B27): a path grows over a rack; two grown-ups take it down, carry it a few steps and set it up on free ground
    /// (<see cref="RackMovePlan"/>). <see cref="RackSites"/> says that a rack wants to move and where to; this decides when (a quiet hour, nobody
    /// hanging or taking down, one beat at a time) and who (<see cref="RackMoveRules.PickCrew"/>), plays the crew's activities (<see cref="ActivityKind.MoveRack"/>)
    /// and moves the rack's committed angle as the carry ends. The view calls <see cref="ResolveRackMove"/> every frame.
    /// </summary>
    public sealed partial class LifePlanner
    {
        sealed class RackMoveRun
        {
            public RackMovePlan plan;
            public ViewTime start;
            public double startAbs, endAbs, walkStartAbs, walkOutEndAbs;
            public int lifterKey, carrierKey;
            public PersonRef lifter, carrier;
            public Place lifterFrom, carrierFrom, lifterTo, carrierTo, rackPlace;
            public bool committed;
        }

        RackMoveRun rackMove;
        // the holes the last move left at the old spot, until the next dawn
        RackMovePlan holesPlan;
        double holesFromAbs, holesUntilAbs;
        /// <summary>How many rack moves have started (tests, the cheat's report).</summary>
        public int RackMovesStarted { get; private set; }
        /// <summary>Why the last try to start a rack move did not (tests, the debug panel).</summary>
        public string RackMoveWhy { get; private set; } = "";

        static readonly Place HideRackPlace = Place.Land(PlaceKind.HideRack, 1);

        /// <summary>Real seconds between two view times (across midnight too).</summary>
        public static float SecondsBetween(ViewTime a, ViewTime b)
        {
            if (b.Absolute < a.Absolute) return -SecondsBetween(b, a);
            if (a.day == b.day) return LifeClock.RealSecondsBetween(a.hour, b.hour);
            float s = LifeClock.RealSecondsBetween(a.hour, 24f) + (b.day - a.day - 1) * LifeClock.RealSecondsPerDay + LifeClock.RealSecondsBetween(0f, b.hour);
            return s;
        }

        bool RackMoveReady => sim != null && sim.EcologyOn;

        /// <summary>The rack beat that runs or has been planned (null = none).</summary>
        public bool TryGetRackMove(ViewTime now, out RackMovePlan plan, out float since)
        {
            plan = null; since = -1f;
            if (rackMove == null) return false;
            since = SecondsBetween(rackMove.start, now);
            plan = rackMove.plan;
            return since >= 0f && since < plan.total + 0.01f;
        }

        /// <summary>True while a beat draws this kind of rack (the regular rack view stays away).</summary>
        public bool RackMoveCovers(RackKind kind, ViewTime now)
            => rackMove != null && rackMove.plan.kind == kind && TryGetRackMove(now, out var p, out float s) && s < p.total + 0.01f;

        /// <summary>The holes the frames left at the old spot (until the next dawn after the move), false when there are none now.</summary>
        public bool RackHoles(ViewTime now, List<(float x, float z)> into)
        {
            into?.Clear();
            if (holesPlan == null || now.Absolute < holesFromAbs || now.Absolute >= holesUntilAbs) return false;
            holesPlan.Holes(into ?? new List<(float, float)>());
            return true;
        }

        /// <summary>The crew's role and the beat's plan for a person, and the seconds into the beat (false for everyone else, or outside the beat).</summary>
        public bool TryGetRackRole(PersonRef who, ViewTime now, out RackMovePlan plan, out RackRole role, out float since)
        {
            plan = null; role = RackRole.Lifter; since = 0f;
            if (rackMove == null) return false;
            if (who == rackMove.lifter) role = RackRole.Lifter;
            else if (who == rackMove.carrier) role = RackRole.Carrier;
            else return false;
            plan = rackMove.plan;
            since = SecondsBetween(rackMove.start, now);
            return since >= 0f && since < plan.total;
        }

        /// <summary>Is any rack beat on (planned crew walking in, the beat, the walk back)?</summary>
        public bool RackMoveRunning => rackMove != null;

        /// <summary>The per-frame hook: starts a move when a rack wants one and the hour is quiet, commits the new angle as the carry ends,
        /// and lets go of the crew after the beat. Returns true while a move runs.</summary>
        public bool ResolveRackMove(ViewTime now)
        {
            Sync();
            if (!RackMoveReady || !FirePoint(out float fx, out float fz)) return false;
            RackSites.EnableBeats(sim);
            RackSites.Observe(sim, now);
            if (rackMove != null)
            {
                var r = rackMove;
                float since = SecondsBetween(r.start, now);
                if (!r.committed && (since >= r.plan.CommitAt || now.Absolute >= r.endAbs))
                {   // the carry ends: the rack stands at its new angle from here (positions, the camp's props and the other rack follow)
                    RackSites.Commit(sim, r.plan.kind, r.plan.newAngle); r.committed = true;
                    holesPlan = r.plan; holesFromAbs = r.startAbs; holesUntilAbs = NextDawn(r.endAbs);
                }
                if (now.Absolute >= r.walkOutEndAbs) { rackMove = null; ClearDays(); return false; }
                return true;
            }
            RackSites.Hide(sim, fx, fz); RackSites.Fish(sim, fx, fz); // the memo notes a rack a path covers
            for (int k = 0; k < 2; k++)
            {
                var kind = k == 0 ? RackKind.Fish : RackKind.Hide;
                if (kind == RackKind.Fish && !FishRackOn) continue;
                if (!RackSites.Pending(sim, kind, out float from, out float to)) continue;
                if (TryStartRackMove(kind, from, to, fx, fz, now)) return true;
            }
            return false;
        }

        static double NextDawn(double abs)
        {
            double day = Math.Floor(abs / 24.0);
            double h = abs - day * 24.0;
            return (h < LifeClock.Dawn ? day : day + 1) * 24.0 + LifeClock.Dawn;
        }

        string racking = "";

        bool SomeoneRacking(ViewTime now, RackKind kind)
        {
            racking = "";
            if (debugRack.Count > 0) { racking = "a cheat's hanger"; return true; }
            foreach (var d in fishDuties) if (d.started && !d.settled) { racking = "a " + (d.hang ? "hang" : "take-down") + " duty"; return true; }
            foreach (var d in debugFishDuties) if (!d.settled) { racking = "a cheat's duty"; return true; }
            if (kind == RackKind.Hide)
                foreach (var pl in tentPlans.Values) if (now.Absolute >= pl.StartAbsolute && now.Absolute < pl.EndAbsolute) { racking = "a tent raising"; return true; } // the hides are being taken to a tent
            return false;
        }

        bool TryStartRackMove(RackKind kind, float from, float to, float fx, float fz, ViewTime now)
        {
            EnsureDay(now.day);
            int cargo = 0, fresh = 0;
            if (kind == RackKind.Fish)
            {
                if (TryGetFishRack(now, out var look)) { cargo = look.Fish; fresh = look.fresh; }
            }
            else cargo = Math.Min(HidesOnFrame(now), 2);
            bool force = RackSites.Forced(sim);
            bool busy = SomeoneRacking(now, kind);
            if (!RackMoveRules.CanStart(LifeClock.PhaseOf(now.hour), kind == RackKind.Fish ? cargo : Math.Min(sim.HidesDrying, cargo), force, busy, false, true))
            { RackMoveWhy = $"{kind}: not yet (phase {LifeClock.PhaseOf(now.hour)}, cargo {cargo}, forced {force}, someone hanging or taking down {busy}: {racking})"; return false; }
            if (!force && now.hour > 23.2f) { RackMoveWhy = "too late in the night for a walk in"; return false; } // a walk in never crosses midnight

            var plan = RackMovePlan.Make(kind, fx, fz, from, to, cargo, fresh);
            var rackPlace = kind == RackKind.Fish ? FishRackPlace : HideRackPlace;
            plan.Centre(false, out float cx, out float cz);

            // the crew: the rack-keeper if free, else the nearest free grown-up; the Carrier is the nearest other
            var cands = new List<RackMoveRules.Candidate>();
            var byCandKey = new Dictionary<int, Person>();
            foreach (var q in people)
            {
                if (!q.Adult || q.job == JobType.Toolmaker || carerBabies.ContainsKey(q.key)) continue;
                bool free = !InHunt(q, now) && (force || !InBed(q, now)) && !TentStepAt(q.who, now, out _, out _);
                float dd = 0f;
                if (positions.TryGetPosition(BasePlace(q, now.day, now.hour), out float px, out float pz)) dd = (px - cx) * (px - cx) + (pz - cz) * (pz - cz);
                cands.Add(new RackMoveRules.Candidate(q.key, true, free, dd));
                byCandKey[q.key] = q;
            }
            var keeper = RackKeeper(dayInfo);
            if (!RackMoveRules.PickCrew(cands, keeper != null ? keeper.key : int.MinValue, out int lk, out int ck)) { RackMoveWhy = $"{kind}: fewer than two free grown-ups ({cands.Count} considered)"; return false; }
            var lifter = byCandKey[lk]; var carrier = byCandKey[ck];

            float walkL = Math.Max(0.1f, WalkHours(BasePlace(lifter, now.day, now.hour), rackPlace));
            float walkC = Math.Max(0.1f, WalkHours(BasePlace(carrier, now.day, now.hour), rackPlace));
            float walk = Math.Max(walkL, walkC);
            var start = now.AddHours(walk + 0.02f);
            var end = LifeClock.Advance(start, plan.total);
            double endAbs = end.Absolute;
            float back = 0.2f;
            var run = new RackMoveRun
            {
                plan = plan, start = start, startAbs = start.Absolute, endAbs = endAbs, walkStartAbs = now.Absolute,
                lifterKey = lk, carrierKey = ck, lifter = lifter.who, carrier = carrier.who, rackPlace = rackPlace,
                lifterFrom = BasePlace(lifter, now.day, now.hour), carrierFrom = BasePlace(carrier, now.day, now.hour),
                lifterTo = BasePlace(lifter, end.day, Math.Min(23.9f, end.hour + back)), carrierTo = BasePlace(carrier, end.day, Math.Min(23.9f, end.hour + back)),
            };
            run.walkOutEndAbs = endAbs + Math.Max(0.15, WalkHours(rackPlace, run.lifterTo));
            run.walkOutEndAbs = Math.Max(run.walkOutEndAbs, endAbs + Math.Max(0.15, WalkHours(rackPlace, run.carrierTo)));
            rackMove = run;
            RackMovesStarted++;
            RackSites.ForceQuiet(sim, false);
            ClearDays(); // the crew's days replan around the beat
            return true;
        }

        /// <summary>The crew's part of the beat as activities: the walk to the rack, <see cref="ActivityKind.MoveRack"/> (the Lifter with the basket), the walk back.</summary>
        bool RackMoveActivityAt(Person p, ViewTime t, out PersonActivity a)
        {
            a = default;
            var r = rackMove;
            if (r == null || buildDepth > 0 || huntBuilding || (p.key != r.lifterKey && p.key != r.carrierKey)) return false;
            bool lifter = p.key == r.lifterKey;
            double abs = t.Absolute;
            if (abs < r.walkStartAbs || abs >= r.walkOutEndAbs) return false;
            if (abs < r.startAbs)
                a = RackAct(p, t, ActivityKind.Walk, r.rackPlace, lifter ? r.lifterFrom : r.carrierFrom, Prop.None, true, r.walkStartAbs, r.startAbs);
            else if (abs < r.endAbs)
            {
                var prop = lifter && r.plan.cargo > 0 ? (r.plan.kind == RackKind.Fish ? Prop.FishBasket : Prop.Basket) : Prop.None;
                a = RackAct(p, t, ActivityKind.MoveRack, r.rackPlace, r.rackPlace, prop, false, r.startAbs, r.endAbs);
                a.count = r.plan.cargo;
            }
            else
                a = RackAct(p, t, ActivityKind.Walk, lifter ? r.lifterTo : r.carrierTo, r.rackPlace, Prop.None, true, r.endAbs, r.walkOutEndAbs);
            return true;
        }

        PersonActivity RackAct(Person p, ViewTime t, ActivityKind kind, Place place, Place from, Prop prop, bool moving, double segStartAbs, double segEndAbs)
        {
            double abs = t.Absolute, len = Math.Max(1e-4, segEndAbs - segStartAbs);
            float into = (float)Math.Max(0.0, abs - segStartAbs);
            return new PersonActivity
            {
                who = p.who, kind = kind, place = place, from = from, moving = moving && from != place, prop = prop,
                start = t.hour - into, end = t.hour - into + (float)len, progress = (float)Math.Min(1.0, Math.Max(0.0, (abs - segStartAbs) / len)),
                pace = 1f, chronicleId = -1, source = ActivitySource.Scene,
            };
        }

        // ---------------------------------------------------------------- debug (#700)

        /// <summary>Debug ("Rack moves now"): the next frame a rack a path covers is taken down at any hour. False when no rack wants to move.</summary>
        public bool DebugRackMoveNow(ViewTime now)
        {
            Sync();
            if (!RackMoveReady || !FirePoint(out float fx, out float fz)) return false;
            RackSites.EnableBeats(sim);
            RackSites.Hide(sim, fx, fz); RackSites.Fish(sim, fx, fz);
            bool any = RackSites.Pending(sim, RackKind.Fish, out _, out _) || RackSites.Pending(sim, RackKind.Hide, out _, out _);
            if (any) RackSites.ForceQuiet(sim, true);
            return any;
        }
    }
}
