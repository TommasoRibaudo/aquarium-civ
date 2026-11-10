using System.Collections;
using AquariumCiv.Game;
using AquariumCiv.Life;
using AquariumCiv.Sim;
using AquariumCiv.Sim.Eco;
using AquariumCiv.Sim.Settle;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AquariumCiv.View.PlayTests
{
    /// <summary>
    /// #539: settlers arrive while the eco pond is small; the pond then fills to its flood line (the "Pond to full" cheat). The camp, its
    /// fire and its shelter stay on dry ground, and the fire's light pool covers no water cell. Shots at 1179x2556 before and after.
    /// </summary>
    [Category("ApartGroups")]
    public class ApartPondPlayTests : PlayTestBase
    {
        static SimCore Sim => GameClock.Instance.Sim;

        static void AssertDry(SimCore sim, ApartGroup g, string when)
        {
            var view = ApartGroupsView.Instance;
            Assert.IsTrue(view.TryGetCamp(g.id, out var fire, out _, out _), when + ": the camp is drawn");
            Assert.IsFalse(PondShore.IsWet(sim, fire.x, fire.y), when + ": the fire is on dry ground");
            Assert.IsFalse(PondShore.InFutureWater(sim, fire.x, fire.y, 0f), when + ": and outside the pond's full basin");
            Assert.IsTrue(view.TryGetPoolRadius(g.id, out float r), when + ": the fire is lit");
            for (int c = 0; c < EcoLand.N; c++)
            {
                if (sim.EcoCoverAt(c) != EcoCover.Water) continue;
                float d = AquariumCiv.Sim.Settle.Terrain.Dist(SiteMap.CXc(c), SiteMap.CZc(c), fire.x, fire.y);
                Assert.Greater(d, r, when + $": the light pool ({r:0.00}) reaches no water cell (one at {d:0.00})");
            }
        }

        [UnityTest, Timeout(600000)]
        public IEnumerator TheSettlersCampStaysDry_WhenThePondFills()
        {
            UseLivingLand();
            yield return OpenSlice(1179, 2556, seed: 4242);
            Assert.IsTrue(Sim.EcologyOn, "a living-land box");
            ShowMeDirector.BubblesOn = false;
            foreach (bool shore in new[] { false, true })
            {
                StringAssert.Contains("broke the surface", EcoDebug.WaterBreaksNow());
                yield return Frames(10);
                Debug.Log("[539] " + EcoDebug.SettlersArrive(shore));
                var g = Sim.ApartGroups[Sim.ApartGroups.Count - 1];
                PeopleView.DebugSetHour(19.2f);
                Camera.main.GetComponent<BoxCamera>().GoHome(0.01f, true);
                yield return new WaitForSecondsRealtime(2.5f);
                string tag = shore ? "shore" : "woods";
                AssertDry(Sim, g, tag + " small pond");
                ApartGroupsView.DebugFly(g.id, 0.4f);
                yield return new WaitForSecondsRealtime(1.2f);
                yield return Shot($"539-{tag}-small-pond-1179x2556");
                Debug.Log("[539] " + EcoDebug.PondToFull());
                yield return new WaitForSecondsRealtime(5.5f); // the view re-asks the spot every 5 s
                yield return Frames(10);
                Assert.Greater(Sim.EcoPondCells, 100, "the pond filled");
                AssertDry(Sim, g, tag + " full pond");
                yield return Shot($"539-{tag}-full-pond-1179x2556");
                EcoDebug.PondDries();
                yield return Frames(5);
            }
        }
    }
}
