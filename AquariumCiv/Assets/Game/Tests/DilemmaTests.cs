using System.Collections.Generic;
using System.Linq;
using AquariumCiv.Sim;
using NUnit.Framework;
using UnityEngine;
using static AquariumCiv.Tests.SimCoreTests;

namespace AquariumCiv.Tests
{
    /// <summary>The Keeper's dilemmas (P7, #220): raised in the expected window and never beside a wish or in the first run, each effect
    /// kind applies, a lapse picks by trait and by hash the same way every time, catch-up equals live stepping, and with the switch
    /// off nothing changes. Pure C# (JsonUtility only), so it also runs in Tools/SimTests.</summary>
    public class DilemmaTests
    {
        static SimConfig Cfg(bool lab = false)
        {
            var c = new SimConfig { dilemmas = true, dilemmaLab = lab };
            return c;
        }

        static void Years(SimCore sim, double years) { for (int m = 0; m < (int)(years * 12 + 0.5); m++) sim.Advance(1.0 / 12); }

        /// <summary>Two defs of every effect kind, so the tests don't depend on the placeholder text.</summary>
        static DilemmaDef WhoDef(string id, string lapseTraitA = null, string lapseTraitB = null) => new DilemmaDef
        {
            id = id, kind = DilemmaKind.Who, cast = DilemmaCast.TwoCandidates, role = DilemmaDef.Leader, replacesHolder = true,
            question = "{a} or {b}?",
            options = new[]
            {
                new DilemmaOptionDef { label = "{a}", hint = "h", takesRole = true, ties = DilemmaTies.RivalOrGrace, custom = "who:a", favouredBy = lapseTraitA == null ? null : new[] { lapseTraitA } },
                new DilemmaOptionDef { label = "{b}", hint = "h", takesRole = true, ties = DilemmaTies.RivalOrGrace, custom = "who:b", favouredBy = lapseTraitB == null ? null : new[] { lapseTraitB } },
            },
        };

        static DilemmaDef StrangersDef(string id) => new DilemmaDef
        {
            id = id, kind = DilemmaKind.Strangers, cast = DilemmaCast.None, question = "Travellers ask to join.",
            options = new[]
            {
                new DilemmaOptionDef { label = "let them in", hint = "h", strangers = 3 },
                new DilemmaOptionDef { label = "send them on", hint = "h" },
            },
        };

        /// <summary>A camp with two free adults who could take the lead, found by looking over seeds.</summary>
        static SimCore WithCandidates(int seed, SimConfig cfg, params DilemmaDef[] defs)
        {
            var sim = NewSim(seed, cfg);
            sim.DilemmaCatalog = defs;
            Years(sim, 3);
            return sim;
        }

        static IEnumerable<SimCore> Camps(SimConfig cfg, DilemmaDef def, int want = 8)
        {
            int found = 0;
            for (int seed = 1; seed < 60 && found < want; seed++)
            {
                var sim = WithCandidates(seed, cfg, def);
                if (sim.State.era != Era.Camp || sim.RoleCandidates(RoleType.Chief).Count < 2) continue;
                found++;
                yield return sim;
            }
            Assert.GreaterOrEqual(found, 1, "no seed gave a camp with two candidates");
        }

        // ---------- pacing ----------

        [Test]
        public void NormalPlayRaisesTheFirstWithinTheWindowOver20Seeds()
        {
            for (int seed = 1; seed <= 20; seed++)
            {
                var sim = NewSim(seed, Cfg());
                Years(sim, 20);
                var first = sim.State.dilemmas.FirstOrDefault();
                Assert.IsNotNull(first, $"seed {seed}: none in 14 years");
                // due 6 to 10 years on
                Assert.GreaterOrEqual(first.month, 6 * 12, $"seed {seed}");
                Assert.LessOrEqual(first.month, 10 * 12 + 24 + 8, $"seed {seed}"); // a wish open at the time is waited out (at most 24 months), then the gap
            }
        }

        [Test]
        public void NormalPlayIsAboutOnePerSixToTenYears()
        {
            int total = 0;
            for (int seed = 1; seed <= 10; seed++)
            {
                var sim = NewSim(seed, Cfg());
                sim.DilemmaCatalog = Enumerable.Range(0, 20).Select(i => new DilemmaDef
                {
                    id = "w" + i, kind = DilemmaKind.WhichWay, cast = DilemmaCast.None, question = "q",
                    options = new[] { new DilemmaOptionDef { label = "x", hint = "h" }, new DilemmaOptionDef { label = "y", hint = "h" } },
                }).ToArray();
                Years(sim, 40);
                int n = sim.State.dilemmas.Count;
                Assert.GreaterOrEqual(n, 3, $"seed {seed}: {n} in 40 years");
                Assert.LessOrEqual(n, 7, $"seed {seed}: {n} in 40 years");
                total += n;
            }
            Assert.Greater(total, 0);
        }

        [Test]
        public void LabScheduleStartsAtYearTwoThenEveryAboutThree()
        {
            for (int seed = 1; seed <= 6; seed++)
            {
                var sim = NewSim(seed, Cfg(true));
                sim.DilemmaCatalog = Enumerable.Range(0, 20).Select(i => StrangersDef("s" + i)).ToArray();
                Years(sim, 20);
                var ds = sim.State.dilemmas;
                Assert.GreaterOrEqual(ds.Count, 3, $"seed {seed}");
                Assert.GreaterOrEqual(ds[0].month, 24, $"seed {seed}");
                Assert.LessOrEqual(ds[0].month, 24 + 24 + 8, $"seed {seed}: year 2, or by year 4 when a wish was open");
                for (int i = 1; i < ds.Count; i++)
                {
                    long gap = ds[i].month - ds[i - 1].month;
                    Assert.GreaterOrEqual(gap, 30, $"seed {seed} #{i}");
                    Assert.LessOrEqual(gap, 36 + 6 + 24 + 8, $"seed {seed} #{i}");
                }
            }
        }

        [Test]
        public void NeverBesideAWishAndAGapFollowsEach()
        {
            for (int seed = 1; seed <= 8; seed++)
            {
                var sim = NewSim(seed, Cfg(true));
                sim.DilemmaCatalog = Enumerable.Range(0, 20).Select(i => StrangersDef("s" + i)).ToArray();
                for (int m = 0; m < 16 * 12; m++)
                {
                    sim.Advance(1.0 / 12);
                    Assert.IsFalse(sim.OpenWish != null && sim.OpenDilemma != null, $"seed {seed} month {sim.State.month}: a wish and a dilemma together");
                    Assert.LessOrEqual(sim.State.dilemmas.Count(d => d.state == DilemmaState.Open), 1);
                }
                Assert.Greater(sim.State.wishes.Count, 0, $"seed {seed}: wishes still come");
                foreach (var d in sim.State.dilemmas)
                {
                    long dEnd = d.endMonth >= 0 ? d.endMonth : sim.State.month;
                    foreach (var w in sim.State.wishes)
                    {
                        long wEnd = w.endMonth >= 0 ? w.endMonth : sim.State.month;
                        Assert.IsFalse(w.month < dEnd && d.month < wEnd, $"seed {seed}: dilemma {d.id} and wish {w.id} overlap");
                        if (w.month >= dEnd) // the next wish after a dilemma waits the gap
                        {
                            var between = sim.State.wishes.Where(x => x.month >= dEnd && x.month < w.month).Count();
                            var nextDilemma = sim.State.dilemmas.Where(x => x.month > d.month).Select(x => x.month).DefaultIfEmpty(long.MaxValue).Min();
                            if (between == 0 && w.month < nextDilemma) Assert.GreaterOrEqual(w.month - dEnd, SimCore.WishGapMonths, $"seed {seed}: wish {w.id} after dilemma {d.id}");
                        }
                    }
                }
            }
        }

        [Test]
        public void ADueDilemmaNeverLapsAnOpenWishButWaitsForItsEnd()
        {
            for (int seed = 1; seed <= 12; seed++)
            {
                var sim = NewSim(seed, Cfg(true));
                sim.DilemmaCatalog = Enumerable.Range(0, 20).Select(i => StrangersDef("s" + i)).ToArray();
                Years(sim, 1);
                var w = sim.DebugForceWish();
                Assert.IsNotNull(w);
                sim.State.dilemmaNextMonth = sim.State.month + 1; // due while the wish is open
                for (int m = 0; m < 20; m++)
                {
                    sim.Advance(1.0 / 12);
                    if (w.state != WishState.Open) { Assert.IsTrue(sim.FindNotable(w.notableId)?.alive != true || sim.State.nudges.Any(), $"seed {seed}: the wish ended without cause"); break; }
                    Assert.IsNull(sim.OpenDilemma, $"seed {seed}: raised beside the open wish");
                }

                if (w.state == WishState.Open) { w.state = WishState.Given; w.endMonth = sim.State.month; } // the player gives the gift
                long ended = w.endMonth;
                Years(sim, 2);
                var first = sim.State.dilemmas.FirstOrDefault();
                Assert.IsNotNull(first, $"seed {seed}");
                Assert.GreaterOrEqual(first.month - ended, SimCore.DilemmaGapMonths, $"seed {seed}: the gap runs from the wish's end");
                Assert.IsFalse(sim.State.wishes.Any(x => x.id != w.id && x.month >= ended && x.month < first.month), $"seed {seed}: no new wish jumps the queue");
            }
        }

        [Test]
        public void NeverDuringTheFirstRun()
        {
            var sim = NewSim(3, Cfg(true));
            sim.State.firstRun.active = true;
            Years(sim, 8);
            Assert.AreEqual(0, sim.State.dilemmas.Count, "none while the first run is active");
            long endedAt = sim.State.month;
            sim.State.firstRun.active = false;
            Years(sim, 6);
            Assert.GreaterOrEqual(sim.State.dilemmas.Count, 1);
            Assert.GreaterOrEqual(sim.State.dilemmas[0].month, endedAt);
        }

        // ---------- answering and effects ----------

        [Test]
        public void AnsweringAWhoDilemmaSeatsThePersonAndWritesAKeeperLine()
        {
            foreach (var sim in Camps(Cfg(), WhoDef("w")))
            {
                var d = sim.DebugRaiseDilemma("w");
                Assert.IsNotNull(d);
                Assert.AreEqual(DilemmaState.Open, d.state);
                int chosen = d.personB, unchosen = d.personA;
                Assert.IsTrue(sim.AnswerDilemma(d.id, 1, DilemmaPath.Box));
                Assert.AreEqual(DilemmaState.Answered, d.state);
                Assert.AreEqual(1, d.option);
                Assert.AreEqual(DilemmaPath.Box, d.path);
                Assert.AreEqual(chosen, sim.HolderOf(RoleType.Chief).id, "the pick bypasses the succession roll");
                Assert.IsNull(sim.OpenDilemma);
                Assert.IsTrue(sim.HasCustom("who:b"));
                Assert.IsFalse(sim.HasCustom("who:a"));
                var line = sim.State.chronicle.Single(e => e.id == d.answerEntryId);
                Assert.AreEqual("keeper", line.kind);
                Assert.IsTrue(line.keeper);
                Assert.AreEqual(chosen, line.notableId);
                Assert.AreEqual(unchosen, line.otherNotableId);
                Assert.IsFalse(sim.AnswerDilemma(d.id, 0, DilemmaPath.Card), "no undo, no second answer");
                Assert.AreEqual(1, d.option);
            }
        }

        [Test]
        public void AnswerRejectsABadIdOptionOrAClosedDilemma()
        {
            var sim = NewSim(2, Cfg());
            sim.DilemmaCatalog = new[] { StrangersDef("s") };
            Years(sim, 1);
            var d = sim.DebugRaiseDilemma("s");
            Assert.IsNotNull(d);
            Assert.IsFalse(sim.AnswerDilemma(999, 0, DilemmaPath.Card));
            Assert.IsFalse(sim.AnswerDilemma(d.id, 2, DilemmaPath.Card));
            Assert.IsFalse(sim.AnswerDilemma(d.id, -1, DilemmaPath.Card));
            Assert.AreEqual(DilemmaState.Open, d.state);
            Assert.IsTrue(sim.AnswerDilemma(d.id, 1, DilemmaPath.Card));
            Assert.AreEqual(DilemmaPath.Card, d.path);
        }

        [Test]
        public void TheUnchosenGetsAGrudgeOrGraceByTrait()
        {
            int rivals = 0, graceful = 0, runs = 0;
            foreach (var proud in new[] { true, false })
                for (int seed = 1; seed < 40; seed++)
                {
                    var sim = WithCandidates(seed, Cfg(), WhoDef("w"));
                    if (sim.State.era != Era.Camp || sim.RoleCandidates(RoleType.Chief).Count < 2) continue;
                    var d = sim.DebugRaiseDilemma("w");
                    var chosen = sim.FindNotable(d.personA);
                    var other = sim.FindNotable(d.personB);
                    other.trait = proud ? "the stubborn" : "the kind";
                    other.rivalId = other.friendId = chosen.rivalId = chosen.friendId = -1;
                    other.partnerId = chosen.partnerId = -1;
                    Assert.IsTrue(sim.AnswerDilemma(d.id, 0, DilemmaPath.Box));
                    runs++;
                    if (other.rivalId == chosen.id)
                    {
                        Assert.IsTrue(proud, "only the proud hold a grudge");
                        Assert.AreEqual(other.id, chosen.rivalId);
                        rivals++;
                    }
                    else
                    {
                        graceful++;
                        Assert.IsTrue(other.friendId == chosen.id || sim.State.chronicle.Any(e => e.kind == "dilemma_grace" && e.notableId == other.id), "grace leaves a tie or a line");
                    }
                }
            Assert.Greater(runs, 4);
            Assert.Greater(rivals, 0, "a proud one sometimes becomes a rival");
            Assert.Greater(graceful, 0);
        }

        [Test]
        public void AWayDilemmaLeansForThirtySixMonthsAndMovesTheFood()
        {
            var cfg = Cfg();
            var def = DilemmaDefs.All.First(x => x.id == "ph_way");
            var withLean = NewSim(7, cfg); withLean.DilemmaCatalog = new[] { def };
            var without = NewSim(7, cfg); without.DilemmaCatalog = new[] { def };
            Years(withLean, 1); Years(without, 1);
            var d = withLean.DebugRaiseDilemma("ph_way");
            without.DebugRaiseDilemma("ph_way");
            Assert.IsTrue(withLean.AnswerDilemma(d.id, 0, DilemmaPath.Card)); // the fields: a food lean
            var lean = withLean.State.dilemmaLeans.Single();
            Assert.AreEqual((int)SimCore.Lean.Food, lean.lean);
            Assert.AreEqual(SimCore.NudgeMonths, lean.untilMonth - lean.fromMonth);
            float before = withLean.State.food, beforeNo = without.State.food;
            Assert.AreEqual(beforeNo, before, 1e-4f);
            withLean.Advance(1.0 / 12); without.Advance(1.0 / 12);
            Assert.Greater(withLean.State.food - before, without.State.food - beforeNo, "the lean moves what gets made");
            // the same lean expires: after 36 months a month's food matches the box with no lean
            Years(withLean, 3.2); Years(without, 3.2);
            float f1 = withLean.State.food, f2 = without.State.food;
            withLean.Advance(1.0 / 12); without.Advance(1.0 / 12);
            Assert.Greater(withLean.State.month, lean.untilMonth);
            Assert.AreEqual(withLean.State.food - f1, without.State.food - f2, 1e-3f * System.Math.Max(1f, f1), "no lean after its 36 months");
        }

        [Test]
        public void AStrangersDilemmaBringsAFamily()
        {
            var sim = NewSim(4, Cfg());
            sim.DilemmaCatalog = new[] { StrangersDef("s") };
            Years(sim, 2);
            int notables = sim.State.notables.Count, pop = sim.State.population, ids = sim.State.nextId;
            var d = sim.DebugRaiseDilemma("s");
            Assert.IsTrue(sim.AnswerDilemma(d.id, 0, DilemmaPath.Box));
            var fresh = sim.State.notables.Skip(notables).ToList();
            Assert.AreEqual(3, fresh.Count);
            Assert.AreEqual(1, fresh.Select(n => n.family).Distinct().Count(), "one family");
            Assert.IsTrue(fresh.All(n => n.alive && n.age >= 16f));
            Assert.AreEqual(pop + 3, sim.State.population);
            Assert.AreEqual(fresh.Select(n => n.name).Distinct().Count(), 3);
            Assert.IsTrue(sim.State.chronicle.Any(e => e.kind == "newcomer" && e.notableId == fresh[0].id));
            // the other option brings nobody
            var sim2 = NewSim(4, Cfg()); sim2.DilemmaCatalog = new[] { StrangersDef("s") };
            Years(sim2, 2);
            var d2 = sim2.DebugRaiseDilemma("s");
            sim2.AnswerDilemma(d2.id, 1, DilemmaPath.Box);
            Assert.AreEqual(notables, sim2.State.notables.Count);
        }

        // ---------- the card ----------

        [Test]
        public void ARaisedDilemmaIsACardOfItsOwnKindWhileOpen()
        {
            var sim = NewSim(5, Cfg());
            sim.DilemmaCatalog = new[] { StrangersDef("s") };
            Years(sim, 2);
            var d = sim.DebugRaiseDilemma("s");
            var stack = sim.ReturnCardsCapped(sim.State.month - 1, 5);
            Assert.AreEqual(1, stack.cards.Count(c => c.kind == CardKind.Dilemma));
            Assert.AreEqual(d.entryId, stack.cards.First(c => c.kind == CardKind.Dilemma).lead.id);
            sim.AnswerDilemma(d.id, 0, DilemmaPath.Card);
            var after = sim.ReturnCardsCapped(sim.State.month - 1, 5);
            Assert.AreEqual(0, after.cards.Count(c => c.kind == CardKind.Dilemma), "an answered dilemma stops being a card");
            Assert.IsTrue(after.cards.Any(c => c.kind == CardKind.Keeper), "its answer is a Keeper card");
        }

        [Test]
        public void TheDilemmaCardComesAfterTheEraCardAndNeverWithAWish()
        {
            var sim = NewSim(5, Cfg());
            sim.DilemmaCatalog = new[] { StrangersDef("s") };
            Years(sim, 2);
            sim.DebugForceWish();
            Assert.IsNotNull(sim.OpenWish);
            var d = sim.DebugRaiseDilemma("s"); // closes the wish
            Assert.IsNull(sim.OpenWish);
            sim.DebugForceWish();               // lapses the dilemma
            Assert.IsNull(sim.OpenDilemma);
            Assert.AreEqual(DilemmaState.Lapsed, d.state);
            var stack = sim.ReturnCardsCapped(sim.State.month - 1, 7);
            Assert.IsFalse(stack.cards.Any(c => c.kind == CardKind.Dilemma) && stack.cards.Any(c => c.kind == CardKind.Wish));
        }

        [Test]
        public void ViewGivesTheQuestionOptionsHintsAndPeople()
        {
            foreach (var sim in Camps(Cfg(), DilemmaDefs.All.First(x => x.id == "ph_leader"), 2))
            {
                var d = sim.DebugRaiseDilemma("ph_leader");
                var v = sim.OpenDilemmaView;
                Assert.AreSame(d, v.dilemma);
                Assert.AreEqual(DilemmaKind.Who, v.kind);
                Assert.AreEqual(2, v.options.Length);
                var a = sim.FindNotable(d.personA);
                var b = sim.FindNotable(d.personB);
                Assert.AreEqual(a.id, v.options[0].personId);
                Assert.AreEqual(b.id, v.options[1].personId);
                StringAssert.Contains(a.name, v.options[0].label);
                StringAssert.Contains(b.name, v.options[1].label);
                Assert.IsFalse(string.IsNullOrEmpty(v.options[0].hint));
                StringAssert.Contains(a.name, v.question);
                StringAssert.DoesNotContain("{", v.question + v.options[0].hint + v.options[1].label);
            }
        }

        // ---------- the lapse ----------

        [Test]
        public void SilenceLapsesAfter24MonthsWithANeutralLineAndTheEffectsApply()
        {
            var sim = NewSim(9, Cfg());
            sim.DilemmaCatalog = new[] { DilemmaDefs.All.First(x => x.id == "ph_way") };
            Years(sim, 1);
            var d = sim.DebugRaiseDilemma("ph_way");
            long raised = d.month;
            sim.Advance(23.0 / 12);
            Assert.AreEqual(DilemmaState.Open, d.state, "still open at 23 months");
            sim.Advance(1.0 / 12);
            Assert.AreEqual(raised + SimCore.DilemmaLapseMonths, sim.State.month);
            Assert.AreEqual(DilemmaState.Lapsed, d.state);
            Assert.AreEqual(-1, d.answerEntryId);
            var line = sim.State.chronicle.Single(e => e.id == d.resultEntryId);
            StringAssert.StartsWith("The people chose ", line.text);
            StringAssert.EndsWith(" themselves.", line.text);
            Assert.IsFalse(line.keeper);
            Assert.AreEqual(1, sim.State.dilemmaLeans.Count, "the people's pick still leans the box");
        }

        [Test]
        public void TheRoleHoldersTraitPicksTheLapse()
        {
            int checkedCount = 0;
            foreach (var kind in new[] { true, false })
                foreach (var sim in Camps(Cfg(), WhoDef("w", "the kind", "the brave"), 4))
                {
                    var chief = sim.HolderOf(RoleType.Chief);
                    if (chief == null) continue;
                    var d = sim.DebugRaiseDilemma("w");
                    var a = sim.FindNotable(d.personA); var b = sim.FindNotable(d.personB);
                    chief.trait = kind ? "the kind" : "the brave";
                    // strip the family and friend ties so the trait is what decides
                    foreach (var n in new[] { a, b }) { chief.friendId = chief.rivalId = chief.partnerId = -1; n.family = "Other"; n.parentId = -1; }
                    chief.family = "Mine"; chief.parentId = -1;
                    Assert.AreEqual(kind ? 0 : 1, sim.DilemmaDefaultOption(d), "the holder's trait leans the people");
                    Assert.IsTrue(sim.DebugLapseDilemma());
                    Assert.AreEqual(DilemmaState.Lapsed, d.state);
                    Assert.AreEqual(kind ? 0 : 1, d.option);
                    checkedCount++;
                }
            Assert.Greater(checkedCount, 2);
        }

        [Test]
        public void WithoutAHolderTheLapseIsASeededHashTheSameEveryTime()
        {
            var picks = new List<int>();
            for (int run = 0; run < 2; run++)
            {
                picks.Clear();
                for (int seed = 1; seed <= 20; seed++)
                {
                    var sim = NewSim(seed, Cfg());
                    sim.DilemmaCatalog = new[] { DilemmaDefs.All.First(x => x.id == "ph_way") };
                    Years(sim, 1);
                    var d = sim.DebugRaiseDilemma("ph_way");
                    sim.Advance(2.0);
                    Assert.AreEqual(DilemmaState.Lapsed, d.state);
                    picks.Add(d.option);
                }
                if (run == 0) first = picks.ToList();
            }
            CollectionAssert.AreEqual(first, picks, "the same seeds pick the same options");
            Assert.IsTrue(picks.Contains(0) && picks.Contains(1), "the hash goes both ways over 20 seeds");
        }
        static List<int> first;

        [Test]
        public void ADeadCandidateLapsesItAtOnceTowardTheLivingOne()
        {
            foreach (var sim in Camps(Cfg(), WhoDef("w"), 2))
            {
                var d = sim.DebugRaiseDilemma("w");
                sim.FindNotable(d.personA).alive = false;
                sim.Advance(1.0 / 12);
                Assert.AreEqual(DilemmaState.Lapsed, d.state);
                Assert.AreEqual(1, d.option, "the one who died cannot be chosen");
            }
        }

        // ---------- determinism and the switch ----------

        [Test]
        public void CatchUpEqualsLiveSteppingWithADilemmaOpen()
        {
            var a = NewSim(11, Cfg(true));
            var b = NewSim(11, Cfg(true));
            Years(a, 2.5);                 // live: month by month
            b.Advance(2.5);                // catch-up: in one go
            Assert.AreEqual(a.State.month, b.State.month);
            Assert.IsNotNull(a.OpenDilemma, "the lab's first is open by month 30");
            Assert.AreEqual(a.OpenDilemma.id, b.OpenDilemma.id);
            Assert.AreEqual(a.OpenDilemma.defId, b.OpenDilemma.defId);
            Assert.AreEqual(a.OpenDilemma.personA, b.OpenDilemma.personA);
            Assert.AreEqual(Fingerprint(a.State), Fingerprint(b.State));
            Assert.IsTrue(a.AnswerDilemma(a.OpenDilemma.id, 0, DilemmaPath.Box));
            Assert.IsTrue(b.AnswerDilemma(b.OpenDilemma.id, 0, DilemmaPath.Box));
            Years(a, 9); b.Advance(9);
            Assert.AreEqual(Fingerprint(a.State), Fingerprint(b.State));
            Assert.AreEqual(JsonUtility.ToJson(a.State.dilemmas), JsonUtility.ToJson(b.State.dilemmas));
            // and a lapse the same way
            var c = NewSim(12, Cfg(true)); var e = NewSim(12, Cfg(true));
            Years(c, 6); e.Advance(6);
            Assert.AreEqual(Fingerprint(c.State), Fingerprint(e.State));
            Assert.AreEqual(string.Join("|", c.State.dilemmas.Select(x => x.defId + x.month + x.option + x.state)), string.Join("|", e.State.dilemmas.Select(x => x.defId + x.month + x.option + x.state)));
            Assert.IsTrue(c.State.dilemmas.Any(x => x.state == DilemmaState.Lapsed));
        }

        [Test]
        public void DilemmasNeverDrawFromTheDiceStreams()
        {
            var on = NewSim(13, Cfg(true));
            var off = NewSim(13, new SimConfig());
            Years(on, 6); Years(off, 6);
            Assert.GreaterOrEqual(on.State.dilemmas.Count, 1);
            Assert.AreEqual(off.State.rng.state, on.State.rng.state);
            Assert.AreEqual(off.State.econRng.state, on.State.econRng.state);
            Assert.AreEqual(off.State.storyRng.state, on.State.storyRng.state);
        }

        [Test]
        public void OffByDefaultChangesNothing()
        {
            Assert.IsFalse(new SimConfig().dilemmas);
            var plain = NewSim(21);
            var labButOff = NewSim(21, new SimConfig { dilemmas = false, dilemmaLab = true });
            Years(plain, 30); Years(labButOff, 30);
            Assert.AreEqual(0, labButOff.State.dilemmas.Count);
            Assert.AreEqual(0, labButOff.State.dilemmaLeans.Count);
            Assert.AreEqual(-1, labButOff.State.dilemmaNextMonth);
            Assert.AreEqual(Fingerprint(plain.State), Fingerprint(labButOff.State));
            Assert.AreEqual(plain.State.chronicle.Count, labButOff.State.chronicle.Count);
            Assert.AreEqual(0, plain.ReturnCardsCapped(-1, 7).cards.Count(c => c.kind == CardKind.Dilemma));
        }

        // ---------- the save ----------

        [Test]
        public void TheSaveRoundTripsAndAnOldSaveLoadsWithNone()
        {
            var sim = NewSim(15, Cfg(true));
            Years(sim, 2.5);
            var d = sim.OpenDilemma ?? sim.DebugRaiseDilemma();
            Assert.IsNotNull(d);
            sim.AnswerDilemma(d.id, 0, DilemmaPath.Card);
            var json = JsonUtility.ToJson(sim.State);
            var back = JsonUtility.FromJson<SimState>(json);
            Assert.AreEqual(json, JsonUtility.ToJson(back));
            Assert.AreEqual(1, back.dilemmas.Count);
            Assert.AreEqual(DilemmaState.Answered, back.dilemmas[0].state);
            Assert.AreEqual(DilemmaPath.Card, back.dilemmas[0].path);

            var old = JsonUtility.FromJson<SimState>("{\"seed\":7,\"month\":5}");
            _ = new SimCore(new SimConfig(), old); // the constructor makes sure the new lists exist
            Assert.IsNotNull(old.dilemmas);
            Assert.AreEqual(0, old.dilemmas.Count);
            Assert.IsNotNull(old.dilemmaLeans);
            Assert.IsNotNull(old.customs);
            Assert.AreEqual(1, old.dilemmaNextId);
            Assert.AreEqual(-1, old.dilemmaNextMonth);
        }
    }
}
