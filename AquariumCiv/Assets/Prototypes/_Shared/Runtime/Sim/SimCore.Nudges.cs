using System;
using System.Collections.Generic;
using System.Linq;

namespace AquariumCiv.Prototypes.Sim
{
    /// <summary>
    /// P5 nudges: the Keeper's indirect influence. A nudge sets a lean for a few game years (food, wood, mood, what gets
    /// built next and where) and the chronicle credits the Keeper when the people act on it ("After the Keeper's rain,
    /// Ada Lund planted a new field."). Omens (totems, shrine) and environment shaping (rain, grove, channel) share this
    /// model, so P5's A/B differs only in the form. Nothing here runs until a nudge is placed (P2 never places one).
    /// </summary>
    public partial class SimCore
    {
        public static class NudgeKind
        {
            public const string HarvestTotem = "harvest_totem", CraftTotem = "craft_totem", FaithShrine = "faith_shrine",
                Rain = "rain", Grove = "grove", Channel = "channel";
        }

        public enum Lean { Food, Craft, Faith }

        public static Lean LeanOf(string kind) => kind switch
        {
            NudgeKind.HarvestTotem or NudgeKind.Rain or NudgeKind.Channel => Lean.Food,
            NudgeKind.CraftTotem or NudgeKind.Grove => Lean.Craft,
            _ => Lean.Faith,
        };

        /// <summary>Game months a nudge keeps its lean.</summary>
        public const int NudgeMonths = 36;

        public IEnumerable<Nudge> ActiveNudges => (State.nudges ?? new List<Nudge>()).Where(n => State.month - n.month < NudgeMonths);
        bool Leaning(Lean l) => State.nudges != null && State.nudges.Count > 0 && ActiveNudges.Any(n => LeanOf(n.kind) == l);

        /// <summary>Place a nudge near a building slot. Returns it (the view shows its prop and logs it).</summary>
        public Nudge PlaceNudge(string kind, int slot)
        {
            State.nudges ??= new List<Nudge>();
            var n = new Nudge { id = State.NextId(), kind = kind, slot = slot, month = State.month };
            State.nudges.Add(n);
            // Rain is felt at once: the fields drink.
            if (kind == NudgeKind.Rain) State.food = Math.Min(FoodCap, State.food + 6f + State.population * 0.5f);
            return n;
        }

        /// <summary>Months until this kind can be used again (0 = ready).</summary>
        public int NudgeCooldown(string kind)
        {
            var last = State.nudges?.Where(n => n.kind == kind).OrderByDescending(n => n.month).FirstOrDefault();
            return last == null ? 0 : (int)Math.Max(0, NudgeMonths - (State.month - last.month));
        }

        /// <summary>Output multipliers from active nudges (applied in Step through NudgeFood / NudgeWood).</summary>
        float NudgeFood => Leaning(Lean.Food) ? 1.35f : 1f;
        float NudgeWood => Leaning(Lean.Craft) ? 1.5f : 1f;

        /// <summary>Called each month after the economy (only when nudges exist): mood, festivals and the credited deed.</summary>
        void NudgeStep(int season)
        {
            var s = State;
            if (s.nudges == null || s.nudges.Count == 0) return;
            if (Leaning(Lean.Faith)) s.mood = Math.Min(1f, s.mood + 0.01f);
            foreach (var n in s.nudges.Where(n => !n.credited && s.month - n.month < NudgeMonths).ToList())
            {
                // The people answer within 1–3 game years: the first month after a short pause where they can afford it.
                long age = s.month - n.month;
                if (age < 8) continue;
                string type = AnswerTo(n);
                if (type == null) continue;
                float cost = NextBuildingCost * 0.6f; // the Keeper's sign makes it a community effort
                bool lastChance = age >= NudgeMonths - 1;
                if (s.wood < cost && !lastChance) continue;
                s.wood = Math.Max(0f, s.wood - cost);
                var b = AddBuildingNear(type, n.slot);
                n.credited = true;
                n.answerMonth = s.month;
                n.buildingId = b?.id ?? -1;
                var who = StoryAdultOrNull();
                var e = Chronicle("keeper", CreditLine(n.kind, type, who), 0.9f, who?.id ?? -1);
                e.buildingId = n.buildingId;
                e.keeper = true;
            }
            // Faith also brings an extra festival now and then, credited to the shrine.
            if (Leaning(Lean.Faith) && season >= 4 && season <= 9 && s.storyRng != null && s.storyRng.Chance(0.06f))
            {
                var host = StoryAdultOrNull();
                var e = Chronicle("festival", host != null
                    ? $"Gathered at the Keeper's shrine, {host.FullName} led a festival of thanks."
                    : "The people gathered at the Keeper's shrine for a festival of thanks.", 0.7f, host?.id ?? -1);
                e.keeper = true;
                s.mood = Math.Min(1f, s.mood + 0.1f);
            }
        }

        /// <summary>What the people build in answer to a nudge (null = nothing fits right now).</summary>
        string AnswerTo(Nudge n) => n.kind switch
        {
            NudgeKind.HarvestTotem or NudgeKind.Rain => BuildingType.Farm,
            NudgeKind.Channel => Count(BuildingType.Windmill) == 0 ? BuildingType.Windmill : BuildingType.Farm,
            NudgeKind.CraftTotem or NudgeKind.Grove => Count(BuildingType.Lumbermill) == 0 ? BuildingType.Lumbermill
                : Count(BuildingType.Market) == 0 ? BuildingType.Market : BuildingType.Hut,
            NudgeKind.FaithShrine => Count(BuildingType.Church) == 0 ? BuildingType.Church
                : Count(BuildingType.Tavern) == 0 ? BuildingType.Tavern : BuildingType.Hut,
            _ => null,
        };

        static string CreditLine(string kind, string type, Notable who)
        {
            string name = who != null ? who.FullName : "the people";
            string built = type switch
            {
                BuildingType.Farm => "planted a new field", BuildingType.Windmill => "built a windmill to grind the extra grain",
                BuildingType.Lumbermill => "set up a lumber mill", BuildingType.Market => "opened a market for their crafts",
                BuildingType.Church => "raised a chapel", BuildingType.Tavern => "opened a tavern for the pilgrims",
                _ => $"built a new {type.ToLowerInvariant()}",
            };
            string cause = kind switch
            {
                NudgeKind.HarvestTotem => "the Keeper raised a harvest totem", NudgeKind.CraftTotem => "the Keeper raised a craft totem",
                NudgeKind.FaithShrine => "the Keeper set down a shrine", NudgeKind.Rain => "the Keeper's rain",
                NudgeKind.Grove => "the Keeper planted a grove", NudgeKind.Channel => "the Keeper dug a channel from the pond",
                _ => "the Keeper's sign",
            };
            return kind == NudgeKind.Rain ? $"After {cause}, {name} {built}." : $"After {cause}, {name} {built} beside it.";
        }

        Notable StoryAdultOrNull()
        {
            var adults = State.notables.Where(n => n.alive && n.age >= 16f).ToList();
            if (adults.Count == 0) return null;
            var r = State.storyRng ?? State.rng;
            return adults[r.Range(0, adults.Count)];
        }

        /// <summary>A building in the free slot nearest to <paramref name="nearSlot"/> (slots are ordered centre-out, so
        /// "nearest" is by index distance; the view maps slots to positions).</summary>
        Building AddBuildingNear(string type, int nearSlot)
        {
            var s = State;
            if (s.buildings.Count >= Config.buildingSlots) return null;
            var used = s.buildings.Select(b => b.slot).ToHashSet();
            int slot = SlotNear?.Invoke(nearSlot, used) ?? Enumerable.Range(0, Config.buildingSlots).Where(i => !used.Contains(i)).OrderBy(i => Math.Abs(i - nearSlot)).First();
            var b = new Building { id = s.NextId(), type = type, slot = slot, foundedMonth = s.month };
            s.buildings.Add(b);
            if (!s.foundedTypes.Contains(type)) s.foundedTypes.Add(type);
            return b;
        }

        /// <summary>Optional: tiles taken by nudges (groves, channels), kept free of buildings. Set by the P5 scene.</summary>
        public Func<IEnumerable<int>> ReservedSlots;

        /// <summary>Optional: the view's real distance between slots (set by the scene), for "beside the totem".</summary>
        public Func<int, HashSet<int>, int> SlotNear;
    }
}
