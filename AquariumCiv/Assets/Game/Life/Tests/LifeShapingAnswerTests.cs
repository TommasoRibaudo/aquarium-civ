using System;
using System.Collections.Generic;
using System.Linq;
using AquariumCiv.Life;
using AquariumCiv.Sim;
using AquariumCiv.Sim.Settle;
using NUnit.Framework;
using UnityEngine;

namespace AquariumCiv.Life.Tests
{
    /// <summary>#85: the people's answer to a shaping, staged in the world: the credited notable walks to the gift and works
    /// it, helpers and kids come, they thank the Keeper at the glass. Every place is real, every walk keeps the walking
    /// pace, and the planner never touches the sim.</summary>
    public class LifeShapingAnswerTests
    {
        static SimCore NewSim(int seed)
        {
            var cfg = new SimConfig();
            return new SimCore(cfg, SimCore.NewWorld(seed, cfg));
        }

        static SimCore SimIn(Era era, int seed)
        {
            var sim = NewSim(seed);
            for (int m = 0; m < 120 * 12 && sim.State.era < era; m++)
            {
                sim.Step();
                if (sim.State.eraReady) sim.AdvanceEra();
            }
            if (sim.State.era != era) return null;
            for (int m = 0; m < 12; m++) sim.Step();
            return sim;
        }

        static (float x, float z) OpenLand(SimCore sim, float x, float z)
        {
            var spec = sim.Settlement.Map.spec;
            foreach (var (px, pz) in new[] { (x, z), (-x, z), (x, -z), (-x, -z), (0.3f, 1.2f), (-1.4f, -0.4f) })
            {
                float ax = px, az = pz;
                if (!sim.Settlement.OpenSpotNear(ref ax, ref az, 0.35f)) continue;
                if (AquariumCiv.Sim.Settle.Terrain.Dist(ax, az, spec.pondX, spec.pondZ) < 1.1f) continue;
                return (ax, az);
            }
            return (x, z);
        }

        /// <summary>A shaping placed and answered in a box of that era (null when the seed doesn't reach it).</summary>
        static (SimCore sim, Nudge n) Answered(string kind, Era era, int seed, bool fish = false)
        {
            var sim = era == Era.Camp ? NewSim(seed) : SimIn(era, seed);
            if (sim == null) return (null, null);
            if (era == Era.Camp) for (int m = 0; m < 18; m++) sim.Step();
            var spec = sim.Settlement.Map.spec;
            var (x, z) = fish ? (spec.pondX, spec.pondZ) : OpenLand(sim, 1.2f, 0.6f);
            var n = sim.DebugShape(kind, x, z);
            Assert.IsTrue(sim.DebugSkipToAnswer(n.id), "the people answer");
            sim.Settlement.PlacePending(); // its building on its lot, as at the month's end
            return (sim, n);
        }

        static readonly (string kind, Era era, bool fish)[] Cases =
        {
            (SimCore.NudgeKind.Rain, Era.Camp, false), (SimCore.NudgeKind.Grove, Era.Camp, false), (SimCore.NudgeKind.Herd, Era.Camp, false),
            (SimCore.NudgeKind.Herd, Era.Camp, true), (SimCore.NudgeKind.Rain, Era.Village, false), (SimCore.NudgeKind.Channel, Era.Village, false),
            (SimCore.NudgeKind.Grove, Era.Town, false), (SimCore.NudgeKind.Channel, Era.Town, false),
        };

        [Test]
        public void EachToolsAnswerIsPlayedAtTheGift()
        {
            int played = 0, ownedByCredited = 0, creditedAlive = 0;
            foreach (int seed in new[] { 1, 1234 })
                foreach (var c in Cases)
                {
                    var (sim, n) = Answered(c.kind, c.era, seed, c.fish);
                    if (sim == null) continue;
                    var planner = LifePlanner.Wired(sim);
                    long day = sim.State.month;
                    while (planner.IsRestDay(day)) day++;
                    var at = new ViewTime(day, 10f);
                    string before = JsonUtility.ToJson(sim.State);
                    var info = planner.StartShapingAnswer(n.id, at);
                    string tag = $"{c.kind}{(c.fish ? " (fish)" : "")} {c.era} seed {seed}";
                    Assert.IsNotNull(info, $"{tag}: the answer plays");
                    played++;
                    Assert.AreEqual(n.id, info.nudgeId);
                    Assert.IsTrue(info.cast.Any(x => x.role == RelicCast.Owner), $"{tag}: someone works the gift");
                    var spot = planner.ShapingAnswerPlace(n);
                    Assert.AreEqual(spot, info.spot);
                    if (n.buildingId >= 0 && sim.State.buildings.First(b => b.id == n.buildingId).lotId != 0)
                        Assert.IsTrue(spot.IsBuilding && spot.buildingId == n.buildingId, $"{tag}: at the new building");
                    int credited = ShapingAnswers.CreditedNotableId(sim, n);
                    if (sim.State.notables.Any(x => x.id == credited && x.alive && x.age >= SimCore.AdultAge))
                    {
                        creditedAlive++;
                        if (info.cast.Any(x => x.role == RelicCast.Owner && x.who.notableId == credited)) ownedByCredited++;
                    }
                    string building = n.buildingId >= 0 ? sim.State.buildings.First(b => b.id == n.buildingId).type : null;
                    var work = ShapingAnswers.WorkFor(n.kind, n.variant, sim.State.era, building);
                    var acts = new List<PersonActivity>();
                    var moments = new List<RelicMomentInfo>();
                    var kinds = new HashSet<ActivityKind>();
                    bool thanked = false;
                    for (float h = at.hour; h < info.end; h += 0.01f)
                    {
                        var t = new ViewTime(day, h);
                        planner.PlanAll(t, acts);
                        foreach (var a in acts)
                        {
                            Assert.IsTrue(planner.IsValidPlace(a.place) && planner.IsValidPlace(a.from), $"{tag}: {a.who} {a.kind} at a real place");
                            if (a.source != ActivitySource.Scene) continue;
                            kinds.Add(a.kind);
                            if (a.facesGlass && (a.kind == ActivityKind.Wave || a.kind == ActivityKind.LookUp)) thanked = true;
                        }
                        Assert.AreEqual(1, planner.ActiveShapingAnswers(t, moments), $"{tag}: playing");
                    }
                    TestContext.WriteLine($"{tag}: {info.cast.Count} cast; {string.Join(", ", kinds)}");
                    Assert.IsTrue(kinds.Contains(work.work), $"{tag}: someone does {work.work}");
                    Assert.IsTrue(thanked, $"{tag}: they thank the Keeper at the glass");
                    Assert.AreEqual(0, planner.FallbackCount, tag);
                    Assert.AreEqual(0, planner.ActiveShapingAnswers(new ViewTime(day, info.end + 0.01f), moments), $"{tag}: then it's over");
                    Assert.AreEqual(before, JsonUtility.ToJson(sim.State), $"{tag}: the planner never writes the sim");
                }
            TestContext.WriteLine($"{played} answers played; the credited notable worked it in {ownedByCredited} of {creditedAlive}");
            Assert.Greater(played, 10);
            Assert.GreaterOrEqual(ownedByCredited * 2, creditedAlive, "the credited notable plays their part, mostly");
        }

        [Test]
        public void NoAnswerForAShapingNotYetAnswered()
        {
            var sim = NewSim(2);
            for (int m = 0; m < 12; m++) sim.Step();
            var n = sim.DebugShape(SimCore.NudgeKind.Rain, 1f, 0.5f);
            var planner = LifePlanner.Wired(sim);
            Assert.IsNull(planner.StartShapingAnswer(n.id, new ViewTime(sim.State.month, 10f)));
            Assert.IsNull(planner.StartShapingAnswer(-5, new ViewTime(sim.State.month, 10f)));
        }

        [Test]
        public void TheWorkFitsTheGiftAndTheEra()
        {
            Assert.AreEqual(ActivityKind.Gather, ShapingAnswers.WorkFor(SimCore.NudgeKind.Rain, "", Era.Camp, BuildingType.Farm).work, "the camp gathers the growth");
            Assert.AreEqual(ActivityKind.Sow, ShapingAnswers.WorkFor(SimCore.NudgeKind.Rain, "", Era.Village, BuildingType.Farm).work, "a village sows its new field");
            Assert.AreEqual(ActivityKind.Harvest, ShapingAnswers.WorkFor(SimCore.NudgeKind.Rain, "", Era.Town, null).work, "a full town brings in a second crop");
            Assert.AreEqual(ActivityKind.Chop, ShapingAnswers.WorkFor(SimCore.NudgeKind.Grove, "", Era.Camp, BuildingType.Lumbermill).work);
            Assert.AreEqual(ActivityKind.Stalk, ShapingAnswers.WorkFor(SimCore.NudgeKind.Herd, "deer", Era.Camp, BuildingType.Farm).work, "the first great hunt");
            Assert.AreEqual(Prop.Spear, ShapingAnswers.WorkFor(SimCore.NudgeKind.Herd, "deer", Era.Camp, BuildingType.Farm).prop);
            Assert.AreEqual(ActivityKind.Fish, ShapingAnswers.WorkFor(SimCore.NudgeKind.Herd, "fish", Era.Town, BuildingType.Workshop).work);
            Assert.AreEqual(ActivityKind.Herd, ShapingAnswers.WorkFor(SimCore.NudgeKind.Herd, "deer", Era.Village, BuildingType.Farm).work);
            Assert.AreEqual(ActivityKind.MillWork, ShapingAnswers.WorkFor(SimCore.NudgeKind.Channel, "", Era.Town, BuildingType.Workshop).work, "the watermill");
            foreach (var kind in SimCore.SliceTools)
                foreach (Era era in Enum.GetValues(typeof(Era)))
                {
                    var d = ShapingAnswers.For(kind, "deer", era, BuildingType.Farm);
                    foreach (var cast in new[] { RelicCast.Owner, RelicCast.Grown, RelicCast.Kids })
                    {
                        var beats = d.For(cast).OrderBy(b => b.from).ToList();
                        Assert.AreEqual(0f, beats[0].from, $"{kind} {era} {cast}: starts at once");
                        Assert.AreEqual(1f, beats[^1].to, $"{kind} {era} {cast}: tiles the moment");
                        for (int i = 1; i < beats.Count; i++) Assert.AreEqual(beats[i - 1].to, beats[i].from, 1e-6f, "no gaps");
                        Assert.IsFalse(beats.Any(b => ActivityInfo.IsMove(b.kind)), "the staging walks them; beats are poses");
                    }
                }
        }

        /// <summary>Every walk into, within and out of an answer moment keeps the walking pace (≤ 2x, as the relic moments).</summary>
        [Test]
        public void AnswerWalksKeepThePace()
        {
            float worst = 0f;
            string worstWhat = "";
            int walked = 0, moments = 0;
            foreach (int seed in new[] { 1, 2, 7, 1234 })
                foreach (var c in Cases)
                {
                    var (sim, n) = Answered(c.kind, c.era, seed, c.fish);
                    if (sim == null) continue;
                    var planner = LifePlanner.Wired(sim);
                    long day = sim.State.month;
                    foreach (float hour in new[] { 6f, 10f, 15f, 18.4f })
                    {
                        planner.ClearLive();
                        var info = planner.StartShapingAnswer(n.id, new ViewTime(day, hour));
                        if (info == null) continue;
                        moments++;
                        var who = planner.People.ToList();
                        var plain = new Dictionary<int, HashSet<(float, float)>>();
                        var daySegs = new List<PersonActivity>();
                        foreach (var p in who)
                        {
                            planner.DaySchedule(p, day, daySegs);
                            plain[p.Key] = new HashSet<(float, float)>(daySegs.Select(a => (a.start, a.end)));
                        }
                        var last = new Dictionary<int, (float x, float z, float h)>();
                        // Within the moment (the walks in and between beats). The step back into the plain day after a moment that ran into
                        // the night is the planner's #64/#92 tail, judged by the day sweeps (see the #85 phase 1 notes).
                        for (float h = hour; h < Math.Min(24f, info.end); h += 0.004f)
                            foreach (var p in who)
                            {
                                if (!planner.PositionAt(p, new ViewTime(day, h), out float x, out float z)) continue;
                                if (last.TryGetValue(p.Key, out var l))
                                {
                                    float d = (float)Math.Sqrt((x - l.x) * (x - l.x) + (z - l.z) * (z - l.z));
                                    float secs = LifeClock.RealSecondsBetween(l.h, h);
                                    var now = planner.Plan(p, new ViewTime(day, h));
                                    if (d > 1e-4f && secs > 0f && !plain[p.Key].Contains((now.start, now.end)))
                                    {
                                        walked++;
                                        float pace = d / secs / LifePlanner.RealWalkSpeed;
                                        if (pace > worst) { worst = pace; worstWhat = $"{c.kind} {c.era} seed {seed} {hour}h {p} at {h:0.000}h: {now.kind} {now.from}->{now.place}"; }
                                    }
                                }
                                last[p.Key] = (x, z, h);
                            }
                    }
                }
            TestContext.WriteLine($"{moments} answers, {walked} steps sampled; fastest {worst:0.00}x the walking pace ({worstWhat})");
            Assert.Greater(moments, 20);
            Assert.Greater(walked, 100);
            Assert.LessOrEqual(worst, 2f, worstWhat);
        }
    }
}
