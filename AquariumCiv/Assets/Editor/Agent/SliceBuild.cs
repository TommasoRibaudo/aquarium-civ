using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace AquariumCiv.EditorTools
{
    /// <summary>
    /// The slice build (#98): Build Settings hold only <c>Assets/Game/Scenes/Slice.unity</c>, the prototype scenes stay in the
    /// list but disabled (so their GUIDs and the Perf/prototype helpers keep working), version 0.4.0. Build methods for
    /// Android (apk) and iOS (Xcode project, the only iOS output a Windows machine can make) write under
    /// <see cref="OutputRoot"/> (outside the repo, off C:). CI's GameCI step builds the enabled scenes of the Build Settings,
    /// so it follows this list without a scene argument.
    /// CLI: <c>unity command script eval --code "AquariumCiv.EditorTools.SliceBuild.Android()"</c>, or the menu items.
    /// </summary>
    public static class SliceBuild
    {
        public const string SliceScene = "Assets/Game/Scenes/Slice.unity";
        public const string TestDefine = "SLICE_TEST"; // FeedbackUI.InTestBuild is true in builds with it
        public const string Version = "0.4.0";
        public const string OutputRoot = @"F:\GameDevBuilds";

        [MenuItem("AquariumCiv/Agent/Slice/Build Settings: slice build")]
        public static string UseSliceBuildSettings()
        {
            var others = EditorBuildSettings.scenes.Select(s => s.path).Where(p => p != SliceScene).Distinct();
            var list = new System.Collections.Generic.List<EditorBuildSettingsScene> { new(SliceScene, true) };
            list.AddRange(others.Select(p => new EditorBuildSettingsScene(p, false)));
            EditorBuildSettings.scenes = list.ToArray();
            PlayerSettings.bundleVersion = Version;
            PlayerSettings.enableFrameTimingStats = true;
            foreach (var t in new[] { UnityEditor.Build.NamedBuildTarget.iOS, UnityEditor.Build.NamedBuildTarget.Android })
            {
                PlayerSettings.GetScriptingDefineSymbols(t, out string[] defs);
                if (!defs.Contains(TestDefine)) PlayerSettings.SetScriptingDefineSymbols(t, defs.Append(TestDefine).ToArray());
            }
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            AssetDatabase.SaveAssets();
            return Describe();
        }

        public static string Describe() =>
            "scenes: " + string.Join(", ", EditorBuildSettings.scenes.Select(s => (s.enabled ? "+" : "-") + Path.GetFileNameWithoutExtension(s.path))) +
            $" | version {PlayerSettings.bundleVersion}";

        static string[] EnabledScenes() => EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();

        static string Build(BuildTarget target, BuildTargetGroup group, string path, BuildOptions options)
        {
            var scenes = EnabledScenes();
            if (scenes.Length != 1 || scenes[0] != SliceScene) return "ERROR: Build Settings must hold only the Slice scene, run UseSliceBuildSettings. Now: " + Describe();
            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? OutputRoot);
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = scenes, locationPathName = path, target = target, targetGroup = group, options = options });
            var s = report.summary;
            return $"{s.result} {target} -> {path} | size {s.totalSize / (1024.0 * 1024.0):0.0} MB | {s.totalTime.TotalMinutes:0.0} min | errors {s.totalErrors}, warnings {s.totalWarnings}";
        }

        [MenuItem("AquariumCiv/Agent/Slice/Build Android (apk)")]
        public static string Android()
        {
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android, BuildTarget.Android)) return "Android build support is not installed";
            return Build(BuildTarget.Android, BuildTargetGroup.Android, Path.Combine(OutputRoot, "android", "AquariumCiv-" + Version + ".apk"), BuildOptions.None);
        }

        [MenuItem("AquariumCiv/Agent/Slice/Build iOS (Xcode project)")]
        public static string Ios()
        {
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.iOS, BuildTarget.iOS)) return "iOS build support is not installed";
            return Build(BuildTarget.iOS, BuildTargetGroup.iOS, Path.Combine(OutputRoot, "ios", "AquariumCiv"), BuildOptions.None);
        }
    }
}
