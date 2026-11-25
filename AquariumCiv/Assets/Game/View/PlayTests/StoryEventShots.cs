// #722 (B78.a-c): the story events in the real box. StoryEventShots (Explicit): in a fresh lived-in camp each cheat of SimDebugPanel's
// "Stories (#722)" block (the event happens now and its held moment plays while watched), phone shots (750x1334) at the default camera and
// zoomed onto the scene during it, then one of what it left (the fruit on the tree, the marks on the trunk, the second stone). They land in
// Temp/agent/722/.
using System;
using System.Collections;
using System.Linq;
using AquariumCiv.Game;
using AquariumCiv.GameUI;
using AquariumCiv.Sim;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AquariumCiv.View.PlayTests
{
    [Explicit, Category("StoryEventShots")]
    public class StoryEventShots : PlayTestBase
    {
        static SimCore Sim => GameClock.Instance != null ? GameClock.Instance.Sim : null;

        IEnumerator Zoomed(string name, Vector2 f)
        {
            var rig = Camera.main.GetComponent<BoxCamera>();
            var box = UnityEngine.Object.FindAnyObjectByType<BoxView>();
            Vector3 tgt0 = rig.target; float z0 = rig.zoom, p0 = rig.pitch, m0 = rig.minZoom;
            var w = box.Root.TransformPoint(new Vector3(f.x, box.SurfaceHeight(f.x, f.y) + 0.08f, f.y));
            for (int i = 0; i < 14; i++) { rig.CancelFlight(); rig.minZoom = 0.05f; rig.target = w; rig.zoom = 0.1f; rig.pitch = 38f; rig.Apply(); yield return null; }
            yield return Shot(name);
            for (int i = 0; i < 4; i++) { rig.CancelFlight(); rig.target = tgt0; rig.zoom = z0; rig.pitch = p0; rig.minZoom = m0; rig.Apply(); yield return null; }
        }

        /// <summary>A fresh box: a lived-in camp at <paramref name="hour"/> with <paramref name="kids"/> kids, the Keeper's tree grown, the setup,
        /// then the cheat; a shot at the default camera at each of <paramref name="at"/> seconds, one zoomed at <paramref name="zoomAt"/>, and once
        /// the scene is over a zoomed shot of what it left.</summary>
        IEnumerator Story(string name, string id, CampUse use, float hour, int kids, Action setup, Func<Vector2?> left, float zoomAt, params float[] at)
        {
            UseLivingLand(true);
            GameUiPanel.AcceptInputUnfocused(true);
            yield return OpenSlice(750, 1334, seed: 4242);
            GameUiPanel.AcceptInputUnfocused(true);
            Debug.Log("[722 shots] " + CampCheats.StageCamp(hour, kids));
            yield return new WaitForSeconds(2f);
            Debug.Log("[722 shots] " + CampCheats.GrowTree());
            yield return new WaitForSeconds(2f);
            setup?.Invoke();
            yield return new WaitForSeconds(2f);
            string r = StoryMoment.DebugPlayNow(id);
            Debug.Log($"[722 shots] {name}: {r}");
            var pv = PeopleView.Find();
            for (float w = 0f; w < 5f && !pv.Camp.Playing(use); w += 0.25f) yield return new WaitForSeconds(0.25f); // the stage looks every second
            Assert.IsTrue(pv.Camp.Playing(use), $"{name}: the scene plays ({r}; {StoryMoment.LastWhy})");
            Debug.Log($"[722 shots] {name}: caption \"{StoryMoment.LastCaption}\"");
            var times = at.Concat(new[] { zoomAt }).OrderBy(x => x).ToList();
            float t = 0f;
            foreach (float s in times)
            {
                yield return new WaitForSeconds(Mathf.Max(0f, s - t));
                t = s;
                if (Mathf.Approximately(s, zoomAt) && pv.Camp.FocusNow() is Vector2 f) { yield return Zoomed($"722/{name}_zoom_{s:0.0}s", f); t += 18f / 60f; }
                else yield return Shot($"722/{name}_default_{s:0.0}s");
            }
            for (float w = 0f; w < 60f && pv.Camp.Playing(use); w += 0.5f) yield return new WaitForSeconds(0.5f);
            Assert.IsFalse(pv.Camp.Playing(use), name + ": the scene ends");
            yield return new WaitForSeconds(1f);
            if (left() is Vector2 l) yield return Zoomed($"722/{name}_left", l);
            Debug.Log($"[722 shots] {name}: fruit drawn {pv.Camp.FruitDrawn}, marks {pv.Camp.MarksDrawn}; {pv.Camp.PropMarks()}");
            CloseSlice();
        }

        static Vector2? TreeSpot() { var s = Sim; if (s == null || !s.EcologyOn) return null; var t = s.EcoTree; return new Vector2(t.x, t.z); }

        [UnityTest, Timeout(300000)]
        public IEnumerator B78a_FirstFruit() => Story("b78a-first-fruit", StoryEvent.FirstFruit, CampUse.FirstFruit, 10f, 3, null, TreeSpot, 4.5f, 2f, 7f, 11f);

        [UnityTest, Timeout(300000)]
        public IEnumerator B78b_TwoMarks() => Story("b78b-two-marks", StoryEvent.TwoMarks, CampUse.TwoMarks, 16.5f, 2, null, TreeSpot, 9f, 3f, 8f, 12f);

        [UnityTest, Timeout(300000)]
        public IEnumerator B78c_SecondStone() => Story("b78c-second-stone", StoryEvent.SecondStone, CampUse.SecondStone, 10f, 2, () =>
        {
            Debug.Log("[722 shots] " + CampCheats.OnlyThing(ConsequenceKind.NameStone));
            var sim = Sim;
            var stone = sim?.FirstNameStone;
            if (stone != null) stone.dueMonth = sim.State.month - 1; // a death after the stone stood
            Debug.Log("[722 shots] " + ConsequenceCheats.OneMoreDeath());
        }, () => { var s = Sim?.FindConsequenceProp(Sim.StoryMarks.secondStoneId); return s != null ? new Vector2(s.x, s.z) : (Vector2?)null; }, 8f, 3f, 7f, 12f, 18f);

        /// <summary>The fruit's life use: after the first fruit, a kid picks one from the tree.</summary>
        [UnityTest, Timeout(300000)]
        public IEnumerator FruitPick()
        {
            UseLivingLand(true);
            GameUiPanel.AcceptInputUnfocused(true);
            yield return OpenSlice(750, 1334, seed: 4242);
            GameUiPanel.AcceptInputUnfocused(true);
            Debug.Log("[722 shots] " + CampCheats.StageCamp(11f, 2));
            yield return new WaitForSeconds(2f);
            Debug.Log("[722 shots] " + CampCheats.GrowTree());
            yield return new WaitForSeconds(2f);
            string r = CampCheats.FruitPick();
            Debug.Log("[722 shots] fruit-pick: " + r);
            var pv = PeopleView.Find();
            Assert.IsTrue(pv.Camp.Playing(CampUse.FruitPick), r);
            yield return new WaitForSeconds(1f);
            Assert.Greater(pv.Camp.FruitDrawn, 0, "the fruit hangs on the tree");
            yield return Shot("722/fruit-pick_default_1.0s");
            yield return new WaitForSeconds(2.5f);
            if (pv.Camp.FocusNow() is Vector2 f) yield return Zoomed("722/fruit-pick_zoom_3.5s", f);
            CampLife.CheatFruit = false;
            CloseSlice();
        }
    }
}
