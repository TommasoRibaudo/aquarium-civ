using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using AquariumCiv.Sim;
using NUnit.Framework;

namespace AquariumCiv.Tests
{
    /// <summary>
    /// #723: every sim line true to the box and the era (<see cref="ContentRules"/>): the choices and their callbacks, the office
    /// lines, the line bank, the ritual captions, and every chronicle line written over the years on both lands, checked as it is
    /// written against what the box has then (the well, a dog, the Keeper's tree, the pond, the market). A deliberately bad line
    /// fails each check. The view's lines are checked in the View EditMode tests (ContentRulesViewTests).
    /// </summary>
    public class ContentRulesTests
    {
        static List<string> Report(IEnumerable<(string where, string line, List<string> bad)> found) =>
            found.Where(x => x.bad.Count > 0).Select(x => $"{x.where}: \"{x.line}\" ({string.Join("; ", x.bad)})").Distinct().ToList();

        [Test]
        public void ABadLineFailsEachCheck()
        {
            Assert.IsNotEmpty(ContentRules.Problems("They came from over the hill.", LineVoice.Narrator), "outside world");
            Assert.IsEmpty(ContentRules.Problems("The Kells wandered in from a far-away tank.", LineVoice.Narrator), "the one allowed far-away");
            Assert.IsNotEmpty(ContentRules.Problems("Best game ever.", LineVoice.People), "the people state the game");
            Assert.IsEmpty(ContentRules.Problems("Best game ever.", LineVoice.Narrator).Where(p => p.Contains("never say")), "the narrator may");
            Assert.IsNotEmpty(ContentRules.Problems("They moved in with a sheep.", LineVoice.Narrator), "a thing the box never has");
            Assert.IsNotEmpty(ContentRules.Problems("Look at the glass!", LineVoice.People, stage: 2), "glass before stage 3");
            Assert.IsEmpty(ContentRules.Problems("Look at the glass!", LineVoice.People, stage: 3));
            Assert.IsNotEmpty(ContentRules.Problems("Everyone to the window!", LineVoice.People, stage: 1), "window in the camp");
            Assert.IsNotEmpty(ContentRules.Problems("Rain — at last.", LineVoice.Narrator), "an em dash");
            Assert.IsNotEmpty(ContentRules.Problems("“Hello,” she said.", LineVoice.Narrator), "curly quotes");
            Assert.IsNotEmpty(ContentRules.Problems("One two three four five six seven eight nine ten eleven twelve thirteen fourteen fifteen sixteen?", LineVoice.People, ask: true), "a long ask");
            var camp = new SimCore(new SimConfig { ecology = true }, SimCore.NewWorld(3, new SimConfig { ecology = true }));
            CollectionAssert.Contains(ContentRules.MissingThings("Mira fell in the well.", camp), "the well", "a gated thing the box lacks");
            CollectionAssert.Contains(ContentRules.MissingThings("Mira walked to the market.", camp), "the market");
        }

        /// <summary>Batch 3 findings: "the children" needs two kids, "mud" needs water, "bench" is never true, "broom" needs sweepers.</summary>
        [Test]
        public void KidsMudBenchAndBroomAreGated()
        {
            var cfg = new SimConfig { ecology = true };
            var camp = new SimCore(cfg, SimCore.NewWorld(3, cfg));
            Assert.AreEqual(0, camp.KidCount);
            CollectionAssert.Contains(ContentRules.MissingThings("The children tried to catch it.", camp), "two or more kids");
            CollectionAssert.Contains(ContentRules.MissingThings("Mira lost a sandal in the mud of the hollow.", camp), "water for mud");
            CollectionAssert.Contains(ContentRules.MissingThings("They no longer share a bench.", camp), "a bench");
            CollectionAssert.Contains(ContentRules.MissingThings("Someone danced with a broom.", camp), "a broom");
            camp.DebugAddKids(1);
            CollectionAssert.Contains(ContentRules.MissingThings("The children tried to catch it.", camp), "two or more kids", "one kid is not children");
            camp.DebugAddKids(1);
            Assert.IsEmpty(ContentRules.MissingThings("The children tried to catch it.", camp));
            Assert.IsEmpty(ContentRules.MissingThings("They no longer share a fire. Someone danced with a ladle.", camp));
        }

        /// <summary>#652: a box with no water never says "stream"; the slice (always a pond) and a land with water may.</summary>
        [Test]
        public void ADryBoxNeverSaysStreamAndAWetOneMay()
        {
            var cfg = new SimConfig { ecology = true };
            var dry = new SimCore(cfg, SimCore.NewWorld(3, cfg));
            Assert.IsFalse(dry.OpenWater, "a fresh living land holds no water");
            CollectionAssert.Contains(ContentRules.MissingThings("Mira lost a sandal crossing the stream.", dry), "the stream");
            CollectionAssert.Contains(ContentRules.MissingThings("Mira found a way across the stream.", dry), "the stream");
            CollectionAssert.Contains(ContentRules.MissingThings("A dry summer: the stream ran low.", dry), "the stream");
            var slice = new SimCore(new SimConfig(), SimCore.NewWorld(3, new SimConfig()));
            Assert.IsTrue(slice.OpenWater);
            Assert.IsEmpty(ContentRules.MissingThings("Mira lost a sandal crossing the stream.", slice), "the slice has its stream");
            // the sandal's loss on the living land is written from the land: never "stream", with or without water
            for (int seed = 1; seed <= 4; seed++)
            {
                var sim = new SimCore(cfg, SimCore.NewWorld(seed, cfg));
                sim.Chronicled += e => { if (!sim.OpenWater) StringAssert.DoesNotContain("stream", e.text.ToLowerInvariant(), $"seed {seed}: {e.text}"); };
                for (int y = 0; y < 30; y++) { sim.Advance(1); if (sim.State.eraReady) sim.AdvanceEra(); }
            }
        }

        /// <summary>2026-11-16 "Bring a pen back": "goat" is no longer a thing the box never has; it is said only in a box with the pond
        /// family's pen (as "stream" only with water).</summary>
        [Test]
        public void ABoxWithoutThePenNeverSaysGoatAndOneWithItMay()
        {
            CollectionAssert.DoesNotContain(ContentRules.NotInTheBox, "goat");
            CollectionAssert.DoesNotContain(ContentRules.NotInTheBox, "goats");
            Assert.IsEmpty(ContentRules.Problems("They moved in with a goat.", LineVoice.Narrator), "a goat is not banned outright");
            var cfg = new SimConfig { ecology = true };
            var bare = new SimCore(cfg, SimCore.NewWorld(3, cfg));
            Assert.IsFalse(bare.HasGoatPen);
            CollectionAssert.Contains(ContentRules.MissingThings("They penned their goat beside it.", bare), "the goat pen");
            CollectionAssert.Contains(ContentRules.MissingThings("Two goats ate the washing.", bare), "the goat pen");
            CollectionAssert.Contains(ContentRules.MissingThings("The pond family's goat has chewed the pen's corner again.", bare), "the goat pen");
            CollectionAssert.Contains(ContentRules.MissingThings("The goat's rope came loose.", bare), "the goat pen");
            Assert.IsEmpty(ContentRules.MissingThings("Mira lost a basket by the boat.", bare), "nothing about a goat");
            // the pond family's "a home here" brings the pen: the words may be said from its own line on
            var def = DilemmaDefs.All.Single(d => d.id == "village_hill_family");
            int home = System.Array.FindIndex(def.options, o => o.label == "a home here");
            SimCore sim = null;
            for (int s = 1; s <= 12 && sim == null; s++) sim = DilemmaContentTests.RaisedWithin40Years(def, s);
            Assert.IsNotNull(sim);
            var said = new List<string>();
            sim.Chronicled += e => said.AddRange(ContentRules.MissingThings(e.text, sim));
            Assert.IsTrue(sim.AnswerDilemma(sim.OpenDilemma.id, home, DilemmaPath.Card));
            CollectionAssert.DoesNotContain(said, "the goat pen", "the answer's own line has its pen");
            Assert.IsTrue(sim.HasGoatPen);
            Assert.IsEmpty(ContentRules.MissingThings("They penned their goat beside it.", sim));
            Assert.IsEmpty(ContentRules.MissingThings("The pond family's goat has chewed the pen's corner again.", sim));
        }

        [Test]
        public void EveryChoiceCallbackOfficeAndBankLineFollowsTheRules()
        {
            var found = new List<(string, string, List<string>)>();
            foreach (var d in DilemmaDefs.All.Concat(SmallChoices.All).Concat(LifeChoices.All))
            {
                foreach (var q in new[] { d.question }.Concat(d.askedBy?.Values ?? Enumerable.Empty<string>()))
                    found.Add((d.id + " ask", q, ContentRules.Problems(DilemmaContentTests.Fill(q), LineVoice.People, ask: true)));
                foreach (var o in d.options)
                {
                    foreach (var t in new[] { o.label, o.hint, o.touch })
                        found.Add((d.id + " option", t, ContentRules.Problems(DilemmaContentTests.Fill(t), LineVoice.People)));
                    found.Add((d.id + " done", o.done, ContentRules.Problems(DilemmaContentTests.Fill(o.done), LineVoice.Narrator)));
                    foreach (var c in o.callbacks ?? new DilemmaCallbackDef[0])
                        foreach (var t in new[] { c.text, c.fallback })
                            found.Add((d.id + " callback", t, ContentRules.Problems(DilemmaContentTests.Fill(t), LineVoice.Narrator)));
                }
                foreach (var t in d.traitHints?.Values ?? Enumerable.Empty<string>())
                    found.Add((d.id + " hint", t, ContentRules.Problems(DilemmaContentTests.Fill(t), LineVoice.People)));
            }
            foreach (var t in SimCore.AllTermText())
                found.Add(("office", t, ContentRules.Problems(SimCore.TermLineText(t ?? ""), LineVoice.Narrator)));
            foreach (var l in SimCore.LineBank)
                found.Add(($"bank {l.no}", l.text, ContentRules.Problems(l.text, l.quote ? LineVoice.People : LineVoice.Narrator, l.quote ? l.stage : 0)));
            foreach (var k in RitualKind.All)
                found.Add(("ritual caption", RitualKind.Caption(k), ContentRules.Problems(RitualKind.Caption(k), LineVoice.Narrator)));
            var bad = Report(found);
            Assert.IsEmpty(bad, string.Join("\n", bad));
        }

        /// <summary>Every chronicle line written over 40 years on both lands (two seeds each, every ritual kept, the eras advanced), checked
        /// as it is written: the rules, and the gated things the box must have right then. The quotes the people say with them too.</summary>
        [Test]
        public void EveryChronicleLineOnBothLandsIsTrueToTheBoxAsItIsWritten()
        {
            var watch = Stopwatch.StartNew();
            var found = new List<(string, string, List<string>)>();
            foreach (bool living in new[] { false, true })
                foreach (int seed in new[] { 1, 2 })
                {
                    var cfg = new SimConfig { dilemmas = true, ecology = living };
                    var sim = new SimCore(cfg, SimCore.NewWorld(seed, cfg));
                    sim.State.customs ??= new List<string>();
                    foreach (var k in RitualKind.All) sim.State.customs.Add(RitualKind.CustomOf(k));
                    string where = (living ? "living" : "slice") + " seed " + seed;
                    sim.Chronicled += e =>
                    {
                        var problems = ContentRules.Problems(e.text, LineVoice.Narrator);
                        problems.AddRange(ContentRules.MissingThings(e.text, sim).Select(t => "needs " + t));
                        found.Add(($"{where} {e.kind} y{e.month / 12}", e.text, problems));
                    };
                    for (int y = 0; y < 40; y++) { sim.Advance(1); if (sim.State.eraReady) sim.AdvanceEra(); }
                    foreach (var e in sim.State.chronicle.Where(x => !string.IsNullOrEmpty(x.quote)))
                        found.Add(($"{where} quote", e.quote, ContentRules.Problems(e.quote, LineVoice.People)));
                }
            var bad = Report(found);
            Assert.IsEmpty(bad, string.Join("\n", bad));
            TestContext.WriteLine($"{found.Count} lines in {watch.Elapsed.TotalSeconds:0.0} s");
        }
    }
}
