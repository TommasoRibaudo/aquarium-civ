using System.Collections;
using System.Collections.Generic;
using System.Linq;
using AquariumCiv.Game;
using AquariumCiv.Life;
using AquariumCiv.Lighting;
using AquariumCiv.Sim;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AquariumCiv.View.PlayTests
{
    /// <summary>Visit-loop batch 5 looks: the camp-frame camera in a living-land Camp of ~8 after an Away (doll px, the near glass, a
    /// pinch out and back), a real hunt in the baked grass at the camp frame, the camp at 09:30 / 12:40 / 16:15, the wedding cheat and
    /// the puff row at the camp frame. Shots and numbers to Temp/agent/b5/ and the log ([b5]).</summary>
    [Explicit("Screenshots for the eye: run by hand"), Category("Batch5")]
    public class Batch5Shots : PlayTestBase
    {
        const string Dir = "b5/";
        public override void Setup() { base.Setup(); System.IO.Directory.CreateDirectory(Shots + Dir); }
        static SimCore Sim => GameClock.Instance.ActiveSim;
        static BoxCamera Rig => Camera.main.GetComponent<BoxCamera>();

        IEnumerator LivingCamp(int w, int h, int grownUps = 8)
        {
            GameClock.EcologyPref = true;
            yield return OpenSlice(w, h, seed: 4242);
            PeopleView.DebugSetHour(9f);
            foreach (var (x, z) in new[] { (-1.6f, 0.6f), (-1.0f, -0.8f), (-2.0f, -0.2f), (1.6f, 0.9f) }) EcoDebug.MistBig(x, z);
            EcoDebug.SkipSeasons(1);
            EcoDebug.OpenCampGate();
            for (int i = 0; i < 12 && Sim.State.era != Era.Camp; i++) { EcoDebug.MistCamp(); EcoDebug.SkipSeasons(1); }
            EcoDebug.AddRabbits(6);
            var born = new List<Notable>();
            int adults = Sim.State.notables.Count(n => n.alive && n.age >= 16f);
            for (int i = adults; i < grownUps; i++) { var n = Sim.DebugBirth(); if (n == null) break; born.Add(n); }
            if (born.Count > 0)
            {
                SimLayoutSource.DebugSkipYears(0.1f);
                yield return null;
                foreach (var n in born) n.age = 20f;
                PeopleView.Find().Planner.Invalidate();
            }
            yield return Calm();
            Debug.Log($"[b5] camp: era {Sim.State.era}, grown-ups {Sim.State.notables.Count(n => n.alive && n.age >= 16f)}, all {Sim.State.notables.Count(n => n.alive)}");
        }

        static IEnumerator Calm()
        {
            for (int calm = 0, f = 0; calm < 60 && f < 900; f++) { calm = StagedMoments.StopAll() == 0 && StagedMoments.Playing == 0 ? calm + 1 : 0; yield return null; }
        }

        static string DollPx(out float median)
        {
            var pv = PeopleView.Find(); var cam = Camera.main;
            var px = new List<float>();
            var who = new List<PersonRef>(); var xs = new List<float>(); var zs = new List<float>();
            pv.StandingNow(who, xs, zs);
            int onScreen = 0;
            for (int i = 0; i < who.Count; i++)
            {
                if (!pv.TryGetDollFeet(who[i], out var feet)) continue;
                var a = cam.WorldToScreenPoint(feet);
                var b = cam.WorldToScreenPoint(feet + Vector3.up * CampFrame.DollHeight * pv.DollZoom * pv.transform.lossyScale.y);
                if (a.z > 0f && a.x > 0 && a.x < Screen.width && a.y > 0f && a.y < Screen.height) { onScreen++; px.Add(Mathf.Abs(b.y - a.y)); }
            }
            px.Sort();
            median = px.Count > 0 ? px[px.Count / 2] : 0f;
            return $"dolls {who.Count}, on screen {onScreen}, px {string.Join(",", px.Select(p => p.ToString("0")))}, median {median:0}";
        }

        static string Glass()
        {
            var box = Object.FindAnyObjectByType<BoxView>();
            return $"panes {box.PaneOpacity(0):0.00}/{box.PaneOpacity(1):0.00}/{box.PaneOpacity(2):0.00}/{box.PaneOpacity(3):0.00} frames {box.FrameOpacity(0):0.00}/{box.FrameOpacity(1):0.00}/{box.FrameOpacity(2):0.00}/{box.FrameOpacity(3):0.00}/{box.FrameOpacity(4):0.00}";
        }

        IEnumerator CameraAt(int w, int h)
        {
            yield return LivingCamp(w, h);
            var rig = Rig; var view = CampFrameView.Instance;
            CampFrameView.Auto = true;
            int before = view.Framed;
            Debug.Log("[b5] " + GameClock.DebugAway(1));
            yield return Until(() => view.Framed > before, 8f);
            yield return Until(() => !rig.IsFlying, 4f);
            yield return new WaitForSecondsRealtime(1.2f);
            Debug.Log($"[b5] {w}x{h} away+1h: {CampFrameView.Status()} | zoom {rig.zoom:0.000} | {DollPx(out _)} | {Glass()}");
            yield return Shot(Dir + $"cam_{w}x{h}_framed");
            Debug.Log("[b5] " + CampFrameView.DebugWholeBox());
            yield return Until(() => !rig.IsFlying, 4f); yield return new WaitForSecondsRealtime(1f);
            Debug.Log($"[b5] {w}x{h} whole: zoom {rig.zoom:0.000} | {DollPx(out _)} | {Glass()}");
            yield return Shot(Dir + $"cam_{w}x{h}_whole");
            Debug.Log("[b5] " + CampFrameView.DebugCampFrame());
            yield return Until(() => !rig.IsFlying, 4f); yield return new WaitForSecondsRealtime(1f);
            Debug.Log($"[b5] {w}x{h} back: zoom {rig.zoom:0.000} | {DollPx(out _)} | {Glass()}");
            yield return Shot(Dir + $"cam_{w}x{h}_back");
        }

        [UnityTest, Timeout(600000)] public IEnumerator A_Camera_Phone() => CameraAt(1179, 2556);
        [UnityTest, Timeout(600000)] public IEnumerator B_Camera_Small() => CameraAt(1080, 1920);

        [UnityTest, Timeout(900000)]
        public IEnumerator C_CampHours_Wedding_Puffs()
        {
            yield return LivingCamp(1179, 2556);
            var pv = PeopleView.Find(); var rig = Rig;
            foreach (var hr in new[] { 9.5f, 12.67f, 16.25f })
            {
                PeopleView.DebugSetHour(hr);
                yield return Frames(20);
                yield return Calm();
                Debug.Log("[b5] " + CampFrameView.DebugCampFrame());
                yield return Until(() => !rig.IsFlying, 4f);
                yield return new WaitForSecondsRealtime(3f);
                var kinds = pv.Activities.GroupBy(a => a.kind).Select(g => $"{g.Key}x{g.Count()}");
                Debug.Log($"[b5] camp at {hr:0.00}: {string.Join(" ", kinds)}");
                yield return Shot(Dir + $"camp_{(int)(hr * 100)}");
            }
            PeopleView.DebugSetHour(11f); yield return Frames(10);
            Debug.Log("[b5] wedding: " + StagedMoments.DebugPlayNow(MomentKind.Wedding));
            Debug.Log("[b5] " + CampFrameView.DebugCampFrame());
            for (int i = 0; i < 4; i++)
            {
                yield return new WaitForSecondsRealtime(i == 0 ? 6f : 5f);
                yield return Shot(Dir + $"wedding_{i}");
            }
            StagedMoments.StopAll();
            yield return Calm();
            Debug.Log("[b5] " + CampFrameView.DebugCampFrame());
            yield return Until(() => !rig.IsFlying, 4f); yield return new WaitForSecondsRealtime(1f);
            Debug.Log("[b5] puffs: " + HitFxCheats.PuffRow());
            yield return Frames(4); yield return Shot(Dir + "puffs_0");
            yield return Frames(6); yield return Shot(Dir + "puffs_1");
        }

        [UnityTest, Timeout(900000)]
        public IEnumerator D_RealHunt_AtTheCampFrame()
        {
            yield return LivingCamp(1179, 2556, 5);
            var pv = PeopleView.Find(); var rv = RabbitView.Instance; var rig = Rig; var cam = Camera.main;
            PeopleView.DebugSetHour(9f);
            yield return Frames(30);
            yield return Until(() => !rv.SignPlaying && rv.Shown >= 3, 15f);
            EcoDebug.ForceHunt(true);
            Assert.IsTrue(pv.Planner.DebugStartHunt(pv.Now, out var plan), "a hunt starts");
            var who = plan.hunter;
            pv.timeScale = 3f;
            float zoom = CampFrameView.Instance.LastZoom > 0f ? CampFrameView.Instance.LastZoom : 0.4f;
            var shot = new HashSet<string>();
            float t0 = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t0 < 150f)
            {
                yield return null;
                HuntPhase ph = HuntPhase.None; float prog = 0f;
                foreach (var a in pv.Activities) if (a.who == who) { ph = a.hunt; prog = a.progress; }
                string tag = ph == HuntPhase.Stalk && prog > 0.4f ? "stalk" : ph == HuntPhase.Strike ? "strike" : ph == HuntPhase.HoldUp ? "holdup" : null;
                if (tag == null || shot.Contains(tag)) { if (shot.Count > 0 && ph == HuntPhase.Return) break; continue; }
                shot.Add(tag);
                pv.timeScale = 0f;
                if (pv.TryGetDollFeet(who, out var feet))
                {
                    rig.FlyTo(feet, zoom, 0.2f);
                    for (int i = 0; i < 100 && rig.IsFlying; i++) yield return null;
                }
                yield return Frames(30);
                Debug.Log($"[b5] hunt {tag}: grass hide {GrassHide.Count} {GrassHide.Describe()} zoom {rig.zoom:0.00}");
                yield return Shot(Dir + "hunt_" + tag);
                pv.timeScale = 3f;
            }
            Debug.Log($"[b5] hunt shots: {string.Join(",", shot)}");
            EcoDebug.ForceHunt(false);
        }
    }
}
