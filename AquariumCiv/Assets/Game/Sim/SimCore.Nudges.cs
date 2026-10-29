using System;
using System.Collections.Generic;
using System.Linq;

namespace AquariumCiv.Sim
{
    /// <summary>
    /// The Keeper shapes the land (Gate 3, P5 variant B): rain, a grove, a channel. A shaping sets a lean for a few game
    /// years (food or wood, and what gets built next and where) and the chronicle credits the Keeper when the people act
    /// on it ("After the Keeper's rain, Ada Lund planted a new field."). Nothing here runs until the player shapes something.
    /// </summary>
    public partial class SimCore
    {
        public static class NudgeKind
        {
            public const string Rain = "rain", Grove = "grove", Channel = "channel";
            public static readonly string[] All = { Rain, Grove, Channel };
        }

        public enum Lean { Food, Craft }

        public static Lean LeanOf(string kind) => kind == NudgeKind.Grove ? Lean.Craft : Lean.Food;

        /// <summary>Game months a nudge keeps its lean.</summary>
        public const int NudgeMonths = 36;

        public IEnumerable<Nudge> ActiveNudges => State.nudges.Where(n => State.month - n.month < NudgeMonths);
        bool Leaning(Lean l) => State.nudges.Count > 0 && ActiveNudges.Any(n => LeanOf(n.kind) == l);

        /// <summary>Shape the land near a building slot. Returns the nudge (the view shows its prop and logs it).</summary>
        public Nudge PlaceNudge(string kind, int slot)
        {
            var n = new Nudge { id = State.NextId(), kind = kind, slot = slot, month = State.month, answerAfter = State.storyRng.Range(10, 25) };
            State.nudges.Add(n);
            // Rain is felt at once: the fields drink.
            if (kind == NudgeKind.Rain) State.food = Math.Min(FoodCap, State.food + 6f + State.population * 0.5f);
            return n;
        }

        /// <summary>Months until this kind can be used again (0 = ready).</summary>
        public int NudgeCooldown(string kind)
        {
            var last = State.nudges.Where(n => n.kind == kind).OrderByDescending(n => n.month).FirstOrDefault();
            return last == null ? 0 : (int)Math.Max(0, NudgeMonths - (State.month - last.month));
        }

        /// <summary>Output multipliers from active nudges (applied in Step through NudgeFood / NudgeWood).</summary>
        float NudgeFood => Leaning(Lean.Food) ? 1.35f : 1f;
        float NudgeWood => Leaning(Lean.Craft) ? 1.5f : 1f;

        /// <summary>Called each month after the economy (only when nudges exist): the credited deed.</summary>
        void NudgeStep()
        {
            var s = State;
            foreach (var n in s.nudges.Where(n => !n.credited && s.month - n.month < NudgeMonths).ToList())
            {
                // The people answer within 1–3 game years: the first month after a short pause where they can afford it.
                long age = s.month - n.month;
                if (age < Math.Max(8, n.answerAfter)) continue;
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
        }

        /// <summary>What the people build in answer to a nudge (null = nothing fits right now).</summary>
        string AnswerTo(Nudge n) => n.kind switch
        {
            NudgeKind.Rain => BuildingType.Farm,
            NudgeKind.Channel => Count(BuildingType.Windmill) == 0 ? BuildingType.Windmill : BuildingType.Farm,
            NudgeKind.Grove => Count(BuildingType.Lumbermill) == 0 ? BuildingType.Lumbermill
                : Count(BuildingType.Market) == 0 ? BuildingType.Market : BuildingType.Hut,
            _ => null,
        };

        static string CreditLine(string kind, string type, Notable who)
        {
            string name = who != null ? who.FullName : "the people";
            string built = type switch
            {
                BuildingType.Farm => "planted a new field", BuildingType.Windmill => "built a windmill to grind the extra grain",
                BuildingType.Lumbermill => "set up a lumber mill", BuildingType.Market => "opened a market for their crafts",
                _ => $"built a new {type.ToLowerInvariant()}",
            };
            string cause = kind switch
            {
                NudgeKind.Rain => "the Keeper's rain",
                NudgeKind.Grove => "the Keeper planted a grove", NudgeKind.Channel => "the Keeper dug a channel from the pond",
                _ => "the Keeper's sign",
            };
            return kind == NudgeKind.Rain ? $"After {cause}, {name} {built}." : $"After {cause}, {name} {built} beside it.";
        }

        Notable StoryAdultOrNull()
        {
            var adults = State.notables.Where(n => n.alive && n.age >= 16f).ToList();
            if (adults.Count == 0) return null;
            return adults[State.storyRng.Range(0, adults.Count)];
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

        /// <summary>Scripted moments (an era's landmark): a building of this type in this slot, if it is free.</summary>
        public Building AddBuildingAt(string type, int slot)
        {
            var s = State;
            if (slot < 0 || slot >= Config.buildingSlots || s.buildings.Any(b => b.slot == slot)) return null;
            var b = new Building { id = s.NextId(), type = type, slot = slot, foundedMonth = s.month };
            s.buildings.Add(b);
            if (!s.foundedTypes.Contains(type)) s.foundedTypes.Add(type);
            return b;
        }

        /// <summary>Optional: tiles taken by groves and channels, kept free of buildings. Set by the scene.</summary>
        public Func<IEnumerable<int>> ReservedSlots;

        /// <summary>Optional: the view's real distance between slots (set by the scene), for "beside the grove".</summary>
        public Func<int, HashSet<int>, int> SlotNear;
    }
}
