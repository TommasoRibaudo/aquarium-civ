using System.Collections;
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

            // One year on screen: the tag on the playhead. The HUD's own has left, and so have the tools.
            var hud = GameHud.Instance;
            Assert.AreEqual(DisplayStyle.None, hud.Root.Q("hud-top").resolvedStyle.display, "the HUD's year leaves");
            Assert.AreEqual(Visibility.Hidden, hud.Root.Q("hud-bottom").resolvedStyle.visibility, "and the tools");
            Assert.IsNull(screen.Root.Q("tl-toast"), "words go in the one shared toast");
            StringAssert.StartsWith("Year ", screen.Root.Q<Label>("tl-year").text);

            // Pin ticks stop short of the era names (question 20)
            var label = screen.Root.Q(className: "tl-band-label");
            Assert.IsNotNull(label, "a 45-year box names its eras");
            foreach (var pin in screen.Root.Query(className: "tl-pin").ToList())
                Assert.LessOrEqual(pin.worldBound.yMax, label.worldBound.yMin + 0.5f, "a pin tick clears the era label");

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

            // The caption's controls have the play row's place (spec §4), so a story is put away before play
            Assert.AreEqual(DisplayStyle.None, screen.Root.Q(className: "tl-controls").resolvedStyle.display, "the caption's row replaces the play row");
            Assert.AreEqual(screen.Bar.resolvedStyle.width, 992f, 1f, "the timeline keeps the full width at a pin");
            yield return TapName("tl-cap-close");
            yield return Frames(3);
            Assert.IsNull(m.Caption);

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
                Assert.AreEqual(DisplayStyle.None, GameHud.Instance.Root.Q("hud-top").resolvedStyle.display, "closing the card doesn't bring the HUD's year back");
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

            // The word comes in the shared toast, above the panel
            yield return Until(() => GameHud.Instance.Root.Q("hud-toast").resolvedStyle.display == DisplayStyle.Flex, 2f);
            var toast = GameHud.Instance.Root.Q<Label>("hud-toast");
            StringAssert.StartsWith("Saved: ", toast.text);
            Assert.LessOrEqual(toast.worldBound.yMax, screen.Root.Q("tl-panel").worldBound.yMin, "the toast rides above the panel");

            yield return TapName("tl-close");
            yield return Frames(4);
            AssertNoErrors("snapshot and who");
        }

        /// <summary>#196: playthrough, year 3.6: the replay ended at year 3.0, without the building the player had just seen go up.</summary>
        [UnityTest]
        public IEnumerator TheReplayEndsOnTheBoxAsItIsNowAndTheSnapshotHasIt()
        {
            yield return OpenSlice(seed: 7903);
            yield return Grow(5);
            var sim = GameClock.Instance.Sim;
            for (int i = 0; i < 7; i++) sim.Step();
            var built = sim.DebugBuild(BuildingType.Hut);
            int lot = built.lotId;
            Assert.Greater(lot, 0, "the new building has a lot");
            int seed = SimLayout.SeedOfLot(lot);
            yield return Frames(2);

            var screen = TimeLapseScreenView.Ensure();
            Assert.IsTrue(screen.Open());
            yield return Until(() => screen.Model.Layout.Width > 0f, 2f);
            yield return Frames(4);
            var box = UnityEngine.Object.FindAnyObjectByType<BoxView>();
            var m = screen.Model;
            Assert.IsTrue(box.layout.pieces.Any(p => p.seed == seed), "the end of the replay shows what went up this year");
            yield return Shot("tl-196-end");

            // Back to the year's start it's gone, and it comes back the way any other year's buildings do.
            float yearStart = Mathf.Floor(m.Clock.End);
            m.Clock.Seek(yearStart);
            yield return Frames(4);
            Assert.IsFalse(box.layout.pieces.Any(p => p.seed == seed), "the year's start stays as it was");
            m.Clock.Seek(yearStart + (m.Clock.End - yearStart) * 0.8f);
            m.SetSpeed(m.SpeedIndex); // a seek from here doesn't bump the model's version, and Now reads as unavailable until it's redrawn
            yield return Frames(4);
            var part = box.layout.pieces.FirstOrDefault(p => p.seed == seed);
            Assert.IsNotNull(part, "on the way to now it grows in");
            Assert.Less(part.grow, 1f);
            Assert.Greater(part.grow, 0.5f);

            yield return TapName("tl-now");
            yield return Frames(4);
            Assert.IsTrue(m.AtNow);
            Assert.IsTrue(box.layout.pieces.Any(p => p.seed == seed && p.grow >= 1f));
            string folder = TimeLapseSnapshot.Folder;
            if (System.IO.Directory.Exists(folder)) System.IO.Directory.Delete(folder, true);
            yield return TapName("tl-snapshot");
            yield return Until(() => System.IO.Directory.Exists(folder) && System.IO.Directory.GetFiles(folder, "*.png").Length > 0, 6f);
            var files = System.IO.Directory.GetFiles(folder, "*.png");
            Assert.AreEqual(1, files.Length);
            System.IO.File.Copy(files[0], Shots + "tl-196-snapshot.png", true);
            yield return TapName("tl-close");
            yield return Frames(4);
            AssertNoErrors("the end of the replay");
        }
    }
}
