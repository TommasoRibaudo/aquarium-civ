using System.Collections;
using System.Linq;
using AquariumCiv.Game;
using AquariumCiv.Sim;
using AquariumCiv.View.Cards;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AquariumCiv.View.PlayTests
{
    /// <summary>#404 in the real Slice scene: the shore family's boat goes out and comes back, and their lookout tower goes up in three stages,
    /// its lamp lit at dusk by someone who climbs up, with a lookout on the platform by day. Shots: Temp/agent/tower-*.png.
    /// Run alone: <c>--filter AquariumCiv.View.PlayTests.ApartTowerPlayTests --filter_type testName</c>.</summary>
    [Category("ApartTower")]
    public class ApartTowerPlayTests : PlayTestBase
    {
        const int Seed = 7531;
        const float BoatLaunchAt = ApartGroupsView.BoatLaunch;
        static SimCore Sim => GameClock.Instance.ActiveSim;
        static ApartGroupsView View => ApartGroupsView.Instance;
        static BoxCamera Cam => Camera.main.GetComponent<BoxCamera>();

        /// <summary>A village with the shore family settled apart, then the town.</summary>
        IEnumerator ShoreFamilyInTheTown(bool living = false)
        {
            if (living) UseLivingLand();
            yield return OpenSlice(seed: Seed);
            if (living) { EcoDebug.Table(0.3f); yield return Frames(10); } // a pond with water, before the family picks its shore
            SimLayoutSource.DebugSkipYears(8f);
            yield return null;
            SimLayoutSource.DebugNextEra(false);
            yield return Frames(10);
            SimLayoutSource.DebugSkipYears(4f);
            yield return null;
            Sim.CleanAll();
            Dilemma d = null;
            for (int i = 0; i < 8 && d == null; i++)
            {
                DilemmaCheats.Raise("village_hill_family");
                d = Sim.OpenDilemma != null && Sim.OpenDilemma.defId == "village_hill_family" ? Sim.OpenDilemma : null;
                if (d == null) { SimLayoutSource.DebugSkipYears(1f); yield return null; }
            }
            Assert.IsNotNull(d, "the pond neighbours' dilemma");
            DilemmaCheats.AnswerOnCard(1);
            var m = CardStackView.Instance.Model;
            if (m.Mode == CardStackMode.Open) m.Close();
            SimLayoutSource.DebugNextEra(false);
            yield return Frames(20);
            Assert.AreEqual(Era.Town, Sim.State.era, "the town");
            Sim.CleanAll();
            Assert.IsNotNull(Sim.ApartGroups.FirstOrDefault(g => g.kind == ApartKind.ShoreHut), "the shore family lives apart");
        }

        ApartGroup Shore => Sim.ApartGroups.First(g => g.kind == ApartKind.ShoreHut);

        /// <summary>The #404 tester saw people jump in one frame (into the boat, at the top of a climb, to a far ladder): the longest step
        /// any of the group took in one frame, and where.</summary>
        readonly System.Collections.Generic.Dictionary<int, Vector2> lastPos = new();
        float worstStep; string worstAt = "";
        void WatchSteps(ApartGroup g, float hour)
        {
            foreach (var x in View.MembersNow(g.id))
            {
                if (lastPos.TryGetValue(x.id, out var was))
                {
                    float d = (x.pos - was).magnitude;
                    if (d > worstStep) { worstStep = d; worstAt = $"#{x.id} at {hour:0.00} h: {d:0.000} ({was} -> {x.pos})"; }
                }
                lastPos[x.id] = x.pos;
            }
        }

        IEnumerator Look(string shot, float zoom = 0.45f, bool tower = false)
        {
            Cam.enabled = true;
            if (tower) ApartGroupsView.DebugFlyTower(zoom); else ApartGroupsView.DebugFly(Shore.id, zoom);
            yield return new WaitForSecondsRealtime(1f);
            yield return Shot(shot);
        }

        /// <summary>Runs the view clock from <paramref name="from"/> fast until <paramref name="until"/>, calling <paramref name="each"/> every frame.</summary>
        static IEnumerator RunHours(float from, float until, System.Action<float> each, float speed = 12f, float budget = 30f)
        {
            PeopleView.DebugSetHour(from);
            PeopleView.DebugSpeed(speed);
            float t0 = Time.realtimeSinceStartup;
            var pv = PeopleView.Find();
            while (pv.ViewHour < until && pv.ViewHour >= from - 0.01f && Time.realtimeSinceStartup - t0 < budget)
            {
                each?.Invoke(pv.ViewHour);
                yield return null;
            }
            PeopleView.DebugSpeed(1f);
        }

        [UnityTest, Timeout(600000)]
        public IEnumerator TheTowerGoesUp_AndItsLampIsLitAtDuskBySomeoneWhoClimbs()
        {
            yield return ShoreFamilyInTheTown();
            var g = Shore;
            Debug.Log("[tower] " + Sim.DebugTowerBegin());
            PeopleView.DebugSetHour(10f);
            yield return Frames(10);
            Assert.IsTrue(View.TryGetTower(g.id, out int stage, out var at, out _), "the tower is drawn");
            Assert.AreEqual(1, stage, "the frame first");
            Assert.Less((at - new Vector2(g.towerX, g.towerZ)).magnitude, 0.01f, "where the sim put it");
            Assert.IsTrue(View.MembersNow(g.id).Any(x => x.scripted), "they work at it by day");
            yield return Look("tower-1-frame", 0.3f, true);
            Sim.DebugTowerNextStage();
            yield return Frames(5);
            View.TryGetTower(g.id, out stage, out _, out _);
            Assert.AreEqual(2, stage, "the platform and ladder");
            yield return Look("tower-2-platform", 0.3f, true);
            Sim.DebugTowerNextStage();
            yield return Frames(5);
            View.TryGetTower(g.id, out stage, out _, out bool lamp);
            Assert.AreEqual(3, stage, "the roof and lamp");
            Assert.IsFalse(lamp, "the lamp is dark by day");
            Assert.IsTrue(Sim.State.chronicle.Any(e => e.kind == "gift"), "the chronicle has the gift");

            // dusk: someone climbs up, the lamp catches, they come down
            float topLift = 0f; bool darkBefore = true, litAfter = false; bool shotUp = false;
            yield return RunHours(18.1f, 19.3f, h =>
            {
                View.TryGetTower(g.id, out _, out _, out bool on);
                if (h < ApartGroupsView.LightOn - 0.02f && on) darkBefore = false;
                if (h > ApartGroupsView.LightOn + 0.05f && on) litAfter = true;
                foreach (var x in View.MembersNow(g.id)) topLift = Mathf.Max(topLift, x.lift);
                WatchSteps(g, h);
            }, speed: 4f, budget: 60f);
            Debug.Log("[tower] lamp: longest one-frame step " + worstAt);
            Assert.Less(worstStep, 0.06f, "nobody jumps (dusk lamp): " + worstAt);
            Assert.IsTrue(darkBefore, "dark before it's lit");
            Assert.IsTrue(litAfter, "lit after");
            Assert.Greater(topLift, ApartProps.TowerPlatformY - 0.02f, "the lighter stood on the platform");
            Assert.IsTrue(View.MembersNow(g.id).All(x => x.lift < 0.01f), "and came down again");
            PeopleView.DebugSetHour(18.7f);
            PeopleView.DebugSpeed(0f);
            yield return Frames(5);
            yield return Look("tower-3-lighting", 0.25f, true);
            PeopleView.DebugSetHour(21f);
            yield return Frames(5);
            yield return Look("tower-4-night", 0.3f, true);
            PeopleView.DebugSpeed(1f);

            // the lookout by day
            PeopleView.DebugSetHour(10f);
            PeopleView.DebugSpeed(0f);
            yield return Frames(10);
            Assert.IsTrue(View.MembersNow(g.id).Any(x => x.lift > ApartProps.TowerPlatformY - 0.02f), "someone keeps a lookout from the platform");
            yield return Look("tower-5-lookout", 0.25f, true);
            PeopleView.DebugSpeed(1f);
            Assert.LessOrEqual(View.MeshCount, 60, "no meshes pile up: " + View.MeshCount);
            AssertNoErrors("tower");
        }

        [UnityTest, Timeout(600000)]
        public IEnumerator TownsfolkClimbItInTheEvening_AndATapOpensItsCard()
        {
            yield return ShoreFamilyInTheTown();
            Sim.DebugTowerBegin(); Sim.DebugTowerNextStage(); Sim.DebugTowerNextStage();
            SimLayoutSource.DebugSkipYears(2f); // past the new town's festival evenings
            yield return Frames(10);
            Sim.CleanAll();
            Assert.IsTrue(Sim.TowerStands);
            AquariumCiv.Life.LifePlanner.DebugTowerEvenings = true;
            var pv = PeopleView.Find();
            pv.Planner?.Invalidate();
            var up = new System.Collections.Generic.List<AquariumCiv.Life.PersonRef>();
            int most = 0;
            try
            {
                yield return RunHours(18.2f, 19.5f, h => { pv.TowerClimbers(up); most = Mathf.Max(most, up.Count); }, speed: 3f, budget: 45f);
                Debug.Log($"[tower] most townsfolk up the tower at once: {most}");
                PeopleView.DebugSetHour(18.9f);
                yield return Frames(3);
                Debug.Log("[tower] at 18:54: " + PeopleView.DebugReport(0).Replace('\n', '|'));
                Assert.Greater(most, 0, "townsfolk climb the tower in the evening");
                PeopleView.DebugSetHour(18.9f);
                PeopleView.DebugSpeed(0f);
                yield return new WaitForSecondsRealtime(2f);
                yield return Look("tower-6-townsfolk", 0.3f, true);

                // a tap on the tower opens its card
                Assert.IsTrue(View.TowerForTap(out var at, out float height));
                var box = Object.FindAnyObjectByType<BoxView>();
                var world = box.transform.TransformPoint(new Vector3(at.x, box.GroundHeight(at.x, at.y) + height * 0.5f, at.y));
                var sp = Camera.main.WorldToScreenPoint(world);
                var cards = PersonCardView.Instance;
                Assert.AreEqual(SimCore.TowerCardId, cards.PickBuilding(new Vector2(sp.x, sp.y)), "the tap finds the tower");
                Assert.IsTrue(cards.OpenBuilding(SimCore.TowerCardId));
                Assert.AreEqual("Lookout tower", cards.BuildingCard.title);
                Debug.Log("[tower] card: " + cards.BuildingCard.whatItIs + " · " + cards.BuildingCard.yearLine);
                yield return Frames(5);
                yield return Shot("tower-7-card");
                cards.CloseBuilding();
            }
            finally
            {
                AquariumCiv.Life.LifePlanner.DebugTowerEvenings = false;
                PeopleView.DebugSpeed(1f);
            }
            AssertNoErrors("townsfolk");
        }

        [UnityTest, Timeout(600000)]
        public IEnumerator TheBoatGoesOutWithTwoAndComesBack()
        {
            yield return ShoreFamilyInTheTown(living: true);
            var g = Shore;
            Debug.Log("[tower] boat box: " + ApartGroupsView.DebugStatus() + " · pond cells " + Sim.EcoPondCells);
            ApartGroupsView.DebugBoatEveryDay = true;
            try
            {
                PeopleView.DebugSetHour(7.9f);
                yield return Frames(5);
                if (!View.TryGetBoat(g.id, out var home, out bool afloat)) Assert.Inconclusive("this box's shore family lives off the water (no boat): " + ApartGroupsView.DebugStatus());
                Assert.IsFalse(afloat, "drawn up on the shore at first");
                if (!View.BoatGoesOut(g.id)) Assert.Inconclusive("the pond has no water in this box: " + ApartGroupsView.DebugStatus());
                bool wentOut = false; int mostOnBoat = 0; float farthest = 0f;
                yield return RunHours(7.9f, 11.5f, h =>
                {
                    if (!View.TryGetBoat(g.id, out var b, out bool f)) return;
                    if (f) { wentOut = true; farthest = Mathf.Max(farthest, (b - home).magnitude); }
                    int on = View.MembersNow(g.id).Count(x => x.scripted && x.lift > 0.5f && (x.pos - b).magnitude < 0.1f);
                    mostOnBoat = Mathf.Max(mostOnBoat, on);
                    // the tester saw them waist-deep far out in the pond before the launch: on the way down they stay between the camp and the boat
                    var bx = Object.FindAnyObjectByType<BoxView>();
                    if (h < BoatLaunchAt && !f)
                        foreach (var x in View.MembersNow(g.id).Where(y => y.scripted && (y.pos - home).magnitude > 0.08f))
                            Assert.GreaterOrEqual(bx.land.Height(x.pos.x, x.pos.y), bx.layout.eco.waterY - 0.02f, $"at {h:0.00} h on the shore (at most wading its edge) on the way to the beached boat, not out in the pond ({x.pos} vs boat {home})");
                    WatchSteps(g, h);
                }, speed: 4f, budget: 90f);
                Debug.Log("[tower] boat: longest one-frame step " + worstAt);
                Assert.Less(worstStep, 0.06f, "nobody jumps (the boat): " + worstAt);
                Assert.IsTrue(wentOut, "the boat went out");
                Assert.Greater(farthest, 0.05f, "out on the water");
                Assert.GreaterOrEqual(mostOnBoat, Mathf.Min(2, g.members.Count(n => n.alive)), "two of them in it");
                PeopleView.DebugSetHour(11.6f);
                yield return Frames(5);
                View.TryGetBoat(g.id, out var back, out afloat);
                Assert.IsFalse(afloat, "drawn up again by midday");
                PeopleView.DebugSetHour(9.5f);
                PeopleView.DebugSpeed(0f);
                yield return Frames(10);
                yield return Look("tower-boat-fishing", 0.5f);
                PeopleView.DebugSpeed(1f);
            }
            finally { ApartGroupsView.DebugBoatEveryDay = false; }
            AssertNoErrors("boat");
        }
    }
}
