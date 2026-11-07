using System;
using System.Collections.Generic;
using System.Linq;
using AquariumCiv.Life;
using AquariumCiv.Sim;
using NUnit.Framework;

namespace AquariumCiv.Life.Tests
{
    /// <summary>
    /// #370: the staged moments (animation-specs C3; visit-loop.md §2.1, §4.3). The pure plan: who plays which role, the
    /// beginning, middle and end beat in order, back to back, everyone released by the end. The planner: it casts real people,
    /// plays the plan as a live scene, carries the newborn, lets everyone go when it ends or is stopped, and a line whose
    /// ceremony is held no longer also plays as this month's scene.
    /// </summary>
    public class StagedMomentTests
    {
        // ---------------------------------------------------------------- the pure plan

        static readonly MomentGeometry Geo = new MomentGeometry { fireX = 0f, fireZ = 0.2f, glassX = 0f, glassZ = -1.95f, nx = 0f, nz = -1f, halfX = 3f, halfZ = 2.2f };

        static MomentPerson P(int key, float x, float z, bool kid = false, MomentKind kind = MomentKind.Wedding)
        {
            float px = kind == MomentKind.Newborn ? Geo.glassX : Geo.fireX, pz = kind == MomentKind.Newborn ? Geo.glassZ : Geo.fireZ;
            return MomentPerson.Of(key, kid, x, z, (float)Math.Sqrt((x - px) * (x - px) + (z - pz) * (z - pz)) / 0.31f);
        }

        static MomentCast Cast(MomentKind kind, bool b = true, bool officiant = true, int grown = 5, int kids = 2)
        {
            var c = new MomentCast { a = P(1, 0.9f, 0.9f, false, kind), hasA = true };
            if (b) { c.b = P(2, -0.6f, 0.75f, false, kind); c.hasB = true; }
            if (officiant) { c.officiant = P(3, 0.35f, 0.65f, false, kind); c.hasOfficiant = true; }
            for (int i = 0; i < grown; i++) c.grown.Add(P(10 + i, -1.2f + 0.5f * i, 0.4f + 0.2f * (i % 3), false, kind));
            for (int i = 0; i < kids; i++) c.kids.Add(P(-1 - i, 0.6f - 1.4f * i, -0.6f, true, kind));
            if (kind == MomentKind.Newborn) c.babyKey = 99;
            return c;
        }

        static MomentStagePlan Build(MomentKind kind, MomentCast c = null)
        {
            var plan = MomentStagePlan.Build(kind, c ?? Cast(kind), Geo, 370);
            Assert.IsNotNull(plan, $"{kind}: {MomentStagePlan.LastWhyNot}");
            return plan;
        }

        static IEnumerable<MomentKind> Kinds => new[] { MomentKind.Wedding, MomentKind.Newborn, MomentKind.NewLeader, MomentKind.Festival };

        [TestCaseSource(nameof(Kinds))]
        public void EveryMember_StepsBackToBack_EndsOnAnEndBeat_AndIsReleasedByTheEnd(MomentKind kind)
        {
            var plan = Build(kind);
            Assert.Greater(plan.members.Count, 2, "a crowd plays it");
            Assert.Greater(plan.Duration, 8f);
            foreach (var m in plan.members)
            {
                Assert.IsNotEmpty(m.steps, $"{m.role} {m.key} has steps");
                for (int i = 1; i < m.steps.Count; i++)
                    Assert.AreEqual(m.steps[i - 1].t1, m.steps[i].t0, 1e-4f, $"{kind} {m.role} {m.key}: step {i} ({m.steps[i].beat}) starts where {m.steps[i - 1].beat} ended");
                foreach (var s in m.steps) Assert.Less(s.t0, s.t1, $"{kind} {m.role}: {s.beat} has a length");
                Assert.IsTrue(m.steps[m.steps.Count - 1].endBeat, $"{kind} {m.role} {m.key} ends on an end beat, not {m.steps[m.steps.Count - 1].beat}");
                Assert.LessOrEqual(m.ReleaseAt, plan.Duration + 1e-4f);
                Assert.IsFalse(plan.StepAt(m.key, plan.Duration, out _, out _), "released at the end");
                Assert.IsFalse(plan.StepAt(m.key, plan.Duration + 5f, out _, out _));
                Assert.IsTrue(plan.StepAt(m.key, m.ReleaseAt - 0.01f, out _, out int last) && last == m.steps.Count - 1, "in the end beat just before the release");
                foreach (var s in m.steps)
                {
                    Assert.LessOrEqual(Math.Abs(s.x1), Geo.halfX); Assert.LessOrEqual(Math.Abs(s.z1), Geo.halfZ, "inside the box");
                }
            }
            Assert.AreEqual(plan.members.Count, plan.members.Select(m => m.key).Distinct().Count(), "one part each");
        }

        /// <summary>t4-fixes (tester run 3, b9_wedding_17s): the arch stood beside the teepee, which hid the couple and the guests. A tent in
        /// front of the usual vow point turns it round the fire to open ground; with nothing in the way the spot is the old one.</summary>
        [Test]
        public void TheWeddingIsHeldInTheOpenAwayFromTents()
        {
            var open = Build(MomentKind.Wedding);
            Assert.AreEqual(Geo.fireX, open.centreX, 1e-4f, "no tents: straight in front of the fire, toward the glass");
            Assert.Less(open.centreZ, Geo.fireZ);
            var geo = Geo;
            geo.blockers = new[] { open.centreX + 0.1f, open.centreZ - 0.2f, 0.25f }; // a teepee right where the ring would be
            var moved = MomentStagePlan.Build(MomentKind.Wedding, Cast(MomentKind.Wedding), geo, 370);
            Assert.IsNotNull(moved, MomentStagePlan.LastWhyNot);
            Assert.Greater(Math.Abs(moved.centreX - open.centreX) + Math.Abs(moved.centreZ - open.centreZ), 0.2f, "turned away from the tent");
            float was = moved.Hidden(open.centreX, open.centreZ, moved.ringRadius), now = moved.Hidden(moved.centreX, moved.centreZ, moved.ringRadius);
            Assert.Greater(was, 0.2f, "the old spot is behind the tent");
            Assert.Less(now, was * 0.05f, "the new one is (nearly) in the open");
            Assert.Greater(Math.Sqrt((moved.centreX - geo.blockers[0]) * (moved.centreX - geo.blockers[0]) + (moved.centreZ - geo.blockers[1]) * (moved.centreZ - geo.blockers[1])),
                moved.ringRadius + 0.12f + geo.blockers[2], $"the tent is outside the ring: centre {moved.centreX},{moved.centreZ} R {moved.ringRadius} open {open.centreX},{open.centreZ} now {now}");
            Assert.Less(moved.centreZ, Geo.fireZ + 0.05f, "still on the glass side of the fire");
        }

        [TestCaseSource(nameof(Kinds))]
        public void ThePlan_IsAPureFunctionOfItsInputs(MomentKind kind)
        {
            var a = Build(kind); var b = Build(kind);
            Assert.AreEqual(a.Duration, b.Duration);
            Assert.AreEqual(a.members.Count, b.members.Count);
            for (int i = 0; i < a.members.Count; i++)
                for (int k = 0; k < a.members[i].steps.Count; k++)
                {
                    var x = a.members[i].steps[k]; var y = b.members[i].steps[k];
                    Assert.AreEqual(x.beat, y.beat); Assert.AreEqual(x.t0, y.t0); Assert.AreEqual(x.x1, y.x1); Assert.AreEqual(x.cue, y.cue);
                }
        }

        [Test]
        public void Wedding_TheKidSpotsTheKeeper_TheCoupleWalksTheAisle_TheRowsCheerAsTheyPass_TheRingDancesRoundThem()
        {
            var plan = Build(MomentKind.Wedding);
            var couple = plan.members.Where(m => m.role == MomentRole.Couple).ToList();
            Assert.AreEqual(2, couple.Count);
            var spotter = plan.members.Single(m => m.role == MomentRole.Spotter);
            Assert.IsTrue(spotter.kid, "a kid spots the Keeper");
            Assert.AreEqual(0f, spotter.JoinAt, "the arrival is the reason: it starts at once");
            CollectionAssert.AreEqual(new[] { StageBeat.LookUp, StageBeat.Point, StageBeat.Run }, spotter.steps.Take(3).Select(s => s.beat).ToArray());
            Assert.IsTrue(spotter.steps[0].faceGlass && spotter.steps[1].faceGlass, "looks and points at the glass");
            float told = spotter.steps[2].t0;
            foreach (var m in plan.members.Where(m => m.role != MomentRole.Spotter))
                Assert.GreaterOrEqual(m.JoinAt, told - 1e-4f, $"{m.role} sets off once the kid runs to tell");
            Assert.IsTrue(plan.members.Any(m => m.role == MomentRole.Officiant && m.steps.Any(s => s.beat == StageBeat.Bless)), "the leader blesses them");
            foreach (var c in couple)
            {
                var beats = c.steps.Select(s => s.beat).ToList();
                Assert.Less(beats.IndexOf(StageBeat.WalkIn), beats.IndexOf(StageBeat.Vow));
                Assert.Less(beats.IndexOf(StageBeat.Vow), beats.IndexOf(StageBeat.Join));
                Assert.Less(beats.IndexOf(StageBeat.Join), beats.IndexOf(StageBeat.Centre));
                Assert.AreEqual(StageBeat.Wave, beats.Last());
                Assert.IsTrue(c.steps.Last().faceGlass, "they wave to the Keeper");
                Assert.IsFalse(plan.ringKeys.Contains(c.key), "the couple is in the middle, not in the ring");
            }
            // The cheer wave: a guest further down the aisle cheers later.
            var cheers = plan.members.Where(m => m.steps.Any(s => s.beat == StageBeat.AisleCheer))
                .Select(m => m.steps.First(s => s.beat == StageBeat.AisleCheer)).ToList();
            Assert.GreaterOrEqual(cheers.Count, 4);
            Assert.Greater(cheers.Select(s => s.cue).Distinct().Count(), 2, "they don't all cheer at once");
            Assert.AreEqual(plan.members.Count - 2, plan.ringKeys.Count, "everyone else dances in the ring");
            Assert.Greater(plan.ringT1 - plan.ringT0, 10f);
            Assert.AreEqual(MomentStagePlan.RingRadiusFor(plan.ringKeys.Count), plan.ringRadius, 1e-5f);
        }

        [Test]
        public void Newborn_TheParentLiftsTheBabyAtTheGlass_TheFamilyCheers_TheKidsPressTheGlass()
        {
            var plan = Build(MomentKind.Newborn);
            var parent = plan.members.Single(m => m.role == MomentRole.Parent);
            Assert.AreEqual(parent.key, plan.carrierKey);
            Assert.AreEqual(99, plan.babyKey);
            var beats = parent.steps.Select(s => s.beat).ToList();
            CollectionAssert.AreEqual(new[] { StageBeat.Travel, StageBeat.Settle, StageBeat.LiftBaby, StageBeat.Cradle }, beats);
            Assert.IsTrue(parent.steps.All(s => s.faceGlass), "facing the Keeper throughout");
            var lift = parent.steps[2];
            float dGlass = Math.Abs(lift.z1 - Geo.glassZ);
            Assert.Less(dGlass, 0.2f, "at the glass");
            Assert.IsTrue(plan.members.Any(m => m.role == MomentRole.Family && m.steps.Any(s => s.beat == StageBeat.Cheer && s.t0 < lift.t1 && s.t1 > lift.t0)), "the family cheers while it's up");
            Assert.IsTrue(plan.members.Any(m => m.role == MomentRole.Kid && m.steps.Any(s => s.kind == ActivityKind.PressHands)), "kids press their hands to the glass");
        }

        [Test]
        public void NewChief_WalksThroughTheGap_TakesTheBand_Speaks_EveryoneBowsNearestFirst_ThenThePop()
        {
            var plan = Build(MomentKind.NewLeader);
            var chief = plan.members.Single(m => m.role == MomentRole.Chief);
            var beats = chief.steps.Select(s => s.beat).ToList();
            CollectionAssert.IsSubsetOf(new[] { StageBeat.WalkThrough, StageBeat.Take, StageBeat.Seat, StageBeat.Speech, StageBeat.GoldPop }, beats);
            Assert.AreEqual(StageBeat.GoldPop, beats.Last());
            var old = plan.members.Single(m => m.role == MomentRole.OldChief);
            var give = old.steps.Single(s => s.beat == StageBeat.Give);
            var take = chief.steps.Single(s => s.beat == StageBeat.Take);
            Assert.AreEqual(give.t0, take.t0, 1e-4f, "the hand-over is one beat");
            Assert.AreEqual(chief.key, give.partner);
            // The bow wave: nearer the seat bows first.
            var seat = chief.steps.Single(s => s.beat == StageBeat.Seat);
            var bows = plan.members.Where(m => m.role != MomentRole.Chief).Select(m => (m, s: m.steps.Single(x => x.beat == StageBeat.Bow))).ToList();
            float D(MomentStep s) => (float)Math.Sqrt((s.x1 - seat.x1) * (s.x1 - seat.x1) + (s.z1 - seat.z1) * (s.z1 - seat.z1));
            var byCue = bows.OrderBy(b => b.s.cue).ToList();
            for (int i = 1; i < byCue.Count; i++) Assert.LessOrEqual(D(byCue[i - 1].s), D(byCue[i].s) + 1e-4f, "nearest first");
            Assert.Greater(byCue.Last().s.cue, byCue.First().s.cue, "a wave, not all at once");
        }

        [Test]
        public void NewChief_WithoutAnOldChief_TheElderBlesses()
        {
            var plan = Build(MomentKind.NewLeader, Cast(MomentKind.NewLeader, b: false));
            Assert.IsFalse(plan.members.Any(m => m.role == MomentRole.OldChief));
            Assert.IsTrue(plan.members.Any(m => m.role == MomentRole.Elder && m.steps.Any(s => s.beat == StageBeat.Bless)));
        }

        [Test]
        public void Festival_EveryoneBringsSomething_TheLeaderFeedsTheFire_Speaks_TheRingDances_TheyEatAndJumpTogether()
        {
            var plan = Build(MomentKind.Festival);
            var lead = plan.members.Single(m => m.role == MomentRole.Leader);
            Assert.IsTrue(lead.steps[0].carries && lead.steps[0].prop == Prop.Log, "the leader brings a log");
            var toss = lead.steps.Single(s => s.beat == StageBeat.Toss);
            Assert.AreEqual(-MomentStagePlan.TossWindSeconds, toss.cue, 1e-5f, "the throw's second half carries on from the wind-up");
            Assert.IsTrue(lead.steps.Any(s => s.beat == StageBeat.Speech));
            foreach (var m in plan.members.Where(m => m.role != MomentRole.Leader))
            {
                Assert.IsTrue(m.steps[0].carries && m.steps[0].prop != Prop.None, $"{m.role} brings something");
                Assert.IsTrue(m.steps.Any(s => s.beat == StageBeat.SetDown));
            }
            Assert.AreEqual(plan.members.Count, plan.ringKeys.Count, "the whole camp dances");
            var jumps = plan.members.Select(m => m.steps.Last()).ToList();
            Assert.IsTrue(jumps.All(s => s.beat == StageBeat.Jump));
            Assert.AreEqual(1, jumps.Select(s => s.t0 + s.cue).Distinct().Count(), "they jump at once (in step on purpose)");
        }

        [Test]
        public void APrincipalTooFarAway_CantBeStaged_AndFarGuestsArentCast()
        {
            var c = Cast(MomentKind.Wedding);
            c.b = MomentPerson.Of(2, false, 2.9f, 2.1f, 30f);
            Assert.IsNull(MomentStagePlan.Build(MomentKind.Wedding, c, Geo, 1));
            StringAssert.Contains("in time", MomentStagePlan.LastWhyNot);
            var c2 = Cast(MomentKind.Wedding);
            c2.grown.Add(MomentPerson.Of(77, false, 2.9f, 2.1f, 40f));
            var plan = MomentStagePlan.Build(MomentKind.Wedding, c2, Geo, 1);
            Assert.IsNotNull(plan);
            Assert.IsNull(plan.Member(77), "too far to come in time");
            Assert.IsNull(MomentStagePlan.Build(MomentKind.Callback, c2, Geo, 1), "a callback isn't staged here");
        }

        // ---------------------------------------------------------------- the planner

        static SimCore Box(int seed = 7)
        {
            var cfg = new SimConfig { moments = true };
            var sim = new SimCore(cfg, SimCore.NewWorld(seed, cfg));
            for (int m = 0; m < 24; m++) sim.Step();
            return sim;
        }

        static IEnumerable<int> Seeds => new[] { 5, 7, 23 };

        [TestCaseSource(nameof(Seeds))]
        public void TheWedding_IsPlayedByRealPeople_AsALiveScene_AndEndsWithEveryoneBackInTheirDay(int seed)
        {
            var sim = Box(seed);
            var m = sim.DebugQueueMoment(MomentKind.Wedding);
            Assume.That(m, Is.Not.Null, "a box with a couple");
            var planner = LifePlanner.Wired(sim);
            var at = new ViewTime(sim.State.month, 9.5f);
            var sc = planner.StageHeldMoment(m, at);
            Assert.IsNotNull(sc, planner.LastHeldWhyNot);
            Assert.Greater(sc.CastCount, 2);
            Assert.IsNotNull(sc.plan.Member(m.personA)); Assert.IsNotNull(sc.plan.Member(m.personB));
            float mid = sc.plan.ringT0 + 2f;
            var tm = LifeClock.Advance(at, mid);
            foreach (var mem in sc.plan.members)
            {
                var who = mem.key > 0 ? PersonRef.Notable(mem.key) : PersonRef.Crowd(-mem.key - 1);
                Assert.IsTrue(planner.HeldMomentAt(who, tm, out var got, out var gm, out int idx, out float secs), $"{mem.role} is in it mid-way");
                Assert.AreSame(sc, got);
                Assert.AreEqual(mem.steps[idx].kind, planner.Plan(who, tm).kind, $"{mem.role}: the planner plays the plan's step ({mem.steps[idx].beat})");
            }
            // Over: nobody is held, everyone is back in their own day.
            var after = LifeClock.Advance(at, sc.plan.Duration + 0.5f);
            Assert.IsFalse(planner.HeldMomentPlaying(sc, after));
            foreach (var mem in sc.plan.members)
                Assert.IsFalse(planner.HeldMomentAt(mem.key > 0 ? PersonRef.Notable(mem.key) : PersonRef.Crowd(-mem.key - 1), after, out _, out _, out _, out _));
        }

        [Test]
        public void Stop_ReleasesEveryoneAtOnce()
        {
            var sim = Box(7);
            var m = sim.DebugQueueMoment(MomentKind.Festival);
            Assert.IsNotNull(m);
            var planner = LifePlanner.Wired(sim);
            var at = new ViewTime(sim.State.month, 10f);
            var sc = planner.StageHeldMoment(m, at);
            Assert.IsNotNull(sc, planner.LastHeldWhyNot);
            var tm = LifeClock.Advance(at, sc.plan.ringT0 + 1f);
            var who = sc.plan.members.Select(x => x.key > 0 ? PersonRef.Notable(x.key) : PersonRef.Crowd(-x.key - 1)).ToList();
            Assert.IsTrue(who.All(w => planner.HeldMomentAt(w, tm, out _, out _, out _, out _)));
            Assert.IsTrue(who.All(w => planner.Plan(w, tm).kind == ActivityKind.Dance), "the ring dances");
            planner.EndHeldMoment(sc);
            Assert.IsEmpty(planner.HeldScenes);
            Assert.IsFalse(planner.HeldMomentPlaying(sc, tm));
            Assert.IsTrue(who.All(w => !planner.HeldMomentAt(w, tm, out _, out _, out _, out _)), "nobody is held");
            Assert.IsTrue(who.All(w => planner.Plan(w, tm).source != ActivitySource.Scene || planner.Plan(w, tm).kind != ActivityKind.Dance), "back in their day");
        }

        [Test]
        public void TheNewborn_RidesInTheParentsArms_ForTheWholeMoment()
        {
            var sim = Box(7);
            var m = sim.DebugQueueMoment(MomentKind.Newborn);
            Assert.IsNotNull(m, "a baby is born for it if need be");
            var planner = LifePlanner.Wired(sim);
            var at = new ViewTime(sim.State.month, 10f);
            var sc = planner.StageHeldMoment(m, at);
            Assert.IsNotNull(sc, planner.LastHeldWhyNot);
            var lift = sc.plan.Member(sc.plan.carrierKey).steps.First(s => s.beat == StageBeat.LiftBaby);
            var tm = LifeClock.Advance(at, (lift.t0 + lift.t1) * 0.5f);
            var parent = planner.Plan(PersonRef.Notable(sc.plan.carrierKey), tm);
            Assert.AreEqual(PersonRef.Notable(m.personA), parent.carrying, "the baby in the arms");
            Assert.IsFalse(parent.carryOnBack);
            Assert.AreEqual(PersonRef.Notable(sc.plan.carrierKey), planner.Plan(PersonRef.Notable(m.personA), tm).carriedBy);
        }

        [Test]
        public void AQueuedWeddingLine_DoesNotAlsoPlayAsThisMonthsScene()
        {
            int Scenes(bool momentsOn)
            {
                var cfg = new SimConfig { moments = momentsOn };
                var sim = new SimCore(cfg, SimCore.NewWorld(7, cfg));
                for (int i = 0; i < 24; i++) sim.Step();
                var adults = sim.State.notables.Where(n => n.alive && n.age >= SimCore.MarryAge).OrderBy(n => n.id).Take(2).ToList();
                Assume.That(adults.Count, Is.EqualTo(2));
                // A wedding line this month, as the sim writes one (through the chronicle, so the queue sees it when on).
                sim.DebugChronicle("wedding", $"{adults[0].FullName} and {adults[1].FullName} promised each other at the fire.", adults[0].id, adults[1].id);
                var e = sim.State.chronicle.Last();
                Assert.AreEqual(momentsOn, sim.MomentOfEntry(e.id) != null, "queued only with the moments on");
                var planner = LifePlanner.Wired(sim);
                var list = new List<PersonActivity>();
                planner.DaySchedule(PersonRef.Notable(adults[0].id), sim.State.month, list);
                return list.Count(a => a.chronicleId == e.id);
            }
            Assert.Greater(Scenes(false), 0, "without the queue, the wedding plays as the month's scene");
            Assert.AreEqual(0, Scenes(true), "held for the Keeper: it plays once, staged, not also as the month's scene");
        }

        [Test]
        public void TheWeddingSaysSoWithHeartsOverTheCoupleAndTheNewbornOverTheParent()
        {
            // t1-moments (tester run 1: nothing said "wedding" in three seconds): a heart over each of the couple from the walk to the ring.
            var wed = new HeldMomentScene { kind = MomentKind.Wedding, plan = Build(MomentKind.Wedding), day = 0, start = 9f, end = 10f };
            var couple = wed.plan.members.Where(m => m.role == MomentRole.Couple).Select(m => PersonRef.Notable(m.key)).ToList();
            var hearts = RitualBubbles.ForHeldMoment(wed).Where(b => b.symbol == RitualSymbol.Heart).ToList();
            Assert.AreEqual(2, couple.Count);
            foreach (var who in couple) Assert.IsTrue(hearts.Any(b => b.who.Equals(who)), "a heart over each of the couple");
            Assert.IsFalse(hearts.Any(b => !couple.Contains(b.who)), "only the couple");

            var born = new HeldMomentScene { kind = MomentKind.Newborn, plan = Build(MomentKind.Newborn), day = 0, start = 9f, end = 10f };
            var parent = PersonRef.Notable(born.plan.carrierKey);
            Assert.IsTrue(RitualBubbles.ForHeldMoment(born).Any(b => b.symbol == RitualSymbol.Heart && b.who.Equals(parent)), "the parent lifting the baby shows a heart");
        }
    }
}
