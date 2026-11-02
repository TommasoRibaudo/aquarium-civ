using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace EcologyLab
{
    /// <summary>One sample of the curves (taken at the end of each game year).</summary>
    public struct Sample
    {
        public float year, grassPct, tallPct, bushPct, humidity, droplets, grime, table, light;
        public int seedlings, young, trees, reeds, pond, treeStage, wildSeeds;
        public float rabbits, birds, treeProgress, treeWeeds, treeMoist;
        public bool camp, village, tooHumid;
    }

    /// <summary>A picture of the box at one moment (what a frame is drawn from).</summary>
    public sealed class Snapshot
    {
        public float year;
        public Cover[] cover;
        public float[] moist;
        public float table, pondLevel, humidity, droplets, grime, rabbits, birds;
        public int treeCell, treeStage, trees, pond;
        public bool camp, village, treeWilting;
    }

    /// <summary>One (land, strategy) run: steps the ecology season by season, keeps the curves, the frames and the
    /// key years. Frames to <see cref="frameYears"/>, the key years on to <see cref="years"/>.</summary>
    public sealed class Run
    {
        public readonly Land land;
        public readonly Strategy strategy;
        public readonly Eco eco;
        public readonly Player player;
        public readonly List<Sample> curve = new List<Sample>();
        public readonly List<Snapshot> frames = new List<Snapshot>();
        public int years = 100, frameYears = 40, frameEvery = 2;
        public int tooHumidSeasons, visits;
        /// <summary>Called after every season (tests watch for die-back here).</summary>
        public Action<Eco, Cover[]> afterStep;

        public Run(Land land, Strategy strategy, int seed, Tuning tuning = null)
        {
            this.land = land; this.strategy = strategy;
            eco = new Eco(land, seed, tuning);
            player = new Player(strategy);
        }

        public Run Go()
        {
            var v = new Visit();
            var before = new Cover[Land.N];
            frames.Add(Snap());
            for (int s = 0; s < years * 4; s++)
            {
                bool there = player.Plan(eco, v);
                if (there) visits++;
                Array.Copy(eco.cover, before, Land.N);
                eco.Step(there ? v : null);
                if (eco.TooHumid && eco.Year <= frameYears) tooHumidSeasons++;
                afterStep?.Invoke(eco, before);
                if (eco.season % 4 == 0)
                {
                    curve.Add(Sample());
                    int y = eco.season / 4;
                    if (y <= frameYears && y % frameEvery == 0) frames.Add(Snap());
                }
            }
            return this;
        }

        Sample Sample()
        {
            var e = eco;
            return new Sample
            {
                year = e.Year, grassPct = e.LandFrac(e.grass) * 100f, tallPct = e.LandFrac(e.tall) * 100f, bushPct = e.BushFrac * 100f,
                humidity = e.humidity, droplets = e.droplets, grime = e.grime, table = e.table, light = e.Light,
                seedlings = e.seedlings, young = e.young, trees = e.grownTrees, reeds = e.reeds, pond = e.pondCells,
                treeStage = e.tree.Planted ? e.tree.stage : -1, wildSeeds = e.wildSeeds,
                rabbits = e.rabbits, birds = e.birds, treeProgress = e.tree.progress, treeWeeds = e.tree.weeds,
                treeMoist = e.tree.Planted ? e.moist[e.tree.cell] : 0f,
                camp = e.CampStarted, village = e.villageYear >= 0, tooHumid = e.TooHumid,
            };
        }

        Snapshot Snap()
        {
            var e = eco;
            return new Snapshot
            {
                year = e.Year, cover = (Cover[])e.cover.Clone(), moist = (float[])e.moist.Clone(),
                table = e.table, pondLevel = e.pondLevel, humidity = e.humidity, droplets = e.droplets, grime = e.grime,
                rabbits = e.rabbits, birds = e.birds, treeCell = e.tree.cell, treeStage = e.tree.stage,
                trees = e.grownTrees, pond = e.pondCells, camp = e.CampStarted, village = e.villageYear >= 0, treeWilting = e.tree.wilting,
            };
        }

        static string F(float v, string fmt = "0.00") => v.ToString(fmt, CultureInfo.InvariantCulture);

        public string CurveCsv()
        {
            var sb = new StringBuilder();
            sb.AppendLine("year,grass_pct,tall_pct,bush_pct,seedlings,young,trees,reeds,pond_cells,rabbits,birds,humidity,droplets,grime,light,water_table,tree_stage,tree_progress,tree_weeds,tree_moisture,camp,village");
            foreach (var s in curve)
                sb.AppendLine(string.Join(",", F(s.year, "0"), F(s.grassPct, "0.0"), F(s.tallPct, "0.0"), F(s.bushPct, "0.0"), s.seedlings, s.young, s.trees, s.reeds, s.pond,
                    F(s.rabbits, "0.0"), F(s.birds, "0.0"), F(s.humidity), F(s.droplets), F(s.grime), F(s.light), F(s.table, "0.000"), s.treeStage,
                    F(s.treeProgress), F(s.treeWeeds), F(s.treeMoist), s.camp ? 1 : 0, s.village ? 1 : 0));
            return sb.ToString();
        }

        public static string Y(float y) => y < 0 ? "never" : y.ToString("0.0", CultureInfo.InvariantCulture);

        public const string SummaryHeader = "seed,terrain,strategy,camp_year,first_bush_year,seed_planted_year,people_water_year,keeper_tree_grown_year,first_tree_grown_year,water_surfaced_year,village_year,trees_y40,pond_y40,meadow_pct_y40,bush_pct_y40,rabbits_y40,too_humid_seasons_to_y40,visits";

        public string SummaryRow(int seedNo)
        {
            var e = eco;
            var at40 = curve[Math.Min(curve.Count, frameYears) - 1];
            return string.Join(",", seedNo, land.spec.name, Player.Letter(strategy), Y(e.campYear), Y(e.bushYear), Y(e.plantedYear), Y(e.peopleWaterYear),
                Y(e.keeperGrownYear), Y(e.firstTreeYear), Y(e.surfacedYear), Y(e.villageYear), at40.trees, at40.pond,
                F(at40.grassPct + at40.tallPct, "0.0"), F(at40.bushPct, "0.0"), F(at40.rabbits, "0.0"), tooHumidSeasons, visits);
        }
    }
}
