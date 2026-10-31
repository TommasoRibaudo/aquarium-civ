using System.Collections.Generic;
using System.Linq;
using AquariumCiv.Audio;
using AquariumCiv.Life;
using AquariumCiv.Sim;
using NUnit.Framework;
using UnityEngine;
using static AquariumCiv.View.Shaping.Tests.ShapingLookTests;
using T = AquariumCiv.View.Shaping.ShapingTuning;

namespace AquariumCiv.View.Shaping.Tests
{
    /// <summary>#85: the view's shaping tracker: a tool placed (the tray's call, #84), arriving, waiting, taken back and
    /// gone; the people's answer shown once (label, highlight, the chronicle line as a bubble, staged in the world) and
    /// reported; a loaded box shows no arrivals; and none of it touches the sim's dice.</summary>
    public class ShapingShowTests
    {
        static ViewTime At(SimCore sim, float hour = 10f) => new ViewTime(sim.State.month, hour);

        [Test]
        public void APlacedToolArrivesWaitsAndCanBeTakenBack()
        {
            var sim = NewSim(4);
            var show = new ShapingShow();
            var ev = new List<ShapingEvent>();
            show.Sync(sim, 0f, At(sim), null, ev);
            Assert.IsEmpty(ev);
            var (x, z) = OpenLand(sim, 1.2f, 0.6f);
            float charges = sim.ShapingCharges;
            var r = show.Place(sim, SimCore.NudgeKind.Rain, x, z);
            Assert.IsTrue(r.Ok, r.fail.ToString());
            show.Sync(sim, 0.1f, At(sim), null, ev);
            Assert.AreEqual(1, ev.Count(e => e.kind == ShapingEventKind.Placed && e.nudgeId == r.nudge.id && e.cue == CueLibrary.Rain), "placed, with the rain's cue");
            var e0 = show.Find(r.nudge.id);
            Assert.AreEqual(ShapingStage.Arriving, show.StageOf(e0));
            Assert.Greater(show.Rain(e0).cloud, 0f, "the cloud is forming");
            Assert.IsTrue(show.ChipFor(sim, r.nudge.id).visible, "the take-back chip shows");

            show.Sync(sim, 30f, At(sim), null, ev);
            Assert.IsEmpty(ev);
            Assert.AreEqual(ShapingStage.Waiting, show.StageOf(e0));

            Assert.IsTrue(show.TakeBack(sim, r.nudge.id));
            Assert.AreEqual(charges, sim.ShapingCharges, 1e-4f, "the charge comes back");
            show.Sync(sim, 30.1f, At(sim), null, ev);
            Assert.AreEqual(1, ev.Count(e => e.kind == ShapingEventKind.TakenBack), "taken back");
            Assert.AreEqual(ShapingStage.Leaving, show.StageOf(e0));
            Assert.IsFalse(show.ChipFor(sim, r.nudge.id).visible, "no chip on a leaving gift");
            Assert.Less(show.Rain(e0).wet, 1f, "the soil dries as it leaves");
            show.Sync(sim, 30f + T.LeaveSeconds + 0.1f, At(sim), null, ev);
            Assert.AreEqual(1, ev.Count(e => e.kind == ShapingEventKind.Gone));
            Assert.IsNull(show.Find(r.nudge.id), "dropped");
            Assert.IsFalse(show.TakeBack(sim, r.nudge.id), "only once");
        }

        [Test]
        public void AToolThatCantBeUsedSaysWhy()
        {
            var sim = NewSim(4);
            var show = new ShapingShow();
            show.Sync(sim, 0f, At(sim));
            var r = show.Place(sim, SimCore.NudgeKind.Channel, 1f, 1f);
            Assert.IsFalse(r.Ok);
            Assert.AreEqual(SimCore.ShapeFail.Locked, r.fail, "the channel comes with the village");
            Assert.IsEmpty(show.Entries, "nothing shows");
            Assert.AreEqual(CueLibrary.ShapeFail, ShapingShow.FailCue);
        }

        [TestCase(SimCore.NudgeKind.Grove, Era.Village, 1234)]
        [TestCase(SimCore.NudgeKind.Rain, Era.Camp, 3)]
        [TestCase(SimCore.NudgeKind.Herd, Era.Camp, 1)]
        [TestCase(SimCore.NudgeKind.Channel, Era.Village, 7)]
        public void ThePeoplesAnswerIsShownOnceAndCredited(string kind, Era era, int seed)
        {
            var sim = era == Era.Camp ? NewSim(seed) : SimIn(era, seed);
            if (era == Era.Camp) for (int m = 0; m < 12; m++) sim.Step();
            var planner = LifePlanner.Wired(sim);
            var show = new ShapingShow();
            var ev = new List<ShapingEvent>();
            long day = sim.State.month;
            while (planner.IsRestDay(day)) day++;
            var view = new ViewTime(day, 10f);
            show.Sync(sim, 0f, view, planner, ev);
            var (x, z) = OpenLand(sim, 1.2f, 0.6f);
            var n = show.DebugPlace(sim, kind, x, z);
            Assert.IsNotNull(n);
            show.Sync(sim, 0.05f, view, planner, ev);
            Assert.AreEqual(ShapingEventKind.Placed, ev.Single().kind);
            show.Sync(sim, 20f, view.AddHours(0.5f), planner, ev);

            Assert.IsTrue(show.SkipToAnswer(sim, n.id), "the people answer");
            planner.Invalidate();
            var entry = ShapingAnswers.EntryOf(sim, n);
            Assert.IsNotNull(entry, "a chronicle line credits it");
            show.Sync(sim, 21f, view.AddHours(0.6f), planner, ev);
            Assert.AreEqual(1, ev.Count(e => e.kind == ShapingEventKind.Answered && e.cue == CueLibrary.Chime), "answered, with a chime");
            var e0 = show.Find(n.id);
            Assert.AreEqual(ShapingStage.Answered, show.StageOf(e0));
            Assert.IsFalse(show.ChipFor(sim, n.id).visible, "used: no take-back");
            Assert.IsNotNull(e0.moment, "the answer is staged in the world");
            Assert.AreEqual(n.id, e0.moment.nudgeId);
            TestContext.WriteLine($"{kind} {era} seed {seed}: \"{entry.text}\"; cast {string.Join(", ", e0.moment.cast.Select(c => $"{c.who} {c.role}"))}");

            show.Sync(sim, 23f, view.AddHours(0.62f), planner, ev);
            var a = show.Answer;
            Assert.IsTrue(a.active);
            Assert.AreEqual(n.id, a.nudgeId);
            Assert.AreEqual(ShapingLooks.LabelOf(kind, n.variant, (Era)n.eraAtPlace), a.label);
            Assert.AreEqual(entry.text, a.bubble, "the chronicle line, as a world bubble");
            Assert.AreEqual(entry.id, a.chronicleId, "the card it credits");
            Assert.AreEqual(n.buildingId, a.buildingId);
            if (n.buildingId >= 0) Assert.Greater(a.highlight, 0.9f, "the new building lights up");
            Assert.AreEqual(ShapingAnswers.CreditedNotableId(sim, n), a.notableId);
            Assert.IsFalse(n.reported, "not reported until shown");

            float total = ShapingLooks.AnswerSeconds(entry.text);
            show.Sync(sim, 21f + total + 0.1f, view.AddHours(1f), planner, ev);
            Assert.IsFalse(show.Answer.active, "then it ends");
            Assert.IsTrue(n.reported, "and is reported");
            show.Sync(sim, 60f, view.AddHours(1.2f), planner, ev);
            Assert.IsFalse(ev.Any(e => e.kind == ShapingEventKind.Answered), "shown once");
        }

        [Test]
        public void ALoadedBoxShowsNoArrivalsAndOnlyRecentAnswers()
        {
            var sim = NewSim(6);
            for (int m = 0; m < 12; m++) sim.Step();
            var (x, z) = OpenLand(sim, 1.2f, 0.6f);
            var old = sim.DebugShape(SimCore.NudgeKind.Grove, x, z);
            sim.DebugSkipToAnswer(old.id);
            for (int m = 0; m < 50; m++) sim.Step();
            var recent = sim.DebugShape(SimCore.NudgeKind.Rain, -x, z);
            sim.DebugSkipToAnswer(recent.id);
            var waiting = sim.DebugShape(SimCore.NudgeKind.Herd, x, -z);
            var show = new ShapingShow();
            var ev = new List<ShapingEvent>();
            show.Sync(sim, 100f, At(sim), null, ev);
            Assert.IsFalse(ev.Any(e => e.kind == ShapingEventKind.Placed), "no arrival replays on a return");
            Assert.AreEqual(ShapingStage.Waiting, show.StageOf(show.Find(waiting.id)));
            Assert.AreEqual(1f, show.Grove(show.Find(old.id)).trees.Max(t => t.growth), 1e-4f, "the old grove is grown");
            Assert.IsTrue(old.reported, "an answer from long ago is reported quietly");
            Assert.AreEqual(1, ev.Count(e => e.kind == ShapingEventKind.Answered && e.nudgeId == recent.id), "a recent one is shown");
            Assert.AreEqual(0f, show.Herd(show.Find(waiting.id)).splash, "no splash replay");
        }

        (SimCore sim, LifePlanner planner, ShapingShow show, Nudge n, long day) AnsweredAt(int seed, float hour, List<ShapingEvent> ev)
        {
            var sim = NewSim(seed);
            for (int m = 0; m < 12; m++) sim.Step();
            var planner = LifePlanner.Wired(sim);
            var show = new ShapingShow();
            long day = sim.State.month;
            while (planner.IsRestDay(day) || planner.IsRestDay(day + 1)) day++;
            show.Sync(sim, 0f, new ViewTime(day, hour), planner, ev);
            var (x, z) = OpenLand(sim, 1.2f, 0.6f);
            var n = show.DebugPlace(sim, SimCore.NudgeKind.Grove, x, z);
            Assert.IsTrue(show.SkipToAnswer(sim, n.id));
            planner.Invalidate();
            return (sim, planner, show, n, day);
        }

        [Test]
        public void AnAnswerAfterDarkWaitsForMorningAndIsThenStaged()
        {
            var ev = new List<ShapingEvent>();
            var (sim, planner, show, n, day) = AnsweredAt(6, 21f, ev);
            show.Sync(sim, 5f, new ViewTime(day, 21f), planner, ev);
            Assert.IsFalse(ev.Any(e => e.kind == ShapingEventKind.Answered), "not shown at night");
            Assert.IsFalse(show.Answer.active);
            Assert.IsNull(show.Find(n.id).moment, "nobody is sent over at night");
            Assert.IsFalse(n.reported);
            Assert.IsTrue(n.credited, "the sim's answer itself isn't delayed");
            // The view day turns and the small hours pass: still held.
            show.Sync(sim, 30f, new ViewTime(day + 1, 2f), planner, ev);
            show.Sync(sim, 40f, new ViewTime(day + 1, 6.5f), planner, ev);
            Assert.IsFalse(ev.Any(e => e.kind == ShapingEventKind.Answered), "held over the day turn");
            Assert.IsFalse(n.reported);
            show.Sync(sim, 50f, new ViewTime(day + 1, 8f), planner, ev);
            Assert.AreEqual(1, ev.Count(e => e.kind == ShapingEventKind.Answered), "shown with the people next morning");
            Assert.IsNotNull(show.Find(n.id).moment, "staged in the world");
            Assert.IsTrue(show.Answer.active);
        }

        [Test]
        public void AnAnswerByDayIsStagedAtOnce()
        {
            var ev = new List<ShapingEvent>();
            var (sim, planner, show, n, day) = AnsweredAt(6, 10f, ev);
            show.Sync(sim, 5f, new ViewTime(day, 10f), planner, ev);
            Assert.AreEqual(1, ev.Count(e => e.kind == ShapingEventKind.Answered));
            Assert.IsNotNull(show.Find(n.id).moment);
        }

        [Test]
        public void HeldAnswersKeepTheQueueCap()
        {
            var sim = NewSim(6);
            for (int m = 0; m < 12; m++) sim.Step();
            var show = new ShapingShow();
            var ev = new List<ShapingEvent>();
            long day = sim.State.month;
            show.Sync(sim, 0f, new ViewTime(day, 22f), null, ev);
            var ns = new List<Nudge>();
            for (int i = 0; i < 5; i++)
            {
                var (x, z) = OpenLand(sim, 1.2f, 0.6f);
                var n = sim.DebugShape(SimCore.NudgeKind.Rain, i % 2 == 0 ? x : -x, i < 2 ? z : -z + i * 0.01f);
                if (n == null) continue;
                sim.DebugSkipToAnswer(n.id);
                ns.Add(n);
            }
            Assert.GreaterOrEqual(ns.Count, 4);
            show.Sync(sim, 5f, new ViewTime(day, 22f), null, ev);
            Assert.AreEqual(ShapingShow.MaxQueuedAnswers, ns.Count(q => !q.reported), "three are held, the rest reported quietly");
            show.Sync(sim, 10f, new ViewTime(day + 1, 9f), null, ev);
            Assert.AreEqual(1, ev.Count(e => e.kind == ShapingEventKind.Answered));
        }

        [Test]
        public void ShowingShapingsNeverTouchesTheSimsDice()
        {
            var a = SimIn(Era.Village, 1234);
            var b = SimIn(Era.Village, 1234);
            var planner = LifePlanner.Wired(a);
            var show = new ShapingShow();
            var (x, z) = OpenLand(a, 1.2f, 0.6f);
            float now = 0f;
            show.Sync(a, now, At(a), planner);
            var placed = new List<int>();
            foreach (var kind in SimCore.SliceTools)
            {
                var ra = show.Place(a, kind, x, z);
                var rb = b.PlaceShaping(kind, x, z);
                Assert.AreEqual(ra.fail, rb.fail);
                if (ra.Ok) placed.Add(ra.nudge.id);
                x = -x;
            }
            Assert.IsNotEmpty(placed);
            for (int m = 0; m < 30; m++)
            {
                for (int f = 0; f < 20; f++)
                {
                    now += 0.5f;
                    show.Sync(a, now, new ViewTime(a.State.month, 8f + f * 0.4f), planner);
                    foreach (var e in show.Entries)
                    {
                        show.Rain(e); show.Grove(e); show.Channel(e); show.Herd(e);
                        show.ChipFor(a, e.nudgeId);
                    }
                }
                a.Step(); b.Step();
            }
            // The only write is the view's own "shown" flag.
            foreach (var n in a.State.nudges) n.reported = false;
            foreach (var n in b.State.nudges) n.reported = false;
            Assert.AreEqual(JsonUtility.ToJson(b.State), JsonUtility.ToJson(a.State), "a box whose shapings were shown lives like one whose weren't");
        }
    }
}
