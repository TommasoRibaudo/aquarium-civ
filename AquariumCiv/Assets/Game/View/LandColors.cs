using AquariumCiv.Sim.Settle;
using UnityEngine;

namespace AquariumCiv.View
{
    /// <summary>
    /// One box's natural colours (#115): the <see cref="SettingLook"/> of its terrain preset as <see cref="Color"/>s, ready for
    /// the chunk builders on their worker threads. The river valley's values are <see cref="Palette"/>'s (a test pins them), so
    /// box 1 draws exactly as it did. What people built keeps the Palette: only the land, the water and what grows on it change.
    /// </summary>
    public sealed class LandColors
    {
        public readonly SettingLook look;
        public readonly Color grassA, grassB, grassC, moss, dirt, path, sand, sandWet, pondBed, water, waterDeep, sprout;
        public readonly Color leafA, leafB, leafC, pine, bark, rock, rockDark, rockLight, snow;
        /// <summary>The three colours of a grass tuft. The valley's are its grass; on sand that would hide them, so the other settings use dry grass and fresh shoots.</summary>
        public readonly Color tuftA, tuftB, tuftC;
        public bool Snowy => look.snowLine < 50f;

        public static readonly LandColors River = new(SettingLook.RiverValley), Desert = new(SettingLook.DesertOasis), Mountain = new(SettingLook.MountainVillage);

        public static LandColors For(SettingLook l) => l == SettingLook.DesertOasis ? Desert : l == SettingLook.MountainVillage ? Mountain : River;
        public static LandColors ForPreset(int preset) => For(SettingLook.ForPreset(preset));

        /// <summary>The room behind the box in this setting, before the hour: the sky tint goes on first, and the day's light then
        /// scales the result (<see cref="DayLightDriver"/>, the wall by the sun). So a tint shifts the room's hue at every hour and never
        /// adds light to a night. At skyAmount 0 this is <paramref name="room"/> itself.</summary>
        public Color Room(Color room)
        {
            if (look.skyAmount <= 0f) return room;
            var c = Color.Lerp(room, C(look.skyTint), look.skyAmount);
            c.a = room.a;
            return c;
        }

        /// <summary>The day's fog colour in this setting: multiplied, so it falls with the hour's. At fogAmount 0 this is <paramref name="fog"/> itself.</summary>
        public Color Fog(Color fog)
        {
            if (look.fogAmount <= 0f) return fog;
            var t = C(look.fogTint);
            return Color.Lerp(fog, new Color(fog.r * t.r, fog.g * t.g, fog.b * t.b, fog.a), look.fogAmount);
        }

        /// <summary>sRGB 0xRRGGBB as a Color, as Palette's hex strings are.</summary>
        public static Color C(uint hex) => new Color32((byte)(hex >> 16), (byte)(hex >> 8), (byte)hex, 255);

        LandColors(SettingLook l)
        {
            look = l;
            grassA = C(l.grassA); grassB = C(l.grassB); grassC = C(l.grassC); moss = C(l.moss);
            dirt = C(l.dirt); path = C(l.path); sand = C(l.sand); sandWet = C(l.sandWet);
            pondBed = C(l.pondBed); water = C(l.water); waterDeep = C(l.waterDeep); sprout = C(l.sprout);
            leafA = C(l.leafA); leafB = C(l.leafB); leafC = C(l.leafC); pine = C(l.pine); bark = C(l.bark);
            rock = C(l.rock); rockDark = C(l.rockDark); rockLight = C(l.rockLight); snow = C(l.snowColor);
            bool valley = l == SettingLook.RiverValley;
            tuftA = valley ? grassC : moss; tuftB = leafC; tuftC = valley ? grassA : sprout;
        }
    }
}
