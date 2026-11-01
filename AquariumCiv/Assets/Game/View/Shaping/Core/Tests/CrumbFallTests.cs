using AquariumCiv.Sim;
using NUnit.Framework;

namespace AquariumCiv.View.Shaping.Tests
{
    /// <summary>#147 phase 1: the crumb's fall as data (timing after the shaping, the wall, the smudge, the last piece).</summary>
    public class CrumbFallTests
    {
        const float HW = 3f, HD = 2.5f, H = 4f;

        [Test]
        public void ItFallsAfterTheHerdHasLandedNotWithIt()
        {
            const float arrive = 4f;
            var p = CrumbFall.For(SimCore.NudgeKind.Herd, arrive, 0.5f, 0.2f, HW, HD, H, 7);
            Assert.GreaterOrEqual(p.delaySeconds, arrive + CrumbFall.BeatAfterHerd - 0.001f);
            Assert.Greater(CrumbFall.Delay(SimCore.NudgeKind.Herd, 5f), CrumbFall.Delay(SimCore.NudgeKind.Rain, 5f));
        }

        [Test]
        public void ItHitsAWallAtAHeightAndLeavesASmudgeThere()
        {
            for (int seed = 1; seed <= 40; seed++)
                foreach (var (x, z) in new[] { (-2.2f, 0.4f), (2.5f, -1f), (0.1f, 1.2f), (0f, 0f) })
                {
                    var p = CrumbFall.For(SimCore.NudgeKind.Rain, 3f, x, z, HW, HD, H, seed);
                    Assert.That(p.hitY, Is.InRange(CrumbFall.HitMin * H - 0.001f, CrumbFall.HitMax * H + 0.001f));
                    Assert.That(p.smudgeU, Is.InRange(0f, 1f)); Assert.That(p.smudgeV, Is.InRange(0f, 1f));
                    Assert.AreEqual(p.hitY / H, p.smudgeV, 1e-4f);
                    Assert.IsTrue(System.Math.Abs(p.hitX) <= HW + 1e-3f && System.Math.Abs(p.hitZ) <= HD + 1e-3f, "the hit is on the glass");
                    if (p.wall == CrumbWall.Left) Assert.AreEqual(-HW, p.hitX, 1e-4f);
                    if (p.wall == CrumbWall.Right) Assert.AreEqual(HW, p.hitX, 1e-4f);
                    if (p.wall == CrumbWall.Back) Assert.AreEqual(-HD, p.hitZ, 1e-4f);
                    Assert.AreEqual(x, p.landX); Assert.AreEqual(z, p.landZ, "it lands at its spot");
                    Assert.Greater(p.smudgeStrength, 0f);
                    Assert.Greater(p.landAt, p.hitAt);
                    if (System.Math.Abs(x) > HW * 0.15f) Assert.AreEqual(x < 0f ? CrumbWall.Left : CrumbWall.Right, p.wall, "the nearer side wall");
                }
        }

        [Test]
        public void TheSamePlanComesBackForTheSameInputs()
        {
            var a = CrumbFall.For("herd", 4f, 1.1f, -0.7f, HW, HD, H, 3);
            var b = CrumbFall.For("herd", 4f, 1.1f, -0.7f, HW, HD, H, 3);
            Assert.AreEqual(a.hitY, b.hitY); Assert.AreEqual(a.wall, b.wall); Assert.AreEqual(a.smudgeU, b.smudgeU);
        }

        [Test]
        public void ThePathRunsFromTheTopThroughTheHitToTheGround()
        {
            var p = CrumbFall.For("rain", 2f, 1.5f, 0.3f, HW, HD, H, 5);
            CrumbFall.At(p, 0f, out _, out float y0, out _);
            Assert.AreEqual(H, y0, 1e-3f);
            CrumbFall.At(p, p.hitAt, out float hx, out float hy, out float hz);
            Assert.AreEqual(p.hitY, hy, 1e-3f); Assert.AreEqual(p.hitX, hx, 1e-3f); Assert.AreEqual(p.hitZ, hz, 1e-3f);
            CrumbFall.At(p, p.landAt + 1f, out float lx, out float ly, out float lz);
            Assert.AreEqual(0f, ly, 1e-3f); Assert.AreEqual(1.5f, lx, 1e-3f); Assert.AreEqual(0.3f, lz, 1e-3f);
            float last = H + 1f;
            for (float t = 0f; t <= p.hitAt; t += 0.02f) { CrumbFall.At(p, t, out _, out float y, out _); Assert.LessOrEqual(y, last + 1e-4f); last = y; }
        }

        [Test]
        public void TheLastPieceShowsOnlyOnceTheLumpIsEaten()
        {
            Assert.IsFalse(CrumbFall.ShowLastPiece(false, -1, 100), "not found yet");
            Assert.IsFalse(CrumbFall.ShowLastPiece(true, 10, 10));
            Assert.IsFalse(CrumbFall.ShowLastPiece(true, 10, 10 + CrumbFall.EatenMonths - 1));
            Assert.IsTrue(CrumbFall.ShowLastPiece(true, 10, 10 + CrumbFall.EatenMonths));
            Assert.AreEqual(1f, CrumbFall.Left(10, 10)); Assert.AreEqual(0f, CrumbFall.Left(10, 500));
        }
    }
}
