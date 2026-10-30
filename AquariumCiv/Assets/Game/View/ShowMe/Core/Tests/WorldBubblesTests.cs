using System;
using System.Collections.Generic;
using System.Linq;
using AquariumCiv.Sim;
using NUnit.Framework;
using UnityEngine;

namespace AquariumCiv.View.ShowMe.Tests
{
    /// <summary>#76 phase 1: world bubbles in the people's voice: one at a time and rate limited, never the same line twice,
    /// only what the belief stage may say (no "glass" before the Lens, no "window" in the camp, never the words that state
    /// the game), stories in the words of the people in them, quiet while the cards are open.</summary>
    public class WorldBubblesTests
    {
        static SimCore Believing(Era era, int seed, int stage, bool later)
        {
            var sim = era == Era.Camp ? ShowMeTests.NewSim(seed) : ShowMeTests.SimIn(era, seed);
            if (era == Era.Camp) for (int m = 0; m < 24; m++) sim.Step();
            if (stage == 3 && sim.Scholars.Count == 0)
            {
                var n = sim.State.notables.Where(x => x.alive && x.age >= SimCore.AdultAge && x.age < 55f).OrderBy(x => x.id).First();
                n.trait = "the clever";
            }
            sim.DebugSetBelief(stage, later);
            if (later && stage == 1) sim.DebugGiveRelic(RelicId.SkyCrumb);
            if (later && stage == 2) sim.DebugGiveRelic(RelicId.KeeperDoll);
            if (later && stage == 3) sim.DebugGiveRelic(RelicId.Lens);
            Assert.AreEqual(stage, sim.BeliefStage);
            return sim;
        }

        static readonly string[] Moments =
        {
            null, PeopleMoment.Glass, PeopleMoment.Rain, PeopleMoment.Return, PeopleMoment.Lamp, PeopleMoment.Evening,
            PeopleMoment.Finger, PeopleMoment.Wipe, PeopleMoment.Away, null, null,
        };

        /// <summary>Two hours of play, a second at a time, with a moment now and then.</summary>
        static List<(double t, WorldBubble b)> Play(WorldBubbles wb, double seconds = 7200)
        {
            var got = new List<(double, WorldBubble)>();
            for (int s = 0; s < seconds; s++)
            {
                string moment = s % 37 == 0 ? Moments[(s / 37) % Moments.Length] : null;
                var b = wb.Next(s, moment);
                if (b != null) got.Add((s, b));
            }
            return got;
        }

        [TestCase(Era.Camp, 1, 1, false)]
        [TestCase(Era.Camp, 1, 1, true)]
        [TestCase(Era.Village, 7, 2, false)]
        [TestCase(Era.Village, 7, 2, true)]
        [TestCase(Era.Town, 1234, 3, false)]
        [TestCase(Era.Town, 1234, 3, true)]
        public void BubblesKeepTheirPaceAndNeverRepeat(Era era, int seed, int stage, bool later)
        {
            var sim = Believing(era, seed, stage, later);
            string before = JsonUtility.ToJson(sim.State);
            var wb = new WorldBubbles(sim);
            var got = Play(wb);
            foreach (var (t, b) in got) TestContext.WriteLine($"{t,5}s {b}");
            Assert.IsNotEmpty(got, "the people say something");
            Assert.AreEqual(got.Count, got.Select(x => x.b.key).Distinct().Count(), "never the same line twice");
            Assert.AreEqual(got.Count, got.Select(x => x.b.text).Distinct().Count(), "never the same words twice");
            for (int i = 1; i < got.Count; i++)
            {
                double gap = got[i].t - got[i - 1].t;
                double min = got[i].b.source == BubbleSource.Moment ? wb.rules.momentGapSeconds : wb.rules.minGapSeconds;
                Assert.GreaterOrEqual(gap, min, $"{got[i].b}: one at a time, not too often");
            }
            foreach (var g in got.Where(x => x.b.speakerId >= 0).GroupBy(x => x.b.speakerId))
            {
                var ts = g.Select(x => x.t).ToList();
                for (int i = 1; i < ts.Count; i++) Assert.GreaterOrEqual(ts[i] - ts[i - 1], wb.rules.speakerGapSeconds, $"#{g.Key} speaks again too soon");
            }
            foreach (var (_, b) in got)
            {
                Assert.IsTrue(PeopleVoice.Allowed(b.text, sim), $"{b}: allowed in stage {stage}");
                Assert.IsFalse(b.text.Contains("{"), $"{b}: filled in");
                if (b.speakerId >= 0) Assert.IsTrue(sim.FindNotable(b.speakerId).alive, "the living speak");
                Assert.That(b.seconds, Is.InRange(3f, 6f));
                Assert.LessOrEqual(b.lines.Count, PeopleVoice.BubbleMaxLines, "fits a phone bubble");
                string lower = b.text.ToLowerInvariant();
                if (stage < 3 || !later) Assert.IsFalse(lower.Contains("glass"), $"{b}: no 'glass' before the Lens");
                if (stage == 1) Assert.IsFalse(lower.Contains("window"), $"{b}: the camp has no window");
            }
            Assert.AreEqual(before, JsonUtility.ToJson(sim.State), "bubbles never touch the sim");
        }

        [Test]
        public void QuietWhileTheCardsAreOpenAndForceIgnoresTheGap()
        {
            var sim = Believing(Era.Village, 7, 2, true);
            var wb = new WorldBubbles(sim);
            Assert.IsNull(wb.Next(1000, PeopleMoment.Glass, null, quiet: true), "the card is never covered");
            var first = wb.Next(1000);
            Assert.IsNotNull(first);
            Assert.IsNull(wb.Next(1001), "too soon");
            var forced = wb.Force(1001);
            Assert.IsNotNull(forced, "the debug panel can force one");
            Assert.AreNotEqual(first.key, forced.key);
        }

        [Test]
        public void OnlyPeopleOnScreenSpeak()
        {
            var sim = Believing(Era.Town, 1234, 3, true);
            var wb = new WorldBubbles(sim);
            var nobody = new HashSet<int>();
            foreach (var b in wb.Candidates(PeopleMoment.Glass, nobody)) Assert.AreEqual(-1, b.speakerId, "with nobody on screen only the crowd speaks");
        }

        [Test]
        public void ARecentStoryIsToldByThePeopleInIt()
        {
            var sim = Believing(Era.Village, 7, 2, false);
            var live = sim.State.notables.Where(n => n.alive && n.age >= SimCore.AdultAge).OrderBy(n => n.id).Take(2).ToList();
            var e = sim.Chronicle("wedding", "A wedding.", 0.9f, live[0].id);
            e.otherNotableId = live[1].id;
            var wb = new WorldBubbles(sim);
            var b = wb.Candidates().FirstOrDefault(x => x.entryId == e.id);
            Assert.IsNotNull(b, "the wedding is talked about");
            Assert.AreEqual(BubbleSource.StoryVoice, b.source);
            Assert.AreEqual(live[0].id, b.speakerId, "by the one it's about");
            StringAssert.Contains(live[1].name, b.text);
            StringAssert.StartsWith("I married", b.text);
            wb.Force(0);
            Assert.IsFalse(wb.Candidates().Any(x => x.entryId == e.id), "one bubble a story");
        }

        [Test]
        public void BankStoryLinesAreSaidInTheFirstPerson()
        {
            var sim = Believing(Era.Camp, 1, 1, false);
            var worrier = sim.State.notables.Where(n => n.alive && n.age >= SimCore.AdultAge).OrderBy(n => n.id).First();
            foreach (var n in sim.State.notables) if (n.trait == "the worrier" && n != worrier) n.trait = "the brave";
            worrier.trait = "the worrier";
            var lines = PeopleVoice.BankLinesNow(sim).ToList();
            var four = lines.FirstOrDefault(q => q.lineNo == 4);
            Assert.IsNotNull(four, "the worrier's line, in their own words");
            Assert.AreEqual(worrier.id, four.speakerId);
            StringAssert.StartsWith("I moved the tents", four.text);
            Assert.IsFalse(lines.Any(q => q.lineNo > 13 && q.lineNo != 28), "never a later stage's line");
        }

        [Test]
        public void TheVoiceRules()
        {
            var camp = Believing(Era.Camp, 1, 1, false);
            Assert.IsFalse(PeopleVoice.Allowed("Look at the glass!", camp));
            Assert.IsFalse(PeopleVoice.Allowed("The window's misted.", camp));
            Assert.IsFalse(PeopleVoice.Allowed("Best game ever.", camp));
            Assert.IsFalse(PeopleVoice.Allowed("Back in the box.", camp));
            Assert.IsTrue(PeopleVoice.Allowed("The sky's gone dusty again.", camp));
            Assert.AreEqual("the Bright One", PeopleVoice.KeeperName(camp));
            foreach (var kv in PeopleVoice.BankFirstPerson)
            {
                var line = SimCore.LineBank.First(l => l.no == kv.Key);
                Assert.IsFalse(line.quote, $"line {kv.Key} is a story line");
                Assert.IsFalse(kv.Value.Contains("{"), $"line {kv.Key}: no blanks");
            }
            foreach (var l in PeopleVoice.Wrap("The Keeper hums while working. Prove me wrong.")) Assert.LessOrEqual(l.Length, PeopleVoice.BubbleLineChars);
        }

        [Test]
        public void ResetLetsTheLinesBeSaidAgain()
        {
            var sim = Believing(Era.Village, 7, 2, true);
            var wb = new WorldBubbles(sim);
            var a = wb.Next(0, PeopleMoment.Glass);
            Assert.IsNotNull(a);
            wb.Reset();
            Assert.AreEqual(a.key, wb.Next(0, PeopleMoment.Glass)?.key);
        }
    }
}
