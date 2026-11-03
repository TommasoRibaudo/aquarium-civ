using System.Collections;
using System.Collections.Generic;
using System.Linq;
using AquariumCiv.Game;
using AquariumCiv.GameUI;
using AquariumCiv.Sim;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace AquariumCiv.View.PlayTests
{
    /// <summary>
    /// P8 the way the phone plays it (the user's build 15 run, 2026-11-03): a fresh living-land box, then only touches a player
    /// makes. Wipe the glass with strokes across the whole screen, hold to mist, name it by tapping the plate, then the Kit
    /// button: the mister mists many spots, Done puts it down; the seed (once the land has earned it, reached with the debug
    /// cheats after the first run) plants the tree with one tap. No step is skipped. Screenshots of every beat go to
    /// Temp/agent/phone_*.png.
    /// </summary>
    [Category("EcoPhone")]
    public class EcoPhonePlaythrough : PlayTestBase
    {
        static SimCore Sim => GameClock.Instance.ActiveSim;
        static FirstRunView Run => FirstRunView.Instance;

        Vector2 ScreenOf(float x, float z)
        {
            var box = Object.FindAnyObjectByType<BoxView>();
            return Camera.main.WorldToScreenPoint(box.transform.TransformPoint(box.land.OnGround(x, z)));
        }

        /// <summary>A player's wipe: rows across the whole screen, where there's glass under the finger and no UI on it.</summary>
        static List<Vector2[]> PlayerStrokes(CareView care, float spacing)
        {
            var list = new List<Vector2[]>();
            bool right = true;
            for (float y = Screen.height * 0.98f; y > Screen.height * 0.02f; y -= spacing)
            {
                float first = -1f, last = -1f;
                for (float x = 6f; x < Screen.width - 6f; x += 10f)
                {
                    var p = new Vector2(x, y);
                    if (!care.GlassAt(p) || UiInput.Over(p)) continue;
                    if (first < 0f) first = x;
                    last = x;
                }
                if (first < 0f) continue;
                list.Add(right ? new[] { new Vector2(first, y), new Vector2(last, y) } : new[] { new Vector2(last, y), new Vector2(first, y) });
                right = !right;
            }
            return list;
        }

        IEnumerator Hold(Vector2 at, float seconds, System.Func<bool> until = null)
        {
            int id = touchId++;
            BeginTouch(id, at, queueEventOnly: true);
            float t = Time.unscaledTime;
            while (Time.unscaledTime - t < seconds && (until == null || !until())) { MoveTouch(id, at + new Vector2(Mathf.Sin(Time.unscaledTime * 9f), 0f), delta: Vector2.zero, queueEventOnly: true); yield return null; }
            EndTouch(id, at, delta: Vector2.zero, queueEventOnly: true);
            yield return Frames(10);
        }

        [UnityTest, Timeout(600000)]
        public IEnumerator FreshLivingBox_PlayedByTouch_ThenTheKit()
        {
            bool prefWas = GameClock.EcologyPref;
            GameClock.EcologyPref = true;
            try
            {
                yield return OpenSlice(skipFirstRun: false);
                Assert.IsTrue(Sim.EcologyOn, "a barren box");
                var care = CareView.Instance;
                var hud = GameHud.Instance;
                yield return Until(() => Run.Step == FirstRunStep.Wipe, 8f);
                yield return Shot("phone_01_misted_glass");

                // ---- 1. Wipe, as a player does: whole-screen strokes, a few passes
                float spacing = care.wipeRadius * Screen.height * 1.1f;
                for (int pass = 0; pass < 6 && Run.Step == FirstRunStep.Wipe; pass++)
                {
                    foreach (var row in PlayerStrokes(care, spacing))
                    {
                        if (Run.Step != FirstRunStep.Wipe) break;
                        yield return Drag(row, 0.03f * Screen.height);
                    }
                    Debug.Log($"[phone] wipe pass {pass}: grime {Sim.State.grime:0.000} panes {care.DebugPaneGrime()}");
                    yield return Shot("phone_02_wipe_pass" + pass);
                }
                Assert.AreNotEqual(FirstRunStep.Wipe, Run.Step, $"the glass can be wiped clean by touch (grime {Sim.State.grime:0.000}, panes {care.DebugPaneGrime()})");
                yield return Frames(20);
                Assert.AreEqual(FirstRunStep.Rain, Run.Step, "a barren box: wipe, then the first mist");

                // ---- 2. One step at a time: only the mist hint, no plate, no name field
                yield return new WaitForSecondsRealtime(1.5f);
                yield return Shot("phone_03_mist_step");
                Assert.AreEqual("mist-hold", Run.Run.Hint.id);
                Assert.IsFalse(Shown(Run.Root.Q("firstrun-plate")), "the name plate waits for its step");

                var (rx, rz) = Run.Run.SuggestedRainSpot;
                yield return Hold(ScreenOf(rx, rz), 2.5f, () => Run.Step != FirstRunStep.Rain);
                yield return Frames(20);
                Assert.AreEqual(FirstRunStep.Name, Run.Step, "the held finger misted");
                yield return new WaitForSecondsRealtime(4.5f); // the reward line, then the name's hint
                yield return Shot("phone_04_name_step");
                Assert.IsTrue(Shown(Run.Root.Q("firstrun-plate")), "the plate");
                Assert.AreEqual("name", Run.Run.Hint.id);
                Assert.IsTrue(Run.HintShowing, "the Name it hint is on screen, pointing at the plate");
                Assert.AreNotEqual("mist-hold", Run.Run.Hint.id, "never the mist and the name at once");

                yield return TapAt(GameUiPanel.ScreenCenter(Run.Root, "firstrun-plate"));
                yield return new WaitForSecondsRealtime(0.4f);
                Assert.IsTrue(Run.NameSheetOpen, "a tap on the plate opens the naming sheet");
                yield return Shot("phone_05_name_sheet");
                yield return TapAt(GameUiPanel.ScreenCenter(Run.Root, "firstrun-name-keep"));
                yield return Frames(30);
                Assert.IsFalse(Run.Active, "named: the run is done");
                yield return new WaitForSecondsRealtime(2f); // the tools arrive
                yield return Shot("phone_06_after_first_run");
                AssertNoErrors("first run");

                // ---- 3. The kit: Kit → Mister, mist several places, Done
                Assert.IsTrue(hud.KitBox, "a living-land box shows the kit");
                Assert.IsFalse(Shown(hud.Root.Q("hud-shape")), "no Shape tray on a living land");
                var kitBtn = hud.Root.Q<Button>("hud-clean");
                Assert.AreEqual("Kit", kitBtn.text);
                yield return TapAt(GameUiPanel.ScreenCenter(hud.Root, "hud-clean"));
                yield return new WaitForSecondsRealtime(0.5f);
                Assert.AreEqual(GameHud.Kit, hud.OpenSheet, "Kit opens the kit");
                yield return Shot("phone_07_kit");
                yield return TapAt(GameUiPanel.ScreenCenter(hud.Root, "kit-mister"));
                yield return new WaitForSecondsRealtime(0.5f);
                Assert.AreEqual(GameHud.KitTool.Mister, hud.Hand, "the mister in hand");
                Assert.AreEqual("Done", kitBtn.text);
                float bare0 = Sim.EcoBareShare;
                var spots = new[] { (-1.2f, 0.4f), (0.6f, 0.9f), (-0.2f, -1.2f), (1.4f, 0.2f) };
                foreach (var (x, z) in spots) yield return Hold(ScreenOf(x, z), 1.2f);
                // and a drag mists along its path
                {
                    var a = ScreenOf(-2f, -0.6f); var b = ScreenOf(-0.4f, 0.6f);
                    yield return Drag(new[] { a, b }, 0.01f * Screen.height);
                }
                yield return new WaitForSecondsRealtime(1.5f);
                yield return Shot("phone_08_misted_many");
                var mist = EcoMistView.Instance;
                Debug.Log($"[phone] bare {bare0:0.000} -> {Sim.EcoBareShare:0.000}, pulses {mist.MistPulses}");
                Assert.That(Sim.EcoBareShare, Is.LessThan(bare0 - 0.01f), "the mister greens again and again, not once");
                yield return TapAt(GameUiPanel.ScreenCenter(hud.Root, "hud-clean"));
                yield return Frames(10);
                Assert.AreEqual(GameHud.KitTool.None, hud.Hand, "Done puts it down");
                Assert.AreEqual("Kit", kitBtn.text);

                // ---- 4. The cloth from the kit
                yield return TapAt(GameUiPanel.ScreenCenter(hud.Root, "hud-clean"));
                yield return new WaitForSecondsRealtime(0.5f);
                yield return TapAt(GameUiPanel.ScreenCenter(hud.Root, "kit-cloth"));
                yield return Frames(10);
                Assert.IsTrue(hud.CleanMode, "the cloth is clean mode");
                yield return TapAt(GameUiPanel.ScreenCenter(hud.Root, "hud-clean"));
                yield return Frames(10);
                Assert.IsFalse(hud.CleanMode);

                // ---- 5. Years later (cheats only to get there): the seed comes, one tap plants it
                EcoDebug.CampGate();
                for (int i = 0; i < 40 && !Sim.CanPlantTree; i++) { EcoDebug.MistCamp(); EcoDebug.SkipSeasons(2); }
                Assert.IsTrue(Sim.CanPlantTree, "the seed comes after the camp and the first bushes");
                yield return new WaitForSecondsRealtime(1.5f);
                yield return Shot("phone_09_seed_came");
                yield return TapAt(GameUiPanel.ScreenCenter(hud.Root, "hud-clean"));
                yield return new WaitForSecondsRealtime(0.5f);
                Assert.IsTrue(Shown(hud.Root.Q("kit-seed")), "the seed is in the kit");
                yield return Shot("phone_10_kit_with_seed");
                yield return TapAt(GameUiPanel.ScreenCenter(hud.Root, "kit-seed"));
                yield return Frames(10);
                Assert.AreEqual(GameHud.KitTool.Seed, hud.Hand);
                var map = Sim.Settlement.Map;
                yield return TapAt(ScreenOf(map.greenX - 0.9f, map.greenZ + 0.5f));
                yield return Frames(20);
                Debug.Log("[phone] seed: " + EcoMistView.Instance.LastSeed);
                Assert.GreaterOrEqual(Sim.EcoTree.cell, 0, "one tap planted the tree: " + EcoMistView.Instance.LastSeed);
                Assert.AreEqual(GameHud.KitTool.None, hud.Hand, "the seed leaves the hand");
                yield return new WaitForSecondsRealtime(1.5f);
                yield return Shot("phone_11_planted");
                AssertNoErrors("kit");
            }
            finally { GameClock.EcologyPref = prefWas; }
        }

        static bool Shown(VisualElement e)
        {
            if (e == null) return false;
            for (; e != null; e = e.parent) if (e.resolvedStyle.display == DisplayStyle.None) return false;
            return true;
        }
    }
}
