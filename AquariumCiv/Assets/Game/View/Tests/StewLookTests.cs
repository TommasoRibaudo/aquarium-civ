// #676: each stew kind has its own look (broth colour, lumps); the pot builds a mesh for each.
using System.Collections.Generic;
using AquariumCiv.Sim;
using NUnit.Framework;
using UnityEngine;

namespace AquariumCiv.View.Tests
{
    public class StewLookTests
    {
        static readonly StewKind[] Kinds = { StewKind.Thin, StewKind.Berries, StewKind.Fish, StewKind.Rabbit };

        [Test]
        public void EveryKindHasItsOwnBrothAndLumps()
        {
            var seen = new HashSet<string>();
            foreach (var k in Kinds)
            {
                var l = MealDirector.LookOf(k);
                Assert.IsNotNull(l, k.ToString());
                Assert.Greater(l.bits.Length, 0, k + " has lumps");
                Assert.IsTrue(seen.Add(l.mid.ToString() + l.edge), k + " broth colour is its own");
            }
            // the three real stews are told apart by more than a shade: their lumps are mostly not shared
            Assert.AreNotEqual(MealDirector.LookOf(StewKind.Fish).bits.Length, MealDirector.LookOf(StewKind.Thin).bits.Length);
            Assert.Greater(Dist(MealDirector.LookOf(StewKind.Fish).mid, MealDirector.LookOf(StewKind.Rabbit).mid), 0.15f, "fish vs rabbit broth");
            Assert.Greater(Dist(MealDirector.LookOf(StewKind.Berries).mid, MealDirector.LookOf(StewKind.Rabbit).mid), 0.15f, "berries vs rabbit broth");
            Assert.Greater(Dist(MealDirector.LookOf(StewKind.Berries).mid, MealDirector.LookOf(StewKind.Fish).mid), 0.15f, "berries vs fish broth");
        }

        static float Dist(Color a, Color b) => Mathf.Abs(a.r - b.r) + Mathf.Abs(a.g - b.g) + Mathf.Abs(a.b - b.b);

        [Test]
        public void ThePotMeshesDifferByKind()
        {
            var cols = new List<List<Color>>();
            foreach (var k in Kinds)
            {
                var mb = new MeshBuilder(1);
                MealDirector.AddStewPot(mb, 0.045f, 0.04f, 0.052f, 0.06f, 3651, MealDirector.LookOf(k));
                var m = mb.ToMesh("t");
                var c = new List<Color>(); m.GetColors(c);
                cols.Add(c);
                Object.DestroyImmediate(m);
            }
            for (int i = 0; i < cols.Count; i++)
                for (int j = i + 1; j < cols.Count; j++)
                    Assert.AreNotEqual(string.Join(",", cols[i]), string.Join(",", cols[j]), $"{Kinds[i]} and {Kinds[j]} build different pots");
        }
    }
}
