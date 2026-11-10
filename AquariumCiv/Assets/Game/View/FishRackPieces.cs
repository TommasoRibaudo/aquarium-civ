using AquariumCiv.Life;
using UnityEngine;

namespace AquariumCiv.View
{
    /// <summary>
    /// #543: the fish chain's code-built pieces (a set of their own, so nothing else's merge meets them; the hide rack's are in Pieces.cs).
    /// <list type="bullet">
    /// <item><see cref="FishBasket"/>: a tall, narrow creel of grey-green reed with a pale rim and tails sticking out (the berry basket
    /// is a low straw bowl with a red top), carried in the hands (<see cref="PropKind.FishBasket"/>) and set down beside a fisher.</item>
    /// <item><see cref="Rack"/> and <see cref="HangingFish"/>: one slim A-frame rack (a ridge pole in two crossed-pole forks) with the fish
    /// hung by their tails from cords along the pole, silver-blue when fresh and brown, thin and curled when dry. The one rack of the
    /// camp: the fishers' catch dries on it, and #429's smoke drifts off it.</item>
    /// <item><see cref="DriedFish"/>: one dry fish lying flat, for the food pile.</item>
    /// </list>
    /// All coordinates are box units; the rack is built in its own space (bar along local z, origin on the ground at its middle).
    /// </summary>
    public static class FishRackPieces
    {
        /// <summary>The ridge pole's height, and the most fish along it (the sim's rack holds <see cref="Sim.SimCore.FishRackMax"/>).</summary>
        public const float Height = 0.19f;
        public const int MaxFish = Sim.SimCore.FishRackMax;
        /// <summary>A fish's length hanging, and the gap between two (the bar is <c>2 * FishRackSpot.HalfLength</c> long).</summary>
        public const float FishLength = 0.05f;
        public static float Spacing => (2f * FishRackSpot.HalfLength - 0.04f) / (MaxFish - 1);

        static readonly Color Wood = new Color(0.40f, 0.28f, 0.16f), WoodDark = new Color(0.28f, 0.19f, 0.11f), Cord = new Color(0.93f, 0.86f, 0.68f);
        static readonly Color Reed = new Color(0.50f, 0.56f, 0.47f), ReedDark = new Color(0.36f, 0.42f, 0.35f), Rim = new Color(0.82f, 0.78f, 0.62f);
        static readonly Color FreshBack = new Color(0.43f, 0.55f, 0.62f), FreshBelly = new Color(0.86f, 0.89f, 0.88f), FreshTail = new Color(0.36f, 0.48f, 0.56f);
        static readonly Color DryBack = new Color(0.40f, 0.24f, 0.11f), DryBelly = new Color(0.62f, 0.42f, 0.20f), DryTail = new Color(0.30f, 0.18f, 0.08f);

        // ---------------------------------------------------------------- the basket

        /// <summary>The fish basket in its own space (origin at its middle, as <see cref="PropKind.Basket"/>): about 0.045 tall, 0.07 across the rim.</summary>
        public static void FishBasket(MeshBuilder mb)
        {
            float jit = mb.jitter; mb.jitter = 0.02f;
            mb.Prism(new Vector3(0f, -0.024f, 0f), 0.022f, 0.034f, 0.046f, 7, Reed, topCol: ReedDark);                    // the creel: narrow foot, wider mouth
            mb.Prism(new Vector3(0f, 0.016f, 0f), 0.036f, 0.036f, 0.007f, 7, Rim, topCol: Rim);                            // the pale rim
            mb.Prism(new Vector3(0f, -0.004f, 0f), 0.0285f, 0.0285f, 0.004f, 7, ReedDark, topCol: ReedDark);              // a band of dark weave
            // a handle: an arch over the mouth, side to side
            for (int i = 0; i < 4; i++)
            {
                float a0 = i / 4f * Mathf.PI, a1 = (i + 1) / 4f * Mathf.PI;
                Vector3 p0 = new Vector3(Mathf.Cos(a0) * 0.034f, 0.020f + Mathf.Sin(a0) * 0.032f, 0f), p1 = new Vector3(Mathf.Cos(a1) * 0.034f, 0.020f + Mathf.Sin(a1) * 0.032f, 0f);
                Pole(mb, p0, p1, 0.006f, WoodDark);
            }
            // the catch: tails and a silver flank sticking out of the mouth
            mb.Ico(new Vector3(-0.010f, 0.022f, 0.004f), 0.014f, new Vector3(0.55f, 0.8f, 1.5f), FreshBelly, 0.04f);
            mb.Ico(new Vector3(0.012f, 0.024f, -0.006f), 0.013f, new Vector3(0.55f, 0.8f, 1.5f), FreshBack, 0.04f);
            mb.Box(new Vector3(-0.008f, 0.030f, -0.022f), new Vector3(0.004f, 0.016f, 0.012f), FreshTail, Quaternion.Euler(25f, 0f, 10f));
            mb.Box(new Vector3(0.014f, 0.032f, 0.020f), new Vector3(0.004f, 0.016f, 0.012f), FreshTail, Quaternion.Euler(-25f, 0f, -10f));
            mb.jitter = jit;
        }

        // ---------------------------------------------------------------- the rack

        /// <summary>The frame: two A-frames of crossed poles at the ends and a ridge pole in their forks, with a cord's loop at each fish place
        /// the sim's rack could hold (<paramref name="cords"/> of them show, so an empty rack has none). Bar along z.</summary>
        public static void Rack(MeshBuilder mb, int cords)
        {
            float sj = mb.jitter, ss = mb.sway; mb.jitter = 0f; mb.sway = 0f;
            float h = Height, half = FishRackSpot.HalfLength;
            foreach (float s in new[] { -1f, 1f })
            {
                float z = s * half;
                for (int k = 0; k < 2; k++)
                {
                    float side = k == 0 ? -1f : 1f;
                    Pole(mb, new Vector3(side * 0.05f, -0.004f, z), new Vector3(-side * 0.012f, h + 0.03f, z), 0.014f, k == 0 ? Wood : WoodDark);
                }
                mb.Box(new Vector3(0f, h - 0.012f, z), new Vector3(0.022f, 0.010f, 0.016f), Cord); // the lashing where they cross
            }
            mb.Box(new Vector3(0f, h, 0f), new Vector3(0.016f, 0.016f, half * 2f + 0.06f), Wood); // the ridge pole in the forks
            mb.Box(new Vector3(0f, h * 0.45f, 0f), new Vector3(0.010f, 0.010f, half * 2f), WoodDark); // a low spreader between the frames
            for (int i = 0; i < Mathf.Clamp(cords, 0, MaxFish); i++)
            {
                var top = HangPoint(i);
                mb.Box(top + new Vector3(0f, -0.006f, 0f), new Vector3(0.003f, 0.012f, 0.003f), Cord);
            }
            mb.jitter = sj; mb.sway = ss;
        }

        /// <summary>Where the i-th fish hangs from the ridge pole (the cord's top, in the rack's space). Fish are laid out from the middle outwards,
        /// so a few fish hang together in the middle and the rack fills to its ends.</summary>
        public static Vector3 HangPoint(int i)
        {
            int ring = (i + 1) / 2; // 0, 1, 1, 2, 2, ...
            float z = (i % 2 == 0 ? 1f : -1f) * ring * Spacing;
            if (i == 0) z = 0f;
            return new Vector3(0f, Height - 0.008f, z);
        }

        /// <summary>One fish hung by its tail from <paramref name="top"/> (a <see cref="HangPoint"/>): head down, flat side across the bar. Fresh ones are
        /// plump and silver-blue; dry ones are thinner, curled a little and brown. <paramref name="seed"/> makes each a little different.</summary>
        public static void HangingFish(MeshBuilder mb, Vector3 top, bool dry, int seed)
        {
            float sj = mb.jitter; mb.jitter = 0f;
            uint r = (uint)(seed * 2654435761u + 977u);
            float len = FishLength * (dry ? 0.86f : 1f) * (0.92f + ((r >> 8) & 255) / 255f * 0.16f);
            float sway = (((r >> 16) & 255) / 255f - 0.5f) * (dry ? 10f : 6f);
            var q = Quaternion.Euler(0f, 0f, sway);
            Vector3 P(float x, float y, float z) => top + q * new Vector3(x, y, z);
            var back = dry ? DryBack : FreshBack; var belly = dry ? DryBelly : FreshBelly; var tail = dry ? DryTail : FreshTail;
            float w = dry ? 0.007f : 0.011f;     // across the bar: thin, the fish hangs flat side to the sides
            mb.Box(P(0f, -0.004f, 0f), new Vector3(0.004f, 0.012f, 0.004f), tail);                               // the tail root, tied to the cord
            mb.Ico(P(0f, -len * 0.5f - 0.004f, 0f), len * 0.5f, new Vector3(w / (len * 0.5f), 1f, (dry ? 0.30f : 0.42f)), back, 0.02f, belly); // the body
            mb.Ico(P(0f, -len - 0.004f, 0f), 0.006f, new Vector3(0.55f, 0.9f, 0.9f), back, 0.02f);              // the head, at the bottom
            mb.Box(P(0f, -0.006f, 0f), new Vector3(0.002f, 0.010f, 0.016f), tail, Quaternion.Euler(0f, 0f, 0f)); // the tail fin
            if (!dry) mb.Box(P(0.0034f, -len - 0.004f, 0.002f), new Vector3(0.0015f, 0.0025f, 0.0025f), new Color(0.12f, 0.12f, 0.14f)); // an eye on the near side
            mb.jitter = sj;
        }

        /// <summary>One dry fish lying flat, for the food pile (0.051 long along x, 0.014 across, 0.01 thick).</summary>
        public const float DriedHalfLength = 0.026f, DriedHalfWidth = 0.008f;
        static readonly Color PileBack = new Color(0.42f, 0.47f, 0.52f), PileBelly = new Color(0.86f, 0.86f, 0.78f), PileTail = new Color(0.30f, 0.34f, 0.40f);
        public static void DriedFish(MeshBuilder mb)
        {
            // pale silver-grey with a dark tail: stands apart from the sand, the logs and the dark mat they lie on
            float sj = mb.jitter; mb.jitter = 0.02f;
            mb.Ico(new Vector3(0f, 0.006f, 0f), 0.017f, new Vector3(1.1f, 0.30f, 0.46f), PileBack, 0.03f, PileBelly);
            mb.Ico(new Vector3(0.0195f, 0.006f, 0f), 0.005f, new Vector3(0.9f, 0.7f, 0.9f), PileBack, 0.02f);
            mb.Box(new Vector3(-0.0215f, 0.005f, 0f), new Vector3(0.008f, 0.003f, 0.015f), PileTail);
            mb.jitter = sj;
        }

        static void Pole(MeshBuilder mb, Vector3 a, Vector3 b, float thick, Color col)
        {
            var dir = b - a;
            if (dir.sqrMagnitude < 1e-10f) return;
            mb.Box((a + b) * 0.5f, new Vector3(thick, dir.magnitude, thick), col, Quaternion.FromToRotation(Vector3.up, dir.normalized));
        }
    }
}
