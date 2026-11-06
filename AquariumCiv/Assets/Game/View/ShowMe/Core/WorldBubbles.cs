using System;
using System.Collections.Generic;
using System.Linq;
using AquariumCiv.Sim;

namespace AquariumCiv.View.ShowMe
{
    /// <summary>How often the people speak up in the world (#76). [est] values, tune here.</summary>
    public sealed class BubbleRules
    {
        /// <summary>Real seconds between two ambient bubbles (one at a time on a phone).</summary>
        public float minGapSeconds = 20f;
        /// <summary>Real seconds of quiet before the first bubble of a session (user 2026-10-30: the player settles in first).</summary>
        public float warmUpSeconds = 8f;
        /// <summary>Real seconds before the same want line (#181) is said again.</summary>
        public float wantRepeatSeconds = 300f;
        /// <summary>Ordinary bubbles that come between two want lines: only some of the talk is about the town.</summary>
        public int bubblesBetweenWants = 2;
        /// <summary>Real seconds before the same person speaks again.</summary>
        public float speakerGapSeconds = 90f;
        /// <summary>A moment's line (the glass, rain, a return) may come this soon after the last bubble.</summary>
        public float momentGapSeconds = 6f;
        /// <summary>Stories this recent (game months) are talked about.</summary>
        public int storyMonths = 12;
        /// <summary>Stories below this salience aren't talked about.</summary>
        public float storyMinSalience = 0.4f;
        /// <summary>Ordinary bubbles that come between two tree lines (#318): the tree is one topic among several.</summary>
        public int bubblesBetweenTree = 2;
    }

    /// <summary>
    /// The people's ambient speech bubbles (#76; chronicle-and-history §5, relics-and-glass-voice §5): which line, who says
    /// it, when. One at a time, rate limited, never the same line twice in a session, only what this belief stage may say
    /// (<see cref="SimCore.Quote"/>, <see cref="SimCore.LinesNow"/>), recent stories in the words of the people in them.
    /// Quiet while the cards are open or a moment plays (the card is never covered; the echo has its own line).
    /// Deterministic: no dice, the order is fixed (a moment's line, then recent stories newest first, then the bank's story
    /// lines, then any-time lines); the sim is only read.
    /// </summary>
    public sealed class WorldBubbles
    {
        readonly SimCore sim;
        public readonly BubbleRules rules;
        /// <summary>Keys said, kept in the box's own state (<see cref="SimState.saidBubbles"/>) so a new session doesn't repeat them (#120).</summary>
        readonly HashSet<string> said = new HashSet<string>();
        /// <summary>How many lines of memory a box keeps (oldest forgotten first).</summary>
        public const int SaidKept = 400;
        /// <summary>Want lines (what the town wants, #181) can come back, but not the same one soon, and only every few bubbles.</summary>
        readonly Dictionary<string, double> wantAt = new Dictionary<string, double>();
        readonly Dictionary<string, int> wantTold = new Dictionary<string, int>();
        int sinceWant;
        int sinceTree = int.MaxValue / 2;
        double clock;
        readonly Dictionary<int, double> lastBy = new Dictionary<int, double>();
        double last = double.NegativeInfinity, started = double.NaN;

        /// <summary>t1-moments: a staged moment is playing in the box (null = none). While it does, the people say its own few lines
        /// (<see cref="PeopleVoice.MomentLines"/>), nothing from the stories: a festival is not the time for "I'm thinking about
        /// getting up".</summary>
        public HeldMoment HeldNow { get; set; }

        public WorldBubbles(SimCore sim, BubbleRules rules = null)
        {
            this.sim = sim ?? throw new ArgumentNullException(nameof(sim));
            this.rules = rules ?? new BubbleRules();
            foreach (var k in sim.State.saidBubbles) said.Add(k);
        }

        /// <summary>Keys said this session (never said again).</summary>
        public IReadOnlyCollection<string> Said => said;

        /// <summary>Forget what was said (a new session, the debug panel).</summary>
        public void Reset() { said.Clear(); sim.State.saidBubbles.Clear(); wantAt.Clear(); wantTold.Clear(); sinceWant = 0; sinceTree = int.MaxValue / 2; lastBy.Clear(); last = double.NegativeInfinity; started = double.NaN; }

        /// <summary>The player came back to the box: a fresh warm-up before the next bubble (what was said stays said).</summary>
        public void WarmUp() => started = double.NaN;

        /// <summary>
        /// The next bubble at real time <paramref name="now"/> (seconds), or null (too soon, quiet, nothing new).
        /// <paramref name="moment"/>: what's happening (a <see cref="PeopleMoment"/>: someone at the glass, rain, the lamp, a
        /// return...) or null for an ordinary moment. <paramref name="visible"/>: the notables on screen (null = anyone); a
        /// crowd line can always be said. <paramref name="quiet"/>: the cards are open or a moment plays.
        /// A bubble returned is counted as said.
        /// </summary>
        public WorldBubble Next(double now, string moment = null, ICollection<int> visible = null, bool quiet = false)
        {
            if (quiet) return null;
            clock = now;
            if (double.IsNaN(started)) started = now; // the session starts with the first moment the people may speak
            if (now - started < rules.warmUpSeconds) return null;
            double gap = now - last;
            bool momentReady = (moment != null || HeldNow != null) && gap >= rules.momentGapSeconds;
            if (gap < rules.minGapSeconds && !momentReady) return null;
            foreach (var b in Candidates(moment, visible))
            {
                if (b.source != BubbleSource.Moment && gap < rules.minGapSeconds) continue;
                if (b.speakerId >= 0 && lastBy.TryGetValue(b.speakerId, out var t) && now - t < rules.speakerGapSeconds) continue;
                Mark(b, now);
                return b;
            }
            return null;
        }

        /// <summary>Debug panel ("force a bubble"): the next line ignoring the gaps (still never one already said, still only
        /// what this stage may say). Null when everything has been said.</summary>
        public WorldBubble Force(double now, string moment = null, ICollection<int> visible = null)
        {
            clock = now;
            var b = Candidates(moment, visible).FirstOrDefault();
            if (b != null) Mark(b, now);
            return b;
        }

        /// <summary>Debug panel: say the want line for one case now (#181), ignoring the gaps. Null when the case isn't true now
        /// (no hungry town, no building short of wood...).</summary>
        public WorldBubble ForceWant(WantCase c, double now)
        {
            clock = now;
            var l = PeopleVoice.WantLineFor(sim, c, wantTold.TryGetValue(c.ToString(), out var n) ? n : 0);
            if (l == null) return null;
            var b = MakeWant(l);
            Mark(b, now);
            return b;
        }

        /// <summary>Debug panel (#318): the next tree line ignoring the gaps (a line already said is skipped). Null when the tree
        /// isn't tended yet, or every line that fits its stage has been said.</summary>
        public WorldBubble ForceTree(double now)
        {
            clock = now;
            var b = TreeBubbles().FirstOrDefault(x => !said.Contains(x.key));
            if (b != null) Mark(b, now);
            return b;
        }

        IEnumerable<WorldBubble> TreeBubbles()
        {
            foreach (var (l, who, text) in PeopleVoice.TreeLinesNow(sim))
                yield return Make($"tree:{l.id}", text, who, BubbleSource.Tree, -1, 0);
        }

        void Remember(string key)
        {
            if (!said.Add(key)) return;
            var kept = sim.State.saidBubbles;
            kept.Add(key);
            while (kept.Count > SaidKept) { said.Remove(kept[0]); kept.RemoveAt(0); }
        }

        void Mark(WorldBubble b, double now)
        {
            if (b.source == BubbleSource.Want)
            {
                wantAt[b.key] = now; sinceWant = 0;
                wantTold[b.wantCase.ToString()] = (wantTold.TryGetValue(b.wantCase.ToString(), out var n) ? n : 0) + 1;
            }
            else
            {
                Remember(b.key);
                sinceWant++;
            }
            if (b.source == BubbleSource.Tree) sinceTree = 0; else sinceTree++;
            if (b.entryId >= 0) { Remember($"entry:{b.entryId}:q"); Remember($"entry:{b.entryId}:v"); } // one bubble a story
            if (b.speakerId >= 0) lastBy[b.speakerId] = now;
            last = now;
        }

        /// <summary>Every line that could be said now, in order, not yet said (the debug panel lists them).</summary>
        public IEnumerable<WorldBubble> Candidates(string moment = null, ICollection<int> visible = null)
        {
            var seen = new HashSet<string>();
            foreach (var b in All(moment))
            {
                if (b == null || said.Contains(b.key) || !seen.Add(b.key)) continue;
                if (b.source == BubbleSource.Want && wantAt.TryGetValue(b.key, out var wt) && clock - wt < rules.wantRepeatSeconds) continue;
                if (b.speakerId >= 0 && !Alive(b.speakerId)) continue;
                if (b.speakerId >= 0 && visible != null && !visible.Contains(b.speakerId)) continue;
                if (!PeopleVoice.Allowed(b.text, sim)) continue;
                if (PeopleVoice.Wrap(b.text).Count > PeopleVoice.BubbleMaxLines) continue;
                yield return b;
            }
        }

        IEnumerable<WorldBubble> All(string moment)
        {
            if (HeldNow != null)
            {
                var lines = PeopleVoice.MomentLines(HeldNow.kind, HeldNow.personA, HeldNow.personB);
                for (int i = 0; i < lines.Count; i++)
                    yield return Make($"held:{HeldNow.id}:{i}", lines[i].text, lines[i].speaker, BubbleSource.Moment, -1, 0);
                yield break;
            }
            // 1. What's happening now, in this stage's words.
            if (!string.IsNullOrEmpty(moment) && moment != PeopleMoment.Any)
            {
                var q = sim.Quote(moment);
                if (q != null) yield return Make($"bank:{q.lineNo}", q.text, q.speakerId, BubbleSource.Moment, -1, q.lineNo);
            }
            // What the town wants comes round every few bubbles, ahead of the stories; otherwise it waits behind them.
            bool wantsFirst = sinceWant >= rules.bubblesBetweenWants;
            if (wantsFirst) foreach (var w in Wants()) yield return w;
            // The tree, once tended (#318): comes round every few bubbles ahead of the stories; otherwise it waits behind the trait lines.
            bool treeFirst = sinceTree >= rules.bubblesBetweenTree;
            if (treeFirst) foreach (var tb in TreeBubbles()) yield return tb;
            // 2. Recent stories, newest first: their quote, else the words of the one it's about.
            var chron = sim.State.chronicle;
            long from = sim.State.month - rules.storyMonths;
            for (int i = chron.Count - 1; i >= 0; i--)
            {
                var e = chron[i];
                if (e.month < from) break;
                if (e.salience < rules.storyMinSalience || !SimCore.ShownAsCard(e)) continue;
                if (!string.IsNullOrEmpty(e.quote))
                    yield return Make($"entry:{e.id}:q", e.quote, e.speakerId, BubbleSource.StoryQuote, e.id, 0);
                var v = PeopleVoice.StoryLine(sim, e);
                if (v != null) yield return Make($"entry:{e.id}:v", v.text, v.speakerId, BubbleSource.StoryVoice, e.id, 0);
            }
            // 3. The bank's story lines in the speaker's own words.
            foreach (var q in PeopleVoice.BankLinesNow(sim))
                yield return Make($"bank:{q.lineNo}:me", q.text, q.speakerId, BubbleSource.BankVoice, -1, q.lineNo);
            foreach (var (tl, who) in PeopleVoice.TraitLinesNow(sim))
                yield return Make($"voice:{tl.id}", tl.text, who, BubbleSource.TraitVoice, -1, 0);
            if (!treeFirst) foreach (var tb in TreeBubbles()) yield return tb;
            if (!wantsFirst) foreach (var w in Wants()) yield return w;
            // 4. Any-time lines of the bank.
            var any = sim.Quote(PeopleMoment.Any);
            if (any != null) yield return Make($"bank:{any.lineNo}", any.text, any.speakerId, BubbleSource.Ambient, -1, any.lineNo);
        }

        IEnumerable<WorldBubble> Wants()
        {
            foreach (var l in PeopleVoice.WantLines(sim, 0))
            {
                // The wording turns with each telling; the same one isn't picked twice running.
                int told = wantTold.TryGetValue(l.kind.ToString(), out var n) ? n : 0;
                var l2 = told == 0 ? l : PeopleVoice.WantLines(sim, told).FirstOrDefault(x => x.kind == l.kind) ?? l;
                yield return MakeWant(l2);
            }
        }

        WorldBubble MakeWant(WantLine l)
        {
            var b = Make($"want:{l.kind}:{l.building}:{l.variant}", l.text, -1, BubbleSource.Want, -1, 0);
            b.wantCase = l.kind;
            return b;
        }

        static WorldBubble Make(string key, string text, int speakerId, BubbleSource src, int entryId, int lineNo) => new WorldBubble
        {
            key = key, text = text, speakerId = speakerId, source = src, entryId = entryId, lineNo = lineNo,
            seconds = PeopleVoice.SecondsFor(text), lines = PeopleVoice.Wrap(text),
        };

        bool Alive(int id) => sim.FindNotable(id) is Notable n && n.alive;
    }
}
