// #352 AnimBench CLI: clip.json in; track.json, render.json and principles.txt out. run.sh calls this, then render.py.
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace AnimBench
{
    public static class Program
    {
        const string Usage = "usage: AnimBench <clip.json> [--out <dir>] [--strict]\n" +
                             "  writes <dir>/track.json, render.json, principles.txt (default dir: Tools/AnimBench/out/<clip name>)\n" +
                             "  --strict: exit 3 when a principle fails that the clip doesn't list in knownGaps";

        public static int Main(string[] args)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            string clipPath = null, outDir = null; bool strict = false;
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "--out" && i + 1 < args.Length) outDir = args[++i];
                else if (args[i] == "--strict") strict = true;
                else if (args[i] is "-h" or "--help") { Console.WriteLine(Usage); return 0; }
                else clipPath = args[i];
            }
            if (clipPath == null) { Console.Error.WriteLine(Usage); return 2; }
            var sw = Stopwatch.StartNew();
            Clip clip;
            try { clip = Clip.Load(clipPath); }
            catch (Exception e) when (e is ClipException or System.Text.Json.JsonException or IOException or InvalidOperationException)
            {
                Console.Error.WriteLine($"AnimBench: bad clip {clipPath}: {e.Message}");
                return 2;
            }
            outDir ??= Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "out", clip.Name);
            outDir = Path.GetFullPath(outDir);
            Directory.CreateDirectory(outDir);

            Track track;
            try { track = new Simulator(clip).Run(); }
            catch (ClipException e) { Console.Error.WriteLine($"AnimBench: {clip.Name}: {e.Message}"); return 2; }
            var results = Principles.CheckAll(track);
            track.WriteTrack(Path.Combine(outDir, "track.json"), results);
            track.WriteRender(Path.Combine(outDir, "render.json"));
            var report = $"AnimBench principles for '{clip.Name}' ({clip.Duration:0.##}s @ {clip.Fps:0} fps, {track.Dolls.Count} dolls, {track.Objects.Count} objects, {track.Hits.Count} hits)\n" +
                         string.Join("\n", results.Select(r => r.ToString())) + "\n";
            File.WriteAllText(Path.Combine(outDir, "principles.txt"), report);
            Console.Write(report);
            Console.WriteLine($"simulated {track.Count} frames in {sw.ElapsedMilliseconds} ms -> {outDir}");
            if (strict && results.Any(r => r.Applies && !r.Pass && !r.KnownGap)) return 3;
            return 0;
        }
    }
}
