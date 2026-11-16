// #692 part 5 (B40-B53): the rest of the camp in the real box.
// - CampCampUses (category CampCamp, ~6 min): one lived-in camp (the land ready, fish and hides on the racks, puddles); each cheat in turn starts
//   its use, and what the use leaves shows (a toy on a tent, lanterns lit, meat strips, a purple mouth, a crown, a gift, a dam, a leaf boat,
//   the swing hung).
// - Camp692CampShots (Explicit): phone shots (1080x1920) of each use at the default camera AND zoomed onto the use, in a fresh box each;
//   they land in Temp/agent/692p5/.
using System;
using System.Collections;
using System.Linq;
using AquariumCiv.GameUI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AquariumCiv.View.PlayTests
{
    [Category("CampCamp")]
    public class CampCampUses : PlayTestBase
    {
        readonly System.Collections.Generic.List<string> failed = new();

        /// <summary>Starts the use (a second press when the first set its thing up), plays the whole scene, and records what did not happen
        /// (the test fails at the end with every miss listed, so one run shows them all).</summary>
        IEnumerator Play(PeopleView pv, CampUse use, Func<string> cheat, Func<bool> left, string what, bool retry = true)
        {
            int before = pv.Camp.Started.TryGetValue(use, out int n) ? n : 0;
            string r = cheat();
            Debug.Log($"[692p5 uses] {use}: {r}");
            int after = pv.Camp.Started.TryGetValue(use, out int m) ? m : 0;
            if (after == before && retry)
            {   // the cheat set its thing up first (the rack, the hides, puddles): once more
                yield return new WaitForSeconds(2f);
                r = cheat();
                Debug.Log($"[692p5 uses] {use} (again): {r}");
                after = pv.Camp.Started.TryGetValue(use, out m) ? m : 0;
            }
            if (after <= before) { failed.Add($"{use} did not start ({r})"); yield break; }
            var s = pv.Camp.Scenes.LastOrDefault(x => x.use == use);
            float len = s != null ? s.Length : 10f;
            float t0 = Time.time; bool done = false;
            while (Time.time - t0 < len + 1f) { yield return null; done |= left != null && left(); }
            Debug.Log($"[692p5 uses] {use} after {Time.time - t0:0.0} s: {pv.Camp.CampMarks()}");
            if (left != null && !done) failed.Add($"{use}: no {what} ({pv.Camp.CampMarks()})");
        }

        [UnityTest, Timeout(900000)]
        public IEnumerator EachUseStartsAndLeavesItsMark()
        {
            UseLivingLand(true);
            GameUiPanel.AcceptInputUnfocused(true);
            yield return OpenSlice(750, 1334, seed: 4242);
            Debug.Log("[692p5 uses] " + CampCheats.StageCamp(10f, 3));
            yield return new WaitForSeconds(2f);
            Debug.Log("[692p5 uses] " + CampCheats.LandReady());
            yield return new WaitForSeconds(3f);
            Debug.Log("[692p5 uses] " + CampCheats.LandReady());
            Debug.Log("[692p5 uses] " + FishRackView.DebugRack());
            Debug.Log("[692p5 uses] " + EcoDebug.ShowRack(3));
            yield return new WaitForSeconds(2f);
            var pv = PeopleView.Find();
            var camp = pv.Camp;
            yield return Play(pv, CampUse.TentToy, CampCheats.TentToy, () => camp.TentThingsNow > 0, "a toy on a tent's pole");
            yield return Play(pv, CampUse.TentPeek, CampCheats.TentPeek, null, "");
            yield return Play(pv, CampUse.LightLanterns, CampCheats.LightLanterns, () => camp.LitNow > 0, "a lantern lit");
            yield return Play(pv, CampUse.ChaseMoths, CampCheats.ChaseMoths, null, "");
            yield return Play(pv, CampUse.TentDoorSit, CampCheats.TentDoorSit, null, "");
            yield return Play(pv, CampUse.CountStash, CampCheats.CountStash, null, "");
            yield return Play(pv, CampUse.SnackSneak, CampCheats.SnackSneak, null, "");
            yield return Play(pv, CampUse.BerryWipe, CampCheats.BerryKid, null, "");
            Debug.Log("[692p5 uses] " + CampCheats.HangMeat());
            if (camp.StripsNow <= 0) failed.Add("no meat strips on the rack");
            yield return Play(pv, CampUse.TendRacks, CampCheats.TendRacks, null, "", true);
            yield return Play(pv, CampUse.CountFish, CampCheats.CountFish, null, "", true);
            yield return Play(pv, CampUse.SneakFish, CampCheats.SneakFish, null, "", true);
            yield return Play(pv, CampUse.DuckUnderHides, CampCheats.DuckUnderHides, null, "", true);
            yield return Play(pv, CampUse.ClimbTree, CampCheats.ClimbTree, null, "");
            yield return Play(pv, CampUse.TreeNap, CampCheats.TreeNap, null, "");
            yield return Play(pv, CampUse.TrunkLean, CampCheats.TrunkLean, null, "");
            yield return Play(pv, CampUse.HangSwing, CampCheats.HangSwing, () => camp.SwingHung, "the swing hung");
            yield return Play(pv, CampUse.Swing, CampCheats.Swing, null, "");
            yield return Play(pv, CampUse.Crown, CampCheats.Crown, () => camp.Worn5Now > 0, "a crown worn");
            yield return Play(pv, CampUse.PullWeed, CampCheats.PullWeed, null, "");
            yield return Play(pv, CampUse.StoneGift, CampCheats.StoneGift, () => camp.GiftsNow > 0, "a gift at the stone");
            yield return Play(pv, CampUse.PathChat, CampCheats.PathChat, null, "");
            yield return Play(pv, CampUse.PathRace, CampCheats.PathRace, null, "");
            yield return Play(pv, CampUse.HillDusk, CampCheats.HillDusk, null, "");
            yield return Play(pv, CampUse.HillRoll, CampCheats.HillRoll, null, "");
            yield return Play(pv, CampUse.FollowStream, CampCheats.FollowStream, null, "");
            yield return Play(pv, CampUse.BuildDam, CampCheats.BuildDam, () => camp.DamStonesNow >= 5, "five dam stones");
            Debug.Log("[692p5 uses] " + EcoDebug.PuddlesNow());
            yield return new WaitForSeconds(2f);
            yield return Play(pv, CampUse.PuddleLook, CampCheats.PuddleLook, null, "", true);
            yield return Play(pv, CampUse.PuddleRace, CampCheats.PuddleRace, null, "", true);
            Debug.Log("[692p5 uses] " + CampCheats.PondReady());
            yield return new WaitForSeconds(4f);
            yield return Play(pv, CampUse.SkipStones, CampCheats.SkipStones, null, "");
            yield return Play(pv, CampUse.LeafBoat, CampCheats.LeafBoat, () => camp.BoatsNow > 0, "a leaf boat on the water");
            yield return Play(pv, CampUse.FeetInWater, CampCheats.FeetInWater, null, "");
            yield return Play(pv, CampUse.CutReeds, CampCheats.CutReeds, null, "");
            yield return Play(pv, CampUse.ReedWhistle, CampCheats.ReedWhistle, null, "");
            // last: placing the thicket and the rabbits starts their arrival, which holds everyone for a while
            Debug.Log("[692p5 uses] " + CampCheats.ThicketReady());
            yield return new WaitForSeconds(3f);
            yield return Play(pv, CampUse.ThicketPeek, CampCheats.ThicketPeek, null, "");
            Debug.Log("[692p5 uses] misses: " + string.Join(" | ", failed));
            CloseSlice();
            Assert.IsEmpty(failed, string.Join(" | ", failed));
        }
    }

    [Explicit, Category("Camp692CampShots")]
    public class Camp692CampShots : PlayTestBase
    {
        /// <summary>A fresh box: a lived-in camp at <paramref name="hour"/> with the land ready (a wild tree, saplings), fish and hides on the racks,
        /// <paramref name="prep"/> (or null), then the cheat; at each of <paramref name="at"/> seconds a shot at the default camera, and at
        /// <paramref name="zoomAt"/> one zoomed onto the use.</summary>
        IEnumerator Use(string name, float hour, Func<string> cheat, float zoomAt, int kids, Func<string> prep, params float[] at)
        {
            UseLivingLand(true);
            GameUiPanel.AcceptInputUnfocused(true);
            yield return OpenSlice(1080, 1920, seed: 4242);
            GameUiPanel.AcceptInputUnfocused(true);
            Debug.Log("[692p5 shots] " + CampCheats.StageCamp(hour, kids));
            yield return new WaitForSeconds(2f);
            Debug.Log("[692p5 shots] " + CampCheats.LandReady());
            yield return new WaitForSeconds(3f);
            Debug.Log("[692p5 shots] " + CampCheats.LandReady());
            if (prep != null) { Debug.Log("[692p5 shots] prep: " + prep()); yield return new WaitForSeconds(2f); }
            yield return new WaitForSeconds(1f);
            string r = cheat();
            Debug.Log($"[692p5 shots] {name}: {r}");
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
                    yield return Shot($"692p5/{name}_zoom_{s:0.0}s");
                    t += 14f / 60f;
                    for (int i = 0; i < 4; i++) { rig.CancelFlight(); rig.target = tgt0; rig.zoom = z0; rig.pitch = p0; rig.minZoom = m0; rig.Apply(); yield return null; }
                }
                else yield return Shot($"692p5/{name}_default_{s:0.0}s");
            }
            Debug.Log($"[692p5 shots] {name} marks: {pv.Camp.CampMarks()}");
            Assert.Greater(pv.Camp.Started.Count, 0, name + ": a use played (" + r + ")");
            CloseSlice();
        }

        static string Rack() => FishRackView.DebugRack() + "; " + EcoDebug.ShowRack(3);
        static string Dusk() { var s = CampCheats.LightLanterns(); DayLightDriver.DebugPauseClock(true); return s; }

        [UnityTest, Timeout(300000)] public IEnumerator B40a() => Use("b40a-tent-peek", 7f, CampCheats.TentPeek, 2.5f, 2, null, 2.2f);
        [UnityTest, Timeout(300000)] public IEnumerator B40b() => Use("b40b-tent-toy", 11f, CampCheats.TentToy, 6f, 2, null, 5.8f);
        [UnityTest, Timeout(300000)] public IEnumerator B40c() => Use("b40c-door-sit", 18f, CampCheats.TentDoorSit, 8f, 2, () => { DayLightDriver.DebugPauseClock(true); return "paused"; }, 7.8f);
        [UnityTest, Timeout(300000)] public IEnumerator B41a() => Use("b41a-count-stash", 15f, CampCheats.CountStash, 5f, 2, null, 4.8f);
        [UnityTest, Timeout(300000)] public IEnumerator B41b() => Use("b41b-snack-sneak", 11f, CampCheats.SnackSneak, 6f, 2, null, 5.8f);
        [UnityTest, Timeout(300000)] public IEnumerator B42a() => Use("b42a-tend-racks", 10f, CampCheats.TendRacks, 5f, 2, () => Rack() + "; " + CampCheats.HangMeat(), 4.8f);
        [UnityTest, Timeout(300000)] public IEnumerator B42b() => Use("b42b-duck-hides", 11f, CampCheats.DuckUnderHides, 3f, 2, Rack, 2.8f);
        [UnityTest, Timeout(300000)] public IEnumerator B42c() => Use("b42c-count-fish", 11f, CampCheats.CountFish, 5f, 2, Rack, 4.8f);
        [UnityTest, Timeout(300000)] public IEnumerator B42d() => Use("b42d-sneak-fish", 11f, CampCheats.SneakFish, 4f, 2, Rack, 3.8f);
        [UnityTest, Timeout(300000)] public IEnumerator B43b() => Use("b43b-thicket-peek", 11f, CampCheats.ThicketPeek, 6f, 2, null, 5.8f);
        [UnityTest, Timeout(300000)] public IEnumerator B43c() => Use("b43c-berry-wipe", 15f, CampCheats.BerryKid, 6f, 2, null, 5.8f);
        [UnityTest, Timeout(300000)] public IEnumerator B44a() => Use("b44a-climb", 11f, CampCheats.ClimbTree, 6f, 2, null, 5.8f);
        [UnityTest, Timeout(300000)] public IEnumerator B44b() => Use("b44b-nap", 12f, CampCheats.TreeNap, 8f, 2, null, 7.8f);
        [UnityTest, Timeout(300000)] public IEnumerator B44c() => Use("b44c-trunk-lean", 11f, CampCheats.TrunkLean, 5f, 2, null, 4.8f);
        [UnityTest, Timeout(300000)] public IEnumerator B45a() => Use("b45a-skip-stones", 11f, CampCheats.SkipStones, 4.6f, 2, null, 4.4f);
        [UnityTest, Timeout(300000)] public IEnumerator B45b() => Use("b45b-leaf-boat", 12f, CampCheats.LeafBoat, 8f, 2, null, 7.8f);
        [UnityTest, Timeout(300000)] public IEnumerator B45c() => Use("b45c-feet-water", 14f, CampCheats.FeetInWater, 7f, 2, null, 6.8f);
        [UnityTest, Timeout(300000)] public IEnumerator B46a() => Use("b46a-puddle-look", 11f, CampCheats.PuddleLook, 4f, 2, EcoDebug.PuddlesNow, 3.8f);
        [UnityTest, Timeout(300000)] public IEnumerator B46b() => Use("b46b-puddle-race", 11f, CampCheats.PuddleRace, 5f, 3, EcoDebug.PuddlesNow, 4.8f);
        [UnityTest, Timeout(300000)] public IEnumerator B47a() => Use("b47a-cut-reeds", 9f, CampCheats.CutReeds, 6f, 2, null, 5.8f);
        [UnityTest, Timeout(300000)] public IEnumerator B47b() => Use("b47b-reed-whistle", 11f, CampCheats.ReedWhistle, 8f, 3, null, 7.8f);
        [UnityTest, Timeout(300000)] public IEnumerator B48a() => Use("b48a-follow-stream", 11f, CampCheats.FollowStream, 6f, 3, null, 5.8f);
        [UnityTest, Timeout(300000)] public IEnumerator B48b() => Use("b48b-dam", 11f, CampCheats.BuildDam, 12f, 2, null, 11.8f);
        [UnityTest, Timeout(300000)] public IEnumerator B49a() => Use("b49a-hill-dusk", 17.3f, CampCheats.HillDusk, 10f, 2, () => { DayLightDriver.DebugPauseClock(true); return "paused"; }, 9.8f);
        [UnityTest, Timeout(300000)] public IEnumerator B49b() => Use("b49b-hill-roll", 11f, CampCheats.HillRoll, 4f, 3, null, 3.8f);
        [UnityTest, Timeout(300000)] public IEnumerator B50a() => Use("b50a-path-race", 11f, CampCheats.PathRace, 4f, 3, null, 3.8f);
        [UnityTest, Timeout(300000)] public IEnumerator B50b() => Use("b50b-path-chat", 11f, CampCheats.PathChat, 5f, 2, null, 4.8f);
        [UnityTest, Timeout(300000)] public IEnumerator B51() => Use("b51-stone-gift", 8f, CampCheats.StoneGift, 5f, 2, null, 4.8f);
        [UnityTest, Timeout(300000)] public IEnumerator B52a() => Use("b52a-lanterns", 18f, CampCheats.LightLanterns, 6f, 2, () => { DayLightDriver.DebugPauseClock(true); return "paused"; }, 5.8f);
        [UnityTest, Timeout(300000)] public IEnumerator B52b() => Use("b52b-moths", 19.4f, CampCheats.ChaseMoths, 3f, 2, Dusk, 2.8f);
        [UnityTest, Timeout(300000)] public IEnumerator B53a() => Use("b53a-pull-weed", 10f, CampCheats.PullWeed, 3.5f, 2, null, 3.3f);
        [UnityTest, Timeout(300000)] public IEnumerator B53c() => Use("b53c-crown", 11f, CampCheats.Crown, 4.5f, 2, null, 4.3f);
        [UnityTest, Timeout(300000)] public IEnumerator B53dHang() => Use("b53d-hang-swing", 14f, CampCheats.HangSwing, 7f, 2, null, 6.8f);
        [UnityTest, Timeout(300000)] public IEnumerator B53dSwing() => Use("b53d-swing", 14f, CampCheats.Swing, 5f, 2, null, 4.8f);
    }
}
