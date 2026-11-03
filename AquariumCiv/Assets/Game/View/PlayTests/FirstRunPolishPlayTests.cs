using System.Collections;
using System.Collections.Generic;
using System.Linq;
using AquariumCiv.Game;
using AquariumCiv.GameUI;
using AquariumCiv.View.ShowMe;
using AquariumCiv.Sim;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace AquariumCiv.View.PlayTests
{
    /// <summary>
    /// P8 polish item 1 (#268, #278, #272): the first run's wipe can finish on every pane by touch at the portrait camera, the first
    /// run plays in daylight whatever the hour and blends softly to the clock's light afterwards, and a flick in a sheet glides (the
    /// Offices list scrolls). Both first runs: the living land (ecology on) and the old slice. Screenshots go to Temp/agent/fr_*.png.
    /// </summary>
    [Category("FirstRunPolish")]
    public class FirstRunPolishPlayTests : PlayTestBase
    {
        static SimCore Sim => GameClock.Instance.ActiveSim;
        static FirstRunView Run => FirstRunView.Instance;

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

        Vector2 ScreenOf(float x, float z)
        {
            var box = Object.FindAnyObjectByType<BoxView>();
            return Camera.main.WorldToScreenPoint(box.transform.TransformPoint(box.land.OnGround(x, z)));
        }

        IEnumerator Hold(Vector2 at, float seconds, System.Func<bool> until)
        {
            int id = touchId++;
            BeginTouch(id, at, queueEventOnly: true);
            float t = Time.unscaledTime;
            while (Time.unscaledTime - t < seconds && !until()) { MoveTouch(id, at + new Vector2(Mathf.Sin(Time.unscaledTime * 9f), 0f), delta: Vector2.zero, queueEventOnly: true); yield return null; }
            EndTouch(id, at, delta: Vector2.zero, queueEventOnly: true);
            yield return Frames(10);
        }

        static string Shares(float[] s) => string.Join(" ", s.Select(v => v.ToString("0.000")));

        // ------------------------------------------------------------------ #268
        [UnityTest, Timeout(300000)]
        public IEnumerator TheWipeStep_EveryFoggedPaneCanBeWipedByTouch([Values(true, false)] bool eco, [Values(1179, 1080)] int w)
            => WipeByTouch(eco, w, w == 1179 ? 2556 : 1920, float.NaN);

        /// <summary>The playtest's screenshots (#260, #261) look from the front right: the camera turned to either end of its swing
        /// mid-step, then a player's wipe.</summary>
        [UnityTest, Timeout(300000)]
        public IEnumerator TheWipeStep_FinishesWithTheCameraTurned([Values(-30f, 55f)] float yaw) => WipeByTouch(true, 1179, 2556, yaw);

        IEnumerator WipeByTouch(bool eco, int w, int h, float yaw)
        {
            bool prefWas = GameClock.EcologyPref;
            GameClock.EcologyPref = eco;
            try
            {
                yield return OpenSlice(w, h, skipFirstRun: false);
                Assert.AreEqual(eco, Sim.EcologyOn);
                var care = CareView.Instance;
                yield return Until(() => Run.Step == FirstRunStep.Wipe, 8f);
                Assert.AreEqual(FirstRunStep.Wipe, Run.Step);
                if (!float.IsNaN(yaw))
                {
                    var rig = Camera.main.GetComponent<BoxCamera>();
                    yield return Until(() => Run.GlassMadeReachable, 4f);
                    rig.yaw = yaw;
                    rig.Apply();
                    yield return new WaitForSecondsRealtime(1f); // it rests: what's out of reach from here goes too
                }
                yield return Until(() => Run.GlassMadeReachable, 4f); // the camera rests, the glass out of reach goes
                Assert.IsTrue(Run.GlassMadeReachable, "the wipe step checked what a finger can reach");
                var unreachable = care.UnreachableShare();
                string tag = $"{(eco ? "eco" : "old")}_{w}x{h}" + (float.IsNaN(yaw) ? "" : $"_yaw{yaw:0}");
                Debug.Log($"[fr-wipe {tag}] start: grime {Sim.State.grime:0.000} panes {care.DebugPaneGrime()} unreachable {Shares(unreachable)}");
                Assert.Greater(Sim.State.grime, FirstRun.WipedGrime, "there's fog to wipe");
                for (int i = 0; i < unreachable.Length; i++)
                    Assert.Less(unreachable[i], 0.005f, $"pane {i}: every fogged bit can be reached by a finger ({Shares(unreachable)})");
                yield return Shot("fr_wipe_" + tag);

                // A player's wipe, row by row, until the step is done.
                float spacing = care.wipeRadius * Screen.height * 1.1f;
                float startedAt = (float)Sim.State.firstRun.stepSeconds;
                string panesAtEnd = "";
                for (int pass = 0; pass < 6 && Run.Step == FirstRunStep.Wipe; pass++)
                {
                    foreach (var row in PlayerStrokes(care, spacing))
                    {
                        if (Run.Step != FirstRunStep.Wipe) break;
                        panesAtEnd = care.DebugPaneGrime();
                        yield return Drag(row, 0.03f * Screen.height);
                    }
                    Debug.Log($"[fr-wipe {tag}] pass {pass}: grime {Sim.State.grime:0.000} panes {care.DebugPaneGrime()} step {Run.Step}");
                }
                Assert.AreNotEqual(FirstRunStep.Wipe, Run.Step, $"the wipe finishes by touch (grime {Sim.State.grime:0.000}, panes {care.DebugPaneGrime()})");
                Assert.LessOrEqual(Sim.State.grime, FirstRun.WipedGrime + 0.005f, "it finished because the glass is clear, not on the slow fallback");
                foreach (var m in care.Masks)
                    Assert.Less(m.Mean, 0.1f, $"no pane is left fogged: a slanted pane wipes as well as one facing the camera ({care.DebugPaneGrime()}; before the last stroke {panesAtEnd})");
                Assert.AreEqual(eco ? FirstRunStep.Rain : FirstRunStep.Patch, Run.Step, "the next step");
                yield return Shot("fr_wiped_" + tag);
                AssertNoErrors("wipe " + tag);
            }
            finally { GameClock.EcologyPref = prefWas; }
        }

        // ------------------------------------------------------------------ #278
        [UnityTest, Timeout(240000)]
        public IEnumerator TheFirstRunIsHeldInDaylight_ThenBlendsToTheClock([Values(true, false)] bool eco)
        {
            bool prefWas = GameClock.EcologyPref;
            GameClock.EcologyPref = eco;
            try
            {
                yield return OpenSlice(1179, 2556, skipFirstRun: false);
                var light = DayLightDriver.Instance;
                Assert.IsNotNull(light);
                Assume.That(light.Drives, "the day's light drives the box");
                PeopleView.DebugSetHour(21.5f); // dusk going on night: the case in #261
                yield return Until(() => Run.Step == FirstRunStep.Wipe, 8f);
                yield return new WaitForSecondsRealtime(1f);
                string tag = eco ? "eco" : "old";
                Assert.IsTrue(light.HeldForFirstRun, "held for the first run");
                Assert.AreEqual(DayLightDriver.FirstRunHour, light.Logic.ShownHour, 0.05f, "the wipe is in daylight at 21:30");
                Assert.Greater(light.Current.Daylight, 0.9f, "full day");
                yield return Shot("fr_daylight_wipe_" + tag);

                if (eco)
                {
                    // the mist by touch, as the phone plays it: a held finger on the spot
                    FirstRunView.DebugSkipTo(FirstRunStep.Rain);
                    yield return new WaitForSecondsRealtime(1.5f);
                    Assert.IsTrue(light.HeldForFirstRun);
                    Assert.AreEqual(DayLightDriver.FirstRunHour, light.Logic.ShownHour, 0.05f, "the mist is in daylight");
                    yield return Shot("fr_daylight_mist_" + tag);
                    var (rx, rz) = Run.Run.SuggestedRainSpot;
                    yield return Hold(ScreenOf(rx, rz), 2.5f, () => Run.Step != FirstRunStep.Rain);
                    yield return Frames(20);
                }
                else FirstRunView.DebugSkipTo(FirstRunStep.Name);
                PeopleView.DebugSetHour(23f);
                yield return new WaitForSecondsRealtime(4.5f); // the reward line, then the name's hint
                Assert.AreEqual(FirstRunStep.Name, Run.Step);
                Assert.AreEqual(DayLightDriver.FirstRunHour, light.Logic.ShownHour, 0.05f, "the naming is in daylight too");
                yield return Shot("fr_daylight_name_" + tag);

                // Name it by touch.
                yield return TapAt(GameUiPanel.ScreenCenter(Run.Root, "firstrun-plate"));
                yield return new WaitForSecondsRealtime(0.4f);
                Assert.IsTrue(Run.NameSheetOpen, "a tap on the plate opens the naming sheet");
                yield return TapAt(GameUiPanel.ScreenCenter(Run.Root, "firstrun-name-keep"));
                yield return Frames(10);
                if (eco) Assert.IsFalse(Run.Active, "named: the living land's run is done");
                else
                {
                    Assert.AreNotEqual(FirstRunStep.Name, Run.Step, "named");
                    Assert.IsTrue(light.HeldForFirstRun, "the old slice's run still holds the day until it ends");
                    FirstRunView.DebugSkipAll();
                    yield return Frames(3);
                }
                Assert.IsFalse(Run.Active);

                // The light lets go and chases the clock at its usual rate: a soft blend, no jump.
                float shown0 = light.Logic.ShownHour;
                Assert.AreEqual(DayLightDriver.FirstRunHour, shown0, 0.2f, "no jump on the frame it lets go");
                Assert.IsFalse(light.HeldForFirstRun);
                float t0 = Time.unscaledTime;
                yield return new WaitForSecondsRealtime(1f);
                float moved = Mathf.Abs(Mathf.DeltaAngle(shown0 * 15f, light.Logic.ShownHour * 15f) / 15f), dt = Time.unscaledTime - t0;
                Debug.Log($"[fr-light {tag}] after {dt:0.00}s: shown {light.Logic.ShownHour:0.00} (view {PeopleView.Find().ViewHour:0.00}) moved {moved:0.00} h");
                Assert.Greater(moved, 0.2f, "it's on its way to the clock's hour");
                Assert.LessOrEqual(moved, AquariumCiv.Lighting.DayLight.MaxHourRate * (dt + 0.1f) + 0.05f, "never faster than the usual chase");
                yield return Shot("fr_daylight_blend_" + tag);
                yield return Until(() => Mathf.Abs(Mathf.DeltaAngle(light.Logic.ShownHour * 15f, PeopleView.Find().ViewHour * 15f)) < 0.3f * 15f, 15f);
                Assert.AreEqual(0f, Mathf.DeltaAngle(light.Logic.ShownHour * 15f, PeopleView.Find().ViewHour * 15f) / 15f, 0.3f, "then the real light");
                yield return Shot("fr_daylight_after_" + tag);
                AssertNoErrors("daylight " + tag);
            }
            finally { GameClock.EcologyPref = prefWas; }
        }

        // ------------------------------------------------------------------ #272
        [UnityTest, Timeout(120000)]
        public IEnumerator AFlickInASheetGlidesWithMomentum()
        {
            yield return OpenSlice(1080, 1920);
            var hud = GameHud.Instance;
            yield return TapAt(GameUiPanel.ScreenCenter(hud.Root, "hud-settings"));
            yield return new WaitForSecondsRealtime(0.6f);
            Assert.AreEqual(GameHud.Settings, hud.OpenSheet);
            var scroll = hud.SettingsView.Scroll;
            var view = scroll.View;
            Assume.That(view.contentContainer.layout.height, Is.GreaterThan(view.layout.height + 40f), "settings has more than fits");
            Assert.Greater(view.scrollDecelerationRate, 0f, "the view keeps its momentum");
            var at = GameUiPanel.ScreenCenter(view) - new Vector2(Screen.width * 0.3f, Screen.height * 0.05f);
            // a quick flick: a short, fast stroke, then the finger lifts
            yield return Drag(new[] { at, at + new Vector2(0f, Screen.height * 0.12f) }, Screen.height * 0.03f);
            float atRelease = view.scrollOffset.y;
            yield return new WaitForSecondsRealtime(0.3f);
            float later = view.scrollOffset.y;
            Debug.Log($"[fr-scroll] release {atRelease:0.0} -> {later:0.0} (end {view.contentContainer.layout.height - view.layout.height:0.0})");
            Assert.Greater(atRelease, 1f, "the finger moved it");
            Assert.Greater(later, atRelease + 4f, "it glides on after the finger lifts");
            AssertNoErrors("flick");
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator TheOfficesListScrolls()
        {
            yield return OpenSlice(1080, 1920, 1234);
            ShowMeDirector.BubblesOn = false;
            StringAssert.Contains("the town waits", MomentPlayer.DebugGrowVillage(30f));
            yield return Frames(3);
            Sim.ForceNextEra(); yield return Frames(3);
            SimLayoutSource.DebugSkipYears(10f); yield return Frames(3);
            Assert.AreEqual(Era.Town, Sim.State.era);
            yield return new WaitForSecondsRealtime(1f);
            var hud = GameHud.Instance;
            yield return TapAt(GameUiPanel.ScreenCenter(hud.Root, "hud-title"));
            yield return new WaitForSecondsRealtime(0.8f);
            Assert.AreEqual(GameHud.Offices, hud.OpenSheet, "the name opens the offices");
            var scroll = hud.Root.Q<GlassScroll>("offices-scroll");
            Assert.IsNotNull(scroll, "the offices are in a scroll");
            Assert.AreSame(scroll, hud.OfficesScroll);
            var view = scroll.View;
            int rows = hud.Root.Q("offices-list").childCount;
            float all = view.contentContainer.layout.height, shown = view.layout.height;
            Debug.Log($"[fr-offices] {rows} rows, content {all:0} in view {shown:0}");
            yield return Shot("fr_offices_top");
            Assert.Greater(rows, 3, "a town has several offices");
            Assert.Greater(all, shown + 1f, "a town's list is longer than the sheet at 1080x1920");
            // the view ends on a whole row
            float top = view.contentContainer.worldBound.yMin, viewBottom = view.worldBound.yMax;
            foreach (var r in view.contentContainer.Query(className: GlassScroll.RowClass).ToList())
                Assert.IsFalse(r.worldBound.yMin < viewBottom - 1f && r.worldBound.yMax > viewBottom + 1f, $"no row is cut at the bottom ({r.name})");
            var from = GameUiPanel.ScreenCenter(view) - new Vector2(0f, view.worldBound.height * 0.25f * Screen.height / hud.Root.panel.visualTree.layout.height);
            yield return Drag(new[] { from, from + new Vector2(0f, Screen.height * 0.15f) }, Screen.height * 0.01f);
            yield return new WaitForSecondsRealtime(0.5f);
            Assert.Greater(view.scrollOffset.y, 10f, "a finger scrolls the list");
            Assert.AreEqual(GameHud.Offices, hud.OpenSheet, "and the sheet stays open");
            yield return Shot("fr_offices_scrolled");
            AssertNoErrors("offices");
        }
    }
}
