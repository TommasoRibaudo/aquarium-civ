// #776 (docs/design/animation-specs.md B81, approved 2026-11-26): CampLife's side of the founder's statue. (1) The raising, the one held Story scene
// (View/Moments/StoryMoment.cs asks StartStory, which calls TryStatueRaise): a carver and a helper from the free grown-ups (a mason first), the
// founder's family if any are alive (up to 3), watchers; the builders are View/Beats/StatueScenes*.cs. (2) What it leaves, drawn from the sim in
// every village and town: the plinth and the figure on the green (the spot is the sim's: SimCore.FindStatueSite), the season's wear, the bunch laid at
// its foot until night; the figure is solid (PeopleView.StatueSolid). (3) The people's uses (B81.b-f): kids climb its base, the anniversary
// flowers, an old one sits at its foot, a nod in passing, the weekly sweep. Cheats: SliceDebugPanel's "Statue (#776)" block.
using System;
using System.Collections.Generic;
using AquariumCiv.Life;
using AquariumCiv.Sim;
using UnityEngine;
using Terrain = AquariumCiv.Sim.Settle.Terrain;

namespace AquariumCiv.View
{
    public sealed partial class CampLife
    {
        const int ThingStatue = 73;

        static bool IsStatueLife(CampUse u) => u >= CampUse.StatueClimb && u <= CampUse.StatueSweep;

        /// <summary>Tests: the statue drawn this frame, its stage, whether the rolled block and the bunch at its foot are drawn.</summary>
        public bool StatueDrawn { get; private set; }
        public StatueStage StatueStageDrawn { get; private set; }
        public StatueWear StatueWearDrawn { get; private set; }
        public bool BlockDrawn { get; private set; }
        public bool BunchDrawn { get; private set; }
        /// <summary>Tests: where the statue is drawn (box x, z) and how it is turned (degrees, as drawn).</summary>
        public Vector2 StatueDrawnAt { get; private set; }
        public float StatueDrawnYaw { get; private set; }

        long statueBunchDay = long.MinValue; bool statueBunchSprig;
        long statueDay = long.MinValue;
        float statueRaiseEnd = float.MinValue;
        readonly HashSet<int> statueInside = new(), statueCrossed = new();
        readonly Dictionary<string, Mesh> statueMeshes = new();
        Mesh statueBlockMesh, statueBunchMesh, statueSprigMesh;

        void ResetStatue()
        {
            statueBunchDay = long.MinValue; statueDay = long.MinValue; statueRaiseEnd = float.MinValue;
            statueInside.Clear(); statueCrossed.Clear();
            PeopleView.StatueSolid = null;
        }

        void DestroyStatue()
        {
            foreach (var m in statueMeshes.Values) if (m != null) UnityEngine.Object.Destroy(m);
            statueMeshes.Clear();
            foreach (var m in new[] { statueBlockMesh, statueBunchMesh, statueSprigMesh }) if (m != null) UnityEngine.Object.Destroy(m);
            statueBlockMesh = statueBunchMesh = statueSprigMesh = null;
            PeopleView.StatueSolid = null;
        }

        // ================================================================== where, and what stands
        /// <summary>The statue's spot (box x, z) and the yaw it is drawn at (the doll's front is local -z), from the sim's site on the green.</summary>
        public bool StatueSite(out Vector2 at, out float yaw)
        {
            at = default; yaw = 0f;
            if (sim == null || !sim.StatueStands || !sim.FindStatueSite(out float x, out float z, out float y)) return false;
            at = new Vector2(x, z); yaw = y + 180f; // FaceYaw is the direction it faces (atan2 x, z); the doll faces local -z
            return true;
        }

        /// <summary>The statue as it stands now (false: none is drawn, its moment still waits for the Keeper or no box): the raising's stage, or the
        /// carved figure. <paramref name="raise"/>: the raising playing now.</summary>
        bool StatueNow(float time, out StatueStage stage, out CampScene raise)
        {
            stage = StatueStage.Carved; raise = null;
            if (sim == null || !sim.StatueStands) return false;
            foreach (var s in scenes) if (s.use == CampUse.StatueRaise) raise = s;
            if (raise != null)
            {
                float u = time - raise.t0; statueRaiseEnd = raise.End;
                stage = u < raise.a + 0.25f ? StatueStage.Plinth : u < raise.b ? StatueStage.Block : u < raise.c ? StatueStage.Rough : u < raise.d ? StatueStage.Headed : StatueStage.Carved;
                return true;
            }
            if (time >= statueRaiseEnd && time < statueRaiseEnd + 1.5f) return true; // the scene just ended: its moment closes a frame later, the figure stays
            return StoryWaiting(StoryEvent.FounderStatue) == null; // its moment still waits for the Keeper (or plays, before its scene starts): nothing stands yet
        }

        /// <summary>The statue is drawn now (tests, the uses).</summary>
        bool StatueShown(float time, out Vector2 at) { at = default; return StatueSite(out at, out _) && StatueNow(time, out _, out _); }

        Vector2 GreenCentre() => sim?.Settlement?.Map != null ? new Vector2(sim.Settlement.Map.greenX, sim.Settlement.Map.greenZ) : Vector2.zero;

        // ================================================================== B81.a the raising (the held Story scene)
        bool KinOf(Notable n, Notable dead) =>
            n.partnerId == dead.id || dead.partnerId == n.id || n.parentId == dead.id || dead.parentId == n.id
            || n.parentId >= 0 && n.parentId == dead.parentId
            || n.parentId >= 0 && sim.FindNotable(n.parentId) is Notable p && p.parentId == dead.id;

        /// <summary>A person the scene may take: drawn, and free (a cheat: not in a scene, a hunt or a tent).</summary>
        bool CastableKey(IReadOnlyList<PersonActivity> acts, int k, bool force)
        {
            foreach (var a in acts)
                if (a.who.Key == k)
                    return At(k).HasValue && (force ? !byKey.ContainsKey(k) && a.hunt == HuntPhase.None && a.tent == TentPhase.None && !HeldElsewhere(a) : VFree(a));
            return false;
        }

        float AgeOfKey(int k) => k > 0 && sim.FindNotable(k) is Notable n ? n.age : 30f;

        /// <summary>The carver's rank: a mason, then a woodcutter or toolmaker, then any grown-up (B81.2's cast).</summary>
        int CarverRank(int k)
        {
            string job = k > 0 ? sim.FindNotable(k)?.job : null;
            return job == JobType.Mason ? 0 : job == JobType.Woodcutter || job == JobType.Toolmaker ? 1 : 2;
        }

        string TryStatueRaise(IReadOnlyList<PersonActivity> acts, float time, int named, bool force)
        {
            if (sim == null || !sim.StatueStands) return "no statue is due ('Statue: raise now' first)";
            if (!StatueSite(out var spot, out _)) return "no open ground on the green for it";
            var dead = sim.FindNotable(named >= 0 ? named : sim.StatueFor) ?? sim.StatueNotable;
            var toCam = ToCam(spot); var acr = new Vector2(-toCam.y, toCam.x);
            // the block comes from the camera's right, else its left, where the box and the pond leave room
            Vector2 from = spot + acr * 0.65f;
            bool Bad(Vector2 p) => !Terrain.InBox(p.x, p.y, 0.5f) || LandBox != null && LandBox.land.IsWater(p.x, p.y);
            if (Bad(from)) from = spot - acr * 0.65f;
            if (Bad(from)) from = spot + toCam * 0.65f;
            // the carver and the helper: free grown-ups near the green, a mason first
            var cand = VPick(acts, spot, a => IsGrown(a), 8, force);
            cand.Sort((x, y) => CarverRank(x).CompareTo(CarverRank(y)));
            if (cand.Count == 0) return "no grown-up free for the carver ('Jump: a village box' first)";
            int carver = cand[0], helper = cand.Count > 1 ? cand[1] : Nobody;
            // the founder's family if any are alive (up to three, the oldest first), then up to three more at the green watching
            var fam = new List<int>(); var famKid = new List<bool>();
            if (dead != null)
            {
                var kin = new List<Notable>();
                foreach (var n in sim.State.notables)
                    if (n.alive && n.id != carver && n.id != helper && n.id != dead.id && KinOf(n, dead) && CastableKey(acts, n.id, force)) kin.Add(n);
                kin.Sort((x, y) => y.age.CompareTo(x.age));
                for (int i = 0; i < kin.Count && fam.Count < 3; i++) { fam.Add(kin[i].id); famKid.Add(kin[i].age < SimCore.AdultAge); }
            }
            var taken = new List<int> { carver }; if (helper != Nobody) taken.Add(helper); taken.AddRange(fam);
            var watch = VPick(acts, spot, a => IsGrown(a) || IsKid(a), 3, force, taken);
            var watchKid = new List<bool>(); foreach (int w in watch) watchKid.Add(KeyIs(acts, w, a => IsKid(a)));
            var sc = StatueScenes.Raise(carver, At(carver).Value, helper, helper != Nobody ? At(helper).Value : default, fam, AtAll(fam), famKid, watch, AtAll(watch), watchKid, spot, from, toCam, time);
            Add(Thing(sc, ThingStatue));
            return null;
        }

        // ================================================================== the people's uses (B81.b-f)
        const float NodReach = 0.5f;

        bool StGo(CampUse u, float chance)
        {
            if (doneToday.Contains(u) || !LifeOk(u) || ThingBusy(ThingStatue)) return false;
            if (Roll(u) >= chance) { Why(u, "not today's roll"); return false; }
            if (triedAt.TryGetValue(u, out float tr) && clock - tr < 3f && clock >= tr) return false;
            triedAt[u] = clock; return true;
        }

        /// <summary>A kid of 5 or more (a notable's age is known; a crowd kid counts).</summary>
        bool KidFive(in PersonActivity a) => IsKid(a) && (a.who.Key <= 0 || sim.FindNotable(a.who.Key) is not Notable n || n.age >= 5f);

        string TryStatueClimb(IReadOnlyList<PersonActivity> acts, float time, bool force)
        {
            if (!StatueShown(time, out var spot)) return "no statue stands ('Statue: stand it' first)";
            var kids = VPick(acts, spot, a => KidFive(a), 2, force);
            if (kids.Count == 0) return "no kid free ('Two kids join the camp' first)";
            int g = VGrown(acts, spot, force, kids);
            if (g == Nobody) return "no grown-up free to stand by (a kid never climbs alone)";
            return StartV(StatueScenes.Climb(kids[0], At(kids[0]).Value, g, At(g).Value, kids.Count > 1 ? kids[1] : Nobody, kids.Count > 1 ? At(kids[1]).Value : default, spot, ToCam(spot), time), ThingStatue, CampUse.StatueClimb);
        }

        string TryStatueFlowers(IReadOnlyList<PersonActivity> acts, float time, bool force)
        {
            if (!StatueShown(time, out var spot)) return "no statue stands ('Statue: stand it' first)";
            var dead = sim.StatueNotable;
            // the nearest living family member, else the oldest grown-up free
            int bearer = Nobody; float best = float.MaxValue;
            if (dead != null)
                foreach (var n in sim.State.notables)
                {
                    if (!n.alive || n.id == dead.id || !KinOf(n, dead) || !CastableKey(acts, n.id, force)) continue;
                    float d = (At(n.id).Value - spot).sqrMagnitude;
                    if (d < best) { best = d; bearer = n.id; }
                }
            if (bearer == Nobody)
            {
                var gs = VPick(acts, spot, a => IsGrown(a), 6, force);
                float age = -1f;
                foreach (int k in gs) if (AgeOfKey(k) > age) { age = AgeOfKey(k); bearer = k; }
            }
            if (bearer == Nobody) return "no one free to bring flowers";
            var others = VPick(acts, spot, a => IsGrown(a), 2, force, new[] { bearer });
            bool winter = LifeClock.SeasonOf(now.day) == Season.Winter;
            return StartV(StatueScenes.Flowers(bearer, At(bearer).Value, others, AtAll(others), winter, spot, ToCam(spot), time), ThingStatue, CampUse.StatueFlowers);
        }

        string TryStatueSit(IReadOnlyList<PersonActivity> acts, float time, bool force)
        {
            if (!StatueShown(time, out var spot)) return "no statue stands ('Statue: stand it' first)";
            var gs = VPick(acts, spot, a => IsGrown(a), 6, force);
            if (gs.Count == 0) return "no grown-up free";
            int who = gs[0]; float age = -1f; // an old one first
            foreach (int k in gs) if (AgeOfKey(k) > age) { age = AgeOfKey(k); who = k; }
            int second = BeatHash.U01((int)(today & 0x7fffffff), who, 6992) < 0.4f || force ? VGrown(acts, spot, force, new[] { who }) : Nobody;
            var toCam = ToCam(spot);
            var side = GreenCentre() - spot; side = side.sqrMagnitude > 1e-4f ? side.normalized : Vector2.up;
            if (Vector2.Dot(side, toCam) > 0.7f) { float r = 70f * Mathf.Deg2Rad, c = Mathf.Cos(r), s = Mathf.Sin(r); side = new Vector2(side.x * c - side.y * s, side.x * s + side.y * c); } // not the camera's side
            float secs = BeatHash.Range(6f, 14f, (int)(today & 0x7fffffff), who, 6993);
            return StartV(StatueScenes.Sit(who, At(who).Value, second, second != Nobody ? At(second).Value : default, spot, side, secs, time), ThingStatue, CampUse.StatueSit);
        }

        string TryStatueSweep(IReadOnlyList<PersonActivity> acts, float time, bool force)
        {
            if (!StatueShown(time, out var spot)) return "no statue stands ('Statue: stand it' first)";
            if (!force && sim.Sweepers == 0) return "no sweepers (a town)";
            // the town's sweepers: a notable sweeper if one is free, else a grown-up of the crowd
            int who = Nobody;
            foreach (var n in sim.State.notables) if (n.alive && n.job == JobType.Sweeper && CastableKey(acts, n.id, force)) { who = n.id; break; }
            if (who == Nobody) who = VGrown(acts, spot, force);
            if (who == Nobody) return "no sweeper free";
            return StartV(StatueScenes.Sweep(who, At(who).Value, spot, ToCam(spot), time), ThingStatue, CampUse.StatueSweep);
        }

        bool KnewThem(int key)
        {
            var dead = sim.StatueNotable;
            if (dead == null) return false;
            if (key > 0 && sim.FindNotable(key) is Notable n) return n.bornMonth <= dead.diedMonth; // alive when they were
            return BeatHash.U01((int)(today & 0x7fffffff), key, 6994) < 0.5f; // the crowd: some did
        }

        /// <summary>B81.e: a grown-up whose walk crosses the green within <see cref="NodReach"/> of it nods (about 1 in 3 crossings; never the first
        /// crossing of the day for the same person).</summary>
        string TryStatueNod(IReadOnlyList<PersonActivity> acts, float time, bool force)
        {
            if (!StatueShown(time, out var spot)) return "no statue stands ('Statue: stand it' first)";
            int who = Nobody; float best = float.MaxValue;
            foreach (var a in acts)
            {
                int k = a.who.Key;
                if (!IsGrown(a) || byKey.ContainsKey(k) || !(At(k) is Vector2 p)) continue;
                if (!force && (!a.moving || !VFree(a))) continue;
                if (force && (a.hunt != HuntPhase.None || a.tent != TentPhase.None || HeldElsewhere(a))) continue;
                float d = (p - spot).sqrMagnitude;
                if (d < best && (force || d < NodReach * NodReach)) { best = d; who = k; }
            }
            if (who == Nobody) return "no grown-up walking by";
            return StartV(StatueScenes.Nod(who, At(who).Value, KnewThem(who), spot, ToCam(spot), time), ThingStatue, CampUse.StatueNod);
        }

        /// <summary>Each frame (every era; the uses in a village or a town): the statue's solid, the bunch it keeps until night, the natural uses.</summary>
        void StepStatue(IReadOnlyList<PersonActivity> acts, float time)
        {
            if (sim == null) { PeopleView.StatueSolid = null; return; }
            CampScene raise = null;
            bool shown = StatueSite(out var at, out _) && StatueNow(time, out _, out raise);
            PeopleView.StatueSolid = shown ? new Solid(at.x, at.y, 0.09f) : (Solid?)null;
            if (now.day != statueDay) { statueDay = now.day; statueCrossed.Clear(); statueInside.Clear(); }
            foreach (var s in scenes)
            {
                float u = time - s.t0;
                if (s.use == CampUse.StatueRaise && u >= s.p1.y) { statueBunchDay = now.day; statueBunchSprig = LifeClock.SeasonOf(now.day) == Season.Winter; }
                else if (s.use == CampUse.StatueFlowers && u >= s.a) { statueBunchDay = now.day; statueBunchSprig = s.kind == 1; }
            }
            if (!shown || !VillageEra || planner == null || OnlyCheats || meals == null || meals.Eaters > 0 || raise != null) return;
            if (Hour >= LastStart || Hour < 6.5f) return;
            float h = Hour;
            var season = LifeClock.SeasonOf(now.day);
            var weather = planner.WeatherOn(now.day);
            bool wet = weather is Weather.KeeperRain or Weather.Storm or Weather.Snow;
            // B81.c: the anniversary of the death, in the morning, once a year
            if (sim.StatueAnniversary && h >= 8f && h < 11f && StGo(CampUse.StatueFlowers, 1f) && Tried(CampUse.StatueFlowers, TryStatueFlowers(acts, time, false))) return;
            // B81.f (work): the weekly clean of the green, one view day in seven, in the morning, a town
            if (sim.State.era == Era.Town && now.day % 7 == 3 && h >= 8f && h < 10f && StGo(CampUse.StatueSweep, 1f) && Tried(CampUse.StatueSweep, TryStatueSweep(acts, time, false))) return;
            // B81.d: an old one sits at its foot, midday or evening, often; not in rain or on a snow day
            bool sitHour = h >= 11.5f && h < 14f || h >= 17f && h < 19f;
            if (sitHour && !wet && StGo(CampUse.StatueSit, Often) && Tried(CampUse.StatueSit, TryStatueSit(acts, time, false))) return;
            // B81.b: kids climb its base, daylight, not in winter, sometimes
            if (h >= 9f && h < 16.5f && !wet && season != Season.Winter && StGo(CampUse.StatueClimb, Sometimes) && Tried(CampUse.StatueClimb, TryStatueClimb(acts, time, false))) return;
            // B81.e: a nod in passing: a grown-up newly within reach of it, not the day's first crossing, about 1 in 3
            if (ThingBusy(ThingStatue)) return;
            foreach (var a in acts)
            {
                int k = a.who.Key;
                if (!a.moving || !IsGrown(a) || byKey.ContainsKey(k) || !(At(k) is Vector2 p)) { if (!byKey.ContainsKey(k)) statueInside.Remove(k); continue; }
                if ((p - at).magnitude > NodReach) { statueInside.Remove(k); continue; }
                if (!statueInside.Add(k)) continue; // still the same crossing
                if (statueCrossed.Add(k)) continue; // the first crossing of the day: no nod
                if (BeatHash.U01((int)(today & 0x7fffffff), k, 6995 + statueInside.Count) >= 0.34f || !LifeOk(CampUse.StatueNod)) continue;
                if (TryStatueNod(acts, time, false) == null) return;
            }
        }

        // ================================================================== drawing
        Mesh StatueMeshFor(StatueStage stage, StatueWear wear)
        {
            var who = sim.StatueNotable;
            var tell = sim.StatueTell(who);
            string key = $"{(int)stage}/{(int)wear}/{(int)tell}/{who?.id}";
            if (statueMeshes.TryGetValue(key, out var m) && m != null) return m;
            var mb = new MeshBuilder(7761 + (who != null ? who.id : 0)) { jitter = 0.025f };
            PegDoll.Statue(mb, Vector3.zero, 1f, who, stage, wear, tell);
            m = mb.ToMesh("Statue_" + key); m.hideFlags = HideFlags.DontSave;
            return statueMeshes[key] = m;
        }

        static Mesh StatueBlockMesh()
        {
            var mb = new MeshBuilder(7762);
            mb.Box(new Vector3(0f, 0.05f, 0f), new Vector3(0.06f, 0.1f, 0.06f), PegDoll.StatueStone);
            var m = mb.ToMesh("Statue_Block"); m.hideFlags = HideFlags.DontSave; return m;
        }

        /// <summary>The bunch laid at the foot (0.03 u across): a few short stems with a flower each; in winter a dark sprig.</summary>
        static Mesh StatueBunchMeshOf(bool sprig)
        {
            var mb = new MeshBuilder(sprig ? 7764 : 7763);
            for (int i = 0; i < 5; i++)
            {
                float a = i * 72f * Mathf.Deg2Rad, r = 0.008f;
                var at = new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
                if (sprig)
                {
                    mb.Box(at + new Vector3(0f, 0.006f, 0f), new Vector3(0.004f, 0.012f, 0.014f), Palette.Pine, Quaternion.Euler(0f, i * 40f, (i - 2) * 12f));
                    mb.Ico(at + new Vector3(0f, 0.012f, 0f), 0.006f, Vector3.one, Palette.Pine, 0.15f);
                }
                else
                {
                    mb.Box(at + new Vector3(0f, 0.005f, 0f), new Vector3(0.002f, 0.01f, 0.002f), Palette.LeafA, Quaternion.Euler((i - 2) * 10f, 0f, (i - 2) * 8f));
                    mb.Ico(at + new Vector3(0f, 0.011f, 0f), 0.0055f, Vector3.one, Palette.Flowers[i % Palette.Flowers.Length], 0.1f);
                }
            }
            var m = mb.ToMesh(sprig ? "Statue_Sprig" : "Statue_Bunch"); m.hideFlags = HideFlags.DontSave; return m;
        }

        int DrawStatue(Transform root, Material material, float time, Func<float, float, float> groundAt, int used)
        {
            StatueDrawn = BlockDrawn = BunchDrawn = false;
            if (sim == null || !StatueSite(out var at, out float yaw) || !StatueNow(time, out var stage, out var raise)) return used;
            var season = LifeClock.SeasonOf(now.day);
            var wear = season == Season.Winter ? StatueWear.Snow : season == Season.Autumn ? StatueWear.Leaves : StatueWear.None;
            float gy = groundAt(at.x, at.y);
            var rot = Quaternion.Euler(0f, yaw, 0f);
            var t = StoryPiece(root, material, used++, StatueMeshFor(stage, wear));
            t.localPosition = new Vector3(at.x, gy, at.y); t.localRotation = rot;
            StatueDrawn = true; StatueStageDrawn = stage; StatueWearDrawn = wear; StatueDrawnAt = at; StatueDrawnYaw = yaw;
            // the block rolled in, hopping up onto the plinth at the seat
            if (raise != null && StatueScenes.BlockNow(raise, time - raise.t0, at, out var bat, out float bh))
            {
                var b = StoryPiece(root, material, used++, statueBlockMesh ??= StatueBlockMesh());
                b.localPosition = new Vector3(bat.x, groundAt(bat.x, bat.y) + bh, bat.y); b.localRotation = rot;
                BlockDrawn = true;
            }
            // the bunch laid at its foot, until night
            bool night = Hour >= 20f || Hour < 5.5f;
            if (statueBunchDay == now.day && !night && stage == StatueStage.Carved || statueBunchDay == now.day && !night && raise != null && time - raise.t0 >= raise.p1.y)
            {
                var toCam = ToCam(at);
                var foot = at + (toCam.sqrMagnitude > 1e-6f ? toCam.normalized : Vector2.down) * 0.07f;
                var bunch = StoryPiece(root, material, used++, statueBunchSprig ? (statueSprigMesh ??= StatueBunchMeshOf(true)) : (statueBunchMesh ??= StatueBunchMeshOf(false)));
                bunch.localPosition = new Vector3(foot.x, gy + PegDoll.StatueStepH, foot.y); bunch.localRotation = Quaternion.Euler(0f, 23f, 0f);
                BunchDrawn = true;
            }
            return used;
        }
    }
}

namespace AquariumCiv.View
{
    /// <summary>#776 cheats (SliceDebugPanel's "Statue (#776)" block; B81.4).</summary>
    public static partial class CampCheats
    {
        static AquariumCiv.Sim.SimCore StatueSim => AquariumCiv.Game.GameClock.Instance != null ? AquariumCiv.Game.GameClock.Instance.Sim : null;

        /// <summary>"Statue: raise now": jumps to a village box if the box is a camp, picks the person (the most pinned, else the first notable; a
        /// living one has died a year back), clears what plays, and the people raise it on the green now. Names the person in the result.</summary>
        public static string StatueRaiseNow()
        {
            var sim = StatueSim;
            if (sim == null) return "no living box";
            if (CampLife.CheatWaitsFor is string wait) return wait; // #799: not over the last evening (the statue stays as it is)
            string jump = sim.State.era == Era.Camp ? StageVillage(10f, false, 4, false) + " | " : "";
            sim.DebugStatueRemove();
            return jump + StoryMoment.DebugPlayNow(StoryEvent.FounderStatue);
        }

        /// <summary>The statue stands (no scene): the setup for the uses' cheats. A village box first if the box is a camp.</summary>
        public static string StatueStand()
        {
            var sim = StatueSim;
            if (sim == null) return "no living box";
            string jump = sim.State.era == Era.Camp ? StageVillage(10f, false, 4, false) + " | " : "";
            var who = sim.DebugStatueStand(out string why);
            if (who == null) return "not now: " + why;
            if (!sim.FindStatueSite(out float x, out float z, out _)) return jump + $"{who.name}'s statue has no open ground on the green";
            return jump + $"{who.name}'s statue stands on the green at {x:0.00}, {z:0.00}";
        }

        /// <summary>A use's cheat: make sure the statue stands, then start the use now with the people nearest.</summary>
        static string StatueUse(CampUse use)
        {
            var sim = StatueSim;
            if (sim == null) return "no living box";
            string pre = "";
            if (!sim.StatueStands || sim.State.era == Era.Camp) pre = StatueStand() + " | ";
            if (use == CampUse.StatueFlowers && !sim.StatueAnniversary) { /* a cheat's flowers play on any day: the cast needs no anniversary */ }
            return pre + Free(use);
        }

        public static string StatueClimb() => StatueUse(CampUse.StatueClimb);
        public static string StatueFlowers() => StatueUse(CampUse.StatueFlowers);
        public static string StatueSit() => StatueUse(CampUse.StatueSit);
        public static string StatueNod() => StatueUse(CampUse.StatueNod);
        public static string StatueSweep() => StatueUse(CampUse.StatueSweep);
        /// <summary>The raising's scene again (no new line), with the people the box has for it.</summary>
        public static string StatueRaiseAgain() => StatueUse(CampUse.StatueRaise);

        /// <summary>"Statue: skip to the anniversary": the person's death is moved so that today is its day, a year on; it is morning.</summary>
        public static string StatueSkipToAnniversary()
        {
            var sim = StatueSim;
            if (sim == null) return "no living box";
            if (!sim.StatueStands) { string r = StatueStand(); if (!sim.StatueStands) return r; }
            var who = sim.DebugStatueMakeAnniversary();
            PeopleView.DebugSetHour(9f);
            return who == null ? "nobody" : $"today is the anniversary of {who.name}'s death (a year on), morning";
        }

        /// <summary>"Founder: die now": the person the statue would be for (the most pinned, else the first notable) dies this month.</summary>
        public static string FounderDie()
        {
            var sim = StatueSim;
            if (sim == null) return "no living box";
            var who = sim.DebugFounderDie();
            return who == null ? "nobody in the box" : $"{who.name} has died (diedMonth {who.diedMonth}); 'Statue: skip a year' lets the rule fire";
        }

        /// <summary>"Statue: skip a year": the death a year further back, so the wait is over.</summary>
        public static string StatueSkipYear()
        {
            var sim = StatueSim;
            if (sim == null) return "no living box";
            var who = sim.DebugStatueSkipYear();
            return who == null ? "nobody in the box" : $"{who.name} died {(sim.State.month - who.diedMonth) / SimConfig.MonthsPerYear:0.#} years ago";
        }

        /// <summary>"Statue: remove": nobody stands, and the raising can be replayed.</summary>
        public static string StatueRemove()
        {
            var sim = StatueSim;
            if (sim == null) return "no living box";
            sim.DebugStatueRemove();
            return "the statue is gone; 'Statue: raise now' plays the raising again";
        }
    }
}
