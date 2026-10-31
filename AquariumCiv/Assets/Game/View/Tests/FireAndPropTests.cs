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
        public void FireLooks_EveryLookHasASpec_AndTheCycleVisitsThemAll()
        {
            Assert.AreEqual(System.Enum.GetValues(typeof(FireLook)).Length, FireLooks.All.Length);
            var look = FireLooks.All[0];
            var seen = new System.Collections.Generic.HashSet<FireLook>();
            for (int i = 0; i < FireLooks.All.Length; i++) { Assert.IsNotNull(FireLooks.Spec(look)); seen.Add(look); look = FireLooks.Next(look); }
            Assert.AreEqual(FireLooks.All.Length, seen.Count);
            Assert.AreEqual(FireLooks.All[0], look, "wraps round");
        }

        [TestCase("current", FireLook.Current)]
        [TestCase("Soft", FireLook.Soft)]
        [TestCase("a", FireLook.Soft)]
        [TestCase(" EMBERS ", FireLook.Embers)]
        [TestCase("b", FireLook.Embers)]
        [TestCase("paper", FireLook.Paper)]
        [TestCase("C", FireLook.Paper)]
        public void FireLooks_ParseByNameOrIssueLetter(string name, FireLook want)
        {
            Assert.IsTrue(FireLooks.TryParse(name, out var look));
            Assert.AreEqual(want, look);
        }

        [Test]
        public void FireLooks_UnknownNameIsRefused_AndTheDefaultIsEmbers()
        {
            Assert.IsFalse(FireLooks.TryParse("lava", out _));
            Assert.IsFalse(FireLooks.TryParse(null, out _));
            Assert.AreEqual(FireLook.Embers, FireLooks.Default, "the user picked Embers (#65)");
        }

        [Test]
        public void FireLooks_StayInBudget_AndLeaveTheBuilderClean()
        {
            foreach (var look in FireLooks.All)
            {
                var spec = FireLooks.Spec(look);
                var mb = new MeshBuilder(3);
                Pieces.FireBody(mb, Vector3.zero, 0.37f, look);
                Assert.Greater(mb.VertexCount, 0, look.ToString());
                Assert.LessOrEqual(mb.VertexCount, spec.bodyVertexBudget, look.ToString());
                Assert.LessOrEqual(spec.GlowQuads, FireLooks.GlowQuadBudget, look.ToString());
                Assert.LessOrEqual(spec.smokePuffs, 4, "smoke is alpha overdraw: never more than the #63 fire");
                Assert.AreEqual(0f, mb.emission, look + " leaves no emission on for the next part");
                Assert.AreEqual(0f, mb.flameHeight, look + " leaves the flame flag off");
            }
        }

        [Test]
        public void FireLooks_ThePitsDifferByLook_AndNeverOutgrowThe63PitByMuch()
        {
            int Pit(FireLook look) { var mb = new MeshBuilder(3); Pieces.Firepit(mb, Vector3.zero, true, 0.37f, look); return mb.VertexCount; }
            int baseline = Pit(FireLook.Current);
            foreach (var look in FireLooks.All) Assert.LessOrEqual(Pit(look), baseline + 30, look.ToString());
            Assert.AreNotEqual(Pit(FireLook.Soft), Pit(FireLook.Paper));
        }

        [TestCase(FireLook.Embers)]
        [TestCase(FireLook.Paper)]
        public void FireLooks_MeshFlames_AreUnlit_SoDayAndNightMatch(FireLook look)
        {
            var mb = new MeshBuilder(3);
            Pieces.FireBody(mb, Vector3.zero, 0.37f, look);
            int flames = 0;
            for (int i = 0; i < mb.lamps.Count; i++)
            {
                if (mb.lamps[i].x < 2f) continue;
                flames++;
                Assert.AreEqual(1f, mb.uvs[i].y, 1e-5f, "emission");
                Assert.GreaterOrEqual(mb.lamps[i].y, 0f); Assert.Less(mb.lamps[i].y, 1f);
            }
            Assert.Greater(flames, 0);
        }

        [Test]
        public void Hot_ComesOutAsThePaletteColour_ThroughTheEmissionBoost()
        {
            foreach (var c in new[] { Palette.FlameCoreA, Palette.FlameBodyA, Palette.FlameBodyB })
            {
                var shown = MeshBuilder.Linear(Pieces.Hot(c));
                var want = MeshBuilder.Linear(c);
                Assert.AreEqual(want.r, shown.r * Pieces.EmissionBoost, want.r * 0.12f + 0.01f);
                Assert.AreEqual(want.g, shown.g * Pieces.EmissionBoost, want.g * 0.12f + 0.01f);
            }
        }

        [Test]
        public void PaperTongue_FacesOutwards_AndKeepsItsHeight()
        {
            var mb = new MeshBuilder(3);
            var foot = new Vector3(0.1f, 0.2f, -0.1f);
            Pieces.PaperTongue(mb, foot, 40f, 0.3f, 0.06f, 0.07f, Color.red, Color.red, Color.red);
            Assert.AreEqual(24, mb.VertexCount, "8 faces");
            float top = float.MinValue;
            var belly = foot + Quaternion.Euler(0, 40f, 0) * new Vector3(0, 0.135f, 0.021f);
            for (int t = 0; t < mb.tris.Count; t += 3)
            {
                Vector3 a = mb.verts[mb.tris[t]], b = mb.verts[mb.tris[t + 1]], c = mb.verts[mb.tris[t + 2]];
                top = Mathf.Max(top, a.y, Mathf.Max(b.y, c.y));
                Assert.Greater(Vector3.Dot(Vector3.Cross(b - a, c - a), (a + b + c) / 3f - belly), 0f, "face " + t / 3);
            }
            Assert.AreEqual(foot.y + 0.3f, top, 1e-4f);
        }

        [Test]
        public void SoftTongues_EndOnTheBigOne_InTheMiddle()
        {
            int n = FireLooks.Spec(FireLook.Soft).softTongues;
            Assert.Greater(n, 1);
            FireLooks.SoftTongue(n - 1, n, 0f, out float dx, out float dz, out float w, out float h);
            Assert.AreEqual(0f, dx); Assert.AreEqual(0f, dz);
            for (int i = 0; i < n - 1; i++)
            {
                FireLooks.SoftTongue(i, n, 1f, out _, out _, out float wi, out float hi);
                Assert.Less(wi, w); Assert.Less(hi, h, "drawn first, so the big one covers their feet");
            }
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
