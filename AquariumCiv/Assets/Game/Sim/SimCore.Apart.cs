using System;
using System.Collections.Generic;
using System.Linq;
using AquariumCiv.Sim.Settle;

namespace AquariumCiv.Sim
{
    /// <summary>The kinds of camp an apart group keeps (#231). The view dresses each one.</summary>
    public static class ApartKind
    {
        public const string WoodsCamp = "woods_camp";   // a fire and a lean-to in the far woods (camp_travellers' "full packs")
        public const string ShoreHut = "shore_hut";     // a hut on the far shore of the pond (village_hill_family's "a home across the pond")
    }

    /// <summary>
    /// Strangers who live apart in the box (#231): real people (named, traited, ageing, mortal) with a home spot and a tiny camp,
    /// who are not the settlement's: not in <see cref="SimState.notables"/>, the population, the jobs, the roles or the housing.
    /// Saved with the box.
    /// </summary>
    [Serializable]
    public class ApartGroup
    {
        public int id;
        public int dilemmaId;               // the dilemma whose answer put them here
        public string kind = "";            // ApartKind
        public string family = "";
        public float x, z;                  // the home spot, in box coordinates (the same as Building.x/z)
        public int cell = -1;               // the SiteMap cell of the spot (-1 when settling is off)
        public long sinceMonth;
        public List<Notable> members = new List<Notable>();

        public bool AnyAlive => members.Any(n => n.alive);
    }

    public partial class SimCore
    {
        /// <summary>For the view (#231 phase 2): every apart group, with its people (alive or not), its spot and its kind of camp.</summary>
        public IReadOnlyList<ApartGroup> ApartGroups => State.apartGroups;

        /// <summary>The apart group a dilemma's answer made, or null.</summary>
        public ApartGroup ApartOf(Dilemma d) => d == null ? null : State.apartGroups.FirstOrDefault(g => g.dilemmaId == d.id);

        /// <summary>The strangers settle apart: made as for joining (named, a couple), then taken out of the settlement's people and
        /// given a home spot and a camp. Nothing in the economy changes.</summary>
        void StrangersApart(Dilemma d, int count, string kind)
        {
            var s = State;
            var made = MakeStrangers(d, count, out string family);
            foreach (var n in made) s.notables.Remove(n);
            var g = new ApartGroup { id = s.apartGroups.Count + 1, dilemmaId = d.id, kind = kind, family = family, sinceMonth = s.month, members = made };
            ApartSpot(g);
            s.apartGroups.Add(g);
            string names = string.Join(" and ", made.Select(n => n.name));
            string home = kind == ApartKind.ShoreHut ? "built a hut on the far shore of the pond" : "made a camp in the far woods";
            Chronicle("newcomer", $"The {family} family, {names}, wandered in from a far-away tank and {home}.", 0.7f, -1);
        }

        /// <summary>Monthly: the people who live apart age with the calendar and die of old age like anyone. No dice; nothing when there are none.</summary>
        void ApartStep()
        {
            var gs = State.apartGroups;
            if (gs == null || gs.Count == 0) return;
            float step = Config.notableAgeFactor / SimConfig.MonthsPerYear;
            foreach (var g in gs)
                foreach (var n in g.members)
                {
                    if (!n.alive) continue;
                    n.age += step;
                    if (n.age < n.lifespan) continue;
                    n.alive = false;
                    n.diedMonth = State.month;
                    string where = g.kind == ApartKind.ShoreHut ? "of the far shore" : "of the far woods";
                    Chronicle("death", $"{n.FullName} {where} died of old age at {Math.Floor(n.age)}.", 0.6f, -1);
                }
        }

        /// <summary>The home spot: for a woods camp, the open cell farthest from the green that has the most forest round it; for a shore
        /// hut, the open cell beside the pond farthest from the green (else as a woods camp). Open: no lot, no yard ring, no pond,
        /// green, canal, commons, yard or rock, and not hard against the glass. Read-only on the map (nothing is reserved). With
        /// settling off, a fixed far corner.</summary>
        void ApartSpot(ApartGroup g)
        {
            var map = Settlement?.Map;
            if (map == null)
            {
                g.x = -Terrain.HX * 0.75f; g.z = g.kind == ApartKind.ShoreHut ? -Terrain.HZ * 0.7f : Terrain.HZ * 0.7f;
                return;
            }
            const byte block = SiteMap.FCanal | SiteMap.FGreen | SiteMap.FCommons | SiteMap.FYard | SiteMap.FRock;
            int best = -1; float bestScore = float.MinValue;
            for (int pass = g.kind == ApartKind.ShoreHut ? 0 : 1; pass < 2 && best < 0; pass++)
                for (int c = 0; c < SiteMap.N; c++)
                {
                    int i = SiteMap.I(c), j = SiteMap.J(c);
                    if (i < 3 || j < 3 || i >= SiteMap.NX - 3 || j >= SiteMap.NZ - 3) continue;
                    if (map.pond[c] || map.lot[c] != 0 || map.ring[c] != 0 || (map.flags[c] & block) != 0) continue;
                    float dx = SiteMap.CXc(c) - map.greenX, dz = SiteMap.CZc(c) - map.greenZ;
                    float score = (float)Math.Sqrt(dx * dx + dz * dz);
                    if (pass == 0)
                    {
                        bool shore = map.pond[c - 1] || map.pond[c + 1] || map.pond[c - SiteMap.NX] || map.pond[c + SiteMap.NX];
                        if (!shore) continue;
                    }
                    else
                    {
                        int forest = 0;
                        for (int dj = -1; dj <= 1; dj++) for (int di = -1; di <= 1; di++) if (map.Forest(c + dj * SiteMap.NX + di)) forest++;
                        score += forest * 0.2f;
                    }
                    if (score > bestScore) { bestScore = score; best = c; }
                }
            if (best < 0) { g.x = -Terrain.HX * 0.75f; g.z = Terrain.HZ * 0.7f; return; }
            g.cell = best;
            g.x = SiteMap.CXc(best);
            g.z = SiteMap.CZc(best);
        }
    }
}
