// #692 part 5: the rest of the camp's timelines (pure builders, CampScenes5 and 5b) and the swing's rules (CampGeo5) against the spec rows
// B40-B53 (EditMode, no scene).
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using static AquariumCiv.View.Tests.CampScenesTests;

namespace AquariumCiv.View.Tests
{
    public class CampScenes5Tests
    {
        static readonly Vector2 Spot = new Vector2(0.4f, 0.9f), ToCam = Vector2.down, Across = Vector2.right;
        static readonly Vector2 Far = Spot + new Vector2(0.9f, 0.3f), Far2 = Spot + new Vector2(-0.7f, 0.4f);

        static int Leaves(CampScene s, CampMark m) => s.fx.Count(f => f.kind == CampFxKind.Leave && f.count == (int)m);
        static void Ok(CampScene s) { Sane(s); PoseRuns(s); Assert.Greater(s.Length, 1f, s.use + " has a length"); }

        [Test]
        public void B40_Tents_PeekToyAndTheDoorway()
        {
            var peek = CampScenes5.TentPeek(1, Spot, Vector2.down, 0f);
            Ok(peek);
            Assert.AreEqual(2, peek.parts[0].Count(p => p.style == DollStyle.Hidden), "in the tent, then ducked back in");
            Assert.IsTrue(peek.parts[0].Any(p => p.run), "then runs out");
            var toy = CampScenes5.TentToy(1, Far, Spot + Vector2.right * 0.07f, Spot, false, 0f);
            Ok(toy);
            Assert.AreEqual(1, Leaves(toy, CampMark.Toy)); Assert.AreEqual(1, Leaves(toy, CampMark.ToySpin), "flicked so it spins");
            Assert.AreEqual(1, toy.fx.Count(f => f.kind == CampFxKind.Jolt), "the door flap jolts");
            Assert.AreEqual(1, Leaves(CampScenes5.TentToy(1, Far, Spot, Spot + Vector2.up * 0.1f, true, 0f), CampMark.Beads), "or beads");
            var sit = CampScenes5.TentDoorSit(1, 2, Far, Far2, Spot, Vector2.down, Spot + Vector2.down, 12f, 0f);
            Ok(sit);
            Assert.AreEqual(2, sit.Count, "a kid joins");
            var arm = DollPose.Rest; sit.parts[0].First(p => p.style == DollStyle.Sit).pose(ref arm, 6f, 12f);
            Assert.AreEqual(-70f, arm.armL.z, 2f, "an arm round the kid (z 70)");
            Assert.IsTrue(sit.parts[0].Any(p => p.style == DollStyle.Hidden), "at bedtime they go in");
        }

        [Test]
        public void B41_B42_CountSneakTendDuckAndTheFish()
        {
            var things = new List<Vector2> { Spot, Spot + Across * 0.04f, Spot + Across * 0.08f };
            var count = CampScenes5.CountThings(CampUse.CountStash, 1, 2, Far, Far2, things, ToCam, 0f);
            Ok(count);
            Assert.AreEqual(CampScenes5.CountPokes, Leaves(count, CampMark.StripTouch), "five pokes");
            Assert.IsTrue(count.parts[0].Any(p => p.style == DollStyle.Cheer) && count.parts[1].Any(p => p.style == DollStyle.Cheer), "so many! both cheer");
            var sneak = CampScenes5.SnackSneak(1, Far, 2, Far2, Spot, ToCam, 0f);
            Ok(sneak);
            Assert.IsTrue(sneak.parts[0].Any(p => p.style == DollStyle.Freeze), "caught: the freeze");
            Assert.AreEqual(1, sneak.tips.Count(t => t.kind == CampTipKind.Berry), "a berry pops into the hand");
            Assert.IsTrue(sneak.parts[1].Any(p => p.style == DollStyle.Laugh), "the grown-up laughs");
            var tend = CampScenes5.TendRacks(1, Far, things, Spot, ToCam, true, Spot + Vector2.left * 0.5f, Vector2.up, 0f);
            Ok(tend);
            Assert.AreEqual(things.Count, Leaves(tend, CampMark.StripTouch), "each hanging thing touched");
            Assert.IsTrue(tend.parts[0].Any(p => p.style == DollStyle.ScrapeHide), "the hide scraped");
            Assert.AreEqual(1, tend.fx.Count(f => f.kind == CampFxKind.Jolt), "tugged taut: the hide jolts");
            var duck = CampScenes5.DuckUnderHides(1, 2, Far, Far2, Spot, Across, 3, Spot + Vector2.down * 0.1f, 0f);
            Ok(duck);
            var d = DollPose.Rest; duck.parts[0].First(p => p.pose != null).pose(ref d, 0.15f, 0.3f);
            Assert.Less(d.squash, 0.75f, "the duck: squash 0.7");
            var fish = CampScenes5.SneakFish(1, Far, 2, Far2, Spot, ToCam, 3, Spot + Vector2.left, Spot + Vector2.left * 0.6f, 0f);
            Ok(fish);
            Assert.AreEqual(1, Leaves(fish, CampMark.FishTaken), "one fish off the rack");
            Assert.IsTrue(fish.parts[0].Any(p => p.prop == PropKind.Fish && p.run), "runs with the fish");
            Assert.IsTrue(fish.parts[2].Any(p => p.style == DollStyle.Taste), "shared behind a tent");
        }

        [Test]
        public void B43_B44_ThicketTreesAndBerryFaces()
        {
            var peek = CampScenes5.ThicketPeek(new[] { 1, 2 }, new[] { Far, Far2 }, Spot, Spot + Vector2.down * 0.06f, Vector2.up, 5f, 0f);
            Ok(peek);
            Assert.AreEqual(1, peek.fx.Count(f => f.kind == CampFxKind.BushSway), "the bush parted");
            var p = DollPose.Rest; peek.parts[0][2].pose(ref p, 1f, 5f);
            Assert.That(p.lean, Is.InRange(10f, 30f), "crouched at the bush, upright (the notes: lying flat read as falling)");
            Assert.Less(p.squash, 0.85f, "crouched");
            var wipe = CampScenes5.BerryWipe(1, Far, 2, Far2, Spot, ToCam, 0f);
            Ok(wipe);
            Assert.AreEqual(3, wipe.tips.Count(t => t.kind == CampTipKind.Berry), "three berries eaten");
            Assert.AreEqual(1, wipe.fx.Count(f => f.kind == CampFxKind.Leave && f.count == (int)CampMark.PurpleMouth && f.anchor == CampAnchor.Head), "a purple mouth");
            Assert.AreEqual(1, wipe.fx.Count(f => f.count == (int)CampMark.WipeMouth), "wiped with a thumb");
            Assert.Greater(wipe.b, wipe.a);
            var climb = CampScenes5.ClimbTree(1, Far, Spot, ToCam, 8f, 2, Far2, 0f);
            Ok(climb);
            Assert.AreEqual(2, climb.fx.Count(f => f.kind == CampFxKind.TreeShake), "the crown sways at the climb and the jump");
            var up = DollPose.Rest; climb.parts[0].First(x => x.style == DollStyle.Sit).pose(ref up, 1f, 8f);
            Assert.Greater(up.offset.y, 0.09f, "up on the branch (+0.09 u)");
            Assert.IsTrue(climb.parts[1].Any(x => x.style == DollStyle.Wave), "waves to a kid below (the kid waves back)");
            var lean = CampScenes5.TrunkLean(1, 2, Far, Far2, Spot, 0.035f, ToCam, 5f, 0f);
            Ok(lean);
            var l = DollPose.Rest; lean.parts[0][1].pose(ref l, 1f, 5f);
            Assert.AreEqual(-CampScenes5.LeanBack, l.lean, 0.5f, "the back on the bark (lean back 12, the notes)");
        }

        [Test]
        public void B45_B46_PondAndPuddles()
        {
            var skip = CampScenes5.SkipStones(1, Far, Spot, Vector2.up, Across, 3, 0f);
            Ok(skip);
            Assert.AreEqual(4, skip.fx.Count(f => f.kind == CampFxKind.WaterRing), "three skips and the sink");
            Assert.AreEqual(1, Leaves(skip, CampMark.SkipSplash), "frogs and ducks near flee");
            Assert.IsTrue(skip.parts[0].Any(x => x.style == DollStyle.Cheer), "three skips: a cheer");
            var rings = skip.fx.Where(f => f.kind == CampFxKind.WaterRing).Select(f => new Vector2(f.at.x, f.at.z)).ToList();
            Assert.AreEqual(0.15f, (rings[1] - rings[0]).magnitude, 1e-3f); Assert.AreEqual(0.12f, (rings[2] - rings[1]).magnitude, 1e-3f);
            var plop = CampScenes5.SkipStones(1, Far, Spot, Vector2.up, Across, 0, 0f);
            Ok(plop);
            Assert.AreEqual(1, plop.fx.Count(f => f.kind == CampFxKind.WaterRing), "a kid's plop: one ring");
            Assert.IsTrue(plop.parts[0].Any(x => x.style == DollStyle.Laugh));
            var boat = CampScenes5.LeafBoat(1, Far, Spot, Vector2.up, 0f);
            Ok(boat);
            Assert.AreEqual(1, Leaves(boat, CampMark.Boat));
            var b0 = CampGeo5.BoatAt(Spot, Vector2.up, 0f, 0.4f, out _); var b1 = CampGeo5.BoatAt(Spot, Vector2.up, 100f, 0.4f, out _);
            Assert.AreEqual(0.4f, (b1 - b0).magnitude, 0.03f, "drifts out its length, then stays");
            Assert.AreEqual(CampGeo5.BoatSpeed * 2f, (CampGeo5.BoatAt(Spot, Vector2.up, 2f, 0.4f, out _) - b0).magnitude, 0.01f, "0.04 u/s");
            var feet = CampScenes5.FeetInWater(1, 2, Far, Far2, Spot, Vector2.up, 10f, 0f);
            Ok(feet);
            Assert.GreaterOrEqual(feet.fx.Count(f => f.kind == CampFxKind.WaterRing), 8, "a ring at the feet every 2 s, each");
            var look = CampScenes5.PuddleLook(1, 2, Far, Far2, Spot, 0.08f, ToCam, 0f);
            Ok(look);
            Assert.IsTrue(look.parts[0].Any(x => x.style == DollStyle.HopBack), "hops back laughing");
            var race = CampScenes5.PuddleRace(new[] { 1, 2, 3 }, new[] { Far, Far2, Spot }, new[] { Spot, Spot + Across * 0.4f, Spot + Across * 0.8f }, 0f);
            Ok(race);
            Assert.AreEqual(9, race.fx.Count(f => f.kind == CampFxKind.Droplets), "everyone stomps at three puddles");
            Assert.IsTrue(race.parts.All(m => m.Any(x => x.run)), "they run");
        }

        [Test]
        public void B47_B48_B49_B50_ReedsStreamHillPaths()
        {
            var cut = CampScenes5b.CutReeds(1, Far, Spot, Vector2.up, Far2, Vector2.down, 0f);
            Ok(cut);
            Assert.AreEqual(1, Leaves(cut, CampMark.ReedsFlat), "the tuft flattens");
            Assert.IsTrue(cut.tips.Any(t => t.kind == CampTipKind.Reeds), "an armful of reeds");
            Assert.IsTrue(cut.parts[0].Any(x => x.style == DollStyle.SitWeave), "weaves at the tent");
            var wh = CampScenes5b.ReedWhistle(1, Far, Spot, new[] { 2 }, new[] { Far2 }, 0f, Across);
            Ok(wh);
            Assert.AreEqual(CampUse.ReedWhistle, wh.use);
            Assert.IsFalse(wh.tips.Any(t => t.kind == CampTipKind.Blade), "a reed, not a grass blade");
            var follow = CampScenes5b.FollowStream(new[] { 1, 2 }, new[] { Far, Far2 }, new[] { Spot, Spot + Across * 0.5f, Spot + Across + Vector2.up * 0.2f }, 0f);
            Ok(follow);
            Assert.Greater(follow.a, 0f, "a hop across");
            var dam = CampScenes5b.BuildDam(1, 2, Far, Far2, Spot, Across, Spot + Vector2.up * 0.2f, 0f);
            Ok(dam);
            Assert.AreEqual(5, Leaves(dam, CampMark.Pebble), "five stones across");
            Assert.AreEqual(1, Leaves(dam, CampMark.DamPool)); Assert.AreEqual(1, Leaves(dam, CampMark.DamBreak), "a stone pushed out: the gush");
            var dusk = CampScenes5b.HillDusk(new[] { 1, 2 }, new[] { Far, Far2 }, Spot, Spot + Vector2.down, 12f, 0f);
            Ok(dusk);
            Assert.IsTrue(dusk.parts.All(m => m.Any(x => x.style == DollStyle.Sit)), "they sit on the top");
            var roll = CampScenes5b.HillRoll(new[] { 1 }, new[] { Far }, Spot, Vector2.down, 7, 0f);
            Ok(roll);
            Assert.AreEqual(1 + 7, roll.parts[0].Count(x => x.pose != null && Mathf.Approximately(x.Len, CampScenes5b.RollEach)), "the flop and seven rolls of 0.25 s");
            var last = roll.parts[0].Where(x => Mathf.Approximately(x.Len, CampScenes5b.RollEach)).Last().pos;
            Assert.AreEqual(CampScenes5b.RollStep * 7, (last - Spot).magnitude, 1e-3f, "down the slope");
            var race = CampScenes5b.PathRace(new[] { 1, 2 }, new[] { Far, Far2 }, Spot, Spot + Across * 0.6f, false, 0f);
            Ok(race);
            Assert.IsTrue(race.parts.Any(m => m.Any(x => x.style == DollStyle.Cheer)), "the winner cheers");
            var hop = CampScenes5b.PathRace(new[] { 1, 2 }, new[] { Far, Far2 }, Spot, Spot + Across * 0.6f, true, 0f);
            Ok(hop);
            var chat = CampScenes5b.PathChat(1, 2, Far, Far2, Spot, Across, 5f, 0f);
            Ok(chat);
            Assert.IsTrue(chat.parts.All(m => m.Any(x => x.style == DollStyle.Talk && Mathf.Approximately(x.Len, 5f))), "they stop and chat");
        }

        [Test]
        public void B51_B52_B53_StoneLanternsWeedsCrown()
        {
            var gift = CampScenes5b.StoneGift(1, Far, 2, Far2, Spot, ToCam, 1, 0f);
            Ok(gift);
            Assert.AreEqual(1, Leaves(gift, CampMark.Gift), "a gift left at the foot");
            Assert.IsTrue(gift.parts[0].Any(x => x.style == DollStyle.Bow) && gift.parts[1].Any(x => x.style == DollStyle.Bow), "they bow");
            var poles = new[] { Spot, Spot + Across * 0.5f, Spot + Across + Vector2.up * 0.3f };
            var light = CampScenes5b.LightLanterns(1, Far, poles, ToCam, 0f);
            Ok(light);
            Assert.AreEqual(3, Leaves(light, CampMark.LanternLit), "each lantern lit");
            var lits = light.fx.Where(f => f.count == (int)CampMark.LanternLit).Select(f => f.time).ToList();
            for (int i = 1; i < lits.Count; i++) Assert.GreaterOrEqual(lits[i] - lits[i - 1], CampScenes5b.LightGap - 1e-3f, "1.5 s apart plus the walk");
            Assert.IsTrue(light.parts[0].All(x => x.prop == PropKind.Twig), "a burning stick");
            var moths = CampScenes5b.ChaseMoths(1, Far, Spot, ToCam, 3, 0f);
            Ok(moths);
            Assert.AreEqual(3, Leaves(moths, CampMark.MothClap), "three claps");
            var weed = CampScenes5b.PullWeed(1, Far, Spot, Spot + Across * 0.3f, 0f);
            Ok(weed);
            Assert.AreEqual(1, Leaves(weed, CampMark.WeedPulled), "the cell clears");
            Assert.IsTrue(weed.tips.Any(t => t.kind == CampTipKind.Weed), "a weed with its root in the hand");
            var crown = CampScenes5b.Crown(1, Far, 2, Far2, Spot, ToCam, 0f);
            Ok(crown);
            Assert.AreEqual(1, crown.fx.Count(f => f.count == (int)CampMark.Crown && f.anchor == CampAnchor.Head && f.member == 1), "on the other kid's head");
        }

        /// <summary>#692 p5 retakes: the tents' shown side, the shore, the climb's seat, the ropes, the racers' lanes and the nap.</summary>
        [Test]
        public void Retakes_ShownSideShoreSeatsLanesAndTheNap()
        {
            var c = new Vector2(0.5f, 0.5f); var toCam = CampGeo5.HomeToCam;
            Assert.AreEqual(1f, toCam.magnitude, 1e-4f);
            var d1 = c + toCam * 0.28f;
            CampGeo5.ShownSide(c, d1, toCam, toCam, 0.17f, out var s1, out _, out bool hem1);
            Assert.IsFalse(hem1, "a door facing the camera is the door"); Assert.AreEqual(d1, s1);
            var d2 = c - toCam * 0.28f;
            CampGeo5.ShownSide(c, d2, -toCam, toCam, 0.17f, out var s2, out var w2, out bool hem2);
            Assert.IsTrue(hem2, "a door facing away: the hem on the camera's side");
            Assert.AreEqual(CampGeo5.ShownDot, Vector2.Dot(w2, toCam), 0.01f, "turned just far enough to face the camera");
            Assert.AreEqual(0.17f, (s2 - c).magnitude, 1e-3f, "on the tent's wall");
            var side = new Vector2(-toCam.y, toCam.x);
            CampGeo5.ShownSide(c, c - side * 0.28f - toCam * 0.1f, -side - toCam * 0.4f, toCam, 0.17f, out _, out var w3, out _);
            Assert.Greater(Vector2.Dot(w3, -side), 0f, "turned the short way round");
            var pole = CampGeo5.PoleBeside(s2, w2, toCam);
            Assert.GreaterOrEqual(Vector2.Dot(pole - s2, toCam), 0f, "the pole on the camera's side of it");
            Assert.AreEqual(CampGeo5.PoleOff, (pole - s2).magnitude, 0.04f);
            System.Func<float, float, bool> wet = (x, z) => z > 1f;
            Assert.AreEqual(1f - CampGeo5.ShoreBack, CampGeo5.ToShore(new Vector2(0f, 0.6f), Vector2.up, wet).y, 0.011f, "walked down to the drawn water");
            Assert.AreEqual(Vector2.zero, CampGeo5.ToShore(Vector2.zero, Vector2.up, (x, z) => false), "no water near: the spot stays");
            Assert.That(CampScenes5.ClimbSeatFor(0.16f), Is.InRange(CampScenes5.ClimbMin, 0.26f)); // the notes: up a grown-up's height
            var climb = CampScenes5.ClimbTree(1, Far, Spot, ToCam, 8f, VillageScenes.Nobody, default, 0f, 0.12f, 0.7f);
            Ok(climb);
            var up = DollPose.Rest; climb.parts[0].First(x => x.style == DollStyle.Sit).pose(ref up, 1f, 8f);
            Assert.GreaterOrEqual(up.offset.y * 0.7f, 0.12f, "the seat in box units, after the kid's scale");
            Assert.GreaterOrEqual(CampGeo5.RopeTop(0.18f), CampGeo5.SeatH + CampGeo5.RopeLen, "the ropes reach at least the bough");
            var race = CampScenes5.PuddleRace(new[] { 1, 2, 3 }, new[] { Far, Far2, Spot }, new[] { Spot + Across * 0.4f }, 0f);
            Ok(race);
            var at = race.parts.Select(m => m.First(x => x.style == DollStyle.Stomp).pos).ToList();
            for (int i = 0; i < at.Count; i++) for (int j = i + 1; j < at.Count; j++) Assert.GreaterOrEqual((at[i] - at[j]).magnitude, CampScenes5.RaceLane - 1e-3f, "each in a lane");
            var nap = DollPose.Rest; CampScenes5.NapPose(99f)(ref nap, 3f, 9f);
            Assert.Greater(Mathf.Abs(nap.roll), 10f, "asleep: slumped to the side");
        }

        /// <summary>#692 p5 notes (the user, 2026-11-16): the gates, the swing's timing and the placements.</summary>
        [Test]
        public void Notes_GatesSwingTimingAndPlacements()
        {
            // B40.c and B52.a never play in the tent phase; everything else may (the moths among them, now at the fire)
            Assert.IsFalse(CampGeo5.AllowedNow(CampUse.TentDoorSit, true)); Assert.IsFalse(CampGeo5.AllowedNow(CampUse.LightLanterns, true));
            Assert.IsTrue(CampGeo5.AllowedNow(CampUse.TentDoorSit, false), "houses: fine");
            Assert.IsTrue(CampGeo5.AllowedNow(CampUse.ChaseMoths, true)); Assert.IsTrue(CampGeo5.AllowedNow(CampUse.TentToy, true));
            var fire = new Vector2(0.2f, 0.3f);
            Assert.AreEqual(CampGeo5.MothOut, (CampGeo5.MothCentre(fire, ToCam) - fire).magnitude, 1e-4f, "the moths at the fire's light");
            var moths = CampScenes5b.ChaseMoths(1, Far, CampGeo5.MothCentre(fire, ToCam), ToCam, 3, 0f);
            Ok(moths);
            Assert.Greater((moths.parts[0][1].pos - fire).magnitude, 0.2f, "the kid clear of the fire");
            // B53.d: the swing follows the ride under way from its first push, also when a later ride is listed
            var rides = new List<(float from, float len)> { (1f, 4.5f), (7f, 4.5f) };
            Assert.Greater(Mathf.Abs(CampGeo5.SwingThetaAt(rides, 2.5f)), 5f, "moving with the first rider");
            Assert.AreEqual(CampGeo5.SwingTheta(2.5f - 1f, 4.5f), CampGeo5.SwingThetaAt(rides, 2.5f), 1e-4f);
            Assert.AreEqual(CampGeo5.SwingTheta(1f, 4.5f), CampGeo5.SwingThetaAt(rides, 8f), 1e-4f, "then the second");
            Assert.AreEqual(0f, CampGeo5.SwingThetaAt(rides, 0.5f), "still before the first push");
            // B40.b: the toy hangs on the canvas of the shown side
            var c = new Vector2(0.5f, 0.5f);
            var hang = CampGeo5.HangPoint(c, Vector2.down, 0.17f, 0.5f);
            Assert.AreEqual(CampGeo5.CanvasR(0.17f, 0.5f, CampGeo5.ToyH) + CampGeo5.ToyOff, (hang - c).magnitude, 1e-4f);
            Assert.Less(CampGeo5.CanvasR(0.17f, 0.5f, CampGeo5.ToyH), 0.17f, "the cone narrows up");
            // B44.a: up a grown-up's height, out under the crown's edge
            Assert.GreaterOrEqual(CampScenes5.ClimbSeatFor(0.12f), CampScenes5.ClimbMin);
            Assert.That(CampScenes5.ClimbOutFor(0.16f), Is.InRange(0.17f, 0.26f));
            var climb = CampScenes5.ClimbTree(1, Far, Spot, ToCam, 8f, VillageScenes.Nobody, default, 0f, 0.2f, 0.7f, 0.2f);
            Ok(climb);
            var sit = climb.parts[0].First(x => x.style == DollStyle.Sit);
            Assert.AreEqual(0.2f, (sit.pos - Spot).magnitude, 1e-3f, "the seat out along the bough");
            var up = DollPose.Rest; sit.pose(ref up, 1f, 8f);
            Assert.GreaterOrEqual(up.offset.y * 0.7f, 0.2f, "the kid up at the seat");
            // B44.c: the leaner's back on the trunk's face
            float face = CampGeo5.TrunkFaceAt(1f, 0.11f);
            Assert.That(face, Is.InRange(0.03f, 0.04f));
            var lean = CampScenes5.TrunkLean(1, 2, Far, Far2, Spot, face, ToCam, 5f, 0f);
            Assert.AreEqual(face + CampScenes5.LeanGap, (lean.parts[0][0].pos - Spot).magnitude, 1e-3f, "by the trunk's real face");
            Assert.Greater(Vector2.Dot(lean.parts[0][0].face - lean.parts[0][0].pos, lean.parts[0][0].pos - Spot), 0f, "facing away: the back to the bark");
            // B46.a: at the puddle's rim, never flat
            var look = CampScenes5.PuddleLook(1, VillageScenes.Nobody, Far, default, Spot, 0.08f, ToCam, 0f);
            Assert.GreaterOrEqual((look.parts[0][0].pos - Spot).magnitude, 0.08f * CampScenes5.PuddleRim, "outside the drawn puddle");
            foreach (var part in look.parts[0]) if (part.pose != null) { var q = DollPose.Rest; part.pose(ref q, 1f, part.Len); Assert.LessOrEqual(q.lean, 40f, "bent over, not flat"); Assert.GreaterOrEqual(q.offset.y, -0.02f, "not sunk"); }
            // B43.b: upright the whole time
            var peek = CampScenes5.ThicketPeek(new[] { 1, 2 }, new[] { Far, Far2 }, Spot, Spot + Vector2.down * 0.06f, Vector2.up, 5f, 0f, ToCam);
            foreach (var m in peek.parts) foreach (var part in m) if (part.pose != null) for (float u = 0f; u <= part.Len; u += 0.1f) { var q = DollPose.Rest; part.pose(ref q, u, part.Len); Assert.LessOrEqual(q.lean, 30f, "never lying down"); }
            // B51: outside the stone
            var gift = CampScenes5b.StoneGift(1, Far, VillageScenes.Nobody, default, Spot, ToCam, 1, 0f, 0.09f);
            Assert.GreaterOrEqual((gift.parts[0][0].pos - Spot).magnitude, 0.09f + 0.06f, "kneels outside the stone");
        }

        [Test]
        public void B53d_TheSwing_RulesHangingAndRiding()
        {
            Assert.IsFalse(CampGeo5.SwingTreeOk(true, true, 0.5f), "never the Keeper's tree");
            Assert.IsFalse(CampGeo5.SwingTreeOk(false, false, 0.5f), "a grown tree only");
            Assert.IsFalse(CampGeo5.SwingTreeOk(false, true, 2f), "within 1.5 u of the fire");
            Assert.IsTrue(CampGeo5.SwingTreeOk(false, true, 1.2f));
            var fire = Vector2.zero;
            Assert.IsTrue(CampGeo5.SwingSpotOk(new Vector2(1f, 0f), Vector2.up, fire, p => false));
            Assert.IsFalse(CampGeo5.SwingSpotOk(new Vector2(1f, 0f), Vector2.up, fire, p => p.y > 0.05f), "never over water or a path, nor jumping onto it");
            Assert.IsFalse(CampGeo5.SwingSpotOk(new Vector2(0.2f, 0f), Vector2.up, fire, p => false), "never over the fire");
            Assert.AreEqual(0.5f + 2f * 4f, CampGeo5.RideLength(8f), 1e-4f, "a ride ends at the top of a front swing");
            float ride = CampGeo5.RideLength(8f);
            Assert.AreEqual(CampGeo5.ArcDeg, CampGeo5.SwingTheta(ride, ride), 0.5f, "the jump at the front top");
            float max = 0f; for (float u = 0f; u <= ride; u += 0.01f) max = Mathf.Max(max, Mathf.Abs(CampGeo5.SwingTheta(u, ride)));
            Assert.LessOrEqual(max, CampGeo5.ArcDeg + 1e-3f, "±25 deg");
            Assert.Less(Mathf.Abs(CampGeo5.SwingTheta(ride + 6f, ride)), 2f, "the empty swing rocks to a stop");
            var hang = CampScenes5b.HangSwing(1, Far, Spot, Spot + Vector2.up * 0.1f, Across, true, 0f);
            Ok(hang);
            Assert.AreEqual(1, Leaves(hang, CampMark.SwingRope)); Assert.AreEqual(1, Leaves(hang, CampMark.SwingHung), "hung once");
            Assert.AreEqual(2, hang.fx.Count(f => f.kind == CampFxKind.Dust), "a miss, then the throw over");
            Assert.IsTrue(hang.tips.Any(t => t.kind == CampTipKind.Rope) && hang.tips.Any(t => t.kind == CampTipKind.Plank));
            var rides = new List<CampScenes5b.Ride>();
            var sw = CampScenes5b.Swing(1, Far, 2, Far2, Spot, Across, 8f, 0.7f, rides, 0f);
            Ok(sw);
            Assert.AreEqual(2, rides.Count, "they swap");
            var lone = new List<CampScenes5b.Ride>();
            var one = CampScenes5b.Swing(1, Far, VillageScenes.Nobody, default, Spot, Across, 8f, 0.7f, lone, 0f);
            Ok(one);
            Assert.AreEqual(1, lone.Count);
            Assert.Greater(one.fx.Count(f => f.kind == CampFxKind.Creak), 4, "a creak at each end of the arc");
            Assert.AreEqual(1, one.fx.Count(f => f.kind == CampFxKind.Dust), "the jump-off lands");
            // the rider is carried round the arc: forward at the front end
            var pr = one.parts[0].First(x => x.style == DollStyle.Sit && x.Len > 2f);
            var p = DollPose.Rest; pr.pose(ref p, 2.5f, pr.Len);
            Assert.Less(p.offset.z, -0.05f, "forward (offset −z) at a front end");
            Assert.Greater(p.offset.y, CampGeo5.SeatH, "lifted on the arc");
        }
    }
}
