using System;
using System.Collections.Generic;
using System.Linq;
using AquariumCiv.Sim.Settle;

namespace AquariumCiv.Sim
{
    /// <summary>
    /// #776 (docs/design/animation-specs.md B81, approved by the user 2026-11-26): the founder's statue, the sim side. A year after the person
    /// the Keeper pinned longest (else the box's first notable) has died, in a grown village, the people raise a small stone figure of them on
    /// the green: the sim only writes the line (a <see cref="StoryEvent.FounderStatue"/> story event: a held Story moment while watched, a card
    /// otherwise) and sets <see cref="SimState.statueFor"/> and <see cref="SimState.statueMonth"/>; the raising is the view's scene. Once a box,
    /// no Keeper choice, no dice. The spot is recomputed from the settlement every load (<see cref="FindStatueSite"/>).
    /// </summary>
    public partial class SimCore
    {
        /// <summary>The statue stands (or its raising waits for the Keeper): whose it is is <see cref="StatueFor"/>.</summary>
        public bool StatueStands => State.statueFor >= 0;
        public int StatueFor => State.statueFor;
        public Notable StatueNotable => State.statueFor < 0 ? null : FindNotable(State.statueFor);

        /// <summary>What the statue's figure carries (B81.1): the person's leader term, job and trait.</summary>
        public StatueRules.Tell StatueTell(Notable who) =>
            who == null ? StatueRules.Tell.Nothing
            : StatueRules.TellOf(who.job, who.trait, TermsOf(who.id).Any(t => t.role == RoleType.Chief || t.role == RoleType.Headman));

        /// <summary>Who the statue would be for now: the most pinned (a day or more), else the first notable. Alive or not.</summary>
        public Notable StatueCandidate => StatueRules.Who(State.notables, PinnedHours, Config.pinLegendHours);

        ChronicleEntry TryFounderStatue(bool force)
        {
            if (StoryHappened(StoryEvent.FounderStatue) || State.statueFor >= 0) return null;
            var who = StatueCandidate;
            if (who == null) return null;
            if (force ? who.alive : !StatueRules.Ready(who, State.month, State.era, State.population, CalendarMonth, false)) return null;
            State.statueFor = who.id; State.statueMonth = State.month; statueSiteKnown = false;
            return StoryLine(StoryEvent.FounderStatue, $"{who.name} is made of stone now, on the green.", 0.8f, who.id, -1);
        }

        /// <summary>Where it stands on the green (box x, z) and the way it faces (yaw degrees); false with no settlement or no open ground.
        /// Worked out once from the settlement the first time it is asked, then kept for the session (a reload works it out again the same way).</summary>
        public bool FindStatueSite(out float x, out float z, out float yaw)
        {
            x = statueX; z = statueZ; yaw = statueYaw;
            if (statueSiteKnown) return statueSiteFound;
            var w = Settlement;
            if (w == null || w.Map == null || !Config.settle) return false;
            var keep = new List<(float x, float z)>();
            foreach (var l in w.Lots)
            {
                if (l.retired) continue;
                if (l.kind == LotKind.Well) keep.Add((l.x, l.z));
                if (l.door >= 0 || l.doorX != 0f || l.doorZ != 0f) keep.Add((l.doorX, l.doorZ));
            }
            w.Fire(out float fx, out float fz, out _);
            keep.Add((fx, fz));
            foreach (var p in ConsequenceProps) if (p.placed) keep.Add((p.x, p.z)); // the name stone, the Keeper's post and the rest of the Keeper's things
            statueSiteFound = StatueSpot.Find(w.Map, keep, out statueX, out statueZ);
            statueYaw = statueSiteFound ? StatueSpot.FaceYaw(statueX, statueZ, w.Map.greenX, w.Map.greenZ) : 0f;
            statueSiteKnown = true;
            x = statueX; z = statueZ; yaw = statueYaw;
            return statueSiteFound;
        }

        bool statueSiteKnown, statueSiteFound; float statueX, statueZ, statueYaw;

        /// <summary>B81.c: today is the day of the person's death, a year or more on (one view day is a sim month: the same month of the year).</summary>
        public bool StatueAnniversary
        {
            get
            {
                var who = StatueNotable;
                if (who == null || who.alive || who.diedMonth < 0) return false;
                long since = State.month - who.diedMonth;
                return since >= StatueRules.WaitMonths && since % SimConfig.MonthsPerYear == 0;
            }
        }

        // ---------- debug (SliceDebugPanel's "Statue (#776)" block) ----------

        /// <summary>"Statue: raise now": the person (the most pinned, else the first notable); if they live, they have died a year back. The line, or null and why.</summary>
        public ChronicleEntry DebugFounderStatue(out string why)
        {
            why = "";
            if (State.era == Era.Camp) { why = "the statue is for a village or a town ('Jump: a village box' first)"; return null; }
            State.statueFor = -1; State.statueMonth = -1; statueSiteKnown = false;
            var who = StatueCandidate;
            if (who == null) { why = "nobody in the box"; return null; }
            if (who.alive) { who.alive = false; who.diedMonth = State.month - StatueRules.WaitMonths; }
            var e = TryFounderStatue(true);
            if (e == null) why = "the statue wasn't made";
            return e;
        }

        /// <summary>"Statue: stand it": the statue stands now with no line and no scene (the setup for the cheats of its uses). The person, or null and why.</summary>
        public Notable DebugStatueStand(out string why)
        {
            why = "";
            if (State.statueFor >= 0) return StatueNotable;
            var who = StatueCandidate;
            if (who == null) { why = "nobody in the box"; return null; }
            if (who.alive) { who.alive = false; who.diedMonth = State.month - StatueRules.WaitMonths; }
            State.statueFor = who.id; State.statueMonth = State.month; statueSiteKnown = false;
            State.storyFlags.Add("story:" + StoryEvent.FounderStatue);
            return who;
        }

        /// <summary>"Founder: die now": the candidate dies this month (so "Statue: skip a year" and the rule can follow).</summary>
        public Notable DebugFounderDie()
        {
            var who = StatueCandidate;
            if (who != null && who.alive) { who.alive = false; who.diedMonth = State.month; }
            return who;
        }

        /// <summary>"Statue: skip a year": the death a year further back, so the wait is over. The person, or null.</summary>
        public Notable DebugStatueSkipYear()
        {
            var who = StatueNotable ?? StatueCandidate;
            if (who == null) return null;
            if (who.alive) { who.alive = false; who.diedMonth = State.month; }
            who.diedMonth -= StatueRules.WaitMonths;
            return who;
        }

        /// <summary>"Statue: remove": nobody stands, and the raising can be replayed.</summary>
        public void DebugStatueRemove()
        {
            State.statueFor = -1; State.statueMonth = -1; statueSiteKnown = false;
            State.storyFlags.Remove("story:" + StoryEvent.FounderStatue);
        }

        /// <summary>"Statue: skip to the anniversary": the person's death moved so that this month (the view's day) is the day of it, a year on.</summary>
        public Notable DebugStatueMakeAnniversary()
        {
            var who = StatueNotable ?? StatueCandidate;
            if (who == null) return null;
            who.alive = false;
            who.diedMonth = State.month - SimConfig.MonthsPerYear;
            return who;
        }
    }
}
