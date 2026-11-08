using System;
using System.Collections.Generic;
using System.Linq;

namespace AquariumCiv.Sim
{
    /// <summary>What the building card shows (#188): what it is, the year it went up, and who built it.</summary>
    public class BuildingCardInfo
    {
        public int buildingId;
        public string type, title, whatItIs;
        public int year;                 // the game year it was founded (Year 0 = the first month)
        public string yearLine;          // "Built in year 12."
        public int builderId = -1;       // the notable who built it, -1 = "together"
        public string builderName = "";
        public bool builderAlive;
        public string builderLine;       // "Built by Mara Oak, the master builder." / "Built by the people, together."
        public bool Together => builderId < 0;
    }

    public partial class SimCore
    {
        /// <summary>Who built a new building (#188). No dice, so a seed plays the same run: the master builder if there is
        /// one, else a living adult notable who works wood or stone (turn by building id, so one hand doesn't build all),
        /// else the chief or whoever leads the era; 0 = nobody to name (the people built it together, also old saves).</summary>
        int PickBuilder(int buildingId)
        {
            var s = State;
            var mb = Living(HolderOf(RoleType.MasterBuilder));
            if (mb != null) return mb.id;
            var crafters = s.notables
                .Where(n => n.alive && n.age >= AdultAge && (n.job == JobType.Woodcutter || n.job == JobType.Mason))
                .OrderBy(n => n.id).ToList();
            if (crafters.Count > 0) return crafters[buildingId % crafters.Count].id;
            var lead = Living(HolderOf(LeaderOf(s.era)));
            return lead != null ? lead.id : 0;
        }

        /// <summary>The card for a building, or null if it isn't there. Builders of old saves (id 0) read as "together".</summary>
        public BuildingCardInfo BuildingCardOf(int buildingId)
        {
            if (buildingId == TowerCardId) return TowerCard(); // #404
            var b = State.buildings.FirstOrDefault(x => x.id == buildingId);
            if (b == null) return null;
            var c = new BuildingCardInfo
            {
                buildingId = b.id, type = b.type, title = BuildingTitle(b.type, State.era), whatItIs = BuildingBlurb(b.type, State.era),
                year = (int)(b.foundedMonth / SimConfig.MonthsPerYear),
            };
            c.yearLine = $"Built in year {c.year}.";
            var by = b.builderId > 0 ? FindNotable(b.builderId) : null;
            if (by == null) c.builderLine = "Built by the people, together.";
            else
            {
                c.builderId = by.id; c.builderName = by.FullName; c.builderAlive = by.alive;
                string role = RoleOf(by);
                c.builderLine = role != null ? $"Built by {by.FullName}, the {role}." : $"Built by {by.FullName}.";
            }
            return c;
        }

        /// <summary>The building's name as the people call it (types are roles; a camp's Farm is its gathering grounds).</summary>
        public static string BuildingTitle(string type, Era era) => type switch
        {
            BuildingType.Camp => era == Era.Camp ? "The fire" : "The old fire",
            BuildingType.Hut => era == Era.Camp ? "A tent" : "A house",
            BuildingType.Farm => era == Era.Camp ? "The gathering grounds" : "A farm",
            BuildingType.Lumbermill => era == Era.Camp ? "The woodpile" : "The lumbermill",
            BuildingType.Well => "The well",
            BuildingType.Windmill => "The windmill",
            BuildingType.Market => "The market",
            BuildingType.Tavern => "The tavern",
            BuildingType.Church => "The chapel",
            BuildingType.Workshop => "A workshop",
            BuildingType.Guildhall => "The guildhall",
            BuildingType.Shop => "A shop",
            BuildingType.Bakery => "The bakery",
            _ => type ?? "A building",
        };

        public static string BuildingBlurb(string type, Era era) => type switch
        {
            BuildingType.Camp => "Where everyone gathers at night. It keeps the cold off and the stories going.",
            BuildingType.Hut => era == Era.Camp ? "Somewhere to sleep out of the wind." : "A family's home.",
            BuildingType.Farm => era == Era.Camp ? "Where the berries and roots are gathered." : "Fields that put food on the tables.",
            BuildingType.Lumbermill => era == Era.Camp ? "Where the wood is stacked for the fire." : "Turns trees into planks and beams.",
            BuildingType.Well => "Fresh water for the whole place.",
            BuildingType.Windmill => "Grinds the grain into flour.",
            BuildingType.Market => "Where people trade and catch up on news.",
            BuildingType.Tavern => "A warm room, a drink and the latest gossip.",
            BuildingType.Church => "A quiet place for the bell and for thinking.",
            BuildingType.Workshop => "Where things are made and mended.",
            BuildingType.Guildhall => "Where the crafts meet and settle their business.",
            BuildingType.Shop => "A stall that grew up into a proper shop.",
            BuildingType.Bakery => "The oven that makes the day's bread.",
            _ => "",
        };

        /// <summary>Debug panel: give a building a builder (0 = together) so both card wordings can be reached.</summary>
        public bool DebugSetBuilder(int buildingId, int notableId)
        {
            var b = State.buildings.FirstOrDefault(x => x.id == buildingId);
            if (b == null) return false;
            b.builderId = notableId;
            return true;
        }
    }
}
