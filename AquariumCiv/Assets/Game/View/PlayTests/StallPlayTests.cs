using System.Collections;
using System.Linq;
using AquariumCiv.Game;
using AquariumCiv.Sim;
using AquariumCiv.View.ShowMe;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AquariumCiv.View.PlayTests
{
    /// <summary>#154: the town's stalls follow the stock, from the debug cheat, in the real scene.</summary>
    [Category("Stalls")]
    public class StallPlayTests : PlayTestBase
    {
        static SimCore Sim => GameClock.Instance.ActiveSim;

        [UnityTest, Timeout(120000)]
        public IEnumerator TheStallsFillAndEmptyWithTheStock()
        {
            yield return OpenSlice(seed: 1234);
            ShowMeDirector.BubblesOn = false;
            MomentPlayer.DebugGrowVillage(30f);
            yield return Frames(3);
            Sim.ForceNextEra();
            yield return Frames(3);
            SimLayoutSource.DebugSkipYears(10f);
            yield return Frames(6);
            Sim.CleanAll();
            PeopleView.DebugSetHour(10f);
            var v = Object.FindAnyObjectByType<StallProps>();
            Assert.IsNotNull(v, "the stalls have their props");
            yield return new WaitForSecondsRealtime(0.7f);
            Assert.Greater(v.StallCount, 0, "a town with stalls");
            Debug.Log("[stalls] " + StallProps.DebugStock("empty"));
            Assert.AreEqual(0, v.Stocked, "bare counters");
            var cam = Camera.main.GetComponent<BoxCamera>();
            var first = Object.FindAnyObjectByType<BoxView>().layout.pieces.First(p => p.kind == PieceKind.Stall);
            cam.EaseTo(Object.FindAnyObjectByType<BoxView>().transform.TransformPoint(new Vector3(first.x, 0f, first.z)), cam.yaw, 72f, 0.3f, 0.2f); // from high up: the town's roofs stand round the square
            yield return new WaitForSecondsRealtime(0.6f);
            yield return Shot("bundle2-154-empty");
            Debug.Log("[stalls] " + StallProps.DebugStock("half"));
            Assert.Greater(v.Stocked, 0);
            Assert.Less(v.Levels.Sum(), v.StallCount * TownStalls.MaxLevel, "not all full");
            yield return Frames(3);
            yield return Shot("bundle2-154-half");
            Debug.Log("[stalls] " + StallProps.DebugStock("full"));
            Assert.AreEqual(v.StallCount, v.Stocked);
            Assert.IsTrue(v.Levels.All(l => l == TownStalls.MaxLevel), "all heaped");
            yield return Frames(3);
            yield return Shot("bundle2-154-full");
            AssertNoErrors("stalls");
        }
    }
}
