using AquariumCiv.Life;
using NUnit.Framework;
using UnityEngine;

namespace AquariumCiv.View.Tests
{
    /// <summary>#357: the pure parts of the contact puffs and target shakes (HitFxMath.cs): the pool limits, the kind mapping from a stroke, the
    /// shake decay, the lifetime curve of a puff and its trajectories.</summary>
    public class HitFxTests
    {
        static PuffPool Fill(int n, double at = 0.0)
        {
            var pool = new PuffPool();
            for (int i = 0; i < n; i++) pool.Spawn(PuffKind.Chips, new Vector3(i, 1f, 0f), Vector3.forward, 1f, at, i, PuffOptions.Default);
            return pool;
        }

        [Test]
        public void Pool_25HitsInOneFrame_Keep24_AndDropTheOldest()
        {
            var pool = Fill(25);
            Assert.AreEqual(PuffPool.MaxPuffs, pool.Live);
            Assert.AreEqual(1, pool.Dropped);
            Assert.AreEqual(25, pool.Spawned);
            bool oldest = false, newest = false;
            for (int i = 0; i < pool.SlotCount; i++)
            {
                if (!pool[i].live) continue;
                if (Mathf.Approximately(pool[i].bits[0].p0.x, 0f)) oldest = true;
                if (Mathf.Approximately(pool[i].bits[0].p0.x, 24f)) newest = true;
            }
            Assert.IsFalse(oldest, "the oldest puff was dropped");
            Assert.IsTrue(newest);
        }

        [Test]
        public void Pool_NeverHoldsMoreThan160Bits()
        {
            var pool = new PuffPool();
            for (int i = 0; i < 60; i++)
            {
                var o = PuffOptions.Default;
                o.count = 10;
                pool.Spawn(PuffKind.Droplets, Vector3.zero, Vector3.forward, 1f, 0.0, i, o);
                Assert.LessOrEqual(pool.LiveBits, PuffPool.MaxBits);
                Assert.LessOrEqual(pool.Live, PuffPool.MaxPuffs);
            }
            Assert.AreEqual(16, pool.Live, "160 bits / 10 per puff");
        }

        [Test]
        public void Pool_FreesFinishedPuffs()
        {
            var pool = Fill(5, 10.0);
            pool.Step(10.1);
            Assert.AreEqual(5, pool.Live);
            pool.Step(10.0 + 0.7 + 0.01);
            Assert.AreEqual(0, pool.Live, "chips live 0.7 s");
            Assert.AreEqual(0, pool.LiveBits);
        }

        [Test]
        public void Spawn_ReusesSlots_NoNewPuffObjects()
        {
            var pool = Fill(24);
            var first = pool[0];
            pool.Spawn(PuffKind.Dust, Vector3.zero, Vector3.forward, 1f, 1.0, 99, PuffOptions.Default);
            Assert.AreSame(first, pool[0], "the slot objects are made up front and reused");
        }

        [TestCase(DollStyle.Chop, PlaceKind.Woods, PuffKind.Chips, ShakeTarget.Tree)]
        [TestCase(DollStyle.Chop, PlaceKind.ForestEdge, PuffKind.Chips, ShakeTarget.Tree)]
        [TestCase(DollStyle.Chop, PlaceKind.Home, PuffKind.Chips, ShakeTarget.None)]
        [TestCase(DollStyle.Chop, PlaceKind.Outcrop, PuffKind.Sparks, ShakeTarget.Stone)]
        [TestCase(DollStyle.Hammer, PlaceKind.BuildingSite, PuffKind.Dust, ShakeTarget.Frame)]
        [TestCase(DollStyle.Hammer, PlaceKind.Smithy, PuffKind.Sparks, ShakeTarget.None)]
        [TestCase(DollStyle.Hoe, PlaceKind.Field, PuffKind.Dust, ShakeTarget.None)]
        [TestCase(DollStyle.SitStrike, PlaceKind.Fire, PuffKind.Sparks, ShakeTarget.Stone)]
        [TestCase(DollStyle.Stomp, PlaceKind.Meadow, PuffKind.Dust, ShakeTarget.None)]
        public void Resolve_MapsStrokeAndPlaceToAKindAndATarget(DollStyle style, PlaceKind place, PuffKind kind, ShakeTarget shake)
        {
            var a = HitFx.Resolve(style, place);
            Assert.IsTrue(a.hasPuff);
            Assert.AreEqual(kind, a.puff);
            Assert.AreEqual(shake, a.shake);
        }

        [TestCase(DollStyle.Stomp, PlaceKind.Puddle)]
        [TestCase(DollStyle.Chop, PlaceKind.Pond)]
        [TestCase(DollStyle.Fish, PlaceKind.Stream)]
        public void Resolve_Water_IsDropletsAndARing(DollStyle style, PlaceKind place)
        {
            var a = HitFx.Resolve(style, place);
            Assert.AreEqual(PuffKind.Droplets, a.puff);
            Assert.IsTrue(a.ring);
        }

        [Test]
        public void Resolve_ABush_SwaysAndDropsLeaves_TheGlassKnockDoesNothing()
        {
            var a = HitFx.Resolve(DollStyle.Pick, PlaceKind.BushCell);
            Assert.AreEqual(ShakeTarget.Bush, a.shake);
            Assert.Greater(a.leaves, 0);
            Assert.IsTrue(HitFx.Resolve(DollStyle.Knock, PlaceKind.Glass).IsNone);
            Assert.IsTrue(HitFx.Resolve(DollStyle.Walk, PlaceKind.Home).IsNone);
        }

        [Test]
        public void ShakeDecay_StartsAtZero_Peaks_AndDiesBelow6Percent()
        {
            var bush = ShakeSpec.Bush;
            Assert.AreEqual(11f, bush.degrees); Assert.AreEqual(6f, bush.hz); Assert.AreEqual(0.18f, bush.tau);
            Assert.AreEqual(0f, ShakeMath.Wobble(bush, 0f));
            Assert.AreEqual(0f, ShakeMath.Wobble(bush, -1f), "nothing before the hit");
            float peak = 0f;
            for (float t = 0.001f; t < 0.1f; t += 0.001f) peak = Mathf.Max(peak, Mathf.Abs(ShakeMath.Wobble(bush, t)));
            Assert.Greater(peak, 0.4f, "a visible first swing");
            Assert.AreEqual(0.5f, bush.Seconds, 0.02f, "about 0.5 s visible (spec 0.5)");
            Assert.AreEqual(0f, ShakeMath.Wobble(bush, bush.Seconds + 0.01f));
            float Env(float t) { float m = 0f; for (float u = t; u < t + 1f / bush.hz; u += 0.002f) m = Mathf.Max(m, Mathf.Abs(ShakeMath.Wobble(bush, u))); return m; }
            Assert.Less(Env(0.35f), Env(0.05f), "the envelope decays");
        }

        [Test]
        public void ShakeFalloff_IsOneAtTheCentre_AndNothingFarOff_AndSwayLeansWithHeight()
        {
            Assert.AreEqual(1f, ShakeMath.Falloff(0f, 0.1f), 1e-5f);
            Assert.Less(ShakeMath.Falloff(0.3f, 0.1f), 0.001f);
            var s = ShakeSpec.Tree;
            float low = Mathf.Abs(ShakeMath.SwayOffset(s, 0.06f, 0.1f, 0f)), high = Mathf.Abs(ShakeMath.SwayOffset(s, 0.06f, 0.4f, 0f));
            Assert.AreEqual(low * 4f, high, 1e-5f, "a rotation about the base: the offset grows with the height");
            Assert.AreEqual(0f, ShakeMath.SwayOffset(s, 0.06f, -0.2f, 0f), "nothing below the base");
        }

        [Test]
        public void Jolt_DropsFirst_AndComesBack()
        {
            var j = ShakeSpec.Jolt;
            Assert.AreEqual(0f, ShakeMath.JoltOffset(j, 0f));
            float min = 0f;
            for (float t = 0.001f; t < 0.2f; t += 0.001f) min = Mathf.Min(min, ShakeMath.JoltOffset(j, t));
            Assert.Less(min, -0.004f, "it drops");
            Assert.GreaterOrEqual(min, -0.0121f, "no further than 0.012 u");
            Assert.AreEqual(0f, ShakeMath.JoltOffset(j, j.Seconds + 0.05f));
        }

        [Test]
        public void ShakeSet_FillsTheArrays_ExpiresTheDead_AndKeepsTheNewestWhenFull()
        {
            var set = new ShakeSet();
            set.Add(ShakeSpec.Bush, new Vector3(1f, 0f, 2f), 0.9f, new Vector3(3f, 0f, 0f), 10.0);
            set.Add(ShakeSpec.Tree, new Vector3(-1f, 0f, 0f), 0.9f, Vector3.zero, 10.0, 0.3f);
            Assert.AreEqual(2, set.Step(10.1));
            Assert.AreEqual(1f, set.A[0].x); Assert.AreEqual(2f, set.A[0].y);
            Assert.AreEqual(0.1f, set.A[0].z, 1e-4f);
            Assert.AreEqual(1f, set.C[0].x, 1e-5f, "the direction is a unit vector");
            Assert.Less(set.A[1].z, 0f, "the delayed one waits (negative age)");
            Assert.AreEqual(Mathf.Tan(7f * Mathf.Deg2Rad), set.A[0].w, 1e-5f);
            Assert.AreEqual(1, set.Step(10.0 + ShakeSpec.Bush.Seconds + 0.01), "the bush is done; the delayed tree is not");
            for (int i = 0; i < ShakeSet.Max + 4; i++) set.Add(ShakeSpec.Bush, new Vector3(i, 0f, 0f), 0f, Vector3.right, 20.0);
            Assert.AreEqual(ShakeSet.Max, set.Step(20.05));
            Assert.AreEqual(0, set.Step(30.0));
            Assert.AreEqual(-1f, set.A[0].z, "unused slots wait");
        }

        [Test]
        public void Lifetime_BitsKeepTheirSizeThenShrinkToNothingOverTheLast40Percent()
        {
            Assert.AreEqual(1f, HitFxMath.Shrink(0f, 1f));
            Assert.AreEqual(1f, HitFxMath.Shrink(0.6f, 1f));
            Assert.AreEqual(0.5f, HitFxMath.Shrink(0.8f, 1f), 1e-4f);
            Assert.AreEqual(0f, HitFxMath.Shrink(1f, 1f));
            Assert.AreEqual(0f, HitFxMath.Shrink(-0.1f, 1f));
            float prev = 1f;
            for (float t = 0.6f; t <= 1f; t += 0.01f) { float s = HitFxMath.Shrink(t, 1f); Assert.LessOrEqual(s, prev + 1e-6f); prev = s; }
        }

        static PuffBit[] Bits(PuffKind kind, int seed, PuffOptions o, out int n, Vector3? at = null, Vector3? dir = null)
        {
            var bits = new PuffBit[PuffSpec.MaxBitsPerPuff];
            n = HitFxMath.Launch(kind, at ?? new Vector3(0f, 1f, 0f), dir ?? Vector3.forward, 1f, seed, o, bits);
            return bits;
        }

        [Test]
        public void Launch_BitCountsFollowTheTable_AndAreDeterministic()
        {
            for (int seed = 0; seed < 40; seed++)
            {
                Bits(PuffKind.Chips, seed, PuffOptions.Default, out int chips); Assert.That(chips, Is.InRange(5, 7));
                Bits(PuffKind.Dust, seed, PuffOptions.Default, out int dust); Assert.That(dust, Is.InRange(3, 4));
                Bits(PuffKind.Sparks, seed, PuffOptions.Default, out int sparks); Assert.AreEqual(5, sparks, "the flash star and 4 diamonds");
                Bits(PuffKind.Droplets, seed, PuffOptions.Default, out int drops); Assert.That(drops, Is.InRange(4, 10));
                Bits(PuffKind.Ring, seed, PuffOptions.Default, out int rings); Assert.AreEqual(2, rings);
                Bits(PuffKind.Grass, seed, PuffOptions.Default, out int grass); Assert.AreEqual(4, grass);
            }
            var a = Bits(PuffKind.Chips, 5, PuffOptions.Default, out int na);
            var b = Bits(PuffKind.Chips, 5, PuffOptions.Default, out int nb);
            Assert.AreEqual(na, nb);
            Assert.AreEqual(a[0].v0, b[0].v0);
        }

        [Test]
        public void Chips_FlyAwayFromTheCutFace_ThenLieOnTheGround()
        {
            var bits = Bits(PuffKind.Chips, 3, PuffOptions.Default, out int n, new Vector3(0f, 1f, 0f), Vector3.right);
            for (int i = 0; i < n; i++)
            {
                var b = bits[i];
                Assert.Greater(b.v0.x, 0.3f, "away from the cut face (+x)");
                Assert.Greater(b.v0.y, 0.3f, "up");
                var late = HitFxMath.Position(b, 0.69f);
                Assert.AreEqual(b.groundY, late.y, 1e-5f, "it lies on the ground by the end");
                Assert.AreEqual(Vector3.zero, HitFxMath.Velocity(b, 0.69f));
                Assert.AreEqual(late, HitFxMath.Position(b, 0.6f), "a landed chip stays put");
                Assert.AreEqual(HitFxMath.Roll(b, 0.69f), HitFxMath.Roll(b, 0.6f), 1e-3f, "and stops spinning");
            }
        }

        [Test]
        public void Sparks_KnapFalls_FireFloatsUp_AndTheFlashStarIsFirst()
        {
            var knap = Bits(PuffKind.Sparks, 1, PuffOptions.Default, out int n);
            Assert.AreEqual(PuffShape.Star, knap[0].shape);
            Assert.AreEqual(0.07f, knap[0].life, 1e-5f);
            Assert.AreEqual(0.35f, knap[1].life, 1e-5f);
            Assert.Greater(knap[1].gravity, 0f);
            var fire = Bits(PuffKind.Sparks, 1, new PuffOptions { fire = true, groundY = float.NaN }, out n);
            Assert.AreEqual(0.7f, fire[1].life, 1e-5f);
            Assert.Less(fire[1].gravity, 0f, "the fire sparks float up");
            Assert.Greater(HitFxMath.Position(fire[1], 0.5f).y, HitFxMath.Position(fire[1], 0.1f).y);
        }

        [Test]
        public void Droplets_AimedAtAPoint_ReachItInThreeTenthsOfASecond()
        {
            var aim = new Vector3(0.4f, 1.1f, -0.2f);
            var o = new PuffOptions { hasAim = true, aim = aim, groundY = -10f };
            var bits = Bits(PuffKind.Droplets, 4, o, out int n, new Vector3(0f, 1f, 0f));
            for (int i = 0; i < n; i++)
                Assert.Less(Vector3.Distance(HitFxMath.Position(bits[i], 0.3f), aim), 0.04f, "within the aim jitter");
        }

        [Test]
        public void Ring_GrowsWithTheOutCurve_ThinsItsLine_AndTheSecondComesLater()
        {
            var bits = Bits(PuffKind.Ring, 2, PuffOptions.Default, out int n);
            Assert.AreEqual(2, n);
            Assert.AreEqual(0f, bits[0].delay); Assert.AreEqual(0.15f, bits[1].delay, 1e-5f);
            Assert.AreEqual(0.03f, HitFxMath.Size(bits[0], 0f), 1e-5f);
            Assert.AreEqual(0.20f, HitFxMath.Size(bits[0], 0.7999f), 1e-3f);
            Assert.AreEqual(0.7f * 0.20f, HitFxMath.Size(bits[1], 0.7999f), 1e-3f, "the second ring is 0.7x");
            Assert.AreEqual(0f, HitFxMath.Size(bits[1], -0.01f), "not yet");
            Assert.Greater(HitFxMath.Line(bits[0], 0.05f), HitFxMath.Line(bits[0], 0.6f));
            Assert.AreEqual(0f, HitFxMath.Line(bits[0], 0.8f));
        }

        [Test]
        public void Dust_BlobsGrowFrom002To005_ThenShrink()
        {
            var bits = Bits(PuffKind.Dust, 9, PuffOptions.Default, out int n);
            Assert.AreEqual(0.02f, HitFxMath.Size(bits[0], 0f), 1e-5f);
            Assert.Greater(HitFxMath.Size(bits[0], 0.25f), 0.04f);
            Assert.Less(HitFxMath.Size(bits[0], 0.44f), HitFxMath.Size(bits[0], 0.27f), "shrinking at the end");
            Assert.AreEqual(0f, HitFxMath.Size(bits[0], 0.45f));
        }

        [Test]
        public void Scale_MultipliesEveryLength()
        {
            var one = new PuffBit[PuffSpec.MaxBitsPerPuff]; var two = new PuffBit[PuffSpec.MaxBitsPerPuff];
            HitFxMath.Launch(PuffKind.Chips, Vector3.zero, Vector3.forward, 1f, 8, PuffOptions.Default, one);
            HitFxMath.Launch(PuffKind.Chips, Vector3.zero, Vector3.forward, 1.2f, 8, PuffOptions.Default, two);
            Assert.AreEqual(one[0].size * 1.2f, two[0].size, 1e-6f);
            Assert.AreEqual(one[0].v0.magnitude * 1.2f, two[0].v0.magnitude, 1e-5f);
            Assert.AreEqual(one[0].gravity * 1.2f, two[0].gravity, 1e-5f);
        }

        [Test]
        public void Shapes_AreBuiltOnce_AndWellFormed()
        {
            foreach (PuffShape s in System.Enum.GetValues(typeof(PuffShape)))
            {
                if (s == PuffShape.Ring) continue;
                var o = HitFxShapes.Unit(s);
                Assert.AreSame(o, HitFxShapes.Unit(s));
                Assert.Greater(o.t.Length, 2);
                Assert.AreEqual(0, o.t.Length % 3);
                foreach (int i in o.t) Assert.That(i, Is.InRange(0, o.v.Length - 1));
            }
            var verts = new System.Collections.Generic.List<Vector3>(); var tris = new System.Collections.Generic.List<int>();
            HitFxShapes.RingTris(0.1f, 0.01f, Vector3.zero, verts, tris);
            Assert.AreEqual(HitFxShapes.RingSides * 2, verts.Count);
            Assert.AreEqual(HitFxShapes.RingSides * 6, tris.Count);
        }
    }
}
