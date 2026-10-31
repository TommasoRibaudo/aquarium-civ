using System;
using AquariumCiv.Sim;

namespace AquariumCiv.Spikes.Growth
{
    public enum Era { Camp, Village, Town }

    /// <summary>What stands on a lot. SimCore's types, plus the Town-era types SimCore doesn't have yet (spike-side).</summary>
    public enum Kind
    {
        Camp, Hut, Farm, Lumbermill, Well, Windmill, Market, Tavern, Church,
        Watermill, Smithy, Dyer, Cottage, Shop,
    }

    /// <summary>The score terms of research 08 §3.3. Each returns f in [-1, 1]; constraints (overlap, water, glass) are
    /// checked separately and make a candidate invalid.</summary>
    public enum Term
    {
        Green, Access, Social, SameKind, Slope, Water, Fertility, Forest, Height, Busy, Quiet, FrontTall, Nudge, Edge, NearKind,
    }

    /// <summary>One building type's entry in the "building encyclopedia" (Emilien et al. 2012): lot size and weights.</summary>
    public sealed class KindSpec
    {
        public Kind kind;
        public float wMin, wMax, dMin, dMax;   // lot: w along the frontage, d back from it
        public float maxSlope = 0.45f;
        public float minWaterDist = 0.15f;     // lot cells keep this far from water...
        public bool needsWater;                // ...unless the lot must touch water (watermill)
        public bool tall;
        public bool home;                      // people live here: the start of trips that wear paths
        public bool workplace;
        public bool edgeCandidates;            // also try spots out on rays from the green (fields, mills)
        public bool backland;                  // also try spots behind existing house lots (Town infill)
        public float greenNear, greenFar;      // Green term: 1 inside [near, far], falling to -1 at far + 1.5
        public Kind[] nearKinds = Array.Empty<Kind>();
        public readonly float[] w = new float[Enum.GetValues(typeof(Term)).Length];

        public KindSpec W(Term t, float weight) { w[(int)t] = weight; return this; }
    }

    public static class Encyclopedia
    {
        public static readonly KindSpec[] Specs = Build();

        public static KindSpec Of(Kind k) => Specs[(int)k];

        public static bool IsHome(Kind k) => Of(k).home;

        public static Kind FromSim(string type) => type switch
        {
            BuildingType.Camp => Kind.Camp,
            BuildingType.Hut => Kind.Hut,
            BuildingType.Farm => Kind.Farm,
            BuildingType.Lumbermill => Kind.Lumbermill,
            BuildingType.Well => Kind.Well,
            BuildingType.Windmill => Kind.Windmill,
            BuildingType.Market => Kind.Market,
            BuildingType.Tavern => Kind.Tavern,
            BuildingType.Church => Kind.Church,
            _ => Kind.Hut,
        };

        static KindSpec[] Build()
        {
            var a = new KindSpec[Enum.GetValues(typeof(Kind)).Length];
            KindSpec S(Kind k, float wMin, float wMax, float dMin, float dMax) => a[(int)k] = new KindSpec { kind = k, wMin = wMin, wMax = wMax, dMin = dMin, dMax = dMax };

            // The founding hearth and the well are placed on the green, not scored.
            S(Kind.Camp, 0.3f, 0.3f, 0.3f, 0.3f);
            S(Kind.Well, 0.24f, 0.24f, 0.24f, 0.24f);

            // Homes: the nucleated look (near the green, fronting a path, near neighbours, dry and flat).
            var hut = S(Kind.Hut, 0.38f, 0.44f, 0.42f, 0.5f);
            hut.home = true; hut.greenNear = 0.45f; hut.greenFar = 1.15f;
            hut.W(Term.Green, 1.0f).W(Term.Access, 1.2f).W(Term.Social, 0.9f).W(Term.Slope, 0.8f).W(Term.Water, 0.3f).W(Term.Forest, 0.6f).W(Term.Nudge, 4.0f);

            var cottage = S(Kind.Cottage, 0.34f, 0.38f, 0.34f, 0.38f);
            cottage.home = true; cottage.backland = true; cottage.greenNear = 0.5f; cottage.greenFar = 1.5f;
            cottage.W(Term.Green, 0.8f).W(Term.Access, 0.8f).W(Term.Social, 1.2f).W(Term.Slope, 0.8f).W(Term.Forest, 0.6f).W(Term.Nudge, 4.0f);

            // Fields: flat, fertile, next to other fields, out at the edge.
            var farm = S(Kind.Farm, 0.4f, 0.5f, 0.7f, 0.9f);
            farm.maxSlope = 0.3f; farm.edgeCandidates = true; farm.greenNear = 1.2f; farm.greenFar = 2.6f;
            farm.W(Term.Edge, 1.0f).W(Term.Fertility, 1.6f).W(Term.Slope, 1.2f).W(Term.SameKind, 0.8f).W(Term.Access, 0.6f).W(Term.Water, 0.3f).W(Term.Forest, 0.8f).W(Term.Nudge, 4.0f);

            var mill = S(Kind.Lumbermill, 0.44f, 0.48f, 0.38f, 0.42f);
            mill.workplace = true; mill.edgeCandidates = true; mill.greenNear = 0.9f; mill.greenFar = 2.4f;
            mill.W(Term.Forest, 1.6f).W(Term.Access, 0.8f).W(Term.Quiet, 0.6f).W(Term.Slope, 0.5f).W(Term.Green, 0.4f).W(Term.Nudge, 4.0f);

            var wind = S(Kind.Windmill, 0.36f, 0.36f, 0.36f, 0.36f);
            wind.workplace = true; wind.tall = true; wind.edgeCandidates = true; wind.maxSlope = 0.6f; wind.greenNear = 1.0f; wind.greenFar = 2.6f;
            wind.nearKinds = new[] { Kind.Farm };
            wind.W(Term.Height, 1.5f).W(Term.Forest, 0.8f).W(Term.NearKind, 0.6f).W(Term.Access, 0.5f).W(Term.FrontTall, 1.0f).W(Term.Green, 0.5f).W(Term.Nudge, 4.0f);

            var market = S(Kind.Market, 0.54f, 0.58f, 0.4f, 0.44f);
            market.workplace = true; market.greenNear = 0.42f; market.greenFar = 0.75f;
            market.W(Term.Green, 2.0f).W(Term.Busy, 1.0f).W(Term.Access, 1.0f).W(Term.Slope, 0.4f).W(Term.Nudge, 4.0f);

            var tavern = S(Kind.Tavern, 0.48f, 0.52f, 0.4f, 0.44f);
            tavern.greenNear = 0.5f; tavern.greenFar = 1.2f; tavern.nearKinds = new[] { Kind.Market };
            tavern.W(Term.Busy, 1.0f).W(Term.NearKind, 0.8f).W(Term.Green, 0.8f).W(Term.Access, 1.0f).W(Term.Slope, 0.4f).W(Term.Nudge, 4.0f);

            var church = S(Kind.Church, 0.42f, 0.46f, 0.7f, 0.76f);
            church.tall = true; church.edgeCandidates = true; church.maxSlope = 0.5f; church.greenNear = 0.5f; church.greenFar = 1.5f;
            church.W(Term.Height, 1.2f).W(Term.Green, 1.0f).W(Term.Access, 0.8f).W(Term.FrontTall, 1.0f).W(Term.Slope, 0.4f).W(Term.Nudge, 4.0f);

            var water = S(Kind.Watermill, 0.44f, 0.48f, 0.38f, 0.42f);
            water.workplace = true; water.needsWater = true; water.edgeCandidates = true; water.minWaterDist = 0f; water.greenNear = 0.8f; water.greenFar = 2.6f;
            water.W(Term.Water, 1.5f).W(Term.Access, 0.8f).W(Term.Green, 0.3f).W(Term.Slope, 0.4f).W(Term.Nudge, 4.0f);

            var smith = S(Kind.Smithy, 0.4f, 0.44f, 0.38f, 0.42f);
            smith.home = true; smith.workplace = true; smith.greenNear = 0.7f; smith.greenFar = 1.7f;
            smith.nearKinds = new[] { Kind.Watermill, Kind.Smithy, Kind.Dyer };
            smith.W(Term.NearKind, 0.9f).W(Term.Quiet, 0.4f).W(Term.Access, 1.0f).W(Term.Green, 0.6f).W(Term.Slope, 0.6f).W(Term.Nudge, 4.0f);

            var dyer = S(Kind.Dyer, 0.42f, 0.46f, 0.42f, 0.46f);
            dyer.home = true; dyer.workplace = true; dyer.edgeCandidates = true; dyer.greenNear = 1.0f; dyer.greenFar = 2.4f;
            dyer.nearKinds = new[] { Kind.Watermill, Kind.Dyer, Kind.Smithy };
            dyer.W(Term.Water, 1.3f).W(Term.Edge, 0.6f).W(Term.NearKind, 0.7f).W(Term.Access, 0.8f).W(Term.Slope, 0.6f).W(Term.Nudge, 4.0f);

            var shop = S(Kind.Shop, 0.36f, 0.4f, 0.34f, 0.38f);
            shop.home = true; shop.greenNear = 0.2f; shop.greenFar = 0.5f; shop.nearKinds = new[] { Kind.Market };
            shop.W(Term.Green, 1.5f).W(Term.Busy, 1.0f).W(Term.NearKind, 0.8f).W(Term.Access, 0.6f);
            return a;
        }

        /// <summary>The ideal lot water distance band per kind for the Water term (wet point vs dry point).</summary>
        public static float WaterTerm(Kind k, float dWater)
        {
            switch (k)
            {
                case Kind.Watermill:
                case Kind.Dyer:
                    return 1f - GrowthTerrain.Clamp01(dWater / 0.6f) * 2f;
                case Kind.Farm:
                    return 1f - GrowthTerrain.Clamp01(dWater / 1.5f) * 2f;
                default:
                    // Houses: near water is good (wet point) but not on its edge (dry point).
                    if (dWater < 0.3f) return -0.5f;
                    if (dWater < 1.2f) return 1f;
                    return 1f - GrowthTerrain.Clamp01((dWater - 1.2f) / 1.5f) * 1.5f;
            }
        }
    }
}
