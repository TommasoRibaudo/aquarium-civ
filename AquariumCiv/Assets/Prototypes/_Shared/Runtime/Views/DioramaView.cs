using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace AquariumCiv.Prototypes
{
    /// <summary>Shows sim buildings on the diorama's free hex cells. Slots are filled center-out by the scene builder.</summary>
    public class DioramaView : MonoBehaviour
    {
        [Serializable]
        public class BuildingPrefabs
        {
            public string type;
            public GameObject[] prefabs;
        }

        public Transform buildingRoot;
        public Vector3[] slotPositions;
        public float[] slotYaw;
        [Tooltip("Half the flat-to-flat width of a hex tile: how far from a slot's center things may stand.")]
        public float slotRadius = 0.5f;
        public BuildingPrefabs[] prefabs;

        readonly Dictionary<int, GameObject> spawned = new();
        int lastCount = -1, generation;

        public Vector3 SlotPosition(int slot) => slotPositions[Mathf.Clamp(slot, 0, slotPositions.Length - 1)];

        void Update()
        {
            var clock = SimClock.Instance;
            if (clock.Generation != generation)
            {
                generation = clock.Generation;
                foreach (var go in spawned.Values) Destroy(go);
                spawned.Clear();
                lastCount = -1;
            }
            var state = clock.Sim.State;
            if (state.buildings.Count == lastCount) return;
            bool firstSync = lastCount < 0;
            lastCount = state.buildings.Count;
            var alive = new HashSet<int>();
            foreach (var b in state.buildings)
            {
                alive.Add(b.id);
                if (spawned.ContainsKey(b.id)) continue;
                spawned[b.id] = Spawn(b.type, b.id, b.slot, animate: !firstSync);
            }
            foreach (var id in new List<int>(spawned.Keys))
                if (!alive.Contains(id)) { Destroy(spawned[id]); spawned.Remove(id); }
        }

        GameObject Spawn(string type, int id, int slot, bool animate)
        {
            var set = Array.Find(prefabs, p => p.type == type);
            if (set == null || set.prefabs.Length == 0) { Debug.LogWarning($"[DioramaView] no prefab for {type}"); return new GameObject(type); }
            var prefab = set.prefabs[id % set.prefabs.Length];
            var yaw = slotYaw != null && slot < slotYaw.Length ? slotYaw[slot] : 0f;
            var go = Instantiate(prefab, SlotPosition(slot), Quaternion.Euler(0, yaw, 0), buildingRoot);
            go.name = $"{type}_{id}";
            AddBoundsCollider(go);
            LogCategory.Tag(go, "building", type);
            if (animate) StartCoroutine(PopIn(go.transform));
            return go;
        }

        public static void AddBoundsCollider(GameObject go)
        {
            var renderers = go.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return;
            var b = renderers[0].bounds;
            foreach (var r in renderers) b.Encapsulate(r.bounds);
            var box = go.AddComponent<BoxCollider>();
            box.center = go.transform.InverseTransformPoint(b.center);
            var s = go.transform.lossyScale;
            box.size = new Vector3(b.size.x / s.x, b.size.y / s.y, b.size.z / s.z);
        }

        static IEnumerator PopIn(Transform t)
        {
            var target = t.localScale;
            for (float k = 0; k < 1f; k += Time.deltaTime * 2.5f)
            {
                float e = 1f + 2.2f * Mathf.Pow(k - 1f, 3) + 1.2f * Mathf.Pow(k - 1f, 2); // ease-out-back
                t.localScale = target * Mathf.Max(0.01f, e);
                yield return null;
            }
            t.localScale = target;
        }
    }
}
