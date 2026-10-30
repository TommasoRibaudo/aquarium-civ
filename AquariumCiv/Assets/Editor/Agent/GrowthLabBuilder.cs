using System.Collections.Generic;
using System.IO;
using AquariumCiv.Spikes.Growth;
using AquariumCiv.Spikes.Growth.View;
using AquariumCiv.Spikes.StyleKit;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace AquariumCiv.EditorTools
{
    /// <summary>
    /// Growth Lab spike (research 08 §5): builds the time-lapse scene (the style kit's light, camera and grade around a
    /// <see cref="GrowthBox"/>) and exports stills and time-lapse frames from edit mode. Call from the menu or from
    /// `unity command script eval`.
    /// </summary>
    public static class GrowthLabBuilder
    {
        public const string ScenePath = "Assets/Spikes/Growth/GrowthLab.unity";
        const string KitDir = "Assets/Spikes/StyleKit";

        [MenuItem("AquariumCiv/Agent/Growth Lab/Build Scene")]
        public static string BuildScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.96f, 0.88f);
            sun.intensity = 1.35f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.85f;
            sun.transform.rotation = Quaternion.Euler(47, -35, 0);
            RenderSettings.sun = sun;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.76f, 0.82f, 0.92f);
            RenderSettings.ambientEquatorColor = new Color(0.68f, 0.65f, 0.6f);
            RenderSettings.ambientGroundColor = new Color(0.42f, 0.36f, 0.3f);
            RenderSettings.fog = false;

            var boxGo = new GameObject("GrowthBox");
            boxGo.SetActive(false);
            var box = boxGo.AddComponent<GrowthBox>();
            box.solid = AssetDatabase.LoadAssetAtPath<Material>($"{KitDir}/Materials/Stylized.mat");
            box.water = AssetDatabase.LoadAssetAtPath<Material>($"{KitDir}/Materials/Water.mat");
            box.glass = AssetDatabase.LoadAssetAtPath<Material>($"{KitDir}/Materials/GlassV2.mat");

            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.9f, 0.86f, 0.8f);
            cam.fieldOfView = 22f;
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 120f;
            var data = camGo.AddComponent<UniversalAdditionalCameraData>();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            data.antialiasingQuality = AntialiasingQuality.High;
            var orbit = camGo.AddComponent<SpikeOrbit>();
            orbit.target = new Vector3(0, 1.0f, 0);
            SetView(cam, "overview");

            var vol = new GameObject("PostFX").AddComponent<Volume>();
            vol.isGlobal = true;
            vol.sharedProfile = AssetDatabase.LoadAssetAtPath<VolumeProfile>($"{KitDir}/Post_Village.asset"); // shared, not modified

            boxGo.SetActive(true);
            EditorSceneManager.SaveScene(scene, ScenePath);
            return $"{ScenePath}: {box.lastBuildInfo}";
        }

        /// <summary>Named camera views: overview (the time-lapse), top (overlays), front (low beauty angle), close.</summary>
        public static void SetView(Camera cam, string view, float aspect = 16f / 9f)
        {
            var orbit = cam.GetComponent<SpikeOrbit>();
            float yaw = 12f, pitch = 52f, dist = 16.8f;
            var target = new Vector3(0.05f, 0.95f, -0.2f);
            switch (view)
            {
                case "top": yaw = 0f; pitch = 78f; dist = 14.5f; target = new Vector3(0, 1f, 0f); break;
                case "front": yaw = 24f; pitch = 34f; dist = 13.5f; break;
                case "close": yaw = 20f; pitch = 42f; dist = 7.5f; target = new Vector3(-0.3f, 1.05f, -0.25f); break;
            }
            if (aspect < 1f) dist *= 1.9f;
            if (orbit != null) { orbit.target = target; orbit.yaw = yaw; orbit.pitch = pitch; orbit.distance = dist; orbit.Apply(); }
            else
            {
                cam.transform.position = target + Quaternion.Euler(pitch, yaw, 0) * Vector3.back * dist;
                cam.transform.LookAt(target);
            }
        }

        public static GrowthBox Box() => Object.FindAnyObjectByType<GrowthBox>();

        /// <summary>Render the main camera into a JPG (edit mode, post-processing included).</summary>
        public static void Render(string path, int w, int h, int quality = 86)
        {
            var cam = Camera.main;
            var rt = RenderTexture.GetTemporary(w, h, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB, 1);
            var prev = cam.targetTexture;
            cam.targetTexture = rt;
            cam.Render();
            cam.targetTexture = prev;
            var keep = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            RenderTexture.active = keep;
            RenderTexture.ReleaseTemporary(rt);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, tex.EncodeToJPG(quality));
            Object.DestroyImmediate(tex);
        }

        /// <summary>Set up the box for a run and a year (rebuilds).</summary>
        public static GrowthBox Show(int seed, int terrain, Variant v, int year, Overlay overlay = Overlay.None, bool candidates = false, bool labels = false)
        {
            var box = Box();
            box.seed = seed; box.terrain = terrain; box.variant = v; box.year = year;
            box.overlay = overlay; box.showCandidates = candidates; box.labels = labels;
            box.Build();
            return box;
        }

        /// <summary>The snapshot year that includes the n-th placement of a kind (placements in months (12(y-1), 12y]).</summary>
        public static int PlacementYear(Kind kind, int n = 0)
        {
            var w = Box().EnsureWorld();
            foreach (var p in w.placements)
                if (p.kind == kind && n-- == 0) return (int)((p.month + 11) / 12);
            return -1;
        }

        /// <summary>Render frames [from, to] of a run's time-lapse into dir/f###.jpg. Returns ms per frame.</summary>
        public static string Frames(int seed, int terrain, Variant v, string dir, int from, int to, int w = 960, int h = 540, string view = "overview")
        {
            SetView(Camera.main, view, w / (float)h);
            var sw = System.Diagnostics.Stopwatch.StartNew();
            int n = 0;
            for (int y = from; y <= to; y++, n++)
            {
                Show(seed, terrain, v, y);
                Render($"{dir}/f{y:000}.jpg", w, h, 88);
            }
            return $"{n} frames in {sw.ElapsedMilliseconds} ms ({sw.ElapsedMilliseconds / Mathf.Max(1, n)} ms each)";
        }
    }

    [CustomEditor(typeof(GrowthBox))]
    public class GrowthBoxEditor : Editor
    {
        enum Tool { None, Rain, Grove, Channel }
        static Tool tool;
        static bool playing;
        static double lastTick;
        static float yearsPerSecond = 4f;

        public override void OnInspectorGUI()
        {
            var box = (GrowthBox)target;
            EditorGUILayout.HelpBox(box.lastBuildInfo ?? "(not built)", MessageType.None);
            EditorGUI.BeginChangeCheck();
            DrawDefaultInspector();
            bool changed = EditorGUI.EndChangeCheck();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Time-lapse", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            int y = EditorGUILayout.IntSlider("Year", box.year, 0, GrowthBox.MaxYear);
            if (EditorGUI.EndChangeCheck()) { box.year = y; changed = true; }
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(playing ? "Pause" : "Play")) { playing = !playing; lastTick = EditorApplication.timeSinceStartup; if (box.year >= GrowthBox.MaxYear) box.year = 0; }
                yearsPerSecond = EditorGUILayout.Slider(yearsPerSecond, 1f, 12f);
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Debug cheats", EditorStyles.boldLabel);
            tool = (Tool)EditorGUILayout.EnumPopup("Click in Scene view to", tool);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Skip 10 years")) box.SkipYears(10);
                if (GUILayout.Button("Force next era")) box.ForceNextEra();
                if (GUILayout.Button("Clear cheats")) box.ClearCheats();
            }
            if (box.world != null)
            {
                var s = GrowthChecks.Check(box.world);
                var e = GrowthChecks.Emerge(box.world);
                EditorGUILayout.HelpBox($"Year 120: {s}\n{e}", MessageType.None);
            }
            if (changed) { box.Build(); SceneView.RepaintAll(); }
        }

        void OnEnable() => EditorApplication.update += Tick;
        void OnDisable() => EditorApplication.update -= Tick;

        void Tick()
        {
            if (!playing || target == null) return;
            var box = (GrowthBox)target;
            double now = EditorApplication.timeSinceStartup;
            if (now - lastTick < 1.0 / yearsPerSecond) return;
            lastTick = now;
            if (box.year >= GrowthBox.MaxYear) { playing = false; return; }
            box.year++;
            box.bakeAO = false; // fast scrub while playing
            box.Build();
            box.bakeAO = true;
            Repaint();
            SceneView.RepaintAll();
        }

        void OnSceneGUI()
        {
            if (tool == Tool.None) return;
            var e = Event.current;
            HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));
            if (e.type != EventType.MouseDown || e.button != 0) return;
            var box = (GrowthBox)target;
            var ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
            ray.origin = box.transform.InverseTransformPoint(ray.origin);
            ray.direction = box.transform.InverseTransformDirection(ray.direction);
            for (float t = 0; t < 60f; t += 0.01f)
            {
                var p = ray.GetPoint(t);
                if (Mathf.Abs(p.x) > GrowthBox.HX || Mathf.Abs(p.z) > GrowthBox.HZ) continue;
                if (p.y > box.Height(p.x, p.z)) continue;
                string kind = tool == Tool.Rain ? "rain" : tool == Tool.Grove ? "grove" : "channel";
                Undo.RecordObject(box, "Growth Lab cheat");
                box.AddCheat(kind, p.x, p.z);
                Debug.Log($"[GrowthLab] {kind} at ({p.x:0.00}, {p.z:0.00}) in year {box.year}");
                e.Use();
                break;
            }
        }
    }
}
