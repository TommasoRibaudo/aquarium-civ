using AquariumCiv.Sim;
using AquariumCiv.Sim.Settle;
using NUnit.Framework;
using UnityEngine;

namespace AquariumCiv.View.Tests
{
    /// <summary>The view's side of the settings (#115) and of the varied grime (#121): one land function, the valley's colours,
    /// a water mesh for every preset, each pane its own pattern and still wipes clean.</summary>
    public class SettingViewTests
    {
        static readonly int[] Presets = { 0, 1, 2, TerrainSpec.DesertOasisPreset, TerrainSpec.MountainVillagePreset };

        [TestCaseSource(nameof(Presets))]
        public void TheDrawnLandIsTheSimsLand(int preset)
        {
            var land = SimLayout.LandFor(preset);
            var spec = TerrainSpec.Preset(preset);
            for (float x = -3f; x <= 3f; x += 0.21f)
                for (float z = -2.2f; z <= 2.2f; z += 0.19f)
                    Assert.AreEqual(Sim.Settle.Terrain.Height(spec, x, z), land.Height(x, z), $"at {x},{z}");
        }

        [TestCaseSource(nameof(Presets))]
        public void EveryPresetHasWaterToDraw(int preset)
        {
            var land = SimLayout.LandFor(preset);
            Assert.Less(land.Height(land.pond.x, land.pond.y), Land.WaterY, "the pond's centre is under water: the shoreline and the water mesh start there");
            var shore = land.Shoreline(64);
            float area = 0f;
            for (int i = 0; i < shore.Length; i++)
            {
                var a = shore[i]; var b = shore[(i + 1) % shore.Length];
                area += a.x * b.y - b.x * a.y;
            }
            Assert.Greater(Mathf.Abs(area) * 0.5f, 0.2f, "water you can see");
            foreach (var p in shore) Assert.IsTrue(land.Inside(p, 0f), $"the shore stays in the box: {p}");
        }

        [Test]
        public void TheValleysColoursAreThePalette()
        {
            var c = LandColors.River;
            var pairs = new (Color, Color)[]
            {
                (c.grassA, Palette.GrassA), (c.grassB, Palette.GrassB), (c.grassC, Palette.GrassC), (c.moss, Palette.Moss), (c.dirt, Palette.Dirt), (c.path, Palette.Path),
                (c.sand, Palette.Sand), (c.sandWet, Palette.SandWet), (c.pondBed, Palette.PondBed), (c.water, Palette.Water), (c.waterDeep, Palette.WaterDeep),
                (c.sprout, Palette.Sprout), (c.leafA, Palette.LeafA), (c.leafB, Palette.LeafB), (c.leafC, Palette.LeafC), (c.pine, Palette.Pine), (c.bark, Palette.Bark),
                (c.rock, Palette.Rock), (c.rockDark, Palette.RockDark), (c.rockLight, Palette.RockLight),
            };
            foreach (var (a, b) in pairs) Assert.AreEqual(b, a);
            Assert.AreEqual(Palette.WallTop, c.Room(Palette.WallTop), "no tint on the valley's room");
            Assert.AreEqual(Color.gray, c.Fog(Color.gray), "nor its fog");
        }

        [TestCase(BoxSetting.DesertOasis)]
        [TestCase(BoxSetting.MountainVillage)]
        public void ASettingsTintKeepsTheNightDark(string setting)
        {
            var c = LandColors.For(SettingLook.For(setting));
            var room = new Color(0.8f, 0.78f, 0.72f, 1f);
            var tinted = c.Room(room);
            Assert.AreNotEqual(room, tinted, "the setting tints the room");
            // The driver scales the room by the hour: what falls with the hour stays in the same ratio to the day, tint or none.
            float night = 0.05f;
            Assert.AreEqual(tinted.r * night, c.Room(room).r * night, 1e-6f);
            var fogDay = c.Fog(new Color(0.8f, 0.8f, 0.8f, 1f)); var fogNight = c.Fog(new Color(0.08f, 0.08f, 0.08f, 1f));
            Assert.AreEqual(fogDay.r * 0.1f, fogNight.r, 1e-4f, "the fog falls with the hour: the tint multiplies");
        }

        [Test]
        public void ThePanesDoNotShareAPattern()
        {
            var size = new Vector2(6f, 2.95f);
            var a = new GlassMask(size, 32f, 8); var b = new GlassMask(size, 32f, 15); var c = new GlassMask(size, 32f, 22);
            Assert.Less(Grime.GrimePatches.Similarity(a.weight, b.weight), 0.6f);
            Assert.Less(Grime.GrimePatches.Similarity(b.weight, c.weight), 0.6f);
            Assert.AreNotEqual(GlassPaneNoise.Offset(8), GlassPaneNoise.Offset(15));
            Assert.AreNotEqual(GlassPaneNoise.Rotation(8), GlassPaneNoise.Rotation(15));
        }

        [TestCase(1), TestCase(8), TestCase(15), TestCase(22), TestCase(20261101)]
        public void AVariedPaneStillWipesSpotless(int seed)
        {
            var m = new GlassMask(new Vector2(6f, 2.95f), 32f, seed);
            m.ResetTo(0.8f);
            Assert.AreEqual(0.8f, m.Mean, 0.2f, "the grime's level holds with clean patches on the pane");
            for (int round = 0; round < 6; round++)
                for (float v = 0f; v <= 1f; v += 0.08f)
                    m.BrushLine(new Vector2(0f, v), new Vector2(1f, v), 0.35f, 0.8f);
            Assert.Less(m.Mean, CareMath.SpotlessMean, "a full wipe reaches spotless");
            Assert.IsTrue(m.CheckCleared(), "and rings the glint");
        }
    }
}
