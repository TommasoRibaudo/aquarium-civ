// #692 part 2 (docs/design/animation-specs.md "#692 part 2: the Keeper's tree visits", B32.a-h): CampLife's side of the visits. Which
// visit starts (the D rules: life uses borrow free time only; how often by the view day's roll; one use per thing at a time), the cheats,
// and what the visits draw beyond the dolls: the newborn lifted (B32.f), the tree's glow coming up (B32.d), this death's charm held back
// until it is tied (B32.d), the ambience dipping 30 % for a quiet visit (B32.d), the falling leaf, the flower, the charm in a hand.
// The timelines are the pure builders in View/Beats/CampScenes2.cs; the rules for who and when are TreeVisits there.
using System.Collections.Generic;
using AquariumCiv.Life;
using AquariumCiv.Sim;
using UnityEngine;

namespace AquariumCiv.View
{
    public sealed partial class CampLife
    {
        const int ThingTree = 4;
        /// <summary>B32.b: at most one touch per this many seconds.</summary>
        public const float TouchGap = 30f;
        /// <summary>B32.a: how long a natural sitter stays (s); the old one's afternoon sit (B32.g, s).</summary>
        public const float SitSeconds = 10f, OldSitSeconds = 30f;
        /// <summary>B32.d: the ambience under a quiet visit (Sound: "the ambience dips 30 %").</summary>
        public const float QuietHush = 0.7f;

        // what each kind of visit has done already: a death's two visits (dead id x 4 + which), a wedding (its chronicle id), a season's newborn
        static readonly HashSet<long> quietDone = new();
        static readonly HashSet<int> weddingsDone = new();
        static long newbornSeason = long.MinValue;
        float lastTouch = -99f, glowAt = -99f;
        Vector3 glowWorld;
        Transform lastRoot;
        readonly Dictionary<int, Vector2> walkPrev = new();

        void ResetTree() { lastTouch = -99f; glowAt = -99f; walkPrev.Clear(); }

        /// <summary>Tests: forget which deaths, weddings and newborns were visited (a new box).</summary>
        public static void ForgetTreeVisits() { quietDone.Clear(); weddingsDone.Clear(); newbornSeason = long.MinValue; }

        static bool IsTreeUse(CampUse u) => u is CampUse.TreeSit or CampUse.TreeTouch or CampUse.TreeCircle or CampUse.TreeQuiet or CampUse.TreeWed or CampUse.TreeNewborn or CampUse.TreeOld or CampUse.TreeTell;

        bool TreeAt(out Vector2 tree, out int stage)
        {
            tree = default; stage = 0;
            if (sim == null || !sim.EcologyOn) return false;
            var t = sim.EcoTree;
            if (!t.planted) return false;
            tree = new Vector2(t.x, t.z); stage = t.stage;
            return true;
        }

        Vector2 TreeDir(Vector2 tree) { var d = TowardCamera(tree) - tree; return d.sqrMagnitude > 1e-6f ? d.normalized : Vector2.down; }

        /// <summary>B32.d (Q10): the Keeper is watching the tree: it is on the screen.</summary>
        bool TreeWatched(Vector2 tree, int stage) => lastRoot != null && HitFx.InView(lastRoot.TransformPoint(new Vector3(tree.x, TreeGeo.CrownY(stage) * 0.5f, tree.y)), -0.05f);

        bool FreeKey(IReadOnlyList<PersonActivity> acts, int key) { foreach (var a in acts) if (a.who.Key == key) return Free(a, clock); return false; }

        /// <summary>Cheats only (#692 part 2, B32.d/e shot tests): the free grown-up nearest <paramref name="to"/>; else any grown-up drawn on the land
        /// and not in a scene (a tent or a hunt is left for the visit), so a forced visit always plays. Nobody only when no grown-up is drawn at all.</summary>
        int StageGrown(IReadOnlyList<PersonActivity> acts, Vector2 to, ICollection<int> not = null)
        {
            int k = Nearest(acts, to, 9f, a => IsGrown(a), not, false);
            if (k != Nobody) return k;
            float bd = float.MaxValue;
            for (int i = 0; i < acts.Count; i++)
            {
                var a = acts[i];
                if (not != null && not.Contains(a.who.Key)) continue;
                if (byKey.ContainsKey(a.who.Key) || !IsGrown(a) || !(At(a.who.Key) is Vector2 p)) continue;
                float d = (p - to).sqrMagnitude;
                if (d < bd) { bd = d; k = a.who.Key; }
            }
            return k;
        }

        /// <summary>Why no grown-up could be staged: each grown-up's activity, drawn or not, in a scene or not.</summary>
        string GrownWhy(IReadOnlyList<PersonActivity> acts)
        {
            var sb = new System.Text.StringBuilder();
            foreach (var a in acts) if (IsGrown(a)) sb.Append($" {a.who.Key}:{a.kind}{(At(a.who.Key).HasValue ? "" : " unseen")}{(byKey.ContainsKey(a.who.Key) ? " in-scene" : "")}{(a.tent != TentPhase.None ? " tent" : "")}{(a.hunt != HuntPhase.None ? " hunt" : "")};");
            return sb.Length == 0 ? " no grown-ups in the plan" : sb.ToString();
        }

        bool IsElder(in PersonActivity a) => planner.StageOf(a.who) == LifeStage.Elder;
        bool Shown(IReadOnlyList<PersonActivity> acts, int k) => k > 0 && At(k).HasValue && !byKey.ContainsKey(k) && !Tied(acts, k);

        /// <summary>The free people near <paramref name="to"/> who pass <paramref name="ok"/>, nearest first, at most <paramref name="max"/>; a nap counts as
        /// free time for the sit under the tree (B32.a).</summary>
        List<int> FreeNear(IReadOnlyList<PersonActivity> acts, Vector2 to, float reach, System.Func<PersonActivity, bool> ok, int max, bool napToo = false)
        {
            var got = new List<(float d, int k)>();
            for (int i = 0; i < acts.Count; i++)
            {
                var a = acts[i];
                bool free = Free(a, clock) || napToo && a.kind == ActivityKind.Nap && !a.moving && !a.enRoute && a.hunt == HuntPhase.None && a.tent == TentPhase.None && !byKey.ContainsKey(a.who.Key);
                if (!free || !ok(a) || !(At(a.who.Key) is Vector2 p)) continue;
                float d = (p - to).magnitude;
                if (d <= reach) got.Add((d, a.who.Key));
            }
            got.Sort((x, y) => x.d.CompareTo(y.d));
            var r = new List<int>();
            for (int i = 0; i < got.Count && r.Count < max; i++) r.Add(got[i].k);
            return r;
        }

        // ================================================================== the triggers (each frame, in the camp era, while the tree stands)
        void StepTree(IReadOnlyList<PersonActivity> acts, float time)
        {
            if (OnlyCheats || !TreeAt(out var tree, out int stage) || stage < 2) { walkPrev.Clear(); return; }
            var dir = TreeDir(tree);
            float h = Hour;
            bool meal = meals != null && meals.Eaters > 0;
            bool busy = ThingBusy(ThingTree);
            Season season = LifeClock.SeasonOf(now.day);

            // B32.d: the quiet visit after a death (a held moment: waits up to two dusks for the Keeper to be watching)
            if (!busy && !meal && h >= TreeVisits.DuskFrom && h < TreeVisits.DuskTo && TryQuietNatural(acts, time, tree, stage, dir)) return;
            // B32.e: the newlyweds, the dusk of the wedding (or the next morning)
            if (!busy && !meal && TryWeddingNatural(acts, time, tree, stage, dir)) return;
            // B32.f: a parent brings the newborn, a morning in its first days (one a game season)
            if (!busy && TryNewbornNatural(acts, time, tree, stage, dir)) return;
            if (stage >= 3)
            {
                // B32.g: the old one's afternoons there
                if (!busy && h >= TreeVisits.OldFrom && h < TreeVisits.OldTo && !doneToday.Contains(CampUse.TreeOld) && TryOld(acts, time, tree, stage, dir, false) == null) return;
                // B32.a: sit under it at midday (sometimes, more in summer)
                if (!busy && h >= 11f && h < 15f && !doneToday.Contains(CampUse.TreeSit) && Roll(CampUse.TreeSit) < (season == Season.Summer ? 0.6f : Sometimes) && LifeOk(CampUse.TreeSit)
                    && TrySit(acts, time, tree, stage, dir, false) == null) return;
                // B32.h: the eldest tells the kids who planted it (1 day in 4, an afternoon)
                if (!busy && h >= 13f && h < 16.5f && !doneToday.Contains(CampUse.TreeTell) && Roll(CampUse.TreeTell) < 0.25f && LifeOk(CampUse.TreeTell)
                    && TryTell(acts, time, tree, stage, dir, false) == null) return;
            }
            // B32.c: kids circle it (by day, sometimes)
            if (!busy && h >= 9f && h < 16.5f && !doneToday.Contains(CampUse.TreeCircle) && Roll(CampUse.TreeCircle) < Sometimes && LifeOk(CampUse.TreeCircle)
                && TryCircle(acts, time, tree, stage, dir, false) == null) return;
            // B32.b: a hand on the bark in passing (often, at most one per 30 s)
            StepTouch(acts, time, tree, stage, busy);
        }

        void StepTouch(IReadOnlyList<PersonActivity> acts, float time, Vector2 tree, int stage, bool busy)
        {
            bool can = !busy && time - lastTouch > TouchGap && Roll(CampUse.TreeTouch) < Often;
            for (int i = 0; i < acts.Count; i++)
            {
                var a = acts[i];
                int k = a.who.Key;
                if (!a.moving || !(At(k) is Vector2 p)) { walkPrev.Remove(k); continue; }
                if ((p - tree).sqrMagnitude > 0.9f * 0.9f) { walkPrev.Remove(k); continue; }
                bool had = walkPrev.TryGetValue(k, out var prev);
                walkPrev[k] = p;
                if (!can || !had || byKey.ContainsKey(k) || a.hunt != HuntPhase.None || a.tent != TentPhase.None) continue;
                var v = p - prev;
                if (v.sqrMagnitude < 1e-8f) continue;
                var heading = v.normalized;
                var rel = tree - p;
                float ahead = Vector2.Dot(rel, heading), side = Mathf.Abs(rel.x * heading.y - rel.y * heading.x);
                // on their way past within 0.4 u (no detour over 0.3 u), the trunk a little ahead
                if (ahead < 0.15f || ahead > 0.45f || side > 0.4f) continue;
                Add(Thing(CampScenes2.TreeTouch(k, IsKid(a), p, tree, stage, heading, time), ThingTree));
                lastTouch = time;
                return;
            }
        }

        bool TryQuietNatural(IReadOnlyList<PersonActivity> acts, float time, Vector2 tree, int stage, Vector2 dir)
        {
            bool watched = TreeWatched(tree, stage);
            foreach (var dead in sim.State.notables)
            {
                if (dead.alive || dead.diedMonth < today - 20) continue;
                if (!TreeVisits.Due(today, dead.diedMonth, out int which, out int dusk)) continue;
                long key = dead.id * 4L + which;
                if (quietDone.Contains(key)) continue;
                if (!TreeVisits.PlayNow(dusk, Hour, watched)) continue;
                int mourner = TreeVisits.Mourner(sim.State.notables, dead, k => Shown(acts, k));
                if (mourner == Nobody) { if (dusk >= TreeVisits.Dusks - 1 && Hour >= TreeVisits.UnseenFrom) quietDone.Add(key); continue; }
                StartQuiet(acts, time, tree, stage, dir, mourner, which == 0 && sim.HasCustom("remember:tree"));
                quietDone.Add(key);
                return true;
            }
            return false;
        }

        string StartQuiet(IReadOnlyList<PersonActivity> acts, float time, Vector2 tree, int stage, Vector2 dir, int mourner, bool tie)
        {
            var watchers = new List<int>(); var wat = new List<Vector2>();
            if (meals != null && meals.HasFire)
                foreach (int k in meals.Sitters)
                    if (k != mourner && Shown(acts, k) && watchers.Count < 3) { watchers.Add(k); wat.Add(At(k).Value); }
            // the one who meets them: one who watched from the fire, else a grown-up nearest the fire (the walk back is to the fire)
            var home = meals != null && meals.HasFire ? meals.Fire : At(mourner).Value;
            int meeter = watchers.Count > 0 ? watchers[0] : Nearest(acts, home, 9f, a => IsGrown(a), new[] { mourner }, false);
            if (meeter == Nobody) meeter = Nearest(acts, home, 9f, a => IsKid(a), new[] { mourner }, false); // anyone at the fire: a kid runs out to them
            var meeterAt = meeter != Nobody ? At(meeter) ?? At(mourner).Value : At(mourner).Value;
            var s = CampScenes2.TreeQuiet(mourner, At(mourner).Value, tree, stage, dir, tie, watchers, wat, meeter, meeterAt, time);
            Add(Thing(s, ThingTree));
            return $"quiet visit: {mourner}{(tie ? ", ties a charm" : "")}, {watchers.Count} watch, met by {meeter}";
        }

        bool TryWeddingNatural(IReadOnlyList<PersonActivity> acts, float time, Vector2 tree, int stage, Vector2 dir)
        {
            var ch = sim.State.chronicle;
            for (int i = ch.Count - 1; i >= 0 && ch[i].month >= today - 1; i--)
            {
                var e = ch[i];
                if (e.kind != "wedding" || weddingsDone.Contains(e.id) || !TreeVisits.WeddingDue(today, e.month, Hour)) continue;
                if (!Shown(acts, e.notableId) || !Shown(acts, e.otherNotableId) || !FreeKey(acts, e.notableId) || !FreeKey(acts, e.otherNotableId)) continue;
                Add(Thing(CampScenes2.TreeWed(e.notableId, e.otherNotableId, At(e.notableId).Value, At(e.otherNotableId).Value, tree, stage, dir, time), ThingTree));
                weddingsDone.Add(e.id);
                return true;
            }
            return false;
        }

        bool TryNewbornNatural(IReadOnlyList<PersonActivity> acts, float time, Vector2 tree, int stage, Vector2 dir)
        {
            long seasonKey = today / 3;
            if (newbornSeason == seasonKey) return false;
            foreach (var n in sim.State.notables)
            {
                if (!n.alive || n.parentId < 0 || !TreeVisits.NewbornDue(today, n.bornMonth, Hour)) continue;
                if (!Shown(acts, n.parentId) || !FreeKey(acts, n.parentId)) continue; // free time only (D rules)
                Add(Thing(CampScenes2.TreeNewborn(n.parentId, At(n.parentId).Value, tree, stage, dir, time), ThingTree));
                newbornSeason = seasonKey;
                return true;
            }
            return false;
        }

        // ================================================================== starting each visit (natural and forced); null = started, else why not
        string TrySit(IReadOnlyList<PersonActivity> acts, float time, Vector2 tree, int stage, Vector2 dir, bool force)
        {
            var who = force ? new List<int>() : FreeNear(acts, tree, BorrowReach, a => IsGrown(a), 2, napToo: true);
            if (force) { int k1 = Nearest(acts, tree, 9f, a => IsGrown(a), null, false); if (k1 != Nobody) { who.Add(k1); int k2 = Nearest(acts, tree, 9f, a => IsGrown(a), who, false); if (k2 != Nobody) who.Add(k2); } }
            if (who.Count == 0) return "no grown-up free near the tree";
            bool two = who.Count > 1 && (force || BeatHash.U01((int)(today & 0x7fffffff), 3, 6931) < 0.5f);
            var s = CampScenes2.TreeSit(who[0], At(who[0]).Value, tree, stage, dir, force ? 8f : SitSeconds, time, two ? who[1] : Nobody, two ? At(who[1]).Value : default);
            Add(Thing(s, ThingTree));
            doneToday.Add(CampUse.TreeSit);
            return null;
        }

        string TryCircle(IReadOnlyList<PersonActivity> acts, float time, Vector2 tree, int stage, Vector2 dir, bool force)
        {
            var kids = force ? new List<int>() : FreeNear(acts, tree, BorrowReach, a => IsKid(a), 5);
            if (force) while (kids.Count < 5) { int k = Nearest(acts, tree, 9f, a => IsKid(a), kids, false); if (k == Nobody) break; kids.Add(k); }
            if (kids.Count < 3) return $"only {kids.Count} kid(s) near the tree (3 needed: 'Two kids join the camp' first)";
            var at = new List<Vector2>(); foreach (int k in kids) at.Add(At(k).Value);
            Add(Thing(CampScenes2.TreeCircle(kids, at, tree, stage, dir, time), ThingTree));
            doneToday.Add(CampUse.TreeCircle);
            return null;
        }

        int Eldest(IReadOnlyList<PersonActivity> acts, bool elderOnly, ICollection<int> not = null)
        {
            int best = Nobody; float age = -1f;
            foreach (var n in sim.State.notables)
            {
                if (!n.alive || n.age < 18f || n.age <= age || not != null && not.Contains(n.id) || !Shown(acts, n.id)) continue;
                if (elderOnly) { bool elder = false; foreach (var a in acts) if (a.who.Key == n.id) { elder = IsElder(a); break; } if (!elder) continue; }
                age = n.age; best = n.id;
            }
            return best;
        }

        string TryOld(IReadOnlyList<PersonActivity> acts, float time, Vector2 tree, int stage, Vector2 dir, bool force)
        {
            int old = Eldest(acts, true);
            if (!force)
            {
                bool free = false;
                foreach (var a in acts) if (a.who.Key == old) { free = Free(a, clock); break; }
                if (old == Nobody || !free) return "the old one is not free";
            }
            else
            {
                if (old == Nobody) old = Eldest(acts, false);
                if (old == Nobody) old = Nearest(acts, tree, 9f, a => IsGrown(a), null, false);
                if (old == Nobody) return "nobody old on view";
            }
            int kid = Nobody;
            if (force || BeatHash.U01((int)(today & 0x7fffffff), 5, 6932) < Sometimes)
            {
                var ks = force ? null : FreeNear(acts, tree, 3f, a => IsKid(a), 1);
                kid = force ? Nearest(acts, tree, 9f, a => IsKid(a), new[] { old }, false) : ks.Count > 0 ? ks[0] : Nobody;
            }
            int helper = Nobody;
            foreach (var n in sim.State.notables) if (n.alive && n.parentId == old && n.age >= 16f && n.id != kid && Shown(acts, n.id)) { helper = n.id; break; }
            if (helper == Nobody) helper = Nearest(acts, tree, 9f, a => IsGrown(a), new[] { old, kid }, false);
            var s = CampScenes2.TreeOld(old, At(old).Value, tree, stage, dir, force ? 12f : OldSitSeconds, kid, kid != Nobody ? At(kid).Value : default, helper, helper != Nobody ? At(helper).Value : default, time);
            Add(Thing(s, ThingTree));
            doneToday.Add(CampUse.TreeOld);
            return null;
        }

        string TryTell(IReadOnlyList<PersonActivity> acts, float time, Vector2 tree, int stage, Vector2 dir, bool force)
        {
            int eldest = Eldest(acts, false);
            if (!force && eldest != Nobody) { bool free = false; foreach (var a in acts) if (a.who.Key == eldest) { free = Free(a, clock); break; } if (!free) eldest = Nobody; }
            if (eldest == Nobody && force) eldest = Nearest(acts, tree, 9f, a => IsGrown(a), null, false);
            if (eldest == Nobody) return "the eldest is not free";
            var kids = force ? new List<int>() : FreeNear(acts, tree, 3f, a => IsKid(a), 4);
            if (force) while (kids.Count < 4) { int k = Nearest(acts, tree, 9f, a => IsKid(a), kids, false); if (k == Nobody) break; kids.Add(k); }
            if (kids.Count < 2) return $"only {kids.Count} kid(s) near the tree ('Two kids join the camp' first)";
            var at = new List<Vector2>(); foreach (int k in kids) at.Add(At(k).Value);
            Add(Thing(CampScenes2.TreeTell(eldest, At(eldest).Value, kids, at, tree, stage, dir, time), ThingTree));
            doneToday.Add(CampUse.TreeTell);
            return null;
        }

        // ================================================================== cheats
        string ForceTree(CampUse use)
        {
            if (sim == null || planner == null) return "no camp yet";
            if (!sim.EcologyOn) return "no living land";
            var et = sim.EcoTree;
            if (!et.planted || et.stage < 3) EcoDebug.TreeStage(4); // grown, and the box redrawn with it
            if (!TreeAt(out var tree, out int stage)) return "no Keeper's tree can stand here";
            var dir = TreeDir(tree);
            var acts = pv.Activities; float time = clock;
            switch (use)
            {
                case CampUse.TreeSit: return TrySit(acts, time, tree, stage, dir, true) ?? "sit under the tree";
                case CampUse.TreeCircle: return TryCircle(acts, time, tree, stage, dir, true) ?? "kids circle the tree";
                case CampUse.TreeOld: return TryOld(acts, time, tree, stage, dir, true) ?? "the old one sits there";
                case CampUse.TreeTell: return TryTell(acts, time, tree, stage, dir, true) ?? "the eldest tells the kids";
                case CampUse.TreeTouch:
                {
                    // a grown-up walks past the trunk, then a kid the other way (who slaps it)
                    var across = new Vector2(-dir.y, dir.x);
                    int g = Nearest(acts, tree, 9f, a => IsGrown(a), null, false);
                    if (g == Nobody) return "no grown-up on view";
                    Add(PassBy(g, false, tree, stage, across, dir, time)); // from behind the tree toward the camera, the trunk at its side: the hand shows
                    int k = Nearest(acts, tree, 9f, a => IsKid(a), new[] { g }, false);
                    if (k != Nobody) Add(PassBy(k, true, tree, stage, -across, -dir, time + 3.5f));
                    return $"touch the bark: {g}{(k != Nobody ? $", kid {k} slaps it" : "")}";
                }
                case CampUse.TreeQuiet:
                {
                    Notable dead = null;
                    foreach (var n in sim.State.notables) if (!n.alive && (dead == null || n.diedMonth > dead.diedMonth)) dead = n;
                    int m = dead != null ? TreeVisits.Mourner(sim.State.notables, dead, k => Shown(acts, k)) : Nobody;
                    if (m == Nobody) m = StageGrown(acts, meals != null && meals.HasFire ? meals.Fire : tree);
                    if (m == Nobody) return "no grown-up on view:" + GrownWhy(acts);
                    return StartQuiet(acts, time, tree, stage, dir, m, sim.HasCustom("remember:tree"));
                }
                case CampUse.TreeWed:
                {
                    int a = StageGrown(acts, tree);
                    if (a == Nobody) return "no grown-up on view:" + GrownWhy(acts);
                    int b = sim.FindNotable(a) is Notable na && na.partnerId > 0 && Shown(acts, na.partnerId) ? na.partnerId : StageGrown(acts, At(a).Value, new[] { a });
                    if (b == Nobody) return "only one grown-up on view:" + GrownWhy(acts);
                    Add(Thing(CampScenes2.TreeWed(a, b, At(a).Value, At(b).Value, tree, stage, dir, time), ThingTree));
                    return $"newlyweds' visit: {a} and {b}";
                }
                case CampUse.TreeNewborn:
                {
                    int p = Nobody;
                    foreach (var n in sim.State.notables) if (n.alive && n.age < 1f && n.parentId > 0 && Shown(acts, n.parentId)) { p = n.parentId; break; }
                    if (p == Nobody) p = Nearest(acts, tree, 9f, a => IsGrown(a), null, false);
                    if (p == Nobody) return "no grown-up on view";
                    Add(Thing(CampScenes2.TreeNewborn(p, At(p).Value, tree, stage, dir, time), ThingTree));
                    return "newborn visit: " + p;
                }
            }
            return "not a tree visit: " + use;
        }

        /// <summary>The touch cheat: walk to a start 0.55 u back along <paramref name="heading"/>, a little to <paramref name="side"/>, then pass the
        /// trunk with it on that side.</summary>
        CampScene PassBy(int key, bool kid, Vector2 tree, int stage, Vector2 side, Vector2 heading, float time)
        {
            var start = tree + side * 0.1f - heading * 0.55f;
            var at = At(key).Value;
            float w = Mathf.Clamp((at - start).magnitude / (kid ? CampScenes1.KidWalk : CampScenes1.Walk), 0.3f, 8f);
            var s = CampScenes2.TreeTouch(key, kid, start, tree, stage, heading, time + w);
            s.Part(0, -w, 0f, start, start + heading, DollStyle.Stand, null, PropKind.None, kid ? CampScenes1.KidWalk : CampScenes1.Walk);
            s.thing = ThingTree;
            return s;
        }

        // ================================================================== drawing (after the dolls)
        Vector3 CameraAcross3(Transform root)
        {
            var c = CameraAcross();
            return new Vector3(c.x, 0f, c.y);
        }

        /// <summary>B32.d: the glow comes up (+0.1 for 4 s): a soft extra lamp in the crown for 4 s (DayLightDriver).</summary>
        void TreeGlow(Vector3 world, float time) { glowWorld = world; glowAt = time; }
        public const float GlowSeconds = 4f, GlowLamp = 0.45f;

        void DrawTree(Transform root, float time)
        {
            lastRoot = root;
            int held = 0; bool quiet = false;
            foreach (var s in scenes)
            {
                float u = time - s.t0;
                if (s.use == CampUse.TreeQuiet) { quiet = true; if (s.b > 0f && u < s.b) held = 1; }
                if (s.use != CampUse.TreeNewborn || s.Count == 0) continue;
                var v = pv.ViewOf(s.keys[0]);
                if (v == null || !v.gameObject.activeInHierarchy || u < 0f || u > s.Length) continue;
                v.SetBaby(true, false);
                v.LiftBaby(BabyLift(u, s.a, s.b));
            }
            ConsequencePropsView.HeldCharms = held;
            AudioDirector.SceneDip = quiet ? QuietHush : 1f;
            float g = time - glowAt;
            DayLightDriver.ExtraLamp = g >= 0f && g < GlowSeconds ? new Vector4(glowWorld.x, glowWorld.y, glowWorld.z, GlowLamp * Mathf.Sin(g / GlowSeconds * Mathf.PI)) : Vector4.zero;
        }

        /// <summary>B32.f: how high the bundle is (PegDollView.LiftBaby: 0 in the arms, 1 high over the head) <paramref name="u"/> s into the scene.</summary>
        public static float BabyLift(float u, float from, float to)
        {
            if (u < from || u > to + 0.6f) return 0f;
            if (u < from + 1.5f) { float k = (u - from) / 1.5f; k -= 1f; return 0.85f * (k * k * (2.4f * k + 1.4f) + 1f); }
            if (u <= to) return 0.85f;
            return 0.85f * (1f - (u - to) / 0.6f);
        }

        void PlaceTreeTip(Transform t, PegDollView v, CampTip tip, float u, Transform root, float sc)
        {
            float rs = root.lossyScale.y;
            if (tip.kind == CampTipKind.Leaf)
            {
                var shoulder = v.transform.TransformPoint(new Vector3(0.026f, PegDoll.HeadY * 0.74f, 0f) * v.scale);
                float k = Mathf.Clamp01((u - tip.from) / 2f), e = CampPose.Smooth(k);
                var from = shoulder + Vector3.up * (0.24f * rs) + new Vector3(0.05f, 0f, 0.03f) * rs;
                var sway = new Vector3(Mathf.Sin(u * 3.1f), 0f, Mathf.Cos(u * 2.3f)) * (0.03f * rs * (1f - e));
                t.position = Vector3.Lerp(from, shoulder, e) + sway;
                t.rotation = Quaternion.Euler(20f * Mathf.Sin(u * 4f) * (1f - e), u * 90f * (1f - e), 15f);
                t.localScale = Vector3.one * 0.014f * Mathf.Max(0.8f, sc);
                return;
            }
            var hand = v.armR.TransformPoint(new Vector3(0f, -PegDoll.ArmLen * v.scale, 0f));
            t.position = hand + Vector3.up * (0.006f * rs);
            t.rotation = Quaternion.identity;
            t.localScale = Vector3.one * (tip.kind == CampTipKind.Flower ? 0.018f : 0.011f) * Mathf.Max(0.8f, sc); // #692 p3: the flower in hand reads on a phone
        }

        /// <summary>The leaf (a flat oval in the season's leaf green), the flower (a stem, a pink head, a yellow middle), the charm (a red bead on a
        /// pale cord), each a unit size.</summary>
        static bool TreeTipMesh(CampTipKind kind, MeshBuilder mb)
        {
            switch (kind)
            {
                case CampTipKind.Leaf:
                    mb.Ico(Vector3.zero, 1f, new Vector3(1f, 0.18f, 0.6f), new Color(0.56f, 0.70f, 0.22f), 0.05f);
                    return true;
                case CampTipKind.Flower:
                    mb.Box(new Vector3(0f, 1.2f, 0f), new Vector3(0.18f, 2.4f, 0.18f), new Color(0.35f, 0.55f, 0.2f));
                    mb.Ico(new Vector3(0f, 2.6f, 0f), 1f, new Vector3(1f, 0.6f, 1f), new Color(0.96f, 0.52f, 0.72f), 0.05f);
                    mb.Ico(new Vector3(0f, 2.9f, 0f), 0.42f, Vector3.one, new Color(1f, 0.85f, 0.25f), 0.05f);
                    return true;
                case CampTipKind.Charm:
                    mb.Box(new Vector3(0f, 1.4f, 0f), new Vector3(0.15f, 1.6f, 0.15f), new Color(0.9f, 0.85f, 0.72f));
                    mb.Ico(Vector3.zero, 1f, Vector3.one, new Color(0.82f, 0.25f, 0.22f), 0.05f);
                    return true;
            }
            return false;
        }
    }
}

namespace AquariumCiv.View
{
    /// <summary>#692 part 2 cheats (SliceDebugPanel's "#692" block): each grows the Keeper's tree if it must and starts the visit now.</summary>
    public static partial class CampCheats
    {
        public static string TreeSit() => Tree(CampUse.TreeSit);
        public static string TreeTouch() => Tree(CampUse.TreeTouch);
        public static string TreeCircle() => Tree(CampUse.TreeCircle);
        public static string TreeQuiet() => Tree(CampUse.TreeQuiet);
        public static string TreeWed() => Tree(CampUse.TreeWed);
        public static string TreeNewborn() => Tree(CampUse.TreeNewborn);
        public static string TreeOld() => Tree(CampUse.TreeOld);
        public static string TreeTell() => Tree(CampUse.TreeTell);
        static string Tree(CampUse use) { var c = PeopleView.Find()?.Camp; return c == null ? "no PeopleView" : c.Force(use); }

        /// <summary>AnimLab and the shots: the Keeper's tree grown (planted near the camp first if need be).</summary>
        public static string GrowTree()
        {
            return EcoDebug.TreeStage(4); // the box's layout refreshed too (a Rebuild alone drew the old layout: the tree came 17 s late)
        }
    }
}
