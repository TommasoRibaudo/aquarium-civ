using System;
using System.Collections.Generic;
using System.Linq;
using AquariumCiv.Sim;
using AquariumCiv.Sim.Eco;
using NUnit.Framework;

namespace AquariumCiv.Life.Tests
{
    /// <summary>#362 B6: the hunt made watchable. The stalk's rhythm (seven creeping steps, the freezes the rabbit sits up on), the throw
    /// geometry (throw point 0.4 u from the rabbit, landing on it for a catch and past or beside it for a miss), the beats' lengths in real
    /// seconds, the spear's aftermath steps, and the rabbit standing still for the throw.</summary>
    public class HuntThrowTests
    {
        static readonly (float dx, float dz)[] Spots = { (0.9f, 0f), (-1.2f, 0.8f), (1.6f, -0.9f), (-0.4f, -1.3f), (2.2f, 0.6f), (0.2f, 1.4f), (-2f, -0.3f), (1f, 1.2f) };

        static (SimCore sim, RabbitHerd.Field field, LifePlanner planner) Herd(int seed = 7, int rabbits = 8)
        {
            var cfg = new SimConfig { ecology = true };
            var sim = new SimCore(cfg, SimCore.NewWorld(seed, cfg));
            for (int i = 0; i < 12; i++) { var s = Spots[i % Spots.Length]; sim.DebugMistBig(sim.EcoGround.campX + s.dx, sim.EcoGround.campZ + s.dz); sim.Advance(1.0 / 12); }
            sim.DebugAddRabbits(rabbits);
            foreach (var n in sim.State.notables.Where(x => x.alive && x.age >= 16f && x.age < sim.Config.retireAge && x.job != JobType.Toolmaker))
                sim.DebugSetJob(n, JobType.Hunter);
            var field = new RabbitHerd.Field();
            sim.EcoRabbitsRefresh(field);
            field.Sync(sim.EcoRabbitsShown());
            return (sim, field, new LifePlanner(sim) { Rabbits = field });
        }

        static List<HuntPlan> Plans((SimCore sim, RabbitHerd.Field field, LifePlanner planner) s, int days = 40)
        {
            var all = new List<HuntPlan>();
            for (int i = 0; i < days; i++) all.AddRange(s.planner.HuntsOn(s.sim.State.month + i));
            return all;
        }

        [Test]
        public void Stalk_IsSevenCreepingSteps_AndFreezesWhenTheRabbitSitsUp()
        {
            const float a = 1.1f, b = 0.9f;
            float total = HuntStalk.Duration(a, b);
            Assert.AreEqual(7 * 1.2f + 2.0f, total, 1e-4f);
            Assert.AreEqual(0f, HuntStalk.Sample(0f, a, b).fraction, 1e-5f);
            Assert.AreEqual(1f, HuntStalk.Sample(total, a, b).fraction, 1e-5f);
            Assert.AreEqual(1f, HuntStalk.Sample(total + 5f, a, b).fraction, 1e-5f, "past the end it waits at the throw point");
            float prev = 0f; int stepsSeen = 0, freezeFrames = 0;
            for (float s = 0f; s <= total; s += 0.05f)
            {
                var n = HuntStalk.Sample(s, a, b);
                Assert.GreaterOrEqual(n.fraction, prev - 1e-5f, "never backs up");
                if (n.move == HuntStalk.Move.Freeze) { freezeFrames++; Assert.AreEqual(prev, n.fraction, 1e-5f, "frozen: not a step"); }
                prev = n.fraction;
                if (n.move == HuntStalk.Move.Step) stepsSeen = Math.Max(stepsSeen, n.step + 1);
            }
            Assert.AreEqual(7, stepsSeen);
            Assert.AreEqual((a + b) / 0.05f, freezeFrames, 4f, "the two freezes, as long as planned");
            Assert.IsTrue(HuntStalk.RabbitUp(3.6f + 0.5f, a, b), "the rabbit sits up in the first freeze (after the third step's still)");
            Assert.IsFalse(HuntStalk.RabbitUp(0.2f, a, b));
        }

        [Test]
        public void Throw_TimingsAddUp()
        {
            Assert.AreEqual(HuntTiming.RiseSeconds + HuntTiming.AimSeconds + 0.05f, HuntTiming.ReleaseAt, 1e-5f);
            Assert.AreEqual(HuntTiming.ReleaseAt + HuntTiming.FlightSeconds, HuntTiming.LandAt, 1e-5f);
            Assert.GreaterOrEqual(HuntTiming.AimSeconds, 0.3f, "the aim is held (P8)");
            Assert.AreEqual(0.45f, HuntTiming.FlightSeconds, 1e-5f, "slower than real, so the eye can follow");
            Assert.Greater(HuntTiming.StrikeSeconds, HuntTiming.LandAt + 0.1f, "a hold follows the landing");
        }

        [Test]
        public void EveryHunt_ThrowsFromThePointAndLandsWhereTheCatchOrMissSays()
        {
            var s = Herd();
            var plans = Plans(s);
            Assert.GreaterOrEqual(plans.Count, 6, "enough hunts to look at");
            int catches = 0, misses = 0;
            foreach (var p in plans)
            {
                float stalkLen = Dist(p.stalkX, p.stalkZ, p.targetX, p.targetZ), throwLen = Dist(p.lungeX, p.lungeZ, p.targetX, p.targetZ);
                Assert.LessOrEqual(stalkLen, HuntTiming.StalkRange + 1e-3f);
                Assert.Less(throwLen, stalkLen, "the throw point is nearer the rabbit than the stalk's start");
                if (stalkLen > HuntTiming.StalkRange - 0.01f) Assert.AreEqual(HuntTiming.ThrowRange, throwLen, 0.02f, "0.4 u from the rabbit, not the old 0.125 lunge");
                float land = Dist(p.landX, p.landZ, p.targetX, p.targetZ);
                if (p.catches) { catches++; Assert.AreEqual(0f, land, 1e-4f, "a catch: the spear lands in the rabbit"); }
                else { misses++; Assert.GreaterOrEqual(land, 0.1f, "a miss lands clear of the rabbit"); Assert.LessOrEqual(land, 0.3f); }
                Assert.AreEqual(HuntTiming.StandOff, Dist(p.standX, p.standZ, p.landX, p.landZ), 1e-3f, "the hunter stands a step short of the spear or the catch");
            }
            Assert.Greater(catches, 0); Assert.Greater(misses, 0);
        }

        [Test]
        public void TheStrike_LastsItsBeatInRealSeconds_AndResolvesAtTheLanding()
        {
            var s = Herd();
            foreach (var p in Plans(s))
            {
                var strike = p.steps.First(x => x.phase == HuntPhase.Strike);
                Assert.AreEqual(HuntTiming.StrikeSeconds, LifeClock.RealSecondsBetween(strike.start, strike.end), 0.08f, "the beat fits its step at any hour");
                Assert.AreEqual(HuntTiming.LandAt, LifeClock.RealSecondsBetween(strike.start, p.resolveHour), 0.08f, "the sim settles as the spear comes down");
                Assert.AreEqual(HuntTiming.ReleaseAt, LifeClock.RealSecondsBetween(strike.start, p.releaseHour), 0.08f);
                var stalk = p.steps.First(x => x.phase == HuntPhase.Stalk);
                Assert.AreEqual(p.StalkSeconds, LifeClock.RealSecondsBetween(stalk.start, stalk.end), 0.1f);
                Assert.AreEqual(p.strikeStart, strike.start, 1e-4f);
            }
        }

        [Test]
        public void TheRabbit_StandsStillThroughTheThrow()
        {
            var s = Herd();
            foreach (var p in Plans(s))
            {
                double t0 = HuntClock.FieldSeconds(p.day, p.strikeStart);
                for (float dt = 0f; dt <= HuntTiming.StrikeSeconds; dt += 0.25f)
                    Assert.AreNotEqual(RabbitAct.Hop, s.field.Sample(p.rabbitId, t0 + dt).act, $"hunt {p.index}: the rabbit hops {dt:0.00} s into the strike");
                s.field.PositionOf(p.rabbitId, t0, out float x, out float z);
                Assert.AreEqual(p.targetX, x, 1e-3f); Assert.AreEqual(p.targetZ, z, 1e-3f);
            }
        }

        [Test]
        public void AfterTheThrow_ACatchIsLiftedAndHeldUp_AMissPullsItsSpearOut()
        {
            var s = Herd();
            bool sawCatch = false, sawMiss = false;
            foreach (var p in Plans(s))
            {
                var ph = p.steps.Select(x => x.phase).ToList();
                int strike = ph.IndexOf(HuntPhase.Strike);
                if (p.catches)
                {
                    sawCatch = true;
                    // t3-hunt: a short CarryBack walk to the hold-up spot comes before the HoldUp (repeats collapse)
                    var after = ph.Skip(strike + 1).Take(7).ToList();
                    if (after.Count > 2 && after[2] == HuntPhase.CarryBack) after.RemoveAt(2);   // the walk to the hold-up spot (absent when the rabbit fell by the camp)
                    CollectionAssert.AreEqual(new[] { HuntPhase.ToCatch, HuntPhase.PickUp, HuntPhase.HoldUp, HuntPhase.CarryBack, HuntPhase.Skin, HuntPhase.Cook }, after.Take(6).ToList());
                    var hold = p.steps.First(x => x.phase == HuntPhase.HoldUp);
                    Assert.AreEqual((ActivityKind.HoldUp, Prop.Rabbit), (hold.kind, hold.prop));
                    Assert.AreEqual(HuntTiming.HoldUpSeconds, LifeClock.RealSecondsBetween(hold.start, hold.end), 0.1f);
                    var carry = p.steps.Where(x => x.phase == HuntPhase.CarryBack).OrderByDescending(x => Dist(x.x0, x.z0, x.x1, x.z1)).First();   // t3-hunt: the long walk home
                    float speed = Dist(carry.x0, carry.z0, carry.x1, carry.z1) / LifeClock.RealSecondsBetween(carry.start, carry.end);
                    float walk = LifePlanner.WalkSpeed / LifeClock.RealSecondsPerViewHour(carry.start);
                    Assert.Less(speed, walk * 0.95f, "a slow proud walk home, slower than a plain one");
                    // #574: parents hunt now, which adds plans; a walk home that crosses into the evening changes clock rate mid-walk (the pace is measured at its start), so the lower bound holds only where the rate is the same at both ends
                    if (Math.Abs(LifeClock.RealSecondsPerViewHour(carry.start) - LifeClock.RealSecondsPerViewHour(carry.end - 0.001f)) < 0.01f) Assert.Greater(speed, walk * 0.6f);
                    Assert.Greater(p.takeHour, p.steps.First(x => x.phase == HuntPhase.PickUp).start);
                }
                else
                {
                    sawMiss = true;
                    if (p.retry != null)   // #680: a missed throw that tries again ends at the pulled spear; the try walks home
                        CollectionAssert.AreEqual(new[] { HuntPhase.Bolt, HuntPhase.FetchSpear, HuntPhase.PullSpear }, ph.Skip(strike + 1).ToList());
                    else
                        CollectionAssert.AreEqual(new[] { HuntPhase.Bolt, HuntPhase.FetchSpear, HuntPhase.PullSpear, HuntPhase.WalkBack }, ph.Skip(strike + 1).Take(4).ToList());
                    var pull = p.steps.First(x => x.phase == HuntPhase.PullSpear);
                    Assert.IsTrue(pull.faceSpear, "he faces the spear");
                    Assert.Less(p.takeHour, pull.end);
                    Assert.Greater(p.takeHour, pull.start);
                }
                if (p.HasKid)
                {
                    Assert.AreEqual(p.catches, p.kidSteps.Any(x => x.phase == HuntPhase.KidCheer));
                    Assert.AreEqual(p.catches, p.kidSteps.Any(x => x.phase == HuntPhase.KidEscort), "the kid walks home beside the hunter after a catch");
                }
            }
            Assert.IsTrue(sawCatch && sawMiss);
        }

        /// <summary>#680: a hunter who misses tries once more before walking home: the miss is followed by a second walk-out, stalk and
        /// strike from where the spear was pulled out, before any walk back; the second try never tries a third time; the catches still
        /// keep to the day's allowance (half the surplus), and the kid who followed stays through the try.</summary>
        [Test]
        public void AMissedHunt_TriesOnceMore_BeforeWalkingHome_NeverAThirdTime()
        {
            var s = Herd(7, 12);
            int retries = 0, withKid = 0;
            for (int i = 0; i < 60; i++)
            {
                long day = s.sim.State.month + i;
                var plans = s.planner.HuntsOn(day);
                int allowed = (s.sim.HuntSurplus + 1) / 2;
                Assert.LessOrEqual(plans.Count(p => p.catches && !p.forced), Math.Max(allowed, 0) + 0, $"day {day}: the try counts against the day's catches");
                foreach (var p in plans)
                {
                    if (p.IsRetry)
                    {
                        Assert.IsNull(p.retry, "never a third throw");
                        Assert.AreSame(p, p.retryOf.retry);
                        Assert.IsFalse(p.retryOf.catches, "only after a miss");
                        continue;
                    }
                    if (p.retry == null) continue;
                    retries++;
                    var r = p.retry;
                    // the whole outing, the first throw then the try, in order
                    var all = p.steps.Concat(r.steps).Select(x => x.phase).ToList();
                    int s1 = all.IndexOf(HuntPhase.Strike), s2 = all.LastIndexOf(HuntPhase.Strike), back = all.IndexOf(HuntPhase.WalkBack);
                    Assert.AreEqual(2, all.Count(x => x == HuntPhase.Strike), "two throws, not three");
                    Assert.AreEqual(2, all.Count(x => x == HuntPhase.Stalk), "a second stalk");
                    Assert.Less(s1, s2);
                    Assert.Less(all.IndexOf(HuntPhase.PullSpear), all.LastIndexOf(HuntPhase.Stalk), "the spear is pulled out before the second stalk");
                    if (!r.catches) Assert.Greater(back, s2, "the walk home comes after the second throw"); else Assert.AreEqual(-1, back, "a catch is carried home");
                    Assert.IsFalse(p.steps.Any(x => x.phase is HuntPhase.WalkBack or HuntPhase.Return), "the first throw has no walk home");
                    Assert.AreEqual(p.end, r.start, 1e-4f, "the try follows the pulled spear without a gap");
                    Assert.AreEqual(HuntPhase.WalkOut, r.steps[0].phase, "the try starts with the spear in hand, from where it was pulled out");
                    Assert.AreEqual(p.standX, r.steps[0].x0, 1e-4f); Assert.AreEqual(p.standZ, r.steps[0].z0, 1e-4f);
                    Assert.AreEqual(Prop.Spear, r.steps[0].prop);
                    Assert.AreNotEqual(p.rabbitId, r.rabbitId, "after another rabbit (the first one bolted)");
                    Assert.Greater(r.resolveHour, p.resolveHour);
                    Assert.LessOrEqual(r.end, LifeClock.AfternoonEnd(LifeClock.SeasonOf(day)) + 0.5f + 1e-3f, "the afternoon had time");
                    if (p.HasKid)
                    {
                        withKid++;
                        Assert.AreEqual(p.kid, r.kid, "the kid stays with them");
                        Assert.IsFalse(p.kidSteps.Any(x => x.phase == HuntPhase.KidBack), "the kid doesn't go home after the miss");
                        Assert.AreEqual(p.end, p.kidSteps[p.kidSteps.Count - 1].end, 1e-4f, "the kid waits for the spear");
                        Assert.AreEqual(r.start, r.kidSteps[0].start, 1e-4f, "and goes on with the try");
                        Assert.IsTrue(r.kidSteps[0].hasXZ, "from where they stood");
                    }
                }
            }
            Assert.Greater(retries, 0, "some misses try again");
            TestContext.WriteLine($"retries {retries}, with a kid {withKid}");
        }

        static float Dist(float ax, float az, float bx, float bz) { float dx = ax - bx, dz = az - bz; return (float)Math.Sqrt(dx * dx + dz * dz); }
    }
}
