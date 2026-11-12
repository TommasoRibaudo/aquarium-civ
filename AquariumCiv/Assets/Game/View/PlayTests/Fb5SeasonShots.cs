using System.Collections;
using System.IO;
using System.Linq;
using AquariumCiv.Game;
using AquariumCiv.GameUI;
using AquariumCiv.Life;
using AquariumCiv.Sim;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AquariumCiv.View.PlayTests
{
    /// <summary>#576 seasons: stages one living-land camp (seed 4242, a few mist strokes, the camp gate, grown-ups, a village after the
    /// first years) and shoots it in each season, at the default camera and close at the camp. <see cref="Stage"/> and <see cref="ToSeason"/>
    /// are public so the orchestrator's walkthrough can reuse the setup. Shots (1179x2556) to Temp/agent/fb5/576/&lt;folder&gt;/&lt;season&gt;_default|close.png.</summary>
    [Explicit("Screenshots for the eye: run by hand"), Category("Fb5Season")]
    public class Fb5SeasonShots : PlayTestBase
    {
        public static string Dir = "F:/GameDev/AquariumCiv/Temp/agent/fb5/576/spec3/";
        static SimCore Sim => GameClock.Instance.ActiveSim;
        static BoxCamera Rig => Camera.main.GetComponent<BoxCamera>();

        /// <summary>A living-land camp with people, midday, at the given size.</summary>
        public IEnumerator Stage(int w = 1179, int h = 2556, int seed = 4242, int grownUps = 8, int villageYears = 0)
        {
            GameClock.EcologyPref = true;
            GameUiPanel.AcceptInputUnfocused(true);
            yield return OpenSlice(w, h, seed: seed);
            GameUiPanel.AcceptInputUnfocused(true);
            PeopleView.DebugSetHour(12f);
            foreach (var (x, z) in new[] { (-1.6f, 0.6f), (-1.0f, -0.8f), (-2.0f, -0.2f), (1.6f, 0.9f) }) EcoDebug.MistBig(x, z);
            EcoDebug.SkipSeasons(1);
            EcoDebug.OpenCampGate();
            for (int i = 0; i < 12 && Sim.State.era != Era.Camp; i++) { EcoDebug.MistCamp(); EcoDebug.SkipSeasons(1); }
            for (int k = 0; k < 6; k++) foreach (var (x, z) in new[] { (-2.4f, 1.2f), (0f, 1.4f), (2.4f, -1f), (0.5f, -1.6f), (-0.5f, 0.2f), (2f, 1.5f) }) EcoDebug.MistBig(x, z);
            EcoDebug.AddRabbits(4);
            int adults = Sim.State.notables.Count(n => n.alive && n.age >= 16f);
            for (int i = adults; i < grownUps; i++) if (Sim.DebugBirth() == null) break;
            if (villageYears > 0) EcoDebug.SkipSeasons(villageYears * 4);
            yield return new WaitForSecondsRealtime(1f);
        }

        /// <summary>Pins the look to the middle of a season (0 spring, 1 summer, 2 autumn, 3 winter) and lets the box settle.</summary>
        public static IEnumerator ToSeason(int season, float settle = 1.2f)
        {
            // the real clock: the sim moves on to the middle of the next season until it is the one asked for (the title word follows)
            for (int i = 0; i < 4 && (int)LifeClock.SeasonOf(Sim.State.month) != season; i++) EcoDebug.SkipToNextSeason();
            yield return new WaitForSecondsRealtime(settle);
        }

        IEnumerator Shot(string name)
        {
            Directory.CreateDirectory(Dir);
            yield return new WaitForEndOfFrame();
            var t = ScreenCapture.CaptureScreenshotAsTexture();
            File.WriteAllBytes(Dir + name + ".png", t.EncodeToPNG());
            Object.Destroy(t);
        }

        static readonly string[] Names = { "spring", "summer", "autumn", "winter" };

        [UnityTest]
        public IEnumerator Shots()
        {
            ShowMeDirector.BubblesOn = false;
            yield return Stage(villageYears: 3);
            var rig = Rig;
            var home = rig.Home; // the camp frame: the default camera
            var box = (target: new Vector3(0f, 1f, 0f), yaw: 28f, pitch: 34f, zoom: 1f); // the whole box
            for (int s = 0; s < 4; s++)
            {
                yield return ToSeason(s);
                rig.EaseTo(home.target, home.yaw, home.pitch, home.zoom, 0.3f);
                yield return new WaitForSecondsRealtime(1.2f);
                yield return Shot(Names[s] + "_default");
                rig.EaseTo(box.target, box.yaw, box.pitch, box.zoom, 0.3f);
                yield return new WaitForSecondsRealtime(1.2f);
                yield return Shot(Names[s] + "_box");
                rig.FlyToPlace(Place.Land(PlaceKind.Fire), 0.4f, 0.3f);
                yield return new WaitForSecondsRealtime(1.6f);
                yield return Shot(Names[s] + "_close");
            }
            CloseSlice();
        }

        /// <summary>The title line with the season word at 750x1334 (autumn, then winter).</summary>
        [UnityTest]
        public IEnumerator Title750()
        {
            ShowMeDirector.BubblesOn = false;
            yield return Stage(750, 1334, villageYears: 3);
            foreach (int s in new[] { 2, 3 })
            {
                yield return ToSeason(s);
                yield return Shot("title750_" + Names[s]);
            }
            CloseSlice();
        }
    }
}
