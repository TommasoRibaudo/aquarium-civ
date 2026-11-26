using System.Linq;

namespace AquariumCiv.Sim
{
    /// <summary>#370: the staged moments' cheats on the sim side (the queue is C2's, SimCore.MomentQueue.cs). No dice.</summary>
    public partial class SimCore
    {
        /// <summary>A cheat moment plays before anything else waiting.</summary>
        public const float DebugMomentPriority = 100f;

        /// <summary>
        /// Debug (#370): one moment of <paramref name="kind"/> waits now, at the top of the queue, about the box's own people: the
        /// last real line of that kind whose people still live, else a cheat line (a real chronicle line). A newborn needs a baby:
        /// one is born if the box has none under 4. Null when nobody can hold it (no couple, no leader, no adult).
        /// </summary>
        public HeldMoment DebugQueueMoment(MomentKind kind)
        {
            if (!Config.moments) return null;
            var adults = State.notables.Where(n => n.alive && n.age >= MarryAge).OrderBy(n => n.id).ToList();
            ChronicleEntry Line(string k, string text, int x, int y)
            {
                var e = new ChronicleEntry { id = State.NextId(), month = State.month, kind = k, text = text, salience = 0.6f, notableId = x, otherNotableId = y };
                e.text = LandWords(e.text); // #656/#677
                State.chronicle.Add(e);
                Chronicled?.Invoke(e);
                return e;
            }
            HeldMoment made = null;
            try
            {
                momentNoTrim = true;
                switch (kind)
                {
                    case MomentKind.Wedding:
                    {
                        var w = State.chronicle.LastOrDefault(e => e.kind == "wedding" && Alive(e.notableId) != null && Alive(e.otherNotableId) != null);
                        if (w != null) { made = QueueMoment(kind, w, w.notableId, w.otherNotableId, true); break; }
                        if (adults.Count < 2) return null;
                        var a = adults[0];
                        var b = adults.Skip(1).FirstOrDefault(x => x.family != a.family) ?? adults[1];
                        made = QueueMoment(kind, Line("wedding", $"{a.FullName} and {b.FullName} promised each other at the fire.", a.id, b.id), a.id, b.id, true);
                        break;
                    }
                    case MomentKind.Newborn:
                    {
                        var baby = State.notables.Where(n => n.alive && n.parentId >= 0 && n.age < 2f && Alive(n.parentId) != null).OrderBy(n => n.age).FirstOrDefault();
                        if (baby == null)
                        {
                            int before = State.moments?.Count ?? 0;
                            baby = DebugBirth(); // its own line queues its moment (MomentFromLine)
                            if (baby == null) return null;
                            made = State.moments != null && State.moments.Count > before ? State.moments.LastOrDefault(m => m.kind == MomentKind.Newborn && m.personA == baby.id) : null;
                            if (made != null) { made.cheat = true; break; }
                        }
                        var line = State.chronicle.LastOrDefault(e => e.kind == "birth" && e.notableId == baby.id)
                            ?? Line("birth", $"{baby.FullName} was held up for the people to see.", baby.id, -1);
                        made = QueueMoment(kind, line, baby.id, baby.parentId, true);
                        break;
                    }
                    case MomentKind.NewLeader:
                    {
                        int leader = SeatOf(LeaderOf(State.era))?.holderId ?? -1;
                        if (Alive(leader) == null) return null; // nobody holds the seat: no new chief to stage
                        made = QueueMoment(kind, Line("role_won", $"{FindNotable(leader).FullName} took the leader's seat at the fire.", leader, -1), leader, -1, true);
                        break;
                    }
                    case MomentKind.Festival:
                    {
                        int a = adults.Count > 0 ? adults[0].id : -1;
                        made = QueueMoment(kind, Line("festival", "The people gathered round the fire for a feast.", a, -1), a, -1, true);
                        break;
                    }
                    default:
                        return null;
                }
            }
            finally { momentNoTrim = false; }
            if (made != null) made.priority = DebugMomentPriority + made.priority;
            return made;
        }

        /// <summary>Tests (#370): a chronicle line about two people, through the usual record (so a held ceremony queues).</summary>
        public ChronicleEntry DebugChronicle(string kind, string text, int a, int b = -1) => Chronicle(kind, text, 0.6f, a, b);

        /// <summary>
        /// Debug (#370): the Keeper arrives now, as after a real absence: the next look starts a new visit, so the top waiting moment
        /// plays as its welcome. Nothing waiting: a wedding is queued first (the people organise it because the Keeper showed up).
        /// Returns what it did.
        /// </summary>
        public string DebugKeeperArrives()
        {
            if (!Config.moments) return "moments off";
            if (Moments.Any(m => m.state == HeldMomentState.Playing)) return "a moment is playing: wait for it to end";
            string queued = "";
            if (!Moments.Any(m => m.state == HeldMomentState.Waiting))
            {
                var m = DebugQueueMoment(MomentKind.Wedding) ?? DebugQueueMoment(MomentKind.Festival);
                queued = m == null ? " (nothing to hold)" : $" (queued a {m.kind.ToString().ToLowerInvariant()})";
            }
            momentAwayHours = MomentRules.VisitAwayHours; // as if away a quarter of an hour: the next look is a new visit
            momentVisitLive = true;
            return "the Keeper arrives" + queued;
        }
    }
}
