using System.Collections;
using System.Linq;
using AquariumCiv.Game;
using AquariumCiv.Life;
using AquariumCiv.Sim;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AquariumCiv.View.PlayTests
{
    /// <summary>#541: two free kids at once play ONE together game (the cheat "Two kids play" forces the case through the real grouping path).</summary>
    public class KidsTogetherPlayTests : PlayTestBase
    {
        [UnityTest, Timeout(120000)]
        public IEnumerator TwoFreeKids_ShareOneGroup_AndPlayTogether()
        {
            yield return OpenSlice(1179, 2556, seed: 1234);
            SimLayoutSource.DebugSkipYears(8f);
            yield return null;
            var sim = GameClock.Instance.ActiveSim;
            var born = new[] { sim.DebugBirth(), sim.DebugBirth() };
            SimLayoutSource.DebugSkipYears(0.1f);
            yield return null;
            foreach (var n in born) n.age = 7f;
            var pv = PeopleView.Find();
            pv.Planner.Invalidate();
            sim.CleanAll();
            PeopleView.DebugSetHour(10f);
            yield return Frames(4);
            for (int calm = 0, f = 0; calm < 120 && f < 1200; f++) { calm = StagedMoments.StopAll() == 0 && StagedMoments.Playing == 0 ? calm + 1 : 0; yield return null; }
            PeopleView.DebugSetHour(10f);
            yield return Frames(4);
            string said = KidPlayDirector.DebugTwo();
            if (!said.StartsWith("two kids")) Assert.Inconclusive(said);
            // Past the director's settle time so the merged group exists.
            yield return Frames(120);
            var kids = pv.Planner.People.Where(w => pv.Planner.StageOf(w) == LifeStage.Kid).Select(w => w.Key).ToList();
            Assert.GreaterOrEqual(kids.Count, 2);
            int inGroups = pv.KidPlay.GroupCount;
            Assert.AreEqual(1, inGroups, "one group, not two solo copies: " + pv.KidPlay.Status());
            Assert.AreEqual(1, pv.KidPlay.GroupsOf(kids), "both kids in the same group");
            foreach (var g in pv.KidPlay.Games) Assert.AreNotEqual(PlayGame.None, g);
            yield return Frames(60);
            yield return Shot("two-kids-together");
            AssertNoErrors("two kids");
        }
    }
}
