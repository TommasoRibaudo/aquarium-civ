using AquariumCiv.Sim;
using NUnit.Framework;
using UnityEngine;

namespace AquariumCiv.View.Tests
{
    /// <summary>#559: the weed patch reads as something to pull: a scatter of thin pointed blades and bent stalks, not a leafy dome.
    /// (There is no trash any more: the heaps were removed in #313, so there is nothing of it to pin.)</summary>
    public class WeedLookTests
    {
        const float DollHeight = 0.11f; // #559 round 2: a patch is low and wide, mesh units (x WeedScale 2.4): the tallest stalk tip is about a doll's height in the box

        [Test]
        public void WeedPatch_IsASpikyScatter_NotADome()
        {
            for (int seed = 0; seed < 8; seed++)
            {
                var m = MessProps.WeedMesh(seed);
                try
                {
                    var b = m.bounds;
                    Assert.Greater(b.size.y, 0.06f, "tall enough to see at the default zoom");
                    Assert.Less(b.max.y, DollHeight, "lower than a doll");
                    Assert.Greater(b.size.x, 0.2f, "it spreads and leans");
                    Assert.Less(m.vertexCount, 600, "mobile budget");

                    // every triangle is a sliver: no face is wider than a blade (a dome's faces are fat)
                    var v = m.vertices; var t = m.triangles;
                    int thin = 0, total = 0;
                    for (int i = 0; i < t.Length; i += 3)
                    {
                        var a = v[t[i]]; var c = v[t[i + 1]]; var d = v[t[i + 2]];
                        float widest = Mathf.Max((a - c).magnitude, Mathf.Max((c - d).magnitude, (d - a).magnitude));
                        float area = Vector3.Cross(c - a, d - a).magnitude * 0.5f;
                        float width = 2f * area / Mathf.Max(widest, 1e-5f); // the triangle's short side across its long one
                        total++;
                        if (width <= MessProps.WeedBladeHalfWidth * 2.2f) thin++;
                    }
                    Assert.GreaterOrEqual(thin / (float)total, 0.9f, $"seed {seed}: nearly every face is a thin blade");
                }
                finally { Object.DestroyImmediate(m); }
            }
        }

        [Test]
        public void WeedPatch_HasManyBladesAndSomeSeedHeads()
        {
            Assert.GreaterOrEqual(MessProps.WeedBlades, 12);
            Assert.GreaterOrEqual(MessProps.WeedStalks, 2);
            var m = MessProps.WeedMesh(3);
            try
            {
                // the blades and stalks are separate pieces: at least WeedBlades * 2 two-triangle blades' worth of faces
                Assert.GreaterOrEqual(m.triangles.Length / 3, MessProps.WeedBlades * 4 + MessProps.WeedStalks * 4);
                // leaning every direction: tips spread on both sides of the middle in x and z
                var v = m.vertices; float minX = 9, maxX = -9, minZ = 9, maxZ = -9;
                foreach (var p in v) if (p.y > 0.035f) { minX = Mathf.Min(minX, p.x); maxX = Mathf.Max(maxX, p.x); minZ = Mathf.Min(minZ, p.z); maxZ = Mathf.Max(maxZ, p.z); }
                Assert.Less(minX, -0.03f); Assert.Greater(maxX, 0.03f); Assert.Less(minZ, -0.03f); Assert.Greater(maxZ, 0.03f);
            }
            finally { Object.DestroyImmediate(m); }
        }
    }
}
