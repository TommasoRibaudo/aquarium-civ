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
using UnityEngine.UIElements;

namespace AquariumCiv.View.PlayTests
{
    /// <summary>P8 polish item 18, the old slice (living land off): #208 the dawn lane at 06:30 with staggered chores, and #209 the
    /// leader lines on a person card going round the label stack. (#210 #211, the crumb bits, are in <see cref="CrumbPlayTests"/>.)</summary>
    [Category("OldPolish")]
    public class OldPolishPlayTests : PlayTestBase
    {
        const int Seed = 1234;
        static SimCore Sim => GameClock.Instance.ActiveSim;
        static PeopleView People => PeopleView.Find();

        IEnumerator Village(float years = 14f, int seed = Seed)
        {
            yield return OpenSlice(seed: seed);
            ShowMeDirector.BubblesOn = false;
            MomentPlayer.DebugGrowVillage(years);
            yield return Frames(3);
            Sim.CleanAll();
            PeopleView.DebugSetHour(10f);
            yield return new WaitForSecondsRealtime(0.5f);
            yield return Until(() => GameUiPanel.Toasts.Current == null && GameUiPanel.Toasts.Waiting == 0, 6f);
        }

        IEnumerator Town(int seed = Seed)
        {
            yield return Village(14f, seed);
            MomentPlayer.DebugGrowVillage(30f);
            yield return Frames(3);
            Sim.ForceNextEra();
            yield return Frames(3);
            SimLayoutSource.DebugSkipYears(10f);
            yield return Frames(6);
            Sim.CleanAll();
            yield return Until(() => GameUiPanel.Toasts.Current == null && GameUiPanel.Toasts.Waiting == 0, 6f);
        }

        // ---------------- #208: the dawn lane ----------------

        /// <summary>Near enough to a door to count as standing at it, and the reach of a knot in the lane (box units).</summary>
        const float AtDoor = 0.2f, KnotReach = 0.25f;
        /// <summary>The worst knot by the homes at 05:54-07:36, measured in the Editor on this test with the chores as they were before #208
        /// (LifePlanner.Day.cs at 1802801^, the same seeds and samples).</summary>
        const int KnotBeforeVillage = 0, KnotBeforeTown = 0;

        /// <summary>Everyone out of doors by the homes now: the biggest bunch within <see cref="KnotReach"/> of one of them, and the most standing at one door.</summary>
        static (int knot, int door) CountLane(PeopleView pv)
        {
            for (int f = 0; f < 75; f++) pv.Tick(1f / 30f);
            var was = new Dictionary<int, Vector2>();
            foreach (var a in pv.Activities) if (pv.TryGetDollPosition(a.who, out var w)) was[a.who.Key] = w;
            pv.Tick(1f / 30f);
            var lane = new List<Vector2>();
            var doors = new Dictionary<Place, int>();
            foreach (var a in pv.Activities)
            {
                if (!pv.TryGetDollTop(a.who, out _) || !pv.TryGetDollPosition(a.who, out var p)) continue; // indoors: no doll
                bool byHome = a.place.kind == PlaceKind.Home;
                if (byHome || a.moving) lane.Add(p);
                if (!byHome || a.moving) continue;
                bool standing = was.TryGetValue(a.who.Key, out var then) && (p - then).magnitude < 0.0005f;
                if (standing && pv.Planner.Positions.TryGetPosition(a.place, out float dx, out float dz) && (p - new Vector2(dx, dz)).magnitude < AtDoor)
                {
                    doors.TryGetValue(a.place, out int n);
                    doors[a.place] = n + 1;
                }
            }
            int knot = 0;
            foreach (var p in lane) knot = Mathf.Max(knot, lane.Count(o => (o - p).magnitude < KnotReach));
            return (knot, doors.Count == 0 ? 0 : doors.Values.Max());
        }

        [UnityTest, Timeout(240000)]
        public IEnumerator TheDawnLaneIsLooserAndTwoStillHoldTheDoor([Values(false, true)] bool town)
        {
            yield return town ? Town() : Village(30f);
            var pv = People;
            pv.HandDriven = true;
            int knot = 0, door = 0, samples = 0;
            float knotSum = 0f;
            string worstAt = "";
            try
            {
                for (int day = 0; day < 3; day++)
                {
                    int dayWorst = 0;
                    for (float h = 5.9f; h <= 7.65f; h += 0.1f)
                    {
                        pv.SetHour(h);
                        var c = CountLane(pv);
                        samples++;
                        if (c.knot > knot) { knot = c.knot; worstAt = $"day {day} {h:0.0}h"; }
                        dayWorst = Mathf.Max(dayWorst, c.knot);
                        door = Mathf.Max(door, c.door);
                    }
                    knotSum += dayWorst;
                    SimLayoutSource.DebugSkipYears(0.09f);
                    pv.Tick(1f / 30f);
                }
                string say = $"[208] {(town ? "Town" : "Village")} dawn: {samples} samples, worst knot by the homes {knot} ({worstAt}), mean of each day's worst {knotSum / 3f:0.0}, most at one door {door}";
                Debug.Log(say);
                pv.SetHour(6.5f);
                for (int f = 0; f < 90; f++) pv.Tick(1f / 30f);
                pv.HandDriven = false;
                yield return Frames(2);
                yield return Shot($"208-dawn-{(town ? "town" : "village")}-0630");
                Assert.LessOrEqual(door, 2, "the door cap of 2 holds: " + say);
                int before = town ? KnotBeforeTown : KnotBeforeVillage;
                if (before > 0) Assert.Less(knot, before, "a smaller worst knot than before the stagger: " + say);
            }
            finally { pv.HandDriven = false; }
            AssertNoErrors("dawn lane");
        }

        // ---------------- #209: leaders round the label stack ----------------

        [UnityTest, Timeout(120000)]
        public IEnumerator NoLeaderCrossesTheLabelStack()
        {
            yield return OpenSlice(seed: 7531);
            ShowMeDirector.BubblesOn = false;
            MomentPlayer.DebugGrowVillage(30f);
            yield return Frames(3);
            SimLayoutSource.DebugSkipYears(25f);
            yield return Frames(4);
            Sim.CleanAll();
            PeopleView.DebugSetHour(19.5f);
            GlassVoice.DebugGather(10);
            yield return new WaitForSecondsRealtime(3f);
            var cards = PersonCardView.Instance;
            // Alwin first if this village has him (the user's card), then the most-tied people: the cards with the biggest stacks.
            var who = Sim.State.notables.Where(x => x.alive).OrderByDescending(x => x.name == "Alwin").ThenByDescending(x => Sim.RelationshipGraph(true, x.id).Count).Take(6).ToList();
            int leadersSeen = 0, crossed = 0, mostLeaders = -1;
            string first = null, shotOf = null;
            foreach (var n in who)
            {
                if (!cards.Open(n.id, true)) continue;
                yield return new WaitForSecondsRealtime(1.2f);
                var root = cards.CardRoot.panel.visualTree;
                var shown = root.Query<GlassTag>().ToList().Where(t => t.name.StartsWith("pc-tie-") && t.resolvedStyle.display == DisplayStyle.Flex).Select(t => t.worldBound).ToList();
                var leaders = cards.Leaders.ToList();
                leadersSeen += leaders.Count;
                foreach (var l in leaders)
                {
                    var legs = new List<(Vector2 a, Vector2 b)>();
                    var pts = new List<Vector2> { new(l.ax, l.ay) };
                    if (l.bends) pts.Add(new Vector2(l.vx, l.vy));
                    if (l.twice) pts.Add(new Vector2(l.wx, l.wy));
                    pts.Add(new Vector2(l.bx, l.by));
                    for (int i = 0; i + 1 < pts.Count; i++) legs.Add((pts[i], pts[i + 1]));
                    var start = pts[0];
                    foreach (var r in shown)
                    {
                        bool mine = new Rect(r.x - 2f, r.y - 2f, r.width + 4f, r.height + 4f).Contains(start);
                        var box = new LabelBox(r.center.x, r.center.y, r.width, r.height);
                        for (int i = 0; i < legs.Count; i++)
                        {
                            if (mine && i == 0) continue; // the first leg leaves its own label's edge
                            if (!MarkerLayout.Crosses(legs[i].a.x, legs[i].a.y, legs[i].b.x, legs[i].b.y, box, mine ? -1f : 1f)) continue;
                            crossed++;
                            first ??= $"{n.name}: leg {i} {legs[i].a} -> {legs[i].b} crosses the label at {r}";
                        }
                    }
                }
                Debug.Log($"[209] {n.name}: {shown.Count} labels, {leaders.Count} leaders, {string.Join("; ", leaders.Select(l => l.twice ? "3 legs" : l.bends ? "2 legs" : "straight"))}");
                if (leaders.Count > mostLeaders)
                {
                    mostLeaders = leaders.Count;
                    shotOf = n.name;
                    yield return Shot("209-leaders-village");
                }
            }
            Debug.Log($"[209] {who.Count} cards, {leadersSeen} leaders, {crossed} crossings; shot of {shotOf}");
            Assert.AreEqual(0, crossed, first);
            AssertNoErrors("leaders");
        }
    }
}
