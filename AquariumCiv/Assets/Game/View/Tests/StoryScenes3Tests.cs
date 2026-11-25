// #722 batch 3 (animation-specs B80.b-h): the last story events' scenes in EditMode, no scene: each builder against its spec row (the cast,
// the beats in order, the hits), the kids' safety (no climbing, the blade and the bucket with the grown-ups, the kids a step back from the
// carve), the life uses they leave, and the woodpile's face look.
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using static AquariumCiv.View.Tests.CampScenesTests;

namespace AquariumCiv.View.Tests
{
    public class StoryScenes3Tests
    {
        static readonly Vector2 ToCam = Vector2.down, Fire = new Vector2(-0.6f, 0.4f);
        static readonly Vector2 Far = new Vector2(0.5f, 0.2f), Far2 = new Vector2(-0.2f, 0.9f), Far3 = new Vector2(0.9f, 0.7f);
        const int Nobody = VillageScenes.Nobody;

        static void Ok(CampScene s) { Sane(s); PoseRuns(s); Assert.Greater(s.Length, 1f, s.use + " has a length"); Assert.Greater(s.a, 0f, s.use + " has its hit"); }

        static float Highest(CampScene s, int m)
        {
            float hi = 0f;
            foreach (var p in s.parts[m])
                if (p.pose != null)
                    for (float t = 0f; t <= p.Len; t += 0.05f) { var d = DollPose.Rest; p.pose(ref d, t, p.Len); hi = Mathf.Max(hi, d.lift + d.offset.y); }
            return hi;
        }

        /// <summary>The part member <paramref name="m"/> is in at scene second <paramref name="t"/> (the last before it if none spans it).</summary>
        static CampPart At(CampScene s, int m, float t) =>
            s.parts[m].Where(p => p.from <= t + 1e-4f).OrderBy(p => p.from).LastOrDefault();

        static Vector2 Left(Vector2 face) => new Vector2(-face.y, face.x);

        // ================================================================== B80.b the antlers passed
        [Test]
        public void B80b_TheAntlers_LiftedOffTheOldPost_CarriedHigh_SetOnTheNewOne_TheNewChiefWatches()
        {
            var oldPost = new Vector2(-0.5f, 0.6f); var newPost = new Vector2(0.4f, 0.5f);
            var s = StoryScenes.AntlersPass(1, Far, 2, Far2, oldPost, newPost, ToCam, 0f);
            Ok(s);
            Assert.AreEqual(CampUse.AntlersPass, s.use);
            Assert.AreEqual(2, s.Count, "the carrier and the new chief");
            Assert.Greater(s.b, s.a, "lifted off, then set on");
            Assert.AreEqual(oldPost, s.p0); Assert.AreEqual(newPost, s.p1);
            Assert.Less((At(s, 0, s.a).pos - oldPost).magnitude, StoryScenes.PostStand + 0.01f, "lifted off at the old post");
            Assert.Less((At(s, 0, s.b).pos - newPost).magnitude, StoryScenes.PostStand + 0.01f, "set on at the new door");
            var carry = s.parts[0].Single(p => p.poseWalking);
            Assert.GreaterOrEqual(carry.from, s.a - 1e-3f); Assert.LessOrEqual(carry.to, s.b + 1e-3f);
            var d = DollPose.Rest; carry.pose(ref d, 0.5f, carry.Len);
            Assert.AreEqual(110f, d.armL.x, 1f, "carried high in front"); Assert.AreEqual(110f, d.armR.x, 1f);
            Assert.IsTrue(s.parts[1].Any(p => p.style == DollStyle.Look && p.to <= s.b), "the new chief watches at their door");
            Assert.IsTrue(s.parts[1].Any(p => p.pose != null && p.from >= s.b - 0.3f), "and nods as they go up");
            Assert.IsTrue(s.parts[0].All(p => p.prop == PropKind.None), "the antlers are drawn by CampLife (no prop)");
            var alone = StoryScenes.AntlersPass(1, Far, Nobody, default, oldPost, newPost, ToCam, 0f);
            Ok(alone); Assert.AreEqual(1, alone.Count, "no new chief drawn: the carrier alone");
        }

        // ================================================================== B80.c the tinkerer's bucket
        [Test]
        public void B80c_TheTinkerer_MendsASpoutOn_TriesItAtThePond_ItPoursLeft_TheWatchersStandRight()
        {
            var edge = new Vector2(0.3f, 0.1f); var toWater = Vector2.right;
            var s = StoryScenes.TinkererBucket(1, Far2, new[] { 2, 3 }, new[] { Far, Far3 }, Fire, edge, toWater, ToCam, 0f);
            Ok(s);
            Assert.AreEqual(3, s.Count);
            var mend = s.parts[0].Single(p => p.style == DollStyle.Mend);
            Assert.AreEqual(StoryScenes.SpoutMendFor, mend.Len, 1e-3f, "the mend beat at the fire");
            Assert.Less((mend.pos - Fire).magnitude, 0.35f);
            Assert.AreEqual(mend.to, s.a, 1e-3f, "the spout is on as the mend ends");
            Assert.IsTrue(s.parts[0].Where(p => p.to <= s.a + 1e-3f).All(p => p.prop == PropKind.BucketEmpty), "a plain bucket until then");
            Assert.IsTrue(s.parts[0].Where(p => p.from >= s.a - 1e-3f).All(p => p.prop is PropKind.SpoutBucket or PropKind.SpoutBucketEmpty), "the crooked bucket after");
            Assert.That(s.b, Is.GreaterThan(s.a)); Assert.That(s.c, Is.GreaterThan(s.b), "filled, then poured");
            Assert.IsTrue(s.parts[0].Any(p => p.from >= s.b - 1e-3f && p.prop == PropKind.SpoutBucket), "full after the dip");
            var wet = s.fx.Single(f => f.kind == CampFxKind.Mark && f.count == (int)CampMark.WetPatch);
            Assert.AreEqual(s.c, wet.time, 1e-3f);
            var lands = new Vector2(wet.at.x, wet.at.z);
            Assert.Greater(Vector2.Dot(lands - edge, Left(toWater)), StoryScenes.SpoutSide * 0.8f, "it pours to the left");
            for (int m = 1; m < 3; m++)
            {
                var at = At(s, m, s.c + 0.1f);
                Assert.Less(Vector2.Dot(at.pos - edge, Left(toWater)), -0.1f, "everyone stood to the right");
                Assert.AreEqual(DollStyle.Laugh, at.style, "and laughs");
            }
        }

        // ================================================================== B80.e the gardener's flowers
        [Test]
        public void B80e_TheGardener_KneelsOutsideTheRing_PressesSixFlowers_OneByOne()
        {
            var stone = new Vector2(0.2f, 0.7f);
            var s = StoryScenes.GardenerFlowers(1, Far2, stone, ToCam, 0f);
            Ok(s);
            var presses = s.fx.Where(f => f.kind == CampFxKind.Grass).OrderBy(f => f.time).ToList();
            Assert.AreEqual(StoryScenes.Flowers, presses.Count, "one press a flower");
            Assert.AreEqual(s.a, presses[0].time, 1e-3f); Assert.AreEqual(s.b, presses[^1].time, 1e-3f);
            var spots = Enumerable.Range(0, StoryScenes.Flowers).Select(i => StoryScenes.FlowerAt(stone, i)).ToList();
            foreach (var p in presses) Assert.IsTrue(spots.Any(f => (f - new Vector2(p.at.x, p.at.z)).magnitude < 1e-3f), "at a ring spot");
            Assert.AreEqual(StoryScenes.Flowers, presses.Select(p => new Vector2(p.at.x, p.at.z)).Distinct().Count(), "each flower once");
            Assert.Greater(Vector2.Dot(new Vector2(presses[0].at.x, presses[0].at.z) - stone, ToCam), 0f, "the camera's side first");
            foreach (var p in s.parts[0].Where(p => p.pose != null)) Assert.Greater((p.pos - stone).magnitude, StoryScenes.FlowerRingR, "kneels outside the ring (off the stone)");
            Assert.IsTrue(s.tips.Any(t => t.kind == CampTipKind.Bunch && t.to >= s.b - 1e-3f), "the bunch in the hand");
            foreach (var f in spots) Assert.Greater((f - stone).magnitude, ConsequenceMeshes.StoneHalfX * ConsequenceMeshes.Scale, "the ring clears the slab");
        }

        [Test]
        public void B80e_TheMorningWater_OnTheFlowers_TheCrookedBucketPoursLeft()
        {
            var stone = new Vector2(0.2f, 0.7f);
            foreach (bool spout in new[] { false, true })
            {
                var s = StoryScenes.FlowerWater(1, Far, stone, ToCam, spout, 0f);
                Ok(s);
                Assert.AreEqual(CampUse.FlowerWater, s.use);
                var wet = s.fx.Single(f => f.kind == CampFxKind.Mark && f.count == (int)CampMark.WetPatch);
                Assert.AreEqual(s.a, wet.time, 1e-3f);
                var target = stone + ToCam * StoryScenes.FlowerRingR;
                float off = (new Vector2(wet.at.x, wet.at.z) - target).magnitude;
                if (!spout) Assert.Less(off, 1e-3f, "on the flowers");
                else Assert.Greater(off, 0.05f, "the crooked bucket's water off to the side");
                Assert.IsTrue(s.parts[0].Any(p => p.prop == (spout ? PropKind.SpoutBucket : PropKind.Bucket)));
            }
        }

        // ================================================================== B80.f the joker's face
        [Test]
        public void B80f_TheJoker_CarvesAGrin_TheKidsPeekAStepBack_AndLaugh()
        {
            var pile = new Vector2(-0.3f, 0.8f);
            var s = StoryScenes.JokerFace(1, Far, new[] { 2, 3 }, new[] { Far2, Far3 }, pile, ToCam, 0f);
            Ok(s);
            Assert.AreEqual(3, s.Count);
            Assert.AreEqual(2 * StoryScenes.Strokes, s.fx.Count(f => f.kind == CampFxKind.Chips), "two rounds of three strokes");
            Assert.IsTrue(s.tips.All(t => t.member == 0), "the cutting stone stays with the grown-up");
            var carve = s.parts[0].First(p => p.pose != null).pos;
            for (int m = 1; m < 3; m++)
            {
                foreach (var p in s.parts[m].Where(p => p.to <= s.a + 1e-3f && p.style == DollStyle.Peek))
                    Assert.GreaterOrEqual((p.pos - carve).magnitude, StoryScenes.KidsPeekBack - 0.01f, "a step back from the blade");
                Assert.IsTrue(s.parts[m].Any(p => p.style == DollStyle.Peek && p.to <= s.a + 1e-3f), "they lean in to watch");
                Assert.AreEqual(DollStyle.Laugh, At(s, m, s.a + 0.2f).style, "and laugh when it shows");
                Assert.LessOrEqual(Highest(s, m), 0.05f + 1e-3f, "no climbing on the pile");
            }
            var end = StoryScenes.FaceEnd(pile, ToCam);
            Assert.Greater(Vector2.Dot(new Vector2(end.x, end.z) - pile, ToCam), 0f, "the face on the camera's end");
        }

        [Test]
        public void B80f_AKidPatsTheFaceLog_AsTheyPass()
        {
            var pile = new Vector2(-0.3f, 0.8f);
            var s = StoryScenes.FacePat(1, Far, pile, ToCam, 0f);
            Ok(s);
            Assert.AreEqual(CampUse.FacePat, s.use);
            var pat = At(s, 0, s.a);
            Assert.Less((pat.pos - pile).magnitude, 0.3f, "at the pile");
            var d = DollPose.Rest; pat.pose(ref d, pat.Len * 0.5f, pat.Len);
            Assert.AreEqual(90f, d.armR.x, 2f, "the hand on the log");
            Assert.IsTrue(s.parts[0].Last().run, "and runs on");
        }

        [Test]
        public void B80f_TheWoodpilesFaceLook_AddsTheGrin_LaidLogsKept()
        {
            var plain = ConsequenceMeshes.Build(AquariumCiv.Sim.ConsequenceKind.WoodpileRoof, 0);
            var face = ConsequenceMeshes.Build(AquariumCiv.Sim.ConsequenceKind.WoodpileRoof, ConsequenceMeshes.FaceLook);
            var two = ConsequenceMeshes.Build(AquariumCiv.Sim.ConsequenceKind.WoodpileRoof, 2);
            var twoFace = ConsequenceMeshes.Build(AquariumCiv.Sim.ConsequenceKind.WoodpileRoof, 2 | ConsequenceMeshes.FaceLook);
            Assert.Greater(face.vertexCount, plain.vertexCount, "the grin");
            Assert.AreEqual(face.vertexCount - plain.vertexCount, twoFace.vertexCount - two.vertexCount, "the same grin, on top of the laid logs");
            Assert.Greater(twoFace.bounds.max.y, face.bounds.max.y - 1e-4f);
            foreach (var m in new[] { plain, face, two, twoFace }) Object.DestroyImmediate(m);
        }

        // ================================================================== B80.g the dry spell
        [Test]
        public void B80g_TheDrySpell_ABucketFromThePondToTheTree_TheOthersLookUpForRain()
        {
            var tree = new Vector2(0.5f, 0.9f); var edge = new Vector2(0.1f, 0.2f);
            var s = StoryScenes.TreeWater(CampUse.DrySpell, 1, Far2, edge, Vector2.down, tree, 4, new[] { 2, 3 }, new[] { Fire, Fire + Vector2.right * 0.2f }, ToCam, false, 0f);
            Ok(s);
            Assert.AreEqual(3, s.Count);
            var full = s.parts[0].First(p => p.prop == PropKind.Bucket);
            Assert.Less((full.pos - edge).magnitude, 0.01f, "filled at the pond's edge");
            Assert.IsTrue(s.parts[0].Where(p => p.to <= full.from + 1e-3f).All(p => p.prop == PropKind.BucketEmpty), "carried there empty");
            Assert.IsTrue(s.fx.Any(f => f.kind == CampFxKind.Ring && f.time <= full.from + 1e-3f), "the dip rings the water");
            var wet = s.fx.Single(f => f.kind == CampFxKind.Mark && f.count == (int)CampMark.WetPatch);
            Assert.AreEqual(s.a, wet.time, 1e-3f);
            Assert.Less((new Vector2(wet.at.x, wet.at.z) - tree).magnitude, TreeGeo.TrunkR(4) + 0.05f, "poured at the tree's foot");
            for (int m = 1; m < 3; m++)
            {
                var look = s.parts[m].Single(p => p.style == DollStyle.LookUp);
                Assert.AreEqual(2.6f, look.Len, 1e-3f, "#687's sky look");
                Assert.IsTrue(s.parts[m].All(p => (p.pos - s.parts[m][0].pos).magnitude < 1e-4f), "where they stand");
            }
            var evening = StoryScenes.TreeWater(CampUse.TreeWater, 1, Far2, edge, Vector2.down, tree, 4, new[] { 2 }, new[] { Fire }, ToCam, true, 0f);
            Ok(evening);
            Assert.AreEqual(1, evening.Count, "the evening repeat: the waterer alone");
            Assert.IsTrue(evening.parts[0].Any(p => p.prop == PropKind.SpoutBucket), "the crooked bucket if it is theirs");
        }

        // ================================================================== B80.h the first snow
        [Test]
        public void B80h_TheFirstSnow_AFlakeOnTheTongue_TheOthersCopy_ASnowFigureRolledByTheFire()
        {
            var s = StoryScenes.FirstSnow(1, Far, new[] { 2, 3, 4 }, new[] { Far + Vector2.left * 0.2f, Far + Vector2.up * 0.2f, Far2 }, new[] { true, true, false }, Fire, ToCam, 0f);
            Ok(s);
            Assert.AreEqual(4, s.Count);
            var look = s.parts[0].First(p => p.style == DollStyle.LookUp);
            Assert.Less(look.from, s.a, "head back first, then the flake");
            for (int m = 1; m < 4; m++)
            {
                var copy = s.parts[m].First(p => p.style == DollStyle.LookUp);
                Assert.Greater(copy.from, s.a, "the others copy");
            }
            Assert.That(s.d, Is.GreaterThan(s.a)); Assert.That(s.b, Is.GreaterThan(s.d)); Assert.That(s.c, Is.GreaterThan(s.b), "rolled, then the head on");
            var spot = StoryScenes.FigureSpot(Fire);
            Assert.AreEqual(spot, s.p1);
            Assert.That((spot - Fire).magnitude, Is.InRange(0.3f, 0.5f), "by the fire");
            Assert.IsFalse(StoryScenes.BaseNow(s, s.d - 0.1f, out _, out _), "no ball before the roll");
            StoryScenes.BaseNow(s, s.d + 0.5f, out var early, out float r0);
            StoryScenes.BaseNow(s, s.b, out var done, out float r1);
            Assert.Less(r0, r1, "it grows as it rolls");
            Assert.Less((done - spot).magnitude, 1e-3f, "into its spot");
            Assert.Greater((early - spot).magnitude, (done - spot).magnitude);
            Assert.AreEqual(2, s.parts.Count(ps => ps.Any(p => p.poseWalking && p.from >= s.d - 1e-3f && p.to <= s.b + 1e-3f)), "two push it");
            for (int m = 0; m < 4; m++) Assert.LessOrEqual(Highest(s, m), 0.05f + 1e-3f, "feet on the ground (a hop at most)");
            // the figure stays put when the camera turns
            Assert.AreEqual(StoryScenes.FigureSpot(Fire), StoryScenes.FirstSnow(1, Far, new int[0], new Vector2[0], new bool[0], Fire, Vector2.right, 0f).p1);
        }
    }
}
