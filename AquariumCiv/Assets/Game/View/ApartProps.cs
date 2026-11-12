using AquariumCiv.Life;
using UnityEngine;

namespace AquariumCiv.View
{
    /// <summary>
    /// The little camps of the people who live apart (#231), code-built in the box's palette like <see cref="ChoiceProps"/> and
    /// <see cref="Pieces"/> (no new art): a camp fire (lit, or gone cold when nobody is left), a lean-to, a woodpile with its block, a hut
    /// with a lit window, a fish rack and a small boat. Box units, standing on their origin; a shelter's open side or door faces local -z.
    /// Camp things are a little smaller than the settlement's, so the camp reads as a few people living out there, not a second village.
    /// </summary>
    public static class ApartProps
    {
        const float CampScale = 0.75f;

        /// <summary>A camp fire: the settlement's fire pit, smaller; cold, a ring of stones round charred logs and ash.</summary>
        public static Mesh Fire(bool lit, int seed)
        {
            var mb = new MeshBuilder(2310 + seed) { jitter = 0.02f, M = Matrix4x4.Scale(Vector3.one * CampScale) };
            if (lit)
            {
                Pieces.Firepit(mb, Vector3.zero, false, Pieces.Phase(seed));
                return Done(mb, "ApartFire");
            }
            for (int i = 0; i < 9; i++)
            {
                float a = i / 9f * Mathf.PI * 2f;
                mb.Ico(new Vector3(Mathf.Cos(a), 0.01f, Mathf.Sin(a)) * 0.14f, 0.042f, new Vector3(1, 0.7f, 1), i % 2 == 0 ? Palette.Rock : Palette.RockDark, 0.25f);
            }
            mb.Prism(Vector3.zero, 0.09f, 0.08f, 0.01f, 7, Palette.RockDark); // the ash
            for (int i = 0; i < 3; i++) Pieces.Log(mb, Vector3.up * 0.022f, 0.016f, 0.16f, i * 60f + 20f, Palette.SoilDark);
            return Done(mb, "ApartFireCold");
        }

        /// <summary>The woods camp's shelter: the camp's hide lean-to with a bedroll under it.</summary>
        public static Mesh LeanTo()
        {
            var mb = new MeshBuilder(2311) { jitter = 0.02f, M = Matrix4x4.Scale(Vector3.one * CampScale) };
            Pieces.LeanTo(mb);
            mb.Prism(new Vector3(0.1f, 0.03f, 0.1f), 0.03f, 0.03f, 0.16f, 6, Palette.Accents[3], Quaternion.Euler(0f, 0f, 90f)); // a rolled blanket
            return Done(mb, "ApartLeanTo");
        }

        /// <summary>A small woodpile and the block they split on.</summary>
        public static Mesh Woodpile()
        {
            var mb = new MeshBuilder(2312) { jitter = 0.02f, M = Matrix4x4.Scale(Vector3.one * CampScale) };
            Pieces.Woodpile(mb, Vector3.zero, 0f);
            mb.Prism(new Vector3(0f, 0f, -0.16f), 0.05f, 0.048f, 0.07f, 7, Palette.Bark, topCol: Palette.WoodLight); // the block
            return Done(mb, "ApartWoodpile");
        }

        /// <summary>#578: a loose stack of <paramref name="count"/> logs (a log a load the travellers set down, up to <see cref="ApartLife.MaxPileLogs"/>), lying along local z,
        /// centred <paramref name="xOffset"/> to the side of the origin. The chopping spot keeps two.</summary>
        public static Mesh Logs(int count, int seed = 0, float xOffset = 0f)
        {
            var mb = new MeshBuilder(2320 + seed) { jitter = 0.02f, M = Matrix4x4.Scale(Vector3.one * CampScale) };
            int n = ApartLife.PileLogs(count), placed = 0;
            int[] row = { 5, 4, 3 };
            for (int r = 0; r < row.Length && placed < n; r++)
                for (int i = 0; i < row[r] && placed < n; i++, placed++)
                    Pieces.Log(mb, new Vector3(xOffset + (i - (row[r] - 1) / 2f) * 0.055f, 0.028f + r * 0.048f, 0f), 0.026f, 0.26f, 0f, placed % 2 == 0 ? Palette.Bark : Palette.WoodLight);
            return Done(mb, "ApartLogs");
        }

        /// <summary>The shore family's hut: plank walls under a peaked thatch, the door on local -z; a window that lights at night while
        /// someone lives there.</summary>
        public static Mesh Hut(bool lived, int seed)
        {
            var mb = new MeshBuilder(2313 + seed) { jitter = 0.03f };
            const float w = 0.3f, d = 0.24f, wallH = 0.17f;
            mb.Box(new Vector3(0f, wallH * 0.5f, 0f), new Vector3(w, wallH, d), Palette.WoodLight);
            mb.Box(new Vector3(0f, wallH, 0f), new Vector3(w + 0.02f, 0.015f, d + 0.02f), Palette.Timber); // the eaves' beam
            mb.Prism(new Vector3(0f, wallH + 0.005f, 0f), 0.25f, 0.015f, 0.17f, 4, Palette.Thatch, Quaternion.Euler(0f, 45f, 0f), topCol: Palette.Straw);
            mb.Box(new Vector3(-0.05f, 0.06f, -d * 0.5f - 0.003f), new Vector3(0.065f, 0.12f, 0.006f), Palette.Timber); // the door
            if (lived) mb.lamp = Pieces.Phase(seed);
            mb.Box(new Vector3(0.075f, 0.1f, -d * 0.5f - 0.003f), new Vector3(0.05f, 0.04f, 0.006f), lived ? Palette.WindowLit : Palette.SoilDark); // the window
            mb.lamp = -1f;
            return Done(mb, "ApartHut");
        }

        /// <summary>A fish rack: two posts and a bar with the day's catch hanging.</summary>
        public static Mesh FishRack(bool stocked)
        {
            var mb = new MeshBuilder(2314) { jitter = 0.02f, M = Matrix4x4.Scale(Vector3.one * CampScale) };
            foreach (int s in new[] { -1, 1 }) mb.Box(new Vector3(s * 0.12f, 0.12f, 0f), new Vector3(0.02f, 0.24f, 0.02f), Palette.Bark);
            mb.Box(new Vector3(0f, 0.235f, 0f), new Vector3(0.28f, 0.016f, 0.016f), Palette.Bark);
            if (stocked)
            {
                mb.sway = 0.2f;
                for (int i = 0; i < 4; i++)
                    mb.Ico(new Vector3(-0.08f + i * 0.055f, 0.18f, 0f), 0.02f, new Vector3(0.45f, 1.9f, 0.8f), i % 2 == 0 ? Palette.Water : Palette.WaterDeep, 0.05f);
                mb.sway = 0f;
            }
            return Done(mb, "ApartFishRack");
        }

        /// <summary>A small boat drawn up on the shore, its length along local x, a paddle across it.</summary>
        public static Mesh Boat()
        {
            var mb = new MeshBuilder(2315) { jitter = 0.02f };
            const float len = 0.26f, beam = 0.085f, h = 0.035f;
            mb.Box(new Vector3(0f, h * 0.5f, 0f), new Vector3(len * 0.7f, h, beam), Palette.Timber);
            foreach (int s in new[] { -1, 1 })
                mb.Box(new Vector3(s * len * 0.38f, h * 0.55f, 0f), new Vector3(beam * 0.75f, h * 0.9f, beam * 0.75f), Palette.Timber, Quaternion.Euler(0f, 45f, 0f));
            mb.Box(new Vector3(0f, h + 0.002f, 0f), new Vector3(len * 0.64f, 0.004f, beam * 0.72f), Palette.Bark); // the inside
            mb.Box(new Vector3(0f, h + 0.008f, 0f), new Vector3(0.02f, 0.008f, beam), Palette.WoodLight);          // the seat
            mb.Box(new Vector3(0.02f, h + 0.014f, 0f), new Vector3(0.008f, 0.006f, 0.17f), Palette.WoodLight, Quaternion.Euler(0f, 30f, 0f)); // the paddle
            return Done(mb, "ApartBoat");
        }

        // ---------------------------------------------------------------- #404: the lookout tower

        /// <summary>The tower's platform height, half its width, and where the ladder stands (local -z, its front), box units.</summary>
        public const float TowerPlatformY = 0.36f, TowerHalf = 0.09f, TowerLadderZ = -0.125f, TowerLampY = 0.47f, TowerLampX = 0.07f, TowerLampZ = -0.2f;

        /// <summary>The people apart's lookout tower (#404), by stage: 1 four posts braced and a pile of planks; 2 the platform with its rail
        /// and the ladder on local -z; 3 the peaked roof over it and the lamp's hook. The lamp itself is <see cref="TowerLamp"/>.</summary>
        public static Mesh Tower(int stage, int seed)
        {
            var mb = new MeshBuilder(2330 + seed) { jitter = 0.015f };
            const float h = TowerHalf, y = TowerPlatformY;
            float postTop = stage >= 3 ? y + 0.17f : stage >= 2 ? y + 0.07f : y;
            foreach (int sx in new[] { -1, 1 })
                foreach (int sz in new[] { -1, 1 })
                    mb.Box(new Vector3(sx * h, postTop * 0.5f, sz * h), new Vector3(0.022f, postTop, 0.022f), Palette.Bark);
            // the cross braces on the two sides
            foreach (int sx in new[] { -1, 1 })
                mb.Box(new Vector3(sx * h, y * 0.45f, 0f), new Vector3(0.01f, 0.012f, h * 2.6f), Palette.Timber, Quaternion.Euler(52f, 0f, 0f));
            if (stage <= 1)
            {
                for (int i = 0; i < 4; i++) // the planks still waiting on the ground
                    mb.Box(new Vector3(h + 0.1f, 0.008f + i * 0.012f, -h - 0.12f + 0.02f * i), new Vector3(0.05f, 0.01f, 0.2f), i % 2 == 0 ? Palette.WoodLight : Palette.Timber, Quaternion.Euler(0f, 70f + 8f * i, 0f)); // on the camp's side
                return Done(mb, "ApartTower1");
            }
            mb.Box(new Vector3(0f, y, 0f), new Vector3(h * 2f + 0.05f, 0.018f, h * 2f + 0.05f), Palette.WoodLight); // the platform
            foreach (int s in new[] { -1, 1 }) // the rail on three sides (the ladder comes up at the front)
            {
                mb.Box(new Vector3(s * (h + 0.022f), y + 0.05f, 0f), new Vector3(0.01f, 0.01f, h * 2f + 0.05f), Palette.Timber);
                mb.Box(new Vector3(0f, y + 0.05f, h + 0.022f), new Vector3(h * 2f + 0.05f, 0.01f, 0.01f), Palette.Timber);
            }
            foreach (int s in new[] { -1, 1 }) // the ladder's rails, leaning a little
                mb.Box(new Vector3(s * 0.035f, y * 0.5f, LadderZ(y * 0.5f)), new Vector3(0.01f, y + 0.03f, 0.01f), Palette.Timber, Quaternion.Euler(6f, 0f, 0f));
            for (int r = 1; r < 7; r++)
                mb.Box(new Vector3(0f, r * y / 7f, LadderZ(r * y / 7f)), new Vector3(0.07f, 0.008f, 0.008f), Palette.WoodLight);
            if (stage <= 2) return Done(mb, "ApartTower2");
            mb.Prism(new Vector3(0f, y + 0.17f, 0f), 0.2f, 0.015f, 0.13f, 4, Palette.Thatch, Quaternion.Euler(0f, 45f, 0f), topCol: Palette.Straw); // the roof
            // the lamp's arm: out from the front post, past the eaves, so the lamp is seen from above
            mb.Box(new Vector3(TowerLampX, TowerLampY + 0.05f, (-h + TowerLampZ) * 0.5f), new Vector3(0.012f, 0.012f, -TowerLampZ - h + 0.02f), Palette.Bark);
            mb.Box(new Vector3(TowerLampX, TowerLampY + 0.035f, TowerLampZ), new Vector3(0.005f, 0.03f, 0.005f), Palette.Bark);
            return Done(mb, "ApartTower3");
        }

        /// <summary>The ladder's line (local z at a height): its foot out at <see cref="TowerLadderZ"/>, leaning in 6° to the platform's front.
        /// A climber's feet follow it.</summary>
        public static float LadderZ(float height) => TowerLadderZ - 0.01f + height * 0.105f;

        /// <summary>The tower's lamp, hung under the roof: dark, or lit (it glows with the night like a lit window).</summary>
        public static Mesh TowerLamp(bool lit, int seed)
        {
            var mb = new MeshBuilder(2334 + seed) { jitter = 0f };
            if (lit) mb.lamp = Pieces.Phase(seed);
            mb.Box(new Vector3(TowerLampX, TowerLampY, TowerLampZ), new Vector3(0.045f, 0.055f, 0.045f), lit ? Palette.WindowLit : Palette.SoilDark);
            mb.lamp = -1f;
            mb.Box(new Vector3(TowerLampX, TowerLampY + 0.031f, TowerLampZ), new Vector3(0.055f, 0.008f, 0.055f), Palette.Bark); // its cap
            return Done(mb, lit ? "ApartTowerLampLit" : "ApartTowerLamp");
        }

        static Mesh Done(MeshBuilder mb, string name)
        {
            var mesh = mb.ToMesh(name);
            mesh.hideFlags = HideFlags.DontSave;
            return mesh;
        }
    }
}
