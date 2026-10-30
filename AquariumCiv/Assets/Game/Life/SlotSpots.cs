using System;

namespace AquariumCiv.Life
{
    /// <summary>
    /// Where the n-th person at a place stands, pure (no engine types): so the tavern, the doorsteps and the heaps don't
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

        /// <summary>On open ground (a heap, the pond's edge, a doorstep with no building): rings all the way round.</summary>
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
