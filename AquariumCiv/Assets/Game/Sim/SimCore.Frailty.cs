using System;
using System.Collections.Generic;
using System.Linq;

namespace AquariumCiv.Sim
{
    /// <summary>
    /// Last days (#788, #790; research 23 §3 A and E): an elder within about two years of their (hidden) lifespan is frail. A window,
    /// never a date: it is 1.5 to 2.5 years by the person (a hash of their id, no dice drawn), and frailty rises from a little to
    /// full across it, so the walk slows and the sits lengthen by degrees and never switch on. Nothing here is a number the
    /// player sees: the card says "slowing down", the glance line says the same, the pinned tag says "slowing".
    /// Pure and deterministic; the planner (Life) bends the elder's day with it, the person card and the chronicle word it.
    /// </summary>
    public partial class SimCore
    {
        /// <summary>The middle of the window (game years): "about two years", never an exact date.</summary>
        public const float FrailWindowYears = 2f;
        /// <summary>How far a person's own window may stray from <see cref="FrailWindowYears"/> (either way).</summary>
        public const float FrailWindowSpread = 0.5f;

        /// <summary>The chronicle kind of the glance line.</summary>
        public const string SlowingKind = "slowing";
        /// <summary>The glance lines' own ids, far above the shared counter, the term lines and the relic cards: a line that exists only for
        /// some elders must not shift the ids (and so the dice and the tests) of everything after it.</summary>
        public const int SlowLineIdBase = 1_600_000_000;

        /// <summary>This person's own window in years: about two, 1.5 to 2.5 by a hash of their id (no dice).</summary>
        public static float FrailWindowOf(int notableId)
        {
            uint h = (uint)notableId * 2654435761u;
            h ^= h >> 15; h *= 2246822519u; h ^= h >> 13;
            float u = (h % 1000u) / 1000f; // 0..0.999
            return FrailWindowYears + (u - 0.5f) * 2f * FrailWindowSpread;
        }

        /// <summary>The elder stage, as the card and the planner read it (retired age, or the elder job).</summary>
        public bool IsElderStage(Notable n) => n != null && n.age >= Config.retireAge || n != null && n.job == JobType.Elder;

        /// <summary>0 outside the last days; 0.1 to 1 across them: how frail an elder is. Only a living elder has any: a
        /// grown-up who lives only to 55 to 64 never gets here, and nobody past their lifespan.</summary>
        public float FrailtyOf(Notable n)
        {
            if (n == null || !n.alive || !IsElderStage(n)) return 0f;
            float window = FrailWindowOf(n.id);
            float left = n.lifespan - n.age;
            if (left > window || left <= 0f) return 0f;
            return Math.Max(0.1f, Math.Min(1f, (window - left) / window));
        }

        public float FrailtyOf(int notableId) => FrailtyOf(FindNotable(notableId));

        /// <summary>In their last days.</summary>
        public bool IsFrail(Notable n) => FrailtyOf(n) > 0f;

        /// <summary>The line on the person card for a frail elder ("" otherwise). Soft, no number, no date: the people live in the box.</summary>
        public string FrailLine(Notable n)
        {
            if (!IsFrail(n)) return "";
            bool family = ParentsOf(n).Count > 0 || n.partnerId >= 0 && Alive(n.partnerId) != null || AllChildrenOf(n.id).Any(c => c.alive);
            return family ? "Slowing down these days. The family comes by to sit." : "Slowing down these days. Happiest on the stool by the door.";
        }

        /// <summary>The soft word on the pinned person's tag ("" unless they are in their last days).</summary>
        public string FrailTagWord(Notable n) => IsFrail(n) ? "slowing" : "";

        /// <summary>Their living kin (partner, parents, children, brothers and sisters), ids, for whoever sits beside them.</summary>
        public List<int> KinOf(Notable n)
        {
            var ids = new List<int>();
            if (n == null) return ids;
            void Add(Notable k) { if (k != null && k.alive && k.id != n.id && !ids.Contains(k.id)) ids.Add(k.id); }
            Add(Alive(n.partnerId));
            foreach (int p in ParentsOf(n)) Add(Alive(p));
            foreach (var c in AllChildrenOf(n.id)) Add(c);
            foreach (var sib in SiblingsOf(n)) Add(sib);
            return ids;
        }

        /// <summary>The glance line: said once, the first yearly tick an elder is in their last days. A chronicle line like any other, so it
        /// is a card on return and a line in History; low on the stack unless the Keeper pinned them (the pin lifts it).</summary>
        void NoteSlowing(Notable n)
        {
            if (n.slowingNoted || !IsFrail(n)) return;
            n.slowingNoted = true;
            // B82: several elders entering their last days together never read as a list: within about a game year only the first gets the story line
            // (the rest keep the card text and the pin sign).
            if (State.chronicle.Any(x => x.kind == SlowingKind && State.month - x.month < SimConfig.MonthsPerYear)) return;
            var s = State;
            if (s.slowLineNextId < SlowLineIdBase) s.slowLineNextId = SlowLineIdBase;
            var e = new ChronicleEntry { id = s.slowLineNextId++, month = s.month, kind = SlowingKind, text = $"{n.FullName} is slowing down these days.", salience = 0.4f, notableId = n.id };
            s.chronicle.Add(e);
            Chronicled?.Invoke(e);
        }

        /// <summary>Debug (#788): puts an elder into their last days: the one given, else the pinned one if elderly, else the oldest living.
        /// Anyone younger than retirement is made that old first. Their lifespan is set a little past the middle of their window, so the
        /// last days really run. Returns who, or null.</summary>
        public Notable DebugLastDays(int notableId = -1)
        {
            var n = notableId >= 0 ? Alive(notableId) : null;
            if (n == null) { var p = Pinned; if (p != null && p.alive && IsElderStage(p)) n = p; }
            if (n == null) n = State.notables.Where(x => x.alive).OrderByDescending(x => x.age).ThenBy(x => x.id).FirstOrDefault();
            if (n == null) return null;
            if (!IsElderStage(n)) n.age = Config.retireAge;
            n.lifespan = n.age + FrailWindowOf(n.id) * 0.6f;
            NoteSlowing(n);
            return n;
        }
    }
}
