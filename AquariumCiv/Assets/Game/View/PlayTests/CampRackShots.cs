using System.Collections;
using System.IO;
using AquariumCiv.Game;
using AquariumCiv.GameUI;
using AquariumCiv.Life;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AquariumCiv.View.PlayTests
{
    /// <summary>#332: the camp's drying rack and the fire without log seats, as the pictures a person judges (JPGs into Temp/agent). Explicit.</summary>
    [Category("CampRackShots"), Explicit]
    public class CampRackShots : PlayTestBase
    {
        static BoxCamera Cam => Camera.main.GetComponent<BoxCamera>();

        void Look(Vector3 world, float zoom, float pitch = 52f, float yawAdd = 0f)
        {
            var rig = Cam;
            rig.CancelFlight();
            rig.target = world; rig.zoom = zoom; rig.pitch = pitch; rig.yaw += yawAdd;
            rig.Apply();
        }

        static IEnumerator Jpg(string name)
        {
            yield return new WaitForEndOfFrame();
            var tex = ScreenCapture.CaptureScreenshotAsTexture();
            Directory.CreateDirectory(Shots);
            File.WriteAllBytes(Shots + name + ".jpg", tex.EncodeToJPG(85));
            Object.Destroy(tex);
        }

        static IEnumerator Wait(float s) { yield return new WaitForSecondsRealtime(s); }

        [UnityTest]
        public IEnumerator TakeShots()
        {
            UseLivingLand();
            yield return OpenSlice(1179, 2556, 1234);
            var box = Object.FindAnyObjectByType<BoxView>();
            var pv = PeopleView.Find();
            Debug.Log("[rackshots] " + EcoDebug.SkipSeasons(18));
            yield return Wait(2f);
            PeopleView.DebugSetHour(11.5f);
            Debug.Log("[rackshots] " + EcoDebug.ShowRack(6));
            yield return Wait(2f);
            Assert.IsTrue(pv.Planner.HideFrame(out float x, out float z, out _, out _));
            pv.Planner.FirePoint(out float fx, out float fz);
            var rack = box.transform.TransformPoint(box.land.OnGround(x, z)) + Vector3.up * 0.04f;
            var fire = box.transform.TransformPoint(box.land.OnGround(fx, fz));
            Debug.Log("[rackshots] " + EcoDebug.Status());
            Look((rack + fire) * 0.5f, 0.55f);
            yield return Wait(1.2f);
            yield return Jpg("332_camp_default_6hides");
            Look(rack, 0.2f);
            yield return Wait(1.0f);
            yield return Jpg("332_rack_close_6hides");
            Look(rack, 0.2f, 40f, 90f);
            yield return Wait(0.8f);
            yield return Jpg("332_rack_close_6hides_side");
            EcoDebug.ShowRack(1);
            Look(rack, 0.2f, 52f, -90f);
            yield return Wait(1.0f);
            yield return Jpg("332_rack_close_1hide");
            Look(fire, 0.3f);
            PeopleView.DebugSetHour(17.4f);
            yield return Wait(5f);
            yield return Until(() => GameUiPanel.Toasts.Current == null && GameUiPanel.Toasts.Waiting == 0, 8f);
            yield return Jpg("332_fire_sitters_late_afternoon");
        }
    }
}
