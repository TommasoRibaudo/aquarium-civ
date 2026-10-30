using UnityEngine;

namespace AquariumCiv.View
{
    /// <summary>
    /// The one palette every mesh in the box draws from (sRGB, authored by eye; ported from the style-kit spike,
    /// docs/design/spikes/style-kit.md). Muted, warm, slightly chalky, so a handful of saturated accents (tunics, roofs,
    /// flowers, fire, the people's headbands and sashes) carry the eye to people and their work. No textures anywhere.
    /// </summary>
    public static class Palette
    {
        static Color H(string hex) { ColorUtility.TryParseHtmlString(hex, out var c); return c; }

        // ground
        public static readonly Color GrassA = H("#8DAA5B"), GrassB = H("#7A9C4E"), GrassC = H("#A3B866"), Moss = H("#5E7F43");
        public static readonly Color Dirt = H("#B08D62"), Path = H("#C8AA7A"), Mud = H("#7D6448"), Sand = H("#D8C08E"), SandWet = H("#B9A57A");
        public static readonly Color SoilDark = H("#5A4232"), SoilLight = H("#7A5A41"), Loam = H("#9B7A55"), Gravel = H("#8C8A84");
        public static readonly Color PondBed = H("#4F6B5E"), Water = H("#6FA6A8"), WaterDeep = H("#4E8C93");
        public static readonly Color FieldSoil = H("#8E6A47"), FieldSoilLight = H("#A07B52"), Wheat = H("#D9B65A"), Sprout = H("#9BBF5E");

        // nature
        public static readonly Color LeafA = H("#6E9A4B"), LeafB = H("#5C8A45"), LeafC = H("#86AE58"), Pine = H("#3F6B4A");
        public static readonly Color Bark = H("#6B4E3A"), Rock = H("#A29C93"), RockDark = H("#878178"), RockLight = H("#BDB7AD");
        public static readonly Color[] Flowers = { H("#F2E8D5"), H("#F0C75E"), H("#E59A8C"), H("#B9A3D9") };

        // built
        public static readonly Color Plaster = H("#EFE3CC"), PlasterWarm = H("#E8D2AE"), Timber = H("#6A4B36"), WoodLight = H("#A47B55");
        public static readonly Color Thatch = H("#CFA65E"), Terracotta = H("#C66F4E"), Slate = H("#6E7C8C"), Stone = H("#B3ACA0");
        public static readonly Color Canvas = H("#E2D3B3"), Hide = H("#B98C62"), WindowLit = H("#FFD98A"), Door = H("#5A3D2B");
        public static readonly Color Fire = H("#FF9A3C"), FireCore = H("#FFE07A"), Ember = H("#C4432B"), Glow = H("#FFB25C");
        public static readonly Color Lamp = H("#FFC86B"), Iron = H("#4A4440");

        // box and room
        public static readonly Color Plinth = H("#5C4033"), PlinthTop = H("#6E4D3C"), Frame = H("#2E2A27");
        /// <summary>The cut edge of the glass seen along its top: pale green-white, the "this is glass" line.</summary>
        public static readonly Color GlassEdge = H("#CFE8DF");
        public static readonly Color Shelf = H("#C49A6C"), ShelfEdge = H("#A87E55"), WallTop = H("#EDE3D3"), WallBottom = H("#D9CBB5");

        // people
        public static readonly Color[] Tunics = { H("#B5523B"), H("#4F7CA8"), H("#D9A441"), H("#5E8C5A"), H("#8A5A8C"), H("#C77D4F"), H("#3F6E73") };
        public static readonly Color[] Skins = { H("#F1C9A5"), H("#D9A47E"), H("#B07A55"), H("#8A5A3C"), H("#E8B98F") };
        public static readonly Color[] Hair = { H("#3B2A20"), H("#6B4430"), H("#C9A066"), H("#2A2420"), H("#9C5A34"), H("#D8D2C8") };
        public static readonly Color Pants = H("#5A4A3E"), Straw = H("#E0C27A");
        /// <summary>
        /// Bright headbands and sashes (spike polish note: camp people in hide colours got lost in the grass). Saturated on
        /// purpose: they are the only strong colour a camp person wears, so the eye finds people first.
        /// </summary>
        public static readonly Color[] Accents = { H("#E0402A"), H("#F2B630"), H("#2E86C8"), H("#1FA38A"), H("#D9487E"), H("#F07A1E") };
    }
}
