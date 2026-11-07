using System;
using System.Collections.Generic;
using System.Linq;
using AquariumCiv.Sim.Settle;

namespace AquariumCiv.Sim
{
    /// <summary>#371: the small things a choice leaves in the box (visit-loop.md 2.5). Names the view draws (ConsequenceProps).</summary>
    public static class ConsequenceKind
    {
        public const string Feather = "feather", Baskets = "baskets", WoodpileRoof = "woodpile_roof", NameStone = "name_stone",
            CharmPole = "charm_pole", StewPot = "stew_pot", AxeStump = "axe_stump", Staff = "staff", FlourSacks = "flour_sacks",
            RoofBird = "roof_bird", LongTable = "long_table", OfferingBasket = "offering_basket", GoatFence = "goat_fence",
            FishCrate = "fish_crate", NamePeg = "name_peg";

        public static readonly string[] All =
        {
            Feather, Baskets, WoodpileRoof, NameStone, CharmPole, StewPot, AxeStump, Staff, FlourSacks, RoofBird, LongTable,
            OfferingBasket, GoatFence, FishCrate, NamePeg,
        };
    }

    /// <summary>Where a consequence prop goes (#371). Each falls back to the fire if its anchor is not there.</summary>
    public enum PropAnchor
    {
        /// <summary>By the home of the option's person (a small choice: of its subject).</summary>
        Home,
        /// <summary>By the option's building, else the first standing building of <see cref="DilemmaOptionDef.buildingType"/>.</summary>
        Building,
        /// <summary>Near the fire or the green.</summary>
        Fire,
        /// <summary>Out at the edge of the settlement (an offering on the hill, a charm pole at the edge of the woods).</summary>
        Edge,
        /// <summary>By the remembering stone / the pond side.</summary>
        Pond,
    }

    /// <summary>One thing an answered (or lapsed) choice left in the box. Saved with the box. Placed by the sim at a real, open spot
    /// (box coordinates, as Building.x/z) at most <see cref="SimCore.PropWithinMonths"/> after the choice ended.</summary>
    [Serializable]
    public class ConsequenceProp
    {
        public int id;
        public int dilemmaId;
        public int option;
        public string kind = "";            // ConsequenceKind
        public PropAnchor anchor;
        public bool placed;
        public float x, z, yaw;             // the spot (valid once placed), yaw in degrees
        public int personId = -1;           // the person it is about (-1 = none)
        public int buildingId = -1;         // the building it stands by (-1 = none)
        public string buildingType = "";    // else the first standing building of this type (PropAnchor.Building)
        public long dueMonth;               // the choice ended this month; it must stand within PropWithinMonths
        public long placedMonth = -1;
        public bool keeper;                 // the Keeper's choice (answered or "let them decide"), not the people's
    }

    public partial class SimCore
    {
        /// <summary>A choice's thing stands within this many game months of its end (one game year).</summary>
        public const int PropWithinMonths = SimConfig.MonthsPerYear;

        /// <summary>The things the choices left, in the order they were made.</summary>
        public IReadOnlyList<ConsequenceProp> ConsequenceProps => State.consequenceProps ??= new List<ConsequenceProp>();

        public ConsequenceProp FindConsequenceProp(int id) => ConsequenceProps.FirstOrDefault(p => p.id == id);

        /// <summary>The thing the option left (null if none).</summary>
        public ConsequenceProp ConsequencePropFor(int dilemmaId, int option) =>
            ConsequenceProps.FirstOrDefault(p => p.dilemmaId == dilemmaId && p.option == option);

        /// <summary>The thing a callback line is about, from its chronicle entry (null if none or not a callback).</summary>
        public ConsequenceProp ConsequencePropOfEntry(ChronicleEntry e)
        {
            var info = CallbackInfoOf(e);
            return info == null ? null : ConsequencePropFor(info.dilemma.id, info.option);
        }

        /// <summary>The option made a thing: queue it (placed now if its anchor is there, else on a later month, within the year).</summary>
        void LeaveConsequence(Dilemma d, DilemmaDef def, int option)
        {
            var o = def.options[option];
            if (string.IsNullOrEmpty(o.prop)) return;
            var list = (State.consequenceProps ??= new List<ConsequenceProp>());
            if (list.Any(p => p.dilemmaId == d.id && p.option == option)) return;
            int person = def.smallKind != null && d.subjectId >= 0 ? d.subjectId : d.PersonOf(option); // t4-fixes: a small choice with no subject (the story) is about its person
            var p = new ConsequenceProp
            {
                id = list.Count == 0 ? 1 : list.Max(x => x.id) + 1, dilemmaId = d.id, option = option, kind = o.prop, anchor = o.propAt,
                personId = person, buildingId = d.BuildingOf(option), buildingType = string.IsNullOrEmpty(o.propBuilding) ? o.buildingType ?? "" : o.propBuilding, dueMonth = State.month,
                keeper = d.state == DilemmaState.Answered,
            };
            list.Add(p);
            TryPlaceProp(p, false);
        }

        /// <summary>Every month: a thing still waiting for its anchor is placed; at the end of the year it goes by the fire.</summary>
        void ConsequencePropStep()
        {
            var list = State.consequenceProps;
            if (list == null || list.Count == 0) return;
            foreach (var p in list)
                if (!p.placed) TryPlaceProp(p, State.month >= p.dueMonth + PropWithinMonths - 1);
        }

        void TryPlaceProp(ConsequenceProp p, bool force)
        {
            float ax = 0f, az = 0f, minR = 0.12f, maxR = 0.4f;
            bool found = AnchorOf(p, ref ax, ref az, ref minR, ref maxR);
            if (!found)
            {
                if (!force) return;
                p.anchor = PropAnchor.Fire;
                AnchorOf(p, ref ax, ref az, ref minR, ref maxR);
            }
            PropSpot(p, ax, az, minR, maxR, out p.x, out p.z);
            p.yaw = (int)(DilemmaRoll(110, p.dilemmaId * 8 + p.option, p.id) * 360f);
            p.placed = true;
            p.placedMonth = State.month;
        }

        bool AnchorOf(ConsequenceProp p, ref float ax, ref float az, ref float minR, ref float maxR)
        {
            var st = Settlement;
            switch (p.anchor)
            {
                case PropAnchor.Home:
                {
                    var n = Alive(p.personId);
                    var lot = n == null || st == null ? null : st.HomeOf(n);
                    if (lot == null) return false;
                    ax = lot.doorX; az = lot.doorZ; minR = 0.1f; maxR = 0.3f;
                    return true;
                }
                case PropAnchor.Building:
                {
                    var b = State.buildings.FirstOrDefault(x => x.id == p.buildingId);
                    if (b == null)
                    {
                        string type = p.buildingType;
                        b = string.IsNullOrEmpty(type) ? null : State.buildings.FirstOrDefault(x => x.type == type && x.lotId != 0);
                    }
                    if (b == null) return false;
                    ax = b.doorX != 0f || b.doorZ != 0f ? b.doorX : b.x; az = b.doorX != 0f || b.doorZ != 0f ? b.doorZ : b.z;
                    minR = 0.12f; maxR = 0.35f;
                    return true;
                }
                case PropAnchor.Fire:
                    if (st != null) st.Fire(out ax, out az, out _); else { ax = 0f; az = 0f; }
                    minR = 0.45f; maxR = 0.85f;
                    return true;
                case PropAnchor.Edge:
                {
                    var (x, z) = ApartSpotPreview(ApartKind.WoodsCamp);
                    ax = x * 0.8f; az = z * 0.8f; minR = 0.05f; maxR = 0.5f;
                    return true;
                }
                case PropAnchor.Pond:
                {
                    var map = st?.Map;
                    if (map == null) { ax = -Terrain.HX * 0.5f; az = 0f; minR = 0.1f; maxR = 0.6f; return true; }
                    // The open ground nearest the pond's edge (the remembering stone stands by the water).
                    float bestD = float.MaxValue;
                    for (int c = 0; c < SiteMap.N; c++)
                    {
                        if (map.pond[c] || map.waterDist[c] > 0.35f || map.lot[c] != 0) continue;
                        float dx = SiteMap.CXc(c) - map.greenX, dz = SiteMap.CZc(c) - map.greenZ, d = dx * dx + dz * dz;
                        if (d < bestD) { bestD = d; ax = SiteMap.CXc(c); az = SiteMap.CZc(c); }
                    }
                    if (bestD == float.MaxValue) return false;
                    minR = 0.0f; maxR = 0.4f;
                    return true;
                }
            }
            return false;
        }

        /// <summary>An open spot in a ring round the anchor, clear of lots, paths, water, trees, the green and the other things: the
        /// candidate with the lowest hash. With no map, a point on the ring.</summary>
        void PropSpot(ConsequenceProp p, float ax, float az, float minR, float maxR, out float x, out float z)
        {
            var map = Settlement?.Map;
            int salt = p.dilemmaId * 8 + p.option;
            float mx = Terrain.HX - Terrain.GlassMargin, mz = Terrain.HZ - Terrain.GlassMargin;
            if (map == null)
            {
                double a = DilemmaRoll(111, salt, p.id) * Math.PI * 2.0;
                float r = (minR + maxR) * 0.5f + p.id * 0.07f;
                x = Math.Max(-mx, Math.Min(mx, ax + (float)Math.Cos(a) * r)); z = Math.Max(-mz, Math.Min(mz, az + (float)Math.Sin(a) * r));
                return;
            }
            const byte block = SiteMap.FCanal | SiteMap.FGreen | SiteMap.FCommons | SiteMap.FYard | SiteMap.FRock;
            for (int pass = 0; pass < 3; pass++)
            {
                float lo = pass == 0 ? minR : 0f, hi = maxR + pass * 0.3f;
                int best = -1; float bestRoll = float.MaxValue;
                for (int c = 0; c < SiteMap.N; c++)
                {
                    float cx = SiteMap.CXc(c), cz = SiteMap.CZc(c);
                    if (Math.Abs(cx) > mx || Math.Abs(cz) > mz) continue;
                    float dx = cx - ax, dz = cz - az, d = (float)Math.Sqrt(dx * dx + dz * dz);
                    if (d < lo || d > hi) continue;
                    if (map.pond[c] || map.lot[c] != 0 || map.ring[c] != 0 || (map.flags[c] & block) != 0 || map.Forest(c)) continue;
                    if (pass < 2 && Settlement.WalkCost(c) == 0) continue;
                    bool crowded = false;
                    foreach (var q in State.consequenceProps)
                        if (q != p && q.placed && (q.x - cx) * (q.x - cx) + (q.z - cz) * (q.z - cz) < 0.22f * 0.22f) { crowded = true; break; }
                    if (crowded) continue;
                    float roll = DilemmaRoll(112, salt * 4096 + c, p.id);
                    if (roll < bestRoll) { bestRoll = roll; best = c; }
                }
                if (best >= 0) { x = SiteMap.CXc(best); z = SiteMap.CZc(best); return; }
            }
            x = Math.Max(-mx, Math.Min(mx, ax)); z = Math.Max(-mz, Math.Min(mz, az));
        }

        /// <summary>Debug: every option of every choice that has a thing leaves it now (one per option, as if each had been taken),
        /// placed at once. Returns how many stand.</summary>
        public int DebugPlaceEveryProp()
        {
            var list = (State.consequenceProps ??= new List<ConsequenceProp>());
            int fake = 90000;
            foreach (var def in DilemmaCatalog.Concat(SmallCatalog))
                for (int i = 0; i < def.options.Length; i++)
                {
                    if (string.IsNullOrEmpty(def.options[i].prop)) continue;
                    var d = new Dilemma { id = fake++, defId = def.id, month = State.month, endMonth = State.month, state = DilemmaState.Answered, end = DilemmaEnd.Answered, option = i, personA = Pick(0), personB = Pick(1), subjectId = Pick(0) };
                    LeaveConsequence(d, def, i);
                }
            foreach (var p in list) if (!p.placed) TryPlaceProp(p, true);
            return list.Count(p => p.placed);
        }

        int Pick(int skip)
        {
            var alive = State.notables.Where(n => n.alive).ToList();
            return alive.Count == 0 ? -1 : alive[Math.Min(skip, alive.Count - 1)].id;
        }
    }
}
