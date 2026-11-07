using System;
using System.Linq;

namespace AquariumCiv.Sim
{
    /// <summary>
    /// t3-welcome: the "welcome when nothing waits" fallback of the moment director (visit-loop.md 2.1-2.2). On an open after
    /// <see cref="WelcomeRules.MinAwayHours"/> away with no ceremony waiting, the box owes the Keeper one small live greeting. The sim
    /// only decides that it is owed and which kinds fit what is true in the box (the diff since the last watched step); the view stages
    /// it (HeldMomentStage) and reports which one played. No dice: the rotation is a hash.
    /// </summary>
    public partial class SimCore
    {
        // Not saved: the cheat's "even if a moment waits".
        bool greetingForced;

        WelcomeMemo Memo => State.welcomeMemo ??= new WelcomeMemo();

        /// <summary>The greeting owed this visit (None = none).</summary>
        public WelcomeKind GreetingDue => MomentPacing.greetingDue ? (WelcomeKind)MomentPacing.greetingKind : WelcomeKind.None;
        /// <summary>The cheat asked for it: a waiting moment does not cancel it.</summary>
        public bool GreetingForced => greetingForced;

        /// <summary>What is true in the box that the people could show.</summary>
        public WelcomeFacts GreetingFacts
        {
            get
            {
                var memo = Memo;
                var f = new WelcomeFacts { newBuildingId = -1, hideDrying = HidesDrying > 0 };
                if (memo.valid)
                {
                    int best = -1;
                    foreach (var b in State.buildings) if (b.id > memo.maxBuildingId && b.id > best && b.type != BuildingType.Camp) best = b.id;
                    f.newBuildingId = best;
                }
                return f;
            }
        }

        /// <summary>From EnsureMomentVisit, on a fresh visit that was <paramref name="awayHours"/> long.</summary>
        void WelcomeOnNewVisit(double awayHours)
        {
            var p = MomentPacing;
            p.greetingDue = false;
            p.greetingKind = 0;
            greetingForced = false;
            if (awayHours < WelcomeRules.MinAwayHours - 1e-9 || p.welcomeDue || Moments.Any(m => m.state == HeldMomentState.Waiting)) return;
            ChooseGreeting();
        }

        WelcomeKind[] AllOptions()
        {
            var p = MomentPacing;
            return WelcomeRules.Options(GreetingFacts, (WelcomeKind)p.lastGreeting, State.seed ^ (p.visits * 7919));
        }

        void ChooseGreeting()
        {
            var p = MomentPacing;
            p.greetingKind = (int)AllOptions()[0];
            p.greetingDue = true;
        }

        /// <summary>The kinds the view may try for this greeting, the chosen one first (the others when it cannot be cast: no kids, nobody free).</summary>
        public WelcomeKind[] GreetingOptions()
        {
            var p = MomentPacing;
            if (!p.greetingDue) return Array.Empty<WelcomeKind>();
            var first = (WelcomeKind)p.greetingKind;
            return AllOptions().OrderBy(k => k == first ? 0 : 1).ToArray();
        }

        /// <summary>The view staged this greeting: it is over as owed, and it is the one not repeated next time.</summary>
        public void GreetingPlayed(WelcomeKind kind)
        {
            MomentPacing.lastGreeting = (int)kind;
            GreetingDrop();
        }

        /// <summary>No greeting this visit (a ceremony came to wait, or it could not be staged).</summary>
        public void GreetingDrop()
        {
            var p = MomentPacing;
            p.greetingDue = false;
            p.greetingKind = 0;
            greetingForced = false;
        }

        /// <summary>The box as the next open diffs against (from every watched step).</summary>
        void WelcomeRemember()
        {
            var memo = Memo;
            int max = 0;
            foreach (var b in State.buildings) if (b.id > max) max = b.id;
            memo.maxBuildingId = max;
            memo.buildings = State.buildings.Count;
            memo.valid = true;
        }

        /// <summary>Debug ("Welcome now"): a greeting is owed right now, whatever the time away or a waiting moment. Returns what it will try first.</summary>
        public string DebugGreetNow()
        {
            if (!Config.moments) return "moments off";
            if (Moments.Any(m => m.state == HeldMomentState.Playing)) return "a moment is playing: wait for it to end";
            ChooseGreeting();
            greetingForced = true;
            return "greeting owed: " + (WelcomeKind)MomentPacing.greetingKind;
        }
    }
}
