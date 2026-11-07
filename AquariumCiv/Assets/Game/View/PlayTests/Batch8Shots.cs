using System.Collections;
using System.Collections.Generic;
using System.Linq;
using AquariumCiv.Game;
using AquariumCiv.GameUI;
using AquariumCiv.Life;
using AquariumCiv.Sim;
using AquariumCiv.View.Cards;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace AquariumCiv.View.PlayTests
{
    /// <summary>Visit-loop batch 8 looks (the tester run 3 fixes), in a living-land Camp after an Away of 2 h: a choice staged in the
    /// box at 0.5 s and 2 s (no tag before its side arrives, tags apart, a touch on a side answers that side), the supper small choice
    /// staged; a night open with a moment waiting (nothing playing, it plays in the morning, the peek not led by the Keeper's old answer);
    /// a wedding near the tents at the default camera; a hunt hold-up. Shots to Temp/agent/b8/ and the log ([b8]).</summary>
    [Explicit("Screenshots for the eye: run by hand"), Category("Batch8")]
    public class Batch8Shots : PlayTestBase
    {
        const string Dir = "b8/";
        public override void Setup() { base.Setup(); System.IO.Directory.CreateDirectory(Shots + Dir); }
        static SimCore Sim => GameClock.Instance.ActiveSim;
        static BoxCamera Rig => Camera.main.GetComponent<BoxCamera>();
        static ChoiceSceneView Choice => ChoiceSceneView.Instance;
        static CardStackModel Model => CardStackView.Instance.Model;

        IEnumerator LivingCamp(int grownUps = 8)
        {
            GameClock.EcologyPref = true;
            GameUiPanel.AcceptInputUnfocused(true);
            yield return OpenSlice(1179, 2556, seed: 4242);
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
            Debug.Log($"[b8] camp: era {Sim.State.era}, grown-ups {Sim.State.notables.Count(n => n.alive && n.age >= 16f)}");
        }

        static string MomentStates() => string.Join(", ", (Sim.State.moments ?? new List<HeldMoment>()).Select(m => $"#{m.id} {m.kind}:{m.state}"));

        static IEnumerator Calm()
        {
            for (int calm = 0, f = 0; calm < 60 && f < 900; f++) { calm = StagedMoments.StopAll() == 0 && StagedMoments.Playing == 0 ? calm + 1 : 0; yield return null; }
        }

        IEnumerator Away(float hours, string tag)
        {
            var rig = Rig; var view = CampFrameView.Instance;
            CampFrameView.Auto = true;
            int before = view.Framed;
            Debug.Log("[b8] " + GameClock.DebugAway(hours));
            yield return Until(() => view.Framed > before, 15f);
            yield return Until(() => !rig.IsFlying, 4f);
            yield return new WaitForSecondsRealtime(1.2f);
            Debug.Log($"[b8] {tag}: hour {PeopleView.Find().Now.hour:0.00} zoom {rig.zoom:0.000} | moments {MomentStates()} | held {HeldMomentStage.Instance?.LastWhy}");
        }

        /// <summary>The two tags: shown, their screen boxes (panel units scaled to px), and whether they overlap.</summary>
        static string Tags()
        {
            var root = Choice.UiRoot;
            if (root == null) return "no ui";
            float k = root.worldBound.height > 0 ? Screen.height / root.worldBound.height : 1f;
            var r = new Rect[2]; var on = new bool[2]; var s = "";
            for (int i = 0; i < 2; i++)
            {
                var t = root.Q("choice-tag-" + i);
                on[i] = t != null && t.resolvedStyle.display == DisplayStyle.Flex && t.resolvedStyle.opacity > 0.01f;
                if (t != null) r[i] = new Rect(t.worldBound.x * k, t.worldBound.y * k, t.worldBound.width * k, t.worldBound.height * k);
                s += $" tag{i} {(on[i] ? "on" : "off")} a{(t != null ? t.resolvedStyle.opacity : 0f):0.00} settled {Choice.Settled(i)} {r[i].x:0},{r[i].y:0} {r[i].width:0}x{r[i].height:0};";
            }
            if (on[0] && on[1]) s += r[0].Overlaps(r[1]) ? " OVERLAP" : $" apart (gap x {Mathf.Max(r[1].xMin - r[0].xMax, r[0].xMin - r[1].xMax):0} y {Mathf.Max(r[1].yMin - r[0].yMax, r[0].yMin - r[1].yMax):0})";
            return s;
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

        IEnumerator StageAndShoot(Dilemma d, string name, int touchSide)
        {
            Debug.Log($"[b8] {name}: {d.defId} | {HeldMomentStage.DebugStageHeldChoice()}");
            yield return Until(() => Choice.State != ChoiceSceneView.Phase.Idle, 30f);
            if (Choice.State == ChoiceSceneView.Phase.Idle) { Debug.Log("[b8] not staged by the held path; " + DilemmaCheats.StageInBox()); yield return Until(() => Choice.State != ChoiceSceneView.Phase.Idle, 10f); }
            float t0 = Time.realtimeSinceStartup;
            yield return new WaitForSecondsRealtime(0.5f);
            Debug.Log($"[b8] {name} 0.5s:{Tags()} | {ChoiceSceneView.DebugStatus()}");
            yield return Shot(Dir + name + "_0.5s");
            yield return new WaitForSecondsRealtime(Mathf.Max(0f, 2f - (Time.realtimeSinceStartup - t0)));
            Debug.Log($"[b8] {name} 2s:{Tags()}");
            yield return Shot(Dir + name + "_2s");
            yield return Until(() => Choice.Settled(0) && Choice.Settled(1), 6f);
            yield return new WaitForSecondsRealtime(1f);
            Debug.Log($"[b8] {name} settled {Time.realtimeSinceStartup - t0:0.0}s:{Tags()} answerable {Choice.Answerable}");
            yield return Shot(Dir + name + "_settled");
            if (touchSide < 0) yield break;
            if (Choice.TryScreenOf(touchSide, out var sp))
            {
                Debug.Log($"[b8] {name} touch side {touchSide} at {sp} (SideAt {Choice.SideAt(sp, out var dist)} d {dist:0})");
                yield return TapAt(sp);
                yield return Frames(3);
                Debug.Log($"[b8] {name} after the touch: {d.state} option {d.option} path {d.path} scene {Choice.State} -> {(d.option == touchSide ? "THAT SIDE" : "WRONG SIDE")}");
                yield return new WaitForSecondsRealtime(1.5f);
                yield return Shot(Dir + name + "_answered");
            }
            else Debug.Log($"[b8] {name} side {touchSide} not on screen");
        }

        /// <summary>A big choice staged in the box (0.5 s, 2 s, settled, a touch on side 1), then the supper small choice staged.</summary>
        [UnityTest, Timeout(900000)]
        public IEnumerator A_Choice_Tags_Touch_Supper()
        {
            ChoiceSceneView.DebugWatch = false;
            yield return LivingCamp();
            DilemmaCheats.Toggle(true);
            yield return Away(2, "away+2h");
            yield return Calm();
            Dilemma d = Sim.OpenDilemma;
            for (int i = 0; i < 4 && d == null; i++) { d = Raise(); if (d == null) { SimLayoutSource.DebugSkipYears(1f); yield return null; } }
            Assert.IsNotNull(d, "a choice can be raised");
            yield return StageAndShoot(d, "a_big", 1);
            yield return Until(() => Choice.State == ChoiceSceneView.Phase.Idle, 30f);
            yield return Calm();
            Debug.Log("[b8] supper: " + DilemmaCheats.EverydaySmallNow(SmallChoices.Supper));
            var s = Sim.OpenDilemma;
            Assert.IsNotNull(s, "the supper choice is open");
            yield return Frames(10);
            yield return Shot(Dir + "a_supper_card");
            yield return StageAndShoot(s, "a_supper", 0);
        }

        /// <summary>A night open with a wedding waiting: nothing playing at night, it plays in the morning; the peek after an answer.</summary>
        [UnityTest, Timeout(900000)]
        public IEnumerator B_Night_Open()
        {
            ChoiceSceneView.DebugWatch = false;
            yield return LivingCamp();
            DilemmaCheats.Toggle(true);
            yield return Away(2, "away+2h");
            yield return Calm();
            // The Keeper answers a choice (on its card), then leaves.
            var d = Sim.OpenDilemma ?? Raise();
            Assert.IsNotNull(d, "a choice");
            Debug.Log("[b8] answer: " + DilemmaCheats.AnswerOnCard(0) + $" | answer entry #{d.answerEntryId}");
            yield return Frames(10);
            yield return Calm();
            PeopleView.DebugSetHour(22.5f);
            yield return Frames(10);
            var m = Sim.DebugQueueMoment(MomentKind.Wedding) ?? Sim.DebugQueueMoment(MomentKind.Festival);
            Debug.Log($"[b8] queued {(m != null ? m.kind.ToString() : "none")} at hour {PeopleView.Find().Now.hour:0.00}; nightwaits {StagedMoments.NightWaits}");
            yield return Away(1, "night open");
            if (PeopleView.Find().Now.hour < 21.5f && PeopleView.Find().Now.hour >= 6f)
            {
                PeopleView.DebugSetHour(22.5f);
                Debug.Log("[b8] the open landed in the day: set 22.5 h; keeper arrives: " + Sim.DebugKeeperArrives());
                Sim.DebugMomentDueNow();
                HeldMomentStage.Instance.Check();
            }
            yield return new WaitForSecondsRealtime(3f);
            var peekLead = Model.PeekIs(d.answerEntryId);
            Debug.Log($"[b8] night: hour {PeopleView.Find().Now.hour:0.00} playing {StagedMoments.Playing} nightwaits {StagedMoments.NightWaits} | moments {MomentStates()} | held {HeldMomentStage.Instance?.LastWhy} | {StagedMoments.LastWhy}");
            Debug.Log($"[b8] peek: mode {Model.Mode} '{Model.Peek.text}' teaser {Model.Peek.isTeaser} | led by the Keeper's old answer: {peekLead}");
            Assert.IsFalse((Sim.State.moments ?? new List<HeldMoment>()).Any(x => x.state == HeldMomentState.Playing), "nothing Playing at night");
            yield return Shot(Dir + "b_night_open");
            // The morning.
            int started = StagedMoments.Started;
            PeopleView.DebugSetHour(6.3f);
            Sim.DebugMomentDueNow();
            yield return Until(() => StagedMoments.Playing > 0 || StagedMoments.Started > started, 60f);
            Debug.Log($"[b8] dawn: hour {PeopleView.Find().Now.hour:0.00} playing {StagedMoments.Playing} started {started}->{StagedMoments.Started} | moments {MomentStates()} | held {HeldMomentStage.Instance?.LastWhy}");
            yield return new WaitForSecondsRealtime(6f);
            yield return Shot(Dir + "b_dawn_plays");
        }

        /// <summary>A wedding at the camp frame (the default camera after the away): the couple and the arch in the open.</summary>
        [UnityTest, Timeout(900000)]
        public IEnumerator C_Wedding()
        {
            yield return LivingCamp();
            yield return Away(2, "away+2h");
            yield return Calm();
            yield return new WaitForSecondsRealtime(1f);
            Debug.Log("[b8] wedding: " + StagedMoments.DebugPlayNow(MomentKind.Wedding));
            float w0 = Time.realtimeSinceStartup;
            foreach (var t in new[] { 4f, 10f, 18f, 26f })
            {
                yield return new WaitForSecondsRealtime(Mathf.Max(0f, t - (Time.realtimeSinceStartup - w0)));
                Debug.Log($"[b8] wedding {t:0}s: playing {StagedMoments.Playing} zoom {Rig.zoom:0.00} | {StagedMoments.DebugStatus()}");
                yield return Shot(Dir + $"c_wedding_{t:0}s");
            }
        }

        /// <summary>A hunt that catches: the hold-up (the hunter faces the camera, the rabbit held high).</summary>
        [UnityTest, Timeout(900000)]
        public IEnumerator D_Hunt_HoldUp()
        {
            yield return LivingCamp(6);
            yield return Away(2, "hunt away+2h");
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
                string tag = ph == HuntPhase.Strike && prog > 0.5f ? "strike" : ph == HuntPhase.HoldUp && prog > 0.7f ? "holdup2" : ph == HuntPhase.HoldUp && prog > 0.3f ? "holdup" : null;
                if (tag == null || shot.Contains(tag)) { if (shot.Count > 0 && ph == HuntPhase.Return) break; continue; }
                shot.Add(tag);
                pv.timeScale = 0f;
                yield return Frames(20);
                string where = pv.TryGetDollFeet(who, out var feet) ? cam.WorldToScreenPoint(feet).ToString("0") : "?";
                Debug.Log($"[b8] hunt {tag}: hunter at screen {where} zoom {Rig.zoom:0.00}");
                yield return Shot(Dir + "d_hunt_" + tag);
                if (pv.TryGetDollFeet(who, out var f2))
                {
                    // A close look at the hunter (the camp frame is wide): fly the rig close, shoot, put it back.
                    var rig = Rig; var (tg, z) = (rig.target, rig.zoom);
                    CampFrameView.Auto = false; rig.target = f2 + Vector3.up * 0.05f; rig.zoom = 0.12f; rig.Apply();
                    yield return Frames(3);
                    yield return Shot(Dir + "d_hunt_" + tag + "_close");
                    rig.target = tg; rig.zoom = z; rig.Apply();
                }
                pv.timeScale = 3f;
            }
            Debug.Log($"[b8] hunt shots: {string.Join(",", shot)}");
            EcoDebug.ForceHunt(false);
        }

        public override void TearDown()
        {
            ChoiceSceneView.DebugWatch = false;
            base.TearDown();
        }
    }
}
