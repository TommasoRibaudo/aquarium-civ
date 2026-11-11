using System.Collections;
using System.Linq;
using AquariumCiv.Game;
using AquariumCiv.GameUI;
using AquariumCiv.Life;
using AquariumCiv.Sim;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace AquariumCiv.View.PlayTests
{
    /// <summary>#576 seasons you can see: the cheat moves the real clock, the shader globals follow the month, the title line says the season,
    /// and no renderer is made per season (the look is two globals, not meshes).</summary>
    [Category("Seasons")]
    public class SeasonPlayTests : PlayTestBase
    {
        static SimCore Sim => GameClock.Instance.ActiveSim;

        [Test]
        public void TheWeightsBlendAndSumToOne()
        {
            for (float p = 0f; p < 12f; p += 0.25f)
            {
                var w = SeasonLook.WeightsAt(p);
                Assert.AreEqual(1f, w.x + w.y + w.z + w.w, 1e-4f, "phase " + p);
            }
            Assert.AreEqual(1f, SeasonLook.WeightsAt(6.5f).y, 1e-4f, "mid summer is all summer");
            Assert.AreEqual(1f, SeasonLook.WeightsAt(0.5f).w, 1e-4f, "mid winter is all winter");
            Assert.Greater(SeasonLook.WeightsAt(8f).y, 0f);
            Assert.Greater(SeasonLook.WeightsAt(8f).z, 0f, "the turn from summer to autumn is a blend, not a switch");
            Assert.AreEqual(0f, SeasonLook.SnowAt(SeasonLook.WeightsAt(6.5f)), "no snow in summer");
            Assert.AreEqual(1f, SeasonLook.SnowAt(SeasonLook.WeightsAt(0.5f)), 1e-4f, "full snow in deep winter");
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator SkippingThroughFourSeasonsMovesTheGlobalAndTheWordWithoutNewRenderers()
        {
            UseLivingLand();
            yield return OpenSlice(seed: 1234);
            var year = GameHud.Instance.Root.panel.visualTree.Q<Label>(className: "hud-title-year");
            Assert.IsNotNull(year);
            yield return new WaitForSecondsRealtime(1f);
            int renderers = Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
            int writes = SeasonLook.Writes;
            var seen = new System.Collections.Generic.HashSet<string>();
            for (int i = 0; i < 4; i++)
            {
                StringAssert.Contains("now", EcoDebug.SkipToNextSeason());
                yield return new WaitForSecondsRealtime(0.8f);
                var season = LifeClock.SeasonOf(Sim.State.month);
                var w = SeasonLook.Weights;
                Assert.Greater(new[] { w.x, w.y, w.z, w.w }[(int)season], 0.9f, "the global shows " + season);
                Assert.AreEqual(season == Season.Winter ? 1f : 0f, SeasonLook.Snow, 0.01f, "snow only in deep winter");
                Assert.AreEqual(w.x, Shader.GetGlobalVector("_AC_Season").x, 1e-4f, "the shader global carries it");
                StringAssert.Contains(" · " + season + " · ", year.text, "the title line says the season");
                seen.Add(year.text);
            }
            Assert.AreEqual(4, seen.Count, "four different title lines");
            Assert.AreEqual(renderers, Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length, "no renderer made per season");
            Assert.LessOrEqual(SeasonLook.Writes - writes, 40, "the globals are set when they move, not every frame");
            CloseSlice();
        }
    }
}
