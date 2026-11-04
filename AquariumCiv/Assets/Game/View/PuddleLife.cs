using AquariumCiv.Game;
using AquariumCiv.Life;
using AquariumCiv.Sim;
using UnityEngine;

namespace AquariumCiv.View
{
    /// <summary>
    /// People and animals use the mist's puddles (#266): from time to time, while a puddle that is deep enough to drink from
    /// stands, the people near it come over (kids stomp in it, now and then a grown-up kneels to wash their hands or cup the
    /// water), they draw a little from it (it shrinks), and a rabbit hops up to drink. While there is no pond the same puddle is
    /// the source of the muddy bucket water (LifePlanner.WaterNow, #267). Gray-box rabbit: a few blobs.
    /// </summary>
    public class PuddleLife : MonoBehaviour
    {
        public static PuddleLife Instance { get; private set; }

        [Tooltip("Seconds between visits (real time).")] public float visitSeconds = 11f;
        BoxView box;
        float timer = 5f;
        int round;
        /// <summary>Visits made, and the cell of the last one (-1 none), for tests and the debug panel.</summary>
        public int Visits { get; private set; }
        public int LastCell { get; private set; } = -1;
        public int RabbitDrinks { get; private set; }

        // the rabbit
        GameObject rabbit;
        Transform head;
        Vector3 from, to;
        float hopT, drinkT;
        int rabbitCell = -1;
        enum RState { Away, Hopping, Drinking, Leaving }
        RState rstate;

        public static PuddleLife Ensure(BoxView b)
        {
            if (b == null) return null;
            var s = b.GetComponent<PuddleLife>();
            return s != null ? s : b.gameObject.AddComponent<PuddleLife>();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (Instance != null || FindAnyObjectByType<GameClock>() == null) return;
            Ensure(FindAnyObjectByType<BoxView>());
        }

        void OnEnable() { if (Instance != null && Instance != this) { enabled = false; return; } Instance = this; box = GetComponent<BoxView>(); }
        void OnDisable() { if (Instance == this) Instance = null; if (rabbit != null) Destroy(rabbit); }

        SimCore Sim => GameClock.Instance != null ? GameClock.Instance.Sim : null;

        void Update()
        {
            var sim = Sim;
            if (sim == null || !sim.EcologyOn || box == null) { HideRabbit(); return; }
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.1f);
            timer -= dt;
            if (timer <= 0f) { timer = visitSeconds; VisitNow(); }
            UpdateRabbit(sim, dt);
        }

        /// <summary>The best puddle to visit: drinkable in the sim, and one the surface draws. -1 when there is none.</summary>
        int Pick(SimCore sim)
        {
            var surf = EcoSurface.Instance;
            if (surf == null) return -1;
            var cells = surf.PuddleCellsShown;
            int n = 0;
            for (int i = 0; i < cells.Count; i++) if (sim.EcoPuddleAt(cells[i]) >= sim.Config.eco.puddleDrinkAt) n++;
            if (n == 0) return -1;
            int pick = round++ % n;
            for (int i = 0; i < cells.Count; i++)
                if (sim.EcoPuddleAt(cells[i]) >= sim.Config.eco.puddleDrinkAt && pick-- == 0) return cells[i];
            return -1;
        }

        /// <summary>Sends the people near a puddle over now (also the debug panel's "puddle visit"). Returns what happened.</summary>
        public string VisitNow()
        {
            var sim = Sim;
            if (sim == null || !sim.EcologyOn) return "living land off";
            int cell = Pick(sim);
            if (cell < 0) return "no puddle to visit";
            var pv = PeopleView.Find();
            if (pv == null || pv.Planner == null) return "no people";
            pv.Planner.React(KeeperGesture.Puddle, pv.Now, Place.Land(PlaceKind.Puddle, cell), default, 0.95f, 4f, 2.2f);
            sim.EcoDrawPuddle(cell, 0.02f); // a little is used
            Visits++; LastCell = cell;
            if (rstate == RState.Away && sim.EcoRabbits >= 1f) SendRabbit(cell);
            return $"people to the puddle at cell {cell}";
        }

        // ---------------- the rabbit ----------------
        void HideRabbit() { if (rabbit != null) rabbit.SetActive(false); rstate = RState.Away; }

        void SendRabbit(int cell)
        {
            if (rabbit == null) MakeRabbit();
            rabbitCell = cell;
            float cx = AquariumCiv.Sim.Settle.SiteMap.CXc(cell), cz = AquariumCiv.Sim.Settle.SiteMap.CZc(cell);
            // from the nearest edge of the land, 1.2 units off
            var dir = new Vector2(cx, cz).normalized; if (dir.sqrMagnitude < 0.01f) dir = Vector2.right;
            float sx = Mathf.Clamp(cx + dir.x * 1.2f, -Land.HX + 0.1f, Land.HX - 0.1f), sz = Mathf.Clamp(cz + dir.y * 1.2f, -Land.HZ + 0.1f, Land.HZ - 0.1f);
            from = new Vector3(sx, 0f, sz);
            to = new Vector3(cx + 0.07f, 0f, cz);
            rabbit.SetActive(true);
            rstate = RState.Hopping; hopT = 0f;
        }

        void MakeRabbit()
        {
            rabbit = new GameObject("PuddleRabbit") { hideFlags = HideFlags.DontSave };
            rabbit.transform.SetParent(box.Root != null ? box.Root : transform, false);
            var co = box.land.Colors;
            var mb = new MeshBuilder(box.seed + 21) { jitter = 0f };
            var fur = new Color(0.62f, 0.52f, 0.42f);
            mb.Ico(new Vector3(0f, 0.022f, 0f), 0.024f, new Vector3(1.35f, 1f, 1f), fur, 0.1f);          // body
            mb.Ico(new Vector3(-0.026f, 0.014f, 0f), 0.011f, Vector3.one, Color.white, 0.1f);            // tail (behind: -x)
            var body = mb.ToMesh("RabbitBody");
            var hb = new MeshBuilder(box.seed + 22) { jitter = 0f };
            hb.Ico(Vector3.zero, 0.014f, new Vector3(1.2f, 1f, 1f), fur, 0.1f);
            hb.Ico(new Vector3(-0.002f, 0.022f, 0.006f), 0.005f, new Vector3(0.6f, 2.6f, 0.6f), fur, 0.1f);
            hb.Ico(new Vector3(-0.002f, 0.022f, -0.006f), 0.005f, new Vector3(0.6f, 2.6f, 0.6f), fur, 0.1f);
            var headMesh = hb.ToMesh("RabbitHead");
            var bg = new GameObject("Body"); bg.transform.SetParent(rabbit.transform, false);
            bg.AddComponent<MeshFilter>().sharedMesh = body; Rend(bg);
            var hg = new GameObject("Head"); hg.transform.SetParent(rabbit.transform, false);
            hg.transform.localPosition = new Vector3(0.03f, 0.03f, 0f);
            hg.AddComponent<MeshFilter>().sharedMesh = headMesh; Rend(hg);
            head = hg.transform;
            rabbit.SetActive(false);
        }

        void Rend(GameObject g)
        {
            var r = g.AddComponent<MeshRenderer>();
            r.sharedMaterial = box.SolidMaterial;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        void UpdateRabbit(SimCore sim, float dt)
        {
            if (rabbit == null || rstate == RState.Away) return;
            Vector3 flat;
            float y = 0f;
            switch (rstate)
            {
                case RState.Hopping:
                case RState.Leaving:
                    hopT += dt / 3.2f;
                    float u = Mathf.Clamp01(hopT);
                    var a = rstate == RState.Hopping ? from : to; var b = rstate == RState.Hopping ? to : from;
                    flat = Vector3.Lerp(a, b, u);
                    y = Mathf.Abs(Mathf.Sin(u * 9f * Mathf.PI)) * 0.03f; // hops
                    Face(b - a);
                    if (u >= 1f)
                    {
                        if (rstate == RState.Hopping) { rstate = RState.Drinking; drinkT = 0f; }
                        else { HideRabbit(); return; }
                        hopT = 0f;
                    }
                    break;
                default:
                    flat = to; drinkT += dt;
                    head.localPosition = new Vector3(0.03f, 0.03f - 0.014f * Mathf.Abs(Mathf.Sin(drinkT * 5f)), 0f); // dips to the water
                    if (drinkT > 4f)
                    {
                        sim.EcoDrawPuddle(rabbitCell, 0.02f); RabbitDrinks++;
                        head.localPosition = new Vector3(0.03f, 0.03f, 0f);
                        rstate = RState.Leaving; hopT = 0f;
                    }
                    break;
            }
            rabbit.transform.localPosition = box.land.OnGround(flat.x, flat.z) + Vector3.up * y;
        }

        void Face(Vector3 d)
        {
            if (d.sqrMagnitude < 1e-6f) return;
            rabbit.transform.localRotation = Quaternion.Euler(0f, -Mathf.Atan2(d.z, d.x) * Mathf.Rad2Deg, 0f);
        }

        /// <summary>Where the rabbit is now, or null when it is away (tests).</summary>
        public Vector3? RabbitAt => rabbit != null && rabbit.activeSelf ? rabbit.transform.localPosition : (Vector3?)null;
    }
}
