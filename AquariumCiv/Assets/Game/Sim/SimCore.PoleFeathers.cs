using System;
using System.Collections.Generic;

namespace AquariumCiv.Sim
{
    /// <summary>#692 (B31.a): one feather tied on the feather pole after a good event. Saved with the box.</summary>
    [Serializable]
    public class PoleFeather
    {
        /// <summary><see cref="PoleFeatherKind"/>: what it was for (its colour).</summary>
        public int kind;
        /// <summary>The month (view day) of the event; the feather is tied at a dusk on or after it.</summary>
        public long month;
        /// <summary>The people it is for (the couple, the parent; −1 none).</summary>
        public int forA = -1, forB = -1;
        /// <summary>Tied on the pole (the view played the tie, or it was tied unseen at a later dusk).</summary>
        public bool tied;
    }

    public static class PoleFeatherKind
    {
        /// <summary>White for a newborn, red for a marriage, ochre for the village (Q11: the user, 2026-11-15, "feather colours OK").</summary>
        public const int Newborn = 0, Marriage = 1, Village = 2;
    }

    /// <summary>
    /// #692 (B31.a): the feather pole fills up over the years. A wedding, a birth and the village's founding each add a feather while the
    /// pole stands (<see cref="FeatherPoleProp"/>); the view ties it at the next dusk (the chief holds it up and ties it on, everyone prays)
    /// and marks it tied. A feather the view never got to (the box closed, nobody near) is tied unseen two months on, so the pole never
    /// falls behind. The pole draws the tied ones (up to <see cref="PoleFeatherCap"/>; after that a new one recolours the oldest).
    /// </summary>
    public sealed partial class SimCore
    {
        public const int PoleFeatherCap = 12;
        /// <summary>A feather not tied this many months after its event is tied unseen.</summary>
        public const int PoleFeatherLate = 2;

        /// <summary>The pole's feathers, oldest first.</summary>
        public IReadOnlyList<PoleFeather> PoleFeathers => State.poleFeathers;
        public int PoleFeathersTied { get { int n = 0; foreach (var f in State.poleFeathers) if (f.tied) n++; return n; } }

        /// <summary>The oldest feather waiting to be tied (index, −1 none).</summary>
        public int PendingPoleFeather()
        {
            for (int i = 0; i < State.poleFeathers.Count; i++) if (!State.poleFeathers[i].tied) return i;
            return -1;
        }

        public void MarkPoleFeatherTied(int index)
        {
            if (index >= 0 && index < State.poleFeathers.Count) State.poleFeathers[index].tied = true;
        }

        /// <summary>Adds a feather (only while the pole stands). Returns its index, −1 none.</summary>
        public int AddPoleFeather(int kind, int forA = -1, int forB = -1)
        {
            if (FeatherPoleProp == null) return -1;
            State.poleFeathers.Add(new PoleFeather { kind = kind, month = State.month, forA = forA, forB = forB });
            return State.poleFeathers.Count - 1;
        }

        /// <summary>The kinds the pole draws, oldest first: the last <see cref="PoleFeatherCap"/> tied ones in their slots, a new one past the
        /// cap taking the oldest slot (so slot <c>i</c> holds feather <c>i</c>, <c>i + 12</c>, ...).</summary>
        public static int[] PoleSlots(IReadOnlyList<PoleFeather> feathers)
        {
            var tied = new List<int>();
            foreach (var f in feathers) if (f.tied) tied.Add(f.kind);
            var slots = new int[Math.Min(tied.Count, PoleFeatherCap)];
            for (int i = 0; i < tied.Count; i++) slots[i % PoleFeatherCap] = tied[i];
            return slots;
        }

        /// <summary>The chronicle's good events add a feather (called from <see cref="Record"/>).</summary>
        void FeatherFromLine(ChronicleEntry e)
        {
            switch (e.kind)
            {
                case "wedding": AddPoleFeather(PoleFeatherKind.Marriage, e.notableId, e.otherNotableId); break;
                case "birth":
                {
                    var baby = FindNotable(e.notableId);
                    AddPoleFeather(PoleFeatherKind.Newborn, baby != null ? baby.parentId : -1, e.notableId);
                    break;
                }
                case "era": if (State.era == Era.Village) AddPoleFeather(PoleFeatherKind.Village); break;
            }
        }

        /// <summary>Once a month: a feather waiting since <see cref="PoleFeatherLate"/> months is tied unseen.</summary>
        void TiePoleFeathersLate()
        {
            foreach (var f in State.poleFeathers) if (!f.tied && State.month - f.month >= PoleFeatherLate) f.tied = true;
        }

        /// <summary>Debug ("Feather pole: tie a feather"): the pole stands (made if none) and a feather of <paramref name="kind"/> waits for tonight.</summary>
        public int DebugPoleFeather(int kind)
        {
            if (!DebugEnsureFeatherPole()) return -1;
            return AddPoleFeather(kind);
        }
    }
}
