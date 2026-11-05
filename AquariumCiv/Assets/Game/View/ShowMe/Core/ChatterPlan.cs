using System;
using System.Collections.Generic;
using System.Linq;

namespace AquariumCiv.View.ShowMe
{
    /// <summary>The five marks of the wordless chatter (#144, research 12 §5): … ♪ ♥ ! ?</summary>
    public enum ChatterMark { None, Dots, Note, Heart, Bang, Question }

    /// <summary>How people talk without words (#144 research 12 §6). The debug panel keeps this until the user picks.</summary>
    public enum ChatterStyle { Off, Marks, MarksBabble, GestureBabble }

    /// <summary>What one person feels about the one they talk to; picks the mark (friend ♥/♪, rival !, unsure ?, idle …).</summary>
    public enum Rapport { Idle, Friend, Partner, Family, Rival, Unsure }

    public sealed class ChatterPerson
    {
        public int id;
        public bool asleep;
        /// <summary>0..3, which babble voice (<see cref="ChatterBabble.VoiceFor"/>).</summary>
        public int voice;
        /// <summary>#316: a child (higher, quicker voice).</summary>
        public bool kid;
        /// <summary>Rapport toward the other members of the group, by their id. Missing = <see cref="Rapport.Idle"/>.</summary>
        public Dictionary<int, Rapport> toward = new Dictionary<int, Rapport>();
    }

    /// <summary>People standing together (at the fire, at the door...). The view builds these each frame or on change.</summary>
    public sealed class ChatterGroup
    {
        public int id;
        /// <summary>At the fire or another warm spot: ♪ is likelier.</summary>
        public bool nearFire;
        /// <summary>#316: somebody in the group is at work (a call out is likelier), and the group is on screen (only those are heard).</summary>
        public bool working, onScreen = true;
        public List<ChatterPerson> members = new List<ChatterPerson>();
    }

    /// <summary>One mark shown over one person.</summary>
    public sealed class ChatterShow
    {
        public int groupId = -1, speakerId, listenerId = -1, voice;
        public ChatterMark mark;
        public double startAt, endAt;
        /// <summary>Babble volume factor 0..1 (0 = silent: style has no babble or the zoom is too far).</summary>
        public float babble;
        /// <summary>A reaction (the knock's !), not a turn in a chat.</summary>
        public bool reaction;
    }

    /// <summary>What to do this frame.</summary>
    public sealed class ChatterTick
    {
        /// <summary>New marks to pop now.</summary>
        public readonly List<ChatterShow> show = new List<ChatterShow>();
        /// <summary>Live marks to fade out now (a real line started, a quiet case began, the style turned off).</summary>
        public readonly List<ChatterShow> fade = new List<ChatterShow>();
    }

    public sealed class ChatterRules
    {
        /// <summary>Real seconds between two turns in one group (3 to 6).</summary>
        public float minGap = 3f, maxGap = 6f;
        /// <summary>Marks alive in the box at once.</summary>
        public int maxLive = 4;
        /// <summary>Marks live when fully zoomed out (a column of tiny glyphs is noise).</summary>
        public int maxLiveZoomedOut = 1;
        public float markSeconds = 2.2f, bangSeconds = 1.2f;
        /// <summary>Zoom 0 = the whole box, 1 = closest. Below this the box counts as zoomed out.</summary>
        public float zoomedOut = 0.2f;
        /// <summary>Babble fades in from zero at <c>babbleFrom</c> to full at <c>babbleFull</c>.</summary>
        public float babbleFrom = 0.5f, babbleFull = 0.8f;
        /// <summary>Real seconds before the first turn of a group seen new (staggered by a roll).</summary>
        public float firstWithin = 3f;
    }

    /// <summary>
    /// The wordless chatter (#144): given the groups, the clock, a seed, the zoom and whether a real line is up, who shows
    /// which mark. One speaker per group every 3 to 6 s, at most 4 alive in the box (1 zoomed out), never over a sleeper, a
    /// real line always wins (live marks fade, no new ones), the same quiet cases as the world bubbles (card open, moment,
    /// clean mode) silence it. Its rate is separate from <see cref="WorldBubbles"/>' 20 s gap. Deterministic for a seed:
    /// the order is fixed, the dice are a hash of (seed, group, turn). Holds only its own clock state; reads nothing else.
    /// </summary>
    public sealed class ChatterPlan
    {
        public readonly ChatterRules rules;
        readonly Dictionary<int, double> due = new Dictionary<int, double>();
        readonly Dictionary<int, int> turn = new Dictionary<int, int>();
        readonly Dictionary<int, int> lastSpeaker = new Dictionary<int, int>();
        readonly List<ChatterShow> live = new List<ChatterShow>();

        public ChatterPlan(ChatterRules rules = null) { this.rules = rules ?? new ChatterRules(); }

        /// <summary>Marks alive now.</summary>
        public IReadOnlyList<ChatterShow> Live => live;

        public void Reset() { due.Clear(); turn.Clear(); lastSpeaker.Clear(); live.Clear(); }

        bool hurry;

        /// <summary>The debug panel: every group is due now, new ones too, for one tick.</summary>
        public void Hurry(double now)
        {
            foreach (var k in due.Keys.ToList()) due[k] = now;
            hurry = true;
        }

        /// <summary>Babble volume factor 0..1 for a zoom (0 = whole box, 1 = closest): off at default zoom, full when close.</summary>
        public float BabbleVolume(float zoom01)
        {
            float t = Clamp01((zoom01 - rules.babbleFrom) / Math.Max(1e-4f, rules.babbleFull - rules.babbleFrom));
            return t * t * (3f - 2f * t);
        }

        /// <summary>
        /// One step. <paramref name="quiet"/>: a card is open, a moment plays or clean mode is on. <paramref name="realLineUp"/>:
        /// a real world bubble or echo line is showing. <paramref name="zoom01"/>: 0 = the whole box, 1 = closest.
        /// A knock's reaction mark (<see cref="React"/>) is kept through a real line, dropped by quiet or Off.
        /// </summary>
        public ChatterTick Tick(double now, IList<ChatterGroup> groups, int seed, float zoom01, bool realLineUp, bool quiet = false, ChatterStyle style = ChatterStyle.MarksBabble)
        {
            var res = new ChatterTick();
            live.RemoveAll(s => s.endAt <= now);
            bool shut = quiet || style == ChatterStyle.Off || style == ChatterStyle.GestureBabble; // gesture: no marks (its motion is the view's)
            if (shut || realLineUp)
            {
                foreach (var s in live.Where(s => shut || !s.reaction).ToList()) { res.fade.Add(s); live.Remove(s); }
                due.Clear(); // afterwards: a fresh, staggered start, not a burst the moment it ends
                return res;
            }
            int cap = zoom01 < rules.zoomedOut ? rules.maxLiveZoomedOut : rules.maxLive;
            float vol = style == ChatterStyle.MarksBabble ? BabbleVolume(zoom01) : 0f;
            while (live.Count(s => !s.reaction) > cap) // zoomed out with more up: the oldest goes
            {
                var old = live.Where(s => !s.reaction).OrderBy(s => s.startAt).First();
                res.fade.Add(old); live.Remove(old);
            }
            if (groups == null) return res;
            foreach (var g in groups.OrderBy(g => g.id))
            {
                var awake = g.members.Where(m => !m.asleep).OrderBy(m => m.id).ToList();
                if (awake.Count < 2) { due.Remove(g.id); continue; }
                if (!due.TryGetValue(g.id, out double d))
                {
                    due[g.id] = hurry ? now : now + Roll(seed, g.id, -1, 1) * rules.firstWithin;
                    if (!hurry) continue;
                    d = now;
                }
                if (now < d) continue;
                if (live.Count(s => !s.reaction) >= cap) continue; // wait for room; stays due
                if (live.Any(s => s.groupId == g.id)) continue;
                int n = turn.TryGetValue(g.id, out int t0) ? t0 : 0;
                turn[g.id] = n + 1;
                due[g.id] = now + rules.minGap + Roll(seed, g.id, n, 2) * (rules.maxGap - rules.minGap);
                var speaker = PickSpeaker(awake, g.id, seed, n);
                var others = awake.Where(m => m != speaker).ToList();
                var listener = others[(int)(Roll(seed, g.id, n, 4) * others.Count) % others.Count];
                lastSpeaker[g.id] = speaker.id;
                var mark = MarkFor(speaker.toward.TryGetValue(listener.id, out var r) ? r : Rapport.Idle, g.nearFire, Roll(seed, g.id, n, 3));
                var show = new ChatterShow
                {
                    groupId = g.id, speakerId = speaker.id, listenerId = listener.id, voice = speaker.voice, mark = mark,
                    startAt = now, endAt = now + (mark == ChatterMark.Bang ? rules.bangSeconds : rules.markSeconds), babble = vol,
                };
                live.Add(show); res.show.Add(show);
            }
            hurry = false;
            return res;
        }

        /// <summary>
        /// A reaction's mark (the knock's <c>!</c>, see <see cref="KnockReaction"/>): skips the groups' turn-taking and the live
        /// cap. Null when the style is off or quiet. Held until <paramref name="seconds"/> pass.
        /// </summary>
        public ChatterShow React(double now, int personId, int voice, ChatterMark mark, ChatterStyle style, bool quiet, float seconds)
        {
            if (quiet || style == ChatterStyle.Off || style == ChatterStyle.GestureBabble) return null;
            var s = new ChatterShow { speakerId = personId, voice = voice, mark = mark, startAt = now, endAt = now + seconds, reaction = true, babble = 0f };
            live.Add(s);
            return s;
        }

        /// <summary>Mood to mark: friends ♥ or ♪ (♪ likelier at the fire), partners ♥, family ♪ or ♥, rivals !, unsure ?, idle …</summary>
        public static ChatterMark MarkFor(Rapport r, bool nearFire, float roll)
        {
            switch (r)
            {
                case Rapport.Partner: return roll < 0.75f ? ChatterMark.Heart : ChatterMark.Note;
                case Rapport.Friend: return roll < (nearFire ? 0.4f : 0.6f) ? ChatterMark.Heart : ChatterMark.Note;
                case Rapport.Family: return roll < (nearFire ? 0.7f : 0.5f) ? ChatterMark.Note : ChatterMark.Heart;
                case Rapport.Rival: return roll < 0.8f ? ChatterMark.Bang : ChatterMark.Dots;
                case Rapport.Unsure: return roll < 0.8f ? ChatterMark.Question : ChatterMark.Dots;
                default: return roll < (nearFire ? 0.25f : 0.1f) ? ChatterMark.Note : ChatterMark.Dots;
            }
        }

        ChatterPerson PickSpeaker(List<ChatterPerson> awake, int groupId, int seed, int n)
        {
            var pool = awake;
            if (awake.Count > 2 && lastSpeaker.TryGetValue(groupId, out int last)) pool = awake.Where(m => m.id != last).ToList(); // take turns
            return pool[(int)(Roll(seed, groupId, n, 0) * pool.Count) % pool.Count];
        }

        static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;

        /// <summary>A deterministic 0..1 from (seed, a, b, salt). Integer hash: the same on every runtime.</summary>
        public static float Roll(int seed, int a, int b, int salt)
        {
            unchecked
            {
                uint h = (uint)seed * 2654435761u;
                h ^= (uint)a * 40503u + 0x9E3779B9u; h *= 2246822519u;
                h ^= (uint)b * 3266489917u + (uint)salt * 668265263u; h ^= h >> 15; h *= 2246822519u; h ^= h >> 13;
                return (h & 0xFFFFFF) / (float)0x1000000;
            }
        }
    }
}
