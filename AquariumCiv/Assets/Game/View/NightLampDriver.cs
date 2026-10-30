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
        void OnDisable() => NightLamps.SetLevel(0f);
        void Update() { if (!Mathf.Approximately(level, NightLamps.Level)) NightLamps.SetLevel(level); }
    }
}
