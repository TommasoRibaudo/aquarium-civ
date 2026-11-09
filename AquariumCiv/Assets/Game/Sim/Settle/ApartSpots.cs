using System;

namespace AquariumCiv.Sim.Settle
{
    /// <summary>
    /// Where the camp of a group that lives apart (#231) may stand: pure, on the site map. Moved here from Life/ApartLife (#404) so the sim
    /// places the gift tower by the camp the view draws (a shore family's camp keeps to the shore). <c>ApartLife</c> forwards to it.
    /// </summary>
    public static class ApartSpots
    {
        /// <summary>Box units: the camp's reach (fire, shelter, a rack or a boat, and the people round them) that must stay clear.</summary>
        public const float Footprint = 0.3f;
        /// <summary>Box units: a nudged camp keeps this far from the glass, so it isn't lost behind the side panes and the frame.</summary>
        public const float GlassMargin = 0.75f;
        /// <summary>Box units: a nudged camp looks first this far or more from the settlement's green (apart, not its outskirts).</summary>
        public const float AwayFromGreen = 1.4f;

        /// <summary>A cell the camp may cover: dry, no lot or yard ring, no path (a trail or more, or laid), not the green, commons or yard,
        /// not a rock, and not hard against the glass.</summary>
        public static bool CellFree(SiteMap map, int c)
        {
            if (c < 0) return false;
            int i = SiteMap.I(c), j = SiteMap.J(c);
            if (i < 2 || j < 2 || i >= SiteMap.NX - 2 || j >= SiteMap.NZ - 2) return false;
            if (map.Water(c) || map.Reserved(c)) return false; // #418: never on the thicket
            if (map.lot != null && map.lot[c] != 0) return false;
            if (map.ring != null && map.ring[c] != 0) return false;
            if (map.wear != null && map.wear[c] >= PathNet.TTrail) return false;
            const byte block = SiteMap.FLaid | SiteMap.FGreen | SiteMap.FCommons | SiteMap.FYard | SiteMap.FRock;
            return (map.flags[c] & block) == 0;
        }

        /// <summary>Box units: the camp's own clearing: no tree within this of the fire (a camp under the canopy can't be seen).</summary>
        public const float Clearing = 0.4f;
        /// <summary>Box units: the clearing reaches this much further towards the front glass, where the Keeper looks from.</summary>
        public const float FrontView = 0.35f;

        /// <summary>A camp can stand at (x, z): every cell within <paramref name="r"/> is free (<see cref="CellFree"/>), no wood within
        /// <see cref="Clearing"/> (a camp under the canopy can't be seen), and not within <see cref="GlassMargin"/> of the glass (seen through
        /// the side panes and the frame, it's lost).</summary>
        public static bool FootprintFree(SiteMap map, float x, float z, float r = Footprint, bool waterOk = false)
        {
            if (Math.Abs(x) > Terrain.HX - GlassMargin || Math.Abs(z) > Terrain.HZ - GlassMargin) return false;
            float reach = Math.Max(r, Clearing + FrontView);
            int n = (int)Math.Ceiling(reach / SiteMap.Cell);
            for (int dj = -n; dj <= n; dj++)
                for (int di = -n; di <= n; di++)
                {
                    float px = x + di * SiteMap.Cell, pz = z + dj * SiteMap.Cell;
                    float d2 = (px - x) * (px - x) + (pz - z) * (pz - z);
                    int c = SiteMap.CellAt(px, pz);
                    if (d2 <= r * r + 1e-6f && !CellFree(map, c) && !(waterOk && c >= 0 && map.pond[c])) return false; // a shore camp's water side is its own
                    if (c < 0 || !map.Forest(c)) continue;
                    if (d2 <= Clearing * Clearing + 1e-6f) return false;
                    // nor between it and the Keeper: the box is looked at from its front (-z), and a tree there hides the camp
                    float fz = pz - (z - FrontView);
                    if ((px - x) * (px - x) + fz * fz <= Clearing * Clearing + 1e-6f) return false;
                }
            return true;
        }

        /// <summary>Where the camp is drawn: its home spot while that is clear, else the nearest clear cell centre (<paramref name="moved"/>
        /// says so; the nearest anywhere in the box). No map (settling off), or nowhere
        /// clear at all: the spot as it is.</summary>
        public static (float x, float z) FreeSpot(SiteMap map, float x, float z, out bool moved, float r = Footprint, bool shore = false)
        {
            moved = false;
            if (map == null) return (x, z);
            // #404: a shore family lives by the water (their boat goes out from there). Their spot sat next to the pond, so its footprint
            // always took a water cell and the camp was nudged inland, away from the water, for good. While the pond has water they keep
            // to a dry spot with water within ShoreReach, the water side counting as clear.
            if (shore && AnyPond(map))
            {
                if (ShoreFree(map, x, z, r)) return (x, z);
                for (int pass = 0; pass < 2; pass++)
                {
                    float best = float.MaxValue; (float x, float z) at = (x, z);
                    for (int c = 0; c < SiteMap.N; c++)
                    {
                        float px = SiteMap.CXc(c), pz = SiteMap.CZc(c);
                        float d = (px - x) * (px - x) + (pz - z) * (pz - z);
                        if (d >= best || !CellFree(map, c)) continue;
                        if (pass == 0 && (px - map.greenX) * (px - map.greenX) + (pz - map.greenZ) * (pz - map.greenZ) < AwayFromGreen * AwayFromGreen) continue;
                        if (!ShoreFree(map, px, pz, r)) continue;
                        best = d; at = (px, pz);
                    }
                    if (best < float.MaxValue) { moved = (at.x - x) * (at.x - x) + (at.z - z) * (at.z - z) > 1e-6f; return at; }
                }
            }
            if (FootprintFree(map, x, z, r)) return (x, z);
            // Apart means apart: first look only where it is well away from the settlement's green, then anywhere.
            for (int pass = 0; pass < 2; pass++)
            {
                float best = float.MaxValue; (float x, float z) at = (x, z);
                for (int c = 0; c < SiteMap.N; c++)
                {
                    float px = SiteMap.CXc(c), pz = SiteMap.CZc(c);
                    float d = (px - x) * (px - x) + (pz - z) * (pz - z);
                    if (d >= best || !CellFree(map, c)) continue;
                    if (pass == 0 && (px - map.greenX) * (px - map.greenX) + (pz - map.greenZ) * (pz - map.greenZ) < AwayFromGreen * AwayFromGreen) continue;
                    if (!FootprintFree(map, px, pz, r)) continue;
                    best = d; at = (px, pz);
                }
                if (best < float.MaxValue) { moved = true; return at; }
            }
            return (x, z);
        }

        /// <summary>#404: how near the water a shore family's camp stands.</summary>
        public const float ShoreReach = 0.35f;

        public static bool AnyPond(SiteMap map) { for (int c = 0; c < SiteMap.N; c++) if (map.pond[c]) return true; return false; }

        /// <summary>A dry spot whose footprint is clear but for water, with the water within <see cref="ShoreReach"/>.</summary>
        public static bool ShoreFree(SiteMap map, float x, float z, float r)
        {
            if (!CellFree(map, SiteMap.CellAt(x, z)) || !FootprintFree(map, x, z, r, true)) return false;
            var w = WaterWay(map, x, z, ShoreReach);
            return w.x != 0f || w.z != 0f;
        }

        /// <summary>The way to the nearest water within <paramref name="reach"/> (a unit vector), or (0, 0) when there is none.</summary>
        public static (float x, float z) WaterWay(SiteMap map, float x, float z, float reach = 0.8f)
        {
            if (map == null) return (0f, 0f);
            int n = (int)Math.Ceiling(reach / SiteMap.Cell);
            float best = float.MaxValue, bx = 0f, bz = 0f;
            for (int dj = -n; dj <= n; dj++)
                for (int di = -n; di <= n; di++)
                {
                    float px = x + di * SiteMap.Cell, pz = z + dj * SiteMap.Cell;
                    int c = SiteMap.CellAt(px, pz);
                    if (c < 0 || !map.pond[c]) continue;
                    float d = di * di + dj * dj;
                    if (d < best) { best = d; bx = px - x; bz = pz - z; }
                }
            if (best == float.MaxValue) return (0f, 0f);
            float l = (float)Math.Sqrt(bx * bx + bz * bz);
            return l < 1e-5f ? (0f, 0f) : (bx / l, bz / l);
        }
    }
}
