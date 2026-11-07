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
        public void ThePlayMoves_AreNotPuffedByWorkHit()
        {
            // The B3 games spawn their own puffs (KidPlayDirector.Sites); the generic WorkHit map must not add a second one.
            foreach (var s in new[] { DollStyle.PlaySplash, DollStyle.PlayJump, DollStyle.PlayDuel, DollStyle.PlayCircle, DollStyle.Point })
                foreach (var place in new[] { PlaceKind.Pond, PlaceKind.Puddle, PlaceKind.Fire, PlaceKind.Woods })
                    Assert.IsTrue(HitFx.Resolve(s, place).IsNone, $"{s} at {place}");
        }
    }
}
