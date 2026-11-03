using AquariumCiv.Sim;
using System;
using System.Collections.Generic;

namespace AquariumCiv.Game
{
    /// <summary>
    /// P7 (#225): keeps the dilemma events honest so the View does not diff by hand. <see cref="Observe"/> after each sim step logs
    /// <c>dilemma_raised</c> / <c>dilemma_lapsed</c> for dilemmas it has not reported yet; <see cref="Shown"/> /
    /// <see cref="Answered"/> / <see cref="CallbackShown"/> are the UI-side calls (the tracker times shown-to-answered). Pure C#,
    /// the clock is passed in (real seconds). Dilemmas present at construction (a loaded save) count as already reported.
    /// </summary>
    public class DilemmaTelemetryTracker
    {
        readonly HashSet<int> raised = new HashSet<int>(), lapsed = new HashSet<int>(), answered = new HashSet<int>(), shownIds = new HashSet<int>();
        readonly Dictionary<int, double> shownAt = new Dictionary<int, double>();

        readonly HashSet<int> cheatIds = new HashSet<int>();

        /// <summary>#239: the debug cheats call this for a dilemma they raise, answer or lapse. Every event logged for that dilemma from now
        /// on carries <c>debug=1</c>, and the analyzer leaves the whole dilemma out of the lab numbers.</summary>
        public void MarkCheat(int dilemmaId) => cheatIds.Add(dilemmaId);
        public bool IsCheat(int dilemmaId) => cheatIds.Contains(dilemmaId);

        public DilemmaTelemetryTracker(SimCore sim = null)
        {
            if (sim?.State?.dilemmas == null) return;
            foreach (var d in sim.State.dilemmas)
            {
                raised.Add(d.id);
                if (d.state == DilemmaState.Lapsed) lapsed.Add(d.id);
                if (d.state == DilemmaState.Answered) answered.Add(d.id);
            }
        }

        /// <summary>Call after every sim step (or each frame): logs what changed since the last call. Returns how many events it wrote.</summary>
        public int Observe(SimCore sim, TelemetryLog log)
        {
            if (sim?.State?.dilemmas == null) return 0;
            int n = 0;
            foreach (var d in sim.State.dilemmas)
            {
                if (raised.Add(d.id)) { log.DilemmaRaised(d, IsCheat(d.id)); n++; }
                if (d.state == DilemmaState.Lapsed && lapsed.Add(d.id)) { log.DilemmaLapsed(d, IsCheat(d.id)); n++; }
            }
            return n;
        }

        /// <summary>The card (or the box) put this dilemma in front of the player. Logs once per id; a re-show later changes nothing.</summary>
        public bool Shown(TelemetryLog log, Dilemma d, double now)
        {
            if (d == null || !shownIds.Add(d.id)) return false;
            shownAt[d.id] = now;
            log.DilemmaShown(d, IsCheat(d.id));
            return true;
        }

        /// <summary>The player answered (<see cref="Dilemma.state"/> is already Answered, so path and end month are set). Logs once per id.</summary>
        public bool Answered(TelemetryLog log, Dilemma d, double now)
        {
            if (d == null || d.state != DilemmaState.Answered || !answered.Add(d.id)) return false;
            double? secs = shownAt.TryGetValue(d.id, out var t) ? Math.Max(0, now - t) : (double?)null;
            log.DilemmaAnswered(d, secs, IsCheat(d.id));
            return true;
        }

        /// <summary>A callback card (kind <see cref="SimCore.CallbackKind"/>) is now in front of the player.</summary>
        public void CallbackShown(TelemetryLog log, CallbackInfo info) => log.CallbackShown(info);
    }
}
