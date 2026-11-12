using System.Collections;
using System.Collections.Generic;
using System.Linq;
using AquariumCiv.Game;
using AquariumCiv.GameUI;
using AquariumCiv.Lighting;
using AquariumCiv.Sim;
using AquariumCiv.Sim.Eco;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AquariumCiv.View.PlayTests
{
    /// <summary>
    /// #577 the lid: a glass pane on top of the box. It is in the camera's way from the default view (every tap below crosses it), but it has
    /// no collider and is not on the grime layer, so a mist stroke, a tweezers pick and a Kit placement all work through it by real touch.
    /// And the glass never leaks: the pane count (renderers and materials) is the same after every <see cref="BoxView.Rebuild"/>.
    /// </summary>
    [Category("Lid")]
    public class LidPlayTests : PlayTestBase
    {
        static SimCore Sim => GameClock.Instance.ActiveSim;
        static BoxView Box => Object.FindAnyObjectByType<BoxView>();

        static int PaneRenderers() => Resources.FindObjectsOfTypeAll<MeshRenderer>()
            .Count(r => r != null && r.gameObject.scene.IsValid() && (r.name.StartsWith("Glass") || r.name == "Lid"));

        static int GlassMaterials()
        {
            var shader = Shader.Find("AquariumCiv/View/Glass");
            return Resources.FindObjectsOfTypeAll<Material>().Count(m => m != null && m.shader == shader && (m.hideFlags & HideFlags.DontSave) != 0);
        }

        /// <summary>Is the lid between the camera and this screen point?</summary>
        static bool LidInTheWay(Vector2 p) => Box.Lid != null && Box.Lid.bounds.IntersectRay(Camera.main.ScreenPointToRay(p));

        static string LidInfo(Vector2 p) { var r = Camera.main.ScreenPointToRay(p); var b = Box.Lid.bounds; return $"lid centre {b.center} size {b.size}, ray from {r.origin} dir {r.direction}"; }

        [UnityTest]
        public IEnumerator TheLid_IsThere_AndTheGlassCountStaysFixedAfterEveryRebuild()
        {
            yield return OpenSlice(1179, 2556, seed: 1234);
            var box = Box;
            Assert.IsNotNull(box.Lid, "the lid is built");
            Assert.AreEqual(5, PaneRenderers(), "four sides and a lid");
            Assert.IsNull(box.Lid.GetComponent<Collider>(), "no collider: nothing can be blocked by it");
            Assert.AreNotEqual(CareView.GlassLayer, box.Lid.gameObject.layer, "not on the glass (grime) layer");
            Assert.Greater(box.LidOpacity, 0.9f, "at the default view the lid is there");
            bool anyUnderLid = false;
            for (float fy = 0.2f; fy < 0.8f && !anyUnderLid; fy += 0.05f)
                for (float fx = 0.1f; fx < 0.9f && !anyUnderLid; fx += 0.05f) anyUnderLid = LidInTheWay(new Vector2(Screen.width * fx, Screen.height * fy));
            Assert.IsTrue(anyUnderLid, "from the default view some of the screen looks through the lid: " + LidInfo(new Vector2(Screen.width * 0.5f, Screen.height * 0.5f)));
            int mats = GlassMaterials();
            Assert.AreEqual(5, mats, "one glass material per pane");
            for (int i = 0; i < 4; i++)
            {
                box.Rebuild();
                yield return Frames(5);
                Assert.AreEqual(5, PaneRenderers(), $"pane renderers after rebuild {i + 1}");
                Assert.AreEqual(mats, GlassMaterials(), $"glass materials after rebuild {i + 1}");
                Assert.AreEqual(5, box.Root.Cast<Transform>().Count(t => t.name.StartsWith("Glass") || t.name == "Lid"), "panes under the one root");
            }
            Assert.IsNotNull(box.Lid, "the lid is rebuilt with the box");
        }

        [UnityTest]
        public IEnumerator TheLid_GoesWhenTheCameraLooksSteeplyDown_AndWhenClose()
        {
            yield return OpenSlice(1179, 2556, seed: 1234);
            var rig = Camera.main.GetComponent<BoxCamera>();
            var box = Box;
            rig.EaseTo(rig.target, rig.yaw, 68f, 1f, 0.01f);
            yield return new WaitForSecondsRealtime(1.5f);
            Assert.Less(box.LidOpacity, 0.35f, "top-down: the lid has faded");
            rig.EaseTo(rig.target, rig.yaw, 30f, 1f, 0.01f);
            yield return new WaitForSecondsRealtime(1.5f);
            Assert.Greater(box.LidOpacity, 0.9f, "back at the default angle it is there");
            rig.FlyTo(rig.target, 0.4f, 0.01f);
            yield return new WaitForSecondsRealtime(1.5f);
            Assert.Less(box.LidOpacity, 0.2f, "zoomed in: the near glass is gone, the lid too");
        }

        [UnityTest]
        public IEnumerator AMistStroke_WorksThroughTheLid()
        {
            UseLivingLand(true);
            yield return OpenSlice(1179, 2556, seed: 1234);
            Assert.IsTrue(Sim.EcologyOn, "a living-land box");
            var mist = EcoMistView.Instance;
            Assert.IsNotNull(mist);
            var hud = GameHud.Instance;
            if (hud != null && hud.CleanMode) hud.SetCleanMode(false);
            yield return Frames(20);
            Vector2 at = default; bool found = false;
            for (float fy = 0.25f; fy < 0.75f && !found; fy += 0.05f)
                for (float fx = 0.15f; fx < 0.85f && !found; fx += 0.05f)
                {
                    var p = new Vector2(Screen.width * fx, Screen.height * fy);
                    if (!UiInput.Over(p) && mist.GroundPoint(p, out _) && LidInTheWay(p)) { at = p; found = true; }
                }
            Assert.IsTrue(found, "ground to mist on screen");
            Assert.IsTrue(LidInTheWay(at), "the lid is between the finger and the ground: " + LidInfo(at));
            int id = touchId++;
            BeginTouch(id, at, queueEventOnly: true);
            yield return null;
            float t0 = Time.unscaledTime; bool sawMisting = false;
            while (Time.unscaledTime - t0 < 2.2f)
            {
                MoveTouch(id, at + new Vector2(Mathf.Sin(Time.unscaledTime * 9f), 0f), delta: Vector2.zero, queueEventOnly: true);
                sawMisting |= mist.Misting;
                yield return null;
            }
            EndTouch(id, at, delta: Vector2.zero, queueEventOnly: true);
            yield return Frames(10);
            Assert.IsTrue(sawMisting, "a still finger under the lid turned into a mist");
            Assert.Greater(mist.MistPulses, 2, "the sim was told");
        }

        [UnityTest]
        public IEnumerator ATweezersPick_WorksThroughTheLid()
        {
            UseLivingLand(true);
            yield return OpenSlice(1179, 2556, seed: 1234);
            var care = CareView.Instance; var hud = GameHud.Instance;
            if (hud.CleanMode) hud.SetCleanMode(false);
            Sim.CleanAll();
            Sim.AddMess(0f, 0.4f);
            yield return Frames(60);
            MessProps.Piece weed = care.Props.AllPieces.FirstOrDefault(p => !p.leaving && p.kind == MessProps.Kind.Weed && OnScreen(Camera.main.WorldToScreenPoint(p.Center)));
            Assert.IsNotNull(weed, "a weed in view");
            var s = Camera.main.WorldToScreenPoint(weed.Center);
            Assert.IsTrue(LidInTheWay(s), "the lid is between the finger and the weed");
            hud.SetHand(GameHud.KitTool.Tweezers);
            yield return null;
            float og0 = Sim.State.overgrowth;
            yield return TapAt(s);
            Assert.AreEqual("weed", care.LastPluck, "the weed came out");
            Assert.Less(Sim.State.overgrowth, og0 - 0.05f, "the weed is gone from the sim");
            hud.SetHand(GameHud.KitTool.None);
        }

        static bool OnScreen(Vector3 s) => s.x > 40 && s.y > 200 && s.x < Screen.width - 40 && s.y < Screen.height - 300 && s.z > 0;

        [UnityTest, Timeout(600000)]
        public IEnumerator AKitPlacement_WorksThroughTheLid()
        {
            UseLivingLand(true);
            GameUiPanel.AcceptInputUnfocused(true);
            yield return OpenSlice(1179, 2556, seed: 4242);
            GameUiPanel.AcceptInputUnfocused(true);
            PeopleView.DebugSetHour(10f);
            foreach (var (x, z) in new[] { (-1.6f, 0.6f), (-1.0f, -0.8f), (-2.0f, -0.2f), (1.6f, 0.9f), (0.8f, -0.9f) }) EcoDebug.MistBig(x, z);
            yield return Frames(10);
            Sim.DebugJarsReady();
            yield return Frames(10);
            Assert.Greater(Box.LidOpacity, 0.9f, "the lid is on");
            yield return KeeperPlaces(LifeSpecies.Rabbits);
            Assert.IsTrue(LastPlaced, "placed by touch through the lid");
        }
    }
}
