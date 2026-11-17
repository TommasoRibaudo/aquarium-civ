// S-ritual (B72-B77, docs/design/animation-specs.md "Rituals and the teller", "The pond family's boat"): the camp's half of the yearly
// rituals and the boat. (1) Each ritual's targets for its staged plan (the bushes, the woodpile, the long table, the tree:
// LifePlanner.RitualTargets). (2) What a ritual leaves and draws: the log heap and the ladder, the board carried to the table and the
// table one board longer, the baskets set on it, the kids' small basket, the offering basket full until spring and empty after, purple
// hands. (3) The camp uses: the empty basket found in spring (B75.b), the fisher sorting the catch on the boat (B77.a), the kids' house
// (B77.b). View-only state is not saved [est]; what lasts (full or empty, the boards) is read from the sim's chronicle.
using System.Collections.Generic;
using AquariumCiv.Life;
using AquariumCiv.Sim;
using AquariumCiv.Sim.Settle;
using UnityEngine;

namespace AquariumCiv.View
{
    public sealed partial class CampLife
    {
        static bool IsRitualUse(CampUse u) => u is CampUse.OfferingEmpty or CampUse.BoatSort or CampUse.BoatHouse;

        /// <summary>The cheats' wishes for the next ritual staged (SimDebugPanel's "Rituals" block); spent when its targets are read.</summary>
        public static bool NextToTable, NextFirstWalk, NextDropLog, NextAddBoard, NextTallestTree;

        /// <summary>The ritual's targets now (null: no camp to read them from).</summary>
        public RitualSpots RitualTargetsFor(SimCore s, HeldMoment m, string ritual)
        {
            if (s == null || planner == null) return null;
            var sp = new RitualSpots { ritual = ritual };
            Vector2 fire = meals != null && meals.HasFire ? meals.Fire : planner.FirePoint(out float fx, out float fz) ? new Vector2(fx, fz) : Vector2.zero;
            var entry = EntryOf(s, m.entryId);
            long year = s.State.month / SimConfig.MonthsPerYear;
            if (planner.StashPoint(out float sx, out float sz)) { sp.hasStash = true; sp.stashX = sx; sp.stashZ = sz; }
            if (PropOf(ConsequenceKind.Baskets, out var stack)) { sp.hasStack = true; sp.stackX = stack.at.x; sp.stackZ = stack.at.y; }
            if (PropOf(ConsequenceKind.LongTable, out var table))
            {
                var along = Local(table, 1f, 0f).normalized;
                sp.hasTable = true; sp.tableX = table.at.x; sp.tableZ = table.at.y; sp.tableAX = along.x; sp.tableAZ = along.y;
                sp.boards = TableBoards(s, false); sp.tableLen = TableLength(sp.boards + (ritual == RitualKind.Feast && FeastAddsBoard(s) ? 1 : 0));
            }
            switch (ritual)
            {
                case RitualKind.Berrying:
                {
                    var cells = new List<int>();
                    int season = (int)LifeClock.SeasonOf(s.State.month);
                    s.NearestBushes(fire.x, fire.y, MomentStagePlan.MaxBushes, now.day, season, true, cells);
                    if (cells.Count == 0) s.NearestBushes(fire.x, fire.y, MomentStagePlan.MaxBushes, now.day, season, false, cells);
                    var box = LandBox; var pts = new List<float>();
                    foreach (int c in cells)
                    {
                        Vector2 p = new Vector2(SiteMap.CXc(c), SiteMap.CZc(c));
                        if (box != null) lock (box.BushSpots) if (box.BushSpots.TryGetValue(c, out var bs)) p = new Vector2(bs.pos.x, bs.pos.z);
                        if (!ClearOfGlass(p)) continue;
                        pts.Add(p.x); pts.Add(p.y);
                    }
                    sp.bushes = pts.ToArray();
                    sp.toTable = sp.hasTable && (NextToTable || entry != null && entry.text != null && entry.text.Contains("long table"));
                    if (NextFirstWalk || FirstWalkKid(s) != null)
                    {
                        var kid = FirstWalkKid(s) ?? YoungestKid(s);
                        if (kid != null)
                        {
                            sp.firstWalk = kid.id;
                            var par = s.FindNotable(kid.parentId);
                            sp.firstWalkParent = par != null && par.alive && par.age >= SimCore.AdultAge ? par.id : s.RitualLead(ritual)?.id ?? 0;
                        }
                    }
                    break;
                }
                case RitualKind.Woodpile:
                    if (PropOf(ConsequenceKind.WoodpileRoof, out var pile))
                    {
                        var f = FaceToCam(pile, false);
                        sp.hasPile = true; sp.pileX = pile.at.x; sp.pileZ = pile.at.y; sp.pileFX = f.x; sp.pileFZ = f.y;
                    }
                    if (PropOf(ConsequenceKind.AxeStump, out var stump)) { sp.hasChop = true; sp.chopX = stump.at.x; sp.chopZ = stump.at.y; }
                    else if (WildTree(fire, 3f, out _, out var trunk, out _)) { sp.hasChop = true; sp.chopX = trunk.x; sp.chopZ = trunk.y; } // the woodcutter's tree [est]
                    sp.dropLog = NextDropLog || MomentStagePlan.Hash01(s.State.seed, (int)year, 3812) < 0.5f; // 1 ritual in 2
                    break;
                case RitualKind.Feast:
                    sp.addBoard = NextAddBoard || FeastAddsBoard(s);
                    break;
                case RitualKind.Offering:
                {
                    var start = sp.hasStash ? new Vector2(sp.stashX, sp.stashZ) : fire + ToCam(fire) * 0.3f;
                    var lead = s.RitualLead(ritual);
                    sp.town = s.State.era == Era.Town && lead != null && lead.id == s.HolderOf(RoleType.Priest)?.id;
                    sp.ritualKid = s.RitualKidThisYear?.id ?? 0;
                    bool keeper = !NextTallestTree && s.KeeperTreeGrown && PropOf(ConsequenceKind.OfferingBasket, out var basket) && TreeAt(out _, out _);
                    if (keeper && PropOf(ConsequenceKind.OfferingBasket, out var ob))
                    {
                        TreeAt(out var tree, out _);
                        var toStart = Dir2(ob.at, start);
                        var spot = ob.at + toStart * 0.12f; // the lead kneels at the basket, on the side they come from
                        sp.hasTree = true; sp.keeperTree = true; sp.treeX = tree.x; sp.treeZ = tree.y; sp.spotX = spot.x; sp.spotZ = spot.y;
                    }
                    else if (TallestTree(fire, out var trunk, out float crown))
                    {
                        var spot = trunk + TreeDir(trunk) * (0.12f + Mathf.Min(0.1f, crown * 0.3f));
                        sp.hasTree = true; sp.keeperTree = false; sp.treeX = trunk.x; sp.treeZ = trunk.y; sp.spotX = spot.x; sp.spotZ = spot.y;
                    }
                    break;
                }
            }
            NextToTable = NextFirstWalk = NextDropLog = NextAddBoard = NextTallestTree = false;
            return sp;
        }

        static ChronicleEntry EntryOf(SimCore s, int id)
        {
            var ch = s.State.chronicle;
            for (int i = ch.Count - 1; i >= 0; i--) if (ch[i].id == id) return ch[i];
            return null;
        }

        /// <summary>B72's first walk: a kid in their first autumn walking with the rest (age 3-4) [est: the camp's kids are 5 and up, so mostly the cheat].</summary>
        static Notable FirstWalkKid(SimCore s)
        {
            Notable k = null;
            foreach (var n in s.State.notables) if (n.alive && n.age >= 3f && n.age < 5f && (k == null || n.age < k.age)) k = n;
            return k;
        }

        static Notable YoungestKid(SimCore s)
        {
            Notable k = null;
            foreach (var n in s.State.notables) if (n.alive && n.age >= 3f && n.age < SimCore.AdultAge && (k == null || n.age < k.age)) k = n;
            return k;
        }

        /// <summary>B75.a before the Keeper's tree: the tallest wild tree (the biggest crown; the nearest the camp on a tie).</summary>
        bool TallestTree(Vector2 near, out Vector2 trunk, out float crown)
        {
            trunk = default; crown = 0f; float bestR = -1f, bestD = float.MaxValue;
            var box = LandBox;
            if (box == null || !box.layout.eco.on) return false;
            var eco = box.layout.eco;
            lock (box.TreeSpots)
                foreach (var kv in box.TreeSpots)
                {
                    if (kv.Key == eco.treeCell || eco.At(kv.Key) != EcoLayer.Tree) continue;
                    var p = new Vector2(kv.Value.pos.x, kv.Value.pos.z);
                    if (!ClearOfGlass(p)) continue;
                    float d = (p - near).magnitude, r = kv.Value.r;
                    if (r > bestR + 1e-3f || Mathf.Abs(r - bestR) <= 1e-3f && d < bestD) { bestR = r; bestD = d; trunk = p; crown = r; }
                }
            return bestR > 0f;
        }

        // ================================================================== the long table by boards (B74)
        /// <summary>B74: the boards added since the first feast (one a year after the first, at most 12). <paramref name="lessThisYear"/>: before this
        /// year's board is laid.</summary>
        static int TableBoards(SimCore s, bool lessThisYear)
        {
            int times = s.RitualTimes(RitualKind.Feast);
            return Mathf.Clamp(times - 1 - (lessThisYear ? 1 : 0), 0, ConsequenceMeshes.MaxBoards);
        }
        static bool FeastAddsBoard(SimCore s) => s.RitualTimes(RitualKind.Feast) >= 2; // every year after the first
        /// <summary>The table's length in box units with <paramref name="boards"/> boards.</summary>
        public static float TableLength(int boards) => (0.44f + ConsequenceMeshes.BoardLength * Mathf.Min(boards, ConsequenceMeshes.MaxBoards)) * ConsequenceMeshes.Scale;

        // ================================================================== the offering basket's look (B75)
        /// <summary>B75: full under its cloth from the offering until the spring line, empty (the cloth flat) after it. Read from the chronicle.</summary>
        int OfferingLook(int propId)
        {
            if (sim == null) return 0;
            long offered = -1, emptied = -1;
            foreach (var e in sim.State.chronicle)
            {
                if (e.kind == SimCore.RitualFirstKind(RitualKind.Offering) || e.kind == SimCore.RitualYearKind(RitualKind.Offering)) offered = e.month;
                else if (e.kind == "ritual_offering_empty") emptied = e.month;
            }
            if (StagedMoments.PlayingRitual(sim, out var sc, out float secs) && sc.plan.RitualStaged == RitualKind.Offering && secs < sc.plan.OfferingSetAt)
                return emptied >= 0 ? ConsequenceMeshes.OfferingEmpty : 0; // before the lead gets there: as it was
            if (emptyCarried) return ConsequenceMeshes.OfferingGone;
            if (offered >= 0 && offered > emptied) return 1; // full, the cloth on, food at the rim
            if (emptied >= 0) return ConsequenceMeshes.OfferingEmpty;
            return offeredDay == now.day ? 1 : 0;
        }
        bool OfferingFull => sim != null && OfferingLookNow == 1;
        int OfferingLookNow;
        /// <summary>B75.b: the empty basket is in someone's hands (it is not at the tree).</summary>
        bool emptyCarried;
        int emptyDoneFor = -1;

        // ================================================================== each frame
        void StepRituals(IReadOnlyList<PersonActivity> acts, float time)
        {
            if (sim == null || planner == null) return;
            if (PropOf(ConsequenceKind.OfferingBasket, out var ob))
            {
                emptyCarried = false;
                foreach (var s in scenes) if (s.use == CampUse.OfferingEmpty) { float u = time - s.t0; emptyCarried = u >= s.a && u < s.b; }
                OfferingLookNow = OfferingLook(ob.id);
                if (!ThingBusy(ThingOf(ob)) || emptyCarried) ConsequencePropsView.SetLook(ob.id, OfferingLookNow);
            }
            if (PropOf(ConsequenceKind.LongTable, out var table))
            {
                bool before = StagedMoments.PlayingRitual(sim, out var fsc, out float fs) && fsc.plan.RitualStaged == RitualKind.Feast && fsc.plan.BoardLaidAt > 0f && fs < fsc.plan.BoardLaidAt;
                ConsequencePropsView.SetLook(table.id, TableBoards(sim, before));
            }
            StepPurple();
            if (OnlyCheats || meals == null || meals.Eaters > 0) return;
            float h = Hour;
            var weather = planner.WeatherOn(now.day);
            bool wet = weather is Weather.KeeperRain or Weather.Storm;
            // B75.b: the morning after the spring line, the first kid who passes the tree finds it empty
            var line = LastEmptyLine();
            if (line != null && line.id != emptyDoneFor && h >= 7f && h < 14f && ob.t != null && OfferingLookNow == ConsequenceMeshes.OfferingEmpty && !ThingBusy(ThingOf(ob))
                && TryOfferingEmpty(acts, time, false, ob) == null) { emptyDoneFor = line.id; return; }
            if (h < 9f || h >= 16.5f || !PropOf(ConsequenceKind.ShoreBoat, out var boat) || ThingBusy(ThingOf(boat))) return;
            bool Go(CampUse u, float chance)
            {
                if (doneToday.Contains(u) || Roll(u) >= chance || !LifeOk(u)) return false;
                if (triedAt.TryGetValue(u, out float tr) && clock - tr < 3f && clock >= tr) return false;
                triedAt[u] = clock; return true;
            }
            if (Go(CampUse.BoatSort, Often) && TryBoatSort(acts, time, false, boat) == null) return;
            if (!wet && Go(CampUse.BoatHouse, Sometimes) && TryBoatHouse(acts, time, false, boat) == null) return;
        }

        ChronicleEntry LastEmptyLine()
        {
            var ch = sim.State.chronicle;
            long from = sim.State.month - 2;
            for (int i = ch.Count - 1; i >= 0; i--) { if (ch[i].month < from) break; if (ch[i].kind == "ritual_offering_empty") return ch[i]; }
            return null;
        }

        /// <summary>B72: from a picker's second pick their hand dots turn purple; a kid who ate 3 berries has a purple mouth. Both until the next meal.</summary>
        void StepPurple()
        {
            if (!StagedMoments.PlayingRitual(sim, out var sc, out float secs) || sc.plan.RitualStaged != RitualKind.Berrying) return;
            foreach (var m in sc.plan.members)
            {
                int i = m.steps.FindIndex(s => s.beat == StageBeat.Pick);
                if (i < 0) continue;
                var st = m.steps[i];
                float into = secs - st.t0 - st.cue;
                if (into >= MomentStagePlan.PickCycle) Wear(m.key, CampMark.PurpleHands);
                if (m.kid && into >= MomentStagePlan.PickCycle * (st.arg == 2 ? 3f : 6f)) Wear(m.key, CampMark.PurpleMouth);
            }
        }

        void Wear(int key, CampMark kind)
        {
            foreach (var w in worn5) if (w.key == key && w.kind == kind) return;
            worn5.Add((key, kind, ViewHours + 24f, -1f)); // until the next meal wipes it (B43.c)
        }

        // ================================================================== the uses
        string ForceRitual(CampUse use)
        {
            if (sim == null || planner == null || meals == null) return "no camp yet";
            var acts = pv.Activities; float time = clock;
            switch (use)
            {
                case CampUse.OfferingEmpty:
                    if (!PropOf(ConsequenceKind.OfferingBasket, out var ob)) return "no offering basket ('#693: every choice's thing' first)";
                    return TryOfferingEmpty(acts, time, true, ob) ?? "a kid finds the offering basket empty and runs to the fire with it";
                case CampUse.BoatSort:
                case CampUse.BoatHouse:
                    if (!PropOf(ConsequenceKind.ShoreBoat, out var boat)) return "no boat by the fire (the pond family's 'A home here' first)";
                    return use == CampUse.BoatSort ? TryBoatSort(acts, time, true, boat) ?? "a fisher sorts the catch on the boat" : TryBoatHouse(acts, time, true, boat) ?? "kids play house round the boat";
            }
            return "not a ritual use";
        }

        string TryOfferingEmpty(IReadOnlyList<PersonActivity> acts, float time, bool force, in PropSpot p)
        {
            int kid = Kids(acts, p.at, 1, force) is var ks && ks.Count > 0 ? ks[0] : Nobody;
            if (kid == Nobody) return "no kid free ('Two kids join the camp' first)";
            var fire = meals.HasFire ? meals.Fire : p.at + ToCam(p.at) * 0.8f;
            var grown = Pick(acts, fire, a => IsGrown(a), 3, force, new[] { kid });
            return Start(RitualScenes.OfferingEmpty(kid, At(kid).Value, grown, AtAll(grown), p.at, fire, ToCam(fire), time), p, CampUse.OfferingEmpty);
        }

        string TryBoatSort(IReadOnlyList<PersonActivity> acts, float time, bool force, in PropSpot p)
        {
            int who = PondFisher(acts, force);
            if (who == Nobody) who = WithJob(acts, JobType.Fisher, force);
            if (who == Nobody && force) who = Grown1(acts, p.at, true);
            if (who == Nobody) return "no fisher free";
            Vector2 rack;
            if (FishRackAt(out var mid, out var rackAlong, out _)) { var n = new Vector2(-rackAlong.y, rackAlong.x); if (Vector2.Dot(n, ToCam(mid)) < 0f) n = -n; rack = mid + n * 0.12f; }
            else rack = meals.HasFire ? meals.Fire + Dir2(meals.Fire, p.at) * 0.35f : p.at + ToCam(p.at) * 0.6f;
            return Start(RitualScenes.BoatSort(who, At(who).Value, p.at, Local(p, 1f, 0f), ToCam(p.at), rack, time), p, CampUse.BoatSort);
        }

        /// <summary>B77.a: the pond family's fisher first (the boat is theirs).</summary>
        int PondFisher(IReadOnlyList<PersonActivity> acts, bool force)
        {
            var boat = sim.State.consequenceProps.Find(x => x.kind == ConsequenceKind.ShoreBoat);
            var owner = boat != null ? sim.FindNotable(boat.personId) : null;
            if (owner == null || string.IsNullOrEmpty(owner.family)) return Nobody;
            foreach (var n in sim.State.notables)
                if (n.alive && n.family == owner.family && n.job == JobType.Fisher && Shown(acts, n.id) && (force || FreeKey(acts, n.id))) return n.id;
            return Nobody;
        }

        string TryBoatHouse(IReadOnlyList<PersonActivity> acts, float time, bool force, in PropSpot p)
        {
            var kids = Kids(acts, p.at, 3, force);
            if (kids.Count < 2) return "two kids needed ('Two kids join the camp' first)";
            return Start(RitualScenes.BoatHouse(kids, AtAll(kids), p.at, Local(p, 1f, 0f), ToCam(p.at), time), p, CampUse.BoatHouse);
        }

        // ================================================================== drawing
        readonly List<Transform> ritualPool = new();
        Mesh plankMesh, smallBasketMesh, heapLog, emptyBasket;

        Transform RitualPiece(Transform root, Material material, int i, Mesh mesh)
        {
            while (i < ritualPool.Count && ritualPool[i] == null) ritualPool.RemoveAt(i);
            while (ritualPool.Count <= i)
            {
                var go = new GameObject("Ritual thing") { hideFlags = HideFlags.DontSave };
                go.transform.SetParent(root, false);
                go.AddComponent<MeshFilter>();
                var r = go.AddComponent<MeshRenderer>(); r.sharedMaterial = material; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                ritualPool.Add(go.transform);
            }
            var t = ritualPool[i];
            t.GetComponent<MeshFilter>().sharedMesh = mesh;
            t.gameObject.SetActive(true);
            t.localRotation = Quaternion.identity; t.localScale = Vector3.one;
            return t;
        }

        Vector3 HandsOf(Transform root, PegDollView v) => root.InverseTransformPoint(HandsMid(v));

        void DrawRituals(Transform root, Material material, float time, System.Func<float, float, float> groundAt)
        {
            int used = 0;
            smallBasketMesh ??= ConsequenceMeshes.OneBasket(2);
            // the kids' small basket beside the offering basket, until spring (B75.a "The basket's stay")
            if (PropOf(ConsequenceKind.OfferingBasket, out var ob) && OfferingLookNow == 1 && sim.KeeperTreeGrown)
            {
                var side = new Vector2(-ToCam(ob.at).y, ToCam(ob.at).x);
                var at = ob.at + side * 0.12f + ToCam(ob.at) * 0.03f;
                var t = RitualPiece(root, material, used++, smallBasketMesh);
                t.localPosition = new Vector3(at.x, groundAt(at.x, at.y), at.y); t.localScale = Vector3.one * (ConsequenceMeshes.Scale * 0.6f);
            }
            // B75.b: the empty offering basket held up by the kid, then in the lead's arms
            foreach (var s in scenes)
            {
                if (s.use != CampUse.OfferingEmpty) continue;
                float u = time - s.t0;
                if (u < s.a || u >= s.b) continue;
                int holder = s.d > 0f && u >= s.d && s.Count > 1 ? 1 : 0;
                var v = pv.ViewOf(s.keys[holder]);
                if (v == null || !v.gameObject.activeInHierarchy) continue;
                var t = RitualPiece(root, material, used++, emptyBasket ??= ConsequenceMeshes.Build(ConsequenceKind.OfferingBasket, ConsequenceMeshes.OfferingEmpty));
                var hands = HandsOf(root, v);
                t.localPosition = new Vector3(hands.x, Mathf.Max(groundAt(hands.x, hands.z), hands.y - (holder == 0 ? 0.02f : 0.06f)), hands.z);
                t.localRotation = Quaternion.Euler(0f, v.transform.eulerAngles.y, 0f); t.localScale = Vector3.one * (ConsequenceMeshes.Scale * 0.8f);
            }
            // B77.a: the rod laid along the hull while the catch is sorted
            foreach (var s in scenes)
            {
                if (s.use != CampUse.BoatSort) continue;
                float u = time - s.t0;
                if (u < s.a || u >= s.b) continue;
                plankMesh ??= Plank();
                var t = RitualPiece(root, material, used++, RodMesh());
                t.localPosition = new Vector3(s.p0.x, groundAt(s.p0.x, s.p0.y) + RitualScenes.HullTop + 0.004f, s.p0.y);
                t.localRotation = Quaternion.LookRotation(new Vector3(s.p1.x, 0f, s.p1.y));
            }
            used = DrawRitualMoment(root, material, used, groundAt);
            for (int i = used; i < ritualPool.Count; i++) if (ritualPool[i] != null) ritualPool[i].gameObject.SetActive(false);
        }

        int DrawRitualMoment(Transform root, Material material, int used, System.Func<float, float, float> groundAt)
        {
            if (!StagedMoments.PlayingRitual(sim, out var sc, out float t)) return used;
            var plan = sc.plan;
            PegDollView V(int key) { var v = pv.ViewOf(key); return v != null && v.gameObject.activeInHierarchy ? v : null; }
            Vector3 On(float x, float z, float h = 0f) => new Vector3(x, groundAt(x, z) + h, z);
            switch (plan.RitualStaged)
            {
                case RitualKind.Woodpile:
                {
                    heapLog ??= PieceMesh(4);
                    // the heap: the logs dropped so far, less those lifted off it
                    int dropped = 0;
                    foreach (var m in plan.members) foreach (var s in m.steps) if (s.beat == StageBeat.DropLog && t >= s.t0 + 0.55f) dropped++;
                    int bringers = 0; foreach (var m in plan.members) foreach (var s in m.steps) if (s.beat == StageBeat.DropLog) bringers++;
                    int lifted = 0; for (int k = 0; k < plan.LogCount; k++) if (t >= plan.LogReaches(k, 0) + 0.2f) lifted++;
                    int onHeap = Mathf.Max(0, Mathf.Min(plan.LogCount, dropped + Mathf.Max(0, plan.LogCount - bringers)) - lifted);
                    var across = new Vector2(-plan.LineZ, plan.LineX);
                    for (int k = 0; k < onHeap; k++)
                    {
                        int row = k < 4 ? 0 : k < 7 ? 1 : 2, inRow = k < 4 ? k : k < 7 ? k - 4 : k - 7;
                        var at = new Vector2(plan.HeapX, plan.HeapZ) + across * ((inRow - (row == 0 ? 1.5f : row == 1 ? 1f : 0f)) * 0.032f);
                        var lt = RitualPiece(root, material, used++, heapLog);
                        lt.localPosition = On(at.x, at.y, 0.012f + row * 0.024f);
                        lt.localRotation = Quaternion.LookRotation(new Vector3(plan.LineX, 0f, plan.LineZ)) * Quaternion.Euler(0f, 90f + (k * 37 % 9 - 4), 0f);
                        lt.localScale = Vector3.one * LogInHeap;
                    }
                    // the dropped log on the ground at the fumbler's feet
                    if (plan.DropLogAt >= 0)
                        foreach (var m in plan.members)
                        {
                            int i = m.steps.FindIndex(s => s.beat == StageBeat.Fumble);
                            if (i < 0 || t < m.steps[i].t0 + 0.2f || t > plan.Duration) continue;
                            var st = m.steps[i];
                            var at = new Vector2(st.x0, st.z0) + new Vector2(plan.geo.nx, plan.geo.nz) * 0.05f;
                            var lt = RitualPiece(root, material, used++, heapLog);
                            float fall = Mathf.Clamp01((t - st.t0 - 0.2f) / 0.25f);
                            lt.localPosition = On(at.x, at.y, 0.012f + 0.06f * (1f - fall));
                            lt.localRotation = Quaternion.LookRotation(new Vector3(plan.LineX, 0f, plan.LineZ)) * Quaternion.Euler(0f, 20f, 0f);
                            lt.localScale = Vector3.one * LogInHeap;
                        }
                    // the short ladder on the roof and the thatch bundle, while the roof is mended
                    foreach (var m in plan.members)
                    {
                        if (m.role != MomentRole.Mender) continue;
                        int i = m.steps.FindIndex(s => s.beat == StageBeat.MendRoof);
                        if (i < 0 || t < m.steps[i].t0 || t >= m.steps[i].t1) continue;
                        var st = m.steps[i];
                        ladderMesh ??= PieceMesh(2);
                        var foot = new Vector2(st.x0, st.z0);
                        var toPile = Dir2(foot, new Vector2(plan.centreX, plan.centreZ));
                        var lt = RitualPiece(root, material, used++, ladderMesh);
                        lt.localPosition = On(foot.x - toPile.x * 0.03f, foot.y - toPile.y * 0.03f);
                        lt.localRotation = Quaternion.LookRotation(new Vector3(toPile.x, 0f, toPile.y)) * Quaternion.Euler(14f, 0f, 0f);
                        lt.localScale = Vector3.one * CampScenes6.LadderScale;
                    }
                    // the kids' twig basket at the pile's foot
                    if (plan.TwigKids > 0)
                    {
                        var at = new Vector2(plan.centreX, plan.centreZ) + new Vector2(plan.geo.nx, plan.geo.nz) * 0.17f + new Vector2(plan.LineX, plan.LineZ) * 0.1f;
                        var bt = RitualPiece(root, material, used++, smallBasketMesh);
                        bt.localPosition = On(at.x, at.y); bt.localScale = Vector3.one * (ConsequenceMeshes.Scale * 0.6f);
                    }
                    break;
                }
                case RitualKind.Feast:
                {
                    // the new board carried between the two
                    if (plan.BoardA != 0 && V(plan.BoardA) is PegDollView a && V(plan.BoardB) is PegDollView b
                        && plan.StepAt(plan.BoardA, t, out var am, out int ai) && am.steps[ai].beat == StageBeat.CarryBoard)
                    {
                        plankMesh ??= Plank();
                        var ha = HandsOf(root, a); var hb = HandsOf(root, b);
                        var mid = (ha + hb) * 0.5f;
                        var bt = RitualPiece(root, material, used++, plankMesh);
                        var dir = hb - ha; dir.y = 0f;
                        bt.localPosition = mid - Vector3.up * 0.01f;
                        bt.localRotation = Quaternion.LookRotation(dir.sqrMagnitude > 1e-6f ? dir.normalized : Vector3.forward);
                    }
                    // the baskets set on the table
                    int baskets = 0;
                    foreach (var m in plan.members) foreach (var s in m.steps) if (s.beat == StageBeat.SetDown && s.arg == (int)StageHold.Basket && t >= s.t0 + s.cue + 0.6f) baskets++;
                    for (int k = 0; k < Mathf.Min(baskets, 6); k++)
                    {
                        float along = (k - (Mathf.Min(baskets, 6) - 1) * 0.5f) * 0.09f;
                        var at = new Vector2(plan.centreX + plan.AxisX * along, plan.centreZ + plan.AxisZ * along) + new Vector2(plan.NearX, plan.NearZ) * 0.025f * (k % 2 == 0 ? 1f : -1f);
                        var bt = RitualPiece(root, material, used++, smallBasketMesh);
                        bt.localPosition = On(at.x, at.y, PropGeo.TableTop); bt.localScale = Vector3.one * (ConsequenceMeshes.Scale * 0.45f);
                    }
                    break;
                }
                case RitualKind.Offering:
                {
                    // the kids' small basket between their hands, then set down beside the offering basket
                    var kids = plan.members.FindAll(m => m.steps.Exists(s => s.beat == StageBeat.KidCarry));
                    if (kids.Count > 0 && t < plan.OfferingSetAt + 0.2f)
                    {
                        var va = V(kids[0].key); var vb = kids.Count > 1 ? V(kids[1].key) : null;
                        if (va != null && plan.StepAt(kids[0].key, t, out var km, out int ki) && km.steps[ki].hold == StageHold.SmallBasket)
                        {
                            var hp = vb != null ? (HandsOf(root, va) + HandsOf(root, vb)) * 0.5f : HandsOf(root, va);
                            var bt = RitualPiece(root, material, used++, smallBasketMesh);
                            bt.localPosition = new Vector3(hp.x, Mathf.Max(groundAt(hp.x, hp.z), hp.y - 0.035f), hp.z);
                            bt.localScale = Vector3.one * (ConsequenceMeshes.Scale * 0.6f);
                        }
                    }
                    else if (kids.Count > 0 && !(sim.KeeperTreeGrown && OfferingLookNow == 1))
                    {   // the tallest tree (or the camp): the small basket stays beside the set-down one for the rest of the moment
                        var st = kids[0].steps.Find(s => s.beat == StageBeat.KneelSet);
                        var at = new Vector2(st.x0, st.z0) + new Vector2(plan.geo.nx, plan.geo.nz) * -0.04f;
                        var bt = RitualPiece(root, material, used++, smallBasketMesh);
                        bt.localPosition = On(at.x, at.y); bt.localScale = Vector3.one * (ConsequenceMeshes.Scale * 0.6f);
                    }
                    // the best basket left at the tallest tree (no offering basket there to tip it into)
                    var lead = plan.members.Find(m => m.role == MomentRole.Leader);
                    var kneel = lead?.steps.Find(s => s.beat == StageBeat.KneelSet) ?? default;
                    if (lead != null && kneel.beat == StageBeat.KneelSet && kneel.arg == 0 && t >= kneel.t0 + 1.2f)
                    {
                        oneBaskets ??= new[] { ConsequenceMeshes.OneBasket(0), ConsequenceMeshes.OneBasket(1), ConsequenceMeshes.OneBasket(2) };
                        var dx = plan.OfferingX - kneel.x0; var dz = plan.OfferingZ - kneel.z0;
                        var at = new Vector2(kneel.x0, kneel.z0) + new Vector2(plan.geo.nx, plan.geo.nz) * 0f + new Vector2(dx, dz).normalized * 0.08f;
                        var bt = RitualPiece(root, material, used++, oneBaskets[2]);
                        bt.localPosition = On(at.x, at.y); bt.localScale = Vector3.one * ConsequenceMeshes.Scale;
                    }
                    break;
                }
            }
            return used;
        }

        /// <summary>A log in the heap is drawn at this scale of the woodpile's own log (0.26 long before the prop scale) [est].</summary>
        public const float LogInHeap = 0.62f;

        static Mesh Plank()
        {
            var mb = new MeshBuilder(7210);
            mb.Box(new Vector3(0f, 0f, 0f), new Vector3(0.15f, 0.012f, 0.06f), Palette.WoodLight); // across the table's width (0.1 × the prop scale)
            var m = mb.ToMesh("Ritual_Plank"); m.hideFlags = HideFlags.DontSave; return m;
        }

        static Mesh rodMesh;
        static Mesh RodMesh()
        {
            if (rodMesh != null) return rodMesh;
            var mb = new MeshBuilder(7211);
            mb.Box(new Vector3(0f, 0f, 0f), new Vector3(0.006f, 0.006f, 0.3f), Palette.Timber);
            mb.Box(new Vector3(0f, 0f, 0.15f), new Vector3(0.002f, 0.002f, 0.06f), new Color(0.85f, 0.85f, 0.8f)); // the line, wound at the tip
            rodMesh = mb.ToMesh("Ritual_Rod"); rodMesh.hideFlags = HideFlags.DontSave; return rodMesh;
        }
    }
}
