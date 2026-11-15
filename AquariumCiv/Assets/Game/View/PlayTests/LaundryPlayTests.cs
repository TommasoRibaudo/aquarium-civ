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
    /// #705 (B28): the washing line, through the real cheats ("Laundry now", "Take in the washing"). A home's doer hangs cloths one by one on a cord between
    /// two stakes; the line stays while the cloths dry; then the same hand takes it in and the stakes come out. Shots (1179x2556) go to Temp/agent/705/.
    /// </summary>
    [Category("Laundry")]
    public class LaundryPlayTests : PlayTestBase
    {
        static SimCore Sim => GameClock.Instance.Sim;

        /// <summary>Runs the cheat at <paramref name="hour"/> and watches the whole life of the line: the stakes, each cloth, the stay, the take-in, nothing left.</summary>
        IEnumerator Watch(float hour, string tag)
        {
            PeopleView.DebugSetHour(hour);
            yield return Frames(20);
            var pv = PeopleView.Find();
            pv.Planner.ClearLive();
            string said = LaundryCheats.LaundryNow();
            StringAssert.Contains("laundry:", said);
            int maxCloths = 0; bool stakes = false, taking = false, hanging = false;
            float t0 = Time.realtimeSinceStartup; bool shotMid = false, shotFull = false;
            while (Time.realtimeSinceStartup - t0 < 60f)
            {
                yield return null;
                foreach (var a in pv.Activities) { if (a.kind == ActivityKind.HangWashing) hanging = true; if (a.kind == ActivityKind.TakeInWashing) taking = true; }
                if (pv.Washing.StakesUp) stakes = true;
                maxCloths = Mathf.Max(maxCloths, pv.Washing.ClothsShown);
                if (!shotMid && pv.Washing.ClothsShown == 2) { shotMid = true; yield return Shot($"705/{tag}_hanging"); }
                if (!shotFull && maxCloths >= 3 && !HangingNow(pv) && pv.Washing.ClothsShown >= 3) { shotFull = true; yield return Shot($"705/{tag}_hung"); }
                if (taking && pv.Washing.Visible == 0) break;
                if (taking && !shotFull) { shotFull = true; }
            }
            Assert.IsTrue(hanging, "the doer hangs the washing (HangWashing)");
            Assert.IsTrue(stakes, "the stakes and the cord stand");
            Assert.GreaterOrEqual(maxCloths, 3, "the cloths appear one by one up to three or more");
            Assert.IsTrue(taking, "the doer takes the washing in (TakeInWashing)");
            Assert.AreEqual(0, pv.Washing.Visible, "the line is gone after the take-in");
        }

        static bool HangingNow(PeopleView pv) { foreach (var a in pv.Activities) if (a.kind == ActivityKind.HangWashing) return true; return false; }

        [UnityTest, Timeout(900000)]
        public IEnumerator TheCheatHangsALineThenTakesItIn_ByDay()
        {
            yield return OpenSlice(seed: 4242);
            Sim.ForceNextEra(); yield return Frames(3);
            Sim.ForceNextEra(); yield return Frames(3);
            yield return Until(() => MomentPlayerIdle(), 90f);
            yield return Watch(9f, "day");
            Assert.IsEmpty(errors, string.Join("\n", errors));
        }

        static bool MomentPlayerIdle() { var src = SimLayoutSource.Find(); return (MomentPlayer.Instance == null || !MomentPlayer.Instance.Playing) && (src == null || (!src.MomentLook && !src.WaveRunning)); }

        [UnityTest, Timeout(900000)]
        public IEnumerator TheCheatHangsALineThenTakesItIn_InTheLateAfternoon()
        {
            yield return OpenSlice(seed: 4242);
            Sim.ForceNextEra(); yield return Frames(3);
            Sim.ForceNextEra(); yield return Frames(3);
            yield return Until(() => MomentPlayerIdle(), 90f);
            yield return Watch(15.4f, "late");
            Assert.IsEmpty(errors, string.Join("\n", errors));
        }

        [UnityTest, Timeout(900000)]
        public IEnumerator EveryHomesLineStandsClearOfThePaths()
        {
            yield return OpenSlice(seed: 4242);
            for (int i = 0; i < 3; i++) { Sim.ForceNextEra(); yield return Frames(3); yield return Until(() => MomentPlayerIdle(), 90f); }
            for (int m = 0; m < 24; m++) Sim.Step();
            PeopleView.DebugSetHour(9f);
            yield return Frames(30);
            var pv = PeopleView.Find();
            int homes = 0;
            foreach (var l in Sim.Settlement.Lots)
            {
                if (l.retired || l.open || l.buildingId <= 0) continue;
                var home = new Place(PlaceKind.Home, l.buildingId);
                if (!pv.TryGetLineAnchor(home, out var an)) continue;
                homes++;
                for (int k = -2; k <= 2; k++)
                {
                    var p = an.centre + an.along * (k * LaundryLines.Pitch);
                    Assert.IsFalse(RackSites.PathAt(Sim, p.x, p.y), $"home {l.buildingId}: the cord crosses a path at {p}");
                }
            }
            Assert.Greater(homes, 0, "some home has a line spot");
            Assert.IsEmpty(errors, string.Join("\n", errors));
        }
    }
}
