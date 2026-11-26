using System.Collections;
using System.Linq;
using AquariumCiv.Game;
using AquariumCiv.GameUI;
using AquariumCiv.Life;
using AquariumCiv.Sim;
using AquariumCiv.View.ShowMe;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace AquariumCiv.View.PlayTests
{
    /// <summary>Polish night 1 tester finding: in the first run the first-mist caption was drawn under Skip step on the frame it appeared, and the
    /// plate's "Tap to name your box" ran off the right edge on the frame it appeared. Checked every frame from the first one, at both phone sizes.</summary>
    [Category("FirstRunWordsClear")]
    public class FirstRunWordsClearPlayTests : PlayTestBase
    {
        [UnityTest, Timeout(170000)]
        public IEnumerator TheCaptionAndThePlateKeepClearFromTheFirstFrame([Values(1179, 750)] int w)
        {
            int h = w == 1179 ? 2556 : 1334;
            GameUiPanel.AcceptInputUnfocused(true);
            yield return OpenSlice(w, h, 1234, skipFirstRun: false);
            var fr = FirstRunView.Instance;
            Assert.IsTrue(fr.Active, "the first run");
            yield return Until(() => fr.Step == FirstRunStep.Wipe, 6f);
            fr.Run.DebugSkipTo(FirstRunStep.Name);
            ShowMeDirector.BubblesOn = true;
            var plate = fr.Root.Q("firstrun-plate");
            var rowEls = new[] { fr.Root.Q("firstrun-skip-step"), fr.Root.Q("firstrun-skip-all") };
            var d = ShowMeDirector.Instance;
            var panelRect = fr.Root.panel.visualTree.worldBound;
            bool said = false, sawCaption = false, sawPlate = false, sawSkip = false;
            for (int f = 0; f < 120; f++)
            {
                if (f == 20 && !said)
                {
                    ShowMeDirector.DebugSayOver(PersonRef.Notable(Sim().State.notables.First(n => n.alive).id), "The sky wept. The dust drank it.");
                    said = true;
                }
                yield return null;
                var cap = fr.Root.panel.visualTree.Q("showme-caption");
                bool capShown = cap != null && cap.resolvedStyle.display == DisplayStyle.Flex && d.CaptionUp;
                if (capShown)
                {
                    sawCaption = true;
                    foreach (var b in rowEls)
                        if (b.resolvedStyle.display != DisplayStyle.None && fr.Root.Q("firstrun-skip-row").resolvedStyle.display == DisplayStyle.Flex)
                        {
                            sawSkip = true;
                            Assert.IsFalse(cap.worldBound.Overlaps(b.worldBound), $"frame {f}: the caption {cap.worldBound} is over {b.name} {b.worldBound}");
                        }
                }
                if (plate.resolvedStyle.display == DisplayStyle.Flex && plate.resolvedStyle.visibility == Visibility.Visible)
                {
                    sawPlate = true;
                    var pb = plate.worldBound;
                    Assert.GreaterOrEqual(pb.xMin, panelRect.xMin - 0.5f, $"frame {f}: plate off the left {pb}");
                    Assert.LessOrEqual(pb.xMax, panelRect.xMax + 0.5f, $"frame {f}: plate off the right {pb} in {panelRect}");
                }
            }
            Assert.IsTrue(sawCaption, "the caption showed");
            Assert.IsTrue(sawPlate, "the plate showed");
            Assert.IsTrue(sawSkip || !FeedbackUI.InTestBuild, "the skip row was checked in a test build");
        }

        static SimCore Sim() => GameClock.Instance.ActiveSim;
    }
}
