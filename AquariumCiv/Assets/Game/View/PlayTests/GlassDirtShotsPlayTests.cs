using System.Collections;
using AquariumCiv.Game;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AquariumCiv.View.PlayTests
{
    /// <summary>#775: the glass at clean, half dirty and dirty, at the home view and flown in close (where the near pane fades), for the look compare.
    /// Explicit: a shot run by hand (category GlassDirtShots). Shots go to Temp/agent/775/.</summary>
    [Category("GlassDirtShots")]
    public class GlassDirtShotsPlayTests : PlayTestBase
    {
        const string Tag = "after";

        [UnityTest, Explicit("#775: look compare shots"), Timeout(240000)]
        public IEnumerator Shots6()
        {
            yield return OpenSlice(1179, 2556, 20261101);
            var sim = GameClock.Instance.Sim;
            var care = CareView.Instance;
            DayLightDriver.DebugPauseClock(true);
            PeopleView.DebugSetHour(12f);
            yield return Frames(6);
            var rig = Camera.main.GetComponent<BoxCamera>();
            var homeTarget = rig.target; var homeZoom = rig.zoom;
            foreach (var (name, level) in new[] { ("clean", 0f), ("half", 0.5f), ("dirty", 1f) })
            {
                sim.State.grime = level;
                for (int i = 0; i < care.Masks.Length; i++) care.Masks[i].ResetTo(level);
                yield return Frames(10);
                yield return Shot($"775/{Tag}_{name}_home");
                rig.DebugFlyIn(new Vector3(0.6f, 1.4f, -1.2f), AquariumCiv.Lighting.CameraReach.MinZoom);
                yield return new WaitForSecondsRealtime(2f);
                yield return Shot($"775/{Tag}_{name}_zoom");
                rig.DebugFlyIn(homeTarget, homeZoom);
                yield return new WaitForSecondsRealtime(2.5f);
            }
            DayLightDriver.DebugPauseClock(false);
        }
    }
}
