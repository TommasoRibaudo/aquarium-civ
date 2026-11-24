using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace AquariumCiv.View.Tests
{
    /// <summary>
    /// #708 (B70): the fish logic on a made-up pond that meets the front pane (a wall 0.09 u deep in the middle, 0.04 at its ends):
    /// the count, at most two at the glass, each fish drawn once, side fish inside the wall's outline and never on a shallow wall,
    /// none with no stock, the dart from a ring, the hook, the lure.
    /// </summary>
    public class FishSchoolTests
    {
        const float ZMin = -2.2f, WaterY = 0.92f;

        static FishSchool Pond(float midDepth = 0.09f)
        {
            var s = new FishSchool(7) { HX = 3f, ZMin = ZMin, ZMax = 2.2f, waterY = WaterY };
            s.Water = (x, z) => x > -0.6f && x < 0.6f && z >= ZMin - 0.01f && z < -1.4f;
            s.WallDepth = (side, a) => side != GlassSide.Front ? 0f : Mathf.Abs(a) < 0.45f ? midDepth : Mathf.Abs(a) < 0.6f ? 0.04f : 0f;
            return s;
        }

        static List<Vector2> Inner(FishSchool s)
        {
            var l = new List<Vector2>();
            for (float x = -0.55f; x < 0.6f; x += 0.1f) for (float z = -2.15f; z < -1.4f; z += 0.1f) if (s.Inner(new Vector2(x, z))) l.Add(new Vector2(x, z));
            return l;
        }

        static void Check(FishSchool s, int budget)
        {
            Assert.LessOrEqual(s.fish.Count, budget, "drawn fish within the budget");
            Assert.LessOrEqual(s.SidesNow, FishSchool.MaxOnWalls, "at most two at the glass");
            int onWalls = 0;
            foreach (var f in s.fish)
            {
                if (f.SideDrawn) onWalls++;
                if (f.mode == FishSchool.Mode.Shadow) Assert.AreEqual(0f, f.sideScale, "a shadow is not also on the wall");
                if (f.mode == FishSchool.Mode.Wall) Assert.AreEqual(0f, f.shadowScale, "a side fish is not also a shadow");
                if (f.ShadowDrawn) Assert.IsTrue(s.Water(f.at.x, f.at.y), $"a shadow on the water ({f.at})");
                if (!f.SideDrawn) continue;
                Assert.IsTrue(s.WallFits(f.side, f.along), $"a side fish on a wall 0.06 u deep ({f.along:0.000})");
                float depth = s.WallDepth(f.side, f.along);
                var p = s.SidePosition(f);
                Assert.LessOrEqual(p.y + FishSchool.SideTop, WaterY - FishSchool.UnderLine + 1e-4f, "under the water line");
                Assert.GreaterOrEqual(p.y - FishSchool.SideBottom, WaterY - depth + FishSchool.OverCut - 1e-4f, "above the soil cut");
            }
            Assert.LessOrEqual(onWalls, FishSchool.MaxOnWalls);
        }

        [Test]
        public void TheSchool_KeepsItsRules_OverTenMinutes()
        {
            var s = Pond();
            var inner = Inner(s);
            Assert.Greater(inner.Count, 10, "the made-up pond has room");
            s.SetCount(5, inner[inner.Count / 2], inner);
            Assert.AreEqual(5, s.fish.Count);
            float t = 0f;
            for (int i = 0; i < 30 * 600; i++)
            {
                s.Step(1f / 30f, t += 1f / 30f);
                Check(s, 5);
                if (i == 3000) s.ToGlass(2, out _, out _);
            }
            Assert.Greater(s.HandOffs, 0, "fish went down to the glass and came back");
            Assert.AreEqual(0, s.DrawnTwice, "never drawn twice outside a hand-off");
        }

        [Test]
        public void ToGlass_SendsTwo_ToTheFrontPane()
        {
            var s = Pond();
            var inner = Inner(s);
            s.SetCount(5, inner[inner.Count / 2], inner);
            int n = s.ToGlass(2, out var side, out var focus);
            Assert.AreEqual(2, n);
            Assert.AreEqual(GlassSide.Front, side);
            float t = 0f;
            for (int i = 0; i < 30; i++) { s.Step(1f / 30f, t += 1f / 30f); Check(s, 5); }
            Assert.AreEqual(2, s.SidesNow, "two side fish after the 0.3 s hand-off");
            Assert.AreEqual(0, s.ToGlass(2, out _, out _), "no third");
        }

        [Test]
        public void AShallowWall_HasNoSideFish()
        {
            var s = Pond(0.05f);
            var inner = Inner(s);
            s.SetCount(5, inner[inner.Count / 2], inner);
            Assert.AreEqual(0, s.ToGlass(2, out _, out _), "no wall 0.06 u deep");
            float t = 0f;
            for (int i = 0; i < 30 * 120; i++) { s.Step(1f / 30f, t += 1f / 30f); Assert.AreEqual(0, s.SidesNow); }
        }

        [Test]
        public void NoStock_NoFish()
        {
            var s = Pond();
            var inner = Inner(s);
            s.SetCount(0, inner[0], inner);
            s.Step(0.1f, 0.1f);
            Assert.AreEqual(0, s.fish.Count);
            Assert.AreEqual(0, s.ShadowsNow + s.SidesNow);
            Assert.AreEqual(0, s.ToGlass(2, out _, out _));
        }

        [Test]
        public void ARing_MakesTheNearFishDart_ThenTheyRegroup()
        {
            var s = Pond();
            var inner = Inner(s);
            s.SetCount(5, inner[inner.Count / 2], inner);
            float t = 0f;
            for (int i = 0; i < 60; i++) s.Step(1f / 30f, t += 1f / 30f);
            var f = s.fish[0];
            var ring = f.at + new Vector2(0.05f, 0f);
            float before = (f.at - ring).magnitude;
            Assert.Greater(s.Startle(ring), 0, "someone darted");
            for (int i = 0; i < 9; i++) s.Step(1f / 30f, t += 1f / 30f);
            Assert.Greater((f.at - ring).magnitude, before + 0.08f, "it went off from the ring in 0.25 s");
            for (int i = 0; i < 30 * 8; i++) s.Step(1f / 30f, t += 1f / 30f);
            Assert.Less((f.at - s.Centre).magnitude, 0.45f, "back with the school");
        }

        [Test]
        public void BeforeABite_AFishGoesToTheHook_AndTheNibbleDoesNotScareIt()
        {
            var s = Pond();
            var inner = Inner(s);
            s.SetCount(5, inner[inner.Count / 2], inner);
            float t = 0f;
            for (int i = 0; i < 30; i++) s.Step(1f / 30f, t += 1f / 30f);
            var hook = new Vector2(0.3f, -1.6f);
            Assert.IsTrue(s.ToHook(hook, 1f));
            Assert.IsFalse(s.ToHook(hook, 1f), "one fish per hook");
            for (int i = 0; i < 13; i++) s.Step(1f / 30f, t += 1f / 30f);
            Assert.AreEqual(0, s.Startle(hook), "the nibble's ring is the biting fish's own");
            bool near = false;
            foreach (var f in s.fish) if ((f.at - hook).magnitude < 0.04f) near = true;
            Assert.IsTrue(near, "a fish noses the hook");
        }

        [Test]
        public void ALure_DrawsAShadowClose()
        {
            var s = Pond();
            var inner = Inner(s);
            s.SetCount(5, inner[inner.Count / 2], inner);
            var lure = new Vector2(-0.5f, -1.5f);
            float t = 0f;
            for (int i = 0; i < 30 * 30; i++) { s.lures.Clear(); s.lures.Add(lure); s.Step(1f / 30f, t += 1f / 30f); }
            float best = float.MaxValue;
            foreach (var f in s.fish) best = Mathf.Min(best, (f.at - lure).magnitude);
            Assert.Less(best, 0.05f, "a shadow came to the kid's hand");
        }
    }
}
