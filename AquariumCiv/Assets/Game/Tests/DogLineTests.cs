using System;
using System.Collections.Generic;
using System.Linq;
using AquariumCiv.Sim;
using AquariumCiv.Sim.Eco;
using NUnit.Framework;
using UnityEngine;

namespace AquariumCiv.Tests
{
    /// <summary>#438 the dog line (docs/design/placed-life.md §5.7; research 19 §6.2, §7): adoption only through the lost-pup choice, the
    /// bond to the finder, the paths (scraps, chase two springs on, never closed off), stages, the cap of 3, the look's drift by
    /// generation, old age only between visits, the guard and the hunt help, the first dog's record, the save, and the dog arc reached on
    /// both answers of choice 6.1 by a Keeper who answers every choice.</summary>
    public class DogLineTests
    {
        static SimConfig Choices() => new SimConfig { ecology = true, dilemmas = true };
        static void Step(SimCore sim, int months) { for (int k = 0; k < months; k++) sim.Step(); }

        static SimCore Pack(SimConfig cfg = null, int seed = 7) => WolfPackTests.Pack(seed, cfg);

        /// <summary>Choice 6.1 answered now (0 chase, 1 scraps).</summary>
        static void Thief(SimCore sim, int option)
        {
            sim.DebugMeatThiefNow();
            var d = sim.DebugRaiseLifeChoice(LifeChoices.Thief);
            Assert.IsNotNull(d);
            Assert.IsTrue(sim.AnswerDilemma(d.id, option, DilemmaPath.Card));
        }

        /// <summary>The lost pup's choice: already open when the Keeper watched the spring it was found (user 2026-11-08: raised as it
        /// happens), else raised now by the debug.</summary>
        static Dilemma RaisedPup(SimCore sim)
        {
            var open = sim.OpenDilemma;
            return open != null && open.defId == LifeChoices.PupDef.id ? open : sim.DebugRaiseLifeChoice(LifeChoices.Pup);
        }

        static void ToMonth(SimCore sim, Func<long, bool> until, int max = 400) { for (int k = 0; k < max && !until(sim.State.month); k++) sim.Step(); }

        // ================================================================ adoption (choice 6.2)
        [Test]
        public void Adoption_OnlyThroughTheChoice_BondedToTheChildWhoFoundIt()
        {
            var none = Pack(); // choices off: a pup may be found, but nothing ever comes home by itself
            for (int m = 0; m < 30 * 12; m++) { none.Step(); Assert.AreEqual(0, none.DogCount, $"month {none.State.month}"); }
            Assert.IsTrue(none.LostPupWaiting, "the pup waits for the Keeper");

            var sim = Pack(Choices());
            WolfPackTests.OnlyLifeChoices(sim);
            Thief(sim, 1);
            ToMonth(sim, m => sim.LostPupWaiting);
            Assert.IsTrue(sim.LostPupWaiting, "a spring on the scraps path");
            Assert.AreEqual(0, EcoModel.SeasonOf(sim.State.eco.pupFoundMonth), "found in spring");
            Assert.Greater(sim.EcoWolfPups, 0, "with this spring's pups at the den");
            int finder = sim.LostPupFinder;
            var kid = sim.FindNotable(finder);
            Assert.IsNotNull(kid);
            var d = RaisedPup(sim);
            Assert.IsNotNull(d);
            Assert.AreEqual(finder, d.subjectId);
            StringAssert.StartsWith(kid.name + " found a wolf pup alone by the den.", sim.QuestionOf(d));
            Assert.AreEqual(0, sim.DogCount);
            Assert.IsTrue(sim.AnswerDilemma(d.id, 0, DilemmaPath.Card), "bring it home");
            Assert.AreEqual(1, sim.DogCount);
            var dog = sim.Dogs[0];
            Assert.AreEqual(finder, dog.keeperId, "bonded to the finder");
            Assert.AreEqual(finder, sim.DogKeeperOf(dog.id), "the dog's card shows its keeper");
            CollectionAssert.Contains(sim.DogsOfKeeper(finder), dog, "the keeper's card shows the dog");
            Assert.AreEqual(0, dog.gen, "a wolf pup");
            Assert.AreEqual(0f, dog.earDroop); Assert.AreEqual(1f, dog.muzzle);
            Assert.AreEqual(DogStage.Pup, sim.DogStageOf(dog));
            Assert.IsFalse(sim.DogCollar(dog));
            Assert.AreEqual(dog.id, sim.FirstDogId);
            Assert.AreEqual(kid.FullName, sim.FirstDogKeeperName, "the book's first dog, with its keeper's name");
            Assert.AreEqual(dog.name, sim.FirstDogName);
            StringAssert.Contains(dog.name, sim.State.chronicle.Last(x => x.kind == "first_dog").text);
            Assert.AreEqual(finder, sim.State.chronicle.Last(x => x.kind == "first_dog").notableId);
            Assert.IsFalse(sim.LostPupWaiting, "never again once a dog has come home");
        }

        [Test]
        public void ThePaths_Scraps_ChaseTwoSpringsOn_NoChoiceStillOpens_LeaveItAsksAgainNextSpring()
        {
            // the scraps path: the next spring with pups
            var scraps = Pack(Choices()); WolfPackTests.OnlyLifeChoices(scraps);
            Thief(scraps, 1);
            long chose = scraps.State.month;
            ToMonth(scraps, m => scraps.LostPupWaiting);
            Assert.Less(scraps.State.eco.pupFoundMonth - chose, 15, "the first spring");

            // the chase path: never before the second spring after the choice, but then it comes
            var chase = Pack(Choices()); WolfPackTests.OnlyLifeChoices(chase);
            Thief(chase, 0);
            long chased = chase.State.month;
            long secondSpring = chased - chased % 12 + 3 + (chased % 12 >= 3 ? 12 : 0) + 12;
            ToMonth(chase, m => chase.LostPupWaiting, 12 * 12);
            Assert.IsTrue(chase.LostPupWaiting, "dogs are never closed off");
            Assert.GreaterOrEqual(chase.State.eco.pupFoundMonth, secondSpring);

            // the thief never came (no choice): the third spring after the wolves came
            var quiet = Pack();
            long placed = quiet.LifePlacedMonth(LifeSpecies.Wolves);
            ToMonth(quiet, m => quiet.LostPupWaiting, 12 * 12);
            Assert.IsTrue(quiet.LostPupWaiting);
            Assert.GreaterOrEqual(quiet.State.eco.pupFoundMonth - placed, 24);

            // leave it with its mother: asked again next spring
            var d = RaisedPup(scraps);
            Assert.IsTrue(scraps.AnswerDilemma(d.id, 1, DilemmaPath.Card));
            Assert.AreEqual(0, scraps.DogCount);
            Assert.IsFalse(scraps.LostPupWaiting);
            long left = scraps.State.month;
            ToMonth(scraps, m => scraps.LostPupWaiting, 3 * 12);
            Assert.IsTrue(scraps.LostPupWaiting, "again");
            Assert.Greater(scraps.State.eco.pupFoundMonth, left);
            Assert.AreEqual(0, EcoModel.SeasonOf(scraps.State.eco.pupFoundMonth));
            Assert.AreNotEqual((left + 1) / 12, (scraps.State.eco.pupFoundMonth + 1) / 12, "a later spring");
            var d2 = RaisedPup(scraps);
            Assert.AreEqual(2, scraps.LostPupAsked);
            Assert.IsTrue(scraps.LetThemDecide(d2.id));
        }

        // ================================================================ stages, generations, the cap, old age
        [Test]
        public void FortyYears_NeverMoreThanThree_TheLookDriftsTowardDog_ByGeneration()
        {
            var sim = Pack();
            sim.DebugAdoptPup();
            int maxDogs = 0, extraLines = 0;
            for (int m = 0; m < 40 * 12; m++)
            {
                if (m % 12 == 0) sim.CatchUp(0.3); // between visits now and then (old dogs go then)
                else sim.Advance(1.0 / 12);
                maxDogs = Math.Max(maxDogs, sim.DogCount);
                Assert.LessOrEqual(sim.EcoWolfCount, 6, "the wild pack, extra pups and all, stays within its cap");
                Assert.AreEqual(2 + sim.EcoWolfPups + sim.EcoWolfYearlings, sim.EcoWolfCount);
                Assert.LessOrEqual(sim.DogCount, SimCore.DogCap, $"month {sim.State.month}");
                Assert.Greater(sim.DogCount, 0, "the line goes on");
            }
            extraLines = sim.State.chronicle.Count(x => x.kind == "dog_pups_wild");
            var all = sim.AllDogs;
            TestContext.Out.WriteLine($"{all.Count} dogs, gens {string.Join(",", all.Select(d => d.gen))}, extra-pup lines {extraLines}");
            Assert.AreEqual(SimCore.DogCap, maxDogs, "the camp fills to three");
            Assert.Greater(all.Max(d => d.gen), 4, "generations every 2-3 years");
            Assert.Greater(extraLines, 0, "extra pups beyond the cap leave, with a line");
            for (int g = 1; g <= 4; g++)
            {
                var gen = all.Where(d => d.gen == g).ToList();
                if (gen.Count == 0) continue;
                foreach (var d in gen)
                {
                    Assert.AreEqual(Math.Min(1f, 0.25f * g), d.earDroop, 0.1001f, "ear droop +0.25 a generation, +-0.1");
                    Assert.AreEqual(Math.Min(1f, 0.25f * g), d.patch, 0.1001f);
                    Assert.AreEqual(Math.Min(1f, 0.25f * g), d.tailCurl, 0.1001f);
                    Assert.AreEqual(1f - 0.05f * g, d.muzzle, 0.1001f, "a shorter face");
                }
            }
            float Mean(int g, Func<Dog, float> f) => all.Where(d => d.gen == g).Select(f).DefaultIfEmpty(float.NaN).Average();
            Assert.Less(Mean(1, d => d.earDroop), Mean(3, d => d.earDroop), "the drift");
            Assert.Greater(Mean(1, d => d.muzzle), Mean(4, d => d.muzzle));
            Assert.IsTrue(all.Where(d => d.gen >= 4).All(d => d.earDroop >= 0.9f), "by generation 4 they look like dogs");
            Assert.IsTrue(all.Any(d => !d.Alive), "old dogs are gone in time");
            Assert.IsTrue(all.Where(d => d.parentId >= 0).All(d => sim.FindDog(d.parentId).gen == d.gen - 1));
        }

        /// <summary>User 2026-11-08: one pup stays per litter, and the extra pups go back to the wild pack (as this year's pups, within
        /// its 2-6); past the cap they leave the box as young wolves do; with no pack in the box they leave.</summary>
        [Test]
        public void Litter_OnePupStays_TheExtraPupsJoinTheWildPack_ThePackStaysAtMostSix()
        {
            var sim = Pack();
            Assert.IsNotNull(sim.DebugAdoptPup());
            var e = sim.State.eco;
            e.wolfPups = 0; e.wolfYearlings = 0; e.wolves = 2;
            int left0 = sim.WolvesLeftTotal;
            int dogs = sim.DogCount;
            Assert.AreEqual(dogs + 1, sim.DebugNextDogGeneration(), "one pup stays");
            var litter = sim.State.chronicle.Last(x => x.kind == "dog_litter");
            int n = litter.text.Contains("had three pups") ? 3 : 2, extra = n - 1;
            Assert.AreEqual(2 + extra, sim.EcoWolfCount, "the extra pups joined the pack");
            Assert.AreEqual(extra, sim.EcoWolfPups, "as this year's pups");
            Assert.AreEqual(left0, sim.WolvesLeftTotal, "none left: there was room");
            var line = sim.State.chronicle.Last(x => x.kind == "dog_pups_wild").text;
            Assert.AreEqual(extra == 1 ? "One of the pups trotted off toward the far wood, and the wolves took it in."
                : "Two of the pups trotted off toward the far wood, and the wolves took them in.", line);
            Assert.IsFalse(sim.State.chronicle.Any(x => x.text.Contains("far side of the meadow") || x.text.Contains("people apart")));

            // a full pack: the extra pups leave the box as young wolves do, and the pack stays at six
            e.wolfPups = 4; e.wolves = 6;
            int left1 = sim.WolvesLeftTotal;
            sim.DebugNextDogGeneration();
            litter = sim.State.chronicle.Last(x => x.kind == "dog_litter");
            extra = (litter.text.Contains("had three pups") ? 3 : 2) - 1;
            Assert.AreEqual(6, sim.EcoWolfCount, "never past the cap");
            Assert.AreEqual(left1 + extra, sim.WolvesLeftTotal, "past the cap they leave");
            Assert.AreEqual(extra, sim.WolvesLeftLast);
            Assert.AreEqual(sim.State.month, sim.WolvesLeftMonth);

            // the pack grows past them with its own spring: still within the cap
            for (int m = 0; m < 5 * 12; m++) { sim.Step(); Assert.LessOrEqual(sim.EcoWolfCount, 6); }

            // no pack in the box (it shouldn't happen: the wolves come first): they leave
            var lone = WolfPackTests.Wooded();
            Assert.IsFalse(lone.WolvesPlaced);
            Assert.IsNotNull(lone.DebugAdoptPup());
            lone.DebugNextDogGeneration();
            Assert.AreEqual(0, lone.EcoWolfCount, "no pack made from the pups");
            Assert.Greater(lone.WolvesLeftTotal, 0);
        }

        [Test]
        public void OldAge_TheMuzzleGreys_ThenGoneBetweenVisits_NeverOnScreen_ALineLikeAPersons()
        {
            var sim = Pack();
            var dog = sim.DebugAdoptPup();
            dog.lifeMonths = 60;
            Step(sim, 40);
            Assert.AreEqual(DogStage.Old, sim.DogStageOf(dog));
            Assert.IsTrue(sim.DogGrey(dog));
            Assert.IsTrue(sim.State.chronicle.Any(x => x.kind == "dog_grey"));
            Step(sim, 60);
            Assert.IsTrue(dog.Alive, "never while the Keeper watches");
            sim.State.simHours = sim.State.month * sim.Config.StepHours; // sim.Step doesn't move the sim clock; the catch-up reads it
            sim.CatchUp(0.5);
            Assert.IsFalse(dog.Alive, "gone between visits");
            var line = sim.State.chronicle.Last(x => x.kind == "dog_died");
            StringAssert.Contains(dog.name, line.text);
            StringAssert.Contains("died of old age", line.text);
            Assert.AreEqual(dog.id, sim.FirstDogId, "the first dog's record stays");
        }

        [Test]
        public void Grown_TheCollar_TheGuard_StopsTheThief_AndHelpsTheHunt()
        {
            var sim = Pack();
            sim.DebugWolvesBold();
            var dog = sim.DebugAdoptPup();
            Assert.AreEqual(0f, sim.DogFlushBonus, "a pup doesn't hunt");
            Assert.IsFalse(sim.DogFindsMiss(1, 0));
            Assert.IsFalse(sim.MeatThiefStopped);
            var guards = new List<Dog>(); sim.DogFirstGuard += guards.Add;
            Step(sim, SimCore.DogGrownMonths);
            Assert.AreEqual(DogStage.Grown, sim.DogStageOf(dog));
            Assert.IsTrue(sim.DogCollar(dog), "a hide collar");
            Assert.AreEqual(1, guards.Count, "the first guard bark");
            Assert.IsTrue(sim.MeatThiefStopped, "the meat thief stops");
            Assert.Less(sim.WolfBoldness, 1f);
            int thefts = sim.WolfThefts;
            for (int m = 0; m < 5 * 12; m++) { sim.State.food = sim.FoodCap; sim.Step(); }
            Assert.AreEqual(thefts, sim.WolfThefts);
            Assert.IsFalse(sim.DebugMeatThiefNow());
            Assert.Greater(sim.DogFlushBonus, 0f, "flushes rabbits");
            int found = 0;
            for (int day = 0; day < 300; day++) if (sim.DogFindsMiss(day, 0)) found++;
            Assert.That(found, Is.InRange(70, 130), "1 time in 3");
            sim.DebugDeerToCap();
            int deer = sim.EcoDeerCount;
            Assert.IsTrue(sim.DogTrackCatch(out _), "a missed deer found");
            Assert.AreEqual(deer - 1, sim.EcoDeerCount);
            sim.DebugToFloor(LifeSpecies.Deer);
            Assert.IsFalse(sim.DogTrackCatch(out _), "never below the floor");
        }

        // ================================================================ the arc, played
        /// <summary>A Keeper who comes every hour and answers every choice as it comes (6.1 by <paramref name="thiefOption"/>, the pup:
        /// bring it home; every other: let them decide).</summary>
        static SimCore Played(int thiefOption, int seed)
        {
            var sim = Pack(Choices(), seed);
            sim.DebugWolvesBold(); // the thief comes sooner
            for (int m = 0; m < 30 * 12 && sim.FirstDogId < 0; m++)
            {
                foreach (var w in sim.State.wishes) if (w.state == WishState.Open) { w.state = WishState.Lapsed; w.endMonth = sim.State.month - 100; }
                sim.State.food = Math.Max(sim.State.food, sim.State.population * 2f + 2f);
                sim.Advance(1.0 / 12);
                var d = sim.OpenDilemma;
                if (d == null) continue;
                if (d.defId == LifeChoices.ThiefDef.id) sim.AnswerDilemma(d.id, thiefOption, DilemmaPath.Card);
                else if (d.defId == LifeChoices.PupDef.id) sim.AnswerDilemma(d.id, 0, DilemmaPath.Card);
                else sim.LetThemDecide(d.id);
            }
            return sim;
        }

        [Test]
        public void TheDogArc_IsReachedOnBothAnswersOfTheThiefChoice()
        {
            foreach (int option in new[] { 1, 0 })
            {
                var sim = Played(option, 7);
                Assert.AreEqual(option == 0 ? WolfWay.Chase : WolfWay.Scraps, sim.WolfWayTaken, "the thief was asked and answered");
                Assert.GreaterOrEqual(sim.FirstDogId, 0, (option == 0 ? "chase" : "scraps") + ": a dog came home");
                TestContext.Out.WriteLine($"{(option == 0 ? "chase" : "scraps")}: thief answered month {sim.State.eco.wolfWayMonth}, first dog month {sim.FirstDogMonth}");
                if (option == 0) Assert.GreaterOrEqual(sim.FirstDogMonth - sim.State.eco.wolfWayMonth, 12, "the chase path waits two springs");
            }
        }

        // ================================================================ save and debug
        [Test]
        public void SaveRoundTrip_KeepsTheDogs()
        {
            var sim = Pack();
            sim.DebugAdoptPup();
            sim.DebugNextDogGeneration(); sim.DebugNextDogGeneration();
            string json = JsonUtility.ToJson(sim.State);
            var loaded = new SimCore(sim.Config, JsonUtility.FromJson<SimState>(json));
            Assert.AreEqual(json, JsonUtility.ToJson(loaded.State));
            Assert.AreEqual(sim.DogCount, loaded.DogCount);
            CollectionAssert.AreEqual(sim.Dogs.Select(d => (d.id, d.gen, d.keeperId, d.earDroop, d.name)), loaded.Dogs.Select(d => (d.id, d.gen, d.keeperId, d.earDroop, d.name)));
            Assert.AreEqual(sim.FirstDogKeeperName, loaded.FirstDogKeeperName);
        }

        [Test]
        public void TheDebugHelpers_LostPup_NextGeneration()
        {
            var sim = Pack(Choices());
            Assert.IsTrue(sim.DebugLostPupNow());
            Assert.AreEqual(LifeChoices.PupDef.id, sim.OpenDilemma.defId);
            Assert.IsTrue(sim.AnswerDilemma(sim.OpenDilemma.id, 0, DilemmaPath.Card));
            Assert.AreEqual(1, sim.DogCount);
            Assert.IsFalse(sim.DebugLostPupNow(), "a dog came home already");
            Assert.AreEqual(2, sim.DebugNextDogGeneration(), "grown, then a litter: one pup stays");
            Assert.AreEqual(1, sim.Dogs.Max(d => d.gen));
            sim.DebugNextDogGeneration();
            Assert.LessOrEqual(sim.DogCount, SimCore.DogCap);
            Assert.AreEqual(2, sim.Dogs.Max(d => d.gen), "the next generation");
            var plain = Pack();
            Assert.IsNotNull(plain.DebugAdoptPup());
            Assert.IsNull(plain.DebugAdoptPup(), "one first pup");
        }
    }
}
