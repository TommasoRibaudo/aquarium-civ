using System.Collections;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using AquariumCiv.Game;
using AquariumCiv.GameUI;
using AquariumCiv.Sim;
using UnityEngine;
using UnityEngine.UIElements;

namespace AquariumCiv.View
{
    /// <summary>
    /// #636: one quick question per session for the first outside playtest (<see cref="PulseQuestions"/> decides which and when). A
    /// <see cref="GlassSheet"/> on the bottom edge, no scrim, at a calm moment (the same calm rule as the recall question, #328). The
    /// screen is captured just before the sheet opens. An answer is logged (<c>pulse_answer</c>, never the typed words) and sent as an
    /// <c>fb:pulse</c> issue through the Feedback outbox with the full package: context, screenshot, session stats, recent events, the
    /// game log, and the box's people and latest stories (to check a recall answer). While it is on, the old recall question stays quiet.
    /// Stable names for tests: <c>pulse-sheet</c>, <c>pulse-answer-0</c>.., <c>pulse-dismiss</c>, <c>pulse-text</c>, <c>pulse-send</c>.
    /// </summary>
    public class PulseQuestionView : MonoBehaviour
    {
        public static PulseQuestionView Instance { get; private set; }
        public static bool AutoCreate = true;
        /// <summary>The questions run (tests of the old recall question switch it off).</summary>
        public static bool On = true;
        /// <summary>True while this view is the one asking: the recall question waits.</summary>
        public static bool Active => On && Instance != null && Instance.isActiveAndEnabled;

        public float checkSeconds = 2f;
        public PulseState State { get; private set; } = new PulseState();
        public bool IsOpen => sheet != null && sheet.IsOpen;
        public PulseDef Asking { get; private set; }
        public VisualElement Root => root;

        UIDocument doc;
        VisualElement root, answers, textStep;
        GlassSheet sheet;
        GlassCard card;
        TextField field;
        Rect lastSafe;
        Vector2Int lastScreen;
        float nextCheck, ridden;
        bool loaded, capturing;
        byte[] shot;
        int chosen = -1;
        int askedDay, askedSession;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (!AutoCreate || Instance != null || FindAnyObjectByType<GameClock>() == null) return;
            Create();
        }

        public static PulseQuestionView Create() => new GameObject("PulseQuestionView").AddComponent<PulseQuestionView>();

        void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
            doc = gameObject.AddComponent<UIDocument>();
            doc.panelSettings = GameUiPanel.Settings;
            doc.sortingOrder = 90; // with the recall question: over the HUD shell, under the Feedback sheet
        }

        void OnEnable()
        {
            if (Instance != this) return;
            Build(doc.rootVisualElement);
            UiInput.Add(IsOverUi);
        }

        void OnDisable() { if (Instance == this) UiInput.Remove(IsOverUi); }
        void OnDestroy() { if (Instance == this) Instance = null; }
        bool IsOverUi(Vector2 p) => IsOpen && GameUiPanel.Over(root, p);

        // ---------------- state ----------------
        string StatePath => GameClock.Instance?.TelemetryDir == null ? null : Path.Combine(GameClock.Instance.TelemetryDir, "pulse.json");

        void LoadState()
        {
            loaded = true;
            try { var p = StatePath; if (p != null && File.Exists(p)) State = JsonUtility.FromJson<PulseState>(File.ReadAllText(p)) ?? new PulseState(); }
            catch (System.Exception) { State = new PulseState(); }
        }

        void SaveState()
        {
            try { var p = StatePath; if (p != null) File.WriteAllText(p, JsonUtility.ToJson(State)); }
            catch (System.Exception e) { Debug.LogWarning("[Pulse] could not save state: " + e.Message); }
        }

        // ---------------- when ----------------
        static SimCore Sim => GameClock.Instance != null ? GameClock.Instance.ActiveSim : null;

        /// <summary>A choice was raised or shown in this visit (or one is waiting): one ask per visit.</summary>
        static bool ChoiceThisVisit()
        {
            var sim = Sim;
            if (sim == null) return false;
            if (sim.OpenDilemma != null) return true;
            int v = sim.State.visitCount;
            return sim.State.dilemmas != null && sim.State.dilemmas.Any(d => d.visitNo == v);
        }

        static bool FirstRunDone()
        {
            var fr = FirstRunView.Instance;
            return fr == null || fr.Step == FirstRunStep.Done;
        }

        public PulseContext Context()
        {
            var clock = GameClock.Instance;
            int session = State.See(GameClock.Tel?.SessionId);
            var recall = RecallPromptView.Instance;
            return new PulseContext
            {
                dayIndex = RecallPromptView.DayIndex(),
                session = session,
                sessionSeconds = clock != null ? clock.SessionSeconds : 0,
                awayHours = clock != null ? clock.SessionAwayHours : 0,
                firstRunDone = FirstRunDone(),
                choiceThisVisit = ChoiceThisVisit(),
                busy = IsOpen || capturing || RecallPromptView.Busy() || recall != null && (recall.IsOpen || !recall.CalmNow()),
            };
        }

        void Update()
        {
            if (Instance != this) return;
            if (sheet != null && (Screen.safeArea != lastSafe || Screen.width != lastScreen.x || Screen.height != lastScreen.y)) ApplySafeArea();
            if (IsOpen) RideKeyboard();
            if (!On || Time.unscaledTime < nextCheck || GameClock.Tel == null) return;
            nextCheck = Time.unscaledTime + checkSeconds;
            if (!loaded) LoadState();
            if (IsOpen || capturing) return;
            var c = Context();
            var def = PulseQuestions.Next(State, c);
            if (def != null) Ask(def, c);
        }

        // ---------------- the sheet ----------------
        void Ask(PulseDef def, PulseContext c)
        {
            if (sheet == null || IsOpen || capturing) return;
            PulseQuestions.Shown(State, def, c);
            SaveState();
            askedDay = c.dayIndex; askedSession = c.session;
            GameClock.Tel?.Log("pulse_shown", "q", def.id, "day", c.dayIndex, "session", c.session, "round", State.Of(def.id).roundsDone);
            StartCoroutine(OpenRoutine(def));
        }

        IEnumerator OpenRoutine(PulseDef def)
        {
            capturing = true;
            root.style.display = DisplayStyle.None;
            yield return null;
            yield return new WaitForEndOfFrame();
            try { shot = ScreenShot.Jpg(ScreenCapture.CaptureScreenshotAsTexture()); } catch (System.Exception) { shot = null; } // what the player saw when asked
            capturing = false;
            Asking = def;
            chosen = -1;
            Fill(def);
            root.style.display = DisplayStyle.Flex;
            sheet.Open();
            GameHud.Instance?.SetOutsideSheet(sheet, true);
        }

        void Close()
        {
            field?.Blur();
            var hud = GameHud.Instance;
            if (hud != null && hud.OutsideSheet == sheet) hud.SetOutsideSheet(null);
            sheet.style.translate = StyleKeyword.Null;
            ridden = 0f;
            sheet.Close(() => { if (!sheet.IsOpen) root.style.display = DisplayStyle.None; });
            Asking = null;
        }

        void Fill(PulseDef def)
        {
            card.Set("A quick question", def.question);
            answers.Clear();
            for (int i = 0; i < def.answers.Length; i++)
            {
                int n = i;
                var b = new GlassButton(def.answers[i], () => Pick(n), ButtonKind.Secondary, UiRole.Confirm) { name = "pulse-answer-" + i };
                b.AddToClassList("pulse-answer");
                answers.Add(b);
            }
            answers.style.display = DisplayStyle.Flex;
            textStep.style.display = DisplayStyle.None;
            field.value = "";
        }

        void Pick(int i)
        {
            var def = Asking;
            if (def == null) return;
            chosen = i;
            if (def.textAlways || def.textOn == i)
            {
                // one line more: the name, what happened, or why
                card.Set("A quick question", def.textAlways ? $"{def.answers[i]}. Want to say why? (optional)" : TextAsk(def));
                answers.style.display = DisplayStyle.None;
                textStep.style.display = DisplayStyle.Flex;
                field.Focus();
                return;
            }
            Send("");
        }

        static string TextAsk(PulseDef d) => d.id == "name" ? "Who? Type the name as you remember it." : d.id == "event" ? "What happened? A few words is plenty." : "Tell us in a few words.";

        void Send(string text)
        {
            var def = Asking;
            if (def == null || chosen < 0) return;
            int i = chosen;
            string answer = def.answers[i];
            PulseQuestions.Answered(State, def, askedDay);
            SaveState();
            GameClock.Tel?.Log("pulse_answer", "q", def.id, "a", i, "day", askedDay, "session", askedSession, "typed", string.IsNullOrWhiteSpace(text) ? 0 : 1);
            GameClock.Tel?.Flush();
            var sender = FeedbackSender.Ensure();
            sender.SubmitPulse(def, answer, text, askedDay, askedSession, shot, BoxFacts());
            shot = null;
            Close();
            if (def.feedbackOn == i && FeedbackUI.Instance != null) FeedbackUI.Instance.OpenAs(FeedbackKind.Confusing);
            else GameHud.Instance?.Toast("Thank you. That helps a lot.", 2.5f);
        }

        void Dismiss()
        {
            var def = Asking;
            if (def != null)
            {
                PulseQuestions.Dismissed(State, def);
                SaveState();
                GameClock.Tel?.Log("pulse_dismissed", "q", def.id, "day", askedDay, "session", askedSession);
            }
            shot = null;
            Close();
        }

        /// <summary>For checking a recall answer: the people living and gone, and the latest stories, as the box has them now.</summary>
        public static string BoxFacts()
        {
            var sim = Sim;
            if (sim == null) return "";
            var b = new StringBuilder("### The box now (to check the answer)\n\n");
            var alive = sim.State.notables.Where(n => n.alive).Select(n => n.name).Take(40).ToList();
            var gone = sim.State.notables.Where(n => !n.alive).OrderByDescending(n => n.diedMonth).Select(n => n.name).Take(20).ToList();
            b.Append("**Living:** ").Append(alive.Count == 0 ? "-" : string.Join(", ", alive)).Append('\n');
            b.Append("**Gone:** ").Append(gone.Count == 0 ? "-" : string.Join(", ", gone)).Append("\n\n");
            b.Append("**Latest stories** (newest first):\n\n");
            foreach (var e in sim.State.chronicle.Where(x => x.kind != "dilemma").OrderByDescending(x => x.month).ThenByDescending(x => x.id).Take(15))
                b.Append("- Y").Append((e.month / SimConfig.MonthsPerYear).ToString(CultureInfo.InvariantCulture)).Append(" · ").Append(e.kind).Append(": ")
                 .Append((e.text ?? "").Replace("\n", " ")).Append('\n');
            return b.ToString();
        }

        void ApplySafeArea()
        {
            lastSafe = Screen.safeArea;
            lastScreen = new Vector2Int(Screen.width, Screen.height);
            var i = GameUiPanel.SafeInsets(lastSafe, Screen.width, Screen.height);
            sheet.SetInsets(i, GlassSheet.IsShort(Screen.width, Screen.height) ? GlassSheet.ShortBottomPad : GlassSheet.BottomPad);
        }

        /// <summary>The text step rides the phone's keyboard, so the field and Send stay in sight.</summary>
        void RideKeyboard()
        {
            float h = textStep != null && textStep.style.display == DisplayStyle.Flex ? GlassKeyboard.Height() : 0f;
            if (Mathf.Abs(h - ridden) < 0.5f) return;
            ridden = h;
            sheet.style.translate = h > 0f ? new Translate(0, -h) : (StyleTranslate)StyleKeyword.Null;
        }

        void Build(VisualElement parent)
        {
            parent.Clear();
            root = new VisualElement { name = "pulse-root", pickingMode = PickingMode.Ignore };
            root.AddToClassList("hud-fill");
            root.style.display = DisplayStyle.None;
            parent.Add(root);

            sheet = new GlassSheet { name = "pulse-sheet" };
            sheet.style.display = DisplayStyle.None;
            root.Add(sheet);

            card = new GlassCard { name = "pulse-card" };
            card.Dots.style.display = DisplayStyle.None;
            card.Head.Add(new GlassButton("Not now", Dismiss, ButtonKind.Quiet, UiRole.Close) { name = "pulse-dismiss" });
            card.Actions.name = "pulse-actions";
            card.Actions.style.flexDirection = FlexDirection.Column;

            answers = new VisualElement { name = "pulse-answers", pickingMode = PickingMode.Ignore };
            answers.style.flexDirection = FlexDirection.Row;
            answers.style.flexWrap = Wrap.Wrap;
            answers.style.flexGrow = 1;
            card.Actions.Add(answers);

            textStep = new VisualElement { name = "pulse-text-step", pickingMode = PickingMode.Ignore };
            textStep.style.flexGrow = 1;
            field = new TextField { name = "pulse-text", maxLength = 200 };
            field.textEdition.placeholder = "Type here...";
            field.AddToClassList("kg-field");
            textStep.Add(field);
            var acts = new VisualElement();
            acts.AddToClassList("kg-card-acts");
            acts.Add(new GlassButton("Skip", () => Send(""), ButtonKind.Secondary, UiRole.Confirm) { name = "pulse-skip" });
            acts.Add(new GlassButton("Send", () => Send(field.value ?? ""), ButtonKind.Primary, UiRole.Confirm) { name = "pulse-send" });
            textStep.Add(acts);
            textStep.style.display = DisplayStyle.None;
            card.Actions.Add(textStep);

            sheet.Add(card);
            ApplySafeArea();
        }

        // ---------------- debug (the dbg panel, `script eval`) ----------------
        /// <summary>Asks question <paramref name="id"/> now, whatever the day, the session and the calm say (it still logs and saves).</summary>
        public static string DebugAsk(string id)
        {
            var v = Instance;
            if (v == null) return "no PulseQuestionView";
            var def = PulseQuestions.Find(id);
            if (def == null) return "no question " + id;
            if (v.IsOpen) v.Close();
            if (!v.loaded) v.LoadState();
            var c = v.Context();
            v.Ask(def, c);
            return $"asking '{id}' (day {c.dayIndex}, session {c.session})";
        }

        public static string DebugReset()
        {
            var v = Instance;
            if (v == null) return "no PulseQuestionView";
            v.State = new PulseState(); v.loaded = true; v.SaveState();
            if (v.IsOpen) v.Close();
            return "quick questions reset";
        }

        public static string Status()
        {
            var v = Instance;
            if (v == null) return "no PulseQuestionView";
            if (!v.loaded) v.LoadState();
            var c = v.Context();
            var next = PulseQuestions.Next(v.State, c);
            return string.Format(CultureInfo.InvariantCulture, "pulse: day {0} · session {1} · {2} · next: {3} · done: {4}",
                c.dayIndex, c.session, v.IsOpen ? "OPEN " + v.Asking?.id : "closed", next?.id ?? (c.busy ? "(busy)" : !c.firstRunDone ? "(first run)" : c.choiceThisVisit ? "(choice this visit)" : "-"),
                string.Join(" ", v.State.records.Where(r => r.roundsDone > 0).Select(r => r.id + "x" + r.roundsDone)));
        }
    }
}
