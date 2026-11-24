using System;
using System.Collections.Generic;
using System.Linq;
using AquariumCiv.Sim;
using NUnit.Framework;

namespace AquariumCiv.Life.Tests
{
    /// <summary>
    /// S-ritual (B72-B75, animation-specs "Rituals and the teller"): each yearly ritual's own staged plan. The pure plan's rules (R) hold
    /// for each: steps back to back, an end beat, everyone released by the end, inside the box, one part each, within
    /// <see cref="LifePlanner.RitualSeconds"/>. Then each ritual's beats: the berrying walk's bushes, line and tip; the log line's zigzag,
    /// the logs passed and the dropped one; the long table's seats, board, budge and bowls up; the offering's carry, kids and bow wave.
    /// No target: the festival's gathering stands in.
    /// </summary>
    public class RitualPlanTests
    {
        static readonly MomentGeometry Geo = new MomentGeometry { fireX = 0f, fireZ = 0.6f, glassX = 0f, glassZ = -2.1f, nx = 0f, nz = -1f, halfX = 3f, halfZ = 3f, centreZ = 0.8f };

        static MomentPerson P(int key, float x, float z, bool kid = false, float age = 30f)
        {
            var p = MomentPerson.Of(key, kid, x, z, (float)Math.Sqrt((x - Geo.fireX) * (x - Geo.fireX) + (z - Geo.fireZ) * (z - Geo.fireZ)) / 0.31f);
            p.age = age;
            return p;
        }

        static MomentCast Cast(int grown = 7, int kids = 4)
        {
            var c = new MomentCast { officiant = P(1, 0.3f, 0.9f, false, 52f), hasOfficiant = true };
            for (int i = 0; i < grown; i++) c.grown.Add(P(10 + i, -1f + 0.3f * i, 0.3f + 0.25f * (i % 3), false, 20f + 4f * i));
            for (int i = 0; i < kids; i++) c.kids.Add(P(-1 - i, 0.7f - 0.4f * i, 1.2f, true, 6f + i));
            return c;
        }

        static RitualSpots Spots(string ritual)
        {
            var s = new RitualSpots { ritual = ritual };
            switch (ritual)
            {
                case RitualKind.Berrying:
                    s.bushes = new[] { 1.4f, 1.6f, 1.7f, 1.1f, 1.2f, 2.0f, 2.9f, 2.9f };
                    s.hasStack = true; s.stackX = 0.4f; s.stackZ = 0.3f;
                    s.hasStash = true; s.stashX = -0.4f; s.stashZ = 0.4f;
                    break;
                case RitualKind.Woodpile:
                    s.hasPile = true; s.pileX = -0.6f; s.pileZ = 1.0f; s.pileFX = 0f; s.pileFZ = -1f;
                    s.hasChop = true; s.chopX = -1.5f; s.chopZ = 1.0f;
                    break;
                case RitualKind.Feast:
                    s.hasTable = true; s.tableX = 0.6f; s.tableZ = 0.1f; s.tableAX = 1f; s.tableAZ = 0f; s.tableLen = 0.78f; s.boards = 3; s.addBoard = true;
                    break;
                case RitualKind.Offering:
                    s.hasStash = true; s.stashX = -0.4f; s.stashZ = 0.4f;
                    s.hasTree = true; s.keeperTree = true; s.treeX = 1.6f; s.treeZ = -0.6f; s.spotX = 1.5f; s.spotZ = -0.75f;
                    s.ritualKid = -2;
                    break;
            }
            return s;
        }

        static MomentStagePlan Build(string ritual, MomentCast c = null, Action<RitualSpots> tweak = null, int seed = 721)
        {
            var g = Geo;
            g.spots = Spots(ritual);
            tweak?.Invoke(g.spots);
            var plan = MomentStagePlan.Build(MomentKind.Ritual, c ?? Cast(), g, seed);
            Assert.IsNotNull(plan, $"{ritual}: {MomentStagePlan.LastWhyNot}");
            return plan;
        }

        static IEnumerable<string> Rituals => RitualKind.All;

        [TestCaseSource(nameof(Rituals))]
        public void EachRitualHasItsOwnPlan_BackToBack_EndBeat_ReleasedInTime(string ritual)
        {
            var plan = Build(ritual);
            Assert.AreEqual(ritual, plan.RitualStaged, "its own beats, not the festival's stand-in");
            Assert.Greater(plan.members.Count, 2);
            Assert.LessOrEqual(plan.Duration, LifePlanner.RitualSeconds, "fits the held moment's budget");
            Assert.Greater(plan.Duration, 20f);
            foreach (var m in plan.members)
            {
                Assert.IsNotEmpty(m.steps, $"{ritual} {m.role} {m.key} has steps");
                for (int i = 1; i < m.steps.Count; i++)
                    Assert.AreEqual(m.steps[i - 1].t1, m.steps[i].t0, 1e-4f, $"{ritual} {m.role} {m.key}: step {i} ({m.steps[i].beat}) starts where {m.steps[i - 1].beat} ended");
                Assert.IsTrue(m.steps[m.steps.Count - 1].endBeat, $"{ritual} {m.role} {m.key} ends on an end beat, not {m.steps[m.steps.Count - 1].beat}");
                Assert.LessOrEqual(m.ReleaseAt, plan.Duration + 1e-4f);
                Assert.IsTrue(m.steps[0].travel, $"{ritual} {m.key}: everyone starts from where they stand (the planner walks them there)");
                Assert.LessOrEqual(m.steps[0].t1, MomentStagePlan.MaxGather + 1e-3f, "the gathering takes at most 16 s");
                foreach (var s in m.steps)
                {
                    Assert.LessOrEqual(Math.Abs(s.x1), Geo.halfX); Assert.LessOrEqual(Math.Abs(s.z1 - Geo.centreZ), Geo.halfZ, "inside the box");
                    if (!s.travel) Assert.GreaterOrEqual(D(s.x1, s.z1, Geo.fireX, Geo.fireZ), MomentStagePlan.FireClear - 0.06f, $"{ritual} {m.key} {s.beat}: nobody stands in the fire");
                    // a walk is never faster than a run (the plan's own moves read as walks)
                    if (!s.travel && s.Moves) Assert.Less(Math.Sqrt((s.x1 - s.x0) * (s.x1 - s.x0) + (s.z1 - s.z0) * (s.z1 - s.z0)) / s.Length, 0.75, $"{ritual} {m.role} {m.key} {s.beat} at {s.t0:0.0}-{s.t1:0.0} moves at a walk");
                }
            }
            Assert.AreEqual(plan.members.Count, plan.members.Select(m => m.key).Distinct().Count(), "one part each");
            int lines = plan.members.Sum(m => m.steps.Count(s => s.line != null));
            Assert.Greater(lines, 0, "its lines are said at their beats");
            foreach (var m in plan.members) Assert.AreEqual(m.steps.Count(s => s.line != null), m.steps.Where(s => s.line != null).Select(s => s.line).Distinct().Count(), "each line once");
        }

        [TestCaseSource(nameof(Rituals))]
        public void NoTarget_TheFestivalStandsIn(string ritual)
        {
            var g = Geo; g.spots = new RitualSpots { ritual = ritual };
            var plan = MomentStagePlan.Build(MomentKind.Ritual, Cast(), g, 7);
            Assert.IsNotNull(plan);
            if (ritual == RitualKind.Offering) { Assert.AreEqual(ritual, plan.RitualStaged, "no tree: the offering is set down in the camp and they look up (beats 1, 5, 6)"); return; }
            Assert.IsNull(plan.RitualStaged, "the festival's gathering plays");
            Assert.IsTrue(plan.members.Any(m => m.steps.Any(s => s.beat == StageBeat.Ring)), "the ring round the fire");
        }

        static float D(float ax, float az, float bx, float bz) => (float)Math.Sqrt((ax - bx) * (ax - bx) + (az - bz) * (az - bz));

        [Test]
        public void Berrying_HandOut_PickAtTheBushes_TipAtTheStash_HandsUp()
        {
            var plan = Build(RitualKind.Berrying);
            var sp = Spots(RitualKind.Berrying);
            var lead = plan.members.First(m => m.role == MomentRole.Leader);
            Assert.AreEqual(4, lead.steps.Count(s => s.beat == StageBeat.HandOut), "the lead hands out four baskets");
            Assert.AreEqual(1, lead.steps.Count(s => s.beat == StageBeat.HoldBasketUp));
            Assert.AreEqual(1, lead.steps.Count(s => s.beat == StageBeat.PointBush && s.line == "Not that bush, that bush."), "the lead points a picker on, once");
            foreach (var m in plan.members)
            {
                var pick = m.steps.First(s => s.beat == StageBeat.Pick);
                float near = float.MaxValue;
                for (int i = 0; i < 6; i += 2) near = Math.Min(near, D(pick.x0, pick.z0, sp.bushes[i], sp.bushes[i + 1]));
                Assert.Less(near, 0.2f, $"{m.key} picks at a bush");
                Assert.AreEqual(m.kid ? 1 : 0, pick.arg, "kids eat every other berry");
                var tip = m.steps.First(s => s.beat == StageBeat.Tip);
                Assert.Less(D(tip.x0, tip.z0, sp.stashX, sp.stashZ), 0.5f, "they tip at the stash");
                Assert.IsTrue(m.steps.Any(s => s.beat == StageBeat.WalkHome && s.hold == StageHold.FullBasket), "home with a full basket");
                Assert.IsTrue(m.steps[m.steps.Count - 1].beat is StageBeat.ShowHands or StageBeat.KidHands, "the hands up to the glass");
            }
            Assert.AreEqual(2, plan.members.Count(m => m.steps[m.steps.Count - 1].beat == StageBeat.KidHands), "two kids, hands side by side");
            Assert.AreNotEqual(0, plan.RunAhead, "a kid runs ahead on the walk out");
            var runner = plan.Member(plan.RunAhead);
            Assert.IsTrue(runner.kid);
            Assert.AreEqual(2, runner.steps.Count(s => s.beat == StageBeat.Run), "ahead and back, once");
            // the tips run down the line, 0.4 s apart
            var cues = plan.members.Select(m => m.steps.First(s => s.beat == StageBeat.Tip).cue).OrderBy(c => c).ToList();
            for (int i = 1; i < cues.Count; i++) Assert.AreEqual(MomentStagePlan.TipGap, cues[i] - cues[i - 1], 1e-4f);
            // the lead walks out first and home last
            var homeCues = plan.members.ToDictionary(m => m.key, m => m.steps.First(s => s.beat == StageBeat.Tip).cue);
            Assert.AreEqual(cues.Last(), homeCues[lead.key], 1e-4f, "the lead at the back on the way home");
        }

        [Test]
        public void Berrying_EndsAtTheLongTable_InAVillageThatKeepsTheFeast()
        {
            var plan = Build(RitualKind.Berrying, tweak: s => { s.toTable = true; s.hasTable = true; s.tableX = 0.8f; s.tableZ = 0.2f; s.tableAX = 1f; s.tableLen = 0.7f; });
            foreach (var m in plan.members) Assert.IsTrue(m.steps.Any(s => s.beat == StageBeat.TableSet), "the full baskets go on the long table");
            Assert.IsFalse(plan.members.Any(m => m.steps.Any(s => s.beat == StageBeat.Tip)));
        }

        [Test]
        public void Berrying_AKidsFirstWalk_HandInHandWithAParent_SmallBasket_EatsEveryBerry()
        {
            var plan = Build(RitualKind.Berrying, tweak: s => { s.firstWalk = -1; s.firstWalkParent = 12; });
            var kid = plan.Member(-1);
            var par = plan.Member(12);
            Assert.IsTrue(kid.steps.Where(s => s.beat is StageBeat.WalkOut or StageBeat.WalkHome).All(s => s.arg == 1), "hand in hand all the way");
            Assert.IsTrue(kid.steps.Where(s => s.hold != StageHold.None).All(s => s.hold == StageHold.SmallBasket), "a small basket");
            Assert.AreEqual(2, kid.steps.First(s => s.beat == StageBeat.Pick).arg, "eats every berry it picks");
            Assert.AreEqual(2, par.steps.First(s => s.beat == StageBeat.Tip).arg, "the parent drops two berries into the kid's basket");
            var kp = kid.steps.First(s => s.beat == StageBeat.Pick); var pp = par.steps.First(s => s.beat == StageBeat.Pick);
            Assert.Less(D(kp.x0, kp.z0, pp.x0, pp.z0), 0.6f, "at the same bush");
        }

        [Test]
        public void LogLine_AZigzagFromTheHeapToThePile_LogsPassedAlong_KidsPassTwigsOnly()
        {
            var plan = Build(RitualKind.Woodpile);
            var sp = Spots(RitualKind.Woodpile);
            var lead = plan.members.First(m => m.role == MomentRole.Leader);
            var pile = lead.steps.First(s => s.beat == StageBeat.PileLog);
            Assert.Less(D(pile.x0, pile.z0, sp.pileX, sp.pileZ), 0.25f, "the lead stands last, at the pile");
            Assert.IsTrue(lead.steps.Any(s => s.beat == StageBeat.LastLog), "the lead sets the last log on top");
            Assert.AreEqual(1, plan.members.Count(m => m.role == MomentRole.Mender), "someone mends the roof");
            Assert.IsTrue(plan.members.First(m => m.role == MomentRole.Mender).steps.Any(s => s.beat == StageBeat.MendRoof));
            var passers = plan.members.Where(m => m.steps.Any(s => s.beat == StageBeat.PassLog)).ToList();
            Assert.AreEqual(plan.Passers, passers.Count);
            Assert.GreaterOrEqual(plan.LogCount, 4); Assert.LessOrEqual(plan.LogCount, MomentStagePlan.MaxLogLine);
            foreach (var m in passers) Assert.IsFalse(m.kid, "kids pass twigs only, never logs");
            // spaced 0.15 along the line, every other one 0.03 to the side
            var spots = passers.Select(m => m.steps.First(s => s.beat == StageBeat.PassLog)).OrderBy(s => s.arg).ToList();
            for (int j = 1; j < spots.Count; j++) Assert.AreEqual(MomentStagePlan.PassGap, Math.Abs((spots[j].x0 - spots[j - 1].x0) * plan.LineX + (spots[j].z0 - spots[j - 1].z0) * plan.LineZ), 0.01f);
            Assert.AreEqual(plan.LogReaches(0, 1) - plan.LogReaches(0, 0), MomentStagePlan.HopSeconds, 1e-4f);
            var kids = plan.members.Where(m => m.kid).ToList();
            Assert.IsTrue(kids.Any(k => k.steps.Any(s => s.beat == StageBeat.KeepTwig && s.line != null)), "one kid keeps the biggest twig");
            foreach (var k in kids)
            {
                Assert.IsFalse(k.steps.Any(s => s.hold == StageHold.Log), "no kid carries a log");
                var tw = k.steps.FirstOrDefault(s => s.beat == StageBeat.PassTwig);
                if (tw.beat == StageBeat.PassTwig)
                    foreach (var p in passers) { var ps = p.steps.First(s => s.beat == StageBeat.PassLog); Assert.GreaterOrEqual(D(tw.x0, tw.z0, ps.x0, ps.z0), 0.13f, "0.15 off the big line"); }
                Assert.AreEqual(StageBeat.SitLogs, k.steps[k.steps.Count - 1].beat, "the kids sit on the low logs at the end");
            }
            Assert.AreEqual(-1, plan.DropLogAt, "no dropped log unless the year's hash (or the cheat) says so");
        }

        [Test]
        public void LogLine_ByTheFrontGlass_TheLineRunsIntoTheBox()
        {
            var plan = Build(RitualKind.Woodpile, tweak: s => { s.pileZ = -1.6f; s.pileFZ = -1f; });
            Assert.IsTrue(plan.ByGlass);
            foreach (var m in plan.members)
                foreach (var st in m.steps.Where(x => !x.travel))
                    Assert.Greater(st.z1, -1.75f, $"{m.key} {st.beat}: nobody stands at the glass (z {st.z1:0.00})");
            Assert.AreEqual(1f, plan.LineZ, 1e-4f, "the line runs into the box, away from the glass");
        }

        [Test]
        public void LogLine_ADroppedLog_ThePasserFumbles_TheNextHopsBack()
        {
            var plan = Build(RitualKind.Woodpile, tweak: s => s.dropLog = true);
            Assert.GreaterOrEqual(plan.DropLogAt, 0);
            Assert.AreEqual(1, plan.members.Count(m => m.steps.Any(s => s.beat == StageBeat.Fumble)));
            Assert.AreEqual(1, plan.members.Count(m => m.steps.Any(s => s.beat == StageBeat.HopBack && s.line == "Mind your toes.")));
        }

        [Test]
        public void LongTable_SeatsFromTheHead_ABoardAdded_TheLateOneBudges_BowlsUpTogether()
        {
            var plan = Build(RitualKind.Feast);
            var seats = plan.Seats.Values.ToList();
            Assert.AreEqual(seats.Count, seats.Distinct().Count(), "one seat each");
            var lead = plan.members.First(m => m.role == MomentRole.Leader);
            Assert.AreEqual((1, 0), plan.Seats[lead.key], "the lead at the head");
            Assert.AreNotEqual(0, plan.EldestKey);
            var eldest = plan.Member(plan.EldestKey);
            Assert.AreEqual(10 + 6, plan.EldestKey, "the oldest grown-up gets the first bowl");
            Assert.IsTrue(lead.steps.Any(s => s.beat == StageBeat.Ladle) && lead.steps.Any(s => s.beat == StageBeat.Give));
            Assert.AreEqual(StageBeat.BenchNap, eldest.steps[eldest.steps.Count - 1].beat, "the oldest stays and lies down on the bench");
            Assert.Greater(plan.BoardLaidAt, 0f, "a board is added this year");
            Assert.IsTrue(plan.members.Any(m => m.steps.Any(s => s.beat == StageBeat.TapBoard && s.line == "One more seat this year.")));
            Assert.AreNotEqual(0, plan.LateKey);
            Assert.IsTrue(plan.members.Any(m => m.steps.Any(s => s.beat == StageBeat.Budge)), "the sitters budge up");
            Assert.AreEqual(2, plan.members.Count(m => m.steps.Any(s => s.beat == StageBeat.UnderTable)), "two kids crawl under the table");
            var ups = plan.members.Select(m => m.steps.First(s => s.beat == StageBeat.BowlsUp && s.arg == 0)).ToList();
            Assert.AreEqual(1, ups.Select(s => s.t0 + s.cue).Distinct().Count(), "the bowls go up at once (in step on purpose)");
            var sways = plan.members.Select(m => m.steps.First(s => s.beat == StageBeat.SongSway).cue).Distinct().Count();
            Assert.Greater(sways, 3, "the sway runs down the benches");
        }

        [Test]
        public void Offering_TheLeadCarries_TheKidsBehind_UpTo4GrownUps_TheBowWave()
        {
            var plan = Build(RitualKind.Offering);
            var sp = Spots(RitualKind.Offering);
            var lead = plan.members.First(m => m.role == MomentRole.Leader);
            Assert.AreEqual(1 + 2 + 4, plan.members.Count, "the lead, the kids and up to 4 grown-ups (row 228)");
            var carry = lead.steps.First(s => s.beat == StageBeat.CarryCareful);
            Assert.Less(D(carry.x1, carry.z1, sp.spotX, sp.spotZ), 0.05f, "to the basket at the Keeper's tree");
            Assert.AreEqual(1, lead.steps.First(s => s.beat == StageBeat.KneelSet).arg, "tipped into the basket at the tree");
            var kids = plan.members.Where(m => m.kid).ToList();
            Assert.AreEqual(2, kids.Count);
            Assert.IsTrue(kids.Any(k => k.key == sp.ritualKid), "the year's kid carries the small basket");
            // nobody walks ahead of the lead
            float dx = sp.spotX - carry.x0, dz = sp.spotZ - carry.z0; float l = (float)Math.Sqrt(dx * dx + dz * dz); dx /= l; dz /= l;
            foreach (var m in plan.members)
                foreach (var s in m.steps.Where(s => s.beat is StageBeat.CarryCareful or StageBeat.KidCarry or StageBeat.WalkOut && s.t0 < carry.t1))
                    if (m != lead) Assert.Less((s.x1 - carry.x1) * dx + (s.z1 - carry.z1) * dz, 0.001f, "behind the lead");
            var bows = plan.members.Select(m => (m, m.steps.First(s => s.beat == StageBeat.Bow).cue)).OrderBy(b => b.cue).ToList();
            Assert.AreEqual(lead, bows.Last().m, "the lead bows last");
            Assert.IsTrue(plan.members.Any(m => m.steps.Any(s => s.beat == StageBeat.LookBack)) && plan.members.Any(m => m.steps.Any(s => s.beat == StageBeat.TurnHead && s.line == "Don't look back, it's rude.")));
            Assert.Greater(plan.OfferingSetAt, 0f);
        }

        [Test]
        public void Offering_InTheTown_EveryoneWalksBehindThePriest()
        {
            var plan = Build(RitualKind.Offering, tweak: s => s.town = true);
            Assert.AreEqual(1 + 7 + 4, plan.members.Count, "the whole town walks behind");
            Assert.AreEqual(2, plan.members.Count(m => m.steps.Any(s => s.beat == StageBeat.KidCarry)), "two kids still carry the small basket");
        }

        [Test]
        public void Offering_BeforeTheKeepersTree_GoesToTheTallestTree()
        {
            var plan = Build(RitualKind.Offering, tweak: s => s.keeperTree = false);
            var lead = plan.members.First(m => m.role == MomentRole.Leader);
            Assert.AreEqual(0, lead.steps.First(s => s.beat == StageBeat.KneelSet).arg, "set down at the tallest tree (no basket there to tip into)");
        }

        [Test]
        public void Offering_AFarTree_StartsPartWay()
        {
            var plan = Build(RitualKind.Offering, tweak: s => { s.spotX = 2.7f; s.spotZ = 3.4f; s.treeX = 2.8f; s.treeZ = 3.5f; });
            var carry = plan.members.First(m => m.role == MomentRole.Leader).steps.First(s => s.beat == StageBeat.CarryCareful);
            Assert.LessOrEqual(carry.Length, MomentStagePlan.MaxCarry + 1e-3f, "the walk is at most 14 s");
        }
    }
}
