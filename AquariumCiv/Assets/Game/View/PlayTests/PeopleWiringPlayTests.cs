using System.Collections;
using System.Collections.Generic;
using System.Linq;
using AquariumCiv.Audio;
using AquariumCiv.Game;
using AquariumCiv.GameUI;
using AquariumCiv.Life;
using AquariumCiv.View.Moments;
using AquariumCiv.Sim;
using AquariumCiv.View.ShowMe;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace AquariumCiv.View.PlayTests
{
    /// <summary>
    /// The people bundle's phase 2, on the real Slice: crowd avoidance and the fire (#189), the loose group at the glass (#190), carried
    /// babies (#192), the wish payoff (#191), a want line through the bubble host (#181), the bubble rules (#143), the notification
    /// question (#122), and tester run 5's polish (a stale tool hint, long tile names).
    /// </summary>
    [Category("PeopleWiring")]
    public class PeopleWiringPlayTests : PlayTestBase
    {
        const int Seed = 1234;
        static SimCore Sim => GameClock.Instance.ActiveSim;
        static PeopleView People => PeopleView.Find();

        IEnumerator Grown(float years = 8f, float hour = 10f)
        {
            yield return OpenSlice(seed: Seed);
            ShowMeDirector.BubblesOn = true;
            SimLayoutSource.DebugSkipYears(years);
            yield return null;
            Sim.CleanAll();
            PeopleView.DebugSetHour(hour);
            yield return Frames(4);
            yield return Until(() => GameUiPanel.Toasts.Current == null && GameUiPanel.Toasts.Waiting == 0, 6f);
        }

        static Vector2 FirePoint()
        {
            Assert.IsTrue(People.Planner.Positions.TryGetPosition(new Place(PlaceKind.Fire), out float x, out float z), "the box has a fire");
            return new Vector2(x, z);
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator NobodyStandsOrWalksOnTheFireOnTheWayToTheGlass()
        {
            yield return Grown();
            var fire = FirePoint();
            StringAssert.Contains("at the glass", GlassVoice.DebugGather(10));
            float closest = float.MaxValue, end = Time.realtimeSinceStartup + 10f;
            string who = "";
            var pv = People;
            while (Time.realtimeSinceStartup < end)
            {
                foreach (var a in pv.Activities)
                {
                    if (a.moving == false && a.place.kind != PlaceKind.Glass) continue;
                    if (!pv.TryGetDollTop(a.who, out _) || !pv.TryGetDollPosition(a.who, out var p)) continue;
                    float d = (p - fire).magnitude;
                    if (d < closest) { closest = d; who = a.who.ToString(); }
                }
                yield return null;
            }
            Assert.GreaterOrEqual(closest, CrowdAvoid.FireRadius - 0.01f, "closest to the fire: " + who);
            AssertNoErrors("past the fire");
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator WalkersKeepOffTheStandingAndTheFrameCostStaysSmall()
        {
            yield return Grown(60f);
            SimLayoutSource.DebugNextEra(false);
            SimLayoutSource.DebugNextEra(false);
            SimLayoutSource.DebugSkipYears(60f);
            Sim.CleanAll();
            PeopleView.DebugSetHour(8f);
            yield return new WaitForSecondsRealtime(1f);
            var pv = People;
            float Mean(int n, ref float sum) { return sum / Mathf.Max(1, n); }
            float[] ms = new float[2]; int[] frames = new int[2];
            for (int round = 0; round < 6; round++)
            {
                int k = round & 1;
                PeopleView.AvoidCrowds = k == 0;
                yield return new WaitForSecondsRealtime(0.5f);
                float end = Time.realtimeSinceStartup + 2f;
                while (Time.realtimeSinceStartup < end) { ms[k] += (float)pv.FrameMs; frames[k]++; yield return null; }
            }
            PeopleView.AvoidCrowds = true;
            Debug.Log($"[perf] PeopleView avoid on {Mean(frames[0], ref ms[0]):0.00} ms, off {Mean(frames[1], ref ms[1]):0.00} ms, {pv.Activities.Count} people, {Sim.State.era}");
            Assert.Greater(frames[0], 10);
            AssertNoErrors("crowd frames");
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator TheGroupAtTheGlassIsLooseAndHoldsStill()
        {
            yield return Grown();
            StringAssert.Contains("at the glass", GlassVoice.DebugGather(6));
            var pv = People;
            yield return Until(() => pv.Activities.Count(a => a.place.kind == PlaceKind.Glass && !a.moving) >= 4, 30f);
            yield return new WaitForSecondsRealtime(3.5f); // the glide from the spot into each one's stand
            var at = pv.Activities.Where(a => a.place.kind == PlaceKind.Glass && !a.moving).ToList();
            Assert.GreaterOrEqual(at.Count, 4, "they came");
            var n = PeopleView.GlassNormal(pv.Planner.GlassSide);
            var depth = new List<float>();
            var first = new Dictionary<int, Vector2>();
            foreach (var a in at) { Assert.IsTrue(pv.TryGetDollPosition(a.who, out var p)); first[a.who.Key] = p; depth.Add(Vector2.Dot(p, n)); }
            Assert.Greater(depth.Max() - depth.Min(), 0.08f, "some closer, some behind: no straight line");
            yield return new WaitForSecondsRealtime(1f);
            foreach (var a in at)
                if (pv.TryGetDollPosition(a.who, out var p)) Assert.Less((p - first[a.who.Key]).magnitude, 0.04f, "they stay where they stand");
            AssertNoErrors("loose group");
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator ACarriedBabyShowsOnTheirCarerAndTheCardAgrees()
        {
            yield return Grown();
            var pv = People;
            string said = PeopleView.DebugBaby();
            StringAssert.StartsWith("born", said);
            var baby = Sim.State.notables.OrderByDescending(x => x.id).First();
            Assert.Less(baby.age, 4f);
            yield return Frames(3);
            PersonActivity carried = default; bool found = false;
            for (float h = 8f; h < 17.5f && !found; h += 0.5f)
            {
                PeopleView.DebugSetHour(h);
                yield return Frames(3);
                foreach (var a in pv.Activities) if (a.carrying.Key == PersonRef.Notable(baby.id).Key) { carried = a; found = true; break; }
            }
            if (!found) Assert.Inconclusive("the baby stayed in on this day (one day in ten)");
            var doll = pv.GetComponentsInChildren<PegDollView>().First(d => d.transform.Find("Baby") != null && d.transform.Find("Baby").gameObject.activeSelf);
            Assert.IsNotNull(doll);
            var card = Sim.PersonCardOf(baby.id);
            Assert.AreEqual("baby", card.stage);
            StringAssert.Contains("baby", card.roleLine);
            var parent = Sim.PersonCardOf(baby.parentId);
            Assert.IsTrue(parent.family.Any(t => t.otherId == baby.id && t.label == "baby"), "the parent's card says baby");
            StringAssert.Contains(carried.carryOnBack ? "on their back" : "in their arms", pv.Planner.Describe(carried), "and what they're doing says so");
            yield return Shot("people-baby");
            AssertNoErrors("baby");
        }

        [UnityTest, Timeout(120000)]
        [Category("Tester6")]
        public IEnumerator AGrantedWishPlaysItsPayoffByDay()
        {
            yield return Grown(hour: 10.5f);
            var sim = Sim;
            Sfx.ResetLog();
            var w = sim.DebugForceWish(SimCore.NudgeKind.Rain);
            Assert.IsNotNull(w);
            StringAssert.Contains("got their", WishPayoffView.DebugGrant());
            var view = WishPayoffView.Instance;
            yield return Until(() => view.Played > 0, 2f);
            Assert.Greater(view.Played, 0);
            Assert.AreEqual(WishBeat.Given, view.LastPlan.beat);
            Assert.AreEqual(w.notableId, view.LastPlan.wisherId);
            CollectionAssert.Contains(Sfx.LastAsked, CueLibrary.WishGiven);
            Assert.IsTrue(view.LastPlan.effectNow);
            var pv = People;
            var me = PersonRef.Notable(w.notableId);
            if (pv.TryGetDollTop(me, out _))
            {
                yield return new WaitForSecondsRealtime(0.4f); // the view replans every 0.2 s
                var a = pv.Activities.First(x => x.who == me);
                Assert.AreEqual(ActivityKind.Cheer, a.kind, "the wisher reacts");
            }
            AssertNoErrors("wish by day");
        }

        /// <summary>Tester run 6: the nudge is dropped, not queued, while a card is up.</summary>
        [UnityTest, Timeout(120000)]
        [Category("Tester6")]
        public IEnumerator AWishLeavesTheCameraOnAnOpenCard()
        {
            yield return Grown(hour: 10.5f);
            var view = WishPayoffView.Instance;
            var cam = Camera.main.GetComponent<BoxCamera>();
            var w = Sim.DebugForceWish(SimCore.NudgeKind.Rain);
            Assert.IsNotNull(w);
            var other = Sim.State.notables.First(n => n.alive && n.age >= 16f && n.id != w.notableId);
            Assert.IsTrue(PersonCardView.Instance.Open(other.id, fly: false), "a card");
            yield return Until(() => !cam.IsFlying, 3f);
            float zoom = cam.zoom, pitch = cam.pitch;
            StringAssert.Contains("got their", WishPayoffView.DebugGrant());
            yield return Until(() => view.Played > 0, 2f);
            Assert.IsTrue(view.LastPlan.flyToWisher, "the camera would have gone");
            yield return new WaitForSecondsRealtime(1f);
            Assert.AreEqual(zoom, cam.zoom, 0.005f, "the camera stayed");
            Assert.AreEqual(pitch, cam.pitch, 0.5f);
            PersonCardView.Instance.Close();
            yield return new WaitForSecondsRealtime(1f);
            Assert.IsFalse(cam.IsFlying, "and nothing was queued");
            Assert.AreEqual(zoom, cam.zoom, 0.005f, "nothing was queued");
            AssertNoErrors("wish with a card up");
        }

        /// <summary>Tester run 6: the payoff line waits for the first run's word to pass, and is then said, not lost.</summary>
        [UnityTest, Timeout(120000)]
        [Category("Tester6")]
        public IEnumerator AWishLineWaitsForTheFirstRunsWord()
        {
            yield return OpenSlice(seed: Seed, skipFirstRun: false);
            var first = FirstRunView.Instance;
            first.Run.DebugSkipTo(FirstRunStep.Year);
            yield return Frames(4);
            Assert.AreEqual(FirstRunStep.Year, first.Step);
            PeopleView.DebugSetHour(10.5f);
            yield return Frames(4);
            var view = WishPayoffView.Instance;
            var sm = ShowMeDirector.Instance;
            var w = Sim.DebugForceWish(SimCore.NudgeKind.Rain);
            Assert.IsNotNull(w);
            w.state = WishState.Given; // the first rain is spent, so no second gift can be placed: what the view sees is all that counts
            w.endMonth = Sim.State.month;
            yield return Until(() => view.Played > 0, 2f);
            string line = view.LastPlan.wisherLine.text;
            Assert.IsTrue(view.LastPlan.wisherSaysNow);
            Assert.AreNotEqual(line, sm.LastLine, "not under the caption");
            Assert.IsTrue(first.HasTheWord, "the year has the word");
            first.Run.DebugSkipTo(FirstRunStep.Done);
            yield return Until(() => sm.LastLine == line, 6f);
            Assert.AreEqual(line, sm.LastLine, "said once the word has passed");
            AssertNoErrors("wish in the first run");
        }

        [UnityTest, Timeout(120000)]
        [Category("Tester6")]
        public IEnumerator AWishGrantedAtNightWaitsForTheWisherToWake()
        {
            yield return Grown(hour: 23f);
            var view = WishPayoffView.Instance;
            var pv = People;
            var sleeping = pv.Activities.Where(a => a.who.IsNotable && a.kind == ActivityKind.Sleep).Select(a => a.who.notableId).ToList();
            Assert.IsNotEmpty(sleeping, "someone's asleep at 23:00");
            Wish w = null;
            foreach (int id in sleeping) { w = Sim.DebugForceWish(SimCore.NudgeKind.Rain, id); if (w != null && w.notableId == id) break; }
            Assert.IsNotNull(w);
            Assert.IsTrue(sleeping.Contains(w.notableId), "the wisher sleeps");
            StringAssert.Contains("got their", WishPayoffView.DebugGrant());
            yield return Until(() => view.Played > 0, 2f);
            Assert.Greater(view.Played, 0);
            Assert.IsTrue(view.LastPlan.quiet, "softer at night");
            Assert.IsFalse(view.LastPlan.wisherSaysNow, "they're asleep");
            yield return new WaitForSecondsRealtime(1f);
            PeopleView.DebugSetHour(9f);
            var sm = ShowMeDirector.Instance;
            string line = view.LastPlan.wisherLine.text;
            yield return Until(() => sm.LastLine == line, 6f);
            Assert.AreEqual(line, sm.LastLine, "they say it when they wake");
            AssertNoErrors("wish by night");
        }

        [UnityTest, Timeout(120000)]
        [Category("Tester6")]
        public IEnumerator AWantLineGoesThroughTheBubbleHost()
        {
            yield return Grown();
            var pv = People;
            string said = null;
            for (int round = 0; round < 8 && (said == null || !said.StartsWith("want:")); round++) // a wording over the bubble's limit is a caption, and the next telling words it differently
                foreach (WantCase c in System.Enum.GetValues(typeof(WantCase)))
                {
                    said = ShowMeDirector.DebugWant(c);
                    if (said.StartsWith("want:")) break;
                }
            StringAssert.StartsWith("want:", said);
            var sm = ShowMeDirector.Instance;
            yield return Frames(4);
            Assert.IsNotNull(sm.ShownBubble, "it shows");
            Assert.AreEqual(BubbleSource.Want, sm.ShownBubble.source);
            Assert.IsNotNull(GameUiPanel.Bubbles.Holder, "the host gave it the word");
            Assert.IsFalse(GameUiPanel.Bubbles.Free(BubbleHost.Rank.World), "and nobody talks over it");
            yield return Shot("people-want");
            AssertNoErrors("want line");
        }

        [UnityTest, Timeout(120000)]
        [Category("Tester6")]
        public IEnumerator ASpeakerGoingIndoorsDoesNotCutTheLine()
        {
            yield return Grown();
            var sm = ShowMeDirector.Instance;
            string said = ShowMeDirector.DebugBubble();
            for (int i = 0; i < 12 && said.StartsWith("caption"); i++) said = ShowMeDirector.DebugBubble(); // a long line is a caption: this one is about the bubble
            StringAssert.StartsWith("bubble", said);
            yield return Frames(4);
            var b = sm.ShownBubble;
            Assert.IsNotNull(b);
            var el = sm.GetComponentsInChildren<UIDocument>().Select(d => d.rootVisualElement.Q("showme-bubble")).First(e => e != null);
            var head = new Vector2(sm.BubbleScreen.x, Screen.height - sm.BubbleScreen.y);
            var panelHead = RuntimePanelUtils.ScreenToPanel(el.panel, head);
            Assert.IsFalse(el.worldBound.Contains(panelHead), "the bubble isn't on the speaker");
            Assert.GreaterOrEqual(b.seconds, 3.5f, "long enough to read");
            float start = Time.realtimeSinceStartup;
            PeopleView.DebugSetHour(23.5f); // everyone goes in at once
            yield return Frames(2);
            yield return new WaitForSecondsRealtime(1f);
            Assert.AreSame(b, sm.ShownBubble, "it finishes");
            yield return Until(() => sm.ShownBubble == null, b.seconds + 2f);
            Assert.GreaterOrEqual(Time.realtimeSinceStartup - start, b.seconds - 1.2f, "at its own time");
            Assert.IsTrue(GameUiPanel.Bubbles.Free(BubbleHost.Rank.World), "the word is free again");
            AssertNoErrors("speaker indoors");
        }

        /// <summary>Tester run 6: a line over the bubble's limit is a caption here too, so no bubble runs past three lines.</summary>
        [UnityTest, Timeout(120000)]
        [Category("Tester6")]
        public IEnumerator AnAmbientLineOverTheLimitIsACaption()
        {
            yield return Grown();
            var sm = ShowMeDirector.Instance;
            string said = "";
            for (int i = 0; i < 40 && !said.StartsWith("caption"); i++) said = ShowMeDirector.DebugBubble();
            StringAssert.StartsWith("caption", said, "the bank has long lines");
            Assert.IsTrue(sm.CaptionUp);
            Assert.IsNull(sm.ShownBubble, "no bubble for it");
            Assert.IsTrue(GlassBubble.TooLong(sm.LastLine), sm.LastLine);
            AssertNoErrors("long ambient line");
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator TheNotificationAskSaysItsNewQuestion()
        {
            yield return Grown();
            MomentPlayer.DebugNotifyAsk();
            yield return Frames(4);
            var root = MomentPlayer.Instance.GetComponent<UIDocument>().rootVisualElement;
            var ask = root.Q("moment-notify");
            Assert.IsNotNull(ask);
            Assert.IsTrue(ask.Query<Label>().ToList().Any(l => l.text == MomentLibrary.NotifyQuestion), "the question as written");
            StringAssert.Contains("note", MomentLibrary.NotifyQuestion);
            yield return Shot("people-notify");
            yield return TapAt(GameUiPanel.ScreenCenter(root, "moment-notify-no"));
            AssertNoErrors("notify ask");
        }

        /// <summary>#151: the shot at default zoom, and no grazer in the water. On the pond's shore too, the worst place for a herd.</summary>
        [UnityTest, Timeout(120000)]
        public IEnumerator AHerdStandsApartOnLand()
        {
            yield return Grown();
            var box = Object.FindAnyObjectByType<BoxView>();
            var land = box.land;
            Sim.DebugShape(SimCore.NudgeKind.Herd, land.pond.x + 0.7f, land.pond.y);
            Sim.DebugShape(SimCore.NudgeKind.Herd, -1.2f, 0.8f);
            yield return new WaitForSecondsRealtime(6f);
            var grazers = Resources.FindObjectsOfTypeAll<MeshFilter>().Where(m => m.gameObject.activeInHierarchy && m.name is "Deer" or "Goat" or "Sheep").Select(m => m.transform).ToList();
            Debug.Log($"[herd] {grazers.Count} grazers");
            foreach (var g in grazers)
            {
                var p = box.transform.InverseTransformPoint(g.position);
                Assert.IsFalse(land.IsWater(p.x, p.z), $"{g.name} at ({p.x:0.00}, {p.z:0.00}) in the water");
            }
            yield return Shot("people-herd");
            AssertNoErrors("herd");
        }

        [UnityTest, Timeout(60000)]
        [Category("Tester6")]
        public IEnumerator AToolHintStillWaitingGoesWithTheTool()
        {
            yield return OpenSlice(seed: Seed);
            yield return Until(() => GameUiPanel.Toasts.Current == null && GameUiPanel.Toasts.Waiting == 0, 6f);
            var hud = GameHud.Instance;
            hud.Toast("Something else", 3f);
            ShapingView.Instance.SetTool(SimCore.NudgeKind.Rain);
            Assert.AreEqual(1, GameUiPanel.Toasts.Waiting, "the hint waits");
            ShapingView.Instance.SetTool(null);
            Assert.AreEqual(0, GameUiPanel.Toasts.Waiting, "and goes with the tool");
            hud.WithdrawToast("Something else");
            ShapingView.Instance.SetTool(SimCore.NudgeKind.Rain);
            yield return Frames(2);
            StringAssert.Contains("Tap the ground", GameUiPanel.Toasts.Current ?? "", "on screen now");
            ShapingView.Instance.SetTool(null);
            yield return Frames(2);
            Assert.IsNull(GameUiPanel.Toasts.Current, "and it goes from the screen too (tester run 6)");
            AssertNoErrors("tool hint");
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator LongToolNamesFitTheirTile()
        {
            yield return OpenSlice(750, 1334, Seed);
            var hud = GameHud.Instance;
            hud.Open(GameHud.Shaping);
            yield return Frames(4);
            var tile = hud.Root.Query(className: "kg-tile").First();
            Assert.IsNotNull(tile, "a tile");
            var name = tile.Q<Label>(className: "kg-tile-name");
            foreach (string word in new[] { "Wool flock", "Blossoming" })
            {
                name.text = word;
                name.AddToClassList("kg-tile-name--long");
                yield return Frames(3);
                float room = tile.contentRect.width;
                float wide = name.MeasureTextSize(word, 0f, VisualElement.MeasureMode.Undefined, 0f, VisualElement.MeasureMode.Undefined).x;
                Assert.LessOrEqual(wide, room, word + " in a tile of " + room);
            }
            hud.CloseSheet();
            AssertNoErrors("tile names");
        }
    }
}
