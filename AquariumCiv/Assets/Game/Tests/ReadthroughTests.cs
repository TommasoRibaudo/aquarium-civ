using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using AquariumCiv.Sim;
using NUnit.Framework;
using static AquariumCiv.Tests.SimCoreTests;

namespace AquariumCiv.Tests
{
    /// <summary>
    /// #61, the read-through of a 120-year box as a player reads it (docs/design/results/readthrough.md): the chronicle
    /// speaks each era's own words, doesn't tell the same sentence twice in a row, never pairs a parent with their child or
    /// a married person with someone else, and the people don't say "glass" before the Lens.
    /// </summary>
    public class ReadthroughTests
    {
        /// <summary>A century watched month by month; the player watches every era moment as soon as it waits.</summary>
        static List<(ChronicleEntry e, Era era)> Century(int seed, out SimCore sim)
        {
            var s = NewSim(seed);
            var log = new List<(ChronicleEntry, Era)>();
            s.Chronicled += e => log.Add((e, s.State.era));
            for (int m = 0; m < 120 * SimConfig.MonthsPerYear; m++)
            {
                s.Step();
                if (s.State.eraReady) s.AdvanceEra();
            }
            sim = s;
            return log;
        }

        [TestCase(1)]
        [TestCase(77)]
        [TestCase(1234)]
        public void TheChronicleSpeaksEachErasOwnWords(int seed)
        {
            var log = Century(seed, out var sim);
            Assert.AreEqual(Era.Town, sim.State.era);
            foreach (var (e, era) in log)
            {
                string t = e.text, at = $"year {e.month / 12} ({era}): {t}";
                if (era == Era.Camp)
                {
                    Assert.IsFalse(Regex.IsMatch(t, @"\b(village|town|street|well|lamps)\b"), at);
                    Assert.IsFalse(Regex.IsMatch(t, @"field of grain|lumber mill"), at);
                }
                if (era == Era.Village) Assert.IsFalse(Regex.IsMatch(t, @"\btown\b"), at);
                if (era == Era.Town) Assert.IsFalse(Regex.IsMatch(t.Replace("The village became a town", ""), @"\bvillage\b|\bcamp\b(?! fire)"), at);
                if (era != Era.Camp) StringAssert.DoesNotContain("Bright One", t, at);
                Assert.IsFalse(Regex.IsMatch(t, @"huddled together in the (well|farm|market|camp|gathering grounds|woodpile)\b"), at);
                if (e.kind == "milestone" && log.Any(x => x.e.kind == "naming" && x.e.month <= e.month)) StringAssert.StartsWith(sim.State.settlementName, t, "named, so the name is used");
            }
        }

        [TestCase(1)]
        [TestCase(77)]
        [TestCase(1234)]
        public void NobodyTellsTheSameSentenceTwiceInARow(int seed)
        {
            var log = Century(seed, out _);
            // Every story told again by the same person (or by the world): never word for word the last one of its kind.
            foreach (var g in log.Select(x => x.e).Where(e => !e.kind.StartsWith("role_")).GroupBy(e => (e.kind, e.notableId)))
            {
                var texts = g.Select(e => e.text).ToList();
                for (int i = 1; i < texts.Count; i++) Assert.AreNotEqual(texts[i - 1], texts[i], $"{g.Key.kind} told twice in a row");
            }
        }

        [TestCase(1)]
        [TestCase(77)]
        [TestCase(1234)]
        public void TheGossipOnlyMatchesCouplesToBe(int seed)
        {
            var log = Century(seed, out var sim);
            int checkedPairs = 0;
            foreach (var (e, _) in log)
            {
                var m = Regex.Match(e.text, @"introduced (.+?) to (.+?) at the|knew about (.+?) and (.+?) before");
                if (!m.Success) continue;
                string a = m.Groups[1].Success ? m.Groups[1].Value : m.Groups[3].Value, b = m.Groups[2].Success ? m.Groups[2].Value : m.Groups[4].Value;
                Notable At(string name) => sim.State.notables.Single(n => n.FullName == name && n.bornMonth <= e.month && (n.alive || n.diedMonth >= e.month));
                var x = At(a); var y = At(b);
                Assert.IsTrue(SimCore.CanCourt(x, y), $"year {e.month / 12}: {e.text}");
                checkedPairs++;
            }
            TestContext.WriteLine($"seed {seed}: {checkedPairs} matches and rumours");
        }

        [TestCase(1)]
        [TestCase(77)]
        [TestCase(1234)]
        public void AHolderNeverPromisesToSortOutTheirOwnFeud(int seed)
        {
            var log = Century(seed, out var sim);
            foreach (var (e, _) in log.Where(x => x.e.kind == "rivalry"))
            {
                int coda = e.text.IndexOf("share a bench. ");
                if (coda < 0) continue;
                string tail = e.text.Substring(coda + "share a bench. ".Length);
                foreach (var id in new[] { e.notableId, e.otherNotableId })
                    Assert.IsFalse(tail.StartsWith(sim.FindNotable(id).FullName + " the "), e.text);
            }
        }

        [Test]
        public void TheTownTalksOfTheWallUntilTheLens()
        {
            var sim = NewSim(8);
            sim.ForceNextEra(); sim.ForceNextEra();
            sim.TermFlavourOn = false; // no office teasers: the slices of life come up
            sim.State.roles.Clear();
            foreach (var n in sim.State.notables) { n.friendId = n.rivalId = -1; }
            var before = Enumerable.Range(0, 8).Select(_ => sim.OpenTeaser()).ToList();
            foreach (var l in before)
            {
                StringAssert.DoesNotContain("glass", l.ToLowerInvariant());
                StringAssert.DoesNotContain("lens", l.ToLowerInvariant());
            }
            sim.GlassRelicFound(SimCore.GlassRelic.Lens);
            var after = Enumerable.Range(0, 8).Select(_ => sim.OpenTeaser()).ToList();
            Assert.IsTrue(after.Any(l => l.Contains("glass")), "the scholars say glass once the Lens is set up");
        }

        [Test]
        public void ANamedPriestIsSeasoned()
        {
            var sim = NewSim(5);
            sim.ForceNextEra();
            Assert.IsNotNull(sim.AddBuildingAt(BuildingType.Church, 19));
            sim.DebugFillRoles();
            var priest = sim.HolderOf(RoleType.Priest);
            Assert.IsNotNull(priest);
            for (int trial = 0; trial < 20; trial++)
            {
                // Everyone else is retired; two can stand, a 17-year-old and a 30-year-old, neither close to the priest.
                var free = sim.State.notables.Where(n => n.alive && n != priest).ToList();
                foreach (var n in free) { n.age = 70f; n.lifespan = 200f; }
                var young = free[0]; var older = free[1];
                foreach (var n in new[] { young, older })
                {
                    sim.DebugVacateRole(sim.RoleOf(n) ?? "-");
                    n.trait = "the lazy"; n.friendId = n.rivalId = n.partnerId = -1; n.mentorId = -1; n.parentId = -1;
                    n.family = n == young ? "Pike" : "Vane";
                }
                priest.family = "Ember"; priest.friendId = priest.partnerId = priest.parentId = -1;
                priest.age = 70f; priest.lifespan = 200f; // retired: not a candidate for their own seat
                young.age = 17f; older.age = 30f;
                foreach (var seat in sim.State.roles.Where(r => r.role != RoleType.Priest)) seat.holderId = -1;
                Assert.IsTrue(sim.DebugVacateRole(RoleType.Priest));
                // One ordinary month (not spring, not new year): the priest names a successor at once; the emptied seats
                // wait for their spring contests.
                sim.State.month = (sim.State.month / 12 + 1) * 12 + 4;
                sim.Step();
                var got = sim.HolderOf(RoleType.Priest);
                Assert.AreEqual(older, got, $"trial {trial}: {got?.FullName} {got?.age} {got?.trait} {sim.RoleOf(older)} {sim.RoleOf(young)}");
                Assert.IsTrue(sim.DebugSetRoleHolder(RoleType.Priest, priest));
                sim.State.month++;
            }
        }

        [Test]
        public void HeadlinesBreakAtAWord()
        {
            var sim = NewSim(1);
            const string text = "Dov Vane the chief kept the meeting at the camp fire going until everyone agreed with them";
            string h = sim.Headline(new ChronicleEntry { kind = "term_meeting", text = text });
            Assert.LessOrEqual(h.Length, 50);
            StringAssert.EndsWith("…", h);
            StringAssert.StartsWith(h.TrimEnd('…') + " ", text, "cut between words");
        }

        [Test]
        public void TheLampMakesTheStrangeLightTheKeepers()
        {
            var sim = NewSim(2);
            sim.ForceNextEra();
            sim.PlaceNudge(SimCore.NudgeKind.Lamp, 3);
            Assert.Contains(SimCore.FlagLampUsed, sim.State.storyFlags);
        }

        [Test]
        public void TheCampGathersBeforeItSows()
        {
            StringAssert.Contains("gathering grounds", NameData.FirstBuildingLine(BuildingType.Farm, null, Era.Camp));
            StringAssert.Contains("woodpile", NameData.FirstBuildingLine(BuildingType.Lumbermill, null, Era.Camp));
            StringAssert.Contains("field of grain", NameData.FirstBuildingLine(BuildingType.Farm, null, Era.Village));
        }
    }
}
