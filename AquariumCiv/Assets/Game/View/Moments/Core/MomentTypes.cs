using System;
using System.Collections.Generic;

namespace AquariumCiv.View.Moments
{
    /// <summary>A plain 3-float vector, so the moment's maths stays pure C# (no UnityEngine); the view converts to
    /// <c>Vector3</c>. World units, the same space as <c>BoxCamera.target</c>.</summary>
    [Serializable]
    public struct MVec3 : IEquatable<MVec3>
    {
        public float x, y, z;
        public MVec3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
        public static readonly MVec3 Zero = new(0f, 0f, 0f);
        public static MVec3 operator +(MVec3 a, MVec3 b) => new(a.x + b.x, a.y + b.y, a.z + b.z);
        public static MVec3 operator -(MVec3 a, MVec3 b) => new(a.x - b.x, a.y - b.y, a.z - b.z);
        public static MVec3 operator *(MVec3 a, float k) => new(a.x * k, a.y * k, a.z * k);
        public static MVec3 Lerp(MVec3 a, MVec3 b, float t) => new(a.x + (b.x - a.x) * t, a.y + (b.y - a.y) * t, a.z + (b.z - a.z) * t);
        public float FlatDistance(MVec3 o) { float dx = x - o.x, dz = z - o.z; return (float)Math.Sqrt(dx * dx + dz * dz); }
        public bool Equals(MVec3 o) => x == o.x && y == o.y && z == o.z;
        public override bool Equals(object obj) => obj is MVec3 o && Equals(o);
        public override int GetHashCode() => HashCode.Combine(x, y, z);
        public override string ToString() => $"({x:0.###}, {y:0.###}, {z:0.###})";
    }

    /// <summary>The <c>BoxCamera</c> pose the rail drives: the point looked at, the orbit angles (degrees) and the zoom
    /// (1 = the whole box fits the width, &lt; 1 closer). Phase 2 copies it into <c>BoxCamera.target/yaw/pitch/zoom</c>.</summary>
    [Serializable]
    public struct CameraPose
    {
        public MVec3 target;
        public float yaw, pitch, zoom;
        public CameraPose(MVec3 target, float yaw, float pitch, float zoom) { this.target = target; this.yaw = yaw; this.pitch = pitch; this.zoom = zoom; }
        /// <summary>BoxCamera's defaults (target (0, 1, 0), yaw 28, pitch 34, zoom 1).</summary>
        public static CameraPose Default => new(new MVec3(0f, 1f, 0f), 28f, 34f, 1f);
        public override string ToString() => $"{target} yaw {yaw:0.#} pitch {pitch:0.#} zoom {zoom:0.##}";
    }

    /// <summary>What a camera key looks at. The view resolves each to a world point (<see cref="IMomentAnchors"/>);
    /// one it can't resolve falls back to <see cref="Box"/>.</summary>
    public enum MomentAnchor
    {
        Box,        // the box centre on the ground (BoxCamera's default target)
        Hearth,     // the camp's fire ring / the village green and its well
        Leader,     // the chief / headman / lord (whoever leads this moment)
        Elder,      // the elder who hands the sash (camp → village); the heir behind the chair (village → town)
        Square,     // the busiest junction: the market square (village → town)
        Steps,      // the market steps where the lord's chair goes
        WaveFront,  // the lot the rebuild wave reached last
    }

    /// <summary>The view's side of the anchors: where each one is right now.</summary>
    public interface IMomentAnchors
    {
        bool TryGet(MomentAnchor anchor, out MVec3 point);
    }

    /// <summary>The light a beat eases to (the view's sky/sun preset); <see cref="Keep"/> leaves it as it is.</summary>
    public enum MomentLight { Keep, Dawn, Morning, Midday, Evening, Dusk }

    /// <summary>Easing of a camera key (the move from the key before it to this one).</summary>
    public enum MomentEase { Smooth, Linear, EaseOut, EaseIn }

    /// <summary>When a beat plays (decided once, at Watch, from <see cref="MomentContext"/>).</summary>
    public enum BeatCondition
    {
        Always,
        FirstMomentOnly,   // the notification question (Choice 4)
        LeaderPresent,     // the leader's own beat (sash, speech)
        LeaderMissing,     // the instant vote beat in place of the leader's
        SomethingCleared,  // "What's cleared" (village → town): only if the old camp hearth is there to clear
    }

    /// <summary>What the people and the world do in a beat. The view (PeopleView / BoxView) stages each; the director
    /// only says when. <see cref="Cheer"/> is the one beat with a sim effect: it calls <c>SimCore.AdvanceEra()</c>.</summary>
    public enum BeatAction
    {
        None,
        // people
        Gather,          // everyone stops and walks to the anchor (the fire / the green)
        LeaderSpeaks,    // the leader steps up; bubble = MomentBeat.line
        PlantStake,      // the chief pushes a stake into the ground by the fire
        PassSash,        // an elder hands the chief a sash: the chief becomes the headman
        Vote,            // no leader: the notables pick one on the spot
        LordChair,       // a cloak and a chair on the market steps: the headman becomes the first lord
        Cheer,           // everyone faces the glass and cheers; the sim moves the era here
        // world
        BellRing,        // the priest rings the market bell for the first time
        GreenSpreads,    // fire ring → the green, the fire shrinks to a hearth, a well rises
        SquareSpreads,   // cobbles spread from the well, stalls round the rim
        ClearHearth,     // the old camp hearth lifted out and set at the square's edge
        RebuildWave,     // lots rebuild one by one from the centre outward (RebuildWave)
        ToolUnlock,      // the shaping slot opens with its hint (MomentBeat.param = the tool id)
        FestivalStart,   // bunting / a fair: the camera is free again
        AskNotification, // "Want a note when the next one is ready?" Yes / Not now
    }

    /// <summary>A camera key on a beat's rail. <see cref="at"/> is seconds from the beat's start; the pose is the
    /// anchor plus <see cref="offset"/>, with these angles and zoom.</summary>
    [Serializable]
    public class CameraKey
    {
        public float at;
        public MomentAnchor anchor = MomentAnchor.Box;
        public MVec3 offset;
        public float yaw = 28f, pitch = 34f, zoom = 1f;
        public MomentEase ease = MomentEase.Smooth;

        public CameraKey() { }
        public CameraKey(float at, MomentAnchor anchor, float yaw, float pitch, float zoom, MomentEase ease = MomentEase.Smooth)
        { this.at = at; this.anchor = anchor; this.yaw = yaw; this.pitch = pitch; this.zoom = zoom; this.ease = ease; }
    }

    /// <summary>One beat of a moment (era-moment.md §4, §5): when it starts, what happens, the camera keys, the light,
    /// the sound cue and the words. A beat lasts until the next beat that plays; the last lasts <see cref="hold"/>.</summary>
    [Serializable]
    public class MomentBeat
    {
        public string id;                 // "dawn", "chief", "green", "wave", "headman", "cheer", ...
        public float start;               // seconds from the moment's start
        public float hold;                // only for the last beat: how long it lasts
        public BeatCondition when = BeatCondition.Always;
        public List<BeatAction> actions = new();
        public string param = "";         // the action's argument (a tool id, the wave's look)
        public string line = "";          // a speech bubble / question; {leader} {place} {year} {era} are filled in
        public string caption = "";       // the big caption (the cheer)
        public MomentLight light = MomentLight.Keep;
        public string audio = "";         // a cue id for the view's audio ("flute", "drum", "knock_scale", "chord", ...)
        public bool haptic;               // a light tick (per lot during the wave)
        public List<CameraKey> camera = new();
        /// <summary>The camera is released at this beat (one finger orbits freely; no more guided rail).</summary>
        public bool freeCamera;

        public bool Has(BeatAction a) => actions.Contains(a);
    }

    /// <summary>A whole moment as data: its beats in time order. Tune timings here, not in code.</summary>
    [Serializable]
    public class MomentDef
    {
        public string id;                   // "camp_to_village"
        public AquariumCiv.Sim.Era toEra;
        public List<MomentBeat> beats = new();

        public MomentBeat Beat(string beatId) => beats.Find(b => b.id == beatId);
    }

    /// <summary>What decides which beats play and fills the words. Built once at Watch (<see cref="MomentContext.From"/>).</summary>
    public class MomentContext
    {
        public bool firstMoment;      // the very first era moment the player watches: ask about the notification at the end
        public bool leaderPresent = true;
        public bool somethingCleared; // the old camp hearth stands and will be cleared for the market
        public string place = "The settlement";
        public string leaderName = "";
        public int year;              // the year the cheer's caption shows (the HUD's year)
        /// <summary>A replay (debug, the time-lapse marker): the cheer does not call AdvanceEra.</summary>
        public bool replay;

        /// <summary>The flag in <c>SimState.storyFlags</c> once the notification question was asked (Choice 4: once).</summary>
        public const string FlagNotifyAsked = "moment:notify_asked";

        /// <summary>The context for the moment into <c>sim.NextEra</c>, read before anything moves.</summary>
        public static MomentContext From(AquariumCiv.Sim.SimCore sim, bool replay = false)
        {
            var s = sim.State;
            var leader = sim.HolderOf(AquariumCiv.Sim.SimCore.LeaderOf(s.era));
            return new MomentContext
            {
                firstMoment = !s.storyFlags.Contains(FlagNotifyAsked),
                leaderPresent = leader != null,
                leaderName = leader?.name ?? "",
                somethingCleared = sim.NextEra == AquariumCiv.Sim.Era.Town && s.buildings.Exists(b => b.type == AquariumCiv.Sim.BuildingType.Camp),
                place = sim.PlaceName,
                year = (int)(s.month / AquariumCiv.Sim.SimConfig.MonthsPerYear), // as the HUD shows it ("Year 14")
                replay = replay,
            };
        }

        /// <summary>Call when the notification question was answered (either way): it is asked once.</summary>
        public static void MarkNotifyAsked(AquariumCiv.Sim.SimState s)
        {
            if (!s.storyFlags.Contains(FlagNotifyAsked)) s.storyFlags.Add(FlagNotifyAsked);
        }

        public bool Plays(MomentBeat b) => b.when switch
        {
            BeatCondition.FirstMomentOnly => firstMoment && !replay,
            BeatCondition.LeaderPresent => leaderPresent,
            BeatCondition.LeaderMissing => !leaderPresent,
            BeatCondition.SomethingCleared => somethingCleared,
            _ => true,
        };

        /// <summary>Fills {leader} {place} {year} in a beat's line or caption.</summary>
        public string Fill(string text) => string.IsNullOrEmpty(text) ? "" :
            text.Replace("{leader}", string.IsNullOrEmpty(leaderName) ? "friend" : leaderName)
                .Replace("{place}", place).Replace("{year}", year.ToString());
    }
}
