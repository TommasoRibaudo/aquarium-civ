// Polish night 1: the media for the sign-off page. Explicit (category "PolishNight1Media"): every test stages its own box at phone size,
// plays the cheat the user would press, and writes stills (PNG) or a real-time frame sequence (JPG + times.txt, one frame about every 0.1 s
// of real time) to Temp/agent/media/<item>/. A GIF is made from the frames outside Unity. The tests assert only what they can (the cheat
// started); what the pictures show is judged by looking at them.
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
    [Explicit("Media for the sign-off page: run by hand"), Category("PolishNight1Media")]
    public class PolishNight1MediaShots : PlayTestBase
    {
        const int W = 1179, H = 2556;
        const float CZ = 0.8f;
        static SimCore Sim => GameClock.Instance != null ? GameClock.Instance.Sim : null;
        static PeopleView Pv => PeopleView.Find();
        static BoxCamera Rig => Camera.main.GetComponent<BoxCamera>();
        static BoxView Box => UnityEngine.Object.FindAnyObjectByType<BoxView>();

        [TearDown]
        public void Restore()
        {
            GlassMotion.Instant = false;
            CampLife.CheatFruit = false;
            CampLife.OnlyCheats = false;
            var rig = Camera.main != null ? Camera.main.GetComponent<BoxCamera>() : null;
            if (rig != null) rig.minZoom = CameraReach.MinZoom;
        }

        // ================================================================== the camera, the frames

        static string OutDir(string item) { string d = Shots + "media/" + item + "/"; Directory.CreateDirectory(d); return d; }

        static IEnumerator Still(string item, string name)
        {
            yield return new WaitForEndOfFrame();
            var tex = ScreenCapture.CaptureScreenshotAsTexture();
            File.WriteAllBytes(OutDir(item) + name + ".png", tex.EncodeToPNG());
            UnityEngine.Object.Destroy(tex);
        }

        static float PersonPx(BoxCamera rig, BoxView box, Vector2 at)
        {
            var cam = Camera.main;
            float h = CampFrame.DollHeight * CameraReach.DollScale(rig.zoom, PeopleView.DollScaleAmount);
            float y = box.SurfaceHeight(at.x, at.y);
            var a = cam.WorldToScreenPoint(box.Root.TransformPoint(new Vector3(at.x, y, at.y)));
            var b = cam.WorldToScreenPoint(box.Root.TransformPoint(new Vector3(at.x, y + h, at.y)));
            return Mathf.Abs(b.y - a.y);
        }

        /// <summary>One frame of the framing: the target eases to the middle of <paramref name="pts"/> (a snap when <paramref name="snap"/>), the
        /// zoom to the one that puts a person about <paramref name="dollPx"/> tall, widened to hold every point in the middle 80 % x 70 %.</summary>
        static float Frame(IReadOnlyList<Vector2> pts, bool snap, float dollPx, float pitch, float? yaw = null)
        {
            var rig = Rig; var box = Box; var cam = Camera.main;
            rig.CancelFlight();
            rig.minZoom = 0.03f;
            var c = Vector2.zero; foreach (var p in pts) c += p; c /= Mathf.Max(1, pts.Count);
            var w = box.Root.TransformPoint(new Vector3(c.x, box.SurfaceHeight(c.x, c.y) + 0.06f, c.y));
            rig.target = snap ? w : Vector3.Lerp(rig.target, w, 0.1f);
            rig.pitch = pitch;
            if (yaw.HasValue) rig.yaw = yaw.Value;
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
            float fit = z * Mathf.Max(ex / 0.8f, ey / 0.7f), near = z * px / dollPx;
            float want = Mathf.Max(fit, near);
            rig.zoom = Mathf.Clamp(snap ? want : Mathf.Lerp(z, want, 0.12f), 0.03f, rig.maxZoom);
            rig.Apply();
            return PersonPx(rig, box, c);
        }

        static IEnumerator Settle(Func<List<Vector2>> pts, float dollPx, float pitch, float? yaw = null, int frames = 40)
        {
            for (int i = 0; i < frames; i++) { var p = pts(); if (p.Count > 0) Frame(p, i == 0, dollPx, pitch, yaw); yield return null; }
        }

        /// <summary>Films <paramref name="secs"/> s of real time: the camera follows <paramref name="pts"/>, a JPG about every 0.1 s into
        /// media/&lt;item&gt;/&lt;name&gt;/ with each frame's real time in times.txt.</summary>
        static IEnumerator Clip(string item, string name, float secs, Func<List<Vector2>> pts, float dollPx, float pitch, float? yaw = null)
        {
            string dir = OutDir(item) + name + "/";
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
            Directory.CreateDirectory(dir);
            var times = new StringBuilder();
            float t0 = Time.realtimeSinceStartup, last = -1f; int n = 0;
            var lastPts = pts();
            while (Time.realtimeSinceStartup - t0 < secs)
            {
                var p = pts(); if (p.Count > 0) lastPts = p;
                if (lastPts.Count > 0) Frame(lastPts, false, dollPx, pitch, yaw);
                float now = Time.realtimeSinceStartup - t0;
                if (now - last >= 0.1f)
                {
                    yield return new WaitForEndOfFrame();
                    var tex = ScreenCapture.CaptureScreenshotAsTexture();
                    File.WriteAllBytes(dir + $"f{n:000}.jpg", tex.EncodeToJPG(85));
                    UnityEngine.Object.Destroy(tex);
                    times.AppendLine(now.ToString("0.000", CultureInfo.InvariantCulture));
                    n++; last = now;
                }
                else yield return null;
            }
            File.WriteAllText(dir + "times.txt", times.ToString());
            Debug.Log($"[pn1 media] {item}/{name}: {n} frames over {secs:0.0} s");
        }

        // ================================================================== the boxes

        /// <summary>A living-land camp (Fb5PassShots.Stage), optionally with a pond and fish, the staged moments ended, real motion on.</summary>
        IEnumerator Box_(int seed, int grownUps, bool pond, float hour, bool fish, int w = W, int h = H)
        {
            var st = new Fb5PassShots();
            yield return st.Stage(seed, grownUps, pond, hour, w, h);
            if (fish)
            {
                Debug.Log("[pn1 media] ducks at " + Sim.DebugPlaceNearCamp(LifeSpecies.Ducks) + ", fish at " + Sim.DebugPlaceNearCamp(LifeSpecies.Fish));
                yield return Until(() => LifeArrivalView.Instance == null || !LifeArrivalView.Instance.AnyPlaying, 20f);
            }
            for (int calm = 0, f = 0; calm < 120 && f < 1500; f++) { calm = StagedMoments.StopAll() == 0 && StagedMoments.Playing == 0 ? calm + 1 : 0; yield return null; }
            if (fish) { int have = Sim.State.notables.Count(n => n.alive && n.age >= SimCore.AdultAge); if (have < grownUps) Sim.DebugAddKids(grownUps - have, 21f); Pv.Planner.Invalidate(); yield return new WaitForSecondsRealtime(8f); }
            ShowMeDirector.BubblesOn = false;
            GlassMotion.Instant = false;
            PeopleView.DebugSetHour(hour);
        }

        static List<Vector2> Where_(Func<PersonActivity, bool> pred)
        {
            var pv = Pv; var r = new List<Vector2>();
            if (pv == null) return r;
            var who = new List<PersonRef>(); var xs = new List<float>(); var zs = new List<float>();
            pv.StandingNow(who, xs, zs);
            for (int i = 0; i < who.Count; i++) if (pv.TryGetActivityOf(who[i], out var a) && pred(a)) r.Add(new Vector2(xs[i], zs[i]));
            return r;
        }

        static List<Vector2> Fishers(bool still) => Where_(a => a.kind == ActivityKind.Fish && (!still || !a.moving));

        static List<Vector2> Kids()
        {
            var pv = Pv; var r = new List<Vector2>();
            if (pv == null) return r;
            var who = new List<PersonRef>(); var xs = new List<float>(); var zs = new List<float>();
            pv.StandingNow(who, xs, zs);
            for (int i = 0; i < who.Count; i++) if (pv.Planner.StageOf(who[i]) == LifeStage.Kid) r.Add(new Vector2(xs[i], zs[i]));
            return r;
        }

        static string Gaps(List<Vector2> p)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < p.Count; i++) for (int j = i + 1; j < p.Count; j++) sb.Append($" {i}-{j}:{(p[i] - p[j]).magnitude:0.00}u");
            return sb.ToString();
        }

        // ================================================================== 1: #765 three fishers

        [UnityTest, Timeout(900000)]
        public IEnumerator Pn1_01_ThreeFishers()
        {
            yield return Box_(4242, 9, pond: true, hour: 10f, fish: true);
            int n = Pv.Planner.DebugFishNow(Pv.Now, 3, 8f);
            Debug.Log("[pn1 media] 765 sent " + n);
            Assert.AreEqual(3, n, "three fishers sent");
            for (int i = 0; i < 12; i++) { yield return new WaitForSecondsRealtime(2f); Debug.Log($"[pn1 media] 765 t{i}: fishers {Fishers(false).Count} (still {Fishers(true).Count}), any activity {Where_(a => true).Count}, hour {Pv.Now.hour:0.0}"); }
            yield return Until(() => Fishers(false).Count >= 3 && Fishers(true).Count >= 2, 60f);
            yield return Until(() => Fishers(true).Count >= 3, 20f);
            yield return new WaitForSecondsRealtime(3f);
            var f = Fishers(false);
            Debug.Log($"[pn1 media] 765 fishers {f.Count}, gaps{Gaps(f)}");
            yield return Settle(() => Fishers(false), 90f, 40f);
            yield return Still("765", "three_fishers");
            yield return Settle(() => Fishers(false), 60f, 52f, frames: 30);
            yield return Still("765", "three_fishers_wide");
            Debug.Log($"[pn1 media] 765 pond cells {Sim.EcoPondCells}");
        }

        // ================================================================== 2: #767 fishers chat

        [UnityTest, Timeout(900000)]
        public IEnumerator Pn1_02_FishersChat()
        {
            yield return Box_(4242, 6, pond: true, hour: 10f, fish: true);
            int n = Pv.Planner.DebugFishersChat(Pv.Now, 3f);
            Assert.AreEqual(2, n, "two fishers sent");
            yield return Until(() => Fishers(true).Count >= 2, 90f);
            yield return Settle(() => Fishers(false), 110f, 48f, frames: 40);
            Debug.Log($"[pn1 media] 767 fishers {Fishers(false).Count}, gaps{Gaps(Fishers(false))}");
            yield return Still("767", "chat_start");
            yield return Clip("767", "chat", 8f, () => Fishers(false), 110f, 48f);
        }

        // ================================================================== 3: #770 dance

        [UnityTest, Timeout(1200000)]
        public IEnumerator Pn1_03_Dance()
        {
            yield return Box_(4242, 6, pond: false, hour: 17f, fish: false);
            Debug.Log("[pn1 media] 770 " + MomentPlayer.DebugGrowVillage(40f));
            yield return new WaitForSecondsRealtime(3f);
            for (int calm = 0, f = 0; calm < 120 && f < 1500; f++) { calm = StagedMoments.StopAll() == 0 && StagedMoments.Playing == 0 ? calm + 1 : 0; yield return null; }
            Box.Rebuild();
            PeopleView.DebugSetHour(17f);
            Debug.Log("[pn1 media] 770 " + MomentPlayer.DebugFestival());
            yield return Until(() => Where_(a => a.kind == ActivityKind.Dance && !a.moving).Count >= 3, 90f);
            var d = Where_(a => a.kind == ActivityKind.Dance);
            Debug.Log($"[pn1 media] 770 dancers {d.Count}, era {Sim.State.era}, gaps{Gaps(d)}");
            Assert.GreaterOrEqual(d.Count, 3, "a dance ring");
            // log where the dancers stand against the pieces: nobody inside a building's lot
            foreach (var pt in d)
                foreach (var pc in Box.layout.pieces)
                    if (pc.kind != PieceKind.Fire && pc.kind != PieceKind.Lantern && pc.kind != PieceKind.LogSeat && pc.kind != PieceKind.Bench && pc.kind != PieceKind.Gathering
                        && Mathf.Abs(pt.x - pc.x) < pc.w * 0.45f && Mathf.Abs(pt.y - pc.z) < pc.d * 0.45f)
                        Debug.Log($"[pn1 media] 770 CHECK dancer at ({pt.x:0.00},{pt.y:0.00}) inside {pc.kind} lot at ({pc.x:0.00},{pc.z:0.00}) {pc.w:0.00}x{pc.d:0.00}");
            yield return Settle(() => Where_(a => a.kind == ActivityKind.Dance), 100f, 42f, frames: 40);
            yield return Still("770", "festival_ring_still");
            yield return Clip("770", "dance", 8f, () => Where_(a => a.kind == ActivityKind.Dance), 100f, 42f);
            Debug.Log("[pn1 media] 770 " + MomentPlayer.DebugEndFestival());
            yield return new WaitForSecondsRealtime(2f);
            for (int k = 0; k < 5; k++) { var kid = Sim.DebugBirth(); if (kid != null) kid.age = 7f + k % 3; }
            Pv.Planner.Invalidate();
            PeopleView.DebugSetHour(12f);
            yield return new WaitForSecondsRealtime(3f);
            Debug.Log("[pn1 media] 770 kids ring: " + KidPlayDirector.Debug(PlayGame.DanceRing));
            yield return new WaitForSecondsRealtime(12f);
            Debug.Log($"[pn1 media] 770 kids {Kids().Count}, dance-kind {Where_(a => a.kind == ActivityKind.Dance).Count}, " + Pv.KidPlay.Status());
            yield return Settle(Kids, 100f, 42f, frames: 40);
            yield return Still("770", "kids_ring");
        }

        // ================================================================== 5: #772 ball at the glass

        [UnityTest, Timeout(900000)]
        public IEnumerator Pn1_05_BallAtTheGlass()
        {
            yield return Box_(4242, 6, pond: false, hour: 11f, fish: false);
            for (int k = 0; k < 4; k++) { var kid = Sim.DebugBirth(); if (kid != null) kid.age = 7f + k; }
            Pv.Planner.Invalidate();
            PeopleView.DebugSetHour(11f);
            yield return new WaitForSecondsRealtime(3f);
            string r = KidPlayDirector.DebugBallAtGlass();
            Debug.Log("[pn1 media] 772 " + r);
            yield return Until(() => Where_(a => a.kind == ActivityKind.Play).Count >= 2, 60f);
            var kids = Where_(a => a.kind == ActivityKind.Play);
            Debug.Log($"[pn1 media] 772 players {kids.Count}: " + string.Join(" ", kids.Select(k => $"({k.x:0.00},{k.y:0.00})")));
            // the kids and the pane they throw at: keep the pane's spot (x 3, the land's middle) in frame
            List<Vector2> Pts() { var p = Where_(a => a.kind == ActivityKind.Play); p.Add(new Vector2(2.9f, CZ)); return p; }
            yield return Settle(Pts, 100f, 40f, yaw: 28f, frames: 40);
            yield return Still("772", "ball_start");
            yield return Clip("772", "ball", 10f, Pts, 100f, 40f);
        }

        // ================================================================== 6: #768 / #762 / #769, a village at a seed (before or after the swap)

        /// <summary>The tag ("before" or "after") comes from Temp/agent/media/6/tag.txt, so the same tests shoot both builds.</summary>
        IEnumerator Village(int seed)
        {
            string tag = File.Exists(Shots + "media/6/tag.txt") ? File.ReadAllText(Shots + "media/6/tag.txt").Trim() : "after";
            var st = new Fb5PassShots();
            yield return st.Stage(seed, 6, false, 10f, W, H);
            GlassMotion.Instant = true;
            Debug.Log("[pn1 media] 6/" + seed + " " + CampCheats.StageVillage(10f, false, 4));
            for (int i = 0; i < 8 && Sim.State.month / SimConfig.MonthsPerYear < 36; i++) SimLayoutSource.DebugSkipYears(3f);
            Box.Rebuild();
            ShowMeDirector.BubblesOn = false;
            yield return new WaitForSecondsRealtime(3f);
            Debug.Log($"[pn1 media] 6/{seed} {tag}: year {Sim.State.month / SimConfig.MonthsPerYear}, era {Sim.State.era}, buildings {Sim.State.buildings.Count}, pieces {Box.layout.pieces.Count}, fields {Box.layout.fields?.Count}");
            var sb = new StringBuilder();
            foreach (var pc in Box.layout.pieces) if (pc.kind is PieceKind.Church or PieceKind.DryingRack or PieceKind.Granary or PieceKind.Barn) sb.Append($" {pc.kind}({pc.x:0.0},{pc.z:0.0})");
            Debug.Log($"[pn1 media] 6/{seed} {tag} pieces:{sb}");
            if (Box.layout.fields != null) foreach (var f in Box.layout.fields) { var c = Vector2.zero; foreach (var p in f.polygon) c += p; c /= f.polygon.Length; Debug.Log($"[pn1 media] 6/{seed} {tag} field {f.crop} at ({c.x:0.0},{c.y:0.0}) n {f.polygon.Length}"); }
            string dir = "6";
            var rig = Rig; rig.CancelFlight(); rig.minZoom = 0.03f;
            var box = Box;
            var mid = Vector2.zero; int mn = 0; foreach (var pc in Box.layout.pieces) { mid += pc.Pos; mn++; } mid = mn > 0 ? mid / mn : new Vector2(0f, CZ);
            rig.target = box.Root.TransformPoint(new Vector3(mid.x, box.SurfaceHeight(mid.x, mid.y), mid.y)); rig.zoom = 0.6f; rig.pitch = 52f; rig.Apply();
            yield return null; yield return null;
            yield return Still(dir, $"{seed}_{tag}");
            var church = Box.layout.pieces.FirstOrDefault(p => p.kind == PieceKind.Church);
            if (church != null) { Frame(new[] { church.Pos }, true, 120f, 45f); yield return null; yield return null; yield return Still(dir, $"{seed}_{tag}_church"); }
            var rack = Box.layout.pieces.FirstOrDefault(p => p.kind == PieceKind.DryingRack);
            if (rack != null) { Frame(new[] { rack.Pos }, true, 120f, 45f); yield return null; yield return null; yield return Still(dir, $"{seed}_{tag}_rack"); }
        }

        [UnityTest, Timeout(1200000)] public IEnumerator Pn1_06_Village7() => Village(7);
        [UnityTest, Timeout(1200000)] public IEnumerator Pn1_06_Village11() => Village(11);
        [UnityTest, Timeout(1200000)] public IEnumerator Pn1_06_Village1() => Village(1);

        // ================================================================== 7: #782 fruit pick

        /// <summary>A lived-in camp with the Keeper's tree grown (the recipe of StoryEventShots.LivedCamp).</summary>
        IEnumerator Lived(float hour, int w, int h)
        {
            UseLivingLand(true);
            GameUiPanel.AcceptInputUnfocused(true);
            ShowMeDirector.BubblesOn = false;
            yield return OpenSlice(w, h, seed: 4242);
            GameUiPanel.AcceptInputUnfocused(true);
            ShowMeDirector.BubblesOn = false;
            var sim = Sim;
            PeopleView.DebugSetHour(hour);
            foreach (var (x, z) in new[] { (-1.6f, 0.6f), (-1.0f, -0.8f), (-2.0f, -0.2f), (1.6f, 0.9f), (0.8f, -0.9f) }) EcoDebug.MistBig(x, z);
            EcoDebug.SkipSeasons(1);
            EcoDebug.OpenCampGate();
            for (int i = 0; i < 12 && sim.State.era != Era.Camp; i++) { EcoDebug.MistCamp(); EcoDebug.SkipSeasons(1); }
            for (int k = 0; k < 6; k++)
                foreach (var (x, z) in new[] { (-2.4f, 1.2f), (0f, 1.4f), (2.4f, -1f), (0.5f, -1.6f), (-0.5f, 0.2f), (2f, 1.5f) }) EcoDebug.MistBig(x, z);
            int adults = sim.State.notables.Count(n => n.alive && n.age >= SimCore.AdultAge);
            if (adults < 7) sim.DebugAddKids(7 - adults, 21f);
            EcoDebug.SkipSeasons(4);
            Debug.Log("[pn1 media] " + EcoDebug.WaterBreaksNow());
            sim.DebugGrowFarWood();
            if (!sim.WoodpileStands) Debug.Log("[pn1 media] " + NewStartView.DebugFirstWildWoodNow());
            sim.MarkFirstWildWoodSeen();
            if (!sim.StockSetDown) sim.SetDownStock();
            sim.DebugFireStage(2);
            Box.Rebuild();
            Debug.Log("[pn1 media] " + CampCheats.GrowTree());
            sim.DebugAddKids(4, 7f);
            sim.State.food = Mathf.Max(sim.State.food, 4f * sim.State.notables.Count(n => n.alive));
            CampLife.OnlyCheats = true;
            for (int calm = 0, f = 0; calm < 120 && f < 1500; f++) { calm = HeldMomentStage.DebugEndPlaying() == 0 ? calm + 1 : 0; yield return null; }
            yield return Until(() => FirstWaterView.Instance == null || !FirstWaterView.Instance.Playing, 60f);
            PeopleView.DebugSetHour(hour);
            yield return new WaitForSecondsRealtime(2f);
            GlassMotion.Instant = false;
        }

        [UnityTest, Timeout(900000)]
        public IEnumerator Pn1_07_FruitPick()
        {
            yield return Lived(10f, W, H);
            var sim = Sim;
            Debug.Log($"[pn1 media] 782 tree stage {sim.EcoTree.stage}, food {sim.State.food:0}");
            ShowMeDirector.BubblesOn = true;
            var d = ShowMeDirector.Instance;
            string r = CampCheats.FruitPick();
            Debug.Log("[pn1 media] 782 " + r);
            var tree = new Vector2(sim.EcoTree.x, sim.EcoTree.z);
            var seen = new HashSet<string>();
            string Line() { var s = d != null ? d.ShownCaption : null; if (!string.IsNullOrEmpty(s) && seen.Add(s)) Debug.Log("[pn1 media] 782 caption: " + s); return s; }
            Func<List<Vector2>> pts = () => { var p = Where_(a => true).Where(q => (q - tree).magnitude < 1.4f).ToList(); p.Add(tree); return p; };
            yield return Settle(pts, 100f, 38f, frames: 30);
            for (int i = 0; i < 40; i++) { Line(); yield return new WaitForSecondsRealtime(0.5f); if (i % 3 == 0) { Frame(pts(), false, 100f, 38f); } if (i == 6 || i == 10 || i == 14) yield return Still("782", $"fruit_pick_{i:00}"); }
            Debug.Log("[pn1 media] 782 lines seen: " + string.Join(" | ", seen) + "; LastLine: " + (d != null ? d.LastLine : "?"));
            Assert.IsFalse(seen.Any(s => s.Contains("grows anything we can eat")), "no hunger line");
            Assert.IsFalse((d != null ? d.LastLine : "").Contains("grows anything we can eat"), "no hunger line (last)");
        }

        // ================================================================== 8: #564 first mist

        [UnityTest, Timeout(900000)]
        public IEnumerator Pn1_08_FirstMist()
        {
            UseLivingLand(true);
            GameUiPanel.AcceptInputUnfocused(true);
            yield return OpenSlice(W, H, seed: 4242);
            GameUiPanel.AcceptInputUnfocused(true);
            GlassMotion.Instant = false;
            ShowMeDirector.BubblesOn = true;
            var d = ShowMeDirector.Instance;
            var sim = Sim;
            Debug.Log($"[pn1 media] 564 start: era {sim.State.era}, firstMistWords {sim.State.eco.firstMistWords}, people {sim.State.notables.Count(n => n.alive)}");
            var said = new List<string>();
            string last = d != null ? d.LastLine : "";
            // the first mist that greens ground, through the real mist path (EcoMistView.Pulse)
            Debug.Log("[pn1 media] 564 " + EcoDebug.MistAt(-1.6f, 0.6f, 4f));
            bool shot = false;
            for (int i = 0; i < 80; i++)
            {
                yield return new WaitForSecondsRealtime(0.1f);
                if (d != null && d.LastLine != last) { last = d.LastLine; said.Add(last); Debug.Log("[pn1 media] 564 line: " + last); }
                if (!shot && d != null && d.ShownCaption == GlassVoice.FirstMistLine) { shot = true; yield return new WaitForSecondsRealtime(0.6f); yield return Still("564", "first_mist_caption"); }
            }
            Debug.Log($"[pn1 media] 564 natural: said {said.Count}: {string.Join(" | ", said)}; firstMistWords {sim.State.eco.firstMistWords}; shot {shot}");
            if (!sim.State.eco.firstMistWords)
            {
                Debug.Log("[pn1 media] 564 natural line did not show: the cheat: " + GlassVoice.DebugFirstMistLine());
                for (int i = 0; i < 30 && !shot; i++) { yield return new WaitForSecondsRealtime(0.1f); if (d != null && d.ShownCaption == GlassVoice.FirstMistLine) { shot = true; yield return new WaitForSecondsRealtime(0.6f); yield return Still("564", "first_mist_caption_cheat"); } }
            }
            // let the caption go, then a second mist: nothing is said
            yield return Until(() => d == null || d.ShownCaption != GlassVoice.FirstMistLine, 15f);
            yield return new WaitForSecondsRealtime(1f);
            string lastBefore = d != null ? d.LastLine : "";
            Debug.Log("[pn1 media] 564 second mist " + EcoDebug.MistAt(1.6f, 0.9f, 4f));
            bool again = false;
            for (int i = 0; i < 60; i++) { yield return new WaitForSecondsRealtime(0.1f); if (d != null && d.ShownCaption == GlassVoice.FirstMistLine) again = true; }
            yield return Still("564", "second_mist_after");
            Debug.Log($"[pn1 media] 564 second mist: the first-mist line shown again {again}; LastLine before '{lastBefore}' now '{(d != null ? d.LastLine : "")}'");
            Assert.IsFalse(again, "the first-mist line shows once");
        }

        // ================================================================== 9: #783 a held story scene at 750x1334

        [UnityTest, Timeout(900000)]
        public IEnumerator Pn1_09_SpeckBefore()
        {
            yield return Lived(10f, 750, 1334);
            string r = StoryMoment.DebugPlayNow(StoryEvent.TinkererBucket);
            Debug.Log("[pn1 media] 783 " + r);
            yield return new WaitForSecondsRealtime(6f);
            var rig = Rig;
            Debug.Log($"[pn1 media] 783 camera zoom {rig.zoom:0.00} pitch {rig.pitch:0} target {rig.target}");
            yield return Still("783", "held_scene_default_750");
            yield return new WaitForSecondsRealtime(4f);
            yield return Still("783", "held_scene_default_750_b");
        }
    }
}
