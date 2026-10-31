using System.Collections;
using System.Linq;
using AquariumCiv.Game;
using AquariumCiv.GameUI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AquariumCiv.View.PlayTests
{
    /// <summary>
    /// The time-lapse screen (#79 phase 2) by touch on the real Slice scene at portrait phone size: open, drag the playhead,
    /// tap a story pin, play and pause, the Now button, close (the live box and the sim clock come back). Written without the
    /// Editor in phase 2: phase 3 runs it and fixes what the real panel does differently. The model's rules are covered by
    /// the dotnet tests (View/TimeLapse/Core/Tests); this checks the drawing and the touch path only.
    /// </summary>
    [Category("TimeLapseScreenPlay")]
    public class TimeLapseScreenPlayTests : PlayTestBase
    {
        static IEnumerator Grow(int years)
        {
            for (int i = 0; i < years; i++)
            {
                SimLayoutSource.DebugSkipYears(1f);
                if (i % 10 == 0) yield return null;
            }
            GameClock.Instance.Sim.CleanAll();
            yield return null; yield return null;
        }

        IEnumerator TapName(string name)
        {
            var e = TimeLapseScreenView.Instance.Root.Q(name);
            Assert.IsNotNull(e, name);
            yield return TapAt(GameUiPanel.ScreenCenter(e));
        }

        [UnityTest]
        public IEnumerator OpenDragTapAPinPlayNowAndClose()
        {
            yield return OpenSlice(seed: 7901);
            yield return Grow(45);
            var screen = TimeLapseScreenView.Ensure();
            var view = TimeLapseView.Find();
            Assert.IsNotNull(view, "the replay view exists");
            Assert.IsTrue(screen.Open(), "opens with a past to show");
            yield return Until(() => screen.Model.Layout.Width > 0f, 2f);
            yield return Frames(3);
            Assert.IsTrue(view.Active);
            Assert.IsTrue(GameClock.Instance.Held, "the live sim is held while the screen is open");
            var m = screen.Model;
            Assert.IsTrue(m.AtNow, "opens at the present");
            Assert.IsFalse(m.NowEnabled);
            yield return Shot("tl-open");

            // drag the playhead from Now back to about year 10
            float target = m.Layout.XOf(10f);
            yield return Drag(new[] { screen.ScreenPointAtBarX(m.Layout.XOf(m.Clock.End) - 2f), screen.ScreenPointAtBarX(target) }, 20f);
            yield return Frames(2);
            Assert.AreEqual(10f, m.Position, 3f, "the playhead followed the finger (a snap to a pin or era start may move it a little)");
            Assert.AreEqual(m.Position, view.Position, 0.01f, "the box is shown where the model says");
            Assert.IsFalse(m.Playing);
            Assert.IsTrue(m.NowEnabled);
            yield return Shot("tl-dragged");

            // tap the first story pin
            Assert.Greater(m.Layout.Groups.Count, 0, "a 45-year box has story pins");
            var g = m.Layout.Groups.First();
            yield return TapAt(screen.ScreenPointAtBarX(g.x));
            yield return Frames(2);
            Assert.IsNotNull(m.Caption, "a tapped pin shows its story");
            Assert.AreEqual(g.Year, m.Position, 0.05f);
            Assert.IsNotNull(screen.Root.Q("tl-caption"));
            yield return Shot("tl-pin");

            // play, then pause
            yield return TapName("tl-play");
            Assert.IsTrue(m.Playing);
            yield return Until(() => m.Position > g.Year + 1f, 5f);
            Assert.Greater(m.Position, g.Year);
            Assert.AreEqual(m.Position, view.Position, 0.5f, "the box follows the clock without being ticked twice");
            yield return TapName("tl-play");
            Assert.IsFalse(m.Playing);

            // Now
            yield return TapName("tl-now");
            Assert.IsTrue(m.AtNow);

            // close: the live box comes back
            yield return TapName("tl-close");
            yield return Frames(3);
            Assert.IsFalse(screen.IsOpen);
            Assert.IsFalse(view.Active);
            Assert.IsFalse(GameClock.Instance.Held, "the sim clock runs again");
            AssertNoErrors("time-lapse screen");
        }
    }
}
