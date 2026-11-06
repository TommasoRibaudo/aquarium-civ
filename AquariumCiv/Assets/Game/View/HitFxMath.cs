// #357 Contact puffs and target shakes: the PURE part (UnityEngine Mathf/Vector3/Vector2 only, no GameObject, no Time, no Palette), so
// EditMode tests and Tools/AnimBench can compile it and draw puffs in contact sheets. Numbers: docs/design/animation-specs.md §0.4 (puffs),
// §0.5 and B1.f (shakes). The view that draws and spawns them is HitFx.cs.
//
// For AnimBench: PuffPool.Spawn(kind, origin, dir, scale, now, seed, options) fills pooled PuffBit records; for every bit,
// HitFxMath.Position / Velocity / Roll / Size / Alpha01 (=1) give the frame at a time, HitFxShapes.Unit(bit.shape) the outline in
// the screen plane (HitFxShapes.RingTris for the flat water ring), and PuffColour names the palette entry (HitFx.ColourOf maps it).
using UnityEngine;

namespace AquariumCiv.View
{
    /// <summary>The puff kinds of animation-specs §0.4. <see cref="Ring"/> is the flat ring on water; <see cref="Droplets"/> are the splash.</summary>
    public enum PuffKind { Chips, Dust, Sparks, Droplets, Ring, Grass, Leaves, Steam, Snow }

    /// <summary>Solid cartoon outlines (HitFxShapes). Ring is flat on the water; the rest face the camera.</summary>
    public enum PuffShape { Slab, Blob, Diamond, Drop, Ring, Blade, Cube, Star }

    /// <summary>The palette entry of a bit (HitFx maps these to Palette colours): pale wood and bark chips, dry or wet soil, ember and
    /// yellow sparks (gold for the chief), water and mud, grass and leaf (the season's), steam, snow.</summary>
    public enum PuffColour { WoodPale, Bark, DrySoil, WetSoil, Ember, SparkYellow, Gold, Water, Mud, RingWater, RingMud, Grass, Leaf, Steam, Snow }

    /// <summary>The ground under a puff, for picking colours that contrast with it.</summary>
    public enum PuffGround { Grass, Soil, Sand }

    /// <summary>
    /// Visit-loop look-fix: puffs on grass vanished (green on green). Pure: the ground under a puff is classified from its colour and each kind gets a
    /// light, saturated fill plus a dark rim that contrasts with it. Colours here are sRGB; HitFx converts to linear.
    /// </summary>
    public static class PuffContrast
    {
        /// <summary>Grass = green-dominant; sand = light; else soil.</summary>
        public static PuffGround Classify(Color c)
        {
            if (c.g > c.r * 1.02f && c.g >= c.b) return PuffGround.Grass;
            float lum = 0.299f * c.r + 0.587f * c.g + 0.114f * c.b;
            return lum > 0.6f ? PuffGround.Sand : PuffGround.Soil;
        }

        static readonly Color SparkRim = new(0.35f, 0.08f, 0.03f), WoodRim = new(0.25f, 0.14f, 0.06f),
            WaterRim = new(0.05f, 0.22f, 0.45f), GrassRim = new(0.12f, 0.28f, 0.05f), LeafRim = new(0.35f, 0.2f, 0.03f), SoilRim = new(0.2f, 0.12f, 0.06f);

        /// <summary>The fill and rim (sRGB) for a kind of bit on a ground. False = keep the palette's own colour (it already reads).</summary>
        public static bool Pick(PuffColour c, PuffGround g, out Color fill, out Color rim)
        {
            fill = rim = default;
            switch (c)
            {
                case PuffColour.WoodPale:
                    fill = g == PuffGround.Sand ? new Color(0.62f, 0.4f, 0.2f) : new Color(1f, 0.96f, 0.8f); rim = WoodRim; return true;
                case PuffColour.Bark:
                    fill = g == PuffGround.Sand ? new Color(0.4f, 0.25f, 0.12f) : new Color(0.85f, 0.62f, 0.35f); rim = WoodRim; return true;
                case PuffColour.DrySoil: case PuffColour.WetSoil: case PuffColour.Mud:
                    if (g == PuffGround.Grass) return false;
                    fill = g == PuffGround.Sand ? new Color(0.5f, 0.36f, 0.22f) : new Color(0.92f, 0.85f, 0.7f); rim = SoilRim; return true;
                case PuffColour.Ember: fill = new Color(1f, 0.55f, 0.1f); rim = SparkRim; return true;
                case PuffColour.SparkYellow: fill = new Color(1f, 0.88f, 0.2f); rim = SparkRim; return true;
                case PuffColour.Gold: fill = new Color(1f, 0.8f, 0.1f); rim = SparkRim; return true;
                case PuffColour.Water: fill = new Color(0.88f, 0.96f, 1f); rim = WaterRim; return true;
                case PuffColour.RingWater: fill = new Color(0.95f, 1f, 1f); rim = WaterRim; return true;
                case PuffColour.Grass:
                    fill = g == PuffGround.Sand ? new Color(0.55f, 0.8f, 0.2f) : new Color(0.85f, 0.95f, 0.4f); rim = GrassRim; return true;
                case PuffColour.Leaf:
                    if (g != PuffGround.Grass) return false;
                    fill = new Color(0.96f, 0.9f, 0.35f); rim = LeafRim; return true;
                default: return false;
            }
        }

        /// <summary>The white pop flash under every puff lasts this long (about 3 frames).</summary>
        public const float PopSeconds = 0.12f;
        /// <summary>The pop ring's radius as a share of its full size at <paramref name="since"/> seconds (grows fast, 0 once over).</summary>
        public static float PopRadius01(float since) => since < 0f || since >= PopSeconds ? 0f : Mathf.Lerp(0.55f, 1f, since / PopSeconds);
    }

    /// <summary>What a spawn may change (all optional; default = the table's numbers).</summary>
    public struct PuffOptions
    {
        /// <summary>Dust of wet soil, chips unchanged.</summary>
        public bool wet;
        /// <summary>Droplets and Ring on muddy water (brown).</summary>
        public bool mud;
        /// <summary>Sparks of the fire (float up, last 0.7 s) rather than a knapped stone's (fall, 0.35 s).</summary>
        public bool fire;
        /// <summary>Sparks in gold (the chief, C3).</summary>
        public bool gold;
        /// <summary>Droplets aimed at this point (a splash) instead of a cone about up.</summary>
        public bool hasAim;
        public Vector3 aim;
        /// <summary>Bits that land stop at this height (world y). NaN = the origin's y minus 0.06 (the chips spawn that far above the ground).</summary>
        public float groundY;
        /// <summary>Bit count override (Leaves: 1–3, 6 on a tree fall). 0 = the table's.</summary>
        public int count;

        public static PuffOptions Default => new() { groundY = float.NaN };
    }

    /// <summary>One flying/growing bit of a puff. Positions are closed-form in the bit's own age (no stepping, no allocation).</summary>
    public struct PuffBit
    {
        public Vector3 p0, v0;
        /// <summary>Downward acceleration (u/s², negative = floats up).</summary>
        public float gravity;
        public float roll0, spin;
        /// <summary>Size at the start (u; the length of a slab, the radius of a blob, the half-width of a diamond...). Already scaled.</summary>
        public float size;
        /// <summary>Seconds after the spawn before the bit shows (the second ring).</summary>
        public float delay;
        public float life;
        public PuffShape shape;
        public PuffColour colour;
        /// <summary>Sideways sine (u, Hz, phase) for steam; a roll flutter (degrees, Hz) for leaves.</summary>
        public float wobbleAmp, wobbleHz, phase, flutterDeg;
        /// <summary>Stops at <see cref="groundY"/> when it lands (chips, grass, droplets, snow).</summary>
        public bool lands;
        public float groundY;
        /// <summary>Grows with age (dust, steam, ring): size at the end of <see cref="life"/> before the shrink.</summary>
        public float endSize;
        /// <summary>Ring: line width at the start and at the end (u).</summary>
        public float line0, line1;
        /// <summary>Along-velocity stretch (droplets 1.6).</summary>
        public float stretch;
    }

    /// <summary>Per kind table (§0.4). Cone angles are half-angles about the axis.</summary>
    public static class PuffSpec
    {
        public struct Row { public int minBits, maxBits; public float life, gravity, shrinkFrom; }

        /// <summary>The share of a bit's life it keeps its size; it shrinks to 0 over the rest (cartoon, no alpha: §0.4).</summary>
        public const float ShrinkFrom = 0.6f;

        public static Row Of(PuffKind k) => k switch
        {
            PuffKind.Chips => new Row { minBits = 5, maxBits = 7, life = 0.7f, gravity = 3.0f, shrinkFrom = ShrinkFrom },
            PuffKind.Dust => new Row { minBits = 3, maxBits = 4, life = 0.45f, gravity = 0f, shrinkFrom = ShrinkFrom },
            PuffKind.Sparks => new Row { minBits = 5, maxBits = 5, life = 0.35f, gravity = 2.0f, shrinkFrom = ShrinkFrom },
            PuffKind.Droplets => new Row { minBits = 4, maxBits = 10, life = 0.4f, gravity = 4.0f, shrinkFrom = ShrinkFrom },
            PuffKind.Ring => new Row { minBits = 2, maxBits = 2, life = 0.8f, gravity = 0f, shrinkFrom = ShrinkFrom },
            PuffKind.Grass => new Row { minBits = 4, maxBits = 4, life = 0.4f, gravity = 2.5f, shrinkFrom = ShrinkFrom },
            PuffKind.Leaves => new Row { minBits = 1, maxBits = 3, life = 1.2f, gravity = 0f, shrinkFrom = ShrinkFrom },
            PuffKind.Steam => new Row { minBits = 2, maxBits = 2, life = 1.2f, gravity = 0f, shrinkFrom = ShrinkFrom },
            _ => new Row { minBits = 5, maxBits = 5, life = 0.4f, gravity = 3.0f, shrinkFrom = ShrinkFrom },
        };

        /// <summary>The most bits any one puff holds (Droplets up to 10, Leaves 6 on a tree fall).</summary>
        public const int MaxBitsPerPuff = 12;
    }

    /// <summary>Spawning and sampling of puffs. Deterministic: bits come from a hash of (seed, index), never the sim's random stream.</summary>
    public static class HitFxMath
    {
        /// <summary>A hash in 0..1 of (seed, i, salt): the puffs' own seeded stream.</summary>
        public static float Hash01(int seed, int i, int salt)
        {
            unchecked
            {
                uint h = (uint)seed * 747796405u + (uint)i * 2891336453u + (uint)salt * 2246822519u + 374761393u;
                h ^= h >> 15; h *= 2246822519u; h ^= h >> 13; h *= 3266489917u; h ^= h >> 16;
                return (h & 0xffffff) / (float)0x1000000;
            }
        }

        static float Rng(int seed, int i, int salt, float a, float b) => Mathf.Lerp(a, b, Hash01(seed, i, salt));

        /// <summary>How many bits a puff of this kind has for this seed (or <see cref="PuffOptions.count"/>).</summary>
        public static int BitCount(PuffKind kind, int seed, in PuffOptions o)
        {
            var row = PuffSpec.Of(kind);
            int n = o.count > 0 ? o.count : row.minBits + Mathf.FloorToInt(Hash01(seed, 0, 99) * (row.maxBits - row.minBits + 1));
            return Mathf.Clamp(n, 1, PuffSpec.MaxBitsPerPuff);
        }

        /// <summary>
        /// Fills <paramref name="bits"/> with a puff of <paramref name="kind"/> at <paramref name="origin"/> (world). <paramref name="dir"/> is
        /// "away from the cut face" for chips (horizontal part used), the lean of the fall for leaves; the others ignore it.
        /// <paramref name="scale"/> multiplies every length (sizes, speeds, gravity): pass the doll zoom factor (CameraReach.DollScale).
        /// Returns the number of bits written.
        /// </summary>
        public static int Launch(PuffKind kind, Vector3 origin, Vector3 dir, float scale, int seed, in PuffOptions o, PuffBit[] bits)
        {
            int n = Mathf.Min(BitCount(kind, seed, o), bits.Length);
            var row = PuffSpec.Of(kind);
            float groundY = float.IsNaN(o.groundY) ? origin.y - 0.06f * scale : o.groundY;
            var flat = new Vector3(dir.x, 0f, dir.z);
            if (flat.sqrMagnitude < 1e-6f) flat = Vector3.forward;
            flat.Normalize();
            float yaw0 = Mathf.Atan2(flat.x, flat.z);
            for (int i = 0; i < n; i++)
            {
                var b = new PuffBit { p0 = origin, life = row.life, gravity = row.gravity * scale, groundY = groundY, endSize = -1f };
                b.roll0 = Rng(seed, i, 1, 0f, 360f);
                switch (kind)
                {
                    case PuffKind.Chips:
                    {
                        // 0.7–1.0 u/s, cone 50° about the away direction, 35–60° up; slabs 0.028 x 0.012 spinning 720°/s.
                        float yaw = yaw0 + Rng(seed, i, 2, -25f, 25f) * Mathf.Deg2Rad;
                        float el = Rng(seed, i, 3, 35f, 60f) * Mathf.Deg2Rad;
                        float sp = Rng(seed, i, 4, 0.7f, 1.0f) * scale;
                        b.v0 = new Vector3(Mathf.Sin(yaw) * Mathf.Cos(el), Mathf.Sin(el), Mathf.Cos(yaw) * Mathf.Cos(el)) * sp;
                        b.shape = PuffShape.Slab;
                        b.size = 0.028f * scale;
                        b.spin = (Hash01(seed, i, 5) < 0.5f ? -720f : 720f);
                        b.colour = Hash01(seed, i, 6) < 1f / 3f ? PuffColour.Bark : PuffColour.WoodPale;
                        b.lands = true;
                        break;
                    }
                    case PuffKind.Dust:
                    {
                        // 3–4 round blobs, r 0.02 -> 0.05 (Out), 0.15–0.3 u/s out along the ground, rise 0.04 over the life.
                        float yaw = (i + Hash01(seed, i, 2)) / n * Mathf.PI * 2f + yaw0;
                        float sp = Rng(seed, i, 4, 0.15f, 0.3f) * scale;
                        b.v0 = new Vector3(Mathf.Sin(yaw) * sp, 0.04f * scale / row.life, Mathf.Cos(yaw) * sp);
                        b.shape = PuffShape.Blob;
                        b.size = 0.02f * scale; b.endSize = 0.05f * scale;
                        b.colour = o.wet ? PuffColour.WetSoil : PuffColour.DrySoil;
                        break;
                    }
                    case PuffKind.Sparks:
                    {
                        // Bit 0 is the flash star (0.045 for 0.07 s); then diamonds 0.012, 0.5–0.9 u/s, cone 30° about up.
                        if (i == 0)
                        {
                            b.v0 = Vector3.zero; b.gravity = 0f; b.shape = PuffShape.Star; b.size = 0.045f * scale; b.life = 0.07f; b.spin = 0f; b.roll0 = 0f;
                            b.colour = o.gold ? PuffColour.Gold : PuffColour.SparkYellow;
                            break;
                        }
                        float yaw = Rng(seed, i, 2, 0f, 360f) * Mathf.Deg2Rad;
                        float cone = Rng(seed, i, 3, 0f, 30f) * Mathf.Deg2Rad;
                        float sp = Rng(seed, i, 4, 0.5f, 0.9f) * scale;
                        b.v0 = new Vector3(Mathf.Sin(cone) * Mathf.Sin(yaw), Mathf.Cos(cone), Mathf.Sin(cone) * Mathf.Cos(yaw)) * sp;
                        b.shape = PuffShape.Diamond;
                        b.size = 0.012f * scale;
                        b.spin = Rng(seed, i, 5, -360f, 360f);
                        b.gravity = (o.fire ? -0.6f : 2.0f) * scale;
                        b.life = o.fire ? 1.1f : 0.35f; // look-fix: the fire's sparks float up for longer, clear of the flames
                        b.colour = o.gold ? PuffColour.Gold : (i & 1) == 0 ? PuffColour.Ember : PuffColour.SparkYellow;
                        break;
                    }
                    case PuffKind.Droplets:
                    {
                        // 0.7–1.1 u/s, cone 35° about up, or aimed at a point (reached in 0.3 s); teardrops 0.016 stretched x1.6 along the velocity.
                        if (o.hasAim)
                        {
                            const float T = 0.3f;
                            var jitter = new Vector3(Rng(seed, i, 2, -0.02f, 0.02f), 0f, Rng(seed, i, 3, -0.02f, 0.02f)) * scale;
                            var to = o.aim + jitter - origin;
                            b.v0 = to / T + new Vector3(0f, 0.5f * b.gravity * T, 0f);
                        }
                        else
                        {
                            float yaw = Rng(seed, i, 2, 0f, 360f) * Mathf.Deg2Rad;
                            float cone = Rng(seed, i, 3, 0f, 35f) * Mathf.Deg2Rad;
                            float sp = Rng(seed, i, 4, 0.7f, 1.1f) * scale;
                            b.v0 = new Vector3(Mathf.Sin(cone) * Mathf.Sin(yaw), Mathf.Cos(cone), Mathf.Sin(cone) * Mathf.Cos(yaw)) * sp;
                        }
                        b.shape = PuffShape.Drop;
                        b.size = 0.016f * scale; b.stretch = 1.6f;
                        b.colour = o.mud ? PuffColour.Mud : PuffColour.Water;
                        b.lands = true;
                        break;
                    }
                    case PuffKind.Ring:
                    {
                        // 1–2 flat rings on the water: r 0.03 -> 0.20 (Out), line 0.012 -> 0.004, the 2nd 0.15 s later at 0.7x. Needs no velocity.
                        float k = i == 0 ? 1f : 0.7f;
                        b.p0 = new Vector3(origin.x, groundY + 0.002f * scale, origin.z);
                        b.v0 = Vector3.zero; b.gravity = 0f;
                        b.shape = PuffShape.Ring;
                        b.size = 0.03f * scale * k; b.endSize = 0.20f * scale * k;
                        b.line0 = 0.012f * scale * k; b.line1 = 0.004f * scale * k;
                        b.delay = i == 0 ? 0f : 0.15f;
                        b.roll0 = 0f;
                        b.colour = o.mud ? PuffColour.RingMud : PuffColour.RingWater;
                        break;
                    }
                    case PuffKind.Grass:
                    {
                        // 4 blades 0.022 x 0.006, 0.4–0.6 u/s, cone 60° about up.
                        float yaw = Rng(seed, i, 2, 0f, 360f) * Mathf.Deg2Rad;
                        float cone = Rng(seed, i, 3, 10f, 60f) * Mathf.Deg2Rad;
                        float sp = Rng(seed, i, 4, 0.4f, 0.6f) * scale;
                        b.v0 = new Vector3(Mathf.Sin(cone) * Mathf.Sin(yaw), Mathf.Cos(cone), Mathf.Sin(cone) * Mathf.Cos(yaw)) * sp;
                        b.shape = PuffShape.Blade;
                        b.size = 0.022f * scale;
                        b.spin = Rng(seed, i, 5, -300f, 300f);
                        b.colour = PuffColour.Grass;
                        b.lands = true;
                        break;
                    }
                    case PuffKind.Leaves:
                    {
                        // Diamonds 0.02 from the crown, drift 0.1 u/s, fall 0.35 u/s, roll flutter +-40° at 3 Hz, 1.2 s.
                        float yaw = Rng(seed, i, 2, 0f, 360f) * Mathf.Deg2Rad;
                        b.v0 = new Vector3(Mathf.Sin(yaw) * 0.1f, -0.35f, Mathf.Cos(yaw) * 0.1f) * scale;
                        b.gravity = 0f;
                        b.shape = PuffShape.Diamond;
                        b.size = 0.02f * scale;
                        b.flutterDeg = 40f; b.wobbleHz = 3f; b.phase = Rng(seed, i, 7, 0f, 6.2832f);
                        b.colour = PuffColour.Leaf;
                        b.p0 = origin + new Vector3(Rng(seed, i, 8, -0.03f, 0.03f), Rng(seed, i, 9, 0f, 0.03f), Rng(seed, i, 10, -0.03f, 0.03f)) * scale;
                        break;
                    }
                    case PuffKind.Steam:
                    {
                        // 2 off-white blobs r 0.025 -> 0.05, rising 0.12 u/s with a 0.02 u sideways sine.
                        b.v0 = new Vector3(0f, 0.12f * scale, 0f);
                        b.shape = PuffShape.Blob;
                        b.size = 0.025f * scale; b.endSize = 0.05f * scale;
                        b.wobbleAmp = 0.02f * scale; b.wobbleHz = 1.2f; b.phase = Rng(seed, i, 7, 0f, 6.2832f);
                        b.delay = i * 0.25f;
                        b.colour = PuffColour.Steam;
                        break;
                    }
                    default: // Snow: 5 cubes 0.014, 0.6 u/s, gravity 3.
                    {
                        float yaw = Rng(seed, i, 2, 0f, 360f) * Mathf.Deg2Rad;
                        float el = Rng(seed, i, 3, 20f, 70f) * Mathf.Deg2Rad;
                        float sp = 0.6f * scale;
                        b.v0 = new Vector3(Mathf.Sin(yaw) * Mathf.Cos(el), Mathf.Sin(el), Mathf.Cos(yaw) * Mathf.Cos(el)) * sp;
                        b.shape = PuffShape.Cube;
                        b.size = 0.014f * scale;
                        b.spin = Rng(seed, i, 5, -400f, 400f);
                        b.colour = PuffColour.Snow;
                        b.lands = true;
                        break;
                    }
                }
                bits[i] = b;
            }
            return n;
        }

        /// <summary>When the last bit of a launch is gone (its delay plus its life), seconds after the spawn.</summary>
        public static float Duration(PuffBit[] bits, int count)
        {
            float d = 0f;
            for (int i = 0; i < count; i++) d = Mathf.Max(d, bits[i].delay + bits[i].life);
            return d;
        }

        /// <summary>Seconds into the bit's own life (after its delay); negative before it shows.</summary>
        public static float Age(in PuffBit b, float sincePuff) => sincePuff - b.delay;

        /// <summary>Seconds after launch at which a bit that lands touches its <see cref="PuffBit.groundY"/>; +infinity if it never does.</summary>
        public static float LandingTime(in PuffBit b)
        {
            if (!b.lands || b.gravity <= 0f) return float.PositiveInfinity;
            float h = b.p0.y - b.groundY;
            if (h < 0f) return 0f;
            return (b.v0.y + Mathf.Sqrt(b.v0.y * b.v0.y + 2f * b.gravity * h)) / b.gravity;
        }

        /// <summary>World position at <paramref name="age"/> seconds into the bit's life (closed form; a landed bit lies still on the ground).</summary>
        public static Vector3 Position(in PuffBit b, float age)
        {
            float t = Mathf.Max(0f, age);
            float tl = LandingTime(b);
            bool landed = t >= tl;
            if (landed) t = tl;
            var p = b.p0 + b.v0 * t;
            p.y -= 0.5f * b.gravity * t * t;
            if (landed) p.y = b.groundY;
            if (b.wobbleAmp > 0f) p.x += b.wobbleAmp * Mathf.Sin(b.phase + age * b.wobbleHz * Mathf.PI * 2f);
            return p;
        }

        /// <summary>Velocity at <paramref name="age"/> (zero once landed); droplets point their tip along it.</summary>
        public static Vector3 Velocity(in PuffBit b, float age)
        {
            float t = Mathf.Max(0f, age);
            if (t >= LandingTime(b)) return Vector3.zero;
            return new Vector3(b.v0.x, b.v0.y - b.gravity * t, b.v0.z);
        }

        /// <summary>The bit's roll in degrees in the screen plane (spins until it lands; leaves flutter).</summary>
        public static float Roll(in PuffBit b, float age)
        {
            float t = Mathf.Min(Mathf.Max(0f, age), LandingTime(b));
            float r = b.roll0 + b.spin * t;
            if (b.flutterDeg > 0f) r += b.flutterDeg * Mathf.Sin(b.phase + age * b.wobbleHz * Mathf.PI * 2f);
            return r;
        }

        /// <summary>1 for the first <see cref="PuffSpec.ShrinkFrom"/> of the life, then down to 0 at the end (cartoon: shrink, not fade).</summary>
        public static float Shrink(float age, float life)
        {
            if (life <= 0f || age < 0f) return 0f;
            float u = age / life;
            if (u >= 1f) return 0f;
            if (u <= PuffSpec.ShrinkFrom) return 1f;
            float k = (u - PuffSpec.ShrinkFrom) / (1f - PuffSpec.ShrinkFrom);
            return 1f - k * k * (3f - 2f * k);
        }

        /// <summary>The bit's size now (u): grows from <see cref="PuffBit.size"/> to <see cref="PuffBit.endSize"/> by an Out curve over
        /// its life where it has an end size, then shrinks to 0 over the last 40 % of the life. 0 before the delay and after the life.</summary>
        public static float Size(in PuffBit b, float age)
        {
            if (age < 0f || age >= b.life) return 0f;
            float s = b.size;
            if (b.endSize >= 0f)
            {
                float u = Mathf.Clamp01(age / b.life);
                s = Mathf.Lerp(b.size, b.endSize, 1f - (1f - u) * (1f - u));
            }
            // A ring keeps its radius to the end and thins its line instead (see Line).
            return b.shape == PuffShape.Ring ? s : s * Shrink(age, b.life);
        }

        /// <summary>A ring's line width now (u): 0.012 -> 0.004 over its life, gone at the end.</summary>
        public static float Line(in PuffBit b, float age)
        {
            if (age < 0f || age >= b.life) return 0f;
            float u = age / b.life;
            return Mathf.Lerp(b.line0, b.line1, u) * Shrink(age, b.life);
        }
    }

    /// <summary>The outlines, built once (2D, unit size, centred; x right, y up in the screen plane).</summary>
    public static class HitFxShapes
    {
        public sealed class Outline { public Vector2[] v; public int[] t; }

        static readonly Outline[] cache = new Outline[8];

        /// <summary>The unit outline of a shape (<see cref="PuffShape.Ring"/> has none: use <see cref="RingTris"/>). The caller scales by
        /// the bit's size (the slab's length, the blob's radius, the diamond's half-width...). Never allocates after the first call.</summary>
        public static Outline Unit(PuffShape s)
        {
            int i = (int)s;
            return cache[i] ??= Build(s);
        }

        static Outline Build(PuffShape s)
        {
            switch (s)
            {
                case PuffShape.Slab: return Quad(1f, 0.43f);
                case PuffShape.Blade: return Quad(1f, 0.27f);
                case PuffShape.Cube: return Quad(1f, 1f);
                case PuffShape.Diamond: return new Outline { v = new[] { new Vector2(0.5f, 0f), new Vector2(0f, 0.5f), new Vector2(-0.5f, 0f), new Vector2(0f, -0.5f) }, t = new[] { 0, 1, 2, 0, 2, 3 } };
                case PuffShape.Blob: return Disc(8, 1f);
                case PuffShape.Star: // four points: thin cross of two diamonds
                    return new Outline
                    {
                        v = new[] { new Vector2(0.5f, 0f), new Vector2(0.09f, 0.09f), new Vector2(0f, 0.5f), new Vector2(-0.09f, 0.09f), new Vector2(-0.5f, 0f), new Vector2(-0.09f, -0.09f), new Vector2(0f, -0.5f), new Vector2(0.09f, -0.09f) },
                        t = new[] { 0, 1, 7, 1, 2, 3, 3, 4, 5, 5, 6, 7, 1, 3, 5, 1, 5, 7 },
                    };
                case PuffShape.Drop: // a teardrop along +x: a round end at -x, the tip at +x. Length 1 (before the stretch), radius 0.3.
                {
                    var v = new System.Collections.Generic.List<Vector2> { new Vector2(0.5f, 0f) };
                    for (int k = 0; k <= 6; k++) { float a = Mathf.Lerp(80f, 280f, k / 6f) * Mathf.Deg2Rad; v.Add(new Vector2(-0.2f + Mathf.Cos(a) * 0.3f, Mathf.Sin(a) * 0.3f)); }
                    var t = new System.Collections.Generic.List<int>();
                    for (int k = 1; k < v.Count - 1; k++) { t.Add(0); t.Add(k); t.Add(k + 1); }
                    return new Outline { v = v.ToArray(), t = t.ToArray() };
                }
                default: return Quad(1f, 1f);
            }
        }

        static Outline Quad(float w, float h) => new()
        {
            v = new[] { new Vector2(-w / 2f, -h / 2f), new Vector2(w / 2f, -h / 2f), new Vector2(w / 2f, h / 2f), new Vector2(-w / 2f, h / 2f) },
            t = new[] { 0, 2, 1, 0, 3, 2 },
        };

        static Outline Disc(int sides, float r)
        {
            var v = new Vector2[sides + 1];
            var t = new int[sides * 3];
            for (int i = 0; i < sides; i++)
            {
                float a = (i + 0.5f) * Mathf.PI * 2f / sides;
                v[i + 1] = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
                t[i * 3] = 0; t[i * 3 + 1] = 1 + (i + 1) % sides; t[i * 3 + 2] = 1 + i;
            }
            return new Outline { v = v, t = t };
        }

        public const int RingSides = 20;

        /// <summary>A flat ring of the given outer radius and line width in the xz plane (y 0, facing up): fills <paramref name="verts"/> (2 x
        /// <see cref="RingSides"/> points: outer, inner) and <paramref name="tris"/> (6 x sides indices, offset by <paramref name="first"/>).</summary>
        public static void RingTris(float radius, float line, Vector3 centre, System.Collections.Generic.List<Vector3> verts, System.Collections.Generic.List<int> tris)
        {
            int first = verts.Count;
            float inner = Mathf.Max(0f, radius - line);
            for (int i = 0; i < RingSides; i++)
            {
                float a = i * Mathf.PI * 2f / RingSides;
                float c = Mathf.Cos(a), s = Mathf.Sin(a);
                verts.Add(centre + new Vector3(c * radius, 0f, s * radius));
                verts.Add(centre + new Vector3(c * inner, 0f, s * inner));
            }
            for (int i = 0; i < RingSides; i++)
            {
                int o0 = first + i * 2, i0 = o0 + 1, o1 = first + (i + 1) % RingSides * 2, i1 = o1 + 1;
                tris.Add(o0); tris.Add(o1); tris.Add(i0);
                tris.Add(i0); tris.Add(o1); tris.Add(i1);
            }
        }
    }

    /// <summary>One live puff in the pool.</summary>
    public sealed class Puff
    {
        public PuffKind kind;
        public bool live;
        public double start, end;
        public long order;
        /// <summary>Where it started, its scale, and the ground under it (the contrast pick and the pop flash).</summary>
        public Vector3 origin;
        public float scale;
        public PuffGround ground;
        public int bitCount;
        public readonly PuffBit[] bits = new PuffBit[PuffSpec.MaxBitsPerPuff];
    }

    /// <summary>
    /// The pool of live puffs: at most <see cref="MaxPuffs"/> puffs and <see cref="MaxBits"/> bits at once (§0.4: 24 and 160); one more
    /// drops the oldest. All records are made up front: a spawn allocates nothing.
    /// </summary>
    public sealed class PuffPool
    {
        public const int MaxPuffs = 24, MaxBits = 160;

        readonly Puff[] slots = new Puff[MaxPuffs];
        long order;

        public PuffPool() { for (int i = 0; i < slots.Length; i++) slots[i] = new Puff(); }

        public int SlotCount => slots.Length;
        public Puff this[int i] => slots[i];
        public int Live { get; private set; }
        public int LiveBits { get; private set; }
        /// <summary>Puffs dropped to make room (oldest first) and puffs spawned, since the last <see cref="Clear"/> (tests).</summary>
        public int Dropped { get; private set; }
        public int Spawned { get; private set; }

        public void Clear()
        {
            foreach (var p in slots) p.live = false;
            Live = LiveBits = Dropped = Spawned = 0;
        }

        /// <summary>Starts a puff at <paramref name="now"/> (seconds, any clock the caller keeps). Drops the oldest if the pool or the bit budget is full.</summary>
        public Puff Spawn(PuffKind kind, Vector3 origin, Vector3 dir, float scale, double now, int seed, in PuffOptions opts)
        {
            Puff slot = null;
            for (int i = 0; i < slots.Length && slot == null; i++) if (!slots[i].live) slot = slots[i];
            if (slot == null) slot = DropOldest(null);
            slot.kind = kind;
            slot.bitCount = HitFxMath.Launch(kind, origin, dir, scale, seed, opts, slot.bits);
            slot.origin = origin; slot.scale = scale; slot.ground = PuffGround.Soil;
            slot.start = now;
            slot.end = now + HitFxMath.Duration(slot.bits, slot.bitCount);
            slot.order = ++order;
            slot.live = true;
            Spawned++;
            Recount();
            while (LiveBits > MaxBits && Live > 1) { DropOldest(slot); Recount(); }
            return slot;
        }

        Puff DropOldest(Puff keep)
        {
            Puff oldest = null;
            foreach (var p in slots) if (p.live && p != keep && (oldest == null || p.order < oldest.order)) oldest = p;
            if (oldest != null) { oldest.live = false; Dropped++; }
            return oldest;
        }

        /// <summary>Frees the puffs whose last bit is gone at <paramref name="now"/>.</summary>
        public void Step(double now)
        {
            foreach (var p in slots) if (p.live && now >= p.end) p.live = false;
            Recount();
        }

        void Recount()
        {
            Live = 0; LiveBits = 0;
            foreach (var p in slots) if (p.live) { Live++; LiveBits += p.bitCount; }
        }
    }

    // ================================================================== shakes (§0.5, B1.f)

    /// <summary>What a stroke's target does (the answer): bush and tree sway in the vegetation shader; frame and stone jolt as transforms.</summary>
    public enum ShakeTarget { None, Bush, Tree, Frame, Stone }

    /// <summary>The numbers of an answer (§0.5): angle in degrees, frequency in Hz, decay tau in seconds, reach in units (how far from the
    /// centre the vertices still move), and for a Jolt the drop in units.</summary>
    public readonly struct ShakeSpec
    {
        public readonly float degrees, hz, tau, reach, drop;
        public ShakeSpec(float degrees, float hz, float tau, float reach, float drop = 0f) { this.degrees = degrees; this.hz = hz; this.tau = tau; this.reach = reach; this.drop = drop; }

        /// <summary>Bush: sway 11°, 6 Hz, τ 0.18 s (about 0.5 s visible). Tree crown: 6°, 4 Hz, τ 0.3 s (batch 1: 7° and 4° moved a berry bush
        /// 2-3 px on a 1179x2556 screen at zoom 0.7: it didn't read). Frame and stone: drop 0.012 u, 8 Hz, τ 0.12 s.</summary>
        public static readonly ShakeSpec Bush = new(11f, 6f, 0.18f, 0.12f);
        public static readonly ShakeSpec Tree = new(6f, 4f, 0.3f, 0.30f);
        public static readonly ShakeSpec Jolt = new(0f, 8f, 0.12f, 0.2f, 0.012f);

        public static ShakeSpec Of(ShakeTarget t) => t switch { ShakeTarget.Bush => Bush, ShakeTarget.Tree => Tree, _ => Jolt };

        /// <summary>The tangent of the sway angle: the shader moves a vertex sideways by this x its height above the base (a rotation about the base, small angle).</summary>
        public float Tan => Mathf.Tan(degrees * Mathf.Deg2Rad);

        /// <summary>Seconds until the wobble is under 6 % of its start.</summary>
        public float Seconds => Easing.WobbleSeconds(tau);
    }

    public static class ShakeMath
    {
        /// <summary>The damped sway factor (-1..1 times e^(-t/τ)) at <paramref name="age"/>; 0 before the hit and after it has died.</summary>
        public static float Wobble(in ShakeSpec s, float age) => age <= 0f || age > s.Seconds ? 0f : Easing.Wobble(age, 1f, s.hz, s.tau);

        /// <summary>How much a vertex <paramref name="distance"/> from the centre moves relative to the centre (1 at it, ~0 past the reach): exp(-(d/r)²).</summary>
        public static float Falloff(float distance, float reach) => reach <= 0f ? 0f : Mathf.Exp(-(distance * distance) / (reach * reach));

        /// <summary>The sideways offset (u) a sway gives a vertex <paramref name="height"/> above the base, <paramref name="distance"/> from the centre: the same
        /// maths as the vegetation shader.</summary>
        public static float SwayOffset(in ShakeSpec s, float age, float height, float distance) => Wobble(s, age) * s.Tan * Mathf.Max(0f, height) * Falloff(distance, s.reach);

        /// <summary>A jolt's vertical offset (u, negative = down first): drop 0.012, 8 Hz, τ 0.12 s.</summary>
        public static float JoltOffset(in ShakeSpec s, float age) => age <= 0f || age > s.Seconds ? 0f : -Easing.Wobble(age, s.drop, s.hz, s.tau);
    }

    /// <summary>
    /// The small set of live shake centres the vegetation shader reads (global arrays <c>_AC_ShakeA/B/C</c>, <c>_AC_ShakeCount</c>):
    /// at most <see cref="Max"/>, the oldest replaced; <see cref="Step"/> writes the arrays for a time. Pure: HitFx uploads them.
    /// </summary>
    public sealed class ShakeSet
    {
        public const int Max = 16;

        struct Slot { public bool live; public Vector2 xz; public float baseY; public Vector2 dir; public ShakeSpec spec; public double start; public long order; }

        readonly Slot[] slots = new Slot[Max];
        long order;
        /// <summary>x, z, age (negative = waits), tan(angle).</summary>
        public readonly Vector4[] A = new Vector4[Max];
        /// <summary>frequency Hz, tau s, reach u, base y.</summary>
        public readonly Vector4[] B = new Vector4[Max];
        /// <summary>direction x, z.</summary>
        public readonly Vector4[] C = new Vector4[Max];
        /// <summary>Slots in use (compacted at the front of the arrays after <see cref="Step"/>).</summary>
        public int Count { get; private set; }
        public int Dropped { get; private set; }

        /// <summary>Starts a sway at <paramref name="centre"/> (world), leaning away along <paramref name="away"/>; <paramref name="delay"/> seconds from <paramref name="now"/>.</summary>
        public void Add(ShakeSpec spec, Vector3 centre, float baseY, Vector3 away, double now, float delay = 0f)
        {
            int at = -1;
            for (int i = 0; i < Max && at < 0; i++) if (!slots[i].live) at = i;
            if (at < 0)
            {
                at = 0;
                for (int i = 1; i < Max; i++) if (slots[i].order < slots[at].order) at = i;
                Dropped++;
            }
            var d = new Vector2(away.x, away.z);
            d = d.sqrMagnitude < 1e-6f ? Vector2.right : d.normalized;
            slots[at] = new Slot { live = true, xz = new Vector2(centre.x, centre.z), baseY = baseY, dir = d, spec = spec, start = now + delay, order = ++order };
        }

        public void Clear() { for (int i = 0; i < Max; i++) slots[i].live = false; Count = 0; }

        /// <summary>Live shakes now (waiting ones included).</summary>
        public int Live { get { int n = 0; foreach (var s in slots) if (s.live) n++; return n; } }

        /// <summary>Frees the dead and fills the arrays for <paramref name="now"/>. Returns <see cref="Count"/>.</summary>
        public int Step(double now)
        {
            int n = 0;
            for (int i = 0; i < Max; i++)
            {
                ref var s = ref slots[i];
                if (!s.live) continue;
                float age = (float)(now - s.start);
                if (age > s.spec.Seconds) { s.live = false; continue; }
                A[n] = new Vector4(s.xz.x, s.xz.y, age, s.spec.Tan);
                B[n] = new Vector4(s.spec.hz, s.spec.tau, s.spec.reach, s.baseY);
                C[n] = new Vector4(s.dir.x, s.dir.y, 0f, 0f);
                n++;
            }
            for (int i = n; i < Max; i++) A[i] = new Vector4(0f, 0f, -1f, 0f);
            Count = n;
            return n;
        }
    }
}
