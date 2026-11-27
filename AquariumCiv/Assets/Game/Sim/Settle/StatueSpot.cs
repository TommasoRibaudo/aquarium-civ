using System;
using System.Collections.Generic;

namespace AquariumCiv.Sim.Settle
{
    /// <summary>
    /// #776 (animation-specs B81.0 "Where"): where the founder's statue stands on the green. Pure, on the site map, deterministic: the same
    /// settlement gives the same spot every load (the sim saves only <see cref="SimState.statueFor"/> and <see cref="SimState.statueMonth"/>).
    /// The ring 0.4 to 0.9 u from the green's centre, the side that shows from the default camera first; a footprint of 0.14 u kept clear 0.2 u all
    /// round (no path, water, lot, yard, rock, trees or reserved ground in it), and 0.25 u from every point in <c>keepClear</c> (the well, the fire,
    /// the doors, the name stone, the Keeper's post). With no cell on the ring: the nearest open ground within 1.4 u of the centre.
    /// #798: and <see cref="FenceRoom"/> from every fenced field or pasture (its fence and its corners), so the plinth and the people round it
    /// are not pressed against a fence; only when no spot within reach has that room does the old rule stand.
    /// </summary>
    public static class StatueSpot
    {
        /// <summary>Box units: the figure's own footprint (a square), the clear margin round it, the distance kept from the listed things.</summary>
        public const float Footprint = 0.14f, Clear = 0.2f, KeepFrom = 0.25f;
        /// <summary>The ring on the green (from its centre) and how far the fallback looks [est].</summary>
        public const float RingNear = 0.4f, RingFar = 0.9f, FallbackReach = 1.4f;
        /// <summary>#798: the room kept between the statue's centre and a fence: the family's half ring stands 0.45 u out (StatueScenes.FamilyR)
        /// and a doll is about 0.11 u across, so the whole ring stays on open ground. The fence runs 0.03 u inside its lot's edge (SimLayout.Corners).</summary>
        public const float FenceRoom = 0.51f, FenceInset = 0.03f;

        /// <summary>A fence round a lot: its centre, half width and half depth (to the fence line) and yaw (degrees, as <see cref="Lot.yaw"/>).</summary>
        public struct Fence
        {
            public float x, z, hw, hd, yaw;
            public static Fence Round(Lot l) => new Fence { x = l.x, z = l.z, hw = l.w * 0.5f - FenceInset, hd = l.d * 0.5f - FenceInset, yaw = l.yaw };

            /// <summary>The distance from (px, pz) to the fence's rectangle (0 inside it): its sides and its corners.</summary>
            public float DistanceTo(float px, float pz)
            {
                float r = yaw * (float)(Math.PI / 180.0), c = (float)Math.Cos(r), s = (float)Math.Sin(r);
                float dx = px - x, dz = pz - z;
                float lx = dx * c - dz * s, lz = dx * s + dz * c; // into the lot's own frame (the inverse of SimLayout.Corners)
                float ox = Math.Max(0f, Math.Abs(lx) - hw), oz = Math.Max(0f, Math.Abs(lz) - hd);
                return (float)Math.Sqrt(ox * ox + oz * oz);
            }
        }

        /// <summary>The fenced lots standing now (fields once the camp is a village, pastures): the fences the statue keeps <see cref="FenceRoom"/> from.</summary>
        public static List<Fence> FencesOf(IEnumerable<Lot> lots, bool camp)
        {
            var f = new List<Fence>();
            if (lots == null) return f;
            foreach (var l in lots)
                if (!l.retired && (l.kind == LotKind.Pasture || l.kind == LotKind.Farm && !camp)) f.Add(Fence.Round(l));
            return f;
        }

        /// <summary>The way the default camera looks in from, as a unit vector from the green (yaw 28 deg: BoxCameraMath puts the camera at -sin, -cos).</summary>
        public const float FrontX = -0.4695f, FrontZ = -0.8829f;

        /// <summary>A cell the statue's footprint or its clear margin may cover: on the land, dry, no path (a walked path or more), no lot or yard
        /// ring, no laid ground, yard, rock or wood, nothing reserved. The green's own cells are fine: the statue stands on the green.</summary>
        public static bool OpenCell(SiteMap map, int c)
        {
            if (c < 0) return false;
            int i = SiteMap.I(c), j = SiteMap.J(c);
            if (i < 2 || j < 2 || i >= SiteMap.NX - 2 || j >= SiteMap.NZ - 2) return false;
            if (map.Water(c) || map.Reserved(c) || map.Forest(c)) return false;
            if (map.tier != null && map.tier[c] >= PathNet.PathTier) return false;
            if (map.lot != null && map.lot[c] != 0) return false;
            if (map.ring != null && map.ring[c] != 0) return false;
            const byte block = SiteMap.FLaid | SiteMap.FYard | SiteMap.FRock | SiteMap.FCanal;
            return (map.flags[c] & block) == 0;
        }

        /// <summary>The statue could stand at (x, z): every cell within its footprint and the clear margin is open, and nothing in
        /// <paramref name="keepClear"/> is within <see cref="KeepFrom"/>.</summary>
        public static bool Fits(SiteMap map, float x, float z, IList<(float x, float z)> keepClear, IList<Fence> fences = null)
        {
            float reach = Footprint * 0.7071f + Clear; // the footprint's corner plus the margin
            int n = (int)Math.Ceiling(reach / SiteMap.Cell);
            for (int dj = -n; dj <= n; dj++)
                for (int di = -n; di <= n; di++)
                {
                    float px = x + di * SiteMap.Cell, pz = z + dj * SiteMap.Cell;
                    float d2 = (px - x) * (px - x) + (pz - z) * (pz - z);
                    if (d2 > reach * reach + 1e-6f) continue;
                    if (!OpenCell(map, SiteMap.CellAt(px, pz))) return false;
                }
            if (keepClear != null)
                foreach (var k in keepClear)
                    if ((k.x - x) * (k.x - x) + (k.z - z) * (k.z - z) < KeepFrom * KeepFrom) return false;
            if (fences != null)
                foreach (var f in fences)
                    if (f.DistanceTo(x, z) < FenceRoom) return false;
            return true;
        }

        /// <summary>Where it stands: false when there is no open ground within reach (then the people raise nothing).</summary>
        public static bool Find(SiteMap map, IList<(float x, float z)> keepClear, out float x, out float z, IList<Fence> fences = null)
        {
            x = z = 0f;
            if (map == null) return false;
            // #798: room from the fences first; a green hemmed in by fields keeps the old rule rather than raise nothing
            if (fences != null && fences.Count > 0 && FindIn(map, keepClear, fences, out x, out z)) return true;
            return FindIn(map, keepClear, null, out x, out z);
        }

        static bool FindIn(SiteMap map, IList<(float x, float z)> keepClear, IList<Fence> fences, out float x, out float z)
        {
            x = z = 0f;
            float gx = map.greenX, gz = map.greenZ;
            // the ring: radii near to far, every 15 degrees; the side toward the camera scores best, then the nearer ring
            float bestScore = float.MinValue; bool found = false;
            for (float r = RingNear; r <= RingFar + 1e-4f; r += 0.1f)
                for (int a = 0; a < 24; a++)
                {
                    float ang = a * (float)(Math.PI / 12.0);
                    float ux = (float)Math.Cos(ang), uz = (float)Math.Sin(ang);
                    float px = Snap(gx + ux * r), pz = Snap(gz + uz * r);
                    float score = ux * FrontX + uz * FrontZ - (r - RingNear) * 0.3f; // 1 straight toward the camera
                    if (score <= bestScore + 1e-5f) continue; // ties: the first tried (the nearer ring, the lower angle)
                    if (!Fits(map, px, pz, keepClear, fences)) continue;
                    bestScore = score; x = px; z = pz; found = true;
                }
            if (found) return true;
            // the fallback: the nearest open cell centre within reach of the green's centre
            float best = float.MaxValue;
            for (int c = 0; c < SiteMap.N; c++)
            {
                float px = SiteMap.CXc(c), pz = SiteMap.CZc(c);
                float d = (px - gx) * (px - gx) + (pz - gz) * (pz - gz);
                if (d >= best || d > FallbackReach * FallbackReach) continue;
                if (!Fits(map, px, pz, keepClear, fences)) continue;
                best = d; x = px; z = pz; found = true;
            }
            return found;
        }

        /// <summary>The spot snapped to the middle of its cell, so it never depends on float rounding of the ring's angles.</summary>
        static float Snap(float v) => (float)Math.Round(v / SiteMap.Cell - 0.5) * SiteMap.Cell + SiteMap.Cell * 0.5f;

        /// <summary>The way the statue faces: toward the green's centre; when that points away from the front glass (the camera looks in from
        /// the front), turned to 45 deg toward it so its face still shows. Degrees yaw (0 = facing +z, as lots).</summary>
        public static float FaceYaw(float x, float z, float gx, float gz)
        {
            float dx = gx - x, dz = gz - z;
            float len = (float)Math.Sqrt(dx * dx + dz * dz);
            if (len < 1e-4f) { dx = FrontX; dz = FrontZ; len = 1f; }
            dx /= len; dz /= len;
            if (dx * FrontX + dz * FrontZ >= 0f) return (float)(Math.Atan2(dx, dz) * 180.0 / Math.PI);
            // faces away from the camera: turn toward it, 45 deg off the front on the side it was already facing
            float front = (float)(Math.Atan2(FrontX, FrontZ) * 180.0 / Math.PI);
            float face = (float)(Math.Atan2(dx, dz) * 180.0 / Math.PI);
            float diff = face - front; while (diff > 180f) diff -= 360f; while (diff < -180f) diff += 360f;
            return front + (diff >= 0f ? 45f : -45f);
        }
    }
}
