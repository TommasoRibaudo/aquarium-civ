using System.Collections;
using System.Collections.Generic;
using AquariumCiv.Game;
using AquariumCiv.Life;
using AquariumCiv.Sim;
using AquariumCiv.Sim.Settle;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AquariumCiv.View.PlayTests
{
    /// <summary>
    /// Box 2 in its setting (#115), on the real scene with every bootstrap, the way the debug panel starts it: the box draws the
    /// sim's land, people stand on it, nothing stands in the water, a tap on a doll and a wipe still work. The look itself is the
    /// user's call: <see cref="LookSet"/> (Explicit) takes the set of camp, village and town at noon and 22 h at both phone sizes.
    /// </summary>
    [Category("SettingBox")]
    public class SettingBoxPlayTests : PlayTestBase
    {
        const int Seed = 20261101;

        IEnumerator OpenBox2(string setting, int w = 1179, int h = 2556)
        {
            yield return OpenSlice(w, h, Seed);
            string said = BoxCompleteController.DebugComplete(setting);
            Debug.Log("[setting] " + said);
            yield return Frames(30);
            if (FirstRunView.Instance != null && FirstRunView.Instance.Active) FirstRunView.DebugSkipAll(); // a new box has its short first run (#198)
            yield return Frames(4);
            Assert.AreEqual(1, GameClock.Instance.ActiveSim.State.boxIndex, said);
            Assert.IsFalse(GameClock.Instance.Visiting, said);
        }

        static BoxView Box => PeopleView.Find().GetComponent<BoxView>();

        static IEnumerator ToEra(Era era)
        {
            var src = SimLayoutSource.Find();
            bool wave = src.waveOnEraChange;
            src.waveOnEraChange = false;
            var sim = GameClock.Instance.Sim;
            for (int guard = 0; guard < 60 && sim.State.era < era; guard++)
            {
                SimLayoutSource.DebugSkipYears(10f);
                yield return null;
            }
            while (sim.State.era < era && sim.ForceNextEra()) { }
            SimLayoutSource.DebugSkipYears(sim.State.era == Era.Camp ? 3f : 6f); // a few homes on the new era's ground
            sim.CleanAll();
            src.GetComponent<BoxView>().Refresh();
            yield return null;
            src.waveOnEraChange = wave;
        }

        /// <summary>The land under the sim and the land on screen are one function, and nothing the sim built or lit stands in the water.</summary>
        static void AssertGroundHolds(string setting)
        {
            var sim = GameClock.Instance.ActiveSim;
            var box = Box;
            int preset = TerrainSpec.PresetFor(setting);
            Assert.AreEqual(preset, sim.TerrainPreset);
            Assert.AreEqual(preset, box.land.preset, "the box draws the box's land");
            var spec = TerrainSpec.Preset(preset);
            for (float x = -2.9f; x < 3f; x += 0.37f)
                for (float z = -2.1f; z < 2.2f; z += 0.31f)
                    Assert.AreEqual(AquariumCiv.Sim.Settle.Terrain.Height(spec, x, z), box.land.Height(x, z), 1e-6f, "the drawn land is the sim's");
            var st = sim.Settlement;
            foreach (var l in st.Lots)
            {
                if (l.retired || l.kind == LotKind.Jetty || l.kind == LotKind.Watermill) continue;
                foreach (var c in SimLayout.Corners(l, 0.03f))
                    Assert.IsFalse(box.land.IsWater(c.x, c.y), $"{l.kind} {l.id} at {l.x:0.00},{l.z:0.00} stands in the water");
            }
            foreach (var p in box.layout.paths)
                if (p.points != null)
                    foreach (var q in p.points)
                        Assert.IsFalse(box.land.IsWater(q.x, q.y) && box.ShoreDistance(q) < -0.06f, $"a {p.tier} path runs through the water at {q}");
            foreach (var t in box.layout.trees) Assert.IsFalse(box.InPond(t.pos), "a tree in the water");
            foreach (var b in box.layout.boulders) Assert.IsFalse(box.InPond(b.pos), "a boulder in the water");
        }

        static void AssertPeopleOnTheGround()
        {
            var pv = PeopleView.Find();
            var box = Box;
            var ids = new List<int>();
            pv.NotablesShown(ids);
            Assert.Greater(ids.Count, 0, "somebody is shown");
            foreach (int n in ids)
            {
                if (!pv.TryGetDollFeet(PersonRef.Notable(n), out var w)) continue;
                var at = pv.transform.InverseTransformPoint(w);
                Assert.AreEqual(box.GroundHeight(at.x, at.z), at.y, 0.2f, $"doll {n}'s feet are on the ground at {at}");
                Assert.IsFalse(box.InPond(new Vector2(at.x, at.z)) && box.ShoreDistance(new Vector2(at.x, at.z)) < -0.2f, $"doll {n} stands deep in the water at {at}");
            }
        }

        [UnityTest]
        public IEnumerator DesertBoxGround() => Ground(BoxSetting.DesertOasis);

        [UnityTest]
        public IEnumerator MountainBoxGround() => Ground(BoxSetting.MountainVillage);

        IEnumerator Ground(string setting)
        {
            yield return OpenBox2(setting);
            PeopleView.DebugSetHour(11f);
            foreach (var era in new[] { Era.Camp, Era.Village, Era.Town })
            {
                yield return ToEra(era);
                PeopleView.DebugSetHour(11f);
                yield return Frames(20);
                AssertGroundHolds(setting);
                AssertPeopleOnTheGround();
                Box.Rebuild();
                yield return null;
                Debug.Log($"[setting] {setting} {era}: {Box.layout.pieces.Count} pieces, {Box.layout.trees.Count} trees, {Box.layout.boulders.Count} boulders, {Box.layout.paths.Count} paths; {Box.lastBuildInfo}");
            }
            AssertNoErrors(setting);
        }

        [UnityTest]
        public IEnumerator TapsAndWipesWorkOnTheNewGround()
        {
            yield return OpenBox2(BoxSetting.MountainVillage);
            yield return ToEra(Era.Village);
            PeopleView.DebugSetHour(11f);
            yield return Frames(10);
            // a ground tap lands on the drawn height, a doll opens their card
            var box = Box;
            var cam = Camera.main;
            var spec = box.land.Spec;
            foreach (var at in new[] { new Vector2(spec.centreX, spec.centreZ), new Vector2(-1.8f, 1.5f) })
            {
                var world = box.transform.TransformPoint(box.land.OnGround(at));
                var sp = (Vector2)cam.WorldToScreenPoint(world);
                Assert.IsTrue(box.land.Inside(at, 0f));
                yield return TapAt(sp);
                yield return Frames(4);
            }
            var cards = PersonCardView.Instance;
            var pv = PeopleView.Find();
            pv.timeScale = 0.05f;
            var ids = new List<int>();
            pv.NotablesShown(ids);
            bool opened = false;
            foreach (int n in ids)
            {
                if (!pv.TryGetDollTop(PersonRef.Notable(n), out var top) || !pv.TryGetDollFeet(PersonRef.Notable(n), out var feet)) continue;
                var p = (Vector2)cam.WorldToScreenPoint((top + feet) * 0.5f);
                if (p.x < 40 || p.y < 200 || p.x > Screen.width - 40 || p.y > Screen.height - 200) continue;
                yield return TapAt(p);
                yield return Until(() => cards.IsOpen, 1.5f);
                if (cards.IsOpen) { opened = true; break; }
            }
            Assert.IsTrue(opened, "a tap on a doll opens a card on the mountain");
            cards.Close();
            yield return Frames(10);
            AssertNoErrors("taps");
        }

        [UnityTest, Explicit("#115: the look set, 24 shots a setting")]
        public IEnumerator LookSet([Values(BoxSetting.DesertOasis, BoxSetting.MountainVillage)] string setting)
        {
            yield return OpenBox2(setting, 1080, 1920);
            string tag = setting == BoxSetting.DesertOasis ? "desert" : "mountain";
            yield return ShotSet(tag);
        }

        [UnityTest, Explicit("#115: the river valley in the same set, for comparison")]
        public IEnumerator LookSetRiver()
        {
            yield return OpenSlice(1080, 1920, Seed);
            yield return ShotSet("river");
        }

        IEnumerator ShotSet(string tag)
        {
            var pv = PeopleView.Find();
            foreach (var era in new[] { Era.Camp, Era.Village, Era.Town })
            {
                yield return ToEra(era);
                foreach (float hour in new[] { 12f, 22f })
                {
                    PeopleView.DebugSetHour(hour);
                    yield return Frames(6);
                    DayLightDriver.DebugPauseClock(true);
                    foreach (var (w, h) in new[] { (1080, 1920), (375, 667) })
                    {
                        SetSize(w, h);
                        for (float t = 0f; (Screen.width != w || Screen.height != h) && t < 3f; t += 0.1f) yield return new WaitForSecondsRealtime(0.1f);
                        yield return Frames(12);
                        yield return Shot($"setting_{tag}_{era}_{hour:00}h_{w}");
                    }
                    SetSize(1080, 1920);
                    yield return Frames(6);
                    DayLightDriver.DebugPauseClock(false);
                }
            }
        }
    }
}
