using AquariumCiv.Sim;

namespace AquariumCiv.Life
{
    /// <summary>What the people do at a Keeper's gift when they answer it: the credited notable's work, the helpers' and
    /// the kids' (#85; game-definition §6, research 10 "Decision": who reacts per tool and era).</summary>
    public struct ShapingWork
    {
        public ActivityKind work;
        public Prop prop;
        public ActivityKind help;
        public Prop helpProp;
        public ActivityKind kids;
    }

    /// <summary>
    /// The people's answer to a shaping, shown in the world (#85): the notable the chronicle credits walks to the gift (the
    /// new field, mill, pasture or jetty, or the gift itself when the box is full and the answer is a harvest), does the
    /// work the gift calls for, a few grown-ups help, the kids watch and play, and at the end they turn to the glass to
    /// thank the Keeper. Staged by <see cref="LifePlanner.StartShapingAnswer"/> on the relic moments' machinery
    /// (<see cref="RelicMomentDef"/>: the same walking-pace casting). Data, not code: tune here.
    /// </summary>
    public static class ShapingAnswers
    {
        /// <summary>Real seconds an answer moment lasts [est].</summary>
        public const float Seconds = 40f;

        /// <summary>Where a far-off credited notable's work starts (a fraction of the moment).</summary>
        public const float LateOwnerArrives = 0.62f;

        /// <summary>The work at the gift, by tool, herd variant, the box's era and what the people built (null = a harvest,
        /// no building: the box was full).</summary>
        public static ShapingWork WorkFor(string kind, string variant, Era era, string buildingType)
        {
            bool camp = era == Era.Camp, town = era >= Era.Town;
            switch (kind)
            {
                case SimCore.NudgeKind.Rain:
                    if (camp) return W(ActivityKind.Gather, Prop.Basket, ActivityKind.Gather, Prop.Basket, ActivityKind.Splash);
                    if (buildingType == null) return W(ActivityKind.Harvest, Prop.Scythe, ActivityKind.Harvest, Prop.Sheaf, ActivityKind.Splash);
                    return W(ActivityKind.Sow, Prop.SeedBag, ActivityKind.Hoe, Prop.Hoe, ActivityKind.Splash);
                case SimCore.NudgeKind.Grove:
                    if (buildingType == BuildingType.Lumbermill) return camp
                        ? W(ActivityKind.Chop, Prop.Axe, ActivityKind.StackWood, Prop.Log, ActivityKind.Play)
                        : W(ActivityKind.Chop, Prop.Axe, ActivityKind.Saw, Prop.Saw, ActivityKind.Play);
                    if (buildingType == BuildingType.Workshop) return W(ActivityKind.Saw, Prop.Saw, ActivityKind.StackWood, Prop.Log, ActivityKind.Play);
                    if (camp || buildingType == null) return W(ActivityKind.Gather, Prop.Basket, ActivityKind.Gather, Prop.Basket, ActivityKind.Play);
                    return W(ActivityKind.Harvest, Prop.Basket, ActivityKind.Gather, Prop.Basket, ActivityKind.Play); // the orchard's fruit
                case SimCore.NudgeKind.Herd:
                    if (variant == "fish") return W(ActivityKind.Fish, Prop.Rod, ActivityKind.Fish, Prop.FishStick, ActivityKind.Point);
                    if (camp) return W(ActivityKind.Stalk, Prop.Spear, ActivityKind.Stalk, Prop.Spear, ActivityKind.Point); // the first great hunt
                    if (town) return W(ActivityKind.Herd, Prop.Shears, ActivityKind.Herd, Prop.Wool, ActivityKind.Watch);
                    return W(ActivityKind.Herd, Prop.Crook, ActivityKind.Herd, Prop.Lamb, ActivityKind.Watch);
                case SimCore.NudgeKind.Channel:
                    if (buildingType == BuildingType.Windmill || buildingType == BuildingType.Workshop)
                        return W(ActivityKind.MillWork, Prop.Sack, ActivityKind.MillWork, Prop.Sack, ActivityKind.Watch);
                    return W(ActivityKind.Hoe, Prop.Hoe, ActivityKind.Weed, Prop.Buckets, ActivityKind.Splash);
            }
            return W(ActivityKind.Watch, Prop.None, ActivityKind.Watch, Prop.None, ActivityKind.Watch);
        }

        static ShapingWork W(ActivityKind work, Prop prop, ActivityKind help, Prop helpProp, ActivityKind kids)
            => new ShapingWork { work = work, prop = prop, help = help, helpProp = helpProp, kids = kids };

        /// <summary>The answer moment as beats (relic-moment data): come over (0–0.45: up to ~9 s of walking, across the box),
        /// work (0.45–0.82), thank the Keeper (0.82–1: a wave to the glass; the camp, still wary, looks up).</summary>
        /// <param name="lateOwner">The credited notable is far off: they take longer to come over (0–0.62) and work less
        /// (0.62–0.82), so it's still them, at the walking pace.</param>
        public static RelicMomentDef For(string kind, string variant, Era era, string buildingType, bool lateOwner = false)
        {
            var w = WorkFor(kind, variant, era, buildingType);
            var d = new RelicMomentDef { relicId = null, seconds = Seconds, maxKids = 2, maxGrown = 3, maxQueue = 0, standIns = true };
            void B(string id, RelicCast c, float from, float to, ActivityKind k, Prop prop = Prop.None, bool faces = false)
                => d.beats.Add(new RelicBeat { id = id, cast = c, from = from, to = to, kind = k, where = RelicWhere.Spot, prop = prop, facesGlass = faces });
            const float work = 0.45f, thanks = 0.82f; // the come-over is long: the credited notable may be across the box
            var thank = era == Era.Camp ? ActivityKind.LookUp : ActivityKind.Wave;
            float arrive = lateOwner ? LateOwnerArrives : work;
            B("come over", RelicCast.Owner, 0f, arrive, ActivityKind.Point);
            B("work", RelicCast.Owner, arrive, thanks, w.work, w.prop);
            B("thanks", RelicCast.Owner, thanks, 1f, thank, Prop.None, true);
            B("come over", RelicCast.Grown, 0f, work, ActivityKind.Watch);
            B("help", RelicCast.Grown, work, thanks, w.help, w.helpProp);
            B("thanks", RelicCast.Grown, thanks, 1f, ActivityKind.Clap);
            B("come over", RelicCast.Kids, 0f, work, ActivityKind.Watch);
            B("play", RelicCast.Kids, work, thanks, w.kids);
            B("cheer", RelicCast.Kids, thanks, 1f, ActivityKind.Cheer, Prop.None, true);
            return d;
        }

        /// <summary>The chronicle line crediting an answered shaping (a building's or a harvest's), or null. Unlike
        /// <see cref="SimCore.AnswerOf"/> it finds the harvest line too (no building behind it).</summary>
        public static ChronicleEntry EntryOf(SimCore sim, Nudge n)
        {
            if (sim == null || n == null || !n.credited) return null;
            var ch = sim.State.chronicle;
            for (int i = ch.Count - 1; i >= 0; i--)
            {
                var e = ch[i];
                if (!e.keeper || e.month != n.answerMonth) continue;
                if (n.buildingId >= 0 ? e.buildingId == n.buildingId : e.buildingId < 0) return e;
            }
            return null;
        }

        /// <summary>The notable the answer is credited to: the one the Keeper named (a wish's author), else the chronicle's.</summary>
        public static int CreditedNotableId(SimCore sim, Nudge n)
        {
            if (n == null) return -1;
            if (n.creditNotableId >= 0) return n.creditNotableId;
            return EntryOf(sim, n)?.notableId ?? -1;
        }
    }
}
