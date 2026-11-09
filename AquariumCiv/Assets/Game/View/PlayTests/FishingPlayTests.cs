using System.Collections;
using System.Collections.Generic;
using AquariumCiv.Game;
using AquariumCiv.Life;
using AquariumCiv.Sim;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AquariumCiv.View.PlayTests
{
    /// <summary>
    /// #364 B8 fishing pays off, through the real cheat ("Fish bite now"): two grown-ups go to the pond's real edge, nibble, yank, and a fish flips out of
    /// the water into the basket beside them (the basket shows it); the yank's splash sends a nearby frog hopping. Screenshots go to Temp/agent/fish_*.png.
    /// </summary>
    [Category("Fishing")]
    public class FishingPlayTests : PlayTestBase
    {
        static SimCore Sim => GameClock.Instance.Sim;

        [UnityTest, Timeout(900000)]
        public IEnumerator TheCheatSendsFishers_AndAFishFlipsIntoTheBasket()
        {
            UseLivingLand();
            yield return OpenSlice(seed: 4242);
            Assert.IsTrue(Sim.EcologyOn, "a living-land box");
            StringAssert.Contains("broke the surface", EcoDebug.WaterBreaksNow());
            // P9 (#421): no fishing before fish are placed: a pond deep enough, then the ducks and the fish
            EcoDebug.Table(0.3f);
            Assert.GreaterOrEqual(Sim.DebugPlaceNearCamp(AquariumCiv.Sim.Eco.LifeSpecies.Ducks), 0, "the ducks placed");
            Assert.GreaterOrEqual(Sim.DebugPlaceNearCamp(AquariumCiv.Sim.Eco.LifeSpecies.Fish), 0, "the fish placed");
            yield return Until(() => LifeArrivalView.Instance == null || !LifeArrivalView.Instance.AnyPlaying, 20f); // the arrivals and the people's reaction pass
            PeopleView.DebugSetHour(10f);
            yield return Frames(20);
            var fv = FishingView.Ensure();
            Assert.IsNotNull(fv, "the fishing view is on the box");
            PeopleView.Find().Planner.ClearLive(); // the arrivals' reactions and any hint scene end: the cheat's fishers are free
            string said = FishingView.DebugBiteNow();
            StringAssert.Contains("sent to fish", said);
            yield return Until(() => fv.FishersShown > 0, 30f);
            { var pv0 = PeopleView.Find(); int fishing = 0, walking = 0; foreach (var a in pv0.Activities) if (a.kind == AquariumCiv.Life.ActivityKind.Fish) { fishing++; if (a.moving || a.enRoute) walking++; } string plans = string.Join(", ", System.Linq.Enumerable.Select(pv0.Planner.DebugFisherKeys, k => pv0.Planner.Plan(k > 0 ? PersonRef.Notable(k) : PersonRef.Crowd(-k - 1), pv0.Now).kind.ToString())); Debug.Log($"[fishing] plans {plans}; said {said}; Fish activities {fishing} (walking {walking}), pond cells {Sim.EcoPondCells}, fish {Sim.EcoFish}, open {Sim.FishingOpen}, arrivals {LifeArrivalView.Instance?.Status()}"); }
            Assert.Greater(fv.FishersShown, 0, "somebody stands fishing");
            yield return Until(() => fv.NibblesSeen > 0, 20f);
            yield return Shot("fish_nibble");
            yield return Until(() => fv.CatchesLaunched > 0, 20f);
            Assert.Greater(fv.CatchesLaunched, 0, "the yank sent a fish up");
            yield return Frames(8);
            yield return Shot("fish_flip");
            yield return Until(() => fv.Landed > 0, 5f);
            Assert.Greater(fv.Landed, 0, "the fish came down");
            var pv = PeopleView.Find();
            var who = new List<PersonRef>(); var xs = new List<float>(); var zs = new List<float>();
            pv.StandingNow(who, xs, zs);
            int shown = 0;
            foreach (var w in who) shown = Mathf.Max(shown, fv.BasketCount(w));
            Assert.GreaterOrEqual(shown, 1, "the basket beside the fisher shows the fish");
            yield return Frames(20);
            yield return Shot("fish_basket");
            Assert.IsEmpty(errors, string.Join("\n", errors));
        }

        [UnityTest, Timeout(900000)]
        public IEnumerator TheSplash_SendsAFrogHopping()
        {
            UseLivingLand();
            yield return OpenSlice(seed: 4242);
            StringAssert.Contains("broke the surface", EcoDebug.WaterBreaksNow());
            PeopleView.DebugSetHour(21f); // a night: frogs sit out
            yield return Frames(20);
            EcoDebug.FrogsNow();
            var pl = PondLife.Instance;
            Assert.IsNotNull(pl);
            yield return Until(() => pl.FrogsSitting > 0, 20f);
            var spots = new List<Vector3>();
            pl.FrogSpots(spots);
            Assert.IsNotEmpty(spots, "a frog sits on the bank");
            var box = Object.FindAnyObjectByType<BoxView>();
            int hops = pl.FrogHops;
            int moved = pl.Startle(box.Root.TransformPoint(spots[0]));
            Assert.Greater(moved, 0, "the splash startled the frog");
            Assert.Greater(pl.FrogHops, hops, "it hopped into the water");
            Assert.IsEmpty(errors, string.Join("\n", errors));
        }
    }
}
