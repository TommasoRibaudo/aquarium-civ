using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace AquariumCiv.EditorTools
{
    /// <summary>
    /// Builds Assets/Scenes/LookTest.unity: a small KayKit village inside a glass terrarium,
    /// framed like a phone diorama. Throwaway art-direction scene, safe to rebuild at any time.
    /// </summary>
    public static class LookTestBuilder
    {
        const string ScenePath = "Assets/Scenes/LookTest.unity";
        const string MaterialDir = "Assets/Art/Materials";
        const string KayKit = "Assets/ThirdParty/KayKit";
        const string Medieval = KayKit + "/MedievalHexagon";
        const int Radius = 3;

        [MenuItem("AquariumCiv/Agent/Build Look Test Scene")]
        public static string Build()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var rng = new System.Random(7);

            var root = new GameObject("Terrarium").transform;
            var ground = new GameObject("Ground").transform;
            ground.SetParent(root);

            // Hex grid. Measure the tile so spacing works whatever its orientation.
            var grassTile = Load($"{Medieval}/tiles/base/hex_grass.fbx");
            var waterTile = Load($"{Medieval}/tiles/base/hex_water.fbx");
            var tileSize = MeasureBounds(grassTile).size;
            bool pointyTop = tileSize.z > tileSize.x;
            float w = pointyTop ? tileSize.x : tileSize.z;

            var waterCells = new HashSet<Vector2Int> { new(2, -1), new(3, -1), new(3, -2), new(2, -2) };
            var cells = new List<Vector2Int>();
            for (int q = -Radius; q <= Radius; q++)
            for (int r = Mathf.Max(-Radius, -q - Radius); r <= Mathf.Min(Radius, -q + Radius); r++)
                cells.Add(new Vector2Int(q, r));

            var cellPos = new Dictionary<Vector2Int, Vector3>();
            foreach (var c in cells)
            {
                // Axial -> world. Spacing derived from the flat-to-flat width w.
                Vector3 p = pointyTop
                    ? new Vector3(w * (c.x + c.y * 0.5f), 0, w * 0.8660254f * c.y)
                    : new Vector3(w * 0.8660254f * c.x, 0, w * (c.y + c.x * 0.5f));
                cellPos[c] = p;
                Place(waterCells.Contains(c) ? waterTile : grassTile, ground, p, 0, 1);
            }

            // Village content, by cell.
            var props = new GameObject("Village").transform;
            props.SetParent(root);
            string B(string n) => $"{Medieval}/buildings/green/building_{n}_green.fbx";
            string N(string n) => $"{Medieval}/decoration/nature/{n}.fbx";
            string P(string n) => $"{Medieval}/decoration/props/{n}.fbx";

            var layout = new (Vector2Int cell, string path, float yaw)[]
            {
                (new(0, 0), B("well"), 0),
                (new(1, 0), B("home_A"), 60),
                (new(-1, 1), B("home_B"), 180),
                (new(0, -1), B("home_A"), 300),
                (new(-1, 0), B("market"), 120),
                (new(1, 1), B("tavern"), 240),
                (new(2, 0), B("watermill"), 0),
                (new(-2, 2), B("windmill"), 30),
                (new(0, 2), $"{Medieval}/buildings/neutral/building_grain.fbx", 0),
                (new(-3, 3), N("mountain_A_grass_trees"), 0),
                (new(-3, 2), N("hills_A_trees"), 60),
                (new(-2, 3), N("hills_B_trees"), 0),
                (new(0, -3), N("trees_A_large"), 0),
                (new(1, -3), N("trees_B_medium"), 90),
                (new(-1, -2), N("trees_A_medium"), 0),
                (new(-2, -1), N("trees_B_large"), 180),
                (new(-3, 0), N("trees_A_small"), 0),
                (new(3, 0), N("trees_B_small"), 0),
                (new(2, 1), N("rock_single_A"), 0),
                (new(1, 2), N("trees_A_cut"), 0),
                (new(-2, 0), P("tent"), 210),
                (new(0, 1), P("resource_lumber"), 45),
                (new(1, -1), P("crate_A_big"), 0),
            };
            float homeHeight = 1f;
            foreach (var (cell, path, yaw) in layout)
            {
                var prefab = Load(path);
                if (prefab == null || !cellPos.ContainsKey(cell)) continue;
                var go = Place(prefab, props, cellPos[cell] + Vector3.up * tileSize.y, yaw, 1);
                if (path.Contains("home_A")) homeHeight = MeasureBounds(prefab).size.y;
            }

            // Tiny citizens: sampled into an idle pose, scaled small against the houses.
            var people = new GameObject("Citizens").transform;
            people.SetParent(root);
            var charPaths = new[] { "Barbarian", "Knight", "Mage", "Rogue", "RogueHooded" }
                .Select(n => $"{KayKit}/Adventurers/{n}.fbx").ToArray();
            var spots = new[] { new Vector2(0.6f, 0.9f), new(-0.8f, 0.5f), new(1.4f, -0.6f), new(-0.3f, -1.2f),
                                new(2.1f, 0.8f), new(-1.6f, -0.4f), new(0.2f, 2.0f), new(-2.2f, 1.4f) };
            for (int i = 0; i < spots.Length; i++)
            {
                var prefab = Load(charPaths[i % charPaths.Length]);
                if (prefab == null) continue;
                float h = MeasureBounds(prefab).size.y;
                float scale = homeHeight * 0.5f / Mathf.Max(h, 0.01f);
                var pos = new Vector3(spots[i].x * w * 0.9f, tileSize.y, spots[i].y * w * 0.9f);
                var go = Place(prefab, people, pos, rng.Next(0, 360), scale);
                PoseIdle(go, charPaths[i % charPaths.Length]);
            }

            // Glass box around the island.
            var islandBounds = new Bounds(Vector3.zero, Vector3.zero);
            foreach (var r in root.GetComponentsInChildren<Renderer>()) islandBounds.Encapsulate(r.bounds);
            BuildTerrarium(root, islandBounds, homeHeight);

            var boxBounds = new Bounds();
            bool firstBox = true;
            foreach (var r in root.Find("GlassBox").GetComponentsInChildren<Renderer>())
            {
                if (firstBox) { boxBounds = r.bounds; firstBox = false; }
                else boxBounds.Encapsulate(r.bounds);
            }
            BuildLightingAndCamera(boxBounds);

            System.IO.Directory.CreateDirectory("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);
            return $"Built {ScenePath}: {cells.Count} tiles, {layout.Length} props, {spots.Length} citizens, " +
                   $"tile {tileSize}, pointyTop={pointyTop}, homeHeight={homeHeight:F2}";
        }

        static GameObject Load(string path)
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (go == null) Debug.LogWarning($"[LookTest] Missing asset {path}");
            return go;
        }

        static GameObject Place(GameObject prefab, Transform parent, Vector3 pos, float yaw, float scale)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0, yaw, 0));
            go.transform.localScale = Vector3.one * scale;
            return go;
        }

        static Bounds MeasureBounds(GameObject prefab)
        {
            var b = new Bounds();
            bool first = true;
            foreach (var r in prefab.GetComponentsInChildren<Renderer>())
            {
                if (first) { b = r.bounds; first = false; }
                else b.Encapsulate(r.bounds);
            }
            return b;
        }

        static void PoseIdle(GameObject go, string fbxPath)
        {
            var clip = AssetDatabase.LoadAllAssetsAtPath(fbxPath).OfType<AnimationClip>()
                .Where(c => !c.name.StartsWith("__preview__"))
                .OrderByDescending(c => c.name.ToLower().Contains("idle"))
                .FirstOrDefault();
            if (clip != null) clip.SampleAnimation(go, 0.3f);
        }

        static void BuildTerrarium(Transform root, Bounds island, float homeHeight)
        {
            var glass = GetOrCreateMaterial("Glass", m =>
            {
                m.SetFloat("_Surface", 1);
                m.SetFloat("_Blend", 0);
                m.SetFloat("_ZWrite", 0);
                m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                m.renderQueue = (int)RenderQueue.Transparent;
                m.SetColor("_BaseColor", new Color(0.85f, 0.95f, 1f, 0.10f));
                m.SetFloat("_Smoothness", 0.97f);
            });
            var frame = GetOrCreateMaterial("Frame", m =>
            {
                m.SetColor("_BaseColor", new Color(0.18f, 0.14f, 0.11f));
                m.SetFloat("_Smoothness", 0.35f);
            });
            var plinth = GetOrCreateMaterial("Plinth", m =>
            {
                m.SetColor("_BaseColor", new Color(0.36f, 0.25f, 0.17f));
                m.SetFloat("_Smoothness", 0.2f);
            });

            var box = new GameObject("GlassBox").transform;
            box.SetParent(root);
            float pad = 0.25f;
            float sx = island.size.x + pad * 2, sz = island.size.z + pad * 2;
            float bottom = island.min.y - 0.05f;
            float height = Mathf.Max(island.size.y, homeHeight * 2.5f) + 0.6f;
            var c = new Vector3(island.center.x, bottom + height / 2, island.center.z);
            const float t = 0.03f, bar = 0.07f;

            Cube(box, "Plinth", new Vector3(c.x, bottom - 0.2f, c.z), new Vector3(sx + 0.3f, 0.4f, sz + 0.3f), plinth);
            Cube(box, "Glass_N", c + new Vector3(0, 0, sz / 2), new Vector3(sx, height, t), glass);
            Cube(box, "Glass_S", c - new Vector3(0, 0, sz / 2), new Vector3(sx, height, t), glass);
            Cube(box, "Glass_E", c + new Vector3(sx / 2, 0, 0), new Vector3(t, height, sz), glass);
            Cube(box, "Glass_W", c - new Vector3(sx / 2, 0, 0), new Vector3(t, height, sz), glass);
            foreach (var (dx, dz) in new[] { (1, 1), (1, -1), (-1, 1), (-1, -1) })
                Cube(box, "Post", c + new Vector3(dx * sx / 2, 0, dz * sz / 2), new Vector3(bar, height, bar), frame);
            float top = bottom + height;
            Cube(box, "Rim_N", new Vector3(c.x, top, c.z + sz / 2), new Vector3(sx + bar, bar, bar), frame);
            Cube(box, "Rim_S", new Vector3(c.x, top, c.z - sz / 2), new Vector3(sx + bar, bar, bar), frame);
            Cube(box, "Rim_E", new Vector3(c.x + sx / 2, top, c.z), new Vector3(bar, bar, sz + bar), frame);
            Cube(box, "Rim_W", new Vector3(c.x - sx / 2, top, c.z), new Vector3(bar, bar, sz + bar), frame);
        }

        static void Cube(Transform parent, string name, Vector3 pos, Vector3 size, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent);
            go.transform.position = pos;
            go.transform.localScale = size;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            if (mat.name == "Glass") r.shadowCastingMode = ShadowCastingMode.Off;
        }

        static Material GetOrCreateMaterial(string name, System.Action<Material> setup)
        {
            System.IO.Directory.CreateDirectory(MaterialDir);
            var path = $"{MaterialDir}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
                AssetDatabase.CreateAsset(mat, path);
            }
            setup(mat);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static void BuildLightingAndCamera(Bounds island)
        {
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.95f, 0.86f);
            sun.intensity = 1.3f;
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(48, -35, 0);

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.78f, 0.84f, 0.92f);
            RenderSettings.ambientEquatorColor = new Color(0.66f, 0.66f, 0.62f);
            RenderSettings.ambientGroundColor = new Color(0.36f, 0.32f, 0.28f);

            // Narrow FOV from far away reads as a miniature; portrait phone framing.
            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.93f, 0.89f, 0.83f);
            cam.fieldOfView = 22f;
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 200f;
            var camData = camGo.AddComponent<UniversalAdditionalCameraData>();
            camData.renderPostProcessing = true;
            // Fit the whole box horizontally on a 9:16 portrait screen.
            var target = island.center;
            const float portraitAspect = 9f / 16f;
            float halfHorizontal = Mathf.Atan(Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * portraitAspect);
            float dist = island.extents.magnitude / Mathf.Tan(halfHorizontal) * 1.05f;
            camGo.transform.position = target + Quaternion.Euler(38, 35, 0) * Vector3.back * dist;
            camGo.transform.LookAt(target);

            // Tilt-shift-ish depth of field + gentle grading.
            var volGo = new GameObject("PostFX");
            var vol = volGo.AddComponent<Volume>();
            vol.isGlobal = true;
            var profilePath = "Assets/Art/LookTestProfile.asset";
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(profilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, profilePath);
            }
            foreach (var comp in profile.components.ToList()) { profile.Remove(comp.GetType()); Object.DestroyImmediate(comp, true); }

            var dof = profile.Add<DepthOfField>(true);
            dof.mode.Override(DepthOfFieldMode.Bokeh);
            dof.focusDistance.Override(dist);
            dof.focalLength.Override(120f);
            dof.aperture.Override(4f);
            var bloom = profile.Add<Bloom>(true);
            bloom.intensity.Override(0.25f);
            bloom.threshold.Override(1.1f);
            var vignette = profile.Add<Vignette>(true);
            vignette.intensity.Override(0.22f);
            var color = profile.Add<ColorAdjustments>(true);
            color.saturation.Override(-12f);
            color.contrast.Override(6f);
            color.colorFilter.Override(new Color(1f, 0.97f, 0.93f));
            foreach (var comp in profile.components) AssetDatabase.AddObjectToAsset(comp, profile);
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            vol.sharedProfile = profile;
        }
    }
}
