using System.Collections;
using System.IO;
using AquariumCiv.Game;
using AquariumCiv.Life;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AquariumCiv.View.PlayTests
{
    /// <summary>#577 looks: hidden near panes over dry land and over the lit camp at night, then the lid from the default and a steep camera.
    /// Shots (1179x2556) to Temp/agent/fb5/577/&lt;tag&gt;_*.png; the tag is read from tag.txt in that folder (before / after).</summary>
    [Explicit("Screenshots for the eye: run by hand"), Category("Fb5Glass")]
    public class Fb5GlassShots : PlayTestBase
    {
        const string Dir = "F:/GameDev/AquariumCiv/Temp/agent/fb5/577/";
        static BoxCamera Rig => Camera.main.GetComponent<BoxCamera>();

        IEnumerator Shot(string name)
        {
            string tag = File.ReadAllText(Dir + "tag.txt").Trim();
            yield return new WaitForEndOfFrame();
            var t = ScreenCapture.CaptureScreenshotAsTexture();
            File.WriteAllBytes(Dir + tag + "_" + name + ".png", t.EncodeToPNG());
            Object.Destroy(t);
        }

        IEnumerator Settle(float s = 1.2f) { yield return new WaitForSecondsRealtime(s); }

        [UnityTest]
        public IEnumerator Shots()
        {
            Directory.CreateDirectory(Dir);
            ShowMeDirector.BubblesOn = false;
            yield return OpenSlice(1179, 2556, seed: 1234);
            Debug.Log("[fb5g] panes " + PaneCount());
            // Default camera, midday: the lid (after) and the whole box.
            PeopleView.DebugSetHour(12f);
            yield return Settle();
            yield return Shot("day_default");
            // Steep camera (the pitch ceiling), whole box.
            var rig = Rig;
            rig.EaseTo(rig.target, rig.yaw, 68f, 1f, 0.01f);
            yield return Settle();
            yield return Shot("day_steep");
            // Camp close, hidden panes, dry land at midday.
            rig.EaseTo(rig.target, 28f, 34f, 1f, 0.01f);
            yield return Settle(0.5f);
            rig.FlyToPlace(Place.Land(PlaceKind.Fire), 0.4f, 0.01f);
            yield return Settle();
            yield return Shot("day_close");
            // The lit camp at night.
            PeopleView.DebugSetHour(21.5f);
            yield return Settle(2f);
            yield return Shot("night_close");
            rig.FlyToPlace(Place.Land(PlaceKind.Fire), 0.7f, 0.01f);
            yield return Settle();
            yield return Shot("night_mid");
            CloseSlice();
        }

        static int PaneCount()
        {
            int n = 0;
            foreach (var r in Resources.FindObjectsOfTypeAll<MeshRenderer>())
                if (r != null && r.gameObject.activeInHierarchy && (r.name.StartsWith("Glass") || r.name == "Lid")) n++;
            return n;
        }
    }
}
