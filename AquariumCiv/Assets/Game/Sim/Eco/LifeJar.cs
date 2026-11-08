using System;

namespace AquariumCiv.Sim.Eco
{
    /// <summary>
    /// #417 (placed-life spec §1-§3): the six kinds of life the Keeper places. Small life (crickets, birds, frogs...) still comes
    /// by itself and is not here. Saved by name (<see cref="LifeJarEntry.species"/>), so new species can be added at the end
    /// or anywhere without breaking a save.
    /// </summary>
    public enum LifeSpecies { Thicket, Rabbits, Deer, Ducks, Fish, Wolves }

    /// <summary>A jar's state in the save (spec §2.1). <c>Held</c> (in the Keeper's hand, a ghost shown) is the view's alone and
    /// is never saved; a restock (§4.3) is <see cref="Ready"/> again with <see cref="LifeJarEntry.restocks"/> counted.</summary>
    public enum JarState { Locked = 0, Ready = 1, Placed = 2 }

    /// <summary>One species' jar in the save (spec §9). Plain fields for JsonUtility.</summary>
    [Serializable]
    public class LifeJarEntry
    {
        /// <summary>The <see cref="LifeSpecies"/> name.</summary>
        public string species;
        /// <summary>A <see cref="JarState"/>.</summary>
        public int state;
        /// <summary>The game month the jar last opened (-1 never), the month of the last placing, and the month the species
        /// came down to its floor (-1 = not at its floor now; restock after <see cref="EcoTuning.jarRestockMonths"/>).</summary>
        public long readyMonth = -1, placedMonth = -1, atFloorSince = -1;
        /// <summary>The site cell of the last placing (-1 none); how many founding groups were placed; how many restocks opened.</summary>
        public int placedCell = -1, placings, restocks;
        /// <summary>The view showed the ready wiggle (cleared with SimCore.AckJarReady). A jar that opened while the Keeper was
        /// away keeps this false, so the wiggle waits for the next open (spec §2.2).</summary>
        public bool readySeen;
        /// <summary>The view played the arrival of the last placing (SimCore.AckLifeArrival). True when nothing waits.</summary>
        public bool arrivalSeen = true;
        /// <summary>#422 restraint (spec §4.3): the game month a notable ruled on this species' take (-1 = not yet; once per species).
        /// From then on the people's take of it pauses in spring.</summary>
        public long ruledMonth = -1;
        /// <summary>#422: who ruled (a notable id, -1 the eldest unnamed) and the chronicle line of the ruling (-1 none): the view's
        /// ruling beat reads them through SimCore.RulingOf.</summary>
        public int ruledBy = -1, ruledEntryId = -1;
        /// <summary>#422: a take brought the species down to its floor during this season (a species hunted down each season is "at
        /// its floor" even if it breeds back by the season's end). Read and cleared at the end of each season.</summary>
        public bool floorHit;

        public JarState State => (JarState)state;
    }

    /// <summary>A founding group was placed (SimCore.LifePlaced): what, where, when, and whether it was a restock.</summary>
    public struct LifePlacedInfo
    {
        public LifeSpecies species;
        public int cell;
        public long month;
        public bool restock;
    }

    public static class LifeJar
    {
        public static readonly LifeSpecies[] All = (LifeSpecies[])Enum.GetValues(typeof(LifeSpecies));

        /// <summary>Spec §4.3: the thicket never needs a restock (a picked bush recovers) and the wolves never drop below their pair.</summary>
        public static bool Restocks(LifeSpecies s) => s != LifeSpecies.Thicket && s != LifeSpecies.Wolves;

        /// <summary>The rung below on the ladder (spec §2.3), or null for the first jar. Fish also wait on the ducks; wolves on the deer.</summary>
        public static LifeSpecies? RungBelow(LifeSpecies s) => s switch
        {
            LifeSpecies.Rabbits => LifeSpecies.Thicket,
            LifeSpecies.Deer => LifeSpecies.Rabbits,
            LifeSpecies.Ducks => LifeSpecies.Rabbits,
            LifeSpecies.Fish => LifeSpecies.Ducks,
            LifeSpecies.Wolves => LifeSpecies.Deer,
            _ => null,
        };
    }
}
