using AquariumCiv.Life;
using UnityEngine;

namespace AquariumCiv.View
{
    /// <summary>The handful of procedural moves a peg doll has (life-in-the-box §9); every activity is drawn as one.</summary>
    public enum DollStyle
    {
        Stand, Walk, Run, Hidden, Lie, Sit, SitStrike, SitWeave, Crouch, Chop, Hammer, Hoe, Pick, Scatter, Fish, Stir, Sweep,
        Saw, Lift, Talk, Argue, Speech, Haggle, Wave, Cheer, Point, LookUp, Hop, Mourn, Swim, Bell, Bless, Reach, Stretch,
        Hang, Look,
        // #92: at the glass and in the relic moments
        LookUpWary, PressHands, WaveSmall, Flinch, Peek, HopBack, Laugh, Measure, Knock, Write, Peer, Freeze, Bow, PoseBig,
        Lopsided, Taste, HoldUp,
    }

    /// <summary>
    /// One frame of a doll's pose, relative to where it stands and faces. Arms are absolute local Euler angles (degrees) of
    /// the stub arms (rest: (0, 0, ∓14); +x swings an arm forward, +z on the right arm raises it out to the side). Lean
    /// tips the whole doll forward about its feet. <see cref="extend"/> turns a hand tool to stick out along the arm (a
    /// strike), instead of standing upright in the fist (carried).
    /// </summary>
    public struct DollPose
    {
        /// <summary>Offset of the feet in the doll's own frame (x right, y up, z back), adult units (scaled by the doll).</summary>
        public Vector3 offset;
        public float yaw, lean, roll, squash;
        public Vector3 armL, armR;
        public bool extend, hidden, lying;
        /// <summary>1 on the frame-window of a stroke's hit (a dip, later a puff of chips or sparks), else 0.</summary>
        public float hit;

        public static DollPose Rest => new DollPose { squash = 1f, armL = new Vector3(0, 0, -14f), armR = new Vector3(0, 0, 14f) };
    }

    /// <summary>
    /// The pose of a peg doll at a moment (life-in-the-box §9, "Animation budget"): pure functions of the activity, real
    /// time, the planner's pace and loop step, and what the doll carries. Transform-only, no rigs: a rhythm per activity
    /// (chop-chop, the hoe's slow swing, the talk bob) is what says what someone does at full-box zoom (§2).
    /// </summary>
    public static class DollMotion
    {
        /// <summary>The arm angle a hand prop is built for (<see cref="PegDollView.SetProp"/>): upright in a slightly raised fist.</summary>
        public static readonly Vector3 HoldArm = new(-35f, 0f, 10f);

        /// <summary>Which move draws an activity when the doll stands at its place (walking is decided by the doll's speed).</summary>
        public static DollStyle StyleOf(ActivityKind k) => k switch
        {
            ActivityKind.None => DollStyle.Stand,
            ActivityKind.Sleep => DollStyle.Hidden,
            ActivityKind.Nap => DollStyle.Lie,
            ActivityKind.SitOut or ActivityKind.Eat or ActivityKind.SitTogether or ActivityKind.ListenStory or ActivityKind.Listen or
                ActivityKind.Drink or ActivityKind.SitWithOld or ActivityKind.MindBabies or ActivityKind.Meeting => DollStyle.Sit,
            ActivityKind.Knap or ActivityKind.ThrowPot => DollStyle.SitStrike,
            ActivityKind.Weave => DollStyle.SitWeave,
            ActivityKind.Stretch => DollStyle.Stretch,
            ActivityKind.Toddle or ActivityKind.Play or ActivityKind.Dance or ActivityKind.Snowball => DollStyle.Hop,
            ActivityKind.SweepStep or ActivityKind.Sweep => DollStyle.Sweep,
            ActivityKind.HangWashing => DollStyle.Hang,
            ActivityKind.ChopKindling or ActivityKind.Chop or ActivityKind.CutStone => DollStyle.Chop,
            ActivityKind.Hammer or ActivityKind.Repair => DollStyle.Hammer,
            ActivityKind.Saw => DollStyle.Saw,
            ActivityKind.StackWood or ActivityKind.MillWork or ActivityKind.Serve or ActivityKind.Prepare or ActivityKind.TendHives or
                ActivityKind.OpenSails or ActivityKind.CarryTables or ActivityKind.Trim => DollStyle.Lift,
            ActivityKind.Gather or ActivityKind.Harvest or ActivityKind.Weed => DollStyle.Pick,
            ActivityKind.Sow => DollStyle.Scatter,
            ActivityKind.Hoe => DollStyle.Hoe,
            ActivityKind.Fish => DollStyle.Fish,
            ActivityKind.Stalk => DollStyle.Crouch,
            ActivityKind.Cook or ActivityKind.Brew or ActivityKind.Knead => DollStyle.Stir,
            ActivityKind.Herd or ActivityKind.Inspect or ActivityKind.Direct or ActivityKind.PacePlans or ActivityKind.Point or
                ActivityKind.JumpAndPoint => DollStyle.Point,
            ActivityKind.MindStall or ActivityKind.Haggle or ActivityKind.Shop => DollStyle.Haggle,
            ActivityKind.Speech or ActivityKind.HoldCourt or ActivityKind.StoryTell or ActivityKind.Mediate or ActivityKind.Sing => DollStyle.Speech,
            ActivityKind.LightLanterns => DollStyle.Reach,
            ActivityKind.RingBell => DollStyle.Bell,
            ActivityKind.Bless or ActivityKind.HandsOut => DollStyle.Bless,
            ActivityKind.LookAtGlass or ActivityKind.LookUp or ActivityKind.Study or ActivityKind.Gaze => DollStyle.LookUp,
            // #92: the glass by belief stage and the relic moments
            ActivityKind.LookUpWary or ActivityKind.GazeWary => DollStyle.LookUpWary,
            ActivityKind.PressHands => DollStyle.PressHands,
            ActivityKind.WaveWary => DollStyle.WaveSmall,
            ActivityKind.Flinch or ActivityKind.StepBack => DollStyle.Flinch,
            ActivityKind.Peek => DollStyle.Peek,
            ActivityKind.CreepUp or ActivityKind.PutOn => DollStyle.Crouch,
            ActivityKind.HopBack => DollStyle.HopBack,
            ActivityKind.LaughPoint => DollStyle.Laugh,
            ActivityKind.Measure => DollStyle.Measure,
            ActivityKind.TapBack => DollStyle.Knock,
            ActivityKind.WriteDown => DollStyle.Write,
            ActivityKind.LookThroughLens => DollStyle.Peer,
            ActivityKind.TurnLens or ActivityKind.Poke or ActivityKind.Give => DollStyle.Point,
            ActivityKind.Freeze => DollStyle.Freeze,
            ActivityKind.Bow => DollStyle.Bow,
            ActivityKind.CopyPose => DollStyle.PoseBig,
            ActivityKind.LopsidedDance => DollStyle.Lopsided,
            ActivityKind.Taste => DollStyle.Taste,
            ActivityKind.HoldUp => DollStyle.HoldUp,
            ActivityKind.Deliver or ActivityKind.SetUp or ActivityKind.PickUp => DollStyle.Lift,
            ActivityKind.Chat or ActivityKind.Court or ActivityKind.Visit => DollStyle.Talk,
            ActivityKind.Argue => DollStyle.Argue,
            ActivityKind.Swim or ActivityKind.Splash => DollStyle.Swim,
            ActivityKind.Mourn or ActivityKind.Service => DollStyle.Mourn,
            ActivityKind.Cheer or ActivityKind.Clap => DollStyle.Cheer,
            ActivityKind.Wave => DollStyle.Wave,
            ActivityKind.Search or ActivityKind.Explore => DollStyle.Look,
            ActivityKind.RunOver => DollStyle.Run,
            _ => DollStyle.Stand, // watching, sheltering, a walk waiting to start
        };

        /// <summary>Does this move swing a hand tool out along the arm (axe, hammer, hoe, rod, stirring stick)?</summary>
        public static bool Extends(DollStyle s) => s is DollStyle.Chop or DollStyle.Hammer or DollStyle.Hoe or DollStyle.Fish or DollStyle.Stir;

        static float Frac(float x) => x - Mathf.Floor(x);
        static float Smooth(float a, float b, float x) => Mathf.SmoothStep(a, b, Mathf.Clamp01(x));

        /// <summary>
        /// The pose at real time <paramref name="t"/> (seconds; give each doll its own offset so neighbours don't move in
        /// step). <paramref name="pace"/> slows strokes (the sim's pace penalty, rule 5); <paramref name="step"/> of
        /// <paramref name="steps"/> is the planner's loop step: the last step of a work loop is a short straighten-up.
        /// <paramref name="speed"/> is the doll's ground speed (u/s) for the walk's cadence.
        /// </summary>
        /// <para><paramref name="progress"/> (0..1 through the activity), <paramref name="since"/> (real seconds into it) and
        /// <paramref name="count"/> drive the moves that tell a story once rather than loop (#92): the lens look (look, lean
        /// back startled, laugh) and the tap-back's knocks (exactly <paramref name="count"/> of them, then still).</para>
        public static DollPose Evaluate(DollStyle style, float t, float pace, int step, int steps, PropSlot slot, bool hasProp, float speed = 0.25f,
                                        float progress = 0f, float since = 0f, int count = 0)
        {
            var p = DollPose.Rest;
            pace = Mathf.Clamp(pace <= 0f ? 1f : pace, 0.5f, 1.5f);
            float tau = Mathf.PI * 2f;
            bool handTool = hasProp && slot == PropSlot.Hand;
            if (handTool) p.armR = HoldArm;
            // The last step of a multi-step work loop: straighten up a moment (a breather between rows).
            bool pause = steps > 2 && step == steps - 1 && style is DollStyle.Pick or DollStyle.Hoe or DollStyle.Lift or DollStyle.Scatter;

            switch (style)
            {
                case DollStyle.Hidden:
                    p.hidden = true;
                    break;

                case DollStyle.Walk:
                case DollStyle.Run:
                {
                    bool run = style == DollStyle.Run;
                    float cadence = run ? 3.2f : Mathf.Clamp(speed / 0.11f, 1.3f, 2.6f);
                    float w = Mathf.Sin(t * cadence * Mathf.PI);
                    p.offset.y = Mathf.Abs(w) * (run ? 0.022f : 0.011f);
                    p.roll = w * (run ? 3f : 4.5f);
                    p.lean = run ? 12f : 3f;
                    float swing = w * (run ? 40f : 26f);
                    p.armL = new Vector3(swing, 0, -14f);
                    p.armR = new Vector3(-swing, 0, 14f);
                    Carry(ref p, slot, hasProp, swing);
                    break;
                }

                case DollStyle.Lie:
                    p.lying = true;
                    p.offset.y = 0.035f;
                    p.armL = new Vector3(0, 0, -40f); p.armR = new Vector3(0, 0, 40f);
                    break;

                case DollStyle.Sit:
                case DollStyle.SitStrike:
                case DollStyle.SitWeave:
                {
                    p.offset.y = -0.03f;
                    p.squash = 0.78f;
                    p.lean = 6f + Mathf.Sin(t * 0.7f) * 1.5f;
                    p.armL = new Vector3(35f, 0, -10f); p.armR = new Vector3(35f, 0, 10f);
                    if (style == DollStyle.SitStrike)
                    {
                        float u = Frac(t * 1.6f * pace);
                        p.armR = new Vector3(u < 0.7f ? Smooth(40f, 85f, u / 0.7f) : Smooth(85f, 40f, (u - 0.7f) / 0.3f), 0, 8f);
                        p.hit = u > 0.92f ? 1f : 0f;
                        p.lean = 14f;
                    }
                    else if (style == DollStyle.SitWeave)
                    {
                        float w = Mathf.Sin(t * 1.4f * tau * 0.5f * pace);
                        p.armL = new Vector3(55f + w * 15f, 0, -10f); p.armR = new Vector3(55f - w * 15f, 0, 10f);
                    }
                    if (handTool && style == DollStyle.Sit) p.armR = HoldArm;
                    break;
                }

                case DollStyle.Crouch:
                    p.squash = 0.72f;
                    p.lean = 18f;
                    p.offset.z = -Mathf.Sin(t * 0.4f) * 0.01f; // edging forward, very slowly
                    if (handTool) p.armR = new Vector3(40f, 0, 10f);
                    break;

                case DollStyle.Chop:
                case DollStyle.Hammer:
                {
                    // Wind up over the head, strike fast down in front, a dip on the hit.
                    bool hammer = style == DollStyle.Hammer;
                    float period = (hammer ? 0.75f : 1.15f) / pace;
                    float u = Frac(t / period);
                    float top = hammer ? 120f : 165f, low = hammer ? 50f : 40f;
                    float a = u < 0.62f ? Smooth(low + 5f, top, u / 0.62f) : u < 0.76f ? Mathf.Lerp(top, low, (u - 0.62f) / 0.14f) : Mathf.Lerp(low, low + 5f, (u - 0.76f) / 0.24f);
                    p.armR = new Vector3(a, 0, 8f);
                    p.armL = new Vector3(hammer ? 45f : 30f, 0, -12f);
                    p.extend = hasProp;
                    bool hitNow = u >= 0.74f && u < 0.86f;
                    p.hit = hitNow ? 1f : 0f;
                    p.lean = (hammer ? 12f : 6f) + (hitNow ? 10f : 0f);
                    p.offset.y = hitNow ? -0.006f : 0f;
                    break;
                }

                case DollStyle.Hoe:
                {
                    float period = 1.5f / pace;
                    float u = Frac(t / period);
                    float a = u < 0.6f ? Smooth(40f, 140f, u / 0.6f) : Smooth(140f, 40f, (u - 0.6f) / 0.25f);
                    p.armR = new Vector3(a, 0, 6f);
                    p.armL = new Vector3(a * 0.8f, 0, -6f);
                    p.extend = hasProp;
                    p.lean = 22f;
                    p.hit = u > 0.82f && u < 0.92f ? 1f : 0f;
                    if (pause) { p.lean = 4f; p.armR = new Vector3(20f, 0, 8f); p.armL = new Vector3(20f, 0, -10f); }
                    break;
                }

                case DollStyle.Pick:
                {
                    // Crouched, reaching down and up again; the basket stays in the left hand.
                    float w = Mathf.Sin(t * tau / (0.95f / pace));
                    p.squash = 0.76f;
                    p.lean = 32f;
                    p.armR = new Vector3(55f + w * 25f, 0, 8f);
                    p.armL = new Vector3(30f, 0, -12f);
                    if (pause) { p.squash = 1f; p.lean = 0f; p.armR = new Vector3(0, 0, 14f); p.roll = Mathf.Sin(t * 2f) * 3f; }
                    break;
                }

                case DollStyle.Scatter:
                {
                    // Sowing: a slow walk-on-the-spot bob, the right arm flings out to the side.
                    float u = Frac(t / (1.25f / pace));
                    p.armR = new Vector3(30f, 0, u < 0.5f ? Smooth(14f, 85f, u / 0.5f) : Smooth(85f, 14f, (u - 0.5f) / 0.5f));
                    p.armL = new Vector3(45f, 0, -10f);
                    p.offset.y = Mathf.Abs(Mathf.Sin(u * tau)) * 0.006f;
                    p.lean = 8f;
                    break;
                }

                case DollStyle.Fish:
                {
                    // The rod held out over the water, a slow bob and now and then a tug.
                    float u = Frac(t / 4.5f);
                    float tug = u > 0.85f ? Mathf.Sin((u - 0.85f) / 0.15f * Mathf.PI) * 22f : 0f;
                    p.armR = new Vector3(118f + Mathf.Sin(t * 1.1f) * 3f + tug, 0, 6f);
                    p.armL = new Vector3(70f, 0, -6f);
                    p.extend = hasProp;
                    p.lean = -2f;
                    break;
                }

                case DollStyle.Stir:
                {
                    float w = t * tau / (1.6f / pace);
                    p.armR = new Vector3(40f + Mathf.Sin(w) * 14f, 0, 10f + Mathf.Cos(w) * 14f);
                    p.armL = new Vector3(25f, 0, -12f);
                    p.extend = hasProp;
                    p.lean = 16f;
                    p.roll = Mathf.Cos(w) * 2f;
                    break;
                }

                case DollStyle.Sweep:
                {
                    float w = Mathf.Sin(t * tau / (1.0f / pace));
                    p.armR = new Vector3(HoldArm.x + 15f, w * 18f, 14f + w * 28f);
                    p.armL = new Vector3(40f, 0, -10f);
                    p.lean = 18f;
                    p.yaw = w * 12f;
                    break;
                }

                case DollStyle.Saw:
                {
                    float w = Mathf.Sin(t * tau / (0.9f / pace));
                    p.armR = new Vector3(50f + w * 28f, 0, 6f);
                    p.armL = new Vector3(50f + w * 28f, 0, -6f);
                    p.lean = 12f + w * 8f;
                    break;
                }

                case DollStyle.Lift:
                {
                    // Bend to pick something up, straighten with it, set it down: a slow lift.
                    float u = Frac(t / (2.4f / pace));
                    float down = 0.5f - 0.5f * Mathf.Cos(u * tau);
                    p.lean = Mathf.Lerp(2f, 34f, down);
                    p.armL = new Vector3(Mathf.Lerp(80f, 40f, down), 0, -8f);
                    p.armR = new Vector3(Mathf.Lerp(80f, 40f, down), 0, 8f);
                    if (handTool) p.armR = HoldArm + new Vector3(down * 30f, 0, 0);
                    if (pause) { p.lean = 2f; p.armL = new Vector3(0, 0, -14f); p.armR = handTool ? HoldArm : new Vector3(0, 0, 14f); }
                    break;
                }

                case DollStyle.Talk:
                case DollStyle.Argue:
                case DollStyle.Haggle:
                {
                    bool argue = style == DollStyle.Argue;
                    float period = argue ? 0.55f : 1.3f;
                    float w = Mathf.Sin(t * tau / period);
                    // Two people facing each other take turns: odd steps talk, even steps listen (the partner's step is opposite).
                    bool talking = argue || (step & 1) == 0;
                    p.offset.y = talking ? Mathf.Max(0f, w) * (argue ? 0.013f : 0.006f) : 0f;
                    p.lean = argue ? 12f + Mathf.Max(0f, w) * 6f : 4f;
                    if (argue) p.armR = new Vector3(150f + w * 20f, 0, 14f);
                    else if (talking) p.armR = new Vector3(30f + Mathf.Max(0f, w) * 35f, 0, 14f);
                    if (style == DollStyle.Haggle) p.armL = new Vector3(30f - Mathf.Min(0f, w) * 35f, 0, -14f);
                    if (handTool && !argue) p.armR = HoldArm;
                    break;
                }

                case DollStyle.Speech:
                {
                    float w = Mathf.Sin(t * tau / 2.2f);
                    p.armR = new Vector3(95f + w * 40f, 0, 30f);
                    p.armL = new Vector3(35f - w * 15f, 0, -22f);
                    p.offset.y = Mathf.Max(0f, Mathf.Sin(t * tau / 1.1f)) * 0.007f;
                    p.lean = -5f;
                    break;
                }

                case DollStyle.Wave:
                    p.armR = new Vector3(0, 0, 155f + Mathf.Sin(t * 14f) * 20f);
                    p.lean = -6f;
                    break;

                case DollStyle.Cheer:
                {
                    float w = Mathf.Abs(Mathf.Sin(t * 5f));
                    p.offset.y = w * 0.035f;
                    p.armL = new Vector3(0, 0, -160f); p.armR = new Vector3(0, 0, 160f);
                    p.lean = -6f;
                    break;
                }

                case DollStyle.Point:
                    p.armR = new Vector3(88f + Mathf.Sin(t * 1.3f) * 6f, 0, 12f);
                    p.lean = 5f;
                    if (handTool) p.armR = HoldArm + new Vector3(60f, 0, 0);
                    break;

                case DollStyle.LookUp:
                    p.lean = -20f + Mathf.Sin(t * 0.8f) * 3f;
                    break;

                case DollStyle.Hop:
                {
                    // Kids at play: hopping round a little circle, faster than a walk (the circle is in the offset).
                    float a = t * 2.4f;
                    p.offset.x = Mathf.Cos(a) * 0.1f;
                    p.offset.z = Mathf.Sin(a) * 0.1f;
                    p.yaw = 180f - a * Mathf.Rad2Deg; // face along the circle (the front is local -z)
                    p.offset.y = Mathf.Abs(Mathf.Sin(t * 9f)) * 0.03f;
                    float sw = Mathf.Sin(t * 9f) * 40f;
                    p.armL = new Vector3(sw, 0, -20f); p.armR = new Vector3(-sw, 0, 20f);
                    p.lean = 10f;
                    break;
                }

                case DollStyle.Mourn:
                    p.lean = 16f;
                    p.armL = new Vector3(15f, 0, -6f); p.armR = new Vector3(15f, 0, 6f);
                    break;

                case DollStyle.Swim:
                {
                    float w = Mathf.Sin(t * 3f);
                    p.offset.y = -0.1f + w * 0.006f;
                    p.armL = new Vector3(0, 0, -90f + w * 40f); p.armR = new Vector3(0, 0, 90f + w * 40f);
                    break;
                }

                case DollStyle.Bell:
                {
                    float u = Frac(t / 1.4f);
                    p.armR = new Vector3(170f - Mathf.Sin(u * Mathf.PI) * 60f, 0, 8f);
                    p.armL = new Vector3(170f - Mathf.Sin(u * Mathf.PI) * 60f, 0, -8f);
                    p.offset.y = -Mathf.Sin(u * Mathf.PI) * 0.008f;
                    break;
                }

                case DollStyle.Bless:
                    p.armL = new Vector3(110f, 0, -28f); p.armR = new Vector3(110f, 0, 28f);
                    p.lean = -3f + Mathf.Sin(t * 0.9f) * 2f;
                    break;

                case DollStyle.Reach:
                    p.armR = new Vector3(165f + Mathf.Sin(t * 2f) * 6f, 0, 10f);
                    p.lean = -8f;
                    break;

                case DollStyle.Stretch:
                {
                    float w = 0.5f + 0.5f * Mathf.Sin(t * 1.2f);
                    p.armL = new Vector3(0, 0, -Mathf.Lerp(30f, 170f, w)); p.armR = new Vector3(0, 0, Mathf.Lerp(30f, 170f, w));
                    p.lean = -6f * w;
                    break;
                }

                case DollStyle.Hang:
                {
                    float w = Mathf.Sin(t * tau / 1.6f);
                    p.armL = new Vector3(140f + w * 25f, 0, -12f); p.armR = new Vector3(140f - w * 25f, 0, 12f);
                    p.lean = -4f;
                    break;
                }

                case DollStyle.Look:
                    p.yaw = Mathf.Sin(t * 0.9f) * 35f;
                    p.armR = new Vector3(0, 0, 150f); // a hand over the eyes, roughly
                    break;

                // ---------------- #92: the glass and the relic moments ----------------
                case DollStyle.LookUpWary:
                    // A step back, arms close, looking up: the camp is a bit scared of the edge of the sky.
                    p.offset.z = 0.03f;
                    p.lean = -14f + Mathf.Sin(t * 0.7f) * 2f;
                    p.armL = new Vector3(18f, 0, -6f); p.armR = new Vector3(18f, 0, 6f);
                    p.squash = 0.96f;
                    break;

                case DollStyle.PressHands:
                {
                    // Both hands flat on the glass, nose close, a little bob on the toes.
                    float w = Mathf.Max(0f, Mathf.Sin(t * 2.2f));
                    p.armL = new Vector3(95f, 0, -18f); p.armR = new Vector3(95f + Mathf.Sin(t * 1.3f) * 8f, 0, 18f);
                    p.lean = 8f;
                    p.offset.y = w * 0.012f;
                    break;
                }

                case DollStyle.WaveSmall:
                    p.offset.z = 0.03f;
                    p.armR = new Vector3(0, 0, 115f + Mathf.Sin(t * 10f) * 15f);
                    p.armL = new Vector3(18f, 0, -6f);
                    p.lean = -4f;
                    break;

                case DollStyle.Flinch:
                {
                    // A jolt back, arms up to the chest, then a held, hunched stillness.
                    float j = Mathf.Clamp01(since / 0.25f);
                    p.offset.z = Mathf.Lerp(0f, 0.05f, j);
                    p.squash = Mathf.Lerp(1f, 0.9f, j);
                    p.lean = -10f * j;
                    p.armL = new Vector3(70f, 0, -30f); p.armR = new Vector3(70f, 0, 30f);
                    break;
                }

                case DollStyle.Peek:
                    p.offset.z = 0.04f;
                    p.squash = 0.86f;
                    p.lean = -14f + Mathf.Sin(t * 1.4f) * 4f;
                    p.armR = new Vector3(150f, 0, 20f); // a hand over the brow, peeking up
                    p.armL = new Vector3(40f, 0, -20f);
                    break;

                case DollStyle.HopBack:
                {
                    float u = Frac(t * 1.6f);
                    p.offset.y = Mathf.Sin(u * Mathf.PI) * 0.03f;
                    p.offset.z = 0.02f + Mathf.Sin(u * Mathf.PI) * 0.02f;
                    p.armL = new Vector3(-20f, 0, -40f); p.armR = new Vector3(-20f, 0, 40f);
                    p.lean = -8f;
                    break;
                }

                case DollStyle.Laugh:
                {
                    // Pointing at the spot, shaking with laughter.
                    float w = Mathf.Abs(Mathf.Sin(t * 9f));
                    p.armR = new Vector3(110f, 0, 12f);
                    p.armL = new Vector3(30f, 0, -40f);
                    p.offset.y = w * 0.012f;
                    p.roll = Mathf.Sin(t * 9f) * 4f;
                    p.lean = -6f;
                    break;
                }

                case DollStyle.Measure:
                    // The rod held up against the glass, the other hand counting along it, slowly sweeping.
                    p.armR = HoldArm + new Vector3(30f, 0, 0);
                    p.armL = new Vector3(120f + Mathf.Sin(t * 1.1f) * 25f, 0, -12f);
                    p.lean = -8f;
                    p.yaw = Mathf.Sin(t * 0.5f) * 10f;
                    break;

                case DollStyle.Knock:
                {
                    // Exactly count knocks, one every half second, then the fist rests at the glass.
                    const float period = 0.5f;
                    float u = since / period;
                    int k = Mathf.FloorToInt(u);
                    bool knocking = since >= 0f && k < Mathf.Max(1, count);
                    float f = Frac(u);
                    float a = knocking ? (f < 0.5f ? Smooth(80f, 105f, f / 0.5f) : Smooth(105f, 80f, (f - 0.5f) / 0.5f)) : 85f;
                    p.armR = new Vector3(a, 0, 8f);
                    p.armL = new Vector3(20f, 0, -10f);
                    p.lean = 6f;
                    p.hit = knocking && f > 0.45f && f < 0.6f ? 1f : 0f;
                    break;
                }

                case DollStyle.Write:
                {
                    // The slate hugged in front, the right hand scribbling.
                    float w = Mathf.Sin(t * 12f);
                    p.armL = new Vector3(70f, 0, -6f);
                    p.armR = new Vector3(65f + w * 6f, w * 10f, 6f);
                    p.lean = 14f;
                    break;
                }

                case DollStyle.Peer:
                {
                    // Look through the lens, lean back startled, then laugh (the queue's turn, 1.5).
                    float u = Mathf.Clamp01(progress);
                    if (u < 0.5f) { p.lean = 16f; p.armL = new Vector3(70f, 0, -8f); p.armR = new Vector3(70f, 0, 8f); }
                    else if (u < 0.7f) { p.lean = -16f; p.offset.z = 0.03f; p.armL = new Vector3(60f, 0, -40f); p.armR = new Vector3(60f, 0, 40f); }
                    else { float w = Mathf.Abs(Mathf.Sin(t * 9f)); p.offset.y = w * 0.012f; p.lean = -6f; p.armL = new Vector3(20f, 0, -30f); p.armR = new Vector3(20f, 0, 30f); p.roll = Mathf.Sin(t * 9f) * 3f; }
                    break;
                }

                case DollStyle.Freeze:
                    // The thud: rigid, arms out, no breathing.
                    p.armL = new Vector3(10f, 0, -45f); p.armR = new Vector3(10f, 0, 45f);
                    p.lean = -4f;
                    break;

                case DollStyle.Bow:
                    p.lean = 32f + Mathf.Sin(t * 0.6f) * 2f;
                    p.armL = new Vector3(10f, 0, -8f); p.armR = new Vector3(10f, 0, 8f);
                    break;

                case DollStyle.PoseBig:
                    // The Keeper Doll's pose, copied by the kids: one hand raised, very big.
                    p.armR = new Vector3(0, 0, 170f);
                    p.armL = new Vector3(0, 0, -60f);
                    p.offset.y = Mathf.Abs(Mathf.Sin(t * 4f)) * 0.01f;
                    p.lean = -5f;
                    break;

                case DollStyle.Lopsided:
                {
                    // A lopsided little dance: hop on one side, tilt, turn.
                    float u = t * 5f;
                    p.offset.y = Mathf.Max(0f, Mathf.Sin(u)) * 0.025f;
                    p.roll = 9f + Mathf.Sin(u) * 6f;
                    p.yaw = Mathf.Sin(t * 1.5f) * 40f;
                    p.armL = new Vector3(0, 0, -100f + Mathf.Sin(u) * 20f); p.armR = new Vector3(0, 0, 60f);
                    break;
                }

                case DollStyle.Taste:
                {
                    // A hand to the mouth and back: chewing, considering.
                    float u = Frac(t / 1.4f);
                    p.armR = new Vector3(u < 0.5f ? Smooth(40f, 135f, u / 0.5f) : 135f - Mathf.Sin((u - 0.5f) * 12f) * 6f, 0, 18f);
                    p.offset.y = u > 0.5f ? Mathf.Abs(Mathf.Sin(t * 8f)) * 0.004f : 0f;
                    p.lean = -3f;
                    break;
                }

                case DollStyle.HoldUp:
                    // Holding the find up high, to the glass.
                    p.armR = new Vector3(160f + Mathf.Sin(t * 3f) * 6f, 0, 10f);
                    p.armL = new Vector3(0, 0, -30f);
                    p.lean = -10f;
                    p.offset.y = Mathf.Abs(Mathf.Sin(t * 3f)) * 0.006f;
                    break;

                default: // Stand: breathing, a slow weight shift
                    p.offset.y = Mathf.Sin(t * 1.6f) * 0.0015f;
                    p.roll = Mathf.Sin(t * 0.5f) * 1.5f;
                    Carry(ref p, slot, hasProp, 0f);
                    break;
            }
            return p;
        }

        /// <summary>Arms that hold a load: a hand on the head load, both arms round what's hugged, both pushing a barrel.</summary>
        static void Carry(ref DollPose p, PropSlot slot, bool hasProp, float swing)
        {
            if (!hasProp) return;
            switch (slot)
            {
                case PropSlot.Hand: p.armR = HoldArm + new Vector3(swing * 0.3f, 0, 0); break;
                case PropSlot.Head: p.armR = new Vector3(0, 0, 160f); break;
                case PropSlot.Shoulder: p.armR = new Vector3(0, 0, 150f); break;
                case PropSlot.Arms: p.armL = new Vector3(62f, 0, -4f); p.armR = new Vector3(62f, 0, 4f); break;
                case PropSlot.Front: p.armL = new Vector3(48f, 0, -6f); p.armR = new Vector3(48f, 0, 6f); p.lean += 8f; break;
                case PropSlot.Back: p.armL.x *= 0.5f; p.armR.x *= 0.5f; break;
            }
        }
    }
}
