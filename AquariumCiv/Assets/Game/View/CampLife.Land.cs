// #692 part 3 (docs/design/animation-specs.md "#692 part 3: grass, saplings, rocks, fallen branches", B33-B36): CampLife's side of the land's
// uses. Which use starts (the D rules: life uses borrow free time only; how often by the view day's roll; one use per thing at a time), the
// cheats, and what the uses leave on the land, drawn here: the two boulders at the camp's edge (new, [est]), the wet patch under a watered
// sapling, the rings of stones, the planted twigs, the pebble stacks (and their topple), the stick horse leaning, grass bits in a kid's hair
// and a flower behind an ear, the tall grass flattened where kids rolled. These are view-only and not saved [est]: a reload clears them.
// The timelines are the pure builders in View/Beats/CampScenes3.cs.
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
        const int ThingGrass = 5, ThingSapling = 6, ThingRock = 7, ThingDeadfall = 8;

        // ---- the marks (view-only)
        sealed class Stack { public Vector2 p; public float h; public int n; public float wobbleAt = -99f, toppleAt = -1f; public long day; }
        readonly List<Vector2> rocks = new();
        Vector2 rocksFire = new(999f, 999f);
        readonly List<(Vector2 sapling, List<Vector2> stones)> rings = new();
        readonly List<(Vector2 p, long season)> twigs = new();
        readonly List<Stack> stacks = new();
        readonly List<(Vector2 p, float r, float at)> wets = new();
        readonly List<(Vector2 p, float until)> horses = new();
        readonly List<(int key, CampMark kind, float until)> worn = new();
        readonly List<(Vector2 p, float until)> flats = new();
        readonly List<(Vector2 p, float at)> laidFlowers = new();
        readonly List<Transform> landPool = new();
        readonly Dictionary<int, Mesh> landMeshes = new();

        void ResetLand()
        {
            rocks.Clear(); rocksFire = new Vector2(999f, 999f);
            rings.Clear(); twigs.Clear(); stacks.Clear(); wets.Clear(); horses.Clear(); worn.Clear(); flats.Clear(); laidFlowers.Clear();
        }

        /// <summary>The view clock in hours (a view day is 24).</summary>
        float ViewHours => now.day * 24f + now.hour;

        static bool IsLandUse(CampUse u) => u >= CampUse.GrassRoll && u <= CampUse.StickHorse;

        // ================================================================== what the land offers
        BoxView LandBox => pv != null ? pv.BoxForPlay : null;

        /// <summary>B35 [est]: the two boulders at the camp's edge (worked out again when the fire moves).</summary>
        public IReadOnlyList<Vector2> Rocks { get { EnsureRocks(); return rocks; } }

        void EnsureRocks()
        {
            if (meals == null || !meals.HasFire || sim == null) { rocks.Clear(); return; }
            var fire = meals.Fire;
            if ((fire - rocksFire).sqrMagnitude < 1e-6f && rocks.Count > 0) return;
            rocksFire = fire;
            rocks.Clear();
            var box = LandBox;
            rocks.AddRange(LandGeo.Boulders(fire, TowardCamera(fire) - fire, sim.State.seed, p =>
            {
                if (!Terrain.InBox(p.x, p.y, Terrain.GlassMargin + 0.1f)) return false;
                int c = SiteMap.CellAt(p.x, p.y);
                var cv = sim.EcoCoverAt(c);
                if (sim.EcologyOn && cv is EcoCover.Water or EcoCover.Reeds or EcoCover.Tree or EcoCover.Young or EcoCover.Bush) return false;
                if (sim.EcologyOn && sim.State.eco.treeCell >= 0 && (new Vector2(SiteMap.CXc(sim.State.eco.treeCell), SiteMap.CZc(sim.State.eco.treeCell)) - p).magnitude < 0.45f) return false; // clear of the Keeper's stone ring
                return box == null || box.FreeForProp(p, LandGeo.RockW * 0.6f);
            }));
        }

        /// <summary>A grass (or tall grass) spot <paramref name="min"/>..<paramref name="max"/> from <paramref name="near"/>, with room (nearest first).</summary>
        bool GrassSpot(Vector2 near, float min, float max, bool tallOnly, out Vector2 spot)
        {
            spot = default;
            if (sim == null || !sim.EcologyOn) return false;
            var box = LandBox;
            float bd = float.MaxValue;
            for (int c = 0; c < EcoLand.N; c++)
            {
                var cv = sim.EcoCoverAt(c);
                if (!(cv == EcoCover.Tall || !tallOnly && cv == EcoCover.Grass)) continue;
                var p = new Vector2(SiteMap.CXc(c), SiteMap.CZc(c));
                float d = (p - near).magnitude;
                if (d < min || d > max || !Terrain.InBox(p.x, p.y, Terrain.GlassMargin + 0.15f)) continue;
                if (box != null && !box.FreeForProp(p, 0.1f)) continue;
                bool onRock = false; foreach (var r in rocks) if ((r - p).magnitude < 0.15f) onRock = true;
                if (onRock) continue;
                if (d < bd) { bd = d; spot = p; }
            }
            return bd < float.MaxValue;
        }

        /// <summary>#692 p3 (B33.a, the user's review: the roll showed bare land): the grass cell whose neighbours (r 0.25 u) hold the most grass and
        /// tall grass (ties: the nearest), with the same room tests as <see cref="GrassSpot"/>.</summary>
        bool GrassiestSpot(Vector2 near, float min, float max, out Vector2 spot)
        {
            spot = default;
            if (sim == null || !sim.EcologyOn) return false;
            var box = LandBox;
            float best = -1f, bd = float.MaxValue;
            for (int c = 0; c < EcoLand.N; c++)
            {
                var cv = sim.EcoCoverAt(c);
                if (cv != EcoCover.Tall && cv != EcoCover.Grass) continue;
                var p = new Vector2(SiteMap.CXc(c), SiteMap.CZc(c));
                float d = (p - near).magnitude;
                if (d < min || d > max || !Terrain.InBox(p.x, p.y, Terrain.GlassMargin + 0.15f)) continue;
                if (box != null && !box.FreeForProp(p, 0.1f)) continue;
                bool onRock = false; foreach (var r in rocks) if ((r - p).magnitude < 0.15f) onRock = true;
                if (onRock) continue;
                float score = 0f;
                for (int dj = -2; dj <= 2; dj++)
                    for (int di = -2; di <= 2; di++)
                    {
                        var q = p + new Vector2(di, dj) * (SiteMap.Cell);
                        if (!Terrain.InBox(q.x, q.y, 0f)) continue;
                        var qc = sim.EcoCoverAt(SiteMap.CellAt(q.x, q.y));
                        score += qc == EcoCover.Tall ? 1.2f : qc == EcoCover.Grass ? 1f : 0f;
                    }
                if (score > best + 1e-3f || Mathf.Abs(score - best) < 1e-3f && d < bd) { best = score; bd = d; spot = p; }
            }
            return best >= 0f;
        }

        /// <summary>B34: the drawn wild sapling or seedling nearest the camp within <paramref name="reach"/> (the view's own spots). #692 p3: only one
        /// clear of the glass (<see cref="ClearOfGlass"/>), so the camera on the use never looks through a pane.</summary>
        bool SaplingNear(Vector2 camp, float reach, out Vector2 at, System.Func<Vector2, bool> ok = null) => SaplingNear(camp, reach, out at, out _, ok);

        bool SaplingNear(Vector2 camp, float reach, out Vector2 at, out float crown, System.Func<Vector2, bool> ok = null)
        {
            at = default; crown = 0f; float bd = reach;
            var box = LandBox;
            if (box == null) return false;
            lock (box.SaplingSpots)
                foreach (var kv in box.SaplingSpots)
                {
                    var p = new Vector2(kv.Value.pos.x, kv.Value.pos.z);
                    float d = (p - camp).magnitude;
                    if (d >= bd || !InView(box, p) || ok != null && !ok(p)) continue;
                    bd = d; at = p; crown = kv.Value.r;
                }
            return bd < reach;
        }

        /// <summary>#692 p3 (the user's review: the camera looked through the front pane at B34): a sapling the camp uses may tend stands at least
        /// <see cref="GlassClearFront"/> in from the front glass and <see cref="GlassClearSide"/> from the others.</summary>
        public static bool ClearOfGlass(Vector2 p) => Terrain.InBox(p.x, p.y, GlassClearSide) && p.y >= Terrain.ZMin + GlassClearFront;
        public const float GlassClearFront = 2.4f, GlassClearSide = 0.6f;

        /// <summary>#692 p3 (the user's review: the water clip showed only the Keeper's crown): a sapling a camp use tends is clear of the glass
        /// and out from under every grown crown the box draws (its play radius + <see cref="CrownClear"/>), so the camera sees the little tree.</summary>
        static bool InView(BoxView box, Vector2 p)
        {
            if (!ClearOfGlass(p)) return false;
            if (box == null) return true;
            lock (box.TreeSpots)
                foreach (var kv in box.TreeSpots)
                    if ((new Vector2(kv.Value.pos.x, kv.Value.pos.z) - p).magnitude < kv.Value.r + CrownClear) return false;
            return true;
        }
        public const float CrownClear = 0.18f;

        bool Ringed(Vector2 sapling) { foreach (var r in rings) if ((r.sapling - sapling).magnitude < 0.03f) return true; return false; }

        /// <summary>B36: where the fallen branches lie under the deadfall tree nearest the camp (the trunk's side toward the camp).</summary>
        bool DeadfallAt(Vector2 camp, out Vector2 at)
        {
            at = default;
            if (sim == null || !sim.EcologyOn) return false;
            int c = sim.DeadfallTreeCell(camp.x, camp.y);
            if (c < 0) return false;
            var box = LandBox;
            Vector2 trunk = new(SiteMap.CXc(c), SiteMap.CZc(c)); float r = 0.16f;
            if (box != null) lock (box.TreeSpots) if (box.TreeSpots.TryGetValue(c, out var ts)) { trunk = new Vector2(ts.pos.x, ts.pos.z); r = ts.r; }
            at = trunk + Dir2(trunk, camp) * (r * 1.5f + 0.05f);
            return true;
        }

        static Vector2 Dir2(Vector2 from, Vector2 to) { var d = to - from; return d.sqrMagnitude > 1e-6f ? d.normalized : Vector2.down; }

        List<Vector2> FlowersNear(Vector2 from, float reach, int max)
        {
            var got = new List<(float d, Vector2 p)>();
            var box = LandBox;
            if (box != null)
                lock (box.FlowerSpots)
                    foreach (var v in box.FlowerSpots.Values)
                    {
                        var p = new Vector2(v.x, v.z);
                        float d = (p - from).magnitude;
                        if (d <= reach) got.Add((d, p));
                    }
            got.Sort((a, b) => a.d.CompareTo(b.d));
            var r = new List<Vector2>();
            foreach (var g in got) { bool near = false; foreach (var q in r) if ((q - g.p).magnitude < 0.06f) near = true; if (!near) r.Add(g.p); if (r.Count >= max) break; }
            return r;
        }

        int ParentOf(int key) { var n = sim.FindNotable(key); return n != null ? n.parentId : -1; }
        int PartnerOf(int key) { var n = sim.FindNotable(key); return n != null ? n.partnerId : -1; }

        // ================================================================== the triggers (each frame, in the camp era, with a fire)
        void StepLand(IReadOnlyList<PersonActivity> acts, float time)
        {
            // the flattened tall grass (B33.a) goes to the shader with the people's hide spots
            for (int i = flats.Count - 1; i >= 0; i--) if (time > flats[i].until) flats.RemoveAt(i);
            if (lastRoot != null) foreach (var f in flats)
                {
                    var box = LandBox;
                    float y = box != null ? box.SurfaceHeight(f.p.x, f.p.y) : 0f;
                    GrassHide.Add(lastRoot.TransformPoint(new Vector3(f.p.x, y, f.p.y)), Mathf.Clamp01((f.until - time) / 0.6f), 0.12f);
                }
            if (OnlyCheats || meals == null || !meals.HasFire || sim == null || !sim.EcologyOn) return;
            EnsureRocks();
            float h = Hour;
            if (meals.Eaters > 0) return;
            var season = LifeClock.SeasonOf(now.day);
            bool warm = season is Season.Spring or Season.Summer, day = h >= 9f && h < 16.5f;
            bool Go(CampUse u, float chance, int thing) => !doneToday.Contains(u) && Roll(u) < chance && LifeOk(u) && !ThingBusy(thing);
            // B34.a: water a sapling (work: the morning, a dry day)
            if (h >= 7f && h < 10f && planner.WeatherOn(now.day) is not (Weather.KeeperRain or Weather.Storm or Weather.Snow) && Go(CampUse.WaterSapling, Sometimes, ThingSapling)
                && TryWater(acts, time, false) == null) return;
            // B34.b: ring a sapling with stones (once per sapling, at most four rings)
            if (day && rings.Count < LandGeo.MaxRings && !doneToday.Contains(CampUse.RingSapling) && !ThingBusy(ThingSapling) && LifeOk(CampUse.RingSapling)
                && TryRing(acts, time, false) == null) return;
            if (!day) return;
            // B35.a: sit on a rock (often: it is a seat)
            if (Go(CampUse.SitRock, Often, ThingRock) && TryRock(acts, time, false) == null) return;
            // B35.b: stack pebbles (sometimes)
            if (Go(CampUse.StackPebbles, Sometimes, ThingRock) && TryStack(acts, time, false) == null) return;
            if (warm)
            {
                // B33.a, B33.c: roll in the grass, pick flowers (spring and summer, sometimes)
                if (Go(CampUse.GrassRoll, Sometimes, ThingGrass) && TryRoll(acts, time, false) == null) return;
                if (Go(CampUse.PickFlowers, Sometimes, 0) && TryFlowers(acts, time, false) == null) return;
            }
            // B33.b: lie back in the grass (summer middays)
            if (season == Season.Summer && h >= 11f && h < 14.5f && Go(CampUse.LieBack, Sometimes, ThingGrass) && TryLieBack(acts, time, false) == null) return;
            // B33.d: a grass whistle (not in winter)
            if (season != Season.Winter && Go(CampUse.GrassWhistle, Sometimes, 0) && TryWhistle(acts, time, false) == null) return;
            // B36: the deadfall's sticks: a sword fight, a stick horse
            if (Go(CampUse.StickFight, Sometimes, ThingDeadfall) && TryFight(acts, time, false) == null) return;
            if (Go(CampUse.StickHorse, Sometimes, ThingDeadfall) && TryHorse(acts, time, false) == null) return;
        }

        // ================================================================== starting each use (natural and forced); null = started, else why not
        List<int> Pick(IReadOnlyList<PersonActivity> acts, Vector2 near, System.Func<PersonActivity, bool> ok, int max, bool force, ICollection<int> not = null)
        {
            if (!force)
            {
                var r = FreeNear(acts, near, BorrowReach, ok, max + (not?.Count ?? 0));
                if (not != null) r.RemoveAll(not.Contains);
                if (r.Count > max) r.RemoveRange(max, r.Count - max);
                return r;
            }
            var got = new List<int>();
            var skip = new List<int>(); if (not != null) skip.AddRange(not);
            while (got.Count < max) { int k = Nearest(acts, near, 9f, ok, skip, false); if (k < 0) break; got.Add(k); skip.Add(k); }
            return got;
        }

        string TryRoll(IReadOnlyList<PersonActivity> acts, float time, bool force)
        {
            var camp = meals.Fire;
            if (!GrassiestSpot(camp, 0.45f, force ? 2.5f : 1.6f, out var spot)) return "no grass near the camp"; // #692 p3: the grassiest spot, not the nearest
            var kids = Pick(acts, spot, a => IsKid(a), 3, force);
            if (kids.Count == 0) return "no kid free ('Two kids join the camp' first)";
            var at = new List<Vector2>(); foreach (int k in kids) at.Add(At(k).Value);
            var across = CameraAcross();
            Add(Thing(CampScenes3.GrassRoll(kids, at, spot, across, time), ThingGrass));
            doneToday.Add(CampUse.GrassRoll);
            return null;
        }

        string TryLieBack(IReadOnlyList<PersonActivity> acts, float time, bool force)
        {
            var camp = meals.Fire;
            if (!GrassSpot(camp, 0.5f, force ? 2.5f : 1.6f, false, out var spot)) return "no grass near the camp";
            var grown = Pick(acts, spot, a => IsGrown(a), 1, force);
            if (grown.Count == 0) return "no grown-up free";
            int a = grown[0];
            // a couple, or a parent with a kid
            int b = PartnerOf(a);
            bool bKid = false;
            if (b <= 0 || !Shown(acts, b) || !force && !FreeKey(acts, b))
            {
                b = -1;
                foreach (var n in sim.State.notables) if (n.alive && n.parentId == a && n.age < 14f && Shown(acts, n.id) && (force || FreeKey(acts, n.id))) { b = n.id; bKid = true; break; }
            }
            if (b < 0 && force)
            {
                var o = Pick(acts, spot, x => IsGrown(x) || IsKid(x), 1, true, new[] { a });
                if (o.Count > 0) { b = o[0]; foreach (var x in acts) if (x.who.Key == b) bKid = IsKid(x); }
            }
            if (b < 0) return "nobody to lie with them";
            Add(Thing(CampScenes3.LieBack(a, b, At(a).Value, At(b).Value, bKid, spot, TowardCamera(spot) - spot, force ? 7f : BeatHash.Range(6f, 10f, (int)today, 3, 6942), time), ThingGrass));
            doneToday.Add(CampUse.LieBack);
            return null;
        }

        string TryFlowers(IReadOnlyList<PersonActivity> acts, float time, bool force)
        {
            var camp = meals.Fire;
            var who = Pick(acts, camp, a => IsKid(a) || IsGrown(a), 1, force);
            if (who.Count == 0) return "nobody free";
            int p = who[0];
            var from = At(p).Value;
            var flowers = FlowersNear(from, force ? 3f : 1.6f, 3);
            if (flowers.Count == 0 && force)
            {   // no baked flower near: the tall grass still has them [est]
                if (GrassSpot(camp, 0.4f, 2.5f, true, out var g) || GrassSpot(camp, 0.4f, 2.5f, false, out g)) { flowers.Add(g); flowers.Add(g + CameraAcross() * 0.1f); }
            }
            if (flowers.Count == 0) return "no flowers in the tall grass near";
            // who gets it: a parent, a sweetheart, the old one, else the nearest grown-up
            int taker = ParentOf(p);
            if (taker <= 0 || !Shown(acts, taker)) taker = PartnerOf(p);
            if (taker <= 0 || !Shown(acts, taker)) taker = Eldest(acts, true, new[] { p });
            if (taker <= 0 || !Shown(acts, taker)) taker = Nearest(acts, camp, 9f, a => IsGrown(a), new[] { p }, false);
            if (taker < 0) return "nobody to give them to";
            Add(CampScenes3.PickFlowers(p, from, flowers, taker, At(taker).Value, time));
            doneToday.Add(CampUse.PickFlowers);
            return null;
        }

        string TryWhistle(IReadOnlyList<PersonActivity> acts, float time, bool force)
        {
            var camp = meals.Fire;
            var kids = Pick(acts, camp, a => IsKid(a), 3, force);
            if (kids.Count == 0) return "no kid free";
            int k = kids[0];
            if (!GrassSpot(At(k).Value, 0f, force ? 2.5f : 1.2f, false, out var spot)) return "no grass near";
            var others = new List<int>(); var oat = new List<Vector2>();
            for (int i = 1; i < kids.Count; i++) { others.Add(kids[i]); oat.Add(At(kids[i]).Value); }
            Add(CampScenes3.GrassWhistle(k, At(k).Value, spot, others, oat, time, CameraAcross()));
            doneToday.Add(CampUse.GrassWhistle);
            return null;
        }

        string TryWater(IReadOnlyList<PersonActivity> acts, float time, bool force)
        {
            var camp = meals.Fire;
            if (!SaplingNear(camp, force ? 4f : LandGeo.SaplingReach, out var sap, out float crown)) return "no wild sapling near the camp ('Land: a wild tree and saplings' first)";
            var who = Pick(acts, sap, a => IsGrown(a), 1, force);
            if (who.Count == 0) return "no grown-up free";
            var s = CampScenes3.WaterSapling(who[0], At(who[0]).Value, sap, time, CameraAcross() + Dir2(sap, TowardCamera(sap)) * 0.35f, Mathf.Max(0.08f, crown * 0.7f)); // beside the little tree as the camera sees it: its crown never hides the pour
            Add(Thing(s, ThingSapling));
            doneToday.Add(CampUse.WaterSapling);
            MaybeTwig(acts, sap, s.Length + 0.3f, -1, force);
            return null;
        }

        string TryRing(IReadOnlyList<PersonActivity> acts, float time, bool force)
        {
            var camp = meals.Fire;
            if (!SaplingNear(camp, force ? 4f : LandGeo.SaplingReach, out var sap, out float crown, p => !Ringed(p))) return "no sapling without a ring near the camp ('Land: a wild tree and saplings' first)";
            if (!force && rings.Count >= LandGeo.MaxRings) return "four rings already";
            var g = Pick(acts, sap, a => IsGrown(a), 1, force);
            if (g.Count == 0) return "no grown-up free";
            var k = Pick(acts, sap, a => IsKid(a), 1, force, g);
            if (k.Count == 0 && !force) return "no kid free to help";
            EnsureRocks();
            // #692 p3 (the user's review): the stones come from a boulder only when one is near; else from loose stones beside the little tree, so
            // both trips stay in the frame
            Vector2 pile = sap + CameraAcross() * 0.3f + Dir2(sap, TowardCamera(sap)) * 0.08f;
            foreach (var rk in rocks) if ((rk - sap).magnitude < 0.6f) { pile = rk + Dir2(rk, sap) * 0.1f; break; }
            int kid = k.Count > 0 ? k[0] : -1;
            var s = CampScenes3.RingSapling(g[0], kid, At(g[0]).Value, kid >= 0 ? At(kid).Value : default, sap, pile, time, LandGeo.RingFor(crown));
            Add(Thing(s, ThingSapling));
            rings.Add((sap, new List<Vector2>()));
            if (rings.Count > LandGeo.MaxRings) rings.RemoveAt(0);
            doneToday.Add(CampUse.RingSapling);
            if (kid >= 0) MaybeTwig(acts, sap, s.Length + 0.3f, kid, force);
            return null;
        }

        /// <summary>B34.c: right after a watering or a ring, sometimes a kid who watched plants a twig beside the sapling.</summary>
        void MaybeTwig(IReadOnlyList<PersonActivity> acts, Vector2 sap, float after, int kid, bool force)
        {
            if (force) return; // the cheats play one use alone; "A kid plants a twig" is its own cheat
            if (BeatHash.U01((int)(today & 0x7fffffff), 9, 6943) >= Sometimes) return;
            Later(after, () =>
            {
                var a = pv.Activities;
                int k = kid;
                if (k < 0 || byKey.ContainsKey(k) || !At(k).HasValue) { var f = FreeNear(a, sap, 1.5f, x => IsKid(x), 1); k = f.Count > 0 ? f[0] : -1; }
                return k < 0 ? "no kid to plant a twig" : StartTwig(k, sap);
            });
        }

        string StartTwig(int kid, Vector2 sap)
        {
            var at = At(kid).Value;
            var twigAt = DeadfallAt(meals.Fire, out var df) && (df - sap).magnitude < 1.2f ? df : Vector2.Lerp(at, sap, 0.4f) + CameraAcross() * 0.12f;
            float crown = 0f; var box = LandBox;
            if (box != null) lock (box.SaplingSpots) foreach (var kv in box.SaplingSpots) if ((new Vector2(kv.Value.pos.x, kv.Value.pos.z) - sap).magnitude < 0.03f) crown = kv.Value.r;
            Add(Thing(CampScenes3.PlantTwig(kid, at, sap, twigAt, clock, Dir2(sap, TowardCamera(sap)), Mathf.Max(0.06f, crown * 0.75f)), ThingSapling)); // #692 p3: on the camera's side
            doneToday.Add(CampUse.PlantTwig);
            return "a kid plants a twig: " + kid;
        }

        string TryTwig(IReadOnlyList<PersonActivity> acts, bool force)
        {
            if (!SaplingNear(meals.Fire, force ? 4f : LandGeo.SaplingReach, out var sap)) return "no wild sapling near the camp ('Land: a wild tree and saplings' first)";
            var k = Pick(acts, sap, a => IsKid(a), 1, force);
            if (k.Count == 0) return "no kid free";
            return StartTwig(k[0], sap);
        }

        string TryRock(IReadOnlyList<PersonActivity> acts, float time, bool force)
        {
            EnsureRocks();
            if (rocks.Count == 0) return "no room for a boulder by the camp";
            var g = Pick(acts, rocks[0], a => IsGrown(a), 1, force);
            int kid = -1;
            if (force || BeatHash.U01((int)(today & 0x7fffffff), 11, 6944) < Sometimes) { var k = Pick(acts, rocks[rocks.Count - 1], a => IsKid(a), 1, force, g); kid = k.Count > 0 ? k[0] : -1; }
            if (g.Count == 0 && kid < 0) return "nobody free";
            int grown = g.Count > 0 ? g[0] : -1;
            var kidRock = rocks.Count > 1 && (rocks[1] - rocks[0]).magnitude <= 0.6f ? rocks[1] : rocks[0]; // #692 p3: a far boulder leaves the frame: share the near one
            Add(Thing(CampScenes3.SitRock(grown, grown >= 0 ? At(grown).Value : default, rocks[0], TowardCamera(rocks[0]) - rocks[0], force ? 10f : 20f, kid, kid >= 0 ? At(kid).Value : default, kidRock, time), ThingRock));
            doneToday.Add(CampUse.SitRock);
            return null;
        }

        string TryStack(IReadOnlyList<PersonActivity> acts, float time, bool force)
        {
            EnsureRocks();
            var camp = meals.Fire;
            var near = rocks.Count > 0 ? rocks[rocks.Count - 1] : camp;
            var kids = Pick(acts, near, a => IsKid(a), 1, force);
            int who = kids.Count > 0 ? kids[0] : -1; bool kid = who >= 0;
            if (who < 0) { int old = Eldest(acts, true); if (old >= 0 && (force || FreeKey(acts, old))) who = old; }
            if (who < 0) return "no kid or old one free";
            // on a boulder's top (its middle, a little toward the camera), else on the ground by the camp
            Vector2 spot; float baseH;
            if (rocks.Count > 0) { var r = rocks[rocks.Count - 1]; spot = r + Dir2(r, TowardCamera(r)) * 0.02f; baseH = LandGeo.RockH; }
            else { spot = camp + Dir2(camp, TowardCamera(camp)) * 0.5f; baseH = 0f; }
            for (int i = stacks.Count - 1; i >= 0; i--) if ((stacks[i].p - spot).magnitude < 0.04f) stacks.RemoveAt(i); // a new stack where the old one was
            bool topple = force ? ForceTopple : BeatHash.Pick(3, (int)today, who, 6945) == 0;
            Add(Thing(CampScenes3.StackPebbles(who, kid, At(who).Value, spot, baseH, topple, time, spot - TowardCamera(spot)), ThingRock));
            doneToday.Add(CampUse.StackPebbles);
            return null;
        }

        /// <summary>The cheat's stack topples (alternates with each press: B35.b plays both ways).</summary>
        public static bool ForceTopple;

        string TryFight(IReadOnlyList<PersonActivity> acts, float time, bool force)
        {
            if (!DeadfallAt(meals.Fire, out var df)) return "no deadfall near the camp ('Land: a wild tree and saplings' first)";
            var kids = Pick(acts, df, a => IsKid(a), 2, force);
            if (kids.Count < 2) return $"only {kids.Count} kid(s) free ('Two kids join the camp' first)";
            Add(Thing(CampScenes3.StickFight(kids[0], kids[1], At(kids[0]).Value, At(kids[1]).Value, df, TowardCamera(df) - df, time), ThingDeadfall));
            doneToday.Add(CampUse.StickFight);
            return null;
        }

        string TryHorse(IReadOnlyList<PersonActivity> acts, float time, bool force)
        {
            if (!DeadfallAt(meals.Fire, out var df)) return "no deadfall near the camp ('Land: a wild tree and saplings' first)";
            var kids = Pick(acts, df, a => IsKid(a), 1, force);
            if (kids.Count == 0) return "no kid free";
            var camp = meals.Fire;
            var centre = camp + Dir2(camp, TowardCamera(camp)) * 0.5f + CameraAcross() * 0.15f;
            EnsureRocks();
            // #692 p3 (the user's review: the kid rode out of frame): the end beat stays by the loop: against a boulder only when one is near
            var lean = rocks.Count > 0 && (rocks[0] - centre).magnitude < 0.55f ? rocks[0] + Dir2(rocks[0], centre) * (LandGeo.RockW * 0.5f + 0.01f) : centre + CameraAcross() * (CampScenes3.HorseLoopR + 0.16f);
            Add(Thing(CampScenes3.StickHorse(kids[0], At(kids[0]).Value, df, centre, lean, time), ThingDeadfall));
            doneToday.Add(CampUse.StickHorse);
            return null;
        }

        // ================================================================== cheats
        string ForceLand(CampUse use)
        {
            if (sim == null || planner == null || meals == null) return "no camp yet";
            if (!meals.HasFire) return "no fire in the camp";
            var acts = pv.Activities; float time = clock;
            EnsureRocks();
            switch (use)
            {
                case CampUse.GrassRoll: return TryRoll(acts, time, true) ?? "kids roll in the grass";
                case CampUse.LieBack: return TryLieBack(acts, time, true) ?? "lie back in the grass";
                case CampUse.PickFlowers: return TryFlowers(acts, time, true) ?? "pick flowers";
                case CampUse.GrassWhistle: return TryWhistle(acts, time, true) ?? "a grass whistle";
                case CampUse.WaterSapling: return TryWater(acts, time, true) ?? "water a sapling";
                case CampUse.RingSapling: return TryRing(acts, time, true) ?? "ring a sapling with stones";
                case CampUse.PlantTwig: return TryTwig(acts, true);
                case CampUse.SitRock: return TryRock(acts, time, true) ?? "sit on a rock / a kid on the rock";
                case CampUse.StackPebbles: { var r = TryStack(acts, time, true) ?? (ForceTopple ? "stack pebbles (it topples)" : "stack pebbles"); ForceTopple = !ForceTopple; return r; }
                case CampUse.StickFight: return TryFight(acts, time, true) ?? "a sword fight with sticks";
                case CampUse.StickHorse: return TryHorse(acts, time, true) ?? "a stick horse";
            }
            return "not a land use: " + use;
        }

        /// <summary>"Land: a wild tree and saplings": the Keeper's tree grown, a grown wild tree (its deadfall) and saplings beside it, near the camp.</summary>
        public string LandReady()
        {
            if (sim == null || !sim.EcologyOn) return "no living land";
            string r = "";
            if (!sim.EcoTree.planted || sim.EcoTree.stage < 3) r += CampCheats.GrowTree() + "; ";
            var camp = meals != null && meals.HasFire ? meals.Fire : Vector2.zero;
            if (!DeadfallAt(camp, out _) || !SaplingNear(camp, 4f, out _)) r += EcoDebug.WildTrees(true);
            if (!SaplingNear(camp, 4f, out _)) r += "; " + EcoDebug.SaplingsWhere((x, z) => InView(LandBox, new Vector2(x, z)) && (new Vector2(x, z) - camp).magnitude < 3.5f); // #692 p3
            EnsureRocks();
            return r + $"; rocks {rocks.Count}, deadfall {(DeadfallAt(camp, out _) ? "yes" : "no")}, sapling {(SaplingNear(camp, 4f, out _) ? "yes" : "not drawn yet")}";
        }

        // ================================================================== the marks: made by the scenes' effects
        void LandMark(CampScene s, in CampFx f)
        {
            var kind = (CampMark)f.count;
            var p = new Vector2(f.at.x, f.at.z);
            if (f.anchor == CampAnchor.Head)
            {
                if (f.member < 0 || f.member >= s.Count) return;
                float until = kind == CampMark.EarFlower ? now.day * 24f + 21f : ViewHours + 3f; // the flower until night; the grass bits until the next meal [est: 3 view hours]
                int wk = s.keys[f.member];
                worn.RemoveAll(w => w.key == wk && w.kind == kind);
                worn.Add((s.keys[f.member], kind, until));
                return;
            }
            switch (kind)
            {
                case CampMark.WetPatch: wets.Add((p, f.scale > 0f ? f.scale : 0.05f, ViewHours)); if (wets.Count > 6) wets.RemoveAt(0); break;
                case CampMark.RingStone:
                {
                    int best = -1; float bd = 0.2f;
                    for (int i = 0; i < rings.Count; i++) { float d = (rings[i].sapling - p).magnitude; if (d < bd) { bd = d; best = i; } }
                    if (best < 0) { rings.Add((p, new List<Vector2>())); best = rings.Count - 1; }
                    if (rings[best].stones.Count < LandGeo.RingStones) rings[best].stones.Add(p);
                    break;
                }
                case CampMark.Twig: twigs.Add((p, today / 3)); if (twigs.Count > LandGeo.MaxTwigs) twigs.RemoveAt(0); break;
                case CampMark.Pebble:
                {
                    Stack st = null;
                    foreach (var x in stacks) if ((x.p - p).magnitude < 0.03f && x.toppleAt < 0f) st = x;
                    if (st == null) { st = new Stack { p = p, h = f.at.y, day = today }; stacks.Add(st); if (stacks.Count > LandGeo.MaxStacks) stacks.RemoveAt(0); }
                    if (st.n < LandGeo.Pebbles) st.n++;
                    st.wobbleAt = clock;
                    break;
                }
                case CampMark.Topple: foreach (var x in stacks) if ((x.p - p).magnitude < 0.03f && x.toppleAt < 0f) x.toppleAt = clock; break;
                case CampMark.Horse: horses.Clear(); horses.Add((p, ViewHours + 1f)); break;
                case CampMark.Flatten: flats.Add((p, clock + 3f)); break;
                case CampMark.EarFlower: laidFlowers.Add((p, ViewHours)); if (laidFlowers.Count > 3) laidFlowers.RemoveAt(0); break;
            }
        }

        // ================================================================== drawing (after the dolls)
        void DrawLand(Transform root, Material material, float time, System.Func<float, float, float> groundAt)
        {
            int used = 0;
            float vh = ViewHours;
            float rs = root.lossyScale.y;
            Transform Next(int meshKey)
            {
                while (used < landPool.Count && landPool[used] == null) landPool.RemoveAt(used); // the box (the people's root) was rebuilt: its marks went with it
                while (landPool.Count <= used)
                {
                    var go = new GameObject("Camp land mark") { hideFlags = HideFlags.DontSave };
                    go.transform.SetParent(root, false);
                    go.AddComponent<MeshFilter>();
                    var r = go.AddComponent<MeshRenderer>(); r.sharedMaterial = material; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    landPool.Add(go.transform);
                }
                var t = landPool[used++];
                t.GetComponent<MeshFilter>().sharedMesh = LandMesh(meshKey);
                t.gameObject.SetActive(true);
                t.localRotation = Quaternion.identity; t.localScale = Vector3.one;
                return t;
            }
            Vector3 On(Vector2 p, float h = 0f) => new Vector3(p.x, groundAt(p.x, p.y) + h, p.y);

            if (sim != null && sim.State.era == Era.Camp) { EnsureRocks(); foreach (var r in rocks) { var t = Next(MeshRock); t.localPosition = On(r, -0.004f); t.localRotation = Quaternion.Euler(0f, r.x * 97f, 0f); } }
            foreach (var ring in rings) foreach (var st in ring.stones) { var t = Next(MeshRingStone); t.localPosition = On(st); }
            for (int i = twigs.Count - 1; i >= 0; i--) if (today / 3 != twigs[i].season) twigs.RemoveAt(i);
            foreach (var tw in twigs) { var t = Next(MeshTwig); t.localPosition = On(tw.p); t.localRotation = Quaternion.Euler(6f, tw.p.x * 131f, 4f); }
            for (int i = stacks.Count - 1; i >= 0; i--) if (stacks[i].day != today) stacks.RemoveAt(i);
            foreach (var st in stacks)
            {
                float w = time - st.wobbleAt;
                float wob = w >= 0f && w < 1.2f ? 3f * Mathf.Sin(w * 4f * Mathf.PI * 2f) * Mathf.Exp(-w / 0.3f) : 0f;
                float tp = st.toppleAt >= 0f ? time - st.toppleAt : -1f;
                float y = st.h;
                for (int i = 0; i < st.n; i++)
                {
                    float size = LandGeo.PebbleSize(i);
                    var t = Next(MeshPebble);
                    t.localScale = Vector3.one * size;
                    if (tp < 0f)
                    {
                        y += size * 0.4f;
                        var lean = Quaternion.Euler(wob, 0f, wob * 0.5f);
                        t.localPosition = On(st.p) + lean * new Vector3(0f, y, 0f);
                        y += size * 0.4f;
                    }
                    else
                    {
                        // the topple: each pebble falls off and rolls out (a bounce, then still), off the boulder's top to the ground
                        float ang = i * 2.1f + st.p.x * 10f, dist = 0.05f + 0.02f * i;
                        float u = Mathf.Clamp01(tp / 0.8f);
                        var to = st.p + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * (dist + (st.h > 0f ? LandGeo.RockW * 0.5f : 0f));
                        var p = Vector2.Lerp(st.p, to, u);
                        float hy = Mathf.Lerp(st.h + LandGeo.StackHeight(i), 0f, u) + 0.025f * Mathf.Abs(Mathf.Sin(u * Mathf.PI * 2f)) * (1f - u);
                        t.localPosition = On(p, hy + size * 0.4f);
                        t.localRotation = Quaternion.Euler(u * 300f, ang * 50f, 0f);
                    }
                }
            }
            for (int i = wets.Count - 1; i >= 0; i--) if (vh - wets[i].at > 1f || vh < wets[i].at) wets.RemoveAt(i);
            foreach (var w in wets) { float k = 1f - (vh - w.at); var t = Next(MeshWet); t.localPosition = On(w.p, 0.002f); t.localScale = new Vector3(w.r * Mathf.Lerp(0.5f, 1f, k), 1f, w.r * Mathf.Lerp(0.5f, 1f, k)); }
            for (int i = horses.Count - 1; i >= 0; i--) if (vh > horses[i].until || vh < horses[i].until - 1.01f) horses.RemoveAt(i);
            bool horseInHand = false;
            foreach (var s in scenes) if (s.use == CampUse.StickHorse && time - s.t0 < s.b) horseInHand = true;
            foreach (var hz in horses) if (!horseInHand) { var t = Next(MeshHorse); t.localPosition = On(hz.p); var away = Dir2(hz.p, meals != null ? meals.Fire : hz.p + Vector2.up); t.localRotation = Quaternion.LookRotation(new Vector3(away.x, 0f, away.y)) * Quaternion.Euler(20f, 0f, 0f); }
            for (int i = laidFlowers.Count - 1; i >= 0; i--) if (vh - laidFlowers[i].at > 6f || vh < laidFlowers[i].at) laidFlowers.RemoveAt(i);
            foreach (var lf in laidFlowers) { var t = Next(MeshBunch); t.localPosition = On(lf.p, 0.004f); t.localRotation = Quaternion.Euler(80f, lf.p.x * 77f, 0f); t.localScale = Vector3.one * 0.011f; }
            // what people wear: grass bits in the hair, a flower behind the ear
            for (int i = worn.Count - 1; i >= 0; i--) if (vh > worn[i].until || worn[i].kind == CampMark.GrassBits && meals != null && meals.Eaters > 0) worn.RemoveAt(i);
            foreach (var w in worn)
            {
                var v = pv.ViewOf(w.key);
                if (v == null || !v.gameObject.activeInHierarchy) continue;
                var head = v.transform.TransformPoint(new Vector3(0f, PegDoll.HeadY * v.scale, 0f));
                if (w.kind == CampMark.GrassBits)
                {
                    for (int b = 0; b < 2; b++)
                    {
                        var t = Next(MeshBit);
                        t.position = head + v.transform.rotation * new Vector3(b == 0 ? 0.008f : -0.006f, 0.022f, b == 0 ? 0.004f : -0.006f) * v.scale * 1.4f;
                        t.localScale = Vector3.one * 0.008f;
                    }
                }
                else
                {
                    var t = Next(MeshBunch);
                    t.position = head + v.transform.rotation * new Vector3(0.022f, 0.012f, -0.004f) * v.scale * 1.4f;
                    t.rotation = v.transform.rotation * Quaternion.Euler(0f, 0f, -60f);
                    t.localScale = Vector3.one * 0.009f;
                }
            }
            for (int i = used; i < landPool.Count; i++) if (landPool[i] != null) landPool[i].gameObject.SetActive(false);
        }

        const int MeshRock = 1, MeshRingStone = 2, MeshTwig = 3, MeshPebble = 4, MeshWet = 5, MeshHorse = 6, MeshBit = 7, MeshBunch = 8;

        Mesh LandMesh(int key)
        {
            if (landMeshes.TryGetValue(key, out var m) && m != null) return m;
            var mb = new MeshBuilder(6946 + key);
            switch (key)
            {
                case MeshRock: // a low flat boulder, 0.12 u wide, 0.07 u high, with a paler top (B35 [est])
                    mb.Ico(new Vector3(0f, LandGeo.RockH * 0.5f, 0f), 1f, new Vector3(LandGeo.RockW * 0.5f, LandGeo.RockH * 0.55f, LandGeo.RockW * 0.42f), Palette.Rock, 0.12f, Palette.RockDark);
                    mb.Ico(new Vector3(0.02f, LandGeo.RockH * 0.35f, 0.03f), 1f, new Vector3(0.03f, 0.022f, 0.025f), Palette.RockDark, 0.12f);
                    break;
                case MeshRingStone: mb.Ico(new Vector3(0f, 0.009f, 0f), 0.019f, new Vector3(1f, 0.7f, 0.9f), Palette.Rock, 0.15f, Palette.RockDark); break; // #692 p3: big and grey enough to read on sand
                case MeshTwig:
                    mb.Box(new Vector3(0f, 0.026f, 0f), new Vector3(0.006f, 0.052f, 0.006f), Palette.Bark);
                    mb.Box(new Vector3(0.006f, 0.04f, 0f), new Vector3(0.012f, 0.003f, 0.003f), Palette.Bark, Quaternion.Euler(0f, 0f, 35f));
                    break;
                case MeshPebble: mb.Ico(Vector3.zero, 0.5f, new Vector3(1f, 0.7f, 0.9f), Palette.Gravel, 0.15f, Palette.RockDark); break;
                case MeshWet: mb.Ico(Vector3.zero, 1f, new Vector3(1f, 0.04f, 1f), Palette.SoilDark, 0.06f); break;
                case MeshHorse: // the stick horse leaning: a long branch, its "head" a fork at the top
                    mb.Box(new Vector3(0f, 0.1f, 0f), new Vector3(0.008f, 0.2f, 0.008f), Palette.Bark);
                    mb.Box(new Vector3(0f, 0.2f, 0.008f), new Vector3(0.006f, 0.006f, 0.02f), Palette.Bark);
                    break;
                case MeshBit: mb.Ico(Vector3.zero, 1f, new Vector3(1f, 0.5f, 0.7f), Palette.LeafC, 0.05f); break;
                case MeshBunch: LandTipMesh(CampTipKind.Bunch, mb); break;
            }
            m = mb.ToMesh("Camp_Land_" + key); m.hideFlags = HideFlags.DontSave;
            return landMeshes[key] = m;
        }

        /// <summary>#692 p3: the bunch's three heads: a hot pink, a white, a sun yellow.</summary>
        static readonly Color[] BunchHeads = { new(1f, 0.38f, 0.62f), new(1f, 1f, 0.97f), new(1f, 0.78f, 0.12f) };

        /// <summary>The part 3 tips, each a unit size: a stone, three stones, a pebble, a bunch of three flowers, a grass blade, the stick horse.</summary>
        static bool LandTipMesh(CampTipKind kind, MeshBuilder mb)
        {
            switch (kind)
            {
                case CampTipKind.Stone: mb.Ico(Vector3.zero, 1f, new Vector3(1f, 0.75f, 0.9f), Palette.RockLight, 0.15f, Palette.RockDark); return true;
                case CampTipKind.Stones:
                    mb.Ico(new Vector3(-0.9f, 0f, 0f), 1f, new Vector3(1f, 0.75f, 0.9f), Palette.RockLight, 0.15f, Palette.RockDark);
                    mb.Ico(new Vector3(0.9f, 0f, 0.1f), 0.9f, new Vector3(1f, 0.75f, 0.9f), Palette.Rock, 0.15f, Palette.RockDark);
                    mb.Ico(new Vector3(0f, 0.9f, 0f), 0.85f, new Vector3(1f, 0.75f, 0.9f), Palette.RockLight, 0.15f, Palette.RockDark);
                    return true;
                case CampTipKind.Pebble: mb.Ico(Vector3.zero, 1f, new Vector3(1f, 0.7f, 0.9f), Palette.Gravel, 0.15f, Palette.RockDark); return true;
                case CampTipKind.Bunch:
                    for (int i = 0; i < 3; i++)
                    {
                        float a = i * 2.1f;
                        var top = new Vector3(Mathf.Cos(a) * 0.6f, 2.6f + 0.2f * i, Mathf.Sin(a) * 0.6f);
                        mb.Box(top * 0.5f, new Vector3(0.22f, 2.6f, 0.22f), new Color(0.35f, 0.6f, 0.2f));
                        mb.Ico(top, 1.05f, new Vector3(1f, 0.6f, 1f), BunchHeads[i], 0.05f); // #692 p3: bright heads that read on a phone
                        mb.Ico(top + Vector3.up * 0.32f, 0.4f, Vector3.one, new Color(1f, 0.9f, 0.3f), 0.05f);
                    }
                    return true;
                case CampTipKind.Blade: mb.Box(new Vector3(0f, 0f, 0f), new Vector3(2.4f, 0.3f, 0.15f), Palette.GrassA); return true;
                case CampTipKind.Horse: // #692 p3: a thicker pale stick with a knobbly "head", so it reads in the kid's hands
                    mb.Box(new Vector3(0f, 0.5f, 0f), new Vector3(0.1f, 1f, 0.1f), Palette.WoodLight);
                    mb.Ico(new Vector3(0f, 1f, 0.04f), 0.09f, new Vector3(0.9f, 1f, 1.5f), Palette.Bark, 0.05f);
                    return true;
            }
            return false;
        }

        /// <summary>Where a part 3 tip sits: in the right hand (the stones, a pebble, the bunch); the blade across both hands at the mouth; the stick
        /// horse from the hand at the chest back and down to the ground behind (30°, 0.2 u long).</summary>
        void PlaceLandTip(Transform t, PegDollView v, CampTip tip, float u, Transform root, float sc)
        {
            float rs = root.lossyScale.y;
            var hand = v.armR.TransformPoint(new Vector3(0f, -PegDoll.ArmLen * v.scale, 0f));
            switch (tip.kind)
            {
                case CampTipKind.Stones:
                {
                    var handL = v.armL.TransformPoint(new Vector3(0f, -PegDoll.ArmLen * v.scale, 0f));
                    t.position = Vector3.Lerp(hand, handL, 0.5f) + Vector3.up * (0.01f * rs);
                    t.rotation = v.transform.rotation;
                    t.localScale = Vector3.one * 0.011f * Mathf.Max(0.8f, sc);
                    return;
                }
                case CampTipKind.Blade:
                {
                    var handL = v.armL.TransformPoint(new Vector3(0f, -PegDoll.ArmLen * v.scale, 0f));
                    t.position = Vector3.Lerp(hand, handL, 0.5f);
                    t.rotation = v.transform.rotation;
                    t.localScale = Vector3.one * 0.01f * Mathf.Max(0.8f, sc);
                    return;
                }
                case CampTipKind.Horse:
                {
                    // #692 p3 (the user's review: it did not read as riding): the stick between the legs, its head up in front of the chest, its
                    // foot trailing on the ground behind, 30° down to the back; it passes the body low, at the hips, and bobs with the doll
                    var back = -v.transform.forward; back.y = 0f; back = back.sqrMagnitude > 1e-6f ? back.normalized : Vector3.back;
                    var dir = (back * Mathf.Cos(30f * Mathf.Deg2Rad) + Vector3.down * Mathf.Sin(30f * Mathf.Deg2Rad)).normalized;
                    var hip = v.transform.TransformPoint(new Vector3(0f, PegDoll.BodyH * 0.3f * v.scale, 0f));
                    float ahead = 0.1f * rs * Mathf.Max(0.8f, sc);
                    var head = hip - dir * ahead;
                    float hipH = Mathf.Max(0.01f * rs, hip.y - v.transform.position.y);
                    float len = ahead + hipH / Mathf.Sin(30f * Mathf.Deg2Rad); // the foot just reaches the ground behind
                    // the unit mesh runs from its foot (0) to its head (1, the fork)
                    t.rotation = Quaternion.FromToRotation(Vector3.up, -dir);
                    t.position = head + dir * len;
                    t.localScale = Vector3.one * len;
                    return;
                }
                default:
                    t.position = hand + Vector3.up * (0.006f * rs);
                    t.rotation = Quaternion.identity;
                    float size = tip.kind == CampTipKind.Bunch ? 0.026f : tip.kind == CampTipKind.Stone ? 0.012f * Mathf.Max(1f, tip.grow) /* #693 retakes: the name-cutter's stone, 1.8x */ : 0.012f * Mathf.Max(0.5f, tip.grow); // #692 p3: the bunch held up big enough to read
                    t.localScale = Vector3.one * size * Mathf.Max(0.8f, sc);
                    return;
            }
        }

        void DestroyLand()
        {
            foreach (var t in landPool) if (t != null) UnityEngine.Object.Destroy(t.gameObject);
            landPool.Clear();
        }

        /// <summary>Tests and the readout: what the land's marks hold now.</summary>
        public string LandMarks() => $"rocks {rocks.Count}, rings {rings.Count} ({(rings.Count > 0 ? rings[^1].stones.Count : 0)} stones), twigs {twigs.Count}, stacks {stacks.Count} ({(stacks.Count > 0 ? stacks[^1].n : 0)}{(stacks.Count > 0 && stacks[^1].toppleAt >= 0f ? " toppled" : "")}), wet {wets.Count}, horses {horses.Count}, worn {worn.Count}, flat {flats.Count}";
        public int RingStonesNow => rings.Count > 0 ? rings[^1].stones.Count : 0;
        public int TwigsNow => twigs.Count;
        public int PebblesNow => stacks.Count > 0 ? stacks[^1].n : 0;
        public bool ToppledNow => stacks.Count > 0 && stacks[^1].toppleAt >= 0f;
        public int WetNow => wets.Count;
        public int HorsesNow => horses.Count;
        public int WornNow => worn.Count;
    }
}

namespace AquariumCiv.View
{
    /// <summary>#692 part 3 cheats (SliceDebugPanel's "#692" block): each starts the use now with the people nearest its thing.</summary>
    public static partial class CampCheats
    {
        public static string GrassRoll() => Do(CampUse.GrassRoll);
        public static string LieBack() => Do(CampUse.LieBack);
        public static string PickFlowers() => Do(CampUse.PickFlowers);
        public static string GrassWhistle() => Do(CampUse.GrassWhistle);
        public static string WaterSapling() => Do(CampUse.WaterSapling);
        public static string RingSapling() => Do(CampUse.RingSapling);
        public static string PlantTwig() => Do(CampUse.PlantTwig);
        public static string SitRock() => Do(CampUse.SitRock);
        public static string StackPebbles() => Do(CampUse.StackPebbles);
        public static string StickFight() => Do(CampUse.StickFight);
        public static string StickHorse() => Do(CampUse.StickHorse);
        /// <summary>"Land: a wild tree and saplings" (the Keeper's tree, a grown wild tree with its deadfall and saplings beside it, near the camp).</summary>
        public static string LandReady() { var c = Camp; return c == null ? "no PeopleView" : c.LandReady(); }
        public static string LandMarks() { var c = Camp; return c == null ? "no PeopleView" : c.LandMarks(); }
    }
}
