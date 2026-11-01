using System.Collections;
using System.Linq;
using AquariumCiv.Game;
using AquariumCiv.GameUI;
using AquariumCiv.Sim;
using AquariumCiv.View.ShowMe;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AquariumCiv.View.PlayTests
{
    /// <summary>#198: box 2 starts the short run, and the finished box shows its town on the shelf, its plinths without the pink dome.</summary>
    [Category("BoxTwo")]
    public class BoxTwoPlayTests : PlayTestBase
    {
        static GameClock Clock => GameClock.Instance;

        [UnityTest, Timeout(180000)]
        public IEnumerator BoxTwoIsShortAndTheShelfShowsTheTown()
        {
            yield return OpenSlice(1080, 1920, 1234);
            ShowMeDirector.BubblesOn = false;
            MomentPlayer.DebugGrowVillage(30f);
            yield return Frames(3);
            Clock.ActiveSim.ForceNextEra();
            yield return Frames(3);
            SimLayoutSource.DebugSkipYears(30f);
            yield return Frames(3);
            foreach (var id in RelicId.All) Clock.ActiveSim.DebugGiveRelic(id);
            yield return Frames(3);
            var town = Clock.ActiveSim.State.buildings.Count;
            Assert.That(BoxCompleteController.DebugComplete(), Does.StartWith("box completed"));
            yield return Frames(8);
            var first = FirstRunView.Instance;
            Assert.AreEqual(1, Clock.ActiveSim.State.boxIndex);
            Assert.IsTrue(first.Active, "box 2 has its run");
            Assert.IsTrue(Clock.ActiveSim.State.firstRun.shortRun, "the short one");
            var steps = new System.Collections.Generic.List<FirstRunStep>();
            first.Run.StepChanged += steps.Add;
            for (int i = 0; i < 12 && first.Active; i++) { first.Run.SkipStep(); yield return Frames(2); }
            Assert.IsFalse(first.Active, "it ends");
            Assert.IsFalse(steps.Contains(FirstRunStep.Name) || steps.Contains(FirstRunStep.Year) || steps.Contains(FirstRunStep.Card), string.Join(",", steps));

            var c = BoxCompleteController.Instance;
            Assert.IsTrue(c.OpenShelf(), "the shelf");
            yield return new WaitForSecondsRealtime(1.5f);
            var shelf = Object.FindAnyObjectByType<ShelfProps>();
            Assert.GreaterOrEqual(shelf.ProxyCount, 1);
            var mesh = shelf.GetComponentsInChildren<MeshFilter>(true).FirstOrDefault(m => m.name == "Town");
            Assert.IsNotNull(mesh, "the museum box carries its town");
            Debug.Log($"[box2] town of {town} buildings: {mesh.sharedMesh.vertexCount} verts");
            Assert.Greater(mesh.sharedMesh.vertexCount, 500);
            yield return Shot("bundle2-198-shelf");
            var cam = Camera.main.GetComponent<BoxCamera>();
            var plinth = shelf.GetComponentsInChildren<Transform>(true).First(t => t.name.StartsWith("Plinth_"));
            Debug.Log($"[box2] plinths {string.Join(",", shelf.GetComponentsInChildren<Transform>(true).Where(t => t.name.StartsWith("Plinth_")).Select(t => t.name))}");
            var look = Camera.main.transform;
            look.position = plinth.parent.TransformPoint(new Vector3(0f, 2.4f, -7.5f));
            look.LookAt(plinth.position + Vector3.up * 0.4f);
            yield return null;
            yield return Shot("bundle2-198-plinths");
            AssertNoErrors("box two");
        }
    }
}
