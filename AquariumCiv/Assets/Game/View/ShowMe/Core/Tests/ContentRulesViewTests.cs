using System.Collections.Generic;
using System.Linq;
using AquariumCiv.Sim;
using NUnit.Framework;

namespace AquariumCiv.View.ShowMe.Tests
{
    /// <summary>
    /// #723, the view half: every line the people say in the view true to the box and the era (<see cref="ContentRules"/>): the trait
    /// lines and their other wordings, the held moments' lines in each era (rituals too), the storyteller's lines, the first-person
    /// bank lines, and, in real boxes of each stage, the want, tree and story-voice lines as said there (with the things they name).
    /// Every bubble wraps to at most four lines on a phone. The sim's lines are checked in ContentRulesTests.
    /// </summary>
    public class ContentRulesViewTests
    {
        static readonly List<string> Bad = new List<string>();

        static void Check(string where, string line, int stage, SimCore sim = null)
        {
            if (string.IsNullOrEmpty(line)) return;
            var p = ContentRules.Problems(line, LineVoice.People, stage);
            if (sim != null) p.AddRange(ContentRules.MissingThings(line, sim).Select(t => "needs " + t));
            if (PeopleVoice.Wrap(line).Count > PeopleVoice.BubbleMaxLines) p.Add($"wraps to {PeopleVoice.Wrap(line).Count} lines");
            if (p.Count > 0) Bad.Add($"{where}: \"{line}\" ({string.Join("; ", p)})");
        }

        [SetUp]
        public void Clear() => Bad.Clear();

        [Test]
        public void ABadBubbleFails()
        {
            Check("fixture", "Best game ever, the screen is so bright.", 2);
            Check("fixture", "Look at the glass!", 1);
            Check("fixture", "This one line goes on and on about everything under the sky until nobody remembers how it began, and then it goes on a bit more for luck.", 0);
            Assert.AreEqual(3, Bad.Count, string.Join("\n", Bad));
        }

        [Test]
        public void EveryWrittenLineOfThePeopleFollowsTheRules()
        {
            foreach (var l in PeopleVoice.TraitLines)
            {
                Check("trait " + l.id, l.text.Replace("{dog}", "Bramble"), l.stage);
                Check("trait " + l.id + " (without)", l.without, l.stage);
            }
            foreach (Era era in new[] { Era.Camp, Era.Village, Era.Town })
            {
                int stage = era == Era.Camp ? 1 : era == Era.Village ? 2 : 3;
                string keeper = stage == 1 ? "the Bright One" : "the Keeper";
                foreach (MomentKind k in System.Enum.GetValues(typeof(MomentKind)))
                    foreach (var r in k == MomentKind.Ritual ? RitualKind.All : new string[] { null })
                        foreach (var (text, _) in PeopleVoice.MomentLines(k, 1, 2, r, keeper, era))
                            Check($"moment {k} {r} {era}", text, stage);
            }
            foreach (int manner in new[] { SmallChoices.MannerSlow, SmallChoices.MannerShort })
                foreach (var t in PeopleVoice.TellerLines(manner, true)) Check("teller", t, 0);
            foreach (var kv in PeopleVoice.BankFirstPerson)
                Check($"bank {kv.Key} (first person)", kv.Value, SimCore.LineBank.First(l => l.no == kv.Key).stage);
            Assert.IsEmpty(Bad, string.Join("\n", Bad));
        }

        /// <summary>In a real box of each stage (two seeds), what the people would say now: want, tree, trait and story-voice lines, with the
        /// things they name present in that box.</summary>
        [TestCase(Era.Camp, 1)]
        [TestCase(Era.Camp, 4)]
        [TestCase(Era.Village, 7)]
        [TestCase(Era.Village, 3)]
        [TestCase(Era.Town, 1234)]
        public void WhatThePeopleSayInARealBoxIsTrueToIt(Era era, int seed)
        {
            var sim = era == Era.Camp ? ShowMeTests.NewSim(seed) : ShowMeTests.SimIn(era, seed);
            if (era == Era.Camp) for (int m = 0; m < 24; m++) sim.Step();
            int stage = sim.BeliefStage;
            for (int v = 0; v < 3; v++) foreach (var w in PeopleVoice.WantLines(sim, v)) Check($"want {w.kind}", w.text, stage); // a want names what the box lacks, by design
            foreach (var (_, _, text) in PeopleVoice.TreeLinesNow(sim)) Check("tree", text, stage, sim);
            foreach (var (l, _) in PeopleVoice.TraitLinesNow(sim)) Check("trait " + l.id, l.text, stage, sim);
            foreach (var q in PeopleVoice.BankLinesNow(sim)) Check($"bank {q.lineNo} (first person)", q.text, stage, sim);
            var quote = sim.Quote(PeopleMoment.Any);
            if (quote != null) Check($"bank {quote.lineNo}", quote.text, stage, sim);
            foreach (var e in sim.State.chronicle.Where(x => x.month >= sim.State.month - 36))
            {
                var v = PeopleVoice.StoryLine(sim, e);
                if (v != null) Check("story voice " + e.kind, v.text, stage, sim);
            }
            Assert.IsEmpty(Bad, string.Join("\n", Bad));
        }
    }
}
