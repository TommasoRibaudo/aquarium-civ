using UnityEngine;

namespace AquariumCiv.View
{
    /// <summary>
    /// The two props the choice scene adds (#224), code-built in the box's palette like <see cref="StashProps"/>: a marker stake (a peeled
    /// post with a cloth tied on near the top, for <i>which way</i>) and an offering (a small mat with something on it, for
    /// <i>tradition</i>). Box units, standing on their origin. No new art beyond these two (the issue's "Out").
    /// </summary>
    public static class ChoiceProps
    {
        /// <summary>The stake's height and the offering mat's half size (box units): a stake stands about as tall as a person.</summary>
        public const float StakeHeight = 0.24f, MatHalfX = 0.05f, MatHalfZ = 0.038f;

        /// <summary>What sits on an offering mat.</summary>
        public enum Offering { Basket, Stone, Sprig, Feast }

        /// <summary>The offering for a dilemma's option: the stone or a charm on a sprig (where to remember), a feast bowl or the best basket
        /// (how to mark the harvest), else a basket.</summary>
        public static Offering OfferingFor(string defId, int option) => defId switch
        {
            "camp_remember" => option == 0 ? Offering.Stone : Offering.Sprig,
            "village_harvest" => option == 0 ? Offering.Feast : Offering.Basket,
            "small_supper" => option == 0 ? Offering.Feast : Offering.Basket, // t4-fixes: one big pot, or a basket each
            _ => Offering.Basket,
        };

        /// <summary>A cloth colour per option, so the two stakes differ by more than place (colour is never the only signal: each has its tag).</summary>
        public static Color ClothFor(int option) => option == 0 ? Palette.Accents[0] : Palette.Accents[2];

        public static Mesh Stake(Color cloth)
        {
            var mb = new MeshBuilder(224) { jitter = 0.01f };
            const float r = 0.011f;
            mb.Prism(Vector3.zero, r * 1.15f, r, StakeHeight, 6, Palette.WoodLight, topCol: Palette.Straw);
            mb.Prism(new Vector3(0f, StakeHeight, 0f), r, 0.002f, 0.025f, 6, Palette.Straw); // the point
            // The cloth: tied on near the top, hanging out to one side with a little droop.
            float y = StakeHeight - 0.035f;
            mb.Box(new Vector3(0f, y, 0f), new Vector3(r * 2.6f, 0.012f, r * 2.6f), Palette.Timber); // the tie
            mb.Box(new Vector3(0.04f, y - 0.018f, 0f), new Vector3(0.07f, 0.04f, 0.006f), cloth, Quaternion.Euler(0f, 0f, -12f));
            // A ring of stones round the foot, so it reads as set there on purpose.
            for (int i = 0; i < 5; i++)
            {
                float a = i * Mathf.PI * 2f / 5f + 0.4f;
                mb.Ico(new Vector3(Mathf.Cos(a) * 0.026f, 0.006f, Mathf.Sin(a) * 0.026f), 0.011f, Vector3.one, Palette.Rock, 0.2f, Palette.RockDark);
            }
            return Done(mb, "ChoiceStake");
        }

        public static Mesh OfferingMesh(Offering kind)
        {
            var mb = new MeshBuilder(2240 + (int)kind) { jitter = 0.01f };
            const float matH = 0.006f;
            mb.Box(new Vector3(0f, matH * 0.5f, 0f), new Vector3(MatHalfX * 2f, matH, MatHalfZ * 2f), Palette.Canvas, bottom: false);
            mb.Box(new Vector3(0f, matH + 0.0005f, 0f), new Vector3(MatHalfX * 1.6f, 0.001f, 0.006f), Palette.Ember); // a stripe woven in
            float y = matH;
            switch (kind)
            {
                case Offering.Stone:
                    mb.Ico(new Vector3(0f, y + 0.014f, 0f), 0.022f, new Vector3(1.2f, 0.7f, 1f), Palette.Stone, 0.12f, Palette.RockDark);
                    mb.Ico(new Vector3(0.004f, y + 0.035f, 0f), 0.015f, new Vector3(1.1f, 0.75f, 1f), Palette.RockLight, 0.12f);
                    mb.Ico(new Vector3(-0.002f, y + 0.051f, 0.001f), 0.009f, Vector3.one, Palette.Rock, 0.15f);
                    break;
                case Offering.Sprig:
                    mb.Prism(new Vector3(0f, y, 0f), 0.004f, 0.003f, 0.07f, 5, Palette.Bark);
                    mb.Ico(new Vector3(0.012f, y + 0.055f, 0f), 0.016f, new Vector3(1f, 0.8f, 1f), Palette.LeafC, 0.15f);
                    mb.Ico(new Vector3(-0.012f, y + 0.045f, 0.004f), 0.014f, new Vector3(1f, 0.8f, 1f), Palette.LeafA, 0.15f);
                    mb.Ico(new Vector3(0.002f, y + 0.074f, 0f), 0.012f, Vector3.one, Palette.LeafB, 0.15f);
                    mb.Ico(new Vector3(0.006f, y + 0.03f, -0.008f), 0.007f, Vector3.one, Palette.Accents[1], 0.05f); // the charm
                    break;
                case Offering.Feast:
                    mb.Prism(new Vector3(0f, y, 0f), 0.018f, 0.028f, 0.018f, 9, Palette.Terracotta, topCol: Palette.Ember);
                    mb.Ico(new Vector3(-0.009f, y + 0.024f, 0f), 0.013f, new Vector3(1.3f, 0.75f, 1f), Palette.Wheat, 0.1f);
                    mb.Ico(new Vector3(0.011f, y + 0.026f, 0.004f), 0.012f, new Vector3(1.2f, 0.8f, 1f), Palette.Thatch, 0.1f);
                    mb.Ico(new Vector3(0.002f, y + 0.03f, -0.01f), 0.008f, Vector3.one, Palette.Flowers[2], 0.1f);
                    break;
                default:
                    mb.Prism(new Vector3(0f, y, 0f), 0.017f, 0.025f, 0.03f, 8, Palette.Thatch, topCol: Palette.Straw);
                    mb.Ico(new Vector3(-0.008f, y + 0.033f, 0f), 0.009f, Vector3.one, Palette.Flowers[1], 0.1f);
                    mb.Ico(new Vector3(0.008f, y + 0.034f, 0.004f), 0.009f, Vector3.one, Palette.Flowers[3], 0.1f);
                    mb.Ico(new Vector3(0f, y + 0.036f, -0.008f), 0.009f, Vector3.one, Palette.Flowers[0], 0.1f);
                    break;
            }
            return Done(mb, "ChoiceOffering" + kind);
        }

        /// <summary>How tall a prop stands (its top, for the tag and the tap).</summary>
        public static float TopOf(Life.ChoiceProp prop) => prop == Life.ChoiceProp.Stake ? StakeHeight + 0.02f : 0.09f;

        static Mesh Done(MeshBuilder mb, string name)
        {
            var mesh = mb.ToMesh(name);
            mesh.hideFlags = HideFlags.DontSave;
            return mesh;
        }
    }
}
