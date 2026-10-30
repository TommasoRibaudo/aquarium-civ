using AquariumCiv.Life;
using AquariumCiv.Sim;
using AquariumCiv.View.Moments;
using UnityEngine;

namespace AquariumCiv.View
{
    /// <summary>
    /// The camp → village staging (era-moment.md §4, #89) on the director's beats: the people gather at the fire and the
    /// chief speaks (the planner's evening-talk scene), the fire ring turns into the green with its well (a puff, the box's
    /// green look), the wave rebuilds each lot in place in the village look (<see cref="SimLayoutSource.BeginMomentLook"/>,
    /// driven per lot by the <see cref="RebuildWave"/>), the sash (a role moment at the hearth), the cheer (everyone cheers
    /// at the glass; nothing is left in the camp look), then the festival. Village → town (#90) reuses the same hooks.
    /// </summary>
    public partial class MomentPlayer
    {
        void Stage(MomentBeat b, int index)
        {
            var src = SimLayoutSource.Find();
            var pv = PeopleView.Find();
            var planner = pv != null ? pv.Planner : null;
            var sim = Sim;
            float beatSeconds = Director.BeatEnd(index) - Director.BeatStart(index);
            int leaderId = sim?.HolderOf(SimCore.LeaderOf(sim.State.era))?.id ?? -1;

            if (planner != null && sim != null)
            {
                var now = pv.Now;
                float Hours(float realSeconds) => LifeClock.ViewHoursFor(now.hour, realSeconds);
                if (b.Has(BeatAction.Gather))
                {
                    // Everyone walks to the fire; the chief (or the eldest) speaks there until the green spreads.
                    planner.ClearLive();
                    var green = Director.Def.Beat(Director.Def.toEra == Era.Village ? "green" : "square");
                    float until = green != null ? green.start - b.start : beatSeconds;
                    planner.StartScene(SceneKind.EveningTalk, now, leaderId, hours: Hours(until));
                }
                if (b.Has(BeatAction.PassSash) || b.Has(BeatAction.Vote) || b.Has(BeatAction.LordChair))
                    planner.StartScene(SceneKind.RoleMoment, now, leaderId, hours: Hours(beatSeconds));
                if (b.Has(BeatAction.Cheer))
                {
                    planner.ClearLive();
                    planner.React(KeeperGesture.FullClean, now, seconds: 4f);
                }
                if (b.Has(BeatAction.FestivalStart))
                {
                    planner.ClearLive();
                    planner.StartScene(SceneKind.Festival, now);
                }
                if (b.Has(BeatAction.RebuildWave)) planner.ClearLive(); // back to their lots: the wave passes them at home and at work
            }

            if (src != null && sim?.Settlement != null)
            {
                float gx = sim.Settlement.Map.greenX, gz = sim.Settlement.Map.greenZ;
                if (b.Has(BeatAction.GreenSpreads))
                {
                    src.MomentGreen(); // fire ring → the green; the well rises beside the old hearth
                    PuffAt(gx, gz, 1.6f);
                    PuffAt(gx + SimLayout.FirstWellDX, gz + SimLayout.FirstWellDZ, 1f);
                }
                if (b.Has(BeatAction.Cheer)) src.MomentAll(); // nothing is left in the old look when the era moves
            }
        }

        /// <summary>One lot rebuilt: the knock a note higher each time and a light haptic tick (placeholder: a log line
        /// until the moment's audio lands).</summary>
        void WaveKnock(RebuildWave.Step s)
        {
            KnocksPlayed++;
        }

        /// <summary>Lots rebuilt by this moment's wave so far (tests, debug status).</summary>
        public int KnocksPlayed { get; private set; }
    }
}
