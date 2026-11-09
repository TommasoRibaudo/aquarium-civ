using System.Collections;
using System.Linq;
using AquariumCiv.Game;
using AquariumCiv.GameUI;
using AquariumCiv.Life;
using AquariumCiv.Sim;
using AquariumCiv.Sim.Eco;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AquariumCiv.View.PlayTests
{
    /// <summary>
    /// P9 #427 (animation-specs B14): dawn at the pond (the does drink at first light; the people near stop and watch, a kid holds still, a
    /// grown-up says "shh"; nobody hunts at the water) and the white stag's choice (it stands at the wood's edge while the choice waits;
    /// "hunt it" sends the hunters after it, the herd untouched, the great antlers; "let it be" sends it back into the trees and it shows
    /// at dawn after). Shots at phone size: Temp/agent/p9-stag-*.png, p9-dawn-*.png.
    /// </summary>
    [Category("DeerHunt")]
    public class DeerScenesPlayTests : PlayTestBase
    {
        static SimCore Sim => GameClock.Instance.ActiveSim;
        static BoxCamera Cam => Camera.main.GetComponent<BoxCamera>();
        static BoxView Box => UnityEngine.Object.FindAnyObjectByType<BoxView>();
        static ViewTime Now => PeopleView.Find().Now;

        IEnumerator DeerLand()
        {
            UseLivingLand(true);
            GameUiPanel.AcceptInputUnfocused(true);
            yield return OpenSlice(1179, 2556, seed: 4242);
            GameUiPanel.AcceptInputUnfocused(true);
            Assert.IsTrue(Sim.EcologyOn, "a living-land box");
            PeopleView.DebugSetHour(9f);
            foreach (var (x, z) in new[] { (-1.6f, 0.6f), (-1.0f, -0.8f), (-2.0f, -0.2f), (1.6f, 0.9f), (0.8f, -0.9f) }) EcoDebug.MistBig(x, z);
            EcoDebug.Table(0.3f);
            Sim.DebugGrowFarWood();
            yield return Frames(20);
            Assert.GreaterOrEqual(Sim.DebugPlaceNearCamp(LifeSpecies.Deer), 0, "deer placed");
            yield return Until(() => !LifeArrivalView.Instance.Playing(LifeSpecies.Deer), 30f);
            Sim.DebugDeerToCap();
            DilemmaCheats.Toggle(true); // the Keeper's choices on (the white stag is one)
            for (int calm = 0, f = 0; calm < 60 && f < 900; f++) { calm = StagedMoments.StopAll() == 0 && StagedMoments.Playing == 0 ? calm + 1 : 0; yield return null; }
            PeopleView.Find().Planner.ClearLive();
            yield return new WaitForSecondsRealtime(2f);
        }

        static IEnumerator LookAt(Vector3 local, float zoom)
        {
            Cam.FlyTo(Box.transform.TransformPoint(local), zoom, 0f);
            yield return Frames(3);
        }

        static Dilemma OpenStag() => Sim.State.dilemmas.LastOrDefault(d => d.defId == "small_" + LifeChoices.WhiteStag && d.state == DilemmaState.Open);

        [UnityTest, Timeout(600000)]
        public IEnumerator Dawn_TheDoesDrink_ThePeopleWatch_AKidHoldsStill()
        {
            yield return DeerLand();
            var dsv = DeerScenesView.Instance; var dv = DeerView.Instance; var pv = PeopleView.Find();
            Assert.IsNotNull(dsv, "the deer scenes view");
            Assert.IsTrue(dv.Field.HasPond, "a pond's edge for the dawn drink");
            pv.timeScale = 0.35f; // first light is short: watch it slowly
            Debug.Log("[p9-dawn] " + dsv.DebugDawnNow());
            var drinking = new System.Collections.Generic.List<Vector3>();
            yield return Until(() => { dv.DrinkingNow(drinking); return drinking.Count > 0; }, 40f);
            Assert.Greater(drinking.Count, 0, "the does drink at first light: " + dv.Status());
            yield return LookAt(drinking[0], 0.24f);
            yield return Until(() => dsv.DawnPlaying, 15f);
            Debug.Log("[p9-dawn] " + dsv.Status());
            Assert.IsTrue(dsv.DawnPlaying && dsv.DawnWatchers > 0, "the people near stop and watch: " + dsv.Status());
            yield return new WaitForSecondsRealtime(DeerScenesView.ShhAt + 0.5f);
            yield return Shot("p9-dawn-1-watch");
            // nobody hunts at the water: no deer hunt plan targets a deer within 0.5 u of it
            foreach (var p in pv.Planner.DeerHuntsOn(Now.day)) Assert.IsFalse(pv.Planner.DeerSpotAtWater(p.targetX, p.targetZ), "a hunt at the water");
            yield return new WaitForSecondsRealtime(6f);
            yield return Shot("p9-dawn-2-still");
            AssertNoErrors("dawn at the pond");
        }

        [UnityTest, Timeout(900000)]
        public IEnumerator WhiteStag_Asked_HuntIt_TheHuntersGo_TheHerdUntouched()
        {
            yield return DeerLand();
            var dsv = DeerScenesView.Instance; var stage = DeerHuntStage.Instance;
            int herd = Sim.EcoDeerCount;
            string said = dsv.DebugWhiteStagNow();
            Debug.Log("[p9-stag] " + said);
            Assert.AreEqual(WhiteStag.Seen, Sim.WhiteStagState);
            var d = OpenStag();
            Assert.IsNotNull(d, "the choice is asked: " + said);
            yield return Until(() => dsv.WhiteShown, 5f);
            Assert.IsTrue(dsv.WhiteShown, "it stands at the wood's edge while the choice waits");
            float fx = Sim.EcoGround.campX, fz = Sim.EcoGround.campZ;
            Assert.GreaterOrEqual(new Vector2(dsv.WhiteAt.x - fx, dsv.WhiteAt.z - fz).magnitude, 0.95f, "out at the wood's edge, not in the camp");
            yield return LookAt(dsv.WhiteAt, 0.24f);
            yield return new WaitForSecondsRealtime(2f);
            yield return Shot("p9-stag-1-edge");
            // the card's words
            StringAssert.Contains("white stag", LifeChoices.WhiteStagDef.question);
            Assert.IsTrue(Sim.AnswerDilemma(d.id, 0, DilemmaPath.Card), "hunt it");
            Assert.AreEqual(WhiteStag.Hunted, Sim.WhiteStagState);
            yield return Until(() => dsv.WhiteHunt != null, 10f);
            var p = dsv.WhiteHunt;
            Assert.IsNotNull(p, "the hunters go after it: " + dsv.Status());
            Assert.IsTrue(p.white && p.playsHit && p.HasScene);
            yield return Until(() => Now.hour >= p.shootStart, 150f);
            yield return LookAt(Box.land.OnGround((p.shotX + p.targetX) * 0.5f, (p.shotZ + p.targetZ) * 0.5f), 0.2f);
            yield return new WaitForSecondsRealtime(0.8f);
            yield return Shot("p9-stag-2-aim");
            yield return Until(() => Now.hour >= p.landHour, 30f);
            yield return Frames(5);
            Assert.IsTrue(p.caught, "the white stag's hunt settles");
            Assert.IsFalse(dsv.WhiteShown, "the stage has it now");
            Assert.IsTrue(stage.TryGetCarcass(out _, out _), "it lies where it fell");
            Assert.AreEqual(herd, Sim.EcoDeerCount, "the white stag was never one of the herd");
            yield return new WaitForSecondsRealtime(1.5f);
            yield return Shot("p9-stag-3-down");
            Assert.IsTrue(Sim.WhiteStagAntlers, "the great antlers");
            AssertNoErrors("white stag hunted");
        }

        [UnityTest, Timeout(600000)]
        public IEnumerator WhiteStag_LetItBe_BackIntoTheTrees_ThenAtDawn()
        {
            yield return DeerLand();
            var dsv = DeerScenesView.Instance; var pv = PeopleView.Find();
            dsv.DebugWhiteStagNow();
            var d = OpenStag();
            Assert.IsNotNull(d, "asked");
            yield return Until(() => dsv.WhiteShown, 5f);
            Assert.IsTrue(Sim.AnswerDilemma(d.id, 1, DilemmaPath.Card), "let it be");
            Assert.AreEqual(WhiteStag.LetBe, Sim.WhiteStagState);
            Assert.IsTrue(Sim.WhiteStagAtDawn);
            yield return Frames(3);
            Assert.AreEqual(1, dsv.WhiteLetGo, "it turns and walks back into the trees");
            yield return LookAt(dsv.WhiteAt, 0.26f);
            yield return new WaitForSecondsRealtime(3f);
            yield return Shot("p9-stag-letbe-1-leaving");
            yield return Until(() => !dsv.WhiteShown, 20f);
            Assert.IsFalse(dsv.WhiteShown, "gone into the wood");
            Assert.IsNull(dsv.WhiteHunt, "never a target");
            // a later dawn: it shows at the back
            pv.timeScale = 0.35f;
            Debug.Log("[p9-stag] " + dsv.DebugWhiteDawnNow());
            yield return Until(() => dsv.WhiteShown, 10f);
            Assert.IsTrue(dsv.WhiteShown, "at dawn it comes back, at the back");
            foreach (var p in pv.Planner.DeerHuntsOn(Now.day)) Assert.IsFalse(p.white, "never a target");
            yield return LookAt(dsv.WhiteAt, 0.3f);
            yield return new WaitForSecondsRealtime(2f);
            yield return Shot("p9-stag-letbe-2-dawn");
            AssertNoErrors("white stag let be");
        }

        [UnityTest, Timeout(300000)]
        public IEnumerator WhiteStag_LetThemDecide_TheChiefPicks()
        {
            yield return DeerLand();
            var dsv = DeerScenesView.Instance;
            dsv.DebugWhiteStagNow();
            var d = OpenStag();
            Assert.IsNotNull(d, "asked");
            Assert.IsTrue(Sim.LetThemDecide(d.id), "let them decide");
            Assert.AreNotEqual(WhiteStag.Seen, Sim.WhiteStagState, "the people answered");
            Debug.Log($"[p9-stag] the people chose: {Sim.WhiteStagState}");
            yield return Frames(5);
            AssertNoErrors("white stag let them decide");
        }
    }
}
