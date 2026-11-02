using System;
using System.Collections.Generic;
using AquariumCiv.Sim.Settle;

namespace EcologyLab
{
    /// <summary>
    /// Top-down frames as seen through the glass: cover per cell (with hillshade), the water, the people's flood line
    /// (dotted), the camp, the Keeper's tree (ring), rabbits (dots), droplets and grime on the glass; under it a
    /// cross-section strip through the main hollow with the water table; and contact sheets.
    /// </summary>
    public static class Render
    {
        public const int S = 5;                          // pixels per cell
        public const int MapW = Land.NX * S, MapH = Land.NZ * S;
        public const int HeadH = 14, StripH = 64, InfoH = 14;
        public const int FrameW = MapW, FrameH = HeadH + MapH + StripH + InfoH;

        const uint Ink = 0x2A2A2A, Paper = 0xF4F1EA, Sky = 0xE9EFF2;
        const uint DirtDry = 0xC9AA80, DirtWet = 0x8C6C4C, Grass = 0xA9C95F, Tall = 0x7EAF46, Bush = 0x7C9440,
            Young = 0x2F6B3A, TreeC = 0x1C4A2C, Reeds = 0x98B66A, Water = 0x3E7BBB, WaterDeep = 0x2A5A92;

        public static Canvas Frame(Land land, Snapshot sn, int seedNo, Strategy st)
        {
            var cv = new Canvas(FrameW, FrameH, Paper);
            // Header: the year and what has happened.
            cv.Text(3, 2, "Y" + sn.year.ToString("0"), Ink);
            var tags = new List<string>();
            if (sn.camp) tags.Add("CAMP");
            if (sn.treeCell >= 0) tags.Add("TREE " + KeeperTree.StageNames[sn.treeStage]);
            if (sn.pond > 0) tags.Add("POND");
            if (sn.village) tags.Add("VILLAGE");
            string tg = string.Join(" ", tags);
            cv.Text(FrameW - Canvas.TextWidth(tg) - 3, 2, tg, sn.village ? 0x8A3B12 : Ink);
            DrawMap(cv, land, sn, 0, HeadH);
            DrawStrip(cv, land, sn, 0, HeadH + MapH);
            DrawInfo(cv, sn, 0, HeadH + MapH + StripH);
            return cv;
        }

        static uint CoverColour(Cover c, float m) => c switch
        {
            Cover.Bare => Canvas.Mix(DirtDry, DirtWet, Terrain.Smooth(0.05f, 0.6f, m)),
            Cover.Grass => Grass, Cover.Tall => Tall, Cover.Bush => Bush, Cover.Seedling => Grass,
            Cover.Young => Young, Cover.Tree => TreeC, Cover.Reeds => Reeds, _ => Water,
        };

        static void DrawMap(Canvas cv, Land land, Snapshot sn, int ox, int oy)
        {
            for (int j = 0; j < Land.NZ; j++)
                for (int i = 0; i < Land.NX; i++)
                {
                    int c = Land.Index(i, j);
                    var cov = sn.cover[c];
                    uint col = CoverColour(cov, sn.moist[c]);
                    if (cov == Cover.Water) col = Canvas.Mix(Water, WaterDeep, Terrain.Clamp01((sn.pondLevel - land.height[c]) / 0.15f));
                    else
                    {
                        int up = Land.Index(Math.Max(0, i - 1), Math.Min(Land.NZ - 1, j + 1));
                        float k = Terrain.Clamp(1f + (land.height[c] - land.height[up]) * 5f, 0.78f, 1.22f);
                        col = Canvas.Shade(col, k);
                    }
                    int px = ox + i * S, py = oy + (Land.NZ - 1 - j) * S;
                    cv.Rect(px, py, S, S, col);
                    if (cov == Cover.Bare && sn.moist[c] < 0.12f && Terrain.Hash(i * 7, j * 13) < 0.35f) cv.Set(px + 1 + (i + j) % 3, py + 2, Canvas.Shade(col, 0.8f)); // cracks
                    if (cov == Cover.Seedling) cv.Rect(px + 2, py + 1, 1, 3, Young);
                    if (cov == Cover.Young) cv.Rect(px + 1, py + 1, 3, 3, Canvas.Shade(Young, 0.8f));
                    if (cov == Cover.Tree) cv.Rect(px + 1, py + 1, 3, 3, Canvas.Shade(TreeC, 0.75f));
                    if (cov == Cover.Bush) cv.Set(px + 2, py + 2, Canvas.Shade(Bush, 0.75f));
                    if (cov == Cover.Reeds) { cv.Rect(px + 1, py, 1, S, 0x6F8F45); cv.Rect(px + 3, py, 1, S, 0x6F8F45); }
                    // The flood line: dots on the rim of every hollow (cells under its spill level that touch higher land).
                    if (land.InBasin(c) && cov != Cover.Water && land.basins[land.basin[c]].cells.Count > 8 && Rim(land, c) && (i + j) % 2 == 0)
                        cv.Rect(px + 1, py + 1, 2, 2, 0x5E86AE);
                }
            // Rabbits as dots on meadow cells.
            int rab = (int)MathF.Min(30, MathF.Round(sn.rabbits / 1.5f)), placed = 0;
            for (int k = 0; k < 2000 && placed < rab; k++)
            {
                int c = (int)(Terrain.Hash3(91, k, 7) * Land.N);
                if (sn.cover[c] != Cover.Grass && sn.cover[c] != Cover.Tall) continue;
                int px = ox + Land.I(c) * S + 1, py = oy + (Land.NZ - 1 - Land.J(c)) * S + 1;
                cv.Rect(px, py, 2, 2, 0xF5F1E8); cv.Set(px + 2, py + 1, 0x6E6455);
                placed++;
            }
            // The camp: a cold fire, then tents once the camp starts.
            int fx = ox + (int)((land.campX - Land.X0) / Land.Cell * S), fz = oy + (int)((Land.NZ - (land.campZ - Land.Z0) / Land.Cell) * S);
            if (sn.camp)
                for (int k = 0; k < 4; k++)
                {
                    float a = k * MathF.PI / 2f + 0.6f;
                    int tx = fx + (int)(MathF.Cos(a) * 12), ty = fz + (int)(MathF.Sin(a) * 10);
                    for (int r = 0; r < 5; r++) cv.Rect(tx - r, ty - 4 + r, 2 * r + 1, 1, r == 4 ? 0x7A6440u : 0xEADBB4u);
                }
            cv.Rect(fx - 2, fz - 2, 5, 5, 0x3A3530);
            cv.Rect(fx - 1, fz - 1, 3, 3, sn.camp ? 0xF07A22u : 0x77706A);
            // The Keeper's tree: a ring (yellow; orange while it wilts).
            if (sn.treeCell >= 0)
            {
                int tx = ox + Land.I(sn.treeCell) * S + S / 2, ty = oy + (Land.NZ - 1 - Land.J(sn.treeCell)) * S + S / 2;
                uint rc = sn.treeWilting ? 0xF09030u : 0xFFE24Au;
                for (int a = 0; a < 40; a++)
                {
                    float an = a / 40f * MathF.PI * 2f;
                    cv.Set(tx + (int)MathF.Round(MathF.Cos(an) * 7), ty + (int)MathF.Round(MathF.Sin(an) * 7), rc);
                    cv.Set(tx + (int)MathF.Round(MathF.Cos(an) * 8), ty + (int)MathF.Round(MathF.Sin(an) * 8), 0x3A3530);
                }
            }
            // The glass: grime as a haze, droplets as beads (denser toward the bottom edge, where they run).
            for (int y = 0; y < MapH; y++)
                for (int x = 0; x < MapW; x++)
                {
                    float hz = sn.grime * 0.22f;
                    if (hz > 0f) cv.Set(ox + x, oy + y, Canvas.Mix(cv.Get(ox + x, oy + y), 0x8C8A7C, hz));
                    float dens = sn.droplets * (0.02f + 0.06f * y / MapH);
                    if (dens > 0f && Terrain.Hash(x * 3 + 1, y * 5 + 2) < dens) { cv.Set(ox + x, oy + y, 0xE6F2FA); cv.Set(ox + x, oy + y + 1, 0x9FB8C8); }
                }
        }

        static bool Rim(Land land, int c)
        {
            bool rim = false;
            Land.ForNeighbours4(c, nb => { if (!land.InBasin(nb)) rim = true; });
            return rim;
        }

        /// <summary>The cross-section through the main hollow's lowest cell, along x: ground, soil, the water table as a
        /// darker wet band, the pond, and the flood line (dashed).</summary>
        static void DrawStrip(Canvas cv, Land land, Snapshot sn, int ox, int oy)
        {
            int bottomCell = land.main.cells[0];
            foreach (int c in land.main.cells) if (land.height[c] < land.height[bottomCell]) bottomCell = c;
            int row = Land.J(bottomCell);
            float rowMax = 0f;
            for (int i = 0; i < Land.NX; i++) rowMax = MathF.Max(rowMax, land.height[Land.Index(i, row)]);
            float vmin = land.minHeight - 0.14f, vmax = land.main.spill + 0.07f; // the hills are clipped: the strip is about the water
            int Y(float v) => oy + StripH - 1 - (int)((v - vmin) / (vmax - vmin) * (StripH - 1));
            cv.Rect(ox, oy, FrameW, StripH, Sky);
            for (int x = 0; x < FrameW; x++)
            {
                float fi = (x + 0.5f) / S - 0.5f;
                int i0 = Math.Clamp((int)MathF.Floor(fi), 0, Land.NX - 1), i1 = Math.Min(Land.NX - 1, i0 + 1);
                float tt = Terrain.Clamp01(fi - i0);
                int c0 = Land.Index(i0, row), c = Land.Index((int)Math.Clamp(MathF.Round(fi), 0, Land.NX - 1), row);
                float hg = land.height[c0] + (land.height[Land.Index(i1, row)] - land.height[c0]) * tt;
                int yg = Math.Max(oy + 1, Y(hg)), yt = Y(sn.table);
                for (int y = yg; y < oy + StripH; y++)
                {
                    float depth = (y - yg) / (float)StripH;
                    uint soil = Canvas.Mix(0x9C7852, 0x5E4430, depth * 1.6f);
                    if (y >= yt) soil = Canvas.Mix(soil, 0x2F6FA8, 0.62f);
                    cv.Set(ox + x, y, soil);
                }
                if (sn.cover[c] == Cover.Water)
                    for (int y = Y(sn.pondLevel); y < yg; y++) cv.Set(ox + x, y, Water);
                uint top = sn.cover[c] == Cover.Water ? WaterDeep : CoverColour(sn.cover[c], sn.moist[c]);
                cv.Rect(ox + x, yg - 1, 1, 2, top);
                if (sn.cover[c] == Cover.Tree || sn.cover[c] == Cover.Young) cv.Rect(ox + x, yg - (sn.cover[c] == Cover.Tree ? 7 : 4), 1, sn.cover[c] == Cover.Tree ? 6 : 3, Canvas.Shade(top, 1.15f));
                if (yt < yg && x % 3 == 0) cv.Set(ox + x, yt, 0x1F4E7A); // the table line inside the soil
                if (land.InBasin(c) && land.basin[c] == land.main.id && x % 6 < 3) cv.Set(ox + x, Y(land.main.spill), 0x5E86AE);
            }
            cv.Text(ox + 3, oy + StripH - 8, "WATER TABLE", 0xF4F1EA, 1);
            cv.Frame(ox, oy, FrameW, StripH, 0xB8B2A6);
        }

        static void DrawInfo(Canvas cv, Snapshot sn, int ox, int oy)
        {
            // Humidity: a bar coloured dry / about right / too humid.
            uint hc = sn.humidity < 0.2f ? 0xC9A06Au : sn.humidity > 0.75f ? 0x5B8FD0u : 0x7DB052u;
            cv.Text(ox + 3, oy + 2, "H", Ink);
            cv.Rect(ox + 13, oy + 3, 50, 8, 0xD8D3C8);
            cv.Rect(ox + 13, oy + 3, (int)(50 * sn.humidity), 8, hc);
            string s = $"RABBITS {sn.rabbits:0}  TREES {sn.trees}  POND {sn.pond}";
            cv.Text(FrameW - Canvas.TextWidth(s) - 3, oy + 2, s, Ink);
        }

        // ------------------------------------------------------------------ sheets
        const int Gap = 6, TitleH = 40;

        /// <summary>One run: every frame, 7 per row, with a title and the key years.</summary>
        public static Canvas ContactSheet(Run run, int seedNo)
        {
            int cols = 7, rows = (run.frames.Count + cols - 1) / cols;
            var sheet = new Canvas(Gap + cols * (FrameW + Gap), TitleH + rows * (FrameH + Gap) + Gap, 0xFFFFFF);
            var e = run.eco;
            sheet.Text(Gap, 6, $"SEED {seedNo} ({run.land.spec.name})  ({Player.Letter(run.strategy)}) {Player.Name(run.strategy)}", Ink, 3);
            sheet.Text(Gap, 26, KeyYears(e), 0x5A4630, 2);
            for (int k = 0; k < run.frames.Count; k++)
            {
                var f = Frame(run.land, run.frames[k], seedNo, run.strategy);
                sheet.Blit(f, Gap + k % cols * (FrameW + Gap), TitleH + k / cols * (FrameH + Gap));
            }
            return sheet;
        }

        public static string KeyYears(Eco e) =>
            $"CAMP Y{Run.Y(e.campYear)}  BUSHES Y{Run.Y(e.bushYear)}  PLANTED Y{Run.Y(e.plantedYear)}  TREE GROWN Y{Run.Y(e.keeperGrownYear)}  FIRST TREE Y{Run.Y(e.firstTreeYear)}  WATER Y{Run.Y(e.surfacedYear)}  VILLAGE Y{Run.Y(e.villageYear)}";

        /// <summary>One seed, the four players side by side: a row each, a few years.</summary>
        public static Canvas Compare(IList<Run> runs, int seedNo, int[] years)
        {
            int labelW = 120;
            var sheet = new Canvas(labelW + years.Length * (FrameW + Gap) + Gap, TitleH + runs.Count * (FrameH + Gap) + Gap, 0xFFFFFF);
            sheet.Text(Gap, 6, $"SEED {seedNo} ({runs[0].land.spec.name}): THE FOUR KEEPERS", Ink, 3);
            for (int r = 0; r < runs.Count; r++)
            {
                int y = TitleH + r * (FrameH + Gap);
                sheet.Text(Gap, y + 20, "(" + Player.Letter(runs[r].strategy) + ")", Ink, 4);
                var words = Player.Name(runs[r].strategy).Split(' ');
                for (int w = 0; w < words.Length; w++) sheet.Text(Gap, y + 50 + w * 14, words[w], Ink, 2);
                for (int k = 0; k < years.Length; k++)
                {
                    var sn = runs[r].frames.Find(f => MathF.Abs(f.year - years[k]) < 0.01f);
                    if (sn == null) continue;
                    sheet.Blit(Frame(runs[r].land, sn, seedNo, runs[r].strategy), labelW + k * (FrameW + Gap), y);
                }
            }
            return sheet;
        }
    }
}
