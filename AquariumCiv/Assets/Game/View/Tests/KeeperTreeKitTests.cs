using NUnit.Framework;
using UnityEngine;

namespace AquariumCiv.View.Tests
{
    /// <summary>The Keeper's tree models (#317): every stage builds, stays in a sane size, and glows only at night.</summary>
    public class KeeperTreeKitTests
    {
        [Test]
        public void EveryStage_BuildsWithSaneVertexCounts_AndGrows()
        {
            int prev = 0;
            for (int stage = 0; stage <= 4; stage++)
            {
                var mb = new MeshBuilder(5);
                Assert.DoesNotThrow(() => KeeperTreeKit.Build(mb, Vector3.zero, stage, 0, LandColors.River), "stage " + stage);
                Assert.Greater(mb.VertexCount, 100, "stage " + stage + " has a ring, a mound and a plant");
                Assert.Less(mb.VertexCount, 3000, "stage " + stage + " stays cheap");
                Assert.AreEqual(mb.verts.Count, mb.lamps.Count);
                Assert.GreaterOrEqual(mb.VertexCount, prev - 150, "later stages are not simpler");
                prev = mb.VertexCount;
                float top = 0f, reach = 0f;
                foreach (var v in mb.verts) { top = Mathf.Max(top, v.y); reach = Mathf.Max(reach, new Vector2(v.x, v.z).magnitude); }
                Assert.Less(top, 1.0f, "in proportion with the box");
                Assert.Less(reach, 0.4f);
                Assert.IsFalse(float.IsNaN(top));
            }
        }

        [Test]
        public void Stages_AreDistinctSilhouettes_TallerEachTime()
        {
            float last = 0f;
            for (int stage = 0; stage <= 4; stage++)
            {
                var mb = new MeshBuilder(5);
                KeeperTreeKit.Build(mb, Vector3.zero, stage, 0, LandColors.River);
                float top = 0f;
                foreach (var v in mb.verts) top = Mathf.Max(top, v.y);
                Assert.Greater(top, last, "stage " + stage + " stands higher than the one before");
                last = top;
            }
        }

        [Test]
        public void NeedsAndOutOfRangeStages_StillBuild()
        {
            foreach (int need in new[] { 0, 1, 3 })
                foreach (int stage in new[] { -2, 0, 2, 4, 9 })
                {
                    var mb = new MeshBuilder(2);
                    Assert.DoesNotThrow(() => KeeperTreeKit.Build(mb, new Vector3(0.3f, 0.1f, -0.2f), stage, need, LandColors.River));
                    Assert.Greater(mb.VertexCount, 50);
                }
        }

        [Test]
        public void Glow_IsOffAtNoon_OnAtMidnight_AndEasesThroughTheDusk()
        {
            Assert.AreEqual(0f, KeeperTreeKit.Glow(12f), 1e-6f);
            Assert.AreEqual(0f, KeeperTreeKit.Glow(8f), 1e-6f);
            Assert.Greater(KeeperTreeKit.Glow(0f), 0.99f);
            Assert.Greater(KeeperTreeKit.Glow(23f), 0.99f);
            float a = KeeperTreeKit.Glow(18f), b = KeeperTreeKit.Glow(19f);
            Assert.Greater(a, 0f);
            Assert.Less(a, b, "rising through the dusk");
            Assert.Less(KeeperTreeKit.Glow(6f), KeeperTreeKit.Glow(5f), "falling through the dawn");
        }

        [Test]
        public void GlowIsOnlyOnTheCanopyBuds_AndFaint()
        {
            var mb = new MeshBuilder(5);
            KeeperTreeKit.Build(mb, Vector3.zero, 4, 0, LandColors.River);
            int glowing = 0;
            foreach (var l in mb.lamps)
                if (l.y > 1.5f) { glowing++; Assert.AreEqual(1f, l.x); Assert.LessOrEqual(l.y - 2f, 0.5f + 1e-4f, "never a full lamp"); }
            Assert.Greater(glowing, 20);
            Assert.Less(glowing, mb.VertexCount, "the stones and the trunk stay dark");
        }
    }
}
