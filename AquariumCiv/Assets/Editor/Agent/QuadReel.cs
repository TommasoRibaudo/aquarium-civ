using System;
using System.Collections.Generic;
using System.IO;
using AquariumCiv.View;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace AquariumCiv.EditorTools
{
    /// <summary>
    /// A review reel of the four-legged animals (#425's QuadDoll: deer, wolves, pups, dogs): each clip drives the real
    /// <see cref="QuadDoll"/> with the same <see cref="QuadMotion"/> calls DeerView / WolfView make, on a plain checker ground,
    /// and writes JPG frames from two cameras (the game's 28/34 angle, closer than the box camera, and a level side view).
    /// Edit mode, in a throwaway empty scene; <see cref="Finish"/> reopens the scene that was open before.
    /// <para>Use: <c>QuadReel.Render("deer-graze", "F:/GameDev/AquariumCiv/Temp/agent/quadreel")</c> per clip (one eval each),
    /// then <c>QuadReel.Finish()</c>. Frames: &lt;out&gt;/&lt;clip&gt;/g_000.jpg and s_000.jpg.</para>
    /// </summary>
    public static class QuadReel
    {
        const string ViewDir = "Assets/Game/View";
        public static readonly string[] Clips =
        {
            "deer-herd-walk", "deer-graze", "deer-look-round", "deer-startle", "deer-lie-down", "deer-drink",
            "wolf-trot", "wolf-lie-down", "wolf-howl", "wolf-watch", "wolf-run", "wolf-den", "pups-play", "dogs-by-generation",
        };

        delegate void PoseFn(ref QuadPose p, float t);

        sealed class Actor
        {
            public QuadLook look;
            public Func<float, Vector3> at;
            public Func<float, float> yaw;
            public PoseFn pose;
            public QuadDoll doll;
        }

        sealed class Clip
        {
            public float dur;
            public bool follow;
            public float span = 0.75f, sideSpan = 0.6f;
            public readonly List<Actor> actors = new();
        }

        static string prevScene;
        static Texture2D readback;

        static Func<float, Vector3> Still(float x, float z) => _ => new Vector3(x, 0f, z);
        static Func<float, float> Yaw(float y) => _ => y;

        static Clip Build(string name)
        {
            var c = new Clip();
            switch (name)
            {
                case "deer-herd-walk": // DeerView: Walk(t + id*0.31) at WalkSpeed 0.12 u/s
                    c.dur = 5f; c.follow = true; c.span = 1.0f; c.sideSpan = 0.9f;
                    AddWalker(c, QuadLook.Of(QuadKind.Stag, 0), 0.0f, -0.12f, 0);
                    AddWalker(c, QuadLook.Of(QuadKind.Doe, 1), -0.30f, 0.05f, 1);
                    AddWalker(c, QuadLook.Of(QuadKind.Fawn, 2), -0.50f, 0.16f, 2);
                    AddWalker(c, QuadLook.Of(QuadKind.WhiteStag, 3), 0.28f, 0.22f, 3);
                    break;
                case "deer-graze": // DeerView: Graze(t, since, id), the full 11 s cycle
                    c.dur = 11f;
                    c.actors.Add(new Actor { look = QuadLook.Of(QuadKind.Doe, 0), at = Still(0f, 0f), yaw = Yaw(100f),
                        pose = (ref QuadPose p, float t) => QuadMotion.Graze(ref p, t, t, 0) });
                    c.actors.Add(new Actor { look = QuadLook.Of(QuadKind.Fawn, 1), at = Still(-0.12f, 0.3f), yaw = Yaw(60f),
                        pose = (ref QuadPose p, float t) => QuadMotion.Graze(ref p, t, t + 4f, 1) });
                    break;
                case "deer-look-round": // DeerView idle: Alert(actSince, sin(t*0.7+id)*50, false)
                    c.dur = 6f;
                    c.actors.Add(new Actor { look = QuadLook.Of(QuadKind.Stag, 0), at = Still(0f, 0f), yaw = Yaw(90f),
                        pose = (ref QuadPose p, float t) => QuadMotion.Alert(ref p, t, Mathf.Sin(t * 0.7f) * 50f, false) });
                    break;
                case "deer-startle": // grazing; a person comes near on its left: Alert(flag) snaps; then the 3 s bound at 1.4 u/s
                    c.dur = 7.5f; c.follow = true; c.span = 1.4f; c.sideSpan = 1.2f;
                    c.actors.Add(new Actor { look = QuadLook.Of(QuadKind.Stag, 0),
                        at = t => new Vector3(t < 4f ? 0f : Mathf.Min(3f, t - 4f) * QuadMotion.BoltSpeedDeer, 0f, 0f), yaw = Yaw(90f),
                        pose = (ref QuadPose p, float t) =>
                        {
                            if (t < 2.5f) QuadMotion.Graze(ref p, t, t + 6f, 0);
                            else if (t < 4f) QuadMotion.Alert(ref p, t - 2.5f, -60f, true);
                            else if (t < 7f) QuadMotion.Bolt(ref p, t, true);
                            else QuadMotion.Alert(ref p, t - 7f, -150f, true);
                        } });
                    break;
                case "deer-lie-down": // DeerView bed: LieDown(since, 0.3)
                    c.dur = 4f;
                    c.actors.Add(new Actor { look = QuadLook.Of(QuadKind.Doe, 0), at = Still(0f, 0f), yaw = Yaw(100f),
                        pose = (ref QuadPose p, float t) => { if (t >= 1f) QuadMotion.LieDown(ref p, t - 1f, 0.3f); } });
                    c.actors.Add(new Actor { look = QuadLook.Of(QuadKind.Stag, 1), at = Still(0.26f, 0.12f), yaw = Yaw(60f),
                        pose = (ref QuadPose p, float t) => QuadMotion.LieDown(ref p, t + 1f, 0.3f) });
                    break;
                case "deer-drink": // Drink(t + id): 3 s sipping, 1 s lifted
                    c.dur = 8f;
                    c.actors.Add(new Actor { look = QuadLook.Of(QuadKind.Doe, 0), at = Still(0f, 0f), yaw = Yaw(90f),
                        pose = (ref QuadPose p, float t) => QuadMotion.Drink(ref p, t) });
                    break;
                case "wolf-trot": // WolfView: Trot(t + cur.x*3), tail -6, at 0.2 u/s (the phase uses x, as in the game)
                    c.dur = 5f; c.follow = true; c.span = 1.0f; c.sideSpan = 0.8f;
                    for (int i = 0; i < 2; i++)
                    {
                        int id = i; float x0 = -0.25f * i, z0 = 0.12f * i;
                        var lk = QuadLook.Of(QuadKind.Wolf, id); lk.paleFace = id == 0;
                        c.actors.Add(new Actor { look = lk, at = t => new Vector3(x0 + 0.2f * t, 0f, z0), yaw = Yaw(90f),
                            pose = (ref QuadPose p, float t) => { QuadMotion.Trot(ref p, t + (x0 + 0.2f * t) * 3f); p.tail = -6f; } });
                    }
                    break;
                case "wolf-lie-down": // WolfView: LieDown(actSince + 0.6) (in the game it is already down when it starts)
                    c.dur = 4f;
                    c.actors.Add(new Actor { look = QuadLook.Of(QuadKind.Wolf, 1), at = Still(0f, 0f), yaw = Yaw(100f),
                        pose = (ref QuadPose p, float t) => { if (t >= 1f) QuadMotion.LieDown(ref p, t - 1f); } });
                    break;
                case "wolf-howl": // WolfView: k = repeat(since + (grey ? 0 : 0.6), 6); SitHowl(k, 2.6); sits on after 3.4 s
                    c.dur = 12f; c.span = 0.9f; c.sideSpan = 0.9f;
                    for (int i = 0; i < 3; i++)
                    {
                        bool grey = i == 0; float off = grey ? 0f : 0.6f;
                        var lk = QuadLook.Of(QuadKind.Wolf, i); lk.paleFace = grey;
                        c.actors.Add(new Actor { look = lk, at = Still(-0.22f + 0.22f * i, i == 1 ? 0.12f : 0f), yaw = Yaw(140f - 40f * i),
                            pose = (ref QuadPose p, float t) =>
                            {
                                float k = Mathf.Repeat(t + off, 6f);
                                QuadMotion.SitHowl(ref p, k, 2.6f);
                                if (k > 3.4f) { p.sit = 1f; p.neck = 0f; p.head = 0f; }
                            } });
                    }
                    break;
                case "wolf-watch": // WolfView at night, the firelight's edge: Alert(since, 0, false), neck -4, tail -8
                    c.dur = 4f;
                    c.actors.Add(new Actor { look = QuadLook.Of(QuadKind.Wolf, 1), at = Still(0f, 0f), yaw = Yaw(60f),
                        pose = (ref QuadPose p, float t) => { QuadMotion.Alert(ref p, t, 0f, false); p.neck = -4f; p.tail = -8f; } });
                    break;
                case "wolf-run": // Bolt(t, false): the flat run at 1.6 u/s
                    c.dur = 3f; c.follow = true; c.span = 1.4f; c.sideSpan = 1.0f;
                    c.actors.Add(new Actor { look = QuadLook.Of(QuadKind.Wolf, 1), at = t => new Vector3(QuadMotion.BoltSpeedWolf * t, 0f, 0f), yaw = Yaw(90f),
                        pose = (ref QuadPose p, float t) => QuadMotion.Bolt(ref p, t, false) });
                    break;
                case "wolf-den": // a day at the den: adults LieDown, pups SleepCurl
                    c.dur = 6f;
                    c.actors.Add(new Actor { look = QuadLook.Of(QuadKind.Wolf, 1), at = Still(0f, 0f), yaw = Yaw(110f),
                        pose = (ref QuadPose p, float t) => QuadMotion.LieDown(ref p, t + 0.6f) });
                    c.actors.Add(new Actor { look = QuadLook.Of(QuadKind.Pup, 2), at = Still(0.2f, -0.05f), yaw = Yaw(30f),
                        pose = (ref QuadPose p, float t) => QuadMotion.SleepCurl(ref p, t) });
                    c.actors.Add(new Actor { look = QuadLook.Of(QuadKind.Pup, 3), at = Still(0.16f, 0.12f), yaw = Yaw(200f),
                        pose = (ref QuadPose p, float t) => QuadMotion.SleepCurl(ref p, t + 1.3f) });
                    break;
                case "pups-play": // WolfView: PlayBow(t + cur.z*5)
                    c.dur = 7.2f; c.span = 0.55f; c.sideSpan = 0.45f;
                    c.actors.Add(new Actor { look = QuadLook.Of(QuadKind.Pup, 0), at = Still(-0.07f, 0f), yaw = Yaw(90f),
                        pose = (ref QuadPose p, float t) => QuadMotion.PlayBow(ref p, t) });
                    c.actors.Add(new Actor { look = QuadLook.Of(QuadKind.Pup, 1), at = Still(0.07f, 0.03f), yaw = Yaw(-90f),
                        pose = (ref QuadPose p, float t) => QuadMotion.PlayBow(ref p, t + 0.15f) });
                    break;
                case "dogs-by-generation": // QuadLook.Dog(g, hash) for g = 0..4, trotting side by side (front: generation 0)
                    c.dur = 4f; c.follow = true; c.span = 1.2f; c.sideSpan = 1.2f;
                    for (int g = 0; g <= 4; g++)
                    {
                        int gen = g; float z0 = -0.24f + 0.12f * g, x0 = 0.4f - 0.2f * g;
                        c.actors.Add(new Actor { look = QuadLook.Dog(gen, 7 + gen), at = t => new Vector3(x0 + 0.2f * t, 0f, z0), yaw = Yaw(90f),
                            pose = (ref QuadPose p, float t) => { QuadMotion.Trot(ref p, t + gen * 0.13f); p.tail = -6f; } });
                    }
                    break;
                default: return null;
            }
            return c;
        }

        static void AddWalker(Clip c, QuadLook look, float x0, float z0, int id)
        {
            c.actors.Add(new Actor { look = look, at = t => new Vector3(x0 + 0.12f * t, 0f, z0), yaw = Yaw(90f),
                pose = (ref QuadPose p, float t) => QuadMotion.Walk(ref p, t + id * 0.31f) });
        }

        /// <summary>Renders one clip's frames. Returns a one-line report.</summary>
        public static string Render(string name, string outRoot, int fps = 24, int w = 560, int h = 400)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return "ERROR: in play mode";
            var clip = Build(name);
            if (clip == null) return "ERROR: unknown clip " + name;
            var active = SceneManager.GetActiveScene();
            if (active.name != "QuadReel")
            {
                for (int i = 0; i < SceneManager.sceneCount; i++)
                    if (SceneManager.GetSceneAt(i).isDirty) return $"ERROR: scene '{SceneManager.GetSceneAt(i).name}' has unsaved changes";
                prevScene = active.path;
                File.WriteAllText(Path.Combine(outRoot, "prev-scene.txt"), prevScene ?? "");
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                scene.name = "QuadReel";
            }
            foreach (var go in SceneManager.GetActiveScene().GetRootGameObjects()) Object.DestroyImmediate(go);

            var owned = new List<Object>();
            var root = new GameObject("QuadReel");
            var cam = Stage(root, owned, clip);
            var mat = AssetDatabase.LoadAssetAtPath<Material>($"{ViewDir}/Materials/Stylized.mat");
            foreach (var a in clip.actors) a.doll = new QuadDoll(a.look, root.transform, mat);

            string dir = Path.Combine(outRoot, name);
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
            Directory.CreateDirectory(dir);
            int frames = Mathf.RoundToInt(clip.dur * fps);
            var sw = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                for (int f = 0; f < frames; f++)
                {
                    float t = f / (float)fps;
                    var mid = Vector3.zero;
                    foreach (var a in clip.actors)
                    {
                        var p = QuadPose.Stand(a.at(t), a.yaw(t));
                        a.pose(ref p, t);
                        a.doll.Apply(p);
                        mid += p.at;
                    }
                    mid /= clip.actors.Count;
                    var focus = clip.follow ? mid : Mid0(clip);
                    float tall = 0f; foreach (var a in clip.actors) tall = Mathf.Max(tall, a.look.shoulder);
                    var target = focus + new Vector3(0f, tall * 0.6f, 0f);
                    Aim(cam, target, 28f, 34f, clip.span, w / (float)h);
                    Shoot(cam, w, h, Path.Combine(dir, $"g_{f:000}.jpg"));
                    Aim(cam, target, 0f, 6f, clip.sideSpan, w / (float)h);
                    Shoot(cam, w, h, Path.Combine(dir, $"s_{f:000}.jpg"));
                }
            }
            finally
            {
                Object.DestroyImmediate(root);
                foreach (var o in owned) if (o != null) Object.DestroyImmediate(o);
            }
            return $"{name}: {frames} frames x2 in {sw.Elapsed.TotalSeconds:0.0} s";
        }

        static Vector3 Mid0(Clip c)
        {
            var m = Vector3.zero;
            foreach (var a in c.actors) m += a.at(0f);
            return m / c.actors.Count;
        }

        /// <summary>Reopens the scene that was open before the first Render.</summary>
        public static string Finish(string outRoot)
        {
            foreach (var go in SceneManager.GetActiveScene().GetRootGameObjects()) if (go.name == "QuadReel") Object.DestroyImmediate(go);
            string file = Path.Combine(outRoot, "prev-scene.txt");
            string prev = prevScene ?? (File.Exists(file) ? File.ReadAllText(file).Trim() : null);
            if (File.Exists(file)) File.Delete(file);
            prevScene = null;
            if (!string.IsNullOrEmpty(prev) && File.Exists(prev) && SceneManager.GetActiveScene().path != prev)
            {
                EditorSceneManager.OpenScene(prev, OpenSceneMode.Single);
                return "reopened " + prev;
            }
            return "done (no previous scene to reopen)";
        }

        static Camera Stage(GameObject root, List<Object> owned, Clip clip)
        {
            // AnimLab's day box: the Village box's midday sun, trilight ambient, the slice's post grade
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.transform.SetParent(root.transform, false);
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.95f, 0.85f);
            sun.intensity = 1.3f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.85f;
            sun.transform.rotation = Quaternion.Euler(42, -40, 0);
            RenderSettings.sun = sun;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.74f, 0.8f, 0.92f);
            RenderSettings.ambientEquatorColor = new Color(0.68f, 0.65f, 0.6f);
            RenderSettings.ambientGroundColor = new Color(0.42f, 0.36f, 0.3f);
            RenderSettings.fog = false;

            var camGo = new GameObject("ReelCamera");
            camGo.transform.SetParent(root.transform, false);
            var cam = camGo.AddComponent<Camera>();
            cam.enabled = false;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.9f, 0.86f, 0.8f);
            cam.fieldOfView = 22f;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 40f;
            var data = camGo.AddComponent<UniversalAdditionalCameraData>();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            data.antialiasingQuality = AntialiasingQuality.High;

            var vol = new GameObject("PostFX").AddComponent<Volume>();
            vol.transform.SetParent(root.transform, false);
            vol.isGlobal = true;
            vol.sharedProfile = AssetDatabase.LoadAssetAtPath<VolumeProfile>($"{ViewDir}/Post_Slice.asset");

            // a checker of grass, 0.1 u tiles, long enough for the runs
            var mb = new MeshBuilder(1) { jitter = 0f };
            const float tile = 0.1f;
            for (int ix = -15; ix < 60; ix++)
                for (int iz = -12; iz < 12; iz++)
                {
                    float x0 = ix * tile, z0 = iz * tile;
                    var col = ((ix + iz) & 1) == 0 ? new Color(0.47f, 0.6f, 0.33f) : new Color(0.43f, 0.56f, 0.3f);
                    mb.Quad(new Vector3(x0, 0f, z0), new Vector3(x0, 0f, z0 + tile), new Vector3(x0 + tile, 0f, z0 + tile), new Vector3(x0 + tile, 0f, z0), col);
                }
            var mesh = mb.ToMesh("ReelGround");
            owned.Add(mesh);
            var g = new GameObject("Ground");
            g.transform.SetParent(root.transform, false);
            g.AddComponent<MeshFilter>().sharedMesh = mesh;
            g.AddComponent<MeshRenderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>($"{ViewDir}/Materials/Stylized.mat");
            return cam;
        }

        static void Aim(Camera cam, Vector3 target, float yaw, float pitch, float span, float aspect)
        {
            float vfov = cam.fieldOfView * Mathf.Deg2Rad;
            float hfov = 2f * Mathf.Atan(Mathf.Tan(vfov * 0.5f) * aspect);
            float dist = span * 0.5f / Mathf.Tan(hfov * 0.5f);
            cam.transform.position = target + Quaternion.Euler(pitch, yaw, 0) * Vector3.back * dist;
            cam.transform.LookAt(target);
            cam.aspect = aspect;
        }

        static void Shoot(Camera cam, int w, int h, string path)
        {
            var rt = RenderTexture.GetTemporary(w, h, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB, 1);
            cam.targetTexture = rt;
            cam.Render();
            cam.targetTexture = null;
            var keep = RenderTexture.active;
            RenderTexture.active = rt;
            if (readback == null || readback.width != w || readback.height != h)
            {
                if (readback != null) Object.DestroyImmediate(readback);
                readback = new Texture2D(w, h, TextureFormat.RGB24, false) { hideFlags = HideFlags.DontSave };
            }
            readback.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            readback.Apply(false);
            RenderTexture.active = keep;
            RenderTexture.ReleaseTemporary(rt);
            File.WriteAllBytes(path, readback.EncodeToJPG(90));
        }
    }
}
