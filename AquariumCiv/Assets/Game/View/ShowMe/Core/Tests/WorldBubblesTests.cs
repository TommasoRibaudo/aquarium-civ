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
        /// <summary>These tests are about the gaps and the words: no warm-up (it has its own test).</summary>
        static BubbleRules NoWarmUp => new BubbleRules { warmUpSeconds = 0f };

        /// <summary>User 2026-10-30: about 8 s of quiet before the first bubble of a session, also for a moment's line;
        /// a return to the box warms up again; the debug panel's force ignores it.</summary>
        [Test]
        public void TheFirstBubbleOfASessionWaitsForTheWarmUp()
        {
            var sim = Believing(Era.Village, 7, 2, true);
            var wb = new WorldBubbles(sim);
            Assert.AreEqual(8f, wb.rules.warmUpSeconds, 2f, "about 8 s");
            float warm = wb.rules.warmUpSeconds;
            Assert.IsNull(wb.Next(100), "the session starts quiet");
            Assert.IsNull(wb.Next(100 + warm - 0.5, PeopleMoment.Glass), "a moment's line waits too");
            Assert.IsNotNull(wb.Next(100 + warm + 0.5), "then someone speaks");
            wb.WarmUp();
            double later = 100 + warm + wb.rules.minGapSeconds + 10;
            Assert.IsNull(wb.Next(later), "back from the home screen: quiet again");
            Assert.IsNotNull(wb.Next(later + warm + 0.5));
            var fresh = new WorldBubbles(sim);
            Assert.IsNotNull(fresh.Force(0), "force ignores the warm-up");
        }

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
            if (later && stage == 2) sim.DebugGiveRelic(RelicId.KeeperDoll); // #718: stages 1 and 3 have no relic; the debug belief opens their later half
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
            var wb = new WorldBubbles(sim, NoWarmUp);
            var got = Play(wb);
            foreach (var (t, b) in got) TestContext.WriteLine($"{t,5}s {b}");
            Assert.IsNotEmpty(got, "the people say something");
            // What the town wants (#181) describes how things stand now, so it may come round again; it never says the same words
            // twice running for one case, and it only repeats after a pause. Everything else is never said twice.
            var told = got.Where(x => x.b.source != BubbleSource.Want).ToList();
            Assert.AreEqual(told.Count, told.Select(x => x.b.key).Distinct().Count(), "never the same line twice");
            Assert.AreEqual(told.Count, told.Select(x => x.b.text).Distinct().Count(), "never the same words twice");
            foreach (var g in got.Where(x => x.b.source == BubbleSource.Want).GroupBy(x => x.b.wantCase))
            {
                var l = g.ToList();
                for (int i = 1; i < l.Count; i++) Assert.AreNotEqual(l[i - 1].b.text, l[i].b.text, $"{g.Key}: not the same words twice running");
            }
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
                Assert.That(b.seconds, Is.InRange(3.5f, 7f));
                Assert.LessOrEqual(b.lines.Count, PeopleVoice.BubbleMaxLines, "fits a phone bubble");
                string lower = b.text.ToLowerInvariant();
                if (stage < 3) Assert.IsFalse(lower.Contains("glass"), $"{b}: no 'glass' before stage 3 (#723)");
                if (stage == 1) Assert.IsFalse(lower.Contains("window"), $"{b}: the camp has no window");
            }
            Assert.IsNotEmpty(sim.State.saidBubbles, "what was said is kept with the box (#120)");
            sim.State.saidBubbles.Clear(); // the one thing they write: the memory of what was said
            Assert.AreEqual(before, JsonUtility.ToJson(sim.State), "bubbles never touch the sim");
        }

        [Test]
        public void QuietWhileTheCardsAreOpenAndForceIgnoresTheGap()
        {
            var sim = Believing(Era.Village, 7, 2, true);
            var wb = new WorldBubbles(sim, NoWarmUp);
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
            var wb = new WorldBubbles(sim, NoWarmUp);
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
            // #719: one telling per story: a wedding is a card, so its bubble is its card quote, never the story voice too
            var v = PeopleVoice.StoryLine(sim, e);
            Assert.IsNotNull(v, "the wedding still has its first-person words (for a line that is never a card)");
            Assert.AreEqual(live[0].id, v.speakerId, "by the one it's about");
            StringAssert.Contains(live[1].name, v.text);
            var wb = new WorldBubbles(sim, NoWarmUp);
            Assert.IsFalse(wb.Candidates().Any(x => x.entryId == e.id), "no quote: the carded wedding is not told again in a bubble");
            e.quote = "We said yes by the fire."; e.speakerId = live[0].id;
            var b = wb.Candidates().FirstOrDefault(x => x.entryId == e.id);
            Assert.IsNotNull(b, "with a quote, the quote is its bubble");
            Assert.AreEqual(BubbleSource.StoryQuote, b.source);
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
            var wb = new WorldBubbles(sim, NoWarmUp);
            var a = wb.Next(0, PeopleMoment.Glass);
            Assert.IsNotNull(a);
            wb.Reset();
            Assert.AreEqual(a.key, wb.Next(0, PeopleMoment.Glass)?.key);
        }

        // ---------- #719: the people's words ----------

        /// <summary>#719: one telling per story. Over two hours in every stage, a story that is a card is never also told in the story
        /// voice (only its card quote may be a bubble); the voice is for lines that are never cards.</summary>
        [TestCase(Era.Camp, 1, 1)]
        [TestCase(Era.Village, 7, 2)]
        [TestCase(Era.Town, 1234, 3)]
        public void ACardedStoryIsNeverAlsoToldInTheStoryVoice(Era era, int seed, int stage)
        {
            var sim = Believing(era, seed, stage, false);
            var wb = new WorldBubbles(sim, NoWarmUp);
            var got = Play(wb);
            foreach (var (_, b) in got.Where(x => x.b.source == BubbleSource.StoryVoice))
            {
                var e = sim.State.chronicle.First(c => c.id == b.entryId);
                Assert.IsFalse(SimCore.ShownAsCard(e), $"{e.kind} \"{e.text}\" is a card: its bubble is its quote, never the voice too");
            }
            var byEntry = got.Where(x => x.b.entryId >= 0).GroupBy(x => x.b.entryId);
            foreach (var g in byEntry) Assert.AreEqual(1, g.Count(), $"entry {g.Key} told once");
        }

        /// <summary>#719: no Pip, goat, whiskers, cat, library or Keeper's lamp in the bank or the trait lines; the new leader is the
        /// era's; the camp's festival has no bread; the grump's dog is a real dog or none.</summary>
        [Test]
        public void ThePeoplesWordsNameOnlyWhatTheBoxHas()
        {
            var banned = new[] { "pip", "goat", "whiskers", "cat", "library", "keeper's lamp" };
            IEnumerable<string> words(string t) => System.Text.RegularExpressions.Regex.Split(t.ToLowerInvariant(), "[^a-z']+");
            foreach (var t in SimCore.LineBank.Select(l => l.text).Concat(PeopleVoice.TraitLines.SelectMany(l => new[] { l.text, l.without ?? "" }))
                         .Concat(PeopleVoice.BankFirstPerson.Values))
            {
                foreach (var w in banned.Where(x => !x.Contains(' '))) Assert.IsFalse(words(t).Contains(w), $"\"{t}\" says {w}");
                StringAssert.DoesNotContain("Keeper's lamp", t);
            }
            Assert.That(PeopleVoice.MomentLines(MomentKind.NewLeader, -1, -1, era: Era.Village).Select(x => x.text), Has.Member("Long live the new headman!"));
            Assert.That(PeopleVoice.MomentLines(MomentKind.NewLeader, -1, -1, era: Era.Town).Select(x => x.text), Has.Member("Long live the new lord!"));
            Assert.That(PeopleVoice.MomentLines(MomentKind.NewLeader, -1, -1, era: Era.Camp).Select(x => x.text), Has.Member("Long live the new chief!"));
            Assert.That(PeopleVoice.MomentLines(MomentKind.Festival, -1, -1, era: Era.Camp).Select(x => x.text), Has.No.Member("Pass the bread, please."));
            var village = Believing(Era.Village, 7, 2, false);
            var grump = PeopleVoice.TraitLines.Single(l => l.id == "grump2");
            string said = PeopleVoice.TraitText(village, grump);
            if (string.IsNullOrEmpty(village.GrownDogName)) Assert.AreEqual("The Keeper looks in at supper. Rude, but I don't mind.", said);
            else StringAssert.Contains(village.GrownDogName, said);
            var town = Believing(Era.Town, 1234, 3, false);
            string loud = PeopleVoice.TraitText(town, PeopleVoice.TraitLines.Single(l => l.id == "loud3"));
            Assert.AreEqual(town.Count(BuildingType.Market) > 0, loud != null, "the market line only with a market");
        }

        /// <summary>#719: a bank line that names a child ("Not you, {kid}") is said only with a living child, and with their name.</summary>
        [Test]
        public void AKidsNameIsARealChildOrTheLineIsNotSaid()
        {
            var camp = Believing(Era.Camp, 1, 1, false);
            bool kid = camp.State.notables.Any(n => n.alive && n.age >= 4f && n.age < SimCore.AdultAge);
            var three = camp.LinesNow().FirstOrDefault(l => l.no == 3);
            Assert.AreEqual(kid, three != null, "line 3 only with a child");
            foreach (var n in camp.State.notables.Where(n => n.age < SimCore.AdultAge)) n.age = 30f;
            Assert.IsFalse(camp.LinesNow().Any(l => l.text.Contains("{kid}")), "no child: the lines that name one are not said");
        }
    }
}
