using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace AquariumCiv.EditorTools
{
    /// <summary>
    /// Agent helper: set the Game view to a fixed portrait resolution (docs/tooling.md, 2026-10-29 tester notes), and grab
    /// a play-mode screenshot that includes IMGUI. Call from `unity command script eval`.
    /// </summary>
    public static class AgentGameView
    {
        public static string SetSize(int w, int h)
        {
            var asm = typeof(EditorWindow).Assembly;
            var sizesType = asm.GetType("UnityEditor.GameViewSizes");
            var singleton = typeof(ScriptableSingleton<>).MakeGenericType(sizesType).GetProperty("instance").GetValue(null);
            var group = sizesType.GetMethod("GetGroup").Invoke(singleton, new object[] { (int)GameViewSizeGroupType.Standalone });
            var groupType = group.GetType();
            int total = (int)groupType.GetMethod("GetTotalCount").Invoke(group, null);
            string name = $"Tester {w}x{h}";
            int index = -1;
            for (int i = 0; i < total; i++)
            {
                var size = groupType.GetMethod("GetGameViewSize").Invoke(group, new object[] { i });
                var st = size.GetType();
                if ((int)st.GetProperty("width").GetValue(size) == w && (int)st.GetProperty("height").GetValue(size) == h &&
                    st.GetProperty("sizeType").GetValue(size).ToString() == "FixedResolution") { index = i; break; }
            }
            if (index < 0)
            {
                var gvsType = asm.GetType("UnityEditor.GameViewSize");
                var sizeTypeEnum = asm.GetType("UnityEditor.GameViewSizeType");
                var ctor = gvsType.GetConstructor(new[] { sizeTypeEnum, typeof(int), typeof(int), typeof(string) });
                var gvs = ctor.Invoke(new[] { Enum.Parse(sizeTypeEnum, "FixedResolution"), w, h, name });
                groupType.GetMethod("AddCustomSize").Invoke(group, new[] { gvs });
                index = (int)groupType.GetMethod("GetTotalCount").Invoke(group, null) - 1;
            }
            var gvType = asm.GetType("UnityEditor.GameView");
            var gv = EditorWindow.GetWindow(gvType);
            gvType.GetProperty("selectedSizeIndex", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).SetValue(gv, index);
            return $"game view {w}x{h} (index {index})";
        }

        /// <summary>Play mode only: writes at the end of the frame (check the file with a second call).</summary>
        public static string Shot(string name)
        {
            var path = $"F:/GameDev/AquariumCiv/Temp/agent/{name}.png";
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));
            if (System.IO.File.Exists(path)) System.IO.File.Delete(path);
            ScreenCapture.CaptureScreenshot(path);
            return path;
        }
    }
}
