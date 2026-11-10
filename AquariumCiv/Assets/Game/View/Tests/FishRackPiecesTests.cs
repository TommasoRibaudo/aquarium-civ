using AquariumCiv.Life;
using AquariumCiv.Sim;
using NUnit.Framework;
using UnityEngine;

namespace AquariumCiv.View.Tests
{
    /// <summary>#543: the fish basket, the rack and the dried fish are built, sit where the layout says, and the pile's fish fit its extent.</summary>
    public class FishRackPiecesTests
    {
        static Mesh Build(System.Action<MeshBuilder> f)
        {
            var mb = new MeshBuilder(543) { jitter = 0f };
            f(mb);
            return mb.ToMesh("t");
        }

        [Test]
        public void TheFishBasketIsItsOwnShapeAndSizedLikeTheBerryBasket()
        {
            var fish = Build(FishRackPieces.FishBasket);
            var berry = PegDoll.PropMesh(PropKind.Basket);
            Assert.Greater(fish.vertexCount, berry.vertexCount, "more to it: the creel, the handle and the catch");
            Assert.That(fish.bounds.size.x, Is.InRange(0.05f, 0.11f));
            Assert.That(fish.bounds.size.y, Is.InRange(0.05f, 0.11f), "a handle over the mouth");
            var viaProp = PegDoll.PropMesh(PropKind.FishBasket);
            Assert.IsNotNull(viaProp, "the doll can carry it");
            Assert.AreEqual(fish.vertexCount, viaProp.vertexCount);
        }

        [Test]
        public void TheRackHoldsTwelveFishAlongItsBar_EachOnItsOwnCord()
        {
            Assert.AreEqual(SimCore.FishRackMax, FishRackPieces.MaxFish);
            float half = FishRackSpot.HalfLength;
            var seen = new System.Collections.Generic.HashSet<float>();
            for (int i = 0; i < FishRackPieces.MaxFish; i++)
            {
                var p = FishRackPieces.HangPoint(i);
                Assert.LessOrEqual(Mathf.Abs(p.z), half, $"fish {i} hangs on the bar");
                Assert.AreEqual(0f, p.x, 1e-5f);
                Assert.IsTrue(seen.Add(Mathf.Round(p.z * 10000f)), $"fish {i} has its own place");
                if (i > 0) Assert.GreaterOrEqual(Mathf.Abs(p.z - FishRackPieces.HangPoint(i - 1).z), FishRackPieces.Spacing - 1e-5f, "never on top of another");
            }
            int tris = Build(mb => FishRackPieces.Rack(mb, 0)).triangles.Length;
            Assert.Greater(Build(mb => FishRackPieces.Rack(mb, 12)).triangles.Length, tris, "a cord for every fish");
            var one = Build(mb => FishRackPieces.HangingFish(mb, FishRackPieces.HangPoint(0), false, 1));
            Assert.Greater(one.vertexCount, 0);
            Assert.LessOrEqual(one.bounds.size.y, FishRackPieces.FishLength * 1.4f, "about a fish long");
            Assert.Greater(Mathf.Abs(Build(mb => FishRackPieces.HangingFish(mb, Vector3.zero, true, 1)).bounds.size.y - one.bounds.size.y), 0f, "dry fish are shorter");
        }

        [Test]
        public void ThePilesFishFitItsExtent_AndClearTheLogsAndBaskets()
        {
            Assert.AreEqual(CampStash.MaxFish, StashProps.FishSpots.Length);
            foreach (var p in StashProps.FishSpots)
            {
                // each lies along z (turned 90 degrees), its half length 0.026 along z and 0.008 across
                foreach (float sx in new[] { -1f, 1f })
                    foreach (float sz in new[] { -1f, 1f })
                        Assert.LessOrEqual(new Vector2(p.x + sx * FishRackPieces.DriedHalfWidth, p.z + sz * FishRackPieces.DriedHalfLength).magnitude, CampStash.PileExtent, "a fish on the pile");
                foreach (var b in StashProps.BasketSpots)
                    Assert.GreaterOrEqual(new Vector2(p.x - b.x, p.z - b.z).magnitude, StashProps.BasketRadius, "clear of a basket");
                foreach (var h in StashProps.HideSpots)
                    Assert.GreaterOrEqual(Mathf.Abs(p.x - h.x), StashProps.HideHalfX, "clear of the hides");
                Assert.LessOrEqual(p.x + FishRackPieces.DriedHalfWidth, -0.05f + 0.012f, "beside the logs' ends (they run x -0.05 to 0.05)");
            }
        }

        [Test]
        public void TheNewActivitiesHavePosesAndWords()
        {
            foreach (var k in new[] { ActivityKind.HangFish, ActivityKind.TakeDownFish })
            {
                Assert.AreNotEqual(DollStyle.Stand, DollMotion.StyleOf(k), k + " has a move of its own");
                Assert.AreEqual(ActivityGroup.Work, ActivityInfo.GroupOf(k));
                Assert.IsTrue(ActivityInfo.IsBusy(k));
            }
            Assert.AreEqual(AquariumCiv.Life.PropSlot.Arms, ActivityInfo.SlotOf(Prop.FishBasket, ActivityKind.Carry));
        }
    }
}
