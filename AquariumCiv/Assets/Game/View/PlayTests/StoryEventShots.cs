// #722 (B78.a-c, B80.a-h): the story events in a lived-in camp box, for sign-off. StoryEventShots (Explicit): each test opens a fresh box at
// phone size (750x1334): the camp era, a fire, tents, 7 grown-ups and 4 kids, grass grown over the land, the Keeper's tree grown (a pond where a
// scene needs one). Then the SimDebugPanel "Stories (#722)" cheat the user presses on a phone (it sets the season, frees the cast, ends what
// plays), and the scene filmed: the camera follows the scene's people (CampLife.FocusNow, the AnimLab "camp" follow), close enough that a
// person is about 80 px tall, one frame every 0.5 s of the whole scene to Temp/agent/722/<event>/f###.png (for a GIF), then one "after" still
// of what stays. Each test asserts the scene started, its cast has the people the line names, and the caption said the line.
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AquariumCiv.Game;
using AquariumCiv.GameUI;
using AquariumCiv.Life;
using AquariumCiv.Lighting;
using AquariumCiv.Sim;
using AquariumCiv.View.ShowMe;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AquariumCiv.View.PlayTests
{
    [Explicit, Category("StoryEventShots")]
    public class StoryEventShots : PlayTestBase
    {
        const int W = 750, H = 1334;
        /// <summary>Seconds of game time between two frames, and the person's height on screen the camera eases to (px).</summary>
        const float FrameEvery = 0.5f, DollPx = 80f, MinPx = 60f, MaxPx = 100f, Pitch = 36f;
        /// <summary>The longest any scene is filmed (s): B78.c, the longest story scene, is ~45 s.</summary>
        const float MaxScene = 100f;

        static SimCore Sim => GameClock.Instance != null ? GameClock.Instance.Sim : null;
        static PeopleView Pv => PeopleView.Find();

        [TearDown]
        public void Restore()
        {
            CampLife.CheatFruit = false;
            CampLife.OnlyCheats = false;
            var pv = Pv;
            if (pv != null && pv.Planner != null) pv.Planner.ForcedWeather = null;
            var rig = Camera.main != null ? Camera.main.GetComponent<BoxCamera>() : null;
            if (rig != null) rig.minZoom = CameraReach.MinZoom;
        }

        // ================================================================== the box

        /// <summary>A lived-in camp: the camp era with its fire, tents and stock; grass misted over the land and grown (four seasons); grown-ups
        /// to <paramref name="grownUps"/> and <paramref name="kids"/> kids; the far wood; the Keeper's tree grown; a pond if
        /// <paramref name="pond"/>. No natural camp use starts (CampLife.OnlyCheats), the staged moments the skips left are ended, the view
        /// at <paramref name="hour"/>. The recipe of Fb5PassShots.Stage and CampCheats.StageCamp.</summary>
        IEnumerator LivedCamp(float hour, bool pond = false, int grownUps = 7, int kids = 4)
        {
            UseLivingLand(true);
            GameUiPanel.AcceptInputUnfocused(true);
            ShowMeDirector.BubblesOn = false;
            yield return OpenSlice(W, H, seed: 4242);
            GameUiPanel.AcceptInputUnfocused(true);
            ShowMeDirector.BubblesOn = false;
            var sim = Sim;
            Assert.IsNotNull(sim, "a living box");
            PeopleView.DebugSetHour(hour);
            foreach (var (x, z) in new[] { (-1.6f, 0.6f), (-1.0f, -0.8f), (-2.0f, -0.2f), (1.6f, 0.9f), (0.8f, -0.9f) }) EcoDebug.MistBig(x, z);
            EcoDebug.SkipSeasons(1);
            EcoDebug.OpenCampGate();
            for (int i = 0; i < 12 && sim.State.era != Era.Camp; i++) { EcoDebug.MistCamp(); EcoDebug.SkipSeasons(1); }
            for (int k = 0; k < 6; k++)
                foreach (var (x, z) in new[] { (-2.4f, 1.2f), (0f, 1.4f), (2.4f, -1f), (0.5f, -1.6f), (-0.5f, 0.2f), (2f, 1.5f) }) EcoDebug.MistBig(x, z);
            int adults = sim.State.notables.Count(n => n.alive && n.age >= SimCore.AdultAge);
            if (adults < grownUps) sim.DebugAddKids(grownUps - adults, 21f); // grown-ups who join a family (a fresh land has five)
            EcoDebug.SkipSeasons(4); // the grass grows in, the newcomers find work
            if (pond) Debug.Log("[722 shots] " + EcoDebug.WaterBreaksNow());
            sim.DebugGrowFarWood();
            if (!sim.WoodpileStands) Debug.Log("[722 shots] " + NewStartView.DebugFirstWildWoodNow()); // a lived camp has reached its first wild wood
            sim.MarkFirstWildWoodSeen();
            if (!sim.StockSetDown) sim.SetDownStock();
            sim.DebugFireStage(2);
            UnityEngine.Object.FindAnyObjectByType<BoxView>().Rebuild();
            Debug.Log("[722 shots] " + CampCheats.GrowTree());
            if (kids > 0) sim.DebugAddKids(kids, 7f);
            sim.State.food = Mathf.Max(sim.State.food, 4f * sim.State.notables.Count(n => n.alive)); // a lived camp eats (a hungry one says the new start's hunger line)
            CampLife.OnlyCheats = true; // a clip shows its one scene: no natural use starts over it
            // the skips leave staged moments (a wedding, a new chief, the newborns) holding people: end them until none comes for 2 s
            for (int calm = 0, f = 0; calm < 120 && f < 1500; f++) { calm = HeldMomentStage.DebugEndPlaying() == 0 ? calm + 1 : 0; yield return null; }
            if (pond) yield return Until(() => FirstWaterView.Instance == null || !FirstWaterView.Instance.Playing, 60f);
            PeopleView.DebugSetHour(hour);
            yield return new WaitForSecondsRealtime(2f);
            int people = sim.State.notables.Count(n => n.alive);
            Debug.Log($"[722 shots] box: era {sim.State.era}, {LifeClock.SeasonOf(sim.State.month)}, people {people} (counted {sim.State.population}), food {sim.State.food:0}, bare {sim.EcoBareShare:0.00}, meadow {sim.EcoMeadowShare:0.00}, pond cells {sim.EcoPondCells}, tree stage {sim.EcoTree.stage}");
            Assert.GreaterOrEqual(people, 8, "a lived-in camp (8-12 people)");
            if (sim.State.population < people) { Debug.Log($"[722 shots] the count was {sim.State.population} (the season skips' crowd deaths): {people}"); sim.State.population = people; }
            Assert.IsNull(NewStartView.HungerLineFor(sim), "a lived camp isn't hungry");
            Assert.AreEqual(Era.Camp, sim.State.era);
        }

        // ================================================================== the camera and the frames

        static BoxCamera Rig => Camera.main.GetComponent<BoxCamera>();
        static BoxView Box => UnityEngine.Object.FindAnyObjectByType<BoxView>();

        /// <summary>A standing person's height on screen at box spot <paramref name="at"/> (px), with the doll's own growth with the zoom.</summary>
        static float PersonPx(BoxCamera rig, BoxView box, Vector2 at)
        {
            var cam = Camera.main;
            float h = CampFrame.DollHeight * CameraReach.DollScale(rig.zoom, PeopleView.DollScaleAmount);
            float y = box.SurfaceHeight(at.x, at.y);
            var a = cam.WorldToScreenPoint(box.Root.TransformPoint(new Vector3(at.x, y, at.y)));
            var b = cam.WorldToScreenPoint(box.Root.TransformPoint(new Vector3(at.x, y + h, at.y)));
            return Mathf.Abs(b.y - a.y);
        }

        /// <summary>One frame of the framing: the camera's target eases to the middle of <paramref name="pts"/> (a snap on the first), and the
        /// zoom to the one that holds them all with a margin (inside the middle 80 % across, 70 % up and down), a person ~<see cref="DollPx"/>
        /// tall when they are close together, never under <see cref="MinPx"/> or over <see cref="MaxPx"/>. Returns the person's height now.</summary>
        static float Frame(IReadOnlyList<Vector2> pts, bool snap)
        {
            var rig = Rig; var box = Box; var cam = Camera.main;
            rig.CancelFlight();
            rig.minZoom = 0.03f;
            var c = Vector2.zero; foreach (var p in pts) c += p; c /= Mathf.Max(1, pts.Count);
            var w = box.Root.TransformPoint(new Vector3(c.x, box.SurfaceHeight(c.x, c.y) + 0.06f, c.y));
            rig.target = snap ? w : Vector3.Lerp(rig.target, w, 0.1f);
            rig.pitch = Pitch;
            rig.Apply();
            float px = PersonPx(rig, box, c);
            if (px <= 1f) return px;
            float h = CampFrame.DollHeight * CameraReach.DollScale(rig.zoom, PeopleView.DollScaleAmount);
            float ex = 0f, ey = 0f;
            foreach (var p in pts)
            {
                float y = box.SurfaceHeight(p.x, p.y);
                foreach (float up in new[] { 0f, h })
                {
                    var v = cam.WorldToViewportPoint(box.Root.TransformPoint(new Vector3(p.x, y + up, p.y)));
                    ex = Mathf.Max(ex, Mathf.Abs(v.x - 0.5f) * 2f); ey = Mathf.Max(ey, Mathf.Abs(v.y - 0.5f) * 2f);
                }
            }
            float z = rig.zoom;
            float fit = z * Mathf.Max(ex / 0.8f, ey / 0.7f), near = z * px / DollPx; // a zoom further out holds more, a person smaller
            float want = Mathf.Clamp(Mathf.Max(fit, near), z * px / MaxPx, z * px / MinPx);
            rig.zoom = Mathf.Clamp(snap ? want : Mathf.Lerp(z, want, 0.12f), 0.03f, rig.maxZoom);
            rig.Apply();
            return PersonPx(rig, box, c);
        }

        static float Aim(Vector2 at, bool snap) => Frame(new[] { at }, snap);

        /// <summary>Where the drawn ones of <paramref name="keys"/> stand now.</summary>
        static List<Vector2> Spots(IEnumerable<int> keys)
        {
            var r = new List<Vector2>();
            if (keys == null) return r;
            foreach (int k in keys.Distinct()) if (Where(k, out var p)) r.Add(p);
            return r;
        }

        /// <summary>A camp scene's cast to frame: its members a part holds now (the ones still in it), else all its members.</summary>
        static Func<IEnumerable<int>> CastOf(CampScene sc) => () =>
        {
            var pv = Pv;
            var on = sc.keys.Where(k => pv != null && pv.Camp.Cued(k)).ToList();
            return on.Count > 0 ? on : sc.keys;
        };

        /// <summary>A staged moment's cast: its plan's members, the named ones (the couple, the leader) first.</summary>
        static Func<IEnumerable<int>> CastOf(HeldMomentScene sc, params int[] named) => () =>
            named.Length > 0 ? named.Where(k => k >= 0) : sc.plan.members.Select(m => m.key);

        static string Dir(string ev) => $"722/{ev}";

        /// <summary>Clears the event's folder (a GIF from a stale run must not mix in).</summary>
        static void Fresh(string ev)
        {
            string d = Shots + Dir(ev);
            try { if (Directory.Exists(d)) Directory.Delete(d, true); } catch (Exception e) { Debug.LogWarning("[722 shots] " + e.Message); }
        }

        /// <summary>Films while <paramref name="playing"/>: the camera frames <paramref name="cast"/> (their middle, all of them in the frame; the
        /// <paramref name="fallback"/> spot when none is drawn), a frame every <see cref="FrameEvery"/> s of game time (f000, f001, ...), at most
        /// <paramref name="max"/> s. The frames filmed.</summary>
        /// <summary>How far from a held scene's thing its staged spots still count for the still frame [est].</summary>
        const float HoldReach = 0.7f;

        IEnumerator Film(string ev, Func<bool> playing, Func<IEnumerable<int>> cast, Func<Vector2?> fallback, float max, List<int> frames, List<Vector2> hold = null)
        {
            int f = 0; float t = 0f, next = 0f, px = 0f, lo = float.MaxValue, hi = 0f;
            List<Vector2> Pts() { if (hold != null) return hold; var p = Spots(cast()); if (p.Count == 0 && fallback() is Vector2 fb) p.Add(fb); return p; }
            var last = Pts();
            for (int settle = 0; settle < 10 && last.Count > 0; settle++) { px = Frame(last, settle == 0); yield return null; } // the zoom lands first
            while (t < max && playing())
            {
                var now = Pts();
                if (now.Count > 0) last = now;
                if (last.Count > 0) px = Frame(last, false);
                if (t >= next - 1e-4f)
                {
                    yield return Shot($"{Dir(ev)}/f{f:000}");
                    f++; next += every; lo = Mathf.Min(lo, px); hi = Mathf.Max(hi, px);
                }
                else yield return null;
                t += Time.deltaTime;
            }
            frames.Add(f); every = FrameEvery;
            Debug.Log($"[722 shots] {ev}: {f} frames over {t:0.0} s, a person {lo:0}-{hi:0} px tall, framing {last.Count}");
        }

        /// <summary>The "after" still: the camera on <paramref name="spot"/> (eased in over ~1.5 s), then the shot.</summary>
        IEnumerator After(string ev, Vector2? spot)
        {
            if (!(spot is Vector2 at)) { Debug.Log($"[722 shots] {ev}: no spot for the after still"); yield break; }
            float px = 0f;
            for (int i = 0; i < 45; i++) { px = Aim(at, i == 0); yield return null; }
            yield return Shot($"{Dir(ev)}/after");
            Debug.Log($"[722 shots] {ev}: after still at ({at.x:0.00}, {at.y:0.00}), a person {px:0} px tall");
        }

        static bool Where(int key, out Vector2 at)
        {
            at = default;
            var pv = Pv;
            if (pv == null || key < 0) return false;
            var who = new List<PersonRef>(); var xs = new List<float>(); var zs = new List<float>();
            pv.StandingNow(who, xs, zs);
            for (int i = 0; i < who.Count; i++) if (who[i].Key == key) { at = new Vector2(xs[i], zs[i]); return true; }
            return false;
        }

        /// <summary>The middle of the people drawn of <paramref name="keys"/>, or null.</summary>
        static Vector2? Middle(params int[] keys)
        {
            var sum = Vector2.zero; int n = 0;
            foreach (int k in keys) if (Where(k, out var p)) { sum += p; n++; }
            return n == 0 ? (Vector2?)null : sum / n;
        }

        static Vector2? Fire()
        {
            var pv = Pv; var l = new List<Vector2>();
            pv?.FireMiddles(l);
            return l.Count > 0 ? l[0] : TreeSpot();
        }

        static Vector2? TreeSpot() { var s = Sim; if (s == null || !s.EcologyOn) return null; var t = s.EcoTree; return new Vector2(t.x, t.z); }
        static Vector2? PropSpot(int id) { var p = id >= 0 ? Sim?.FindConsequenceProp(id) : null; return p != null ? new Vector2(p.x, p.z) : (Vector2?)null; }
        static Vector2? Door(int id) => DeerHuntStage.DoorSpot(Sim, id, out var at) ? at : (Vector2?)null;
        static Vector2? Person(int id) => Where(id, out var at) ? at : (Vector2?)null;

        /// <summary>The setup's thing (a choice's prop of <paramref name="kind"/>) is drawn in the box, so a scene at it can be cast (bounded).</summary>
        static IEnumerator UntilThing(string kind)
        {
            var sim = Sim; var pv = Pv;
            var p = sim.State.consequenceProps?.FirstOrDefault(x => x.kind == kind && x.placed);
            Assert.IsNotNull(p, kind + " is placed");
            yield return Until(() => pv.Camp.PropStands(p.id), 8f);
            Assert.IsTrue(pv.Camp.PropStands(p.id), kind + " stands in the box");
        }

        static ChronicleEntry LineOf(string id) => Sim.State.chronicle.LastOrDefault(x => x.kind == StoryEvent.Kind(id));

        static CampScene SceneOf(CampUse use) => Pv.Camp.Scenes.LastOrDefault(s => s.use == use);

        // ================================================================== a story event: its cheat, its scene filmed, what stays

        /// <summary>
        /// A lived-in camp at <paramref name="hour"/>, the <paramref name="setup"/>, then the event's cheat (StoryMoment.DebugPlayNow, the panel's
        /// button); the scene must start, cast <paramref name="named"/>(its line) and say the line on the caption; it is filmed to its end, then
        /// <paramref name="before"/> (what stays, for the still) and the after still at <paramref name="left"/>.
        /// </summary>
        IEnumerator Story(string ev, string id, CampUse use, float hour, Action setup, Func<ChronicleEntry, int[]> named, Func<Vector2?> left,
            bool pond = false, Action before = null, string thing = null)
        {
            Fresh(ev);
            yield return LivedCamp(hour, pond);
            setup?.Invoke();
            if (thing != null) yield return UntilThing(thing);
            yield return new WaitForSecondsRealtime(1.5f);
            int started = StoryMoment.Started;
            string r = StoryMoment.DebugPlayNow(id);
            Debug.Log($"[722 shots] {ev}: {r}");
            var pv = Pv;
            yield return Until(() => pv.Camp.Playing(use), 15f); // the stage looks every second (a welcome greeting, 10 s, goes first)
            Assert.IsTrue(pv.Camp.Playing(use), $"{ev}: the scene plays ({r}; {StoryMoment.LastWhy})");
            Assert.Greater(StoryMoment.Started, started, ev + ": its held moment started");
            var e = LineOf(id);
            Assert.IsNotNull(e, ev + ": its line");
            var sc = SceneOf(use);
            Debug.Log($"[722 shots] {ev}: beats a {sc.a:0.0} b {sc.b:0.0} c {sc.c:0.0} d {sc.d:0.0} of {sc.Length:0.0} s; starts " + string.Join(" ", System.Linq.Enumerable.Select(sc.parts, l => l.Count > 0 ? $"{l[0].from:0.0}-{l[0].to:0.0}" : "-")));
            Debug.Log($"[722 shots] {ev}: line \"{e.text}\"; cast {string.Join(",", sc.keys)}; season {LifeClock.SeasonOf(pv.Now.day)}, snow {SeasonLook.Snow:0.00}");
            foreach (int k in named(e)) Assert.Contains(k, sc.keys, $"{ev}: {Sim.FindNotable(k)?.name ?? k.ToString()}, whom the line names, is in the scene");
            Assert.AreEqual(e.text, StoryMoment.LastCaption, ev + ": the caption says the line");
            var frames = new List<int>();
            // a scene that holds the game's camera on its thing (holdFocus) is filmed the same way: one still frame round the spots it stages
            // near the thing, not chasing whoever walks in from across the camp (the user: the stone's camera wandered)
            List<Vector2> hold = null;
            if (sc.holdFocus)
            {
                hold = new List<Vector2> { sc.focus };
                foreach (var l in sc.parts) foreach (var pt in l) if ((pt.pos - sc.focus).magnitude < HoldReach) hold.Add(pt.pos);
            }
            yield return Film(ev, () => pv.Camp.Playing(use), CastOf(sc), () => pv.Camp.FocusNow(), MaxScene, frames, hold);
            Assert.IsFalse(pv.Camp.Playing(use), ev + ": the scene ends");
            Assert.Greater(frames[0], 4, ev + ": frames of the scene");
            yield return new WaitForSecondsRealtime(1f);
            before?.Invoke();
            yield return After(ev, left());
            Debug.Log($"[722 shots] {ev}: fruit {pv.Camp.FruitDrawn}, marks {pv.Camp.MarksDrawn}, flowers {pv.Camp.FlowersDrawn}, figure {pv.Camp.FigureDrawn}; {pv.Camp.PropMarks()}");
            CloseSlice();
        }

        static int[] Named(ChronicleEntry e) => new[] { e.notableId };
        static int[] Both(ChronicleEntry e) => new[] { e.notableId, e.otherNotableId };

        /// <summary>B78.a in summer: the kid the line names picks the fruit and it goes round. After: the fruit every summer after the first
        /// (that summer's one was picked, so the still shows the next summers' look: CampLife.CheatFruit).</summary>
        [UnityTest, Timeout(600000)]
        public IEnumerator B78a_FirstFruit() => Fast(0.15f, () => Story("b78a-first-fruit", StoryEvent.FirstFruit, CampUse.FirstFruit, 10f, null, Named, TreeSpot,
            before: () => CampLife.CheatFruit = true));

        /// <summary>The frame interval of the next clip (the fruit's fall is 0.45 s: those clips film every 0.15 s); back to
        /// <see cref="FrameEvery"/> after each clip.</summary>
        static float every = FrameEvery;
        static IEnumerator Fast(float e, Func<IEnumerator> clip) { every = e; return clip(); }

        /// <summary>B78.b toward dusk: the couple cut their marks. After: the two notches on the trunk.</summary>
        [UnityTest, Timeout(600000)]
        public IEnumerator B78b_TwoMarks() => Story("b78b-two-marks", StoryEvent.TwoMarks, CampUse.TwoMarks, 16.5f, null, Both, TreeSpot);

        /// <summary>B78.c: the name stone full, the second rolled in. After: the second stone beside the first.</summary>
        [UnityTest, Timeout(600000)]
        public IEnumerator B78c_SecondStone() => Story("b78c-second-stone", StoryEvent.SecondStone, CampUse.SecondStone, 10f, () =>
        {
            Debug.Log("[722 shots] " + CampCheats.OnlyThing(ConsequenceKind.NameStone));
            var sim = Sim;
            var stone = sim?.FirstNameStone;
            if (stone != null) stone.dueMonth = sim.State.month - 1; // a death after the stone stood
            Debug.Log("[722 shots] " + ConsequenceCheats.OneMoreDeath());
        }, Named, () =>
        {
            var sim = Sim; var e = sim.State.eco; var a = sim.FirstNameStone; var b = sim.FindConsequenceProp(sim.StoryMarks.secondStoneId);
            Debug.Log($"[722 shots] b78c places: first ({a?.x:0.00}, {a?.z:0.00}), second ({b?.x:0.00}, {b?.z:0.00}), eco {sim.EcologyOn}, tree cell {e?.treeCell} at ({(e != null && e.treeCell >= 0 ? AquariumCiv.Sim.Settle.SiteMap.CXc(e.treeCell) : 0f):0.00}, {(e != null && e.treeCell >= 0 ? AquariumCiv.Sim.Settle.SiteMap.CZc(e.treeCell) : 0f):0.00}), tree spot {TreeSpot()}");
            var stands = PropSpot(sim.StoryMarks.secondStoneId);
            Assert.IsTrue(Pv.Camp.StoneRaisedAt.HasValue, "the scene raised it");
            Assert.IsTrue(ConsequencePropsView.Instance.DrawnSpot(sim.StoryMarks.secondStoneId, out var drawn), "it stands");
            Assert.Less((drawn - Pv.Camp.StoneRaisedAt.Value).magnitude, 0.01f, $"it stands where it was raised (the user: it slid aside once up): raised {Pv.Camp.StoneRaisedAt.Value}, drawn {drawn}, saved {stands}");
            return stands;
        }, thing: ConsequenceKind.NameStone);

        /// <summary>The #722 tester's box: a lived-in camp WITH a pond and the natural camp uses ON (the clips switch them off). The first snow
        /// and the second stone must each start their scene from the cheat (the tester: the snow never cast a kid; the stone, anchored at the
        /// pond, waited for a camp spot that never came and held every later story back).</summary>
        [UnityTest, Timeout(600000)]
        public IEnumerator LivedCamp_SnowAndStonePlay()
        {
            yield return LivedCamp(10f, pond: true);
            CampLife.OnlyCheats = false; // an ordinary box: kids play, people go about the camp
            yield return new WaitForSecondsRealtime(20f);
            var fails = new List<string>();
            int started = StoryMoment.Started;
            Debug.Log("[722 lived] snow: " + StoryMoment.DebugPlayNow(StoryEvent.FirstSnow));
            yield return Until(() => StoryMoment.Started > started, 20f);
            if (StoryMoment.Started == started) fails.Add("the first snow: " + StoryMoment.LastWhy);
            for (int calm = 0, f = 0; calm < 60 && f < 3000; f++) { calm = HeldMomentStage.DebugEndPlaying() == 0 ? calm + 1 : 0; yield return null; }
            Pv.Planner.ForcedWeather = null;
            Debug.Log("[722 lived] " + CampCheats.OnlyThing(ConsequenceKind.NameStone));
            var sim = Sim;
            var stone = sim.FirstNameStone;
            Assert.IsNotNull(stone, "the name stone stands");
            Debug.Log($"[722 lived] the name stone: anchor {stone.anchor} at ({stone.x:0.00}, {stone.z:0.00})");
            stone.dueMonth = sim.State.month - 1;
            Debug.Log("[722 lived] " + ConsequenceCheats.OneMoreDeath());
            yield return UntilThing(ConsequenceKind.NameStone);
            yield return new WaitForSecondsRealtime(1.5f);
            started = StoryMoment.Started;
            Debug.Log("[722 lived] stone: " + StoryMoment.DebugPlayNow(StoryEvent.SecondStone));
            yield return Until(() => StoryMoment.Started > started, 20f);
            if (StoryMoment.Started == started) fails.Add("the second stone: " + StoryMoment.LastWhy);
            else
            {
                yield return Until(() => sim.PlayingMoments().Count == 0, MaxScene);
                yield return new WaitForSecondsRealtime(2f);
                Assert.IsTrue(ConsequencePropsView.Instance.DrawnSpot(sim.StoryMarks.secondStoneId, out var drawn), "the second stone stands");
                Assert.IsTrue(Pv.Camp.StoneRaisedAt.HasValue, "the scene raised it");
                Assert.Less((drawn - Pv.Camp.StoneRaisedAt.Value).magnitude, 0.01f, $"it stands where it was raised: raised {Pv.Camp.StoneRaisedAt.Value}, drawn {drawn}");
            }
            Assert.IsEmpty(fails, string.Join(" | ", fails));
        }

        /// <summary>The #722 tester's run: in one lived-in camp (a pond, natural uses on) every event's cheat in turn, each scene to its end,
        /// the first snow last. Each must start (or its cheat say why not, for a gate the box lacks), and the first snow must still cast a kid
        /// (the tester: after the others it never did).</summary>
        [UnityTest, Timeout(1800000)]
        public IEnumerator LivedCamp_AllInTurn_SnowLast()
        {
            yield return LivedCamp(10f, pond: true);
            CampLife.OnlyCheats = false;
            yield return new WaitForSecondsRealtime(10f);
            var sim = Sim;
            Debug.Log("[722 turn] " + CampCheats.OnlyThing(ConsequenceKind.NameStone));
            var stone = sim.FirstNameStone; if (stone != null) stone.dueMonth = sim.State.month - 1;
            Debug.Log("[722 turn] " + ConsequenceCheats.OneMoreDeath());
            yield return UntilThing(ConsequenceKind.NameStone);
            foreach (string id in StoryEvent.All)
            {
                if (id == StoryEvent.FirstSnow || id == StoryEvent.SingerSong) continue;
                int started = StoryMoment.Started;
                Debug.Log($"[722 turn] {id}: {StoryMoment.DebugPlayNow(id)}");
                yield return Until(() => StoryMoment.Started > started, 20f);
                if (StoryMoment.Started == started) { Debug.Log($"[722 turn] {id} did not start: {StoryMoment.LastWhy}"); continue; }
                yield return Until(() => sim.PlayingMoments().Count == 0, MaxScene);
                yield return new WaitForSecondsRealtime(2f);
            }
            int before = StoryMoment.Started;
            Debug.Log("[722 turn] snow: " + StoryMoment.DebugPlayNow(StoryEvent.FirstSnow));
            yield return Until(() => StoryMoment.Started > before, 20f);
            Assert.Greater(StoryMoment.Started, before, "the first snow after the others: " + StoryMoment.LastWhy);
        }

        /// <summary>B80.a: the Keeper's child handed their work's tool at the fire. After: the child with the tool.</summary>
        [UnityTest, Timeout(600000)]
        public IEnumerator B80a_KeeperChildGrown()
        {
            int child = -1;
            yield return Story("b80a-keeper-child-grown", StoryEvent.KeeperChildGrown, CampUse.KeeperChildGrown, 10f, null,
                e => { child = e.notableId; return Named(e); }, () => Person(child) ?? Fire());
        }

        /// <summary>B80.b: the antlers carried from the old door to the new chief's. After: the antlers at the new door.</summary>
        [UnityTest, Timeout(600000)]
        public IEnumerator B80b_AntlersPassed() => Story("b80b-antlers", StoryEvent.AntlersPassed, CampUse.AntlersPass, 10f, null,
            e => new[] { e.otherNotableId }, () => Door(Sim.StoryMarks.antlersAtId));

        /// <summary>B80.c at the pond: the spout pours to the left. After: the tinkerer with the crooked bucket.</summary>
        [UnityTest, Timeout(600000)]
        public IEnumerator B80c_TinkererBucket()
        {
            int who = -1;
            yield return Story("b80c-tinkerer", StoryEvent.TinkererBucket, CampUse.TinkererBucket, 10f, null,
                e => { who = e.notableId; return Named(e); }, () => Person(who), pond: true);
        }

        /// <summary>B80.e: the ring of flowers at the name stone. After: the flowers.</summary>
        [UnityTest, Timeout(600000)]
        public IEnumerator B80e_GardenerFlowers() => Story("b80e-gardener", StoryEvent.GardenerFlowers, CampUse.GardenerFlowers, 10f,
            () => Debug.Log("[722 shots] " + CampCheats.OnlyThing(ConsequenceKind.NameStone)), Named, () => PropSpot(Sim.StoryMarks.flowersStoneId), thing: ConsequenceKind.NameStone);

        /// <summary>B80.f: the joker's grin on the woodpile, two kids peek. After: the face log.</summary>
        [UnityTest, Timeout(600000)]
        public IEnumerator B80f_JokerFace() => Story("b80f-joker", StoryEvent.JokerFace, CampUse.JokerFace, 10f,
            () => Debug.Log("[722 shots] " + CampCheats.OnlyThing(ConsequenceKind.WoodpileRoof)), Named, () => PropSpot(Sim.StoryMarks.faceLogPropId), thing: ConsequenceKind.WoodpileRoof);

        /// <summary>B80.g in a dry spell: the bucket from the pond to the Keeper's tree, the others look up for rain. After: the tree.</summary>
        [UnityTest, Timeout(600000)]
        public IEnumerator B80g_DrySpell() => Story("b80g-dry-spell", StoryEvent.DrySpell, CampUse.DrySpell, 10f, null, Named, TreeSpot, pond: true);

        /// <summary>B80.h in winter, snow on the ground and falling: tongues out, then the snow figure. After: the snow figure by the fire.</summary>
        [UnityTest, Timeout(600000)]
        public IEnumerator B80h_FirstSnow() => Story("b80h-first-snow", StoryEvent.FirstSnow, CampUse.FirstSnow, 10f, null, Named,
            () => Fire() is Vector2 f ? StoryScenes.FigureSpot(f) : (Vector2?)null);

        // ================================================================== the life uses the events leave

        /// <summary>A life use: a lived-in camp, the event it follows played through by its cheat (unfilmed), the view at
        /// <paramref name="hour"/>, then the use's cheat, filmed to its end, and an after still on its thing.</summary>
        IEnumerator Life(string ev, CampUse use, float hour, string after, Action setup, Func<string> cheat, Func<Vector2?> thing, bool pond = false,
            Func<int> named = null, string prop = null)
        {
            Fresh(ev);
            yield return LivedCamp(hour, pond);
            setup?.Invoke();
            if (prop != null) yield return UntilThing(prop); // the stone or the woodpile drawn before its event's scene is cast at it
            yield return new WaitForSecondsRealtime(1f);
            var pv = Pv;
            if (after != null)
            {
                string r0 = StoryMoment.DebugPlayNow(after);
                var u0 = CampLife.UseOfStory(after);
                Debug.Log($"[722 shots] {ev}: first {r0}");
                yield return Until(() => pv.Camp.Playing(u0), 15f);
                Assert.IsTrue(pv.Camp.Playing(u0), $"{ev}: {after} plays first ({r0}; {StoryMoment.LastWhy})");
                yield return Until(() => !pv.Camp.Playing(u0), MaxScene);
                yield return new WaitForSecondsRealtime(1f);
            }
            PeopleView.DebugSetHour(hour);
            yield return new WaitForSecondsRealtime(1f);
            string r = cheat();
            Debug.Log($"[722 shots] {ev}: {r}");
            yield return Until(() => pv.Camp.Playing(use), 3f);
            Assert.IsTrue(pv.Camp.Playing(use), $"{ev}: the use plays ({r})");
            var sc = SceneOf(use);
            Debug.Log($"[722 shots] {ev}: cast {string.Join(",", sc.keys)}");
            Assert.Greater(sc.keys.Count, 0, ev + ": someone plays it");
            if (named != null && named() is int n && n >= 0 && pv.Camp.CanCastNow(n)) Assert.Contains(n, sc.keys, $"{ev}: {Sim.FindNotable(n)?.name}, who keeps it, plays it");
            var frames = new List<int>();
            yield return Film(ev, () => pv.Camp.Playing(use), CastOf(sc), () => pv.Camp.FocusNow(), MaxScene, frames);
            Assert.Greater(frames[0], 2, ev + ": frames of the use");
            yield return new WaitForSecondsRealtime(0.5f);
            yield return After(ev, thing());
            Debug.Log($"[722 shots] {ev}: fruit {pv.Camp.FruitDrawn}, flowers {pv.Camp.FlowersDrawn}, figure {pv.Camp.FigureDrawn}; {pv.Camp.PropMarks()}");
            CloseSlice();
        }

        /// <summary>The fruit's life use: a summer day after the first fruit, a kid picks one from the tree.</summary>
        [UnityTest, Timeout(600000)]
        public IEnumerator FruitPick() => Fast(0.15f, () => Life("fruit-pick", CampUse.FruitPick, 11f, null,
            () => Debug.Log("[722 shots] " + StoryMoment.ReadyForCheat(Sim, Pv, Season.Summer, false, 11f)), CampCheats.FruitPick, TreeSpot));

        /// <summary>B80.e's morning water on the gardener's flowers.</summary>
        [UnityTest, Timeout(600000)]
        public IEnumerator B80e_FlowerWater() => Life("b80e-flower-water", CampUse.FlowerWater, 8f, StoryEvent.GardenerFlowers,
            () => Debug.Log("[722 shots] " + CampCheats.OnlyThing(ConsequenceKind.NameStone)), CampCheats.FlowerWater, () => PropSpot(Sim.StoryMarks.flowersStoneId), prop: ConsequenceKind.NameStone);

        /// <summary>B80.f's life use: a kid pats the face log as they pass.</summary>
        [UnityTest, Timeout(600000)]
        public IEnumerator B80f_FacePat() => Life("b80f-face-pat", CampUse.FacePat, 11f, StoryEvent.JokerFace,
            () => Debug.Log("[722 shots] " + CampCheats.OnlyThing(ConsequenceKind.WoodpileRoof)), CampCheats.FacePat, () => PropSpot(Sim.StoryMarks.faceLogPropId), prop: ConsequenceKind.WoodpileRoof);

        /// <summary>B80.g's evening repeat in a later dry spell: the waterer's bucket from the pond to the Keeper's tree (the pond is the spec's: the
        /// bucket is dipped at its edge, so the box has one).</summary>
        [UnityTest, Timeout(600000)]
        public IEnumerator B80g_TreeWater() => Life("b80g-tree-water", CampUse.TreeWater, 16.5f, StoryEvent.DrySpell,
            () => Debug.Log("[722 shots] " + EcoDebug.DrySpell()), CampCheats.TreeWater, TreeSpot, pond: true, named: () => Sim.TreeWaterer?.id ?? -1);

        // ================================================================== batch 2: the asks, the song, the Keeper's child's wedding

        /// <summary>A story ask's cheat: raised now and staged by the choice scene, filmed for 6 s on its two candidates, answered (option 2), and a
        /// still after.</summary>
        IEnumerator Ask(string ev, string kind, Action<SimCore> check)
        {
            Fresh(ev);
            yield return LivedCamp(10f);
            string r = StoryMoment.DebugAskNow(kind);
            Debug.Log($"[722 shots] {ev}: {r}");
            Assert.IsTrue(r.StartsWith("raised"), r);
            var sim = Sim;
            var d = sim.OpenDilemma;
            Assert.IsNotNull(d, "the ask is open");
            var frames = new List<int>();
            float t0 = Time.time;
            yield return Film(ev, () => Time.time - t0 < 6f, () => new[] { d.PersonOf(0), d.PersonOf(1) }, Fire, 6f, frames);
            Assert.IsTrue(sim.AnswerDilemma(d.id, 1, DilemmaPath.Card), "answered");
            GameClock.DilemmaAnswered(d);
            check?.Invoke(sim);
            yield return new WaitForSecondsRealtime(3f);
            yield return After(ev, Middle(d.PersonOf(1)) ?? Fire());
            CloseSlice();
        }

        [UnityTest, Timeout(600000)]
        public IEnumerator Ask_OfferingCarrier() => Ask("ask-offering", StoryChoices.Offering, s =>
            Assert.AreEqual(s.StoryMarks.offeringCarrierId, s.RitualLead(RitualKind.Offering)?.id, "the pick leads the offering"));

        [UnityTest, Timeout(600000)]
        public IEnumerator Ask_FirstLog() => Ask("ask-first-log", StoryChoices.FirstLog, s =>
            Assert.AreEqual(s.StoryMarks.firstLogId, s.RitualLead(RitualKind.Woodpile)?.id, "the pick leads the woodpile"));

        [UnityTest, Timeout(600000)]
        public IEnumerator Ask_PupHolder() => Ask("ask-pup", StoryChoices.PupHolder, s =>
            Assert.AreEqual(s.StoryMarks.pupKidB, s.FindDog(s.StoryMarks.pupDogId)?.keeperId, "the pup's keeper is the pick"));

        /// <summary>4.3 with 2.2 first: the song's ask raised (its ritual, the song's first year, plays as it is made) and answered; then its
        /// ritual's cheat (what plays is ended) plays it again with the name on the caption ("The harvest feast: The Long Table"), filmed.</summary>
        [UnityTest, Timeout(600000)]
        public IEnumerator Ask_SongName_ThenTheRitualsCaption()
        {
            const string ev = "ask-song-name";
            Fresh(ev);
            yield return LivedCamp(10f);
            Debug.Log("[722 shots] song: " + StoryMoment.DebugAskNow(StoryChoices.SongName));
            var sim = Sim;
            var d = sim.OpenDilemma;
            Assert.IsNotNull(d, "the song's ask is open");
            yield return new WaitForSecondsRealtime(2f);
            Assert.IsTrue(sim.AnswerDilemma(d.id, 0, DilemmaPath.Card));
            GameClock.DilemmaAnswered(d);
            string ritual = sim.StoryMarks.songRitual, name = sim.StoryMarks.songName;
            Assert.IsNotEmpty(name, "the song has its name");
            yield return new WaitForSecondsRealtime(1f);
            string r = StagedMoments.DebugPlayRitualNow(ritual);
            Debug.Log("[722 shots] song caption: " + r + " | " + StagedMoments.LastCaption);
            Assert.IsFalse(r.StartsWith("nothing started"), r);
            Assert.AreEqual($"{RitualKind.Caption(ritual)}: {name}", StagedMoments.LastCaption, "the song's name on the ritual's caption");
            var pv = Pv; var frames = new List<int>();
            var rs = StagedMoments.LastScene;
            yield return Film(ev, () => StagedMoments.Playing > 0, CastOf(rs), Fire, MaxScene, frames);
            CloseSlice();
        }

        /// <summary>2.2: the singer's song is made as a ritual plays: its line on the caption, filmed for 10 s.</summary>
        [UnityTest, Timeout(600000)]
        public IEnumerator SingerSong()
        {
            const string ev = "singer-song";
            Fresh(ev);
            yield return LivedCamp(10f);
            string r = StoryMoment.DebugSingerSong();
            Debug.Log("[722 shots] singer-song: " + r);
            Assert.IsTrue(Sim.StoryHappened(StoryEvent.SingerSong), r);
            var pv = Pv; var frames = new List<int>(); float t0 = Time.time;
            var rs = StagedMoments.LastScene;
            yield return Film(ev, () => StagedMoments.Playing > 0 && Time.time - t0 < 10f, CastOf(rs, Sim.StoryMarks.songOwnerId), Fire, 10f, frames);
            CloseSlice();
        }

        /// <summary>B80.d: the singer's song rides the ritual's moment: its line is the caption, the singer's notes first; the whole ritual filmed.</summary>
        [UnityTest, Timeout(600000)]
        public IEnumerator B80d_SingerSong_OnTheRitualsCaption()
        {
            const string ev = "b80d-song";
            Fresh(ev);
            yield return LivedCamp(10f);
            string r = StoryMoment.DebugSingerSong();
            Debug.Log("[722 shots] b80d-song: " + r + " | caption: " + StagedMoments.LastCaption);
            var song = Sim.State.chronicle.LastOrDefault(x => x.kind == StoryEvent.Kind(StoryEvent.SingerSong));
            Assert.IsNotNull(song, r);
            Assert.AreEqual(song.text, StagedMoments.LastCaption, "the song's line is the ritual's caption");
            Assert.Greater(StagedMoments.Playing, 0, "the ritual plays");
            var pv = Pv; var frames = new List<int>();
            var rs = StagedMoments.LastScene;
            yield return Film(ev, () => StagedMoments.Playing > 0, CastOf(rs), Fire, MaxScene, frames);
            Assert.Greater(frames[0], 4, "frames of the ritual");
            CloseSlice();
        }

        /// <summary>5.2: the Keeper's child's wedding: the wedding moment plays now with its line on the caption; filmed on the couple.</summary>
        [UnityTest, Timeout(600000)]
        public IEnumerator KeeperChildWed()
        {
            const string ev = "keeper-child-wed";
            Fresh(ev);
            yield return LivedCamp(10f);
            string r = StoryMoment.DebugKeeperChildWed();
            Debug.Log("[722 shots] keeper-child-wed: " + r + " | caption: " + StagedMoments.LastCaption);
            Assert.IsTrue(r.StartsWith("playing"), r);
            var e = Sim.State.chronicle.LastOrDefault(x => x.kind == "wedding");
            Assert.IsNotNull(e);
            StringAssert.Contains("named by the Keeper, married", e.text);
            Assert.AreEqual(e.text, StagedMoments.LastCaption, "the line on the wedding's caption");
            var sc = StagedMoments.LastScene;
            Assert.IsNotNull(sc, "the wedding is staged");
            Assert.AreEqual(MomentKind.Wedding, sc.kind);
            Assert.IsNotNull(sc.plan.Member(e.notableId), "the Keeper's child is in the wedding");
            Assert.IsNotNull(sc.plan.Member(e.otherNotableId), "and the one they marry");
            var frames = new List<int>();
            yield return Film(ev, () => StagedMoments.Playing > 0, CastOf(sc, e.notableId, e.otherNotableId), Fire, MaxScene, frames);
            Assert.Greater(frames[0], 4, "frames of the wedding");
            CloseSlice();
        }
    }
}
