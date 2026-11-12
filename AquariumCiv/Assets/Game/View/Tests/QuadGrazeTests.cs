using NUnit.Framework;
using UnityEngine;

namespace AquariumCiv.View.Tests
{
    /// <summary>#504: the deer's graze (head to the grass, chewing, up to look round with the ears turning) and its fuller body. Pure poses and
    /// transforms, no scene.</summary>
    public class QuadGrazeTests
    {
        static QuadPose Graze(float since, int seed = 0)
        {
            var p = QuadPose.Stand(Vector3.zero, 0f);
            QuadMotion.Graze(ref p, since, since, seed);
            return p;
        }

        [Test]
        public void TheCycleGoesDownThenUpThenDownAgain()
        {
            Assert.That(QuadMotion.GrazeDown(2f), Is.EqualTo(1f));
            Assert.That(QuadMotion.GrazeDown(7.5f), Is.EqualTo(0f));
            Assert.That(QuadMotion.GrazeDown(6.3f), Is.GreaterThan(0.05f));
            Assert.That(QuadMotion.GrazeDown(6.3f), Is.LessThan(0.95f));
            Assert.That(QuadMotion.GrazeDown(10.5f), Is.EqualTo(1f));
        }

        [Test]
        public void TheMuzzleReachesTheGrassThenLiftsToLook()
        {
            var mat = new Material(Shader.Find("Hidden/InternalErrorShader"));
            var root = new GameObject("graze").transform;
            try
            {
                foreach (var kind in new[] { QuadKind.Stag, QuadKind.Doe, QuadKind.Fawn })
                {
                    var d = new QuadDoll(kind, root, mat);
                    d.Apply(Graze(2f));
                    float down = d.MuzzleWorld.y, shoulder = d.look.shoulder;
                    d.Apply(Graze(7.5f));
                    float up = d.MuzzleWorld.y;
                    Assert.That(down, Is.LessThan(shoulder * 0.28f), kind + ": the muzzle is at the grass");
                    Assert.That(up, Is.GreaterThan(shoulder * 0.8f), kind + ": the head is up to look round");
                    d.Destroy();
                }
            }
            finally { Object.DestroyImmediate(root.gameObject); Object.DestroyImmediate(mat); }
        }

        [Test]
        public void ItChewsWhileDownAndTheEarsFlick()
        {
            float lo = 99f, hi = -99f;
            for (float t = 1f; t < 1.4f; t += 0.01f) { float h = Graze(t).head; lo = Mathf.Min(lo, h); hi = Mathf.Max(hi, h); }
            Assert.That(hi - lo, Is.GreaterThan(4f), "small chewing bobs");
            Assert.That(hi - lo, Is.LessThan(25f), "but small");
            Assert.That(Graze(3.15f).earR, Is.GreaterThan(20f), "an ear flicks while grazing");
            Assert.That(Graze(7.55f).earL, Is.GreaterThan(20f), "and one while looking round");
            Assert.That(Graze(7.5f).earSwivel, Is.GreaterThan(15f), "the ears turn forward to look");
            float yawLo = 0f, yawHi = 0f;
            for (float t = 6.6f; t < 8.6f; t += 0.05f) { float y = Graze(t).headYaw; yawLo = Mathf.Min(yawLo, y); yawHi = Mathf.Max(yawHi, y); }
            Assert.That(yawHi, Is.GreaterThan(25f)); Assert.That(yawLo, Is.LessThan(-25f));
        }

        [Test]
        public void TheDeerIsFullerThanTheWolfAndTheWolfBuildIsUnchanged()
        {
            var stag = QuadLook.Of(QuadKind.Stag);
            Assert.That(stag.fullness, Is.GreaterThan(0.3f));
            Assert.That(QuadLook.Of(QuadKind.Wolf).fullness, Is.EqualTo(0f));
            Assert.That(QuadLook.Dog(3, 5).fullness, Is.EqualTo(0f));
            Assert.That(stag.legShare, Is.LessThan(0.6f), "shorter legs under a deeper body");
            // body depth against length: was 0.38 * 0.16 / 0.20 = 0.30; now deeper
            Assert.That(stag.BodyH / stag.bodyLen * (1f + stag.fullness * 0.6f), Is.GreaterThan(0.42f));
        }

        [Test]
        public void EarsAreTheirOwnPartsAndTurn()
        {
            var mat = new Material(Shader.Find("Hidden/InternalErrorShader"));
            var root = new GameObject("ears").transform;
            try
            {
                var d = new QuadDoll(QuadKind.Wolf, root, mat);
                var l = d.go.transform.Find("Body/Neck/Head/EarL");
                var r = d.go.transform.Find("Body/Neck/Head/EarR");
                Assert.That(l != null && r != null, Is.True, "ear parts");
                var p = QuadPose.Stand(Vector3.zero, 0f);
                p.earL = 30f; p.earSwivel = 20f;
                d.Apply(p);
                Assert.That(Quaternion.Angle(l.localRotation, r.localRotation), Is.GreaterThan(1f), "one ear flicked");
                Assert.That(Quaternion.Angle(l.localRotation, Quaternion.identity), Is.GreaterThan(20f));
                d.Destroy();
            }
            finally { Object.DestroyImmediate(root.gameObject); Object.DestroyImmediate(mat); }
        }
    }
}
