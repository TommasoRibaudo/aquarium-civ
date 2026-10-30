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
            /// <summary>The prototypes' three (P5). Kept as it was: Growth Lab's criterion 2 runs over exactly these.</summary>
            public static readonly string[] All = { Rain, Grove, Channel };
            // The rest of the shaping toolset per era (research 10 "Decision", #42). Their own sim layers (charges, herds,
            // light, deposits) come later; for now the people answer them at the spot like the first three.
            public const string Herd = "herd", Lamp = "lamp", Spring = "spring", Outcrop = "outcrop", DyeMeadow = "dye meadow";
            /// <summary>Every gift tool the people answer: camp rain, grove, herd; village channel, lamp; town outcrop,
            /// spring, dye meadow. (Hill and hollow are landscaping: nothing is owed for them.)</summary>
            public static readonly string[] Gifts = { Rain, Grove, Herd, Channel, Lamp, Outcrop, Spring, DyeMeadow };
        }

        public enum Lean { Food, Craft }

        public static Lean LeanOf(string kind) => kind == NudgeKind.Grove || kind == NudgeKind.Outcrop || kind == NudgeKind.DyeMeadow ? Lean.Craft : Lean.Food;

        /// <summary>Game months a nudge keeps its lean.</summary>
        public const int NudgeMonths = 36;

        public IEnumerable<Nudge> ActiveNudges => State.nudges.Where(n => State.month - n.month < NudgeMonths);
        bool Leaning(Lean l) => State.nudges.Count > 0 && ActiveNudges.Any(n => LeanOf(n.kind) == l);

        /// <summary>Shape the land near a building slot (the prototypes' call). The spot is the slot's old hex position.</summary>
        public Nudge PlaceNudge(string kind, int slot)
        {
            float x = 0f, z = 0f;
            Settlement?.SlotPosition(slot, out x, out z);
            return PlaceNudge(kind, x, z, slot);
        }

        /// <summary>Shape the land at a spot (box coordinates, the same as Building.x/z). Returns the nudge (the view
        /// shows its prop and logs it). The land changes at once (Settle); the people answer within 1–3 years.</summary>
        public Nudge PlaceNudge(string kind, float x, float z) => PlaceNudge(kind, x, z, 0);

        Nudge PlaceNudge(string kind, float x, float z, int slot)
        {
            var n = new Nudge { id = State.NextId(), kind = kind, slot = slot, x = x, z = z, month = State.month, answerAfter = State.storyRng.Range(10, 25) };
            State.nudges.Add(n);
            // Rain is felt at once: the fields drink.
            if (kind == NudgeKind.Rain) State.food = Math.Min(FoodCap, State.food + 6f + State.population * 0.5f);
            if (kind == NudgeKind.Lamp) KeeperLampUsed(); // from now on the strange light is the Keeper's lamp (#61: nothing called the hook)
            Settlement?.ApplyShaping(n); // no dice, nothing the economy reads
            RelicsOnShaping(n); // the Sky Crumb, the Lens, the doll's count (SimCore.Relics.cs; no dice)
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
                bool lastChance = age >= NudgeMonths - 1;
                Answer(n, lastChance);
            }
        }

        /// <summary>The people answer a shaping: a building beside it, credited in the chronicle to the notable the Keeper
        /// named (<see cref="Nudge.creditNotableId"/>) or else one the story picks. Without <paramref name="force"/> they
        /// wait until they can afford it. False if nothing was done.</summary>
        bool Answer(Nudge n, bool force)
        {
            var s = State;
            string type = AnswerTo(n);
            if (type == null) return false;
            var named = n.creditNotableId >= 0 ? s.notables.FirstOrDefault(x => x.id == n.creditNotableId && x.alive && x.age >= 16f) : null;
            // The town is full (#82): no building can be added, so the answer is a harvest of the gift, credited to a person,
            // with no building behind it (the text never names what doesn't exist).
            if (s.buildings.Count >= Config.buildingSlots)
            {
                if (s.wood < NextBuildingCost * 0.6f && !force) return false;
                n.credited = true;
                n.answerMonth = s.month;
                n.buildingId = -1;
                var hw = named ?? StoryAdultOrNull();
                var he = Chronicle("keeper", HarvestLine(n.kind, hw, s.era), 0.9f, hw?.id ?? -1);
                he.keeper = true;
                return true;
            }
            float cost = NextBuildingCost * 0.6f; // the Keeper's sign makes it a community effort
            if (s.wood < cost && !force) return false;
            s.wood = Math.Max(0f, s.wood - cost);
            var b = AddBuildingNear(type, n.slot);
            n.credited = true;
            n.answerMonth = s.month;
            n.buildingId = b?.id ?? -1;
            var who = named ?? StoryAdultOrNull();
            var e = Chronicle("keeper", b != null ? CreditLine(n.kind, type, who, s.era) : HarvestLine(n.kind, who, s.era), 0.9f, who?.id ?? -1);
            e.buildingId = n.buildingId;
            e.keeper = true;
            return true;
        }

        /// <summary>The credited deed when no building can be added (the box is full): the people make use of the gift.
        /// Never names a building.</summary>
        static string HarvestLine(string kind, Notable who, Era era)
        {
            string name = who != null ? who.FullName : "the people";
            string own = who != null ? who.FullName + "'s" : "the people's";
            bool camp = era == Era.Camp;
            return kind switch
            {
                NudgeKind.Rain => $"After the Keeper's rain, {name} {(camp ? "gathered the bright new growth" : "brought in a second crop")}.",
                NudgeKind.Grove => $"After the Keeper planted a grove, {name} {(camp ? "gathered nuts and berries among the new trees" : "cut and gathered from the new trees")}.",
                NudgeKind.Herd => $"After the Keeper sent a herd, {own} flock grazed the new herd.",
                NudgeKind.Channel => $"After the Keeper dug a channel from the pond, {name} drew water along it to the fields.",
                NudgeKind.Lamp => $"After the Keeper hung a lamp, {name} sat late in its light and the work went on.",
                NudgeKind.Spring => $"After a spring broke out at the Keeper's touch, {name} drew the first water from it.",
                NudgeKind.Outcrop => $"After the Keeper raised an outcrop, {name} split stone from it.",
                NudgeKind.DyeMeadow => $"After the Keeper sowed a meadow of dye flowers, {name} picked the first flowers.",
                _ => $"After the Keeper's sign, {name} put the gift to use.",
            };
        }

        /// <summary>What the people build in answer to a nudge, by the era and the land at the spot (Growth Lab gap 5:
        /// a grove was answered by a market or a hut). Only types of the box's era. The land is read from the terrain
        /// alone (never from placement), so placement can't change the economy. Settlement.KindFor then picks the
        /// lot's kind: a town's channel workshop is a watermill, a herd by the pond is a fishing jetty, and so on.
        /// - rain: a field (gathering grounds in the camp).
        /// - grove: the first lumber mill; then a field among the trees (camp) or an orchard (village), a timber yard (town).
        /// - herd: hunting grounds or a fishing jetty (camp), a pasture (village), a weaver's workshop or a jetty (town).
        /// - channel: a field (camp), the windmill then irrigated fields (village), a watermill (town).
        /// - lamp: a field under the sunbeam; in a town the tavern (tables out under the lantern), else the market, else a field.
        /// - spring: a well (a field in a camp).
        /// - outcrop: a quarry, kiln or smithy by the rock (town); a stone hut before.
        /// - dye meadow: a dyer's yard (town); a meadow field before.</summary>
        string AnswerTo(Nudge n)
        {
            var era = State.era;
            bool town = era >= Era.Town, village = era >= Era.Village;
            switch (n.kind)
            {
                case NudgeKind.Rain: return BuildingType.Farm;
                case NudgeKind.Grove:
                    if (Count(BuildingType.Lumbermill) == 0) return BuildingType.Lumbermill;
                    return town ? BuildingType.Workshop : BuildingType.Farm;
                case NudgeKind.Herd: return town ? BuildingType.Workshop : BuildingType.Farm;
                case NudgeKind.Channel:
                    if (town) return BuildingType.Workshop;
                    return Wants(BuildingType.Windmill, Count(BuildingType.Windmill) == 0) ? BuildingType.Windmill : BuildingType.Farm;
                case NudgeKind.Lamp:
                    if (town && Count(BuildingType.Tavern) == 0) return BuildingType.Tavern;
                    if (town && Count(BuildingType.Market) == 0) return BuildingType.Market;
                    return BuildingType.Farm;
                case NudgeKind.Spring: return village ? BuildingType.Well : BuildingType.Farm;
                case NudgeKind.Outcrop: return town ? BuildingType.Workshop : BuildingType.Hut;
                case NudgeKind.DyeMeadow: return town ? BuildingType.Workshop : BuildingType.Farm;
            }
            return null;
        }

        internal static string CreditLine(string kind, string type, Notable who, Era era = Era.Village, bool plain = false)
        {
            string name = who != null ? who.FullName : "the people";
            bool camp = era == Era.Camp; // the camp gathers; it doesn't sow yet (#61)
            string built = type switch
            {
                BuildingType.Farm => kind == NudgeKind.Herd ? (camp ? "staked out new hunting grounds" : "staked out new hunting and grazing grounds")
                    : kind == NudgeKind.Grove ? (camp ? "gathered nuts and berries among the new trees" : "planted among the new trees")
                    : camp ? "staked out new gathering grounds" : "planted a new field",
                BuildingType.Lumbermill when camp => "started a woodpile",
                BuildingType.Windmill => "built a windmill to grind the extra grain",
                BuildingType.Lumbermill => "set up a lumber mill", BuildingType.Market => "opened a market for their crafts",
                BuildingType.Well => "dug a well", BuildingType.Tavern => "opened a tavern",
                BuildingType.Workshop when plain => "set up a workshop",
                BuildingType.Workshop => kind switch
                {
                    NudgeKind.Channel => "built a watermill", NudgeKind.Grove => "opened a timber yard",
                    NudgeKind.Outcrop => "opened a workshop for the new stone", NudgeKind.DyeMeadow => "set up a dyer's yard",
                    _ => "set up a workshop",
                },
                _ => $"built a new {type.ToLowerInvariant()}",
            };
            string cause = kind switch
            {
                NudgeKind.Rain => "the Keeper's rain",
                NudgeKind.Grove => "the Keeper planted a grove", NudgeKind.Channel => "the Keeper dug a channel from the pond",
                NudgeKind.Herd => "the Keeper sent a herd", NudgeKind.Lamp => "the Keeper hung a lamp",
                NudgeKind.Spring => "a spring broke out at the Keeper's touch", NudgeKind.Outcrop => "the Keeper raised an outcrop",
                NudgeKind.DyeMeadow => "the Keeper sowed a meadow of dye flowers",
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
