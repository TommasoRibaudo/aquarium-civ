using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace AquariumCiv.GameUI
{
    /// <summary>The device and build facts attached to every feedback note (no personal data: nothing that names the owner).</summary>
    public static class DeviceInfo
    {
        public static List<(string key, string value)> Rows(string buildInfo)
        {
            var ci = CultureInfo.InvariantCulture;
            return new List<(string, string)>
            {
                ("Build", $"{Application.version} · {buildInfo} · Unity {Application.unityVersion} · {Application.platform}{(Debug.isDebugBuild ? " (dev build)" : "")}"),
                ("Device", $"{SystemInfo.deviceModel} · {SystemInfo.operatingSystem}"),
                ("CPU", $"{SystemInfo.processorType} · {SystemInfo.processorCount} cores"),
                ("GPU", $"{SystemInfo.graphicsDeviceName} · {SystemInfo.graphicsDeviceType} {SystemInfo.graphicsDeviceVersion} · {SystemInfo.graphicsMemorySize} MB"),
                ("Memory", $"{SystemInfo.systemMemorySize} MB"),
                ("Screen", string.Format(ci, "{0}x{1} @ {2:0} dpi · safe area {3}", Screen.width, Screen.height, Screen.dpi, SafeArea())),
                ("Quality", string.Format(ci, "{0} · {1:0} fps now (target {2})", QualitySettings.names[QualitySettings.GetQualityLevel()], 1f / Mathf.Max(0.0001f, Time.smoothDeltaTime), Application.targetFrameRate)),
                ("Battery", SystemInfo.batteryLevel < 0 ? "n/a" : string.Format(ci, "{0:0}% ({1})", SystemInfo.batteryLevel * 100f, SystemInfo.batteryStatus)),
                ("Network", Application.internetReachability.ToString()),
                ("Language", Application.systemLanguage.ToString()),
            };
        }

        static string SafeArea()
        {
            var r = Screen.safeArea;
            return $"{r.x:0},{r.y:0} {r.width:0}x{r.height:0}";
        }
    }
}
