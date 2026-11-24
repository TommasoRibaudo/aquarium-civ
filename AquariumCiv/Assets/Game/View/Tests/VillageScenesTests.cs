// #695 part 1 (B57.a-j, B58.c/f/g/j/k): the village's and the town's uses as pure timelines. Each test builds a scene from fixed spots and
// checks what the spec says can be read and what must never happen: kids run on the furrows only, nobody stands in the well's rim, the
// scarecrow is made in order, the ring keeps round the sheaf and ends in a jump, the log walk keeps to the log, the hopscotch hops along
// its grid, the fence fixer kneels outside the field, the kids on the fence sit on its top rail, the crust goes from the baker to a kid
// before it is shared.
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace AquariumCiv.View.Tests
{
    public class VillageScenesTests
    {
        static List<Vector2> Ats(int n, Vector2 from) { var r = new List<Vector2>(); for (int i = 0; i < n; i++) r.Add(from + new Vector2(0.15f * i, -0.6f)); return r; }

        static IEnumerable<CampPart> All(CampScene s) => s.parts.SelectMany(l => l);

        static float Pose(CampPart p, float u, System.Func<DollPose, float> pick)
        {
            var d = new DollPose { armL = Vector3.zero, armR = Vector3.zero, squash = 1f };
            p.pose?.Invoke(ref d, u, p.Len);
            return pick(d);
        }

        static void TimesInOrder(CampScene s)
        {
            Assert.Greater(s.Length, 1f, "it lasts");
            for (int m = 0; m < s.Count; m++)
            {
                var l = s.parts[m].OrderBy(p => p.from).ToList();
                for (int i = 0; i + 1 < l.Count; i++) Assert.LessOrEqual(l[i].to, l[i + 1].from + 1e-3f, $"member {m}: part {i} overlaps the next");
                foreach (var p in l) Assert.Greater(p.to, p.from, "a part with length");
            }
        }

        static IEnumerable<VillageFx> Fx(CampScene s) => s.fx.Where(f => f.kind == CampFxKind.Village).OrderBy(f => f.time).Select(f => (VillageFx)f.count);
        static float FxAt(CampScene s, VillageFx what) => s.fx.First(f => f.kind == CampFxKind.Village && (VillageFx)f.count == what).time;

        [Test]
        public void FieldTag_KidsRunOnTheFurrowsOnly_TheFarmerWavesThemOut()
        {
            var rows = new List<(Vector2 a, Vector2 b)> { (new Vector2(0f, 0f), new Vector2(0f, 0.5f)), (new Vector2(0.08f, 0f), new Vector2(0.08f, 0.5f)), (new Vector2(0.16f, 0f), new Vector2(0.16f, 0.5f)) };
            Vector2 gOut = new Vector2(0.08f, -0.2f), gIn = new Vector2(0.08f, -0.05f);
            var s = VillageScenes.FieldTag(new[] { 1, 2, 3 }, Ats(3, gOut), rows, gOut, gIn, 9, new Vector2(0.3f, 0.3f), 0f);
            Assert.AreEqual(CampUse.FieldTag, s.use);
            TimesInOrder(s);
            for (int m = 0; m < 3; m++)
                foreach (var p in s.parts[m])
                {
                    if (p.pos.y < -0.01f) continue; // outside the field: the gate, the way off
                    bool onRow = rows.Any(r => Mathf.Abs(p.pos.x - r.a.x) < 1e-3f && p.pos.y <= 0.5f + 1e-3f) || (p.pos - gIn).magnitude < 1e-3f;
                    Assert.IsTrue(onRow, $"kid {m} stands at {p.pos} inside the field off every furrow");
                }
            Assert.IsTrue(s.parts[0].Any(p => p.run && p.pose != null && Pose(p, 0.1f, d => d.squash) <= 0.81f), "they duck as they run (squash 0.8)");
            var wave = s.parts[3].First(p => p.from >= s.a - 1e-3f);
            Assert.Greater(Pose(wave, 0.2f, d => Mathf.Abs(d.armR.z)), 59f, "the farmer waves (arm z 60+)");
            Assert.Greater(s.a, 2f); Assert.Less(s.a, s.Length);
        }

        [Test]
        public void Scarecrow_MadeInOrder_CrossStrawHatThenTheBird()
        {
            var spot = new Vector2(0.2f, 0.4f);
            var s = VillageScenes.Scarecrow(1, new Vector2(-0.5f, 0f), 2, new Vector2(0.6f, 0.1f), 3, new Vector2(0f, -0.4f), spot, Vector2.down, 0f);
            TimesInOrder(s);
            CollectionAssert.AreEqual(new[] { VillageFx.Cross, VillageFx.Straw, VillageFx.Hat, VillageFx.Bird }, Fx(s).ToArray());
            Assert.AreEqual(s.a, FxAt(s, VillageFx.Cross), 1e-3f);
            Assert.AreEqual(s.b, FxAt(s, VillageFx.Hat), 1e-3f);
            Assert.IsTrue(s.parts[2].Any(p => p.prop == PropKind.Sheaf), "the kid brings the straw");
            foreach (var p in All(s)) Assert.GreaterOrEqual((p.pos - spot).magnitude, 0.06f, "nobody stands in the scarecrow");
            Assert.IsTrue(s.parts.All(l => l.Any(p => p.style == DollStyle.Laugh)), "everyone laughs");
        }

        [Test]
        public void HarvestDance_ARingRoundTheSheaf_OneWayAndBack_ThenTheJump()
        {
            var c = new Vector2(0.4f, 0.6f);
            var who = new[] { 1, 2, 3, 4, 5, 6, 7 };
            var s = VillageScenes.HarvestDance(who, Ats(7, c), c, 14f, 0f);
            TimesInOrder(s);
            Assert.AreEqual(VillageFx.Sheaf, Fx(s).First());
            var ring = All(s).Where(p => p.poseWalking).ToList();
            Assert.IsNotEmpty(ring);
            float r0 = (ring[0].pos - c).magnitude;
            foreach (var p in ring) Assert.AreEqual(r0, (p.pos - c).magnitude, 1e-3f, "the ring keeps its round");
            Assert.Greater(r0, 0.1f, "clear of the sheaf");
            float danceFrom = ring.Min(p => p.from), danceTo = ring.Max(p => p.to);
            Assert.GreaterOrEqual(danceTo - danceFrom, 11.5f); Assert.LessOrEqual(danceTo - danceFrom, 16.5f);
            Assert.Greater(Pose(ring[0], 0.2f, d => d.offset.y), 0.005f, "they skip");
            Assert.AreEqual(70f, Pose(ring[0], 0.2f, d => d.armR.z), 1e-3f, "hands joined (arms z 70)");
            Assert.IsTrue(s.parts.All(l => l.Any(p => p.from >= danceTo - 1e-3f && Pose(p, 0.25f, d => d.armR.x) > 100f)), "all jump together, arms up");
        }

        [Test]
        public void HomeMend_TheLadderThenTheTapsThenTheNewThing()
        {
            Vector2 foot = new Vector2(0f, -0.1f), wall = new Vector2(0f, 0f), extra = new Vector2(0.15f, -0.03f);
            var s = VillageScenes.HomeMend(1, new Vector2(0.5f, -0.8f), foot, wall, extra, Vector2.down, 0.36f, 0f);
            TimesInOrder(s);
            Assert.Less(s.b, s.a); Assert.Less(s.a, s.c); Assert.Less(s.c, s.d);
            Assert.AreEqual(3, s.fx.Count(f => f.kind == CampFxKind.Chips), "three taps");
            Assert.IsTrue(s.parts[0].Any(p => p.prop == PropKind.Hammer));
            Assert.AreEqual(s.d, FxAt(s, VillageFx.Extra), 1e-3f);
            foreach (var p in All(s)) Assert.Less(p.pos.y, 0f, "never inside the wall");
        }

        [Test]
        public void Well_NobodyStandsInTheRim_ThePebbleThenTheRing()
        {
            var well = new Vector2(0.3f, 0.2f);
            var g = VillageScenes.WellGossip(new[] { 1, 2, 3 }, Ats(3, well), well, VillageGeo.WellR, Vector2.down, 8f, 0f);
            TimesInOrder(g);
            foreach (var p in All(g)) Assert.GreaterOrEqual((p.pos - well).magnitude, VillageGeo.WellR + 0.035f, "outside the rim");
            Assert.Less(g.a, g.b); Assert.Greater(g.b - g.a, 6f, "6-10 s of talk");
            Assert.IsTrue(g.parts.All(l => l.Any(p => p.style == DollStyle.Laugh)), "the laugh at the end");
            Assert.IsTrue(g.parts.All(l => l.Any(p => p.prop == PropKind.Bucket)), "they come with buckets");

            var k = VillageScenes.WellPebble(5, well + new Vector2(0.5f, -0.5f), well, VillageGeo.WellR, Vector2.down, 0f);
            TimesInOrder(k);
            foreach (var p in All(k)) Assert.GreaterOrEqual((p.pos - well).magnitude, VillageGeo.WellR + 0.03f, "the kid never stands in the rim");
            Assert.AreEqual(k.a, FxAt(k, VillageFx.Pebble), 1e-3f);
            Assert.AreEqual(k.b, FxAt(k, VillageFx.PebbleRing), 1e-3f);
            Assert.GreaterOrEqual(k.b - k.a, 1.5f, "they wait for it (1.5 s)");
            var listen = k.parts[0].First(p => p.from <= k.b - 0.5f && p.to > k.b - 0.5f);
            Assert.Greater(Pose(listen, 1f, d => d.armL.x), 110f, "a hand at the ear (arm x 120)");
        }

        [Test]
        public void Mill_TheWatchersSitAndNod_TheRacersKeepToTheCircleAndJump()
        {
            var mill = new Vector2(0f, 1f);
            var w = VillageScenes.SailWatch(new[] { 1, 2 }, Ats(2, mill), new[] { mill + new Vector2(-0.05f, -0.46f), mill + new Vector2(0.06f, -0.46f) }, mill, 40f, 10f, 0f);
            TimesInOrder(w);
            var sit = w.parts[0].First(p => p.style == DollStyle.Sit);
            float y0 = Pose(sit, 0.1f, d => d.yaw), y1 = Pose(sit, 0.1f + 90f / 40f * 0.25f, d => d.yaw);
            Assert.Greater(Mathf.Abs(y1 - y0), 20f, "the head nods with the sails");
            Assert.LessOrEqual(Mathf.Abs(y1), 30.1f, "never more than 30 deg");

            var r = VillageScenes.SailShadow(new[] { 1, 2 }, Ats(2, mill), mill, VillageGeo.ShadowR, -40f, -90f, 9f, 0f);
            TimesInOrder(r);
            var run = All(r).Where(p => p.run).ToList();
            Assert.IsNotEmpty(run);
            foreach (var p in run) Assert.AreEqual(VillageGeo.ShadowR, (p.pos - mill).magnitude, 1e-3f, "round the mill's foot");
            Assert.IsTrue(run.Any(p => Enumerable.Range(0, 10).Any(i => Pose(p, i * p.Len / 10f, d => d.offset.y) > 0.04f)), "they jump as a shadow passes");
            Assert.LessOrEqual(r.Length, 20f, "a short race");
        }

        [Test]
        public void LumberMill_TheBreakOnTheLogs_TheWalkAlongTheLog()
        {
            var a = new Vector2(0f, 0f); var b = new Vector2(0.11f, 0f);
            var s = VillageScenes.LogPileSit(new[] { 1, 2 }, Ats(2, a), new[] { a, b }, new Vector2(0.05f, -1f), 8f, 0f);
            TimesInOrder(s);
            var sit = s.parts[0].First(p => p.style == DollStyle.Sit);
            Assert.AreEqual(a, sit.pos);
            Assert.Greater(Pose(sit, 1f, d => d.offset.y), 0.04f, "up on the logs");
            Assert.IsTrue(Enumerable.Range(0, 40).Any(i => Pose(sit, i * 0.2f, d => d.armR.x) > 120f), "a drink from the cup");

            Vector2 from = new Vector2(0.5f, 0f), to = new Vector2(0.74f, 0.05f);
            var l = VillageScenes.LogBalance(3, new Vector2(0f, -0.5f), from, to, Vector2.down, 0f);
            TimesInOrder(l);
            var walk = l.parts[0].First(p => p.poseWalking && (p.pos - to).magnitude < 1e-3f);
            Assert.AreEqual(VillageGeo.LogTop, Pose(walk, 0.3f, d => d.offset.y), 1e-3f, "on the log's top");
            Assert.AreEqual(90f, Mathf.Abs(Pose(walk, 0f, d => d.armR.z)), 15f, "arms out");
            Assert.IsTrue(l.fx.Any(f => f.kind == CampFxKind.Dust && Mathf.Abs(f.time - l.a) < 1e-3f), "dust at the landing");
        }

        [Test]
        public void Bakery_TheCrustFromTheBakerToAKid_ThenShared()
        {
            Vector2 door = new Vector2(0f, 0f), outward = Vector2.down;
            var s = VillageScenes.CrustWait(new[] { 1, 2, 3 }, Ats(3, door), 9, new Vector2(0.2f, 0.2f), door, outward, 0f);
            TimesInOrder(s);
            Assert.AreEqual(3, Mathf.RoundToInt(s.d), "the baker's member");
            var fx = Fx(s).ToList();
            Assert.AreEqual(VillageFx.CrustGive, fx[0]);
            Assert.AreEqual(2, fx.Count(x => x == VillageFx.CrustShare), "shared with the other two");
            for (int m = 0; m < 3; m++) foreach (var p in s.parts[m]) Assert.Less(Vector2.Dot(p.pos - door, outward) , 9f);
            var rock = s.parts[1].First(p => p.from > 0.5f && p.pose != null);
            Assert.IsTrue(Enumerable.Range(0, 10).Any(i => Pose(rock, i * 0.1f, d => d.offset.y) > 0.01f), "rocking on the toes");
        }

        [Test]
        public void Hopscotch_TheGridFirst_ThenHopsAlongIt()
        {
            Vector2 start = new Vector2(0f, 0f), along = Vector2.right;
            var s = VillageScenes.Hopscotch(new[] { 1, 2, 3 }, Ats(3, start), start, along, Vector2.down, 0f);
            TimesInOrder(s);
            Assert.AreEqual(s.a, FxAt(s, VillageFx.Grid), 1e-3f);
            var hops = All(s).Where(p => p.poseWalking).ToList();
            Assert.GreaterOrEqual(hops.Count, 4, "two kids out and back");
            foreach (var p in hops) { Assert.AreEqual(0f, p.pos.y, 1e-3f, "along the grid"); Assert.GreaterOrEqual(p.from, s.a, "after the grid is drawn"); }
            Assert.Greater(hops.Max(p => p.pos.x), VillageGeo.GridLen * 0.8f, "to the far end");
        }

        [Test]
        public void Fences_TheFixerKneelsOutside_TheKidsSitOnTheTopRail()
        {
            Vector2 post = new Vector2(0f, 0f), outward = Vector2.down;
            var f = VillageScenes.FenceFix(1, new Vector2(0.4f, -0.6f), post, outward, Vector2.down, 0f);
            TimesInOrder(f);
            foreach (var p in f.parts[0]) Assert.Greater(Vector2.Dot(p.pos - post, outward), 0.05f, "outside the field");
            Assert.Less(f.a, f.b);
            Assert.AreEqual(3, f.fx.Count(x => x.kind == CampFxKind.Village && (VillageFx)x.count == VillageFx.PostTap));

            var l = VillageScenes.FenceLean(1, new Vector2(-0.3f, -0.5f), 2, new Vector2(0.3f, -0.5f), 3, new Vector2(0.5f, -0.5f), post, Vector2.right, Vector2.up, 8f, 0f);
            TimesInOrder(l);
            for (int m = 0; m < 2; m++)
            {
                var sit = l.parts[m].First(p => p.style == DollStyle.Sit);
                Assert.AreEqual(0.05f, (sit.pos - post).magnitude, 1e-3f, "on the rail");
                Assert.Greater(Pose(sit, 0.5f, d => d.offset.y), VillageGeo.FenceTop * 0.7f, "up on the top rail");
            }
            foreach (var p in l.parts[2]) Assert.LessOrEqual(Vector2.Dot(p.pos - post, Vector2.up), -0.04f, "the grown-up stays outside");
        }

        [Test]
        public void Barn_HayPitchedThenTheBucket_KidsDropIntoTheHeap()
        {
            Vector2 heap = new Vector2(0.2f, 0f), door = new Vector2(0f, 0f);
            var c = VillageScenes.BarnChores(1, new Vector2(0.6f, -0.6f), heap, door, Vector2.down, 0f);
            TimesInOrder(c);
            Assert.AreEqual(3, c.parts[0].Count(p => p.prop == PropKind.Sheaf), "three armfuls of hay");
            Assert.GreaterOrEqual(c.fx.Count(f => f.kind == CampFxKind.Grass), 6, "grass bits at the heap and the door");
            Assert.Less(c.a, c.b);
            Assert.IsTrue(c.parts[0].Any(p => p.prop == PropKind.Bucket), "the bucket for the animals");
            foreach (var p in c.parts[0]) Assert.GreaterOrEqual((p.pos - heap).magnitude, VillageGeo.HayR + 0.04f, "never inside the heap");

            var j = VillageScenes.HayJump(new[] { 5, 6 }, new[] { new Vector2(0f, -0.6f), new Vector2(0.4f, -0.6f) }, heap, Vector2.down, 0f);
            TimesInOrder(j);
            Assert.AreEqual(4, j.fx.Count(f => f.kind == CampFxKind.Village && (VillageFx)f.count == VillageFx.HayDent), "each kid drops in twice");
            var drop = j.parts[0].First(p => p.from >= j.a - 0.4f && p.pose != null);
            Assert.Less(Enumerable.Range(0, 12).Min(i => Pose(drop, i * 0.05f, d => d.squash)), 0.65f, "squash 0.6 on landing");
            Assert.IsTrue(j.parts.All(l => l.Any(p => p.style == DollStyle.Laugh)), "they laugh");
        }

        [Test]
        public void StreetChat_IsB50bsMeet()
        {
            var s = VillageScenes.StreetChat(1, 2, new Vector2(-0.5f, 0f), new Vector2(0.5f, 0f), Vector2.zero, Vector2.right, 5f, 0f);
            Assert.AreEqual(CampUse.StreetChat, s.use);
            Assert.IsTrue(s.parts.All(l => l.Any(p => p.style == DollStyle.Talk && p.Len >= 4f)));
        }
    }
}
