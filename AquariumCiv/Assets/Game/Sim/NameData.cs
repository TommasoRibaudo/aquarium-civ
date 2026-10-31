namespace AquariumCiv.Sim
{
    /// <summary>Name, trait and line tables for the event generator.</summary>
    public static class NameData
    {
        // Medieval, short, easy to read at phone size. The first 40 are the originals (keep their order; saves and tests lean on them).
        public static readonly string[] Given =
        {
            "Ada", "Bram", "Cela", "Dov", "Edda", "Fen", "Greta", "Hob", "Ilse", "Joss", "Kit", "Lune", "Mabel", "Nils",
            "Odo", "Pim", "Quill", "Rhea", "Sten", "Tilda", "Ulf", "Vera", "Wim", "Yara", "Zeb", "Alma", "Bo", "Cobb",
            "Dina", "Esk", "Flora", "Gus", "Hester", "Ivo", "Juna", "Lorn", "Moss", "Nell", "Otto", "Pell",
            // Women
            "Agnes", "Alys", "Avis", "Beatrix", "Bryn", "Cecily", "Clara", "Dulcie", "Edith", "Elsa", "Emma", "Etta", "Freya", "Gwen",
            "Hilda", "Imogen", "Isolde", "Joan", "Kate", "Lenna", "Maud", "Merry", "Mina", "Nan", "Olive", "Petra", "Rosa", "Sibyl",
            "Thea", "Ursa", "Wynn", "Berta", "Cora", "Dorrit", "Elin", "Gytha", "Hanne", "Iris", "Linna", "Marta",
            // Men
            "Alder", "Aldo", "Alwin", "Arne", "Baldo", "Cuthbert", "Cedric", "Dunstan", "Edmund", "Elric", "Eben", "Finn", "Godric",
            "Hal", "Harl", "Hugh", "Ivar", "Jory", "Kell", "Leif", "Lowell", "Magnus", "Merrick", "Nolan", "Osric", "Piers", "Rolf",
            "Rowan", "Silas", "Tam", "Tobin", "Tuck", "Wat", "Wulf", "Aubrey", "Bertil", "Corwin", "Dag", "Egil", "Garrick", "Hamo", "Ingo",
            "Kai", "Osmund", "Rufus", "Sigurd", "Torin", "Wilf", "Yorick", "Fulk",
            // More of both
            "Aveline", "Brita", "Constance", "Daisy", "Eadgyth", "Faye", "Hawise", "Idony", "Jessa", "Leofa", "Orla", "Sunniva",
            "Brand", "Colm", "Drustan", "Eorl", "Gareth", "Hereward", "Jarl", "Lambert", "Oswin", "Perrin", "Stig", "Wystan"
        };

        public static readonly string[] Families =
        {
            "Lund", "Barrow", "Fernby", "Holt", "Marsh", "Quarry", "Reed", "Stile", "Thorn", "Wick", "Ashby", "Brook",
            "Cole", "Dale", "Ember", "Gale", "Hearth", "Mill", "Pike", "Vane",
            // Trades
            "Miller", "Fletcher", "Cooper", "Thatcher", "Tanner", "Weaver", "Fuller", "Smith", "Carter", "Baker", "Mason", "Wright",
            "Shepherd", "Fisher", "Glover", "Turner", "Dyer", "Chandler",
            // Places
            "Ashdown", "Oakley", "Hollin", "Eastmere", "Fenwick", "Hartley", "Kirby", "Linden", "Moorcroft", "Netherby", "Ridley",
            "Stanton", "Thornley", "Westcott", "Wynd", "Harrow", "Elmstead", "Copley", "Dunmore", "Sedge", "Bracken", "Alderby",
            "Redfern", "Stoke", "Wickham"
        };

        public static readonly string[] Traits =
        {
            "the stubborn", "the dreamer", "the kind", "the loud", "the clever", "the lazy", "the brave", "the gossip",
            "the gardener", "the grump", "the singer", "the tinkerer", "the worrier", "the joker"
        };

        public static string FirstBuildingLine(string type, Notable by, Era era = Era.Village)
        {
            string who = by != null ? by.FullName : "The people";
            // The camp gathers and stacks wood; it sows nothing yet (decisions 2026-10-29, #23: farms and lumber mills exist in
            // the camp but look different, and the village era is "staying put and sowing seed"). #61.
            if (era == Era.Camp && type == BuildingType.Farm) return $"{who} marked out the first gathering grounds, where the berries grow thickest.";
            if (era == Era.Camp && type == BuildingType.Lumbermill) return $"{who} started a woodpile by the trees, and everyone added to it.";
            return type switch
            {
                BuildingType.Hut => $"{who} raised the first proper hut.",
                BuildingType.Farm => $"{who} planted the first field of grain.",
                BuildingType.Lumbermill => $"{who} built a lumber mill by the trees.",
                BuildingType.Well => $"{who} dug the first well. The water tasted of glass.",
                BuildingType.Windmill => $"{who} built a windmill, though nobody is sure where the wind comes from.",
                BuildingType.Market => $"{who} opened a market. Haggling began immediately.",
                BuildingType.Tavern => $"{who} opened a tavern. Productivity dipped.",
                BuildingType.Church => $"{who} built a chapel facing the glass, where the Keeper sometimes appears.",
                BuildingType.Workshop => $"{who} set up a workshop by the green. The first tools were borrowed from everyone.",
                BuildingType.Guildhall => $"{who} raised a guildhall. The crafts finally had a door to argue behind.",
                BuildingType.Bakery => $"{who} lit the first oven in the new bakery. The whole box smelled of it by noon.",
                BuildingType.Shop => $"{who} turned a market stall into the first shop. The sign took longer than the shelves.",
                _ => $"{who} built a {type.ToLower()}."
            };
        }
    }
}
