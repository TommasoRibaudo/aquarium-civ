using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine.TextCore.Text;

namespace AquariumCiv.EditorTools
{
    /// <summary>
    /// Keeps the dynamic UI fonts out of git's sight (#108). A dynamic font asset fills its atlas as text draws, and the Editor would save
    /// that into the .asset: 2 MB of hex, different on every run. So the guard never saves them during play, and empties them first
    /// otherwise. The committed files are always empty; a build fills its own at runtime.
    /// </summary>
    [InitializeOnLoad]
    public class GlassFontGuard : AssetModificationProcessor
    {
        const string Folder = "Assets/Game/UI/Resources/GameUI/Fonts/";

        static GlassFontGuard()
        {
            EditorApplication.playModeStateChanged += s =>
            {
                if (s == PlayModeStateChange.EnteredEditMode) Empty();
            };
        }

        static bool IsFont(string path) => path.StartsWith(Folder) && path.EndsWith(".asset");

        static string[] OnWillSaveAssets(string[] paths)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return paths.Where(p => !IsFont(p)).ToArray();
            foreach (var p in paths)
                if (IsFont(p)) EmptyOne(p);
            return paths;
        }

        /// <summary>Back to the committed state.</summary>
        public static void Empty()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:FontAsset", new[] { Folder.TrimEnd('/') }))
            {
                var p = AssetDatabase.GUIDToAssetPath(guid);
                EmptyOne(p);
                AssetDatabase.SaveAssetIfDirty(AssetDatabase.GUIDFromAssetPath(p));
            }
        }

        static void EmptyOne(string path)
        {
            var font = AssetDatabase.LoadAssetAtPath<FontAsset>(path);
            if (font != null) font.ClearFontAssetData(true);
        }
    }
}
