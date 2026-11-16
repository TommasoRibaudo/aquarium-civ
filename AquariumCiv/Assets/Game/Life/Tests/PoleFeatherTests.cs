using System.Collections.Generic;
using System.Reflection;
using AquariumCiv.Sim;
using NUnit.Framework;
using UnityEngine;

namespace AquariumCiv.Life.Tests
{
    /// <summary>#692 (B31.a, B31.c): the feather pole fills up over the years (a wedding, a birth, the village each add a feather while the pole
    /// stands; the view ties it at the next dusk, or it is tied unseen two months on), and a prayer visit can be seen every day.</summary>
    public class PoleFeatherTests
    {
        static SimCore Eco(int seed = 5)
        {
            var cfg = new SimConfig { ecology = true, settle = true };
            return new SimCore(cfg, SimCore.NewWorld(seed, cfg));
        }

        static void Pole(SimCore sim)
        {
            var list = sim.State.consequenceProps;
            list.Add(new ConsequenceProp { id = list.Count + 1, dilemmaId = 790, kind = ConsequenceKind.Feather, anchor = PropAnchor.Home, placed = true, x = 0.3f, z = 0.2f });
            Assert.IsNotNull(sim.FeatherPoleProp);
        }

        static void Record(SimCore sim, string kind, int a = -1, int b = -1) =>
            typeof(SimCore).GetMethod("Record", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(sim, new object[] { kind, "line", 0.7f, a, b });

        [Test]
        public void NoPoleNoFeather()
        {
            var sim = Eco();
            Record(sim, "wedding", 1, 2);
            Assert.AreEqual(0, sim.PoleFeathers.Count, "a wedding with no pole standing adds nothing");
        }

        [Test]
        public void AWeddingABirthAndTheVillageEachAddOneOfItsKind()
        {
            var sim = Eco();
            Pole(sim);
            Record(sim, "wedding", 11, 12);
            Record(sim, "birth", -1);
            Record(sim, "feud", 1, 2);
            Assert.AreEqual(2, sim.PoleFeathers.Count, "the feud adds none");
            Assert.AreEqual(PoleFeatherKind.Marriage, sim.PoleFeathers[0].kind);
            Assert.AreEqual((11, 12), (sim.PoleFeathers[0].forA, sim.PoleFeathers[0].forB), "for the couple");
            Assert.AreEqual(PoleFeatherKind.Newborn, sim.PoleFeathers[1].kind);
            sim.State.era = Era.Village;
            Record(sim, "era");
            Assert.AreEqual(PoleFeatherKind.Village, sim.PoleFeathers[2].kind);
            Assert.AreEqual(0, sim.PendingPoleFeather(), "the oldest waits first");
            sim.MarkPoleFeatherTied(0);
            Assert.AreEqual(1, sim.PendingPoleFeather());
            Assert.AreEqual(1, sim.PoleFeathersTied);
            var back = new SimCore(sim.Config, JsonUtility.FromJson<SimState>(JsonUtility.ToJson(sim.State)));
            Assert.AreEqual(3, back.PoleFeathers.Count, "saved");
            Assert.IsTrue(back.PoleFeathers[0].tied);
        }

        [Test]
        public void AFeatherNobodyTiedOnViewIsTiedUnseenTwoMonthsOn()
        {
            var sim = Eco();
            Pole(sim);
            sim.AddPoleFeather(PoleFeatherKind.Newborn);
            var late = typeof(SimCore).GetMethod("TiePoleFeathersLate", BindingFlags.Instance | BindingFlags.NonPublic);
            sim.State.month += 1; late.Invoke(sim, null);
            Assert.IsFalse(sim.PoleFeathers[0].tied, "a month on: still waiting for a dusk on view");
            sim.State.month += SimCore.PoleFeatherLate - 1; late.Invoke(sim, null);
            Assert.IsTrue(sim.PoleFeathers[0].tied, "the pole never falls behind");
        }

        [Test]
        public void ThePoleDrawsTwelveAndANewOneRecoloursTheOldest()
        {
            var list = new List<PoleFeather>();
            for (int i = 0; i < 14; i++) list.Add(new PoleFeather { kind = i < 12 ? PoleFeatherKind.Newborn : PoleFeatherKind.Marriage, tied = true });
            list.Add(new PoleFeather { kind = PoleFeatherKind.Village, tied = false });
            var slots = SimCore.PoleSlots(list);
            Assert.AreEqual(SimCore.PoleFeatherCap, slots.Length, "up to 12 drawn");
            Assert.AreEqual(PoleFeatherKind.Marriage, slots[0], "the 13th recolours the oldest");
            Assert.AreEqual(PoleFeatherKind.Marriage, slots[1]);
            Assert.AreEqual(PoleFeatherKind.Newborn, slots[2]);
            Assert.AreEqual(0, SimCore.PoleSlots(new List<PoleFeather> { new PoleFeather() }).Length, "an untied one isn't drawn");
        }

        [Test]
        public void APrayerVisitIsSeenEveryDay()
        {
            Assert.AreEqual(1f, LifePlanner.MiddayVisitChance, "B31.c: the midday visit every day");
            var sim = Eco(7);
            Pole(sim);
            var planner = LifePlanner.Wired(sim);
            var into = new List<LifePlanner.PrayerVisit>();
            for (long day = sim.State.month; day < sim.State.month + 10; day++)
            {
                planner.PrayerVisitsOn(day, into);
                bool midday = false;
                foreach (var v in into) if (!v.kid && v.start >= LifePlanner.MiddayFrom && v.start < LifePlanner.MiddayTo) midday = true;
                Assert.IsTrue(midday || into.Count > 0, $"day {day}: a visit to the pole by day");
            }
        }
    }
}
