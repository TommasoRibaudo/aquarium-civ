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
        const float Pitch = 56f, MinDollPx = 64f, FrameEvery = 0.1f, MaxScene = 100f;
        /// <summary>v4: the statue's share of the screen's height (the nod films wider, so the walk in and on is in the frame).</summary>
        static float StatueShare = 0.14f;
        /// <summary>v4 (2026-11-26): the poses tried are on the statue's FRONT side only (v3 picked yaw -32 and filmed its back): the yaw that looks
        /// straight at its face (<see cref="CampLife.StatueDrawnYaw"/>) and 30 / 60 degrees either side, at two pitches that look over the roofs.</summary>
        static readonly float[] FrontOffsets = { 0f, -30f, 30f, -60f, 60f }, Pitches = { 56f, 62f };

        static SimCore Sim => GameClock.Instance != null ? GameClock.Instance.Sim : null;
        static PeopleView Pv => PeopleView.Find();
        static BoxCamera Rig => Camera.main.GetComponent<BoxCamera>();
        static BoxView Box => UnityEngine.Object.FindAnyObjectByType<BoxView>();

        [TearDown]
        public void Restore()
        {
            GlassMotion.Instant = false;
            CampLife.OnlyCheats = false;
            aimYaw = 28f; aimPitch = Pitch; StatueShare = 0.14f;
            var pv = PeopleView.Find(); if (pv != null) pv.timeScale = 1f;
            var rig = Camera.main != null ? Camera.main.GetComponent<BoxCamera>() : null;
            if (rig != null) { rig.minZoom = CameraReach.MinZoom; rig.minYaw = -30f; rig.maxYaw = 55f; }
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
            Debug.Log("[776 shots] " + AsGrown()); // v2: what a real grown village has (the hunger line and the first-seed toast showed in the v1 raising)
            yield return new WaitForSeconds(3f);
            GlassMotion.Instant = false; // real time
            haze = $"grime {Sim.State.grime:0.00}, overgrowth {Sim.State.overgrowth:0.00}, fog {RenderSettings.fog} {RenderSettings.fogMode} d{RenderSettings.fogDensity:0.000} {RenderSettings.fogStartDistance:0.0}-{RenderSettings.fogEndDistance:0.0}";
            Debug.Log("[776 shots] haze at staging: " + haze);
            Assert.AreEqual(town ? Era.Town : Era.Village, Sim.State.era);
            Assert.IsNull(NewStartView.HungerLineFor(Sim), "a grown village says no hunger line of bare land");
            Assert.IsFalse(Sim.CanPlantTree, "a grown village has its Keeper's tree: no first-seed toast");
        }

        /// <summary>v2: the staged village is jumped to by the era cheat, so its land never fed anyone (no bush, no rabbits: the carried basket ran
        /// out, and the hunger line "The ground is bare..." showed) and the Keeper's tree was never planted (the first-seed toast showed). A real
        /// village has both: its people only grow past the camp on food the land gives (births need food over 1.5 x the people, SimCore.Step).
        /// Called in the same frame as the staging, before the HUD's kit looks at the seed.</summary>
        static string AsGrown()
        {
            var sim = Sim;
            if (sim == null || !sim.NewStartOn) return "an old box: nothing to grow";
            string r = sim.CanPlantTree ? EcoDebug.PlantAtEdge() : "the Keeper's tree stands";
            if (!sim.LandGivesFood) { sim.State.eco.milestoneKeys.Add(EcoMilestones.FirstBush); sim.State.eco.milestoneMonths.Add(sim.State.month); r += "; the land gives food"; }
            sim.State.food = Math.Max(sim.State.food, 200f);
            return r;
        }

        static string haze = "";

        /// <summary>v4 (2026-11-26): the v3 town shots looked milky. A still as staged (dirty), then the glass wiped clean the way the debug panel's
        /// "Clean all" does (grime and overgrowth to 0, the panes' masks fade), a still again: the two side by side say what the haze was.</summary>
        IEnumerator CleanGlass(string beat)
        {
            yield return StillOnStatue(beat, "haze-as-staged");
            var cam = Camera.main;
            string where = $"camera {cam.transform.position} near {cam.nearClipPlane:0.000}, zoom {Rig.zoom:0.000}";
            Sim.CleanAll();
            yield return new WaitForSeconds(4f);
            yield return StillOnStatue(beat, "haze-cleaned");
            haze += $"; {where}; after Clean all: grime {Sim.State.grime:0.00}";
            // still milky after the wipe (first v4 run): what is drawn between the camera and the statue (renderer bounds the sight line crosses)?
            StatueWorld(out var foot, out var top);
            var mid = Vector3.Lerp(foot, top, 0.5f); var from = cam.transform.position; float dist = (mid - from).magnitude;
            var ray = new Ray(from, (mid - from).normalized);
            var between = new List<Renderer>();
            foreach (var rr in Resources.FindObjectsOfTypeAll<Renderer>())
                if (rr != null && rr.enabled && !rr.forceRenderingOff && rr.gameObject.activeInHierarchy && rr.bounds.IntersectRay(ray, out float hit) && hit < dist - 0.05f
                    && !(rr.bounds.Contains(mid))) between.Add(rr);
            haze += "; between: " + string.Join(", ", between.Select(b => $"{b.name}[{(b.sharedMaterial != null ? b.sharedMaterial.name : "-")}] {b.bounds.min:F2}-{b.bounds.max:F2}"));
            foreach (var b in between) b.forceRenderingOff = true;
            yield return StillOnStatue(beat, "haze-nothing-between");
            foreach (var b in between) if (b != null) b.forceRenderingOff = false;
            var vols = UnityEngine.Object.FindObjectsByType<UnityEngine.Rendering.Volume>(FindObjectsSortMode.None);
            haze += "; volumes: " + string.Join(", ", vols.Select(v => v.name + (v.sharedProfile != null ? ":" + string.Join("/", v.sharedProfile.components.Where(c => c.active).Select(c => c.GetType().Name)) : "")));
            var w0 = vols.Select(v => v.weight).ToArray();
            foreach (var v in vols) v.weight = 0f;
            yield return StillOnStatue(beat, "haze-no-postfx");
            for (int i = 0; i < vols.Length; i++) vols[i].weight = w0[i];
            File.WriteAllText(OutDir(beat) + "haze.txt", haze);
            Debug.Log($"[776 shots] {beat} haze: {haze}");
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

        /// <summary>The carved statue's height on screen (px): its foot to the top of the head.</summary>
        static float StatuePx()
        {
            var cam = Camera.main;
            StatueWorld(out var foot, out var top);
            return Mathf.Abs(cam.WorldToScreenPoint(top).y - cam.WorldToScreenPoint(foot).y);
        }

        /// <summary>v3 (2026-11-26): the statue's foot and head in world space, from its own renderer's bounds when it is drawn (the v2 run showed the
        /// box-root sum off-centre and ~1.5 x short in the village box), else from the drawn spot. The height is never less than the carved figure's.</summary>
        static void StatueWorld(out Vector3 foot, out Vector3 top)
        {
            var r = StatueRenderer();
            var box = Box; var at = Pv.Camp.StatueDrawnAt;
            if (r != null)
            {
                var b = r.bounds;
                float h = Mathf.Max(b.size.y, PegDoll.StatueHeight * r.transform.lossyScale.y);
                foot = new Vector3(b.center.x, b.min.y, b.center.z); top = foot + Vector3.up * h;
                return;
            }
            foot = box.Root.TransformPoint(new Vector3(at.x, box.SurfaceHeight(at.x, at.y), at.y));
            top = box.Root.TransformPoint(new Vector3(at.x, box.SurfaceHeight(at.x, at.y) + PegDoll.StatueHeight, at.y));
        }

        /// <summary>v2 (2026-11-26): the camera on the statue: the target its middle at screen centre, the zoom that makes the statue
        /// <see cref="StatueShare"/> of the screen's height and a person at least <see cref="MinDollPx"/> tall, from <see cref="aimYaw"/> /
        /// <see cref="aimPitch"/> (picked by <see cref="AimByCover"/>). A snap lands at once; otherwise it eases.</summary>
        static void Frame(bool snap)
        {
            var rig = Rig; var box = Box;
            rig.CancelFlight();
            rig.minZoom = 0.03f; rig.minYaw = -180f; rig.maxYaw = 180f;
            StatueWorld(out var foot, out var top);
            var at = Pv.Camp.StatueDrawnAt;
            var w = Vector3.Lerp(foot, top, 0.5f);
            rig.target = snap ? w : Vector3.Lerp(rig.target, w, 0.2f);
            rig.pitch = aimPitch; rig.yaw = aimYaw;
            rig.Apply();
            for (int i = 0; i < (snap ? 4 : 1); i++)
            {
                float sp = StatuePx(), dp = PersonPx(rig, box, at);
                if (sp <= 1f || dp <= 1f) break;
                float want = rig.zoom * sp / (StatueShare * Screen.height); // the statue sets it: PersonPx reads a doll ~2.5 x short (v2 run 1), a person is ~0.4 x the statue on screen
                rig.zoom = Mathf.Clamp(snap ? want : Mathf.Lerp(rig.zoom, want, 0.2f), 0.03f, rig.maxZoom);
                rig.Apply();
            }
        }

        /// <summary>The yaw / pitch the camera looks in from (set by <see cref="AimByCover"/>).</summary>
        static float aimYaw = 28f, aimPitch = Pitch;
        static string aimLog = "";

        /// <summary>The statue's own renderer (a "Story thing" whose mesh is a Statue_ mesh), or null.</summary>
        static Renderer StatueRenderer()
        {
            foreach (var mf in Resources.FindObjectsOfTypeAll<MeshFilter>()) // the story things are DontSave: FindObjectsByType skips them
                if (mf.sharedMesh != null && mf.sharedMesh.name.StartsWith("Statue_") && mf.sharedMesh.name != "Statue_Bunch" && mf.sharedMesh.name != "Statue_Sprig"
                    && mf.gameObject.activeInHierarchy && mf.TryGetComponent<Renderer>(out var r)) return r;
            return null;
        }

        /// <summary>The screen box (px) of the statue and the foot of it where its people stand: <paramref name="reach"/> u round it, up to the statue's head.</summary>
        static RectInt ActorBox(float reach)
        {
            var cam = Camera.main;
            StatueWorld(out var foot, out var top);
            float h = top.y - foot.y + 0.01f;
            float x0 = float.MaxValue, y0 = float.MaxValue, x1 = float.MinValue, y1 = float.MinValue;
            foreach (float dx in new[] { -reach, reach }) foreach (float dz in new[] { -reach, reach }) foreach (float up in new[] { 0f, h })
            {
                var v = cam.WorldToScreenPoint(foot + new Vector3(dx, up, dz));
                x0 = Mathf.Min(x0, v.x); x1 = Mathf.Max(x1, v.x); y0 = Mathf.Min(y0, v.y); y1 = Mathf.Max(y1, v.y);
            }
            int ix0 = Mathf.Clamp((int)x0, 0, Screen.width - 1), iy0 = Mathf.Clamp((int)y0, 0, Screen.height - 1);
            int ix1 = Mathf.Clamp((int)x1, 0, Screen.width - 1), iy1 = Mathf.Clamp((int)y1, 0, Screen.height - 1);
            return new RectInt(ix0, iy0, Mathf.Max(1, ix1 - ix0), Mathf.Max(1, iy1 - iy0));
        }

        static Color32[] Grab(RectInt r)
        {
            var tex = ScreenCapture.CaptureScreenshotAsTexture();
            var all = tex.GetPixels32(); int tw = tex.width, th = tex.height;
            var o = new Color32[r.width * r.height];
            for (int y = 0; y < r.height; y++)
                for (int x = 0; x < r.width; x++)
                    o[y * r.width + x] = all[Mathf.Min(r.y + y, th - 1) * tw + Mathf.Min(r.x + x, tw - 1)];
            UnityEngine.Object.Destroy(tex);
            return o;
        }

        static int Diff(Color32[] a, Color32[] b)
        {
            int n = 0;
            for (int i = 0; i < a.Length; i++) if (Mathf.Abs(a[i].r - b[i].r) + Mathf.Abs(a[i].g - b[i].g) + Mathf.Abs(a[i].b - b[i].b) > 30) n++;
            return n;
        }

        /// <summary>One frame at the pose, the clock held, then the pixels of <paramref name="r"/>.</summary>
        IEnumerator GrabAt(RectInt r, Color32[][] into, int slot)
        {
            yield return null;
            Frame(true);
            yield return new WaitForEndOfFrame();
            into[slot] = Grab(r);
        }

        /// <summary>v2: the yaw (30 degrees apart) and pitch the statue is least covered from. The town's houses are baked into the land's four chunk
        /// meshes, so no lot can be switched off alone: per pose, with the clock held, the statue is switched off and on instead, once with the box
        /// drawn (the statue pixels that show) and once with every other renderer off (all of its pixels); compared inside the statue's screen box
        /// only (the wind moves trees elsewhere). Cover = 1 - shown / all. A back view costs a little; the least-covered pose wins.</summary>
        IEnumerator AimByCover()
        {
            var statue = StatueRenderer();
            var log = new StringBuilder(statue == null ? "no statue renderer;" : "");
            float best = float.MaxValue, bestYaw = 28f, bestPitch = Pitch;
            if (statue != null)
            {
                var others = Resources.FindObjectsOfTypeAll<Renderer>().Where(r => r != statue && r.enabled && !r.forceRenderingOff && r.gameObject.activeInHierarchy && r.gameObject.scene.IsValid()).ToList();
                float faceYaw = Pv.Camp.StatueDrawnYaw; // it faces its local -z; the camera at this yaw sits on that side (BoxCamera.Apply)
                float fy = faceYaw * Mathf.Deg2Rad;
                var face = new Vector2(-Mathf.Sin(fy), -Mathf.Cos(fy));
                log.Append($" faces yaw {faceYaw:0};");
                Freeze(true);
                var g = new Color32[4][]; var wide = new Color32[1][];
                foreach (float pitch in Pitches)
                    foreach (float off in FrontOffsets)
                    {
                        float yaw = Mathf.Repeat(faceYaw + off + 180f, 360f) - 180f;
                        aimYaw = yaw; aimPitch = pitch;
                        Frame(true); yield return null; Frame(true);
                        var rect = ActorBox(0.1f);
                        var around = ActorBox(0.3f); // where the use's people stand and walk: a roof over it hides them (v3: a roof over the left third)
                        yield return GrabAt(around, wide, 0);
                        float roof = RoofShare(wide[0]);
                        yield return GrabAt(rect, g, 0);
                        statue.forceRenderingOff = true; yield return GrabAt(rect, g, 1);
                        foreach (var o in others) if (o != null) o.forceRenderingOff = true;
                        yield return GrabAt(rect, g, 3);
                        statue.forceRenderingOff = false; yield return GrabAt(rect, g, 2);
                        foreach (var o in others) if (o != null) o.forceRenderingOff = false;
                        int shown = Diff(g[0], g[1]), all = Diff(g[2], g[3]);
                        float cover = all <= 0 ? 1f : Mathf.Clamp01(1f - shown / (float)all);
                        var dir = new Vector2(-Mathf.Sin(yaw * Mathf.Deg2Rad), -Mathf.Cos(yaw * Mathf.Deg2Rad)); // from the statue toward the camera
                        float score = cover + 0.6f * roof + 0.15f * (1f - Vector2.Dot(face, dir)) * 0.5f + 0.0005f * (pitch - Pitch);
                        log.Append($" y{yaw:0}/p{pitch:0} cover {cover:0.00} ({shown}/{all}) roof {roof:0.00}");
                        if (score < best) { best = score; bestYaw = yaw; bestPitch = pitch; }
                    }
            }
            aimYaw = bestYaw; aimPitch = bestPitch;
            Frame(true);
            StatueWorld(out var f0, out var t0);
            var sc = Camera.main.WorldToScreenPoint(Vector3.Lerp(f0, t0, 0.5f));
            aimLog = $"yaw {bestYaw:0} pitch {bestPitch:0} statue at {sc.x:0},{sc.y:0} px {StatuePx():0} of {Screen.width}x{Screen.height} |{log}";
            Debug.Log("[776 shots] aim by cover: " + aimLog);
        }

        /// <summary>v4: the share of pixels that are roof (the town's terracotta and dark-red roofs: hue under 28 degrees, saturated), the grass and
        /// paths being green or tan. People in red are a few pixels; a roof over the actors is a big share.</summary>
        static float RoofShare(Color32[] px)
        {
            if (px == null || px.Length == 0) return 0f;
            int n = 0;
            for (int i = 0; i < px.Length; i++)
            {
                Color.RGBToHSV(px[i], out float h, out float s, out float v);
                if ((h < 28f / 360f || h > 345f / 360f) && s > 0.38f && v > 0.25f) n++;
            }
            return n / (float)px.Length;
        }

        /// <summary>The clock held still while the camera lands (the lesson of the 788 shots), then running again.</summary>
        static void Freeze(bool on) { var pv = Pv; if (pv != null) pv.timeScale = on ? 0f : 1f; }

        /// <summary>v2: films real time, statue-centred from the pose <see cref="AimByCover"/> picked: from <paramref name="preRoll"/> s before
        /// <paramref name="start"/> says it started the use (null: the scene already plays) until the scene ends plus <paramref name="postRoll"/> s
        /// (at most <paramref name="max"/> s). A JPG about every 0.1 s into media/statue776/&lt;beat&gt;/, each frame's real time in times.txt.</summary>
        IEnumerator Film(string beat, CampUse use, float max, List<int> frames, Func<bool, bool> start = null, float preRoll = 0f, float postRoll = 1.5f,
            float startBy = 30f, List<bool> played = null)
        {
            string dir = OutDir(beat);
            foreach (var old in Directory.GetFiles(dir, "f*.jpg")) File.Delete(old);
            var times = new StringBuilder();
            yield return AimByCover();
            Freeze(true);
            for (int settle = 0; settle < 6; settle++) { Frame(settle == 0); yield return null; }
            Freeze(false);
            float t0 = Time.realtimeSinceStartup, lastAt = -1f, endAt = -1f; int n = 0;
            bool started = start == null, seen = start == null;
            while (Time.realtimeSinceStartup - t0 < max)
            {
                float now = Time.realtimeSinceStartup - t0;
                if (!started && now >= preRoll && (start(false) || now > preRoll + startBy && start(true))) // v4: past the wait the cheat is pressed anyway (v3 never pressed it)
                {
                    started = true;
                    File.WriteAllText(dir + "press.txt", now.ToString("0.000", CultureInfo.InvariantCulture) + " s, frame " + n);
                }
                bool on = Pv.Camp.Playing(use);
                if (on) seen = true;
                if (started && seen && !on && endAt < 0f) endAt = now;
                if (endAt >= 0f && now - endAt >= postRoll) break;
                if (started && !seen && now > preRoll + startBy + 6f) break;
                Frame(false);
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
            File.WriteAllText(dir + "aim.txt", aimLog);
            frames.Add(n);
            played?.Add(seen);
            Debug.Log($"[776 shots] {beat}: {n} frames over {Time.realtimeSinceStartup - t0:0.0} s of real time; aim {aimLog}");
        }

        /// <summary>A still of what stands: the camera on the statue from the least-covered pose, then a PNG.</summary>
        IEnumerator StillOnStatue(string beat, string name)
        {
            yield return AimByCover();
            Freeze(true);
            for (int i = 0; i < 20; i++) { Frame(i == 0); yield return null; }
            yield return Still(beat, name);
            Freeze(false);
        }

        IEnumerator After(string beat) => StillOnStatue(beat, "after");

        static IEnumerator Still(string beat, string name)
        {
            yield return new WaitForEndOfFrame();
            var tex = ScreenCapture.CaptureScreenshotAsTexture();
            File.WriteAllBytes(OutDir(beat) + name + ".png", tex.EncodeToPNG());
            UnityEngine.Object.Destroy(tex);
        }

        /// <summary>A grown-up walking (moved since the last look) within <paramref name="reach"/> u of the statue now: the nod's "in passing".</summary>
        static bool WalkerNear(float reach)
        {
            var pv = Pv; var at = pv.Camp.StatueDrawnAt;
            var who = new List<PersonRef>(); var xs = new List<float>(); var zs = new List<float>();
            pv.StandingNow(who, xs, zs);
            bool found = false; float nearest = float.MaxValue; int walker = -1, near = -1;
            for (int i = 0; i < who.Count; i++)
            {
                var st = pv.Planner.StageOf(who[i]);
                if (st != LifeStage.Adult && st != LifeStage.Elder) continue;
                var p = new Vector2(xs[i], zs[i]);
                float d = (p - at).magnitude;
                if (d < nearest) { nearest = d; near = who[i].Key; } // v4: the cheat nods the NEAREST grown-up: the walker must be that one
                if (d <= reach && d >= 0.15f && lastSeen.TryGetValue(who[i].Key, out var q) && (p - q).magnitude > 0.002f && OnScreen(p)) { found = true; walker = who[i].Key; }
                lastSeen[who[i].Key] = p;
            }
            found = found && walker == near;
            if (found) { lastSeen.Clear(); lastWalker = walker; }
            return found;
        }
        static readonly Dictionary<int, Vector2> lastSeen = new();
        static int lastWalker = -1;

        /// <summary>v3: the walker is in the frame (the v2 nod was pressed for someone off screen: the clip showed no one).</summary>
        static bool OnScreen(Vector2 p)
        {
            var box = Box;
            var v = Camera.main.WorldToViewportPoint(box.Root.TransformPoint(new Vector3(p.x, box.SurfaceHeight(p.x, p.y), p.y)));
            return v.z > 0f && v.x > 0.12f && v.x < 0.88f && v.y > 0.15f && v.y < 0.85f;
        }

        /// <summary>A use on the standing statue: the cheat the user presses (once <paramref name="when"/> holds), the scene must start; filmed from
        /// <paramref name="preRoll"/> s before the cheat to 2 s after its end; the before and after stills.</summary>
        IEnumerator Use(string beat, CampUse use, float hour, Func<string> cheat, bool town = true, float preRoll = 2f, Func<bool> when = null,
            float share = 0.18f, float startBy = 30f, float max = MaxScene)
        {
            yield return VillageBox(hour, town);
            var pv = Pv;
            Debug.Log("[776 shots] " + CampCheats.StatueStand());
            yield return Until(() => pv.Camp.StatueDrawn, 8f);
            Assert.IsTrue(pv.Camp.StatueDrawn, "the statue stands");
            PeopleView.DebugSetHour(hour);
            yield return new WaitForSeconds(1.5f);
            StatueShare = share;
            yield return CleanGlass(beat);
            yield return StillOnStatue(beat, "before");
            string r = null;
            var frames = new List<int>(); var played = new List<bool>();
            yield return Film(beat, use, max, frames, force =>
            {
                if (!force && when != null && !when()) return false;
                r = cheat(); Debug.Log($"[776 shots] {beat}: {(force ? "pressed at the wait's end (no walker came by): " : "")}{r}");
                var sc = pv.Camp.Scenes.LastOrDefault(x => x.use == use);
                if (sc != null && sc.keys.Count > 0) Debug.Log($"[776 shots] {beat}: the scene's person {sc.keys[0]}, walker seen {lastWalker}");
                return true;
            }, preRoll, 2f, startBy, played);
            Assert.IsTrue(played[0], $"{beat}: the use plays ({r})");
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
        public IEnumerator B81e_ANodInPassing() => Use("e-nod", CampUse.StatueNod, 11f, CampCheats.StatueNod, preRoll: 3f, when: () => WalkerNear(0.45f),
            share: 0.09f, startBy: 150f, max: 200f); // v4: pressed as a grown-up walks past on screen (the nearest one, so the cheat picks them), filmed wider, waited up to 150 s (v3: nobody came in 30 s)

        [UnityTest, Explicit, Timeout(900000)]
        public IEnumerator B81f_TheGreensSweep() => Use("f-sweep", CampUse.StatueSweep, 9f, CampCheats.StatueSweep);

        /// <summary>The look in every season (the wear: a snow cap in winter, leaves in autumn): one still each, statue-centred from the least-covered
        /// pose. v2: each season reached with the AnimLab's ToSeason (the winter still was missing).</summary>
        [UnityTest, Explicit, Timeout(900000)]
        public IEnumerator B81_TheLookInEverySeason()
        {
            yield return VillageBox(10f, false);
            var pv = Pv;
            Debug.Log("[776 shots] " + CampCheats.StatueStand());
            yield return Until(() => pv.Camp.StatueDrawn, 8f);
            foreach (var season in new[] { Season.Spring, Season.Summer, Season.Autumn, Season.Winter })
            {
                Debug.Log("[776 shots] season: " + CampCheats.ToSeason(season));
                yield return null;
                PeopleView.DebugSetHour(11f); // the view's day follows the sim's month (its season with it)
                yield return new WaitForSeconds(2f);
                yield return Until(() => pv.Camp.StatueDrawn, 8f);
                Assert.IsTrue(pv.Camp.StatueDrawn, season + ": the statue stands");
                yield return StillOnStatue("season", $"{(int)LifeClock.SeasonOf(pv.Now.day)}-{LifeClock.SeasonOf(pv.Now.day)}-{pv.Camp.StatueWearDrawn}");
            }
            Assert.AreEqual(StatueWear.Snow, pv.Camp.StatueWearDrawn, "winter: the snow cap");
            CloseSlice();
        }
    }
}
