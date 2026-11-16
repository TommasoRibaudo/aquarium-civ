// #692 part 4 (docs/design/animation-specs.md "#692 part 4: animals", B37-B39, and B15.d for B39.b's rate check): CampLife's side of the
// animal uses. Which use starts (the D rules: life uses borrow free time only; how often by the view day's roll; one use per animal group at a
// time), the cheats, and the animals' moments, handed to their own views each frame: PondLife's frog sits on while a kid creeps up, rides in
// the cupped hands and hops back in; the ducks come fast to the crumbs and one climbs out; a doe looks up at the watchers; the herd bolts from
// the creeping kids (DeerView's knock); a rabbit kit (the rabbit doll at 0.6, drawn here) hops to a kid, is held and hops back to its mother.
// B39.c (kids try to catch a fish by hand) is #708's FishGrabGame, a kids' play game: not a camp scene. The timelines: View/Beats/CampScenes4.cs.
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
        const int ThingFrogs = 11, ThingHerd = 12, ThingMeadow = 13, ThingDucks = 14;

        static bool IsAnimalUse(CampUse u) => u >= CampUse.FrogCatch && u <= CampUse.FeedDucks;

        /// <summary>A scene's animal moments already handed over (once each).</summary>
        sealed class AnimalTrack { public int frog = -1; public bool grabbed, hopped, looked, looked2, bolted, freed; }
        readonly Dictionary<CampScene, AnimalTrack> animalTracks = new();
        RabbitDoll kitDoll; Transform kitParent;
        readonly List<Transform> crumbPool = new();
        Mesh crumbMesh;
        /// <summary>The cheat's catch misses (alternates with each press: B37 plays both ways).</summary>
        public static bool ForceFrogMiss;
        /// <summary>Tests: frames the frog rode in a kid's hands, frames the kit was drawn, crumbs drawn in flight.</summary>
        public int FrogHeldFrames { get; private set; }
        public int KitFrames { get; private set; }
        public int CrumbFrames { get; private set; }
        public int DeerLooks { get; private set; }
        public int DeerBolts { get; private set; }

        void ResetAnimals()
        {
            foreach (var kv in animalTracks) if (kv.Value.frog >= 0 && !kv.Value.hopped) PondLife.Instance?.FreeFrog(kv.Value.frog);
            animalTracks.Clear();
        }

        AnimalTrack Track(CampScene s) { if (!animalTracks.TryGetValue(s, out var t)) animalTracks[s] = t = new AnimalTrack(); return t; }

        // ================================================================== what the animals offer
        /// <summary>The way to the water from a bank point (the open water cells round it), or zero when none is beside it.</summary>
        Vector2 ToWaterAt(Vector2 p, float reach = 0.25f)
        {
            var box = LandBox;
            if (box == null) return Vector2.zero;
            var eco = box.layout.eco;
            var sum = Vector2.zero;
            int ci = SiteMap.I(SiteMap.CellAt(p.x, p.y)), cj = SiteMap.J(SiteMap.CellAt(p.x, p.y));
            int r = Mathf.CeilToInt(reach / SiteMap.Cell);
            for (int dj = -r; dj <= r; dj++)
                for (int di = -r; di <= r; di++)
                {
                    int a = ci + di, b = cj + dj;
                    if (a < 0 || b < 0 || a >= SiteMap.NX || b >= SiteMap.NZ) continue;
                    int c = SiteMap.Index(a, b);
                    if (!eco.WaterCell(c)) continue;
                    var q = new Vector2(SiteMap.CXc(c), SiteMap.CZc(c)) - p;
                    if (q.magnitude <= reach + 1e-3f) sum += q.sqrMagnitude > 1e-6f ? q.normalized : Vector2.zero;
                }
            return sum.sqrMagnitude > 1e-6f ? sum.normalized : Vector2.zero;
        }

        /// <summary>The pond's edge nearest <paramref name="near"/> within <paramref name="reach"/>: a dry cell with open water beside it, clear of the
        /// glass, and the way to the water.</summary>
        bool PondEdge(Vector2 near, float reach, out Vector2 edge, out Vector2 toWater)
        {
            edge = toWater = default;
            var box = LandBox;
            if (box == null || box.layout.eco.pondCells <= 0) return false;
            var eco = box.layout.eco;
            float bd = reach;
            for (int c = 0; c < SiteMap.N; c++)
            {
                if (eco.WaterCell(c) || eco.At(c) == EcoLayer.Tree) continue;
                var p = new Vector2(SiteMap.CXc(c), SiteMap.CZc(c));
                if (!Terrain.InBox(p.x, p.y, Terrain.GlassMargin + 0.15f)) continue;
                float d = (p - near).magnitude + (ClearOfGlass(p) ? 0f : 1.5f); // clear of the front glass if it can be (the camera looks through it otherwise)
                if (d >= bd) continue;
                var w = ToWaterAt(p, SiteMap.Cell * 1.1f);
                if (w.sqrMagnitude < 1e-4f) continue;
                bd = d; edge = p; toWater = w;
            }
            return bd < reach;
        }

        bool HuntNow()
        {
            var deer = planner.DeerHuntsOn(now.day);
            if (deer != null) foreach (var h in deer) if (Hour >= h.walkOutHour - 0.3f && Hour < h.landHour + 1f) return true;
            return false;
        }

        bool HunterNear(IReadOnlyList<PersonActivity> acts, Vector2 p, float r)
        {
            foreach (var a in acts) if (a.hunt != HuntPhase.None && At(a.who.Key) is Vector2 q && (q - p).magnitude < r) return true;
            return false;
        }

        // ================================================================== the triggers (each frame, in the camp era)
        void StepAnimals(IReadOnlyList<PersonActivity> acts, float time)
        {
            StepAnimalMoments(time);
            if (OnlyCheats || meals == null || !meals.HasFire || sim == null || !sim.EcologyOn) return;
            if (meals.Eaters > 0) return;
            float h = Hour;
            if (h < 9f || h >= 16.5f) return;
            var season = LifeClock.SeasonOf(now.day);
            bool Go(CampUse u, float chance, int thing) => !doneToday.Contains(u) && Roll(u) < chance && LifeOk(u) && !ThingBusy(thing);
            // B15.d (B39.b): the crumbs, on the days AnimalGeo.DuckUsesPlanned says (the morning, the afternoon)
            var pond = PondLife.Instance;
            if (pond != null && pond.DucksOnWater > 0 && !pond.DucksAway && !ThingBusy(ThingDucks) && LifeOk(CampUse.FeedDucks))
            {
                AnimalGeo.DuckUsesPlanned(now.day, sim.State.seed, planner.EggGathersOn(now.day).Count > 0, out bool am, out bool pm);
                bool slot = h < 12f ? am && !feedAm.Contains(now.day) : h >= 13.5f && pm && !feedPm.Contains(now.day);
                if (slot && TryFeedDucks(acts, time, false) == null) { (h < 12f ? feedAm : feedPm).Add(now.day); return; }
            }
            // B37: catch a frog (spring and summer, sometimes)
            if (season is Season.Spring or Season.Summer && Go(CampUse.FrogCatch, Sometimes, ThingFrogs) && TryFrog(acts, time, false) == null) return;
            // B38: the deer at the camp's edge (never during a deer hunt)
            if (!ThingBusy(ThingHerd) && !HuntNow())
            {
                if (Go(CampUse.DeerWatch, Sometimes, ThingHerd) && TryDeerWatch(acts, time, false) == null) return;
                if (season != Season.Spring && Go(CampUse.DeerCreep, 0.25f, ThingHerd) && TryDeerCreep(acts, time, false) == null) return; // rare: 1 day in 4; not while the fawns are small [est]
            }
            // B39.a: a kid holds a kit (spring, rare: 1 spring day in 3)
            if (season == Season.Spring && Go(CampUse.KitHold, 0.33f, ThingMeadow) && TryKit(acts, time, false) == null) return;
        }

        readonly HashSet<long> feedAm = new(), feedPm = new();

        // ================================================================== starting each use; null = started, else why not
        string TryFrog(IReadOnlyList<PersonActivity> acts, float time, bool force)
        {
            var pond = PondLife.Instance;
            if (pond == null) return "no pond life";
            var camp = meals.Fire;
            int frog = pond.ClaimFrog(camp, force ? 6f : BorrowReach, out var fat);
            if (frog < 0 && force)
            {   // the cheat: a frog sits at the pond's edge nearest the camp
                pond.FrogsNow();
                if (!PondEdge(camp, 6f, out var edge, out _)) return "no pond near the camp ('water breaks the surface now' first)";
                frog = pond.PlaceFrogAt(edge);
                if (frog < 0 || !pond.FrogAt(frog, out fat)) return "no frog free";
            }
            if (frog < 0) return "no frog on the bank";
            var at = new Vector2(fat.x, fat.z);
            var toWater = ToWaterAt(at);
            if (toWater.sqrMagnitude < 1e-4f) toWater = Dir2(camp, at);
            var kids = Pick(acts, at, a => IsKid(a), 2, force);
            if (kids.Count == 0) { pond.FreeFrog(frog); return "no kid free ('Two kids join the camp' first)"; }
            bool miss = force ? ForceFrogMiss : BeatHash.Pick(2, (int)today, kids[0], 6951) == 0; // 1 in 2
            var s = CampScenes4.FrogCatch(kids[0], kids.Count > 1 ? kids[1] : -1, At(kids[0]).Value, kids.Count > 1 ? At(kids[1]).Value : default, at, toWater, miss, time);
            Add(Thing(s, ThingFrogs));
            Track(s).frog = frog;
            doneToday.Add(CampUse.FrogCatch);
            return null;
        }

        string TryDeerWatch(IReadOnlyList<PersonActivity> acts, float time, bool force)
        {
            var dv = DeerView.Instance;
            if (dv == null) return "no deer view";
            var camp = meals.Fire;
            if (!dv.HerdNear(camp, force ? 6f : AnimalGeo.HerdReach, out var nearest, out _)) return "no deer grazing near the camp (" + dv.Status() + ")";
            var stand = nearest + Dir2(nearest, camp) * 1.0f;
            var g = Pick(acts, stand, a => IsGrown(a), 1, force);
            if (g.Count == 0) return "no grown-up free";
            var k = Pick(acts, stand, a => IsKid(a), 2, force, g);
            if (k.Count == 0 && !force) return "no kid free";
            var who = new List<int> { k.Count > 0 ? k[0] : g[0] };
            if (k.Count > 0) { who.Insert(0, g[0]); if (k.Count > 1) who.Add(k[1]); }
            // the grown-up stands beside a kid: put them second of three, first of two
            var at = new List<Vector2>(); foreach (int w in who) at.Add(At(w).Value);
            var s = CampScenes4.DeerWatch(who, at, g[0], nearest, camp, force ? 7f : BeatHash.Range(6f, 10f, (int)today, 4, 6952), time);
            Add(Thing(s, ThingHerd));
            doneToday.Add(CampUse.DeerWatch);
            return null;
        }

        string TryDeerCreep(IReadOnlyList<PersonActivity> acts, float time, bool force)
        {
            var dv = DeerView.Instance;
            if (dv == null) return "no deer view";
            var camp = meals.Fire;
            if (!dv.HerdNear(camp, force ? 6f : AnimalGeo.HerdReach, out var nearest, out _)) return "no deer grazing near the camp (" + dv.Status() + ")";
            var line = nearest + Dir2(nearest, camp) * AnimalGeo.CreepFrom;
            var kids = Pick(acts, line, a => IsKid(a), 3, force);
            if (kids.Count < 2) return $"only {kids.Count} kid(s) free ('Two kids join the camp' first)";
            int grown = Nearest(acts, line, 3f, a => IsGrown(a), kids, false);
            var at = new List<Vector2>(); foreach (int k in kids) at.Add(At(k).Value);
            bool twig = BeatHash.Pick(2, (int)today, kids[0], 6953) == 0;
            var s = CampScenes4.DeerCreep(kids, at, grown, grown >= 0 ? At(grown).Value : camp, nearest, camp, twig, time);
            Add(Thing(s, ThingHerd));
            doneToday.Add(CampUse.DeerCreep);
            return null;
        }

        string TryKit(IReadOnlyList<PersonActivity> acts, float time, bool force)
        {
            var rv = RabbitView.Instance;
            if (rv == null) return "no rabbit view";
            var camp = meals.Fire;
            int id = rv.NearestShown(camp.x, camp.y);
            if (id < 0 || !rv.TryGetPosition(id, out var rp)) return "no rabbits shown";
            var mother = new Vector2(rp.x, rp.z);
            if (!force && (mother - camp).magnitude > 3f) return "the rabbits are far from the camp";
            if (!force && HunterNear(acts, mother, 1f)) return "a hunter is near the rabbits";
            var spot = mother + Dir2(mother, camp) * 0.35f;
            if (!Terrain.InBox(spot.x, spot.y, Terrain.GlassMargin + 0.1f)) spot = mother + Dir2(mother, TowardCamera(mother)) * 0.35f;
            var kids = Pick(acts, spot, a => IsKid(a), 2, force);
            if (kids.Count == 0) return "no kid free ('Two kids join the camp' first)";
            var s = CampScenes4.KitHold(kids[0], kids.Count > 1 ? kids[1] : -1, At(kids[0]).Value, kids.Count > 1 ? At(kids[1]).Value : default, spot, mother, time);
            Add(Thing(s, ThingMeadow));
            doneToday.Add(CampUse.KitHold);
            return null;
        }

        string TryFeedDucks(IReadOnlyList<PersonActivity> acts, float time, bool force)
        {
            var pond = PondLife.Instance;
            if (pond == null) return "no pond life";
            var camp = meals.Fire;
            if (!PondEdge(camp, force ? 6f : BorrowReach, out var bank, out var toWater)) return "no pond near the camp ('water breaks the surface now' first)";
            if (pond.DucksOnWater == 0 && !force) return "no ducks on the pond";
            var kids = Pick(acts, bank, a => IsKid(a), 3, force);
            if (kids.Count < (force ? 1 : 2)) return $"only {kids.Count} kid(s) free ('Two kids join the camp' first)";
            var at = new List<Vector2>(); foreach (int k in kids) at.Add(At(k).Value);
            var s = CampScenes4.FeedDucks(kids, at, bank, toWater, time);
            s.p0 = bank; s.p1 = toWater;
            Add(Thing(s, ThingDucks));
            doneToday.Add(CampUse.FeedDucks);
            return null;
        }

        // ================================================================== the animals' moments (each frame)
        void StepAnimalMoments(float time)
        {
            var pond = PondLife.Instance; var dv = DeerView.Instance;
            foreach (var s in scenes)
            {
                if (!IsAnimalUse(s.use)) continue;
                var tr = Track(s);
                float u = time - s.t0;
                switch (s.use)
                {
                    case CampUse.FrogCatch:
                        if (!tr.hopped && s.b > 0f && u >= s.b) { pond?.FrogHopIn(tr.frog); tr.hopped = true; }
                        break;
                    case CampUse.DeerWatch:
                        if (!tr.looked && u >= s.a && dv != null) { tr.looked = dv.LookUpOne(s.focus) >= 0; if (tr.looked) DeerLooks++; else tr.looked = true; }
                        break;
                    case CampUse.DeerCreep:
                        if (!tr.looked && u >= s.c && dv != null) { tr.looked = true; if (dv.LookUpOne(s.focus) >= 0) DeerLooks++; }
                        if (!tr.bolted && u >= s.a && dv != null)
                        {
                            tr.bolted = true;
                            var k = s.Count > 0 ? pv.DrawnAt(s.keys[0]) : null;
                            var at = k ?? s.focus;
                            dv.StopGrazeHold();
                            if (dv.KnockAtGround(at.x, at.y) > 0) DeerBolts++;
                        }
                        break;
                    case CampUse.FeedDucks:
                        if (pond != null && u >= 0f && u < s.b) pond.Lure(s.p0, s.p1, u >= s.a, !tr.looked);
                        tr.looked = true;
                        break;
                }
            }
            // scenes gone: a frog nobody let go is its own again
            List<CampScene> gone = null;
            foreach (var kv in animalTracks) if (!scenes.Contains(kv.Key)) (gone ??= new List<CampScene>()).Add(kv.Key);
            if (gone != null) foreach (var s in gone) { var tr = animalTracks[s]; if (tr.frog >= 0 && !tr.hopped) pond?.FreeFrog(tr.frog); animalTracks.Remove(s); }
        }

        // ================================================================== after the dolls: the frog in the hands, the kit, the crumbs
        Vector3 HandsMid(PegDollView v)
        {
            var r = v.armR.TransformPoint(new Vector3(0f, -PegDoll.ArmLen * v.scale, 0f));
            var l = v.armL.TransformPoint(new Vector3(0f, -PegDoll.ArmLen * v.scale, 0f));
            return Vector3.Lerp(r, l, 0.5f);
        }

        void DrawAnimals(Transform root, Material material, float time, System.Func<float, float, float> groundAt)
        {
            var box = LandBox;
            var pond = PondLife.Instance;
            CampScene kitScene = null;
            int crumbs = 0;
            foreach (var s in scenes)
            {
                if (!IsAnimalUse(s.use)) continue;
                float u = time - s.t0;
                if (s.use == CampUse.FrogCatch && pond != null && box != null && animalTracks.TryGetValue(s, out var tr) && tr.frog >= 0 && !tr.hopped && s.a >= 0f && u >= s.a && u < s.b)
                {
                    var v = pv.ViewOf(s.keys[0]);
                    if (v != null && v.gameObject.activeInHierarchy)
                    {
                        var local = box.Root.InverseTransformPoint(HandsMid(v));
                        float wriggle = u >= s.c ? 15f * Mathf.Sin((u - s.c) * 8f * Mathf.PI * 2f) : 0f; // the wriggle (Wobble 15°, 8 Hz)
                        pond.PoseFrog(tr.frog, local - Vector3.up * 0.012f, v.transform.eulerAngles.y + 180f, wriggle);
                        FrogHeldFrames++;
                    }
                }
                if (s.use == CampUse.KitHold) kitScene = s;
                foreach (var a in s.arcs)
                {
                    float k = (u - a.from) / Mathf.Max(0.01f, a.seconds);
                    if (k < 0f || k > 1f || a.member >= s.Count) continue;
                    var v = pv.ViewOf(s.keys[a.member]);
                    if (v == null || !v.gameObject.activeInHierarchy) continue;
                    var from = root.InverseTransformPoint(v.armR.TransformPoint(new Vector3(0f, -PegDoll.ArmLen * v.scale, 0f)));
                    float ground = pond != null && box != null && box.layout.eco.pondCells > 0 ? box.layout.eco.waterY : groundAt(a.to.x, a.to.z);
                    var to = new Vector3(a.to.x, ground + a.to.y, a.to.z);
                    var p = Vector3.Lerp(from, to, k) + Vector3.up * (4f * a.h * k * (1f - k));
                    var t = Crumb(root, material, crumbs++);
                    t.localPosition = p; t.localScale = Vector3.one * 0.014f; // [est] big enough to see in flight
                    CrumbFrames++;
                }
            }
            for (int i = crumbs; i < crumbPool.Count; i++) if (crumbPool[i] != null) crumbPool[i].gameObject.SetActive(false);
            DrawKit(kitScene, time, groundAt);
        }

        Transform Crumb(Transform root, Material material, int i)
        {
            while (i < crumbPool.Count && crumbPool[i] == null) crumbPool.RemoveAt(i);
            while (crumbPool.Count <= i)
            {
                var go = new GameObject("Camp crumb") { hideFlags = HideFlags.DontSave };
                go.transform.SetParent(root, false);
                if (crumbMesh == null) { var mb = new MeshBuilder(6954); mb.Ico(Vector3.zero, 1f, new Vector3(1f, 0.8f, 0.9f), new Color(0.86f, 0.72f, 0.46f), 0.06f); crumbMesh = mb.ToMesh("Camp_Crumb"); crumbMesh.hideFlags = HideFlags.DontSave; }
                go.AddComponent<MeshFilter>().sharedMesh = crumbMesh;
                var r = go.AddComponent<MeshRenderer>(); r.sharedMaterial = material; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                crumbPool.Add(go.transform);
            }
            var t = crumbPool[i];
            t.gameObject.SetActive(true);
            return t;
        }

        void DrawKit(CampScene s, float time, System.Func<float, float, float> groundAt)
        {
            var box = LandBox;
            if (s == null || box == null || box.Root == null || !CampScenes4.KitAt(s, time - s.t0, out var at, out bool held, out bool hopping, out float phase))
            {
                if (kitDoll != null) kitDoll.gameObject.SetActive(false);
                return;
            }
            if (kitDoll == null || kitParent != box.Root)
            {
                if (kitDoll != null) Object.Destroy(kitDoll.gameObject);
                kitDoll = RabbitDoll.Create(box, box.Root, 1);
                kitDoll.name = "Camp rabbit kit";
                kitParent = box.Root;
            }
            kitDoll.gameObject.SetActive(true);
            float u = time - s.t0;
            if (held && s.Count > 0 && pv.ViewOf(s.keys[0]) is PegDollView v && v.gameObject.activeInHierarchy)
            {
                var local = box.Root.InverseTransformPoint(HandsMid(v));
                kitDoll.transform.localPosition = local - Vector3.up * 0.03f;
                kitDoll.Ground(Vector3.up);
                kitDoll.Face(v.transform.eulerAngles.y + 90f, Time.deltaTime, true); // across the chest
                kitDoll.Show(RabbitAct.SitUp, 0f, time, AnimalGeo.KitScale);
            }
            else
            {
                var to = u < s.a ? s.p1 : s.p0;
                var yaw = RabbitDoll.YawOf(to.x - at.x, to.y - at.y);
                kitDoll.transform.localPosition = new Vector3(at.x, groundAt(at.x, at.y) + (hopping ? 0.03f * Mathf.Sin(phase * Mathf.PI) : 0f), at.y);
                kitDoll.Ground(Vector3.up);
                kitDoll.Face(yaw, Time.deltaTime);
                kitDoll.Show(hopping ? RabbitAct.Hop : RabbitAct.SitUp, phase, time, AnimalGeo.KitScale);
            }
            KitFrames++;
        }

        void DestroyAnimals()
        {
            if (kitDoll != null) Object.Destroy(kitDoll.gameObject);
            kitDoll = null;
            foreach (var t in crumbPool) if (t != null) Object.Destroy(t.gameObject);
            crumbPool.Clear();
        }

        // ================================================================== cheats
        string ForceAnimal(CampUse use)
        {
            if (sim == null || planner == null || meals == null) return "no camp yet";
            if (!meals.HasFire) return "no fire in the camp";
            var acts = pv.Activities; float time = clock;
            switch (use)
            {
                case CampUse.FrogCatch: { var r = TryFrog(acts, time, true) ?? (ForceFrogMiss ? "kids catch a frog (it gets away)" : "kids catch a frog"); ForceFrogMiss = !ForceFrogMiss; return r; }
                case CampUse.DeerWatch: return EnsureHerd() ?? TryDeerWatch(acts, time, true) ?? "watch the deer";
                case CampUse.DeerCreep: return EnsureHerd() ?? TryDeerCreep(acts, time, true) ?? "kids creep up on the deer";
                case CampUse.KitHold: return EnsureRabbits() ?? TryKit(acts, time, true) ?? "a kid holds a rabbit kit";
                case CampUse.FeedDucks: return EnsureDucks() ?? TryFeedDucks(acts, time, true) ?? "kids feed the ducks";
            }
            return "not an animal use: " + use;
        }

        /// <summary>The cheats' herd: deer placed if none, then grazing <see cref="HerdSide"/> to the camp's side (across the camera's view, so the
        /// watchers and the deer share the frame) for a minute.</summary>
        string EnsureHerd()
        {
            if (sim.EcoDeerCount == 0 && sim.DebugPlaceNearCamp(LifeSpecies.Deer) < 0) return "no ground for deer";
            var dv = DeerView.Instance;
            if (dv == null) return "no deer view";
            var camp = meals.Fire;
            var across = CameraAcross(); var back = -Dir2(camp, TowardCamera(camp));
            var eco = LandBox != null ? LandBox.layout.eco : null;
            Vector2 spot = camp + back * HerdSide; bool found = false;
            // #692 p4 (the clips: the herd at the side glass, then the watchers stacked along the view): behind the camp as the camera sees it, so the watchers stand side by side across the screen; well inside the box,
            // clear of the front glass and off the water
            foreach (var d in new[] { back, (across + back).normalized, (-across + back).normalized, across, -across })
            {
                var q = camp + d * HerdSide;
                if (!Terrain.InBox(q.x, q.y, 0.5f) || !ClearOfGlass(q) || eco != null && eco.WaterCell(SiteMap.CellAt(q.x, q.y))) continue;
                spot = q; found = true; break;
            }
            if (!found) spot = new Vector2(Mathf.Clamp(spot.x, -Terrain.HX + 0.7f, Terrain.HX - 0.7f), Terrain.ClampZ(spot.y, 0.7f));
            dv.DebugGrazeAt(spot, 60f);
            return null;
        }
        /// <summary>How far to the side of the fire the cheats' herd grazes [est].</summary>
        public const float HerdSide = 2.0f;

        string EnsureRabbits()
        {
            if (sim.EcoRabbitsShown() == 0 && sim.DebugPlaceNearCamp(LifeSpecies.Rabbits) < 0) return "no ground for rabbits";
            return RabbitView.Instance != null && RabbitView.Instance.Shown > 0 ? null : "rabbits placed: press again in a moment";
        }

        string EnsureDucks()
        {
            var pond = PondLife.Instance;
            if (pond == null) return "no pond life";
            if (sim.EcoDucksShown() == 0) { sim.DebugJarReady(LifeSpecies.Ducks); sim.DebugPlaceNearCamp(LifeSpecies.Ducks); }
            if (pond.DucksOnWater == 0) { pond.DucksNow(); return "ducks called: press again in a moment"; }
            return null;
        }

        /// <summary>The cheats' animals at once: deer grazing at the camp's side, rabbits placed.</summary>
        public string AnimalsReady()
        {
            if (sim == null || meals == null || !meals.HasFire) return "no camp yet";
            return (EnsureHerd() ?? $"deer {DeerView.Instance?.Shown ?? 0} grazing by the camp") + "; " + (EnsureRabbits() ?? $"rabbits {RabbitView.Instance?.Shown ?? 0}");
        }

        /// <summary>Tests and the readout.</summary>
        public string AnimalMarks() => $"frog held {FrogHeldFrames}, kit {KitFrames}, crumbs {CrumbFrames}, deer looks {DeerLooks}, bolts {DeerBolts}, ducks lured {(PondLife.Instance != null ? PondLife.Instance.LuredDucks : 0)}";
    }
}

namespace AquariumCiv.View
{
    /// <summary>#692 part 4 cheats (SliceDebugPanel's "#692" block).</summary>
    public static partial class CampCheats
    {
        public static string FrogCatch() => Do(CampUse.FrogCatch);
        public static string DeerWatch() => Do(CampUse.DeerWatch);
        public static string DeerCreep() => Do(CampUse.DeerCreep);
        public static string KitHold() => Do(CampUse.KitHold);
        public static string FeedDucks() => Do(CampUse.FeedDucks);
        public static string AnimalMarks() { var c = Camp; return c == null ? "no PeopleView" : c.AnimalMarks(); }
        /// <summary>AnimLab and the tests: a pond, ducks, frogs, deer grazing at the camp's side and rabbits (call twice, a moment apart).</summary>
        public static string AnimalsReady()
        {
            var c = Camp;
            if (c == null) return "no PeopleView";
            return PondReady() + "; " + c.AnimalsReady();
        }

        /// <summary>AnimLab and the tests: a pond by the camp (the water table raised if there is none), the ducks placed, the frogs out.</summary>
        public static string PondReady()
        {
            var sim = AquariumCiv.Game.GameClock.Instance != null ? AquariumCiv.Game.GameClock.Instance.Sim : null;
            if (sim == null || !sim.EcologyOn) return "living land off";
            string r = sim.EcoPondCells > 0 ? $"pond {sim.EcoPondCells} cells" : EcoDebug.Table(0.3f);
            if (sim.EcoPondCells > 0 && sim.EcoDucksShown() == 0) { sim.DebugJarReady(LifeSpecies.Ducks); r += $"; ducks at {sim.DebugPlaceNearCamp(LifeSpecies.Ducks)}"; }
            r += "; " + EcoDebug.FrogsNow();
            return r;
        }
    }
}
