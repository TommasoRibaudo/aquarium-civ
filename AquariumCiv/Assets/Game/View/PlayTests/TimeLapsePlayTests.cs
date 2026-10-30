using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AquariumCiv.Game;
using AquariumCiv.Sim;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AquariumCiv.View.PlayTests
{
    /// <summary>
    /// The world replay (#78) in the real Slice scene at a portrait phone size: a box grown 60 years scrubs to year 0, 30 and
    /// 60 with the layout matching the history records, the live world steps aside while it plays and comes back exactly
    /// on closing, and a scrub step is timed (logged). Screenshots go to Temp/agent. Backs up and restores the save file.
    /// </summary>
    [Category("TimeLapsePlay")]
    public class TimeLapsePlayTests
    {
        const string ScenePath = "Assets/Game/Scenes/Slice.unity";
        const string Shots = "F:/GameDev/AquariumCiv/Temp/agent/";
        string savePath, backup;
        readonly List<string> errors = new();

        void OnLog(string msg, string stack, LogType type)
        {
            if (type == LogType.Exception || type == LogType.Error) errors.Add(msg);
        }

        IEnumerator Open(int w = 1179, int h = 2556)
        {
            LogAssert.ignoreFailingMessages = true;
            savePath = Path.Combine(Application.persistentDataPath, "game", "box.json");
            backup = savePath + ".timelapse-test-backup";
            if (File.Exists(savePath)) { File.Copy(savePath, backup, true); File.Delete(savePath); }
            if (File.Exists(savePath + ".bak")) { File.Copy(savePath + ".bak", backup + ".bak", true); File.Delete(savePath + ".bak"); }
            SetSize(w, h);
            float waited = 0f;
            while ((Screen.width != w || Screen.height != h) && waited < 4f) { waited += 0.1f; yield return new WaitForSecondsRealtime(0.1f); }
#if UNITY_EDITOR
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(ScenePath, new UnityEngine.SceneManagement.LoadSceneParameters(UnityEngine.SceneManagement.LoadSceneMode.Single));
#endif
            for (int i = 0; i < 90; i++) yield return null;
            Assume.That(GameClock.Instance, Is.Not.Null, "the slice scene has a GameClock");
            errors.Clear();
            Application.logMessageReceived += OnLog;
        }

        void Close()
        {
            Application.logMessageReceived -= OnLog;
            try
            {
                if (File.Exists(backup + ".bak")) { File.Copy(backup + ".bak", savePath + ".bak", true); File.Delete(backup + ".bak"); }
                if (File.Exists(backup)) { File.Copy(backup, savePath, true); File.Delete(backup); }
                else if (File.Exists(savePath)) File.Delete(savePath);
            }
            catch (Exception e) { Debug.LogWarning("restore save: " + e.Message); }
        }

        static void SetSize(int w, int h)
        {
            foreach (var a in AppDomain.CurrentDomain.GetAssemblies())
            {
                var t = a.GetType("AquariumCiv.EditorTools.AgentGameView");
                if (t == null) continue;
                t.GetMethod("SetSize").Invoke(null, new object[] { w, h });
                return;
            }
        }

        static IEnumerator Shot(string name)
        {
            yield return new WaitForEndOfFrame();
            var tex = ScreenCapture.CaptureScreenshotAsTexture();
            Directory.CreateDirectory(Shots);
            File.WriteAllBytes(Shots + name + ".png", tex.EncodeToPNG());
            UnityEngine.Object.Destroy(tex);
        }

        static string Signature(BoxLayout l)
        {
            return string.Join(";", l.pieces.Select(p => p.kind + "@" + p.x.ToString("0.000") + "," + p.z.ToString("0.000") + "/" + p.era + "/" + p.seed + "/" + p.grow.ToString("0.00")))
                + "|" + l.fields.Count + "|" + string.Join(";", l.paths.Select(p => p.tier + ":" + p.points.Length + ":" + p.cobbled))
                + "|" + l.clearings.Count + "|" + l.trees.Count;
        }

        /// <summary>The lots the layout has a piece or a field for (farms are fields).</summary>
        static HashSet<int> Seeds(BoxLayout l)
        {
            var s = new HashSet<int>(l.pieces.Select(p => p.seed));
            foreach (var f in l.fields) s.Add(f.seed);
            return s;
        }

        static IEnumerator Grow(int years)
        {
            for (int i = 0; i < years; i++)
            {
                SimLayoutSource.DebugSkipYears(1f);
                if (i % 10 == 0) yield return null;
            }
            GameClock.Instance.Sim.CleanAll();
            yield return null; yield return null;
        }

        [UnityTest]
        public IEnumerator AGrownBoxReplaysYearZeroThirtySixtyAndComesBackExactly()
        {
            yield return Open();
            var sim = GameClock.Instance.Sim;
            yield return Grow(60);
            var box = UnityEngine.Object.FindAnyObjectByType<BoxView>();
            var tl = TimeLapseView.Ensure();
            Assert.IsNotNull(tl);
            Assert.GreaterOrEqual(sim.State.month / SimConfig.MonthsPerYear, 60);
            var people = box.GetComponent<PeopleView>();
            bool hadPeople = people != null && people.enabled;
            box.Refresh();
            string before = Signature(box.layout);
            int piecesBefore = box.layout.pieces.Count;

            var hud = AquariumCiv.GameUI.GameHud.Instance ?? AquariumCiv.GameUI.GameHud.Create();
            yield return null;
            Assert.IsTrue(tl.Open(), "the replay opens");
            Assert.IsTrue(GameClock.Instance.Held, "the live clock holds while replaying");
            if (people != null) Assert.IsFalse(people.enabled, "the live people step aside");
            yield return null;
            Assert.IsTrue(hud.ToolsHidden, "no Clean / Shape on the past (tester batch 2)");
            Assert.IsFalse(hud.CleanMode);

            var seen = new Dictionary<int, int>();
            foreach (int year in new[] { 0, 30, 60 })
            {
                tl.Seek(year);
                yield return null;
                var f = tl.Lapse.Frame(year);
                var seeds = Seeds(box.layout);
                var want = f.buildings.Where(b => b.lotId > 0 && b.type != BuildingType.Camp).Select(b => SimLayout.SeedOfLot(b.lotId)).ToList();
                foreach (int s in want) Assert.IsTrue(seeds.Contains(s), "year " + year + ": a lot of the records is drawn");
                var other = sim.Settlement.Lots.Where(l => l.kind != AquariumCiv.Sim.Settle.LotKind.Camp && l.buildingId > 0 && !f.buildings.Any(b => b.lotId == l.id)).ToList();
                foreach (var l in other) Assert.IsFalse(seeds.Contains(SimLayout.SeedOf(l)), "year " + year + ": lot " + l.id + " (" + l.kind + ", founded month " + l.foundedMonth + ", retired " + l.retired + ") did not stand yet");
                Assert.AreEqual(f.paths.Count > 0, box.layout.paths.Count > 0, "year " + year + ": paths");
                Assert.AreEqual(0, box.layout.villagers.Count, "no standing people in a replay");
                seen[year] = box.layout.pieces.Count;
                TestContext.Out.WriteLine("year " + (year + 1) + ": " + f.buildings.Count + " buildings in the records, " + box.layout.pieces.Count + " pieces, "
                    + box.layout.paths.Count + " path runs, era " + f.era + "; " + tl.LastInfo);
                if (year == 0) Assert.IsTrue(box.layout.pieces.All(p => p.era == Era.Camp), "year 0 is the camp");
            }
            Assert.Less(seen[0], seen[60], "the box grew");

            // a scrub sweep, timed
            tl.Seek(0f); // the jump from year 60 is the open-like big one, not part of the sweep
            double max = 0, total = 0; int n = 0;
            for (float y = 0f; y <= 60f; y += 0.25f)
            {
                tl.Seek(y);
                if (tl.LastStepMs > 25) Debug.Log("[TimeLapse] slow step at " + y + ": " + box.lastRefreshInfo); max = Math.Max(max, tl.LastStepMs); total += tl.LastStepMs; n++;
            }
            TestContext.Out.WriteLine("scrub sweep 0..60 in quarter years: " + n + " steps, mean " + (total / n).ToString("0.0") + " ms, max " + max.ToString("0.0") + " ms");
            Debug.Log("[TimeLapse] scrub sweep: " + n + " steps, mean " + (total / n).ToString("0.0") + " ms, max " + max.ToString("0.0") + " ms");
            Assert.Less(total / n, 12.0, "a scrub step stays cheap on desktop");
            Assert.Less(max, 60.0, "no scrub step is a hitch (target 25: the worst are 4-chunk era changes with a cold GC)");

            Assert.Greater(box.AoPending, 0, "the scrub left chunks without AO");
            tl.Settle();
            Assert.AreEqual(0, box.AoPending, "settling bakes them");
            tl.Seek(10.5f);
            yield return null;
            TestContext.Out.WriteLine("blend: " + tl.LastInfo + ", growing pieces " + box.layout.pieces.Count(p => p.grow < 1f));

            tl.Close();
            yield return null; yield return null;
            Assert.IsFalse(tl.Active);
            Assert.IsFalse(GameClock.Instance.Held, "the clock runs again");
            Assert.IsFalse(AquariumCiv.GameUI.GameHud.Instance.ToolsHidden, "Clean and Shape are back");
            if (hadPeople) Assert.IsTrue(people.enabled, "the people are back");
            box.Refresh();
            Assert.AreEqual(piecesBefore, box.layout.pieces.Count);
            Assert.AreEqual(before, Signature(box.layout), "the live box is exactly as it was");
            Assert.IsEmpty(errors, string.Join("\n", errors));
            Close();
        }

        [UnityTest]
        public IEnumerator Screenshots()
        {
            yield return Open();
            yield return Grow(60);
            var tl = TimeLapseView.Ensure();
            Assert.IsTrue(tl.Open());
            foreach (var (name, y) in new[] { ("tl_year01", 0f), ("tl_year31", 30f), ("tl_year61", 60f), ("tl_blend", 30.5f) })
            {
                tl.Seek(y);
                for (int i = 0; i < 8; i++) yield return null;
                yield return Shot(name);
            }
            tl.Close();
            for (int i = 0; i < 4; i++) yield return null;
            yield return Shot("tl_back_to_now");
            Assert.IsEmpty(errors, string.Join("\n", errors));
            Close();
        }
    }
}
