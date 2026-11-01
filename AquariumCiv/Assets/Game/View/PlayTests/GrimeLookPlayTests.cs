using System.Collections;
using System.Linq;
using AquariumCiv.Game;
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
