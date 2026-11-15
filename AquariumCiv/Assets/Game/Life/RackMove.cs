using System;
using System.Collections.Generic;

namespace AquariumCiv.Life
{
    public enum RackKind : byte { Fish, Hide }
    public enum RackRole : byte { Lifter, Carrier }
    public enum RackPhase : byte { Antic, TakeCargo, LiftRidge, PullFrames, Carry, Plant, Raise, ReHang, End, Done }

    /// <summary>One thing the rack beat puts up in a puff (the view fires it once as the beat's clock passes it).</summary>
    public readonly struct RackEvent
    {
        public readonly float t, x, z;
        public readonly bool dust, grass;
        public RackEvent(float t, float x, float z, bool dust, bool grass) { this.t = t; this.x = x; this.z = z; this.dust = dust; this.grass = grass; }
    }

    /// <summary>One part of the rack as the beat draws it: where it is (x/z on the ground, y up), the way its bar runs (dirX/dirZ), a tip
    /// about the bar's cross axis (degrees), a wobble (degrees, a settling frame) and a small jolt (y offset).</summary>
    public struct RackPart
    {
        public bool shown;
        public float x, y, z, dirX, dirZ, tilt, wobble, jolt;
    }

    /// <summary>The rack at one moment of the beat: its three parts, the cargo on it at the old and the new spot, and the holes left.</summary>
    public struct RackLook
    {
        public RackPart ridge, frameA, frameB;
        /// <summary>The whole rack stands at the old spot with this much cargo on it (-1 = it is not standing there whole).</summary>
        public int oldWhole;
        /// <summary>The whole rack stands at the new spot with this much cargo on it (-1 = not whole).</summary>
        public int newWhole;
        /// <summary>The frames' holes at the old spot show (they stay for the rest of the day).</summary>
        public bool holes;
    }

    /// <summary>
    /// #700 (B27): a path has grown over a rack and the people take it down and set it up again. Pure: the plan, its clock and its geometry, so
    /// AnimBench and the tests run it. Times are real seconds from the beat's start (<see cref="Total"/> about 22 s with a loaded rack):
    /// antic 2.0, the cargo comes off (0.4 s each, at most 2.0), the ridge pole lifts 0.7, the frames are pulled 3.0, the carry (3-8 s by
    /// distance), the plant 4.0, the raise 3.0, the cargo goes back on, the end beat 2.2. The Lifter carries the ridge pole and the basket;
    /// the Carrier pulls and plants both frames. The rack's angle round the fire moves to <see cref="newAngle"/> at <see cref="CommitAt"/>.
    /// </summary>
    public sealed class RackMovePlan
    {
        public const float AnticSeconds = 2.0f, LiftSeconds = 0.7f, PullSeconds = 3.0f, PlantSeconds = 4.0f, RaiseSeconds = 3.0f, EndSeconds = 2.2f;
        public const float CarrySpeed = 0.25f, CarryMin = 3f, CarryMax = 8f, CargoEach = 0.4f, CargoMost = 2.0f;
        /// <summary>How far in front of the rack's line (fire side) the people stand, and the Lifter's extra step back.</summary>
        public const float StandIn = 0.12f, LifterIn = 0.2f;
        /// <summary>The carry starts 0.3 s after the Lifter for the Carrier (de-sync, B27).</summary>
        public const float CarrierDelay = 0.3f;

        public readonly RackKind kind;
        public readonly float fx, fz, oldAngle, newAngle, dist, half, height;
        /// <summary>What is on the rack at the start (fish or hides), and how many of them are the fresh (silver) fish.</summary>
        public readonly int cargo, fresh;
        public readonly float cargoSeconds, carrySeconds, hangSeconds;
        public readonly float tTake, tLift, tPull, tCarry, tPlant, tRaise, tHang, total;
        public readonly float arc, rOldL, rNewL;

        public RackMovePlan(RackKind kind, float fx, float fz, float oldAngle, float newAngle, float dist, float half, float height, int cargo, int fresh)
        {
            this.kind = kind; this.fx = fx; this.fz = fz; this.oldAngle = oldAngle; this.newAngle = newAngle; this.dist = dist; this.half = half; this.height = height;
            this.cargo = Math.Max(0, cargo); this.fresh = Math.Max(0, Math.Min(fresh, this.cargo));
            cargoSeconds = this.cargo == 0 ? 0f : Math.Min(CargoMost, CargoEach * this.cargo);
            hangSeconds = this.cargo == 0 ? 0f : cargoSeconds + 0.4f;
            arc = AngleDelta(oldAngle, newAngle);
            float walk = Math.Abs(arc) * (dist - StandIn);
            carrySeconds = Math.Max(CarryMin, Math.Min(CarryMax, walk / CarrySpeed));
            tTake = AnticSeconds + cargoSeconds;
            tLift = tTake + LiftSeconds;
            tPull = tLift + PullSeconds;
            tCarry = tPull + carrySeconds;
            tPlant = tCarry + PlantSeconds;
            tRaise = tPlant + RaiseSeconds;
            tHang = tRaise + hangSeconds;
            total = tHang + EndSeconds;
        }

        /// <summary>The plan for a rack of <paramref name="kind"/> standing at <paramref name="oldAngle"/> round the fire (<paramref name="fx"/>, <paramref name="fz"/>)
        /// that moves to <paramref name="newAngle"/>, with <paramref name="cargo"/> fish (<paramref name="fresh"/> of them silver) or hides on it. The rack's size
        /// is its kind's (<c>FishRackSpot</c>; the hide rack is 0.22 half-long with two hides on it, 0.13 with fewer, as <c>Pieces.RackHalfLength</c>).</summary>
        public static RackMovePlan Make(RackKind kind, float fx, float fz, float oldAngle, float newAngle, int cargo, int fresh)
            => kind == RackKind.Fish
                ? new RackMovePlan(kind, fx, fz, oldAngle, newAngle, FishRackSpot.Distance, FishRackSpot.HalfLength, 0.19f, cargo, fresh)
                : new RackMovePlan(kind, fx, fz, oldAngle, newAngle, LifePlanner.HideFrameDistance, cargo >= 2 ? 0.22f : 0.13f, 0.22f, cargo, fresh);

        /// <summary>Signed shortest turn from <paramref name="a"/> to <paramref name="b"/> (radians, -pi to pi).</summary>
        public static float AngleDelta(float a, float b)
        {
            double d = (b - a) % (Math.PI * 2.0);
            if (d > Math.PI) d -= Math.PI * 2.0;
            if (d < -Math.PI) d += Math.PI * 2.0;
            return (float)d;
        }

        /// <summary>When the committed angle moves (the Plant begins): <see cref="RackSites"/> then answers the new angle.</summary>
        public float CommitAt => tCarry;

        public RackPhase PhaseAt(float t)
        {
            if (t < AnticSeconds) return RackPhase.Antic;
            if (t < tTake) return RackPhase.TakeCargo;
            if (t < tLift) return RackPhase.LiftRidge;
            if (t < tPull) return RackPhase.PullFrames;
            if (t < tCarry) return RackPhase.Carry;
            if (t < tPlant) return RackPhase.Plant;
            if (t < tRaise) return RackPhase.Raise;
            if (t < tHang) return RackPhase.ReHang;
            if (t < total) return RackPhase.End;
            return RackPhase.Done;
        }

        // ---------------------------------------------------------------- the rack's frames (old and new)
        public void Centre(bool atNew, out float x, out float z)
        {
            float a = atNew ? newAngle : oldAngle;
            x = fx + (float)Math.Cos(a) * dist; z = fz + (float)Math.Sin(a) * dist;
        }

        /// <summary>The unit way along the bar, and the unit way from the rack towards the fire.</summary>
        public void Axes(bool atNew, out float dx, out float dz, out float ix, out float iz)
        {
            float a = atNew ? newAngle : oldAngle;
            float ux = (float)Math.Cos(a), uz = (float)Math.Sin(a);
            dx = -uz; dz = ux; ix = -ux; iz = -uz;
        }

        /// <summary>A point on the fire side of a rack: <paramref name="along"/> along the bar from its middle, <paramref name="inward"/> towards the fire.</summary>
        public void Spot(bool atNew, float along, float inward, out float x, out float z)
        {
            Centre(atNew, out float cx, out float cz); Axes(atNew, out float dx, out float dz, out float ix, out float iz);
            x = cx + dx * along + ix * inward; z = cz + dz * along + iz * inward;
        }

        /// <summary>Where the frame <paramref name="a"/> (the -bar end) or B (the +bar end) stands, on the ground.</summary>
        public void FrameAt(bool atNew, bool frameB, out float x, out float z) => Spot(atNew, frameB ? half : -half, 0f, out x, out z);

        // ---------------------------------------------------------------- the people
        // Stands (along, inward), per role and spot. The Lifter keeps back near the middle; the Carrier works along the bar's ends.
        float LifterAlong => -0.05f;

        /// <summary>Where a person stands at <paramref name="t"/> (clip seconds into the beat), what they face, and whether their legs are walking (a glide or the carry).</summary>
        public void Stand(RackRole role, float t, out float x, out float z, out float faceX, out float faceZ, out bool walking)
        {
            walking = false;
            var ph = PhaseAt(t);
            bool lifter = role == RackRole.Lifter;
            // the old spot
            float inOld = lifter ? LifterIn : StandIn;
            float alongOldStart = lifter ? LifterAlong : 0.09f;
            float cdx, cdz; Centre(false, out cdx, out cdz);
            if (t < tLift || lifter && t < tPull)
            {
                Spot(false, alongOldStart, inOld, out x, out z); faceX = cdx; faceZ = cdz;
                return;
            }
            if (!lifter && t < tPull)
            {   // the Carrier: glide to frame B's end, pull it, glide to frame A's end, pull it
                float k = t - tLift;
                float bAlong = half - 0.03f, aAlong = -(half - 0.03f);
                float along;
                if (k < 0.5f) { along = Lerp(alongOldStart, bAlong, Smooth(k / 0.5f)); walking = true; }
                else if (k < 1.4f) along = bAlong;
                else if (k < 2.2f) { along = Lerp(bAlong, aAlong, Smooth((k - 1.4f) / 0.8f)); walking = true; }
                else along = aAlong;
                Spot(false, along, StandIn, out x, out z);
                Spot(false, Lerp(half, -half, Smooth((k - 1.4f) / 0.8f)), 0f, out faceX, out faceZ); // the look follows him from frame B's end to A's
                return;
            }
            // the carry: round the fire from the old stand to the new one, never across it
            float aOldL = StandAngle(false, true, out float rOL), aNewL = StandAngle(true, true, out float rNL);
            float aOldC = StandAngle(false, false, out float rOC), aNewC = StandAngle(true, false, out float rNC);
            if (ph == RackPhase.Carry)
            {
                float dur = carrySeconds - (lifter ? 0f : CarrierDelay);
                float tc = t - tPull - (lifter ? 0f : CarrierDelay);
                float k = Smooth(Clamp01(tc / dur));
                if (lifter) Polar(aOldL, rOL, aNewL, rNL, arc, k, out x, out z);
                else Polar(aOldC, rOC, aNewC, rNC, arc, k, out x, out z);
                walking = tc > 0f && tc < dur;
                // the way he faces: from the old look, round to the way he goes, then to the new one (so what he carries never flips sides)
                float ax, az;
                float k2 = Clamp01(tc / dur) + 0.06f; k2 = Smooth(k2 > 1.2f ? 1.2f : k2);
                if (lifter) Polar(aOldL, rOL, aNewL, rNL, arc, k2, out ax, out az); else Polar(aOldC, rOC, aNewC, rNC, arc, k2, out ax, out az);
                float mx = ax - x, mz = az - z; Norm(ref mx, ref mz, 1f, 0f);
                float prx, prz, pox, poz;
                if (lifter) { Centre(false, out prx, out prz); Centre(true, out pox, out poz); }
                else { Spot(false, -half, 0f, out prx, out prz); Spot(true, half, 0f, out pox, out poz); }
                float ex = prx - x, ez = prz - z; Norm(ref ex, ref ez, mx, mz);
                float fx2 = pox - x, fz2 = poz - z; Norm(ref fx2, ref fz2, mx, mz);
                float w1 = Smooth(tc / 0.6f), w2 = Smooth((tc - (dur - 0.6f)) / 0.6f);
                float hx = Lerp(ex, mx, w1), hz = Lerp(ez, mz, w1);
                hx = Lerp(hx, fx2, w2); hz = Lerp(hz, fz2, w2);
                Norm(ref hx, ref hz, mx, mz);
                faceX = x + hx * 0.2f; faceZ = z + hz * 0.2f;
                return;
            }
            // at the new spot
            Centre(true, out float nx, out float nz);
            if (lifter)
            {
                Spot(true, 0f, LifterIn + 0.04f, out x, out z);
                if (ph == RackPhase.End)
                {   // steps back from the rack with hands on hips
                    float k = Smooth(Clamp01((t - tHang) / 0.6f));
                    Spot(true, 0f, LifterIn + 0.04f + 0.05f * k, out x, out z);
                }
                faceX = nx; faceZ = nz;
                return;
            }
            // Carrier at the new spot
            float bA = half - 0.03f, aA = -(half - 0.03f);
            if (ph == RackPhase.Plant)
            {
                float k = t - tCarry;
                float g = Smooth((k - 1.8f) / 0.8f);
                float along = Lerp(bA, aA, g);
                walking = k >= 1.8f && k < 2.6f;
                Spot(true, along, StandIn, out x, out z);
                Spot(true, Lerp(half, -half, g), 0f, out faceX, out faceZ);
                return;
            }
            Spot(true, aA, StandIn, out x, out z);
            { float g = Smooth((t - tPlant) / 0.5f); Spot(true, Lerp(-half, 0f, g), 0f, out faceX, out faceZ); }
            if (ph == RackPhase.End)
            {   // turns to look back at the old spot and waves
                float k = Clamp01((t - tHang) / 0.5f);
                Centre(false, out float ox, out float oz);
                faceX = Lerp(nx, ox, k); faceZ = Lerp(nz, oz, k);
            }
        }

        /// <summary>The stand's angle and radius round the fire (a person's place at a rack's end, or the Lifter's).</summary>
        float StandAngle(bool atNew, bool lifter, out float r)
        {
            float along = lifter ? (atNew ? -0.03f : -(half - 0.03f) + 0.0f) : (atNew ? half - 0.03f : -(half - 0.03f));
            if (lifter) along = atNew ? 0f : LifterAlong;
            float inward = lifter ? (atNew ? LifterIn + 0.04f : LifterIn) : StandIn;
            Spot(atNew, along, inward, out float x, out float z);
            r = (float)Math.Sqrt((x - fx) * (x - fx) + (z - fz) * (z - fz));
            return (float)Math.Atan2(z - fz, x - fx);
        }

        /// <summary>A point on the way round the fire between two stands (angle by the shorter way, radius straight).</summary>
        void Polar(float a0, float r0, float a1, float r1, float arcAngle, float k, out float x, out float z)
        {
            float a = a0 + AngleDelta(a0, a1) * k, r = r0 + (r1 - r0) * k;
            x = fx + (float)Math.Cos(a) * r; z = fz + (float)Math.Sin(a) * r;
        }

        // ---------------------------------------------------------------- the rack itself
        /// <summary>The rack's parts at <paramref name="t"/>. <paramref name="s"/> scales the carried heights (the dolls' zoom scale).</summary>
        public RackLook LookAt(float t, float s = 1f)
        {
            var look = new RackLook { oldWhole = -1, newWhole = -1 };
            float h = height;
            Centre(false, out float ox, out float oz); Axes(false, out float odx, out float odz, out _, out _);
            Centre(true, out float nx, out float nz); Axes(true, out float ndx, out float ndz, out _, out _);
            // cargo coming off one at a time
            if (t < tLift)
            {
                int left = cargo;
                if (t >= AnticSeconds && cargo > 0) left = cargo - Math.Min(cargo, TakenBy(cargo, (t - AnticSeconds) / Math.Max(1e-4f, cargoSeconds)));
                if (t < tTake) { look.oldWhole = left; return Finish(ref look, t); }
            }
            // old spot: parts still there
            Stand(RackRole.Lifter, t, out float lx, out float lz, out float lfx, out float lfz, out _);
            Stand(RackRole.Carrier, t, out float cx, out float cz, out float cfx, out float cfz, out _);
            float lyawX = lfx - lx, lyawZ = lfz - lz; Norm(ref lyawX, ref lyawZ, 1f, 0f);
            float cyawX = cfx - cx, cyawZ = cfz - cz; Norm(ref cyawX, ref cyawZ, 1f, 0f);
            float shoulder = 0.115f * s;

            // ridge pole
            if (t < tLift)
            {
                float k = Smooth(Clamp01((t - tTake) / LiftSeconds));
                look.ridge = new RackPart { shown = true, x = ox, z = oz, y = h + 0.1f * k, dirX = odx, dirZ = odz, tilt = 15f * k };
            }
            else if (t < tPlant)
            {   // on the Lifter's shoulder from the lift's end: eases up onto it over 0.5 s, runs along the way she faces
                float k = Smooth(Clamp01((t - tLift) / 0.5f));
                float sx = lx, sz = lz;
                float ry = Lerp(h + 0.1f, shoulder + 0.03f * s, k);
                look.ridge = new RackPart { shown = true, x = Lerp(ox, sx, k), z = Lerp(oz, sz, k), y = ry, dirX = Lerp(odx, lyawX, k), dirZ = Lerp(odz, lyawZ, k), tilt = 15f * (1f - k) };
                Norm(ref look.ridge.dirX, ref look.ridge.dirZ, odx, odz);
            }
            else if (t < tRaise)
            {   // the raise: up off the shoulder to the forks, dropped in (Back ease), then settled
                float k = t - tPlant;
                float up = Smooth(Clamp01(k / 1.3f));
                float sx = Lerp(lx, nx, up), sz = Lerp(lz, nz, up);
                float y = Lerp(shoulder + 0.03f * s, h + 0.12f, up);
                float dirX = Lerp(lyawX, ndx, up), dirZ = Lerp(lyawZ, ndz, up);
                float drop = Clamp01((k - 1.5f) / 0.25f);
                y = Lerp(y, h, drop * drop);
                float jolt = k >= 1.75f ? 0.012f * (float)Math.Exp(-(k - 1.75f) / 0.12f) * (float)Math.Cos((k - 1.75f) * 8f * 2f * Math.PI) : 0f;
                look.ridge = new RackPart { shown = true, x = sx, z = sz, y = y, dirX = dirX, dirZ = dirZ, jolt = jolt };
                Norm(ref look.ridge.dirX, ref look.ridge.dirZ, ndx, ndz);
            }
            else look.ridge = new RackPart { shown = true, x = nx, z = nz, y = h, dirX = ndx, dirZ = ndz };
            if (t >= tRaise) { look.newWhole = cargo == 0 ? 0 : HungNow(t); return Finish(ref look, t); }
            if (t >= tPlant + 1.75f && t < tRaise) { /* settled, frames wobble below */ }

            // frames
            look.frameA = Frame(false, t, ox, oz, odx, odz, nx, nz, ndx, ndz, cx, cz, cyawX, cyawZ, s);
            look.frameB = Frame(true, t, ox, oz, odx, odz, nx, nz, ndx, ndz, cx, cz, cyawX, cyawZ, s);
            look.holes = t >= tLift + 1.0f;
            return Finish(ref look, t);
        }

        RackLook Finish(ref RackLook look, float t) { look.holes = look.holes || t >= tLift + 1.0f; return look; }

        /// <summary>Frame A (-bar end) or B (+bar end): stands at the old spot, is pulled (B first, then A), is carried under the Carrier's arm, and is planted (B first, then A).</summary>
        RackPart Frame(bool isB, float t, float ox, float oz, float odx, float odz, float nx, float nz, float ndx, float ndz, float cx, float cz, float cdx, float cdz, float s)
        {
            float sgn = isB ? 1f : -1f;
            float oldX = ox + odx * half * sgn, oldZ = oz + odz * half * sgn;
            float newX = nx + ndx * half * sgn, newZ = nz + ndz * half * sgn;
            // when each is pulled (seconds into the PullFrames phase): B at 0.55 + 0.5 of glide, A at 1.9
            float pullT = tLift + (isB ? 1.0f : 2.4f);
            // when each is set down at the new spot: B at the Plant's 0.55, A at 2.55
            float setT = tCarry + (isB ? 0.5f : 3.1f);
            var p = new RackPart { shown = true, dirX = odx, dirZ = odz };
            // the Carrier's arm side (his left for A, right for B)
            float rx = cdz, rz = -cdx; // his right-hand way
            float carryX = cx + rx * 0.05f * sgn, carryZ = cz + rz * 0.05f * sgn, carryY = 0.085f * s;
            if (t < pullT) { p.x = oldX; p.z = oldZ; p.y = 0f; return p; }
            if (t < pullT + 0.45f)
            {   // up out of its hole in a Snap, a little way, then to the arm
                float k = (t - pullT) / 0.45f;
                float e = Smooth(k);
                p.x = Lerp(oldX, carryX, e); p.z = Lerp(oldZ, carryZ, e); p.y = Lerp(0f, carryY, e);
                p.dirX = Lerp(odx, cdz, e); p.dirZ = Lerp(odz, -cdx, e); p.tilt = -8f * e;
                Norm(ref p.dirX, ref p.dirZ, odx, odz);
                return p;
            }
            if (t < setT - 0.5f)
            {   // carried under his arm, leaning back a little
                p.x = carryX; p.z = carryZ; p.y = carryY; p.dirX = cdz; p.dirZ = -cdx; p.tilt = -8f;
                Norm(ref p.dirX, ref p.dirZ, ndx, ndz);
                return p;
            }
            if (t < setT)
            {   // down onto the ground at its place
                float k = Smooth((t - (setT - 0.5f)) / 0.5f);
                p.x = Lerp(carryX, newX, k); p.z = Lerp(carryZ, newZ, k); p.y = Lerp(carryY, 0f, k);
                p.dirX = Lerp(cdz, ndx, k); p.dirZ = Lerp(-cdx, ndz, k); p.tilt = Lerp(-8f, 0f, k);
                Norm(ref p.dirX, ref p.dirZ, ndx, ndz);
                return p;
            }
            p.x = newX; p.z = newZ; p.y = 0f; p.dirX = ndx; p.dirZ = ndz;
            float since = t - setT;
            if (since < 1.5f) p.jolt = 0.01f * (float)Math.Exp(-since / 0.12f) * (float)Math.Cos(since * 8f * 2f * Math.PI); // pressed in: a Jolt
            // the raise's drop sets the frames wobbling: 4 degrees at 5 Hz, tau 0.2 s
            float wob = t - (tPlant + 1.75f);
            if (wob >= 0f) p.wobble = 4f * (float)Math.Exp(-wob / 0.2f) * (float)Math.Sin(wob * 5f * 2f * Math.PI);
            return p;
        }

        /// <summary>How many of the cargo are on the rack at the new spot at <paramref name="t"/> (they go back one at a time, as the hang beat).</summary>
        public int HungNow(float t)
        {
            if (cargo == 0) return 0;
            float k = Clamp01((t - tRaise) / Math.Max(1e-4f, hangSeconds - 0.4f));
            return HungBy(cargo, k);
        }

        /// <summary>The take-down's and the hang's counts, as <c>FishRackSpot.TakeFraction</c> / <c>HangFraction</c> give them.</summary>
        public static int TakenBy(int n, float k)
        {
            int c = 0;
            for (int i = 0; i < n; i++) if (TakeFraction(i, n) <= k + 1e-4f) c++;
            return c;
        }

        public static int HungBy(int n, float k)
        {
            int c = 0;
            for (int i = 0; i < n; i++) if (HangFraction(i, n) <= k + 1e-4f) c++;
            return c;
        }

        public static float TakeFraction(int i, int n) => 0.1f + 0.85f * (i + 1f) / Math.Max(1, n);
        public static float HangFraction(int i, int n) => 0.15f + 0.8f * (i + 1f) / Math.Max(1, n);

        /// <summary>When cargo piece <paramref name="i"/> comes off (seconds into the beat) and goes back on.</summary>
        public float TakeAt(int i) => AnticSeconds + TakeFraction(i, cargo) * cargoSeconds;
        public float HangAt(int i) => tRaise + HangFraction(i, cargo) * Math.Max(0.01f, hangSeconds - 0.4f);

        /// <summary>The puffs: the frames leaving the ground, the plants, the drop. Times are seconds into the beat.</summary>
        public List<RackEvent> Events()
        {
            var ev = new List<RackEvent>();
            FrameAt(false, true, out float bx, out float bz); FrameAt(false, false, out float ax, out float az);
            ev.Add(new RackEvent(tLift + 1.0f, bx, bz, true, false));
            ev.Add(new RackEvent(tLift + 2.4f, ax, az, true, true)); // the last frame leaving the ground: 3 dust, 4 grass
            FrameAt(true, true, out float pbx, out float pbz); FrameAt(true, false, out float pax, out float paz);
            ev.Add(new RackEvent(tCarry + 0.5f, pbx, pbz, true, false));
            ev.Add(new RackEvent(tCarry + 3.1f, pax, paz, true, false));
            Centre(true, out float nx, out float nz);
            ev.Add(new RackEvent(tPlant + 1.75f, nx, nz, true, false));
            return ev;
        }

        /// <summary>The holes the frames leave at the old spot: the 4 pole feet (a frame's two poles, two frames).</summary>
        public void Holes(List<(float x, float z)> into)
        {
            into.Clear();
            for (int f = 0; f < 2; f++)
            {
                FrameAt(false, f == 1, out float x, out float z); Axes(false, out _, out _, out float ix, out float iz);
                into.Add((x + ix * 0.05f, z + iz * 0.05f)); into.Add((x - ix * 0.05f, z - iz * 0.05f));
            }
        }

        // ---------------------------------------------------------------- helpers
        static float Lerp(float a, float b, float k) => a + (b - a) * k;
        static float Clamp01(float x) => x < 0f ? 0f : x > 1f ? 1f : x;
        static float Smooth(float x) { x = Clamp01(x); return x * x * (3f - 2f * x); }
        static void Norm(ref float x, ref float z, float fallbackX, float fallbackZ)
        {
            float l = (float)Math.Sqrt(x * x + z * z);
            if (l < 1e-5f) { x = fallbackX; z = fallbackZ; return; }
            x /= l; z /= l;
        }
    }

    /// <summary>
    /// #700 (B27): when a rack may be taken down and who does it. Pure rules, tested without a sim.
    /// </summary>
    public static class RackMoveRules
    {
        /// <summary>A quiet hour for the move: at dusk or in the night block, or by day when nothing hangs on the rack. The cheat forces it.</summary>
        public static bool Quiet(DayPhase phase, int cargo, bool force) => force || phase == DayPhase.Evening || phase == DayPhase.Night || cargo == 0;

        /// <summary>The move may begin: a quiet hour, nobody hanging or taking down, no other rack beat running, and a rack that has somewhere to go.</summary>
        public static bool CanStart(DayPhase phase, int cargo, bool force, bool someoneHangingOrTakingDown, bool otherMoveRunning, bool hasFreeSpot)
            => hasFreeSpot && !otherMoveRunning && !someoneHangingOrTakingDown && Quiet(phase, cargo, force);

        public readonly struct Candidate
        {
            public readonly int key; public readonly bool adult, free; public readonly float distance;
            public Candidate(int key, bool adult, bool free, float distance) { this.key = key; this.adult = adult; this.free = free; this.distance = distance; }
        }

        /// <summary>The Lifter is the rack-keeper (<paramref name="keeperKey"/>) when they are free, else the nearest free grown-up; the Carrier is the
        /// nearest other free grown-up. False when there are fewer than two. Ties go to the lower key.</summary>
        public static bool PickCrew(IReadOnlyList<Candidate> people, int keeperKey, out int lifter, out int carrier)
        {
            lifter = carrier = int.MinValue;
            int best = -1;
            for (int i = 0; i < people.Count; i++)
                if (people[i].key == keeperKey && people[i].adult && people[i].free) { best = i; break; }
            if (best < 0) best = Nearest(people, -1, int.MinValue);
            if (best < 0) return false;
            int other = Nearest(people, best, people[best].key);
            if (other < 0) return false;
            lifter = people[best].key; carrier = people[other].key;
            return true;
        }

        static int Nearest(IReadOnlyList<Candidate> people, int skipIndex, int skipKey)
        {
            int best = -1;
            for (int i = 0; i < people.Count; i++)
            {
                var c = people[i];
                if (i == skipIndex || c.key == skipKey || !c.adult || !c.free) continue;
                if (best < 0 || c.distance < people[best].distance - 1e-6f || Math.Abs(c.distance - people[best].distance) <= 1e-6f && c.key < people[best].key) best = i;
            }
            return best;
        }
    }
}
