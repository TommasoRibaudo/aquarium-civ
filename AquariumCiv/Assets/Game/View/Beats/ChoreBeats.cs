// Sign-off 2026-11-14 (#651/#677, the user: "put effort in the animations, they have to read properly"). Pure: Mathf and DollPose
// only, so Tools/AnimBench compiles it.
using UnityEngine;

namespace AquariumCiv.View
{
    /// <summary>
    /// The camp's hand work, each move told on a thing you can see:
    /// <list type="bullet">
    /// <item><b>Feeding the grass fire</b> (<see cref="Feed"/>): crouch at the grass heap by the fire and gather an armful, stand and step
    /// in hugging it, crouch at the flames and push it in with both hands (Contact: the fire's sparks flare), hold the hands out to the
    /// heat a moment, then back up. The armful shows only while it is held (<see cref="FeedHolds"/>). Never a stir over the flames: that
    /// read as stroking the fire.</item>
    /// <item><b>Scraping a hide</b> on the rack (<see cref="Scrape"/>): both hands on the scraper stone, raised to the top of the hide,
    /// then a hard push down along it with the body behind it (Contact: shavings fall), a short hold, and the lift back up.</item>
    /// <item><b>Sorting the basket</b> at the pile (<see cref="Sort"/>): the basket held in the left hand over the pile, the right hand dips
    /// in, brings a handful up to look at it, and lays it on the pile.</item>
    /// <item><b>Mending a bedroll</b> at the tent (<see cref="Mend"/>): seated with the hide spread in front, the left hand holds the edge,
    /// the right pushes the awl through and pulls the thread up and out, high, every stitch.</item>
    /// </list>
    /// Each loops on its own clock with a per-person offset, so two at one fire never move in step.
    /// </summary>
    public static class ChoreBeats
    {
        /// <summary>Seconds into a looping chore's beat at the doll's pose clock <paramref name="t"/> (which already carries the
        /// person's own offset), at the plan's pace (clamped as <see cref="DollMotion.Evaluate"/> does).</summary>
        public static float Clock(Beat beat, float t, float pace) => Mathf.Repeat(t * Mathf.Clamp(pace <= 0f ? 1f : pace, 0.5f, 1.5f), beat.Duration);

        // ---------------------------------------------------------------- feeding the fire
        /// <summary>One feed, gather to back-off (seconds).</summary>
        public static readonly Beat FeedBeat = new Beat(
            // gather: turned round to the grass heap behind them (PeopleView kneels them between the fire and it), a crouch, arms down and in
            Beat.Step(BeatPhase.Approach, 0.45f, Ease.Smooth, PoseKey.Hold.Yaw(180f).Squash(0.74f).Lean(32f).Offset(0f, 0f, -0.035f).Arms(74f, 16f)),
            Beat.Step(BeatPhase.Hold, 0.55f, Ease.Linear, PoseKey.Hold.Arms(60f, 6f)),
            // up with the armful hugged, turned back to the fire
            Beat.Step(BeatPhase.Act, 0.50f, Ease.Smooth, PoseKey.Hold.Squash(1f).Lean(2f).Yaw(0f).Offset(0f, 0f, 0f).Arms(48f, 8f)),
            Beat.Step(BeatPhase.Hold, 0.45f, Ease.Linear, PoseKey.Hold.Offset(0f, 0f, -0.02f)),
            // antic: down to the flames, the armful drawn back to the chest
            Beat.Step(BeatPhase.Antic, 0.35f, Ease.Smooth, PoseKey.Hold.Squash(0.78f).Lean(22f).Arms(34f, 8f)),
            // act: both arms push it in, forward and down
            Beat.Step(BeatPhase.Act, 0.22f, Ease.Snap, PoseKey.Hold.Lean(34f).Offset(0f, -0.004f, -0.035f).Arms(96f, 6f)),
            Beat.Step(BeatPhase.Contact, 0.08f, Ease.Linear, PoseKey.Hold),
            // the hands stay out to the heat (palms to the fire), a breath
            Beat.Step(BeatPhase.Hold, 0.70f, Ease.Smooth, PoseKey.Hold.Lean(20f).Offset(0f, -0.004f, -0.02f).Arms(78f, 18f)),
            // back up and a step off the flames
            Beat.Step(BeatPhase.Settle, 0.55f, Ease.Smooth, PoseKey.Hold.Squash(1f).Lean(0f).Offset(0f, 0f, 0f).Arms(12f, 14f)),
            Beat.Step(BeatPhase.EndBeat, 0.75f, Ease.Linear, PoseKey.Hold));

        /// <summary>When the armful is picked up (the end of the gather's hold) and when it leaves the hands (the Contact).</summary>
        public static readonly float PickUpAt = FeedBeat[0].seconds + FeedBeat[1].seconds * 0.6f;
        public static readonly float DropAt = FeedBeat.ContactAt;


        /// <summary>Is the armful of dry grass in the arms at this point of the loop (picked up at the heap, gone into the fire)?</summary>
        public static bool FeedHolds(float clock) => clock >= PickUpAt && clock < DropAt + 0.02f;

        public static void Feed(ref DollPose p, float clock) => FeedBeat.Apply(ref p, clock);

        // ---------------------------------------------------------------- scraping a hide
        public static readonly Beat ScrapeBeat = new Beat(
            // the stone up at the top of the hide, both hands on it, the body back
            Beat.Step(BeatPhase.Antic, 0.30f, Ease.Smooth, PoseKey.Hold.Lean(-3f).Offset(0f, 0f, 0.006f).Arms(122f, 10f).Extend(true)),
            // the hard push down along the hide, the weight behind it
            Beat.Step(BeatPhase.Act, 0.30f, Ease.Snap, PoseKey.Hold.Lean(24f).Squash(0.9f).Offset(0f, -0.004f, -0.018f).Arms(64f, 8f)),
            Beat.Step(BeatPhase.Contact, 0.07f, Ease.Linear, PoseKey.Hold),
            Beat.Step(BeatPhase.Hold, 0.16f, Ease.Linear, PoseKey.Hold.Lean(26f)),
            // the lift back up (lighter, a little out from the hide)
            Beat.Step(BeatPhase.Settle, 0.32f, Ease.Smooth, PoseKey.Hold.Lean(6f).Squash(1f).Offset(0f, 0f, 0f).Arms(100f, 16f)));


        public static void Scrape(ref DollPose p, float clock) => ScrapeBeat.Apply(ref p, clock);

        // ---------------------------------------------------------------- sorting the basket
        public static readonly Beat SortBeat = new Beat(
            // the basket held out in the left hand (PeopleView: HoldInLeft), bent over the pile; the right hand dips into the basket
            Beat.Step(BeatPhase.Approach, 0.30f, Ease.Smooth, PoseKey.Hold.Squash(0.86f).Lean(18f).Yaw(-22f).ArmL(48f, 0f, -18f).ArmR(52f, 0f, -14f)),
            Beat.Step(BeatPhase.Hold, 0.40f, Ease.Linear, PoseKey.Hold.ArmR(56f, 0f, -18f)),
            // a handful up to look at it (a bad one is kept back)
            Beat.Step(BeatPhase.Act, 0.30f, Ease.Smooth, PoseKey.Hold.Lean(8f).ArmR(34f, 0f, 4f)),
            Beat.Step(BeatPhase.Hold, 0.40f, Ease.Linear, PoseKey.Hold.Roll(-4f)),
            // turned back to the pile, bent, laid on it
            Beat.Step(BeatPhase.Act, 0.35f, Ease.Smooth, PoseKey.Hold.Yaw(0f).Roll(0f).Lean(30f).Squash(0.8f).ArmR(78f, 0f, 10f)),
            Beat.Step(BeatPhase.Contact, 0.07f, Ease.Linear, PoseKey.Hold),
            Beat.Step(BeatPhase.Settle, 0.45f, Ease.Smooth, PoseKey.Hold.Lean(18f).Squash(0.86f).ArmR(50f, 0f, -6f)));

        public static void Sort(ref DollPose p, float clock) => SortBeat.Apply(ref p, clock);

        // ---------------------------------------------------------------- mending a bedroll
        public static readonly Beat MendBeat = new Beat(
            // seated over the hide, the left hand holds the edge, the right brings the awl down to it
            Beat.Step(BeatPhase.Approach, 0.30f, Ease.Smooth, PoseKey.Hold.Squash(0.78f).Offset(0f, -0.03f, 0f).Lean(16f).ArmL(52f, 0f, -6f).ArmR(58f, 0f, 4f)),
            // the push through
            Beat.Step(BeatPhase.Act, 0.18f, Ease.Snap, PoseKey.Hold.Lean(20f).ArmR(66f, 0f, 2f)),
            Beat.Step(BeatPhase.Hold, 0.25f, Ease.Linear, PoseKey.Hold),
            // the pull: the thread drawn up and out, high
            Beat.Step(BeatPhase.Act, 0.40f, Ease.Smooth, PoseKey.Hold.Lean(6f).ArmR(118f, 0f, 34f)),
            Beat.Step(BeatPhase.Hold, 0.30f, Ease.Linear, PoseKey.Hold),
            Beat.Step(BeatPhase.Settle, 0.35f, Ease.Smooth, PoseKey.Hold.Lean(16f).ArmR(58f, 0f, 4f)));


        public static void Mend(ref DollPose p, float clock) => MendBeat.Apply(ref p, clock);
    }
}
