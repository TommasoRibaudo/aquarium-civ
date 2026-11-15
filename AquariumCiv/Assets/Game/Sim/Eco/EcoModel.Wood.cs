using System;
using AquariumCiv.Sim.Settle;

namespace AquariumCiv.Sim.Eco
{
    /// <summary>
    /// #690 (playtest, feedback#80): the deer come to a wood, not to the first few trees. A grown tree is "in the wood" when at least
    /// <see cref="EcoTuning.jarWoodCompany"/> other grown trees stand within <see cref="EcoTuning.jarWoodReach"/> cells of it (a square
    /// window, the same one in every direction). The deer's jar opens at <see cref="EcoTuning.jarDeerWood"/> such trees, so a handful
    /// of trees scattered over the box never count, however many there are, while a clump does. Read from the cover each time (no cache:
    /// the cover is the truth, and the scan only visits grown trees).
    /// </summary>
    public sealed partial class EcoModel
    {
        /// <summary>How many grown trees stand in company of at least <see cref="EcoTuning.jarWoodCompany"/> others (see the class note).</summary>
        public int WoodTrees()
        {
            int reach = Math.Max(1, t.jarWoodReach), need = t.jarWoodCompany, wood = 0;
            for (int c = 0; c < EcoLand.N; c++)
            {
                if (Cv(c) != EcoCover.Tree) continue;
                int i = SiteMap.I(c), j = SiteMap.J(c), near = 0;
                for (int b = Math.Max(0, j - reach); b <= Math.Min(EcoLand.NZ - 1, j + reach) && near < need; b++)
                    for (int a = Math.Max(0, i - reach); a <= Math.Min(EcoLand.NX - 1, i + reach); a++)
                        if ((a != i || b != j) && Cv(SiteMap.Index(a, b)) == EcoCover.Tree && ++near >= need) break;
                if (near >= need) wood++;
            }
            return wood;
        }
    }
}
