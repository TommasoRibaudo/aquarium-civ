using AquariumCiv.Life;
using NUnit.Framework;
using UnityEngine;

namespace AquariumCiv.View.Tests
{
    /// <summary>#359: the stamp in a puddle lands (B1.d: HitFx answers it with droplets and a ring), and the B3 play moves pose safely on their own.</summary>
    public class KidPlay2ViewTests
    {
        [Test]
        public void Stomp_EachStampIsAContact_OnceABounce()
        {
            int edges = 0; bool was = false; float last = -1f, minGap = float.MaxValue;
            for (float t = 0f; t < 3f; t += 1f / 120f)
            {
                var p = DollMotion.Evaluate(DollStyle.Stomp, t, 1f, 0, 1, PropSlot.Hand, false);
                bool hit = p.hit > 0.5f;
                if (hit && !was) { edges++; if (last >= 0f) minGap = Mathf.Min(minGap, t - last); last = t; }
                was = hit;
            }
            Assert.That(edges, Is.InRange(5, 8), "a stamp every ~0.45 s");
            Assert.GreaterOrEqual(minGap, 0.3f, "at most one a 0.3 s");
            var ans = HitFx.Resolve(DollStyle.Stomp, PlaceKind.Puddle);
            Assert.AreEqual(PuffKind.Droplets, ans.puff);
            Assert.IsTrue(ans.ring);
        }

        [Test]
        public void SplashWater_WithTheLivingLand_IsOnlyTheEcoPondsCells()
        {
            // #630: a hollow under the pond's level that the eco pond doesn't cover is grass, not water to splash in.
            var land = new Land();
            var p = land.pond;
            float level = land.Height(p.x, p.y) + 0.05f;
            var eco = new EcoLayer { on = true };
            Assert.IsTrue(KidPlayDirector.PondWaterAt(land, null, level, p.x, p.y), "without the eco layer the height alone says water");
            Assert.IsFalse(KidPlayDirector.PondWaterAt(land, eco, level, p.x, p.y), "no eco water cell there: dry");
            Assert.IsFalse(KidPlayDirector.NearestPondWater(land, eco, level, Vector2.zero, out _), "no water cell anywhere: no pond edge");

            int c = AquariumCiv.Sim.Settle.SiteMap.CellAt(p.x, p.y);
            eco.cover[c] = EcoLayer.Water;
            var centre = new Vector2(AquariumCiv.Sim.Settle.SiteMap.CXc(c), AquariumCiv.Sim.Settle.SiteMap.CZc(c));
            Assert.IsTrue(KidPlayDirector.PondWaterAt(land, eco, land.Height(centre.x, centre.y) + 0.05f, centre.x, centre.y), "on the water cell, under the level");
            Assert.IsTrue(KidPlayDirector.NearestPondWater(land, eco, land.Height(centre.x, centre.y) + 0.05f, Vector2.zero, out var at));
            Assert.AreEqual(centre.x, at.x, 1e-4f);
            Assert.AreEqual(centre.y, at.y, 1e-4f);
        }

        [Test]
        public void ThePlayMoves_AreNotPuffedByWorkHit()
        {
            // The B3 games spawn their own puffs (KidPlayDirector.Sites); the generic WorkHit map must not add a second one.
            foreach (var s in new[] { DollStyle.PlaySplash, DollStyle.PlayJump, DollStyle.PlayDuel, DollStyle.PlayCircle, DollStyle.Point })
                foreach (var place in new[] { PlaceKind.Pond, PlaceKind.Puddle, PlaceKind.Fire, PlaceKind.Woods })
                    Assert.IsTrue(HitFx.Resolve(s, place).IsNone, $"{s} at {place}");
        }
    }
}
