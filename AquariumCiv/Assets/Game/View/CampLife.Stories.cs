// #722 (docs/design/new-events.md §1.1-1.3, animation-specs B78.a-c): CampLife's side of the story events. (1) The held moment's scene
// (View/Moments/StoryMoment.cs asks StartStory): the line's people cast when they are drawn and free (a cheat's moment: drawn is enough), else
// the spec's fallback (1.1: the nearest free kid; 1.3: the helper and the family come as they are free); the builders are
// View/Beats/StoryScenes.cs. (2) What the events leave, drawn from SimState.storyMarks in every era: the fruit on the Keeper's tree every
// summer after the first, the couples' marks on its trunk (a pair each, stacked), the second stone (a ConsequenceProp: ConsequencePropsView
// draws it; it is held back from its spot while it is rolled in, or while its moment still waits). (3) The life use the fruit leaves: a kid
// picks one in summer. Cheats: SimDebugPanel's "Stories (#722)".
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
        static bool IsStoryUse(CampUse u) => u is CampUse.FirstFruit or CampUse.TwoMarks or CampUse.SecondStone or CampUse.FruitPick;

        /// <summary>The scenes a held moment plays (meals and bedtime don't take their people).</summary>
        readonly HashSet<CampScene> storyHeld = new();
        bool StoryHeld(CampScene s) => storyHeld.Contains(s);
        /// <summary>Why the last story scene could not be cast (the stage's reason).</summary>
        public string LastStoryWhy { get; private set; } = "";
        /// <summary>"Tree: a kid picks a fruit": the tree shows its fruit whatever the season (view only, this session).</summary>
        public static bool CheatFruit;
        /// <summary>Tests: the fruits and notches drawn this frame; the second stone drawn rolling.</summary>
        public int FruitDrawn { get; private set; }
        public int MarksDrawn { get; private set; }
        public bool StoneRolling { get; private set; }

        readonly List<Transform> storyPool = new();
        Mesh fruitMesh, notchMesh, rollStoneMesh;
        SimCore storyScanned; int storyScanCount = -1;
        readonly Dictionary<string, int> storyEntry = new();

        void ResetStories()
        {
            storyHeld.Clear(); storyEntry.Clear(); storyScanCount = -1; storyScanned = null;
            ConsequencePropsView.HeldBack.Clear();
        }

        void DestroyStories()
        {
            foreach (var t in storyPool) if (t != null) UnityEngine.Object.Destroy(t.gameObject);
            storyPool.Clear();
            foreach (var m in new[] { fruitMesh, notchMesh, rollStoneMesh }) if (m != null) UnityEngine.Object.Destroy(m);
            fruitMesh = notchMesh = rollStoneMesh = null;
            ConsequencePropsView.HeldBack.Clear();
        }

        // ================================================================== the held moment's scene
        /// <summary>Starts the scene of a story moment now (its people cast), or null when it can't be cast now (<see cref="LastStoryWhy"/>):
        /// the moment then waits and, after a few tries, is a card.</summary>
        public CampScene StartStory(SimCore s, HeldMoment m)
        {
            LastStoryWhy = "";
            if (s == null || m == null) { LastStoryWhy = "no moment"; return null; }
            if (!ReferenceEquals(s, sim) || planner == null || meals == null) { LastStoryWhy = "no camp yet"; return null; }
            string id = s.StoryOfEntry(m.entryId);
            if (id == null) { LastStoryWhy = "not a story line"; return null; }
            var use = UseOfStory(id);
            int before = scenes.Count;
            string why;
            forcing = true;
            try { why = TryStory(use, m.personA, m.personB, m.cheat); }
            finally { forcing = false; }
            if (why != null || scenes.Count == before) { LastStoryWhy = why ?? "not cast"; return null; }
            var sc = scenes[^1];
            storyHeld.Add(sc);
            if (m.cheat) forced = sc; // the cheat's scene: AnimLab's follow and lead keep to it
            return sc;
        }

        public static CampUse UseOfStory(string id) => id == StoryEvent.FirstFruit ? CampUse.FirstFruit : id == StoryEvent.TwoMarks ? CampUse.TwoMarks : CampUse.SecondStone;

        /// <summary>The scene is still playing.</summary>
        public bool StoryPlaying(CampScene sc) => sc != null && scenes.Contains(sc);

        /// <summary>Ends a story scene now (the box changed, the app left): its people go back to their plans.</summary>
        public void StopStory(CampScene sc)
        {
            if (sc == null) return;
            storyHeld.Remove(sc);
            if (scenes.Remove(sc)) Index();
        }

        bool Castable(IReadOnlyList<PersonActivity> acts, int k, bool force) => k > 0 && Shown(acts, k) && (force || FreeKey(acts, k));

        bool KeyIs(IReadOnlyList<PersonActivity> acts, int k, Func<PersonActivity, bool> ok)
        {
            foreach (var a in acts) if (a.who.Key == k) return ok(a);
            return false;
        }

        /// <summary>Casts and starts one story scene: null = started, else why not.</summary>
        string TryStory(CampUse use, int a, int b, bool force)
        {
            if (sim == null || planner == null || meals == null) return "no camp yet";
            var acts = pv.Activities; float time = clock;
            if (use == CampUse.SecondStone) return TrySecondStoneScene(acts, time, a, b, force);
            if (!TreeAt(out var tree, out int stage) || stage < 4) return "the Keeper's tree isn't grown ('Grow the Keeper's tree' first)";
            switch (use)
            {
                case CampUse.FirstFruit: return TryFirstFruitScene(acts, time, a, force, tree, stage);
                case CampUse.TwoMarks: return TryMarksScene(acts, time, a, b, force, tree, stage);
                case CampUse.FruitPick: return TryFruitPick(acts, time, force, tree, stage);
            }
            return "not a story use: " + use;
        }

        string TryFirstFruitScene(IReadOnlyList<PersonActivity> acts, float time, int named, bool force, Vector2 tree, int stage)
        {
            var dir = TreeDir(tree);
            // the kid the line names when free, else the nearest free kid (the spec's cast)
            int kid = Castable(acts, named, force) && KeyIs(acts, named, x => IsKid(x)) ? named : Kid1(acts, tree, force);
            if (kid == Nobody) return "no kid free near the tree ('Two kids join the camp' first)";
            var fire = meals.HasFire ? meals.Fire : tree + dir * 0.8f;
            var grown = Pick(acts, fire, x => IsGrown(x), 3, force, new[] { kid });
            int young = Youngest(acts, force, kid, grown);
            Add(Thing(StoryScenes.FirstFruit(kid, At(kid).Value, grown, AtAll(grown), young, young != Nobody ? At(young).Value : default, tree, stage, dir, fire, ToCam(fire), time), ThingTree));
            return null;
        }

        /// <summary>The youngest drawn (and free) person younger than the picker, for the last bite; Nobody: the picker is the youngest.</summary>
        int Youngest(IReadOnlyList<PersonActivity> acts, bool force, int kid, ICollection<int> not)
        {
            var picker = sim.FindNotable(kid);
            int best = Nobody; float age = picker != null ? picker.age : 99f;
            foreach (var n in sim.State.notables)
                if (n.alive && n.id != kid && n.age < age && !not.Contains(n.id) && Castable(acts, n.id, force)) { age = n.age; best = n.id; }
            return best;
        }

        string TryMarksScene(IReadOnlyList<PersonActivity> acts, float time, int a, int b, bool force, Vector2 tree, int stage)
        {
            if (a <= 0 || b <= 0) return "no couple";
            if (!Castable(acts, a, force) || !Castable(acts, b, force)) return "the couple isn't free to walk to the tree";
            var mk = sim.StoryMarks;
            int pair = -1;
            for (int i = mk.markA.Count - 1; i >= 0 && pair < 0; i--) if (mk.markA[i] == a && mk.markB[i] == b || mk.markA[i] == b && mk.markB[i] == a) pair = i;
            if (pair < 0) pair = Mathf.Max(0, mk.markA.Count - 1);
            var kids = Pick(acts, tree, x => IsKid(x), 2, force, new[] { a, b });
            Add(Thing(StoryScenes.TwoMarks(a, b, At(a).Value, At(b).Value, kids, AtAll(kids), tree, stage, pair, time), ThingTree));
            return null;
        }

        string TrySecondStoneScene(IReadOnlyList<PersonActivity> acts, float time, int named, int deadId, bool force)
        {
            int id = sim.StoryMarks.secondStoneId;
            var stone = id >= 0 ? sim.FindConsequenceProp(id) : null;
            if (stone == null) return "no second stone ('Stone: fill it' first)";
            var spot = new Vector2(stone.x, stone.z);
            var first = sim.FirstNameStone;
            var firstAt = first == null ? spot + Vector2.left * 0.24f : PropById(first.id, out var fp) ? fp.at : new Vector2(first.x, first.z);
            // the one the line names rolls it (a cheat: any grown-up if they aren't drawn); a helper and the family come as they are free
            int who = Castable(acts, named, force) && KeyIs(acts, named, x => IsGrown(x)) ? named : force ? Grown1(acts, spot, true) : Nobody;
            if (who == Nobody) return (sim.FindNotable(named)?.name ?? "the roller") + " isn't free";
            int helper = Grown1(acts, spot, force, new[] { who });
            var dead = sim.FindNotable(deadId);
            var fam = new List<int>(); var kid = new List<bool>();
            if (dead != null)
                foreach (var n in sim.State.notables)
                {
                    if (fam.Count >= 4) break;
                    if (!n.alive || n.id == who || n.id == helper) continue;
                    bool kin = n.partnerId == dead.id || dead.partnerId == n.id || n.parentId == dead.id || dead.parentId == n.id
                               || n.parentId >= 0 && n.parentId == dead.parentId || !string.IsNullOrEmpty(n.family) && n.family == dead.family;
                    if (kin && Castable(acts, n.id, force)) { fam.Add(n.id); kid.Add(n.age < SimCore.AdultAge); }
                }
            var from = RollStart(spot, firstAt);
            var sc = StoryScenes.SecondStone(who, At(who).Value, helper, helper != Nobody ? At(helper).Value : default, fam, AtAll(fam), kid, from, spot, firstAt, ToCam(spot), time);
            Add(Thing(sc, ThingPropBase + id));
            return null;
        }

        /// <summary>B78.c: where the roll starts: out from the stone toward the camp (its fire), turned off a line that would pass the first stone,
        /// kept in the box.</summary>
        Vector2 RollStart(Vector2 spot, Vector2 firstAt)
        {
            var toCamp = meals.HasFire ? Dir2(spot, meals.Fire) : ToCam(spot);
            foreach (float deg in new[] { 0f, 35f, -35f, 70f, -70f, 110f, -110f })
            {
                var d = Rotate(toCamp, deg);
                var from = spot + d * StoryScenes.RollFrom;
                from = new Vector2(Mathf.Clamp(from.x, -Terrain.HX + 0.4f, Terrain.HX - 0.4f), Terrain.ClampZ(from.y, 0.4f));
                if (StoryScenes.ToSegment(firstAt, from, spot) >= 0.2f && (from - spot).magnitude > 0.3f) return from;
            }
            return spot + Rotate(toCamp, 90f) * StoryScenes.RollFrom;
        }

        string TryFruitPick(IReadOnlyList<PersonActivity> acts, float time, bool force, Vector2 tree, int stage)
        {
            if (!force && !FruitOnTree) return "the tree has no fruit now (summer, after the first fruit)";
            int kid = Kid1(acts, tree, force);
            if (kid == Nobody) return "no kid free near the tree ('Two kids join the camp' first)";
            Add(Thing(StoryScenes.FruitPick(kid, At(kid).Value, tree, stage, TreeDir(tree), ToCam(tree), time), ThingTree));
            doneToday.Add(CampUse.FruitPick);
            return null;
        }

        /// <summary>The camp cheat (CampLife.Force): plays an event's scene again with the people the box has for it (no new line).</summary>
        string ForceStory(CampUse use)
        {
            if (sim == null || planner == null || meals == null) return "no camp yet";
            var mk = sim.StoryMarks;
            int a = -1, b = -1;
            switch (use)
            {
                case CampUse.FirstFruit: a = LastStoryNotable(StoryEvent.FirstFruit); break;
                case CampUse.TwoMarks:
                    if (mk.markA.Count == 0) return "no marks on the tree yet ('Tree: a couple's marks' first)";
                    a = mk.markA[^1]; b = mk.markB[^1]; break;
                case CampUse.SecondStone:
                    if (mk.secondStoneId < 0) return "no second stone ('Stone: fill it' first)";
                    a = LastStoryNotable(StoryEvent.SecondStone); b = mk.secondStoneFirst; break;
                case CampUse.FruitPick: CheatFruit = true; break;
            }
            return TryStory(use, a, b, true) ?? (use == CampUse.FruitPick ? "a kid picks a fruit from the Keeper's tree" : use + " plays");
        }

        int LastStoryNotable(string id)
        {
            int e = StoryEntryId(id);
            var ch = sim.State.chronicle;
            for (int i = ch.Count - 1; i >= 0; i--) if (ch[i].id == e) return ch[i].notableId;
            return -1;
        }

        /// <summary>The chronicle id of an event's newest line (−1 none); rescanned when the chronicle changes.</summary>
        int StoryEntryId(string id)
        {
            var ch = sim.State.chronicle;
            if (!ReferenceEquals(storyScanned, sim) || ch.Count != storyScanCount)
            {
                storyScanned = sim; storyScanCount = ch.Count; storyEntry.Clear();
                for (int i = ch.Count - 1; i >= 0; i--)
                {
                    var ev = StoryEvent.OfKind(ch[i].kind);
                    if (ev != null && !storyEntry.ContainsKey(ev)) storyEntry[ev] = ch[i].id;
                }
            }
            return storyEntry.TryGetValue(id, out int e) ? e : -1;
        }

        /// <summary>The event's held moment while it still waits for the Keeper (or plays), else null.</summary>
        HeldMoment StoryWaiting(string id)
        {
            int e = StoryEntryId(id);
            var m = e >= 0 ? sim.MomentOfEntry(e) : null;
            return m != null && (m.state == HeldMomentState.Waiting || m.state == HeldMomentState.Playing) ? m : null;
        }

        /// <summary>1.1 "Stays": the tree has its fruit now: every summer after the first fruit (that summer's one fruit was the one picked);
        /// the cheat: always.</summary>
        bool FruitOnTree => sim != null && sim.KeeperTreeGrown && (CheatFruit || sim.TreeInFruit && sim.StoryMarks.fruitYear != sim.State.month / SimConfig.MonthsPerYear);

        // ================================================================== each frame (every era)
        void StepStories(IReadOnlyList<PersonActivity> acts, float time)
        {
            // B78.c: the second stone stays off its spot while it is rolled in and stood up, and while its moment still waits for the Keeper
            var held = ConsequencePropsView.HeldBack;
            held.Clear();
            int id = sim.StoryMarks.secondStoneId;
            if (id >= 0)
            {
                CampScene roll = null;
                foreach (var s in scenes) if (s.use == CampUse.SecondStone && s.thing == ThingPropBase + id) roll = s;
                bool hold = roll != null ? time - roll.t0 < roll.c : StoryWaiting(StoryEvent.SecondStone) != null;
                if (hold) held.Add(id);
                else if (roll == null && ConsequencePropsView.LookOf(id) == 0)
                {   // standing with nothing at it: its first name is cut (a reload, a card, a scene cut short)
                    nameCut[id] = Mathf.Max(1, nameCut.TryGetValue(id, out int c) ? c : 0);
                    nameSeen[id] = DeathsSince(id);
                    ConsequencePropsView.SetLook(id, Mathf.Min(3, nameCut[id]));
                }
            }
            // #722 (1.1 "Read later"): a kid picks a fruit from the tree in summer (sometimes, by day)
            if (OnlyCheats || meals == null || meals.Eaters > 0 || !FruitOnTree) return;
            float h = Hour;
            if (h < 10f || h >= 16.5f || doneToday.Contains(CampUse.FruitPick) || Roll(CampUse.FruitPick) >= Sometimes || !LifeOk(CampUse.FruitPick) || ThingBusy(ThingTree)) return;
            if (triedAt.TryGetValue(CampUse.FruitPick, out float tr) && clock - tr < 3f && clock >= tr) return;
            triedAt[CampUse.FruitPick] = clock;
            TryStory(CampUse.FruitPick, -1, -1, false);
        }

        // ================================================================== drawing
        Transform StoryPiece(Transform root, Material material, int i, Mesh mesh)
        {
            while (i < storyPool.Count && storyPool[i] == null) storyPool.RemoveAt(i); // the box was rebuilt
            while (storyPool.Count <= i)
            {
                var go = new GameObject("Story thing") { hideFlags = HideFlags.DontSave };
                go.transform.SetParent(root, false);
                go.AddComponent<MeshFilter>();
                var r = go.AddComponent<MeshRenderer>(); r.sharedMaterial = material; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                storyPool.Add(go.transform);
            }
            var t = storyPool[i];
            t.GetComponent<MeshFilter>().sharedMesh = mesh;
            t.gameObject.SetActive(true);
            t.localRotation = Quaternion.identity; t.localScale = Vector3.one;
            return t;
        }

        /// <summary>B78.a "Fruit": a red ball (0.018 u), a short stem and a leaf.</summary>
        static Mesh FruitMesh()
        {
            var mb = new MeshBuilder(7223);
            float r = StoryScenes.FruitR;
            mb.Ico(Vector3.zero, r, Vector3.one, new Color(0.80f, 0.15f, 0.16f), 0.05f);
            mb.Box(new Vector3(0f, r + 0.004f, 0f), new Vector3(0.0025f, 0.009f, 0.0025f), Palette.Bark);
            mb.Box(new Vector3(0.006f, r + 0.006f, 0f), new Vector3(0.012f, 0.002f, 0.007f), Palette.LeafA, Quaternion.Euler(0f, 0f, 25f));
            var m = mb.ToMesh("Story_Fruit"); m.hideFlags = HideFlags.DontSave; return m;
        }

        /// <summary>B78.b "TreeMark": a pale notch in the bark (0.012 high, 0.004 wide), its face toward local +z.</summary>
        static Mesh NotchMesh()
        {
            var mb = new MeshBuilder(7224);
            mb.Box(Vector3.zero, new Vector3(StoryScenes.NotchW, StoryScenes.NotchH, 0.003f), new Color(0.90f, 0.84f, 0.70f));
            var m = mb.ToMesh("Story_TreeMark"); m.hideFlags = HideFlags.DontSave; return m;
        }

        void DrawStories(Transform root, Material material, float time, Func<float, float, float> groundAt)
        {
            int used = 0;
            FruitDrawn = MarksDrawn = 0; StoneRolling = false;
            if (sim != null)
            {
                CampScene fruitScene = null, marksScene = null, stoneScene = null;
                foreach (var s in scenes)
                {
                    if (s.use is CampUse.FirstFruit or CampUse.FruitPick) fruitScene = s;
                    else if (s.use == CampUse.TwoMarks) marksScene = s;
                    else if (s.use == CampUse.SecondStone) stoneScene = s;
                }
                Vector3 On(Vector3 p) => new Vector3(p.x, groundAt(p.x, p.z) + p.y, p.z);
                if (TreeAt(out var tree, out int stage) && stage >= 4 && sim.KeeperTreeGrown)
                {
                    // B78.a "Stays": a few red fruits on the crown every summer after the first
                    fruitMesh ??= FruitMesh();
                    if (FruitOnTree)
                        for (int i = 0; i < StoryScenes.TreeFruits; i++) { var t = StoryPiece(root, material, used++, fruitMesh); t.localPosition = On(StoryScenes.TreeFruit(tree, stage, i)); FruitDrawn++; }
                    // the low one a kid picks: until the hit (and the first one while its moment waits for the Keeper)
                    Vector3? low = null;
                    if (fruitScene != null) { if (time - fruitScene.t0 < fruitScene.a) low = new Vector3(fruitScene.p0.x, fruitScene.p1.x, fruitScene.p0.y); }
                    else if (StoryWaiting(StoryEvent.FirstFruit) != null) low = StoryScenes.LowFruit(tree, stage, TreeDir(tree));
                    if (low is Vector3 lp) { var t = StoryPiece(root, material, used++, fruitMesh); t.localPosition = On(lp); FruitDrawn++; }
                    // B78.b: the couples' marks on the trunk, a pair each, stacked; a pair still to be cut (its moment waits, or its scene's strokes) is not
                    notchMesh ??= NotchMesh();
                    var mk = sim.StoryMarks;
                    var wait = StoryWaiting(StoryEvent.TwoMarks);
                    int waitPair = -1;
                    if (wait != null)
                        for (int i = mk.markA.Count - 1; i >= 0 && waitPair < 0; i--)
                            if (mk.markA[i] == wait.personA && mk.markB[i] == wait.personB) waitPair = i;
                    var faceRot = Quaternion.LookRotation(new Vector3(StoryScenes.MarkFace.x, 0f, StoryScenes.MarkFace.y));
                    for (int i = 0; i < mk.markA.Count; i++)
                        for (int w = 0; w < 2; w++)
                        {
                            bool show = true;
                            if (marksScene != null && marksScene.kind == i)
                            {
                                float u = time - marksScene.t0;
                                show = w == Mathf.RoundToInt(marksScene.p0.x) ? u >= marksScene.a : u >= marksScene.b;
                            }
                            else if (i == waitPair) show = false;
                            if (!show) continue;
                            var t = StoryPiece(root, material, used++, notchMesh);
                            t.localPosition = On(StoryScenes.MarkAt(tree, stage, i, w)); t.localRotation = faceRot;
                            MarksDrawn++;
                        }
                }
                // B78.c: the second stone rolled in on its side, then heaved up (ConsequencePropsView draws it standing from the jolt)
                int sid = sim.StoryMarks.secondStoneId;
                if (stoneScene != null && sid >= 0)
                {
                    float u = time - stoneScene.t0;
                    if (u >= 0f && u < stoneScene.c)
                    {
                        rollStoneMesh ??= ConsequenceMeshes.SecondStone(0);
                        StoryScenes.StoneNow(stoneScene, u, out var at, out float turn, out float up);
                        float yaw = sim.FindConsequenceProp(sid)?.yaw ?? 0f;
                        var dir = Dir2(stoneScene.p0, stoneScene.p1); var side3 = new Vector3(-dir.y, 0f, dir.x);
                        var lie = Quaternion.AngleAxis(turn, side3) * Quaternion.FromToRotation(Vector3.up, side3);
                        var q = Quaternion.Slerp(lie, Quaternion.Euler(0f, yaw, 0f), up);
                        float sc = ConsequenceMeshes.Scale;
                        var g = new Vector3(at.x, groundAt(at.x, at.y), at.y);
                        var lying = g + Vector3.up * ((ConsequenceMeshes.StoneHalfX + ConsequenceMeshes.StoneHalfZ) * 0.5f * sc) - q * (Vector3.up * (ConsequenceMeshes.StoneH * 0.5f * sc));
                        var t = StoryPiece(root, material, used++, rollStoneMesh);
                        t.localPosition = Vector3.Lerp(lying, g, up); t.localRotation = q; t.localScale = Vector3.one * sc;
                        StoneRolling = true;
                    }
                }
            }
            for (int i = used; i < storyPool.Count; i++) if (storyPool[i] != null) storyPool[i].gameObject.SetActive(false);
        }
    }
}

namespace AquariumCiv.View
{
    /// <summary>#722 cheats (SimDebugPanel's "Stories (#722)" block; the events themselves: <see cref="StoryMoment.DebugPlayNow"/>).</summary>
    public static partial class CampCheats
    {
        /// <summary>"Tree: a kid picks a fruit": the tree shows its fruit, and the nearest kid picks one now.</summary>
        public static string FruitPick() => Do(CampUse.FruitPick);
        /// <summary>An event's scene again, with the people the box has for it (no new line).</summary>
        public static string FirstFruitAgain() => Do(CampUse.FirstFruit);
        public static string TwoMarksAgain() => Do(CampUse.TwoMarks);
        public static string SecondStoneAgain() => Do(CampUse.SecondStone);
    }
}
