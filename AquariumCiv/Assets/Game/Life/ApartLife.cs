using System;
using AquariumCiv.Sim;
using AquariumCiv.Sim.Settle;

namespace AquariumCiv.Life
{
    /// <summary>What someone who lives apart (#231) is doing at an hour of the view day.</summary>
    public enum ApartDoing { Sleep, SitAtFire, Fish, Chop, Wander, Weave }

    /// <summary>#578: the beats of one wood carry (the travellers'): chop at the cutting spot, lift a log, walk it to the camp's pile, set it down.</summary>
    public enum CarryPhase { Chop, Lift, Walk, Drop }

    /// <summary>
    /// The little life of an apart group (#231 phase 2; the sim's <see cref="ApartGroup"/>): where its camp stands now and what its people do
    /// through the view day. Pure and view-side, read-only on the sim. The camp keeps clear of the settlement: never on a lot, a lot's ring,
    /// a path or trail, the green, a yard or the water; when the settlement grows over the spot, the camp is drawn at the nearest free one.
    /// </summary>
    public static class ApartLife
    {
        /// <summary>Box units: the camp's reach (fire, shelter, a rack or a boat, and the people round them) that must stay clear.</summary>
        public const float Footprint = ApartSpots.Footprint;
        /// <summary>Box units: a nudged camp keeps this far from the glass, so it isn't lost behind the side panes and the frame.</summary>
        public const float GlassMargin = ApartSpots.GlassMargin;
        /// <summary>Box units: a nudged camp looks first this far or more from the settlement's green (apart, not its outskirts).</summary>
        public const float AwayFromGreen = ApartSpots.AwayFromGreen;
        /// <summary>Box units: the seats round the fire.</summary>
        public const float SeatRadius = 0.2f;
        /// <summary>Hours of the view day: up at, at the fire from, in bed at.</summary>
        public const float WakeHour = 6f, EveningHour = 17f, BedHour = 22f;

        /// <summary>A cell the camp may cover (<see cref="ApartSpots.CellFree"/>).</summary>
        public static bool CellFree(SiteMap map, int c) => ApartSpots.CellFree(map, c);
        public const float Clearing = ApartSpots.Clearing, FrontView = ApartSpots.FrontView, ShoreReach = ApartSpots.ShoreReach;
        /// <summary>A camp can stand at (x, z) (<see cref="ApartSpots.FootprintFree"/>).</summary>
        public static bool FootprintFree(SiteMap map, float x, float z, float r = Footprint, bool waterOk = false) => ApartSpots.FootprintFree(map, x, z, r, waterOk);
        /// <summary>Where the camp is drawn (<see cref="ApartSpots.FreeSpot"/>).</summary>
        public static (float x, float z) FreeSpot(SiteMap map, float x, float z, out bool moved, float r = Footprint, bool shore = false, bool[] avoid = null, float avoidR = Footprint) => ApartSpots.FreeSpot(map, x, z, out moved, r, shore, avoid, avoidR);
        /// <summary>The way to the nearest water within reach (<see cref="ApartSpots.WaterWay"/>).</summary>
        public static (float x, float z) WaterWay(SiteMap map, float x, float z, float reach = 0.8f) => ApartSpots.WaterWay(map, x, z, reach);

        /// <summary>What member <paramref name="member"/> of a group of <paramref name="kind"/> does at <paramref name="hour"/>: asleep in the
        /// shelter at night, at the fire in the morning and from the evening (their fire glows at dusk), and in the day the first works (fishing
        /// at a shore hut, chopping at a woods camp), the second wanders a little, the third weaves by the shelter. #421: with
        /// <paramref name="fishing"/> false (SimCore.FishingOpen: no fish placed yet) the shore hut's first wanders by the water instead.</summary>
        public static ApartDoing Doing(string kind, int member, float hour, bool fishing = true)
        {
            hour = ((hour % 24f) + 24f) % 24f;
            if (hour < WakeHour || hour >= BedHour) return ApartDoing.Sleep;
            if (hour < WakeHour + 2f || hour >= EveningHour) return ApartDoing.SitAtFire;
            // a short break at the fire at midday, so the day has more than one beat
            if (hour >= 12.5f && hour < 13.5f) return ApartDoing.SitAtFire;
            switch (member % 3)
            {
                case 0: return kind == ApartKind.ShoreHut ? (fishing ? ApartDoing.Fish : ApartDoing.Wander) : ApartDoing.Chop;
                case 1: return ApartDoing.Wander;
                default: return ApartDoing.Weave;
            }
        }

        /// <summary>Real seconds between two carries by the camp's cutter at the usual pace (#578). The wood lean (x1.5) shortens it to two thirds.</summary>
        public const float CarrySeconds = 45f;
        /// <summary>The share of one carry cycle (0..1) at which each beat starts: Lift, Walk, Drop.</summary>
        public const float LiftAt = 0.6f, WalkAt = 0.66f, DropAt = 0.92f;

        /// <summary>The beat at <paramref name="cycle"/> (0..1, wrapping) of a carry. In the Walk beat <paramref name="walk"/> runs 0..1 (the pile is at 1).</summary>
        public static CarryPhase CarryAt(float cycle, out float walk)
        {
            cycle -= (float)Math.Floor(cycle);
            walk = cycle < WalkAt ? 0f : cycle >= DropAt ? 1f : (cycle - WalkAt) / (DropAt - WalkAt);
            return cycle < LiftAt ? CarryPhase.Chop : cycle < WalkAt ? CarryPhase.Lift : cycle < DropAt ? CarryPhase.Walk : CarryPhase.Drop;
        }

        /// <summary>Real seconds one carry takes at <paramref name="woodRate"/> (the sim's <c>ApartWoodRate</c>: 1 usually, 1.5 while the wood lean lasts).</summary>
        public static float CarryPeriod(float woodRate) => CarrySeconds / Math.Max(0.1f, woodRate);

        /// <summary>How many logs a pile of <paramref name="loads"/> loads shows (a log a load, up to a full stack).</summary>
        public static int PileLogs(int loads) => Math.Max(0, Math.Min(MaxPileLogs, loads));
        public const int MaxPileLogs = 12;

        /// <summary>A seat round the fire (box units), the members spread evenly, the first on the side away from <paramref name="back"/>.</summary>
        public static (float x, float z) Seat((float x, float z) fire, (float x, float z) back, int member, int count)
        {
            float a0 = (float)Math.Atan2(-back.z, -back.x);
            float a = a0 + (member - (Math.Max(1, count) - 1) * 0.5f) * 1.25f;
            return (fire.x + (float)Math.Cos(a) * SeatRadius, fire.z + (float)Math.Sin(a) * SeatRadius);
        }

        /// <summary>Where a wanderer heads at this hour: a new point round the camp every half hour of the view day, inside the footprint and a
        /// little past it.</summary>
        public static (float x, float z) WanderPoint((float x, float z) camp, int member, float hour, int seed)
        {
            int step = (int)Math.Floor(hour * 2f);
            float a = (float)(Hash(seed, member, step) * Math.PI * 2.0);
            float r = 0.18f + 0.22f * (float)Hash(seed + 7, member, step);
            return (camp.x + (float)Math.Cos(a) * r, camp.z + (float)Math.Sin(a) * r);
        }

        static double Hash(int a, int b, int c)
        {
            unchecked
            {
                uint h = (uint)a * 0x9E3779B1u ^ (uint)b * 0x85EBCA77u ^ (uint)c * 0xC2B2AE3Du ^ 0x27D4EB2Fu;
                h ^= h >> 16; h *= 0x7FEB352Du; h ^= h >> 15; h *= 0x846CA68Bu; h ^= h >> 16;
                return (h >> 8) / 16777216.0;
            }
        }
    }
}
