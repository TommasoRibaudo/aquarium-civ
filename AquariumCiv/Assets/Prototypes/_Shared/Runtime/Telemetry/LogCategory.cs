using UnityEngine;

namespace AquariumCiv.Prototypes
{
    /// <summary>Tags an object for tap/look logging: building, citizen, notable, trash, vine, glass, nature...</summary>
    public class LogCategory : MonoBehaviour
    {
        public string category = "other";
        public string id;

        public static LogCategory Of(Component c) => c == null ? null : c.GetComponentInParent<LogCategory>();

        public static LogCategory Tag(GameObject go, string category, string id = null)
        {
            if (!go.TryGetComponent<LogCategory>(out var lc)) lc = go.AddComponent<LogCategory>();
            lc.category = category;
            lc.id = id;
            return lc;
        }
    }

    /// <summary>Anything that reacts to a tap routed by OrbitCamera.</summary>
    public interface ITappable
    {
        void OnTap(RaycastHit hit);
    }
}
