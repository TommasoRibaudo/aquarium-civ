// #692 part 3 (B33-B36): grass, saplings, rocks, fallen branches in the real box.
// - CampLandUses (category CampLand, ~3 min): one lived-in camp; each cheat in turn starts its use, and what the use leaves on the land shows
//   (the boulders, a wet patch, six ring stones, a twig, five pebbles, the horse leaning, grass bits worn).
// - Camp692LandShots (Explicit): phone shots (750x1334) of each use at the default camera AND zoomed onto the use, in a fresh box each;
//   they land in Temp/agent/692p3/.
using System;
using System.Collections;
using System.Linq;
using AquariumCiv.GameUI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AquariumCiv.View.PlayTests
{
    [Category("CampLand")]
    public class CampLandUses : PlayTestBase
    {
        IEnumerator Play(PeopleView pv, CampUse use, Func<string> cheat, Func<bool> left, string what)
        {
            int before = pv.Camp.Started.TryGetValue(use, out int n) ? n : 0;
            string r = cheat();
            Debug.Log($"[692p3 uses] {use}: {r}");
            int after = pv.Camp.Started.TryGetValue(use, out int m) ? m : 0;
            Assert.Greater(after, before, $"{use} started ({r})");
            var s = pv.Camp.Scenes.LastOrDefault(x => x.use == use);
            float len = s != null ? s.Length : 10f;
            float t0 = Time.time;
            while (Time.time - t0 < len + 1f) { yield return null; if (left != null && left()) break; }
            Debug.Log($"[692p3 uses] {use} after {Time.time - t0:0.0} s: {pv.Camp.LandMarks()}");
            if (left != null) Assert.IsTrue(left(), $"{use}: {what} ({pv.Camp.LandMarks()})");
        }

        [UnityTest, Timeout(600000)]
        public IEnumerator EachUseStartsAndLeavesItsMark()
        {
            UseLivingLand(true);
            GameUiPanel.AcceptInputUnfocused(true);
            yield return OpenSlice(750, 1334, seed: 4242);
            Debug.Log("[692p3 uses] " + CampCheats.StageCamp(10f, 3));
            yield return new WaitForSeconds(2f);
            Debug.Log("[692p3 uses] " + CampCheats.LandReady());
            yield return new WaitForSeconds(3f);
            Debug.Log("[692p3 uses] " + CampCheats.LandReady());
            var pv = PeopleView.Find();
            Assert.AreEqual(2, pv.Camp.Rocks.Count, "two boulders at the camp's edge");
            yield return Play(pv, CampUse.SitRock, CampCheats.SitRock, null, "");
            yield return Play(pv, CampUse.StackPebbles, CampCheats.StackPebbles, () => pv.Camp.PebblesNow == LandGeo.Pebbles, "five pebbles stacked");
            yield return Play(pv, CampUse.GrassRoll, CampCheats.GrassRoll, () => pv.Camp.WornNow > 0, "grass bits in the hair");
            yield return Play(pv, CampUse.WaterSapling, CampCheats.WaterSapling, () => pv.Camp.WetNow > 0, "a wet patch under the sapling");
            yield return Play(pv, CampUse.RingSapling, CampCheats.RingSapling, () => pv.Camp.RingStonesNow >= 4, "a ring of stones");
            yield return Play(pv, CampUse.PlantTwig, CampCheats.PlantTwig, () => pv.Camp.TwigsNow > 0, "a twig planted");
            yield return Play(pv, CampUse.StickHorse, CampCheats.StickHorse, () => pv.Camp.HorsesNow > 0, "the horse leaning");
            yield return Play(pv, CampUse.StickFight, CampCheats.StickFight, null, "");
            yield return Play(pv, CampUse.GrassWhistle, CampCheats.GrassWhistle, null, "");
            yield return Play(pv, CampUse.PickFlowers, CampCheats.PickFlowers, null, "");
            yield return Play(pv, CampUse.LieBack, CampCheats.LieBack, null, "");
            CloseSlice();
        }
    }

    [Explicit, Category("Camp692LandShots")]
    public class Camp692LandShots : PlayTestBase
    {
        /// <summary>A fresh box: a lived-in camp at <paramref name="hour"/> with the land ready (the Keeper's tree, a wild tree and saplings), then the
        /// cheat; at each of <paramref name="at"/> seconds a shot at the default camera, and at <paramref name="zoomAt"/> one zoomed onto the use.</summary>
        IEnumerator Use(string name, float hour, Func<string> cheat, float zoomAt, int kids, params float[] at)
        {
            UseLivingLand(true);
            GameUiPanel.AcceptInputUnfocused(true);
            yield return OpenSlice(750, 1334, seed: 4242);
            GameUiPanel.AcceptInputUnfocused(true);
            Debug.Log("[692p3 shots] " + CampCheats.StageCamp(hour, kids));
            yield return new WaitForSeconds(2f);
            Debug.Log("[692p3 shots] " + CampCheats.LandReady());
            yield return new WaitForSeconds(3f);
            Debug.Log("[692p3 shots] " + CampCheats.LandReady());
            yield return new WaitForSeconds(1f);
            string r = cheat();
            Debug.Log($"[692p3 shots] {name}: {r}");
            var pv = PeopleView.Find();
            var rig = Camera.main.GetComponent<BoxCamera>();
            var box = UnityEngine.Object.FindAnyObjectByType<BoxView>();
            var times = at.Concat(new[] { zoomAt }).OrderBy(x => x).ToList();
            float t = 0f;
            foreach (float s in times)
            {
                yield return new WaitForSeconds(Mathf.Max(0f, s - t));
                t = s;
                if (Mathf.Approximately(s, zoomAt) && pv.Camp.FocusNow() is Vector2 f)
                {
                    Vector3 tgt0 = rig.target; float z0 = rig.zoom, p0 = rig.pitch, m0 = rig.minZoom;
                    var w = box.Root.TransformPoint(new Vector3(f.x, box.SurfaceHeight(f.x, f.y) + 0.06f, f.y));
                    for (int i = 0; i < 14; i++) { rig.CancelFlight(); rig.minZoom = 0.05f; rig.target = w; rig.zoom = 0.1f; rig.pitch = 38f; rig.Apply(); yield return null; }
                    yield return Shot($"692p3/{name}_zoom_{s:0.0}s");
                    t += 14f / 60f;
                    for (int i = 0; i < 4; i++) { rig.CancelFlight(); rig.target = tgt0; rig.zoom = z0; rig.pitch = p0; rig.minZoom = m0; rig.Apply(); yield return null; }
                }
                else yield return Shot($"692p3/{name}_default_{s:0.0}s");
            }
            Debug.Log($"[692p3 shots] {name} marks: {pv.Camp.LandMarks()}");
            Assert.Greater(pv.Camp.Started.Count, 0, name + ": a use played (" + r + ")");
            CloseSlice();
        }

        [UnityTest, Timeout(300000)] public IEnumerator B33a() => Use("b33a-roll", 11f, CampCheats.GrassRoll, 4.5f, 3, 4.2f);
        [UnityTest, Timeout(300000)] public IEnumerator B33b() => Use("b33b-lie-back", 12f, CampCheats.LieBack, 9f, 2, 8.8f);
        [UnityTest, Timeout(300000)] public IEnumerator B33c() => Use("b33c-flowers", 11f, CampCheats.PickFlowers, 12f, 2, 11.8f);
        [UnityTest, Timeout(300000)] public IEnumerator B33d() => Use("b33d-whistle", 11f, CampCheats.GrassWhistle, 8.6f, 3, 8.4f);
        [UnityTest, Timeout(300000)] public IEnumerator B34a() => Use("b34a-water", 8f, CampCheats.WaterSapling, 7f, 2, 6.8f);
        [UnityTest, Timeout(300000)] public IEnumerator B34b() => Use("b34b-ring", 11f, CampCheats.RingSapling, 16f, 2, 15.8f);
        [UnityTest, Timeout(300000)] public IEnumerator B34c() => Use("b34c-twig", 11f, CampCheats.PlantTwig, 8f, 2, 7.8f);
        [UnityTest, Timeout(300000)] public IEnumerator B35a() => Use("b35a-rock", 11f, CampCheats.SitRock, 8f, 2, 7.8f);
        [UnityTest, Timeout(300000)] public IEnumerator B35b() => Use("b35b-pebbles", 11f, CampCheats.StackPebbles, 11f, 2, 10.8f);
        [UnityTest, Timeout(300000)] public IEnumerator B36a() => Use("b36a-sticks", 11f, CampCheats.StickFight, 8f, 2, 7.8f);
        [UnityTest, Timeout(300000)] public IEnumerator B36b() => Use("b36b-horse", 11f, CampCheats.StickHorse, 9f, 2, 8.8f);
    }
}
