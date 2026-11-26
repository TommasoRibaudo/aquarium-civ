// #776 (animation-specs B81, approved 2026-11-26): the founder's statue filmed, for the AnimLab / sign-off review. Explicit (category "Statue776"):
// each test opens a village box at phone size (750x1334), plays the cheat the user would press ("Statue: raise now", then the uses' cheats on the
// standing statue), and films the beat in REAL time (GlassMotion.Instant = false) as a frame sequence: a JPG about every 0.1 s into
// Temp/agent/media/statue776/<beat>/f###.jpg with each frame's real time in times.txt (a GIF is made from them outside Unity), framed so a doll is
// about 80 px tall; then one still of what stays. The tests assert only what they can (the scene started, its cast, the caption); the pictures are
// judged by looking at them: the beats are [est] until the user signs the clips off.
//   a the raising (B81.a)   b a kid climbs the base   c flowers on the anniversary   d sit at its foot   e a nod in passing   f the green's sweep
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using AquariumCiv.Game;
using AquariumCiv.GameUI;
using AquariumCiv.Life;
using AquariumCiv.Lighting;
using AquariumCiv.Sim;
using AquariumCiv.Sim.Eco;
using AquariumCiv.View.ShowMe;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AquariumCiv.View.PlayTests
{
    [Category("Statue776")]
    public class StatueShots776 : PlayTestBase
    {
        const int W = 750, H = 1334;
        const float DollPx = 80f, Pitch = 48f, FrameEvery = 0.1f, MaxScene = 100f;

        static SimCore Sim => GameClock.Instance != null ? GameClock.Instance.Sim : null;
        static PeopleView Pv => PeopleView.Find();
        static BoxCamera Rig => Camera.main.GetComponent<BoxCamera>();
        static BoxView Box => UnityEngine.Object.FindAnyObjectByType<BoxView>();

        [TearDown]
        public void Restore()
        {
            GlassMotion.Instant = false;
            CampLife.OnlyCheats = false;
            aimYaw = 28f;
            var pv = PeopleView.Find(); if (pv != null) pv.timeScale = 1f;
            var rig = Camera.main != null ? Camera.main.GetComponent<BoxCamera>() : null;
            if (rig != null) rig.minZoom = CameraReach.MinZoom;
        }

        // ================================================================== the box, the camera, the frames
        IEnumerator VillageBox(float hour, bool town)
        {
            UseLivingLand(true);
            GameUiPanel.AcceptInputUnfocused(true);
            ShowMeDirector.BubblesOn = false;
            yield return OpenSlice(W, H, seed: 4242);
            GameUiPanel.AcceptInputUnfocused(true);
            ShowMeDirector.BubblesOn = false;
            Debug.Log("[776 shots] " + CampCheats.StageVillage(hour, town, 6, true)); // no natural use starts over the beat
            yield return new WaitForSeconds(3f);
            GlassMotion.Instant = false; // real time
            Assert.AreEqual(town ? Era.Town : Era.Village, Sim.State.era);
        }

        static string OutDir(string beat) { string d = Shots + "media/statue776/" + beat + "/"; Directory.CreateDirectory(d); return d; }

        static float PersonPx(BoxCamera rig, BoxView box, Vector2 at)
        {
            var cam = Camera.main;
            float h = CampFrame.DollHeight * CameraReach.DollScale(rig.zoom, PeopleView.DollScaleAmount);
            float y = box.SurfaceHeight(at.x, at.y);
            var a = cam.WorldToScreenPoint(box.Root.TransformPoint(new Vector3(at.x, y, at.y)));
            var b = cam.WorldToScreenPoint(box.Root.TransformPoint(new Vector3(at.x, y + h, at.y)));
            return Mathf.Abs(b.y - a.y);
        }

        /// <summary>One frame of the framing: the target eases to the middle of <paramref name="pts"/> (a snap when <paramref name="snap"/>), the zoom to the
        /// one that puts a doll about <see cref="DollPx"/> tall, widened to hold every point in the middle 80 % x 70 %.</summary>
        static float Frame(IReadOnlyList<Vector2> pts, bool snap)
        {
            var rig = Rig; var box = Box; var cam = Camera.main;
            rig.CancelFlight();
            rig.minZoom = 0.03f;
            var c = Vector2.zero; foreach (var p in pts) c += p; c /= Mathf.Max(1, pts.Count);
            var w = box.Root.TransformPoint(new Vector3(c.x, box.SurfaceHeight(c.x, c.y) + 0.06f, c.y));
            rig.target = snap ? w : Vector3.Lerp(rig.target, w, 0.1f);
            rig.pitch = Pitch;
            rig.yaw = aimYaw;
            rig.Apply();
            float px = PersonPx(rig, box, c);
            if (px <= 1f) return px;
            float h = CampFrame.DollHeight * CameraReach.DollScale(rig.zoom, PeopleView.DollScaleAmount);
            float ex = 0f, ey = 0f;
            foreach (var p in pts)
            {
                float y = box.SurfaceHeight(p.x, p.y);
                foreach (float up in new[] { 0f, h })
                {
                    var v = cam.WorldToViewportPoint(box.Root.TransformPoint(new Vector3(p.x, y + up, p.y)));
                    ex = Mathf.Max(ex, Mathf.Abs(v.x - 0.5f) * 2f); ey = Mathf.Max(ey, Mathf.Abs(v.y - 0.5f) * 2f);
                }
            }
            float z = rig.zoom;
            float fit = z * Mathf.Max(ex / 0.8f, ey / 0.7f), near = z * px / DollPx;
            rig.zoom = Mathf.Clamp(snap ? Mathf.Max(fit, near) : Mathf.Lerp(z, Mathf.Max(fit, near), 0.12f), 0.03f, rig.maxZoom);
            rig.Apply();
            return PersonPx(rig, box, c);
        }

        /// <summary>The yaw the camera looks in from for this beat (set by <see cref="AimFromTheOpenSide"/>).</summary>
        static float aimYaw = 28f;

        /// <summary>Villages are packed and roofs hide people at the default angle: the camera looks in from the side of the statue with the most open
        /// ground between it and the glass (the statue's spot is kept clear of building, the lots round it are not), its face side preferred.
        /// Every yaw the rig allows, 5 degrees apart; per yaw the nearest lot to the sight line (out to 1.6 u toward the camera), less its half size.</summary>
        static void AimFromTheOpenSide()
        {
            var rig = Rig; var sim = Sim; var at = Pv.Camp.StatueDrawnAt;
            float fy = Pv.Camp.StatueDrawnYaw * Mathf.Deg2Rad;
            var face = new Vector2(Mathf.Sin(fy), Mathf.Cos(fy));
            float best = float.MinValue, bestYaw = 28f; string log = "";
            for (float yaw = rig.minYaw; yaw <= rig.maxYaw + 0.1f; yaw += 5f)
            {
                var dir = new Vector2(-Mathf.Sin(yaw * Mathf.Deg2Rad), -Mathf.Cos(yaw * Mathf.Deg2Rad)); // from the statue toward the camera
                float clear = 1f;
                if (sim?.Settlement != null)
                    foreach (var l in sim.Settlement.Lots)
                    {
                        if (l.retired) continue;
                        var v = new Vector2(l.x, l.z) - at;
                        float t = Vector2.Dot(v, dir);
                        if (t < 0.05f || t > 1.6f) continue;
                        float off = Mathf.Abs(v.x * dir.y - v.y * dir.x) - 0.5f * Mathf.Max(l.w, l.d);
                        clear = Mathf.Min(clear, off);
                    }
                float score = Mathf.Min(clear, 0.6f) + 0.25f * Vector2.Dot(face, dir) - 0.001f * Mathf.Abs(yaw - 28f);
                if (score > best) { best = score; bestYaw = yaw; log = $"yaw {yaw:0} clear {clear:0.00} face {Vector2.Dot(face, dir):0.00}"; }
            }
            aimYaw = bestYaw;
            Debug.Log("[776 shots] aim from the open side: " + log);
        }

        /// <summary>The clock held still while the camera lands (the lesson of the 788 shots), then running again.</summary>
        static void Freeze(bool on) { var pv = Pv; if (pv != null) pv.timeScale = on ? 0f : 1f; }

        static bool Where(int key, out Vector2 at)
        {
            at = default;
            var pv = Pv;
            if (pv == null || key == 0) return false;
            var who = new List<PersonRef>(); var xs = new List<float>(); var zs = new List<float>();
            pv.StandingNow(who, xs, zs);
            for (int i = 0; i < who.Count; i++) if (who[i].Key == key) { at = new Vector2(xs[i], zs[i]); return true; }
            return false;
        }

        /// <summary>The statue and the scene's people to frame: the statue's spot always, the cast drawn now.</summary>
        static List<Vector2> Pts(CampScene sc)
        {
            var r = new List<Vector2> { Pv.Camp.StatueDrawnAt };
            if (sc != null) foreach (int k in sc.keys) if (Where(k, out var p) && (p - r[0]).magnitude < 1.2f) r.Add(p);
            return r;
        }

        /// <summary>Films real time while the use plays (at most <paramref name="max"/> s): the camera frames the statue and the cast, a JPG about
        /// every 0.1 s into media/statue776/&lt;beat&gt;/, the real time of each frame in times.txt. The number of frames.</summary>
        IEnumerator Film(string beat, CampUse use, float max, List<int> frames)
        {
            string dir = OutDir(beat);
            foreach (var old in Directory.GetFiles(dir, "f*.jpg")) File.Delete(old);
            var times = new StringBuilder();
            var sc = Pv.Camp.Scenes.LastOrDefault(s => s.use == use);
            var last = Pts(sc);
            AimFromTheOpenSide();
            Freeze(true);
            for (int settle = 0; settle < 12; settle++) { Frame(last, settle == 0); yield return null; } // the zoom lands first, the clock held
            Freeze(false);
            float t0 = Time.realtimeSinceStartup, lastAt = -1f; int n = 0;
            while (Time.realtimeSinceStartup - t0 < max && Pv.Camp.Playing(use))
            {
                var p = Pts(sc); if (p.Count > 0) last = p;
                Frame(last, false);
                float now = Time.realtimeSinceStartup - t0;
                if (now - lastAt >= FrameEvery)
                {
                    yield return new WaitForEndOfFrame();
                    var tex = ScreenCapture.CaptureScreenshotAsTexture();
                    File.WriteAllBytes(dir + $"f{n:000}.jpg", tex.EncodeToJPG(85));
                    UnityEngine.Object.Destroy(tex);
                    times.AppendLine(now.ToString("0.000", CultureInfo.InvariantCulture));
                    n++; lastAt = now;
                }
                else yield return null;
            }
            File.WriteAllText(dir + "times.txt", times.ToString());
            frames.Add(n);
            Debug.Log($"[776 shots] {beat}: {n} frames over {Time.realtimeSinceStartup - t0:0.0} s of real time, {sc?.keys.Count ?? 0} people, scene {sc?.Length:0.0} s");
        }

        /// <summary>The "after" still of what stays: the camera eased onto the statue, then a PNG.</summary>
        IEnumerator After(string beat)
        {
            AimFromTheOpenSide();
            Freeze(true);
            for (int i = 0; i < 45; i++) { Frame(new List<Vector2> { Pv.Camp.StatueDrawnAt }, i == 0); yield return null; }
            yield return Still(beat, "after");
            Freeze(false);
        }

        static IEnumerator Still(string beat, string name)
        {
            yield return new WaitForEndOfFrame();
            var tex = ScreenCapture.CaptureScreenshotAsTexture();
            File.WriteAllBytes(OutDir(beat) + name + ".png", tex.EncodeToPNG());
            UnityEngine.Object.Destroy(tex);
        }

        /// <summary>A use on the standing statue: the cheat the user presses, the scene must start; filmed to its end; the after still.</summary>
        IEnumerator Use(string beat, CampUse use, float hour, Func<string> cheat, bool town = true)
        {
            yield return VillageBox(hour, town);
            var pv = Pv;
            Debug.Log("[776 shots] " + CampCheats.StatueStand());
            yield return Until(() => pv.Camp.StatueDrawn, 8f);
            Assert.IsTrue(pv.Camp.StatueDrawn, "the statue stands");
            PeopleView.DebugSetHour(hour);
            yield return new WaitForSeconds(1.5f);
            AimFromTheOpenSide();
            Freeze(true);
            for (int i = 0; i < 30; i++) { Frame(new List<Vector2> { pv.Camp.StatueDrawnAt }, i == 0); yield return null; }
            yield return Still(beat, "before");
            Freeze(false);
            string r = cheat();
            Debug.Log($"[776 shots] {beat}: {r}");
            yield return Until(() => pv.Camp.Playing(use), 5f);
            Assert.IsTrue(pv.Camp.Playing(use), $"{beat}: the use plays ({r})");
            var frames = new List<int>();
            yield return Film(beat, use, MaxScene, frames);
            Assert.IsFalse(pv.Camp.Playing(use), beat + ": the scene ends");
            Assert.Greater(frames[0], 5, beat + ": frames of the scene");
            yield return new WaitForSeconds(1f);
            yield return After(beat);
            CloseSlice();
        }

        // ================================================================== a the raising

        /// <summary>B81.a (about 30 s, held Story scene): "Statue: raise now" in a village box at 10 h: the block rolled onto the plinth, carved in
        /// three rounds (shoulders, head, figure), the family on the camera's side, the first flowers. After: the carved statue and the bunch.</summary>
        [UnityTest, Explicit, Timeout(900000)]
        public IEnumerator B81a_TheRaising()
        {
            yield return VillageBox(10f, false);
            var pv = Pv; var sim = Sim;
            yield return Still("a-raising", "before");
            int started = StoryMoment.Started;
            string r = CampCheats.StatueRaiseNow();
            Debug.Log("[776 shots] a-raising: " + r);
            yield return Until(() => pv.Camp.Playing(CampUse.StatueRaise), 25f);
            Assert.IsTrue(pv.Camp.Playing(CampUse.StatueRaise), $"the raising plays ({r}; {StoryMoment.LastWhy})");
            Assert.Greater(StoryMoment.Started, started);
            var who = sim.StatueNotable;
            Assert.AreEqual($"{who.name} is made of stone now, on the green.", StoryMoment.LastCaption, "the caption says the line");
            var sc = pv.Camp.Scenes.Last(s => s.use == CampUse.StatueRaise);
            Debug.Log($"[776 shots] a-raising: block seats {sc.a:0.0} s, hits {sc.b:0.0} / {sc.c:0.0} / {sc.d:0.0}, flowers laid {sc.p1.y:0.0}, of {sc.Length:0.0} s; {sc.keys.Count} people; {who.name} the {sim.StatueTell(who)}");
            var frames = new List<int>();
            yield return Film("a-raising", CampUse.StatueRaise, MaxScene, frames);
            Assert.IsFalse(pv.Camp.Playing(CampUse.StatueRaise), "the scene ends");
            Assert.Greater(frames[0], 50, "frames of the scene");
            yield return new WaitForSeconds(1.5f);
            Assert.IsTrue(pv.Camp.StatueDrawn);
            yield return After("a-raising");
            CloseSlice();
        }

        // ================================================================== b-f the uses
        [UnityTest, Explicit, Timeout(900000)]
        public IEnumerator B81b_KidsClimbTheBase() => Use("b-climb", CampUse.StatueClimb, 11f, CampCheats.StatueClimb);

        [UnityTest, Explicit, Timeout(900000)]
        public IEnumerator B81c_FlowersOnTheAnniversary() => Use("c-flowers", CampUse.StatueFlowers, 9f, CampCheats.StatueFlowers);

        [UnityTest, Explicit, Timeout(900000)]
        public IEnumerator B81d_SitAtItsFoot() => Use("d-sit", CampUse.StatueSit, 12f, CampCheats.StatueSit);

        [UnityTest, Explicit, Timeout(900000)]
        public IEnumerator B81e_ANodInPassing() => Use("e-nod", CampUse.StatueNod, 11f, CampCheats.StatueNod);

        [UnityTest, Explicit, Timeout(900000)]
        public IEnumerator B81f_TheGreensSweep() => Use("f-sweep", CampUse.StatueSweep, 9f, CampCheats.StatueSweep);

        /// <summary>The look in every season (the wear: a snow cap in winter, leaves in autumn): one still each, the camera close on the statue.</summary>
        [UnityTest, Explicit, Timeout(900000)]
        public IEnumerator B81_TheLookInEverySeason()
        {
            yield return VillageBox(10f, false);
            var pv = Pv;
            Debug.Log("[776 shots] " + CampCheats.StatueStand());
            yield return Until(() => pv.Camp.StatueDrawn, 8f);
            for (int i = 0; i < 4; i++)
            {
                AimFromTheOpenSide();
                for (int f = 0; f < 45; f++) { Frame(new List<Vector2> { pv.Camp.StatueDrawnAt }, f == 0); yield return null; }
                yield return Still("season", $"{(int)LifeClock.SeasonOf(pv.Now.day)}-{LifeClock.SeasonOf(pv.Now.day)}-{pv.Camp.StatueWearDrawn}");
                EcoDebug.SkipToNextSeason();
                PeopleView.DebugSetHour(10f); // the view's day follows the sim's month (its season with it)
                yield return new WaitForSeconds(2f);
            }
            CloseSlice();
        }
    }
}
