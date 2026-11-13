using System.Collections.Generic;
using AquariumCiv.Game;
using AquariumCiv.Life;
using AquariumCiv.Sim;
using AquariumCiv.Sim.Eco;
using AquariumCiv.Sim.Settle;
using UnityEngine;
using Terrain = AquariumCiv.Sim.Settle.Terrain;

namespace AquariumCiv.View
{
    /// <summary>
    /// #618 the frozen pond. While <see cref="PondIce.Frozen"/> (the calendar's winter, any pond with water) a sheet of ice lies just over
    /// the water: it grows in from the shore over about a minute at the freeze and melts back to the shore at the thaw, pale blue-grey with a
    /// whiter rim, a little patchy cell by cell. Once it is whole, people may stand on it (<see cref="IceTop"/>: BoxView.SurfaceHeight and
    /// PeopleView ask) where the pond is big enough (<see cref="PondIce.OnIce"/>). Every fisher on the ice gets a hole where the line goes
    /// down (a dark disc with a ring of ice chips), opened over a second as they cut it; the holes stay until the thaw. Code-built, one
    /// mesh for the sheet (rebuilt only when the frozen share or the pond changes) and pooled holes.
    /// </summary>
    [DefaultExecutionOrder(6)]
    public sealed class IceView : MonoBehaviour
    {
        public static bool AutoCreate = true;
        public static IceView Instance { get; private set; }

        /// <summary>Real seconds for the ice to close over the pond (and to melt back).</summary>
        public const float FreezeSeconds = 50f;
        /// <summary>The sheet sits this far above the water (u).</summary>
        public const float Lift = 0.004f;
        /// <summary>The most holes shown; a hole's radius (u) and how long it takes to cut (s).</summary>
        public const int MaxHoles = 8;
        public const float HoleR = 0.026f, CutSeconds = 1.2f;
        /// <summary>The frozen share steps (the sheet is rebuilt at most this many times a freeze).</summary>
        const int Steps = 10;

        // pond ice: frosted and pale blue at the shore, clear and deeper blue-teal toward the middle (the water shows through), a few white patches of snow
        static readonly Color IceRim = new(0.74f, 0.86f, 0.93f), IceMid = new(0.56f, 0.75f, 0.88f), IceDeep = new(0.40f, 0.62f, 0.80f), IceSnow = new(0.86f, 0.92f, 0.96f), HoleCol = new(0.07f, 0.13f, 0.18f), ChipCol = new(0.93f, 0.96f, 0.98f);

        BoxView box;
        PeopleView peopleSeen;
        Transform holder, rootSeen;
        GameObject sheet;
        Mesh sheetMesh, holeMesh;
        int key = int.MinValue;
        float amount; // 0 open water .. 1 frozen over
        float iceY;
        readonly int[] dist = new int[SiteMap.N];
        int maxDist;
        readonly bool[] iced = new bool[SiteMap.N];

        sealed class Hole { public Transform t; public Vector2 at; public float born; public bool used; }
        readonly List<Hole> holes = new();
        readonly Dictionary<int, Hole> holeOf = new();

        // tests and the panel
        public float Amount => amount;
        public bool Frozen { get; private set; }
        public bool Whole => amount >= 0.999f;
        public int HolesShown { get { int n = 0; foreach (var h in holes) if (h.used) n++; return n; } }
        public int CellsIced { get; private set; }

        SimCore Sim => GameClock.Instance != null ? GameClock.Instance.Sim : null;

        public static IceView Ensure(BoxView b)
        {
            if (b == null) return null;
            var v = b.GetComponent<IceView>();
            return v != null ? v : b.gameObject.AddComponent<IceView>();
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
            if (peopleSeen != null) peopleSeen.Fishing -= OnFisher;
            peopleSeen = null;
            if (holder != null) Destroy(holder.gameObject);
            if (sheetMesh != null) Destroy(sheetMesh);
            if (holeMesh != null) Destroy(holeMesh);
            holder = null; sheet = null; sheetMesh = null; holeMesh = null; holes.Clear(); holeOf.Clear(); key = int.MinValue;
        }

        void Update()
        {
            var sim = Sim;
            var pv = PeopleView.Find();
            if (pv != peopleSeen)
            {
                if (peopleSeen != null) peopleSeen.Fishing -= OnFisher;
                peopleSeen = pv;
                if (pv != null) pv.Fishing += OnFisher;
            }
            if (sim == null || !sim.EcologyOn || box == null || box.Root == null || box.layout.eco.pondCells <= 0 || pv == null)
            {
                Frozen = false; amount = 0f; Hide(); return;
            }
            if (rootSeen != box.Root) { holder = null; sheet = null; holes.Clear(); holeOf.Clear(); key = int.MinValue; rootSeen = box.Root; }
            Frozen = PondIce.Frozen(sim, pv.Now.day);
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.1f);
            amount = Mathf.MoveTowards(amount, Frozen ? 1f : 0f, dt / FreezeSeconds);
            if (PondIce.Force.HasValue) amount = PondIce.Force.Value ? 1f : 0f; // the cheat shows it at once
            var eco = box.layout.eco;
            iceY = eco.waterY + Lift;
            int step = Mathf.CeilToInt(amount * Steps);
            int k = unchecked((int)eco.WaterHash() * 31 + step);
            if (k != key) { Build(eco, step / (float)Steps); key = k; }
            if (sheet != null) sheet.SetActive(step > 0);
            if (!Frozen && amount <= 0f) ClearHoles();
            StepHoles();
        }

        void Hide()
        {
            if (sheet != null) sheet.SetActive(false);
            ClearHoles();
            CellsIced = 0;
        }

        Transform Holder()
        {
            if (holder == null)
            {
                holder = new GameObject("Ice") { hideFlags = HideFlags.DontSave }.transform;
                holder.SetParent(box.Root, false);
            }
            return holder;
        }

        // ---------------------------------------------------------------- the sheet

        /// <summary>Steps from the shore for every cell near the water (0 on the bank cells the water sheet reaches, 1 for water at the edge, ...).</summary>
        void Distances(EcoLayer eco)
        {
            var q = new Queue<int>();
            for (int c = 0; c < SiteMap.N; c++)
            {
                dist[c] = -1;
                if (!eco.NearWater(c)) continue;
                if (!eco.WaterCell(c)) { dist[c] = 0; q.Enqueue(c); }
            }
            maxDist = 0;
            while (q.Count > 0)
            {
                int c = q.Dequeue(), i = SiteMap.I(c), j = SiteMap.J(c);
                for (int n = 0; n < 4; n++)
                {
                    int a = i + (n == 0 ? 1 : n == 1 ? -1 : 0), b = j + (n == 2 ? 1 : n == 3 ? -1 : 0);
                    if (a < 0 || b < 0 || a >= SiteMap.NX || b >= SiteMap.NZ) continue;
                    int o = SiteMap.Index(a, b);
                    if (dist[o] >= 0 || !eco.WaterCell(o)) continue;
                    dist[o] = dist[c] + 1; maxDist = Mathf.Max(maxDist, dist[o]);
                    q.Enqueue(o);
                }
            }
            for (int c = 0; c < SiteMap.N; c++) if (dist[c] < 0 && eco.WaterCell(c)) { dist[c] = maxDist + 1; } // a pond with no bank in the box (all water)
        }

        void Build(EcoLayer eco, float share)
        {
            Distances(eco);
            if (sheet != null) Destroy(sheet);
            if (sheetMesh != null) Destroy(sheetMesh);
            sheet = null; sheetMesh = null;
            System.Array.Clear(iced, 0, iced.Length);
            CellsIced = 0;
            if (share <= 0f) return;
            var mb = new MeshBuilder(box.seed + 618) { jitter = 0f };
            float h = SiteMap.Cell * 0.5f + 0.003f;
            float reach = share >= 1f ? float.MaxValue : share * (maxDist + 1);
            for (int c = 0; c < SiteMap.N; c++)
            {
                if (dist[c] < 0 || dist[c] > reach) continue;
                float x = SiteMap.CXc(c), z = SiteMap.CZc(c);
                if (box.land.Height(x, z) > iceY + 0.02f) continue; // a bank cell well above the water: the ground hides it anyway
                iced[c] = true; CellsIced++;
                float v = (Terrain.Hash3(box.seed + 618, c, 1) - 0.5f) * 0.05f;
                float deep = Mathf.Clamp01((dist[c] - 1) / 4f);
                var col = deep < 0.5f ? Color.Lerp(IceRim, IceMid, deep * 2f) : Color.Lerp(IceMid, IceDeep, (deep - 0.5f) * 2f);
                if (Terrain.Hash3(box.seed + 618, c, 2) < 0.06f) col = Color.Lerp(col, IceSnow, 0.4f); // a drift of snow on the ice
                col = new Color(col.r + v, col.g + v, col.b + v * 0.6f);
                var a = new Vector3(x - h, iceY, z - h); var b = new Vector3(x - h, iceY, z + h); var cc = new Vector3(x + h, iceY, z + h); var d = new Vector3(x + h, iceY, z - h);
                if (Vector3.Cross(b - a, cc - a).y > 0f) mb.Quad(a, b, cc, d, col); else mb.Quad(d, cc, b, a, col);
            }
            sheetMesh = mb.ToMesh("Ice");
            sheetMesh.hideFlags = HideFlags.DontSave;
            sheet = new GameObject("IceSheet") { hideFlags = HideFlags.DontSave };
            sheet.transform.SetParent(Holder(), false);
            sheet.AddComponent<MeshFilter>().sharedMesh = sheetMesh;
            var r = sheet.AddComponent<MeshRenderer>();
            r.sharedMaterial = box.SolidMaterial;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        /// <summary>Is there whole ice to stand on at (x, z), and its top? Only on a pond big enough (<see cref="PondIce.OnIce"/>) and
        /// once the sheet is whole: nobody walks onto thin ice at the edges of a freeze.</summary>
        public bool IceTop(float x, float z, out float y)
        {
            y = 0f;
            if (!Frozen || !Whole || sheet == null) return false;
            int c = SiteMap.CellAt(x, z);
            if (c < 0 || !iced[c]) return false;
            var sim = Sim; var pv = peopleSeen;
            if (sim == null || pv == null || !PondIce.OnIce(sim, pv.Now.day)) return false;
            y = iceY;
            return true;
        }

        public bool OnIce(Vector2 p) => IceTop(p.x, p.y, out _);
        /// <summary>The ice's top (box units).</summary>
        public float IceY => iceY;

        /// <summary>The planner's people may go on the ice now (frozen whole and big enough).</summary>
        public bool PeopleOnIce => Frozen && Whole && sheet != null && Sim != null && peopleSeen != null && PondIce.OnIce(Sim, peopleSeen.Now.day);

        // ---------------------------------------------------------------- the holes

        void OnFisher(PersonRef who, PegDollView v, float s, float left, Vector3 line)
        {
            if (!PeopleOnIce || box == null || box.Root == null) return;
            var local = box.Root.InverseTransformPoint(line);
            var at = new Vector2(local.x, local.z);
            if (!IceTop(at.x, at.y, out _)) return;
            // one hole per fisher: the line sways and jerks on a bite, but it goes down the same hole
            if (holeOf.TryGetValue(who.Key, out var mine) && mine.used && (mine.at - at).sqrMagnitude < 0.12f * 0.12f) return;
            foreach (var h in holes) if (h.used && (h.at - at).sqrMagnitude < 0.1f * 0.1f) { holeOf[who.Key] = h; return; } // back at a hole cut before
            var hole = Take();
            hole.at = at; hole.born = Time.unscaledTime; hole.used = true;
            hole.t.localPosition = new Vector3(at.x, iceY + 0.0015f, at.y);
            hole.t.gameObject.SetActive(true);
            holeOf[who.Key] = hole;
        }

        Hole Take()
        {
            foreach (var h in holes) if (!h.used) return h;
            if (holes.Count < MaxHoles)
            {
                var g = new GameObject("IceHole") { hideFlags = HideFlags.DontSave };
                g.transform.SetParent(Holder(), false);
                g.AddComponent<MeshFilter>().sharedMesh = HoleMesh();
                var r = g.AddComponent<MeshRenderer>();
                r.sharedMaterial = box.SolidMaterial;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                var h = new Hole { t = g.transform };
                holes.Add(h);
                return h;
            }
            Hole oldest = holes[0];
            foreach (var h in holes) if (h.born < oldest.born) oldest = h;
            foreach (var kv in new List<KeyValuePair<int, Hole>>(holeOf)) if (kv.Value == oldest) holeOf.Remove(kv.Key);
            return oldest;
        }

        void StepHoles()
        {
            foreach (var h in holes)
            {
                if (!h.used) continue;
                float u = Mathf.Clamp01((Time.unscaledTime - h.born) / CutSeconds);
                float s = Mathf.SmoothStep(0.15f, 1f, u);
                h.t.localScale = new Vector3(s, 1f, s);
            }
        }

        void ClearHoles()
        {
            foreach (var h in holes) { h.used = false; if (h.t != null) h.t.gameObject.SetActive(false); }
            holeOf.Clear();
        }

        Mesh HoleMesh()
        {
            if (holeMesh != null) return holeMesh;
            var mb = new MeshBuilder(618) { jitter = 0f };
            const int seg = 14;
            for (int i = 0; i < seg; i++)
            {
                float a0 = i * Mathf.PI * 2f / seg, a1 = (i + 1) * Mathf.PI * 2f / seg;
                var p0 = new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)); var p1 = new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1));
                // the dark water, then the ring of chipped ice round it (a little raised, uneven)
                mb.Tri(Vector3.zero, p1 * HoleR, p0 * HoleR, HoleCol);
                float r0 = HoleR * (1.45f + 0.18f * Mathf.Sin(i * 2.7f)), r1 = HoleR * (1.45f + 0.18f * Mathf.Sin((i + 1) * 2.7f));
                var q0 = p0 * r0 + Vector3.up * 0.002f; var q1 = p1 * r1 + Vector3.up * 0.002f;
                mb.Quad(p0 * HoleR, p1 * HoleR, q1, q0, ChipCol);
            }
            holeMesh = mb.ToMesh("IceHole");
            holeMesh.hideFlags = HideFlags.DontSave;
            return holeMesh;
        }

        // ---------------------------------------------------------------- debug (SliceDebugPanel's #618 block)

        /// <summary>"Ice: on / off / season": forces the freeze (and the ducks' leave) or gives it back to the calendar.</summary>
        public static string DebugIce(bool? on)
        {
            PondIce.Force = on;
            var sim = GameClock.Instance != null ? GameClock.Instance.Sim : null;
            if (sim == null) return "no sim";
            string size = sim.EcoPondCells <= 0 ? "no pond: nothing to freeze" : PondIce.BigEnough(sim) ? $"pond {sim.EcoPondCells} cells: people may go on the ice" : $"pond {sim.EcoPondCells} cells (< {sim.Config.eco.iceActivityCells}): ice as a look only";
            return (on.HasValue ? (on.Value ? "ice forced on" : "ice forced off") : "ice follows the season") + "; " + size;
        }
    }
}
