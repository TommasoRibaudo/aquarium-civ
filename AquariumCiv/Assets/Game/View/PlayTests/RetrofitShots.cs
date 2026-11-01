using System.Collections;
using System.IO;
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
    /// #168, #169: every state of boards 3.5 and 3.7 on the real scene, at 1179x2556 and 750x1334, for the eye. Not a check: only a thrown
    /// exception fails it. PNGs go to Temp/agent/retrofit/&lt;width&gt;/. Run one test at a time, by name, with
    /// <c>--include_explicit true --async_tests true</c>.
    /// </summary>
    [Explicit("Screenshots for the eye: run by hand"), Category("RetrofitShots")]
    public class RetrofitShots : PlayTestBase
    {
        string dir;

        IEnumerator Cap(string name)
        {
            yield return new WaitForEndOfFrame();
            var tex = ScreenCapture.CaptureScreenshotAsTexture();
            Directory.CreateDirectory(dir);
            File.WriteAllBytes(dir + name + ".png", tex.EncodeToPNG());
            Object.Destroy(tex);
        }

        static IEnumerator Seconds(float s) { yield return new WaitForSecondsRealtime(s); }

        static IEnumerator Grow(float years)
        {
            for (float y = 0f; y < years; y += 10f) { SimLayoutSource.DebugSkipYears(Mathf.Min(10f, years - y)); yield return null; }
            GameClock.Instance.Sim.CleanAll();
            GameClock.Instance.Sim.CardsSeenToNow();
            PeopleView.DebugSetHour(11f);
            yield return Frames(30);
        }

        IEnumerator Tap(VisualElement root, string name)
        {
            yield return TapAt(GameUiPanel.ScreenCenter(root.Q(name) ?? throw new System.ArgumentException(name)));
            yield return Frames(6);
        }

        [UnityTest, Timeout(600000)]
        public IEnumerator TimeLapse([Values(1179, 750)] int w)
        {
            dir = Shots + "retrofit/" + w + "/";
            yield return OpenSlice(w, w == 750 ? 1334 : 2556, seed: 7531);
            DebugDock.ForceLocked = true;
            var fresh = TimeLapseScreenView.Ensure();
            fresh.Open();
            yield return Frames(10);
            yield return Cap("3.5-empty");
            fresh.Close();
            yield return Frames(6);

            yield return Grow(45f);
            var sim = GameClock.Instance.Sim;
            for (int i = 0; i < 7; i++) sim.Step();
            yield return Frames(4);
            var screen = TimeLapseScreenView.Ensure();
            Assert.IsTrue(screen.Open());
            yield return Until(() => screen.Model.Layout.Width > 0f, 2f);
            yield return Frames(20);
            var m = screen.Model;
            yield return Cap("3.5-paused-now");
            TimeLapseScreenView.BookRibbon = true;
            m.Clock.Seek(m.Clock.End); m.SetSpeed(m.SpeedIndex);
            yield return Frames(6);
            yield return Cap("3.5-paused-ribbon");
            TimeLapseScreenView.BookRibbon = false;

            var bands = m.Lapse.EraBands();
            float era = bands.Count > 1 ? bands[1].startYear : 10f;
            m.Clock.Seek(Mathf.Max(0f, era - 0.5f));
            m.Play();
            yield return Until(() => m.EraCard != null, 8f);
            yield return Cap("3.5-era-card");
            yield return Seconds(0.2f);
            yield return Cap("3.5-playing");
            yield return Until(() => m.Caption != null, 8f);
            yield return Frames(3);
            yield return Cap("3.5-playing-caption");
            m.Pause();
            yield return Frames(4);

            var person = m.Layout.Groups.FirstOrDefault(x => x.pins.Any(p => p.notableId >= 0 && !x.IsGroup)) ?? m.Layout.Groups.First(x => x.pins.Any(p => p.notableId >= 0));
            m.TapPin(person, Mathf.Max(0, person.pins.FindIndex(p => p.notableId >= 0)));
            yield return Frames(10);
            yield return Cap("3.5-pin-person");
            var nobody = m.Layout.Groups.FirstOrDefault(x => x.pins.Any(p => p.notableId < 0));
            if (nobody != null)
            {
                m.TapPin(nobody, nobody.pins.FindIndex(p => p.notableId < 0));
                yield return Frames(10);
                yield return Cap("3.5-pin-nobody");
            }
            var group = m.Layout.Groups.FirstOrDefault(x => x.IsGroup);
            if (group != null)
            {
                m.TapPin(group, 0);
                yield return Frames(10);
                yield return Cap("3.5-pin-group");
            }
            m.DismissCaption();
            yield return Frames(4);
            yield return Tap(screen.Root, "tl-book");
            yield return Seconds(0.4f);
            yield return Cap("3.5-book-toast");
            m.GoNow();
            yield return Seconds(3f);
            if (Directory.Exists(TimeLapseSnapshot.Folder)) Directory.Delete(TimeLapseSnapshot.Folder, true);
            yield return Tap(screen.Root, "tl-snapshot");
            yield return Seconds(1.2f);
            yield return Cap("3.5-snapshot-toast");
            string[] files = Directory.Exists(TimeLapseSnapshot.Folder) ? Directory.GetFiles(TimeLapseSnapshot.Folder, "*.png") : new string[0];
            if (files.Length > 0) File.Copy(files[0], dir + "3.5-snapshot-saved.png", true);
            yield return Tap(screen.Root, "tl-close");
            AssertNoErrors("time-lapse shots");
        }

        [UnityTest, Timeout(600000)]
        public IEnumerator FirstRun([Values(1179, 750)] int w)
        {
            dir = Shots + "retrofit/" + w + "/";
            yield return OpenSlice(w, w == 750 ? 1334 : 2556, seed: 7531, skipFirstRun: false);
            DebugDock.ForceLocked = true;
            var run = FirstRunView.Instance;
            var hud = GameHud.Instance;
            yield return Until(() => run.Step == FirstRunStep.Wipe, 6f);
            yield return Seconds(1.6f);
            yield return Cap("3.7-wipe");
            FirstRunView.DebugReward();
            yield return Frames(6);
            yield return Cap("3.7-wipe-reward");
            yield return Seconds(3.8f);
            run.Run.DebugSkipTo(FirstRunStep.Patch);
            yield return Seconds(1.6f);
            yield return Cap("3.7-patch");
            run.Run.DebugSkipTo(FirstRunStep.Name);
            yield return Seconds(1.6f);
            yield return Cap("3.7-plate-blank");
            yield return Tap(run.Root, "firstrun-plate");
            yield return Seconds(0.2f);
            yield return Cap("3.7-naming-suggested");
            FirstRunView.DebugKeyboard = true;
            yield return Frames(8);
            yield return Cap("3.7-naming-keyboard");
            FirstRunView.DebugKeyboard = false;
            yield return Frames(8);
            yield return Tap(run.Root, "firstrun-name-reroll");
            yield return Cap("3.7-naming-another");
            FirstRunView.DebugRefuseName();
            yield return Frames(6);
            yield return Cap("3.7-naming-invalid");
            yield return Tap(run.Root, "firstrun-name-close");
            yield return Cap("3.7-naming-later");
            yield return Tap(run.Root, "firstrun-plate");
            run.Root.Q<TextField>("firstrun-name-field").value = "Mossbarrow";
            yield return Frames(4);
            yield return Tap(run.Root, "firstrun-name-keep");
            yield return Seconds(0.2f);
            yield return Cap("3.7-keep");
            yield return Seconds(2f);
            yield return Cap("3.7-rain");
            yield return Tap(run.Root, "firstrun-rain");
            yield return Seconds(0.8f);
            yield return Cap("3.7-rain-armed");
            var (rx, rz) = run.Run.SuggestedRainSpot;
            yield return TapAt(ShapingView.Instance.ScreenOf(rx, rz));
            yield return Seconds(6f);
            yield return Cap("3.7-year");
            yield return Until(() => run.Step == FirstRunStep.Card, 25f);
            yield return Until(() => run.CardOpen, 4f);
            yield return Seconds(0.6f);
            yield return Cap("3.7-card");
            var showMe = run.Root.Q("firstrun-card-showme");
            if (showMe.resolvedStyle.display != DisplayStyle.None)
            {
                yield return Tap(run.Root, "firstrun-card-showme");
                yield return Seconds(2.5f);
                yield return Cap("3.7-card-showme");
                yield return Tap(run.Root, "firstrun-showme-back");
                yield return Seconds(0.4f);
            }
            yield return Tap(run.Root, "firstrun-card-done");
            yield return Seconds(1.4f);
            yield return Cap("3.7-done");
            Assert.IsFalse(run.Active);
            AssertNoErrors("first-run shots");
        }

        public override void TearDown()
        {
            FirstRunView.DebugKeyboard = false;
            TimeLapseScreenView.BookRibbon = false;
            DebugDock.ResetLock();
            base.TearDown();
        }
    }
}
