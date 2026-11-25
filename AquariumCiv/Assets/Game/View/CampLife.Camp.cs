// #692 part 5 (docs/design/animation-specs.md "#692 part 5: the rest of the camp", B40-B53): CampLife's side of the camp's other things: the
// tents, the stash, the racks, the thicket, wild trees, the pond, mist puddles, reeds, the stream bed, the hill, paths, the remembering stone,
// the tents' lanterns, weeds and the rope swing. Which use starts (the D rules: life uses borrow free time only; how often by the view day's
// roll; one use per thing at a time; B11's 30 s gap), the cheats, and what the uses leave, drawn here: a lantern on a pole at each tent's door
// (lit at dusk), toys and beads on the poles, meat strips on the fish rack, gifts at the stone, the dam's pool, leaf boats, the skipping stone,
// moths round a lantern, crowns and purple mouths, the rope swing. View-only and not saved [est] (a reload clears them), except the weed
// pulled (the cell clears in the sim) and the fish a kid sneaks (the rack's count drops). The timelines: View/Beats/CampScenes5.cs, 5b.cs.
using System.Collections.Generic;
using AquariumCiv.Life;
using AquariumCiv.Sim;
using AquariumCiv.Sim.Eco;
using AquariumCiv.Sim.Settle;
using Terrain = AquariumCiv.Sim.Settle.Terrain;
using UnityEngine;

namespace AquariumCiv.View
{
    public sealed partial class CampLife
    {
        const int ThingStash = 21, ThingFishRack = 22, ThingHideRack = 23, ThingThicket = 24, ThingBank = 26, ThingPuddle = 27, ThingReeds = 28,
            ThingStream = 29, ThingHill = 30, ThingPath = 31, ThingStone = 32, ThingSwing = 33, ThingTentBase = 2000, ThingWildBase = 40000;
        /// <summary>B42.d [est]: a kid sneaks a fish one day in four; B40.b, B47.b, B48.b, B51 are rare.</summary>
        public const float Rare = 0.25f, VeryRare = 0.12f;

        static bool IsCampUse(CampUse u) => u >= CampUse.TentPeek && u <= CampUse.Swing || u == CampUse.SkyLook; // #687: the look up for rain
        /// <summary>#692 p5 (the Editor pass: rings, the boat and the skipping stone sank under a raised pond): the pond's own surface height.</summary>
        float PondY { get { var b = LandBox; return b != null && b.layout.eco.pondCells > 0 ? b.layout.eco.waterY : Land.WaterY; } }

        // ---- what the uses leave (view-only)
        sealed class TentThing { public CampMark kind; public float spinAt = -99f; }
        readonly Dictionary<int, List<TentThing>> tentThings = new();
        readonly Dictionary<int, long> tentToyDone = new();
        readonly Dictionary<int, (long day, float at)> lit = new();
        readonly HashSet<int> peekedToday = new();
        int meatStrips; long meatDay = long.MinValue;
        readonly List<(Vector2 p, float at)> stripTouches = new();
        readonly List<(int key, CampMark kind, float until, float fadeAt)> worn5 = new();
        readonly List<(Vector2 p, int kind, float at)> gifts = new();
        sealed class Dam { public Vector2 p; public float r, at, brokeAt = -1f; public readonly List<Vector2> stones = new(); }
        readonly List<Dam> dams = new();
        readonly List<(Vector2 p, float at)> damStones = new();
        sealed class Boat { public Vector2 start, dir; public float push, len; public long day; }
        readonly List<Boat> boats = new();
        readonly Dictionary<CampScene, (Vector2 bank, Vector2 toWater)> waterOf = new();
        /// <summary>#692 p5 retakes: each climb's tree, the way to the camera and the seat's height, for the low bough drawn under the climber.</summary>
        readonly Dictionary<CampScene, (Vector2 trunk, Vector2 toCam, float seatH, float seatOut)> climbOf = new();
        float mothClapAt = -99f;
        // the swing (one per box)
        bool swingRope, swingHung, swingTrunkSet; int swingCell = -1; Vector2 swingPivot, swingFwd, swingTrunk;
        readonly List<(float from, float len)> swingRides = new();
        readonly Dictionary<CampUse, float> triedAt = new();
        readonly List<Transform> campPool = new();
        readonly Dictionary<int, Mesh> campMeshes = new();
        long campDay = long.MinValue;
        /// <summary>The cheat's skip alternates: three skips, then a kid's plop (B45.a plays both ways).</summary>
        public static bool ForcePlop;

        void ResetCamp()
        {
            tentThings.Clear(); tentToyDone.Clear(); lit.Clear(); peekedToday.Clear(); meatStrips = 0; meatDay = long.MinValue; stripTouches.Clear();
            worn5.Clear(); gifts.Clear(); dams.Clear(); damStones.Clear(); boats.Clear(); waterOf.Clear(); climbOf.Clear(); mothClapAt = -99f;
            swingRope = swingHung = false; swingCell = -1; swingRides.Clear(); triedAt.Clear(); campDay = long.MinValue;
        }

        // ================================================================== what the camp offers
        /// <summary>A tent: its door and the way out of it (the sim's), its shown side (<see cref="CampGeo5.ShownSide"/>: where the peek, the toy,
        /// the doorway sit and the lantern pole play, so the home camera sees them), and the pole.</summary>
        /// <remarks>#692 p5 notes: no lantern poles in the camp; <c>pole</c> is now where a toy hangs on the canvas (the lantern round is houses-only).</remarks>
        struct Tent { public int id; public Vector2 door, out1, pole, centre, look, lookOut; public bool hem; public float r, h; }

        /// <summary>The camp's tents (huts on the settle lots): the door, the way out of it, and the lantern pole beside it [est].</summary>
        List<Tent> Tents()
        {
            var r = new List<Tent>();
            var st = sim?.Settlement;
            if (st == null || sim.State.era != Era.Camp) return r;
            foreach (var l in st.Lots)
            {
                if (l.retired || l.kind != LotKind.Hut) continue;
                l.Front(out float fx, out float fz);
                var out1 = new Vector2(fx, fz);
                var c = new Vector2(l.x, l.z);
                var door = l.door >= 0 ? new Vector2(l.doorX, l.doorZ) : c + out1 * (l.d * 0.5f);
                if ((door - c).sqrMagnitude < 1e-4f) door = c + out1 * (l.d * 0.5f);
                // #692 p5 retakes: the uses and the pole on the side the home camera sees (a door facing away hid them behind the tent)
                var toCam = CampGeo5.HomeToCam;
                CampGeo5.ShownSide(c, door, out1, toCam, CampGeo5.TentRadius(l.w, l.d), out var look, out var lookOut, out bool hem);
                float tr = CampGeo5.TentRadius(l.w, l.d), th = CampGeo5.TentHeight(l.w, l.d);
                r.Add(new Tent { id = l.id, door = door, out1 = out1, pole = CampGeo5.HangPoint(c, toCam, tr, th), centre = c, look = look, lookOut = lookOut, hem = hem, r = tr, h = th });
            }
            return r;
        }

        bool NearestTent(Vector2 to, float reach, out Tent tent, System.Func<Tent, bool> ok = null)
        {
            tent = default; float bd = reach;
            foreach (var t in Tents()) { float d = Mathf.Min((t.door - to).magnitude, Mathf.Min((t.look - to).magnitude, (t.pole - to).magnitude)); if (d < bd && (ok == null || ok(t))) { bd = d; tent = t; } } // the door, its shown side or its pole
            return bd < reach;
        }

        bool PileAt(out Vector2 pile)
        {
            pile = default;
            if (planner == null || !planner.Positions.TryGetPosition(CampStash.Pile, out float x, out float z)) return false;
            pile = new Vector2(x, z); return true;
        }

        /// <summary>The fish rack (when it is drawn): its middle, the way along its bar, and the fish on it.</summary>
        bool FishRackAt(out Vector2 mid, out Vector2 along, out int fish)
        {
            mid = along = default; fish = 0;
            if (planner == null || FishRackView.Instance == null || !FishRackView.Instance.Visible || !planner.TryGetFishRack(now, out var look)) return false;
            mid = new Vector2(look.x, look.z); along = new Vector2(look.dirX, look.dirZ); fish = look.Fish;
            return true;
        }

        /// <summary>The fish rack's hang points (box x, z) along its bar, the first <paramref name="n"/>.</summary>
        List<Vector2> RackPoints(Vector2 mid, Vector2 along, int n)
        {
            var r = new List<Vector2>();
            along = along.sqrMagnitude > 1e-6f ? along.normalized : Vector2.right;
            for (int i = 0; i < n; i++) { var h = FishRackPieces.HangPoint(i); r.Add(mid + along * h.z); }
            r.Sort((a, b) => Vector2.Dot(a - mid, along).CompareTo(Vector2.Dot(b - mid, along)));
            return r;
        }

        bool HideRackAt(out Vector2 mid, out Vector2 along)
        {
            mid = along = default;
            if (planner == null || !planner.HideFrame(out float x, out float z, out float dx, out float dz) || planner.HidesOnFrame(now) <= 0) return false;
            mid = new Vector2(x, z); along = new Vector2(dx, dz); return true;
        }

        /// <summary>A grown wild tree near <paramref name="near"/> (never the Keeper's tree), clear of the glass.</summary>
        bool WildTree(Vector2 near, float reach, out int cell, out Vector2 trunk, out float crown, System.Func<int, Vector2, bool> ok = null)
        {
            cell = -1; trunk = default; crown = 0f; float bd = reach;
            var box = LandBox;
            if (box == null || !box.layout.eco.on) return false;
            var eco = box.layout.eco;
            lock (box.TreeSpots)
                foreach (var kv in box.TreeSpots)
                {
                    if (kv.Key == eco.treeCell || eco.At(kv.Key) != EcoLayer.Tree) continue;
                    var p = new Vector2(kv.Value.pos.x, kv.Value.pos.z);
                    float d = (p - near).magnitude;
                    if (d >= bd || !ClearOfGlass(p) || ok != null && !ok(kv.Key, p)) continue;
                    bd = d; cell = kv.Key; trunk = p; crown = kv.Value.r;
                }
            return cell >= 0;
        }

        static readonly int[] Ni = { 1, -1, 0, 0 }, Nj = { 0, 0, 1, -1 };

        /// <summary>The pond's bank nearest <paramref name="near"/>: a dry cell beside open water (clear of the glass), and the way to the water.</summary>
        bool BankNear(Vector2 near, float reach, out Vector2 bank, out Vector2 toWater, bool reedsOnly = false)
        {
            bank = toWater = default;
            var box = LandBox;
            if (box == null || !box.layout.eco.on) return false;
            var eco = box.layout.eco;
            float bd = reach;
            for (int c = 0; c < SiteMap.N; c++)
            {
                byte cv = eco.At(c);
                if (reedsOnly ? cv != EcoLayer.Reeds : cv == EcoLayer.Water || cv == EcoLayer.Reeds) continue;
                var p = new Vector2(SiteMap.CXc(c), SiteMap.CZc(c));
                if (!Terrain.InBox(p.x, p.y, Terrain.GlassMargin + 0.15f)) continue;
                float d = (p - near).magnitude + (ClearOfGlass(p) ? 0f : 1.5f); // #692 p5 (the Editor pass): clear of the front glass when it can be
                if (d >= bd) continue;
                if (!reedsOnly && box.land.IsWater(p.x, p.y)) continue;
                int i = SiteMap.I(c), j = SiteMap.J(c);
                var w = Vector2.zero;
                for (int k = 0; k < 4; k++)
                {
                    int ii = i + Ni[k], jj = j + Nj[k];
                    if (ii < 0 || jj < 0 || ii >= SiteMap.NX || jj >= SiteMap.NZ) continue;
                    if (eco.At(SiteMap.Index(ii, jj)) == EcoLayer.Water) w += new Vector2(Ni[k], Nj[k]);
                }
                if (w.sqrMagnitude < 1e-4f) continue;
                bd = d; bank = p; toWater = w.normalized;
            }
            if (bd >= reach) return false;
            toWater = OpenWaterWay(bank, toWater, eco); // #692 p5 retakes: toward the open water, not along the shore
            bank = AtShore(bank, toWater); // the reeds too: they stand in the water's edge
            return true;
        }

        /// <summary>#692 p5 retakes (B45.a: the stone skipped along the shore and its rings fell on sand): the way to the open water from a bank
        /// cell, the sum of the ways to every water cell within 3 cells (the nearest neighbour alone often ran along the bank).</summary>
        static Vector2 OpenWaterWay(Vector2 bank, Vector2 fallback, EcoLayer eco)
        {
            int c0 = SiteMap.CellAt(bank.x, bank.y), i0 = SiteMap.I(c0), j0 = SiteMap.J(c0);
            var sum = Vector2.zero;
            for (int dj = -3; dj <= 3; dj++)
                for (int di = -3; di <= 3; di++)
                {
                    int i = i0 + di, j = j0 + dj;
                    if ((di == 0 && dj == 0) || i < 0 || j < 0 || i >= SiteMap.NX || j >= SiteMap.NZ) continue;
                    if (eco.At(SiteMap.Index(i, j)) != EcoLayer.Water) continue;
                    sum += new Vector2(di, dj).normalized;
                }
            return sum.sqrMagnitude > 1e-4f ? sum.normalized : fallback;
        }

        /// <summary>#692 p5 retakes (the user's review: the leaf boat and the feet never reached the water): a bank cell's middle can sit 0.2 u up the
        /// slope from where the pond shows. Walk along <paramref name="toWater"/> to the first ground under the pond's surface and stand
        /// <see cref="CampGeo5.ShoreBack"/> short of it (the cell's middle when the water is not within 0.6 u).</summary>
        Vector2 AtShore(Vector2 bank, Vector2 toWater)
        {
            var box = LandBox;
            if (box == null) return bank;
            return CampGeo5.ToShore(bank, toWater, box.WaterDrawnAt);
        }

        /// <summary>The mist puddles near <paramref name="near"/>, nearest first.</summary>
        List<(Vector2 p, float r)> PuddlesNear(Vector2 near, float reach)
        {
            var got = new List<(float d, Vector2 p, float r)>();
            if (sim == null || !sim.EcologyOn) return new List<(Vector2, float)>();
            var cells = new List<int>();
            sim.EcoPuddleCells(cells, 0.2f);
            foreach (int c in cells)
            {
                var p = new Vector2(SiteMap.CXc(c), SiteMap.CZc(c));
                float d = (p - near).magnitude;
                if (d > reach || !ClearOfGlass(p)) continue;
                bool close = false; foreach (var g in got) if ((g.p - p).magnitude < 0.2f) close = true;
                if (!close) got.Add((d, p, Mathf.Lerp(0.05f, 0.1f, Mathf.Clamp01(sim.EcoPuddleAt(c)))));
            }
            got.Sort((x, y) => x.d.CompareTo(y.d));
            var r = new List<(Vector2 p, float r)>();
            foreach (var g in got) r.Add((g.p, g.r));
            return r;
        }

        /// <summary>The thicket's edge facing the meadow (toward the nearest rabbit when one is out): a bush near <paramref name="near"/>.</summary>
        bool ThicketEdge(Vector2 near, float reach, bool needRabbit, out Vector2 edge, out Vector2 bush, out Vector2 toMeadow)
        {
            edge = bush = toMeadow = default;
            var box = LandBox;
            if (box == null || !box.layout.eco.on) return false;
            var eco = box.layout.eco;
            float bd = reach; float br = 0.08f;
            lock (box.BushSpots)
                foreach (var kv in box.BushSpots)
                {
                    if (eco.At(kv.Key) != EcoLayer.Bush) continue;
                    var p = new Vector2(kv.Value.pos.x, kv.Value.pos.z);
                    float d = (p - near).magnitude;
                    if (d >= bd || !ClearOfGlass(p)) continue;
                    bd = d; bush = p; br = kv.Value.r;
                }
            if (bd >= reach) return false;
            var rv = RabbitView.Instance;
            bool rabbit = false;
            if (rv != null)
            {
                int id = rv.NearestShown(bush.x, bush.y);
                if (id >= 0 && rv.TryGetPosition(id, out var rl) && (new Vector2(rl.x, rl.z) - bush).magnitude < 1.2f) { toMeadow = Dir2(bush, new Vector2(rl.x, rl.z)); rabbit = true; }
            }
            if (!rabbit) { if (needRabbit) return false; toMeadow = Dir2(bush, TowardCamera(bush)); }
            edge = bush + toMeadow * (br * 0.7f);
            return true;
        }

        bool LandSpot(PlaceKind kind, int index, out Vector2 at)
        {
            at = default;
            if (planner == null || !planner.Positions.TryGetPosition(AquariumCiv.Life.Place.Land(kind, index), out float x, out float z)) return false;
            at = new Vector2(x, z); return Terrain.InBox(x, z, 0.1f);
        }

        /// <summary>A short straight bit of path near the camp (both ends and the middle on the path), 0.5–0.8 u long.</summary>
        bool PathNear(Vector2 near, out Vector2 a, out Vector2 b)
        {
            a = b = default;
            var on = RackSites.PathTest(sim);
            if (on == null) return false;
            var cells = new List<Vector2>();
            for (int c = 0; c < SiteMap.N; c++)
            {
                float x = SiteMap.CXc(c), z = SiteMap.CZc(c);
                var p = new Vector2(x, z);
                float d = (p - near).magnitude;
                if (d < 0.3f || d > 2f || !ClearOfGlass(p) || !on(x, z)) continue;
                cells.Add(p);
            }
            cells.Sort((x, y) => (x - near).sqrMagnitude.CompareTo((y - near).sqrMagnitude));
            float best = 0f;
            for (int i = 0; i < cells.Count && i < 30; i++)
                for (int k = 0; k < cells.Count; k++)
                {
                    float l = (cells[k] - cells[i]).magnitude;
                    if (l < 0.5f || l > 0.8f || l <= best) continue;
                    var m = (cells[k] + cells[i]) * 0.5f;
                    if (!on(m.x, m.y) || !on(Vector2.Lerp(cells[i], cells[k], 0.25f).x, Vector2.Lerp(cells[i], cells[k], 0.25f).y) || !on(Vector2.Lerp(cells[i], cells[k], 0.75f).x, Vector2.Lerp(cells[i], cells[k], 0.75f).y)) continue;
                    best = l; a = cells[i]; b = cells[k];
                }
            return best > 0f;
        }

        public string ThicketReady()
        {
            if (sim == null || meals == null || !meals.HasFire) return "no camp yet";
            int c = ThicketEdge(meals.Fire, 4f, false, out _, out _, out _) ? -2 : sim.DebugPlaceThicketNearCamp();
            int r = sim.EcoRabbitsShown() == 0 ? sim.DebugPlaceNearCamp(LifeSpecies.Rabbits) : -2;
            return $"thicket {(c == -2 ? "there" : c.ToString())}, rabbits {(r == -2 ? "there" : r.ToString())}";
        }

        public string ReedsReady()
        {
            if (sim == null || meals == null || !meals.HasFire) return "no camp yet";
            return BankNear(meals.Fire, 6f, out _, out _, true) ? "reeds there" : $"reeds grew: {sim.DebugReedsNear(meals.Fire.x, meals.Fire.y, 6)}";
        }

        /// <summary>#692 p5 (the Editor pass: a camp has no paths yet): the cheats' stand-in, the trodden way from the fire to the nearest tent's
        /// door (0.3 u out from the fire to 0.1 u short of the door) [est].</summary>
        bool TrodLine(out Vector2 a, out Vector2 b)
        {
            a = b = default;
            if (meals == null || !meals.HasFire) return false;
            var fire = meals.Fire;
            if (!NearestTent(fire, 4f, out var t)) return false;
            var d = Dir2(fire, t.door);
            a = fire + d * 0.3f; b = t.door - d * 0.1f;
            if ((b - a).magnitude < 0.3f) { b = a + d * 0.6f; }
            return true;
        }

        /// <summary>B51: the remembering stone: the name stone a choice left (as drawn), else the remembering spot [est].</summary>
        bool StoneAt(out Vector2 at, out bool nameStone) => StoneAt(out at, out nameStone, out _);

        /// <summary>#692 p5 notes: also the stone's drawn half-width (its renderers' bounds, box units; 0.03 for the remembering spot).</summary>
        bool StoneAt(out Vector2 at, out bool nameStone, out float halfW, bool second = false)
        {
            at = default; nameStone = false; halfW = 0.03f;
            var cv = ConsequencePropsView.Instance;
            if (sim != null && cv != null)
            {
                var standing = cv.Standing;
                int secondId = sim.StoryMarks.secondStoneId; // #722: the first stone, or (asked) the second one
                foreach (var p in sim.ConsequenceProps)
                    if (p.placed && p.kind == ConsequenceKind.NameStone && (p.id == secondId) == second && standing.TryGetValue(p.id, out var t) && t != null && t.gameObject.activeInHierarchy)
                    {
                        at = new Vector2(t.localPosition.x, t.localPosition.z); nameStone = true;
                        var rs = t.GetComponentsInChildren<Renderer>();
                        if (rs.Length > 0)
                        {
                            var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
                            float sc = t.parent != null ? Mathf.Max(1e-4f, t.parent.lossyScale.x) : 1f;
                            var c = t.parent != null ? t.parent.InverseTransformPoint(b.center) : b.center;
                            float hw = Mathf.Max(Mathf.Abs(c.x - at.x) + b.extents.x / sc, Mathf.Abs(c.z - at.y) + b.extents.z / sc);
                            halfW = Mathf.Clamp(hw, 0.02f, 0.4f);
                        }
                        return true;
                    }
            }
            return LandSpot(PlaceKind.Remembering, 0, out at);
        }

        /// <summary>B53.a: a creeping seedling on a tent's door cell, the fire ring or a path in the camp (−1 none).</summary>
        int WeedCell(out Vector2 at)
        {
            at = default;
            if (sim == null || !sim.EcologyOn || meals == null) return -1;
            var fire = meals.Fire;
            var on = RackSites.PathTest(sim);
            int best = -1; float bd = float.MaxValue;
            void Try(int c)
            {
                if (c < 0 || c >= SiteMap.N || sim.EcoCoverAt(c) != EcoCover.Seedling) return;
                var p = new Vector2(SiteMap.CXc(c), SiteMap.CZc(c));
                float d = (p - fire).magnitude;
                if (d < bd) { bd = d; best = c; }
            }
            foreach (var t in Tents()) { Try(SiteMap.CellAt(t.door.x, t.door.y)); Try(SiteMap.CellAt(t.look.x + t.lookOut.x * 0.06f, t.look.y + t.lookOut.y * 0.06f)); }
            for (int c = 0; c < SiteMap.N; c++)
            {
                var p = new Vector2(SiteMap.CXc(c), SiteMap.CZc(c));
                float d = (p - fire).magnitude;
                if (d < 0.35f || on != null && d < 1.6f && on(p.x, p.y)) Try(c);
            }
            if (best >= 0) at = new Vector2(SiteMap.CXc(best), SiteMap.CZc(best));
            return best;
        }

        // ---- B53.d the swing's tree and pivot
        bool SwingSpot(float reach, out int cell, out Vector2 pivot, out Vector2 fwd, out Vector2 trunk)
        {
            cell = -1; pivot = fwd = trunk = default;
            if (meals == null || !meals.HasFire) return false;
            var fire = meals.Fire;
            var box = LandBox;
            var path = RackSites.PathTest(sim);
            var tents = Tents();
            bool Bad(Vector2 p)
            {
                if (box != null && box.land.IsWater(p.x, p.y)) return true;
                if (path != null && path(p.x, p.y)) return true;
                foreach (var t in tents) if ((t.centre - p).magnitude < 0.25f) return true;
                return !Terrain.InBox(p.x, p.y, Terrain.GlassMargin + 0.1f);
            }
            int c0 = -1; Vector2 tr = default, pvt = default, fw = default;
            bool found = WildTree(fire, reach, out c0, out tr, out float crown, (c, p) =>
            {
                if (!CampGeo5.SwingTreeOk(false, true, (p - fire).magnitude) && reach <= CampGeo5.SwingReach) return false;
                var toCam = Dir2(p, TowardCamera(p));
                // #692 p5 retakes: out under the crown's edge and to the side as the camera sees it (0.07-0.12 straight toward the camera hugged
                // the trunk: the plank hid on the bark, the riders could not reach it), the arc turned 45 deg off the screen's across so both
                // ropes show apart and the swing still moves across the screen [est]
                var across = CameraAcross();
                var piv = p + (toCam * 0.6f + across * 0.8f).normalized * Mathf.Clamp(crownOf(c) * 1.0f, 0.14f, 0.2f);
                // #692 p5 notes 2 (the user: "rotate the swing 90 degrees, it's perpendicular to the branch"): both ropes hang along the branch
                // (trunk -> pivot), so the swing arcs at right angles to it; a few degrees of give if the straight arc is blocked
                var branch = (piv - p).sqrMagnitude > 1e-8f ? (piv - p).normalized : across;
                var perp = new Vector2(-branch.y, branch.x);
                if (Vector2.Dot(perp, toCam) < 0f) perp = -perp; // reshoot2: the front of the arc toward the camera, so the pusher stands behind the swing
                foreach (float deg in new[] { 0f, 10f, -10f, 20f, -20f })
                {
                    var f = Rotate(perp, deg);
                    if (CampGeo5.SwingSpotOk(piv, f, fire, Bad)) { pvt = piv; fw = f.normalized; return true; }
                }
                return false;
            });
            if (!found) return false;
            cell = c0; pivot = pvt; fwd = fw; trunk = tr;
            return true;
            float crownOf(int c) { if (box == null) return 0.15f; lock (box.TreeSpots) return box.TreeSpots.TryGetValue(c, out var v) ? v.r : 0.15f; }
        }

        // ================================================================== the triggers (each frame, in the camp era)
        void StepCamp(IReadOnlyList<PersonActivity> acts, float time)
        {
            if (now.day != campDay) { campDay = now.day; peekedToday.Clear(); }
            for (int i = stripTouches.Count - 1; i >= 0; i--) if (time - stripTouches[i].at > 2f || time < stripTouches[i].at) stripTouches.RemoveAt(i);
            if (meatStrips > 0 && now.day - meatDay >= CampGeo5.StripDays) meatStrips = 0;
            if (meals != null && meals.Eaters > 0) worn5.RemoveAll(w => (w.kind == CampMark.PurpleMouth || w.kind == CampMark.PurpleHands) && w.fadeAt < 0f && ViewHours - (w.until - 24f) > 0.05f); // the next meal wipes it
            if (sim != null && sim.EcologyOn && sim.State.eco.rabbitCatchDay == now.day && meatDay != now.day && Hour > 15f && !OnlyCheats) { meatStrips = 2; meatDay = now.day; } // [est] the day's catch: two strips hang beside the fish
            if (sim != null && sim.EcologyOn && sim.State.eco.deerHuntDay == now.day && sim.State.eco.deerHuntDone > 0 && meatDay != now.day && Hour > 15f && !OnlyCheats) { meatStrips = CampGeo5.MaxStrips; meatDay = now.day; }
            if (swingRope && swingCell >= 0 && LandBox != null && LandBox.layout.eco.At(swingCell) != EcoLayer.Tree) { swingRope = swingHung = false; swingCell = -1; } // the tree is gone: the swing with it
            if (OnlyCheats || meals == null || !meals.HasFire || sim == null || !sim.EcologyOn || planner == null) return;
            if (meals.Eaters > 0) return;
            float h = Hour;
            var season = LifeClock.SeasonOf(now.day);
            var weather = planner.WeatherOn(now.day);
            bool wet = weather is Weather.KeeperRain or Weather.Storm or Weather.Snow;
            bool day = h >= 9f && h < 16.5f, warm = season is Season.Spring or Season.Summer;
            bool Go(CampUse u, float chance, int thing)
            {
                if (doneToday.Contains(u) || Roll(u) >= chance || !LifeOk(u) || ThingBusy(thing)) return false;
                if (triedAt.TryGetValue(u, out float tr) && clock - tr < 3f && clock >= tr) return false; // a failed try waits 3 s (the spot scans)
                triedAt[u] = clock;
                return true;
            }
            var fire = meals.Fire;
            // B52.a: the lamplighter at dusk (work: every dusk)
            if (h >= 17.6f && h < 18.6f && Go(CampUse.LightLanterns, 1f, 0) && TryLanterns(acts, time, false) == null) return;
            // B40.a: kids peek out of the tents in the morning (often; once per tent a day)
            if (h >= 6.3f && h < 8.2f && !doneToday.Contains(CampUse.TentPeek) && Roll(CampUse.TentPeek) < Often && LifeOk(CampUse.TentPeek) && TryPeek(acts, time, false) == null) return;
            // B40.c: sitting in the doorway at dusk (often)
            if (h >= 17f && h < 19f && Go(CampUse.TentDoorSit, Often, 0) && TryDoorSit(acts, time, false) == null) return;
            // B49.a: the hill at dusk, in summer (often)
            if (season == Season.Summer && h >= 17f && h < 18.2f && Go(CampUse.HillDusk, Often, ThingHill) && TryHillDusk(acts, time, false) == null) return;
            // B52.b: kids chase moths at the fire's light, summer dusk (often). 2026-11-24: 17.5-19 h, before the bedtimes (19.2 h): at 19-20 h the
            // late-start rule refused nearly every try (a year's census: never)
            if (season == Season.Summer && h >= 17.5f && h < 18.3f && Go(CampUse.ChaseMoths, Often, 0) && Tried(CampUse.ChaseMoths, TryMoths(acts, time, false))) return;
            // B51: a gift at the stone, the morning of a feast (rare)
            if (h >= 7f && h < 10f && (sim.FestivalOn || sim.CalendarMonth == 9) && Go(CampUse.StoneGift, Rare, ThingStone) && TryGift(acts, time, false) == null) return;
            // B42.a: the racks tended mid-morning (work: often)
            if (h >= 9.5f && h < 11.5f && Go(CampUse.TendRacks, Often, ThingFishRack) && TryTend(acts, time, false) == null) return;
            // B50.b at dusk (2026-11-24, a year's census: the grown-ups are free from ~17 h): two coming in to the fire meet on the way and talk
            if (h >= 17f && h < 18.3f && Go(CampUse.PathChat, Often, ThingPath) && Tried(CampUse.PathChat, TryPathChat(acts, time, false))) return;
            // #687 (the thirst signs): in a dry spell (most of the grass thirsty) a free grown-up looks up at the glass for rain, 9-18.3 h
            if (h >= 9f && h < 18.3f && sim.EcoThirst >= ThirstyBox && Go(CampUse.SkyLook, Sometimes, 0) && Tried(CampUse.SkyLook, TrySkyLook(acts, time, false))) return;
            if (!day) return;
            // B53.d: the swing is hung once, a free afternoon the first view day the tree stands; then kids swing (play time only)
            if (!swingHung && h >= 13f && Go(CampUse.HangSwing, 1f, ThingSwing) && TryHangSwing(acts, time, false) == null) return;
            if (swingHung && !wet && season != Season.Winter && Go(CampUse.Swing, Sometimes, ThingSwing) && TrySwing(acts, time, false) == null) return;
            // B53.a: a weed pulled in the camp (sometimes; at most one a view day)
            if (Go(CampUse.PullWeed, Sometimes, 0) && TryWeed(acts, time, false) == null) return;
            // the stash, the racks
            if (season == Season.Autumn && Go(CampUse.CountStash, Sometimes, ThingStash) && TryCountStash(acts, time, false) == null) return;
            if (Go(CampUse.SnackSneak, Sometimes, ThingStash) && TrySneak(acts, time, false) == null) return;
            if (season is Season.Summer or Season.Autumn && Go(CampUse.BerryWipe, Often, ThingStash) && TryBerryWipe(acts, time, false) == null) return;
            if (Go(CampUse.CountFish, Sometimes, ThingFishRack) && TryCountFish(acts, time, false) == null) return;
            if (Go(CampUse.SneakFish, Rare, ThingFishRack) && TrySneakFish(acts, time, false) == null) return;
            if (Go(CampUse.DuckUnderHides, Sometimes, ThingHideRack) && TryDuckUnder(acts, time, false) == null) return;
            // tents
            if (Go(CampUse.TentToy, VeryRare, 0) && TryToy(acts, time, false) == null) return;
            // the thicket, wild trees
            if (Go(CampUse.ThicketPeek, Sometimes, ThingThicket) && TryThicket(acts, time, false) == null) return;
            if (season != Season.Winter && Go(CampUse.ClimbTree, Sometimes, 0) && TryClimb(acts, time, false) == null) return;
            if (season == Season.Summer && h >= 11.5f && h < 14.5f && Go(CampUse.TreeNap, Often, 0) && TryNap(acts, time, false) == null) return;
            if (Go(CampUse.TrunkLean, Often, 0) && TryLean(acts, time, false) == null) return;
            // the pond (not on ice in winter)
            if (season != Season.Winter)
            {
                if (Go(CampUse.SkipStones, Sometimes, ThingBank) && TrySkip(acts, time, false) == null) return;
                if (season == Season.Summer && Go(CampUse.LeafBoat, Sometimes, ThingBank) && TryBoat(acts, time, false) == null) return;
                if (season == Season.Summer && h >= 13f && Go(CampUse.FeetInWater, Sometimes, ThingBank) && TryFeet(acts, time, false) == null) return;
                if (Go(CampUse.CutReeds, Sometimes, ThingReeds) && h < 12f && TryReeds(acts, time, false) == null) return;
                if (Go(CampUse.ReedWhistle, VeryRare, ThingReeds) && TryReedWhistle(acts, time, false) == null) return;
            }
            // puddles after a mist
            if (Go(CampUse.PuddleRace, Often, ThingPuddle) && TryPuddleRace(acts, time, false) == null) return;
            if (Go(CampUse.PuddleLook, Sometimes, ThingPuddle) && TryPuddleLook(acts, time, false) == null) return;
            // the stream bed, the hill, paths
            if (Go(CampUse.FollowStream, Sometimes, ThingStream) && TryStream(acts, time, false) == null) return;
            if (Go(CampUse.BuildDam, VeryRare, ThingStream) && TryDam(acts, time, false) == null) return;
            if (Go(CampUse.HillRoll, Sometimes, ThingHill) && TryHillRoll(acts, time, false) == null) return;
            if (Go(CampUse.PathRace, Sometimes, ThingPath) && Tried(CampUse.PathRace, TryPathRace(acts, time, false))) return;
            if (Go(CampUse.PathChat, Often, ThingPath) && Tried(CampUse.PathChat, TryPathChat(acts, time, false))) return;
            // weeds and flowers: a crown (summer)
            if (season == Season.Summer && Go(CampUse.Crown, Sometimes, 0) && TryCrown(acts, time, false) == null) return;
        }

        // ================================================================== starting each use (natural and forced); null = started, else why not
        int Grown1(IReadOnlyList<PersonActivity> acts, Vector2 near, bool force, ICollection<int> not = null)
        {
            var g = Pick(acts, near, a => IsGrown(a), 1, force, not);
            if (g.Count > 0) return g[0];
            return force ? StageGrown(acts, near, not) : Nobody;
        }

        int Kid1(IReadOnlyList<PersonActivity> acts, Vector2 near, bool force, ICollection<int> not = null)
        {
            var k = Pick(acts, near, a => IsKid(a), 1, force, not);
            return k.Count > 0 ? k[0] : Nobody;
        }

        Vector2 ToCam(Vector2 p) => Dir2(p, TowardCamera(p));

        string TryPeek(IReadOnlyList<PersonActivity> acts, float time, bool force)
        {
            var fire = meals.Fire;
            Tent tent = default; int kid = Nobody;
            foreach (var t in Tents())
            {
                if (!force && peekedToday.Contains(t.id)) continue;
                int k = force ? Kid1(acts, t.door, true) : Nearest(acts, t.door, 0.6f, a => IsKid(a));
                if (k != Nobody) { tent = t; kid = k; break; }
            }
            if (kid == Nobody) return Tents().Count == 0 ? "no tents" : "no kid by a tent ('Two kids join the camp' first)";
            if (force && NearestTent(At(kid).Value, 9f, out var near)) tent = near;
            Add(Thing(CampScenes5.TentPeek(kid, tent.look, tent.lookOut, time), ThingTentBase + tent.id));
            peekedToday.Add(tent.id);
            if (peekedToday.Count >= Tents().Count) doneToday.Add(CampUse.TentPeek);
            return null;
        }

        string TryToy(IReadOnlyList<PersonActivity> acts, float time, bool force)
        {
            bool Room(Tent t) => !tentThings.TryGetValue(t.id, out var l) || l.Count < CampGeo5.TentThings;
            var fire = meals.Fire;
            if (!NearestTent(fire, 9f, out var tent, t => Room(t) && (force || !tentToyDone.ContainsKey(t.id)))) return "every tent has two things on its pole";
            int who = Kid1(acts, tent.door, force);
            if (who == Nobody) who = Grown1(acts, tent.door, force);
            if (who == Nobody) return "nobody free by a tent";
            bool beads = BeatHash.Pick(2, tent.id, (int)(today & 0x7fff), 6960) == 1;
            float standOut = tent.r - CampGeo5.CanvasR(tent.r, tent.h, CampGeo5.ToyH) + 0.05f; // at the canvas's foot, the toy over the head
            Add(Thing(CampScenes5.TentToy(who, At(who).Value, tent.pole, tent.centre, beads, time, CampGeo5.HomeToCam, standOut), ThingTentBase + tent.id));
            tentToyDone[tent.id] = today;
            doneToday.Add(CampUse.TentToy);
            return null;
        }

        /// <summary>#692 p5 notes: the people live in tents (B40.c and B52.a never play then).</summary>
        bool InTents => sim != null && sim.State.era == Era.Camp;

        string TryDoorSit(IReadOnlyList<PersonActivity> acts, float time, bool force)
        {
            if (!CampGeo5.AllowedNow(CampUse.TentDoorSit, InTents)) return "houses only: nobody sits in a tent's door (the user, 2026-11-16)";
            var fire = meals.Fire;
            int grown = Nobody; Tent tent = default;
            foreach (var t in Tents())
            {
                if (ThingBusy(ThingTentBase + t.id)) continue;
                int g = force ? Nobody : Nearest(acts, t.door, BorrowReach, a => IsGrown(a));
                if (g != Nobody) { grown = g; tent = t; break; }
            }
            if (force && grown == Nobody)
            {
                if (!NearestTent(fire, 9f, out tent, t => !ThingBusy(ThingTentBase + t.id))) return "no tents";
                grown = Grown1(acts, tent.door, true);
            }
            if (grown == Nobody) return "no grown-up free by a tent";
            int kid = Nobody;
            foreach (var n in sim.State.notables) if (n.alive && n.parentId == grown && n.age < 12f && Shown(acts, n.id) && (force || FreeKey(acts, n.id))) { kid = n.id; break; }
            if (kid == Nobody) kid = Kid1(acts, tent.door, force, new[] { grown });
            var watch = Vector2.Dot(fire - tent.look, tent.lookOut) > 0f ? fire : tent.look + tent.lookOut; // the fire, else (it is behind the tent) out over the camp [est]
            Add(Thing(CampScenes5.TentDoorSit(grown, kid, At(grown).Value, kid != Nobody ? At(kid).Value : default, tent.look, tent.lookOut, watch, force ? 10f : BeatHash.Range(10f, 18f, (int)today, tent.id, 6961), time, CampGeo5.HomeToCam), ThingTentBase + tent.id));
            doneToday.Add(CampUse.TentDoorSit);
            return null;
        }

        string TryCountStash(IReadOnlyList<PersonActivity> acts, float time, bool force)
        {
            if (!PileAt(out var pile)) return "no stash pile";
            var kids = Pick(acts, pile, a => IsKid(a), 2, force);
            if (kids.Count < 2) return $"only {kids.Count} kid(s) free ('Two kids join the camp' first)";
            var toCam = ToCam(pile); var across = new Vector2(-toCam.y, toCam.x);
            var baskets = new List<Vector2>();
            for (int i = 0; i < 5; i++) baskets.Add(pile + across * ((i - 2) * 0.035f) + toCam * (CampStash.PileExtent * 0.6f));
            Add(Thing(CampScenes5.CountThings(CampUse.CountStash, kids[0], kids[1], At(kids[0]).Value, At(kids[1]).Value, baskets, toCam, time), ThingStash));
            doneToday.Add(CampUse.CountStash);
            return null;
        }

        string TrySneak(IReadOnlyList<PersonActivity> acts, float time, bool force)
        {
            if (!PileAt(out var pile)) return "no stash pile";
            // a grown-up at the stash (sorting, carrying) who turns: else (the cheat) the nearest grown-up comes over
            int grown = Nobody;
            foreach (var a in acts) if (IsGrown(a) && !byKey.ContainsKey(a.who.Key) && a.place.kind == PlaceKind.Stash && At(a.who.Key).HasValue) { grown = a.who.Key; break; }
            if (grown == Nobody && !force) return "nobody at the stash";
            if (grown == Nobody) grown = Grown1(acts, pile, true);
            if (grown == Nobody) return "no grown-up";
            int sneak = Kid1(acts, pile, force, new[] { grown });
            if (sneak == Nobody) return "no kid free";
            Add(Thing(CampScenes5.SnackSneak(sneak, At(sneak).Value, grown, At(grown).Value, pile, ToCam(pile), time), ThingStash));
            doneToday.Add(CampUse.SnackSneak);
            return null;
        }

        string TryBerryWipe(IReadOnlyList<PersonActivity> acts, float time, bool force)
        {
            if (!PileAt(out var pile)) return "no stash pile";
            int kid = Kid1(acts, pile, force);
            if (kid == Nobody) return "no kid free ('Two kids join the camp' first)";
            int parent = ParentOf(kid);
            if (parent <= 0 || !Shown(acts, parent)) parent = Grown1(acts, pile, force, new[] { kid });
            Add(Thing(CampScenes5.BerryWipe(kid, At(kid).Value, parent, parent != Nobody ? At(parent).Value : default, pile, ToCam(pile), time), ThingStash));
            doneToday.Add(CampUse.BerryWipe);
            return null;
        }

        string RackReady(bool force)
        {
            if (FishRackAt(out _, out _, out int fish) && fish > 0) return null;
            if (!force) return "no fish on the rack";
            FishRackView.DebugRack();
            return "the rack was empty: 'Fish on the rack' first (try again in a moment)";
        }

        string TryTend(IReadOnlyList<PersonActivity> acts, float time, bool force)
        {
            if (!FishRackAt(out var mid, out var along, out int fish) || fish + meatStrips == 0) return force ? RackReady(true) ?? "nothing on the rack" : "nothing on the rack";
            int keeper = Grown1(acts, mid, force);
            if (keeper == Nobody) return "no grown-up free";
            var pieces = RackPoints(mid, along, Mathf.Clamp(fish, 1, 6));
            bool hide = HideRackAt(out var hm, out var ha);
            Add(Thing(CampScenes5.TendRacks(keeper, At(keeper).Value, pieces, mid, ToCam(mid), hide, hm, ha, time), ThingFishRack));
            doneToday.Add(CampUse.TendRacks);
            return null;
        }

        string TryCountFish(IReadOnlyList<PersonActivity> acts, float time, bool force)
        {
            var r = RackReady(force); if (r != null) return r;
            FishRackAt(out var mid, out var along, out int fish);
            if (!force && fish < 3) return "fewer than three fish";
            var kids = Pick(acts, mid, a => IsKid(a), 2, force);
            if (kids.Count < 2) return $"only {kids.Count} kid(s) free";
            Add(Thing(CampScenes5.CountThings(CampUse.CountFish, kids[0], kids[1], At(kids[0]).Value, At(kids[1]).Value, RackPoints(mid, along, Mathf.Clamp(fish, 2, 6)), ToCam(mid), time), ThingFishRack));
            doneToday.Add(CampUse.CountFish);
            return null;
        }

        string TrySneakFish(IReadOnlyList<PersonActivity> acts, float time, bool force)
        {
            var r = RackReady(force); if (r != null) return r;
            FishRackAt(out var mid, out var along, out int fish);
            int kid = Kid1(acts, mid, force);
            if (kid == Nobody) return "no kid free";
            int keeper = Nearest(acts, mid, force ? 9f : 1.5f, a => IsGrown(a), new[] { kid }, false);
            if (keeper == Nobody) return "no keeper near the rack";
            int friend = Kid1(acts, mid, force, new[] { kid, keeper });
            var pts = RackPoints(mid, along, fish);
            var fishAt = pts[pts.Count - 1];
            var behind = mid + ToCam(mid) * 0.2f + Dir2(mid, fishAt) * 0.6f;
            if (NearestTent(mid, 2f, out var tent)) behind = tent.centre - ToCam(tent.centre) * 0.15f + Dir2(tent.centre, mid) * 0.05f;
            Add(Thing(CampScenes5.SneakFish(kid, At(kid).Value, keeper, At(keeper).Value, fishAt, ToCam(mid), friend, friend != Nobody ? At(friend).Value : default, behind, time), ThingFishRack));
            doneToday.Add(CampUse.SneakFish);
            return null;
        }

        string TryDuckUnder(IReadOnlyList<PersonActivity> acts, float time, bool force)
        {
            if (!HideRackAt(out var mid, out var along))
            {
                if (!force) return "no hides on the rack";
                EcoDebug.ShowRack(3);
                if (!HideRackAt(out mid, out along)) return "no hides on the rack (rack 3 hides: try again)";
            }
            var kids = Pick(acts, mid, a => IsKid(a), 2, force);
            if (kids.Count < 2) return $"only {kids.Count} kid(s) free";
            int scraper = Nobody; Vector2 scAt = default;
            foreach (var a in acts) if (a.kind == ActivityKind.ScrapeHide && !byKey.ContainsKey(a.who.Key) && At(a.who.Key) is Vector2 p) { scraper = a.who.Key; scAt = p; break; }
            Add(Thing(CampScenes5.DuckUnderHides(kids[0], kids[1], At(kids[0]).Value, At(kids[1]).Value, mid, along, scraper, scAt, time), ThingHideRack));
            doneToday.Add(CampUse.DuckUnderHides);
            return null;
        }

        string TryThicket(IReadOnlyList<PersonActivity> acts, float time, bool force)
        {
            var fire = meals.Fire;
            if (!ThicketEdge(fire, force ? 4f : 2f, !force, out var edge, out var bush, out var toMeadow))
            {
                if (!force) return "no thicket near the camp (with rabbits out)";
                // #692 p5 (the Editor pass): the cheat places the thicket and the rabbits by the camp first
                int tc = sim.DebugPlaceThicketNearCamp();
                if (sim.EcoRabbitsShown() == 0) sim.DebugPlaceNearCamp(LifeSpecies.Rabbits);
                return tc >= 0 ? "thicket and rabbits placed by the camp: press again in a moment" : "no ground for a thicket";
            }
            var kids = Pick(acts, edge, a => IsKid(a), 2, force);
            if (kids.Count == 0) return "no kid free";
            var at = new List<Vector2>(); foreach (int k in kids) at.Add(At(k).Value);
            Add(Thing(CampScenes5.ThicketPeek(kids, at, edge, bush, toMeadow, force ? 5f : BeatHash.Range(4f, 6f, (int)today, 1, 6962), time, ToCam(bush)), ThingThicket));
            doneToday.Add(CampUse.ThicketPeek);
            return null;
        }

        string TryClimb(IReadOnlyList<PersonActivity> acts, float time, bool force)
        {
            if (!WildTree(meals.Fire, force ? 4f : 2f, out int cell, out var trunk, out float crownR, (c, p) => !ThingBusy(ThingWildBase + c) && c != swingCell)) return "no wild tree near the camp ('Land: a wild tree and saplings' first)";
            int kid = Kid1(acts, trunk, force);
            if (kid == Nobody) return "no kid free";
            int friend = Kid1(acts, trunk, force, new[] { kid });
            float sc = pv.ScaleOf(kid, out float s0) ? s0 : 0.7f;
            float seatH = CampScenes5.ClimbSeatFor(crownR), seatOut = CampScenes5.ClimbOutFor(crownR);
            var boughWay = CampScenes5.Rot(ToCam(trunk), 55f); // the bough off to the side as the camera sees it, so its length shows beside the kid [est]
            var climb = CampScenes5.ClimbTree(kid, At(kid).Value, trunk, boughWay, force ? 7f : BeatHash.Range(6f, 10f, (int)today, 2, 6963), friend, friend != Nobody ? At(friend).Value : default, time, seatH, sc, seatOut);
            climbOf[climb] = (trunk, boughWay, seatH, seatOut);
            Add(Thing(climb, ThingWildBase + cell));
            doneToday.Add(CampUse.ClimbTree);
            return null;
        }

        string TryNap(IReadOnlyList<PersonActivity> acts, float time, bool force)
        {
            if (!WildTree(meals.Fire, force ? 4f : 2.5f, out int cell, out var trunk, out float napCrown, (c, p) => !ThingBusy(ThingWildBase + c))) return "no wild tree near the camp";
            int who;
            if (force) who = Grown1(acts, trunk, true);
            else { var n = FreeNear(acts, trunk, BorrowReach, a => IsGrown(a) && a.kind == ActivityKind.Nap, 1, true); who = n.Count > 0 ? n[0] : Nobody; }
            if (who == Nobody) return "nobody napping near a wild tree";
            // #692 p5 retakes: the back on this tree's bark (a wild trunk is 0.045 x its scale), asleep (CampScenes5.NapPose)
            var s = CampScenes2.TreeSit(who, At(who).Value, trunk, 4, ToCam(trunk), force ? 9f : BeatHash.Range(10f, 18f, (int)today, 3, 6964), time,
                doze: CampScenes5.NapPose, trunkR: 0.045f * napCrown / 0.16f);
            foreach (var pt in s.parts[0]) if (pt.style == DollStyle.Sit) { s.a = pt.from; break; } // the sit (AnimLab's lead)
            s.use = CampUse.TreeNap;
            Add(Thing(s, ThingWildBase + cell));
            doneToday.Add(CampUse.TreeNap);
            return null;
        }

        string TryLean(IReadOnlyList<PersonActivity> acts, float time, bool force)
        {
            if (!WildTree(meals.Fire, force ? 4f : 2.5f, out int cell, out var trunk, out float leanCrown, (c, p) => !ThingBusy(ThingWildBase + c))) return "no wild tree near the camp";
            int a;
            if (force) a = Grown1(acts, trunk, true);
            else { var n = FreeNear(acts, trunk, 1.2f, x => IsGrown(x) && x.kind == ActivityKind.Chat, 1); a = n.Count > 0 ? n[0] : Nobody; }
            if (a == Nobody) return "nobody chatting near a wild tree";
            int b = Grown1(acts, trunk, force, new[] { a });
            if (b == Nobody) return "nobody to chat with";
            Add(Thing(CampScenes5.TrunkLean(a, b, At(a).Value, At(b).Value, trunk, CampGeo5.TrunkFaceAt(leanCrown / 0.16f, 0.11f), ToCam(trunk), force ? 5f : BeatHash.Range(4f, 6f, (int)today, 4, 6965), time), ThingWildBase + cell));
            doneToday.Add(CampUse.TrunkLean);
            return null;
        }

        string NoPond => "no pond near the camp ('water breaks the surface now' first)";

        string TrySkip(IReadOnlyList<PersonActivity> acts, float time, bool force)
        {
            if (!BankNear(meals.Fire, force ? 6f : 2f, out var bank, out var toWater)) return NoPond;
            bool plop = force ? ForcePlop : BeatHash.Pick(3, (int)today, 5, 6966) == 0;
            int who = plop ? Kid1(acts, bank, force) : Grown1(acts, bank, force);
            if (who == Nobody) who = plop ? Grown1(acts, bank, force) : Kid1(acts, bank, force);
            if (who == Nobody) return "nobody free by the pond";
            var s = CampScenes5.SkipStones(who, At(who).Value, bank, toWater, CameraAcross(), plop ? 0 : 3, time);
            waterOf[s] = (bank, toWater);
            Add(Thing(s, ThingBank));
            doneToday.Add(CampUse.SkipStones);
            return null;
        }

        string TryBoat(IReadOnlyList<PersonActivity> acts, float time, bool force)
        {
            if (!BankNear(meals.Fire, force ? 6f : 2f, out var bank, out var toWater)) return NoPond;
            int kid = Kid1(acts, bank, force);
            if (kid == Nobody) return "no kid free";
            var s = CampScenes5.LeafBoat(kid, At(kid).Value, bank, toWater, time);
            waterOf[s] = (bank, toWater);
            Add(Thing(s, ThingBank));
            doneToday.Add(CampUse.LeafBoat);
            return null;
        }

        string TryFeet(IReadOnlyList<PersonActivity> acts, float time, bool force)
        {
            if (!BankNear(meals.Fire, force ? 6f : 2f, out var bank, out var toWater)) return NoPond;
            var g = Pick(acts, bank, a => IsGrown(a), 2, force);
            if (g.Count == 0) return "no grown-up free";
            Add(Thing(CampScenes5.FeetInWater(g[0], g.Count > 1 ? g[1] : Nobody, At(g[0]).Value, g.Count > 1 ? At(g[1]).Value : default, bank, toWater, force ? 9f : BeatHash.Range(12f, 20f, (int)today, 6, 6967), time), ThingBank));
            doneToday.Add(CampUse.FeetInWater);
            return null;
        }

        string NoPuddles(bool force, int want)
        {
            if (!force) return "no puddles";
            EcoDebug.PuddlesNow();
            return $"fewer than {want} puddles: 'Puddles now' (try again in a moment)";
        }

        string TryPuddleLook(IReadOnlyList<PersonActivity> acts, float time, bool force)
        {
            var pd = PuddlesNear(meals.Fire, force ? 4f : 2f);
            if (pd.Count == 0) return NoPuddles(force, 1);
            int k1 = Kid1(acts, pd[0].p, force);
            if (k1 == Nobody) return "no kid free";
            int k2 = Kid1(acts, pd[0].p, force, new[] { k1 });
            Add(Thing(CampScenes5.PuddleLook(k1, k2, At(k1).Value, k2 != Nobody ? At(k2).Value : default, pd[0].p, pd[0].r, ToCam(pd[0].p), time), ThingPuddle));
            doneToday.Add(CampUse.PuddleLook);
            return null;
        }

        string TryPuddleRace(IReadOnlyList<PersonActivity> acts, float time, bool force)
        {
            var pd = PuddlesNear(meals.Fire, force ? 4f : 2.5f);
            if (pd.Count < 3) return NoPuddles(force, 3);
            // three in a row, each the nearest to the last
            var order = new List<Vector2> { pd[0].p }; var left = new List<Vector2>(); for (int i = 1; i < pd.Count; i++) left.Add(pd[i].p);
            while (order.Count < 3 && left.Count > 0) { int bi = 0; for (int i = 1; i < left.Count; i++) if ((left[i] - order[^1]).sqrMagnitude < (left[bi] - order[^1]).sqrMagnitude) bi = i; order.Add(left[bi]); left.RemoveAt(bi); }
            var kids = Pick(acts, pd[0].p, a => IsKid(a), 4, force);
            if (kids.Count < 2) return $"only {kids.Count} kid(s) free";
            var at = new List<Vector2>(); foreach (int k in kids) at.Add(At(k).Value);
            Add(Thing(CampScenes5.PuddleRace(kids, at, order, time), ThingPuddle));
            doneToday.Add(CampUse.PuddleRace);
            return null;
        }

        string TryReeds(IReadOnlyList<PersonActivity> acts, float time, bool force)
        {
            if (!BankNear(meals.Fire, force ? 6f : 2.5f, out var reeds, out var toWater, true)) return force && sim.DebugReedsNear(meals.Fire.x, meals.Fire.y, 6) > 0 ? "reeds grew at the water by the camp: press again in a moment" : "no reeds near the camp";
            int who = Grown1(acts, reeds, force);
            if (who == Nobody) return "no grown-up free";
            var home = At(who).Value;
            Tent tent;
            var door = NearestTent(home, 9f, out tent) ? tent.look : meals.Fire + ToCam(meals.Fire) * 0.3f;
            var dout = tent.lookOut.sqrMagnitude > 0f ? tent.lookOut : ToCam(door);
            Add(Thing(CampScenes5b.CutReeds(who, home, reeds, toWater, door, dout, time), ThingReeds));
            doneToday.Add(CampUse.CutReeds);
            return null;
        }

        string TryReedWhistle(IReadOnlyList<PersonActivity> acts, float time, bool force)
        {
            if (!BankNear(meals.Fire, force ? 6f : 2.5f, out var reeds, out var toWater, true)) return force && sim.DebugReedsNear(meals.Fire.x, meals.Fire.y, 6) > 0 ? "reeds grew at the water by the camp: press again in a moment" : "no reeds near the camp";
            var kids = Pick(acts, reeds, a => IsKid(a), 3, force);
            if (kids.Count == 0) return "no kid free";
            var others = new List<int>(); var oat = new List<Vector2>();
            for (int i = 1; i < kids.Count; i++) { others.Add(kids[i]); oat.Add(At(kids[i]).Value); }
            Add(Thing(CampScenes5b.ReedWhistle(kids[0], At(kids[0]).Value, reeds - toWater * 0.08f, others, oat, time, CameraAcross()), ThingReeds));
            doneToday.Add(CampUse.ReedWhistle);
            return null;
        }

        List<Vector2> Bed()
        {
            var r = new List<Vector2>();
            if (LandSpot(PlaceKind.Stream, 1, out var a)) r.Add(a);
            if (LandSpot(PlaceKind.Stream, 0, out var b) && (r.Count == 0 || (b - r[0]).magnitude > 0.1f)) r.Add(b);
            if (LandSpot(PlaceKind.Stream, 2, out var c) && (c - r[^1]).magnitude > 0.1f) r.Add(c);
            return r;
        }

        string TryStream(IReadOnlyList<PersonActivity> acts, float time, bool force)
        {
            var bed = Bed();
            if (bed.Count < 2) return "no stream bed";
            if (!force && (bed[0] - meals.Fire).magnitude > 2.5f) return "the stream bed is far";
            var kids = Pick(acts, bed[0], a => IsKid(a), 3, force);
            if (kids.Count < 2) return $"only {kids.Count} kid(s) free";
            var at = new List<Vector2>(); foreach (int k in kids) at.Add(At(k).Value);
            Add(Thing(CampScenes5b.FollowStream(kids, at, bed, time), ThingStream));
            doneToday.Add(CampUse.FollowStream);
            return null;
        }

        string TryDam(IReadOnlyList<PersonActivity> acts, float time, bool force)
        {
            if (!LandSpot(PlaceKind.Stream, 0, out var bed)) return "no stream bed";
            int c = SiteMap.CellAt(bed.x, bed.y);
            if (!force && sim.EcoCoverAt(c) != EcoCover.Water && sim.EcoPuddleAt(c) < 0.05f) return "the bed holds no water [est]";
            var kids = Pick(acts, bed, a => IsKid(a), 2, force);
            if (kids.Count == 0) return "no kid free";
            var along = Bed().Count > 1 ? Dir2(Bed()[0], Bed()[^1]) : CameraAcross();
            var pile = bed + new Vector2(-along.y, along.x) * 0.18f;
            Add(Thing(CampScenes5b.BuildDam(kids[0], kids.Count > 1 ? kids[1] : Nobody, At(kids[0]).Value, kids.Count > 1 ? At(kids[1]).Value : default, bed, along, pile, time), ThingStream));
            doneToday.Add(CampUse.BuildDam);
            return null;
        }

        string TryHillDusk(IReadOnlyList<PersonActivity> acts, float time, bool force)
        {
            if (!LandSpot(PlaceKind.Hill, 0, out var top)) return "no hill";
            var g = Pick(acts, top, a => IsGrown(a), 3, force);
            if (g.Count == 0) return "no grown-up free";
            var at = new List<Vector2>(); foreach (int k in g) at.Add(At(k).Value);
            Add(Thing(CampScenes5b.HillDusk(g, at, top, meals.Fire, force ? 12f : BeatHash.Range(20f, 40f, (int)today, 7, 6968), time), ThingHill));
            doneToday.Add(CampUse.HillDusk);
            return null;
        }

        string TryHillRoll(IReadOnlyList<PersonActivity> acts, float time, bool force)
        {
            if (!LandSpot(PlaceKind.Hill, 0, out var top)) return "no hill";
            if (!force && (top - meals.Fire).magnitude > 2.5f) return "the hill is far";
            var kids = Pick(acts, top, a => IsKid(a), 3, force);
            if (kids.Count == 0) return "no kid free";
            var at = new List<Vector2>(); foreach (int k in kids) at.Add(At(k).Value);
            var down = Dir2(top, meals.Fire);
            Add(Thing(CampScenes5b.HillRoll(kids, at, top, down, 6 + BeatHash.Pick(3, (int)today, 8, 6969), time), ThingHill));
            doneToday.Add(CampUse.HillRoll);
            return null;
        }

        string TryPathRace(IReadOnlyList<PersonActivity> acts, float time, bool force)
        {
            // 2026-11-24: a camp has no worn path; the trodden way from the fire to the nearest tent is its path (as the clip's)
            if (!PathNear(meals.Fire, out var a, out var b) && !TrodLine(out a, out b)) return "no straight bit of path near the camp";
            var kids = Pick(acts, a, x => IsKid(x), 3, force);
            if (kids.Count < 2) return $"only {kids.Count} kid(s) free";
            var at = new List<Vector2>(); foreach (int k in kids) at.Add(At(k).Value);
            bool hop = BeatHash.Pick(3, (int)today, 9, 6970) == 0;
            Add(Thing(CampScenes5b.PathRace(kids, at, a, b, hop, time), ThingPath));
            doneToday.Add(CampUse.PathRace);
            return null;
        }

        string TryPathChat(IReadOnlyList<PersonActivity> acts, float time, bool force)
        {
            bool worn = PathNear(meals.Fire, out var a, out var b);
            if (!worn && !TrodLine(out a, out b)) return "no path near the camp";
            var mid = (a + b) * 0.5f;
            int x = force ? Grown1(acts, a, true) : Nobody;
            if (!force)
            {   // B50.b: two walkers on the path (never on an urgent carry [est]: carrying walkers are left alone)
                var on = RackSites.PathTest(sim);
                var la = a; var lb = b;
                bool OnWay(Vector2 p) => worn ? on != null && on(p.x, p.y) : WolfDriveOff.SegDist(la, lb, p) < 0.15f; // the trodden way: within 0.15 u of it [est]
                var walkers = new List<int>();
                foreach (var w in acts)
                    if (w.moving && IsGrown(w) && w.kind is ActivityKind.Visit or ActivityKind.SitOut or ActivityKind.Chat && !byKey.ContainsKey(w.who.Key) && At(w.who.Key) is Vector2 p && OnWay(p) && (p - mid).magnitude < 0.8f) walkers.Add(w.who.Key);
                if (walkers.Count < 2)
                {   // 2026-11-24 (a year's census: two walkers met on it never): two free grown-ups near the way walk onto it, meet and talk
                    var two = Pick(acts, mid, g => IsGrown(g), 2, false);
                    if (two.Count < 2) return "no two walkers on the path";
                    Add(Thing(CampScenes5b.PathChat(two[0], two[1], At(two[0]).Value, At(two[1]).Value, mid, Dir2(a, b), BeatHash.Range(4f, 6f, (int)today, 10, 6971), time), ThingPath));
                    doneToday.Add(CampUse.PathChat);
                    return null;
                }
                x = walkers[0];
                Add(Thing(CampScenes5b.PathChat(walkers[0], walkers[1], At(walkers[0]).Value, At(walkers[1]).Value, mid, Dir2(a, b), BeatHash.Range(4f, 6f, (int)today, 10, 6971), time), ThingPath));
                doneToday.Add(CampUse.PathChat);
                return null;
            }
            if (x == Nobody) return "no grown-up";
            int y = Grown1(acts, b, true, new[] { x });
            if (y == Nobody) return "nobody to meet";
            Add(Thing(CampScenes5b.PathChat(x, y, At(x).Value, At(y).Value, mid, Dir2(a, b), 5f, time), ThingPath));
            doneToday.Add(CampUse.PathChat);
            return null;
        }

        string TryGift(IReadOnlyList<PersonActivity> acts, float time, bool force)
        {
            // #722 (1.3 "Read later"): once the second stone stands the gift goes to either (by the day) [est]
            bool toSecond = sim.StoryMarks.secondStoneId >= 0 && BeatHash.Pick(2, (int)(today & 0x7fffffff), 1, 7222) == 1;
            if (!StoneAt(out var stone, out bool named, out float stoneR, toSecond) && !(toSecond && StoneAt(out stone, out named, out stoneR))) return "no remembering stone";
            // a family member of a name on the stone (a dead one's child, parent or partner), else (the cheat) any grown-up
            int who = Nobody, second = Nobody;
            foreach (var d in sim.State.notables)
            {
                if (d.alive) continue;
                foreach (var n in sim.State.notables)
                    if (n.alive && n.age >= 10f && (n.parentId == d.id || d.parentId == n.id || n.partnerId == d.id || d.partnerId == n.id) && Shown(acts, n.id) && (force || FreeKey(acts, n.id)))
                    { if (who == Nobody) who = n.id; else if (second == Nobody && n.id != who) second = n.id; }
                if (who != Nobody) break;
            }
            if (who == Nobody && !force) return "no family of a name on the stone free";
            if (who == Nobody) who = Grown1(acts, stone, true);
            if (who == Nobody) return "nobody";
            int gift = BeatHash.Pick(3, (int)today, who, 6972);
            Add(Thing(CampScenes5b.StoneGift(who, At(who).Value, second, second != Nobody ? At(second).Value : default, stone, ToCam(stone), gift, time, stoneR), ThingStone));
            doneToday.Add(CampUse.StoneGift);
            return null;
        }

        bool Lit(int id) => lit.TryGetValue(id, out var l) && (l.day == now.day || l.day == now.day - 1 && Hour < 6.5f); // lit today (at dusk; the cheat any time) until the next dawn

        string TryLanterns(IReadOnlyList<PersonActivity> acts, float time, bool force)
        {
            if (!CampGeo5.AllowedNow(CampUse.LightLanterns, InTents)) return "houses only: no lanterns in the tent phase (the user, 2026-11-16)";
            var tents = Tents();
            var poles = new List<Vector2>();
            // the round from the tent nearest the fire outward (each tent's nearest next)
            var left = new List<Tent>(); foreach (var t in tents) if (force || !Lit(t.id)) left.Add(t);
            if (left.Count == 0) return tents.Count == 0 ? "no tents" : "the lanterns are lit";
            var from = meals.Fire;
            while (left.Count > 0) { int bi = 0; for (int i = 1; i < left.Count; i++) if ((left[i].pole - from).sqrMagnitude < (left[bi].pole - from).sqrMagnitude) bi = i; poles.Add(left[bi].pole); from = left[bi].pole; left.RemoveAt(bi); }
            int who = Grown1(acts, poles[0], force);
            if (who == Nobody) return "no grown-up free";
            Add(CampScenes5b.LightLanterns(who, At(who).Value, poles, ToCam(poles[0]), time));
            doneToday.Add(CampUse.LightLanterns);
            return null;
        }

        /// <summary>#687: the share of the grass thirsty before the people look up for rain.</summary>
        public const float ThirstyBox = SimCore.ThirstyBox; // #722 (3.2): shared with the sim's dry spell

        string TrySkyLook(IReadOnlyList<PersonActivity> acts, float time, bool force)
        {
            if (meals == null || !meals.HasFire) return "no camp";
            int who = Grown1(acts, meals.Fire, force);
            if (who == Nobody) return "no grown-up free";
            var at = At(who).Value;
            Add(CampScenes5b.SkyLook(who, at, ToCam(at), time));
            doneToday.Add(CampUse.SkyLook);
            return null;
        }

        string TryMoths(IReadOnlyList<PersonActivity> acts, float time, bool force)
        {
            // #692 p5 notes: the moths come to the fire's light (no lanterns in the camp)
            if (meals == null || !meals.HasFire) return "no fire";
            var mc = CampGeo5.MothCentre(meals.Fire, ToCam(meals.Fire));
            int kid = Kid1(acts, mc, force);
            if (kid == Nobody) return "no kid free";
            Add(CampScenes5b.ChaseMoths(kid, At(kid).Value, mc, ToCam(mc), 3, time));
            doneToday.Add(CampUse.ChaseMoths);
            return null;
        }

        string TryWeed(IReadOnlyList<PersonActivity> acts, float time, bool force)
        {
            int c = WeedCell(out var at);
            if (c < 0 && force)
            {   // the cheat: a seedling creeps onto a tent's door (else by the fire ring)
                foreach (var t in Tents()) if (sim.DebugSeedlingAt(SiteMap.CellAt(t.look.x + t.lookOut.x * 0.06f, t.look.y + t.lookOut.y * 0.06f))) break; // #692 p5 retakes: on the side the camera sees
                if (WeedCell(out at) < 0) { var p = meals.Fire + ToCam(meals.Fire) * 0.3f; sim.DebugSeedlingAt(SiteMap.CellAt(p.x, p.y)); }
                RefreshBox();
                c = WeedCell(out at);
            }
            if (c < 0) return "no seedling at a door, the fire or a path";
            int who = Grown1(acts, at, force);
            if (who == Nobody) return "no grown-up free";
            var edge = at + Dir2(meals.Fire, at) * 0.3f;
            Add(CampScenes5b.PullWeed(who, At(who).Value, at, edge, time));
            doneToday.Add(CampUse.PullWeed);
            return null;
        }

        static void RefreshBox() { var src = SimLayoutSource.Find(); if (src != null) src.GetComponent<BoxView>()?.Refresh(); }

        string TryCrown(IReadOnlyList<PersonActivity> acts, float time, bool force)
        {
            var fire = meals.Fire;
            Vector2 spot = fire + ToCam(fire) * 0.45f + CameraAcross() * 0.2f;
            if (GrassSpot(fire, 0.4f, force ? 2.5f : 1.6f, true, out var g) || GrassSpot(fire, 0.4f, force ? 2.5f : 1.6f, false, out g)) spot = g;
            var kids = Pick(acts, spot, a => IsKid(a), 2, force);
            if (kids.Count < 2) return $"only {kids.Count} kid(s) free";
            Add(CampScenes5b.Crown(kids[0], At(kids[0]).Value, kids[1], At(kids[1]).Value, spot, ToCam(spot), time));
            doneToday.Add(CampUse.Crown);
            return null;
        }

        string TryHangSwing(IReadOnlyList<PersonActivity> acts, float time, bool force)
        {
            if (swingHung && !force) return "the swing is hung";
            if (!SwingSpot(force ? 3f : CampGeo5.SwingReach, out int cell, out var pivot, out var fwd, out var trunk)) return "no grown wild tree near the fire with room for a swing ('Land: a wild tree and saplings' first)";
            int who;
            if (force) who = Grown1(acts, pivot, true);
            else { var n = FreeNear(acts, pivot, BorrowReach, a => IsGrown(a) && a.kind == ActivityKind.SitOut, 1); who = n.Count > 0 ? n[0] : Nobody; }
            if (who == Nobody) return "no grown-up with a free afternoon";
            swingCell = cell; swingPivot = pivot; swingFwd = fwd; swingTrunk = trunk; swingTrunkSet = true; swingRope = swingHung = false; swingRides.Clear();
            bool miss = force ? false : BeatHash.Pick(2, (int)today, 11, 6973) == 0;
            Add(Thing(CampScenes5b.HangSwing(who, At(who).Value, pivot, trunk, fwd, miss, time), ThingSwing));
            doneToday.Add(CampUse.HangSwing);
            return null;
        }

        string TrySwing(IReadOnlyList<PersonActivity> acts, float time, bool force)
        {
            if (!swingHung)
            {
                if (!force) return "no swing";
                if (!SwingSpot(3f, out swingCell, out swingPivot, out swingFwd, out swingTrunk)) return "no grown wild tree near the fire with room for a swing ('Land: a wild tree and saplings' first)";
                swingRope = swingHung = swingTrunkSet = true; // "Kids on the swing" hangs it first if none
            }
            var pivot = swingPivot;
            var kids = force ? Pick(acts, pivot, a => IsKid(a), 2, true) : FreeNear(acts, pivot, BorrowReach, a => IsKid(a) && a.kind == ActivityKind.Play, 2);
            if (kids.Count == 0) return "no kid at play ('Two kids join the camp' first)";
            // kids stay safe: a grown-up within 1 u; the cheat brings one over to watch
            int grown = Nobody;
            foreach (var a in acts) if (IsGrown(a) && At(a.who.Key) is Vector2 p && (p - pivot).magnitude <= CampGeo5.GrownNear && !kids.Contains(a.who.Key)) { grown = a.who.Key; break; }
            int watcher = Nobody;
            if (grown == Nobody)
            {
                if (!force) return "no grown-up within 1 u of the swing";
                watcher = Grown1(acts, pivot, true, kids);
            }
            float sc = pv.ScaleOf(kids[0], out float s0) ? s0 : 0.7f;
            var rides = new List<CampScenes5b.Ride>();
            var sw = CampScenes5b.Swing(kids[0], At(kids[0]).Value, kids.Count > 1 ? kids[1] : Nobody, kids.Count > 1 ? At(kids[1]).Value : default, pivot, swingFwd, force ? 8f : BeatHash.Range(6f, 10f, (int)today, 12, 6974), sc, rides, time);
            if (watcher != Nobody)
            {
                int m = sw.Add(watcher);
                var acr = CameraAcross(); float sd = Vector2.Dot(swingFwd, acr) >= 0f ? -1f : 1f;
                var spot = pivot + acr * (sd * 0.45f) - ToCam(pivot) * 0.05f; // reshoot2: across the camera's view from the swing, a little behind it (in front it hid the swing)
                float w = Mathf.Clamp(((At(watcher) ?? spot) - spot).magnitude / CampScenes1.Walk, 0.4f, 9f);
                sw.Part(m, 0f, w, spot, pivot, DollStyle.Stand, null, PropKind.None, CampScenes1.Walk);
                sw.Part(m, w, Mathf.Max(w + 0.5f, sw.Length), spot, pivot, DollStyle.Stand);
            }
            foreach (var r in rides) swingRides.Add((time + r.from, r.len));
            while (swingRides.Count > 6) swingRides.RemoveAt(0);
            Add(Thing(sw, ThingSwing));
            doneToday.Add(CampUse.Swing);
            return null;
        }

        /// <summary>B42: meat strips hang on the rack (the cheat; a hunt's day hangs them by itself).</summary>
        public string HangMeat()
        {
            if (planner == null) return "no camp yet";
            meatStrips = CampGeo5.MaxStrips; meatDay = now.day;
            return FishRackAt(out _, out _, out _) ? "meat strips on the rack" : "meat strips hang once the rack stands ('Fish on the rack' shows it)";
        }

        // ================================================================== cheats
        string ForceCamp(CampUse use)
        {
            if (sim == null || planner == null || meals == null) return "no camp yet";
            if (!meals.HasFire) return "no fire in the camp";
            var acts = pv.Activities; float time = clock;
            switch (use)
            {
                case CampUse.TentPeek: return TryPeek(acts, time, true) ?? "a kid peeks out of a tent";
                case CampUse.TentToy: return TryToy(acts, time, true) ?? "a toy on a tent";
                case CampUse.TentDoorSit: return InTents ? TryDoorSit(acts, time, true) ?? "sitting in a tent's door" : TryHouseDoorSit(acts, time, true) ?? "sitting on a doorstep";
                case CampUse.CountStash: return TryCountStash(acts, time, true) ?? "kids count the stash";
                case CampUse.SnackSneak: return TrySneak(acts, time, true) ?? "a snack sneaked at the stash";
                case CampUse.TendRacks: return TryTend(acts, time, true) ?? "the racks tended";
                case CampUse.DuckUnderHides: return TryDuckUnder(acts, time, true) ?? "kids run under the hide rack";
                case CampUse.CountFish: return TryCountFish(acts, time, true) ?? "kids count the fish";
                case CampUse.SneakFish: return TrySneakFish(acts, time, true) ?? "a kid sneaks a fish";
                case CampUse.ThicketPeek: return TryThicket(acts, time, true) ?? "kids peek at rabbits from the thicket";
                case CampUse.BerryWipe: return TryBerryWipe(acts, time, true) ?? "a berry kid (a purple mouth, wiped)";
                case CampUse.ClimbTree: return TryClimb(acts, time, true) ?? "a kid climbs a tree";
                case CampUse.TreeNap: return TryNap(acts, time, true) ?? "a nap under a tree";
                case CampUse.TrunkLean: return TryLean(acts, time, true) ?? "a chat leaning on a tree";
                case CampUse.SkipStones: { var r = TrySkip(acts, time, true) ?? (ForcePlop ? "a kid's stone goes plop" : "skip stones: three skips"); ForcePlop = !ForcePlop; return r; }
                case CampUse.LeafBoat: return TryBoat(acts, time, true) ?? "a leaf boat";
                case CampUse.FeetInWater: return TryFeet(acts, time, true) ?? "feet in the water";
                case CampUse.PuddleLook: return TryPuddleLook(acts, time, true) ?? "kids look in a puddle";
                case CampUse.PuddleRace: return TryPuddleRace(acts, time, true) ?? "kids race the puddles";
                case CampUse.CutReeds: return TryReeds(acts, time, true) ?? "cut reeds";
                case CampUse.ReedWhistle: return TryReedWhistle(acts, time, true) ?? "a reed whistle";
                case CampUse.FollowStream: return TryStream(acts, time, true) ?? "kids follow the stream";
                case CampUse.BuildDam: return TryDam(acts, time, true) ?? "kids build a dam";
                case CampUse.HillDusk: return TryHillDusk(acts, time, true) ?? "sit on the hill";
                case CampUse.HillRoll: return TryHillRoll(acts, time, true) ?? "kids roll down the hill";
                case CampUse.PathRace: return TryPathRace(acts, time, true) ?? "kids race on the path";
                case CampUse.PathChat: return TryPathChat(acts, time, true) ?? "a chat on a path";
                case CampUse.StoneGift: return TryGift(acts, time, true) ?? "a gift at the remembering stone";
                case CampUse.SkyLook: return TrySkyLook(acts, time, true) ?? "looking up at the glass for rain";
                case CampUse.LightLanterns: return InTents ? TryLanterns(acts, time, true) ?? "the lanterns lit" : TryHouseLanterns(acts, time, true) ?? "the house lanterns lit";
                case CampUse.ChaseMoths: return TryMoths(acts, time, true) ?? "kids chase moths";
                case CampUse.PullWeed: return TryWeed(acts, time, true) ?? "a weed pulled";
                case CampUse.Crown: return TryCrown(acts, time, true) ?? "a crown made";
                case CampUse.HangSwing: return TryHangSwing(acts, time, true) ?? "the swing hung";
                case CampUse.Swing: return TrySwing(acts, time, true) ?? "kids on the swing";
            }
            return "not a part 5 use: " + use;
        }

        // ================================================================== what the scenes leave (CampFxKind.Leave and the water fx)
        void FireCamp(Transform root, CampScene s, in CampFx f, Vector3 world)
        {
            var o = PuffOptions.Default; if (f.count > 0) o.count = f.count;
            switch (f.kind)
            {
                // reshoot2: the puffs' ground is the pond's surface. With none given a ring sat 0.06 x its scale under the given point, under the
                // water, so no skip, plop, boat or feet ring was ever seen
                case CampFxKind.WaterRing: { var w = root.TransformPoint(new Vector3(f.at.x, PondY + 0.003f + f.at.y, f.at.z)); o.groundY = w.y; HitFx.Puff(PuffKind.Ring, w, Vector3.up, f.scale, o); break; }
                case CampFxKind.WaterDrops: { var w = root.TransformPoint(new Vector3(f.at.x, PondY + 0.01f + f.at.y, f.at.z)); o.groundY = root.TransformPoint(new Vector3(f.at.x, PondY, f.at.z)).y; HitFx.Puff(PuffKind.Droplets, w, Vector3.up, f.scale, o); break; }
                case CampFxKind.Creak: AudioDirector.Instance?.Play(AquariumCiv.Audio.CueLibrary.KnockBack, -5f, 0.25f * Mathf.Max(0.3f, f.scale), 0f); break; // [est] impactWood_light pitched down
                case CampFxKind.BushSway: HitFx.Shake(ShakeTarget.Bush, world, CameraAcross3(root), 0f); break;
                case CampFxKind.Leave: Leave(s, f, root); break;
            }
        }

        void Leave(CampScene s, in CampFx f, Transform root)
        {
            var kind = (CampMark)f.count;
            var p = new Vector2(f.at.x, f.at.z);
            if (f.anchor == CampAnchor.Head)
            {
                if (f.member < 0 || f.member >= s.Count) return;
                int key = s.keys[f.member];
                if (kind == CampMark.WipeMouth)
                {
                    for (int i = 0; i < worn5.Count; i++) if (worn5[i].key == key && worn5[i].kind == CampMark.PurpleMouth) { var w = worn5[i]; w.fadeAt = clock; worn5[i] = w; }
                    return;
                }
                worn5.RemoveAll(w => w.key == key && w.kind == kind);
                float until = kind == CampMark.Crown ? now.day * 24f + 21f : ViewHours + 24f; // the crown until night; the mouth until the next meal
                worn5.Add((key, kind, until, -1f));
                return;
            }
            switch (kind)
            {
                case CampMark.Toy:
                case CampMark.Beads:
                    if (NearestTent(p, 0.3f, out var t1))
                    {
                        if (!tentThings.TryGetValue(t1.id, out var l)) tentThings[t1.id] = l = new List<TentThing>();
                        if (l.Count < CampGeo5.TentThings) l.Add(new TentThing { kind = kind });
                    }
                    break;
                case CampMark.ToySpin: if (NearestTent(p, 0.3f, out var t2) && tentThings.TryGetValue(t2.id, out var l2) && l2.Count > 0) l2[^1].spinAt = clock; break;
                case CampMark.LanternLit: if (NearestTent(p, 0.3f, out var t3)) lit[t3.id] = (now.day, clock); else HouseLanternLit(p); break;
                case CampMark.Gift: gifts.Add((p, Mathf.RoundToInt(f.scale), ViewHours)); if (gifts.Count > 4) gifts.RemoveAt(0); break;
                case CampMark.DamPool: dams.Add(new Dam { p = p, r = f.scale > 0f ? f.scale : CampGeo5.DamPoolR, at = ViewHours }); if (dams.Count > 2) dams.RemoveAt(0); break;
                case CampMark.DamBreak: foreach (var d in dams) if ((d.p - p).magnitude < 0.2f && d.brokeAt < 0f) d.brokeAt = clock; break;
                case CampMark.Pebble: damStones.Add((p, ViewHours)); if (damStones.Count > 10) damStones.RemoveAt(0); break;
                case CampMark.Boat:
                    if (waterOf.TryGetValue(s, out var wo))
                    {
                        var dir = Rotate(wo.toWater, BeatHash.Range(-25f, 25f, (int)today, s.keys[0], 6975));
                        boats.Add(new Boat { start = p, dir = dir, push = clock, len = BeatHash.Range(0.3f, 0.5f, (int)today, s.keys[0], 6976), day = now.day });
                        if (boats.Count > 3) boats.RemoveAt(0);
                    }
                    break;
                case CampMark.SkipSplash: PondLife.Instance?.Startle(root.TransformPoint(new Vector3(p.x, PondY, p.y)), 0.5f); break; // P3: frogs and ducks near flee
                case CampMark.SwingRope: swingRope = true; break;
                case CampMark.SwingHung: swingRope = swingHung = true; break;
                case CampMark.WeedPulled: if (sim != null && sim.EcoPeopleWeed(SiteMap.CellAt(p.x, p.y))) RefreshBox(); break;
                case CampMark.FishTaken: sim?.EcoKidTakesRackFish(); break;
                case CampMark.ReedsFlat: flats.Add((p, clock + 3f)); break;
                case CampMark.MothClap: mothClapAt = clock; break;
                case CampMark.StripTouch: stripTouches.Add((p, clock)); break;
            }
        }

        // ================================================================== drawing (after the dolls)
        /// <summary>#692 p5 retakes: the worn crown's scale (the ring's 0.022 radius to 1.2 x the head's 0.043) and how far up the head it sits
        /// (x the head's radius) [est].</summary>
        public const float CrownOnHead = 2.35f, CrownRise = 0.45f;
        /// <summary>S-ritual (B72): a purple hand dot drawn at this scale of the purple mouth's (0.015 u → 0.012 u... ×1.6 to read at phone scale) [est].</summary>
        public const float PurpleHandDot = 1.6f;
        /// <summary>#692 p5 retakes: the weed in the hand drawn at this scale (its 0.024 u read as nothing at phone scale) [est].</summary>
        public const float WeedInHand = 2f;
        /// <summary>#692 p5 retakes: a toy or beads on a tent's pole drawn at this scale [est].</summary>
        public const float TentThingScale = 1.4f, BoatScale = 1.4f;

        const int MPole = 1, MLantern = 2, MLanternLit = 3, MToy = 4, MBeads = 5, MStrip = 6, MBoat = 7, MCrown = 8, MMouth = 9, MGiftBowl = 10,
            MPool = 11, MDamStone = 12, MRope = 13, MPlank = 14, MMoth = 15, MFlatStone = 16, MReeds = 17, MReed = 18, MWeed = 19, MCoil = 20, MLeaf = 21, MBough = 22, MBranch = 23, MBracket = 24;

        /// <summary>#692 p5 retakes: the swing's rope and plank colours, dark enough to read on sand and grass [est].</summary>
        static readonly Color ReedGreen = new Color(0.5f, 0.64f, 0.28f), ReedStraw = new Color(0.8f, 0.72f, 0.42f), ReedHead = new Color(0.42f, 0.27f, 0.15f);
        static readonly Color SwingRope = new Color(0.36f, 0.25f, 0.15f), SwingPlank = new Color(0.52f, 0.33f, 0.18f);

        /// <summary>The swing tree's crown radius (its spot), 0.16 when unknown.</summary>
        float SwingCrownR()
        {
            var box = LandBox;
            if (box == null || swingCell < 0) return 0.16f;
            lock (box.TreeSpots) return box.TreeSpots.TryGetValue(swingCell, out var v) ? v.r : 0.16f;
        }

        /// <summary>The house lantern's arm: as far out as the lantern hangs (<see cref="HouseLanternOut"/>).</summary>
        const float VillageLanternArm = HouseLanternOut;

        /// <summary>A light struck at a house lantern (the round's hit): the nearest home's lantern within 0.1 u is lit.</summary>
        void HouseLanternLit(Vector2 p)
        {
            if (!VillageEra) return;
            foreach (var home in PiecesOf(PieceKind.Home, p)) { HouseDoor(home, out _, out _, out var lan); if ((lan - p).magnitude < 0.1f) houseLit[home.seed] = now.day; return; }
        }

        void DrawCamp(Transform root, Material material, float time, System.Func<float, float, float> groundAt)
        {
            int used = 0;
            float vh = ViewHours;
            Transform Next(int meshKey)
            {
                while (used < campPool.Count && campPool[used] == null) campPool.RemoveAt(used);
                while (campPool.Count <= used)
                {
                    var go = new GameObject("Camp part 5 mark") { hideFlags = HideFlags.DontSave };
                    go.transform.SetParent(root, false);
                    go.AddComponent<MeshFilter>();
                    var r = go.AddComponent<MeshRenderer>(); r.sharedMaterial = material; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    campPool.Add(go.transform);
                }
                var t = campPool[used++];
                t.GetComponent<MeshFilter>().sharedMesh = CampMesh(meshKey);
                t.gameObject.SetActive(true);
                t.localRotation = Quaternion.identity; t.localScale = Vector3.one;
                return t;
            }
            Vector3 On(Vector2 p, float h = 0f) => new Vector3(p.x, groundAt(p.x, p.y) + h, p.y);

            // ---- B40.b: the toys and beads hung on the tents' canvas (#692 p5 notes: no lantern poles in the camp)
            if (sim != null && sim.State.era == Era.Camp)
                foreach (var t in Tents())
                {
                    if (tentThings.TryGetValue(t.id, out var things))
                        for (int i = 0; i < things.Count; i++)
                        {
                            var th = things[i];
                            var m = Next(th.kind == CampMark.Beads ? MBeads : MToy);
                            float sp = time - th.spinAt;
                            float yaw = sp >= 0f && sp < 1.5f ? 720f * (1f - (1f - sp / 1.5f) * (1f - sp / 1.5f)) : 0f;
                            float sway = 4f * Mathf.Sin(time * 1.7f + i * 2f + t.id);
                            // on the canvas of the shown side, the second a little to the side and lower [est]
                            var side = new Vector2(-CampGeo5.HomeToCam.y, CampGeo5.HomeToCam.x) * (i == 0 ? 0f : 0.03f); // on the canvas straight toward the home camera
                            m.localPosition = new Vector3(t.pole.x + side.x, groundAt(t.centre.x, t.centre.y) + CampGeo5.ToyH - 0.015f * i, t.pole.y + side.y);
                            m.localScale = Vector3.one * TentThingScale;
                            m.localRotation = Quaternion.Euler(sway, yaw + t.id * 37f, sway * 0.5f);
                        }
                }

            // ---- B52.a at the houses (2026-11-24): every home's door lantern on its iron arm, lit from the round at dusk to dawn
            if (VillageEra)
                foreach (var home in PiecesOf(PieceKind.Home, VillageMiddle()))
                {
                    HouseDoor(home, out _, out var outw, out var lan);
                    var wallAt = lan - outw * HouseLanternOut;
                    float gy = groundAt(wallAt.x, wallAt.y);
                    var arm = Next(MBracket);
                    arm.localPosition = new Vector3(wallAt.x, gy + CampGeo5.PoleH, wallAt.y);
                    arm.localRotation = Quaternion.LookRotation(new Vector3(outw.x, 0f, outw.y));
                    var l = Next(HouseLit(home.seed) ? MLanternLit : MLantern);
                    l.localPosition = new Vector3(lan.x, gy + CampGeo5.PoleH - 0.004f, lan.y);
                }

            // ---- B42: meat strips on the fish rack (on the far face of the bar from the fish [est])
            if (meatStrips > 0 && FishRackAt(out var rmid, out var ralong, out _))
            {
                ralong = ralong.sqrMagnitude > 1e-6f ? ralong.normalized : Vector2.right;
                var across = new Vector2(-ralong.y, ralong.x);
                float yawR = Mathf.Atan2(ralong.x, ralong.y) * Mathf.Rad2Deg;
                for (int i = 0; i < meatStrips; i++)
                {
                    var hp = FishRackPieces.HangPoint(i * 2);
                    var at = rmid + ralong * hp.z + across * 0.015f;
                    float sw = 0f;
                    foreach (var st in stripTouches) if ((st.p - at).magnitude < 0.05f) { float u = time - st.at; sw = 5f * Mathf.Sin(u * 9f) * Mathf.Exp(-u / 0.5f); }
                    var m = Next(MStrip);
                    m.localPosition = On(rmid, hp.y) + new Vector3(at.x - rmid.x, 0f, at.y - rmid.y);
                    m.localRotation = Quaternion.Euler(0f, yawR, sw + 3f * Mathf.Sin(time * 1.3f + i));
                }
            }

            // ---- B51: gifts at the stone (a view day)
            for (int i = gifts.Count - 1; i >= 0; i--) if (vh - gifts[i].at > 24f || vh < gifts[i].at) gifts.RemoveAt(i);
            foreach (var g in gifts)
            {
                var m = Next(g.kind == 0 ? MLeaf + 100 : g.kind == 1 ? MGiftBowl : MDamStone);
                m.localPosition = On(g.p, 0.003f);
                if (g.kind == 0) { m.localRotation = Quaternion.Euler(80f, g.p.x * 77f, 0f); m.localScale = Vector3.one * 0.012f; }
            }

            // ---- B48.b: the dam's stones and its little pool (a view day; the pool drains in 2 s after the gush)
            for (int i = damStones.Count - 1; i >= 0; i--) if (vh - damStones[i].at > 24f || vh < damStones[i].at) damStones.RemoveAt(i);
            foreach (var ds in damStones) { var m = Next(MDamStone); m.localPosition = On(ds.p, 0.004f); m.localRotation = Quaternion.Euler(0f, ds.p.x * 131f, 0f); }
            for (int i = dams.Count - 1; i >= 0; i--) if (vh - dams[i].at > 24f || vh < dams[i].at || dams[i].brokeAt >= 0f && time - dams[i].brokeAt > 2f) dams.RemoveAt(i);
            foreach (var d in dams)
            {
                float k = d.brokeAt >= 0f ? 1f - Mathf.Clamp01((time - d.brokeAt) / 2f) : 1f;
                var m = Next(MPool); m.localPosition = On(d.p, 0.004f); m.localScale = new Vector3(d.r * k, 1f, d.r * k * 0.8f);
            }

            // ---- B45.b: leaf boats (until night)
            for (int i = boats.Count - 1; i >= 0; i--) if (boats[i].day != now.day || Hour >= 21f) boats.RemoveAt(i);
            foreach (var b in boats)
            {
                var p = CampGeo5.BoatAt(b.start, b.dir, time - b.push, b.len, out float yaw);
                var m = Next(MBoat); m.localPosition = new Vector3(p.x, PondY + 0.004f + 0.002f * Mathf.Sin(time * 2f), p.y); m.localRotation = Quaternion.Euler(0f, yaw, 0f); m.localScale = Vector3.one * BoatScale; // #692 p5 retakes: 1.4 x, a fleck that reads on the water [est]
            }

            // ---- B44.a (#692 p5 retakes): the low bough the climber sits on, while the climb plays [est]
            if (climbOf.Count > 0)
            {
                foreach (var k in new List<CampScene>(climbOf.Keys)) if (!scenes.Contains(k)) climbOf.Remove(k);
                foreach (var kv in climbOf)
                {
                    var (trunk, tc, seatH, seatOut) = kv.Value;
                    // #692 p5 notes: the bough runs from the trunk out under the climber's seat (and a little past it)
                    var m = Next(MBough); m.localPosition = On(trunk, seatH - 0.012f); m.localRotation = Quaternion.LookRotation(new Vector3(tc.x, 0f, tc.y), Vector3.up);
                    m.localScale = new Vector3(1f, 1f, (seatOut + 0.04f) / 0.14f);
                }
            }
            foreach (var k in new List<CampScene>(waterOf.Keys)) if (!scenes.Contains(k)) waterOf.Remove(k);

            // ---- B45.a: the skipping stone in flight; B52.b: the moths; the swing
            foreach (var s in scenes)
            {
                if (s.use == CampUse.SkipStones && waterOf.TryGetValue(s, out var wo))
                {
                    float u = time - (s.t0 + s.a);
                    int skips = s.kind;
                    float end = skips <= 0 ? 0.35f : 0.2f + skips * CampGeo5.SkipGap;
                    if (u < 0f || u > end) continue;
                    Vector2 a2, b2; float h0, k, arc;
                    var hand = wo.bank + wo.toWater * 0.04f;
                    if (skips <= 0) { a2 = hand; b2 = wo.bank + wo.toWater * CampGeo5.PlopFar; k = u / 0.35f; h0 = 0.06f; arc = 0.1f; }
                    else if (u < 0.2f) { a2 = hand; b2 = CampGeo5.SkipPoint(wo.bank, wo.toWater, 0); k = u / 0.2f; h0 = 0.05f; arc = 0.01f; }
                    else { int i = Mathf.Min(skips - 1, Mathf.FloorToInt((u - 0.2f) / CampGeo5.SkipGap)); a2 = CampGeo5.SkipPoint(wo.bank, wo.toWater, i); b2 = CampGeo5.SkipPoint(wo.bank, wo.toWater, i + 1); k = (u - 0.2f - i * CampGeo5.SkipGap) / CampGeo5.SkipGap; h0 = 0f; arc = 0.03f * (1f - i * 0.3f); }
                    var pp = Vector2.Lerp(a2, b2, k);
                    float y = PondY + CampGeo5.SkipH * 0.5f + Mathf.Lerp(h0, 0f, k) + arc * Mathf.Sin(k * Mathf.PI);
                    var m = Next(MFlatStone); m.localPosition = new Vector3(pp.x, y, pp.y); m.localScale = Vector3.one * 0.012f; m.localRotation = Quaternion.Euler(0f, time * 900f, 0f);
                }
                else if (s.use == CampUse.ChaseMoths)
                {
                    float u = time - s.t0;
                    if (u < 0f || u > s.Length) continue;
                    var c = On(s.focus, CampGeo5.MothH);
                    float scatter = Mathf.Clamp01(1f - (time - mothClapAt) / 0.8f);
                    for (int i = 0; i < 3; i++)
                    {
                        float a = time * CampGeo5.MothHz * Mathf.PI * 2f * (i % 2 == 0 ? 1f : -0.8f) + i * 2.1f;
                        float r = CampGeo5.MothR * (1f + 2.5f * scatter) * (0.8f + 0.2f * i);
                        var m = Next(MMoth); m.localPosition = c + new Vector3(Mathf.Cos(a) * r, 0.01f * Mathf.Sin(a * 1.7f) + 0.03f * scatter, Mathf.Sin(a) * r);
                        m.localScale = Vector3.one * CampGeo5.MothSize;
                    }
                }
            }
            if (swingRope && swingCell >= 0)
            {
                float th = 0f;
                th = CampGeo5.SwingThetaAt(swingRides, time); // #692 p5 notes: the ride under way (only the last listed held the swing still through the first)
                var fwd3 = new Vector3(swingFwd.x, 0f, swingFwd.y);
                var across3 = new Vector3(-swingFwd.y, 0f, swingFwd.x);
                var foot = On(swingPivot);
                // #692 p5 retakes (the user's review: no ropes, no plank): the ropes go up into the crown's underside, darker and wider than the
                // straw on sand they were; the plank lies across the swing (it lay along it, the ropes outside it)
                var top = foot + Vector3.up * CampGeo5.RopeTop(SwingCrownR());
                var off = CampGeo5.SeatOffset(th);
                var seat = foot + Vector3.up * (CampGeo5.SeatH + off.y) + fwd3 * off.x;
                if (!swingHung) seat = top + Vector3.down * CampGeo5.RopeLen * 0.8f + across3 * 0.01f; // the rope over the bough, not tied yet
                float rs = Mathf.Max(1e-4f, root.lossyScale.y);
                float w = CampGeo5.RopeW;
                var cam = Camera.main;
                if (cam != null)
                {
                    var wp = root.TransformPoint(top);
                    float dist = Vector3.Distance(cam.transform.position, wp);
                    float perPx = 2f * dist * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) / Mathf.Max(1, cam.pixelHeight);
                    w = Mathf.Max(w, CampGeo5.RopeMinPx * perPx / rs); // [est] at least 2 px, else the swing reads as a floating plank
                    w = Mathf.Min(w, 0.01f); // #692 p5 (the Editor pass): never a panel (a far camera made the ropes a wall in a close shot)
                }
                for (int side = -1; side <= 1; side += 2)
                {
                    var a = top + across3 * (CampGeo5.PlankW * 0.4f * side);
                    var b = seat + across3 * (CampGeo5.PlankW * 0.4f * side);
                    var d = b - a;
                    var m = Next(MRope); m.localPosition = a; m.localRotation = Quaternion.LookRotation(d.sqrMagnitude > 1e-8f ? d : Vector3.down, fwd3); m.localScale = new Vector3(w, w, d.magnitude);
                }
                if (swingHung) { var pl = Next(MPlank); pl.localPosition = seat; pl.localRotation = Quaternion.LookRotation(across3, Vector3.up) * Quaternion.Euler(0f, 0f, th); }
                // #692 p5 notes (the user: "the rope hangs to nothing"): a branch from the trunk out over the swing, the ropes tied under it, and a
                // short fork across the rope tops
                if (swingTrunkSet)
                {
                    var tf = On(swingTrunk); var tp = new Vector3(top.x, 0f, top.z) - new Vector3(tf.x, 0f, tf.z);
                    float len = tp.magnitude + 0.05f;
                    var br = Next(MBranch); br.localPosition = new Vector3(tf.x, top.y + 0.01f, tf.z);
                    br.localRotation = Quaternion.LookRotation(tp.sqrMagnitude > 1e-6f ? tp : fwd3, Vector3.up); br.localScale = new Vector3(1f, 1f, len);
                    var fk = Next(MBranch); fk.localPosition = top + Vector3.up * 0.01f - across3 * 0.05f;
                    fk.localRotation = Quaternion.LookRotation(across3, Vector3.up); fk.localScale = new Vector3(0.8f, 0.8f, 0.1f);
                }
            }

            // ---- what people wear: a crown, a purple mouth (B43.c)
            for (int i = worn5.Count - 1; i >= 0; i--)
            {
                var w = worn5[i];
                if (vh > w.until || w.fadeAt >= 0f && time - w.fadeAt > 0.6f) worn5.RemoveAt(i);
            }
            foreach (var w in worn5)
            {
                var v = pv.ViewOf(w.key);
                if (v == null || !v.gameObject.activeInHierarchy) continue;
                var head = v.transform.TransformPoint(new Vector3(0f, PegDoll.HeadY * v.scale, 0f));
                if (w.kind == CampMark.Crown)
                {
                    // #692 p5 retakes (the user's review: no crown on the head): the ring (0.044 u across) was half the head's width and sat
                    // inside it; round the head now (CrownOnHead x the head), on its upper half
                    var m = Next(MCrown);
                    m.position = head + v.transform.up * (PegDoll.HeadR * CrownRise * v.scale * root.lossyScale.y);
                    m.rotation = v.transform.rotation;
                    m.localScale = Vector3.one * (CrownOnHead * v.scale);
                }
                else if (w.kind == CampMark.PurpleHands)
                {
                    // S-ritual (B72): a purple dot at each arm's end (0.012 u; the pose carries the end beat, the colour is a bonus)
                    foreach (var arm in new[] { v.armL, v.armR })
                    {
                        var m = Next(MMouth);
                        m.position = arm.TransformPoint(new Vector3(0f, -PegDoll.ArmLen * v.scale, 0f));
                        m.rotation = v.transform.rotation;
                        m.localScale = Vector3.one * Mathf.Max(0.8f, v.scale) * PurpleHandDot;
                    }
                }
                else
                {
                    float k = w.fadeAt >= 0f ? 1f - Mathf.Clamp01((time - w.fadeAt) / 0.6f) : 1f;
                    var m = Next(MMouth);
                    m.position = head + v.transform.rotation * new Vector3(0f, -0.008f, 0.024f) * v.scale * root.lossyScale.y;
                    m.rotation = v.transform.rotation;
                    m.localScale = Vector3.one * Mathf.Max(0.7f, v.scale) * k;
                }
            }

            // ---- the part 5 tips in the hands (CampLife.DrawTips leaves these kinds to us)
            foreach (var s in scenes)
                foreach (var tip in s.tips)
                {
                    if (tip.kind < CampTipKind.FlatStone) continue;
                    float u = time - s.t0;
                    if (u < tip.from || u >= tip.to || tip.member >= s.Count) continue;
                    var v = pv.ViewOf(s.keys[tip.member]);
                    if (v == null || !v.gameObject.activeInHierarchy) continue;
                    var handR = v.armR.TransformPoint(new Vector3(0f, -PegDoll.ArmLen * v.scale, 0f));
                    var handL = v.armL.TransformPoint(new Vector3(0f, -PegDoll.ArmLen * v.scale, 0f));
                    var both = Vector3.Lerp(handR, handL, 0.5f);
                    float sc = Mathf.Max(0.8f, pv.ScaleOf(s.keys[tip.member], out float s0) ? s0 : 1f);
                    Transform m;
                    switch (tip.kind)
                    {
                        case CampTipKind.FlatStone: m = Next(MFlatStone); m.position = handR; m.localScale = Vector3.one * 0.012f * sc; break;
                        case CampTipKind.Reeds: m = Next(MReeds); m.position = both + Vector3.up * (0.01f * root.lossyScale.y); m.rotation = v.transform.rotation * Quaternion.Euler(70f, 0f, 0f); m.localScale = Vector3.one * sc; break;
                        case CampTipKind.Reed: m = Next(MReed); m.position = both; m.rotation = v.transform.rotation * Quaternion.Euler(90f, 0f, 0f); m.localScale = Vector3.one * sc; break;
                        case CampTipKind.Weed: m = Next(MWeed); m.position = handR; m.rotation = Quaternion.identity; m.localScale = Vector3.one * (sc * WeedInHand); break;
                        case CampTipKind.CrownRing: m = Next(MCrown); m.position = both; m.rotation = v.transform.rotation * Quaternion.Euler(70f, 0f, 0f); m.localScale = Vector3.one * sc; break;
                        case CampTipKind.Toy: m = Next(MToy); m.position = handR; m.localScale = Vector3.one * sc; break;
                        case CampTipKind.Beads: m = Next(MBeads); m.position = handR; m.localScale = Vector3.one * sc; break;
                        case CampTipKind.Rope: m = Next(MCoil); m.position = v.transform.TransformPoint(new Vector3(0.025f, PegDoll.BodyH * 0.75f, 0f) * v.scale); m.rotation = v.transform.rotation * Quaternion.Euler(0f, 0f, 70f); m.localScale = Vector3.one * sc; break;
                        case CampTipKind.Plank: m = Next(MPlank); m.position = both; m.rotation = v.transform.rotation * Quaternion.Euler(0f, 90f, 0f); m.localScale = Vector3.one * sc; break;
                        case CampTipKind.LeafFold: m = Next(MLeaf); m.position = both; m.rotation = v.transform.rotation; m.localScale = Vector3.one * sc; break;
                        case CampTipKind.GiftBowl: m = Next(MGiftBowl); m.position = handR; m.localScale = Vector3.one * sc; break;
                    }
                }

            for (int i = used; i < campPool.Count; i++) if (campPool[i] != null) campPool[i].gameObject.SetActive(false);
        }

        /// <summary>The part 5 props, each built from primitives (no new asset pack): sizes in box units, the props' 1.5 scale left to the numbers.</summary>
        Mesh CampMesh(int key)
        {
            if (campMeshes.TryGetValue(key, out var m) && m != null) return m;
            var mb = new MeshBuilder(6980 + key);
            var straw = new Color(0.86f, 0.77f, 0.52f);
            switch (key)
            {
                case MPole: // a short pole by the door, a crossbar at the top for the lantern
                    mb.Box(new Vector3(0f, CampGeo5.PoleH * 0.5f, 0f), new Vector3(0.008f, CampGeo5.PoleH, 0.008f), Palette.WoodLight);
                    mb.Box(new Vector3(0f, CampGeo5.PoleH - 0.004f, 0f), new Vector3(0.02f, 0.004f, 0.005f), Palette.Bark);
                    break;
                case MBracket: // 2026-11-24: a house lantern's iron arm out of the wall (+z out), the hook down at its end
                    mb.Box(new Vector3(0f, 0f, VillageLanternArm * 0.5f), new Vector3(0.004f, 0.004f, VillageLanternArm), Palette.Iron);
                    mb.Box(new Vector3(0f, -0.002f, VillageLanternArm), new Vector3(0.003f, 0.006f, 0.003f), Palette.Iron);
                    break;
                case MLantern:
                case MLanternLit: // a small lantern hanging from the bar: a dark cap, a body (glowing when lit), a base
                    mb.Box(new Vector3(0f, -0.006f, 0f), new Vector3(0.022f, 0.005f, 0.022f), Palette.Iron);
                    if (key == MLanternLit) mb.emission = 1f;
                    mb.Box(new Vector3(0f, -0.019f, 0f), new Vector3(0.017f, 0.022f, 0.017f), key == MLanternLit ? Palette.WindowLit : new Color(0.55f, 0.5f, 0.42f));
                    mb.emission = 0f;
                    mb.Box(new Vector3(0f, -0.032f, 0f), new Vector3(0.02f, 0.004f, 0.02f), Palette.Iron);
                    break;
                case MToy: // a small wooden figure (0.02 u) on a string
                    mb.Box(new Vector3(0f, 0.012f, 0f), new Vector3(0.002f, 0.024f, 0.002f), straw);
                    mb.Box(new Vector3(0f, -0.004f, 0f), new Vector3(0.012f, 0.014f, 0.008f), Palette.Terracotta);
                    mb.Ico(new Vector3(0f, 0.007f, 0f), 0.006f, Vector3.one, Palette.WoodLight, 0.05f);
                    break;
                case MBeads: // a string of beads (0.03 u), bright
                    mb.Box(new Vector3(0f, 0.005f, 0f), new Vector3(0.002f, 0.02f, 0.002f), straw);
                    for (int i = 0; i < 4; i++) mb.Ico(new Vector3(0f, -0.006f * i, 0f), 0.0045f, Vector3.one, Palette.Flowers[i % Palette.Flowers.Length], 0.05f);
                    break;
                case MStrip: // a brown meat strip 0.06 x 0.015 u hanging from the bar
                    mb.Box(new Vector3(0f, -CampGeo5.StripLong * 0.5f, 0f), new Vector3(CampGeo5.StripWide * 0.3f, CampGeo5.StripLong, CampGeo5.StripWide), new Color(0.45f, 0.22f, 0.15f));
                    break;
                case MBoat: // a folded leaf 0.03 u floating
                    mb.Ico(new Vector3(0f, 0.002f, 0f), 1f, new Vector3(CampGeo5.BoatSize * 0.3f, 0.004f, CampGeo5.BoatSize * 0.5f), Palette.LeafC, 0.05f);
                    mb.Box(new Vector3(0f, 0.005f, 0f), new Vector3(0.002f, 0.006f, CampGeo5.BoatSize * 0.7f), Palette.LeafB);
                    break;
                case MLeaf: // the leaf folded in the hands
                    mb.Ico(Vector3.zero, 1f, new Vector3(0.012f, 0.006f, 0.018f), Palette.LeafC, 0.05f);
                    break;
                case MLeaf + 100: LandTipMesh(CampTipKind.Bunch, mb); break; // the gift flower: the bunch's look
                case MCrown: // a green ring 0.05 u across with three bright flower dots
                    for (int i = 0; i < 8; i++) { float a = i * Mathf.PI / 4f; mb.Ico(new Vector3(Mathf.Cos(a) * 0.022f, 0f, Mathf.Sin(a) * 0.022f), 0.006f, Vector3.one, Palette.LeafA, 0.05f); }
                    for (int i = 0; i < 3; i++) { float a = i * 2.1f + 0.4f; mb.Ico(new Vector3(Mathf.Cos(a) * 0.023f, 0.004f, Mathf.Sin(a) * 0.023f), 0.006f, Vector3.one, BunchHeads[i], 0.05f); }
                    break;
                case MMouth: mb.Ico(Vector3.zero, 1f, new Vector3(0.0075f, 0.005f, 0.003f), new Color(0.35f, 0.1f, 0.35f), 0.02f); break; // the purple mouth (0.015 u)
                case MGiftBowl: // a small bowl of berries
                    mb.Prism(Vector3.zero, 0.009f, 0.012f, 0.008f, 7, Palette.WoodLight);
                    for (int i = 0; i < 3; i++) mb.Ico(new Vector3((i - 1) * 0.005f, 0.009f, 0f), 0.004f, Vector3.one, new Color(0.6f, 0.1f, 0.3f), 0.05f);
                    break;
                case MPool: mb.Ico(Vector3.zero, 1f, new Vector3(1f, 0.02f, 1f), Palette.Water, 0.04f); break; // the dam's little pool (scaled to r)
                case MDamStone: mb.Ico(new Vector3(0f, 0.006f, 0f), 0.01f, new Vector3(1f, 0.7f, 0.9f), Palette.Rock, 0.15f, Palette.RockDark); break;
                case MRope: mb.Box(new Vector3(0f, 0f, 0.5f), new Vector3(1f, 1f, 1f), SwingRope); break; // a unit rope along local z (scaled: width, width, length)
                case MPlank: mb.Box(Vector3.zero, new Vector3(CampGeo5.PlankD, CampGeo5.PlankT, CampGeo5.PlankW), SwingPlank); break; // long across the swing (local z)
                case MMoth: mb.Ico(Vector3.zero, 0.5f, new Vector3(1.2f, 0.5f, 1f), new Color(1f, 0.97f, 0.75f), 0.02f); break; // [est] pale yellow specks 0.016 u
                case MFlatStone: mb.Ico(Vector3.zero, 1f, new Vector3(1f, 0.35f, 0.8f), Palette.RockLight, 0.12f, Palette.RockDark); break;
                case MReeds: // #692 p5 retakes (the pale armful vanished on sand): seven green and straw stalks with brown cattail heads [est]
                    for (int i = 0; i < 7; i++)
                    {
                        var q = Quaternion.Euler(0f, 0f, (i - 3) * 4f);
                        mb.Box(new Vector3((i - 3) * 0.005f, 0f, 0f), new Vector3(0.005f, 0.13f, 0.005f), i % 2 == 0 ? ReedGreen : ReedStraw, q);
                        if (i % 2 == 1) mb.Box(new Vector3((i - 3) * 0.005f, 0f, 0f) + q * new Vector3(0f, 0.065f, 0f), new Vector3(0.008f, 0.022f, 0.008f), ReedHead, q);
                    }
                    break;
                case MReed: mb.Box(Vector3.zero, new Vector3(0.006f, 0.08f, 0.006f), ReedGreen); break;
                case MWeed: // a weed with its dangling root
                    for (int i = 0; i < 3; i++) mb.Ico(new Vector3((i - 1) * 0.006f, 0.012f, 0f), 0.006f, new Vector3(1f, 1.6f, 0.6f), Palette.LeafB, 0.08f);
                    mb.Box(new Vector3(0f, -0.006f, 0f), new Vector3(0.002f, 0.02f, 0.002f), Palette.SoilLight);
                    mb.Box(new Vector3(0.003f, -0.013f, 0f), new Vector3(0.0015f, 0.01f, 0.0015f), Palette.SoilLight, Quaternion.Euler(0f, 0f, 35f));
                    break;
                case MBough: // #692 p5 retakes: a low bough out of the trunk toward the camera (0.1 u, bark), a leafy tip [est]
                    mb.Box(new Vector3(0f, 0f, 0.07f), new Vector3(0.02f, 0.016f, 0.14f), Palette.Bark);
                    mb.Ico(new Vector3(0f, 0.012f, 0.14f), 0.02f, Vector3.one, Palette.LeafA, 0.1f);
                    break;
                case MBranch: mb.Box(new Vector3(0f, 0f, 0.5f), new Vector3(0.022f, 0.018f, 1f), Palette.Bark); break; // #692 p5 notes: a unit branch along z (scaled to its length)
                case MCoil: // the coiled rope over the shoulder
                    for (int i = 0; i < 8; i++) { float a = i * Mathf.PI / 4f; mb.Box(new Vector3(Mathf.Cos(a) * 0.012f, Mathf.Sin(a) * 0.012f, 0f), new Vector3(0.008f, 0.004f, 0.004f), straw, Quaternion.Euler(0f, 0f, a * Mathf.Rad2Deg + 90f)); }
                    break;
            }
            m = mb.ToMesh("Camp_P5_" + key); m.hideFlags = HideFlags.DontSave;
            return campMeshes[key] = m;
        }

        void DestroyCamp()
        {
            foreach (var t in campPool) if (t != null) UnityEngine.Object.Destroy(t.gameObject);
            campPool.Clear();
        }

        // ================================================================== tests and the readout
        public string CampMarks() => $"tents {Tents().Count} (things {CountThings()}, lit {LitCount()}), strips {meatStrips}, worn {worn5.Count}, gifts {gifts.Count}, dams {dams.Count} (stones {damStones.Count}), boats {boats.Count}, swing {(swingHung ? "hung" : swingRope ? "rope" : "none")}";
        int CountThings() { int n = 0; foreach (var l in tentThings.Values) n += l.Count; return n; }
        int LitCount() { int n = 0; foreach (var t in Tents()) if (Lit(t.id)) n++; return n; }
        public int TentThingsNow => CountThings();
        public int LitNow => LitCount();
        public int StripsNow => meatStrips;
        public int Worn5Now => worn5.Count;
        public bool WearingCrown(int key) { foreach (var w in worn5) if (w.key == key && w.kind == CampMark.Crown) return true; return false; }
        public int GiftsNow => gifts.Count;
        public int DamsNow => dams.Count;
        public int DamStonesNow => damStones.Count;
        public int BoatsNow => boats.Count;
        public bool SwingHung => swingHung;
        public Vector2 SwingPivot => swingPivot;
        public int SwingTreeCell => swingCell;
    }
}

namespace AquariumCiv.View
{
    /// <summary>#692 part 5 cheats (SliceDebugPanel's "#692" block): each starts the use now with the people nearest its thing.</summary>
    public static partial class CampCheats
    {
        public static string TentPeek() => Do(CampUse.TentPeek);
        public static string TentToy() => Do(CampUse.TentToy);
        public static string TentDoorSit() => Do(CampUse.TentDoorSit);
        public static string SkyLook() => Do(CampUse.SkyLook); // #687
        public static string CountStash() => Do(CampUse.CountStash);
        public static string SnackSneak() => Do(CampUse.SnackSneak);
        public static string TendRacks() => Do(CampUse.TendRacks);
        public static string HangMeat() { var c = Camp; return c == null ? "no PeopleView" : c.HangMeat(); }
        /// <summary>#692 p5 retakes: six dry fish hang on the rack and stay (no take-down: "Fish on the rack" sends someone to take them down,
        /// so a count or a sneak lost its fish mid-way).</summary>
        public static string FishStay()
        {
            var sim = AquariumCiv.Game.GameClock.Instance != null ? AquariumCiv.Game.GameClock.Instance.Sim : null;
            var pv = PeopleView.Find();
            if (sim == null || pv == null) return "no sim";
            return $"{sim.DebugFishOnRack(6, pv.Now.day)} dry fish on the rack (they stay)";
        }
        public static string DuckUnderHides() => Do(CampUse.DuckUnderHides);
        public static string CountFish() => Do(CampUse.CountFish);
        public static string SneakFish() => Do(CampUse.SneakFish);
        public static string ThicketPeek() => Do(CampUse.ThicketPeek);
        public static string BerryKid() => Do(CampUse.BerryWipe);
        public static string ClimbTree() => Do(CampUse.ClimbTree);
        public static string TreeNap() => Do(CampUse.TreeNap);
        public static string TrunkLean() => Do(CampUse.TrunkLean);
        public static string SkipStones() => Do(CampUse.SkipStones);
        public static string LeafBoat() => Do(CampUse.LeafBoat);
        public static string FeetInWater() => Do(CampUse.FeetInWater);
        public static string PuddleLook() => Do(CampUse.PuddleLook);
        public static string PuddleRace() => Do(CampUse.PuddleRace);
        public static string CutReeds() => Do(CampUse.CutReeds);
        /// <summary>AnimLab and the tests (#692 p5): the thicket and rabbits by the camp; reeds at the water's edge by the camp.</summary>
        public static string ThicketReady() { var c = Camp; return c == null ? "no PeopleView" : c.ThicketReady(); }
        public static string ReedsReady() { var c = Camp; return c == null ? "no PeopleView" : c.ReedsReady(); }
        public static string ReedWhistle() => Do(CampUse.ReedWhistle);
        public static string FollowStream() => Do(CampUse.FollowStream);
        public static string BuildDam() => Do(CampUse.BuildDam);
        public static string HillDusk() => Do(CampUse.HillDusk);
        public static string HillRoll() => Do(CampUse.HillRoll);
        public static string PathRace() => Do(CampUse.PathRace);
        public static string PathChat() => Do(CampUse.PathChat);
        public static string StoneGift() => Do(CampUse.StoneGift);
        public static string LightLanterns() => Do(CampUse.LightLanterns);
        public static string ChaseMoths() => Do(CampUse.ChaseMoths);
        public static string PullWeed() => Do(CampUse.PullWeed);
        public static string Crown() => Do(CampUse.Crown);
        public static string HangSwing() => Do(CampUse.HangSwing);
        public static string Swing() => Do(CampUse.Swing);
        public static string CampMarks() { var c = Camp; return c == null ? "no PeopleView" : c.CampMarks(); }
    }
}
