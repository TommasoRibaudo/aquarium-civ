using UnityEngine;

namespace AquariumCiv.View
{
    /// <summary>
    /// The box's night level for lamps (decisions.md "Life in the box": lamps light up nicely, flickering as they come on
    /// and now and then). 0 = day (windows show their soft daytime glow), 1 = night (every lamp on). Lamps cross on one
    /// by one as the level rises through the dusk; the shaders do the flicker. The day/night cycle (#30) drives this;
    /// until then set it from the inspector (<see cref="NightLampDriver"/>) or with <see cref="DebugSetLevel"/>.
    /// </summary>
    public static class NightLamps
    {
        static readonly int LevelId = Shader.PropertyToID("_AC_LampLevel");
        public static float Level { get; private set; }

        public static void SetLevel(float level)
        {
            Level = Mathf.Clamp01(level);
            Shader.SetGlobalFloat(LevelId, Level);
        }

        /// <summary>Debug hook (for `unity command script eval` and the debug panel when it arrives).</summary>
        public static string DebugSetLevel(float level)
        {
            var driver = Object.FindAnyObjectByType<NightLampDriver>();
            if (driver != null) driver.level = Mathf.Clamp01(level);
            SetLevel(level);
            return $"lamp level {Level:0.00}";
        }
    }
}
