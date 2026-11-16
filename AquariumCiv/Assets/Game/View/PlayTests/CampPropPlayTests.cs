// #693 (B54): the choice props' uses in the real box.
// - CampPropUses (category CampProps, ~6 min): one lived-in camp with every choice's thing standing; each cheat in turn starts its use, the
//   whole scene plays, and what it does to the thing shows (a basket taken, the axe out, a new row of names, flour on a kid, the bird
//   upright, the offering full). The people still eat: a supper after the uses still seats eaters at the fire.
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
    [Category("CampProps")]
    public class CampPropUses : PlayTestBase
    {
        readonly System.Collections.Generic.List<string> failed = new();

        int PropId(string kind)
        {
            var sim = AquariumCiv.Game.GameClock.Instance.Sim;
            var cv = ConsequencePropsView.Instance;
            foreach (var p in sim.ConsequenceProps) if (p.placed && p.kind == kind && cv.Standing.ContainsKey(p.id)) return p.id;
            return -1;
        }

        /// <summary>Starts the use, plays it through, and records what did not happen: <paramref name="during"/> must hold at some frame (a basket
        /// taken), <paramref name="after"/> at the end (all back).</summary>
        IEnumerator Play(PeopleView pv, CampUse use, Func<string> cheat, Func<bool> during, Func<bool> after, string what)
        {
            if (pv.Now.hour < 9f || pv.Now.hour > 15.5f) { PeopleView.DebugSetHour(11f); yield return new WaitForSeconds(1f); }
            int before = pv.Camp.Started.TryGetValue(use, out int n) ? n : 0;
            string r = cheat();
            Debug.Log($"[693 uses] {use}: {r}");
            int now = pv.Camp.Started.TryGetValue(use, out int m) ? m : 0;
            if (now <= before) { failed.Add($"{use} did not start ({r})"); yield break; }
            var s = pv.Camp.Scenes.LastOrDefault(x => x.use == use);
            float len = s != null ? s.Length : 10f;
            float t0 = Time.time; bool seen = during == null;
            while (Time.time - t0 < len + 1f) { yield return null; seen |= during != null && during(); }
            Debug.Log($"[693 uses] {use} after {Time.time - t0:0.0} s: {pv.Camp.PropMarks()}");
            if (!seen) failed.Add($"{use}: no {what} during it ({pv.Camp.PropMarks()})");
            if (after != null && !after()) failed.Add($"{use}: not as it should be after ({pv.Camp.PropMarks()})");
        }

        [UnityTest, Timeout(900000)]
        public IEnumerator EachUseStartsAndDoesItsThing()
        {
            UseLivingLand(true);
            GameUiPanel.AcceptInputUnfocused(true);
            yield return OpenSlice(750, 1334, seed: 4242);
            Debug.Log("[693 uses] " + CampCheats.StageCamp(10f, 4));
            yield return new WaitForSeconds(2f);
            Debug.Log("[693 uses] " + CampCheats.PondReady());
            Debug.Log("[693 uses] " + CampCheats.EveryThing());
            yield return new WaitForSeconds(3f);
            var pv = PeopleView.Find();
            var camp = pv.Camp;
            Assert.GreaterOrEqual(camp.PropsStanding, 13, "every choice's thing stands");
            int baskets = PropId(ConsequenceKind.Baskets), axe = PropId(ConsequenceKind.AxeStump), staff = PropId(ConsequenceKind.Staff), stone = PropId(ConsequenceKind.NameStone);
            int bird = PropId(ConsequenceKind.RoofBird), offer = PropId(ConsequenceKind.OfferingBasket), crate = PropId(ConsequenceKind.FishCrate);
            Func<int, int> look = ConsequencePropsView.LookOf;
            yield return Play(pv, CampUse.BasketTake, CampCheats.BasketTake, () => look(baskets) == 1, () => look(baskets) == 0, "basket taken");
            yield return Play(pv, CampUse.BasketTower, CampCheats.BasketTower, () => look(baskets) == 3, () => look(baskets) == 0, "all three in the tower");
            yield return Play(pv, CampUse.WoodStack, CampCheats.WoodStack, null, null, "");
            yield return Play(pv, CampUse.WoodShelter, CampCheats.WoodShelter, null, null, "");
            pv.Planner.ForcedWeather = null;
            int rows = camp.NameRowsCut(stone);
            yield return Play(pv, CampUse.NameCut, CampCheats.NameCut, null, () => camp.NameRowsCut(stone) == rows + 1, "a new row");
            yield return Play(pv, CampUse.NameTrace, CampCheats.NameTrace, null, null, "");
            yield return Play(pv, CampUse.CharmFlick, CampCheats.CharmFlick, null, null, "");
            yield return Play(pv, CampUse.PotDrum, CampCheats.PotDrum, null, null, "");
            yield return Play(pv, CampUse.StumpSplit, CampCheats.StumpSplit, () => look(axe) == 1, () => look(axe) == 0, "the axe out of the stump");
            yield return Play(pv, CampUse.StumpSit, CampCheats.StumpSit, null, null, "");
            yield return Play(pv, CampUse.StaffWalk, CampCheats.StaffWalk, () => look(staff) == 1, () => look(staff) == 0, "the staff taken up");
            yield return Play(pv, CampUse.StaffPlay, CampCheats.StaffPlay, () => look(staff) == 1, () => look(staff) == 0, "the staff in a kid's hand");
            yield return Play(pv, CampUse.FlourCarry, CampCheats.FlourCarry, null, null, "");
            yield return Play(pv, CampUse.FlourFight, CampCheats.FlourFight, () => camp.FlouredNow > 0, null, "flour on a kid");
            yield return Play(pv, CampUse.BirdStraighten, CampCheats.BirdStraighten, () => ConsequencePropsView.TiltOf(bird) > 10f, () => Mathf.Abs(ConsequencePropsView.TiltOf(bird)) < 0.5f, "the bird tipped");
            yield return Play(pv, CampUse.BirdFly, CampCheats.BirdFly, null, null, "");
            yield return Play(pv, CampUse.TableMeal, CampCheats.TableMeal, null, null, "");
            yield return Play(pv, CampUse.TableUnder, CampCheats.TableUnder, null, null, "");
            yield return Play(pv, CampUse.OfferingFill, CampCheats.OfferingFill, () => look(offer) >= 2, () => camp.OfferedToday && look(offer) == 1, "the cloth lifted");
            int knocks = camp.KnockAnswers;
            yield return Play(pv, CampUse.OfferingWait, CampCheats.OfferingWaitKnock, () => camp.KnockAnswers > knocks, null, "jump and point at the Keeper's knock");
            yield return Play(pv, CampUse.CrateFish, CampCheats.CrateFish, () => look(crate) == 3, null, "the crate emptied");
            yield return Play(pv, CampUse.CrateWave, CampCheats.CrateWave, null, null, "");
            yield return Play(pv, CampUse.PegRetie, CampCheats.PegRetie, null, null, "");
            yield return Play(pv, CampUse.PegTap, CampCheats.PegTap, null, null, "");
            yield return Play(pv, CampUse.BigPot, CampCheats.BigPot, null, null, "");
            if (pv.Camp.Scenes.Count(s => s.use >= CampUse.BasketTake) > 0) yield return new WaitForSeconds(2f);
            // the work still gets done: after the uses, a meal still seats people at the fire
            CampLife.OnlyCheats = false;
            PeopleView.DebugSetHour(17.6f);
            Debug.Log("[693 uses] " + PeopleView.DebugMealNow()); // the camp's meal, called now: nobody is held by a #693 scene
            float t = Time.time; int eaters = 0;
            while (Time.time - t < 40f && eaters == 0) { yield return null; eaters = camp.EatersNow; }
            if (eaters == 0) failed.Add("no supper after the uses (nobody sat down to eat)");
            Debug.Log("[693 uses] misses: " + string.Join(" | ", failed));
            CloseSlice();
            Assert.IsEmpty(failed, string.Join(" | ", failed));
        }
    }

    /// <summary>#693 (D rules, as CampTreeWorkShare): 10 view days of a lived-in camp with every choice's thing standing, at x16, natural uses
    /// on: under 5 % of each work's minutes go to a #693 use, and no meal or sleep is lost.</summary>
    [Category("CampPropWork")]
    public class CampPropWorkShare : PlayTestBase
    {
        static bool WorkKind(ActivityKind k) => k >= ActivityKind.Gather && k <= ActivityKind.Prepare
            || k is ActivityKind.SweepStep or ActivityKind.HangWashing or ActivityKind.ChopKindling or ActivityKind.MindBabies or ActivityKind.Carry or ActivityKind.FetchWater
                or ActivityKind.RollBarrel or ActivityKind.FeedFire or ActivityKind.ScrapeHide or ActivityKind.HangFish or ActivityKind.TakeDownFish or ActivityKind.MoveRack
                or ActivityKind.TakeInWashing;

        [UnityTest, Timeout(900000)]
        public IEnumerator PropUsesTakeFreeTimeOnly_WorkWithin5PercentOver10ViewDays()
        {
            UseLivingLand(true);
            GameUiPanel.AcceptInputUnfocused(true);
            yield return OpenSlice(750, 1334, seed: 4242);
            Debug.Log("[693 work] " + CampCheats.StageCamp(8f, 3, onlyCheats: false));
            Debug.Log("[693 work] " + CampCheats.EveryThing());
            yield return Frames(10);
            var pv = PeopleView.Find();
            pv.timeScale = 16f;
            var work = new System.Collections.Generic.Dictionary<ActivityKind, float>(); var taken = new System.Collections.Generic.Dictionary<ActivityKind, float>();
            float eat = 0f, eatTaken = 0f, sleep = 0f, sleepTaken = 0f, propReal = 0f;
            int days = 0; float hourWas = pv.Now.hour;
            float t0 = Time.realtimeSinceStartup;
            while (days < 10 && Time.realtimeSinceStartup - t0 < 780f)
            {
                yield return null;
                float dt = Time.deltaTime * pv.timeScale, real = Time.deltaTime; // a day's plan in view time, a scene in real seconds (CampTreeWorkShare)
                if (pv.Now.hour < hourWas - 12f) days++;
                hourWas = pv.Now.hour;
                foreach (var a in pv.Activities)
                {
                    bool cued = pv.Camp.PropCued(a.who.Key);
                    if (cued) propReal += real;
                    if (WorkKind(a.kind)) { work[a.kind] = (work.TryGetValue(a.kind, out var w) ? w : 0f) + dt; if (cued) taken[a.kind] = (taken.TryGetValue(a.kind, out var x) ? x : 0f) + real; }
                    else if (a.kind == ActivityKind.Eat) { eat += dt; if (cued) eatTaken += real; }
                    else if (a.kind == ActivityKind.Sleep) { sleep += dt; if (cued) sleepTaken += real; }
                }
            }
            pv.timeScale = 1f;
            var props = string.Join(", ", pv.Camp.Started.Where(kv => kv.Key >= CampUse.BasketTake).Select(kv => $"{kv.Key} x{kv.Value}"));
            Debug.Log($"[693 work] {days} view days; #693 uses started: {props}; {propReal:0} person-seconds in them");
            float allWork = work.Values.Sum(), allTaken = taken.Values.Sum();
            foreach (var kv in work.OrderByDescending(k => k.Value))
                Debug.Log($"[693 work] {kv.Key}: {kv.Value:0} s at work, {(taken.TryGetValue(kv.Key, out var tk) ? tk : 0f):0.0} s of it in a #693 use");
            Debug.Log($"[693 work] all work {allWork:0} s, in a #693 use {allTaken:0.0} s ({(allWork > 0 ? allTaken / allWork * 100f : 0f):0.00} %); meals {eat:0} s ({eatTaken:0.0} in a use), sleep {sleep:0} s ({sleepTaken:0.0})");
            Assert.GreaterOrEqual(days, 10, "ran 10 view days");
            Assert.Greater(allWork, 0f, "somebody worked");
            Assert.Less(allTaken / allWork, 0.05f, "the work still gets done: under 5 % of work minutes in a #693 use");
            foreach (var kv in work) if (kv.Value > 60f) Assert.Less((taken.TryGetValue(kv.Key, out var tk2) ? tk2 : 0f) / kv.Value, 0.05f, kv.Key + ": within 5 %");
            Assert.Less(eat > 0f ? eatTaken / eat : 0f, 0.05f, "no meal lost");
            Assert.Less(sleep > 0f ? sleepTaken / sleep : 0f, 0.05f, "no sleep lost");
            Assert.IsNotEmpty(props, "the #693 uses ran by themselves");
            CloseSlice();
        }
    }

    /// <summary>#693 phone shots (1179x2556, Explicit): a fresh box per use, only that use's thing standing, one shot zoomed onto the use at its
    /// hit (CampLife.UntilA) and one at the default camera. They land in Temp/agent/693/.</summary>
    [Explicit, Category("Camp693Shots")]
    public class Camp693Shots : PlayTestBase
    {
        IEnumerator Use(string name, string kind, Func<string> cheat, float lead = 0.2f)
        {
            UseLivingLand(true);
            GameUiPanel.AcceptInputUnfocused(true);
            yield return OpenSlice(1179, 2556, seed: 4242);
            GameUiPanel.AcceptInputUnfocused(true);
            Debug.Log("[693 shots] " + CampCheats.StageCamp(11f, 4));
            yield return new WaitForSeconds(2f);
            if (kind == ConsequenceKind.FishCrate) { Debug.Log("[693 shots] " + CampCheats.PondReady()); yield return new WaitForSeconds(2f); }
            Debug.Log("[693 shots] " + CampCheats.OnlyThing(kind));
            yield return new WaitForSeconds(3f);
            PeopleView.DebugSetHour(11f);
            string r = cheat();
            Debug.Log($"[693 shots] {name}: {r}");
            var pv = PeopleView.Find();
            var rig = Camera.main.GetComponent<BoxCamera>();
            var box = UnityEngine.Object.FindAnyObjectByType<BoxView>();
            float t0 = Time.time;
            while (Time.time - t0 < 30f && !(pv.Camp.UntilA is float u && u <= lead)) yield return null;
            if (pv.Camp.FocusNow() is Vector2 f)
            {
                Vector3 tgt0 = rig.target; float z0 = rig.zoom, p0 = rig.pitch, m0 = rig.minZoom;
                var w = box.Root.TransformPoint(new Vector3(f.x, box.SurfaceHeight(f.x, f.y) + 0.06f, f.y));
                for (int i = 0; i < 14; i++) { rig.CancelFlight(); rig.minZoom = 0.05f; rig.target = w; rig.zoom = 0.1f; rig.pitch = 38f; rig.Apply(); yield return null; }
                yield return Shot($"693/{name}_zoom_hit");
                for (int i = 0; i < 4; i++) { rig.CancelFlight(); rig.target = tgt0; rig.zoom = z0; rig.pitch = p0; rig.minZoom = m0; rig.Apply(); yield return null; }
            }
            yield return Shot($"693/{name}_default_hit");
            Assert.Greater(pv.Camp.Started.Count, 0, name + ": a use played (" + r + ")");
            CloseSlice();
        }

        [UnityTest, Timeout(300000)] public IEnumerator B54a() => Use("b54a-basket-take", ConsequenceKind.Baskets, CampCheats.BasketTake);
        [UnityTest, Timeout(300000)] public IEnumerator B54b() => Use("b54b-basket-tower", ConsequenceKind.Baskets, CampCheats.BasketTower, -0.5f);
        [UnityTest, Timeout(300000)] public IEnumerator B54c() => Use("b54c-wood-stack", ConsequenceKind.WoodpileRoof, CampCheats.WoodStack);
        [UnityTest, Timeout(300000)] public IEnumerator B54d() => Use("b54d-wood-shelter", ConsequenceKind.WoodpileRoof, CampCheats.WoodShelter, -2f);
        [UnityTest, Timeout(300000)] public IEnumerator B54e() => Use("b54e-name-cut", ConsequenceKind.NameStone, CampCheats.NameCut, -2f);
        [UnityTest, Timeout(300000)] public IEnumerator B54f() => Use("b54f-name-trace", ConsequenceKind.NameStone, CampCheats.NameTrace, -0.4f);
        [UnityTest, Timeout(300000)] public IEnumerator B54g() => Use("b54g-charm-flick", ConsequenceKind.CharmPole, CampCheats.CharmFlick, 0f);
        [UnityTest, Timeout(300000)] public IEnumerator B54h() => Use("b54h-big-pot", ConsequenceKind.StewPot, CampCheats.BigPot, -1f);
        [UnityTest, Timeout(300000)] public IEnumerator B54i() => Use("b54i-pot-drum", ConsequenceKind.StewPot, CampCheats.PotDrum, -1f);
        [UnityTest, Timeout(300000)] public IEnumerator B54j() => Use("b54j-stump-split", ConsequenceKind.AxeStump, CampCheats.StumpSplit, -1f);
        [UnityTest, Timeout(300000)] public IEnumerator B54k() => Use("b54k-stump-sit", ConsequenceKind.AxeStump, CampCheats.StumpSit, -1.5f);
        [UnityTest, Timeout(300000)] public IEnumerator B54l() => Use("b54l-staff-walk", ConsequenceKind.Staff, CampCheats.StaffWalk, -3f);
        [UnityTest, Timeout(300000)] public IEnumerator B54m() => Use("b54m-staff-play", ConsequenceKind.Staff, CampCheats.StaffPlay, -0.6f);
        [UnityTest, Timeout(300000)] public IEnumerator B54n() => Use("b54n-flour-carry", ConsequenceKind.FlourSacks, CampCheats.FlourCarry, 3f);
        [UnityTest, Timeout(300000)] public IEnumerator B54o() => Use("b54o-flour-fight", ConsequenceKind.FlourSacks, CampCheats.FlourFight, -0.1f);
        [UnityTest, Timeout(300000)] public IEnumerator B54p() => Use("b54p-bird-straighten", ConsequenceKind.RoofBird, CampCheats.BirdStraighten, 0.6f);
        [UnityTest, Timeout(300000)] public IEnumerator B54q() => Use("b54q-bird-fly", ConsequenceKind.RoofBird, CampCheats.BirdFly, -1f);
        [UnityTest, Timeout(300000)] public IEnumerator B54r() => Use("b54r-table-meal", ConsequenceKind.LongTable, CampCheats.TableMeal, -2f);
        [UnityTest, Timeout(300000)] public IEnumerator B54s() => Use("b54s-table-under", ConsequenceKind.LongTable, CampCheats.TableUnder, 1.2f);
        [UnityTest, Timeout(300000)] public IEnumerator B54t() => Use("b54t-offering-fill", ConsequenceKind.OfferingBasket, CampCheats.OfferingFill, 0.6f);
        [UnityTest, Timeout(300000)] public IEnumerator B54u() => Use("b54u-offering-wait", ConsequenceKind.OfferingBasket, CampCheats.OfferingWait, -0.4f);
        [UnityTest, Timeout(300000)] public IEnumerator B54x() => Use("b54x-crate-fish", ConsequenceKind.FishCrate, CampCheats.CrateFish, -1f);
        [UnityTest, Timeout(300000)] public IEnumerator B54y() => Use("b54y-crate-wave", ConsequenceKind.FishCrate, CampCheats.CrateWave, -0.5f);
        [UnityTest, Timeout(300000)] public IEnumerator B54z() => Use("b54z-peg-retie", ConsequenceKind.NamePeg, CampCheats.PegRetie, 0.3f);
        [UnityTest, Timeout(300000)] public IEnumerator B54zz() => Use("b54zz-peg-tap", ConsequenceKind.NamePeg, CampCheats.PegTap, 0f);
    }
}
