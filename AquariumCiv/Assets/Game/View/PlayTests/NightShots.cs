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

        /// <summary>#671: wet ground along the near glass, a slow zoom from the default view to the closest; a shot per step with the
        /// panes' and frame's fade logged. <paramref name="winter"/>: the same in snow.</summary>
        [UnityTest, Timeout(600000)]
        public IEnumerator NearGlass671([Values(false, true)] bool winter)
        {
            UseLivingLand(true);
            yield return OpenSlice(1179, 2556, 4242);
            ShowMeDirector.BubblesOn = false;
            PeopleView.DebugSetHour(11f);
            var box = Object.FindAnyObjectByType<BoxView>();
            var rig = Camera.main.GetComponent<BoxCamera>();
            var camLocal = box.transform.InverseTransformPoint(Camera.main.transform.position);
            float nx = Mathf.Clamp(camLocal.x, -Land.HX + 0.5f, Land.HX - 0.5f), nz = Mathf.Clamp(camLocal.z, -Land.HZ + 0.5f, Land.HZ - 0.5f);
            // wet the ground along the two near panes
            for (float t = -1f; t <= 1f; t += 0.5f)
            {
                EcoDebug.MistBig(nx, nz + t * 1.2f * -Mathf.Sign(nz));
                EcoDebug.MistBig(nx + t * 1.6f * -Mathf.Sign(nx), nz);
            }
            if (winter) EcoDebug.SkipSeasons(3);
            yield return new WaitForSecondsRealtime(2f);
            var focus = box.transform.TransformPoint(box.land.OnGround(nx, nz));
            string tag = winter ? "winter" : "wet";
            foreach (float zoom in new[] { 1f, 0.6f, 0.45f, 0.35f, 0.28f, 0.22f, 0.18f })
            {
                for (float t0 = Time.realtimeSinceStartup; Time.realtimeSinceStartup - t0 < 1.5f;)
                {
                    rig.CancelFlight();
                    rig.target = focus; rig.zoom = zoom;
                    rig.Apply();
                    yield return null;
                }
                Debug.Log($"[night] 671 {tag} z{zoom}: panes {box.PaneOpacity(0):0.00} {box.PaneOpacity(1):0.00} {box.PaneOpacity(2):0.00} {box.PaneOpacity(3):0.00}" +
                          $" frame {box.FrameOpacity(0):0.00} {box.FrameOpacity(1):0.00} {box.FrameOpacity(2):0.00} {box.FrameOpacity(3):0.00} {box.FrameOpacity(4):0.00} {box.FrameOpacity(5):0.00} {box.FrameOpacity(6):0.00} {box.FrameOpacity(7):0.00}" +
                          $" shadowDist {UnityEngine.Rendering.Universal.UniversalRenderPipeline.asset.shadowDistance:0.0} near {Camera.main.nearClipPlane:0.000}");
                yield return Snap($"671_{tag}_z{zoom:0.00}");
            }
            CloseSlice();
        }

        /// <summary>#676: the camp's stew pot up close, by day, at dusk (the cook's hour) and in winter (more steam).</summary>
        [UnityTest, Timeout(600000)]
        public IEnumerator StewPot676()
        {
            UseLivingLand(true);
            yield return OpenSlice(1179, 2556, 1234);
            ShowMeDirector.BubblesOn = false;
            S.CleanAll();
            var rig = Camera.main.GetComponent<BoxCamera>();
            foreach (var (tag, hour, winter) in new[] { ("day", 11.5f, false), ("dusk", 17.8f, false), ("winter", 17.8f, true) })
            {
                if (winter) { EcoDebug.SkipSeasons(3); yield return new WaitForSecondsRealtime(1f); }
                GameObject pot = null;
                foreach (float hr in new[] { hour, 12.5f, 16.5f, 8f })
                {
                    PeopleView.DebugSetHour(hr);
                    yield return new WaitForSecondsRealtime(3f);
                    pot = GameObject.Find("Camp stew pot");
                    if (pot != null && pot.activeInHierarchy) { Debug.Log($"[night] 676 {tag}: pot at {hr}"); break; }
                }
                if (pot == null) { Debug.Log($"[night] 676 {tag}: no pot (nobody at the fire)"); continue; }
                foreach (var (zoom, yaw) in new[] { (0.06f, 0f), (0.03f, 25f) })
                {
                    rig.yaw += yaw;
                    for (float t0 = Time.realtimeSinceStartup; Time.realtimeSinceStartup - t0 < 1.2f;)
                    {
                        rig.CancelFlight(); rig.minZoom = 0.02f;
                        rig.target = pot.transform.position + Vector3.up * 0.05f; rig.zoom = zoom; rig.pitch = 40f;
                        rig.Apply();
                        yield return null;
                    }
                    Debug.Log($"[night] 676 {tag} z{zoom}: pot on screen at {Camera.main.WorldToViewportPoint(pot.transform.position)}");
                    yield return Snap($"676_pot_{tag}_z{zoom:0.00}");
                }
            }
            CloseSlice();
        }
    }
}
