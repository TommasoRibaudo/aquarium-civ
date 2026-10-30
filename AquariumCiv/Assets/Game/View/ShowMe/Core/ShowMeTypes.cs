using System.Collections.Generic;
using AquariumCiv.Life;
using AquariumCiv.Sim;

namespace AquariumCiv.View.ShowMe
{
    /// <summary>What "Show me" frames (chronicle-and-history §2.5 step 1).</summary>
    public enum ShowMeSubject
    {
        /// <summary>Nothing to show (no entry).</summary>
        None,
        /// <summary>The entry's building.</summary>
        Building,
        /// <summary>The person it's about, wherever they are now (the view follows their doll; the point is their home).</summary>
        Person,
        /// <summary>A relic in the world.</summary>
        Relic,
        /// <summary>The Keeper's shaping the people answered (no building: the town was full, or the building is gone).</summary>
        Shaping,
        /// <summary>The remembering stone (a death; a person no longer in the box).</summary>
        Remembering,
        /// <summary>Where it happened (the entry's stored spot).</summary>
        Spot,
        /// <summary>The year's spot: where that year's best-placed story happened (the entry has no place of its own).</summary>
        YearSpot,
        /// <summary>The whole box (an era, the weather, a naming): a wide view.</summary>
        Box,
    }

    /// <summary>Why the target isn't the first choice (§2.5: the building, else the person, else where it happened).</summary>
    public enum ShowMeFallback
    {
        None,
        /// <summary>The building it happened at is gone (retired, rebuilt): where it stood.</summary>
        BuildingGone,
        /// <summary>The person died: where it happened, else the remembering stone.</summary>
        PersonDied,
        /// <summary>The relic isn't in the box (not found, carried away).</summary>
        RelicGone,
        /// <summary>No place recorded and nobody to follow: the year's spot or the whole box.</summary>
        NoPlace,
    }

    /// <summary>
    /// Where the camera goes for "Show me" and how it frames it (#76). Pure data: the view flies there (BoxCamera.FlyTo /
    /// FlyToPerson) over at most <see cref="ShowMeCore.MaxFlySeconds"/>, puts the point at <see cref="anchorY"/> of the screen
    /// height (above the card), then hands control back.
    /// </summary>
    public sealed class ShowMeTarget
    {
        public ChronicleEntry entry;
        public ShowMeSubject subject;
        public ShowMeFallback fallback;
        /// <summary>A ground point (box space, as the sim's x, z) to fly to. False only for <see cref="ShowMeSubject.None"/>.</summary>
        public bool hasPoint;
        public float x, z;
        /// <summary>The planner's place for it (FlyToPlace), when there is one; None otherwise.</summary>
        public Place place;
        public int buildingId = -1, nudgeId = -1;
        public string relicId = "";
        /// <summary>Follow this notable's doll (live position), falling back to (<see cref="x"/>, <see cref="z"/>) when the
        /// view can't find the doll. -1 = a fixed point.</summary>
        public int followId = -1;
        /// <summary>The living people in the story, lead first (to keep in frame; the echo's principals).</summary>
        public readonly List<int> people = new List<int>();
        /// <summary>The camera's end zoom (BoxCamera: &lt; 1 = closer; 1 = the whole box).</summary>
        public float zoom = ShowMeCore.PlaceZoom;
        /// <summary>Where the point sits on screen, as a fraction of the height from the bottom (the card covers the bottom).</summary>
        public float anchorY = ShowMeCore.AnchorAboveCard;

        public override string ToString() =>
            $"{subject}{(fallback != ShowMeFallback.None ? " (" + fallback + ")" : "")} at ({x:0.00}, {z:0.00}) zoom {zoom:0.00}" +
            (followId >= 0 ? $" follow {followId}" : "") + (buildingId >= 0 ? $" building {buildingId}" : "") +
            (!string.IsNullOrEmpty(relicId) ? $" relic {relicId}" : "") + (nudgeId >= 0 ? $" shaping {nudgeId}" : "");
    }

    /// <summary>Everything "Show me" does for one card or entry: fly there, then the echo (or not), with its line.</summary>
    public sealed class ShowMePlan
    {
        public ChronicleEntry entry;
        public ShowMeTarget target;
        /// <summary>Always set; <see cref="EchoScene.Plays"/> false = fly-to only (<see cref="EchoScene.skip"/> says why).</summary>
        public EchoScene echo;
        /// <summary>Seconds after the flight lands before the echo starts (the people "notice" first).</summary>
        public float echoDelay = ShowMeCore.EchoDelay;

        public bool HasEcho => echo != null && echo.Plays;
    }

    /// <summary>A speech bubble in the world, in the people's voice (#76): what, who, how long. The view puts it over the
    /// speaker's doll (a crowd line: any doll on screen), never over the card.</summary>
    public sealed class WorldBubble
    {
        /// <summary>Never said twice: "bank:12", "entry:340:q" (the entry's quote), "entry:340:v" (the lead's own words),
        /// "bank:21:me" (a bank story line in the speaker's own words).</summary>
        public string key;
        public string text;
        /// <summary>Who says it: a notable id, or -1 = the crowd.</summary>
        public int speakerId = -1;
        public BubbleSource source;
        public int entryId = -1, lineNo;
        /// <summary>How long it stays (real seconds, from its length).</summary>
        public float seconds;
        /// <summary>The text broken for a phone-sized bubble (<see cref="PeopleVoice.Wrap"/>).</summary>
        public List<string> lines = new List<string>();

        public override string ToString() => $"[{source} {key}] {(speakerId >= 0 ? "#" + speakerId : "crowd")}: {text}";
    }

    public enum BubbleSource
    {
        /// <summary>A line of the bank for what's happening now (the glass, rain, the lamp, a return...).</summary>
        Moment,
        /// <summary>A recent story's quote (<see cref="ChronicleEntry.quote"/>).</summary>
        StoryQuote,
        /// <summary>A recent story in the words of the person it's about.</summary>
        StoryVoice,
        /// <summary>A bank story line (a card line) rephrased in the speaker's own words.</summary>
        BankVoice,
        /// <summary>An any-time line of the bank.</summary>
        Ambient,
    }
}
