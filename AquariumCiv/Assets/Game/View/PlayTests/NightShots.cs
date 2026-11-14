using System.Collections;
using System.IO;
using AquariumCiv.Game;
using AquariumCiv.GameUI;
using AquariumCiv.Sim;
using AquariumCiv.Sim.Eco;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AquariumCiv.View.PlayTests
{
    /// <summary>Polish night (2026-11-14) shots for the eye: #675 the life jar's seed choice and its hint, at two phone sizes.
    /// Shots to Temp/agent/night/.</summary>
    [Explicit("Screenshots for the eye: run by hand"), Category("NightShots")]
    public class NightShots : PlayTestBase
    {
        const string Out = "F:/GameDev/AquariumCiv/Temp/agent/night/";
        static SimCore S => GameClock.Instance.ActiveSim;

        protected static IEnumerator Snap(string name)
        {
            Directory.CreateDirectory(Out);
            yield return new WaitForEndOfFrame();
            var t = ScreenCapture.CaptureScreenshotAsTexture();
            File.WriteAllBytes(Out + name + ".png", t.EncodeToPNG());
            Object.Destroy(t);
        }

        [UnityTest, Timeout(600000)]
        public IEnumerator SeedChoice675([Values(1179, 750)] int w)
        {
            int h = w == 750 ? 1334 : 2556;
            UseLivingLand(true);
            yield return OpenSlice(w, h, 4242);
            GameUiPanel.AcceptInputUnfocused(true);
            ShowMeDirector.BubblesOn = false;
            PeopleView.DebugSetHour(11f);
            foreach (var (x, z) in new[] { (-1.6f, 0.6f), (-1.0f, -0.8f), (1.6f, 0.9f) }) EcoDebug.MistBig(x, z);
            yield return Frames(10);
            EcoDebug.SeedInHand();
            S.DebugJarsReady(); // a full row: the groups beside the seed
            var hud = GameHud.Instance;
            hud.SetHand(GameHud.KitTool.None);
            yield return Until(() => hud.IsVisible && !(PersonCardView.Instance != null && (PersonCardView.Instance.AnyOpen || PersonCardView.Instance.BuildingOpen)), 30f);
            if (hud.SheetOpen) hud.CloseSheet();
            yield return new WaitForSecondsRealtime(1f);
            yield return TapAt(GameUiPanel.ScreenCenter(hud.Root, "hud-clean"));
            yield return new WaitForSecondsRealtime(0.6f);
            yield return TapAt(GameUiPanel.ScreenCenter(hud.Root, "kit-life"));
            yield return new WaitForSecondsRealtime(0.6f);
            Assert.IsTrue(hud.LifeRowOpen, "the jar's chooser is open");
            yield return Snap($"675_jar_row_{w}x{h}");
            yield return TapAt(GameUiPanel.ScreenCenter(hud.Root, "kit-life-seed"));
            yield return Until(() => GameUiPanel.Toasts.Current == GameHud.SeedHint, 6f);
            yield return new WaitForSecondsRealtime(0.5f);
            Assert.AreEqual(GameHud.SeedHint, GameUiPanel.Toasts.Current, "the seed's hint");
            yield return Snap($"675_seed_hint_{w}x{h}");
            CloseSlice();
        }
    }
}
