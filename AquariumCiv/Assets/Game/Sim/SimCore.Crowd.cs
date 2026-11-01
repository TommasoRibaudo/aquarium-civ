using System;
using System.Linq;

namespace AquariumCiv.Sim
{
    /// <summary>What a crowd person's small card shows (#148): no stories, family or pin. Built on demand; only the name is saved.</summary>
    public sealed class CrowdCard
    {
        public int index;               // the crowd index (the planner's PersonRef.Crowd)
        public string name;             // first name, stable for this person
        public string stage;            // "baby", "child", "adult", "elder" (as PersonCard.stage)
        public string job = "";         // the planner's job as given ("" = no job of their own)
        public string jobLine;          // what the card prints: "cook", "helper", "child", "elder"
        public string doing;            // "Ivy is hoeing at the field."
    }

    /// <summary>
    /// The crowd is a count in the sim (population minus the living notables) and a person by index in the view: an index
    /// keeps its doll, its stage and now its first name (life-in-the-box rule 13). The count moves at the tail, so the last
    /// index is who leaves and who comes back.
    /// <para><b>Names.</b> <see cref="SimState.crowdNames"/> holds them by index, saved, read from a table walked in an order
    /// set by the seed. No dice: nothing here shifts a stream. An older save has none yet and fills from the seed the same way.</para>
    /// <para><b>Stepping out of the crowd.</b> The person who rises is the last index, whose doll is the one that goes; the
    /// notable takes that first name and a new name waits at the index. A newcomer comes from outside and keeps their own.</para>
    /// </summary>
    public partial class SimCore
    {
        /// <summary>People in the crowd now (the planner's count: indices 0 to this, exclusive).</summary>
        public int CrowdCount => Math.Max(0, State.population - State.notables.Count(n => n.alive));

        /// <summary>This crowd person's first name, or null if nobody stands at that index now.</summary>
        public string CrowdName(int index)
        {
            if (index < 0 || index >= CrowdCount) return null;
            EnsureCrowdNames();
            return State.crowdNames[index];
        }

        /// <summary>The small card for a crowd person, or null if nobody stands at that index now. The sim doesn't know the
        /// day: <paramref name="job"/> and <paramref name="stage"/> are the planner's <c>JobOf</c> and <c>StageOf</c>
        /// (LifeStage as text), <paramref name="doing"/> its job report (<c>Describe</c>), whose subject becomes the name.</summary>
        public CrowdCard CrowdCardOf(int index, string job, string stage, string doing)
        {
            string name = CrowdName(index);
            if (name == null) return null;
            string st = stage == "Baby" ? "baby" : stage == "Kid" ? "child" : stage == "Elder" ? "elder" : "adult";
            job ??= "";
            return new CrowdCard
            {
                index = index, name = name, stage = st, job = job,
                jobLine = st != "adult" ? st : job.Length > 0 ? job : "helper",
                doing = CrowdDoingLine(name, doing),
            };
        }

        static string CrowdDoingLine(string name, string report)
        {
            int verb = string.IsNullOrEmpty(report) ? -1 : report.IndexOf(" is ", StringComparison.Ordinal);
            return verb < 0 ? $"{name} is out and about." : name + report.Substring(verb);
        }

        /// <summary>Everyone counted in the population has a name. Keeps to the population, not today's crowd, so names come
        /// out the same whenever they are first asked for.</summary>
        void EnsureCrowdNames()
        {
            var names = State.crowdNames;
            while (names.Count < State.population) names.Add(NextCrowdName());
        }

        /// <summary>The next name of this box's walk through the table: every name once before any repeats, and no name a
        /// notable or the crowd is using while another is free. A hash of the seed, no dice.</summary>
        string NextCrowdName()
        {
            var s = State;
            var table = NameData.CrowdGiven;
            uint h = (uint)s.seed * 0x9E3779B1u ^ 0x7C3A55EDu;
            h ^= h >> 16; h *= 0x7FEB352Du; h ^= h >> 15;
            int start = (int)(h % (uint)table.Length);
            int stride = 1 + (int)((h >> 8) % (uint)(table.Length - 1));
            while (Gcd(stride, table.Length) != 1) stride++;
            string At(int k) => table[(int)((start + (long)k * stride) % table.Length)];
            for (int tries = 0; tries < table.Length; tries++)
            {
                string name = At(s.crowdNameNext++);
                if (!s.crowdNames.Contains(name) && !s.notables.Any(n => n.name == name)) return name;
            }
            return At(s.crowdNameNext++); // every name in the table is in use: a repeat, on purpose
        }

        static int Gcd(int a, int b) => b == 0 ? a : Gcd(b, a % b);

        /// <summary>A notable just stepped out of the crowd (and is in the list): they take the first name of the crowd's last
        /// index, and a new person waits there. With nobody in the crowd, or if a notable already bears the name, they keep
        /// the name they were drawn.</summary>
        void TakeCrowdName(Notable n)
        {
            var s = State;
            int index = s.population - s.notables.Count(o => o.alive); // n counts now, so this was the last index
            if (index < 0) return;
            EnsureCrowdNames();
            string name = s.crowdNames[index];
            if (s.notables.Any(o => o != n && o.name == name && (o.alive || o.family == n.family))) return;
            n.name = name;
            s.crowdNames[index] = NextCrowdName();
        }

        // ---------- debug (debug panel: a crowd person rises now) ----------
        /// <summary>Debug: the last crowd person steps out of the crowd now, or null if the era has no room for a notable.</summary>
        public Notable ForceCrowdRise() => CrowdCount > 0 ? FromTheCrowd() : null;
    }
}
