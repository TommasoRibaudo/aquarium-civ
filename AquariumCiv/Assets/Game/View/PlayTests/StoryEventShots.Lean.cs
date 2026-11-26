// #783 (docs/design/783-story-camera.md, option A): the camera leans in at a held scene's start and eases back after. Written offline; the
// Editor worker runs it (Category StoryLean). The shots go to Temp/agent/783/: before (the lean off, the default view), after (the lean landed),
// return (the camera back at the player's own view), at 750x1334 and 1179x2556.
using System.Collections;
using System.Linq;
using AquariumCiv.Game;
using AquariumCiv.GameUI;
using AquariumCiv.Life;
using AquariumCiv.Lighting;
using AquariumCiv.Sim;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

namespace AquariumCiv.View.PlayTests
{
    [Category("StoryLean")]
    public partial class StoryEventShots
    {
        const string LeanStory = StoryEvent.TinkererBucket;
        const CampUse LeanUse = CampUse.TinkererBucket;

        struct Pose { public Vector3 target; public float zoom, pitch, yaw; }

        static Pose PoseOf(BoxCamera r) => new Pose { target = r.target, zoom = r.zoom, pitch = r.pitch, yaw = r.yaw };

        static void AssertBack(Pose want, string what)
        {
            var r = Rig;
            Assert.AreEqual(want.zoom, r.zoom, 0.03f, what + ": the player's zoom");
            Assert.AreEqual(want.pitch, r.pitch, 1.5f, what + ": the player's pitch");
            Assert.AreEqual(want.yaw, r.yaw, 0.5f, what + ": yaw never touched");
            Assert.Less(Vector3.Distance(want.target, r.target), 0.1f, what + ": the player's own spot, not home");
        }

        /// <summary>How tall a person is on screen at the camera's current scene focus (px), 0 when none is drawn.</summary>
        static float FocusPx()
        {
            var pv = Pv;
            return pv != null && pv.Camp.FocusNow() is Vector2 f ? PersonPx(Rig, Box, f) : 0f;
        }

        /// <summary>Plays the story at once and waits until its scene runs.</summary>
        IEnumerator PlayLeanStory(string tag)
        {
            string r = StoryMoment.DebugPlayNow(LeanStory);
            Debug.Log($"[783] {tag}: {r}");
            Assert.IsFalse(r.StartsWith("not now") || r.StartsWith("no "), $"{tag}: the cheat does not refuse: {r}");
            var pv = Pv;
            yield return Until(() => pv.Camp.Playing(LeanUse), 20f);
            Assert.IsTrue(pv.Camp.Playing(LeanUse), $"{tag}: the scene plays ({r}; {StoryMoment.LastWhy})");
        }

        IEnumerator EndScene(string tag)
        {
            var pv = Pv;
            HeldMomentStage.DebugEndPlaying();
            yield return Until(() => !pv.Camp.Playing(LeanUse), 20f);
            HeldMomentLean.DebugForget();
        }

        /// <summary>The whole spec in one box: before (the lean off, today's view), after (landed: a person ~5 % of the screen), return (the player's view).</summary>
        IEnumerator BeforeAfterReturn(int w, int h, string tag)
        {
            yield return LivedCamp(10f, pond: true, w: w, h: h);
            var rig = Rig;
            var home = PoseOf(rig);
            // before: today. The lean is off, the story plays, the scene is a speck.
            HeldMomentLean.Enabled = false;
            yield return PlayLeanStory(tag + " before");
            yield return new WaitForSecondsRealtime(3f);
            float before = FocusPx();
            yield return Shot($"783/{tag}_before");
            AssertBack(home, tag + " before: the camera did not move");
            Debug.Log($"[783] {tag}: person {before:0} px before");
            yield return EndScene(tag);
            yield return new WaitForSecondsRealtime(2f);
            // after: the lean on, the same story
            HeldMomentLean.Enabled = true;
            HeldMomentLean.DebugForget();
            int leans = HeldMomentLean.Leans;
            yield return PlayLeanStory(tag + " after");
            yield return Until(() => HeldMomentLean.Leans > leans, 8f);
            Assert.Greater(HeldMomentLean.Leans, leans, tag + ": the camera leaned in: " + HeldMomentLean.LastWhy);
            yield return Until(() => !rig.IsFlying, 6f);
            yield return new WaitForSecondsRealtime(0.5f);
            float after = FocusPx();
            float want = StoryLean.ScenePx(Screen.height);
            Debug.Log($"[783] {tag}: person {after:0} px after (target {want:0}); zoom {home.zoom:0.00} -> {rig.zoom:0.00}, pitch {home.pitch:0} -> {rig.pitch:0}");
            yield return Shot($"783/{tag}_after");
            Assert.Less(rig.zoom, home.zoom - 0.02f, tag + ": it leaned in");
            Assert.AreEqual(home.yaw, rig.yaw, 0.5f, tag + ": yaw is never touched");
            Assert.GreaterOrEqual(rig.pitch, home.pitch - 0.5f, tag + ": never flatter than the player's own");
            Assert.LessOrEqual(rig.pitch, Mathf.Max(home.pitch, BoxCamera.LeanPitch) + 0.5f);
            Assert.Greater(after, before * 1.5f, tag + ": the scene is much bigger");
            Assert.That(after, Is.InRange(want * 0.7f, want * 1.4f), tag + ": a person is about 5 % of the screen's height");
            // the return: the scene ends, the camera glides back to the saved pose
            yield return Until(() => !Pv.Camp.Playing(LeanUse), 120f);
            Assert.IsFalse(Pv.Camp.Playing(LeanUse), tag + ": the scene ends");
            yield return Until(() => !rig.GlideArmed && !rig.Leaning && !rig.IsFlying, 15f);
            yield return new WaitForSecondsRealtime(0.5f);
            yield return Shot($"783/{tag}_return");
            AssertBack(home, tag + " return");
            Assert.AreEqual(0, rig.LeanCancels, tag + ": nobody touched the camera");
            AssertNoErrors(tag);
            CloseSlice();
        }

        [UnityTest, Timeout(900000)]
        public IEnumerator Lean783_AtAHeldStory_LeansIn_AndEasesBackToTheDefaultView_750x1334() => BeforeAfterReturn(750, 1334, "p750");

        [UnityTest, Timeout(900000)]
        public IEnumerator Lean783_AtAHeldStory_LeansIn_AndEasesBackToTheDefaultView_1179x2556() => BeforeAfterReturn(1179, 2556, "p1179");

        /// <summary>A player already closer keeps their zoom: nothing in the lean ever zooms out. The view they set is what comes back.</summary>
        [UnityTest, Timeout(900000)]
        public IEnumerator Lean783_APlayerAlreadyZoomedIn_NeverZoomsOut_AndKeepsTheirView()
        {
            yield return LivedCamp(10f, pond: true);
            var rig = Rig; var pv = Pv;
            HeldMomentLean.Enabled = true;
            HeldMomentLean.DebugForget();
            // zoomed in on the fire (somebody else), well past the lean's own target
            rig.EaseTo(new Vector3(0f, 1f, Land.CZ), rig.yaw, 40f, 0.24f, 0f);
            yield return Until(() => !rig.IsFlying, 4f);
            yield return Frames(5);
            var mine = PoseOf(rig);
            float minZoomSeen = 9f, maxZoomSeen = 0f;
            yield return PlayLeanStory("zoomed in");
            for (int i = 0; i < 180; i++)
            {
                minZoomSeen = Mathf.Min(minZoomSeen, rig.zoom); maxZoomSeen = Mathf.Max(maxZoomSeen, rig.zoom);
                yield return null;
            }
            Assert.LessOrEqual(maxZoomSeen, mine.zoom + 0.02f, "the camera never zoomed out (it only panned over)");
            Assert.AreEqual(rig.yaw, mine.yaw, 0.5f, "yaw is never touched");
            yield return Until(() => !pv.Camp.Playing(LeanUse), 120f);
            yield return Until(() => !rig.GlideArmed && !rig.Leaning && !rig.IsFlying, 15f);
            AssertBack(mine, "zoomed in, the return");
            AssertNoErrors("zoomed in");
            CloseSlice();
        }

        /// <summary>A two-finger move during the lean cancels it at once: LeanCancels is 1, nothing returns the camera, no second lean for the scene.</summary>
        [UnityTest, Timeout(900000)]
        public IEnumerator Lean783_ATwoFingerMove_CancelsTheLean_NothingReturnsIt()
        {
            yield return LivedCamp(10f, pond: true);
            var rig = Rig; var pv = Pv;
            HeldMomentLean.Enabled = true;
            HeldMomentLean.DebugForget();
            int leans = HeldMomentLean.Leans, cancels = rig.LeanCancels;
            yield return PlayLeanStory("cancel");
            yield return Until(() => HeldMomentLean.Leans > leans, 8f);
            Assert.Greater(HeldMomentLean.Leans, leans, "it leaned: " + HeldMomentLean.LastWhy);
            yield return Until(() => !rig.IsFlying, 6f);
            var c = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            var off = new Vector2(Screen.width * 0.1f, 0f);
            BeginTouch(931, c - off, queueEventOnly: true);
            BeginTouch(932, c + off, queueEventOnly: true);
            yield return Frames(3);
            for (int i = 1; i <= 12; i++)
            {
                MoveTouch(931, c - off - new Vector2(i * 6f, 0f), delta: Vector2.zero, queueEventOnly: true);
                MoveTouch(932, c + off + new Vector2(i * 6f, 0f), delta: Vector2.zero, queueEventOnly: true);
                yield return null;
            }
            EndTouch(931, c - off, delta: Vector2.zero, queueEventOnly: true);
            EndTouch(932, c + off, delta: Vector2.zero, queueEventOnly: true);
            yield return Frames(5);
            Assert.AreEqual(cancels + 1, rig.LeanCancels, "the two-finger move cancelled the lean");
            Assert.AreEqual(1, HeldMomentLean.Cancelled, "the scene's lean knows the player took the camera");
            var theirs = PoseOf(rig);
            int after = HeldMomentLean.Leans;
            yield return Until(() => !pv.Camp.Playing(LeanUse), 120f);
            yield return new WaitForSecondsRealtime(4f); // longer than the glide's wait and ease
            Assert.IsFalse(rig.GlideArmed, "nothing returns the camera: the player has taken it");
            Assert.AreEqual(after, HeldMomentLean.Leans, "no second lean for that scene");
            Assert.AreEqual(theirs.zoom, rig.zoom, 0.05f, "the camera stayed where the player left it");
            AssertNoErrors("cancel");
            CloseSlice();
        }

        /// <summary>A tool in hand (the same gate as a card, a sheet, photo mode: GlassVoice.CameraFree) means no lean at all.</summary>
        [UnityTest, Timeout(900000)]
        public IEnumerator Lean783_WhileAToolIsInHand_TheCameraStaysPut()
        {
            yield return LivedCamp(10f, pond: true);
            var rig = Rig;
            HeldMomentLean.Enabled = true;
            HeldMomentLean.DebugForget();
            int leans = HeldMomentLean.Leans;
            GameHud.Instance.SetHand(GameHud.KitTool.Mister);
            yield return Frames(5);
            Assert.IsFalse(GlassVoice.CameraFree(), "a tool in hand: the camera is the player's");
            var home = PoseOf(rig);
            yield return PlayLeanStory("tool");
            yield return new WaitForSecondsRealtime(6f);
            Assert.AreEqual(leans, HeldMomentLean.Leans, "no lean: " + HeldMomentLean.LastWhy);
            AssertBack(home, "a tool in hand: the camera did not move");
            GameHud.Instance.SetHand(GameHud.KitTool.None);
            AssertNoErrors("tool");
            CloseSlice();
        }

        /// <summary>Two scenes one after the other: the camera goes scene to scene and comes back to the player's view once (one way home).</summary>
        [UnityTest, Timeout(900000)]
        public IEnumerator Lean783_TwoScenesOneAfterTheOther_OneWayHome()
        {
            yield return LivedCamp(10f, pond: true);
            var rig = Rig; var pv = Pv;
            HeldMomentLean.Enabled = true;
            HeldMomentLean.DebugForget();
            var home = PoseOf(rig);
            int leans = HeldMomentLean.Leans;
            yield return PlayLeanStory("first");
            yield return Until(() => HeldMomentLean.Leans > leans, 8f);
            Assert.Greater(HeldMomentLean.Leans, leans, "the first leaned: " + HeldMomentLean.LastWhy);
            yield return Until(() => !rig.IsFlying, 6f);
            // the first ends and the second starts in the same breath (the stage plays one at a time)
            HeldMomentStage.DebugEndPlaying();
            string r = StoryMoment.DebugPlayNow(StoryEvent.GardenerFlowers);
            Debug.Log("[783] second: " + r);
            Assert.IsFalse(r.StartsWith("not now") || r.StartsWith("no "), "the second scene starts: " + r);
            yield return Until(() => pv.Camp.Playing(CampUse.GardenerFlowers), 20f);
            yield return new WaitForSecondsRealtime(3f);
            Assert.IsFalse(rig.Gliding, "the camera did not bounce back to the player's view between the two scenes");
            yield return Until(() => !pv.Camp.Playing(CampUse.GardenerFlowers), 120f);
            yield return Until(() => !rig.GlideArmed && !rig.Leaning && !rig.IsFlying, 15f);
            AssertBack(home, "two scenes: the one way home");
            AssertNoErrors("two scenes");
            CloseSlice();
        }
    }
}
