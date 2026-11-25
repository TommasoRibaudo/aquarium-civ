// #722 (animation-specs B78.a-c): the story events' scenes in EditMode, no scene: each builder against its spec row (the cast, the beats in
// order, the props' hits), the kids' safety (the fruit picked from the ground, never by climbing; the knife and the stone with the grown-ups;
// the kids 0.3 u clear of the stone's roll), the fruit's life use, and the second stone's mesh.
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using static AquariumCiv.View.Tests.CampScenesTests;

namespace AquariumCiv.View.Tests
{
    public class StoryScenesTests
    {
        static readonly Vector2 Tree = new Vector2(0.4f, 0.9f), ToCam = Vector2.down, Fire = new Vector2(-0.8f, 0.5f);
        static readonly Vector2 Far = Tree + new Vector2(0.9f, 0.3f), Far2 = Tree + new Vector2(-0.7f, 0.4f), Far3 = Tree + new Vector2(0.2f, 1.1f);
        const int Nobody = VillageScenes.Nobody;

        static void Ok(CampScene s) { Sane(s); PoseRuns(s); Assert.Greater(s.Length, 1f, s.use + " has a length"); Assert.Greater(s.a, 0f, s.use + " has its hit"); }

        /// <summary>The highest a member is drawn off the ground over every part (the pose's lift and offset).</summary>
        static float Highest(CampScene s, int m)
        {
            float hi = 0f;
            foreach (var p in s.parts[m])
                if (p.pose != null)
                    for (float t = 0f; t <= p.Len; t += 0.05f) { var d = DollPose.Rest; p.pose(ref d, t, p.Len); hi = Mathf.Max(hi, d.lift + d.offset.y); }
            return hi;
        }

        static float FirstOf(CampScene s, int m, DollStyle style) => s.parts[m].Where(p => p.style == style).Select(p => p.from).DefaultIfEmpty(-1f).Min();

        [Test]
        public void B78a_FirstFruit_PickedOnTheToes_RunToTheFire_ABiteEach_TheLastToTheYoungest()
        {
            var grownAt = new[] { Fire + Vector2.left * 0.4f, Fire + Vector2.right * 0.4f, Fire + Vector2.up * 0.4f };
            var s = StoryScenes.FirstFruit(1, Far, new[] { 2, 3, 4 }, grownAt, 5, Far2, Tree, 4, ToCam, Fire, ToCam, 0f);
            Ok(s);
            Assert.AreEqual(CampUse.FirstFruit, s.use);
            Assert.AreEqual(5, s.Count, "the kid, three grown-ups at the fire, the youngest");
            // the beats in order: looks up, picks (the hit), runs to the fire held high, the bites, the last one the youngest's
            float look = FirstOf(s, 0, DollStyle.LookUp);
            Assert.Greater(look, 0f, "stops under the tree and looks up");
            Assert.AreEqual(1f, s.parts[0].First(p => p.style == DollStyle.LookUp).Len, 1e-3f, "LookUp, 1 s");
            Assert.Greater(s.a, look, "then picks it");
            var run = s.parts[0].Single(p => p.run);
            Assert.IsTrue(run.poseWalking, "runs with it held up");
            var d = DollPose.Rest; run.pose(ref d, 1f, run.Len);
            Assert.AreEqual(150f, d.armL.x, 1f); Assert.AreEqual(150f, d.armR.x, 1f);
            Assert.GreaterOrEqual(run.from, s.a, "runs after the pick");
            Assert.Less((run.pos - (Fire + ToCam * 0.3f)).magnitude, 0.01f, "to the fire");
            Assert.AreEqual(s.c, run.to, 1e-3f);
            var bites = Enumerable.Range(1, 3).Select(m => s.parts[m].Single(p => p.pose != null && p.from >= s.c)).ToList();
            foreach (var b in bites) Assert.AreEqual(StoryScenes.BiteFor, b.Len, 1e-3f, "a bite each, 0.6 s");
            for (int i = 1; i < 3; i++) Assert.Greater(bites[i].from, bites[i - 1].from, "one after another, round the fire");
            var last = s.parts[4].Single(p => p.pose != null);
            Assert.Greater(last.from, bites.Max(b => b.from), "the last bite goes to the youngest");
            Assert.AreEqual(s.d, last.to, 1e-3f, "and then it is eaten");
            Assert.GreaterOrEqual(s.parts[4].Min(p => p.from), s.c, "the youngest comes over once the kid is at the fire");
            // the fruit in the hands from the pick until it is eaten
            var tip = s.tips.Single();
            Assert.AreEqual(CampTipKind.Berry, tip.kind); Assert.AreEqual(0, tip.member);
            Assert.AreEqual(s.a, tip.from, 1e-3f); Assert.AreEqual(s.d, tip.to, 1e-3f); Assert.Greater(tip.scale, 1f, "the berry drawn bigger: a fruit");
            // kids safe: picked from the ground, never by climbing
            Assert.LessOrEqual(Highest(s, 0), StoryScenes.Toes + 1e-4f, "on the toes, never more (the kid never climbs)");
            Assert.LessOrEqual(Highest(s, 4), StoryScenes.Toes + 1e-4f);
            Assert.IsFalse(s.parts[0].Any(p => p.carried), "never carried up");
            var low = StoryScenes.LowFruit(Tree, 4, ToCam);
            Assert.AreEqual(low.x, s.p0.x, 1e-4f); Assert.AreEqual(low.y, s.p1.x, 1e-4f, "the low fruit's height");
            Assert.Less(low.y, 0.2f, "hangs within a kid's reach on the toes");
        }

        [Test]
        public void B78a_FirstFruit_WhenThePickerIsTheYoungest_TheyHaveTheLastBite()
        {
            var s = StoryScenes.FirstFruit(1, Far, new[] { 2, 3 }, new[] { Fire + Vector2.left * 0.4f, Fire + Vector2.right * 0.4f }, Nobody, default, Tree, 4, ToCam, Fire, ToCam, 0f);
            Ok(s);
            Assert.AreEqual(3, s.Count);
            var taste = s.parts[0].Single(p => p.style == DollStyle.Taste);
            Assert.AreEqual(s.d, taste.to, 1e-3f, "the picker eats the last bite");
            Assert.Greater(taste.from, s.parts[2].Single(p => p.pose != null && p.from >= s.c).from);
        }

        [Test]
        public void FruitPick_AKidPicksOneInSummer_OnTheToes_AndEatsIt()
        {
            var s = StoryScenes.FruitPick(1, Far, Tree, 4, ToCam, ToCam, 0f);
            Ok(s);
            Assert.AreEqual(CampUse.FruitPick, s.use); Assert.AreEqual(1, s.Count);
            Assert.Less(FirstOf(s, 0, DollStyle.LookUp), s.a, "looks up, then picks");
            Assert.Greater(FirstOf(s, 0, DollStyle.Taste), s.a, "eats it after the pick");
            var tip = s.tips.Single();
            Assert.Less(tip.grow, 1f, "the fruit gets smaller as it is eaten");
            Assert.LessOrEqual(Highest(s, 0), StoryScenes.Toes + 1e-4f, "never climbs");
        }

        [Test]
        public void B78b_TwoMarks_TheCoupleCutANotchEach_TheKidsWatchFromAStepBack()
        {
            var s = StoryScenes.TwoMarks(1, 2, Far, Far2, new[] { 3, 4 }, new[] { Far3, Far3 + Vector2.right * 0.2f }, Tree, 4, 2, 0f);
            Ok(s);
            Assert.AreEqual(CampUse.TwoMarks, s.use);
            Assert.AreEqual(4, s.Count, "the couple and two kids");
            Assert.AreEqual(2, s.kind, "the couple's pair");
            Assert.Greater(s.b, s.a, "one cuts, then the other beside it");
            var chips = s.fx.Where(f => f.kind == CampFxKind.Chips).Select(f => f.time).OrderBy(x => x).ToList();
            Assert.AreEqual(2 * StoryScenes.Strokes, chips.Count, "three small strokes each, a chip puff each");
            Assert.AreEqual(3, chips.Count(x => x < s.a)); Assert.AreEqual(3, chips.Count(x => x > s.a && x < s.b));
            int aw = Mathf.RoundToInt(s.p0.x);
            var ma = StoryScenes.MarkAt(Tree, 4, 2, aw); var mb = StoryScenes.MarkAt(Tree, 4, 2, 1 - aw);
            var first = s.fx.Where(f => f.kind == CampFxKind.Chips).OrderBy(f => f.time).First();
            Assert.Less((first.at - ma).magnitude, 1e-4f, "a's strokes at a's notch");
            Assert.Less(((Vector2)(new Vector2(ma.x, ma.z)) - new Vector2(mb.x, mb.z)).magnitude, 0.03f, "side by side");
            Assert.AreEqual(ma.y, mb.y, 1e-5f);
            // the knife (the name cut's stone) stays with the grown-ups
            Assert.AreEqual(2, s.tips.Count);
            Assert.IsTrue(s.tips.All(t => t.member < 2 && t.kind == CampTipKind.Stone));
            // the kids watch from a step back, never at the trunk
            for (int m = 2; m < 4; m++)
            {
                Assert.IsTrue(s.parts[m].Any(p => p.style == DollStyle.Peek), "kids watch (Peek)");
                foreach (var p in s.parts[m]) Assert.Greater((p.pos - Tree).magnitude, TreeGeo.RingR(4) + 0.08f + 0.1f, "a step back from where the couple stand");
            }
            // together, a step back, looking at the two marks (1.5 s)
            Assert.IsTrue(s.parts[0].Any(p => p.from >= s.b && Mathf.Abs(p.Len - 1.5f) < 1e-3f) && s.parts[1].Any(p => p.from >= s.b && Mathf.Abs(p.Len - 1.5f) < 1e-3f));
        }

        [Test]
        public void B78b_TheMarks_StackDownTheTrunk_EightPairsAboveTheRoots()
        {
            for (int i = 1; i < 8; i++) Assert.Less(StoryScenes.MarkAt(Tree, 4, i, 0).y, StoryScenes.MarkAt(Tree, 4, i - 1, 0).y, "each couple's pair under the last");
            Assert.Greater(StoryScenes.MarkAt(Tree, 4, 7, 0).y, 0.01f, "the eighth still above the ground");
            var m = StoryScenes.MarkAt(Tree, 4, 0, 0);
            Assert.Less(Vector2.Dot(new Vector2(m.x, m.z) - Tree, StoryScenes.MarkFace), TreeGeo.TrunkR(4) + 1e-4f, "on the bark, not in the air");
        }

        [Test]
        public void B78c_SecondStone_RolledInByGrownUps_StoodUp_TheNameCut_TheFamilyRound()
        {
            var spot = Tree; var from = spot + new Vector2(0.6f, 0f); var first = spot - new Vector2(0.24f, 0f);
            var fam = new[] { 3, 4, 5 }; var famAt = new[] { from + new Vector2(0.1f, -0.2f), from + new Vector2(0.05f, 0.05f), Far3 };
            var kid = new[] { false, true, true };
            var s = StoryScenes.SecondStone(1, Far, 2, Far2, fam, famAt, kid, from, spot, first, ToCam, 0f);
            Ok(s);
            Assert.AreEqual(CampUse.SecondStone, s.use);
            Assert.AreEqual(5, s.Count, "two to roll it, and the family");
            Assert.That(new[] { s.a, s.b, s.c, s.d }, Is.Ordered, "the roll, the heave, the name");
            Assert.Greater(s.b - s.a, 1f);
            float quarters = (s.b - s.a) / StoryScenes.QuarterEvery;
            Assert.AreEqual(Mathf.Round(quarters), quarters, 1e-3f, "whole quarter turns: it arrives flat");
            // only grown-ups roll it: the pushes are the two rollers'
            for (int m = 0; m < s.Count; m++)
            {
                bool pushes = s.parts[m].Any(p => p.poseWalking && p.from >= s.a - 1e-3f && p.to <= s.b + 1e-3f);
                Assert.AreEqual(m < 2, pushes, $"member {m} pushes");
            }
            var push = s.parts[0].First(p => p.poseWalking);
            var d = DollPose.Rest; push.pose(ref d, 0.3f, push.Len);
            Assert.GreaterOrEqual(d.lean, StoryScenes.PushLean, "both lean in, 25");
            Assert.AreEqual(1, s.fx.Count(f => f.kind == CampFxKind.Prop && f.count == (int)PropFx.Jolt && Mathf.Abs(f.time - s.c) < 1e-3f), "the stone jolts up as it stands");
            Assert.AreEqual(1, s.fx.Count(f => f.kind == CampFxKind.Prop && f.count == (int)PropFx.NameRow && Mathf.Abs(f.time - s.d) < 1e-3f), "the name is cut on it (#693's cut)");
            Assert.GreaterOrEqual(s.fx.Count(f => f.kind == CampFxKind.Chips && f.time > s.c), 10, "the runestone's chips");
            Assert.IsTrue(s.tips.Any(t => t.member == 0 && t.kind == CampTipKind.Stone), "the one who rolled it cuts the name");
            // the family round the stones, heads down; the kids 0.3 u clear of the roll, and in only once it has stopped
            for (int i = 0; i < fam.Length; i++)
            {
                int m = 2 + i;
                Assert.IsTrue(s.parts[m].Any(p => p.style == DollStyle.Mourn), "heads down (Mourn)");
                Assert.GreaterOrEqual(s.parts[m].Min(p => p.from), s.b - 1e-3f, "they come once the stone has stopped");
                if (!kid[i]) continue;
                foreach (var p in s.parts[m]) Assert.GreaterOrEqual(StoryScenes.ToSegment(p.pos, from, spot), StoryScenes.KidsBack - 1e-3f, "a kid keeps 0.3 u from the roll");
            }
            foreach (var p in s.parts.Skip(1).SelectMany(l => l)) Assert.GreaterOrEqual((p.pos - first).magnitude, 0.15f, "nobody stands in the first stone");
            // the stone: at its start until pushed, at its spot after the roll, lying until the heave, upright at the jolt
            StoryScenes.StoneNow(s, 0f, out var at0, out _, out float up0);
            Assert.Less((at0 - from).magnitude, 1e-4f); Assert.AreEqual(0f, up0, 1e-4f);
            StoryScenes.StoneNow(s, s.b, out var at1, out float turn1, out float up1);
            Assert.Less((at1 - spot).magnitude, 1e-4f); Assert.AreEqual(0f, up1, 1e-4f);
            Assert.AreEqual(0f, Mathf.Repeat(turn1 + 45f, 90f) - 45f, 0.05f, "a quarter turn each 0.8 s: it lies flat");
            StoryScenes.StoneNow(s, s.c, out _, out _, out float up2);
            Assert.AreEqual(1f, up2, 1e-3f, "upright as it jolts");
            Assert.AreEqual(90f, StoryScenes.RollTurn(StoryScenes.QuarterEvery), 1e-3f);
            Assert.AreEqual(180f, StoryScenes.RollTurn(2f * StoryScenes.QuarterEvery), 1e-3f);
        }

        [Test]
        public void B78c_TheFamilySpots_KeepTheKidsClearWhateverTheRollsSide()
        {
            var spot = Tree;
            foreach (var dir in new[] { Vector2.right, Vector2.down, Vector2.up, new Vector2(-0.7f, -0.7f) })
            {
                var from = spot + dir.normalized * 0.6f; var first = spot - new Vector2(0.24f, 0f);
                var kids = new[] { true, true, false, true };
                var got = StoryScenes.FamilySpots(kids, from, spot, first, spot + ToCam * CampScenes6.NameKneel, ToCam);
                for (int i = 0; i < kids.Length; i++)
                {
                    if (kids[i]) Assert.GreaterOrEqual(StoryScenes.ToSegment(got[i], from, spot), StoryScenes.KidsBack - 1e-3f, $"roll {dir}: kid {i}");
                    Assert.GreaterOrEqual((got[i] - first).magnitude, 0.2f - 1e-3f);
                    for (int j = 0; j < i; j++) Assert.Greater((got[i] - got[j]).magnitude, 0.08f, "not on top of each other");
                }
            }
        }

        [Test]
        public void B78c_TheSecondStone_TheRunestoneWithAnotherSeed_OnlyItsOwnRows()
        {
            var first = ConsequenceMeshes.Build(AquariumCiv.Sim.ConsequenceKind.NameStone);
            var blank = ConsequenceMeshes.SecondStone(0); var one = ConsequenceMeshes.SecondStone(1);
            Assert.Less(blank.vertexCount, first.vertexCount, "the new stone has no old rows");
            Assert.Greater(one.vertexCount, blank.vertexCount, "the first name shows");
            var fv = first.vertices; var bv = blank.vertices;
            Assert.IsTrue(Enumerable.Range(0, bv.Length).Any(i => (fv[i] - bv[i]).sqrMagnitude > 1e-8f), "another seed: its own rough shape");
            foreach (var m in new[] { first, blank, one }) Object.DestroyImmediate(m);
        }
    }
}
