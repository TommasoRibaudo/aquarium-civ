// #789 (animation-specs B82, approved 2026-11-26): the last evening filmed in the box, for the user's sign-off of its four new motions (the hand
// on the shoulder, the kid's lean on the knee, the help up, the pair walk). Explicit (category "LastEvening789Shots"): each test opens a living box
// at phone size (750x1334), wipes the glass (Sim.CleanAll, as the panel's "Clean all"), presses the cheat the user would press ("Last evening:
// play now", or "with a dog") and films the scene in REAL time as JPGs about every 0.1 s into Temp/agent/media/lastevening789/<clip>/f###.jpg
// (times.txt: each frame's real time; scene.txt: real time, the scene's own clock; marks.txt: the beats' scene times). The camera is framed
// from the cast's own doll bounds (and the dog's spot), and its pose picked by a bounded search (a few yaws on the side the people face, two
// pitches, with the clock held): the least hidden cast wins. It re-aims twice: when the walk home starts (from the door's side) and at the door
// (from the fire's side, for the wave). GIFs and contact sheets are made outside Unity. The pictures are judged by looking at them.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using AquariumCiv.Game;
using AquariumCiv.GameUI;
using AquariumCiv.Life;
using AquariumCiv.Lighting;
using AquariumCiv.Sim;
using AquariumCiv.View.ShowMe;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AquariumCiv.View.PlayTests
{
    [Category("LastEvening789Shots")]
    public class LastEveningShots789 : PlayTestBase
    {
        const int W = 750, H = 1334;
        const float FrameEvery = 0.1f, PersonPx = 100f, ClosePx = 170f;
        static readonly float[] Offsets = { 0f, -30f, 30f, -60f, 60f }, Pitches = { 46f, 58f };

        static SimCore Sim => GameClock.Instance != null ? GameClock.Instance.Sim : null;
        static PeopleView Pv => PeopleView.Find();
        static BoxCamera Rig => Camera.main.GetComponent<BoxCamera>();
        static BoxView Box => UnityEngine.Object.FindAnyObjectByType<BoxView>();
        static readonly FieldInfo ClockField = typeof(CampLife).GetField("clock", BindingFlags.NonPublic | BindingFlags.Instance);

        float aimYaw = 28f, aimPitch = 52f, zoomNow = 0.2f;
        string aimLog = "";

        [TearDown]
        public void Restore()
        {
            GlassMotion.Instant = false;
            CampLife.OnlyCheats = false;
            var pv = PeopleView.Find(); if (pv != null) pv.timeScale = 1f;
            var rig = Camera.main != null ? Camera.main.GetComponent<BoxCamera>() : null;
            if (rig != null) { rig.enabled = true; rig.minZoom = CameraReach.MinZoom; rig.minYaw = -30f; rig.maxYaw = 55f; }
        }

        static string OutDir(string clip) { string d = Shots + "media/lastevening789/" + clip + "/"; Directory.CreateDirectory(d); return d; }
        static float SceneClock(PeopleView pv) => ClockField != null ? (float)ClockField.GetValue(pv.Camp) : 0f;
        static void Freeze(bool on) { var pv = Pv; if (pv != null) pv.timeScale = on ? 0f : 1f; }

        // ================================================================== the cast's bounds on screen
        /// <summary>The cast's doll feet and tops (world), and the dog's spot, now.</summary>
        static List<Vector3> CastPoints(CampScene sc, out float tallest)
        {
            var pv = Pv; var box = Box; var pts = new List<Vector3>(); tallest = 0f;
            var cam = Camera.main;
            foreach (int k in sc.keys)
            {
                var who = PersonRef.Notable(k);
                if (!pv.TryGetDollTop(who, out var top) || !pv.TryGetDollFeet(who, out var feet)) continue;
                pts.Add(top); pts.Add(feet);
                tallest = Mathf.Max(tallest, Mathf.Abs(cam.WorldToScreenPoint(top).y - cam.WorldToScreenPoint(feet).y));
            }
            if (sc.dog >= 0 && pv.Camp.DogAt(sc.dog) is Vector2 d)
                pts.Add(box.Root.TransformPoint(new Vector3(d.x, box.SurfaceHeight(d.x, d.y) + 0.02f, d.y)));
            return pts;
        }

        static Rect ScreenRect(List<Vector3> pts)
        {
            var cam = Camera.main;
            float x0 = float.MaxValue, y0 = float.MaxValue, x1 = float.MinValue, y1 = float.MinValue;
            foreach (var p in pts) { var v = cam.WorldToScreenPoint(p); x0 = Mathf.Min(x0, v.x); x1 = Mathf.Max(x1, v.x); y0 = Mathf.Min(y0, v.y); y1 = Mathf.Max(y1, v.y); }
            return pts.Count == 0 ? new Rect(0, 0, 0, 0) : Rect.MinMaxRect(x0, y0, x1, y1);
        }

        /// <summary>The elder's doll height on screen (px), else the tallest of the cast.</summary>
        static float ElderPx(CampScene sc)
        {
            var pv = Pv; var cam = Camera.main; var who = PersonRef.Notable(sc.keys[0]);
            if (!pv.TryGetDollTop(who, out var top) || !pv.TryGetDollFeet(who, out var feet)) { CastPoints(sc, out float t); return t; }
            return Mathf.Abs(cam.WorldToScreenPoint(top).y - cam.WorldToScreenPoint(feet).y);
        }

        /// <summary>The camera on the cast: the target the middle of their bounds; the zoom that makes the elder about <see cref="PersonPx"/> tall
        /// (v2: v1 filled the screen with two dolls ~300 px tall, the fire and the tent out of the frame), wider when the cast would not fit in
        /// ~75 % of the width and ~50 % of the height. A snap lands at once, else it eases (no jitter).</summary>
        void Frame(CampScene sc, bool snap)
        {
            var rig = Rig;
            rig.enabled = false; rig.CancelFlight(); rig.minZoom = 0.01f; rig.minYaw = -180f; rig.maxYaw = 180f;
            var pts = CastPoints(sc, out _);
            if (pts.Count == 0) return;
            var mid = Vector3.zero; foreach (var p in pts) mid += p; mid /= pts.Count;
            var b = new Bounds(pts[0], Vector3.zero); foreach (var p in pts) b.Encapsulate(p);
            var want = Vector3.Lerp(b.center, mid, 0.5f);
            rig.target = snap ? want : Vector3.Lerp(rig.target, want, 0.12f);
            rig.yaw = aimYaw; rig.pitch = aimPitch; rig.zoom = zoomNow; rig.Apply(); rig.FitDepthNow();
            for (int i = 0; i < (snap ? 4 : 1); i++)
            {
                var r = ScreenRect(CastPoints(sc, out _));
                float ext = Mathf.Max(r.width / (0.75f * Screen.width), r.height / (0.5f * Screen.height));
                float px = ElderPx(sc);
                float st = SceneClock(Pv) - sc.t0;
                float wantPx = st >= sc.a - 0.5f && st < sc.d + 0.5f ? ClosePx : PersonPx; // v3: closer from the hand to the help up (the arms are the point)
                float z = Mathf.Max(rig.zoom * Mathf.Max(ext, 0.05f), px > 1f ? rig.zoom * px / wantPx : 0f);
                z = Mathf.Clamp(z, 0.03f, rig.maxZoom);
                zoomNow = snap ? z : Mathf.Lerp(zoomNow, z, 0.1f);
                rig.zoom = zoomNow; rig.Apply(); rig.FitDepthNow();
            }
        }

        static IEnumerator Grab(Color32[][] into, int slot)
        {
            yield return new WaitForEndOfFrame();
            var tex = ScreenCapture.CaptureScreenshotAsTexture();
            into[slot] = tex.GetPixels32();
            UnityEngine.Object.Destroy(tex);
        }

        static void Hide(PersonRef who, bool off)
        {
            if (!Pv.TryGetDollTransform(who, out var t)) return;
            foreach (var r in t.GetComponentsInChildren<Renderer>(true)) r.forceRenderingOff = off;
        }

        static RectInt RectOf(PersonRef who, out float hPx)
        {
            hPx = 0f; var pv = Pv; var cam = Camera.main;
            if (!pv.TryGetDollTop(who, out var top) || !pv.TryGetDollFeet(who, out var feet)) return new RectInt(0, 0, 0, 0);
            Vector3 t = cam.WorldToScreenPoint(top), f = cam.WorldToScreenPoint(feet);
            hPx = Mathf.Abs(t.y - f.y);
            float half = hPx * 0.5f;
            return new RectInt((int)(f.x - half), (int)Mathf.Min(f.y, t.y) - (int)(hPx * 0.1f), (int)(2 * half), (int)(hPx * 1.25f));
        }

        static int Diff(Color32[] a, Color32[] b, RectInt r, int w)
        {
            if (a == null || b == null || a.Length != b.Length) return 0;
            int n = 0, h = a.Length / w;
            for (int y = Mathf.Max(0, r.yMin); y < Mathf.Min(h, r.yMax); y++)
                for (int x = Mathf.Max(0, r.xMin); x < Mathf.Min(w, r.xMax); x++)
                {
                    int i = y * w + x; var p = a[i]; var q = b[i];
                    if (Mathf.Abs(p.r - q.r) + Mathf.Abs(p.g - q.g) + Mathf.Abs(p.b - q.b) > 24) n++;
                }
            return n;
        }

        static float YawFrom(Vector3 outWorld) => Mathf.Atan2(-outWorld.x, -outWorld.z) * Mathf.Rad2Deg;

        /// <summary>The bounded pose search, the clock held: yaws <see cref="Offsets"/> round <paramref name="frontYaw"/> (the side the people
        /// face) at <see cref="Pitches"/>; per pose each of <paramref name="who"/> is hidden in turn and the pixels that change counted (the
        /// hide/compare trick): the score is the least seen of them, less a little for leaving the front. Leaves the best pose set.</summary>
        IEnumerator Aim(CampScene sc, float frontYaw, IList<int> who, string tag)
        {
            Freeze(true);
            var bufs = new Color32[3][];
            float best = float.MinValue, bestYaw = frontYaw, bestPitch = Pitches[0];
            var log = new StringBuilder($"{tag}: front {frontYaw:0};");
            foreach (float pitch in Pitches)
                foreach (float off in Offsets)
                {
                    aimYaw = Mathf.Repeat(frontYaw + off + 180f, 360f) - 180f; aimPitch = pitch;
                    Frame(sc, true); yield return null; Frame(sc, true); yield return null;
                    yield return Grab(bufs, 0);
                    yield return Grab(bufs, 2);
                    int w = Screen.width;
                    float worst = 1f;
                    foreach (int k in who)
                    {
                        var p = PersonRef.Notable(k);
                        var r = RectOf(p, out float hPx);
                        if (hPx < 1f) continue;
                        Hide(p, true); yield return null; yield return Grab(bufs, 1); Hide(p, false);
                        float frac = Mathf.Clamp01((Diff(bufs[0], bufs[1], r, w) - Diff(bufs[0], bufs[2], r, w)) / (0.30f * hPx * hPx));
                        worst = Mathf.Min(worst, frac);
                    }
                    float score = worst - 0.12f * Mathf.Abs(off) / 60f;
                    log.Append($" y{aimYaw:0}/p{pitch:0} seen {worst:0.00}");
                    if (score > best) { best = score; bestYaw = aimYaw; bestPitch = pitch; }
                }
            aimYaw = bestYaw; aimPitch = bestPitch;
            for (int i = 0; i < 4; i++) { Frame(sc, true); yield return null; }
            aimLog += log + $" -> y{bestYaw:0}/p{bestPitch:0} elder {ElderPx(sc):0} px\n";
            Debug.Log("[789 shots] aim " + log + $" -> y{bestYaw:0}/p{bestPitch:0}");
            Freeze(false);
        }

        /// <summary>A cut to a new yaw without a search or a held clock (the pitch stays the first search's).</summary>
        void Recut(CampScene sc, float yaw, string tag)
        {
            aimYaw = Mathf.Repeat(yaw + 180f, 360f) - 180f;
            Frame(sc, true);
            aimLog += $"{tag}: cut to y{aimYaw:0}/p{aimPitch:0} elder {ElderPx(sc):0} px\n";
        }

        Vector3 WorldDir(Vector2 boxDir) => Box.Root.TransformDirection(new Vector3(boxDir.x, 0f, boxDir.y));

        // ================================================================== the clip
        IEnumerator Film(string clip, Func<string> cheat)
        {
            UseLivingLand(true);
            GameUiPanel.AcceptInputUnfocused(true);
            ShowMeDirector.BubblesOn = false;
            yield return OpenSlice(W, H, seed: 4242);
            GameUiPanel.AcceptInputUnfocused(true);
            ShowMeDirector.BubblesOn = false;
            yield return new WaitForSeconds(2f);
            var sim = Sim; var pv = Pv;
            sim.CleanAll(); // clean glass (the panel's "Clean all")
            Debug.Log("[789 shots] " + clip + ": family " + Family(sim));
            yield return new WaitForSeconds(4f); // the kid is drawn

            GlassMotion.Instant = false; // real time
            string r = cheat();
            Debug.Log("[789 shots] " + clip + ": " + r);
            yield return Until(() => pv.Camp.LastEveningScene != null, 20f);
            var sc = pv.Camp.LastEveningScene;
            Assert.IsNotNull(sc, $"it plays ({r}; why: {pv.Camp.LastEveningWhy})");
            if (clip.Contains("dog"))
            {
                var mine = sim.DogsOfKeeper(sc.keys[0]);
                string dogs = $"era {sim.State.era}, eco {sim.EcologyOn}, the elder's dogs: " + string.Join(", ", mine.Select(d => $"{d.name} {sim.DogStageOf(d)} at {pv.Camp.DogAt(d.id)?.ToString() ?? "not drawn"}"));
                Debug.Log("[789 shots] " + clip + ": " + dogs + "; why " + pv.Camp.LastEveningWhy + "; at the cast: " + pv.Camp.LastEveningDogNote);
                Assert.GreaterOrEqual(sc.dog, 0, "the dog is in the scene: " + dogs + "; at the cast: " + pv.Camp.LastEveningDogNote);
            }
            string dir = OutDir(clip);
            foreach (var old in Directory.GetFiles(dir, "f*.jpg")) File.Delete(old);
            aimLog = "";
            float tIn = sc.c, tDoor = sc.c - LastEveningScenes.InFor - LastEveningScenes.WaveFor - LastEveningScenes.TurnFor;
            File.WriteAllText(dir + "marks.txt", string.Format(CultureInfo.InvariantCulture,
                "hand {0:0.00}\nstars {1:0.00}\nrise {2:0.00}\nwalk {3:0.00}\ndoor {4:0.00}\nin {5:0.00}\nend {6:0.00}\ncast {7}\ndog {8}\n{9}\n",
                sc.a, sc.b, sc.b + LastEveningScenes.LookFor + LastEveningScenes.NodFor + LastEveningScenes.StillFor, sc.d, tDoor, tIn, sc.Length,
                string.Join(",", sc.keys), sc.dog, r));

            // the seat faces the fire: the camera on the fire's side sees their faces
            var seat = sc.focus; // CampScene's focus is the elder's seat
            yield return Aim(sc, YawFrom(WorldDir((FireOf(sc) - seat).normalized)), sc.keys, "at the fire");
            var times = new StringBuilder(); var scene = new StringBuilder(); var spots = new StringBuilder();
            float t0 = Time.realtimeSinceStartup, lastAt = -1f; int n = 0; bool walkAimed = false, doorAimed = false;
            float minPx = float.MaxValue, maxPx = 0f;
            while (pv.Camp.Scenes.Contains(sc) && Time.realtimeSinceStartup - t0 < 240f)
            {
                float st = SceneClock(pv) - sc.t0;
                if (!walkAimed && st >= sc.d)
                {
                    walkAimed = true;
                    Vector2 door = DoorOf(sc);
                    // v4: no search here (the search holds the scene's clock but the dolls' steps run on, so the walk was over before the first
                    // frame) and not from beyond the door (the tent stood in front of them): from the side, a little ahead of them
                    Recut(sc, YawFrom(WorldDir((door - sc.focus).normalized)) + 65f, "the walk home");
                }
                if (!doorAimed && st >= tDoor - 0.2f)
                {
                    doorAimed = true;
                    Recut(sc, YawFrom(WorldDir((sc.focus - DoorOf(sc)).normalized)) + 20f, "at the door"); // from the fire's side: the wave back to the fire
                }
                Frame(sc, false);
                float now = Time.realtimeSinceStartup - t0;
                if (now - lastAt >= FrameEvery)
                {
                    yield return new WaitForEndOfFrame();
                    var tex = ScreenCapture.CaptureScreenshotAsTexture();
                    File.WriteAllBytes(dir + $"f{n:000}.jpg", tex.EncodeToJPG(85));
                    UnityEngine.Object.Destroy(tex);
                    times.AppendLine(now.ToString("0.000", CultureInfo.InvariantCulture));
                    float px = ElderPx(sc);
                    if (st >= 3f && st < tIn && px > 1f) { minPx = Mathf.Min(minPx, px); maxPx = Mathf.Max(maxPx, px); }
                    scene.AppendLine(string.Format(CultureInfo.InvariantCulture, "{0:0.000} {1:0.000} {2:0}", now, SceneClock(pv) - sc.t0, px));
                    if (n % 20 == 0) // every ~2 s: where each of the cast is drawn and why (the planner's spot or the scene's part)
                        foreach (int k in sc.keys)
                        {
                            bool cued = sc.TryPart(k, SceneClock(pv), out var part, out _);
                            string act = pv.TryGetActivityOf(PersonRef.Notable(k), out var pa) ? $"{pa.kind} at {pa.place.kind} hunt {pa.hunt} tent {pa.tent} moving {pa.moving}" : "-";
                            spots.AppendLine($"{st:0.0} {k} {sim.FindNotable(k)?.name} use {pv.Camp.UseOf(k)} cued {cued} part {(cued ? part.pos.ToString("0.00") + " " + part.style : "-")} | {act} | {pv.DebugSpotOf(PersonRef.Notable(k))}");
                        }
                    n++; lastAt = now;
                }
                else yield return null;
            }
            File.WriteAllText(dir + "times.txt", times.ToString());
            File.WriteAllText(dir + "scene.txt", scene.ToString());
            File.WriteAllText(dir + "spots.txt", spots.ToString());
            File.WriteAllText(dir + "aim.txt", aimLog + $"elder px {minPx:0}-{maxPx:0}; caption: {pv.Camp.LastEveningCaption}\n");
            Debug.Log($"[789 shots] {clip}: {n} frames, elder {minPx:0}-{maxPx:0} px; {aimLog}");
            Assert.IsFalse(pv.Camp.Scenes.Contains(sc), "the scene ends");
            Assert.Greater(n, 100, "frames of the scene");
            Assert.GreaterOrEqual(minPx, 45f, "a person about 50 px tall or more");
            CloseSlice();
        }

        /// <summary>v2: a new living land is five grown-ups with no kids, so the cheat's elder had only a partner (no kid at the knee, no second
        /// helper). The clips need the whole cast: the oldest grown-up is made the eldest (62) with a partner, a grown child (another grown-up's
        /// parent set to them) and a grandchild of 7 (the debug panel's "add kids", their parent set to the grown child).</summary>
        static string Family(SimCore sim)
        {
            var grown = sim.State.notables.Where(x => x.alive && x.age >= SimCore.AdultAge).OrderByDescending(x => x.age).ThenBy(x => x.id).ToList();
            if (grown.Count < 3) return "not enough grown-ups";
            var e = grown[0];
            e.age = Mathf.Max(62f, e.age);
            var p = sim.FindNotable(e.partnerId) is Notable pp && pp.alive ? pp : grown.First(x => x != e && !(sim.FindNotable(x.partnerId) is Notable q && q.alive));
            e.partnerId = p.id; p.partnerId = e.id;
            var c = grown.First(x => x != e && x != p);
            c.parentId = e.id;
            int before = sim.State.notables.Count;
            sim.DebugAddKids(1, 7f);
            var k = sim.State.notables.Skip(before).FirstOrDefault();
            if (k != null) k.parentId = c.id;
            return $"{e.name} {e.age:0} (partner {p.name}, child {c.name}, grandchild {k?.name ?? "none"})";
        }

        /// <summary>The fire (box x, z): the seat is <see cref="LastEveningScenes.SeatR"/> from it; the elder's first sitting part faces it.</summary>
        static Vector2 FireOf(CampScene sc)
        {
            foreach (var p in sc.parts[0]) if (p.style == DollStyle.Sit) return p.face;
            return sc.focus + Vector2.up * LastEveningScenes.SeatR;
        }

        /// <summary>The door (box x, z): where the elder's last part (hidden, in) stands.</summary>
        static Vector2 DoorOf(CampScene sc)
        {
            var ps = sc.parts[0];
            return ps.Count > 0 ? ps[ps.Count - 1].pos : sc.focus;
        }

        [UnityTest, Explicit, Timeout(900000)]
        public IEnumerator B82a_PlayNow() => Film("play-now", CampCheats.LastEveningPlayNow);

        [UnityTest, Explicit, Timeout(900000)]
        public IEnumerator B82a_WithADog() => Film("with-dog", CampCheats.LastEveningWithDog);
    }
}
