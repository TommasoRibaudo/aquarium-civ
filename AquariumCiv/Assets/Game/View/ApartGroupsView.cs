using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using AquariumCiv.Game;
using AquariumCiv.Life;
using AquariumCiv.Sim;
using AquariumCiv.Sim.Eco;
using SiteMap = AquariumCiv.Sim.Settle.SiteMap;
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
            public bool scripted, onBoat;
            public float lift;
            public PropKind prop;
            // #578: the cutter's carry (chop, lift a log, walk it to the pile, set it down) paced by the sim's wood rate
            public float carryClock;
            public bool dropped;
            public CarryPhase carry;
        }

        sealed class Camp
        {
            public float poolMax = 9f; // #539: the fire's light pool stops short of the water and the pond hole
            public int groupId, dilemmaId;
            public string kind;
            public Transform root;
            public bool lit;
            public Vector2 spot, home, back, side, water, fire, shelter, work, fishAt, boatAt, fishFace;
            public bool fishWet, fishing; // #495: the fisher's dry bank point is the eco pond's edge now; the pond has open water and fish
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
            // #578: the travellers' wood and their walk in. chopAt: where the cutter cuts; pile: the logs set down at the woodpile (pileShown of them);
            // carryNow: a cheat has sent the cutter out; joinClock: seconds since they began to pack up (0 = not joining)
            public Vector2 chopAt;
            public GameObject pile;
            public int pileShown = -1;
            public float rate = 1f;
            public bool carryNow;
            public float joinClock;
            public int leanStage;
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
                if (g.joinState == 1 && c.root != null && JoinCalm(sim)) sim.BeginApartJoin(g.id); // #578: due; it starts when watched and calm
                if (g.joinState == 2 && c.root != null) { DriveJoin(sim, c, g, k, dt); continue; } // #578: packing up and walking in to the green
                UpdateTower(sim, c, g, hour); // #404
                UpdateBoat(sim, c, hour);
                c.rate = sim.ApartWoodRate(g);
                UpdatePile(c, g);
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
            var (x, z) = sim.ApartCampSpot(c.home.x, c.home.y, c.kind, out bool moved); // #539: also clear of where the pond will be
            var spot = new Vector2(x, z);
            var land = box.land;
            // never in the water, also without a map
            for (int i = 0; i < 12 && land.IsWater(spot.x, spot.y); i++) spot = Vector2.MoveTowards(spot, Vector2.zero, 0.08f);
            if (c.root != null && (spot - c.spot).sqrMagnitude < 1e-6f) { if (c.kind == ApartKind.ShoreHut) FishSpot(sim, c); return; } // #495: the pond grows: ask again
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
            c.poolMax = PoolReach(sim, spot);
            if (c.kind == ApartKind.ShoreHut)
            {
                c.shelter = spot + c.back * 0.3f;
                c.work = spot + c.side * 0.26f;                 // the fish rack
                if (c.hasWater)
                {
                    var p = spot;
                    for (int i = 0; i < 20 && !Wet(p); i++) p += c.water * 0.04f; // #404 tester: the living pond's water, not the fixed line (the boat sat out in the pond)
                    c.fishAt = p - c.water * 0.06f;             // the last dry step
                    c.boatAt = p + c.water * 0.02f - c.side * 0.16f;
                }
                else { c.fishAt = spot + c.side * 0.3f; c.boatAt = c.fishAt; }
                c.boatOut = BoatOutFrom(c);
                FishSpot(sim, c);
            }
            else
            {
                c.shelter = spot - c.side * 0.3f + c.back * 0.08f; // beside the fire, not back under the trees
                c.work = spot + c.side * 0.3f;                  // the woodpile
                c.fishAt = c.boatAt = c.work;
                c.chopAt = c.work + c.side * 0.45f;             // #578: where the cutter cuts; the logs are carried from here to the pile
                if (Wet(c.chopAt) || land.IsWater(c.chopAt.x, c.chopAt.y)) c.chopAt = c.work - c.back * 0.14f;
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
            c.pile = null; c.pileShown = -1; // #578: the pile of logs is drawn again under the new root
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
                if (c.chopAt != c.work - c.back * 0.14f) Part(c, "ChopLogs", ApartProps.Logs(2, 1), c.chopAt - c.side * 0.1f, yawBack); // #578: the cutter's own two
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

        /// <summary>#539: how far a fire at <paramref name="at"/> can light the ground before the pond (its full basin, wet or dry) begins.</summary>
        static float PoolReach(SimCore sim, Vector2 at)
        {
            var mask = sim.FutureWaterMask(0f);
            if (mask == null) return 9f;
            float best = 9f;
            const float scan = 0.7f;
            int n = Mathf.CeilToInt(scan / SiteMap.Cell);
            for (int dj = -n; dj <= n; dj++)
                for (int di = -n; di <= n; di++)
                {
                    int cell = SiteMap.CellAt(at.x + di * SiteMap.Cell, at.y + dj * SiteMap.Cell);
                    if (cell < 0 || !mask[cell]) continue;
                    float d = Mathf.Sqrt(di * di + dj * dj) * SiteMap.Cell - SiteMap.Cell * 0.5f;
                    if (d < best) best = d;
                }
            return Mathf.Max(0.12f, best);
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
            Add(foot + Vector3.up * 0.02f, Mathf.Min(look.poolRadius * s, c.poolMax), 4f, Palette.FirePool, phase, look.poolBoost);
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
                    at = c.fishAt + (c.fishWet ? new Vector2(-c.fishFace.y, c.fishFace.x) : c.side) * (m.index * 0.08f); face = c.fishWet ? c.fishFace : c.hasWater ? c.water : c.side; break;
                case ApartDoing.Chop:
                    at = c.kind == ApartKind.ShoreHut ? c.work - c.back * 0.14f : CarryGoal(c, m); face = c.back; break;
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

        /// <summary>#495: the fisher stands on dry ground at the eco pond's open-water edge nearest the hut (<see cref="PondShore"/>), facing the water; asked again every spot check, so it follows the pond as it grows.</summary>
        void FishSpot(SimCore sim, Camp c)
        {
            c.fishing = sim.FishingWater;
            c.fishWet = PondShore.NearestShore(sim, c.spot.x, c.spot.y, 0, out float sx, out float sz, out float fx, out float fz);
            if (!c.fishWet) return;
            c.fishAt = new Vector2(sx, sz);
            c.fishFace = new Vector2(fx - sx, fz - sz).normalized;
        }

        void Step(Camp c, Member m, int count, float hour, float k, float dt, bool sceneHasThem)
        {
            var v = m.view;
            if (sceneHasThem) { v.gameObject.SetActive(false); return; } // the choice scene's travellers are them, for now
            var doing = ApartLife.Doing(c.kind, m.index, hour, c.fishing || c.kind != ApartKind.ShoreHut);
            if (c.carryNow && m.index == 0 && c.kind != ApartKind.ShoreHut) doing = ApartDoing.Chop; // the cheat: out to cut now, whatever the hour
            if (doing != m.doing)
            {
                m.doing = doing;
                m.prop = doing == ApartDoing.Fish ? PropKind.Rod : doing == ApartDoing.Chop ? PropKind.Axe : PropKind.None;
                v.SetProp(m.prop, PropSlot.Hand);
            }
            var carry = CarryPhase.Chop;
            if (doing == ApartDoing.Chop && c.kind != ApartKind.ShoreHut) carry = Carry(c, m, dt); // #578
            var land = box.land;
            var goal = Goal(c, m, count, hour, out float faceYaw);
            var before = m.pos;
            m.pos = Vector2.MoveTowards(m.pos, goal, WalkSpeed * dt);
            if (Wet(m.pos)) m.pos = before;
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
                ApartDoing.Chop => carry == CarryPhase.Chop ? DollStyle.Chop : DollStyle.Stand,
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

        /// <summary>#539: the radius of the camp fire's light pool on the ground, as drawn (clipped to stop short of the pond's basin).</summary>
        public bool TryGetPoolRadius(int groupId, out float radius)
        {
            radius = 0f;
            if (!camps.TryGetValue(groupId, out var c) || c.root == null || !c.lit) return false;
            radius = Mathf.Min(FireLooks.Spec(FireLooks.Active).poolRadius * 0.9f, c.poolMax);
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
