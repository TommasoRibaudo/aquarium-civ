using System.Collections;
using System.Collections.Generic;
using System.Linq;
using AquariumCiv.Game;
using AquariumCiv.Sim;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AquariumCiv.View.PlayTests
{
    /// <summary>Feedback 9 editor pass. A camp staged some years on, left alone: every ground-chunk rebuild in a minute and why (#632),
    /// then the camp's paths close up (#631). Shots (1179x2556 PNG) to Temp/agent/fb5pass/ (Fb5PassShots' folder). Explicit.</summary>
    [Explicit("Screenshots and a log for the eye: run by hand"), Category("Fb9Pass")]
    public class Fb9Shots : PlayTestBase
    {
        static SimCore Sim => GameClock.Instance.ActiveSim;

        [UnityTest, Timeout(900000)]
        public IEnumerator IdleCamp_RebuildLog_ThenPathsCloseUp()
        {
            var stage = new Fb5PassShots();
            yield return stage.Stage(hour: 11f, pond: true, grownUps: 9);
            // The tester's box (#625/#626/#629): a camp near year 14, lush grass, 9-11 people, a tent wanted.
            for (int y = 0; y < 10; y++)
            {
                for (int k = 0; k < 3; k++) foreach (var (x, z) in new[] { (-2.4f, 1.2f), (0f, 1.4f), (2.4f, -1f), (0.5f, -1.6f), (-0.5f, 0.2f), (2f, 1.5f), (-1.5f, -1.2f), (1f, 0.3f) }) EcoDebug.MistBig(x, z);
                EcoDebug.SkipSeasons(4);
                for (int calm = 0, f = 0; calm < 30 && f < 600; f++) { calm = StagedMoments.StopAll() == 0 && StagedMoments.Playing == 0 ? calm + 1 : 0; yield return null; }
            }
            for (int calm = 0, f = 0; calm < 120 && f < 1500; f++) { calm = StagedMoments.StopAll() == 0 && StagedMoments.Playing == 0 ? calm + 1 : 0; yield return null; }
            PeopleView.DebugSetHour(11f);
            yield return new WaitForSecondsRealtime(3f);
            var box = Object.FindAnyObjectByType<BoxView>();
            Debug.Log($"[fb9] staged: era {Sim.State.era}, year {Sim.State.Year:0.0}, people {Sim.State.notables.Count(n => n.alive)}, pieces {box.layout.pieces.Count}, paths {box.layout.paths.Count}");

            // #632: a minute with nobody touching the box.
            var lines = new List<string>();
            void Grab(string msg, string trace, LogType type) { if (msg.StartsWith("[BoxView] rebuilt")) lines.Add(msg); }
            Application.logMessageReceived += Grab;
            float t0 = Time.realtimeSinceStartup;
            int tentShots = 0, liveMax = 0;
            while (Time.realtimeSinceStartup - t0 < 60f)
            {
                liveMax = Mathf.Max(liveMax, box.LivePieceCount);
                var live = box.layout.pieces.FirstOrDefault(p => p.live && p.grow > 0.35f + 0.3f * tentShots);
                if (live != null && tentShots < 2)
                {
                    // the rising tent (#632: its own object), framed; the camera move itself rebuilds nothing
                    Fb5PassShots.Look(live.x, live.z, 0.3f, 50f);
                    yield return new WaitForSecondsRealtime(0.3f);
                    yield return Fb5PassShots.Snap($"632_tent_rising_{tentShots}_grow{live.grow:0.0}");
                    tentShots++;
                }
                yield return null;
            }
            Application.logMessageReceived -= Grab;
            Debug.Log($"[fb9] live pieces at most {liveMax}, rising-tent shots {tentShots}");
            float secs = Time.realtimeSinceStartup - t0;
            var why = new Dictionary<string, int>();
            foreach (var l in lines)
            {
                int i = l.IndexOf(" why ");
                if (i < 0) continue;
                foreach (var part in l.Substring(i + 5).Split(';'))
                {
                    var p = part.Trim(); int c = p.IndexOf(':');
                    if (c < 0) continue;
                    foreach (var name in p.Substring(c + 1).Split(',')) { var n = name.Trim(); why[n] = why.TryGetValue(n, out int v) ? v + 1 : 1; }
                }
            }
            Debug.Log($"[fb9] idle {secs:0} s: {lines.Count} rebuilds; parts {string.Join(", ", why.Select(kv => kv.Key + " " + kv.Value))}");
            foreach (var l in lines.Take(12)) Debug.Log("[fb9] " + l);

            // #631: the camp's paths, close up.
            int shot = 0;
            foreach (var path in box.layout.paths)
            {
                if (path.points == null || path.points.Length < 2 || shot >= 4) continue;
                var mid = path.points[path.points.Length / 2];
                Fb5PassShots.Look(mid.x, mid.y, 0.3f, 55f);
                yield return new WaitForSecondsRealtime(0.6f);
                yield return Fb5PassShots.Snap($"631_path_{shot}_{path.tier}");
                shot++;
            }
            Debug.Log($"[fb9] path shots: {shot}");
        }
    }
}
