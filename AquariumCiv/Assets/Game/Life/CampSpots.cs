using System;
using System.Collections.Generic;

namespace AquariumCiv.Life
{
    /// <summary>
    /// fb4 #540: where the things round a camp's fire stand so they never overlap. Pure geometry (x/z in world units, angle as the
    /// stash uses it: x = cos, z = sin from the fire). The fixed things: the cook's pot (<c>MealDirector.PotOffset</c>), the crumb's
    /// flat stone (fire + (0.34, 0.1)), the stash stand and pile (<see cref="CampStash"/>), the ring of seats (0.3) and the hide rack
    /// (<see cref="LifePlanner.HideFrameDistance"/> out, a bar across the line from the fire). The rack's side is the seed's, nudged off
    /// the stash and the stone; the Keeper's stew pot (a prop the sim drops somewhere in a ring) is moved to the nearest free spot.
    /// </summary>
    public static class CampSpots
    {
        public struct Disc { public float x, z, r; public Disc(float x, float z, float r) { this.x = x; this.z = z; this.r = r; } }

        /// <summary>Mirrors Pieces.RackHalfLength(2) and Pieces.RackHalfDepth (a view test pins them).</summary>
        public const float RackHalfLength = 0.22f, RackHalfDepth = 0.2f;
        /// <summary>The room a prop of the Keeper's takes (bigger than life: 1.5x), and the nearest one stands to the fire (clear of the ring of seats).</summary>
        public const float PropRadius = 0.16f, MinFireDistance = 0.55f;
        /// <summary>Mirrors MealDirector.PotOffset, and the room round the pot for the cook.</summary>
        public const float PotX = 0.10f, PotZ = -0.09f, PotRoom = 0.18f;
        /// <summary>The crumb's flat stone (RelicProps: fire + (0.34, 0.1)).</summary>
        public const float StoneX = 0.34f, StoneZ = 0.10f, StoneRoom = 0.14f;

        /// <summary>The fixed things by the fire, as discs (the rack is separate: <see cref="DistanceToRack"/>).</summary>
        public static void FireSide(float fx, float fz, List<Disc> into)
        {
            into.Add(new Disc(fx + PotX, fz + PotZ, PotRoom));
            into.Add(new Disc(fx + StoneX, fz + StoneZ, StoneRoom));
            CampStash.At(fx, fz, CampStash.StandRadius, out float sx, out float sz);
            into.Add(new Disc(sx, sz, 0.12f));
            CampStash.At(fx, fz, CampStash.PileRadius, out float px, out float pz);
            into.Add(new Disc(px, pz, CampStash.PileExtent + 0.06f));
        }

        /// <summary>Distance from a point to the rack's footprint (0 inside it). The rack stands <paramref name="dist"/> from the fire at
        /// <paramref name="angle"/>, its bar across that line.</summary>
        public static float DistanceToRack(float fx, float fz, float angle, float dist, float px, float pz)
        {
            float ux = (float)Math.Cos(angle), uz = (float)Math.Sin(angle);
            float rx = px - fx - ux * dist, rz = pz - fz - uz * dist;
            float across = rx * ux + rz * uz;          // along the line from the fire (the rack's depth)
            float along = rx * -uz + rz * ux;          // along the bar
            float dx = Math.Max(0f, Math.Abs(across) - RackHalfDepth), dz = Math.Max(0f, Math.Abs(along) - (RackHalfLength + 0.035f));
            return (float)Math.Sqrt(dx * dx + dz * dz);
        }

        static bool RackClearOfDiscs(float fx, float fz, float angle, float dist, List<Disc> discs)
        {
            foreach (var d in discs)
                if (DistanceToRack(fx, fz, angle, dist, d.x, d.z) < d.r) return false;
            return true;
        }

        /// <summary>The rack's angle: the seed's (<paramref name="seedAngle"/>), or the nearest one to it that leaves the stash, the stone
        /// and the pot clear (scans both ways in 5 degree steps).</summary>
        public static float RackAngle(float fx, float fz, float seedAngle, float dist)
        {
            var discs = new List<Disc>(); FireSide(fx, fz, discs);
            const float step = 5f * (float)(Math.PI / 180.0);
            for (int k = 0; k <= 36; k++)
                foreach (int s in new[] { 1, -1 })
                {
                    float a = seedAngle + s * k * step;
                    if (RackClearOfDiscs(fx, fz, a, dist, discs)) return a;
                    if (k == 0) break;
                }
            return seedAngle;
        }

        /// <summary>Is a prop of the Keeper's at (x, z) clear of the fire, the seats, the fixed things, the rack and the extra discs
        /// (tents, other props)?</summary>
        public static bool Clear(float fx, float fz, float rackAngle, float rackDist, float x, float z, List<Disc> fixedDiscs, List<Disc> extra)
        {
            float dx = x - fx, dz = z - fz;
            if (dx * dx + dz * dz < MinFireDistance * MinFireDistance) return false;
            if (DistanceToRack(fx, fz, rackAngle, rackDist, x, z) < PropRadius) return false;
            foreach (var d in fixedDiscs) if (Dist(x, z, d.x, d.z) < d.r + PropRadius * 0.6f) return false;
            if (extra != null) foreach (var d in extra) if (Dist(x, z, d.x, d.z) < d.r + PropRadius) return false;
            return true;
        }

        static float Dist(float ax, float az, float bx, float bz) => (float)Math.Sqrt((ax - bx) * (ax - bx) + (az - bz) * (az - bz));

        /// <summary>The spot for a Keeper's prop the sim left at (<paramref name="wantX"/>, <paramref name="wantZ"/>): there if that is
        /// clear, else the nearest clear one on rings round the fire (24 angles). Same answer for the same inputs.</summary>
        public static void Spot(float fx, float fz, float rackAngle, float rackDist, float wantX, float wantZ, List<Disc> extra, out float x, out float z)
        {
            var fixedDiscs = new List<Disc>(); FireSide(fx, fz, fixedDiscs);
            x = wantX; z = wantZ;
            if (Clear(fx, fz, rackAngle, rackDist, wantX, wantZ, fixedDiscs, extra)) return;
            float best = float.MaxValue;
            foreach (float r in new[] { 0.6f, 0.72f, 0.84f, 0.96f })
                for (int i = 0; i < 24; i++)
                {
                    float a = i / 24f * 6.2831853f;
                    float cx = fx + (float)Math.Cos(a) * r, cz = fz + (float)Math.Sin(a) * r;
                    if (!Clear(fx, fz, rackAngle, rackDist, cx, cz, fixedDiscs, extra)) continue;
                    float d = Dist(cx, cz, wantX, wantZ);
                    if (d < best) { best = d; x = cx; z = cz; }
                }
        }
    }
}
