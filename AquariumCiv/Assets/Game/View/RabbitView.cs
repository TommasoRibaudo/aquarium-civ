using System.Collections.Generic;
using AquariumCiv.Game;
using AquariumCiv.Life;
using AquariumCiv.Sim;
using AquariumCiv.Sim.Eco;
using AquariumCiv.Sim.Settle;
using UnityEngine;

namespace AquariumCiv.View
{
    /// <summary>
    /// Rabbits in the box (#282 phase 2; keepers-hands §2 beat 2): before the first rabbit, tracks in the grass at the meadow's
    /// edge; then the first rabbit hops in from the edge and a person nearby stops and points; then rabbits live in the meadow,
    /// as many as the sim counts (capped, <see cref="RabbitHerd.DefaultCap"/>). They graze, sit up and hop between green cells
    /// (<see cref="RabbitHerd.Field"/>), keep to the green, and scatter when someone comes close (<see cref="RabbitHerd.Scare"/>).
    /// <para><see cref="Field"/> and <see cref="FieldNow"/> are what the hunt planner shares (#283: <c>planner.Rabbits = Field</c>,
    /// sampled with <see cref="HuntClock.FieldSeconds"/>). Dolls are pooled.</para>
    /// </summary>
    [DefaultExecutionOrder(5)]
    public class RabbitView : MonoBehaviour
    {
        public static bool AutoCreate = true;
        public static RabbitView Instance { get; private set; }

        /// <summary>The first rabbit's sign lasts this many seconds (it hops in, the person points).</summary>
        public const float SignSeconds = 6f;
        /// <summary>The sign waits for daylight (view hours): nobody stands out pointing in the dark.</summary>
        public const float SignFromHour = 6.5f, SignToHour = 19f;

        /// <summary>The rabbits' shared field: the hunt planner takes this one (never its own).</summary>
        public RabbitHerd.Field Field { get; } = new RabbitHerd.Field();

        BoxView box;
        PeopleView people;
        Transform holder;
        Transform rootSeen;
        readonly List<RabbitDoll> pool = new();
        sealed class Slot { public RabbitDoll doll; public RabbitHerd.Scare scare = new(); public float grow; public int id; }
        readonly Dictionary<int, Slot> slots = new();
        readonly List<int> gone = new();
        readonly List<PersonRef> whoBuf = new();
        readonly List<float> px = new(), pz = new();
        double ownClock;
        float refreshT;
        int fieldSeason = int.MinValue, greenHash;
        bool fieldReady;

        // tracks
        GameObject tracks;
        int tracksKey;
        // the first rabbit's sign
        sealed class Sign { public int id; public float t; public Vector2 from; public PersonRef pointer; }
        Sign sign;

        /// <summary>Rabbit dolls shown now, and how many are fleeing, for tests and the debug panel.</summary>
        public int Shown { get { int n = 0; foreach (var s in slots.Values) if (s.doll != null && s.doll.gameObject.activeSelf) n++; return n; } }
        public int Fleeing { get { int n = 0; foreach (var s in slots.Values) if (s.scare.Fleeing) n++; return n; } }
        public bool TracksShown => tracks != null && tracks.activeSelf;
        public bool SignPlaying => sign != null;
        public int SignsPlayed { get; private set; }
        /// <summary>Who pointed at the first rabbit (none before it), and whether they were really in the pointing activity.</summary>
        public PersonRef LastPointer { get; private set; }
        public int SignRabbitId => sign != null ? sign.id : -1;

        /// <summary>Extra spots (box-local x, z) that scare the rabbits like a person would: for tests and the debug panel.</summary>
        public readonly List<Vector2> Scarers = new();

        SimCore Sim => GameClock.Instance != null ? GameClock.Instance.Sim : null;

        /// <summary>The field's clock in seconds: the people's view day clock (what the hunt planner samples with).</summary>
        public double FieldNow
        {
            get
            {
                var pv = People;
                if (pv != null && pv.Planner != null) return HuntClock.FieldSeconds(pv.Now.day, pv.Now.hour);
                return ownClock;
            }
        }

        PeopleView People => people != null ? people : people = PeopleView.Find();

        public static RabbitView Ensure(BoxView b)
        {
            if (b == null) return null;
            var v = b.GetComponent<RabbitView>();
            return v != null ? v : b.gameObject.AddComponent<RabbitView>();
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
            if (tracks != null) Destroy(tracks);
            holder = null; tracks = null; slots.Clear(); pool.Clear();
        }

        void Update()
        {
            var sim = Sim;
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.1f);
            ownClock += dt;
            if (sim == null || !sim.EcologyOn || box == null || box.Root == null) { HideAll(); return; }
            if (rootSeen != box.Root) { ForgetDolls(); rootSeen = box.Root; } // the box was rebuilt: its children went with the old root
            EnsureField(sim, dt);
            UpdateTracks(sim);
            TryStartSign(sim);
            UpdateRabbits(sim, dt);
        }

        // ---------------------------------------------------------------- the field
        void EnsureField(SimCore sim, float dt)
        {
            refreshT -= dt;
            int season = (int)(sim.State.month / 3);
            if (fieldReady && season == fieldSeason && refreshT > 0f) return;
            refreshT = 0.8f;
            // the green changes with the mists too, not only at the season: refresh when the mask changed
            int h = 17;
            var cover = sim.State.eco.cover;
            for (int c = 0; c < cover.Length; c++) h = h * 31 + (RabbitHerd.IsGreen((EcoCover)cover[c]) ? c + 1 : 0);
            if (fieldReady && season == fieldSeason && h == greenHash) return;
            fieldReady = true; fieldSeason = season; greenHash = h;
            sim.EcoRabbitsRefresh(Field);
        }

        void HideAll()
        {
            foreach (var s in slots.Values) if (s.doll != null) { s.doll.gameObject.SetActive(false); pool.Add(s.doll); }
            slots.Clear();
            if (tracks != null) tracks.SetActive(false);
            sign = null;
        }

        void ForgetDolls() { slots.Clear(); pool.Clear(); holder = null; tracks = null; tracksKey = 0; }

        Transform Holder()
        {
            if (holder == null)
            {
                holder = new GameObject("Rabbits") { hideFlags = HideFlags.DontSave }.transform;
                holder.SetParent(box.Root, false);
            }
            return holder;
        }

        // ---------------------------------------------------------------- tracks
        void UpdateTracks(SimCore sim)
        {
            var cells = sim.EcoRabbitTrackCells;
            if (cells.Count == 0) { if (tracks != null) tracks.SetActive(false); return; }
            int key = 7;
            foreach (int c in cells) key = key * 31 + c;
            if (tracks == null || key != tracksKey)
            {
                if (tracks != null) Destroy(tracks);
                tracks = BuildTracks(sim, cells);
                tracksKey = key;
            }
            tracks.SetActive(true);
        }

        GameObject BuildTracks(SimCore sim, IReadOnlyList<int> cells)
        {
            var mb = new MeshBuilder(box.seed + 77) { jitter = 0f };
            var land = box.land;
            var soil = new Color(0.22f, 0.14f, 0.07f);
            foreach (int cell in cells)
            {
                float cx = SiteMap.CXc(cell), cz = SiteMap.CZc(cell);
                uint r = RabbitHerd.H((uint)sim.State.eco.seed, (uint)cell, 77);
                float a = RabbitHerd.F(r, 1, 2) * Mathf.PI * 2f;
                var dir = new Vector2(Mathf.Sin(a), Mathf.Cos(a));
                var side = new Vector2(dir.y, -dir.x);
                // prints sized for the doll (RabbitDoll.Scale 1.2): a trail of small dark prints (no trodden patch: raised, it read as beetles)
                for (int g = 0; g < 4; g++)
                {
                    var at = new Vector2(cx, cz) + dir * ((g - 1.5f) * 0.075f) + side * (((g & 1) == 0 ? 1f : -1f) * 0.006f);
                    // the hind feet side by side, the forefeet one behind the other ahead of them
                    foreach (float s in new[] { -1f, 1f })
                        Print(mb, land, at - dir * 0.01f + side * (s * 0.012f), soil, 0.007f, 0.016f, a, 0.005f);
                    Print(mb, land, at + dir * 0.02f + side * 0.003f, soil, 0.005f, 0.008f, a, 0.005f);
                    Print(mb, land, at + dir * 0.033f - side * 0.003f, soil, 0.005f, 0.008f, a, 0.005f);
                }
            }
            var go = new GameObject("RabbitTracks") { hideFlags = HideFlags.DontSave };
            go.transform.SetParent(Holder(), false);
            go.AddComponent<MeshFilter>().sharedMesh = mb.ToMesh("RabbitTracks");
            var rend = go.AddComponent<MeshRenderer>();
            rend.sharedMaterial = box.SolidMaterial;
            rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }

        static void Print(MeshBuilder mb, Land land, Vector2 at, Color col, float rx, float rz, float yawRad, float lift)
        {
            var g = land.OnGround(at.x, at.y);
            mb.M = Matrix4x4.TRS(g + Vector3.up * lift, Quaternion.Euler(0f, yawRad * Mathf.Rad2Deg, 0f), Vector3.one);
            mb.Ico(Vector3.zero, 1f, new Vector3(rx, Mathf.Max(0.003f, rx * 0.25f), rz), col, 0.05f);
            mb.M = Matrix4x4.identity;
        }

        // ---------------------------------------------------------------- the first rabbit's sign
        void TryStartSign(SimCore sim)
        {
            if (sign != null || !sim.EcoRabbitArrivalPending || Field.Alive.Count == 0 || !Field.HasGreen) return;
            var pv = People;
            if (pv == null || pv.Planner == null) return;
            float hour = pv.ViewHour;
            if (hour < SignFromHour || hour > SignToHour) return; // waits for the day
            if (!sim.AckRabbitArrival()) return;
            int id = Field.Alive[0];
            var sp = Field.Sample(id, FieldNow);
            Vector2 from = SignStart(sp.x, sp.z);
            sign = new Sign { id = id, from = from };
            SignsPlayed++;
            // whoever stands nearest points at it
            pv.StandingNow(whoBuf, px, pz);
            int n = RabbitHerd.Nearest(px, pz, sp.x, sp.z);
            if (n >= 0)
            {
                sign.pointer = whoBuf[n]; LastPointer = sign.pointer;
                pv.Planner.React(KeeperGesture.Sighting, pv.Now, default, sign.pointer, 1f, SignSeconds);
                pv.LookAt(sign.pointer, new Vector2(sp.x, sp.z));
            }
            else LastPointer = PersonRef.None;
            var cam = Camera.main != null ? Camera.main.GetComponent<BoxCamera>() : null;
            if (cam != null) cam.NudgeToward(box.transform.TransformPoint(box.land.OnGround(sp.x, sp.z)));
        }

        /// <summary>Where the first rabbit comes in from: just past the meadow's edge, on the side where the grass ends.</summary>
        Vector2 SignStart(float x, float z)
        {
            float best = float.MaxValue; var bestAt = new Vector2(x + 0.5f, z);
            for (int k = 0; k < 16; k++)
            {
                float a = k / 16f * Mathf.PI * 2f; var d = new Vector2(Mathf.Sin(a), Mathf.Cos(a));
                for (float r = 0.1f; r <= 0.9f; r += 0.05f)
                {
                    var p = new Vector2(x, z) + d * r;
                    if (Mathf.Abs(p.x) > Land.HX - 0.1f || Mathf.Abs(p.y) > Land.HZ - 0.1f) break;
                    if (!Field.IsGreenAt(p.x, p.y)) { if (r < best) { best = r; bestAt = new Vector2(x, z) + d * Mathf.Min(0.9f, r + 0.35f); } break; }
                }
            }
            bestAt.x = Mathf.Clamp(bestAt.x, -Land.HX + 0.1f, Land.HX - 0.1f);
            bestAt.y = Mathf.Clamp(bestAt.y, -Land.HZ + 0.1f, Land.HZ - 0.1f);
            return bestAt;
        }

        // ---------------------------------------------------------------- the rabbits
        void UpdateRabbits(SimCore sim, float dt)
        {
            if (!Field.HasGreen) { HideRabbitsOnly(); return; }
            Field.Sync(sim.EcoRabbitsShown());
            double t = FieldNow;
            var pv = People;
            var hunts = HookHunts(pv, sim);
            if (pv != null && pv.Planner != null) pv.StandingNow(whoBuf, px, pz); else { whoBuf.Clear(); px.Clear(); pz.Clear(); }

            foreach (var sc in Scarers) { px.Add(sc.x); pz.Add(sc.y); }

            // leavers
            gone.Clear();
            foreach (var kv in slots) if (!IsAlive(kv.Key)) gone.Add(kv.Key);
            foreach (int id in gone) { Release(slots[id]); slots.Remove(id); }

            foreach (int id in Field.Alive)
            {
                if (!slots.TryGetValue(id, out var s))
                {
                    s = new Slot { id = id, doll = Rent(id) };
                    slots[id] = s;
                }
                s.grow = Mathf.MoveTowards(s.grow, 1f, dt / 0.7f);
                var basePose = Field.Sample(id, t);
                RabbitPose p;
                if (sign != null && sign.id == id) p = SignPose(basePose, dt, pv);
                else
                {
                    var hunt = hunts != null ? HuntOn(hunts, id, pv.Now.hour) : null;
                    if (hunt != null)
                    {   // #283: the rabbit the hunter stalks does not mind the hunter (nor the kid behind) until the strike
                        FilterOut(hunt);
                        p = s.scare.Step(dt, basePose, fpx, fpz, Field, t);
                        if (p.act == RabbitAct.Graze && NearAnyone(p.x, p.z, hunt, pv, StalkAlertRange)) p.act = RabbitAct.SitUp; // it looks up, and does not see
                    }
                    else p = s.scare.Step(dt, basePose, px, pz, Field, t);
                }
                var d = s.doll;
                d.gameObject.SetActive(true);
                var tr = d.transform;
                tr.localPosition = box.land.OnGround(p.x, p.z) + Vector3.up * p.y;
                d.Face(p.yaw * Mathf.Rad2Deg, dt, p.act == RabbitAct.Flee);
                d.Show(p.act, p.phase, (float)t, s.grow);
            }
            if (sign != null && !slots.ContainsKey(sign.id)) EndSign(pv);
        }

        // ---------------------------------------------------------------- a tap startles (#333)
        readonly List<RabbitHerd.Field.Startled> startleBuf = new();

        /// <summary>#333: a tap on the land at a screen spot (called by GlassVoice for a tap no person card took). Returns how many rabbits
        /// it startled (0 when no rabbit is near, or the spot is not on the land). Never removes one.</summary>
        /// <summary>Tester (batch 2): a rabbit's doll is right under the finger (within a fingertip on screen, about 0.03 of the screen's
        /// height). Then the tap is the rabbit's, before a piece's or a person's card (a rabbit by the fire opened "The fire").</summary>
        public bool RabbitUnderScreen(Vector2 screenPos)
        {
            var cam = Camera.main;
            if (cam == null) return false;
            float r = 0.03f * Screen.height;
            foreach (var kv in slots)
            {
                var d = kv.Value.doll;
                if (d == null || !d.gameObject.activeInHierarchy) continue;
                var sp = cam.WorldToScreenPoint(d.transform.position);
                if (sp.z > 0f && ((Vector2)sp - screenPos).sqrMagnitude <= r * r) return true;
            }
            return false;
        }

        public int TapAtScreen(Vector2 screenPos)
        {
            var shaping = ShapingView.Instance;
            if (shaping == null || !shaping.GroundPoint(screenPos, out float x, out float z)) return 0;
            return TapAtGround(x, z);
        }

        /// <summary>#333: the tap, in box-local ground coordinates. The rabbit tapped (and the ones near) freeze with their ears up, then
        /// bolt to another part of the meadow and settle there (<see cref="RabbitHerd.Field.Startle"/> picks where). A rabbit the hunter
        /// is stalking and the first rabbit during its sign are left alone.</summary>
        public int TapAtGround(float x, float z)
        {
            var sim = Sim;
            if (sim == null || !sim.EcologyOn || !Field.HasGreen || slots.Count == 0) return 0;
            var pv = People;
            var hunts = pv != null && pv.Planner != null ? pv.Planner.HuntsOn(pv.Now.day) : null;
            float hour = pv != null ? pv.Now.hour : 0f;
            int n = Field.Startle(x, z, FieldNow, id => (sign != null && sign.id == id) || (hunts != null && HuntOn(hunts, id, hour) != null), startleBuf);
            foreach (var st in startleBuf)
            {
                if (!slots.TryGetValue(st.id, out var s) || s.doll == null) continue;
                var at = s.doll.transform.localPosition;
                s.scare.Hold(at.x, at.z, x, z, st.until, RabbitHerd.Scare.FreezeSeconds);
            }
            return n;
        }

        /// <summary>Debug ("Startle all"): every rabbit freezes and runs to a new place, as if tapped one after the other.</summary>
        public int DebugStartleAll()
        {
            int total = 0;
            var ids = new List<int>(Field.Alive);
            foreach (int id in ids)
                if (TryGetPosition(id, out var p)) total += TapAtGround(p.x - 0.05f, p.z);
            return total;
        }

        /// <summary>Debug ("Spawn rabbits here"): adds n rabbits (sim count) and sets them down around the ground under the middle of the
        /// screen. Returns a status line.</summary>
        public string DebugSpawnHere(int n)
        {
            var sim = Sim;
            if (sim == null || !sim.EcologyOn) return "living land off";
            sim.DebugAddRabbits(n);
            Field.Sync(sim.EcoRabbitsShown());
            var shaping = ShapingView.Instance;
            float x = 0f, z = 0f;
            if (shaping == null || !shaping.GroundPoint(new Vector2(Screen.width * 0.5f, Screen.height * 0.5f), out x, out z)) return "no ground under the screen's middle";
            int cell = SiteMap.CellAt(x, z);
            if (!Field.IsGreenAt(x, z)) return "no green under the screen's middle";
            int k = 0;
            for (int i = Field.Alive.Count - 1; i >= 0 && k < n; i--, k++) Field.Rehome(Field.Alive[i], cell, FieldNow);
            return $"{k} rabbits set down here (shown {Field.Alive.Count})";
        }

        // ---------------------------------------------------------------- the hunt (#283)
        /// <summary>The hunter looks like a threat to the stalked rabbit only from the strike on; this near it sits up and looks about.</summary>
        const float StalkAlertRange = 0.8f;
        readonly List<float> fpx = new(), fpz = new();

        /// <summary>Gives the planner this view's field, settles the hunts whose strike is over (a catch takes the rabbit out of the
        /// field and the sim count) and returns the day's hunts (null when there is nothing to hunt with).</summary>
        IReadOnlyList<HuntPlan> HookHunts(PeopleView pv, SimCore sim)
        {
            if (pv == null || pv.Planner == null) return null;
            var pl = pv.Planner;
            if (!ReferenceEquals(pl.Rabbits, Field))
            {
                if (Field.Alive.Count == 0) return null; // a day planned with no rabbits would stay without a hunt
                pl.Rabbits = Field;
            }
            pl.ResolveHunts(pv.Now);
            return pl.HuntsOn(pv.Now.day);
        }

        static HuntPlan HuntOn(IReadOnlyList<HuntPlan> hunts, int id, float hour)
        {
            for (int i = 0; i < hunts.Count; i++)
            {
                var h = hunts[i];
                if (h.rabbitId != id || h.resolved || h.steps.Count < 3) continue;
                if (hour >= h.steps[2].start && hour < h.resolveHour) return h;
            }
            return null;
        }

        void FilterOut(HuntPlan hunt)
        {
            // #333 review: the hunted rabbit minds nobody (not only the hunter and the kid) until the strike: the hunt is planned on its spot,
            // and since rabbits spread over the whole meadow, a passer-by near it would send it off and leave the hunter striking at grass.
            fpx.Clear(); fpz.Clear();
            for (int i = whoBuf.Count; i < px.Count; i++) { fpx.Add(px[i]); fpz.Add(pz[i]); } // the ghost scarers
        }

        bool NearAnyone(float x, float z, HuntPlan hunt, PeopleView pv, float range)
        {
            for (int i = 0; i < whoBuf.Count && i < px.Count; i++)
            {
                if (whoBuf[i] != hunt.hunter) continue;
                float dx = px[i] - x, dz = pz[i] - z;
                return dx * dx + dz * dz < range * range;
            }
            return false;
        }

        bool IsAlive(int id) { var a = Field.Alive; for (int i = 0; i < a.Count; i++) if (a[i] == id) return true; return false; }

        RabbitPose SignPose(RabbitPose basePose, float dt, PeopleView pv)
        {
            sign.t += dt;
            float u = Mathf.Clamp01(sign.t / SignSeconds);
            float e = u * u * (3f - 2f * u);
            var to = new Vector2(basePose.x, basePose.z);
            var at = Vector2.Lerp(sign.from, to, e);
            var p = basePose;
            p.x = at.x; p.z = at.y;
            float dist = (to - sign.from).magnitude;
            int hops = Mathf.Max(4, Mathf.CeilToInt(dist / RabbitHerd.HopLen));
            float h = u * hops; float ph = h - Mathf.Floor(h);
            if (u < 0.97f)
            {
                p.act = RabbitAct.Hop; p.phase = ph; p.y = Mathf.Sin(Mathf.PI * ph) * RabbitHerd.HopArc;
                var dir = to - sign.from; p.yaw = Mathf.Atan2(dir.x, dir.y);
            }
            if (pv != null && !sign.pointer.IsNone) pv.LookAt(sign.pointer, at);
            if (sign.t >= SignSeconds) EndSign(pv);
            return p;
        }

        void EndSign(PeopleView pv)
        {
            if (pv != null && sign != null && !sign.pointer.IsNone) pv.ClearLookAt(sign.pointer);
            sign = null;
        }

        void HideRabbitsOnly()
        {
            foreach (var s in slots.Values) Release(s);
            slots.Clear();
        }

        RabbitDoll Rent(int id)
        {
            int variant = (int)(RabbitHerd.H((uint)Field.Seed, (uint)id, 5) % RabbitDoll.Variants);
            for (int i = pool.Count - 1; i >= 0; i--)
            {
                if (pool[i] == null) { pool.RemoveAt(i); continue; }
                if (pool[i].Variant != variant) continue;
                var d = pool[i]; pool.RemoveAt(i);
                d.transform.SetParent(Holder(), false);
                return d;
            }
            return RabbitDoll.Create(box, Holder(), variant);
        }

        void Release(Slot s)
        {
            if (s.doll == null) return;
            s.doll.gameObject.SetActive(false);
            pool.Add(s.doll);
        }

        // ---------------------------------------------------------------- tests and the debug panel
        /// <summary>Where a rabbit doll stands now (box-local), false when it isn't shown.</summary>
        public bool TryGetPosition(int id, out Vector3 local)
        {
            local = default;
            if (!slots.TryGetValue(id, out var s) || s.doll == null || !s.doll.gameObject.activeSelf) return false;
            local = s.doll.transform.localPosition; return true;
        }

        public bool TryGetAct(int id, out bool fleeing)
        {
            fleeing = false;
            if (!slots.TryGetValue(id, out var s)) return false;
            fleeing = s.scare.Fleeing; return true;
        }

        /// <summary>The nearest shown rabbit to a box-local spot, or -1.</summary>
        public int NearestShown(float x, float z)
        {
            int best = -1; float bd = float.MaxValue;
            foreach (var kv in slots)
            {
                if (kv.Value.doll == null || !kv.Value.doll.gameObject.activeSelf) continue;
                var p = kv.Value.doll.transform.localPosition;
                float d = (p.x - x) * (p.x - x) + (p.z - z) * (p.z - z);
                if (d < bd) { bd = d; best = kv.Key; }
            }
            return best;
        }
    }
}
