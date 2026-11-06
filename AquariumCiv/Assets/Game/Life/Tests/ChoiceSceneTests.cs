using System;
using System.Collections.Generic;
using System.Linq;
using AquariumCiv.Life;
using AquariumCiv.Sim;
using NUnit.Framework;
using UnityEngine;

namespace AquariumCiv.Life.Tests
{
    /// <summary>#224: the choice scene in the box. The rules (who stands where for each kind, which props, the timing, the reactions) and
    /// the staging on a real camp: the people walk over, wait looking up in a loop, react to the touch, then walk back into their day,
    /// and the planner never writes the sim.</summary>
    public class ChoiceSceneTests
    {
        static readonly (float x, float z) Right = (1f, 0f), Viewer = (0f, -1f);

        // ---------------------------------------------------------------- the rules

        [Test]
        public void EachKindHasItsOwnStaging()
        {
            var who = ChoiceScenes.Rules(DilemmaKind.Who);
            Assert.AreEqual(ChoiceStand.OptionPerson, who.a.stand); Assert.AreEqual(ChoiceStand.OptionPerson, who.b.stand);
            Assert.AreEqual(ChoiceProp.None, who.a.prop);
            Assert.AreEqual(ChoiceAnchor.FireOrSeat, who.a.anchor);
            var way = ChoiceScenes.Rules(DilemmaKind.WhichWay);
            Assert.AreEqual(ChoiceProp.Stake, way.a.prop); Assert.AreEqual(ChoiceProp.Stake, way.b.prop);
            Assert.AreEqual(ChoiceAnchor.Building, way.a.anchor);
            var trad = ChoiceScenes.Rules(DilemmaKind.Tradition);
            Assert.AreEqual(ChoiceProp.Offering, trad.a.prop); Assert.AreEqual(ChoiceProp.Offering, trad.b.prop);
            Assert.AreEqual(ChoiceAnchor.ShrineOrGather, trad.a.anchor);
            var str = ChoiceScenes.Rules(DilemmaKind.Strangers);
            Assert.AreEqual(ChoiceStand.Asker, str.a.stand, "the chief at the fire");
            Assert.AreEqual(ChoiceStand.Travellers, str.b.stand, "the travellers at the edge");
            Assert.AreEqual(ChoiceAnchor.Edge, str.b.anchor);
            Assert.IsTrue(ChoiceScenes.AskerBehind(DilemmaKind.Who));
            Assert.IsFalse(ChoiceScenes.AskerBehind(DilemmaKind.Strangers));
        }

        [Test]
        public void FlankPutsOptionZeroOnTheLeftAndBothTowardsTheGlass()
        {
            foreach (var r in new[] { (1f, 0f), (0f, 1f), (0.6f, -0.8f) })
            {
                var t = (-r.Item2, r.Item1); // any unit vector square to it
                var (l, rr) = ChoiceScenes.Flank((0.5f, -0.3f), r, t, 0.3f, 0.1f);
                float dl = (l.x - 0.5f) * r.Item1 + (l.z + 0.3f) * r.Item2, dr = (rr.x - 0.5f) * r.Item1 + (rr.z + 0.3f) * r.Item2;
                Assert.Less(dl, 0f, "left is on the screen's left");
                Assert.Greater(dr, 0f, "right is on the screen's right");
                Assert.AreEqual(0.6f, Dist(l, rr), 1e-4f, "two spreads apart");
                Assert.AreEqual(0.1f, (l.x - 0.5f) * t.Item1 + (l.z + 0.3f) * t.Item2, 1e-4f, "towards the glass");
            }
        }

        [Test]
        public void TheTravellersStandTowardsTheEdgeButInsideTheBox()
        {
            const float hx = 3f, hz = 2.2f;
            for (float fx = -2.5f; fx <= 2.5f; fx += 0.5f)
                for (float fz = -1.8f; fz <= 1.8f; fz += 0.6f)
                {
                    var e = ChoiceScenes.EdgePoint((fx, fz), Right, hx, hz, out bool onRight);
                    string tag = $"fire at {fx},{fz}";
                    Assert.LessOrEqual(Math.Abs(e.x), hx - ChoiceScenes.EdgeMargin + 1e-4f, tag);
                    Assert.LessOrEqual(Math.Abs(e.z), hz - ChoiceScenes.EdgeMargin + 1e-4f, tag);
                    float d = Dist(e, (fx, fz));
                    Assert.LessOrEqual(d, ChoiceScenes.EdgeMax + 1e-4f, tag + ": both fit one look");
                    if (Math.Abs(fx) < 1.4f) Assert.GreaterOrEqual(d, ChoiceScenes.EdgeMin - 1e-4f, tag + ": clearly apart from the fire");
                    Assert.AreEqual(onRight, e.x > fx, tag);
                    if (fx <= 1.5f) Assert.IsTrue(onRight, tag + ": on the right while there's room");
                }
            var spots = ChoiceScenes.TravellerSpots((1f, 0f), Right, Viewer, 3);
            Assert.AreEqual(3, spots.Count);
            Assert.Greater(Dist(spots[0], spots[1]), 0.1f, "a group, not a heap");
        }

        [Test]
        public void TheWaitIsALoopThatCoversTheWholeWait()
        {
            foreach (int salt in new[] { 0, 1, 7, 23 })
                foreach (float come in new[] { 0f, 4f, 11f })
                {
                    var beats = ChoiceScenes.WaitBeats(60f, come, ActivityKind.WaveWary, salt);
                    Assert.AreEqual(0f, beats[0].from, 1e-4f);
                    Assert.AreEqual(60f, beats[beats.Count - 1].to, 1e-3f);
                    for (int i = 1; i < beats.Count; i++)
                    {
                        Assert.AreEqual(beats[i - 1].to, beats[i].from, 1e-4f, "no gap");
                        Assert.AreNotEqual(beats[i - 1].kind, beats[i].kind, "each beat changes the pose");
                    }
                    Assert.IsTrue(beats.Any(b => b.kind == ActivityKind.WaveWary), "a small gesture now and then");
                    foreach (var b in beats.Skip(1)) Assert.LessOrEqual(b.to - b.from, ChoiceScenes.LookSeconds + 2f, "never frozen for long");
                    Assert.IsTrue(beats.All(b => b.kind == ActivityKind.LookUp || b.kind == ActivityKind.WaveWary));
                }
            var a = ChoiceScenes.WaitBeats(60f, 0f, ActivityKind.Point, 0).Select(b => b.from).ToList();
            var c = ChoiceScenes.WaitBeats(60f, 0f, ActivityKind.Point, 1).Select(b => b.from).ToList();
            CollectionAssert.AreNotEqual(a, c, "two people never move as one");
        }

        [Test]
        public void TheChosenCheersTheOtherShrugsOrSulksByTrait()
        {
            Assert.AreEqual(ActivityKind.Cheer, ChoiceScenes.ReactBeats(true, false, "the grump").Single().kind);
            var sulk = ChoiceScenes.ReactBeats(false, false, "the stubborn");
            Assert.AreEqual(ActivityKind.SitOut, sulk.Last().kind);
            var shrug = ChoiceScenes.ReactBeats(false, false, "the kind");
            Assert.AreEqual(ActivityKind.Clap, shrug.Last().kind);
            Assert.AreEqual(ActivityKind.WaveWary, shrug.First().kind);
            Assert.AreEqual(ActivityKind.Clap, ChoiceScenes.ReactBeats(false, false, null).Last().kind, "the crowd shrugs");
            Assert.AreEqual(ActivityKind.Clap, ChoiceScenes.ReactBeats(false, true, "the grump").Last().kind, "the asker claps");
            foreach (var b in new[] { sulk, shrug }) Assert.AreEqual(1f, b.Last().to, 1e-5f);
        }

        [Test]
        public void TimingMatchesTheIssue()
        {
            Assert.AreEqual(20f, ChoiceScenes.DriftSeconds, 2f, "people drift back to work after ~20 s");
            Assert.Greater(ChoiceScenes.WaitSeconds, ChoiceScenes.DriftSeconds);
            Assert.That(ChoiceScenes.ReactSeconds, Is.InRange(4f, 12f));
        }

        // ---------------------------------------------------------------- staged on a real box

        static SimCore Grown(int seed, int months)
        {
            var cfg = new SimConfig();
            var sim = new SimCore(cfg, SimCore.NewWorld(seed, cfg));
            for (int m = 0; m < months; m++) sim.Step();
            return sim;
        }

        /// <summary>A box with this dilemma raised, or null when it doesn't hold in any of the seeds.</summary>
        static (SimCore sim, Dilemma d) Raised(string defId, int months)
        {
            foreach (int seed in new[] { 1, 2, 6, 7, 10, 16, 18, 33, 1234, 7531 })
            {
                var sim = Grown(seed, months);
                // Some defs wait on the box (camp_fire needs an elder chief, #222 revision): look a little later too.
                for (int tries = 0; tries < 16; tries++)
                {
                    var d = sim.DebugRaiseDilemma(defId);
                    if (d != null) return (sim, d);
                    for (int m = 0; m < 12; m++) sim.Step();
                }
            }
            return (null, null);
        }

        static Dictionary<int, PersonActivity> Plans(LifePlanner planner, ViewTime t)
        {
            var acts = new List<PersonActivity>();
            planner.PlanAll(t, acts);
            return acts.ToDictionary(a => a.who.Key);
        }

        static ViewTime After(ViewTime at, float seconds) => LifeClock.Advance(at, seconds);

        static readonly string[] CampDefs = { "camp_fire", "camp_winter", "camp_remember", "camp_travellers" };

        [Test]
        public void EveryCampKindStagesClearlyApartAndNeverWritesTheSim()
        {
            foreach (var defId in CampDefs)
            {
                var (sim, d) = Raised(defId, 72);
                Assert.IsNotNull(sim, defId + " holds in some seed");
                var planner = LifePlanner.Wired(sim);
                var v = sim.ViewOfDilemma(d);
                string before = JsonUtility.ToJson(sim.State);
                var at = new ViewTime(sim.State.month, 10f);
                var st = planner.StageChoice(v, at, Right.x, Right.z, Viewer.x, Viewer.z);
                Assert.IsNotNull(st, defId + " stages");
                Assert.AreEqual(d.id, st.dilemmaId);
                Assert.AreEqual(v.kind, st.kind);
                float apart = Dist((st.sides[0].x, st.sides[0].z), (st.sides[1].x, st.sides[1].z));
                Assert.GreaterOrEqual(apart, 0.35f, defId + ": the two choices are clearly apart");
                foreach (var s in st.sides)
                {
                    Assert.LessOrEqual(Math.Abs(s.x), 3f, defId); Assert.LessOrEqual(Math.Abs(s.z), 2.2f, defId);
                }
                switch (v.kind)
                {
                    case DilemmaKind.Who:
                        Assert.AreEqual(v.options[0].personId, st.sides[0].people.Single().who.notableId);
                        Assert.AreEqual(v.options[1].personId, st.sides[1].people.Single().who.notableId);
                        Assert.Less(st.sides[0].x, st.sides[1].x, "option 0 on the Keeper's left");
                        break;
                    case DilemmaKind.WhichWay:
                        Assert.AreEqual(ChoiceProp.Stake, st.sides[0].Prop);
                        break;
                    case DilemmaKind.Tradition:
                        Assert.AreEqual(ChoiceProp.Offering, st.sides[1].Prop);
                        Assert.Less(st.sides[0].x, st.sides[1].x);
                        break;
                    case DilemmaKind.Strangers:
                        Assert.AreEqual(2, st.sides[1].travellers.Count, "the travellers are a couple");
                        Assert.AreEqual(0, st.sides[1].people.Count, "not people of the box yet");
                        break;
                }
                // They come over and wait looking up, in a loop.
                var kinds = new HashSet<ActivityKind>();
                foreach (var pin in st.Pins())
                    for (float s = 32f; s <= 52f; s += 0.5f)
                    {
                        var a = Plans(planner, After(at, s))[pin.who.Key];
                        Assert.IsFalse(a.moving, $"{defId}: {pin.who} has arrived after 32 s");
                        Assert.IsTrue(a.facesGlass, $"{defId}: {pin.who} looks to the glass");
                        Assert.AreEqual(ActivitySource.Scene, a.source);
                        kinds.Add(a.kind);
                    }
                if (st.Pins().Any()) Assert.Greater(kinds.Count, 1, defId + ": an idle loop, not frozen");
                Assert.AreEqual(before, JsonUtility.ToJson(sim.State), defId + ": staging never writes the sim");
            }
        }

        [Test]
        public void ATouchMakesTheChosenCheerThenEveryoneGoesBackToTheirDay()
        {
            var (sim, d) = Raised("camp_fire", 72);
            Assert.IsNotNull(sim);
            var planner = LifePlanner.Wired(sim);
            var v = sim.ViewOfDilemma(d);
            var at = new ViewTime(sim.State.month, 10f);
            var st = planner.StageChoice(v, at, Right.x, Right.z, Viewer.x, Viewer.z);
            Assert.IsNotNull(st);
            int chosenKey = st.sides[0].people[0].who.Key, otherKey = st.sides[1].people[0].who.Key;
            var touch = After(at, 25f);
            Assert.IsTrue(sim.AnswerDilemma(d.id, 0, DilemmaPath.Box));
            Assert.IsTrue(planner.ReactChoice(0, touch));
            Assert.AreEqual(0, planner.Choice.chosen);
            var mid = After(touch, ChoiceScenes.ReactSeconds * 0.7f);
            var plans = Plans(planner, mid);
            Assert.AreEqual(ActivityKind.Cheer, plans[chosenKey].kind, "the chosen one cheers");
            Assert.IsTrue(plans[otherKey].kind == ActivityKind.Clap || plans[otherKey].kind == ActivityKind.SitOut, "the other one shrugs or sulks: " + plans[otherKey].kind);
            // Nobody walks off and back at the touch: the reaction goes on from where the wait left them.
            for (float s = 0.1f; s < ChoiceScenes.ReactSeconds; s += 0.25f)
                Assert.IsFalse(Plans(planner, After(touch, s))[chosenKey].moving, $"{s:0.00} s into the reaction the chosen one stays put");
            // Over: back to the day (the plan with no scene at all), walking back from where they stood.
            var over = After(touch, ChoiceScenes.ReactSeconds);
            Assert.IsFalse(planner.ChoiceRunning(After(over, 0.5f)));
            planner.EndChoice(over);
            Assert.IsNull(planner.Choice);
            var later = After(over, 90f);
            var fresh = LifePlanner.Wired(sim);
            var back = Plans(planner, later);
            var day = Plans(fresh, later);
            Assert.AreEqual(day[chosenKey].kind, back[chosenKey].kind, "back to their day");
            Assert.AreEqual(day[otherKey].kind, back[otherKey].kind, "back to their day");
        }

        [Test]
        public void LeftAloneTheSceneEndsAndTheDilemmaStaysOpen()
        {
            var (sim, d) = Raised("camp_remember", 72);
            Assert.IsNotNull(sim);
            var planner = LifePlanner.Wired(sim);
            var at = new ViewTime(sim.State.month, 11f);
            var st = planner.StageChoice(sim.ViewOfDilemma(d), at, Right.x, Right.z, Viewer.x, Viewer.z, ChoiceScenes.DriftSeconds);
            Assert.IsNotNull(st);
            Assert.IsTrue(planner.ChoiceRunning(After(at, ChoiceScenes.DriftSeconds - 2f)));
            Assert.IsFalse(planner.ChoiceRunning(After(at, ChoiceScenes.DriftSeconds + 1f)), "they drift back after the wait");
            Assert.AreEqual(DilemmaState.Open, d.state, "leaving without answering is fine");
            // The next Show me stages it again.
            Assert.IsNotNull(planner.StageChoice(sim.ViewOfDilemma(d), After(at, 40f), Right.x, Right.z, Viewer.x, Viewer.z));
        }

        /// <summary>Batch 3: a staged moment plays (the welcome on a return) when the Keeper taps Show me: the choice scene holds its
        /// cast. Everyone it stands is out of the moment (the view no longer places them by it) and plays the choice; the rest of the
        /// moment's cast plays on.</summary>
        [Test]
        public void AChoiceStagedDuringAMoment_HoldsItsCast()
        {
            var (sim, d) = Raised("camp_remember", 72);
            Assert.IsNotNull(sim);
            sim.Config.moments = true;
            var m = sim.DebugQueueMoment(MomentKind.Festival);
            Assert.IsNotNull(m, "a festival can be queued");
            var planner = LifePlanner.Wired(sim);
            var at = new ViewTime(sim.State.month, 10f);
            var sc = planner.StageHeldMoment(m, at);
            Assert.IsNotNull(sc, planner.LastHeldWhyNot);
            var ask = After(at, sc.plan.ringT0 + 1f);
            var st = planner.StageChoice(sim.ViewOfDilemma(d), ask, Right.x, Right.z, Viewer.x, Viewer.z);
            Assert.IsNotNull(st);
            var cast = st.Pins().Select(p => p.who).ToList();
            Assert.IsTrue(cast.Any(w => sc.plan.Member(w.Key) != null), "the test needs someone in both (the festival takes everyone near)");
            var later = After(ask, 6f);
            var plans = Plans(planner, later);
            foreach (var w in cast)
            {
                Assert.IsFalse(planner.HeldMomentAt(w, later, out _, out _, out _, out _), w + " is out of the moment");
                Assert.AreNotEqual(ActivityKind.Dance, plans[w.Key].kind, w + " plays the choice, not the ring");
            }
            var rest = sc.plan.members.Select(x => x.key > 0 ? PersonRef.Notable(x.key) : PersonRef.Crowd(-x.key - 1)).Where(w => !cast.Contains(w)).ToList();
            Assert.IsTrue(rest.Any(w => planner.HeldMomentAt(w, later, out _, out _, out _, out _)), "the others play the moment on");
        }

        static float Dist((float x, float z) a, (float x, float z) b) => (float)Math.Sqrt((a.x - b.x) * (a.x - b.x) + (a.z - b.z) * (a.z - b.z));
    }
}
