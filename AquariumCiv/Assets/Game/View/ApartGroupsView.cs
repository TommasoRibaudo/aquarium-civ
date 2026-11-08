using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using AquariumCiv.Game;
using AquariumCiv.Life;
using AquariumCiv.Sim;
using UnityEngine;
using UnityEngine.Rendering;

namespace AquariumCiv.View
{
    /// <summary>
    /// The strangers who live apart in the box (#231 phase 2; the sim's <see cref="SimCore.ApartGroups"/>): each group drawn at its home spot
    /// with its camp (a woods camp: a fire, a lean-to and a woodpile; a shore hut: a hut, a fire, a fish rack and a boat on the shore) and its
    /// living people going through a small day (<see cref="ApartLife.Doing"/>: asleep in the shelter at night, at the fire in the morning and
    /// the evening, so their fire glows at dusk with people round it; fishing or chopping, wandering, weaving by day). They are not the
    /// settlement's: the planner and <see cref="PeopleView"/> never see them. The dead are gone; when nobody is left the fire is cold and the
    /// hut's window dark. The camp keeps clear of lots, paths and water (<see cref="ApartLife.FreeSpot"/>): if the settlement grows over the
    /// spot, it is drawn at the nearest free one. While a choice scene reacts for the same dilemma, its travellers stand for them.
    /// Everything hangs under the box and goes with it. Debug: <see cref="DebugStatus"/>.
    /// </summary>
    public partial class ApartGroupsView : MonoBehaviour
    {
        public static bool AutoCreate = true;
        public static ApartGroupsView Instance { get; private set; }

        /// <summary>Box units a second: a walk at the camp (as the choice scene's travellers).</summary>
        const float WalkSpeed = 0.2f;
        /// <summary>Real seconds between checks that the spot is still clear (lots and paths change slowly).</summary>
        const float SpotCheckSeconds = 5f;

        sealed class Member
        {
            public int id, index;
            public PegDollView view;
            public Vector2 pos;
            public float yaw, stride, phase;
            public ApartDoing doing = (ApartDoing)(-1);
            public bool hidden;
            // #404: a scripted moment (the boat, the lamp, the lookout, the build) has them: how high they stand, what they hold
            public bool scripted;
            public float lift;
            public PropKind prop;
        }

        sealed class Camp
        {
            public int groupId, dilemmaId;
            public string kind;
            public Transform root;
            public bool lit;
            public Vector2 spot, home, back, side, water, fire, shelter, work, fishAt, boatAt;
            public bool hasWater, nudged;
            public float nextCheck;
            public readonly List<Object> meshes = new();
            public readonly Dictionary<int, Member> members = new();
            // #404: the boat that goes out, and the lookout tower
            public Transform boat;
            public Vector2 boatOut;
            public float boatHomeYaw;
            public int towerStage = -1;
            public Vector2 towerAt;
            public float towerYaw;
            public bool lampOn;
            public readonly List<Object> towerMeshes = new();
        }

        BoxView box;
        Transform root;
        SimCore drawn;
        float clock;
        readonly Dictionary<int, Camp> camps = new();

        static SimCore Sim => GameClock.Instance != null ? GameClock.Instance.Sim : null;
        static BoxCamera Cam => Camera.main != null ? Camera.main.GetComponent<BoxCamera>() : FindAnyObjectByType<BoxCamera>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (!AutoCreate || Instance != null || FindAnyObjectByType<GameClock>() == null) return;
            var b = FindAnyObjectByType<BoxView>();
            if (b != null && b.GetComponent<ApartGroupsView>() == null) b.gameObject.AddComponent<ApartGroupsView>();
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
            ClearAll();
        }

        static void Kill(Object o) { if (o == null) return; if (Application.isPlaying) Destroy(o); else DestroyImmediate(o); }

        void ClearAll()
        {
            foreach (var c in camps.Values) KillCamp(c);
            camps.Clear();
            if (root != null) Kill(root.gameObject);
            root = null;
            drawn = null;
        }

        static void KillCamp(Camp c)
        {
            foreach (var m in c.members.Values) KillDoll(m.view);
            c.members.Clear();
            foreach (var o in c.meshes) Kill(o);
            c.meshes.Clear();
            foreach (var o in c.towerMeshes) Kill(o);
            c.towerMeshes.Clear();
            if (c.root != null) Kill(c.root.gameObject);
            c.root = null;
        }

        /// <summary>A doll and its own meshes (the body, the arms; props and straps are shared and stay).</summary>
        static void KillDoll(PegDollView v)
        {
            if (v == null) return;
            if (v.body != null) Kill(v.body.GetComponent<MeshFilter>()?.sharedMesh);
            if (v.armL != null) Kill(v.armL.GetComponent<MeshFilter>()?.sharedMesh);
            Kill(v.gameObject);
        }

        // ================================================================== every frame

        void LateUpdate()
        {
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.1f);
            clock += dt;
            var sim = Sim;
            if (box == null || sim == null || sim.State.apartGroups == null || sim.State.apartGroups.Count == 0)
            {
                if (camps.Count > 0 || root != null) ClearAll();
                return;
            }
            if (sim != drawn) { ClearAll(); drawn = sim; }
            if (root == null)
            {
                root = new GameObject("ApartGroups") { hideFlags = HideFlags.DontSave }.transform;
                root.SetParent(transform, false);
            }
            var pv = PeopleView.Find();
            float hour = pv != null ? pv.ViewHour : 12f;
            var cam = Cam;
            float k = AquariumCiv.Lighting.CameraReach.DollScale(cam != null ? cam.zoom : 1f, PeopleView.DollScaleAmount);
            var choice = ChoiceSceneView.Instance;
            int staged = choice != null && choice.Stage != null ? choice.Stage.dilemmaId : -1;
            foreach (var g in sim.ApartGroups)
            {
                if (!camps.TryGetValue(g.id, out var c)) camps[g.id] = c = new Camp { groupId = g.id, dilemmaId = g.dilemmaId, kind = g.kind, home = new Vector2(g.x, g.z) };
                if (c.root == null || clock >= c.nextCheck) Place(sim, c);
                bool alive = g.AnyAlive;
                if (c.root == null || alive != c.lit) Build(sim, c, alive);
                UpdateTower(sim, c, g, hour); // #404
                UpdateBoat(sim, c, hour);
                Drive(sim, c, g, hour, k, dt, staged == g.dilemmaId);
            }
            if (camps.Count > sim.ApartGroups.Count)
                foreach (var id in camps.Keys.Where(id => sim.ApartGroups.All(g => g.id != id)).ToList()) { KillCamp(camps[id]); camps.Remove(id); }
        }

        // ================================================================== where the camp stands

        /// <summary>The camp's spot (the home spot, or the nearest clear one) and the layout round it. Rebuilds the camp when it moved.</summary>
        void Place(SimCore sim, Camp c)
        {
            c.nextCheck = clock + SpotCheckSeconds;
            var map = sim.Settlement?.Map;
            var (x, z) = ApartLife.FreeSpot(map, c.home.x, c.home.y, out bool moved, shore: c.kind == ApartKind.ShoreHut);
            var spot = new Vector2(x, z);
            var land = box.land;
            // never in the water, also without a map
            for (int i = 0; i < 12 && land.IsWater(spot.x, spot.y); i++) spot = Vector2.MoveTowards(spot, Vector2.zero, 0.08f);
            if (c.root != null && (spot - c.spot).sqrMagnitude < 1e-6f) return;
            c.spot = spot;
            c.nudged = moved;
            var w = ApartLife.WaterWay(map, spot.x, spot.y);
            c.water = new Vector2(w.x, w.z);
            c.hasWater = c.water.sqrMagnitude > 0.5f;
            var green = map != null ? new Vector2(map.greenX, map.greenZ) : Vector2.zero;
            var away = spot - green;
            c.back = c.kind == ApartKind.ShoreHut && c.hasWater ? -c.water : away.sqrMagnitude > 1e-4f ? away.normalized : Vector2.up;
            c.side = new Vector2(-c.back.y, c.back.x);
            c.fire = spot;
            if (c.kind == ApartKind.ShoreHut)
            {
                c.shelter = spot + c.back * 0.3f;
                c.work = spot + c.side * 0.26f;                 // the fish rack
                if (c.hasWater)
                {
                    var p = spot;
                    for (int i = 0; i < 20 && !land.IsWater(p.x, p.y); i++) p += c.water * 0.04f;
                    c.fishAt = p - c.water * 0.06f;             // the last dry step
                    c.boatAt = p + c.water * 0.02f - c.side * 0.16f;
                }
                else { c.fishAt = spot + c.side * 0.3f; c.boatAt = c.fishAt; }
                c.boatOut = BoatOutFrom(c);
            }
            else
            {
                c.shelter = spot - c.side * 0.3f + c.back * 0.08f; // beside the fire, not back under the trees
                c.work = spot + c.side * 0.3f;                  // the woodpile
                c.fishAt = c.boatAt = c.work;
            }
            if (c.root != null) Build(sim, c, c.lit);
        }

        // ================================================================== the camp

        void Build(SimCore sim, Camp c, bool lit)
        {
            foreach (var o in c.meshes) Kill(o);
            c.meshes.Clear();
            if (c.root != null) Kill(c.root.gameObject); // the dolls are kept: they hang under the box's root, not the camp's
            c.lit = lit;
            c.boat = null;
            c.towerStage = -1; // #404: the tower and its lamp are rebuilt under the new root
            c.lampOn = false;
            c.root = new GameObject("ApartCamp" + c.groupId) { hideFlags = HideFlags.DontSave }.transform;
            c.root.SetParent(root, false);
            float yawBack = Mathf.Atan2(c.back.x, c.back.y) * Mathf.Rad2Deg;
            Part(c, "Fire", ApartProps.Fire(lit, c.groupId), c.fire, yawBack);
            if (c.kind == ApartKind.ShoreHut)
            {
                Part(c, "Hut", ApartProps.Hut(lit, c.groupId), c.shelter, yawBack);
                Part(c, "FishRack", ApartProps.FishRack(lit), c.work, yawBack + 90f);
                c.boatHomeYaw = Mathf.Atan2(c.side.x, c.side.y) * Mathf.Rad2Deg - 90f + 12f;
                if (c.hasWater) c.boat = Part(c, "Boat", ApartProps.Boat(), c.boatAt, c.boatHomeYaw).transform;
            }
            else
            {
                Part(c, "LeanTo", ApartProps.LeanTo(), c.shelter, yawBack);
                Part(c, "Woodpile", ApartProps.Woodpile(), c.work, yawBack + 90f);
            }
            if (lit) Glow(c);
        }

        GameObject Part(Camp c, string name, Mesh mesh, Vector2 at, float yaw)
        {
            c.meshes.Add(mesh);
            var go = new GameObject(name) { hideFlags = HideFlags.DontSave };
            go.transform.SetParent(c.root, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = box.SolidMaterial;
            mr.shadowCastingMode = ShadowCastingMode.On;
            go.transform.SetLocalPositionAndRotation(box.land.OnGround(at), Quaternion.Euler(0f, yaw, 0f));
            return go;
        }

        /// <summary>The fire's glow, as the settlement's (BoxView.Glows): a halo behind the flame and a warm pool on the ground, both
        /// stronger at night (the Glow shader reads the lamp level), and a few embers.</summary>
        void Glow(Camp c)
        {
            var verts = new List<Vector3>(); var cols = new List<Color>(); var corner = new List<Vector2>(); var data = new List<Vector2>(); var extra = new List<Vector2>(); var tris = new List<int>();
            var corners = new[] { new Vector2(-1, -1), new Vector2(-1, 1), new Vector2(1, 1), new Vector2(1, -1) };
            void Add(Vector3 p, float radius, float kind, Color col, float x = 0f, float y = 0f)
            {
                int o = verts.Count;
                foreach (var q in corners) { verts.Add(p); cols.Add(col); corner.Add(q); data.Add(new Vector2(radius, kind)); extra.Add(new Vector2(x, y)); }
                tris.AddRange(new[] { o, o + 1, o + 2, o, o + 2, o + 3 });
            }
            var look = FireLooks.Spec(FireLooks.Active);
            var foot = box.land.OnGround(c.fire);
            float phase = Pieces.Phase(c.groupId * 31 + 5);
            const float s = 0.9f; // a camp fire, a little smaller than the settlement's
            Add(foot + Vector3.up * 0.13f * s, look.haloRadius * s, 0f, Palette.FireHalo, phase, 0.13f * s);
            Add(foot + Vector3.up * 0.02f, look.poolRadius * s, 4f, Palette.FirePool, phase, look.poolBoost);
            int embers = Mathf.Min(look.embers, 3);
            for (int e = 0; e < embers; e++)
                Add(foot + Vector3.up * look.emberY * s, 0.012f, 3f, Palette.FireSpark, (e + Land.Hash3(c.groupId, e, 11)) / Mathf.Max(1, embers), 1.2f + 0.8f * Land.Hash3(c.groupId, e, 12));
            var m = new Mesh { name = "ApartGlow", hideFlags = HideFlags.DontSave };
            m.SetVertices(verts); m.SetColors(cols); m.SetUVs(0, corner); m.SetUVs(1, data); m.SetUVs(2, extra); m.SetTriangles(tris, 0);
            m.bounds = new Bounds(foot + Vector3.up * 0.3f, Vector3.one * 2f);
            c.meshes.Add(m);
            var go = new GameObject("Glow") { hideFlags = HideFlags.DontSave };
            go.transform.SetParent(c.root, false);
            go.AddComponent<MeshFilter>().sharedMesh = m;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = box.GlowMaterial;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
        }

        // ================================================================== the people

        void Drive(SimCore sim, Camp c, ApartGroup g, float hour, float k, float dt, bool sceneHasThem)
        {
            int n = g.members.Count;
            int alive = 0, rank = 0;
            foreach (var x in g.members) if (x.alive) alive++;
            for (int i = 0; i < n; i++)
            {
                var person = g.members[i];
                c.members.TryGetValue(person.id, out var m);
                if (!person.alive)
                {
                    if (m != null) { KillDoll(m.view); c.members.Remove(person.id); }
                    continue;
                }
                if (m == null) c.members[person.id] = m = MakeMember(sim, c, g, person, i);
                if (!sceneHasThem && Scripted(sim, c, rank, alive, hour, out var script)) StepScripted(c, m, script, k, dt); // #404
                else
                {
                    if (m.scripted) { m.scripted = false; m.lift = 0f; m.doing = (ApartDoing)(-1); } // back to their day: its prop again
                    Step(c, m, n, hour, k, dt, sceneHasThem);
                }
                rank++;
            }
        }

        Member MakeMember(SimCore sim, Camp c, ApartGroup g, Notable person, int index)
        {
            // The same look the choice scene gave them as travellers (ChoiceSceneView.Build), so the Keeper knows them again.
            var look = PegDoll.PickLook(9001 + g.dilemmaId * 7 + index, sim.State.era == Era.Camp, false, 0);
            look.accent = Palette.Accents[(index + 4) % Palette.Accents.Length];
            var v = PegDollView.Create(root, box.SolidMaterial, look, false, 4000 + g.dilemmaId * 5 + index);
            v.name = "Apart" + g.id + "_" + person.name;
            var m = new Member { id = person.id, index = index, view = v, phase = index * 0.37f };
            var seat = ApartLife.Seat((c.fire.x, c.fire.y), (c.back.x, c.back.y), index, g.members.Count);
            m.pos = new Vector2(seat.x, seat.z);
            return m;
        }

        /// <summary>Where this member should be for what they do now, and which way they face there.</summary>
        Vector2 Goal(Camp c, Member m, int count, float hour, out float faceYaw)
        {
            Vector2 at;
            Vector2 face;
            switch (m.doing)
            {
                case ApartDoing.Sleep:
                    at = c.shelter; face = -c.back; break;
                case ApartDoing.Fish:
                    at = c.fishAt + c.side * (m.index * 0.08f); face = c.hasWater ? c.water : c.side; break;
                case ApartDoing.Chop:
                    at = c.work - c.back * 0.14f; face = c.back; break;
                case ApartDoing.Weave:
                    at = c.shelter - c.back * 0.16f + c.side * 0.1f; face = -c.back; break;
                case ApartDoing.Wander:
                {
                    var w = ApartLife.WanderPoint((c.fire.x, c.fire.y), m.index, hour, c.groupId * 13 + m.id);
                    at = new Vector2(w.x, w.z);
                    face = at - m.pos;
                    if (face.sqrMagnitude < 1e-4f) face = c.fire - at;
                    break;
                }
                default:
                {
                    var seat = ApartLife.Seat((c.fire.x, c.fire.y), (c.back.x, c.back.y), m.index, count);
                    at = new Vector2(seat.x, seat.z);
                    face = c.fire - at;
                    break;
                }
            }
            faceYaw = Mathf.Atan2(face.x, face.y) * Mathf.Rad2Deg + 180f; // a doll's front is its -z
            return at;
        }

        void Step(Camp c, Member m, int count, float hour, float k, float dt, bool sceneHasThem)
        {
            var v = m.view;
            if (sceneHasThem) { v.gameObject.SetActive(false); return; } // the choice scene's travellers are them, for now
            var doing = ApartLife.Doing(c.kind, m.index, hour);
            if (doing != m.doing)
            {
                m.doing = doing;
                if (doing == ApartDoing.Fish) v.SetProp(PropKind.Rod, PropSlot.Hand);
                else if (doing == ApartDoing.Chop) v.SetProp(PropKind.Axe, PropSlot.Hand);
                else v.SetProp(PropKind.None, PropSlot.Hand);
            }
            var land = box.land;
            var goal = Goal(c, m, count, hour, out float faceYaw);
            var before = m.pos;
            m.pos = Vector2.MoveTowards(m.pos, goal, WalkSpeed * dt);
            if (land.IsWater(m.pos.x, m.pos.y)) m.pos = before;
            float speed = (m.pos - before).magnitude / Mathf.Max(dt, 1e-4f);
            bool walking = (m.pos - goal).sqrMagnitude > 0.0004f && speed > 0.01f;
            // asleep once in the shelter: hidden inside it
            bool hide = doing == ApartDoing.Sleep && !walking;
            if (hide != m.hidden) { m.hidden = hide; v.gameObject.SetActive(!hide); }
            else if (!hide && !v.gameObject.activeSelf) v.gameObject.SetActive(true);
            if (hide) return;
            var style = walking ? DollStyle.Walk : doing switch
            {
                ApartDoing.SitAtFire => Mathf.Repeat(clock + m.phase * 7f, 9f) > 7.5f ? DollStyle.Talk : DollStyle.Sit,
                ApartDoing.Fish => DollStyle.Fish,
                ApartDoing.Chop => DollStyle.Chop,
                ApartDoing.Weave => DollStyle.SitWeave,
                ApartDoing.Wander => DollStyle.Look,
                _ => DollStyle.Stand,
            };
            var vel = (m.pos - before);
            float want = walking ? Mathf.Atan2(vel.x, vel.y) * Mathf.Rad2Deg + 180f : faceYaw;
            m.yaw = Mathf.MoveTowardsAngle(m.yaw, want, 300f * dt);
            m.stride = Mathf.Repeat(m.stride + DollMotion.Cadence(style, speed) * Mathf.PI * dt, Mathf.PI * 2f);
            bool holds = doing == ApartDoing.Fish || doing == ApartDoing.Chop;
            var pose = DollMotion.Evaluate(style, clock + m.phase, 1f, 0, 1, PropSlot.Hand, holds, speed, 0f, 0f, 0, m.stride);
            float s = v.scale;
            var yawQ = Quaternion.Euler(0f, m.yaw + pose.yaw, 0f);
            var tilt = pose.lying ? Quaternion.Euler(90f, 0f, 0f) : Quaternion.Euler(-pose.lean, 0f, pose.roll);
            v.transform.SetLocalPositionAndRotation(land.OnGround(m.pos) + yawQ * (pose.offset * s), yawQ * tilt);
            v.transform.localScale = new Vector3(k, pose.squash * k, k);
            v.ApplyArms(pose);
        }

        // ================================================================== tests and debug

        /// <summary>Where the group's camp is drawn (its fire), false if it isn't drawn.</summary>
        public bool TryGetCamp(int groupId, out Vector2 fire, out bool lit, out bool nudged)
        {
            fire = default; lit = false; nudged = false;
            if (!camps.TryGetValue(groupId, out var c) || c.root == null) return false;
            fire = c.fire; lit = c.lit; nudged = c.nudged;
            return true;
        }

        /// <summary>The group's people drawn now: (sim id, box position, shown).</summary>
        public List<(int id, Vector2 pos, bool shown)> DrawnMembers(int groupId)
        {
            var list = new List<(int, Vector2, bool)>();
            if (!camps.TryGetValue(groupId, out var c)) return list;
            foreach (var m in c.members.Values) list.Add((m.id, m.pos, m.view != null && m.view.gameObject.activeInHierarchy));
            return list;
        }

        /// <summary>The group's dolls drawn now (tests: their meshes must go with them).</summary>
        public List<PegDollView> Dolls(int groupId) =>
            camps.TryGetValue(groupId, out var c) ? c.members.Values.Where(m => m.view != null).Select(m => m.view).ToList() : new List<PegDollView>();

        /// <summary>How many meshes the camps own now (the sanity check's count).</summary>
        public int MeshCount => camps.Values.Sum(c => c.meshes.Count + c.towerMeshes.Count + c.members.Count * 2);

        public static string DebugStatus()
        {
            var v = Instance;
            if (v == null) return "no ApartGroupsView";
            if (v.camps.Count == 0) return "apart: none drawn";
            var sb = new StringBuilder();
            var ci = CultureInfo.InvariantCulture;
            foreach (var c in v.camps.Values)
                sb.AppendFormat(ci, "apart {0} {1} at ({2:0.00},{3:0.00}){4}{8} · fire {5} · {6} drawn: {7}\n", c.groupId, c.kind, c.fire.x, c.fire.y,
                    c.nudged ? " (nudged)" : "", c.lit ? "lit" : "cold", c.members.Count,
                    string.Join(", ", c.members.Values.Select(m => $"{(m.scripted ? "scripted" : m.doing.ToString())}{(m.hidden ? " (in)" : "")}{(m.lift > 0.01f ? $" (up {m.lift:0.00})" : "")}")),
                    (c.hasWater ? " · by water" : " · no water") + (c.boat != null ? " · boat" : "") + (c.towerStage > 0 ? $" · tower {c.towerStage}{(c.lampOn ? " lit" : "")}" : ""));
            return sb.ToString().TrimEnd('\n');
        }

        /// <summary>Debug: fly the camera to a group's camp (the first when <paramref name="groupId"/> is -1).</summary>
        public static string DebugFly(int groupId = -1, float zoom = 0.45f)
        {
            var v = Instance;
            var cam = Cam;
            if (v == null || cam == null) return "no ApartGroupsView or camera";
            var c = groupId < 0 ? v.camps.Values.FirstOrDefault() : v.camps.TryGetValue(groupId, out var x) ? x : null;
            if (c == null) return "no camp drawn";
            cam.EaseTo(v.transform.TransformPoint(v.box.land.OnGround(c.fire)), cam.yaw, cam.pitch, zoom, 0.6f);
            return "flying to apart camp " + c.groupId;
        }
    }
}
