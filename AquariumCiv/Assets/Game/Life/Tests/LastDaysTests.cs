using System.Collections.Generic;
using System.Linq;
using AquariumCiv.Sim;
using NUnit.Framework;

namespace AquariumCiv.Life.Tests
{
    /// <summary>#788, #790 (research 23 §3 A and E; decisions 2026-11-26): the last days. An elder within about two years of their hidden
    /// lifespan is frail: the rule is a window of 1.5 to 2.5 years that rises by degrees (never a date, never a number shown), and the
    /// planner bends the elder's existing day: slower walks, longer sits at their own stool, home earlier, family beside them, all with
    /// uses the box already has. Outside the window nothing changes.</summary>
    public class LastDaysTests
    {
        static SimCore Fresh(int seed = 5)
        {
            var cfg = new SimConfig();
            return new SimCore(cfg, SimCore.NewWorld(seed, cfg));
        }

        /// <summary>Grandmother and her grown child, set up by hand: the sim's links are the real ones, the ages are ours.</summary>
        static (Notable elder, Notable child) Pair(SimCore sim, float lifespanLeft)
        {
            var alive = sim.State.notables.Where(n => n.alive).OrderBy(n => n.id).ToList();
            Assert.GreaterOrEqual(alive.Count, 2, "the world has people");
            var elder = alive[0];
            var child = alive[1];
            elder.age = sim.Config.retireAge + 7f;
            elder.lifespan = elder.age + lifespanLeft;
            elder.job = JobType.Elder;
            elder.partnerId = -1;
            child.age = 40f; child.lifespan = 90f;
            child.parentId = elder.id;
            child.partnerId = -1; child.job = child.job == JobType.Elder ? "" : child.job;
            foreach (var o in alive.Skip(2)) { o.age = o.age < SimCore.AdultAge ? o.age : 30f; o.lifespan = 95f; } // nobody else is near their end
            return (elder, child);
        }

        static List<PersonActivity> Day(LifePlanner planner, int id, long day)
        {
            var buf = new List<PersonActivity>();
            planner.DaySchedule(PersonRef.Notable(id), day, buf);
            return buf;
        }

        static PersonActivity At(List<PersonActivity> day, float h) => day.FirstOrDefault(a => a.start <= h && h < a.end);

        /// <summary>Hours between 06:00 and 18:00 sitting on the stool at their own door.</summary>
        static float StoolHours(List<PersonActivity> day, Place home)
        {
            float t = 0f;
            foreach (var a in day)
            {
                if (a.kind != ActivityKind.SitOut || a.moving || a.place != home || a.prop != Prop.Stool) continue;
                t += System.Math.Max(0f, System.Math.Min(a.end, 18f) - System.Math.Max(a.start, 6f));
            }
            return t;
        }

        /// <summary>How many places the day's sits and stays move between (a walk each).</summary>
        static int Walks(List<PersonActivity> day) => day.Count(a => a.kind == ActivityKind.Walk && a.moving && a.start >= 6f && a.end <= 18f);

        // ------------------------------------------------------------------ the rule (sim)

        [Test]
        public void TheWindowIsAboutTwoYearsAndNeverTheSameForEveryone()
        {
            var seen = new HashSet<float>();
            for (int id = 0; id < 400; id++)
            {
                float w = SimCore.FrailWindowOf(id);
                Assert.GreaterOrEqual(w, SimCore.FrailWindowYears - SimCore.FrailWindowSpread - 1e-4f, $"id {id}");
                Assert.LessOrEqual(w, SimCore.FrailWindowYears + SimCore.FrailWindowSpread + 1e-4f, $"id {id}");
                Assert.AreEqual(w, SimCore.FrailWindowOf(id), "the same every time: no dice");
                seen.Add(w);
            }
            Assert.Greater(seen.Count, 20, "a window, not one date for all");
        }

        [Test]
        public void FrailtyRisesByDegreesAcrossTheWindowAndIsZeroOutsideIt()
        {
            var sim = Fresh();
            var (e, _) = Pair(sim, 50f);
            float w = SimCore.FrailWindowOf(e.id);
            Assert.AreEqual(0f, sim.FrailtyOf(e), "far from the end: nothing");
            e.lifespan = e.age + w + 0.01f;
            Assert.AreEqual(0f, sim.FrailtyOf(e), "just outside the window: nothing");
            Assert.IsFalse(sim.IsFrail(e));
            float last = 0f;
            for (float left = w - 0.01f; left > 0.05f; left -= 0.1f)
            {
                e.lifespan = e.age + left;
                float f = sim.FrailtyOf(e);
                Assert.Greater(f, 0f, $"{left:0.00} years left");
                Assert.LessOrEqual(f, 1f);
                Assert.GreaterOrEqual(f, last, "it only rises: no jump back");
                Assert.LessOrEqual(f - last, 0.2f, "by degrees, no switch at a date");
                last = f;
            }
            Assert.Greater(last, 0.85f, "near the end it is nearly full");
            e.lifespan = e.age; // their time: gone, not frail
            Assert.AreEqual(0f, sim.FrailtyOf(e));
        }

        [Test]
        public void OnlyALivingElderIsFrail()
        {
            var sim = Fresh();
            var (e, c) = Pair(sim, 0.5f);
            Assert.IsTrue(sim.IsFrail(e));
            c.lifespan = c.age + 0.5f; // a grown-up near the end of a short life is not an elder
            Assert.IsFalse(sim.IsFrail(c), "an adult of 40 has no last days shown");
            e.alive = false;
            Assert.IsFalse(sim.IsFrail(e), "the dead are not frail");
            Assert.AreEqual("", sim.FrailLine(e));
            Assert.AreEqual("", sim.FrailLine(null));
            Assert.AreEqual(0f, sim.FrailtyOf(-1));
        }

        [Test]
        public void TheCardAndTheTagSayItSoftlyWithNoNumber()
        {
            var sim = Fresh();
            var (e, c) = Pair(sim, 50f);
            Assert.AreEqual("", sim.PersonCardOf(e.id).frailLine, "no sign outside the window");
            Assert.AreEqual("", sim.FrailTagWord(e));
            e.lifespan = e.age + SimCore.FrailWindowOf(e.id) * 0.5f;
            var card = sim.PersonCardOf(e.id);
            StringAssert.StartsWith("Slowing down", card.frailLine);
            Assert.IsFalse(card.frailLine.Any(char.IsDigit), "no number, no date: " + card.frailLine);
            StringAssert.Contains("family", card.frailLine, "has a child: the family comes by");
            Assert.AreEqual("slowing", sim.FrailTagWord(e));
            e.parentId = -1; c.parentId = -1; // alone
            var alone = sim.FrailLine(e);
            StringAssert.StartsWith("Slowing down", alone);
            StringAssert.DoesNotContain("family", alone);
            Assert.AreEqual("", sim.PersonCardOf(c.id).frailLine, "the child is not frail");
        }

        [Test]
        public void TheGlanceLineIsSaidOnceAndReadsAsACard()
        {
            var sim = Fresh();
            var (e, _) = Pair(sim, 50f);
            int before = sim.State.chronicle.Count(x => x.kind == SimCore.SlowingKind);
            sim.DebugLastDays(e.id);
            var lines = sim.State.chronicle.Where(x => x.kind == SimCore.SlowingKind && x.notableId == e.id).ToList();
            Assert.AreEqual(before + 1, sim.State.chronicle.Count(x => x.kind == SimCore.SlowingKind), "said once");
            Assert.AreEqual(1, lines.Count);
            StringAssert.Contains("slowing down", lines[0].text);
            Assert.AreEqual($"{e.name} is slowing down", sim.Headline(lines[0]), "the glance line");
            Assert.IsTrue(SimCore.ShownAsCard(lines[0]));
            Assert.AreEqual(CardTone.Neutral, SimCore.ToneOf(lines[0].kind));
            sim.DebugLastDays(e.id);
            Assert.AreEqual(1, sim.State.chronicle.Count(x => x.kind == SimCore.SlowingKind && x.notableId == e.id), "never twice");
            foreach (var l in new[] { lines[0].text, sim.Headline(lines[0]), sim.FrailLine(e) }) // the people live in the box: nothing outside it
                foreach (var word in new[] { "world", "sky", "sea", "city", "outside", "die", "death", "dying" })
                    StringAssert.DoesNotContain(word, l.ToLowerInvariant());
        }

        [Test]
        public void ThroughAWholeLifeTheLineComesOncePerElderAndBeforeTheirDeath()
        {
            var sim = Fresh(11);
            for (int m = 0; m < SimConfig.MonthsPerYear * 160; m++)
            {
                sim.Step();
                if (sim.State.eraReady) sim.AdvanceEra();
                foreach (var n in sim.State.notables)
                {
                    float f = sim.FrailtyOf(n);
                    float left = n.lifespan - n.age;
                    if (f > 0f) Assert.LessOrEqual(left, SimCore.FrailWindowOf(n.id) + 1e-3f, $"{n.name} is frail only inside the window");
                    if (left > SimCore.FrailWindowYears + SimCore.FrailWindowSpread) Assert.AreEqual(0f, f, $"{n.name}: far from the end, no frailty");
                    if (f > 0f) Assert.IsTrue(sim.IsElderStage(n) && n.alive, "frail means a living elder");
                }
            }
            var slow = sim.State.chronicle.Where(x => x.kind == SimCore.SlowingKind).ToList();
            Assert.Greater(slow.Count, 0, "over 160 years some elders reach their last days");
            Assert.AreEqual(slow.Count, slow.Select(x => x.notableId).Distinct().Count(), "once each");
            foreach (var l in slow)
            {
                var death = sim.State.chronicle.FirstOrDefault(x => x.kind == "death" && x.notableId == l.notableId);
                if (death != null) Assert.LessOrEqual(l.month, death.month, "the glance line comes before the death card");
            }
        }

        // ------------------------------------------------------------------ the day (planner)

        [Test]
        public void AFrailElderWalksSlowerAndOnlyInsideTheirLastDays()
        {
            var sim = Fresh();
            var (e, _) = Pair(sim, 50f);
            var planner = LifePlanner.Wired(sim);
            Assert.AreEqual(1f, planner.PaceOf(PersonRef.Notable(e.id)), "healthy: the usual pace");
            Assert.AreEqual(0f, planner.FrailtyOf(PersonRef.Notable(e.id)));
            e.lifespan = e.age + SimCore.FrailWindowOf(e.id) * 0.5f;
            planner = LifePlanner.Wired(sim); // a planner reads the box when it is asked
            float f = planner.FrailtyOf(PersonRef.Notable(e.id));
            Assert.Greater(f, 0f);
            float pace = planner.PaceOf(PersonRef.Notable(e.id));
            Assert.AreEqual(1f + LifePlanner.FrailSlowest * f, pace, 1e-4f);
            Assert.Greater(pace, 1.05f, "slower");
            Assert.LessOrEqual(pace, 1f + LifePlanner.FrailSlowest + 1e-4f, "gently: never more than half as long again");
            foreach (var o in sim.State.notables.Where(n => n.alive && n.id != e.id))
                Assert.AreEqual(1f, planner.PaceOf(PersonRef.Notable(o.id)), $"{o.name} is not in their last days");
        }

        [Test]
        public void AFrailElderSitsLongerAtTheirOwnStoolAndWalksLess()
        {
            float healthyStool = 0f, frailStool = 0f; int healthyWalks = 0, frailWalks = 0;
            const int days = 8;
            foreach (bool frail in new[] { false, true })
            {
                var sim = Fresh();
                var (e, _) = Pair(sim, frail ? 0.05f : 50f);
                var planner = LifePlanner.Wired(sim);
                var who = PersonRef.Notable(e.id);
                Assert.AreEqual(frail, planner.FrailtyOf(who) > 0.9f);
                for (long d = 2; d < 2 + days; d++)
                {
                    var day = Day(planner, e.id, d);
                    float stool = StoolHours(day, At(day, 3f).place); // asleep at home at 03:00
                    int walks = Walks(day);
                    if (frail) { frailStool += stool; frailWalks += walks; } else { healthyStool += stool; healthyWalks += walks; }
                }
            }
            TestContext.WriteLine($"stool hours per day: healthy {healthyStool / days:0.0}, frail {frailStool / days:0.0}; walks per day: healthy {healthyWalks / (float)days:0.0}, frail {frailWalks / (float)days:0.0}");
            Assert.Greater(frailStool / days, 7f, "most of the day on their own stool");
            Assert.Greater(frailStool, healthyStool + 2f * days, "sits longer than the same elder in health");
            Assert.Less(frailWalks, healthyWalks, "walks about less");
        }

        [Test]
        public void ItRisesWithTheLastDaysNotAtOnce()
        {
            // The same elder at three points of the window: the time at the stool never falls as the end nears.
            float last = -1f;
            foreach (float share in new[] { 0.95f, 0.6f, 0.2f })
            {
                var sim = Fresh();
                var (e, _) = Pair(sim, 50f);
                e.lifespan = e.age + SimCore.FrailWindowOf(e.id) * share;
                var planner = LifePlanner.Wired(sim);
                float t = 0f;
                for (long d = 2; d < 14; d++) { var day = Day(planner, e.id, d); t += StoolHours(day, At(day, 3f).place); }
                t /= 12f;
                TestContext.WriteLine($"{share:0.00} of the window left: {t:0.0} h at the stool");
                Assert.GreaterOrEqual(t + 0.8f, last, "never less as the end nears (a little noise from the dice)");
                last = t;
            }
        }

        [Test]
        public void TheFamilySitsBesideAFrailElderAtTheirDoorAndNotOtherwise()
        {
            int besideFrail = 0, besideHealthy = 0, days = 0;
            float hoursFrail = 0f;
            foreach (bool frail in new[] { false, true })
            {
                var sim = Fresh();
                var (e, c) = Pair(sim, frail ? 0.4f : 50f);
                var planner = LifePlanner.Wired(sim);
                if (frail) Assert.Greater(planner.FrailtyOf(PersonRef.Notable(e.id)), 0f);
                for (long d = 2; d < 14; d++)
                {
                    var eld = Day(planner, e.id, d);
                    var kid = Day(planner, c.id, d);
                    bool beside = false;
                    float together = 0f;
                    for (float h = 16.9f; h < 17.8f; h += 0.05f)
                    {
                        var ae = At(eld, h); var ac = At(kid, h);
                        if (ae.kind == ActivityKind.None || ac.kind == ActivityKind.None) continue;
                        bool near = !ae.moving && !ac.moving && ae.place == ac.place;
                        if (near) together += 0.05f;
                        if (near && ac.kind == ActivityKind.SitWithOld && ac.partner == PersonRef.Notable(e.id)) beside = true;
                    }
                    if (frail) { days++; if (beside) besideFrail++; hoursFrail += together; }
                    else if (beside) besideHealthy++;
                }
            }
            TestContext.WriteLine($"family beside a frail elder on {besideFrail} of {days} days ({hoursFrail / days:0.00} h a day), healthy: {besideHealthy}");
            Assert.GreaterOrEqual(besideFrail, days * 2 / 3, "most afternoons the family is beside them at their door");
            Assert.GreaterOrEqual(hoursFrail / days, 0.5f, "for a good part of the spell");
            Assert.AreEqual(0, besideHealthy, "a healthy elder is not sat beside this way");
        }

        [Test]
        public void ThePartnerAndTheChildrenAlsoComeAndTheMorningHasItsSpell()
        {
            var sim = Fresh();
            var (e, c) = Pair(sim, 0.4f);
            var third = sim.State.notables.First(n => n.alive && n.id != e.id && n.id != c.id);
            third.parentId = e.id; third.age = 12f; // a grandchild-age child of the elder
            var planner = LifePlanner.Wired(sim);
            int morning = 0, kidBeside = 0, n = 0;
            for (long d = 2; d < 14; d++)
            {
                n++;
                var kid = Day(planner, third.id, d);
                var m = At(kid, 10.5f); var a = At(kid, 17.3f);
                if (m.kind == ActivityKind.SitWithOld && m.partner == PersonRef.Notable(e.id)) morning++;
                if (a.kind == ActivityKind.SitWithOld && a.partner == PersonRef.Notable(e.id)) kidBeside++;
            }
            TestContext.WriteLine($"a child of the house: afternoon beside them {kidBeside}/{n}, morning {morning}/{n}");
            Assert.Greater(kidBeside, n / 2, "the young sit with them in the afternoon");
            Assert.Greater(morning, 0, "and sometimes in the morning too");
        }

        [Test]
        public void NobodyIsFrailOrVisitedInAFreshBoxAndNothingChangesForThem()
        {
            var sim = Fresh(9);
            var planner = LifePlanner.Wired(sim);
            foreach (var who in planner.People)
            {
                Assert.AreEqual(0f, planner.FrailtyOf(who));
                Assert.AreEqual(1f, planner.PaceOf(who));
            }
            for (long d = 1; d < 6; d++)
                foreach (var n in sim.State.notables.Where(x => x.alive))
                {
                    var day = Day(planner, n.id, d);
                    Assert.IsFalse(day.Any(a => a.kind == ActivityKind.SitWithOld && a.partner != PersonRef.None && a.start < 11f && a.end > 10.1f && At(Day(planner, a.partner.Key, d), 10.5f).place == a.place && a.place.kind == PlaceKind.Home),
                        "no visit to a door when nobody is frail");
                }
        }

        [Test]
        public void TheCheatPutsAnElderIntoTheWindow()
        {
            var sim = Fresh(7);
            var oldest = sim.State.notables.Where(n => n.alive).OrderByDescending(n => n.age).First();
            var got = sim.DebugLastDays();
            Assert.AreEqual(oldest.id, got.id, "no pin: the oldest");
            Assert.IsTrue(sim.IsElderStage(got));
            Assert.IsTrue(sim.IsFrail(got));
            var other = sim.State.notables.First(n => n.alive && n.id != got.id);
            sim.Pin(other.id);
            sim.DebugLastDays(other.id);
            Assert.IsTrue(sim.IsFrail(other), "a chosen one");
            Assert.AreEqual(other.id, sim.DebugLastDays().id, "a pinned elder is the default");
            var planner = LifePlanner.Wired(sim);
            Assert.Greater(planner.PaceOf(PersonRef.Notable(other.id)), 1f);
        }
    }
}
