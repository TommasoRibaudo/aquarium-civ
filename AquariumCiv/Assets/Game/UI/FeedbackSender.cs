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
    /// The screenshot goes in the issue itself as base64, shrunk to fit (#153): the token stays Issues-only, it ships in the build.
    /// A note whose send may have got through (the app paused after GitHub took it, before the file went: #155, #171) is looked for
    /// by its note id before it's sent again.
    /// </summary>
    [DefaultExecutionOrder(-140)]
    public class FeedbackSender : MonoBehaviour
    {
        public string repo = "TommasoRibaudo/aquarium-civ";
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
            lastSubmit = Time.realtimeSinceStartup;

            var stamp = $"{DateTime.UtcNow:yyyyMMddHHmmssfff}-{TesterId()}";
            var stats = TelemetryJson();
            var events = GameClock.Tel?.RecentLines(40);
            var log = LogRing.Last(FeedbackReport.MaxLogLines);
            var ctx = Context(buildInfo);
            string Build(FeedbackReport.Fit f) => FeedbackReport.Body(kind, f.text, f.screenshot, TesterId(), ctx, log.GetRange(log.Count - f.logLines, f.logLines),
                f.stats ? stats : null, f.events ? events : null) + FeedbackReport.NoteIdLine(stamp);
            // #619: the tester's words win; the log, the events, the stats and the picture give way first (FitNote)
            var fit = FeedbackReport.FitNote(text, log.Count, jpg != null, jpg != null ? ScreenShot.Base64Chars(jpg.Length) : 0, Build);
            text = fit.text;
            string body = Build(fit);
            if (!fit.screenshot) jpg = null;
            else if (ScreenShot.Base64Chars(jpg.Length) > fit.screenshotRoom) jpg = ScreenShot.Shrink(jpg, fit.screenshotRoom / 4 * 3);
            if (fit.textCut) Debug.LogWarning($"[Feedback] note too long even without the log and picture: {text.Length} chars kept");
            var payload = FeedbackReport.PayloadJson(FeedbackReport.Title(kind, text, EraAndYear()), body, FeedbackReport.Labels(kind));
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

        /// <summary>#636: queues a quick question's answer as an <c>fb:pulse</c> issue, with the same package as a note (context, the
        /// screenshot taken when the question opened, session stats, recent events, the game log) plus <paramref name="boxFacts"/>.
        /// No refusal for an empty text or a quick second answer: an answer is always kept. Returns the outbox file's stamp.</summary>
        public string SubmitPulse(PulseDef def, string answer, string typed, int day, int session, byte[] jpg, string boxFacts, bool debug = false)
        {
            typed = (typed ?? "").Trim();
            var stamp = $"{DateTime.UtcNow:yyyyMMddHHmmssfff}-{TesterId()}-pulse";
            var stats = TelemetryJson();
            var events = GameClock.Tel?.RecentLines(40);
            var log = LogRing.Last(FeedbackReport.MaxLogLines);
            var ctx = Context(buildInfo);
            string Build(FeedbackReport.Fit f) => FeedbackReport.PulseBody(def.id, def.question, answer, f.text, day, session, f.screenshot, TesterId(), ctx,
                log.GetRange(log.Count - f.logLines, f.logLines), f.stats ? stats : null, f.events ? events : null, boxFacts) + FeedbackReport.NoteIdLine(stamp);
            var fit = FeedbackReport.FitNote(typed, log.Count, jpg != null, jpg != null ? ScreenShot.Base64Chars(jpg.Length) : 0, Build);
            string body = Build(fit);
            if (!fit.screenshot) jpg = null;
            else if (ScreenShot.Base64Chars(jpg.Length) > fit.screenshotRoom) jpg = ScreenShot.Shrink(jpg, fit.screenshotRoom / 4 * 3);
            var title = PulseQuestions.Title(def, answer, day, session, typed);
            if (debug) { title = title.Replace("[Slice] Pulse", "[Slice] [debug] Pulse"); body = "_Asked from the debug panel: not a real answer._\n\n" + body; }
            var payload = FeedbackReport.PayloadJson(title, body, PulseQuestions.Labels);
            try
            {
                Directory.CreateDirectory(OutboxDir);
                if (jpg != null) File.WriteAllBytes(Path.Combine(OutboxDir, stamp + ".jpg"), jpg);
                File.WriteAllText(Path.Combine(OutboxDir, stamp + ".json"), payload);
            }
            catch (Exception e) { Debug.LogWarning($"[Feedback] could not save the pulse answer: {e.Message}"); return null; }
            Debug.Log($"[Feedback] queued pulse '{def.id}' = {answer} (day {day}, session {session}, screenshot {(jpg != null ? jpg.Length / 1024 + " KB" : "none")})");
            StartCoroutine(Flush(false));
            return stamp;
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
            string sentMark = Path.Combine(OutboxDir, stamp + ".sending");
            if (File.Exists(sentMark))
            {
                // A send began and never heard back: did it land? Look before sending it twice.
                long look = 0;
                string found = null;
                yield return Request("GET", $"https://api.github.com/repos/{repo}/issues?labels=feedback&state=all&sort=created&direction=desc&per_page=30", null, c => look = c, b => found = b);
                if (look != 200) { done(0); yield break; } // can't tell: try again later
                if (FeedbackReport.AlreadyFiled(found, stamp))
                {
                    Debug.Log($"[Feedback] note {stamp} had already landed");
                    TryDelete(sentMark);
                    done(201);
                    yield break;
                }
            }
            if (payload.Contains(FeedbackReport.ScreenshotMarker))
                payload = payload.Replace(FeedbackReport.ScreenshotMarker, FeedbackReport.Escape(FeedbackReport.ScreenshotBlock(jpg != null ? Convert.ToBase64String(jpg) : null)));
            try { File.WriteAllText(sentMark, ""); } catch { /* at worst a duplicate */ }
            long code = 0;
            yield return Request("POST", $"https://api.github.com/repos/{repo}/issues", payload, c => code = c);
            if (code == 201 || code == 422 || code == 400) TryDelete(sentMark);
            done(code);
        }

        IEnumerator Request(string method, string url, string json, Action<long> done, Action<string> body = null)
        {
            using var req = new UnityWebRequest(url, method)
            {
                uploadHandler = json != null ? new UploadHandlerRaw(Encoding.UTF8.GetBytes(json)) : null,
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
            body?.Invoke(req.downloadHandler?.text);
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
        /// <summary>The picture's share of an issue: base64 chars, so the bytes it may take (#153). The rest of the 65,536 is the note and its logs.</summary>
        public const int MaxBase64Chars = 40000, MaxBytes = MaxBase64Chars / 4 * 3;
        /// <summary>The steps down until it fits: the long side, then the quality at each.</summary>
        static readonly int[] Sides = { 640, 520, 420, 340, 260 };
        static readonly int[] Qualities = { 55, 38 };

        public static int Base64Chars(int bytes) => (bytes + 2) / 3 * 4;

        /// <summary>Takes ownership of <paramref name="tex"/> (destroyed). Null in, null out. The largest step within <paramref name="maxBytes"/>,
        /// or the smallest there is.</summary>
        public static byte[] Jpg(Texture2D tex, int maxBytes = MaxBytes)
        {
            if (tex == null) return null;
            try
            {
                byte[] bytes = null;
                foreach (int side in Sides)
                {
                    var small = Scaled(tex, side);
                    foreach (int q in Qualities)
                    {
                        bytes = small.EncodeToJPG(q);
                        if (bytes.Length <= maxBytes) { Destroy(small); return bytes; }
                    }
                    Destroy(small);
                }
                return bytes;
            }
            finally { Destroy(tex); }
        }

        /// <summary>A box average on the CPU, bytes in and bytes out. Never through a shader: a screen capture's texture is read as linear in this
        /// Linear project and a loaded JPG as sRGB, so a blit that suits one adds or drops a gamma curve on the other (tester run 7: 0.766 came
        /// out 0.881, one encode too many).</summary>
        static Texture2D Scaled(Texture2D tex, int side)
        {
            float k = Mathf.Min(1f, side / (float)Mathf.Max(tex.width, tex.height));
            int w = Mathf.Max(1, Mathf.RoundToInt(tex.width * k)), h = Mathf.Max(1, Mathf.RoundToInt(tex.height * k));
            var from = tex.GetPixels32();
            var to = new Color32[w * h];
            for (int y = 0; y < h; y++)
            {
                int y0 = y * tex.height / h, y1 = Mathf.Max(y0 + 1, (y + 1) * tex.height / h);
                for (int x = 0; x < w; x++)
                {
                    int x0 = x * tex.width / w, x1 = Mathf.Max(x0 + 1, (x + 1) * tex.width / w);
                    int r = 0, g = 0, b = 0, n = (x1 - x0) * (y1 - y0);
                    for (int j = y0; j < y1; j++)
                    {
                        int row = j * tex.width;
                        for (int i = x0; i < x1; i++) { var c = from[row + i]; r += c.r; g += c.g; b += c.b; }
                    }
                    to[y * w + x] = new Color32((byte)(r / n), (byte)(g / n), (byte)(b / n), 255);
                }
            }
            var small = new Texture2D(w, h, TextureFormat.RGB24, false);
            small.SetPixels32(to);
            small.Apply();
            return small;
        }

        /// <summary>A JPG made smaller still, for a note whose logs left less room; null if it still won't fit.</summary>
        public static byte[] Shrink(byte[] jpg, int maxBytes)
        {
            if (jpg == null || maxBytes <= 0) return null;
            var tex = new Texture2D(2, 2);
            if (!tex.LoadImage(jpg)) { Destroy(tex); return null; }
            var bytes = Jpg(tex, maxBytes);
            return bytes != null && bytes.Length <= maxBytes ? bytes : null;
        }

        static void Destroy(UnityEngine.Object o)
        {
            if (Application.isPlaying) UnityEngine.Object.Destroy(o); else UnityEngine.Object.DestroyImmediate(o);
        }
    }
}
