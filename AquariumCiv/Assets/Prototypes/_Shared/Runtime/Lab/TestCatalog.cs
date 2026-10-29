using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AquariumCiv.Prototypes
{
    /// <summary>
    /// What each prototype tests and what its two variants are, in plain words. The Test Hub draws its cards from this
    /// and every lab prototype's top strip says "P3 · A: Chronicle cards (vs B: plain return)" from it, so a tester
    /// always knows what they're playing and what it's compared with. Data only: a missing scene just isn't offered.
    /// </summary>
    public static class TestCatalog
    {
        public class Entry
        {
            public string id, scene, title, hypothesis, gate;
            public string[] names, lines; // per variant: short name for buttons, one line on what it does
            public int diaryDays;         // 0 = lab only
            public string diaryNote;
        }

        public static readonly Entry[] All =
        {
            new()
            {
                id = "P2", scene = "P2_ReturnToMess", title = "Return to Mess", gate = "Gate 1",
                hypothesis = "Coming back to a messy tank pulls you in, not guilt.",
                names = new[] { "Cosmetic mess", "Mess slows work" },
                lines = new[] { "Mess only looks bad.", "Mess dims the light and slows work a little." },
                diaryDays = 4,
            },
            new()
            {
                id = "P3", scene = "P3_WhileYouWereAway", title = "While You Were Away", gate = "Gate 2",
                hypothesis = "Story cards about named people make coming back the best part.",
                names = new[] { "Chronicle cards", "Plain return" },
                lines = new[] { "On return, 3-7 story cards with faces and \"show me\".", "On return, the changed village and one stats line." },
                diaryDays = 4, diaryNote = "Variant alternates by day.",
            },
            new()
            {
                id = "P4", scene = "P4_BookOfAges", title = "Book of Ages", gate = "Gate 2",
                hypothesis = "Browsing the village's 100-year history makes you linger and want to share it.",
                names = new[] { "Time-lapse", "Illustrated book" },
                lines = new[] { "Drag a timeline to replay the village growing.", "Turn pages: chapters, portraits, family tree." },
            },
            new()
            {
                id = "P5", scene = "P5_KeepersNudge", title = "Keeper's Nudge", gate = "Gate 3",
                hypothesis = "Gentle nudges make you feel you shaped the story, without micromanaging.",
                names = new[] { "Omens", "Shape the land" },
                lines = new[] { "Place a totem or shrine; the people lean that way.", "Rain, plant a grove, dig a channel; the people adapt." },
                diaryDays = 3, diaryNote = "Variant alternates by day.",
            },
            new()
            {
                id = "P6", scene = "P6_ErasAndShelf", title = "Eras & Shelf", gate = "Gate 3",
                hypothesis = "Watching a new era arrive live is worth waiting for.",
                names = new[] { "The box grows", "New box on the shelf" },
                lines = new[] { "At the Village era, an annex box joins the tank.", "At the Village era, a new box starts beside the old one." },
            },
        };

        public static Entry Find(string id)
        {
            foreach (var e in All) if (e.id == id) return e;
            return null;
        }

        public static string Letter(int variant) => variant == 0 ? "A" : "B";
    }

    /// <summary>The hub's request to the next prototype scene: which variant, which mode. Lives for one app run.</summary>
    public static class LabLaunch
    {
        public class Request
        {
            public string proto;
            public int variant = -1;     // -1 = keep the schedule
            public TestMode mode;
            public bool modeChanged;     // filled in by PrototypeConfig
        }

        static Request pending;
        /// <summary>The request this scene was opened with (after PrototypeConfig consumed it), or null.</summary>
        public static Request Active { get; private set; }
        /// <summary>This app run went through the hub at least once (lab strips and pair questions only then).</summary>
        public static bool ViaHub { get; private set; }

        public static void Open(string proto, int variant, TestMode mode)
        {
            var e = TestCatalog.Find(proto);
            if (e == null) return;
            pending = new Request { proto = proto, variant = variant, mode = mode };
            ViaHub = true;
            LabSession.MarkPlayed(proto, variant, mode);
            SceneManager.LoadScene(e.scene);
        }

        /// <summary>Called by PrototypeConfig.Awake: the request meant for this prototype, once.</summary>
        public static Request Consume(string proto)
        {
            if (pending == null || pending.proto != proto) { Active = null; return null; }
            Active = pending;
            pending = null;
            return Active;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { pending = null; Active = null; ViaHub = false; LabSession.Reset(); HubLink.Reset(); }
    }

    /// <summary>What was played and surveyed this app run (hub ticks, the A/B pair question).</summary>
    public static class LabSession
    {
        static readonly HashSet<string> played = new(), surveyed = new(), paired = new();

        public static void MarkPlayed(string proto, int variant, TestMode mode)
        {
            if (variant >= 0) played.Add($"{proto}{TestCatalog.Letter(variant)}{(mode == TestMode.Diary ? "d" : "")}");
        }
        public static bool WasPlayed(string proto, int variant) => played.Contains($"{proto}{TestCatalog.Letter(variant)}");
        public static void MarkSurveyed(string proto, string variant) => surveyed.Add(proto + variant);
        public static bool BothSurveyed(string proto) => surveyed.Contains(proto + "A") && surveyed.Contains(proto + "B");
        public static bool Paired(string proto) => paired.Contains(proto);
        public static void MarkPaired(string proto) => paired.Add(proto);
        internal static void Reset() { played.Clear(); surveyed.Clear(); paired.Clear(); }
    }

    /// <summary>Getting back to the Test Hub from any prototype.</summary>
    public static class HubLink
    {
        public const string HubScene = "Hub";
        static bool? available;
        /// <summary>The hub is in this build (it isn't in P2-only builds).</summary>
        public static bool Available => available ??= Application.CanStreamedLevelBeLoaded(HubScene);
        /// <summary>The hub's cold-launch redirect (straight into a running diary) already happened this run.</summary>
        public static bool LaunchHandled;

        public static void GoToHub()
        {
            EventLog.Instance?.Log("hub", ("action", "open"));
            EventLog.Instance?.EndSession("hub");
            if (SimClock.Instance != null) SimClock.Instance.Save();
            LaunchHandled = true; // coming back on purpose: don't bounce into the diary again
            SceneManager.LoadScene(HubScene);
        }

        /// <summary>Does this prototype have a saved world on this device (a diary in progress)?</summary>
        public static bool HasSave(string proto) => File.Exists(Path.Combine(Application.persistentDataPath, "proto", $"{proto}.json"));

        internal static void Reset() { available = null; LaunchHandled = false; }
    }
}
