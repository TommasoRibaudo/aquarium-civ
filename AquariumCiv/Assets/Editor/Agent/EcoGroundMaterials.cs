using UnityEditor;
using UnityEngine;

namespace AquariumCiv.EditorTools
{
    /// <summary>
    /// Makes the four materials the living land's wet look uses (#266), as assets under <c>Game/View/Resources/EcoGround/</c> so the
    /// shader is in every build (EcoSurface loads them with Resources.Load). Run once: <c>AquariumCiv/Agent/Make EcoGround materials</c>.
    /// </summary>
    public static class EcoGroundMaterials
    {
        const string Dir = "Assets/Game/View/Resources/EcoGround";

        [MenuItem("AquariumCiv/Agent/Make EcoGround materials")]
        public static string Make()
        {
            var sh = Shader.Find("AquariumCiv/View/EcoGround");
            if (sh == null) return "shader AquariumCiv/View/EcoGround not found";
            if (!AssetDatabase.IsValidFolder(Dir))
            {
                if (!AssetDatabase.IsValidFolder("Assets/Game/View/Resources")) AssetDatabase.CreateFolder("Assets/Game/View", "Resources");
                AssetDatabase.CreateFolder("Assets/Game/View/Resources", "EcoGround");
            }
            Mk("EcoWet", sh, 0, 2, 0, 2940);      // multiply: DstColor Zero
            Mk("EcoSheen", sh, 1, 1, 1, 2944);    // add
            Mk("EcoPuddle", sh, 2, 5, 10, 2946);  // alpha
            Mk("EcoTint", sh, 3, 5, 10, 2942);
            Mk("EcoDrop", sh, 4, 5, 10, 3005);
            AssetDatabase.SaveAssets();
            return "made 5 materials in " + Dir;
        }

        static void Mk(string name, Shader sh, float mode, float src, float dst, int queue)
        {
            string path = Dir + "/" + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(sh); AssetDatabase.CreateAsset(m, path); }
            m.shader = sh;
            m.SetFloat("_Mode", mode); m.SetFloat("_SrcBlend", src); m.SetFloat("_DstBlend", dst);
            m.renderQueue = queue;
            EditorUtility.SetDirty(m);
        }
    }
}
