// #722 batch 3 (docs/design/new-events.md §1.4, §2.1-2.4, §3.2, §3.3; animation-specs B80.b-h): CampLife's side of the last story events. (1) The
// held moments' scenes (StoryMoment asks StartStory; the builders are View/Beats/StoryScenes.Batch3.cs): the antlers carried to the new
// door, the tinkerer's spout, the gardener's ring, the joker's face, the dry spell's bucket, the first snow. (2) What they leave, every era:
// where the antlers are drawn while their pass plays (DeerHuntStage.Hold), the ring of flowers at the name stone, the face log on the woodpile
// (a look bit on the woodpile's own look), the snow figure by the fire (and the snowball game's spot near it). (3) The life uses they leave:
// the morning water on the flowers, a kid's pat on the face log, the waterer's evening bucket in a later dry spell. 2.1's crooked bucket is
// a prop the tinkerer carries (PeopleView), and pours to the left in the watering uses. Cheats: SimDebugPanel's "Stories (#722)".
using System;
using System.Collections.Generic;
using AquariumCiv.Life;
using AquariumCiv.Sim;
using UnityEngine;

namespace AquariumCiv.View
{
    public sealed partial class CampLife
    {
        const int ThingAntlers = 71, ThingFigure = 72;

        static bool IsStory3Use(CampUse u) => u >= CampUse.AntlersPass && u <= CampUse.FirstSnow;

        /// <summary>Tests: the flowers drawn at the stone, the snow figure drawn (whole), the antlers drawn in hands, this frame.</summary>
        public int FlowersDrawn { get; private set; }
        public bool FigureDrawn { get; private set; }
        public bool AntlersCarried { get; private set; }

        Mesh flowerMesh, figureMesh, snowBallMesh, carriedAntlers, carriedWhite;

        void DestroyStories3()
        {
            foreach (var m in new[] { flowerMesh, figureMesh, snowBallMesh, carriedAntlers, carriedWhite }) if (m != null) UnityEngine.Object.Destroy(m);
            flowerMesh = figureMesh = snowBallMesh = carriedAntlers = carriedWhite = null;
            DeerHuntStage.Hold = DeerHuntStage.AntlersHold.AsTheSimSays;
            KidPlayDirector.SnowFigure = null;
        }

        static CampUse UseOfStory3(string id) => id switch
        {
            StoryEvent.AntlersPassed => CampUse.AntlersPass, StoryEvent.TinkererBucket => CampUse.TinkererBucket,
            StoryEvent.GardenerFlowers => CampUse.GardenerFlowers, StoryEvent.JokerFace => CampUse.JokerFace,
            StoryEvent.DrySpell => CampUse.DrySpell, StoryEvent.FirstSnow => CampUse.FirstSnow,
            _ => CampUse.None,
        };

        CampScene Playing3(CampUse use) { foreach (var s in scenes) if (s.use == use) return s; return null; }

        // ================================================================== the held moments' scenes
        string TryStory3(CampUse use, IReadOnlyList<PersonActivity> acts, float time, int a, int b, bool force)
        {
            switch (use)
            {
                case CampUse.AntlersPass: return TryAntlersScene(acts, time, a, b, force);
                case CampUse.TinkererBucket: return TryTinkererScene(acts, time, a, force);
                case CampUse.GardenerFlowers: return TryGardenerScene(acts, time, a, force);
                case CampUse.FlowerWater: return TryFlowerWater(acts, time, force);
                case CampUse.JokerFace: return TryJokerScene(acts, time, a, force);
                case CampUse.FacePat: return TryFacePat(acts, time, force);
                case CampUse.DrySpell: case CampUse.TreeWater: return TryTreeWaterScene(use, acts, time, a, force);
                case CampUse.FirstSnow: return TryFirstSnowScene(acts, time, a, force);
            }
            return "not a story use: " + use;
        }

        /// <summary>The named one when drawn and free (a cheat: drawn), else <paramref name="fallback"/>().</summary>
        int NamedOr(IReadOnlyList<PersonActivity> acts, int named, bool force, Func<PersonActivity, bool> ok, Func<int> fallback) =>
            Castable(acts, named, force) && KeyIs(acts, named, ok) ? named : fallback();

        /// <summary>B80.b: the new chief (<paramref name="nu"/>) and the old one (<paramref name="old"/>; their family carry if they have died or
        /// are not free; a cheat: any grown-up), between the two doors' posts.</summary>
        string TryAntlersScene(IReadOnlyList<PersonActivity> acts, float time, int nu, int old, bool force)
        {
            if (!DeerHuntStage.DoorSpot(sim, old, out var oldPost)) return "the old door has no home (no post to take them from)";
            if (!DeerHuntStage.DoorSpot(sim, nu, out var newPost)) return "the new chief has no home (no door to take them to)";
            if ((oldPost - newPost).magnitude < 0.12f) return "one home: the antlers stay";
            int carrier = NamedOr(acts, old, force, x => IsGrown(x), () =>
            {
                var o = sim.FindNotable(old);
                if (o != null)
                    foreach (var n in sim.State.notables)
                    {
                        if (!n.alive || n.id == nu || n.age < SimCore.AdultAge) continue;
                        bool kin = n.partnerId == o.id || o.partnerId == n.id || n.parentId == o.id || o.parentId == n.id || n.parentId >= 0 && n.parentId == o.parentId;
                        if (kin && Castable(acts, n.id, force)) return n.id;
                    }
                return force ? Grown1(acts, oldPost, true, new[] { nu }) : Nobody;
            });
            if (carrier == Nobody) return (sim.FindNotable(old)?.name ?? "the old chief") + " (or their family) isn't free";
            int n2 = Castable(acts, nu, force) ? nu : Nobody;
            var toCam = ToCam((oldPost + newPost) * 0.5f);
            Add(Thing(StoryScenes.AntlersPass(carrier, At(carrier).Value, n2, n2 != Nobody ? At(n2).Value : default, oldPost, newPost, toCam, time), ThingAntlers));
            return null;
        }

        /// <summary>B80.c: the tinkerer, the fire, the pond's edge nearest it, and up to two free people to watch from the right.</summary>
        string TryTinkererScene(IReadOnlyList<PersonActivity> acts, float time, int named, bool force)
        {
            if (!meals.HasFire) return "no fire";
            var fire = meals.Fire;
            if (!PondEdge(fire, force ? 9f : 3f, out var edge, out var toWater)) return "no pond edge near the fire";
            int who = NamedOr(acts, named, force, x => IsGrown(x), () => Nobody);
            if (who == Nobody) return (sim.FindNotable(named)?.name ?? "the tinkerer") + " isn't free";
            var watch = Pick(acts, edge, x => IsGrown(x) || IsKid(x), 2, force, new[] { who });
            Add(Thing(StoryScenes.TinkererBucket(who, At(who).Value, watch, AtAll(watch), fire, edge, toWater, ToCam(fire), time), ThingBank));
            return null;
        }

        /// <summary>The name stone the flowers ring, as drawn (false: it isn't standing).</summary>
        bool FlowerStone(out PropSpot stone)
        {
            stone = default;
            int id = sim.StoryMarks.flowersStoneId;
            return id >= 0 && PropById(id, out stone);
        }

        string TryGardenerScene(IReadOnlyList<PersonActivity> acts, float time, int named, bool force)
        {
            if (!FlowerStone(out var stone) && !(sim.FirstNameStone is ConsequenceProp f && PropById(f.id, out stone))) return "the name stone isn't standing";
            int who = NamedOr(acts, named, force, x => IsGrown(x), () => Nobody);
            if (who == Nobody) return (sim.FindNotable(named)?.name ?? "the gardener") + " isn't free";
            Add(Thing(StoryScenes.GardenerFlowers(who, At(who).Value, stone.at, ToCam(stone.at), time), ThingOf(stone)));
            return null;
        }

        string TryFlowerWater(IReadOnlyList<PersonActivity> acts, float time, bool force)
        {
            if (!sim.FlowersAtStone || !FlowerStone(out var stone)) return "no flowers at the stone ('Trait: the gardener's flowers' first)";
            if (!force && ThingBusy(ThingOf(stone))) return "someone is at the stone";
            int who = Grown1(acts, stone.at, force);
            if (who == Nobody) return "no grown-up free";
            Add(Thing(StoryScenes.FlowerWater(who, At(who).Value, stone.at, ToCam(stone.at), who == sim.SpoutBucketUserId, time), ThingOf(stone)));
            doneToday.Add(CampUse.FlowerWater);
            return null;
        }

        /// <summary>The woodpile with the face log, as drawn (false: it isn't standing).</summary>
        bool FacePile(out PropSpot pile)
        {
            pile = default;
            int id = sim.StoryMarks.faceLogPropId;
            if (id >= 0) return PropById(id, out pile);
            return sim.WoodpileProp is ConsequenceProp p && PropById(p.id, out pile);
        }

        string TryJokerScene(IReadOnlyList<PersonActivity> acts, float time, int named, bool force)
        {
            if (!FacePile(out var pile)) return "the woodpile isn't standing";
            int who = NamedOr(acts, named, force, x => IsGrown(x), () => Nobody);
            if (who == Nobody) return (sim.FindNotable(named)?.name ?? "the joker") + " isn't free";
            var kids = Kids(acts, pile.at, 2, force, new[] { who });
            Add(Thing(StoryScenes.JokerFace(who, At(who).Value, kids, AtAll(kids), pile.at, FaceToCam(pile, false), time), ThingOf(pile)));
            return null;
        }

        string TryFacePat(IReadOnlyList<PersonActivity> acts, float time, bool force)
        {
            if (!sim.FaceLogOn || !FacePile(out var pile)) return "no face log ('Trait: the joker's face' first)";
            if (!force && ThingBusy(ThingOf(pile))) return "someone is at the woodpile";
            int kid = Kid1(acts, pile.at, force);
            if (kid == Nobody) return "no kid free ('Two kids join the camp' first)";
            Add(Thing(StoryScenes.FacePat(kid, At(kid).Value, pile.at, FaceToCam(pile, false), time), ThingOf(pile)));
            doneToday.Add(CampUse.FacePat);
            return null;
        }

        /// <summary>B80.g: the one named (the event) or the tree's waterer (the evening repeat), the pond's edge nearest the Keeper's tree, and (the
        /// event) up to two free grown-ups who look up for rain.</summary>
        string TryTreeWaterScene(CampUse use, IReadOnlyList<PersonActivity> acts, float time, int named, bool force)
        {
            if (!TreeAt(out var tree, out int stage)) return "the Keeper's tree isn't planted";
            if (!PondEdge(tree, force ? 9f : 3f, out var edge, out var toWater)) return "no pond edge near the Keeper's tree";
            if (use == CampUse.TreeWater) named = sim.TreeWaterer?.id ?? -1;
            if (use == CampUse.TreeWater && !force && ThingBusy(ThingTree)) return "someone is at the Keeper's tree";
            int who = NamedOr(acts, named, force, x => IsGrown(x), () => use == CampUse.TreeWater && force ? Grown1(acts, tree, true) : Nobody);
            if (who == Nobody) return (sim.FindNotable(named)?.name ?? "the tree's waterer") + " isn't free";
            var look = use == CampUse.DrySpell ? Pick(acts, meals != null && meals.HasFire ? meals.Fire : tree, x => IsGrown(x), 2, force, new[] { who }) : new List<int>();
            var sc = StoryScenes.TreeWater(use, who, At(who).Value, edge, toWater, tree, stage, look, AtAll(look), ToCam(tree), who == sim.SpoutBucketUserId, time);
            Add(Thing(sc, ThingTree));
            if (use == CampUse.TreeWater) doneToday.Add(CampUse.TreeWater);
            return null;
        }

        /// <summary>B80.h: the kid named (else the nearest free kid), up to three free people near them, by the fire.</summary>
        string TryFirstSnowScene(IReadOnlyList<PersonActivity> acts, float time, int named, bool force)
        {
            if (!meals.HasFire) return "no fire";
            var fire = meals.Fire;
            int kid = NamedOr(acts, named, force, x => IsKid(x), () => Kid1(acts, fire, force));
            if (kid == Nobody) return "no kid free ('Two kids join the camp' first)";
            var others = Pick(acts, At(kid).Value, x => IsKid(x) || IsGrown(x), 3, force, new[] { kid });
            var isKid = new List<bool>();
            foreach (int o in others) isKid.Add(KeyIs(acts, o, x => IsKid(x)));
            Add(Thing(StoryScenes.FirstSnow(kid, At(kid).Value, others, AtAll(others), isKid, fire, ToCam(fire), time), ThingFigure));
            return null;
        }

        /// <summary>The camp cheat (CampLife.Force): plays a batch-3 scene again with the people the box has for it (no new line).</summary>
        string ForceStory3(CampUse use)
        {
            var mk = sim.StoryMarks;
            int a = -1, b = -1;
            switch (use)
            {
                case CampUse.AntlersPass:
                    if (mk.antlersFromId < 0) return "the antlers haven't passed ('Chief: pass the antlers' first)";
                    a = mk.antlersAtId; b = mk.antlersFromId; break;
                case CampUse.TinkererBucket: a = mk.bucketOwnerId >= 0 ? sim.SpoutBucketUserId : -1; if (a < 0) return "no tinkerer's bucket ('Trait: the tinkerer's bucket' first)"; break;
                case CampUse.GardenerFlowers: a = mk.gardenerId; if (a < 0) return "no gardener's flowers ('Trait: the gardener's flowers' first)"; break;
                case CampUse.JokerFace: a = mk.jokerId; if (a < 0) return "no joker's face ('Trait: the joker's face' first)"; break;
                case CampUse.DrySpell: a = sim.TreeWaterer?.id ?? -1; if (a < 0) return "no waterer yet ('Weather: the dry spell' first)"; break;
                case CampUse.FirstSnow: a = LastStoryNotable(StoryEvent.FirstSnow); break;
            }
            string why = TryStory3(use, pv.Activities, clock, a, b, true);
            return why ?? use switch
            {
                CampUse.FlowerWater => "someone waters the flowers at the stone",
                CampUse.FacePat => "a kid pats the face log",
                CampUse.TreeWater => "the waterer carries a bucket to the Keeper's tree",
                _ => use + " plays",
            };
        }

        // ================================================================== each frame (every era)
        const int FaceBit = ConsequenceMeshes.FaceLook;

        void StepStories3(IReadOnlyList<PersonActivity> acts, float time)
        {
            var mk = sim.StoryMarks;
            // B80.b: the antlers at the old door while their pass waits or before the lift, in the hands while carried, then as the sim says
            var pass = Playing3(CampUse.AntlersPass);
            if (pass != null) { float u = time - pass.t0; DeerHuntStage.Hold = u < pass.a ? DeerHuntStage.AntlersHold.AtOldDoor : u < pass.b ? DeerHuntStage.AntlersHold.InHands : DeerHuntStage.AntlersHold.AsTheSimSays; }
            else DeerHuntStage.Hold = mk.antlersFromId >= 0 && StoryWaiting(StoryEvent.AntlersPassed) != null ? DeerHuntStage.AntlersHold.AtOldDoor : DeerHuntStage.AntlersHold.AsTheSimSays;
            // B80.f: the face log on the woodpile from the carve's hit (not while its moment still waits)
            if (mk.faceLogPropId >= 0)
            {
                var carve = Playing3(CampUse.JokerFace);
                bool on = sim.FaceLogOn && (carve != null ? time - carve.t0 >= carve.a : StoryWaiting(StoryEvent.JokerFace) == null);
                int cur = ConsequencePropsView.LookOf(mk.faceLogPropId), want = on ? cur | FaceBit : cur & ~FaceBit;
                if (want != cur) ConsequencePropsView.SetLook(mk.faceLogPropId, want);
            }
            // B80.h: the snowball game starts near the snow figure while it stands
            KidPlayDirector.SnowFigure = FigureNow(time, out _, out _) ? FigureAt : (Vector2?)null;
            // the life uses (by day, never over a meal)
            if (OnlyCheats || meals == null || meals.Eaters > 0) return;
            float h = Hour;
            if (h >= 7f && h < 10f && sim.FlowersAtStone && StoryWaiting(StoryEvent.GardenerFlowers) == null && Life3(CampUse.FlowerWater, Often) && Tried3(CampUse.FlowerWater, TryFlowerWater(acts, time, false))) return;
            if (h >= 9f && h < 17f && sim.FaceLogOn && StoryWaiting(StoryEvent.JokerFace) == null && Life3(CampUse.FacePat, Sometimes) && Tried3(CampUse.FacePat, TryFacePat(acts, time, false))) return;
            if (h >= 16f && h < 18.3f && sim.InDrySpell && sim.TreeWaterer != null && StoryWaiting(StoryEvent.DrySpell) == null && Life3(CampUse.TreeWater, Often))
                Tried3(CampUse.TreeWater, TryTreeWaterScene(CampUse.TreeWater, acts, time, -1, false));
        }

        /// <summary>Once a day, by the day's roll, not soon after the last, not tried in the last 3 s.</summary>
        bool Life3(CampUse u, float chance)
        {
            if (doneToday.Contains(u) || Roll(u) >= chance || !LifeOk(u)) return false;
            if (triedAt.TryGetValue(u, out float tr) && clock - tr < 3f && clock >= tr) return false;
            triedAt[u] = clock;
            return true;
        }
        static bool Tried3(CampUse u, string why) => why == null;

        // ================================================================== drawing
        /// <summary>B80.h: where the snow figure stands (by the fire, toward the front glass).</summary>
        Vector2 FigureAt => meals != null && meals.HasFire ? StoryScenes.FigureSpot(meals.Fire) : Vector2.zero;

        /// <summary>B80.h: the figure as it is now: none, the base alone (rolled in, its head not on), or whole; and the rolling base.</summary>
        bool FigureNow(float time, out bool whole, out CampScene rolling)
        {
            whole = false; rolling = Playing3(CampUse.FirstSnow);
            if (meals == null || !meals.HasFire) return false;
            if (rolling != null) { float u = time - rolling.t0; whole = u >= rolling.c; return u >= rolling.b; }
            whole = sim.SnowFigureStands && StoryWaiting(StoryEvent.FirstSnow) == null;
            return whole;
        }

        static Mesh FlowerMesh()
        {
            var mb = new MeshBuilder(7231);
            TreeTipMesh(CampTipKind.Flower, mb);
            var m = mb.ToMesh("Story_StoneFlower"); m.hideFlags = HideFlags.DontSave; return m;
        }

        /// <summary>B80.h "SnowFigure": two white balls (MeshBuilder.Ico), the head on the base, two dark pebble eyes on the front (local −z).</summary>
        static Mesh FigureMesh(bool head)
        {
            var mb = new MeshBuilder(head ? 7232 : 7233);
            var white = new Color(0.97f, 0.98f, 1f); var shade = new Color(0.86f, 0.9f, 0.97f);
            float br = StoryScenes.BaseR, hr = StoryScenes.HeadR;
            mb.Ico(new Vector3(0f, br * 0.9f, 0f), br, Vector3.one, white, 0.04f, shade);
            if (head)
            {
                float hy = br * 1.8f + hr * 0.85f;
                mb.Ico(new Vector3(0f, hy, 0f), hr, Vector3.one, white, 0.04f, shade);
                foreach (float s in new[] { -1f, 1f }) mb.Box(new Vector3(s * 0.009f, hy + 0.006f, -hr * 0.92f), new Vector3(0.005f, 0.005f, 0.004f), new Color(0.2f, 0.19f, 0.18f));
            }
            var m = mb.ToMesh(head ? "Story_SnowFigure" : "Story_SnowBase"); m.hideFlags = HideFlags.DontSave; return m;
        }

        int DrawStories3(Transform root, Material material, float time, Func<float, float, float> groundAt, int used)
        {
            FlowersDrawn = 0; FigureDrawn = false; AntlersCarried = false;
            if (sim == null) return used;
            Vector3 On(Vector2 p, float y = 0f) => new Vector3(p.x, groundAt(p.x, p.y) + y, p.y);
            // B80.b: the antlers in the carrier's hands between the lift and the set
            var pass = Playing3(CampUse.AntlersPass);
            if (pass != null && pv != null)
            {
                float u = time - pass.t0;
                var v = u >= pass.a && u < pass.b ? pv.ViewOf(pass.keys[0]) : null;
                if (v != null && v.gameObject.activeInHierarchy)
                {
                    bool white = sim.WhiteStagAntlers;
                    var mesh = white ? (carriedWhite ??= DeerHuntStage.CarriedAntlers(true)) : (carriedAntlers ??= DeerHuntStage.CarriedAntlers(false));
                    var t = StoryPiece(root, material, used++, mesh);
                    var hands = HandsOf(root, v);
                    t.localPosition = hands + Vector3.up * 0.01f; t.localRotation = Quaternion.Euler(0f, v.transform.eulerAngles.y, 0f);
                    AntlersCarried = true;
                }
            }
            // B80.e: the ring of flowers round the name stone (pressed in one by one in its scene; none while its moment waits)
            if (sim.FlowersAtStone && FlowerStone(out var stone))
            {
                int shown = StoryScenes.Flowers;
                var plant = Playing3(CampUse.GardenerFlowers);
                if (plant != null) { shown = 0; float u = time - plant.t0; foreach (var f in plant.fx) if (f.kind == CampFxKind.Grass && f.time <= u) shown++; }
                else if (StoryWaiting(StoryEvent.GardenerFlowers) != null) shown = 0;
                flowerMesh ??= FlowerMesh();
                for (int i = 0; i < shown && i < StoryScenes.Flowers; i++)
                {
                    var t = StoryPiece(root, material, used++, flowerMesh);
                    t.localPosition = On(StoryScenes.FlowerAt(stone.at, i)); t.localScale = Vector3.one * 0.018f;
                    t.localRotation = Quaternion.Euler(0f, i * 61f, (i % 2 == 0 ? 6f : -5f));
                    FlowersDrawn++;
                }
            }
            // B80.h: the snow figure (the base rolled in, growing; then the head on), until the thaw
            if (FigureNow(time, out bool whole, out var roll) || roll != null)
            {
                if (roll != null && !whole && StoryScenes.BaseNow(roll, time - roll.t0, out var at, out float r))
                {
                    var t = StoryPiece(root, material, used++, snowBallMesh ??= FigureMesh(false));
                    t.localPosition = On(at); t.localScale = Vector3.one * (r / StoryScenes.BaseR);
                    t.localRotation = Quaternion.Euler((time - roll.t0) * 90f, 0f, 0f);
                }
                else if (whole)
                {
                    var t = StoryPiece(root, material, used++, figureMesh ??= FigureMesh(true));
                    t.localPosition = On(FigureAt);
                    FigureDrawn = true;
                }
            }
            return used;
        }
    }
}

namespace AquariumCiv.View
{
    /// <summary>#722 batch 3 cheats (SimDebugPanel's "Stories (#722)" block): the life uses and each scene again.</summary>
    public static partial class CampCheats
    {
        /// <summary>"Stone: water the flowers": the morning water on the gardener's flowers, now.</summary>
        public static string FlowerWater() => Do(CampUse.FlowerWater);
        /// <summary>"Woodpile: a kid pats the face": a kid pats the joker's face log as they pass, now.</summary>
        public static string FacePat() => Do(CampUse.FacePat);
        /// <summary>"Tree: the waterer's evening bucket": the dry spell's repeat (the waterer, or any grown-up), now.</summary>
        public static string TreeWater() => Do(CampUse.TreeWater);
        public static string AntlersPassAgain() => Do(CampUse.AntlersPass);
        public static string TinkererBucketAgain() => Do(CampUse.TinkererBucket);
        public static string GardenerFlowersAgain() => Do(CampUse.GardenerFlowers);
        public static string JokerFaceAgain() => Do(CampUse.JokerFace);
        public static string DrySpellAgain() => Do(CampUse.DrySpell);
        public static string FirstSnowAgain() => Do(CampUse.FirstSnow);
    }
}
