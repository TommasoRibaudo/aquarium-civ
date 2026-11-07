using System.Collections;
using System.Collections.Generic;
using System.Linq;
using AquariumCiv.Game;
using AquariumCiv.GameUI;
using AquariumCiv.Life;
using AquariumCiv.Lighting;
using AquariumCiv.Sim;
using AquariumCiv.View.Cards;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace AquariumCiv.View.PlayTests
{
    /// <summary>Visit-loop batch 7 looks, in a living-land Camp of ~8 after an Away of 2 h: the camp frame (doll px, the near glass),
    /// the greeting on a quiet return, a resize re-framing the camp, the held choice staged and still there 10 s later (bar + Let them
    /// decide), a touch answering it, a live choice taking the peek with "?", and a hunt catch inside the camp frame. Shots to
    /// Temp/agent/b7/ and the log ([b7]).</summary>
    [Explicit("Screenshots for the eye: run by hand"), Category("Batch7")]
    public class Batch7Shots : PlayTestBase
    {
        const string Dir = "b7/";
        public override void Setup() { base.Setup(); System.IO.Directory.CreateDirectory(Shots + Dir); }
        static SimCore Sim => GameClock.Instance.ActiveSim;
        static BoxCamera Rig => Camera.main.GetComponent<BoxCamera>();
        static ChoiceSceneView Choice => ChoiceSceneView.Instance;
        static CardStackModel Model => CardStackView.Instance.Model;

        IEnumerator LivingCamp(int w, int h, int grownUps = 8)
        {
            GameClock.EcologyPref = true;
            GameUiPanel.AcceptInputUnfocused(true);
            yield return OpenSlice(w, h, seed: 4242);
            GameUiPanel.AcceptInputUnfocused(true);
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
            Debug.Log($"[b7] camp: era {Sim.State.era}, grown-ups {Sim.State.notables.Count(n => n.alive && n.age >= 16f)}, all {Sim.State.notables.Count(n => n.alive)}");
        }

        static string MomentStates() => string.Join(", ", (Sim.State.moments ?? new List<HeldMoment>()).Select(m => $"{m.kind}:{m.state}"));

        static IEnumerator Calm()
        {
            for (int calm = 0, f = 0; calm < 60 && f < 900; f++) { calm = StagedMoments.StopAll() == 0 && StagedMoments.Playing == 0 ? calm + 1 : 0; yield return null; }
        }

        static string DollPx()
        {
            var pv = PeopleView.Find(); var cam = Camera.main;
            var px = new List<float>();
            var who = new List<PersonRef>(); var xs = new List<float>(); var zs = new List<float>();
            pv.StandingNow(who, xs, zs);
            for (int i = 0; i < who.Count; i++)
            {
                if (!pv.TryGetDollFeet(who[i], out var feet)) continue;
                var a = cam.WorldToScreenPoint(feet);
                var b = cam.WorldToScreenPoint(feet + Vector3.up * CampFrame.DollHeight * pv.DollZoom * pv.transform.lossyScale.y);
                if (a.z > 0f && a.x > 0 && a.x < Screen.width && a.y > 0f && a.y < Screen.height) px.Add(Mathf.Abs(b.y - a.y));
            }
            px.Sort();
            float median = px.Count > 0 ? px[px.Count / 2] : 0f;
            return $"dolls {who.Count}, on screen {px.Count}, px {string.Join(",", px.Select(p => p.ToString("0")))}, median {median:0}";
        }

        static string Glass()
        {
            var box = Object.FindAnyObjectByType<BoxView>();
            return $"panes {box.PaneOpacity(0):0.00}/{box.PaneOpacity(1):0.00}/{box.PaneOpacity(2):0.00}/{box.PaneOpacity(3):0.00} frames {box.FrameOpacity(0):0.00}/{box.FrameOpacity(2):0.00}/{box.FrameOpacity(4):0.00}";
        }

        IEnumerator AwayAndFrame(float hours, string tag)
        {
            var rig = Rig; var view = CampFrameView.Instance;
            CampFrameView.Auto = true;
            int before = view.Framed;
            Debug.Log("[b7] " + GameClock.DebugAway(hours));
            yield return Until(() => view.Framed > before, 15f);
            yield return Until(() => !rig.IsFlying, 4f);
            yield return new WaitForSecondsRealtime(1.2f);
            Debug.Log($"[b7] {tag} {Screen.width}x{Screen.height}: framed {view.Framed} ({view.LastWhy}) {CampFrameView.Status()} | zoom {rig.zoom:0.000} | {DollPx()} | {Glass()} | moments {MomentStates()} | held {HeldMomentStage.Instance?.LastWhy}");
        }

        /// <summary>The camp frame at the phone size, the greeting, then a resize to 1080x1920 re-frames the camp.</summary>
        [UnityTest, Timeout(900000)]
        public IEnumerator A_Frame_Greeting_Resize()
        {
            yield return LivingCamp(1179, 2556);
            yield return AwayAndFrame(2, "away+2h");
            yield return Shot(Dir + "a1_frame_1179x2556");
            // The greeting on a quiet return: the away's own, or the cheat.
            float t0 = Time.realtimeSinceStartup;
            Debug.Log("[b7] the away played: " + MomentStates() + " | " + HeldMomentStage.Instance?.LastWhy);
            yield return Calm(); // the away brought a ceremony (the welcome itself); stop it for a quiet greeting
            yield return new WaitForSecondsRealtime(2f);
            Debug.Log("[b7] greeting cheat: " + HeldMomentStage.DebugWelcomeNow());
            yield return Until(() => HeldMomentStage.Instance.LastGreeting != null, 20f);
            Debug.Log($"[b7] greeting after {Time.realtimeSinceStartup - t0:0}s: {HeldMomentStage.Instance?.LastWhy}; greeting {HeldMomentStage.Instance.LastGreeting?.kind} playing {StagedMoments.Playing}");
            yield return new WaitForSecondsRealtime(3f);
            yield return Shot(Dir + "a2_greeting_3s");
            yield return new WaitForSecondsRealtime(4f);
            yield return Shot(Dir + "a3_greeting_7s");
            yield return Until(() => StagedMoments.Playing == 0, 60f);
            yield return Calm();
            var rig = Rig; var view = CampFrameView.Instance;
            yield return Until(() => !rig.IsFlying, 4f);
            float zBefore = rig.zoom; int fBefore = view.Framed;
            SetSize(1080, 1920);
            yield return Frames(5);
            yield return Until(() => view.Framed > fBefore, 15f);
            yield return Until(() => !rig.IsFlying, 4f);
            yield return new WaitForSecondsRealtime(1.2f);
            Debug.Log($"[b7] resize 1179x2556 -> {Screen.width}x{Screen.height}: framed {fBefore}->{view.Framed} ({view.LastWhy}) zoom {zBefore:0.000}->{rig.zoom:0.000} | {DollPx()} | {Glass()}");
            yield return Shot(Dir + "a4_resized_1080x1920");
        }

        [UnityTest, Timeout(600000)]
        public IEnumerator B_Frame_Small()
        {
            yield return LivingCamp(1080, 1920);
            yield return AwayAndFrame(2, "away+2h");
            yield return Shot(Dir + "b1_frame_1080x1920");
        }

        Dilemma Raise()
        {
            foreach (var def in new[] { "camp_fire", "camp_remember", "camp_winter", "camp_travellers" })
            {
                var d = Sim.DebugRaiseDilemma(def);
                if (d != null) return d;
            }
            return null;
        }

        /// <summary>The held choice staged on the open, 10 s later, answered by a touch; then a live choice takes the peek with "?".</summary>
        [UnityTest, Timeout(900000)]
        public IEnumerator C_HeldChoice_Touch_LiveChoice()
        {
            ChoiceSceneView.DebugWatch = false;
            yield return LivingCamp(1179, 2556);
            DilemmaCheats.Toggle(true);
            Dilemma d = null;
            for (int i = 0; i < 4 && d == null; i++) { d = Raise(); if (d == null) { SimLayoutSource.DebugSkipYears(1f); yield return null; } }
            Assert.IsNotNull(d, "a choice can be raised");
            Debug.Log($"[b7] raised {d.defId}");
            yield return AwayAndFrame(2, "choice away+2h");
            yield return Until(() => Choice.State == ChoiceSceneView.Phase.Waiting, 90f);
            Debug.Log($"[b7] choice: {ChoiceSceneView.DebugStatus()} | held {HeldMomentStage.Instance?.LastWhy}");
            yield return new WaitForSecondsRealtime(2f);
            yield return Shot(Dir + "c1_choice_staged");
            yield return new WaitForSecondsRealtime(10f);
            Debug.Log($"[b7] choice 10 s later: {Choice.State} bar {Choice.BoxBarUp} answerable {Choice.Answerable} | {ChoiceSceneView.DebugStatus()}");
            var let = Choice.UiRoot.Q("choice-let");
            if (let != null) Debug.Log($"[b7] let-them-decide box {let.worldBound} bg {let.resolvedStyle.backgroundColor}");
            for (int i = 0; i < 2; i++) { var tag = Choice.UiRoot.Q("choice-tag-" + i); if (tag != null) Debug.Log($"[b7] tag {i} box {tag.worldBound}"); }
            yield return Shot(Dir + "c2_choice_10s_later");
            if (Choice.TryScreenOf(0, out var s0))
            {
                yield return TapAt(s0);
                yield return Frames(3);
                Debug.Log($"[b7] after the touch: {d.state} path {d.path} scene {Choice.State}");
                yield return new WaitForSecondsRealtime(1.5f);
                yield return Shot(Dir + "c3_touch_answered");
            }
            else Debug.Log("[b7] option 0 not on screen");
            yield return Until(() => Choice.State == ChoiceSceneView.Phase.Idle, 30f);
            yield return Calm();

            // A live choice.
            Model.DebugPrepare(3);
            yield return Frames(4);
            Dilemma d2 = null;
            for (int i = 0; i < 4 && d2 == null; i++) { d2 = Raise(); if (d2 == null) { SimLayoutSource.DebugSkipYears(1f); yield return null; } }
            Assert.IsNotNull(d2, "a second choice can be raised");
            Sim.DebugHoldChoiceForOpen();
            yield return Until(() => Model.PeekIs(d2.entryId), 10f);
            Debug.Log($"[b7] live {d2.defId}: peek is it {Model.PeekIs(d2.entryId)}, ? {CardStackView.Instance.Root.Q("cards-peek")?.ClassListContains("kg-peek--choice")}");
            yield return Shot(Dir + "c4_live_peek");
            yield return Until(() => Choice.State == ChoiceSceneView.Phase.Waiting, 60f);
            yield return new WaitForSecondsRealtime(2f);
            Debug.Log($"[b7] live staged: {Choice.State} in box {Choice.InBox}");
            yield return Shot(Dir + "c5_live_staged");
        }

        /// <summary>A hunt that catches, at the camp frame the away left: the strike and the hold-up, with no fly to the hunter.</summary>
        [UnityTest, Timeout(900000)]
        public IEnumerator D_HuntCatch_InTheFrame()
        {
            yield return LivingCamp(1179, 2556, 6);
            yield return AwayAndFrame(2, "hunt away+2h");
            yield return Calm();
            var pv = PeopleView.Find(); var rv = RabbitView.Instance; var cam = Camera.main;
            PeopleView.DebugSetHour(9f);
            yield return Frames(30);
            yield return Until(() => !rv.SignPlaying && rv.Shown >= 3, 15f);
            EcoDebug.ForceHunt(true);
            Assert.IsTrue(pv.Planner.DebugStartHunt(pv.Now, out var plan), "a hunt starts");
            var who = plan.hunter;
            pv.timeScale = 3f;
            var shot = new HashSet<string>();
            float t0 = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t0 < 150f)
            {
                yield return null;
                HuntPhase ph = HuntPhase.None; float prog = 0f;
                foreach (var a in pv.Activities) if (a.who == who) { ph = a.hunt; prog = a.progress; }
                string tag = ph == HuntPhase.Strike && prog > 0.5f ? "strike" : ph == HuntPhase.HoldUp && prog > 0.3f ? "holdup" : null;
                if (tag == null || shot.Contains(tag)) { if (shot.Count > 0 && ph == HuntPhase.Return) break; continue; }
                shot.Add(tag);
                pv.timeScale = 0f;
                yield return Frames(20);
                string where = pv.TryGetDollFeet(who, out var feet) ? cam.WorldToScreenPoint(feet).ToString("0") : "?";
                Debug.Log($"[b7] hunt {tag}: hunter at screen {where}, grass hide {GrassHide.Count} {GrassHide.Describe()} zoom {Rig.zoom:0.00}");
                yield return Shot(Dir + "d_hunt_" + tag);
                pv.timeScale = 3f;
            }
            Debug.Log($"[b7] hunt shots: {string.Join(",", shot)}");
            EcoDebug.ForceHunt(false);
        }

        public override void TearDown()
        {
            ChoiceSceneView.DebugWatch = false;
            base.TearDown();
        }
    }
}
