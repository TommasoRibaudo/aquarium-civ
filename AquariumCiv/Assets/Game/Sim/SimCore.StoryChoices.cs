using System;
using System.Collections.Generic;
using System.Linq;
using AquariumCiv.Sim.Eco;

namespace AquariumCiv.Sim
{
    /// <summary>
    /// #722 §4 (docs/design/new-events.md 4.1-4.4): the story events' small owned choices. Each is a Keeper's ask on the small-choice machinery
    /// (SimCore.SmallChoices.cs: one open at a time, the gap, three visits, "Let them decide", the card and the choice scene; no new UI). Their
    /// own catalog (<see cref="SimCore.StoryChoiceCatalog"/>), like the life choices', so the small choices' "every option leaves a thing" rule
    /// keeps to the everyday ones: these leave a person's part in a ritual, a song's name or a pup's keeper, not a prop. Option 0 / 1:
    /// <list type="bullet">
    /// <item><see cref="Offering"/> (4.1): {a} or {b} carries the offering every year while they live (the town's priest still comes first).</item>
    /// <item><see cref="FirstLog"/> (4.2): {a} or {b} sets the first log on the woodpile every autumn.</item>
    /// <item><see cref="SongName"/> (4.3): the singer's song (2.2) is called {n1} or {n2} (the ritual's two names); on the ritual's caption.</item>
    /// <item><see cref="PupHolder"/> (4.4): the litter's pup's keeper is {a} (today's rule: the first kid without a dog) or {b}.</item>
    /// </list>
    /// All text is the spec's, [est] until the user signs it off.
    /// </summary>
    public static class StoryChoices
    {
        public const string Offering = "offering_carrier", FirstLog = "first_log", SongName = "song_name", PupHolder = "pup_holder";

        /// <summary>4.1 / 4.2: asked in the months before the ritual's own (<see cref="RitualKind.MonthOf"/>), never in its month or after.</summary>
        public const int AskMonthsAhead = 3;

        static DilemmaOptionDef Pick(string who, string touch, string hint, string done) => new DilemmaOptionDef
        {
            touch = touch.Replace("{p}", who), label = who, hint = hint.Replace("{p}", who), done = done.Replace("{p}", who),
        };

        static DilemmaDef Who(string kind, string question, string shortAsk, string touch, string hint, string done) => new DilemmaDef
        {
            id = "small_" + kind, kind = DilemmaKind.Who, size = DilemmaSize.Small, smallKind = kind, cast = DilemmaCast.TwoCandidates,
            minEra = Era.Camp, maxEra = Era.Town, question = question, shortAsk = shortAsk,
            letDone = done.Replace("{p}", "{n}"),
            options = new[] { Pick("{a}", touch, hint, done), Pick("{b}", touch, hint, done) },
        };

        public static readonly DilemmaDef OfferingDef = Who(Offering,
            "Who carries the offering to the Keeper's tree this year, {a} or {b}?", "Who carries the offering.",
            "{p} carries it", "{p} carries it to the tree every year after this.",
            "{p} carried the offering to the Keeper's tree.");

        public static readonly DilemmaDef FirstLogDef = Who(FirstLog,
            "Who sets the first log on the woodpile this autumn, {a} or {b}?", "Who sets the first log.",
            "{p} sets it", "{p} starts the line and sets the first log every autumn.",
            "{p} set the first log on the woodpile.");

        public static readonly DilemmaDef PupDef = Who(PupHolder,
            "A new pup came. Which child holds it first: {a} or {b}?", "Who holds the new pup.",
            "{p} holds it", "The pup follows {p} until it is grown.",
            "{p} held the new pup first. It followed them everywhere after that.");

        /// <summary>4.3: {who} is the singer (the dilemma's subject), {n1} / {n2} the ritual's two names.</summary>
        public static readonly DilemmaDef SongDef = new DilemmaDef
        {
            id = "small_" + SongName, kind = DilemmaKind.Tradition, size = DilemmaSize.Small, smallKind = SongName, cast = DilemmaCast.None,
            minEra = Era.Camp, maxEra = Era.Town,
            question = "{who}'s song needs a name. {n1} or {n2}?", shortAsk = "A name for {who}'s song.",
            letDone = "{who} called the song {n}.",
            options = new[]
            {
                new DilemmaOptionDef { touch = "call it {n1}", label = "{n1}", hint = "Everyone sings {n1} at it every year.", done = "The Keeper named {who}'s song {n1}." },
                new DilemmaOptionDef { touch = "call it {n2}", label = "{n2}", hint = "Everyone sings {n2} at it every year.", done = "The Keeper named {who}'s song {n2}." },
            },
        };

        public static readonly DilemmaDef[] All = { OfferingDef, FirstLogDef, SongDef, PupDef };

        public static bool Is(DilemmaDef def) => def?.smallKind != null && (def.smallKind == Offering || def.smallKind == FirstLog || def.smallKind == SongName || def.smallKind == PupHolder);

        /// <summary>4.3's names, two per ritual (the spec's table).</summary>
        public static (string a, string b) SongNames(string ritual) => ritual switch
        {
            RitualKind.Berrying => ("Purple Fingers", "The Long Walk"),
            RitualKind.Feast => ("The Long Table", "One More Bowl"),
            RitualKind.Offering => ("To the Tree", "The Best Basket"),
            RitualKind.Woodpile => ("The First Log", "Snow on the Roof"),
            _ => ("", ""),
        };

        /// <summary>2.2's {ritual}: "made up a song for the harvest feast".</summary>
        public static string RitualWords(string ritual) => ritual switch
        {
            RitualKind.Berrying => "the berrying walk", RitualKind.Feast => "the harvest feast", RitualKind.Offering => "the offering",
            RitualKind.Woodpile => "stacking the woodpile", _ => "the ritual",
        };
    }

    /// <summary>
    /// #722 §2.2, §4, §5: the singer's song, the four asks' sim side (the hooks SimCore.SmallChoices.cs calls), and the Keeper's child's two
    /// callbacks (5.1 the coming of age, a staged story; 5.2 the wedding, whose line the wedding moment says on its caption). No dice.
    /// </summary>
    public partial class SimCore
    {
        /// <summary>The story asks the sim draws from (tests can swap it).</summary>
        public IReadOnlyList<DilemmaDef> StoryChoiceCatalog { get; set; } = StoryChoices.All;

        // ---------- 4.1 / 4.2: who leads the offering / the woodpile ----------

        /// <summary>The one the Keeper picked to lead this ritual (-1 none). <see cref="RitualLead"/> takes them while they live.</summary>
        public int ChosenLeadId(string ritual) => ritual == RitualKind.Offering ? StoryMarks.offeringCarrierId : ritual == RitualKind.Woodpile ? StoryMarks.firstLogId : -1;

        static string LeadRitualOf(string smallKind) => smallKind == StoryChoices.Offering ? RitualKind.Offering : smallKind == StoryChoices.FirstLog ? RitualKind.Woodpile : null;

        /// <summary>The person card's line for a ritual the Keeper picked them to lead ("" none).</summary>
        string StoryLeadCardLine(Notable n)
        {
            var parts = new List<string>();
            if (StoryMarks.offeringCarrierId == n.id) parts.Add(n.alive ? "Carries the offering to the Keeper's tree every year." : "Carried the offering to the Keeper's tree.");
            if (StoryMarks.firstLogId == n.id) parts.Add(n.alive ? "Sets the first log on the woodpile every autumn." : "Set the first log on the woodpile.");
            return string.Join(" ", parts);
        }

        /// <summary>Who has led this ritual (its lines' people, pruned History aside).</summary>
        HashSet<int> LedBefore(string ritual)
        {
            string a = RitualFirstKind(ritual), b = RitualYearKind(ritual);
            return new HashSet<int>(State.chronicle.Where(e => e.kind == a || e.kind == b).Select(e => e.notableId));
        }

        bool AskedThisYear(DilemmaDef def) => State.dilemmas.Any(x => x.defId == def.id && x.month / SimConfig.MonthsPerYear == RitualYear);

        string LeadAskWhyNot(DilemmaDef def, string ritual, out int a, out int b, bool force)
        {
            a = b = -1;
            if (State.customs == null || !HasCustom(RitualKind.CustomOf(ritual))) return $"the {ritual} ritual isn't kept";
            if (ritual == RitualKind.Offering && !KeeperTreeGrown) return "the Keeper's tree isn't grown";
            if (ritual == RitualKind.Woodpile && !WoodpileStands) return "no woodpile yet";
            if (ritual == RitualKind.Offering && State.era == Era.Town && HolderOf(RoleType.Priest) is Notable p && p.alive) return "the priest carries it";
            if (Alive(ChosenLeadId(ritual)) != null) return "the Keeper's pick still leads it";
            if (!force)
            {
                int month = RitualKind.MonthOf(ritual), cal = CalendarMonth;
                if (cal >= month || cal < month - StoryChoices.AskMonthsAhead) return "not in the months before it";
                if (AskedThisYear(def)) return "asked this year";
            }
            var led = LedBefore(ritual);
            var fresh = State.notables.Where(n => n.alive && n.age >= AdultAge && !led.Contains(n.id))
                .OrderByDescending(n => DilemmaRoll(124, n.id, State.dilemmaNextId)).ThenBy(n => n.id).ToList();
            if (fresh.Count < 2) return "fewer than two grown-ups who haven't led it";
            a = fresh[0].id; b = fresh[1].id;
            return null;
        }

        // ---------- 2.2 the singer's song ----------

        /// <summary>Debug: the next ritual played makes the song whatever the year's pacing (not saved).</summary>
        bool songForced;

        /// <summary>The first grown-up singer alive (the spec: "the first grown-up with that trait").</summary>
        Notable Singer() => State.notables.Where(n => n.alive && n.age >= AdultAge && n.trait == "the singer").OrderBy(n => n.id).FirstOrDefault();

        /// <summary>From <see cref="PlayRitual"/>: the first ritual played while a singer lives gets their song, once a box (at most one new event a
        /// game year). B80.d: it rides the ritual line's held moment (<paramref name="ritualLine"/>): the singer's notes first, everyone's by the
        /// end, the song's line on the caption; a card only if that moment is (<see cref="HeldEntryOf"/>).</summary>
        ChronicleEntry SingerSongAfter(string ritual, ChronicleEntry ritualLine = null)
        {
            var m = StoryMarks;
            if (StoryHappened(StoryEvent.SingerSong) || m.songOwnerId >= 0) return null;
            if (!songForced && StoryFirstThisYear()) return null;
            var singer = Singer();
            if (singer == null) return null;
            songForced = false;
            m.songRitual = ritual; m.songOwnerId = singer.id; m.songName = "";
            var e = StoryLine(StoryEvent.SingerSong, $"{singer.name} made up a song for {StoryChoices.RitualWords(ritual)}. After that, everyone called it {singer.name}'s song.", 0.6f, singer.id, -1);
            m.songLineId = e.id; m.songRideId = ritualLine?.id ?? -1;
            return e;
        }

        /// <summary>#721 + #722 (4.3): the caption the view shows over a ritual's held moment: its name, and the song's name once it has one
        /// ("The harvest feast: One More Bowl"). Never a bubble: ritual bubbles stay wordless (#276).</summary>
        public string RitualCaption(string ritual)
        {
            string c = RitualKind.Caption(ritual);
            var m = StoryMarks;
            return !string.IsNullOrEmpty(c) && m.songRitual == ritual && !string.IsNullOrEmpty(m.songName) ? $"{c}: {m.songName}" : c;
        }

        // ---------- 4.4 which child holds the new pup ----------

        /// <summary>From <see cref="Litter"/>: the pup that stays went to the first kid without a dog (today's rule) while a second one had none
        /// too: the Keeper may pick between them.</summary>
        void PupAskAfterLitter(Dog pup, List<Notable> kids)
        {
            if (pup == null || kids == null || kids.Count < 2 || pup.keeperId != kids[0].id) return;
            var m = StoryMarks;
            m.pupDogId = pup.id; m.pupKidA = kids[0].id; m.pupKidB = kids[1].id;
        }

        string PupAskWhyNot(out int a, out int b)
        {
            a = b = -1;
            if (!EcologyOn) return "no living land";
            var m = StoryMarks;
            if (m.pupDogId < 0) return "no new pup with two kids to hold it";
            if (m.pupAskedDog == m.pupDogId) return "asked for this pup";
            var dog = FindDog(m.pupDogId);
            if (dog == null || !dog.Alive || DogStageOf(dog) != DogStage.Pup) return "the pup has grown";
            var ka = Alive(m.pupKidA); var kb = Alive(m.pupKidB);
            if (ka == null || kb == null || ka.age >= AdultAge || kb.age >= AdultAge) return "the two kids are not both kids now";
            a = ka.id; b = kb.id;
            return null;
        }

        /// <summary>The pup's ask is raised as the litter happens (like the lost pup's), not at the next small slot: null when it isn't waiting.</summary>
        DilemmaDef SeasonalStoryAsk(out int a, out int b)
        {
            a = b = -1;
            var def = StoryChoiceCatalog?.FirstOrDefault(x => x.smallKind == StoryChoices.PupHolder);
            if (def == null || State.era < def.minEra || State.era > def.maxEra) return null;
            if (State.dilemmas.Any(x => x.defId == def.id && x.state == DilemmaState.Open)) return null;
            return PupAskWhyNot(out a, out b) == null ? def : null;
        }

        // ---------- the hooks (SimCore.SmallChoices.cs) ----------

        /// <summary>Why this story ask can't be raised now (null = it can); a and b are the people its options point at. <paramref name="force"/>
        /// (the cheats) lifts the season window and the asked-this-year rule.</summary>
        string StoryAskWhyNot(DilemmaDef def, out int a, out int b, bool force)
        {
            a = b = -1;
            if (State.era < def.minEra || State.era > def.maxEra) return "not in this era";
            if (State.dilemmas.Any(x => x.defId == def.id && x.state == DilemmaState.Open)) return "open now";
            switch (def.smallKind)
            {
                case StoryChoices.Offering: case StoryChoices.FirstLog:
                    return LeadAskWhyNot(def, LeadRitualOf(def.smallKind), out a, out b, force);
                case StoryChoices.SongName:
                {
                    var m = StoryMarks;
                    if (m.songOwnerId < 0 || string.IsNullOrEmpty(m.songRitual)) return "no song yet (the singer's song first)";
                    if (!string.IsNullOrEmpty(m.songName)) return "the song has its name";
                    if (Alive(m.songOwnerId) == null) return "the singer is gone";
                    if (!force && State.dilemmas.Any(x => x.defId == def.id)) return "asked already";
                    return null;
                }
                case StoryChoices.PupHolder:
                    return PupAskWhyNot(out a, out b);
            }
            return "unknown story ask";
        }

        /// <summary>Debug and tests: why the story ask of this kind can't be raised now in normal play (null = it can). Read-only.</summary>
        public string StoryAskWhy(string kind)
        {
            var def = StoryChoiceCatalog?.FirstOrDefault(x => x.smallKind == kind);
            return def == null ? "not in the catalog" : StoryAskWhyNot(def, out _, out _, false);
        }

        List<(DilemmaDef def, int a, int b)> EligibleStoryAsks()
        {
            var list = new List<(DilemmaDef def, int a, int b)>();
            if (StoryChoiceCatalog == null) return list;
            foreach (var def in StoryChoiceCatalog)
                if (StoryAskWhyNot(def, out int a, out int b, false) == null) list.Add((def, a, b));
            return list;
        }

        /// <summary>Fill the card's words once raised: the song's maker and its two names; the pup asked about.</summary>
        void PrepareStoryAsk(Dilemma d, DilemmaDef def)
        {
            var m = StoryMarks;
            if (def.smallKind == StoryChoices.SongName)
            {
                d.subjectId = m.songOwnerId;
                (d.nameA, d.nameB) = StoryChoices.SongNames(m.songRitual);
            }
            else if (def.smallKind == StoryChoices.PupHolder) m.pupAskedDog = m.pupDogId;
        }

        /// <summary>What the pick does. 4.1 / 4.2: the Keeper's pick (or "Let them decide") leads the ritual while they live; a lapse leaves the
        /// usual rule (nobody was picked). 4.3: the song takes the name, whoever chose it. 4.4: the pup's keeper is the one picked (option 0,
        /// the default, is today's rule's kid).</summary>
        void ApplyStoryAsk(Dilemma d, DilemmaDef def, int option)
        {
            var m = StoryMarks;
            int chosen = d.PersonOf(option);
            bool keeper = d.state == DilemmaState.Answered;
            switch (def.smallKind)
            {
                case StoryChoices.Offering:
                    if (keeper && Alive(chosen) != null) m.offeringCarrierId = chosen;
                    break;
                case StoryChoices.FirstLog:
                    if (keeper && Alive(chosen) != null) m.firstLogId = chosen;
                    break;
                case StoryChoices.SongName:
                {
                    string name = option == 0 ? d.nameA : d.nameB;
                    if (!string.IsNullOrEmpty(name)) m.songName = name;
                    break;
                }
                case StoryChoices.PupHolder:
                {
                    var dog = FindDog(m.pupDogId);
                    if (dog != null && dog.Alive && Alive(chosen) != null) dog.keeperId = chosen;
                    break;
                }
            }
        }

        // ---------- 5.1 / 5.2 the Keeper's child ----------

        static string PersonFlag(string id, int notableId) => "story:" + id + ":" + notableId;

        /// <summary>A notable turns grown-up: the hidden coming-of-age line (their first job), and the Keeper's child's story (5.1).</summary>
        void ComeOfAge(Notable n, bool cheatWork = false)
        {
            Chronicle("coming_of_age", ComingOfAgeLine(n, cheatWork), 0.4f, n.id);
            KeeperChildGrown(n);
        }

        /// <summary>5.1: a child the Keeper named comes of age with work: their line (a held Story moment: at the fire an elder hands them their
        /// work's tool). Once per person; nothing for one who waits for work.</summary>
        ChronicleEntry KeeperChildGrown(Notable n)
        {
            if (n == null || !n.namedByKeeper || string.IsNullOrEmpty(n.job) || n.job == JobType.Elder) return null;
            string flag = PersonFlag(StoryEvent.KeeperChildGrown, n.id);
            if (State.storyFlags.Contains(flag)) return null;
            State.storyFlags.Add(flag);
            return Record(StoryEvent.Kind(StoryEvent.KeeperChildGrown), $"{n.name}, named by the Keeper, came of age and took up work as {A(JobLabel(n.job))}.", 0.6f, n.id, -1);
        }

        /// <summary>5.2: the one of a couple the Keeper named who hasn't had their wedding told so yet (a first), else null; the flag is set.</summary>
        Notable KeeperChildWedding(Notable a, Notable b)
        {
            foreach (var n in new[] { a, b })
            {
                if (n == null || !n.namedByKeeper) continue;
                string flag = PersonFlag("keeper_child_wed", n.id);
                if (State.storyFlags.Contains(flag)) continue;
                State.storyFlags.Add(flag);
                var other = n == a ? b : a;
                if (other != null && other.namedByKeeper) State.storyFlags.Add(PersonFlag("keeper_child_wed", other.id)); // one line for both
                return n;
            }
            return null;
        }

        string KeeperChildWeddingLine(Notable child, Notable other) => $"{child.name}, named by the Keeper, married {other.name} at the fire.";

        /// <summary>5.2: the wedding moment's caption: its line when it is the Keeper's child's (said on the caption when its vows begin, #764), else null.</summary>
        public string KeeperWeddingLine(int entryId)
        {
            if (!StoryMarks.keeperWeddings.Contains(entryId)) return null;
            for (int i = State.chronicle.Count - 1; i >= 0; i--)
                if (State.chronicle[i].id == entryId) return State.chronicle[i].text;
            return null;
        }

        // ---------- debug (SimDebugPanel's "Stories (#722)" block) ----------

        /// <summary>Debug ("Trait: the singer's song"): the song is made at the next ritual played: a grown-up singer (the youngest grown-up
        /// becomes one if there is none) and a kept ritual (the harvest feast is kept if none is). <paramref name="ritual"/>: the one to play.
        /// False and why when there is no grown-up.</summary>
        public bool DebugSingerReady(out string ritual, out string why)
        {
            why = ""; ritual = null;
            var singer = Singer();
            if (singer == null)
            {
                singer = State.notables.Where(n => n.alive && n.age >= AdultAge).OrderBy(n => n.age).ThenBy(n => n.id).FirstOrDefault();
                if (singer == null) { why = "no grown-up to sing"; return false; }
                singer.trait = "the singer";
            }
            EnsureDilemmaState();
            var m = StoryMarks;
            ritual = !string.IsNullOrEmpty(m.songRitual) ? m.songRitual : Rituals.FirstOrDefault() ?? RitualKind.Feast;
            if (!State.customs.Contains(RitualKind.CustomOf(ritual))) State.customs.Add(RitualKind.CustomOf(ritual));
            State.storyFlags.Remove("story:" + StoryEvent.SingerSong);
            m.songOwnerId = -1; m.songRitual = ""; m.songName = ""; m.songLineId = m.songRideId = -1;
            songForced = true;
            return true;
        }

        /// <summary>Debug ("People: the Keeper's child grows up"): the oldest kid the Keeper named (a kid is named so first if none is; a kid
        /// joins if there is none; with <see cref="DebugCastable"/> set, one the view can cast now) comes of age now, with work: on a land that
        /// gives none yet they help make the tools. The 5.1 line, or null and why.</summary>
        public ChronicleEntry DebugKeeperChildGrown(out string why)
        {
            why = "";
            var kids = State.notables.Where(n => n.alive && n.age >= 4f && n.age < AdultAge).OrderByDescending(n => n.namedByKeeper).ThenByDescending(n => n.age).ThenBy(n => n.id).ToList();
            var kid = State.notables.Where(n => n.alive && n.age < AdultAge && n.namedByKeeper).OrderByDescending(n => n.age).ThenBy(n => n.id).FirstOrDefault()
                      ?? kids.FirstOrDefault();
            kid = DebugPick(kids, kid);
            if (kid == null) { DebugAddKids(1, AdultAge - 1f); kid = State.notables.Where(n => n.alive && n.age < AdultAge).OrderByDescending(n => n.age).ThenBy(n => n.id).FirstOrDefault(); }
            if (kid == null) { why = "no kid in the box"; return null; }
            kid.namedByKeeper = true;
            State.storyFlags.Remove(PersonFlag(StoryEvent.KeeperChildGrown, kid.id));
            kid.age = AdultAge;
            ComeOfAge(kid, true);
            var e = State.chronicle.LastOrDefault(x => x.kind == StoryEvent.Kind(StoryEvent.KeeperChildGrown) && x.notableId == kid.id);
            if (e == null) why = $"{kid.name} came of age with no work ({kid.job})";
            return e;
        }

        /// <summary>Debug ("People: the Keeper's child marries"): a grown-up the Keeper named (the youngest unwed grown-up is named so first if
        /// none is) marries another unwed grown-up now; with <see cref="DebugCastable"/> set, two the view can cast now. With fewer than two
        /// unwed grown-ups a newcomer joins first (the gate made true). The wedding line, or null and why.</summary>
        public ChronicleEntry DebugKeeperChildWed(out string why)
        {
            why = "";
            List<Notable> Unwed() => State.notables.Where(n => n.alive && n.age >= AdultAge && Alive(n.partnerId) == null).OrderBy(n => n.age).ThenBy(n => n.id).ToList();
            var free = Unwed();
            for (int i = free.Count; i < 2 && State.notables.Any(n => n.alive && n.age >= AdultAge); i++)
            {
                var nu = AddNotable(AdultAge + 3f + i, -1, null);
                nu.job = ChooseJob(nu, null); // a cheat's grown-up (no line of their own)
                State.population++;
            }
            free = Unwed();
            var ready = free.Where(DebugOk).ToList();
            if (ready.Count >= 2) free = ready;
            var child = free.FirstOrDefault(n => n.namedByKeeper) ?? free.FirstOrDefault();
            var other = free.FirstOrDefault(n => n != child);
            if (child == null || other == null) { why = "two unwed grown-ups needed"; return null; }
            child.namedByKeeper = true;
            State.storyFlags.Remove(PersonFlag("keeper_child_wed", child.id));
            Wedding(child, other, CalendarMonth);
            return State.chronicle.LastOrDefault(x => x.kind == "wedding");
        }

        /// <summary>Debug: raise one story ask now (by kind, <see cref="StoryChoices"/>), its gate eased where a cheat must (the ritual kept, the
        /// season, an old pick forgotten; the song made first; a litter had); the open choice lapses and the open wish closes first. Null and why
        /// when it can't be cast.</summary>
        public Dilemma DebugRaiseStoryAsk(string kind, out string why)
        {
            why = "";
            var def = StoryChoiceCatalog.FirstOrDefault(x => x.smallKind == kind) ?? StoryChoices.All.FirstOrDefault(x => x.smallKind == kind);
            if (def == null) { why = "unknown ask " + kind; return null; }
            EnsureDilemmaState();
            var m = StoryMarks;
            switch (kind)
            {
                case StoryChoices.Offering: case StoryChoices.FirstLog:
                {
                    string ritual = LeadRitualOf(kind);
                    if (!State.customs.Contains(RitualKind.CustomOf(ritual))) State.customs.Add(RitualKind.CustomOf(ritual));
                    if (kind == StoryChoices.Offering) m.offeringCarrierId = -1; else m.firstLogId = -1;
                    if (kind == StoryChoices.FirstLog && !WoodpileStands) DebugFirstWildWoodNow(); // a living land's woodpile comes with its first wild wood
                    break;
                }
                case StoryChoices.SongName:
                    if (m.songOwnerId < 0 || !string.IsNullOrEmpty(m.songName) || Alive(m.songOwnerId) == null)
                    {
                        if (DebugStory(StoryEvent.SingerSong, out why) == null) return null;
                    }
                    break;
                case StoryChoices.PupHolder:
                    if (PupAskWhyNot(out _, out _) != null)
                    {
                        if (!EcologyOn) { why = "no living land"; return null; }
                        m.pupDogId = -1;
                        DebugNextDogGeneration();
                        if (m.pupDogId < 0) { why = "no litter with two kids without a dog ('Two kids join the camp' first)"; return null; }
                    }
                    break;
            }
            var open = OpenDilemma;
            if (open != null) LapseDilemma(open);
            var w = OpenWish;
            if (w != null) CloseWish(w, WishState.Lapsed);
            why = StoryAskWhyNot(def, out int a, out int b, true);
            if (why != null) return null;
            why = "";
            var d = RaiseDilemma(def, a, b);
            choiceStageFor = d.id;
            return d;
        }
    }
}
