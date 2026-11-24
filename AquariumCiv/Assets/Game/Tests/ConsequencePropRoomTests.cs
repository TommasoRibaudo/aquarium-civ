using System.Collections.Generic;
using System.Linq;
using AquariumCiv.Sim;
using AquariumCiv.Sim.Settle;
using NUnit.Framework;
using static AquariumCiv.Tests.SimCoreTests;

namespace AquariumCiv.Tests
{
    /// <summary>
    /// B3 tester fixes (finding 4): the choices' things overlapped each other and a village laid its fields and houses over them. Two things
    /// keep their footprints (<see cref="ConsequenceFootprint"/>) apart, and no lot, field or path ever covers one: checked on real growth (a
    /// camp with every thing grown into a village and on), not only the jump cheat. Pure C#, so it also runs in Tools/SimTests.
    /// </summary>
    public class ConsequencePropRoomTests
    {
        static float Dist(ConsequenceProp a, ConsequenceProp b) => (float)System.Math.Sqrt((a.x - b.x) * (a.x - b.x) + (a.z - b.z) * (a.z - b.z));
        static bool Overhead(ConsequenceProp p) => p.anchor == PropAnchor.Tree && p.treeCell >= 0;
        /// <summary>The things drawn on the ground: every feather is the one pole (fb4 #566), so only the pole counts.</summary>
        static List<ConsequenceProp> Standing(SimCore sim)
        {
            var pole = sim.FeatherPoleProp;
            return sim.ConsequenceProps.Where(p => p.placed && !Overhead(p) && (p.kind != ConsequenceKind.Feather || p == pole)).ToList();
        }

        /// <summary>Every pair of standing things that are closer than their footprints allow (a tolerance of a tenth of a cell: the spots are cell
        /// middles).</summary>
        static List<string> Overlaps(SimCore sim)
        {
            var r = new List<string>();
            var ps = Standing(sim);
            for (int i = 0; i < ps.Count; i++)
                for (int j = i + 1; j < ps.Count; j++)
                {
                    float need = ConsequenceFootprint.Radius(ps[i].kind) + ConsequenceFootprint.Radius(ps[j].kind);
                    if (Dist(ps[i], ps[j]) < need - 0.01f) r.Add($"{ps[i].kind}#{ps[i].id} and {ps[j].kind}#{ps[j].id} {Dist(ps[i], ps[j]):0.00} u apart (need {need:0.00})");
                }
            return r;
        }

        /// <summary>Every standing thing whose footprint a standing lot (a house, a field, a barn, a yard) covers.</summary>
        static List<string> UnderLots(SimCore sim)
        {
            var r = new List<string>();
            var st = sim.Settlement;
            if (st == null) return r;
            var cells = new List<int>();
            foreach (var p in Standing(sim))
            {
                ConsequenceFootprint.Cells(p.kind, p.x, p.z, 0f, cells);
                foreach (int c in cells)
                {
                    int lot = st.Map.lot[c];
                    if (lot == 0) continue;
                    var l = st.Lots.FirstOrDefault(x => x.id == lot);
                    if (l == null || l.retired) continue;
                    r.Add($"{p.kind}#{p.id} at ({p.x:0.00}, {p.z:0.00}) under lot {l.kind}#{l.id}");
                    break;
                }
            }
            return r;
        }

        [Test]
        public void EveryThingKeepsItsFootprintClearOfTheOthers_OnBothLands()
        {
            foreach (bool living in new[] { false, true })
                for (int seed = 1; seed <= 4; seed++)
                {
                    var cfg = new SimConfig { dilemmas = true, ecology = living };
                    var sim = NewSim(seed, cfg);
                    for (int m = 0; m < 24; m++) sim.Advance(1.0 / 12);
                    int n = sim.DebugPlaceEveryProp();
                    Assert.AreEqual(sim.ConsequenceProps.Count, n, "every thing stands");
                    var bad = Overlaps(sim);
                    Assert.IsEmpty(bad, (living ? "living" : "slice") + " seed " + seed + ": " + string.Join("; ", bad));
                    var under = UnderLots(sim);
                    Assert.IsEmpty(under, (living ? "living" : "slice") + " seed " + seed + ": " + string.Join("; ", under));
                }
        }

        /// <summary>The camp with every thing grows into a village and on for years (the lots, the fields, the barns, the paths): no lot ever
        /// covers a thing and the things never overlap.</summary>
        [Test]
        public void ACampWithEveryThingGrowsIntoAVillage_NoLotOrFieldCoversAThing()
        {
            int grown = 0, fields = 0;
            foreach (bool living in new[] { false }) // the living land does not grow untended (it needs the Keeper's mister): the slice land grows
                for (int seed = 1; seed <= 5; seed++)
                {
                    var cfg = new SimConfig { dilemmas = true, ecology = living };
                    var sim = NewSim(seed, cfg);
                    for (int m = 0; m < 24; m++) sim.Advance(1.0 / 12);
                    sim.DebugPlaceEveryProp();
                    string where = (living ? "living" : "slice") + " seed " + seed;
                    var seen = new List<string>();
                    for (int h = 0; h < 160 && sim.State.era < Era.Town; h++)
                    {
                        sim.Advance(1);
                        if (sim.State.eraReady) sim.AdvanceEra();
                        foreach (var u in UnderLots(sim)) if (!seen.Contains(u)) seen.Add($"hour {h} ({sim.State.era}): {u}");
                    }
                    if (sim.State.era >= Era.Village) grown++;
                    System.Console.WriteLine($"{where}: {sim.State.era}, {sim.Settlement.Lots.Count(l => !l.retired)} lots ({sim.Settlement.Lots.Count(l => !l.retired && l.kind == LotKind.Farm)} fields), {Standing(sim).Count} things");
                    Assert.IsEmpty(seen, where + ": " + string.Join("; ", seen.Take(6)));
                    var bad = Overlaps(sim);
                    Assert.IsEmpty(bad, where + ": " + string.Join("; ", bad));
                    if (sim.Settlement.Lots.Any(l => !l.retired && l.kind == LotKind.Farm)) fields++;
                }
            Assert.GreaterOrEqual(grown, 4, "most of the camps grew into villages");
            Assert.GreaterOrEqual(fields, 2, "villages laid their fields (round the things)");
        }

        /// <summary>A lot laid over a thing before this fix (an old save) moves the thing at the next month.</summary>
        [Test]
        public void AThingUnderALotMovesToAClearSpot()
        {
            var sim = NewSim(2, new SimConfig { dilemmas = true });
            for (int m = 0; m < 24; m++) sim.Advance(1.0 / 12);
            sim.DebugPlaceEveryProp();
            var st = sim.Settlement;
            var lot = st.Lots.First(l => !l.retired && !l.open);
            var p = sim.ConsequenceProps.First(x => x.placed && !Overhead(x) && x.kind == ConsequenceKind.Staff);
            p.x = lot.x; p.z = lot.z; // as an old save had it
            Assert.IsTrue(sim.PropCovered(p), "the staff stands on a lot");
            Assert.GreaterOrEqual(sim.MoveCoveredProps(), 1);
            Assert.IsFalse(sim.PropCovered(p), "moved off the lot");
            Assert.IsEmpty(UnderLots(sim));
        }
    }
}
