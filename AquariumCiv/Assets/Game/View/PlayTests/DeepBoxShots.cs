using System.Collections;
using System.IO;
using System.Text;
using AquariumCiv.Game;
using AquariumCiv.GameUI;
using AquariumCiv.Sim;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AquariumCiv.View.PlayTests
{
    /// <summary>#658: the deeper box (6 x 6, z from -2.2 to 3.8) at phone size: a new box, the camp frame, an old 44-row save grown,
    /// the mountain against the glass top, the back glass close up, and the shelf. Shots to Temp/agent/658/ (1179 x 2556).</summary>
    [Explicit("#658: screenshots for the eye, run by hand")]
    public class DeepBoxShots : PlayTestBase
    {
        const string Dir = "658/";
        static BoxCamera Rig => Camera.main.GetComponent<BoxCamera>();
        static SimCore Sim => GameClock.Instance.ActiveSim;
        static BoxView Box => PeopleView.Find().GetComponent<BoxView>();
        readonly StringBuilder log = new();
        void Log(string s) { log.AppendLine(s); Debug.Log("[deep] " + s); }

        IEnumerator View(string name, Vector3 target, float yaw, float pitch, float zoom)
        {
            Rig.CancelFlight();
            Rig.minZoom = 0.05f;
            Rig.target = target; Rig.yaw = yaw; Rig.pitch = pitch; Rig.zoom = zoom;
            Rig.Apply();
            yield return new WaitForSecondsRealtime(0.6f);
            yield return Shot(Dir + name);
        }

        /// <summary>A living box with a village standing, stage by the cheats a tester would use.</summary>
        IEnumerator Village(int seed)
        {
            GameClock.EcologyPref = true;
            yield return OpenSlice(1179, 2556, seed: seed);
            GameUiPanel.AcceptInputUnfocused(true);
            ShowMeDirector.BubblesOn = false;
            PeopleView.DebugSetHour(12f);
            Log(EcoDebug.OpenCampGate());
            for (int k = 0; k < 6; k++) { Log(EcoDebug.MistBigCamp()); yield return null; }
            foreach (var (x, z) in new[] { (-1.6f, 0.6f), (-1.0f, -0.8f), (-2.0f, -0.2f), (1.6f, 0.9f), (0.8f, -0.9f), (0f, 0.4f), (1f, 1.6f), (-1.4f, 1.5f) }) EcoDebug.MistBig(x, z);
            Log(EcoDebug.SkipSeasons(12));
            Log(EcoDebug.VillageGate());
            Log(EcoDebug.SkipSeasons(16));
            yield return new WaitForSecondsRealtime(1f);
            Log($"era {Sim.State.era}, buildings {Sim.State.buildings.Count}, pop {Sim.State.population}");
        }

        [UnityTest, Timeout(1200000)]
        public IEnumerator NewBoxAndCamp()
        {
            Directory.CreateDirectory(Shots + Dir);
            GameClock.EcologyPref = true;
            yield return OpenSlice(1179, 2556, seed: 4242);
            GameUiPanel.AcceptInputUnfocused(true);
            Rig.GoWholeBox(0.01f);
            yield return new WaitForSecondsRealtime(1.5f);
            yield return Shot(Dir + "1_new_box_default");
            File.WriteAllText(Shots + Dir + "log_new.txt", log.ToString());
        }

        [UnityTest, Timeout(1800000)]
        public IEnumerator VillageCampAndGrown()
        {
            Directory.CreateDirectory(Shots + Dir);
            yield return Village(4242);
            Rig.GoWholeBox(0.01f);
            yield return new WaitForSecondsRealtime(1.5f);
            yield return Shot(Dir + "2_village_default");
            CampFrameView.Auto = true;
            Log(CampFrameView.DebugCampFrame());
            yield return new WaitForSecondsRealtime(2.5f);
            yield return Shot(Dir + "3_camp_frame_B");
            Rig.GoWholeBox(0.01f);
            yield return new WaitForSecondsRealtime(1f);
            // the same box, as an old 44-row save loads
            Log(GameClock.DebugGrowFrom44());
            yield return new WaitForSecondsRealtime(2f);
            Rig.GoWholeBox(0.01f);
            yield return new WaitForSecondsRealtime(1.5f);
            yield return Shot(Dir + "4_grown_v3_default");
            var back = new Vector3(0f, 1f, Land.ZMax - 0.8f);
            yield return View("4b_grown_back_strip", back, 28f, 45f, 0.55f);
            Log(CampFrameView.DebugCampFrame());
            yield return new WaitForSecondsRealtime(2.5f);
            yield return Shot(Dir + "4c_grown_camp_frame");
            // the back glass close up
            yield return View("6_back_glass_close", new Vector3(0f, 1.2f, Land.ZMax - 0.3f), 20f, 22f, 0.28f);
            yield return View("6b_back_glass_from_front", new Vector3(0f, 1.2f, Land.ZMax - 0.5f), 0f, 30f, 0.4f);
            File.WriteAllText(Shots + Dir + "log_village.txt", log.ToString());
        }

        [UnityTest, Timeout(1800000)]
        public IEnumerator MountainAndShelf()
        {
            Directory.CreateDirectory(Shots + Dir);
            GameClock.EcologyPref = true;
            yield return OpenSlice(1179, 2556, seed: 4242);
            GameUiPanel.AcceptInputUnfocused(true);
            Log(BoxCompleteController.DebugComplete(BoxSetting.MountainVillage));
            yield return Frames(30);
            if (FirstRunView.Instance != null && FirstRunView.Instance.Active) FirstRunView.DebugSkipAll();
            yield return Frames(10);
            Rig.GoWholeBox(0.01f);
            yield return new WaitForSecondsRealtime(1.5f);
            yield return Shot(Dir + "5_mountain_default");
            yield return View("5b_mountain_back", new Vector3(0f, 1.5f, Land.ZMax - 1f), 28f, 20f, 0.6f);
            yield return View("5c_mountain_side", new Vector3(0f, 1.8f, Land.CZ), 75f, 14f, 0.9f);
            Log(BoxCompleteController.DebugShelf());
            yield return new WaitForSecondsRealtime(3f);
            yield return Shot(Dir + "7_shelf_museum_box");
            File.WriteAllText(Shots + Dir + "log_mountain.txt", log.ToString());
        }
    }
}
