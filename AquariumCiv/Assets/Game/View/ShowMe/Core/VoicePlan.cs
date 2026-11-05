using System;
using System.Collections.Generic;

namespace AquariumCiv.View.ShowMe
{
    /// <summary>How near the camera is, for the voices (#316): far is the whole box, near is a group filling the screen.</summary>
    public enum VoiceBand { Far, Mid, Near }

    /// <summary>Every number of the voice model in one place (#316). Zoom is 0 = the whole box .. 1 = closest.</summary>
    public sealed class VoiceRules
    {
        /// <summary>Zoom below <c>farUntil</c> is the far band, below <c>midUntil</c> the mid band, above it the near band.</summary>
        public float farUntil = 0.25f, midUntil = 0.55f;
        /// <summary>Real seconds between two lone voices (far: a shout now and then; mid: more often).</summary>
        public float farGapMin = 20f, farGapMax = 40f, midGapMin = 9f, midGapMax = 18f;
        /// <summary>Voice level (0..1 of the cue) in each band; it eases between them. Near rises to full over <see cref="nearRamp"/> of zoom.</summary>
        public float farVolume = 0.5f, midVolume = 0.6f, nearVolume = 1f, nearRamp = 0.25f;
        /// <summary>Below this zoom a voice is the muffled (low-passed) take.</summary>
        public float muffledBelow = 0.4f;
        /// <summary>Real seconds a group rests after an exchange, before its next one.</summary>
        public float exchangeGapMin = 8f, exchangeGapMax = 16f;
        /// <summary>A turn lasts about this long, and the next speaker comes in after a pause of this much.</summary>
        public float turnSeconds = 1.1f, replyLatencyMin = 0.35f, replyLatencyMax = 1.1f;
        /// <summary>Exchanges that may overlap in the box.</summary>
        public int maxExchangesAtOnce = 2;
        /// <summary>± semitones of each person's own pitch; kids sit this much higher on top of their (higher) voice.</summary>
        public float personSemitones = 1.6f, kidSemitones = 1.5f;
        /// <summary>At night the voices hush: this share of the volume, and only soft kinds.</summary>
        public float nightHush = 0.5f, nightFrom = 21f, nightTo = 5f;
        /// <summary>No clip among this many of the last played ones repeats (when the pool is bigger).</summary>
        public int noRepeatWindow = 3;
    }

    /// <summary>One person who may speak.</summary>
    public struct VoiceMember
    {
        public int id, voice;
        public bool kid;
    }

    /// <summary>A group about to talk (the view reads these twice a second).</summary>
    public sealed class VoiceGroup
    {
        public int id;
        public bool nearFire, working;
        public List<VoiceMember> members = new List<VoiceMember>();
    }

    /// <summary>One voice to play: who, which babble clip, how loud, how shifted, how late, muffled or not, and the mark over the speaker.</summary>
    public struct VoiceTurn
    {
        public int speakerId, voice, clip;
        public double delay;
        public float volume, semitones;
        public bool muffled;
        public ChatterMark mark;
    }

    /// <summary>
    /// The voices by distance (#316; the user, 2026-11-04: far away only a muffled voice every once in a while, closer more and more
    /// conversations, varied). Pure decisions, no clock of its own: the view passes the time, the zoom and who is there.
    /// <list type="bullet">
    /// <item>Far: one lone voice (a shout, a call, a laugh) every 20 to 40 s, quiet and muffled, none at night.</item>
    /// <item>Mid: a lone voice every 9 to 18 s, or a group's single line; muffled in the lower half.</item>
    /// <item>Near: a group's turn becomes an exchange, 2 or 3 people taking turns with pauses, then the group rests 8 to 16 s.</item>
    /// <item>Variety: which clip fits the mark, the fire, the work, the hour and the age; never one of the last few clips again;
    /// each person's own pitch.</item>
    /// </list>
    /// Deterministic for a seed (integer-hash dice, like <see cref="ChatterPlan"/>).
    /// </summary>
    public sealed class VoicePlan
    {
        public readonly VoiceRules rules;
        readonly int seed;
        int counter;
        double nextLone = -1, nextMid = -1;
        int lastLoneSpeaker = int.MinValue;
        readonly Dictionary<int, double> nextExchange = new Dictionary<int, double>();
        readonly List<double> exchangeEnds = new List<double>();
        readonly List<int> recent = new List<int>();
        readonly Dictionary<int, int> lastBySpeaker = new Dictionary<int, int>();

        /// <summary>Voices decided so far, by band (tests, debug).</summary>
        public int Lone { get; private set; }
        public int Exchanges { get; private set; }
        public int Singles { get; private set; }

        // clip pools (ChatterBabble: 0/5 murmur, 1 notes, 2 soft pair, 3 sharp, 4 rising, 6 laugh, 7 call, 8 shout, 9 hum, 10 giggle, 11 chat)
        static readonly int[] FarDay = { ChatterBabble.Shout, ChatterBabble.Call, ChatterBabble.Laugh };
        static readonly int[] FarKid = { ChatterBabble.Giggle, ChatterBabble.Shout, ChatterBabble.Call };
        static readonly int[] NightPool = { ChatterBabble.Hum, 2, 0 };

        public VoicePlan(int seed = 316, VoiceRules rules = null) { this.seed = seed; this.rules = rules ?? new VoiceRules(); }

        public void Reset()
        {
            nextLone = nextMid = -1; lastLoneSpeaker = int.MinValue; counter = 0;
            nextExchange.Clear(); exchangeEnds.Clear(); recent.Clear(); lastBySpeaker.Clear();
            Lone = Exchanges = Singles = 0;
        }

        // ------------------------------------------------------------------ the band

        public VoiceBand BandOf(float zoom01) => zoom01 < rules.farUntil ? VoiceBand.Far : zoom01 < rules.midUntil ? VoiceBand.Mid : VoiceBand.Near;

        /// <summary>The volume factor 0..1 at a zoom: eased between the bands.</summary>
        public float VolumeAt(float zoom01)
        {
            if (zoom01 <= rules.farUntil) return rules.farVolume;
            if (zoom01 <= rules.midUntil) return Lerp(rules.farVolume, rules.midVolume, Smooth((zoom01 - rules.farUntil) / Math.Max(1e-4f, rules.midUntil - rules.farUntil)));
            return Lerp(rules.midVolume, rules.nearVolume, Smooth((zoom01 - rules.midUntil) / Math.Max(1e-4f, rules.nearRamp)));
        }

        public bool Muffled(float zoom01) => zoom01 < rules.muffledBelow;

        public bool IsNight(float hour) => hour >= rules.nightFrom || hour < rules.nightTo;

        // ------------------------------------------------------------------ a lone voice (far and mid)

        /// <summary>
        /// Time-driven: a lone voice from one of <paramref name="onView"/> (people on screen who are awake and not in a group's turn).
        /// False in the near band (the groups talk there), at night, or when nobody is on view (it looks again in 3 s).
        /// </summary>
        public bool TickLone(double now, float zoom01, float hour, IList<VoiceMember> onView, out VoiceTurn turn)
        {
            turn = default;
            var band = BandOf(zoom01);
            if (band == VoiceBand.Near || IsNight(hour)) { nextLone = -1; return false; }
            float gMin = band == VoiceBand.Far ? rules.farGapMin : rules.midGapMin, gMax = band == VoiceBand.Far ? rules.farGapMax : rules.midGapMax;
            if (nextLone < 0) { nextLone = now + Lerp(gMin, gMax, Dice(1)) * 0.6; return false; }
            if (now < nextLone) return false;
            if (onView == null || onView.Count == 0) { nextLone = now + 3; return false; }
            nextLone = now + Lerp(gMin, gMax, Dice(2));
            int i = (int)(Dice(3) * onView.Count) % onView.Count;
            if (onView.Count > 1 && onView[i].id == lastLoneSpeaker) i = (i + 1) % onView.Count; // a different person than last time
            var who = onView[i];
            lastLoneSpeaker = who.id;
            var pool = who.kid ? FarKid : FarDay;
            int clip = Pick(pool, who.id, Dice(4));
            turn = Make(who, clip, 0, zoom01, MarkForClip(clip), false);
            Lone++;
            return true;
        }

        /// <summary>A lone voice right now, whatever the clock and band (the debug cheat).</summary>
        public VoiceTurn LoneNow(float zoom01, VoiceMember who)
        {
            int clip = Pick(who.kid ? FarKid : FarDay, who.id, Dice(5));
            Lone++;
            return Make(who, clip, 0, zoom01, MarkForClip(clip), false);
        }

        /// <summary>The mark that goes with a lone voice: a shout is <c>!</c>, a laugh or giggle ♪, a call or anything else …</summary>
        public static ChatterMark MarkForClip(int clip) =>
            clip == ChatterBabble.Shout ? ChatterMark.Bang : clip == ChatterBabble.Laugh || clip == ChatterBabble.Giggle ? ChatterMark.Note : ChatterMark.Dots;

        // ------------------------------------------------------------------ a group's turn (mid: one line, near: an exchange)

        /// <summary>
        /// A group's plan turn started (<paramref name="speakerId"/> speaks, <paramref name="mark"/> is over them). Returns the voices
        /// to play: none (far, or the group is resting, or too many exchanges), one line (mid) or 2 to 3 turns with pauses (near).
        /// The first turn is the speaker's, at delay 0; each reply carries its own mark. <paramref name="force"/> ignores the rests (debug).
        /// </summary>
        public List<VoiceTurn> OnTurn(double now, float zoom01, float hour, VoiceGroup g, int speakerId, int listenerId, ChatterMark mark, bool force = false)
        {
            var res = new List<VoiceTurn>();
            var band = BandOf(zoom01);
            if (band == VoiceBand.Far && !force) return res;
            var speaker = Find(g, speakerId);
            if (speaker.id == int.MinValue || g.members.Count < 2) return res;
            bool night = IsNight(hour);
            if (band != VoiceBand.Near)
            {
                if (!force && nextMid < 0) { nextMid = now + Lerp(rules.midGapMin, rules.midGapMax, Dice(5)) * 0.6f; return res; } // the first line waits a while
                if (!force && now < nextMid) return res;
                nextMid = now + Lerp(rules.midGapMin, rules.midGapMax, Dice(6));
                int clip = Pick(PoolFor(mark, g, speaker.kid, night), speaker.id, Dice(7));
                res.Add(Make(speaker, clip, 0, zoom01, mark, night));
                Singles++;
                return res;
            }
            exchangeEnds.RemoveAll(e => e <= now);
            if (!force)
            {
                if (nextExchange.TryGetValue(g.id, out double rest) && now < rest) return res;
                if (exchangeEnds.Count >= rules.maxExchangesAtOnce) return res;
            }
            int turns = g.members.Count >= 3 && Dice(8) < 0.5f ? 3 : 2;
            var listener = Find(g, listenerId);
            if (listener.id == int.MinValue || listener.id == speaker.id) listener = Other(g, speaker.id, -1);
            VoiceMember third = turns == 3 ? Other(g, speaker.id, listener.id) : default;
            if (turns == 3 && third.id == int.MinValue) turns = 2;
            var order = new[] { speaker, listener, turns == 3 ? third : speaker };
            double t = 0;
            ChatterMark m = mark;
            for (int i = 0; i < turns; i++)
            {
                var who = order[i];
                int[] pool = i == 0 ? PoolFor(m, g, who.kid, night) : PoolFor(m, g, who.kid, night, true);
                int clip = Pick(pool, who.id, Dice(10 + i));
                res.Add(Make(who, clip, t, zoom01, m, night));
                t += rules.turnSeconds * (who.kid ? 0.8f : 1f) + Lerp(rules.replyLatencyMin, rules.replyLatencyMax, Dice(20 + i));
                m = ReplyMark(m, Dice(30 + i));
            }
            exchangeEnds.Add(now + t);
            nextExchange[g.id] = now + t + Lerp(rules.exchangeGapMin, rules.exchangeGapMax, Dice(40));
            Exchanges++;
            return res;
        }

        static VoiceMember Find(VoiceGroup g, int id)
        {
            foreach (var m in g.members) if (m.id == id) return m;
            return new VoiceMember { id = int.MinValue };
        }

        VoiceMember Other(VoiceGroup g, int not1, int not2)
        {
            var pool = new List<VoiceMember>();
            foreach (var m in g.members) if (m.id != not1 && m.id != not2) pool.Add(m);
            if (pool.Count == 0) return new VoiceMember { id = int.MinValue };
            return pool[(int)(Dice(50) * pool.Count) % pool.Count];
        }

        /// <summary>The mark a reply carries: a rival's ! is met with ! or ?, a ? with …, warmth with warmth.</summary>
        public static ChatterMark ReplyMark(ChatterMark m, float roll)
        {
            switch (m)
            {
                case ChatterMark.Bang: return roll < 0.5f ? ChatterMark.Bang : ChatterMark.Question;
                case ChatterMark.Question: return ChatterMark.Dots;
                case ChatterMark.Heart: return roll < 0.6f ? ChatterMark.Heart : ChatterMark.Note;
                case ChatterMark.Note: return roll < 0.5f ? ChatterMark.Note : ChatterMark.Heart;
                default: return ChatterMark.Dots;
            }
        }

        /// <summary>
        /// The clips that fit what is going on: at night only a hum, a soft pair or a murmur; a rival's ! is sharp or a call; a ? rises;
        /// ♥ is a soft pair or a laugh (a child's giggle); ♪ held notes or a hum (a laugh at the fire); … murmurs, a call out at work.
        /// A reply (<paramref name="reply"/>) is gentler: no shouts, no sharp ones.
        /// </summary>
        public static int[] PoolFor(ChatterMark mark, VoiceGroup g, bool kid, bool night, bool reply = false)
        {
            if (night) return NightPool;
            int laugh = kid ? ChatterBabble.Giggle : ChatterBabble.Laugh;
            switch (mark)
            {
                case ChatterMark.Bang: return reply ? new[] { 4, 0 } : new[] { 3, ChatterBabble.Call };
                case ChatterMark.Question: return new[] { 4, ChatterBabble.Chat };
                case ChatterMark.Heart: return g != null && g.nearFire ? new[] { laugh, 2, ChatterBabble.Laugh } : new[] { 2, laugh };
                case ChatterMark.Note: return g != null && g.nearFire ? new[] { 1, ChatterBabble.Hum, laugh } : new[] { 1, ChatterBabble.Hum };
                default:
                    if (!reply && g != null && g.working) return new[] { ChatterBabble.Call, 0, 5, ChatterBabble.Chat };
                    return new[] { 0, 5, ChatterBabble.Chat };
            }
        }

        // ------------------------------------------------------------------ the dice and the picker

        /// <summary>Picks from <paramref name="pool"/> by <paramref name="roll"/>, skipping the last few clips played and this speaker's last, when something else is left.</summary>
        public int Pick(int[] pool, int speakerId, float roll)
        {
            var open = new List<int>();
            int mine = lastBySpeaker.TryGetValue(speakerId, out int l) ? l : -1;
            foreach (int c in pool) if (!recent.Contains(c) && c != mine) open.Add(c);
            if (open.Count == 0) foreach (int c in pool) if (c != mine && c != (recent.Count > 0 ? recent[recent.Count - 1] : -1)) open.Add(c);
            if (open.Count == 0) open.AddRange(pool);
            int pick = open[(int)(roll * open.Count) % open.Count];
            recent.Add(pick);
            while (recent.Count > Math.Max(1, rules.noRepeatWindow)) recent.RemoveAt(0);
            lastBySpeaker[speakerId] = pick;
            return pick;
        }

        /// <summary>One person's own pitch shift in semitones: steady for the same person, a little higher for a child.</summary>
        public float SemitonesOf(int personId, bool kid)
            => (ChatterPlan.Roll(seed, personId, 0, 11) - 0.5f) * 2f * rules.personSemitones + (kid ? rules.kidSemitones : 0f);

        /// <summary>How loud a kind is against the others (a shout carries, a hum does not).</summary>
        public static float KindGain(int clip)
        {
            switch (clip)
            {
                case ChatterBabble.Shout: return 1f;
                case ChatterBabble.Call: return 0.9f;
                case ChatterBabble.Laugh: return 0.85f;
                case ChatterBabble.Hum: return 0.6f;
                case 2: return 0.7f;
                default: return 0.8f;
            }
        }

        VoiceTurn Make(VoiceMember who, int clip, double delay, float zoom01, ChatterMark mark, bool night)
        {
            return new VoiceTurn
            {
                speakerId = who.id, voice = who.voice, clip = clip, delay = delay, mark = mark,
                muffled = Muffled(zoom01),
                volume = VolumeAt(zoom01) * KindGain(clip) * (night ? rules.nightHush : 1f) * Lerp(0.85f, 1f, Dice(60)),
                semitones = SemitonesOf(who.id, who.kid),
            };
        }

        float Dice(int salt) => ChatterPlan.Roll(seed, counter++, salt, 316);

        static float Lerp(float a, float b, float t) => a + (b - a) * t;
        static float Smooth(float t) { t = t < 0f ? 0f : t > 1f ? 1f : t; return t * t * (3f - 2f * t); }
    }
}
