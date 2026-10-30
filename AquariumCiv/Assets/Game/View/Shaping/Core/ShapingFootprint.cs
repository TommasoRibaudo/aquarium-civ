using System;
using System.Collections.Generic;
using AquariumCiv.Sim;
using AquariumCiv.Sim.Settle;

namespace AquariumCiv.View.Shaping
{
    /// <summary>Tuning for the shaping looks (#85). All [est], to tune in play; data here, not literals in the view.</summary>
    public static class ShapingTuning
    {
        // rain
        public const float CloudFormSeconds = 1.2f, RainDelaySeconds = 0.8f, CloudFadeSeconds = 1.5f;
        /// <summary>Seconds it rains, by the tool's level: a shower, steady rain, a storm.</summary>
        public static float RainSeconds(Era level) => level == Era.Camp ? 4f : level == Era.Village ? 6f : 8f;
        /// <summary>The soil stays dark this long (game months), then dries to <see cref="DampAfter"/> until the people answer.</summary>
        public const float WetMonths = 1f, DryMonths = 2f, DampAfter = 0.35f, AnsweredDryMonths = 2f;
        /// <summary>Sprouts show from this game month and are grown by <see cref="SproutGrownMonths"/> ("by the next months").</summary>
        public const float SproutFromMonths = 0.5f, SproutGrownMonths = 3f;
        public const float RainRadius = 0.8f, RainCellRadius = 0.5f;

        // grove
        public const float GroveRadius = 0.38f, SaplingScale = 0.25f, SaplingPopSeconds = 0.6f, SaplingStagger = 0.05f;
        /// <summary>A tree grows from a sapling to full size over this many game months, starting up to <see cref="TreeDelayMonths"/> late.</summary>
        public const float TreeGrowMonths = 36f, TreeDelayMonths = 6f;
        /// <summary>Woods: one tree per this share of the grove's cells; orchard: every other cell in a checker (rows).</summary>
        public const float WoodsTreeShare = 0.6f, TreeJitter = 0.035f;

        // channel
        public const float CarveSecondsPerCell = 0.08f, CarveMinSeconds = 0.8f, CarveMaxSeconds = 2.5f, WaterLagSeconds = 0.35f;
        /// <summary>The town's mill race falls this far along (0 = the pond, 1 = the spot).</summary>
        public const float MillFallAt = 0.75f;

        // herd
        public static int HerdCount(HerdSpecies s) => s switch { HerdSpecies.Deer => 5, HerdSpecies.Goat => 6, HerdSpecies.Sheep => 7, _ => 8 };
        public const float RunInSeconds = 2.4f, RunInStagger = 0.15f, SplashSeconds = 0.6f;
        /// <summary>The herd's grazing ground: spots within this of the gift; the herd moves between them every <see cref="GrazeCycle"/> s.</summary>
        public const float GrazeRadius = 0.45f, ShoalRadius = 0.5f, GrazeCycle = 14f, GrazeShare = 0.65f, SwimCycle = 8f;
        public const int GrazeSpots = 5;
        /// <summary>How far an animal stands from the herd's focus (deer) or swims round the shoal's centre (fish).</summary>
        public const float HerdSpread = 0.1f, ShoalSpread = 0.06f;
        /// <summary>An answered herd drifts to its pasture or jetty over this many real seconds.</summary>
        public const float HerdMoveToLotSeconds = 6f;
        /// <summary>Animals never walk faster than this (box units per real second): a run-in is brisk, never a teleport.</summary>
        public const float AnimalMaxSpeed = 2.2f;

        // the take-back
        public const float LeaveSeconds = 1.2f, HerdLeaveSeconds = 1.6f;

        // the answer
        public const float HighlightIn = 0.4f, HighlightHold = 6f, HighlightOut = 1f;
        public const float BubbleDelay = 1f, BubbleMin = 4f, BubbleMax = 9f, BubbleCharsPerSecond = 15f, FadeSeconds = 0.5f;
        public const float LabelSeconds = 12f;
    }

    /// <summary>
    /// What a shaping covers in the land, snapshotted when the view first sees it (a take-back clears the sim's record, but
    /// the leaving look still needs it): the rain's wet cells, the grove's tree cells, the channel's cells from the pond to
    /// the spot, the herd's grazing spots and where it runs in from. Pure: read from the nudge and the settle map (or a disc
    /// round the spot when the sim doesn't settle), no dice.
    /// </summary>
    public sealed class ShapingFootprint
    {
        public int nudgeId;
        public string kind, variant;
        public Era level;
        public float x, z;
        /// <summary>Cells: rain = wet land, grove = new woods, channel = the channel from the pond to the spot (in order).</summary>
        public readonly List<int> cells = new List<int>();
        /// <summary>Herd: grazing spots (deer) or swimming spots in the water (fish).</summary>
        public readonly List<(float x, float z)> spots = new List<(float, float)>();
        /// <summary>Herd: where the animals come in from (the nearest box edge; the splash for a shoal).</summary>
        public float entryX, entryZ;
        public int seed;

        /// <summary>The box's walkable inside (the glass margin kept).</summary>
        public static float MaxX => Terrain.HX - Terrain.GlassMargin;
        public static float MaxZ => Terrain.HZ - Terrain.GlassMargin;

        public static float ClampX(float x) => Math.Max(-MaxX, Math.Min(MaxX, x));
        public static float ClampZ(float z) => Math.Max(-MaxZ, Math.Min(MaxZ, z));

        public HerdSpecies Species => SpeciesOf(variant, level);

        /// <summary>The animal now (user 2026-10-30: herds level up with the era): the era's animal, never a step back from the
        /// one sent; a herd sent in the camp is goats in the village and a flock of sheep in the town.</summary>
        public HerdSpecies SpeciesAt(Era now) => SpeciesOf(variant, now > level ? now : level);

        /// <summary>The animal sent: fish in water; on grass deer in the camp, goats in the village, sheep in the town (the
        /// herd's level when it was sent, research 10).</summary>
        public static HerdSpecies SpeciesOf(string variant, Era level) =>
            variant == "fish" ? HerdSpecies.Fish : level == Era.Camp ? HerdSpecies.Deer : level == Era.Village ? HerdSpecies.Goat : HerdSpecies.Sheep;

        /// <summary>The footprint of a shaping now. <paramref name="map"/> is the settle map (null = no settling: discs).</summary>
        public static ShapingFootprint Of(Nudge n, SiteMap map, int boxSeed = 0)
        {
            var f = new ShapingFootprint
            {
                nudgeId = n.id, kind = n.kind, variant = n.variant ?? "", level = (Era)n.eraAtPlace, x = n.x, z = n.z,
                seed = unchecked(boxSeed * 31 + n.id),
            };
            switch (n.kind)
            {
                case SimCore.NudgeKind.Rain: f.RainCells(map); break;
                case SimCore.NudgeKind.Grove: f.GroveCells(n, map); break;
                case SimCore.NudgeKind.Channel: f.ChannelCells(n, map); break;
                case SimCore.NudgeKind.Herd: f.HerdSpots(map); break;
            }
            return f;
        }

        void RainCells(SiteMap map)
        {
            foreach (int c in Disc(x, z, ShapingTuning.RainCellRadius))
                if (map == null || !map.Water(c)) cells.Add(c);
        }

        void GroveCells(Nudge n, SiteMap map)
        {
            if (n.shapeCells != null && map != null)
            {
                for (int k = 0; k < n.shapeCells.Length; k++)
                {
                    int c = n.shapeCells[k];
                    if ((n.shapeBefore[k] & SiteMap.FForest) == 0 && map.Forest(c)) cells.Add(c);
                }
                if (cells.Count > 0) return;
            }
            foreach (int c in Disc(x, z, ShapingTuning.GroveRadius))
                if (map == null || (!map.Water(c) && map.lot[c] == 0)) cells.Add(c);
        }

        /// <summary>The channel's cells from the pond to the spot: the cells it turned to canal, walked breadth-first from the
        /// one by the pond (the farthest from the spot without a map).</summary>
        void ChannelCells(Nudge n, SiteMap map)
        {
            var set = new HashSet<int>();
            if (n.shapeCells != null)
                for (int k = 0; k < n.shapeCells.Length; k++)
                {
                    int c = n.shapeCells[k];
                    byte before = n.shapeBefore[k];
                    bool canalNow = map != null ? map.Canal(c) : (before & SiteMap.FCommons) == 0;
                    if ((before & SiteMap.FCanal) == 0 && canalNow) set.Add(c);
                }
            if (set.Count == 0) return;
            int start = -1;
            float far = -1f;
            foreach (int c in set)
            {
                bool byPond = false;
                if (map != null)
                    foreach (int nb in Neighbours(c)) if (map.pond[nb]) { byPond = true; break; }
                float d = Terrain.Dist(SiteMap.CXc(c), SiteMap.CZc(c), x, z) + (byPond ? 100f : 0f);
                if (d > far || (d == far && c < start)) { far = d; start = c; }
            }
            var q = new Queue<int>();
            var seen = new HashSet<int> { start };
            q.Enqueue(start);
            while (q.Count > 0)
            {
                int c = q.Dequeue();
                cells.Add(c);
                foreach (int nb in Neighbours(c))
                    if (set.Contains(nb) && seen.Add(nb)) q.Enqueue(nb);
            }
            // Cells not 4-connected to the rest (never in practice): appended by distance from the pond end.
            if (cells.Count < set.Count)
            {
                var rest = new List<int>();
                foreach (int c in set) if (!seen.Contains(c)) rest.Add(c);
                float sx = SiteMap.CXc(start), sz = SiteMap.CZc(start);
                rest.Sort((a, b) => Terrain.Dist(SiteMap.CXc(a), SiteMap.CZc(a), sx, sz).CompareTo(Terrain.Dist(SiteMap.CXc(b), SiteMap.CZc(b), sx, sz)));
                cells.AddRange(rest);
            }
        }

        void HerdSpots(SiteMap map)
        {
            bool fish = variant == "fish";
            float r = fish ? ShapingTuning.ShoalRadius : ShapingTuning.GrazeRadius;
            var cands = new List<int>();
            var shore = new List<int>();
            foreach (int c in Disc(x, z, r))
            {
                float cx = SiteMap.CXc(c), cz = SiteMap.CZc(c);
                if (Math.Abs(cx) > MaxX - 0.1f || Math.Abs(cz) > MaxZ - 0.1f) continue;
                if (map != null && (fish ? !map.pond[c] : map.Water(c) || map.slope[c] > 0.45f)) continue;
                // Grazing spots keep a step from the shore, so a deer never stands in the water.
                if (map != null && !fish && NearWater(map, cx, cz, ShapingTuning.HerdSpread + 0.08f)) { shore.Add(c); continue; }
                cands.Add(c);
            }
            if (cands.Count == 0) cands.AddRange(shore);
            if (fish && map != null && cands.Count == 0)
            {
                // Flicked just short of the water: the shoal takes the nearest pond cells.
                var pond = new List<int>();
                for (int c = 0; c < SiteMap.N; c++) if (map.pond[c]) pond.Add(c);
                pond.Sort((a, b) => Terrain.Dist(SiteMap.CXc(a), SiteMap.CZc(a), x, z).CompareTo(Terrain.Dist(SiteMap.CXc(b), SiteMap.CZc(b), x, z)));
                for (int i = 0; i < pond.Count && i < 12; i++) cands.Add(pond[i]);
            }
            // A stable pick: hashed order, the gift's own cell first.
            cands.Sort((a, b) => Terrain.Hash3(seed, a, 7).CompareTo(Terrain.Hash3(seed, b, 7)));
            int own = SiteMap.CellAt(x, z);
            if (cands.Remove(own)) cands.Insert(0, own);
            for (int i = 0; i < cands.Count && spots.Count < ShapingTuning.GrazeSpots; i++)
                spots.Add((SiteMap.CXc(cands[i]), SiteMap.CZc(cands[i])));
            if (spots.Count == 0)
                for (int i = 0; i < ShapingTuning.GrazeSpots; i++)
                {
                    double a = i * 2.0 * Math.PI / ShapingTuning.GrazeSpots;
                    spots.Add((ClampX(x + (float)Math.Cos(a) * r * 0.5f), ClampZ(z + (float)Math.Sin(a) * r * 0.5f)));
                }
            if (fish) { entryX = spots[0].x; entryZ = spots[0].z; return; }
            // Deer run in from the nearest box edge, straight towards the spot.
            float dl = x + MaxX, dr = MaxX - x, db = z + MaxZ, df = MaxZ - z;
            float m = Math.Min(Math.Min(dl, dr), Math.Min(db, df));
            if (m == dl) { entryX = -MaxX; entryZ = z; }
            else if (m == dr) { entryX = MaxX; entryZ = z; }
            else if (m == db) { entryX = x; entryZ = -MaxZ; }
            else { entryX = x; entryZ = MaxZ; }
        }

        static bool NearWater(SiteMap map, float x, float z, float r)
        {
            int ri = (int)Math.Ceiling(r / SiteMap.Cell) + 1;
            int c0 = SiteMap.CellAt(x, z);
            if (c0 < 0) return false;
            int i0 = SiteMap.I(c0), j0 = SiteMap.J(c0);
            for (int j = Math.Max(0, j0 - ri); j <= Math.Min(SiteMap.NZ - 1, j0 + ri); j++)
                for (int i = Math.Max(0, i0 - ri); i <= Math.Min(SiteMap.NX - 1, i0 + ri); i++)
                {
                    int c = SiteMap.Index(i, j);
                    if (map.Water(c) && Terrain.Dist(SiteMap.CX(i), SiteMap.CZ(j), x, z) <= r + SiteMap.Cell * 0.71f) return true;
                }
            return false;
        }

        static IEnumerable<int> Disc(float x, float z, float r)
        {
            for (int c = 0; c < SiteMap.N; c++)
                if (Terrain.Dist(SiteMap.CXc(c), SiteMap.CZc(c), x, z) <= r) yield return c;
        }

        static IEnumerable<int> Neighbours(int c)
        {
            int i = SiteMap.I(c), j = SiteMap.J(c);
            if (i > 0) yield return c - 1;
            if (i < SiteMap.NX - 1) yield return c + 1;
            if (j > 0) yield return c - SiteMap.NX;
            if (j < SiteMap.NZ - 1) yield return c + SiteMap.NX;
        }
    }
}
