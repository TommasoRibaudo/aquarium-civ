using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using AquariumCiv.GameUI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace AquariumCiv.View.PlayTests
{
    /// <summary>
    /// The feedback sheet (#55) by touch on the real Slice scene: HUD button, kind chip, text, Send. The sender's token is
    /// blanked so nothing ever reaches GitHub: the note must land in the outbox (JSON with the right labels, screenshot beside
    /// it) and wait there, which is also the offline path. The outbox is backed up and put back. Run alone.
    /// </summary>
    [Category("FeedbackPlay")]
    public class FeedbackSheetPlayTests : PlayTestBase
    {
        string outboxBackup;

        IEnumerator Tap(VisualElement root, string name)
        {
            var e = root.Q(name);
            Assert.IsNotNull(e, name);
            yield return TapAt(GameUiPanel.ScreenCenter(e));
        }

        [UnityTest]
        public IEnumerator ANoteSentByTouchLandsInTheOutboxWithLabelsAndAScreenshot()
        {
            outboxBackup = FeedbackSender.OutboxDir + "~playtest";
            if (Directory.Exists(outboxBackup)) Directory.Delete(outboxBackup, true);
            if (Directory.Exists(FeedbackSender.OutboxDir)) Directory.Move(FeedbackSender.OutboxDir, outboxBackup);
            try
            {
                yield return OpenSlice(seed: 5501);
                var sender = FeedbackSender.Ensure();
                typeof(FeedbackSender).GetField("token", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(sender, ""); // never reaches GitHub
                var hud = GameHud.Instance;
                Assert.IsNotNull(hud);
                var fb = FeedbackUI.Instance;
                Assert.IsNotNull(fb);

                yield return Tap(hud.Root, "hud-feedback");
                yield return Until(() => fb.IsOpen, 3f);
                Assert.IsTrue(fb.IsOpen, "the HUD button opens the sheet");
                yield return Shot("feedback_sheet_open");

                var send = fb.Root.Q<Button>("fb-send");
                Assert.IsFalse(send.enabledSelf, "Send waits for a kind and a text");
                yield return Tap(fb.Root, "fb-kind-bug");
                fb.Root.Q<TextField>("fb-text").value = "[TEST #55] Please ignore: automated check of the feedback sheet. Safe to close.";
                yield return Frames(3);
                Assert.IsTrue(send.enabledSelf, "Send enabled");
                yield return Tap(fb.Root, "fb-send");
                yield return Until(() => !fb.IsOpen, 3f);
                Assert.IsFalse(fb.IsOpen, "the sheet closes after Send");
                yield return Frames(10);
                yield return Shot("feedback_sheet_sent");

                Assert.AreEqual(1, sender.Pending, "one note waits in the outbox (no token: the offline path)");
                var json = Directory.GetFiles(FeedbackSender.OutboxDir, "*.json").Single();
                var text = File.ReadAllText(json);
                StringAssert.Contains("\"feedback\",\"fb:bug\",\"proto:slice\"", text.Replace(" ", ""));
                StringAssert.Contains("[TEST #55]", text);
                var jpg = Path.ChangeExtension(json, ".jpg");
                Assert.IsTrue(File.Exists(jpg), "the screenshot is beside it");
                Assert.Greater(new FileInfo(jpg).Length, 2000, "a real picture");
                Debug.Log("[fbtest] outbox payload: " + text.Substring(0, Mathf.Min(text.Length, 400)));

                // Never in the way of the wipe: clean mode keeps working with the button there.
                yield return Tap(hud.Root, "hud-clean");
                yield return Frames(5);
                Assert.IsTrue(hud.CleanMode, "Clean still starts");
                yield return Shot("feedback_clean_mode");
                var fbBtn = hud.Root.Q("hud-feedback");
                var glass = new Rect(Screen.width * 0.1f, Screen.height * 0.25f, Screen.width * 0.8f, Screen.height * 0.5f);
                Assert.IsFalse(glass.Contains(GameUiPanel.ScreenCenter(fbBtn)), "the Feedback button is not on the glass");
                AssertNoErrors("feedback sheet");
            }
            finally
            {
                if (Directory.Exists(FeedbackSender.OutboxDir)) Directory.Delete(FeedbackSender.OutboxDir, true);
                if (Directory.Exists(outboxBackup)) Directory.Move(outboxBackup, FeedbackSender.OutboxDir);
            }
        }
    }
}
