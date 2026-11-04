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
                    if (p.wall == CrumbWall.Back) Assert.AreEqual(HD, p.hitZ, 1e-4f); // the far pane: -z is the one the player looks through
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

        // ---------------- #210 #211: the bits knocked off on the glass ----------------

        static CrumbFall.Bit[] BitsAt(float x, float z, int seed, out CrumbFall.Plan p)
        {
            p = CrumbFall.For("rain", 2f, x, z, HW, HD, H, seed);
            return CrumbFall.Bits(p, seed);
        }

        [Test]
        public void TheBitsAreSmallAndStartOnTheGlassWhereTheLumpHit()
        {
            foreach (var (x, z) in new[] { (-2.2f, 0.4f), (2.5f, -1f), (0f, 0f) })
                for (int seed = 1; seed <= 20; seed++)
                {
                    var bits = BitsAt(x, z, seed, out var p);
                    Assert.AreEqual(CrumbFall.BitCount, bits.Length);
                    foreach (var b in bits)
                    {
                        Assert.That(b.size, Is.InRange(CrumbFall.BitMin * 0.79f, CrumbFall.BitMax + 1e-4f), "a crumb, not a lump");
                        Assert.LessOrEqual(System.Math.Abs(b.startY - p.hitY), CrumbFall.SpreadUp / 2f + 1e-4f, "at the hit's height");
                        float off = p.wall == CrumbWall.Back ? System.Math.Abs(b.startZ - p.hitZ) : System.Math.Abs(b.startX - p.hitX);
                        Assert.Less(off, b.size, "against the glass");
                        CrumbFall.BitAt(b, 0f, HW, HD, out _, out float y, out _, out _, out _, out _);
                        Assert.AreEqual(b.startY, y, 1e-4f, "it clings first");
                    }
                }
        }

        [Test]
        public void TheyFallWithGravitySpeedingUp()
        {
            var bits = BitsAt(2.5f, -1f, 4, out _);
            foreach (var b in bits)
            {
                float dt = (b.landAt - b.cling) / 6f, lastDrop = 0f, lastY = b.startY;
                for (int k = 1; k <= 5; k++)
                {
                    CrumbFall.BitAt(b, b.cling + k * dt, HW, HD, out _, out float y, out _, out _, out _, out _);
                    float drop = lastY - y;
                    Assert.Greater(drop, lastDrop, "each step falls further than the last: accelerating, not constant");
                    lastDrop = drop; lastY = y;
                }
                CrumbFall.BitAt(b, b.landAt, HW, HD, out _, out float yl, out _, out _, out _, out _);
                Assert.AreEqual(0f, yl, 1e-3f, "down at landAt");
            }
        }

        [Test]
        public void OnLandingTheyScatterFlattenRestAndGo()
        {
            var bits = BitsAt(-2.2f, 0.4f, 9, out var p);
            float spreadLand = 0f, spreadRest = 0f;
            var land = new System.Collections.Generic.List<(float, float)>();
            var rest = new System.Collections.Generic.List<(float, float)>();
            foreach (var b in bits)
            {
                CrumbFall.BitAt(b, b.landAt, HW, HD, out float lx, out _, out float lz, out float f0, out _, out _);
                CrumbFall.BitAt(b, b.landAt + 2f, HW, HD, out float rx, out float ry, out float rz, out float f1, out float s1, out _);
                Assert.Greater((rx - lx) * (rx - lx) + (rz - lz) * (rz - lz), 0.03f * 0.03f, "it slides out from where it landed");
                Assert.AreEqual(0f, ry, 1e-4f, "and settles on the ground");
                Assert.AreEqual(1f, f0, 1e-3f); Assert.AreEqual(CrumbFall.Squashed, f1, 1e-3f, "a little flattened: its smear");
                Assert.AreEqual(1f, s1, 1e-4f, "still there while it rests");
                Assert.Greater(rx, -HW, "inside the glass");
                CrumbFall.BitAt(b, CrumbFall.BitsDone(bits), HW, HD, out _, out _, out _, out _, out float gone, out _);
                Assert.AreEqual(0f, gone, 1e-4f, "gone in the end");
                land.Add((lx, lz)); rest.Add((rx, rz));
            }
            spreadLand = Spread(land); spreadRest = Spread(rest);
            Assert.Greater(spreadRest, spreadLand * 1.3f, $"they scatter: spread {spreadLand:0.000} on landing, {spreadRest:0.000} at rest");
        }

        static float Spread(System.Collections.Generic.List<(float x, float z)> pts)
        {
            float mx = 0f, mz = 0f;
            foreach (var q in pts) { mx += q.x; mz += q.z; }
            mx /= pts.Count; mz /= pts.Count;
            float s = 0f;
            foreach (var q in pts) s += (float)System.Math.Sqrt((q.x - mx) * (q.x - mx) + (q.z - mz) * (q.z - mz));
            return s / pts.Count;
        }

        [Test]
        public void MostlyDoughWithAFewChocChips()
        {
            for (int seed = 1; seed <= 10; seed++)
            {
                var bits = BitsAt(1.5f, 0.3f, seed, out _);
                int chips = 0;
                foreach (var b in bits) if (b.kind == CrumbFall.BitKind.Chip) chips++;
                Assert.AreEqual(CrumbFall.ChipCount, chips);
                Assert.Less(chips * 2, bits.Length, "chips are the minority");
                Assert.Greater(chips, 0, "but there are some");
            }
            var a = BitsAt(1.5f, 0.3f, 3, out _); var c = BitsAt(1.5f, 0.3f, 3, out _);
            Assert.AreEqual(a[5].scatterX, c[5].scatterX, "same plan, same bits");
        }
    }
}
