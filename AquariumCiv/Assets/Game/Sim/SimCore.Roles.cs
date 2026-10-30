using System;
using System.Collections.Generic;
using System.Linq;

namespace AquariumCiv.Sim
{
    /// <summary>The important roles (docs/design/notable-roles.md §2, decisions.md 2026-10-29). Saved as plain strings on
    /// <see cref="RoleSeat.role"/>. The town leader is an inherited lord (not an elected mayor) and the town's 7th role is
    /// the innkeeper (not the miller).</summary>
    public static class RoleType
    {
        public const string Chief = "chief", Headman = "headman", Lord = "lord";
        public const string Miller = "miller", Priest = "priest", Innkeeper = "innkeeper", Guildmaster = "guildmaster",
            MasterBuilder = "master builder", Merchant = "merchant", Captain = "captain of the watch";
    }

    /// <summary>How a role is won (notable-roles.md §3): each role its own way.</summary>
    public enum RoleRule
    {
        Contest,        // the highest standing wins (chief)
        Election,       // the notables vote; 12-year terms (headman)
        Succession,     // to the apprentice or eldest child (miller, innkeeper)
        Naming,         // the outgoing priest names the next one
        Inheritance,    // in the family: eldest adult child, else the nearest relative, else a contest (lord)
        Appointment,    // by the lord; until the next lord (merchant, captain of the watch)
        GuildElection,  // only the craft notables vote and stand; 12-year terms (guildmaster)
        WorkContest,    // the craft notables each build something; the best wins (master builder)
    }

    /// <summary>One important role and who holds it. A serializable list on <see cref="SimState.roles"/> (no dictionary).</summary>
    [Serializable]
    public class RoleSeat
    {
        public string role;
        public int holderId = -1;
        public long sinceMonth;             // month the holder took it
        public long termEndsMonth = -1;     // elected roles: the next election (-1 = no term)
        public long vacantSinceMonth = -1;  // -1 = held
        public int lastHolderId = -1;       // who held it last (succession, naming and inheritance pass from them)
        public long lastChallengeMonth = -1;
        public bool everHeld;               // a seat never held is filled at once ("whoever the mill was built for")
    }

    /// <summary>
    /// Important roles (#27, notable-roles.md §2–§5): camp 1 (chief), village 3 (headman, miller, priest), town 7 (lord,
    /// priest, innkeeper, guildmaster, master builder, merchant, captain of the watch). A role opens when its seat exists;
    /// one role per notable; traits drive aspiration; rivals contest the same role. Vacancies (death, the chief stepping
    /// down at <see cref="SimConfig.retireAge"/>, an era change) are filled by each role's own rule; contests and elections
    /// come the next spring. The leader line carries over: chief → headman → lord. With no candidate, one notable steps
    /// out of the crowd (within the era band + family room).
    /// Roles are view and story only: every choice comes from a hash of the seed, the people and the month (no dice
    /// stream), and nothing in the economy reads them.
    /// </summary>
    public partial class SimCore
    {
        const int TermYears = 12;           // elected terms [est]
        const int ChallengeYears = 15;      // a rival may challenge the chief this often [est]
        const int SpringSeason = 2;         // contests and elections are held in the first month of spring

        /// <summary>Per era, in fill order (the leader first). needs: building types that must exist ("a|b" = either;
        /// null = always open). seat: where the role works from, the first that exists.</summary>
        static readonly (Era era, string role, RoleRule rule, string needs, string seat)[] RoleTable =
        {
            (Era.Camp, RoleType.Chief, RoleRule.Contest, null, BuildingType.Camp),

            (Era.Village, RoleType.Headman, RoleRule.Election, null, BuildingType.Well + "|" + BuildingType.Camp),
            (Era.Village, RoleType.Miller, RoleRule.Succession, BuildingType.Windmill, BuildingType.Windmill),
            (Era.Village, RoleType.Priest, RoleRule.Naming, BuildingType.Church, BuildingType.Church),

            (Era.Town, RoleType.Lord, RoleRule.Inheritance, null, BuildingType.Market + "|" + BuildingType.Well + "|" + BuildingType.Camp),
            (Era.Town, RoleType.Priest, RoleRule.Naming, BuildingType.Church, BuildingType.Church),
            (Era.Town, RoleType.Innkeeper, RoleRule.Succession, BuildingType.Tavern, BuildingType.Tavern),
            (Era.Town, RoleType.Guildmaster, RoleRule.GuildElection, BuildingType.Guildhall + "|" + BuildingType.Tavern, BuildingType.Guildhall + "|" + BuildingType.Tavern),
            (Era.Town, RoleType.MasterBuilder, RoleRule.WorkContest, BuildingType.Lumbermill, BuildingType.Lumbermill),
            (Era.Town, RoleType.Merchant, RoleRule.Appointment, BuildingType.Market, BuildingType.Market),
            (Era.Town, RoleType.Captain, RoleRule.Appointment, BuildingType.Market, BuildingType.Market),
        };

        /// <summary>The roles a trait wants (§4; the mayor's wants go to the lord, the innkeeper's are [est]).</summary>
        static readonly Dictionary<string, string[]> TraitRoles = new()
        {
            ["the brave"] = new[] { RoleType.Chief, RoleType.Captain },
            ["the stubborn"] = new[] { RoleType.Chief, RoleType.Headman, RoleType.Lord, RoleType.MasterBuilder },
            ["the kind"] = new[] { RoleType.Headman, RoleType.Lord, RoleType.Priest },
            ["the loud"] = new[] { RoleType.Chief, RoleType.Captain, RoleType.Merchant },
            ["the clever"] = new[] { RoleType.Guildmaster, RoleType.MasterBuilder, RoleType.Miller },
            ["the tinkerer"] = new[] { RoleType.Miller, RoleType.MasterBuilder },
            ["the dreamer"] = new[] { RoleType.Priest },
            ["the singer"] = new[] { RoleType.Priest, RoleType.Chief, RoleType.Innkeeper },
            ["the joker"] = new[] { RoleType.Lord, RoleType.Merchant, RoleType.Innkeeper },
            ["the gossip"] = new[] { RoleType.Headman, RoleType.Merchant, RoleType.Innkeeper },
            ["the grump"] = new[] { RoleType.Captain, RoleType.Guildmaster },
            ["the worrier"] = new[] { RoleType.Captain, RoleType.Priest },
            ["the gardener"] = new[] { RoleType.Miller, RoleType.Priest },
            ["the lazy"] = new string[0],
        };

        /// <summary>Craft jobs: the guild's voters and candidates, and the master builder's contest.</summary>
        static readonly string[] CraftJobs =
        {
            JobType.Mason, JobType.Smith, JobType.Weaver, JobType.Potter, JobType.Baker, JobType.Brewer, JobType.Toolmaker,
            JobType.Woodcutter, JobType.Stallholder,
        };

        static readonly Dictionary<string, string> FirstActs = new()
        {
            [RoleType.Chief] = "First act: moving the fire closer to their tent.",
            [RoleType.Headman] = "First act: a new bench on the green.",
            [RoleType.Lord] = "First act: a chair for the market steps.",
            [RoleType.Miller] = "The sails turned a little faster that week.",
            [RoleType.Priest] = "The bell was rung three times, just to be sure.",
            [RoleType.Innkeeper] = "The first round was on the house.",
            [RoleType.Guildmaster] = "A banner went up over the door the next morning.",
            [RoleType.MasterBuilder] = "They measured everything twice, including the people.",
            [RoleType.Merchant] = "The big stall got a new awning.",
            [RoleType.Captain] = "The lanterns were lit early that night.",
        };

        /// <summary>While the era moment fills its new offices, the lines are gathered into one (null otherwise).</summary>
        List<(Notable n, string role)> eraOffices;

        /// <summary>A new lord reviews the appointed posts once their own line is told.</summary>
        Notable newLordToReview;

        void ReviewIfNewLord()
        {
            var lord = newLordToReview;
            newLordToReview = null;
            if (lord != null) ReviewAppointments(lord);
        }

        // ---------- queries for views (and the planner, #29/#30) ----------
        /// <summary>The era's roles, in table order (whatever exists).</summary>
        public static IEnumerable<string> RolesOf(Era era) => RoleTable.Where(r => r.era == era).Select(r => r.role);

        public static RoleRule RuleOf(Era era, string role) => RoleTable.First(r => r.era == era && r.role == role).rule;

        /// <summary>The leader role of an era: chief → headman → lord.</summary>
        public static string LeaderOf(Era era) => era == Era.Camp ? RoleType.Chief : era == Era.Village ? RoleType.Headman : RoleType.Lord;

        /// <summary>Whether this role is open now: on the era's list and its seat exists.</summary>
        public bool RoleOpen(string role) => RoleTable.Any(r => r.era == State.era && r.role == role && (r.needs == null || Has(r.needs)));

        public RoleSeat SeatOf(string role) => State.roles?.FirstOrDefault(r => r.role == role);

        /// <summary>The role this notable holds, or null.</summary>
        public string RoleOf(Notable n) => n == null || State.roles == null ? null : State.roles.FirstOrDefault(r => r.holderId == n.id)?.role;

        public Notable HolderOf(string role)
        {
            var seat = SeatOf(role);
            return seat == null || seat.holderId < 0 ? null : FindNotable(seat.holderId);
        }

        /// <summary>The building a role works from (the planner reads it), or null.</summary>
        public Building SeatBuilding(string role)
        {
            var row = RoleTable.FirstOrDefault(r => r.era == State.era && r.role == role);
            if (row.seat == null) return null;
            foreach (var type in row.seat.Split('|'))
            {
                var b = State.buildings.FirstOrDefault(x => x.type == type);
                if (b != null) return b;
            }
            return null;
        }

        /// <summary>Living notables who could stand for this role now: working adults with no role; when there are none,
        /// the retired (except for chief, which they step down from), before anyone is pulled from the crowd. The standing
        /// favours age and experience, so the young rarely win while someone older stands (the doc's 25+ [est]).</summary>
        public List<Notable> RoleCandidates(string role = null)
        {
            var free = State.notables.Where(n => n.alive && n.age >= AdultAge && RoleOf(n) == null).ToList();
            var working = free.Where(n => n.age < Config.retireAge).ToList();
            return working.Count > 0 || role == RoleType.Chief ? working : free;
        }

        bool CanStand(Notable n) => n.alive && n.age >= AdultAge && n.age < Config.retireAge && RoleOf(n) == null;

        public static bool Aspires(Notable n, string role) => TraitRoles.TryGetValue(n.trait ?? "", out var r) && Array.IndexOf(r, role) >= 0;

        // ---------- the monthly step ----------
        /// <summary>Every month (end of Step), at founding and after a load: seats follow the era and the buildings, the dead
        /// leave their seats, the chief steps down at retirement (unless stubborn), vacancies are filled by each role's
        /// rule; in spring, terms end and rivals challenge.</summary>
        void RolesStep() => RolesStep(false);

        void RolesStep(bool fillAll)
        {
            var s = State;
            s.roles ??= new List<RoleSeat>();
            bool spring = s.month % SimConfig.MonthsPerYear == SpringSeason;
            SyncSeats();

            List<(RoleSeat seat, Notable dead)> diedInOffice = null;
            foreach (var seat in s.roles)
            {
                if (seat.holderId < 0) continue;
                var h = FindNotable(seat.holderId);
                if (h == null || !h.alive)
                {
                    Vacate(seat);
                    if (h != null) (diedInOffice ??= new List<(RoleSeat, Notable)>()).Add((seat, h));
                }
                else if (seat.role == RoleType.Chief && h.age >= Config.retireAge && h.trait != "the stubborn")
                {
                    Vacate(seat);
                    RoleLine("role_stepdown", $"{h.FullName} stepped down as chief at {Math.Floor(h.age)} and sat with the elders by the fire.", 0.6f, h, null, seat);
                }
            }

            for (int i = 0; i < s.roles.Count; i++)
                if (s.roles[i].holderId < 0) TryFill(s.roles[i], fillAll || spring || !s.roles[i].everHeld);

            if (diedInOffice != null)
                foreach (var (seat, dead) in diedInOffice)
                {
                    if (seat.holderId >= 0) continue; // the succession line told it
                    int years = (int)((s.month - seat.sinceMonth) / SimConfig.MonthsPerYear);
                    RoleLine("role_death", $"{dead.FullName} died as {seat.role} after {Years(years)}. The {EraNoun(s.era)} left the seat empty until spring.", 0.85f, dead, null, seat);
                }

            if (!spring) return;
            foreach (var seat in s.roles.ToList())
                if (seat.holderId >= 0 && seat.termEndsMonth >= 0 && s.month >= seat.termEndsMonth) Reelect(seat);
            ChiefChallenge();
        }

        /// <summary>The seats follow the era's list and the buildings; a role leaving the list frees its holder.</summary>
        void SyncSeats()
        {
            var s = State;
            s.roles.RemoveAll(r => !RoleOpen(r.role));
            foreach (var row in RoleTable)
                if (row.era == s.era && !s.roles.Any(r => r.role == row.role) && RoleOpen(row.role))
                    s.roles.Add(new RoleSeat { role = row.role, vacantSinceMonth = s.month });
            var order = RolesOf(s.era).ToList();
            s.roles.Sort((a, b) => order.IndexOf(a.role).CompareTo(order.IndexOf(b.role)));
        }

        void Vacate(RoleSeat seat)
        {
            seat.lastHolderId = seat.holderId;
            seat.holderId = -1;
            seat.termEndsMonth = -1;
            seat.vacantSinceMonth = State.month;
        }

        void Assign(RoleSeat seat, Notable n)
        {
            var other = State.roles.FirstOrDefault(r => r != seat && r.holderId == n.id);
            if (other != null) Vacate(other); // one role per notable: an heir leaves their old role, which is filled by its own rule
            bool newLord = seat.role == RoleType.Lord && seat.holderId != n.id;
            seat.holderId = n.id;
            seat.sinceMonth = State.month;
            seat.vacantSinceMonth = -1;
            seat.everHeld = true;
            var rule = RuleOf(State.era, seat.role);
            seat.termEndsMonth = rule == RoleRule.Election || rule == RoleRule.GuildElection ? State.month + TermYears * SimConfig.MonthsPerYear : -1;
            if (newLord) newLordToReview = n; // after the line that tells it (RoleLine)
        }

        /// <summary>Fills an empty seat by its role's rule. Succession, naming, inheritance and appointment happen at once;
        /// contests and elections only when <paramref name="contestNow"/> (spring, a new seat, an era moment).</summary>
        void TryFill(RoleSeat seat, bool contestNow)
        {
            var rule = RuleOf(State.era, seat.role);
            var prev = FindNotable(seat.lastHolderId);
            switch (rule)
            {
                case RoleRule.Inheritance:
                    if (prev != null && Inherit(seat, prev)) return;
                    break;
                case RoleRule.Succession:
                    if (prev != null && Succeed(seat, prev)) return;
                    break;
                case RoleRule.Naming:
                    if (prev != null && NameSuccessor(seat, prev)) return;
                    break;
                case RoleRule.Appointment:
                    var lord = HolderOf(RoleType.Lord);
                    if (lord != null && lord.alive) { Appoint(seat, lord, null); return; }
                    break;
            }
            if (!contestNow) return;

            var pool = RoleCandidates(seat.role);
            if (pool.Count == 0)
            {
                var risen = FromTheCrowd();
                if (risen == null) return; // nobody, and no room: the seat waits
                Assign(seat, risen);
                RoleLine("role_rise", $"{risen.FullName}, {risen.trait}, stepped out of the crowd to become {RoleIn(seat.role)}.", 0.7f, risen, null, seat);
                return;
            }

            if (rule == RoleRule.Election || rule == RoleRule.GuildElection)
            {
                var (cands, voters) = Electorate(rule, pool);
                var (w, r, vw, vr) = Election(seat.role, cands, voters);
                Assign(seat, w);
                Won(seat, w, r, vw, vr, voters.Count);
                return;
            }
            var field = rule == RoleRule.WorkContest && pool.Any(IsCraft) ? pool.Where(IsCraft).ToList() : pool;
            var ranked = field.OrderByDescending(n => Standing(n, seat.role)).ThenBy(n => n.id).ToList();
            Assign(seat, ranked[0]);
            var runnerUp = ranked.Count > 1 ? ranked[1] : null;
            if (rule == RoleRule.WorkContest && runnerUp != null)
                RoleLine("role_masterwork", $"{ranked[0].FullName} and {runnerUp.FullName} each built a chimney to prove who should be master builder. {ranked[0].FullName}'s is still standing.", 0.65f + RivalBonus(ranked[0], runnerUp), ranked[0], runnerUp, seat);
            else if (rule == RoleRule.Inheritance && prev != null)
                RoleLine("role_won", $"With no one of {prev.FullName}'s blood left to take the title, the notables chose {ranked[0].FullName} as {RoleIn(RoleType.Lord)}.", 0.75f, ranked[0], prev, seat);
            else
                Won(seat, ranked[0], runnerUp != null && Aspires(runnerUp, seat.role) ? runnerUp : null, -1, -1, 0);
        }

        /// <summary>The line for a contest or an election won.</summary>
        void Won(RoleSeat seat, Notable w, Notable loser, int votesW, int votesL, int voters)
        {
            string place = EraNoun(State.era);
            if (w.trait == "the lazy")
                RoleLine("role_reluctant", $"{w.FullName} was made {seat.role} while asleep at the meeting. They have not yet been told.", 0.6f, w, loser, seat);
            else if (loser != null && w.rivalId == loser.id)
                RoleLine("role_contest", $"Old rivals {w.FullName} and {loser.FullName} both stood for {seat.role}. The {place} chose {w.FullName}{Votes(votesW, votesL)}; {loser.FullName} has not spoken to them since.", 0.88f, w, loser, seat);
            else if (loser != null)
                RoleLine("role_contest", $"{w.FullName} and {loser.FullName} both wanted to be {seat.role}. The {place} chose {w.FullName}{Votes(votesW, votesL)}; {loser.FullName} {SoreLoser(loser)}.", 0.78f, w, loser, seat);
            else
                RoleLine("role_won", $"{w.FullName} the {Calling(w)} became {RoleIn(seat.role)}. {FirstActs[seat.role]}", 0.75f, w, null, seat);
        }

        /// <summary>"chief of the camp", "the town's captain of the watch".</summary>
        string RoleIn(string role) => role.Contains(" of ") ? $"the {EraNoun(State.era)}'s {role}" : $"{role} of the {EraNoun(State.era)}";

        static string Votes(int a, int b) => a < 0 ? "" : a == b ? " on a tied vote" : $", {a} {(a == 1 ? "vote" : "votes")} to {b}";

        static float RivalBonus(Notable a, Notable b) => a.rivalId == b.id ? 0.1f : 0f;

        /// <summary>How a loser takes it (#61: every lost vote read "went home and shut the door very loudly", tents included).
        /// Picked by a hash, no dice.</summary>
        string SoreLoser(Notable loser)
        {
            if (State.era == Era.Camp) return "stomped off to their tent and stayed there until supper";
            string[] ways =
            {
                "went home and shut the door very loudly",
                "spent the evening telling the goats it was rigged",
                "took a very long walk round the pond",
            };
            return ways[(int)(RoleRoll(29, loser.id, 0) * ways.Length) % ways.Length];
        }

        /// <summary>A naming or an appointment passes over the young while someone of 25 or more could take it (§3's 25+
        /// [est]; #61 found a priest named on their sixteenth birthday).</summary>
        static float Seasoned(Notable n) => n.age >= 25f ? 2f : 0f;

        // ---------- the rules ----------
        /// <summary>The lord's title passes in the family: eldest adult child, else the nearest relative (§ lord, decisions
        /// 2026-10-29), else a contest in spring.</summary>
        bool Inherit(RoleSeat seat, Notable prev)
        {
            var (heir, rel) = Heir(prev);
            if (heir == null) return false;
            Assign(seat, heir);
            string text = prev.alive
                ? $"{prev.FullName} gave up the title, and {rel}, {heir.FullName}, became {RoleIn(RoleType.Lord)}."
                : $"When old {prev.FullName} died, {rel}, {heir.FullName}, became {RoleIn(RoleType.Lord)}.";
            RoleLine("role_inherited", text, 0.8f, heir, prev, seat);
            return true;
        }

        /// <summary>The next of the blood: eldest adult child, then grandchild, sibling, parent, sibling's child, then anyone
        /// of the family name. Adults only; the retired may inherit.</summary>
        public (Notable heir, string relation) Heir(Notable prev)
        {
            var living = State.notables.Where(n => n.alive && n.id != prev.id && n.age >= AdultAge).ToList();
            Notable Eldest(IEnumerable<Notable> xs) => xs.OrderByDescending(n => n.age).ThenBy(n => n.id).FirstOrDefault();
            bool ChildOf(Notable c, Notable p) => p != null && c.parentId == p.id;
            var kids = State.notables.Where(c => ChildOf(c, prev)).ToList();
            var parent = FindNotable(prev.parentId);
            var siblings = prev.parentId < 0 ? new List<Notable>() : State.notables.Where(n => n.id != prev.id && n.parentId == prev.parentId).ToList();

            var h = Eldest(living.Where(n => ChildOf(n, prev)));
            if (h != null) return (h, "their eldest child");
            h = Eldest(living.Where(n => kids.Any(k => ChildOf(n, k))));
            if (h != null) return (h, "their grandchild");
            h = Eldest(living.Where(n => siblings.Contains(n)));
            if (h != null) return (h, "their sibling");
            if (parent != null && parent.alive) return (parent, "their parent");
            h = Eldest(living.Where(n => siblings.Any(sb => ChildOf(n, sb))));
            if (h != null) return (h, "their sibling's child");
            h = Eldest(living.Where(n => n.family == prev.family));
            if (h != null) return (h, "a cousin");
            return (null, null);
        }

        /// <summary>The mill or the inn passes to the apprentice, else the eldest adult child (one role each).</summary>
        bool Succeed(RoleSeat seat, Notable prev)
        {
            bool Ok(Notable n) => n.alive && n.age >= AdultAge && RoleOf(n) == null;
            var apprentice = State.notables.Where(n => Ok(n) && n.mentorId == prev.id).OrderByDescending(n => n.age).ThenBy(n => n.id).FirstOrDefault();
            var child = State.notables.Where(n => Ok(n) && n.parentId == prev.id).OrderByDescending(n => n.age).ThenBy(n => n.id).FirstOrDefault();
            var next = apprentice ?? child;
            if (next == null) return false;
            Assign(seat, next);
            string place = seat.role == RoleType.Miller ? "mill" : "inn";
            string chore = seat.role == RoleType.Miller ? "swept its floor" : "carried its barrels";
            string who = next == apprentice ? $"their apprentice {next.FullName}" : $"their child {next.FullName}";
            string text = prev.alive
                ? $"Old {prev.FullName} handed the {place} to {who}, who had {chore} since they were small."
                : $"When {prev.FullName} died, the {place} passed to {who}, who had {chore} since they were small.";
            RoleLine("role_succession", text, 0.7f, next, prev, seat);
            return true;
        }

        /// <summary>The outgoing priest names the next: their apprentice, else whoever they think best (friends, family,
        /// a calling for it). The first priest is chosen at the founding of the shrine (a contest).</summary>
        bool NameSuccessor(RoleSeat seat, Notable prev)
        {
            var pool = RoleCandidates(seat.role);
            var apprentice = State.notables.Where(n => n.alive && n.age >= AdultAge && RoleOf(n) == null && n.mentorId == prev.id)
                .OrderByDescending(n => n.age).ThenBy(n => n.id).FirstOrDefault();
            var next = apprentice ?? pool.OrderByDescending(c => Affinity(prev, c) + (Aspires(c, seat.role) ? 1.5f : 0f) + Seasoned(c) + RoleRoll(3, prev.id, c.id))
                .ThenBy(c => c.id).FirstOrDefault();
            if (next == null) return false;
            Assign(seat, next);
            RoleLine("role_named", $"{(prev.alive ? "" : "Before the end, ")}{prev.FullName} named {next.FullName} the new priest. {next.FullName} has been practising the blessing on the goats.", 0.72f, next, prev, seat);
            return true;
        }

        /// <summary>The lord appoints: friends, family and those who want it; rivals never. <paramref name="keep"/> is the
        /// current holder when a new lord reviews the post.</summary>
        void Appoint(RoleSeat seat, Notable lord, Notable keep)
        {
            var pool = RoleCandidates(seat.role);
            if (keep != null && keep.alive && keep.age < Config.retireAge) pool.Add(keep);
            if (pool.Count == 0) { if (keep == null) TryFillFromCrowd(seat); return; }
            var pick = pool.OrderByDescending(c => Affinity(lord, c) + (Aspires(c, seat.role) ? 1.5f : 0f) + (c == keep ? 1f : 0f) + Seasoned(c) + RoleRoll(4, lord.id, c.id))
                .ThenBy(c => c.id).First();
            if (pick == keep) return; // kept on, no line
            Assign(seat, pick);
            string flavour = seat.role == RoleType.Captain ? $"The watch is {pick.FullName} and a very large dog." : $"{pick.FullName} promised everyone a fair price, and mostly kept it.";
            RoleLine("role_appointed", $"Lord {lord.FullName} made {pick.FullName} {seat.role}. {flavour}", 0.55f, pick, lord, seat);
        }

        void TryFillFromCrowd(RoleSeat seat)
        {
            var risen = FromTheCrowd();
            if (risen == null) return;
            Assign(seat, risen);
            RoleLine("role_rise", $"{risen.FullName}, {risen.trait}, stepped out of the crowd to become {RoleIn(seat.role)}.", 0.7f, risen, null, seat);
        }

        /// <summary>A new lord reviews the appointed posts: they keep or replace each holder.</summary>
        void ReviewAppointments(Notable lord)
        {
            foreach (var seat in State.roles.ToList())
            {
                if (RuleOf(State.era, seat.role) != RoleRule.Appointment || seat.holderId == lord.id) continue;
                var holder = seat.holderId >= 0 ? FindNotable(seat.holderId) : null;
                if (holder == null) continue; // empty posts fill in their own turn
                Appoint(seat, lord, holder);
            }
        }

        /// <summary>Spring at the end of a term: an election; the holder may stand again (if not retired).</summary>
        void Reelect(RoleSeat seat)
        {
            var rule = RuleOf(State.era, seat.role);
            var holder = FindNotable(seat.holderId);
            var pool = RoleCandidates(seat.role).Where(CanStand).ToList(); // an election wants working adults
            if (holder != null && holder.alive && holder.age < Config.retireAge) pool.Add(holder);
            if (pool.Count == 0 || (pool.Count == 1 && pool[0] == holder))
            {
                seat.termEndsMonth = State.month + TermYears * SimConfig.MonthsPerYear; // nobody else: another term
                return;
            }
            var (cands, voters) = Electorate(rule, pool);
            var (w, r, vw, vr) = Election(seat.role, cands, voters);
            if (w == holder)
            {
                seat.termEndsMonth = State.month + TermYears * SimConfig.MonthsPerYear;
                string recount = r != null ? $" {r.FullName} asked for a recount of all {voters.Count} votes." : "";
                RoleLine("role_reelected", $"{Place} kept {w.FullName} as {seat.role} for another twelve years.{recount}", 0.6f, w, r, seat);
                return;
            }
            Vacate(seat);
            Assign(seat, w);
            if (holder != null)
                RoleLine("role_contest", $"{w.FullName} stood against {seat.role} {holder.FullName} and won{Votes(vw, VotesFor(holder, cands, voters, seat.role))}. {holder.FullName} {SoreLoser(holder)}.", 0.78f + RivalBonus(w, holder), w, holder, seat);
            else Won(seat, w, r, vw, vr, voters.Count);
        }

        int VotesFor(Notable c, List<Notable> cands, List<Notable> voters, string role) => voters.Count(v => Ballot(v, cands, role) == c);

        /// <summary>Who stands and who votes: everyone (headman), or the craft notables (guildmaster; everyone if no guild yet).</summary>
        (List<Notable> cands, List<Notable> voters) Electorate(RoleRule rule, List<Notable> pool)
        {
            var voters = State.notables.Where(n => n.alive && n.age >= AdultAge).ToList();
            if (rule != RoleRule.GuildElection) return (pool, voters);
            var guild = voters.Where(IsCraft).ToList();
            var craftCands = pool.Where(IsCraft).ToList();
            return (craftCands.Count > 0 ? craftCands : pool, guild.Count >= 2 ? guild : voters);
        }

        bool IsCraft(Notable n) => Array.IndexOf(CraftJobs, n.job) >= 0;

        /// <summary>Each voter votes for the candidate they're closest to (self, partner, friend, family; never a rival),
        /// leaning to those who want it. Most votes wins; a tie goes to standing.</summary>
        (Notable winner, Notable runnerUp, int votesW, int votesR) Election(string role, List<Notable> cands, List<Notable> voters)
        {
            var tally = cands.ToDictionary(c => c, c => 0);
            foreach (var v in voters)
            {
                var pick = Ballot(v, cands, role);
                if (pick != null) tally[pick]++;
            }
            var ranked = cands.OrderByDescending(c => tally[c]).ThenByDescending(c => Standing(c, role)).ThenBy(c => c.id).ToList();
            var r = ranked.Count > 1 && tally[ranked[1]] > 0 ? ranked[1] : null;
            return (ranked[0], r, tally[ranked[0]], r != null ? tally[r] : 0);
        }

        Notable Ballot(Notable v, List<Notable> cands, string role) =>
            cands.OrderByDescending(c => (v == c ? 3f : Affinity(v, c)) + (Aspires(c, role) ? 1f : 0f) + RoleRoll(2, v.id, c.id)).ThenBy(c => c.id).FirstOrDefault();

        /// <summary>How much <paramref name="a"/> favours <paramref name="b"/>: partner, friend, family; a rival counts against.</summary>
        static float Affinity(Notable a, Notable b)
        {
            if (a == b) return 0f;
            float s = 0f;
            if (a.partnerId == b.id) s += 3f;
            if (a.friendId == b.id) s += 2f;
            if (a.family == b.family || a.parentId == b.id || b.parentId == a.id) s += 1.5f;
            if (a.rivalId == b.id) s -= 3f;
            return s;
        }

        /// <summary>A candidate's standing (§3): aspiration + age and experience + friends and family − rivals + a die
        /// from a hash [est weights].</summary>
        public float Standing(Notable n, string role)
        {
            float s = Aspires(n, role) ? 2f : 0f;
            s += Math.Clamp((n.age - AdultAge) / 15f, 0f, 2f);
            int ties = State.notables.Count(o => o.alive && o != n &&
                (o.id == n.partnerId || o.id == n.friendId || o.family == n.family || o.id == n.parentId || o.parentId == n.id));
            s += Math.Min(2f, ties * 0.5f);
            if (Alive(n.rivalId) != null) s -= 1f;
            return s + RoleRoll(1, n.id, role.Length * 31 + role[0]) * 1.5f;
        }

        /// <summary>Every ~15 years a rival who could stand may challenge the chief at the fire.</summary>
        void ChiefChallenge()
        {
            var seat = SeatOf(RoleType.Chief);
            var chief = seat == null ? null : HolderOf(RoleType.Chief);
            if (chief == null) return;
            long since = Math.Max(seat.sinceMonth, seat.lastChallengeMonth);
            if (State.month - since < ChallengeYears * SimConfig.MonthsPerYear) return;
            var rival = Alive(chief.rivalId);
            if (rival == null || !CanStand(rival) || RoleRoll(5, chief.id, rival.id) >= 0.5f) return;
            seat.lastChallengeMonth = State.month;
            if (Standing(rival, RoleType.Chief) > Standing(chief, RoleType.Chief))
            {
                Vacate(seat);
                Assign(seat, rival);
                seat.lastChallengeMonth = State.month;
                RoleLine("role_challenge", $"{rival.FullName} challenged old rival {chief.FullName} at the fire, and won. {rival.FullName} is chief now; {chief.FullName} sulks by the woodpile.", 0.85f, rival, chief, seat);
            }
            else
                RoleLine("role_challenge", $"{rival.FullName} challenged {chief.FullName} for the chief's seat at the fire, and lost. {chief.FullName} is still chief.", 0.7f, chief, rival, seat);
        }

        // ---------- era change ----------
        /// <summary>The era moved: the leader carries over (chief → headman → lord, one line), roles the new era doesn't
        /// have end, and the new offices are filled at once, told in one line.</summary>
        void RolesOnEraChange()
        {
            var s = State;
            s.roles ??= new List<RoleSeat>();
            var oldLeader = s.roles.FirstOrDefault(r => r.role == LeaderOf(s.era - 1));
            var leader = oldLeader != null && oldLeader.holderId >= 0 ? FindNotable(oldLeader.holderId) : null;
            if (leader != null && !leader.alive) leader = null;
            if (oldLeader != null) s.roles.Remove(oldLeader);
            SyncSeats();
            if (leader != null)
            {
                var seat = SeatOf(LeaderOf(s.era));
                Assign(seat, leader);
                string from = LeaderOf(s.era - 1), to = LeaderOf(s.era);
                string coda = s.era == Era.Village ? "They kept the feather." : "The title will pass to their children.";
                RoleLine("role_era", $"The {EraNoun(s.era - 1)} became a {(s.era == Era.Village ? "village" : "town")}, and {from} {leader.FullName} became its first {to}. {coda}", 0.8f, leader, null, seat);
            }
            eraOffices = new List<(Notable, string)>();
            try { RolesStep(true); }
            finally
            {
                var offices = eraOffices;
                eraOffices = null;
                if (offices.Count > 0)
                {
                    var parts = offices.Take(3).Select(o => $"{o.n.FullName} became {o.role}").ToList();
                    string rest = offices.Count > 3 ? $", and {offices.Count - 3} more {(offices.Count == 4 ? "took up an office" : "took up offices")}" : "";
                    var e = Chronicle("role_offices", $"The new {EraNoun(s.era)} needed people for its offices: {string.Join(", ", parts)}{rest}.", 0.7f, offices[0].n.id);
                    if (offices.Count > 1) e.otherNotableId = offices[1].n.id;
                }
            }
        }

        // ---------- helpers ----------
        /// <summary>A notable steps out of the crowd to fill a role, if the era's band + family room has space. Drawn from a
        /// stream seeded by a hash (not the economy's dice); they get a job at once.</summary>
        Notable FromTheCrowd()
        {
            var s = State;
            if (s.notables.Count(n => n.alive) >= NotablesWanted.max + Config.familyRoom) return null;
            var r = new SimRandom((int)((uint)s.seed * 0x2545F491u ^ (uint)s.month * 0x9E3779B1u ^ (uint)s.nextId * 0x85EBCA77u ^ 0x5EA7C0DEu));
            var n = new Notable
            {
                id = s.NextId(), name = r.Pick(NameData.Given), family = r.Pick(NameData.Families), trait = r.Pick(NameData.Traits),
                age = r.Range(25f, 40f), lifespan = r.Range(Config.lifespanMin, Config.lifespanMax), bornMonth = s.month,
            };
            for (int tries = 0; tries < 20 && s.notables.Any(o => o.alive && o.FullName == n.FullName); tries++)
                n.name = r.Pick(NameData.Given);
            s.notables.Add(n);
            n.job = ChooseJob(n, null);
            return n;
        }

        /// <summary>0..1 from the seed, the people and the month: deterministic, and it draws from no stream.</summary>
        float RoleRoll(int salt, int a, int b)
        {
            uint h = (uint)State.seed * 0x9E3779B1u ^ (uint)a * 0x85EBCA77u ^ (uint)b * 0x27D4EB2Fu ^ (uint)State.month * 0xC2B2AE3Du ^ (uint)salt * 0x165667B1u;
            h ^= h >> 16; h *= 0x7FEB352Du; h ^= h >> 15; h *= 0x846CA68Bu; h ^= h >> 16;
            return (h >> 8) * (1f / 16777216f);
        }

        static string Calling(Notable n) => string.IsNullOrEmpty(n.job) ? (n.trait ?? "").Replace("the ", "") : n.job;

        static string Years(int y) => y == 1 ? "a year" : y < 1 ? "less than a year" : $"{y} years";

        /// <summary>A role line, linked to the seat building; during the era moment the new offices gather into one line.</summary>
        void RoleLine(string kind, string text, float salience, Notable who, Notable other, RoleSeat seat)
        {
            if (eraOffices != null && who != null && seat.holderId == who.id && kind != "role_era" && kind != "role_rise")
                eraOffices.Add((who, seat.role));
            else
            {
                var e = Chronicle(kind, text, salience, who?.id ?? -1, other?.id ?? -1);
                e.buildingId = SeatBuilding(seat.role)?.id ?? -1;
            }
            ReviewIfNewLord();
        }

        // ---------- debug ----------
        /// <summary>Debug: empty this role now (the holder steps out; the role's own rule fills it, contests next spring).</summary>
        public bool DebugVacateRole(string role)
        {
            var seat = SeatOf(role);
            if (seat == null || seat.holderId < 0) return false;
            Vacate(seat);
            return true;
        }

        /// <summary>Debug: make this notable the holder now (they leave any other role).</summary>
        public bool DebugSetRoleHolder(string role, Notable n)
        {
            var seat = SeatOf(role);
            if (seat == null || n == null || !n.alive) return false;
            Assign(seat, n);
            ReviewIfNewLord();
            return true;
        }

        /// <summary>Debug: hold every pending contest and election now, instead of waiting for spring.</summary>
        public void DebugFillRoles() => RolesStep(true);

        /// <summary>Debug: end this role's term now (an election at once).</summary>
        public bool DebugEndTerm(string role)
        {
            var seat = SeatOf(role);
            if (seat == null || seat.holderId < 0 || seat.termEndsMonth < 0) return false;
            Reelect(seat);
            return true;
        }
    }
}
