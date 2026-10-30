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
        /// <summary>Real seconds before the same person speaks again.</summary>
        public float speakerGapSeconds = 90f;
        /// <summary>A moment's line (the glass, rain, a return) may come this soon after the last bubble.</summary>
        public float momentGapSeconds = 6f;
        /// <summary>Stories this recent (game months) are talked about.</summary>
        public int storyMonths = 12;
        /// <summary>Stories below this salience aren't talked about.</summary>
        public float storyMinSalience = 0.4f;
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
        readonly HashSet<string> said = new HashSet<string>();
        readonly Dictionary<int, double> lastBy = new Dictionary<int, double>();
        double last = double.NegativeInfinity, started = double.NaN;

        public WorldBubbles(SimCore sim, BubbleRules rules = null)
        {
            this.sim = sim ?? throw new ArgumentNullException(nameof(sim));
            this.rules = rules ?? new BubbleRules();
        }

        /// <summary>Keys said this session (never said again).</summary>
        public IReadOnlyCollection<string> Said => said;

        /// <summary>Forget what was said (a new session, the debug panel).</summary>
        public void Reset() { said.Clear(); lastBy.Clear(); last = double.NegativeInfinity; started = double.NaN; }

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
            if (double.IsNaN(started)) started = now; // the session starts with the first moment the people may speak
            if (now - started < rules.warmUpSeconds) return null;
            double gap = now - last;
            bool momentReady = moment != null && gap >= rules.momentGapSeconds;
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
            var b = Candidates(moment, visible).FirstOrDefault();
            if (b != null) Mark(b, now);
            return b;
        }

        void Mark(WorldBubble b, double now)
        {
            said.Add(b.key);
            if (b.entryId >= 0) { said.Add($"entry:{b.entryId}:q"); said.Add($"entry:{b.entryId}:v"); } // one bubble a story
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
                if (b.speakerId >= 0 && !Alive(b.speakerId)) continue;
                if (b.speakerId >= 0 && visible != null && !visible.Contains(b.speakerId)) continue;
                if (!PeopleVoice.Allowed(b.text, sim)) continue;
                if (PeopleVoice.Wrap(b.text).Count > PeopleVoice.BubbleMaxLines) continue;
                yield return b;
            }
        }

        IEnumerable<WorldBubble> All(string moment)
        {
            // 1. What's happening now, in this stage's words.
            if (!string.IsNullOrEmpty(moment) && moment != PeopleMoment.Any)
            {
                var q = sim.Quote(moment);
                if (q != null) yield return Make($"bank:{q.lineNo}", q.text, q.speakerId, BubbleSource.Moment, -1, q.lineNo);
            }
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
            // 4. Any-time lines of the bank.
            var any = sim.Quote(PeopleMoment.Any);
            if (any != null) yield return Make($"bank:{any.lineNo}", any.text, any.speakerId, BubbleSource.Ambient, -1, any.lineNo);
        }

        static WorldBubble Make(string key, string text, int speakerId, BubbleSource src, int entryId, int lineNo) => new WorldBubble
        {
            key = key, text = text, speakerId = speakerId, source = src, entryId = entryId, lineNo = lineNo,
            seconds = PeopleVoice.SecondsFor(text), lines = PeopleVoice.Wrap(text),
        };

        bool Alive(int id) => sim.FindNotable(id) is Notable n && n.alive;
    }
}
