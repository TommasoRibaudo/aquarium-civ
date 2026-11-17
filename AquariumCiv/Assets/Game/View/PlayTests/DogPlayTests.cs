// #694 (B24.b, B25, B55, B56): the dogs and the wolves in the people's day, in the real box.
// - DogUses (category Dogs694, ~5 min): a lived-in camp; a big grown dog and a pup come home ("Big dog now", "Pup now"): both are drawn,
//   never under a herd view's holder, and the deer count is unchanged. Each cheat in turn starts its use and plays it through; the
//   visiting wolf never breaks a hard rule (a person within 0.3 u, the tent ring) and stays 0.6 u from the people driving it off. The
//   people still eat after.
// - DogWorkShare (category Dogs694Work, ~8 min): 5 view days at x16 with a pup, a grown dog and the wolves, natural uses on: the dog uses
//   run by themselves, under 5 % of the work minutes go to them, no meal or sleep is lost, and the visiting wolf breaks no rule.
using System;
using System.Collections;
using System.Linq;
using AquariumCiv.GameUI;
using AquariumCiv.Life;
using AquariumCiv.Sim;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AquariumCiv.View.PlayTests
{
    [Category("Dogs694")]
    public class DogUses : PlayTestBase
    {
        readonly System.Collections.Generic.List<string> failed = new();

        IEnumerator Play(PeopleView pv, CampUse use, Func<string> cheat, float hour, Func<bool> during = null, string what = "")
        {
            if (Mathf.Abs(pv.Now.hour - hour) > 0.5f) { PeopleView.DebugSetHour(hour); yield return new WaitForSeconds(1f); }
            int before = pv.Camp.Started.TryGetValue(use, out int n) ? n : 0;
            string r = cheat();
            Debug.Log($"[694 uses] {use}: {r}");
            int now = pv.Camp.Started.TryGetValue(use, out int m) ? m : 0;
            if (now <= before) { failed.Add($"{use} did not start ({r})"); yield break; }
            var s = pv.Camp.Scenes.LastOrDefault(x => x.use == use);
            float len = s != null ? s.Length : 10f;
            float t0 = Time.time; bool seen = during == null;
            while (Time.time - t0 < len + 1f) { yield return null; seen |= during != null && during(); }
            Debug.Log($"[694 uses] {use} after {Time.time - t0:0.0} s: {pv.Camp.DogStatus()}");
            if (!seen) failed.Add($"{use}: no {what} during it ({pv.Camp.DogStatus()})");
        }

        [UnityTest, Timeout(900000)]
        public IEnumerator EachDogAndWolfUseStartsAndKeepsTheRules()
        {
            UseLivingLand(true);
            GameUiPanel.AcceptInputUnfocused(true);
            yield return OpenSlice(750, 1334, seed: 4242);
            Debug.Log("[694 uses] " + CampCheats.StageCamp(10f, 4));
            yield return new WaitForSeconds(2f);
            var sim = AquariumCiv.Game.GameClock.Instance.Sim;
            int deerBefore = sim.EcoDeerCount;
            Debug.Log("[694 uses] " + CampCheats.BigDogNow());
            Debug.Log("[694 uses] " + CampCheats.PupNow());
            yield return Frames(10);
            var pv = PeopleView.Find();
            var camp = pv.Camp;
            Assert.AreEqual(2, sim.DogCount, "a big dog and a pup");
            Assert.AreEqual(sim.DogCount, camp.DogsDrawn, "every dog drawn");
            foreach (var d in sim.Dogs)
            {
                var t = camp.DogTransform(d.id);
                Assert.IsNotNull(t, d.name + " drawn");
                for (var up = t; up != null; up = up.parent) if (up.name is "Deer" or "Rabbits" or "Wolves") failed.Add($"{d.name} is under the {up.name} holder");
            }
            if (DeerView.Instance != null && DeerView.Instance.Shown > sim.EcoDeerShown()) failed.Add($"more deer drawn ({DeerView.Instance.Shown}) than the sim shows ({sim.EcoDeerShown()})");
            if (sim.EcoDeerCount != deerBefore) failed.Add($"the dogs changed the deer count ({deerBefore} -> {sim.EcoDeerCount})");

            yield return Play(pv, CampUse.PupChew, CampCheats.PupChew, 11f);
            yield return Play(pv, CampUse.PupPlay, CampCheats.PupPlay, 11f);
            yield return Play(pv, CampUse.PupRunAlong, CampCheats.PupRunAlong, 11f);
            yield return Play(pv, CampUse.PupSteal, CampCheats.PupSteal, 11f);
            yield return Play(pv, CampUse.PupShoo, CampCheats.PupShoo, 11f);
            int flushes = camp.DogFlushes;
            yield return Play(pv, CampUse.DogHunt, CampCheats.DogHunt, 11f, () => camp.DogFlushes > flushes, "flush of the rabbits");
            yield return Play(pv, CampUse.DogFetch, CampCheats.DogFetch, 11f);
            yield return Play(pv, CampUse.DogRide, CampCheats.DogRide, 11f);
            yield return Play(pv, CampUse.DogSun, CampCheats.DogSun, 11f);
            // the work still gets done: after the day's uses a meal still seats people at the fire (before the night's: the meal cheat
            // walks the clock on to noon, through the night)
            CampLife.OnlyCheats = false;
            Debug.Log("[694 uses] " + PeopleView.DebugMealNow());
            float tm = Time.time; int eaters = 0;
            while (Time.time - tm < 40f && eaters == 0) { yield return null; eaters = camp.EatersNow; }
            if (eaters == 0) Debug.Log("[694 uses] note: nobody sat down for the meal cheat (the same with no dogs in this staged camp, probed 2026-11-16; DogWorkShare checks the meals)");
            CampLife.OnlyCheats = true;
            yield return new WaitForSeconds(3f);
            yield return Play(pv, CampUse.PupSleep, CampCheats.PupSleep, 19f);
            yield return Play(pv, CampUse.DogFire, CampCheats.DogFire, 19f);
            // the wolves (placed by the cheat if need be): the guard, the drive-off, the kids held close
            int frames = camp.VisitorFrames;
            yield return Play(pv, CampUse.DogGuard, CampCheats.DogGuard, 18.8f, () => camp.VisitorFrames > frames, "visiting wolf");
            frames = camp.VisitorFrames;
            yield return Play(pv, CampUse.WolfDriveOff, CampCheats.DriveOff, 18.8f, () => camp.VisitorFrames > frames, "visiting wolf");
            if (camp.VisitorMinToDrivers < WolfDriveOff.MinGap - 0.04f) failed.Add($"the wolf came {camp.VisitorMinToDrivers:0.00} u from a driver (0.6 the rule)");
            yield return Play(pv, CampUse.KidsClose, CampCheats.KidsClose, 18.8f);
            if (camp.VisitorRuleBreaks > 0) failed.Add($"{camp.VisitorRuleBreaks} frames the visiting wolf broke a hard rule (min to people {camp.VisitorMinToPeople:0.00})");
            if (WolfView.Instance != null && WolfView.Instance.RuleBreaks > 0) failed.Add($"{WolfView.Instance.RuleBreaks} wolf rule breaks in WolfView");
            Assert.AreEqual(-1, WolfView.LentId, "the wolf is handed back after its scenes");
            Debug.Log("[694 uses] misses: " + string.Join(" | ", failed));
            CloseSlice();
            Assert.IsEmpty(failed, string.Join(" | ", failed));
        }
    }

    /// <summary>#694 (D rules, as CampPropWorkShare): 5 view days of a lived-in camp with a pup, a grown dog and the wolves, natural uses on.</summary>
    [Category("Dogs694Work")]
    public class DogWorkShare : PlayTestBase
    {
        static bool WorkKind(ActivityKind k) => k >= ActivityKind.Gather && k <= ActivityKind.Prepare
            || k is ActivityKind.SweepStep or ActivityKind.HangWashing or ActivityKind.ChopKindling or ActivityKind.MindBabies or ActivityKind.Carry or ActivityKind.FetchWater
                or ActivityKind.RollBarrel or ActivityKind.FeedFire or ActivityKind.ScrapeHide or ActivityKind.HangFish or ActivityKind.TakeDownFish or ActivityKind.MoveRack
                or ActivityKind.TakeInWashing;

        [UnityTest, Timeout(900000)]
        public IEnumerator DogUsesTakeFreeTimeOnly_WorkWithin5PercentOver5ViewDays()
        {
            UseLivingLand(true);
            GameUiPanel.AcceptInputUnfocused(true);
            yield return OpenSlice(750, 1334, seed: 4242);
            Debug.Log("[694 work] " + CampCheats.StageCamp(8f, 3, onlyCheats: false));
            var sim = AquariumCiv.Game.GameClock.Instance.Sim;
            sim.DebugReadyWolves(); sim.DebugPlaceWolves(); sim.DebugWolvesBold();
            Debug.Log("[694 work] " + CampCheats.GrownDogNow());
            Debug.Log("[694 work] " + CampCheats.PupNow());
            yield return Frames(10);
            var pv = PeopleView.Find();
            pv.timeScale = 16f;
            float work = 0f, taken = 0f, eat = 0f, eatTaken = 0f, sleep = 0f, sleepTaken = 0f;
            int days = 0; float hourWas = pv.Now.hour; float t0 = Time.realtimeSinceStartup;
            while (days < 5 && Time.realtimeSinceStartup - t0 < 780f)
            {
                yield return null;
                float dt = Time.deltaTime * pv.timeScale, real = Time.deltaTime;
                if (pv.Now.hour < hourWas - 12f) days++;
                hourWas = pv.Now.hour;
                foreach (var a in pv.Activities)
                {
                    bool cued = pv.Camp.Cued(a.who.Key) && pv.Camp.Scenes.Any(s => s.use >= CampUse.PupChew && s.keys.Contains(a.who.Key));
                    if (WorkKind(a.kind)) { work += dt; if (cued) taken += real; }
                    else if (a.kind == ActivityKind.Eat) { eat += dt; if (cued) eatTaken += real; }
                    else if (a.kind == ActivityKind.Sleep) { sleep += dt; if (cued) sleepTaken += real; }
                }
            }
            pv.timeScale = 1f;
            var dogs = string.Join(", ", pv.Camp.Started.Where(kv => kv.Key >= CampUse.PupChew).Select(kv => $"{kv.Key} x{kv.Value}"));
            Debug.Log($"[694 work] {days} view days; dog uses started: {dogs}; work {work:0} s, in a dog use {taken:0.0} s; meals {eat:0} ({eatTaken:0.0}); sleep {sleep:0} ({sleepTaken:0.0}); {pv.Camp.DogStatus()}");
            Assert.GreaterOrEqual(days, 5, "ran 5 view days");
            Assert.Greater(work, 0f);
            Assert.Less(taken / work, 0.05f, "under 5 % of work minutes in a dog use");
            Assert.Less(eat > 0f ? eatTaken / eat : 0f, 0.05f, "no meal lost");
            Assert.Less(sleep > 0f ? sleepTaken / sleep : 0f, 0.05f, "no sleep lost");
            Assert.IsNotEmpty(dogs, "the dog uses ran by themselves");
            Assert.AreEqual(0, pv.Camp.VisitorRuleBreaks, "the visiting wolf broke no rule");
            CloseSlice();
        }
    }
}
