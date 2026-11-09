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
    /// P9 #427 (animation-specs B14): living with the deer.
    /// <list type="bullet">
    /// <item><b>Dawn at the pond</b> (B14.a): on about every third day with deer and a pond, while the does drink at first light, whoever
    /// is up near the water stops and watches; a kid near crouches and holds very still; a grown-up holds a finger up ("shh"). At the end the
    /// kid looks up at the grown-up, who nods. Nobody hunts at the water (the planner's rule, LifePlanner.DeerScene.cs).</item>
    /// <item><b>The white stag</b> (B14.b): while its choice waits (SimCore.WhiteStagChoiceDue) it stands at the wood's edge, still, looking
    /// at the camp, a gold glint in the eye, and the people near turn to it. "Hunt it": the hunters go after it (B13.a with the white
    /// stag, LifePlanner.StartWhiteStagHunt; the feast and the great antlers are DeerHuntStage's). "Let it be": it walks slowly back into
    /// the trees; from then on it shows at the back on about one dawn in five, and a kid points at it.</item>
    /// </list>
    /// The people are held through <see cref="PeopleView.LifeSpot"/> / <see cref="PeopleView.LifePose"/> (view only: their day goes on and
    /// they walk back into it when the scene ends).
    /// </summary>
    [DefaultExecutionOrder(9)]
    public class DeerScenesView : MonoBehaviour
    {
        public static bool AutoCreate = true;
        public static DeerScenesView Instance { get; private set; }

        /// <summary>The dawn scene: on one day in this many, this long (real seconds) at most; the watchers come from this far; a kid this
        /// near holds still.</summary>
        public const int DawnEvery = 3, WhiteDawnEvery = 5;
        public const float DawnSeconds = 25f, DawnReach = 2f, KidReach = 1f, ShhAt = 3f, ShhSeconds = 1.2f, NodSeconds = 2.5f;
        /// <summary>The white stag leaving into the trees (let it be): how far, how fast.</summary>
        public const float LeaveRun = 0.9f, LeaveSpeed = 0.1f;
        /// <summary>The white stag stands at least this far from the fire (the wood's edge, never in the camp).</summary>
        public const float MinOut = 1.0f;

        BoxView box;
        Transform holder, rootSeen;
        QuadDoll white;
        Vector3 whiteAt; float whiteYaw, whiteT;
        WhiteStag lastState = WhiteStag.None;
        bool leaving, huntWanted; float leaveT; Vector3 leaveFrom, leaveTo;
        DeerHuntPlan whiteHunt;
        long forcedDawn = -1, playedDawn = -1, forcedWhiteDawn = -1;
        readonly List<Vector3> drinking = new();

        sealed class Member { public PersonRef who; public Vector2 spot, face; public int role; } // role 0 watcher, 1 kid, 2 shusher
        readonly Dictionary<int, Member> cast = new();
        float dawnStart = -1f; Vector2 dawnDeer; Member kid, shusher;

        // ---------------------------------------------------------------- tests and the panel
        public int DawnScenes { get; private set; }
        public int DawnWatchers => cast.Count;
        public bool DawnPlaying => dawnStart >= 0f;
        public bool KidHolding => kid != null && dawnStart >= 0f;
        public int WhiteShows { get; private set; }
        public int WhiteHunts { get; private set; }
        public int WhiteLetGo { get; private set; }
        public int WhiteDawns { get; private set; }
        public bool WhiteShown => white != null && white.go.activeSelf;
        public Vector3 WhiteAt => whiteAt;
        public DeerHuntPlan WhiteHunt => whiteHunt;
        public string LastWhy { get; private set; } = "";

        SimCore Sim => GameClock.Instance != null ? GameClock.Instance.Sim : null;

        public static DeerScenesView Ensure(BoxView b)
        {
            if (b == null) return null;
            var v = b.GetComponent<DeerScenesView>();
            return v != null ? v : b.gameObject.AddComponent<DeerScenesView>();
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
            PeopleView.LifeSpot = SpotHook;
            PeopleView.LifePose = PoseHook;
        }

        void OnDisable()
        {
            if (Instance == this) Instance = null;
            if (PeopleView.LifeSpot == (PeopleView.StagedSpotHook)SpotHook) PeopleView.LifeSpot = null;
            if (PeopleView.LifePose == (PeopleView.StagedPoseHook)PoseHook) PeopleView.LifePose = null;
            white?.Destroy(); white = null;
            if (holder != null) Destroy(holder.gameObject);
            holder = null;
            EndDawn();
        }

        Transform Holder()
        {
            if (holder == null)
            {
                holder = new GameObject("Deer scenes") { hideFlags = HideFlags.DontSave }.transform;
                holder.SetParent(box.Root, false);
            }
            return holder;
        }

        static uint Hash(long day, int seed, uint salt) => RabbitHerd.H((uint)seed, (uint)(day & 0x7fffffff), salt);

        /// <summary>Is <paramref name="day"/> a dawn the people watch the does (about one in <see cref="DawnEvery"/>, or the panel's)?</summary>
        public bool DawnDay(long day, int seed) => day == forcedDawn || Hash(day, seed, 427) % DawnEvery == 0;
        /// <summary>Does the let-be white stag show at the back this dawn (about one in <see cref="WhiteDawnEvery"/>)?</summary>
        public bool WhiteDawnDay(long day, int seed) => day == forcedWhiteDawn || Hash(day, seed, 428) % WhiteDawnEvery == 0;

        void Update()
        {
            var sim = Sim;
            var pv = PeopleView.Find();
            if (sim == null || !sim.EcologyOn || box == null || box.Root == null || pv == null || pv.Planner == null) { HideWhite(); EndDawn(); return; }
            if (rootSeen != box.Root) { white = null; holder = null; rootSeen = box.Root; }
            var now = pv.Now;
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.1f);
            WhiteStagStep(sim, pv, now, dt);
            Dawn(sim, pv, now);
        }

        // ---------------------------------------------------------------- B14.b the white stag
        void WhiteStagStep(SimCore sim, PeopleView pv, ViewTime now, float dt)
        {
            var state = sim.WhiteStagState;
            if (lastState == WhiteStag.Seen && state == WhiteStag.Hunted) huntWanted = true;
            if (lastState == WhiteStag.Seen && state == WhiteStag.LetBe && WhiteShown)
            {
                leaving = true; leaveT = 0f; leaveFrom = whiteAt; WhiteLetGo++;
                var away = new Vector2(whiteAt.x - sim.EcoGround.campX, whiteAt.z - sim.EcoGround.campZ);
                if (away.sqrMagnitude < 1e-4f) away = Vector2.right;
                away.Normalize();
                float tx = whiteAt.x + away.x * LeaveRun, tz = whiteAt.z + away.y * LeaveRun;
                tx = Mathf.Clamp(tx, -Land.HX + 0.15f, Land.HX - 0.15f); tz = Mathf.Clamp(tz, -Land.HZ + 0.15f, Land.HZ - 0.15f);
                leaveTo = box.land.OnGround(tx, tz);
            }
            lastState = state;
            whiteT += dt;
            // "Hunt it": the hunters go after it in the day
            if (huntWanted && now.hour >= 7f && now.hour <= 15.5f)
            {
                huntWanted = false;
                if (pv.Planner.StartWhiteStagHunt(now, whiteAt.x, whiteAt.z, out var plan)) { whiteHunt = plan; WhiteHunts++; LastWhy = "the hunters go after the white stag"; }
                else LastWhy = "nobody could go after the white stag";
            }
            if (whiteHunt != null && (now.day != whiteHunt.day || now.hour > whiteHunt.end)) whiteHunt = null;
            bool hunted = whiteHunt != null && now.hour >= whiteHunt.landHour;
            bool show = state == WhiteStag.Seen || leaving || huntWanted || (whiteHunt != null && !hunted) || WhiteAtDawn(sim, now);
            if (!show) { HideWhite(); return; }
            if (white == null)
            {
                white = new QuadDoll(QuadLook.Of(QuadKind.WhiteStag), Holder(), box.SolidMaterial);
                white.go.name = "White stag";
                white.SetActive(false); // placed on its first show below
            }
            if (!white.go.activeSelf)
            {
                whiteT = 0f;
                if (!leaving) PlaceWhite(sim, WhiteAtDawn(sim, now));
                if (state == WhiteStag.Seen) { WhiteShows++; TurnToTheStag(pv); }
            }
            var camp = new Vector3(sim.EcoGround.campX, 0f, sim.EcoGround.campZ);
            QuadPose q;
            if (leaving)
            {
                leaveT += dt;
                float dist = (leaveTo - leaveFrom).magnitude, k = dist > 1e-3f ? Mathf.Clamp01(leaveT * LeaveSpeed / dist) : 1f;
                var at = Vector3.Lerp(leaveFrom, leaveTo, k);
                at = box.land.OnGround(at.x, at.z);
                q = QuadPose.Stand(at, Mathf.Atan2(leaveTo.x - leaveFrom.x, leaveTo.z - leaveFrom.z) * Mathf.Rad2Deg);
                QuadMotion.Walk(ref q, leaveT * 0.8f);
                q.scale = 1f - Mathf.SmoothStep(0f, 1f, (k - 0.75f) / 0.25f) * 0.999f; // it fades into the trees at the end
                if (k >= 1f) { leaving = false; HideWhite(); return; }
            }
            else
            {
                whiteYaw = Mathf.Atan2(camp.x - whiteAt.x, camp.z - whiteAt.z) * Mathf.Rad2Deg + 35f;
                q = QuadPose.Stand(whiteAt, whiteYaw);
                // B14.b: it steps out and stands (Alert, 3 s), head to the camp; then the head turns slowly, still standing
                QuadMotion.Alert(ref q, whiteT, -35f + Mathf.Sin(whiteT * 0.5f) * (whiteT > 3f ? 15f : 0f), true);
                if (whiteHunt != null && now.hour >= whiteHunt.stalkStart && now.hour < whiteHunt.shootStart)
                {
                    float into = LifeClock.RealSecondsBetween(whiteHunt.stalkStart, now.hour);
                    if (HuntStalk.Sample(into, whiteHunt.freezeA, whiteHunt.freezeB).move != HuntStalk.Move.Freeze) QuadMotion.Graze(ref q, whiteT, into, 7);
                }
            }
            white.Apply(q);
            white.SetActive(true);
        }

        bool WhiteAtDawn(SimCore sim, ViewTime now) => sim.WhiteStagAtDawn && now.hour >= DeerHerd.DawnFrom && now.hour < DeerHerd.DawnTo && WhiteDawnDay(now.day, sim.State.eco.seed);

        void HideWhite() { if (white != null) white.SetActive(false); }

        /// <summary>Where it stands: the wood's edge nearest the camp (the herd's range cell closest to the fire, at least 1 u out), out from
        /// under the crowns; at dawn (let be) the far side of the range instead, "at the back".</summary>
        void PlaceWhite(SimCore sim, bool back)
        {
            var dv = DeerView.Instance;
            float fx = sim.EcoGround.campX, fz = sim.EcoGround.campZ;
            float bx = fx + 1.4f, bz = fz, best = back ? -1f : float.MaxValue;
            if (dv != null && dv.Field.HasRange)
                foreach (int c in dv.Field.Range)
                {
                    float x = SiteMap.CXc(c), z = SiteMap.CZc(c), d = Mathf.Sqrt((x - fx) * (x - fx) + (z - fz) * (z - fz));
                    if (d < 1f) continue;
                    if (back ? d > best : d < best) { best = d; bx = x; bz = z; }
                }
            else if (sim.EcoDeerHome >= 0) { bx = SiteMap.CXc(sim.EcoDeerHome); bz = SiteMap.CZc(sim.EcoDeerHome); }
            // never in the camp: out to the wood's edge at least MinOut from the fire, along the line from it
            for (int pass = 0; pass < 3; pass++)
            {
                TreeCrowns.Push(box, sim, ref bx, ref bz, 0.08f);
                var v = new Vector2(bx - fx, bz - fz);
                if (v.magnitude >= MinOut) break;
                if (v.sqrMagnitude < 1e-4f) v = Vector2.right;
                v = v.normalized * (MinOut + 0.05f);
                bx = Mathf.Clamp(fx + v.x, -Land.HX + 0.15f, Land.HX - 0.15f); bz = Mathf.Clamp(fz + v.y, -Land.HZ + 0.15f, Land.HZ - 0.15f);
            }
            whiteAt = box.land.OnGround(bx, bz);
        }

        /// <summary>The camp turns to the wood: everyone up within 3 u looks at the white stag a few seconds.</summary>
        void TurnToTheStag(PeopleView pv)
        {
            var who = new List<PersonRef>(); var xs = new List<float>(); var zs = new List<float>();
            pv.StandingNow(who, xs, zs);
            for (int i = 0; i < who.Count; i++)
                if ((new Vector2(xs[i] - whiteAt.x, zs[i] - whiteAt.z)).magnitude < 3f) pv.LookAt(who[i], new Vector2(whiteAt.x, whiteAt.z));
            lookUntil = Time.unscaledTime + 5f; lookers.Clear(); lookers.AddRange(who);
        }
        float lookUntil; readonly List<PersonRef> lookers = new();

        // ---------------------------------------------------------------- B14.a dawn at the pond
        void Dawn(SimCore sim, PeopleView pv, ViewTime now)
        {
            if (lookUntil > 0f && Time.unscaledTime > lookUntil) { foreach (var w in lookers) pv.ClearLookAt(w); lookers.Clear(); lookUntil = 0f; }
            if (dawnStart >= 0f)
            {
                // 20-30 s (B14.a): first light is short (20 s of the view day), so the scene runs past it while the does walk back into the wood
                if (Time.unscaledTime - dawnStart > DawnSeconds || now.hour >= 9f || now.hour < DeerHerd.DawnFrom - 0.5f) EndDawn();
                return;
            }
            if (now.hour < DeerHerd.DawnFrom + 0.1f || now.hour >= DeerHerd.DawnTo - 0.2f || playedDawn == now.day) return;
            if (!DawnDay(now.day, sim.State.eco.seed)) return;
            var dv = DeerView.Instance;
            if (dv == null) return;
            dv.DrinkingNow(drinking);
            if (drinking.Count == 0) return;
            // nobody hunts at the water, and the dawn waits for a hunt in the scene
            foreach (var p in pv.Planner.DeerHuntsOn(now.day)) if (p.HasScene && now.hour >= p.start && now.hour < p.end) return;
            StartDawn(pv, now, now.day == forcedDawn);
        }

        void StartDawn(PeopleView pv, ViewTime now, bool forced)
        {
            var deer = drinking[0];
            dawnDeer = new Vector2(deer.x, deer.z);
            var who = new List<PersonRef>(); var xs = new List<float>(); var zs = new List<float>();
            pv.StandingNow(who, xs, zs);
            var near = new List<(float d, int i)>();
            for (int i = 0; i < who.Count; i++) near.Add(((new Vector2(xs[i], zs[i]) - dawnDeer).magnitude, i));
            near.Sort((a, b) => a.d.CompareTo(b.d));
            cast.Clear(); kid = shusher = null;
            var planner = pv.Planner;
            int adults = 0;
            foreach (var (d, i) in near)
            {
                if (cast.Count >= 4) break;
                bool isKid = planner.StageOf(who[i]) == LifeStage.Kid;
                if (!forced && d > DawnReach) break;
                if (forced && d > DawnReach && (isKid ? kid != null : adults >= 2)) continue; // the panel brings a kid and two grown-ups over
                if (planner.InLifeStory(who[i], now) || planner.DeerHuntStepAt(who[i], now, out _, out _)) continue;
                var pos = new Vector2(xs[i], zs[i]);
                var m = new Member { who = who[i], face = dawnDeer };
                if (d > DawnReach || (isKid && d > KidReach && kid == null))
                {   // walked over to watch: the kid nearer, the grown-ups a step behind, on the camp's side
                    var from = (pos - dawnDeer).sqrMagnitude > 1e-4f ? (pos - dawnDeer).normalized : Vector2.left;
                    float r = isKid ? 0.85f : 1.2f + 0.15f * adults;
                    var side = new Vector2(-from.y, from.x) * ((cast.Count % 2 == 0 ? 1f : -1f) * 0.18f * cast.Count);
                    m.spot = dawnDeer + from * r + side;
                }
                else m.spot = pos;
                if (isKid && kid == null && (m.spot - dawnDeer).magnitude <= KidReach + 0.05f) { m.role = 1; kid = m; }
                else if (!isKid) { adults++; if (shusher == null) { m.role = 2; shusher = m; } }
                cast[who[i].Key] = m;
            }
            playedDawn = now.day;
            if (cast.Count == 0) { LastWhy = "the does drink; nobody is up near the water"; return; }
            dawnStart = Time.unscaledTime;
            DawnScenes++;
            LastWhy = $"dawn at the pond: {cast.Count} watch{(kid != null ? ", a kid holds still" : "")}";
        }

        void EndDawn()
        {
            dawnStart = -1f;
            cast.Clear(); kid = shusher = null;
        }

        bool SpotHook(in PersonActivity a, ViewTime now, out float x, out float z, out Vector2? face)
        {
            x = z = 0f; face = null;
            if (dawnStart < 0f || !cast.TryGetValue(a.who.Key, out var m)) return false;
            x = m.spot.x; z = m.spot.y;
            float left = DawnSeconds - (Time.unscaledTime - dawnStart);
            // the end beat: the kid looks up at the grown-up
            face = m.role == 1 && shusher != null && left < NodSeconds ? shusher.spot : m.face;
            return true;
        }

        void PoseHook(in PersonActivity a, PegDollView view, ref DollPose pose)
        {
            if (dawnStart < 0f || !cast.TryGetValue(a.who.Key, out var m)) return;
            float s = Time.unscaledTime - dawnStart, left = DawnSeconds - s;
            if (m.role == 1 && left > NodSeconds)
            {   // the kid crouches and holds very still, arms in (squash 0.8)
                pose.squash = 0.8f; pose.lean = 10f; pose.roll = 0f; pose.extend = false;
                pose.armL = new Vector3(24f, 0f, -2f); pose.armR = new Vector3(24f, 0f, 2f);
                if (WhiteShown && s > 8f && s < 10.5f) pose.armR = new Vector3(90f, 0f, 6f); // and points at the white stag at the back
            }
            else if (m.role == 2)
            {
                float u = (s - ShhAt) / ShhSeconds;
                if (u > 0f && u < 1f) { float k = Mathf.Sin(u * Mathf.PI); pose.armR = Vector3.Lerp(pose.armR, new Vector3(140f, 0f, -18f), Mathf.Clamp01(k * 2f)); }
                if (left < NodSeconds && left > 0f) pose.lean += Mathf.Abs(Mathf.Sin((NodSeconds - left) / 0.6f * Mathf.PI)) * 10f; // the nod
            }
        }

        // ---------------------------------------------------------------- cheats (the debug panel)
        /// <summary>"Dawn at the pond now": the deer placed if none, the hour to first light, today a watched dawn.</summary>
        public string DebugDawnNow()
        {
            var sim = Sim;
            if (sim == null || !sim.EcologyOn) return "no living box";
            if (sim.EcoDeerCount == 0 && sim.DebugPlaceNearCamp(LifeSpecies.Deer) < 0) return "no ground for deer";
            PeopleView.DebugSetHour(DeerHerd.DawnFrom + 0.8f); // the first ones are up
            var pv = PeopleView.Find();
            if (pv == null) return "no people view";
            forcedDawn = pv.Now.day; playedDawn = -1; EndDawn();
            var dv = DeerView.Instance;
            return dv != null && !dv.Field.HasPond ? "dawn: no pond's edge near the deer (fill the pond)" : "dawn: the does walk to the water; the people watch";
        }

        /// <summary>"White stag now": the white stag steps out and its choice is asked now (deer placed if none).</summary>
        public string DebugWhiteStagNow()
        {
            var sim = Sim;
            if (sim == null || !sim.EcologyOn) return "no living box";
            if (sim.WhiteStagState == WhiteStag.None && !sim.DebugWhiteStagNow()) return "no deer for it";
            if (sim.WhiteStagState != WhiteStag.Seen) return $"the white stag: {sim.WhiteStagState} already";
            var d = sim.DebugRaiseLifeChoice(LifeChoices.WhiteStag);
            return d != null ? "the white stag at the wood's edge: hunt it, or let it be?" : "the white stag shows (its choice waits)";
        }

        /// <summary>"White stag at dawn now" (after "let it be"): first light, a watched dawn, and it shows at the back.</summary>
        public string DebugWhiteDawnNow()
        {
            var sim = Sim;
            if (sim == null || !sim.EcologyOn) return "no living box";
            if (!sim.WhiteStagAtDawn) return "only after \"let it be\"";
            string s = DebugDawnNow();
            forcedWhiteDawn = forcedDawn;
            return s + "; the white stag at the back";
        }

        public string Status()
        {
            var sim = Sim;
            return $"deer scenes: dawn {(DawnPlaying ? $"playing ({cast.Count} watch{(kid != null ? ", kid still" : "")})" : "off")}, dawns {DawnScenes}; white stag " +
                   $"{(sim != null ? sim.WhiteStagState.ToString() : "-")}{(WhiteShown ? " (shown)" : "")}, hunts {WhiteHunts}, let go {WhiteLetGo}; {LastWhy}";
        }
    }
}
