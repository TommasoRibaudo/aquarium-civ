using System.Collections;
using System.Collections.Generic;
using System.Linq;
using AquariumCiv.Game;
using AquariumCiv.GameUI;
using AquariumCiv.Sim;
using AquariumCiv.Sim.Settle;
using AquariumCiv.View.ShowMe;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AquariumCiv.View.PlayTests
{
    /// <summary>Tester run 7 by touch (#188, #160): by day a roof tap opens the building, a doll in front of it still wins, and a tap on
    /// mess does one thing.</summary>
    [Category("WorldTap"), Category("Bundle3")]
    public class WorldTapPlayTests : PlayTestBase
    {
        const int Seed = 1234;
        static SimCore Sim => GameClock.Instance.ActiveSim;
        static GameHud Hud => GameHud.Instance;
        static PersonCardView Cards => PersonCardView.Instance;
        static BoxCamera Cam => Camera.main.GetComponent<BoxCamera>();
        static BoxView Box => Object.FindAnyObjectByType<BoxView>();

        IEnumerator Village()
        {
            yield return OpenSlice(seed: Seed);
            ShowMeDirector.BubblesOn = false;
            MomentPlayer.DebugGrowVillage(14f);
            yield return Frames(3);
            Sim.CleanAll();
            PeopleView.DebugSetHour(10f);
            yield return new WaitForSecondsRealtime(0.5f);
            yield return Until(() => GameUiPanel.Toasts.Current == null && GameUiPanel.Toasts.Waiting == 0, 6f);
        }

        IEnumerator Town()
        {
            yield return Village();
            MomentPlayer.DebugGrowVillage(30f);
            yield return Frames(3);
            Sim.ForceNextEra();
            yield return Frames(3);
            SimLayoutSource.DebugSkipYears(10f);
            yield return Frames(6);
            Sim.CleanAll();
            PeopleView.DebugSetHour(10f);
            yield return new WaitForSecondsRealtime(0.5f);
            yield return Until(() => GameUiPanel.Toasts.Current == null && GameUiPanel.Toasts.Waiting == 0, 6f);
        }

        static bool Clear(Vector2 p) => p.x > 40f && p.x < Screen.width - 40f && p.y > Screen.height * 0.25f && p.y < Screen.height * 0.8f && !UiInput.Over(p);

        /// <summary>Three points on each standing building's roof, at default zoom.</summary>
        static List<(int lot, Vector2 at)> RoofPoints()
        {
            var res = new List<(int, Vector2)>();
            var box = Box;
            var cam = Camera.main;
            foreach (var l in Sim.Settlement.Lots.Where(x => !x.retired && x.buildingId > 0 && x.kind != LotKind.Farm && x.kind != LotKind.Pasture && x.kind != LotKind.Orchard))
                foreach (var off in new[] { Vector2.zero, new Vector2(l.w * 0.22f, 0f), new Vector2(-l.w * 0.22f, 0f) })
                {
                    float r = l.yaw * Mathf.Deg2Rad, c = Mathf.Cos(r), s = Mathf.Sin(r);
                    float x = l.x + off.x * c, z = l.z + off.x * -s;
                    Vector2 at = cam.WorldToScreenPoint(box.transform.TransformPoint(new Vector3(x, box.GroundHeight(l.x, l.z) + 0.42f, z)));
                    if (Clear(at)) res.Add((l.buildingId, at));
                }
            return res;
        }

        /// <summary>The camera sees this doll's head or middle: one behind a roof or in the trees isn't there to tap (tester run 8).</summary>
        static bool InSight(Vector3 top, Vector3 feet)
        {
            var lens = Camera.main.transform.position;
            return !Box.Blocked(lens, top) || !Box.Blocked(lens, (top + feet) * 0.5f);
        }

        IEnumerator Home()
        {
            Cards.Close(); Cards.CloseCrowd(); Cards.CloseBuilding();
            Cam.GoHome(0.02f);
            yield return new WaitForSecondsRealtime(0.12f);
        }

        IEnumerator Sweep(string where)
        {
            yield return Home();
            var points = RoofPoints();
            Assert.GreaterOrEqual(points.Count, 6, "roofs on screen");
            int building = 0, person = 0, knock = 0;
            foreach (var (lot, at) in points)
            {
                yield return Home();
                var near = Cards.PickDoll(at);
                string nearLog = near.IsNone ? "" : $"{near} {Cards.DebugDollReach(near, at)}";
                yield return TapAt(at);
                yield return Frames(2);
                if (Cards.BuildingOpen) building++;
                else if (Cards.AnyOpen) { person++; Debug.Log($"[worldtap] person at {at}: {nearLog}; {PersonCardView.Status()}"); }
                else knock++;
            }
            Debug.Log($"[worldtap] {where}: {points.Count} roof taps, building {building}, person {person}, nothing {knock}, hour {PeopleView.Find().Now.hour:0.0}");
            // Most of them: what's left is a finger really on a doll standing at that spot on screen (the log gives each one's distance).
            Assert.GreaterOrEqual(building / (float)points.Count, 0.6f, $"{where}: building {building} person {person} nothing {knock}");
            yield return Home();
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator ByDayARoofTapOpensTheBuildingInTheVillage()
        {
            yield return Village();
            yield return Sweep("village");
            AssertNoErrors("roofs");
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator ByDayARoofTapOpensTheBuildingInTheTown()
        {
            yield return Town();
            yield return Sweep("town");
            AssertNoErrors("roofs");
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator ADollInFrontOfAHouseStillWinsByTouch()
        {
            yield return Village();
            yield return Home();
            var pv = PeopleView.Find();
            var cam = Camera.main;
            foreach (var a in pv.Activities)
            {
                if (!pv.TryGetDollTop(a.who, out var top) || !pv.TryGetDollFeet(a.who, out var feet)) continue;
                Vector2 s = cam.WorldToScreenPoint((top + feet) * 0.5f);
                if (!Clear(s) || Cards.PickBuilding(s) < 0 || !InSight(top, feet)) continue;
                yield return TapAt(s);
                yield return Frames(2);
                Assert.IsTrue(Cards.AnyOpen, "the person's card");
                Assert.IsFalse(Cards.BuildingOpen, "not the building's");
                AssertNoErrors("people first");
                yield break;
            }
            Assert.Inconclusive("nobody stood in front of a building in this run");
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator ADollOnOpenGroundKeepsTheGenerousRadius()
        {
            yield return Village();
            yield return Home();
            var pv = PeopleView.Find();
            var cam = Camera.main;
            float off = PersonCardView.PickRadius * 0.8f * Screen.width / GameUiPanel.ReferenceWidth;
            foreach (var a in pv.Activities)
            {
                if (!pv.TryGetDollFeet(a.who, out var feet)) continue;
                foreach (var dir in new[] { Vector2.right, Vector2.left })
                {
                    Vector2 s = (Vector2)cam.WorldToScreenPoint(feet) + dir * off;
                    if (!Clear(s) || Cards.PickBuilding(s) >= 0 || !Cards.PickDoll(s).IsNone) continue;
                    if (Cards.PickPerson(s).IsNone) continue;
                    yield return TapAt(s);
                    yield return Frames(2);
                    Assert.IsTrue(Cards.AnyOpen, "no building under the finger: the near doll takes it");
                    AssertNoErrors("generous");
                    yield break;
                }
            }
            Assert.Inconclusive("nobody on open ground in this run");
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator TheWellTakesATapFromTheHouseInFrontOfIt()
        {
            yield return Village();
            PeopleView.DebugSetHour(23.5f); // everyone in: this is about the two lots
            yield return new WaitForSecondsRealtime(0.5f);
            yield return Home();
            var well = Sim.State.buildings.FirstOrDefault(b => b.type == BuildingType.Well);
            Assert.IsNotNull(well, "the village has a well");
            var lot = Sim.Settlement.Lots.First(l => l.buildingId == well.id);
            Vector2 at = Camera.main.WorldToScreenPoint(Box.transform.TransformPoint(new Vector3(lot.x, Box.GroundHeight(lot.x, lot.z) + 0.2f, lot.z)));
            Assert.AreEqual(well.id, Cards.PickBuilding(at), "the well under the finger");
            AssertNoErrors("well");
        }

        // ---------------- mess ----------------
        static void MoveMess(Vector3 world)
        {
            var piece = CareView.Instance.Props.AllPieces.First(p => !p.leaving);
            piece.t.position = world;
        }

        IEnumerator WithMess()
        {
            Sim.AddMess(0f, 0f, 1);
            yield return Frames(8);
            Assert.IsNotEmpty(CareView.Instance.Props.AllPieces, "mess to touch");
            CareView.Instance.TouchHint.Reset();
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator ATapOnMessOffADollHintsAndOpensNothing()
        {
            yield return Village();
            PeopleView.DebugSetHour(23.5f); // nobody near
            yield return new WaitForSecondsRealtime(0.5f);
            yield return Home();
            yield return WithMess();
            var lot = Sim.Settlement.Lots.First(l => !l.retired && l.buildingId > 0 && l.kind != LotKind.Farm);
            var box = Box;
            var w = box.transform.TransformPoint(new Vector3(lot.x, box.GroundHeight(lot.x, lot.z) + 0.1f, lot.z));
            MoveMess(w);
            yield return Frames(2);
            int knocks = GlassVoice.Instance.Knocks;
            int hints = CareView.Instance.TouchHint.HintsShown;
            Vector2 at = Camera.main.WorldToScreenPoint(w);
            Assert.IsTrue(Cards.PickBuilding(at) > 0, "a building behind the mess");
            yield return TapAt(at);
            yield return Frames(3);
            Assert.AreEqual(hints + 1, CareView.Instance.TouchHint.HintsShown, "the hint shows");
            Assert.IsTrue(GameUiPanel.Toasts.Current == CleanHint.Text || GameUiPanel.Toasts.Waiting > 0, "up, or waiting behind the prompt that holds the lane");
            Assert.IsFalse(Cards.AnyOpen || Cards.BuildingOpen, "and no card");
            yield return new WaitForSecondsRealtime(GlassVoice.TapGap + 0.2f);
            Assert.AreEqual(knocks, GlassVoice.Instance.Knocks, "and no knock");
            AssertNoErrors("mess");
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator ATapOnMessTightlyOnADollOpensTheCardAndNoHint()
        {
            yield return Village();
            yield return Home();
            yield return WithMess();
            var pv = PeopleView.Find();
            var cam = Camera.main;
            foreach (var a in pv.Activities)
            {
                if (!pv.TryGetDollTop(a.who, out var top) || !pv.TryGetDollFeet(a.who, out var feet)) continue;
                Vector2 s = cam.WorldToScreenPoint((top + feet) * 0.5f);
                if (!Clear(s) || !InSight(top, feet)) continue;
                MoveMess(feet);
                yield return Frames(2);
                int hints = CareView.Instance.TouchHint.HintsShown;
                int pulses = Hud.CleanPulses;
                yield return TapAt(s);
                yield return Frames(3);
                Assert.IsTrue(Cards.AnyOpen, "the card");
                Assert.AreEqual(hints, CareView.Instance.TouchHint.HintsShown, "no hint");
                Assert.AreEqual(pulses, Hud.CleanPulses, "no pulse");
                AssertNoErrors("mess on a doll");
                yield break;
            }
            Assert.Inconclusive("nobody on screen");
        }
    }
}
