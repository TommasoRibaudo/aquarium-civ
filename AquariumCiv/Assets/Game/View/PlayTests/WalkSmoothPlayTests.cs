using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using AquariumCiv.Game;
using AquariumCiv.Life;
using AquariumCiv.Sim;
using AquariumCiv.View.ShowMe;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AquariumCiv.View.PlayTests
{
    /// <summary>#203 on the real scene: walkers close together and walkers alone, stepped on a fixed frame time (the phone runs at 30 fps), measured
    /// frame to frame: how much the walk's direction turns, how the facing wobbles, how the speed jerks and whether the stride's roll jumps.</summary>
    [Category("WalkSmooth")]
    public class WalkSmoothPlayTests : PlayTestBase
    {
        const int Seed = 1234;
        static SimCore Sim => GameClock.Instance.ActiveSim;
        static PeopleView People => PeopleView.Find();

        sealed class Track
        {
            public Vector2 at, step, facing, accel;
            public float roll, yawRate;
            public bool has, hasStep, hasRate, hasAccel;
            public int age;
        }

        /// <summary>What one population's frames came to, in rates so 30 and 60 fps compare.</summary>
        sealed class Tally
        {
            public readonly List<float> turn = new(), yaw = new(), jerk = new(), stride = new();
            public int flips, frames, reversals;
            /// <summary>The push and pull of jitter: the doll's acceleration turning back on itself from one frame to the next. A corner or a curve doesn't.</summary>
            public float ReversalsPerSecond(float dt) => frames == 0 ? 0f : reversals / (frames * dt);
            public float Seconds(float dt) => frames * dt;
            public float FlipsPerSecond(float dt) => frames == 0 ? 0f : flips / (frames * dt);
            static float P(List<float> l, float q) { if (l.Count == 0) return 0f; var s = l.OrderBy(x => x).ToList(); return s[Mathf.Min(s.Count - 1, (int)(s.Count * q))]; }
            public float TurnP95 => P(turn, 0.95f);
            public float JerkP95 => P(jerk, 0.95f);
            public float StrideMax => P(stride, 0.999f);
            public float YawP95 => P(yaw, 0.95f);
            public string Say(float dt) => $"{frames} walker-frames, turn p95 {TurnP95:0} deg/s max {P(turn, 1f):0}, yaw rate p95 {YawP95:0} deg/s, yaw flips {FlipsPerSecond(dt):0.00}/s, push reversals {ReversalsPerSecond(dt):0.00}/s, speed change p95 {JerkP95:0.00} u/s2, stride roll jump p99.9 {StrideMax:0.0} deg/s";
        }

        const float Close = 0.16f, Lone = 0.4f;

        /// <summary>Steps the people <paramref name="frames"/> frames and files every walker's frame under close or alone.</summary>
        static void Measure(PeopleView pv, int frames, float dt, Tally close, Tally lone)
        {
            var tracks = new Dictionary<int, Track>();
            var where = new List<(int key, Vector2 at, bool walking)>();
            for (int f = 0; f < frames; f++)
            {
                pv.Tick(dt);
                where.Clear();
                foreach (var a in pv.Activities)
                    if (pv.TryGetDollTop(a.who, out _) && pv.TryGetDollPosition(a.who, out var at)) where.Add((a.who.Key, at, a.moving));
                foreach (var a in pv.Activities)
                {
                    if (!tracks.TryGetValue(a.who.Key, out var t)) tracks[a.who.Key] = t = new Track();
                    if (!a.moving || !pv.TryGetDollTop(a.who, out _) || !pv.TryGetDollPosition(a.who, out var p)) { t.has = t.hasStep = t.hasRate = false; t.age = 0; continue; }
                    pv.TryGetDollFacing(a.who, out var face);
                    pv.TryGetDollPose(a.who, out var pose);
                    if (t.has && t.age > 3)
                    {
                        float near = float.MaxValue;
                        foreach (var o in where) if (o.key != a.who.Key) near = Mathf.Min(near, (o.at - p).magnitude);
                        var into = near < Close ? close : near > Lone ? lone : null;
                        var step = p - t.at;
                        if (into != null)
                        {
                            into.frames++;
                            if (t.hasStep && step.magnitude > 1e-4f && t.step.magnitude > 1e-4f)
                            {
                                into.turn.Add(Mathf.Abs(Vector2.SignedAngle(t.step, step)) / dt);
                                into.jerk.Add(Mathf.Abs(step.magnitude - t.step.magnitude) / dt / dt);
                                var accel = (step - t.step) / dt / dt;
                                if (t.hasAccel && accel.magnitude > 0.5f && t.accel.magnitude > 0.5f && Vector2.Dot(accel, t.accel) < 0f) into.reversals++;
                                t.accel = accel; t.hasAccel = true;
                            }
                            float rate = Vector2.SignedAngle(t.facing, face) / dt;
                            into.yaw.Add(Mathf.Abs(rate));
                            if (t.hasRate && Mathf.Sign(rate) != Mathf.Sign(t.yawRate) && Mathf.Abs(rate) > 40f && Mathf.Abs(t.yawRate) > 40f) into.flips++;
                            into.stride.Add(Mathf.Abs(pose.roll - t.roll) / dt);
                            t.yawRate = rate; t.hasRate = true;
                        }
                        t.step = step; t.hasStep = true;
                    }
                    t.at = p; t.facing = face; t.roll = pose.roll; t.has = true; t.age++;
                }
            }
        }

        IEnumerator Village(int w = 1080, int h = 1920)
        {
            yield return OpenSlice(w, h, Seed);
            ShowMeDirector.BubblesOn = false;
            StringAssert.Contains("the town waits", MomentPlayer.DebugGrowVillage(30f));
            yield return Frames(3);
            Sim.CleanAll();
            yield return Frames(4);
        }

        IEnumerator Run(float fps)
        {
            yield return Village();
            var pv = People;
            pv.HandDriven = true;
            float dt = 1f / fps;
            var close = new Tally();
            var lone = new Tally();
            try
            {
                foreach (float hour in new[] { 6.6f, 12.2f, 17.4f }) // the morning, the midday and the evening walks
                {
                    pv.SetHour(hour);
                    Measure(pv, (int)(18f * fps), dt, close, lone);
                }
                pv.SetHour(10f);
                StringAssert.Contains("at the glass", GlassVoice.DebugGather(12)); // a group walking one way
                Measure(pv, (int)(20f * fps), dt, close, lone);
            }
            finally { pv.HandDriven = false; }
            string report = $"[#203] {fps:0} fps close {close.Say(dt)}\n[#203] {fps:0} fps alone {lone.Say(dt)}";
            Debug.Log(report);
            Directory.CreateDirectory(Shots);
            File.AppendAllText(Shots + "walksmooth.txt", report + "\n");
            Assert.Greater(close.frames, (int)(4f * fps), "walkers really did walk close together");
            Assert.Greater(lone.frames, (int)(4f * fps), "and alone");
            Assert.Less(close.ReversalsPerSecond(dt), 1.5f, "close walkers don't shake: " + report);
            Assert.Less(close.FlipsPerSecond(dt), 0.3f, "their facing doesn't flip from side to side: " + report);
            Assert.Less(close.StrideMax, 60f, "their stride doesn't skip: " + report);
            Assert.Less(lone.StrideMax, 60f, "nor does a lone walker's: " + report);
            Assert.Less(lone.ReversalsPerSecond(dt), 1.5f, "a lone walker doesn't shake: " + report);
            AssertNoErrors("walks");
        }

        [UnityTest, Timeout(170000)]
        public IEnumerator WalkersStaySmoothAt30Fps() => Run(30f);

        [UnityTest, Timeout(170000)]
        public IEnumerator WalkersStaySmoothAt60Fps() => Run(60f);

        // ---------------- #200, #201: what a Village does at its doors ----------------

        static bool AtHome(PersonActivity a) => a.place.kind == PlaceKind.Home && !a.moving;

        const string Look = "C:/Users/User/AppData/Local/Temp/claude/F--GameDev/0b07151d-6b1f-42ed-8a84-5cd49b334010/scratchpad/bundle4-shots/";

        static IEnumerator LookShot(string name)
        {
            yield return new WaitForEndOfFrame();
            var tex = ScreenCapture.CaptureScreenshotAsTexture();
            Directory.CreateDirectory(Look);
            File.WriteAllBytes(Look + name + ".png", tex.EncodeToPNG());
            Object.Destroy(tex);
        }

        /// <summary>The user's Village (pop 14, a winter month): no hammer at any finished house, at most <see cref="LifePlanner.DoorSitters"/> sitting out, every season, every half hour.</summary>
        [UnityTest, Timeout(170000)]
        public IEnumerator TheDoorsStayQuietAndNobodyBuildsAFinishedHouse()
        {
            yield return Village(828, 1792);
            var pv = People;
            var planner = pv.Planner;
            var list = new List<PersonActivity>();
            var sb = new StringBuilder();
            long day = Sim.State.month;
            sb.AppendLine($"[census] {Sim.State.era} y{Sim.State.month / 12f:0.0} people {planner.People.Count} buildings {Sim.State.buildings.Count}");
            int worstSit = 0, hammers = 0, indoors = 0;
            foreach (Season season in System.Enum.GetValues(typeof(Season)))
            {
                planner.ForcedSeason = season;
                for (long d = day; d < day + 6; d++)
                    for (float h = 5f; h < 22f; h += 0.5f)
                    {
                        planner.PlanAll(new ViewTime(d, h), list);
                        var doors = list.Where(AtHome).ToList();
                        worstSit = Mathf.Max(worstSit, doors.Count(a => a.kind == ActivityKind.SitOut));
                        hammers += doors.Count(a => DollMotion.StyleOf(a.kind) == DollStyle.Hammer);
                        indoors += list.Count(a => a.kind == ActivityKind.Indoors);
                        if (d == day) sb.AppendLine($"[census] {season} {h:00.0} at doors {doors.Count}/{list.Count(x => x.kind != ActivityKind.Sleep && x.kind != ActivityKind.None && x.kind != ActivityKind.Indoors)}: {string.Join(", ", doors.GroupBy(a => a.kind).Select(g => $"{g.Key} {g.Count()}"))}");
                    }
            }
            Directory.CreateDirectory(Shots);
            File.WriteAllText(Shots + "census.txt", sb.ToString());
            planner.ForcedSeason = Season.Winter;
            foreach (float h in new[] { 6.3f, 7.6f, 9f, 13.5f, 17.5f })
            {
                pv.SetHour(h);
                yield return Frames(6);
                yield return LookShot($"doors-winter-{h:00.0}");
            }
            var cam = Camera.main.GetComponent<BoxCamera>();
            cam.EaseTo(Vector3.zero, cam.yaw, 42f, 0.5f, 0.1f);
            yield return new WaitForSecondsRealtime(0.4f);
            pv.SetHour(7.6f);
            yield return Frames(6);
            yield return LookShot("doors-winter-close-07.6");
            planner.ForcedSeason = null;
            Debug.Log($"[census] worst sitting out {worstSit}, hammers at homes {hammers}, indoors frames {indoors}");
            Assert.AreEqual(0, hammers, "nobody hammers at a finished house");
            Assert.LessOrEqual(worstSit, LifePlanner.DoorSitters, "only a few sit out");
            Assert.Greater(indoors, 0, "the rest are in");
            AssertNoErrors("doors");
        }

        /// <summary>A group on the move, close up, a few frames apart, to look at.</summary>
        [UnityTest, Timeout(170000)]
        public IEnumerator AFrameStripOfAGroupWalking()
        {
            yield return Village();
            var pv = People;
            pv.SetHour(10f);
            StringAssert.Contains("at the glass", GlassVoice.DebugGather(12));
            yield return new WaitForSecondsRealtime(1.2f);
            var who = pv.Activities.Where(a => a.moving && pv.TryGetDollTop(a.who, out _)).Select(a => a.who).ToList();
            Assume.That(who.Count, Is.GreaterThan(2));
            pv.TryGetDollFeet(who[0], out var at);
            var cam = Camera.main.GetComponent<BoxCamera>();
            cam.EaseTo(at, cam.yaw, 50f, cam.minZoom, 0.1f);
            yield return new WaitForSecondsRealtime(0.3f);
            for (int i = 0; i < 6; i++)
            {
                if (pv.TryGetDollFeet(who[0], out at)) cam.target = at;
                yield return new WaitForSecondsRealtime(0.17f);
                yield return LookShot($"strip-{i}");
            }
            AssertNoErrors("strip");
        }
    }
}
