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
    /// <summary>#231 phase 2: the strangers kept apart live in the box. Each apart option answered through the debug cheats makes a group
    /// that <see cref="ApartGroupsView"/> draws at its spot (the far woods camp, the shore hut): its people as dolls going through their day
    /// round the camp, a lit fire, clear of lots and paths. Then years pass and they die: the dolls go, the fire is cold, and nothing is
    /// left behind. Screenshots at default zoom, day and dusk, 1170x2532 and 750x1334: <c>231-*</c>.</summary>
    [Category("ApartGroups")]
    public class ApartGroupsPlayTests : PlayTestBase
    {
        const int Seed = 7531;
        static SimCore Sim => GameClock.Instance.ActiveSim;
        static ApartGroupsView View => ApartGroupsView.Instance;
        static BoxCamera Cam => Camera.main.GetComponent<BoxCamera>();

        IEnumerator Open(int w, int h, bool village)
        {
            yield return OpenSlice(w, h, seed: Seed);
            SimLayoutSource.DebugSkipYears(8f);
            yield return null;
            if (village) { SimLayoutSource.DebugNextEra(false); yield return Frames(10); SimLayoutSource.DebugSkipYears(4f); yield return null; }
            Sim.CleanAll();
            Assert.IsNotNull(View, "the apart view is bootstrapped with the box");
        }

        /// <summary>Raises <paramref name="def"/> (later years until it holds), answers the apart option on the card, closes the stack.</summary>
        IEnumerator AnswerApart(string def, string kind)
        {
            Dilemma d = null;
            string line = null;
            for (int i = 0; i < 8 && d == null; i++)
            {
                line = DilemmaCheats.Raise(def);
                d = Sim.OpenDilemma != null && Sim.OpenDilemma.defId == def ? Sim.OpenDilemma : null;
                if (d == null) { SimLayoutSource.DebugSkipYears(1f); yield return null; }
            }
            Assert.IsNotNull(d, def + ": " + line);
            Debug.Log("[Apart] " + DilemmaCheats.AnswerOnCard(1));
            Assert.AreEqual(DilemmaState.Answered, d.state);
            var g = Sim.ApartOf(d);
            Assert.IsNotNull(g, def + ": option 2 keeps them apart");
            Assert.AreEqual(kind, g.kind);
            var m = CardStackView.Instance.Model;
            if (m.Mode == CardStackMode.Open) m.Close();
            Cam.GoHome(0.01f, true);
            yield return Frames(20);
        }

        ApartGroup LastGroup => Sim.ApartGroups[Sim.ApartGroups.Count - 1];

        /// <summary>The group's people are drawn at its spot: every living member a doll near the camp, out by day.</summary>
        void AssertDrawnAtTheSpot(ApartGroup g, string when)
        {
            Assert.IsTrue(View.TryGetCamp(g.id, out var fire, out bool lit, out bool nudged), when + ": the camp is drawn");
            Assert.IsTrue(lit, when + ": their fire burns");
            if (!nudged) Assert.Less((fire - new Vector2(g.x, g.z)).magnitude, 0.05f, when + ": at the group's home spot");
            else Debug.Log($"[Apart] {when}: nudged {(fire - new Vector2(g.x, g.z)).magnitude:0.00} from the home spot");
            Assert.IsTrue(ApartLife.FootprintFree(Sim.Settlement.Map, fire.x, fire.y), when + ": clear of lots, paths and water");
            var drawn = View.DrawnMembers(g.id);
            int alive = g.members.Count(n => n.alive);
            Assert.AreEqual(alive, drawn.Count, when + ": a doll for every living member");
            foreach (var (id, pos, shown) in drawn)
            {
                Assert.IsTrue(shown, when + $": #{id} is out by day");
                Assert.Less((pos - fire).magnitude, 0.9f, when + $": #{id} lives at the camp ({pos} vs {fire})");
            }
            Debug.Log("[Apart] " + when + ": " + ApartGroupsView.DebugStatus());
        }

        IEnumerator Shots(string name, int w, int h)
        {
            ShowMeDirector.BubblesOn = false; // a villager's chatter bubble must not stand over the camp in the shot
            Cam.GoHome(0.01f, true);
            PeopleView.DebugSetHour(10f);
            yield return new WaitForSecondsRealtime(2.5f); // they walk to their day's places
            yield return Shot($"231-{name}-day-{w}x{h}");
            PeopleView.DebugSetHour(19.2f);
            yield return new WaitForSecondsRealtime(2.5f);
            yield return Shot($"231-{name}-dusk-{w}x{h}");
            ApartGroupsView.DebugFly(-1, 0.4f);
            yield return new WaitForSecondsRealtime(1.2f);
            yield return Shot($"231-{name}-dusk-close-{w}x{h}");
            PeopleView.DebugSetHour(10f);
            yield return new WaitForSecondsRealtime(2.5f);
            yield return Shot($"231-{name}-day-close-{w}x{h}");
            Cam.GoHome(0.01f, true);
            yield return Frames(4);
        }

        [UnityTest, Timeout(300000)]
        public IEnumerator TheTravellersSentOnCampInTheFarWoods()
        {
            yield return Open(1170, 2532, false);
            yield return AnswerApart("camp_travellers", ApartKind.WoodsCamp);
            var g = LastGroup;
            PeopleView.DebugSetHour(10f);
            yield return new WaitForSecondsRealtime(2f);
            AssertDrawnAtTheSpot(g, "woods camp");
            yield return Shots("woods", 1170, 2532);
            AssertNoErrors("woods camp");
        }

        [UnityTest, Timeout(300000)]
        public IEnumerator TheHillFamilyLivesAcrossThePond()
        {
            yield return Open(1170, 2532, true);
            yield return AnswerApart("village_hill_family", ApartKind.ShoreHut);
            var g = LastGroup;
            PeopleView.DebugSetHour(10f);
            yield return new WaitForSecondsRealtime(2f);
            AssertDrawnAtTheSpot(g, "shore hut");
            yield return Shots("shore", 1170, 2532);
            AssertNoErrors("shore hut");
        }

        [UnityTest, Timeout(420000)]
        public IEnumerator BothCampsOnASmallPhone()
        {
            yield return Open(750, 1334, false);
            yield return AnswerApart("camp_travellers", ApartKind.WoodsCamp);
            AssertDrawnAtTheSpot(LastGroup, "woods camp, small phone");
            yield return Shots("woods", 750, 1334);
            SimLayoutSource.DebugNextEra(false);
            yield return Frames(10);
            SimLayoutSource.DebugSkipYears(4f);
            yield return null;
            Sim.CleanAll();
            yield return AnswerApart("village_hill_family", ApartKind.ShoreHut);
            PeopleView.DebugSetHour(10f);
            yield return new WaitForSecondsRealtime(2f);
            AssertDrawnAtTheSpot(LastGroup, "shore hut, small phone");
            yield return Shots("shore", 750, 1334);
            AssertNoErrors("small phone");
        }

        static int ApartObjects() => Resources.FindObjectsOfTypeAll<Transform>().Count(t => t != null && t.name.StartsWith("Apart") && t.gameObject.scene.IsValid());
        static int ApartMeshes() => Resources.FindObjectsOfTypeAll<Mesh>().Count(m => m != null && m.name.StartsWith("Apart"));
        static int DollMeshes() => Resources.FindObjectsOfTypeAll<Mesh>().Count(m => m != null && (m.name == "DollBody" || m.name == "DollArm"));
        static List<string> RootNames() => UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects().Select(g => g.name).OrderBy(n => n).ToList();

        [UnityTest, Timeout(300000)]
        public IEnumerator WhenTheyDieTheCampGoesColdAndNothingLeaks()
        {
            yield return Open(1170, 2532, false);
            var roots = RootNames();
            int meshesBefore = ApartMeshes();
            Assert.AreEqual(0, ApartObjects(), "no camp before the answer");
            yield return AnswerApart("camp_travellers", ApartKind.WoodsCamp);
            var g = LastGroup;
            PeopleView.DebugSetHour(10f);
            yield return new WaitForSecondsRealtime(1.5f);
            AssertDrawnAtTheSpot(g, "before");
            int dolls = DollMeshes();
            var theirMeshes = View.Dolls(g.id).SelectMany(v => new[] { v.body.GetComponent<MeshFilter>().sharedMesh, v.armL.GetComponent<MeshFilter>().sharedMesh }).ToList();
            Assert.AreEqual(g.members.Count * 2, theirMeshes.Count(x => x != null), "a body and arms each");
            // Years pass and their time comes (old age, as the sim's ApartStep has it).
            foreach (var n in g.members) n.lifespan = n.age + 0.5f;
            Debug.Log("[Apart] " + SimLayoutSource.DebugSkipYears(2f));
            yield return Frames(6);
            Assert.IsFalse(g.AnyAlive, "they died");
            Assert.IsTrue(View.TryGetCamp(g.id, out _, out bool lit, out _), "the camp stays");
            Assert.IsFalse(lit, "its fire is cold");
            Assert.AreEqual(0, View.DrawnMembers(g.id).Count, "the dead are not drawn");
            Assert.AreEqual(0, Resources.FindObjectsOfTypeAll<PegDollView>().Count(v => v != null && v.name.StartsWith("Apart")), "no doll of theirs is left");
            yield return Frames(2);
            Assert.IsTrue(theirMeshes.All(x => x == null), "their doll meshes went with them");
            Assert.AreEqual(View.MeshCount, ApartMeshes() - meshesBefore, "the camp holds no stale meshes (the lit fire's went)");
            var now = RootNames();
            var added = now.ToList(); foreach (var n in roots) added.Remove(n);
            Debug.Log($"[Apart] sanity: roots {roots.Count} -> {now.Count} (new: {string.Join(", ", added)}), apart objects {ApartObjects()}, apart meshes {ApartMeshes()}, doll meshes {dolls} -> {DollMeshes()}");
            Assert.IsFalse(added.Any(n => n.StartsWith("Apart")), "no camp root at the scene's top");
            Assert.LessOrEqual(added.Count, 1, "new roots: " + string.Join(", ", added));
            yield return Shot("231-cold-camp-1170x2532");
            AssertNoErrors("cold camp");
        }

        public override void TearDown()
        {
            ChoiceSceneView.DebugWatch = false;
            ShowMeDirector.BubblesOn = true;
            base.TearDown();
        }
    }
}
