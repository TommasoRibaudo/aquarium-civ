using System.Collections.Generic;
using System.Linq;
using AquariumCiv.Life;
using AquariumCiv.Sim;
using NUnit.Framework;
using UnityEngine;

namespace AquariumCiv.View.Tests
{
    /// <summary>
    /// #776 (animation-specs B81.5): the founder's statue, the view's pure parts (EditMode): the code-built figure (its stages, tells, wear, size), and
    /// the scenes' timelines: the raising's stages in order and inside the story moment's time, no gap in anyone's part (a gap sends them back to their
    /// plan), the kids' climb (the steps only, never the figure, a grown-up within 0.6 u), the nod under 1.5 s, the sit, the flowers, the sweep.
    /// </summary>
    public class StatueScenesTests
    {
        static readonly Vector2 Spot = new Vector2(0.2f, 0.3f), ToCam = new Vector2(-0.47f, -0.88f);

        static List<Vector2> At(int n, float dx = 0f) { var l = new List<Vector2>(); for (int i = 0; i < n; i++) l.Add(Spot + new Vector2(-1.2f + 0.2f * i + dx, -0.9f)); return l; }

        static CampScene Raise(int family, int watchers, bool helper = true, int kidsAmongFamily = 1)
        {
            var fam = Enumerable.Range(10, family).ToList(); var famAt = At(family); var famKid = new List<bool>();
            for (int i = 0; i < family; i++) famKid.Add(i >= family - kidsAmongFamily);
            var watch = Enumerable.Range(20, watchers).ToList();
            var watchKid = new List<bool>(); for (int i = 0; i < watchers; i++) watchKid.Add(i % 2 == 1);
            return StatueScenes.Raise(1, Spot + new Vector2(1.2f, 0.2f), helper ? 2 : VillageScenes.Nobody, helper ? Spot + new Vector2(1f, -0.4f) : default, fam, famAt, famKid,
                watch, At(watchers, 2f), watchKid, Spot, Spot + new Vector2(0.65f, 0f), ToCam, 0f);
        }

        /// <summary>Every member's parts follow one another without a gap (a gap sends them back to their plan mid-scene) and never overlap.</summary>
        static void AssertContinuous(CampScene s)
        {
            for (int m = 0; m < s.Count; m++)
            {
                var parts = s.parts[m].OrderBy(p => p.from).ToList();
                Assert.IsNotEmpty(parts, "member " + s.keys[m]);
                for (int i = 0; i < parts.Count; i++)
                {
                    Assert.Greater(parts[i].to, parts[i].from, $"{s.use} member {s.keys[m]} part {i} has a length");
                    if (i > 0) Assert.AreEqual(parts[i - 1].to, parts[i].from, 1e-3f, $"{s.use} member {s.keys[m]}: no gap or overlap before part {i}");
                }
            }
        }

        static IEnumerable<(CampPart part, float t, DollPose pose)> Poses(CampScene s, int member, float step = 0.1f)
        {
            foreach (var p in s.parts[member])
                for (float t = 0f; t < p.Len; t += step)
                {
                    var pose = DollPose.Rest;
                    p.pose?.Invoke(ref pose, t, p.Len);
                    yield return (p, p.from + t, pose);
                }
        }

        // ---------- the figure ----------

        static Mesh Build(StatueStage stage, StatueWear wear = StatueWear.None, StatueRules.Tell tell = StatueRules.Tell.Nothing)
        {
            var mb = new MeshBuilder(7);
            PegDoll.Statue(mb, Vector3.zero, 1f, null, stage, wear, tell);
            return mb.ToMesh("t");
        }

        [Test]
        public void TheFigure_GrowsThroughItsStages_AndIsAboutThirtyFourHundredthsTall()
        {
            var plinth = Build(StatueStage.Plinth); var block = Build(StatueStage.Block); var rough = Build(StatueStage.Rough);
            var headed = Build(StatueStage.Headed); var carved = Build(StatueStage.Carved);
            Assert.Greater(block.vertexCount, plinth.vertexCount); Assert.Greater(rough.vertexCount, plinth.vertexCount);
            Assert.Greater(headed.vertexCount, rough.vertexCount, "the head shows");
            Assert.Greater(carved.vertexCount, plinth.vertexCount);
            Assert.AreEqual(PegDoll.StatueFeetY + 0.1f, block.bounds.max.y, 0.01f, "the block stands on the plinth: 0.1 u tall");
            Assert.AreEqual(0.34f, carved.bounds.max.y, 0.03f, "the carved figure with its plinth is about 0.34 u tall (B81.1)");
            Assert.AreEqual(PegDoll.StatueHeight, carved.bounds.max.y, 0.03f);
            Assert.AreEqual(0.14f, plinth.bounds.size.x, 0.005f, "the lower step is 0.14 u wide");
            Assert.AreEqual(0f, plinth.bounds.min.y, 1e-3f, "the plinth stands on the ground");
            Assert.GreaterOrEqual(carved.bounds.size.x, 0.14f - 0.005f);
            foreach (var c in carved.colors) Assert.AreEqual(c.r, c.g, 0.12f, "the stone is a grey: no skin or tunic colour");
        }

        [Test]
        public void EveryTell_BuildsItsOwnShape_AndTheSeasonsWearItOnly()
        {
            var counts = new Dictionary<StatueRules.Tell, int>();
            foreach (StatueRules.Tell t in System.Enum.GetValues(typeof(StatueRules.Tell))) counts[t] = Build(StatueStage.Carved, StatueWear.None, t).vertexCount;
            Assert.Greater(counts[StatueRules.Tell.Axe], counts[StatueRules.Tell.Nothing], "a tool in the hand");
            Assert.Greater(counts[StatueRules.Tell.Hand], counts[StatueRules.Tell.Nothing]);
            Assert.Greater(counts[StatueRules.Tell.Crook], counts[StatueRules.Tell.Hand]);
            Assert.Greater(Build(StatueStage.Carved, StatueWear.Snow).vertexCount, Build(StatueStage.Carved).vertexCount, "a snow cap");
            Assert.Greater(Build(StatueStage.Carved, StatueWear.Leaves).vertexCount, Build(StatueStage.Carved).vertexCount, "two leaves on the step");
            Assert.Greater(PegDoll.PropMesh(PropKind.FounderStatue).vertexCount, 0);
        }

        // ---------- the raising ----------

        [Test]
        public void TheRaising_StepsThroughItsStagesInOrder_AndFitsTheStoryMoment()
        {
            var s = Raise(3, 3);
            Assert.AreEqual(CampUse.StatueRaise, s.use);
            Assert.Greater(s.a, 2f); Assert.Less(s.a, s.b); Assert.Less(s.b, s.c); Assert.Less(s.c, s.d, "the seat, then three hits");
            Assert.Less(s.d, s.p1.y, "the flowers are laid after the carving"); Assert.Less(s.p1.y, s.Length);
            Assert.Less(s.p1.x, s.a, "the roll starts before the seat");
            Assert.Less(s.Length, LifePlanner.StorySeconds, "inside the story moment's time");
            Assert.Greater(s.Length, 20f, "a held scene, not a blink");
            Assert.AreEqual(8, s.Count);
            AssertContinuous(s);
            // the hits are the carver's three rounds of three strokes
            var carver = s.parts[0];
            Assert.AreEqual(StatueScenes.Rounds, carver.Count(p => p.pose != null && p.Len > 1.5f && p.Len < 2.2f && p.style == DollStyle.Stand && Mathf.Abs(p.Len - StatueScenes.RoundFor) < 1e-3f));
            Assert.AreEqual(StatueScenes.Rounds, s.tips.Count(t => t.kind == CampTipKind.Stone), "the cutting stone in the hand");
            Assert.AreEqual(1, s.tips.Count(t => t.kind == CampTipKind.Bunch), "one bunch carried");
        }

        [Test]
        public void TheRaising_WorksWithNoFamilyNoWatchersNoHelper()
        {
            var s = Raise(0, 0, helper: false, kidsAmongFamily: 0);
            Assert.AreEqual(1, s.Count, "the carver alone");
            AssertContinuous(s);
            Assert.Less(s.Length, LifePlanner.StorySeconds);
            Assert.Less(s.a, s.b);
            Assert.Greater(s.p1.y, s.d, "even alone the carver lays the first flowers");
            Assert.AreEqual(1, s.tips.Count(t => t.kind == CampTipKind.Bunch));
        }

        [Test]
        public void TheRaising_TheYoungestKidOfTheFamilyCarriesTheBunch_ElseTheOldest()
        {
            var s = Raise(3, 0, kidsAmongFamily: 2); // family 10 (grown), 11 (kid), 12 (kid): the youngest kid is the last
            var bunch = s.tips.Single(t => t.kind == CampTipKind.Bunch);
            Assert.AreEqual(s.IndexOf(12), bunch.member, "the youngest kid");
            var grown = Raise(2, 0, kidsAmongFamily: 0);
            Assert.AreEqual(grown.IndexOf(10), grown.tips.Single(t => t.kind == CampTipKind.Bunch).member, "no kid: the oldest of the family");
        }

        [Test]
        public void TheRaising_TheFamilyStandsOnTheCamerasSide_AndTheKidsKeepBackFromTheCarver()
        {
            var s = Raise(3, 0);
            var work = StatueScenes.Front(Spot, ToCam, StatueScenes.WorkAt);
            foreach (int k in new[] { 10, 11, 12 })
            {
                var arrive = s.parts[s.IndexOf(k)].OrderBy(p => p.from).First().pos;
                Assert.Greater(Vector2.Dot((arrive - Spot).normalized, ToCam.normalized), 0.5f, $"{k} stands on the camera's side");
                Assert.GreaterOrEqual((arrive - Spot).magnitude, 0.3f, $"{k}: 0.3 u back at least");
                Assert.GreaterOrEqual((arrive - work).magnitude, 0.24f, $"{k} keeps clear of the carver's chisel");
            }
        }

        [Test]
        public void TheBlock_RollsInOnTheGround_ThenHopsOntoThePlinthAtTheSeat()
        {
            var s = Raise(1, 0);
            var from = s.p0;
            Assert.IsTrue(StatueScenes.BlockNow(s, 0f, Spot, out var at0, out float h0));
            Assert.AreEqual(from.x, at0.x, 1e-4f); Assert.AreEqual(0f, h0, 1e-4f, "on the ground before the roll");
            float mid = (s.p1.x + s.a) * 0.5f;
            Assert.IsTrue(StatueScenes.BlockNow(s, mid, Spot, out var atM, out float hM));
            Assert.Less((atM - Spot).magnitude, (from - Spot).magnitude); Assert.Greater((atM - Spot).magnitude, 0f); Assert.AreEqual(0f, hM, 1e-4f);
            Assert.IsTrue(StatueScenes.BlockNow(s, s.a + 0.12f, Spot, out _, out float hHop));
            Assert.Greater(hHop, 0f); Assert.Less(hHop, PegDoll.StatueFeetY + 0.02f, "hopping up");
            Assert.IsFalse(StatueScenes.BlockNow(s, s.a + 0.3f, Spot, out _, out _), "then it is the plinth's block");
        }

        // ---------- the uses ----------

        [Test]
        public void TheClimb_StepsOnlyOnTheTwoSteps_NeverTheFigure_WithAGrownUpWithin06()
        {
            var s = StatueScenes.Climb(1, Spot + new Vector2(1f, -1f), 2, Spot + new Vector2(-1f, -1f), 3, Spot + new Vector2(0.5f, -1.5f), Spot, ToCam, 0f);
            AssertContinuous(s);
            Assert.Less(s.Length, 14f);
            float maxLift = 0f;
            foreach (var (p, t, pose) in Poses(s, s.IndexOf(1))) maxLift = Mathf.Max(maxLift, pose.lift);
            Assert.AreEqual(2f * PegDoll.StatueStepH, maxLift, 1e-3f, "0.06 u in all: the two steps, never the figure");
            Assert.LessOrEqual(maxLift, PegDoll.StatueFeetY + 1e-3f, "feet on the steps only");
            foreach (var p in s.parts[s.IndexOf(1)]) Assert.GreaterOrEqual((p.pos - Spot).magnitude, 0.04f, "a kid never stands in the figure");
            // the grown-up stands within 0.6 u for as long as the kid is up
            var g = s.parts[s.IndexOf(2)].Last();
            Assert.LessOrEqual((g.pos - Spot).magnitude, 0.6f);
            float up = float.MaxValue, down = 0f;
            foreach (var (part, t, pose) in Poses(s, s.IndexOf(1))) if (pose.lift > 1e-3f) { up = Mathf.Min(up, t); down = Mathf.Max(down, t); }
            Assert.Less(up, float.MaxValue, "the kid does climb");
            var gFirst = s.parts[s.IndexOf(2)].OrderBy(p => p.from).First(p => (p.pos - Spot).magnitude <= 0.6f);
            Assert.LessOrEqual(gFirst.from, up, "the grown-up is within 0.6 u before the kid climbs");
            Assert.GreaterOrEqual(g.to, down, "and until the kid is down");
            Assert.LessOrEqual(StatueScenes.GrownNear, 0.6f);
        }

        [Test]
        public void TheClimb_WithNoKidBelow_StillPlays()
        {
            var s = StatueScenes.Climb(1, Spot + new Vector2(1f, -1f), 2, Spot + new Vector2(-1f, -1f), VillageScenes.Nobody, default, Spot, ToCam, 0f);
            Assert.AreEqual(2, s.Count);
            AssertContinuous(s);
        }

        [Test]
        public void TheNod_NeverStopsTheWalkLongerThan15Seconds()
        {
            var s = StatueScenes.Nod(1, Spot + new Vector2(0.3f, 0f), false, Spot, ToCam, 0f);
            Assert.LessOrEqual(s.Length, 1.5f);
            AssertContinuous(s);
            var knew = StatueScenes.Nod(1, Spot + new Vector2(0.3f, 0f), true, Spot, ToCam, 0f);
            Assert.LessOrEqual(knew.Length, 1.5f, "touching the plinth fits the same 1.5 s");
            Assert.Greater(knew.Length, s.Length - 0.2f);
            AssertContinuous(knew);
            Assert.IsTrue(Poses(knew, 0).Any(x => x.pose.armR.x >= 69f), "someone who knew them puts a hand out (x 70)");
            Assert.IsFalse(Poses(s, 0).Any(x => x.pose.armR.x >= 69f), "the others only nod");
        }

        [Test]
        public void TheSit_IsOnTheLowerStep_ForTheSecondsAsked_AndTheSecondSitsBeside()
        {
            var side = new Vector2(0.6f, 0.8f);
            var alone = StatueScenes.Sit(1, Spot + new Vector2(1f, 1f), VillageScenes.Nobody, default, Spot, side, 8f, 0f);
            AssertContinuous(alone);
            Assert.AreEqual(1, alone.Count);
            float seated = 0f;
            foreach (var (p, t, pose) in Poses(alone, 0)) if (p.style == DollStyle.Sit) { Assert.AreEqual(PegDoll.StatueStepH, pose.lift, 1e-4f, "on the lower step"); seated += 0.1f; }
            Assert.AreEqual(8f + 0.5f, seated, 1.5f, "the seconds asked (and the hand on the knee)");
            var pair = StatueScenes.Sit(1, Spot + new Vector2(1f, 1f), 2, Spot + new Vector2(-1f, 1f), Spot, side, 8f, 0f);
            AssertContinuous(pair);
            Assert.AreEqual(2, pair.Count);
            Assert.IsTrue(Poses(pair, 0).Any(x => x.pose.armR.x >= 100f), "the first points up at the figure");
            Assert.Less(pair.Length, 40f);
            // the sitter's back is to the plinth: they face away from it
            var seat = alone.parts[0].First(p => p.style == DollStyle.Sit);
            Assert.Greater(Vector2.Dot((seat.face - seat.pos).normalized, side), 0.9f);
        }

        [Test]
        public void TheFlowers_AreLaidAtTheFoot_TheBowedStandBy_AndWinterBringsASprig()
        {
            var s = StatueScenes.Flowers(1, Spot + new Vector2(1f, -1f), new[] { 2, 3 }, new[] { Spot + new Vector2(-1f, -1f), Spot + new Vector2(-1.2f, -0.8f) }, false, Spot, ToCam, 0f);
            AssertContinuous(s);
            Assert.AreEqual(3, s.Count);
            Assert.Greater(s.a, 1f); Assert.Less(s.a, s.Length);
            Assert.AreEqual(0, s.kind); Assert.AreEqual(1, StatueScenes.Flowers(1, Spot, new int[0], new Vector2[0], true, Spot, ToCam, 0f).kind, "winter: a sprig");
            var bunch = s.tips.Single(t => t.kind == CampTipKind.Bunch);
            Assert.AreEqual(0f, bunch.darken); Assert.Greater(StatueScenes.Flowers(1, Spot, new int[0], new Vector2[0], true, Spot, ToCam, 0f).tips[0].darken, 0.5f);
            Assert.IsTrue(Poses(s, 1).Any(x => x.pose.lean >= 11f), "the head bowed");
            Assert.IsTrue(Poses(s, 1).Any(x => x.pose.armR.x >= 69f), "then a hand on the plinth");
            Assert.IsTrue(Poses(s, 0).Any(x => x.pose.armR.x >= 49f && x.pose.squash < 0.8f), "the bearer kneels with a hand on the foot");
            Assert.Less(s.Length, 30f);
        }

        [Test]
        public void TheSweep_BrushesTheFeetTwiceForThreeSeconds_WithABroom()
        {
            var s = StatueScenes.Sweep(1, Spot + new Vector2(1f, -1f), Spot, ToCam, 0f);
            AssertContinuous(s);
            var brush = s.parts[0].Single(p => p.style == DollStyle.Sweep);
            Assert.AreEqual(3f, brush.Len, 1e-3f); Assert.AreEqual(PropKind.Broom, brush.prop);
            Assert.AreEqual(2, s.fx.Count(f => f.kind == CampFxKind.Dust), "two strokes");
            Assert.Less((brush.pos - Spot).magnitude, 0.3f, "at the plinth's feet");
        }

        [Test]
        public void TheUses_AreAppendedLast_AndTheStatueKindsAreAllNamed()
        {
            var all = System.Enum.GetValues(typeof(CampUse)).Cast<CampUse>().ToList();
            Assert.AreEqual(CampUse.LastEvening, all.Max(), "#789 appended after the statue's uses");
            Assert.AreEqual(5, all.Count(u => u >= CampUse.StatueRaise && u <= CampUse.StatueSweep) - 1, "five uses after the raising");
            foreach (var u in new[] { CampUse.StatueClimb, CampUse.StatueFlowers, CampUse.StatueSit, CampUse.StatueNod, CampUse.StatueSweep })
                Assert.IsTrue(u > CampUse.StatueRaise);
        }
    }
}
