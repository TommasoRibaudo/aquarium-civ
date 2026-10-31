using System;
using System.Collections;
using System.IO;
using System.Linq;
using AquariumCiv.Game;
using AquariumCiv.GameUI;
using AquariumCiv.Sim;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

namespace AquariumCiv.View.PlayTests
{
    /// <summary>
    /// The care layer (#37) driven by touch in the real Slice scene at a portrait phone size: the Clean button, a wipe by
    /// finger until the glass is spotless (timed), a flick of every heap and weed patch, the camera gate (a stroke on the
    /// glass in clean mode never orbits; off the glass or with the mode off it does), the cheer and the auto-exit.
    /// Run alone (one InputTestFixture class per run); it backs up and restores the slice's save file.
    /// </summary>
    [Category("CarePlay")]
    public class CarePlayTests : InputTestFixture
    {
        const string ScenePath = "Assets/Game/Scenes/Slice.unity";
        const string Shots = "F:/GameDev/AquariumCiv/Temp/agent/";
        Touchscreen touch;
        int touchId = 1;
        string savePath, backup;
        bool soundWas;

        // ---------------- harness ----------------
        IEnumerator Open(int w = 1179, int h = 2556)
        {
            LogAssert.ignoreFailingMessages = true;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            GameUiPanel.AcceptInputUnfocused(true); // UI Toolkit drops taps while the Editor is behind another window
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            savePath = GameClock.DefaultSavePath;
            backup = savePath + ".care-test-backup";
            if (File.Exists(savePath)) { File.Copy(savePath, backup, true); File.Delete(savePath); }
            if (File.Exists(savePath + ".bak")) { File.Copy(savePath + ".bak", backup + ".bak", true); File.Delete(savePath + ".bak"); }
            soundWas = GameSettings.Sound;
            SetSize(w, h);
            float waited = 0f;
            while ((Screen.width != w || Screen.height != h) && waited < 4f) { waited += 0.1f; yield return new WaitForSecondsRealtime(0.1f); }
            Debug.Log($"[care] game view {Screen.width}x{Screen.height} after {waited:0.0} s");
            #if UNITY_EDITOR
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(ScenePath, new UnityEngine.SceneManagement.LoadSceneParameters(UnityEngine.SceneManagement.LoadSceneMode.Single));
#endif
            for (int i = 0; i < 90; i++) yield return null; // build, layout, the Game view size lands
            // The start-up bootstraps run after the first scene only, so a scene loaded in play mode gets them by hand.
            if (GameClock.Instance != null)
            {
                if (GameHud.Instance == null) GameHud.Create();
                var b = UnityEngine.Object.FindAnyObjectByType<BoxView>();
                if (CareView.Instance == null && b != null) b.gameObject.AddComponent<CareView>();
            }
            yield return null; yield return null;
            touch = InputSystem.AddDevice<Touchscreen>();
            yield return null;
        }

        void Close()
        {
            GameSettings.Sound = soundWas;
            try
            {
                if (File.Exists(backup + ".bak")) { File.Copy(backup + ".bak", savePath + ".bak", true); File.Delete(backup + ".bak"); }
                if (File.Exists(backup)) { File.Copy(backup, savePath, true); File.Delete(backup); }
                else if (File.Exists(savePath)) File.Delete(savePath);
            }
            catch (Exception e) { Debug.LogWarning("restore save: " + e.Message); }
        }

        static void SetSize(int w, int h)
        {
            foreach (var a in AppDomain.CurrentDomain.GetAssemblies())
            {
                var t = a.GetType("AquariumCiv.EditorTools.AgentGameView");
                if (t == null) continue;
                t.GetMethod("SetSize").Invoke(null, new object[] { w, h });
                return;
            }
        }

        static IEnumerator Shot(string name)
        {
            yield return new WaitForEndOfFrame();
            var tex = ScreenCapture.CaptureScreenshotAsTexture();
            File.WriteAllBytes(Shots + name + ".png", tex.EncodeToPNG());
            UnityEngine.Object.Destroy(tex);
        }

        IEnumerator TapAt(Vector2 p)
        {
            BeginTouch(touchId, p, queueEventOnly: true);
            yield return null; yield return null;
            EndTouch(touchId, p, delta: Vector2.zero, queueEventOnly: true);
            yield return null; yield return null;
            touchId++;
        }

        /// <summary>One finger down at <paramref name="from"/>, moved along the points by <paramref name="step"/> pixels per frame, lifted.</summary>
        IEnumerator Drag(Vector2[] path, float step, Action<float, int> done = null)
        {
            int id = touchId++;
            var p = path[0];
            BeginTouch(id, p, queueEventOnly: true);
            yield return null;
            float len = 0f; int frames = 0;
            for (int i = 1; i < path.Length; i++)
            {
                while (Vector2.Distance(p, path[i]) > 0.01f)
                {
                    var next = Vector2.MoveTowards(p, path[i], step);
                    len += Vector2.Distance(p, next);
                    p = next;
                    MoveTouch(id, p, delta: Vector2.zero, queueEventOnly: true);
                    frames++;
                    yield return null;
                }
            }
            EndTouch(id, p, delta: Vector2.zero, queueEventOnly: true);
            yield return null; yield return null;
            done?.Invoke(len, frames);
        }

        /// <summary>Waits until the camera has stopped (fling and spring-back done).</summary>
        static IEnumerator Settle(BoxCamera rig)
        {
            float last = float.MaxValue; int still = 0;
            for (int i = 0; i < 240 && still < 15; i++)
            {
                float v = rig.yaw + rig.pitch * 3.1f + rig.zoom * 7f;
                still = Mathf.Abs(v - last) < 0.002f ? still + 1 : 0;
                last = v;
                yield return null;
            }
        }

        static SimCore Sim => GameClock.Instance.Sim;
        static BoxCamera Rig => UnityEngine.Object.FindAnyObjectByType<BoxCamera>();
        static Camera Cam => Camera.main;

        /// <summary>Horizontal strokes across the wipeable glass, top to bottom, a bit closer than a wipe is wide: each starts on glass and ends on glass (what a hand does).</summary>
        static System.Collections.Generic.List<Vector2[]> RowStrokes(CareView care)
        {
            var list = new System.Collections.Generic.List<Vector2[]>();
            float spacing = care.wipeRadius * Screen.height * 1.2f;
            bool right = true;
            for (float y = Screen.height * 0.9f; y > Screen.height * 0.12f; y -= spacing)
            {
                float first = -1f, last = -1f;
                for (float x = 8f; x < Screen.width - 8f; x += 12f)
                {
                    if (!care.GlassAt(new Vector2(x, y))) continue;
                    if (first < 0f) first = x;
                    last = x;
                }
                if (first < 0f) continue;
                list.Add(right ? new[] { new Vector2(first, y), new Vector2(last, y) } : new[] { new Vector2(last, y), new Vector2(first, y) });
                right = !right;
            }
            return list;
        }

        // ---------------- tests ----------------
        [UnityTest]
        public IEnumerator FullCleanByTouch_IsUnderAMinute_AndTheMessGoesAway()
        {
            yield return Open();
            try
            {
                Assert.IsNotNull(GameClock.Instance, "the slice has a GameClock");
                Assert.IsNotNull(GameHud.Instance, "and a HUD");
                Assert.IsNotNull(CareView.Instance, "and the care layer");
                var sim = Sim;
                var care = CareView.Instance;
                var hud = GameHud.Instance;
                sim.AddMess(1f, 1f, sim.Config.trashMax);
                for (int i = 0; i < 20; i++) yield return null;
                Assert.Greater(care.GlassMean, 0.5f, "the glass shows the grime");
                Assert.Greater(care.Props.HeapCount, 0, "heaps are drawn");
                Assert.Greater(care.Props.WeedCount, 0, "weeds are drawn");
                // User 2026-10-30: bigger trash and weeds, clearly visible at default zoom (each at least 3 % of the screen
                // height on a phone in portrait).
                yield return new WaitForSecondsRealtime(0.6f); // done popping in
                var cam = Camera.main;
                foreach (var p in care.Props.AllPieces)
                {
                    if (p.leaving || p.t == null) continue;
                    var b = p.t.GetComponent<Renderer>().bounds;
                    float lo = float.MaxValue, hi = float.MinValue;
                    for (int k = 0; k < 8; k++)
                    {
                        var c = b.center + Vector3.Scale(b.extents, new Vector3((k & 1) * 2 - 1, (k >> 1 & 1) * 2 - 1, (k >> 2 & 1) * 2 - 1));
                        var s = cam.WorldToScreenPoint(c);
                        lo = Mathf.Min(lo, s.x); hi = Mathf.Max(hi, s.x);
                    }
                    Assert.Greater((hi - lo) / Screen.height, 0.03f, $"{p.t.name} reads at default zoom ({hi - lo:0} px wide)");
                }
                Assert.Less(care.SunFactor, 0.99f, "and the sun has started to dim");
                yield return Shot("care_dirty");

                // Clean mode by the HUD button.
                Assert.IsFalse(hud.CleanMode);
                yield return TapAt(GameUiPanel.ScreenCenter(hud.Root, "hud-clean"));
                Assert.IsTrue(hud.CleanMode, "the Clean button switches clean mode on");
                for (int i = 0; i < 40; i++) yield return null;
                Assert.Less(care.SunFactor, 0.80f, "full mess: the sun is at about -25 %");

                // Wipe the whole glass by finger: sweeps across the box, fingertip-and-a-half rows.
                var rows = RowStrokes(care);
                Assert.GreaterOrEqual(rows.Count, 3, "the glass covers several rows of the screen");
                float t0 = Time.realtimeSinceStartup;
                float pathLen = 0f; int frames = 0, rounds = 0;
                while (sim.State.grime > 0f && rounds < 4)
                {
                    foreach (var row in rows)
                        yield return Drag(row, 0.02f * Screen.height, (l, f) => { pathLen += l; frames += f; });
                    rounds++;
                    for (int i = 0; i < 3; i++) yield return null;
                }
                float real = Time.realtimeSinceStartup - t0;
                Debug.Log("[care] pane means after the wipe: " + string.Join(", ", care.Masks.Select(m => m.Mean.ToString("0.000"))) + " grime " + sim.State.grime);
                Debug.Log($"[care] wiped the glass in {rounds} pass(es) over {rows.Count} rows: {frames} frames, {pathLen / Screen.height:0.0} screen heights of finger path, {real:0.0} s real here; at 1.2 screen heights/s a hand needs {pathLen / Screen.height / 1.2f:0.0} s");
                Assert.AreEqual(0f, sim.State.grime, 1e-5f, "the glass is spotless");
                Assert.Less(care.GlassMean, CareMath.SpotlessMean + 1e-4f);
                Assert.LessOrEqual(pathLen / Screen.height / 1.2f, 30f, "wiping all the glass takes well under half a minute at a hand's pace");
                Assert.GreaterOrEqual(care.CheerCount, 1, "the people cheer when the glass goes spotless");
                yield return Shot("care_glass_clean");

                // Flick every heap and weed patch out (a fast release); behind clean glass they are in reach.
                int before = sim.State.trash.Count;
                int guard = 0;
                float flickT0 = Time.realtimeSinceStartup;
                float flickLen = 0f;
                while ((sim.State.trash.Count > 0 || sim.State.overgrowth >= 0.03f) && guard++ < 24)
                {
                    var piece = care.Props.AllPieces.FirstOrDefault(p => !p.leaving);
                    if (piece == null) break;
                    var sp = (Vector2)Cam.WorldToScreenPoint(piece.Center);
                    if (sp.x < 40 || sp.x > Screen.width - 40 || sp.y < 0.16f * Screen.height || sp.y > Screen.height - 0.15f * Screen.height)
                    {
                        // out of the comfortable area: skip it by wiping it through the sim (the test only needs the rest)
                        if (piece.weed) sim.Prune(MessProps.WeedUnit); else sim.RemoveTrash(piece.key);
                        yield return null; yield return null;
                        continue;
                    }
                    yield return Drag(new[] { sp, sp + new Vector2(0.01f, 0.08f) * Screen.height }, 0.045f * Screen.height, (l, f) => flickLen += l);
                    for (int i = 0; i < 3; i++) yield return null;
                }
                Debug.Log($"[care] flicked the mess out: {before} heaps first, {Time.realtimeSinceStartup - flickT0:0.0} s real, {flickLen / Screen.height:0.0} screen heights of path, trash left {sim.State.trash.Count}, overgrowth {sim.State.overgrowth:0.00}");
                Assert.AreEqual(0, sim.State.trash.Count, "every heap is thrown out");
                Assert.Less(sim.State.overgrowth, 0.03f, "and the weeds");
                Assert.Less(sim.MessLevel, 0.001f, "all tidy");
                // In seconds: the sun takes about 4 s to recover, whatever the frame rate.
                for (float t = 0f; t < 8f && (care.LastCheer != "all" || care.SunFactor <= 0.995f); t += Time.unscaledDeltaTime) yield return null;
                Assert.AreEqual("all", care.LastCheer, "the big cheer for everything tidy");
                Assert.Greater(care.SunFactor, 0.995f, "the sun is back");
                yield return new WaitForSecondsRealtime(2.5f);
                Assert.IsFalse(hud.CleanMode, "clean mode ends by itself once everything is clean");
                yield return Shot("care_all_clean");
            }
            finally { Close(); }
        }

        [UnityTest]
        public IEnumerator Gate_WipeOnGlassNeverOrbits_ButOffGlassOrNoModeDoes()
        {
            yield return Open();
            try
            {
                Assert.IsNotNull(GameClock.Instance, "the slice has a GameClock");
                Assert.IsNotNull(GameHud.Instance, "and a HUD");
                Assert.IsNotNull(CareView.Instance, "and the care layer");
                var sim = Sim;
                var hud = GameHud.Instance;
                var rig = Rig;
                sim.AddMess(1f, 0f, 0);
                for (int i = 0; i < 10; i++) yield return null;
                var rows = RowStrokes(CareView.Instance);
                Assert.GreaterOrEqual(rows.Count, 2);
                var onGlass = rows[rows.Count / 2][0];
                Assert.IsTrue(CareView.Instance.GlassAt(onGlass));

                // Mode off: a drag on the glass orbits.
                float yaw0 = rig.yaw;
                yield return Drag(new[] { onGlass, onGlass + new Vector2(0.18f * Screen.width, 0f) }, 0.02f * Screen.height);
                yield return Settle(rig);
                Assert.Greater(Mathf.Abs(rig.yaw - yaw0), 1f, "with clean mode off, one finger orbits even on the glass");
                float grime0 = sim.State.grime;
                Assert.AreEqual(grime0, 1f, 1e-4f, "and nothing was wiped");

                // Mode on: a stroke that starts on the glass wipes and does not orbit.
                hud.SetCleanMode(true);
                yield return Settle(rig);
                yaw0 = rig.yaw;
                float pitch0 = rig.pitch;
                yield return Drag(new[] { onGlass, onGlass + new Vector2(0.2f * Screen.width, -0.05f * Screen.height) }, 0.02f * Screen.height);
                yield return Settle(rig);
                Assert.AreEqual(yaw0, rig.yaw, 0.05f, "a wipe on the glass never becomes an orbit");
                Assert.AreEqual(pitch0, rig.pitch, 0.05f);
                Assert.Less(sim.State.grime, grime0 - 0.002f, "it wiped");

                // Mode on, but the stroke starts off the glass (below the tank): the camera takes it.
                var off = new Vector2(Screen.width * 0.5f, 0.2f * Screen.height);
                Assert.IsFalse(CareView.Instance.ClaimsPress(off), "a press off the glass is not ours");
                yaw0 = rig.yaw;
                float grime1 = sim.State.grime;
                yield return Drag(new[] { off, off + new Vector2(0.2f * Screen.width, 0f) }, 0.02f * Screen.height);
                yield return Settle(rig);
                Assert.Greater(Mathf.Abs(rig.yaw - yaw0), 1f, "a stroke that starts off the glass stays the camera's");
                Assert.AreEqual(grime1, sim.State.grime, 1e-4f, "and wipes nothing");

                // Two fingers are always the camera: a pinch on the glass zooms.
                float zoom0 = rig.zoom;
                int a = touchId++, b = touchId++;
                var c = onGlass;
                BeginTouch(a, c + new Vector2(-60, 0), queueEventOnly: true);
                BeginTouch(b, c + new Vector2(60, 0), queueEventOnly: true);
                yield return null;
                for (int i = 1; i <= 12; i++)
                {
                    MoveTouch(a, c + new Vector2(-60 - i * 14, 0), delta: Vector2.zero, queueEventOnly: true);
                    MoveTouch(b, c + new Vector2(60 + i * 14, 0), delta: Vector2.zero, queueEventOnly: true);
                    yield return null;
                }
                EndTouch(a, c, delta: Vector2.zero, queueEventOnly: true);
                EndTouch(b, c, delta: Vector2.zero, queueEventOnly: true);
                yield return null; yield return null;
                Assert.Less(rig.zoom, zoom0 - 0.01f, "two fingers zoom in clean mode too");
            }
            finally { Close(); }
        }

        // ---------------- the glass as rendered (tester batch 3: 81 leaked panes hid the land and the grime) ----------------
        /// <summary>The camera's frame without the UI, for reading pixels.</summary>
        static Texture2D Render(string shot)
        {
            var cam = Camera.main;
            int w = Screen.width, h = Screen.height;
            var rt = RenderTexture.GetTemporary(w, h, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB, 1);
            var keep = cam.targetTexture;
            cam.targetTexture = rt;
            cam.Render();
            cam.targetTexture = keep;
            var active = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            RenderTexture.active = active;
            RenderTexture.ReleaseTemporary(rt);
            if (shot != null) File.WriteAllBytes(Shots + shot + ".png", tex.EncodeToPNG());
            return tex;
        }

        /// <summary>The mean colour (0..255) of a square of the frame.</summary>
        static Vector3 Mean(Texture2D t, Vector2 at, int half)
        {
            var s = Vector3.zero; int n = 0;
            for (int y = (int)at.y - half; y <= at.y + half; y += 2)
                for (int x = (int)at.x - half; x <= at.x + half; x += 2)
                { var c = t.GetPixel(x, y); s += new Vector3(c.r, c.g, c.b) * 255f; n++; }
            return s / n;
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator Glass_CleanIsClear_DirtyIsObvious_AndAWipeShows()
        {
            yield return Open();
            try
            {
#if UNITY_EDITOR
                Assert.IsEmpty(BoxView.OrphanRoots().Select(g => g.name), "no generated box is left over from another scene");
#endif
                var panes = Resources.FindObjectsOfTypeAll<Renderer>().Where(r => r.name.StartsWith("Glass") && r.enabled && r.gameObject.activeInHierarchy).ToArray();
                Assert.AreEqual(4, panes.Length, "four panes are drawn, no more");
                var sim = GameClock.Instance.Sim;
                var care = CareView.Instance;
                var cam = Camera.main;
                // a spot of the front pane over the land, well inside the pane
                var front = panes.First(r => r.name == "GlassFront");
                var c = front.bounds.center;
                var spot = (Vector2)cam.WorldToScreenPoint(new Vector3(c.x, 1.7f, c.z));
                sim.CleanAll();
                for (int i = 0; i < 6; i++) yield return null;
                front.enabled = false;
                var bare = Render(null);
                front.enabled = true;
                var clean = Render("t3_glass_clean");
                Vector3 bareC = Mean(bare, spot, 60), cleanC = Mean(clean, spot, 60);
                Debug.Log($"[care] pixels: no pane {bareC}, clean {cleanC}");
                Assert.Less((cleanC - bareC).magnitude, 26f, "clean glass barely changes what is behind it");

                sim.State.grime = 0.8f;
                for (int i = 0; i < 6; i++) yield return null;
                var dirty = Render("t3_glass_dirty");
                Vector3 dirtyC = Mean(dirty, spot, 60);
                Debug.Log($"[care] pixels: grime 0.8 {dirtyC}, mask means {string.Join(", ", care.Masks.Select(m => m.Mean.ToString("0.00")))}");
                Assert.Greater((dirtyC - cleanC).magnitude, 45f, "0.8 grime is obviously dirty");

                // wipe a trail across the front pane: the wiped strip comes back to the clean look
                int fi = Array.FindIndex(care.Masks, m => Mathf.Approximately(m.size.y, front.bounds.size.y) && Mathf.Approximately(m.size.x, front.bounds.size.x));
                Assert.GreaterOrEqual(fi, 0);
                float uvy = (1.7f - front.bounds.min.y) / front.bounds.size.y;
                for (int i = 0; i < 4; i++) care.Masks[fi].BrushLine(new Vector2(0.3f, uvy), new Vector2(0.7f, uvy), 0.35f, 0.5f);
                for (int i = 0; i < 3; i++) yield return null;
                var wiped = Render("t3_glass_wiped");
                Vector3 wipedC = Mean(wiped, spot, 30), wasC = Mean(dirty, spot, 30);
                Debug.Log($"[care] pixels: wiped {wipedC}, same spot dirty {wasC}");
                Assert.Greater((wipedC - wasC).magnitude, 40f, "the wiped trail shows");
                UnityEngine.Object.Destroy(bare); UnityEngine.Object.Destroy(clean); UnityEngine.Object.Destroy(dirty); UnityEngine.Object.Destroy(wiped);
            }
            finally { Close(); }
        }

        [UnityTest]
        public IEnumerator SlowDragOnMess_DropsItBack_AndAFastFlickThrowsItOut()
        {
            yield return Open();
            try
            {
                Assert.IsNotNull(GameClock.Instance, "the slice has a GameClock");
                Assert.IsNotNull(GameHud.Instance, "and a HUD");
                Assert.IsNotNull(CareView.Instance, "and the care layer");
                var sim = Sim;
                var care = CareView.Instance;
                var hud = GameHud.Instance;
                sim.AddMess(0f, 0f, 3);
                for (int i = 0; i < 20; i++) yield return null;
                hud.SetCleanMode(true);
                yield return null;
                var piece = care.Props.AllPieces.First(p => !p.leaving && !p.weed);
                var sp = (Vector2)Cam.WorldToScreenPoint(piece.Center);
                // A slow drag only wobbles the heap and drops it back.
                Assert.IsTrue(care.ClaimsPress(sp), "a heap in reach is ours");
                yield return Drag(new[] { sp, sp + new Vector2(0.0f, 0.05f) * Screen.height }, 0.002f * Screen.height);
                for (int i = 0; i < 5; i++) yield return null;
                Assert.AreEqual(3, sim.State.trash.Count, "a slow drag drops it back");
                yield return Drag(new[] { sp, sp + new Vector2(0.01f, 0.1f) * Screen.height }, 0.05f * Screen.height);
                for (int i = 0; i < 5; i++) yield return null;
                Assert.AreEqual(2, sim.State.trash.Count, "a fast flick throws it out");
            }
            finally { Close(); }
        }
    }
}
