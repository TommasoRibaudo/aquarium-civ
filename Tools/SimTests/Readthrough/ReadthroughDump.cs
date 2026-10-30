// #61 read-through: what a player would read over a 120-year box, as plain text transcripts.
// Not a check: an [Explicit] tool, run on demand. Not compiled by Unity (it lives under Tools/).
//   dotnet test Tools/SimTests --filter "FullyQualifiedName~ReadthroughDump"
// Writes docs/design/results/readthrough/seed-<n>-*.txt.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using AquariumCiv.Sim;
using NUnit.Framework;

namespace AquariumCiv.Tests
{
    [TestFixture, Explicit("Writes the #61 read-through transcripts; run on demand")]
    public class ReadthroughDump
    {
        static readonly int[] Seeds = { 1, 1234, 77 };
        const int Years = 120;
        static readonly int[] TableYears = { 0, 10, 25, 50, 75, 100, 120 };
        /// <summary>The player shapes the land in long sessions; READTHROUGH_NOSHAPE=1 leaves it alone (to compare counts).</summary>
        static bool Shape => Environment.GetEnvironmentVariable("READTHROUGH_NOSHAPE") != "1";
        static string OutDir => Environment.GetEnvironmentVariable("READTHROUGH_OUT");

        /// <summary>One day of a player who checks in a lot: away 3 h, a 3-min check-in, away 3 h, a 15-min session
        /// (evening talk at its end), twice, then 8 h overnight. Every third day the night becomes a 48 h weekend away.</summary>
        static IEnumerable<(double away, double session, bool longSession)> Schedule()
        {
            for (int day = 0; ; day++)
            {
                yield return (3, 0.05, false);
                yield return (3, 0.25, true);
                yield return (3, 0.05, false);
                yield return (3, 0.25, true);
                yield return (day % 3 == 2 ? 48 : 8, 0.05, false);
            }
        }

        [Test]
        public void Dump()
        {
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            string dir = OutDir ?? Path.Combine(RepoRoot(), "docs", "design", "results", "readthrough");
            Directory.CreateDirectory(dir);
            foreach (int seed in Seeds) DumpSeed(seed, dir);
        }

        sealed class Logged { public ChronicleEntry e; public Era era; public int glass; }

        static void DumpSeed(int seed, string dir)
        {
            var cfg = new SimConfig();
            var sim = new SimCore(cfg, SimCore.NewWorld(seed, cfg));
            var log = sim.State.chronicle.Select(e => new Logged { e = e, era = Era.Camp, glass = 1 }).ToList();
            sim.Chronicled += e => log.Add(new Logged { e = e, era = sim.State.era, glass = sim.GlassStage });

            var returns = new StringBuilder();
            var tables = new StringBuilder();
            var teasers = new List<(long month, Era era, string line, string source)>();
            int nextTable = 0;
            void Table()
            {
                while (nextTable < TableYears.Length && sim.State.Year >= TableYears[nextTable])
                {
                    tables.Append(NotableTable(sim, $"Year {TableYears[nextTable]} (seen at year {sim.State.Year:0.0})"));
                    nextTable++;
                }
            }

            // The first session: 15 minutes live, then the evening talk.
            sim.Advance(0.25);
            Table();
            string first = sim.OpenTeaser();
            teasers.Add((sim.State.month, sim.State.era, first, sim.FindThread(sim.State.teaserThreadId)?.source));
            returns.AppendLine($"== First session ends, year {sim.State.Year:0.0}. Evening talk: {Q(first)}");
            long seen = sim.State.month;
            double realHours = 0.25;
            int n = 0;

            foreach (var (away, session, longSession) in Schedule())
            {
                if (sim.State.Year >= Years) break;
                n++;
                var r = sim.CatchUp(away);
                realHours += away;
                var stack = sim.ReturnCards(seen, away);
                returns.AppendLine();
                returns.AppendLine($"== Return {n}: after {away:0} h away (real h {realHours:0}), year {(seen / 12.0):0.0} -> {sim.State.Year:0.0}, " +
                                   $"{sim.State.era}{(sim.State.eraReady ? " (next era READY)" : "")}, {stack.collected} entries, cap {stack.cap}");
                string peek = stack.cards.Count > 0 ? stack.cards[0].headline : null;
                returns.AppendLine($"   Peek: {Q(peek ?? "(no cards) " + sim.CurrentTeaser)}   | teaser under it: {Q(sim.CurrentTeaser)}");
                int i = 0;
                foreach (var c in stack.cards)
                {
                    i++;
                    returns.AppendLine($"   {i}. [{c.kind}/{c.tone}/{c.group} {c.score:0.00}] {c.headline}");
                    if (!string.IsNullOrEmpty(c.teaser)) returns.AppendLine($"        (at the fire: {c.teaser})");
                    returns.AppendLine($"        {c.lead.text}");
                    foreach (var other in c.entries.Where(x => x != c.lead)) returns.AppendLine($"        + {other.text}");
                    if (!string.IsNullOrEmpty(c.endLine)) returns.AppendLine($"        ({c.endLine})");
                }
                if (stack.also.Count > 0)
                    returns.AppendLine($"   Also: {string.Join(" / ", stack.alsoHeadlines)}{(stack.alsoMore > 0 ? $" (+{stack.alsoMore} more)" : "")}");

                if (sim.State.eraReady)
                {
                    sim.AdvanceEra(); // the player taps the waiting leader and watches the era moment
                    returns.AppendLine($"   ** The player watches the era moment: now {sim.State.era} (year {sim.State.Year:0.0}).");
                }
                Table();
                sim.Advance(session);
                realHours += session;
                if (longSession && Shape)
                {
                    // A long session shapes the land once: the era's first gift tool that is ready (Gate 3).
                    string[] tools = sim.State.era switch
                    {
                        Era.Camp => new[] { SimCore.NudgeKind.Rain, SimCore.NudgeKind.Grove, SimCore.NudgeKind.Herd },
                        Era.Village => new[] { SimCore.NudgeKind.Channel, SimCore.NudgeKind.Lamp, SimCore.NudgeKind.Rain },
                        _ => new[] { SimCore.NudgeKind.Outcrop, SimCore.NudgeKind.Spring, SimCore.NudgeKind.DyeMeadow },
                    };
                    var tool = tools.FirstOrDefault(k => sim.NudgeCooldown(k) == 0);
                    if (tool != null && !sim.State.nudges.Any(x => !x.credited && sim.State.month - x.month < SimCore.NudgeMonths))
                    {
                        sim.PlaceNudge(tool, (n * 7) % cfg.buildingSlots);
                        returns.AppendLine($"   (the player shapes: {tool})");
                    }
                }
                if (longSession)
                {
                    string t = sim.OpenTeaser();
                    var th = sim.FindThread(sim.State.teaserThreadId);
                    teasers.Add((sim.State.month, sim.State.era, t, t == null ? "era-ready" : th?.source));
                    returns.AppendLine($"   Evening talk (end of a 15-min session, year {sim.State.Year:0.0}): {Q(t ?? "(era ready: the waiting leader replaces the teaser)")}");
                }
                seen = sim.State.month;
            }
            Table();

            // Teaser outcomes.
            var teaserOut = new StringBuilder();
            teaserOut.AppendLine("== Teaser threads (said at the evening talk) and how they ended");
            foreach (var t in sim.State.threads.Where(t => t.source != ThreadSource.Link))
            {
                var pay = sim.State.chronicle.FirstOrDefault(e => e.id == t.payoffEntryId);
                teaserOut.AppendLine($"  Y{t.month / 12,3} [{t.source}] {t.teaser}");
                teaserOut.AppendLine($"        -> {t.state}{(pay != null ? $" Y{pay.month / 12}: {pay.text}" : "")}{(t.endLine != "" ? $" ({t.endLine})" : "")}");
            }

            File.WriteAllText(Path.Combine(dir, $"seed-{seed}-chronicle.txt"), ChronicleText(sim, log, seed));
            File.WriteAllText(Path.Combine(dir, $"seed-{seed}-returns.txt"),
                $"# Seed {seed}: returns as the player sees them\n# Schedule: away 3 h / 3-min check-in, 3 h / 15-min session (evening talk), x2, then 8 h overnight; every 3rd night is 48 h.\n\n" +
                returns + "\n" + teaserOut);
            File.WriteAllText(Path.Combine(dir, $"seed-{seed}-notables.txt"), $"# Seed {seed}: notables\n\n" + tables);
            File.WriteAllText(Path.Combine(dir, $"seed-{seed}-stats.txt"), Stats(sim, log, teasers, seed));
        }

        static string Q(string s) => s == null ? "(none)" : "\"" + s + "\"";

        static string ChronicleText(SimCore sim, List<Logged> log, int seed)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"# Seed {seed}: the full chronicle, {Years} years, eras advanced at the player's next return");
            sb.AppendLine("# year.month  era      glass  kind  (salience)  text");
            foreach (var l in log)
                sb.AppendLine($"Y{l.e.month / 12,3}.{l.e.month % 12,-2} {l.era,-7} g{l.glass} {l.e.kind,-16} ({l.e.salience:0.00}) {l.e.text}");
            return sb.ToString();
        }

        static string NotableTable(SimCore sim, string title)
        {
            var s = sim.State;
            var sb = new StringBuilder();
            sb.AppendLine($"== {title}, {s.era}, population {s.population}, buildings {s.buildings.Count}");
            sb.AppendLine("   name             age  life  trait          job          role                  parent           partner          friend / rival");
            foreach (var n in s.notables.Where(n => n.alive).OrderByDescending(n => n.age))
            {
                string Name(int id) => id < 0 ? "-" : (sim.FindNotable(id) is Notable o ? o.FullName + (o.alive ? "" : "†") : "?");
                sb.AppendLine($"   {n.FullName,-16} {Math.Floor(n.age),3} {Math.Floor(n.lifespan),4}  {n.trait,-14} {(n.job == "" ? "-" : n.job),-12} {sim.RoleOf(n) ?? "-",-21} {Name(n.parentId),-16} {Name(n.partnerId),-16} {Name(n.friendId)} / {Name(n.rivalId)}");
            }
            var seats = s.roles.Select(r => $"{r.role}: {(r.holderId >= 0 ? sim.FindNotable(r.holderId)?.FullName : "(empty)")}");
            sb.AppendLine($"   Seats: {string.Join("; ", seats)}");
            sb.AppendLine();
            return sb.ToString();
        }

        // ---------- counts and automatic flags (for the report; reading is still the point) ----------
        static string Stats(SimCore sim, List<Logged> log, List<(long month, Era era, string line, string source)> teasers, int seed)
        {
            var s = sim.State;
            var sb = new StringBuilder();
            sb.AppendLine($"# Seed {seed}: counts and automatic flags");
            var eraStarts = log.Where(l => l.e.kind == "era").Select(l => $"{l.era} at Y{l.e.month / 12}");
            var ready = log.Where(l => l.e.kind == "era-ready").Select(l => $"ready Y{l.e.month / 12}");
            sb.AppendLine($"Eras: {string.Join(", ", ready.Concat(eraStarts))}");
            sb.AppendLine($"Entries: {log.Count}; shown as cards: {log.Count(l => SimCore.ShownAsCard(l.e))}");
            sb.AppendLine();
            sb.AppendLine("Card-worthy entries per decade (all / term_ lines / role_ lines / deaths / births):");
            for (int d = 0; d < Years / 10; d++)
            {
                var dec = log.Where(l => l.e.month / 120 == d && SimCore.ShownAsCard(l.e)).ToList();
                sb.AppendLine($"  {d * 10,3}-{d * 10 + 9,3}: {dec.Count,3} / {dec.Count(l => l.e.kind.StartsWith("term_")),2} / {dec.Count(l => l.e.kind.StartsWith("role_")),2} / {dec.Count(l => l.e.kind == "death"),2} / {dec.Count(l => l.e.kind == "birth"),2}   " +
                              string.Join(" ", dec.Select(l => l.era.ToString()[0]).Distinct()));
            }
            sb.AppendLine();
            sb.AppendLine("By kind:");
            foreach (var g in log.GroupBy(l => l.e.kind).OrderByDescending(g => g.Count()))
                sb.AppendLine($"  {g.Key,-18} {g.Count(),3}");
            sb.AppendLine();

            // Templates: the text with every notable's name and the settlement name taken out.
            var names = s.notables.Select(x => x.FullName).Distinct().OrderByDescending(x => x.Length).ToList();
            string Tmpl(string t)
            {
                foreach (var nm in names) t = t.Replace(nm, "{N}");
                if (!string.IsNullOrEmpty(s.settlementName)) t = t.Replace(s.settlementName, "{P}");
                return Regex.Replace(t, @"\d+", "#");
            }
            sb.AppendLine("Most repeated templates (names and numbers taken out):");
            foreach (var g in log.GroupBy(l => Tmpl(l.e.text)).Where(g => g.Count() > 2).OrderByDescending(g => g.Count()).Take(25))
                sb.AppendLine($"  x{g.Count(),-3} {g.Key}");
            sb.AppendLine($"Exact duplicate texts: {log.GroupBy(l => l.e.text).Count(g => g.Count() > 1)}");
            foreach (var g in log.GroupBy(l => l.e.text).Where(g => g.Count() > 1).Take(15))
                sb.AppendLine($"  x{g.Count()} {g.Key}");
            sb.AppendLine();

            sb.AppendLine("Evening-talk teasers (" + teasers.Count + "):");
            foreach (var g in teasers.GroupBy(t => t.line ?? "(era ready)").OrderByDescending(g => g.Count()).Take(20))
                sb.AppendLine($"  x{g.Count(),-3} [{g.First().source}] {g.Key}");
            sb.AppendLine();

            // Automatic flags.
            sb.AppendLine("Flags:");
            int flags = 0;
            void Flag(Logged l, string why) { flags++; sb.AppendLine($"  Y{l.e.month / 12,3} {l.era,-7} {l.e.kind,-16} {why}: {l.e.text}"); }
            foreach (var l in log)
            {
                string t = l.e.text;
                // A name in the text that belongs to nobody alive that month (and someone dead).
                foreach (var nm in names)
                {
                    if (!t.Contains(nm)) continue;
                    bool aliveThen = s.notables.Any(x => x.FullName == nm && x.bornMonth <= l.e.month && (x.alive || x.diedMonth >= l.e.month));
                    if (!aliveThen) Flag(l, $"names {nm}, not alive then");
                }
                if (l.era == Era.Camp && Regex.IsMatch(t, @"\b(village|town|street|house|houses)\b")) Flag(l, "village/town word in the camp");
                if (l.era == Era.Village && Regex.IsMatch(t, @"\b(town|camp)\b") && !t.Contains("became a farming village") && !t.Contains("The camp became")) Flag(l, "camp/town word in the village");
                if (l.era == Era.Town && Regex.IsMatch(t, @"\b(camp|village)\b") && !t.Contains("village became")) Flag(l, "camp/village word in the town");
                if (l.era == Era.Camp && Regex.IsMatch(t, @"\b(tavern|market|chapel|church|mill|windmill)\b") && !l.e.kind.StartsWith("first_")) Flag(l, "a building the camp may not have");
                if (l.era != Era.Camp && t.Contains("Bright One")) Flag(l, "camp word for the Keeper after the camp");
                var who = l.e.notableId >= 0 ? sim.FindNotable(l.e.notableId) : null;
                var m = Regex.Match(t, @"the (\w+) became (chief|headman|lord|priest|miller)");
                if (m.Success) { /* job at the time is not stored; checked by reading */ }
                if (l.e.kind == "death" && who != null && who.age < cfgLifeMin) Flag(l, "died young");
                if (l.e.kind == "birth" && who != null && sim.FindNotable(who.parentId) is Notable p)
                {
                    double pAge = p.age - (s.month - l.e.month) / 12.0 * (p.alive ? 1 : 0);
                    if (!p.alive) pAge = p.age - (p.diedMonth - l.e.month) / 12.0;
                    if (pAge < 20 || pAge > 41) Flag(l, $"parent aged {pAge:0}");
                }
            }
            foreach (var t in teasers.Where(t => t.line != null))
            {
                if (t.era < Era.Town && t.line.Contains("glass")) { flags++; sb.AppendLine($"  Y{t.month / 12,3} teaser says glass before the town: {t.line}"); }
                if (t.era == Era.Town && t.line.Contains("glass") && !s.storyFlags.Contains("relic:lens")) { flags++; sb.AppendLine($"  Y{t.month / 12,3} teaser says glass before the Lens: {t.line}"); }
                if (t.era == Era.Camp && t.line.Contains("Keeper")) { flags++; sb.AppendLine($"  Y{t.month / 12,3} camp teaser says Keeper: {t.line}"); }
            }
            sb.AppendLine($"  ({flags} flags)");
            return sb.ToString();
        }

        const float cfgLifeMin = 55f;

        static string RepoRoot()
        {
            var d = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
            while (d != null && !(Directory.Exists(Path.Combine(d.FullName, "docs")) && Directory.Exists(Path.Combine(d.FullName, "AquariumCiv")))) d = d.Parent;
            return d?.FullName ?? throw new DirectoryNotFoundException("repo root");
        }
    }
}
