using System.Collections.Generic;
using System.Linq;
using AquariumCiv.Spikes.Growth;
using AquariumCiv.Spikes.Growth.View;
using AquariumCiv.Spikes.Perf;
using AquariumCiv.Spikes.StyleKit;
using Era = AquariumCiv.Spikes.StyleKit.Era;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace AquariumCiv.EditorTools
{
    /// <summary>
    /// Issue #35: the device perf check of the style kit. Builds `Assets/Spikes/Perf/PerfCheck.unity` (the Growth Lab
    /// town at year 110, organic, plus the style kit's camp and village, one at a time, with <see cref="PerfCheck"/>'s
    /// readout), and switches Build Settings between the perf build and the prototype builds (docs/testflight.md).
    /// Call from the menu or `unity command script eval`.
    /// </summary>
    public static class PerfSceneBuilder
    {
        public const string ScenePath = "Assets/Spikes/Perf/PerfCheck.unity";
        const string KitDir = "Assets/Spikes/StyleKit";

        /// <summary>The 0.3.x prototype build list, in order (Hub first = the launch scene).</summary>
        public static readonly string[] PrototypeScenes =
        {
            "Assets/Prototypes/_Hub/Hub.unity",
            "Assets/Prototypes/P2_ReturnToMess/P2_ReturnToMess.unity",
            "Assets/Prototypes/P6_ErasAndShelf/P6_ErasAndShelf.unity",
            "Assets/Prototypes/P5_KeepersNudge/P5_KeepersNudge.unity",
            "Assets/Prototypes/P4_BookOfAges/P4_BookOfAges.unity",
            "Assets/Prototypes/P3_WhileYouWereAway/P3_WhileYouWereAway.unity",
            "Assets/Prototypes/P1_GlassMess/P1_GlassMess.unity",
            "Assets/Prototypes/_Shared/Sandbox.unity",
            "Assets/Scenes/SampleScene.unity",
        };

        [MenuItem("AquariumCiv/Agent/Perf Check/Build Scene")]
        public static string BuildScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var solid = AssetDatabase.LoadAssetAtPath<Material>($"{KitDir}/Materials/Stylized.mat");
            var water = AssetDatabase.LoadAssetAtPath<Material>($"{KitDir}/Materials/Water.mat");
            var glass = AssetDatabase.LoadAssetAtPath<Material>($"{KitDir}/Materials/GlassV2.mat");
            var postCamp = AssetDatabase.LoadAssetAtPath<VolumeProfile>($"{KitDir}/Post_Camp.asset");
            var postVillage = AssetDatabase.LoadAssetAtPath<VolumeProfile>($"{KitDir}/Post_Village.asset");
            if (solid == null || water == null || glass == null || postCamp == null || postVillage == null)
                return "missing style-kit materials or profiles: run Style Spike/Build Camp + Village first";

            // Same light rig as StyleSpikeBuilder / GrowthLabBuilder; PerfCheck re-aims the sun per box.
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.85f;
            RenderSettings.sun = sun;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientEquatorColor = new Color(0.68f, 0.65f, 0.6f);
            RenderSettings.ambientGroundColor = new Color(0.42f, 0.36f, 0.3f);
            RenderSettings.fog = false;

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
            camGo.transform.position = new Vector3(0, 12, -38);
            camGo.transform.LookAt(new Vector3(0, 1, 0));

            var vol = new GameObject("PostFX").AddComponent<Volume>();
            vol.isGlobal = true;
            vol.sharedProfile = postVillage;

            // Boxes, saved inactive; PerfCheck activates one at a time (each generates itself on enable).
            var growthGo = new GameObject("Growth town y110");
            growthGo.SetActive(false);
            var growth = growthGo.AddComponent<GrowthBox>();
            growth.seed = 7; growth.terrain = 0; growth.variant = Variant.Organic; growth.year = 110;
            growth.labels = false; growth.showCandidates = false; growth.overlay = Overlay.None;
            growth.solid = solid; growth.water = water; growth.glass = glass;

            GameObject Style(Era era)
            {
                var go = new GameObject($"{era} (style kit)");
                go.SetActive(false);
                var box = go.AddComponent<StyleBox>();
                box.era = era; box.solid = solid; box.water = water; box.glass = glass;
                return go;
            }
            var campGo = Style(Era.Camp);
            var villageGo = Style(Era.Village);

            var rig = new GameObject("PerfCheck");
            var perf = rig.AddComponent<PerfCheck>();
            perf.sun = sun; perf.volume = vol; perf.cam = cam;
            var noon = new Color(1f, 0.96f, 0.88f);
            perf.entries = new[]
            {
                new PerfCheck.Entry { name = "Growth town y110", root = growthGo, profile = postVillage, sunEuler = new Vector3(47, -35, 0),
                    sunColor = noon, sunIntensity = 1.35f, ambientSky = new Color(0.76f, 0.82f, 0.92f),
                    target = new Vector3(0.05f, 0.95f, -0.2f), pitch = 52f, yaw = 12f, radius = 4.3f },
                new PerfCheck.Entry { name = "Camp (style kit)", root = campGo, profile = postCamp, sunEuler = new Vector3(30, -58, 0),
                    sunColor = new Color(1f, 0.86f, 0.7f), sunIntensity = 1.25f, ambientSky = new Color(0.7f, 0.78f, 0.92f),
                    target = new Vector3(0, 1f, 0), pitch = 34f, yaw = 28f, radius = 4.5f },
                new PerfCheck.Entry { name = "Village (style kit)", root = villageGo, profile = postVillage, sunEuler = new Vector3(47, -35, 0),
                    sunColor = noon, sunIntensity = 1.35f, ambientSky = new Color(0.76f, 0.82f, 0.92f),
                    target = new Vector3(0, 1f, 0), pitch = 34f, yaw = 28f, radius = 4.5f },
            };
            sun.transform.rotation = Quaternion.Euler(47, -35, 0);
            sun.color = noon; sun.intensity = 1.35f;
            RenderSettings.ambientSkyColor = new Color(0.76f, 0.82f, 0.92f);

            new GameObject("FeedbackSender").AddComponent<AquariumCiv.Prototypes.FeedbackSender>();

            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(ScenePath));
            EditorSceneManager.SaveScene(scene, ScenePath);
            return $"{ScenePath}: {perf.entries.Length} boxes";
        }

        /// <summary>
        /// Perf build (issue #35): only PerfCheck.unity is enabled (first = launch scene); the prototype scenes stay in
        /// the list, disabled, in their old order. Frame Timing Stats on (FrameTimingManager needs it in release builds).
        /// </summary>
        [MenuItem("AquariumCiv/Agent/Perf Check/Build Settings: perf build")]
        public static string UsePerfBuildSettings()
        {
            var list = new List<EditorBuildSettingsScene> { new(ScenePath, true) };
            list.AddRange(PrototypeScenes.Select(p => new EditorBuildSettingsScene(p, false)));
            EditorBuildSettings.scenes = list.ToArray();
            PlayerSettings.enableFrameTimingStats = true;
            PlayerSettings.bundleVersion = "0.4.0";
            AssetDatabase.SaveAssets();
            return Describe();
        }

        /// <summary>Back to the 0.3.x prototype build list (Hub first, all prototypes enabled, no perf scene).</summary>
        [MenuItem("AquariumCiv/Agent/Perf Check/Build Settings: prototype build")]
        public static string UsePrototypeBuildSettings()
        {
            EditorBuildSettings.scenes = PrototypeScenes.Select(p => new EditorBuildSettingsScene(p, true)).ToArray();
            AssetDatabase.SaveAssets();
            return Describe() + " (set PlayerSettings.bundleVersion by hand: 0.3.x; Frame Timing Stats can stay on)";
        }

        public static string Describe() =>
            string.Join(", ", EditorBuildSettings.scenes.Select(s => (s.enabled ? "+" : "-") + System.IO.Path.GetFileNameWithoutExtension(s.path))) +
            $" | version {PlayerSettings.bundleVersion}, frame timing stats {PlayerSettings.enableFrameTimingStats}";
    }
}
