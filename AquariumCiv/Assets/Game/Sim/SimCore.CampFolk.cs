using AquariumCiv.Sim.Eco;

namespace AquariumCiv.Sim
{
    /// <summary>#692 part 5: the few things the camp's people (not the Keeper) change in the land and the stock. B53.a: a grown-up pulls a
    /// creeping seedling out of the camp (a tent's door, the fire ring, a path); it is not a Keeper action (no prune counted, no history line).
    /// B42.d: a kid sneaks one fish off the rack [est: the rack's count drops by one].</summary>
    public partial class SimCore
    {
        /// <summary>B53.a: a wild seedling on <paramref name="cell"/> is pulled out by the people (never the Keeper's tree, never a young tree or a
        /// grown one). False when there is none there.</summary>
        public bool EcoPeopleWeed(int cell)
        {
            if (!EcologyOn || cell < 0 || cell >= EcoLand.N) return false;
            if ((EcoCover)State.eco.cover[cell] != EcoCover.Seedling || !EcoM.CanPluckSapling(cell)) return false;
            return EcoM.PluckSapling(cell);
        }

        /// <summary>Debug (B53.a's cheat): a wild seedling creeps onto <paramref name="cell"/> (bare ground or grass only, never the Keeper's tree).</summary>
        public bool DebugSeedlingAt(int cell)
        {
            if (!EcologyOn || cell < 0 || cell >= EcoLand.N || cell == State.eco.treeCell) return false;
            var cv = (EcoCover)State.eco.cover[cell];
            if (cv != EcoCover.Bare && cv != EcoCover.Grass && cv != EcoCover.Tall) return false;
            State.eco.cover[cell] = (byte)EcoCover.Seedling;
            return true;
        }

        /// <summary>Debug (B47's cheats): reeds grow on up to <paramref name="n"/> dry cells at the water's edge nearest (<paramref name="x"/>,
        /// <paramref name="z"/>) (bare, grass or tall grass only). How many.</summary>
        public int DebugReedsNear(float x, float z, int n)
        {
            if (!EcologyOn) return 0;
            var e = State.eco; int got = 0;
            for (int k = 0; k < n; k++)
            {
                int best = -1; float bd = float.MaxValue;
                for (int c = 0; c < EcoLand.N; c++)
                {
                    var cv = (EcoCover)e.cover[c];
                    if (cv != EcoCover.Bare && cv != EcoCover.Grass && cv != EcoCover.Tall || c == e.treeCell) continue;
                    int i = Settle.SiteMap.I(c), j = Settle.SiteMap.J(c); bool wet = false;
                    for (int dj = -1; dj <= 1 && !wet; dj++) for (int di = -1; di <= 1; di++)
                    {
                        int a = i + di, b = j + dj;
                        if (a < 0 || b < 0 || a >= Settle.SiteMap.NX || b >= Settle.SiteMap.NZ) continue;
                        if ((EcoCover)e.cover[Settle.SiteMap.Index(a, b)] == EcoCover.Water) { wet = true; break; }
                    }
                    if (!wet) continue;
                    float dx = Settle.SiteMap.CXc(c) - x, dz = Settle.SiteMap.CZc(c) - z, d = dx * dx + dz * dz;
                    if (d < bd) { bd = d; best = c; }
                }
                if (best < 0) break;
                e.cover[best] = (byte)EcoCover.Reeds; got++;
            }
            if (got > 0) { EcoM.Count(); EcoTell(); }
            return got;
        }

        /// <summary>B42.d: a kid takes one fish off the rack (a dry one first). False when the rack is empty.</summary>
        public bool EcoKidTakesRackFish()
        {
            if (!EcologyOn) return false;
            var e = State.eco;
            if (e.fishRackDry > 0) { e.fishRackDry--; return true; }
            if (e.fishRackFresh > 0) { e.fishRackFresh--; return true; }
            return false;
        }
    }
}
