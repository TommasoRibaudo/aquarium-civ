using System.Linq;
using AquariumCiv.Game;
using AquariumCiv.View;
using AquariumCiv.View.Demo;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace AquariumCiv.EditorTools
{
    /// <summary>
    /// Builds the vertical slice's scene (vertical-slice-plan §7): the box on its shelf drawn by the View assembly's
    /// style kit (<see cref="BoxView"/>: terrain, soil cut, glass, plinth, shelf, wall), a few placeholder pieces from
    /// the TEMPORARY demo layout (until #33 draws the sim), the camera rig with light and grade, and the
    /// <see cref="GameClock"/>. The box generates itself on load, so the scene only holds the rig. Not added to Build
    /// Settings. Call from the menu or `unity command script eval`.
    /// </summary>
    public static class SliceSceneBuilder
    {
        public const string ScenePath = "Assets/Game/Scenes/Slice.unity";
        const string ViewDir = "Assets/Game/View";

        [MenuItem("AquariumCiv/Agent/Slice/Build Slice Scene")]
        public static string Build()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var solid = Mat("Stylized", "AquariumCiv/View/Stylized", m => { });
            var water = Mat("Water", "AquariumCiv/View/Stylized", m => { m.SetFloat("_Rim", 0.45f); m.SetFloat("_BandSoft", 0.3f); });
            var glass = Mat("Glass", "AquariumCiv/View/Glass", m =>
            {
                m.SetColor("_CleanColor", new Color(0.86f, 0.95f, 0.95f, 0.05f)); m.SetFloat("_FresnelStrength", 0.35f);
                m.SetFloat("_EdgeWidth", 0.14f); m.SetColor("_EdgeColor", new Color(0.55f, 0.78f, 0.74f, 0.55f));
                m.SetFloat("_Condensation", 0.2f); m.SetFloat("_StreakAlpha", 0.12f);
            });
            var glow = Mat("Glow", "AquariumCiv/View/Glow", m => { });

            // Light: the Village box's higher midday sun, trilight ambient (style kit).
            var sun = new GameObject("Sun").AddComponent<Light>();
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

            var boxGo = new GameObject("Box");
            boxGo.SetActive(false);
            var box = boxGo.AddComponent<BoxView>();
            box.solid = solid; box.water = water; box.glass = glass; box.glow = glow;
            boxGo.AddComponent<TemporaryDemoLayout>(); // TEMPORARY until #33 draws the sim
            boxGo.AddComponent<NightLampDriver>();

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
            var rig = camGo.AddComponent<BoxCamera>();
            rig.Apply();

            var vol = new GameObject("PostFX").AddComponent<Volume>();
            vol.isGlobal = true;
            vol.sharedProfile = Profile();

            var clock = new GameObject("GameClock").AddComponent<GameClock>();
            clock.config.startNotables = 3;

            boxGo.SetActive(true); // generates now
            var info = box.lastBuildInfo;
            System.IO.Directory.CreateDirectory("Assets/Game/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.Refresh();
            return $"built {ScenePath}: {info}";
        }

        /// <summary>
        /// Renders the main camera to a PNG at a phone size (edit or play mode, URP post included), framed by the
        /// <see cref="BoxCamera"/> for that aspect. <paramref name="zoom"/> &lt; 1 moves in on <paramref name="target"/>
        /// (null = the box centre). The camera rig is restored afterwards.
        /// </summary>
        public static string Shot(string path, int w = 1179, int h = 2556, float zoom = 1f, Vector3? target = null, float yaw = float.NaN, float pitch = float.NaN)
        {
            var cam = Camera.main;
            var rig = cam.GetComponent<BoxCamera>();
            var keep = (rig.target, rig.zoom, rig.yaw, rig.pitch);
            var rt = RenderTexture.GetTemporary(w, h, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB, 1);
            var prev = cam.targetTexture;
            try
            {
                cam.targetTexture = rt;
                cam.aspect = w / (float)h;
                if (target.HasValue) rig.target = target.Value;
                rig.zoom = zoom;
                if (!float.IsNaN(yaw)) rig.yaw = yaw;
                if (!float.IsNaN(pitch)) rig.pitch = pitch;
                rig.Apply();
                cam.Render();
                var active = RenderTexture.active;
                RenderTexture.active = rt;
                var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                tex.Apply();
                RenderTexture.active = active;
                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));
                System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
            }
            finally
            {
                cam.targetTexture = prev;
                cam.ResetAspect();
                RenderTexture.ReleaseTemporary(rt);
                (rig.target, rig.zoom, rig.yaw, rig.pitch) = keep;
                rig.Apply();
            }
            return path;
        }

        static Material Mat(string name, string shader, System.Action<Material> setup)
        {
            var path = $"{ViewDir}/Materials/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                System.IO.Directory.CreateDirectory($"{ViewDir}/Materials");
                m = new Material(Shader.Find(shader));
                AssetDatabase.CreateAsset(m, path);
            }
            m.shader = Shader.Find(shader);
            setup(m);
            EditorUtility.SetDirty(m);
            return m;
        }

        static VolumeProfile Profile()
        {
            var path = $"{ViewDir}/Post_Slice.asset";
            var p = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if (p == null) { p = ScriptableObject.CreateInstance<VolumeProfile>(); AssetDatabase.CreateAsset(p, path); }
            foreach (var c in p.components.ToList()) { p.Remove(c.GetType()); Object.DestroyImmediate(c, true); }
            var tone = p.Add<Tonemapping>(true); tone.mode.Override(TonemappingMode.Neutral);
            var ca = p.Add<ColorAdjustments>(true);
            ca.postExposure.Override(0f); ca.contrast.Override(10f); ca.saturation.Override(5f);
            var split = p.Add<SplitToning>(true);
            split.shadows.Override(new Color(0.42f, 0.5f, 0.6f));
            split.highlights.Override(new Color(1f, 0.88f, 0.72f));
            split.balance.Override(-15f);
            var vig = p.Add<Vignette>(true); vig.intensity.Override(0.26f); vig.smoothness.Override(0.45f);
            var bloom = p.Add<Bloom>(true); bloom.intensity.Override(0.5f); bloom.threshold.Override(1.3f); bloom.scatter.Override(0.55f); // glows only
            // No depth of field: an un-overridden DepthOfField component softened the whole frame (style-kit spike).
            EditorUtility.SetDirty(p);
            AssetDatabase.SaveAssets();
            return p;
        }
    }
}
