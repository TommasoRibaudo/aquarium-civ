using System.Collections;
using AquariumCiv.Game;
using AquariumCiv.Sim;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AquariumCiv.View.PlayTests
{
    /// <summary>
    /// P8 #269, #270: the pond meets the glass and the water table is damp earth in the soil cut. Plays the real living box at
    /// 1179x2556, raises the table with the debug cheats (low, mid, high, the pond surfaced and full) and shoots the default
    /// camera and a side view into Temp/agent/water_*.png. Checks the water reaches both glass walls once it is full and that the
    /// damp earth rises with the table.
    /// </summary>
    [Category("EcoWater")]
    public class EcoWaterPlayTests : PlayTestBase
    {
        static SimCore Sim => GameClock.Instance.ActiveSim;
        static BoxCamera Cam => Camera.main.GetComponent<BoxCamera>();

        IEnumerator Views(string tag)
        {
            var cam = Cam;
            float yaw = cam.yaw, pitch = cam.pitch, zoom = cam.zoom; var target = cam.target;
            yield return Frames(30);
            yield return Shot($"water_{tag}_default");
            cam.yaw = -30f; cam.pitch = 14f; cam.zoom = 0.8f; cam.Apply();
            yield return Frames(60);
            yield return Shot($"water_{tag}_side");
            cam.yaw = yaw; cam.pitch = pitch; cam.zoom = zoom; cam.target = target; cam.Apply();
            yield return Frames(30);
        }

        static float DampTop(Mesh m)
        {
            // The highest vertex with a visible damp amount.
            float top = 0f;
            var v = m.vertices; var c = m.colors;
            for (int i = 0; i < v.Length; i++) if (c[i].a > 0.05f && v[i].y > top) top = v[i].y;
            return top;
        }

        [UnityTest, Timeout(600000)]
        public IEnumerator TheTableShowsAsDampEarth_AndThePondReachesTheGlass()
        {
            bool prefWas = GameClock.EcologyPref;
            GameClock.EcologyPref = true;
            try
            {
                yield return OpenSlice(skipFirstRun: true);
                Assert.IsTrue(Sim.EcologyOn);
                var box = Object.FindAnyObjectByType<BoxView>();
                float flood = Sim.EcoFloodLine;
                float prevTop = -1f;
                foreach (var (tag, above) in new[] { ("low", -0.55f), ("mid", -0.3f), ("high", -0.08f) })
                {
                    EcoDebug.Table(above);
                    yield return Frames(20);
                    var damp = box.DebugDampMesh;
                    Assert.IsNotNull(damp, "the damp earth is drawn at table " + tag);
                    float top = DampTop(damp);
                    Debug.Log($"[water] {tag}: table {Sim.EcoTable:0.000} flood {flood:0.000} damp top {top:0.000} pond {Sim.EcoPondCells}");
                    Assert.Greater(top, prevTop, "the damp earth rises with the table (" + tag + ")");
                    prevTop = top;
                    yield return Views(tag);
                }
                // The pond full: the hollow's water runs to both glass walls.
                EcoDebug.Table(0f);
                EcoDebug.SkipSeasons(6);
                yield return Frames(20);
                Assert.Greater(Sim.EcoPondCells, 20, "a real pond");
                var wb = box.DebugWaterMesh.bounds;
                Debug.Log($"[water] full: pond {Sim.EcoPondCells} bounds {wb.min} .. {wb.max}");
                Assert.GreaterOrEqual(wb.max.x, Land.HX + BoxView.GlassInner - 0.002f, "the water meets the right glass");
                Assert.LessOrEqual(wb.min.z, -Land.HZ - BoxView.GlassInner + 0.002f, "the water meets the front glass");
                yield return Views("full");
            }
            finally { GameClock.EcologyPref = prefWas; }
        }
    }
}
