// #776 (animation-specs B81.5, approved 2026-11-26): the founder's statue, used in the real box (PlayMode; run them from the Editor:
// PROJ=... bash Tools/run-tests.sh "play|AquariumCiv.View.PlayTests.StatuePlayTests||" ; each is one box, ~2 to 8 minutes).
// - TheRaising: "Statue: raise now" in a village box: the held scene plays (its caption is the line), leaves the carved statue on the green with the
//   bunch at its foot; the statue stands where the sim says and is solid.
// - EachUse: the statue stands; each use's cheat starts its scene, it plays out and the people are free again.
// - TheRaising_TheLeanHolds (#783/#776 tester, polish 2): from the home view of a village with its life on, the lean holds the plinth and the carver
//   in the middle 60 % of the screen for the whole raising (a village use starting meanwhile must not pull it away) and lets go only when it ends.
// - TapOnTheStatue: a tap on the statue opens the person's card.
// - TheStatueUses_TakeFreeTimeOnly: 10 view days of a town with the statue and the natural uses on: the work minutes the uses take stay under 5 %,
//   no meal or sleep is lost (D rules), and the uses ran by themselves.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using AquariumCiv.Game;
using AquariumCiv.GameUI;
using AquariumCiv.Life;
using AquariumCiv.Sim;
using AquariumCiv.View.ShowMe;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AquariumCiv.View.PlayTests
{
    [Category("Statue776Play")]
    public class StatuePlayTests : PlayTestBase
    {
        static SimCore Sim => GameClock.Instance != null ? GameClock.Instance.Sim : null;
        static PeopleView Pv => PeopleView.Find();

        [TearDown]
        public void Restore()
        {
            CampLife.OnlyCheats = false;
        }

        /// <summary>A lived-in box grown into a village (the "Jump: a village box" cheat), at <paramref name="hour"/>.</summary>
        IEnumerator VillageBox(float hour, bool town = false, bool onlyCheats = true)
        {
            UseLivingLand(true);
            GameUiPanel.AcceptInputUnfocused(true);
            ShowMeDirector.BubblesOn = false;
            yield return OpenSlice(750, 1334, seed: 4242);
            GameUiPanel.AcceptInputUnfocused(true);
            Debug.Log("[776] " + CampCheats.StageVillage(hour, town, 6, onlyCheats));
            yield return new WaitForSeconds(3f);
            Assert.AreEqual(town ? Era.Town : Era.Village, Sim.State.era, "a village or a town");
        }

        [UnityTest, Timeout(900000)]
        public IEnumerator TheRaising_PlaysInAVillageBox_AndLeavesTheStatue()
        {
            yield return VillageBox(10f);
            var sim = Sim; var pv = Pv;
            int started = StoryMoment.Started;
            string r = CampCheats.StatueRaiseNow();
            Debug.Log("[776] " + r);
            yield return Until(() => pv.Camp.Playing(CampUse.StatueRaise), 25f); // the stage looks every second
            Assert.IsTrue(pv.Camp.Playing(CampUse.StatueRaise), $"the raising plays ({r}; story: {StoryMoment.LastWhy}; camp: {pv.Camp.LastStoryWhy}; stage: {HeldMomentStage.Instance?.LastWhy}; " +
                $"playing: {string.Join(", ", sim.PlayingMoments().Select(m => m.kind + "#" + m.id))}; queue: {sim.MomentQueueStatus()})");
            Assert.Greater(StoryMoment.Started, started, "its held moment started");
            var who = sim.StatueNotable;
            Assert.IsNotNull(who, "the person is named");
            Assert.IsFalse(who.alive, "never a statue of the living");
            Assert.AreEqual($"{who.name} is made of stone now, on the green.", StoryMoment.LastCaption, "the caption says the line");
            var sc = pv.Camp.Scenes.Last(s => s.use == CampUse.StatueRaise);
            Assert.Greater(sc.keys.Count, 1, "a carver and a helper at least");
            Assert.Less(sc.Length, LifePlanner.StorySeconds, "the scene fits the story moment's time");
            // the stages step on at the three hits; the plinth stands from the start
            yield return Until(() => pv.Camp.StatueDrawn, 5f);
            Assert.IsTrue(pv.Camp.StatueDrawn, "the plinth is drawn from the start");
            var seen = new HashSet<StatueStage>();
            float t0 = Time.time;
            while (pv.Camp.Playing(CampUse.StatueRaise) && Time.time - t0 < 90f) { if (pv.Camp.StatueDrawn) seen.Add(pv.Camp.StatueStageDrawn); yield return null; }
            Assert.IsFalse(pv.Camp.Playing(CampUse.StatueRaise), "the scene ends");
            Debug.Log("[776] stages seen: " + string.Join(", ", seen));
            foreach (var st in new[] { StatueStage.Plinth, StatueStage.Block, StatueStage.Rough, StatueStage.Headed, StatueStage.Carved })
                Assert.IsTrue(seen.Contains(st), "the stage " + st + " was drawn");
            yield return new WaitForSeconds(3f);
            Assert.IsTrue(sim.StatueStands, "the statue stands");
            Assert.IsTrue(pv.Camp.StatueDrawn, "and is drawn");
            Assert.AreEqual(StatueStage.Carved, pv.Camp.StatueStageDrawn, "carved");
            Assert.IsFalse(pv.Camp.BlockDrawn, "the block is part of the figure now");
            Assert.IsTrue(pv.Camp.BunchDrawn, "the first flowers lie at its foot until night");
            Assert.IsTrue(sim.FindStatueSite(out float x, out float z, out _));
            Assert.Less((pv.Camp.StatueDrawnAt - new Vector2(x, z)).magnitude, 0.01f, "it stands where the sim says");
            Assert.IsTrue(PeopleView.StatueSolid.HasValue, "people go round it");
            Assert.IsEmpty(pv.Camp.Scenes.Where(s => s.use == CampUse.StatueRaise), "the people are free");
            CloseSlice();
        }

        static PersonRef RefOf(int key) => key > 0 ? PersonRef.Notable(key) : PersonRef.Crowd(-key - 1);

        /// <summary>#783/#776 tester (polish 2): the raising's lean framed the plinth at 3 s, slid toward the front glass by 10 to 17 s (the carver and
        /// the crowd under the HUD) and was home at 24 s while the raising ran to 32 s. The lean now holds the plinth and the carver in the middle
        /// 60 % for the whole scene, whatever other use starts in the village meanwhile, and lets go when it ends. Shots: Temp/agent/776/lean_*.</summary>
        [UnityTest, Timeout(900000)]
        public IEnumerator TheRaising_TheLeanHoldsThePlinthAndTheCarver_UntilItEnds()
        {
            yield return VillageBox(10f, onlyCheats: false); // the village's own life on, as a player has it: its uses start during the raising
            var sim = Sim; var pv = Pv;
            var rig = Camera.main.GetComponent<BoxCamera>(); var box = UnityEngine.Object.FindAnyObjectByType<BoxView>();
            HeldMomentLean.Enabled = true; HeldMomentLean.DebugForget();
            yield return Until(() => !rig.IsFlying, 6f);
            var homeTarget = rig.target; float homeZoom = rig.zoom;
            int leans = HeldMomentLean.Leans;
            string r = CampCheats.StatueRaiseNow();
            Debug.Log("[776 lean] " + r);
            yield return Until(() => pv.Camp.Playing(CampUse.StatueRaise), 25f);
            Assert.IsTrue(pv.Camp.Playing(CampUse.StatueRaise), $"the raising plays ({r}; {StoryMoment.LastWhy}; {pv.Camp.LastStoryWhy})");
            var sc = pv.Camp.Scenes.Last(s => s.use == CampUse.StatueRaise);
            float start = Time.time;
            yield return Until(() => HeldMomentLean.Leans > leans, 8f);
            Assert.Greater(HeldMomentLean.Leans, leans, "the camera leaned in: " + HeldMomentLean.LastWhy);
            yield return Until(() => !rig.IsFlying, 6f);
            yield return new WaitForSeconds(1f);
            Vector3 Vp(Vector2 at) => Camera.main.WorldToViewportPoint(box.Root.TransformPoint(new Vector3(at.x, box.SurfaceHeight(at.x, at.y) + 0.06f, at.y)));
            bool Mid(Vector3 v) => v.z > 0f && v.x >= 0.2f && v.x <= 0.8f && v.y >= 0.2f && v.y <= 0.8f;
            int askable = 0, frames = 0, plinthOff = 0, carverOff = 0, released = 0, othersMax = 0, shot = 0;
            string firstOff = "", stateWas = "";
            var carver = RefOf(sc.keys[0]);
            while (pv.Camp.Playing(CampUse.StatueRaise) && Time.time - start < 90f)
            {
                float u = Time.time - start;
                bool last = u > sc.Length - 1.5f; // the last steps back: the moment closes a frame after
                frames++;
                othersMax = Mathf.Max(othersMax, pv.Camp.Scenes.Count(s => s != sc));
                var pvp = Vp(sc.focus);
                if (!Mid(pvp)) { plinthOff++; if (firstOff == "") firstOff = $"plinth at ({pvp.x:0.00}, {pvp.y:0.00}) at {u:0.0} s"; }
                if (pv.TryGetDollPosition(carver, out var cp) && !Mid(Vp(cp)) && !last) { carverOff++; if (firstOff == "") { var cv = Vp(cp); firstOff = $"carver at ({cv.x:0.00}, {cv.y:0.00}) at {u:0.0} s"; } }
                if (!HeldMomentLean.Holding && !last) released++;
                if (HeldMomentLean.Holding && !RecallPromptView.Busy()) askable++; // #797: no question over the scene
                string stateNow = $"holding {HeldMomentLean.Holding}, leaning {rig.Leaning}, glide {rig.GlideArmed}, raise scenes {pv.Camp.Scenes.Count(s => s.use == CampUse.StatueRaise)}, ours {pv.Camp.Scenes.Contains(sc)}, staging {HeldMomentStage.Instance?.AnyPlayingStaging()?.Moment?.kind}#{HeldMomentStage.Instance?.AnyPlayingStaging()?.Moment?.id} of {HeldMomentStage.Instance?.StagingCount}, sim playing {string.Join(",", sim.PlayingMoments().Select(m => m.kind + "#" + m.id))}, why {HeldMomentLean.LastWhy}";
                if (stateNow != stateWas) { Debug.Log($"[776 lean] {u:0.0} s: {stateNow}"); stateWas = stateNow; }
                if (u >= 3f + shot * 7f && shot < 5) { yield return Shot($"776/lean_{shot}_{u:00}s"); shot++; }
                yield return null;
            }
            Debug.Log($"[776 lean] {frames} frames of the raising: plinth off the middle 60 % {plinthOff}, carver off {carverOff}, lean let go early {released}; other scenes at once up to {othersMax}; {firstOff}; last look {HeldMomentLean.LastLook}; why {HeldMomentLean.LastWhy}");
            Assert.IsFalse(pv.Camp.Playing(CampUse.StatueRaise), "the raising ends");
            Assert.Greater(frames, 100, "watched the raising");
            Assert.AreEqual(0, released, "the lean holds until the raising ends");
            Assert.AreEqual(0, askable, "#797: the playtest question waits while a held scene plays");
            Assert.LessOrEqual(plinthOff, frames / 20, "the plinth stays in the middle 60 % (" + firstOff + ")");
            Assert.LessOrEqual(carverOff, frames / 10, "the carver stays in the middle 60 % (" + firstOff + ")");
            yield return Until(() => !rig.GlideArmed && !rig.Leaning && !rig.IsFlying, 15f);
            yield return new WaitForSeconds(0.5f);
            Assert.AreEqual(homeZoom, rig.zoom, 0.03f, "back at the player's zoom after the raising");
            Assert.Less(Vector3.Distance(homeTarget, rig.target), 0.1f, "back at the player's view after the raising");
            AssertNoErrors("776 lean");
            CloseSlice();
        }

        [UnityTest, Timeout(900000)]
        public IEnumerator EachUse_StartsPlaysOut_AndTheStatueStays()
        {
            yield return VillageBox(10f, town: true);
            var sim = Sim; var pv = Pv;
            Debug.Log("[776] " + CampCheats.StatueStand());
            yield return Until(() => pv.Camp.StatueDrawn, 8f);
            Assert.IsTrue(pv.Camp.StatueDrawn, "the statue stands");
            var misses = new List<string>();
            foreach (var (use, cheat, hour) in new (CampUse, Func<string>, float)[]
            {
                (CampUse.StatueClimb, CampCheats.StatueClimb, 11f), (CampUse.StatueFlowers, CampCheats.StatueFlowers, 9f), (CampUse.StatueSit, CampCheats.StatueSit, 12f),
                (CampUse.StatueNod, CampCheats.StatueNod, 11f), (CampUse.StatueSweep, CampCheats.StatueSweep, 9f),
            })
            {
                if (Mathf.Abs(pv.Now.hour - hour) > 0.5f) { PeopleView.DebugSetHour(hour); yield return new WaitForSeconds(1f); }
                string r = cheat();
                Debug.Log($"[776] {use}: {r}");
                yield return Until(() => pv.Camp.Playing(use), 3f);
                if (!pv.Camp.Playing(use)) { misses.Add($"{use} did not start ({r})"); continue; }
                var s = pv.Camp.Scenes.Last(x => x.use == use);
                Assert.Less(s.Length, 40f, use + " is a short scene");
                float t0 = Time.time;
                while (pv.Camp.Playing(use) && Time.time - t0 < s.Length + 3f) yield return null;
                if (pv.Camp.Playing(use)) misses.Add($"{use} did not end");
                foreach (int k in s.keys) if (pv.Camp.Busy(k) && pv.Camp.UseOf(k) == use.ToString()) misses.Add($"{use}: {k} is still held");
                Assert.IsTrue(pv.Camp.StatueDrawn, use + " leaves the statue");
            }
            Debug.Log("[776] misses: " + string.Join(" | ", misses));
            CloseSlice();
            Assert.IsEmpty(misses, string.Join(" | ", misses));
        }

        [UnityTest, Timeout(900000)]
        public IEnumerator TapOnTheStatue_OpensThePersonsCard()
        {
            yield return VillageBox(10f);
            var sim = Sim; var pv = Pv;
            Debug.Log("[776] " + CampCheats.StatueStand());
            PeopleView.DebugSetHour(3f); // everyone asleep: no doll between the finger and the stone
            yield return new WaitForSeconds(2f);
            Assert.IsTrue(pv.Camp.StatueDrawn);
            var at = pv.Camp.StatueDrawnAt;
            var rig = Camera.main.GetComponent<BoxCamera>(); var box = UnityEngine.Object.FindAnyObjectByType<BoxView>();
            rig.CancelFlight();
            rig.target = box.Root.TransformPoint(new Vector3(at.x, box.SurfaceHeight(at.x, at.y) + 0.15f, at.y));
            rig.zoom = Mathf.Max(0.05f, rig.zoom * 0.5f); rig.Apply();
            yield return null;
            var screen = (Vector2)Camera.main.WorldToScreenPoint(box.Root.TransformPoint(new Vector3(at.x, box.SurfaceHeight(at.x, at.y) + 0.12f, at.y)));
            var cards = PersonCardView.Instance;
            Assert.IsNotNull(cards);
            Assert.AreEqual(sim.StatueFor, cards.PickStatue(screen), "the finger is on the statue");
            Assert.AreEqual(-1, cards.PickStatue(screen + new Vector2(0f, 900f)), "and not on the sky");
            Assert.IsTrue(cards.WorldTap(screen), "the tap is used");
            Assert.AreEqual(sim.StatueFor, cards.OpenId, "the person's card is open");
            CloseSlice();
        }

        static bool WorkKind(ActivityKind k) => k >= ActivityKind.Gather && k <= ActivityKind.Prepare
            || k is ActivityKind.SweepStep or ActivityKind.HangWashing or ActivityKind.ChopKindling or ActivityKind.MindBabies or ActivityKind.Carry or ActivityKind.FetchWater
                or ActivityKind.RollBarrel or ActivityKind.FeedFire or ActivityKind.ScrapeHide or ActivityKind.HangFish or ActivityKind.TakeDownFish or ActivityKind.MoveRack
                or ActivityKind.TakeInWashing;

        /// <summary>D rules (as VillageWorkShare): 10 view days of a town at x16, the statue standing, natural uses on.</summary>
        [UnityTest, Timeout(1500000), Category("Statue776Work")]
        public IEnumerator TheStatueUses_TakeFreeTimeOnly_WorkWithin5PercentOver10ViewDays()
        {
            yield return VillageBox(7f, town: true, onlyCheats: false);
            var sim = Sim; var pv = Pv;
            Debug.Log("[776 work] " + CampCheats.StatueStand());
            yield return Frames(10);
            pv.timeScale = 16f;
            float work = 0f, taken = 0f, eat = 0f, eatTaken = 0f, sleep = 0f, sleepTaken = 0f;
            int days = 0; float hourWas = pv.Now.hour; float t0 = Time.realtimeSinceStartup;
            bool IsStatue(CampScene s) => s.use >= CampUse.StatueClimb && s.use <= CampUse.StatueSweep;
            while (days < 10 && Time.realtimeSinceStartup - t0 < 1380f)
            {
                yield return null;
                float dt = Time.deltaTime * pv.timeScale;
                if (pv.Now.hour < hourWas - 12f) { days++; GameClock.DebugSkipHours(1.0 / 12.0 + 0.001); /* a view day is a sim month */ }
                hourWas = pv.Now.hour;
                foreach (var a in pv.Activities)
                {
                    bool cued = pv.Camp.Cued(a.who.Key) && pv.Camp.Scenes.Any(s => IsStatue(s) && s.keys.Contains(a.who.Key));
                    if (WorkKind(a.kind)) { work += dt; if (cued) taken += dt; }
                    else if (a.kind == ActivityKind.Eat) { eat += dt; if (cued) eatTaken += dt; }
                    else if (a.kind == ActivityKind.Sleep) { sleep += dt; if (cued) sleepTaken += dt; }
                }
            }
            pv.timeScale = 1f;
            var uses = string.Join(", ", pv.Camp.Started.Where(kv => kv.Key >= CampUse.StatueClimb).Select(kv => $"{kv.Key} x{kv.Value}"));
            Debug.Log($"[776 work] {days} view days; statue uses started: {uses}; work {work:0} s, in a statue use {taken:0.0} s ({100f * taken / Mathf.Max(1f, work):0.00} %); meals {eat:0} ({eatTaken:0.0}); sleep {sleep:0} ({sleepTaken:0.0})");
            Debug.Log("[776 work] why not: " + string.Join(" | ", pv.Camp.WhyNot.Where(kv => kv.Key.StartsWith("Statue")).OrderByDescending(kv => kv.Value).Select(kv => $"{kv.Key} x{kv.Value}")));
            Assert.GreaterOrEqual(days, 10, "ran 10 view days");
            Assert.Greater(work, 0f);
            Assert.Less(taken / work, 0.05f, "under 5 % of the work minutes in a statue use");
            Assert.Less(eat > 0f ? eatTaken / eat : 0f, 0.05f, "no meal lost");
            Assert.LessOrEqual(sleep > 0f ? sleepTaken / sleep : 0f, 0.02f, "no sleep lost");
            Assert.IsNotEmpty(uses, "the statue's uses ran by themselves");
            CloseSlice();
        }

        /// <summary>B81.0 "Where" in a grown town (the "Jump: a town box" layout, then five more years lived with the view on): the spot is off every path,
        /// field and building, and a load of the save works out the same spot.</summary>
        [UnityTest, Timeout(900000)]
        public IEnumerator TheSpot_InAGrownTown_IsOffPathsFieldsAndBuildings_AndTheSameAfterALoad()
        {
            yield return VillageBox(10f, town: true, onlyCheats: false);
            var sim = Sim;
            Debug.Log("[776 spot] " + CampCheats.StatueStand());
            yield return Frames(10);
            Assert.IsTrue(sim.FindStatueSite(out float x0, out float z0, out float yaw0), "a spot on the green");
            for (int m = 0; m < 60; m++) { sim.Advance(1.0 / 12); yield return null; } // five years: the town keeps building round it
            yield return Frames(10);
            Assert.IsTrue(sim.FindStatueSite(out float x1, out float z1, out _));
            Assert.AreEqual(x0, x1, 1e-4f); Assert.AreEqual(z0, z1, 1e-4f);
            var w = sim.Settlement;
            Assert.IsTrue(AquariumCiv.Sim.Settle.StatueSpot.Fits(w.Map, x1, z1, null), "its footprint and clear ring are still open ground (no path, lot or yard)");
            Assert.IsFalse(RackSites.PathAt(sim, x1, z1), "not on a path");
            Assert.IsFalse(RackSites.FieldAt(sim, x1, z1), "not on a field");
            float nearLot = w.Lots.Where(l => !l.retired).Select(l => new Vector2(l.x - x1, l.z - z1).magnitude - 0.5f * Mathf.Max(l.w, l.d)).DefaultIfEmpty(9f).Min();
            Assert.Greater(nearLot, 0f, "not in a building's lot");
            var loaded = new SimCore(sim.Config, JsonUtility.FromJson<SimState>(JsonUtility.ToJson(sim.State)));
            Assert.IsTrue(loaded.FindStatueSite(out float x2, out float z2, out float yaw2), "the loaded box finds it");
            Debug.Log($"[776 spot] raised at ({x0:0.000}, {z0:0.000}) yaw {yaw0:0}; five years on ({x1:0.000}, {z1:0.000}); after a load ({x2:0.000}, {z2:0.000}) yaw {yaw2:0}; " +
                      $"{w.Lots.Count(l => !l.retired)} lots, the nearest lot edge {nearLot:0.00} u, the green's centre ({w.Map.greenX:0.00}, {w.Map.greenZ:0.00}), pop {sim.State.population}");
            Assert.AreEqual(x1, x2, 1e-4f, "the same spot after a load"); Assert.AreEqual(z1, z2, 1e-4f); Assert.AreEqual(yaw0, yaw2, 1e-3f);
            CloseSlice();
        }
    }
}
