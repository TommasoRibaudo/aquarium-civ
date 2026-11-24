using UnityEngine;

namespace AquariumCiv.View
{
    /// <summary>#692 (B29.d): the spit over the fire, code-built: two forked sticks 0.12 u, a bar 0.22 u, and the meat on it (a rabbit: a
    /// brown oval 0.05 u with four leg stubs; a deer: a haunch 0.07 u), browning from pink-brown to rich brown. The bar runs along local x.</summary>
    public static class SpitMeshes
    {
        static readonly Color Raw = new Color(0.72f, 0.42f, 0.36f), Roast = new Color(0.40f, 0.20f, 0.09f);
        public const int BrownSteps = 5;

        public static Mesh Stick()
        {
            var mb = new MeshBuilder(6930) { jitter = 0.02f };
            mb.Box(new Vector3(0f, 0.05f, 0f), new Vector3(0.01f, 0.1f, 0.01f), Palette.Bark);
            mb.Box(new Vector3(-0.009f, 0.108f, 0f), new Vector3(0.007f, 0.03f, 0.007f), Palette.Bark, Quaternion.Euler(0f, 0f, 28f));
            mb.Box(new Vector3(0.009f, 0.108f, 0f), new Vector3(0.007f, 0.03f, 0.007f), Palette.Bark, Quaternion.Euler(0f, 0f, -28f));
            return Done(mb, "Camp_SpitStick");
        }

        public static Mesh Bar()
        {
            var mb = new MeshBuilder(6931);
            mb.Box(Vector3.zero, new Vector3(0.008f, 0.008f, 0.22f), Palette.WoodLight);
            return Done(mb, "Camp_SpitBar");
        }

        /// <summary>The meat in <see cref="BrownSteps"/> shades, raw to roast (the bar's axis is local z, as the bar's box).</summary>
        public static Mesh[] Meat(bool deer)
        {
            var ms = new Mesh[BrownSteps];
            for (int i = 0; i < BrownSteps; i++)
            {
                var c = Color.Lerp(Raw, Roast, i / (float)(BrownSteps - 1));
                var dark = Color.Lerp(c, Color.black, 0.25f);
                var mb = new MeshBuilder(6932 + i);
                if (deer)
                {
                    mb.Ico(Vector3.zero, 0.035f, new Vector3(1f, 0.85f, 1.25f), c, 0.15f);
                    mb.Box(new Vector3(0f, 0f, 0.045f), new Vector3(0.012f, 0.012f, 0.03f), new Color(0.88f, 0.84f, 0.74f)); // the bone end
                }
                else
                {
                    mb.Ico(Vector3.zero, 0.025f, new Vector3(0.9f, 0.8f, 1.35f), c, 0.12f);
                    for (int k = 0; k < 4; k++)
                    {
                        float z = k < 2 ? 0.024f : -0.024f, x = k % 2 == 0 ? 0.012f : -0.012f;
                        mb.Box(new Vector3(x, -0.014f, z), new Vector3(0.007f, 0.016f, 0.007f), dark, Quaternion.Euler(z > 0 ? -30f : 30f, 0f, 0f));
                    }
                }
                ms[i] = Done(mb, "Camp_SpitMeat" + i);
            }
            return ms;
        }

        static Mesh Done(MeshBuilder mb, string name) { var m = mb.ToMesh(name); m.hideFlags = HideFlags.DontSave; return m; }
    }
}
