using UnityEngine;

namespace AquariumCiv.View
{
    /// <summary>Holds the scene's lamp level and pushes it to the shaders (edit and play mode). Scrub it to watch the
    /// lamps come on.</summary>
    [ExecuteAlways]
    public class NightLampDriver : MonoBehaviour
    {
        [Range(0, 1)] public float level;

        void OnEnable() => NightLamps.SetLevel(level);
        void OnDisable() { if (!DayLightDriver.Driving) NightLamps.SetLevel(0f); }
        void Update() { if (!DayLightDriver.Driving && !Mathf.Approximately(level, NightLamps.Level)) NightLamps.SetLevel(level); } // in play the day drives it (#113)
    }
}
