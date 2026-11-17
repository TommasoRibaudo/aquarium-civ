// #693 (docs/design/animation-specs.md "#693: the choice props", B54.a-zz): CampLife's side of the things the choices leave. Each thing gets a
// work use and a life use: when each starts (the D rules: life uses borrow free time only; work uses borrow a free grown-up of the right
// kind [est: they don't take a block of the day plan, so no plan starves]; how often by the view day's roll; one use per thing at a time),
// the cheats ("#693" in SliceDebugPanel), what happens to the thing (a basket taken, the axe out, a new row of names: ConsequencePropsView's
// looks), and what the uses draw beside it: the carried basket and log, the log split on the stump, the ladder at the roof bird, flour in flight and
// on the kids. Unlike the camp's uses these play in every era (the village's things: the staff, the flour, the roof bird, the long table,
// the offering, the fish crate). View-only and not saved [est] (a reload clears a tipped bird, the floury kids, the rows cut this session).
// No goat pen: #714 made the pond family's goat pen their boat (B77), so B54.v/w and the goat have no thing to stand at.
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
        const int ThingPropBase = 60000;
        static bool IsPropUse(CampUse u) => u >= CampUse.BasketTake && u <= CampUse.PenPet; // #694: the dogs' uses come after

        /// <summary>A choice's thing as drawn now: its id, kind, spot (box x, z), yaw and transform.</summary>
        struct PropSpot { public int id; public string kind; public Vector2 at; public float yaw; public Transform t; public int personId; public bool onTree; }
        readonly List<PropSpot> propSpots = new();
        int propFrame = -1;
        /// <summary>#693 notes: logs laid on each woodpile (0-3, ConsequenceMeshes' slots), scratches and the re-cut day of each name post, and
        /// the sacks set down at doors (view-only [est]).</summary>
        readonly Dictionary<int, int> woodStacked = new(), postScratches = new();
        readonly Dictionary<int, long> postRecut = new();
        readonly List<(Vector2 at, float yaw)> sacksDown = new();
        readonly Dictionary<CampScene, (Vector2 foot, Vector2 pole)> ladders = new();
        readonly Dictionary<CampScene, (Vector2 stump, Vector2 across)> splits = new();
        readonly Dictionary<int, float> floured = new();
        readonly Dictionary<int, int> nameSeen = new(), nameCut = new();
        readonly HashSet<int> birdTipped = new();
        long propDay = long.MinValue, offeredDay = long.MinValue, bigPotDay = long.MinValue, birdFixedDay = long.MinValue, retieYear = long.MinValue;
        int nameExtra;
        readonly List<Transform> propPool = new();
        Mesh[] oneBaskets; Mesh logMesh, halfMesh, ladderMesh, flourMesh, carryLog, staffMesh, fishMesh, charmMesh, oneSack, blockMesh, ladleMesh;
        /// <summary>The cheats alternate: a log stacked, then one fetched for the fire (B54.c); rain, then a dry day's sit (B54.d).</summary>
        public static bool ForceFetch, ForceDry;

        void ResetProps()
        {
            woodStacked.Clear(); postScratches.Clear(); postRecut.Clear(); sacksDown.Clear(); ladders.Clear(); splits.Clear(); floured.Clear(); nameSeen.Clear(); nameCut.Clear(); birdTipped.Clear();
            propDay = offeredDay = bigPotDay = birdFixedDay = retieYear = long.MinValue; nameExtra = 0; propFrame = -1;
            ResetGoat();
        }

        // ================================================================== the things
        List<PropSpot> Props()
        {
            if (propFrame == Time.frameCount && propSpots.Count > 0) return propSpots;
            propFrame = Time.frameCount; propSpots.Clear();
            var cv = ConsequencePropsView.Instance;
            if (sim == null || cv == null) return propSpots;
            var standing = cv.Standing;
            foreach (var p in sim.ConsequenceProps)
                if (p.placed && standing.TryGetValue(p.id, out var t) && t != null && t.gameObject.activeInHierarchy)
                    propSpots.Add(new PropSpot { id = p.id, kind = p.kind, at = new Vector2(t.localPosition.x, t.localPosition.z), yaw = p.yaw, t = t, personId = p.personId, onTree = p.anchor == PropAnchor.Tree && p.treeCell >= 0 });
            return propSpots;
        }

        bool PropOf(string kind, out PropSpot spot)
        {
            spot = default;
            foreach (var p in Props()) if (p.kind == kind) { spot = p; return true; }
            return false;
        }

        static int ThingOf(in PropSpot p) => ThingPropBase + p.id;
        /// <summary>A direction in the thing's own frame (its yaw), on the ground.</summary>
        static Vector2 Local(in PropSpot p, float lx, float lz) { var w = Quaternion.Euler(0f, p.yaw, 0f) * new Vector3(lx, 0f, lz); return new Vector2(w.x, w.z); }
        /// <summary>Of the thing's two ends along local <paramref name="alongX"/> x (else z), the one nearer the camera.</summary>
        Vector2 FaceToCam(in PropSpot p, bool alongX) { var d = alongX ? Local(p, 1f, 0f) : Local(p, 0f, 1f); return Vector2.Dot(d, ToCam(p.at)) >= 0f ? d.normalized : -d.normalized; }

        List<int> Kids(IReadOnlyList<PersonActivity> acts, Vector2 near, int max, bool force, ICollection<int> not = null) => Pick(acts, near, a => IsKid(a), max, force, not);
        List<Vector2> AtAll(List<int> keys) { var r = new List<Vector2>(); foreach (int k in keys) r.Add(At(k).Value); return r; }

        string Start(CampScene s, in PropSpot p, CampUse use)
        {
            Add(Thing(s, ThingOf(p)));
            doneToday.Add(use);
            return null;
        }

        /// <summary>A drawn, free grown-up with this job (the cheat: not free too), else −1.</summary>
        int WithJob(IReadOnlyList<PersonActivity> acts, string job, bool force)
        {
            foreach (var n in sim.State.notables)
                if (n.alive && n.job == job && Shown(acts, n.id) && (force || FreeKey(acts, n.id))) return n.id;
            return -1;
        }

        int DeathsSince(int propId)
        {
            var p = sim.FindConsequenceProp(propId);
            if (p == null) return 0;
            int n = 0; foreach (var x in sim.State.notables) if (!x.alive && x.diedMonth > p.dueMonth) n++;
            return n;
        }

        // ================================================================== each frame (every era)
        void StepProps(IReadOnlyList<PersonActivity> acts, float time)
        {
            if (now.day != propDay)
            {
                propDay = now.day;
                foreach (var p in Props()) if (p.kind == ConsequenceKind.FishCrate) ConsequencePropsView.SetLook(p.id, 0); // [est] the family's catch comes each morning
            }
            var drop = new List<int>(); foreach (var kv in floured) if (ViewHours > kv.Value) drop.Add(kv.Key); foreach (int k in drop) floured.Remove(k); // the evening wash
            foreach (var p in Props())
            {
                // a look a use left half-way (its scene cut short) goes back once nothing plays at the thing
                if (!ThingBusy(ThingOf(p)))
                {
                    int look = ConsequencePropsView.LookOf(p.id);
                    if (look != 0 && (p.kind == ConsequenceKind.Baskets || p.kind == ConsequenceKind.AxeStump || p.kind == ConsequenceKind.Staff)) ConsequencePropsView.SetLook(p.id, 0);
                    if (p.kind == ConsequenceKind.OfferingBasket) ConsequencePropsView.SetLook(p.id, offeredDay == now.day ? 1 : 0);
                }
                if (p.kind == ConsequenceKind.NamePeg && !ThingBusy(ThingOf(p))) ConsequencePropsView.SetLook(p.id, PostLook(p.id)); // the fresh re-cut fades the next day
                if (p.kind == ConsequenceKind.NameStone && !nameSeen.ContainsKey(p.id)) { int d = DeathsSince(p.id); nameSeen[p.id] = d; nameCut[p.id] = Mathf.Min(2, d); ConsequencePropsView.SetLook(p.id, nameCut[p.id]); } // [est] the rows already cut for the dead: at most 2 more, so the next one shows
                // B54.p: a storm tips the bird over in the night
                if (p.kind == ConsequenceKind.RoofBird && !birdTipped.Contains(p.id) && birdFixedDay != now.day && planner != null && planner.WeatherOn(now.day - 1) == Weather.Storm && Hour < 12f)
                { birdTipped.Add(p.id); ConsequencePropsView.Tip(p.id, 20f); }
            }
            StepGoat(acts, time); // the goat pen's look and uses
            StepKnock(time); // B54.u: the Keeper knocks while kids wait by the offering
            // B54.h: the together supper at the big pot on a "one big pot" night
            if (meals != null)
                meals.BigPotAt = (bigPotDay == now.day || sim.SupperOn(now.day) == SupperPick.Pot) && sim.State.era == Era.Camp && PropOf(ConsequenceKind.StewPot, out var pot) ? pot.at : (Vector2?)null;
            if (OnlyCheats || meals == null) return;
            if (meals.Eaters > 0) return; // never during a meal (the big pot's supper is the meal's own bowl round)
            float h = Hour;
            var season = LifeClock.SeasonOf(now.day);
            var weather = planner.WeatherOn(now.day);
            bool wet = weather is Weather.KeeperRain or Weather.Storm, day = h >= 9f && h < 16.5f;
            bool Go(CampUse u, float chance, string kind, out PropSpot p)
            {
                p = default;
                if (doneToday.Contains(u) || Roll(u) >= chance || !LifeOk(u) || !PropOf(kind, out p) || ThingBusy(ThingOf(p))) return false;
                if (triedAt.TryGetValue(u, out float tr) && clock - tr < 3f && clock >= tr) return false;
                triedAt[u] = clock;
                return true;
            }
            PropSpot q;
            // the work uses
            if (h >= 6.3f && h < 8.5f && season is Season.Summer or Season.Autumn && Go(CampUse.BasketTake, Often, ConsequenceKind.Baskets, out q) && TryBasketTake(acts, time, false, q) == null) return;
            if (h >= 7f && h < 10f && PropOf(ConsequenceKind.NameStone, out var ns) && DeathsSince(ns.id) > nameSeen[ns.id] && Go(CampUse.NameCut, 1f, ConsequenceKind.NameStone, out q) && TryNameCut(acts, time, false, q) == null) return;
            if (h >= 7f && h < 11f && birdTipped.Count > 0 && Go(CampUse.BirdStraighten, 1f, ConsequenceKind.RoofBird, out q) && TryBird(acts, time, false, q) == null) return;
            if (h >= 7f && h < 10f && season == Season.Spring && retieYear != now.day / SimConfig.MonthsPerYear && Go(CampUse.PostCarve, Rare, ConsequenceKind.NamePeg, out q) && TryPostCarve(acts, time, false, q) == null) return;
            if (h >= 8f && h < 11f && season == Season.Spring && Go(CampUse.PostMeasure, Sometimes, ConsequenceKind.NamePeg, out q) && TryPostMeasure(acts, time, false, q) == null) return; // [est] the kids' heights, on spring mornings
            if (h >= 8f && h < 10f && sim.FestivalOn && Go(CampUse.OfferingFill, 1f, ConsequenceKind.OfferingBasket, out q) && TryOffer(acts, time, false, q) == null) return;
            if (h >= 8.5f && h < 10.5f && Go(CampUse.StaffWalk, Often, ConsequenceKind.Staff, out q) && TryStaffWalk(acts, time, false, q) == null) return;
            if (h >= 17.4f && h < 18.6f && sim.FestivalOn && season == Season.Autumn && Go(CampUse.TableMeal, Often, ConsequenceKind.LongTable, out q) && TryTableMeal(acts, time, false, q) == null) return;
            if (h >= 16.8f && h < 17.6f && meals.BigPotAt != null && Go(CampUse.BigPot, 1f, ConsequenceKind.StewPot, out q) && TryBigPot(acts, time, false, q) == null) return;
            if (!day) return;
            if (wet && Go(CampUse.WoodShelter, Often, ConsequenceKind.WoodpileRoof, out q) && TryShelter(acts, time, false, q, true) == null) return;
            if (Go(CampUse.WoodStack, Often, ConsequenceKind.WoodpileRoof, out q) && TryWoodStack(acts, time, false, q, wet) == null) return;
            if (!wet && Go(CampUse.StumpSplit, Often, ConsequenceKind.AxeStump, out q) && TryStumpSplit(acts, time, false, q) == null) return;
            if (!wet && Go(CampUse.FlourCarry, Often, ConsequenceKind.FlourSacks, out q) && TryFlourCarry(acts, time, false, q) == null) return;
            if (Go(CampUse.CrateFish, Often, ConsequenceKind.FishCrate, out q) && ConsequencePropsView.LookOf(q.id) < 3 && TryCrateFish(acts, time, false, q) == null) return;
            if (wet) return;
            // the life uses (kids at play, idle grown-ups)
            if (Go(CampUse.WoodShelter, Sometimes, ConsequenceKind.WoodpileRoof, out q) && TryShelter(acts, time, false, q, false) == null) return;
            if (Go(CampUse.NameTrace, Sometimes, ConsequenceKind.NameStone, out q) && TryNameTrace(acts, time, false, q) == null) return;
            if (Go(CampUse.CharmFlick, Sometimes, ConsequenceKind.CharmPole, out q) && TryCharmFlick(acts, time, false, q) == null) return;
            if ((season == Season.Winter || weather == Weather.Snow) && Go(CampUse.PotDrum, Sometimes, ConsequenceKind.StewPot, out q) && TryPotDrum(acts, time, false, q) == null) return;
            if (Go(CampUse.StumpSit, Sometimes, ConsequenceKind.AxeStump, out q) && TryStumpSit(acts, time, false, q) == null) return;
            if (Go(CampUse.StaffPlay, Sometimes, ConsequenceKind.Staff, out q) && TryStaffPlay(acts, time, false, q) == null) return;
            if (Go(CampUse.FlourFight, VeryRare * 0.25f, ConsequenceKind.FlourSacks, out q) && TryFlourFight(acts, time, false, q) == null) return; // rare: about once a game year [est]
            if (Go(CampUse.BirdFly, Sometimes, ConsequenceKind.RoofBird, out q) && TryBirdFly(acts, time, false, q) == null) return;
            if (Go(CampUse.TableUnder, Sometimes, ConsequenceKind.LongTable, out q) && TryTableUnder(acts, time, false, q) == null) return;
            if (h >= 13f && offeredDay == now.day && Go(CampUse.OfferingWait, Sometimes, ConsequenceKind.OfferingBasket, out q) && TryOfferWait(acts, time, false, q) == null) return;
            if (Go(CampUse.CrateWave, Sometimes, ConsequenceKind.FishCrate, out q) && TryCrateWave(acts, time, false, q) == null) return;
        }

        // ================================================================== starting each use (natural and forced); null = started, else why not
        string TryBasketTake(IReadOnlyList<PersonActivity> acts, float time, bool force, in PropSpot p)
        {
            int who = Grown1(acts, p.at, force);
            if (who < 0) return "no grown-up free";
            var toCam = ToCam(p.at);
            var bush = p.at + toCam * 0.45f + CameraAcross() * 0.35f; // [est] the pick is cut short and played on the camera's side (a real bush was often behind the camp's things)
            return Start(CampScenes6.BasketTake(who, At(who).Value, p.at, bush, toCam, time), p, CampUse.BasketTake);
        }

        string TryWoodStack(IReadOnlyList<PersonActivity> acts, float time, bool force, in PropSpot p, bool fetch)
        {
            int who = Grown1(acts, p.at, force);
            if (who < 0) return "no grown-up free";
            var face = FaceToCam(p, false); // the logs' front (they lie along the pile's z)
            var fire = meals.HasFire ? meals.Fire : p.at + face * 0.8f;
            int had = woodStacked.TryGetValue(p.id, out int n0) ? n0 : 0;
            if (!fetch && had >= ConsequenceMeshes.WoodSlots) fetch = true; // [est] a full pile: today one is fetched instead
            var s = CampScenes6.WoodStack(who, At(who).Value, p.at, face, fetch, fire, time);
            s.b = p.yaw; s.kind = fetch ? 1 : 0; s.p0 = new Vector2(had, 0f); // the pile's yaw and the slot the log goes to
            return Start(s, p, CampUse.WoodStack);
        }

        string TryShelter(IReadOnlyList<PersonActivity> acts, float time, bool force, in PropSpot p, bool rain)
        {
            var who = rain ? Pick(acts, p.at, a => IsGrown(a) || IsKid(a), 3, force) : Kids(acts, p.at, 2, force);
            if (who.Count < (rain ? 2 : 1)) return rain ? "two people needed near the woodpile" : "no kid free ('Two kids join the camp' first)";
            var dir = FaceToCam(p, false); // pressed to the logs' front under the eave (rain), or on the low logs (dry)
            return Start(CampScenes6.WoodShelter(who, AtAll(who), p.at, dir, rain, time), p, CampUse.WoodShelter);
        }

        string TryNameCut(IReadOnlyList<PersonActivity> acts, float time, bool force, in PropSpot p)
        {
            int who = WithJob(acts, JobType.Toolmaker, force);
            if (who < 0) who = Grown1(acts, p.at, force);
            if (who < 0) return "no grown-up free";
            if (!force) nameSeen[p.id] = nameSeen.TryGetValue(p.id, out int seen) ? seen + 1 : 1;
            return Start(CampScenes6.NameCut(who, At(who).Value, p.at, FaceToCam(p, false), time), p, CampUse.NameCut); // #693 notes: off the slab's face, by its footprint
        }

        string TryNameTrace(IReadOnlyList<PersonActivity> acts, float time, bool force, in PropSpot p)
        {
            int kid = Kid1(acts, p.at, force);
            if (kid < 0) return "no kid free ('Two kids join the camp' first)";
            int g = Grown1(acts, p.at, force, new[] { kid });
            return Start(CampScenes6.NameTrace(kid, At(kid).Value, g, g >= 0 ? At(g).Value : default, p.at, FaceToCam(p, false), time), p, CampUse.NameTrace);
        }

        string TryCharmFlick(IReadOnlyList<PersonActivity> acts, float time, bool force, in PropSpot p)
        {
            int kid = Kid1(acts, p.at, force);
            if (kid < 0) return "no kid free ('Two kids join the camp' first)";
            var toCam = ToCam(p.at);
            // under a charm on the camera's side: on a tree, out at the ring the charms hang on (#635 CharmTie); on its own pole, under the bough
            Vector2 under;
            if (p.onTree)
            {
                bool keeper = sim.State.eco.treeCell >= 0 && sim.FindConsequenceProp(p.id)?.treeCell == sim.State.eco.treeCell;
                ConsequenceMeshes.CharmTie(keeper, 0.16f, out _, out float ring);
                under = p.at + toCam * ring;
            }
            else under = p.at + Local(p, 0.08f * ConsequenceMeshes.Scale, 0f) + toCam * 0.02f;
            return Start(CampScenes6.CharmFlick(kid, At(kid).Value, under, toCam, force ? 2 : 1, time), p, CampUse.CharmFlick);
        }

        string TryPotDrum(IReadOnlyList<PersonActivity> acts, float time, bool force, in PropSpot p)
        {
            var kids = Kids(acts, p.at, 2, force);
            if (kids.Count == 0) return "no kid free ('Two kids join the camp' first)";
            int g = Grown1(acts, p.at, force, kids);
            return Start(CampScenes6.PotDrum(kids, AtAll(kids), g, g >= 0 ? At(g).Value : default, p.at, ToCam(p.at), 3.5f, time), p, CampUse.PotDrum);
        }

        string TryBigPot(IReadOnlyList<PersonActivity> acts, float time, bool force, in PropSpot p)
        {
            int cook = meals.CookAtFire >= 0 && (force || FreeKey(acts, meals.CookAtFire)) ? meals.CookAtFire : Grown1(acts, p.at, force);
            if (cook < 0) return "no cook free";
            return Start(CampScenes6.BigPotStir(cook, At(cook).Value, p.at, ToCam(p.at), time), p, CampUse.BigPot);
        }

        string TryStumpSplit(IReadOnlyList<PersonActivity> acts, float time, bool force, in PropSpot p)
        {
            int who = Grown1(acts, p.at, force);
            if (who < 0) return "no grown-up free";
            var toCam = ToCam(p.at);
            var s = CampScenes6.StumpSplit(who, At(who).Value, p.at, toCam, time);
            splits[s] = (p.at, new Vector2(-toCam.y, toCam.x));
            return Start(s, p, CampUse.StumpSplit);
        }

        string TryStumpSit(IReadOnlyList<PersonActivity> acts, float time, bool force, in PropSpot p)
        {
            var kids = Kids(acts, p.at, 2, force);
            if (kids.Count == 0) return "no kid free ('Two kids join the camp' first)";
            return Start(CampScenes6.StumpSit(kids[0], At(kids[0]).Value, kids.Count > 1 ? kids[1] : -1, kids.Count > 1 ? At(kids[1]).Value : default, p.at, ToCam(p.at), 6f, time), p, CampUse.StumpSit);
        }

        /// <summary>The era's leader if drawn and free (the cheat: any grown-up nearest the thing).</summary>
        int Leader(IReadOnlyList<PersonActivity> acts, Vector2 near, bool force, ICollection<int> not = null)
        {
            var n = sim.HolderOf(SimCore.LeaderOf(sim.State.era));
            if (n != null && n.alive && (not == null || !not.Contains(n.id)) && Shown(acts, n.id) && (force || FreeKey(acts, n.id))) return n.id;
            return force ? Grown1(acts, near, true, not) : -1;
        }

        string TryStaffWalk(IReadOnlyList<PersonActivity> acts, float time, bool force, in PropSpot p)
        {
            int who = Leader(acts, p.at, force);
            if (who < 0) return "the headman is not free";
            var toCam = ToCam(p.at); var acr = CameraAcross();
            var round = new List<Vector2>();
            var c = meals.HasFire ? meals.Fire : p.at;
            // [est] the walk round: three points on a ring round the green (the fire), on the camera's side first, the meeting at the fire
            foreach (float deg in new[] { -50f, 30f, 110f }) round.Add(c + CampScenes5.Rot(toCam, deg) * 0.55f);
            round.Add(c + toCam * 0.25f + acr * 0.12f);
            for (int i = 0; i < round.Count; i++) round[i] = new Vector2(Mathf.Clamp(round[i].x, -Terrain.HX + 0.5f, Terrain.HX - 0.5f), Terrain.ClampZ(round[i].y, 0.5f));
            return Start(CampScenes6.StaffWalk(who, At(who).Value, p.at, round, toCam, time), p, CampUse.StaffWalk);
        }

        string TryStaffPlay(IReadOnlyList<PersonActivity> acts, float time, bool force, in PropSpot p)
        {
            var kids = Kids(acts, p.at, 3, force);
            if (kids.Count < 2) return "two kids needed ('Two kids join the camp' first)";
            int head = Leader(acts, p.at, force, kids);
            var others = kids.GetRange(1, kids.Count - 1);
            return Start(CampScenes6.StaffPlay(kids[0], At(kids[0]).Value, others, AtAll(others), head, head >= 0 ? At(head).Value : default, p.at, ToCam(p.at), time), p, CampUse.StaffPlay);
        }

        /// <summary>B54.n: where the flour goes: the nearest home's door or the bakery's, else toward the fire [est].</summary>
        Vector2 FlourDoor(Vector2 from)
        {
            Vector2 best = meals.HasFire ? meals.Fire + Dir2(meals.Fire, from) * 0.3f : from + ToCam(from) * 0.8f; float bd = 3f;
            foreach (var b in sim.State.buildings)
            {
                if (b.lotId == 0 || b.type == BuildingType.Windmill) continue;
                if (b.type != BuildingType.Hut && b.type != BuildingType.Bakery && b.type != BuildingType.Camp) continue;
                var d = new Vector2(b.doorX, b.doorZ); if (d.sqrMagnitude < 1e-6f) d = new Vector2(b.x, b.z);
                float m = (d - from).magnitude;
                if (m < bd && m > 0.4f) { bd = m; best = d; }
            }
            return best;
        }

        string TryFlourCarry(IReadOnlyList<PersonActivity> acts, float time, bool force, in PropSpot p)
        {
            int who = Grown1(acts, p.at, force);
            if (who < 0) return "no grown-up free";
            return Start(CampScenes6.FlourCarry(who, At(who).Value, p.at, FlourDoor(p.at), ToCam(p.at), time), p, CampUse.FlourCarry);
        }

        string TryFlourFight(IReadOnlyList<PersonActivity> acts, float time, bool force, in PropSpot p)
        {
            var kids = Kids(acts, p.at, 4, force);
            if (kids.Count < (force ? 2 : 3)) return "three kids needed ('Two kids join the camp' first)";
            return Start(CampScenes6.FlourFight(kids, AtAll(kids), p.at, ToCam(p.at), time), p, CampUse.FlourFight);
        }

        string TryBird(IReadOnlyList<PersonActivity> acts, float time, bool force, in PropSpot p)
        {
            int who = Grown1(acts, p.at, force);
            if (who < 0) return "no grown-up free";
            var toCam = ToCam(p.at);
            var s = CampScenes6.BirdStraighten(who, At(who).Value, p.at, toCam, time);
            ladders[s] = (p.at + toCam * 0.07f + new Vector2(-toCam.y, toCam.x) * 0.03f, p.at);
            birdFixedDay = now.day;
            return Start(s, p, CampUse.BirdStraighten);
        }

        string TryBirdFly(IReadOnlyList<PersonActivity> acts, float time, bool force, in PropSpot p)
        {
            var kids = Kids(acts, p.at, 2, force);
            if (kids.Count == 0) return "no kid free ('Two kids join the camp' first)";
            return Start(CampScenes6.BirdFly(kids, AtAll(kids), p.at, ToCam(p.at), 2, time), p, CampUse.BirdFly);
        }

        /// <summary>B54.r: the long table's seats (both benches, facing across) and where its giver fetches the bowls.</summary>
        void TableSeats(in PropSpot p, int n, out List<Vector2> seats, out List<Vector2> faces)
        {
            seats = new List<Vector2>(); faces = new List<Vector2>();
            var near = FaceToCam(p, false); var along = Local(p, 1f, 0f).normalized;
            for (int i = 0; i < n; i++)
            {
                bool front = i % 2 == 0; int k = i / 2;
                float x = (k - (Mathf.Ceil(n / 2f) - 1) * 0.5f) * 0.13f;
                var side = front ? near : -near;
                var seat = p.at + along * x + side * (CampScenes6Geo.BenchOut + 0.015f);
                seats.Add(seat); faces.Add(seat - side);
            }
        }

        string TryTableMeal(IReadOnlyList<PersonActivity> acts, float time, bool force, in PropSpot p)
        {
            var eaters = Pick(acts, p.at, a => IsGrown(a) || IsKid(a), 5, force);
            if (eaters.Count < 3) return "three people needed at the table";
            int giver = eaters[eaters.Count - 1]; eaters.RemoveAt(eaters.Count - 1);
            TableSeats(p, eaters.Count, out var seats, out var faces);
            var along = Local(p, 1f, 0f).normalized;
            var pot = p.at + along * (PropGeo.TableHalf + 0.12f) + FaceToCam(p, false) * 0.1f; // the bowls wait at the table's end [est]
            return Start(CampScenes6.TableMeal(giver, At(giver).Value, eaters, AtAll(eaters), seats, faces, p.at, pot, ToCam(p.at), time), p, CampUse.TableMeal);
        }

        string TryTableUnder(IReadOnlyList<PersonActivity> acts, float time, bool force, in PropSpot p)
        {
            var kids = Kids(acts, p.at, 2, force);
            if (kids.Count < 2) return "two kids needed ('Two kids join the camp' first)";
            var along = Local(p, 1f, 0f).normalized;
            if (Vector2.Dot(along, CameraAcross()) < 0f) along = -along; // they crawl across the screen
            var endA = p.at - along * (ConsequenceMeshes.TableLegX * PropGeo.S + 0.07f); var endB = p.at + along * (ConsequenceMeshes.TableLegX * PropGeo.S + 0.07f); // #693 notes: in and out between the legs
            int old = -1;
            foreach (var a in acts) if (planner.StageOf(a.who) == LifeStage.Elder && !kids.Contains(a.who.Key) && (force ? !byKey.ContainsKey(a.who.Key) && At(a.who.Key).HasValue : Free(a, clock))) { old = a.who.Key; break; }
            if (old < 0 && force) old = Grown1(acts, p.at, true, kids);
            var bench = p.at + FaceToCam(p, false) * CampScenes6Geo.BenchOut;
            return Start(CampScenes6.TableUnder(kids[0], kids[1], At(kids[0]).Value, At(kids[1]).Value, endA, endB, old, old >= 0 ? At(old).Value : default, bench, along, ToCam(p.at), time), p, CampUse.TableUnder);
        }

        string TryOffer(IReadOnlyList<PersonActivity> acts, float time, bool force, in PropSpot p)
        {
            int who = Leader(acts, p.at, force);
            if (who < 0) { int kid = Kid1(acts, p.at, force); who = kid; } // the chief, or a kid the chief sends
            if (who < 0) return "nobody free to carry the offering";
            return Start(CampScenes6.OfferingFill(who, At(who).Value, p.at, ToCam(p.at), time), p, CampUse.OfferingFill);
        }

        string TryOfferWait(IReadOnlyList<PersonActivity> acts, float time, bool force, in PropSpot p)
        {
            var kids = Kids(acts, p.at, 3, force);
            if (kids.Count < (force ? 1 : 2)) return "two kids needed ('Two kids join the camp' first)";
            if (force) { offeredDay = now.day; ConsequencePropsView.SetLook(p.id, 1); }
            return Start(CampScenes6.OfferingWait(kids, AtAll(kids), p.at, ToCam(p.at), time), p, CampUse.OfferingWait);
        }

        string TryCrateFish(IReadOnlyList<PersonActivity> acts, float time, bool force, in PropSpot p)
        {
            int who = WithJob(acts, JobType.Fisher, force); // the rack keeper: a fisher [est]
            if (who < 0) who = Grown1(acts, p.at, force);
            if (who < 0) return "no grown-up free";
            if (force) ConsequencePropsView.SetLook(p.id, 0);
            Vector2 rack, face;
            if (FishRackAt(out var mid, out var rackAlong, out _)) { var n = new Vector2(-rackAlong.y, rackAlong.x); if (Vector2.Dot(n, ToCam(mid)) < 0f) n = -n; rack = mid + n * 0.12f; face = mid; }
            else { rack = meals.HasFire ? meals.Fire + Dir2(meals.Fire, p.at) * 0.35f : p.at + ToCam(p.at) * 0.6f; face = meals.HasFire ? meals.Fire : rack + Vector2.up; } // no rack: the fish go to the fire [est]
            return Start(CampScenes6.CrateFish(who, At(who).Value, p.at, rack, face, ToCam(p.at), time), p, CampUse.CrateFish);
        }

        string TryCrateWave(IReadOnlyList<PersonActivity> acts, float time, bool force, in PropSpot p)
        {
            var kids = Kids(acts, p.at, 2, force);
            if (kids.Count == 0) return "no kid free ('Two kids join the camp' first)";
            Vector2 shore = p.at - ToCam(p.at);
            foreach (var g in sim.ApartGroups) { shore = new Vector2(g.x, g.z); break; } // the pond family's hut across the water
            if (planner != null && sim.ApartGroups.Count == 0 && planner.Positions.TryGetPosition(AquariumCiv.Life.Place.Land(PlaceKind.Pond), out float wx, out float wz)) shore = new Vector2(wx, wz);
            return Start(CampScenes6.CrateWave(kids, AtAll(kids), p.at, shore, ToCam(p.at), time), p, CampUse.CrateWave);
        }

        // ================================================================== B54.u: the Keeper knocks while they wait
        readonly Dictionary<CampScene, int> knockSeen = new();
        /// <summary>Tests: how many waits the Keeper's knock answered.</summary>
        public int KnockAnswers { get; private set; }

        void StepKnock(float time)
        {
            var g = GlassVoice.Instance;
            if (g == null) return;
            CleanUp(knockSeen);
            foreach (var s in scenes)
            {
                if (s.use != CampUse.OfferingWait) continue;
                if (!knockSeen.TryGetValue(s, out int k)) { knockSeen[s] = g.Knocks; continue; }
                if (g.Knocks <= k) continue;
                knockSeen[s] = g.Knocks;
                float u = time - s.t0;
                if (u < 0f || u > s.Length - 0.5f) continue;
                AnswerKnock(s, u);
                KnockAnswers++;
            }
        }

        /// <summary>B54.u: the knock comes now (scene second <paramref name="u"/>): every kid jumps up (a hop of 0.05 u, both arms up) and points
        /// at the glass (2 s), then runs off. The rest of their wait is dropped.</summary>
        void AnswerKnock(CampScene s, float u)
        {
            var toCam = ToCam(s.focus);
            for (int m = 0; m < s.Count; m++)
            {
                var ps = s.parts[m];
                Vector2 at = s.focus;
                foreach (var p in ps) if (p.from <= u && u < p.to) { at = p.keepSpot ? (At(s.keys[m]) ?? s.focus) : p.pos; p.to = u; }
                ps.RemoveAll(p => p.from >= u);
                float ph = m * 0.12f;
                s.Part(m, u, u + 0.6f, at, at + toCam, DollStyle.Stand, (ref DollPose p, float t, float l) =>
                {
                    float k = CampPose.Bump(t, ph, 0.45f); p.offset.y += 0.05f * k; p.armL = new Vector3(150f, 0f, -20f); p.armR = new Vector3(150f, 0f, 20f);
                });
                s.Part(m, u + 0.6f, u + 2.6f, at, at + toCam, DollStyle.Stand, (ref DollPose p, float t, float l) =>
                {
                    p.armR = new Vector3(115f + 8f * Mathf.Sin(t * 12f), 0f, 6f); p.lean -= 10f; p.offset.y += 0.012f * Mathf.Abs(Mathf.Sin(t * 7f + ph));
                });
                var go = s.Part(m, u + 2.6f, u + 3.6f, at + toCam * 0.25f + CameraAcross() * ((m - 1) * 0.15f), at + toCam, DollStyle.Stand, null, PropKind.None, CampScenes1.KidRun);
                go.run = true;
            }
            s.Fx(u + 0.05f, CampFxKind.Dust, new Vector3(s.focus.x, 0.01f, s.focus.y), 0.3f, 2);
        }

        /// <summary>The named child of a name post (its person), else (the cheat) any kid.</summary>
        int PostKid(IReadOnlyList<PersonActivity> acts, in PropSpot p, bool force)
        {
            var n = p.personId >= 0 ? sim.FindNotable(p.personId) : null;
            if (n != null && n.alive && Shown(acts, n.id) && (force || FreeKey(acts, n.id)))
                foreach (var a in acts) if (a.who.Key == n.id && IsKid(a)) return n.id;
            return force ? Kid1(acts, p.at, true) : -1;
        }

        /// <summary>The named child's parent if drawn and free, else a grown-up.</summary>
        int PostGrown(IReadOnlyList<PersonActivity> acts, in PropSpot p, bool force, ICollection<int> not = null)
        {
            var child = p.personId >= 0 ? sim.FindNotable(p.personId) : null;
            if (child != null)
                foreach (var n in sim.State.notables)
                    if (n.alive && n.id == child.parentId && (not == null || !not.Contains(n.id)) && Shown(acts, n.id) && (force || FreeKey(acts, n.id))) return n.id;
            return Grown1(acts, p.at, force, not);
        }

        string TryPostCarve(IReadOnlyList<PersonActivity> acts, float time, bool force, in PropSpot p)
        {
            int who = PostGrown(acts, p, force);
            if (who < 0) return "no grown-up free";
            retieYear = now.day / SimConfig.MonthsPerYear;
            return Start(CampScenes6.PostCarve(who, At(who).Value, p.at, FaceToCam(p, false), time), p, CampUse.PostCarve);
        }

        string TryPostMeasure(IReadOnlyList<PersonActivity> acts, float time, bool force, in PropSpot p)
        {
            int kid = PostKid(acts, p, force);
            if (kid < 0) return "no kid free ('Two kids join the camp' first)";
            int g = PostGrown(acts, p, force, new[] { kid });
            return Start(CampScenes6.PostMeasure(kid, At(kid).Value, g, g >= 0 ? At(g).Value : default, p.at, FaceToCam(p, false), time), p, CampUse.PostMeasure);
        }

        // ================================================================== what the scenes do to the thing (CampFxKind.Prop)
        void FireProp(CampScene s, in CampFx f)
        {
            int id = s.thing - ThingPropBase;
            if (id < 0) return;
            switch ((PropFx)f.count)
            {
                case PropFx.Jolt: ConsequencePropsView.Jolt(id); break;
                case PropFx.Look: ConsequencePropsView.SetLook(id, Mathf.RoundToInt(f.scale)); break;
                case PropFx.Upright: ConsequencePropsView.Upright(id); birdTipped.Remove(id); break;
                case PropFx.Flour: if (f.member >= 0 && f.member < s.Count) floured[s.keys[f.member]] = now.day * 24f + 20f; break; // until the evening wash
                case PropFx.NameRow:
                {
                    int cut = (nameCut.TryGetValue(id, out int c) ? c : 0) + 1;
                    nameCut[id] = cut;
                    ConsequencePropsView.SetLook(id, Mathf.Min(3, cut));
                    break;
                }
                case PropFx.Offered: offeredDay = now.day; break;
                case PropFx.Swing: ConsequencePropsView.Swing(id); break;
                case PropFx.Stack: { int n = Mathf.Min(ConsequenceMeshes.WoodSlots, (woodStacked.TryGetValue(id, out int w0) ? w0 : 0) + 1); woodStacked[id] = n; ConsequencePropsView.SetLook(id, n); break; }
                case PropFx.Unstack: { int n = Mathf.Max(0, (woodStacked.TryGetValue(id, out int w0) ? w0 : 0) - 1); woodStacked[id] = n; ConsequencePropsView.SetLook(id, n); break; }
                case PropFx.Recut: postRecut[id] = now.day; ConsequencePropsView.SetLook(id, PostLook(id)); break;
                case PropFx.Measure: postScratches[id] = Mathf.Min(3, (postScratches.TryGetValue(id, out int m0) ? m0 : 0) + 1); ConsequencePropsView.SetLook(id, PostLook(id)); break;
            }
        }

        /// <summary>The name post's look: its scratches (0-3) and +4 while the mark is freshly re-cut (the day it was).</summary>
        int PostLook(int id) => (postScratches.TryGetValue(id, out int n) ? n : 0) | (postRecut.TryGetValue(id, out long d) && d == now.day ? 4 : 0);

        // ================================================================== drawn beside the things
        void DrawProps(Transform root, Material material, float time, System.Func<float, float, float> groundAt)
        {
            int used = 0;
            CleanUp(ladders); CleanUp(splits);
            foreach (var s in scenes)
            {
                float u = time - s.t0;
                if (s.use == CampUse.BasketTake && u >= s.a && u < s.b && pv.ViewOf(s.keys[0]) is PegDollView bv && bv.gameObject.activeInHierarchy)
                {   // #693 notes: the very basket of the pile (same mesh, size, colour), carried in both hands
                    oneBaskets ??= new[] { ConsequenceMeshes.OneBasket(0), ConsequenceMeshes.OneBasket(1), ConsequenceMeshes.OneBasket(2) };
                    var t = PropPiece(root, material, used++, oneBaskets[2]);
                    var hands = root.InverseTransformPoint(HandsMid(bv));
                    t.localPosition = new Vector3(hands.x, Mathf.Max(groundAt(hands.x, hands.z), hands.y - 0.05f), hands.z);
                    t.localRotation = Quaternion.Euler(0f, bv.transform.eulerAngles.y, 0f);
                    t.localScale = Vector3.one * ConsequenceMeshes.Scale;
                }
                if (s.use == CampUse.StumpSplit && splits.TryGetValue(s, out var sp) && u >= s.b && u < s.Length - 1.1f)
                {
                    logMesh ??= PieceMesh(0); halfMesh ??= PieceMesh(1);
                    float top = groundAt(sp.stump.x, sp.stump.y) + PropGeo.StumpTop;
                    if (u < s.a)
                    {
                        var t = PropPiece(root, material, used++, logMesh);
                        t.localPosition = new Vector3(sp.stump.x, top, sp.stump.y); t.localRotation = Quaternion.identity; t.localScale = Vector3.one * ConsequenceMeshes.Scale;
                    }
                    else
                        for (int k = -1; k <= 1; k += 2)
                        {
                            float e = CampPose.Smooth((u - s.a) / 0.4f);
                            var p = sp.stump + sp.across * (k * 0.13f * e);
                            float g = groundAt(p.x, p.y);
                            var t = PropPiece(root, material, used++, halfMesh);
                            t.localPosition = new Vector3(p.x, Mathf.Lerp(top, g + 0.012f, e) + 0.03f * Mathf.Sin(e * Mathf.PI), p.y);
                            float yaw = Mathf.Atan2(sp.across.x, sp.across.y) * Mathf.Rad2Deg;
                            t.localRotation = Quaternion.Euler(0f, yaw, 0f) * Quaternion.Euler(0f, 0f, -k * 90f * e);
                            t.localScale = Vector3.one * ConsequenceMeshes.Scale;
                        }
                }
                if (s.use == CampUse.BirdStraighten && ladders.TryGetValue(s, out var ld) && u >= s.b && u < s.d)
                {
                    ladderMesh ??= PieceMesh(2);
                    var t = PropPiece(root, material, used++, ladderMesh);
                    var toPole = Dir2(ld.foot, ld.pole);
                    t.localPosition = new Vector3(ld.foot.x, groundAt(ld.foot.x, ld.foot.y), ld.foot.y);
                    t.localRotation = Quaternion.LookRotation(new Vector3(toPole.x, 0f, toPole.y)) * Quaternion.Euler(14f, 0f, 0f); // leaning on the pole
                    t.localScale = Vector3.one * CampScenes6.LadderScale; // #693 retakes: it reaches the bird
                }
                used = DrawRetakeThings(root, material, s, u, used, groundAt);
                if (s.use == CampUse.FlourFight)
                    foreach (var a in s.arcs)
                    {
                        float k = (u - a.from) / Mathf.Max(0.01f, a.seconds);
                        if (k < 0f || k > 1f || a.member >= s.Count) continue;
                        var v = pv.ViewOf(s.keys[a.member]);
                        if (v == null || !v.gameObject.activeInHierarchy) continue;
                        flourMesh ??= PieceMesh(3);
                        var from = root.InverseTransformPoint(v.armR.TransformPoint(new Vector3(0f, -PegDoll.ArmLen * v.scale, 0f)));
                        var to = new Vector3(a.to.x, groundAt(a.to.x, a.to.z) + a.to.y, a.to.z);
                        var t = PropPiece(root, material, used++, flourMesh);
                        t.localPosition = Vector3.Lerp(from, to, k) + Vector3.up * (4f * a.h * k * (1f - k)); t.localRotation = Quaternion.identity; t.localScale = Vector3.one * 0.03f; // #693 retakes: 1.7x
                    }
            }
            // #693 notes: the sacks set down at doors stay there (view-only, at most 3)
            foreach (var sc in scenes) if (sc.use == CampUse.FlourCarry && time - sc.t0 >= sc.a && time - sc.t0 < sc.a + 0.05f && (sacksDown.Count == 0 || (sacksDown[^1].at - sc.p0).sqrMagnitude > 1e-6f)) { sacksDown.Add((sc.p0, (sc.t0 * 37f) % 360f)); if (sacksDown.Count > 3) sacksDown.RemoveAt(0); }
            foreach (var sd in sacksDown)
            {
                oneSack ??= ConsequenceMeshes.OneSack();
                var t = PropPiece(root, material, used++, oneSack);
                t.localPosition = new Vector3(sd.at.x, groundAt(sd.at.x, sd.at.y), sd.at.y); t.localRotation = Quaternion.Euler(0f, sd.yaw, 0f); t.localScale = Vector3.one * ConsequenceMeshes.Scale;
            }
            // white flour on the kids until the evening wash
            foreach (var kv in floured)
            {
                var v = pv.ViewOf(kv.Key);
                if (v == null || !v.gameObject.activeInHierarchy) continue;
                flourMesh ??= PieceMesh(3);
                for (int k = 0; k < 2; k++)
                {
                    var t = PropPiece(root, material, used++, flourMesh);
                    t.position = v.transform.TransformPoint(new Vector3(k == 0 ? -0.012f : 0.014f, PegDoll.HeadY * (k == 0 ? 0.55f : 0.9f), -0.024f) * v.scale);
                    t.localRotation = Quaternion.identity; t.localScale = Vector3.one * (k == 0 ? 0.026f : 0.02f) * Mathf.Max(0.8f, v.scale); // #693 retakes: 1.6x
                }
            }
            for (int i = used; i < propPool.Count; i++) if (propPool[i] != null) propPool[i].gameObject.SetActive(false);
            DrawGoat(root, material, time, groundAt); // the pond family's goat in its pen
        }

        /// <summary>#693 retakes: what a use holds or shows, drawn 1.4-2x so it reads at phone scale: the log carried to the woodpile and laid on
        /// it, the headman's staff in the hand, a fish lifted from the crate, the charm a kid flicks (lit, on the camera's side of the crown).</summary>
        int DrawRetakeThings(Transform root, Material material, CampScene s, float u, int used, System.Func<float, float, float> groundAt)
        {
            PegDollView V(int m) { var v = m < s.Count ? pv.ViewOf(s.keys[m]) : null; return v != null && v.gameObject.activeInHierarchy ? v : null; }
            Vector3 Hand(PegDollView v) => root.InverseTransformPoint(v.armR.TransformPoint(new Vector3(0f, -PegDoll.ArmLen * v.scale, 0f)));
            if (s.use == CampUse.WoodStack && s.kind == 0 && u < s.a)
            {   // #693 notes: the pile's own log piece, held along the pile's logs (no turn); over the front logs and down into its free slot
                carryLog ??= PieceMesh(4);
                var v = V(0);
                if (v != null)
                {
                    var t = PropPiece(root, material, used++, carryLog);
                    var rot = Quaternion.Euler(0f, s.b, 0f);
                    var hands = root.InverseTransformPoint(HandsMid(v));
                    var slotL = ConsequenceMeshes.WoodSlot(Mathf.RoundToInt(s.p0.x)) * ConsequenceMeshes.Scale;
                    var slot = new Vector3(s.focus.x, groundAt(s.focus.x, s.focus.y), s.focus.y) + rot * slotL;
                    var pos = hands;
                    if (u >= s.c)
                    {
                        float k = CampPose.Smooth(Mathf.Clamp01((u - s.c) / Mathf.Max(0.05f, s.a - s.c)));
                        var front = new Vector3(hands.x, slot.y + 0.06f, hands.z);
                        var over = new Vector3(slot.x, slot.y + 0.05f, slot.z);
                        pos = k < 0.5f ? Vector3.Lerp(hands, front, k * 2f) : k < 0.85f ? Vector3.Lerp(front, over, (k - 0.5f) / 0.35f) : Vector3.Lerp(over, slot, (k - 0.85f) / 0.15f);
                    }
                    t.localPosition = pos; t.localRotation = rot; t.localScale = Vector3.one * ConsequenceMeshes.Scale;
                }
            }
            if ((s.use == CampUse.StaffWalk || s.use == CampUse.StaffPlay) && u >= s.c && u < s.d && V(0) is PegDollView sv)
            {
                staffMesh ??= PieceMesh(5);
                var t = PropPiece(root, material, used++, staffMesh);
                var hand = Hand(sv);
                t.localPosition = new Vector3(hand.x, Mathf.Max(groundAt(hand.x, hand.z), hand.y - 0.09f), hand.z); // planted: its foot near the ground, the hand on it
                t.localRotation = Quaternion.Euler(0f, sv.transform.eulerAngles.y, 0f);
                t.localScale = Vector3.one * 1.6f;
            }
            if (s.use == CampUse.CrateFish && V(0) is PegDollView cv)
                foreach (var f in s.fx)
                    if (f.kind == CampFxKind.Prop && f.count == (int)PropFx.Look && u >= f.time - 0.3f && u < f.time + 0.35f)
                    {
                        fishMesh ??= PieceMesh(6);
                        var t = PropPiece(root, material, used++, fishMesh);
                        t.localPosition = Hand(cv); t.localRotation = Quaternion.Euler(0f, cv.transform.eulerAngles.y, 15f * Mathf.Sin(u * 20f)); t.localScale = Vector3.one * 1.7f;
                    }
            if (s.use == CampUse.CrateWave && u >= s.c && u < s.d && V(0) is PegDollView wv)
            {
                fishMesh ??= PieceMesh(6);
                var t = PropPiece(root, material, used++, fishMesh);
                t.localPosition = Hand(wv); t.localRotation = Quaternion.Euler(0f, wv.transform.eulerAngles.y, 20f * Mathf.Sin(u * 18f)); t.localScale = Vector3.one * 1.8f;
            }
            if (s.use == CampUse.BigPot)
            {   // #693 notes: the block he stands on, and the ladle from his hands down into the pot, going round
                blockMesh ??= PieceMesh(8); ladleMesh ??= PieceMesh(9);
                var bt = PropPiece(root, material, used++, blockMesh);
                bt.localPosition = new Vector3(s.p0.x, groundAt(s.p0.x, s.p0.y), s.p0.y); bt.localRotation = Quaternion.identity; bt.localScale = Vector3.one;
                if (u >= s.c && u < s.d && V(0) is PegDollView lv)
                {
                    var hands = root.InverseTransformPoint(HandsMid(lv));
                    float a = (u - s.c) * Mathf.PI * 2f * 0.9f;
                    var pot = s.focus; float g = groundAt(pot.x, pot.y);
                    var tip = new Vector3(pot.x + Mathf.Cos(a) * 0.035f, g + PropGeo.PotRim - 0.04f, pot.y + Mathf.Sin(a) * 0.035f);
                    var lt = PropPiece(root, material, used++, ladleMesh);
                    var d = hands - tip;
                    lt.localPosition = tip; lt.localRotation = Quaternion.FromToRotation(Vector3.up, d.normalized); lt.localScale = new Vector3(1f, d.magnitude + 0.02f, 1f);
                }
            }
            if (s.use == CampUse.FlourCarry && u >= s.c && u < s.a && V(0) is PegDollView fv)
            {   // #693 notes: the heap's own sack, on the carrier's shoulder
                oneSack ??= ConsequenceMeshes.OneSack();
                var t = PropPiece(root, material, used++, oneSack);
                t.position = fv.transform.TransformPoint(new Vector3(-0.022f, PegDoll.HeadY * 0.78f, 0.004f) * fv.scale);
                t.localRotation = Quaternion.Euler(0f, fv.transform.eulerAngles.y, 70f); t.localScale = Vector3.one * ConsequenceMeshes.Scale;
            }
            if (s.use == CampUse.CharmFlick)
            {
                charmMesh ??= PieceMesh(7);
                var t = PropPiece(root, material, used++, charmMesh);
                var at = s.focus; float g = groundAt(at.x, at.y);
                float spin = u >= s.a ? 720f * (1f - Mathf.Exp(-(u - s.a) / 0.5f)) : 0f;
                float sway = u >= s.a ? 18f * Mathf.Sin((u - s.a) * 6f) * Mathf.Exp(-(u - s.a) / 0.8f) : 0f;
                t.localPosition = new Vector3(at.x, g + 0.36f, at.y); // the cord's top, in the crown's edge; the charm hangs to ~0.26 u, a jump's reach
                t.localRotation = Quaternion.Euler(sway, spin, 0f);
                t.localScale = Vector3.one * 1.8f;
            }
            return used;
        }

        void CleanUp<T>(Dictionary<CampScene, T> d)
        {
            if (d.Count == 0) return;
            var gone = new List<CampScene>(); foreach (var s in d.Keys) if (!scenes.Contains(s)) gone.Add(s);
            foreach (var s in gone) d.Remove(s);
        }

        Transform PropPiece(Transform root, Material material, int i, Mesh mesh)
        {
            while (i < propPool.Count && propPool[i] == null) propPool.RemoveAt(i);
            while (propPool.Count <= i)
            {
                var go = new GameObject("Camp prop use") { hideFlags = HideFlags.DontSave };
                go.transform.SetParent(root, false);
                go.AddComponent<MeshFilter>();
                var r = go.AddComponent<MeshRenderer>(); r.sharedMaterial = material; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                propPool.Add(go.transform);
            }
            var t = propPool[i];
            t.GetComponent<MeshFilter>().sharedMesh = mesh;
            t.gameObject.SetActive(true);
            return t;
        }

        /// <summary>The pieces (box units, foot at the origin): 0 a short log stood on end (B54.j), 1 a split half, 2 the short ladder (B54.p, 0.18 u,
        /// rails along +y, leaning toward +z), 3 a unit ball of flour (white).</summary>
        static Mesh PieceMesh(int which)
        {
            var mb = new MeshBuilder(6930 + which);
            switch (which)
            {
                case 0: mb.Prism(Vector3.zero, 0.026f, 0.026f, 0.06f, 7, Palette.Bark, topCol: Palette.WoodLight); break;
                case 1: mb.Box(new Vector3(0f, 0.03f, 0f), new Vector3(0.024f, 0.06f, 0.045f), Palette.WoodLight); mb.Box(new Vector3(-0.013f, 0.03f, 0f), new Vector3(0.006f, 0.058f, 0.044f), Palette.Bark); break;
                case 2:
                    foreach (float x in new[] { -0.025f, 0.025f }) mb.Box(new Vector3(x, CampScenes6.LadderH * 0.5f, 0f), new Vector3(0.008f, CampScenes6.LadderH, 0.008f), Palette.Timber);
                    for (int k = 0; k < 4; k++) mb.Box(new Vector3(0f, 0.03f + k * 0.045f, 0f), new Vector3(0.05f, 0.006f, 0.006f), Palette.WoodLight);
                    break;
                case 4: Pieces.Log(mb, Vector3.zero, 0.026f, 0.26f, 90f, Palette.Bark); break; // #693 notes: the woodpile's own log, crosswise (before its 1.5 scale)
                case 8: mb.Prism(Vector3.zero, 0.055f, 0.06f, CampScenes6.StirBlock, 8, Palette.Bark, topCol: Palette.WoodLight); for (int k = 0; k < 3; k++) mb.Prism(new Vector3(0f, CampScenes6.StirBlock + 0.0005f, 0f), 0.015f + k * 0.014f, 0.015f + k * 0.014f, 0.001f, 8, k % 2 == 0 ? Palette.Bark : Palette.WoodLight); break; // a round of log, its rings on top // the block of wood the stirrer stands on
                case 9: mb.Box(new Vector3(0f, 0.5f, 0f), new Vector3(0.009f, 1f, 0.009f), Palette.WoodLight); break; // the ladle's handle, a unit long (scaled to reach)
                case 5: // the headman's staff: a pale pole, a red knob, a ribbon
                    mb.Prism(Vector3.zero, 0.007f, 0.006f, 0.22f, 6, Palette.WoodLight);
                    mb.Ico(new Vector3(0f, 0.225f, 0f), 0.016f, Vector3.one, Palette.Accents[1], 0.05f);
                    mb.Box(new Vector3(0.016f, 0.19f, 0f), new Vector3(0.03f, 0.026f, 0.002f), Palette.Accents[0]);
                    break;
                case 6: mb.Ico(Vector3.zero, 0.016f, new Vector3(1.7f, 0.55f, 0.7f), Palette.Water, 0.05f); mb.Ico(new Vector3(-0.03f, 0f, 0f), 0.008f, new Vector3(0.6f, 1.2f, 0.4f), Palette.WaterDeep, 0.05f); break; // a fish
                case 7: // a charm on its cord: the cord from the crown down, a red bead and a pale shell under it (lit: bright colours)
                    mb.Box(new Vector3(0f, -0.04f, 0f), new Vector3(0.003f, 0.08f, 0.003f), new Color(0.95f, 0.9f, 0.78f));
                    mb.Ico(new Vector3(0f, -0.085f, 0f), 0.012f, Vector3.one, new Color(0.95f, 0.22f, 0.18f), 0.05f);
                    mb.Box(new Vector3(0.008f, -0.1f, 0f), new Vector3(0.016f, 0.014f, 0.004f), new Color(0.98f, 0.95f, 0.85f));
                    break;
                default: mb.Ico(Vector3.zero, 1f, Vector3.one, new Color(0.97f, 0.96f, 0.92f), 0.1f); break;
            }
            var m = mb.ToMesh("Camp_PropUse_" + which); m.hideFlags = HideFlags.DontSave;
            return m;
        }

        void DestroyProps()
        {
            DestroyGoat();
            foreach (var t in propPool) if (t != null) UnityEngine.Object.Destroy(t.gameObject);
            propPool.Clear();
        }

        // ================================================================== cheats
        string ForceProp(CampUse use)
        {
            if (IsPenUse(use)) return ForcePen(use);
            if (sim == null || planner == null || meals == null) return "no camp yet";
            var acts = pv.Activities; float time = clock;
            string kind = KindFor(use);
            if (!PropOf(kind, out var p)) return $"no {kind} in the box ('#693: every choice's thing' first)";
            switch (use)
            {
                case CampUse.BasketTake: return TryBasketTake(acts, time, true, p) ?? "a basket taken and brought back full";
                case CampUse.WoodStack: { bool f = ForceFetch; ForceFetch = !ForceFetch; return TryWoodStack(acts, time, true, p, f) ?? (f ? "a log fetched for the fire" : "a log stacked under the roof"); }
                case CampUse.WoodShelter:
                {
                    bool dry = ForceDry; ForceDry = !ForceDry;
                    if (!dry) planner.ForcedWeather = Weather.KeeperRain;
                    return TryShelter(acts, time, true, p, !dry) ?? (dry ? "kids sit on the logs" : "rain: under the woodpile's roof");
                }
                case CampUse.NameCut: nameExtra++; if (nameCut.TryGetValue(p.id, out int cutNow) && cutNow >= 3) { nameCut[p.id] = 2; ConsequencePropsView.SetLook(p.id, 2); } return TryNameCut(acts, time, true, p) ?? "a new name cut in the stone";
                case CampUse.NameTrace: return TryNameTrace(acts, time, true, p) ?? "kids trace the names";
                case CampUse.CharmFlick: return TryCharmFlick(acts, time, true, p) ?? "a kid flicks the charms";
                case CampUse.BigPot: bigPotDay = now.day; meals.BigPotAt = p.at; return TryBigPot(acts, time, true, p) ?? "the big pot tonight (then 'Bowl round: ladle' or 'Meal now')";
                case CampUse.PotDrum: return TryPotDrum(acts, time, true, p) ?? "kids drum on the pot";
                case CampUse.StumpSplit: return TryStumpSplit(acts, time, true, p) ?? "wood split at the stump";
                case CampUse.StumpSit: return TryStumpSit(acts, time, true, p) ?? "kids sit on the stump";
                case CampUse.StaffWalk: return TryStaffWalk(acts, time, true, p) ?? "the headman takes the staff";
                case CampUse.StaffPlay: return TryStaffPlay(acts, time, true, p) ?? "kids play headman";
                case CampUse.FlourCarry: return TryFlourCarry(acts, time, true, p) ?? "flour carried";
                case CampUse.FlourFight: return TryFlourFight(acts, time, true, p) ?? "a flour fight";
                case CampUse.BirdStraighten: birdTipped.Add(p.id); ConsequencePropsView.Tip(p.id, 20f); return TryBird(acts, time, true, p) ?? "storm: the roof bird set straight";
                case CampUse.BirdFly: return TryBirdFly(acts, time, true, p) ?? "kids fly under the bird";
                case CampUse.TableMeal: return TryTableMeal(acts, time, true, p) ?? "the meal at the long table";
                case CampUse.TableUnder: return TryTableUnder(acts, time, true, p) ?? "kids under the long table";
                case CampUse.OfferingFill: return TryOffer(acts, time, true, p) ?? "the offering basket filled";
                case CampUse.OfferingWait: return TryOfferWait(acts, time, true, p) ?? "kids wait by the offering";
                case CampUse.CrateFish: return TryCrateFish(acts, time, true, p) ?? "the fish crate to the rack";
                case CampUse.CrateWave: return TryCrateWave(acts, time, true, p) ?? "kids wave across the pond";
                case CampUse.PostCarve: return TryPostCarve(acts, time, true, p) ?? "the name post re-carved";
                case CampUse.PostMeasure: return TryPostMeasure(acts, time, true, p) ?? "a kid's height marked on the name post";
            }
            return "not a #693 use: " + use;
        }

        public static string KindFor(CampUse use) => use switch
        {
            CampUse.BasketTake => ConsequenceKind.Baskets,
            CampUse.WoodStack or CampUse.WoodShelter => ConsequenceKind.WoodpileRoof,
            CampUse.NameCut or CampUse.NameTrace => ConsequenceKind.NameStone,
            CampUse.CharmFlick => ConsequenceKind.CharmPole,
            CampUse.BigPot or CampUse.PotDrum => ConsequenceKind.StewPot,
            CampUse.StumpSplit or CampUse.StumpSit => ConsequenceKind.AxeStump,
            CampUse.StaffWalk or CampUse.StaffPlay => ConsequenceKind.Staff,
            CampUse.FlourCarry or CampUse.FlourFight => ConsequenceKind.FlourSacks,
            CampUse.BirdStraighten or CampUse.BirdFly => ConsequenceKind.RoofBird,
            CampUse.TableMeal or CampUse.TableUnder => ConsequenceKind.LongTable,
            CampUse.OfferingFill or CampUse.OfferingWait => ConsequenceKind.OfferingBasket,
            CampUse.CrateFish or CampUse.CrateWave => ConsequenceKind.FishCrate,
            CampUse.PostCarve or CampUse.PostMeasure => ConsequenceKind.NamePeg,
            CampUse.PenMend or CampUse.PenRails or CampUse.PenPet => ConsequenceKind.GoatPen,
            _ => "",
        };

        // ================================================================== tests and the readout
        public string PropMarks()
        {
            var sb = new System.Text.StringBuilder();
            foreach (var p in Props()) sb.Append($"{p.kind}#{p.id}:{ConsequencePropsView.LookOf(p.id)}{(ConsequencePropsView.TiltOf(p.id) != 0f ? $" tilt {ConsequencePropsView.TiltOf(p.id):0}" : "")}; ");
            sb.Append($"floured {floured.Count}, offered {(offeredDay == now.day ? "today" : "no")}, big pot {(meals != null && meals.BigPotAt != null ? "on" : "off")}");
            return sb.ToString();
        }
        public int FlouredNow => floured.Count;
        public bool OfferedToday => offeredDay == now.day;
        public int PropsStanding => Props().Count;
        public int EatersNow => meals != null ? meals.Eaters : 0;
        /// <summary>Tests (the work share): this person is drawn by a #693 use now.</summary>
        public bool PropCued(int key) => byKey.TryGetValue(key, out var s) && IsPropUse(s.use) && s.TryPart(key, clock, out _, out _);
        public int NameRowsCut(int propId) => nameCut.TryGetValue(propId, out int c) ? c : 0;
    }

    /// <summary>#693: the long table's bench distance, as drawn (the CampScenes6 sizes for the director).</summary>
    static class CampScenes6Geo { public const float BenchOut = PropGeo.BenchOut; }
}

namespace AquariumCiv.View
{
    /// <summary>#693 cheats (SliceDebugPanel's "#693" block): each starts the use now with the people nearest its thing (the box needs the
    /// thing: <see cref="EveryThing"/> stands every choice's thing at once).</summary>
    public static partial class CampCheats
    {
        /// <summary>"#693: every choice's thing": each choice's thing stands in the box now (ConsequenceCheats.PlaceEvery).</summary>
        public static string EveryThing() => ConsequenceCheats.PlaceEvery();
        /// <summary>AnimLab and the tests: only <paramref name="kind"/>'s thing stands (with the feather pole), as in a box where that choice was
        /// made (the two answers of one choice never stand together: with every thing out, the baskets hid behind the woodpile's roof).</summary>
        public static string OnlyThing(string kind)
        {
            var sim = AquariumCiv.Game.GameClock.Instance != null ? AquariumCiv.Game.GameClock.Instance.Sim : null;
            if (sim == null) return "no sim";
            ConsequenceCheats.PlaceEvery();
            bool first = true;
            sim.State.consequenceProps.RemoveAll(p => { if (p.kind == AquariumCiv.Sim.ConsequenceKind.Feather) return false; if (p.kind != kind) return true; bool keep = first; first = false; return !keep; });
            return $"only the {kind} stands ({sim.State.consequenceProps.Count} things)";
        }
        public static string BasketTake() => Do(CampUse.BasketTake);
        public static string WoodStack() => Do(CampUse.WoodStack);
        public static string WoodShelter() => Do(CampUse.WoodShelter);
        public static string NameCut() => Do(CampUse.NameCut);
        public static string NameTrace() => Do(CampUse.NameTrace);
        public static string CharmFlick() => Do(CampUse.CharmFlick);
        /// <summary>"Big pot: the together supper": tonight is a big-pot night, the cook stirs the big pot, and the bowl round dips there.</summary>
        public static string BigPot() => Do(CampUse.BigPot);
        public static string PotDrum() => Do(CampUse.PotDrum);
        public static string StumpSplit() => Do(CampUse.StumpSplit);
        public static string StumpSit() => Do(CampUse.StumpSit);
        public static string StaffWalk() => Do(CampUse.StaffWalk);
        public static string StaffPlay() => Do(CampUse.StaffPlay);
        public static string FlourCarry() => Do(CampUse.FlourCarry);
        public static string FlourFight() => Do(CampUse.FlourFight);
        public static string BirdStraighten() => Do(CampUse.BirdStraighten);
        public static string BirdFly() => Do(CampUse.BirdFly);
        public static string TableMeal() => Do(CampUse.TableMeal);
        public static string TableUnder() => Do(CampUse.TableUnder);
        public static string OfferingFill() => Do(CampUse.OfferingFill);
        public static string OfferingWait() => Do(CampUse.OfferingWait);
        /// <summary>B54.u with the Keeper's knock: the kids wait, and 2.5 s after the cloth check the glass is knocked (they jump and point).</summary>
        public static string OfferingWaitKnock()
        {
            var r = Do(CampUse.OfferingWait);
            var c = Camp;
            if (c == null || !(c.UntilA is float a)) return r + " (no knock: the wait did not start)";
            c.Later(Mathf.Max(0.5f, a + 2.5f), () => GlassVoice.DebugKnock(3));
            return r + $"; the Keeper knocks in {a + 2.5f:0.0} s";
        }
        public static string CrateFish() => Do(CampUse.CrateFish);
        public static string CrateWave() => Do(CampUse.CrateWave);
        public static string PostCarve() => Do(CampUse.PostCarve);
        public static string PostMeasure() => Do(CampUse.PostMeasure);
        public static string PropMarks() { var c = Camp; return c == null ? "no PeopleView" : c.PropMarks(); }
    }
}
