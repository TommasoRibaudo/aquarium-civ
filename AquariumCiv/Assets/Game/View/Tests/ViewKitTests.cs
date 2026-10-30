using System.Linq;
using AquariumCiv.Sim;
using NUnit.Framework;
using UnityEngine;

namespace AquariumCiv.View.Tests
{
    /// <summary>The style kit's mesh writers, land, peg dolls and a full box build (EditMode).</summary>
    public class ViewKitTests
    {
        [Test]
        public void Box_WritesFiveFacesWithoutBottom_SixWithBottom()
        {
            var mb = new MeshBuilder();
            mb.Box(Vector3.zero, Vector3.one, Color.white);
            Assert.AreEqual(30, mb.VertexCount);
            mb.Box(Vector3.zero, Vector3.one, Color.white, bottom: true);
            Assert.AreEqual(30 + 36, mb.VertexCount);
            Assert.AreEqual(mb.VertexCount, mb.tris.Count, "flat shading: every corner is its own vertex");
        }

        [Test]
        public void Prism_And_Ico_VertexCounts()
        {
            var mb = new MeshBuilder();
            mb.Prism(Vector3.zero, 1f, 0.5f, 1f, 8, Color.white); // 8 side quads + 8 top-cap tris
            Assert.AreEqual(8 * 6 + 8 * 3, mb.VertexCount);
            var cone = new MeshBuilder();
            cone.Prism(Vector3.zero, 1f, 0f, 1f, 5, Color.white); // a cone: 5 side tris, no cap
            Assert.AreEqual(15, cone.VertexCount);
            var ico = new MeshBuilder();
            ico.Ico(Vector3.zero, 1f, Vector3.one, Color.white);
            Assert.AreEqual(60, ico.VertexCount);
        }

        [Test]
        public void DegenerateTriangle_IsSkipped_AndChannelsStayAligned()
        {
            var mb = new MeshBuilder();
            mb.Tri(Vector3.zero, Vector3.zero, Vector3.one, Color.white);
            Assert.AreEqual(0, mb.VertexCount);
            Pieces.House(mb, Vector3.zero, 0.6f, 0.45f, 0.3f, 0.25f, Palette.Plaster, Palette.Thatch, true);
            Assert.AreEqual(mb.verts.Count, mb.normals.Count);
            Assert.AreEqual(mb.verts.Count, mb.colors.Count);
            Assert.AreEqual(mb.verts.Count, mb.uvs.Count);
            Assert.AreEqual(mb.verts.Count, mb.lamps.Count);
        }

        [Test]
        public void HouseWindows_AreLamps_WallsAreNot()
        {
            var mb = new MeshBuilder();
            Pieces.House(mb, Vector3.zero, 0.6f, 0.45f, 0.3f, 0.25f, Palette.Plaster, Palette.Thatch, false, true, 1, 0.4f);
            int lamp = mb.lamps.Count(l => l.x > 0.5f);
            Assert.AreEqual(3 * 30, lamp, "three windows (front, two sides), one box each");
            Assert.IsTrue(mb.lamps.Where(l => l.x > 0.5f).All(l => Mathf.Approximately(l.y, 0.4f)), "lamp phase carried");
            Assert.Less(lamp, mb.VertexCount);
        }

        [Test]
        public void Fire_IsEmissive_AndSways()
        {
            var mb = new MeshBuilder();
            Pieces.Firepit(mb, Vector3.zero, false);
            Assert.IsTrue(mb.uvs.Any(u => u.y >= 0.99f && u.x > 1f), "flames glow fully and sway");
        }

        [Test]
        public void Shoreline_SitsAtTheWaterLevel_AroundThePond()
        {
            var land = Land.StyleKit();
            var shore = land.Shoreline(64);
            Assert.AreEqual(64, shore.Length);
            foreach (var p in shore)
            {
                if (!land.Inside(p, 0.05f)) continue; // where the pond meets the glass
                Assert.AreEqual(Land.WaterY, land.Height(p.x, p.y), 0.01f, $"shore point {p}");
            }
            Assert.IsTrue(BoxView.InPolygon(shore, land.pond));
            Assert.IsFalse(BoxView.InPolygon(shore, land.centre));
        }

        [Test]
        public void StyleKitLand_MatchesTheSpikeTerrain()
        {
            // The spike's StyleBox.Height at a few points (copied values: hills back-left, pond, flat centre).
            var land = Land.StyleKit();
            Assert.Less(land.Height(1.75f, -1.15f), Land.WaterY, "pond");
            Assert.AreEqual(Land.G + 0.03f, land.Height(-0.35f, -0.25f), 0.02f, "flat founding spot");
            Assert.Greater(land.Height(-2.6f, 2.0f), Land.G + 0.3f, "hills at the back left");
        }

        [Test]
        public void PegDoll_IsAboutAPegDollTall_KidsSmaller_CampHasAccent()
        {
            var look = PegDoll.PickLook(5, camp: true, kid: false, accent: -1);
            Assert.Greater(look.accent.a, 0.5f, "camp people always get a bright headband");
            Assert.AreEqual(0f, PegDoll.PickLook(5, camp: false, kid: false, accent: -1).accent.a, "village people only on request");

            var adult = new MeshBuilder(); PegDoll.Body(adult, look, 1f);
            var kid = new MeshBuilder(); PegDoll.Body(kid, look, PegDoll.KidScale);
            float top = adult.verts.Max(v => v.y), kidTop = kid.verts.Max(v => v.y);
            Assert.That(top, Is.InRange(0.2f, 0.25f));
            Assert.AreEqual(top * PegDoll.KidScale, kidTop, 0.01f);
            Assert.AreEqual(0f, adult.verts.Min(v => v.y), 1e-4f, "feet on the origin");
        }

        [Test]
        public void EveryPropKind_BuildsAMesh()
        {
            foreach (PropKind k in System.Enum.GetValues(typeof(PropKind)))
            {
                var m = PegDoll.PropMesh(k);
                if (k == PropKind.None) Assert.IsNull(m);
                else Assert.Greater(m.vertexCount, 0, k.ToString());
            }
        }

        [Test]
        public void EveryPieceKind_DrawsInEveryEra()
        {
            foreach (PieceKind k in System.Enum.GetValues(typeof(PieceKind)))
                foreach (Era e in System.Enum.GetValues(typeof(Era)))
                {
                    var mb = new MeshBuilder();
                    Pieces.Draw(mb, new PieceData { kind = k, era = e, w = 0.5f, d = 0.45f, seed = 3 }, q => q);
                    Assert.Greater(mb.VertexCount, 0, $"{k} in {e}");
                }
        }

        [Test]
        public void BoxBuild_PaintsPaths_BakesAOInRange_AndSpawnsPeople()
        {
            var go = new GameObject("TestBox");
            go.SetActive(false);
            try
            {
                var box = go.AddComponent<BoxView>();
                box.layout.Add(PieceKind.Fire, -0.35f, -0.25f, era: Era.Camp);
                box.layout.House(1.0f, 1.0f, 180f, 0.6f, 0.45f, Era.Village);
                box.layout.Path(PathTier.Path, new Vector2(0.2f, 0.1f), new Vector2(1.0f, 0.1f));
                box.layout.Villager(new Vector2(0f, -0.6f), 0f, Era.Camp, 1, prop: PropKind.Axe);
                box.layout.autoNature = false;
                go.SetActive(true); // builds

                var onPath = box.GroundColor(0.6f, 0.1f, Land.G, out _);
                var grass = box.GroundColor(-2.0f, -1.6f, Land.G, out _);
                Assert.AreNotEqual(onPath, grass);
                Assert.Greater(onPath.r, onPath.g * 0.95f, "path ground is sandy, not green");

                var alphas = box.Root.Cast<Transform>().Where(t => t.name.StartsWith("Chunk"))
                    .SelectMany(t => t.GetComponent<MeshFilter>().sharedMesh.colors).Select(c => c.a).ToArray();
                Assert.AreEqual(BoxView.ChunkCount, box.Root.Cast<Transform>().Count(t => t.name.StartsWith("Chunk")));
                Assert.That(alphas.Min(), Is.GreaterThanOrEqualTo(0.14f));
                Assert.That(alphas.Max(), Is.LessThanOrEqualTo(1f));
                Assert.Less(alphas.Min(), 0.95f, "something is occluded");

                Assert.AreEqual(1, box.Dolls.Count);
                Assert.IsNotNull(box.Dolls[0].prop, "the axe is in the hand slot");
                Assert.IsNotNull(box.Root.Find("Glows"), "the fire glows");
                Assert.AreEqual(4, box.Root.Cast<Transform>().Count(t => t.name.StartsWith("Glass")));
            }
            finally { Object.DestroyImmediate(go); }
        }
    }
}
