using System.Collections.Generic;
using System.Linq;
using AquariumCiv.Life;
using AquariumCiv.Sim;
using NUnit.Framework;
using UnityEngine;

namespace AquariumCiv.View.Tests
{
    /// <summary>People visibly doing things (#30): the doll poses, the props, the role accent, and the planner driving the
    /// dolls in a live box (EditMode).</summary>
    public class PeopleViewTests
    {
        static IEnumerable<ActivityKind> Kinds => System.Enum.GetValues(typeof(ActivityKind)).Cast<ActivityKind>();

        [Test]
        public void EveryActivity_HasAFinitePose_AtAnyTime()
        {
            foreach (var k in Kinds)
            {
                var style = DollMotion.StyleOf(k);
                for (float t = 0f; t < 6f; t += 0.37f)
                {
                    var p = DollMotion.Evaluate(style, t, 1f, 0, ActivityInfo.LoopSteps(k), PropSlot.Hand, true);
                    Assert.IsFalse(float.IsNaN(p.lean + p.roll + p.yaw + p.squash + p.offset.x + p.offset.y + p.offset.z), $"{k} at {t}");
                    Assert.IsFalse(float.IsNaN(p.armL.x + p.armL.y + p.armL.z + p.armR.x + p.armR.y + p.armR.z), $"{k} arms at {t}");
                    Assert.That(p.squash, Is.InRange(0.6f, 1.01f), $"{k}");
                    Assert.Less(Mathf.Abs(p.offset.y), 0.12f, $"{k} stays near the ground");
                }
            }
        }

        [Test]
        public void TheIssuesMoves_ReadAsDifferentRhythms()
        {
            Assert.AreEqual(DollStyle.Chop, DollMotion.StyleOf(ActivityKind.Chop));
            Assert.AreEqual(DollStyle.Hoe, DollMotion.StyleOf(ActivityKind.Hoe));
            Assert.AreEqual(DollStyle.Fish, DollMotion.StyleOf(ActivityKind.Fish));
            Assert.AreEqual(DollStyle.Hammer, DollMotion.StyleOf(ActivityKind.Hammer));
            Assert.AreEqual(DollStyle.Stir, DollMotion.StyleOf(ActivityKind.Cook));
            Assert.AreEqual(DollStyle.Speech, DollMotion.StyleOf(ActivityKind.Speech));
            Assert.AreEqual(DollStyle.Argue, DollMotion.StyleOf(ActivityKind.Argue));
            Assert.AreEqual(DollStyle.Hidden, DollMotion.StyleOf(ActivityKind.Sleep));

            // A chop: the arm sweeps over the head and down, with a hit (the dip) once a stroke.
            float min = float.MaxValue, max = float.MinValue; int hits = 0;
            for (float t = 0f; t < 1.15f; t += 0.01f)
            {
                var p = DollMotion.Evaluate(DollStyle.Chop, t, 1f, 0, 2, PropSlot.Hand, true);
                min = Mathf.Min(min, p.armR.x); max = Mathf.Max(max, p.armR.x);
                if (p.hit > 0f) hits++;
                Assert.IsTrue(p.extend, "the axe sticks out along the arm");
            }
            Assert.Greater(max - min, 100f, "a big overhead arc");
            Assert.Greater(hits, 0);

            // A hammer strikes faster than an axe; a slower pace (the sim's penalty) slows the stroke.
            Assert.Less(Period(DollStyle.Hammer, 1f), Period(DollStyle.Chop, 1f));
            Assert.Greater(Period(DollStyle.Chop, 0.75f), Period(DollStyle.Chop, 1f));

            // Picking crouches; walking bobs; talking turns alternate between the two people.
            Assert.Less(DollMotion.Evaluate(DollStyle.Pick, 0.3f, 1f, 0, 4, PropSlot.Hand, false).squash, 0.9f);
            Assert.AreEqual(1f, DollMotion.Evaluate(DollStyle.Pick, 0.3f, 1f, 3, 4, PropSlot.Hand, false).squash, "the loop's last step straightens up");
            Assert.Greater(Enumerable.Range(0, 40).Max(i => DollMotion.Evaluate(DollStyle.Walk, i * 0.05f, 1f, 0, 1, PropSlot.Hand, false).offset.y), 0.005f);
            var talk = DollMotion.Evaluate(DollStyle.Talk, 0.3f, 1f, 0, 2, PropSlot.Hand, false);
            var listen = DollMotion.Evaluate(DollStyle.Talk, 0.3f, 1f, 1, 2, PropSlot.Hand, false);
            Assert.Greater(talk.offset.y, listen.offset.y);
        }

        static float Period(DollStyle s, float pace)
        {
            // Time between the starts of two hits.
            bool was = false; float first = -1f;
            for (float t = 0f; t < 6f; t += 0.002f)
            {
                bool h = DollMotion.Evaluate(s, t, pace, 0, 2, PropSlot.Hand, true).hit > 0f;
                if (h && !was) { if (first < 0f) first = t; else return t - first; }
                was = h;
            }
            return float.MaxValue;
        }

        [Test]
        public void CarryPoses_HoldTheLoad()
        {
            var head = DollMotion.Evaluate(DollStyle.Walk, 0.2f, 1f, 0, 1, PropSlot.Head, true);
            Assert.Greater(head.armR.z, 120f, "a hand up to the load on the head");
            var arms = DollMotion.Evaluate(DollStyle.Walk, 0.2f, 1f, 0, 1, PropSlot.Arms, true);
            Assert.Greater(arms.armL.x, 40f); Assert.Greater(arms.armR.x, 40f);
            var free = DollMotion.Evaluate(DollStyle.Walk, 0.2f, 1f, 0, 1, PropSlot.Hand, false);
            Assert.Less(free.armR.z, 20f, "empty-handed arms swing at the sides");
        }

        [Test]
        public void EveryPlannerProp_MapsToAViewProp_InASlotItFits()
        {
            foreach (Life.Prop p in System.Enum.GetValues(typeof(Life.Prop)))
            {
                var (kind, slot) = PeopleView.PropOf(p, ActivityInfo.SlotOf(p, ActivityKind.Carry), ActivityKind.Carry);
                if (p == Life.Prop.None) Assert.AreEqual(PropKind.None, kind);
                if (kind == PropKind.Bundle) Assert.That(slot, Is.EqualTo(PropSlot.Back).Or.EqualTo(PropSlot.Shoulder));
                if (kind == PropKind.Barrel) Assert.AreEqual(PropSlot.Front, slot);
            }
            Assert.AreEqual((PropKind.Axe, PropSlot.Hand), PeopleView.PropOf(Life.Prop.Axe, Life.PropSlot.Hand, ActivityKind.Chop));
            Assert.AreEqual((PropKind.Rod, PropSlot.Hand), PeopleView.PropOf(Life.Prop.Rod, Life.PropSlot.Hand, ActivityKind.Fish));
            Assert.AreEqual((PropKind.Hammer, PropSlot.Hand), PeopleView.PropOf(Life.Prop.Hammer, Life.PropSlot.Hand, ActivityKind.Hammer));
            Assert.AreEqual((PropKind.Log, PropSlot.Shoulder), PeopleView.PropOf(Life.Prop.Log, Life.PropSlot.Shoulder, ActivityKind.Carry));
            Assert.AreEqual((PropKind.Sheaf, PropSlot.Head), PeopleView.PropOf(Life.Prop.Sheaf, Life.PropSlot.Head, ActivityKind.Carry));
            Assert.AreEqual((PropKind.Bundle, PropSlot.Back), PeopleView.PropOf(Life.Prop.Goods, Life.PropSlot.Back, ActivityKind.Carry));
        }

        [Test]
        public void RoleHolders_WearAWideSashAndHeadband_InTheRolesColour()
        {
            Assert.AreEqual(Palette.RoleAccent(RoleType.Chief), Palette.RoleAccent(RoleType.Headman), "the leader keeps one colour across eras");
            Assert.AreEqual(Palette.RoleAccent(RoleType.Chief), Palette.RoleAccent(RoleType.Lord));
            Assert.AreNotEqual(Palette.RoleAccent(RoleType.Miller), Palette.RoleAccent(RoleType.Priest));

            int Verts(PegDoll.Look l) { var mb = new MeshBuilder(1); PegDoll.Body(mb, l, 1f); return mb.VertexCount; }
            // Vertex colours are linear and brightness-jittered per face: match the hue, not the exact value.
            int AccentVerts(PegDoll.Look l, Color c)
            {
                var mb = new MeshBuilder(1); PegDoll.Body(mb, l, 1f);
                var lin = c.linear;
                return mb.colors.Count(x =>
                {
                    float k = x.maxColorComponent / Mathf.Max(1e-4f, lin.maxColorComponent);
                    return k > 0.85f && k < 1.15f && Mathf.Abs(x.r / k - lin.r) + Mathf.Abs(x.g / k - lin.g) + Mathf.Abs(x.b / k - lin.b) < 0.03f;
                });
            }
            foreach (bool camp in new[] { true, false })
            {
                var plain = PegDoll.PickLook(5, camp, false, -1);
                plain.hairStyle = 0;
                var notable = plain; notable.accent = Palette.Accents[1];
                var holder = plain; holder.accent = Palette.RoleAccent(RoleType.Chief); holder.role = true;
                Assert.Greater(Verts(holder), Verts(plain), $"camp {camp}: the holder wears more than the plain");
                Assert.Greater(AccentVerts(holder, holder.accent), AccentVerts(notable, notable.accent), $"camp {camp}: more of the role colour than a notable's accent");
            }
        }

        // ---------------- the live box ----------------
        static GameObject NewBox(int seed, float years, out BoxView box, out PeopleView people)
        {
            var go = new GameObject("TestPeopleBox");
            go.SetActive(false);
            box = go.AddComponent<BoxView>();
            box.bakeAO = false;
            box.logRebuilds = false;
            var src = go.AddComponent<SimLayoutSource>();
            src.previewSeed = seed;
            src.previewYears = years;
            people = go.AddComponent<PeopleView>();
            go.SetActive(true);
            people.Tick(0f);
            return go;
        }

        [Test]
        public void ThePlanner_DrivesOneDollPerPerson_AndTheLayoutsStandersAreGone()
        {
            var go = NewBox(20261029, 30f, out var box, out var people);
            try
            {
                var sim = go.GetComponent<SimLayoutSource>().Sim;
                Assert.AreEqual(0, box.Dolls.Count, "the layout's standing people are dropped");
                int expected = Mathf.Min(people.Planner.People.Count, people.maxPeople);
                Assert.AreEqual(expected, people.Activities.Count);
                var dolls = go.GetComponentsInChildren<PegDollView>(true);
                Assert.AreEqual(expected, dolls.Length, "one doll per person");

                // Role holders wear their role's colour.
                foreach (var seat in sim.State.roles.Where(r => r.holderId > 0))
                {
                    var n = sim.FindNotable(seat.holderId);
                    if (n == null || !n.alive || n.age < 16f) continue;
                    var doll = dolls.FirstOrDefault(d => d.name.StartsWith(n.FullName));
                    Assert.IsNotNull(doll, $"{seat.role} {n.FullName} is in the box");
                    Assert.IsTrue(doll.look.role, $"{seat.role} wears a sash");
                    Assert.AreEqual(Palette.RoleAccent(seat.role), doll.look.accent);
                }
                Assert.IsTrue(dolls.Any(d => d.look.role), "30 years in, someone holds a role");

                // Turning the planner off brings the stand-ins back.
                people.enabled = false;
                Assert.Greater(box.Dolls.Count, 0);
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void ADay_DollsWalkWithoutJumping_StayOnLandInTheGlass_AndAreBusyAtWork()
        {
            var go = NewBox(7, 30f, out var box, out var people);
            try
            {
                var land = box.land;
                var settle = go.GetComponent<SimLayoutSource>().Sim.Settlement;
                people.timeScale = 1f; // the real day: walks at the speed a player sees
                PeopleView.DebugSetHour(6.5f);
                var last = new Dictionary<PegDollView, Vector3>();
                int jumps = 0, nightJumps = 0, onLot = 0, stacked = 0, frames = 0, busySamples = 0, busyAdults = 0, adultSamples = 0, wet = 0;
                var styles = new HashSet<string>();
                var jumpLog = new List<string>();
                var lotLog = new List<string>();
                while (people.Now.hour < 22.5f && frames < 4000)
                {
                    people.Tick(0.1f);
                    frames++;
                    foreach (var d in go.GetComponentsInChildren<PegDollView>(false))
                    {
                        var p = d.transform.localPosition;
                        if (last.TryGetValue(d, out var q) && Vector2.Distance(new Vector2(p.x, p.z), new Vector2(q.x, q.z)) > 0.25f)
                        {
                            // Also after nightfall, when view hours run ~3x faster: the planner paces night walks (#30).
                            jumps++;
                            if (LifeClock.PhaseOf(people.Now.hour) == DayPhase.Night) nightJumps++;
                            if (jumpLog.Count < 12) jumpLog.Add($"{people.Now} {d.name}: ({q.x:0.00},{q.z:0.00}) -> ({p.x:0.00},{p.z:0.00}) · " +
                                (people.TryGetActivity(d, out var act) ? people.Describe(act) : "?"));
                        }
                        last[d] = p;
                        Assert.LessOrEqual(Mathf.Abs(p.x), Land.HX - 0.1f, d.name);
                        Assert.LessOrEqual(Mathf.Abs(p.z), Land.HZ - 0.1f, d.name);
                    }
                    foreach (var a in people.Activities)
                    {
                        styles.Add(DollMotion.StyleOf(a.kind).ToString());
                        if (!people.TryGetDollPosition(a.who, out var pos)) continue;
                        if (a.kind != ActivityKind.Swim && a.kind != ActivityKind.Splash && land.IsWater(pos.x, pos.y)) wet++;
                        int cell = Sim.Settle.SiteMap.CellAt(pos.x, pos.y);
                        if (!a.moving && !people.IsWalking(a.who) && a.place.kind != PlaceKind.Field && a.place.kind != PlaceKind.Pasture && a.kind != ActivityKind.Sleep &&
                            people.Planner.StageOf(a.who) != LifeStage.Baby && cell >= 0 && settle.WalkCost(cell) == 0)
                        {
                            onLot++;
                            if (lotLog.Count < 10 && frames % 10 == 0) lotLog.Add($"{people.Now} {people.Describe(a)}");
                        }
                    }
                    // Rule 6: one doll per spot. Settled, visible dolls never stand on top of each other.
                    if (frames % 10 == 0)
                    {
                        var still = people.Activities.Where(a => !a.moving && !people.IsWalking(a.who) && a.kind != ActivityKind.Sleep &&
                            people.Planner.StageOf(a.who) != LifeStage.Baby && people.TryGetDollPosition(a.who, out _))
                            .Select(a => { people.TryGetDollPosition(a.who, out var q); return q; }).ToList();
                        for (int i = 0; i < still.Count; i++)
                            for (int j = i + 1; j < still.Count; j++)
                                if ((still[i] - still[j]).sqrMagnitude < 0.05f * 0.05f) stacked++;
                    }
                    if (frames % 20 == 0 && LifeClock.IsWorkHour(people.Now.hour) && !people.Planner.IsRestDay(people.Now.day))
                    {
                        foreach (var a in people.Activities)
                        {
                            // A story scene (this month's festival, a wedding) takes people off work on purpose (rule 10).
                            if (people.Planner.StageOf(a.who) != LifeStage.Adult || a.source == ActivitySource.Scene) continue;
                            adultSamples++;
                            if (a.IsBusy) busyAdults++;
                        }
                        busySamples++;
                    }
                }
                Assert.Less(frames, 4000, "the clock ran through the day");
                Assert.AreEqual(0, jumps, "no doll teleports (> 0.25 u in a 100 ms frame):\n" + string.Join("\n", jumpLog));
                Assert.AreEqual(0, wet, "nobody stands in the pond unless swimming");
                Assert.AreEqual(0, stacked, "no two dolls stand on the same spot");
                Assert.AreEqual(0, onLot, "nobody stands inside a building's lot (a ring round the market runs round the houses):\n" + string.Join("\n", lotLog));
                TestContext.WriteLine($"styles seen: {string.Join(", ", styles)}; busy {busyAdults}/{adultSamples}; night rushes {nightJumps}; {people.Status()}");
                Assert.GreaterOrEqual(styles.Count, 8, "a day shows many different moves");
                if (adultSamples > 0) Assert.GreaterOrEqual(busyAdults / (float)adultSamples, 0.6f, "work hours: most adults are busy");
                Assert.Less(people.FrameMs, 5.0, "the people's frame work stays small (Editor, generous; ≤ 0.5 ms is the phone target)");
            }
            finally { Object.DestroyImmediate(go); }
        }

        /// <summary>#30: the sim's month ticks in the middle of a view day. The view keeps its day till midnight, and a plan
        /// the month did change (a heap swept away, a new home) is walked to, not jumped to.</summary>
        [Test]
        public void AMonthTick_MidDay_NoDollJumps()
        {
            var go = NewBox(3, 90f, out var box, out var people);
            try
            {
                var sim = go.GetComponent<SimLayoutSource>().Sim;
                people.timeScale = 1f;
                var jumpLog = new List<string>();
                int ticks = 0;
                foreach (float hour in new[] { 6.5f, 9.3f, 13.6f, 17.4f })
                {
                    PeopleView.DebugSetHour(hour);
                    for (int f = 0; f < 20; f++) people.Tick(0.1f); // settle into slots
                    long day = people.Now.day;
                    var last = new Dictionary<PegDollView, Vector3>();
                    foreach (var d in go.GetComponentsInChildren<PegDollView>(false)) last[d] = d.transform.localPosition;
                    sim.Step();
                    ticks++;
                    for (int f = 0; f < 30; f++)
                    {
                        people.Tick(0.1f);
                        Assert.AreEqual(day, people.Now.day, "the view keeps its day till midnight");
                        foreach (var d in go.GetComponentsInChildren<PegDollView>(false))
                        {
                            var p = d.transform.localPosition;
                            if (last.TryGetValue(d, out var q) && Vector2.Distance(new Vector2(p.x, p.z), new Vector2(q.x, q.z)) > 0.25f && jumpLog.Count < 12)
                                jumpLog.Add($"{people.Now} {d.name}: ({q.x:0.00},{q.z:0.00}) -> ({p.x:0.00},{p.z:0.00}) · " +
                                    (people.TryGetActivity(d, out var act) ? people.Describe(act) : "?"));
                            last[d] = p;
                        }
                    }
                }
                Assert.AreEqual(4, ticks);
                Assert.AreEqual(0, jumpLog.Count, "no doll jumps (> 0.25 u in a 100 ms frame) at a month tick:\n" + string.Join("\n", jumpLog));
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void DebugReport_SaysWhatPeopleAreDoing()
        {
            var go = NewBox(20261029, 12f, out _, out _);
            try
            {
                PeopleView.DebugSetHour(10f);
                var report = PeopleView.DebugReport(5);
                var lines = report.Split('\n');
                Assert.AreEqual(6, lines.Length, report);
                foreach (var l in lines.Skip(1)) StringAssert.Contains(" at ", l.Replace(" to ", " at "));
            }
            finally { Object.DestroyImmediate(go); }
        }
    }
}
