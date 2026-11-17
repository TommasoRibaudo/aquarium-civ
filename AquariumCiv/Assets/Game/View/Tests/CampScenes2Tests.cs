// #692 part 2: the Keeper's tree visits' timelines (pure builders, CampScenes2) and who/when rules (TreeVisits) against the spec rows
// B32.a-h and the D rules (EditMode, no scene).
using System.Collections.Generic;
using System.Linq;
using AquariumCiv.Life;
using AquariumCiv.Sim;
using NUnit.Framework;
using UnityEngine;
using static AquariumCiv.View.Tests.CampScenesTests;

namespace AquariumCiv.View.Tests
{
    public class CampScenes2Tests
    {
        static readonly Vector2 Tree = new Vector2(-0.6f, 0.4f), Dir = Vector2.down;
        const int Stage = 4;

        static Vector2 Pos(CampScene s, int m, DollStyle style) => s.parts[m].First(p => p.style == style && !p.keepSpot).pos;

        [Test]
        public void TreeGeo_TheVisitSpotIsOutsideTheRingAndTheSitIsAtTheTrunk()
        {
            Assert.AreEqual(TreeGeo.RingR(Stage) + 0.08f, (TreeGeo.Visit(Tree, Stage, Dir) - Tree).magnitude, 1e-4f, "the ring's edge + 0.08 u");
            Assert.AreEqual(0.2f, TreeGeo.RingR(4), 1e-4f, "KeeperTreeKit's stone ring at stage 4");
            Assert.Less((TreeGeo.Lean(Tree, Stage, Dir) - Tree).magnitude, 0.13f, "sits against the trunk");
            Assert.Greater(TreeGeo.CrownY(4), TreeGeo.CrownY(3));
        }

        [Test]
        public void B32a_SitUnderIt_SlideDownDozeLeafStretchPat()
        {
            var s = CampScenes2.TreeSit(1, Tree + new Vector2(1f, -0.5f), Tree, Stage, Dir, 8f, 0f, 2, Tree + new Vector2(-1f, -0.4f));
            Sane(s); PoseRuns(s);
            Assert.AreEqual(2, s.Count, "two sitters");
            var p = DollPose.Rest; CampScenes2.SlideSitPose(ref p, 0.6f, 0.6f);
            Assert.AreEqual(0.7f, p.squash, 0.01f, "squash 1 -> 0.7");
            Assert.AreEqual(-10f, p.lean, 0.5f, "leaning back on the bark");
            float droop = 0f;
            for (float t = 0f; t < 6f; t += 0.05f) { var q = DollPose.Rest; CampScenes2.DozePose(ref q, t, 8f); droop = Mathf.Max(droop, q.lean + 10f); }
            Assert.AreEqual(20f, droop, 1f, "the head droops 20");
            Assert.AreEqual(2, s.fx.Count(f => f.kind == CampFxKind.Leaves), "a leaf falls on each");
            Assert.AreEqual(2, s.fx.Count(f => f.kind == CampFxKind.TreeShake), "a pat on the trunk each: the crown sways");
            Assert.IsTrue(s.parts[0].Any(x => x.style == DollStyle.Stretch), "stands and stretches");
            Assert.Less((Pos(s, 0, DollStyle.Sit) - Tree).magnitude, 0.15f, "against the trunk");
        }

        [Test]
        public void B32b_TouchInPassing_GrownUpTrailsAHandKidSlaps()
        {
            var g = CampScenes2.TreeTouch(1, false, Tree + new Vector2(-0.5f, -0.1f), Tree, Stage, Vector2.right, 0f);
            Sane(g); PoseRuns(g);
            Assert.IsTrue(g.parts[0].Any(x => x.poseWalking && x.speed < CampScenes1.Walk * 0.7f), "slows to 0.6 x with the hand on the bark");
            float closest = g.parts[0].Min(x => (x.pos - Tree).magnitude);
            Assert.Less(closest, 0.4f + 1e-3f, "within 0.4 u of the trunk");
            var k = CampScenes2.TreeTouch(2, true, Tree + new Vector2(-0.5f, 0.1f), Tree, Stage, Vector2.right, 0f);
            Sane(k); PoseRuns(k);
            Assert.AreEqual(1, k.fx.Count(f => f.kind == CampFxKind.TreeShake), "the slap shakes the leaves");
            Assert.IsTrue(k.parts[0].Any(x => x.run), "runs on");
        }

        [Test]
        public void B32c_KidsCircle_RingRoundTheTrunkBothWaysThenTouchAndFall()
        {
            var kids = new List<int> { 1, 2, 3, 4 };
            var at = kids.Select(i => Tree + new Vector2(0.5f + 0.1f * i, -0.6f)).ToList();
            var s = CampScenes2.TreeCircle(kids, at, Tree, Stage, Dir, 0f);
            Sane(s); PoseRuns(s);
            float r = TreeGeo.RingR(Stage) + 0.1f;
            var ring = s.parts[0].Where(x => x.poseWalking).ToList();
            Assert.GreaterOrEqual(ring.Count, 24, "one round each way");
            foreach (var p in ring) Assert.AreEqual(r, (p.pos - Tree).magnitude, 1e-3f, "on the ring (the stone ring + 0.1 u)");
            Assert.AreEqual(CampScenes2.RingSpeed, ring[0].speed, 1e-4f);
            Assert.AreEqual(1, s.fx.Count(f => f.kind == CampFxKind.TreeShake), "all touch the trunk together");
        }

        [Test]
        public void B32d_QuietVisit_WalkTouchGlowSitLeafTieLookUpHug()
        {
            var fire = Tree + new Vector2(1.2f, -0.6f);
            var s = CampScenes2.TreeQuiet(1, fire + new Vector2(0.3f, 0f), Tree, Stage, Dir, true, new List<int> { 2, 3 }, new List<Vector2> { fire + Vector2.left * 0.3f, fire + Vector2.up * 0.3f }, 2, fire + Vector2.left * 0.3f, 10f);
            Sane(s); PoseRuns(s);
            var walk = s.parts[0][0];
            Assert.IsTrue(walk.poseWalking, "head down while walking");
            Assert.That(walk.Len, Is.InRange(4f, 12f), "a slow walk, 4-6 s or more from further off");
            Assert.LessOrEqual(walk.speed, CampScenes1.Walk * 0.75f, "0.7 x");
            Assert.IsTrue(s.parts[0].Any(x => x.style == DollStyle.LookUp), "looks up at the crown");
            Assert.AreEqual(1, s.fx.Count(f => f.kind == CampFxKind.Glow), "the glow comes up");
            Assert.IsTrue(s.tips.Any(t => t.kind == CampTipKind.Leaf), "a leaf on the shoulder");
            Assert.IsTrue(s.tips.Any(t => t.kind == CampTipKind.Charm), "the charm to tie");
            Assert.Greater(s.b, s.a, "tied after the touch");
            Assert.AreEqual(1, s.pairs.Count, "a hug at the end");
            Assert.AreEqual(PairKind.Hug, s.pairs[0].kind);
            Assert.IsTrue(s.parts[1].Any(x => x.keepSpot), "the people at the fire turn and watch, from where they sit");
            var noTie = CampScenes2.TreeQuiet(1, fire, Tree, Stage, Dir, false, new List<int>(), new List<Vector2>(), VillageScenes.Nobody, fire, 0f);
            Sane(noTie);
            Assert.IsFalse(noTie.tips.Any(t => t.kind == CampTipKind.Charm), "no charm: the forehead on the bark");
            Assert.That(noTie.Length, Is.InRange(20f, 40f), "~25 s");
        }

        [Test]
        public void B32efgh_Newlyweds_Newborn_TheOldOne_TheEldestTells()
        {
            var wed = CampScenes2.TreeWed(1, 2, Tree + new Vector2(1f, -0.4f), Tree + new Vector2(1.3f, -0.2f), Tree, Stage, Dir, 0f);
            Sane(wed); PoseRuns(wed);
            Assert.IsTrue(wed.parts[0].Any(p => p.style == DollStyle.LookUp) && wed.parts[1].Any(p => p.style == DollStyle.LookUp), "both look up");
            Assert.IsTrue(wed.parts[0].Count(p => p.poseWalking) >= 2, "hand in hand there and back");

            var nb = CampScenes2.TreeNewborn(3, Tree + new Vector2(1f, -0.4f), Tree, Stage, Dir, 0f);
            Sane(nb); PoseRuns(nb);
            Assert.AreEqual(1, nb.fx.Count(f => f.kind == CampFxKind.Leaves), "the tree's answer: a leaf drifts down past it");
            Assert.IsTrue(nb.parts[0].Any(p => p.style == DollStyle.Bow), "a small bow");
            Assert.AreEqual(0f, CampLife.BabyLift(nb.a - 0.1f, nb.a, nb.b), 1e-4f);
            Assert.AreEqual(0.85f, CampLife.BabyLift(nb.a + 2f, nb.a, nb.b), 1e-3f, "lifted toward the crown");
            Assert.AreEqual(0f, CampLife.BabyLift(nb.b + 0.7f, nb.a, nb.b), 1e-4f, "cradled again");

            var old = CampScenes2.TreeOld(4, Tree + new Vector2(1f, -0.4f), Tree, Stage, Dir, 12f, 5, Tree + new Vector2(0.8f, -0.9f), 6, Tree + new Vector2(1.4f, -0.5f), 0f);
            Sane(old); PoseRuns(old);
            Assert.IsTrue(old.parts[0][0].poseWalking && old.parts[0][0].speed <= CampScenes1.Walk * 0.65f, "a slow bent walk");
            Assert.IsTrue(old.tips.Count(t => t.kind == CampTipKind.Flower) == 2, "the kid brings a flower and hands it over");
            Assert.IsTrue(old.parts[2].Any(p => p.poseWalking), "the grown kid walks them home");

            var kids = new List<int> { 7, 8, 9 };
            var tell = CampScenes2.TreeTell(10, Tree + new Vector2(1f, 0f), kids, kids.Select(k => Tree + new Vector2(0.5f, -0.5f - 0.1f * k)).ToList(), Tree, Stage, Dir, 0f);
            Sane(tell); PoseRuns(tell);
            Assert.AreEqual(2, tell.fx.Count(f => f.kind == CampFxKind.TreeShake), "the eldest's pat and a kid's");
            Assert.IsTrue(tell.parts.Skip(1).Count(l => l.Any(p => p.style == DollStyle.LookUp)) >= 2, "the kids' heads turn up to the glass");
            Assert.IsTrue(tell.parts[1].Any(p => p.style == DollStyle.WaveSmall), "one waves at the glass");
            Assert.IsTrue(tell.parts.Skip(1).All(l => l.Any(p => p.run)), "they scatter");
        }

        static Notable N(int id, float age, bool alive = true, int partner = -1, int parent = -1, int friend = -1) => new Notable { id = id, age = age, alive = alive, partnerId = partner, parentId = parent, friendId = friend };

        [Test]
        public void B32d_TheMourner_PartnerThenAGrownKidThenAFriend()
        {
            var dead = N(1, 60f, false, partner: 2, friend: 5);
            var all = new List<Notable> { dead, N(2, 58f, partner: 1), N(3, 30f, parent: 1), N(4, 8f, parent: 1), N(5, 50f) };
            Assert.AreEqual(2, TreeVisits.Mourner(all, dead, k => true), "the partner");
            Assert.AreEqual(3, TreeVisits.Mourner(all, dead, k => k != 2), "else a grown kid (not the 8-year-old)");
            Assert.AreEqual(5, TreeVisits.Mourner(all, dead, k => k == 5 || k == 4), "else a friend");
            Assert.AreEqual(VillageScenes.Nobody, TreeVisits.Mourner(all, dead, k => k == 4), "nobody: no visit");
        }

        [Test]
        public void B32d_WaitsUpToTwoDusksForTheKeeper_ThenPlaysUnseen()
        {
            Assert.IsTrue(TreeVisits.Due(100, 100, out int which, out int dusk) && which == 0 && dusk == 0, "the first dusk after the death");
            Assert.IsTrue(TreeVisits.Due(101, 100, out _, out dusk) && dusk == 1, "the second dusk");
            Assert.IsFalse(TreeVisits.Due(102, 100, out _, out _), "not a third (Q10)");
            Assert.IsTrue(TreeVisits.PlayNow(0, 17.5f, true), "watched: now");
            Assert.IsFalse(TreeVisits.PlayNow(0, 18.9f, false), "unwatched on the first dusk: waits");
            Assert.IsFalse(TreeVisits.PlayNow(1, 18.5f, false), "the second dusk: still waits for the Keeper a while");
            Assert.IsTrue(TreeVisits.PlayNow(1, 19.1f, false), "late on the last dusk: plays unseen");
            Assert.IsFalse(TreeVisits.PlayNow(0, 12f, true), "only at dusk");
            long spring = TreeVisits.FirstSpringAfter(100);
            Assert.Greater(spring, 101);
            Assert.AreEqual(Season.Spring, LifeClock.SeasonOf(spring));
            Assert.AreNotEqual(Season.Spring, LifeClock.SeasonOf(spring - 1), "the first day of the next spring");
            Assert.IsTrue(TreeVisits.Due(spring, 100, out which, out dusk) && which == 1 && dusk == 0, "once more at the first dusk of the next spring");
        }

        [Test]
        public void B32ef_WhenTheNewlywedsAndTheNewbornGo()
        {
            Assert.IsTrue(TreeVisits.WeddingDue(50, 50, 18f), "the dusk of the wedding");
            Assert.IsTrue(TreeVisits.WeddingDue(51, 50, 8f), "or the next morning");
            Assert.IsFalse(TreeVisits.WeddingDue(51, 50, 18f));
            Assert.IsFalse(TreeVisits.WeddingDue(52, 50, 8f));
            Assert.IsTrue(TreeVisits.NewbornDue(20, 18, 9f), "a morning in its first days");
            Assert.IsFalse(TreeVisits.NewbornDue(20, 18, 14f), "mornings only");
            Assert.IsFalse(TreeVisits.NewbornDue(30, 18, 9f));
        }
    }
}
