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
    /// P8 #288 phase 2 (keepers-hands 2 beat 3 and 2.1): the berry bushes of a living land.
    /// <list type="bullet">
    /// <item>Berries on the bushes the land grew: small red and dark dots in summer and autumn, flowers in spring, bare in
    /// winter, fewer after a pick (<see cref="SimCore.BerriesAt"/>). One mesh over all the bushes, rebuilt when what it shows changes.</item>
    /// <item>The picks: whoever stands reaching at a bush (Gather) or eating from it (Eat) takes from it as they work
    /// (<see cref="SimCore.PickBush"/>), so the berries really drop on that bush. A walker whose bush stopped being a bush
    /// never starts a pick (the planner drops the place).</item>
    /// <item>The first bush's sign: a bird lands on it, one person nearby points (<see cref="KeeperGesture.Sighting"/>), and
    /// the planner sends another to look. Once per first bush.</item>
    /// </list>
    /// </summary>
    [DefaultExecutionOrder(6)]
    public class BushView : MonoBehaviour
    {
        public static bool AutoCreate = true;
        public static BushView Instance { get; private set; }

        /// <summary>Berry dots on a full bush; fewer as it is picked.</summary>
        public const int FullDots = 7;
        /// <summary>A person picks from the bush they stand at once per this many real seconds (and once more when they leave).</summary>
        public const float PickEverySeconds = 5f;
        /// <summary>The bird's visit: it flies in, sits, flies off (seconds).</summary>
        public const float FlyIn = 2.2f, Sit = 8f, FlyOut = 2.4f, SignSeconds = FlyIn + Sit + FlyOut;
        public const float SignFromHour = 6.5f, SignToHour = 19f;

        BoxView box;
        PeopleView people;
        Transform holder, rootSeen;
        GameObject berriesGo;
        Mesh berriesMesh;
        long berriesKey = long.MinValue;
        float refreshT;

        // picks
        sealed class Pick { public int cell; public float t; }
        readonly Dictionary<int, Pick> picking = new();
        readonly HashSet<int> nowPicking = new();
        readonly List<int> ended = new();
        public int PicksDone { get; private set; }
        public int LastPickCell { get; private set; } = -1;
        /// <summary>Who is reaching or eating at a bush right now (for tests).</summary>
        public int PickersNow => picking.Count;

        // the sign
        int signCell = -1;               // the first bush this view has played the sign for
        float signT = -1f;
        Vector3 bushTop, flyFrom, flyTo;
        PersonRef pointer;
        GameObject bird;
        MeshFilter birdMf;
        Mesh birdUp, birdDown;
        public int SignsPlayed { get; private set; }
        public bool SignPlaying => signT >= 0f;
        /// <summary>The bird is on the bush (landed).</summary>
        public bool BirdLanded => signT >= FlyIn && signT < FlyIn + Sit;
        public PersonRef LastPointer { get; private set; }
        public Vector3 BirdLocal => bird != null ? bird.transform.localPosition : Vector3.zero;
        public int BirdCell => SignPlaying ? signCell : -1;

        /// <summary>Berry dots drawn now, and flowers (for tests).</summary>
        public int DotsShown { get; private set; }
        public int BushesWithBerries { get; private set; }

        SimCore Sim => GameClock.Instance != null ? GameClock.Instance.Sim : null;
        PeopleView People => people != null ? people : people = PeopleView.Find();

        public static BushView Ensure(BoxView b)
        {
            if (b == null) return null;
            var v = b.GetComponent<BushView>();
            return v != null ? v : b.gameObject.AddComponent<BushView>();
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
            if (berriesMesh != null) Destroy(berriesMesh);
            holder = null; berriesGo = null; bird = null; berriesKey = long.MinValue;
            picking.Clear();
        }

        void Update()
        {
            var sim = Sim;
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.1f);
            if (sim == null || !sim.EcologyOn || box == null || box.Root == null) { Hide(); return; }
            if (rootSeen != box.Root) { holder = null; berriesGo = null; bird = null; berriesKey = long.MinValue; rootSeen = box.Root; }
            var pv = People;
            if (pv == null || pv.Planner == null) return;
            TrackPicks(sim, pv, dt);
            refreshT -= dt;
            if (refreshT <= 0f) { refreshT = 0.4f; UpdateBerries(sim, pv); }
            UpdateSign(sim, pv, dt);
        }

        void Hide()
        {
            if (berriesGo != null) berriesGo.SetActive(false);
            if (bird != null) bird.SetActive(false);
            signT = -1f;
            picking.Clear();
        }

        Transform Holder()
        {
            if (holder == null)
            {
                holder = new GameObject("Bushes") { hideFlags = HideFlags.DontSave }.transform;
                holder.SetParent(box.Root, false);
            }
            return holder;
        }

        // ---------------------------------------------------------------- the picks
        void TrackPicks(SimCore sim, PeopleView pv, float dt)
        {
            nowPicking.Clear();
            foreach (var a in pv.Activities)
            {
                if (a.moving || a.place.kind != PlaceKind.BushCell || a.kind is not (ActivityKind.Gather or ActivityKind.Eat)) continue;
                int key = a.who.Key;
                nowPicking.Add(key);
                if (!picking.TryGetValue(key, out var p) || p.cell != a.place.index)
                {
                    if (p != null) PickAt(sim, pv, p.cell); // moved to another bush: the last one was picked
                    p = new Pick { cell = a.place.index };
                    picking[key] = p;
                }
                p.t += dt;
                if (p.t >= PickEverySeconds) { PickAt(sim, pv, p.cell); p.t = 0f; }
            }
            ended.Clear();
            foreach (var kv in picking) if (!nowPicking.Contains(kv.Key)) ended.Add(kv.Key);
            foreach (int key in ended)
            {
                var p = picking[key];
                if (p.t >= 1f) PickAt(sim, pv, p.cell); // the pick ended: the bush has fewer berries
                picking.Remove(key);
            }
        }

        void PickAt(SimCore sim, PeopleView pv, int cell)
        {
            if (sim.PickBush(cell, pv.Now.day) <= 0f) return;
            PicksDone++; LastPickCell = cell;
            berriesKey = long.MinValue; // draw it at once
            refreshT = 0f;
        }

        // ---------------------------------------------------------------- #357: the bush answers a stroke
        /// <summary>#357: shakes this bush (site cell) once, leaning away from <paramref name="away"/> (world direction from whoever reached in).
        /// The bush is in the land's chunk mesh, so this is the shader's sway at the bush's spot (<see cref="HitFx.Shake"/>); its berry dots are
        /// one mesh with the same sway weight and move with it. False if the cell has no bush.</summary>
        public bool Shake(int cell, Vector3 away, float delay = 0f)
        {
            if (box == null || box.Root == null || !box.BushSpots.TryGetValue(cell, out var spot)) return false;
            return HitFx.Shake(ShakeTarget.Bush, box.Root.TransformPoint(spot.pos), away, delay);
        }

        // ---------------------------------------------------------------- the berries
        static float H01(int a, int b) => Land.Hash3(7311, a, b);

        void UpdateBerries(SimCore sim, PeopleView pv)
        {
            long day = pv.Now.day;
            int season = (int)LifeClock.SeasonOf(sim.State.month); // the land's season, not the view day's (which turns at midnight)
            var state = sim.BerryStateOf(season);
            long key = 17 + season * 131 + (state == BerryState.Berries ? 1 : 0);
            var items = new List<(int cell, Vector3 pos, float r, int n)>();
            foreach (var kv in box.BushSpots)
            {
                if (sim.State.eco.cover[kv.Key] != (byte)EcoCover.Bush) continue;
                int n = state == BerryState.Flowers ? 5 : state == BerryState.Berries ? Mathf.RoundToInt(sim.BerriesAt(kv.Key, day, season) * FullDots) : 0;
                if (n > 0) items.Add((kv.Key, kv.Value.pos, kv.Value.r, n));
                key = key * 31 + kv.Key * 7 + n;
            }
            if (key == berriesKey && berriesGo != null) return;
            berriesKey = key;
            var mb = new MeshBuilder(box.GetHashCode() & 0xffff) { jitter = 0f, sway = 0.3f }; // #357: the dots sway with the bush (its weight), so a shake moves them too
            int dots = 0, withBerries = 0;
            foreach (var (cell, pos, r, n) in items)
            {
                if (state == BerryState.Berries) withBerries++;
                for (int i = 0; i < n; i++)
                {
                    // a fixed place per dot index on the bush's upper half, so picking takes the last ones and the rest stay put
                    float az = H01(cell, i * 3 + 1) * Mathf.PI * 2f, el = Mathf.Lerp(0.25f, 1.15f, H01(cell, i * 3 + 2));
                    var dir = new Vector3(Mathf.Cos(az) * Mathf.Cos(el), Mathf.Sin(el), Mathf.Sin(az) * Mathf.Cos(el));
                    var at = pos + Vector3.up * r * 0.5f + new Vector3(dir.x * r * 1.2f, dir.y * r * 0.8f, dir.z * r * 1.1f) * 1.02f;
                    Color col;
                    float rad;
                    if (state == BerryState.Flowers)
                    {
                        col = H01(cell, i + 40) < 0.5f ? new Color(0.96f, 0.94f, 0.86f) : new Color(0.93f, 0.7f, 0.8f);
                        rad = 0.011f;
                    }
                    else
                    {
                        col = (i & 1) == 0 ? new Color(0.78f, 0.1f, 0.15f) : new Color(0.36f, 0.07f, 0.22f);
                        rad = 0.014f;
                    }
                    mb.Ico(at, rad, Vector3.one, col, 0.1f);
                    dots++;
                }
            }
            DotsShown = dots; BushesWithBerries = withBerries;
            if (berriesMesh != null) Destroy(berriesMesh);
            if (dots == 0) { if (berriesGo != null) berriesGo.SetActive(false); berriesMesh = null; return; }
            berriesMesh = mb.ToMesh("BushBerries");
            if (berriesGo == null)
            {
                berriesGo = new GameObject("BushBerries") { hideFlags = HideFlags.DontSave };
                berriesGo.transform.SetParent(Holder(), false);
                berriesGo.AddComponent<MeshFilter>();
                var rend = berriesGo.AddComponent<MeshRenderer>();
                rend.sharedMaterial = box.SolidMaterial;
                rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            berriesGo.GetComponent<MeshFilter>().sharedMesh = berriesMesh;
            berriesGo.SetActive(true);
        }

        // ---------------------------------------------------------------- the first bush's sign
        void UpdateSign(SimCore sim, PeopleView pv, float dt)
        {
            if (signT >= 0f) { StepSign(pv, dt); return; }
            int cell = sim.FirstBushCell;
            if (cell < 0 || cell == signCell || !sim.FirstBushSignActive) return;
            if (!box.BushSpots.TryGetValue(cell, out var spot)) return;
            float hour = pv.ViewHour;
            if (hour < SignFromHour || hour > SignToHour) return; // waits for the day
            signCell = cell;
            SignsPlayed++;
            bushTop = spot.pos + Vector3.up * (spot.r * 1.25f);
            // it comes in from over the glass side, high, and lands on the top
            var away = new Vector3(bushTop.x >= 0f ? 1f : -1f, 0f, bushTop.z >= 0f ? 1f : -1f).normalized;
            flyFrom = bushTop + away * 0.9f + Vector3.up * 0.55f;
            flyTo = bushTop;
            signT = 0f;
            pointer = PersonRef.None; LastPointer = PersonRef.None;
            var who = new List<PersonRef>(); var xs = new List<float>(); var zs = new List<float>();
            pv.StandingNow(who, xs, zs);
            int n = RabbitHerd.Nearest(xs, zs, spot.pos.x, spot.pos.z);
            if (n >= 0)
            {
                pointer = who[n]; LastPointer = pointer;
                pv.Planner.React(KeeperGesture.Sighting, pv.Now, default, pointer, 1f, SignSeconds - 2f);
                pv.LookAt(pointer, new Vector2(spot.pos.x, spot.pos.z));
            }
            var cam = Camera.main != null ? Camera.main.GetComponent<BoxCamera>() : null;
            if (cam != null) cam.NudgeToward(box.transform.TransformPoint(bushTop));
            EnsureBird();
            bird.SetActive(true);
            bird.transform.localScale = Vector3.one * 1.8f; // a gray box bird, big enough to read at the default zoom
            bird.transform.localPosition = flyFrom;
        }

        void StepSign(PeopleView pv, float dt)
        {
            signT += dt;
            Vector3 p;
            bool flying;
            if (signT < FlyIn)
            {
                float u = Smooth(signT / FlyIn);
                p = Vector3.Lerp(flyFrom, flyTo, u) + Vector3.up * Mathf.Sin(u * Mathf.PI) * 0.06f;
                flying = true;
            }
            else if (signT < FlyIn + Sit)
            {
                float s = signT - FlyIn;
                p = flyTo + Vector3.up * (Mathf.Max(0f, Mathf.Sin(s * 2.6f)) < 0.97f ? 0f : 0.004f); // an occasional small hop
                p.y += Mathf.Max(0f, Mathf.Sin(s * 5.1f) - 0.8f) * 0.02f;
                flying = false;
            }
            else
            {
                float u = Smooth((signT - FlyIn - Sit) / FlyOut);
                var to = flyFrom + (flyFrom - flyTo) * 0.6f + Vector3.up * 0.3f;
                p = Vector3.Lerp(flyTo, to, u);
                flying = true;
            }
            var prev = bird.transform.localPosition;
            bird.transform.localPosition = p;
            var d = p - prev; d.y = 0f;
            Vector3 face = d.sqrMagnitude > 1e-8f ? d : bird.transform.localRotation * Vector3.forward;
            if (face.sqrMagnitude > 1e-8f) bird.transform.localRotation = Quaternion.Slerp(bird.transform.localRotation, Quaternion.LookRotation(face), 1f - Mathf.Exp(-dt * 8f));
            birdMf.sharedMesh = flying && ((int)(signT * 9f) & 1) == 0 ? birdUp : birdDown;
            if (signT >= SignSeconds)
            {
                bird.SetActive(false);
                if (!pointer.IsNone) pv.ClearLookAt(pointer);
                signT = -1f;
            }
        }

        static float Smooth(float u) { u = Mathf.Clamp01(u); return u * u * (3f - 2f * u); }

        void EnsureBird()
        {
            if (bird != null) return;
            if (birdUp == null) { birdUp = BirdMesh(true); birdDown = BirdMesh(false); }
            bird = new GameObject("Bird") { hideFlags = HideFlags.DontSave };
            bird.transform.SetParent(Holder(), false);
            birdMf = bird.AddComponent<MeshFilter>();
            birdMf.sharedMesh = birdDown;
            var rend = bird.AddComponent<MeshRenderer>();
            rend.sharedMaterial = box.SolidMaterial;
            rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        /// <summary>A small bird, gray box: round body, head, beak, tail and two wings (up or folded down), facing local +z,
        /// its feet at the origin.</summary>
        static Mesh BirdMesh(bool wingsUp)
        {
            var mb = new MeshBuilder(55) { jitter = 0f };
            var body = new Color(0.46f, 0.44f, 0.42f);
            var dark = new Color(0.3f, 0.29f, 0.29f);
            var breast = new Color(0.78f, 0.62f, 0.5f);
            mb.Ico(new Vector3(0, 0.022f, 0), 0.02f, new Vector3(0.85f, 0.8f, 1.35f), body, 0f, breast);
            mb.Ico(new Vector3(0, 0.038f, 0.022f), 0.0125f, Vector3.one, dark, 0f);
            mb.Box(new Vector3(0, 0.037f, 0.039f), new Vector3(0.006f, 0.005f, 0.012f), new Color(0.9f, 0.62f, 0.2f));
            mb.Box(new Vector3(0, 0.024f, -0.034f), new Vector3(0.016f, 0.004f, 0.034f), dark, Quaternion.Euler(-15f, 0, 0), true);
            foreach (int s in new[] { -1, 1 })
            {
                var rot = Quaternion.Euler(0f, 0f, s * (wingsUp ? 55f : -8f));
                mb.Box(new Vector3(s * (wingsUp ? 0.03f : 0.016f), wingsUp ? 0.04f : 0.024f, -0.002f), new Vector3(0.034f, 0.003f, 0.026f), dark, rot, true);
            }
            mb.Box(new Vector3(-0.005f, 0.006f, 0.003f), new Vector3(0.002f, 0.012f, 0.002f), new Color(0.9f, 0.62f, 0.2f));
            mb.Box(new Vector3(0.005f, 0.006f, 0.003f), new Vector3(0.002f, 0.012f, 0.002f), new Color(0.9f, 0.62f, 0.2f));
            return mb.ToMesh(wingsUp ? "BirdUp" : "BirdDown");
        }
    }
}
