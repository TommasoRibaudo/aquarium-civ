using System;
using System.Collections.Generic;
using System.Linq;
using AquariumCiv.Sim.Settle;

namespace AquariumCiv.Sim
{
    /// <summary>
    /// Shaping the land by placement (#82; game-definition §6, decisions.md "Shaping toolset per era", "Shaping limit",
    /// research 10): the slice's four tools, rain, grove, channel and herd. The Keeper puts one at a point
    /// (<see cref="PlaceShaping"/>); the land changes at once (Settle) and the people answer within 1–3 game years with a
    /// credited building (<see cref="Nudge.credited"/>; the debug panel can skip to it). Shaping spends one of the
    /// Keeper's shared charges; charges refill by cleaning (<see cref="SimConfig.shapingPerGrime"/>...) and over time
    /// while the people are happy. <see cref="TakeBack"/> undoes a shaping, with its charge, until the people have used it.
    /// Each tool has a natural cap (<see cref="ShapingCap"/>) and is named by the era (<see cref="ShapingToolName(string)"/>).
    /// Built on the nudges of SimCore.Nudges.cs, which does the leaning and the answering. Hill and hollow and the Lamp
    /// come after the slice. The only dice are the story stream's one draw per placement (the answer's delay).
    /// </summary>
    public partial class SimCore
    {
        public enum ShapeFail { None, Locked, NoCharge, AtCap, Unknown }

        /// <summary>What placing a tool did: the shaping, or why not.</summary>
        public struct ShapeResult
        {
            public Nudge nudge;
            public ShapeFail fail;
            public bool Ok => fail == ShapeFail.None;
        }

        /// <summary>The slice's tools, in tray order.</summary>
        public static readonly string[] SliceTools = { NudgeKind.Rain, NudgeKind.Grove, NudgeKind.Herd, NudgeKind.Channel };

        // ---------- charges ----------
        /// <summary>The shared charges the Keeper holds now (fractions fill up between whole ones).</summary>
        public float ShapingCharges
        {
            get
            {
                if (State.shapingCharges < 0f) State.shapingCharges = Math.Min(Config.shapingStartCharges, ShapingMaxCharges);
                return State.shapingCharges;
            }
        }

        /// <summary>Whole charges that can be spent now.</summary>
        public int ShapingChargesWhole => (int)Math.Floor(ShapingCharges + 1e-4f);

        /// <summary>The most charges the Keeper holds: the camp's, plus one per later era.</summary>
        public float ShapingMaxCharges => Config.shapingMaxCharges + (int)State.era;

        void AddShapingCharges(float amount)
        {
            if (amount <= 0f) return;
            State.shapingCharges = Math.Min(ShapingMaxCharges, ShapingCharges + amount);
        }

        /// <summary>Cleaning gives charges back, by how much was really cleaned (no cleaning, nothing to farm).</summary>
        void CleaningRefill(float charges) => AddShapingCharges(charges);

        /// <summary>Every month: charges come back slowly while the people are happy.</summary>
        void ShapingStep()
        {
            if (State.mood >= Config.shapingHappyMood) AddShapingCharges(Config.shapingPerHappyYear / SimConfig.MonthsPerYear);
        }

        // ---------- the tools ----------
        /// <summary>Is this tool in the Keeper's hands in the box's era? Rain, grove and herd from the camp; the channel
        /// from the village (research 10 "Decision").</summary>
        public bool ShapingUnlocked(string kind) => kind switch
        {
            NudgeKind.Rain or NudgeKind.Grove or NudgeKind.Herd => true,
            NudgeKind.Channel => State.era >= Era.Village,
            _ => false,
        };

        /// <summary>The tool's name at the box's era, for the tray and the log: rain is a shower, then rain, then a storm;
        /// the grove blossoms into an orchard; the herd grows to goats and wool; the channel becomes a mill race.</summary>
        public string ShapingToolName(string kind) => ShapingToolName(kind, State.era);

        public static string ShapingToolName(string kind, Era era) => kind switch
        {
            NudgeKind.Rain => era == Era.Camp ? "shower" : era == Era.Village ? "rain" : "storm",
            NudgeKind.Grove => era == Era.Town ? "orchard" : era == Era.Village ? "blossoming grove" : "grove",
            NudgeKind.Herd => era == Era.Camp ? "herd" : era == Era.Village ? "goats" : "wool flock",
            NudgeKind.Channel => era >= Era.Town ? "mill race" : "channel",
            _ => kind,
        };

        /// <summary>Natural cap on shapings of this kind standing in the box at once (research 10): one cloud at a time,
        /// a few groves, two herds, three channels.</summary>
        public static int ShapingCap(string kind) => kind switch
        {
            NudgeKind.Rain => 1,
            NudgeKind.Grove => 4,
            NudgeKind.Herd => 2,
            NudgeKind.Channel => 3,
            _ => 0,
        };

        /// <summary>The herd's look at a point: deer on grass, a shoal of fish in water (and by the pond).</summary>
        public string HerdVariantAt(float x, float z) => WetSpot(TerrainSpec.Preset(TerrainPreset), x, z) ? "fish" : "deer";

        /// <summary>How many of this tool count against its cap. Rain only counts until the people answer it (one cloud
        /// at a time); the rest stand for good. A herd can be asked for by variant (fish: one shoal per water body).</summary>
        public int ShapingStanding(string kind, string variant = null)
        {
            var q = State.nudges.Where(n => n.kind == kind);
            if (kind == NudgeKind.Rain) q = q.Where(n => !n.credited && State.month - n.month < NudgeMonths);
            if (kind == NudgeKind.Herd && variant != null) q = q.Where(n => n.variant == variant);
            return q.Count();
        }

        /// <summary>Why this tool can't be used now, or None. <paramref name="variant"/> is the herd's (deer or fish).</summary>
        public ShapeFail CanShape(string kind, string variant = null)
        {
            if (!SliceTools.Contains(kind)) return ShapeFail.Unknown;
            if (!ShapingUnlocked(kind)) return ShapeFail.Locked;
            if (ShapingChargesWhole < 1) return ShapeFail.NoCharge;
            if (ShapingStanding(kind) >= ShapingCap(kind)) return ShapeFail.AtCap;
            if (kind == NudgeKind.Herd && variant == "fish" && ShapingStanding(kind, "fish") >= 1) return ShapeFail.AtCap;
            return ShapeFail.None;
        }

        /// <summary>
        /// Put a tool at a point (box coordinates, as Building.x/z). Spends a charge, changes the land at once, and the
        /// people answer in 1–3 game years with a credited building. <paramref name="creditNotableId"/> names the notable
        /// the answer is credited to (a wish's author, #83); -1 lets the story pick someone.
        /// </summary>
        public ShapeResult PlaceShaping(string kind, float x, float z, int creditNotableId = -1)
        {
            string variant = kind == NudgeKind.Herd ? HerdVariantAt(x, z) : "";
            var fail = CanShape(kind, variant);
            if (fail != ShapeFail.None) return new ShapeResult { fail = fail };
            return new ShapeResult { nudge = Shape(kind, x, z, creditNotableId, variant, 1f) };
        }

        Nudge Shape(string kind, float x, float z, int creditNotableId, string variant, float charge)
        {
            float food = State.food;
            var n = PlaceNudge(kind, x, z);
            n.variant = variant ?? "";
            n.creditNotableId = creditNotableId;
            n.eraAtPlace = (int)State.era;
            n.foodGiven = State.food - food;
            n.answerAfter = Math.Max(12, n.answerAfter); // a year at least; NudgeStep makes it by the end of the third
            n.chargeSpent = charge;
            State.shapingCharges = Math.Max(0f, ShapingCharges - charge);
            return n;
        }

        /// <summary>Can the Keeper still take this shaping back? Until the people have used it: before they answer, and
        /// before any lot or building is made for it.</summary>
        public bool CanTakeBack(int nudgeId)
        {
            var n = State.nudges.FirstOrDefault(x => x.id == nudgeId);
            return n != null && !n.credited && n.lotId == 0 && n.buildingId < 0;
        }

        /// <summary>Take a shaping back: the land returns to what it was and the charge comes back in full (even above
        /// the cap, it was only lent). False once the people have used it, or if there is no such shaping.</summary>
        public bool TakeBack(int nudgeId)
        {
            if (!CanTakeBack(nudgeId)) return false;
            var n = State.nudges.First(x => x.id == nudgeId);
            Settlement?.RevertShaping(n);
            State.food = Math.Max(0f, State.food - n.foodGiven);
            State.nudges.Remove(n);
            State.shapingCharges = ShapingCharges + n.chargeSpent;
            return true;
        }

        /// <summary>The shapings waiting for the people's answer, oldest first (the ones that can still be taken back).</summary>
        public List<Nudge> ShapingsWaiting() =>
            State.nudges.Where(n => !n.credited && State.month - n.month < NudgeMonths).OrderBy(n => n.id).ToList();

        /// <summary>The chronicle line crediting an answered shaping (names the notable), or null.</summary>
        public ChronicleEntry AnswerOf(Nudge n) => n.credited && n.buildingId >= 0
            ? State.chronicle.LastOrDefault(e => e.keeper && e.buildingId == n.buildingId) : null;

        // ---------- debug (the debug panel: each tool at a point, refill charges, skip to the answer) ----------
        /// <summary>Debug: a tool at a point, free of charge, era lock and cap (a non-slice kind returns null).</summary>
        public Nudge DebugShape(string kind, float x, float z, int creditNotableId = -1)
        {
            if (!SliceTools.Contains(kind)) return null;
            return Shape(kind, x, z, creditNotableId, kind == NudgeKind.Herd ? HerdVariantAt(x, z) : "", 0f);
        }

        /// <summary>Debug: fill the charges to the cap.</summary>
        public void DebugRefillShaping() => State.shapingCharges = ShapingMaxCharges;

        /// <summary>Debug: the people answer this shaping now (their cost is waived). False if it was already answered.</summary>
        public bool DebugSkipToAnswer(int nudgeId)
        {
            var n = State.nudges.FirstOrDefault(x => x.id == nudgeId);
            return n != null && !n.credited && Answer(n, true);
        }
    }
}
