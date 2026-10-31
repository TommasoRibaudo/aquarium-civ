using System;
using System.Collections.Generic;
using System.Linq;
using AquariumCiv.Sim.Settle;

namespace AquariumCiv.Sim
{
    public enum HintGesture { None, Wipe, Flick, Tap, Rub }
    public enum HintAnchor { None, Glass, Patch, Plate, ShapingButton, RainSpot }
    /// <summary>One-off moments the view stages (a child's smiley in the mist).</summary>
    public enum FirstRunCue { None, ChildSmiley }

    /// <summary>What the view draws now: a ghost finger doing <see cref="gesture"/> at <see cref="anchor"/> with at most three words.</summary>
    public struct FirstRunHint
    {
        public bool visible;
        public string id;              // stable, for telemetry and tests ("wipe", "patch", "name", "rain-tap", "rain-rub")
        public string text;            // at most 3 words
        public HintGesture gesture;
        public HintAnchor anchor;
    }

    /// <summary>"Their first year" as data: what the caption and the first card show (all drawn from the chronicle).</summary>
    public sealed class FirstYearSummary
    {
        public string caption = "Their first year";
        public string place = "";
        public int populationStart, populationEnd, buildingsStart, buildingsEnd;
        public bool rainAnswered;
        public ChronicleCard card;          // the first chronicle card: the rain's answer if it came, else the best of the year
        public string cardText = "";        // its body (a plain fallback if the year was quiet)
        public int flyToBuildingId = -1;    // "Show me" flies here (-1 = nowhere to fly)
        public List<string> alsoHeadlines = new(); // up to 3 more short lines from the year (not shown in the first run)
    }

    /// <summary>
    /// The first run (#94; docs/design/first-session-and-check-in.md §2, decisions.md "First session"): a small state machine
    /// over the real sim, pure C#. It never blocks: nothing is locked, every step can be skipped, the player may do things in
    /// any order, and the hints are only a ghost finger. Progress lives in <see cref="SimState.firstRun"/>, so it is saved with
    /// the box and resumes after the app is killed. Opt-in: a world has no first run until <see cref="Begin"/>.
    ///
    /// Phase 2 (the UI) drives it like this: <c>Begin</c> (or <c>Resume</c> after a load) once; <c>Tick(dt, touching)</c> every
    /// frame; draw <see cref="Hint"/>; react to <see cref="StepChanged"/> and <see cref="Cue"/>; on the plate call
    /// <see cref="KeepName"/>; on the tray <see cref="NoteTrayOpened"/> then <see cref="PlaceRain"/>; during the year skip
    /// call <see cref="AdvanceYear"/> each frame (with the live clock paused); show <see cref="Summary"/> then
    /// <see cref="AcknowledgeCard"/>. <see cref="SkipStep"/> and <see cref="SkipAll"/> are the escape hatches.
    /// </summary>
    public sealed class FirstRun
    {
        // ---- timing rules (all tunable; [est]) ----
        public const double ArriveSeconds = 2.5;            // the box is set down with a thump
        public const double HintDelaySeconds = 0.8;         // a step's ghost finger shows this long after the step starts
        public const double HintIdleSeconds = 25.0;         // a hidden hint comes back after this long with no touch
        public const int HintMaxReturns = 3;                // ...at most this many times, then never again
        public const int HintMaxWords = 3;
        public const double SmileyIdleSeconds = 20.0;       // untouched in the Wipe step: a child draws a smiley in the mist
        public const double NameAutoKeepSeconds = 75.0;     // untouched at the plate: the suggested name is kept and the run goes on
        public const double YearSeconds = 15.0;             // the live skip, one game year
        public const float WipedGrime = 0.08f;              // the glass counts as wiped at or below this
        public const float ClearedOvergrowth = 0.06f;       // the patch counts as cleared at or below this
        public const float StartGrime = 0.8f, StartOvergrowth = 0.5f;

        readonly SimCore sim;
        FirstRunState F => sim.State.firstRun;

        /// <summary>The step changed (the new step; also fired for each step a skip passes).</summary>
        public event Action<FirstRunStep> StepChanged;
        public event Action<FirstRunCue> Cue;

        FirstRun(SimCore sim) { this.sim = sim; }

        // ---------- starting ----------
        /// <summary>Starts the first run on a fresh world: a misted glass, an overgrown patch, a suggested name. Opt-in;
        /// call once on a new world, before the first frame.</summary>
        public static FirstRun Begin(SimCore sim)
        {
            var s = sim.State;
            var spec = TerrainSpec.Preset(sim.TerrainPreset);
            // The patch lies on the far side of the camp from the pond; the first rain falls on the dry ground between.
            float dx = spec.centreX - spec.pondX, dz = spec.centreZ - spec.pondZ;
            float len = Math.Max(0.001f, (float)Math.Sqrt(dx * dx + dz * dz));
            dx /= len; dz /= len;
            var f = new FirstRunState
            {
                active = true, beganMonth = s.month, step = (int)FirstRunStep.Arrive,
                patchX = ClampX(spec.centreX + dz * 1.2f), patchZ = ClampZ(spec.centreZ - dx * 1.2f),
                rainX = ClampX(spec.pondX + dx * 1.1f), rainZ = ClampZ(spec.pondZ + dz * 1.1f),
                suggestedName = SettlementNaming.Suggest(s.seed),
            };
            s.firstRun = f;
            s.grime = Math.Max(s.grime, StartGrime);
            s.overgrowth = Math.Max(s.overgrowth, StartOvergrowth);
            s.trash.Clear();
            return new FirstRun(sim);
        }

        /// <summary>After a load: the director for the saved state (inactive and harmless if the run is done or never began).</summary>
        public static FirstRun Resume(SimCore sim)
        {
            var d = new FirstRun(sim);
            var f = d.F;
            if (f.active)
            {
                f.stepSeconds = 0; f.untouchedSeconds = 0; f.hintIdle = 0;
                if (f.hintPhase == 1) f.hintPhase = 2;        // it was showing: it comes back after an idle spell
                d.Evaluate();                                   // the world may have moved on (a name set, a year passed)
            }
            return d;
        }

        /// <summary>Debug: begin the run again on the current world (the glass dirty again, a new patch).</summary>
        public static FirstRun Replay(SimCore sim)
        {
            sim.State.settlementName = "";
            sim.State.storyFlags.Remove("named");
            return Begin(sim);
        }

        static float ClampX(float x) => Math.Clamp(x, -Terrain.HX + 0.7f, Terrain.HX - 0.7f);
        static float ClampZ(float z) => Math.Clamp(z, -Terrain.HZ + 0.7f, Terrain.HZ - 0.7f);

        // ---------- state ----------
        public bool Active => F.active;
        public FirstRunStep Step => (FirstRunStep)F.step;
        public bool Skipped => F.skipped;
        public (float x, float z) PatchSpot => (F.patchX, F.patchZ);
        public (float x, float z) SuggestedRainSpot => (F.rainX, F.rainZ);

        // ---------- every frame ----------
        /// <summary>Call every frame while the game is in the box. <paramref name="touching"/> = a finger is down (anywhere).
        /// Re-checks the step's condition against the sim, runs the hint timing and the idle cues.</summary>
        public void Tick(float dt, bool touching)
        {
            var f = F;
            if (!f.active || dt <= 0f) return;
            f.playSeconds += dt;
            f.stepSeconds += dt;
            f.untouchedSeconds = touching ? 0 : f.untouchedSeconds + dt;
            int before = f.step;
            Evaluate();
            if (f.step != before || !f.active) return; // a new step starts its own timers next frame
            UpdateHint(dt, touching);
            if (Step == FirstRunStep.Wipe && !f.smileyDone && f.untouchedSeconds >= SmileyIdleSeconds)
            {
                f.smileyDone = true;
                Cue?.Invoke(FirstRunCue.ChildSmiley);
            }
            if (Step == FirstRunStep.Name && f.untouchedSeconds >= NameAutoKeepSeconds) KeepName(null);
        }

        void UpdateHint(float dt, bool touching)
        {
            var f = F;
            if (!HasHint(Step)) { f.hintPhase = 0; return; }
            switch (f.hintPhase)
            {
                case 0: if (f.stepSeconds >= HintDelaySeconds) f.hintPhase = 1; break;
                case 1: if (touching) { f.hintPhase = 2; f.hintIdle = 0; } break;
                case 2:
                    if (touching) { f.hintIdle = 0; break; }
                    f.hintIdle += dt;
                    if (f.hintIdle >= HintIdleSeconds)
                    {
                        if (f.hintReturns < HintMaxReturns) { f.hintReturns++; f.hintPhase = 1; }
                        else f.hintPhase = 3; // spent: never again
                    }
                    break;
            }
        }

        static bool HasHint(FirstRunStep s) => s == FirstRunStep.Wipe || s == FirstRunStep.Patch || s == FirstRunStep.Name || s == FirstRunStep.Rain;

        /// <summary>The ghost finger to draw now (visible = false when none).</summary>
        public FirstRunHint Hint
        {
            get
            {
                var f = F;
                if (!f.active || f.hintPhase != 1) return default;
                return HintFor(Step, f.hintSubstep);
            }
        }

        /// <summary>Every hint of the run, for tests and the tester's pass list.</summary>
        public static IEnumerable<FirstRunHint> AllHints()
        {
            yield return HintFor(FirstRunStep.Wipe, false);
            yield return HintFor(FirstRunStep.Patch, false);
            yield return HintFor(FirstRunStep.Name, false);
            yield return HintFor(FirstRunStep.Rain, false);
            yield return HintFor(FirstRunStep.Rain, true);
        }

        static FirstRunHint HintFor(FirstRunStep s, bool tray) => s switch
        {
            FirstRunStep.Wipe => new FirstRunHint { visible = true, id = "wipe", text = "Wipe the glass", gesture = HintGesture.Wipe, anchor = HintAnchor.Glass },
            FirstRunStep.Patch => new FirstRunHint { visible = true, id = "patch", text = "Flick the weeds", gesture = HintGesture.Flick, anchor = HintAnchor.Patch },
            FirstRunStep.Name => new FirstRunHint { visible = true, id = "name", text = "Name it", gesture = HintGesture.Tap, anchor = HintAnchor.Plate },
            FirstRunStep.Rain => tray
                ? new FirstRunHint { visible = true, id = "rain-tap-spot", text = "Tap a spot", gesture = HintGesture.Tap, anchor = HintAnchor.RainSpot }
                : new FirstRunHint { visible = true, id = "rain-tap", text = "Give them rain", gesture = HintGesture.Tap, anchor = HintAnchor.ShapingButton },
            _ => default,
        };

        /// <summary>The player opened the shaping tray: the rain hint moves on to "rub a spot" and shows at once.</summary>
        public void NoteTrayOpened()
        {
            if (!F.active || Step != FirstRunStep.Rain || F.hintSubstep) return;
            F.hintSubstep = true;
            if (F.hintPhase != 3) F.hintPhase = 1;
        }

        // ---------- the step conditions ----------
        void Evaluate()
        {
            var f = F;
            if (!f.active) return;
            AdoptRain();
            for (int guard = 0; guard < 10 && f.active; guard++)
            {
                if (!StepDone(Step)) break;
                Advance();
            }
        }

        bool StepDone(FirstRunStep s)
        {
            var f = F; var st = sim.State;
            switch (s)
            {
                case FirstRunStep.Arrive: return f.stepSeconds >= ArriveSeconds;
                case FirstRunStep.Wipe: return st.grime <= WipedGrime;
                case FirstRunStep.Patch: return st.overgrowth <= ClearedOvergrowth;
                case FirstRunStep.Name: return f.nameKept;
                case FirstRunStep.Rain: return f.rainNudgeId >= 0;
                case FirstRunStep.Year: return f.yearDone;
                case FirstRunStep.Card: return f.cardAcknowledged;
                default: return false;
            }
        }

        void Advance()
        {
            var f = F;
            f.step++;
            f.stepSeconds = 0; f.untouchedSeconds = 0; f.hintPhase = 0; f.hintReturns = 0; f.hintIdle = 0; f.hintSubstep = false;
            if (Step == FirstRunStep.Rain) RaiseRainWish();
            if (Step == FirstRunStep.Year) BeginYear();
            if (Step == FirstRunStep.Done) { f.active = false; sim.CardsSeenToNow(); }
            StepChanged?.Invoke(Step);
        }

        /// <summary>A rain the player placed through the ordinary tray still counts (and gets its guaranteed answer).</summary>
        void AdoptRain()
        {
            var f = F;
            if (f.rainNudgeId >= 0) return;
            var n = sim.State.nudges.Where(x => x.kind == SimCore.NudgeKind.Rain && x.month >= f.beganMonth).OrderBy(x => x.id).FirstOrDefault();
            if (n == null) return;
            f.rainNudgeId = n.id;
            n.answerAfter = 8;
            if (n.creditNotableId < 0 && f.rainNotableId >= 0) n.creditNotableId = f.rainNotableId;
        }

        // ---------- naming ----------
        public string SuggestedName => F.suggestedName;
        public string PlateName => sim.State.settlementName ?? "";

        /// <summary>Another suggestion (the plate's dice button); never the current one.</summary>
        public string RerollName()
        {
            var f = F;
            int i = Array.IndexOf(SettlementNaming.Defaults, f.suggestedName);
            f.suggestedName = SettlementNaming.Defaults[(Math.Max(0, i) + 1) % SettlementNaming.Defaults.Length];
            return f.suggestedName;
        }

        public NameProblem CheckName(string typed, out string clean) => SettlementNaming.Check(typed, out clean);

        /// <summary>Keep the typed name, or the suggestion when <paramref name="typed"/> is null or empty. Returns the problem
        /// (and changes nothing) if the typed name is refused. Works any time, not only in the Name step.</summary>
        public NameProblem KeepName(string typed)
        {
            string name = string.IsNullOrWhiteSpace(typed) ? F.suggestedName : typed;
            var problem = SettlementNaming.Check(name, out string clean);
            if (problem != NameProblem.None) return problem;
            sim.SetSettlementName(clean);
            F.nameKept = true;
            Evaluate();
            return NameProblem.None;
        }

        // ---------- the first rain ----------
        void RaiseRainWish()
        {
            var f = F;
            if (f.rainWishId >= 0) return;
            var who = sim.State.notables.Where(n => n.alive && n.age >= 16f)
                .OrderBy(n => n.job == JobType.Gatherer || n.job == JobType.Farmer ? 0 : 1).ThenBy(n => n.id).FirstOrDefault();
            if (who == null) return;
            var w = sim.DebugForceWish(SimCore.NudgeKind.Rain, who.id); // a real wish card: "Mira hopes for rain."
            if (w == null) return;
            w.x = f.rainX; w.z = f.rainZ;
            f.rainWishId = w.id; f.rainNotableId = who.id;
        }

        /// <summary>The wish to show as a bubble over the wisher (null before the Rain step; its state is Given once the rain fell).</summary>
        public Wish RainWish => F.rainWishId >= 0 ? sim.FindWish(F.rainWishId) : null;

        /// <summary>Place the first rain at a point (box coordinates). Never refused for want of a charge; the answer lands in
        /// the first year. Moves the run on.</summary>
        public SimCore.ShapeResult PlaceRain(float x, float z)
        {
            var r = sim.PlaceFirstRain(x, z);
            if (r.Ok) Evaluate();
            return r;
        }

        // ---------- "Their first year" ----------
        void BeginYear()
        {
            var f = F;
            if (f.yearStartMonth >= 0) return;
            f.yearStartMonth = sim.State.month;
            f.popAtStart = sim.State.population;
            f.buildingsAtStart = sim.State.buildings.Count;
            f.yearSeconds = 0;
        }

        /// <summary>0..1 how far the live skip has played.</summary>
        public float YearProgress => F.yearStartMonth < 0 ? 0f : F.yearDone ? 1f : (float)Math.Min(1.0, F.yearSeconds / YearSeconds);

        /// <summary>Plays the skip: advances the sim by dt/15 of a year, in whole months. Pause the live clock while it runs.
        /// Returns the progress; at 1 the year is over and the run moves to the card.</summary>
        public float AdvanceYear(float dt)
        {
            var f = F;
            if (!f.active || Step != FirstRunStep.Year) return YearProgress;
            f.yearSeconds = Math.Min(YearSeconds, f.yearSeconds + Math.Max(0f, dt));
            AdvanceSimTo(f.yearStartMonth + (long)Math.Floor(f.yearSeconds / YearSeconds * SimConfig.MonthsPerYear + 1e-6));
            if (f.yearSeconds >= YearSeconds)
            {
                f.yearDone = true;
                Evaluate();
            }
            return YearProgress;
        }

        void AdvanceSimTo(long targetMonth)
        {
            var s = sim.State;
            if (s.month >= targetMonth) return;
            double hours = targetMonth * sim.Config.StepHours - s.simHours;
            sim.Advance(Math.Max(hours, 1e-6) + 1e-6);
        }

        /// <summary>The caption and first card for "Their first year", from the chronicle. Safe to call any time after the
        /// year began; before it ends it shows what has happened so far.</summary>
        public FirstYearSummary Summary()
        {
            var f = F; var st = sim.State;
            var sum = new FirstYearSummary
            {
                place = sim.PlaceName, populationStart = f.popAtStart, populationEnd = st.population,
                buildingsStart = f.buildingsAtStart, buildingsEnd = st.buildings.Count,
            };
            if (f.yearStartMonth < 0) { sum.cardText = FallbackLine; return sum; }
            var stack = sim.ReturnCardsCapped(f.yearStartMonth - 1, 4);
            var rain = f.rainNudgeId >= 0 ? st.nudges.FirstOrDefault(n => n.id == f.rainNudgeId) : null;
            var answer = rain != null ? sim.AnswerOf(rain) : null;
            sum.rainAnswered = answer != null;
            var all = stack.cards.Where(c => c.kind != CardKind.Wish && c.lead?.kind != "founding").ToList();
            sum.card = (answer != null ? all.FirstOrDefault(c => c.entries.Any(e => e.id == answer.id)) : null) ?? all.FirstOrDefault();
            sum.cardText = sum.card?.lead?.text ?? FallbackLine;
            sum.flyToBuildingId = sum.card?.buildingId ?? -1;
            sum.alsoHeadlines = all.Where(c => c != sum.card).Select(c => c.headline).Where(h => !string.IsNullOrEmpty(h)).Take(3).ToList();
            return sum;
        }

        /// <summary>True, never a number or a worry: used only when a year left nothing to tell.</summary>
        public const string FallbackLine = "The camp got through its first year, and the glass kept the weather out.";

        /// <summary>The first card was seen (Show me, or dismissed): the run is over.</summary>
        public void AcknowledgeCard()
        {
            if (!F.active || Step != FirstRunStep.Card) return;
            F.cardAcknowledged = true;
            Evaluate();
        }

        // ---------- skipping (never blocking) ----------
        /// <summary>Moves past the current step without doing it. The name is kept as suggested; the rain falls on the suggested
        /// spot (the Keeper's gift, so the first year still has its story); the year plays out at once. The mess stays: it can
        /// still be cleaned any time.</summary>
        public void SkipStep()
        {
            var f = F;
            if (!f.active) return;
            switch (Step)
            {
                case FirstRunStep.Arrive: f.stepSeconds = ArriveSeconds; break;
                case FirstRunStep.Wipe:
                case FirstRunStep.Patch: Advance(); return;
                case FirstRunStep.Name: KeepName(null); break;
                case FirstRunStep.Rain: if (f.rainNudgeId < 0) PlaceRain(f.rainX, f.rainZ); break;
                case FirstRunStep.Year: AdvanceYear((float)YearSeconds); break;
                case FirstRunStep.Card: f.cardAcknowledged = true; break;
            }
            Evaluate();
        }

        /// <summary>Skip the whole run (never asked twice): the suggested name is kept, the world stays as it is, no year plays.</summary>
        public void SkipAll()
        {
            var f = F;
            if (!f.active) return;
            f.skipped = true;
            if (string.IsNullOrEmpty(sim.State.settlementName)) sim.SetSettlementName(f.suggestedName);
            f.step = (int)FirstRunStep.Done;
            f.active = false;
            sim.CardsSeenToNow();
            StepChanged?.Invoke(FirstRunStep.Done);
        }

        /// <summary>Debug: fast-forward to a step, doing what the steps before it ask (glass wiped, patch cleared, name kept,
        /// rain placed, the year played).</summary>
        public void DebugSkipTo(FirstRunStep target)
        {
            var f = F;
            for (int guard = 0; guard < 20 && f.active && Step < target; guard++)
            {
                if (Step == FirstRunStep.Wipe) sim.CleanGrime(1f);
                if (Step == FirstRunStep.Patch) sim.Prune(1f);
                SkipStep();
            }
        }
    }
}
