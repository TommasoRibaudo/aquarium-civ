namespace AquariumCiv.Sim
{
    /// <summary>Name, trait and line tables for the event generator.</summary>
    public static class NameData
    {
        public static readonly string[] Given =
        {
            "Ada", "Bram", "Cela", "Dov", "Edda", "Fen", "Greta", "Hob", "Ilse", "Joss", "Kit", "Lune", "Mabel", "Nils",
            "Odo", "Pim", "Quill", "Rhea", "Sten", "Tilda", "Ulf", "Vera", "Wim", "Yara", "Zeb", "Alma", "Bo", "Cobb",
            "Dina", "Esk", "Flora", "Gus", "Hester", "Ivo", "Juna", "Lorn", "Moss", "Nell", "Otto", "Pell"
        };

        public static readonly string[] Families =
        {
            "Lund", "Barrow", "Fernby", "Holt", "Marsh", "Quarry", "Reed", "Stile", "Thorn", "Wick", "Ashby", "Brook",
            "Cole", "Dale", "Ember", "Gale", "Hearth", "Mill", "Pike", "Vane"
        };

        public static readonly string[] Traits =
        {
            "the stubborn", "the dreamer", "the kind", "the loud", "the clever", "the lazy", "the brave", "the gossip",
            "the gardener", "the grump", "the singer", "the tinkerer", "the worrier", "the joker"
        };

        public static string FirstBuildingLine(string type, Notable by)
        {
            string who = by != null ? by.FullName : "The people";
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
                _ => $"{who} built a {type.ToLower()}."
            };
        }
    }
}
