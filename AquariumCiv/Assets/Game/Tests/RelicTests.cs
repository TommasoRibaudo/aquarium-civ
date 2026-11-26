using System.Collections.Generic;
using System.Linq;
using AquariumCiv.Sim;
using NUnit.Framework;
using UnityEngine;
using static AquariumCiv.Tests.SimCoreTests;

namespace AquariumCiv.Tests
{
    /// <summary>The slice relics, belief halves and the people's quotes (#74, relics-and-glass-voice.md §1–§6): each relic's
    /// trigger and fallback, the stage halves, no line from a later stage early, the Sandal lost ≥ 20 years, and proof the
    /// economy doesn't move.</summary>
    public class RelicTests
    {
        static ChronicleEntry RelicCard(SimCore sim, string id) => sim.State.chronicle.LastOrDefault(e => e.relicId == id && e.kind != "lost");

        static void Watched(SimCore sim, int years, System.Action<SimCore, int> yearly = null)
        {
            for (int y = 0; y < years; y++)
            {
                sim.Advance(1);
                if (sim.State.eraReady) sim.AdvanceEra();
                yearly?.Invoke(sim, y);
            }
        }

        /// <summary>A credited Keeper entry in the month the next Step will run.</summary>
        static void CreditNextMonth(SimCore sim)
        {
            var e = sim.Chronicle("keeper", "After the Keeper's rain, the people planted a new field.", 0.9f);
            e.keeper = true;
            e.month = sim.State.month + 1;
        }

        static void PastCredits(SimCore sim, int n)
        {
            for (int i = 0; i < n; i++) { var e = sim.Chronicle("keeper", "An old credit.", 0.9f); e.keeper = true; }
        }

        static Notable MakeScholar(SimCore sim)
        {
            var n = sim.State.notables.Where(x => x.alive && x.age >= 16f).OrderBy(x => x.id).First();
            n.trait = "the clever";
            return n;
        }

        // ---------- the Festival Mask ----------
        [Test]
        public void TheFirstEraFestivalLeavesTheMask()
        {
            var sim = NewSim(5);
            Assert.IsNull(sim.EraFestivalHeld(), "no era festival in the camp");
            sim.ForceNextEra();
            var mask = sim.FindRelic(RelicId.FestivalMask);
            Assert.IsNotNull(mask);
            Assert.IsTrue(mask.Found);
            var e = RelicCard(sim, RelicId.FestivalMask);
            StringAssert.Contains("the dance for the new village", e.text);
            var festival = sim.State.chronicle.Single(x => x.kind == "era_festival");
            Assert.IsTrue(sim.ReturnCardsCapped(-1, 50).cards.Any(c => c.entries.Contains(festival) && c.entries.Contains(e)), "one card with #86's festival");
            Assert.AreEqual("Dance facing the sky tonight. Let it see us.", e.quote);
            Assert.AreEqual(mask.ownerId, e.speakerId, "the wearer says it");
            Assert.AreSame(mask, sim.EraFestivalHeld(), "#86's festival seam: idempotent");
            sim.ForceNextEra();
            Assert.AreEqual(1, sim.State.chronicle.Count(x => x.relicId == RelicId.FestivalMask), "only the first festival");
            Assert.AreEqual(BeliefHalf.Early, sim.BeliefHalfNow, "the mask says nothing about the stage's half");
        }

        // ---------- the Keeper Doll ----------
        [Test]
        public void TheKeeperDollComesWithTheTenthCreditInTheVillage()
        {
            var sim = NewSim(8);
            PastCredits(sim, 12);
            CreditNextMonth(sim);
            sim.Step();
            Assert.IsFalse(sim.HasRelic(RelicId.KeeperDoll), "not in the camp");

            sim.ForceNextEra();
            sim.State.chronicle.RemoveAll(e => e.keeper);
            PastCredits(sim, 8);
            CreditNextMonth(sim);
            sim.Step();
            Assert.IsFalse(sim.HasRelic(RelicId.KeeperDoll), "9 credits");
            Assert.AreEqual(BeliefHalf.Early, sim.BeliefHalfNow);
            for (int i = 0; i < 3; i++) sim.PlaceNudge(SimCore.NudgeKind.Rain, 0f, 0f);
            sim.CleanGrime(0.1f);
            CreditNextMonth(sim);
            sim.Step();
            Assert.IsTrue(sim.HasRelic(RelicId.KeeperDoll), "the 10th");
            var doll = sim.FindRelic(RelicId.KeeperDoll);
            Assert.AreEqual("cloth", doll.prop, "#718: it always holds the cloth");
            Assert.GreaterOrEqual(doll.ownerId, 0);
            Assert.AreEqual(BeliefHalf.Later, sim.BeliefHalfNow, "the doll opens stage 2's later half");
            Assert.AreEqual("The doll's eye is open. That means the Keeper's is too.", RelicCard(sim, RelicId.KeeperDoll).quote);
        }

        [Test]
        public void TheKeeperDollFallbackAfterFifteenVillageYears()
        {
            var sim = NewSim(8);
            sim.ForceNextEra();
            CreditNextMonth(sim);
            sim.Step();
            Assert.IsFalse(sim.HasRelic(RelicId.KeeperDoll));
            sim.State.eraStartMonth -= 15 * SimConfig.MonthsPerYear;
            sim.Step();
            Assert.IsFalse(sim.HasRelic(RelicId.KeeperDoll), "the fallback waits for the next credit");
            CreditNextMonth(sim);
            sim.Step();
            Assert.IsTrue(sim.HasRelic(RelicId.KeeperDoll));
        }

        [Test]
        public void NoRelicIsFoundWhileThePlayerIsAwayItWaitsForTheFirstLiveMonth()
        {
            var sim = NewSim(8);
            sim.ForceNextEra();
            sim.State.eraStartMonth -= 15 * SimConfig.MonthsPerYear; // the fallback is open
            CreditNextMonth(sim); // a credited month, which would make the doll
            var r = sim.CatchUp(sim.Config.StepHours * 3);
            Assert.GreaterOrEqual(r.months, 1, "months passed while away");
            Assert.IsFalse(sim.CatchingUp);
            Assert.IsFalse(sim.HasRelic(RelicId.KeeperDoll), "nothing is found away");
            Assert.IsTrue(sim.State.dollDue, "the trigger fired and waits");
            sim.Step(); // the first live month: the same trigger fires
            Assert.IsTrue(sim.HasRelic(RelicId.KeeperDoll));
            Assert.IsFalse(sim.State.dollDue);
        }

        // ---------- the Left Sandal ----------
        static SimCore WithSandalLost(int seed, out Notable owner)
        {
            var sim = NewSim(seed);
            for (int i = 0; i < 400 && sim.FindRelic(RelicId.LeftSandal) == null; i++)
            {
                sim.ForceStory();
                sim.State.month += 7 * SimConfig.MonthsPerYear;
                foreach (var n in sim.State.notables) n.age = 30f; // nobody dies of the jumps
            }
            var s = sim.FindRelic(RelicId.LeftSandal);
            Assert.IsNotNull(s, "a lost story came");
            owner = sim.FindNotable(s.ownerId);
            return sim;
        }

        [Test]
        public void TheFirstLostStoryIsTheSandalAndItStaysLostTwentyYears()
        {
            var sim = WithSandalLost(11, out var owner);
            var s = sim.FindRelic(RelicId.LeftSandal);
            Assert.IsFalse(s.Found);
            var lost = sim.State.chronicle.Single(e => e.id == s.lostEntryId);
            Assert.AreEqual("lost", lost.kind, "the story stream's own kind: its dice don't move");
            Assert.AreEqual(RelicId.LeftSandal, lost.relicId);
            StringAssert.Contains("lost a sandal crossing the stream", lost.text);
            Assert.AreEqual($"{owner.name} lost a sandal", sim.Headline(lost));
            StringAssert.Contains("still talks about that sandal", sim.RelicHint(RelicId.LeftSandal));

            sim.State.overgrowth = 1f;
            sim.State.month = s.lostMonth;
            for (int y = 0; y < 20; y++)
            {
                sim.Prune(0.01f);
                Assert.IsFalse(s.Found, $"year {y}");
                sim.State.month += SimConfig.MonthsPerYear;
            }
            sim.State.month = s.lostMonth + 20 * SimConfig.MonthsPerYear - 1;
            sim.Prune(0.01f);
            Assert.IsFalse(s.Found, "a month short");
            sim.State.month++;
            sim.State.overgrowth = 0f;
            sim.Prune(0.5f);
            Assert.IsFalse(s.Found, "no weeds, no find");
            sim.State.overgrowth = 0.2f;
            sim.Prune(0.05f);
            Assert.IsTrue(s.Found);
            Assert.AreEqual(owner.id, s.holderId);
            var find = RelicCard(sim, RelicId.LeftSandal);
            StringAssert.Contains("lost 20 years ago", find.text);
            StringAssert.Contains("still fits", find.text);
            Assert.AreEqual(owner.id, find.speakerId);
            StringAssert.StartsWith("Lost something? Ask the", find.quote);
            var cards = sim.ReturnCardsCapped(-1, 50);
            Assert.IsTrue(cards.cards.Any(c => c.entries.Contains(lost) && c.entries.Contains(find)), "the loss and the find are one card");

            // #715: the sandal is the box's only "lost" story (no more lost in the hills)
            int before = sim.State.chronicle.Count(e => e.kind == "lost");
            for (int i = 0; i < 100; i++)
            {
                sim.ForceStory();
                sim.State.month += 7 * SimConfig.MonthsPerYear;
            }
            Assert.AreEqual(before, sim.State.chronicle.Count(e => e.kind == "lost"), "never lost in the hills");
        }

        [Test]
        public void ADeadOwnersSandalGoesToTheirEldestChild()
        {
            var sim = WithSandalLost(12, out var owner);
            sim.State.notables.RemoveAll(n => n.parentId == owner.id);
            var child = sim.DebugBirth(); // born to the first adult; make it the owner's
            child.parentId = owner.id;
            child.age = 30f;
            owner.alive = false;
            Assert.IsTrue(sim.DebugSkipSandalYears());
            sim.State.overgrowth = 0.3f;
            sim.Prune(0.1f);
            var s = sim.FindRelic(RelicId.LeftSandal);
            Assert.IsTrue(s.Found);
            Assert.AreEqual(child.id, s.holderId);
            var find = RelicCard(sim, RelicId.LeftSandal);
            StringAssert.Contains("remembering stone", find.text);
            Assert.AreEqual(child.id, find.otherNotableId);
            Assert.AreEqual(child.id, find.speakerId);
        }

        // ---------- the Lens ----------
        [Test]
        public void ScholarsAreTownNotablesWhoAreCleverTinkerersOrDreamers()
        {
            var sim = NewSim(6);
            MakeScholar(sim);
            Assert.AreEqual(0, sim.Scholars.Count, "not before the town");
            sim.ForceNextEra(); sim.ForceNextEra();
            Assert.IsTrue(sim.Scholars.Count > 0);
            Assert.IsTrue(sim.Scholars.All(n => n.alive && n.age >= 16f && (n.trait == "the clever" || n.trait == "the tinkerer" || n.trait == "the dreamer")));
        }

        // ---------- belief and the line bank ----------
        [Test]
        public void NoLineFromALaterStageOrHalfComesEarly()
        {
            string[] moments = typeof(PeopleMoment).GetFields().Select(f => (string)f.GetValue(null)).ToArray();
            var sim = NewSim(9);
            sim.DebugBirth();
            foreach (var stage in new[] { 1, 2, 3 })
            {
                if (stage > 1) sim.ForceNextEra();
                foreach (var scholar in new[] { false, true }) { if (scholar) MakeScholar(sim); }
                foreach (var later in new[] { false, true })
                {
                    sim.DebugSetBelief(stage, later);
                    foreach (var l in sim.LinesNow(false))
                    {
                        Assert.IsTrue(l.stage == 0 || l.stage == stage, $"line {l.no} in stage {stage}");
                        if (!later) Assert.AreNotEqual('L', l.half, $"line {l.no} before stage {stage}'s relic");
                        if (l.quote && !(stage == 3 && later)) StringAssert.DoesNotContain("glass", l.text, $"line {l.no}: no glass before the Lens");
                    }
                    for (int m = 0; m < 12; m++, sim.State.month++)
                        foreach (var moment in moments)
                        {
                            var q = sim.Quote(moment);
                            if (q == null) continue;
                            var line = SimCore.LineBank.Single(x => x.no == q.lineNo);
                            Assert.IsTrue(line.quote);
                            Assert.IsTrue(line.stage == 0 || line.stage == stage, $"{moment}: line {line.no} in stage {stage}");
                            Assert.IsTrue(later || line.half != 'L', $"{moment}: line {line.no} early");
                        }
                }
            }
            sim.DebugSetBelief(3, false);
            Assert.AreEqual(0, SimCore.LineBank.Count(l => l.quote && l.text.Contains("glass") && !(l.stage == 3 && l.half == 'L')), "the bank itself");
        }

        [Test]
        public void QuotesComeFromTheirStageAndSpeaker()
        {
            var sim = NewSim(9);
            Assert.AreEqual("The sky knocked!", sim.Quote(PeopleMoment.Tap).text);
            Assert.AreEqual(-1, sim.Quote(PeopleMoment.Tap).speakerId, "the crowd");
            var chief = sim.HolderOf(RoleType.Chief);
            var glass = sim.Quote(PeopleMoment.Glass);
            bool kid = sim.State.notables.Any(n => n.alive && n.age >= 4f && n.age < SimCore.AdultAge);
            if (!kid) Assert.IsNull(glass, "#719: line 3 names a child: no child, no line");
            else { Assert.AreEqual(3, glass.lineNo); Assert.AreEqual(chief.id, glass.speakerId); StringAssert.DoesNotContain("{kid}", glass.text); }
            Assert.IsNull(sim.Quote(PeopleMoment.Doll), "no doll lines in the camp");
            sim.ForceNextEra();
            StringAssert.Contains("window", sim.Quote(PeopleMoment.Return).text);
            sim.RelicsOn = false;
            Assert.IsNull(sim.Quote(PeopleMoment.Return));
        }

        // ---------- debug, saves, era tags ----------
        [Test]
        public void TheDebugPanelCanGiveEveryRelic()
        {
            var sim = NewSim(4);
            foreach (var id in RelicId.All)
            {
                var r = sim.DebugGiveRelic(id);
                Assert.IsNotNull(r, id);
                Assert.IsTrue(r.Found, id);
                Assert.IsNotNull(RelicCard(sim, id), id);
                Assert.IsNotEmpty(sim.RelicLabel(id));
            }
            Assert.AreEqual(3, sim.FoundRelics.Count()); // #718: three relics
            CollectionAssert.AreEqual(RelicId.DefaultCarried, sim.DefaultRelicPicks());
            Assert.IsTrue(sim.TakeRelic(RelicId.KeeperDoll));
            Assert.AreEqual(sim.State.month, sim.FindRelic(RelicId.KeeperDoll).takenMonth);

            var loaded = new SimCore(sim.Config, JsonUtility.FromJson<SimState>(JsonUtility.ToJson(sim.State)));
            Assert.AreEqual(JsonUtility.ToJson(sim.State), JsonUtility.ToJson(loaded.State));
            Assert.AreEqual(3, loaded.FoundRelics.Count());
            Assert.AreEqual(RelicId.LeftSandal, loaded.State.chronicle.First(e => e.kind == "lost").relicId);
        }

        [Test]
        public void TheRelicsCrossWithTheFounders()
        {
            var sim = NewSim(4);
            foreach (var id in RelicId.All) sim.DebugGiveRelic(id);
            CollectionAssert.AreEqual(RelicId.DefaultCarried, sim.RelicsForCrossing(), "the pre-ticked one (#718)");
            Assert.AreEqual("Sky Crumb", SimCore.RelicName(RelicId.SkyCrumb));

            var picked = NewSim(4);
            foreach (var id in RelicId.All) picked.DebugGiveRelic(id);
            picked.TakeRelic(RelicId.LeftSandal);
            picked.TakeRelic(RelicId.FestivalMask);
            CollectionAssert.AreEqual(new[] { RelicId.FestivalMask, RelicId.LeftSandal }, picked.RelicsForCrossing(), "the player's pick wins");

            // CompleteBox (#86) carries them.
            sim.ForceNextEra(); sim.ForceNextEra();
            Assert.IsTrue(sim.ForcePeakReady());
            var done = sim.CompleteBox();
            CollectionAssert.AreEqual(new[] { "Keeper Doll" }, done.relics); // #718
            Assert.IsTrue(RelicId.DefaultCarried.All(id => sim.FindRelic(id).takenMonth >= 0), "marked taken");
            if (done.leader != null) StringAssert.Contains("carrying the Keeper Doll wrapped in cloth", done.foundersLine);
        }

        [Test]
        public void TheDebugSandalLosesAndSkips()
        {
            var sim = NewSim(4);
            var s = sim.DebugLoseSandal();
            Assert.IsNotNull(s);
            sim.State.overgrowth = 0.5f;
            sim.Prune(0.1f);
            Assert.IsFalse(s.Found);
            sim.DebugSkipSandalYears(20);
            sim.Prune(0.1f);
            Assert.IsTrue(s.Found);
        }


        // ---------- the economy ----------
        [TestCase(1)]
        [TestCase(7)]
        [TestCase(77)]
        [TestCase(1234)]
        public void TheEconomyIsTheSameWithAndWithoutTheRelics(int seed)
        {
            var off = NewSim(seed); off.RelicsOn = false;
            var on = NewSim(seed);
            void Keeper(SimCore s, int y)
            {
                if (y % 3 == 0) s.PlaceNudge(SimCore.NudgeKind.Gifts[y % SimCore.NudgeKind.Gifts.Length], 0.1f * (y % 7) - 0.3f, 0.2f);
                if (y % 2 == 0) { s.CleanGrime(0.3f); s.Prune(0.2f); }
            }
            Watched(off, 120, Keeper);
            Watched(on, 120, Keeper);
            Assert.AreNotEqual(Era.Camp, on.State.era);
            Assert.GreaterOrEqual(on.FoundRelics.Count(), 3, string.Join(",", on.FoundRelics.Select(r => r.id)));
            TestContext.WriteLine($"seed {seed}: {string.Join(", ", on.FoundRelics.Select(r => $"{r.id} y{r.foundMonth / 12}"))}");
            Assert.AreEqual(JobTests.Economy(off.State), JobTests.Economy(on.State), "economy, dice and notables");
            Assert.AreEqual(off.State.chronicle.Select(e => e.id + e.kind), on.State.chronicle.Where(e => e.id < SimCore.RelicLineIdBase || e.id >= SimCore.SlowLineIdBase).Select(e => e.id + e.kind),
                "the same stories, same ids; relic cards come on top");
            Assert.AreEqual(off.State.nextId, on.State.nextId);
        }
    }
}
