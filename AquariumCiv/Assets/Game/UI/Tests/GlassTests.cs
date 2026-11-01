using System;
using AquariumCiv.Audio;
using NUnit.Framework;
using UnityEngine;

namespace AquariumCiv.GameUI.Tests
{
    /// <summary>Keeper's Glass, the parts with no pixels in them (#108): the one-message queue, the lanes, the line tag, the role table.</summary>
    public class GlassTests
    {
        [Test]
        public void OneMessageAtATimeAndTheNextWaits()
        {
            var q = new MessageQueue();
            q.Post("first", 3f);
            q.Post("second", 3f);
            Assert.IsTrue(q.Tick(0f));
            Assert.AreEqual("first", q.Current);
            Assert.IsFalse(q.Tick(1f));
            Assert.AreEqual("first", q.Current, "the one showing keeps the lane");
            Assert.IsTrue(q.Tick(MessageQueue.MinSeconds), "with a line waiting it gives way after the minimum");
            Assert.AreEqual("second", q.Current);
            Assert.IsTrue(q.Tick(MessageQueue.MinSeconds + 3f));
            Assert.IsNull(q.Current, "and the lane empties");
        }

        [Test]
        public void AQuietQueueGivesAMessageItsFullTime()
        {
            var q = new MessageQueue();
            q.Post("alone", 3f);
            q.Tick(0f);
            Assert.IsFalse(q.Tick(2.9f));
            Assert.AreEqual("alone", q.Current);
            q.Tick(3f);
            Assert.IsNull(q.Current);
        }

        [Test]
        public void ARepeatStaysOneMessageAndTheQueueCantGrow()
        {
            var q = new MessageQueue();
            q.Post("same", 3f);
            q.Post("same", 3f);
            q.Tick(0f);
            q.Post("same", 3f);
            Assert.AreEqual(0, q.Waiting, "the same line again is the same message");
            for (int i = 0; i < 10; i++) q.Post("line " + i, 3f);
            Assert.AreEqual(MessageQueue.MaxWaiting, q.Waiting, "the oldest goes first");
        }

        [Test]
        public void AHintThatNoLongerAppliesIsTakenBack()
        {
            var q = new MessageQueue();
            q.Post("showing", 3f);
            q.Tick(0f);
            q.Post("hint", 3f);
            q.Post("next", 3f);
            q.Withdraw("hint");
            q.Withdraw("showing");
            Assert.AreEqual(1, q.Waiting, "only the waiting hint goes");
            Assert.AreEqual("showing", q.Current, "the one on screen stays");
            q.Tick(3.1f);
            Assert.AreEqual("next", q.Current);
        }

        [Test]
        public void APromptHoldsTheToastsAndTheyComeBackAfter()
        {
            var q = new MessageQueue();
            q.Post("showing", 3f);
            q.Tick(0f);
            q.Post("next", 3f);
            q.Hold("watch", true);
            Assert.IsTrue(q.Tick(1f), "the toast goes down for the prompt");
            Assert.IsNull(q.Current);
            Assert.IsFalse(q.Tick(50f), "nothing shows while it's held");
            q.Hold("other", true);
            q.Hold("watch", false);
            q.Tick(51f);
            Assert.IsNull(q.Current, "still held by the other reason");
            q.Hold("other", false);
            q.Tick(52f);
            Assert.AreEqual("showing", q.Current, "first back is the one that was cut off");
            q.Tick(52f + MessageQueue.MinSeconds);
            Assert.AreEqual("next", q.Current);
        }

        [Test]
        public void LanesCountFromTheScreenEdgeAndATallHomeBarLiftsThem()
        {
            Assert.AreEqual(GameUiPanel.PromptBottom, GameUiPanel.Lane(GameUiPanel.PromptBottom, 0f), 0.01f, "no inset");
            Assert.AreEqual(0f, GameUiPanel.Lane(GameUiPanel.ToolsBottom, GameUiPanel.ToolsBottom), 0.01f, "a 30 pt inset: the lane is the inset");
            float home34 = 34f * GlassType.Pt;
            Assert.AreEqual(0f, GameUiPanel.Lane(GameUiPanel.ToolsBottom, home34), 0.01f, "tools sit on the safe edge, the larger of 30 and the inset");
            Assert.AreEqual(GameUiPanel.PinBottom - GameUiPanel.ToolsBottom, GameUiPanel.Lane(GameUiPanel.PinBottom, home34), 0.01f, "the spacing between lanes holds");
            Assert.Less(GameUiPanel.ToolsBottom + 154f, GameUiPanel.PinBottom, "tools under pins");
            Assert.Less(GameUiPanel.PinBottom + 121f, GameUiPanel.ToastBottom, "pins under toasts");
            Assert.Less(GameUiPanel.ToastBottom, GameUiPanel.PromptBottom, "toasts under prompts");
        }

        [Test]
        public void LineHeightIsAPxTag()
        {
            Assert.AreEqual("<line-height=55.69px>x", GlassType.Lines("x"));
            StringAssert.DoesNotContain("%", GlassType.Lines("x"), "a percent tag is a percent of the font's own line");
        }

        [Test]
        public void EveryRoleHasARealCueAndTheSpecsHaptic()
        {
            foreach (UiRole role in Enum.GetValues(typeof(UiRole)))
            {
                if (role == UiRole.None) continue;
                var cue = CueLibrary.Get(UiSound.Cue(role));
                Assert.IsNotNull(cue, role.ToString());
                if (role == UiRole.NewTool)
                {
                    Assert.AreEqual(HapticKind.None, cue.haptic, "the moments' chime stays silent to the hand");
                    Assert.AreEqual(HapticKind.Success, UiSound.Haptic(role));
                }
                else Assert.AreEqual(UiSound.Haptic(role), cue.haptic, role.ToString());
            }
        }

        [Test]
        public void ARoleAsksForItsCue()
        {
            Sfx.ResetLog();
            UiSound.Play(UiRole.Close);
            UiSound.Play(UiRole.None);
            Assert.AreEqual(1, Sfx.Asked);
            Assert.AreEqual(CueLibrary.UiClose, Sfx.LastAsked[0]);
        }
    }
}
