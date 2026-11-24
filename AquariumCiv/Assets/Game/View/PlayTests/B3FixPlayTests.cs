// B3 tester fixes (the batch 3 tester's [real] findings), each with the test that would have caught it:
// - B3FireWatch (category B3Fixes): a lived-in camp with every choice's thing, three dogs, natural uses and the kids' games on, watched over
//   view hours at x6 with the reported uses forced in between: nobody's feet within 0.25 u of a fire (the feeder's signed-off kneel apart),
//   nobody's middle on a choice thing's footprint but a use's cast at its own seat on it, and no console error. Then the same box grown
//   into a village (real sim months), watched again: the kids' hop no longer circles into the village fire.
// - B3StumpAndDogs (category B3Fixes): a kid on the stump sits on its top (the seat's lift is not scaled by the kid's size); three dogs
//   lying by day and asleep by night each keep their own spot, 0.15 u apart.
// - B3VillageThings (category B3Fixes): a camp with every thing grown into a village and a town by the sim's own months: no two drawn
//   things overlap and no drawn thing stands on a lot (a house, a field, a barn); the chop logs' pool survives the box's rebuild.
// - B3RitualReturn (category B3Fixes): a kept custom's ritual plays when the Keeper is there: crossing into its month live, and returning
//   from away into its month.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using AquariumCiv.GameUI;
using AquariumCiv.Life;
using AquariumCiv.Sim;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AquariumCiv.View.PlayTests
{
    /// <summary>The watch every B3 fix test shares: per frame, each person's feet (box-local) against the fires and the things.</summary>
    public sealed class B3Watch
    {
        public float minFire = 99f; public string minFireWho = "";
        public int frames, fireHits, propHits;
        public readonly Dictionary<string, int> fireWhy = new(), propWhy = new();
        readonly List<Vector2> fires = new();
        readonly List<(int id, string kind, Vector2 at, float yaw)> feet = new();
        public const float FireLimit = 0.25f;

        public void Step(PeopleView pv, BoxView box)
        {
            frames++;
            pv.FireMiddles(fires);
            ConsequencePropsView.Instance?.Footprints(feet);
            foreach (var a in pv.Activities)
            {
                if (!pv.TryGetDollFeet(a.who, out var w)) continue;
                var l = box.Root.InverseTransformPoint(w);
                var p = new Vector2(l.x, l.z);
                string what = $"{a.kind}/{pv.DrawnOf(a.who)}/{pv.Camp.UseOf(a.who.Key)}";
                if (!pv.AtFlames(a.who.Key))
                    foreach (var f in fires)
                    {
                        float d = (p - f).magnitude;
                        if (d < minFire) { minFire = d; minFireWho = $"{a.who} {what} at {pv.Now.hour:0.0} h"; }
                        if (d < FireLimit) { fireHits++; fireWhy[what] = fireWhy.TryGetValue(what, out int n) ? n + 1 : 1; }
                    }
                int enter = pv.EnterPropOf(a.who.Key);
                if (enter == -2) continue; // a staged moment's own spot
                foreach (var t in feet)
                {
                    if (t.id == enter || !ConsequenceFootprint.Covers(t.kind, t.at.x, t.at.y, t.yaw, p.x, p.y)) continue;
                    propHits++;
                    string k = t.kind + ": " + what;
                    propWhy[k] = propWhy.TryGetValue(k, out int n) ? n + 1 : 1;
                }
            }
        }

        public string Report() =>
            $"{frames} frames; min to a fire {minFire:0.000} ({minFireWho}); feet within {FireLimit} of a fire: {fireHits} [{Top(fireWhy)}]; middles on a thing: {propHits} [{Top(propWhy)}]";

        static string Top(Dictionary<string, int> d) => string.Join("; ", d.OrderByDescending(kv => kv.Value).Take(8).Select(kv => $"{kv.Key} x{kv.Value}"));
    }

    [Category("B3Fixes")]
    public class B3FireWatch : PlayTestBase
    {
        IEnumerator Watch(B3Watch w, PeopleView pv, BoxView box, float realSeconds, Func<float, string> during = null)
        {
            float t0 = Time.time, next = 6f;
            while (Time.time - t0 < realSeconds)
            {
                yield return null;
                w.Step(pv, box);
                if (during != null && Time.time - t0 > next) { next += 6f; string r = during(Time.time - t0); if (r != null) Debug.Log("[b3 watch] " + r); }
            }
        }

        [UnityTest, Timeout(1200000)]
        public IEnumerator NobodyInTheFireOrInAThing_CampThenVillage()
        {
            UseLivingLand(true);
            GameUiPanel.AcceptInputUnfocused(true);
            yield return OpenSlice(750, 1334, seed: 4242);
            Debug.Log("[b3 watch] " + CampCheats.StageCamp(9f, 4, onlyCheats: false));
            yield return new WaitForSeconds(1f);
            Debug.Log("[b3 watch] " + CampCheats.PondReady());
            Debug.Log("[b3 watch] " + CampCheats.EveryThing());
            Debug.Log("[b3 watch] " + CampCheats.PupNow() + " | " + CampCheats.BigDogNow() + " | " + CampCheats.OldDogNow());
            yield return new WaitForSeconds(2f);
            var pv = PeopleView.Find();
            var box = UnityEngine.Object.FindAnyObjectByType<BoxView>();
            CampLife.OnlyCheats = false;
            var camp = new B3Watch();
            // the reported cases forced in between (the flour carried to the fire, the stump, the woodpile, the pot, the charm pole), natural uses
            // and the kids' games the rest of the time
            var forced = new Func<string>[] { CampCheats.FlourCarry, CampCheats.StumpSit, CampCheats.StumpSplit, CampCheats.WoodShelter, CampCheats.BigPot, CampCheats.CharmFlick, CampCheats.PotDrum, CampCheats.TableUnder, CampCheats.KidPoke };
            int fi = 0;
            pv.timeScale = 6f;
            yield return Watch(camp, pv, box, 150f, t => fi < forced.Length && pv.Now.hour < 16f ? forced[fi++]() : null);
            pv.timeScale = 1f;
            Debug.Log("[b3 watch] camp: " + camp.Report());

            // the same box grown into a village (the sim's own months), watched with its natural uses and games
            Debug.Log("[b3 watch] " + CampCheats.StageVillage(9.5f, false, 4, onlyCheats: false));
            yield return new WaitForSeconds(3f);
            pv = PeopleView.Find();
            box = UnityEngine.Object.FindAnyObjectByType<BoxView>();
            var village = new B3Watch();
            pv.timeScale = 6f;
            yield return Watch(village, pv, box, 90f);
            pv.timeScale = 1f;
            Debug.Log("[b3 watch] village: " + village.Report());
            CloseSlice();
            Assert.Greater(camp.frames, 500, "watched the camp");
            Assert.AreEqual(0, camp.fireHits, "camp: " + camp.Report());
            Assert.AreEqual(0, camp.propHits, "camp: " + camp.Report());
            Assert.AreEqual(0, village.fireHits, "village: " + village.Report());
            Assert.AreEqual(0, village.propHits, "village: " + village.Report());
            AssertNoErrors("the watch");
        }
    }

    [Category("B3Fixes")]
    public class B3StumpAndDogs : PlayTestBase
    {
        [UnityTest, Timeout(600000)]
        public IEnumerator AKidSitsOnTheStumpsTop()
        {
            UseLivingLand(true);
            GameUiPanel.AcceptInputUnfocused(true);
            yield return OpenSlice(750, 1334, seed: 4242);
            Debug.Log("[b3 stump] " + CampCheats.StageCamp(11f, 4));
            yield return new WaitForSeconds(2f);
            Debug.Log("[b3 stump] " + CampCheats.OnlyThing(ConsequenceKind.AxeStump));
            yield return new WaitForSeconds(2f);
            var pv = PeopleView.Find();
            var box = UnityEngine.Object.FindAnyObjectByType<BoxView>();
            string r = CampCheats.StumpSit();
            Debug.Log("[b3 stump] " + r);
            var s = pv.Camp.Scenes.LastOrDefault(x => x.use == CampUse.StumpSit);
            Assert.IsNotNull(s, "the stump sit started: " + r);
            float best = -1f; int sat = 0;
            float t0 = Time.time;
            while (Time.time - t0 < s.Length)
            {
                yield return null;
                foreach (int k in s.keys)
                {
                    var who = pv.Activities.FirstOrDefault(a => a.who.Key == k).who;
                    if (!pv.DrawnOf(who).StartsWith("Sit") || !pv.TryGetDollFeet(who, out var w)) continue;
                    var l = box.Root.InverseTransformPoint(w);
                    float above = l.y - box.SurfaceHeight(l.x, l.z);
                    best = Mathf.Max(best, above); sat++;
                    if (above < PropGeo.StumpTop - 0.03f) Assert.Fail($"a kid sits {above:0.000} u up, the stump's top is {PropGeo.StumpTop:0.000}: sunk into it");
                }
            }
            Debug.Log($"[b3 stump] {sat} seated frames, the seat {best:0.000} u up (top {PropGeo.StumpTop:0.000})");
            Assert.Greater(sat, 10, "kids sat on the stump");
            CloseSlice();
        }

        [UnityTest, Timeout(600000)]
        public IEnumerator EachDogLiesInItsOwnSpot_DayAndNight()
        {
            UseLivingLand(true);
            GameUiPanel.AcceptInputUnfocused(true);
            yield return OpenSlice(750, 1334, seed: 4242);
            Debug.Log("[b3 dogs] " + CampCheats.StageCamp(11f, 2));
            yield return new WaitForSeconds(1f);
            Debug.Log("[b3 dogs] " + CampCheats.PupNow() + " | " + CampCheats.BigDogNow() + " | " + CampCheats.OldDogNow());
            var pv = PeopleView.Find();
            var rest = new List<(int id, Vector2 at)>();
            float worst = 99f; string worstWho = ""; int pairs = 0;
            foreach (float hour in new[] { 11f, 15.5f, 21.5f })
            {
                PeopleView.DebugSetHour(hour);
                float t0 = Time.time;
                while (Time.time - t0 < 25f)
                {
                    yield return null;
                    if (Time.time - t0 < 12f) continue; // they walk to their spots
                    pv.Camp.DogsResting(rest);
                    for (int i = 0; i < rest.Count; i++)
                        for (int j = i + 1; j < rest.Count; j++)
                        {
                            float d = (rest[i].at - rest[j].at).magnitude; pairs++;
                            if (d < worst) { worst = d; worstWho = $"dogs {rest[i].id} and {rest[j].id} at {hour:0.0} h"; }
                        }
                }
                Debug.Log($"[b3 dogs] {hour:0.0} h: {pv.Camp.DogStatus()}");
            }
            Debug.Log($"[b3 dogs] {pairs} resting pairs seen; the closest {worst:0.000} u ({worstWho})");
            CloseSlice();
            Assert.Greater(pairs, 20, "two dogs lay down at once");
            Assert.GreaterOrEqual(worst, 0.15f, "two dogs lie inside each other: " + worstWho);
        }
    }

    [Category("B3Fixes")]
    public class B3VillageThings : PlayTestBase
    {
        static List<string> Clashes(SimCore sim)
        {
            var r = new List<string>();
            var feet = new List<(int id, string kind, Vector2 at, float yaw)>();
            ConsequencePropsView.Instance.Footprints(feet);
            for (int i = 0; i < feet.Count; i++)
                for (int j = i + 1; j < feet.Count; j++)
                {
                    float need = ConsequenceFootprint.Radius(feet[i].kind) + ConsequenceFootprint.Radius(feet[j].kind);
                    float d = (feet[i].at - feet[j].at).magnitude;
                    if (d < need - 0.01f) r.Add($"{feet[i].kind} and {feet[j].kind} {d:0.00} u apart (need {need:0.00})");
                }
            if (r.Count > 0)
                foreach (var f in feet)
                {
                    var p = sim.FindConsequenceProp(f.id);
                    Debug.Log($"[b3 village] thing {f.kind}#{f.id} {p?.anchor} sim ({p?.x:0.00}, {p?.z:0.00}) drawn ({f.at.x:0.00}, {f.at.y:0.00}) yaw {f.yaw:0}");
                }
            var st = sim.Settlement; var cells = new List<int>();
            foreach (var f in feet)
            {
                ConsequenceFootprint.Cells(f.kind, f.at.x, f.at.y, 0f, cells);
                foreach (int c in cells)
                {
                    int lot = st.Map.lot[c];
                    var l = lot == 0 ? null : st.Lots.FirstOrDefault(x => x.id == lot);
                    if (l == null || l.retired) continue;
                    r.Add($"{f.kind} at ({f.at.x:0.00}, {f.at.y:0.00}) on lot {l.kind}#{l.id}"); break;
                }
            }
            return r;
        }

        [UnityTest, Timeout(900000)]
        public IEnumerator ACampWithItsThingsGrowsIntoAVillageAndATown_NoThingOnALotOrOnAnother()
        {
            UseLivingLand(true);
            GameUiPanel.AcceptInputUnfocused(true);
            yield return OpenSlice(750, 1334, seed: 4242);
            Debug.Log("[b3 village] " + CampCheats.StageCamp(10f, 4));
            yield return new WaitForSeconds(1f);
            Debug.Log("[b3 village] " + CampCheats.PondReady());
            Debug.Log("[b3 village] " + CampCheats.EveryThing());
            yield return new WaitForSeconds(2f);
            var sim = AquariumCiv.Game.GameClock.Instance.Sim;
            var camp = Clashes(sim);
            Debug.Log("[b3 village] camp: " + string.Join(" | ", camp));
            // the chop logs lie under the box's root: lay some, then rebuild the box (the town jump did) and lay more
            var chop = UnityEngine.Object.FindAnyObjectByType<ChopLogsView>();
            var lay = typeof(ChopLogsView).GetMethod("Lay", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.IsNotNull(chop, "the chop logs' view"); Assert.IsNotNull(lay);
            var box = UnityEngine.Object.FindAnyObjectByType<BoxView>();
            for (int i = 0; i < 5; i++) lay.Invoke(chop, new object[] { 7, box.Root.TransformPoint(new Vector3(0.5f, 0f, 1f)) });
            Debug.Log("[b3 village] " + CampCheats.StageVillage(10f, false, 4)); // real sim months (ForceNextEra, then years of growth), a box rebuild
            yield return new WaitForSeconds(3f);
            box = UnityEngine.Object.FindAnyObjectByType<BoxView>();
            for (int i = 0; i < 5; i++) lay.Invoke(chop, new object[] { 7, box.Root.TransformPoint(new Vector3(0.5f, 0f, 1f)) });
            yield return Frames(30);
            var village = Clashes(sim);
            Debug.Log($"[b3 village] {sim.State.era}: {sim.Settlement.Lots.Count(l => !l.retired)} lots ({sim.Settlement.Lots.Count(l => !l.retired && l.kind == AquariumCiv.Sim.Settle.LotKind.Farm)} fields); " + string.Join(" | ", village));
            yield return Shot("b3fix/village_things");
            Debug.Log("[b3 village] " + CampCheats.StageVillage(10f, true, 0)); // and on to a town
            yield return new WaitForSeconds(3f);
            box = UnityEngine.Object.FindAnyObjectByType<BoxView>();
            for (int i = 0; i < 5; i++) lay.Invoke(chop, new object[] { 7, box.Root.TransformPoint(new Vector3(0.5f, 0f, 1f)) });
            yield return Frames(30);
            var town = Clashes(sim);
            Debug.Log($"[b3 village] {sim.State.era}: " + string.Join(" | ", town));
            CloseSlice();
            Assert.IsEmpty(camp, "camp: " + string.Join(" | ", camp));
            Assert.IsEmpty(village, "village: " + string.Join(" | ", village));
            Assert.IsEmpty(town, "town: " + string.Join(" | ", town));
            AssertNoErrors("growing the box with its things (the chop logs' pool over a rebuild)");
        }
    }

    [Category("B3Fixes")]
    public class B3RitualReturn : PlayTestBase
    {
        static bool RitualPlaying(SimCore sim) => (sim.State.moments ?? new List<HeldMoment>()).Any(m => m.kind == MomentKind.Ritual && m.state == HeldMomentState.Playing)
            || StagedMoments.LastScene != null && StagedMoments.LastScene.kind == MomentKind.Ritual && (sim.State.moments ?? new List<HeldMoment>()).Any(m => m.id == StagedMoments.LastScene.momentId && m.state != HeldMomentState.Waiting && m.state != HeldMomentState.Carded);

        /// <summary>Moves the box's month on to the last month before <paramref name="ritual"/>'s (by whole sim months, live).</summary>
        static void ToMonthBefore(SimCore sim, string ritual)
        {
            int want = (RitualKind.MonthOf(ritual) + 11) % 12;
            for (int i = 0; i < 13 && sim.CalendarMonth != want; i++) sim.Step();
        }

        [UnityTest, Timeout(900000)]
        public IEnumerator AKeptRitualPlaysWhenTheKeeperIsThere_LiveAndOnReturn()
        {
            UseLivingLand(true);
            GameUiPanel.AcceptInputUnfocused(true);
            yield return OpenSlice(750, 1334, seed: 4242);
            Debug.Log("[b3 ritual] " + CampCheats.StageCamp(9f, 3, onlyCheats: false));
            yield return new WaitForSeconds(2f);
            var sim = AquariumCiv.Game.GameClock.Instance.Sim;
            if (!sim.MomentsOn) AquariumCiv.Game.GameClock.Instance.config.moments = true;
            sim.State.customs ??= new List<string>();
            if (!sim.State.customs.Contains(RitualKind.CustomOf(RitualKind.Berrying))) sim.State.customs.Add(RitualKind.CustomOf(RitualKind.Berrying));

            // 1. away into its month: the Keeper comes back in the morning of its month and sees it
            ToMonthBefore(sim, RitualKind.Berrying);
            int monthWas = sim.CalendarMonth;
            for (int i = 0; i < 60 && sim.CalendarMonth == monthWas; i++) Debug.Log($"[b3 ritual] month {monthWas}: " + AquariumCiv.Game.GameClock.DebugAway(0.05)); // away until its month comes
            Debug.Log($"[b3 ritual] back in month {sim.CalendarMonth}; held: {string.Join(", ", (sim.State.moments ?? new List<HeldMoment>()).Where(m => m.kind == MomentKind.Ritual).Select(m => m.state.ToString()))}");
            PeopleView.DebugSetHour(9.2f);
            float t0 = Time.time; bool onReturn = false;
            while (Time.time - t0 < 120f && !onReturn) { yield return null; onReturn = RitualPlaying(sim); }
            Debug.Log($"[b3 ritual] on return: played {onReturn} after {Time.time - t0:0} s; {StagedMoments.LastWhy}");
            float t1 = Time.time;
            while (Time.time - t1 < 150f && sim.PlayingMoments().Count > 0) yield return null; // a ritual is 66 s of view time, plus the walk in
            yield return new WaitForSeconds(2f); // the stage's next check finishes it (and starts the rest)
            var pc0 = sim.MomentPacing;
            Debug.Log($"[b3 ritual] phase 1 over after {Time.time - t1:0} s: playing {sim.PlayingMoments().Count}, watched {pc0.watchedMinutes:0.00} rest until {pc0.restUntil:0.00}");

            // 2. live: a year on, the month turns while the Keeper watches
            sim.State.moments?.RemoveAll(m => m.kind == MomentKind.Ritual);
            ToMonthBefore(sim, RitualKind.Berrying);
            // Only the ritual waits: a wedding queued in the months stepped through played first and the ritual sat out the rest behind it
            // (pacing, fine: a month is ~5 watched minutes, the ritual is held 2, a camp rests 6). This part checks the live path alone.
            sim.State.moments?.RemoveAll(m => m.kind != MomentKind.Ritual && m.state == HeldMomentState.Waiting);
            // A year of watching lies between the two: the rest after the return's ritual (a camp: 6 watched minutes) and the kind rule are long
            // over by then. Without this the live ritual sat "resting" behind phase 1's, which is the pacing working, not the ritual failing.
            sim.DebugMomentDueNow();
            PeopleView.DebugSetHour(9f);
            sim.Step(); // the month turns live
            sim.State.moments?.RemoveAll(m => m.kind != MomentKind.Ritual && m.state == HeldMomentState.Waiting); // (and one the month's step queued)
            sim.DebugMomentDueNow(); // (again: the month's step may end phase 1's batch)
            var pc = sim.MomentPacing;
            Debug.Log($"[b3 ritual] pacing: watched {pc.watchedMinutes:0.00} rest until {pc.restUntil:0.00} credit {pc.credit:0.0} last kind {pc.lastKind}; why {sim.MomentWaitReason()}");
            Debug.Log($"[b3 ritual] live in month {sim.CalendarMonth}; held:{string.Join(", ", (sim.State.moments ?? new List<HeldMoment>()).Where(m => m.kind == MomentKind.Ritual).Select(m => m.state.ToString()))}");
            t0 = Time.time; bool live = false;
            while (Time.time - t0 < 120f && !live) { yield return null; live = RitualPlaying(sim); }
            Debug.Log($"[b3 ritual] moments now: {string.Join(", ", (sim.State.moments ?? new List<HeldMoment>()).Select(m => $"{m.kind}#{m.id} {m.state}"))}; started {StagedMoments.Started} finished {StagedMoments.Finished} last scene {StagedMoments.LastScene?.kind}#{StagedMoments.LastScene?.momentId}");
            Debug.Log($"[b3 ritual] live: played {live} after {Time.time - t0:0} s; staged: {StagedMoments.LastWhy}; stage: {HeldMomentStage.Instance?.LastWhy}; sim: {sim.MomentWaitReason()}");
            CloseSlice();
            Assert.IsTrue(onReturn, "the berrying walk played on the Keeper's return in its month");
            Assert.IsTrue(live, "the berrying walk played when its month came while watched");
        }
    }
}
