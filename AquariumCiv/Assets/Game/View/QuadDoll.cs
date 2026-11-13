using System.Collections.Generic;
using UnityEngine;

namespace AquariumCiv.View
{
    /// <summary>The four-legged looks P0 names (animation-specs P0, #425): a parameter set on <see cref="QuadLook"/> each.</summary>
    public enum QuadKind { None, Stag, Doe, Wolf, Fawn, Pup, Dog, WhiteStag, WhiteWolf }

    /// <summary>
    /// P0 (#425): the parameters of the one procedural quadruped. Deer, wolves and dogs are parameter sets on it (research 19 §11): size,
    /// leg length, neck, head and muzzle, ears and their droop, tail and its curl, coat and patches, antlers. Sizes in box units, from the
    /// P0 table (the 1.5-2x of every animal in the box). <see cref="Dog"/> gives the drift by generation.
    /// </summary>
    public struct QuadLook
    {
        public QuadKind kind;
        /// <summary>Shoulder height, body length, legs as a share of the shoulder height.</summary>
        public float shoulder, bodyLen, legShare;
        /// <summary>The neck's length and how far it rises from the withers at rest (degrees above level).</summary>
        public float neckLen, neckRise;
        /// <summary>Head length and muzzle (1 = the wolf's long muzzle, 0.6 a short round one), a round baby head (1 adult, 1.4 big).</summary>
        public float headLen, muzzle, headRound;
        /// <summary>Ear size, droop 0 (upright) to 1 (floppy), wide deer ears or pointed ones.</summary>
        public float earSize, earDroop; public bool wideEars;
        /// <summary>Tail length, how it hangs at rest (degrees down from level), curl 0..1 (up over the back), a deer's short white flag.</summary>
        public float tailLen, tailHang, tailCurl; public bool flag;
        /// <summary>Coat, belly, a darker saddle, piebald patches 0..1, the white rump, a pale face.</summary>
        public Color coat, belly, saddle, patchCol; public float patch; public bool rump, paleFace;
        /// <summary>Antlers (size 0 none .. 1.4 the white stag's great ones), a hide collar, the eye's glint (the white stag's gold).</summary>
        public float antlers; public bool collar; public Color eye;
        /// <summary>#504: 0 = the lean build (wolves, dogs, pups); the deer carry 0.4: a wider and deeper barrel (chest and belly) and a thicker neck.</summary>
        public float fullness;
        /// <summary>Litter-mates and herd-mates differ a little (a hash of the animal).</summary>
        public int variant;

        public float Leg => shoulder * legShare;
        public float BodyH => shoulder - Leg;

        static readonly Color Fawn = new(0.66f, 0.47f, 0.30f), DeerBelly = new(0.92f, 0.86f, 0.74f);
        static readonly Color Grey = new(0.55f, 0.55f, 0.53f), DarkSaddle = new(0.30f, 0.29f, 0.28f), WolfPale = new(0.84f, 0.83f, 0.79f);
        static readonly Color Black = new(0.07f, 0.06f, 0.05f);

        public static QuadLook Of(QuadKind k, int variant = 0)
        {
            var l = new QuadLook { kind = k, variant = variant, eye = Black };
            switch (k)
            {
                case QuadKind.Stag: case QuadKind.Doe: case QuadKind.Fawn: case QuadKind.WhiteStag:
                    l.shoulder = k == QuadKind.Fawn ? 0.09f : k == QuadKind.Doe ? 0.15f : 0.16f;
                    l.bodyLen = k == QuadKind.Fawn ? 0.12f : 0.20f;
                    l.legShare = k == QuadKind.Fawn ? 0.62f : 0.57f; // #504: shorter legs under a deeper body (it read long and thin)
                    l.fullness = k == QuadKind.Fawn ? 0.3f : 0.4f;
                    l.neckLen = k == QuadKind.Fawn ? 0.05f : 0.09f; l.neckRise = 58f; // a longer neck reaches the grass
                    l.headLen = k == QuadKind.Fawn ? 0.04f : 0.05f; l.muzzle = 0.75f; l.headRound = k == QuadKind.Fawn ? 1.25f : 1f;
                    l.earSize = k == QuadKind.Fawn ? 0.019f : 0.017f; l.wideEars = true;
                    l.tailLen = 0.022f; l.tailHang = 20f; l.flag = true;
                    l.coat = Fawn; l.belly = DeerBelly; l.saddle = new Color(0.55f, 0.38f, 0.24f); l.rump = true;
                    l.antlers = k == QuadKind.Stag ? 1f : k == QuadKind.WhiteStag ? 1.4f : 0f;
                    if (k == QuadKind.Fawn) { l.patch = 0.6f; l.patchCol = DeerBelly; } // spots
                    if (k == QuadKind.WhiteStag) { l.coat = new Color(0.93f, 0.92f, 0.88f); l.saddle = new Color(0.86f, 0.85f, 0.80f); l.eye = new Color(0.95f, 0.75f, 0.25f); }
                    break;
                case QuadKind.Pup:
                    l.shoulder = 0.05f; l.bodyLen = 0.08f; l.legShare = 0.45f;
                    l.neckLen = 0.012f; l.neckRise = 30f; l.headLen = 0.034f; l.muzzle = 0.55f; l.headRound = 1.4f;
                    l.earSize = 0.013f; l.tailLen = 0.014f; l.tailHang = 30f;
                    l.coat = new Color(0.60f, 0.59f, 0.57f); l.belly = WolfPale; l.saddle = new Color(0.50f, 0.49f, 0.47f);
                    break;
                default: // the wolf (and the dog's generation 0)
                    l.shoulder = 0.10f; l.bodyLen = 0.16f; l.legShare = 0.56f;
                    l.neckLen = 0.03f; l.neckRise = 22f; l.headLen = 0.05f; l.muzzle = 1f; l.headRound = 1f;
                    l.earSize = 0.016f; l.tailLen = 0.07f; l.tailHang = 62f;
                    l.coat = Grey; l.belly = WolfPale; l.saddle = DarkSaddle; l.paleFace = true;
                    if (k == QuadKind.WhiteWolf) { l.coat = new Color(0.94f, 0.94f, 0.92f); l.saddle = new Color(0.86f, 0.86f, 0.84f); l.belly = Color.white; }
                    break;
            }
            return l;
        }

        /// <summary>P0 drift: each dog generation adds 0.25 to the ear droop, patches and tail curl (capped at 1), takes 0.05 off the muzzle and
        /// a little off the size, with +-0.1 from a hash of the dog so litter-mates differ. Generation 0 is a wolf; 4 looks like a dog.</summary>
        public static QuadLook Dog(int generation, int hash)
        {
            // user 2026-11-12 ("white cancer"; "more characteristics of pups, more dog-like"): no white blotches on the flanks. The drift
            // is the domestic look: a coat from the dog palette, white bib, socks and blaze, floppy ears, a curled tail, and the pup's
            // face kept into adulthood (a rounder head, shorter muzzle, bigger eyes), on a smaller, shorter-legged body.
            var l = Of(QuadKind.Wolf, hash);
            l.kind = QuadKind.Dog;
            float g = Mathf.Max(0, generation), k = Mathf.Clamp01(g / 4f);
            float J(int salt) => ((Mathf.Abs(hash * 73856093 ^ salt * 19349663) % 1000) / 1000f - 0.5f) * 0.2f;
            l.earDroop = Mathf.Clamp01(0.3f * g + J(1));
            l.patch = Mathf.Clamp01(0.25f * g + J(2));
            l.tailCurl = Mathf.Clamp01(0.25f * g + J(3));
            l.muzzle = Mathf.Lerp(1f, 0.68f, k);
            l.headRound = Mathf.Lerp(1f, 1.25f, k);
            l.headLen *= Mathf.Lerp(1f, 0.92f, k);
            l.earSize *= Mathf.Lerp(1f, 1.25f, k);
            l.shoulder *= Mathf.Lerp(1f, 0.88f, k); l.bodyLen *= Mathf.Lerp(1f, 0.85f, k);
            l.legShare = Mathf.Lerp(0.56f, 0.49f, k);
            l.tailHang = Mathf.Lerp(62f, 10f, l.tailCurl);
            var pick = DogCoats[Mathf.Abs(hash * 2654435 + 17) % DogCoats.Length];
            l.coat = Color.Lerp(l.coat, pick, k);
            l.saddle = Color.Lerp(l.saddle, (Mathf.Abs(hash) % 3 == 0 && g >= 3) ? Black : pick * 0.78f, k); // some go black-and-tan
            l.belly = Color.Lerp(l.belly, Color.Lerp(pick, Color.white, 0.55f), k);
            l.paleFace = g < 2;
            l.patchCol = new Color(0.95f, 0.93f, 0.88f);
            l.collar = generation >= 2;
            return l;
        }

        static readonly Color[] DogCoats =
        {
            new(0.74f, 0.54f, 0.31f), new(0.62f, 0.36f, 0.19f), new(0.20f, 0.18f, 0.17f), new(0.45f, 0.30f, 0.19f), new(0.86f, 0.77f, 0.60f),
        };

        /// <summary>A key for the mesh cache: looks that build the same meshes share them.</summary>
        public string Key() => $"{kind}:{shoulder:0.000}:{bodyLen:0.000}:{fullness:0.00}:{legShare:0.00}:{neckLen:0.000}:{earDroop:0.00}:{patch:0.00}:{tailCurl:0.00}:{muzzle:0.00}:{antlers:0.00}:{collar}:{variant % 4}:{headRound:0.00}:{headLen:0.000}:{earSize:0.000}:{ColorUtility.ToHtmlStringRGB(coat)}:{ColorUtility.ToHtmlStringRGB(saddle)}:{ColorUtility.ToHtmlStringRGB(belly)}";
    }

    /// <summary>
    /// One pose of the quadruped (<see cref="QuadDoll.Apply"/>): where and which way, the legs' phase and gait, body pitch / roll / lift, the
    /// neck's and head's angles, lying, sitting, the play bow, the tail. Built by <see cref="QuadMotion"/>'s gaits.
    /// </summary>
    public struct QuadPose
    {
        public Vector3 at; public float yaw;
        /// <summary>Leg phase in radians (NaN: standing still) and the gait: 0 walk (4-beat), 1 trot (diagonal pairs), 2 bound (front and back pairs).</summary>
        public float phase, gait;
        /// <summary>Body pitch (degrees, + nose down), roll (degrees), lift (box units, a bob or a leap).</summary>
        public float bodyPitch, roll, lift;
        /// <summary>Neck pitch (degrees from its rest, + down), head pitch (+ down), head yaw (degrees, + right).</summary>
        public float neck, head, headYaw;
        /// <summary>0..1: lying down (legs folded), sitting (the howl's sit), the play bow (front low), curled asleep.</summary>
        public float lie, sit, bow, curl;
        /// <summary>The tail (degrees up from its rest hang), its wag (degrees, side to side).</summary>
        public float tail, wag;
        /// <summary>The bound / gallop: how far (radians) the hind pair lags the front pair (0: they stretch and gather together, the deer's leap).</summary>
        public float lead;
        /// <summary>#504 ears (degrees): each ear's flick out and down (0 at rest), and both ears' swivel (+ forward, - back).</summary>
        public float earL, earR, earSwivel;
        public float scale;

        public static QuadPose Stand(Vector3 at, float yaw) => new QuadPose { at = at, yaw = yaw, phase = float.NaN, scale = 1f };
    }

    /// <summary>
    /// P0's gaits and poses as functions of time (the beat sheet's numbers): <see cref="Walk"/>, <see cref="Trot"/>, <see cref="Graze"/>,
    /// <see cref="Alert"/>, <see cref="Bolt"/>, <see cref="LieDown"/>, <see cref="Drink"/>, <see cref="SitHowl"/>, <see cref="PlayBow"/>,
    /// <see cref="SleepCurl"/>, <see cref="Sniff"/>. Each fills the pose's motion fields; the caller sets where and which way.
    /// </summary>
    public static class QuadMotion
    {
        /// <summary>Steps per second: the walk 1.6, the trot 2.6; the deer's bound 3 s at 1.4 u/s.</summary>
        public const float WalkSteps = 1.6f, TrotSteps = 2.6f, BoundSteps = 2.2f, BoltSpeedDeer = 1.4f, BoltSpeedWolf = 1.6f;

        public static void Walk(ref QuadPose p, float t)
        {
            p.phase = t * WalkSteps * Mathf.PI; p.gait = 0f;
            // user 2026-11-12 ("pretty stiff"): the body rocks and sways with the steps, the head nods twice a stride
            p.lift = Mathf.Abs(Mathf.Sin(p.phase)) * 0.004f;
            p.bodyPitch = Mathf.Sin(p.phase * 2f + 0.8f) * 1.5f;
            p.roll = Mathf.Sin(p.phase + 0.6f) * 2.5f;
            p.neck = Mathf.Sin(p.phase * 2f + 0.5f) * 6f; p.head = -Mathf.Sin(p.phase * 2f + 0.5f) * 3f;
            p.headYaw = Mathf.Sin(p.phase) * 4f; p.tail = 0f;
        }

        public static void Trot(ref QuadPose p, float t)
        {
            p.phase = t * TrotSteps * Mathf.PI; p.gait = 1f;
            p.lift = Mathf.Abs(Mathf.Sin(p.phase)) * 0.015f * 0.5f;
            p.neck = 6f; p.head = 4f; p.tail = -4f;
        }

        /// <summary>Graze cycle in seconds: head down and chewing, up to look round, down again (a per-deer offset keeps a herd out of step).</summary>
        public const float GrazeCycle = 11f;

        /// <summary>
        /// #504: how far down the head is (0 = up and looking, 1 = at the grass) at cycle time <paramref name="c"/> (0..<see cref="GrazeCycle"/>):
        /// down and chewing to 6.0 s, up by 6.6 s, a look round to 8.6 s, down again by 9.3 s.
        /// </summary>
        public static float GrazeDown(float c)
        {
            if (c < 6f) return 1f;
            if (c < 6.6f) return 1f - Mathf.SmoothStep(0f, 1f, (c - 6f) / 0.6f);
            if (c < 8.6f) return 0f;
            if (c < 9.3f) return Mathf.SmoothStep(0f, 1f, (c - 8.6f) / 0.7f);
            return 1f;
        }

        /// <summary>
        /// #504 (playtest 582: "their eating animation sucks"). The old graze put the neck 14 degrees below level and the head 112 degrees
        /// down, nose tucked under the chin and still 8 cm off the grass, held for ever with a 5 degree sway: no chewing, no lift, ears dead.
        /// Now the neck goes 58 degrees below level and the head 75, so the muzzle reaches the ground; it chews (small 2.6 Hz bobs, a
        /// tug of the neck every 1.9 s), lifts to look round (head swings, ears turn forward, one flicks), then goes down again.
        /// <paramref name="neckRise"/> is the look's rest rise (the deer's 58): the pose angles are relative to it.
        /// </summary>
        public static void Graze(ref QuadPose p, float t, float since, int seed, float neckRise = 58f)
        {
            float kIn = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(since / 0.6f));
            float c = Mathf.Repeat(since + seed * 1.7f, GrazeCycle);
            float down = GrazeDown(c);
            float eff = Mathf.Min(kIn, down);   // 1 = head at the grass
            float look = (1f - down) * kIn;     // 1 = head up and looking round
            float chew = Mathf.Clamp01((eff - 0.85f) / 0.15f);
            float tugPhase = Mathf.Repeat(t + seed * 0.37f, 1.9f) / 1.9f;
            float tug = tugPhase < 0.25f ? Mathf.Sin(tugPhase / 0.25f * Mathf.PI) * 7f * chew : 0f;
            float bob = Mathf.Sin((t + seed * 0.21f) * Mathf.PI * 2f * 2.6f) * 3.5f * chew;
            p.phase = float.NaN;
            // world angles below level, from the rest pose (neck -rise, head 0) to looking round (neck -22, head -4) or the grass (58, 75)
            float neckDown = Mathf.Lerp(-neckRise, Mathf.Lerp(-22f, 58f, down), kIn);
            float headDown = Mathf.Lerp(0f, Mathf.Lerp(-4f, 75f, down), kIn);
            p.neck = neckDown + neckRise - tug;                 // the pose's neck is relative to its rest rise
            p.head = headDown - (neckDown + neckRise) + bob + tug * 0.5f; // and its head relative to the neck
            p.bodyPitch = 6f * eff;
            float w = Mathf.Clamp01((c - 6.6f) / 2f); // the look round: one sweep left and right
            p.headYaw = Mathf.Sin(w * Mathf.PI * 2f) * 40f;
            p.earSwivel = -10f * eff + 30f * look;
            p.earR = c >= 3f && c < 3.3f ? 32f * Mathf.Sin((c - 3f) / 0.3f * Mathf.PI) : 0f; // a flick while grazing
            p.earL = c >= 7.4f && c < 7.7f ? 38f * Mathf.Sin((c - 7.4f) / 0.3f * Mathf.PI) : 0f; // and one while looking round
            p.tail = c >= 4.6f && c < 5.1f ? 18f * Mathf.Sin((c - 4.6f) / 0.5f * Mathf.PI) : 0f; // the tail flicks too
        }

        /// <summary>Head up and turned to the cause in 0.25 s (Snap), tail flag up, still.</summary>
        public static void Alert(ref QuadPose p, float since, float yawToCause, bool flag)
        {
            float k = 1f - Mathf.Pow(1f - Mathf.Clamp01(since / 0.25f), 3f);
            p.phase = float.NaN;
            p.neck = -14f * k; p.head = -6f * k;
            p.headYaw = Mathf.Clamp(yawToCause, -80f, 80f) * k;
            p.tail = flag ? 80f * k : 10f * k;
            p.earSwivel = (flag ? 32f : 18f) * k; // ears turn to the cause
            if (since < 0.35f) p.earL = 28f * Mathf.Sin(since / 0.35f * Mathf.PI); // and one flicks as the head comes up
        }

        /// <summary>The deer's bound (leaps of 0.04 u, the white rump flashing: tail up) or the wolf's flat run.</summary>
        public static void Bolt(ref QuadPose p, float t, bool deer)
        {
            if (deer)
            {
                // user 2026-11-12 ("the body does not move, the legs are all together"): the leap stretches out in the air and gathers
                // on landing, the hinds a beat behind the fronts; the body rocks nose up at take-off, nose down to land; the neck counters
                p.phase = t * BoundSteps * Mathf.PI * 2f; p.gait = 2f; p.lead = 0.45f;
                float s = Mathf.Sin(p.phase - 0.2f);
                p.lift = Mathf.Max(0f, s) * 0.04f;
                p.bodyPitch = -Mathf.Cos(p.phase) * 14f;
                p.neck = -10f + Mathf.Cos(p.phase) * 10f; p.head = -Mathf.Cos(p.phase) * 6f; p.tail = 85f;
            }
            else
            {
                // the wolf's gallop: the hinds lag the fronts by a third of a stride (gathered, then stretched flat), a rocking body
                p.phase = t * TrotSteps * 1.6f * Mathf.PI; p.gait = 2f; p.lead = 1.2f;
                p.lift = Mathf.Max(0f, Mathf.Sin(p.phase - 0.5f)) * 0.014f;
                p.bodyPitch = -Mathf.Cos(p.phase - 0.4f) * 8f;
                p.neck = 14f + Mathf.Cos(p.phase - 0.4f) * 8f; p.head = -6f - Mathf.Cos(p.phase - 0.4f) * 4f; p.tail = 15f + Mathf.Sin(p.phase) * 6f;
            }
        }

        /// <summary>Front legs fold, then back, over 0.6 s; head down on the paws at the end.</summary>
        public static void LieDown(ref QuadPose p, float since, float headRest = 1f)
        {
            p.phase = float.NaN;
            p.lie = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(since / 0.6f));
            p.neck = Mathf.Lerp(0f, 18f, p.lie * headRest); p.head = 10f * p.lie * headRest;
        }

        /// <summary>Front legs splay a little, head down to the water, lapping nods at 2 Hz, lifting between sips.</summary>
        public static void Drink(ref QuadPose p, float t)
        {
            p.phase = float.NaN;
            float sip = Mathf.Repeat(t, 4f) < 3f ? 1f : 0.3f;
            p.neck = 80f * sip; p.head = 45f * sip + Mathf.Sin(t * Mathf.PI * 4f) * 6f * sip; p.bodyPitch = 6f * sip;
        }

        /// <summary>Sit (0.4 s), muzzle up to 70 degrees, a slow chest swell, held; <paramref name="end"/> 0..1 lets it go again.</summary>
        public static void SitHowl(ref QuadPose p, float since, float hold = 2.5f)
        {
            p.phase = float.NaN;
            float sit = Mathf.Clamp01(since / 0.4f) * Mathf.Clamp01((hold + 0.8f - since) / 0.4f);
            p.sit = sit;
            float up = Mathf.Clamp01((since - 0.3f) / 0.5f) * Mathf.Clamp01((hold + 0.5f - since) / 0.4f);
            p.neck = -45f * up; p.head = -35f * up;
            p.lift = Mathf.Sin(since * 1.4f) * 0.002f * up; // the chest swell
            p.tail = -10f * sit;
        }

        /// <summary>Front low (pitch -20), rump up, tail wag at 3 Hz, then a hop sideways.</summary>
        public static void PlayBow(ref QuadPose p, float t)
        {
            p.phase = float.NaN;
            float c = Mathf.Repeat(t, 2.4f);
            p.bow = c < 1.6f ? Mathf.SmoothStep(0f, 1f, c / 0.2f) : 0f;
            p.wag = Mathf.Sin(t * Mathf.PI * 6f) * 30f; p.tail = 40f;
            p.lift = c >= 1.6f && c < 1.9f ? Mathf.Sin((c - 1.6f) / 0.3f * Mathf.PI) * 0.02f : 0f;
            p.neck = -8f * p.bow;
        }

        /// <summary>Lying curled into a ring, the tail over the nose, a breath bob every 3 s.</summary>
        public static void SleepCurl(ref QuadPose p, float t)
        {
            p.phase = float.NaN; p.lie = 1f; p.curl = 1f;
            p.lift = Mathf.Max(0f, Mathf.Sin(t * Mathf.PI * 2f / 3f)) * 0.004f;
            p.neck = 25f; p.head = 15f; p.headYaw = 70f; p.tail = 0f;
        }

        /// <summary>Head down 0.3 s, three quick nods, head up (2 s in all); the arrival's air sniff when <paramref name="air"/>.</summary>
        public static void Sniff(ref QuadPose p, float since, bool air = false)
        {
            p.phase = float.NaN;
            float down = since < 0.3f ? since / 0.3f : since < 1.6f ? 1f : Mathf.Clamp01(1f - (since - 1.6f) / 0.4f);
            float nod = since >= 0.3f && since < 1.5f ? Mathf.Sin((since - 0.3f) * Mathf.PI * 2.5f) * 7f : 0f;
            if (air) { p.neck = -25f * down; p.head = -15f * down + nod; }
            else { p.neck = 50f * down; p.head = 30f * down + nod; }
        }
    }

    /// <summary>
    /// P0 (#425): the shared procedural quadruped, built like <see cref="RabbitDoll"/>: low-poly boxes and icos from the palette, one mesh per
    /// part per look (cached), pooled by the views, posed by <see cref="QuadPose"/>. No skinning. Parts: the body (with coat, saddle,
    /// patches, rump), the neck, the head (ears, eyes, muzzle, antlers baked in), four legs hinged at hip and shoulder, the tail.
    /// Deer (<c>DeerView</c>), wolves (<c>WolfView</c>) and, later, the dogs are looks on it.
    /// </summary>
    public sealed class QuadDoll
    {
        public readonly GameObject go;
        public readonly QuadLook look;
        readonly Transform body, neck, head, tail;
        readonly Transform[] ears = new Transform[2]; // #504: left (-x) and right (+x), pivoting at the root
        readonly Transform[] legs = new Transform[4];   // the upper legs, from the shoulder or the hip
        readonly Transform[] lowers = new Transform[4]; // below the knee (front) or the hock (hind): the cannon to the foot
        readonly Transform[] mids = new Transform[4];   // the hind's shin, from the stifle back down to the hock (null on the fronts)

        /// <summary>
        /// user 2026-11-12 ("the knee is the other way around"): the hind leg is three bones, as on a deer or a dog. The thigh runs forward
        /// and down from the hip to the stifle (the knee, under the belly), the shin back and down to the hock, the cannon straight down
        /// to the foot. Standing angles (degrees at each joint, relative; + swings the foot back): thigh -35, stifle +65, hock -30.
        /// </summary>
        const float Thigh = -35f, Stifle = 65f, HockJ = -30f;
        /// <summary>The sit: the thigh forward along the ground, the shin folded back under it, the cannon forward to the paws.</summary>
        const float SitThigh = -51f, SitStifle = 173f, SitHock = -178f;
        static readonly float[] WalkOff = { 0f, Mathf.PI, Mathf.PI * 0.5f, Mathf.PI * 1.5f };
        static readonly float[] TrotOff = { 0f, Mathf.PI, Mathf.PI, Mathf.PI * 2f };

        static readonly Dictionary<string, Mesh> meshes = new();

        public QuadKind Kind => look.kind;

        public QuadDoll(QuadKind kind, Transform parent, Material mat) : this(QuadLook.Of(kind), parent, mat) { }

        public QuadDoll(QuadLook look, Transform parent, Material mat)
        {
            this.look = look;
            go = new GameObject(look.kind.ToString()) { hideFlags = HideFlags.DontSave };
            go.transform.SetParent(parent, false);
            float L = look.bodyLen, H = look.BodyH;
            body = Part(go.transform, "Body", MeshOf(look, 0), mat, new Vector3(0f, look.Leg, 0f));
            neck = Part(body, "Neck", MeshOf(look, 1), mat, NeckBase(look));
            head = Part(neck, "Head", MeshOf(look, 2), mat, new Vector3(0f, 0f, look.neckLen));
            for (int i = 0; i < 2; i++) ears[i] = Part(head, i == 0 ? "EarL" : "EarR", MeshOf(look, 5 + i), mat, EarAt(look, i == 0 ? -1 : 1));
            tail = Part(body, "Tail", MeshOf(look, 3), mat, new Vector3(0f, H * 0.78f, -L * 0.47f));
            for (int i = 0; i < 4; i++)
            {
                bool front = i < 2;
                legs[i] = Part(body, "Leg" + i, MeshOf(look, front ? 4 : 7), mat, LegAt(look, i));
                var legParent = legs[i];
                if (!front) legParent = mids[i] = Part(legs[i], "Shin" + i, MeshOf(look, 10), mat, new Vector3(0f, -HindLen(look, 0), 0f));
                lowers[i] = Part(legParent, "Low" + i, MeshOf(look, front ? 8 : 9), mat, new Vector3(0f, -(front ? SegLen(look) : HindLen(look, 1)), 0f));
            }
            Apply(QuadPose.Stand(Vector3.zero, 0f));
        }

        /// <summary>One leg segment's length: half the leg; the hind's two are longer, so its dog-leg stands as tall as the front.</summary>
        static float SegLen(QuadLook l) => (l.Leg + l.BodyH * 0.2f) * 0.5f;

        /// <summary>
        /// The hind bones' lengths (0 thigh, 1 shin, 2 cannon). They stand as tall as the front leg with the foot under the hip: the
        /// cannon is 38% of the height (a high hock), the thigh and shin share the rest so their forward and back slopes cancel.
        /// </summary>
        static float HindLen(QuadLook l, int bone)
        {
            float h = l.Leg + l.BodyH * 0.2f;
            float a1 = -Thigh * Mathf.Deg2Rad, a2 = (Thigh + Stifle) * Mathf.Deg2Rad; // the thigh's and the shin's slopes from vertical
            float rest = h * 0.62f, va = rest / (1f + Mathf.Tan(a1) / Mathf.Tan(a2)), vb = rest - va;
            return bone == 0 ? va / Mathf.Cos(a1) : bone == 1 ? vb / Mathf.Cos(a2) : h * 0.38f;
        }

        static Vector3 NeckBase(QuadLook l) => new(0f, l.BodyH * 0.72f, l.bodyLen * 0.4f);
        static Vector3 LegAt(QuadLook l, int i) => new((i % 2 == 0 ? -1f : 1f) * l.BodyH * 0.32f, l.BodyH * 0.2f, (i < 2 ? 1f : -1f) * l.bodyLen * 0.36f);

        /// <summary>Where an ear's root sits in the head's space (sd -1 left, +1 right).</summary>
        static Vector3 EarAt(QuadLook l, int sd)
        {
            float r = l.headLen * 0.42f * l.headRound;
            return new Vector3(sd * r * 0.55f, r * 0.85f, l.headLen * 0.05f);
        }

        static Transform Part(Transform parent, string name, Mesh m, Material mat, Vector3 at)
        {
            var g = new GameObject(name) { hideFlags = HideFlags.DontSave };
            g.transform.SetParent(parent, false);
            g.transform.localPosition = at;
            g.AddComponent<MeshFilter>().sharedMesh = m;
            var r = g.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return g.transform;
        }

        /// <summary>The look's part meshes: 0 body, 1 neck, 2 head, 3 tail, 4 leg (hanging from 0 to -leg), 5 left ear and 6 right ear (from their roots). Cached per look.</summary>
        static Mesh MeshOf(QuadLook l, int part)
        {
            string key = l.Key() + "#" + part;
            if (meshes.TryGetValue(key, out var m) && m != null) return m;
            var mb = new MeshBuilder(425 + part * 7 + l.variant % 4) { jitter = 0.02f };
            BuildPart(mb, l, part);
            m = mb.ToMesh($"Quad{l.kind}{part}");
            meshes[key] = m;
            return m;
        }

        static void BuildPart(MeshBuilder mb, QuadLook l, int part)
        {
            float L = l.bodyLen, H = l.BodyH, W = H * 0.78f;
            switch (part)
            {
                case 0: // the body: a long ico, belly pale underneath, then the saddle, patches, the rump
                    if (l.fullness > 0.01f) { DeerBody(mb, l); break; } // the deer: one lofted barrel (user 2026-11-12: the blobs didn't merge)
                    // #504: a full build widens the barrel and drops the belly (the back stays where it was), and the chest swells
                    float fw = 1f + l.fullness * 0.9f, fh = 1f + l.fullness * 0.6f;
                    mb.Ico(new Vector3(0f, H * (1f - 0.5f * fh), 0f), L * 0.5f, new Vector3(W * fw / L, H * fh / L, 1f), l.coat, 0.05f, l.belly);
                    mb.Ico(new Vector3(0f, H * 0.62f - H * 0.1f * l.fullness, L * 0.26f), H * 0.62f, new Vector3(0.95f * fw, 1.05f * fh, 1.1f), l.coat, 0.05f, l.belly); // the chest
                    if (l.saddle != l.coat) mb.Ico(new Vector3(0f, H * 0.93f, -L * 0.04f), L * 0.3f, new Vector3(W / L * 1.25f, 0.18f, 1f), l.saddle, 0.04f);
                    if (l.rump) mb.Ico(new Vector3(0f, H * 0.6f, -L * 0.47f), H * 0.42f, new Vector3(1.05f, 1f, 0.45f), l.belly, 0.04f);
                    if (l.kind == QuadKind.Dog)
                    {
                        if (l.patch > 0.15f) mb.Ico(new Vector3(0f, H * 0.42f, L * 0.4f), H * 0.4f, new Vector3(0.75f, 1f, 0.55f), l.patchCol, 0.04f); // the white bib
                    }
                    else if (l.patch > 0.05f)
                    {
                        int n = Mathf.RoundToInt(2 + l.patch * 4);
                        var rnd = new System.Random(l.variant * 31 + 5);
                        for (int i = 0; i < n; i++)
                        {
                            float z = ((float)rnd.NextDouble() - 0.5f) * L * 0.75f, sd = rnd.NextDouble() < 0.5 ? -1f : 1f;
                            float r = (l.kind == QuadKind.Fawn ? 0.006f : 0.016f) * (0.7f + 0.6f * (float)rnd.NextDouble()) * Mathf.Lerp(0.6f, 1.2f, l.patch);
                            mb.Ico(new Vector3(sd * W * 0.43f, H * (0.6f + 0.3f * (float)rnd.NextDouble()), z), r, new Vector3(0.5f, 1f, 1.2f), l.patchCol, 0.05f);
                        }
                    }
                    if (l.collar) mb.Prism(new Vector3(0f, H * 0.75f, L * 0.44f), H * 0.42f, H * 0.42f, 0.008f, 7, new Color(0.45f, 0.30f, 0.18f), Quaternion.Euler(70f, 0f, 0f));
                    break;
                case 1: // the neck: along +z from the withers (the rest rise is the transform's)
                    if (l.fullness > 0.01f) DeerNeck(mb, l); // a lofted column, deep and narrow, tapering to the head
                    else
                    mb.Ico(new Vector3(0f, 0f, l.neckLen * 0.5f), Mathf.Max(l.neckLen * 0.5f, H * 0.3f), new Vector3(H * 0.62f / Mathf.Max(l.neckLen * 0.5f, H * 0.3f), H * 0.7f / Mathf.Max(l.neckLen * 0.5f, H * 0.3f), 1.1f), l.coat, 0.05f, l.belly);
                    break;
                case 2: // the head, facing +z from the neck's tip
                    if (l.wideEars) { DeerHead(mb, l); break; }
                    {
                        float hl = l.headLen, r = hl * 0.42f * l.headRound;
                        var face = l.paleFace ? Color.Lerp(l.coat, l.belly, 0.55f) : l.coat;
                        if (l.kind == QuadKind.Dog && l.patch > 0.5f) face = Color.Lerp(face, l.patchCol, 0.85f); // the blaze
                        mb.Ico(new Vector3(0f, r * 0.25f, hl * 0.18f), r, new Vector3(0.9f, 0.9f, 1.05f), l.coat, 0.05f, l.belly);
                        float ml = hl * 0.38f * l.muzzle;
                        mb.Ico(new Vector3(0f, r * 0.02f, hl * 0.2f + r * 0.6f + ml * 0.5f), Mathf.Max(ml, 0.006f), new Vector3(r * 0.55f / Mathf.Max(ml, 0.006f), r * 0.5f / Mathf.Max(ml, 0.006f), 1f), face, 0.04f);
                        mb.Ico(new Vector3(0f, r * 0.12f, hl * 0.2f + r * 0.6f + ml * 1.0f), r * 0.2f, Vector3.one, new Color(0.07f, 0.06f, 0.05f), 0.02f); // nose
                        foreach (int sd in new[] { -1, 1 })
                        {
                            mb.Ico(new Vector3(sd * r * 0.62f, r * 0.45f, hl * 0.3f), r * (l.kind == QuadKind.Dog ? 0.14f + 0.08f * l.headRound : 0.2f), Vector3.one, l.eye, 0.02f);
                            if (l.antlers > 0.01f)
                            {
                                // the beam up and back, then out, with tines: reads as "antlers" at phone size
                                float a = l.antlers;
                                var col = new Color(0.86f, 0.78f, 0.62f);
                                // from the top of the skull behind the ears, sweeping back (so they point up when the head is down)
                                var b0 = new Vector3(sd * r * 0.4f, r * 0.8f, -hl * 0.1f);
                                Vector3 Tine(Vector3 from, Vector3 dir, float len)
                                {
                                    dir.Normalize();
                                    mb.Box(from + dir * (len * 0.5f), new Vector3(0.006f, len, 0.006f), col, Quaternion.FromToRotation(Vector3.up, dir));
                                    return from + dir * len;
                                }
                                var tip = Tine(b0, new Vector3(sd * 0.3f, 0.8f, -0.55f), 0.04f * a);
                                Tine(b0 + (tip - b0) * 0.3f, new Vector3(sd * 0.1f, 0.5f, 0.85f), 0.02f * a); // the brow tine
                                Tine(tip, new Vector3(sd * 0.55f, 0.8f, -0.1f), 0.028f * a);
                                Tine(tip, new Vector3(sd * 0.05f, 0.9f, -0.4f), 0.026f * a);
                            }
                        }
                    }
                    break;
                case 5: case 6: // an ear from its root (#504: its own part, so it can turn and flick)
                    {
                        int sd = part == 5 ? -1 : 1;
                        float e = l.earSize;
                        if (l.wideEars)
                            mb.Ico(new Vector3(sd * e * 0.7f, e * 0.3f, 0f), e, new Vector3(1.25f, 0.45f, 0.7f), l.coat, 0.04f, l.belly);
                        else
                        {
                            // upright and pointed, or drooping to the side and down (the dog's drift)
                            var rot = Quaternion.Euler(0f, 0f, -sd * (8f + 110f * l.earDroop)) * Quaternion.Euler(-10f * (1f - l.earDroop), 0f, 0f);
                            mb.Prism(Vector3.zero, e * 0.55f, 0.0006f, e * 1.4f, 4, Color.Lerp(l.saddle, l.coat, 0.4f), rot);
                        }
                    }
                    break;
                case 3: // the tail, hanging from its root along -z (the hang is the transform's)
                    if (l.flag) mb.Ico(new Vector3(0f, 0f, -l.tailLen * 0.45f), l.tailLen * 0.5f, new Vector3(1.0f, 0.55f, 1f), l.belly, 0.05f, l.coat);
                    else if (l.tailCurl > 0.05f)
                    {
                        // a curl up over the back: segments bending up
                        int n = 5; var p = Vector3.zero; float ang = 0f; float seg = l.tailLen / n;
                        for (int i = 0; i < n; i++)
                        {
                            ang += l.tailCurl * 42f;
                            var dir = Quaternion.Euler(-ang, 0f, 0f) * Vector3.back;
                            var c = p + dir * seg * 0.5f;
                            mb.Ico(c, seg * 0.75f, new Vector3(1f, 1f, 1.1f), i == n - 1 && l.patch > 0.3f ? l.patchCol : l.coat, 0.05f);
                            p += dir * seg;
                        }
                    }
                    else
                    {
                        mb.Ico(new Vector3(0f, 0f, -l.tailLen * 0.5f), l.tailLen * 0.5f, new Vector3(0.28f, 0.3f, 1f), l.coat, 0.05f, l.saddle);
                        mb.Ico(new Vector3(0f, 0f, -l.tailLen * 0.92f), l.tailLen * 0.16f, Vector3.one, l.saddle, 0.05f); // the dark tip
                    }
                    break;
                default: // 4 front upper, 8 front lower; 7 hind thigh, 10 hind shin, 9 hind cannon (the foot at each lower end)
                    {
                        bool front = part == 4 || part == 8;
                        bool deer = l.wideEars;
                        float knee = deer ? H * 0.13f : H * 0.19f, foot = deer ? H * 0.1f : H * 0.16f;
                        if (part == 4)
                            mb.Prism(new Vector3(0f, -SegLen(l), 0f), knee * 0.5f, (deer ? H * 0.3f : H * 0.26f) * 0.5f, SegLen(l), 6, l.coat, null, true, false, l.coat);
                        else if (part == 7) // the thigh: thick, mostly inside the haunch
                        {
                            float a = HindLen(l, 0), w = deer ? H * 0.4f : H * 0.32f;
                            mb.Prism(new Vector3(0f, -a, 0f), w * 0.4f, w * 0.5f, a, 6, l.coat, null, true, false, l.coat);
                            if (deer) mb.Ico(new Vector3(0f, -a * 0.3f, -H * 0.04f), H * 0.25f, new Vector3(0.75f, 1.3f, 1.15f), l.coat, 0.03f); // the haunch
                        }
                        else if (part == 10) // the shin: from the stifle back down to the hock
                        {
                            float b = HindLen(l, 1), w = deer ? H * 0.2f : H * 0.22f;
                            mb.Prism(new Vector3(0f, -b, 0f), knee * 0.5f, w * 0.5f, b, 5, l.coat, null, true, false, l.coat);
                        }
                        else
                        {
                            float seg = front ? SegLen(l) : HindLen(l, 2);
                            bool sock = l.kind == QuadKind.Dog && l.patch > 0.45f;
                            var col = sock ? l.patchCol : l.coat;
                            mb.Prism(new Vector3(0f, -seg, 0f), foot * 0.5f, knee * 0.5f, seg, 5, col, null, true, false, col);
                            mb.Box(new Vector3(0f, -seg + 0.004f, 0.002f), new Vector3(foot * 1.15f, 0.008f, foot * 1.4f), deer ? new Color(0.12f, 0.09f, 0.07f) : sock ? l.patchCol : Color.Lerp(l.saddle, l.coat, 0.3f));
                        }
                    }
                    break;
            }
        }

        /// <summary>
        /// A tube along +z through elliptical rings (z, top y, bottom y, half width), faces wound outward, the first and last rings capped.
        /// <paramref name="colorAt"/> picks a face's colour from its z and the sine of its angle round the ring (+1 the top, -1 the underside).
        /// </summary>
        static void Loft(MeshBuilder mb, (float z, float top, float bot, float hw)[] rings, int sides, System.Func<float, float, Color> colorAt)
        {
            int n = rings.Length;
            var pts = new Vector3[n, sides];
            for (int r = 0; r < n; r++)
            {
                float cy = (rings[r].top + rings[r].bot) * 0.5f, ry = (rings[r].top - rings[r].bot) * 0.5f;
                for (int k = 0; k < sides; k++)
                {
                    float a = Mathf.PI * 0.5f + k * Mathf.PI * 2f / sides;
                    pts[r, k] = new Vector3(Mathf.Cos(a) * rings[r].hw, cy + Mathf.Sin(a) * ry, rings[r].z);
                }
            }
            Vector3 Mid(int r) => new(0f, (rings[r].top + rings[r].bot) * 0.5f, rings[r].z);
            for (int r = 0; r < n - 1; r++)
            {
                var inside = (Mid(r) + Mid(r + 1)) * 0.5f;
                for (int k = 0; k < sides; k++)
                {
                    int k1 = (k + 1) % sides;
                    float sa = Mathf.Sin(Mathf.PI * 0.5f + (k + 0.5f) * Mathf.PI * 2f / sides);
                    mb.QuadOut(pts[r, k], pts[r, k1], pts[r + 1, k1], pts[r + 1, k], inside, colorAt((rings[r].z + rings[r + 1].z) * 0.5f, sa));
                }
            }
            for (int k = 0; k < sides; k++)
            {
                int k1 = (k + 1) % sides;
                float sa = Mathf.Sin(Mathf.PI * 0.5f + (k + 0.5f) * Mathf.PI * 2f / sides);
                mb.TriOut(Mid(0), pts[0, k], pts[0, k1], Mid(1), colorAt(rings[0].z, sa));
                mb.TriOut(Mid(n - 1), pts[n - 1, k], pts[n - 1, k1], Mid(n - 2), colorAt(rings[n - 1].z, sa));
            }
        }

        /// <summary>The deer's body: rump, haunch, a deep barrel, the girth and the brisket as one loft; pale belly and white rump, a darker back.</summary>
        static void DeerBody(MeshBuilder mb, QuadLook l)
        {
            float L = l.bodyLen, H = l.BodyH, d = l.fullness / 0.4f; // d: 1 for the adults, 0.75 for the fawn
            (float, float, float, float) R(float z, float top, float bot, float hw) => (z * L, top * H, Mathf.Lerp(0.3f, bot, d) * H, hw * H * Mathf.Lerp(0.85f, 1f, d));
            var rings = new[]
            {
                R(-0.52f, 0.86f, 0.50f, 0.18f), R(-0.45f, 0.98f, 0.16f, 0.40f), R(-0.32f, 1.02f, -0.02f, 0.50f), R(-0.12f, 1.0f, -0.18f, 0.54f),
                R(0.10f, 1.0f, -0.24f, 0.56f), R(0.28f, 1.05f, -0.18f, 0.50f), R(0.40f, 1.10f, -0.02f, 0.42f), R(0.50f, 1.0f, 0.22f, 0.30f),
                R(0.55f, 0.84f, 0.44f, 0.14f),
            };
            Loft(mb, rings, 10, (z, sa) => sa < -0.75f || (z < -0.43f * L && sa < 0.75f) ? l.belly : sa > 0.8f ? l.saddle : l.coat);
            if (l.patch > 0.05f) // the fawn's spots, on the flanks
            {
                var rnd = new System.Random(l.variant * 31 + 5);
                int n = Mathf.RoundToInt(3 + l.patch * 6);
                for (int i = 0; i < n; i++)
                {
                    float z = ((float)rnd.NextDouble() - 0.5f) * L * 0.7f, sd = rnd.NextDouble() < 0.5 ? -1f : 1f;
                    mb.Ico(new Vector3(sd * H * 0.47f, H * (0.55f + 0.35f * (float)rnd.NextDouble()), z), H * 0.06f, new Vector3(0.45f, 1f, 1.2f), l.patchCol, 0.05f);
                }
            }
        }

        /// <summary>The deer's neck: deep at the chest and narrow side to side, tapering to the head, a pale throat.</summary>
        static void DeerNeck(MeshBuilder mb, QuadLook l)
        {
            float H = l.BodyH, nl = l.neckLen;
            var rings = new[]
            {
                (-0.08f * H, 0.24f * H, -0.42f * H, 0.30f * H), (0.15f * nl, 0.22f * H, -0.32f * H, 0.27f * H), (0.55f * nl, 0.18f * H, -0.21f * H, 0.21f * H),
                (nl, 0.17f * H, -0.17f * H, 0.19f * H), (nl + 0.1f * H, 0.09f * H, -0.08f * H, 0.1f * H),
            };
            Loft(mb, rings, 8, (z, sa) => sa < -0.6f ? Color.Lerp(l.coat, l.belly, 0.8f) : l.coat);
        }

        /// <summary>The deer's head: a rounded skull tapering to a long muzzle, a dark nose, a pale chin, eyes and antlers.</summary>
        static void DeerHead(MeshBuilder mb, QuadLook l)
        {
            float hl = l.headLen, r = hl * 0.42f * l.headRound;
            var rings = new[]
            {
                (-0.35f * r, 0.55f * r, -0.45f * r, 0.5f * r), (0f, 0.95f * r, -0.6f * r, 0.78f * r), (0.45f * hl, 0.9f * r, -0.5f * r, 0.66f * r),
                (0.85f * hl, 0.55f * r, -0.42f * r, 0.42f * r), (1.2f * hl, 0.3f * r, -0.4f * r, 0.3f * r),
            };
            Loft(mb, rings, 8, (z, sa) => sa < -0.55f || z > 1.0f * hl ? l.belly : l.coat);
            mb.Ico(new Vector3(0f, 0.02f * r, 1.2f * hl + 0.05f * r), r * 0.24f, new Vector3(1f, 0.8f, 0.7f), new Color(0.07f, 0.06f, 0.05f), 0.02f); // the nose
            foreach (int sd in new[] { -1, 1 })
            {
                mb.Ico(new Vector3(sd * r * 0.72f, r * 0.42f, hl * 0.3f), r * 0.17f, Vector3.one, l.eye, 0.02f);
                if (l.antlers < 0.01f) continue;
                float a = l.antlers;
                var col = new Color(0.86f, 0.78f, 0.62f);
                var b0 = new Vector3(sd * r * 0.4f, r * 0.85f, -hl * 0.1f);
                Vector3 Tine(Vector3 from, Vector3 dir, float len)
                {
                    dir.Normalize();
                    mb.Box(from + dir * (len * 0.5f), new Vector3(0.006f, len, 0.006f), col, Quaternion.FromToRotation(Vector3.up, dir));
                    return from + dir * len;
                }
                var tip = Tine(b0, new Vector3(sd * 0.3f, 0.8f, -0.55f), 0.04f * a);
                Tine(b0 + (tip - b0) * 0.3f, new Vector3(sd * 0.1f, 0.5f, 0.85f), 0.02f * a);
                Tine(tip, new Vector3(sd * 0.55f, 0.8f, -0.1f), 0.028f * a);
                Tine(tip, new Vector3(sd * 0.05f, 0.9f, -0.4f), 0.026f * a);
            }
        }

        /// <summary>The standing doll as one mesh (the ghost's silhouette), facing +z, feet at y 0.</summary>
        public static void Shape(MeshBuilder mb, QuadKind k) => Shape(mb, QuadLook.Of(k));

        public static void Shape(MeshBuilder mb, QuadLook l)
        {
            var root = mb.M;
            var bodyM = root * Matrix4x4.Translate(new Vector3(0f, l.Leg, 0f));
            mb.M = bodyM; BuildPart(mb, l, 0);
            var neckM = bodyM * Matrix4x4.TRS(NeckBase(l), Quaternion.Euler(-l.neckRise, 0f, 0f), Vector3.one);
            mb.M = neckM; BuildPart(mb, l, 1);
            var headM = neckM * Matrix4x4.TRS(new Vector3(0f, 0f, l.neckLen), Quaternion.Euler(l.neckRise, 0f, 0f), Vector3.one);
            mb.M = headM; BuildPart(mb, l, 2);
            for (int i = 0; i < 2; i++) { mb.M = headM * Matrix4x4.Translate(EarAt(l, i == 0 ? -1 : 1)); BuildPart(mb, l, 5 + i); }
            mb.M = bodyM * Matrix4x4.TRS(new Vector3(0f, l.BodyH * 0.78f, -l.bodyLen * 0.47f), Quaternion.Euler(l.tailHang, 0f, 0f), Vector3.one); BuildPart(mb, l, 3);
            for (int i = 0; i < 4; i++)
            {
                bool front = i < 2;
                var upM = bodyM * Matrix4x4.TRS(LegAt(l, i), Quaternion.Euler(front ? 0f : Thigh, 0f, 0f), Vector3.one);
                mb.M = upM; BuildPart(mb, l, front ? 4 : 7);
                if (front) { mb.M = upM * Matrix4x4.Translate(new Vector3(0f, -SegLen(l), 0f)); BuildPart(mb, l, 8); continue; }
                var shinM = upM * Matrix4x4.TRS(new Vector3(0f, -HindLen(l, 0), 0f), Quaternion.Euler(Stifle, 0f, 0f), Vector3.one);
                mb.M = shinM; BuildPart(mb, l, 10);
                mb.M = shinM * Matrix4x4.TRS(new Vector3(0f, -HindLen(l, 1), 0f), Quaternion.Euler(HockJ, 0f, 0f), Vector3.one); BuildPart(mb, l, 9);
            }
            mb.M = root;
        }

        /// <summary>Old placeholder signature (the #424 arrivals): head pitch + down to graze, - up to howl.</summary>
        public void Pose(Vector3 at, float yawDeg, float phase, float trot, float headPitch, float headYaw, float lie, float sit, float tailUp, float scale = 1f)
        {
            var p = new QuadPose
            {
                at = at, yaw = yawDeg, phase = phase, gait = trot, neck = headPitch * 0.9f, head = headPitch * 0.5f, headYaw = headYaw,
                lie = lie, sit = sit, tail = tailUp, scale = scale,
                lift = float.IsNaN(phase) ? 0f : Mathf.Abs(Mathf.Sin(phase)) * Mathf.Lerp(0.004f, 0.0075f, trot),
            };
            Apply(p);
        }

        /// <summary>Puts the doll in a pose (parent-local).</summary>
        public void Apply(in QuadPose p)
        {
            var t = go.transform;
            t.localPosition = p.at;
            t.localRotation = Quaternion.Euler(0f, p.yaw, 0f);
            t.localScale = Vector3.one * Mathf.Max(0.0001f, p.scale);
            float leg = look.Leg, H = look.BodyH;
            bool moving = !float.IsNaN(p.phase);
            float swing = moving ? (p.gait >= 1.5f ? 46f : Mathf.Lerp(26f, 32f, p.gait)) : 0f;
            float down = Mathf.Max(p.lie, p.curl);
            // the body: lowered by lying and sitting, pitched by the bow and the sit, lifted by the bob or a leap
            float y = leg * (1f - 0.82f * down) - p.sit * leg * 0.35f - p.bow * leg * 0.2f + p.lift;
            if (moving && p.gait >= 1.5f) y -= leg * 0.2f * Mathf.Max(0f, -Mathf.Sin(p.phase)); // gathered: the angled legs reach less far down
            body.localPosition = new Vector3(0f, y, 0f);
            body.localRotation = Quaternion.Euler(p.bodyPitch - p.sit * 34f + p.bow * 20f, p.curl * 0f, p.roll);
            // the standing hind leg's bends (thigh forward, shin back, cannon down) fade out as the legs fold
            float bend = 1f - Mathf.Clamp01(down + p.sit + p.bow);
            for (int i = 0; i < 4; i++)
            {
                bool front = i < 2;
                float upper = 0f, mid = 0f, lower = 0f;
                if (moving)
                {
                    if (p.gait >= 1.5f)
                    {
                        // the bound / gallop: e = 1 stretched out (fronts reach forward, hinds push back), -1 gathered under the body
                        // (the knees and hocks tuck). The right pair leads the left a little; the hinds lag the fronts by p.lead.
                        float ph = p.phase + (i % 2) * 0.55f - (front ? 0f : p.lead);
                        float e = Mathf.Sin(ph);
                        // a leg folds while it swings back to the front (the front's knee as it reaches, the hind's hock as it comes under)
                        float tuck = front ? Mathf.Max(0f, Mathf.Cos(ph)) : Mathf.Max(0f, -Mathf.Cos(ph));
                        upper = front ? -e * swing * 0.85f - 4f : e * swing + 4f;
                        if (front) lower = tuck * 85f; else { mid = tuck * 40f; lower = -tuck * 55f; }
                    }
                    else
                    {
                        // walk: the lateral sequence (LH, LF, RH, RF, a quarter apart, each front half a cycle from the other);
                        // trot: diagonal pairs. A leg swinging forward lifts its foot: the knee folds back, the hock folds forward.
                        float off = Mathf.Lerp(WalkOff[i], TrotOff[i], Mathf.Clamp01(p.gait));
                        float s = Mathf.Sin(p.phase + off), c = Mathf.Cos(p.phase + off);
                        float lift = Mathf.Max(0f, -c);
                        upper = s * swing - lift * (front ? 10f : 8f);
                        if (front) lower = lift * 62f; else { mid = lift * 32f; lower = -lift * 42f; }
                    }
                }
                float fold = front ? down * 88f + p.bow * 55f + p.sit * 34f : down * -96f + p.sit * SitThigh;
                if (!front) { upper += Thigh * bend; mid += Stifle * bend + p.sit * SitStifle; lower += HockJ * bend + p.sit * SitHock; }
                float splay = front && p.bodyPitch > 3f && !moving ? -p.bodyPitch * 1.2f : 0f; // front legs splay a little to reach down
                legs[i].localRotation = Quaternion.Euler(upper + fold + splay, 0f, front ? 0f : p.curl * (i % 2 == 0 ? 20f : -20f));
                if (!front) mids[i].localRotation = Quaternion.Euler(mid, 0f, 0f);
                lowers[i].localRotation = Quaternion.Euler(lower, 0f, 0f);
            }
            neck.localRotation = Quaternion.Euler(-look.neckRise + p.neck - p.bodyPitch * 0.5f + p.sit * 30f, p.headYaw * 0.45f + p.curl * 40f, 0f);
            head.localRotation = Quaternion.Euler(look.neckRise + p.head, p.headYaw * 0.55f, 0f);
            tail.localRotation = Quaternion.Euler(look.tailHang - p.tail, p.wag, 0f);
            // the ears: a flick turns the ear out and down about its root, the swivel turns both toward (+) or away from (-) the front
            for (int i = 0; i < 2; i++)
            {
                float sd = i == 0 ? -1f : 1f, flick = i == 0 ? p.earL : p.earR;
                ears[i].localRotation = Quaternion.Euler(0f, -sd * p.earSwivel, -sd * flick);
            }
        }

        /// <summary>Where the head is now (world): the cause a person turns to, the shots.</summary>
        public Vector3 HeadWorld => head.position;

        /// <summary>#504: the muzzle's tip (world), for the graze tests and shots: it should touch the grass when the head is down.</summary>
        public Vector3 MuzzleWorld => head.TransformPoint(new Vector3(0f, 0f, look.headLen * 0.8f));

        public void SetActive(bool on) { if (go.activeSelf != on) go.SetActive(on); }

        public void Destroy() { if (go == null) return; if (Application.isPlaying) Object.Destroy(go); else Object.DestroyImmediate(go); } // edit-mode shots and tests build dolls too
    }
}
