using System.Collections.Generic;
using System.Linq;
using AquariumCiv.Sim;
using NUnit.Framework;

namespace AquariumCiv.Life.Tests
{
    /// <summary>#789 (docs/design/animation-specs.md B82.0, approved 2026-11-26): the last evening's rule, pure and against the real sim and planner.
    /// It only attaches to a death the sim has already set for tonight (never moves it), needs an elder with kin or a friend free, plays only while
    /// the Keeper watches, at most once per visit and two game years apart, never over another held moment, one elder of two; the line is History
    /// only (its own id range, read); the elder stays in after it until the night takes them.</summary>
    public class LastEveningTests
    {
        static SimCore Fresh(int seed = 7)
        {
            var cfg = new SimConfig();
            return new SimCore(cfg, SimCore.NewWorld(seed, cfg));
        }

        /// <summary>The month is the last of the year (the next step is the year's turn, where the age roll runs).</summary>
        static void ToTheYearsEnd(SimCore sim)
        {
            while (SeasonCalendar.OfMonth(sim.State.month + 1, sim.State.seasonShift) != 0) sim.State.month++;
        }

        /// <summary>An elder (70) dying at the year's turn and their grown child; nobody else near their end, no other ties to them.</summary>
        static (Notable elder, Notable child) Elder(SimCore sim)
        {
            var alive = sim.State.notables.Where(n => n.alive).OrderBy(n => n.id).ToList();
            Assert.GreaterOrEqual(alive.Count, 3, "the world has people");
            var elder = alive[0]; var child = alive[1];
            foreach (var o in alive) { o.partnerId = -1; o.friendId = -1; if (o.parentId == elder.id) o.parentId = -1; }
            elder.age = 70.2f; elder.lifespan = 70.9f; elder.parentId = -1;
            child.age = 40f; child.lifespan = 90f; child.parentId = elder.id;
            foreach (var o in alive.Skip(2)) { o.age = o.age < SimCore.AdultAge ? o.age : 30f; o.lifespan = 95f; }
            ToTheYearsEnd(sim);
            return (elder, child);
        }

        /// <summary>A grown notable made by hand (the new world has three).</summary>
        static Notable Person(SimCore sim, float age)
        {
            var n = new Notable { id = sim.State.NextId(), name = "P" + sim.State.nextId, family = "Test", alive = true, age = age, lifespan = 95f };
            sim.State.notables.Add(n);
            return n;
        }

        static Notable Choose(SimCore sim, out string why, bool watched = true, bool other = false) => sim.ChooseLastEvening(k => true, watched, other, out why);

        // ------------------------------------------------------------------ the pure rule

        [Test]
        public void TheDeathIsTheSims_OnlyAtTheYearsTurn_OnlyForAnElderWhoseLifespanItReaches()
        {
            var n = new Notable { id = 1, alive = true, age = 70.3f, lifespan = 71f };
            Assert.IsTrue(LastEveningRules.DiesAtTheTurn(n, 0, 1f, 45f), "the age roll at the turn reaches the lifespan");
            Assert.IsFalse(LastEveningRules.DiesAtTheTurn(n, 5, 1f, 45f), "any other month: no death is set tonight");
            n.lifespan = 72f;
            Assert.IsFalse(LastEveningRules.DiesAtTheTurn(n, 0, 1f, 45f), "a year more doesn't reach it");
            var young = new Notable { id = 2, alive = true, age = 40f, lifespan = 40.5f };
            Assert.IsFalse(LastEveningRules.DiesAtTheTurn(young, 0, 1f, 45f), "only an elder");
            n.lifespan = 71f; n.alive = false;
            Assert.IsFalse(LastEveningRules.DiesAtTheTurn(n, 0, 1f, 45f), "nor the dead");
        }

        [Test]
        public void TheRule_NeverUnseen_SkippedOverAnotherMoment_OncePerVisit_TwoYearsApart_NeedsKin()
        {
            Assert.IsNull(LastEveningRules.Why(true, false, 100, -1, 3, -1, 1), "never played: it may");
            StringAssert.Contains("away", LastEveningRules.Why(false, false, 100, -1, 3, -1, 1));
            StringAssert.Contains("skipped", LastEveningRules.Why(true, true, 100, -1, 3, -1, 1));
            StringAssert.Contains("this visit", LastEveningRules.Why(true, false, 200, 100, 3, 3, 1), "the same visit, even years later");
            StringAssert.Contains("two years", LastEveningRules.Why(true, false, 100 + LastEveningRules.SpacingMonths - 1, 100, 4, 3, 1));
            Assert.IsNull(LastEveningRules.Why(true, false, 100 + LastEveningRules.SpacingMonths, 100, 4, 3, 1), "two game years on, another visit");
            StringAssert.Contains("no kin", LastEveningRules.Why(true, false, 100, -1, 3, -1, 0));
            Assert.AreEqual(24, LastEveningRules.SpacingMonths);
        }

        [Test]
        public void Pick_ThePinnedFirst_ElseTheMostKinFree_ElseTheLowerId_NeverOneWithNobody()
        {
            Notable a = new Notable { id = 4 }, b = new Notable { id = 9 }, c = new Notable { id = 2 };
            Assert.AreSame(b, LastEveningRules.Pick(new List<(Notable, int)> { (a, 1), (b, 3), (c, 2) }, -1), "the most kin free");
            Assert.AreSame(a, LastEveningRules.Pick(new List<(Notable, int)> { (a, 1), (b, 3) }, 4), "the pinned one first");
            Assert.AreSame(c, LastEveningRules.Pick(new List<(Notable, int)> { (a, 2), (c, 2) }, -1), "a tie: the lower id");
            Assert.AreSame(b, LastEveningRules.Pick(new List<(Notable, int)> { (a, 0), (b, 1) }, 4), "a pinned elder with nobody free is passed over");
            Assert.IsNull(LastEveningRules.Pick(new List<(Notable, int)> { (a, 0) }, -1));
        }

        [Test]
        public void TheCaption_NamesWhoIsWithThem_NeverADeath()
        {
            Assert.AreEqual("Ada looks up at the stars with Bo.", LastEveningRules.Caption("Ada", "Bo", true));
            Assert.AreEqual("Ada looks up at the stars with the family.", LastEveningRules.Caption("Ada", null, true));
            Assert.AreEqual("Ada looks up at the stars with friends.", LastEveningRules.Caption("Ada", null, false));
            foreach (var w in new[] { "die", "death", "last", "end", "goodbye" })
                StringAssert.DoesNotContain(w, LastEveningRules.Caption("Ada", "Bo", true).ToLowerInvariant());
        }

        // ------------------------------------------------------------------ the sim

        [Test]
        public void TheElderDyingTonight_IsChosen_OnlyWhileWatched_AndNotOverAnotherMoment()
        {
            var sim = Fresh();
            var (elder, child) = Elder(sim);
            CollectionAssert.AreEqual(new[] { elder.id }, sim.LastEveningElders().Select(n => n.id).ToArray());
            Assert.AreSame(elder, Choose(sim, out string why), why);
            Assert.IsNull(Choose(sim, out why, watched: false), "never unseen");
            StringAssert.Contains("away", why);
            Assert.IsNull(Choose(sim, out why, other: true), "another moment is playing: skipped");
            Assert.IsNull(sim.ChooseLastEvening(k => k != child.id, true, false, out why), "nobody free");
            StringAssert.Contains("no kin", why);
            sim.State.month -= 3; // not the last evening of the year
            Assert.AreEqual(0, sim.LastEveningElders().Count, "no death is set for tonight");
            Assert.IsNull(Choose(sim, out _));
        }

        [Test]
        public void Marking_StampsWho_WritesOneReadHistoryLineInItsOwnIdRange_AndNeverMovesTheDeath()
        {
            var sim = Fresh();
            var (elder, child) = Elder(sim);
            float life = elder.lifespan; int shared = sim.State.nextId;
            var e = sim.MarkLastEvening(elder, new List<int> { child.id }, 40, 19.5f);
            Assert.AreEqual(SimCore.LastEveningKind, e.kind);
            Assert.GreaterOrEqual(e.id, SimCore.LastEveningLineIdBase, "its own id range");
            Assert.AreEqual(shared, sim.State.nextId, "the shared ids never shift");
            Assert.AreEqual($"{elder.name} looks up at the stars with the family.", e.text);
            Assert.IsTrue(sim.State.cards.read.Contains(e.id), "watched: never a return card");
            var stack = sim.UnreadCardsCapped(10);
            Assert.IsFalse(stack.cards.Concat(stack.also).Any(c => c.entries.Contains(e)), "not in the return stack");
            Assert.AreEqual(elder.id, sim.State.lastEveningFor);
            Assert.AreEqual(life, elder.lifespan, "the death is not moved");
            sim.Step();
            Assert.IsFalse(elder.alive, "the night takes them, as the age roll set");
            Assert.AreEqual(1, sim.State.chronicle.Count(x => x.kind == "death" && x.notableId == elder.id), "the death line as today");
        }

        [Test]
        public void OncePerVisit_AndTwoGameYearsApart()
        {
            var sim = Fresh();
            var (elder, child) = Elder(sim);
            sim.MomentPacing.visits = 3;
            sim.MarkLastEvening(elder, new List<int> { child.id }, 40, 19.5f);
            // a second elder in their last evening the same night (the first is stamped): nothing this visit
            var other = sim.State.notables.Where(n => n.alive && n.id != elder.id && n.id != child.id && n.age >= SimCore.AdultAge).OrderBy(n => n.id).First();
            other.age = 71.2f; other.lifespan = 71.8f; child.friendId = other.id;
            Assert.IsNull(sim.ChooseLastEvening(k => true, true, false, out string why), "one per visit");
            StringAssert.Contains("this visit", why);
            sim.MomentPacing.visits = 4;
            Assert.IsNull(Choose(sim, out why), "a new visit, but within two years");
            StringAssert.Contains("two years", why);
            sim.State.month += LastEveningRules.SpacingMonths;
            ToTheYearsEnd(sim);
            elder.alive = false; // the night took them
            other.age = 71.2f; other.lifespan = 71.8f; other.alive = true;
            Assert.AreSame(other, Choose(sim, out why), "two game years on, a new visit: it may again (" + why + ")");
        }

        [Test]
        public void TwoEldersTheSameNight_OneIsChosen_TheOneWithMoreKinFree()
        {
            var sim = Fresh(11);
            var (elder, child) = Elder(sim);
            var alive = new List<Notable> { Person(sim, 30f), Person(sim, 35f), Person(sim, 33f) };
            var second = alive[0];
            second.age = 72.1f; second.lifespan = 72.5f; second.parentId = -1;
            alive[1].parentId = second.id; alive[1].age = 35f; alive[2].parentId = second.id; alive[2].age = 33f; // two children
            var both = sim.LastEveningElders();
            Assert.AreEqual(2, both.Count, "two in their last evening");
            Assert.AreSame(second, Choose(sim, out _), "the more kin free");
            sim.State.pinnedNotableId = elder.id;
            Assert.AreSame(elder, Choose(sim, out _), "the pinned first");
            sim.MarkLastEvening(elder, new List<int> { child.id }, 40, 19.5f);
            Assert.IsNull(Choose(sim, out _), "and the other gets nothing");
        }

        [Test]
        public void NoKinNoFriend_NoScene_TheyDieInTheNightAsToday()
        {
            var sim = Fresh();
            var (elder, child) = Elder(sim);
            child.parentId = -1; // now nobody is tied to them
            Assert.AreEqual(0, sim.KinAndFriendsOf(elder).Count);
            Assert.IsNull(Choose(sim, out string why));
            StringAssert.Contains("no kin", why);
            sim.Step();
            Assert.IsFalse(elder.alive);
            Assert.AreEqual(-1, sim.State.lastEveningFor, "nothing stamped");
            Assert.AreEqual(0, sim.State.chronicle.Count(x => x.kind == SimCore.LastEveningKind), "nothing said");
        }

        [Test]
        public void KinAndFriends_ThePartnerFirst_ThenChildrenGrandchildrenSiblings_ThenFriends()
        {
            var sim = Fresh(13);
            var (elder, child) = Elder(sim);
            var o = new List<Notable> { Person(sim, 50f), Person(sim, 66f), Person(sim, 9f) };
            var friend = o[0]; var partner = o[1]; var grand = o[2];
            friend.friendId = elder.id;
            elder.partnerId = partner.id; partner.partnerId = elder.id;
            grand.parentId = child.id; grand.age = 9f;
            CollectionAssert.AreEqual(new[] { partner.id, child.id, grand.id, friend.id }, sim.KinAndFriendsOf(elder).ToArray());
            Assert.IsTrue(sim.IsKinOf(elder, grand.id)); Assert.IsFalse(sim.IsKinOf(elder, friend.id));
            var e = sim.MarkLastEvening(elder, new List<int> { friend.id, partner.id }, 40, 19.5f);
            Assert.AreEqual($"{elder.name} looks up at the stars with {partner.name}.", e.text, "the partner by name");
        }

        [Test]
        public void TheShared_IdsAndDice_AreTheSameWithOrWithoutIt()
        {
            var a = Fresh(17); var b = Fresh(17);
            var (ea, ca) = Elder(a); Elder(b);
            a.MarkLastEvening(ea, new List<int> { ca.id }, 40, 19.5f);
            for (int i = 0; i < 36; i++) { a.Step(); b.Step(); }
            var la = a.State.chronicle.Where(x => x.kind != SimCore.LastEveningKind).Select(x => (x.id, x.month, x.text)).ToList();
            var lb = b.State.chronicle.Select(x => (x.id, x.month, x.text)).ToList();
            CollectionAssert.AreEqual(lb, la, "the same box either way, but for the one line");
            Assert.AreEqual(b.State.population, a.State.population);
        }

        [Test]
        public void TheStamp_IsSaved()
        {
            var sim = Fresh();
            var (elder, child) = Elder(sim);
            sim.MomentPacing.visits = 2;
            sim.MarkLastEvening(elder, new List<int> { child.id }, 40, 19.5f);
            var loaded = new SimCore(sim.Config, UnityEngine.JsonUtility.FromJson<SimState>(UnityEngine.JsonUtility.ToJson(sim.State)));
            Assert.AreEqual(elder.id, loaded.State.lastEveningFor);
            Assert.AreEqual(sim.State.month, loaded.State.lastEveningMonth);
            Assert.AreEqual(2, loaded.State.lastEveningVisit);
            Assert.AreEqual(40, loaded.State.lastEveningDay);
            Assert.AreEqual(19.5f, loaded.State.lastEveningHour, 1e-4f);
            Assert.AreEqual(SimCore.LastEveningLineIdBase + 1, loaded.State.lastEveningNextId);
            var old = UnityEngine.JsonUtility.FromJson<SimState>("{}");
            Assert.AreEqual(-1, old.lastEveningFor, "an old save: never played");
            Assert.AreEqual(-1, old.lastEveningMonth);
        }

        // ------------------------------------------------------------------ the planner: they stay in after

        [Test]
        public void AfterTheDoor_TheElderStaysIn_UntilTheNightTakesThem_NobodyElseChanges()
        {
            var sim = Fresh();
            var (elder, child) = Elder(sim);
            var planner = new LifePlanner(sim);
            long day = sim.State.month;
            sim.MarkLastEvening(elder, new List<int> { child.id }, day, 19.5f);
            planner.Invalidate();
            Assert.IsTrue(sim.StaysInAfterLastEvening(elder.id, day, 19.6f, out bool all) && !all);
            Assert.IsFalse(sim.StaysInAfterLastEvening(elder.id, day, 18f, out _), "before the door: their evening as it was");
            var today = Day(planner, elder.id, day);
            var late = today.First(a => a.start <= 20f && 20f < a.end);
            Assert.AreEqual(ActivityKind.Sleep, late.kind, "in after the door");
            var next = Day(planner, elder.id, day + 1);
            Assert.IsTrue(next.Where(a => a.start < 23f).All(a => a.kind is ActivityKind.Indoors or ActivityKind.Sleep && !a.moving), "the next day they never come out: " + string.Join(", ", next.Select(a => $"{a.kind}@{a.start:0.0}")));
            Assert.IsFalse(Day(planner, child.id, day + 1).Any(a => a.kind == ActivityKind.SitWithOld && a.partner == PersonRef.Notable(elder.id)), "nobody sits at an empty door (#788's visits keep to the elder's seat)");
            Assert.IsFalse(sim.StaysInAfterLastEvening(child.id, day + 1, 12f, out _));
        }

        static List<PersonActivity> Day(LifePlanner planner, int id, long day)
        {
            var buf = new List<PersonActivity>();
            planner.DaySchedule(PersonRef.Notable(id), day, buf);
            return buf;
        }
    }
}
