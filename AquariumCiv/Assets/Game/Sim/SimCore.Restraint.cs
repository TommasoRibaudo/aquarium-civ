using System;
using System.Linq;
using AquariumCiv.Sim.Eco;

namespace AquariumCiv.Sim
{
    /// <summary>
    /// #422 shy animals and the people's restraint (docs/design/placed-life.md §4.2, §4.3; research 18 §6): no species ever dies out.
    /// <list type="bullet">
    /// <item><b>Floors:</b> every taker goes through <see cref="EcoModel.TryTake"/>, which never crosses a species' floor (rabbits 2,
    /// deer 2, ducks 2, fish 3): the hunters (<see cref="HuntCatch"/>, <see cref="DeerHuntCatch"/>), the egg gatherers
    /// (<see cref="TakeEgg"/>), the fishers (<see cref="FishCatch"/>), and the wolves (<see cref="WolfTake"/>, the hook for #437).</item>
    /// <item><b>Shy:</b> a species at its floor after takes is <see cref="LifeShy"/>; its home (<see cref="LifeHomeCell"/>) moves to
    /// the far side of its ground from the camp, and the view shows it mostly at dawn. Fewer seen, none gone.</item>
    /// <item><b>Restraint:</b> the first season a species sits at its floor after takes (the ducks: a spring whose nests were emptied),
    /// a notable rules on it, once per species: a history line and a held moment at the fire (<see cref="MomentKind.Ruling"/>). From
    /// then on the people's take of it pauses in spring (<see cref="LifeTakePaused"/>).</item>
    /// <item><b>Restock:</b> two game years at the floor opens the jar again (EcoModel.UpdateJars), for every species that restocks.</item>
    /// </list>
    /// </summary>
    public partial class SimCore
    {
        /// <summary>The chronicle kind of a ruling line; it queues a <see cref="MomentKind.Ruling"/> moment.</summary>
        public const string RulingKind = "ruling";

        /// <summary>A notable ruled on a species' take (the history line is written; the moment is queued when moments are on).</summary>
        public event Action<LifeSpecies> LifeRuled;

        /// <summary>The species is at its floor after takes: the view keeps it to the far side of its ground, mostly at dawn.</summary>
        public bool LifeShy(LifeSpecies s) => EcologyOn && EcoM.Shy(s);

        /// <summary>Where the species lives now: its placed home, or the far side of its ground while shy. -1 when none is placed.</summary>
        public int LifeHomeCell(LifeSpecies s) => EcologyOn ? EcoM.HomeCell(s) : -1;

        /// <summary>The species' count the floors read (the wild ducks only), or -1 for the thicket and the wolves (#437).</summary>
        public float LifePopulation(LifeSpecies s) => EcologyOn ? EcoM.Population(s) : -1f;

        /// <summary>The floor no taker crosses.</summary>
        public static float LifeFloor(LifeSpecies s) => EcoModel.Floor(s);

        /// <summary>A notable has ruled on the species' take (the month, or -1).</summary>
        public long LifeRuledMonth(LifeSpecies s) => EcologyOn ? EcoM.Jar(s).ruledMonth : -1;

        /// <summary>The people's take of the species pauses now: it is spring and a notable has ruled on it.</summary>
        public bool LifeTakePaused(LifeSpecies s) => EcologyOn && EcoM.SpringPaused(s, LifeTaker.Hunters, State.month);

        /// <summary>The hook for the wolves (#437): a wolf kill of a rabbit, a deer or a wild duck, within the floors (the people's spring
        /// rule does not bind the wolves). False, and nothing changes, when the prey has none to spare.</summary>
        public bool WolfTake(LifeSpecies prey) => EcologyOn && EcoM.TryTake(prey, LifeTaker.Wolves, State.month, out _);

        /// <summary>The rulings made since last time: the history line in the people's words (it queues the moment), then the event.</summary>
        void RulingsTell()
        {
            var m = EcoM;
            if (m.newRulings.Count == 0) return;
            var made = m.newRulings.ToArray();
            m.newRulings.Clear();
            foreach (var s in made)
            {
                var who = RulingNotable();
                string name = who?.FullName ?? "The eldest";
                Chronicle(RulingKind, RulingLine(s, name), 0.8f, who?.id ?? -1);
                LifeRuled?.Invoke(s);
            }
        }

        /// <summary>Who rules: the era's leader, else the eldest grown-up.</summary>
        Notable RulingNotable()
        {
            var leader = Alive(SeatOf(LeaderOf(State.era))?.holderId ?? -1);
            if (leader != null) return leader;
            return State.notables.Where(n => n.alive && n.age >= 16f).OrderByDescending(n => n.age).ThenBy(n => n.id).FirstOrDefault();
        }

        /// <summary>The ruling, in the people's words (spec §4.3; closed world). {name} is the notable.</summary>
        static string RulingLine(LifeSpecies s, string name) => s switch
        {
            LifeSpecies.Deer => $"There were only a few deer left at the edge of the trees. {name} stood up at the fire and said: leave the does in spring. Nobody hunted deer in spring after that.",
            LifeSpecies.Rabbits => $"The rabbits had grown few. {name} said at the fire: leave the young ones in spring. The hunters stayed home in spring after that.",
            LifeSpecies.Ducks => $"The nests by the water had been emptied, and no ducklings came. {name} said: leave the nests till summer. Nobody took an egg in spring after that.",
            LifeSpecies.Fish => $"The fishers came back with little. {name} said: let the small ones go. Nobody fished in spring after that.",
            _ => $"{name} said the people should take less, and they did.",
        };

        // ---------- debug (the panel's "Placed life" block) ----------
        /// <summary>Debug ("species to its floor"): the species drops to its floor now, as if hunted down (it counts as taken, so it is
        /// shy at once and the ruling and the restock follow in their time). Places it first if it isn't. False for the thicket and the
        /// wolves, or when nothing could be placed.</summary>
        public bool DebugToFloor(LifeSpecies s)
        {
            if (!EcologyOn || !LifeJar.Restocks(s)) return false;
            if (!LifeIsPlaced(s) && DebugPlaceNearCamp(s) < 0) return false;
            var e = State.eco;
            switch (s)
            {
                case LifeSpecies.Rabbits: e.rabbits = LifeFloor(s); e.rabbitsCaught = Math.Max(1, e.rabbitsCaught); break;
                case LifeSpecies.Deer: e.deerStags = 1; e.deerDoes = 1; e.deerTaken = Math.Max(1, e.deerTaken); break;
                case LifeSpecies.Ducks: e.ducks = (int)LifeFloor(s); e.duckEggs = 0; e.ducksTaken = Math.Max(1, e.ducksTaken); break;
                case LifeSpecies.Fish: e.fish = (int)LifeFloor(s); e.fishCaught = Math.Max(1, e.fishCaught); break;
            }
            EcoM.Jar(s).floorHit = true;
            return true;
        }

        /// <summary>Debug ("restraint now"): a notable rules on the species now (the line, the moment, the spring pause from now on).
        /// Places it first if it isn't. False when it has ruled already.</summary>
        public bool DebugRestraintNow(LifeSpecies s)
        {
            if (!EcologyOn || !LifeJar.Restocks(s)) return false;
            if (!LifeIsPlaced(s) && DebugPlaceNearCamp(s) < 0) return false;
            if (!EcoM.DebugRule(s, State.month)) return false;
            EcoTell();
            return true;
        }
    }
}
