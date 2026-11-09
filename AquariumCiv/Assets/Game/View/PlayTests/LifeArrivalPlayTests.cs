using System.Collections;
using System.Collections.Generic;
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
    /// P9 #424 (B12): placed life arrives at once. Every species' arrival plays from the real kit tap (<see cref="PlayTestBase.KeeperPlaces"/>)
    /// and from the debug "place near camp"; the people react within about a second (they turn, one points, a kid runs over); the line
    /// and the history entry come; the waiting hint plays for a ready jar, once a visit, never for the wolves. Also the user's look calls
    /// for the life tool (2026-11-08): the land dims while a species is in hand and clears when it is put down; the ghost is the group's
    /// silhouettes. Shots: Temp/agent/p9-arr-*.png.
    /// </summary>
    [Category("LifeArrival")]
    public class LifeArrivalPlayTests : PlayTestBase
    {
        static SimCore Sim => GameClock.Instance.ActiveSim;

        /// <summary>A living box with a meadow, a pond and a far wood, at 10 h: every species has ground.</summary>
        IEnumerator LivingLand(int w = 1179, int h = 2556)
        {
            UseLivingLand(true);
            GameUiPanel.AcceptInputUnfocused(true);
            yield return OpenSlice(w, h, seed: 4242);
            GameUiPanel.AcceptInputUnfocused(true);
            Assert.IsTrue(Sim.EcologyOn, "a living-land box");
            PeopleView.DebugSetHour(10f);
            foreach (var (x, z) in new[] { (-1.6f, 0.6f), (-1.0f, -0.8f), (-2.0f, -0.2f), (1.6f, 0.9f), (0.8f, -0.9f) }) EcoDebug.MistBig(x, z);
            EcoDebug.Table(0.3f); // a pond with open water
            Sim.DebugGrowFarWood();
            yield return Frames(20);
            Sim.DebugJarsReady();
            yield return new WaitForSecondsRealtime(4f); // the first water's moment (its line, the camera's lean) passes
        }

        static readonly LifeSpecies[] Ladder = { LifeSpecies.Thicket, LifeSpecies.Rabbits, LifeSpecies.Deer, LifeSpecies.Ducks, LifeSpecies.Fish, LifeSpecies.Wolves };

        /// <summary>The arrival started on the tap's frame (B12 P2), the people reacted within ~1 s, the line came after the end beat and the
        /// sim's history line is there.</summary>
        IEnumerator AssertArrival(LifeSpecies s, int played0, string shot)
        {
            var la = LifeArrivalView.Instance;
            Assert.IsNotNull(la, "the arrivals view");
            Assert.AreEqual(played0 + 1, la.PlayedOf(s), $"{s}: the arrival started on the placing");
            float t0 = Time.unscaledTime;
            yield return Until(() => la.LastReactAt >= 0f, 2f);
            Assert.GreaterOrEqual(la.LastReactAt, 0f, $"{s}: the people reacted");
            Assert.LessOrEqual(la.LastReactAt - la.LastStartAt, 1f, $"{s}: within about a second");
            Assert.Greater(la.LastTurned, 0, $"{s}: someone near turned to look ({la.Status()})");
            if (s != LifeSpecies.Wolves) Assert.IsFalse(la.LastPointer.IsNone, $"{s}: the nearest grown-up points");
            else Assert.AreEqual(0, la.LastWalkers, "the wolves: nobody walks over");
            // shots through the arrival at the default camera
            float len = LifeArrivalView.Length(s);
            foreach (float at in new[] { 0.3f, len * 0.45f, len * 0.9f })
            {
                yield return new WaitForSecondsRealtime(Mathf.Max(0f, at - (Time.unscaledTime - t0)));
                yield return Shot($"{shot}-{at:0.0}s");
            }
            yield return Until(() => la.LastLine == LifeArrivalView.Lines[(int)s], len + 6f);
            Assert.AreEqual(LifeArrivalView.Lines[(int)s], la.LastLine, $"{s}: its line after the end beat");
            string kind = "life_" + s.ToString().ToLowerInvariant();
            Assert.IsTrue(Sim.State.chronicle.Any(e => e.kind == kind), $"{s}: the history line {kind}");
        }

        [UnityTest, Timeout(900000)]
        public IEnumerator EveryArrival_FromTheKitTap()
        {
            yield return LivingLand();
            var la = LifeArrivalView.Instance;
            var lpv = LifePlaceView.Instance;
            int byTap = 0;
            foreach (var s in Ladder)
            {
                Sim.DebugJarsReady();
                yield return Frames(5);
                if (!Sim.JarReady(s)) { Assert.Inconclusive($"{s}: its jar would not open ({Sim.JarStateOf(s)})"); yield break; }
                int played0 = la.PlayedOf(s);
                var ground = new List<int>();
                Sim.LifeGround(s, ground);
                // the real kit path when a glowing spot is on screen (the far wood's den may be off it)
                bool tapped = false;
                bool onScreen = GlowingCellOnScreen(Sim, s) >= 0;
                if (onScreen)
                {
                    yield return KeeperPlaces(s, "p9-arr-" + s.ToString().ToLowerInvariant());
                    tapped = LastPlaced;
                    byTap++;
                }
                else
                {
                    Debug.Log($"[p9-arr] {s}: no glowing spot on screen ({ground.Count} cells): placed by the debug path");
                    Assert.GreaterOrEqual(s == LifeSpecies.Wolves ? Sim.DebugPlaceWolves() : Sim.DebugPlaceNearCamp(s), 0, $"{s}: placed");
                }
                yield return null;
                if (s == LifeSpecies.Rabbits)
                {
                    // tester 2026-11-08: the pair drops at the chosen spot, and never inside the thicket's bushes
                    int home = Sim.LifePlacedCell(LifeSpecies.Rabbits);
                    var rv = RabbitView.Instance;
                    yield return Until(() => rv.TryGetDropSpot(out _), 2f);
                    Assert.IsTrue(rv.TryGetDropSpot(out var drop), "the pair drops");
                    var c0 = new Vector2(AquariumCiv.Sim.Settle.SiteMap.CXc(home), AquariumCiv.Sim.Settle.SiteMap.CZc(home));
                    Assert.Less((drop - c0).magnitude, 0.08f, $"the pair lands at the placed spot ({drop} vs {c0})");
                    var e = Sim.State.eco;
                    if (e.thicketA >= 0)
                    {
                        var t0 = new Vector2(AquariumCiv.Sim.Settle.SiteMap.CXc(e.thicketA), AquariumCiv.Sim.Settle.SiteMap.CZc(e.thicketA));
                        Assert.Greater((drop - t0).magnitude, e.thicketRadius, "the pair lands outside the thicket");
                    }
                }
                yield return AssertArrival(s, played0, "p9-arr-" + s.ToString().ToLowerInvariant() + (tapped ? "-tap" : "-dbg"));
                yield return Until(() => !la.Playing(s), 20f);
                yield return CameraSettled();
            }
            Assert.GreaterOrEqual(byTap, 4, "most species placed by the real tap");
            AssertNoErrors("every arrival");
        }

        [UnityTest, Timeout(900000)]
        public IEnumerator EveryArrival_FromTheDebugPlace_AndReplay()
        {
            yield return LivingLand();
            var la = LifeArrivalView.Instance;
            foreach (var s in Ladder)
            {
                int played0 = la.PlayedOf(s);
                int c = s == LifeSpecies.Wolves ? Sim.DebugPlaceWolves() : Sim.DebugPlaceNearCamp(s);
                Assert.GreaterOrEqual(c, 0, $"{s}: the debug place found ground");
                yield return null;
                Assert.AreEqual(played0 + 1, la.PlayedOf(s), $"{s}: the arrival plays from the debug place");
                Assert.IsFalse(Sim.LifeArrivalPending(s), $"{s}: and is acknowledged");
                yield return Until(() => la.LastReactAt >= 0f, 2f);
                Assert.LessOrEqual(la.LastReactAt - la.LastStartAt, 1f, $"{s}: the people react within about a second");
                yield return Until(() => !la.Playing(s), 20f);
            }
            // the panel's replay plays one again, with no change to the sim
            int r0 = la.PlayedOf(LifeSpecies.Ducks), ducks = Sim.EcoDucks;
            StringAssert.Contains("replayed", la.Replay(LifeSpecies.Ducks));
            Assert.AreEqual(r0 + 1, la.PlayedOf(LifeSpecies.Ducks), "replay");
            Assert.AreEqual(ducks, Sim.EcoDucks, "a replay places nothing");
            yield return Until(() => !la.Playing(LifeSpecies.Ducks), 10f);
            StringAssert.Contains("howl", la.DebugHowl());
            Assert.AreEqual(1, la.Howls, "the wolves' howl");
            Assert.AreEqual(1, la.HowlsVoiced, "the cheat's howl is heard");
            yield return new WaitForSecondsRealtime(6f);
            AssertNoErrors("debug arrivals");
        }

        static BoxCamera Cam => Camera.main.GetComponent<BoxCamera>();

        /// <summary>Waits until no lean holds the camera and it has glided home (between species).</summary>
        static IEnumerator CameraSettled() { yield return Until(() => !Cam.Leaning && !Cam.Gliding && !Cam.GlideArmed && !Cam.IsFlying, 30f); } // capped frame time: slow frames stretch the glide

        /// <summary>User 2026-11-08, "the camera leans in": the ghost and the arrival are seen close (the group reads at phone size), the camera
        /// stays through the arrival and then eases back to where the player had it. Shots: p9-lean-*.png.</summary>
        [UnityTest, Timeout(900000)]
        public IEnumerator Lean_Ghost_And_Arrival_Then_Back()
        {
            yield return LivingLand();
            var la = LifeArrivalView.Instance; var lpv = LifePlaceView.Instance; var hud = GameHud.Instance;
            foreach (var s in new[] { LifeSpecies.Rabbits, LifeSpecies.Ducks, LifeSpecies.Fish, LifeSpecies.Thicket, LifeSpecies.Deer })
            {
                Sim.DebugJarsReady();
                yield return CameraSettled();
                string tag = "p9-lean-" + s.ToString().ToLowerInvariant();
                float zoom0 = Cam.zoom; var target0 = Cam.target; int cancels0 = Cam.LeanCancels;
                hud.TakeLife(s);
                yield return Frames(5);
                int cell = GlowingCellOnScreen(Sim, s);
                if (cell < 0) { Debug.Log($"[p9-lean] {s}: no glowing spot on screen"); hud.SetHand(GameHud.KitTool.None); continue; }
                yield return Shot(tag + "-0-default");
                int n0 = la.Nudges;
                yield return TapAt(lpv.ScreenOf(cell));
                yield return new WaitForSecondsRealtime(0.3f);
                yield return Until(() => !Cam.IsFlying, 8f); // the flight runs on capped frame time: slow Editor frames stretch it
                Assert.IsTrue(lpv.GhostShown, $"{s}: the ghost");
                Assert.Greater(la.Nudges, n0, $"{s}: the camera leaned in on the ghost");
                Assert.IsTrue(Cam.Leaning, $"{s}: and holds there while the ghost waits");
                Assert.LessOrEqual(Cam.zoom, Mathf.Min(zoom0, LifeArrivalView.LeanZoom(s)) + 0.02f, $"{s}: close in");
                // user 2026-11-08: the smallest groups lean past the player's floor (0.2) to about 0.15; the rest stay above it
                bool small = s is LifeSpecies.Rabbits or LifeSpecies.Ducks or LifeSpecies.Fish;
                if (small) Assert.Less(Cam.zoom, AquariumCiv.Lighting.CameraReach.MinZoom - 0.02f, $"{s}: past the player's zoom floor");
                else Assert.GreaterOrEqual(Cam.zoom, AquariumCiv.Lighting.CameraReach.MinZoom - 0.005f, $"{s}: not past the floor");
                Assert.GreaterOrEqual(Cam.zoom, BoxCamera.LeanMinZoom - 0.005f, $"{s}: never past the lean's own floor");
                yield return Shot(tag + "-1-ghost");
                // the second tap (on the ghost where it is on screen now) sets them down; the camera stays through the arrival
                yield return TapAt(lpv.ScreenOf(lpv.GhostCell));
                yield return Frames(5);
                Assert.IsTrue(Sim.LifeIsPlaced(s), $"{s}: placed by the tap on the ghost ({lpv.Last})");
                float len = LifeArrivalView.Length(s);
                yield return new WaitForSecondsRealtime(len * 0.45f);
                Assert.IsTrue(Cam.Leaning, $"{s}: the lean holds through the arrival");
                yield return Shot(tag + "-2-arrival");
                yield return new WaitForSecondsRealtime(len * 0.5f);
                yield return Shot(tag + "-3-end");
                yield return CameraSettled();
                Assert.IsFalse(Cam.Leaning, $"{s}: the lean let go after the arrival");
                Assert.AreEqual(0, Cam.LeanCancels - cancels0, $"{s}: nothing cancelled the lean");
                Assert.AreEqual(zoom0, Cam.zoom, 0.03f, $"{s}: and the camera eased back to the player's zoom (leaning {Cam.Leaning}, glide {Cam.GlideArmed}/{Cam.Gliding}, flying {Cam.IsFlying})");
                Assert.GreaterOrEqual(Cam.zoom, Cam.minZoom - 0.005f, $"{s}: back inside the player's range");
                Assert.Less(new Vector2(Cam.target.x - target0.x, Cam.target.z - target0.z).magnitude, 0.1f, $"{s}: and their spot");
            }
            AssertNoErrors("leans");
        }

        /// <summary>A two-finger move while the camera leans cancels it (the player's camera wins), and no lean comes back until the tool is
        /// put down.</summary>
        [UnityTest, Timeout(600000)]
        public IEnumerator Lean_TwoFingers_Cancel_It()
        {
            yield return LivingLand();
            var la = LifeArrivalView.Instance; var lpv = LifePlaceView.Instance; var hud = GameHud.Instance;
            hud.TakeLife(LifeSpecies.Rabbits);
            yield return Frames(5);
            int cell = GlowingCellOnScreen(Sim, LifeSpecies.Rabbits);
            Assert.GreaterOrEqual(cell, 0, "a glowing spot");
            yield return TapAt(lpv.ScreenOf(cell));
            yield return new WaitForSecondsRealtime(0.3f);
            yield return Until(() => !Cam.IsFlying, 8f);
            Assert.IsTrue(Cam.Leaning, "leaning on the ghost");
            int c0 = Cam.LeanCancels;
            // two fingers pan across the screen
            var a = new Vector2(Screen.width * 0.4f, Screen.height * 0.5f); var b = new Vector2(Screen.width * 0.6f, Screen.height * 0.5f);
            BeginTouch(901, a, queueEventOnly: true); BeginTouch(902, b, queueEventOnly: true);
            yield return null;
            for (int i = 1; i <= 12; i++) { var d = new Vector2(0f, i * Screen.height * 0.01f); MoveTouch(901, a + d, delta: Vector2.zero, queueEventOnly: true); MoveTouch(902, b + d, delta: Vector2.zero, queueEventOnly: true); yield return null; }
            EndTouch(901, a, delta: Vector2.zero, queueEventOnly: true); EndTouch(902, b, delta: Vector2.zero, queueEventOnly: true);
            yield return Frames(3);
            Assert.Greater(Cam.LeanCancels, c0, "two fingers cancelled the lean");
            Assert.IsFalse(Cam.Leaning, "no lean now");
            float z = Cam.zoom; var t = Cam.target;
            yield return new WaitForSecondsRealtime(3f);
            // user 2026-11-08: a rabbit's lean sits under the player's floor; once cancelled it only eases up to the floor, no glide back
            Assert.AreEqual(Mathf.Max(z, Cam.minZoom), Cam.zoom, 0.01f, "no glide back after a cancel: the player has the camera");
            // moving the ghost now doesn't lean again
            int other = GlowingCellOnScreen(Sim, LifeSpecies.Rabbits);
            int n0 = la.Nudges;
            if (other >= 0 && other != lpv.GhostCell) { yield return TapAt(lpv.ScreenOf(other)); yield return Frames(5); Assert.AreEqual(n0, la.Nudges, "refused: no new lean"); }
            Assert.IsTrue(la.LeanRefused, "the lean stays off for this placing");
            hud.SetHand(GameHud.KitTool.None);
            yield return Frames(5);
            Assert.IsFalse(la.LeanRefused, "put down: the next placing may lean again");
            AssertNoErrors("lean cancel");
        }

        [UnityTest, Timeout(600000)]
        public IEnumerator Ghost_And_Dim_Look()
        {
            yield return LivingLand();
            var hud = GameHud.Instance; var lpv = LifePlaceView.Instance;
            // the same view without and with a species in hand: the dim and the soft glow
            yield return Shot("p9-arr-dim-0-off");
            hud.TakeLife(LifeSpecies.Thicket);
            yield return new WaitForSecondsRealtime(0.3f);
            yield return Shot("p9-arr-dim-1-on");
            hud.SetHand(GameHud.KitTool.None);
            yield return Frames(2);
            Assert.IsFalse(lpv.Dimmed, "the dim clears the moment the tool is put down");
            foreach (var s in Ladder)
            {
                hud.TakeLife(s);
                yield return Frames(5);
                Assert.IsTrue(lpv.Dimmed, $"{s} in hand: the rest of the land dims");
                int cell = GlowingCellOnScreen(Sim, s);
                if (cell < 0) { Debug.Log($"[p9-arr] {s}: no glowing spot on screen for the ghost shot"); yield return Shot($"p9-arr-dim-{s.ToString().ToLowerInvariant()}"); hud.SetHand(GameHud.KitTool.None); continue; }
                yield return TapAt(lpv.ScreenOf(cell));
                yield return new WaitForSecondsRealtime(0.5f);
                Assert.IsTrue(lpv.GhostShown, $"{s}: the ghost");
                yield return Shot($"p9-arr-ghost-{s.ToString().ToLowerInvariant()}");
                hud.SetHand(GameHud.KitTool.None);
                yield return Frames(2);
                Assert.IsFalse(lpv.Dimmed, $"{s}: the dim clears at once");
                Assert.IsFalse(lpv.GhostShown, $"{s}: and the ghost goes");
            }
            AssertNoErrors("ghosts");
        }

        [UnityTest, Timeout(600000)]
        public IEnumerator AReadyJar_Hints_OncePerVisit_NeverWolves()
        {
            yield return LivingLand();
            var la = LifeArrivalView.Instance; var sdv = StoryDirectorView.Instance;
            Assert.IsNotNull(sdv, "the story director");
            Assert.IsTrue(Sim.JarReady(LifeSpecies.Thicket), "the thicket's jar is ready");
            Assert.IsTrue(la.HintWanted(Sim, out int sp, out int stage, out int cell, out _, out _), "a ready jar wants its hint");
            Assert.AreEqual((int)LifeSpecies.Thicket, sp, "the first ready jar's");
            Assert.AreEqual(1, stage, "stage 1 in its first year");
            // the box's first moments (a welcome, the first water) hold everyone: let them pass, as a player would
            for (int calm = 0, f = 0; calm < 60 && f < 900; f++) { calm = StagedMoments.StopAll() == 0 && StagedMoments.Playing == 0 ? calm + 1 : 0; yield return null; }
            PeopleView.Find().Planner.ClearLive();
            yield return new WaitForSecondsRealtime(1f);
            // the cheat starts it at once with people near the spot
            string said = "";
            for (int i = 0; i < 6 && !said.Contains("stage"); i++)
            {
                said = sdv.HintNow(LifeSpecies.Thicket, 1);
                if (!said.Contains("stage")) { PeopleView.DebugSetHour(10f + i * 0.5f); yield return new WaitForSecondsRealtime(1f); }
            }
            StringAssert.Contains("stage", said, "the hint started");
            var plan = sdv.LastPlan;
            var pv = PeopleView.Find();
            Assert.IsTrue(plan.members.Any(k => pv.Planner.InLifeStory(k > 0 ? PersonRef.Notable(k) : PersonRef.Crowd(-k - 1), pv.Now)), "someone goes to the empty meadow");
            Assert.IsTrue(la.HintedThisVisit(LifeSpecies.Thicket), "once this visit");
            yield return new WaitForSecondsRealtime(3f);
            yield return Shot("p9-arr-hint-thicket");
            // once per visit per jar, and never the wolves
            var seen = new List<int>();
            while (la.HintWanted(Sim, out int s2, out int st2, out _, out _, out _))
            {
                Assert.AreNotEqual((int)LifeSpecies.Thicket, s2, "the thicket hinted already this visit");
                Assert.AreNotEqual((int)LifeSpecies.Wolves, s2, "nobody misses wolves");
                seen.Add(s2);
                la.HintStarted((LifeSpecies)s2, st2, 0);
                if (seen.Count > 8) break;
            }
            Assert.Greater(seen.Count, 0, "the other ready jars hint too");
            // the stage grows with the time ready
            Assert.AreEqual(2, StoryRules.HintStage(13));
            Assert.AreEqual(3, StoryRules.HintStage(25));
            AssertNoErrors("hint");
        }

        /// <summary>User 2026-11-08: the waiting hint leans only when its spot is off screen or the camera is zoomed out past 0.6; framed and
        /// close enough, the camera stays where the player has it.</summary>
        [UnityTest, Timeout(600000)]
        public IEnumerator HintLean_OnlyOffScreen_OrZoomedOut()
        {
            // the rule itself
            Assert.IsTrue(LifeArrivalView.HintWantsLean(new Vector3(0.5f, 0.5f, 5f), 0.9f), "zoomed out past 0.6: lean");
            Assert.IsFalse(LifeArrivalView.HintWantsLean(new Vector3(0.5f, 0.5f, 5f), 0.5f), "framed and close: no lean");
            Assert.IsTrue(LifeArrivalView.HintWantsLean(new Vector3(1.2f, 0.5f, 5f), 0.5f), "off to the side: lean");
            Assert.IsTrue(LifeArrivalView.HintWantsLean(new Vector3(0.5f, 0.5f, -1f), 0.4f), "behind the camera: lean");
            Assert.IsTrue(LifeArrivalView.HintWantsLean(new Vector3(0.5f, 0.97f, 5f), 0.4f), "at the top edge: lean");
            yield return LivingLand();
            for (int calm = 0, f = 0; calm < 60 && f < 900; f++) { calm = StagedMoments.StopAll() == 0 && StagedMoments.Playing == 0 ? calm + 1 : 0; yield return null; }
            yield return CameraSettled();
            var la = LifeArrivalView.Instance;
            Assert.IsTrue(la.HintSpot(Sim, LifeSpecies.Thicket, out int cell, out float hx, out float hz), "a hint spot");
            var box = UnityEngine.Object.FindAnyObjectByType<BoxView>();
            var spot = box.transform.TransformPoint(box.land.OnGround(hx, hz));
            // 1. framed, zoomed in to 0.5: no lean
            Cam.FlyTo(spot, 0.5f, 0f);
            yield return Frames(3);
            int leans0 = la.HintLeans, framed0 = la.HintFramed;
            la.HintStarted(LifeSpecies.Thicket, 1, 1, 6f, cell);
            yield return Frames(3);
            Assert.AreEqual(leans0, la.HintLeans, $"framed at zoom {Cam.zoom:0.00}: no lean");
            Assert.AreEqual(framed0 + 1, la.HintFramed, "counted as framed");
            Assert.IsFalse(Cam.Leaning, "the camera stays");
            // 2. zoomed out past 0.6 (the spot still on screen): lean
            Cam.FlyTo(spot, 0.95f, 0f);
            yield return Frames(3);
            la.NewVisit();
            la.HintStarted(LifeSpecies.Thicket, 1, 1, 3f, cell);
            yield return Frames(3);
            Assert.AreEqual(leans0 + 1, la.HintLeans, $"zoomed out ({Cam.zoom:0.00}): the camera leans in");
            yield return Shot("p9-hint-lean-zoomed-out");
            yield return CameraSettled();
            // 3. close (0.5) but the spot is off screen: lean
            var away = box.transform.TransformPoint(box.land.OnGround(-hx, -hz));
            Cam.FlyTo(away, 0.4f, 0f);
            yield return Frames(3);
            var vp = Camera.main.WorldToViewportPoint(spot);
            if (LifeArrivalView.HintWantsLean(vp, Cam.zoom) && Cam.zoom <= LifeArrivalView.HintLeanFromZoom)
            {
                la.NewVisit();
                la.HintStarted(LifeSpecies.Thicket, 1, 1, 3f, cell);
                yield return Frames(3);
                Assert.AreEqual(leans0 + 2, la.HintLeans, $"off screen ({vp}): the camera leans in");
            }
            else Debug.Log($"[p9-hint-lean] the far spot still frames the hint ({vp}); case 3 skipped");
            AssertNoErrors("hint lean");
        }

        /// <summary>User 2026-11-08: the dusk howls are silent except at most one a visit (the CC0 chorus); a new visit may be heard again.</summary>
        [UnityTest, Timeout(600000)]
        public IEnumerator DuskHowl_HeardOncePerVisit()
        {
            yield return LivingLand();
            var la = LifeArrivalView.Instance;
            Assert.GreaterOrEqual(Sim.DebugPlaceWolves(), 0, "wolves");
            yield return Until(() => !la.Playing(LifeSpecies.Wolves), 20f);
            la.NewVisit();
            int v0 = la.HowlsVoiced, s0 = la.HowlsSilent;
            Assert.IsTrue(la.VoiceHowl(), "the first dusk howl of a visit is heard");
            Assert.IsFalse(la.VoiceHowl(), "the next one is silent");
            Assert.IsFalse(la.VoiceHowl(), "and the next");
            Assert.AreEqual(v0 + 1, la.HowlsVoiced); Assert.AreEqual(s0 + 2, la.HowlsSilent);
            // the pack's own dusk howl in normal play goes through the same limit
            PeopleView.DebugSetHour(19.2f);
            var wv = WolfView.Instance;
            Assert.IsNotNull(wv, "the wolves' view");
            int d0 = wv.DuskHowls;
            yield return Until(() => wv.DuskHowls > d0, 60f);
            Debug.Log($"[p9-howl] dusk howls {wv.DuskHowls - d0}, heard {la.HowlsVoiced - v0}, silent {la.HowlsSilent - s0}");
            Assert.AreEqual(v0 + 1, la.HowlsVoiced, "no second howl heard this visit");
            la.NewVisit();
            Assert.IsFalse(la.HowlHeardThisVisit, "a new visit");
            Assert.IsTrue(la.VoiceHowl(), "a new visit may hear one again");
            AssertNoErrors("howl per visit");
        }
    }
}
