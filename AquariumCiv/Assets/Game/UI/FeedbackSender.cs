using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using AquariumCiv.Game;
using UnityEngine;
using UnityEngine.Networking;

namespace AquariumCiv.GameUI
{
    /// <summary>
    /// Sends a feedback note from the slice as a GitHub issue in the game's repo (same token, labels and issue layout as
    /// the prototypes: docs/feedback.md). Every note is written to <c>persistentDataPath/feedback_outbox</c> first
    /// (text as the issue payload, screenshot as a .jpg beside it), then sent; it leaves the outbox only once GitHub
    /// accepted it. Offline, without a token, or killed mid-send, it simply goes out on a later launch or resume.
    /// The screenshot is committed to the <see cref="assetBranch"/> branch (Contents API) and linked from the issue;
    /// if that is refused the (small) image is embedded in the issue instead, so it is never lost.
    /// </summary>
    [DefaultExecutionOrder(-140)]
    public class FeedbackSender : MonoBehaviour
    {
        public string repo = "TommasoRibaudo/aquarium-civ";
        public string assetBranch = "feedback-assets";
        /// <summary>A second note inside this window is refused (a stuck finger must not flood the board).</summary>
        public float minSecondsBetween = 4f;

        public static FeedbackSender Instance { get; private set; }
        /// <summary>After each attempt: (reached GitHub now, message for the player).</summary>
        public event Action<bool, string> Result;

        string token = "", buildInfo = "local";
        bool flushing;
        float lastSubmit = -100f;

        public static string OutboxDir => Path.Combine(Application.persistentDataPath, "feedback_outbox");
        public bool HasToken => token.Length > 0;
        public int Pending => Directory.Exists(OutboxDir) ? Directory.GetFiles(OutboxDir, "*.json").Length : 0;

        /// <summary>Make sure a sender exists (the HUD and the debug panel both need one).</summary>
        public static FeedbackSender Ensure()
        {
            if (Instance != null) return Instance;
            var go = new GameObject("FeedbackSender");
            DontDestroyOnLoad(go);
            return go.AddComponent<FeedbackSender>();
        }

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            token = Resources.Load<TextAsset>("FeedbackToken")?.text.Trim() ?? "";
            buildInfo = Resources.Load<TextAsset>("BuildInfo")?.text.Trim() ?? "local";
#if UNITY_EDITOR
            // Editor testing: a token in UserSettings/ (gitignored, never built into the app).
            var local = Path.Combine(Application.dataPath, "..", "UserSettings", "FeedbackToken.txt");
            if (token.Length == 0 && File.Exists(local)) token = File.ReadAllText(local).Trim();
#endif
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        IEnumerator Start()
        {
            yield return new WaitForSecondsRealtime(3f); // let the session start first
            yield return Flush(false);
        }

        void OnApplicationPause(bool pause) { if (!pause && Instance == this && isActiveAndEnabled) StartCoroutine(Flush(false)); }

        // ---------- submitting ----------

        /// <summary>The numbers a reader needs to place the note in the box.</summary>
        public static List<(string key, string value)> Context(string buildInfo)
        {
            var rows = new List<(string, string)>();
            var sim = GameClock.Instance != null ? GameClock.Instance.Sim : null;
            if (sim != null)
            {
                var s = sim.State;
                rows.Add(("Box", string.Format(CultureInfo.InvariantCulture, "{0} · year {1:0.0} · pop {2} · {3} lots · mess {4:0.00}",
                    s.era, s.Year, s.population, sim.Settlement?.Lots.Count ?? 0, sim.MessLevel)));
            }
            else rows.Add(("Box", "no live sim"));
            rows.Add(("Session", string.Format(CultureInfo.InvariantCulture, "{0:0.0} min since launch", Time.realtimeSinceStartup / 60f)));
            rows.AddRange(DeviceInfo.Rows(buildInfo));
            return rows;
        }

        public static string EraAndYear()
        {
            var sim = GameClock.Instance != null ? GameClock.Instance.Sim : null;
            return sim == null ? "" : string.Format(CultureInfo.InvariantCulture, "{0}, y{1:0}", sim.State.era, sim.State.Year);
        }

        /// <summary>Tester id: random, per install, stored locally. Not a name, not a device id.</summary>
        public static string TesterId()
        {
            const string key = "tester_id";
            var id = PlayerPrefs.GetString(key, "");
            if (id.Length != 8)
            {
                id = Guid.NewGuid().ToString("N").Substring(0, 8);
                PlayerPrefs.SetString(key, id);
            }
            return id;
        }

        /// <summary>The event log's summary (opens, D1/D3/D7, cleans, cards...) for the report; null when there is no log.</summary>
        static string TelemetryJson()
        {
            var log = GameClock.Tel;
            if (log == null) return null;
            try { return TelemetryAnalyzer.Analyze(log.ReadAllLines(), GameClock.TelemetryNow).ToJson(); }
            catch (Exception) { return null; }
        }

        /// <summary>Queues the note (and its screenshot, if any) and tries to send it. Returns false when it was refused
        /// (empty text, or a note a moment ago).</summary>
        public bool Submit(FeedbackKind kind, string text, byte[] jpg)
        {
            text = (text ?? "").Trim();
            if (text.Length == 0 || Time.realtimeSinceStartup - lastSubmit < minSecondsBetween) return false;
            if (text.Length > FeedbackReport.MaxTextChars) text = text.Substring(0, FeedbackReport.MaxTextChars);
            lastSubmit = Time.realtimeSinceStartup;

            var body = FeedbackReport.Body(kind, text, jpg != null, TesterId(), Context(buildInfo), LogRing.Last(FeedbackReport.MaxLogLines),
                TelemetryJson(), GameClock.Tel?.RecentLines(40));
            var payload = FeedbackReport.PayloadJson(FeedbackReport.Title(kind, text, EraAndYear()), body, FeedbackReport.Labels(kind));
            var stamp = $"{DateTime.UtcNow:yyyyMMddHHmmssfff}";
            try
            {
                Directory.CreateDirectory(OutboxDir);
                if (jpg != null) File.WriteAllBytes(Path.Combine(OutboxDir, stamp + ".jpg"), jpg);
                File.WriteAllText(Path.Combine(OutboxDir, stamp + ".json"), payload); // last: the .json is what marks it ready
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Feedback] could not save the note: {e.Message}");
                Result?.Invoke(false, "Sorry, that note could not be saved.");
                return true;
            }
            Debug.Log($"[Feedback] queued {kind} note ({text.Length} chars, screenshot {(jpg != null ? jpg.Length / 1024 + " KB" : "none")})");
            StartCoroutine(Flush(true));
            return true;
        }

        /// <summary>Debug panel cheat: a test note with the current screen, to check the whole path end to end.</summary>
        public static string DebugSendTest()
        {
            var s = Ensure();
            s.lastSubmit = -100f;
            s.StartCoroutine(s.CaptureThenSubmit(FeedbackKind.Idea, "[debug] test note from the debug panel. Safe to close."));
            return s.HasToken ? "test note capturing, then sending" : "test note capturing; no token here, it will wait in the outbox";
        }

        IEnumerator CaptureThenSubmit(FeedbackKind kind, string text)
        {
            yield return new WaitForEndOfFrame();
            Submit(kind, text, ScreenShot.Jpg(ScreenCapture.CaptureScreenshotAsTexture()));
        }

        // ---------- sending ----------

        IEnumerator Flush(bool report)
        {
            while (flushing) yield return null; // one send at a time; a note made meanwhile goes out right after
            flushing = true;
            bool anySent = false, offline = false;
            if (Directory.Exists(OutboxDir))
            {
                var files = Directory.GetFiles(OutboxDir, "*.json");
                Array.Sort(files, StringComparer.Ordinal);
                foreach (var file in files)
                {
                    string payload;
                    try { payload = File.ReadAllText(file); }
                    catch { continue; }
                    string jpgPath = Path.ChangeExtension(file, ".jpg");
                    byte[] jpg = null;
                    try { if (File.Exists(jpgPath)) jpg = File.ReadAllBytes(jpgPath); } catch { /* sent without the picture */ }

                    long code = 0;
                    yield return SendOne(payload, jpg, Path.GetFileNameWithoutExtension(file), c => code = c);
                    if (code == 201)
                    {
                        anySent = true;
                        TryDelete(file); TryDelete(jpgPath);
                    }
                    else if (code == 422 || code == 400) // GitHub will never take this one: set it aside, keep going
                    {
                        Debug.LogWarning($"[Feedback] note {Path.GetFileName(file)} was rejected ({code}); set aside");
                        Reject(file); Reject(jpgPath);
                    }
                    else { offline = true; break; } // no network, no token, rate limit: leave the rest for later
                }
            }
            flushing = false;
            if (report)
                Result?.Invoke(anySent && !offline, anySent && !offline ? "Thanks! Sent to the team." : "Thanks! Saved, it will send when you're online.");
        }

        IEnumerator SendOne(string payload, byte[] jpg, string stamp, Action<long> done)
        {
            if (token.Length == 0) { done(0); yield break; }
            if (payload.Contains(FeedbackReport.ScreenshotMarker))
            {
                string md = "_(no screenshot)_";
                if (jpg != null)
                {
                    string path = $"feedback/{stamp}-{TesterId()}.jpg";
                    string b64 = Convert.ToBase64String(jpg);
                    long up = 0;
                    var json = $"{{\"message\":\"feedback screenshot {stamp}\",\"content\":\"{b64}\",\"branch\":\"{assetBranch}\"}}";
                    yield return Request("PUT", $"https://api.github.com/repos/{repo}/contents/{path}", json, c => up = c);
                    if (up == 201 || up == 200)
                        md = $"![screenshot](https://github.com/{repo}/blob/{assetBranch}/{path}?raw=true)";
                    else
                    {
                        Debug.LogWarning($"[Feedback] screenshot upload refused ({up}); embedding it in the issue");
                        md = b64.Length <= 40000
                            ? $"<details><summary>Screenshot (base64 jpg, decode to view)</summary>\n\n```\n{b64}\n```\n</details>"
                            : "_(screenshot too big to embed; upload refused)_";
                    }
                }
                payload = payload.Replace(FeedbackReport.ScreenshotMarker, FeedbackReport.Escape(md));
            }
            long code = 0;
            yield return Request("POST", $"https://api.github.com/repos/{repo}/issues", payload, c => code = c);
            done(code);
        }

        IEnumerator Request(string method, string url, string json, Action<long> done)
        {
            using var req = new UnityWebRequest(url, method)
            {
                uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json)),
                downloadHandler = new DownloadHandlerBuffer(),
                timeout = 30,
            };
            req.SetRequestHeader("Authorization", "Bearer " + token);
            req.SetRequestHeader("Accept", "application/vnd.github+json");
            req.SetRequestHeader("X-GitHub-Api-Version", "2022-11-28");
            req.SetRequestHeader("Content-Type", "application/json");
            yield return req.SendWebRequest();
            if (req.result != UnityWebRequest.Result.Success)
                Debug.LogWarning($"[Feedback] {method} {url.Substring(url.IndexOf("/repos/", StringComparison.Ordinal))} failed: {req.responseCode} {req.error}");
            done(req.result == UnityWebRequest.Result.Success || req.responseCode > 0 ? req.responseCode : 0);
        }

        static void TryDelete(string path) { try { if (File.Exists(path)) File.Delete(path); } catch { /* retried as a duplicate at worst */ } }

        static void Reject(string path)
        {
            try
            {
                if (!File.Exists(path)) return;
                var dir = Path.Combine(Application.persistentDataPath, "feedback_rejected");
                Directory.CreateDirectory(dir);
                File.Move(path, Path.Combine(dir, Path.GetFileName(path)));
            }
            catch { /* keep it where it is */ }
        }
    }

    /// <summary>Shrinks a captured screen to a phone-friendly JPG (the issue only needs to show what the tester saw).</summary>
    public static class ScreenShot
    {
        public const int MaxSide = 960, Quality = 62;

        /// <summary>Takes ownership of <paramref name="tex"/> (destroyed). Null in, null out.</summary>
        public static byte[] Jpg(Texture2D tex)
        {
            if (tex == null) return null;
            try
            {
                float k = Mathf.Min(1f, MaxSide / (float)Mathf.Max(tex.width, tex.height));
                int w = Mathf.Max(1, Mathf.RoundToInt(tex.width * k)), h = Mathf.Max(1, Mathf.RoundToInt(tex.height * k));
                var rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32);
                Graphics.Blit(tex, rt);
                var prev = RenderTexture.active;
                RenderTexture.active = rt;
                var small = new Texture2D(w, h, TextureFormat.RGB24, false);
                small.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                small.Apply();
                RenderTexture.active = prev;
                RenderTexture.ReleaseTemporary(rt);
                var bytes = small.EncodeToJPG(Quality);
                Destroy(small);
                return bytes;
            }
            finally { Destroy(tex); }
        }

        static void Destroy(UnityEngine.Object o)
        {
            if (Application.isPlaying) UnityEngine.Object.Destroy(o); else UnityEngine.Object.DestroyImmediate(o);
        }
    }
}
