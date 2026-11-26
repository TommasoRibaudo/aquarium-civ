using System.Collections;
using System.IO;
using AquariumCiv.Game;
using AquariumCiv.Life;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AquariumCiv.View.PlayTests
{
    /// <summary>#559 looks: weed patches at the home view, a closer patch and the tufts round the Keeper's tree.
    /// Shots (1179x2556) to Temp/agent/559/&lt;tag&gt;_*.png; the tag is read from tag.txt in that folder (before / after).</summary>
    [Explicit("Screenshots for the eye: run by hand"), Category("WeedShots")]
    public class WeedShots559 : PlayTestBase
    {
        static readonly string Dir = UnityEngine.Application.dataPath + "/../Temp/agent/559/";
        static BoxCamera Rig => Camera.main.GetComponent<BoxCamera>();

        IEnumerator Shot(string name)
        {
            Directory.CreateDirectory(Dir);
            string tag = File.Exists(Dir + "tag.txt") ? File.ReadAllText(Dir + "tag.txt").Trim() : "x";
            yield return new WaitForEndOfFrame();
            var t = ScreenCapture.CaptureScreenshotAsTexture();
            File.WriteAllBytes(Dir + tag + "_" + name + ".png", t.EncodeToPNG());
            Object.Destroy(t);
        }

        [UnityTest, Timeout(240000)]
        public IEnumerator Shots()
        {
            Directory.CreateDirectory(Dir);
            ShowMeDirector.BubblesOn = false;
            yield return OpenSlice(1179, 2556, seed: 1234);
            var sim = GameClock.Instance.ActiveSim;
            PeopleView.DebugSetHour(12f);
            sim.AddMess(1f, 1f);
            yield return new WaitForSecondsRealtime(2.5f);
            yield return Shot("home_fullmess");
            var mp = Object.FindFirstObjectByType<MessProps>();
            var names = new System.Text.StringBuilder();
            if (mp != null) foreach (var r in mp.GetComponentsInChildren<Renderer>()) names.Append(r.name).Append('@').Append(r.transform.position.ToString("F2")).Append("; ");
            Debug.Log("[weedshot] mess pieces: " + names);
            var rig = Rig;
            // a closer view of the first weed patch
            if (mp != null)
            {
                foreach (var r in mp.GetComponentsInChildren<Renderer>())
                    if (r.name.StartsWith("Weeds")) { rig.EaseTo(r.transform.position, rig.yaw, rig.pitch, 0.45f, 0.01f); break; }
            }
            yield return new WaitForSecondsRealtime(1.5f);
            yield return Shot("patch_close");
            Debug.Log("[weedshot] tree weeds grown: " + sim.DebugGrowTreeWeeds());
            yield return new WaitForSecondsRealtime(2f);
            if (sim.EcoTree.cell >= 0)
            {
                var tr = sim.EcoTree;
                rig.EaseTo(new Vector3(tr.x, 0.2f, tr.z), rig.yaw, rig.pitch, 0.45f, 0.01f);
                yield return new WaitForSecondsRealtime(1.5f);
                yield return Shot("tree_tufts");
            }
            CloseSlice();
        }
    }
}
