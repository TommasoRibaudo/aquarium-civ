using System.Collections.Generic;
using AquariumCiv.Life;
using AquariumCiv.Sim;
using UnityEngine;
using UnityEngine.Rendering;

namespace AquariumCiv.View
{
    /// <summary>
    /// #404 (playtest note #386; the user's calls 2026-11-07): the people apart do more than sit there.
    /// <list type="bullet">
    /// <item><b>The boat.</b> On about two mornings in three the shore family pushes the boat off, rows out on the pond, fishes from it and rows
    /// back before midday (while the pond has water; else they fish from the shore as before).</item>
    /// <item><b>The lookout tower</b> they give the Keeper in the town (<see cref="SimCore.TowerStage"/>): it goes up in three stages while
    /// they hammer at it and carry planks; once it stands, the first of them climbs up at dusk with a lantern, lights its lamp, waves and
    /// climbs down (the lamp burns till dawn), and the second keeps a lookout from the platform by day, pointing and waving when the boat
    /// comes back.</item>
    /// </list>
    /// Who does what goes by their order among the living: the first lights the lamp and rows, the second keeps the lookout and hammers, the
    /// third rows with the first and carries the planks.
    /// </summary>
    public partial class ApartGroupsView
    {
        // the dusk lamp (view hours)
        public const float LightWalk = 18.2f, LightClimb = 18.4f, LightTop = 18.6f, LightOn = 18.66f, LightWave = 18.8f, LightDown = 18.9f, LightDone = 19.1f;
        /// <summary>View hours a climb up or down the ladder takes (about three seconds).</summary>
        public const float ClimbHours = 0.2f;
        /// <summary>The lookout's watches (view hours): a walk to the ladder, the climb, the watch, the climb down.</summary>
        public static readonly (float from, float to)[] Watches = { (9f, 12f), (14f, 16.5f) };
        // the boat's morning (view hours): walk down to it, push off and row out, fish, row back, step out
        public const float BoatWalk = 8f, BoatLaunch = 8.3f, BoatOutAt = 8.6f, BoatBack = 10.9f, BoatIn = 11.2f, BoatOff = 11.35f;
        /// <summary>The share of mornings the boat goes out.</summary>
        public const float BoatChance = 2f / 3f;
        // the build's working hours
        static readonly (float from, float to)[] BuildHours = { (8f, 12f), (13.5f, 17f) };

        struct Script
        {
            public Vector2 at, face;
            public float lift;
            public DollStyle style;
            public PropKind prop;
            public bool walk, onBoat, wade, climb;
            /// <summary>#578: a walk's pace as a multiple of the camp walk (0 = 1): the travellers hurry to the green.</summary>
            public float speed;
            /// <summary>A climb's ladder foot: nobody starts up before they've walked to it.</summary>
            public Vector2 foot;
        }

        // ================================================================== the tower

        void UpdateTower(SimCore sim, Camp c, ApartGroup g, float hour)
        {
            int stage = g.tower ? sim.TowerStage(g) : 0;
            if (stage != c.towerStage) BuildTower(c, g, stage);
            bool on = stage >= SimCore.TowerDone && c.lit && LampLit(hour);
            if (stage >= SimCore.TowerDone && on != c.lampOn) SetLamp(c, on);
        }

        /// <summary>The lamp burns from the moment it's lit at dusk until dawn.</summary>
        public static bool LampLit(float hour) => hour >= LightOn || hour < ApartLife.WakeHour;

        void BuildTower(Camp c, ApartGroup g, int stage)
        {
            foreach (var o in c.towerMeshes) Kill(o);
            c.towerMeshes.Clear();
            if (c.root != null)
                foreach (var name in new[] { "Tower", "TowerLamp", "TowerGlow" })
                {
                    var t = c.root.Find(name);
                    if (t != null) Kill(t.gameObject);
                }
            c.towerStage = stage;
            c.lampOn = false;
            if (stage <= 0 || c.root == null) return;
            c.towerAt = new Vector2(g.towerX, g.towerZ);
            var toCamp = c.fire - c.towerAt;
            if (toCamp.sqrMagnitude < 1e-6f) toCamp = Vector2.down;
            toCamp.Normalize();
            c.towerYaw = Mathf.Atan2(-toCamp.x, -toCamp.y) * Mathf.Rad2Deg; // the ladder (local -z) faces their camp
            TowerPart(c, "Tower", ApartProps.Tower(stage, c.groupId));
            if (stage >= SimCore.TowerDone) SetLamp(c, false);
        }

        void TowerPart(Camp c, string name, Mesh mesh)
        {
            c.towerMeshes.Add(mesh);
            var go = new GameObject(name) { hideFlags = HideFlags.DontSave };
            go.transform.SetParent(c.root, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = box.SolidMaterial;
            mr.shadowCastingMode = ShadowCastingMode.On;
            go.transform.SetLocalPositionAndRotation(box.land.OnGround(c.towerAt), Quaternion.Euler(0f, c.towerYaw, 0f));
        }

        void SetLamp(Camp c, bool on)
        {
            c.lampOn = on;
            if (c.root == null) return;
            foreach (var name in new[] { "TowerLamp", "TowerGlow" })
            {
                var t = c.root.Find(name);
                if (t == null) continue;
                var mf = t.GetComponent<MeshFilter>();
                if (mf != null && mf.sharedMesh != null) { c.towerMeshes.Remove(mf.sharedMesh); Kill(mf.sharedMesh); }
                Kill(t.gameObject);
            }
            TowerPart(c, "TowerLamp", ApartProps.TowerLamp(on, c.groupId));
            if (on) LampGlow(c);
        }

        /// <summary>The lit lamp's halo, as a camp fire's (the Glow shader brightens it with the night).</summary>
        void LampGlow(Camp c)
        {
            var lampXZ = OnTower(c, ApartProps.TowerLampX, ApartProps.TowerLampZ);
            var at = new Vector3(lampXZ.x, box.land.OnGround(c.towerAt).y + ApartProps.TowerLampY, lampXZ.y);
            var verts = new List<Vector3>(); var cols = new List<Color>(); var corner = new List<Vector2>(); var data = new List<Vector2>(); var extra = new List<Vector2>();
            foreach (var q in new[] { new Vector2(-1, -1), new Vector2(-1, 1), new Vector2(1, 1), new Vector2(1, -1) })
            { verts.Add(at); cols.Add(Palette.FireHalo); corner.Add(q); data.Add(new Vector2(0.24f, 0f)); extra.Add(new Vector2(Pieces.Phase(c.groupId * 17 + 3), 0.05f)); }
            var m = new Mesh { name = "ApartTowerGlow", hideFlags = HideFlags.DontSave };
            m.SetVertices(verts); m.SetColors(cols); m.SetUVs(0, corner); m.SetUVs(1, data); m.SetUVs(2, extra); m.SetTriangles(new[] { 0, 1, 2, 0, 2, 3 }, 0);
            m.bounds = new Bounds(at, Vector3.one);
            c.towerMeshes.Add(m);
            var go = new GameObject("TowerGlow") { hideFlags = HideFlags.DontSave };
            go.transform.SetParent(c.root, false);
            go.AddComponent<MeshFilter>().sharedMesh = m;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = box.GlowMaterial;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
        }

        /// <summary>A point on the tower: local x/z (the ladder at -z) to box coordinates.</summary>
        Vector2 OnTower(Camp c, float lx, float lz)
        {
            float r = c.towerYaw * Mathf.Deg2Rad, cs = Mathf.Cos(r), sn = Mathf.Sin(r);
            return c.towerAt + new Vector2(lx * cs + lz * sn, -lx * sn + lz * cs);
        }

        Vector2 IntoTower(Camp c) => OnTower(c, 0f, 1f) - c.towerAt;

        /// <summary>On the ladder at height h (0 at its foot, the platform at the top), facing it.</summary>
        void Climbing(Camp c, float h, ref Script s)
        {
            s.at = OnTower(c, 0f, ApartProps.LadderZ(h) - 0.03f);
            s.lift = h;
            s.face = IntoTower(c);
            s.style = DollStyle.Reach;
            s.climb = true;
            s.foot = LadderFoot(c);
        }

        Vector2 LadderFoot(Camp c) => OnTower(c, 0f, ApartProps.TowerLadderZ - 0.07f);

        // ================================================================== the boat

        /// <summary>Where the boat fishes: out from its beach along the way to the water, stopping short of the far bank.</summary>
        Vector2 BoatOutFrom(Camp c)
        {
            if (!c.hasWater) return c.boatAt;
            float reach = 0f;
            for (int i = 1; i <= 20; i++)
            {
                var p = c.boatAt + c.water * (i * 0.03f);
                if (!Wet(p)) break;
                reach = i * 0.03f;
            }
            return c.boatAt + c.water * Mathf.Clamp(reach - 0.12f, 0.06f, 0.45f);
        }

        /// <summary>Water here now: the living pond's (which may be dry or low), else the fixed one.</summary>
        bool Wet(Vector2 p) => box.layout != null && box.layout.eco.on ? box.land.Height(p.x, p.y) < box.layout.eco.waterY : box.land.IsWater(p.x, p.y);

        float WaterLevel => box.layout != null && box.layout.eco.on ? box.layout.eco.waterY : Land.WaterY;

        bool PondHasWater => box.layout == null || !box.layout.eco.on || box.layout.eco.pondCells > 0;

        /// <summary>The boat goes out this view day (one per sim month): about two mornings in three, a shore family's, while the pond has water.</summary>
        bool BoatDay(SimCore sim, Camp c) =>
            c.kind == ApartKind.ShoreHut && c.hasWater && c.boat != null && PondHasWater && (DebugBoatEveryDay || Land.Hash3(c.groupId, (int)(sim.State.month % 100000), 404) < BoatChance);

        /// <summary>Debug (the panel, tests): the boat goes out every morning.</summary>
        public static bool DebugBoatEveryDay;

        /// <summary>Where the boat is at this hour of a boat day, its heading (the way its bow points), and whether it floats.</summary>
        bool BoatAt(Camp c, float hour, out Vector2 p, out Vector2 heading)
        {
            heading = c.water;
            if (hour < BoatLaunch || hour >= BoatIn) { p = c.boatAt; return false; }
            if (hour < BoatOutAt) { p = Vector2.Lerp(c.boatAt, c.boatOut, Mathf.SmoothStep(0f, 1f, (hour - BoatLaunch) / (BoatOutAt - BoatLaunch))); return true; }
            if (hour >= BoatBack) { p = Vector2.Lerp(c.boatOut, c.boatAt, Mathf.SmoothStep(0f, 1f, (hour - BoatBack) / (BoatIn - BoatBack))); return true; }
            // fishing: one slow loop of drift that ends where it began. The hull is the same at both ends, so it keeps its line across the
            // pond, out and back: the rowers keep their seats (a flip of the bow swapped them in one frame, the tester's jump at 10:54).
            float a = Mathf.PI * 2f * (hour - BoatOutAt) / (BoatBack - BoatOutAt);
            p = c.boatOut + new Vector2(Mathf.Cos(a) - 1f, Mathf.Sin(a)) * 0.03f;
            if (!Wet(p)) p = c.boatOut;
            return true;
        }

        void UpdateBoat(SimCore sim, Camp c, float hour)
        {
            if (c.boat == null) return;
            if (!BoatDay(sim, c) || !BoatAt(c, hour, out var p, out var heading))
            {
                c.boat.SetLocalPositionAndRotation(box.land.OnGround(c.boatAt), Quaternion.Euler(0f, c.boatHomeYaw, 0f));
                return;
            }
            float bob = Mathf.Sin(clock * 2.1f + c.groupId) * 0.004f;
            float yaw = Mathf.Atan2(-heading.y, heading.x) * Mathf.Rad2Deg; // the hull's length is local x
            c.boat.SetLocalPositionAndRotation(new Vector3(p.x, WaterLevel - 0.012f + bob, p.y), Quaternion.Euler(Mathf.Sin(clock * 1.7f) * 2f, yaw, 0f));
        }

        // ================================================================== who is in a scripted moment now

        bool Scripted(SimCore sim, Camp c, int rank, int alive, float hour, out Script s)
        {
            s = default;
            if (c.root == null || alive == 0) return false;
            bool boatDay = BoatDay(sim, c);
            bool rower = rank == 0 || rank == 2;
            if (boatDay && rower && hour >= BoatWalk && hour < BoatOff) return BoatScript(c, rank, hour, ref s);
            if (c.towerStage >= SimCore.TowerDone)
            {
                if (rank == 0 && hour >= LightWalk && hour < LightDone) return LampScript(c, hour, ref s);
                int lookout = alive >= 2 ? 1 : 0;
                if (rank == lookout)
                    foreach (var w in Watches)
                        if (hour >= w.from && hour < w.to && LookoutScript(c, hour, w.from, w.to, boatDay, ref s)) return true;
            }
            else if (c.towerStage >= 1)
                foreach (var w in BuildHours)
                    if (hour >= w.from && hour < w.to) return BuildScript(c, rank, alive, hour, boatDay, ref s);
            return false;
        }

        bool BoatScript(Camp c, int rank, float hour, ref Script s)
        {
            int seat = rank == 0 ? 0 : 1;
            if (hour < BoatLaunch)
            {
                // down to the beached boat and into their seat in it (wading the last step), so the launch carries them off from there
                s.walk = true; s.wade = true; s.at = c.boatAt + c.water * (seat == 0 ? 0.045f : -0.045f); s.face = seat == 0 ? -c.water : c.water; s.style = DollStyle.Sit;
                return true;
            }
            if (hour >= BoatIn)
            {
                s.walk = true; s.wade = true; s.at = c.boatAt - c.water * 0.16f + c.side * (seat == 0 ? -0.06f : 0.06f); s.face = -c.water; s.style = DollStyle.Stand;
                return true;
            }
            BoatAt(c, hour, out var p, out _);
            var along = c.water; // the seats keep their ends of the hull, out and back
            s.onBoat = true;
            s.at = p + along * (seat == 0 ? 0.045f : -0.045f);
            s.lift = WaterLevel + 0.01f;
            s.face = seat == 0 ? -along : along;
            bool fishing = hour >= BoatOutAt && hour < BoatBack;
            if (fishing && seat == 0) { s.style = DollStyle.Fish; s.prop = PropKind.Rod; }
            else { s.style = DollStyle.Sit; s.prop = fishing ? PropKind.None : PropKind.Stick; } // the oar
            return true;
        }

        bool LampScript(Camp c, float hour, ref Script s)
        {
            float top = ApartProps.TowerPlatformY + 0.01f;
            if (hour < LightClimb) { s.walk = true; s.at = LadderFoot(c); s.face = IntoTower(c); s.style = DollStyle.Stand; s.prop = PropKind.Lantern; return true; }
            if (hour < LightTop) { Climbing(c, top * (hour - LightClimb) / (LightTop - LightClimb), ref s); s.prop = PropKind.Lantern; return true; }
            if (hour < LightDown)
            {
                s.at = OnTower(c, ApartProps.TowerLampX * 0.6f, -0.05f); s.lift = top;
                if (hour < LightWave) { s.face = OnTower(c, ApartProps.TowerLampX, ApartProps.TowerLampZ) - s.at; s.style = DollStyle.Reach; s.prop = hour < LightOn ? PropKind.Lantern : PropKind.None; } // out to the lamp on its arm
                else { s.face = -IntoTower(c); s.style = DollStyle.Wave; }
                return true;
            }
            Climbing(c, top * (1f - Mathf.Clamp01((hour - LightDown) / (LightDone - LightDown))), ref s);
            return true;
        }

        bool LookoutScript(Camp c, float hour, float from, float to, bool boatDay, ref Script s)
        {
            float top = ApartProps.TowerPlatformY + 0.01f;
            float up = from + 0.25f, onTop = up + ClimbHours, offTop = to - 0.25f - ClimbHours, down = to - 0.25f;
            if (hour < up) { s.walk = true; s.at = LadderFoot(c); s.face = IntoTower(c); s.style = DollStyle.Stand; return true; }
            if (hour < onTop) { Climbing(c, top * (hour - up) / ClimbHours, ref s); return true; }
            if (hour < offTop)
            {
                s.at = OnTower(c, 0.03f, 0.04f); s.lift = top;
                s.face = c.hasWater ? c.water : -IntoTower(c);
                bool boatComing = boatDay && hour >= BoatBack - 0.1f && hour < BoatIn + 0.1f;
                if (boatComing)
                {
                    BoatAt(c, Mathf.Min(hour, BoatIn - 0.01f), out var bp, out _);
                    var toBoat = bp - s.at;
                    if (toBoat.sqrMagnitude > 1e-6f) s.face = toBoat;
                    s.style = hour < (BoatBack + BoatIn) * 0.5f ? DollStyle.Point : DollStyle.Wave;
                }
                else s.style = DollStyle.Look; // shading their eyes over the pond and the glass
                return true;
            }
            if (hour < down) { Climbing(c, top * (1f - (hour - offTop) / ClimbHours), ref s); return true; }
            return false; // back to their day
        }

        bool BuildScript(Camp c, int rank, int alive, float hour, bool boatDay, ref Script s)
        {
            bool rowing = boatDay && (rank == 0 || rank == 2) && hour < BoatOff;
            if (rowing) return false;
            int hammerer = alive >= 2 ? 1 : 0;
            if (rank == hammerer)
            {
                s.walk = true; s.at = OnTower(c, ApartProps.TowerHalf + 0.07f, 0f); s.face = c.towerAt - s.at;
                s.style = DollStyle.Hammer; s.prop = PropKind.Hammer;
                return true;
            }
            // the others carry planks from the camp to the pile by the tower, half an hour each way
            bool loaded = Mathf.Repeat(hour, 1f) >= 0.5f;
            s.walk = true;
            s.at = loaded ? OnTower(c, ApartProps.TowerHalf + 0.12f, -0.12f) : c.work - c.back * 0.1f;
            s.face = loaded ? c.towerAt - s.at : c.work - s.at;
            s.style = loaded ? DollStyle.Lift : DollStyle.Stand;
            s.prop = loaded ? PropKind.Log : PropKind.None;
            return true;
        }

        // ================================================================== a scripted step

        void StepScripted(Camp c, Member m, Script s, float k, float dt)
        {
            var v = m.view;
            if (m.hidden || !v.gameObject.activeSelf) { m.hidden = false; v.gameObject.SetActive(true); }
            if (!m.scripted || m.prop != s.prop) { v.SetProp(s.prop, PropSlot.Hand); m.prop = s.prop; }
            m.scripted = true;
            m.doing = (ApartDoing)(-1);
            var land = box.land;
            var before = m.pos;
            bool walking = false;
            // a climb waits until they've walked to the ladder's foot (the tester saw a lookout jump 0.85 to it)
            if (s.climb && m.lift < 0.02f && (m.pos - s.foot).sqrMagnitude > 0.04f * 0.04f) { s.walk = true; s.at = s.foot; s.face = s.at - m.pos; }
            // a rower not in the boat yet (late from the camp) wades after their seat rather than being put in it
            if (s.onBoat && !m.onBoat && (m.pos - s.at).sqrMagnitude > 0.05f * 0.05f) { s.walk = true; s.wade = true; s.onBoat = false; s.lift = 0f; }
            if (s.walk)
            {
                if (m.lift > 0.02f && !m.onBoat) { m.pos = s.at; m.lift = 0f; } // never walk off a platform (the climb ends at its foot); out of a boat they step
                m.pos = Vector2.MoveTowards(m.pos, s.at, WalkSpeed * (s.speed > 0f ? s.speed : 1f) * dt);
                if (Wet(m.pos) && !s.wade) m.pos = before;
                walking = (m.pos - s.at).sqrMagnitude > 0.0004f;
                m.lift = 0f;
                m.onBoat = false;
            }
            else if (m.lift > 0.02f && s.lift > 0.02f && !s.onBoat && !m.onBoat && (m.pos - s.at).sqrMagnitude > 0.0004f)
            {
                // on the ladder or the platform: from the ladder's top to their spot (and back down) they step, not jump
                m.pos = Vector2.MoveTowards(m.pos, s.at, WalkSpeed * dt);
                m.lift = s.lift;
            }
            else { m.pos = s.at; m.lift = s.lift; m.onBoat = s.onBoat; }
            float speed = (m.pos - before).magnitude / Mathf.Max(dt, 1e-4f);
            var style = walking ? DollStyle.Walk : s.style;
            var dir = walking ? m.pos - before : s.face;
            if (dir.sqrMagnitude > 1e-8f) m.yaw = Mathf.MoveTowardsAngle(m.yaw, Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg + 180f, 300f * dt);
            m.stride = Mathf.Repeat(m.stride + DollMotion.Cadence(style, speed) * Mathf.PI * dt, Mathf.PI * 2f);
            var pose = DollMotion.Evaluate(style, clock + m.phase, 1f, 0, 1, PropSlot.Hand, s.prop != PropKind.None, speed, 0f, 0f, 0, m.stride);
            float sc = v.scale;
            var yawQ = Quaternion.Euler(0f, m.yaw + pose.yaw, 0f);
            var tilt = pose.lying ? Quaternion.Euler(90f, 0f, 0f) : Quaternion.Euler(-pose.lean, 0f, pose.roll);
            var ground = land.OnGround(m.pos);
            if (s.wade) ground.y = Mathf.Max(ground.y, WaterLevel - 0.01f); // wading the shallows, not walking the bed
            var foot = s.onBoat && !walking ? new Vector3(m.pos.x, m.lift, m.pos.y) : ground + Vector3.up * m.lift;
            v.transform.SetLocalPositionAndRotation(foot + yawQ * (pose.offset * sc), yawQ * tilt);
            v.transform.localScale = new Vector3(k, pose.squash * k, k);
            v.ApplyArms(pose);
        }

        // ================================================================== townsfolk at the tower (PeopleView asks)

        /// <summary>Real seconds a townsperson takes up or down the ladder, and a kid's turn on it.</summary>
        public const float TownClimbSeconds = 3f, KidTurnSeconds = 8f;

        /// <summary>
        /// #404: where a townsperson at the tower (<see cref="PlaceKind.Tower"/>) stands now, how high, facing what, in which pose. The first
        /// two grown-ups (slots 0 and 1: a couple goes up together) climb to the platform over <see cref="TownClimbSeconds"/> and look out,
        /// now and then talking or pointing, and climb down in their last seconds there; anyone after them sits round its foot. Kids dare
        /// each other: one at a time goes halfway up, waves and comes down, while the rest cheer at the foot. False when no tower stands.
        /// </summary>
        /// <param name="upSeconds">Real seconds since they stood still at the tower.</param>
        /// <param name="leftSeconds">Real seconds until they leave it.</param>
        public static bool TowerUse(int slot, bool kid, float upSeconds, float leftSeconds, float time, out Vector2 at, out float lift, out Vector2 face, out DollStyle style)
        {
            at = default; lift = 0f; face = default; style = DollStyle.Stand;
            var v = Instance;
            if (v == null) return false;
            Camp c = null;
            foreach (var x in v.camps.Values) if (x.root != null && x.towerStage >= SimCore.TowerDone) { c = x; break; }
            if (c == null) return false;
            float top = ApartProps.TowerPlatformY + 0.01f;
            var s = new Script();
            // while one of the people apart lights the lamp (18:12-19:06) the platform and the ladder are theirs: townsfolk wait at the foot
            var pvNow = PeopleView.Find();
            float hourNow = pvNow != null ? pvNow.ViewHour : 12f;
            bool lighting = hourNow >= LightWalk && hourNow < LightDone + 0.05f;
            if (!kid && slot < 2 && !lighting)
            {
                float t = Mathf.Clamp01(Mathf.Min(upSeconds, leftSeconds) / TownClimbSeconds);
                if (t < 1f) { v.Climbing(c, top * t, ref s); at = s.at; lift = s.lift; face = s.at + s.face; style = s.style; return true; }
                at = v.OnTower(c, slot == 0 ? -0.04f : 0.045f, 0.035f);
                lift = top;
                face = at + v.IntoTower(c); // out over the far side
                float ph = Mathf.Repeat(time * 0.11f + slot * 0.45f, 1f);
                style = ph < 0.55f ? DollStyle.Look : ph < 0.82f ? DollStyle.Talk : DollStyle.Point;
                return true;
            }
            if (!kid)
            {
                // round its foot, never in front of the ladder
                int k = lighting ? slot : slot - 2;
                float a = (50f + (k % 6) * 52f) * Mathf.Deg2Rad, r = 0.2f + (k / 6) * 0.07f;
                at = v.OnTower(c, Mathf.Sin(a) * r, -Mathf.Cos(a) * r);
                face = at + (at - c.towerAt);
                style = DollStyle.Sit;
                return true;
            }
            // the dare: one kid at a time on the ladder
            int turn = (int)Mathf.Floor(time / KidTurnSeconds);
            float p = Mathf.Repeat(time, KidTurnSeconds) / KidTurnSeconds;
            bool mine = turn % 4 == slot % 4 && upSeconds > 1f && leftSeconds > KidTurnSeconds && !lighting;
            if (mine && p < 0.85f)
            {
                float h = p < 0.35f ? top * 0.5f * p / 0.35f : p < 0.55f ? top * 0.5f : top * 0.5f * (1f - (p - 0.55f) / 0.3f);
                v.Climbing(c, h, ref s);
                at = s.at; lift = s.lift; face = s.at + s.face;
                style = p >= 0.35f && p < 0.55f ? DollStyle.WaveSmall : DollStyle.Reach;
                return true;
            }
            at = v.OnTower(c, ((slot % 4) - 1.5f) * 0.07f, ApartProps.TowerLadderZ - 0.13f - (slot / 4) * 0.06f);
            face = v.OnTower(c, 0f, ApartProps.TowerLadderZ);
            style = Mathf.Repeat(time * 0.7f + slot * 0.3f, 1f) < 0.5f ? DollStyle.Cheer : DollStyle.Laugh;
            return true;
        }

        /// <summary>The names of the people apart up the tower now (on the ladder or the platform).</summary>
        public List<string> UpTheTower(SimCore sim)
        {
            var names = new List<string>();
            foreach (var c in camps.Values)
            {
                if (c.towerStage <= 0) continue;
                var g = sim?.ApartGroups != null ? System.Linq.Enumerable.FirstOrDefault(sim.ApartGroups, x => x.id == c.groupId) : null;
                foreach (var m in c.members.Values)
                    if (m.lift > 0.1f && m.lift < 0.6f)
                    {
                        var n = g != null ? g.members.Find(x => x.id == m.id) : null;
                        if (n != null) names.Add(n.name);
                    }
            }
            return names;
        }

        /// <summary>The tower drawn now, for a tap: where it stands (box coordinates) and how tall it is.</summary>
        public bool TowerForTap(out Vector2 at, out float height)
        {
            at = default; height = 0f;
            foreach (var c in camps.Values)
                if (c.root != null && c.towerStage > 0) { at = c.towerAt; height = c.towerStage >= SimCore.TowerDone ? 0.68f : ApartProps.TowerPlatformY + 0.05f; return true; }
            return false;
        }

        // ================================================================== tests and debug

        /// <summary>The tower drawn for a group: its stage (0 none), where it stands, and whether its lamp is lit.</summary>
        public bool TryGetTower(int groupId, out int stage, out Vector2 at, out bool lampOn)
        {
            stage = 0; at = default; lampOn = false;
            if (!camps.TryGetValue(groupId, out var c) || c.root == null || c.towerStage <= 0) return false;
            stage = c.towerStage; at = c.towerAt; lampOn = c.lampOn;
            return true;
        }

        /// <summary>The group's people now: (sim id, box position, height above the ground, scripted).</summary>
        public List<(int id, Vector2 pos, float lift, bool scripted)> MembersNow(int groupId)
        {
            var list = new List<(int, Vector2, float, bool)>();
            if (!camps.TryGetValue(groupId, out var c)) return list;
            foreach (var m in c.members.Values) list.Add((m.id, m.pos, m.lift, m.scripted));
            return list;
        }

        /// <summary>Where the group's boat is now (box coordinates) and whether it floats.</summary>
        public bool TryGetBoat(int groupId, out Vector2 at, out bool afloat)
        {
            at = default; afloat = false;
            if (!camps.TryGetValue(groupId, out var c) || c.boat == null) return false;
            var p = c.boat.localPosition;
            at = new Vector2(p.x, p.z);
            afloat = (at - c.boatAt).sqrMagnitude > 0.0009f;
            return true;
        }

        /// <summary>Debug: fly the camera to the tower (the first drawn).</summary>
        public static string DebugFlyTower(float zoom = 0.35f)
        {
            var v = Instance;
            var cam = Cam;
            if (v == null || cam == null) return "no ApartGroupsView or camera";
            foreach (var c in v.camps.Values)
            {
                if (c.towerStage <= 0) continue;
                var at = v.box.land.OnGround(c.towerAt) + Vector3.up * (ApartProps.TowerPlatformY * 0.6f);
                cam.EaseTo(v.transform.TransformPoint(at), cam.yaw, cam.pitch, zoom, 0.6f);
                return "flying to the tower of group " + c.groupId;
            }
            return "no tower drawn";
        }

        /// <summary>The boat goes out this view day for this group (tests, the debug panel).</summary>
        public bool BoatGoesOut(int groupId) => camps.TryGetValue(groupId, out var c) && Sim != null && BoatDay(Sim, c);
    }
}
