using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AquariumCiv.Game;
using AquariumCiv.Sim;
using AquariumCiv.Sim.Eco;
using AquariumCiv.Sim.Settle;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AquariumCiv.View.PlayTests
{
    /// <summary>
    /// #669 (playtests 1, feedback 30 and 52): History draws only what existed in the replayed time. Life placed and things built
    /// after year 1 are not in the box at year 1 (no berries, rabbits, tracks, rack, woodpile, props, tents) and are all back at now.
    /// </summary>
    [Category("TimeLapsePlay")]
    public class HistoryBuiltPlayTests : PlayTestBase
    {
        static SimCore Sim => GameClock.Instance.ActiveSim;

        /// <summary>Active renderers under the box, by the name of the top object they sit in (the drawn world, counted).</summary>
        static Dictionary<string, int> Drawn(BoxView box)
        {
            var d = new Dictionary<string, int>();
            foreach (var r in box.GetComponentsInChildren<Renderer>(false))
            {
                if (!r.enabled || !r.gameObject.activeInHierarchy) continue;
                var t = r.transform;
                while (t.parent != null && t.parent != box.transform && t.parent != box.Root) t = t.parent;
                string k = t.name;
                int i = k.IndexOf('_'); if (i > 0 && char.IsDigit(k[k.Length - 1])) k = k.Substring(0, i);
                d[k] = d.TryGetValue(k, out var n) ? n + 1 : 1;
            }
            return d;
        }

        static string Dump(string tag, BoxView box) =>
            $"[669dump] {tag}: " + string.Join(", ", Drawn(box).OrderBy(k => k.Key).Select(kv => kv.Key + "=" + kv.Value));

        static void Meadow()
        {
            foreach (var (x, z) in new[] { (-1.6f, 0.6f), (-1.0f, -0.8f), (-2.0f, -0.2f), (1.6f, 0.9f) }) EcoDebug.MistBig(x, z);
            EcoDebug.SkipSeasons(1);
        }

        static int PlaceNear(LifeSpecies s, float x, float z)
        {
            if (!Sim.JarReady(s)) Sim.DebugJarsReady();
            int best = -1; float bd = float.MaxValue;
            for (int c = 0; c < SiteMap.N; c++)
            {
                if (!Sim.CanPlaceLife(s, c)) continue;
                float dx = SiteMap.CXc(c) - x, dz = SiteMap.CZc(c) - z, d = dx * dx + dz * dz;
                if (d < bd) { bd = d; best = c; }
            }
            Assert.GreaterOrEqual(best, 0, "a cell for " + s);
            Assert.IsTrue(Sim.PlaceLife(s, best), s + " placed");
            return best;
        }

        static bool Has(Dictionary<string, int> d, string key) => d.Keys.Any(k => k.StartsWith(key));

        [UnityTest, Timeout(900000)]
        public IEnumerator History_YearOneDrawsNothingPlacedOrBuiltLater()
        {
            UseLivingLand();
            yield return OpenSlice(seed: 4242);
            var box = Object.FindAnyObjectByType<BoxView>();
            var src = SimLayoutSource.Find();
            // year 1 (index 0) passes with nothing placed or built; everything below is year 2 or later
            SimLayoutSource.DebugSkipYears(1f);
            yield return Frames(20);
            Assert.GreaterOrEqual(Sim.State.month, SimConfig.MonthsPerYear, "year 1 is over");
            Sim.DebugJarsReady();
            Assert.GreaterOrEqual(Sim.DebugPlaceNearCamp(LifeSpecies.Thicket), 0, "thicket placed");
            Meadow();
            Assert.GreaterOrEqual(Sim.DebugPlaceNearCamp(LifeSpecies.Rabbits), 0, "rabbits placed");
            var hut = Sim.DebugBuild(BuildingType.Hut);
            var lot = Sim.State.settle.lots.Find(l => l.buildingId == hut.id);
            Assert.IsNotNull(lot, "the new tent has a lot");
            Assert.GreaterOrEqual(ConsequenceCheats.PlaceEvery().Length, 1);
            Assert.Greater(Sim.ConsequenceProps.Count(p => p.placed && p.placedMonth >= SimConfig.MonthsPerYear), 0, "things left in year 2");
            string rack = FishRackView.DebugRack();
            Debug.Log("[669] rack cheat: " + rack);
            yield return Frames(90);
            Assert.GreaterOrEqual(Sim.LifePlacedMonth(LifeSpecies.Rabbits), SimConfig.MonthsPerYear, "rabbits placed after year 1");
            Assert.IsNotNull(RabbitView.Instance, "live: rabbits");
            Assert.IsNotNull(BushView.Instance, "live: bushes");
            bool rackLive = FishRackView.Instance != null && FishRackView.Instance.Visible;
            long rackSince = Sim.ThingSince("rack");
            Assert.AreEqual(rackLive, rackSince >= SimConfig.MonthsPerYear, "the rack's first month was noted when it first stood (after year 1)");
            var live = Drawn(box);
            Debug.Log(Dump("live", box));
            Assert.IsTrue(Has(live, "ConsequenceProps"), "live: the things the choices left");
            Assert.IsTrue(src.Replay == null, "no replay yet");
            SimLayoutSource.DebugSkipYears(1f); // the box has a year 3 behind these
            yield return Frames(30);

            // ---- year 1
            Assert.IsTrue(TimeLapseView.DebugOpen().StartsWith("time-lapse open"), "replay opens");
            yield return Frames(20);
            TimeLapseView.DebugSeek(0f);
            yield return Frames(40);
            var y1 = Drawn(box);
            Debug.Log(Dump("year 1", box));
            Assert.IsTrue(ReplayGate.Active && !ReplayGate.AtPresent && ReplayGate.Month == 0, "the gate says month 0");
            Assert.IsNull(BushView.Instance, "no berries in year 1");
            Assert.IsNull(RabbitView.Instance, "no rabbits and no tracks in year 1");
            Assert.IsFalse(Has(y1, "Rabbits") || Has(y1, "Bushes") || Has(y1, "Arrival_"), "no animals, berries or thicket mark drawn");
            Assert.IsFalse(Has(y1, "ConsequenceProps"), "no props left by year-2 choices");
            Assert.IsFalse(Has(y1, "Fish rack"), "no rack");
            Assert.IsFalse(src.Replay.Shows(lot), "the year-2 tent is not in year 1's box");
            yield return Shot("669/hist669_year1");

            // ---- back to now (the end of the timeline, then closed)
            TimeLapseView.DebugSeek(TimeLapseView.Find().Lapse.EndPosition);
            yield return Frames(40);
            Assert.IsTrue(ReplayGate.AtPresent, "the end of the timeline is now");
            var end = Drawn(box);
            Assert.IsTrue(Has(end, "ConsequenceProps"), "at the end of the timeline the props stand");
            Assert.IsTrue(src.Replay.Shows(lot), "and the tent");
            TimeLapseView.DebugClose();
            yield return Frames(60);
            Assert.IsFalse(ReplayGate.Active, "gate off");
            var now = Drawn(box);
            Debug.Log(Dump("now", box));
            Assert.IsNotNull(RabbitView.Instance, "back: rabbits");
            Assert.IsNotNull(BushView.Instance, "back: berries");
            Assert.IsTrue(Has(now, "ConsequenceProps"), "back: props");
            Assert.IsTrue(Has(now, "Rabbits"), "back: rabbits drawn");
            if (rackLive) Assert.IsTrue(FishRackView.Instance.Visible, "back: the rack");
            Assert.IsTrue(box.layout.pieces.Any(p => p.seed == SimLayout.SeedOfLot(lot.id)) || box.layout.fields.Any(f => f.seed == SimLayout.SeedOfLot(lot.id)), "back: the tent is in the layout");
            yield return Shot("669/hist669_now");
            AssertNoErrors("history built");
            CloseSlice();
        }
    }
}
