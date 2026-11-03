using System;
using AquariumCiv.Sim.Settle;

namespace EcologyLab
{
    public enum Strategy { A_Nothing, B_Casual, C_Care, D_OverMist }

    /// <summary>
    /// A scripted Keeper on a real-day clock: 1 real hour = 1 game year, so a season is 15 real minutes. The run starts
    /// at 08:00; the player is awake 08:00–24:00 and asleep 00:00–08:00 (the nights are the absences). (c) and (d)
    /// check in every waking hour, (b) every 4 waking hours, (a) never. The first session is half an hour (2 seasons).
    /// </summary>
    public sealed class Player
    {
        public readonly Strategy strategy;
        int spotsMisted;

        public Player(Strategy s) { strategy = s; }

        public static string Letter(Strategy s) => s switch { Strategy.A_Nothing => "a", Strategy.B_Casual => "b", Strategy.C_Care => "c", _ => "d" };
        public static string Name(Strategy s) => s switch
        {
            Strategy.A_Nothing => "does nothing", Strategy.B_Casual => "mists now and then",
            Strategy.C_Care => "full care", _ => "over-mists",
        };

        /// <summary>The hour of the real day at the start of this season's year (the run starts at 08:00).</summary>
        public static int HourOfDay(int season) => (8 + season / 4) % 24;
        public static bool Awake(int season) => HourOfDay(season) >= 8;

        public bool VisitsAt(int season)
        {
            if (strategy == Strategy.A_Nothing) return false;
            if (season <= 1) return true; // the first session
            if (season % 4 != 0 || !Awake(season)) return false;
            int hour = season / 4;
            return strategy == Strategy.B_Casual ? hour % 4 == 0 : true;
        }

        /// <summary>Fills <paramref name="v"/> for this season; false when the player isn't there.</summary>
        public bool Plan(Eco e, Visit v)
        {
            v.Clear();
            if (!VisitsAt(e.season)) return false;
            var land = e.land;
            if (!e.tree.Planted && e.SeedAvailable) v.plant = PlantSpot(e);
            float amount = strategy == Strategy.D_OverMist ? 1.5f : 1f;
            int spots = strategy switch { Strategy.B_Casual => 2, Strategy.C_Care => 3, _ => 6 };
            // The tree first, when there is one (b mists it too: water is the core need).
            var tc = e.tree.cell >= 0 ? e.tree.cell : v.plant.HasValue ? Land.CellAt(v.plant.Value.x, v.plant.Value.z) : -1;
            if (tc >= 0 && e.tree.stage < 4) { v.mists.Add((Land.CXc(tc), Land.CZc(tc), amount)); spots--; }
            for (int k = 0; k < spots; k++) { var p = MistSpot(e, k); v.mists.Add((p.x, p.z, amount)); spotsMisted++; }
            if (strategy == Strategy.C_Care || strategy == Strategy.D_OverMist) { v.wipe = true; v.snip = true; }
            return true;
        }

        /// <summary>"Here and there": the driest of a few candidate spots, starting round the camp and spreading out as
        /// the player mists more (they green the barren parts).</summary>
        (float x, float z) MistSpot(Eco e, int k)
        {
            var land = e.land;
            float reach = MathF.Min(2.8f, 0.7f + spotsMisted * 0.08f);
            float bestScore = float.MaxValue, bx = land.campX, bz = land.campZ;
            for (int tries = 0; tries < 10; tries++)
            {
                float a = Terrain.Hash3(e.seed * 31 + 101, e.season * 16 + k, tries) * MathF.PI * 2f;
                float r = MathF.Sqrt(Terrain.Hash3(e.seed * 31 + 102, e.season * 16 + k, tries)) * reach;
                float x = land.campX + MathF.Cos(a) * r, z = land.campZ + MathF.Sin(a) * r;
                if (MathF.Abs(x) > Terrain.HX - 0.2f || MathF.Abs(z) > Terrain.HZ - 0.2f) continue;
                int c = Land.CellAt(x, z);
                if (c < 0 || e.cover[c] == Cover.Water) continue;
                float score = e.moist[c] + (e.cover[c] == Cover.Bare ? 0f : 0.3f);
                if (score < bestScore) { bestScore = score; bx = x; bz = z; }
            }
            return (bx, bz);
        }

        /// <summary>The seed goes on open ground near the camp, out of the yard and above the flood line: the moistest of
        /// a ring of candidates 0.8–1.0 u out, toward the middle of the box.</summary>
        static (float x, float z) PlantSpot(Eco e)
        {
            var land = e.land;
            float best = float.MinValue, bx = land.campX, bz = land.campZ;
            for (int k = 0; k < 24; k++)
            {
                float a = k / 24f * MathF.PI * 2f, r = 0.8f + 0.2f * (k % 2);
                float x = land.campX + MathF.Cos(a) * r, z = land.campZ + MathF.Sin(a) * r;
                int c = Land.CellAt(x, z);
                if (c < 0 || land.InBasin(c) || land.yard[c] || land.slope[c] > 0.3f) continue;
                float score = e.moist[c] - 0.1f * Terrain.Dist(x, z, 0f, 0f);
                if (score > best) { best = score; bx = Land.CXc(c); bz = Land.CZc(c); }
            }
            return (bx, bz);
        }
    }
}
