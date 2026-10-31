using System;
using System.Linq;

namespace AquariumCiv.Sim
{
    /// <summary>
    /// The sim side of the first run (#94). Everything is opt-in on <see cref="FirstRunState.active"/>; nothing here draws
    /// from a random stream, so worlds that never begin a first run are byte-for-byte what they were.
    /// - <see cref="SetSettlementName"/>: the brass plate's name (the people no longer rename the place at 12 people).
    /// - <see cref="PlaceFirstRain"/>: the guaranteed early rain; its answer lands inside "Their first year".
    /// </summary>
    public partial class SimCore
    {
        /// <summary>Game months after the first rain at which the people answer it whatever they can afford (a game year is 12).</summary>
        public const int FirstRainAnswerMonths = 8;

        /// <summary>Sets the settlement's name (already checked by <c>SettlementNaming</c>) and marks it named.</summary>
        public void SetSettlementName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return;
            State.settlementName = name;
            if (!State.storyFlags.Contains(FlagNamed)) State.storyFlags.Add(FlagNamed);
        }

        /// <summary>
        /// The first rain: spends no more than a charge (one is granted if the Keeper has none), puts the cloud at a point,
        /// credits the answer to the notable who wished for it and closes that wish as given. The people answer it by
        /// <see cref="FirstRainAnswerMonths"/> at the latest. Returns the shaping, or the reason it failed.
        /// </summary>
        public ShapeResult PlaceFirstRain(float x, float z)
        {
            var f = State.firstRun;
            if (ShapingChargesWhole < 1) State.shapingCharges = 1f; // the first rain is never refused for want of a charge
            if (ShapingStanding(NudgeKind.Rain) >= ShapingCap(NudgeKind.Rain)) return new ShapeResult { fail = ShapeFail.AtCap };
            var r = PlaceShaping(NudgeKind.Rain, x, z, f.rainNotableId);
            if (!r.Ok) return r;
            r.nudge.answerAfter = 8;
            f.rainNudgeId = r.nudge.id;
            var w = f.rainWishId >= 0 ? FindWish(f.rainWishId) : null;
            if (w != null && w.state == WishState.Open) { w.state = WishState.Given; w.endMonth = State.month; w.nudgeId = r.nudge.id; }
            return r;
        }

        /// <summary>Every month while a first run is active: the first rain is answered in time (no dice).</summary>
        void FirstRunStep()
        {
            var f = State.firstRun;
            if (!f.active || f.rainNudgeId < 0) return;
            var n = State.nudges.FirstOrDefault(x => x.id == f.rainNudgeId);
            if (n != null && !n.credited && State.month - n.month >= FirstRainAnswerMonths) Answer(n, true);
        }
    }
}
