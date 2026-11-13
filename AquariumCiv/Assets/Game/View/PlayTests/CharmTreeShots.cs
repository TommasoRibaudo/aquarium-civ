using System.Collections;
using System.IO;
using System.Linq;
using System.Text;
using AquariumCiv.Game;
using AquariumCiv.GameUI;
using AquariumCiv.Life;
using AquariumCiv.Sim;
using AquariumCiv.Sim.Settle;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AquariumCiv.View.PlayTests
{
    /// <summary>#635 (playtest #634): "where do we remember the ones we lose", answered "the tree": first with no grown tree (a pole stands
    /// in), then the Keeper's tree grows and the charms move onto it; two more deaths add charms. Shots to Temp/agent/635/.</summary>
    [Explicit("Screenshots for the eye: run by hand")]
    public class CharmTreeShots : PlayTestBase
    {
        const string Dir = "635/";
        static SimCore Sim => GameClock.Instance.ActiveSim;
        static BoxCamera Rig => Camera.main.GetComponent<BoxCamera>();
        readonly StringBuilder log = new();
        void Log(string s) { log.AppendLine(s); Debug.Log("[635] " + s); }

        [UnityTest, Timeout(600000), Category("CharmTree")]
        public IEnumerator CharmsOnTheKeepersTree()
        {
            Directory.CreateDirectory(Shots + Dir);
            GameClock.EcologyPref = true;
            yield return OpenSlice(1179, 2556, seed: 4242);
            GameUiPanel.AcceptInputUnfocused(true);
            ShowMeDirector.BubblesOn = false;
            PeopleView.DebugSetHour(12f);
            foreach (var (x, z) in new[] { (-1.6f, 0.6f), (-1.0f, -0.8f), (-2.0f, -0.2f), (1.6f, 0.9f) }) EcoDebug.MistBig(x, z);
            EcoDebug.SkipSeasons(1);
            EcoDebug.OpenCampGate();
            for (int i = 0; i < 12 && Sim.State.era != Era.Camp; i++) { EcoDebug.MistCamp(); EcoDebug.SkipSeasons(1); }
            yield return new WaitForSecondsRealtime(1f);
            Assert.AreEqual(Era.Camp, Sim.State.era, "a camp");

            var d = Sim.DebugRaiseDilemma("camp_remember");
            Assert.IsNotNull(d, Sim.DilemmaWhyNot("camp_remember"));
            var v = Sim.ViewOfDilemma(d);
            Log($"question: {v.question}");
            foreach (var o in v.options) Log($"  {o.label}: {o.hint}");
            Assert.IsTrue(Sim.AnswerDilemma(d.id, 1, DilemmaPath.Card));
            Log("answer line: " + Sim.State.chronicle.Last(e => e.kind == "keeper").text);
            var p = Sim.ConsequencePropFor(d.id, 1);
            Log($"placed {p.placed}, tree cell {p.treeCell}, at ({p.x:0.00}, {p.z:0.00}), words '{Sim.CharmTreeWords}'");

            var box = Object.FindAnyObjectByType<BoxView>();
            IEnumerator Look(string name, float yaw)
            {
                var world = box.transform.TransformPoint(box.land.OnGround(p.x, p.z));
                Rig.EaseTo(world + Vector3.up * 0.25f, yaw, 16f, 0.22f, 0.3f);
                yield return new WaitForSecondsRealtime(1.5f);
                yield return Shot(Dir + name);
            }
            if (p.treeCell < 0) yield return Look("a_pole", Rig.Home.yaw);

            Assert.IsTrue(Sim.DebugTreeStage(4), "the Keeper's tree grows");
            EcoDebug.SkipSeasons(1);
            yield return new WaitForSecondsRealtime(2f);
            Log($"after growing: tree cell {p.treeCell} (Keeper's {Sim.State.eco.treeCell}), charms {Sim.CharmsOn(p)}");
            Assert.AreEqual(Sim.State.eco.treeCell, p.treeCell, "the charms hang on the Keeper's tree");
            yield return Look("b_tree_1", Rig.Home.yaw);
            Sim.DebugKillOldest(); Sim.DebugKillOldest();
            yield return new WaitForSecondsRealtime(1f);
            Log($"two more deaths: charms {Sim.CharmsOn(p)}");
            yield return Look("c_tree_3", Rig.Home.yaw);
            yield return Look("d_tree_3_side", Rig.Home.yaw + 70f);
            Rig.EaseTo(Rig.Home.target, Rig.Home.yaw, Rig.Home.pitch, Rig.Home.zoom, 0.3f);
            yield return new WaitForSecondsRealtime(1.5f);
            yield return Shot(Dir + "e_home");
            File.WriteAllText(Shots + Dir + "log.txt", log.ToString());
        }
    }
}
