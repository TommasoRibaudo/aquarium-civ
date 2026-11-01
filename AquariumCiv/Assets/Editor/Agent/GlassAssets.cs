using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.TextCore.Text;
using Unity.VectorGraphics.Editor;

namespace AquariumCiv.EditorTools
{
    /// <summary>
    /// One-off import of the Keeper's Glass assets (#108): the SVG icons as UI Toolkit vector images, and an SDF font asset per
    /// TTF in Resources/GameUI/Fonts. Dynamic atlas, so a name the player types still draws; emptied on build.
    /// Run through <c>unity command script eval</c>: <c>AquariumCiv.EditorTools.GlassAssets.Run()</c>. Safe to run again.
    /// </summary>
    public static class GlassAssets
    {
        const string Root = "Assets/Game/UI/Resources/GameUI/";

        public static string Run()
        {
            AssetDatabase.Refresh();
            int icons = 0, fonts = 0;
            foreach (var path in Directory.GetFiles(Root + "Icons", "*.svg"))
            {
                var p = path.Replace(Path.DirectorySeparatorChar, '/');
                if (AssetImporter.GetAtPath(p) is not SVGImporter svg) continue;
                if (svg.SvgType != SVGType.VectorImage)
                {
                    svg.SvgType = SVGType.VectorImage;
                    svg.SaveAndReimport();
                }
                icons++;
            }
            foreach (var path in Directory.GetFiles(Root + "Fonts", "*.ttf"))
            {
                var p = path.Replace(Path.DirectorySeparatorChar, '/');
                var target = p.Replace(".ttf", ".asset");
                if (AssetDatabase.LoadAssetAtPath<FontAsset>(target) != null) continue;
                var font = AssetDatabase.LoadAssetAtPath<Font>(p);
                var asset = FontAsset.CreateFontAsset(font, 90, 18, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true);
                asset.name = Path.GetFileNameWithoutExtension(p);
                AssetDatabase.CreateAsset(asset, target);
                asset.material.name = asset.name + " Material";
                AssetDatabase.AddObjectToAsset(asset.material, asset);
                foreach (var tex in asset.atlasTextures)
                {
                    tex.name = asset.name + " Atlas";
                    AssetDatabase.AddObjectToAsset(tex, asset);
                }
                EditorUtility.SetDirty(asset);
                fonts++;
            }
            AssetDatabase.SaveAssets();
            return $"icons {icons}, new font assets {fonts}";
        }
    }
}
