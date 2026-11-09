using System;
using System.Collections.Generic;
using System.Linq;
using AquariumCiv.Sim.Eco;
using AquariumCiv.Sim.Settle;

namespace AquariumCiv.Sim
{
    /// <summary>
    /// #437 / #438: the wolves' and the dogs' choices (placed-life spec §5.6, research 19 §6). They ride the small-choice machinery
    /// (SimCore.SmallChoices.cs: one open at a time, the gap, three visits, "Let them decide", the card and the choice scene), raised from
    /// the box's own moment, never while the Keeper is away (the dilemma machinery holds them), never resolved unseen. A waiting life
    /// choice takes the next small slot even when a big one would come next (<see cref="SimCore.LifeChoiceWaiting"/>); the seasonal
    /// ones (the thief, the pup) don't even wait for the gap (<see cref="SimCore.SeasonalLifeWaiting"/>, user 2026-11-08). Option 0 / 1:
    /// <list type="bullet">
    /// <item><see cref="Thief"/> (6.1, after the first theft): chase them off / leave them the scraps. Let them decide: a bold chief shares.</item>
    /// <item><see cref="Pup"/> (6.2, the lost pup; spring): bring it home / leave it with its mother. {child} is the one who found it.</item>
    /// <item><see cref="Kill"/> (6.3, ravens over the wood): take their meat / leave it to them.</item>
    /// <item><see cref="Pen"/> (6.4, the duck pen, once, only with a dog): a thorn fence / the dogs keep watch.</item>
    /// </list>
    /// The white wolf (6.5) is a pure moment, not a choice (<see cref="MomentKind.WhiteWolf"/>).
    /// </summary>
    public static class LifeChoices
    {
        public const string Thief = "wolf_thief", Pup = "lost_pup", Kill = "wolf_kill", Pen = "duck_pen";
        /// <summary>P9 #427 (B14.b, spec D5): the white stag, once per box: hunt it / let it be.</summary>
        public const string WhiteStag = "white_stag";

        static DilemmaDef Def(string kind, string question, string shortAsk, string a, string aHint, string b, string bHint) => new DilemmaDef
        {
            id = "small_" + kind, kind = DilemmaKind.WhichWay, size = DilemmaSize.Small, smallKind = kind, cast = DilemmaCast.None,
            minEra = Era.Camp, maxEra = Era.Town, question = question, shortAsk = shortAsk, letDone = "The people decided {n}.",
            options = new[]
            {
                new DilemmaOptionDef { label = a, hint = aHint, done = "The Keeper chose " + a + "." },
                new DilemmaOptionDef { label = b, hint = bHint, done = "The Keeper chose " + b + "." },
            },
        };

        public static readonly DilemmaDef ThiefDef = Def(Thief,
            "The wolves took meat from the rack again. Chase them off, or leave them the scraps?", "The wolves at the meat rack.",
            "to chase them off", "Thorns round the rack and a torch at night. The wolves keep to the far wood.",
            "to leave them the scraps", "A pile of bones at the camp's edge. One of them may come closer.");

        public static readonly DilemmaDef PupDef = Def(Pup,
            "{child} found a wolf pup alone by the den. Bring it home, or leave it with its mother?", "A lost wolf pup.",
            "to bring it home", "{child} carries it to the fire. It will grow up with the people.",
            "to leave it with its mother", "{child} sets it down, and it runs back to the den.");

        public static readonly DilemmaDef KillDef = Def(Kill,
            "The wolves have killed in the wood, and the ravens are circling. Take their meat, or leave it to them?", "The wolves' kill.",
            "to take their meat", "The hunters shout them off it. Meat for the camp, and the wolves keep away a while.",
            "to leave it to them", "The hunters walk home. The wolves eat, and grow a little bolder.");

        public static readonly DilemmaDef PenDef = Def(Pen,
            "The wolves circled the duck pen last night. A thorn fence, or let the dogs keep watch?", "Wolves at the duck pen.",
            "a thorn fence", "A ring of thorny branches round the pen, and a torch at night.",
            "the dogs keeping watch", "A dog sleeps by the pen gate, and barks when the wolves come.");

        /// <summary>#427: the words are [est] until the user signs them off (the issue's own question).</summary>
        public static readonly DilemmaDef WhiteStagDef = Def(WhiteStag,
            "The hunters saw the white stag. Hunt it, or let it be?", "The white stag.",
            "to hunt it", "A feast that night, and its great white antlers over the chief's door.",
            "to let it be", "The hunters lower their bows. It may come back at dawn, and the people will tell of it.");

        public static readonly DilemmaDef[] All = { ThiefDef, PupDef, KillDef, PenDef, WhiteStagDef };

        public static bool Is(DilemmaDef def) => def?.smallKind != null && Array.IndexOf(new[] { Thief, Pup, Kill, Pen, WhiteStag }, def.smallKind) >= 0;

        /// <summary>#427 "Let them decide" for the white stag: these chiefs let it be (the gentle and the dreaming); the rest hunt it.</summary>
        public static readonly string[] LetBeTraits = { "the kind", "the dreamer", "the singer" };

        /// <summary>"Let them decide" leans on the chief's trait: these share and take chances (the scraps, the pup, the kill, the dogs).</summary>
        public static readonly string[] BoldTraits = { "the brave", "the kind", "the dreamer", "the joker", "the singer", "the loud" };
    }

    /// <summary>
    /// #437 the wolf pack, the sim side the view (#439-#447) and the choices read (docs/design/placed-life.md §5.6; research 19 §3-§6).
    /// The Keeper places a pair at a den in the far wood (SimCore.LifeJar); the pack lives in EcoModel.Wolves.cs (2-6, pups in spring,
    /// young leave in autumn, a few kills a year within the floors). Here: boldness and what it sets (eyes at the firelight, a wolf
    /// trailing the hunters, the grey wolf's approach), the meat thief (at most once a season, only while watched, never when food would
    /// go short), the kill log the ravens circle and choice 6.3, the duck pen's circling (#431 / #446 hooks), the white wolf moment,
    /// and the planner-side rule (<see cref="WolfRules"/>: no wolf inside the tent ring, none within 0.3 u of a person).
    /// With the ecology off none of this exists.
    /// </summary>
    public partial class SimCore
    {
        /// <summary>A kill in the wood (the ravens circle it; B20). Raised after the land's step.</summary>
        public event Action<WolfKill> WolfKilled;
        /// <summary>A wolf stole from the rack while the Keeper watched (B22.a): a strip of meat, or a hide (true).</summary>
        public event Action<bool> WolfTheft;
        /// <summary>The white wolf howled on the ridge (B21.d; a held moment, a book line).</summary>
        public event Action WhiteWolfHowled;
        /// <summary>The pack circled the duck pen one night (B26); the guard choice follows when there is a dog.</summary>
        public event Action WolvesCircledPen;

        // ---------- the pack ----------
        public bool WolvesPlaced => EcologyOn && EcoM.WolvesPlaced;
        public int EcoWolfCount => EcologyOn ? State.eco.wolves : 0;
        /// <summary>This spring's pups (they play at the den), and last spring's young (they leave at the box's edge in autumn).</summary>
        public int EcoWolfPups => EcologyOn ? State.eco.wolfPups : 0;
        public int EcoWolfYearlings => EcologyOn ? State.eco.wolfYearlings : 0;
        /// <summary>The den's site cell (-1 none).</summary>
        public int WolfDen => EcologyOn ? State.eco.wolfDen : -1;
        /// <summary>B12.f: the wolves are placed and their first howl hasn't played yet (it plays at the next dusk; saved).</summary>
        public bool WolfFirstHowlDue => WolvesPlaced && !State.eco.firstHowlHeard;
        /// <summary>The first howl played: true the first time.</summary>
        public bool AckFirstHowl() { if (!WolfFirstHowlDue) return false; State.eco.firstHowlHeard = true; return true; }
        /// <summary>Young wolves who left the box at its edge: the last time (game month, -1 never) and how many then; and all so far.</summary>
        public long WolvesLeftMonth => EcologyOn ? State.eco.wolvesLeftMonth : -1;
        public int WolvesLeftLast => EcologyOn ? State.eco.wolvesLeftLast : 0;
        public int WolvesLeftTotal => EcologyOn ? State.eco.wolvesLeft : 0;
        public int WolfChases => EcologyOn ? State.eco.wolfChases : 0;
        public int WolfKillCount => EcologyOn ? State.eco.wolfKills : 0;
        /// <summary>The last chase (game month, -1 none) and whether it ended in a kill (B20 plays "mostly they get away").</summary>
        public long WolfChaseMonth => EcologyOn ? State.eco.wolfChaseMonth : -1;
        public bool WolfChaseCaught => EcologyOn && State.eco.wolfChaseCaught;

        /// <summary>0..1, starts 0.2 (spec §5.6): up with scraps and kills left to them; down with a chase, a kill taken, the dogs on guard.</summary>
        public float WolfBoldness => EcologyOn ? State.eco.wolfBold : 0f;
        /// <summary>Choice 6.1's answer (None until the people chose).</summary>
        public WolfWay WolfWayTaken => EcologyOn ? (WolfWay)State.eco.wolfWay : WolfWay.None;
        /// <summary>How near the fire a wolf may come now (box units): 2.0 for a shy pack, never nearer than the firelight's edge (1.0).</summary>
        public float WolfMinFromFire => WolfRules.MinFromFire(WolfBoldness);

        /// <summary>The chance the eyes show at the firelight on a night (B21.b): more as the pack grows bold; none for a season after a
        /// chase or a kill taken from them.</summary>
        public float WolfEyesChance => !WolvesPlaced || State.month < State.eco.wolfQuietUntil ? 0f : 0.1f + 0.6f * WolfBoldness;
        /// <summary>Do the eyes show at the firelight on view day <paramref name="day"/>'s night?</summary>
        public bool WolfEyesTonight(long day) => WolvesPlaced && WolfPack.EyesTonight(State.eco.seed, day, WolfEyesChance);

        /// <summary>A wolf trails the deer hunters (B23.a): the pack is bold enough.</summary>
        public bool WolfTrailsHunters => WolvesPlaced && WolfBoldness >= 0.35f && State.month >= State.eco.wolfQuietUntil;
        /// <summary>How far behind the hunters the trailing wolf keeps: 1.0 u, closing to 0.6 u on the scraps path as it grows bold (always
        /// at least <see cref="WolfRules.PersonGap"/> from anyone: the planner keeps it).</summary>
        public float WolfTrailDistance => WolfWayTaken == WolfWay.Scraps ? 1.0f - 0.4f * Math.Clamp((WolfBoldness - 0.35f) / 0.65f, 0f, 1f) : 1.0f;

        /// <summary>The scraps path: the grey wolf comes to the bone spot at dusk (B22.c).</summary>
        public bool GreyWolfAtBoneSpot => WolvesPlaced && WolfWayTaken == WolfWay.Scraps && !MeatThiefStoppedByDogs;
        /// <summary>How far from the fire the grey wolf lies by the bones: 1.3 u at first, nearer over a game year as it grows bold, never
        /// nearer than the firelight's edge (1.0 u).</summary>
        public float GreyWolfFromFire
        {
            get
            {
                if (!GreyWolfAtBoneSpot) return 1.3f;
                float year = Math.Clamp((State.month - State.eco.wolfWayMonth) / 12f, 0f, 1f);
                return Math.Max(WolfRules.FirelightEdge, 1.3f - 0.3f * year * Math.Clamp(WolfBoldness / 0.6f, 0f, 1f));
            }
        }

        /// <summary>The fire and the tent ring as they stand now (every building but the fire, plus 0.25 u).</summary>
        public WolfGround WolfGroundNow()
        {
            var g = EcoGround;
            return new WolfGround(g.campX, g.campZ, State.buildings);
        }

        float DenX => SiteMap.CXc(Math.Max(0, State.eco.wolfDen));
        float DenZ => SiteMap.CZc(Math.Max(0, State.eco.wolfDen));

        /// <summary>The bone spot (B22.b, the scraps): at the camp's edge toward the den, at least 1.1 u from the fire, outside the tent ring.
        /// False with no wolves.</summary>
        public bool WolfBoneSpot(out float x, out float z)
        {
            x = z = 0f;
            if (!WolvesPlaced) return false;
            return WolfRules.EdgeSpot(WolfGroundNow(), DenX, DenZ, 1.1f, out x, out z);
        }

        /// <summary>Where the grey wolf lies by the bones now (<see cref="GreyWolfFromFire"/> out on the den's ray, a little to the side),
        /// kept off the tent ring and away from <paramref name="people"/> (x, z pairs; null none). False when no spot is safe: it stays in
        /// the trees.</summary>
        public bool GreyWolfSpot(IReadOnlyList<float> people, out float x, out float z)
        {
            x = z = 0f;
            if (!GreyWolfAtBoneSpot) return false;
            var g = WolfGroundNow();
            float a = MathF.Atan2(DenZ - g.fireZ, DenX - g.fireX) + 0.15f;
            WolfRules.EdgeSpot(g, g.fireX + MathF.Cos(a), g.fireZ + MathF.Sin(a), GreyWolfFromFire, out x, out z);
            return WolfRules.Keep(ref x, ref z, DenX, DenZ, g, people, WolfRules.FirelightEdge);
        }

        /// <summary>Where the drying rack stands (B16.c, B22.a: at the camp's edge, outside the tent ring, so the thief reaches it without
        /// crossing the ring): on the den's side, turned off the bone spot's ray, at the firelight's edge. False with no wolves.</summary>
        public bool WolfRackSpot(out float x, out float z)
        {
            x = z = 0f;
            if (!WolvesPlaced) return false;
            var g = WolfGroundNow();
            float a = MathF.Atan2(DenZ - g.fireZ, DenX - g.fireX) - 0.6f;
            return WolfRules.EdgeSpot(g, g.fireX + MathF.Cos(a), g.fireZ + MathF.Sin(a), WolfRules.FirelightEdge, out x, out z);
        }

        /// <summary>The cells no wolf goes (a kill never falls there): the settlement's lots, paths and green, and the tent ring.</summary>
        Func<int, bool> WolfBlockedCell()
        {
            var g = WolfGroundNow();
            return c => SettledCell(c) || WolfRules.InTentRing(SiteMap.CXc(c), SiteMap.CZc(c), g.ring);
        }

        /// <summary>Is a planned wolf point allowed now (the planner's check, B21-B23, B26): on the land, outside the tent ring, at least the
        /// boldness's distance from the fire, at least 0.3 u from every person in <paramref name="people"/> (x, z pairs).</summary>
        public bool WolfPointOk(float x, float z, IReadOnlyList<float> people) => WolfRules.Allowed(x, z, WolfGroundNow(), people, WolfMinFromFire);

        /// <summary>Rebuilds the view's <see cref="WolfPack.Field"/> (at start, each season, and when the pack, boldness or the buildings change).</summary>
        public void EcoWolfRefresh(WolfPack.Field field)
        {
            if (!EcologyOn) return;
            var e = State.eco;
            field.Refresh(e.cover, e.seed, WolvesPlaced ? e.wolfDen : -1, e.wolves, e.wolfPups, e.wolfBold, WolfEyesChance, WolfGroundNow());
        }

        // ---------- kills and the ravens (B20, B23.b) ----------
        /// <summary>The latest kills, oldest first (the view's ravens; at most EcoModel.WolfKillsKept).</summary>
        public IReadOnlyList<WolfKill> WolfKills => EcologyOn && State.eco.wolfKillLog != null ? State.eco.wolfKillLog : (IReadOnlyList<WolfKill>)Array.Empty<WolfKill>();
        public WolfKill LastWolfKill => WolfKills.Count > 0 ? WolfKills[WolfKills.Count - 1] : null;
        /// <summary>The kill the ravens circle now: the newest whose meat is still open (null none).</summary>
        public WolfKill RavensKill => EcologyOn ? EcoM.OpenKill() : null;

        /// <summary>A kill is open this long (game months) before the wolves have simply eaten it (unless choice 6.3 is asking about it).</summary>
        public const int KillOpenMonths = 3;

        // ---------- the deer's human shield (research 19 §1) ----------
        /// <summary>0..1: with the pack in the box the deer graze nearer the camp (<see cref="EcoDeerHome"/> already moved; 0 without wolves).</summary>
        public float EcoDeerShield => EcologyOn ? EcoM.DeerShield : 0f;

        // ---------- the meat thief (B22.a; choice 6.1) ----------
        /// <summary>A theft the view has not played yet (it plays at dusk, then <see cref="AckWolfTheft"/>).</summary>
        public bool WolfTheftScenePending => EcologyOn && !State.eco.theftSeen;
        public bool AckWolfTheft() { if (!WolfTheftScenePending) return false; State.eco.theftSeen = true; return true; }
        public int WolfThefts => EcologyOn ? State.eco.thefts : 0;
        public long WolfTheftMonth => EcologyOn ? State.eco.theftMonth : -1;
        /// <summary>The last theft took a hide (else a strip of meat).</summary>
        public bool WolfTheftWasHide => EcologyOn && State.eco.theftHide;
        /// <summary>No more thefts: the thorns round the rack (the chase path), or a dog on guard (#438).</summary>
        public bool MeatThiefStopped => EcologyOn && (State.eco.wolfWay == (int)WolfWay.Chase || State.eco.dogGuardMonth >= 0);
        bool MeatThiefStoppedByDogs => EcologyOn && State.eco.dogGuardMonth >= 0;

        /// <summary>The thief at most once a season (seasons as the land's: spring is months 2-4), only while the Keeper watches (never a
        /// "you lost" while away), never when the people's food would drop below what they need, never once stopped. A strip of meat, or
        /// now and then a hide when there are a few drying.</summary>
        void WolfThiefMonth(long month)
        {
            var e = State.eco; var t = Config.eco;
            if (!WolvesPlaced || CatchingUp || MeatThiefStopped) return;
            if (e.theftMonth >= 0 && (e.theftMonth + 1) / 3 == (month + 1) / 3) return;
            float chance = (t.wolfThiefBase + t.wolfThiefBold * e.wolfBold) / 3f;
            if (Terrain.Hash3(e.seed * 7919 + 66 * 977, 0, (int)(month & 0x7fffffff)) >= chance) return;
            bool hide = e.hidesDrying >= 3 && Terrain.Hash3(e.seed * 7919 + 67 * 977, 0, (int)(month & 0x7fffffff)) < 0.3f;
            if (!hide && State.food - t.wolfThiefStrip < t.wolfThiefFood * State.population) return;
            if (hide) e.hidesDrying--; else State.food -= t.wolfThiefStrip;
            e.thefts++; e.theftMonth = month; e.theftHide = hide; e.theftSeen = false;
            Chronicle("wolf_thief", hide
                ? "At dusk a wolf crept to the rack and ran off with a hide in its teeth. The people shouted and waved a torch after it, then laughed."
                : "At dusk a wolf crept to the drying rack and snatched a strip of meat. The people jumped up shouting and waving, and it trotted off with it, tail up.", 0.5f);
            WolfTheft?.Invoke(hide);
        }

        // ---------- the month of the pack ----------
        /// <summary>Every month with the ecology on (SimCore.Eco.cs EcoStep): the pack's chase, the kills the wolves have eaten, the thief,
        /// boldness's drift, the pen, and the dogs (SimCore.Dogs.cs).</summary>
        void WolvesMonth(int season)
        {
            var e = State.eco; var t = Config.eco; long month = State.month;
            if (WolvesPlaced)
            {
                EcoM.WolfMonth(season, month, WolfBlockedCell());
                var log = e.wolfKillLog;
                if (log != null)
                    foreach (var k in log)
                        if (k.eatenBy == (int)KillEaten.Open && !k.asked && month - k.month >= KillOpenMonths) k.eatenBy = (int)KillEaten.Wolves;
                if (e.wolfWay == (int)WolfWay.Scraps) e.wolfBold += t.boldScrapsMonth;
                if (GrownDogs > 0) e.wolfBold -= t.boldDogMonth;
                e.wolfBold = Math.Clamp(e.wolfBold, 0f, 1f);
                WolfThiefMonth(month);
                PenMonth(month);
            }
            DogsMonth(season);
            WolvesTell();
        }

        /// <summary>Raises the kills and the white wolf the land made (from the month and the season steps).</summary>
        void WolvesTell()
        {
            if (!EcologyOn) return;
            var m = EcoM;
            if (m.newKills.Count > 0)
            {
                var kills = m.newKills.ToArray();
                m.newKills.Clear();
                foreach (var k in kills)
                {
                    if (State.eco.wolfKills == 1 && !CatchingUp)
                        Chronicle("wolf_kill", "Black birds circled over the wood all afternoon. The hunters said the wolves had killed there.", 0.6f);
                    WolfKilled?.Invoke(k);
                }
            }
            if (m.newWhiteWolf)
            {
                m.newWhiteWolf = false;
                Chronicle(WhiteWolfKind, "At dusk a white wolf stood on the high ground at the edge of the wood and howled, and the pack answered below. Everyone came out to see it. Nobody spoke.", 0.9f);
                WhiteWolfHowled?.Invoke();
            }
        }

        /// <summary>The chronicle kind of the white wolf's line: it queues a <see cref="MomentKind.WhiteWolf"/> moment (a pure moment, no choice).</summary>
        public const string WhiteWolfKind = "white_wolf";
        public long WhiteWolfMonth => EcologyOn ? State.eco.whiteWolfMonth : -1;
        public bool WhiteWolfSeen => WhiteWolfMonth >= 0;

        // ---------- the duck pen (#431 builds it; #446 stages the night and the choice) ----------
        /// <summary>#431's hook: the duck pen stands now (the pack will circle it once, on a night soon after).</summary>
        public bool DuckPenBuilt { get => EcologyOn && State.eco.duckPen; set { if (EcologyOn) State.eco.duckPen = value; } }
        /// <summary>#431's pen ducks (apart from the wild flock's floor and cap). Wolves may take them only until the pen is guarded.</summary>
        public int PenDucks { get => EcologyOn ? State.eco.penDucks : 0; set { if (EcologyOn) State.eco.penDucks = Math.Max(0, value); } }
        public PenGuard DuckPenGuard => EcologyOn ? (PenGuard)State.eco.penGuard : PenGuard.None;
        public long PenCircledMonth => EcologyOn ? State.eco.penCircledMonth : -1;
        public bool PenOpenToWolves => EcologyOn && EcoM.PenOpenToWolves;

        /// <summary>Once, the first month after the pen stands with wolves in the box: the pack circles it at night (nothing taken). With a
        /// dog the guard choice (6.4) waits for the Keeper; without one there is nothing to choose, and the people weave a thorn fence.</summary>
        void PenMonth(long month)
        {
            var e = State.eco;
            if (!e.duckPen || e.penCircledMonth >= 0) return;
            e.penCircledMonth = month;
            WolvesCircledPen?.Invoke();
            if (GrownDogs == 0) SetPenGuard(PenGuard.Fence, false);
        }

        void SetPenGuard(PenGuard g, bool keeper)
        {
            var e = State.eco;
            if (e.penGuard != (int)PenGuard.None) return;
            e.penGuard = (int)g;
            if (keeper) return; // the choice wrote its own line
            Chronicle("duck_pen_fence", "The wolves circled the duck pen in the night. In the morning the people wove a fence of thorny branches round it.", 0.55f);
        }

        // ---------- the choices (6.1, 6.3, 6.4 here; 6.2 in SimCore.Dogs.cs) ----------
        /// <summary>The life choices the sim draws from (tests can swap it).</summary>
        public IReadOnlyList<DilemmaDef> LifeChoiceCatalog { get; set; } = LifeChoices.All;

        /// <summary>A life choice's moment exists and no choice is open: it takes the next small slot once the gap has passed.</summary>
        public bool LifeChoiceWaiting => Config.smallChoices && EligibleLife().Count > 0;

        /// <summary>User 2026-11-08: the seasonal life choices (the meat thief, the lost pup) are raised when they happen, without the
        /// small-choice gap or waiting for the next small slot. Still one choice at a time, only while the Keeper watches or on an open,
        /// never resolved unseen. The kill and the pen keep the normal pacing.</summary>
        public static bool IsSeasonalLife(DilemmaDef def) => def?.smallKind == LifeChoices.Thief || def?.smallKind == LifeChoices.Pup
            || def?.smallKind == LifeChoices.WhiteStag; // #427: the white stag stands at the wood's edge now: it is asked now

        /// <summary>A seasonal life choice (thief, pup) has its moment and isn't open: it is raised at the next watched month or open, ahead
        /// of the gap (it still waits for an open choice or wish to end).</summary>
        public bool SeasonalLifeWaiting => Config.smallChoices && SeasonalLifeDef() != null;

        /// <summary>The first seasonal life choice (in the catalog's order: thief, pup) whose moment is there now; null none.</summary>
        DilemmaDef SeasonalLifeDef()
        {
            if (!EcologyOn || LifeChoiceCatalog == null) return null;
            foreach (var def in LifeChoiceCatalog)
                if (IsSeasonalLife(def) && LifeWhyNot(def) == null) return def;
            return null;
        }

        List<(DilemmaDef def, int a, int b)> EligibleLife()
        {
            var list = new List<(DilemmaDef def, int a, int b)>();
            if (!EcologyOn || LifeChoiceCatalog == null) return list;
            foreach (var def in LifeChoiceCatalog)
                if (LifeWhyNot(def) == null) list.Add((def, -1, -1));
            return list;
        }

        bool LifeAsked(DilemmaDef def) => State.dilemmas.Any(d => d.defId == def.id);
        bool LifeOpen(DilemmaDef def) => State.dilemmas.Any(d => d.defId == def.id && d.state == DilemmaState.Open);

        /// <summary>Why this life choice can't be raised now (null = it can).</summary>
        string LifeWhyNot(DilemmaDef def)
        {
            if (!EcologyOn) return "no ecology";
            if (LifeOpen(def)) return "open now";
            var e = State.eco;
            switch (def.smallKind)
            {
                case LifeChoices.Thief:
                    if (e.thefts == 0) return "no theft yet";
                    return e.wolfWay != (int)WolfWay.None || LifeAsked(def) ? "asked already" : null;
                case LifeChoices.Kill:
                {
                    var k = EcoM.OpenKill();
                    if (k == null || k.asked) return "no fresh kill";
                    return State.month - k.month > KillOpenMonths ? "the kill is old" : null;
                }
                case LifeChoices.Pen:
                    if (e.penCircledMonth < 0) return "the wolves have not circled the pen";
                    if (e.penGuard != (int)PenGuard.None || LifeAsked(def)) return "the pen is guarded";
                    return GrownDogs == 0 ? "no dog to keep watch" : null;
                case LifeChoices.Pup:
                    return PupWhyNot();
                case LifeChoices.WhiteStag:
                    return WhiteStagChoiceDue && !LifeAsked(def) ? null : "no white stag waiting";
            }
            return "unknown life choice";
        }

        /// <summary>Fill the card's words once raised (the pup's finder; the kill it asks about).</summary>
        void PrepareLife(Dilemma d, DilemmaDef def)
        {
            if (def.smallKind == LifeChoices.Pup) PreparePup(d);
            else if (def.smallKind == LifeChoices.Kill) { var k = EcoM.OpenKill(); if (k != null) k.asked = true; }
        }

        /// <summary>What the pick does (the Keeper's, "Let them decide", or the people's after three visits).</summary>
        void ApplyLife(Dilemma d, DilemmaDef def, int option)
        {
            var e = State.eco; var t = Config.eco;
            switch (def.smallKind)
            {
                case LifeChoices.Thief:
                    e.wolfWay = option == 0 ? (int)WolfWay.Chase : (int)WolfWay.Scraps; e.wolfWayMonth = State.month;
                    if (option == 0) { e.wolfBold -= t.boldChase; e.wolfQuietUntil = State.month + 3; }
                    else e.wolfBold += t.boldScraps;
                    break;
                case LifeChoices.Kill:
                {
                    var k = e.wolfKillLog?.LastOrDefault(x => x.asked && x.eatenBy == (int)KillEaten.Open);
                    if (k != null) k.eatenBy = option == 0 ? (int)KillEaten.People : (int)KillEaten.Wolves;
                    if (option == 0)
                    {
                        e.wolfBold -= t.boldKillTaken; e.wolfQuietUntil = State.month + 3;
                        if (k != null) State.food = Math.Min(FoodCap, State.food + (k.Prey == LifeSpecies.Deer ? 4f : 1f));
                    }
                    else e.wolfBold += t.boldKillLeft;
                    break;
                }
                case LifeChoices.Pen:
                    SetPenGuard(option == 1 && GrownDogs > 0 ? PenGuard.Dogs : PenGuard.Fence, true);
                    break;
                case LifeChoices.Pup:
                    ApplyPup(d, option);
                    break;
                case LifeChoices.WhiteStag:
                    ResolveWhiteStag(option == 0); // #427: 0 hunt it (the feast, the great antlers), 1 let it be (it shows at dawn)
                    break;
            }
            e.wolfBold = Math.Clamp(e.wolfBold, 0f, 1f);
        }

        /// <summary>"Let them decide" (and the people's own pick after three visits): the chief's trait (a bold chief shares, takes the
        /// pup and the kill, trusts the dogs; a careful one chases, leaves the pup, leaves the kill, builds the fence), else a hash.</summary>
        int LifeDefaultOption(Dilemma d, DilemmaDef def)
        {
            var leader = Alive(SeatOf(LeaderOf(State.era))?.holderId ?? -1) ?? RulingNotable();
            if (leader == null) return (int)(DilemmaRoll(32, d.id, d.month) * 2f) % 2;
            bool bold = Array.IndexOf(LifeChoices.BoldTraits, leader.trait) >= 0;
            return def.smallKind switch
            {
                LifeChoices.Thief => bold ? 1 : 0,
                LifeChoices.Pup => bold ? 0 : 1,
                LifeChoices.Kill => bold ? 0 : 1,
                LifeChoices.Pen => bold && GrownDogs > 0 ? 1 : 0,
                LifeChoices.WhiteStag => Array.IndexOf(LifeChoices.LetBeTraits, leader.trait) >= 0 ? 1 : 0,
                _ => 0,
            };
        }

        // ---------- debug (the panel's "Wolves and dogs" block; CLAUDE.md "debug panel stays current") ----------
        /// <summary>Debug ("ready wolves"): the deer, a wood and the herd's year are made true enough, and the wolves' jar opens now. False
        /// when there is no den spot (the panel then says so) or the ecology is off.</summary>
        public bool DebugReadyWolves()
        {
            if (!EcologyOn) return false;
            var m = EcoM;
            if (LifePlaceCellNearCamp(LifeSpecies.Wolves) < 0) DebugGrowFarWood();
            if (!LifeIsPlaced(LifeSpecies.Deer) && DebugPlaceNearCamp(LifeSpecies.Deer) < 0) return false;
            if (LifePlaceCellNearCamp(LifeSpecies.Wolves) < 0) return false;
            var j = m.Jar(LifeSpecies.Wolves);
            if (j.State == JarState.Placed) return false;
            if (j.State != JarState.Ready) { j.state = (int)JarState.Ready; j.readyMonth = State.month; j.readySeen = false; m.newJars.Add(LifeSpecies.Wolves); }
            EcoTell();
            return true;
        }

        /// <summary>Debug: a far wood (a 5 x 5 block of grown trees, every cell at least 1.6 u from the fire, dry, off the settlement), the
        /// block farthest from the fire that fits. True when one was grown.</summary>
        public bool DebugGrowFarWood()
        {
            if (!EcologyOn) return false;
            var e = State.eco; var g = EcoGround;
            const int W = 5;
            int best = -1; float bd = -1f;
            for (int j = 1; j + W < SiteMap.NZ - 1; j++)
                for (int i = 1; i + W < SiteMap.NX - 1; i++)
                {
                    bool ok = true;
                    for (int dj = 0; dj < W && ok; dj++)
                        for (int di = 0; di < W && ok; di++)
                        {
                            int c = SiteMap.Index(i + di, j + dj);
                            float x = SiteMap.CXc(c), z = SiteMap.CZc(c);
                            ok = !g.InBasin(c) && !g.yard[c] && (EcoCover)e.cover[c] != EcoCover.Water && !SettledCell(c)
                                 && WolfRules.OnLand(x, z) && Terrain.Dist(x, z, g.campX, g.campZ) >= 1.6f;
                        }
                    if (!ok) continue;
                    int mid = SiteMap.Index(i + W / 2, j + W / 2);
                    float d = Terrain.Dist(SiteMap.CXc(mid), SiteMap.CZc(mid), g.campX, g.campZ);
                    if (d > 2.6f || d <= bd) continue;
                    bd = d; best = SiteMap.Index(i, j);
                }
            if (best < 0) return false;
            for (int dj = 0; dj < W; dj++)
                for (int di = 0; di < W; di++) e.cover[SiteMap.Index(SiteMap.I(best) + di, SiteMap.J(best) + dj)] = (byte)EcoCover.Tree;
            EcoM.Count();
            if (Config.settle) Settlement.SyncEco();
            return true;
        }

        /// <summary>Debug ("place wolves near camp's far wood"): places the pair at the den spot nearest the fire (the jar opens first). The
        /// den cell, or -1 (no den spot: grow a wood far from the fire first).</summary>
        public int DebugPlaceWolves()
        {
            if (!EcologyOn || WolvesPlaced) return -1;
            if (LifePlaceCellNearCamp(LifeSpecies.Wolves) < 0) DebugGrowFarWood();
            return DebugPlaceNearCamp(LifeSpecies.Wolves);
        }

        /// <summary>Debug ("wolves bold"): boldness to 1 (eyes at the firelight's edge every night, the trailing wolf at its nearest).</summary>
        public bool DebugWolvesBold()
        {
            if (!WolvesPlaced) return false;
            State.eco.wolfBold = 1f; State.eco.wolfQuietUntil = -1;
            return true;
        }

        /// <summary>Debug ("meat thief now"): a theft now, whatever the season and the food (a strip, or a hide when no food), and the view
        /// plays it. Not when the thief is stopped (the thorns, a dog on guard). False without wolves.</summary>
        public bool DebugMeatThiefNow()
        {
            if (!WolvesPlaced || MeatThiefStopped) return false;
            var e = State.eco;
            bool hide = State.food < Config.eco.wolfThiefStrip && e.hidesDrying > 0;
            if (hide) e.hidesDrying--; else State.food = Math.Max(0f, State.food - Config.eco.wolfThiefStrip);
            e.thefts++; e.theftMonth = State.month; e.theftHide = hide; e.theftSeen = false;
            Chronicle("wolf_thief", "At dusk a wolf crept to the drying rack and snatched a strip of meat. The people jumped up shouting and waving, and it trotted off with it, tail up.", 0.5f);
            WolfTheft?.Invoke(hide);
            return true;
        }

        /// <summary>Debug ("wolves' kill now"): a chase that ends in a kill now (the floors hold: none when no prey has one to spare). The
        /// kill, or null.</summary>
        public WolfKill DebugWolfKillNow()
        {
            if (!WolvesPlaced) return null;
            WolfKill k = null;
            var blocked = WolfBlockedCell();
            for (int r = 0; r < 4 && k == null; r++) k = EcoM.Chase(State.month, true, r * 0.27f, blocked);
            WolvesTell();
            return k;
        }

        /// <summary>Debug ("white wolf now"): the white wolf howls now (once per box). False without wolves or when it already has.</summary>
        public bool DebugWhiteWolfNow()
        {
            if (!WolvesPlaced || !EcoM.RaiseWhiteWolf(State.month)) return false;
            WolvesTell();
            return true;
        }

        /// <summary>Debug ("wolves at the pen now"): a pen stands (the debug pen, until #431 builds one) and the pack circles it now. False
        /// without wolves or when it has already circled.</summary>
        public bool DebugWolvesAtPenNow()
        {
            if (!WolvesPlaced || State.eco.penCircledMonth >= 0) return false;
            State.eco.duckPen = true;
            PenMonth(State.month);
            return true;
        }

        /// <summary>Debug: raise one life choice now (by kind, LifeChoices.Thief...): the open choice lapses and the open wish closes first.
        /// Null when its moment isn't there (no theft, no kill, no pup, no pen).</summary>
        public Dilemma DebugRaiseLifeChoice(string kind)
        {
            var def = LifeChoiceCatalog.FirstOrDefault(x => x.smallKind == kind);
            if (def == null || !Config.dilemmas) return null;
            var open = OpenDilemma;
            if (open != null) LapseDilemma(open);
            var w = OpenWish;
            if (w != null) CloseWish(w, WishState.Lapsed);
            if (LifeWhyNot(def) != null) return null;
            var d = RaiseDilemma(def, -1, -1);
            choiceStageFor = d.id;
            return d;
        }
    }
}
