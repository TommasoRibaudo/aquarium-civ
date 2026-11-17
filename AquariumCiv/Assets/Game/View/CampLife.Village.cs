// #695 part 1 (docs/design/animation-specs.md "#695: the village and the town", B57.a-j, B58.c/f/g/j/k): CampLife's side of the village's and
// the town's things. Each frame in a village or a town box it decides which use starts (the D rules as the camp's: life uses borrow free time
// only; the two work uses, a home mended and a fence fixed, borrow a free grown-up [est: as #693, so no plan starves]; how often by the view
// day's roll; one use per thing at a time), works out the spots from the drawn pieces (the fields' furrows and gates, the well's rim, the mill's
// foot, the lumber mill's woodpile and lying log, the bakery's door, the streets, the field fences), and draws what the uses leave or hold: the
// scarecrow (it stays the growing season), a home's flower box, painted door or herbs (they stay), the ladder, the buckets set down, the
// pebble, the cup, the crust, the hopscotch grid (a view day), the leaning post. View-only and not saved [est], except a home's things, which
// also come back from the home's age (one per ten years [est]). The scenes are pure builders in View/Beats/VillageScenes.cs. Cheats: the
// "#695" block in SliceDebugPanel ("Jump: a village box", "Jump: a town box" first).
using System.Collections.Generic;
using AquariumCiv.Life;
using AquariumCiv.Sim;
using UnityEngine;
using Terrain = AquariumCiv.Sim.Settle.Terrain;

namespace AquariumCiv.View
{
    public sealed partial class CampLife
    {
        static bool IsVillageUse(CampUse u) => u >= CampUse.FieldTag && u <= CampUse.FenceLean;
        const int ThingVillageBase = 70000, ThingWell = ThingVillageBase + 1, ThingMill = ThingVillageBase + 2, ThingPile = ThingVillageBase + 3,
            ThingLog = ThingVillageBase + 4, ThingBakery = ThingVillageBase + 5, ThingStreet = ThingVillageBase + 6, ThingFieldBase = ThingVillageBase + 100,
            ThingHomeBase = ThingVillageBase + 300, ThingFenceBase = ThingVillageBase + 200;
        /// <summary>[est] a home gets one more of its things every this many years, and at most <see cref="MaxExtras"/>.</summary>
        public const int ExtraEveryYears = 10, MaxExtras = 3;

        long villageDay = long.MinValue;
        /// <summary>B57.b: the scarecrow of each field (by the field's seed): where, which year, how far made (1 cross, 2 straw, 3 hat).</summary>
        readonly Dictionary<int, (Vector2 at, long year, int stage)> scarecrows = new();
        /// <summary>B57.d: each home's things (by its seed), counted on first sight from its age and one more for each mend this session.</summary>
        readonly Dictionary<int, int> homeExtras = new();
        /// <summary>B58.f: the hopscotch grids scratched today (start, yaw); B58.j: the posts set straight today (where, outward, lean, sunk).</summary>
        readonly List<(Vector2 at, float yaw, long day)> grids = new();
        readonly Dictionary<CampScene, (Vector2 at, Vector2 outward, float lean, float sunk, long day)> posts = new();
        readonly List<(Vector2 at, Vector2 outward, float sunk, long day)> postsLeft = new();
        readonly List<Transform> villagePool = new();
        readonly Dictionary<int, Mesh> villageMeshes = new();
        int birdPerched = -1;

        void ResetVillage()
        {
            villageDay = long.MinValue; scarecrows.Clear(); homeExtras.Clear(); grids.Clear(); posts.Clear(); postsLeft.Clear(); birdPerched = -1;
        }

        void DestroyVillage()
        {
            foreach (var t in villagePool) if (t != null) { if (Application.isPlaying) UnityEngine.Object.Destroy(t.gameObject); else UnityEngine.Object.DestroyImmediate(t.gameObject); }
            villagePool.Clear();
        }

        // ================================================================== the things, from the drawn layout
        BoxLayout Layout => LandBox != null ? LandBox.layout : null;
        bool VillageEra => sim != null && sim.State.era != Era.Camp;

        static Vector2 LotPoint(PieceData p, float lx, float lz) { var w = Quaternion.Euler(0f, p.yaw, 0f) * new Vector3(lx, 0f, lz); return new Vector2(p.x + w.x, p.z + w.z); }
        static Vector2 LotDir(PieceData p, float lx, float lz) { var w = Quaternion.Euler(0f, p.yaw, 0f) * new Vector3(lx, 0f, lz); return new Vector2(w.x, w.z).normalized; }

        /// <summary>The pieces of this kind standing now (grown, not the camp's), nearest <paramref name="near"/> first.</summary>
        List<PieceData> PiecesOf(PieceKind kind, Vector2 near)
        {
            var r = new List<PieceData>();
            var L = Layout;
            if (L == null) return r;
            foreach (var p in L.pieces) if (p.kind == kind && p.era != Era.Camp && p.grow > 0.95f && Terrain.InBox(p.x, p.z, 0.3f)) r.Add(p);
            r.Sort((a, b) => (a.Pos - near).sqrMagnitude.CompareTo((b.Pos - near).sqrMagnitude));
            return r;
        }

        Vector2 VillageMiddle()
        {
            var L = Layout;
            if (L != null) foreach (var p in L.pieces) if (p.kind == PieceKind.Well && p.era != Era.Camp) return p.Pos;
            if (meals != null && meals.HasFire) return meals.Fire;
            return new Vector2(0f, Terrain.CZ);
        }

        // ---- fields
        static bool InPoly(Vector2[] poly, Vector2 q)
        {
            bool inside = false;
            for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
                if ((poly[i].y > q.y) != (poly[j].y > q.y) && q.x < (poly[j].x - poly[i].x) * (q.y - poly[i].y) / (poly[j].y - poly[i].y) + poly[i].x) inside = !inside;
            return inside;
        }

        static float DistRing(Vector2[] poly, Vector2 q)
        {
            float best = float.MaxValue;
            for (int i = 0; i < poly.Length; i++)
            {
                Vector2 a = poly[i], b = poly[(i + 1) % poly.Length], ab = b - a;
                float t = ab.sqrMagnitude > 1e-8f ? Mathf.Clamp01(Vector2.Dot(q - a, ab) / ab.sqrMagnitude) : 0f;
                best = Mathf.Min(best, (q - (a + ab * t)).magnitude);
            }
            return best;
        }

        static Vector2 Centroid(Vector2[] poly) { var c = Vector2.zero; foreach (var q in poly) c += q; return c / Mathf.Max(1, poly.Length); }

        /// <summary>The crop fields standing now (wheat or sprouts, fenced, inside the box), nearest <paramref name="near"/> first.</summary>
        List<FieldData> Fields(Vector2 near, bool cropOnly = true)
        {
            var r = new List<FieldData>();
            var L = Layout;
            if (L == null) return r;
            foreach (var f in L.fields)
            {
                if (f.polygon == null || f.polygon.Length < 3) continue;
                if (cropOnly && f.crop != CropKind.Wheat && f.crop != CropKind.Sprout && f.crop != CropKind.Fallow) continue;
                var c = Centroid(f.polygon);
                if (!Terrain.InBox(c.x, c.y, 0.35f)) continue;
                r.Add(f);
            }
            r.Sort((a, b) => (Centroid(a.polygon) - near).sqrMagnitude.CompareTo((Centroid(b.polygon) - near).sqrMagnitude));
            return r;
        }

        /// <summary>The field's gate: the middle of its gate edge, a step outside it and a step inside.</summary>
        static void Gate(FieldData f, out Vector2 outside, out Vector2 inside)
        {
            int n = f.polygon.Length, e = f.gateEdge >= 0 && f.gateEdge < n ? f.gateEdge : 0;
            var a = f.polygon[e]; var b = f.polygon[(e + 1) % n];
            var mid = (a + b) * 0.5f;
            var c = Centroid(f.polygon);
            var edge = (b - a).normalized; var nrm = new Vector2(-edge.y, edge.x);
            if (Vector2.Dot(nrm, mid - c) < 0f) nrm = -nrm;
            outside = mid + nrm * 0.12f; inside = mid - nrm * 0.09f;
        }

        /// <summary>The furrows (the paths between the crop rows, BoxView.FieldMesh's rows) inside the fence, at least 0.25 u long, the middle
        /// ones first: each a start and an end.</summary>
        static List<(Vector2 a, Vector2 b)> Furrows(FieldData f)
        {
            var res = new List<(Vector2 a, Vector2 b)>();
            float ang = f.rowYaw * Mathf.Deg2Rad;
            var across = new Vector2(Mathf.Cos(ang), -Mathf.Sin(ang)); var along = new Vector2(Mathf.Sin(ang), Mathf.Cos(ang));
            Vector2 min = f.polygon[0], max = f.polygon[0];
            foreach (var q in f.polygon) { min = Vector2.Min(min, q); max = Vector2.Max(max, q); }
            var c = (min + max) / 2f; float ext = (max - min).magnitude / 2f;
            for (float u = -ext; u <= ext; u += VillageGeo.RowGap)
            {
                (Vector2 a, Vector2 b) best = default; float bestLen = 0f;
                Vector2? runA = null; Vector2 last = default;
                for (float v = -ext; v <= ext + 0.01f; v += 0.01f)
                {
                    var q = c + across * u + along * v;
                    bool ok = InPoly(f.polygon, q) && DistRing(f.polygon, q) >= 0.07f;
                    if (ok) { runA ??= q; last = q; }
                    if ((!ok || v > ext) && runA.HasValue) { float len = (last - runA.Value).magnitude; if (len > bestLen) { bestLen = len; best = (runA.Value, last); } runA = null; }
                }
                if (bestLen >= 0.25f) res.Add(best);
            }
            var mid = Centroid(f.polygon);
            res.Sort((x, y) => (((x.a + x.b) * 0.5f) - mid).sqrMagnitude.CompareTo((((y.a + y.b) * 0.5f) - mid).sqrMagnitude));
            return res;
        }

        /// <summary>A fence edge of the field (not its gate edge) whose outside faces the camera most: its ends and its outward normal.</summary>
        bool FenceEdge(FieldData f, out Vector2 a, out Vector2 b, out Vector2 outward)
        {
            a = b = outward = default;
            if (!f.fence) return false;
            int n = f.polygon.Length; float best = -9f;
            var c = Centroid(f.polygon);
            for (int e = 0; e < n; e++)
            {
                if (e == f.gateEdge) continue;
                var p = f.polygon[e]; var q = f.polygon[(e + 1) % n];
                if ((q - p).magnitude < 0.3f) continue;
                var mid = (p + q) * 0.5f; var ed = (q - p).normalized; var nrm = new Vector2(-ed.y, ed.x);
                if (Vector2.Dot(nrm, mid - c) < 0f) nrm = -nrm;
                if (!Terrain.InBox(mid.x + nrm.x * 0.3f, mid.y + nrm.y * 0.3f, 0.2f)) continue; // room outside for the people
                float d = Vector2.Dot(nrm, ToCam(mid));
                if (d > best) { best = d; a = p; b = q; outward = nrm; }
            }
            return best > -9f;
        }

        /// <summary>The middle between two of the fence's posts on an edge (Pieces.Fence: posts every ~0.2 u), near the edge's middle.</summary>
        static Vector2 BetweenPosts(Vector2 a, Vector2 b)
        {
            int n = Mathf.Max(1, Mathf.RoundToInt((b - a).magnitude / 0.2f));
            float k = (Mathf.Floor(n / 2f) + (n % 2 == 1 ? 0.5f : -0.5f)) / n;
            if (n == 1) k = 0.5f;
            return Vector2.Lerp(a, b, Mathf.Clamp01(k));
        }

        int FieldKey(FieldData f) => f.seed;

        // ---- the well, the mill, the lumber mill, the bakery, homes, streets
        bool WellOf(Vector2 near, out PieceData well, out float rimR, bool villageOnly = false)
        {
            well = null; rimR = VillageGeo.WellR;
            foreach (var p in PiecesOf(PieceKind.Well, near)) { if (villageOnly && p.era == Era.Town) continue; well = p; rimR = p.era == Era.Town ? VillageGeo.FountainR : VillageGeo.WellR; return true; }
            return false;
        }

        /// <summary>The well's free side toward the camera: its roof posts stand at the lot's ±x, so the kid leans in between them.</summary>
        Vector2 WellSide(PieceData well)
        {
            var toCam = ToCam(well.Pos); Vector2 best = toCam; float bd = -9f;
            foreach (float sgn in new[] { 1f, -1f })
                foreach (float rot in new[] { -25f, 25f })
                {
                    var d = CampScenes5.Rot(LotDir(well, 0f, sgn), rot);
                    float dd = Vector2.Dot(d, toCam);
                    if (dd > bd) { bd = dd; best = d; }
                }
            return best;
        }

        float SailDegPerSec => WindView.Instance != null ? WindDrift.Degrees(WindView.Instance.Wind) : 30f;

        /// <summary>The lumber mill's woodpile (Pieces.Lumbermill: at the lot's (−w·0.2, −d·0.25), logs along the lot's x): two seats on its low
        /// front log on the side toward the camera, and the way out from it.</summary>
        void PileSeats(PieceData p, out Vector2 a, out Vector2 b, out Vector2 outward)
        {
            var pile = LotPoint(p, -p.w * 0.2f, -p.d * 0.25f);
            var zAxis = LotDir(p, 0f, 1f); var xAxis = LotDir(p, 1f, 0f);
            outward = Vector2.Dot(zAxis, ToCam(pile)) >= 0f ? zAxis : -zAxis;
            var log = pile + outward * (0.0825f + 0.012f);
            a = log - xAxis * 0.055f; b = log + xAxis * 0.055f;
        }

        /// <summary>The lumber mill's lying log (Pieces.Lumbermill: centre (w·0.18, 0.03, −d·0.25), 0.3 long along the lot's z turned 10 deg):
        /// its two ends, a little in, the one nearer <paramref name="from"/> first.</summary>
        void LogEnds(PieceData p, Vector2 from, out Vector2 a, out Vector2 b)
        {
            var c = LotPoint(p, p.w * 0.18f, -p.d * 0.25f);
            var w = Quaternion.Euler(0f, p.yaw + 10f, 0f) * Vector3.forward; var d = new Vector2(w.x, w.z).normalized;
            a = c - d * (VillageGeo.LogLen * 0.5f - 0.03f); b = c + d * (VillageGeo.LogLen * 0.5f - 0.03f);
            if ((b - from).sqrMagnitude < (a - from).sqrMagnitude) (a, b) = (b, a);
        }

        /// <summary>The town bakery's door (PiecesTown.TownBakery: left of the front) and the way out of it.</summary>
        static void BakeryDoor(PieceData p, out Vector2 door, out Vector2 outward)
        {
            float hw = p.w * 0.82f, hd = Mathf.Min(p.d * 0.7f, 0.3f), hz = p.d / 2f - hd / 2f - 0.03f;
            door = LotPoint(p, -hw * 0.22f, hz - hd / 2f - 0.01f);
            outward = LotDir(p, 0f, -1f);
        }

        /// <summary>A home's front (Pieces.Draw's Home: the house's front wall at the lot's z = −d/2 + 0.02, the door at x = +0.18·w, the window
        /// at −0.2·w): the wall's z, its width, the wall's height and how many storeys.</summary>
        static void HomeFront(PieceData p, out float frontZ, out float hw, out float wallH)
        {
            bool town = p.era == Era.Town;
            int storeys = p.storeys > 0 ? p.storeys : town ? 2 : 1;
            hw = p.w * 0.92f; frontZ = -p.d / 2f + 0.02f;
            wallH = storeys > 1 ? 0.2f : town ? 0.27f : 0.3f;
        }

        int ExtrasOf(PieceData home)
        {
            if (homeExtras.TryGetValue(home.seed, out int n)) return n;
            int lotId = (home.seed - 7) / 101, years = 0;
            if (sim != null) foreach (var l in sim.State.settle.lots) if (l.id == lotId) { years = (int)((sim.State.month - l.foundedMonth) / 12); break; }
            n = Mathf.Clamp(years / ExtraEveryYears, 0, MaxExtras);
            homeExtras[home.seed] = n;
            return n;
        }

        /// <summary>The home's things in the order they come (a flower box, a painted door, a string of herbs), shuffled by the home's seed.</summary>
        static int ExtraKind(PieceData home, int k) { int s = Mathf.Abs(home.seed) % 6; int[][] orders = { new[] { 0, 1, 2 }, new[] { 0, 2, 1 }, new[] { 1, 0, 2 }, new[] { 1, 2, 0 }, new[] { 2, 0, 1 }, new[] { 2, 1, 0 } }; return orders[s][k]; }

        /// <summary>Where a home's thing sits (lot-local x, y, z): 0 the flower box under the window, 1 over the door, 2 the herbs beside the door.</summary>
        static Vector3 ExtraLocal(PieceData home, int kind)
        {
            HomeFront(home, out float fz, out float hw, out float wallH);
            float wy = 0.1f + wallH * 0.6f;
            return kind switch
            {
                0 => new Vector3(-hw * 0.2f, wy - 0.075f, fz - 0.03f),
                1 => new Vector3(hw * 0.18f, 0.19f, fz - 0.009f),
                _ => new Vector3(hw * 0.18f + 0.09f, 0.29f, fz - 0.012f),
            };
        }

        /// <summary>A straight bit of a street (a laid or worn path, inside the box, clear of the pieces) near <paramref name="near"/>: its start
        /// and its way, <paramref name="len"/> long.</summary>
        bool StreetBit(Vector2 near, float len, out Vector2 start, out Vector2 along, float clear = 0.03f)
        {
            start = along = default;
            var L = Layout;
            if (L == null) return false;
            float best = float.MaxValue;
            foreach (var path in L.paths)
            {
                var pts = path.points;
                if (pts == null || pts.Length < 2) continue;
                // a worn path is a polyline of short steps: from each point, the first point len along, if the steps between keep straight
                for (int k = 0; k + 1 < pts.Length; k++)
                {
                    float acc = 0f; int j = k;
                    while (j + 1 < pts.Length && acc < len) { acc += (pts[j + 1] - pts[j]).magnitude; j++; }
                    if (acc < len) break;
                    var s0 = pts[k]; var d = (pts[j] - s0).normalized; var s1 = s0 + d * len;
                    bool straight = true;
                    for (int q = k + 1; q < j; q++) { var r = pts[q] - s0; if (Mathf.Abs(r.x * d.y - r.y * d.x) > 0.035f) { straight = false; break; } }
                    if (!straight) continue;
                    var m = (s0 + s1) * 0.5f;
                    if (!Terrain.InBox(s0.x, s0.y, 0.4f) || !Terrain.InBox(s1.x, s1.y, 0.4f)) continue;
                    bool blocked = false;
                    foreach (var p in L.pieces)
                    {
                        if (p.era == Era.Camp && p.kind != PieceKind.Fire) continue;
                        float r = Mathf.Max(p.w, p.d) * 0.42f + clear;
                        for (float t = 0f; t <= 1.001f && !blocked; t += 0.25f) if ((p.Pos - Vector2.Lerp(s0, s1, t)).magnitude < r) blocked = true;
                        if (blocked) break;
                    }
                    if (blocked) continue;
                    float dd = (m - near).sqrMagnitude;
                    if (dd < best) { best = dd; start = s0; along = d; }
                }
            }
            return best < float.MaxValue;
        }

        /// <summary>The nearest point of any street to <paramref name="p"/> (far: none).</summary>
        float StreetDist(Vector2 p)
        {
            var L = Layout; float best = 9f;
            if (L == null) return best;
            foreach (var path in L.paths)
            {
                if (path.points == null) continue;
                for (int k = 0; k + 1 < path.points.Length; k++)
                {
                    Vector2 a = path.points[k], b = path.points[k + 1], ab = b - a;
                    float t = ab.sqrMagnitude > 1e-8f ? Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude) : 0f;
                    best = Mathf.Min(best, (p - (a + ab * t)).magnitude);
                }
            }
            return best;
        }

        // ================================================================== each frame in a village or a town
        void StepVillage(IReadOnlyList<PersonActivity> acts, float time)
        {
            if (!VillageEra || planner == null || Layout == null) return;
            if (now.day != villageDay)
            {
                villageDay = now.day;
                grids.RemoveAll(g => g.day != now.day);
                postsLeft.RemoveAll(p => p.day != now.day);
                var season0 = LifeClock.SeasonOf(now.day);
                if (season0 is Season.Autumn or Season.Winter) scarecrows.Clear(); // [est] it stays the growing season
            }
            if (OnlyCheats || meals == null || meals.Eaters > 0) return;
            float h = Hour;
            var season = LifeClock.SeasonOf(now.day);
            var weather = planner.WeatherOn(now.day);
            bool wet = weather is Weather.KeeperRain or Weather.Storm or Weather.Snow;
            bool day = h >= 9f && h < 16.5f;
            int cal = SeasonCalendar.OfMonth(now.day);
            bool town = sim.State.era == Era.Town;
            bool Go(CampUse u, float chance, int thing)
            {
                if (doneToday.Contains(u) || Roll(u) >= chance || !LifeOk(u) || ThingBusy(thing)) return false;
                if (triedAt.TryGetValue(u, out float tr) && clock - tr < 3f && clock >= tr) return false;
                triedAt[u] = clock; return true;
            }
            // B57.c: the harvest dance, the last harvest day (calendar month 10 [est]), late afternoon
            if (cal == 10 && h >= 15f && h < 17f && Go(CampUse.HarvestDance, 1f, 0) && TryHarvestDance(acts, time, false) == null) return;
            // B58.c: kids at the bakery door in the morning (often)
            if (town && h >= 7f && h < 9.5f && Go(CampUse.CrustWait, Often, ThingBakery) && TryCrust(acts, time, false) == null) return;
            // B57.e: gossip at the well in the morning (often)
            if (h >= 7.5f && h < 10.5f && Go(CampUse.WellGossip, Often, ThingWell) && TryGossip(acts, time, false) == null) return;
            // B57.d (work): a home mended and dressed, about once a season [est: one view day in three]
            if (h >= 9f && h < 12f && !wet && Go(CampUse.HomeMend, 0.34f, 0) && TryHomeMend(acts, time, false) == null) return;
            // B58.j (work): a leaning fence post fixed (sometimes), mornings
            if (h >= 8f && h < 11.5f && !wet && Go(CampUse.FenceFix, Sometimes, 0) && TryFenceFix(acts, time, false) == null) return;
            // B58.g: two who meet in a street stop to talk (often, as B50.b)
            if (h >= 8f && h < 18f && Go(CampUse.StreetChat, Often, ThingStreet) && TryStreetChat(acts, time, false) == null) return;
            // B57.i: a break on the log pile, midday (often)
            if (h >= 11.5f && h < 14f && Go(CampUse.LogPileSit, Often, ThingPile) && TryLogPile(acts, time, false) == null) return;
            if (!day || wet) return;
            // B57.b: a scarecrow, spring after sowing (rare: once a year per field)
            if (season == Season.Spring && cal >= 3 && Go(CampUse.Scarecrow, Rare, 0) && TryScarecrow(acts, time, false) == null) return;
            // B57.a: kids run between the rows, summer, crops up (sometimes)
            if (season == Season.Summer && Go(CampUse.FieldTag, Sometimes, 0) && TryFieldTag(acts, time, false) == null) return;
            // B58.k: on the fences (often)
            if (Go(CampUse.FenceLean, Often, 0) && TryFenceLean(acts, time, false) == null) return;
            // B57.f: a pebble down the village well (sometimes)
            if (!town && Go(CampUse.WellPebble, Sometimes, ThingWell) && TryPebble(acts, time, false) == null) return;
            // B57.g, B57.h: the windmill (sometimes; the shadow race on clear days)
            if (Go(CampUse.SailWatch, Sometimes, ThingMill) && TrySailWatch(acts, time, false) == null) return;
            if (weather == Weather.Clear && Go(CampUse.SailShadow, Sometimes, ThingMill) && TrySailShadow(acts, time, false) == null) return;
            // B57.j: a kid balances on a log (sometimes)
            if (Go(CampUse.LogBalance, Sometimes, ThingLog) && TryLogBalance(acts, time, false) == null) return;
            // B58.f: hopscotch in a street (sometimes)
            if (Go(CampUse.Hopscotch, Sometimes, ThingStreet) && TryHopscotch(acts, time, false) == null) return;
        }

        string StartV(CampScene s, int thing, CampUse use)
        {
            Add(Thing(s, thing));
            doneToday.Add(use);
            return null;
        }

        // ================================================================== who (crowd keys are negative: CampLife's Nearest reads -1 as nobody, so
        // the village picks with its own list; most of a village's grown-ups are the crowd)
        const int Nobody = VillageScenes.Nobody;

        /// <summary>Up to <paramref name="max"/> people passing <paramref name="ok"/>, nearest <paramref name="near"/> first: free ones within
        /// BorrowReach, or (a cheat) anyone drawn and not in a scene, a hunt or a tent within 9 u.</summary>
        List<int> VPick(IReadOnlyList<PersonActivity> acts, Vector2 near, System.Func<PersonActivity, bool> ok, int max, bool force, ICollection<int> not = null)
        {
            var cand = new List<(float d, int k)>();
            foreach (var a in acts)
            {
                if (not != null && not.Contains(a.who.Key)) continue;
                if (force ? byKey.ContainsKey(a.who.Key) || a.hunt != HuntPhase.None || a.tent != TentPhase.None : !Free(a, clock)) continue;
                if (!ok(a) || !(At(a.who.Key) is Vector2 p)) continue;
                float d = (p - near).magnitude;
                if (d <= (force ? 9f : BorrowReach)) cand.Add((d, a.who.Key));
            }
            cand.Sort((x, y) => x.d.CompareTo(y.d));
            var r = new List<int>();
            for (int i = 0; i < cand.Count && r.Count < max; i++) r.Add(cand[i].k);
            return r;
        }

        List<int> VKids(IReadOnlyList<PersonActivity> acts, Vector2 near, int max, bool force, ICollection<int> not = null) => VPick(acts, near, a => IsKid(a), max, force, not);
        int VGrown(IReadOnlyList<PersonActivity> acts, Vector2 near, bool force, ICollection<int> not = null) { var g = VPick(acts, near, a => IsGrown(a), 1, force, not); return g.Count > 0 ? g[0] : Nobody; }
        int VKid(IReadOnlyList<PersonActivity> acts, Vector2 near, bool force, ICollection<int> not = null) { var g = VPick(acts, near, a => IsKid(a), 1, force, not); return g.Count > 0 ? g[0] : Nobody; }

        // ================================================================== starting each use (null = started, else why not)
        string TryFieldTag(IReadOnlyList<PersonActivity> acts, float time, bool force)
        {
            foreach (var f in Fields(VillageMiddle()))
            {
                if (f.crop == CropKind.Fallow) continue;
                if (ThingBusy(ThingFieldBase + FieldKey(f) % 100)) continue;
                var rows = Furrows(f);
                if (rows.Count < 2) continue;
                Gate(f, out var gOut, out var gIn);
                var kids = VKids(acts, gOut, 3, force);
                if (kids.Count < 2) return "two kids needed ('Two kids join the camp' first)";
                // the rows next to each other, the middle ones
                rows.Sort((x, y) => Vector2.Dot(x.a - rows[0].a, new Vector2(-(rows[0].b - rows[0].a).y, (rows[0].b - rows[0].a).x).normalized).CompareTo(Vector2.Dot(y.a - rows[0].a, new Vector2(-(rows[0].b - rows[0].a).y, (rows[0].b - rows[0].a).x).normalized)));
                int mid = rows.Count / 2, first = Mathf.Clamp(mid - kids.Count / 2, 0, Mathf.Max(0, rows.Count - kids.Count));
                var use = rows.GetRange(first, Mathf.Min(kids.Count, rows.Count - first));
                // each furrow run from the gate's end
                for (int i = 0; i < use.Count; i++) if ((use[i].b - gIn).sqrMagnitude < (use[i].a - gIn).sqrMagnitude) use[i] = (use[i].b, use[i].a);
                int farmer = Nobody; Vector2 farmerAt = default;
                foreach (var a in acts)
                    if (a.kind is ActivityKind.Hoe or ActivityKind.Weed or ActivityKind.Sow or ActivityKind.Harvest && !byKey.ContainsKey(a.who.Key) && At(a.who.Key) is Vector2 p && InPoly(f.polygon, p))
                    { farmer = a.who.Key; farmerAt = p; break; }
                return StartV(VillageScenes.FieldTag(kids, AtAll(kids), use, gOut, gIn, farmer, farmerAt, time), ThingFieldBase + FieldKey(f) % 100, CampUse.FieldTag);
            }
            return "no crop field with rows in the box";
        }

        string TryScarecrow(IReadOnlyList<PersonActivity> acts, float time, bool force)
        {
            long year = now.day / 12;
            foreach (var f in Fields(VillageMiddle()))
            {
                if (!force && scarecrows.TryGetValue(FieldKey(f), out var sc) && sc.year == year) continue;
                Gate(f, out var gOut, out var gIn);
                var inward = (gIn - gOut).normalized;
                var spot = gIn + inward * 0.2f;
                if (!InPoly(f.polygon, spot) || DistRing(f.polygon, spot) < 0.12f) spot = Vector2.Lerp(gIn, Centroid(f.polygon), 0.5f);
                int g1 = VGrown(acts, gOut, force); if (g1 == Nobody) return "no grown-up free";
                int g2 = VGrown(acts, gOut, force, new[] { g1 }); if (g2 == Nobody) return "two grown-ups needed";
                int kid = VKid(acts, gOut, force);
                scarecrows[FieldKey(f)] = (spot, year, 0);
                // they come in at the gate: the scene's spots face the gate (the people stand between it and the gate)
                var s = VillageScenes.Scarecrow(g1, At(g1).Value, g2, At(g2).Value, kid, kid != Nobody ? At(kid).Value : default, spot, -inward, time);
                ViaGate(s, gOut, gIn, new[] { At(g1).Value, At(g2).Value, kid != Nobody ? At(kid).Value : gOut });
                s.kind = FieldKey(f);
                return StartV(s, ThingFieldBase + FieldKey(f) % 100, CampUse.Scarecrow);
            }
            return "no crop field in the box";
        }

        /// <summary>The cast of a field scene comes in by the gate and goes out by it: each member's first walk goes through the gate (outside,
        /// inside), and its last walk back out the same way.</summary>
        static void ViaGate(CampScene s, Vector2 gOut, Vector2 gIn, IList<Vector2> from)
        {
            for (int m = 0; m < s.Count; m++)
            {
                var l = s.parts[m];
                if (l.Count == 0) continue;
                l.Sort((x, y) => x.from.CompareTo(y.from));
                var first = l[0];
                float speed = Mathf.Max(first.speed, CampScenes1.Walk);
                var start = m < from.Count ? from[m] : gOut;
                float leg1 = CampScenes5.WalkTime(start, gOut, speed), leg2 = CampScenes5.WalkTime(gOut, gIn, speed);
                l.Insert(0, new CampPart { from = first.from - leg2, to = first.from, pos = gIn, face = first.pos, style = DollStyle.Stand, speed = speed, prop = first.prop });
                l.Insert(0, new CampPart { from = first.from - leg2 - leg1, to = first.from - leg2, pos = gOut, face = gIn, style = DollStyle.Stand, speed = speed, prop = first.prop });
                // the last walk away (a plain walk) goes out by the gate instead
                var last = l[^1];
                float end = last.to;
                if (last.pose == null && last.style == DollStyle.Stand && l.Count > 3) { end = last.from; l.RemoveAt(l.Count - 1); }
                var lastAt = l[^1].pos;
                float back1 = CampScenes5.WalkTime(lastAt, gIn, speed), back2 = CampScenes5.WalkTime(gIn, gOut, speed);
                l.Add(new CampPart { from = end, to = end + back1, pos = gIn, face = gOut, style = DollStyle.Stand, speed = speed });
                var away = gOut + (gOut - gIn).normalized * 0.18f + new Vector2(-(gOut - gIn).y, (gOut - gIn).x).normalized * ((m % 3) - 1) * 0.1f;
                l.Add(new CampPart { from = end + back1, to = end + back1 + back2 + 0.6f, pos = away, face = away + (gOut - gIn), style = DollStyle.Stand, speed = speed });
            }
            // the scene starts when the first walk to the gate does
            float shift = 0f; foreach (var l in s.parts) foreach (var p in l) shift = Mathf.Min(shift, p.from);
            if (shift < 0f)
            {
                foreach (var l in s.parts) foreach (var p in l) { p.from -= shift; p.to -= shift; }
                for (int i = 0; i < s.fx.Count; i++) { var f = s.fx[i]; f.time -= shift; s.fx[i] = f; }
                s.a -= shift; s.b -= shift; s.c -= shift; s.d -= shift;
            }
        }

        string TryHarvestDance(IReadOnlyList<PersonActivity> acts, float time, bool force)
        {
            var fields = Fields(VillageMiddle(), false);
            if (fields.Count == 0) return "no field in the box";
            var f = fields[0];
            Gate(f, out var gOut, out var gIn);
            var c = Centroid(f.polygon);
            var who = new List<int>();
            who.AddRange(VPick(acts, c, a => IsGrown(a), 6, force));
            who.AddRange(VPick(acts, c, a => IsKid(a), 4, force, who));
            if (who.Count < 4) return "four free people needed for the ring";
            float r = Mathf.Max(0.13f, VillageGeo.RingPer * who.Count / (Mathf.PI * 2f) * 6.3f);
            // the ring's middle: inside the fence with room for the ring
            var centre = c;
            if (DistRing(f.polygon, centre) < r + 0.09f) centre = Vector2.Lerp(c, gIn, 0.2f);
            var s = VillageScenes.HarvestDance(who, AtAll(who), centre, BeatHash.Range(12f, 16f, (int)today, 3, 6950), time);
            ViaGate(s, gOut, gIn, AtAll(who));
            return StartV(s, ThingFieldBase + FieldKey(f) % 100, CampUse.HarvestDance);
        }

        string TryHomeMend(IReadOnlyList<PersonActivity> acts, float time, bool force)
        {
            var homes = PiecesOf(PieceKind.Home, VillageMiddle());
            foreach (var home in homes)
            {
                int done = ExtrasOf(home);
                if (done >= MaxExtras && !force) continue;
                if (ThingBusy(ThingHomeBase + Mathf.Abs(home.seed) % 100)) continue;
                var front = LotDir(home, 0f, -1f);
                if (!force && Vector2.Dot(front, ToCam(home.Pos)) < -0.2f) continue; // [est] the natural mend happens where it can be seen: the front toward the camera
                HomeFront(home, out float fz, out float hw, out float wallH);
                var wall = LotPoint(home, -hw * 0.2f - 0.08f, fz);
                var foot = LotPoint(home, -hw * 0.2f - 0.08f, fz - 0.095f);
                if (done >= MaxExtras) { homeExtras[home.seed] = done = MaxExtras - 1; } // the cheat: the newest thing goes on again
                var ex = ExtraLocal(home, ExtraKind(home, done));
                var exAt = LotPoint(home, ex.x, ex.z);
                                int who = VGrown(acts, home.Pos, force); // [est] the nearest free grown-up (the planner keeps no public home per person)
                if (who == Nobody) return "no grown-up free";
                var s = VillageScenes.HomeMend(who, At(who).Value, foot, wall, exAt, ToCam(home.Pos), 0.1f + wallH * 0.85f, time);
                s.kind = home.seed;
                return StartV(s, ThingHomeBase + Mathf.Abs(home.seed) % 100, CampUse.HomeMend);
            }
            return homes.Count == 0 ? "no home in the box" : "every home has its three things";
        }

        string TryGossip(IReadOnlyList<PersonActivity> acts, float time, bool force)
        {
            if (!WellOf(VillageMiddle(), out var well, out float rimR)) return "no well in the box";
            var who = VPick(acts, well.Pos, a => IsGrown(a), 3, force);
            if (who.Count < 2) return "two grown-ups needed";
            var toCam = ToCam(well.Pos);
            if (well.era != Era.Town) toCam = WellSide(well);
            return StartV(VillageScenes.WellGossip(who, AtAll(who), well.Pos, rimR, toCam, BeatHash.Range(6f, 10f, (int)today, 4, 6951), time), ThingWell, CampUse.WellGossip);
        }

        string TryPebble(IReadOnlyList<PersonActivity> acts, float time, bool force)
        {
            if (!WellOf(VillageMiddle(), out var well, out float rimR, true)) return "no village well in the box (a town's fountain has no deep water)";
            int kid = VKid(acts, well.Pos, force);
            if (kid == Nobody) return "no kid free ('Two kids join the camp' first)";
            var side = WellSide(well);
            return StartV(VillageScenes.WellPebble(kid, At(kid).Value, well.Pos, rimR, CampScenes5.Rot(side, -35f), time), ThingWell, CampUse.WellPebble);
        }

        string TrySailWatch(IReadOnlyList<PersonActivity> acts, float time, bool force)
        {
            var mills = PiecesOf(PieceKind.Windmill, VillageMiddle());
            if (mills.Count == 0) return "no windmill in the box";
            var mill = mills[0];
            var kids = VKids(acts, mill.Pos, 2, force);
            if (kids.Count == 0) return "no kid free ('Two kids join the camp' first)";
            var front = LotDir(mill, 0f, -1f);
            var dir = (front + ToCam(mill.Pos)).normalized; if (dir.sqrMagnitude < 0.01f) dir = front;
            var across = new Vector2(-dir.y, dir.x);
            float rr = mill.era == Era.Town ? 0.52f : 0.46f;
            var seats = new List<Vector2> { mill.Pos + dir * rr - across * 0.05f, mill.Pos + dir * rr + across * 0.06f };
            return StartV(VillageScenes.SailWatch(kids, AtAll(kids), seats, mill.Pos, SailDegPerSec, BeatHash.Range(9f, 12f, (int)today, 5, 6952), time), ThingMill, CampUse.SailWatch);
        }

        string TrySailShadow(IReadOnlyList<PersonActivity> acts, float time, bool force)
        {
            var mills = PiecesOf(PieceKind.Windmill, VillageMiddle());
            if (mills.Count == 0) return "no windmill in the box";
            var mill = mills[0];
            var kids = VKids(acts, mill.Pos, 2, force);
            if (kids.Count < 2) return "two kids needed ('Two kids join the camp' first)";
            float r = mill.era == Era.Town ? VillageGeo.ShadowR + 0.06f : VillageGeo.ShadowR;
            // room all round the foot: nothing in the way (another piece, the glass)
            foreach (var p in Layout.pieces) if (p != mill && p.era != Era.Camp && (p.Pos - mill.Pos).magnitude < r + Mathf.Max(p.w, p.d) * 0.5f + 0.04f) r = Mathf.Max(0.22f, (p.Pos - mill.Pos).magnitude - Mathf.Max(p.w, p.d) * 0.5f - 0.06f);
            var tc = ToCam(mill.Pos);
            float start = Mathf.Atan2(tc.y, tc.x) * Mathf.Rad2Deg;
            return StartV(VillageScenes.SailShadow(kids, AtAll(kids), mill.Pos, r, -SailDegPerSec, start, 9f, time), ThingMill, CampUse.SailShadow);
        }

        string TryLogPile(IReadOnlyList<PersonActivity> acts, float time, bool force)
        {
            var mills = PiecesOf(PieceKind.Lumbermill, VillageMiddle());
            if (mills.Count == 0) return "no lumber mill in the box";
            var p = mills[0];
            PileSeats(p, out var a, out var b, out var outward);
            var who = VPick(acts, a, x => IsGrown(x), 2, force);
            if (who.Count < 2) return "two grown-ups needed";
            return StartV(VillageScenes.LogPileSit(who, AtAll(who), new List<Vector2> { a, b }, a + outward, BeatHash.Range(7f, 10f, (int)today, 6, 6953), time), ThingPile, CampUse.LogPileSit);
        }

        string TryLogBalance(IReadOnlyList<PersonActivity> acts, float time, bool force)
        {
            var mills = PiecesOf(PieceKind.Lumbermill, VillageMiddle());
            if (mills.Count == 0) return "no lumber mill in the box";
            var p = mills[0];
            int kid = VKid(acts, p.Pos, force);
            if (kid == Nobody) return "no kid free ('Two kids join the camp' first)";
            LogEnds(p, At(kid).Value, out var a, out var b);
            return StartV(VillageScenes.LogBalance(kid, At(kid).Value, a, b, ToCam(p.Pos), time), ThingLog, CampUse.LogBalance);
        }

        string TryCrust(IReadOnlyList<PersonActivity> acts, float time, bool force)
        {
            var bakeries = PiecesOf(PieceKind.Bakery, VillageMiddle());
            if (bakeries.Count == 0) return "no bakery in the box (a town has one)";
            var p = bakeries[0];
            BakeryDoor(p, out var door, out var outward);
            var kids = VKids(acts, door, 3, force);
            if (kids.Count < 2) return "two kids needed ('Two kids join the camp' first)";
            int baker = Nobody;
            foreach (var a in acts) if (a.kind == ActivityKind.Knead && !byKey.ContainsKey(a.who.Key) && At(a.who.Key).HasValue) { baker = a.who.Key; break; }
            if (baker == Nobody && force) baker = VGrown(acts, door, true, kids);
            var bakerAt = baker != Nobody ? At(baker).Value : door;
            return StartV(VillageScenes.CrustWait(kids, AtAll(kids), baker, bakerAt, door, outward, time), ThingBakery, CampUse.CrustWait);
        }

        string TryHopscotch(IReadOnlyList<PersonActivity> acts, float time, bool force)
        {
            if (!StreetBit(VillageMiddle(), VillageGeo.GridLen + 0.15f, out var start, out var along)) return "no clear straight street";
            var kids = VKids(acts, start, 3, force);
            if (kids.Count < 2) return "two kids needed ('Two kids join the camp' first)";
            return StartV(VillageScenes.Hopscotch(kids, AtAll(kids), start + along * 0.08f, along, ToCam(start), time), ThingStreet, CampUse.Hopscotch);
        }

        string TryStreetChat(IReadOnlyList<PersonActivity> acts, float time, bool force)
        {
            if (!force)
            {   // two walkers on a street, near each other (B50.b's rule; never on a carry)
                var walkers = new List<int>();
                foreach (var w in acts)
                    if (w.moving && IsGrown(w) && w.kind is ActivityKind.Visit or ActivityKind.SitOut or ActivityKind.Chat or ActivityKind.Walk && !byKey.ContainsKey(w.who.Key) && At(w.who.Key) is Vector2 p && StreetDist(p) < 0.08f)
                        walkers.Add(w.who.Key);
                for (int i = 0; i < walkers.Count; i++)
                    for (int j = i + 1; j < walkers.Count; j++)
                    {
                        var pa = At(walkers[i]).Value; var pb = At(walkers[j]).Value;
                        if ((pa - pb).magnitude > 0.8f) continue;
                        var meet = (pa + pb) * 0.5f;
                        return StartV(VillageScenes.StreetChat(walkers[i], walkers[j], pa, pb, meet, Dir2(pa, pb), BeatHash.Range(4f, 6f, (int)today, 7, 6954), time), ThingStreet, CampUse.StreetChat);
                    }
                return "no two walkers in a street";
            }
            if (!StreetBit(VillageMiddle(), 0.4f, out var start, out var along)) return "no straight street";
            var mid = start + along * 0.2f;
            int x = VGrown(acts, start, true); if (x == Nobody) return "no grown-up";
            int y = VGrown(acts, start + along * 0.4f, true, new[] { x }); if (y == Nobody) return "nobody to meet";
            return StartV(VillageScenes.StreetChat(x, y, At(x).Value, At(y).Value, mid, along, 5f, time), ThingStreet, CampUse.StreetChat);
        }

        string TryFenceFix(IReadOnlyList<PersonActivity> acts, float time, bool force)
        {
            foreach (var f in Fields(VillageMiddle(), false))
            {
                if (!FenceEdge(f, out var a, out var b, out var outward)) continue;
                if (ThingBusy(ThingFenceBase + FieldKey(f) % 100)) continue;
                var post = BetweenPosts(a, b);
                int who = VGrown(acts, post + outward * 0.1f, force);
                if (who == Nobody) return "no grown-up free";
                var s = VillageScenes.FenceFix(who, At(who).Value, post, outward, ToCam(post), time);
                posts[s] = (post, outward, 18f, 0f, now.day);
                return StartV(s, ThingFenceBase + FieldKey(f) % 100, CampUse.FenceFix);
            }
            return "no fenced field in the box";
        }

        string TryFenceLean(IReadOnlyList<PersonActivity> acts, float time, bool force)
        {
            foreach (var f in Fields(VillageMiddle(), false))
            {
                if (!FenceEdge(f, out var a, out var b, out var outward)) continue;
                if (ThingBusy(ThingFenceBase + FieldKey(f) % 100)) continue;
                var rail = Vector2.Lerp(a, b, 0.4f) + outward * 0.012f;
                var along = (b - a).normalized;
                var kids = VKids(acts, rail, 2, force);
                if (kids.Count == 0) return "no kid free ('Two kids join the camp' first)";
                int g = VGrown(acts, rail, force, kids);
                var s = VillageScenes.FenceLean(kids[0], At(kids[0]).Value, kids.Count > 1 ? kids[1] : Nobody, kids.Count > 1 ? At(kids[1]).Value : default,
                    g, g != Nobody ? At(g).Value : default, rail, along, -outward, BeatHash.Range(7f, 10f, (int)today, 8, 6955), time);
                return StartV(s, ThingFenceBase + FieldKey(f) % 100, CampUse.FenceLean);
            }
            return "no fenced field in the box";
        }

        // ================================================================== cheats
        string ForceVillage(CampUse use)
        {
            if (sim == null || planner == null) return "no box yet";
            if (!VillageEra) return "not a village or a town ('Jump: a village box' first)";
            var acts = pv.Activities; float time = clock;
            string r = use switch
            {
                CampUse.FieldTag => TryFieldTag(acts, time, true),
                CampUse.Scarecrow => TryScarecrow(acts, time, true),
                CampUse.HarvestDance => TryHarvestDance(acts, time, true),
                CampUse.HomeMend => TryHomeMend(acts, time, true),
                CampUse.WellGossip => TryGossip(acts, time, true),
                CampUse.WellPebble => TryPebble(acts, time, true),
                CampUse.SailWatch => TrySailWatch(acts, time, true),
                CampUse.SailShadow => TrySailShadow(acts, time, true),
                CampUse.LogPileSit => TryLogPile(acts, time, true),
                CampUse.LogBalance => TryLogBalance(acts, time, true),
                CampUse.CrustWait => TryCrust(acts, time, true),
                CampUse.Hopscotch => TryHopscotch(acts, time, true),
                CampUse.StreetChat => TryStreetChat(acts, time, true),
                CampUse.FenceFix => TryFenceFix(acts, time, true),
                CampUse.FenceLean => TryFenceLean(acts, time, true),
                _ => "not a village use: " + use,
            };
            return r ?? use + ": started (" + VillageCast() + ")";
        }

        string VillageCast()
        {
            CampScene s = null; foreach (var x in scenes) if (IsVillageUse(x.use)) s = x;
            if (s == null) return "none";
            var sb = new System.Text.StringBuilder($"{s.use} at {s.focus.x:0.00},{s.focus.y:0.00}, {s.Length:0.0} s:");
            foreach (int k in s.keys) sb.Append(' ').Append(k).Append(At(k) is Vector2 p ? $"@{p.x:0.00},{p.y:0.00}" : "@-");
            return sb.ToString();
        }

        /// <summary>The village's state for the debug panel and the tests.</summary>
        public string VillageStatus()
        {
            var L = Layout;
            if (L == null || sim == null) return "no box";
            int homes = 0, wells = 0, mills = 0, lumber = 0, bakeries = 0;
            foreach (var p in L.pieces) if (p.era != Era.Camp) switch (p.kind) { case PieceKind.Home: homes++; break; case PieceKind.Well: wells++; break; case PieceKind.Windmill: mills++; break; case PieceKind.Lumbermill: lumber++; break; case PieceKind.Bakery: bakeries++; break; }
            return $"{sim.State.era}: {homes} homes, {wells} wells, {mills} mills, {lumber} lumber mills, {bakeries} bakeries, {L.fields.Count} fields, {L.paths.Count} paths; scarecrows {scarecrows.Count}, grids {grids.Count}; playing: {VillageCast()}";
        }

        /// <summary>Debug: every person's stage, activity and whether they are drawn, free or in a scene.</summary>
        public string VillageWho()
        {
            if (planner == null) return "no planner";
            var sb = new System.Text.StringBuilder();
            foreach (var a in pv.Activities)
                sb.Append($" {a.who.Key}:{planner.StageOf(a.who)}/{a.kind}{(a.moving ? "~" : "")}{(At(a.who.Key).HasValue ? "" : " unseen")}{(byKey.ContainsKey(a.who.Key) ? " scene" : "")}{(a.tent != TentPhase.None ? " tent" : "")}{(a.hunt != HuntPhase.None ? " hunt:" + a.hunt : "")}{(meals != null && Free(a, clock) ? " free" : "")};");
            var mid = VillageMiddle();
            sb.Append($" | nearest grown (cheat) {string.Join(",", VPick(pv.Activities, mid, a => IsGrown(a), 4, true))}; kids {string.Join(",", VPick(pv.Activities, mid, a => IsKid(a), 4, true))}");
            return sb.ToString();
        }

        /// <summary>Tests: how many of a home's things show now, all homes together.</summary>
        public int HomeThingsShown { get { int n = 0; foreach (var kv in homeExtras) n += kv.Value; return n; } }
        public int ScarecrowsStanding { get { int n = 0; foreach (var kv in scarecrows) if (kv.Value.stage >= 1) n++; return n; } }
        public int GridsDrawn => grids.Count;

        // ================================================================== what the uses do to the things
        void FireVillage(CampScene s, in CampFx f)
        {
            var at = new Vector2(f.at.x, f.at.z);
            switch ((VillageFx)f.count)
            {
                case VillageFx.Cross: case VillageFx.Straw: case VillageFx.Hat:
                    if (scarecrows.TryGetValue(s.kind, out var sc)) scarecrows[s.kind] = (sc.at, sc.year, (VillageFx)f.count == VillageFx.Cross ? 1 : (VillageFx)f.count == VillageFx.Straw ? 2 : 3);
                    if ((VillageFx)f.count == VillageFx.Hat) HitFx.Puff(PuffKind.Grass, Root3(at, 0.2f), Vector3.up, 0.3f, PuffOptions.Default);
                    break;
                case VillageFx.Extra:
                    homeExtras[s.kind] = Mathf.Min(MaxExtras, (homeExtras.TryGetValue(s.kind, out int n) ? n : 0) + 1);
                    HitFx.Puff(PuffKind.Sparks, Root3(at, 0.2f), Vector3.up, 0.25f, PuffOptions.Default);
                    break;
                case VillageFx.PebbleRing:
                {
                    var o = PuffOptions.Default; o.count = 1;
                    HitFx.Puff(PuffKind.Ring, Root3(at, f.at.y), Vector3.up, 0.35f, o);
                    break;
                }
                case VillageFx.Grid:
                    grids.Add((s.p0, f.scale, now.day)); if (grids.Count > 2) grids.RemoveAt(0);
                    break;
                case VillageFx.PostStraight:
                    if (posts.TryGetValue(s, out var ps)) posts[s] = (ps.at, ps.outward, 0f, ps.sunk, ps.day);
                    break;
                case VillageFx.PostTap:
                    if (posts.TryGetValue(s, out var pt)) posts[s] = (pt.at, pt.outward, pt.lean, pt.sunk + 0.008f, pt.day);
                    break;
            }
        }

        Transform lastVillageRoot;
        Vector3 Root3(Vector2 at, float h) => lastVillageRoot != null && LandBox != null ? lastVillageRoot.TransformPoint(new Vector3(at.x, LandBox.SurfaceHeight(at.x, at.y) + h, at.y)) : new Vector3(at.x, h, at.y);

        // ================================================================== drawn
        Transform VPiece(Transform root, Material material, int i, Mesh mesh)
        {
            while (i < villagePool.Count && villagePool[i] == null) villagePool.RemoveAt(i);
            while (villagePool.Count <= i)
            {
                var go = new GameObject("Village use") { hideFlags = HideFlags.DontSave };
                go.transform.SetParent(root, false);
                go.AddComponent<MeshFilter>();
                var r = go.AddComponent<MeshRenderer>(); r.sharedMaterial = material; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                villagePool.Add(go.transform);
            }
            var t = villagePool[i];
            if (t.parent != root) t.SetParent(root, false);
            t.GetComponent<MeshFilter>().sharedMesh = mesh;
            t.gameObject.SetActive(true);
            return t;
        }

        /// <summary>The pieces (box units, foot at the origin): 0 the scarecrow's cross, 1 its straw, 2 its hat, 3 a crust, 4 a cup, 5 a pebble,
        /// 6 a bucket of water, 7 the last sheaf, 8 the hopscotch grid (along +z), 9 a fence post, 10 a flower box, 11 a painted door (by the
        /// home's seed: 12+), 12 a string of herbs, 13 a songbird.</summary>
        Mesh VMesh(int which, int tint = 0)
        {
            int key = which * 16 + tint;
            if (villageMeshes.TryGetValue(key, out var m) && m != null) return m;
            var mb = new MeshBuilder(6950 + key);
            var wood = Palette.Timber; var light = Palette.WoodLight;
            switch (which)
            {
                case 0: // two poles: the post (0.26 u) and the cross bar
                    mb.Box(new Vector3(0f, 0.13f, 0f), new Vector3(0.016f, 0.26f, 0.016f), wood);
                    mb.Box(new Vector3(0f, 0.17f, 0f), new Vector3(0.17f, 0.014f, 0.014f), wood);
                    break;
                case 1: // the straw body, a sack head, straw at the arms' ends
                    mb.Ico(new Vector3(0f, 0.135f, 0f), 0.036f, new Vector3(1f, 1.35f, 0.75f), Palette.Straw, 0.12f);
                    mb.Ico(new Vector3(0f, 0.21f, 0f), 0.026f, Vector3.one, Palette.Canvas, 0.05f);
                    foreach (float x in new[] { -0.085f, 0.085f }) mb.Ico(new Vector3(x, 0.165f, 0f), 0.014f, new Vector3(1.3f, 1f, 1f), Palette.Straw, 0.15f);
                    mb.Box(new Vector3(0f, 0.165f, 0f), new Vector3(0.12f, 0.026f, 0.03f), Palette.Accents[2]); // the old shirt's arms
                    break;
                case 2: // an old hat: a brim and a crown
                    mb.Prism(new Vector3(0f, 0f, 0f), 0.04f, 0.04f, 0.006f, 10, Palette.Door);
                    mb.Prism(new Vector3(0f, 0.006f, 0f), 0.022f, 0.019f, 0.026f, 10, Palette.Door);
                    break;
                case 3: mb.Ico(Vector3.zero, 0.014f, new Vector3(1.3f, 0.8f, 1f), new Color(0.72f, 0.48f, 0.24f), 0.15f); break; // a crust
                case 4: mb.Prism(Vector3.zero, 0.011f, 0.012f, 0.022f, 8, Palette.Terracotta, topCol: Palette.Door); break; // a cup
                case 5: mb.Ico(Vector3.zero, 0.009f, new Vector3(1.2f, 0.8f, 1f), Palette.RockLight, 0.2f); break; // a pebble
                case 6: mb.Prism(Vector3.zero, 0.022f, 0.026f, 0.04f, 8, light, topCol: Palette.Water); break; // a full bucket
                case 7: // the last sheaf, standing, tied in the middle
                    mb.sway = 0f;
                    mb.Prism(Vector3.zero, 0.03f, 0.018f, 0.06f, 7, Palette.Wheat);
                    mb.Prism(new Vector3(0f, 0.06f, 0f), 0.018f, 0.04f, 0.06f, 7, Palette.Wheat);
                    mb.Box(new Vector3(0f, 0.058f, 0f), new Vector3(0.042f, 0.008f, 0.042f), Palette.Straw);
                    break;
                case 8: // the hopscotch grid: six squares in a row, pale scratched lines (along +z, from z 0)
                {
                    var c = new Color(0.93f, 0.9f, 0.8f); float sq = VillageGeo.Square, w = 0.004f, hh = 0.0015f;
                    for (int k = 0; k <= 6; k++) mb.Box(new Vector3(0f, hh, k * sq), new Vector3(sq, 0.003f, w), c);
                    foreach (float x in new[] { -sq / 2f, sq / 2f }) mb.Box(new Vector3(x, hh, VillageGeo.GridLen / 2f), new Vector3(w, 0.003f, VillageGeo.GridLen), c);
                    break;
                }
                case 9: mb.Box(new Vector3(0f, 0.075f, 0f), new Vector3(0.025f, 0.15f, 0.025f), new Color(0.62f, 0.5f, 0.36f)); break; // a new post (paler wood)
                case 10: // a flower box: a wooden trough, flowers on top
                    mb.Box(Vector3.zero, new Vector3(0.1f, 0.028f, 0.032f), light);
                    for (int k = 0; k < 5; k++) mb.Ico(new Vector3(-0.04f + k * 0.02f, 0.02f, 0f), 0.011f, Vector3.one, k % 2 == 0 ? Palette.Accents[4] : Palette.Accents[1], 0.1f);
                    break;
                case 11: mb.Box(Vector3.zero, new Vector3(0.106f, 0.186f, 0.004f), Palette.Accents[tint % Palette.Accents.Length]); break; // a painted door
                case 12: // a string of herbs: a cord and three green bundles hanging
                    mb.Box(Vector3.zero, new Vector3(0.09f, 0.004f, 0.004f), Palette.Straw);
                    for (int k = 0; k < 3; k++) mb.Prism(new Vector3(-0.03f + k * 0.03f, -0.045f, 0f), 0.006f, 0.014f, 0.04f, 6, Color.Lerp(Palette.Sprout, new Color(0.35f, 0.5f, 0.28f), k * 0.3f));
                    break;
                case 13: // a songbird (as B17.b's): body, head, tail
                    mb.Ico(Vector3.zero, 0.012f, new Vector3(1.4f, 1f, 1f), new Color(0.55f, 0.36f, 0.25f), 0.1f);
                    mb.Ico(new Vector3(0.012f, 0.008f, 0f), 0.008f, Vector3.one, new Color(0.85f, 0.45f, 0.3f), 0.05f);
                    mb.Box(new Vector3(-0.016f, 0.002f, 0f), new Vector3(0.012f, 0.003f, 0.008f), new Color(0.4f, 0.28f, 0.2f));
                    break;
            }
            m = mb.ToMesh("Village_Use_" + key); m.hideFlags = HideFlags.DontSave;
            return villageMeshes[key] = m;
        }

        void DrawVillage(Transform root, Material material, float time, System.Func<float, float, float> groundAt)
        {
            lastVillageRoot = root;
            int used = 0;
            Vector3 G3(Vector2 p, float h = 0f) => new Vector3(p.x, groundAt(p.x, p.y) + h, p.y);
            PegDollView V(CampScene s, int m) { var v = m >= 0 && m < s.Count ? pv.ViewOf(s.keys[m]) : null; return v != null && v.gameObject.activeInHierarchy ? v : null; }
            Vector3 Hand(PegDollView v) => root.InverseTransformPoint(v.armR.TransformPoint(new Vector3(0f, -PegDoll.ArmLen * v.scale, 0f)));
            if (VillageEra)
            {
                // the scarecrows (the growing season), the homes' things, the grids, the posts set straight
                long year = now.day / 12;
                foreach (var kv in scarecrows)
                {
                    var sc = kv.Value;
                    if (sc.stage < 1 || sc.year != year) continue;
                    var basePos = G3(sc.at);
                    float yaw = Mathf.Repeat(kv.Key * 37f, 360f);
                    for (int part = 0; part < sc.stage; part++)
                    {
                        var t = VPiece(root, material, used++, VMesh(part));
                        t.localPosition = part == 2 ? basePos + Vector3.up * 0.232f : basePos; t.localRotation = Quaternion.Euler(0f, yaw, part == 2 ? 8f : 0f); t.localScale = Vector3.one;
                    }
                }
                var L = Layout;
                if (L != null)
                    foreach (var home in L.pieces)
                    {
                        if (home.kind != PieceKind.Home || home.era == Era.Camp || home.grow < 0.95f) continue;
                        int n = ExtrasOf(home);
                        for (int k = 0; k < n; k++)
                        {
                            int kind = ExtraKind(home, k);
                            var lp = ExtraLocal(home, kind);
                            var at = LotPoint(home, lp.x, lp.z);
                            var t = VPiece(root, material, used++, VMesh(kind == 0 ? 10 : kind == 1 ? 11 : 12, kind == 1 ? Mathf.Abs(home.seed) % 6 : 0));
                            t.localPosition = new Vector3(at.x, groundAt(home.x, home.z) + lp.y, at.y); t.localRotation = Quaternion.Euler(0f, home.yaw, 0f); t.localScale = Vector3.one;
                        }
                    }
                foreach (var g in grids)
                {
                    var t = VPiece(root, material, used++, VMesh(8));
                    t.localPosition = G3(g.at, 0.002f); t.localRotation = Quaternion.Euler(0f, g.yaw, 0f); t.localScale = Vector3.one;
                }
                // the posts: leaning before the push, then straight and a little sunk; they stay the view day
                foreach (var kv in posts) if (!scenes.Contains(kv.Key)) { postsLeft.Add((kv.Value.at, kv.Value.outward, kv.Value.sunk, kv.Value.day)); }
                var gone = new List<CampScene>(); foreach (var kv in posts) if (!scenes.Contains(kv.Key)) gone.Add(kv.Key); foreach (var k in gone) posts.Remove(k);
                foreach (var kv in posts)
                {
                    float u = time - kv.Key.t0;
                    float lean = u < kv.Key.a - 0.4f ? kv.Value.lean : Mathf.Lerp(18f, 0f, CampPose.Smooth((u - (kv.Key.a - 0.4f)) / 0.4f));
                    DrawPost(root, material, ref used, kv.Value.at, kv.Value.outward, lean, kv.Value.sunk, groundAt);
                }
                foreach (var p in postsLeft) DrawPost(root, material, ref used, p.at, p.outward, 0f, p.sunk, groundAt);
            }
            birdPerched = -1;
            foreach (var s in scenes)
            {
                if (!IsVillageUse(s.use)) continue;
                float u = time - s.t0;
                switch (s.use)
                {
                    case CampUse.Scarecrow:
                        if (u >= s.c && scarecrows.TryGetValue(s.kind, out var sc) && sc.stage >= 2)
                        {   // a songbird drops onto the cross's arm and stays
                            float k = CampPose.Smooth(Mathf.Clamp01((u - s.c) / 0.6f));
                            var yaw = Quaternion.Euler(0f, Mathf.Repeat(s.kind * 37f, 360f), 0f);
                            var perch = G3(sc.at) + yaw * new Vector3(0.07f, 0.185f, 0f);
                            var t = VPiece(root, material, used++, VMesh(13));
                            t.localPosition = perch + new Vector3(0.1f, 0.15f, 0.05f) * (1f - k); t.localRotation = yaw * Quaternion.Euler(0f, 180f, 0f); t.localScale = Vector3.one * 1.3f;
                            birdPerched = s.kind;
                        }
                        break;
                    case CampUse.HarvestDance:
                        if (u >= 0.2f) { var t = VPiece(root, material, used++, VMesh(7)); t.localPosition = G3(s.focus); t.localRotation = Quaternion.identity; t.localScale = Vector3.one * 1.3f; }
                        break;
                    case CampUse.HomeMend:
                        if (u >= s.b + 0.4f && u < s.c)
                        {
                            var ladder = PieceMesh(2);
                            var t = VPiece(root, material, used++, ladder);
                            var toWall = Dir2(s.p0, s.p1);
                            t.localPosition = G3(s.p0); t.localRotation = Quaternion.LookRotation(new Vector3(toWall.x, 0f, toWall.y)) * Quaternion.Euler(14f, 0f, 0f);
                            t.localScale = Vector3.one * CampScenes6.LadderScale;
                        }
                        break;
                    case CampUse.WellGossip:
                        for (int m = 0; m < s.Count; m++)
                        {
                            if (u < s.a || u >= s.b) continue;
                            var part = s.parts[m].Count > 0 ? s.parts[m][0] : null;
                            if (part == null) continue;
                            var foot = part.pos + Dir2(part.pos, s.focus) * 0.035f + new Vector2(-(s.focus - part.pos).y, (s.focus - part.pos).x).normalized * 0.03f;
                            var t = VPiece(root, material, used++, VMesh(6)); t.localPosition = G3(foot); t.localRotation = Quaternion.identity; t.localScale = Vector3.one * 1.4f;
                        }
                        break;
                    case CampUse.WellPebble:
                    {
                        var v = V(s, 0);
                        if (u < s.a && v != null) { var t = VPiece(root, material, used++, VMesh(5)); t.localPosition = Hand(v); t.localRotation = Quaternion.identity; t.localScale = Vector3.one * 1.4f; }
                        else if (u >= s.a && u < s.a + 0.45f && v != null)
                        {   // it falls from the hand into the well's middle and down out of sight
                            float k = (u - s.a) / 0.45f;
                            var from = Hand(v); var to = G3(s.focus, VillageGeo.WellTop - 0.06f);
                            var t = VPiece(root, material, used++, VMesh(5)); t.localPosition = Vector3.Lerp(new Vector3(from.x, from.y, from.z), new Vector3(to.x, from.y, to.z), Mathf.Min(1f, k * 1.6f)) + Vector3.down * (from.y - to.y) * k * k; t.localRotation = Quaternion.identity; t.localScale = Vector3.one * 1.4f;
                        }
                        break;
                    }
                    case CampUse.LogPileSit:
                        if (u >= s.a && u < s.b)
                            for (int m = 0; m < s.Count; m++)
                            {
                                var v = V(s, m); if (v == null) continue;
                                var t = VPiece(root, material, used++, VMesh(4)); t.localPosition = Hand(v) + Vector3.down * 0.01f; t.localRotation = Quaternion.identity; t.localScale = Vector3.one * 1.3f;
                            }
                        break;
                    case CampUse.CrustWait:
                    {
                        // the crust: in the baker's hand, then kid 0's; after the share each kid holds a piece; they shrink as they are eaten
                        int bakerM = Mathf.RoundToInt(s.d);
                        var give = 0f; var shares = new List<(float t, int m)>();
                        foreach (var f in s.fx) if (f.kind == CampFxKind.Village) { if ((VillageFx)f.count == VillageFx.CrustGive) { give = f.time; } else if ((VillageFx)f.count == VillageFx.CrustShare) shares.Add((f.time, f.member)); }
                        float eat = s.b - 2.6f;
                        if (u < give)
                        {
                            var bv = bakerM >= 0 ? V(s, bakerM) : null;
                            if (bv != null && u > give - 2f) { var t = VPiece(root, material, used++, VMesh(3)); t.localPosition = Hand(bv); t.localRotation = Quaternion.identity; t.localScale = Vector3.one * 1.6f; }
                        }
                        else if (u < s.b)
                        {
                            float size = u < eat ? 1.6f : Mathf.Lerp(1.6f, 0.5f, (u - eat) / 2.6f);
                            var v0 = V(s, 0);
                            if (v0 != null) { var t = VPiece(root, material, used++, VMesh(3)); t.localPosition = Hand(v0); t.localRotation = Quaternion.identity; t.localScale = Vector3.one * size * (shares.Count > 0 && u >= shares[0].t ? 0.8f : 1f); }
                            foreach (var sh in shares)
                            {
                                if (u < sh.t) continue;
                                var vk = V(s, sh.m); if (vk == null) continue;
                                var t = VPiece(root, material, used++, VMesh(3)); t.localPosition = Hand(vk); t.localRotation = Quaternion.identity; t.localScale = Vector3.one * size * 0.7f;
                            }
                        }
                        break;
                    }
                }
            }
            for (int i = used; i < villagePool.Count; i++) if (villagePool[i] != null) villagePool[i].gameObject.SetActive(false);
        }

        void DrawPost(Transform root, Material material, ref int used, Vector2 at, Vector2 outward, float lean, float sunk, System.Func<float, float, float> groundAt)
        {
            var t = VPiece(root, material, used++, VMesh(9));
            t.localPosition = new Vector3(at.x, groundAt(at.x, at.y) - sunk, at.y);
            var axis = new Vector3(-outward.y, 0f, outward.x); // tips toward the outside
            t.localRotation = Quaternion.AngleAxis(-lean, axis);
            t.localScale = Vector3.one;
        }
    }

    public static partial class CampCheats
    {
        /// <summary>"Jump: a village box" / "Jump: a town box": a lived-in camp grown into a village (its homes, the well, the windmill, the lumber mill,
        /// fenced fields; the people fed and the wood given so the sim builds them over a few years), and on to a town (the workshop, the stalls,
        /// a bakery), the era festival over, <paramref name="kids"/> kids added, at <paramref name="hour"/>.</summary>
        public static string StageVillage(float hour, bool town = false, int kids = 4, bool onlyCheats = true)
        {
            var sim = AquariumCiv.Game.GameClock.Instance != null ? AquariumCiv.Game.GameClock.Instance.Sim : null;
            if (sim == null) return "no sim";
            string r = sim.State.era == Era.Camp ? StageCamp(hour, 0, onlyCheats) + " | " : "";
            CampLife.OnlyCheats = onlyCheats;
            void Grow(int steps)
            {
                for (int i = 0; i < steps; i++)
                {
                    sim.State.population = System.Math.Max(sim.State.population, 26);
                    sim.State.wood = 2000f; sim.State.food = System.Math.Max(sim.State.food, 200f);
                    SimLayoutSource.DebugSkipYears(3f);
                }
            }
            if (sim.State.era == Era.Camp) { sim.ForceNextEra(); Grow(5); }
            if (town && sim.State.era == Era.Village) { sim.ForceNextEra(); Grow(4); }
            if (town && !sim.State.buildings.Exists(b => b.type == BuildingType.Bakery)) SimLayoutSource.DebugBuild(BuildingType.Bakery);
            sim.State.festivalUntilMonth = sim.State.month; // an ordinary year (tooling.md 2026-11-11: the era's festival evenings override the rest)
            if (kids > 0) sim.DebugAddKids(kids);
            var box = UnityEngine.Object.FindAnyObjectByType<BoxView>();
            if (box != null) box.Rebuild();
            ShowMeDirector.BubblesOn = false;
            PeopleView.DebugSetHour(hour);
            var c = Camp;
            return r + $"{sim.State.era} at {hour:0.0} h, kids +{kids}; " + (c != null ? c.VillageStatus() : "");
        }

        public static string FieldTag() => Do(CampUse.FieldTag);
        public static string Scarecrow() => Do(CampUse.Scarecrow);
        public static string HarvestDance() => Do(CampUse.HarvestDance);
        public static string HomeMend() => Do(CampUse.HomeMend);
        public static string WellGossip() => Do(CampUse.WellGossip);
        public static string WellPebble() => Do(CampUse.WellPebble);
        public static string SailWatch() => Do(CampUse.SailWatch);
        public static string SailShadow() => Do(CampUse.SailShadow);
        public static string LogPileSit() => Do(CampUse.LogPileSit);
        public static string LogBalance() => Do(CampUse.LogBalance);
        public static string CrustWait() => Do(CampUse.CrustWait);
        public static string Hopscotch() => Do(CampUse.Hopscotch);
        public static string StreetChat() => Do(CampUse.StreetChat);
        public static string FenceFix() => Do(CampUse.FenceFix);
        public static string FenceLean() => Do(CampUse.FenceLean);
        public static string VillageStatus() { var c = Camp; return c == null ? "no PeopleView" : c.VillageStatus(); }
        public static string VillageWho() { var c = Camp; return c == null ? "no PeopleView" : c.VillageWho(); }
    }
}
