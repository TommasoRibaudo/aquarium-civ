using System.Collections.Generic;

namespace AquariumCiv.View.Shaping
{
    /// <summary>Where a shaping is in its life on screen.</summary>
    public enum ShapingStage
    {
        /// <summary>Just placed: the cloud forms, the saplings pop up, the channel is carved, the herd runs in.</summary>
        Arriving,
        /// <summary>In the land, the people not yet answering (it can still be taken back).</summary>
        Waiting,
        /// <summary>The people answered (a credited building or a harvest); the gift stays.</summary>
        Answered,
        /// <summary>Taken back: the gift goes, quickly and quietly.</summary>
        Leaving,
        /// <summary>Gone: the view drops it.</summary>
        Gone,
    }

    /// <summary>The clocks a look is sampled at. Real seconds drive the quick parts (a cloud forming, a channel carved, a
    /// herd running in); game months (the view day, fractional) drive the slow ones (sprouts, trees growing).</summary>
    public struct ShapingClock
    {
        /// <summary>Real seconds since the view first saw it (large for one loaded from a save: its arrival is over).</summary>
        public float sinceSeen;
        /// <summary>Real seconds since the view saw the answer (−1 = not answered).</summary>
        public float sinceAnswered;
        /// <summary>Real seconds since it was taken back (−1 = it wasn't).</summary>
        public float sinceLeaving;
        /// <summary>Game months since it was placed (the view day plus the hour's fraction, minus the placing month).</summary>
        public float ageMonths;
        /// <summary>Game months since the people answered (−1 = not answered).</summary>
        public float monthsSinceAnswer;
        /// <summary>The box's era now (the grove and the channel level up with it).</summary>
        public AquariumCiv.Sim.Era era;
        /// <summary>The view day (= sim month) now, for the season.</summary>
        public long day;

        public bool Answered => sinceAnswered >= 0f || monthsSinceAnswer >= 0f;
        public bool Leaving => sinceLeaving >= 0f;
    }

    // ------------------------------------------------------------------ rain

    /// <summary>The rain at a moment: a cloud over the spot, rain falling from it, the soil darkening, sprouts coming up.
    /// Camp: a shower; village: steady rain; town: a storm with lightning (the tool's level when it was used).</summary>
    public struct RainLook
    {
        public float x, z, radius;
        /// <summary>The cloud, 0 (none) to 1 (fully formed): scale and opacity.</summary>
        public float cloud;
        /// <summary>How hard it rains, 0..1 (particle rate, the rain sheet's opacity).</summary>
        public float rain;
        /// <summary>The soil under it darkened, 0..1 (a tint on the wet cells).</summary>
        public float wet;
        /// <summary>Sprouts over the wet ground, 0 (none) to 1 (grown).</summary>
        public float sprouts;
        /// <summary>A lightning flash now, 0..1 (storms only).</summary>
        public float flash;
        public bool storm;
        /// <summary>The land cells it wets (the soil tint, where sprouts come up).</summary>
        public IReadOnlyList<int> cells;
    }

    // ------------------------------------------------------------------ grove

    public enum GroveStyle { Woods, Blossom, Orchard }

    /// <summary>One tree of a grove: where, how grown (0 = not yet out of the ground, 1 = full size), its blossom and fruit.</summary>
    public struct TreeLook
    {
        public float x, z;
        public float growth;
        public float blossom, fruit;
        /// <summary>A stable per-tree number (mesh variant, yaw).</summary>
        public int seed;
    }

    public struct GroveLook
    {
        public GroveStyle style;
        public float x, z;
        public List<TreeLook> trees;
    }

    // ------------------------------------------------------------------ channel

    public enum ChannelStyle { Ditch, Irrigation, MillRace }

    /// <summary>One cell of a channel, in order from the pond to the spot.</summary>
    public struct ChannelCellLook
    {
        public int cell;
        public float x, z;
        /// <summary>Carved into the turf, 0..1 (the trench's depth).</summary>
        public float carved;
        /// <summary>Water in it, 0..1 (the water strip's fill and its flow).</summary>
        public float water;
    }

    public struct ChannelLook
    {
        public ChannelStyle style;
        public List<ChannelCellLook> cells;
        /// <summary>The mill race's little fall (town): the index into <see cref="cells"/>, −1 = none.</summary>
        public int fallAt;
        /// <summary>Where the water runs now, along the channel's length (0 = the pond, 1 = the spot): the flow's head.</summary>
        public float flowHead;
    }

    // ------------------------------------------------------------------ herd

    public enum HerdSpecies { Deer, Goat, Sheep, Fish }

    public enum AnimalPose { Hidden, Run, Walk, Graze, Swim, Dive }

    public struct AnimalLook
    {
        public float x, z;
        /// <summary>Facing, radians, 0 = +z (as Unity's yaw, in radians).</summary>
        public float heading;
        public AnimalPose pose;
        /// <summary>0..1: fades in on a fish's splash, out on a dive.</summary>
        public float alpha;
        public int seed;
    }

    public struct HerdLook
    {
        public HerdSpecies species;
        public List<AnimalLook> animals;
        /// <summary>A splash where a shoal lands in the water, 0..1.</summary>
        public float splash;
    }

    // ------------------------------------------------------------------ the people's answer and the take-back

    /// <summary>
    /// The people's answer, shown (#85): the answer's building lights up as it arrives (the chunked rebuild), a world label
    /// names the gift ("The Keeper's rain"), and the chronicle line rises as a bubble over the credited notable (or the
    /// building). The card is the chronicle's own (<see cref="chronicleId"/>).
    /// </summary>
    public struct AnswerLook
    {
        public bool active;
        public int nudgeId, buildingId, chronicleId, notableId;
        /// <summary>Where it's shown: the building, else the gift.</summary>
        public float x, z;
        /// <summary>The building's highlight, 0..1 (a glow and a soft ring).</summary>
        public float highlight;
        /// <summary>The world label ("The Keeper's rain") and its opacity.</summary>
        public string label;
        public float labelAlpha;
        /// <summary>The chronicle line as a bubble and its opacity.</summary>
        public string bubble;
        public float bubbleAlpha;
        /// <summary>0..1 through the show; 1 = over (the view marks it reported).</summary>
        public float progress;
    }

    /// <summary>The "Take back" chip over a shaping the people haven't used yet (research 10 "Undo").</summary>
    public struct TakeBackChip
    {
        public bool visible;
        public int nudgeId;
        public float x, z;
    }

    /// <summary>What happened in a <see cref="ShapingShow.Sync"/>: the view plays the cue and starts the effect.</summary>
    public enum ShapingEventKind { Placed, Answered, TakenBack, Gone }

    public struct ShapingEvent
    {
        public ShapingEventKind kind;
        public int nudgeId;
        public string tool;
        /// <summary>The sound cue (<see cref="AquariumCiv.Audio.CueLibrary"/>), or null.</summary>
        public string cue;
    }
}
