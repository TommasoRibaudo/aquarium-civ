// #693: the choice props' timelines (pure builders, CampScenes6) against the spec rows B54.a-zz (EditMode, no scene), and the props' looks
// (ConsequenceMeshes.Build's variants).
using System.Collections.Generic;
using System.Linq;
using AquariumCiv.Sim;
using NUnit.Framework;
using UnityEngine;
using static AquariumCiv.View.Tests.CampScenesTests;

namespace AquariumCiv.View.Tests
{
    public class CampScenes6Tests
    {
        static readonly Vector2 Spot = new Vector2(0.4f, 0.9f), ToCam = Vector2.down, Across = Vector2.right;
        static readonly Vector2 Far = Spot + new Vector2(0.9f, 0.3f), Far2 = Spot + new Vector2(-0.7f, 0.4f), Far3 = Spot + new Vector2(0.2f, 1.1f);

        static int PropFxs(CampScene s, PropFx what) => s.fx.Count(f => f.kind == CampFxKind.Prop && f.count == (int)what);
        static IEnumerable<float> Looks(CampScene s) => s.fx.Where(f => f.kind == CampFxKind.Prop && f.count == (int)PropFx.Look).OrderBy(f => f.time).Select(f => f.scale);
        static void Ok(CampScene s) { Sane(s); PoseRuns(s); Assert.Greater(s.Length, 1f, s.use + " has a length"); Assert.Greater(s.a, 0f, s.use + " has its hit"); }
        static DollPose PoseAt(CampPart p, float into) { var d = DollPose.Rest; p.pose?.Invoke(ref d, into, p.Len); return d; }

        [Test]
        public void B54ab_Baskets_TakeAndBringBack_AndTheTower()
        {
            var take = CampScenes6.BasketTake(1, Far, Spot, Spot + new Vector2(0.5f, -0.4f), ToCam, 0f);
            Ok(take);
            CollectionAssert.AreEqual(new[] { 1f, 0f }, Looks(take).ToArray(), "one off the stack, then back");
            Assert.IsTrue(take.parts[0].Any(p => p.style == DollStyle.PickBerry && p.prop == PropKind.Basket), "picks with the basket");
            Assert.GreaterOrEqual(PropFxs(take, PropFx.Jolt), 2, "the stack jolts on the lift and the set-down");
            var tower = CampScenes6.BasketTower(1, 2, Far, Far2, Spot, ToCam, 0f, out var tw);
            Ok(tower);
            CollectionAssert.AreEqual(new[] { 1f, 2f, 3f, 0f }, Looks(tower).ToArray(), "three taken one by one, then all back");
            for (int i = 1; i < 3; i++) Assert.Greater(tw.set[i], tw.set[i - 1], "stacked one on the other, in turn");
            Assert.Greater(tw.fall, tw.set[2] + 1f, "it wobbles before it falls");
            Assert.IsTrue(CampScenes6.TowerBasket(tw, 2, tw.set[2] + 0.2f, Spot, Across, out var top, out _, out bool carried) && !carried);
            Assert.IsTrue(CampScenes6.TowerBasket(tw, 0, tw.set[2] + 0.2f, Spot, Across, out var bottom, out _, out _));
            Assert.Greater(top.y, bottom.y + 0.1f, "the third sits two baskets up");
            CampScenes6.TowerBasket(tw, 2, tw.fall + 1f, Spot, Across, out var fallen, out float roll, out _);
            Assert.Less(fallen.y, 0.05f, "fallen to the ground"); Assert.Greater(roll, 80f, "on its side");
            Assert.IsFalse(CampScenes6.TowerBasket(tw, 1, tw.back + 0.1f, Spot, Across, out _, out _, out _), "back on the stack");
            var arm = PoseAt(tower.parts[0].First(p => p.from >= tw.set[2] - 0.5f && p.from < tw.set[2]), 0.4f);
            Assert.Greater(arm.armR.x, 130f, "the third on tiptoe, arms up");
            Assert.IsTrue(tower.parts[0].Any(p => p.style == DollStyle.Laugh) && tower.parts[1].Any(p => p.style == DollStyle.Laugh), "they laugh");
        }

        [Test]
        public void B54cd_Woodpile_StackFetchAndShelter()
        {
            var stack = CampScenes6.WoodStack(1, Far, Spot, ToCam, false, Spot + Vector2.left, 0f);
            Ok(stack);
            Assert.AreEqual(0, stack.kind, "carries a log in (drawn 1.6x by CampLife, #693 retakes)"); Assert.Greater(PoseAt(stack.parts[0][0], 0.5f).armL.x, 60f, "in both arms");
            Assert.AreEqual(1, PropFxs(stack, PropFx.Jolt), "the pile jolts when it slides on");
            var fetch = CampScenes6.WoodStack(1, Far, Spot, ToCam, true, Spot + Vector2.left, 0f);
            Ok(fetch);
            Assert.IsTrue(fetch.parts[0].Any(p => p.style == DollStyle.FeedFire), "a wet day: one goes on the fire");
            Assert.AreEqual(1, fetch.fx.Count(f => f.kind == CampFxKind.Sparks));
            var rain = CampScenes6.WoodShelter(new[] { 1, 2, 3 }, new[] { Far, Far2, Far3 }, Spot, ToCam, true, 0f);
            Ok(rain);
            Assert.IsTrue(rain.parts.All(l => l[0].run), "they hurry in");
            var a = rain.parts[0].First(p => p.pose != null).pos; var b = rain.parts[1].First(p => p.pose != null).pos;
            Assert.AreEqual(0.06f, (a - b).magnitude, 0.005f, "pressed together, 0.06 u apart");
            Assert.Less(PoseAt(rain.parts[0].First(p => p.pose != null), 1f).squash, 0.97f, "shoulders up");
            Assert.GreaterOrEqual(rain.fx.Count(f => f.kind == CampFxKind.Droplets), 6, "drops off the roof");
            var dry = CampScenes6.WoodShelter(new[] { 1, 2 }, new[] { Far, Far2 }, Spot, ToCam, false, 0f);
            Ok(dry);
            Assert.IsTrue(dry.parts.All(l => l.Any(p => p.style == DollStyle.Sit)), "a dry day: kids sit on the logs");
        }

        [Test]
        public void B54efg_NameStoneAndCharms()
        {
            var cut = CampScenes6.NameCut(1, Far, Spot, ToCam, 0f);
            Ok(cut);
            Assert.AreEqual(12, cut.fx.Count(f => f.kind == CampFxKind.Chips), "a tap every 0.5 s for 6 s");
            Assert.AreEqual(1, PropFxs(cut, PropFx.NameRow), "a new row shows");
            var trace = CampScenes6.NameTrace(1, Far, 2, Far2, Spot, ToCam, 0f);
            Ok(trace);
            var tr = trace.parts[0].Where(p => p.pose != null).ToList();
            Assert.GreaterOrEqual(tr.Count, 3, "two traces and the rub");
            float z0 = PoseAt(tr[0], 0.15f).armR.z, z1 = PoseAt(tr[0], 1.05f).armR.z;
            Assert.Greater(Mathf.Abs(z1 - z0), 25f, "the finger moves along the row");
            Assert.IsTrue(trace.parts[1].Any(p => p.pose != null), "a grown-up nods");
            var flick = CampScenes6.CharmFlick(1, Far, Spot, ToCam, 1, 0f);
            Ok(flick);
            var jump = PoseAt(flick.parts[0].First(p => p.pose != null), 0.3f);
            Assert.Greater(jump.offset.y, 0.05f, "a hop of 0.06 u"); Assert.Greater(jump.armR.x, 150f, "arm x 170");
            Assert.AreEqual(1, PropFxs(flick, PropFx.Jolt), "the charm spins");
        }

        [Test]
        public void B54hi_BigPot_StirAndDrum()
        {
            var stir = CampScenes6.BigPotStir(1, Far, Spot, ToCam, 0f);
            Ok(stir);
            Assert.IsTrue(stir.parts[0].Any(p => p.style == DollStyle.Stir), "the cook stirs the big pot");
            var drum = CampScenes6.PotDrum(new[] { 1, 2 }, new[] { Far, Far2 }, 3, Far3, Spot, ToCam, 3.5f, 0f);
            Ok(drum);
            Assert.GreaterOrEqual(PropFxs(drum, PropFx.Jolt), 12, "a beat every 0.25 s");
            Assert.IsTrue(drum.parts[0].Any(p => p.prop == PropKind.Twig && p.style == DollStyle.Sit), "sitting with sticks");
            Assert.IsTrue(drum.parts[2].Any(p => p.pose != null), "a grown-up's hand over the pot");
        }

        [Test]
        public void B54jk_AxeStump_SplitAndSit()
        {
            var split = CampScenes6.StumpSplit(1, Far, Spot, ToCam, 0f);
            Ok(split);
            CollectionAssert.AreEqual(new[] { 1f, 0f }, Looks(split).ToArray(), "the axe out, then back in");
            Assert.IsTrue(split.parts[0].Any(p => p.style == DollStyle.Chop && p.prop == PropKind.Axe), "the split");
            Assert.Greater(split.a, split.b, "the log is set before it is split");
            var sit = CampScenes6.StumpSit(1, Far, 2, Far2, Spot, ToCam, 6f, 0f);
            Ok(sit);
            var p0 = PoseAt(sit.parts[0].First(p => p.style == DollStyle.Sit), 0.25f);
            Assert.Greater(p0.offset.y, PropGeo.StumpTop * 0.7f, "up on the stump"); Assert.AreEqual(5f, Mathf.Abs(p0.roll), 0.5f, "swinging, roll 5");
            Assert.AreEqual(2, sit.Count, "a friend on the other edge");
        }

        [Test]
        public void B54lm_Staff_TheWalkRoundAndPlayHeadman()
        {
            var round = new List<Vector2> { Spot + Vector2.right * 0.5f, Spot + Vector2.up * 0.5f, Spot + Vector2.left * 0.3f };
            var walk = CampScenes6.StaffWalk(1, Far, Spot, round, ToCam, 0f);
            Ok(walk);
            CollectionAssert.AreEqual(new[] { 1f, 0f }, Looks(walk).ToArray(), "taken up, put back");
            Assert.GreaterOrEqual(walk.parts[0].Count(p => p.poseWalking && p.from >= walk.c && p.to <= walk.d), round.Count, "walks round with it planted (the staff drawn 1.6x between c and d)"); Assert.Greater(walk.d, walk.c);
            var play = CampScenes6.StaffPlay(1, Far, new[] { 2, 3 }, new[] { Far2, Far3 }, 4, Far + Vector2.right, Spot, ToCam, 0f);
            Ok(play);
            Assert.IsTrue(play.parts[1].Any(p => p.style == DollStyle.Bow) && play.parts[2].Any(p => p.style == DollStyle.Bow), "the others bow");
            Assert.Greater(play.parts[2].First(p => p.style == DollStyle.Bow).from, play.parts[1].First(p => p.style == DollStyle.Bow).from, "in a wave");
            Assert.IsTrue(play.parts[3].Any(p => p.style == DollStyle.Laugh), "the headman laughs");
            Assert.IsTrue(play.parts[0].Any(p => p.style == DollStyle.Freeze), "caught");
            CollectionAssert.AreEqual(new[] { 1f, 0f }, Looks(play).ToArray(), "put back fast");
        }

        [Test]
        public void B54no_Flour_CarryAndTheFight()
        {
            var carry = CampScenes6.FlourCarry(1, Far, Spot, Spot + new Vector2(1f, 0.5f), ToCam, 0f);
            Ok(carry);
            Assert.IsTrue(carry.parts[0].Any(p => p.prop == PropKind.Sack && p.slot == PropSlot.Shoulder), "the sack on the shoulder");
            Assert.AreEqual(1, carry.fx.Count(f => f.kind == CampFxKind.Dust), "set down: a white puff");
            var fight = CampScenes6.FlourFight(new[] { 1, 2, 3, 4 }, new[] { Far, Far2, Far3, Far + Vector2.up }, Spot, ToCam, 0f);
            Ok(fight);
            Assert.AreEqual(5, fight.arcs.Count, "a handful thrown by turns");
            Assert.AreEqual(5, PropFxs(fight, PropFx.Flour), "each hit leaves flour on the one it hits");
            Assert.IsTrue(fight.parts.All(l => l.Any(p => p.run)), "then they chase about");
        }

        [Test]
        public void B54pq_RoofBird_StraightenAndFly()
        {
            var fix = CampScenes6.BirdStraighten(1, Far, Spot, ToCam, 0f);
            Ok(fix);
            Assert.AreEqual(1, PropFxs(fix, PropFx.Upright));
            Assert.Greater(fix.d, fix.a, "the ladder stands until after the bird is set");
            Assert.Greater(fix.a, fix.b, "the ladder is up before the climb");
            var up = PoseAt(fix.parts[0].First(p => p.from >= fix.a - 0.5f && p.from < fix.a), 0.4f);
            Assert.AreEqual(CampScenes6.ClimbH, up.offset.y, 0.005f, "up the ladder (#693 retakes: to the bird)"); Assert.Greater(up.armR.x, 140f);
            var fly = CampScenes6.BirdFly(new[] { 1, 2 }, new[] { Far, Far2 }, Spot, ToCam, 2, 0f);
            Ok(fly);
            var flap = PoseAt(fly.parts[0].First(p => p.run), 0.08f);
            Assert.Greater(Mathf.Abs(flap.armL.z), 59f, "arms out, flapping");
            foreach (var p in fly.parts[0].Where(p => p.run)) Assert.AreEqual(0.17f, (p.pos - Spot).magnitude, 0.01f, "a circle round the pole");
        }

        [Test]
        public void B54rs_LongTable_MealUnderAndTheNap()
        {
            var seats = new List<Vector2> { Spot + new Vector2(-0.1f, -0.14f), Spot + new Vector2(-0.1f, 0.14f), Spot + new Vector2(0.1f, -0.14f), Spot + new Vector2(0.1f, 0.14f) };
            var faces = seats.Select(x => new Vector2(x.x, Spot.y)).ToList();
            var meal = CampScenes6.TableMeal(5, Far, new[] { 1, 2, 3, 4 }, new[] { Far, Far2, Far3, Far }, seats, faces, Spot, Spot + Vector2.right * 0.45f, ToCam, 0f);
            Ok(meal);
            for (int m = 1; m <= 4; m++) Assert.IsTrue(meal.parts[m].Any(p => p.style == DollStyle.EatMeal && p.prop == PropKind.Bowl), "everyone eats from a bowl");
            Assert.AreEqual(4, meal.parts[0].Count(p => p.prop == PropKind.Bowl && p.pose != null), "a bowl handed to each");
            var under = CampScenes6.TableUnder(1, 2, Far, Far2, Spot + Vector2.left * 0.36f, Spot + Vector2.right * 0.36f, 3, Far3, Spot + Vector2.down * 0.13f, Vector2.right, ToCam, 0f);
            Ok(under);
            var crawl = PoseAt(under.parts[0].First(p => p.poseWalking), 0.5f);
            Assert.Less(crawl.squash, 0.8f, "low"); Assert.Greater(crawl.lean, 40f, "on hands and knees (#693 retakes)");
            Assert.AreEqual(0.15f, under.parts[0].First(p => p.poseWalking).speed, 1e-3f, "0.15 u/s");
            Assert.IsTrue(under.parts[2].Any(p => p.style == DollStyle.Lie && p.Len >= 6f), "the old one naps on the bench, 6 s");
        }

        [Test]
        public void B54tu_Offering_FillAndWait()
        {
            var fill = CampScenes6.OfferingFill(1, Far, Spot, ToCam, 0f);
            Ok(fill);
            CollectionAssert.AreEqual(new[] { 2f, 3f, 1f }, Looks(fill).ToArray(), "the cloth lifted, the food in, the cloth back over a fuller heap");
            Assert.IsTrue(fill.parts[0].Any(p => p.style == DollStyle.TipBasket) && fill.parts[0].Any(p => p.style == DollStyle.Bow), "tips it in, bows to the glass");
            Assert.AreEqual(1, PropFxs(fill, PropFx.Offered));
            var wait = CampScenes6.OfferingWait(new[] { 1, 2, 3 }, new[] { Far, Far2, Far3 }, Spot, ToCam, 0f);
            Ok(wait);
            var chin = PoseAt(wait.parts[1].First(p => p.style == DollStyle.Sit), 1f);
            Assert.AreEqual(120f, chin.armL.x, 1f, "chins in hands");
            CollectionAssert.AreEqual(new[] { 2f, 1f }, Looks(wait).ToArray(), "one lifts the cloth to check, still full");
            foreach (var l in wait.parts) { var sp = l.First(p => p.style == DollStyle.Sit); Assert.Greater(Vector2.Dot((sp.face - sp.pos).normalized, ToCam), 0.9f, "facing the glass (the camera)"); }
        }

        [Test]
        public void B54xy_FishCrate_ToTheRackAndTheWave()
        {
            var crate = CampScenes6.CrateFish(1, Far, Spot, Spot + Vector2.left * 0.6f, Spot + Vector2.left * 0.7f, ToCam, 0f);
            Ok(crate);
            CollectionAssert.AreEqual(new[] { 1f, 2f, 3f }, Looks(crate).ToArray(), "the fish lifted out one by one");
            Assert.IsTrue(crate.parts[0].Any(p => p.style == DollStyle.Hang), "hung on the rack");
            var wave = CampScenes6.CrateWave(new[] { 1, 2 }, new[] { Far, Far2 }, Spot, Spot + Vector2.up * 2f, ToCam, 0f);
            Ok(wave);
            Assert.AreEqual(2, wave.parts[0].Count(p => p.style == DollStyle.Wave), "waves big, twice");
            var w = PoseAt(wave.parts[0].First(p => p.style == DollStyle.Wave), 0.2f);
            Assert.Greater(w.armL.x, 150f); Assert.Greater(w.armR.x, 150f); Assert.Greater(w.offset.y, PropGeo.CrateTop * 0.9f, "standing on the crate");
        }

        [Test]
        public void B54z_zz_NamePeg_ReTieAndTheTap()
        {
            var tie = CampScenes6.PegRetie(1, Far, Spot, ToCam, 0f);
            Ok(tie);
            Assert.AreEqual(2, PropFxs(tie, PropFx.Jolt), "untied, tied on again");
            var tap = CampScenes6.PegTap(1, Spot + Vector2.left * 0.6f, Spot, Spot + Vector2.right * 0.7f, ToCam, 0f);
            Ok(tap);
            Assert.IsTrue(tap.parts[0].All(p => p.run), "without stopping");
            Assert.AreEqual(1, PropFxs(tap, PropFx.Swing), "the peg swings");
            Assert.Less(tap.Length, 6f, "a tap in passing");
        }

        [Test]
        public void B54_Looks_EachVariantBuilds()
        {
            foreach (var (kind, max) in new[] { (ConsequenceKind.Baskets, 3), (ConsequenceKind.AxeStump, 1), (ConsequenceKind.Staff, 1), (ConsequenceKind.NameStone, 3), (ConsequenceKind.OfferingBasket, 3), (ConsequenceKind.FishCrate, 3) })
            {
                var m0 = ConsequenceMeshes.Build(kind); int v0 = m0.vertexCount; var b0 = m0.bounds.size;
                for (int v = 1; v <= max; v++)
                {
                    var m = ConsequenceMeshes.Build(kind, v);
                    if (!(kind == ConsequenceKind.Baskets && v == 3)) Assert.Greater(m.vertexCount, 0, $"{kind} variant {v}"); // all three taken: nothing left
                    Assert.IsTrue(m.vertexCount != v0 || (m.bounds.size - b0).magnitude > 1e-3f, $"{kind} variant {v} looks different");
                    Object.DestroyImmediate(m);
                }
            }
            Assert.Less(ConsequenceMeshes.Build(ConsequenceKind.Baskets, 1).vertexCount, ConsequenceMeshes.Build(ConsequenceKind.Baskets).vertexCount, "a basket fewer");
            Assert.Greater(ConsequenceMeshes.Build(ConsequenceKind.NameStone, 1).vertexCount, ConsequenceMeshes.Build(ConsequenceKind.NameStone).vertexCount, "a row more");
            Assert.AreEqual(CampUse.PegTap, System.Enum.GetValues(typeof(CampUse)).Cast<CampUse>().Max(), "#693's uses are the last");
            foreach (CampUse u in System.Enum.GetValues(typeof(CampUse))) if (u >= CampUse.BasketTake) Assert.IsNotEmpty(CampLife.KindFor(u), u + " has its thing");
        }
    }
}
