using System.Collections.Generic;
using System.Linq;
using AquariumCiv.Sim;
using NUnit.Framework;
using static AquariumCiv.Tests.SimCoreTests;

namespace AquariumCiv.Tests
{
    /// <summary>The era moment and the box completing (docs/design/era-moment.md §6–§10, #86): roles at the change, the
    /// festival year, the town's peak, CompleteBox and the book's chapters.</summary>
    public class EraMomentTests
    {
        /// <summary>Month by month, crossing each era the moment it is ready (a player who is always watching).</summary>
        static void Watched(SimCore sim, int years)
        {
            for (int m = 0; m < years * SimConfig.MonthsPerYear; m++)
            {
                sim.Step();
                if (sim.State.eraReady) sim.AdvanceEra();
            }
        }

        static SimCore Town(int seed)
        {
            var sim = NewSim(seed);
            for (int m = 0; m < 150 * SimConfig.MonthsPerYear && sim.State.era != Era.Town; m++)
            {
                sim.Step();
                if (sim.State.eraReady) sim.AdvanceEra();
            }
            Assert.AreEqual(Era.Town, sim.State.era, $"seed {seed} reaches the town");
            return sim;
        }

        // ---------- roles at the change ----------
        [TestCase(1)]
        [TestCase(7)]
        [TestCase(1234)]
        public void RolesCarryOverAtEachChange(int seed)
        {
            var sim = NewSim(seed);
            sim.Advance(5);
            var chief = sim.HolderOf(RoleType.Chief);
            Assert.IsNotNull(chief);
            chief.age = 30f; chief.lifespan = 200f;
            Assert.IsTrue(sim.ForceNextEra());
            Assert.AreEqual(chief, sim.HolderOf(RoleType.Headman), "chief → headman");
            Assert.AreEqual(-1, sim.State.lordLineId, "no lord line before the town");
            Assert.IsTrue(sim.ForceNextEra());
            Assert.AreEqual(chief, sim.HolderOf(RoleType.Lord), "headman → lord");
            Assert.AreEqual(chief, sim.Lord);
            Assert.AreEqual(chief.id, sim.State.lordLineId, "the lord line starts with the first lord");
            // The leaders are logged for the book, one term per role.
            CollectionAssert.AreEqual(new[] { RoleType.Chief, RoleType.Headman, RoleType.Lord },
                sim.State.leaders.Where(t => t.notableId == chief.id).Select(t => t.role).ToArray());
        }

        [Test]
        public void TheLordsHeirIsTheEldestChild()
        {
            var sim = NewSim(3);
            sim.Advance(5);
            var chief = sim.HolderOf(RoleType.Chief);
            chief.age = 30f; chief.lifespan = 200f;
            sim.ForceNextEra(); sim.ForceNextEra();
            var lord = sim.Lord;
            Assert.IsNotNull(lord);
            var older = sim.DebugBirth(); older.parentId = lord.id; older.age = 9f; older.lifespan = 200f;
            var younger = sim.DebugBirth(); younger.parentId = lord.id; younger.age = 4f; younger.lifespan = 200f;
            Assert.AreEqual(older, sim.LordHeir, "the eldest child stands behind the chair, of any age");
        }

        [TestCase(1)]
        [TestCase(7)]
        public void TheLordAppointsTheMerchantAndTheCaptain(int seed)
        {
            var sim = Town(seed);
            var lord = sim.Lord;
            Assert.IsNotNull(lord);
            if (sim.SeatOf(RoleType.Merchant) == null) Assert.Inconclusive("no market yet");
            var lines = sim.State.chronicle.Where(e => e.month == sim.State.month).Select(e => e.text).ToList();
            foreach (var role in new[] { RoleType.Merchant, RoleType.Captain })
            {
                var holder = sim.HolderOf(role);
                Assert.IsNotNull(holder, $"{role} is filled at the moment");
                Assert.AreNotEqual(lord, holder);
            }
            Assert.AreEqual(sim.State.era, Era.Town);
            Assert.IsTrue(lines.Any(t => t.Contains("needed people for its offices")), string.Join("\n", lines));
        }

        [Test]
        public void AnEmptyLeaderSeatGetsAnInstantVote()
        {
            var sim = NewSim(5);
            sim.Advance(5);
            Assert.IsTrue(sim.ForceEraReady());
            Assert.IsTrue(sim.DebugVacateRole(RoleType.Chief));
            Assert.IsTrue(sim.AdvanceEra());
            var headman = sim.HolderOf(RoleType.Headman);
            Assert.IsNotNull(headman, "the village chose a headman on the spot");
            var line = sim.State.chronicle.Last(e => e.kind == "role_era");
            Assert.AreEqual(sim.State.month, line.month);
            StringAssert.Contains($"chose {headman.FullName} as its first headman", line.text);
            var offices = sim.State.chronicle.Where(e => e.month == sim.State.month && e.kind == "role_offices").ToList();
            Assert.IsFalse(offices.Any(e => e.text.Contains($"{headman.FullName} became headman")), "told once, as the vote");
        }

        [Test]
        public void AMomentLeftHalfwayChangesNothing()
        {
            // The view calls AdvanceEra only at the cheer: before it, the era still waits and nothing has moved.
            var sim = NewSim(9);
            sim.Advance(5);
            sim.ForceEraReady();
            sim.Advance(2);
            Assert.AreEqual(Era.Camp, sim.State.era);
            Assert.IsTrue(sim.State.eraReady);
            Assert.IsFalse(sim.FestivalOn);
            Assert.IsFalse(sim.State.chronicle.Any(e => e.kind == "era_festival"));
        }

        // ---------- the festival ----------
        [TestCase(1)]
        [TestCase(1234)]
        public void TheFestivalLiftsTheMoodForAYearThenEnds(int seed)
        {
            var a = NewSim(seed);
            a.Advance(5);
            a.ForceEraReady();
            var b = new SimCore(a.Config, UnityEngine.JsonUtility.FromJson<SimState>(UnityEngine.JsonUtility.ToJson(a.State)));
            Assert.IsTrue(a.AdvanceEra()); Assert.IsTrue(b.AdvanceEra());
            b.DebugEndFestival(); // the same box without its festival
            Assert.IsTrue(a.FestivalOn);
            Assert.AreEqual(a.Config.festivalMoodLift, a.FestivalMoodLift);
            Assert.IsFalse(b.FestivalOn);
            var line = a.State.chronicle.Last(e => e.kind == "era_festival");
            StringAssert.Contains("danced for three days to welcome the new village", line.text);
            long until = a.State.festivalUntilMonth;
            Assert.AreEqual(a.State.month + SimConfig.MonthsPerYear, until, "one game year");
            uint econA = a.State.econRng.state, storyA = a.State.storyRng.state, rngA = a.State.rng.state;
            Assert.AreEqual(b.State.econRng.state, econA, "the festival draws no dice");

            for (int m = 0; m < 6; m++) { a.Step(); b.Step(); }
            Assert.Greater(a.State.mood, b.State.mood, "half a year in, the festival box is happier");
            Assert.AreEqual(b.State.econRng.state, a.State.econRng.state, "no dice drawn for it");
            while (a.State.month < until) { a.Step(); b.Step(); }
            Assert.IsFalse(a.FestivalOn, "a year on, the bunting comes down");
            Assert.AreEqual(0f, a.FestivalMoodLift);
            float gap = a.State.mood - b.State.mood;
            for (int m = 0; m < 36; m++) { a.Step(); b.Step(); }
            Assert.Less(System.Math.Abs(a.State.mood - b.State.mood), System.Math.Max(gap, 0.001f), "the lift fades once it ends");
        }

        [Test]
        public void TheTownMomentClearsTheOldHearthAndHasAFair()
        {
            var sim = Town(7);
            var now = sim.State.chronicle.Where(e => e.month == sim.State.month).ToList();
            Assert.IsTrue(now.Any(e => e.kind == "era_cleared"), "the old fire ring is cleared for the market");
            StringAssert.Contains("welcome the new town", now.Last(e => e.kind == "era_festival").text);
            Assert.IsTrue(sim.FestivalOn);
        }

        // ---------- the peak ----------
        [Test]
        public void ThePeakRuleFiresOnA120YearBox()
        {
            int ready = 0, seeds = 0;
            var when = new List<int>();
            for (int seed = 1; seed <= 12; seed++)
            {
                var sim = NewSim(seed);
                int readied = 0;
                sim.PeakReadied += () => readied++;
                Watched(sim, 120);
                if (sim.State.era != Era.Town) continue;
                seeds++;
                var s = sim.State;
                TestContext.WriteLine($"seed {seed}: town {sim.YearsInEra} y, seats {s.townSeatsHeld.Count} ({string.Join(", ", s.townSeatsHeld)}), full {sim.YearsFull} y, peak {(s.peakReady ? SimCore.YearNo(s.peakReadyMonth).ToString() : "-")}");
                bool rule = sim.YearsInEra >= sim.Config.peakAt.townYears && s.townSeatsHeld.Count >= sim.Config.peakAt.seats && sim.YearsFull >= sim.Config.peakAt.fullYears;
                if (rule) Assert.IsTrue(s.peakReady, $"seed {seed}: the rule holds, so the peak is ready");
                if (!s.peakReady) continue;
                ready++;
                when.Add(SimCore.YearNo(s.peakReadyMonth));
                Assert.AreEqual(1, readied, "raised once");
                Assert.AreEqual(1, s.chronicle.Count(e => e.kind == "peak-ready"));
                Assert.IsFalse(s.boxComplete, "the peak waits for the player");
                Assert.GreaterOrEqual(s.townSeatsHeld.Count, sim.Config.peakAt.seats);
            }
            TestContext.WriteLine($"{ready}/{seeds} towns at their peak by year 120: years {string.Join(", ", when)}");
            Assert.GreaterOrEqual(seeds, 10, "most boxes reach the town in 120 years");
            // Measured (#86): 5 of 12 by year 120, at years 71–110; the rest are towns under 40 years old or not yet full.
            Assert.GreaterOrEqual(ready, seeds / 3, "a good share of towns reach their peak by year 120");
        }

        [Test]
        public void ThePeakNeedsAnOldTownEverySeatAndAFullBox()
        {
            var sim = Town(7);
            var s = sim.State;
            Assert.IsFalse(sim.MeetsPeak, "a new town is not at its peak");
            // Every seat once, the box full for long enough, but the town too young.
            s.townSeatsHeld = SimCore.RolesOf(Era.Town).ToList();
            s.fullSinceMonth = s.month - 5 * SimConfig.MonthsPerYear;
            Assert.IsFalse(sim.MeetsPeak, "too young");
            s.eraStartMonth = s.month - 40 * SimConfig.MonthsPerYear;
            Assert.IsTrue(sim.MeetsPeak);
            s.townSeatsHeld.RemoveAt(s.townSeatsHeld.Count - 1);
            Assert.IsFalse(sim.MeetsPeak, "a seat never held");
            s.townSeatsHeld = SimCore.RolesOf(Era.Town).ToList();
            s.fullSinceMonth = s.month - 2 * SimConfig.MonthsPerYear;
            Assert.IsFalse(sim.MeetsPeak, "full for only two years");
        }

        [Test]
        public void NoPeakBeforeTheTown()
        {
            var sim = NewSim(4);
            sim.Advance(5);
            Assert.IsFalse(sim.ForcePeakReady());
            Assert.IsNull(sim.CompleteBox(), "no box completes before its peak");
        }

        // ---------- CompleteBox ----------
        [Test]
        public void CompleteBoxWritesTheLastLineAndReturnsTheFounders()
        {
            var sim = Town(7);
            Assert.IsNull(sim.CompleteBox(), "not before the peak");
            var lord = sim.Lord;
            Assert.IsNotNull(lord);
            // The lord has two grown children: the eldest inherits, the younger leads the founders.
            var heir = sim.DebugBirth(); heir.parentId = lord.id; heir.age = 24f; heir.lifespan = 200f;
            var younger = sim.DebugBirth(); younger.parentId = lord.id; younger.age = 19f; younger.lifespan = 200f;
            Assert.IsTrue(sim.ForcePeakReady());
            Assert.IsTrue(sim.State.chronicle.Any(e => e.kind == "peak-ready"));

            var r = sim.CompleteBox();
            Assert.IsNotNull(r);
            Assert.AreEqual(heir, sim.LordHeir);
            Assert.AreEqual(younger, r.leader, "the lord's younger child leads");
            Assert.AreEqual(3, r.founders.Count, "the leader and two young notables");
            Assert.AreEqual(r.founders.Count, r.founders.Select(n => n.family).Distinct().Count(), "three families");
            Assert.IsFalse(r.founders.Contains(lord) || r.founders.Contains(heir), "the lord and the heir stay");
            Assert.IsTrue(r.founders.All(n => n.alive && n.age >= SimCore.AdultAge));
            Assert.IsNotNull(r.relics, "relics are #74's; the list is there to fill");
            Assert.AreEqual(SimCore.YearNo(sim.State.month), r.year);
            StringAssert.StartsWith($"{sim.PlaceName} · years 1–{r.year} · camp, village, town", r.plate);
            StringAssert.StartsWith(younger.FullName, r.foundersLine);

            var s = sim.State;
            var last = s.chronicle.Single(e => e.kind == "box-complete");
            Assert.AreEqual(1.0f, last.salience);
            Assert.AreEqual($"{sim.PlaceName} was finished in year {r.year}. Lord {lord.FullName} lit the last lantern. The town lives on, on the Keeper's shelf.", last.text);
            StringAssert.Contains("younger child", s.chronicle.Single(e => e.kind == "founders_left").text);
            Assert.IsTrue(s.boxComplete);
            Assert.AreEqual(s.month, s.boxCompleteMonth);
            CollectionAssert.AreEqual(r.founders.Select(n => n.id).ToArray(), s.founderIds.ToArray());
            Assert.IsNull(sim.CompleteBox(), "only once");
        }

        [Test]
        public void ForceCompleteBoxWorksFromACamp()
        {
            var sim = NewSim(11);
            sim.Advance(3);
            var r = sim.ForceCompleteBox();
            Assert.IsNotNull(r);
            Assert.AreEqual(Era.Town, sim.State.era);
            Assert.IsTrue(sim.State.boxComplete);
            Assert.IsNotEmpty(r.founders);
        }

        // ---------- the book ----------
        [Test]
        public void TheBookHasAChapterPerEra()
        {
            var sim = Town(7);
            var ch = sim.BookChapters();
            Assert.AreEqual(3, ch.Count);
            CollectionAssert.AreEqual(new[] { Era.Camp, Era.Village, Era.Town }, ch.Select(c => c.era).ToArray());
            StringAssert.StartsWith("The camp years (1–", ch[0].title);
            Assert.AreEqual(ch[0].toYear, ch[1].fromYear, "chapters meet at the era moment");
            Assert.AreEqual(ch[1].toYear, ch[2].fromYear);
            Assert.AreEqual(-1, ch[2].toYear);
            StringAssert.EndsWith("–…)", ch[2].title);
            foreach (var c in ch)
            {
                Assert.IsNotEmpty(c.leaders, $"{c.title}: its leaders");
                Assert.LessOrEqual(c.events.Count, 5);
                Assert.IsTrue(c.events.All(e => e.month >= c.fromMonth && (c.toMonth < 0 || e.month < c.toMonth)), c.title);
            }
            Assert.IsNotEmpty(ch[0].events, "the camp years have something to tell");
        }

        // ---------- determinism ----------
        [Test]
        public void TheEraMomentDrawsNoDice()
        {
            var sim = NewSim(21);
            sim.Advance(5);
            sim.ForceEraReady();
            var s = sim.State;
            uint r = s.rng.state, e = s.econRng.state, st = s.storyRng.state;
            sim.AdvanceEra();
            Assert.AreEqual(r, s.rng.state); Assert.AreEqual(e, s.econRng.state); Assert.AreEqual(st, s.storyRng.state);
        }

        [Test]
        public void TheNewStateSurvivesASave()
        {
            var sim = Town(7);
            sim.ForcePeakReady();
            var copy = UnityEngine.JsonUtility.FromJson<SimState>(UnityEngine.JsonUtility.ToJson(sim.State));
            Assert.AreEqual(sim.State.festivalUntilMonth, copy.festivalUntilMonth);
            Assert.AreEqual(sim.State.lordLineId, copy.lordLineId);
            Assert.AreEqual(sim.State.leaders.Count, copy.leaders.Count);
            Assert.IsTrue(copy.peakReady);
            Assert.AreEqual(sim.State.peakReadyMonth, copy.peakReadyMonth);
        }
    }
}
