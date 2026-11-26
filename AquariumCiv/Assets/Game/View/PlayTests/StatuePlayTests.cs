// #776 (animation-specs B81.5, approved 2026-11-26): the founder's statue, used in the real box (PlayMode; run them from the Editor:
// PROJ=... bash Tools/run-tests.sh "play|AquariumCiv.View.PlayTests.StatuePlayTests||" ; each is one box, ~2 to 8 minutes).
// - TheRaising: "Statue: raise now" in a village box: the held scene plays (its caption is the line), leaves the carved statue on the green with the
//   bunch at its foot; the statue stands where the sim says and is solid.
// - EachUse: the statue stands; each use's cheat starts its scene, it plays out and the people are free again.
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
            Assert.IsTrue(pv.Camp.Playing(CampUse.StatueRaise), $"the raising plays ({r}; {StoryMoment.LastWhy})");
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
    }
}
