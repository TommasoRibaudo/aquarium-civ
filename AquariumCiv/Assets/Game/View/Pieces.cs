using System;
using AquariumCiv.Sim;
using UnityEngine;

namespace AquariumCiv.View
{
    /// <summary>
    /// The box's buildings and props, drawn with <see cref="MeshBuilder"/> and the <see cref="Palette"/> (ported from the
    /// style-kit spike's StyleBox and the Growth Lab's GrowthArt). Every function draws in the builder's local space: the
    /// caller sets <c>mb.M</c> to the lot's frame (origin = lot centre on the ground, local -z = the front). Windows and
    /// lanterns are lamps (<see cref="MeshBuilder.lamp"/>): they light at night through <see cref="NightLamps"/>.
    /// </summary>
    public static partial class Pieces
    {
        /// <summary>Lot-local ground height: maps a local point onto the terrain and back (for fences and crops on slopes).</summary>
        public delegate Vector3 Ground(Vector3 local);

        // ---------------- dispatcher ----------------
        /// <summary>Draws one <see cref="PieceData"/> into <paramref name="mb"/>, whose M must already be the lot's frame.</summary>
        public static void Draw(MeshBuilder mb, PieceData p, Ground ground)
        {
            float w = p.w, d = p.d;
            bool camp = p.era == Era.Camp, town = p.era == Era.Town;
            Vector3 Front(float depth) => new(0, 0, -d / 2f + 0.02f + depth / 2f);
            switch (p.kind)
            {
                case PieceKind.Home:
                    if (camp)
                    {
                        Teepee(mb, Vector3.zero, Mathf.Clamp(Mathf.Min(w, d) * 0.42f, 0.14f, 0.38f), Mathf.Clamp(Mathf.Min(w, d) * 1.1f, 0.42f, 0.66f),
                            p.seed % 2 == 0 ? Palette.Hide : Palette.Canvas, 0f, p.seed);
                        break;
                    }
                    {
                        int storeys = p.storeys > 0 ? p.storeys : town ? 2 : 1;
                        float hd = Mathf.Min(d * 0.9f, town ? 0.32f : 0.42f), hw = w * 0.92f;
                        float wallH = storeys > 1 ? 0.2f : town ? 0.27f : 0.3f;
                        float roofH = Mathf.Clamp(hd * 0.62f, 0.2f, 0.3f);
                        House(mb, Front(hd), hw, hd, wallH, roofH, Wall(p.seed), RoofColor(p.roof, p.seed, p.era), p.chimney, true, storeys, Phase(p.seed));
                    }
                    break;
                case PieceKind.Fire: Firepit(mb, Vector3.zero, true, Phase(p.seed)); break;
                case PieceKind.Well:
                    if (town) Fountain(mb, Vector3.zero, p.seed);
                    else Well(mb, Vector3.zero, !camp);
                    break;
                case PieceKind.Woodpile: Woodpile(mb, Vector3.zero, 0f); break;
                case PieceKind.DryingRack: DryingRack(mb, Vector3.zero); break;
                case PieceKind.LeanTo: LeanTo(mb); break;
                case PieceKind.Bench: Bench(mb, Vector3.zero); break;
                case PieceKind.Lantern: Lantern(mb, Vector3.zero, Phase(p.seed)); break;
                case PieceKind.Granary: Granary(mb, Vector3.zero); break;
                case PieceKind.Crates: Crates(mb, Vector3.zero); break;
                case PieceKind.LogSeat: Log(mb, new Vector3(0, 0.035f, 0), 0.035f, Mathf.Max(0.2f, w), 90f, Palette.Bark); break;
                case PieceKind.Stall:
                    if (town) MarketStall(mb, Vector3.zero, p.seed);
                    else Stall(mb, Vector3.zero, 0f, Palette.Tunics[Mathf.Abs(p.seed) % Palette.Tunics.Length]);
                    break;
                case PieceKind.Windmill:
                    if (camp) Granary(mb, Vector3.zero);
                    else if (town) TowerMill(mb, Vector3.zero, p.seed * 37f, Phase(p.seed));
                    else Windmill(mb, Vector3.zero, p.seed * 37f, Palette.Plaster, false, Phase(p.seed));
                    break;
                case PieceKind.Lumbermill:
                    if (camp) { Woodpile(mb, new Vector3(0, 0, -d * 0.1f), 20f); Log(mb, new Vector3(w * 0.2f, 0.03f, d * 0.2f), 0.035f, 0.3f, 70f, Palette.Bark); }
                    else
                    {
                        Lumbermill(mb, Vector3.zero, w, d);
                        if (town) SawFrame(mb, new Vector3(w * 0.05f, 0, -d * 0.36f));
                        if (p.variant == 1) { Woodpile(mb, new Vector3(w * 0.25f, 0, -d * 0.3f), 0f); Woodpile(mb, new Vector3(-w * 0.32f, 0, d * 0.3f), 80f); } // a timber yard
                    }
                    break;
                case PieceKind.Market:
                    if (camp)
                    {
                        mb.Box(new Vector3(0, 0.005f, 0), new Vector3(0.3f, 0.01f, 0.2f), Palette.Hide);
                        Crates(mb, new Vector3(-0.05f, 0, 0));
                    }
                    else if (town) TownMarketHall(mb, Vector3.zero, w, d, Phase(p.seed), p.seed);
                    else MarketHall(mb, Vector3.zero, w, d, false, Phase(p.seed));
                    break;
                case PieceKind.Tavern:
                    if (camp) { Firepit(mb, Vector3.zero, true, Phase(p.seed)); break; }
                    House(mb, Front(0.34f), w * 0.9f, 0.34f, town ? 0.2f : 0.28f, 0.26f, Palette.PlasterWarm, town ? Palette.Terracotta : Palette.Thatch, true, true, town ? 2 : 1, Phase(p.seed));
                    {
                        var sign = new Vector3(w * 0.5f, 0, -d / 2f + 0.02f);
                        mb.Box(sign + new Vector3(0, 0.16f, 0), new Vector3(0.02f, 0.32f, 0.02f), Palette.Timber);
                        mb.Box(sign + new Vector3(-0.05f, 0.27f, 0), new Vector3(0.08f, 0.06f, 0.01f), Palette.Ember);
                        mb.Box(new Vector3(-w * 0.25f, 0.04f, -d / 2f + 0.01f), new Vector3(0.18f, 0.016f, 0.05f), Palette.WoodLight);
                        if (town) { Barrel(mb, new Vector3(w * 0.32f, 0, -d / 2f + 0.03f)); Barrel(mb, new Vector3(w * 0.42f, 0, -d / 2f + 0.07f)); }
                    }
                    break;
                case PieceKind.Church:
                    if (camp) StandingStones(mb, Vector3.zero);
                    else Church(mb, Vector3.zero, w, d, Phase(p.seed));
                    break;
                case PieceKind.StandingStones: StandingStones(mb, Vector3.zero); break;
                case PieceKind.Watermill:
                    if (town) TownWatermill(mb, Vector3.zero, w, d, Phase(p.seed));
                    else Watermill(mb, Vector3.zero, w, d, Phase(p.seed));
                    break;
                case PieceKind.Smithy: Smithy(mb, Vector3.zero, w, d, Phase(p.seed)); break;
                case PieceKind.Dyer: Dyer(mb, Vector3.zero, w, d); break;
                case PieceKind.Shop:
                    if (camp) { Teepee(mb, Vector3.zero, 0.16f, 0.46f, Palette.Canvas, 0f, p.seed); Crates(mb, new Vector3(w * 0.3f, 0, -d * 0.3f)); break; }
                    Shop(mb, Front(0.3f), w * 0.86f, 0.3f, town, Phase(p.seed), p.seed);
                    break;
                case PieceKind.Workshop:
                    if (camp) { LeanTo(mb); break; }
                    if (town) { TradeWorkshop(mb, w, d, TradeOf(p), Phase(p.seed), p.seed); break; }
                    House(mb, Front(0.3f) + new Vector3(-w * 0.1f, 0, 0), w * 0.66f, 0.3f, 0.24f, 0.2f, Wall(p.seed), town ? Palette.Slate : Palette.Thatch, true, true, 1, Phase(p.seed));
                    Shed(mb, new Vector3(w * 0.32f, 0, d * 0.05f), 0.16f, 0.22f, 0.17f, Palette.WoodLight, Palette.Timber);
                    Crates(mb, new Vector3(w * 0.25f, 0, -d * 0.32f));
                    break;
                case PieceKind.Guildhall:
                    if (camp) { StandingStones(mb, Vector3.zero); break; }
                    if (town) TownGuildhall(mb, w, d, Phase(p.seed), p.seed);
                    else Guildhall(mb, Front(0.36f), w * 0.92f, 0.36f, Phase(p.seed), p.seed);
                    break;
                case PieceKind.Bakery:
                    if (camp) { Teepee(mb, Vector3.zero, 0.16f, 0.46f, Palette.Canvas, 0f, p.seed); break; } // a camp has no bakery; only a stray save gets here
                    if (town) { TownBakery(mb, w, d, Phase(p.seed), p.seed); break; }
                    House(mb, Front(0.3f), w * 0.8f, 0.3f, 0.2f, 0.18f, Palette.PlasterWarm, Palette.Terracotta, true, true, 1, Phase(p.seed));
                    break;
                case PieceKind.Barn:
                    if (camp) { DryingRack(mb, Vector3.zero); break; }
                    if (p.variant == 0) Barn(mb, Vector3.zero, Mathf.Min(w * 0.85f, 0.26f), Mathf.Min(d * 0.85f, 0.2f), 0.13f, RoofColor(RoofStyle.Auto, p.seed, Era.Village));
                    else { Shed(mb, new Vector3(0, 0, d * 0.1f), Mathf.Min(w * 0.8f, 0.24f), Mathf.Min(d * 0.7f, 0.17f), 0.13f, Palette.Thatch, Palette.Timber); Woodpile(mb, new Vector3(0, 0, d * 0.08f), 90f); }
                    break;
                case PieceKind.Gathering: Gathering(mb, ground, w, d, p.seed); break;
                case PieceKind.Orchard: Orchard(mb, ground, w, d, p.seed); break;
                case PieceKind.Herd: Herd(mb, ground, w, d, p.seed, Mathf.Max(2, p.variant)); break;
                case PieceKind.Jetty: Jetty(mb, ground, w, d); break;
                case PieceKind.Quarry: Quarry(mb, ground, w, d, p.seed); break;
                case PieceKind.Kiln: Kiln(mb, w, d); break;
                case PieceKind.Outcrop: Outcrop(mb, ground, Mathf.Max(0.1f, w * 0.5f), p.variant, p.seed); break;
            }
        }

        // ---------------- #33: the sim's other lot kinds and the shapings ----------------
        /// <summary>A shop: a two-storey town house (one in the village) with a striped awning and crates out front.</summary>
        public static void Shop(MeshBuilder mb, Vector3 at, float w, float d, bool town, float lampPhase, int seed)
        {
            House(mb, at, w, d, town ? 0.2f : 0.26f, 0.22f, Wall(seed), RoofColor(RoofStyle.Auto, seed, Era.Town), false, true, town ? 2 : 1, lampPhase);
            var aw = at + new Vector3(0, 0.27f, -d / 2f - 0.05f);
            mb.Box(aw, new Vector3(w * 0.85f, 0.012f, 0.11f), Palette.Tunics[Mathf.Abs(seed) % Palette.Tunics.Length], Quaternion.Euler(-18, 0, 0), true);
            Crates(mb, at + new Vector3(-w * 0.3f, 0, -d / 2f - 0.09f));
        }

        /// <summary>The guildhall (a town placeholder until M6): a stone plinth, two jettied storeys, a banner.</summary>
        public static void Guildhall(MeshBuilder mb, Vector3 at, float w, float d, float lampPhase, int seed)
        {
            mb.Box(at + new Vector3(0, 0.04f, 0), new Vector3(w + 0.08f, 0.08f, d + 0.08f), Palette.RockDark);
            House(mb, at + new Vector3(0, 0.05f, 0), w, d, 0.22f, 0.26f, Palette.PlasterWarm, Palette.Slate, true, true, 2, lampPhase);
            var pole = at + new Vector3(-w * 0.45f, 0, -d / 2f - 0.04f);
            mb.Box(pole + new Vector3(0, 0.3f, 0), new Vector3(0.018f, 0.6f, 0.018f), Palette.Timber);
            mb.sway = 0.4f;
            mb.Box(pole + new Vector3(0.05f, 0.5f, 0), new Vector3(0.09f, 0.14f, 0.006f), Palette.Accents[Mathf.Abs(seed) % Palette.Accents.Length]);
            mb.sway = 0f;
        }

        /// <summary>A family's barn: plank walls, corner posts, a big door at the front, a steep roof.</summary>
        public static void Barn(MeshBuilder mb, Vector3 at, float w, float d, float h, Color roof)
        {
            var plank = Color.Lerp(Palette.Bark, Palette.WoodLight, 0.45f);
            mb.Box(at + new Vector3(0, h / 2f, 0), new Vector3(w, h, d), plank);
            foreach (var (x, z) in new[] { (-1, -1), (1, -1), (1, 1), (-1, 1) })
                mb.Box(at + new Vector3(x * (w / 2f - 0.008f), h / 2f, z * (d / 2f - 0.008f)), new Vector3(0.022f, h, 0.022f), Palette.Timber);
            mb.Box(at + new Vector3(0, h * 0.38f, -d / 2f - 0.006f), new Vector3(w * 0.42f, h * 0.76f, 0.012f), Palette.Timber);
            float half = d / 2f + 0.04f, rh = h * 0.75f;
            float slope = Mathf.Sqrt(half * half + rh * rh), ang = Mathf.Atan2(rh, half) * Mathf.Rad2Deg;
            for (int s = -1; s <= 1; s += 2)
                mb.Box(at + new Vector3(0, h + rh / 2f, s * half / 2f), new Vector3(w + 0.06f, 0.028f, slope), roof, Quaternion.Euler(s * ang, 0, 0), true);
            foreach (int s in new[] { -1, 1 })
            {
                float x = at.x + s * w / 2f;
                var a = new Vector3(x, at.y + h, at.z - d / 2f); var b = new Vector3(x, at.y + h + rh * (d / 2f) / half, at.z); var c = new Vector3(x, at.y + h, at.z + d / 2f);
                if (s > 0) mb.Tri(a, b, c, plank); else mb.Tri(c, b, a, plank);
            }
        }

        static Vector3 LotSpot(int seed, int k, float w, float d, float inset = 0.7f) =>
            new((Land.Hash3(seed, k, 41) - 0.5f) * w * inset, 0, (Land.Hash3(seed, k, 42) - 0.5f) * d * inset);

        /// <summary>The camp's farm: gathering grounds, berry bushes and a drying rack (decisions 2026-10-29: farms
        /// exist in the camp but look different).</summary>
        public static void Gathering(MeshBuilder mb, Ground ground, float w, float d, int seed)
        {
            for (int k = 0; k < 5; k++) Bush(mb, ground(LotSpot(seed, k, w, d)), mb.Range(0.06f, 0.09f), true);
            DryingRack(mb, ground(new Vector3(0, 0, -d * 0.32f)));
            mb.Prism(ground(new Vector3(w * 0.28f, 0, -d * 0.3f)), 0.035f, 0.03f, 0.06f, 7, Palette.Bark, topCol: Palette.Ember); // a basket of berries
        }

        /// <summary>Fruit trees in rows (an orchard on a grove, village era on).</summary>
        public static void Orchard(MeshBuilder mb, Ground ground, float w, float d, int seed)
        {
            for (int i = 0; i < 2; i++)
                for (int j = 0; j < 3; j++)
                {
                    var g = ground(new Vector3((i - 0.5f) * w * 0.5f + (Land.Hash3(seed, i, j) - 0.5f) * 0.03f, 0, (j - 1f) * d * 0.3f));
                    FruitTree(mb, g, 0.5f + 0.12f * Land.Hash3(seed, j, i + 9));
                }
        }

        public static void FruitTree(MeshBuilder mb, Vector3 g, float s)
        {
            Tree(mb, g, s, false);
            for (int f = 0; f < 5; f++)
            {
                float a = f * 1.3f + mb.Range(0f, 1f);
                mb.Ico(g + new Vector3(Mathf.Cos(a) * 0.15f * s, (0.38f + mb.Range(0f, 0.2f)) * s, Mathf.Sin(a) * 0.15f * s), 0.018f, Vector3.one, f % 2 == 0 ? Palette.Ember : Palette.Wheat, 0.1f);
            }
        }

        /// <summary>A few sheep (a herd shaping, a pasture): woolly bodies, dark heads, grazing every which way.</summary>
        public static void Herd(MeshBuilder mb, Ground ground, float w, float d, int seed, int count)
        {
            for (int k = 0; k < count; k++)
            {
                var g = ground(LotSpot(seed, k + 3, w, d, 0.6f));
                var f = Quaternion.Euler(0, Land.Hash3(seed, k, 43) * 360f, 0);
                mb.Ico(g + Vector3.up * 0.05f, 0.045f, new Vector3(1.25f, 0.85f, 0.9f), Color.Lerp(Palette.Plaster, Palette.Canvas, Land.Hash3(seed, k, 44)), 0.2f);
                mb.Ico(g + Vector3.up * 0.06f + f * new Vector3(0.055f, 0, 0), 0.02f, new Vector3(1.2f, 0.9f, 0.9f), Palette.Door, 0.1f);
                foreach (int s in new[] { -1, 1 })
                    mb.Box(g + f * new Vector3(s * 0.025f, 0.012f, 0), new Vector3(0.01f, 0.024f, 0.01f), Palette.Door);
            }
        }

        /// <summary>A fishing jetty: planks on posts from the lot out over the water at its back (+z), a small boat.</summary>
        public static void Jetty(MeshBuilder mb, Ground ground, float w, float d)
        {
            float water = Land.WaterY - mb.M.m13; // the water level in the lot's local space
            float DeckY(float z) => Mathf.Max(ground(new Vector3(0, 0, z)).y, water) + 0.03f;
            float len = d / 2f + 0.3f;
            int n = 0;
            for (float z = -d * 0.2f; z < len; z += 0.05f, n++)
                mb.Box(new Vector3(0, DeckY(z), z), new Vector3(0.12f, 0.012f, 0.042f), n % 2 == 0 ? Palette.WoodLight : Color.Lerp(Palette.WoodLight, Palette.Bark, 0.3f));
            foreach (int s in new[] { -1, 1 })
                for (float z = 0.05f; z < len; z += 0.15f)
                    mb.Box(new Vector3(s * 0.065f, DeckY(z) - 0.04f, z), new Vector3(0.018f, 0.1f, 0.018f), Palette.Timber);
            var boat = new Vector3(0.14f, water + 0.015f, len - 0.08f);
            mb.Box(boat, new Vector3(0.07f, 0.03f, 0.18f), Palette.Bark);
            mb.Box(boat + Vector3.up * 0.012f, new Vector3(0.05f, 0.012f, 0.15f), Palette.WoodLight);
            Crates(mb, ground(new Vector3(-0.12f, 0, -d * 0.15f)));
        }

        /// <summary>A quarry: rubble, cut blocks and a timber hoist.</summary>
        public static void Quarry(MeshBuilder mb, Ground ground, float w, float d, int seed)
        {
            for (int k = 0; k < 6; k++)
                mb.Ico(ground(LotSpot(seed, k, w, d, 0.8f)), mb.Range(0.045f, 0.08f), new Vector3(1.2f, 0.7f, 1f), k % 2 == 0 ? Palette.Rock : Palette.RockLight, 0.25f, Palette.RockDark);
            for (int k = 0; k < 4; k++)
            {
                var g = ground(new Vector3(-w * 0.2f + (k % 2) * 0.09f, 0, -d * 0.25f + (k / 2) * 0.08f));
                mb.Box(g + new Vector3(0, 0.025f + (k == 3 ? 0.05f : 0), 0), new Vector3(0.07f, 0.05f, 0.06f), Palette.Stone, Quaternion.Euler(0, k * 7f, 0));
            }
            var h = ground(new Vector3(w * 0.22f, 0, 0));
            foreach (int s in new[] { -1, 1 }) mb.Box(h + new Vector3(s * 0.06f, 0.14f, 0), new Vector3(0.018f, 0.3f, 0.018f), Palette.Timber, Quaternion.Euler(0, 0, -s * 12f));
            mb.Box(h + new Vector3(0, 0.28f, 0), new Vector3(0.1f, 0.018f, 0.018f), Palette.Timber);
        }

        /// <summary>A kiln: a clay dome with a glowing mouth, pots drying out front, a woodpile.</summary>
        public static void Kiln(MeshBuilder mb, float w, float d)
        {
            var c = new Vector3(0, 0, d * 0.1f);
            mb.Ico(c + Vector3.up * 0.06f, 0.15f, new Vector3(1f, 0.9f, 1f), Color.Lerp(Palette.Terracotta, Palette.Loam, 0.4f), 0.08f);
            mb.Prism(c + Vector3.up * 0.18f, 0.035f, 0.03f, 0.08f, 6, Palette.RockDark);
            mb.emission = 0.8f;
            mb.Box(c + new Vector3(0, 0.05f, -0.135f), new Vector3(0.07f, 0.06f, 0.02f), Palette.Fire);
            mb.emission = 0f;
            for (int k = 0; k < 4; k++)
                mb.Prism(new Vector3(-w * 0.3f + k * 0.06f, 0, -d * 0.32f), 0.022f, 0.016f, 0.05f, 7, Color.Lerp(Palette.Terracotta, Palette.SoilLight, k * 0.2f));
            Woodpile(mb, new Vector3(w * 0.3f, 0, -d * 0.25f), 90f);
        }

        /// <summary>The Keeper's outcrop: pushed-up rock, tinted by what it holds (0 stone, 1 clay by the water, 2 ore).</summary>
        public static void Outcrop(MeshBuilder mb, Ground ground, float r, int deposit, int seed)
        {
            Color a, b;
            switch (deposit)
            {
                case 1: a = Color.Lerp(Palette.Terracotta, Palette.Loam, 0.5f); b = Palette.Loam; break;
                case 2: a = Palette.RockDark; b = Color.Lerp(Palette.RockDark, Palette.Iron, 0.5f); break;
                default: a = Palette.Rock; b = Palette.RockLight; break;
            }
            for (int k = 0; k < 6; k++)
            {
                float ang = (k + Land.Hash3(seed, k, 45)) / 6f * Mathf.PI * 2f, rr = r * (k == 0 ? 0f : 0.45f + 0.4f * Land.Hash3(seed, k, 46));
                var g = ground(new Vector3(Mathf.Cos(ang) * rr, 0, Mathf.Sin(ang) * rr));
                float size = (k == 0 ? 0.11f : 0.055f) + 0.03f * Land.Hash3(seed, k, 47);
                mb.Ico(g + Vector3.up * size * 0.25f, size, new Vector3(1.1f, k == 0 ? 1.1f : 0.75f, 1f), k % 2 == 0 ? a : b, 0.28f, Palette.RockDark);
            }
            if (deposit == 2)
                for (int k = 0; k < 4; k++)
                    mb.Ico(ground(new Vector3((Land.Hash3(seed, k, 48) - 0.5f) * r, 0, (Land.Hash3(seed, k, 49) - 0.5f) * r)) + Vector3.up * 0.05f, 0.014f, Vector3.one, Palette.Ember, 0.1f);
        }

        /// <summary>A lamp's phase in 0..1 from a seed: when in the dusk it comes on.</summary>
        public static float Phase(int seed) => Land.Hash3(seed, 7, 3);

        public static Color RoofColor(RoofStyle style, int seed, Era era)
        {
            switch (style)
            {
                case RoofStyle.Thatch: return Palette.Thatch;
                case RoofStyle.Terracotta: return Palette.Terracotta;
                case RoofStyle.Slate: return Palette.Slate;
            }
            float h = Land.Hash3(seed, 5, 9);
            if (era == Era.Village) return h < 0.62f ? Palette.Thatch : h < 0.85f ? Palette.Terracotta : Palette.Slate;
            return h < 0.25f ? Palette.Thatch : h < 0.7f ? Palette.Terracotta : Palette.Slate;
        }

        public static Color Wall(int seed) => Land.Hash3(seed, 6, 2) < 0.35f ? Palette.PlasterWarm : Palette.Plaster;

        // ---------------- houses ----------------
        /// <summary>The half-timbered house: stone footing, plaster walls, timber posts, a gable roof along x with an
        /// overhang, a door and windows on the front (-z), optional chimney and jettied upper storeys.</summary>
        public static void House(MeshBuilder mb, Vector3 at, float w, float d, float wallH, float roofH, Color wall, Color roof, bool chimney, bool timber = true, int storeys = 1, float lampPhase = 0.5f)
        {
            mb.Box(at + new Vector3(0, 0.05f, 0), new Vector3(w + 0.05f, 0.1f, d + 0.05f), Palette.Stone);
            float y0 = at.y + 0.1f;
            for (int s = 0; s < storeys; s++)
            {
                float jut = s * 0.025f;
                float sw = w + 2 * jut, sd = d + 2 * jut;
                var c = new Vector3(at.x, y0 + wallH / 2f, at.z);
                mb.Box(c, new Vector3(sw, wallH, sd), s == 0 ? wall : Color.Lerp(wall, Palette.PlasterWarm, 0.4f));
                if (timber)
                {
                    foreach (var (x, z) in new[] { (-1, -1), (1, -1), (1, 1), (-1, 1) })
                        mb.Box(new Vector3(at.x + x * (sw / 2f - 0.01f), y0 + wallH / 2f, at.z + z * (sd / 2f - 0.01f)), new Vector3(0.033f, wallH, 0.033f), Palette.Timber);
                    mb.Box(new Vector3(at.x, y0 + wallH - 0.015f, at.z - sd / 2f - 0.003f), new Vector3(sw, 0.03f, 0.012f), Palette.Timber);
                    if (s > 0) mb.Box(new Vector3(at.x, y0 + 0.012f, at.z - sd / 2f - 0.004f), new Vector3(sw, 0.024f, 0.014f), Palette.Timber);
                }
                float wy = y0 + wallH * 0.6f;
                mb.emission = 0.35f;
                mb.lamp = Mathf.Repeat(lampPhase + s * 0.17f, 1f);
                if (s == 0) mb.Box(new Vector3(at.x - sw * 0.2f, wy, at.z - sd / 2f - 0.006f), new Vector3(0.075f, 0.07f, 0.012f), Palette.WindowLit);
                else
                {
                    mb.Box(new Vector3(at.x - sw * 0.22f, wy, at.z - sd / 2f - 0.006f), new Vector3(0.065f, 0.06f, 0.012f), Palette.WindowLit);
                    mb.Box(new Vector3(at.x + sw * 0.22f, wy, at.z - sd / 2f - 0.006f), new Vector3(0.065f, 0.06f, 0.012f), Palette.WindowLit);
                }
                mb.Box(new Vector3(at.x + sw / 2f + 0.006f, wy, at.z), new Vector3(0.012f, 0.065f, 0.07f), Palette.WindowLit);
                mb.Box(new Vector3(at.x - sw / 2f - 0.006f, wy, at.z), new Vector3(0.012f, 0.065f, 0.07f), Palette.WindowLit);
                mb.emission = 0f;
                mb.lamp = -1f;
                if (s == 0) mb.Box(new Vector3(at.x - sw * 0.2f, wy - 0.045f, at.z - sd / 2f - 0.012f), new Vector3(0.1f, 0.015f, 0.02f), Palette.Timber); // sill
                y0 += wallH;
            }
            float top = y0, ww = w + 2 * (storeys - 1) * 0.025f, dd = d + 2 * (storeys - 1) * 0.025f;
            float oh = 0.065f, half = dd / 2f + oh;
            float slope = Mathf.Sqrt(half * half + roofH * roofH), ang = Mathf.Atan2(roofH, half) * Mathf.Rad2Deg;
            for (int s = -1; s <= 1; s += 2)
                mb.Box(new Vector3(at.x, top + roofH / 2f, at.z + s * half / 2f), new Vector3(ww + 2 * oh, 0.034f, slope), roof, Quaternion.Euler(s * ang, 0, 0), true);
            foreach (int s in new[] { -1, 1 })
            {
                float x = at.x + s * ww / 2f;
                var a = new Vector3(x, top, at.z - dd / 2f); var b = new Vector3(x, top + roofH * (dd / 2f) / half, at.z); var c = new Vector3(x, top, at.z + dd / 2f);
                if (s > 0) mb.Tri(a, b, c, wall); else mb.Tri(c, b, a, wall);
            }
            mb.Box(new Vector3(at.x + w * 0.18f, at.y + 0.1f + 0.09f, at.z - d / 2f - 0.006f), new Vector3(0.1f, 0.18f, 0.012f), Palette.Door);
            if (chimney) mb.Box(new Vector3(at.x + ww * 0.28f, top + roofH * 0.75f, at.z + dd * 0.12f), new Vector3(0.07f, roofH * 0.9f, 0.07f), Palette.Stone);
        }

        // ---------------- camp ----------------
        /// <summary>A teepee with its door flap facing local -z rotated by <paramref name="doorYaw"/>, and poles out of the top.</summary>
        public static void Teepee(MeshBuilder mb, Vector3 p, float r, float h, Color cover, float doorYaw, int seed = 0)
        {
            p -= Vector3.up * 0.02f;
            mb.Prism(p, r, 0.03f, h, 7, cover, Quaternion.Euler(0, Land.Hash(seed, 3) * 50f, 0), true, false, Palette.Bark);
            // A painted band round the cover, so a white tent isn't plain (spike polish note).
            float bandY = h * 0.32f, bandR = Mathf.Lerp(r, 0.03f, bandY / h) + 0.004f;
            var band = Palette.Accents[Mathf.Abs(seed) % Palette.Accents.Length];
            mb.Prism(p + Vector3.up * bandY, bandR, Mathf.Lerp(r, 0.03f, (bandY + 0.035f) / h) + 0.004f, 0.035f, 7, Color.Lerp(band, cover, 0.25f),
                Quaternion.Euler(0, Land.Hash(seed, 3) * 50f, 0), false);
            var fwd = Quaternion.Euler(0, doorYaw, 0) * Vector3.back;
            var right = Vector3.Cross(Vector3.up, fwd);
            var basePt = p + fwd * (r * 0.93f);
            mb.Tri(basePt - right * r * 0.5f + Vector3.up * 0.01f, basePt + Vector3.up * h * 0.38f - fwd * r * 0.3f, basePt + right * r * 0.5f + Vector3.up * 0.01f, Palette.Door);
            for (int i = 0; i < 5; i++)
            {
                float a = i / 5f * Mathf.PI * 2f;
                var tip = p + Vector3.up * (h + 0.09f) + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * 0.05f;
                var mid = p + Vector3.up * h * 0.85f;
                var dir = tip - mid;
                mb.Prism(mid, 0.008f, 0.006f, dir.magnitude, 4, Palette.Bark, Quaternion.FromToRotation(Vector3.up, dir.normalized));
            }
        }

        /// <summary>
        /// The fire pit: a stone ring, crossed logs, an ember bed and flames. Flames are 1.5× the spike's (props 1.5–2×,
        /// life-in-the-box §5) with a bright core, so the fire reads and blooms at full-box zoom; the view adds a glow
        /// billboard (<see cref="BoxView"/>) and a warm pool on the ground. <paramref name="seats"/> adds three log seats.
        /// </summary>
        public static void Firepit(MeshBuilder mb, Vector3 p, bool seats, float phase = 0f) => Firepit(mb, p, seats, phase, FireLooks.Active);

        /// <summary><see cref="Firepit(MeshBuilder, Vector3, bool, float)"/> in a given look (#65). Embers warms the stones
        /// and seats in the mesh and lays its logs radially, hot ends in; the others share the #63 ring and logs.</summary>
        public static void Firepit(MeshBuilder mb, Vector3 p, bool seats, float phase, FireLook look)
        {
            bool warm = look == FireLook.Embers;
            mb.emission = warm ? 0.12f : 0f;
            for (int i = 0; i < 9; i++)
            {
                float a = i / 9f * Mathf.PI * 2f;
                var rock = i % 2 == 0 ? Palette.Rock : Palette.RockDark;
                mb.Ico(p + new Vector3(Mathf.Cos(a), 0.01f, Mathf.Sin(a)) * 0.14f, 0.042f, new Vector3(1, 0.7f, 1), warm ? Color.Lerp(rock, Palette.Ember, 0.22f) : rock, 0.25f);
            }
            mb.emission = 0f;
            if (warm) RadialLogs(mb, p);
            else for (int i = 0; i < 3; i++) Log(mb, p + Vector3.up * 0.025f, 0.02f, 0.22f, i * 60f, Palette.Bark);
            mb.emission = 0.6f;
            mb.Prism(p, 0.085f, 0.075f, 0.016f, 7, Palette.Ember);
            FireBody(mb, p, phase, look);
            mb.emission = 0f;
            if (!seats) return;
            mb.emission = warm ? 0.1f : 0f;
            for (int i = 0; i < 3; i++)
            {
                float a = i * 2.1f + 0.4f;
                var q = p + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * 0.4f;
                Log(mb, q + Vector3.up * 0.035f, 0.035f, 0.3f, -a * Mathf.Rad2Deg + 90f, warm ? Color.Lerp(Palette.Bark, Palette.Glow, 0.18f) : Palette.Bark);
            }
            mb.emission = 0f;
        }

        /// <summary>The look's flame body, on top of the ember bed. Soft has only coals: its flames are glow quads
        /// (<see cref="BoxView"/>).</summary>
        public static void FireBody(MeshBuilder mb, Vector3 p, float phase, FireLook look)
        {
            switch (look)
            {
                case FireLook.Soft: Coals(mb, p, 0.06f, 0.03f, 0.022f, 5); break;
                case FireLook.Embers: EmberHeap(mb, p, phase); break;
                case FireLook.Paper: PaperFlames(mb, p, phase); break;
                default: Flames(mb, p, phase); break;
            }
        }

        /// <summary>Linear brightness of a fully emissive face per unit of vertex colour (Stylized.mat's _Emission).</summary>
        public const float EmissionBoost = 3f;

        /// <summary>
        /// A palette colour for a face at emission 1 (the shader ignores the lights there, so day and night match and flat
        /// faces don't go dark). That face shows albedo x <see cref="EmissionBoost"/>, so the colour is scaled to come out as
        /// the palette colour x <paramref name="gain"/> in linear. Bloom starts at 1.3: only gains above it get a bloom.
        /// </summary>
        public static Color Hot(Color c, float gain = 1f)
        {
            float k = Mathf.Pow(gain / EmissionBoost, 1f / 2.2f);
            return new Color(c.r * k, c.g * k, c.b * k, c.a);
        }

        /// <summary>A low glowing dome of coals, unlit (6 sides = 54 vertices).</summary>
        static void Coals(MeshBuilder mb, Vector3 p, float rBottom, float rTop, float h, int sides)
        {
            mb.emission = 1f;
            mb.Prism(p + Vector3.up * 0.012f, rBottom, rTop, h, sides, Hot(Palette.Ember, 1.1f), topCol: Hot(Palette.FlameCoreB, 1.4f), ramp: true);
            mb.emission = 0f;
        }

        /// <summary>Four half logs from the rim to the middle, hot at the inner end. 180 vertices against the #63 logs' 216.</summary>
        static void RadialLogs(MeshBuilder mb, Vector3 p)
        {
            mb.emission = 0.35f;
            for (int i = 0; i < 4; i++)
            {
                var rot = Quaternion.Euler(0, i * 90f + 25f, 0) * Quaternion.Euler(84f, 0, 0);
                mb.Prism(p + Vector3.up * 0.036f, 0.02f, 0.018f, 0.16f, 5, Palette.Ember, rot, topCol: Palette.Bark, ramp: true);
            }
            mb.emission = 0f;
        }

        /// <summary>Embers: the coal heap and three stubby flames, 0.13-0.18 tall, gold going to orange, each on its own
        /// flicker phase. 90 vertices.</summary>
        public static void EmberHeap(MeshBuilder mb, Vector3 p, float phase)
        {
            Coals(mb, p, 0.1f, 0.05f, 0.035f, 6);
            float y0 = 0.03f;
            mb.flameBaseY = mb.M.MultiplyPoint3x4(p + Vector3.up * y0).y; mb.flameHeight = 0.2f; mb.sway = 0f; mb.emission = 1f;
            float yaw0 = H01(phase * 91f + 1f) * 120f;
            for (int i = 0; i < 3; i++)
            {
                var yaw = Quaternion.Euler(0, yaw0 + i * 120f, 0);
                float h = Mathf.Lerp(0.13f, 0.18f, H01(phase * 33f + i * 7.7f));
                mb.flamePhase = Mathf.Repeat(phase + i * 0.37f, 1f);
                mb.Prism(p + yaw * new Vector3(0, y0, 0.03f), 0.052f, 0f, h, 4, Hot(Palette.FlameCoreB, 1f), yaw * Quaternion.Euler(10f, 0, 0), topCol: Hot(Palette.FlameBodyB, 1f), ramp: true);
            }
            mb.emission = 0f; mb.flameHeight = 0f;
        }

        /// <summary>
        /// Paper: six folded tongues, back to front: three tall orange ones, two gold-orange, one gold (0.27 tall at most).
        /// Each is a bent double pyramid of 8 faces, 24 vertices (144 in all), two tones per tongue, like a folded strip of
        /// paper; the fold reads with no lights because the tones are in the vertex colours. Each tongue flickers on its own
        /// phase.
        /// </summary>
        public static void PaperFlames(MeshBuilder mb, Vector3 p, float phase)
        {
            float y0 = 0.012f;
            mb.flameBaseY = mb.M.MultiplyPoint3x4(p + Vector3.up * y0).y; mb.flameHeight = 0.3f; mb.sway = 0f; mb.emission = 1f;
            float yaw0 = H01(phase * 91f + 1f) * 120f;
            void Tongue(int i, float yaw, float r, float h, float w, Color low, Color belly, Color tip)
            {
                mb.flamePhase = Mathf.Repeat(phase + i * 0.37f, 1f);
                float a = yaw0 + yaw + (H01(phase * 57f + i * 3.1f) - 0.5f) * 24f;
                PaperTongue(mb, p + Quaternion.Euler(0, a, 0) * new Vector3(0, y0, r), a, h, w, 0.04f + 0.03f * H01(phase * 71f + i * 5.3f), low, belly, tip);
            }
            Color gold = Hot(Palette.FlameCoreA, 1.3f), amber = Hot(Palette.FlameCoreB, 1.25f), orange = Hot(Palette.FlameBodyA, 1.1f), red = Hot(Palette.FlameBodyB, 1f);
            Tongue(0, 0f, 0.05f, 0.27f, 0.078f, orange, orange, red);
            Tongue(1, 125f, 0.05f, 0.23f, 0.07f, orange, orange, red);
            Tongue(2, 235f, 0.05f, 0.25f, 0.07f, orange, orange, red);
            Tongue(3, 60f, 0.03f, 0.19f, 0.066f, amber, amber, orange);
            Tongue(4, 190f, 0.03f, 0.17f, 0.062f, amber, amber, orange);
            Tongue(5, 300f, 0.012f, 0.14f, 0.058f, gold, gold, amber);
            mb.emission = 0f; mb.flameHeight = 0f;
        }

        /// <summary>One tongue: from a pointed foot out to a diamond belly at 45 % of the height, then to a tip leaning
        /// <paramref name="lean"/> outwards. Faces at each side of the blade are the lit fold, the others a shade deeper.</summary>
        public static void PaperTongue(MeshBuilder mb, Vector3 foot, float yawDeg, float h, float halfWidth, float lean, Color low, Color belly, Color tip)
        {
            var q = Quaternion.Euler(0, yawDeg, 0);
            var bellyC = foot + q * new Vector3(0, h * 0.45f, lean * 0.3f);
            var tipP = foot + q * new Vector3(lean * 0.35f, h, lean);
            Vector3 wide = q * Vector3.right * halfWidth, deep = q * Vector3.forward * (halfWidth * 0.45f);
            var ring = new[] { bellyC + wide, bellyC + deep, bellyC - wide, bellyC - deep };
            for (int i = 0; i < 4; i++)
            {
                var a = ring[i]; var b = ring[(i + 1) % 4];
                bool lit = i % 2 == 0;
                Color l = lit ? low : Fold(low), m = lit ? belly : Fold(belly), t = lit ? tip : Fold(tip);
                FacetOut(mb, foot, a, b, bellyC, l, m, m);
                FacetOut(mb, a, tipP, b, bellyC, m, t, m);
            }
        }

        /// <summary>The shaded side of a paper fold.</summary>
        static Color Fold(Color c) => new Color(c.r * 0.8f, c.g * 0.8f, c.b * 0.8f, c.a);

        /// <summary>A gradient triangle wound to face away from <paramref name="inside"/>.</summary>
        static void FacetOut(MeshBuilder mb, Vector3 a, Vector3 b, Vector3 c, Vector3 inside, Color ca, Color cb, Color cc)
        {
            if (Vector3.Dot(Vector3.Cross(b - a, c - a), (a + b + c) / 3f - inside) < 0f) mb.TriC(a, c, b, ca, cc, cb);
            else mb.TriC(a, b, c, ca, cb, cc);
        }

        static float H01(float x) => Mathf.Repeat(Mathf.Sin(x * 12.9898f) * 43758.5453f, 1f);

        /// <summary>Vertices in the flames (about 120): the budget the flicker shader and the phone's vertex stage see per fire.</summary>
        public const int FlameVertexBudget = 130;

        /// <summary>
        /// Three nested layers with a colour ramp up each (no lemon: green never above 0.65 of red): a gold core (0.10 tall),
        /// an orange body (0.24) and five red outer tongues (0.12-0.20), each two overlapping cones, the upper one leaning
        /// further so the tip curls. The vertices carry the flame flag and a height weight in UV1 (<see cref="MeshBuilder.flameHeight"/>),
        /// so the Stylized shader stretches and wobbles them: the flicker costs nothing on the CPU. Emission is low
        /// (0.3 tips, 0.4 body, 0.5 core): only the core gets near the bloom threshold, the rest stays orange.
        /// </summary>
        public static void Flames(MeshBuilder mb, Vector3 p, float phase)
        {
            float y0 = 0.012f;
            mb.flameBaseY = mb.M.MultiplyPoint3x4(p + Vector3.up * y0).y; mb.flameHeight = 0.3f; mb.flamePhase = phase; mb.sway = 0f;
            // Outer tongues, behind the body (random yaw and lean from the fire's phase, so a rebuild never changes them).
            mb.emission = 0.3f;
            float yaw0 = H01(phase * 91f + 1f) * 72f;
            for (int i = 0; i < 5; i++)
            {
                float a = yaw0 + i * 72f + (H01(phase * 57f + i * 3.1f) - 0.5f) * 36f;
                float h = Mathf.Lerp(0.12f, 0.2f, H01(phase * 33f + i * 7.7f));
                float lean = Mathf.Lerp(12f, 22f, H01(phase * 71f + i * 5.3f));
                var yaw = Quaternion.Euler(0, a, 0);
                var q1 = yaw * Quaternion.Euler(lean, 0, 0);
                var q2 = yaw * Quaternion.Euler(lean + 16f, 0, 0);
                var b1 = p + yaw * new Vector3(0, y0, 0.042f);
                var mid = Color.Lerp(Palette.FlameTongueA, Palette.FlameTongueB, 0.45f);
                mb.Prism(b1, 0.035f, 0f, h * 0.7f, 3, Palette.FlameTongueA, q1, topCol: mid, ramp: true);
                mb.Prism(b1 + q1 * Vector3.up * (h * 0.32f), 0.02f, 0f, h * 0.68f, 3, mid, q2, topCol: Palette.FlameTongueB, ramp: true);
            }
            mb.emission = 0.4f;
            mb.Prism(p + Vector3.up * (y0 + 0.025f), 0.06f, 0f, 0.215f, 5, Palette.FlameBodyA, Quaternion.Euler(0, 20f, 0), topCol: Palette.FlameBodyB, ramp: true);
            mb.emission = 0.4f; // spec 0.5; 0.4 keeps the gold core from tonemapping to pale lemon (tester #63)
            mb.Prism(p + new Vector3(0, y0, 0), 0.07f, 0f, 0.10f, 5, Palette.FlameCoreA, Quaternion.Euler(0, 50f, 0), topCol: Palette.FlameCoreB, ramp: true);
            mb.emission = 0f; mb.flameHeight = 0f;
        }

        public static void Log(MeshBuilder mb, Vector3 centre, float r, float len, float yaw, Color col)
        {
            var rot = Quaternion.Euler(0, yaw, 0) * Quaternion.Euler(90, 0, 0);
            mb.Prism(centre - rot * Vector3.up * (len / 2f), r, r, len, 6, col, rot, true, true, Palette.WoodLight);
        }

        public static void Woodpile(MeshBuilder mb, Vector3 p, float yaw)
        {
            var r = Quaternion.Euler(0, yaw, 0);
            for (int row = 0; row < 3; row++)
                for (int i = 0; i < 4 - row; i++)
                {
                    var c = p + r * new Vector3((i - (3 - row) / 2f) * 0.055f, 0.028f + row * 0.048f, 0);
                    Log(mb, c, 0.026f, 0.26f, yaw, i % 2 == 0 ? Palette.Bark : Palette.WoodLight);
                }
        }

        public static void DryingRack(MeshBuilder mb, Vector3 p)
        {
            foreach (int s in new[] { -1, 1 }) mb.Box(p + new Vector3(s * 0.15f, 0.14f, 0), new Vector3(0.02f, 0.28f, 0.02f), Palette.Bark);
            mb.Box(p + new Vector3(0, 0.27f, 0), new Vector3(0.34f, 0.018f, 0.018f), Palette.Bark);
            mb.sway = 0.25f;
            mb.Box(p + new Vector3(-0.06f, 0.19f, 0), new Vector3(0.1f, 0.15f, 0.008f), Palette.Hide);
            mb.Box(p + new Vector3(0.07f, 0.2f, 0), new Vector3(0.08f, 0.12f, 0.008f), Palette.Canvas);
            mb.sway = 0f;
        }

        /// <summary>A hide lean-to, open side facing local -z.</summary>
        public static void LeanTo(MeshBuilder mb)
        {
            mb.Box(new Vector3(0, 0.2f, 0.05f), new Vector3(0.55f, 0.02f, 0.46f), Palette.Hide, Quaternion.Euler(-48f, 0, 0), true);
            foreach (int s in new[] { -1, 1 }) mb.Box(new Vector3(s * 0.25f, 0.17f, -0.11f), new Vector3(0.025f, 0.34f, 0.025f), Palette.Bark);
            mb.Box(new Vector3(0, 0.34f, -0.11f), new Vector3(0.6f, 0.025f, 0.025f), Palette.Bark);
            mb.Box(new Vector3(0, 0.012f, 0.08f), new Vector3(0.4f, 0.02f, 0.26f), Palette.Canvas);
        }

        public static void Bush(MeshBuilder mb, Vector3 g, float r, bool berries)
        {
            mb.sway = 0.3f;
            mb.Ico(g + Vector3.up * r * 0.5f, r, new Vector3(1.2f, 0.8f, 1.1f), Palette.LeafB, 0.25f);
            mb.sway = 0f;
            if (berries)
                for (int k = 0; k < 4; k++)
                    mb.Ico(g + new Vector3(mb.Range(-r, r) * 0.7f, r * mb.Range(0.6f, 1.1f), mb.Range(-r, r) * 0.7f), 0.016f, Vector3.one, Palette.Ember, 0.1f);
        }

        // ---------------- village ----------------
        public static void Well(MeshBuilder mb, Vector3 p, bool roofed)
        {
            mb.Prism(p, 0.13f, 0.13f, 0.12f, 9, Palette.Stone, topCol: Palette.RockDark);
            mb.Prism(p + Vector3.up * 0.121f, 0.09f, 0.09f, 0.001f, 9, Palette.WaterDeep);
            if (!roofed) return;
            foreach (int s in new[] { -1, 1 }) mb.Box(p + new Vector3(s * 0.11f, 0.2f, 0), new Vector3(0.025f, 0.28f, 0.025f), Palette.Timber);
            mb.Box(p + new Vector3(0, 0.32f, 0), new Vector3(0.26f, 0.02f, 0.02f), Palette.Timber);
            mb.Prism(p + new Vector3(0.0f, 0.2f, 0), 0.02f, 0.02f, 0.05f, 6, Palette.WoodLight); // bucket
            foreach (int s in new[] { -1, 1 })
                mb.Box(p + new Vector3(0, 0.37f, s * 0.07f), new Vector3(0.3f, 0.02f, 0.17f), Palette.Thatch, Quaternion.Euler(s * 35f, 0, 0), true);
        }

        public static void Bench(MeshBuilder mb, Vector3 p)
        {
            mb.Box(p + new Vector3(0, 0.05f, 0), new Vector3(0.24f, 0.018f, 0.06f), Palette.WoodLight);
            foreach (int s in new[] { -1, 1 }) mb.Box(p + new Vector3(s * 0.09f, 0.022f, 0), new Vector3(0.02f, 0.045f, 0.05f), Palette.Timber);
        }

        /// <summary>A lantern on a post: dark iron cage round a lamp that lights at night (flickering on, NightLamps).</summary>
        public static void Lantern(MeshBuilder mb, Vector3 p, float lampPhase)
        {
            mb.Box(p + new Vector3(0, 0.16f, 0), new Vector3(0.025f, 0.32f, 0.025f), Palette.Timber);
            mb.Box(p + new Vector3(0.035f, 0.31f, 0), new Vector3(0.08f, 0.015f, 0.015f), Palette.Timber);
            var lp = p + new Vector3(0.065f, 0.255f, 0);
            mb.Box(lp + new Vector3(0, 0.05f, 0), new Vector3(0.05f, 0.012f, 0.05f), Palette.Iron);
            mb.emission = 0.15f;
            mb.lamp = lampPhase;
            mb.Box(lp + new Vector3(0, 0.02f, 0), new Vector3(0.036f, 0.048f, 0.036f), Palette.Lamp);
            mb.lamp = -1f;
            mb.emission = 0f;
            mb.Box(lp + new Vector3(0, -0.008f, 0), new Vector3(0.044f, 0.01f, 0.044f), Palette.Iron);
        }

        /// <summary>A fence along a polyline in lot-local (or world) space, posts every ~0.2 u.</summary>
        public static void Fence(MeshBuilder mb, Ground ground, Vector3[] pts)
        {
            for (int i = 0; i + 1 < pts.Length; i++)
            {
                var a = pts[i]; var b = pts[i + 1];
                int n = Mathf.Max(1, Mathf.RoundToInt(Vector3.Distance(a, b) / 0.2f));
                for (int k = 0; k <= n; k++)
                {
                    var p = Vector3.Lerp(a, b, k / (float)n);
                    var gp = ground(p);
                    mb.Box(gp + Vector3.up * 0.07f, new Vector3(0.025f, 0.15f, 0.025f), Palette.Timber, Quaternion.Euler(0, mb.Range(-8f, 8f), mb.Range(-4f, 4f)));
                    if (k == n) continue;
                    var gq = ground(Vector3.Lerp(a, b, (k + 1) / (float)n));
                    var dir = gq - gp;
                    if (dir.sqrMagnitude < 1e-6f) continue;
                    var rot = Quaternion.LookRotation(dir.normalized, Vector3.up);
                    foreach (float y in new[] { 0.06f, 0.11f })
                        mb.Box((gp + gq) / 2f + Vector3.up * y, new Vector3(0.014f, 0.016f, dir.magnitude), Palette.WoodLight, rot);
                }
            }
        }

        /// <summary>One crop plant at a ground point (wheat ear or green shoot), swaying in the wind.</summary>
        public static void Crop(MeshBuilder mb, Vector3 p, Color col)
        {
            mb.sway = 0.7f;
            mb.Prism(p, 0.022f, 0f, mb.Range(0.09f, 0.13f), 4, col, Quaternion.Euler(0, mb.Range(0, 90f), 0));
            mb.sway = 0f;
        }

        public static void Granary(MeshBuilder mb, Vector3 at)
        {
            foreach (var (x, z) in new[] { (-1, -1), (1, -1), (1, 1), (-1, 1) })
                mb.Box(at + new Vector3(x * 0.09f, 0.06f, z * 0.09f), new Vector3(0.03f, 0.12f, 0.03f), Palette.Bark);
            mb.Box(at + new Vector3(0, 0.17f, 0), new Vector3(0.24f, 0.1f, 0.24f), Palette.WoodLight);
            mb.Prism(at + new Vector3(0, 0.22f, 0), 0.2f, 0f, 0.16f, 6, Palette.Thatch);
        }

        public static void Crates(MeshBuilder mb, Vector3 p)
        {
            mb.Box(p + new Vector3(0, 0.035f, 0), new Vector3(0.07f, 0.07f, 0.07f), Palette.WoodLight, Quaternion.Euler(0, 15, 0));
            mb.Box(p + new Vector3(0.08f, 0.03f, 0.02f), new Vector3(0.06f, 0.06f, 0.06f), Palette.Hide, Quaternion.Euler(0, -10, 0));
            mb.Prism(p + new Vector3(0.03f, 0.0f, 0.08f), 0.035f, 0.03f, 0.06f, 7, Palette.Bark, topCol: Palette.Wheat);
        }

        public static void Stall(MeshBuilder mb, Vector3 p, float yaw, Color canopy)
        {
            var keep = mb.M;
            mb.M = keep * Matrix4x4.TRS(p, Quaternion.Euler(0, yaw, 0), Vector3.one);
            mb.Box(new Vector3(0, 0.07f, 0), new Vector3(0.2f, 0.03f, 0.11f), Palette.WoodLight);
            foreach (int s in new[] { -1, 1 }) mb.Box(new Vector3(s * 0.09f, 0.1f, 0.04f), new Vector3(0.016f, 0.2f, 0.016f), Palette.Timber);
            mb.Box(new Vector3(0, 0.2f, 0.0f), new Vector3(0.24f, 0.012f, 0.16f), canopy, Quaternion.Euler(-14, 0, 0), true);
            mb.Ico(new Vector3(-0.04f, 0.1f, -0.01f), 0.022f, Vector3.one, Palette.Flowers[1], 0.1f);
            mb.Ico(new Vector3(0.04f, 0.1f, 0.0f), 0.022f, Vector3.one, Palette.Ember, 0.1f);
            mb.M = keep;
        }

        /// <summary>An open-sided shed: posts and a pitched roof (lumber mill, market hall, dyer's shed).</summary>
        public static void Shed(MeshBuilder mb, Vector3 at, float w, float d, float h, Color roof, Color post)
        {
            foreach (var (x, z) in new[] { (-1, -1), (1, -1), (1, 1), (-1, 1), (0, -1), (0, 1) })
                mb.Box(at + new Vector3(x * (w / 2f - 0.02f), h / 2f, z * (d / 2f - 0.02f)), new Vector3(0.03f, h, 0.03f), post);
            float half = d / 2f + 0.05f, rh = 0.12f;
            float slope = Mathf.Sqrt(half * half + rh * rh), ang = Mathf.Atan2(rh, half) * Mathf.Rad2Deg;
            for (int s = -1; s <= 1; s += 2)
                mb.Box(at + new Vector3(0, h + rh / 2f, s * half / 2f), new Vector3(w + 0.1f, 0.03f, slope), roof, Quaternion.Euler(s * ang, 0, 0), true);
        }

        public static void Windmill(MeshBuilder mb, Vector3 at, float yaw, Color body, bool stone, float lampPhase)
        {
            float h = 0.56f;
            mb.Prism(at, 0.13f, 0.085f, h, 8, body, topCol: body);
            mb.Prism(at + Vector3.up * h, 0.105f, 0.0f, 0.14f, 8, stone ? Palette.Slate : Palette.Thatch);
            mb.Box(at + new Vector3(0, 0.08f, -0.125f), new Vector3(0.07f, 0.13f, 0.02f), Palette.Door);
            mb.emission = 0.3f; mb.lamp = lampPhase;
            mb.Box(at + new Vector3(0, 0.34f, -0.1f), new Vector3(0.045f, 0.05f, 0.02f), Palette.WindowLit);
            mb.emission = 0f; mb.lamp = -1f;
            var hub = at + new Vector3(0, h - 0.02f, -0.13f);
            mb.Box(hub, new Vector3(0.04f, 0.04f, 0.05f), Palette.Timber);
            for (int k = 0; k < 4; k++)
            {
                var r = Quaternion.Euler(0, 0, 45f + k * 90f + yaw * 0.13f);
                mb.Box(hub + r * new Vector3(0, 0.2f, 0) - new Vector3(0, 0, 0.02f), new Vector3(0.016f, 0.4f, 0.01f), Palette.Timber, r);
                mb.Box(hub + r * new Vector3(0.035f, 0.23f, 0) - new Vector3(0, 0, 0.024f), new Vector3(0.06f, 0.3f, 0.005f), Palette.Canvas, r);
            }
        }

        public static void MarketHall(MeshBuilder mb, Vector3 at, float w, float d, bool town, float lampPhase)
        {
            if (town)
            {
                mb.Box(at + new Vector3(0, 0.02f, 0), new Vector3(w, 0.04f, d), Palette.Stone);
                foreach (var (x, z) in new[] { (-1, -1), (1, -1), (1, 1), (-1, 1), (0, -1), (0, 1) })
                    mb.Box(at + new Vector3(x * (w / 2f - 0.03f), 0.12f, z * (d / 2f - 0.03f)), new Vector3(0.05f, 0.2f, 0.05f), Palette.Stone);
                House(mb, at + new Vector3(0, 0.12f, 0), w - 0.02f, d - 0.02f, 0.18f, 0.2f, Palette.PlasterWarm, Palette.Terracotta, false, true, 1, lampPhase);
            }
            else Shed(mb, at, w - 0.06f, d - 0.06f, 0.22f, Palette.Terracotta, Palette.Timber);
            Crates(mb, at + new Vector3(-w * 0.2f, 0, -d * 0.1f));
        }

        public static void Church(MeshBuilder mb, Vector3 at, float w, float d, float lampPhase)
        {
            float nw = w * 0.8f, nd = d * 0.72f;
            var nave = at + new Vector3(0, 0, d * 0.12f);
            mb.Box(nave + new Vector3(0, 0.03f, 0), new Vector3(nw + 0.04f, 0.06f, nd + 0.04f), Palette.RockDark);
            mb.Box(nave + new Vector3(0, 0.06f + 0.16f, 0), new Vector3(nw, 0.32f, nd), Palette.Stone);
            float rh = 0.2f, half = nw / 2f + 0.05f;
            float slope = Mathf.Sqrt(half * half + rh * rh), ang = Mathf.Atan2(rh, half) * Mathf.Rad2Deg;
            for (int s = -1; s <= 1; s += 2)
                mb.Box(nave + new Vector3(s * half / 2f, 0.38f + rh / 2f, 0), new Vector3(slope, 0.032f, nd + 0.08f), Palette.Slate, Quaternion.Euler(0, 0, -s * ang), true);
            foreach (int s in new[] { -1, 1 })
            {
                float z = nave.z + s * nd / 2f;
                var a = new Vector3(nave.x - nw / 2f, at.y + 0.38f, z); var b = new Vector3(nave.x, at.y + 0.38f + rh * (nw / 2f) / half, z); var c = new Vector3(nave.x + nw / 2f, at.y + 0.38f, z);
                if (s < 0) mb.Tri(a, b, c, Palette.Stone); else mb.Tri(c, b, a, Palette.Stone);
            }
            mb.emission = 0.3f; mb.lamp = lampPhase;
            for (int k = -1; k <= 1; k++)
                foreach (int s in new[] { -1, 1 })
                    mb.Box(nave + new Vector3(s * (nw / 2f + 0.006f), 0.26f, k * nd * 0.28f), new Vector3(0.012f, 0.1f, 0.045f), Palette.WindowLit);
            mb.emission = 0f; mb.lamp = -1f;
            var tower = at + new Vector3(0, 0, -d / 2f + 0.12f);
            mb.Box(tower + new Vector3(0, 0.33f, 0), new Vector3(0.2f, 0.66f, 0.2f), Palette.Stone);
            mb.Prism(tower + new Vector3(0, 0.66f, 0), 0.15f, 0f, 0.3f, 4, Palette.Slate, Quaternion.Euler(0, 45, 0));
            mb.Box(tower + new Vector3(0, 0.1f, -0.106f), new Vector3(0.08f, 0.16f, 0.012f), Palette.Door);
            mb.Box(tower + new Vector3(0, 0.5f, -0.106f), new Vector3(0.05f, 0.08f, 0.012f), Palette.RockDark);
        }

        public static void StandingStones(MeshBuilder mb, Vector3 at)
        {
            for (int i = 0; i < 6; i++)
            {
                float a = i / 6f * Mathf.PI * 2f;
                mb.Box(at + new Vector3(Mathf.Cos(a) * 0.17f, 0.08f, Mathf.Sin(a) * 0.17f), new Vector3(0.05f, mb.Range(0.13f, 0.2f), 0.035f), i % 2 == 0 ? Palette.Rock : Palette.RockLight, Quaternion.Euler(mb.Range(-6, 6), -a * Mathf.Rad2Deg, 0));
            }
        }

        public static void Watermill(MeshBuilder mb, Vector3 at, float w, float d, float lampPhase)
        {
            House(mb, at + new Vector3(0, 0, -0.03f), w * 0.78f, d * 0.6f, 0.28f, 0.2f, Palette.Stone, Palette.Slate, true, false, 1, lampPhase);
            var axle = at + new Vector3(w * 0.12f, 0.12f, d / 2f + 0.03f);
            var rot = Quaternion.Euler(0, 0, 90);
            mb.Prism(axle - new Vector3(0.04f, 0, 0), 0.17f, 0.17f, 0.08f, 10, Palette.Timber, rot, true, true, Palette.WoodLight);
            for (int k = 0; k < 8; k++)
            {
                var r = Quaternion.Euler(k * 45f, 0, 0);
                mb.Box(axle + r * new Vector3(0, 0.17f, 0), new Vector3(0.1f, 0.012f, 0.05f), Palette.WoodLight, r);
            }
        }

        public static void Smithy(MeshBuilder mb, Vector3 at, float w, float d, float lampPhase)
        {
            House(mb, at + new Vector3(0, 0, 0.04f), w * 0.8f, d * 0.55f, 0.24f, 0.18f, Palette.Stone, Palette.Slate, false, false, 1, lampPhase);
            var ch = at + new Vector3(w * 0.28f, 0, 0.1f);
            mb.Box(ch + new Vector3(0, 0.3f, 0), new Vector3(0.09f, 0.6f, 0.09f), Palette.RockDark);
            mb.emission = 1f;
            mb.Box(ch + new Vector3(0, 0.08f, -0.05f), new Vector3(0.06f, 0.05f, 0.012f), Palette.Fire);
            mb.emission = 0f;
            var f = at + new Vector3(-w * 0.15f, 0, -d * 0.36f);
            mb.Box(f + new Vector3(0, 0.03f, 0), new Vector3(0.05f, 0.06f, 0.05f), Palette.Bark);
            mb.Box(f + new Vector3(0, 0.07f, 0), new Vector3(0.09f, 0.025f, 0.04f), Palette.RockDark);
            mb.Prism(f + new Vector3(0.12f, 0, 0.0f), 0.035f, 0.032f, 0.07f, 8, Palette.Bark, topCol: Palette.WaterDeep);
        }

        public static void Dyer(MeshBuilder mb, Vector3 at, float w, float d)
        {
            Shed(mb, at + new Vector3(0, 0, d * 0.18f), w * 0.7f, d * 0.45f, 0.2f, Palette.Thatch, Palette.Timber);
            Color[] dyes = { Palette.Tunics[0], Palette.Tunics[1], Palette.Tunics[2] };
            for (int k = 0; k < 3; k++)
                mb.Prism(at + new Vector3(-w * 0.28f + k * 0.11f, 0, -d * 0.22f), 0.045f, 0.045f, 0.06f, 8, Palette.Bark, topCol: dyes[k]);
            var p = at + new Vector3(w * 0.18f, 0, -d * 0.18f);
            foreach (int s in new[] { -1, 1 }) mb.Box(p + new Vector3(s * 0.12f, 0.11f, 0), new Vector3(0.016f, 0.22f, 0.016f), Palette.Timber);
            mb.sway = 0.35f;
            for (int k = 0; k < 3; k++)
                mb.Box(p + new Vector3(-0.07f + k * 0.07f, 0.15f, 0), new Vector3(0.055f, 0.12f, 0.006f), dyes[(k + 1) % 3]);
            mb.sway = 0f;
        }

        public static void Lumbermill(MeshBuilder mb, Vector3 at, float w, float d)
        {
            Shed(mb, at + new Vector3(0, 0, d * 0.12f), w * 0.62f, d * 0.5f, 0.2f, Palette.WoodLight, Palette.Timber);
            Woodpile(mb, at + new Vector3(-w * 0.2f, 0, -d * 0.25f), 90f);
            Log(mb, at + new Vector3(w * 0.18f, 0.03f, -d * 0.25f), 0.03f, 0.3f, 10f, Palette.Bark);
            mb.Prism(at + new Vector3(w * 0.3f, 0, d * 0.32f), 0.045f, 0.045f, 0.05f, 7, Palette.Bark, topCol: Palette.WoodLight);
        }

        // ---------------- nature ----------------
        public static void Tree(MeshBuilder mb, Vector3 g, float s, bool pine)
        {
            g -= Vector3.up * 0.02f;
            mb.Prism(g, 0.045f * s, 0.032f * s, 0.3f * s, 6, Palette.Bark);
            mb.sway = 1f;
            if (pine)
            {
                for (int i = 0; i < 3; i++)
                    mb.Prism(g + Vector3.up * (0.2f + i * 0.17f) * s, (0.24f - i * 0.06f) * s, 0f, 0.3f * s, 7, Palette.Pine, Quaternion.Euler(0, i * 20f, 0), false, true);
            }
            else
            {
                var col = mb.Range(0, 3) switch { 0 => Palette.LeafA, 1 => Palette.LeafB, _ => Palette.LeafC };
                mb.Ico(g + Vector3.up * 0.42f * s, 0.2f * s, new Vector3(1.1f, 0.95f, 1.1f), col, 0.18f, Palette.LeafB);
                mb.Ico(g + new Vector3(0.1f, 0.55f, 0.04f) * s, 0.14f * s, Vector3.one, col, 0.18f);
                mb.Ico(g + new Vector3(-0.09f, 0.5f, -0.06f) * s, 0.13f * s, Vector3.one, Color.Lerp(col, Palette.LeafC, 0.4f), 0.18f);
            }
            mb.sway = 0f;
        }

        public static void Rock(MeshBuilder mb, Vector3 g, float r, bool light)
        {
            mb.Ico(g + Vector3.up * r * 0.2f, r, new Vector3(1.2f, 0.6f, 1f), light ? Palette.RockLight : Palette.Rock, 0.22f, Palette.RockDark);
        }

        public static void GrassTuft(MeshBuilder mb, Vector3 g, float h, Color col)
        {
            mb.sway = 1f;
            for (int b = 0; b < 3; b++)
            {
                float a = b * 2.1f + mb.Range(0f, 1f);
                var lean = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * h * 0.35f;
                var side = new Vector3(-Mathf.Sin(a), 0, Mathf.Cos(a)) * 0.012f;
                mb.Tri(g - side, g + lean + Vector3.up * h, g + side, col);
                mb.Tri(g + side, g + lean + Vector3.up * h, g - side, col);
            }
            mb.sway = 0f;
        }

        public static void Reed(MeshBuilder mb, Vector3 g, Color col)
        {
            mb.sway = 0.8f;
            mb.Prism(g, 0.012f, 0f, mb.Range(0.14f, 0.24f), 3, col, Quaternion.Euler(mb.Range(-8f, 8f), 0, mb.Range(-8f, 8f)));
            mb.sway = 0f;
        }
    }
}
