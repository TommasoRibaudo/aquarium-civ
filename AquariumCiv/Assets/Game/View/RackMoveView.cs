using System.Collections.Generic;
using AquariumCiv.Game;
using AquariumCiv.Life;
using AquariumCiv.Sim;
using UnityEngine;

namespace AquariumCiv.View
{
    /// <summary>
    /// #700 (B27): runs the rack move (<see cref="LifePlanner.ResolveRackMove"/> every frame) and draws it: while the beat plays the regular rack
    /// views stay away (<see cref="LifePlanner.RackMoveCovers"/>) and this draws the rack taken down, carried and planted (<see cref="RackMoveMesh"/>),
    /// with the puffs as the frames leave and meet the ground; after it, the four holes at the old spot show until the next dawn.
    /// One mesh per frame while a beat runs (a handful of boxes).
    /// </summary>
    [DefaultExecutionOrder(7)]
    public sealed class RackMoveView : MonoBehaviour
    {
        public static bool AutoCreate = true;
        public static RackMoveView Instance { get; private set; }

        BoxView box;
        PeopleView people;
        Transform holder;
        GameObject beat, holes;
        Mesh beatMesh, holesMesh;
        int holesKey;
        float lastSince = -1f;
        readonly List<(float x, float z)> holeList = new();
        public bool Drawing { get; private set; }
        public int HolesShown { get; private set; }
        public int Puffs { get; private set; }
        public RackPhase Phase { get; private set; } = RackPhase.Done;
        PeopleView People => people != null ? people : people = PeopleView.Find();

        public static RackMoveView Ensure(BoxView b)
        {
            if (b == null) return null;
            var v = b.GetComponent<RackMoveView>();
            return v != null ? v : b.gameObject.AddComponent<RackMoveView>();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (!AutoCreate || Instance != null || FindAnyObjectByType<GameClock>() == null) return;
            Ensure(FindAnyObjectByType<BoxView>());
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
            if (holder != null) Destroy(holder.gameObject);
            if (beatMesh != null) Destroy(beatMesh);
            if (holesMesh != null) Destroy(holesMesh);
            holder = null; beat = holes = null; beatMesh = holesMesh = null; holesKey = 0;
        }

        Transform Holder()
        {
            if (holder == null)
            {
                holder = new GameObject("Rack move") { hideFlags = HideFlags.DontSave }.transform;
                holder.SetParent(box.Root, false);
            }
            return holder;
        }

        void Update()
        {
            var sim = GameClock.Instance != null ? GameClock.Instance.Sim : null;
            var pv = People;
            Drawing = false; HolesShown = 0;
            if (sim == null || !sim.EcologyOn || box == null || box.Root == null || pv == null || pv.Planner == null || ReplayGate.Active) { Off(); return; }
            if (holder != null && holder.parent != box.Root) { Destroy(holder.gameObject); holder = null; beat = holes = null; holesKey = 0; }
            var planner = pv.Planner;
            planner.ResolveRackMove(pv.Now);
            if (planner.TryGetRackMove(pv.Now, out var plan, out float since) && since < plan.total)
            {
                DrawBeat(plan, since);
                Drawing = true; Phase = plan.PhaseAt(since);
                FirePuffs(plan, since);
                lastSince = since;
            }
            else { if (beat != null) beat.SetActive(false); lastSince = -1f; Phase = RackPhase.Done; }
            DrawHoles(planner, pv.Now);
        }

        void Off() { if (beat != null) beat.SetActive(false); if (holes != null) holes.SetActive(false); lastSince = -1f; }

        void DrawBeat(RackMovePlan plan, float since)
        {
            var look = plan.LookAt(since, People != null ? People.DollZoom : 1f);
            var mb = new MeshBuilder(box.seed + 700) { jitter = 0f };
            RackMoveMesh.Build(mb, plan, look, (x, z) => box.land.Height(x, z), box.seed + 284);
            if (beatMesh != null) Destroy(beatMesh);
            beatMesh = mb.ToMesh("RackMove"); beatMesh.hideFlags = HideFlags.DontSave;
            if (beat == null)
            {
                beat = new GameObject("RackMoveBeat") { hideFlags = HideFlags.DontSave };
                beat.transform.SetParent(Holder(), false);
                beat.AddComponent<MeshFilter>();
                var r = beat.AddComponent<MeshRenderer>();
                r.sharedMaterial = box.SolidMaterial;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            }
            beat.GetComponent<MeshFilter>().sharedMesh = beatMesh;
            beat.SetActive(true);
        }

        void FirePuffs(RackMovePlan plan, float since)
        {
            if (lastSince < 0f) lastSince = since - 0.0001f; // a beat caught mid-way fires only what comes next
            foreach (var e in plan.Events())
            {
                if (e.t <= lastSince || e.t > since) continue;
                var at = box.transform.TransformPoint(box.land.OnGround(e.x, e.z));
                if (e.dust) { HitFx.Puff(PuffKind.Dust, at, Vector3.up, 1f); Puffs++; }
                if (e.grass) { HitFx.Puff(PuffKind.Grass, at, Vector3.up, 1f); Puffs++; }
            }
        }

        void DrawHoles(LifePlanner planner, ViewTime now)
        {
            if (!planner.RackHoles(now, holeList)) { if (holes != null) holes.SetActive(false); return; }
            int k = 17;
            foreach (var (x, z) in holeList) { k = k * 31 + Mathf.RoundToInt(x * 500f); k = k * 31 + Mathf.RoundToInt(z * 500f); }
            if (holes == null || k != holesKey)
            {
                var mb = new MeshBuilder(box.seed + 701) { jitter = 0f };
                RackMoveMesh.Holes(mb, holeList, (x, z) => box.land.Height(x, z));
                if (holesMesh != null) Destroy(holesMesh);
                holesMesh = mb.ToMesh("RackHoles"); holesMesh.hideFlags = HideFlags.DontSave;
                if (holes == null)
                {
                    holes = new GameObject("RackHoles") { hideFlags = HideFlags.DontSave };
                    holes.transform.SetParent(Holder(), false);
                    holes.AddComponent<MeshFilter>();
                    holes.AddComponent<MeshRenderer>().sharedMaterial = box.SolidMaterial;
                }
                holes.GetComponent<MeshFilter>().sharedMesh = holesMesh;
                holesKey = k;
            }
            holes.SetActive(true);
            HolesShown = holeList.Count;
        }
    }
}
