using System.Globalization;
using System.Linq;
using System.Text;
using AquariumCiv.Game;
using AquariumCiv.Sim;
using AquariumCiv.View.Cards;
using UnityEngine;

namespace AquariumCiv.View
{
    /// <summary>
    /// The P7 dilemma cheats (#225), for the debug panel's "Dilemmas (P7)" section, `script eval` and play tests: the A/B switch, the
    /// lab preset, raise any of the eight now, answer on the card or in the box, lapse, fire the next callback, and a list of what is
    /// open, answered, lapsed and still to come. Every call returns a line for the panel. The events they cause go to the telemetry log
    /// as a player's would (the card path through the card stack, the box path through the choice scene).
    /// </summary>
    public static class DilemmaCheats
    {
        /// <summary>The eight dilemmas, in the order the panel shows them.</summary>
        public static readonly string[] Ids =
        {
            "camp_fire", "camp_winter", "camp_remember", "camp_travellers",
            "village_headman", "village_spring", "village_harvest", "village_hill_family",
        };

        static readonly CultureInfo Ci = CultureInfo.InvariantCulture;
        static SimCore Sim => GameClock.Instance != null ? GameClock.Instance.ActiveSim : null;

        static string Saved(string line)
        {
            if (GameClock.Instance != null) GameClock.Instance.Save();
            return line;
        }

        public static string Toggle(bool on) => GameClock.DebugDilemmas(on);
        public static string Lab(bool on) => GameClock.DebugLab(on);
        public static bool On => GameClock.Instance != null && GameClock.Instance.config.dilemmas;
        public static bool LabOn => GameClock.Instance != null && GameClock.Instance.config.dilemmaLab;

        /// <summary>Why <paramref name="id"/> can't be raised in this box now (null = it can).</summary>
        public static string WhyNot(string id)
        {
            var sim = Sim;
            return sim == null ? "no box" : sim.DilemmaWhyNot(id);
        }

        /// <summary>Raises <paramref name="id"/> now (an open one lapses first: the slot is shared) and opens the stack on its card.</summary>
        public static string Raise(string id)
        {
            var sim = Sim;
            if (sim == null) return "no box";
            string why = sim.DilemmaWhyNot(id);
            if (why == SimCore.NobodyDiedWhy) { sim.DebugKillOldest(); why = sim.DilemmaWhyNot(id); } // #346: the cheat brings the death it waits for
            if (why != null) return id + " can't be raised here: " + why;
            var d = sim.DebugRaiseDilemma(id);
            if (d == null) return id + " can't be raised here";
            GameClock.Instance?.DilemmaTracker()?.MarkCheat(d.id); // #239: its events carry debug=1
            GameClock.Instance?.DilemmaTracker()?.Observe(sim, GameClock.Tel); // the raise is logged now, not on the next frame
            string card = CardStackView.DebugOpenEntry(d.entryId);
            return Saved($"raised {id} (#{d.id}); {card}");
        }

        /// <summary>Answers the open dilemma with option <paramref name="option"/> (0 or 1) on the Card path: through the card when it is on
        /// screen, else straight into the sim.</summary>
        public static string AnswerOnCard(int option)
        {
            var sim = Sim;
            var d = sim?.OpenDilemma;
            if (d == null) return "no open dilemma";
            GameClock.Instance?.DilemmaTracker()?.MarkCheat(d.id);
            var m = CardStackView.Instance != null ? CardStackView.Instance.Model : null;
            bool onCard = m != null && m.Face != null && m.Face.kind == CardKind.Dilemma && m.Shown != null && m.Shown.lead.id == d.entryId;
            bool ok = onCard ? m.AnswerDilemma(option) : sim.AnswerDilemma(d.id, option, DilemmaPath.Card);
            if (!ok) return "could not answer";
            if (!onCard) GameClock.DilemmaAnswered(d);
            return Saved($"answered {d.defId} with option {option + 1} on the card ({Label(sim, d, option)})");
        }

        public static string StageInBox() => ChoiceSceneView.DebugStage();
        public static string AnswerInBox(int option) { var od = Sim?.OpenDilemma; if (od != null) GameClock.Instance?.DilemmaTracker()?.MarkCheat(od.id); var r = ChoiceSceneView.DebugAnswer(option); return Saved(r); }
        public static string LeaveBox() => ChoiceSceneView.DebugLeave();
        public static string BoxStatus() => ChoiceSceneView.DebugStatus();

        /// <summary>The people decide the open dilemma now (the default option).</summary>
        public static string Lapse()
        {
            var sim = Sim;
            var d = sim?.OpenDilemma;
            if (d != null) GameClock.Instance?.DilemmaTracker()?.MarkCheat(d.id);
            if (d == null || !sim.DebugLapseDilemma()) return "no open dilemma";
            GameClock.Instance?.DilemmaTracker()?.Observe(sim, GameClock.Tel);
            return Saved($"{d.defId} lapsed: the people chose option {d.option + 1}");
        }

        /// <summary>Fires the next pending callback now and opens the stack on its card.</summary>
        public static string FireCallback()
        {
            var sim = Sim;
            if (sim == null) return "no box";
            var e = sim.DebugFireCallbackNow();
            if (e == null) return "no callback left to tell";
            string card = CardStackView.DebugOpenEntry(e.id);
            return Saved($"callback: {e.text} ({card})");
        }

        static string Label(SimCore sim, Dilemma d, int option)
        {
            var v = sim.ViewOfDilemma(d);
            return v?.options != null && option >= 0 && option < v.options.Length ? v.options[option].label : "?";
        }

        /// <summary>The panel's list: switches, the open dilemma, every answered and lapsed one, callbacks to come, apart groups.</summary>
        public static string List()
        {
            var sim = Sim;
            if (sim == null) return "no box";
            var sb = new StringBuilder();
            sb.Append(GameClock.Instance != null ? GameClock.Instance.DilemmaStatus() : "no clock").Append('\n');
            foreach (var d in sim.State.dilemmas)
            {
                string how = d.state == DilemmaState.Open ? "OPEN"
                    : d.state == DilemmaState.Answered ? $"answered {d.option + 1} ({d.path.ToString().ToLowerInvariant()})" : $"lapsed to {d.option + 1}";
                sb.AppendFormat(Ci, "#{0} {1} · {2} · year {3:0.0}\n", d.id, d.defId, how, d.month / (float)SimConfig.MonthsPerYear);
            }
            var pend = sim.PendingCallbacks();
            if (pend.Count == 0) sb.Append("no callbacks to come\n");
            foreach (var c in pend)
            {
                var d = sim.FindDilemma(c.dilemmaId);
                sb.AppendFormat(Ci, "callback {0} of {1}, option {2}, due year {3:0.0}\n", c.index + 1, d?.defId ?? "?", c.option + 1, c.dueMonth / (float)SimConfig.MonthsPerYear);
            }
            foreach (var g in sim.ApartGroups)
                sb.AppendFormat(Ci, "apart: the {0} family, {1} at ({2:0.00}, {3:0.00}), {4} of {5} alive\n", g.family, g.kind, g.x, g.z, g.members.Count(n => n.alive), g.members.Count);
            return sb.ToString().TrimEnd('\n');
        }

        /// <summary>The dilemma measures from the log so far (the analyzer's line for each variant), also to the console.</summary>
        public static string Report()
        {
            var log = GameClock.Tel;
            if (log == null) return "no telemetry running";
            var r = TelemetryAnalyzer.Analyze(log.ReadAllLines(), GameClock.TelemetryNow);
            if (!r.dilemmas.Any) return "no dilemma events in the log yet";
            var sb = new StringBuilder();
            foreach (var kv in r.dilemmasByVariant) sb.Append(kv.Value.ToText()).Append('\n');
            Debug.Log("[Telemetry] " + sb);
            return sb.ToString().TrimEnd('\n');
        }

        // ---------------- the panel section ----------------
        /// <summary>Draws the "Dilemmas (P7)" section (IMGUI, inside <see cref="SimDebugPanel"/>). Returns the last action's line, or null.</summary>
        // ---- #368: the choice fix (raised while watching or on open, paced by hours, "Let them decide") ----
        /// <summary>#368: raise whichever choice holds now, ignoring the 3-hour gap and the schedule; opens the stack on its card.</summary>
        public static string RaiseChoiceNow()
        {
            var sim = Sim;
            if (sim == null) return "no box";
            var d = sim.DebugRaiseDilemma();
            if (d == null) return "no choice holds in this box now";
            GameClock.Instance?.DilemmaTracker()?.MarkCheat(d.id);
            GameClock.Instance?.DilemmaTracker()?.Observe(sim, GameClock.Tel);
            return Saved($"raised {d.defId} (#{d.id}); {CardStackView.DebugOpenEntry(d.entryId)}");
        }

        /// <summary>#368: forget the gap: the next watched month raises a choice.</summary>
        public static string SkipChoiceGap()
        {
            var sim = Sim;
            if (sim == null) return "no box";
            sim.DebugSkipDilemmaGap();
            return Saved("choice gap skipped: the next watched month raises one (a wish or the first run may hold it)");
        }

        /// <summary>#368: visits counted, the open choice's visits, hours since the last raise.</summary>
        public static string ChoiceVisitStatus() => Sim == null ? "no box" : Sim.DilemmaVisitStatus();

        /// <summary>#368: the player was away <paramref name="hours"/> and opens the app: a catch-up, then a visit.</summary>
        public static string OpenAfter(double hours)
        {
            var sim = Sim;
            if (sim == null) return "no box";
            bool open = sim.DebugOpenAfter(hours);
            return Saved($"opened after {hours:0.#} h away; {(open ? "a choice is waiting" : "no choice waiting")}; {sim.DilemmaVisitStatus()}");
        }

        /// <summary>#368: "Let them decide" on the open choice, on the Card path (through the card when it is up).</summary>
        public static string LetThemDecide()
        {
            var sim = Sim;
            var d = sim?.OpenDilemma;
            if (d == null) return "no open dilemma";
            GameClock.Instance?.DilemmaTracker()?.MarkCheat(d.id);
            var m = CardStackView.Instance != null ? CardStackView.Instance.Model : null;
            bool onCard = m != null && m.Face != null && m.Face.kind == CardKind.Dilemma && m.Shown != null && m.Shown.lead.id == d.entryId;
            bool ok = onCard ? m.LetThemDecide() : sim.LetThemDecide(d.id, DilemmaPath.Card);
            if (!ok) return "could not hand it back";
            if (!onCard) GameClock.DilemmaAnswered(d);
            return Saved($"let them decide {d.defId}: they chose option {d.option + 1}");
        }

        static void GuiChoiceFix(ref string last)
        {
            GUILayout.Label("Choice pacing (#368):");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Raise a choice now")) last = RaiseChoiceNow();
            if (GUILayout.Button("Skip choice gap")) last = SkipChoiceGap();
            if (GUILayout.Button("Let them decide")) last = LetThemDecide();
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label("Open after:", GUILayout.Width(70));
            if (GUILayout.Button("5 min")) last = OpenAfter(5 / 60.0);
            if (GUILayout.Button("1 h")) last = OpenAfter(1);
            if (GUILayout.Button("4 h")) last = OpenAfter(4);
            if (GUILayout.Button("12 h")) last = OpenAfter(12);
            GUILayout.EndHorizontal();
            GUILayout.Label(ChoiceVisitStatus());
        }

        public static string Gui()
        {
            string last = null;
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(On ? "Dilemmas: ON (B)" : "Dilemmas: off (A)")) last = Toggle(!On);
            if (GUILayout.Button(LabOn ? "P7 lab: ON (x60)" : "P7 lab: off")) last = Lab(!LabOn);
            GUILayout.EndHorizontal();
            GUILayout.Label("Raise now:");
            for (int i = 0; i < Ids.Length; i += 2)
            {
                GUILayout.BeginHorizontal();
                for (int k = i; k < i + 2 && k < Ids.Length; k++)
                {
                    string why = WhyNot(Ids[k]);
                    if (GUILayout.Button(why == null ? Ids[k] : Ids[k] + " (no)")) last = why == null ? Raise(Ids[k]) : Ids[k] + ": " + why;
                }
                GUILayout.EndHorizontal();
            }
            GUILayout.BeginHorizontal();
            GUILayout.Label("Card:", GUILayout.Width(40));
            if (GUILayout.Button("Answer 1")) last = AnswerOnCard(0);
            if (GUILayout.Button("Answer 2")) last = AnswerOnCard(1);
            if (GUILayout.Button("Lapse")) last = Lapse();
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label("Box:", GUILayout.Width(40));
            if (GUILayout.Button("Stage")) last = StageInBox();
            if (GUILayout.Button("1")) last = AnswerInBox(0);
            if (GUILayout.Button("2")) last = AnswerInBox(1);
            if (GUILayout.Button("Leave")) last = LeaveBox();
            GUILayout.EndHorizontal();
            GUILayout.Label(BoxStatus());
            GuiChoiceFix(ref last);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Fire next callback")) last = FireCallback();
            if (GUILayout.Button("Report")) last = Report();
            GUILayout.EndHorizontal();
            GUILayout.Label(List());
            return last;
        }
    }
}
