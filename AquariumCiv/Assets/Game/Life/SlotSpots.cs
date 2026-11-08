using System;

namespace AquariumCiv.Life
{
    /// <summary>
    /// Where the n-th person at a place stands, pure (no engine types): so the tavern and the doorsteps don't
    /// pile people on one spot (the tester's "9 dolls within 0.12 u at the tavern, 3-4 on one spot at homes", 2026-10-30).
    /// The view numbers the slots (rule 6: one doll per slot) and asks here for the offset from the place's spot.
    /// Slot 0 stands on the spot; the rest fill rings round it, each ring's people at least <see cref="Gap"/> apart.
    /// </summary>
    public static class SlotSpots
    {
        /// <summary>The least distance between two people at one place (a doll is about 0.1 wide).</summary>
        public const float Gap = 0.12f;

        /// <summary>At a door: a fan opening away from the building, so nobody stands in the wall.</summary>
        public static (float dx, float dz) Door(int slot, float outAngle)
        {
            const float span = 2.83f; // ~162 degrees
            return Ring(slot, outAngle, span);
        }

        /// <summary>#200, tester run 9: where a home's people stand. A whole household knotted at the door read as a queue, and an axe against the wall as
        /// building the house: the door holds <see cref="AtDoor"/>, wood is split out to one side, washing hangs on the other, and the rest are out in front.</summary>
        public enum HomeSpot { Door, Block, Line, Front }

        public const int AtDoor = 2;
        /// <summary>Nearer than this to a door is standing at it.</summary>
        public const float AtDoorReach = 0.2f;
        /// <summary>The block and the line: along the wall from the door, and out from it. Then the front: how far out, and the fan's step (radians).</summary>
        public const float YardSide = 0.3f, YardOut = 0.1f, FrontOut = 0.36f, FrontStep = 0.75f;

        public static HomeSpot SpotFor(ActivityKind k) => k switch
        {
            ActivityKind.ChopKindling or ActivityKind.StackWood => HomeSpot.Block,
            ActivityKind.HangWashing => HomeSpot.Line,
            ActivityKind.Play or ActivityKind.Toddle or ActivityKind.Snowball => HomeSpot.Front,
            _ => HomeSpot.Door,
        };

        /// <summary>The <paramref name="rank"/>th person at this kind of spot at one home, from the door: the first two of <see cref="HomeSpot.Door"/> at it, anyone after them out in front.</summary>
        /// <param name="blockSide">Verify run 2026-11-07 (kindling split in the gap between two houses, under their roofs): which side
        /// of the door the block stands, +1 as drawn, -1 swapped with the line (a neighbour's house on that side), 0 out in front (both sides shut).</param>
        public static (float dx, float dz) Home(HomeSpot spot, int rank, float outAngle, int blockSide = 1)
        {
            float ox = (float)Math.Cos(outAngle), oz = (float)Math.Sin(outAngle), sx = -oz, sz = ox;
            if (spot == HomeSpot.Block && blockSide == 0) spot = HomeSpot.Front;
            switch (spot)
            {
                case HomeSpot.Door when rank < AtDoor: return Door(rank, outAngle);
                case HomeSpot.Block:
                case HomeSpot.Line:
                {
                    float side = (spot == HomeSpot.Block ? YardSide : -YardSide) * (blockSide < 0 ? -1f : 1f), o = YardOut + rank * Gap;
                    return (ox * o + sx * side, oz * o + sz * side);
                }
            }
            if (spot == HomeSpot.Door) rank -= AtDoor;
            float a = outAngle + ((rank + 1) / 2) * ((rank & 1) == 1 ? FrontStep : -FrontStep), r = FrontOut + (rank / 4) * Gap;
            return ((float)Math.Cos(a) * r, (float)Math.Sin(a) * r);
        }

        /// <summary>On open ground (the pond's edge, a doorstep with no building): rings all the way round.</summary>
        public static (float dx, float dz) Open(int slot, float startAngle) => Ring(slot, startAngle, 2f * (float)Math.PI);

        /// <summary>Slot -> (ring, index); each ring is <see cref="Gap"/> further out and holds as many as fit its arc.</summary>
        static (float dx, float dz) Ring(int slot, float centreAngle, float span)
        {
            if (slot <= 0) return (0f, 0f);
            int ring = 1, first = 1; // slot 0 is the centre
            while (true)
            {
                int cap = Capacity(ring, span);
                if (slot < first + cap) break;
                first += cap; ring++;
            }
            int cap2 = Capacity(ring, span), idx = slot - first;
            float r = Gap * ring;
            bool full = span >= 6.2f;
            float ang;
            if (full) ang = centreAngle + (idx + (ring & 1) * 0.5f) * span / cap2;
            else
            {
                // A fan centred on the way out: the ring's people spread evenly across it.
                float step = cap2 > 1 ? span / (cap2 - 1) : 0f;
                ang = centreAngle + (idx - (cap2 - 1) * 0.5f) * step;
            }
            return ((float)Math.Cos(ang) * r, (float)Math.Sin(ang) * r);
        }

        static int Capacity(int ring, float span)
        {
            float arc = Gap * ring * span; // the arc length at this ring
            return Math.Max(1, (int)Math.Floor(arc / Gap + 1e-3f) + (span >= 6.2f ? 0 : 1));
        }
    }
}
