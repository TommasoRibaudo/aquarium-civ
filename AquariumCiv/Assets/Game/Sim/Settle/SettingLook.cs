using System;

namespace AquariumCiv.Sim.Settle
{
    /// <summary>
    /// The colours and prop mix of a box's setting (#115), as plain data so it is tested without Unity. The view reads one
    /// <see cref="SettingLook"/> per box (<see cref="For"/> of the shelf box's setting, or <see cref="ForPreset"/> of the sim's
    /// terrain preset) and uses its values in place of the matching <c>Palette</c> constants. The river valley's look IS today's
    /// Palette (a test pins every value to the hex in Palette.cs), so box 1 is unchanged. Colours are sRGB 0xRRGGBB.
    /// </summary>
    public sealed class SettingLook
    {
        public string setting;

        // Ground: the meadow greens and what takes their place.
        public uint grassA, grassB, grassC, moss;       // BoxView.Grass / Meadow / GrassTuft
        public uint dirt, path, sand, sandWet;           // clearings, paths, the shore rim
        public uint pondBed, water, waterDeep;           // the pond / stream
        public uint sprout;                              // meadow, young reeds

        // Trees and rock.
        public uint leafA, leafB, leafC, pine, bark;     // Pieces trees; pine used by the conifers
        public uint rock, rockDark, rockLight;           // outcrops, glass-edge strata, ground at height

        // Height cap: ground above snowLine (world y) blends to snowColor over snowBand. 99 = never.
        public float snowLine = 99f, snowBand = 0.25f;
        public uint snowColor;

        // Sky and fog: the box's backdrop is lerped towards skyTint by skyAmount (0 = untouched).
        public uint skyTint, fogTint;
        public float skyAmount, fogAmount;

        // Props: the share of each tree/prop kind among the starting woods (sums to 1 over the first four), and the scatter on open ground.
        public TreeKind trees;
        public float palmShare, pineShare, broadleafShare, cactusShare;
        public float boulderDensity, tuftDensity;        // 1 = today's river valley

        public enum TreeKind { Broadleaf, Palm, Pine }

        public static readonly SettingLook RiverValley = new SettingLook
        {
            setting = BoxSetting.RiverValley,
            grassA = 0x8DAA5B, grassB = 0x7A9C4E, grassC = 0xA3B866, moss = 0x5E7F43,
            dirt = 0xB08D62, path = 0xC8AA7A, sand = 0xD8C08E, sandWet = 0xB9A57A,
            pondBed = 0x4F6B5E, water = 0x6FA6A8, waterDeep = 0x4E8C93, sprout = 0x9BBF5E,
            leafA = 0x6E9A4B, leafB = 0x5C8A45, leafC = 0x86AE58, pine = 0x3F6B4A, bark = 0x6B4E3A,
            rock = 0xA29C93, rockDark = 0x878178, rockLight = 0xBDB7AD,
            snowLine = 99f, snowColor = 0xF1F4F6,
            skyTint = 0xFFFFFF, fogTint = 0xFFFFFF, skyAmount = 0f, fogAmount = 0f,
            trees = TreeKind.Broadleaf, broadleafShare = 0.7f, pineShare = 0.3f, palmShare = 0f, cactusShare = 0f,
            boulderDensity = 1f, tuftDensity = 1f,
        };

        public static readonly SettingLook DesertOasis = new SettingLook
        {
            setting = BoxSetting.DesertOasis,
            // Sand ground in three warm tones, the oasis green kept for the rim and the palms. Dirt/path stay a shade off the sand so paths read.
            grassA = 0xD8C08E, grassB = 0xCDB27C, grassC = 0xE3CE9F, moss = 0xB8B070,
            dirt = 0xA9815A, path = 0xBF9C70, sand = 0xE6D4A8, sandWet = 0xBFA574,
            pondBed = 0x3F6B66, water = 0x4FB1B0, waterDeep = 0x2E8E94, sprout = 0x8FBF4E,
            leafA = 0x5E9A3F, leafB = 0x4C8A3A, leafC = 0x7BB04A, pine = 0x4F7A3C, bark = 0x7A5A3E,
            rock = 0xB59A78, rockDark = 0x9A7F60, rockLight = 0xD0B994,
            snowLine = 99f, snowColor = 0xF1F4F6,
            skyTint = 0xFFE3B0, fogTint = 0xF2DDB4, skyAmount = 0.35f, fogAmount = 0.3f,
            trees = TreeKind.Palm, palmShare = 0.8f, broadleafShare = 0.1f, pineShare = 0f, cactusShare = 0.1f,
            boulderDensity = 0.6f, tuftDensity = 0.25f,
        };

        public static readonly SettingLook MountainVillage = new SettingLook
        {
            setting = BoxSetting.MountainVillage,
            // A cool high meadow: bluer greens, bare grey rock higher up, a snow cap at the top of the slope, a clear cold stream.
            grassA = 0x7C9D62, grassB = 0x6C8E58, grassC = 0x93AE72, moss = 0x587458,
            dirt = 0x9A8268, path = 0xB4A58C, sand = 0xB7B2A2, sandWet = 0x9C9886,
            pondBed = 0x4A6068, water = 0x78B4C6, waterDeep = 0x4E8AA4, sprout = 0x9DBF7A,
            leafA = 0x5E8A52, leafB = 0x4E7A4E, leafC = 0x72A060, pine = 0x2F5A44, bark = 0x5E4838,
            rock = 0x9A9AA0, rockDark = 0x7A7A82, rockLight = 0xBEC0C6,
            snowLine = 1.85f, snowBand = 0.3f, snowColor = 0xF1F4F6,
            skyTint = 0xC7D8EC, fogTint = 0xD5E2EE, skyAmount = 0.35f, fogAmount = 0.35f,
            trees = TreeKind.Pine, pineShare = 0.85f, broadleafShare = 0.15f, palmShare = 0f, cactusShare = 0f,
            boulderDensity = 2.2f, tuftDensity = 0.8f,
        };

        public static SettingLook For(string setting) => setting switch
        {
            BoxSetting.DesertOasis => DesertOasis,
            BoxSetting.MountainVillage => MountainVillage,
            _ => RiverValley,
        };

        /// <summary>The look for a sim terrain preset (what the view has at hand when it builds the land).</summary>
        public static SettingLook ForPreset(int preset) => For(TerrainSpec.SettingOf(preset));

        /// <summary>The ground above the snow line blends to snow: 0 below the line, 1 a band above it.</summary>
        public float SnowAmount(float y) => snowLine > 50f ? 0f : Terrain.Smooth(snowLine, snowLine + snowBand, y);
    }
}
