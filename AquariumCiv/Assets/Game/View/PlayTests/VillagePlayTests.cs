// #695 part 1 (B57.a-j, B58.c/f/g/j/k): the village's and the town's things used, in the real box.
// - VillageUses (category Village695, ~6 min): a lived-in camp grown into a village ("Jump: a village box"); each cheat in turn starts its use
//   and plays it through; what a use leaves stays (the scarecrow, a home's new thing, the hopscotch grid). Then on to a town ("Jump: a town
//   box": a bakery stands) for the crust at the bakery door.
// - VillageWorkShare (category Village695Work, ~8 min): 5 view days of the village at x16, natural uses on: the village uses run by themselves,
//   under 5 % of the work minutes go to them, no meal or sleep is lost.
// - VillageLastDays (category LastDays788Play, ~3 min): #788 tester (polish 2): "Elder: last days" on the oldest in a village (seed 4242), natural
//   uses on, x8 for 150 s real (about 4 view days): every frame a visitor sits with the frail elder, the elder is on their stool in that place
//   and no camp use has taken their doll.
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
    [Category("Village695")]
    public class VillageUses : PlayTestBase
    {
        readonly System.Collections.Generic.List<string> failed = new();

        IEnumerator Play(PeopleView pv, CampUse use, Func<string> cheat, float hour)
        {
            if (Mathf.Abs(pv.Now.hour - hour) > 0.5f) { PeopleView.DebugSetHour(hour); yield return new WaitForSeconds(1f); }
            int before = pv.Camp.Started.TryGetValue(use, out int n) ? n : 0;
            string r = cheat();
            Debug.Log($"[695 uses] {use}: {r}");
            int now = pv.Camp.Started.TryGetValue(use, out int m) ? m : 0;
            if (now <= before) { failed.Add($"{use} did not start ({r})"); yield break; }
            var s = pv.Camp.Scenes.LastOrDefault(x => x.use == use);
            float len = s != null ? Mathf.Min(s.Length, 45f) : 10f;
            float t0 = Time.time;
            while (Time.time - t0 < len + 1f) yield return null;
            Debug.Log($"[695 uses] {use} after {Time.time - t0:0.0} s: {pv.Camp.VillageStatus()}");
        }

        [UnityTest, Timeout(900000)]
        public IEnumerator EachVillageAndTownUseStartsAndLeavesItsThing()
        {
            UseLivingLand(true);
            GameUiPanel.AcceptInputUnfocused(true);
            yield return OpenSlice(750, 1334, seed: 4242);
            Debug.Log("[695 uses] " + CampCheats.StageVillage(10f, false, 6));
            yield return new WaitForSeconds(3f);
            var sim = AquariumCiv.Game.GameClock.Instance.Sim;
            Assert.AreEqual(Era.Village, sim.State.era, "a village");
            var pv = PeopleView.Find();
            var camp = pv.Camp;
            int things = camp.HomeThingsShown;
            yield return Play(pv, CampUse.FieldTag, CampCheats.FieldTag, 11f);
            yield return Play(pv, CampUse.Scarecrow, CampCheats.Scarecrow, 11f);
            if (camp.ScarecrowsStanding < 1) failed.Add("no scarecrow stands after it was made");
            yield return Play(pv, CampUse.HomeMend, CampCheats.HomeMend, 11f);
            if (camp.HomeThingsShown <= things) failed.Add($"no new thing on a home ({things} -> {camp.HomeThingsShown})");
            yield return Play(pv, CampUse.WellGossip, CampCheats.WellGossip, 11f);
            yield return Play(pv, CampUse.WellPebble, CampCheats.WellPebble, 11f);
            yield return Play(pv, CampUse.SailWatch, CampCheats.SailWatch, 11f);
            yield return Play(pv, CampUse.SailShadow, CampCheats.SailShadow, 11f);
            yield return Play(pv, CampUse.LogPileSit, CampCheats.LogPileSit, 12f);
            yield return Play(pv, CampUse.LogBalance, CampCheats.LogBalance, 12f);
            yield return Play(pv, CampUse.Hopscotch, CampCheats.Hopscotch, 12f);
            if (camp.GridsDrawn < 1) failed.Add("no hopscotch grid on the street");
            yield return Play(pv, CampUse.StreetChat, CampCheats.StreetChat, 12f);
            yield return Play(pv, CampUse.FenceFix, CampCheats.FenceFix, 12f);
            yield return Play(pv, CampUse.FenceLean, CampCheats.FenceLean, 12f);
            yield return Play(pv, CampUse.HarvestDance, CampCheats.HarvestDance, 15.5f);
            yield return Play(pv, CampUse.BarnChores, CampCheats.BarnChores, 8f); // B59.k
            yield return Play(pv, CampUse.HayJump, CampCheats.HayJump, 11f); // B59.l
            // the town: the bakery's crust
            Debug.Log("[695 uses] " + CampCheats.StageVillage(10f, true, 4));
            yield return new WaitForSeconds(3f);
            Assert.AreEqual(Era.Town, sim.State.era, "a town");
            yield return Play(pv, CampUse.CrustWait, CampCheats.CrustWait, 10f);
            yield return Play(pv, CampUse.WellGossip, CampCheats.WellGossip, 10.5f); // at the town's fountain
            Debug.Log("[695 uses] misses: " + string.Join(" | ", failed));
            CloseSlice();
            Assert.IsEmpty(failed, string.Join(" | ", failed));
        }
    }

    /// <summary>#695 (D rules, as DogWorkShare): 5 view days of a village, natural uses on.</summary>
    [Category("Village695Work")]
    public class VillageWorkShare : PlayTestBase
    {
        static bool WorkKind(ActivityKind k) => k >= ActivityKind.Gather && k <= ActivityKind.Prepare
            || k is ActivityKind.SweepStep or ActivityKind.HangWashing or ActivityKind.ChopKindling or ActivityKind.MindBabies or ActivityKind.Carry or ActivityKind.FetchWater
                or ActivityKind.RollBarrel or ActivityKind.FeedFire or ActivityKind.ScrapeHide or ActivityKind.HangFish or ActivityKind.TakeDownFish or ActivityKind.MoveRack
                or ActivityKind.TakeInWashing;

        [UnityTest, Timeout(900000)]
        public IEnumerator VillageUsesTakeFreeTimeOnly_WorkWithin5PercentOver5ViewDays()
        {
            UseLivingLand(true);
            GameUiPanel.AcceptInputUnfocused(true);
            yield return OpenSlice(750, 1334, seed: 4242);
            Debug.Log("[695 work] " + CampCheats.StageVillage(7f, false, 4, onlyCheats: false));
            yield return Frames(10);
            var pv = PeopleView.Find();
            pv.timeScale = 16f;
            float work = 0f, taken = 0f, eat = 0f, eatTaken = 0f, sleep = 0f, sleepTaken = 0f;
            int days = 0; float hourWas = pv.Now.hour; float t0 = Time.realtimeSinceStartup;
            while (days < 5 && Time.realtimeSinceStartup - t0 < 780f)
            {
                yield return null;
                float dt = Time.deltaTime * pv.timeScale;
                if (pv.Now.hour < hourWas - 12f) { days++; AquariumCiv.Game.GameClock.DebugSkipHours(1.0 / 12.0 + 0.001); /* a view day is a sim month: at x16 the view would loop one month five times */ Debug.Log($"[695 work] day {pv.Now.day}: {string.Join(", ", pv.Camp.Started.Where(kv => kv.Key >= CampUse.FieldTag).Select(kv => $"{kv.Key} x{kv.Value}"))}"); }
                hourWas = pv.Now.hour;
                foreach (var a in pv.Activities)
                {
                    bool cued = pv.Camp.Cued(a.who.Key) && pv.Camp.Scenes.Any(s => s.use >= CampUse.FieldTag && s.keys.Contains(a.who.Key));
                    if (WorkKind(a.kind)) { work += dt; if (cued) taken += dt; }
                    else if (a.kind == ActivityKind.Eat) { eat += dt; if (cued) eatTaken += dt; }
                    else if (a.kind == ActivityKind.Sleep) { sleep += dt; if (cued) sleepTaken += dt; }
                }
            }
            pv.timeScale = 1f;
            var uses = string.Join(", ", pv.Camp.Started.Where(kv => kv.Key >= CampUse.FieldTag).Select(kv => $"{kv.Key} x{kv.Value}"));
            Debug.Log($"[695 work] {days} view days; village uses started: {uses}; work {work:0} s, in a village use {taken:0.0} s; meals {eat:0} ({eatTaken:0.0}); sleep {sleep:0} ({sleepTaken:0.0}, {100f * sleepTaken / Mathf.Max(1f, sleep):0.0} %), bedtime cuts {pv.Camp.SleepCut}; work {100f * taken / Mathf.Max(1f, work):0.0} %; {pv.Camp.VillageStatus()}");
            Debug.Log("[695 work] why not: " + string.Join(" | ", pv.Camp.WhyNot.OrderByDescending(kv => kv.Value).Select(kv => $"{kv.Key} x{kv.Value}")));
            Assert.GreaterOrEqual(days, 5, "ran 5 view days");
            Assert.Greater(work, 0f);
            Assert.Less(taken / work, 0.05f, "under 5 % of work minutes in a village use");
            Assert.Less(eat > 0f ? eatTaken / eat : 0f, 0.05f, "no meal lost");
            Assert.LessOrEqual(sleep > 0f ? sleepTaken / sleep : 0f, 0.02f, "no sleep lost (2 %: bedtime ends a use)");
            Assert.IsNotEmpty(uses, "the village uses ran by themselves");
            CloseSlice();
        }
    }

    [Category("LastDays788Play")]
    public class VillageLastDays : PlayTestBase
    {
        [UnityTest, Timeout(900000)]
        public IEnumerator AVisitorSitsWithTheFrailElder_OnlyWhileTheElderIsOnTheStool_InAVillage()
        {
            UseLivingLand(true);
            GameUiPanel.AcceptInputUnfocused(true);
            yield return OpenSlice(750, 1334, seed: 4242);
            Debug.Log("[788 live] " + CampCheats.StageVillage(8f, false, 4, onlyCheats: false));
            yield return new WaitForSeconds(3f);
            var sim = AquariumCiv.Game.GameClock.Instance.Sim;
            Assert.AreEqual(Era.Village, sim.State.era, "a village");
            var pv = PeopleView.Find();
            var oldest = sim.State.notables.Where(n => n.alive && sim.KinOf(n).Any()).OrderByDescending(n => n.age).First();
            Debug.Log("[788 live] " + SimDebugPanel.LastDaysCheat(sim, oldest.id));
            var elder = PersonRef.Notable(oldest.id);
            yield return new WaitForSeconds(1f);
            Assert.Greater(pv.Planner.FrailtyOf(elder), 0f, "the elder is in their last days");
            var plan = new System.Collections.Generic.List<PersonActivity>();
            pv.Planner.DaySchedule(elder, pv.Now.day, plan);
            Debug.Log($"[788 live] {oldest.name} ({sim.RoleOf(oldest) ?? oldest.job}), day {pv.Now.day}: " + string.Join(" ", plan.Select(a => $"{a.start:0.00}-{a.end:0.00} {a.kind}{(a.moving ? ">" : "")}@{a.place}/{a.prop}")));
            var seen = new System.Collections.Generic.Dictionary<string, int>();
            pv.timeScale = 8f;
            int frames = 0, visitFrames = 0, stoolFrames = 0, offStool = 0, heldByUse = 0;
            var bad = new System.Collections.Generic.List<string>();
            var hist = new System.Collections.Generic.Dictionary<string, int>();
            float t0 = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t0 < 150f && oldest.alive)
            {
                yield return null;
                frames++;
                PersonActivity ea = default; bool found = false;
                foreach (var a in pv.Activities) if (a.who == elder) { ea = a; found = true; break; }
                if (!found) continue;
                bool onStool = ea.kind == ActivityKind.SitOut && !ea.moving && ea.prop == Prop.Stool;
                string ek = $"{ea.kind}/{ea.prop}@{ea.place.kind}/{ea.source}";
                seen[ek] = seen.TryGetValue(ek, out int e0) ? e0 + 1 : 1;
                if (onStool) stoolFrames++;
                foreach (var v in pv.Activities)
                {
                    if (v.kind != ActivityKind.SitWithOld || v.moving || v.partner != elder) continue;
                    visitFrames++;
                    string why = !onStool || ea.place != v.place ? $"elder {ea.kind}/{ea.prop} at {ea.place} (moving {ea.moving}, {ea.source}), visitor at {v.place} ({v.source})"
                        : pv.Camp.Busy(elder.Key) ? "the elder's doll is in the camp use " + pv.Camp.UseOf(elder.Key) : null;
                    if (why == null) continue;
                    if (why.StartsWith("the elder's")) heldByUse++; else offStool++;
                    string key = why.Split('(')[0];
                    hist[key] = hist.TryGetValue(key, out int c) ? c + 1 : 1;
                    if (bad.Count < 6) bad.Add($"day {pv.Now.day} {pv.Now.hour:0.00} h: {v.who.Key} beside the elder; {why}");
                }
            }
            pv.timeScale = 1f;
            Debug.Log("[788 live] the elder's frames: " + string.Join(" | ", seen.OrderByDescending(kv => kv.Value).Select(kv => kv.Key + " x" + kv.Value)));
            Debug.Log($"[788 live] {frames} frames to day {pv.Now.day} {pv.Now.hour:0.0} h: elder on the stool {stoolFrames}, a visitor beside them {visitFrames}, off the stool {offStool}, held by a use {heldByUse}; " +
                      string.Join(" | ", hist.Select(kv => kv.Key + " x" + kv.Value)) + "; " + string.Join("; ", bad));
            Assert.IsTrue(oldest.alive, "the elder lived through the watch");
            Assert.Greater(stoolFrames, 0, "the elder sat on their stool");
            Assert.Greater(visitFrames, 0, "the family came to sit with them");
            Assert.AreEqual(0, offStool + heldByUse, "nobody sits beside an empty stool: " + string.Join("; ", bad));
            AssertNoErrors("788 live");
            CloseSlice();
        }
    }
}
