using System;
using System.Collections.Generic;
using System.Linq;

namespace AquariumCiv.Sim
{
    public enum DilemmaState { Open, Answered, Lapsed }

    /// <summary>Where the player answered: by touching one of the two people or places in the box, or on the card's two buttons.</summary>
    public enum DilemmaPath { Box, Card }

    /// <summary>One dilemma raised in a box (P7, #220): which def, when, who it points at, how it ended. Saved with the box.</summary>
    [Serializable]
    public class Dilemma
    {
        public int id;
        public string defId = "";
        public long month;                  // raised
        public long endMonth = -1;          // answered or lapsed (-1 = open)
        public DilemmaState state;
        public int option = -1;             // the option chosen (answered) or picked by the people (lapsed); -1 while open
        public DilemmaPath path;            // how it was answered (meaningful when state is Answered)
        public int personA = -1, personB = -1;       // the two people the options point at (-1 = none)
        public int buildingA = -1, buildingB = -1;   // the place each option points at (-1 = none)
        public int entryId = -1;            // its chronicle entry (kind "dilemma"): the card
        public int answerEntryId = -1;      // the Keeper line an answer wrote (kind "keeper"); -1 for a lapse
        public int resultEntryId = -1;      // the line a lapse wrote (kind "dilemma_result": a story card, not the question)

        /// <summary>The person option <paramref name="i"/> points at (-1 = none).</summary>
        public int PersonOf(int i) => i == 0 ? personA : i == 1 ? personB : -1;
        public int BuildingOf(int i) => i == 0 ? buildingA : i == 1 ? buildingB : -1;
    }

    /// <summary>A lean an answer left on the box, like a nudge's (SimCore.Nudges.cs) but without a shaping behind it.</summary>
    [Serializable]
    public class DilemmaLean
    {
        public int lean;                    // (int)SimCore.Lean
        public long fromMonth, untilMonth;
        public int dilemmaId;
    }

    /// <summary>What the view reads for one option: the words and the person or place it points at.</summary>
    public sealed class DilemmaOptionView
    {
        public int index;
        public string label, hint;
        public int personId = -1;           // the notable it points at (-1 = none)
        public int buildingId = -1;         // the building it points at (-1 = none)
        public string buildingType = "";
    }

    /// <summary>What the view reads for a dilemma: kind, the question, and its two options (#223 card, #224 choice scene).</summary>
    public sealed class DilemmaView
    {
        public Dilemma dilemma;
        public DilemmaDef def;
        public DilemmaKind kind;
        public string question;
        public DilemmaOptionView[] options;
    }

    /// <summary>
    /// The Keeper's dilemmas (P7, #220; docs/design/results/P7.md, research 14 section 2). The people put two proposals to the Keeper:
    /// a card (chronicle kind "dilemma", <see cref="CardKind.Dilemma"/>) raised from a <see cref="DilemmaDef"/>, answered with
    /// <see cref="AnswerDilemma"/> or left to lapse after <see cref="DilemmaLapseMonths"/>, when the people decide. It copies the
    /// wish: open, then answered or lapsed; one open at a time, sharing the wish slot, a gap after each. Everything is opt-in on
    /// <see cref="SimConfig.dilemmas"/> (off: <see cref="DilemmaStep"/> returns at once, no state changes, no dice). With it on,
    /// no dice stream is drawn either (hashes of the seed, ids and month), so the economy's dice never shift.
    /// </summary>
    public partial class SimCore
    {
        public const int DilemmaGapMonths = 8, DilemmaLapseMonths = 24;
        /// <summary>Dilemma entries take their own ids (above the wishes'), so the shared ids never shift.</summary>
        public const int DilemmaIdBase = 1_200_000_000;

        /// <summary>The defs the sim draws from. Tests (and later the content) can swap it; default <see cref="DilemmaDefs.All"/>.</summary>
        public IReadOnlyList<DilemmaDef> DilemmaCatalog { get; set; } = DilemmaDefs.All;

        public Dilemma FindDilemma(int id) => State.dilemmas.FirstOrDefault(d => d.id == id);
        public Dilemma DilemmaOfEntry(int entryId) => State.dilemmas.FirstOrDefault(d => d.entryId == entryId);
        public DilemmaDef DilemmaDefOf(Dilemma d) => d == null ? null : DilemmaCatalog.FirstOrDefault(x => x.id == d.defId);

        /// <summary>The dilemma that is open now (at most one), or null.</summary>
        public Dilemma OpenDilemma => State.dilemmas.FirstOrDefault(d => d.state == DilemmaState.Open);

        /// <summary>A dilemma entry is a card only while its dilemma is open.</summary>
        bool DilemmaCardLive(ChronicleEntry e)
        {
            var d = DilemmaOfEntry(e.id);
            return d == null || d.state == DilemmaState.Open;
        }

        /// <summary>A save from before dilemmas has none of these fields: they start empty (the same as a new world).</summary>
        void EnsureDilemmaState()
        {
            var s = State;
            s.dilemmas ??= new List<Dilemma>();
            s.dilemmaLeans ??= new List<DilemmaLean>();
            s.customs ??= new List<string>();
            if (s.dilemmaNextId < 1) s.dilemmaNextId = 1;
        }

        /// <summary>True when a custom set by an answer is on the box ("burial:stone").</summary>
        public bool HasCustom(string key) => State.customs.Contains(key);

        bool DilemmaLeaning(Lean l)
        {
            var ls = State.dilemmaLeans;
            if (ls.Count == 0) return false;
            foreach (var x in ls) if (x.lean == (int)l && State.month < x.untilMonth) return true;
            return false;
        }

        // ---------- hashes (no dice) ----------
        float DilemmaRoll(int salt, int id, long month)
        {
            uint h = (uint)State.seed * 0x9E3779B1u ^ (uint)id * 0x85EBCA77u ^ (uint)month * 0xC2B2AE3Du ^ (uint)salt * 0x27D4EB2Fu ^ 0x51ED270Bu;
            h ^= h >> 16; h *= 0x7FEB352Du; h ^= h >> 15; h *= 0x846CA68Bu; h ^= h >> 16;
            return (h >> 8) * (1f / 16777216f);
        }

        // ---------- pacing ----------
        long DilemmaLastEnd => State.dilemmas.Count == 0 ? long.MinValue / 2 : State.dilemmas.Max(d => d.endMonth >= 0 ? d.endMonth : d.month);

        bool DilemmaDue => Config.dilemmas && !State.firstRun.active && State.dilemmaNextMonth >= 0 && State.month >= State.dilemmaNextMonth && EligibleDefs().Count > 0;

        /// <summary>While a dilemma is open or due, no new wish starts (the slot is shared).</summary>
        bool DilemmaHoldsSlot => Config.dilemmas && (OpenDilemma != null || DilemmaDue);

        /// <summary>The month the next dilemma is due, counted from <paramref name="from"/>. Normal play: 6 to 10 game years on;
        /// the lab: the first at year 2, then about every 3 years (plus or minus 6 months).</summary>
        long DilemmaScheduleFrom(long from, bool first)
        {
            var c = Config;
            if (c.dilemmaLab)
            {
                if (first) return Math.Max(from, (long)c.dilemmaLabFirstYear * SimConfig.MonthsPerYear);
                int jitter = (int)(DilemmaRoll(21, State.dilemmaNextId, from) * 13f) - 6;
                return from + (long)c.dilemmaLabEveryYears * SimConfig.MonthsPerYear + jitter;
            }
            int lo = c.dilemmaMinYears * SimConfig.MonthsPerYear, hi = Math.Max(lo, c.dilemmaMaxYears * SimConfig.MonthsPerYear);
            return from + lo + (int)(DilemmaRoll(20, State.dilemmaNextId, from) * (hi - lo + 1));
        }

        /// <summary>Every month: lapse what is old, and raise the next when it is due. No dice. Nothing at all when off.</summary>
        void DilemmaStep()
        {
            if (!Config.dilemmas) return;
            var s = State;
            var open = OpenDilemma;
            if (open != null)
            {
                if (s.month - open.month >= DilemmaLapseMonths || CastGone(open)) LapseDilemma(open);
                return;
            }
            if (s.firstRun.active) { s.dilemmaNextMonth = -1; return; } // none during the first run; the clock restarts after it
            if (s.dilemmaNextMonth < 0) s.dilemmaNextMonth = DilemmaScheduleFrom(s.month, s.dilemmas.Count == 0);
            if (s.month < s.dilemmaNextMonth) return;
            // A due dilemma never ends a wish: it waits for the open one to end (new wishes already wait for it), then the 8-month gap
            // runs from that end.
            if (OpenWish != null) return;
            long lastWish = s.wishes.Count == 0 ? long.MinValue / 2 : s.wishes.Max(w => w.endMonth >= 0 ? w.endMonth : w.month);
            if (s.month - Math.Max(lastWish, DilemmaLastEnd) < DilemmaGapMonths) return;
            var pick = PickDilemmaDef(out int a, out int b);
            if (pick == null) return;
            RaiseDilemma(pick, a, b);
        }

        /// <summary>A person the options point at died: the people decide at once (the same as a wish whose wisher dies).</summary>
        bool CastGone(Dilemma d)
        {
            var def = DilemmaDefOf(d);
            if (def == null || def.cast != DilemmaCast.TwoCandidates) return false;
            return Alive(d.personA) == null || Alive(d.personB) == null;
        }

        // ---------- conditions and the cast ----------
        string RoleOfDef(DilemmaDef def) => def.role == DilemmaDef.Leader ? LeaderOf(State.era) : def.role;

        /// <summary>Does the def hold now? On success the two people (or -1).</summary>
        bool DefHolds(DilemmaDef def, out int a, out int b)
        {
            a = b = -1;
            if (State.dilemmas.Any(d => d.defId == def.id)) return false;           // never repeats in a box
            if (State.era < def.minEra || State.era > def.maxEra) return false;
            if (!string.IsNullOrEmpty(def.needs) && !Has(def.needs)) return false;
            string role = RoleOfDef(def);
            if (!string.IsNullOrEmpty(role))
            {
                var seat = SeatOf(role);
                if (seat == null) return false;
                if (seat.holderId >= 0 && !def.replacesHolder) return false;
            }
            if (def.cast == DilemmaCast.TwoCandidates)
            {
                var pool = RoleCandidates(role);
                if (pool.Count < 2) return false;
                var ranked = string.IsNullOrEmpty(role)
                    ? pool.OrderByDescending(n => DilemmaRoll(5, n.id, 0)).ThenBy(n => n.id).ToList()
                    : pool.OrderByDescending(n => Standing(n, role)).ThenBy(n => n.id).ToList();
                a = ranked[0].id; b = ranked[1].id;
            }
            return true;
        }

        List<(DilemmaDef def, int a, int b)> EligibleDefs()
        {
            var list = new List<(DilemmaDef def, int a, int b)>();
            foreach (var def in DilemmaCatalog)
                if (DefHolds(def, out int a, out int b)) list.Add((def, a, b));
            return list;
        }

        DilemmaDef PickDilemmaDef(out int a, out int b)
        {
            a = b = -1;
            var ok = EligibleDefs();
            if (ok.Count == 0) return null;
            var pick = ok[(int)(DilemmaRoll(22, State.dilemmaNextId, State.month) * ok.Count) % ok.Count];
            a = pick.a; b = pick.b;
            return pick.def;
        }

        // ---------- words ----------
        string DilemmaText(string t, Dilemma d)
        {
            if (string.IsNullOrEmpty(t)) return "";
            if (t.IndexOf('{') < 0) return t;
            return t.Replace("{a}", FindNotable(d.personA)?.name ?? "someone").Replace("{b}", FindNotable(d.personB)?.name ?? "someone")
                    .Replace("{place}", Place);
        }

        /// <summary>The words and the people and places of a dilemma, for the card and the choice scene.</summary>
        public DilemmaView ViewOfDilemma(Dilemma d)
        {
            var def = DilemmaDefOf(d);
            if (d == null || def == null) return null;
            var v = new DilemmaView { dilemma = d, def = def, kind = def.kind, question = DilemmaText(def.question, d), options = new DilemmaOptionView[def.options.Length] };
            for (int i = 0; i < v.options.Length; i++)
            {
                var o = def.options[i];
                v.options[i] = new DilemmaOptionView
                {
                    index = i, label = DilemmaText(o.label, d), hint = DilemmaText(o.hint, d),
                    personId = d.PersonOf(i), buildingId = d.BuildingOf(i), buildingType = o.buildingType ?? "",
                };
            }
            return v;
        }

        /// <summary>The open dilemma as the view reads it, or null.</summary>
        public DilemmaView OpenDilemmaView => ViewOfDilemma(OpenDilemma);

        string DilemmaHeadline(ChronicleEntry e)
        {
            var d = DilemmaOfEntry(e.id);
            return d == null ? "The people ask the Keeper" : Clip(DilemmaText(DilemmaDefOf(d)?.question, d));
        }

        string DilemmaAnswerHeadline(ChronicleEntry e)
        {
            var d = State.dilemmas.FirstOrDefault(x => x.answerEntryId == e.id);
            var def = DilemmaDefOf(d);
            return d == null || def == null || d.option < 0 ? null : $"The Keeper chose {DilemmaText(def.options[d.option].label, d)}";
        }

        // ---------- raise ----------
        Dilemma RaiseDilemma(DilemmaDef def, int a, int b)
        {
            var s = State;
            int id = s.dilemmaNextId++;
            var d = new Dilemma { id = id, defId = def.id, month = s.month, personA = a, personB = b };
            for (int i = 0; i < 2 && i < def.options.Length; i++)
            {
                var bt = def.options[i].buildingType;
                var bld = string.IsNullOrEmpty(bt) ? null : s.buildings.FirstOrDefault(x => x.type == bt);
                if (i == 0) d.buildingA = bld?.id ?? -1; else d.buildingB = bld?.id ?? -1;
            }
            var e = new ChronicleEntry { id = DilemmaIdBase + id, month = s.month, kind = "dilemma", text = DilemmaText(def.question, d), salience = 0.6f, notableId = a, otherNotableId = b };
            s.chronicle.Add(e);
            Chronicled?.Invoke(e);
            d.entryId = e.id;
            s.dilemmas.Add(d);
            s.dilemmaNextMonth = DilemmaScheduleFrom(s.month, false);
            return d;
        }

        // ---------- answer ----------
        /// <summary>
        /// The Keeper's answer (a recorded input, like a shaping: it takes effect at the sim's current month and is final; there is
        /// no undo). Writes a "keeper" chronicle entry and applies the option's effects. False if the dilemma is not open or
        /// the option is not 0 or 1.
        /// </summary>
        public bool AnswerDilemma(int dilemmaId, int option, DilemmaPath path)
        {
            var d = FindDilemma(dilemmaId);
            var def = DilemmaDefOf(d);
            if (d == null || def == null || d.state != DilemmaState.Open || option < 0 || option >= def.options.Length) return false;
            d.state = DilemmaState.Answered;
            d.option = option;
            d.path = path;
            d.endMonth = State.month;
            var o = def.options[option];
            string text = string.IsNullOrEmpty(o.done) ? $"The people asked, and the Keeper chose {DilemmaText(o.label, d)}." : DilemmaText(o.done, d);
            var e = Record("keeper", text, 0.8f, d.PersonOf(option), d.PersonOf(1 - option));
            e.keeper = true;
            d.answerEntryId = e.id;
            ApplyDilemmaEffects(d, def, option);
            return true;
        }

        // ---------- lapse ----------
        /// <summary>Which option the people take when the Keeper says nothing: the role-holder's trait (and who they favour), else a seeded hash.</summary>
        public int DilemmaDefaultOption(Dilemma d)
        {
            var def = DilemmaDefOf(d);
            if (def == null) return 0;
            string role = RoleOfDef(def);
            var holder = string.IsNullOrEmpty(role) ? null : Alive(SeatOf(role)?.holderId ?? -1);
            float[] score = new float[def.options.Length];
            for (int i = 0; i < score.Length; i++)
            {
                var o = def.options[i];
                var person = Alive(d.PersonOf(i));
                if (def.cast == DilemmaCast.TwoCandidates && person == null) score[i] -= 100f; // the one who died cannot be chosen
                if (holder == null) continue;
                if (o.favouredBy != null && Array.IndexOf(o.favouredBy, holder.trait) >= 0) score[i] += 2f;
                if (person != null) score[i] += Affinity(holder, person);
            }
            int best = 0; bool tie = true;
            for (int i = 1; i < score.Length; i++) { if (score[i] != score[0]) tie = false; if (score[i] > score[best]) best = i; }
            if (!tie) return best;
            return (int)(DilemmaRoll(30, d.id, d.month) * score.Length) % score.Length;
        }

        /// <summary>The people decide (24 months of silence, or a person in it died): the default option's effects apply and the
        /// chronicle says so neutrally. No blame, nothing credited to the Keeper.</summary>
        void LapseDilemma(Dilemma d)
        {
            if (d == null || d.state != DilemmaState.Open) return;
            var def = DilemmaDefOf(d);
            d.state = DilemmaState.Lapsed;
            d.endMonth = State.month;
            if (def == null) return;
            d.option = DilemmaDefaultOption(d);
            string label = DilemmaText(def.options[d.option].label, d);
            var e = Record("dilemma_result", $"The people chose {label} themselves.", 0.55f, d.PersonOf(d.option), d.PersonOf(1 - d.option));
            d.resultEntryId = e.id;
            ApplyDilemmaEffects(d, def, d.option);
        }

        // ---------- effects ----------
        static readonly string[] ProudTraits = { "the stubborn", "the grump", "the loud", "the brave", "the clever", "the tinkerer", "the worrier" };

        void ApplyDilemmaEffects(Dilemma d, DilemmaDef def, int option)
        {
            var s = State;
            var o = def.options[option];
            var chosen = Alive(d.PersonOf(option));
            var other = Alive(d.PersonOf(1 - option));

            if (o.takesRole && chosen != null)
            {
                string role = RoleOfDef(def);
                var seat = string.IsNullOrEmpty(role) ? null : SeatOf(role);
                if (seat != null)
                {
                    if (seat.holderId >= 0 && seat.holderId != chosen.id) Vacate(seat);   // the holder steps aside; bypasses the succession roll
                    Assign(seat, chosen);
                    RoleLine("role_won", $"{chosen.FullName} became {RoleIn(role)}.", 0.65f, chosen, other, seat);
                }
            }

            if (o.lean.HasValue)
                s.dilemmaLeans.Add(new DilemmaLean { lean = (int)o.lean.Value, fromMonth = s.month, untilMonth = s.month + NudgeMonths, dilemmaId = d.id });

            if (o.ties == DilemmaTies.RivalOrGrace && chosen != null && other != null) UnchosenTies(d, chosen, other);

            if (!string.IsNullOrEmpty(o.custom) && !s.customs.Contains(o.custom)) s.customs.Add(o.custom);

            if (o.strangers > 0) StrangersJoin(d, o.strangers);
        }

        /// <summary>The unchosen: by trait either a grudge (a rivalry, sometimes) or grace (a friendship, or just good will).</summary>
        void UnchosenTies(Dilemma d, Notable chosen, Notable other)
        {
            bool proud = Array.IndexOf(ProudTraits, other.trait) >= 0;
            bool free = other.rivalId < 0 && chosen.rivalId < 0 && other.partnerId != chosen.id && other.friendId != chosen.id;
            if (proud && free && DilemmaRoll(40, d.id, d.month) < 0.6f)
            {
                other.rivalId = chosen.id; chosen.rivalId = other.id;
                Chronicle("rivalry", $"{other.FullName} could not forgive being passed over for {chosen.FullName}. They have been rivals since.", 0.7f, other.id, chosen.id);
                return;
            }
            if (other.friendId < 0 && chosen.friendId < 0 && other.rivalId != chosen.id && other.partnerId != chosen.id)
            {
                other.friendId = chosen.id; chosen.friendId = other.id;
                Chronicle("friendship", $"{other.FullName} took it well, and wished {chosen.FullName} luck. They became fast friends over it.", 0.6f, other.id, chosen.id);
                return;
            }
            Chronicle("dilemma_grace", proud ? $"{other.name} sulked for a season, then let it go." : $"{other.name} took it gracefully and wished {chosen.name} well.", 0.5f, other.id, chosen.id);
        }

        /// <summary>A family of strangers joins: new notables with one family name, drawn from hashes (no dice).</summary>
        void StrangersJoin(Dilemma d, int count)
        {
            var s = State;
            string family = NameData.Families[(int)(DilemmaRoll(50, d.id, d.month) * NameData.Families.Length) % NameData.Families.Length];
            var local = new SimRandom((int)(DilemmaRoll(51, d.id, d.month) * 1_000_000f) + 1); // only for the name retries
            var made = new List<Notable>();
            for (int i = 0; i < count; i++)
            {
                var n = new Notable
                {
                    id = s.NextId(),
                    name = NameData.Given[(int)(DilemmaRoll(52 + i, d.id, d.month) * NameData.Given.Length) % NameData.Given.Length],
                    family = family,
                    trait = NameData.Traits[(int)(DilemmaRoll(60 + i, d.id, d.month) * NameData.Traits.Length) % NameData.Traits.Length],
                    age = 18f + DilemmaRoll(70 + i, d.id, d.month) * 17f,
                    lifespan = Config.lifespanMin + DilemmaRoll(80 + i, d.id, d.month) * (Config.lifespanMax - Config.lifespanMin),
                    bornMonth = s.month,
                };
                MakeNameUnique(n, local);
                s.notables.Add(n);
                made.Add(n);
            }
            if (made.Count > 1) { made[0].partnerId = made[1].id; made[1].partnerId = made[0].id; }
            s.population += made.Count;
            string names = string.Join(" and ", made.Select(n => n.name));
            Chronicle("newcomer", $"The {family} family, {names}, came from beyond and joined the {EraNoun(s.era)}.", 0.7f, made[0].id);
        }

        // ---------- debug ----------
        /// <summary>Debug: raise a dilemma now (by def id, else the first that holds), ignoring pacing, the gap and the first run. Lapses
        /// the open dilemma and closes the open wish first (the slot is shared). Null if no def holds.</summary>
        public Dilemma DebugRaiseDilemma(string defId = null)
        {
            var open = OpenDilemma;
            if (open != null) LapseDilemma(open);
            var w = OpenWish;
            if (w != null) CloseWish(w, WishState.Lapsed);
            foreach (var def in DilemmaCatalog)
            {
                if (defId != null && def.id != defId) continue;
                if (DefHolds(def, out int a, out int b)) return RaiseDilemma(def, a, b);
            }
            return null;
        }

        /// <summary>Debug: the people decide the open dilemma now. False if none is open.</summary>
        public bool DebugLapseDilemma()
        {
            var open = OpenDilemma;
            if (open == null) return false;
            LapseDilemma(open);
            return true;
        }
    }
}
