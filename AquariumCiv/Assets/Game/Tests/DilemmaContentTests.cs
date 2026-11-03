using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using AquariumCiv.Sim;
using NUnit.Framework;
using static AquariumCiv.Tests.SimCoreTests;

namespace AquariumCiv.Tests
{
    /// <summary>The eight dilemmas' content (P7, #222): four camp and four village, two of each kind; every def is raised in some seed
    /// within 40 game years; every option changes something and comes back (1-2 callbacks, 4-15 years); the words fit a phone, carry no
    /// numbers or the player's words for the box ("glass", "tank"...) in the people's mouths; no two questions are the same; every text resolves with no placeholder
    /// left. Pure C#, so it also runs in Tools/SimTests.</summary>
    public class DilemmaContentTests
    {
        const int QuestionMax = 60, LabelMax = 24, HintMax = 70, DoneMax = 120, CallbackMax = 140;
        const string Name = "Greta", Place = "Westmere"; // a typical name and place, for the length checks

        static IEnumerable<DilemmaDef> Defs => DilemmaDefs.All;

        static string Fill(string t) => (t ?? "").Replace("{a}", Name).Replace("{b}", Name).Replace("{p}", Name).Replace("{q}", Name).Replace("{place}", Place);

        static IEnumerable<string> Questions(DilemmaDef d) => new[] { d.question }.Concat(d.askedBy?.Values ?? Enumerable.Empty<string>());

        static IEnumerable<string> Hints(DilemmaDef d) => d.options.Select(o => o.hint).Concat(d.traitHints?.Values ?? Enumerable.Empty<string>());

        /// <summary>Everything the people say: questions, labels, hints.</summary>
        static IEnumerable<string> PeoplesWords(DilemmaDef d) => Questions(d).Concat(Hints(d)).Concat(d.options.Select(o => o.label));

        static IEnumerable<string> AllText(DilemmaDef d) => PeoplesWords(d).Concat(d.options.Select(o => o.done))
            .Concat(d.options.SelectMany(o => o.callbacks ?? new DilemmaCallbackDef[0]).SelectMany(c => new[] { c.text, c.fallback }));

        // ---------- the set ----------

        [Test]
        public void FourCampAndFourVillageTwoOfEachKind()
        {
            Assert.AreEqual(8, Defs.Count());
            Assert.AreEqual(Defs.Count(), Defs.Select(d => d.id).Distinct().Count(), "ids are unique");
            Assert.AreEqual(4, Defs.Count(d => d.minEra == Era.Camp && d.maxEra == Era.Camp));
            Assert.AreEqual(4, Defs.Count(d => d.minEra == Era.Village && d.maxEra == Era.Village));
            foreach (DilemmaKind k in System.Enum.GetValues(typeof(DilemmaKind)))
            {
                Assert.AreEqual(2, Defs.Count(d => d.kind == k), k.ToString());
                Assert.AreEqual(1, Defs.Count(d => d.kind == k && d.minEra == Era.Camp), $"{k} in the camp");
            }
            Assert.IsFalse(Defs.Any(d => d.id.StartsWith("ph_")), "the placeholders are gone");
        }

        [Test]
        public void EveryOptionChangesSomethingAndComesBack()
        {
            foreach (var d in Defs)
            {
                Assert.AreEqual(2, d.options.Length, d.id);
                Assert.AreEqual(d.kind == DilemmaKind.Who, d.cast == DilemmaCast.TwoCandidates, $"{d.id}: only a who dilemma is about two people");
                var ids = new HashSet<string>();
                foreach (var o in d.options)
                {
                    string at = $"{d.id}/{o.label}";
                    Assert.IsTrue(o.takesRole || o.lean.HasValue || o.strangers > 0 || !string.IsNullOrEmpty(o.custom), $"{at}: changes something");
                    if (d.kind == DilemmaKind.Who) Assert.IsTrue(o.takesRole && o.ties == DilemmaTies.RivalOrGrace, $"{at}: the pick takes the seat");
                    if (d.kind == DilemmaKind.Tradition) Assert.IsFalse(string.IsNullOrEmpty(o.custom), $"{at}: a tradition is a custom");
                    if (d.kind == DilemmaKind.Strangers) Assert.IsTrue(d.options.Any(x => x.strangers > 0), $"{d.id}: someone can arrive");
                    Assert.IsNotNull(o.callbacks, at);
                    Assert.That(o.callbacks.Length, Is.InRange(1, 2), $"{at}: 1-2 callbacks");
                    foreach (var c in o.callbacks)
                    {
                        Assert.IsTrue(ids.Add(c.id), $"{at}: callback id {c.id} is unique");
                        Assert.That(c.minYears, Is.InRange(4, 15), $"{at}/{c.id}");
                        Assert.That(c.maxYears, Is.InRange(c.minYears, 15), $"{at}/{c.id}");
                        Assert.IsFalse(string.IsNullOrEmpty(c.text), $"{at}/{c.id}");
                    }
                    if (d.kind != DilemmaKind.Who) Assert.IsTrue(o.favouredBy != null && o.favouredBy.Length > 0, $"{at}: a lapse can lean to it by the asker's trait");
                }
                // Both good: neither option stacks two economy effects over the other (a lean is food or wood: different, not more).
                // (strangers who live apart are not the settlement's people: no economy effect)
                var econ = d.options.Select(o => (o.lean.HasValue ? 1 : 0) + (o.strangers > 0 && string.IsNullOrEmpty(o.apart) ? 1 : 0)).ToArray();
                Assert.LessOrEqual(System.Math.Abs(econ[0] - econ[1]), 1, d.id);
                Assert.IsTrue(econ.All(x => x <= 1), $"{d.id}: one economy effect at most per option");
                if (d.options.All(o => o.lean.HasValue)) Assert.AreNotEqual(d.options[0].lean, d.options[1].lean, $"{d.id}: the two ways lean differently");
            }
        }

        // ---------- the words ----------

        [Test]
        public void TextFitsAPhone()
        {
            foreach (var d in Defs)
            {
                foreach (var q in Questions(d)) Assert.LessOrEqual(Fill(q).Length, QuestionMax, $"{d.id}: \"{q}\"");
                foreach (var h in Hints(d)) Assert.LessOrEqual(Fill(h).Length, HintMax, $"{d.id}: \"{h}\"");
                foreach (var o in d.options)
                {
                    Assert.That(Fill(o.label).Length, Is.InRange(1, LabelMax), $"{d.id}: \"{o.label}\"");
                    Assert.That(Fill(o.done).Length, Is.InRange(1, DoneMax), $"{d.id}: \"{o.done}\"");
                    Assert.IsFalse(string.IsNullOrEmpty(o.hint), $"{d.id}/{o.label}: a hint");
                    foreach (var c in o.callbacks)
                    {
                        // told after "Because the Keeper chose <label>, ": it reads on from a comma
                        foreach (var t in new[] { c.text, c.fallback }.Where(x => !string.IsNullOrEmpty(x)))
                        {
                            Assert.LessOrEqual(Fill(t).Length, CallbackMax, $"{d.id}/{c.id}: \"{t}\"");
                            Assert.IsTrue(t[0] == '{' || char.IsLower(t[0]), $"{d.id}/{c.id}: reads on after the credit: \"{t}\"");
                            Assert.IsTrue(t.EndsWith(".") || t.EndsWith("!"), $"{d.id}/{c.id}: a whole sentence");
                        }
                    }
                }
            }
        }

        [Test]
        public void NoNumbersAndNoPlayersWordsInThePeoplesMouths()
        {
            foreach (var d in Defs)
            {
                foreach (var t in PeoplesWords(d))
                {
                    Assert.IsFalse(Regex.IsMatch(t, "[0-9]"), $"{d.id}: never how much: \"{t}\"");
                    // chronicle-and-history Choice 10 / §5.1: the people never name the box in the player's words; they say what their
                    // era believes (camp: the sky wall, the Bright One; village: the Keeper's window). Narrator lines may say "tank".
                    var m = Regex.Match(t.ToLowerInvariant(), @"\b(glass|tank|box|aquarium|player|phone|game)\b");
                    Assert.IsFalse(m.Success, $"{d.id}: the people never say \"{m.Value}\": \"{t}\"");
                }
                foreach (var q in Questions(d)) Assert.IsTrue(q.TrimEnd().EndsWith("?"), $"{d.id}: a question: \"{q}\"");
            }
        }

        /// <summary>The people live inside the box: nobody comes from, or goes back to, anywhere outside it (user, 2026-11-02).</summary>
        /// The one way in is the user's fiction (#231): newcomers wandered in from a far-away tank, so "far-away" is allowed only before "tank".
        static readonly string[] OutsideWords = { "over the hill", "beyond", "outside", "abroad", "other lands", "the road", "came back through", "the world" };

        static bool GoesOutside(string text, out string word)
        {
            string t = text.ToLowerInvariant();
            word = OutsideWords.FirstOrDefault(t.Contains);
            if (word != null) return true;
            var m = Regex.Match(t, "far[- ]?away(?! tank)");
            word = m.Success ? m.Value : null;
            return m.Success;
        }

        [Test]
        public void NothingComesFromOrGoesOutsideTheBox()
        {
            foreach (var d in Defs)
                foreach (var t in AllText(d).Where(x => !string.IsNullOrEmpty(x)))
                    Assert.IsFalse(GoesOutside(t, out var w), $"{d.id}: \"{w}\" in \"{t}\"");
        }

        [Test]
        public void NoTwoQuestionsAreTheSame()
        {
            var all = Defs.SelectMany(d => Questions(d).Select(q => (d.id, q))).ToList();
            var dupes = all.GroupBy(x => x.q).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
            CollectionAssert.IsEmpty(dupes);
            Assert.AreEqual(Defs.Count(), Defs.Select(d => d.question).Distinct().Count());
        }

        [Test]
        public void PlaceholdersAndTraitsAreRealOnes()
        {
            var traits = new HashSet<string>(NameData.Traits);
            foreach (var d in Defs)
            {
                bool who = d.cast == DilemmaCast.TwoCandidates;
                foreach (var t in AllText(d).Where(x => !string.IsNullOrEmpty(x)))
                    foreach (Match m in Regex.Matches(t, "{[^}]*}"))
                    {
                        var ok = new List<string> { "{place}" };
                        if (who) ok.AddRange(new[] { "{a}", "{b}", "{p}", "{q}" });
                        Assert.Contains(m.Value, ok, $"{d.id}: \"{t}\"");
                    }
                if (d.traitHints != null)
                {
                    Assert.IsTrue(who, $"{d.id}: trait hints are for two people");
                    CollectionAssert.AreEquivalent(traits, d.traitHints.Keys, $"{d.id}: a line for every trait");
                    foreach (var h in d.traitHints.Values) StringAssert.StartsWith("{p}", h, d.id);
                }
                foreach (var k in d.askedBy?.Keys ?? Enumerable.Empty<string>()) Assert.IsTrue(traits.Contains(k), $"{d.id}: {k}");
                foreach (var o in d.options)
                    foreach (var k in o.favouredBy ?? new string[0]) Assert.IsTrue(traits.Contains(k), $"{d.id}: {k}");
                if (who)
                    for (int i = 0; i < 2; i++)
                    {
                        string me = i == 0 ? "{a}" : "{b}", other = i == 0 ? "{b}" : "{a}";
                        Assert.AreEqual(me, d.options[i].label, d.id);
                        foreach (var c in d.options[i].callbacks)
                        {
                            if (c.text.Contains(me) && !c.text.Contains(other)) Assert.IsTrue(c.aliveA == (i == 0) || c.aliveB == (i == 1), $"{d.id}/{c.id}: about the chosen one, alive");
                            if (c.text.Contains(other)) Assert.IsTrue(i == 0 ? c.aliveB : c.aliveA, $"{d.id}/{c.id}: about the other one, alive");
                        }
                    }
            }
        }

        // ---------- in a box ----------

        static void Month(SimCore sim, DilemmaDef def)
        {
            sim.Advance(1.0 / 12);
            if (sim.State.eraReady && sim.State.era < def.minEra) sim.AdvanceEra(); // the player watches the era moment
        }

        /// <summary>A fresh box with only this def: the month it is raised within 40 game years, or null.</summary>
        static SimCore RaisedWithin40Years(DilemmaDef def, int seed)
        {
            var sim = NewSim(seed, new SimConfig { dilemmas = true });
            sim.DilemmaCatalog = new[] { def };
            for (int m = 0; m < 40 * 12; m++)
            {
                Month(sim, def);
                if (sim.State.dilemmas.Count > 0) return sim;
            }
            return null;
        }

        [Test]
        public void EveryDefIsRaisedInSomeSeedWithin40YearsAndEveryTextResolves()
        {
            foreach (var def in Defs)
            {
                int seed = 0;
                SimCore sim = null;
                for (int s = 1; s <= 12 && sim == null; s++) { sim = RaisedWithin40Years(def, s); seed = s; }
                Assert.IsNotNull(sim, $"{def.id}: raised in no seed within 40 years");
                var d = sim.State.dilemmas[0];
                Assert.AreEqual(def.id, d.defId);
                Assert.That(sim.State.era, Is.InRange(def.minEra, def.maxEra), def.id);
                var v = sim.OpenDilemmaView;
                Assert.IsNotNull(v, def.id);
                foreach (var t in new[] { v.question, v.options[0].label, v.options[0].hint, v.options[1].label, v.options[1].hint })
                {
                    StringAssert.DoesNotContain("{", t, $"{def.id}: \"{t}\"");
                    Assert.IsTrue(char.IsUpper(t[0]) || t == v.options[0].label || t == v.options[1].label, $"{def.id}: \"{t}\"");
                }
                if (def.cast == DilemmaCast.TwoCandidates) Assert.IsTrue(d.personA >= 0 && d.personB >= 0, def.id);

                // both options, on the same box: the effect lands and every callback (or its fallback) reads whole
                for (int opt = 0; opt < 2; opt++)
                {
                    var box = opt == 0 ? sim : RaisedWithin40Years(def, seed);
                    var dd = box.State.dilemmas[0];
                    var o = def.options[opt];
                    int people = box.State.notables.Count, pop = box.State.population;
                    Assert.IsTrue(box.AnswerDilemma(dd.id, opt, DilemmaPath.Card), def.id);
                    var line = box.State.chronicle.Single(e => e.id == dd.answerEntryId).text;
                    foreach (var told in box.State.chronicle.Where(x => x.month == box.State.month))
                        Assert.IsFalse(GoesOutside(told.text, out var w), $"{def.id}: what the answer told: \"{told.text}\"");
                    StringAssert.DoesNotContain("{", line, def.id);
                    if (o.takesRole) Assert.AreEqual(dd.PersonOf(opt), box.HolderOf(SimCore.LeaderOf(box.State.era))?.id, $"{def.id}: the pick holds the seat");
                    if (!string.IsNullOrEmpty(o.custom)) Assert.IsTrue(box.HasCustom(o.custom), def.id);
                    if (o.strangers > 0 && string.IsNullOrEmpty(o.apart)) Assert.AreEqual(people + o.strangers, box.State.notables.Count, $"{def.id}: the strangers arrive");
                    if (!string.IsNullOrEmpty(o.apart))
                    {
                        var g = box.ApartOf(dd);
                        Assert.IsNotNull(g, $"{def.id}: they live apart");
                        Assert.AreEqual(o.apart, g.kind);
                        Assert.AreEqual(o.strangers, g.members.Count);
                        Assert.AreEqual(people, box.State.notables.Count, $"{def.id}: not the settlement's people");
                        Assert.AreEqual(pop, box.State.population, def.id);
                    }
                    if (o.lean.HasValue) Assert.IsTrue(box.State.dilemmaLeans.Any(l => l.lean == (int)o.lean.Value), def.id);
                    Assert.AreEqual(o.callbacks.Length, box.PendingCallbacks().Count, def.id);
                    ChronicleEntry e;
                    while ((e = box.DebugFireCallbackNow()) != null)
                    {
                        StringAssert.StartsWith($"Because the Keeper chose {box.ViewOfDilemma(dd).options[opt].label}, ", e.text, def.id);
                        StringAssert.DoesNotContain("{", e.text, def.id);
                        StringAssert.DoesNotContain("..", e.text, def.id);
                    }
                }
            }
        }
    }
}
