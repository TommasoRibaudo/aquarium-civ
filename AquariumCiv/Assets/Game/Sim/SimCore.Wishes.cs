using System;
using System.Linq;

namespace AquariumCiv.Sim
{
    public enum WishState { Open, Given, Lapsed, Met }

    /// <summary>A notable's wish for a shaping (#83). #718: nobody makes one any more; the data stays so an old save loads.</summary>
    [Serializable]
    public class Wish
    {
        public int id;
        public int notableId;
        public string kind = "";        // SimCore.NudgeKind
        public float x, z;              // where it points
        public long month;              // raised
        public long endMonth = -1;      // given, lapsed or met
        public int entryId = -1;        // its chronicle entry (kind "wish")
        public int nudgeId = -1;        // the shaping that answered it (Given)
        public WishState state;
    }

    /// <summary>
    /// #718 (the user, 2026-11-14: the text only shaping makes is deleted): wishes are gone. Nobody makes a new one, no wish card
    /// is shown, and an old save's open wish lapses quietly on the first month it runs.
    /// </summary>
    public partial class SimCore
    {
        public Wish FindWish(int id) => State.wishes.FirstOrDefault(w => w.id == id);

        /// <summary>An old save's open wish (at most one), or null.</summary>
        Wish OpenWish => State.wishes.FirstOrDefault(w => w.state == WishState.Open);

        /// <summary>A wish entry is never shown as a card (#718); a dilemma's card only while it is open.</summary>
        bool WishCardLive(ChronicleEntry e)
        {
            if (e.kind == "dilemma") return DilemmaCardLive(e);
            return e.kind != "wish";
        }

        /// <summary>Every month: an old save's open wish lapses.</summary>
        void WishStep()
        {
            var open = OpenWish;
            if (open != null) CloseWish(open, WishState.Lapsed);
        }

        void CloseWish(Wish w, WishState s) { w.state = s; w.endMonth = State.month; }
    }
}
