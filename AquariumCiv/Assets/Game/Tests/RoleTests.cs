using System.Collections.Generic;
using System.Linq;
using AquariumCiv.Sim;
using NUnit.Framework;
using UnityEngine;
using static AquariumCiv.Tests.SimCoreTests;

namespace AquariumCiv.Tests
{
    /// <summary>Important roles (#27, docs/design/notable-roles.md §2–§5): camp 1, village 3, town 7; each won its own way;
    /// one role per notable; the lord's title passes in the family; roles are story only (the economy never reads them).</summary>
    public class RoleTests
    {
        /// <summary>Month by month, crossing each era as soon as it is ready (a player who always watches).</summary>
        static IEnumerable<SimCore> Months(SimCore sim, int years)
        {
            for (int m = 0; m < years * SimConfig.MonthsPerYear; m++)
            {
                sim.Step();
                if (sim.State.eraReady) sim.AdvanceEra();
                yield return sim;
            }
        }

        static SimCore Town(int seed)
        {
            var sim = NewSim(seed);
            foreach (var _ in Months(sim, 120)) if (sim.State.era == Era.Town) break;
            Assert.AreEqual(Era.Town, sim.State.era);
            return sim;
        }

        /// <summary>An adult with no role (right after the era moment every adult may hold an office): one is born and grown if needed.</summary>
        static Notable FreeAdult(SimCore sim, Notable not = null)
        {
            var n = sim.State.notables.Where(x => x.alive && x != not && sim.RoleOf(x) == null && x.age >= 16f && x.age < sim.Config.retireAge)
                .OrderBy(x => x.id).FirstOrDefault();
            if (n != null) return n;
            n = sim.DebugBirth();
            n.age = 30f; n.lifespan = 200f; n.job = JobType.Farmer;
            return n;
        }

        static IEnumerable<ChronicleEntry> RoleStories(SimState s) => s.chronicle.Where(e => e.kind.StartsWith("role"));

        static void AssertOneRoleEach(SimCore sim, string when)
        {
            var held = sim.State.roles.Where(r => r.holderId >= 0).Select(r => r.holderId).ToList();
            Assert.AreEqual(held.Count, held.Distinct().Count(), $"{when}: one role per notable ({string.Join(",", held)})");
            foreach (var r in sim.State.roles.Where(r => r.holderId >= 0))
                Assert.IsTrue(sim.FindNotable(r.holderId).alive, $"{when}: {r.role} is held by the dead");
        }

        [TestCase(1)]
        [TestCase(7)]
        [TestCase(1234)]
        public void RolesAreNeverEmptyLongWhileSomeoneCouldStand(int seed)
        {
            var sim = NewSim(seed);
            var emptyFor = new Dictionary<string, int>();
            int worst = 0, rises = 0;
            string worstAt = "";
            foreach (var _ in Months(sim, 120))
            {
                var s = sim.State;
                bool someone = sim.RoleCandidates().Count > 0;
                CollectionAssert.AreEquivalent(SimCore.RolesOf(s.era).Where(sim.RoleOpen), s.roles.Select(r => r.role), "the era's open roles");
                foreach (var seat in s.roles)
                {
                    emptyFor.TryGetValue(seat.role, out int n);
                    n = seat.holderId < 0 && someone ? n + 1 : 0;
                    emptyFor[seat.role] = n;
                    if (n > worst) { worst = n; worstAt = $"{seat.role} at month {s.month}"; }
                }
                foreach (var gone in emptyFor.Keys.Where(k => s.roles.All(r => r.role != k)).ToList()) emptyFor.Remove(gone);
                AssertOneRoleEach(sim, $"seed {seed} month {s.month}");
            }
            rises = sim.State.chronicle.Count(e => e.kind == "role_rise");
            TestContext.WriteLine($"seed {seed}: {sim.State.era}, roles {string.Join(", ", sim.State.roles.Select(r => r.role + (r.holderId < 0 ? " (empty)" : "")))}; " +
                                  $"longest empty seat with a candidate: {worst} months ({worstAt}); {RoleStories(sim.State).Count()} role stories, {rises} from the crowd");
            Assert.AreEqual(Era.Town, sim.State.era, "the eras were advanced");
            Assert.LessOrEqual(worst, SimConfig.MonthsPerYear, "a seat waits at most until the next spring");
            Assert.GreaterOrEqual(sim.State.roles.Count, 5, "the town has most of its offices");
        }

        [TestCase(1)]
        [TestCase(7)]
        [TestCase(1234)]
        public void ACenturyMakesRoleStories(int seed)
        {
            var sim = NewSim(seed);
            foreach (var _ in Months(sim, 120)) { }
            var stories = RoleStories(sim.State).ToList();
            foreach (var e in stories.Take(15)) TestContext.WriteLine($"{e.month / 12,4} {e.kind} ({e.salience:0.00}): {e.text}");
            TestContext.WriteLine("kinds: " + string.Join(", ", stories.GroupBy(e => e.kind).Select(g => $"{g.Key} {g.Count()}")));
            Assert.GreaterOrEqual(stories.Count, 3);
            Assert.IsTrue(stories.All(e => e.salience >= 0.55f && e.salience <= 0.9f), "role lines rank among the chronicle's important cards");
            Assert.IsTrue(stories.All(e => e.notableId >= 0), "every role line is about someone");
            Assert.IsTrue(stories.All(e => !e.text.Contains("{")), "templates are filled");
        }

        [Test]
        public void TheFoundersChooseAChief()
        {
            for (int seed = 1; seed <= 20; seed++)
            {
                var sim = NewSim(seed);
                var chief = sim.HolderOf(RoleType.Chief);
                Assert.IsNotNull(chief, $"seed {seed}");
                Assert.AreEqual(RoleType.Chief, sim.RoleOf(chief));
                var line = sim.State.chronicle.Last(e => e.kind.StartsWith("role"));
                Assert.AreEqual(chief.id, line.notableId);
                Assert.AreEqual(sim.SeatBuilding(RoleType.Chief).id, line.buildingId, "fly-to the seat");
            }
        }

        [Test]
        public void TraitsWantTheirRoles()
        {
            int aspiringChiefs = 0, chiefs = 0, aspiringFounders = 0, founders = 0;
            for (int seed = 1; seed <= 300; seed++)
            {
                var sim = NewSim(seed);
                chiefs++;
                if (SimCore.Aspires(sim.HolderOf(RoleType.Chief), RoleType.Chief)) aspiringChiefs++;
                foreach (var n in sim.State.notables) { founders++; if (SimCore.Aspires(n, RoleType.Chief)) aspiringFounders++; }
            }
            float a = aspiringChiefs / (float)chiefs, b = aspiringFounders / (float)founders;
            TestContext.WriteLine($"chiefs who wanted it: {a:P0}; founders who want it: {b:P0}");
            Assert.Greater(a, b * 1.5f);
        }

        [Test]
        public void TheLeaderCarriesOverChiefHeadmanLord()
        {
            var sim = NewSim(3);
            sim.Advance(5);
            var chief = sim.HolderOf(RoleType.Chief);
            Assert.IsNotNull(chief);
            chief.lifespan = 200f; chief.age = 30f;
            Assert.IsTrue(sim.ForceNextEra());
            Assert.AreEqual(chief, sim.HolderOf(RoleType.Headman));
            Assert.IsNull(sim.SeatOf(RoleType.Chief), "the camp's role is gone");
            StringAssert.Contains($"chief {chief.FullName} became its first headman", sim.State.chronicle.Last(e => e.kind == "role_era").text);
            Assert.IsTrue(sim.ForceNextEra());
            Assert.AreEqual(chief, sim.HolderOf(RoleType.Lord));
            StringAssert.Contains($"headman {chief.FullName} became its first lord", sim.State.chronicle.Last(e => e.kind == "role_era").text);
            AssertOneRoleEach(sim, "town");
        }

        [Test]
        public void TheEraMomentFillsNewOfficesInOneLine()
        {
            var sim = Town(7);
            long m = sim.State.month;
            var lines = sim.State.chronicle.Where(e => e.month == m && e.kind.StartsWith("role")).ToList();
            Assert.AreEqual(1, lines.Count(e => e.kind == "role_era"));
            Assert.AreEqual(1, lines.Count(e => e.kind == "role_offices"), string.Join("\n", lines.Select(e => e.text)));
            Assert.IsFalse(lines.Any(e => e.kind == "role_won" || e.kind == "role_appointed"), "no separate line per office");
            StringAssert.StartsWith("The new town needed people for its offices: ", lines.First(e => e.kind == "role_offices").text);
        }

        [Test]
        public void TheLordsTitlePassesInTheFamily()
        {
            var sim = Town(7);
            var lord = sim.HolderOf(RoleType.Lord);
            Assert.IsNotNull(lord);
            // Give the lord a grown child (and a baby), then let the lord die.
            var heir = FreeAdult(sim, lord);
            heir.parentId = lord.id; heir.age = 34f; heir.lifespan = 200f;
            var baby = sim.DebugBirth();
            if (baby != null) baby.parentId = lord.id;
            lord.alive = false;
            lord.diedMonth = sim.State.month;
            sim.Step();
            Assert.AreEqual(heir, sim.HolderOf(RoleType.Lord), "the eldest adult child inherits");
            var line = sim.State.chronicle.Last(e => e.kind == "role_inherited");
            Assert.AreEqual($"When old {lord.FullName} died, their eldest child, {heir.FullName}, became lord of the town.", line.text);
            Assert.AreEqual(lord.id, line.otherNotableId);
            AssertOneRoleEach(sim, "after");
        }

        [TestCase(1)]
        [TestCase(7)]
        [TestCase(1234)]
        public void InALongRunTheTitleStaysInTheFamilyWhileThereIsAnHeir(int seed)
        {
            var sim = NewSim(seed);
            Notable lord = null;
            int passed = 0;
            foreach (var _ in Months(sim, 160))
            {
                var now = sim.HolderOf(RoleType.Lord);
                if (lord != null && now != lord && !lord.alive)
                {
                    // the title moved on a death: if the old lord had kin, it went to them
                    var line = sim.State.chronicle.LastOrDefault(e => e.month == sim.State.month && e.otherNotableId == lord.id && e.kind.StartsWith("role"));
                    if (now != null && line != null && line.kind == "role_inherited") passed++;
                    else Assert.IsNull(sim.Heir(lord).heir, $"seed {seed}: {lord.FullName}'s title left the family with an heir alive");
                }
                if (now != null) lord = now;
            }
            TestContext.WriteLine($"seed {seed}: the title was inherited {passed} times");
        }

        [Test]
        public void AnHeirWithAnotherRoleLeavesItForTheTitle()
        {
            var sim = Town(7);
            var lord = sim.HolderOf(RoleType.Lord);
            var seat = sim.State.roles.First(r => r.role != RoleType.Lord && r.holderId >= 0);
            var heir = sim.FindNotable(seat.holderId);
            heir.parentId = lord.id;
            lord.alive = false;
            sim.Step();
            Assert.AreEqual(heir, sim.HolderOf(RoleType.Lord));
            Assert.AreNotEqual(heir.id, sim.SeatOf(seat.role).holderId, "they left their old role");
            AssertOneRoleEach(sim, "after");
        }

        [Test]
        public void WithNoHeirTheNearestRelativeThenAContest()
        {
            var sim = Town(7);
            var lord = sim.HolderOf(RoleType.Lord);
            foreach (var n in sim.State.notables.Where(n => n != lord))
            {
                if (n.parentId == lord.id || (lord.parentId >= 0 && n.parentId == lord.parentId) || n.id == lord.parentId) n.parentId = -2;
                if (n.family == lord.family) n.family = "Nobody";
            }
            Assert.IsNull(sim.Heir(lord).heir);
            var cousin = FreeAdult(sim, lord);
            cousin.parentId = -2;
            cousin.family = lord.family;
            Assert.AreEqual((cousin, "a cousin"), sim.Heir(lord));
            cousin.family = "Nobody";
            lord.alive = false;
            sim.Step();
            // No one of the blood: the seat waits for spring, then the notables choose.
            while (sim.State.month % 12 != 3) sim.Step();
            Assert.IsNotNull(sim.HolderOf(RoleType.Lord));
            Assert.IsTrue(sim.State.chronicle.Any(e => e.kind == "role_won" && e.text.StartsWith($"With no one of {lord.FullName}'s blood left")));
        }

        [Test]
        public void SuccessionAndAppointment()
        {
            var sim = Town(7);
            // The innkeeper's inn passes to their apprentice.
            var inn = sim.HolderOf(RoleType.Innkeeper);
            Assert.IsNotNull(inn, "seed 7's town has a tavern");
            var apprentice = FreeAdult(sim, inn);
            apprentice.mentorId = inn.id;
            inn.alive = false;
            sim.Step();
            Assert.AreEqual(apprentice, sim.HolderOf(RoleType.Innkeeper));
            StringAssert.Contains($"the inn passed to their apprentice {apprentice.FullName}", sim.State.chronicle.Last(e => e.kind == "role_succession").text);

            // The lord appoints a new captain at once.
            Assert.IsTrue(sim.DebugVacateRole(RoleType.Captain));
            sim.Step();
            var captain = sim.HolderOf(RoleType.Captain);
            Assert.IsNotNull(captain);
            var appointed = sim.State.chronicle.Last(e => e.kind == "role_appointed" || e.kind == "role_rise");
            Assert.AreEqual(captain.id, appointed.notableId);
            if (appointed.kind == "role_appointed") Assert.AreEqual(sim.HolderOf(RoleType.Lord).id, appointed.otherNotableId);
            AssertOneRoleEach(sim, "after");
        }

        [Test]
        public void ThePriestNamesTheNext()
        {
            int named = 0;
            for (int seed = 1; seed <= 40 && named < 3; seed++)
            {
                var sim = NewSim(seed);
                foreach (var _ in Months(sim, 150)) if (sim.HolderOf(RoleType.Priest) != null) break;
                var priest = sim.HolderOf(RoleType.Priest);
                if (priest == null) continue;
                priest.alive = false;
                priest.diedMonth = sim.State.month;
                sim.Step();
                var next = sim.HolderOf(RoleType.Priest);
                if (next == null) continue; // nobody free to name; the spring contest or the crowd fills it
                named++;
                var line = sim.State.chronicle.Last(e => e.kind == "role_named");
                Assert.AreEqual($"Before the end, {priest.FullName} named {next.FullName} the new priest. {next.FullName} has been practising the blessing on the goats.", line.text);
            }
            Assert.GreaterOrEqual(named, 3);
        }

        /// <summary>JobTests.Economy measured on the sim before roles (vs/slice 5d0fcf3): seeds 1 and 1234 watched month by
        /// month for 120 years, and seed 77 stepped 60 years with the village forced at year 25.</summary>
        const uint PreRoles1 = 2496239907u, PreRoles1234 = 1855436489u, PreRoles77 = 2087997378u; // measured on 5d0fcf3 (before roles) with the float-bits hash

        [Test]
        public void TheEconomyIsWhatItWasBeforeRoles()
        {
            // Unless a notable steps out of the crowd (which adds a notable, as #26's rise does), roles leave every dice
            // stream, the economy and the notables exactly as they were.
            var camp = NewSim(1234);
            camp.Advance(48);
            Assert.AreEqual(3155006145u, JobTests.Economy(camp.State), "camp, as before jobs and roles");
            foreach (var (seed, before) in new[] { (1, PreRoles1), (1234, PreRoles1234) })
            {
                var town = NewSim(seed);
                foreach (var _ in Months(town, 120)) { }
                Assert.AreEqual(Era.Town, town.State.era);
                Assert.IsFalse(town.State.chronicle.Any(e => e.kind == "role_rise"), $"seed {seed}: nobody came from the crowd");
                Assert.Greater(RoleStories(town.State).Count(), 10);
                Assert.AreEqual(before, JobTests.Economy(town.State), $"seed {seed}: a century, as before roles");
            }
        }

        [Test]
        public void MeddlingWithRolesLeavesTheEconomyAlone()
        {
            var plain = NewSim(77); var meddled = NewSim(77);
            for (int m = 0; m < 12 * 60; m++)
            {
                if (m % 5 == 0)
                {
                    var seat = meddled.State.roles[m / 5 % meddled.State.roles.Count];
                    if (m % 2 == 0) meddled.DebugVacateRole(seat.role);
                    else meddled.DebugFillRoles();
                }
                plain.Step(); meddled.Step();
                if (m == 12 * 25) { plain.ForceNextEra(); meddled.ForceNextEra(); }
            }
            Assert.IsFalse(meddled.State.chronicle.Any(e => e.kind == "role_rise"), "nobody came from the crowd (that adds a notable)");
            Assert.AreNotEqual(RoleStories(plain.State).Count(), RoleStories(meddled.State).Count(), "the meddling did something");
            Assert.AreEqual(PreRoles77, JobTests.Economy(plain.State), "as before roles");
            Assert.AreEqual(PreRoles77, JobTests.Economy(meddled.State), "meddled");
        }

        [Test]
        public void ThePeopleStepOutOfTheCrowdForAnEmptyRole()
        {
            var sim = NewSim(5);
            foreach (var n in sim.State.notables.Where(n => n.alive)) { n.age = 70f; n.lifespan = 200f; } // all retired: nobody can stand
            Assert.IsTrue(sim.DebugVacateRole(RoleType.Chief));
            uint rng = sim.State.rng.state, story = sim.State.storyRng.state, econ = sim.State.econRng.state;
            int count = sim.State.notables.Count;
            sim.DebugFillRoles();
            var chief = sim.HolderOf(RoleType.Chief);
            Assert.IsNotNull(chief);
            Assert.AreEqual(count + 1, sim.State.notables.Count);
            Assert.AreEqual(chief, sim.State.notables.Last());
            Assert.AreNotEqual(JobType.None, chief.job, "they come with a job");
            Assert.AreEqual((rng, story, econ), (sim.State.rng.state, sim.State.storyRng.state, sim.State.econRng.state), "no draw from any stream");
            StringAssert.Contains("stepped out of the crowd to become chief of the camp", sim.State.chronicle.Last().text);

            // ...but not past the era's band + family room.
            var full = NewSim(5);
            while (full.State.notables.Count(n => n.alive) < full.Config.campNotables.max + full.Config.familyRoom) full.DebugBirth();
            foreach (var n in full.State.notables.Where(n => n.alive && n.age >= 16f)) { n.age = 70f; n.lifespan = 200f; }
            full.DebugVacateRole(RoleType.Chief);
            full.DebugFillRoles();
            Assert.IsNull(full.HolderOf(RoleType.Chief), "no room: the seat waits");
        }

        [Test]
        public void TheChiefStepsDownAtRetirementUnlessStubborn()
        {
            var sim = NewSim(2);
            var chief = sim.HolderOf(RoleType.Chief);
            chief.trait = "the kind";
            chief.age = sim.Config.retireAge - 0.5f; chief.lifespan = 200f;
            sim.Advance(1);
            Assert.AreNotEqual(chief, sim.HolderOf(RoleType.Chief));
            Assert.IsTrue(sim.State.chronicle.Any(e => e.kind == "role_stepdown" && e.notableId == chief.id));

            var stubborn = NewSim(2);
            var old = stubborn.HolderOf(RoleType.Chief);
            old.trait = "the stubborn";
            old.age = stubborn.Config.retireAge - 0.5f; old.lifespan = 200f;
            stubborn.Advance(2);
            Assert.AreEqual(old, stubborn.HolderOf(RoleType.Chief), "hardly ever steps down");
        }

        [Test]
        public void ElectedTermsEndInAnElection()
        {
            var sim = NewSim(4);
            sim.Advance(3);
            sim.ForceNextEra();
            var seat = sim.SeatOf(RoleType.Headman);
            Assert.AreEqual(sim.State.month + 12 * 12, seat.termEndsMonth, "a 12-year term");
            foreach (var n in sim.State.notables.Where(n => n.alive && n.age < 16f)) n.age = 25f; // enough people to stand
            int before = RoleStories(sim.State).Count();
            Assert.IsTrue(sim.DebugEndTerm(RoleType.Headman));
            Assert.IsNotNull(sim.HolderOf(RoleType.Headman));
            Assert.Greater(seat.termEndsMonth, sim.State.month);
            Assert.Greater(RoleStories(sim.State).Count(), before, "re-elected or unseated, it's a story");
        }

        [Test]
        public void ADeathInOfficeLeavesTheSeatEmptyUntilSpring()
        {
            var sim = NewSim(6);
            while (sim.State.month % 12 != 6) sim.Step();
            var chief = sim.HolderOf(RoleType.Chief);
            chief.alive = false;
            sim.Step();
            Assert.IsNull(sim.HolderOf(RoleType.Chief), "the contest comes in spring");
            StringAssert.Contains($"{chief.FullName} died as chief after", sim.State.chronicle.Last(e => e.kind == "role_death").text);
            while (sim.State.month % 12 != 2) sim.Step();
            Assert.IsNotNull(sim.HolderOf(RoleType.Chief), "filled in spring");
        }

        [Test]
        public void RolesAreDeterministic()
        {
            var a = NewSim(21); var b = NewSim(21);
            foreach (var _ in Months(a, 80)) { }
            foreach (var _ in Months(b, 80)) { }
            Assert.AreEqual(JsonUtility.ToJson(a.State), JsonUtility.ToJson(b.State));
        }

        [Test]
        public void RolesAreSaved()
        {
            var sim = NewSim(7);
            foreach (var _ in Months(sim, 50)) { }
            var loaded = new SimCore(sim.Config, JsonUtility.FromJson<SimState>(JsonUtility.ToJson(sim.State)));
            Assert.AreEqual(JsonUtility.ToJson(sim.State.roles), JsonUtility.ToJson(loaded.State.roles));
            Assert.Greater(loaded.State.roles.Count(r => r.holderId >= 0), 2);
            foreach (var _ in Months(sim, 30)) { }
            foreach (var _ in Months(loaded, 30)) { }
            Assert.AreEqual(JsonUtility.ToJson(sim.State), JsonUtility.ToJson(loaded.State), "a loaded box goes on the same way");
        }

        [Test]
        public void ASaveFromBeforeRolesGetsItsRolesNextMonth()
        {
            var sim = NewSim(7);
            foreach (var _ in Months(sim, 20)) { }
            var old = new SimCore(sim.Config, JsonUtility.FromJson<SimState>(JsonUtility.ToJson(sim.State)));
            old.State.roles = null; // what a save without the field may load as
            old.Step();
            Assert.IsNotNull(old.HolderOf(SimCore.LeaderOf(old.State.era)));
            AssertOneRoleEach(old, "after load");
        }

        [Test]
        public void DebugCheats()
        {
            var sim = NewSim(11);
            var n = sim.State.notables.First(x => sim.RoleOf(x) == null);
            Assert.IsTrue(sim.DebugSetRoleHolder(RoleType.Chief, n));
            Assert.AreEqual(n, sim.HolderOf(RoleType.Chief));
            Assert.IsFalse(sim.DebugSetRoleHolder(RoleType.Lord, n), "no lord in a camp");
            Assert.IsTrue(sim.DebugVacateRole(RoleType.Chief));
            Assert.IsNull(sim.HolderOf(RoleType.Chief));
            sim.DebugFillRoles();
            Assert.IsNotNull(sim.HolderOf(RoleType.Chief));
            Assert.IsFalse(sim.DebugEndTerm(RoleType.Chief), "the chief has no term");
        }
    }
}
