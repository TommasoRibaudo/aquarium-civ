using System.Collections;
using System.IO;
using AquariumCiv.Game;
using AquariumCiv.Life;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AquariumCiv.View.PlayTests
{
    /// <summary>fb4 #526/#540: the camp's fire-side props and the Keeper's feather posts, day and night, default and close (JPGs). Explicit.</summary>
    [Category("Fb4CampPropsShots"), Explicit]
    public class Fb4CampPropsShots : PlayTestBase
    {
        const string Dir = Shots + "fb4/540/";

        static BoxCamera Cam => Camera.main.GetComponent<BoxCamera>();

        static void Look(Vector3 world, float zoom, float pitch = 52f, float yawAdd = 0f)
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
            Directory.CreateDirectory(Dir);
            File.WriteAllBytes(Dir + name + ".jpg", tex.EncodeToJPG(88));
            Object.Destroy(tex);
        }

        static IEnumerator Wait(float s) { yield return new WaitForSecondsRealtime(s); }

        IEnumerator Round(string tag, BoxView box, PeopleView pv)
        {
            pv.Planner.FirePoint(out float fx, out float fz);
            pv.Planner.HideFrame(out float rx, out float rz, out _, out _);
            var fire = box.transform.TransformPoint(box.land.OnGround(fx, fz));
            var rack = box.transform.TransformPoint(box.land.OnGround(rx, rz));
            Transform feather = null, pot = null;
            foreach (var kv in ConsequencePropsView.Instance.Standing)
            {
                if (kv.Value == null) continue;
                if (kv.Value.name.StartsWith("Consequence_feather")) feather = kv.Value;
                if (kv.Value.name.StartsWith("Consequence_stew_pot")) pot = kv.Value;
            }
            Debug.Log($"[fb4] {tag} fire {fx:0.00},{fz:0.00} rack {rx:0.00},{rz:0.00} pot {(pot != null ? pot.localPosition.ToString("F2") : "none")} feather {(feather != null ? feather.localPosition.ToString("F2") : "none")}");
            foreach (float hour in new[] { 11.5f, 22.5f })
            {
                string t = hour < 18f ? "day" : "night";
                PeopleView.DebugSetHour(hour);
                yield return Wait(2.5f);
                Look((rack + fire) * 0.5f, 0.55f);
                yield return Wait(1f);
                yield return Jpg($"{tag}_{t}_default");
                Look(fire, 0.3f);
                yield return Wait(0.8f);
                yield return Jpg($"{tag}_{t}_fire_close");
                if (feather != null)
                {
                    Look(feather.position, 0.2f);
                    yield return Wait(0.8f);
                    yield return Jpg($"{tag}_{t}_feather_close");
                }
            }
        }

        [UnityTest]
        public IEnumerator TakeShots()
        {
            UseLivingLand();
            yield return OpenSlice(1179, 2556, 1234);
            var box = Object.FindAnyObjectByType<BoxView>();
            var pv = PeopleView.Find();
            Debug.Log("[fb4] " + EcoDebug.SkipSeasons(50));
            yield return Wait(2f);
            Debug.Log("[fb4] " + ConsequenceCheats.ShowCampProps());
            yield return Wait(2f);
            yield return Round("a", box, pv);
            Debug.Log("[fb4] " + EcoDebug.SkipSeasons(50));
            yield return Wait(2f);
            Debug.Log("[fb4] " + ConsequenceCheats.ShowCampProps());
            yield return Wait(2f);
            yield return Round("b", box, pv);
        }
    }
}
