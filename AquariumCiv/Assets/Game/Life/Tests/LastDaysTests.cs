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

        /// <summary>The family's spell today at the elder's door (the day's own window), or none.</summary>
        static bool Spell(LifePlanner pl, Notable e, long d, bool morning, out float s, out float en) => pl.VisitSpell(PersonRef.Notable(e.id), d, morning, out s, out en);

        [Test]
        public void TheFamilySitsBesideAFrailElderAtTheirDoorInTheDaysOwnSpellAndNotOtherwise()
        {
            int visitDays = 0, days = 0, besideHealthy = 0;
            foreach (bool frail in new[] { false, true })
            {
                var sim = Fresh();
                var (e, c) = Pair(sim, frail ? 0.4f : 50f);
                var planner = LifePlanner.Wired(sim);
                for (long d = 2; d < 22; d++)
                {
                    var eld = Day(planner, e.id, d);
                    var kid = Day(planner, c.id, d);
                    bool has = Spell(planner, e, d, false, out float s, out float en);
                    if (frail) days++;
                    if (frail && !has) continue;
                    if (!frail) { s = 16.5f; en = 17.4f; }
                    bool beside = false;
                    for (float h = s + 0.05f; h < en - 0.05f; h += 0.05f)
                    {
                        var ae = At(eld, h); var ac = At(kid, h);
                        if (ae.kind == ActivityKind.None || ac.kind == ActivityKind.None) continue;
                        if (ac.kind == ActivityKind.SitWithOld && ac.partner == PersonRef.Notable(e.id) && ac.place == ae.place && !ae.moving) beside = true;
                    }
                    if (frail) { if (beside) visitDays++; } else if (beside) besideHealthy++;
                }
            }
            TestContext.WriteLine($"the only child came on {visitDays} of {days} days");
            Assert.GreaterOrEqual(visitDays, 8, "the family comes on a good share of the days");
            Assert.Less(visitDays, days, "but not every day");
            Assert.AreEqual(0, besideHealthy, "a healthy elder is not sat beside this way");
        }

        [Test]
        public void TheSpellsAreIrregularNotATimetable()
        {
            var sim = Fresh();
            var (e, _) = Pair(sim, 0.4f);
            var planner = LifePlanner.Wired(sim);
            var starts = new List<float>(); int morning = 0, none = 0;
            for (long d = 2; d < 22; d++)
            {
                if (Spell(planner, e, d, false, out float s, out float en))
                {
                    starts.Add(s);
                    Assert.GreaterOrEqual(s, LifePlanner.AfternoonFrom - 1e-3f);
                    Assert.LessOrEqual(en, LifePlanner.AfternoonTo + 1e-3f);
                    Assert.GreaterOrEqual(en - s, 0.4f - 1e-3f);
                    Assert.LessOrEqual(en - s, 1f + 1e-3f);
                }
                else none++;
                if (Spell(planner, e, d, true, out _, out _)) morning++;
            }
            TestContext.WriteLine($"afternoon starts over 20 days: {string.Join(" ", starts.Select(x => x.ToString("0.0")))}; none {none}; mornings {morning}");
            Assert.Greater(starts.Max() - starts.Min(), 1.5f, "the starts spread over the window");
            Assert.GreaterOrEqual(starts.Select(x => (int)System.Math.Floor(x)).Distinct().Count(), 3, "over at least three different hours");
            Assert.Greater(none, 0, "not every day");
            Assert.Greater(starts.Count, 12, "most days");
            Assert.Less(morning, starts.Count, "the morning spell is rarer");
        }

        [Test]
        public void TwoFrailEldersFamiliesDoNotGatherOnTheSameMinute()
        {
            var sim = Fresh();
            var (e1, _) = Pair(sim, 0.4f);
            var alive = sim.State.notables.Where(n => n.alive && n.id != e1.id).OrderBy(n => n.id).ToList();
            var e2 = alive[1];
            e2.age = sim.Config.retireAge + 6f; e2.lifespan = e2.age + 0.5f; e2.job = JobType.Elder; e2.partnerId = -1;
            var planner = LifePlanner.Wired(sim);
            Assert.Greater(planner.FrailtyOf(PersonRef.Notable(e2.id)), 0f);
            int both = 0, same = 0;
            for (long d = 2; d < 22; d++)
            {
                if (!Spell(planner, e1, d, false, out float a, out _) || !Spell(planner, e2, d, false, out float b, out _)) continue;
                both++;
                if (System.Math.Abs(a - b) < 0.1f) same++;
            }
            TestContext.WriteLine($"both visited on {both} days, within 6 minutes of each other on {same}");
            Assert.Greater(both, 8);
            Assert.LessOrEqual(same, both / 4, "not at the same time on most days");
        }

        [Test]
        public void SeveralElderGlanceLinesAreNotAList()
        {
            var sim = Fresh();
            var alive = sim.State.notables.Where(n => n.alive).OrderBy(n => n.id).Take(2).ToList();
            foreach (var n in alive) { n.age = sim.Config.retireAge + 5f; n.job = JobType.Elder; n.lifespan = n.age + 0.8f; sim.DebugLastDays(n.id); n.lifespan = n.age + 0.8f; }
            var lines = sim.State.chronicle.Where(x => x.kind == SimCore.SlowingKind).ToList();
            Assert.AreEqual(1, lines.Count, "within a game year only the first gets the story line");
            Assert.AreEqual(alive[0].id, lines[0].notableId);
            foreach (var n in alive)
            {
                Assert.IsTrue(sim.IsFrail(n));
                StringAssert.StartsWith("Slowing down", sim.PersonCardOf(n.id).frailLine, "the card text stays");
                Assert.AreEqual("slowing", sim.FrailTagWord(n), "so does the pin sign");
            }
            sim.State.month += SimConfig.MonthsPerYear + 1;
            var late = sim.State.notables.First(n => n.alive && !alive.Contains(n));
            sim.DebugLastDays(late.id);
            Assert.AreEqual(2, sim.State.chronicle.Count(x => x.kind == SimCore.SlowingKind), "a year later the next one is said again");
        }

        [Test]
        public void ThePartnerAndTheChildrenAlsoComeNowAndThen()
        {
            var sim = Fresh();
            var (e, c) = Pair(sim, 0.4f);
            var third = sim.State.notables.First(n => n.alive && n.id != e.id && n.id != c.id);
            third.parentId = e.id; third.age = 12f; // a child of the elder
            var planner = LifePlanner.Wired(sim);
            int cCame = 0, tCame = 0, spells = 0;
            for (long d = 2; d < 30; d++)
            {
                if (!Spell(planner, e, d, false, out float s, out float en)) continue;
                spells++;
                float mid = (s + en) / 2f;
                var kid = At(Day(planner, third.id, d), mid); var adult = At(Day(planner, c.id, d), mid);
                if (kid.kind == ActivityKind.SitWithOld && kid.partner == PersonRef.Notable(e.id)) tCame++;
                if (adult.kind == ActivityKind.SitWithOld && adult.partner == PersonRef.Notable(e.id)) cCame++;
            }
            TestContext.WriteLine($"{spells} spells: the adult child came {cCame}, the young one {tCame}");
            Assert.Greater(cCame, 0); Assert.Greater(tCame, 0);
            Assert.Less(cCame, spells, "not all of them every time");
            Assert.Less(tCame, spells);
            Assert.LessOrEqual(cCame + tCame, spells * 2);
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
                    Assert.IsFalse(day.Any(a => a.kind == ActivityKind.SitWithOld && a.partner != PersonRef.None && a.place.kind == PlaceKind.Home),
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

        // ------------------------------------------------------------------ the visit waits for the seat (Editor bug, 2026-11-26)

        static SimCore Village(int seed)
        {
            var cfg = new SimConfig();
            var sim = new SimCore(cfg, SimCore.NewWorld(seed, cfg));
            for (int m = 0; m < 120 * 12 && sim.State.era < Era.Village; m++)
            {
                sim.Step();
                if (sim.State.eraReady) sim.AdvanceEra();
            }
            Assert.AreEqual(Era.Village, sim.State.era);
            for (int m = 0; m < 24; m++) sim.Step(); // past the era festival's year (every evening a festival)
            Assert.IsFalse(sim.FestivalOn);
            return sim;
        }

        /// <summary>The Editor showed family sitting at a frail elder's door 73 frames while the elder was off minding the babies or
        /// elsewhere. Every minute a visitor sits beside the elder, the elder is on their own stool at that door: the elder's other
        /// doings give way to the spell.</summary>
        [TestCase(1)]
        [TestCase(3)]
        public void EveryMinuteAVisitorSitsBesideTheElderTheElderIsOnTheirStool(int seed)
        {
            var sim = Village(seed);
            var alive = sim.State.notables.Where(n => n.alive).OrderBy(n => n.id).ToList();
            var frail = new List<Notable>(); // two elders who each have family besides the other (a frail elder is visited, they don't go visiting)
            foreach (var n in alive)
                if (frail.Count < 2 && sim.KinOf(n).Any(k => frail.All(f => f.id != k)) && frail.All(f => sim.KinOf(f).Any(k => k != n.id))) frail.Add(n);
            Assert.AreEqual(2, frail.Count, "two elders with family");
            foreach (var e in frail) { e.age = sim.Config.retireAge + 6f; e.lifespan = e.age + 0.5f; e.job = JobType.Elder; }
            var planner = LifePlanner.Wired(sim);
            foreach (var e in frail) Assert.Greater(planner.FrailtyOf(PersonRef.Notable(e.id)), 0f, e.name);
            int minutes = 0, wrong = 0, seated = 0;
            var bad = new List<string>();
            var hist = new Dictionary<string, int>();
            var firstVisit = new Dictionary<(int, long), float>(); // elder, day -> the first minute anyone sat beside them
            for (long d = 2; d < 32; d++)
            {
                for (int m = 0; m < 24 * 60; m++)
                {
                    var t = new ViewTime(d, m / 60f + 1f / 120f);
                    foreach (var e in frail)
                    {
                        var who = PersonRef.Notable(e.id);
                        var ae = planner.Plan(who, t);
                        bool sits = ae.kind == ActivityKind.SitOut && !ae.moving && ae.prop == Prop.Stool && ae.place.kind == PlaceKind.Home;
                        if (sits) seated++;
                        foreach (var v in alive)
                        {
                            if (v.id == e.id) continue;
                            var av = planner.Plan(PersonRef.Notable(v.id), t);
                            if (av.kind != ActivityKind.SitWithOld || av.partner != who || av.moving || av.place.kind != PlaceKind.Home) continue; // the family at the door (the priest's hour with an elder at the gather is another beat)
                            minutes++;
                            if (!firstVisit.ContainsKey((e.id, d))) firstVisit[(e.id, d)] = t.hour;
                            if (sits && ae.place == av.place) continue;
                            wrong++;
                            string key = $"{ae.kind}/{ae.source}/{(ae.place.kind == PlaceKind.Home && ae.place == av.place ? "home" : ae.place.kind.ToString())}";
                            hist[key] = hist.TryGetValue(key, out int c0) ? c0 + 1 : 1;
                            if (bad.Count < 8) bad.Add($"day {d} {t.hour:0.00} h: {v.name} beside {e.name}, who is {ae.kind} at {ae.place} (moving {ae.moving}, prop {ae.prop})");
                        }
                    }
                }
            }
            TestContext.WriteLine($"{minutes} visitor-minutes over 30 days; the elders sat {seated} minutes on their stools; wrong {wrong}");
            foreach (var l in bad) TestContext.WriteLine(l);
            foreach (var kv in hist.OrderByDescending(x => x.Value)) TestContext.WriteLine($"  {kv.Key}: {kv.Value}");
            Assert.Greater(minutes, 30 * 20, "the family comes on most days");
            Assert.AreEqual(0, wrong, "a visit only while the elder sits at their door: " + string.Join("; ", bad));
            // Still irregular: per elder the visits start at many different times, and the two families don't arrive together.
            foreach (var e in frail)
            {
                var starts = firstVisit.Where(kv => kv.Key.Item1 == e.id && kv.Value > 13f).Select(kv => kv.Value).ToList();
                int spells = 0, held = 0;
                for (long d = 2; d < 32; d++)
                {
                    if (!planner.VisitSpell(PersonRef.Notable(e.id), d, false, out float s0, out float e0)) continue;
                    spells++;
                    bool all = true;
                    for (float h = s0 + 0.01f; h < e0; h += 0.05f) { var a0 = planner.Plan(PersonRef.Notable(e.id), new ViewTime(d, h)); if (a0.kind != ActivityKind.SitOut || a0.moving) all = false; }
                    if (all) held++;
                }
                TestContext.WriteLine($"{e.name}: {spells} afternoon spells, seated through {held}; kin {string.Join(",", sim.KinOf(e))}");
                TestContext.WriteLine($"{e.name}: afternoon visits on {starts.Count} days, from {string.Join(" ", starts.Select(x => x.ToString("0.0")))}");
                Assert.GreaterOrEqual(starts.Count, 6, e.name + " is visited on many afternoons (fewer for an elder with a role beat in the evening)");
                Assert.Greater(starts.Max() - starts.Min(), 0.8f, e.name + ": the first sit beside them spreads over the afternoon, no timetable");
            }
            int together = 0, bothDays = 0;
            for (long d = 2; d < 32; d++)
                if (firstVisit.TryGetValue((frail[0].id, d), out float a) && firstVisit.TryGetValue((frail[1].id, d), out float b) && a > 13f && b > 13f)
                { bothDays++; if (System.Math.Abs(a - b) < 0.1f) together++; }
            Assert.LessOrEqual(together, bothDays / 4, "the two families don't gather on a schedule");
        }
    }
}
