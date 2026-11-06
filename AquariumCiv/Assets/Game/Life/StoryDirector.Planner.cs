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

        /// <summary>End every errand the director started (the cheat turned it off, the box changed): they walk back into their day.</summary>
        public int EndLifeStories()
        {
            int n = live.RemoveAll(sc => sc.kind == SceneKind.LifeStory);
            if (n > 0) LiveChanged();
            return n;
        }
    }
}
