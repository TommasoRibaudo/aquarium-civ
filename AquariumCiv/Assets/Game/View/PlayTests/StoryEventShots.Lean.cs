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
            float want = StoryLean.ScenePx(Screen.height);
            // the cast walks in: the lean frames all of it first, then tightens as it gathers (a few seconds)
            yield return Until(() => FocusPx() >= want * 0.8f || rig.zoom <= rig.minZoom + 0.015f, 20f);
            yield return new WaitForSecondsRealtime(1.5f);
            float after = FocusPx();
            Debug.Log($"[783] {tag}: aim doll {HeldMomentLean.LastDollPx:0} px, cast extent {HeldMomentLean.LastExtX:0.00} x {HeldMomentLean.LastExtY:0.00}, zoomTo {HeldMomentLean.LastZoomTo:0.00}");
            Debug.Log($"[783] {tag}: person {after:0} px after (target {want:0}); zoom {home.zoom:0.00} -> {rig.zoom:0.00}, pitch {home.pitch:0} -> {rig.pitch:0}");
            yield return Shot($"783/{tag}_after");
            Assert.Less(rig.zoom, home.zoom - 0.02f, tag + ": it leaned in");
            Assert.AreEqual(home.yaw, rig.yaw, 0.5f, tag + ": yaw is never touched");
            Assert.GreaterOrEqual(rig.pitch, home.pitch - 0.5f, tag + ": never flatter than the player's own");
            Assert.LessOrEqual(rig.pitch, Mathf.Max(home.pitch, BoxCamera.LeanPitch) + 0.5f);
            Assert.Greater(after, before * 1.5f, tag + ": the scene is much bigger");
            // on a tall phone the player's own zoom floor (CameraReach.MinZoom) stops short of 5 %: the lean never goes closer than the floor (spec), so there it must sit AT the floor
            if (rig.zoom > rig.minZoom + 0.015f)
                Assert.That(after, Is.InRange(want * 0.7f, want * 1.4f), tag + ": a person is about 5 % of the screen's height");
            else
                Assert.GreaterOrEqual(after, want * 0.55f, tag + ": at the player's floor a person is still well over 3 % of the screen's height");
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

        /// <summary>Explicit: films the lean in and back out of a story (name: LeanStory or "wedding" / "ritual") as a frame sequence in Temp/agent/783/film_*, real time.</summary>
        IEnumerator Film(string tag, System.Func<string> start, System.Func<bool> playing)
        {
            yield return LivedCamp(10f, pond: true, w: 750, h: 1334);
            var rig = Rig;
            HeldMomentLean.Enabled = true; HeldMomentLean.DebugForget();
            string r = start();
            Debug.Log($"[783] film {tag}: {r}");
            float t0 = Time.realtimeSinceStartup; int n = 0; float endAt = -1f;
            while (Time.realtimeSinceStartup - t0 < 75f)
            {
                yield return Shot($"783/film_{tag}/f{n:000}");
                Debug.Log($"[783] film {tag} f{n:000} t={Time.realtimeSinceStartup - t0:0.0} zoom={rig.zoom:0.00} lean={rig.Leaning} glide={rig.GlideArmed} playing={playing()} ext={HeldMomentLean.LastExtX:0.00}x{HeldMomentLean.LastExtY:0.00} why={HeldMomentLean.LastWhy}");
                n++;
                if (!playing() && endAt < 0f && n > 4) endAt = Time.realtimeSinceStartup;
                if (endAt > 0f && Time.realtimeSinceStartup - endAt > 5f) break;
                yield return new WaitForSecondsRealtime(0.25f);
            }
            CloseSlice();
        }

        [UnityTest, Explicit, Timeout(900000)]
        public IEnumerator Lean783_Film_Wedding() => Film("wedding", () => StoryMoment.DebugKeeperChildWed(), () => HeldMomentStage.Instance != null && HeldMomentStage.Instance.AnyPlayingStaging() != null);

        [UnityTest, Explicit, Timeout(900000)]
        public IEnumerator Lean783_Film_Bucket() => Film("bucket", () => StoryMoment.DebugPlayNow(LeanStory), () => Pv.Camp.Playing(LeanUse));

        [UnityTest, Explicit, Timeout(900000)]
        public IEnumerator Lean783_Film_Ritual() => Film("ritual", () => StagedMoments.DebugPlayRitualNow(RitualKind.All[0]), () => HeldMomentStage.Instance != null && HeldMomentStage.Instance.AnyPlayingStaging() != null);

        // ================================================================== #783: every held scene leans in

        /// <summary>The held scenes the box can play, each by its own cheat: two stories, a wedding, a newborn, a new chief, two rituals.</summary>
        static readonly (string name, System.Func<string> start)[] EveryHeld =
        {
            ("story_bucket", () => StoryMoment.DebugPlayNow(StoryEvent.TinkererBucket)),
            ("story_flowers", () => StoryMoment.DebugPlayNow(StoryEvent.GardenerFlowers)),
            ("wedding", () => StagedMoments.DebugPlayNow(MomentKind.Wedding)),
            ("newborn", () => StagedMoments.DebugPlayNow(MomentKind.Newborn)),
            ("new_leader", () => StagedMoments.DebugPlayNow(MomentKind.NewLeader)),
            ("ritual_feast", () => StagedMoments.DebugPlayRitualNow(RitualKind.Feast)),
            ("ritual_berrying", () => StagedMoments.DebugPlayRitualNow(RitualKind.Berrying)),
        };

        static readonly System.Collections.Generic.List<Vector2> leanCast = new System.Collections.Generic.List<Vector2>();

        /// <summary>The playing scene's cast spots (box x, z) and the middle of their box (the lean's aim); false when nothing is drawn.</summary>
        static bool SceneNow(out Vector2 mid)
        {
            mid = default;
            leanCast.Clear();
            var st = HeldMomentStage.Instance != null ? HeldMomentStage.Instance.AnyPlayingStaging() : null;
            if (st == null) return false;
            st.Cast?.Invoke(leanCast);
            if (leanCast.Count == 0 && st.Focus?.Invoke() is Vector2 f) leanCast.Add(f);
            if (leanCast.Count == 0) return false;
            Vector2 lo = leanCast[0], hi = leanCast[0];
            foreach (var q in leanCast) { lo = Vector2.Min(lo, q); hi = Vector2.Max(hi, q); }
            mid = (lo + hi) * 0.5f;
            if (OnScreen() < leanCast.Count)
            {   // not all of it fits: the scene is its thing (a story) or where most of its people stand, not the ground between (HeldMomentLean)
                if (st.Focus?.Invoke() is Vector2 f2) mid = f2;
                else
                {
                    var xs = leanCast.Select(q => q.x).OrderBy(x => x).ToList(); var zs = leanCast.Select(q => q.y).OrderBy(z => z).ToList();
                    int n = xs.Count;
                    mid = n % 2 == 1 ? new Vector2(xs[n / 2], zs[n / 2]) : new Vector2((xs[n / 2 - 1] + xs[n / 2]) * 0.5f, (zs[n / 2 - 1] + zs[n / 2]) * 0.5f);
                }
            }
            return true;
        }

        /// <summary>#796: a leaned scene's focus must sit within this share of the view (each way) of the middle, as a median over 3 s. The lean
        /// re-aims once a second when the focus drifts past <see cref="StoryLean.FollowX"/> / <see cref="StoryLean.FollowY"/>, and a lead walking
        /// between two looks moves it a few hundredths more; the old 0.22 / 0.2 re-aim band let a gathered cast settle 0.13 to 0.25 off.</summary>
        const float CentreTol = 0.1f;

        static float Median(System.Collections.Generic.List<float> l)
        {
            if (l.Count == 0) return 0f;
            l.Sort();
            int n = l.Count;
            return n % 2 == 1 ? l[n / 2] : (l[n / 2 - 1] + l[n / 2]) * 0.5f;
        }

        static int OnScreen()
        {
            int on = 0;
            foreach (var q in leanCast) { var qv = ViewportOf(q); if (qv.z > 0f && qv.x > 0.02f && qv.x < 0.98f && qv.y > 0.02f && qv.y < 0.98f) on++; }
            return on;
        }

        static Vector3 ViewportOf(Vector2 at)
        {
            var box = Box;
            return Camera.main.WorldToViewportPoint(box.Root.TransformPoint(new Vector3(at.x, box.SurfaceHeight(at.x, at.y) + 0.06f, at.y)));
        }

        /// <summary>Every held scene from the default view: the camera leans in (a person ~5 % of the screen's height, or the player's floor), the
        /// scene's middle sits in the middle of the view, most of its cast is on screen, yaw is never touched, and when the scene ends the camera
        /// glides back to the default view. Shots: 783/every_{size}_{scene}_before (the lean's first frame) and _after (landed).</summary>
        IEnumerator EveryHeldSceneLeansIn(int w, int h, string tag, bool campFrame)
        {
            yield return LivedCamp(10f, pond: true, w: w, h: h);
            var rig = Rig;
            if (campFrame)
            {   // the game's own default view on a lived camp: the camp frame (this compact camp's is at the player's zoom floor: the lean pans)
                Debug.Log("[783] every " + tag + ": " + CampFrameView.DebugCampFrame());
                yield return Until(() => !rig.IsFlying, 6f);
                yield return Frames(5);
            }
            // else the whole box the tests open on: the player zoomed out wide, the one big move
            var home = PoseOf(rig);
            float want = StoryLean.ScenePx(Screen.height);
            var report = new System.Text.StringBuilder();
            var failed = new System.Collections.Generic.List<string>();
            HeldMomentLean.Enabled = true;
            foreach (var (name, start) in EveryHeld)
            {
                HeldMomentStage.DebugEndPlaying();
                yield return Until(() => !rig.GlideArmed && !rig.Leaning && !rig.IsFlying, 15f);
                HeldMomentLean.DebugForget();
                PeopleView.DebugSetHour(10f);
                yield return new WaitForSecondsRealtime(1f);
                int leans = HeldMomentLean.Leans, framed = HeldMomentLean.Framed;
                string r = start();
                Debug.Log($"[783] every {tag} {name}: {r}");
                yield return Until(() => HeldMomentLean.Leans > leans || HeldMomentLean.Framed > framed, 10f);
                bool framedAlready = HeldMomentLean.Leans == leans && HeldMomentLean.Framed > framed; // the spec: a scene already framed moves nothing
                if (HeldMomentLean.Leans == leans && !framedAlready)
                {
                    failed.Add($"{name}: no lean ({r}; {HeldMomentLean.LastWhy})");
                    continue;
                }
                yield return Shot($"783/every_{tag}_{name}_before");
                float before = SceneNow(out var m0) ? PersonPx(rig, Box, m0) : 0f;
                yield return Until(() => !rig.IsFlying, 6f);
                // the cast may still be gathering: the lean tightens as it does (re-looked every second)
                yield return Until(() => !SceneNow(out var mm) || PersonPx(rig, Box, mm) >= want * 0.8f || rig.zoom <= rig.minZoom + 0.015f, 12f);
                // a lead still walking in from far is followed (the scene's thing pulled toward them) until the cast has gathered on screen
                yield return Until(() => !SceneNow(out _) || OnScreen() >= Mathf.CeilToInt(leanCast.Count * 0.75f), 15f);
                yield return new WaitForSecondsRealtime(1.2f);
                if (!SceneNow(out var mid)) { failed.Add($"{name}: the scene ended before it was framed"); continue; }
                float after = PersonPx(rig, Box, mid);
                var v = ViewportOf(mid);
                int on = 0;
                foreach (var q in leanCast) { var qv = ViewportOf(q); if (qv.z > 0f && qv.x > 0.02f && qv.x < 0.98f && qv.y > 0.02f && qv.y < 0.98f) on++; }
                yield return Shot($"783/every_{tag}_{name}_after");
                string line = $"{name}: person {before:0} -> {after:0} px (target {want:0}), zoom {home.zoom:0.00} -> {rig.zoom:0.00} (floor {rig.minZoom:0.00}), pitch {rig.pitch:0}, middle at ({v.x:0.00}, {v.y:0.00}), cast on screen {on}/{leanCast.Count}";
                var want0 = BoxCameraMath.PivotOver(HeldMomentLean.LastAim, rig.target.y, rig.yaw, rig.pitch);
                var aimV = Camera.main.WorldToViewportPoint(HeldMomentLean.LastAim);
                Debug.Log($"[783] every {tag} {line}; aim {HeldMomentLean.LastAim} at ({aimV.x:0.00}, {aimV.y:0.00}), pivot {rig.target} (unclamped {want0}), why {HeldMomentLean.LastWhy}; stagings {HeldMomentStage.Instance.StagingCount}; last look {HeldMomentLean.LastLook}; cast {string.Join(" ", leanCast.Select(q => q.ToString("0.00")))}");
                report.AppendLine(line);
                bool atFloor = rig.zoom <= rig.minZoom + 0.015f;
                bool homeAtFloor = home.zoom <= rig.minZoom + 0.03f;
                if (!homeAtFloor && rig.zoom > home.zoom - 0.02f) failed.Add(name + ": it did not lean in");
                if (rig.zoom > home.zoom + 0.02f) failed.Add(name + ": it zoomed out");
                if (Mathf.Abs(rig.yaw - home.yaw) > 0.5f) failed.Add(name + ": yaw moved");
                if (after < want * (atFloor ? 0.55f : 0.7f)) failed.Add($"{name}: a person is {after:0} px, under {(atFloor ? 55 : 70)} % of the {want:0} px target");
                // a walking scene is re-looked every second, so one frame can catch it between two looks: watch it for 3 s. A scene at the front glass
                // can't be centred (the pivot stays out of the pane, #273): there the pivot sits at its bound and only the cast on screen counts.
                bool clamped = Vector3.Distance(rig.target, want0) > 0.05f;
                int seen = 0, centred = 0;
                // #796: where the lean's own focus sits (the way it reads it each second), and whether the pivot is held at the glass then
                var offX = new System.Collections.Generic.List<float>(); var offY = new System.Collections.Generic.List<float>();
                bool atBound = false;
                for (float t0 = Time.realtimeSinceStartup; Time.realtimeSinceStartup - t0 < 3f; )
                {
                    if (SceneNow(out var mw)) { var vw = ViewportOf(mw); seen++; if (vw.z > 0f && vw.x >= 0.2f && vw.x <= 0.8f && vw.y >= 0.2f && vw.y <= 0.8f) centred++; }
                    if (HeldMomentLean.DebugFocusViewport(out var fv)) { offX.Add(Mathf.Abs(fv.x - 0.5f)); offY.Add(Mathf.Abs(fv.y - 0.5f)); }
                    if (!rig.IsFlying && Vector3.Distance(rig.target, BoxCameraMath.PivotOver(HeldMomentLean.LastAim, rig.target.y, rig.yaw, rig.pitch)) > 0.05f) atBound = true;
                    yield return null;
                }
                if (!framedAlready)
                {
                    float mx = Median(offX), my = Median(offY);
                    Debug.Log($"[796] {tag} {name}: the lean's focus off the middle by {mx:0.000} across, {my:0.000} up (median of {offX.Count} frames){(atBound ? ", the pivot held at the glass" : "")}");
                    report.AppendLine($"  {name}: focus off {mx:0.00} x {my:0.00}{(atBound ? " (glass)" : "")}");
                    if (offX.Count == 0) failed.Add($"{name}: no held lean to read the focus from");
                    else if (!atBound && (mx > CentreTol || my > CentreTol)) failed.Add($"{name}: the scene's focus sits off the middle ({mx:0.00} across, {my:0.00} up, over {CentreTol})");
                }
                Debug.Log($"[783] every {tag} {name}: middle in the middle 60 % for {centred}/{seen} frames of 3 s{(clamped ? " (the pivot at the glass bound)" : "")}");
                report.AppendLine($"  {name}: centred {centred}/{seen} frames{(clamped ? ", pivot at the glass bound" : "")}");
                if (!clamped && centred < seen * 0.6f) failed.Add($"{name}: the scene's middle is off centre ({v.x:0.00}, {v.y:0.00}; centred {centred}/{seen} frames)");
                if (on < Mathf.CeilToInt(leanCast.Count * 0.5f)) failed.Add($"{name}: only {on} of {leanCast.Count} of its cast on screen");
                // the end: the camera glides back to the default view
                HeldMomentStage.DebugEndPlaying();
                yield return Until(() => !rig.GlideArmed && !rig.Leaning && !rig.IsFlying, 15f);
                yield return new WaitForSecondsRealtime(0.3f);
                if (Mathf.Abs(rig.zoom - home.zoom) > 0.03f || Vector3.Distance(rig.target, home.target) > 0.1f) failed.Add($"{name}: did not come back to the default view (zoom {rig.zoom:0.00}, target off {Vector3.Distance(rig.target, home.target):0.00})");
            }
            Debug.Log($"[783] every {tag}:\n{report}");
            Assert.IsEmpty(failed, $"{tag}: {string.Join(" | ", failed)}\n{report}");
            Assert.AreEqual(0, rig.LeanCancels, tag + ": nobody touched the camera");
            AssertNoErrors(tag);
            CloseSlice();
        }

        [UnityTest, Timeout(1200000)]
        public IEnumerator Lean783_EveryHeldScene_LeansIn_750x1334() => EveryHeldSceneLeansIn(750, 1334, "p750", campFrame: true);

        [UnityTest, Timeout(1200000)]
        public IEnumerator Lean783_EveryHeldScene_FromTheWholeBox_750x1334() => EveryHeldSceneLeansIn(750, 1334, "p750box", campFrame: false);

        [UnityTest, Timeout(1200000)]
        public IEnumerator Lean783_EveryHeldScene_LeansIn_1179x2556() => EveryHeldSceneLeansIn(1179, 2556, "p1179", campFrame: true);
    }
}
