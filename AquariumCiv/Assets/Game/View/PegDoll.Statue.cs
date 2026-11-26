// #776 (animation-specs B81.1, approved 2026-11-26): the founder's statue, code-built in the house style: a two-step stone plinth and the founder's
// peg doll carved in one grey, with its tell in the hand. No imported assets. Every size is [est] until the clip.
using AquariumCiv.Sim;
using UnityEngine;

namespace AquariumCiv.View
{
    public static partial class PegDoll
    {
        /// <summary>The statue's stone: the name stone's grey in three tones (the figure is one flat grey: no skin or tunic colours).</summary>
        public static readonly Color StatueStone = Color.Lerp(Palette.Stone, Palette.Rock, 0.35f), StatueStoneLit = Palette.RockLight, StatueStoneDark = Palette.RockDark;
        /// <summary>The plinth's two steps (box units), where the figure's feet stand on it, and the figure's size (1.3 x a person: 0.28 u of doll).</summary>
        public const float StatueLowW = 0.14f, StatueStepH = 0.03f, StatueUpW = 0.09f, StatueFeetY = 0.06f, StatueScale = 1.3f;
        /// <summary>The whole carved statue's height above the ground (box units): the plinth and the 1.3 x doll.</summary>
        public static float StatueHeight => StatueFeetY + (HeadY + HeadR) * StatueScale;

        /// <summary>
        /// #776 (B81.1): the founder's statue: a two-step stone plinth (the lower step chipped at a back corner) and, on it, the founder's peg doll
        /// carved in one grey: a six-sided body and a head about 1.3 x a person, with its tell in the hand (the job's tool in stone, the leader's crook,
        /// the kind one's open hand; <paramref name="tell"/> null: from the person's job and trait). The plinth's foot at <paramref name="at"/>, facing
        /// local -z. <paramref name="stage"/> is the raising's progress (the block, the shoulders, the head, the carved figure); <paramref name="wear"/>
        /// the season's (view only). <paramref name="s"/> scales all.
        /// </summary>
        public static void Statue(MeshBuilder mb, Vector3 at, float s, Notable who, StatueStage stage = StatueStage.Carved, StatueWear wear = StatueWear.None, StatueRules.Tell? tell = null)
        {
            var stone = StatueStone; var lit = StatueStoneLit; var dark = StatueStoneDark;
            float step = StatueStepH * s, low = StatueLowW * s, up = StatueUpW * s;
            // the lower step: one corner (the back left) chipped away, a stub left; the upper step a shade lighter
            mb.Box(at + new Vector3(0f, step * 0.5f, -0.02f * s), new Vector3(low, step, low - 0.04f * s), stone);
            mb.Box(at + new Vector3(-0.02f * s, step * 0.5f, low * 0.5f - 0.02f * s), new Vector3(low - 0.04f * s, step, 0.04f * s), stone);
            mb.Box(at + new Vector3(low * 0.5f - 0.02f * s, step * 0.25f, low * 0.5f - 0.02f * s), new Vector3(0.012f * s, step * 0.5f, 0.012f * s), dark);
            mb.Box(at + new Vector3(0f, step * 1.5f, 0f), new Vector3(up, step, up), lit);
            var feet = at + new Vector3(0f, StatueFeetY * s, 0f);
            if (stage == StatueStage.Plinth) { StatueLeaves(mb, at, s, wear); return; }
            if (stage == StatueStage.Block)
            {
                mb.Box(feet + new Vector3(0f, 0.05f * s, 0f), new Vector3(0.06f * s, 0.1f * s, 0.06f * s), stone);
                if (wear == StatueWear.Snow) mb.Box(feet + new Vector3(0f, 0.1f * s, 0f), new Vector3(0.064f * s, 0.01f * s, 0.064f * s), Color.white);
                StatueLeaves(mb, at, s, wear);
                return;
            }
            float f = StatueScale * s, bodyH = BodyH * f;
            if (stage == StatueStage.Rough)
            {   // the shoulders cut into the block: a six-sided body a block's width, a ridge of the block still on top
                mb.Prism(feet, 0.05f * s, 0.045f * s, bodyH, 6, stone, topCol: stone);
                mb.Box(feet + new Vector3(0f, bodyH + 0.01f * s, 0f), new Vector3(0.05f * s, 0.022f * s, 0.05f * s), dark);
                StatueLeaves(mb, at, s, wear);
                return;
            }
            // [est] a slimmer body than a person's (0.04 u radius at the foot instead of 0.055 x 1.3) so the feet fit the upper step (0.09 u wide)
            mb.Prism(feet, 0.04f * f, 0.03f * f, bodyH, 6, stone, topCol: stone);
            mb.Prism(feet + new Vector3(0f, bodyH * 0.42f, 0f), 0.036f * f, 0.034f * f, 0.012f * f, 6, dark); // the belt, cut shallow
            mb.Ico(feet + new Vector3(0f, HeadY * f, 0f), HeadR * f, Vector3.one, stone, stage == StatueStage.Headed ? 0.16f : 0.06f, lit);
            if (stage == StatueStage.Carved)
            {
                var kind = tell ?? (who != null ? StatueRules.TellOf(who.job, who.trait, false) : StatueRules.Tell.Nothing);
                StatueArms(mb, feet, f, kind, stone, dark);
            }
            if (wear == StatueWear.Snow)
            {
                mb.Ico(feet + new Vector3(0f, HeadY * f + HeadR * f * 0.75f, 0f), 0.04f * s, new Vector3(1.2f, 0.7f, 1.2f), Color.white, 0.05f); // the cap on the head
                mb.Box(feet + new Vector3(0f, bodyH + 0.004f * s, 0f), new Vector3(0.1f * s, 0.01f * s, 0.07f * s), Color.white); // and on the shoulders
            }
            StatueLeaves(mb, at, s, wear);
        }

        static void StatueLeaves(MeshBuilder mb, Vector3 at, float s, StatueWear wear)
        {
            if (wear != StatueWear.Leaves) return;
            float step = StatueStepH * s;
            mb.Box(at + new Vector3(0.04f * s, step + 0.002f * s, -0.045f * s), new Vector3(0.016f * s, 0.003f * s, 0.01f * s), new Color(0.78f, 0.45f, 0.16f), Quaternion.Euler(0f, 30f, 0f));
            mb.Box(at + new Vector3(-0.035f * s, step + 0.002f * s, -0.05f * s), new Vector3(0.014f * s, 0.003f * s, 0.009f * s), new Color(0.68f, 0.30f, 0.14f), Quaternion.Euler(0f, -40f, 0f));
        }

        /// <summary>The carved figure's arms and its tell (all in the one grey): the left arm hangs at the side; the right (with a tell) is bent forward
        /// at the elbow holding the job's tool, the crook, or the open hand palm up for a kind one; with no tell both hang at the sides.</summary>
        static void StatueArms(MeshBuilder mb, Vector3 feet, float f, StatueRules.Tell tell, Color stone, Color dark)
        {
            float bodyH = BodyH * f, sx = ShoulderX * f, armL = ArmLen * f, shY = bodyH * 0.88f;
            mb.Box(feet + new Vector3(-sx - 0.006f * f, shY - armL * 0.5f, 0f), new Vector3(0.016f * f, armL, 0.016f * f), stone, Quaternion.Euler(0f, 0f, -8f));
            if (tell == StatueRules.Tell.Nothing)
            {
                mb.Box(feet + new Vector3(sx + 0.006f * f, shY - armL * 0.5f, 0f), new Vector3(0.016f * f, armL, 0.016f * f), stone, Quaternion.Euler(0f, 0f, 8f));
                return;
            }
            // the right arm: down to the elbow, then forward (-z) to the hand in front of the hip
            var elbow = feet + new Vector3(sx + 0.008f * f, shY - armL * 0.55f, 0f);
            var hand = feet + new Vector3(sx + 0.004f * f, shY - armL * 0.62f, -armL * 0.55f);
            mb.Box(Vector3.Lerp(feet + new Vector3(sx, shY, 0f), elbow, 0.5f), new Vector3(0.016f * f, armL * 0.58f, 0.016f * f), stone, Quaternion.Euler(0f, 0f, 6f));
            mb.Box(Vector3.Lerp(elbow, hand, 0.5f), new Vector3(0.016f * f, 0.016f * f, armL * 0.56f), stone);
            if (tell == StatueRules.Tell.Hand)
            {
                mb.Box(hand + new Vector3(0f, 0.004f * f, -0.012f * f), new Vector3(0.026f * f, 0.006f * f, 0.026f * f), stone); // the palm, up
                return;
            }
            if (tell == StatueRules.Tell.Crook)
            {   // the antlers' post's crook: a staff standing at the hand, bent over at the top
                mb.Box(hand + new Vector3(0f, 0.06f * f, 0f), new Vector3(0.012f * f, 0.22f * f, 0.012f * f), dark);
                mb.Box(hand + new Vector3(0f, 0.168f * f, -0.016f * f), new Vector3(0.012f * f, 0.012f * f, 0.04f * f), dark);
                mb.Box(hand + new Vector3(0f, 0.15f * f, -0.034f * f), new Vector3(0.012f * f, 0.04f * f, 0.012f * f), dark);
                return;
            }
            var kind = tell switch
            {
                StatueRules.Tell.Axe => PropKind.Axe, StatueRules.Tell.Hammer => PropKind.Hammer,
                StatueRules.Tell.Rod => PropKind.Rod, StatueRules.Tell.Basket => PropKind.Basket,
                _ => PropKind.Hoe,
            };
            // the kit's tool, grip at the hand, carved in the same stone: each colour becomes a grey by how bright it was
            int from = mb.verts.Count;
            var keep = mb.M;
            mb.M = keep * Matrix4x4.TRS(hand, Quaternion.identity, Vector3.one * f * 0.8f);
            BuildProp(mb, kind);
            mb.M = keep;
            for (int i = from; i < mb.colors.Count; i++)
            {
                var c = mb.colors[i];
                float l = Mathf.Clamp01(c.r * 0.3f + c.g * 0.59f + c.b * 0.11f);
                mb.colors[i] = Color.Lerp(dark, StatueStoneLit, l);
            }
        }
    }
}
