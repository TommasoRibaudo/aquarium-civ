using System;
using System.Collections.Generic;
using System.Linq;
using AquariumCiv.Sim;
using AquariumCiv.Sim.Eco;
using AquariumCiv.Sim.Settle;
using NUnit.Framework;
using UnityEngine;
using Terrain = AquariumCiv.Sim.Settle.Terrain;

namespace AquariumCiv.Tests
{
    /// <summary>#437 the wolf pack (docs/design/placed-life.md §3, §4, §5.6; research 19): the jar's readiness and the den ground, placing
    /// the pair, the pack's 2-6 over 40 years, kills within every floor, the ravens' log, the deer's human shield, boldness, the meat
    /// thief, the choices (6.1, 6.3, 6.4) through the small-choice machinery and never resolved unseen, the white wolf moment, the hard
    /// rules (<see cref="WolfRules"/>), the save, the debug helpers.</summary>
    public class WolfPackTests
    {
        // ---------------------------------------------------------------- helpers
        static void Step(SimCore sim, int months) { for (int k = 0; k < months; k++) sim.Step(); }
        static float FromFire(SimCore sim, int c) => Terrain.Dist(SiteMap.CXc(c), SiteMap.CZc(c), sim.EcoGround.campX, sim.EcoGround.campZ);

        /// <summary>The camp's rungs, a wood of <paramref name="trees"/> far from the fire, a pond, the deer placed and at their cap.</summary>
        internal static SimCore Wooded(int seed = 7, int trees = 30, SimConfig cfg = null)
        {
            SimCore sim;
            if (cfg == null) sim = PlacedLifeTests.Camp(seed, trees);
            else
            {
                cfg.ecology = true;
                sim = new SimCore(cfg, SimCore.NewWorld(seed, cfg));
                LifeJarTests.Meadow(sim, 0.3f);
                sim.DebugPlaceNearCamp(LifeSpecies.Thicket);
                sim.DebugPlaceNearCamp(LifeSpecies.Rabbits);
                PlacedLifeTests.Wood(sim, trees);
                PlacedLifeTests.Pond(sim);
            }
            Assert.GreaterOrEqual(sim.DebugPlaceNearCamp(LifeSpecies.Deer), 0, "the deer");
            sim.DebugDeerToCap();
            return sim;
        }

        /// <summary>A wooded camp with the wolves' pair placed at the den nearest the fire.</summary>
        internal static SimCore Pack(int seed = 7, SimConfig cfg = null)
        {
            var sim = Wooded(seed, 30, cfg);
            Assert.GreaterOrEqual(sim.DebugPlaceWolves(), 0, "a den spot in the far wood");
            Assert.IsTrue(sim.WolvesPlaced);
            return sim;
        }

        static SimConfig Choices() => new SimConfig { ecology = true, dilemmas = true };

        /// <summary>No wish holds the shared slot (as ChoiceFixTests do), and no big or everyday small choice competes: only the life choices.</summary>
        internal static void OnlyLifeChoices(SimCore sim)
        {
            sim.DilemmaCatalog = new DilemmaDef[0]; sim.SmallCatalog = new DilemmaDef[0];
            foreach (var w in sim.State.wishes) { if (w.state == WishState.Open) w.state = WishState.Lapsed; w.endMonth = sim.State.month - 100; }
        }

        // ================================================================ the jar (spec §3)
        [Test]
        public void Jar_OpensAfterAFullYearOfFiveDeer_WithAWoodOfTwenty_AndADen()
        {
            var sim = Wooded();
            Assert.AreEqual(JarState.Locked, sim.JarStateOf(LifeSpecies.Wolves), "not before the herd has held a year");
            var opened = new List<LifeSpecies>(); sim.LifeJarOpened += opened.Add;
            long first = -1;
            for (int k = 0; k < 24 && sim.JarStateOf(LifeSpecies.Wolves) == JarState.Locked; k++)
            {
                sim.Step();
                if (first < 0 && sim.State.eco.deerFiveSince >= 0) first = sim.State.eco.deerFiveSince;
                Assert.GreaterOrEqual(sim.EcoDeerCount, 5);
            }
            Assert.AreEqual(JarState.Ready, sim.JarStateOf(LifeSpecies.Wolves));
            Assert.GreaterOrEqual(sim.JarReadyMonth(LifeSpecies.Wolves) - first, 12, "a full game year at five or more");
            CollectionAssert.Contains(opened, LifeSpecies.Wolves);
            Assert.GreaterOrEqual(sim.EcoGrownTrees, 20);
            Assert.GreaterOrEqual(sim.LifePlaceCellNearCamp(LifeSpecies.Wolves), 0, "a den spot");
        }

        [Test]
        public void Jar_StaysShut_WithoutTheWood_TheHerdsYear_OrTheDeer()
        {
            var small = Wooded(7, 18); // a wood of 18: the deer are there, the wood isn't big enough
            Step(small, 36);
            Assert.AreEqual(JarState.Locked, small.JarStateOf(LifeSpecies.Wolves), "a wood of 20 grown trees");

            var lean = Wooded(); // the herd taken down every season: never a year at five
            for (int k = 0; k < 12; k++) { Step(lean, 3); lean.DebugToFloor(LifeSpecies.Deer); }
            Assert.AreEqual(JarState.Locked, lean.JarStateOf(LifeSpecies.Wolves), "the deer at five for a full year");

            var none = PlacedLifeTests.Camp(7, 30); // no deer placed
            Step(none, 48);
            Assert.AreEqual(0, none.EcoDeerCount);
            Assert.AreEqual(JarState.Locked, none.JarStateOf(LifeSpecies.Wolves), "the rung below: the deer");
        }

        [Test]
        public void Ground_DenSpotsOnly_GrownTreesInTheFarWood()
        {
            var sim = Wooded();
            var dens = new List<int>(); sim.LifeGround(LifeSpecies.Wolves, dens);
            Assert.Greater(dens.Count, 0);
            var e = sim.State.eco; var g = sim.EcoGround;
            foreach (int c in dens)
            {
                Assert.AreEqual((byte)EcoCover.Tree, e.cover[c], "under a tree's roots");
                Assert.GreaterOrEqual(FromFire(sim, c), sim.Config.eco.denFromFire - 1e-4f, "at least 1.5 u from the fire");
                Assert.IsFalse(g.InBasin(c));
            }
            // the wood's near edge, the meadow, the water: never
            int nearEdge = Enumerable.Range(0, EcoLand.N).Where(c => e.cover[c] == (byte)EcoCover.Tree).OrderBy(c => FromFire(sim, c)).First();
            Assert.IsFalse(sim.LifeGroundOk(LifeSpecies.Wolves, nearEdge), "the wood's near edge (no wood between it and the fire)");
            int meadow = Enumerable.Range(0, EcoLand.N).First(c => e.cover[c] == (byte)EcoCover.Grass && FromFire(sim, c) > 1.6f);
            Assert.IsFalse(sim.LifeGroundOk(LifeSpecies.Wolves, meadow));
            // a lone tree far out is no wood
            int lone = Enumerable.Range(0, EcoLand.N).First(c => e.cover[c] == (byte)EcoCover.Grass && FromFire(sim, c) > 1.8f
                && Enumerable.Range(0, EcoLand.N).Where(o => e.cover[o] == (byte)EcoCover.Tree).All(o => Terrain.Dist(SiteMap.CXc(o), SiteMap.CZc(o), SiteMap.CXc(c), SiteMap.CZc(c)) > 0.5f));
            e.cover[lone] = (byte)EcoCover.Tree;
            Assert.IsFalse(sim.LifeGroundOk(LifeSpecies.Wolves, lone), "a lone tree");
            Assert.IsFalse(sim.CanPlaceLife(LifeSpecies.Wolves, dens[0]), "the jar is shut");
        }

        [Test]
        public void Placing_APairAtTheDen_Boldness02_NeverARestock()
        {
            var sim = Wooded();
            while (sim.JarStateOf(LifeSpecies.Wolves) != JarState.Ready) sim.Step();
            var placed = new List<LifePlacedInfo>(); sim.LifePlaced += placed.Add;
            int den = sim.LifePlaceCellNearCamp(LifeSpecies.Wolves);
            Assert.IsTrue(sim.PlaceLife(LifeSpecies.Wolves, den));
            Assert.AreEqual(2, sim.EcoWolfCount, "a pair");
            Assert.AreEqual(den, sim.WolfDen);
            Assert.AreEqual(den, sim.LifeHomeCell(LifeSpecies.Wolves));
            Assert.AreEqual(0.2f, sim.WolfBoldness, 1e-6f);
            Assert.AreEqual(2f, sim.LifePopulation(LifeSpecies.Wolves));
            Assert.AreEqual(1, placed.Count);
            Assert.IsTrue(sim.LifeArrivalPending(LifeSpecies.Wolves));
            Assert.IsTrue(sim.State.chronicle.Any(x => x.kind == "life_wolves"));
            Assert.IsFalse(sim.PlaceLife(LifeSpecies.Wolves, den), "placed once");
            Assert.IsFalse(sim.DebugJarAgain(LifeSpecies.Wolves), "the wolves never restock");
            Assert.IsFalse(sim.DebugToFloor(LifeSpecies.Wolves));
            Step(sim, 12 * 12);
            Assert.AreEqual(JarState.Placed, sim.JarStateOf(LifeSpecies.Wolves));
            Assert.AreEqual(0, sim.JarRestocks(LifeSpecies.Wolves));
        }

        // ================================================================ the pack (spec §4.1)
        [Test]
        public void Pack_FortyYears_TwoToSix_PupsInSpring_YoungLeaveInAutumn()
        {
            var sim = Pack();
            int pupSprings = 0; bool leftOutsideAutumn = false; long lastLeft = -1;
            for (int m = 0; m < 40 * 12; m++)
            {
                int pupsBefore = sim.EcoWolfPups;
                sim.Step();
                Assert.GreaterOrEqual(sim.EcoWolfCount, 2, "the pair never leaves");
                Assert.LessOrEqual(sim.EcoWolfCount, 6, "one pack, at most six");
                Assert.AreEqual(2 + sim.EcoWolfPups + sim.EcoWolfYearlings, sim.EcoWolfCount);
                if (sim.State.month % 12 == 3)
                {
                    if (sim.EcoWolfPups > 0) pupSprings++;
                    Assert.LessOrEqual(sim.EcoWolfPups, 4);
                }
                else Assert.AreEqual(pupsBefore, sim.EcoWolfPups, "pups come only in spring");
                if (sim.WolvesLeftMonth != lastLeft)
                {
                    lastLeft = sim.WolvesLeftMonth;
                    if (EcoModel.SeasonOf(lastLeft) != 2) leftOutsideAutumn = true;
                }
            }
            Assert.Greater(pupSprings, 30, "pups most springs");
            Assert.Greater(sim.WolvesLeftTotal, 20, "the young move on");
            Assert.IsFalse(leftOutsideAutumn, "in autumn");
            Assert.AreEqual(WolfPack.GreyWolf, 0);
        }

        // ================================================================ kills (spec §4.2, §5.6, B20)
        [Test]
        public void Kills_FortyYears_WolvesAndHunters_EveryFloorHolds_NoSpeciesDiesOut()
        {
            var sim = Pack();
            sim.DebugPlaceNearCamp(LifeSpecies.Ducks);
            var prey = new HashSet<LifeSpecies>(); sim.WolfKilled += k => prey.Add(k.Prey);
            for (int m = 0; m < 40 * 12; m++)
            {
                for (int k = 0; k < 3; k++) { sim.HuntCatch(); sim.DeerHuntCatch(out _); sim.TakeEgg(); sim.DebugWolfKillNow(); }
                sim.Step();
                Assert.GreaterOrEqual(sim.EcoRabbitCount, SimCore.LifeFloor(LifeSpecies.Rabbits), $"rabbits, month {sim.State.month}");
                Assert.GreaterOrEqual(sim.EcoDeerCount, 2, $"deer, month {sim.State.month}");
                Assert.GreaterOrEqual(sim.EcoDeerStags, 1); Assert.GreaterOrEqual(sim.EcoDeerDoes, 1);
                Assert.GreaterOrEqual(sim.EcoDucks, 2, $"ducks, month {sim.State.month}");
                Assert.GreaterOrEqual(sim.EcoWolfCount, 2);
            }
            Assert.Greater(sim.WolfKillCount, 20);
            CollectionAssert.IsSupersetOf(prey, new[] { LifeSpecies.Deer, LifeSpecies.Rabbits }, "deer and rabbits");
            Assert.IsFalse(prey.Contains(LifeSpecies.Fish), "the wolves don't fish");
        }

        [Test]
        public void Kills_AFewAYear_MoreChasesInWinter_MostFail()
        {
            int[] chasesBySeason = new int[4]; int chases = 0, kills = 0;
            foreach (int seed in new[] { 3, 7, 11 })
            {
                var sim = Pack(seed);
                for (int m = 0; m < 40 * 12; m++)
                {
                    int c0 = sim.WolfChases;
                    sim.Step();
                    if (sim.WolfChases > c0) chasesBySeason[EcoModel.SeasonOf(sim.State.month)]++;
                }
                chases += sim.WolfChases; kills += sim.WolfKillCount;
            }
            TestContext.Out.WriteLine($"chases {chases} kills {kills} by season {string.Join(",", chasesBySeason)}");
            float killsAYear = kills / 120f;
            Assert.That(killsAYear, Is.InRange(0.5f, 3f), "a few a year");
            Assert.Less(kills, chases / 2, "most chases fail");
            Assert.Greater(chasesBySeason[3], chasesBySeason[1], "more in winter");
        }

        [Test]
        public void Kills_TheRavensLog_WhereWhenWhat_OpenThenEaten()
        {
            var sim = Pack();
            var k = sim.DebugWolfKillNow();
            Assert.IsNotNull(k);
            Assert.AreSame(k, sim.LastWolfKill);
            Assert.AreSame(k, sim.RavensKill, "the ravens circle it");
            Assert.AreEqual(KillEaten.Open, k.EatenBy);
            Assert.AreEqual(sim.State.month, k.month);
            Assert.GreaterOrEqual(FromFire(sim, k.cell), sim.Config.eco.wolfKillFromFire - 1e-4f, "out past the firelight");
            Assert.AreNotEqual((byte)EcoCover.Water, sim.State.eco.cover[k.cell]);
            Step(sim, SimCore.KillOpenMonths + 1);
            Assert.AreEqual(KillEaten.Wolves, k.EatenBy, "nobody asked: the wolves ate it");
            for (int i = 0; i < 12; i++) sim.DebugWolfKillNow();
            Assert.LessOrEqual(sim.WolfKills.Count, EcoModel.WolfKillsKept);
        }

        [Test]
        public void Kills_NoChaseWhenEveryPreyIsAtItsFloor()
        {
            var sim = Pack();
            sim.DebugToFloor(LifeSpecies.Deer); sim.DebugToFloor(LifeSpecies.Rabbits);
            int chases = sim.WolfChases;
            Assert.IsNull(sim.DebugWolfKillNow());
            Assert.AreEqual(chases, sim.WolfChases, "no chase is planned at the floor (B20)");
        }

        // ================================================================ the human shield (research 19 §1)
        [Test]
        public void Shield_WithThePackAbout_TheDeerGrazeNearerTheCamp()
        {
            var sim = PlacedLifeTests.Camp(7, 30);
            var ground = new List<int>(); sim.LifeGround(LifeSpecies.Deer, ground);
            int far = ground.OrderByDescending(c => FromFire(sim, c)).ThenBy(c => c).First();
            sim.DebugJarsReady();
            Assert.IsTrue(sim.PlaceLife(LifeSpecies.Deer, far));
            sim.DebugDeerToCap();
            Assert.AreEqual(far, sim.EcoDeerHome);
            Assert.AreEqual(0f, sim.EcoDeerShield);
            Assert.GreaterOrEqual(sim.DebugPlaceWolves(), 0);
            Assert.AreEqual(1f, sim.EcoDeerShield);
            int shielded = sim.EcoDeerHome;
            Assert.Less(FromFire(sim, shielded), FromFire(sim, far) - 0.2f, "the herd's home moved toward the fire");
            Assert.IsTrue(sim.LifeGroundOk(LifeSpecies.Deer, shielded), "still on the deer's ground (outside the camp margin)");
            sim.DebugToFloor(LifeSpecies.Deer);
            Assert.IsTrue(sim.LifeShy(LifeSpecies.Deer));
            Assert.Greater(FromFire(sim, sim.EcoDeerHome), FromFire(sim, shielded), "shy wins: the far side");
        }

        // ================================================================ boldness (spec §5.6)
        [Test]
        public void Boldness_RisesWithScrapsAndLeftKills_FallsWithChasesTakenKillsAndDogs_StaysInItsBounds()
        {
            var up = Pack(7, Choices());
            up.DebugMeatThiefNow();
            var d = up.DebugRaiseLifeChoice(LifeChoices.Thief);
            Assert.IsNotNull(d);
            float b0 = up.WolfBoldness;
            Assert.IsTrue(up.AnswerDilemma(d.id, 1, DilemmaPath.Card), "leave them the scraps");
            Assert.Greater(up.WolfBoldness, b0);
            Assert.AreEqual(WolfWay.Scraps, up.WolfWayTaken);
            float b1 = up.WolfBoldness;
            Step(up, 12);
            Assert.Greater(up.WolfBoldness, b1, "the scraps wear it up");
            Assert.IsNotNull(up.DebugWolfKillNow());
            var kd = up.DebugRaiseLifeChoice(LifeChoices.Kill);
            float b2 = up.WolfBoldness;
            up.AnswerDilemma(kd.id, 1, DilemmaPath.Card); // leave it to them
            Assert.Greater(up.WolfBoldness, b2);
            Step(up, 12 * 40);
            Assert.AreEqual(1f, up.WolfBoldness, 1e-6f, "capped at 1");

            var down = Pack(7, Choices());
            down.DebugMeatThiefNow();
            var c = down.DebugRaiseLifeChoice(LifeChoices.Thief);
            Assert.IsTrue(down.AnswerDilemma(c.id, 0, DilemmaPath.Card), "chase them off");
            Assert.Less(down.WolfBoldness, 0.2f);
            Assert.AreEqual(0f, down.WolfEyesChance, "no eyes for a season");
            Assert.IsNotNull(down.DebugWolfKillNow());
            var k2 = down.DebugRaiseLifeChoice(LifeChoices.Kill);
            float b3 = down.WolfBoldness; float food = down.State.food;
            down.AnswerDilemma(k2.id, 0, DilemmaPath.Card); // take their meat
            Assert.Less(down.WolfBoldness, b3 + 1e-6f);
            Assert.AreEqual(KillEaten.People, down.WolfKills.Last(x => x.asked).EatenBy);
            Assert.Greater(down.State.food, food - 1e-3f, "meat for the camp");
            Assert.GreaterOrEqual(down.WolfBoldness, 0f);

            var dogs = Pack();
            dogs.DebugWolvesBold();
            Assert.IsNotNull(dogs.DebugAdoptPup());
            Step(dogs, SimCore.DogGrownMonths + 1);
            Assert.GreaterOrEqual(dogs.State.eco.dogGuardMonth, 0, "the first guard bark");
            Assert.Less(dogs.WolfBoldness, 1f, "the dogs on guard");
            Step(dogs, 12 * 40);
            Assert.GreaterOrEqual(dogs.WolfBoldness, 0f);
        }

        [Test]
        public void Boldness_SetsTheEyes_TheTrailingWolf_AndTheGreyWolfsApproach()
        {
            var sim = Pack();
            float shyEyes = sim.WolfEyesChance, shyFire = sim.WolfMinFromFire;
            Assert.IsFalse(sim.WolfTrailsHunters, "a shy pack keeps off the hunters");
            sim.DebugWolvesBold();
            Assert.Greater(sim.WolfEyesChance, shyEyes);
            Assert.Less(sim.WolfMinFromFire, shyFire);
            Assert.AreEqual(WolfRules.FirelightEdge, sim.WolfMinFromFire, 1e-6f, "the edge of the firelight, never nearer");
            Assert.IsTrue(sim.WolfTrailsHunters);
            Assert.AreEqual(1.0f, sim.WolfTrailDistance, 1e-6f, "far behind until the scraps");
            Assert.IsFalse(sim.GreyWolfAtBoneSpot);
            sim.State.eco.wolfWay = (int)WolfWay.Scraps; sim.State.eco.wolfWayMonth = sim.State.month;
            Assert.IsTrue(sim.GreyWolfAtBoneSpot);
            Assert.AreEqual(1.3f, sim.GreyWolfFromFire, 1e-4f, "at first 1.3 u from the fire");
            Assert.AreEqual(0.6f, sim.WolfTrailDistance, 1e-4f, "the trailing wolf comes to 0.6 u");
            Step(sim, 24);
            sim.DebugWolvesBold();
            Assert.AreEqual(WolfRules.FirelightEdge, sim.GreyWolfFromFire, 1e-4f, "nearer over a year, never past the firelight's edge");
            int nights = 0;
            for (long day = 0; day < 200; day++) if (sim.WolfEyesTonight(day)) nights++;
            Assert.That(nights, Is.InRange(100, 190), "most nights at full boldness");
        }

        // ================================================================ the meat thief (B22.a, choice 6.1)
        [Test]
        public void Thief_AtMostOnceASeason_OnlyWhileWatched_NeverWhenFoodIsShort_NeverOnceChased()
        {
            var sim = Pack();
            sim.DebugWolvesBold();
            var seasons = new HashSet<long>(); int thefts = 0;
            sim.WolfTheft += _ => thefts++;
            for (int m = 0; m < 20 * 12; m++)
            {
                sim.State.food = sim.FoodCap;
                int before = sim.WolfThefts;
                sim.Step();
                if (sim.WolfThefts > before) Assert.IsTrue(seasons.Add((sim.State.month + 1) / 3), "once a season at most");
            }
            Assert.Greater(sim.WolfThefts, 10, "a bold pack steals now and then");
            Assert.AreEqual(sim.WolfThefts, thefts);
            Assert.IsTrue(sim.WolfTheftScenePending, "the view plays the last one at dusk");
            Assert.IsTrue(sim.AckWolfTheft()); Assert.IsFalse(sim.WolfTheftScenePending);
            Assert.IsFalse(sim.State.chronicle.Where(x => x.kind == "wolf_thief").Any(x => x.text.Contains("lost")), "no 'you lost'");

            // while away: none (and so no card)
            int t0 = sim.WolfThefts;
            sim.State.food = sim.FoodCap;
            sim.CatchUp(6);
            Assert.AreEqual(t0, sim.WolfThefts, "never while the Keeper is away");

            // short of food: none
            var lean = Pack();
            lean.DebugWolvesBold();
            for (int m = 0; m < 10 * 12; m++) { lean.State.food = lean.State.population * 1.5f; lean.Step(); }
            Assert.AreEqual(0, lean.WolfThefts, "never when the people's food would go short");

            // chased off: the thorns round the rack
            sim.State.eco.wolfWay = (int)WolfWay.Chase;
            Assert.IsTrue(sim.MeatThiefStopped);
            int t1 = sim.WolfThefts;
            for (int m = 0; m < 10 * 12; m++) { sim.State.food = sim.FoodCap; sim.Step(); }
            Assert.AreEqual(t1, sim.WolfThefts);
            Assert.IsFalse(sim.DebugMeatThiefNow());
        }

        // ================================================================ the choices (6.1, 6.3, 6.4) and the visit loop
        [Test]
        public void Choices_TheThief_RaisedOnlyWhileWatched_HeldThroughAnAbsence_NeverResolvedUnseen()
        {
            var sim = Pack(7, Choices());
            OnlyLifeChoices(sim);
            sim.DebugMeatThiefNow();
            Assert.IsTrue(sim.LifeChoiceWaiting);
            // away for a long time: nothing is raised, nothing is decided
            sim.CatchUp(30);
            Assert.IsNull(sim.OpenDilemma, "never raised while away");
            Assert.AreEqual(WolfWay.None, sim.WolfWayTaken, "never decided unseen");
            Assert.IsTrue(sim.BeginVisit(), "raised on the next open");
            var d = sim.OpenDilemma;
            Assert.AreEqual(LifeChoices.ThiefDef.id, d.defId);
            Assert.IsTrue(sim.ChoiceStageDue, "staged in the box first");
            StringAssert.Contains("Chase them off, or leave them the scraps?", sim.QuestionOf(d));
            // three visits without an answer: the people decide (shown three times, never unseen)
            for (int v = 0; v < 3 && d.state == DilemmaState.Open; v++) { sim.CatchUp(1); sim.BeginVisit(); }
            Assert.AreEqual(DilemmaState.Lapsed, d.state);
            Assert.AreEqual(DilemmaEnd.Visits, d.end);
            Assert.AreEqual(3, d.visits);
            Assert.AreNotEqual(WolfWay.None, sim.WolfWayTaken, "the people's own pick");
            Assert.IsFalse(sim.LifeChoiceWaiting, "asked once");
        }

        [Test]
        public void Choices_LetThemDecide_TheChiefsTrait()
        {
            foreach (var (trait, way) in new[] { ("the brave", WolfWay.Scraps), ("the kind", WolfWay.Scraps), ("the worrier", WolfWay.Chase), ("the grump", WolfWay.Chase) })
            {
                var sim = Pack(7, Choices());
                foreach (var n in sim.State.notables) n.trait = trait;
                sim.DebugMeatThiefNow();
                var d = sim.DebugRaiseLifeChoice(LifeChoices.Thief);
                Assert.IsTrue(sim.LetThemDecide(d.id));
                Assert.AreEqual(way, sim.WolfWayTaken, trait);
                Assert.AreEqual(DilemmaEnd.LetThemDecide, d.end);
            }
        }

        [Test]
        public void Choices_TheKill_TakeOrLeave_OnTheNewestKill()
        {
            var sim = Pack(7, Choices());
            Assert.IsNull(sim.DebugRaiseLifeChoice(LifeChoices.Kill), "no kill, no question");
            var k = sim.DebugWolfKillNow();
            var d = sim.DebugRaiseLifeChoice(LifeChoices.Kill);
            Assert.IsNotNull(d);
            Assert.IsTrue(k.asked);
            StringAssert.Contains("ravens are circling", sim.QuestionOf(d));
            Step(sim, 6);
            Assert.AreEqual(KillEaten.Open, k.EatenBy, "held open while the Keeper is asked");
            Assert.IsTrue(sim.AnswerDilemma(d.id, 1, DilemmaPath.Card));
            Assert.AreEqual(KillEaten.Wolves, k.EatenBy);
            Assert.IsNull(sim.DebugRaiseLifeChoice(LifeChoices.Kill), "asked about once");
        }

        [Test]
        public void Choices_ThePen_OnceAfterItStands_FenceWithoutADog_TheChoiceWithOne_SafeAfter()
        {
            var nodog = Pack(7, Choices());
            Step(nodog, 24);
            Assert.AreEqual(-1, nodog.PenCircledMonth, "no pen, no circling (dormant until #431)");
            nodog.DuckPenBuilt = true; nodog.PenDucks = 6;
            Assert.IsTrue(nodog.PenOpenToWolves);
            nodog.Step();
            Assert.GreaterOrEqual(nodog.PenCircledMonth, 0, "the first night after it stands");
            Assert.AreEqual(PenGuard.Fence, nodog.DuckPenGuard, "no dog: nothing to choose, a thorn fence");
            Assert.IsFalse(nodog.PenOpenToWolves);
            Assert.IsNull(nodog.DebugRaiseLifeChoice(LifeChoices.Pen));

            var dog = Pack(7, Choices());
            dog.DebugAdoptPup(); Step(dog, SimCore.DogGrownMonths + 1);
            Assert.Greater(dog.GrownDogs, 0);
            dog.PenDucks = 6;
            Assert.IsTrue(dog.DebugWolvesAtPenNow());
            Assert.IsFalse(dog.DebugWolvesAtPenNow(), "once");
            Assert.AreEqual(PenGuard.None, dog.DuckPenGuard, "the choice waits");
            Assert.AreEqual(6, dog.PenDucks, "nothing taken at the circling");
            var d = dog.DebugRaiseLifeChoice(LifeChoices.Pen);
            Assert.IsNotNull(d);
            Assert.IsTrue(dog.AnswerDilemma(d.id, 1, DilemmaPath.Card));
            Assert.AreEqual(PenGuard.Dogs, dog.DuckPenGuard);
            Assert.IsFalse(dog.PenOpenToWolves, "safe from then on");
            for (int i = 0; i < 40; i++) dog.DebugWolfKillNow();
            Assert.AreEqual(6, dog.PenDucks, "wolves never take from a guarded pen");
            Assert.IsFalse(dog.WolfKills.Any(x => x.pen));
        }

        [Test]
        public void Choices_AWaitingLifeChoice_TakesTheNextSmallSlot()
        {
            var sim = Pack(7, Choices());
            sim.SmallCatalog = new DilemmaDef[0];
            sim.State.dilemmaNextHours = sim.State.clockHours + 100; // the big one is far off
            sim.DebugMeatThiefNow();
            for (int m = 0; m < 24 && sim.OpenDilemma == null; m++)
            {
                foreach (var w in sim.State.wishes) { if (w.state == WishState.Open) w.state = WishState.Lapsed; w.endMonth = sim.State.month - 100; }
                sim.Advance(1.0 / 12);
            }
            Assert.IsNotNull(sim.OpenDilemma, "raised while watched, without waiting for the big one");
            Assert.AreEqual(LifeChoices.ThiefDef.id, sim.OpenDilemma.defId);
        }

        // ================================================================ the white wolf (6.5)
        [Test]
        public void WhiteWolf_OnceInAWinterAfterTheFirstPups_APureMoment()
        {
            var cfg = new SimConfig { ecology = true, moments = true };
            var sim = Pack(7, cfg);
            int howls = 0; sim.WhiteWolfHowled += () => howls++;
            for (int m = 0; m < 40 * 12; m++) sim.Step();
            Assert.AreEqual(1, howls, "once per box");
            Assert.AreEqual(3, EcoModel.SeasonOf(sim.WhiteWolfMonth), "in a winter");
            Assert.Greater(sim.WhiteWolfMonth, sim.State.eco.wolfFirstPupsMonth, "after the first pups");
            var e = sim.State.chronicle.Single(x => x.kind == SimCore.WhiteWolfKind);
            var mo = sim.MomentOfEntry(e.id);
            Assert.IsNotNull(mo, "held for the Keeper as a moment");
            Assert.AreEqual(MomentKind.WhiteWolf, mo.kind);
            Assert.IsFalse(sim.DebugWhiteWolfNow(), "once");
            Assert.IsFalse(LifeChoices.All.Any(x => x.question.Contains("white")), "no pelt, no choice");
        }

        // ================================================================ the hard rules (WolfRules)
        [Test]
        public void Rules_TheTentRing_ThePersonGap_TheFirelight_Keep()
        {
            var sim = Pack();
            sim.DebugOpenCampGate(); sim.DebugAddHides(10);
            Step(sim, 24);
            var g = sim.WolfGroundNow();
            Assert.Greater(g.ring.Count, 0, "the tents");
            foreach (var b in sim.State.buildings.Where(b => b.lotId != 0 && b.type != BuildingType.Camp))
            {
                Assert.IsTrue(WolfRules.InTentRing(b.x, b.z, g.ring));
                Assert.IsTrue(WolfRules.InTentRing(b.x + 0.5f * b.w + 0.2f, b.z, g.ring), "the ring reaches 0.25 u past the tent");
                Assert.IsFalse(sim.WolfPointOk(b.x, b.z, null));
            }
            Assert.IsFalse(sim.WolfPointOk(g.fireX + 0.9f, g.fireZ, null), "inside the firelight");
            float px = SiteMap.CXc(sim.WolfDen), pz = SiteMap.CZc(sim.WolfDen);
            var person = new List<float> { px + 0.2f, pz };
            Assert.IsFalse(WolfRules.Allowed(px, pz, g, person, 1f), "0.2 u from a person");
            float x = px, z = pz;
            Assert.IsTrue(WolfRules.Keep(ref x, ref z, px, pz, g, person, 1f));
            Assert.GreaterOrEqual(Terrain.Dist(x, z, person[0], person[1]), WolfRules.PersonGap);
            // a crowd round the den: no safe spot near it, the wolf stays in the trees
            var crowd = new List<float>();
            for (float dx = -2f; dx <= 2f; dx += 0.2f) for (float dz = -2f; dz <= 2f; dz += 0.2f) { crowd.Add(px + dx); crowd.Add(pz + dz); }
            x = px; z = pz;
            if (WolfRules.Keep(ref x, ref z, px, pz, g, crowd, 1f)) Assert.IsTrue(WolfRules.Allowed(x, z, g, crowd, 1f));
            Assert.AreEqual(WolfRules.ShyApproach, WolfRules.MinFromFire(0f), 1e-6f);
            Assert.AreEqual(WolfRules.FirelightEdge, WolfRules.MinFromFire(5f), 1e-6f, "capped: never nearer than the firelight's edge");
            // the bone spot and the rack: at the camp's edge, outside the ring
            sim.State.eco.wolfWay = (int)WolfWay.Scraps;
            Assert.IsTrue(sim.WolfBoneSpot(out float bx, out float bz));
            Assert.IsFalse(WolfRules.InTentRing(bx, bz, g.ring)); Assert.GreaterOrEqual(Terrain.Dist(bx, bz, g.fireX, g.fireZ), 1.0f);
            Assert.IsTrue(sim.WolfRackSpot(out float rx, out float rz));
            Assert.IsFalse(WolfRules.InTentRing(rx, rz, g.ring), "the rack stands outside the ring");
        }

        // ================================================================ the save
        [Test]
        public void SaveRoundTrip_KeepsThePack_AndAnOldSaveLoadsWithNoWolves()
        {
            var sim = Pack(7, Choices());
            Step(sim, 15);
            sim.DebugWolfKillNow(); sim.DebugMeatThiefNow(); sim.DebugWhiteWolfNow(); sim.DebugAdoptPup();
            sim.DuckPenBuilt = true; sim.PenDucks = 4;
            var d = sim.DebugRaiseLifeChoice(LifeChoices.Thief);
            sim.AnswerDilemma(d.id, 1, DilemmaPath.Card);
            string json = JsonUtility.ToJson(sim.State);
            var loaded = new SimCore(sim.Config, JsonUtility.FromJson<SimState>(json));
            Assert.AreEqual(json, JsonUtility.ToJson(loaded.State), "loading changes nothing");
            Assert.AreEqual(sim.EcoWolfCount, loaded.EcoWolfCount); Assert.AreEqual(sim.WolfDen, loaded.WolfDen);
            Assert.AreEqual(sim.WolfBoldness, loaded.WolfBoldness); Assert.AreEqual(WolfWay.Scraps, loaded.WolfWayTaken);
            Assert.AreEqual(sim.WolfKills.Count, loaded.WolfKills.Count); Assert.AreEqual(sim.WhiteWolfMonth, loaded.WhiteWolfMonth);
            Assert.AreEqual(sim.DogCount, loaded.DogCount); Assert.AreEqual(sim.FirstDogKeeperName, loaded.FirstDogKeeperName);
            Assert.IsTrue(loaded.DuckPenBuilt); Assert.AreEqual(4, loaded.PenDucks);
            for (int k = 0; k < 24; k++) { sim.Step(); loaded.Step(); }
            Assert.AreEqual(JsonUtility.ToJson(sim.State.eco), JsonUtility.ToJson(loaded.State.eco), "and lives on the same");

            // a save from before #437/#438: none of the new fields at all
            var old = Wooded();
            string oldJson = JsonUtility.ToJson(old.State);
            foreach (var f in new[] { "deerFiveSince", "wolves", "wolfPups", "wolfYearlings", "wolfDen", "wolvesLeft", "wolvesLeftLast", "wolfFirstPupsMonth",
                         "wolvesLeftMonth", "wolfChases", "wolfKills", "wolfChaseMonth", "thefts", "wolfWay", "theftMonth", "wolfWayMonth", "wolfQuietUntil",
                         "whiteWolfMonth", "penDucks", "penGuard", "penCircledMonth", "pupFoundMonth", "pupNotBefore", "nextLitterMonth", "dogGuardMonth",
                         "pupFinder", "pupAsked", "dogNextId", "firstDogId", "firstDogKeeperId", "firstDogMonth" })
                oldJson = System.Text.RegularExpressions.Regex.Replace(oldJson, "\"" + f + "\":-?\\d+,?", "");
            foreach (var f in new[] { "wolfBold" }) oldJson = System.Text.RegularExpressions.Regex.Replace(oldJson, "\"" + f + "\":-?[\\d.eE+-]+,?", "");
            foreach (var f in new[] { "wolfChaseCaught", "theftSeen", "theftHide", "duckPen" }) oldJson = System.Text.RegularExpressions.Regex.Replace(oldJson, "\"" + f + "\":(true|false),?", "");
            foreach (var f in new[] { "wolfKillLog", "dogs" }) oldJson = System.Text.RegularExpressions.Regex.Replace(oldJson, "\"" + f + "\":\\[\\],?", "");
            foreach (var f in new[] { "firstDogKeeperName", "firstDogName" }) oldJson = System.Text.RegularExpressions.Regex.Replace(oldJson, "\"" + f + "\":\"\",?", "");
            StringAssert.DoesNotContain("\"wolfDen\"", oldJson);
            var back = new SimCore(old.Config, JsonUtility.FromJson<SimState>(oldJson));
            Assert.AreEqual(0, back.EcoWolfCount); Assert.IsFalse(back.WolvesPlaced); Assert.AreEqual(-1, back.WolfDen);
            Assert.AreEqual(0, back.DogCount); Assert.AreEqual(0, back.WolfKills.Count);
            Step(back, 12 * 4);
            Assert.AreEqual(0, back.EcoWolfCount, "nothing arrives by itself");
        }

        // ================================================================ debug
        [Test]
        public void TheDebugHelpers_ReachEveryWolfState()
        {
            var sim = new SimCore(Choices(), SimCore.NewWorld(7, Choices()));
            LifeJarTests.Meadow(sim, 0.3f);
            sim.DebugPlaceNearCamp(LifeSpecies.Thicket); sim.DebugPlaceNearCamp(LifeSpecies.Rabbits);
            Assert.IsTrue(sim.DebugReadyWolves(), "grows a far wood and places the deer if needed");
            Assert.AreEqual(JarState.Ready, sim.JarStateOf(LifeSpecies.Wolves));
            Assert.GreaterOrEqual(sim.DebugPlaceWolves(), 0);
            Assert.IsTrue(sim.DebugWolvesBold()); Assert.AreEqual(1f, sim.WolfBoldness);
            Assert.IsTrue(sim.DebugMeatThiefNow()); Assert.IsTrue(sim.WolfTheftScenePending);
            Assert.IsNotNull(sim.DebugWolfKillNow());
            Assert.IsTrue(sim.DebugWhiteWolfNow());
            Assert.IsTrue(sim.DebugWolvesAtPenNow());
            Assert.IsTrue(sim.DebugLostPupNow()); Assert.IsNotNull(sim.OpenDilemma);
            Assert.AreEqual(LifeChoices.PupDef.id, sim.OpenDilemma.defId);
            var off = new SimCore(new SimConfig(), SimCore.NewWorld(3, new SimConfig()));
            Assert.IsFalse(off.DebugReadyWolves()); Assert.AreEqual(-1, off.DebugPlaceWolves()); Assert.IsFalse(off.DebugWolvesBold());
            Assert.IsFalse(off.DebugMeatThiefNow()); Assert.IsNull(off.DebugWolfKillNow()); Assert.IsFalse(off.DebugWhiteWolfNow());
            Assert.IsFalse(off.DebugWolvesAtPenNow()); Assert.IsFalse(off.DebugLostPupNow()); Assert.AreEqual(0, off.DebugNextDogGeneration());
        }
    }
}
