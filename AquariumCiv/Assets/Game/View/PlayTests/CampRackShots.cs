using System.Collections;
using AquariumCiv.Game;
using AquariumCiv.GameUI;
using AquariumCiv.Life;
using AquariumCiv.Sim;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AquariumCiv.View.PlayTests
{
    /// <summary>#332: the camp's drying rack and the fire without log seats, as the pictures a person judges (into Temp/agent). Explicit.</summary>
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


        [UnityTest]
        public IEnumerator Shots()
        {
            UseLivingLand();
            yield return OpenSlice(1179, 2556, 1234);
            var box = Object.FindAnyObjectByType<BoxView>();
            var pv = PeopleView.Find();
            PeopleView.DebugSetHour(11f);
            Debug.Log("[rackshots] " + EcoDebug.ShowRack(1));
            yield return new WaitForSecondsRealtime(1.5f);
            yield return Shot("rack_default_1hide");
            EcoDebug.ShowRack(7);
            yield return new WaitForSecondsRealtime(1.0f);
            yield return Shot("rack_default_7hides");
            Assert.IsTrue(pv.Planner.HideFrame(out float x, out float z, out _, out _));
            var at = box.transform.TransformPoint(box.land.OnGround(x, z)) + Vector3.up * 0.05f;
            var cvw = CampView.Instance;
            Look(at, 0.15f);
            yield return new WaitForSecondsRealtime(1.2f);
            Debug.Log($"[rackshots] frame {cvw.FrameShown} hides {cvw.HidesShown} zoom {Cam.zoom} min {Cam.minZoom}");
            var fr = GameObject.Find("HideFrame"); var fm = fr != null ? fr.GetComponent<MeshFilter>().sharedMesh : null;
            Debug.Log($"[rackshots] mesh verts {(fm != null ? fm.vertexCount : -1)} bounds {(fm != null ? fm.bounds.ToString() : "-")} tgt {Cam.target} at {at}");
            yield return Shot("rack_close_7hides");
            Look(at, 0.15f, 40f, 70f);
            yield return new WaitForSecondsRealtime(0.8f);
            yield return Shot("rack_close_7hides_b");
            EcoDebug.ShowRack(1);
            Look(at, 0.15f, 52f, -70f);
            yield return new WaitForSecondsRealtime(1.0f);
            yield return Shot("rack_close_1hide");
            pv.Planner.FirePoint(out float fx, out float fz);
            Look(box.transform.TransformPoint(box.land.OnGround(fx, fz)), 0.25f);
            PeopleView.DebugSetHour(12.8f);
            yield return new WaitForSecondsRealtime(3f);
            yield return Shot("fire_sitters_noon");
            PeopleView.DebugSetHour(18.2f);
            yield return new WaitForSecondsRealtime(4f);
            yield return Shot("fire_sitters_evening");
        }
    }
}
