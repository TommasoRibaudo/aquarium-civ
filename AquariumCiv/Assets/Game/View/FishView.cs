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
    /// #708 (B70): the pond's fish after their arrival. The sim's school (<see cref="SimCore.EcoFishShown"/>, capped by the animal budget) as
    /// dark slivers drifting on the water (B70.b), and, where the pond stands against a pane the Keeper can look through, up to two fish seen
    /// from the side on the water's wall (B70.c), each fish drawn once (<see cref="FishSchool"/> is the logic). They dart from rings and
    /// splashes (any <see cref="HitFx"/> ring or droplets on the water), from a person in the water, nose a fisher's hook before a bite
    /// (<see cref="FishingView"/>), and one drifts to a kid's hand at the edge (B39.c, <see cref="FishGrabGame"/>). Code-built meshes, one opaque
    /// material (the box's), no new transparent layer. Debug: "Fish: show them at the glass", "Fish: startle", "Fish: the shadows".
    /// </summary>
    [DefaultExecutionOrder(7)]
    public sealed class FishView : MonoBehaviour
    {
        public static bool AutoCreate = true;
        public static FishView Instance { get; private set; }

        /// <summary>Wall samples along each side (box units).</summary>
        public const float WallStep = 0.01f;

        BoxView box;
        Transform holder, rootSeen;
        FishSchool school;
        PeopleView people;

        sealed class Drawn
        {
            public GameObject go;
            public Transform shadow, shadowTail, side, sideTail, sideFin;
        }
        readonly List<Drawn> drawn = new();
        Mesh shadowBody, shadowTailM, sideBody, sideTailM, sideFinM;
        int colourKey = int.MinValue;
        uint wallKey;
        readonly Dictionary<GlassSide, float[]> walls = new();
        readonly List<Vector2> inner = new();
        readonly List<Vector3> hits = new();
        readonly List<PersonRef> whoBuf = new(); readonly List<float> px = new(), pz = new();
        float clock;

        /// <summary>For the tests and the panel: the school (its fish and counters), the count the sim asks for, the walls' depth sampler.</summary>
        public FishSchool School => school;
        public int Wanted { get; private set; }
        public int ShadowsShown => school?.ShadowsNow ?? 0;
        public int SidesShown => school?.SidesNow ?? 0;
        public float WallDepthAt(GlassSide s, float along) => Depth(s, along);
        SimCore Sim => GameClock.Instance != null ? GameClock.Instance.Sim : null;

        public static FishView Ensure(BoxView b)
        {
            if (b == null) return null;
            var v = b.GetComponent<FishView>();
            return v != null ? v : b.gameObject.AddComponent<FishView>();
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
            HitFx.WaterPuff += OnPuff;
        }

        void OnDisable()
        {
            HitFx.WaterPuff -= OnPuff;
            if (Instance == this) Instance = null;
            if (holder != null) Destroy(holder.gameObject);
            foreach (var m in new[] { shadowBody, shadowTailM, sideBody, sideTailM, sideFinM }) if (m != null) Destroy(m);
            holder = null; drawn.Clear(); school = null; colourKey = int.MinValue; wallKey = 0;
        }

        void OnPuff(PuffKind kind, Vector3 world)
        {
            if (box == null || box.Root == null) return;
            hits.Add(box.Root.InverseTransformPoint(world));
        }

        // ---------------------------------------------------------------- update
        void Update()
        {
            var sim = Sim;
            float dt = Mathf.Min(Time.deltaTime, 0.1f);
            if (sim == null || !sim.EcologyOn || box == null || box.Root == null || box.layout.eco.pondCells <= 0) { Hide(); hits.Clear(); return; }
            if (rootSeen != box.Root) { holder = null; drawn.Clear(); rootSeen = box.Root; school = null; wallKey = 0; }
            var eco = box.layout.eco;
            if (school == null) school = new FishSchool(box.seed * 31 + 708) { Water = (x, z) => eco.WaterCell(SiteMap.CellAt(x, z)), WallDepth = Depth };
            school.HX = Land.HX; school.ZMin = Land.ZMin; school.ZMax = Land.ZMax;
            school.waterY = eco.waterY;
            RebuildWalls(eco);

            int want = sim.EcoFishShown();
            var arrival = LifeArrivalView.Instance;
            if (arrival != null && arrival.Playing(LifeSpecies.Fish)) want = 0; // the arrival's own fish fan out first (B12.e)
            if (!ReplayGate.Shows(-1)) want = 0; // #669: no record of the stock by year: a replay shows none
            if (inner.Count == 0) want = 0;
            Wanted = want;
            int homeCell = sim.EcoFishHome;
            var home = homeCell >= 0 ? new Vector2(SiteMap.CXc(homeCell), SiteMap.CZc(homeCell)) : (inner.Count > 0 ? inner[inner.Count / 2] : Vector2.zero);
            if (!school.Inner(home) && inner.Count > 0) home = Nearest(inner, home);
            school.SetCount(want, home, inner);
            if (want == 0) { Hide(); hits.Clear(); return; }

            var ice = IceView.Instance;
            bool iced = ice != null && ice.Amount > 0.3f;
            school.winter = LifeClock.SeasonOf(sim.State.month) == Season.Winter;
            EnsureMeshes(iced);

            // what the fish answer: rings and splashes on the water, people in it, the kids' hands at the edge
            foreach (var h in hits) if (eco.WaterCell(SiteMap.CellAt(h.x, h.z)) || NearWaterCell(eco, h.x, h.z)) school.Startle(new Vector2(h.x, h.z));
            hits.Clear();
            if (people == null) people = PeopleView.Find();
            if (people != null && people.Planner != null)
            {
                people.StandingNow(whoBuf, px, pz);
                for (int i = 0; i < whoBuf.Count; i++) if (eco.WaterCell(SiteMap.CellAt(px[i], pz[i]))) school.PersonIn(new Vector2(px[i], pz[i]));
                school.lures.Clear();
                people.KidPlay.FishLures(people.PoseClock, school.lures);
            }
            else school.lures.Clear();

            clock += dt;
            school.Step(dt, clock);
            Draw(eco, iced, ice);
        }

        static Vector2 Nearest(List<Vector2> list, Vector2 p)
        {
            var best = list[0]; float bd = float.MaxValue;
            foreach (var q in list) { float d = (q - p).sqrMagnitude; if (d < bd) { bd = d; best = q; } }
            return best;
        }

        static bool NearWaterCell(EcoLayer eco, float x, float z) => eco.NearWater(SiteMap.CellAt(x, z));

        void Hide()
        {
            if (holder != null && holder.gameObject.activeSelf) holder.gameObject.SetActive(false);
        }

        // ---------------------------------------------------------------- the walls (BoxView.WaterWall's own rule)
        void RebuildWalls(EcoLayer eco)
        {
            uint key = eco.WaterHash() ^ (uint)Mathf.RoundToInt(eco.waterY * 1000f) * 2654435761u;
            if (key == wallKey && walls.Count == 4) return;
            wallKey = key;
            inner.Clear();
            for (int c = 0; c < SiteMap.N; c++)
            {
                if (!eco.WaterCell(c)) continue;
                var p = new Vector2(SiteMap.CXc(c), SiteMap.CZc(c));
                if (school.Inner(p)) inner.Add(p);
            }
            for (int s = 0; s < 4; s++)
            {
                var gs = (GlassSide)s;
                float lim = FishSchool.AlongX(gs) ? Land.HX : Mathf.Max(-Land.ZMin, Land.ZMax);
                int n = Mathf.CeilToInt(2f * lim / WallStep) + 1;
                var arr = new float[n];
                for (int i = 0; i < n; i++) arr[i] = WallDepthNow(eco, gs, -lim + i * WallStep);
                walls[gs] = arr;
            }
        }

        float Depth(GlassSide s, float along)
        {
            if (!walls.TryGetValue(s, out var arr) || arr.Length == 0) return 0f;
            float lim = FishSchool.AlongX(s) ? Land.HX : Mathf.Max(-Land.ZMin, Land.ZMax);
            int i = Mathf.RoundToInt((along + lim) / WallStep);
            return i < 0 || i >= arr.Length ? 0f : arr[i];
        }

        /// <summary>The wall's depth at a point of a side as BoxView draws it: a sheet piece at the edge under the level, the ground at the edge under it;
        /// from the water line down to the foot (the ground 0.03 u down, at least 0.01 u under the level). 0 = no wall there.</summary>
        float WallDepthNow(EcoLayer eco, GlassSide s, float along)
        {
            float y = eco.waterY;
            Vector2 edge = s switch
            {
                GlassSide.Left => new Vector2(-Land.HX + 0.001f, along), GlassSide.Right => new Vector2(Land.HX - 0.001f, along),
                GlassSide.Front => new Vector2(along, Land.ZMin + 0.001f), _ => new Vector2(along, Land.ZMax - 0.001f),
            };
            if (Mathf.Abs(edge.x) > Land.HX || edge.y < Land.ZMin || edge.y > Land.ZMax) return 0f;
            int c = SiteMap.CellAt(edge.x, edge.y);
            if (c < 0 || !eco.NearWater(c)) return 0f;
            // the sheet's sub-piece at the edge (BoxView.EcoWaterMesh cuts each cell in 3 x 3)
            float h = SiteMap.Cell * 0.5f + 0.003f, step = 2f * h / 3f;
            float cx = SiteMap.CXc(c), cz = SiteMap.CZc(c);
            int si = Mathf.Clamp(Mathf.FloorToInt((edge.x - (cx - h)) / step), 0, 2), sj = Mathf.Clamp(Mathf.FloorToInt((edge.y - (cz - h)) / step), 0, 2);
            float x0 = cx - h + si * step, z0 = cz - h + sj * step;
            if (!box.WaterPieceUnder(x0, z0, x0 + step, z0 + step)) return 0f;
            float ground = box.land.Height(edge.x, edge.y);
            if (ground >= y) return 0f;
            float foot = Mathf.Min(ground - 0.03f, y - 0.01f);
            return y - foot;
        }

        // ---------------------------------------------------------------- drawing
        void EnsureMeshes(bool iced)
        {
            var co = box.land.Colors;
            int key = (iced ? 1 : 0) + Mathf.RoundToInt(co.waterDeep.r * 255f) * 7 + Mathf.RoundToInt(co.waterDeep.g * 255f) * 1031 + Mathf.RoundToInt(co.waterDeep.b * 255f) * 65537;
            if (key == colourKey && shadowBody != null) return;
            colourKey = key;
            float k = iced ? 0.7f : 0.55f; // B70.b: waterDeep × 0.55, dimmer under ice
            var body = new Color(co.waterDeep.r * k, co.waterDeep.g * k, co.waterDeep.b * k);
            var belly = new Color(Mathf.Min(1f, co.water.r * 1.1f), Mathf.Min(1f, co.water.g * 1.1f), Mathf.Min(1f, co.water.b * 1.1f));
            var eye = new Color(0.9f, 0.92f, 0.86f);
            foreach (var m in new[] { shadowBody, shadowTailM, sideBody, sideTailM, sideFinM }) if (m != null) Destroy(m);
            shadowBody = Build("FishShadowBody", mb => LifeLooks.FishShape(mb, 1f, body, LifeLooks.FishPart.Body));
            shadowTailM = Build("FishShadowTail", mb => LifeLooks.FishShape(mb, 1f, body, LifeLooks.FishPart.Tail));
            sideBody = Build("FishSideBody", mb => LifeLooks.FishSide(mb, 1f, body, belly, eye, LifeLooks.FishPart.Body));
            sideTailM = Build("FishSideTail", mb => LifeLooks.FishSide(mb, 1f, body, belly, eye, LifeLooks.FishPart.Tail));
            sideFinM = Build("FishSideFin", mb => LifeLooks.FishSide(mb, 1f, body, belly, eye, LifeLooks.FishPart.Fin));
            foreach (var d in drawn)
            {
                d.shadow.GetComponent<MeshFilter>().sharedMesh = shadowBody; d.shadowTail.GetComponent<MeshFilter>().sharedMesh = shadowTailM;
                d.side.GetComponent<MeshFilter>().sharedMesh = sideBody; d.sideTail.GetComponent<MeshFilter>().sharedMesh = sideTailM; d.sideFin.GetComponent<MeshFilter>().sharedMesh = sideFinM;
            }
        }

        static Mesh Build(string name, System.Action<MeshBuilder> f)
        {
            var mb = new MeshBuilder(708) { jitter = 0f };
            f(mb);
            var m = mb.ToMesh(name);
            m.hideFlags = HideFlags.DontSave;
            return m;
        }

        Transform Holder()
        {
            if (holder == null)
            {
                holder = new GameObject("Fish") { hideFlags = HideFlags.DontSave }.transform;
                holder.SetParent(box.Root, false);
            }
            if (!holder.gameObject.activeSelf) holder.gameObject.SetActive(true);
            return holder;
        }

        Transform Part(Transform parent, string name, Mesh mesh, Vector3 at)
        {
            var go = new GameObject(name) { hideFlags = HideFlags.DontSave };
            go.transform.SetParent(parent, false);
            go.transform.localPosition = at;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = box.SolidMaterial;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return go.transform;
        }

        void Draw(EcoLayer eco, bool iced, IceView ice)
        {
            var h = Holder();
            while (drawn.Count < school.fish.Count)
            {
                var d = new Drawn { go = new GameObject("Fish " + drawn.Count) { hideFlags = HideFlags.DontSave } };
                d.go.transform.SetParent(h, false);
                d.shadow = Part(d.go.transform, "Shadow", shadowBody, Vector3.zero);
                d.shadowTail = Part(d.shadow, "Tail", shadowTailM, new Vector3(0f, 0f, LifeLooks.FishTailRoot));
                d.side = Part(d.go.transform, "Side", sideBody, Vector3.zero);
                d.sideTail = Part(d.side, "Tail", sideTailM, LifeLooks.FishSideTailRoot);
                d.sideFin = Part(d.side, "Fin", sideFinM, LifeLooks.FishSideFinRoot);
                drawn.Add(d);
            }
            float surface = iced && ice != null ? ice.IceY + 0.0006f : eco.waterY + 0.0004f;
            for (int i = 0; i < drawn.Count; i++)
            {
                var d = drawn[i];
                if (i >= school.fish.Count) { if (d.go.activeSelf) d.go.SetActive(false); continue; }
                if (!d.go.activeSelf) d.go.SetActive(true);
                var f = school.fish[i];
                bool sh = f.ShadowDrawn && f.shadowScale > 0.001f, sd = f.SideDrawn && f.sideScale > 0.001f;
                d.shadow.gameObject.SetActive(sh);
                d.side.gameObject.SetActive(sd);
                if (sh)
                {
                    d.shadow.localPosition = new Vector3(f.at.x, surface, f.at.y);
                    d.shadow.localRotation = Quaternion.Euler(0f, f.heading * Mathf.Rad2Deg, 0f);
                    d.shadow.localScale = Vector3.one * Mathf.Max(0.001f, f.shadowScale);
                    d.shadowTail.localRotation = Quaternion.Euler(0f, f.tail, 0f);
                }
                if (sd)
                {
                    var outward = Outward(f.side);
                    var p = school.SidePosition(f) + outward * (BoxView.WaterFace + 0.001f);
                    var rot = Quaternion.LookRotation(outward, Vector3.up);
                    var alongAxis = FishSchool.AlongX(f.side) ? Vector3.right : Vector3.forward;
                    float sign = Vector3.Dot(rot * Vector3.right, alongAxis) >= 0f ? 1f : -1f;
                    d.side.localPosition = p;
                    d.side.localRotation = rot;
                    float sx = f.flipScale * sign * f.sideScale;
                    d.side.localScale = new Vector3(Mathf.Abs(sx) < 0.001f ? 0.001f * Mathf.Sign(sx == 0f ? 1f : sx) : sx, Mathf.Max(0.001f, f.sideScale), 1f);
                    d.sideTail.localRotation = Quaternion.Euler(0f, 0f, f.tail);
                    d.sideFin.localRotation = Quaternion.Euler(0f, 0f, f.fin);
                }
            }
        }

        public static Vector3 Outward(GlassSide s) => s switch
        {
            GlassSide.Left => Vector3.left, GlassSide.Right => Vector3.right, GlassSide.Front => Vector3.back, _ => Vector3.forward,
        };

        /// <summary>B16.e (FishingView): before a bite, a fish darts to the hook (world) and stays the nibble.</summary>
        public bool ToHook(Vector3 world, float stay)
        {
            if (school == null || box == null || box.Root == null) return false;
            var l = box.Root.InverseTransformPoint(world);
            return school.ToHook(new Vector2(l.x, l.z), stay);
        }

        // ---------------------------------------------------------------- debug (SliceDebugPanel's "Fish (#708)" block)
        /// <summary>Water, then fish placed, if the box has none yet (the cheats need a school to show).</summary>
        static string EnsureFish(SimCore sim)
        {
            if (sim == null || !sim.EcologyOn) return "no living land";
            if (sim.EcoPondCells == 0) EcoDebug.WaterBreaksNow();
            if (sim.EcoPondCells == 0) return "no pond";
            if (sim.EcoFish == 0)
            {
                EcoDebug.Table(0.3f);
                if (sim.DebugPlaceNearCamp(LifeSpecies.Fish) < 0) return "could not place the fish";
            }
            return null;
        }

        /// <summary>"Fish: show them at the glass": the camera to the pond's pane, two fish sent onto the wall.</summary>
        public static string DebugGlass()
        {
            var v = Instance; var sim = GameClock.Instance != null ? GameClock.Instance.Sim : null;
            string why = EnsureFish(sim);
            if (why != null) return why;
            if (v == null || v.school == null || v.school.fish.Count == 0) return "fish placed: try again in a moment (the school shows after the arrival)";
            int n = v.school.ToGlass(FishSchool.MaxOnWalls, out var side, out var focus);
            if (n == 0) return "the pond doesn't reach a pane the camera sees (no wall 0.06 u deep)";
            var cam = Camera.main != null ? Camera.main.GetComponent<BoxCamera>() : null;
            if (cam != null)
            {
                var world = v.box.Root.TransformPoint(new Vector3(focus.x, v.box.layout.eco.waterY - 0.03f, focus.y));
                float yaw = side == GlassSide.Front ? 8f : side == GlassSide.Left ? 55f : -30f;
                cam.EaseTo(world, yaw, 12f, 0.25f, 0.8f);
            }
            return $"{n} fish at the {side} pane";
        }

        /// <summary>"Fish: startle": a ring on the school: they dart and regroup.</summary>
        public static string DebugStartle()
        {
            var v = Instance;
            if (v == null || v.school == null || v.school.fish.Count == 0) return "no fish shown";
            var c = v.school.Centre;
            var world = v.box.Root.TransformPoint(new Vector3(c.x, v.box.layout.eco.waterY, c.y));
            if (HitFx.Enabled) HitFx.Puff(PuffKind.Ring, world, Vector3.up, 1f, true);
            int n = v.school.Startle(c, 0.6f);
            return $"{n} fish darted";
        }

        /// <summary>"Fish: the shadows": fish placed if none, the camera over the school.</summary>
        public static string DebugShadows()
        {
            var v = Instance; var sim = GameClock.Instance != null ? GameClock.Instance.Sim : null;
            string why = EnsureFish(sim);
            if (why != null) return why;
            if (v == null || v.school == null) return "no fish view";
            var c = v.school.Centre;
            var cam = Camera.main != null ? Camera.main.GetComponent<BoxCamera>() : null;
            if (cam != null) cam.EaseTo(v.box.Root.TransformPoint(new Vector3(c.x, v.box.layout.eco.waterY, c.y)), 20f, 55f, 0.3f, 0.8f);
            return $"{v.school.fish.Count} fish on the pond";
        }
    }
}
