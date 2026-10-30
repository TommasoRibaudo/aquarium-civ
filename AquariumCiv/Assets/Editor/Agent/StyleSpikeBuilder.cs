using System.Linq;
using AquariumCiv.Spikes.StyleKit;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace AquariumCiv.EditorTools
{
    /// <summary>
    /// Builds the style-kit spike scenes (docs/research/07-art-direction.md §7.3, everything code-built, no KayKit):
    /// one Camp box and one Village box on a shelf, each with its own light and grade. The boxes generate themselves
    /// (StyleBox), so the scene only holds the rig. Call from the menu or `unity command script eval`.
    /// </summary>
    public static class StyleSpikeBuilder
    {
        const string Dir = "Assets/Spikes/StyleKit";

        [MenuItem("AquariumCiv/Agent/Style Spike/Build Camp + Village")]
        public static string BuildBoth() => Build(Era.Camp) + "\n" + Build(Era.Village);

        public static string Build(Era era)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var solid = Mat("Stylized", "AquariumCiv/Spike/Stylized", m => { });
            var water = Mat("Water", "AquariumCiv/Spike/Stylized", m => { m.SetFloat("_Rim", 0.45f); m.SetFloat("_BandSoft", 0.3f); });
            var glass = Mat("GlassV2", "AquariumCiv/Spike/GlassV2", m => { m.SetColor("_CleanColor", new Color(0.86f, 0.95f, 0.95f, 0.05f)); m.SetFloat("_FresnelStrength", 0.35f); m.SetFloat("_EdgeWidth", 0.14f); m.SetColor("_EdgeColor", new Color(0.55f, 0.78f, 0.74f, 0.55f)); m.SetFloat("_Condensation", 0.2f); m.SetFloat("_StreakAlpha", 0.12f); });

            bool camp = era == Era.Camp;
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = camp ? new Color(1f, 0.86f, 0.7f) : new Color(1f, 0.96f, 0.88f);
            sun.intensity = camp ? 1.25f : 1.35f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.85f;
            sun.transform.rotation = camp ? Quaternion.Euler(30, -58, 0) : Quaternion.Euler(47, -35, 0);
            RenderSettings.sun = sun;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = camp ? new Color(0.7f, 0.78f, 0.92f) : new Color(0.76f, 0.82f, 0.92f);
            RenderSettings.ambientEquatorColor = new Color(0.68f, 0.65f, 0.6f);
            RenderSettings.ambientGroundColor = new Color(0.42f, 0.36f, 0.3f);
            RenderSettings.fog = false;

            var boxGo = new GameObject("StyleBox");
            boxGo.SetActive(false);
            var box = boxGo.AddComponent<StyleBox>();
            box.era = era; box.solid = solid; box.water = water; box.glass = glass;

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
            // Fit the whole box horizontally on a 9:16 portrait screen.
            float halfH = Mathf.Atan(Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * 9f / 16f);
            orbit.distance = new Vector3(StyleBox.HX + 0.3f, 1.7f, StyleBox.HZ + 0.3f).magnitude / Mathf.Tan(halfH) * 0.88f;
            orbit.Apply();

            var vol = new GameObject("PostFX").AddComponent<Volume>();
            vol.isGlobal = true;
            vol.sharedProfile = Profile(era, orbit.distance);

            boxGo.SetActive(true); // generates now
            var info = box.lastBuildInfo;
            var path = $"{Dir}/StyleSpike_{era}.unity";
            EditorSceneManager.SaveScene(scene, path);
            return $"{path}: {info}";
        }

        static Material Mat(string name, string shader, System.Action<Material> setup)
        {
            var path = $"{Dir}/Materials/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                System.IO.Directory.CreateDirectory($"{Dir}/Materials");
                m = new Material(Shader.Find(shader));
                AssetDatabase.CreateAsset(m, path);
            }
            m.shader = Shader.Find(shader);
            setup(m);
            EditorUtility.SetDirty(m);
            return m;
        }

        static VolumeProfile Profile(Era era, float camDistance)
        {
            var path = $"{Dir}/Post_{era}.asset";
            var p = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if (p == null) { p = ScriptableObject.CreateInstance<VolumeProfile>(); AssetDatabase.CreateAsset(p, path); }
            foreach (var c in p.components.ToList()) { p.Remove(c.GetType()); Object.DestroyImmediate(c, true); }
            bool camp = era == Era.Camp;
            var tone = p.Add<Tonemapping>(true); tone.mode.Override(TonemappingMode.Neutral);
            var ca = p.Add<ColorAdjustments>(true);
            ca.postExposure.Override(0f); ca.contrast.Override(10f); ca.saturation.Override(camp ? -2f : 6f);
            var split = p.Add<SplitToning>(true);
            split.shadows.Override(camp ? new Color(0.36f, 0.5f, 0.6f) : new Color(0.42f, 0.5f, 0.6f));
            split.highlights.Override(camp ? new Color(1f, 0.8f, 0.6f) : new Color(1f, 0.88f, 0.72f));
            split.balance.Override(-15f);
            var vig = p.Add<Vignette>(true); vig.intensity.Override(0.26f); vig.smoothness.Override(0.45f);
            var bloom = p.Add<Bloom>(true); bloom.intensity.Override(0.4f); bloom.threshold.Override(1.35f); bloom.scatter.Override(0.5f); // glows only (fire, windows)
            // No depth of field: even an un-overridden DepthOfField component softened the whole frame (Gaussian
            // tilt-shift is a follow-up, research 07 §4.6).
            EditorUtility.SetDirty(p);
            AssetDatabase.SaveAssets();
            return p;
        }
    }
}
