using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using AquariumCiv.Game;
using AquariumCiv.GameUI;
using AquariumCiv.Life;
using AquariumCiv.Sim;
using AquariumCiv.View.Cards;
using AquariumCiv.View.Moments;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using K = AquariumCiv.Sim.SimCore.NudgeKind;

namespace AquariumCiv.View.PlayTests
{
    /// <summary>
    /// #163: the capture pass. Not a check: an [Explicit] tour that forces every state in docs/design/ui-inventory.md §3 on
    /// the real Slice scene and saves a JPG to docs/design/ui/captures (small/ for 750x1334), named after the inventory
    /// section. Run it by hand: <c>--filter UiCapture --filter_type category --async_tests true</c>; a single group with the
    /// test's name. Each shot writes a line to Temp/agent/ui-captures.tsv: "CHECK" means the state wasn't there when it was
    /// taken. Only a thrown exception fails a test. The debug dock is hidden unless the shot is of the dock.
    /// </summary>
    [Explicit("Writes screenshots into docs/: run by hand"), Category("UiCapture")]
    public class UiCaptureTour : PlayTestBase
    {
        const int Seed = 7531, Long = 900000;
        static readonly string Out = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "docs", "design", "ui", "captures")) + "/";
        static readonly string Manifest = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Temp", "agent", "ui-captures.tsv"));
        string folder = "";

        static SimCore Sim => GameClock.Instance.ActiveSim;
        static GameHud Hud => GameHud.Instance;
        static CardStackView Stack => CardStackView.Instance;
        static CardStackModel Model => Stack.Model;
        static ShapingView Sv => ShapingView.Instance;
        static PersonCardView Cards => PersonCardView.Instance;
        static VisualElement UiOf(Component c) => c.GetComponentInChildren<UIDocument>().rootVisualElement;

        static bool Shown(VisualElement e)
        {
            if (e == null) return false;
            for (; e != null; e = e.parent) if (e.resolvedStyle.display == DisplayStyle.None) return false;
            return true;
        }

        public override void TearDown()
        {
            DebugDock.ResetLock();
            base.TearDown();
        }

        /// <summary>Saves the frame. <paramref name="ok"/> false marks the shot CHECK in the manifest.</summary>
        IEnumerator Cap(string name, bool ok = true, string note = "")
        {
            yield return new WaitForEndOfFrame();
            var tex = ScreenCapture.CaptureScreenshotAsTexture();
            Directory.CreateDirectory(Out + folder);
            File.WriteAllBytes(Out + folder + name + ".jpg", tex.EncodeToJPG(80));
            Directory.CreateDirectory(Path.GetDirectoryName(Manifest));
            File.AppendAllText(Manifest, $"{(ok ? "ok" : "CHECK")}\t{folder}{name}\t{tex.width}x{tex.height}\t{note}\n");
            UnityEngine.Object.Destroy(tex);
        }

        static IEnumerator Years(float years)
        {
            for (float y = 0f; y < years; y += 10f) { SimLayoutSource.DebugSkipYears(Mathf.Min(10f, years - y)); yield return null; }
        }

        /// <summary>A box of this age: clean glass, midday, nothing unread, the dock out of the picture.</summary>
        IEnumerator Grown(float years, int w = 1179, int h = 2556, int seed = Seed)
        {
            yield return OpenSlice(w, h, seed: seed);
            DebugDock.ForceLocked = true;
            yield return Years(years);
            yield return Calm();
        }

        static IEnumerator Calm()
        {
            Sim.CleanAll();
            Sim.CardsSeenToNow();
            PeopleView.DebugSetHour(11f);
            yield return Frames(40);
        }

        IEnumerator Tap(VisualElement root, string name)
        {
            var e = root.Q(name);
            Assert.IsNotNull(e, name);
            yield return TapAt(GameUiPanel.ScreenCenter(e));
            yield return Frames(4);
        }

        static int FirstAdult()
        {
            var pv = PeopleView.Find();
            var shown = new List<int>();
            pv.NotablesShown(shown);
            var n = shown.Select(Sim.FindNotable).Where(x => x != null && x.alive && x.age >= SimCore.AdultAge).OrderBy(x => x.id).FirstOrDefault();
            return (n ?? Sim.State.notables.First(x => x.alive)).id;
        }

        (float x, float z) OpenSpot()
        {
            foreach (var (px, pz) in new[] { (1.3f, 0.6f), (-1.3f, 0.6f), (1.3f, -0.6f), (-1.3f, -0.6f), (0.3f, 1.2f), (-1.4f, -0.4f) })
            {
                float ax = px, az = pz;
                if (Sim.Settlement.OpenSpotNear(ref ax, ref az, 0.35f)) return (ax, az);
            }
            return (1.3f, 0.6f);
        }

        // ================================================================== 3.1 always there
        [UnityTest, Timeout(Long), Category("CapA")]
        public IEnumerator A_Hud()
        {
            yield return Grown(8f);
            var hud = Hud;
            var title = hud.Root.Q<Label>(className: "hud-title-name");
            Sim.State.settlementName = "";
            yield return Frames(3);
            yield return Cap("3.1-title-default-name", title.text == "Your box", title.text);
            Sim.State.settlementName = "Mossbarrow";
            yield return Frames(3);
            yield return Cap("3.1-hud-default", !Shown(hud.Root.Q("hud-history-dot")), title.text);

            hud.SetCleanMode(true);
            yield return Frames(4);
            yield return Cap("3.1-clean-on", hud.CleanMode);
            CareView.Instance.idleExitSeconds = 0f;
            yield return Until(() => !hud.CleanMode, 3f);
            yield return Frames(4);
            yield return Cap("3.1-clean-auto-off", !hud.CleanMode);
            CareView.Instance.idleExitSeconds = 30f;
            yield return new WaitForSecondsRealtime(2.5f);

            hud.SetCameraMode(true);
            yield return Frames(6);
            yield return Cap("3.1-photo-camera-mode", Shown(hud.Root.Q("hud-photo-exit")));
            hud.SetCameraMode(false);
            yield return Frames(6);

            int id = FirstAdult();
            Cards.Open(id, false);
            Cards.TogglePin();
            yield return Frames(3);
            Cards.Close();
            yield return Frames(4);
            yield return Cap("3.1-pin-pinned", hud.PinChipText != null, hud.PinChipText);
            CardStackView.DebugClearRead();
            CardStackView.DebugReturn(3);
            yield return Frames(3);
            Model.HidePeek();
            yield return Frames(4);
            yield return Cap("3.1-hud-pin-dot", Model.HistoryDot && hud.PinChipText != null);
            Cards.TogglePin();
            yield return Frames(3);
            yield return Cap("3.1-history-dot", Shown(hud.Root.Q("hud-history-dot")));
            yield return Cap("3.3-peek-dismissed", Model.Mode == CardStackMode.Hidden);
            Sim.CardsSeenToNow();
            yield return Frames(40);

            DebugDock.ForceLocked = false;
            yield return Frames(4);
            yield return Cap("3.1-debug-dock-closed");
            DebugDock.Active = DebugDock.Dbg;
            yield return Frames(4);
            yield return Cap("3.1-debug-dock-open-dbg");
            DebugDock.Active = DebugDock.Sim;
            yield return Frames(4);
            yield return Cap("3.1-debug-dock-open-sim");
            DebugDock.Active = DebugDock.None;
            DebugDock.ForceLocked = true;
            yield return Frames(4);

            hud.Open(GameHud.Settings);
            yield return Frames(6);
            yield return Cap("3.8-settings-sheet", hud.OpenSheet == GameHud.Settings);
            hud.CloseSheet();
            yield return Frames(4);
            AssertNoErrors("hud");
        }

        /// <summary>The look of the box under the HUD: dirty, night, and the care states.</summary>
        [UnityTest, Timeout(Long), Category("CapB")]
        public IEnumerator B_DirtyNightClean()
        {
            yield return Grown(8f);
            var hud = Hud;
            PeopleView.DebugSetHour(22f);
            NightLamps.DebugSetLevel(1f);
            yield return Frames(40);
            yield return Cap("extra-hud-default-night");
            NightLamps.DebugSetLevel(0f);
            PeopleView.DebugSetHour(11f);
            Sim.AddMess(0.7f, 0.5f, 6);
            yield return Frames(60);
            yield return Cap("extra-hud-default-dirty", Sim.State.grime > 0.3f, "grime " + Sim.State.grime);
            hud.SetCleanMode(true);
            yield return Frames(6);
            yield return Cap("3.4-cleanmode-on", hud.CleanMode);
            Sim.CleanAll();
            CareView.Instance.Cheer(Sim, true);
            yield return Frames(10);
            yield return Cap("3.4-cleanmode-spotless", hud.CleanMode, CareView.Instance.LastCheer);
            yield return Until(() => !hud.CleanMode, 6f);
            yield return Frames(3);
            AssertNoErrors("dirty, night");
        }

        // ================================================================== 3.2 in the world, 3.4 tray and shaping
        [UnityTest, Timeout(Long), Category("CapC")]
        public IEnumerator C_WorldAndShaping()
        {
            yield return Grown(4f);
            var hud = Hud;
            var ui = UiOf(Sv);
            Sim.State.shapingCharges = 3f;

            yield return TapAt(GameUiPanel.ScreenCenter(hud.Root.Q("hud-shape")));
            yield return Frames(8);
            yield return Cap("3.4-tray-tools", hud.OpenSheet == GameHud.Shaping, hud.Root.Q<Label>("shaping-charges-text")?.text);
            var ch = hud.Root.Q("shaping-tool-" + K.Channel);
            yield return Cap("3.4-tray-locked", ch != null && ch.ClassListContains("tool--off"), ch?.Q<Label>(className: "tool-reason")?.text);
            Sim.State.shapingCharges = 0f;
            yield return Frames(8);
            yield return Cap("3.4-tray-empty", hud.Root.Q("shaping-tool-" + K.Rain).ClassListContains("tool--off"));
            Sim.DebugRefillShaping();
            hud.CloseSheet();
            yield return Frames(4);

            Assert.IsTrue(Sv.ArmTool(K.Grove));
            yield return Frames(6);
            yield return Cap("3.4-shapingmode-armed", Shown(ui.Q("shaping-wish-bar")), ui.Q<Label>("shaping-banner").text);
            var (x, z) = OpenSpot();
            Sim.State.shapingCharges = 0f;
            yield return TapAt(Sv.ScreenOf(x, z));
            yield return Frames(4);
            yield return Cap("3.4-shapingmode-refused", Sv.ShapeMode, Sv.LastResult);
            Sim.DebugRefillShaping();
            yield return Frames(90);
            Sv.CancelTool();
            yield return Frames(4);

            var w = Sim.DebugForceWish(K.Grove);
            Assert.IsNotNull(w);
            Assert.IsTrue(Sv.ArmWish(w.id));
            yield return Frames(6);
            yield return Cap("3.4-shapingmode-wish-armed", Sv.WishArmed && Shown(ui.Q("shaping-wish-spot")), ui.Q<Label>("shaping-banner").text);
            Sv.CancelWish();
            yield return Frames(4);

            Assert.IsTrue(Sv.ArmTool(K.Rain));
            yield return Frames(3);
            yield return TapAt(Sv.ScreenOf(x, z));
            yield return Frames(6);
            yield return Cap("3.4-shapingmode-placed", !Sv.ShapeMode, Sv.LastResult);
            var chip = ui.Q("shaping-takeback-0");
            yield return Until(() => Shown(chip), 12f);
            yield return Frames(2);
            yield return Cap("3.2-takeback-visible", Shown(chip));
            hud.Open(GameHud.Shaping);
            yield return Frames(8);
            yield return Cap("3.4-tray-capped", hud.Root.Q("shaping-tool-" + K.Rain).ClassListContains("tool--off"), hud.Root.Q("shaping-tool-" + K.Rain).Q<Label>(className: "tool-reason")?.text);
            yield return Cap("3.4-tray-takeback-list", hud.Root.Query<Button>(className: "tray-takeback").First() != null);
            hud.CloseSheet();
            yield return Frames(4);
            AssertNoErrors("shaping");
        }

        [UnityTest, Timeout(Long), Category("CapD")]
        public IEnumerator D_AmbientBubble()
        {
            yield return Grown(30f);
            var d = ShowMeDirector.Instance;
            Camera.main.GetComponent<BoxCamera>().FlyTo(Vector3.zero, 0.75f, 0.2f);
            yield return new WaitForSecondsRealtime(1f);
            string said = ShowMeDirector.DebugBubble();
            yield return Frames(4);
            var el = d.GetComponentInChildren<UIDocument>().rootVisualElement.Q("showme-bubble");
            yield return Until(() => Shown(el), 8f);
            yield return Cap("3.2-bubble-shown", Shown(el), said);
            AssertNoErrors("bubble");
        }

        // ================================================================== 3.3 the return
        [UnityTest, Timeout(Long), Category("CapE")]
        public IEnumerator E_Return()
        {
            yield return OpenSlice(seed: Seed);
            DebugDock.ForceLocked = true;
            SimLayoutSource.DebugSkipYears(8f);
            yield return null;
            Sim.CleanAll();
            PeopleView.DebugSetHour(11f);
            GameClock.DebugAway(24);
            yield return Frames(8);
            yield return Cap("3.3-peek-shown", Model.Mode == CardStackMode.Peek, Model.Peek.text + " / teaser: " + Model.Peek.teaser);
            Model.TapPeek();
            yield return Frames(6);
            yield return Cap("3.3-stack-story", Model.Mode == CardStackMode.Open && Model.Face.kind == CardKind.Story, Model.Face?.headline);
            yield return Cap("3.1-hud-tools-hidden", Hud.ToolsHidden);
            int pages = Model.PageCount;
            var showBtn = Stack.Root.Q("cards-showme");
            if (Shown(showBtn))
            {
                yield return TapAt(GameUiPanel.ScreenCenter(showBtn));
                yield return new WaitForSecondsRealtime(1.6f);
                yield return Cap("3.3-showme-running", ShowMeDirector.Instance.Busy, ShowMeDirector.Status());
                yield return new WaitForSecondsRealtime(0.5f);
            }
            for (int i = 0; i < pages + 1 && Model.Mode == CardStackMode.Open && Model.Index < Model.PageCount - 1; i++) { Model.Next(); yield return Frames(3); }
            yield return Frames(4);
            yield return Cap("3.3-stack-last-page", Model.Mode == CardStackMode.Open, $"page {Model.Index + 1}/{Model.PageCount}");
            Model.SkipAll();
            yield return Frames(6);

            // Also…
            Sim.DebugClearCardsRead();
            GameClock.DebugAway(0.5);
            yield return Frames(6);
            CardStackView.DebugClearRead();
            CardStackView.DebugReturn(3);
            yield return Frames(4);
            Model.TapPeek();
            yield return Frames(4);
            for (int g = 0; g < 8 && !Model.OnAlsoPage && Model.Mode == CardStackMode.Open; g++) { Model.Next(); yield return Frames(3); }
            yield return Frames(4);
            yield return Cap("3.3-also-lines", Model.OnAlsoPage, $"also {Model.AlsoCount}");
            Model.SkipAll();
            yield return Frames(6);
            AssertNoErrors("return");
        }

        /// <summary>Every kind of card once: the box is old and full of stories, and each kind is forced where it can be.</summary>
        [UnityTest, Timeout(Long), Category("CapF")]
        public IEnumerator F_CardKinds()
        {
            yield return Grown(70f);
            var seen = new HashSet<string>();
            bool noShowMe = false;
            string log = "";
            Sim.State.shapingCharges = Sim.ShapingMaxCharges;
            var wish = Sim.DebugForceWish(K.Rain);
            Sim.DebugGiveRelic(RelicId.All[0]);
            Sim.ForceEraReady();
            for (int round = 0; round < 6; round++)
            {
                if (round == 0) CardStackView.DebugClearRead();
                CardStackView.DebugReturn(7);
                yield return Frames(4);
                if (!Model.TapPeek()) break;
                yield return Frames(4);
                log += $"round {round}: {Model.PageCount} pages, ";
                for (int p = 0; p < 12 && Model.Mode == CardStackMode.Open; p++)
                {
                    var f = Model.Face;
                    if (f == null || f.isAlso) break;
                    log += f.kind + (f.card != null && f.card.paysOffTeaser ? "+payoff" : "") + (f.teaserLine.Length > 0 ? "+teaser" : "") + (f.endLine.Length > 0 ? "+end" : "") + (f.canShowMe ? "" : "+noshow") + " ";
                    string name = f.kind switch
                    {
                        CardKind.Wish => "3.3-stack-wish", CardKind.EraReady => "3.3-stack-era", CardKind.Keeper => "3.3-stack-keeper",
                        CardKind.Relic => "3.3-stack-relic", CardKind.Legend => "3.3-stack-legend", _ => null,
                    };
                    if (name != null && seen.Add(name)) { yield return Frames(3); yield return Cap(name, true, f.headline); }
                    if (f.teaserLine.Length > 0 && seen.Add("payoff")) { yield return Frames(3); yield return Cap("3.3-stack-payoff", true, f.teaserLine + " / " + f.headline); }
                    if (f.endLine.Length > 0 && seen.Add("end")) { yield return Frames(3); yield return Cap("3.3-stack-endline", true, f.endLine); }
                    if (!f.canShowMe && !noShowMe) { noShowMe = true; yield return Frames(3); yield return Cap("3.3-showme-cannot", true, f.headline); }
                    if (Model.Index >= Model.PageCount - 1) break;
                    Model.Next();
                    yield return Frames(3);
                }
                Model.SkipAll();
                yield return Frames(6);
                if (round == 1 && wish != null && wish.state == WishState.Open) { Sim.State.shapingCharges = 0; }
                string t = Sim.DebugOpenTeaser();
                log += $"· teaser {t} · ";
                SimLayoutSource.DebugSkipYears(6f);
                Sim.CleanAll();
                yield return null;
            }
            File.AppendAllText(Manifest, "log\tcard-kinds\t\t" + log + "\n");
            AssertNoErrors("card kinds");
        }

        [UnityTest, Timeout(Long), Category("CapG")]
        public IEnumerator G_Recall()
        {
            yield return Grown(8f);
            var v = RecallPromptView.Instance;
            RecallPromptView.DebugReset();
            string r = RecallPromptView.DebugForce();
            yield return Frames(6);
            yield return Cap("3.3-recall-asked", v.IsOpen, r);
            yield return TapAt(GameUiPanel.ScreenCenter(v.Root.Q("recall-2")));
            yield return Frames(6);
            yield return Cap("3.3-recall-answered", !v.IsOpen);
            yield return new WaitForSecondsRealtime(3.2f);
            RecallPromptView.DebugReset();
            RecallPromptView.DebugForce();
            yield return Frames(6);
            yield return TapAt(GameUiPanel.ScreenCenter(v.Root.Q("recall-dismiss")));
            yield return Frames(6);
            yield return Cap("3.3-recall-not-now", !v.IsOpen);
            RecallPromptView.DebugReset();
            AssertNoErrors("recall");
        }

        // ================================================================== 3.4 person card, 3.2 markers and ties
        [UnityTest, Timeout(Long), Category("CapH")]
        public IEnumerator H_PersonCard()
        {
            yield return Grown(30f);
            var sim = Sim;
            var cam = Camera.main.GetComponent<BoxCamera>();
            int id = FirstAdult();
            var a = sim.FindNotable(id);
            var others = sim.State.notables.Where(n => n.alive && n.id != id && n.age >= SimCore.AdultAge).ToList();
            if (others.Count > 1)
            {
                if (sim.FindNotable(a.friendId)?.alive != true) { a.friendId = others[0].id; others[0].friendId = id; }
                if (sim.FindNotable(a.rivalId)?.alive != true && a.friendId != others[1].id) { a.rivalId = others[1].id; others[1].rivalId = id; }
            }
            Cards.Open(id, true);
            yield return new WaitForSecondsRealtime(1.3f);
            var root = Cards.CardRoot;
            yield return Cap("3.4-person-notable", Cards.IsOpen, Cards.Card.name);
            yield return Cap("3.2-marker-name-tag", Cards.IsOpen);
            var scroll = root.Q<ScrollView>("person-card-body");
            scroll.scrollOffset = new Vector2(0f, 99999f);
            yield return Frames(4);
            yield return Cap("3.4-person-recent", !Cards.ShowingBest);
            yield return Tap(root, "person-card-best");
            root.Q<ScrollView>("person-card-body").scrollOffset = new Vector2(0f, 99999f);
            yield return Frames(4);
            yield return Cap("3.4-person-best", Cards.ShowingBest);
            root.Q<ScrollView>("person-card-body").scrollOffset = Vector2.zero;
            yield return Tap(root, "person-card-recent");

            yield return Tap(root, "person-card-pin");
            yield return Frames(4);
            yield return Cap("3.4-person-pinned", root.Q<Button>("person-card-pin").text == "Pinned");
            Cards.Close();
            cam.FlyToPerson(PersonRef.Notable(id), 0.6f, 0.3f);
            yield return new WaitForSecondsRealtime(1f);
            yield return Cap("3.2-pin-tag-gold", Cards.PinTagShown);
            Cards.TogglePin();
            yield return Frames(3);

            Cards.Open(id, false);
            cam.FlyTo(Vector3.zero, 1f, 0.2f);
            yield return new WaitForSecondsRealtime(0.6f);
            yield return Until(() => Cards.LinesDrawn > 0, 2f);
            yield return Cap("3.2-tie-lines", Cards.LinesDrawn > 0, Cards.LinesDrawn + " lines");
            Cards.Close();
            yield return Frames(3);

            // No stories: the notable with the fewest.
            int blank = -1;
            foreach (var n in sim.State.notables.Where(n => n.alive).OrderBy(n => n.age))
            {
                Cards.Open(n.id, false);
                yield return null;
                if (Cards.Card.recentStories.Count == 0) { blank = n.id; break; }
            }
            if (blank >= 0)
            {
                Cards.Open(blank, true);
                yield return new WaitForSecondsRealtime(1.2f);
                root.Q<ScrollView>("person-card-body").scrollOffset = new Vector2(0f, 99999f);
                yield return Frames(4);
                yield return Cap("3.4-person-no-stories", true, Cards.Card.name);
            }
            else File.AppendAllText(Manifest, "log\tno-stories\t\tevery living notable has a story\n");
            Cards.Close();
            yield return Frames(3);

            yield return Years(60f);
            yield return Calm();
            var deadLegend = sim.State.notables.Where(n => !n.alive).OrderByDescending(n => n.id).FirstOrDefault(n => sim.PersonCardOf(n.id)?.memorial?.IsLegend == true);
            var dead = deadLegend ?? sim.State.notables.LastOrDefault(n => !n.alive);
            if (dead != null)
            {
                Cards.Open(dead.id, true);
                yield return new WaitForSecondsRealtime(1.2f);
                yield return Cap("3.4-person-legend", deadLegend != null && root.Q("person-card-memorial") != null, Cards.Card.name);
                Cards.Close();
            }
            AssertNoErrors("person card");
        }

        // ================================================================== 3.5 history
        [UnityTest, Timeout(Long), Category("CapI")]
        public IEnumerator I_TimeLapse()
        {
            yield return Grown(45f);
            var screen = TimeLapseScreenView.Ensure();
            Assert.IsTrue(screen.Open());
            yield return Until(() => screen.Model.Layout.Width > 0f, 2f);
            yield return Frames(6);
            var m = screen.Model;
            yield return Cap("3.5-timelapse-paused", !m.Playing, m.YearLabel);

            var bands = m.Lapse.EraBands();
            float era = bands.Count > 1 ? bands[1].startYear : 10f;
            yield return Drag(new[] { screen.ScreenPointAtBarX(m.Layout.XOf(m.Clock.End) - 2f), screen.ScreenPointAtBarX(m.Layout.XOf(Mathf.Max(0f, era - 1.5f))) }, 20f);
            yield return Frames(4);
            m.Play();
            var card = screen.Root.Q("tl-era-card");
            yield return Until(() => Shown(card), 10f);
            yield return Cap("3.5-eracard-shown", Shown(card), card.Q<Label>()?.text);
            yield return Cap("3.5-timelapse-playing", m.Playing, m.YearLabel);
            m.Pause();
            yield return Frames(3);

            var g = m.Layout.Groups.FirstOrDefault(x => x.pins.Any(p => p.notableId >= 0)) ?? m.Layout.Groups.First();
            int idx = Mathf.Max(0, g.pins.FindIndex(p => p.notableId >= 0));
            m.TapPin(g, idx);
            yield return Frames(6);
            yield return Cap("3.5-timelapse-at-pin", m.Caption != null, m.Caption?.Text);
            yield return Cap("3.5-caption-with-person", m.Caption != null && m.Caption.CanOpenPerson && Shown(screen.Root.Q("tl-cap-person")));
            var g2 = m.Layout.Groups.FirstOrDefault(x => x.pins.Any(p => p.notableId < 0));
            if (g2 != null)
            {
                m.TapPin(g2, g2.pins.FindIndex(p => p.notableId < 0));
                yield return Frames(6);
                yield return Cap("3.5-caption-no-person", m.Caption != null && !m.Caption.CanOpenPerson, m.Caption?.Text);
            }
            else File.AppendAllText(Manifest, "log\tcaption\t\tno pin without a person\n");

            string folderShots = TimeLapseSnapshot.Folder;
            var before = Directory.Exists(folderShots) ? Directory.GetFiles(folderShots, "*.png").ToList() : new List<string>();
            m.GoNow();
            yield return Frames(4);
            yield return TapAt(GameUiPanel.ScreenCenter(screen.Root.Q("tl-snapshot")));
            string fresh = null;
            yield return Until(() => { fresh = Directory.Exists(folderShots) ? Directory.GetFiles(folderShots, "*.png").FirstOrDefault(f => !before.Contains(f)) : null; return fresh != null; }, 6f);
            yield return Frames(4);
            yield return Cap("3.5-snapshot-toast", fresh != null, screen.Root.Q<Label>("tl-toast")?.text);
            if (fresh != null)
            {
                yield return new WaitForSecondsRealtime(0.3f);
                var tex = new Texture2D(2, 2);
                tex.LoadImage(File.ReadAllBytes(fresh));
                File.WriteAllBytes(Out + "3.5-snapshot-label.jpg", tex.EncodeToJPG(80));
                UnityEngine.Object.Destroy(tex);
                File.Delete(fresh);
                File.AppendAllText(Manifest, "ok\t3.5-snapshot-label\t\tfrom " + Path.GetFileName(fresh) + "\n");
            }
            yield return TapAt(GameUiPanel.ScreenCenter(screen.Root.Q("tl-close")));
            yield return Frames(4);
            AssertNoErrors("time-lapse");
        }

        [UnityTest, Timeout(Long), Category("CapJ")]
        public IEnumerator J_TimeLapseEmpty()
        {
            yield return OpenSlice(seed: Seed);
            DebugDock.ForceLocked = true;
            yield return Frames(10);
            var screen = TimeLapseScreenView.Ensure();
            bool opened = screen.Open();
            yield return Frames(10);
            yield return Cap("3.5-timelapse-empty", opened && Shown(screen.Root.Q("tl-empty")), "opened " + opened + ", year " + Sim.State.Year);
            if (opened) screen.Close();
            yield return Frames(4);
            AssertNoErrors("time-lapse empty");
        }

        // ================================================================== 3.6 the moments and the close
        [UnityTest, Timeout(Long), Category("CapK")]
        public IEnumerator K_EraMoment()
        {
            yield return Grown(6f);
            var sim = Sim;
            var mp = MomentPlayer.Instance;
            var ui = mp.GetComponent<UIDocument>().rootVisualElement;
            yield return Cap("3.6-watch-hidden", !Shown(ui.Q("moment-watch")));
            sim.State.grime = 0f;
            Assert.IsTrue(sim.ForceEraReady());
            MomentPlayer.DebugWaited(100);
            yield return new WaitForSecondsRealtime(0.8f);
            yield return Frames(20);
            yield return Cap("3.6-watch-ready", Shown(ui.Q("moment-watch")), mp.Glance);
            Hud.Toast("Tap the ground to place the Grove. Two fingers move the camera.", 2f);
            yield return Frames(4);
            yield return Cap("3.6-watch-with-toast", Shown(ui.Q("moment-watch")) && Shown(Hud.Root.Q("hud-toast")));
            yield return new WaitForSecondsRealtime(2.2f);

            sim.State.grime = 0.9f;
            mp.Watch();
            yield return Frames(6);
            var offer = ui.Q("moment-wipe");
            yield return Cap("3.6-watch-wipe-first", Shown(offer));
            yield return TapAt(GameUiPanel.ScreenCenter(offer.Q("moment-wipe-no")));
            yield return Frames(3);
            sim.State.grime = 0f;
            var hint = ui.Q("moment-hint"); var bubble = ui.Q("moment-bubble"); var caption = ui.Q("moment-caption");
            yield return Until(() => Shown(hint), 8f);
            yield return Frames(3);
            yield return Cap("3.6-moment-hint", Shown(hint));
            yield return Cap("3.1-hud-hidden", !Hud.IsVisible);
            MomentPlayer.DebugHurry(3f);
            MomentPlayer.DebugHoldHurry = true;
            yield return Until(() => Shown(bubble) || !mp.Playing, 60f);
            MomentPlayer.DebugHoldHurry = false;
            yield return Frames(3);
            yield return Cap("3.6-moment-bubble", Shown(bubble));
            MomentPlayer.DebugHoldHurry = true;
            yield return Until(() => Shown(caption) || !mp.Playing, 60f);
            MomentPlayer.DebugHoldHurry = false;
            yield return Frames(3);
            yield return Cap("3.6-moment-caption", Shown(caption), caption.Q<Label>()?.text ?? (caption as TextElement)?.text);
            MomentPlayer.DebugHoldHurry = true;
            yield return Until(() => !mp.Playing, 90f);
            MomentPlayer.DebugHoldHurry = false;
            yield return Frames(8);
            var ask = ui.Q("moment-notify");
            yield return Cap("3.6-notification-ask", Shown(ask));
            if (Shown(ask)) yield return TapAt(GameUiPanel.ScreenCenter(ask.Q("moment-notify-no")));
            yield return Frames(6);
            AssertNoErrors("era moment");
        }

        [UnityTest, Timeout(Long), Category("CapL")]
        public IEnumerator L_LastMomentAndShelf()
        {
            yield return Grown(60f);
            var sim = Sim;
            var clock = GameClock.Instance;
            var c = BoxCompleteController.Instance;
            var ui = c.GetComponent<UIDocument>().rootVisualElement;
            sim.State.settlementName = "Mossbarrow";
            yield return Cap("3.6-finish-hidden", !Shown(ui.Q("complete-prompt")));
            if (sim.State.era != Era.Town) { sim.ForceNextEra(); yield return Frames(3); }
            sim.ForcePeakReady();
            sim.CleanAll();
            yield return new WaitForSecondsRealtime(0.8f);
            yield return Frames(20);
            var prompt = ui.Q("complete-prompt");
            yield return Cap("3.6-finish-ready", Shown(prompt));
            yield return TapAt(GameUiPanel.ScreenCenter(prompt));
            Assert.IsTrue(c.Playing, "the last moment starts");
            var hint = ui.Q("boxcomplete-hint"); var bubble = ui.Q("boxcomplete-bubble"); var caption = ui.Q("boxcomplete-caption"); var roll = ui.Q("boxcomplete-roll");
            yield return Until(() => Shown(hint), 8f);
            yield return Frames(3);
            yield return Cap("3.6-last-hint", Shown(hint));
            c.Director.HurryFactor = 3f;
            bool gotBubble = false, gotCaption = false, gotRoll = false;
            MomentPlayer.DebugHoldHurry = true;
            float t = 0f;
            while (c.Playing && t < 200f && !(gotBubble && gotCaption && gotRoll))
            {
                t += Time.unscaledDeltaTime;
                if (!gotBubble && Shown(bubble)) { gotBubble = true; yield return Frames(2); yield return Cap("3.6-last-bubble", Shown(bubble), (bubble as TextElement)?.text); }
                if (!gotCaption && Shown(caption)) { gotCaption = true; yield return Frames(2); yield return Cap("3.6-last-caption", Shown(caption), (caption as TextElement)?.text); }
                if (!gotRoll && Shown(roll)) { gotRoll = true; yield return Frames(6); yield return Cap("3.6-last-roll-call", Shown(roll), (roll as TextElement)?.text); }
                yield return null;
            }
            yield return Until(() => !c.Playing, 200f);
            MomentPlayer.DebugHoldHurry = false;
            File.AppendAllText(Manifest, $"log\tlast\t\tbubble {gotBubble} caption {gotCaption} roll {gotRoll} committed {c.Committed} boxes {clock.Shelf.boxes.Count}\n");
            yield return Frames(30);
            yield return Cap("3.6-shelf-open-button", Shown(ui.Q("shelf-open")), "after the moment: box 2");
            yield return new WaitForSecondsRealtime(5.5f);
            c.OpenShelf();
            yield return Frames(30);
            yield return Cap("3.6-shelf-list", c.ShelfOpen);
            yield return Tap(ui, "shelf-box-0");
            yield return Frames(50);
            yield return Cap("3.6-shelf-visiting", clock.Visiting, (ui.Q("visit-plate") as TextElement)?.text);
            var props = UnityEngine.Object.FindAnyObjectByType<BoxView>().GetComponent<ShelfProps>();
            File.AppendAllText(Manifest, $"log\tplate\t\t{props?.PlateFull}\n");
            c.Back();
            yield return Frames(30);
            AssertNoErrors("last moment");
        }

        // ================================================================== 3.7 first run
        [UnityTest, Timeout(Long), Category("CapM")]
        public IEnumerator M_FirstRun()
        {
            yield return OpenSlice(seed: Seed, skipFirstRun: false);
            DebugDock.ForceLocked = true;
            var run = FirstRunView.Instance;
            var hud = Hud;
            var sim = Sim;
            yield return Until(() => run.Step == FirstRunStep.Wipe, 6f);
            yield return new WaitForSecondsRealtime(2f);
            yield return Cap("3.7-hints-wipe", run.ShownHintId == "wipe", run.ShownHintText);
            yield return Cap("3.7-skip-row", Shown(run.Root.Q("firstrun-skip-row")));
            hud.Open(GameHud.History);
            yield return Frames(6);
            yield return Cap("extra-history-plain-sheet", hud.OpenSheet == GameHud.History);
            hud.CloseSheet();
            yield return Frames(4);

            run.Run.DebugSkipTo(FirstRunStep.Patch);
            yield return new WaitForSecondsRealtime(2f);
            yield return Cap("3.7-hints-patch", run.Step == FirstRunStep.Patch, run.ShownHintText);
            run.Run.DebugSkipTo(FirstRunStep.Name);
            yield return new WaitForSecondsRealtime(1.8f);
            yield return Cap("3.2-plate-blank", Shown(run.Root.Q("firstrun-plate")), (run.Root.Q("firstrun-plate") as TextElement)?.text);
            yield return Tap(run.Root, "firstrun-plate");
            yield return new WaitForSecondsRealtime(0.4f);
            var field = run.Root.Q<TextField>("firstrun-name-field");
            yield return Cap("3.7-naming-suggested", run.NameSheetOpen, field.value);
            yield return Tap(run.Root, "firstrun-name-reroll");
            yield return Frames(3);
            yield return Cap("3.7-naming-another", run.NameSheetOpen, field.value);
            field.value = "shit hole";
            yield return Tap(run.Root, "firstrun-name-keep");
            yield return Frames(3);
            yield return Cap("3.7-naming-invalid", run.NameMessage.Length > 0, run.NameMessage);
            yield return Tap(run.Root, "firstrun-name-close");
            yield return Frames(4);
            yield return Cap("3.7-naming-later", !run.NameSheetOpen);
            yield return Tap(run.Root, "firstrun-plate");
            yield return new WaitForSecondsRealtime(0.4f);
            field.value = "Mossbarrow";
            yield return Tap(run.Root, "firstrun-name-keep");
            yield return Frames(6);
            yield return Cap("3.7-naming-keep", run.Step == FirstRunStep.Rain, sim.PlaceName);
            yield return Cap("3.2-plate-named", sim.PlaceName == "Mossbarrow");

            yield return new WaitForSecondsRealtime(2.2f);
            yield return Cap("3.7-firstrain-button", Shown(run.Root.Q("firstrun-rain")), run.ShownHintText);
            yield return Tap(run.Root, "firstrun-rain");
            yield return new WaitForSecondsRealtime(1f);
            yield return Cap("3.7-firstrain-armed", Sv.ShapeMode);
            var (rx, rz) = run.Run.SuggestedRainSpot;
            yield return TapAt(Sv.ScreenOf(rx, rz));
            yield return Frames(10);
            yield return new WaitForSecondsRealtime(4f);
            yield return Cap("3.7-firstyear-running", run.Step == FirstRunStep.Year, "");
            yield return Until(() => run.Step == FirstRunStep.Card, 35f);
            yield return Until(() => run.CardOpen, 4f);
            yield return new WaitForSecondsRealtime(0.8f);
            yield return Cap("3.7-firstcard-shown", run.CardOpen, (run.Root.Q("firstrun-card-also") as TextElement)?.text);
            if (Shown(run.Root.Q("firstrun-card-showme")))
            {
                yield return Tap(run.Root, "firstrun-card-showme");
                yield return new WaitForSecondsRealtime(3f);
                yield return Cap("3.7-firstcard-showme", true, ShowMeDirector.Status());
                yield return Until(() => run.CardOpen, 12f);
            }
            yield return Tap(run.Root, "firstrun-card-done");
            yield return Frames(20);
            yield return Cap("3.7-firstcard-done", !run.Active);
            AssertNoErrors("first run");
        }

        // ================================================================== 3.8 feedback
        [UnityTest, Timeout(Long), Category("CapN")]
        public IEnumerator N_Feedback()
        {
            string backup = FeedbackSender.OutboxDir + "~capture";
            if (Directory.Exists(backup)) Directory.Delete(backup, true);
            if (Directory.Exists(FeedbackSender.OutboxDir)) Directory.Move(FeedbackSender.OutboxDir, backup);
            yield return Grown(8f);
            var sender = FeedbackSender.Ensure();
            typeof(FeedbackSender).GetField("token", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(sender, ""); // never reaches GitHub
            var fb = FeedbackUI.Instance;
            yield return Tap(Hud.Root, "hud-feedback");
            yield return Until(() => fb.IsOpen, 3f);
            yield return Frames(6);
            yield return Cap("3.8-feedback-empty", fb.IsOpen);
            yield return Tap(fb.Root, "fb-kind-bug");
            fb.Root.Q<TextField>("fb-text").value = "The peek covers the second line of the toast when I come back.";
            yield return Frames(4);
            yield return Cap("3.8-feedback-filled", fb.Root.Q<Button>("fb-send").enabledSelf);
            yield return Tap(fb.Root, "fb-send");
            yield return Until(() => !fb.IsOpen, 3f);
            yield return Frames(6);
            var toast = fb.Root.Q<Label>("fb-toast");
            yield return Until(() => Shown(toast), 3f);
            yield return Cap("3.8-feedback-queued", !fb.IsOpen, toast.text);
            typeof(FeedbackUI).GetMethod("ShowToast", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(fb, new object[] { "Thanks! Sent to the team." });
            yield return Frames(4);
            yield return Cap("3.8-feedback-sent", Shown(toast), toast.text + " (the text forced: a test note must never reach GitHub)");
            if (Directory.Exists(FeedbackSender.OutboxDir)) Directory.Delete(FeedbackSender.OutboxDir, true);
            if (Directory.Exists(backup)) Directory.Move(backup, FeedbackSender.OutboxDir);
            AssertNoErrors("feedback");
        }

        // ================================================================== the small phone (375 x 667 pt)
        [UnityTest, Timeout(Long), Category("CapO")]
        public IEnumerator O_SmallPhone()
        {
            folder = "small/";
            yield return Grown(8f, 750, 1334);
            var hud = Hud;
            Sim.State.settlementName = "Mossbarrow";
            yield return Frames(3);
            yield return Cap("3.1-hud-default");

            int id = FirstAdult();
            Cards.Open(id, true);
            yield return new WaitForSecondsRealtime(1.3f);
            yield return Cap("3.4-person-notable", Cards.IsOpen);
            Cards.TogglePin();
            yield return Frames(3);
            Cards.Close();
            yield return Frames(4);
            Camera.main.GetComponent<BoxCamera>().FlyTo(Vector3.zero, 1f, 0.2f);
            yield return new WaitForSecondsRealtime(0.5f);
            CardStackView.DebugClearRead();
            CardStackView.DebugReturn(3);
            yield return Frames(3);
            Model.HidePeek();
            yield return Frames(4);
            yield return Cap("3.1-hud-pin-dot", Model.HistoryDot && hud.PinChipText != null);

            CardStackView.DebugClearRead();
            CardStackView.DebugReturn(5);
            yield return Frames(4);
            yield return Cap("3.3-peek-shown", Model.Mode == CardStackMode.Peek);
            Model.TapPeek();
            yield return Frames(6);
            yield return Cap("3.3-stack-story", Model.Mode == CardStackMode.Open, Model.Face?.kind.ToString());
            Model.SkipAll();
            yield return Frames(6);

            Sim.DebugRefillShaping();
            hud.Open(GameHud.Shaping);
            yield return Frames(8);
            yield return Cap("3.4-tray-tools", hud.OpenSheet == GameHud.Shaping);
            hud.CloseSheet();
            yield return Frames(4);

            yield return Years(37f);
            yield return Calm();
            var screen = TimeLapseScreenView.Ensure();
            Assert.IsTrue(screen.Open());
            yield return Until(() => screen.Model.Layout.Width > 0f, 2f);
            yield return Frames(6);
            yield return Cap("3.5-timelapse-paused", !screen.Model.Playing);
            var m = screen.Model;
            var g = m.Layout.Groups.FirstOrDefault(x => x.pins.Any(p => p.notableId >= 0));
            if (g != null)
            {
                m.TapPin(g, g.pins.FindIndex(p => p.notableId >= 0));
                yield return Frames(6);
                yield return Cap("3.5-timelapse-at-pin", m.Caption != null);
            }
            yield return TapAt(GameUiPanel.ScreenCenter(screen.Root.Q("tl-close")));
            yield return Frames(6);

            var sim = Sim;
            var mp = MomentPlayer.Instance;
            var ui = mp.GetComponent<UIDocument>().rootVisualElement;
            sim.CleanAll();
            sim.State.grime = 0f;
            sim.ForceEraReady();
            MomentPlayer.DebugWaited(100);
            yield return new WaitForSecondsRealtime(0.8f);
            yield return Frames(20);
            hud.Toast("A toast long enough to wrap to a second line, to show that Watch stays in its own lane.", 2f);
            yield return Frames(4);
            yield return Cap("3.6-watch-with-toast", Shown(ui.Q("moment-watch")) && Shown(hud.Root.Q("hud-toast")));
            AssertNoErrors("small phone");
        }
    }
}
