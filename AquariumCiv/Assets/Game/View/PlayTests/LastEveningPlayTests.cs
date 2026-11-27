// #789 (animation-specs B82, approved 2026-11-26): the last evening in the real box (PlayMode; run them from the Editor:
// PROJ=... bash Tools/run-tests.sh "play|AquariumCiv.View.PlayTests.LastEveningPlayTests||" ; each is one box, a few minutes).
// - PlayNow: "Last evening: play now": the scene plays at the fire at dusk with the elder and kin, the caption at the stars, the elder ends in at
//   their door, everyone else back in their evening; nothing else held starts over it; the night takes the elder.
// - NoKin: "Last evening: elder with no kin": nothing plays (the rule says why), they die in the night as today.
// - TwoElders: two in their last evening: only one scene plays, and nothing more this visit.
// - Away: the Keeper away over the dusk (a catch-up): nothing plays and nothing is said.
using System.Collections;
using System.Linq;
using AquariumCiv.Game;
using AquariumCiv.GameUI;
using AquariumCiv.Sim;
using AquariumCiv.View.ShowMe;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AquariumCiv.View.PlayTests
{
    [Category("LastEvening789Play")]
    public class LastEveningPlayTests : PlayTestBase
    {
        static SimCore Sim => GameClock.Instance != null ? GameClock.Instance.Sim : null;
        static PeopleView Pv => PeopleView.Find();

        [TearDown]
        public void Restore() { CampLife.OnlyCheats = false; }

        IEnumerator Box()
        {
            UseLivingLand(true);
            GameUiPanel.AcceptInputUnfocused(true);
            ShowMeDirector.BubblesOn = false;
            yield return OpenSlice(750, 1334, seed: 4242);
            GameUiPanel.AcceptInputUnfocused(true);
            yield return new WaitForSeconds(2f);
        }

        [UnityTest, Timeout(900000)]
        public IEnumerator PlayNow_PlaysAtTheFire_EndsWithTheElderIn_AndTheNightTakesThem()
        {
            yield return Box();
            var sim = Sim; var pv = Pv;
            string r = CampCheats.LastEveningPlayNow();
            Debug.Log("[789] " + r);
            yield return Until(() => pv.Camp.LastEveningScene != null, 20f);
            var sc = pv.Camp.LastEveningScene;
            Assert.IsNotNull(sc, $"it plays ({r}; why: {pv.Camp.LastEveningWhy})");
            int elder = sc.keys[0];
            Assert.Greater(sc.keys.Count, 1, "with kin");
            Assert.AreEqual(elder, sim.State.lastEveningFor, "stamped");
            Assert.AreEqual("the last evening", HeldMomentStage.BusyReason(), "nothing else held starts over it");
            StringAssert.Contains("looks up at the stars with", pv.Camp.LastEveningCaption);
            float t0 = Time.time;
            while (pv.Camp.Scenes.Contains(sc) && Time.time - t0 < 150f) yield return null;
            Assert.IsFalse(pv.Camp.Scenes.Contains(sc), "the scene ends");
            Assert.IsNull(pv.Camp.LastEveningScene);
            Assert.IsFalse(CampLife.LastEveningNow);
            foreach (int k in sc.keys.Skip(1)) Assert.IsFalse(pv.Camp.Busy(k), "the kin are back in their evening");
            // the night takes them (the cheat set the death for the next month)
            GameClock.DebugSkipHours(1.0 / 12.0 + 0.001);
            yield return new WaitForSeconds(1f);
            Assert.IsFalse(sim.FindNotable(elder).alive, "they died in the night");
            Assert.AreEqual(1, sim.State.chronicle.Count(e => e.kind == SimCore.LastEveningKind), "one line in History");
        }

        [UnityTest, Timeout(900000)]
        public IEnumerator NoKin_NothingPlays_TheyDieInTheNight()
        {
            yield return Box();
            var sim = Sim; var pv = Pv;
            string r = CampCheats.LastEveningNoKin();
            Debug.Log("[789] " + r);
            yield return new WaitForSeconds(8f);
            Assert.IsNull(pv.Camp.LastEveningScene, "no scene");
            Assert.AreEqual(-1, sim.State.lastEveningFor, "nothing stamped (" + pv.Camp.LastEveningWhy + ")");
            StringAssert.Contains("no kin", pv.Camp.LastEveningWhy);
        }

        [UnityTest, Timeout(900000)]
        public IEnumerator TwoElders_OnlyOneScenePlays()
        {
            yield return Box();
            var sim = Sim; var pv = Pv;
            string r = CampCheats.LastEveningTwo();
            Debug.Log("[789] " + r);
            Assert.AreEqual(2, sim.LastEveningElders().Count, r);
            yield return Until(() => pv.Camp.LastEveningScene != null, 20f);
            Assert.IsNotNull(pv.Camp.LastEveningScene, pv.Camp.LastEveningWhy);
            var sc = pv.Camp.LastEveningScene;
            float t0 = Time.time;
            while (pv.Camp.Scenes.Contains(sc) && Time.time - t0 < 150f) yield return null;
            yield return new WaitForSeconds(3f);
            Assert.AreEqual(1, pv.Camp.LastEveningsStarted, "only one scene");
            Assert.IsNull(sim.ChooseLastEvening(k => true, true, false, out string why), "the other gets nothing this visit");
            Debug.Log("[789] the other: " + why);
        }

        [UnityTest, Timeout(900000)]
        public IEnumerator Away_NothingPlays()
        {
            yield return Box();
            var sim = Sim; var pv = Pv;
            var n = sim.DebugLastEveningElder(true, out string why);
            Assert.IsNotNull(n, why);
            sim.DebugLastEveningClear();
            var res = sim.CatchUp(4.0); // away four hours: the sim lives the dusk unseen
            yield return new WaitForSeconds(2f);
            Assert.AreEqual(-1, sim.State.lastEveningFor, "nothing stamped while away");
            Assert.AreEqual(0, sim.State.chronicle.Count(e => e.kind == SimCore.LastEveningKind), "nothing said");
            Assert.AreEqual(0, pv.Camp.LastEveningsStarted);
        }
    }
}
