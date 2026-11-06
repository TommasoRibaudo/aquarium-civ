// #355 Beat kit (pure: Mathf, Vector3, Quaternion only, so Tools/AnimBench compiles it). Arc objects: docs/design/animation-specs.md §0.6.
using UnityEngine;

namespace AquariumCiv.View
{
    /// <summary>What a thrown thing does when it gets there (animation-specs §0.6).</summary>
    public enum Landing
    {
        /// <summary>Into the arms: it rests at the end (the owner then puts it in the catcher's <c>Arms</c> slot).</summary>
        Catch,
        /// <summary>A spear: stops at the landing tangent clamped to 30–50° off vertical, then quivers (<c>Wobble</c> 6°, 10 Hz, τ 0.25 s).</summary>
        Stick,
        /// <summary>A ball missed: two bounces (0.09, 0.03 u) on along the way it flew, then a roll that slows to a stop.</summary>
        BounceRoll,
        /// <summary>Into a basket or a bucket: gone at the end (the container pops).</summary>
        DropIn,
        /// <summary>Into water: gone at the end (the owner puts <c>Droplets</c> and a <c>Ring</c> there).</summary>
        Splash,
        /// <summary>A snowball: gone at the end in a <c>Snow</c> puff.</summary>
        Burst,
    }

    /// <summary>Where an arc object is in its life.</summary>
    public enum ArcStage { Waiting, Flying, Bouncing, Rolling, Stuck, Resting, Gone }

    /// <summary>
    /// One throw (animation-specs §0.6): <c>Arc(start, end, h, T, spin, landing)</c>. Flight: position = lerp(start, end, u) +
    /// up · 4h·u(1−u), u = (time − <see cref="launchAt"/>) / <see cref="flight"/>, linear in time (it reads as a throw). The object's
    /// long axis (local +z) follows the tangent, plus <see cref="spin"/> degrees of pitch over the flight (a fish's two flips: 720).
    /// <b>Start is the hand</b>, end a real thing (a catcher's chest, the basket, a spot on the green). Units: box units, seconds.
    /// </summary>
    public struct ArcShot
    {
        public Vector3 start, end;
        /// <summary>Apex above the straight line from start to end, at the middle of the flight.</summary>
        public float apex;
        /// <summary>Seconds in the air.</summary>
        public float flight;
        /// <summary>When it leaves the hand (the owner's clock: the group's or the view's seconds).</summary>
        public float launchAt;
        /// <summary>Extra pitch over the flight, degrees (a tumbling fish; 0 for a spear, which follows its tangent).</summary>
        public float spin;
        public Landing landing;
        /// <summary>The object's radius (a rolling ball turns by the distance over this).</summary>
        public float radius;
        /// <summary><see cref="Landing.BounceRoll"/>: the two bounces' heights (u), and the roll's length (u) and time (s).</summary>
        public float bounce1, bounce2, roll, rollSeconds;
        /// <summary><see cref="Landing.Stick"/>: the clamp off vertical (degrees) and the quiver.</summary>
        public float stickMin, stickMax, wobbleDeg, wobbleHz, wobbleTau;

        /// <summary>A throw with the spec sheets' defaults for its landing (§0.6: ball bounces 0.09/0.03, roll 0.25 u in 0.8 s; spear
        /// 30–50°, wobble 6°, 10 Hz, τ 0.25 s).</summary>
        public static ArcShot Throw(Vector3 start, Vector3 end, float apex, float flight, Landing landing, float launchAt = 0f, float spin = 0f, float radius = 0.035f)
            => new ArcShot
            {
                start = start, end = end, apex = apex, flight = Mathf.Max(0.05f, flight), launchAt = launchAt, spin = spin, landing = landing, radius = radius,
                bounce1 = 0.09f, bounce2 = 0.03f, roll = 0.25f, rollSeconds = 0.8f,
                stickMin = 30f, stickMax = 50f, wobbleDeg = 6f, wobbleHz = 10f, wobbleTau = 0.25f,
            };

        public float LandAt => launchAt + flight;
        /// <summary>The horizontal way it flies (unit, xz), or +z for a straight-up throw.</summary>
        public Vector3 Heading
        {
            get { var d = new Vector3(end.x - start.x, 0f, end.z - start.z); float m = d.magnitude; return m > 1e-5f ? d / m : Vector3.forward; }
        }
    }

    /// <summary>An arc object at one moment: where it is, how it's turned, and what stage it's in.</summary>
    public struct ArcSample
    {
        public Vector3 position, velocity;
        public Quaternion rotation;
        public ArcStage stage;
        /// <summary>Seconds since it landed (negative while it flies).</summary>
        public float sinceLanding;
        public bool Visible => stage != ArcStage.Waiting && stage != ArcStage.Gone;
    }

    /// <summary>The arc maths (pure functions of the shot and the time).</summary>
    public static class Arc
    {
        /// <summary>Flight time for <paramref name="distance"/> at a <paramref name="speed"/> (u/s; the ball's 0.45 u in 0.75 s is 0.6).</summary>
        public static float FlightFor(float distance, float speed) => Mathf.Max(0.15f, distance / Mathf.Max(0.05f, speed));

        /// <summary>The point at <paramref name="u"/> 0..1 of the flight.</summary>
        public static Vector3 PointAt(in ArcShot s, float u)
        {
            u = Mathf.Clamp01(u);
            return Vector3.LerpUnclamped(s.start, s.end, u) + Vector3.up * (4f * s.apex * u * (1f - u));
        }

        /// <summary>The velocity (u/s) at <paramref name="u"/> of the flight.</summary>
        public static Vector3 VelocityAt(in ArcShot s, float u)
        {
            u = Mathf.Clamp01(u);
            return (s.end - s.start) / s.flight + Vector3.up * (4f * s.apex * (1f - 2f * u) / s.flight);
        }

        /// <summary>Turned so its local +z points along <paramref name="dir"/> (yaw about up, then pitch), plus <paramref name="extraPitch"/>
        /// degrees nose-down. Only Euler and the shim's maths: AnimBench has no LookRotation.</summary>
        public static Quaternion Along(Vector3 dir, float extraPitch = 0f)
        {
            float h = Mathf.Sqrt(dir.x * dir.x + dir.z * dir.z);
            float yaw = h > 1e-5f ? Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg : 0f;
            float pitchDown = -Mathf.Atan2(dir.y, Mathf.Max(h, 1e-5f)) * Mathf.Rad2Deg;
            return Quaternion.Euler(pitchDown + extraPitch, yaw, 0f);
        }

        /// <summary>Did it land in (<paramref name="from"/>, <paramref name="to"/>]? The owner's once-only landing event (a puff, a basket pop).</summary>
        public static bool LandedBetween(in ArcShot s, float from, float to) => from < s.LandAt && s.LandAt <= to;

        /// <summary>Seconds after landing until it stops moving (rest, stuck still, or gone).</summary>
        public static float SettleSeconds(in ArcShot s)
        {
            switch (s.landing)
            {
                case Landing.BounceRoll: { float g = Gravity(s); return Hop(s.bounce1, g) + Hop(s.bounce2, g) + s.rollSeconds; }
                case Landing.Stick: return Easing.WobbleSeconds(s.wobbleTau);
                default: return 0f;
            }
        }

        /// <summary>The shot's own gravity (u/s²): the flight's parabola has it, so the bounces after it look like the same world.</summary>
        static float Gravity(in ArcShot s) => Mathf.Max(0.5f, 8f * Mathf.Max(0.01f, s.apex) / (s.flight * s.flight));
        static float Hop(float height, float g) => height > 0f ? 2f * Mathf.Sqrt(2f * height / g) : 0f;

        /// <summary>The object at <paramref name="time"/> (the same clock as <see cref="ArcShot.launchAt"/>).</summary>
        public static ArcSample Sample(in ArcShot s, float time)
        {
            var o = new ArcSample { sinceLanding = time - s.LandAt };
            if (time < s.launchAt) { o.stage = ArcStage.Waiting; o.position = s.start; o.rotation = Along(s.Heading); return o; }
            if (time < s.LandAt)
            {
                float u = (time - s.launchAt) / s.flight;
                o.stage = ArcStage.Flying;
                o.position = PointAt(s, u);
                o.velocity = VelocityAt(s, u);
                o.rotation = Along(o.velocity, s.spin * u);
                return o;
            }
            float t = time - s.LandAt;
            var land = VelocityAt(s, 1f);
            switch (s.landing)
            {
                case Landing.Stick:
                {
                    // The tangent's angle off vertical, kept to 30–50° (a spear that came in flat still stands up in the ground).
                    float h = Mathf.Sqrt(land.x * land.x + land.z * land.z);
                    float offVertical = Mathf.Atan2(h, Mathf.Max(1e-5f, -land.y)) * Mathf.Rad2Deg;
                    offVertical = Mathf.Clamp(offVertical, s.stickMin, s.stickMax);
                    float pitchDown = 90f - offVertical + Easing.Wobble(t, s.wobbleDeg, s.wobbleHz, s.wobbleTau);
                    float yaw = Mathf.Atan2(land.x, land.z) * Mathf.Rad2Deg;
                    o.stage = ArcStage.Stuck;
                    o.position = s.end;
                    o.rotation = Quaternion.Euler(pitchDown, h > 1e-5f ? yaw : 0f, 0f);
                    return o;
                }
                case Landing.BounceRoll:
                {
                    var dir = s.Heading;
                    float g = Gravity(s);
                    float vh = new Vector3(land.x, 0f, land.z).magnitude * 0.45f; // the ground takes most of the way it flew
                    float h1 = Hop(s.bounce1, g), h2 = Hop(s.bounce2, g);
                    float along, up = 0f;
                    var stage = ArcStage.Bouncing;
                    if (t < h1) { along = vh * t; up = HopHeight(s.bounce1, t, h1); }
                    else if (t < h1 + h2) { along = vh * t; up = HopHeight(s.bounce2, t - h1, h2); }
                    else
                    {
                        float hopped = vh * (h1 + h2);
                        float r = s.rollSeconds > 0f ? Mathf.Clamp01((t - h1 - h2) / s.rollSeconds) : 1f;
                        along = hopped + s.roll * (1f - (1f - r) * (1f - r)); // Out: slows to a stop
                        stage = r >= 1f ? ArcStage.Resting : ArcStage.Rolling;
                    }
                    o.stage = stage;
                    o.position = s.end + dir * along + Vector3.up * up;
                    float turn = s.radius > 1e-4f ? along / s.radius * Mathf.Rad2Deg : 0f;
                    o.rotation = Quaternion.AngleAxis(turn, Vector3.Cross(Vector3.up, dir)) * Along(dir);
                    return o;
                }
                case Landing.Catch:
                    o.stage = ArcStage.Resting;
                    o.position = s.end;
                    o.rotation = Along(s.Heading);
                    return o;
                default: // DropIn, Splash, Burst: gone into the basket, the water or a puff
                    o.stage = ArcStage.Gone;
                    o.position = s.end;
                    o.rotation = Along(s.Heading);
                    return o;
            }
        }

        static float HopHeight(float height, float t, float duration)
        {
            float u = duration > 0f ? t / duration : 1f;
            return 4f * height * u * (1f - u);
        }
    }

    /// <summary>A handle on a pooled arc (stale once its slot is reused).</summary>
    public readonly struct ArcHandle
    {
        public readonly int slot, gen;
        public ArcHandle(int slot, int gen) { this.slot = slot; this.gen = gen; }
        public static ArcHandle None => new ArcHandle(-1, 0);
        public bool IsNone => slot < 0;
    }

    /// <summary>
    /// A fixed pool of arc objects for one owner (a play group, the hunt, the fishers): no allocation per throw. The owner launches
    /// a shot, samples it each frame (or the drawer walks the live slots), and releases it, or lets <see cref="Sweep"/> free what has
    /// settled. Full: the oldest shot is taken over (a new throw is never dropped). The meshes are the drawer's (B1, B2, ...).
    /// </summary>
    public sealed class ArcPool
    {
        readonly ArcShot[] shots;
        readonly int[] gens;
        readonly bool[] live;

        public ArcPool(int capacity = 8)
        {
            capacity = Mathf.Max(1, capacity);
            shots = new ArcShot[capacity]; gens = new int[capacity]; live = new bool[capacity];
        }

        public int Capacity => shots.Length;
        public int Live { get { int n = 0; for (int i = 0; i < live.Length; i++) if (live[i]) n++; return n; } }

        public ArcHandle Launch(in ArcShot shot)
        {
            int slot = -1;
            for (int i = 0; i < live.Length; i++) if (!live[i]) { slot = i; break; }
            if (slot < 0)
            {
                slot = 0;
                for (int i = 1; i < shots.Length; i++) if (shots[i].launchAt < shots[slot].launchAt) slot = i;
            }
            gens[slot]++;
            shots[slot] = shot;
            live[slot] = true;
            return new ArcHandle(slot, gens[slot]);
        }

        public bool IsValid(ArcHandle h) => h.slot >= 0 && h.slot < shots.Length && live[h.slot] && gens[h.slot] == h.gen;

        public bool TryGet(ArcHandle h, out ArcShot shot)
        {
            if (!IsValid(h)) { shot = default; return false; }
            shot = shots[h.slot];
            return true;
        }

        public bool Sample(ArcHandle h, float time, out ArcSample sample)
        {
            if (!IsValid(h)) { sample = default; sample.stage = ArcStage.Gone; return false; }
            sample = Arc.Sample(shots[h.slot], time);
            return true;
        }

        public void Release(ArcHandle h) { if (IsValid(h)) live[h.slot] = false; }

        /// <summary>Frees every shot that has settled for longer than <paramref name="linger"/> seconds (gone ones at once). Returns how many.</summary>
        public int Sweep(float time, float linger)
        {
            int n = 0;
            for (int i = 0; i < shots.Length; i++)
            {
                if (!live[i]) continue;
                ref readonly var s = ref shots[i];
                float settled = s.LandAt + Arc.SettleSeconds(s);
                bool gone = s.landing is Landing.DropIn or Landing.Splash or Landing.Burst;
                if (time >= settled + (gone ? 0f : linger)) { live[i] = false; n++; }
            }
            return n;
        }

        /// <summary>For the drawer: is slot <paramref name="i"/> live, and its shot.</summary>
        public bool IsLive(int i) => live[i];
        public ArcShot ShotAt(int i) => shots[i];
    }
}
