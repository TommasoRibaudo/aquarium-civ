using System;

namespace AquariumCiv.Sim.Settle
{
    /// <summary>The score terms of research 08 §3.3. Each returns f in [−1, 1]; constraints (overlap, water, glass) are
    /// checked separately and make a candidate invalid.</summary>
    public enum Term
    {
        Green, Access, Social, SameKind, Slope, Water, Fertility, Forest, Height, Busy, Quiet, FrontTall, Nudge, Edge, NearKind,
    }

    /// <summary>Which trips a lot's people make or draw (the wear pass and the planner use the same groups).</summary>
    public enum TripGroup { None, Field, Work, Gathering }

    /// <summary>One lot kind's entry in the "building encyclopedia" (Emilien et al. 2012): lot size and weights.</summary>
    public sealed class KindSpec
    {
        public LotKind kind;
        public float wMin, wMax, dMin, dMax;   // lot: w along the frontage, d back from it
        public float maxSlope = 0.45f;
        public float minWaterDist = 0.15f;     // lot cells keep this far from water...
        public bool needsWater;                // ...unless the lot must touch water (watermill, jetty)
        public bool tall;
        public bool home;                      // people live here: the start of trips that wear paths
        public TripGroup trips;                // what people walk here for
        public bool edgeCandidates;            // also try spots out on rays from the green (fields, mills)
        public bool backland;                  // also try spots behind existing house lots (Town infill)
        public bool rows;                      // strip-field rows beside lots of the same kind (round 1)
        public float greenNear, greenFar;      // Green term: 1 inside [near, far], falling to −1 at far + 1.5
        public LotKind[] nearKinds = Array.Empty<LotKind>();
        public readonly float[] w = new float[Enum.GetValues(typeof(Term)).Length];

        public KindSpec W(Term t, float weight) { w[(int)t] = weight; return this; }
    }

    /// <summary>Lot sizes and siting weights per kind (growth-lab.md §4 for the round-1 values; the kinds added for
    /// the shaping answers borrow from their nearest round-1 relative).</summary>
    public static class Encyclopedia
    {
        public static readonly KindSpec[] Specs = Build();

        public static KindSpec Of(LotKind k) => Specs[(int)k];
        public static bool IsHome(LotKind k) => Of(k).home;

        /// <summary>The kind a building of an economy type stands on, when nothing (a shaping, the land) says otherwise.</summary>
        public static LotKind FromType(string type) => type switch
        {
            BuildingType.Camp => LotKind.Camp,
            BuildingType.Hut => LotKind.Hut,
            BuildingType.Farm => LotKind.Farm,
            BuildingType.Lumbermill => LotKind.Lumbermill,
            BuildingType.Well => LotKind.Well,
            BuildingType.Windmill => LotKind.Windmill,
            BuildingType.Market => LotKind.Market,
            BuildingType.Tavern => LotKind.Tavern,
            BuildingType.Church => LotKind.Church,
            BuildingType.Workshop => LotKind.Workshop,
            BuildingType.Guildhall => LotKind.Guildhall,
            BuildingType.Shop => LotKind.Shop,
            _ => LotKind.Hut,
        };

        static KindSpec[] Build()
        {
            var a = new KindSpec[Enum.GetValues(typeof(LotKind)).Length];
            KindSpec S(LotKind k, float wMin, float wMax, float dMin, float dMax) => a[(int)k] = new KindSpec { kind = k, wMin = wMin, wMax = wMax, dMin = dMin, dMax = dMax };

            // The founding hearth and the well are placed on the green, not scored.
            S(LotKind.Camp, 0.3f, 0.3f, 0.3f, 0.3f);
            // The first well stands on the green unscored; a later one (a spring's) is sited like a small gathering place.
            var well = S(LotKind.Well, 0.24f, 0.24f, 0.24f, 0.24f);
            well.trips = TripGroup.Gathering; well.greenNear = 0.6f; well.greenFar = 2.2f;
            well.W(Term.Access, 1.0f).W(Term.Green, 0.4f).W(Term.Slope, 0.4f).W(Term.Nudge, 4.0f);

            var hut = S(LotKind.Hut, 0.38f, 0.44f, 0.42f, 0.5f);
            hut.home = true; hut.greenNear = 0.45f; hut.greenFar = 1.15f;
            hut.W(Term.Green, 1.0f).W(Term.Access, 1.2f).W(Term.Social, 0.9f).W(Term.Slope, 0.8f).W(Term.Water, 0.3f).W(Term.Forest, 0.6f).W(Term.Nudge, 4.0f);

            var cottage = S(LotKind.Cottage, 0.34f, 0.38f, 0.34f, 0.38f);
            cottage.home = true; cottage.backland = true; cottage.greenNear = 0.5f; cottage.greenFar = 1.5f;
            cottage.W(Term.Green, 0.8f).W(Term.Access, 0.8f).W(Term.Social, 1.2f).W(Term.Slope, 0.8f).W(Term.Forest, 0.6f).W(Term.Nudge, 4.0f);

            // Fields: flat, fertile, next to other fields, out at the edge (strip-field rows).
            var farm = S(LotKind.Farm, 0.4f, 0.5f, 0.7f, 0.9f);
            farm.maxSlope = 0.3f; farm.edgeCandidates = true; farm.rows = true; farm.trips = TripGroup.Field; farm.greenNear = 1.2f; farm.greenFar = 2.6f;
            farm.W(Term.Edge, 1.0f).W(Term.Fertility, 1.6f).W(Term.Slope, 1.2f).W(Term.SameKind, 0.8f).W(Term.Access, 0.6f).W(Term.Water, 0.3f).W(Term.Forest, 0.8f).W(Term.Nudge, 4.0f);

            var orchard = S(LotKind.Orchard, 0.44f, 0.5f, 0.5f, 0.6f);
            orchard.maxSlope = 0.4f; orchard.edgeCandidates = true; orchard.trips = TripGroup.Field; orchard.greenNear = 1.0f; orchard.greenFar = 2.4f;
            orchard.W(Term.Edge, 0.6f).W(Term.Fertility, 1.0f).W(Term.Slope, 0.8f).W(Term.Access, 0.6f).W(Term.Water, 0.3f).W(Term.Nudge, 4.0f);

            var pasture = S(LotKind.Pasture, 0.5f, 0.6f, 0.5f, 0.6f);
            pasture.maxSlope = 0.5f; pasture.edgeCandidates = true; pasture.trips = TripGroup.Field; pasture.greenNear = 1.2f; pasture.greenFar = 2.6f;
            pasture.W(Term.Edge, 1.0f).W(Term.Slope, 0.6f).W(Term.Access, 0.5f).W(Term.Forest, 0.6f).W(Term.Quiet, 0.4f).W(Term.Nudge, 4.0f);

            var mill = S(LotKind.Lumbermill, 0.44f, 0.48f, 0.38f, 0.42f);
            mill.trips = TripGroup.Work; mill.edgeCandidates = true; mill.greenNear = 0.9f; mill.greenFar = 2.4f;
            mill.W(Term.Forest, 1.6f).W(Term.Access, 0.8f).W(Term.Quiet, 0.6f).W(Term.Slope, 0.5f).W(Term.Green, 0.4f).W(Term.Nudge, 4.0f);

            var timber = S(LotKind.TimberYard, 0.46f, 0.52f, 0.4f, 0.46f);
            timber.trips = TripGroup.Work; timber.edgeCandidates = true; timber.greenNear = 0.9f; timber.greenFar = 2.4f;
            timber.W(Term.Forest, 1.4f).W(Term.Access, 1.0f).W(Term.Quiet, 0.4f).W(Term.Slope, 0.5f).W(Term.Green, 0.4f).W(Term.Nudge, 4.0f);

            var wind = S(LotKind.Windmill, 0.36f, 0.36f, 0.36f, 0.36f);
            wind.trips = TripGroup.Work; wind.tall = true; wind.edgeCandidates = true; wind.maxSlope = 0.6f; wind.greenNear = 1.0f; wind.greenFar = 2.6f;
            wind.nearKinds = new[] { LotKind.Farm };
            wind.W(Term.Height, 1.5f).W(Term.Forest, 0.8f).W(Term.NearKind, 0.6f).W(Term.Access, 0.5f).W(Term.FrontTall, 1.0f).W(Term.Green, 0.5f).W(Term.Nudge, 4.0f);

            var market = S(LotKind.Market, 0.54f, 0.58f, 0.4f, 0.44f);
            market.trips = TripGroup.Gathering; market.greenNear = 0.42f; market.greenFar = 0.75f;
            market.W(Term.Green, 2.0f).W(Term.Busy, 1.0f).W(Term.Access, 1.0f).W(Term.Slope, 0.4f).W(Term.Nudge, 4.0f);

            var tavern = S(LotKind.Tavern, 0.48f, 0.52f, 0.4f, 0.44f);
            tavern.trips = TripGroup.Gathering; tavern.greenNear = 0.5f; tavern.greenFar = 1.2f; tavern.nearKinds = new[] { LotKind.Market };
            tavern.W(Term.Busy, 1.0f).W(Term.NearKind, 0.8f).W(Term.Green, 0.8f).W(Term.Access, 1.0f).W(Term.Slope, 0.4f).W(Term.Nudge, 4.0f);

            var guild = S(LotKind.Guildhall, 0.5f, 0.54f, 0.44f, 0.5f);
            guild.trips = TripGroup.Gathering; guild.tall = true; guild.greenNear = 0.5f; guild.greenFar = 1.2f; guild.nearKinds = new[] { LotKind.Market };
            guild.W(Term.Busy, 1.0f).W(Term.NearKind, 1.0f).W(Term.Green, 1.0f).W(Term.Access, 1.0f).W(Term.Slope, 0.4f).W(Term.Nudge, 4.0f);

            var church = S(LotKind.Church, 0.42f, 0.46f, 0.7f, 0.76f);
            church.trips = TripGroup.Gathering; church.tall = true; church.edgeCandidates = true; church.maxSlope = 0.5f; church.greenNear = 0.5f; church.greenFar = 1.5f;
            church.W(Term.Height, 1.2f).W(Term.Green, 1.0f).W(Term.Access, 0.8f).W(Term.FrontTall, 1.0f).W(Term.Slope, 0.4f).W(Term.Nudge, 4.0f);

            var water = S(LotKind.Watermill, 0.44f, 0.48f, 0.38f, 0.42f);
            water.trips = TripGroup.Work; water.needsWater = true; water.edgeCandidates = true; water.minWaterDist = 0f; water.greenNear = 0.8f; water.greenFar = 2.6f;
            water.W(Term.Water, 1.5f).W(Term.Access, 0.8f).W(Term.Green, 0.3f).W(Term.Slope, 0.4f).W(Term.Nudge, 4.0f);

            var jetty = S(LotKind.Jetty, 0.3f, 0.34f, 0.3f, 0.34f);
            jetty.trips = TripGroup.Work; jetty.needsWater = true; jetty.minWaterDist = 0f; jetty.greenNear = 0.8f; jetty.greenFar = 2.6f;
            jetty.W(Term.Water, 1.5f).W(Term.Access, 0.8f).W(Term.Slope, 0.4f).W(Term.Nudge, 4.0f);

            var smith = S(LotKind.Smithy, 0.4f, 0.44f, 0.38f, 0.42f);
            smith.home = true; smith.trips = TripGroup.Work; smith.greenNear = 0.7f; smith.greenFar = 1.7f;
            smith.nearKinds = new[] { LotKind.Watermill, LotKind.Smithy, LotKind.Dyer, LotKind.Workshop };
            smith.W(Term.NearKind, 0.9f).W(Term.Quiet, 0.4f).W(Term.Access, 1.0f).W(Term.Green, 0.6f).W(Term.Slope, 0.6f).W(Term.Nudge, 4.0f);

            var shop = S(LotKind.Workshop, 0.4f, 0.44f, 0.38f, 0.42f);
            shop.home = true; shop.trips = TripGroup.Work; shop.greenNear = 0.7f; shop.greenFar = 1.7f;
            shop.nearKinds = new[] { LotKind.Watermill, LotKind.Smithy, LotKind.Dyer, LotKind.Workshop };
            shop.W(Term.NearKind, 0.9f).W(Term.Quiet, 0.4f).W(Term.Access, 1.0f).W(Term.Green, 0.6f).W(Term.Slope, 0.6f).W(Term.Nudge, 4.0f);

            var quarry = S(LotKind.Quarry, 0.4f, 0.46f, 0.38f, 0.44f);
            quarry.trips = TripGroup.Work; quarry.edgeCandidates = true; quarry.maxSlope = 0.7f; quarry.greenNear = 0.9f; quarry.greenFar = 2.6f;
            quarry.W(Term.Quiet, 0.6f).W(Term.Access, 0.8f).W(Term.Green, 0.3f).W(Term.Nudge, 4.0f);

            var kiln = S(LotKind.Kiln, 0.38f, 0.42f, 0.38f, 0.42f);
            kiln.home = true; kiln.trips = TripGroup.Work; kiln.edgeCandidates = true; kiln.greenNear = 0.9f; kiln.greenFar = 2.4f;
            kiln.nearKinds = new[] { LotKind.Smithy, LotKind.Workshop, LotKind.Dyer };
            kiln.W(Term.Water, 1.0f).W(Term.NearKind, 0.5f).W(Term.Access, 0.8f).W(Term.Slope, 0.6f).W(Term.Nudge, 4.0f);

            var dyer = S(LotKind.Dyer, 0.42f, 0.46f, 0.42f, 0.46f);
            dyer.home = true; dyer.trips = TripGroup.Work; dyer.edgeCandidates = true; dyer.greenNear = 1.0f; dyer.greenFar = 2.4f;
            dyer.nearKinds = new[] { LotKind.Watermill, LotKind.Dyer, LotKind.Smithy };
            dyer.W(Term.Water, 1.3f).W(Term.Edge, 0.6f).W(Term.NearKind, 0.7f).W(Term.Access, 0.8f).W(Term.Slope, 0.6f).W(Term.Nudge, 4.0f);

            var stall = S(LotKind.Shop, 0.36f, 0.4f, 0.34f, 0.38f);
            stall.home = true; stall.greenNear = 0.2f; stall.greenFar = 0.5f; stall.nearKinds = new[] { LotKind.Market };
            stall.W(Term.Green, 1.5f).W(Term.Busy, 1.0f).W(Term.NearKind, 0.8f).W(Term.Access, 0.6f);

            // A family's barn or shed: sited by the yard rules (ClusterPlacer.Outbuilding), not scored here.
            S(LotKind.Outbuilding, 0.3f, 0.36f, 0.26f, 0.32f);
            return a;
        }

        /// <summary>The ideal lot water distance per kind for the Water term (wet point vs dry point).</summary>
        public static float WaterTerm(LotKind k, float dWater)
        {
            switch (k)
            {
                case LotKind.Watermill:
                case LotKind.Jetty:
                case LotKind.Dyer:
                case LotKind.Kiln:
                    return 1f - Terrain.Clamp01(dWater / 0.6f) * 2f;
                case LotKind.Farm:
                case LotKind.Orchard:
                    return 1f - Terrain.Clamp01(dWater / 1.5f) * 2f;
                default:
                    // Houses: near water is good (wet point) but not on its edge (dry point).
                    if (dWater < 0.3f) return -0.5f;
                    if (dWater < 1.2f) return 1f;
                    return 1f - Terrain.Clamp01((dWater - 1.2f) / 1.5f) * 1.5f;
            }
        }
    }
}
