using System;
using System.Collections.Generic;
using System.Linq;

namespace AquariumCiv.Sim
{
    /// <summary>The slice's five relics (relics-and-glass-voice.md §1, decisions 2026-10-29 "Slice relics and the glass voice").</summary>
    public static class RelicId
    {
        public const string SkyCrumb = SimCore.GlassRelic.SkyCrumb, FestivalMask = "festival_mask", KeeperDoll = SimCore.GlassRelic.KeeperDoll,
            LeftSandal = "left_sandal", Lens = SimCore.GlassRelic.Lens;

        public static readonly string[] All = { SkyCrumb, FestivalMask, KeeperDoll, LeftSandal, Lens };

        /// <summary>Box 2 pre-ticks these at the era moment (Choice 6 ★): one per stage.</summary>
        public static readonly string[] DefaultCarried = { SkyCrumb, KeeperDoll, Lens };
    }

    /// <summary>One relic of this box. Kept on <see cref="SimState.relics"/>. The Left Sandal is seeded (lost) long before it
    /// is found; every other relic is added when it is made.</summary>
    [Serializable]
    public class Relic
    {
        public string id;
        public long foundMonth = -1;    // made or found (-1 = the Sandal, still lost)
        public long lostMonth = -1;     // the Sandal: when it was lost
        public long takenMonth = -1;    // carried to the next box (the era moment's pick; -1 = not taken)
        public int ownerId = -1;        // whose it is: the taster, the mask's wearer, the doll's maker, the sandal's owner, the lens's scholar
        public int holderId = -1;       // who keeps it now (the Sandal goes to an heir if its owner has died)
        public float x, z;              // its spot (box coordinates, as Building.x/z): where it fell, was set up, was lost
        public int buildingId = -1;     // the building by the spot, if any
        public int entryId = -1;        // the card it was born in
        public int lostEntryId = -1;    // the Sandal: the card it was lost in
        public string prop = "";        // the Keeper Doll: what it holds (KeeperAction prop: cloth, watering can, ...)
        public string cause = "";       // the Sky Crumb: the shaping it fell with; the Lens: "lamp" or "fallback"

        public bool Found => foundMonth >= 0;
    }

    /// <summary>How often the Keeper did one thing in this box (the Keeper Doll holds the most-done one). Wipes, prunes and
    /// trash count at most once per game month (a wipe is many strokes); every shaping counts.</summary>
    [Serializable]
    public class KeeperActionCount
    {
        public string action;
        public int count;
        public long lastMonth = -1;
    }

    /// <summary>The Keeper's actions the sim counts (<see cref="SimCore.KeeperActionsDone"/>). Shapings use their NudgeKind.</summary>
    public static class KeeperAction
    {
        public const string Wipe = "wipe", Prune = "prune", Trash = "trash";

        /// <summary>The Keeper Doll's prop for an action, or null if the doll has none for it (§1.3).</summary>
        public static string PropOf(string action) => action switch
        {
            Wipe => "cloth",
            SimCore.NudgeKind.Rain => "watering can",
            SimCore.NudgeKind.Grove => "sapling",
            SimCore.NudgeKind.Channel => "spade",
            SimCore.NudgeKind.Lamp => "lantern",
            _ => null,
        };

        /// <summary>The five actions the doll can hold, in tie-break order.</summary>
        public static readonly string[] WithProp = { Wipe, SimCore.NudgeKind.Rain, SimCore.NudgeKind.Grove, SimCore.NudgeKind.Channel, SimCore.NudgeKind.Lamp };
    }

    /// <summary>Belief: each stage has an early half (the era opened it) and a later half (its relic opened it).</summary>
    public enum BeliefHalf { Early, Later }

    /// <summary>When the people say a line (relics-and-glass-voice.md §5 "When").</summary>
    public static class PeopleMoment
    {
        public const string Wipe = "wipe", Finger = "finger", Glass = "glass", Tap = "tap", Return = "return", Away = "away",
            Evening = "evening", Any = "any", Rain = "rain", Lamp = "lamp", Festival = "festival", Crumb = "crumb", Doll = "doll",
            Play = "play", Found = "found", Lens = "lens", LensLook = "lens_look", TapBack = "tapback", Heir = "heir";
    }

    /// <summary>One line of the bank (§5). Stage 0 = any stage (the festival bridge, the Sandal). Half 'E' early, 'L' after
    /// the stage's relic, '-' either. <see cref="quote"/> false = a chronicle card line (narrator; {0} = the speaker).</summary>
    public sealed class BankLine
    {
        public int no, stage;
        public char half;
        public string speaker, moment, text;
        public bool quote;
    }

    /// <summary>A quote picked for the view or an entry: the line and who says it (-1 = the crowd).</summary>
    public sealed class PeopleQuote
    {
        public int lineNo;
        public string text;
        public int speakerId = -1;
    }

    /// <summary>
    /// The slice relics (#74; relics-and-glass-voice.md §1–§6). Five relics, each born in a chronicle card, each with a
    /// trigger from the box's own story and a fallback so none is missable:
    /// <list type="bullet">
    /// <item><b>Sky Crumb</b>: the box's second shaping (fallback: the first shaping after the camp).</item>
    /// <item><b>Festival Mask</b>: the first era festival (camp → village). Seam: the era change is the festival until #86's
    /// festival calls <see cref="EraFestivalHeld"/>; a box that missed it gets it at the next era change.</item>
    /// <item><b>Keeper Doll</b>: village and 10 credited Keeper entries (fallback: after 15 village years, or in the town, the
    /// next credited entry). It holds what the Keeper does most (<see cref="KeeperActionCount"/>).</item>
    /// <item><b>Left Sandal</b>: the first "lost" story becomes the sandal's loss (owner and spot kept); at least 20 game years
    /// later the next prune finds it, and it goes back to the owner or an heir.</item>
    /// <item><b>Lens</b>: town + the Lamp used in the town + a scholar alive (clever, tinkerer or dreamer notable; fallback:
    /// after 10 town years, any town shaping).</item>
    /// </list>
    /// Belief (<see cref="BeliefStage"/>, <see cref="BeliefHalfNow"/>) is #59's glass stage: the era opens a stage, its relic
    /// opens the later half. The people's quotes come from the line bank (§5) by stage and half.
    /// No dice are drawn here, no economy value is written, and relic cards take ids from their own counter
    /// (<see cref="SimState.relicLineNextId"/>), so the shared ids and the economy never move. The Sandal's loss is the
    /// "lost" story the story stream already drew, retold (same kind, same id).
    /// </summary>
    public partial class SimCore
    {
        /// <summary>Debug switch: false turns the relics off (no relic cards, no retold loss, no quotes, no counters). Not saved.</summary>
        public bool RelicsOn { get; set; } = true;

        public const int RelicLineIdBase = 1_500_000_000; // relic cards' own ids, far above the shared counter and the term lines

        /// <summary>Raised when a relic is made or found (the view plays its moment).</summary>
        public event Action<Relic> RelicFound;

        const string FlagLampInTown = "lamp-town", FlagTapBack = "tapback";

        // ---------- queries ----------
        public Relic FindRelic(string id) => State.relics?.FirstOrDefault(r => r.id == id);

        public bool HasRelic(string id) => FindRelic(id)?.Found == true;

        /// <summary>Relics found in this box, in the order they were found.</summary>
        public IEnumerable<Relic> FoundRelics => (State.relics ?? new List<Relic>()).Where(r => r.Found).OrderBy(r => r.foundMonth);

        /// <summary>The belief stage (1–3): #59's glass stage. The era opens it; it never goes back.</summary>
        public int BeliefStage => GlassStage;

        /// <summary>Early until the stage's own relic is found (Sky Crumb, Keeper Doll, Lens), then later.</summary>
        public BeliefHalf BeliefHalfNow => GlassNudged ? BeliefHalf.Later : BeliefHalf.Early;

        /// <summary>The scholars (Choice 5 ★): town notables, adult and alive, with the clever, tinkerer or dreamer trait
        /// (clever first, then tinkerer, then dreamer; then by id). Empty before the town.</summary>
        public List<Notable> Scholars => State.era < Era.Town ? new List<Notable>() : State.notables
            .Where(n => n.alive && n.age >= AdultAge && ScholarRank(n.trait) >= 0)
            .OrderBy(n => ScholarRank(n.trait)).ThenBy(n => n.id).ToList();

        static int ScholarRank(string trait) => trait switch { "the clever" => 0, "the tinkerer" => 1, "the dreamer" => 2, _ => -1 };

        /// <summary>How often the Keeper did this (an action of <see cref="KeeperAction"/> or a NudgeKind).</summary>
        public int KeeperActionsDone(string action) => State.keeperActions?.FirstOrDefault(a => a.action == action)?.count ?? 0;

        /// <summary>The prop the Keeper Doll would hold now: what the Keeper did most of the five (cloth for wipes, watering
        /// can, sapling, spade, lantern). Ties go to the earlier of the five; nothing yet = the cloth.</summary>
        public string DollProp
        {
            get
            {
                string best = KeeperAction.Wipe;
                int most = -1;
                foreach (var a in KeeperAction.WithProp)
                {
                    int c = KeeperActionsDone(a);
                    if (c > most) { most = c; best = a; }
                }
                return KeeperAction.PropOf(best);
            }
        }

        /// <summary>The label in the people's voice (§1; {0} = the Sandal's owner).</summary>
        public string RelicLabel(string id)
        {
            switch (id)
            {
                case RelicId.SkyCrumb: return "Sky Bread. Fell when the Bright One made the trees. Delicious.";
                case RelicId.FestivalMask: return "The Sky's Face. Worn at the first village dance. Smiles on one side; the other side was still drying.";
                case RelicId.KeeperDoll: return "The Keeper. Hands: very large. Eye: always open.";
                case RelicId.LeftSandal:
                    return $"The Left Sandal of {Named(FindRelic(id)?.ownerId ?? -1)}. Lost to the stream, found by the Keeper. The right one was never seen again.";
                case RelicId.Lens: return "The Great Eye-Glass. Through it: a room, a lamp, and something with whiskers.";
            }
            return "";
        }

        /// <summary>The book's hint for a relic not found yet (§1.6, C8), in the people's voice.</summary>
        public string RelicHint(string id)
        {
            switch (id)
            {
                case RelicId.SkyCrumb: return "The old ones say something fell from the sky once…";
                case RelicId.FestivalMask: return "When the camp becomes a village, there will be dancing.";
                case RelicId.KeeperDoll: return "Someone should make a likeness of the one who watches.";
                case RelicId.LeftSandal:
                    var s = FindRelic(id);
                    return s == null ? "Every family has a story about something lost." : $"{Named(s.ownerId)} still talks about that sandal.";
                case RelicId.Lens: return "If only we could see further than the wall.";
            }
            return "";
        }

        // ---------- the era tag on trait lines (§4) ----------
        /// <summary>The first era a trait line may be told in: the four that know too much are town-only (stage 3); every
        /// other line, the stand-ins included, from the camp. (The stand-ins are only told before the town.)</summary>
        public static Era TraitLineEra(string line) => TownOnlyLines.ContainsKey(line) ? Era.Town : Era.Camp;

        /// <summary>Every trait line with its era tag, and its stand-in before the town where it has one.</summary>
        public static IEnumerable<(string trait, string line, Era era, string standIn)> TraitLines() =>
            TraitStories.SelectMany(kv => kv.Value.lines.Select(l => (kv.Key, l, TraitLineEra(l), TownOnlyLines.TryGetValue(l, out var s) ? s : null)));

        // ---------- hooks from the rest of the sim ----------
        /// <summary>A Keeper's action (SimCore.cs: wipe, prune, trash; Nudges: every shaping). Counted for the doll.</summary>
        void CountKeeperAction(string action, bool oncePerMonth)
        {
            if (!RelicsOn) return;
            State.keeperActions ??= new List<KeeperActionCount>();
            var a = State.keeperActions.FirstOrDefault(x => x.action == action);
            if (a == null) State.keeperActions.Add(a = new KeeperActionCount { action = action });
            if (oncePerMonth && a.lastMonth == State.month) return;
            a.count++;
            a.lastMonth = State.month;
        }

        /// <summary>After every shaping (SimCore.Nudges.cs): count it; the Sky Crumb falls with the second; the Lamp in the
        /// town readies the Lens; after 10 town years any town shaping makes it.</summary>
        void RelicsOnShaping(Nudge n)
        {
            if (!RelicsOn || n == null) return;
            CountKeeperAction(n.kind, false);
            if (!HasRelic(RelicId.SkyCrumb) && (State.nudges.Count >= 2 || State.era > Era.Camp)) MakeSkyCrumb(n);
            if (State.era == Era.Town && !HasRelic(RelicId.Lens))
            {
                if (n.kind == NudgeKind.Lamp && !State.storyFlags.Contains(FlagLampInTown)) State.storyFlags.Add(FlagLampInTown);
                if (State.storyFlags.Contains(FlagLampInTown) && Scholars.Count > 0) MakeLens(n, "lamp");
                else if (YearsInEra >= Config.lensFallbackYears) MakeLens(n, "fallback");
            }
        }

        /// <summary>The player pruned (SimCore.cs). Some overgrowth must have been cut: weeds never make more finds.</summary>
        void RelicsOnPrune(float pruned)
        {
            if (!RelicsOn || pruned <= 0f) return;
            CountKeeperAction(KeeperAction.Prune, true);
            var s = FindRelic(RelicId.LeftSandal);
            if (s != null && !s.Found && State.month - s.lostMonth >= (long)Config.sandalLostYears * SimConfig.MonthsPerYear)
                FindSandal(s);
        }

        /// <summary>The era moment (SimCore.Cards.cs GlassOnEraChange): the first era festival leaves the Festival Mask.</summary>
        void RelicsOnEraChange()
        {
            if (!RelicsOn || State.era == Era.Camp) return;
            EraFestivalHeld();
        }

        /// <summary>
        /// The era festival was held (#86's festival calls this; until then the era change does). The first one leaves the
        /// Festival Mask; later calls do nothing. Returns the mask, or null before the village.
        /// </summary>
        public Relic EraFestivalHeld()
        {
            if (!RelicsOn || State.era == Era.Camp) return FindRelic(RelicId.FestivalMask);
            return HasRelic(RelicId.FestivalMask) ? FindRelic(RelicId.FestivalMask) : MakeMask();
        }

        /// <summary>The story stream drew a "lost" story for <paramref name="n"/> (SimCore.Story.cs): the box's first one becomes
        /// the Sandal's loss. Returns the line to tell instead, or null to tell the drawn one. The camp and village usually
        /// tell it; a town tells it only if no earlier "lost" story came (so it is never missable).</summary>
        string SandalLossLine(Notable n)
        {
            if (!RelicsOn || n == null || FindRelic(RelicId.LeftSandal) != null) return null;
            return $"{n.FullName} lost a sandal crossing the stream. {n.FullName} has walked a little lopsided, with great dignity, ever since.";
        }

        /// <summary>The Sandal's loss was told: seed the relic (owner, spot) to be found ≥ 20 years later.</summary>
        void SandalLost(Notable n, ChronicleEntry e)
        {
            var s = new Relic { id = RelicId.LeftSandal, ownerId = n.id, holderId = n.id, lostMonth = State.month, lostEntryId = e?.id ?? -1 };
            SpotAt(s, FirstOf(BuildingType.Farm) ?? FirstOf(BuildingType.Camp)); // the old gathering ground
            Relics.Add(s);
            if (e != null) e.relicId = RelicId.LeftSandal;
        }

        /// <summary>Every month, after care (no dice): the Keeper Doll and the Lens, and quotes on this month's Keeper entries.</summary>
        void RelicsStep()
        {
            if (!RelicsOn) return;
            var s = State;
            if (s.era >= Era.Village && !HasRelic(RelicId.KeeperDoll) && CreditedThisMonth())
            {
                int credited = s.chronicle.Count(e => e.keeper);
                bool fallback = s.era == Era.Town || YearsInEra >= Config.dollFallbackYears;
                if (credited >= Config.dollKeeperEntries || fallback) MakeDoll();
            }
            if (s.era == Era.Town && !HasRelic(RelicId.Lens) && s.storyFlags.Contains(FlagLampInTown) && Scholars.Count > 0) MakeLens(null, "lamp");
            QuoteKeeperEntries();
        }

        bool CreditedThisMonth()
        {
            var c = State.chronicle;
            for (int i = c.Count - 1; i >= 0 && c[i].month == State.month; i--) if (c[i].keeper) return true;
            return false;
        }

        /// <summary>The month's Keeper credits get a quote for their gift where the bank has one (rain, the Lamp), each line once.</summary>
        void QuoteKeeperEntries()
        {
            var c = State.chronicle;
            for (int i = c.Count - 1; i >= 0 && c[i].month == State.month; i--)
            {
                var e = c[i];
                if (!e.keeper || !string.IsNullOrEmpty(e.quote)) continue;
                var n = State.nudges.FirstOrDefault(x => x.credited && x.answerMonth == e.month && x.buildingId == e.buildingId);
                string moment = n?.kind == NudgeKind.Rain ? PeopleMoment.Rain : n?.kind == NudgeKind.Lamp ? PeopleMoment.Lamp : null;
                var q = moment == null ? null : Quote(moment);
                if (q != null && !c.Any(x => x.quote == q.text)) SetQuote(e, q); // each line once on a card; the view says it again live
            }
        }

        // ---------- making the relics ----------
        List<Relic> Relics => State.relics ??= new List<Relic>();

        Relic NewRelic(string id)
        {
            var r = FindRelic(id);
            if (r == null) Relics.Add(r = new Relic { id = id });
            return r;
        }

        Relic Found(Relic r, ChronicleEntry e)
        {
            r.foundMonth = State.month;
            r.entryId = e.id;
            e.relicId = r.id;
            GlassRelicFound(r.id); // the stage's later half (SimCore.Cards.cs); the Mask and Sandal say nothing about the stage
            RelicFound?.Invoke(r);
            return r;
        }

        static string KeeperDid(string kind) => kind switch
        {
            NudgeKind.Rain => "made rain",
            NudgeKind.Grove => "planted the grove",
            NudgeKind.Herd => "sent the herd",
            NudgeKind.Channel => "dug the channel",
            NudgeKind.Lamp => "hung the lamp",
            NudgeKind.Spring => "opened the spring",
            NudgeKind.Outcrop => "raised the rock",
            NudgeKind.DyeMeadow => "sowed the dye flowers",
            _ => "shaped the land",
        };

        Relic MakeSkyCrumb(Nudge n)
        {
            var r = NewRelic(RelicId.SkyCrumb);
            var taster = Living(HolderOf(LeaderOf(State.era))) ?? WithTrait("the brave") ?? FirstAdult();
            r.ownerId = r.holderId = taster?.id ?? -1;
            r.cause = n?.kind ?? "";
            if (n != null) { r.x = n.x; r.z = n.z; }
            var fire = FirstOf(BuildingType.Camp);
            r.buildingId = fire?.id ?? -1; // the last piece rests on a flat stone by the fire
            var e = RelicLine("relic",
                $"A piece of bread the size of a hut fell from the sky the year the Keeper {KeeperDid(r.cause)}. {Named(r.ownerId)} tasted it first. The {EraNoun(State.era)} ate well all winter.",
                0.9f, r.ownerId, r.buildingId);
            SetQuote(e, Line(9, -1));
            return Found(r, e);
        }

        Relic MakeMask()
        {
            var r = NewRelic(RelicId.FestivalMask);
            var wearer = Living(HolderOf(LeaderOf(State.era))) ?? WithTrait("the singer") ?? FirstAdult();
            r.ownerId = r.holderId = wearer?.id ?? -1;
            SpotAt(r, FirstOf(BuildingType.Camp)); // the ring dance round the fire, facing the glass
            var e = RelicLine("relic",
                $"{Place} danced for three days to welcome the new {EraNoun(State.era)}. {Named(r.ownerId)} wore a mask of the sky's face and nobody recognised them, though everyone did.",
                0.75f, r.ownerId, r.buildingId);
            SetQuote(e, Line(14, r.ownerId));
            return Found(r, e);
        }

        Relic MakeDoll()
        {
            var r = NewRelic(RelicId.KeeperDoll);
            var priest = Living(HolderOf(RoleType.Priest));
            var maker = priest != null && (priest.trait == "the dreamer" || priest.trait == "the singer") ? priest
                : WithTrait("the dreamer") ?? WithTrait("the kind") ?? WithTrait("the singer") ?? FirstAdult();
            r.ownerId = r.holderId = maker?.id ?? -1;
            r.prop = DollProp;
            SpotAt(r, FirstOf(BuildingType.Church) ?? FirstOf(BuildingType.Camp)); // by the shrine, or on the green
            var e = RelicLine("relic",
                $"{Named(r.ownerId)} carved the Keeper and stood them by the {(FirstOf(BuildingType.Church) != null ? "shrine" : "green")}. Hands: enormous. Eye: open. Everyone agrees it's a good likeness.",
                0.85f, r.ownerId, r.buildingId);
            SetQuote(e, Line(23, -1));
            return Found(r, e);
        }

        Relic MakeLens(Nudge n, string cause)
        {
            var r = NewRelic(RelicId.Lens);
            var scholar = Scholars.FirstOrDefault() ?? FirstAdult();
            r.ownerId = r.holderId = scholar?.id ?? -1;
            r.cause = cause;
            var lamp = State.nudges.LastOrDefault(x => x.kind == NudgeKind.Lamp) ?? n;
            if (lamp != null) { r.x = lamp.x; r.z = lamp.z; }
            else SpotAt(r, FirstOf(BuildingType.Church) ?? FirstOf(BuildingType.Camp));
            bool light = State.storyFlags.Contains(FlagLampInTown);
            var e = RelicLine("relic",
                $"{Named(r.ownerId)} ground a lens from the clearest glass in {Place} and pointed it at {(light ? "the Keeper's light" : "the wall")}. They have not slept since.",
                0.9f, r.ownerId, r.buildingId);
            SetQuote(e, Line(33, r.ownerId));
            return Found(r, e);
        }

        void FindSandal(Relic s)
        {
            var owner = FindNotable(s.ownerId);
            int years = (int)((State.month - s.lostMonth) / SimConfig.MonthsPerYear);
            string who = Named(s.ownerId);
            string text;
            if (owner != null && owner.alive)
            {
                s.holderId = owner.id;
                text = $"Under the weeds by the old gathering ground, the Keeper found the left sandal of {who}, lost {years} years ago. {who} says it still fits. It doesn't.";
            }
            else
            {
                var heir = HeirOf(s.ownerId, out string kin);
                s.holderId = heir?.id ?? -1;
                text = heir != null
                    ? $"Under the weeds by the old gathering ground, the Keeper found the left sandal of {who}, lost {years} years ago. {heir.name}, {who}'s {kin}, set it on the remembering stone."
                    : $"Under the weeds by the old gathering ground, the Keeper found the left sandal of {who}, lost {years} years ago. It sits on the remembering stone now.";
            }
            var e = RelicLine("relic", text, 0.8f, s.ownerId, s.buildingId);
            if (s.holderId >= 0 && s.holderId != s.ownerId) e.otherNotableId = s.holderId;
            var q = Line(28, s.holderId);
            if (q != null && State.era == Era.Camp) q.text = q.text.Replace("the window", "the sky"); // the camp has no window yet
            SetQuote(e, q);
            Found(s, e);
            var lost = State.chronicle.FirstOrDefault(x => x.id == s.lostEntryId);
            if (lost != null) LinkStories(lost, e); // the loss and the find are one card
        }

        /// <summary>The eldest living child, else the eldest living grandchild.</summary>
        Notable HeirOf(int id, out string kin)
        {
            kin = "child";
            var kids = State.notables.Where(n => n.parentId == id).ToList();
            var heir = kids.Where(n => n.alive).OrderByDescending(n => n.age).ThenBy(n => n.id).FirstOrDefault();
            if (heir != null) return heir;
            kin = "grandchild";
            var kidIds = kids.Select(k => k.id).ToHashSet();
            return State.notables.Where(n => n.alive && kidIds.Contains(n.parentId)).OrderByDescending(n => n.age).ThenBy(n => n.id).FirstOrDefault();
        }

        /// <summary>A relic card: its own id counter (shared ids never shift), no office coda.</summary>
        ChronicleEntry RelicLine(string kind, string text, float salience, int notableId, int buildingId)
        {
            var s = State;
            if (s.relicLineNextId < RelicLineIdBase) s.relicLineNextId = RelicLineIdBase;
            var e = new ChronicleEntry { id = s.relicLineNextId++, month = s.month, kind = kind, text = text, salience = salience, notableId = notableId, buildingId = buildingId };
            s.chronicle.Add(e);
            Chronicled?.Invoke(e);
            return e;
        }

        void SpotAt(Relic r, Building b)
        {
            if (b == null) return;
            r.buildingId = b.id;
            r.x = b.x; r.z = b.z;
        }

        /// <summary>A notable's full name for a card ("someone" if unknown).</summary>
        string Named(int id) => FindNotable(id)?.FullName ?? "someone";

        /// <summary>A relic card's short line for the peek and "Also…".</summary>
        string RelicHeadline(ChronicleEntry e) => e.relicId switch
        {
            RelicId.SkyCrumb => "Bread fell from the sky",
            RelicId.FestivalMask => $"{Who(e.notableId)} wore the sky's face",
            RelicId.KeeperDoll => $"{Who(e.notableId)} carved the Keeper",
            RelicId.LeftSandal => $"{Who(e.notableId)}'s sandal turned up",
            RelicId.Lens => $"{Who(e.notableId)} made a lens",
            _ => null,
        };

        Building FirstOf(string type) => State.buildings.FirstOrDefault(b => b.type == type);
        static Notable Living(Notable n) => n != null && n.alive ? n : null;
        Notable WithTrait(string trait) => State.notables.Where(n => n.alive && n.age >= AdultAge && n.trait == trait).OrderBy(n => n.id).FirstOrDefault();
        Notable FirstAdult() => State.notables.Where(n => n.alive && n.age >= AdultAge).OrderBy(n => n.id).FirstOrDefault();

        // ---------- the line bank (§5) ----------
        static BankLine Q(int no, int stage, char half, string speaker, string moment, string text) =>
            new BankLine { no = no, stage = stage, half = half, speaker = speaker, moment = moment, text = text, quote = true };
        static BankLine C(int no, int stage, char half, string speaker, string moment, string text) =>
            new BankLine { no = no, stage = stage, half = half, speaker = speaker, moment = moment, text = text, quote = false };

        /// <summary>The people's lines about the glass (relics-and-glass-voice.md §5), by stage and half. Quotes never say
        /// glass before the Lens; the narrator's card lines may.</summary>
        public static readonly BankLine[] LineBank =
        {
            Q(1, 1, 'E', "crowd", PeopleMoment.Wipe, "Not too close. Nobody knows what the sky does when it's poked."),
            Q(2, 1, 'E', "kid", PeopleMoment.Finger, "It's pointing at me! Is it pointing at me?"),
            Q(3, 1, 'E', "chief", PeopleMoment.Glass, "Nobody touches the edge of the sky. Not you, Pip. Especially not you."),
            C(4, 1, 'E', "the worrier", PeopleMoment.Any, "{0} moved the tents back from the edge of the sky. Just a little. Just in case."),
            Q(5, 1, 'E', "crowd", PeopleMoment.Tap, "The sky knocked!"),
            C(6, 1, 'E', "the brave", PeopleMoment.Any, "{0} walked right up to the edge of the sky and touched it. It was cold. {0} is a hero now."),
            Q(7, 1, 'E', "crowd", PeopleMoment.Return, "It's back. Wave, so it knows we're friendly."),
            Q(8, 1, 'E', "elder", PeopleMoment.Away, "The Bright One sleeps. Keep the fire low and the children quiet."),
            Q(9, 1, 'L', "crowd", PeopleMoment.Crumb, "If the sky drops bread, what else is up there?"),
            Q(10, 1, 'L', "kid", PeopleMoment.Glass, "Everyone look up. Maybe it'll drop another one."),
            C(11, 1, 'L', "the gossip", PeopleMoment.Any, "{0} swears the Bright One sneezed last spring. Half the camp believes it."),
            C(12, 1, 'L', "the dreamer", PeopleMoment.Any, "{0} dreamt they climbed to the edge of the sky and knocked. Something knocked back."),
            Q(13, 1, 'L', "chief", PeopleMoment.Evening, "Save the last of the sky bread. One day we'll need to remember it was real."),
            Q(14, 0, '-', "leader", PeopleMoment.Festival, "Dance facing the sky tonight. Let it see us."),
            Q(15, 2, 'E', "kid", PeopleMoment.Finger, "Catch the finger! It went that way!"),
            Q(16, 2, 'E', "crowd", PeopleMoment.Return, "Everyone to the window! The Keeper's looking in."),
            Q(17, 2, 'E', "priest", PeopleMoment.Glass, "Hats off at the window. Not you, goat."),
            Q(18, 2, 'E', "headman", PeopleMoment.Evening, "We'll put the new field where the Keeper can see it."),
            Q(19, 2, 'E', "crowd", PeopleMoment.Rain, "Keeper's rain on the east field. Told you the Keeper liked it."),
            Q(20, 2, 'E', "farmer", PeopleMoment.Lamp, "The Keeper's light is on the barley. Go and stand in it, it's lovely."),
            C(21, 2, 'E', "the grump", PeopleMoment.Any, "{0} says the Keeper looks in at all hours and it isn't polite. {0} combed their hair anyway."),
            Q(22, 2, 'L', "kid", PeopleMoment.Glass, "I waved and the Keeper waved back. They did, I saw it."),
            Q(23, 2, 'L', "crowd", PeopleMoment.Doll, "The doll's eye is open. That means the Keeper's is too."),
            Q(24, 2, 'L', "kid", PeopleMoment.Play, "Hide! See if the Keeper finds us."),
            C(25, 2, 'L', "the gossip", PeopleMoment.Any, "{0} says the Keeper hums while working. Nobody can prove otherwise."),
            C(26, 2, 'L', "the dreamer", PeopleMoment.Evening, "{0} sat at the window all evening, practising a wink for the Keeper."),
            Q(27, 2, 'L', "kid", PeopleMoment.Away, "We played Keeper all week. Pip was the hand."),
            Q(28, 0, '-', "owner", PeopleMoment.Found, "Lost something? Ask the window."),
            Q(29, 3, 'E', "scholar", PeopleMoment.Glass, "The wall is not sky. It is a thing. Things can be measured."),
            Q(30, 3, 'E', "priest", PeopleMoment.Evening, "The scholars want to measure the Keeper. The priest wants them to ask first."),
            Q(31, 3, 'E', "merchant", PeopleMoment.Any, "If there's a room out there, there's a market out there."),
            Q(32, 3, 'E', "captain", PeopleMoment.Lamp, "Lanterns lit from the Keeper's light. The night round can start."),
            Q(33, 3, 'L', "lens maker", PeopleMoment.Lens, "It's glass. Very thick, very clean, very old. I'd like a word with whoever cleans it."),
            Q(34, 3, 'L', "scholar", PeopleMoment.LensLook, "A lamp out there, and a chair, and something with whiskers."),
            C(35, 3, 'L', "the clever", PeopleMoment.Any, "{0} invented a mirror for signalling the Keeper."),
            C(36, 3, 'L', "the clever", PeopleMoment.Any, "{0} worked out how to tell the time from the glass's shadow."),
            C(37, 3, 'L', "the dreamer", PeopleMoment.Any, "{0} dreamt of a city beyond the glass and drew a map of it."),
            C(38, 3, 'L', "the gossip", PeopleMoment.Any, "{0} spread a rumour that the Keeper has a cat. It caused quite a stir."),
            C(39, 3, 'L', "scholar", PeopleMoment.TapBack, "The Keeper knocked {1} times. {0} knocked {1} times back and wrote it down."),
            Q(40, 3, 'L', "priest", PeopleMoment.Any, "The scholars say the Keeper is just very large. I've been saying that for years."),
            Q(41, 3, 'L', "scholar", PeopleMoment.Away, "The room's been dark. We lit a candle in our window, in case the Keeper's looking for the way back."),
            Q(42, 3, 'L', "the dreamer", PeopleMoment.Evening, "The Keeper's room has a window too. So who keeps the Keeper?"),
            Q(43, 3, 'L', "lord", PeopleMoment.Heir, "The Keeper, not a king. We bow to nobody. We do wave."),
            Q(44, 3, 'L', "kid", PeopleMoment.Glass, "When I grow up I'll be a scholar and look at the Keeper all day."),
        };

        /// <summary>The bank's lines the people can say now: this stage's early lines, its later ones once its relic is found,
        /// and the any-stage ones. Never a later stage's line.</summary>
        public IEnumerable<BankLine> LinesNow(bool quotesOnly = true)
        {
            int stage = BeliefStage;
            bool later = BeliefHalfNow == BeliefHalf.Later;
            return LineBank.Where(l => (!quotesOnly || l.quote) && (l.stage == 0 || (l.stage == stage && (l.half != 'L' || later))));
        }

        /// <summary>
        /// A quote for a moment (§5 "When"; <see cref="PeopleMoment"/>) in the people's words for their stage and half, and who
        /// says it; null if the bank has none now or nobody fits. Takes turns by the month. No dice.
        /// </summary>
        public PeopleQuote Quote(string moment)
        {
            if (!RelicsOn) return null;
            var fits = new List<PeopleQuote>();
            foreach (var l in LinesNow())
                if (l.moment == moment && Speaker(l.speaker, out int id)) fits.Add(Say(l, id));
            return fits.Count == 0 ? null : fits[(int)(State.month % fits.Count)];
        }

        /// <summary>A bank line as said by <paramref name="speakerId"/> (or its own speaker if -1 and one fits).</summary>
        PeopleQuote Line(int no, int speakerId)
        {
            var l = LineBank.First(x => x.no == no);
            if (speakerId < 0) Speaker(l.speaker, out speakerId);
            return Say(l, speakerId);
        }

        PeopleQuote Say(BankLine l, int speakerId) =>
            new PeopleQuote { lineNo = l.no, speakerId = speakerId, text = l.text.Replace("{0}", speakerId >= 0 ? Named(speakerId) : "Someone") };

        static void SetQuote(ChronicleEntry e, PeopleQuote q)
        {
            if (e == null || q == null) return;
            e.quote = q.text;
            e.speakerId = q.speakerId;
        }

        /// <summary>Who says a line now (-1 = the crowd). False if nobody fits (the line waits).</summary>
        bool Speaker(string who, out int id)
        {
            id = -1;
            Notable n;
            switch (who)
            {
                case "crowd": return true;
                case "kid":
                    n = State.notables.Where(x => x.alive && x.age < AdultAge && x.age >= 4f).OrderBy(x => x.age).ThenBy(x => x.id).FirstOrDefault()
                        ?? State.notables.Where(x => x.alive && x.age >= AdultAge).OrderBy(x => x.age).ThenBy(x => x.id).FirstOrDefault();
                    break;
                case "chief": n = Living(HolderOf(RoleType.Chief)); break;
                case "headman": n = Living(HolderOf(RoleType.Headman)); break;
                case "lord": n = Living(HolderOf(RoleType.Lord)); break;
                case "leader": n = Living(HolderOf(LeaderOf(State.era))); break;
                case "priest": n = Living(HolderOf(RoleType.Priest)); break;
                case "merchant": n = Living(HolderOf(RoleType.Merchant)); break;
                case "captain": n = Living(HolderOf(RoleType.Captain)); break;
                case "farmer": n = State.notables.Where(x => x.alive && x.job == JobType.Farmer).OrderBy(x => x.id).FirstOrDefault(); break;
                case "elder": n = State.notables.Where(x => x.alive && x.age >= Config.elderAge).OrderByDescending(x => x.age).ThenBy(x => x.id).FirstOrDefault(); break;
                case "scholar": n = Scholars.FirstOrDefault(); break;
                case "lens maker": n = Living(FindNotable(FindRelic(RelicId.Lens)?.ownerId ?? -1)) ?? Scholars.FirstOrDefault(); break;
                case "owner":
                    var s = FindRelic(RelicId.LeftSandal);
                    n = s == null ? null : Living(FindNotable(s.holderId)) ?? Living(FindNotable(s.ownerId));
                    break;
                default: n = who.StartsWith("the ") ? WithTrait(who) : null; break;
            }
            if (n == null) return false;
            id = n.id;
            return true;
        }

        // ---------- the tap-back (Choice 9 ★) ----------
        static readonly string[] Counts = { "no", "once", "twice", "three", "four", "five", "six", "seven", "eight", "nine", "ten" };

        /// <summary>
        /// The Keeper tapped the glass <paramref name="taps"/> times. In stage 3's later half (the Lens is up) a scholar taps
        /// back the same number of times: returns their id (the view plays it), or -1. The first tap-back makes a card.
        /// </summary>
        public int KeeperTapped(int taps) => TapBack(taps, false);

        int TapBack(int taps, bool force)
        {
            if (!RelicsOn || taps <= 0) return -1;
            if (!force && (BeliefStage < 3 || BeliefHalfNow != BeliefHalf.Later)) return -1;
            var scholar = Scholars.FirstOrDefault() ?? (force ? FirstAdult() : null);
            if (scholar == null) return -1;
            if (!State.storyFlags.Contains(FlagTapBack))
            {
                State.storyFlags.Add(FlagTapBack);
                string n = taps < Counts.Length ? Counts[taps] : taps.ToString();
                string text = taps == 1 ? $"The Keeper knocked once. {scholar.FullName} knocked once back and wrote it down."
                    : taps == 2 ? $"The Keeper knocked twice. {scholar.FullName} knocked twice back and wrote it down."
                    : LineBank.First(l => l.no == 39).text.Replace("{0}", scholar.FullName).Replace("{1}", n);
                RelicLine("tapback", text, 0.6f, scholar.id, -1);
            }
            return scholar.id;
        }

        // ---------- the era moment's pick ----------
        /// <summary>Carry a found relic to the next box (the era moment's pick of 3; #86's CompleteBox calls this). False if
        /// it isn't found.</summary>
        public bool TakeRelic(string id)
        {
            var r = FindRelic(id);
            if (r == null || !r.Found) return false;
            if (r.takenMonth < 0) r.takenMonth = State.month;
            return true;
        }

        /// <summary>The relics pre-ticked for the next box (Choice 6 ★: Sky Crumb, Keeper Doll, Lens), those found only.</summary>
        public List<string> DefaultRelicPicks() => RelicId.DefaultCarried.Where(HasRelic).ToList();

        // ---------- debug (the debug panel) ----------
        /// <summary>Debug: make this relic now, whatever its trigger says. The Sandal is lost first if it hasn't been.</summary>
        public Relic DebugGiveRelic(string id)
        {
            if (HasRelic(id)) return FindRelic(id);
            bool on = RelicsOn;
            RelicsOn = true;
            try
            {
                switch (id)
                {
                    case RelicId.SkyCrumb: return MakeSkyCrumb(State.nudges.LastOrDefault());
                    case RelicId.FestivalMask: return MakeMask();
                    case RelicId.KeeperDoll: return MakeDoll();
                    case RelicId.Lens: return MakeLens(null, "debug");
                    case RelicId.LeftSandal:
                        var s = FindRelic(id) ?? DebugLoseSandal();
                        if (s == null) return null;
                        FindSandal(s);
                        return s;
                }
                return null;
            }
            finally { RelicsOn = on; }
        }

        /// <summary>Debug: a notable loses the sandal now (the eldest adult), if no sandal story exists yet.</summary>
        public Relic DebugLoseSandal()
        {
            var existing = FindRelic(RelicId.LeftSandal);
            if (existing != null) return existing;
            var n = State.notables.Where(x => x.alive && x.age >= AdultAge).OrderByDescending(x => x.age).ThenBy(x => x.id).FirstOrDefault();
            if (n == null) return null;
            var e = RelicLine("lost", $"{n.FullName} lost a sandal crossing the stream. {n.FullName} has walked a little lopsided, with great dignity, ever since.", 0.6f, n.id, -1);
            SandalLost(n, e);
            return FindRelic(RelicId.LeftSandal);
        }

        /// <summary>Debug: the lost sandal has been lost <paramref name="years"/> years longer (20 = findable at the next prune).</summary>
        public bool DebugSkipSandalYears(int years = 20)
        {
            var s = FindRelic(RelicId.LeftSandal);
            if (s == null || s.Found) return false;
            s.lostMonth -= (long)years * SimConfig.MonthsPerYear;
            return true;
        }

        /// <summary>Debug: set the belief stage (1–3, never below the era's) and half. The later half sets the stage's relic
        /// flag (not the relic itself); the early half clears it.</summary>
        public void DebugSetBelief(int stage, bool later)
        {
            DebugSetGlassStage(stage);
            string relic = GlassRelic.OfStage(GlassStage);
            if (relic == null) return;
            if (later) GlassRelicFound(relic);
            else State.storyFlags.Remove("relic:" + relic);
        }

        /// <summary>Debug: a scholar (or any adult) taps back now, whatever the stage. Returns who, or -1.</summary>
        public int DebugTapBack(int taps = 3) => TapBack(taps, true);
    }
}
