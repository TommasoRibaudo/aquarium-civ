using System;
using System.Collections.Generic;

namespace AquariumCiv.View.Moments
{
    /// <summary>
    /// Plays a <see cref="MomentDef"/> against time (era-moment.md §3, issue #88). Pure C#: the MonoBehaviour side calls
    /// <see cref="Tick"/> every frame with the real delta and whether the player holds to hurry, and stages what the events
    /// say. Rules:
    /// <list type="bullet">
    /// <item>It never starts by itself: <see cref="Start"/> is the Watch button.</item>
    /// <item>Hold to hurry plays at <see cref="HurrySpeed"/>; there is no skip (Choice 2).</item>
    /// <item>The cheer beat calls the cheer callback (<c>SimCore.AdvanceEra</c>): the only place the era moves.</item>
    /// <item>Left before the cheer (<see cref="Leave"/>), nothing moved: the moment replays from the start next time.
    /// Left after it, the era has moved and the festival runs; the director just finishes.</item>
    /// <item>While it plays the sim clock holds still (<see cref="HoldsSimClock"/>).</item>
    /// <item>A frame that jumps over several beats (a hitch, hurry) still raises every beat, in order.</item>
    /// </list>
    /// </summary>
    public class MomentDirector
    {
        public const float HurrySpeed = 2f;
        /// <summary>Real seconds the "hold to hurry" hint shows at the start.</summary>
        public const float HurryHintSeconds = 3f;
        public const string HurryHint = "Hold to hurry";

        public enum Phase { Idle, Playing, Finished }
        public enum LeaveResult { NotPlaying, Replays, AlreadyCrossed }

        public Phase State { get; private set; } = Phase.Idle;
        public MomentDef Def { get; private set; }
        public MomentContext Context { get; private set; }
        /// <summary>The beats that play this time (conditions applied), in time order.</summary>
        public IReadOnlyList<MomentBeat> Beats => beats;
        public CameraRail Rail { get; private set; }
        /// <summary>The camera pose when Watch was tapped (the rail glides from it).</summary>
        public CameraPose StartPose { get; private set; } = CameraPose.Default;

        /// <summary>Moment time, seconds (advances at <see cref="Speed"/> × real time).</summary>
        public float Time { get; private set; }
        /// <summary>Real seconds since Watch.</summary>
        public float RealTime { get; private set; }
        public float Speed { get; private set; } = 1f;
        public bool Hurrying => Speed > 1f;
        /// <summary>The hurry speed (debug panel: hurry speed); <see cref="HurrySpeed"/> by default.</summary>
        public float HurryFactor { get; set; } = HurrySpeed;
        /// <summary>Total length of this playing (the last beat's start plus its hold).</summary>
        public float Length { get; private set; }

        /// <summary>Index into <see cref="Beats"/> of the beat playing now (-1 before the first).</summary>
        public int BeatIndex { get; private set; } = -1;
        public MomentBeat CurrentBeat => BeatIndex >= 0 && BeatIndex < beats.Count ? beats[BeatIndex] : null;
        public float BeatStart(int i) => beats[i].start;
        public float BeatEnd(int i) => i + 1 < beats.Count ? beats[i + 1].start : Length;
        /// <summary>0..1 through the current beat.</summary>
        public float BeatProgress
        {
            get
            {
                if (BeatIndex < 0) return 0f;
                float a = BeatStart(BeatIndex), b = BeatEnd(BeatIndex);
                return b <= a ? 1f : Math.Clamp((Time - a) / (b - a), 0f, 1f);
            }
        }

        /// <summary>The cheer beat has played (the era moved, unless a replay).</summary>
        public bool Cheered { get; private set; }
        /// <summary>What the cheer callback answered (false on a replay or when the sim had no era ready).</summary>
        public bool EraAdvanced { get; private set; }

        public bool HoldsSimClock => State == Phase.Playing;
        public bool ShowHurryHint => State == Phase.Playing && RealTime < HurryHintSeconds;
        /// <summary>The camera follows the rail (false once a beat frees it, and when not playing).</summary>
        public bool CameraGuided => State == Phase.Playing && Time < Rail.FreeAt;

        /// <summary>A beat starts (raised for every beat that plays, in order).</summary>
        public event Action<MomentBeat, int> BeatStarted;
        /// <summary>The cheer: <c>true</c> when the era moved.</summary>
        public event Action<bool> CheerPlayed;
        public event Action Finished;
        /// <summary>Left before the cheer: nothing moved, the moment will replay.</summary>
        public event Action Abandoned;

        readonly List<MomentBeat> beats = new();
        readonly Func<bool> cheer;

        /// <param name="cheer">Moves the era (<c>sim.AdvanceEra</c>); not called on a replay.</param>
        public MomentDirector(Func<bool> cheer) { this.cheer = cheer; }

        /// <summary>Watch: start (or restart) the moment from its first beat. Returns false if already playing.</summary>
        public bool Start(MomentDef def, MomentContext ctx, CameraPose from)
        {
            if (State == Phase.Playing || def == null) return false;
            Def = def;
            Context = ctx ?? new MomentContext();
            beats.Clear();
            foreach (var b in def.beats) if (Context.Plays(b)) beats.Add(b);
            beats.Sort((a, c) => a.start.CompareTo(c.start));
            Length = beats.Count == 0 ? 0f : beats[beats.Count - 1].start + Math.Max(0f, beats[beats.Count - 1].hold);
            Rail = new CameraRail(beats);
            StartPose = from;
            Time = RealTime = 0f;
            Speed = 1f;
            BeatIndex = -1;
            Cheered = EraAdvanced = false;
            State = Phase.Playing;
            Advance(0f);
            return true;
        }

        /// <summary>One frame: <paramref name="realDt"/> real seconds, <paramref name="hurry"/> while the player holds.</summary>
        public void Tick(float realDt, bool hurry)
        {
            if (State != Phase.Playing) return;
            if (realDt < 0f) realDt = 0f;
            Speed = hurry ? Math.Max(1f, HurryFactor) : 1f;
            RealTime += realDt;
            Advance(realDt * Speed);
        }

        void Advance(float dt)
        {
            Time = Math.Min(Length, Time + dt);
            while (BeatIndex + 1 < beats.Count && beats[BeatIndex + 1].start <= Time)
            {
                BeatIndex++;
                var b = beats[BeatIndex];
                BeatStarted?.Invoke(b, BeatIndex);
                if (b.Has(BeatAction.Cheer) && !Cheered) DoCheer();
            }
            if (Time >= Length && State == Phase.Playing)
            {
                State = Phase.Finished;
                Finished?.Invoke();
            }
        }

        void DoCheer()
        {
            Cheered = true;
            EraAdvanced = !Context.replay && cheer != null && cheer();
            CheerPlayed?.Invoke(EraAdvanced);
        }

        /// <summary>The player leaves mid-moment (closes the app, goes to the Keeper's room). Before the cheer the
        /// moment is dropped and will replay from the start; after it the director finishes (the festival runs).</summary>
        public LeaveResult Leave()
        {
            if (State != Phase.Playing) return LeaveResult.NotPlaying;
            if (!Cheered)
            {
                State = Phase.Idle;
                BeatIndex = -1;
                Time = RealTime = 0f;
                Abandoned?.Invoke();
                return LeaveResult.Replays;
            }
            Time = Length;
            State = Phase.Finished;
            Finished?.Invoke();
            return LeaveResult.AlreadyCrossed;
        }

        /// <summary>The guided camera now, with the player's own orbit (<paramref name="offset"/>, may be null) on top.</summary>
        public CameraPose CameraNow(IMomentAnchors anchors, GuidedOffset offset = null)
        {
            if (Rail == null) return StartPose;
            var p = Rail.Evaluate(Time, StartPose, anchors);
            return offset != null ? offset.Apply(p) : p;
        }

        /// <summary>The beat (index) playing at moment time <paramref name="t"/> (-1 before the first).</summary>
        public int BeatAt(float t)
        {
            int idx = -1;
            for (int i = 0; i < beats.Count; i++) if (beats[i].start <= t) idx = i;
            return idx;
        }
    }
}
