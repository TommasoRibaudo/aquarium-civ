using AquariumCiv.Lighting;
using UnityEngine;

namespace AquariumCiv.View
{
    /// <summary>
    /// The Keeper's tree (#317), one hand-built low-poly model per growth stage (0 seed in a mound, 1 sprout, 2 sapling,
    /// 3 young tree, 4 grown). Every stage stands in a ring of small stones on dark, turned soil, in a fresher, more saturated green
    /// than the meadow, so the eye finds it; from stage 3 on it carries pale blossoms that become gold buds. The leaves sway with
    /// the wind (vertex sway); at night the canopy, the sprout and the buds glow very faintly through the lamp channel of the Stylized
    /// shader (phase 2 + strength, see <see cref="GlowChannel"/>), which follows the box's night level.
    /// </summary>
    public static class KeeperTreeKit
    {
        public static readonly Color Soil = LandColors.C(0x5A4331), SoilLight = LandColors.C(0x7A5E42);
        public static readonly Color LeafFresh = LandColors.C(0x7CCB45), LeafLight = LandColors.C(0xA3DE5C), LeafDeep = LandColors.C(0x55AE3C);
        public static readonly Color Trunk = LandColors.C(0x7A5640), Blossom = LandColors.C(0xFFF0C4), Bud = LandColors.C(0xFFD66E), Petal = LandColors.C(0xF7B9C6);

        /// <summary>Glow strengths: the share of the warm lit colour mixed in at full night (0.12 = barely there).</summary>
        public const float CanopyGlow = 0.12f, BudGlow = 0.5f;

        /// <summary>How far the tree glows at this hour, 0 by day and 1 at night, easing through the dusk and the dawn (the
        /// same curve the shader applies to the box's lamp level).</summary>
        public static float Glow(float hour) => GlowOf(DayLight.LampLevel(hour));

        public static float GlowOf(float lampLevel)
        {
            float t = Mathf.Clamp01((lampLevel - 0.15f) / 0.7f);
            return t * t * (3f - 2f * t);
        }

        /// <summary>#525: the ground pool under the tree at night (BoxView.Glows): from this stage, this wide, this strong (a lantern's is 1, the fire's much more),
        /// switching on with the lamp level at <see cref="PoolPhase"/> (threshold 0.12 + 0.6 x phase = 0.15, where <see cref="GlowOf"/> starts).</summary>
        public const int PoolStage = 3;
        public const float PoolPhase = 0.05f, PoolStrength = 0.3f;
        public static float PoolRadius(int stage) => stage >= 4 ? 0.5f : 0.4f;

        /// <summary>The lamp channel value for a soft glow of this strength: 2 + strength.</summary>
        public static float GlowChannel(float strength) => 2f + Mathf.Clamp01(strength);

        /// <summary>Builds the tree at ground point <paramref name="g"/>. <paramref name="need"/>: 1 thirst (fallen leaves at its
        /// foot), 3 weeds (tufts crowding it).</summary>
        public static void Build(MeshBuilder mb, Vector3 g, int stage, int need, LandColors co, Land land = null)
        {
            stage = Mathf.Clamp(stage, 0, 4);
            // #609: with the land at hand the pad, mound and stones lie on the slope; the plant stays upright at the middle.
            var tilt = Quaternion.identity; float lift = 0f;
            if (land != null) PadFit(land, g.x, g.z, out tilt, out lift);
            Ring(mb, g, stage, tilt, lift);
            switch (stage)
            {
                case 0: Seed(mb, g); break;
                case 1: Sprout(mb, g); break;
                default: Grown(mb, g, stage); break;
            }
            Needs(mb, g, stage, need, co);
        }

        // A mound of dark soil with a ring of stones round it: bigger as the tree grows.
        static void Ring(MeshBuilder mb, Vector3 g, int stage, Quaternion tilt, float lift)
        {
            var prevM = mb.M;
            mb.M = prevM * Matrix4x4.Translate(g + Vector3.up * lift) * Matrix4x4.Rotate(tilt) * Matrix4x4.Translate(-g);
            RingParts(mb, g, stage);
            mb.M = prevM;
        }

        /// <summary>Half the pad's width: where the land is sampled for the slope.</summary>
        public const float PadSpan = 0.2f;
        /// <summary>The pad never leans past this, however steep the ground (the plant stays upright anyway).</summary>
        public const float MaxTiltDeg = 40f;
        public const float MaxPadLift = 0.03f;

        /// <summary>The ground normal from the heights a span east, west, north (+z) and south of a point (central differences).</summary>
        public static Vector3 PadNormal(float hE, float hW, float hN, float hS, float span)
            => new Vector3(-(hE - hW) / (2f * span), 1f, -(hN - hS) / (2f * span)).normalized;

        /// <summary>The pad's lean from the ground normal (capped at <see cref="MaxTiltDeg"/>) and the lift that keeps its rim out of
        /// a hollow: the plane through the middle misses a curved ground by the mean of opposite samples minus the middle.</summary>
        public static void PadFit(float hC, float hE, float hW, float hN, float hS, float span, out Quaternion tilt, out float lift)
        {
            var n = PadNormal(hE, hW, hN, hS, span);
            tilt = Quaternion.RotateTowards(Quaternion.identity, Quaternion.FromToRotation(Vector3.up, n), MaxTiltDeg);
            float miss = Mathf.Max((hE + hW) * 0.5f - hC, (hN + hS) * 0.5f - hC);
            lift = Mathf.Clamp(miss, 0f, MaxPadLift);
        }

        public static void PadFit(Land land, float x, float z, out Quaternion tilt, out float lift)
            => PadFit(land.Height(x, z), land.Height(x + PadSpan, z), land.Height(x - PadSpan, z), land.Height(x, z + PadSpan), land.Height(x, z - PadSpan), PadSpan, out tilt, out lift);

        static void RingParts(MeshBuilder mb, Vector3 g, int stage)
        {
            // #317 / tester: the seed and the sprout were lost at the default view. They get a wider ring and a halo of fresh
            // green moss, so the one spot the Keeper planted reads at a glance on bare sand or among the grass.
            float r = stage < 2 ? 0.1f : 0.06f + 0.035f * stage;
            if (stage < 2) mb.Ico(g + Vector3.up * 0.001f, r * 2f, new Vector3(1f, 0.06f, 1f), LeafFresh, 0.12f, LeafLight);
            mb.Ico(g + Vector3.up * 0.002f, r * 0.82f, new Vector3(1f, 0.22f, 1f), Soil, 0.1f, Soil);
            int n = 6 + stage * 2;
            for (int i = 0; i < n; i++)
            {
                float a = (i + mb.Range(-0.2f, 0.2f)) / n * Mathf.PI * 2f;
                float sz = 0.016f + 0.008f * mb.Range(0f, 1f) + 0.002f * stage;
                var c = i % 3 == 0 ? Palette.RockLight : i % 3 == 1 ? Palette.Stone : Palette.Rock;
                mb.Ico(g + new Vector3(Mathf.Cos(a) * r, sz * 0.15f, Mathf.Sin(a) * r), sz, new Vector3(1.15f, 0.65f, 1f), c, 0.2f, Palette.RockDark);
            }
        }

        static void Seed(MeshBuilder mb, Vector3 g)
        {
            // A plump seed half-buried in the mound, with a glint of gold (it glows, very faintly, at night).
            mb.lamp = GlowChannel(BudGlow);
            mb.Ico(g + Vector3.up * 0.024f, 0.028f, new Vector3(1f, 0.8f, 1.4f), Bud, 0.08f, Soil);
            mb.Ico(g + new Vector3(0f, 0.04f, 0.007f), 0.013f, Vector3.one, Blossom, 0.05f);
            mb.lamp = -1f;
        }

        static void Sprout(MeshBuilder mb, Vector3 g)
        {
            // A bright stem with two fat seed leaves, tipped with a bud.
            mb.sway = 0.4f;
            const float e = 1.9f; // #317: big enough to find at the default view
            mb.Prism(g, 0.006f * e, 0.0045f * e, 0.07f * e, 5, LeafLight);
            mb.sway = 1f;
            mb.lamp = GlowChannel(CanopyGlow * 1.6f);
            for (int i = 0; i < 2; i++)
            {
                float s = i == 0 ? 1f : -1f;
                mb.Ico(g + new Vector3(0.026f * s, 0.07f, 0.004f * s) * e, 0.03f * e, new Vector3(1.4f, 0.28f, 0.9f), i == 0 ? LeafFresh : LeafLight, 0.1f, LeafDeep);
            }
            mb.Ico(g + Vector3.up * 0.09f * e, 0.014f * e, new Vector3(0.9f, 1.3f, 0.9f), LeafLight, 0.1f);
            mb.lamp = -1f;
            mb.sway = 0f;
        }

        static void Grown(MeshBuilder mb, Vector3 g, int stage)
        {
            float k = stage == 2 ? 0.45f : stage == 3 ? 0.72f : 1.15f;      // overall scale of the same plan
            float trunkH = stage == 2 ? 0.17f : stage == 3 ? 0.26f : 0.4f;
            float rootR = 0.034f * k, topR = 0.016f * k + 0.004f;
            // The trunk: a root flare, then a tapering stem with a slight lean.
            mb.Prism(g - Vector3.up * 0.005f, rootR * 1.5f, rootR, 0.05f * k, 7, Trunk);
            var lean = new Vector3(0.012f, 0f, 0.006f) * k;
            mb.Prism(g + Vector3.up * 0.04f * k, rootR, topR, trunkH, 7, Trunk, Quaternion.FromToRotation(Vector3.up, (Vector3.up * trunkH + lean).normalized));
            if (stage >= 3)
            {
                // Two branch stubs under the crown.
                for (int i = 0; i < 2; i++)
                {
                    float a = 1.2f + i * 3.3f;
                    var dir = new Vector3(Mathf.Cos(a), 1.1f, Mathf.Sin(a)).normalized;
                    mb.Prism(g + Vector3.up * trunkH * (0.55f + 0.12f * i), topR * 0.7f, topR * 0.4f, 0.1f * k, 5, Trunk, Quaternion.FromToRotation(Vector3.up, dir));
                }
            }
            var top = g + lean + Vector3.up * (0.04f * k + trunkH);
            // The crown: rounded clumps of leaves in three greens, a bright one on top. Phase 2 + strength lights them at night.
            mb.sway = 1f;
            mb.lamp = GlowChannel(CanopyGlow);
            int clumps = stage == 2 ? 4 : stage == 3 ? 5 : 7;
            float cr = stage == 2 ? 0.075f : stage == 3 ? 0.1f : 0.15f;
            mb.Ico(top + Vector3.up * cr * 0.55f, cr * 1.05f, new Vector3(1.1f, 0.9f, 1.1f), LeafFresh, 0.16f, LeafDeep);
            for (int i = 0; i < clumps - 1; i++)
            {
                float a = i / (float)(clumps - 1) * Mathf.PI * 2f + 0.5f;
                float up = i % 2 == 0 ? 0.35f : 0.8f;
                var c = i % 3 == 0 ? LeafLight : i % 3 == 1 ? LeafFresh : LeafDeep;
                mb.Ico(top + new Vector3(Mathf.Cos(a) * cr * 0.95f, cr * up, Mathf.Sin(a) * cr * 0.95f), cr * 0.62f, Vector3.one, c, 0.18f, LeafDeep);
            }
            mb.Ico(top + new Vector3(0.01f, cr * 1.35f, -0.01f) * k, cr * 0.55f, Vector3.one, LeafLight, 0.15f);
            mb.lamp = -1f;
            // Blossoms (young) and gold buds (grown) dotted over the crown: tiny, bright, and the most visible part of the glow.
            int blooms = stage == 2 ? 0 : stage == 3 ? 4 : 9;
            for (int i = 0; i < blooms; i++)
            {
                float a = i * 2.4f + 0.3f, e = 0.25f + 0.6f * ((i * 5) % 7) / 6f;
                var dir = new Vector3(Mathf.Cos(a) * Mathf.Cos(e), Mathf.Sin(e) + 0.15f, Mathf.Sin(a) * Mathf.Cos(e)).normalized;
                var at = top + Vector3.up * cr * 0.7f + Vector3.Scale(dir, new Vector3(cr * 1.45f, cr * 1.25f, cr * 1.45f));
                mb.lamp = GlowChannel(BudGlow);
                var col = stage == 3 ? (i % 2 == 0 ? Blossom : Petal) : (i % 3 == 0 ? Petal : i % 3 == 1 ? Bud : Blossom);
                mb.Ico(at, 0.0085f * (stage == 3 ? 1f : 1.2f), Vector3.one, col, 0.08f);
                mb.lamp = -1f;
            }
            mb.sway = 0f;
        }

        static void Needs(MeshBuilder mb, Vector3 g, int stage, int need, LandColors co)
        {
            float foot = 0.1f + 0.035f * stage;
            if (need == 1)
                for (int i = 0; i < 5; i++)
                {
                    float a = i * 1.3f;
                    mb.Ico(g + new Vector3(Mathf.Cos(a) * foot, 0.006f, Mathf.Sin(a) * foot), 0.014f, new Vector3(1f, 0.3f, 1f), Palette.Ember, 0.1f);
                }
            else if (need == 3)
                for (int i = 0; i < 5; i++)
                {
                    float a = i * 1.25f + 0.4f;
                    Pieces.GrassTuft(mb, g + new Vector3(Mathf.Cos(a) * foot, 0f, Mathf.Sin(a) * foot), 0.07f, co.tuftC);
                }
        }
    }
}
