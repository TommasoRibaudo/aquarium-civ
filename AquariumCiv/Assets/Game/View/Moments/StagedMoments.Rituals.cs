using System;
using AquariumCiv.Game;
using AquariumCiv.Life;
using AquariumCiv.Sim;
using AquariumCiv.View.ShowMe;
using UnityEngine;

namespace AquariumCiv.View
{
    /// <summary>
    /// S-ritual (B72-B75, animation-specs "Rituals and the teller"): the yearly rituals staged with their own beats. The planner builds each
    /// ritual's plan (Life/MomentStagePlan.Rituals.cs) on the targets CampLife reads (<see cref="CampLife.RitualTargetsFor"/>); here the
    /// people view asks each frame what the beat looks like (<see cref="RitualBeats"/>), and each beat's line is said once, as it starts,
    /// by whoever the plan gives it to (the people's random moment lines stay quiet: the beats speak). The cheats in SimDebugPanel's
    /// "Rituals" block set this year's variant and play it now.
    /// </summary>
    public static partial class StagedMoments
    {
        static void InstallRituals()
        {
            PeopleView.StagedLook = (in PersonActivity a, bool w, ref DollStyle s, ref PropKind k, ref PropSlot sl) => LookHook(a, w, ref s, ref k, ref sl) || (PeopleView.Find()?.Camp?.TellerLook(a, w, ref s) ?? false);
            PeopleView.RitualPose = (in PersonActivity a, bool w, float stride, ref DollPose p) => { RitualPoseHook(a, w, stride, ref p); PeopleView.Find()?.Camp?.TellerPose(a, w, ref p); };
            PeopleView.TellerSpot = (in PersonActivity a, ViewTime now, out float x, out float z, out Vector2? face) => { x = z = 0f; face = null; var c = PeopleView.Find()?.Camp; return c != null && c.TellerSpot(a, out x, out z, out face); };
            LifePlanner.RitualTargets = (sim, m, ritual) => PeopleView.Find()?.Camp?.RitualTargetsFor(sim, m, ritual);
        }

        static void UninstallRituals()
        {
            PeopleView.StagedLook = null;
            PeopleView.RitualPose = null;
            PeopleView.TellerSpot = null;
            LifePlanner.RitualTargets = null;
        }

        /// <summary>The ritual staged with its own beats playing now in <paramref name="sim"/>'s box, and how far in (real seconds).</summary>
        public static bool PlayingRitual(SimCore sim, out HeldMomentScene scene, out float secs)
        {
            scene = null; secs = 0f;
            if (sim == null) return false;
            foreach (var l in lives)
            {
                if (l.scene == null || l.over || l.pv == null || !ReferenceEquals(l.sim, sim) || l.scene.plan?.RitualStaged == null) continue;
                var now = l.pv.Now;
                if (now.day != l.scene.day || now.hour < l.scene.start || now.hour >= l.scene.end) continue;
                scene = l.scene; secs = LifeClock.RealSecondsBetween(l.scene.start, now.hour);
                return true;
            }
            return false;
        }

        /// <summary>A ritual with its own beats is playing: its lines are the beats' own (ShowMeDirector leaves the moment's random lines out).</summary>
        public static bool SpeaksItsOwnLines(SimCore sim) => PlayingRitual(sim, out _, out _);

        static bool RitualStep(in PersonActivity a, out Live live, out HeldMomentScene sc, out MomentMember m, out int i, out float secs)
        {
            live = null; sc = null; m = null; i = -1; secs = 0f;
            if (lives.Count == 0) return false;
            Live any = null;
            foreach (var l in lives) if (l.pv != null && l.scene?.plan?.RitualStaged != null) { any = l; break; }
            if (any == null) return false;
            if (!Find(a, any.pv.Now, out live, out sc, out m, out i, out secs)) return false;
            return sc.plan.RitualStaged != null && RitualBeats.Handles(m.steps[i].beat);
        }

        static bool LookHook(in PersonActivity a, bool walking, ref DollStyle style, ref PropKind kind, ref PropSlot slot)
        {
            if (!RitualStep(a, out _, out var sc, out var m, out int i, out float secs)) return false;
            if (m.steps[i].travel && !walking && !a.moving && m.steps[i].hold == StageHold.None) return false; // arrived empty-handed: as the planner has them
            RitualBeats.Look(sc.plan, m, i, secs, walking || a.moving, ref style, ref kind, ref slot);
            return true;
        }

        static void RitualPoseHook(in PersonActivity a, bool walking, float stride, ref DollPose pose)
        {
            if (!RitualStep(a, out var live, out var sc, out var m, out int i, out float secs)) return;
            var st = m.steps[i];
            RitualBeats.Apply(ref pose, sc.plan, m, i, secs, stride, walking || st.travel && a.moving);
            // each beat's line, once, as the step starts (its speaker's ring and mark on the one caption)
            if (st.line != null && secs - st.t0 < 1.5f && live.said.Add(((long)m.key << 8) | (uint)i))
            {
                string text = st.line.Replace("{keeper}", PeopleVoice.KeeperName(live.sim));
                if (PeopleVoice.Allowed(text, live.sim)) ShowMeDirector.Instance?.Say(text, a.who, BeatLineSeconds, LineKind.Echo);
                LinesSaid++;
            }
        }

        /// <summary>S-ritual: a beat's line stays up this long (real seconds) [est].</summary>
        public const float BeatLineSeconds = 3.2f;
        /// <summary>Tests: the beats' lines said so far.</summary>
        public static int LinesSaid;

        // ---------------------------------------------------------------- cheats (SimDebugPanel "Rituals (#721)")

        static string Variant(string ritual, Action set, string what)
        {
            set();
            string r = DebugPlayRitualNow(ritual);
            var sc = LastScene;
            string staged = sc != null && sc.kind == MomentKind.Ritual ? sc.plan?.RitualStaged ?? "the festival's stand-in (" + MomentStagePlan.LastWhyNot + ")" : "";
            return $"{what}: {r}{(staged.Length > 0 ? "; staged as " + staged : "")}";
        }

        /// <summary>"Berrying: end at the long table": the box keeps the feast too (its long table stands), and the walk ends there.</summary>
        public static string DebugBerryingToTable()
        {
            var sim = Sim;
            if (sim == null) return "no living box";
            string custom = RitualKind.CustomOf(RitualKind.Feast);
            sim.DebugPlayRitual(RitualKind.Feast); // keeps the feast (its custom) and its table's first year
            StopAll();
            if (!sim.State.consequenceProps.Exists(p => p.kind == ConsequenceKind.LongTable)) ConsequenceCheats.PlaceEvery();
            return Variant(RitualKind.Berrying, () => CampLife.NextToTable = true, "berrying to the long table" + (sim.State.era == Era.Camp ? " (a camp: 'Jump: a village box' first for the village)" : ""));
        }

        public static string DebugBerryingFirstWalk() => Variant(RitualKind.Berrying, () => CampLife.NextFirstWalk = true, "a kid's first walk");
        public static string DebugLogLineDrop() => Variant(RitualKind.Woodpile, () => CampLife.NextDropLog = true, "the log line, a log dropped");
        public static string DebugFeastBoard()
        {
            var sim = Sim;
            if (sim != null && !sim.State.consequenceProps.Exists(p => p.kind == ConsequenceKind.LongTable)) ConsequenceCheats.PlaceEvery();
            return Variant(RitualKind.Feast, () => { CampLife.NextAddBoard = true; CampLife.CheatBoards++; }, "the feast, a year's board added");
        }
        public static string DebugOfferingTallestTree() => Variant(RitualKind.Offering, () => CampLife.NextTallestTree = true, "the offering to the tallest tree");

        /// <summary>"Offering: empty it now (spring)": the spring line is written and the first kid finds the basket empty now.</summary>
        public static string DebugOfferingEmpty()
        {
            var sim = Sim;
            if (sim == null) return "no living box";
            if (!sim.State.consequenceProps.Exists(p => p.kind == ConsequenceKind.OfferingBasket)) ConsequenceCheats.PlaceEvery();
            StopAll();
            sim.DebugEmptyOffering();
            var pv = PeopleView.Find();
            if (pv != null && (pv.Now.hour < 7f || pv.Now.hour >= 17f)) PeopleView.DebugSetHour(8f);
            return "the offering basket is empty; " + CampCheats.OfferingEmpty();
        }
    }

    public static partial class CampCheats
    {
        public static string OfferingEmpty() => Do(CampUse.OfferingEmpty);
        /// <summary>"Boat: sort the catch on it" (B77.a).</summary>
        public static string BoatSort() => Do(CampUse.BoatSort);
        /// <summary>"Boat: kids play house" (B77.b).</summary>
        public static string BoatHouse() => Do(CampUse.BoatHouse);
    }
}
