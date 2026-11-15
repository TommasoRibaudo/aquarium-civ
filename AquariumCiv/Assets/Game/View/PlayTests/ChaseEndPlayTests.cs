using System.Collections;
using System.Collections.Generic;
using System.Linq;
using AquariumCiv.Game;
using AquariumCiv.GameUI;
using AquariumCiv.Life;
using AquariumCiv.Sim;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AquariumCiv.View.PlayTests
{
    /// <summary>
    /// #698 (a child chased a rabbit and then was gone, feedback note 68): the rabbit chase runs (the debug panel's "Kids play: chase
    /// rabbits", <see cref="KidPlayDirector.Debug"/>), then the group is handed back to the planner (as when the play block ends). From
    /// then on every kid in it is drawn, and walks, smoothly (no frame where the doll jumps) until it reaches the place its plan has for
    /// it: by day a slot at that place, at bedtime the tent, which they walk into (hidden only once at the tent).
    /// </summary>
    [Category("ChaseEnd")]
    public class ChaseEndPlayTests : PlayTestBase
    {
        const int Seed = 1234;
        static SimCore Sim => GameClock.Instance.ActiveSim;
        static PeopleView People => PeopleView.Find();

        IEnumerator Grown(float hour)
        {
            yield return OpenSlice(seed: Seed);
            SimLayoutSource.DebugSkipYears(8f);
            yield return null;
            var born = new List<Notable>();
            for (int i = 0; i < 4; i++) { var n = Sim.DebugBirth(); Assert.IsNotNull(n, "a grown-up to be a parent"); born.Add(n); }
            SimLayoutSource.DebugSkipYears(0.1f);
            yield return null;
            foreach (var n in born) n.age = 7f;
            People.Planner.Invalidate();
            Sim.CleanAll();
            PeopleView.DebugSetHour(hour);
            yield return Frames(4);
            yield return Until(() => GameUiPanel.Toasts.Current == null && GameUiPanel.Toasts.Waiting == 0, 6f);
            for (int calm = 0, f = 0; calm < 120 && f < 1200; f++) { calm = StagedMoments.StopAll() == 0 && StagedMoments.Playing == 0 ? calm + 1 : 0; yield return null; }
            PeopleView.DebugSetHour(hour);
            yield return Frames(4);
        }

        static PersonActivity ActOf(PeopleView pv, PersonRef who) => pv.Activities.First(a => a.who.Key == who.Key);

        static float DistToPlan(PeopleView pv, PersonActivity a, Vector2 at)
        {
            if (!pv.Planner.Positions.TryGetPosition(a.place, out float px, out float pz)) return 0f;
            return (new Vector2(px, pz) - at).magnitude;
        }

        /// <summary>The first sleep segment of a kid's day after noon: its start hour (-1: none).</summary>
        static float BedtimeOf(PeopleView pv, PersonRef kid)
        {
            var segs = new List<PersonActivity>();
            pv.Planner.DaySchedule(kid, pv.Now.day, segs);
            foreach (var s in segs) if (s.kind == ActivityKind.Sleep && s.start > 12f) return s.start;
            return -1f;
        }

        /// <summary>Runs the chase, hands the group back (at once by day; at bedtime when the first kid's plan sets off for the tent) and
        /// watches the kids for <paramref name="watch"/> s. Returns through asserts.</summary>
        IEnumerator ChaseThenWatch(bool bedtime, float watch)
        {
            bool prefWas = GameClock.EcologyPref;
            GameClock.EcologyPref = true; // rabbits live only in a living-land box
            try
            {
                yield return Grown(14f);
                var pv = People;
                var rv = RabbitView.Instance;
                Assert.IsNotNull(rv, "the rabbit view runs on a living-land Slice");
                foreach (var (x, z) in new[] { (-1.6f, 0.6f), (-1.0f, -0.8f), (-2.0f, -0.2f), (1.6f, 0.9f) }) EcoDebug.MistBig(x, z);
                Debug.Log("[698] " + EcoDebug.AddRabbits(6));
                var kids = pv.Planner.People.Where(w => pv.Planner.StageOf(w) == LifeStage.Kid).ToList();
                Assert.GreaterOrEqual(kids.Count, 2, "kids in the box");
                float bed = bedtime ? BedtimeOf(pv, kids[0]) : -1f;
                if (bedtime) Assert.Greater(bed, 12f, "a kid has a bedtime");
                float hour = bedtime ? bed - 0.9f : 14f; // an hour or so before bed: the plan's walk to the tent is next
                PeopleView.DebugSetHour(hour);
                yield return Until(() => !rv.SignPlaying && rv.Shown >= 1, 15f);
                Assert.GreaterOrEqual(rv.Shown, 1, "rabbits on view to chase");
                string said = KidPlayDirector.Debug(PlayGame.ChaseRabbits);
                Debug.Log("[698] " + said + $" (hour {hour:0.00}, bed {bed:0.00})");
                StringAssert.Contains("chase", said.ToLower().Replace("chaserabbits", "chase"), "the cheat starts the chase");
                var members = kids.Where(k => pv.KidPlay.GameOf(k.Key, out _) != null).ToList();
                Assert.GreaterOrEqual(members.Count, 1, "kids in the chase");
                yield return new WaitForSecondsRealtime(10f); // a chase's approach, creep, charge, laugh
                yield return Shot(bedtime ? "698-bed-chase" : "698-day-chase");
                if (bedtime)
                {
                    // a chase that is still going when the plan has put them to bed (a story's chase lasts ~45 s): the group is over then
                    float until = Time.realtimeSinceStartup + 40f;
                    while (Time.realtimeSinceStartup < until && !members.All(m => { var a = ActOf(pv, m); return a.kind == ActivityKind.Sleep && !a.moving; })) { PeopleView.DebugSetHour(pv.Now.hour + 0.02f); yield return null; }
                    Debug.Log("[698] handed back at " + pv.Now.hour.ToString("0.00") + " h: " + string.Join(", ", members.Select(m => ActOf(pv, m).kind + (ActOf(pv, m).moving ? ">" : "="))));
                }
                var last = new Dictionary<int, Vector2>(); var wasShown = new Dictionary<int, bool>();
                foreach (var m in members) { wasShown[m.Key] = pv.TryGetDollTransform(m, out _); pv.TryGetDollPosition(m, out var p0); last[m.Key] = p0; }
                var startFar = members.Where(m => DistToPlan(pv, ActOf(pv, m), last[m.Key]) > 0.6f).Select(m => m.Key).ToHashSet();
                var shownFrames = new Dictionary<int, int>();
                pv.KidPlay.Clear(); // the group is handed back
                yield return Shot(bedtime ? "698-bed-end" : "698-day-end");
                                var settled = new HashSet<int>(); var still = new Dictionary<int, float>();
                float t0 = Time.realtimeSinceStartup, lastT = t0; bool shot2 = false; int frames = 0;
                var log = new System.Text.StringBuilder();
                while (Time.realtimeSinceStartup - t0 < watch && settled.Count < members.Count)
                {
                    yield return null;
                    frames++;
                    float dt = Time.realtimeSinceStartup - lastT; lastT = Time.realtimeSinceStartup;
                    if (!shot2 && Time.realtimeSinceStartup - t0 > (bedtime ? 1.0f : 2.5f)) { shot2 = true; yield return Shot(bedtime ? "698-bed-later" : "698-day-later"); lastT = Time.realtimeSinceStartup; }
                    foreach (var m in members)
                    {
                        var a = ActOf(pv, m);
                        bool shown = pv.TryGetDollTransform(m, out _);
                        pv.TryGetDollPosition(m, out var pos);
                        if (shown) shownFrames[m.Key] = shownFrames.GetValueOrDefault(m.Key) + 1;
                        bool had = wasShown.TryGetValue(m.Key, out var ws);
                        if (shown && had && ws)
                        {
                            float step = (pos - last[m.Key]).magnitude;
                            if (step > 1.2f * dt + 0.25f) log.AppendLine($"JUMP {m} {step:0.000} u in {dt * 1000:0} ms at {pv.Now.hour:0.00} h, act {a.kind} moving {a.moving} to {a.place.kind}");
                        }
                        if (had && ws && !shown)
                        {
                            float d = DistToPlan(pv, a, last[m.Key]);
                            bool intent = a.kind is ActivityKind.Sleep or ActivityKind.Indoors && !a.moving;
                            if (!intent || d > 0.6f) log.AppendLine($"VANISH {m} at ({last[m.Key].x:0.00},{last[m.Key].y:0.00}), {d:0.00} u from its plan's place ({a.kind} {(a.moving ? "moving" : "still")} at {a.place.kind}), hour {pv.Now.hour:0.00}");
                        }
                        if (!shown && !a.moving) settled.Add(m.Key);
                        else if (shown && !a.moving && had && (pos - last[m.Key]).magnitude < 1e-4f) { still[m.Key] = still.GetValueOrDefault(m.Key) + dt; if (still[m.Key] > 1.5f) settled.Add(m.Key); }
                        else still[m.Key] = 0f;
                        wasShown[m.Key] = shown; last[m.Key] = pos;
                    }
                }
                Debug.Log($"[698] watched {frames} frames, {Time.realtimeSinceStartup - t0:0.0} s, settled {settled.Count}/{members.Count}\n{log}");
                Assert.IsEmpty(log.ToString(), "kids hidden or jumping on the way back to their plan");
                foreach (int k in startFar) Assert.GreaterOrEqual(shownFrames.GetValueOrDefault(k), 20, $"kid {k} started >0.6 u from its plan's place and must be seen walking there, not gone at once");
                Assert.AreEqual(members.Count, settled.Count, "every kid reached its plan's place");
                AssertNoErrors("698");
            }
            finally { GameClock.EcologyPref = prefWas; }
        }

        [UnityTest, Timeout(300000)]
        public IEnumerator ByDay_TheChaseEnds_AndTheKidsWalkBackVisibly() { yield return ChaseThenWatch(false, 90f); }

        [UnityTest, Timeout(300000)]
        public IEnumerator NearBedtime_TheChaseEnds_AndTheKidsWalkIntoTheTent() { yield return ChaseThenWatch(true, 90f); }
    }
}
