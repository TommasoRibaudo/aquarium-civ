using System;
using AquariumCiv.Sim;

namespace AquariumCiv.Life
{
    /// <summary>#697/#696: every way the planner throws its cached days away and plans them again, as one list, so a test or the debug panel
    /// can fire each of them on purpose. A replan must keep what each person is doing now (see <c>KeepSleepersIn</c>).</summary>
    public sealed partial class LifePlanner
    {
        /// <summary>The triggers: the parts of the sim signature (month, population, chronicle, nudge, supper, building), a forced
        /// <see cref="Invalidate"/>, and the planner's own forced day changes (season, weather, a water run).</summary>
        public static readonly string[] ReplanTriggers = { "month", "population", "chronicle", "nudge", "supper", "building", "invalidate", "season", "weather", "waterrun" };

        /// <summary>Debug: make one replan happen the way <paramref name="trigger"/> would. Returns what was done.</summary>
        public string DebugTrigger(string trigger)
        {
            var s = sim.State;
            switch (trigger)
            {
                case "month": sim.Step(); break;
                case "population": s.population += 1; break;
                case "chronicle": s.chronicle.Add(new ChronicleEntry { id = s.NextId(), month = s.month, kind = "note", text = "", salience = 1f }); break;
                case "nudge": sim.PlaceNudge(SimCore.NudgeKind.Rain, 0f, 0f); break;
                case "supper": sim.DebugSupper(SupperPick.Pot); break;
                case "building": sim.DebugBuild(BuildingType.Hut); break;
                case "invalidate": Invalidate(); break;
                case "season": ForcedSeason = ForcedSeason; break;
                case "weather": ForcedWeather = ForcedWeather; break;
                case "waterrun": ForceWaterRun(0); break;
                default: return "unknown trigger " + trigger;
            }
            return "replan: " + trigger;
        }
    }
}
