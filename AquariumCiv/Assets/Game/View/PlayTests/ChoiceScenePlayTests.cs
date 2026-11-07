using System.Collections;
using System.Linq;
using AquariumCiv.Game;
using AquariumCiv.GameUI;
using AquariumCiv.Life;
using AquariumCiv.Sim;
using AquariumCiv.View.Cards;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace AquariumCiv.View.PlayTests
{
    /// <summary>#224: the choice scene in the box by touch, on the real Slice at portrait phone sizes. Show me on the dilemma card stages
    /// the people's ask in the world; a touch on the left one answers on the Box path; leaving without a touch keeps it open and the
    /// people drift back; nothing is left behind. Screenshots of each kind at 1170x2532 and 750x1334: <c>224-*</c>.</summary>
    [Category("ChoiceScene")]
    public class ChoiceScenePlayTests : PlayTestBase
    {
        const int Seed = 7531;
        static SimCore Sim => GameClock.Instance.ActiveSim;
        static CardStackView Cards => CardStackView.Instance;
        static CardStackModel Model => Cards.Model;
        static ChoiceSceneView Choice => ChoiceSceneView.Instance;

        static bool Shown(VisualElement e)
        {
            for (; e != null; e = e.parent) if (e.resolvedStyle.display == DisplayStyle.None) return false;
            return true;
        }

        IEnumerator Tap(string name)
        {
            var e = Cards.Root.Q(name);
            Assert.IsNotNull(e, name);
            Assert.IsTrue(Shown(e), name + " is on screen");
            yield return TapAt(GameUiPanel.ScreenCenter(e));
            yield return Frames(4);
        }

        IEnumerator OpenBox(int w, int h)
        {
            ChoiceSceneView.DebugWatch = false;
            GameUiPanel.AcceptInputUnfocused(true);
            yield return OpenSlice(w, h, seed: Seed);
            GameUiPanel.AcceptInputUnfocused(true);
            SimLayoutSource.DebugSkipYears(8f);
            yield return null;
            Sim.CleanAll();
            Assert.IsNotNull(Choice, "the choice scene is bootstrapped with the box");
        }

        /// <summary>This dilemma raised (a year more at a time until it holds), the stack open on its card.</summary>
        IEnumerator ToTheCard(string def)
        {
            if (Model.Mode == CardStackMode.Open) { Model.Close(); yield return Frames(10); }
            Dilemma d = null;
            for (int i = 0; i < 4 && d == null; i++)
            {
                d = Sim.DebugRaiseDilemma(def);
                if (d == null) { SimLayoutSource.DebugSkipYears(1f); yield return null; }
            }
            Assert.IsNotNull(d, def + " can be raised in this box");
            GameClock.DebugAway(24);
            yield return Frames(6);
            Assert.AreEqual(CardStackMode.Peek, Model.Mode, def + ": the peek");
            yield return Tap("cards-peek");
            for (int guard = 0; guard < 10 && Model.Face.kind != CardKind.Dilemma; guard++) yield return Tap("cards-next");
            Assert.AreEqual(CardKind.Dilemma, Model.Face.kind);
            Assert.AreEqual(d.id, Sim.OpenDilemma?.id, def + " is the open one");
            yield return Frames(6);
        }

        /// <summary>Show me on the card: the scene is staged and the people have walked to their spots, the camera landed.</summary>
        IEnumerator ShowTheChoice(string when)
        {
            yield return Tap("cards-showme");
            Assert.AreEqual(ChoiceSceneView.Phase.Waiting, Choice.State, when + ": Show me stages the choice scene");
            Assert.IsTrue(Cards.ShowingChoice, when + ": the sheet slides off, the bar keeps the ask");
            var pv = PeopleView.Find();
            yield return Until(() => Choice.Stage == null || Choice.Stage.Pins().All(p => pv.TryGetDollPosition(p.who, out var at) && (at - new Vector2(p.x, p.z)).magnitude < 0.06f), 45f);
            yield return new WaitForSecondsRealtime(1.2f); // the halos fade in, the camera lands
            Assert.IsNotNull(Choice.Stage, when);
            foreach (var p in Choice.Stage.Pins())
            {
                Assert.IsTrue(pv.TryGetDollPosition(p.who, out var at), when + ": " + p.who + " is out");
                var act = pv.Activities.FirstOrDefault(a => a.who == p.who);
                Assert.Less((at - new Vector2(p.x, p.z)).magnitude, 0.06f, when + ": " + p.who + " stands on their spot"
                    + $" (doing {act.kind}/{act.source}, moving {act.moving}, staged moments playing {StagedMoments.Playing}, at {at}, pin ({p.x:0.00}, {p.z:0.00}))");
            }
            Assert.Greater(Choice.HaloAlpha(0), 0.5f, when + ": a halo on the first");
            Assert.Greater(Choice.HaloAlpha(1), 0.5f, when + ": a halo on the second");
        }

        /// <summary>Both choices on screen, clearly apart (more than two 44 pt targets between them) and above the bar.</summary>
        void AssertApart(string when, out int left)
        {
            Assert.IsTrue(Choice.TryScreenOf(0, out var a), when + ": option 0 on screen");
            Assert.IsTrue(Choice.TryScreenOf(1, out var b), when + ": option 1 on screen");
            float pt = Screen.width / 375f; // a 375 pt wide phone at this width
            Assert.Greater((a - b).magnitude, 88f * pt, when + $": the two choices are clearly apart ({a} vs {b})");
            float bar = Cards.Root.Q("cards-showme-bar").worldBound.yMin / Cards.Root.worldBound.height * Screen.height;
            foreach (var s in new[] { a, b }) Assert.Greater(Screen.height - s.y, 44f * pt, when + ": not under the screen's edge");
            foreach (var s in new[] { a, b }) Assert.Less(Screen.height - s.y, bar, when + ": above the bar");
            Assert.AreEqual(0, Choice.SideAt(a, out _), when + ": a touch on option 0 takes option 0");
            Assert.AreEqual(1, Choice.SideAt(b, out _), when + ": a touch on option 1 takes option 1");
            left = a.x <= b.x ? 0 : 1;
        }

        // Others' lazy one-offs (the speaker ring, the chatter marks) appear on first use: not ours, not a leak (as in AssertNoNewRoots).
        // HitFx: a scene root since batch 1; its puff renderer is made on the first hit. RitualBubbles (#370): the staged moments'
        // glyphs, one mesh made on the first bubble and kept (off) after, exactly like ChatterMarks.
        static readonly string[] LazyRoots = { "SpeakerRing", "ChatterMarks", "HitFx", "RitualBubbles" };
        /// <summary>Not ours and not a leak: the doll's own, the lazy one-offs, and the answer's consequence prop (#371: the sim places
        /// it when the Keeper answers, it is meant to stay in the box).</summary>
        static bool Counted(MeshRenderer r) => r != null && r.gameObject.scene.IsValid() && r.GetComponentInParent<PegDollView>() == null
                        && System.Array.IndexOf(LazyRoots, r.transform.root.name) < 0 && !r.name.StartsWith("Consequence_");
        static int Renderers() => Resources.FindObjectsOfTypeAll<MeshRenderer>().Count(Counted);
        static System.Collections.Generic.List<string> RendererNames() => Resources.FindObjectsOfTypeAll<MeshRenderer>()
            .Where(Counted)
            .Select(r => r.transform.root.name + "/" + r.name + (r.gameObject.activeInHierarchy ? "" : " (off)")).ToList();
        static string AddedRenderers(System.Collections.Generic.List<string> before)
        {
            var added = RendererNames();
            foreach (var n in before) added.Remove(n);
            return string.Join(", ", added);
        }
        static int Roots() => RootNames().Count;
        static System.Collections.Generic.List<string> RootNames()
        {
            var list = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects().Select(g => g.name).ToList();
            list.AddRange(LeakCheck.Orphans().Select(n => "(orphan) " + n));
            list.Sort();
            return list;
        }

        /// <summary>The scene sanity check: the roots the scene gained over the cycle (others' lazy one-offs are logged, never ours).</summary>
        static void AssertNoNewRoots(System.Collections.Generic.List<string> before, string when)
        {
            var now = RootNames();
            var added = now.ToList();
            foreach (var n in before) added.Remove(n);
            Debug.Log($"[ChoiceScene] {when}: roots {before.Count} -> {now.Count}" + (added.Count > 0 ? " (new: " + string.Join(", ", added) + ")" : ""));
            added.RemoveAll(n => System.Array.IndexOf(LazyRoots, n) >= 0); // others' lazy one-offs: logged, not counted
            Assert.IsFalse(added.Any(n => n.Contains("Choice")), when + ": a root of the choice scene was left: " + string.Join(", ", added));
            Assert.LessOrEqual(added.Count, 1, when + ": new roots " + string.Join(", ", added));
        }
        static int ChoiceObjects() => Resources.FindObjectsOfTypeAll<Transform>().Count(t => t != null && t.name.StartsWith("Choice") && t.name != "ChoiceTags" && t.gameObject.scene.IsValid());

        [UnityTest, Timeout(300000)]
        public IEnumerator ShowMeThenATouchOnTheLeftPersonAnswersInTheBox()
        {
            yield return OpenBox(1170, 2532);
            yield return ToTheCard("camp_fire");
            var roots = RootNames(); int renderers = Renderers(); var rendererNames = RendererNames();
            var d = Sim.OpenDilemma;
            yield return ShowTheChoice("who");
            AssertApart("who", out int left);
            var leftWho = Choice.Stage.sides[left].people[0].who;
            Assert.IsTrue(Choice.TryScreenOf(left, out var at));
            Assert.IsTrue(Choice.Answerable);
            yield return TapAt(at);
            yield return Frames(3);
            Assert.AreEqual(DilemmaState.Answered, d.state, "a touch on the left person answers");
            Assert.AreEqual(DilemmaPath.Box, d.path, "on the Box path");
            Assert.AreEqual(left, d.option, "the left one");
            Assert.AreEqual(leftWho.notableId, d.PersonOf(d.option));
            Assert.AreEqual(ChoiceSceneView.Phase.Reacting, Choice.State);
            yield return new WaitForSecondsRealtime(ChoiceScenes.ReactSeconds * 0.5f);
            var pv = PeopleView.Find();
            var act = pv.Activities.FirstOrDefault(a => a.who == leftWho);
            Assert.AreEqual(ActivityKind.Cheer, act.kind, "the chosen one cheers");
            yield return Shot("224-who-reaction-1170x2532");
            yield return Until(() => Choice.State == ChoiceSceneView.Phase.Idle, ChoiceScenes.ReactSeconds + 4f);
            Assert.AreEqual(ChoiceSceneView.Phase.Idle, Choice.State, "the reaction ends");
            yield return Frames(20);
            Assert.IsFalse(Cards.ShowingChoice, "the card comes back");
            Assert.AreEqual(left, Model.Face.dilemmaChosen, "answered on the card too");
            // The scene sanity check: nothing left behind after stage, answer and disperse.
            Assert.AreEqual(0, ChoiceObjects(), "no prop, halo or traveller left");
            Assert.AreEqual(0, pv.PinCount, "nobody pinned");
            AssertNoNewRoots(roots, "who");
            Assert.LessOrEqual(Renderers(), renderers, "no leaked renderers: " + AddedRenderers(rendererNames));
            Debug.Log($"[ChoiceScene] sanity: roots {roots.Count} -> {Roots()}, renderers {renderers} -> {Renderers()}, choice objects {ChoiceObjects()}");
            AssertNoErrors("who by touch");
        }

        [UnityTest, Timeout(300000)]
        public IEnumerator LeavingWithoutATouchKeepsItOpenAndThePeopleDriftBack()
        {
            yield return OpenBox(1170, 2532);
            yield return ToTheCard("camp_remember");
            var roots = RootNames(); int renderers = Renderers(); var rendererNames = RendererNames();
            var d = Sim.OpenDilemma;
            yield return ShowTheChoice("tradition");
            yield return Tap("cards-showme-back");
            yield return Frames(4);
            Assert.AreEqual(ChoiceSceneView.Phase.Drifting, Choice.State, "left without a touch");
            Assert.IsFalse(Choice.Answerable, "a touch no longer answers");
            Assert.AreEqual(DilemmaState.Open, d.state, "it stays open");
            // The scene's clock caps a frame at 0.1 s, so on a slow Editor its 20 s take longer in real time: allow for it.
            yield return Until(() => Choice.State == ChoiceSceneView.Phase.Idle, ChoiceScenes.DriftSeconds * 3f + 5f);
            Assert.AreEqual(ChoiceSceneView.Phase.Idle, Choice.State, "after ~20 s they drift back to work");
            yield return Frames(2); // End() destroys at the end of its frame: look after it
            Assert.AreEqual(DilemmaState.Open, d.state, "still open");
            Assert.AreEqual(0, ChoiceObjects(), "nothing left behind");
            Assert.AreEqual(0, PeopleView.Find().PinCount);
            AssertNoNewRoots(roots, "leave");
            Assert.LessOrEqual(Renderers(), renderers, "no leaked renderers: " + AddedRenderers(rendererNames));
            // The next Show me stages it again.
            yield return ShowTheChoice("tradition again");
            Assert.AreEqual(d.id, Choice.Stage.dilemmaId);
            AssertNoErrors("leave");
        }

        static readonly (string def, string name)[] Kinds =
        {
            ("camp_fire", "who"), ("camp_winter", "whichway"), ("camp_remember", "tradition"), ("camp_travellers", "strangers"),
        };

        IEnumerator EveryKindAt(int w, int h)
        {
            yield return OpenBox(w, h);
            foreach (var (def, name) in Kinds)
            {
                yield return ToTheCard(def);
                yield return ShowTheChoice(name);
                AssertApart($"{name} {w}x{h}", out _);
                yield return Shot($"224-{name}-{w}x{h}");
                yield return Tap("cards-showme-back");
                yield return Frames(4);
                Sim.DebugLapseDilemma(); // the people decide; the scene ends with it
                yield return Until(() => Choice.State == ChoiceSceneView.Phase.Idle, 3f);
                Assert.AreEqual(ChoiceSceneView.Phase.Idle, Choice.State, name + ": a closed dilemma ends its scene");
            }
            AssertNoErrors($"every kind {w}x{h}");
        }

        [UnityTest, Timeout(600000)]
        public IEnumerator EveryKindOnATallPhone() => EveryKindAt(1170, 2532);

        [UnityTest, Timeout(600000)]
        public IEnumerator EveryKindOnASmallPhone() => EveryKindAt(750, 1334);

        // ================================================================== #241

        /// <summary>A target's drawn height in points on this screen (a 375 pt wide phone).</summary>
        static float Pt(VisualElement e) => e.worldBound.height / Cards.Root.worldBound.height * Screen.height * 375f / Screen.width;

        static bool AnyWorldCard => PersonCardView.Instance != null && (PersonCardView.Instance.AnyOpen || PersonCardView.Instance.BuildingOpen);

        [UnityTest, Timeout(300000)]
        public IEnumerator ATapBetweenTheSidesOpensNothingAndDoesNotAnswer()
        {
            yield return OpenBox(750, 1334);
            yield return ToTheCard("camp_fire");
            foreach (var n in new[] { "cards-dil-answer-0", "cards-dil-answer-1" })
                Assert.GreaterOrEqual(Pt(Cards.Root.Q(n)), 44f, n + " is 44 pt or more at 750x1334");
            var d = Sim.OpenDilemma;
            yield return ShowTheChoice("near miss");
            Assert.GreaterOrEqual(Pt(Cards.Root.Q("cards-showme-back")), 44f, "Back to the story is 44 pt or more at 750x1334");
            AssertApart("near miss", out _);
            Assert.IsTrue(Choice.TryScreenOf(0, out var a));
            Assert.IsTrue(Choice.TryScreenOf(1, out var b));
            int tapped = 0;
            foreach (float t in new[] { 0.5f, 0.35f, 0.65f })
            {
                var p = Vector2.Lerp(a, b, t);
                if (Choice.SideAt(p, out _) >= 0) continue; // on a side's reach: that is a real touch, not a miss
                yield return TapAt(p);
                yield return Frames(8);
                tapped++;
                Assert.AreEqual(DilemmaState.Open, d.state, $"a tap at {p} between the sides does not answer");
                Assert.IsFalse(AnyWorldCard, $"a tap at {p} between the sides opens no person or building card");
                Assert.AreEqual(ChoiceSceneView.Phase.Waiting, Choice.State, "the people still wait");
                Assert.IsTrue(Cards.ShowingChoice, "the bar is still up");
            }
            Assert.Greater(tapped, 0, "at least one point between the sides is a miss");
            // A building in the frame, tapped while they wait: still nothing opens (it opened the building card before #241).
            var box = UnityEngine.Object.FindAnyObjectByType<BoxView>();
            int onBuilding = 0;
            foreach (var lot in Sim.Settlement.Lots)
            {
                if (lot.retired || lot.buildingId <= 0 || onBuilding >= 2) continue;
                var w = box.transform.TransformPoint(new Vector3(lot.x, box.GroundHeight(lot.x, lot.z) + 0.05f, lot.z));
                var s = Camera.main.WorldToScreenPoint(w);
                var p = new Vector2(s.x, s.y);
                if (s.z <= 0f || p.x < 0f || p.x > Screen.width || p.y < Screen.height * 0.3f || p.y > Screen.height) continue;
                if (Choice.SideAt(p, out _) >= 0 || PersonCardView.Instance.PickBuilding(p) < 0) continue;
                yield return TapAt(p);
                yield return Frames(8);
                onBuilding++;
                Assert.IsFalse(AnyWorldCard, "a tap on a building while they wait opens no card");
                Assert.AreEqual(DilemmaState.Open, d.state);
            }
            Debug.Log("[241] building taps while waiting: " + onBuilding);
            yield return Shot("241-near-miss-750x1334");
            // A real touch still answers.
            Assert.IsTrue(Choice.TryScreenOf(0, out var at));
            string before = $"state {Choice.State}, side {Choice.SideAt(at, out var sd)} ({sd:0}), at {at}, screen {Screen.width}x{Screen.height}"; // batch 5: why a side tap missed
            yield return TapAt(at);
            yield return Frames(3);
            Assert.AreEqual(DilemmaState.Answered, d.state, "a touch on a side still answers: " + before + $" -> {Choice.State}");
            AssertNoErrors("near miss");
        }

        [UnityTest, Timeout(300000)]
        public IEnumerator AnOpenBuildingCardHoldsTheDilemmaCardBack()
        {
            yield return OpenBox(750, 1334);
            yield return ToTheCard("camp_fire");
            var sheet = Cards.Root.Q<GlassSheet>("cards-sheet");
            Assert.IsTrue(sheet.IsOpen, "the dilemma card is up");
            var person = PersonCardView.Instance;
            int building = Sim.State.buildings.Select(x => x.id).FirstOrDefault(id => Sim.BuildingCardOf(id) != null);
            Assert.IsTrue(person.OpenBuilding(building), "a building card opens");
            yield return Frames(10);
            Assert.IsFalse(sheet.IsOpen, "the dilemma card waits behind the building card");
            yield return Shot("241-building-over-750x1334");
            person.CloseBuilding();
            yield return Frames(10);
            Assert.IsTrue(sheet.IsOpen, "back once the building card closes");
            Assert.AreEqual(CardKind.Dilemma, Model.Face.kind);
            AssertNoErrors("building over");
        }

        /// <summary>t1-moments (visit-loop 2.4, the fail item of tester run 1): a choice held for the next open is staged in the box
        /// first, by itself, with no Show me; the card peek is still there.</summary>
        [UnityTest, Timeout(600000)]
        public IEnumerator AHeldChoiceIsStagedInTheBoxOnTheOpenWithoutShowMe()
        {
            yield return OpenBox(1170, 2532);
            DilemmaCheats.Toggle(true); // batch 5: dilemmas are off in the Editor (tooling 2026-11-06), and then BeginVisit never holds the choice for the open
            Dilemma d = null;
            for (int i = 0; i < 4 && d == null; i++)
            {
                d = Sim.DebugRaiseDilemma("camp_fire");
                if (d == null) { SimLayoutSource.DebugSkipYears(1f); yield return null; }
            }
            Assert.IsNotNull(d);
            GameClock.DebugAway(24); // away a day and back: an open with the choice waiting
            yield return Until(() => Choice.State == ChoiceSceneView.Phase.Waiting, 90f);
            Assert.AreEqual(ChoiceSceneView.Phase.Waiting, Choice.State, $"staged in the box on the open, no Show me tapped (batch 5: why {HeldMomentStage.Instance?.LastWhy}, due {Sim.ChoiceStageDue}, moment first {Sim.MomentBeforeChoice()}, busy {HeldMomentStage.BusyReason()}, open {Sim.OpenDilemma?.id})");
            Assert.IsFalse(Sim.ChoiceStageDue, "once per open");
            Assert.IsNotNull(Choice.Stage);
            Assert.AreEqual(d.id, Sim.OpenDilemma?.id, "still the same open choice, with its card");
            Assert.IsTrue(Choice.InBox, "staged in the box by itself");
            yield return Shot("t1-held-choice-staged-on-open");

            // t3-choice (tester run 2 A): it holds while the player looks at the box; it went Drifting after 1.5 s before.
            yield return new WaitForSecondsRealtime(10f);
            Assert.AreEqual(ChoiceSceneView.Phase.Waiting, Choice.State, "10 s later the people still wait: " + ChoiceSceneView.DebugStatus());
            Assert.IsTrue(Choice.Answerable, "a touch answers");
            var pv = PeopleView.Find();
            foreach (var p in Choice.Stage.Pins())
                Assert.IsTrue(pv.TryGetDollPosition(p.who, out var at) && (at - new Vector2(p.x, p.z)).magnitude < 0.08f, p.who + " is still at the spot");
            Assert.IsTrue(Choice.BoxBarUp, "the bar invites the touch without opening the card");
            var bar = Choice.UiRoot.Q("choice-bar");
            Assert.IsTrue(Shown(bar), "choice-bar on screen");
            Assert.AreEqual(CardStackView.ChoiceNote, bar.Q<Label>("choice-bar-text").text);
            var let = Choice.UiRoot.Q("choice-let");
            Assert.IsTrue(Shown(let) && let.pickingMode == PickingMode.Position, "Let them decide on the bar");
            Assert.GreaterOrEqual(let.worldBound.height, 121f - 1f, "44 pt tall (tester run 2 E)");
            // t3-choice F: the tags keep the gutter from the screen's sides.
            for (int i = 0; i < 2; i++)
            {
                var tag = Choice.UiRoot.Q("choice-tag-" + i);
                if (!Shown(tag)) continue;
                Assert.GreaterOrEqual(tag.worldBound.xMin, ChoiceSceneView.TagGutter - 1f, "tag " + i + " clear of the left edge");
                Assert.LessOrEqual(tag.worldBound.xMax, Choice.UiRoot.worldBound.width - ChoiceSceneView.TagGutter + 1f, "tag " + i + " clear of the right edge");
            }
            yield return Shot("t3-held-choice-10s-later");

            // A touch on an option in the box answers it (the Box path), no card opened.
            Assert.IsTrue(Choice.TryScreenOf(0, out var s0), "option 0 on screen");
            Assert.AreNotEqual(CardStackMode.Open, Model.Mode);
            yield return TapAt(s0);
            yield return Frames(3);
            Assert.AreEqual(DilemmaState.Answered, d.state, "a touch in the box answers: " + ChoiceSceneView.DebugStatus());
            Assert.AreEqual(DilemmaPath.Box, d.path);
            Assert.AreEqual(ChoiceSceneView.Phase.Reacting, Choice.State);
            yield return Until(() => Choice.State == ChoiceSceneView.Phase.Idle, 30f);
            Assert.IsFalse(Choice.BoxBarUp, "the bar goes with the scene");
            AssertNoErrors("held choice staged");
        }

        /// <summary>t3-choice (tester run 2 D): a choice raised while the player watches: the peek switches to it with the "?" mark,
        /// and the people stage it in the box.</summary>
        [UnityTest, Timeout(600000)]
        public IEnumerator AChoiceRaisedLiveTakesThePeekAndIsStaged()
        {
            yield return OpenBox(1170, 2532);
            DilemmaCheats.Toggle(true);
            Assert.IsTrue(Model.DebugPrepare(3), "a return's peek with older stories");
            yield return Frames(4);
            Dilemma d = null;
            for (int i = 0; i < 4 && d == null; i++)
            {
                d = Sim.DebugRaiseDilemma("camp_fire");
                if (d == null) { SimLayoutSource.DebugSkipYears(1f); yield return null; }
            }
            Assert.IsNotNull(d);
            Sim.DebugHoldChoiceForOpen(); // what a live raise does (TryRaiseDilemma); the cheat raises around it
            yield return Until(() => Model.PeekIs(d.entryId), 10f);
            Assert.IsTrue(Model.PeekIs(d.entryId), "the peek switched to the new choice");
            Assert.IsTrue(Cards.Root.Q("cards-peek").ClassListContains("kg-peek--choice"), "and wears the ? mark");
            yield return Until(() => Choice.State == ChoiceSceneView.Phase.Waiting, 60f);
            Assert.IsTrue(Choice.InBox, "staged in the box while watched: " + HeldMomentStage.Instance?.LastWhy);
            yield return Shot("t3-live-choice-peek-and-stage");
            Assert.IsTrue(ChoiceSceneView.DebugRelax().StartsWith("let go"));
            yield return Frames(3);
            Assert.AreEqual(ChoiceSceneView.Phase.Drifting, Choice.State, "it lets go");
            Assert.IsFalse(Choice.BoxBarUp);
            yield return Until(() => Choice.State == ChoiceSceneView.Phase.Idle, 30f);
            yield return Until(() => Model.PeekIs(d.entryId), 10f);
            Assert.IsTrue(Model.PeekIs(d.entryId), "still one touch away on the peek");
            Assert.AreEqual(DilemmaState.Open, d.state, "never resolves unseen");
            AssertNoErrors("live choice");
        }

        public override void TearDown()
        {
            ChoiceSceneView.DebugWatch = false;
            base.TearDown();
        }
    }
}
