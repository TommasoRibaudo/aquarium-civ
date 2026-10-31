using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using static AquariumCiv.EditorTools.DioramaBuilder;

namespace AquariumCiv.EditorTools
{
    /// <summary>
    /// Builds Assets/Scenes/LookTest.unity: a small KayKit village inside a glass terrarium,
    /// framed like a phone diorama. Throwaway art-direction scene, safe to rebuild at any time.
    /// </summary>
    public static class LookTestBuilder
    {
        const string ScenePath = "Assets/Scenes/LookTest.unity";
        const int Radius = 3;

        [MenuItem("AquariumCiv/Agent/Build Look Test Scene")]
        public static string Build()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var rng = new System.Random(7);

            var waterCells = new[] { new Vector2Int(2, -1), new(3, -1), new(3, -2), new(2, -2) };
            var d = BuildGround(Radius, waterCells);

            // Village content, by cell.
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
                (new(0, 2), Neutral("building_grain"), 0),
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
            PlaceProps(d, "Village", layout);

            // Tiny citizens: sampled into an idle pose, scaled small against the houses.
            var people = new GameObject("Citizens").transform;
            people.SetParent(d.root);
            var charPaths = CharacterNames.Select(Character).ToArray();
            var spots = new[] { new Vector2(0.6f, 0.9f), new(-0.8f, 0.5f), new(1.4f, -0.6f), new(-0.3f, -1.2f),
                                new(2.1f, 0.8f), new(-1.6f, -0.4f), new(0.2f, 2.0f), new(-2.2f, 1.4f) };
            for (int i = 0; i < spots.Length; i++)
            {
                var prefab = Load(charPaths[i % charPaths.Length]);
                if (prefab == null) continue;
                float h = MeasureBounds(prefab).size.y;
                float scale = d.homeHeight * 0.5f / Mathf.Max(h, 0.01f);
                var pos = new Vector3(spots[i].x * d.w * 0.9f, d.surfaceY, spots[i].y * d.w * 0.9f);
                var go = Place(prefab, people, pos, rng.Next(0, 360), scale);
                PoseIdle(go, charPaths[i % charPaths.Length]);
            }

            BuildTerrarium(d);
            BuildLightingAndCamera(d, "Assets/Art/LookTestProfile.asset");

            System.IO.Directory.CreateDirectory("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);
            return $"Built {ScenePath}: {d.cells.Count} tiles, {layout.Length} props, {spots.Length} citizens, " +
                   $"tile {d.tileSize}, homeHeight={d.homeHeight:F2}";
        }

        static void PoseIdle(GameObject go, string fbxPath)
        {
            var clip = AssetDatabase.LoadAllAssetsAtPath(fbxPath).OfType<AnimationClip>()
                .Where(c => !c.name.StartsWith("__preview__"))
                .OrderByDescending(c => c.name.ToLower().Contains("idle"))
                .FirstOrDefault();
            if (clip != null) clip.SampleAnimation(go, 0.3f);
        }
    }
}
