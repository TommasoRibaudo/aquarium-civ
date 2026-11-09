// #367 Visit loop B11: the planner half of the background-life director. The grown-ups' errands it starts (a water run, a pick
// at a bush) are short live scenes on the relic moments' staging: the planner walks the person there along the paths, they do
// the beat, and they walk back into their day. Nothing is planned ahead or saved, and no sim dice.
using System;
using System.Collections.Generic;
using AquariumCiv.Sim;

namespace AquariumCiv.Life
{
    public sealed partial class LifePlanner
    {
        /// <summary>The draw at the water (kneel, dip, hold, lift; B4 #360 plays it in about 3.5 s) and the pick at a bush [est].</summary>
        public const float StoryDrawSeconds = 4.5f, StoryPickSeconds = 14f;
        /// <summary>An errand that would take longer than this (a far walk) isn't started: it's a background story, not a trip [est].</summary>
        public const float StoryMaxErrandSeconds = 40f;
        /// <summary>Slack on a planned walk (the route the view walks is a little longer than the planner's line) [est].</summary>
        const float StoryWalkPad = 0.4f;

        readonly List<int> storyBushes = new List<int>(2);

        /// <summary>Where the water is drawn now (the well, the pond, the wet low ground), box units. False without positions.</summary>
        public bool StoryWaterSpot(out float x, out float z)
        {
            Sync();
            return positions.TryGetPosition(WaterPlace, out x, out z);
        }

        /// <summary>The bush with berries nearest (x, z) on this view day, and where it stands. False with no living land or no berries.</summary>
        public bool StoryBushNear(float x, float z, long day, out int cell, out float bx, out float bz)
        {
            Sync();
            cell = -1; bx = bz = 0f;
            if (!sim.EcologyOn) return false;
            sim.NearestBushes(x, z, 1, day, (int)SeasonOn(day), true, storyBushes);
            if (storyBushes.Count == 0) return false;
            cell = storyBushes[0];
            return positions.TryGetPosition(Place.Land(PlaceKind.BushCell, cell), out bx, out bz);
        }

        /// <summary>Is <paramref name="who"/> in an errand the director started, at <paramref name="t"/>?</summary>
        public bool InLifeStory(PersonRef who, ViewTime t)
        {
            foreach (var sc in live)
                if (sc.kind == SceneKind.LifeStory && sc.day == t.day && t.hour < sc.end && sc.staging != null && sc.staging.segs.ContainsKey(who.Key)) return true;
            return false;
        }

        /// <summary>
        /// #367: start a grown-up's errand now: <see cref="StoryKind.WaterRun"/> (walk to the water with the empty bucket, draw, carry the
        /// full one home) or <see cref="StoryKind.Pick"/> (walk to the bush <paramref name="bushCell"/> with a basket and pick). Only for
        /// someone idle (<see cref="StoryRules.FreeFor"/>): never anyone asleep, working, eating, hunting, raising a tent or in a moment.
        /// Returns false (and why) when it can't; <paramref name="seconds"/> is how long it takes, walks included.
        /// </summary>
        public bool StartStoryErrand(StoryKind kind, PersonRef who, ViewTime at, int bushCell, out float seconds, out string why)
        {
            seconds = 0f; why = "";
            Sync();
            EnsureDay(at.day);
            PruneLive(at);
            if (!byKey.TryGetValue(who.Key, out var p) || !p.Adult) { why = "not a grown-up in the box"; return false; }
            if (!Free(p, at) || InLifeStory(who, at)) { why = "busy"; return false; }
            var now = PlanPerson(p, at);
            if (!StoryRules.FreeFor(now, p.stage)) { why = "not idle: " + now.kind; return false; }
            var from = StopPlace(p, at.hour);
            var list = new List<Seg>(3);
            float t = 0f;
            void Add(ActivityKind k, Place place, Prop prop, float secs, bool carries, bool still = false)
            {
                float h0 = HourOf(at, LifeClock.Advance(at, t));
                t += secs;
                float h1 = HourOf(at, LifeClock.Advance(at, t));
                if (h1 <= h0 + 1e-5f) return;
                var s = Mk(k, h0, h1, place, prop, ActivitySource.Scene);
                s.carries = carries;
                s.still = still; // the draw is done where they stand, not a move from wherever they were
                if (prop == Prop.Buckets || prop == Prop.EmptyBucket) s.water = WaterNow;
                list.Add(s);
            }
            Place spot;
            switch (kind)
            {
                case StoryKind.WaterRun:
                {
                    var water = WaterPlace;
                    var home = IsValidPlace(p.home) ? p.home : fire;
                    if (!IsValidPlace(water) || home.IsNone) { why = "no water"; return false; }
                    // The empty bucket on the way (shown as a carry while walking; a moment's stand if they arrive early), the draw, the full one home.
                    Add(ActivityKind.Watch, water, Prop.EmptyBucket, WalkSecs(WalkLen(from, water)) + StoryWalkPad, true);
                    Add(ActivityKind.FetchWater, water, Prop.EmptyBucket, StoryDrawSeconds, false, still: true);
                    Add(ActivityKind.Watch, home, Prop.Buckets, WalkSecs(WalkLen(water, home)) + StoryWalkPad + 0.4f, true);
                    spot = water;
                    break;
                }
                case StoryKind.Pick:
                {
                    var bush = Place.Land(PlaceKind.BushCell, bushCell);
                    if (bushCell < 0 || !IsValidPlace(bush)) { why = "no bush"; return false; }
                    Add(ActivityKind.Gather, bush, Prop.Basket, WalkSecs(WalkLen(from, bush)) + StoryWalkPad + StoryPickSeconds, true);
                    spot = bush;
                    break;
                }
                default:
                    why = "not an errand"; return false;
            }
            if (list.Count == 0 || t > StoryMaxErrandSeconds) { why = "too far"; return false; }
            float end = list[list.Count - 1].end;
            if (LifeClock.Advance(at, t).day != at.day || end >= 24f) { why = "the day ends first"; return false; }
            var st = new Staging { glass = GlassPlace, spot = spot, start = at.hour, end = end };
            st.segs[p.key] = list;
            st.roles[p.key] = RelicCast.Owner;
            st.order.Add(p.key);
            AddLive(SceneKind.LifeStory, at, end, st, p.key);
            seconds = t;
            return true;
        }

        /// <summary>
        /// P9 #424 (B12.w, placed-life §2.4): a ready jar's waiting hint. The people in <paramref name="members"/> (grown-ups first, as
        /// <see cref="StoryRules.HintCast"/> casts them) walk to the empty spot by <paramref name="cell"/> and show what is missing there:
        /// a gatherer with an empty basket looks round the meadow, a hunter crouches with his spear and studies the grass, a child sits at
        /// the empty pond; the later stages add a second grown-up who points, and a kid's pretend (tasting the grass, playing "rabbit",
        /// sticks held up as antlers). Only idle people (<see cref="StoryRules.FreeFor"/>); someone busy is left out. False when nobody
        /// could go. <paramref name="seconds"/> is the longest of their errands, walks included.
        /// </summary>
        public bool StartStoryHint(int species, int stage, IReadOnlyList<int> members, int cell, ViewTime at, out float seconds, out string why, bool force = false)
        {
            seconds = 0f; why = "";
            Sync();
            EnsureDay(at.day);
            PruneLive(at);
            if (cell < 0) { why = "no spot"; return false; }
            var st = new Staging { glass = GlassPlace, spot = Place.Land(PlaceKind.Arrival, cell * 8 + 1), start = at.hour, end = at.hour };
            int slot = 1, adultsSeen = 0, kidsSeen = 0;
            StoryRules.HintCast(species, stage, out int wantAdults, out int wantKids); // the cast may hold spares: take the first free ones
            foreach (int key in members)
            {
                if (!byKey.TryGetValue(key, out var p)) { why = "not in the box"; continue; }
                if (!Free(p, at) || InLifeStory(p.who, at)) { why = "busy: " + FreeWhy(p, at); continue; }
                var now = PlanPerson(p, at);
                if (!force && !StoryRules.FreeFor(now, p.stage)) { why = "not idle: " + now.kind; continue; } // the cheat ("Hint now") borrows a worker for its 14 s
                bool kid = p.stage == LifeStage.Kid;
                if (kid ? kidsSeen >= wantKids : adultsSeen >= wantAdults) continue;
                if (kid) kidsSeen++;
                int role = kid ? 2 : adultsSeen++ == 0 ? 0 : 1; // 0: the one who looks, 1: the one who points, 2: a kid's pretend
                var spot = Place.Land(PlaceKind.Arrival, cell * 8 + Math.Min(7, slot++));
                var from = StopPlace(p, at.hour);
                var list = new List<Seg>(3);
                float t = 0f;
                void Add(ActivityKind k, Prop prop, float secs, bool carries, bool still = false)
                {
                    float h0 = HourOf(at, LifeClock.Advance(at, t));
                    t += secs;
                    float h1 = HourOf(at, LifeClock.Advance(at, t));
                    if (h1 <= h0 + 1e-5f) return;
                    var s = Mk(k, h0, h1, spot, prop, ActivitySource.Scene);
                    s.carries = carries;
                    s.still = still;
                    list.Add(s);
                }
                float walk = WalkSecs(WalkLen(from, spot)) + StoryWalkPad;
                HintBeats(species, role, out var k1, out var p1, out float s1, out var k2, out float s2);
                Add(k1, p1, walk + s1, p1 != Prop.None);
                if (k2 != ActivityKind.None) Add(k2, p1, s2, false, still: true);
                if (list.Count == 0) { why = "no beats"; continue; }
                if (t > StoryMaxErrandSeconds * 1.5f) { why = $"too far ({t:0} s)"; continue; }
                st.segs[p.key] = list;
                st.roles[p.key] = role == 0 ? RelicCast.Owner : role == 1 ? RelicCast.Second : RelicCast.Kids;
                st.order.Add(p.key);
                st.end = Math.Max(st.end, list[list.Count - 1].end);
                seconds = Math.Max(seconds, t);
            }
            if (st.order.Count == 0) { if (why == "") why = "nobody free"; return false; }
            if (st.end >= 24f || LifeClock.Advance(at, seconds).day != at.day) { why = "the day ends first"; return false; }
            AddLive(SceneKind.LifeStory, at, st.end, st, st.order[0]);
            return true;
        }

        /// <summary>B12.w: what each hint actor does at the spot (the arrival beat while walking there and a while after, then a second beat).</summary>
        static void HintBeats(int species, int role, out ActivityKind k1, out Prop prop, out float s1, out ActivityKind k2, out float s2)
        {
            k2 = ActivityKind.None; s2 = 0f; s1 = 4f; prop = Prop.None;
            if (role == 2)
            {
                // the kids' pretend: tasting the grass (no berries), playing "rabbit", sticks up as antlers, a pebble in the empty water
                switch (species)
                {
                    case 0: k1 = ActivityKind.Taste; s1 = 5f; return;
                    case 1: k1 = ActivityKind.Play; s1 = 7f; return;
                    case 2: k1 = ActivityKind.HoldUp; prop = Prop.Stick; s1 = 4f; return;
                    default: k1 = ActivityKind.Watch; s1 = 6f; return;
                }
            }
            if (role == 1) { k1 = ActivityKind.Point; s1 = 3f; k2 = ActivityKind.Search; s2 = 2.5f; return; }
            switch (species)
            {
                case 0: k1 = ActivityKind.Search; prop = Prop.Basket; s1 = 5f; return;                                   // the empty basket
                case 1: k1 = ActivityKind.Stalk; prop = Prop.Spear; s1 = 4f; k2 = ActivityKind.Search; s2 = 2f; return;  // crouch, study, stand
                case 2: k1 = ActivityKind.Watch; prop = Prop.Spear; s1 = 4f; k2 = ActivityKind.Search; s2 = 2f; return;  // looking into the wood
                case 3: k1 = ActivityKind.Inspect; s1 = 4f; return;                                                      // kneels at the bank
                default: k1 = ActivityKind.Watch; prop = Prop.Spear; s1 = 5f; k2 = ActivityKind.Search; s2 = 2f; return; // the fisher with nothing to spear
            }
        }

        /// <summary>End every errand the director started (the cheat turned it off, the box changed): they walk back into their day.</summary>
        public int EndLifeStories()
        {
            int n = live.RemoveAll(sc => sc.kind == SceneKind.LifeStory);
            if (n > 0) LiveChanged();
            return n;
        }
    }
}
