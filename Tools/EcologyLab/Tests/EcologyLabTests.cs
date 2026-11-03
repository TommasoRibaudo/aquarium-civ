using System;
using System.Collections.Generic;
using EcologyLab;
using NUnit.Framework;

namespace EcologyLab.Tests
{
    /// <summary>#228's Done-when checks, on all three river-valley terrains.</summary>
    public class EcologyLabTests
    {
        static readonly Dictionary<(int, Strategy), Run> cache = new Dictionary<(int, Strategy), Run>();

        static Run Get(int p, Strategy s)
        {
            lock (cache)
            {
                if (!cache.TryGetValue((p, s), out var r)) cache[(p, s)] = r = new Run(new Land(Program.Presets[p]), s, Program.Seeds[p]).Go();
                return r;
            }
        }

        static IEnumerable<int> Terrains() => new[] { 0, 1, 2 };

        [Test]
        public void SameSeedSameBox_FramesIdentical()
        {
            var land = new Land(0);
            var a = new Run(land, Strategy.C_Care, 7) { years = 40 }.Go();
            var b = new Run(new Land(0), Strategy.C_Care, 7) { years = 40 }.Go();
            Assert.That(b.eco.Fingerprint(), Is.EqualTo(a.eco.Fingerprint()));
            Assert.That(Render.Frame(land, b.frames[^1], 1, b.strategy).px, Is.EqualTo(Render.Frame(land, a.frames[^1], 1, a.strategy).px));
            var other = new Run(land, Strategy.C_Care, 8) { years = 40 }.Go();
            Assert.That(other.eco.Fingerprint(), Is.Not.EqualTo(a.eco.Fingerprint()), "a different seed gives a different box");
        }

        [TestCaseSource(nameof(Terrains))]
        public void Absence_StillGreens(int p)
        {
            var a = Get(p, Strategy.A_Nothing);
            var y40 = a.curve[39];
            Assert.That(y40.grassPct + y40.tallPct, Is.GreaterThan(10f), "(a) greens by year 40");
            Assert.That(a.eco.campYear, Is.GreaterThan(0f).And.LessThan(40f), "(a) the camp starts by year 40");
            Assert.That(a.eco.villageYear, Is.GreaterThan(0f), "(a) the village comes eventually (by year 100)");
        }

        [TestCaseSource(nameof(Terrains))]
        public void CareGrowsTheTree_ClearlySooner(int p)
        {
            var b = Get(p, Strategy.B_Casual).eco; var c = Get(p, Strategy.C_Care).eco; var a = Get(p, Strategy.A_Nothing).eco;
            Assert.That(c.keeperGrownYear, Is.GreaterThan(0f).And.LessThan(20f), "(c) grows the tree");
            Assert.That(b.keeperGrownYear, Is.GreaterThan(0f), "(b) grows it too, later");
            float cTime = c.keeperGrownYear - c.plantedYear, bTime = b.keeperGrownYear - b.plantedYear;
            Assert.That(bTime, Is.GreaterThan(2f * cTime), "planting to grown: (b) at least 2× (c)");
            Assert.That(b.keeperGrownYear, Is.GreaterThan(c.keeperGrownYear + 10f), "(c) clearly sooner");
            Assert.That(a.keeperGrownYear, Is.LessThan(0f), "(a) never plants");
            Assert.That(a.firstTreeYear < 0f || a.firstTreeYear > c.keeperGrownYear + 25f, Is.True, "(a) a tree only slowly, from a passing bird");
        }

        [TestCaseSource(nameof(Terrains))]
        public void OverMisting_SurfacesWaterSooner(int p)
        {
            var c = Get(p, Strategy.C_Care).eco; var d = Get(p, Strategy.D_OverMist).eco;
            Assert.That(d.surfacedYear, Is.GreaterThan(0f));
            Assert.That(d.surfacedYear, Is.LessThan(c.surfacedYear));
            Assert.That(Get(p, Strategy.D_OverMist).tooHumidSeasons, Is.GreaterThan(0), "(d) shows the too-humid state");
            Assert.That(Get(p, Strategy.C_Care).tooHumidSeasons, Is.EqualTo(0), "(c) never fogs the glass");
        }

        [TestCaseSource(nameof(Terrains))]
        public void TheArc_InOrder_WithCare(int p)
        {
            var c = Get(p, Strategy.C_Care).eco;
            Assert.That(c.campYear, Is.GreaterThan(0f).And.LessThan(6f), "camp within the first game years");
            Assert.That(c.bushYear, Is.GreaterThanOrEqualTo(c.campYear));
            Assert.That(c.plantedYear, Is.GreaterThanOrEqualTo(c.bushYear));
            Assert.That(c.keeperGrownYear, Is.GreaterThan(c.plantedYear));
            Assert.That(c.surfacedYear, Is.GreaterThan(c.keeperGrownYear), "the water rises after the tree");
            Assert.That(c.villageYear, Is.GreaterThanOrEqualTo(c.surfacedYear), "water before the village");
        }

        [TestCaseSource(nameof(Terrains))]
        public void PondsOnlyInBasins_AndNothingDiesBack(int p)
        {
            var land = new Land(Program.Presets[p]);
            foreach (Strategy s in Enum.GetValues(typeof(Strategy)))
            {
                var run = new Run(land, s, Program.Seeds[p]) { years = 60 };
                int lastStage = 0;
                run.afterStep = (e, before) =>
                {
                    for (int c = 0; c < Land.N; c++)
                    {
                        var now = e.cover[c]; var was = before[c];
                        if (now == Cover.Water || now == Cover.Reeds)
                            Assert.That(land.InBasin(c), Is.True, $"{s}: water/reeds outside a basin at cell {c}");
                        if (was == Cover.Water) Assert.That(now, Is.EqualTo(Cover.Water), $"{s}: a pond cell dried");
                        else if (now != Cover.Water && now != Cover.Reeds && was != Cover.Reeds)
                            Assert.That((int)now, Is.GreaterThanOrEqualTo((int)was), $"{s}: cell {c} died back {was} → {now} in year {e.Year}");
                    }
                    Assert.That(e.tree.stage, Is.GreaterThanOrEqualTo(lastStage));
                    lastStage = e.tree.stage;
                };
                run.Go();
            }
        }

        [TestCaseSource(nameof(Terrains))]
        public void TheFloodLine_IsDryAtTheStart(int p)
        {
            var land = new Land(Program.Presets[p]);
            var e = new Eco(land, Program.Seeds[p]);
            Assert.That(land.main.cells.Count, Is.GreaterThan(50), "a real hollow to fill");
            Assert.That(e.pondCells, Is.EqualTo(0));
            Assert.That(e.table, Is.LessThan(land.minHeight), "the water table starts below the lowest ground");
            for (int c = 0; c < Land.N; c++) Assert.That(e.cover[c], Is.EqualTo(Cover.Bare));
            // and still dry after the first season of the heaviest misting
            var d = new Run(land, Strategy.D_OverMist, Program.Seeds[p]) { years = 1 }.Go();
            Assert.That(d.eco.pondCells, Is.EqualTo(0));
        }

        [Test]
        public void PngWriter_WritesAValidFile()
        {
            var cv = new Canvas(4, 3, 0x336699);
            cv.Text(0, 0, "Y1", 0xFFFFFF, 1);
            string path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ecolab-test.png");
            cv.SavePng(path);
            var bytes = System.IO.File.ReadAllBytes(path);
            Assert.That(bytes[1..4], Is.EqualTo(new byte[] { 80, 78, 71 }));
            Assert.That(bytes.Length, Is.GreaterThan(50));
        }
    }
}
