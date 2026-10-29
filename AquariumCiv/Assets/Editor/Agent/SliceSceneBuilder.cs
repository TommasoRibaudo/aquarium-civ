using AquariumCiv.Game;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AquariumCiv.EditorTools
{
    /// <summary>
    /// Builds the vertical slice's scene (vertical-slice-plan §7, M1): a camera and the <see cref="GameClock"/>, no art yet
    /// (the style kit moves in at M2). Not added to Build Settings. Call from the menu or `unity command script eval`.
    /// </summary>
    public static class SliceSceneBuilder
    {
        public const string ScenePath = "Assets/Game/Scenes/Slice.unity";

        [MenuItem("AquariumCiv/Agent/Slice/Build Slice Scene")]
        public static string Build()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.9f, 0.86f, 0.8f);
            cam.fieldOfView = 22f;
            camGo.transform.SetPositionAndRotation(new Vector3(0f, 6f, -12f), Quaternion.Euler(25f, 0f, 0f));

            new GameObject("GameClock").AddComponent<GameClock>();

            System.IO.Directory.CreateDirectory("Assets/Game/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.Refresh();
            return "built " + ScenePath;
        }
    }
}
