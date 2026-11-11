using System.Collections;
using System.Collections.Generic;
using System.Linq;
using AquariumCiv.Game;
using AquariumCiv.Life;
using AquariumCiv.Sim;
using AquariumCiv.View.Cards;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AquariumCiv.View.PlayTests
{
    /// <summary>#578 (the travellers), modelled on <see cref="ApartGroupsPlayTests"/>: the camp's cutter carries a log from the cutting spot to the woodpile
    /// (the sim counts the load, the first one writes its line once, the pile of logs grows by one), and when they join (the panel's "Travellers join
    /// now", as the era moment would) they pack up and walk to the green, become villagers, and their camp is cleared: no "Apart" object, mesh or doll
    /// is left and no root is added to the scene. Screenshots <c>578-*</c> at 1170x2532.</summary>
    [Category("Travellers")]
    public class TravellersPlayTests : PlayTestBase
    {
        const int Seed = 7531;
        static SimCore Sim => GameClock.Instance.ActiveSim;
        static ApartGroupsView View => ApartGroupsView.Instance;
        static BoxCamera Cam => Camera.main.GetComponent<BoxCamera>();

        IEnumerator OpenWithTravellers()
        {
            yield return OpenSlice(1170, 2532, seed: Seed);
            SimLayoutSource.DebugSkipYears(8f);
            yield return null;
            Sim.CleanAll();
            Dilemma d = null;
            for (int i = 0; i < 8 && d == null; i++)
            {
                DilemmaCheats.Raise("camp_travellers");
                d = Sim.OpenDilemma != null && Sim.OpenDilemma.defId == "camp_travellers" ? Sim.OpenDilemma : null;
                if (d == null) { SimLayoutSource.DebugSkipYears(1f); yield return null; }
            }
            Assert.IsNotNull(d, "camp_travellers held");
            Debug.Log("[Travellers] " + Sim.QuestionOf(d) + " -> " + DilemmaCheats.AnswerOnCard(1));
            var g = Sim.ApartOf(d);
            Assert.IsNotNull(g, "full packs keeps them apart");
            Assert.IsTrue(g.joins, "the travellers join at the village");
            var m = CardStackView.Instance.Model;
            if (m.Mode == CardStackMode.Open) m.Close();
            Cam.GoHome(0.01f, true);
            PeopleView.DebugSetHour(10f);
            yield return Frames(20);
        }

        static ApartGroup Group => Sim.ApartGroups.Count > 0 ? Sim.ApartGroups[Sim.ApartGroups.Count - 1] : null;
        static int ApartObjects() => Resources.FindObjectsOfTypeAll<Transform>().Count(t => t != null && t.name.StartsWith("Apart") && t.gameObject.scene.IsValid());
        static int ApartMeshes() => Resources.FindObjectsOfTypeAll<Mesh>().Count(m => m != null && m.name.StartsWith("Apart"));
        static List<string> RootNames() => UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects().Select(g => g.name).OrderBy(n => n).ToList();

        [UnityTest, Timeout(300000)]
        public IEnumerator TheCutterCarriesALogToThePileAndThePileGrows()
        {
            yield return OpenWithTravellers();
            var g = Group;
            int before = g.woodLoads;
            Assert.AreEqual(0, before);
            Debug.Log("[Travellers] " + ApartGroupsView.DebugWoodNow());
            ApartGroupsView.DebugFly(g.id, 0.4f);
            yield return new WaitForSecondsRealtime(1.0f);
            yield return Shot("578-carry-lift-1170x2532");
            float t = 0f;
            while (g.woodLoads == before && t < 20f) { t += Time.unscaledDeltaTime; yield return null; }
            Assert.AreEqual(before + 1, g.woodLoads, "the load reached the pile (the cutter set it down)");
            yield return new WaitForSecondsRealtime(0.5f);
            yield return Shot("578-carry-pile-1170x2532");
            Assert.AreEqual(1, Sim.State.chronicle.Count(e => e.text.Contains("first full load")), "the first load is told, once");
            Assert.AreEqual(SimCore.FirstLoadLine, View.LastCaption, "and seen: the carrier says it on the caption");
            var camp = GameObject.Find("ApartCamp" + g.id);
            Assert.IsNotNull(camp, "the camp is drawn");
            Assert.IsNotNull(camp.transform.Find("PileLogs"), "a log on the pile for the load");
            Debug.Log("[Travellers] " + ApartGroupsView.DebugStatus());
            AssertNoErrors("carry");
        }

        [UnityTest, Timeout(300000)]
        public IEnumerator TheyPackUpWalkInAndTheCampIsCleared()
        {
            var roots = RootNames();
            int meshes0 = ApartMeshes();
            yield return OpenWithTravellers();
            var g = Group;
            var people = g.members.ToList();
            int id = g.id, pop = Sim.State.population;
            Assert.Greater(ApartObjects(), 0, "the camp is drawn before");
            // The Keeper is busy (a question is open): they are due, but the camp is not just gone, nor does the walk start under a card.
            DilemmaCheats.Raise("camp_fire");
            Assume.That(Sim.OpenDilemma, Is.Not.Null, "a camp question to keep the Keeper busy");
            Debug.Log("[Travellers] " + ApartGroupsView.DebugJoinNow());
            Assert.AreEqual(1, g.joinState, "due");
            yield return new WaitForSecondsRealtime(5f);
            Assert.AreEqual(1, g.joinState, "it waits while the Keeper is busy");
            Assert.Greater(ApartObjects(), 0, "the camp stands");
            Assert.AreEqual(0, View.JoinsPlayed);
            // the visit goes calm: the walk begins and the camera leans to them
            Debug.Log("[Travellers] " + DilemmaCheats.AnswerOnCard(0));
            var cm = CardStackView.Instance.Model;
            if (cm.Mode == CardStackMode.Open) cm.Close();
            Cam.GoHome(0.01f, true);
            yield return Frames(10);
            float w = 0f;
            while (g.joinState == 1 && w < 90f) { w += Time.unscaledDeltaTime; yield return null; }
            var ch = ChoiceSceneView.Instance;
            Assert.AreEqual(2, g.joinState, $"calm: they pack up and walk (catching={Sim.CatchingUp} firstRun={Sim.State.firstRun.active} camFree={GlassVoice.CameraFree()} moments={Sim.PlayingMoments().Count} beforeChoice={Sim.MomentBeforeChoice()} staged={StagedMoments.Playing} choiceStage={(ch != null && ch.Stage != null)} open={Sim.OpenDilemma?.defId})");
            yield return new WaitForSecondsRealtime(1.2f);
            Assert.GreaterOrEqual(View.JoinLeans, 1, "the camera leaned to the camp");
            yield return Shot("578-join-pack-1170x2532");
            yield return new WaitForSecondsRealtime(3.0f);
            yield return Shot("578-join-walk-1170x2532");
            float t = 0f;
            while (Sim.ApartGroups.Any(x => x.id == id) && t < 40f) { t += Time.unscaledDeltaTime; yield return null; }
            Assert.IsFalse(Sim.ApartGroups.Any(x => x.id == id), "they have walked in: the apart group is gone");
            Assert.IsTrue(people.All(n => Sim.State.notables.Contains(n)), "they are the village's notables now");
            Assert.AreEqual(pop + people.Count(n => n.alive), Sim.State.population, "and counted");
            Assert.AreEqual(1, Sim.State.chronicle.Count(e => e.text.Contains("walked in to the green")), "one line");
            Assert.AreEqual(ApartGroupsView.ArrivalLine, View.LastCaption, "and a caption when they reach the green");
            Assert.AreEqual(1, View.JoinsPlayed);
            yield return Frames(6);
            yield return new WaitForSecondsRealtime(0.5f);
            Assert.AreEqual(0, ApartObjects(), "no camp root, no doll left");
            Assert.AreEqual(0, Resources.FindObjectsOfTypeAll<PegDollView>().Count(v => v != null && v.name.StartsWith("Apart")), "no doll of theirs");
            Assert.LessOrEqual(ApartMeshes(), meshes0, "no stale mesh (the count before may include meshes the previous test left, which this clears)");
            var now = RootNames();
            var added = now.ToList(); foreach (var n in roots) added.Remove(n);
            added.RemoveAll(n => n == "ChatterMarks" || n == "RitualBubbles");
            Assert.IsFalse(added.Any(n => n.StartsWith("Apart")), "no Apart root at the scene's top: " + string.Join(", ", added));
            yield return Shot("578-join-cleared-1170x2532");
            AssertNoErrors("join");
        }
    }
}
