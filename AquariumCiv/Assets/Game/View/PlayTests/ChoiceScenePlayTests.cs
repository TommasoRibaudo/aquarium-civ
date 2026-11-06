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
                Assert.Less((at - new Vector2(p.x, p.z)).magnitude, 0.06f, when + ": " + p.who + " stands on their spot");
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
        static readonly string[] LazyRoots = { "SpeakerRing", "ChatterMarks", "HitFx" }; // HitFx: a scene root since batch 1; its puff renderer is made on the first hit
        static int Renderers() => Resources.FindObjectsOfTypeAll<MeshRenderer>()
            .Count(r => r != null && r.gameObject.scene.IsValid() && r.GetComponentInParent<PegDollView>() == null
                        && System.Array.IndexOf(LazyRoots, r.transform.root.name) < 0);
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
            Assert.IsFalse(added.Any(n => n.Contains("Choice")), when + ": a root of the choice scene was left: " + string.Join(", ", added));
            Assert.LessOrEqual(added.Count, 1, when + ": new roots " + string.Join(", ", added));
        }
        static int ChoiceObjects() => Resources.FindObjectsOfTypeAll<Transform>().Count(t => t != null && t.name.StartsWith("Choice") && t.name != "ChoiceTags" && t.gameObject.scene.IsValid());

        [UnityTest, Timeout(300000)]
        public IEnumerator ShowMeThenATouchOnTheLeftPersonAnswersInTheBox()
        {
            yield return OpenBox(1170, 2532);
            yield return ToTheCard("camp_fire");
            var roots = RootNames(); int renderers = Renderers();
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
            Assert.LessOrEqual(Renderers(), renderers, "no leaked renderers");
            Debug.Log($"[ChoiceScene] sanity: roots {roots.Count} -> {Roots()}, renderers {renderers} -> {Renderers()}, choice objects {ChoiceObjects()}");
            AssertNoErrors("who by touch");
        }

        [UnityTest, Timeout(300000)]
        public IEnumerator LeavingWithoutATouchKeepsItOpenAndThePeopleDriftBack()
        {
            yield return OpenBox(1170, 2532);
            yield return ToTheCard("camp_remember");
            var roots = RootNames(); int renderers = Renderers();
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
            Assert.LessOrEqual(Renderers(), renderers);
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
            yield return TapAt(at);
            yield return Frames(3);
            Assert.AreEqual(DilemmaState.Answered, d.state, "a touch on a side still answers");
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

        public override void TearDown()
        {
            ChoiceSceneView.DebugWatch = false;
            base.TearDown();
        }
    }
}
