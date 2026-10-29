using System;
using UnityEngine;

namespace AquariumCiv.Prototypes
{
    public enum TestMode { Lab, Diary }

    /// <summary>
    /// Per-scene prototype identity and A/B variant assignment.
    /// Lab: each tester gets a counterbalanced order and advances to the next variant explicitly.
    /// Diary: the variant alternates by calendar day since first launch.
    /// </summary>
    [DefaultExecutionOrder(-200)]
    public class PrototypeConfig : MonoBehaviour
    {
        public string protoId = "P0";
        public string[] variants = { "A", "B" };
        public TestMode testMode = TestMode.Lab;
        [TextArea] public string surveyQ4 = "Cleaning felt satisfying";

        public static PrototypeConfig Current { get; private set; }
        public event Action<string> VariantChanged;

        public string Variant => variants.Length == 0 ? "-" : variants[VariantIndex % variants.Length];

        string IndexKey => $"variant_idx_{protoId}";
        string OverrideKey => $"variant_override_{protoId}";
        string ModeKey => $"test_mode_{protoId}";

        void Awake()
        {
            Current = this;
            if (PlayerPrefs.HasKey(ModeKey)) testMode = (TestMode)PlayerPrefs.GetInt(ModeKey);
        }

        /// <summary>Switch lab/diary on this device (persists), e.g. a phone that joins a lab session.</summary>
        public void SetTestMode(TestMode mode)
        {
            testMode = mode;
            PlayerPrefs.SetInt(ModeKey, (int)mode);
            PlayerPrefs.Save();
            VariantChanged?.Invoke(Variant);
        }

        int VariantIndex
        {
            get
            {
                if (PlayerPrefs.HasKey(OverrideKey)) return PlayerPrefs.GetInt(OverrideKey);
                int order = Tester.Id[0] % 2; // counterbalance: half the testers start on B (stable across runs)
                int step = testMode == TestMode.Lab ? PlayerPrefs.GetInt(IndexKey, 0) : Tester.DaysSinceFirstLaunch;
                return (step + order) % Math.Max(1, variants.Length);
            }
        }

        /// <summary>Lab protocol: move this tester to their next variant.</summary>
        public void NextVariant()
        {
            int n = Math.Max(1, variants.Length);
            int next = (VariantIndex + 1) % n; // from what's shown, even if a debug override forced it
            PlayerPrefs.DeleteKey(OverrideKey);
            int order = Tester.Id[0] % 2;
            int step = PlayerPrefs.GetInt(IndexKey, 0) + 1;
            while ((step + order) % n != next) step++;
            PlayerPrefs.SetInt(IndexKey, step);
            PlayerPrefs.Save();
            VariantChanged?.Invoke(Variant);
        }

        /// <summary>Debug: force a variant regardless of schedule.</summary>
        public void ForceVariant(int index)
        {
            PlayerPrefs.SetInt(OverrideKey, index);
            PlayerPrefs.Save();
            VariantChanged?.Invoke(Variant);
        }
    }

    /// <summary>Anonymous tester identity: random GUID + self-chosen nickname. No personal data.</summary>
    public static class Tester
    {
        public static string Id
        {
            get
            {
                if (!PlayerPrefs.HasKey("tester_id")) { PlayerPrefs.SetString("tester_id", Guid.NewGuid().ToString("N")); PlayerPrefs.Save(); }
                return PlayerPrefs.GetString("tester_id");
            }
        }

        public static string Nickname
        {
            get => PlayerPrefs.GetString("tester_nick", "");
            set { PlayerPrefs.SetString("tester_nick", value); PlayerPrefs.Save(); }
        }

        public static int DaysSinceFirstLaunch
        {
            get
            {
                if (!PlayerPrefs.HasKey("first_launch_day"))
                {
                    PlayerPrefs.SetString("first_launch_day", DateTime.Now.Date.ToString("yyyy-MM-dd"));
                    PlayerPrefs.Save();
                }
                var first = DateTime.ParseExact(PlayerPrefs.GetString("first_launch_day"), "yyyy-MM-dd", null);
                return (int)(DateTime.Now.Date - first).TotalDays;
            }
        }
    }
}
