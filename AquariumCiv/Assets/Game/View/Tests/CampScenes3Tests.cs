// #692 part 3: the land's uses' timelines (pure builders, CampScenes3) and the boulders' spots (LandGeo) against the spec rows B33-B36
// (EditMode, no scene).
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using static AquariumCiv.View.Tests.CampScenesTests;

namespace AquariumCiv.View.Tests
{
    public class CampScenes3Tests
    {
        static readonly Vector2 Spot = new Vector2(0.4f, 0.9f), ToCam = Vector2.down, Across = Vector2.right;

        static int Marks(CampScene s, CampMark m) => s.fx.Count(f => f.kind == CampFxKind.Mark && f.count == (int)m);

        [Test]
        public void LandGeo_BouldersAreTwoApartOnFreeGround_AndPebblesShrinkUpTheStack()
        {
            var fire = new Vector2(0.2f, 0.6f);
            var b = LandGeo.Boulders(fire, ToCam, 4242, p => true);
            Assert.AreEqual(2, b.Count, "two boulders");
            foreach (var p in b) Assert.That((p - fire).magnitude, Is.InRange(LandGeo.RockFrom - 1e-3f, LandGeo.RockTo + 1e-3f), "at the camp's edge");
            Assert.GreaterOrEqual((b[0] - b[1]).magnitude, LandGeo.RockApart, "apart");
            Assert.AreEqual(b, LandGeo.Boulders(fire, ToCam, 4242, p => true), "the same every time (from the seed)");
            Assert.AreEqual(0, LandGeo.Boulders(fire, ToCam, 4242, p => false).Count, "never where the box has no room");
            Assert.AreEqual(0.02f, LandGeo.PebbleSize(0), 1e-4f); Assert.AreEqual(0.012f, LandGeo.PebbleSize(4), 1e-4f);
            Assert.Greater(LandGeo.StackHeight(5), LandGeo.StackHeight(4));
            for (int i = 0; i < LandGeo.RingStones; i++) Assert.AreEqual(LandGeo.RingR, (LandGeo.RingSpot(Spot, i) - Spot).magnitude, 1e-4f, "the ring r 0.06");
        }

        [Test]
        public void B33a_RollInTheGrass_FlopThreeRollsGiggleSitUpWithGrassBits()
        {
            var s = CampScenes3.GrassRoll(new[] { 1, 2 }, new[] { Spot + new Vector2(1f, 0f), Spot + new Vector2(-1f, 0.2f) }, Spot, Across, 0f);
            Sane(s); PoseRuns(s);
            Assert.AreEqual(2, s.Count);
            Assert.AreEqual(2 * CampScenes3.Rolls, s.fx.Count(f => f.kind == CampFxKind.Grass && f.count == 4), "4 grass blades at each turn");
            float spin = 0f, move = 0f;
            var roll = CampScenes3.RollPose(2);
            for (int i = 0; i <= 20; i++) { float t = CampScenes3.RollSeconds * i / 20f; var p = DollPose.Rest; roll(ref p, t, CampScenes3.RollSeconds); spin = Mathf.Max(spin, 90f - p.lean); move = p.offset.z; }
            Assert.AreEqual(360f, spin, 8f, "a whole turn a roll");
            Assert.AreEqual(CampScenes3.RollStep * 3, move, 1e-3f, "RollStep a roll (#692 p3: 0.08 [est]), three rolls");
            var g = DollPose.Rest; CampScenes3.GigglePose(ref g, 0.1f, 0.8f);
            Assert.AreEqual(90f, Mathf.Abs(g.armR.z), 1e-3f, "arms out on the back");
            Assert.AreEqual(2, Marks(s, CampMark.GrassBits), "both sit up with grass bits in the hair");
            Assert.AreEqual(1, Marks(s, CampMark.Flatten), "the grass flattens where they rolled");
            Assert.IsTrue(s.parts[0].Any(p => p.run), "they run to it");
        }

        [Test]
        public void B33b_LieBack_HandsBehindTheHeadAPointAndALaugh()
        {
            var s = CampScenes3.LieBack(1, 2, Spot + new Vector2(0.8f, 0f), Spot + new Vector2(-0.6f, 0f), true, Spot, ToCam, 8f, 0f);
            Sane(s); PoseRuns(s);
            var p = DollPose.Rest; CampScenes3.LieBackPose(ref p, 0.6f, 0.6f);
            Assert.AreEqual(-90f, p.lean, 1f, "on the back: tipped backwards");
            Assert.AreEqual(170f, p.armL.x, 1f, "hands behind the head (x 170)");
            Assert.Greater(s.a, 0f, "a point up");
            Assert.IsTrue(s.parts[0].Any(x => x.style == DollStyle.Stretch), "one stretches");
            float still = s.parts[0].Where(x => x.Len >= 6f).Sum(x => x.Len);
            Assert.That(still, Is.InRange(6f, 10f), "still 6-10 s");
        }

        [Test]
        public void B33c_PickFlowers_TwoOrThreePicksABunchSniffedAndTucked()
        {
            var flowers = new List<Vector2> { Spot, Spot + new Vector2(0.1f, 0.05f), Spot + new Vector2(0.18f, 0f) };
            var s = CampScenes3.PickFlowers(1, Spot + new Vector2(-0.8f, 0f), flowers, 2, Spot + new Vector2(0.6f, -0.5f), 0f);
            Sane(s); PoseRuns(s);
            Assert.AreEqual(3, s.fx.Count(f => f.kind == CampFxKind.Grass && f.count == 2), "a pick each: 2 grass blades");
            Assert.IsTrue(s.tips.Any(t => t.kind == CampTipKind.Bunch && t.member == 1), "the taker holds the bunch");
            Assert.AreEqual(1, Marks(s, CampMark.EarFlower), "tucked behind the ear");
            Assert.IsTrue(s.parts[1].Any(p => p.pose == (CampPoseFn)CampScenes1.SniffNodPose), "the taker sniffs it (B30.d's sniff)");
            var take = s.parts[1].First(p => p.pose == (CampPoseFn)CampScenes1.SniffNodPose);
            Assert.Less((take.pos - flowers[2]).magnitude, 0.2f, "#692 p3: the taker comes to the grass, so the camera stays on the flowers");
            var stone = CampScenes3.PickFlowers(1, Spot, flowers.Take(2).ToList(), VillageScenes.Nobody, Spot + Vector2.up, 0f);
            Sane(stone);
            Assert.AreEqual(1, stone.Count, "laid on the stone: nobody else");
            Assert.AreEqual(1, Marks(stone, CampMark.EarFlower), "the flowers stay on the stone");
        }

        [Test]
        public void B33d_GrassWhistle_TwoDudsThenItWorks_TheOthersCoverTheirEars()
        {
            var s = CampScenes3.GrassWhistle(1, Spot + Vector2.left, Spot, new[] { 2, 3 }, new[] { Spot + new Vector2(0.3f, 0.1f), Spot + new Vector2(-0.2f, 0.3f) }, 0f);
            Sane(s); PoseRuns(s);
            var whistles = s.fx.Where(f => f.kind == CampFxKind.Whistle).OrderBy(f => f.time).ToList();
            Assert.AreEqual(2, whistles.Count, "it works (and once more after the laugh)");
            Assert.AreEqual(s.a, whistles[0].time, 1e-4f);
            int blows = s.parts[0].Count(p => p.pose == (CampPoseFn)CampScenes3.BlowPose && p.to <= s.a + 1e-3f);
            Assert.AreEqual(3, blows, "two duds, then it works");
            foreach (int m in new[] { 1, 2 })
            {
                var ears = s.parts[m].First(p => p.pose == (CampPoseFn)CampScenes3.EarsPose);
                Assert.Greater(ears.from, s.a, "they cover their ears after it works");
                Assert.AreEqual(0.8f, ears.Len, 1e-3f, "0.8 s");
                Assert.Less((ears.pos - Spot).magnitude, 0.25f, "#692 p3: they come near the whistler, into the frame");
                Assert.IsFalse(ears.keepSpot, "they stand where the scene puts them");
            }
            var e = DollPose.Rest; CampScenes3.EarsPose(ref e, 0.4f, 0.8f);
            Assert.AreEqual(150f, e.armL.x, 1f);
        }

        [Test]
        public void B34a_WaterASapling_SixDropsAWetPatchAndAPerk()
        {
            var sap = Spot;
            var s = CampScenes3.WaterSapling(1, sap + new Vector2(1f, 0f), sap, 0f);
            Sane(s); PoseRuns(s);
            Assert.AreEqual(6, s.fx.Count(f => f.kind == CampFxKind.Droplets), "6 droplets");
            var drops = s.fx.Where(f => f.kind == CampFxKind.Droplets).Select(f => f.time).ToList();
            Assert.LessOrEqual(drops.Max() - drops.Min(), 1f, "in 1 s");
            Assert.AreEqual(1, Marks(s, CampMark.WetPatch), "a wet patch");
            Assert.AreEqual(1, s.fx.Count(f => f.kind == CampFxKind.Perk), "the little tree perks up");
            var stand = s.parts[0].First(p => p.pose == (CampPoseFn)CampScenes3.PourPose).pos;
            Assert.AreEqual(0.08f, (stand - sap).magnitude, 1e-3f, "stops 0.08 u from it");
            Assert.IsTrue(s.parts[0].Any(p => p.prop == PropKind.Bucket) && s.parts[0].Any(p => p.prop == PropKind.BucketEmpty), "full, then empty");
        }

        [Test]
        public void B34b_RingASapling_SixStonesInTwoTrips_TheKidHops()
        {
            var sap = Spot;
            var s = CampScenes3.RingSapling(1, 2, sap + new Vector2(1f, 0f), sap + new Vector2(0.8f, 0.3f), sap, sap + new Vector2(-0.6f, 0.2f), 0f);
            Sane(s); PoseRuns(s);
            Assert.AreEqual(LandGeo.RingStones, Marks(s, CampMark.RingStone), "six stones");
            var at = s.fx.Where(f => f.kind == CampFxKind.Mark && f.count == (int)CampMark.RingStone).Select(f => new Vector2(f.at.x, f.at.z)).ToList();
            Assert.AreEqual(LandGeo.RingStones, at.Distinct().Count(), "each in its own place");
            foreach (var p in at) Assert.AreEqual(LandGeo.RingR, (p - sap).magnitude, 1e-3f, "r 0.06 round it");
            Assert.AreEqual(LandGeo.RingStones, s.fx.Count(f => f.kind == CampFxKind.Dust), "a dust puff at each");
            Assert.IsTrue(s.parts[1].Any(p => p.style == DollStyle.Hop), "the kid hops");
            Assert.IsTrue(s.tips.Any(t => t.kind == CampTipKind.Stones && t.member == 0) && s.tips.Any(t => t.kind == CampTipKind.Stone && t.member == 1), "3 in the arms, one in the hand");
            // #692 p3: round a young tree the ring lies outside its crown, where the camera sees it
            float wide = LandGeo.RingFor(0.13f);
            Assert.Greater(wide, 0.13f, "outside the crown"); Assert.AreEqual(LandGeo.RingR, LandGeo.RingFor(0.02f), 1e-5f, "a seedling keeps r 0.06");
            var w = CampScenes3.RingSapling(1, 2, sap + new Vector2(-0.6f, 0.2f), sap + new Vector2(0.5f, -0.3f), sap, sap + new Vector2(-0.5f, -0.4f), 0f, wide);
            Sane(w);
            foreach (var f in w.fx.Where(f => f.kind == CampFxKind.Mark && f.count == (int)CampMark.RingStone)) Assert.AreEqual(wide, (new Vector2(f.at.x, f.at.z) - sap).magnitude, 1e-3f);
        }

        [Test]
        public void B34_SaplingUses_StayClearOfTheGlass()
        {
            Assert.IsFalse(CampLife.ClearOfGlass(new Vector2(-1.42f, -0.28f)), "#692 p3: the clip's sapling, 1.9 u from the front pane");
            Assert.IsTrue(CampLife.ClearOfGlass(new Vector2(0f, AquariumCiv.Sim.Settle.Terrain.CZ)));
            Assert.IsFalse(CampLife.ClearOfGlass(new Vector2(AquariumCiv.Sim.Settle.Terrain.HX - 0.3f, AquariumCiv.Sim.Settle.Terrain.CZ)), "near a side pane");
        }

        [Test]
        public void B34c_PlantATwig_PushedInPattedWateredNodded()
        {
            var sap = Spot;
            var s = CampScenes3.PlantTwig(1, sap + new Vector2(0.7f, 0f), sap, sap + new Vector2(-0.4f, 0.2f), 0f);
            Sane(s); PoseRuns(s);
            Assert.AreEqual(1, Marks(s, CampMark.Twig), "the twig stands");
            var twig = s.fx.First(f => f.kind == CampFxKind.Mark);
            Assert.AreEqual(0.06f, (new Vector2(twig.at.x, twig.at.z) - sap).magnitude, 1e-3f, "0.06 u from the sapling");
            Assert.AreEqual(2, s.fx.Count(f => f.kind == CampFxKind.Droplets), "the bucket's last drops");
            Assert.IsTrue(s.parts[0].Any(p => p.prop == PropKind.Twig), "the twig in the hand");
            // #692 p3: with the camera's way, the kid crouches on the camera's side of the little tree
            var c = CampScenes3.PlantTwig(1, sap + new Vector2(0f, 0.7f), sap, sap + new Vector2(-0.4f, 0.2f), 0f, ToCam, 0.1f);
            Sane(c);
            var crouch = c.parts[0].First(p => p.pose == (CampPoseFn)CampScenes3.PushTwigPose).pos;
            Assert.Greater(Vector2.Dot(crouch - sap, ToCam), 0.1f, "in front of the tree as the camera sees it");
            var p0 = DollPose.Rest; CampScenes3.PushTwigPose(ref p0, 0.45f, 0.8f);
            Assert.AreEqual(35f, p0.lean, 1f);
        }

        [Test]
        public void B35a_SitOnARock_HigherThanTheGround_TheKidIsKingOfTheRock()
        {
            var r1 = Spot; var r2 = Spot + new Vector2(0.5f, 0.1f);
            var s = CampScenes3.SitRock(1, Spot + new Vector2(-0.8f, -0.3f), r1, ToCam, 10f, 2, Spot + new Vector2(0.9f, -0.5f), r2, 0f);
            Sane(s); PoseRuns(s);
            var sit = DollPose.Rest; CampScenes3.RockSitPose(ref sit, 1f, 10f);
            Assert.Greater(sit.offset.y, 0f, "sits higher than the ground");
            Assert.IsTrue(s.parts[0].Any(p => p.style == DollStyle.Sit && (p.pos - r1).magnitude < LandGeo.RockW * 0.5f && p.Len >= 10f), "on the boulder (#692 p3: on its top, to one side)");
            Assert.IsTrue(s.parts[1].Any(p => p.style == DollStyle.PoseBig && (p.pos - r2).magnitude < 1e-3f), "king of the rock on top");
            Assert.AreEqual(1, s.fx.Count(f => f.kind == CampFxKind.Dust), "the jump off lands in a dust puff");
            // #692 p3: one boulder shared: the sitter on one side, the kid on the other, both in the scene's frame
            var sh = CampScenes3.SitRock(1, Spot + new Vector2(-0.8f, -0.3f), r1, ToCam, 10f, 2, Spot + new Vector2(0.9f, -0.5f), r1, 0f);
            Sane(sh);
            var seat = sh.parts[0].First(p => p.style == DollStyle.Sit).pos; var king = sh.parts[1].First(p => p.style == DollStyle.PoseBig).pos;
            Assert.Greater((seat - king).magnitude, 0.06f, "side by side, not in each other");
            Assert.Less((sh.focus - r1).magnitude, 0.05f, "the camera on the boulder");
            var alone = CampScenes3.SitRock(VillageScenes.Nobody, default, r1, ToCam, 5f, 2, Spot + Vector2.left, r2, 0f);
            Sane(alone); Assert.AreEqual(1, alone.Count, "a kid alone");
        }

        [Test]
        public void B35b_StackPebbles_FiveSmallerAndSmaller_ACheerOrATopple()
        {
            var s = CampScenes3.StackPebbles(1, true, Spot + Vector2.left, Spot, LandGeo.RockH, false, 0f);
            Sane(s); PoseRuns(s);
            Assert.AreEqual(LandGeo.Pebbles, Marks(s, CampMark.Pebble), "five pebbles");
            Assert.AreEqual(0, Marks(s, CampMark.Topple));
            Assert.IsTrue(s.parts[0].Any(p => p.style == DollStyle.Cheer), "a cheer");
            var sizes = s.tips.Where(t => t.kind == CampTipKind.Pebble).Select(t => t.grow).ToList();
            for (int i = 1; i < sizes.Count; i++) Assert.Less(sizes[i], sizes[i - 1], "each smaller");
            var t2 = CampScenes3.StackPebbles(1, true, Spot + Vector2.left, Spot, 0f, true, 0f);
            Sane(t2);
            Assert.AreEqual(1, Marks(t2, CampMark.Topple), "it topples");
            Assert.Greater(t2.b, t2.a, "after the fifth");
            Assert.IsTrue(t2.parts[0].Any(p => p.style == DollStyle.Laugh), "they laugh");
        }

        [Test]
        public void B36a_StickFight_SnapToSizeEnGardeFourCrossesSticksUp()
        {
            var df = Spot;
            var s = CampScenes3.StickFight(1, 2, df + new Vector2(0.8f, 0f), df + new Vector2(-0.7f, 0.2f), df, ToCam, 0f);
            Sane(s); PoseRuns(s);
            Assert.AreEqual(CampScenes3.Strikes, s.fx.Count(f => f.kind == CampFxKind.Clash), "a flash and a knock at each cross");
            Assert.AreEqual(2, s.fx.Count(f => f.kind == CampFxKind.Chips), "each snaps a branch: chips");
            for (int m = 0; m < 2; m++)
            {
                Assert.IsTrue(s.parts[m].Any(p => p.prop == PropKind.Branch), "a fallen branch first");
                Assert.IsTrue(s.parts[m].Any(p => p.prop == PropKind.Twig), "then a stick");
                Assert.AreEqual(PropKind.None, s.parts[m].OrderBy(p => p.from).Last().prop, "dropped back on the deadfall");
            }
            var a0 = s.parts[0].First(p => p.from >= s.a - WoodsGame.Strike.ContactAt - 1e-3f).pos;
            var b0 = s.parts[1].First(p => p.from >= s.a - WoodsGame.Strike.ContactAt - 1e-3f).pos;
            Assert.AreEqual(CampScenes3.DuelGap, (a0 - b0).magnitude, 1e-3f, "the duel's gap");
        }

        [Test]
        public void B36b_StickHorse_GallopRearLeanAndPat()
        {
            var s = CampScenes3.StickHorse(1, Spot + new Vector2(0.9f, 0f), Spot + new Vector2(0.5f, 0.5f), Spot, Spot + new Vector2(-0.5f, -0.2f), 0f);
            Sane(s); PoseRuns(s);
            Assert.Greater(s.a, 0f, "it rears");
            Assert.Greater(s.b, s.a, "then leant");
            Assert.AreEqual(1, Marks(s, CampMark.Horse), "the horse stays leaning");
            var horse = s.tips.Single(t => t.kind == CampTipKind.Horse);
            Assert.AreEqual(s.b, horse.to, 1e-3f, "in the hand until it is leant");
            int gallop = s.parts[0].Count(p => p.pose == (CampPoseFn)CampScenes3.GallopPose);
            Assert.GreaterOrEqual(gallop, 18, "round the loop one and a half times");
            float lo = 9f, hi = -9f;
            for (float t = 0f; t < 1f; t += 0.01f) { var p = DollPose.Rest; CampScenes3.GallopPose(ref p, t, 5f); lo = Mathf.Min(lo, p.lean); hi = Mathf.Max(hi, p.lean); }
            Assert.AreEqual(12f, hi - lo, 1f, "the pitch rocks ±6°");
            var r = DollPose.Rest; CampScenes3.RearPose(ref r, 0.3f, 0.6f);
            Assert.Less(r.lean, -10f, "rears back");
        }
    }
}
