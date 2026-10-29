using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace AquariumCiv.EditorTools
{
    /// <summary>The KayKit hex island in a glass box, shared by the look test and every prototype scene.</summary>
    public class Diorama
    {
        public Transform root;
        public Vector3 tileSize;
        public float surfaceY; // world height of the tiles' top face (KayKit tiles pivot at the top, not the bottom)
        public float w; // flat-to-flat tile width
        public List<Vector2Int> cells = new();
        public Dictionary<Vector2Int, Vector3> cellPos = new();
        public HashSet<Vector2Int> water = new();
        public float homeHeight = 1f;
        public Bounds boxBounds;
        public Renderer[] glass;
        public Camera camera;
        public Vector3 cameraTarget;

        public Vector3 Top(Vector2Int cell) => cellPos[cell] + Vector3.up * surfaceY;
    }

    public static class DioramaBuilder
    {
        public const string KayKit = "Assets/ThirdParty/KayKit";
        public const string Medieval = KayKit + "/MedievalHexagon";
        const string MaterialDir = "Assets/Art/Materials";

        public static string B(string n) => $"{Medieval}/buildings/green/building_{n}_green.fbx";
        public static string N(string n) => $"{Medieval}/decoration/nature/{n}.fbx";
        public static string P(string n) => $"{Medieval}/decoration/props/{n}.fbx";
        public static string Neutral(string n) => $"{Medieval}/buildings/neutral/{n}.fbx";
        public static string Character(string n) => $"{KayKit}/Adventurers/{n}.fbx";
        public static readonly string[] CharacterNames = { "Barbarian", "Knight", "Mage", "Rogue", "RogueHooded" };

        /// <summary>Hex grid of grass (and water) tiles under a new "Terrarium" root.</summary>
        public static Diorama BuildGround(int radius, IEnumerable<Vector2Int> waterCells)
        {
            var d = new Diorama { root = new GameObject("Terrarium").transform };
            d.water.UnionWith(waterCells);
            var ground = new GameObject("Ground").transform;
            ground.SetParent(d.root);

            // Measure the tile so spacing works whatever its orientation.
            var grassTile = Load($"{Medieval}/tiles/base/hex_grass.fbx");
            var waterTile = Load($"{Medieval}/tiles/base/hex_water.fbx");
            var tileBounds = MeasureBounds(grassTile);
            d.tileSize = tileBounds.size;
            d.surfaceY = tileBounds.max.y;
            bool pointyTop = d.tileSize.z > d.tileSize.x;
            d.w = pointyTop ? d.tileSize.x : d.tileSize.z;
            d.homeHeight = MeasureBounds(Load(B("home_A"))).size.y;

            for (int q = -radius; q <= radius; q++)
            for (int r = Mathf.Max(-radius, -q - radius); r <= Mathf.Min(radius, -q + radius); r++)
            {
                var c = new Vector2Int(q, r);
                d.cells.Add(c);
                // Axial -> world. Spacing derived from the flat-to-flat width w.
                Vector3 p = pointyTop
                    ? new Vector3(d.w * (c.x + c.y * 0.5f), 0, d.w * 0.8660254f * c.y)
                    : new Vector3(d.w * 0.8660254f * c.x, 0, d.w * (c.y + c.x * 0.5f));
                d.cellPos[c] = p;
                Place(d.water.Contains(c) ? waterTile : grassTile, ground, p, 0, 1);
            }
            return d;
        }

        public static Transform PlaceProps(Diorama d, string group, IEnumerable<(Vector2Int cell, string path, float yaw)> props)
        {
            var parent = new GameObject(group).transform;
            parent.SetParent(d.root);
            foreach (var (cell, path, yaw) in props)
            {
                var prefab = Load(path);
                if (prefab == null || !d.cellPos.ContainsKey(cell)) continue;
                Place(prefab, parent, d.Top(cell), yaw, 1);
            }
            return parent;
        }

        public static GameObject Load(string path)
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (go == null) Debug.LogWarning($"[Diorama] Missing asset {path}");
            return go;
        }

        public static GameObject Place(GameObject prefab, Transform parent, Vector3 pos, float yaw, float scale)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0, yaw, 0));
            go.transform.localScale = Vector3.one * scale;
            return go;
        }

        public static Bounds MeasureBounds(GameObject prefab)
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

        public static AnimationClip Clip(string fbxPath, string name) =>
            AssetDatabase.LoadAllAssetsAtPath(fbxPath).OfType<AnimationClip>().FirstOrDefault(c => c.name == name);

        /// <summary>Glass box sized around everything currently under the root.</summary>
        public static void BuildTerrarium(Diorama d)
        {
            var island = new Bounds(Vector3.zero, Vector3.zero);
            foreach (var r in d.root.GetComponentsInChildren<Renderer>()) island.Encapsulate(r.bounds);

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
            box.SetParent(d.root);
            float pad = 0.25f;
            float sx = island.size.x + pad * 2, sz = island.size.z + pad * 2;
            float bottom = island.min.y - 0.05f;
            float height = Mathf.Max(island.size.y, d.homeHeight * 2.5f) + 0.6f;
            var c = new Vector3(island.center.x, bottom + height / 2, island.center.z);
            const float t = 0.03f, bar = 0.07f;

            var panes = new List<Renderer>();
            Cube(box, "Plinth", new Vector3(c.x, bottom - 0.2f, c.z), new Vector3(sx + 0.3f, 0.4f, sz + 0.3f), plinth);
            panes.Add(Cube(box, "Glass_N", c + new Vector3(0, 0, sz / 2), new Vector3(sx, height, t), glass));
            panes.Add(Cube(box, "Glass_S", c - new Vector3(0, 0, sz / 2), new Vector3(sx, height, t), glass));
            panes.Add(Cube(box, "Glass_E", c + new Vector3(sx / 2, 0, 0), new Vector3(t, height, sz), glass));
            panes.Add(Cube(box, "Glass_W", c - new Vector3(sx / 2, 0, 0), new Vector3(t, height, sz), glass));
            foreach (var (dx, dz) in new[] { (1, 1), (1, -1), (-1, 1), (-1, -1) })
                Cube(box, "Post", c + new Vector3(dx * sx / 2, 0, dz * sz / 2), new Vector3(bar, height, bar), frame);
            float top = bottom + height;
            Cube(box, "Rim_N", new Vector3(c.x, top, c.z + sz / 2), new Vector3(sx + bar, bar, bar), frame);
            Cube(box, "Rim_S", new Vector3(c.x, top, c.z - sz / 2), new Vector3(sx + bar, bar, bar), frame);
            Cube(box, "Rim_E", new Vector3(c.x + sx / 2, top, c.z), new Vector3(bar, bar, sz + bar), frame);
            Cube(box, "Rim_W", new Vector3(c.x - sx / 2, top, c.z), new Vector3(bar, bar, sz + bar), frame);
            d.glass = panes.ToArray();

            bool firstBox = true;
            foreach (var r in box.GetComponentsInChildren<Renderer>())
            {
                if (firstBox) { d.boxBounds = r.bounds; firstBox = false; }
                else d.boxBounds.Encapsulate(r.bounds);
            }
        }

        static Renderer Cube(Transform parent, string name, Vector3 pos, Vector3 size, Material mat)
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
            return r;
        }

        public static Material GetOrCreateMaterial(string name, System.Action<Material> setup, string shader = "Universal Render Pipeline/Lit", string dir = MaterialDir)
        {
            System.IO.Directory.CreateDirectory(dir);
            var path = $"{dir}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find(shader)) { name = name };
                AssetDatabase.CreateAsset(mat, path);
            }
            setup(mat);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        /// <summary>Sun, trilight ambient, a narrow-FOV portrait camera framing the box, and tilt-shift-ish post FX.</summary>
        public static Camera BuildLightingAndCamera(Diorama d, string profilePath)
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
            var island = d.boxBounds;
            var target = island.center;
            const float portraitAspect = 9f / 16f;
            float halfHorizontal = Mathf.Atan(Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * portraitAspect);
            float dist = island.extents.magnitude / Mathf.Tan(halfHorizontal) * 1.05f;
            camGo.transform.position = target + Quaternion.Euler(38, 35, 0) * Vector3.back * dist;
            camGo.transform.LookAt(target);
            d.camera = cam;
            d.cameraTarget = target;

            // Tilt-shift-ish depth of field + gentle grading.
            var volGo = new GameObject("PostFX");
            var vol = volGo.AddComponent<Volume>();
            vol.isGlobal = true;
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(profilePath);
            if (profile == null)
            {
                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(profilePath));
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
            return cam;
        }
    }
}
