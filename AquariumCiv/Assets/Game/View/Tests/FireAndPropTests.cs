using AquariumCiv.View;
using NUnit.Framework;
using UnityEngine;

namespace AquariumCiv.View.Tests
{
    /// <summary>The fire pit's budgets and colours, and the props' fit on the doll (#63).</summary>
    public class FireAndPropTests
    {
        [Test]
        public void Flames_StayUnderTheVertexBudget_AndCarryTheFlameFlag()
        {
            var mb = new MeshBuilder(3);
            Pieces.Flames(mb, Vector3.zero, 0.37f);
            Assert.LessOrEqual(mb.VertexCount, Pieces.FlameVertexBudget, "the whole flame is ~120 vertices");
            Assert.Greater(mb.VertexCount, 60);
            Assert.AreEqual(mb.verts.Count, mb.lamps.Count);
            foreach (var l in mb.lamps)
            {
                Assert.GreaterOrEqual(l.x, 2f, "every flame vertex is flagged 2 + height weight in the lamp channel");
                Assert.Less(l.x, 3f);
                Assert.AreEqual(0.37f, l.y, 1e-5f, "with the fire's phase");
            }
            Assert.AreEqual(0f, mb.flameHeight, "the flag is switched off again for the next piece");
        }

        [Test]
        public void Flames_AreTallest_AtTheTip_AndWeightedByHeight()
        {
            var mb = new MeshBuilder(3);
            Pieces.Flames(mb, Vector3.zero, 0.5f);
            float maxY = float.MinValue, minY = float.MaxValue;
            for (int i = 0; i < mb.verts.Count; i++)
            {
                maxY = Mathf.Max(maxY, mb.verts[i].y); minY = Mathf.Min(minY, mb.verts[i].y);
                Assert.AreEqual(Mathf.Clamp01((mb.verts[i].y - mb.flameBaseY) / 0.3f) * 0.99f, mb.lamps[i].x - 2f, 1e-4f + 0.0f, "weight follows height");
            }
            Assert.AreEqual(0.24f + 0.012f, maxY, 0.03f, "the body is 0.24 tall");
            Assert.GreaterOrEqual(maxY - minY, 0.2f);
        }

        [Test]
        public void FlameColours_NeverGoLemon_GreenStaysUnderTwoThirdsOfRed()
        {
            foreach (var c in new[] { Palette.FlameCoreA, Palette.FlameCoreB, Palette.FlameBodyA, Palette.FlameBodyB, Palette.FlameTongueA, Palette.FlameTongueB, Palette.FireHalo, Palette.FireSpark, Palette.FirePool })
                { var l = c.linear; Assert.LessOrEqual(l.g / l.r, 0.65f + 1e-3f, "linear G/R of #" + ColorUtility.ToHtmlStringRGB(c)); }
            // The ramps go from bright at the base to deeper at the tip.
            Assert.Greater(Palette.FlameCoreA.g, Palette.FlameCoreB.g);
            Assert.Greater(Palette.FlameBodyA.g, Palette.FlameBodyB.g);
            Assert.Greater(Palette.FlameTongueA.r, Palette.FlameTongueB.r);
        }

        [Test]
        public void Sash_HugsTheTunic_NothingSticksOutMoreThanFourMillimetres()
        {
            foreach (var s in new[] { 1f, PegDoll.KidScale })
            {
                var mb = new MeshBuilder(1);
                PegDoll.Band(mb, s, PegDoll.BodyH * 0.62f * s, 0.046f * s * Mathf.Sin(28f * Mathf.Deg2Rad), 0.007f * s, 0f, Mathf.PI * 2f, 10, Color.red, 0.003f);
                Assert.Greater(mb.VertexCount, 0);
                foreach (var v in mb.verts)
                {
                    float r = Mathf.Sqrt(v.x * v.x + v.z * v.z);
                    float tunic = PegDoll.TunicRadius(v.y, s);
                    Assert.LessOrEqual(r - tunic, 0.004f * s + 1e-5f, "sticks out by " + (r - tunic));
                    Assert.GreaterOrEqual(r - tunic, 0.002f * s, "and does not sink into the tunic");
                    Assert.Less(v.y, PegDoll.BodyH * s, "stays under the shoulders");
                }
            }
        }

        [Test]
        public void Props_HaveTheirSpecSizes()
        {
            Bounds B(PropKind k) { var m = PegDoll.PropMesh(k); return m.bounds; }
            Assert.AreEqual(0.15f, B(PropKind.Sheaf).size.x, 0.03f, "the sheaf is 0.15 long");
            var bundle = B(PropKind.Bundle).size;
            Assert.AreEqual(0.079f, bundle.x, 0.006f); Assert.AreEqual(0.085f, bundle.y, 0.006f); Assert.AreEqual(0.049f, bundle.z, 0.006f);
            Assert.AreEqual(0.075f, B(PropKind.Pot).size.y, 0.015f, "the pot is 0.075 tall (plus its straw ring)");
            Assert.AreEqual(0.08f, B(PropKind.Pot).size.x, 0.006f, "belly radius 0.04");
            Assert.GreaterOrEqual(B(PropKind.Axe).size.y, 0.2f);
            Assert.GreaterOrEqual(B(PropKind.Hoe).size.y, 0.24f);
        }
    }
}
