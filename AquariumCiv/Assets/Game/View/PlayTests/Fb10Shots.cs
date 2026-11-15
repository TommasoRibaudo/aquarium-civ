using System.Collections;
using System.IO;
using System.Linq;
using AquariumCiv.Game;
using AquariumCiv.GameUI;
using AquariumCiv.Life;
using AquariumCiv.Sim;
using AquariumCiv.Sim.Eco;
using AquariumCiv.Sim.Settle;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AquariumCiv.View.PlayTests
{
    /// <summary>First outside playtest (#643, #644): a village's fields in each season (winter bare), and the wolf jar taken from the chooser
    /// (the chooser opens with one ready jar; the hint names the wolves; the camera brings their ground into view). Shots (1179x2556) to
    /// Temp/agent/fb10/.</summary>
    [Explicit("Screenshots for the eye: run by hand"), Category("Fb10Shots")]
    public class Fb10Shots : Fb5SeasonShots
    {
        const string Out = "F:/GameDev/AquariumCiv/Temp/agent/fb10/";
        static SimCore S => GameClock.Instance.ActiveSim;

        IEnumerator Snap(string name)
        {
            Directory.CreateDirectory(Out);
            yield return new WaitForEndOfFrame();
            var t = ScreenCapture.CaptureScreenshotAsTexture();
            File.WriteAllBytes(Out + name + ".png", t.EncodeToPNG());
            Object.Destroy(t);
        }

        IEnumerator Village()
        {
            ShowMeDirector.BubblesOn = false;
            yield return Stage(villageYears: 2);
            EcoDebug.VillageGate();
            EcoDebug.SkipSeasons(5 * 4);
            yield return new WaitForSecondsRealtime(1.5f);
        }

        [UnityTest]
        public IEnumerator FieldsBySeason()
        {
            // a grown village with farms (the FeedbackShotPlayTests recipe): the fields are the layout's, ecology or not
            yield return OpenSlice(1179, 2556, 1234);
            ShowMeDirector.BubblesOn = false;
            PeopleView.DebugSetHour(12f);
            MomentPlayer.DebugGrowVillage(30f);
            yield return Frames(3);
            S.ForceNextEra();
            yield return Frames(3);
            SimLayoutSource.DebugSkipYears(30f);
            yield return Frames(4);
            S.CleanAll();
            var farm = S.Settlement.Lots.FirstOrDefault(l => l.kind == LotKind.Farm);
            Assert.IsNotNull(farm, "a village with a farm lot");
            var rig = Camera.main.GetComponent<BoxCamera>();
            string[] names = { "spring", "summer", "autumn", "winter" };
            for (int s = 0; s < 4; s++)
            {
                yield return ToSeason(s);
                rig.FlyTo(Object.FindAnyObjectByType<BoxView>().transform.TransformPoint(new Vector3(farm.x, 0f, farm.z)), 0.35f, 0.3f);
                Debug.Log($"[fb10] {names[s]}: season {LifeClock.SeasonOf(S.State.month)} farm {farm.id} at {farm.x:0.00},{farm.z:0.00}");
                yield return new WaitForSecondsRealtime(1.6f);
                yield return Snap("644_field_" + names[s]);
            }
            CloseSlice();
        }

        [UnityTest]
        public IEnumerator WolfJarFromTheChooser()
        {
            yield return Village();
            foreach (var s in new[] { LifeSpecies.Thicket, LifeSpecies.Rabbits, LifeSpecies.Deer, LifeSpecies.Ducks, LifeSpecies.Fish })
                if (S.LifePlacedMonth(s) < 0) S.DebugPlaceNearCamp(s);
            S.DebugJarsReady(); // only the wolves are left to open
            yield return new WaitForSecondsRealtime(0.5f);
            var hud = GameHud.Instance;
            var lp = LifePlaceView.Instance;
            int frames0 = lp != null ? lp.GroundFrames : 0;
            var ready = new System.Collections.Generic.List<LifeSpecies>();
            S.ReadyJars(ready);
            Debug.Log($"[fb10] ready jars: {string.Join(",", ready)}; wolf den cells glow on take");
            yield return TapAt(GameUiPanel.ScreenCenter(hud.Root, "hud-clean"));
            yield return new WaitForSecondsRealtime(0.6f);
            yield return TapAt(GameUiPanel.ScreenCenter(hud.Root, "kit-life"));
            yield return new WaitForSecondsRealtime(0.6f);
            Assert.IsTrue(hud.LifeRowOpen, "#643: the chooser opens, a single ready jar too");
            Assert.AreEqual(GameHud.KitTool.None, hud.Hand, "nothing in hand before the pick");
            yield return Snap("643_chooser");
            yield return TapAt(GameUiPanel.ScreenCenter(hud.Root, "kit-life-wolves"));
            yield return new WaitForSecondsRealtime(0.3f);
            yield return Snap("643_taken");
            yield return new WaitForSecondsRealtime(1.5f);
            Assert.AreEqual(LifeSpecies.Wolves, hud.HeldLife);
            Debug.Log($"[fb10] glow cells {lp.GlowCells}, ground frames {lp.GroundFrames - frames0}");
            yield return Snap("643_ground");
            CloseSlice();
        }
    }
}
