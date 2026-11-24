using System.Collections;
using System.IO;
using AquariumCiv.Game;
using AquariumCiv.GameUI;
using AquariumCiv.Sim;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AquariumCiv.View.PlayTests
{
    /// <summary>#668: every kit state (kit-mockups.html s0 to s6, each tool in hand, the longest name in the bar), staged by
    /// <see cref="KitStates"/> as the debug panel does, at 750x1334 and 1179x2556. Shots to Temp/agent/668/ (or KIT_SHOTS_DIR).</summary>
    [Explicit("Screenshots for the eye: run by hand"), Category("KitShots")]
    public class KitShots : PlayTestBase
    {
        static string Out
        {
            get
            {
                var d = System.Environment.GetEnvironmentVariable("KIT_SHOTS_DIR");
                return string.IsNullOrEmpty(d) ? Application.dataPath + "/../Temp/agent/668/" : d.TrimEnd('/', '\\') + "/";
            }
        }

        static SimCore Sim => GameClock.Instance.ActiveSim;

        IEnumerator Snap(string name)
        {
            Directory.CreateDirectory(Out);
            yield return new WaitForEndOfFrame();
            var t = ScreenCapture.CaptureScreenshotAsTexture();
            File.WriteAllBytes(Out + name + ".png", t.EncodeToPNG());
            Object.Destroy(t);
        }

        IEnumerator Every(int w, int h)
        {
            UseLivingLand(true);
            GameUiPanel.AcceptInputUnfocused(true);
            yield return OpenSlice(w, h, seed: 4242);
            GameUiPanel.AcceptInputUnfocused(true);
            Assert.IsTrue(Sim.EcologyOn, "a living-land box");
            PeopleView.DebugSetHour(10f);
            foreach (var (x, z) in new[] { (-1.6f, 0.6f), (-1.0f, -0.8f), (-2.0f, -0.2f), (1.6f, 0.9f), (0.8f, -0.9f) }) EcoDebug.MistBig(x, z);
            yield return Frames(10);
            var hud = GameHud.Instance;
            int i = 0;
            foreach (var state in KitStates.All)
            {
                // the states that need the Keeper's own seed come before the tree goes in
                string said = KitStates.Stage(state);
                Debug.Log($"[668] {w}x{h} {state}: {said} | note: {hud.LifeNoteText.Replace('\n', '/')} | bar: {hud.KitWord}");
                yield return new WaitForSecondsRealtime(state == KitStates.Waits ? 1.1f : 0.8f);
                yield return Snap($"{w}x{h}_{i:00}_{state.Replace(' ', '-').Replace("(", "").Replace(")", "")}");
                i++;
            }
            KitStates.Stage(KitStates.HandMister);
            hud.SetHand(GameHud.KitTool.None);
            CloseSlice();
        }

        [UnityTest, Timeout(900000)]
        public IEnumerator Phone750() { yield return Every(750, 1334); }

        [UnityTest, Timeout(900000)]
        public IEnumerator Phone1179() { yield return Every(1179, 2556); }
    }
}
