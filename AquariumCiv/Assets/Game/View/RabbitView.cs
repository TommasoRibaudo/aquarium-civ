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

        /// <summary>P9 #424 (B12.b): the placed pair's arrival lasts this many seconds: they drop lightly from above the rim (0.5 s), land
        /// with a bounce and a grass puff, shake off, sit up and look round, then hop off into their day. (Was the first rabbit hopping
        /// in from the meadow's edge while a person pointed, #282; the people's reaction is LifeArrivalView's now.)</summary>
        public const float SignSeconds = 3.4f;
        public const float DropSeconds = 0.5f, DropHeight = 0.4f, BounceSeconds = 0.25f, ShakeSeconds = 0.7f, LookSeconds = 1.2f;
        /// <summary>Kept for old callers: the arrival plays at any hour now (B12.0: the people's reaction waits for dawn, not the arrival).</summary>
        public const float SignFromHour = 0f, SignToHour = 24f;

        /// <summary>The rabbits' shared field: the hunt planner takes this one (never its own).</summary>
        public RabbitHerd.Field Field { get; } = new RabbitHerd.Field();

        BoxView box;
        PeopleView people;
        Transform holder;
        Transform rootSeen;
        readonly List<RabbitDoll> pool = new();
        sealed class Slot
        {
            public RabbitDoll doll; public RabbitHerd.Scare scare = new(); public float grow; public int id;
            /// <summary>#362: this rabbit was hit by a thrown spear and lies where it fell until the hunter lifts it; a bolt the hunt set off
            /// (the first bound throws up grass).</summary>
            public HuntPlan fallenPlan; public bool huntBolt, wasRunning;
            /// <summary>#400: where the doll stood last frame (box-local x, z), for <see cref="BoundsInPlace"/>.</summary>
            public Vector2 lastAt; public bool hasLast;
        }
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
        sealed class Sign { public int id; public float t; public Vector2 from; public PersonRef pointer; public readonly List<int> ids = new(); public readonly Dictionary<int, RabbitPose> at = new(); public bool landed; }
        Sign sign;

        /// <summary>Rabbit dolls shown now, and how many are fleeing, for tests and the debug panel.</summary>
        public int Shown { get { int n = 0; foreach (var s in slots.Values) if (s.doll != null && s.doll.gameObject.activeSelf) n++; return n; } }
        public int Fleeing { get { int n = 0; foreach (var s in slots.Values) if (s.scare.Fleeing) n++; return n; } }
        public bool TracksShown => tracks != null && tracks.activeSelf;
        public bool SignPlaying => sign != null;
        public int SignsPlayed { get; private set; }
        /// <summary>#400 (playtest: "jump up and down on the same spot"): frames a rabbit was shown in the air in a hop or a bound without
        /// moving over the ground. Should stay 0; for tests and the tester.</summary>
        public int BoundsInPlace { get; private set; }
        /// <summary>Who pointed at the first rabbit (none before it), and whether they were really in the pointing activity.</summary>
        public PersonRef LastPointer => LifeArrivalView.Instance != null ? LifeArrivalView.Instance.LastPointer : PersonRef.None; // #424: the arrival view picks who points
        public int SignRabbitId => sign != null ? sign.id : -1;
        /// <summary>P9 #424: rabbits in the arrival's drop now (the pair), and how far it is (0..1).</summary>
        public int Dropping => sign != null ? sign.ids.Count : 0;
        public float DropProgress => sign != null ? Mathf.Clamp01(sign.t / SignSeconds) : 0f;
        /// <summary>P9 #424, the debug panel's "replay the rabbits' arrival": the drop plays again for the rabbits shown now.</summary>
        public bool ReplayDrop()
        {
            if (Field.Alive.Count == 0) return false;
            if (sign != null) EndSign(People);
            StartDrop();
            return true;
        }

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
            // the green changes with the mists too, and the village builds on it (#400): refresh when the rabbits' ground changed
            int h = sim.EcoRabbitGroundKey();
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
            if (!sim.AckRabbitArrival()) return;
            StartDrop();
        }

        /// <summary>B12.b: the pair (every rabbit shown, at most two) drops where the field has them now; the people's reaction, the
        /// camera's nudge and the line are LifeArrivalView's.</summary>
        void StartDrop()
        {
            int id = Field.Alive[0];
            sign = new Sign { id = id };
            double now = FieldNow;
            foreach (int r in Field.Alive)
            {
                if (sign.ids.Count >= 2) break;
                sign.ids.Add(r);
                sign.at[r] = Field.Sample(r, now);
            }
            var sp = sign.at[id];
            sign.from = new Vector2(sp.x, sp.z);
            SignsPlayed++;
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
            HuntEvents(hunts, pv);
            if (pv != null && pv.Planner != null) pv.StandingNow(whoBuf, px, pz); else { whoBuf.Clear(); px.Clear(); pz.Clear(); }

            foreach (var sc in Scarers) { px.Add(sc.x); pz.Add(sc.y); }

            // leavers
            gone.Clear();
            foreach (var kv in slots) if (!IsAlive(kv.Key) && !Lies(kv.Value, pv)) gone.Add(kv.Key);
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
                float roll = 0f;
                if (sign != null && sign.ids.Contains(id)) p = DropPose(id, basePose, out roll);
                else
                {
                    var hunt = hunts != null ? HuntOn(hunts, id, pv.Now.hour) : null;
                    if (hunt != null)
                    {   // #283: the rabbit the hunter stalks does not mind the hunter (nor the kid behind) until the strike
                        FilterOut(hunt);
                        // #362: from the strike on it stands where the plan aimed (the planner waited for it not to hop), grazing
                        double hold = HuntClock.FieldSeconds(hunt.day, hunt.strikeStart);
                        if (t > hold) { basePose = Field.Sample(id, hold); basePose.act = basePose.act == RabbitAct.Hop ? RabbitAct.Graze : basePose.act; basePose.y = 0f; basePose.phase = 0f; }
                        p = s.scare.Step(dt, basePose, fpx, fpz, Field, t);
                        // #362: it sits up when the PLAN says so (the hunter freezes at the same moment), not when he happens to be near
                        if (p.act == RabbitAct.Graze && StalkFreeze(hunt, pv.Now.hour)) p.act = RabbitAct.SitUp;
                    }
                    else p = s.scare.Step(dt, basePose, px, pz, Field, t);
                }
                // #400: kept off its day with no way back (or people stay there): it makes its home where it stands
                if (s.scare.TakeStranded(out float stX, out float stZ)) Field.Rehome(id, SiteMap.CellAt(stX, stZ), t);
                var d = s.doll;
                d.gameObject.SetActive(true);
                var tr = d.transform;
                tr.localPosition = box.land.OnGround(p.x, p.z) + Vector3.up * p.y;
                var at = new Vector2(p.x, p.z);
                if (s.hasLast && dt > 0f && !(sign != null && sign.ids.Contains(id)) && (p.act == RabbitAct.Hop || p.act == RabbitAct.Flee) && p.y > 0.002f && (at - s.lastAt).sqrMagnitude < 1e-10f) BoundsInPlace++;
                s.lastAt = at; s.hasLast = true;
                d.Roll(roll);
                d.Ground(GroundNormal(p.x, p.z));
                d.Face(p.yaw * Mathf.Rad2Deg, dt, p.act == RabbitAct.Flee);
                d.Show(p.act, p.phase, (float)t, s.grow);
                bool running = s.scare.Running;
                if (running && !s.wasRunning && s.huntBolt && HitFx.Enabled)
                    HitFx.Puff(PuffKind.Grass, box.Root.TransformPoint(tr.localPosition), Vector3.up, 0.8f);   // #362: the first bound
                if (!running) s.huntBolt = s.huntBolt && s.scare.Holding;
                s.wasRunning = running;
            }
            ShowFallen(pv, t, dt);
            if (sign != null)
            {
                sign.t += dt;
                if (!sign.landed && sign.t >= DropSeconds)
                {
                    sign.landed = true;
                    if (HitFx.Enabled) foreach (int r in sign.ids) if (sign.at.TryGetValue(r, out var q)) HitFx.Puff(PuffKind.Grass, box.Root.TransformPoint(box.land.OnGround(q.x, q.z)), Vector3.up, 0.9f, true);
                }
                if (sign.t >= SignSeconds || !slots.ContainsKey(sign.id)) EndSign(pv);
            }
        }

        // ---------------------------------------------------------------- a knock on the glass startles (#333)
        readonly List<RabbitHerd.Field.Startled> startleBuf = new();

        /// <summary>On screen, a finger this close to a rabbit's doll is ON it (a fraction of the screen's height, about a fingertip).</summary>
        public const float OnRabbitScreen = 0.025f;
        /// <summary>On screen, the rabbits within this of a knock on the glass hear it and run (a fraction of the screen's height).</summary>
        public const float KnockScreenReach = 0.10f;
        readonly List<int> knockIds = new();

        /// <summary>#333 (the user, 2026-11-05): a knock on the GLASS close to rabbits makes the close ones run off; a tap ON a rabbit
        /// does nothing to it (nor does a tap on the land through the open top: GlassVoice only calls this for a knock). "Close" is what
        /// the player sees: the rabbits near the finger on screen. They run away from the land the finger points at. Returns how many.</summary>
        /// <summary>Debug: what a knock at this screen point would reach (the dolls' screen distances), without startling anything.</summary>
        public string DebugKnockReach(Vector2 screenPos)
        {
            var cam = Camera.main;
            if (cam == null) return "no camera";
            var parts = new List<string>();
            foreach (var kv in slots)
            {
                var d = kv.Value.doll;
                if (d == null || !d.gameObject.activeInHierarchy) continue;
                var sp = cam.WorldToScreenPoint(d.transform.position);
                parts.Add($"{kv.Key}:{((Vector2)sp - screenPos).magnitude:0}");
            }
            return $"on {OnRabbitScreen * Screen.height:0} reach {KnockScreenReach * Screen.height:0} [{string.Join(" ", parts)}]";
        }

        public int KnockAtScreen(Vector2 screenPos)
        {
            var cam = Camera.main;
            if (cam == null || box == null) return 0;
            float on = OnRabbitScreen * Screen.height, reach = KnockScreenReach * Screen.height;
            knockIds.Clear();
            foreach (var kv in slots)
            {
                var d = kv.Value.doll;
                if (d == null || !d.gameObject.activeInHierarchy) continue;
                var sp = cam.WorldToScreenPoint(d.transform.position);
                if (sp.z <= 0f) continue;
                float dist = ((Vector2)sp - screenPos).magnitude;
                if (dist <= on) return 0; // the finger is on a rabbit: nothing
                if (dist <= reach) knockIds.Add(kv.Key);
            }
            if (knockIds.Count == 0) return 0;
            float fx, fz;
            var shaping = ShapingView.Instance;
            if (shaping == null || !shaping.GroundPoint(screenPos, out fx, out fz))
            {
                var care = CareView.Instance;
                if (care == null || !care.GlassPointAt(screenPos, out var world)) return 0;
                var local = box.transform.InverseTransformPoint(world);
                fx = local.x; fz = local.z;
            }
            return Scatter(fx, fz, knockIds);
        }

        /// <summary>#333: a knock whose spot on the glass is above (x, z) (box-local ground). The rabbits within
        /// <see cref="RabbitHerd.KnockRadius"/> freeze with their ears up, then bolt to another part of the meadow and settle there
        /// (<see cref="RabbitHerd.Field.Knock"/> picks where). A rabbit the hunter is stalking and the first rabbit during its sign are
        /// left alone.</summary>
        public int KnockAtGround(float x, float z) => Scatter(x, z, null, true);

        int Scatter(float x, float z, List<int> ids) => Scatter(x, z, ids, true);

        int Scatter(float x, float z, List<int> ids, bool knock)
        {
            var sim = Sim;
            if (sim == null || !sim.EcologyOn || !Field.HasGreen || slots.Count == 0) return 0;
            var pv = People;
            var hunts = pv != null && pv.Planner != null ? pv.Planner.HuntsOn(pv.Now.day) : null;
            float hour = pv != null ? pv.Now.hour : 0f;
            System.Func<int, bool> skip = id => (sign != null && sign.id == id) || (hunts != null && HuntOn(hunts, id, hour) != null);
            int n = ids != null ? Field.StartleThese(ids, x, z, FieldNow, skip, startleBuf) : knock ? Field.Knock(x, z, FieldNow, skip, startleBuf) : Field.Startle(x, z, FieldNow, skip, startleBuf);
            foreach (var st in startleBuf)
            {
                if (!slots.TryGetValue(st.id, out var s) || s.doll == null) continue;
                if (huntScatter) s.huntBolt = true;
                var at = s.doll.transform.localPosition;
                s.scare.Hold(at.x, at.z, x, z, st.until, RabbitHerd.Scare.NoticeSeconds, SiteMap.CXc(st.refuge), SiteMap.CZc(st.refuge));
            }
            return n;
        }

        /// <summary>Debug ("Startle all"): every rabbit freezes and runs to a new place, one after the other.</summary>
        public int DebugStartleAll()
        {
            int total = 0;
            var ids = new List<int>(Field.Alive);
            foreach (int id in ids)
                if (TryGetPosition(id, out var p)) total += Scatter(p.x - 0.05f, p.z, null, false);
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
            settledBuf.Clear();
            pl.ResolveHunts(pv.Now, settledBuf);
            foreach (var plan in settledBuf)   // #362: a hit rabbit stays where it fell until the hunter lifts it (the sim already counts it gone)
                if (plan.caught && slots.TryGetValue(plan.rabbitId, out var fs)) fs.fallenPlan = plan;
            return pl.HuntsOn(pv.Now.day);
        }

        // ---------------------------------------------------------------- #362: the thrown spear's effect on the rabbits
        readonly List<HuntPlan> settledBuf = new();
        readonly Dictionary<HuntPlan, int> huntFired = new();
        bool huntScatter;

        /// <summary>Is the hunted rabbit sitting up now (the hunter freezes at the same moment)?</summary>
        static bool StalkFreeze(HuntPlan hunt, float hour)
        {
            if (hunt.steps.Count < 4) return false;
            var st = hunt.steps[3];
            if (st.phase != HuntPhase.Stalk || hour < st.start || hour >= st.end) return false;
            return HuntStalk.RabbitUp(LifeClock.RealSecondsBetween(st.start, hour), hunt.freezeA, hunt.freezeB);
        }

        /// <summary>The spear leaves the hand: every other rabbit near the target bolts (at the throw, not before). The spear lands:
        /// on a miss the target freezes and zigzags off; a hit rabbit falls (<see cref="ShowFallen"/>).</summary>
        void HuntEvents(IReadOnlyList<HuntPlan> hunts, PeopleView pv)
        {
            if (hunts == null || pv == null) return;
            if (huntFired.Count > 24) huntFired.Clear();
            for (int i = 0; i < hunts.Count; i++)
            {
                var plan = hunts[i];
                if (plan.steps.Count < 5 || plan.steps[4].phase != HuntPhase.Strike) continue;
                double rel = HuntClock.FieldSeconds(pv.Now.day, pv.Now.hour) - HuntClock.FieldSeconds(plan.day, plan.steps[4].start);
                if (rel < HuntTiming.ReleaseAt) continue;
                huntFired.TryGetValue(plan, out int fired);
                if ((fired & 1) == 0)
                {
                    huntFired[plan] = fired |= 1;
                    if (rel < HuntTiming.ReleaseAt + 2.5) { huntScatter = true; Scatter(plan.targetX, plan.targetZ, null, true); huntScatter = false; }
                }
                if ((fired & 2) == 0 && rel >= HuntTiming.LandAt && plan.resolved)
                {
                    huntFired[plan] = fired |= 2;
                    if (rel < HuntTiming.LandAt + 2.5 && !plan.caught) ScatterTarget(plan);
                }
            }
        }

        /// <summary>A miss: the target rabbit freezes (ears up, a flinch), then bolts away from the hunter.</summary>
        void ScatterTarget(HuntPlan plan)
        {
            var sim = Sim;
            if (sim == null || !sim.EcologyOn || !Field.HasGreen || !slots.ContainsKey(plan.rabbitId)) return;
            huntIds.Clear(); huntIds.Add(plan.rabbitId);
            Field.StartleThese(huntIds, plan.lungeX, plan.lungeZ, FieldNow, null, startleBuf);
            foreach (var st in startleBuf)
            {
                if (!slots.TryGetValue(st.id, out var s) || s.doll == null) continue;
                s.huntBolt = true;
                var at = s.doll.transform.localPosition;
                s.scare.Hold(at.x, at.z, plan.lungeX, plan.lungeZ, st.until, RabbitHerd.Scare.NoticeSeconds, SiteMap.CXc(st.refuge), SiteMap.CZc(st.refuge));
            }
        }
        readonly List<int> huntIds = new();

        /// <summary>This slot's rabbit was hit and lies where it fell, until the hunter has lifted it.</summary>
        bool Lies(Slot s, PeopleView pv)
        {
            if (s.fallenPlan == null) return false;
            if (pv != null && (pv.Now.day != s.fallenPlan.day || pv.Now.hour >= s.fallenPlan.takeHour)) { s.fallenPlan = null; return false; }
            return true;
        }

        /// <summary>The hit rabbit: it jumps 0.05 u and falls on its side (roll 90 degrees over 0.25 s), limp, where the spear came down.</summary>
        void ShowFallen(PeopleView pv, double t, float dt)
        {
            foreach (var kv in slots)
            {
                var s = kv.Value;
                if (s.fallenPlan == null || s.doll == null || !Lies(s, pv)) continue;
                var plan = s.fallenPlan;
                double landAt = HuntClock.FieldSeconds(plan.day, plan.steps[4].start) + HuntTiming.LandAt;
                float u = Mathf.Clamp01((float)(t - landAt) / HuntTiming.FallSeconds);
                float e = u * u * (3f - 2f * u);
                var d = s.doll;
                d.gameObject.SetActive(true);
                d.transform.localPosition = box.land.OnGround(plan.targetX, plan.targetZ) + Vector3.up * (0.05f * 4f * u * (1f - u) + 0.018f * e);
                d.Roll(90f * e);
                d.Ground(GroundNormal(plan.targetX, plan.targetZ));
                d.Face(Mathf.Atan2(plan.targetX - plan.lungeX, plan.targetZ - plan.lungeZ) * Mathf.Rad2Deg + 70f, dt);
                d.Show(RabbitAct.Flee, 0.5f, (float)t, s.grow);
            }
        }

        static HuntPlan HuntOn(IReadOnlyList<HuntPlan> hunts, int id, float hour)
        {
            for (int i = 0; i < hunts.Count; i++)
            {
                var h = hunts[i];
                if (h.rabbitId != id || h.resolved || h.steps.Count < 3) continue;
                if (hour >= h.steps[0].start && hour < h.resolveHour) return h; // from the walk-out on: spooked by a passer-by then, it was gone before the stalk
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

        /// <summary>The ground's normal at (x, z), box-local, over a rabbit's length (so a single bump does not tip it).</summary>
        Vector3 GroundNormal(float x, float z)
        {
            const float e = 0.04f;
            var land = box.land;
            float hx = land.OnGround(x + e, z).y - land.OnGround(x - e, z).y;
            float hz = land.OnGround(x, z + e).y - land.OnGround(x, z - e).y;
            return new Vector3(-hx, 2f * e, -hz).normalized;
        }

        bool IsAlive(int id) { var a = Field.Alive; for (int i = 0; i < a.Count; i++) if (a[i] == id) return true; return false; }

        /// <summary>B12.b, one rabbit of the dropping pair: falls 0.4 u (ease in), lands with a 35 % bounce, shakes off (roll 12 deg,
        /// 8 Hz, dying away), sits up and looks round, then hops from where it landed into the field's pose.</summary>
        RabbitPose DropPose(int id, RabbitPose basePose, out float roll)
        {
            roll = 0f;
            float t = sign.t;
            var p = sign.at.TryGetValue(id, out var landed) ? landed : basePose;
            if (sign.ids.IndexOf(id) == 1) t -= 0.18f; // the second lands a beat after the first
            p.act = RabbitAct.SitUp; p.phase = 0f; p.y = 0f;
            if (t < DropSeconds)
            {
                float u = Mathf.Clamp01(t / DropSeconds);
                p.y = DropHeight * (1f - u * u); // ease in: falling faster
                p.act = RabbitAct.Hop; p.phase = 0.5f;
                return p;
            }
            t -= DropSeconds;
            if (t < BounceSeconds) { float u = t / BounceSeconds; p.y = Mathf.Sin(Mathf.PI * u) * DropHeight * 0.35f * 0.25f; p.act = RabbitAct.Hop; p.phase = u; return p; }
            t -= BounceSeconds;
            if (t < ShakeSeconds) { roll = 12f * Mathf.Sin(t * 8f * Mathf.PI * 2f) * Mathf.Exp(-t / 0.3f); p.act = RabbitAct.Graze; return p; }
            t -= ShakeSeconds;
            if (t < LookSeconds) { p.yaw += Mathf.Sin(t / LookSeconds * Mathf.PI * 2f) * 0.6f; return p; } // sits up, looks round
            t -= LookSeconds;
            float k = Mathf.Clamp01(t / Mathf.Max(0.05f, SignSeconds - DropSeconds - BounceSeconds - ShakeSeconds - LookSeconds));
            var a = new Vector2(p.x, p.z); var b = new Vector2(basePose.x, basePose.z);
            var q = basePose;
            var at = Vector2.Lerp(a, b, k);
            q.x = at.x; q.z = at.y;
            if ((b - a).sqrMagnitude > 1e-4f) { q.act = RabbitAct.Hop; q.phase = k; q.y = Mathf.Sin(Mathf.PI * k) * RabbitHerd.HopArc; q.yaw = Mathf.Atan2(b.x - a.x, b.y - a.y); }
            return q;
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

        /// <summary>Debug: a rabbit's scare state and its scheduled (home) pose.</summary>
        public string DebugScare(int id)
        {
            if (!slots.TryGetValue(id, out var s)) return "none";
            var b = Field.Sample(id, FieldNow);
            return $"{s.scare.Debug} base ({b.x:0.00},{b.z:0.00}) {b.act}";
        }

        /// <summary>The rabbit is on its way somewhere (a run, or hopping back into its day), so it may be crossing bare earth.</summary>
        /// <summary>Startled by a knock (frozen to run, or running), not just shy of a passer-by.</summary>
        public bool IsStartled(int id) => slots.TryGetValue(id, out var s) && (s.scare.Holding || s.scare.Running);

        public bool InTransit(int id) => slots.TryGetValue(id, out var s) && s.scare.InTransit;

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
