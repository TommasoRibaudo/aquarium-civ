using System.Collections.Generic;
using AquariumCiv.Game;
using AquariumCiv.Life;
using AquariumCiv.Lighting;
using AquariumCiv.Sim;
using UnityEngine;
using UnityEngine.Rendering;

namespace AquariumCiv.View
{
    /// <summary>
    /// #573: chopping reads as wood. The chips are HitFx's (one burst per axe hit at the woods); this view adds the logs: every
    /// <c>chopHitsPerLog</c>th hit by a woodcutter lays one log on the ground beside them (<see cref="ChopLogTally"/> counts,
    /// at most <see cref="ChopLogTally.MaxPerChopper"/> by one chopper). When the chopper's chopping ends (the planner's Chop work then carries
    /// a Log to the stash: their doll now holds a Log and walks) the logs on the ground are lifted with the carry's own start, and the pile at the stash (<see cref="StashProps"/>) grows with the stock.
    /// A tree is never removed (#366 felling stays undecided): the logs come from it. Pooled, one mesh, one material.
    /// The log-carry itself is the planner's <c>LifePlanner.CarryLogs</c> work (reusable by travellers, #578).
    /// </summary>
    [DefaultExecutionOrder(8)]
    public sealed class ChopLogsView : MonoBehaviour
    {
        public static bool AutoCreate = true;
        public static ChopLogsView Instance { get; private set; }
        public const int PoolSize = 24;

        sealed class Lying { public Transform t; public int who; }
        readonly ChopLogTally tally = new();
        readonly List<Lying> lying = new();
        readonly List<Transform> pool = new();
        readonly Dictionary<int, PersonRef> lastHit = new();
        readonly List<int> gone = new();
        PeopleView people;
        BoxView box;
        Mesh logMesh;
        float findT, clock;

        /// <summary>For tests and the debug panel: logs on the ground now, and hits seen.</summary>
        public int LogsOnGround => lying.Count;
        public int HitsSeen { get; private set; }

        public static ChopLogsView Ensure()
        {
            if (Instance != null) return Instance;
            var b = FindAnyObjectByType<BoxView>();
            return b != null ? (b.GetComponent<ChopLogsView>() ?? b.gameObject.AddComponent<ChopLogsView>()) : null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (AutoCreate && Instance == null && FindAnyObjectByType<GameClock>() != null) Ensure();
        }

        void OnEnable()
        {
            if (Instance != null && Instance != this) { enabled = false; return; }
            Instance = this;
            box = GetComponent<BoxView>();
        }

        void OnDisable()
        {
            if (Instance != this) return;
            Instance = null;
            if (people != null) people.WorkHit -= OnWorkHit;
            people = null;
            foreach (var t in pool) if (t != null) Destroy(t.gameObject);
            pool.Clear(); lying.Clear(); lastHit.Clear(); tally.Clear();
            if (logMesh != null) Destroy(logMesh);
        }

        void Update()
        {
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.1f);
            clock += dt;
            findT -= dt;
            if (findT <= 0f)
            {
                findT = 0.5f;
                var pv = PeopleView.Find();
                if (pv != people)
                {
                    if (people != null) people.WorkHit -= OnWorkHit;
                    people = pv;
                    if (people != null) people.WorkHit += OnWorkHit;
                }
            }
            // The choppers whose chopping is over: the carry has begun (the log is in their hands), so the logs on the ground go with them.
            gone.Clear();
            foreach (var kv in lastHit)
            {
                bool chopping = people != null && people.TryGetActivityOf(kv.Value, out var act) && act.kind == ActivityKind.Chop && !act.moving;
                if (!chopping) gone.Add(kv.Key);
            }
            foreach (int who in gone)
            {
                lastHit.Remove(who); tally.Leave(who);
                for (int i = lying.Count - 1; i >= 0; i--)
                    if (lying[i].who == who) { lying[i].t.gameObject.SetActive(false); lying.RemoveAt(i); }
            }
        }

        void OnWorkHit(PersonRef who, Place place, DollStyle style, Vector3 at)
        {
            if (style != DollStyle.Chop || (place.kind != PlaceKind.Woods) || box == null || box.Root == null) return;
            HitsSeen++;
            int key = who.Key;
            lastHit[key] = who;
            var sim = GameClock.Instance != null ? GameClock.Instance.Sim : null;
            int perLog = sim != null && sim.Config.eco != null ? sim.Config.eco.chopHitsPerLog : 4;
            if (!tally.Hit(key, perLog)) return;
            Lay(key, at);
        }

        void Lay(int who, Vector3 at)
        {
            var t = Take();
            var local = box.Root.InverseTransformPoint(at);
            int n = tally.Logs(who);
            // beside the chopper's stroke, fanned out a little so the logs do not stack into one
            float a = (who * 37 + n * 83) % 360 * Mathf.Deg2Rad;
            float lx = local.x + Mathf.Cos(a) * 0.08f, lz = local.z + Mathf.Sin(a) * 0.08f;
            t.SetParent(box.Root, false);
            t.SetLocalPositionAndRotation(new Vector3(lx, box.SurfaceHeight(lx, lz) + StashProps.LogRadius, lz), Quaternion.Euler(0f, (who * 53 + n * 71) % 180, 0f));
            t.gameObject.SetActive(true);
            lying.Add(new Lying { t = t, who = who });
        }

        Transform Take()
        {
            foreach (var p in pool) if (!p.gameObject.activeSelf) return p;
            if (pool.Count >= PoolSize)
            {   // the oldest log goes
                var o = lying[0]; lying.RemoveAt(0);
                return o.t;
            }
            if (logMesh == null)
            {
                var mb = new MeshBuilder("ChopLog".GetHashCode()) { jitter = 0.02f };
                Pieces.Log(mb, Vector3.zero, StashProps.LogRadius, StashProps.LogLength, 90f, Palette.Bark);
                logMesh = mb.ToMesh("ChopLog");
                logMesh.hideFlags = HideFlags.DontSave;
            }
            var go = new GameObject("ChopLog" + pool.Count) { hideFlags = HideFlags.DontSave };
            go.AddComponent<MeshFilter>().sharedMesh = logMesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = box.SolidMaterial;
            r.shadowCastingMode = ShadowCastingMode.On;
            pool.Add(go.transform);
            return go.transform;
        }
    }
}
