using System.Collections;
using System.Collections.Generic;
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
    /// #495: the fisher stands on dry ground at the edge of the water that is drawn, as the pond grows step by step (the "Pond +1 step"
    /// cheat), and nobody fishes while there is no open water.
    /// </summary>
    [Category("Fishing")]
    public class FisherShorePlayTests : PlayTestBase
    {
        static SimCore Sim => GameClock.Instance.Sim;

        static float NearestWater(SimCore sim, float x, float z)
        {
            float best = float.MaxValue;
            for (int c = 0; c < EcoLand.N; c++)
                if (sim.EcoCoverAt(c) == EcoCover.Water) best = Mathf.Min(best, AquariumCiv.Sim.Settle.Terrain.Dist(SiteMap.CXc(c), SiteMap.CZc(c), x, z));
            return best;
        }

        static int Fishers(PeopleView pv, List<Vector2> feet)
        {
            var who = new List<PersonRef>(); var xs = new List<float>(); var zs = new List<float>();
            pv.StandingNow(who, xs, zs);
            feet.Clear();
            foreach (var a in pv.Activities)
            {
                if (a.kind != ActivityKind.Fish || a.moving || a.enRoute) continue;
                int i = who.FindIndex(w => w.Key == a.who.Key);
                if (i >= 0) feet.Add(new Vector2(xs[i], zs[i]));
            }
            return feet.Count;
        }

        [UnityTest, Timeout(900000)]
        public IEnumerator TheFisherFollowsTheGrowingPond_OnDryGround_AndNoneWithoutWater()
        {
            UseLivingLand();
            yield return OpenSlice(seed: 4242);
            Assert.IsTrue(Sim.EcologyOn, "a living-land box");
            var pv = PeopleView.Find();
            var feet = new List<Vector2>();
            PeopleView.DebugSetHour(10f);
            yield return Frames(30);
            Assert.AreEqual(0, Sim.EcoPondCells, "no pond yet");
            Assert.AreEqual(0, Fishers(pv, feet), "nobody fishes with no open water");
            Assert.IsFalse(Sim.FishingWater);

            StringAssert.Contains("broke the surface", EcoDebug.WaterBreaksNow());
            Assert.GreaterOrEqual(Sim.DebugPlaceNearCamp(LifeSpecies.Ducks), 0, "the ducks placed");
            Assert.GreaterOrEqual(Sim.DebugPlaceNearCamp(LifeSpecies.Fish), 0, "the fish placed");
            yield return Until(() => LifeArrivalView.Instance == null || !LifeArrivalView.Instance.AnyPlaying, 20f);
            PeopleView.DebugSetHour(10f);
            Assert.IsTrue(Sim.FishingWater);

            int prev = 0, checks = 0;
            for (int step = 0; step < 4; step++)
            {
                if (step > 0) StringAssert.Contains("pond grew", EcoDebug.PondStep());
                yield return Frames(20);
                pv.Planner.ClearLive();
                FishingView.DebugBiteNow(); // sends fishers if none are free
                yield return Until(() => Fishers(pv, feet) > 0, 30f);
                yield return Frames(30); // they have settled on their spots
                Assert.Greater(Fishers(pv, feet), 0, $"step {step}: somebody fishes ({Sim.EcoPondCells} cells)");
                foreach (var f in feet)
                {
                    Assert.IsFalse(PondShore.IsWet(Sim, f.x, f.y), $"step {step}: the fisher at ({f.x:0.00},{f.y:0.00}) stands on dry ground");
                    Assert.LessOrEqual(NearestWater(Sim, f.x, f.y), 0.3f, $"step {step}: within 0.3 u of open water");
                    checks++;
                }
                Assert.GreaterOrEqual(Sim.EcoPondCells, prev, "the pond never shrinks");
                prev = Sim.EcoPondCells;
                yield return Shot($"fisher_shore_{step}");
            }
            Assert.Greater(checks, 0);
            Assert.IsEmpty(errors, string.Join("\n", errors));
        }
    }
}
