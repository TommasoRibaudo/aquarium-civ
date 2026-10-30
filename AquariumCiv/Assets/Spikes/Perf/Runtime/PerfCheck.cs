using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using AquariumCiv.Prototypes;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace AquariumCiv.Spikes.Perf
{
    /// <summary>
    /// Device perf check of the style kit (issue #35): shows one box at a time (Growth Lab town at year 110, the style
    /// kit's camp and village), orbits in a slow sweep, and draws fps, CPU and GPU frame ms (FrameTimingManager), render counters
    /// and the device. Tap outside the panel = next scene. "Log" writes one CSV row a second to
    /// persistentDataPath/perf/; "Send" posts the per-scene summary through the feedback path (GitHub issue, or the
    /// outbox when offline). The boxes generate themselves when their GameObject is activated.
    /// </summary>
    public class PerfCheck : MonoBehaviour
    {
        [Serializable]
        public class Entry
        {
            public string name;
            public GameObject root;
            public VolumeProfile profile;
            public Vector3 sunEuler;
            public Color sunColor = Color.white;
            public float sunIntensity = 1.3f;
            public Color ambientSky = Color.gray;
            public Vector3 target = new(0, 1, 0);
            public float pitch = 40f, yaw = 20f;
            [Tooltip("Half-diagonal of what must stay in frame while orbiting (u).")]
            public float radius = 4.5f;
        }

        public Entry[] entries = Array.Empty<Entry>();
        public Light sun;
        public Volume volume;
        public Camera cam;
        [Tooltip("Slow sweep in front of the box (the wall and shelf are behind it): yaw = base ± amplitude.")]
        public float orbitAmplitude = 35f, orbitPeriod = 40f;
        public int targetFrameRate = 60;
        public int warmupFrames = 90;

        public static PerfCheck Instance { get; private set; }
        public int Current { get; private set; } = -1;

        // Stats of the latest visit to each scene (ring buffers, ~60 s at 60 fps).
        class Stats
        {
            public const int Cap = 3600;
            public readonly float[] frame = new float[Cap], cpu = new float[Cap], gpu = new float[Cap], main = new float[Cap], render = new float[Cap];
            public int n, head;
            public long batches, setPass, tris, draws, samplesCounters;
            public float seconds;
            public void Add(float f, float c, float g, float m, float r)
            {
                frame[head] = f; cpu[head] = c; gpu[head] = g; main[head] = m; render[head] = r;
                head = (head + 1) % Cap;
                if (n < Cap) n++;
            }
            public void Clear() { n = head = 0; batches = setPass = tris = draws = samplesCounters = 0; seconds = 0; }
        }

        Stats[] stats;
        int warmup, pendingSwitch = -1, pendingDelay;
        float phase, distance = 40f, fittedAspect;
        bool orbit = true, logging;
        StreamWriter log;
        string logPath, status = "", buildInfo;
        float statusUntil, logTimer, uiTimer;
        readonly FrameTiming[] ft = new FrameTiming[1];

        // Rolling display (0.5 s window).
        float accFrame, accCpu, accGpu, accMain, accRender; int accN, accGpuN;
        float showFps, showFrame, showCpu, showGpu, showMain, showRender;
        // Per-second log window.
        float logFrame, logCpu, logGpu; int logN, logGpuN;

        ProfilerRecorder recBatches, recSetPass, recTris, recVerts, recDraws;
        long sceneTris, sceneVerts; int sceneRenderers;

        // Input (tap = short press, little movement).
        bool pressing; Vector2 pressPos; float pressTime;

        // GUI layout in GUI units (scaled to the screen width).
        const float GuiWidth = 420f;
        float scale = 1f;
        Rect panel, bNext, bOrbit, bLog, bSend;
        GUIStyle label, small, button;
        Texture2D bg, btnTex;

        void Awake()
        {
            Instance = this;
            Application.targetFrameRate = targetFrameRate;
            QualitySettings.vSyncCount = 0;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            buildInfo = Resources.Load<TextAsset>("BuildInfo")?.text.Trim() ?? "local";
            stats = new Stats[entries.Length];
            for (int i = 0; i < stats.Length; i++) stats[i] = new Stats();
            foreach (var e in entries) if (e.root != null) e.root.SetActive(false);
            if (cam == null) cam = Camera.main;
            if (FeedbackSender.Instance == null && FindAnyObjectByType<FeedbackSender>() == null)
                new GameObject("FeedbackSender").AddComponent<FeedbackSender>();
        }

        void OnEnable()
        {
            recBatches = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Batches Count");
            recSetPass = ProfilerRecorder.StartNew(ProfilerCategory.Render, "SetPass Calls Count");
            recTris = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Triangles Count");
            recVerts = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Vertices Count");
            recDraws = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count");
            if (FeedbackSender.Instance != null) FeedbackSender.Instance.Result += OnSent;
        }

        void OnDisable()
        {
            recBatches.Dispose(); recSetPass.Dispose(); recTris.Dispose(); recVerts.Dispose(); recDraws.Dispose();
            if (FeedbackSender.Instance != null) FeedbackSender.Instance.Result -= OnSent;
            CloseLog();
        }

        void Start()
        {
            if (FeedbackSender.Instance != null) { FeedbackSender.Instance.Result -= OnSent; FeedbackSender.Instance.Result += OnSent; }
            Show(0);
        }

        // ---------------- scenes ----------------

        public void Next() => RequestShow((Current + 1) % Mathf.Max(1, entries.Length));

        /// <summary>Switch after the "Building..." line has been drawn (the box build blocks for a moment).</summary>
        public void RequestShow(int i) { pendingSwitch = i; pendingDelay = 2; }

        public void Show(int i)
        {
            if (entries.Length == 0) return;
            for (int k = 0; k < entries.Length; k++)
                if (k != i && entries[k].root != null) entries[k].root.SetActive(false);
            var e = entries[i];
            Current = i;
            if (sun != null)
            {
                sun.transform.rotation = Quaternion.Euler(e.sunEuler);
                sun.color = e.sunColor;
                sun.intensity = e.sunIntensity;
            }
            RenderSettings.ambientSkyColor = e.ambientSky;
            if (volume != null && e.profile != null) volume.sharedProfile = e.profile;
            if (e.root != null) e.root.SetActive(true); // builds the box now
            CountScene();
            stats[i].Clear();
            warmup = warmupFrames;
            FitCamera();
            WriteLog($"# scene {e.name}: {sceneTris} tris, {sceneVerts} verts, {sceneRenderers} renderers");
        }

        void CountScene()
        {
            sceneTris = sceneVerts = 0; sceneRenderers = 0;
            var root = entries[Current].root;
            if (root == null) return;
            foreach (var mf in root.GetComponentsInChildren<MeshFilter>())
            {
                var m = mf.sharedMesh;
                if (m == null) continue;
                sceneRenderers++;
                sceneVerts += m.vertexCount;
                for (int s = 0; s < m.subMeshCount; s++) sceneTris += (long)m.GetIndexCount(s) / 3;
            }
        }

        void FitCamera()
        {
            if (cam == null || entries.Length == 0) return;
            var e = entries[Current];
            float aspect = Mathf.Max(0.1f, cam.aspect);
            // Fit the radius horizontally (portrait is width-limited) and vertically, whichever is tighter.
            float halfV = cam.fieldOfView * 0.5f * Mathf.Deg2Rad;
            float halfH = Mathf.Atan(Mathf.Tan(halfV) * aspect);
            distance = e.radius / Mathf.Tan(Mathf.Min(halfH, halfV)) * 0.9f;
            fittedAspect = aspect;
        }

        void ApplyCamera()
        {
            if (cam == null || entries.Length == 0 || Current < 0) return;
            if (Mathf.Abs(cam.aspect - fittedAspect) > 0.001f) FitCamera(); // screen size settled or rotated
            var e = entries[Current];
            float yaw = e.yaw + orbitAmplitude * Mathf.Sin(phase);
            cam.transform.position = e.target + Quaternion.Euler(e.pitch, yaw, 0) * Vector3.back * distance;
            cam.transform.LookAt(e.target);
        }

        // ---------------- per frame ----------------

        void Update()
        {
            if (pendingSwitch >= 0 && --pendingDelay <= 0) { int s = pendingSwitch; pendingSwitch = -1; Show(s); }

            float dtMs = Time.unscaledDeltaTime * 1000f;
            FrameTimingManager.CaptureFrameTimings();
            float cpu = 0, gpu = 0, main = 0, render = 0;
            if (FrameTimingManager.GetLatestTimings(1, ft) > 0)
            {
                cpu = (float)ft[0].cpuFrameTime; gpu = (float)ft[0].gpuFrameTime;
                main = (float)ft[0].cpuMainThreadFrameTime; render = (float)ft[0].cpuRenderThreadFrameTime;
            }

            if (warmup > 0) warmup--;
            else if (Current >= 0 && pendingSwitch < 0)
            {
                var st = stats[Current];
                st.Add(dtMs, cpu, gpu, main, render);
                st.seconds += Time.unscaledDeltaTime;
                if (recBatches.Valid) { st.batches += recBatches.LastValue; st.setPass += recSetPass.LastValue; st.tris += recTris.LastValue; st.draws += recDraws.LastValue; st.samplesCounters++; }
            }

            accFrame += dtMs; accCpu += cpu; accMain += main; accRender += render; accN++;
            if (gpu > 0) { accGpu += gpu; accGpuN++; }
            uiTimer += Time.unscaledDeltaTime;
            if (uiTimer >= 0.5f && accN > 0)
            {
                showFrame = accFrame / accN; showFps = 1000f / Mathf.Max(0.01f, showFrame);
                showCpu = accCpu / accN; showMain = accMain / accN; showRender = accRender / accN;
                showGpu = accGpuN > 0 ? accGpu / accGpuN : 0;
                accFrame = accCpu = accGpu = accMain = accRender = 0; accN = accGpuN = 0; uiTimer = 0;
            }

            if (logging)
            {
                logFrame += dtMs; logCpu += cpu; logN++;
                if (gpu > 0) { logGpu += gpu; logGpuN++; }
                logTimer += Time.unscaledDeltaTime;
                if (logTimer >= 1f && logN > 0)
                {
                    WriteLog(string.Format(CultureInfo.InvariantCulture, "{0:0.0},{1},{2:0.0},{3:0.00},{4:0.00},{5:0.00},{6},{7},{8}",
                        Time.realtimeSinceStartup, Name(Current), 1000f * logN / logFrame, logFrame / logN, logCpu / logN,
                        logGpuN > 0 ? logGpu / logGpuN : 0, Counter(recBatches), Counter(recSetPass), Counter(recTris)));
                    logFrame = logCpu = logGpu = 0; logN = logGpuN = 0; logTimer = 0;
                }
            }

            if (orbit) phase += Time.unscaledDeltaTime * 2f * Mathf.PI / Mathf.Max(1f, orbitPeriod);
            ApplyCamera();
            HandleInput();
        }

        static long Counter(ProfilerRecorder r) => r.Valid ? r.LastValue : -1;

        string Name(int i) => i >= 0 && i < entries.Length ? entries[i].name : "-";

        // ---------------- input ----------------

        void HandleInput()
        {
            var p = Pointer.current;
            if (p == null) return;
            bool down = p.press.isPressed;
            var pos = p.position.ReadValue();
            if (down && !pressing) { pressing = true; pressPos = pos; pressTime = Time.unscaledTime; }
            else if (!down && pressing)
            {
                pressing = false;
                if (Time.unscaledTime - pressTime < 0.5f && (pos - pressPos).magnitude < 40f) Tap(pos);
            }
        }

        /// <summary>A tap at a screen position (origin bottom-left), as from a finger.</summary>
        public void Tap(Vector2 screenPos)
        {
            var g = new Vector2(screenPos.x / scale, (Screen.height - screenPos.y) / scale);
            if (bNext.Contains(g)) Next();
            else if (bOrbit.Contains(g)) orbit = !orbit;
            else if (bLog.Contains(g)) ToggleLog();
            else if (bSend.Contains(g)) SendNumbers();
            else if (!panel.Contains(g)) Next();
        }

        // ---------------- log file ----------------

        public void ToggleLog()
        {
            if (logging) { CloseLog(); Status("Log saved: " + logPath); return; }
            try
            {
                var dir = Path.Combine(Application.persistentDataPath, "perf");
                Directory.CreateDirectory(dir);
                logPath = Path.Combine(dir, $"perf_{DateTime.Now:yyyyMMdd_HHmmss}.csv");
                log = new StreamWriter(logPath, false, new UTF8Encoding(false)) { AutoFlush = true };
                log.WriteLine("# " + DeviceLine());
                log.WriteLine("t_s,scene,fps,frame_ms,cpu_ms,gpu_ms,batches,setpass,tris");
                logging = true;
                logFrame = logCpu = logGpu = 0; logN = logGpuN = 0; logTimer = 0;
                Status("Logging to " + logPath);
            }
            catch (Exception ex) { Status("Log failed: " + ex.Message); }
        }

        void WriteLog(string line)
        {
            if (!logging || log == null) return;
            try { log.WriteLine(line); } catch { /* disk full etc.: keep running */ }
        }

        void CloseLog()
        {
            logging = false;
            try { log?.Dispose(); } catch { }
            log = null;
        }

        // ---------------- summary + send ----------------

        static float Avg(float[] a, int n, bool skipZero = false)
        {
            double s = 0; int k = 0;
            for (int i = 0; i < n; i++) { if (skipZero && a[i] <= 0) continue; s += a[i]; k++; }
            return k > 0 ? (float)(s / k) : 0;
        }

        static float P95(float[] a, int n, bool skipZero = false)
        {
            var l = new List<float>(n);
            for (int i = 0; i < n; i++) if (!skipZero || a[i] > 0) l.Add(a[i]);
            if (l.Count == 0) return 0;
            l.Sort();
            return l[Mathf.Clamp(Mathf.CeilToInt(l.Count * 0.95f) - 1, 0, l.Count - 1)];
        }

        public string DeviceLine() =>
            $"{SystemInfo.deviceModel} · {SystemInfo.graphicsDeviceName} ({SystemInfo.graphicsDeviceType}) · {SystemInfo.operatingSystem} · " +
            $"{Screen.width}x{Screen.height} · render scale {RenderScale()} · quality {QualitySettings.names[QualitySettings.GetQualityLevel()]} · " +
            $"target {Application.targetFrameRate} fps · frame timing {(FrameTimingManager.IsFeatureEnabled() ? "on" : "off")}";

        static string RenderScale()
        {
            var a = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            return a != null ? a.renderScale.ToString("0.##", CultureInfo.InvariantCulture) : "?";
        }

        /// <summary>Markdown summary of every scene measured so far (the latest visit of each).</summary>
        public string Summary()
        {
            var b = new StringBuilder();
            b.Append($"Perf check {Application.version} ({buildInfo})\n").Append(DeviceLine()).Append("\n\n");
            b.Append("| Scene | s | fps | frame ms avg / p95 | CPU frame ms avg / p95 (incl. wait) | main / render ms | GPU ms avg / p95 | batches | setpass | tris (drawn) |\n");
            b.Append("|---|---|---|---|---|---|---|---|---|---|\n");
            for (int i = 0; i < entries.Length; i++)
            {
                var s = stats[i];
                if (s.n == 0) { b.Append($"| {entries[i].name} | not visited | | | | | | | | |\n"); continue; }
                float f = Avg(s.frame, s.n);
                long c = Math.Max(1, s.samplesCounters);
                b.Append(string.Format(CultureInfo.InvariantCulture,
                    "| {0} | {1:0} | {2:0.0} | {3:0.0} / {4:0.0} | {5:0.0} / {6:0.0} | {7:0.0} / {8:0.0} | {9} / {10} | {11} | {12} | {13} |\n",
                    entries[i].name, s.seconds, 1000f / Mathf.Max(0.01f, f), f, P95(s.frame, s.n), Avg(s.cpu, s.n), P95(s.cpu, s.n),
                    Avg(s.main, s.n), Avg(s.render, s.n),
                    Fmt(Avg(s.gpu, s.n, true)), Fmt(P95(s.gpu, s.n, true)),
                    s.samplesCounters > 0 ? (s.batches / c).ToString() : "n/a", s.samplesCounters > 0 ? (s.setPass / c).ToString() : "n/a",
                    s.samplesCounters > 0 ? (s.tris / c).ToString() : "n/a"));
            }
            b.Append("\nBudget: 60 fps, GPU ≤ 10 ms (issue #35). Stats skip the first ")
             .Append(warmupFrames).Append(" frames after each switch.\n");
            return b.ToString();
        }

        public void SendNumbers()
        {
            var text = Summary();
            Debug.Log("[Perf]\n" + text);
            // Always keep a copy on the device.
            try
            {
                var dir = Path.Combine(Application.persistentDataPath, "perf");
                Directory.CreateDirectory(dir);
                File.WriteAllText(Path.Combine(dir, $"summary_{DateTime.Now:yyyyMMdd_HHmmss}.md"), text);
            }
            catch { }
            if (FeedbackSender.Instance == null) { Status("Saved on the device (no feedback sender)"); return; }
            FeedbackSender.Instance.SendFeedback(FeedbackSender.Kind.Feel, $"Perf numbers from {SystemInfo.deviceModel}\n\n{text}");
            Status("Sending...");
        }

        void OnSent(bool ok, string message) => Status(ok ? "Sent. Thanks!" : "Saved; it will send when online.");

        void Status(string s) { status = s; statusUntil = Time.unscaledTime + 6f; }

        // ---------------- HUD ----------------

        void Styles()
        {
            if (label != null) return;
            bg = new Texture2D(1, 1); bg.SetPixel(0, 0, new Color(0, 0, 0, 0.62f)); bg.Apply();
            btnTex = new Texture2D(1, 1); btnTex.SetPixel(0, 0, new Color(1, 1, 1, 0.22f)); btnTex.Apply();
            label = new GUIStyle(GUI.skin.label) { fontSize = 15, richText = true, wordWrap = false };
            label.normal.textColor = Color.white;
            small = new GUIStyle(label) { fontSize = 11, wordWrap = true };
            small.normal.textColor = new Color(1, 1, 1, 0.8f);
            button = new GUIStyle(label) { alignment = TextAnchor.MiddleCenter, fontSize = 14 };
            button.normal.background = btnTex;
        }

        void OnGUI()
        {
            Styles();
            scale = Screen.width / GuiWidth;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1));
            float top = (Screen.height - Screen.safeArea.yMax) / scale + 6f;
            const float lineH = 20f;
            panel = new Rect(6, top, GuiWidth - 12, lineH * 9 + 90);
            GUI.DrawTexture(panel, bg);
            float x = panel.x + 10, y = panel.y + 6, w = panel.width - 20;
            void Line(string s) { GUI.Label(new Rect(x, y, w, lineH + 2), s, label); y += lineH; }

            string Col(float v, float good, float ok) => v <= 0 ? "n/a" :
                $"<color={(v <= good ? "#8f8" : v <= ok ? "#fd6" : "#f77")}>{v.ToString("0.0", CultureInfo.InvariantCulture)}</color>";
            var st = Current >= 0 ? stats[Current] : null;
            Line($"<b>{(pendingSwitch >= 0 ? "Building " + Name(pendingSwitch) + "..." : Name(Current))}</b>  ({Current + 1}/{entries.Length})   tap to switch");
            Line($"FPS <b><color={(showFps >= 58f ? "#8f8" : showFps >= 50f ? "#fd6" : "#f77")}>{showFps.ToString("0.0", CultureInfo.InvariantCulture)}</color></b>   frame {Col(showFrame, 17.2f, 20f)} ms");
            Line($"CPU {Fmt(showCpu)} ms   main {Col(showMain, 12f, 16.6f)}  render {Col(showRender, 12f, 16.6f)}");
            Line($"GPU <b>{Col(showGpu, 10f, 16.6f)}</b> ms   (budget ≤ 10)");
            if (st != null && st.n > 0)
                Line(string.Format(CultureInfo.InvariantCulture, "since switch {0:0}s: frame p95 {1:0.0}  GPU avg {2}  p95 {3}",
                    st.seconds, P95(st.frame, st.n), Fmt(Avg(st.gpu, st.n, true)), Fmt(P95(st.gpu, st.n, true))));
            else Line(warmup > 0 ? $"warming up ({warmup})" : "-");
            Line($"batches {Cnt(recBatches)}  setpass {Cnt(recSetPass)}  draws {Cnt(recDraws)}");
            Line($"tris drawn {Cnt(recTris)}  verts {Cnt(recVerts)}");
            Line(string.Format(CultureInfo.InvariantCulture, "scene mesh {0:0.#}k tris, {1:0.#}k verts, {2} renderers", sceneTris / 1000f, sceneVerts / 1000f, sceneRenderers));
            GUI.Label(new Rect(x, y, w, 50), $"{DeviceLine()} · {Application.version} ({buildInfo})", small); y += 52;

            float bw = (w - 18) / 4f, bh = 30f;
            bNext = new Rect(x, y, bw, bh);
            bOrbit = new Rect(x + (bw + 6), y, bw, bh);
            bLog = new Rect(x + (bw + 6) * 2, y, bw, bh);
            bSend = new Rect(x + (bw + 6) * 3, y, bw, bh);
            // Drawn as boxes, not GUI.Button: taps are handled once, in HandleInput.
            GUI.Box(bNext, "Next", button);
            GUI.Box(bOrbit, orbit ? "Orbit: on" : "Orbit: off", button);
            GUI.Box(bLog, logging ? "Log: ON" : "Log: off", button);
            GUI.Box(bSend, "Send", button);
            y += bh + 4;
            panel.height = y - panel.y + 2;
            if (Time.unscaledTime < statusUntil) GUI.Label(new Rect(6, y + 4, GuiWidth - 12, 40), status, small);
        }

        static string Fmt(float v) => v > 0 ? v.ToString("0.0", CultureInfo.InvariantCulture) : "n/a";

        static string Cnt(ProfilerRecorder r)
        {
            if (!r.Valid) return "n/a";
            long v = r.LastValue;
            return v <= 0 ? "n/a" : v >= 10000 ? (v / 1000f).ToString("0.#", CultureInfo.InvariantCulture) + "k" : v.ToString();
        }
    }
}
