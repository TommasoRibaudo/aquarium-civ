using System.Collections.Generic;
using AquariumCiv.Game;
using AquariumCiv.Life;
using AquariumCiv.Sim;
using UnityEngine;

namespace AquariumCiv.View
{
    /// <summary>
    /// #202: the mills' sails turn with the wind (<see cref="WindDrift"/>): some spells they stand still, some they drift round, some they go. Only
    /// sails are wind-driven in this box: the watermill's wheel is the stream's, and the festival's bunting is built into the chunks and hangs still.
    /// Each mill's sails are a mesh of their own, one draw call a mill, turned about the hub; <see cref="BoxView"/> leaves them out of its chunks.
    /// Debug: <see cref="DebugNext"/> holds the wind still, gentle or brisk.
    /// </summary>
    [RequireComponent(typeof(BoxView))]
    public class WindView : MonoBehaviour
    {
        public static bool AutoCreate = true;
        public static WindView Instance { get; private set; }

        /// <summary>The debug panel's held wind, negative when it blows by itself.</summary>
        public static float Held = -1f;

        const float SyncSeconds = 0.5f;

        sealed class Mill { public GameObject go; public float x, z, yaw, baseAngle, grow; public Era era; public int seed; }

        BoxView box;
        Transform root;
        readonly List<Mill> mills = new();
        readonly List<PieceData> found = new();
        Mesh villageSails, townSails;
        float angle, nextSync;

        /// <summary>The wind now (0 still, 1 brisk), how far the sails have gone (degrees), and how many mills have sails (tests, the debug panel).</summary>
        public float Wind { get; private set; }
        public float Angle => angle;
        public int Mills => mills.Count;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (!AutoCreate || Instance != null || FindAnyObjectByType<GameClock>() == null) return;
            var b = FindAnyObjectByType<BoxView>();
            if (b != null && b.GetComponent<WindView>() == null) b.gameObject.AddComponent<WindView>();
        }

        void OnEnable()
        {
            if (Instance != null && Instance != this) { enabled = false; return; }
            Instance = this;
            box = GetComponent<BoxView>();
        }

        void OnDisable()
        {
            if (Instance == this) Instance = null;
            if (root != null) Destroy(root.gameObject);
            root = null;
            mills.Clear();
            if (villageSails != null) Destroy(villageSails);
            if (townSails != null) Destroy(townSails);
            villageSails = townSails = null;
        }

        void Update()
        {
            var sim = GameClock.Instance != null ? GameClock.Instance.ActiveSim : null;
            int seed = sim != null ? sim.State.seed : 1;
            Wind = Held >= 0f ? Held : WindDrift.At(seed, Time.unscaledTimeAsDouble);
            angle = Mathf.Repeat(angle + WindDrift.Degrees(Wind) * Time.unscaledDeltaTime, 360f);
            if (Time.unscaledTime >= nextSync) { nextSync = Time.unscaledTime + SyncSeconds; Sync(); }
            foreach (var m in mills)
                if (m.go != null) Pose(m);
        }

        void Pose(Mill m)
        {
            var yaw = Quaternion.Euler(0f, m.yaw, 0f);
            var hub = m.era == Era.Town ? Pieces.TowerHub : Pieces.WindmillHub;
            m.go.transform.SetLocalPositionAndRotation(box.land.OnGround(m.x, m.z) + yaw * (hub * m.grow), yaw * Quaternion.Euler(0f, 0f, m.baseAngle - angle));
            m.go.transform.localScale = Vector3.one * m.grow;
        }

        /// <summary>The mills in the layout now: they come and go with the box, the years and the time-lapse.</summary>
        void Sync()
        {
            if (box == null || box.layout == null) return;
            found.Clear();
            foreach (var p in box.layout.pieces)
                if (p.kind == PieceKind.Windmill && p.era != Era.Camp) found.Add(p);
            while (mills.Count > found.Count)
            {
                var last = mills[mills.Count - 1];
                if (last.go != null) Destroy(last.go);
                mills.RemoveAt(mills.Count - 1);
            }
            for (int i = 0; i < found.Count; i++)
            {
                var p = found[i];
                if (i == mills.Count) mills.Add(new Mill());
                var m = mills[i];
                bool same = m.go != null && m.era == p.era && m.seed == p.seed;
                m.x = p.x; m.z = p.z; m.yaw = p.yaw; m.era = p.era; m.seed = p.seed;
                m.grow = Mathf.Max(0.02f, p.grow);
                m.baseAngle = p.seed * 37f * 0.13f;
                if (same) continue;
                if (m.go != null) Destroy(m.go);
                Make(m);
                Pose(m);
            }
        }

        void Make(Mill m)
        {
            if (root == null)
            {
                root = new GameObject("Sails") { hideFlags = HideFlags.DontSave }.transform;
                root.SetParent(transform, false);
            }
            m.go = new GameObject("Mill sails") { hideFlags = HideFlags.DontSave };
            m.go.transform.SetParent(root, false);
            m.go.AddComponent<MeshFilter>().sharedMesh = SailsFor(m.era == Era.Town);
            m.go.AddComponent<MeshRenderer>().sharedMaterial = box.SolidMaterial;
        }

        Mesh SailsFor(bool town)
        {
            ref Mesh mesh = ref town ? ref townSails : ref villageSails;
            if (mesh != null) return mesh;
            var mb = new MeshBuilder(7) { jitter = 0f };
            if (town) Pieces.TowerSails(mb, Vector3.zero, 0f); else Pieces.WindmillSails(mb, Vector3.zero, 0f);
            mesh = mb.ToMesh(town ? "TowerSails" : "WindmillSails");
            mesh.hideFlags = HideFlags.DontSave;
            return mesh;
        }

        // ---------------- debug ----------------
        public static string Status() => Instance == null ? "wind: off" : $"wind: {Instance.Wind:0.00}{(Held >= 0f ? " (held)" : "")}, {Instance.Mills} mills, sails {WindDrift.Degrees(Instance.Wind):0} deg/s";

        /// <summary>Debug: round the winds: by itself, still, gentle, brisk.</summary>
        public static string DebugNext()
        {
            Held = Held < 0f ? 0f : Held < WindDrift.Gentle ? WindDrift.Gentle : Held < WindDrift.Brisk ? WindDrift.Brisk : -1f;
            return Status();
        }
    }
}
