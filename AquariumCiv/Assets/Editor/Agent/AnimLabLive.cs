// #353 AnimLab, live clips: the real game in play mode (a play group of real kids, a hunt with the real herd), for what a
// staged AnimBench clip can't show. One play-mode session for every live clip in the queue:
//   EnterPlay()  (edit mode)  an empty scene, then play mode (a domain reload; nothing below survives it, state is on disk)
//   Load(seed)   (play mode)  a sandboxed save (the player's is never touched), the seed, the Slice scene, every bootstrap,
//                             the first run skipped; Status() says "ready" when it's up
//   the request's own setup code (live.setup, through `script eval`): cheats that start the scene state
//   Begin(json)               PeopleView.HandDriven, Time.captureDeltaTime = 1/fps: every frame the game moves exactly one
//                             step (PeopleView.Tick by hand, everything else on the capture clock) and both cameras render
//                             through URP post into out/<clip>/frames, as the edit-mode lab does; Status() says "done"
//   ExitPlay()
// Tools/animlab.sh live drives it with bounded calls, then post.py makes the same videos and sheets.
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using AquariumCiv.Life;
using AquariumCiv.View;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace AquariumCiv.EditorTools
{
    /// <summary>What a live request asks for ("live" in the clip JSON; prep.py passes it as JSON).</summary>
    [System.Serializable]
    public sealed class LiveSpec
    {
        public string clip, outDir;
        /// <summary>Seconds stepped before recording (unrecorded), then recorded, at fps.</summary>
        public float warm = 1f, seconds = 6f, fps = 30f;
        /// <summary>"hunt" (whoever is on a hunt step), "play" (the kids at play), a doll name substring, or "" (the fixed focus).</summary>
        public string follow = "";
        public float fx, fz, span = 1.5f, closeSpan = 0.6f, yaw = 28f, pitch = 34f;
        public int dw = 480, dh = 360, cw = 360, ch = 360;
    }

    public static class AnimLabLive
    {
        const string SlicePath = "Assets/Game/Scenes/Slice.unity";
        static string SaveDir => Path.Combine(LabBatch.Root, "save");
        static string PrevScenePath => Path.Combine(LabBatch.Root, "prev-scene.txt");

        static string state = "idle";
        static LiveSpec spec;
        static int loadFrame = -1, frame, warmFrames, recFrames;
        static Camera cam;
        static Texture2D readback;
        static Vector2 followAt;
        static bool followSet;
        static int ecologyWas = -1;

        // ------------------------------------------------------------------ session

        public static string EnterPlay()
        {
            if (EditorApplication.isPlaying) return "already playing";
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) return $"ERROR: scene '{SceneManager.GetSceneAt(i).name}' has unsaved changes";
            var active = SceneManager.GetActiveScene();
            if (!File.Exists(PrevScenePath) && !string.IsNullOrEmpty(active.path)) File.WriteAllText(PrevScenePath, active.path);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorApplication.EnterPlaymode();
            return "entering play mode";
        }

        /// <summary><paramref name="ecology"/>: 1 = the living land on (P8), 0 = off, -1 = the Editor's pref as it is. The pref is
        /// put back as soon as the box has read it.</summary>
        public static string Load(int seed, int ecology = 1)
        {
            if (!EditorApplication.isPlaying) return "ERROR: not in play mode";
            ecologyWas = -1;
            if (ecology >= 0)
            {
                ecologyWas = AquariumCiv.Game.GameClock.EcologyPref ? 1 : 0;
                AquariumCiv.Game.GameClock.EcologyPref = ecology == 1;
            }
            if (Directory.Exists(SaveDir)) Directory.Delete(SaveDir, true);
            Directory.CreateDirectory(SaveDir);
            AquariumCiv.Game.GameClock.SaveFolderOverride = SaveDir;
            if (seed != 0) AquariumCiv.Game.GameClock.DebugNextSeed = seed;
            EditorSceneManager.LoadSceneAsyncInPlayMode(SlicePath, new LoadSceneParameters(LoadSceneMode.Single));
            state = "loading";
            loadFrame = Time.frameCount;
            Runner.Ensure();
            return "loading " + SlicePath;
        }

        public static string Status() => spec != null && state == "recording"
            ? $"recording {frame}/{warmFrames + recFrames}" : state;

        public static string ExitPlay()
        {
            Stop();
            if (EditorApplication.isPlaying) EditorApplication.ExitPlaymode();
            return "exiting play mode";
        }

        /// <summary>The [RuntimeInitializeOnLoadMethod(AfterSceneLoad)] methods: Unity runs them after the first scene only (PlayTests' Bootstraps).</summary>
        static int RunBootstraps()
        {
            int n = 0;
            foreach (var a in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                string name = a.GetName().Name;
                if (!name.StartsWith("AquariumCiv.") || name.Contains("Tests")) continue;
                foreach (var t in a.GetTypes())
                    foreach (var m in t.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                    {
                        var at = m.GetCustomAttribute<RuntimeInitializeOnLoadMethodAttribute>();
                        if (at != null && at.loadType == RuntimeInitializeLoadType.AfterSceneLoad) { m.Invoke(null, null); n++; }
                    }
            }
            TimeLapseView.Ensure();
            return n;
        }

        // ------------------------------------------------------------------ one clip

        public static string Begin(string json)
        {
            if (state != "ready" && state != "done") return "ERROR: not ready (" + state + ")";
            var s = JsonUtility.FromJson<LiveSpec>(json);
            var pv = PeopleView.Find();
            if (pv == null) return "ERROR: no PeopleView";
            Directory.CreateDirectory(Path.Combine(s.outDir, "frames"));
            spec = s;
            frame = 0;
            warmFrames = Mathf.RoundToInt(s.warm * s.fps);
            recFrames = Mathf.RoundToInt(s.seconds * s.fps) + 1;
            followSet = false;
            pv.HandDriven = true;
            Time.captureDeltaTime = 1f / s.fps;
            if (cam == null)
            {
                var go = new GameObject("AnimLabLiveCamera"); // DontDestroyOnLoad only: a DontSave root outlives play mode (LeakCheck)
                Object.DontDestroyOnLoad(go);
                cam = go.AddComponent<Camera>();
                cam.enabled = false;
                var main = Camera.main;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = main != null ? main.backgroundColor : new Color(0.9f, 0.86f, 0.8f);
                cam.fieldOfView = 22f;
                cam.nearClipPlane = 0.05f;
                cam.farClipPlane = 60f;
                if (main != null) cam.cullingMask = main.cullingMask;
                var data = go.AddComponent<UniversalAdditionalCameraData>();
                data.renderPostProcessing = true;
                data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            }
            state = "recording";
            return $"recording {s.clip}: {warmFrames} warm + {recFrames} frames";
        }

        static void Stop()
        {
            var pv = PeopleView.Find();
            if (pv != null) pv.HandDriven = false;
            Time.captureDeltaTime = 0f;
        }

        /// <summary>Where the cameras look this frame: the followed doll (eased), else the spec's focus.</summary>
        static Vector2 Focus(PeopleView pv)
        {
            var want = new Vector2(spec.fx, spec.fz);
            if (!string.IsNullOrEmpty(spec.follow))
            {
                var pts = new List<Vector3>();
                foreach (var a in pv.Activities)
                {
                    bool pick = spec.follow == "hunt" ? a.hunt != HuntPhase.None : spec.follow == "play" ? a.kind == ActivityKind.Play : false;
                    if (pick && pv.TryGetDollFeet(a.who, out var w)) pts.Add(w);
                }
                if (pts.Count == 0 && spec.follow != "hunt" && spec.follow != "play")
                    foreach (var v in Resources.FindObjectsOfTypeAll<PegDollView>())
                        if (v.gameObject.activeInHierarchy && v.name.Contains(spec.follow)) { pts.Add(v.transform.position); break; }
                if (pts.Count > 0)
                {
                    var c = Vector3.zero;
                    foreach (var p in pts) c += p;
                    c /= pts.Count;
                    want = new Vector2(c.x, c.z);
                }
            }
            followAt = followSet ? Vector2.Lerp(followAt, want, 0.08f) : want;
            followSet = true;
            return followAt;
        }

        static void Aim(Vector2 focus, float span, float aspect)
        {
            float y = 0f;
            var box = Object.FindAnyObjectByType<BoxView>();
            if (box != null) y = box.SurfaceHeight(focus.x, focus.y);
            var target = new Vector3(focus.x, y + 0.07f, focus.y);
            float hfov = 2f * Mathf.Atan(Mathf.Tan(cam.fieldOfView * Mathf.Deg2Rad * 0.5f) * aspect);
            float dist = span * 0.5f / Mathf.Tan(hfov * 0.5f);
            cam.transform.position = target + Quaternion.Euler(spec.pitch, spec.yaw, 0) * Vector3.back * dist;
            cam.transform.LookAt(target);
            cam.aspect = aspect;
        }

        static void Shoot(int w, int h, string path)
        {
            var rt = RenderTexture.GetTemporary(w, h, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB, 1);
            cam.targetTexture = rt;
            cam.Render();
            cam.targetTexture = null;
            var keep = RenderTexture.active;
            RenderTexture.active = rt;
            if (readback == null || readback.width != w || readback.height != h)
                readback = new Texture2D(w, h, TextureFormat.RGB24, false) { hideFlags = HideFlags.DontSave };
            readback.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            readback.Apply(false);
            RenderTexture.active = keep;
            RenderTexture.ReleaseTemporary(rt);
            File.WriteAllBytes(path, readback.EncodeToJPG(92));
        }

        /// <summary>One game frame: step the people by hand, then (past the warm-up) render both cameras.</summary>
        static void Step()
        {
            if (state == "loading")
            {
                if (Time.frameCount - loadFrame < 90 || PeopleView.Find() == null) return;
                if (ecologyWas >= 0) { AquariumCiv.Game.GameClock.EcologyPref = ecologyWas == 1; ecologyWas = -1; }
                int n = RunBootstraps();
                FirstRunView.DebugSkipAll();
                state = "ready";
                Debug.Log($"[AnimLab] live scene ready ({n} bootstraps)");
                return;
            }
            if (state != "recording" || spec == null) return;
            var pv = PeopleView.Find();
            if (pv == null) { state = "ERROR: PeopleView gone"; Stop(); return; }
            pv.Tick(1f / spec.fps);
            if (frame >= warmFrames)
            {
                int f = frame - warmFrames;
                var focus = Focus(pv);
                string dir = Path.Combine(spec.outDir, "frames");
                Aim(focus, spec.span, spec.dw / (float)spec.dh);
                Shoot(spec.dw, spec.dh, Path.Combine(dir, $"d_{f:00000}.jpg"));
                Aim(focus, spec.closeSpan, spec.cw / (float)spec.ch);
                Shoot(spec.cw, spec.ch, Path.Combine(dir, $"c_{f:00000}.jpg"));
            }
            else Focus(pv);
            frame++;
            if (frame >= warmFrames + recFrames)
            {
                Stop();
                WriteMeta();
                state = "done";
            }
        }

        /// <summary>A track.json / lab.json stand-in, so post.py treats a live clip like a staged one (no hits: the live game has no track).</summary>
        static void WriteMeta()
        {
            var inv = CultureInfo.InvariantCulture;
            Directory.CreateDirectory(Path.Combine(spec.outDir, "bench"));
            float dur = (recFrames - 1) / spec.fps;
            File.WriteAllText(Path.Combine(spec.outDir, "bench", "track.json"), string.Format(inv,
                "{{\"format\":\"animlab-live/1\",\"clip\":\"{0}\",\"fps\":{1},\"duration\":{2},\"frames\":{3},\"sheet\":{{\"from\":0,\"to\":{2},\"frames\":24,\"columns\":6}},\"hits\":[]}}",
                spec.clip, spec.fps, dur, recFrames));
        }

        /// <summary>Calls <see cref="Step"/> once per game frame, after every Update (so PeopleView's hand tick is the frame's last).</summary>
        sealed class Runner : MonoBehaviour
        {
            static Runner instance;
            public static void Ensure()
            {
                if (instance != null) return;
                var go = new GameObject("AnimLabLiveRunner"); // DontDestroyOnLoad only: dies with play mode
                Object.DontDestroyOnLoad(go);
                instance = go.AddComponent<Runner>();
            }
            void LateUpdate() => Step();
        }
    }
}
