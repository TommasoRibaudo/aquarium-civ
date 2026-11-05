using System.Collections.Generic;
using AquariumCiv.Life;
using AquariumCiv.View;
using NUnit.Framework;
using UnityEngine;

namespace AquariumCiv.View.Tests
{
    /// <summary>#332: no log seats round the fire, and the hide rack keeps clear of the fire and builds what it should.</summary>
    public class CampRackTests
    {
        [Test]
        public void Firepit_HasNoLogSeats_NothingStandsOutPastTheStones()
        {
            foreach (var look in FireLooks.All)
            {
                var mb = new MeshBuilder(3);
                Pieces.Firepit(mb, Vector3.zero, true, 0.37f, look);
                float far = 0f;
                foreach (var v in mb.verts) far = Mathf.Max(far, new Vector2(v.x, v.z).magnitude);
                Assert.Less(far, 0.3f, look + ": the seats sat 0.4 out; now the pit ends at its stones");
            }
        }

        [Test]
        public void LogSeatPiece_DrawsNothing_SoOldSavesLoad()
        {
            var mb = new MeshBuilder(3);
            var piece = new PieceData { kind = PieceKind.LogSeat, w = 0.3f, d = 0.1f, era = AquariumCiv.Sim.Era.Camp };
            Pieces.Draw(mb, piece, v => v);
            Assert.AreEqual(0, mb.VertexCount);
        }

        [Test]
        public void HideRack_StaysInsideItsFootprint_AndGrowsWithItsPile()
        {
            
            var a = new MeshBuilder(1); Pieces.HideRack(a, 3, 0, 1);
            var b = new MeshBuilder(1); Pieces.HideRack(b, 3, 3, 1);
            Assert.Greater(b.VertexCount, a.VertexCount, "the folded hides add geometry");
            foreach (var v in b.verts)
            {
                Assert.LessOrEqual(Mathf.Abs(v.x), Pieces.RackHalfDepth + 1e-3f, "across the bar");
                Assert.LessOrEqual(Mathf.Abs(v.z), Pieces.RackHalfLength(2) + 0.05f, "along the bar");
                Assert.GreaterOrEqual(v.y, -0.01f);
            }
        }

        [Test]
        public void HideRack_StandsClearOfTheFire_AndTheSittersRing()
        {
            float nearest = LifePlanner.HideFrameDistance - Pieces.RackHalfDepth;
            Assert.Greater(nearest, CrowdAvoid.FireRadius + 0.08f, "room to walk and sit between the fire and the rack");
            Assert.AreEqual(0, CampView.PileFor(2));
            Assert.AreEqual(1, CampView.PileFor(3));
            Assert.AreEqual(3, CampView.PileFor(8));
        }
    }
}
