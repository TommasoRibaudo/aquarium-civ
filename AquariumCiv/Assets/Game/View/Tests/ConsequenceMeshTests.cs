using AquariumCiv.Sim;
using NUnit.Framework;
using UnityEngine;

namespace AquariumCiv.View.Tests
{
    /// <summary>#371: every consequence prop kind builds a small, non-empty mesh that stands on the ground (code-built, palette-coloured).</summary>
    public class ConsequenceMeshTests
    {
        [Test]
        public void EveryKindBuildsASmallMeshStandingOnTheGround()
        {
            Assert.AreEqual(15, ConsequenceKind.All.Length);
            foreach (var kind in ConsequenceKind.All)
            {
                var mesh = ConsequenceMeshes.Build(kind);
                try
                {
                    Assert.Greater(mesh.vertexCount, 12, kind);
                    Assert.Less(mesh.vertexCount, 4000, kind + ": small");
                    var b = mesh.bounds;
                    Assert.Less(b.size.x, 0.7f, kind); Assert.Less(b.size.z, 0.7f, kind); Assert.Less(b.size.y, 0.7f, kind);
                    Assert.GreaterOrEqual(b.min.y, -0.05f, kind + ": stands on the ground, not under it");
                    Assert.Less(b.min.y, 0.08f, kind + ": and not floating");
                }
                finally { Object.DestroyImmediate(mesh); }
            }
        }

        [Test]
        public void AnUnknownKindStillMakesACairn()
        {
            var mesh = ConsequenceMeshes.Build("nothing");
            Assert.Greater(mesh.vertexCount, 0);
            Object.DestroyImmediate(mesh);
        }
    }
}
