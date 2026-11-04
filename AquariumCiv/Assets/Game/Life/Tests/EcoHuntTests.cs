using System.Collections.Generic;
using System.Linq;
using AquariumCiv.Sim;
using AquariumCiv.Sim.Eco;
using NUnit.Framework;
using UnityEngine;

namespace AquariumCiv.Life.Tests
{
    /// <summary>P8 polish #283 phase 1: the hunt as planner data. The hunter job opens with the first rabbit, a hunt targets a
    /// real rabbit of the herd, outcomes are seeded and some miss, a catch lowers the count once and leaves a hide, nothing is
    /// hunted without rabbits or below the floor, hunts keep to the morning and dusk, the village fades them, the cheats, and the
    /// ecology off plays the old hunt.</summary>
    public class EcoHuntTests
    {
        static readonly (float dx, float dz)[] Spots = { (0.9f, 0f), (-1.2f, 0.8f), (1.6f, -0.9f), (-0.4f, -1.3f), (2.2f, 0.6f), (0.2f, 1.4f), (-2f, -0.3f), (1f, 1.2f) };

        class Setup
        {
            public SimCore sim;
            public RabbitHerd.Field field;
            public LifePlanner planner;
        }

        /// <summary>A green meadow with a herd, every grown-up (but the toolmaker) a hunter, and the planner looking at the herd.</summary>
        static Setup Herd(int seed = 7, int rabbits = 8, bool hunters = true)
        {
            var cfg = new SimConfig { ecology = true };
            var sim = new SimCore(cfg, SimCore.NewWorld(seed, cfg));
            for (int i = 0; i < 12; i++) { var s = Spots[i % Spots.Length]; sim.DebugMistBig(sim.EcoGround.campX + s.dx, sim.EcoGround.campZ + s.dz); sim.Advance(1.0 / 12); }
            sim.DebugAddRabbits(rabbits);
            if (hunters)
                foreach (var n in sim.State.notables.Where(x => x.alive && x.age >= 16f && x.age < sim.Config.retireAge && x.job != JobType.Toolmaker))
                    sim.DebugSetJob(n, JobType.Hunter);
            var field = new RabbitHerd.Field();
            sim.EcoRabbitsRefresh(field);
            field.Sync(sim.EcoRabbitsShown());
            return new Setup { sim = sim, field = field, planner = new LifePlanner(sim) { Rabbits = field } };
        }

        static IEnumerable<long> Days(Setup s, int n) { for (int i = 0; i < n; i++) yield return s.sim.State.month + i; }

        static List<HuntPlan> AllPlans(Setup s, int days)
        {
            var all = new List<HuntPlan>();
            foreach (long d in Days(s, days)) all.AddRange(s.planner.HuntsOn(d));
            return all;
        }

        [Test]
        public void TheHunterJobOpensWithTheFirstRabbit()
        {
            var cfg = new SimConfig { ecology = true };
            var sim = new SimCore(cfg, SimCore.NewWorld(3, cfg));
            Assert.IsFalse(sim.JobOpen(JobType.Hunter), "no rabbit, no hunt");
            sim.DebugAddRabbits(3);
            Assert.IsTrue(sim.RabbitsSeen);
            Assert.IsTrue(sim.JobOpen(JobType.Hunter), "the first rabbits open it, not a forest");
            StringAssert.Contains("hunter: open", sim.DebugJobsReadout());
        }

        [Test]
        public void WithTheEcologyOffTheOldForestRuleHolds()
        {
            var cfg = new SimConfig();
            var sim = new SimCore(cfg, SimCore.NewWorld(3, cfg));
            Assert.IsTrue(sim.JobOpen(JobType.Hunter), "always there without the living land");
            Assert.IsTrue(sim.RabbitsSeen);
            var planner = LifePlanner.Wired(sim);
            var field = new RabbitHerd.Field();
            planner.Rabbits = field;
            for (int d = 0; d < 30; d++) Assert.IsEmpty(planner.HuntsOn(d), "no planned hunt: the old hunt plays");
            var day = new List<PersonActivity>();
            foreach (var n in sim.State.notables.Where(x => x.alive && x.age >= 16f && x.age < sim.Config.retireAge && x.job != JobType.Toolmaker)) sim.DebugSetJob(n, JobType.Hunter);
            planner.Invalidate();
            bool stalked = false;
            foreach (var who in planner.People) { planner.DaySchedule(who, 1, day); stalked |= day.Any(a => a.kind == ActivityKind.Stalk && a.place.kind == PlaceKind.ForestEdge); }
            Assert.IsTrue(stalked, "hunters still stalk at the forest edge");
            Assert.IsFalse(day.Any(a => a.hunt != HuntPhase.None));
        }

        [Test]
        public void AHuntTargetsARealRabbitAndWalksToIt()
        {
            var s = Herd();
            var plans = AllPlans(s, 24);
            Assert.IsNotEmpty(plans, "hunters hunt");
            foreach (var p in plans)
            {
                Assert.IsTrue(s.field.Alive.Contains(p.rabbitId), "a rabbit of the herd");
                double t = HuntClock.FieldSeconds(p.day, p.strikeStart);
                Assert.IsTrue(s.field.PositionOf(p.rabbitId, t, out float rx, out float rz));
                Assert.Less(System.Math.Abs(rx - p.targetX) + System.Math.Abs(rz - p.targetZ), 0.15f, "the spear flies at where the rabbit is");
                Assert.Less(Dist(p.stalkX, p.stalkZ, p.targetX, p.targetZ), LifePlanner.StalkRange + 1e-3f, "stalks from close");
                Assert.IsTrue(s.field.IsGreenAt(p.targetX, p.targetZ), "the rabbit is on the green");
                Assert.AreEqual(HuntPhase.ToFire, p.steps[0].phase);
                Assert.AreEqual(HuntPhase.TakeSpear, p.steps[1].phase);
                Assert.AreEqual(Prop.Spear, p.steps[1].prop, "takes a spear at the fire");
                Assert.AreEqual(HuntPhase.WalkOut, p.steps[2].phase);
                Assert.AreEqual(HuntPhase.Stalk, p.steps[3].phase);
                Assert.AreEqual(HuntPhase.Strike, p.steps[4].phase);
                for (int i = 1; i < p.steps.Count; i++) Assert.AreEqual(p.steps[i - 1].end, p.steps[i].start, 1e-4f, "beats run one into the next");
                Assert.AreEqual(p.steps[p.steps.Count - 1].end, p.end, 1e-4f);
            }
            Assert.AreEqual(plans.Count, plans.Select(p => (p.day, p.rabbitId)).Distinct().Count(), "nobody hunts the same rabbit twice in a day");
        }

        [Test]
        public void TheBeatsReachThePersonThroughThePlanner()
        {
            var s = Herd();
            var p = AllPlans(s, 24).First(x => x.catches);
            p = s.planner.HuntsOn(p.day).First(x => x.catches && x.hunter == p.hunter && x.start == p.start); // the day as the planner keeps it
            var who = p.hunter;
            PersonActivity At(float h) => s.planner.Plan(who, new ViewTime(p.day, h));
            var take = p.steps[1]; var stalk = p.steps[3]; var carry = p.steps.First(x => x.phase == HuntPhase.CarryBack);
            var a = At((take.start + take.end) / 2);
            Assert.AreEqual((HuntPhase.TakeSpear, Prop.Spear), (a.hunt, a.prop));
            a = At((stalk.start + stalk.end) / 2);
            Assert.AreEqual((ActivityKind.Stalk, HuntPhase.Stalk, Prop.Spear), (a.kind, a.hunt, a.prop));
            a = At((carry.start + carry.end) / 2);
            Assert.AreEqual((ActivityKind.Carry, HuntPhase.CarryBack, Prop.Rabbit), (a.kind, a.hunt, a.prop), "a rabbit over the shoulder");
            Assert.AreEqual(PropSlot.Shoulder, a.PropSlot);
            Assert.IsTrue(a.moving);
            var skin = p.steps.First(x => x.phase == HuntPhase.Skin); var cook = p.steps.First(x => x.phase == HuntPhase.Cook);
            Assert.AreEqual(HuntPhase.Skin, At((skin.start + skin.end) / 2).hunt);
            Assert.AreEqual((ActivityKind.Cook, HuntPhase.Cook), (At((cook.start + cook.end) / 2).kind, At((cook.start + cook.end) / 2).hunt));
            Assert.IsTrue(s.planner.HuntStepAt(who, new ViewTime(p.day, (stalk.start + stalk.end) / 2), out var step, out var plan));
            Assert.AreSame(p, plan);
            Assert.AreEqual(HuntPhase.Stalk, step.phase);
            Assert.AreEqual(HuntPhase.None, At(p.end + 0.05f).hunt, "after the hunt the day goes on");
        }

        [Test]
        public void OutcomesAreSeededAndSomeMiss()
        {
            var a = Herd();
            var b = Herd();
            var pa = AllPlans(a, 40); var pb = AllPlans(b, 40);
            Assert.AreEqual(pa.Count, pb.Count);
            for (int i = 0; i < pa.Count; i++)
                Assert.AreEqual((pa[i].day, pa[i].hunter, pa[i].rabbitId, pa[i].catches, pa[i].start), (pb[i].day, pb[i].hunter, pb[i].rabbitId, pb[i].catches, pb[i].start), "same seed, same hunt");
            Assert.Greater(pa.Count(p => p.catches), 0, "some catch");
            Assert.Greater(pa.Count(p => !p.catches), 0, "some miss");
            Assert.AreEqual(HuntPhase.Bolt, pa.First(p => !p.catches).steps[5].phase, "a miss: the rabbit bolts");
            // asking again changes nothing
            Assert.AreSame(a.planner.HuntsOn(a.sim.State.month), a.planner.HuntsOn(a.sim.State.month));
        }

        [Test]
        public void ACatchLowersTheCountOnceAndLeavesAHide()
        {
            var s = Herd();
            long day = s.sim.State.month;
            HuntPlan target = null;
            for (long d = day; d < day + 30 && target == null; d++) target = s.planner.HuntsOn(d).FirstOrDefault(p => p.catches);
            Assert.IsNotNull(target);
            int c0 = s.sim.EcoRabbitCount, h0 = s.sim.HidesDrying, alive0 = s.field.Alive.Count, caught0 = s.sim.EcoRabbitsCaught;
            var settled = new List<HuntPlan>();
            s.planner.ResolveHunts(new ViewTime(target.day, target.resolveHour - 0.5f), settled);
            Assert.IsFalse(settled.Contains(target), "nothing is settled before its strike");
            var at = new ViewTime(target.day, target.resolveHour + 0.001f);
            s.planner.ResolveHunts(at, settled);
            Assert.Contains(target, settled);
            int took = settled.Count(p => p.caught);
            Assert.GreaterOrEqual(took, 1);
            Assert.AreEqual(c0 - took, s.sim.EcoRabbitCount, "the sim count follows the take");
            Assert.AreEqual(h0 + took, s.sim.HidesDrying, "a hide per catch");
            Assert.AreEqual(caught0 + took, s.sim.EcoRabbitsCaught);
            Assert.IsFalse(s.field.Alive.Contains(target.rabbitId), "the rabbit left the box");
            Assert.AreEqual(alive0 - took, s.field.Alive.Count);
            settled.Clear();
            Assert.AreEqual(0, s.planner.ResolveHunts(at, settled), "settled once");
            Assert.AreEqual(0, s.planner.ResolveHunts(new ViewTime(target.day, target.resolveHour + 0.002f), settled));
            Assert.AreEqual(c0 - took, s.sim.EcoRabbitCount);
        }

        [Test]
        public void AMissLeavesTheHerdAlone()
        {
            var s = Herd();
            HuntPlan miss = null;
            for (long d = s.sim.State.month; d < s.sim.State.month + 40 && miss == null; d++) miss = s.planner.HuntsOn(d).FirstOrDefault(p => !p.catches);
            Assert.IsNotNull(miss);
            int c0 = s.sim.EcoRabbitCount, h0 = s.sim.HidesDrying;
            var done = new List<HuntPlan>();
            s.planner.ResolveHunts(new ViewTime(miss.day, miss.resolveHour + 0.001f), done);
            Assert.Contains(miss, done);
            Assert.IsFalse(miss.caught);
            Assert.IsTrue(s.field.Alive.Contains(miss.rabbitId), "the rabbit bolted, it is still there");
            Assert.AreEqual(c0 - done.Count(p => p.caught), s.sim.EcoRabbitCount);
            Assert.AreEqual(h0 + done.Count(p => p.caught), s.sim.HidesDrying);
        }

        [Test]
        public void NoHuntWithoutRabbitsOrBelowTheFloor()
        {
            var none = Herd(rabbits: 0);
            none.field.Sync(0);
            Assert.IsEmpty(AllPlans(none, 30), "no rabbit shown, no hunt");

            var low = Herd();
            low.sim.State.eco.rabbits = SimCore.HuntFloor;
            Assert.AreEqual(0, low.sim.HuntSurplus);
            Assert.IsEmpty(AllPlans(low, 30), "at the floor nothing is hunted");

            var one = Herd(seed: 9);
            one.sim.State.eco.rabbits = SimCore.HuntFloor + 1;
            var plans = AllPlans(one, 12);
            foreach (long d in Days(one, 12))
                Assert.LessOrEqual(one.planner.HuntsOn(d).Count(p => p.catches), 1, "one over the floor, at most one catch a day");
            // play every day out: the count never goes under the floor, however many hunts
            for (long d = one.sim.State.month; d < one.sim.State.month + 12; d++)
            {
                one.planner.ResolveHunts(new ViewTime(d, 23.9f));
                Assert.GreaterOrEqual(one.sim.EcoRabbitCount, SimCore.HuntFloor);
            }
        }

        [Test]
        public void ACatchPastTheFloorIsRefusedAtTheSettling()
        {
            var s = Herd();
            long day = s.sim.State.month;
            HuntPlan target = null;
            for (long d = day; d < day + 30 && target == null; d++) target = s.planner.HuntsOn(d).FirstOrDefault(p => p.catches);
            Assert.IsNotNull(target);
            s.sim.State.eco.rabbits = SimCore.HuntFloor; // the herd dropped while they were out
            int h0 = s.sim.HidesDrying;
            s.planner.ResolveHunts(new ViewTime(target.day, target.resolveHour + 0.001f));
            Assert.IsFalse(target.caught);
            Assert.AreEqual(SimCore.HuntFloor, s.sim.EcoRabbitCount);
            Assert.AreEqual(h0, s.sim.HidesDrying);
        }

        [Test]
        public void HuntsKeepToTheMorningAndDusk()
        {
            // the rule itself
            int morning = 0, dusk = 0, other = 0, n = 0;
            foreach (var season in new[] { Season.Spring, Season.Summer, Season.Autumn, Season.Winter })
                for (int i = 0; i < 40; i++)
                    for (int j = 0; j < 40; j++)
                    {
                        float h = LifePlanner.HuntStartHour(i / 40f, j / 40f, season);
                        float end = LifeClock.AfternoonEnd(season);
                        Assert.GreaterOrEqual(h, 7f); Assert.Less(h, end);
                        if (h < 9.5f) morning++; else if (h >= end - 1.9f) dusk++; else other++;
                        n++;
                    }
            Assert.Greater((morning + dusk) / (float)n, 0.8f, "morning and dusk are the hunters' hours");
            Assert.Greater(morning, other); Assert.Greater(dusk, other);
            // and the real plans
            var s = Herd();
            var plans = AllPlans(s, 60);
            Assert.Greater(plans.Count, 20);
            int favoured = plans.Count(p => p.start < 9.7f || p.start >= LifeClock.AfternoonEnd(LifeClock.SeasonOf(p.day)) - 2.2f);
            Assert.Greater(favoured / (float)plans.Count, 0.65f, $"{favoured} of {plans.Count} at morning or dusk");
            foreach (var p in plans)
                Assert.LessOrEqual(p.end, LifeClock.AfternoonEnd(LifeClock.SeasonOf(p.day)) + 0.55f, "home before the evening gathering");
        }

        [Test]
        public void TheVillageFadesTheHunt()
        {
            var s = Herd();
            long day = s.sim.State.month;
            Assert.IsNotEmpty(AllPlans(s, 20), "the camp hunts");
            var v = Herd();
            v.sim.State.era = Era.Village;
            Assert.IsFalse(v.sim.JobOpen(JobType.Hunter), "the village has no hunter's job");
            v.planner.Invalidate();
            Assert.IsEmpty(AllPlans(v, 20), "and no hunt");
            v.sim.Step();
            Assert.IsFalse(v.sim.State.notables.Any(n => n.alive && n.age >= 16f && n.age < v.sim.Config.retireAge && n.job == JobType.Hunter), "the hunters moved on to open jobs");
        }

        [Test]
        public void KidsFollowSometimes()
        {
            var s = Herd();
            var plans = AllPlans(s, 80);
            var withKid = plans.Where(p => p.HasKid).ToList();
            Assert.IsNotEmpty(withKid, "now and then a kid follows");
            Assert.Less(withKid.Count, plans.Count, "not every time");
            var p0 = withKid[0];
            for (int i = 1; i < p0.kidSteps.Count; i++) Assert.AreEqual(p0.kidSteps[i - 1].end, p0.kidSteps[i].start, 1e-4f);
            Assert.AreEqual(HuntPhase.KidFollow, p0.kidSteps[0].phase);
            var watch = p0.kidSteps.First(x => x.phase == HuntPhase.KidWatch);
            var act = s.planner.Plan(p0.kid, new ViewTime(p0.day, watch.start + 0.01f));
            Assert.AreEqual((HuntPhase.KidWatch, ActivityKind.Watch), (act.hunt, act.kind));
            Assert.AreEqual(p0.catches, p0.kidSteps.Any(x => x.phase == HuntPhase.KidCheer), "the kid cheers a catch");
            Assert.AreNotEqual(p0.hunter, p0.kid);
        }

        [Test]
        public void TheCheatsStartAHuntAndForceItsEnd()
        {
            var s = Herd();
            long day = s.sim.State.month;
            var now = new ViewTime(day, 10f);
            Assert.IsTrue(s.planner.DebugForceHunt(true), "ok at once: held for the next hunt made");
            Assert.IsTrue(s.planner.DebugStartHunt(now, out var plan));
            Assert.IsTrue(plan.debugStarted);
            Assert.IsTrue(plan.catches && plan.forced, "the held word applied");
            Assert.AreEqual(now.hour + 0.02f, plan.start, 1e-4f);
            Assert.IsTrue(s.planner.HuntsOn(day).Contains(plan));
            Assert.IsTrue(plan.steps.Any(x => x.phase == HuntPhase.CarryBack));

            // flip a hunt that has not struck yet
            s.planner.Plan(plan.hunter, new ViewTime(day, plan.strikeStart - 0.01f)); // the planner learns the time: this hunt is the next to strike
            Assert.IsTrue(s.planner.DebugForceHunt(false));
            Assert.IsFalse(plan.catches);
            Assert.IsTrue(plan.steps.Any(x => x.phase == HuntPhase.Bolt) && !plan.steps.Any(x => x.phase == HuntPhase.CarryBack));
            Assert.IsTrue(s.planner.DebugForceHunt(true));
            Assert.IsTrue(plan.catches);
            for (int i = 1; i < plan.steps.Count; i++) Assert.AreEqual(plan.steps[i - 1].end, plan.steps[i].start, 1e-4f);

            // it settles once
            int c0 = s.sim.EcoRabbitCount;
            var done = new List<HuntPlan>();
            s.planner.ResolveHunts(new ViewTime(day, plan.resolveHour + 0.001f), done);
            Assert.IsTrue(plan.resolved && plan.caught);
            Assert.AreEqual(c0 - done.Count(p => p.caught), s.sim.EcoRabbitCount, "lowered by each catch settled, this one included");
        }

        [Test]
        public void ForcingACatchAtTheFloorStaysAMiss()
        {
            var s = Herd();
            s.sim.State.eco.rabbits = SimCore.HuntFloor;
            Assert.IsFalse(s.planner.DebugForceHunt(true), "refused: the herd is at its floor");
            Assert.IsTrue(s.planner.DebugStartHunt(new ViewTime(s.sim.State.month, 10f), out var plan));
            Assert.IsFalse(plan.catches);
            Assert.IsTrue(s.planner.DebugForceHunt(false));
        }

        [Test]
        public void HidesAreSavedAndTentsCanTakeThem()
        {
            var s = Herd();
            Assert.AreEqual(3, s.sim.DebugAddHides(3));
            Assert.IsTrue(s.sim.HuntCatch());
            Assert.AreEqual(4, s.sim.HidesDrying);
            var copy = new SimCore(s.sim.Config, JsonUtility.FromJson<SimState>(JsonUtility.ToJson(s.sim.State)));
            Assert.AreEqual(4, copy.HidesDrying);
            Assert.AreEqual(4, copy.HidesTotal);
            Assert.AreEqual(2, copy.TakeHides(2));
            Assert.AreEqual(2, copy.HidesDrying);
            Assert.AreEqual(2, copy.TakeHides(9));
            Assert.AreEqual(0, copy.HidesDrying);
        }

        [Test]
        public void ASettledHuntIsNotSettledAgainAfterAReload()
        {
            var s = Herd();
            long day = s.sim.State.month;
            var plans = new List<HuntPlan>();
            for (long d = day; d < day + 30 && plans.Count == 0; d++) plans.AddRange(s.planner.HuntsOn(d).Where(p => p.catches));
            var first = plans[0];
            s.planner.ResolveHunts(new ViewTime(first.day, 23.9f));
            int c1 = s.sim.EcoRabbitCount;
            var copy = new SimCore(s.sim.Config, JsonUtility.FromJson<SimState>(JsonUtility.ToJson(s.sim.State)));
            var field = new RabbitHerd.Field();
            copy.EcoRabbitsRefresh(field); field.Sync(copy.EcoRabbitsShown());
            var planner = new LifePlanner(copy) { Rabbits = field };
            planner.ResolveHunts(new ViewTime(first.day, 23.9f));
            planner.HuntsOn(first.day);
            planner.ResolveHunts(new ViewTime(first.day, 23.9f));
            Assert.AreEqual(c1, copy.EcoRabbitCount, "the day's hunts were settled before the save");
        }

        static float Dist(float ax, float az, float bx, float bz) => (float)System.Math.Sqrt((ax - bx) * (ax - bx) + (az - bz) * (az - bz));
    }
}
