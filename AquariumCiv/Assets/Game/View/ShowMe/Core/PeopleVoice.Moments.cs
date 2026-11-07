using System.Collections.Generic;
using AquariumCiv.Sim;

namespace AquariumCiv.View.ShowMe
{
    /// <summary>
    /// t1-moments: what the people say while a staged moment plays (visit-loop 2.1; tester run 1: the welcome festival's bubble read
    /// "I'm thinking about getting up. It's going well."). A few short lines per kind, all true in the tank: food, dancing, thanks, a
    /// vow, a baby. The principals say theirs (the couple, the parent, the new chief); the crowd says the rest.
    /// </summary>
    public static partial class PeopleVoice
    {
        /// <summary>The lines for a moment of <paramref name="kind"/>: (text, who) where who is -1 for the crowd, else a notable id.
        /// <paramref name="a"/> and <paramref name="b"/> are the moment's principals (the couple; the child and the parent; the new
        /// chief). Deterministic order; the caller says each at most once a moment.</summary>
        public static List<(string text, int speaker)> MomentLines(MomentKind kind, int a, int b)
        {
            var l = new List<(string, int)>();
            switch (kind)
            {
                case MomentKind.Festival:
                    l.Add(("Save me a dance!", -1));
                    l.Add(("There's plenty. Eat up!", -1));
                    l.Add(("Pass the bread, please.", -1));
                    l.Add(("Thank you for the fire.", -1));
                    l.Add(("This stew is the best yet.", -1));
                    l.Add(("Round again! Round again!", -1));
                    break;
                case MomentKind.Wedding:
                    if (a >= 0) l.Add(("I do. I really do.", a));
                    l.Add(("Throw the petals!", -1));
                    if (b >= 0) l.Add(("My hands are shaking. Hold them.", b));
                    l.Add(("Look at the two of them!", -1));
                    l.Add(("What a lovely arch.", -1));
                    l.Add(("Dance with us!", -1));
                    break;
                case MomentKind.Newborn:
                    if (b >= 0) l.Add(("Look at that face. Look!", b));
                    l.Add(("Welcome, little one!", -1));
                    l.Add(("Ten tiny toes!", -1));
                    if (b >= 0) l.Add(("Hold still. Everyone wants to see you.", b));
                    l.Add(("So small!", -1));
                    break;
                case MomentKind.NewLeader:
                    if (a >= 0) l.Add(("I'll do my best for all of us.", a));
                    l.Add(("Long live the new chief!", -1));
                    l.Add(("A good seat. Wear it well.", -1));
                    break;
            }
            return l;
        }
    }
}
