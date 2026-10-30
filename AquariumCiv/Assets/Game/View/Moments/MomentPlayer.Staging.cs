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
                if (b.Has(BeatAction.GreenSpreads) || b.Has(BeatAction.SquareSpreads))
                {
                    // camp: fire ring → the green, the well rises beside the old hearth;
                    // village: the green → the cobbled square, stalls round its rim, the well becomes the fountain
                    src.MomentGreen();
                    if (b.Has(BeatAction.SquareSpreads)) src.SetCobbleReach(SquareReach);
                    PuffAt(gx, gz, 1.6f);
                    PuffAt(gx + SimLayout.FirstWellDX, gz + SimLayout.FirstWellDZ, 1f);
                }
                if (b.Has(BeatAction.ClearHearth)) PuffAt(gx, gz, 1.2f); // the old camp hearth lifted out (#91 stages the stone)
                if (b.Has(BeatAction.Cheer)) src.MomentAll(); // nothing is left in the old look when the era moves
            }
        }

        /// <summary>One lot rebuilt: the knock a note higher each time and a light haptic tick (placeholder: a log line
        /// until the moment's audio lands).</summary>
        void WaveKnock(RebuildWave.Step s)
        {
            KnocksPlayed++;
        }

        /// <summary>The cobbles at the square beat: the square itself (world units from the green's centre) [est].</summary>
        public const float SquareReach = 0.45f;
        /// <summary>How far the cobbles unroll along the main paths by the end of the town wave: the whole box [est].</summary>
        public const float CobbleMaxReach = 4.5f;

        /// <summary>The town wave: the cobbles unroll from the square along the main paths with the wave, in steps (each
        /// step rebuilds the touched chunks).</summary>
        void UnrollCobbles()
        {
            if (wave == null || Director.Def == null || Director.Def.toEra != Era.Town) return;
            var src = SimLayoutSource.Find();
            if (src == null || wave.Steps.Count == 0) return;
            float a = wave.Steps[0].time, b = wave.Steps[wave.Steps.Count - 1].time;
            float u = b > a ? Mathf.Clamp01((Director.Time - a) / (b - a)) : 1f;
            float reach = Mathf.Lerp(SquareReach, CobbleMaxReach, u);
            reach = Mathf.Floor(reach / 0.3f) * 0.3f; // a refresh every ~0.3 units, not every frame
            if (reach > src.CobbleReach + 1e-3f) src.SetCobbleReach(reach);
        }

        /// <summary>Lots rebuilt by this moment's wave so far (tests, debug status).</summary>
        public int KnocksPlayed { get; private set; }
    }
}
