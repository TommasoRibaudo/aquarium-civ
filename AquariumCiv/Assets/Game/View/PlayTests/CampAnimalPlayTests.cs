// #692 part 4 (B37-B39, B15.d): the animal uses in the real box.
// - CampAnimalUses (category CampAnimals, ~3 min): one lived-in camp with a pond; each cheat in turn starts its use, and its animal does its
//   part (the frog rides in the hands, then hops in; a doe looks up; the herd bolts; the kit is drawn; the ducks come to the crumbs).
// - Camp692AnimalShots (Explicit): phone shots (1080x1920) of each use at the default camera AND zoomed onto it, in a fresh box each; they
//   land in Temp/agent/692p4/.
using System;
using System.Collections;
using System.Linq;
using AquariumCiv.GameUI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AquariumCiv.View.PlayTests
{
    [Category("CampAnimals")]
    public class CampAnimalUses : PlayTestBase
    {
        internal static IEnumerator Stage(float hour, int kids)
        {
            Debug.Log("[692p4] " + CampCheats.StageCamp(hour, kids));
            yield return new WaitForSeconds(1f);
            Debug.Log("[692p4] " + CampCheats.PondReady());
            yield return new WaitForSeconds(3f);
            Debug.Log("[692p4] " + CampCheats.PondReady());
            yield return new WaitForSeconds(3f);
            PeopleView.DebugSetHour(hour);
            yield return new WaitForSeconds(1f);
        }

        /// <summary>Starts <paramref name="cheat"/> (a second press when the first only placed the animals).</summary>
        internal static IEnumerator Start(PeopleView pv, CampUse use, Func<string> cheat, Action<string> said)
        {
            int before = pv.Camp.Started.TryGetValue(use, out int n) ? n : 0;
            string r = cheat();
            for (int i = 0; i < 8 && (pv.Camp.Started.TryGetValue(use, out int m) ? m : 0) == before; i++)
            {
                Debug.Log($"[692p4] {use}: {r} (again)");
                yield return new WaitForSeconds(2f);
                r = cheat();
            }
            Debug.Log($"[692p4] {use}: {r}");
            said(r);
        }

        IEnumerator Play(PeopleView pv, CampUse use, Func<string> cheat, Func<bool> did, string what)
        {
            int before = pv.Camp.Started.TryGetValue(use, out int n) ? n : 0;
            string r = "";
            yield return Start(pv, use, cheat, x => r = x);
            int after = pv.Camp.Started.TryGetValue(use, out int m) ? m : 0;
            Assert.Greater(after, before, $"{use} started ({r})");
            var s = pv.Camp.Scenes.LastOrDefault(x => x.use == use);
            float len = s != null ? s.Length : 10f;
            float t0 = Time.time;
            bool done = false;
            while (Time.time - t0 < len + 1f) { yield return null; done |= did != null && did(); } // the whole scene (its kids are busy until it ends)
            Debug.Log($"[692p4] {use} after {Time.time - t0:0.0} s: {pv.Camp.AnimalMarks()}");
            if (did != null) Assert.IsTrue(done, $"{use}: {what} ({pv.Camp.AnimalMarks()})");
            yield return new WaitForSeconds(1f);
        }

        [UnityTest, Timeout(600000)]
        public IEnumerator EachAnimalUseStartsAndItsAnimalPlaysAlong()
        {
            UseLivingLand(true);
            GameUiPanel.AcceptInputUnfocused(true);
            yield return OpenSlice(750, 1334, seed: 4242);
            yield return Stage(10f, 3);
            var pv = PeopleView.Find();
            var pond = PondLife.Instance;
            Assert.IsNotNull(pond, "the pond's life");
            CampLife.ForceFrogMiss = false;
            int held = pv.Camp.FrogHeldFrames, hops = pond.FrogHops;
            yield return Play(pv, CampUse.FrogCatch, CampCheats.FrogCatch, () => pv.Camp.FrogHeldFrames > held && pond.FrogHops > hops, "the frog rode in the hands, then hopped in");
            hops = pond.FrogHops;
            yield return Play(pv, CampUse.FrogCatch, CampCheats.FrogCatch, () => pond.FrogHops > hops, "the miss: the frog hopped in");
            int crumbs = pv.Camp.CrumbFrames;
            yield return Play(pv, CampUse.FeedDucks, CampCheats.FeedDucks, () => pv.Camp.CrumbFrames > crumbs && pond.LuredDucks > 0, "crumbs flew and a duck came");
            int kit = pv.Camp.KitFrames;
            yield return Play(pv, CampUse.KitHold, CampCheats.KitHold, () => pv.Camp.KitFrames > kit, "the kit was drawn");
            int looks = pv.Camp.DeerLooks;
            yield return Play(pv, CampUse.DeerWatch, CampCheats.DeerWatch, null, "");
            Assert.Greater(pv.Camp.DeerLooks, looks, "a doe looked up at the watchers");
            int bolts = pv.Camp.DeerBolts;
            yield return Play(pv, CampUse.DeerCreep, CampCheats.DeerCreep, () => pv.Camp.DeerBolts > bolts, "the herd bolted");
            CloseSlice();
        }
    }

    [Explicit, Category("Camp692AnimalShots")]
    public class Camp692AnimalShots : PlayTestBase
    {
        IEnumerator Use(string name, float hour, CampUse use, Func<string> cheat, float zoomAt, int kids, params float[] at)
        {
            UseLivingLand(true);
            GameUiPanel.AcceptInputUnfocused(true);
            yield return OpenSlice(1080, 1920, seed: 4242);
            GameUiPanel.AcceptInputUnfocused(true);
            yield return CampAnimalUses.Stage(hour, kids);
            var pv = PeopleView.Find();
            string r = "";
            yield return CampAnimalUses.Start(pv, use, cheat, x => r = x);
            Debug.Log($"[692p4 shots] {name}: {r}");
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
                    for (int i = 0; i < 14; i++) { rig.CancelFlight(); rig.minZoom = 0.05f; rig.target = w; rig.zoom = 0.12f; rig.pitch = 38f; rig.Apply(); yield return null; }
                    yield return Shot($"692p4/{name}_zoom_{s:0.0}s");
                    t += 14f / 60f;
                    for (int i = 0; i < 4; i++) { rig.CancelFlight(); rig.target = tgt0; rig.zoom = z0; rig.pitch = p0; rig.minZoom = m0; rig.Apply(); yield return null; }
                }
                else yield return Shot($"692p4/{name}_default_{s:0.0}s");
            }
            Debug.Log($"[692p4 shots] {name}: {pv.Camp.AnimalMarks()}");
            Assert.Greater(pv.Camp.Started.TryGetValue(use, out int n) ? n : 0, 0, name + ": the use played (" + r + ")");
            CloseSlice();
        }

        [UnityTest, Timeout(300000)] public IEnumerator B37() { CampLife.ForceFrogMiss = false; return Use("b37-frog", 11f, CampUse.FrogCatch, CampCheats.FrogCatch, 7.5f, 2, 7.3f); }
        [UnityTest, Timeout(300000)] public IEnumerator B37Miss() { CampLife.ForceFrogMiss = true; return Use("b37-frog-miss", 11f, CampUse.FrogCatch, CampCheats.FrogCatch, 6.5f, 2, 6.3f); }
        [UnityTest, Timeout(300000)] public IEnumerator B38a() => Use("b38a-deer-watch", 11f, CampUse.DeerWatch, CampCheats.DeerWatch, 9f, 2, 8.8f);
        [UnityTest, Timeout(300000)] public IEnumerator B38b() => Use("b38b-deer-creep", 11f, CampUse.DeerCreep, CampCheats.DeerCreep, 9f, 3, 8.8f, 12f);
        [UnityTest, Timeout(300000)] public IEnumerator B39a() => Use("b39a-kit", 11f, CampUse.KitHold, CampCheats.KitHold, 10f, 2, 9.8f);
        [UnityTest, Timeout(300000)] public IEnumerator B15d() => Use("b15d-feed-ducks", 10f, CampUse.FeedDucks, CampCheats.FeedDucks, 9f, 3, 5f, 8.8f);
    }
}
