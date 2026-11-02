using System.Collections;
using System.Linq;
using AquariumCiv.Game;
using AquariumCiv.Sim;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AquariumCiv.View.PlayTests
{
    /// <summary>The varied grime (#121) on the real glass: dirty, at home and close, noon and night, every pane can be wiped in Clean
    /// mode's own stroke, and the near-glass fade and the night frost still ride on top. Shots go to Temp/agent.</summary>
    [Category("GrimeLook")]
    public class GrimeLookPlayTests : PlayTestBase
    {
        const int Seed = 20261101;

        [UnityTest]
        public IEnumerator EveryPaneWipesCleanAndTheFadesStillRide()
        {
            yield return OpenSlice(1080, 1920, Seed);
            var sim = GameClock.Instance.Sim;
            var care = CareView.Instance;
            yield return Frames(6);
            Assert.AreEqual(4, care.Masks.Length, "four panes");
            sim.State.grime = 0.8f;
            for (int i = 0; i < 4; i++) care.Masks[i].ResetTo(0.8f);
            yield return Frames(4);
            // every pane its own offset and turn, and they reached the material
            var offsets = Enumerable.Range(0, 4).Select(i => care.PaneOf(i).GetComponent<MeshRenderer>().sharedMaterial.GetVector("_NoiseOffset")).ToList();
            Assert.AreEqual(4, offsets.Distinct().Count(), "each pane sits differently on the detail noise");
            // the near-glass fade (#162) and the night frost go on working over the new layers
            BoxView.NearGlassForce = 1f;
            yield return new WaitForSecondsRealtime(0.6f);
            yield return Shot("grime_fade_near");
            BoxView.NearGlassForce = -1f;
            PeopleView.DebugSetHour(22f);
            yield return Frames(10);
            yield return Shot("grime_night");
            PeopleView.DebugSetHour(11f);
            yield return Frames(6);
            // a wipe with the view's own brush, pane by pane
            for (int i = 0; i < 4; i++)
            {
                var m = care.Masks[i];
                for (int round = 0; round < 6; round++)
                    for (float v = 0f; v <= 1f; v += 0.07f)
                        m.BrushLine(new Vector2(0f, v), new Vector2(1f, v), 0.35f, 0.8f);
                Debug.Log($"[grime] pane {i} after the wipe: {m.Mean:0.000}");
                Assert.Less(m.Mean, CareMath.SpotlessMean, $"pane {i} wipes spotless");
            }
            yield return Frames(4);
            AssertNoErrors("grime");
        }

        /// <summary>Night dims the world, it doesn't thicken the glass: <see cref="MostNightGain"/> is the most a pane's measured opacity may rise over noon.</summary>
        const float MostNightGain = 0.03f;
        /// <summary>A pane at 0.8 stays see-through at its worst spots (p95 of what it covers), and still reads as dirty on average.</summary>
        const float MostPatchOpacity = 0.7f, LeastDirtyOpacity = 0.25f;
        /// <summary>A box that has just opened: light smudges, on average.</summary>
        const float MostNewBoxOpacity = 0.3f;

        [UnityTest, Timeout(180000)]
        public IEnumerator PatchesAreNoThickerAtNightAndNewBoxesAreLight()
        {
            yield return OpenSlice(1080, 1920, Seed, skipFirstRun: false);
            ShowMeDirector.BubblesOn = false;
            var sim = GameClock.Instance.ActiveSim;
            Assert.LessOrEqual(sim.State.grime, FirstRun.StartGrime, "the first box");
            yield return Frames(6);
            GrimeMatrixPlayTests.Opacity(out float mean, out _);
            Debug.Log($"[grime] first box, as it opens: opacity {mean:0.000} at grime {sim.State.grime:0.00}");
            Assert.Less(mean, MostNewBoxOpacity, "the first box opens lightly smudged");

            DayLightDriver.DebugPauseClock(true);
            yield return GrimeMatrixPlayTests.Apply(0.8f, 12f);
            GrimeMatrixPlayTests.Opacity(out float noon, out float noonTop);
            Debug.Log($"[grime] noon at 0.8: opacity {noon:0.000}, p95 {noonTop:0.000}");
            Assert.Greater(noon, LeastDirtyOpacity, "a dirty box reads as dirty");
            Assert.Less(noonTop, MostPatchOpacity);
            foreach (float hour in new[] { 17.5f, 19.5f, 22f })
            {
                yield return GrimeMatrixPlayTests.Apply(0.8f, hour);
                GrimeMatrixPlayTests.Opacity(out float m, out float top);
                Debug.Log($"[grime] {hour} at 0.8: opacity {m:0.000}, p95 {top:0.000}");
                Assert.LessOrEqual(m, noon + MostNightGain, $"hour {hour}: mean");
                Assert.LessOrEqual(top, noonTop + MostNightGain, $"hour {hour}: p95");
            }
            DayLightDriver.DebugPauseClock(false);

            // the next box, with the short run
            Assert.That(BoxCompleteController.DebugComplete(AquariumCiv.Sim.BoxSetting.DesertOasis), Does.StartWith("box completed"));
            yield return Frames(30);
            sim = GameClock.Instance.ActiveSim;
            Assert.AreEqual(1, sim.State.boxIndex);
            Assert.LessOrEqual(sim.State.grime, FirstRun.StartGrime, "box 2");
            GrimeMatrixPlayTests.Opacity(out float next, out _);
            Debug.Log($"[grime] box 2, as it opens: opacity {next:0.000} at grime {sim.State.grime:0.00}");
            Assert.Less(next, MostNewBoxOpacity, "box 2 opens lightly smudged");
            AssertNoErrors("grime hours");
        }

        [UnityTest, Explicit("#121: the repetition shot; pass the tag by hand in BeforeTag when comparing with an older build")]
        public IEnumerator Repetition()
        {
            yield return OpenSlice(1080, 1920, Seed);
            var sim = GameClock.Instance.Sim;
            var care = CareView.Instance;
            yield return Frames(6);
            sim.State.grime = 0.55f;
            for (int i = 0; i < care.Masks.Length; i++) care.Masks[i].ResetTo(0.55f);
            PeopleView.DebugSetHour(12f);
            DayLightDriver.DebugPauseClock(true);
            yield return Frames(10);
            yield return Shot("grime_" + BeforeTag + "_home");
            var rig = Camera.main.GetComponent<BoxCamera>();
            rig.DebugFlyIn(new Vector3(0.6f, 1.4f, -1.2f), AquariumCiv.Lighting.CameraReach.MinZoom);
            yield return new WaitForSecondsRealtime(1.5f);
            yield return Shot("grime_" + BeforeTag + "_close");
            DayLightDriver.DebugPauseClock(false);
        }

        const string BeforeTag = "after";
    }
}
