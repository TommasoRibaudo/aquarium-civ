using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace EcologyLab
{
    /// <summary>
    /// Runs 3 river-valley terrains × 4 scripted Keepers and writes frames, contact sheets and curves.
    ///   dotnet run -c Release                 → docs/design/spikes/img/ecology-lab/
    ///   dotnet run -c Release -- --out DIR    → elsewhere (tuning rounds)
    ///   dotnet run -c Release -- --no-frames  → curves and key years only
    /// </summary>
    public static class Program
    {
        public static readonly int[] Presets = { 0, 1, 2 };
        public static readonly int[] Seeds = { 7, 11, 1 };   // as Growth Lab: seed 1 = 7 on stylebox, 2 = 11 on mirror, 3 = 1 on backpond

        public static int Main(string[] args)
        {
            string outDir = Path.GetFullPath(Path.Combine(FindRepo(), "docs/design/spikes/img/ecology-lab"));
            bool frames = true;
            for (int k = 0; k < args.Length; k++)
            {
                if (args[k] == "--out" && k + 1 < args.Length) outDir = Path.GetFullPath(args[++k]);
                if (args[k] == "--no-frames") frames = false;
            }
            Directory.CreateDirectory(outDir);
            var sw = Stopwatch.StartNew();
            var summary = new StringBuilder(Run.SummaryHeader + "\n");
            for (int p = 0; p < Presets.Length; p++)
            {
                var land = new Land(Presets[p]);
                int seedNo = p + 1;
                var runs = new List<Run>();
                foreach (Strategy st in Enum.GetValues(typeof(Strategy)))
                {
                    var run = new Run(land, st, Seeds[p]).Go();
                    runs.Add(run);
                    string tag = $"seed{seedNo}-{Player.Letter(st)}";
                    Directory.CreateDirectory(Path.Combine(outDir, "curves")); File.WriteAllText(Path.Combine(outDir, "curves", tag + ".csv"), run.CurveCsv());
                    summary.AppendLine(run.SummaryRow(seedNo));
                    Console.WriteLine($"{tag,-9} {Render.KeyYears(run.eco)}");
                    if (!frames) continue;
                    Render.ContactSheet(run, seedNo).SavePng(Path.Combine(outDir, $"sheet-{tag}.png"));
                    foreach (var f in run.frames)
                        Render.Frame(land, f, seedNo, st).SavePng(Path.Combine(outDir, "frames", tag, $"y{f.year:00}.png"));
                }
                if (frames) Render.Compare(runs, seedNo, new[] { 0, 4, 8, 12, 20, 30, 40 }).SavePng(Path.Combine(outDir, $"compare-seed{seedNo}.png"));
            }
            File.WriteAllText(Path.Combine(outDir, "summary.csv"), summary.ToString());
            Console.WriteLine($"done in {sw.ElapsedMilliseconds} ms → {outDir}");
            return 0;
        }

        static string FindRepo()
        {
            var d = new DirectoryInfo(AppContext.BaseDirectory);
            while (d != null && !Directory.Exists(Path.Combine(d.FullName, "docs"))) d = d.Parent;
            return d?.FullName ?? Directory.GetCurrentDirectory();
        }
    }
}
