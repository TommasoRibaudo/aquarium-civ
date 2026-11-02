using UnityEngine;

namespace AquariumCiv.View
{
    /// <summary>
    /// The box's night level for lamps (decisions.md "Life in the box": lamps light up nicely, flickering as they come on
    /// and now and then). 0 = day (windows show their soft daytime glow), 1 = night (every lamp on). Lamps cross on one
    /// by one as the level rises through the dusk; the shaders do the flicker. In play <see cref="DayLightDriver"/> drives this from
    /// the view hour; in edit mode, and for look Today, it comes from the inspector (<see cref="NightLampDriver"/>) or <see cref="DebugSetLevel"/>.
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
            // While the day drives the lamps, Day and Night are hours.
            if (DayLightDriver.Driving) { PeopleView.DebugSetHour(level > 0.5f ? 22f : 12f); return $"lamp level {Level:0.00}"; }
            var driver = Object.FindAnyObjectByType<NightLampDriver>();
            if (driver != null) driver.level = Mathf.Clamp01(level);
            SetLevel(level);
            return $"lamp level {Level:0.00}";
        }
    }
}
