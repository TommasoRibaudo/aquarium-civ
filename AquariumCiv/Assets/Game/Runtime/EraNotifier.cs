using System;
using System.Diagnostics;
using System.Globalization;
using System.Threading;
using AquariumCiv.Sim;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace AquariumCiv.Game
{
    /// <summary>What the last close did, for the log, the debug panel and the tests.</summary>
    public struct CloseReport
    {
        public RunAheadResult result;
        public long copyMs, waitMs, runMs;  // the main thread's two costs, and the worker's
        public bool scheduled;
        public TimeSpan after;
        public string title, body;

        public override string ToString()
        {
            string found = result.Found ? $"{result.target} {result.era} in {result.realHours:0.00} real h" : result.miss.ToString();
            string what = scheduled ? $"scheduled in {after.TotalHours:0.00} h" : "nothing scheduled";
            return string.Format(CultureInfo.InvariantCulture, "{0} · {1} months · copy {2} ms and wait {3} ms on the main thread, {4} ms on the worker · {5}",
                found, result.monthsRun, copyMs, waitMs, runMs, what);
        }
    }

    /// <summary>
    /// The one local notification (#97, era-moment.md §10, decisions.md Notifications): at close a copy of the box runs ahead to the moment the
    /// next era or the peak is ready, one notification goes for then, and an open cancels it.
    /// <para><b>The time a pause has.</b> The run ahead cost 708 to 1,323 ms in the Editor at worst, far more than a pause callback should take,
    /// so it runs on a worker. The sim is safe there: <see cref="SimCore.RunAhead"/> lives a separate copy, the only statics it wrote (Placer's and
    /// SiteMap's scratch) are per thread now, and JsonUtility is documented for background threads. The state is written to JSON on the main thread,
    /// about what the pause's own save costs, because the live box mustn't change under the writer. Then the main thread gives the worker at most
    /// <see cref="WaitMs"/> and leaves. Past that the worker goes on alone and schedules from its own thread, if the phone lets it live: iOS may
    /// suspend it first. Then there is simply no notification, and the open throws the run away. The worst case is a box with nothing ready in 30
    /// days, which has nothing to schedule.</para>
    /// </summary>
    public sealed class EraNotifier
    {
        /// <summary>The player's answer and the settings toggle are one value.</summary>
        public const string EnabledKey = "moment.notify";
        /// <summary>The most the pause callback waits for the worker.</summary>
        public const int WaitMs = 500;
        /// <summary>A worker past this has stopped being useful.</summary>
        public const double GiveUpSeconds = 20;
        /// <summary>A time already gone, or a moment away, still gives the phone a moment.</summary>
        public const double SoonestSeconds = 60;
        public const int NotificationId = 9701;
        const int StackBytes = 2 * 1024 * 1024; // iOS gives a new thread 512 KB unless asked

        public static bool Enabled
        {
            get => PlayerPrefs.GetInt(EnabledKey, 0) == 1;
            set { PlayerPrefs.SetInt(EnabledKey, value ? 1 : 0); PlayerPrefs.Save(); }
        }

        /// <summary>The phone itself: logged on anything else.</summary>
        public static ILocalNotifications MakePlatform()
        {
#if (UNITY_IOS || UNITY_ANDROID) && !UNITY_EDITOR
            return new PhoneNotifications();
#else
            return new LoggedNotifications();
#endif
        }

        sealed class Run
        {
            public Thread thread;
            public volatile bool stopped;
            public DateTime closeUtc;
            public string name, json;
            public RunAheadResult result;
            public Exception failed;
            public long copyMs, waitMs, runMs;
            public int claimed; // whoever arrives second, of the worker and the main thread, finishes the run
        }

        readonly ILocalNotifications phone;
        readonly object gate = new();
        readonly int waitMs;
        Run current;
        CloseReport? last;

        /// <summary>What the last close that ran to the end did.</summary>
        public CloseReport? Last { get { lock (gate) return last; } }

        public bool Running { get { lock (gate) return current != null; } }

        /// <summary>The phone this notifier talks to, for the one question it doesn't ask itself.</summary>
        public ILocalNotifications Phone => phone;

        public EraNotifier(ILocalNotifications phone, int waitMs = WaitMs)
        {
            this.phone = phone;
            this.waitMs = waitMs;
            phone.Prepare();
        }

        /// <summary>The box opened, or came back: the run in flight is dropped and whatever was scheduled is cancelled.</summary>
        public void Open()
        {
            Stop();
            phone.CancelAll();
        }

        /// <summary>Drops the run in flight. After this it can't schedule anything.</summary>
        public void Stop()
        {
            lock (gate)
            {
                if (current != null) current.stopped = true;
                current = null;
            }
        }

        /// <summary>The player left at <paramref name="closeUtc"/>. Main thread, bounded by the JSON plus <see cref="WaitMs"/>.</summary>
        public void Close(SimCore sim, DateTime closeUtc, bool enabled)
        {
            Stop();
            if (!enabled || sim == null) return;
            var clock = Stopwatch.StartNew();
            var run = new Run { closeUtc = closeUtc, name = sim.State.settlementName, json = JsonUtility.ToJson(sim.State) };
            run.copyMs = clock.ElapsedMilliseconds;
            run.thread = new Thread(() => Work(sim, run), StackBytes) { IsBackground = true, Name = "EraNotifier" };
            lock (gate) current = run;
            run.thread.Start();
            run.thread.Join(waitMs);
            run.waitMs = clock.ElapsedMilliseconds - run.copyMs;
            Claim(run);
        }

        /// <summary>Waits for the run in flight. Tests, and the debug panel.</summary>
        public bool WaitForRun(int ms)
        {
            Run run;
            lock (gate) run = current;
            return run == null || run.thread.Join(ms);
        }

        void Work(SimCore sim, Run run)
        {
            var clock = Stopwatch.StartNew();
            try
            {
                run.result = sim.RunAhead(_ => JsonUtility.FromJson<SimState>(run.json), () => run.stopped || clock.Elapsed.TotalSeconds > GiveUpSeconds);
            }
            catch (Exception e) { run.failed = e; }
            run.runMs = clock.ElapsedMilliseconds;
            Claim(run);
        }

        void Claim(Run run)
        {
            if (Interlocked.Exchange(ref run.claimed, 1) == 1) Finish(run);
        }

        /// <summary>Under the lock, so an open either finds it scheduled, and cancels it, or stops it first.</summary>
        void Finish(Run run)
        {
            CloseReport report;
            string problem = null;
            lock (gate)
            {
                if (current != run) return;
                current = null;
                report = new CloseReport { result = run.result, copyMs = run.copyMs, waitMs = run.waitMs, runMs = run.runMs };
                if (run.failed != null) problem = run.failed.Message;
                else if (run.result.Found)
                {
                    var after = run.closeUtc.AddHours(run.result.realHours) - DateTime.UtcNow;
                    report.after = after.TotalSeconds < SoonestSeconds ? TimeSpan.FromSeconds(SoonestSeconds) : after;
                    Words(run.result, run.name, out report.title, out report.body);
                    try { phone.Schedule(NotificationId, report.after, report.title, report.body); report.scheduled = true; }
                    catch (Exception e) { problem = e.Message; }
                }
                last = report;
            }
            if (problem != null) Debug.LogWarning("[Notify] no notification: " + problem);
            Debug.Log("[Notify] close: " + report);
        }

        /// <summary>The words: warm, and they ask for nothing (pillar 4). Never a count, a deadline or what was missed.</summary>
        public static void Words(RunAheadResult r, string name, out string title, out string body)
        {
            title = string.IsNullOrEmpty(name) ? "Your box" : name;
            body = r.target == RunAheadTarget.Peak
                ? "It has grown as far as it can. They'll wait for you."
                : "Your people are ready to grow. They'll wait for you.";
        }
    }
}
