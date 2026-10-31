using System.Collections;
using System.Linq;
using AquariumCiv.Game;
using AquariumCiv.GameUI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

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
            yield return Frames(4);
            yield return Shot("tl-before-close");
            yield return TapName("tl-close");
            yield return Frames(3);
            Assert.IsFalse(screen.IsOpen);
            Assert.IsFalse(view.Active);
            Assert.IsFalse(GameClock.Instance.Held, "the sim clock runs again");
            AssertNoErrors("time-lapse screen");
        }

        /// <summary>The panel scale on a second phone size, the Snapshot button's PNG (with its label), and Who on a pin.</summary>
        [UnityTest]
        public IEnumerator SnapshotSavesAPngWhoOpensTheCardAndThePanelFitsAt1080x2340()
        {
            yield return OpenSlice(1080, 2340, seed: 7902);
            yield return Grow(45);
            var screen = TimeLapseScreenView.Ensure();
            Assert.IsTrue(screen.Open());
            yield return Until(() => screen.Model.Layout.Width > 0f, 2f);
            yield return Frames(4);
            var m = screen.Model;
            var vt = screen.Root.panel.visualTree.worldBound;
            Assert.AreEqual(1080f, vt.width, 1f, "the panel is 1080 units wide at this size");
            var pw = screen.Root.Q("tl-panel").worldBound;
            Assert.LessOrEqual(pw.xMax, vt.xMax + 0.5f); Assert.GreaterOrEqual(pw.xMin, -0.5f);
            Assert.LessOrEqual(pw.yMax, vt.yMax + 0.5f, "the panel stays on screen");
            Assert.Greater(screen.Bar.resolvedStyle.width, 800f, "the bar uses the width");
            Assert.AreEqual(screen.Bar.resolvedStyle.width, m.Layout.Width, 1f, "the model laid out for the bar's width");
            yield return Shot("tl-1080x2340");

            // Who: a pin of a person opens their card over the panel
            var g = m.Layout.Groups.FirstOrDefault(x => x.pins.Any(p => p.notableId >= 0));
            if (g != null)
            {
                int idx = g.pins.FindIndex(p => p.notableId >= 0);
                m.TapPin(g, idx);
                yield return Frames(4);
                var who = screen.Root.Q("tl-cap-person");
                Assert.AreNotEqual(UnityEngine.UIElements.DisplayStyle.None, who.resolvedStyle.display, "Who shows for a person's story");
                yield return TapAt(GameUiPanel.ScreenCenter(who));
                yield return Until(() => PersonCardView.Instance.IsOpen, 2f);
                Assert.IsTrue(PersonCardView.Instance.IsOpen, "Who opens the person card");
                yield return Shot("tl-who");
                PersonCardView.Instance.Close();
                yield return Frames(4);
            }

            // Snapshot: a PNG with the label, saved
            string folder = TimeLapseSnapshot.Folder;
            if (System.IO.Directory.Exists(folder)) System.IO.Directory.Delete(folder, true);
            m.GoNow();
            yield return Frames(4);
            yield return TapName("tl-snapshot");
            yield return Until(() => System.IO.Directory.Exists(folder) && System.IO.Directory.GetFiles(folder, "*.png").Length > 0, 6f);
            var files = System.IO.Directory.Exists(folder) ? System.IO.Directory.GetFiles(folder, "*.png") : new string[0];
            Assert.AreEqual(1, files.Length, "Snapshot saved one PNG");
            Assert.Greater(new System.IO.FileInfo(files[0]).Length, 20000, "the PNG has a picture in it");
            System.IO.Directory.CreateDirectory(Shots);
            System.IO.File.Copy(files[0], Shots + "tl-snapshot.png", true);
            StringAssert.Contains("year-", System.IO.Path.GetFileName(files[0]));

            yield return TapName("tl-close");
            yield return Frames(4);
            AssertNoErrors("snapshot and who");
        }
    }
}
