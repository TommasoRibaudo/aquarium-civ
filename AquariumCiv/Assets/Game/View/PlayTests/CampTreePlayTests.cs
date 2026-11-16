// #692 part 2 (B32): the Keeper's tree visits in the real box.
// - CampTreeWorkShare (Explicit, ~4 min): the D rule "the work still gets done": over 10 view days with the natural uses on, the minutes
//   people spend at work while a camp use has them stay under 5 % of all work minutes (each work kind), and no meal or sleep is lost.
// - Camp692TreeShots (Explicit): phone shots (750x1334) of each visit at the default camera AND zoomed onto the use, in a fresh box each;
//   they land in Temp/agent/692p2/.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using AquariumCiv.Game;
using AquariumCiv.GameUI;
using AquariumCiv.Life;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AquariumCiv.View.PlayTests
{
    [Explicit, Category("CampWorkShare")]
    public class CampTreeWorkShare : PlayTestBase
    {
        /// <summary>The work kinds: the work loops at a site, the home chores, carrying, and the camp's hand work.</summary>
        static bool WorkKind(ActivityKind k) => k >= ActivityKind.Gather && k <= ActivityKind.Prepare
            || k is ActivityKind.SweepStep or ActivityKind.HangWashing or ActivityKind.ChopKindling or ActivityKind.MindBabies or ActivityKind.Carry or ActivityKind.FetchWater
                or ActivityKind.RollBarrel or ActivityKind.FeedFire or ActivityKind.ScrapeHide or ActivityKind.HangFish or ActivityKind.TakeDownFish or ActivityKind.MoveRack
                or ActivityKind.TakeInWashing;

        [UnityTest, Timeout(900000)]
        public IEnumerator UsesTakeFreeTimeOnly_WorkWithin5PercentOver10ViewDays()
        {
            UseLivingLand(true);
            GameUiPanel.AcceptInputUnfocused(true);
            yield return OpenSlice(750, 1334, seed: 4242);
            Debug.Log("[692 work] " + CampCheats.StageCamp(8f, 3, onlyCheats: false));
            Debug.Log("[692 work] " + CampCheats.GrowTree());
            CampLife.ForgetTreeVisits();
            yield return Frames(10);
            var pv = PeopleView.Find();
            pv.timeScale = 16f;
            var work = new Dictionary<ActivityKind, float>(); var taken = new Dictionary<ActivityKind, float>();
            float eat = 0f, eatTaken = 0f, sleep = 0f, sleepTaken = 0f;
            long dayWas = pv.Now.day; int days = 0;
            float hourWas = pv.Now.hour;
            float t0 = Time.realtimeSinceStartup;
            while (days < 10 && Time.realtimeSinceStartup - t0 < 780f)
            {
                yield return null;
                // The view clock runs x16 but a camp scene runs on the real pose clock: what a day's plan holds is counted in view time
                // (seconds a x1 day would take), what a scene takes in real seconds (what it takes at x1). Counting both in view time made
                // a 20 s scene weigh 16 times its real share (16 % on the first run).
                float dt = Time.deltaTime * pv.timeScale, real = Time.deltaTime;
                if (pv.Now.hour < hourWas - 12f) days++; // past midnight (the view day can repeat while the sim's month waits)
                hourWas = pv.Now.hour;
                foreach (var a in pv.Activities)
                {
                    bool cued = pv.Camp.Cued(a.who.Key);
                    if (WorkKind(a.kind)) { work[a.kind] = (work.TryGetValue(a.kind, out var w) ? w : 0f) + dt; if (cued) taken[a.kind] = (taken.TryGetValue(a.kind, out var x) ? x : 0f) + real; }
                    else if (a.kind == ActivityKind.Eat) { eat += dt; if (cued) eatTaken += real; }
                    else if (a.kind == ActivityKind.Sleep) { sleep += dt; if (cued) sleepTaken += real; }
                }
            }
            pv.timeScale = 1f;
            var started = string.Join(", ", pv.Camp.Started.Select(kv => $"{kv.Key} x{kv.Value}"));
            Debug.Log($"[692 work] {days} view days; uses started: {started}");
            float allWork = work.Values.Sum(), allTaken = taken.Values.Sum();
            foreach (var kv in work.OrderByDescending(k => k.Value))
                Debug.Log($"[692 work] {kv.Key}: {kv.Value:0} s at work, {(taken.TryGetValue(kv.Key, out var tk) ? tk : 0f):0.0} s of it in a camp use");
            Debug.Log($"[692 work] all work {allWork:0} s, in a use {allTaken:0.0} s ({(allWork > 0 ? allTaken / allWork * 100f : 0f):0.00} %); meals {eat:0} s ({eatTaken:0.0} in a use), sleep {sleep:0} s ({sleepTaken:0.0})");
            Assert.GreaterOrEqual(days, 10, "ran 10 view days");
            Assert.Greater(allWork, 0f, "somebody worked");
            Assert.Less(allTaken / allWork, 0.05f, "the work still gets done: under 5 % of work minutes in a camp use");
            foreach (var kv in work) if (kv.Value > 60f) Assert.Less((taken.TryGetValue(kv.Key, out var tk2) ? tk2 : 0f) / kv.Value, 0.05f, kv.Key + ": within 5 %");
            Assert.Less(eat > 0f ? eatTaken / eat : 0f, 0.05f, "no meal lost");
            Assert.Less(sleep > 0f ? sleepTaken / sleep : 0f, 0.05f, "no sleep lost");
            Assert.Greater(pv.Camp.Started.Count, 0, "the natural uses ran (" + started + ")");
            CloseSlice();
        }
    }

    [Explicit, Category("Camp692TreeShots")]
    public class Camp692TreeShots : PlayTestBase
    {
        /// <summary>A fresh box: a lived-in camp at <paramref name="hour"/>, the Keeper's tree grown, then the cheat; at each of <paramref name="at"/>
        /// seconds a shot at the default camera, and at <paramref name="zoomAt"/> one zoomed onto the use (CampLife.FocusNow).</summary>
        IEnumerator Use(string name, float hour, Func<string> cheat, float zoomAt, int kids, params float[] at)
        {
            UseLivingLand(true);
            GameUiPanel.AcceptInputUnfocused(true);
            yield return OpenSlice(750, 1334, seed: 4242);
            GameUiPanel.AcceptInputUnfocused(true);
            // the AnimLab recipe: each setup step, then 2 s of game (a plan settles between them)
            Debug.Log("[692p2 shots] " + CampCheats.StageCamp(hour, kids));
            yield return new WaitForSeconds(2f);
            Debug.Log("[692p2 shots] " + CampCheats.GrowTree());
            yield return new WaitForSeconds(2f);
            if (name == "b32d-quiet") { Debug.Log("[692p2 shots] " + ConsequenceCheats.RememberOnTree()); yield return new WaitForSeconds(2f); }
            if (hour >= 19f) { Debug.Log("[692p2 shots] " + DayLightDriver.DebugPauseClock(true)); yield return new WaitForSeconds(2f); } // dusk: tents and bed come in the next hour
            yield return new WaitForSeconds(2f);
            string r = cheat();
            Debug.Log($"[692p2 shots] {name}: {r}");
            var pv = PeopleView.Find();
            var rig = Camera.main.GetComponent<BoxCamera>();
            var box = UnityEngine.Object.FindAnyObjectByType<BoxView>();
            var times = at.Concat(new[] { zoomAt }).OrderBy(x => x).ToList();
            float t = 0f;
            foreach (float s in times)
            {
                yield return new WaitForSeconds(Mathf.Max(0f, s - t));
                t = s;
                if (Mathf.Approximately(s, zoomAt) && pv.Camp.FocusNow() is Vector2 f)
                {
                    Vector3 tgt0 = rig.target; float z0 = rig.zoom, p0 = rig.pitch, m0 = rig.minZoom;
                    var w = box.Root.TransformPoint(new Vector3(f.x, box.SurfaceHeight(f.x, f.y) + 0.08f, f.y));
                    for (int i = 0; i < 14; i++) { rig.CancelFlight(); rig.minZoom = 0.05f; rig.target = w; rig.zoom = 0.1f; rig.pitch = 38f; rig.Apply(); yield return null; }
                    Debug.Log($"[692p2 shots] {name} zoom: focus on screen at {Camera.main.WorldToViewportPoint(w)}");
                    yield return Shot($"692p2/{name}_zoom_{s:0.0}s");
                    t += 14f / 60f;
                    for (int i = 0; i < 4; i++) { rig.CancelFlight(); rig.target = tgt0; rig.zoom = z0; rig.pitch = p0; rig.minZoom = m0; rig.Apply(); yield return null; }
                }
                else yield return Shot($"692p2/{name}_default_{s:0.0}s");
            }
            Assert.Greater(pv.Camp.Started.Count, 0, name + ": a use played (" + r + ")");
            CloseSlice();
        }

        [UnityTest, Timeout(300000)] public IEnumerator B32a() => Use("b32a-sit", 12.5f, CampCheats.TreeSit, 12f, 2, 11.8f);
        [UnityTest, Timeout(300000)] public IEnumerator B32b() => Use("b32b-touch", 10f, CampCheats.TreeTouch, 9.2f, 2, 9f);
        [UnityTest, Timeout(300000)] public IEnumerator B32c() => Use("b32c-circle", 11f, CampCheats.TreeCircle, 12f, 4, 11.8f);
        [UnityTest, Timeout(300000)] public IEnumerator B32d() => Use("b32d-quiet", 19.1f, CampCheats.TreeQuiet, 22f, 2, 15f, 21.8f);
        [UnityTest, Timeout(300000)] public IEnumerator B32e() => Use("b32e-newlyweds", 19.1f, CampCheats.TreeWed, 11f, 2, 10.8f);
        [UnityTest, Timeout(300000)] public IEnumerator B32f() => Use("b32f-newborn", 9f, CampCheats.TreeNewborn, 9.6f, 2, 9.4f);
        [UnityTest, Timeout(300000)] public IEnumerator B32g() => Use("b32g-old-one", 15f, CampCheats.TreeOld, 12f, 2, 11.8f);
        [UnityTest, Timeout(300000)] public IEnumerator B32h() => Use("b32h-eldest-tells", 14f, CampCheats.TreeTell, 9f, 3, 8.8f);
    }
}
