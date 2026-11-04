using System;
using AquariumCiv.Sim.Eco;

namespace AquariumCiv.Sim
{
    /// <summary>
    /// #283 phase 1: the sim side of the hunt. The planner (Life/LifePlanner.Hunt.cs) decides who hunts which rabbit and how it
    /// ends; here live the herd's floor, the hides a catch leaves (drying by the fire, saved; #284 turns them into tents) and
    /// the resolver's mark, so that a hunt settles exactly once even across a save. A catch itself is
    /// <see cref="CatchRabbit"/>. With the ecology off none of this does anything.
    /// </summary>
    public partial class SimCore
    {
        /// <summary>The herd is never hunted below this many (the model keeps it at 2 at the least, so a catch regrows).</summary>
        public const int HuntFloor = 2;

        /// <summary>How many rabbits the hunt may still take: the sim count minus <see cref="HuntFloor"/>. 0 with the ecology off.</summary>
        public int HuntSurplus => EcologyOn ? Math.Max(0, EcoRabbitCount - HuntFloor) : 0;

        /// <summary>Hides drying by the fire now (a catch adds one; tents take them, #284).</summary>
        public int HidesDrying => EcologyOn ? State.eco.hidesDrying : 0;
        /// <summary>All the hides the hunt has brought in.</summary>
        public int HidesTotal => EcologyOn ? State.eco.hidesTotal : 0;

        /// <summary>#284: tents take hides. Returns how many were taken (at most <paramref name="n"/>).</summary>
        public int TakeHides(int n)
        {
            if (!EcologyOn || n <= 0) return 0;
            int took = Math.Min(n, State.eco.hidesDrying);
            State.eco.hidesDrying -= took;
            return took;
        }

        /// <summary>A hunt's catch, settled in sim time: the herd's count goes down by one (<see cref="CatchRabbit"/>) and a hide
        /// is drying by the fire. False, and nothing changes, when the herd is at its floor (the hunt then counts as a miss).</summary>
        public bool HuntCatch()
        {
            if (!EcologyOn || HuntSurplus <= 0 || !CatchRabbit()) return false;
            State.eco.hidesDrying++;
            State.eco.hidesTotal++;
            return true;
        }

        /// <summary>How many of the hunts of view day <paramref name="day"/> are already settled (saved, so a reload doesn't settle them twice).</summary>
        public int HuntsSettled(long day) => EcologyOn && State.eco.huntDay == day ? State.eco.huntDone : 0;

        /// <summary>One more hunt of that day is settled. A new day starts the count again.</summary>
        public void HuntSettled(long day)
        {
            if (!EcologyOn) return;
            if (State.eco.huntDay != day) { State.eco.huntDay = day; State.eco.huntDone = 0; }
            State.eco.huntDone++;
        }

        /// <summary>Debug ("+N hides"): hides drying by the fire, for #284's tents.</summary>
        public int DebugAddHides(int n)
        {
            if (!EcologyOn || n <= 0) return HidesDrying;
            State.eco.hidesDrying += n; State.eco.hidesTotal += n;
            return State.eco.hidesDrying;
        }
    }
}
