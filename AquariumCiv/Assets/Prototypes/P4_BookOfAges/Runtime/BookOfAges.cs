using System.Collections;
using System.IO;
using AquariumCiv.Prototypes.Sim;
using UnityEngine;

namespace AquariumCiv.Prototypes.P4
{
    /// <summary>
    /// P4 Book of Ages (prototype-plan.md §5): is browsing the civ's history a reason to linger and share? One variable,
    /// the history view:
    ///   A "time-lapse": drag a timeline and the village rewinds and regrows in the glass, with story pins.
    ///   B "illustrated book": chapters, portraits of everyone who ever lived, family trees.
    /// Both: the P3 sim and stories, seeded with 100 game years, a History button, a Snapshot button, plain returns.
    /// </summary>
    public class BookOfAges : PrototypeBase, IDebugCheats
    {
        public TimelapseView timelapse;
        public IllustratedBook book;

        IHistoryView View => Cfg.Variant == "A" ? timelapse : book;
        IHistoryView Other => Cfg.Variant == "A" ? (IHistoryView)book : timelapse;
        bool AnyOpen => timelapse.IsOpen || book.IsOpen;
        bool capturing;
        float lastHintAt = -999;

        protected override void Awake()
        {
            base.Awake();
            timelapse.Snapshot += Shot;
            book.Snapshot += Shot;
        }

        protected override void ApplyConfig(SimConfig cfg)
        {
            base.ApplyConfig(cfg);
            cfg.storyEvents = true;
            cfg.recordYears = true;
        }

        protected override void Update()
        {
            base.Update();
            if (hud != null) hud.hidden = AnyOpen || NotableCardPanel.IsOpen;
            if (notables != null) notables.showMarkers = !book.IsOpen;
        }

        protected override void OnVariantStart(bool switched)
        {
            timelapse.Close();
            book.Close();
            lastHintAt = Time.unscaledTime;
            string what = Cfg.Variant == "A" ? "replay it" : "read its story";
            if (Lab) HudToast.ShowBanner($"Lab: this village is {Mathf.FloorToInt(clock.Sim.State.Year)} years old. Tap \"History\" to {what}.", 8f);
            else if (!PlayerPrefs.HasKey("p4_hinted")) { PlayerPrefs.SetInt("p4_hinted", 1); wipe.ShowHint(); }
        }

        protected override void OnReturn(double hours, float years, long sinceMonth)
        {
            if (AnyOpen) { timelapse.Close(); book.Close(); }
            HudToast.ShowBanner($"Welcome back, Keeper! {StatsLine(hours, years)}", 6f);
        }

        void OpenHistory(string how)
        {
            NotableCardPanel.Instance?.Close("history");
            View.Open();
            float sinceHint = Time.unscaledTime - lastHintAt;
            EventLog.Instance?.Log("history", ("action", "open"), ("view", Cfg.Variant == "A" ? "timelapse" : "book"), ("how", how),
                ("since_hint_s", sinceHint), ("mode", Cfg.testMode.ToString()));
            EventLog.Instance?.Count("history_opens");
        }

        /// <summary>Snapshot: save what's on screen (the view included) in the app's folder, and say so.</summary>
        void Shot(string view)
        {
            if (!capturing) StartCoroutine(Capture(view));
        }

        IEnumerator Capture(string view)
        {
            capturing = true;
            var dir = Path.Combine(Application.persistentDataPath, "snapshots");
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, $"{PrototypeConfig.Current.protoId}_{view}_{System.DateTime.Now:yyyyMMdd_HHmmss}.png");
            ScreenCapture.CaptureScreenshot(path);
            yield return new WaitForEndOfFrame();
            yield return null;
            Juice.Chime();
            HudToast.ShowBanner("Snapshot saved. (Sharing to Photos comes in a later build.)", 3f);
            if (book.IsOpen) book.Flash("Snapshot saved.");
            EventLog.Instance?.Log("history", ("action", "snapshot"), ("view", view));
            EventLog.Instance?.Count("snapshots");
            capturing = false;
        }

        void OnGUI()
        {
            if (!started || AnyOpen || !SurveyPanel.IsPlaying || NotableCardPanel.IsOpen || FeedbackPanel.IsOpen) return;
            ProtoGui.Begin();
            // History sits between Feedback and Done, in the thumb zone.
            float w = ProtoGui.Width, h = ProtoGui.Height;
            var r = new Rect(w / 2 - 110 + 10, h - 112 - ProtoGui.SafeBottom, 220 - 20, 88);
            ProtoGui.Block(r);
            if (GUI.Button(r, "History", ProtoGui.Primary)) OpenHistory("button");
        }

        public void DrawCheats()
        {
            GUILayout.Label("<b>P4</b>  history");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Open history", GUILayout.Height(54))) OpenHistory("debug");
            if (GUILayout.Button($"Open {(Cfg.Variant == "A" ? "book (B)" : "time-lapse (A)")}", GUILayout.Height(54))) Other.Open();
            if (GUILayout.Button("Story now", GUILayout.Height(54))) clock.Sim.ForceStory();
            GUILayout.EndHorizontal();
            DrawTimeCheats();
        }
    }
}
