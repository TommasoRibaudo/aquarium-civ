// S-ritual (B72-B77) in the real scene:
// - RitualScenes (category Ritual721, ~6 min): a lived-in camp; each ritual's cheat plays its own staged plan (not the festival's stand-in),
//   through to the end, says its beats' lines, and leaves what it leaves (purple hands, the table's board, the basket full at the Keeper's
//   tree; the spring empty basket and its run); the boat's two uses; the picked teller tells slow and short from behind the fire.
// - RitualWorkShare (category RitualWork, ~8 min): 5 view days from late summer at x16 with the rituals kept and the boat by the fire,
//   natural uses on: under 5 % of the work minutes go to the rituals and the boat, no meal or sleep is lost.
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
    [Category("Ritual721")]
    public class RitualScenes : PlayTestBase
    {
        readonly System.Collections.Generic.List<string> failed = new();

        IEnumerator Ritual(PeopleView pv, string ritual, Func<string> cheat, string what)
        {
            var sim = AquariumCiv.Game.GameClock.Instance.Sim;
            StagedMoments.StopAll();
            yield return new WaitForSeconds(0.5f);
            if (pv.Now.hour < 8f || pv.Now.hour > 11.5f) { PeopleView.DebugSetHour(9f); yield return new WaitForSeconds(1f); }
            int lines = StagedMoments.LinesSaid;
            string r = cheat();
            Debug.Log($"[ritual] {what}: {r}");
            var sc = StagedMoments.LastScene;
            if (sc == null || sc.kind != MomentKind.Ritual) { failed.Add($"{what}: no ritual staged ({r})"); yield break; }
            if (sc.plan.RitualStaged != ritual) { failed.Add($"{what}: the festival's stand-in played ({MomentStagePlan.LastWhyNot})"); yield break; }
            float t0 = Time.time, len = sc.plan.Duration;
            bool purple = false, boardSeen = false;
            while (Time.time - t0 < len + 2f && StagedMoments.Playing > 0)
            {
                if (ritual == RitualKind.Berrying && pv.Camp.Worn5Now > 0) purple = true;
                if (ritual == RitualKind.Feast && PropLook(sim, ConsequenceKind.LongTable) > 0) boardSeen = true;
                yield return null;
            }
            if (StagedMoments.Playing > 0) failed.Add($"{what}: still playing after {len:0} s");
            if (StagedMoments.LinesSaid <= lines) failed.Add($"{what}: no beat's line was said");
            if (ritual == RitualKind.Berrying && !purple) failed.Add("berrying: no purple hands");
            if (ritual == RitualKind.Feast && !boardSeen) failed.Add("feast: the table did not grow a board");
            if (ritual == RitualKind.Offering && sim.KeeperTreeGrown && PropLook(sim, ConsequenceKind.OfferingBasket) != 1) failed.Add("offering: the basket at the tree is not full");
            Debug.Log($"[ritual] {what} over after {Time.time - t0:0.0} s; {StagedMoments.DebugStatus()}");
        }

        static int PropLook(SimCore sim, string kind)
        {
            var p = sim.State.consequenceProps.Find(x => x.kind == kind);
            return p == null ? -1 : ConsequencePropsView.LookOf(p.id);
        }

        IEnumerator Use(PeopleView pv, CampUse use, Func<string> cheat, float hour)
        {
            if (Mathf.Abs(pv.Now.hour - hour) > 0.5f) { PeopleView.DebugSetHour(hour); yield return new WaitForSeconds(1f); }
            int before = pv.Camp.Started.TryGetValue(use, out int n) ? n : 0;
            string r = cheat();
            Debug.Log($"[ritual] {use}: {r}");
            int now = pv.Camp.Started.TryGetValue(use, out int m) ? m : 0;
            if (now <= before) { failed.Add($"{use} did not start ({r})"); yield break; }
            var s = pv.Camp.Scenes.LastOrDefault(x => x.use == use);
            float len = s != null ? Mathf.Min(s.Length, 45f) : 10f, t0 = Time.time;
            while (Time.time - t0 < len + 1f) yield return null;
        }

        [UnityTest, Timeout(900000)]
        public IEnumerator EachRitualPlaysItsOwnBeats_TheBoatsUses_TheTeller()
        {
            UseLivingLand(true);
            GameUiPanel.AcceptInputUnfocused(true);
            yield return OpenSlice(750, 1334, seed: 4242);
            EveningTalkView.AutoStart = false; // a long session's close would hold the later rituals back ("the evening talk")
            Debug.Log("[ritual] " + CampCheats.StageCamp(9f, 4));
            Debug.Log("[ritual] " + ConsequenceCheats.PlaceEvery());
            Debug.Log("[ritual] " + EcoDebug.AddBushes(6));
            Debug.Log("[ritual] grown-ups +" + AquariumCiv.Game.GameClock.Instance.Sim.DebugAddKids(3, 22f)); // a fuller camp: the log line needs free grown-ups
            Debug.Log("[ritual] " + CampCheats.GrowTree());
            yield return new WaitForSeconds(3f);
            var pv = PeopleView.Find();
            var sim = AquariumCiv.Game.GameClock.Instance.Sim;
            yield return Ritual(pv, RitualKind.Berrying, () => StagedMoments.DebugPlayRitualNow(RitualKind.Berrying), "berrying");
            yield return Ritual(pv, RitualKind.Woodpile, StagedMoments.DebugLogLineDrop, "the log line (a log dropped)");
            yield return Ritual(pv, RitualKind.Feast, StagedMoments.DebugFeastBoard, "the long table (a board added)");
            yield return Ritual(pv, RitualKind.Offering, () => StagedMoments.DebugPlayRitualNow(RitualKind.Offering), "the offering to the Keeper's tree");
            // B75.b: the spring line, the empty basket
            string empty = StagedMoments.DebugOfferingEmpty();
            Debug.Log("[ritual] " + empty);
            if (!pv.Camp.Started.ContainsKey(CampUse.OfferingEmpty)) failed.Add("the empty basket's run did not start: " + empty);
            else { var s = pv.Camp.Scenes.LastOrDefault(x => x.use == CampUse.OfferingEmpty); float t0 = Time.time; while (s != null && Time.time - t0 < s.Length + 1f) yield return null; }
            var ob = sim.State.consequenceProps.Find(x => x.kind == ConsequenceKind.OfferingBasket);
            if (ob != null && ConsequencePropsView.LookOf(ob.id) != ConsequenceMeshes.OfferingEmpty) failed.Add($"the basket is not back at the tree, empty (look {ConsequencePropsView.LookOf(ob.id)})");
            // B77
            yield return Use(pv, CampUse.BoatSort, CampCheats.BoatSort, 11f);
            yield return Use(pv, CampUse.BoatHouse, CampCheats.BoatHouse, 11f);
            // B76: the picked teller, slow then short
            foreach (int manner in new[] { 0, 1 })
            {
                Debug.Log("[ritual] " + SliceDebugPanel.DebugStoryTonight(manner));
                float t0 = Time.time; bool told = false; DollStyle drew = DollStyle.Stand;
                while (Time.time - t0 < 60f && pv.Now.hour < (manner == 0 ? LifePlanner.TellSlowUntil : LifePlanner.TellShortUntil) - 0.05f)
                {
                    if (pv.Camp.TellerNow >= 0) { told = true; drew = pv.StyleOf(pv.Camp.TellerNow); }
                    yield return null;
                }
                Debug.Log($"[ritual] teller {manner}: {pv.Camp.TellerStatus()} drew {drew}");
                if (!told) failed.Add($"teller {(manner == 0 ? "slow" : "short")}: nobody told it");
                else if (drew != DollStyle.Tell) failed.Add($"teller {(manner == 0 ? "slow" : "short")}: drawn {drew}, not Tell");
                PeopleView.DebugSetHour(9f); yield return new WaitForSeconds(1f);
            }
            Debug.Log("[ritual] misses: " + string.Join(" | ", failed));
            EveningTalkView.AutoStart = true;
            CloseSlice();
            Assert.IsEmpty(failed, string.Join(" | ", failed));
        }
    }

    /// <summary>S-ritual (D rules, as VillageWorkShare): 5 view days through the ritual months, the rituals kept, the boat by the fire.</summary>
    [Category("RitualWork")]
    public class RitualWorkShare : PlayTestBase
    {
        static bool WorkKind(ActivityKind k) => k >= ActivityKind.Gather && k <= ActivityKind.Prepare
            || k is ActivityKind.SweepStep or ActivityKind.HangWashing or ActivityKind.ChopKindling or ActivityKind.MindBabies or ActivityKind.Carry or ActivityKind.FetchWater
                or ActivityKind.RollBarrel or ActivityKind.FeedFire or ActivityKind.ScrapeHide or ActivityKind.HangFish or ActivityKind.TakeDownFish or ActivityKind.MoveRack
                or ActivityKind.TakeInWashing;

        [UnityTest, Timeout(900000)]
        public IEnumerator RitualsAndTheBoat_TakeFreeTimeOnly_WorkWithin5PercentOver5ViewDays()
        {
            UseLivingLand(true);
            GameUiPanel.AcceptInputUnfocused(true);
            yield return OpenSlice(750, 1334, seed: 4242);
            Debug.Log("[ritual work] " + CampCheats.StageCamp(7f, 4, onlyCheats: false));
            Debug.Log("[ritual work] " + ConsequenceCheats.PlaceEvery());
            Debug.Log("[ritual work] " + EcoDebug.AddBushes(6));
            var sim = AquariumCiv.Game.GameClock.Instance.Sim;
            // keep one of each pair (berrying, the feast) and the offering's pair partner off; start in calendar month 7
            foreach (var k in new[] { RitualKind.Berrying, RitualKind.Feast }) if (!sim.State.customs.Contains(RitualKind.CustomOf(k))) sim.State.customs.Add(RitualKind.CustomOf(k));
            for (int i = 0; i < 12 && sim.CalendarMonth != 7; i++) AquariumCiv.Game.GameClock.DebugSkipHours(1.0 / 12.0 + 0.001);
            if (AquariumCiv.Game.GameClock.Instance != null) AquariumCiv.Game.GameClock.Instance.config.moments = true;
            yield return Frames(10);
            var pv = PeopleView.Find();
            pv.timeScale = 16f;
            float work = 0f, taken = 0f, takenRitual = 0f, eat = 0f, eatTaken = 0f, sleep = 0f, sleepTaken = 0f;
            int days = 0, rituals = 0; float hourWas = pv.Now.hour; float t0 = Time.realtimeSinceStartup;
            bool InRitual(int key) => StagedMoments.PlayingRitual(sim, out var sc, out float s) && sc.plan.StepAt(key, s, out _, out _);
            bool InUse(int key) => pv.Camp.Cued(key) && pv.Camp.Scenes.Any(x => x.use >= CampUse.OfferingEmpty && x.keys.Contains(key));
            bool wasRitual = false; long playedDay = -1;
            while (days < 5 && Time.realtimeSinceStartup - t0 < 780f)
            {
                yield return null;
                float dt = Time.deltaTime * pv.timeScale;
                if (pv.Now.hour < hourWas - 12f) { days++; AquariumCiv.Game.GameClock.DebugSkipHours(1.0 / 12.0 + 0.001); Debug.Log($"[ritual work] day {pv.Now.day} (month {sim.CalendarMonth}): rituals {rituals}, {string.Join(", ", pv.Camp.Started.Where(kv => kv.Key >= CampUse.OfferingEmpty).Select(kv => $"{kv.Key} x{kv.Value}"))}"); }
                hourWas = pv.Now.hour;
                // the month's ritual, played at 10:00 as the Keeper would watch it (the sim queues it only for a watching Keeper)
                int cal = sim.CalendarMonth;
                string want = cal == 8 ? RitualKind.Berrying : cal == 9 ? RitualKind.Feast : cal == 10 ? RitualKind.Woodpile : null;
                if (want != null && pv.Now.hour >= 10f && pv.Now.hour < 11f && playedDay != pv.Now.day && StagedMoments.Playing == 0)
                { playedDay = pv.Now.day; pv.timeScale = 1f; Debug.Log("[ritual work] " + StagedMoments.DebugPlayRitualNow(want)); }
                if (pv.timeScale < 16f && StagedMoments.Playing == 0 && playedDay == pv.Now.day) pv.timeScale = 16f;
                bool r = StagedMoments.PlayingRitual(sim, out _, out _);
                if (r && !wasRitual) rituals++;
                wasRitual = r;
                foreach (var a in pv.Activities)
                {
                    bool inRitual = InRitual(a.who.Key), cued = inRitual || InUse(a.who.Key);
                    if (WorkKind(a.kind)) { work += dt; if (inRitual) takenRitual += dt; else if (cued) taken += dt; }
                    else if (a.kind == ActivityKind.Eat) { eat += dt; if (cued) eatTaken += dt; }
                    else if (a.kind == ActivityKind.Sleep) { sleep += dt; if (cued) sleepTaken += dt; }
                }
            }
            pv.timeScale = 1f;
            var uses = string.Join(", ", pv.Camp.Started.Where(kv => kv.Key >= CampUse.OfferingEmpty).Select(kv => $"{kv.Key} x{kv.Value}"));
            float yearWork = work * 12f / Mathf.Max(1, days); // a view day is a sim month: the year's three rituals against a year's work
            Debug.Log($"[ritual work] {days} view days; rituals staged {rituals}; uses {uses}; work {work:0} s, in a boat or basket use {taken:0.0} s ({100f * taken / Mathf.Max(1f, work):0.0} %), in the year's rituals {takenRitual:0.0} s ({100f * takenRitual / Mathf.Max(1f, yearWork):0.0} % of a year's work); meals {eat:0} ({eatTaken:0.0}); sleep {sleep:0} ({sleepTaken:0.0})");
            Assert.GreaterOrEqual(days, 5, "ran 5 view days");
            Assert.Greater(work, 0f);
            Assert.GreaterOrEqual(rituals, 2, "the month's rituals played");
            Assert.Less(taken / work, 0.05f, "under 5 % of work minutes in a boat or basket use");
            Assert.Less(takenRitual / yearWork, 0.05f, "the year's rituals take under 5 % of a year's work minutes (they come once a year each)");
            Assert.Less(eat > 0f ? eatTaken / eat : 0f, 0.05f, "no meal lost");
            Assert.LessOrEqual(sleep > 0f ? sleepTaken / sleep : 0f, 0.02f, "no sleep lost");
            CloseSlice();
        }
    }
}
