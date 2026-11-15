using System;
using System.Collections.Generic;

namespace AquariumCiv.Sim.Eco
{
    /// <summary>#709 (B71): where the nest is in its year. None = no nest this spring yet (or the thicket is gone); Building = the parent is
    /// bringing twigs (first month of spring); Eggs = the parent sits, the eggs are in; Chicks = the end of spring, the eggs hatched
    /// (only while <see cref="EcoTuning.nestChicks"/> is above 0); Empty = summer on: everyone gone, the nest stays.</summary>
    public enum NestPhase { None = 0, Building = 1, Eggs = 2, Chicks = 3, Empty = 4 }

    /// <summary>What happened at the nest, as the view and the chronicle hear it.</summary>
    public enum NestEventKind { Built, Moved, Laid, EggTaken, Hatched, ParentLeft, Gone, KidsLooked }

    /// <summary>#709: the one nest. Saved in <see cref="EcoState.nest"/>; an old save has none and loads as "no nest yet" (built = false).</summary>
    [Serializable]
    public class Nest
    {
        /// <summary>A nest stands (in a bush of a thicket). False = no nest yet, or it went with its thicket.</summary>
        public bool built;
        /// <summary>The thicket's centre cell (a thicket is named by it) and the bush cell the nest sits in.</summary>
        public int thicketCell = -1, cell = -1;
        public int phase;
        /// <summary>Eggs in the nest now, eggs the people took this spring, chicks now, and how many times the kids looked in.</summary>
        public int eggs, eggsTaken, chicks, looks;
        /// <summary>The parent bird is at the nest (spring only).</summary>
        public bool parent;
        /// <summary>The spring this cycle belongs to ((month + season shift) / 12), -1 = none yet.</summary>
        public int year = -1;
        /// <summary>Bit 0: the first nest was told, 1: the first egg taken, 2: the first chicks.</summary>
        public int told;

        public NestPhase Phase => (NestPhase)phase;
    }

    /// <summary>One thing that happened at the nest (drained by SimCore).</summary>
    public struct NestEvent
    {
        public NestEventKind kind; public int cell;
        public NestEvent(NestEventKind k, int c) { kind = k; cell = c; }
    }
}
