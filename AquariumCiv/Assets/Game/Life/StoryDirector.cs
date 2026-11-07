// #367 Visit loop B11: the background-life director (docs/design/animation-specs.md B11, research 17 §4 principle 7, §5 #10, §6.7).
// "There is always one story on screen to watch; a small camp is calm, a town is busy." Pure (System + Life + MomentRules only):
// Tools/SimTests compiles it. The view half (View/StoryDirectorView.cs) fills a StoryWorld from the camera's view rectangle every
// few seconds and starts what this picks.
using System;
using System.Collections.Generic;
using AquariumCiv.Sim;

namespace AquariumCiv.Life
{
    /// <summary>The cheap stories the director can start, and what a visible story is (B11). Appended values keep their numbers.</summary>
    public enum StoryKind
    {
        None,
        /// <summary>A story the director didn't start and doesn't start (a hunt, a tent, a staged moment, felling, fishing ...).</summary>
        Other,
        ChaseRabbits, Ball, Tag, Splash,
        WaterRun, HandOver, Pick,
    }

    /// <summary>The cheat: off, as the population says, or twice the rate (for the tester).</summary>
    public enum StoryDensity { Off, Normal, High }

    /// <summary>Daylight: anything; the evening: only a story at the fire (a hand-over between two sitting there); night: nothing.</summary>
    public enum StoryDayPart { Day, Evening, Night }

    /// <summary>A person on view the director may borrow: idle (not working, eating, sleeping, hunting, in a moment), and how long
    /// their idle stretch still runs (real seconds).</summary>
    public struct StoryActor
    {
        public int key;
        public bool kid, adult;
        public float x, z;
        public float freeSeconds;
        /// <summary>Sitting at the fire or the gathering place (the evening's only stage).</summary>
        public bool atFire;
    }

    /// <summary>What the view sees now, filled every look (box units, real seconds).</summary>
    public sealed class StoryWorld
    {
        public float time;
        public int population;
        public StoryDayPart part;
        /// <summary>Dawn or morning: the water is drawn then (B4).</summary>
        public bool waterHours;
        /// <summary>Distinct stories in progress inside the view rectangle (inset 10 %), each counted once.</summary>
        public int storiesInView;
        /// <summary>Batch 3: the staged (held) moments playing in view, each counted once. A moment is the foreground, not a
        /// background story: all of them together count as ONE story (<see cref="Counted"/>), so a wedding fills a tribe's view but
        /// a busy day (two moments) can't hold the director at its cap while the queue plays one after another. Their people are
        /// never free (the view leaves them out of <see cref="free"/>), so nothing started steals a moment's cast.</summary>
        public int momentsInView;
        /// <summary>What the director counts against its floor and cap: the background stories plus one for any moments.</summary>
        public int Counted => storiesInView + (momentsInView > 0 ? 1 : 0);
        /// <summary>The director's kinds among them (never start one already in view).</summary>
        public readonly List<StoryKind> kindsInView = new List<StoryKind>();
        /// <summary>Where the camera looks (the ground under the screen's middle).</summary>
        public float focusX, focusZ;
        public readonly List<StoryActor> free = new List<StoryActor>();
        public bool rabbits; public float rabbitX, rabbitZ;
        /// <summary>A hunt is on: the kids leave the rabbits to the hunter.</summary>
        public bool huntOn;
        public bool water; public float waterX, waterZ;
        /// <summary>Water the kids can splash in (the pond's edge or a puddle, not in winter).</summary>
        public bool splash; public float splashX, splashZ;
        public bool bush; public float bushX, bushZ; public int bushCell = -1;

        public void Clear()
        {
            kindsInView.Clear(); free.Clear();
            storiesInView = momentsInView = 0; rabbits = huntOn = water = splash = bush = false; bushCell = -1; waterHours = false;
        }
    }

    /// <summary>One story to start: its kind, who (person keys; the giver first in a hand-over), where, about how long.</summary>
    public sealed class StoryPlan
    {
        public StoryKind kind;
        public readonly List<int> members = new List<int>();
        public float x, z;
        public float seconds;
        public int seed;
        public int bushCell = -1;
        public override string ToString() => $"{kind} ×{members.Count}";
    }

    /// <summary>Every number of the director in one place. All [est] until the user signs the feel off (B11 "Needs the user").</summary>
    public static class StoryRules
    {
        /// <summary>How often the director counts the stories in view [est] (B11: every 3 s).</summary>
        public const float CheckSeconds = 3f;
        /// <summary>A kind it started may not start again for this long [est] (B11: 30 s).</summary>
        public const float NoRepeatSeconds = 30f;
        /// <summary>How far past their idle stretch a borrowed person may be kept (0.5 view h at work hours ≈ 8 s) [est].</summary>
        public const float MaxBorrowSeconds = 8f;
        /// <summary>Kids for one game stand within this of the first one [est]; a hand-over's giver walks at most this far [est].</summary>
        public const float GameReach = 1.6f, HandReach = 0.9f;
        /// <summary>A water run or a pick takes someone within this of the water or the bush; kids chase rabbits / splash this near [est].</summary>
        public const float ErrandReach = 2.6f, SiteReach = 2.2f;
        /// <summary>The interval jitter (±) of the own seeded stream, so starts don't tick like a metronome [est].</summary>
        public const float Jitter = 0.2f;

        /// <summary>B11's table, one value per population row (tribe 6, camp 10, village 22, town 30; <see cref="MomentRules.ByPopulation"/>):
        /// a new story about every 20 / 15 / 10 / 6 s, linear between the rows. The floor goes by the moments' rows (<see cref="MomentRules.SizeOf"/>):
        /// at least 0 / 1 / 1 / 2 in view, at most one more than that [est].</summary>
        static readonly double[] IntervalAt = { 20, 15, 10, 6 };

        public static float IntervalSeconds(int population, StoryDensity density)
        {
            double s = MomentRules.ByPopulation(population, IntervalAt);
            return (float)(density == StoryDensity.High ? s * 0.5 : s);
        }

        public static int Floor(int population, StoryDensity density)
            => MomentRules.SizeOf(population) switch { MomentBoxSize.Tribe => 0, MomentBoxSize.Town => 2, _ => 1 } + (density == StoryDensity.High ? 1 : 0);

        /// <summary>At most this many stories in view before the director stops adding (B11: village 1–2, town 2–3).</summary>
        public static int Cap(int population, StoryDensity density) => Floor(population, density) + 1;

        /// <summary>The longest the view may stay without a story before one is started (the tribe's "none for 20 s"): the
        /// interval less two looks, so the gap, the look's own wait included, stays under the interval.</summary>
        public static float QuietSeconds(int population, StoryDensity density) => Math.Max(CheckSeconds, IntervalSeconds(population, density) - 2f * CheckSeconds);

        /// <summary>About how long each story plays once started (real seconds) [est].</summary>
        public static float Seconds(StoryKind k) => k switch
        {
            StoryKind.ChaseRabbits or StoryKind.Ball or StoryKind.Tag or StoryKind.Splash => 28f,
            StoryKind.WaterRun => 18f,
            StoryKind.HandOver => 6f,
            StoryKind.Pick => 20f,
            _ => 10f,
        };

        /// <summary>B11's order: the first that's possible starts. Kids' games first (they borrow nobody from work), then the
        /// grown-ups' errands.</summary>
        public static readonly StoryKind[] Order =
        {
            StoryKind.ChaseRabbits, StoryKind.Ball, StoryKind.Tag, StoryKind.Splash, StoryKind.WaterRun, StoryKind.HandOver, StoryKind.Pick,
        };

        public static bool IsKidGame(StoryKind k) => k is StoryKind.ChaseRabbits or StoryKind.Ball or StoryKind.Tag or StoryKind.Splash;

        public static StoryDayPart PartOf(float hour)
        {
            var p = LifeClock.PhaseOf(hour);
            return p == DayPhase.Night ? StoryDayPart.Night : p == DayPhase.Evening ? StoryDayPart.Evening : StoryDayPart.Day;
        }

        /// <summary>Dawn and morning: when the water is drawn.</summary>
        public static bool WaterHours(float hour) => hour >= LifeClock.Dawn && hour < LifeClock.Midday;

        /// <summary>
        /// Can the director borrow this person now? Only someone idle in their day: not at work, a chore, a carry or a role's duty, not
        /// eating, asleep, napping or indoors, not walking somewhere, not in a hunt, a tent raising, a scene or a moment, a reaction, and
        /// not carrying a baby. Kids at play, sitting, stretching or chatting; grown-ups sitting out, chatting, listening or watching.
        /// </summary>
        public static bool FreeFor(in PersonActivity a, LifeStage stage)
        {
            if (stage is not (LifeStage.Kid or LifeStage.Adult or LifeStage.Elder)) return false;
            if (a.source != ActivitySource.Schedule || a.moving || a.enRoute) return false;
            if (a.hunt != HuntPhase.None || a.tent != TentPhase.None || !a.carrying.IsNone || !a.carriedBy.IsNone) return false;
            switch (a.kind)
            {
                case ActivityKind.SitOut: case ActivityKind.Stretch: case ActivityKind.Chat: case ActivityKind.Listen:
                    return true;
                case ActivityKind.Play:
                    return stage == LifeStage.Kid;
                case ActivityKind.SitTogether: case ActivityKind.WatchWork: case ActivityKind.Visit:
                    return stage != LifeStage.Kid;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Is this activity a story in progress (an actor, a verb, a real target, an answer; B11's "counts" column)? The view adds
        /// what the planner can't see (a play group's game, a pair beat). <paramref name="id"/>: one story for everyone in it (the
        /// three pickers at one bush count once). None: sitting, standing, a lone walk, sleep, a plain chat, a loop with no target.
        /// </summary>
        public static StoryKind StoryOf(in PersonActivity a, out long id)
        {
            id = 0;
            if (a.hunt != HuntPhase.None)
            {
                bool on = a.hunt is HuntPhase.Stalk or HuntPhase.Strike or HuntPhase.Bolt or HuntPhase.FetchSpear or HuntPhase.PullSpear
                    or HuntPhase.ToCatch or HuntPhase.PickUp or HuntPhase.HoldUp or HuntPhase.KidWatch or HuntPhase.KidCheer;
                id = 1;
                return on ? StoryKind.Other : StoryKind.None;
            }
            if (a.tent != TentPhase.None) { id = 2; return a.moving ? StoryKind.None : StoryKind.Other; }
            if (a.moving || a.enRoute) return StoryKind.None;
            long placeHash = (long)a.place.GetHashCode() << 8;
            long PlaceId(int salt) => placeHash ^ salt;
            if (a.source == ActivitySource.Scene && a.kind is not (ActivityKind.Walk or ActivityKind.SitOut or ActivityKind.Carry))
            {
                // A staged moment or a story scene; the director's own errands are told by their kind below.
                if (a.kind == ActivityKind.Gather && a.place.kind == PlaceKind.BushCell) { id = PlaceId(3); return StoryKind.Pick; }
                if (a.kind == ActivityKind.FetchWater) { id = PlaceId(4); return StoryKind.WaterRun; }
                id = 5 + (a.chronicleId + 1) * 16L;
                return StoryKind.Other;
            }
            switch (a.kind)
            {
                case ActivityKind.Gather:
                    if (a.place.kind == PlaceKind.BushCell) { id = PlaceId(3); return StoryKind.Pick; }
                    if (a.place.kind == PlaceKind.Stream) { id = PlaceId(6); return StoryKind.Other; } // digging roots
                    return StoryKind.None;
                case ActivityKind.FetchWater:
                    if (a.place.kind is PlaceKind.Pond or PlaceKind.Stream or PlaceKind.Puddle) { id = PlaceId(4); return StoryKind.WaterRun; }
                    return StoryKind.None;
                case ActivityKind.Fish: id = PlaceId(7); return StoryKind.Other;
                case ActivityKind.Chop: id = PlaceId(8); return StoryKind.Other;
                default: return StoryKind.None;
            }
        }
    }

    /// <summary>
    /// The background-life director's decision (B11): every <see cref="StoryRules.CheckSeconds"/> it counts the stories in view; if
    /// fewer than the population's floor, or none for <see cref="StoryRules.QuietSeconds"/>, or the interval is up and there's room,
    /// it picks a cheap story near the camera's focus with idle people, first possible in <see cref="StoryRules.Order"/>. Never the
    /// kind it started last, never one started in the last 30 s, never one already in view; never at night; in the evening only a
    /// hand-over at the fire. Its randomness (the jitter, who of the free) is its own seeded stream: no sim dice.
    /// </summary>
    public sealed class StoryDirector
    {
        readonly int seed;
        uint draws;
        float nextCheck = float.NegativeInfinity, lastStart = float.NaN, lastSeen = float.NaN, jitter = 1f;
        StoryKind lastKind = StoryKind.None;
        readonly Dictionary<StoryKind, float> lastOf = new Dictionary<StoryKind, float>();
        readonly List<StoryActor> pool = new List<StoryActor>();

        public StoryDirector(int seed) { this.seed = seed; }

        public StoryDensity Density = StoryDensity.Normal;
        /// <summary>Why the last look started nothing ("" = it started one).</summary>
        public string LastWhy { get; private set; } = "";
        /// <summary>Seconds until the next start may come (the panel's status line); +inf when nothing is due (full, night, off).</summary>
        public float NextIn { get; private set; } = float.PositiveInfinity;
        public int StoriesInView { get; private set; }
        public StoryKind LastKind => lastKind;
        /// <summary>When the last look was (the director's clock): <see cref="NextIn"/> counts from it.</summary>
        public float LastLook { get; private set; }
        public int Started { get; private set; }

        /// <summary>Forget the last look (the box changed, the cheat turned it back on): the next <see cref="Step"/> looks at once.</summary>
        public void Reset() { nextCheck = float.NegativeInfinity; lastSeen = float.NaN; lastStart = float.NaN; }

        uint Draw() => PlayGroups.Hash(seed, draws++, 367);
        float U01() => (Draw() >> 8) / 16777216f;

        /// <summary>
        /// One look if it's due: returns the story started (the first plan <paramref name="tryStart"/> accepted), or null. Call it every
        /// frame or every look; it only decides every <see cref="StoryRules.CheckSeconds"/>.
        /// </summary>
        public StoryPlan Step(StoryWorld w, Func<StoryPlan, bool> tryStart)
        {
            if (w.time < nextCheck) return null;
            nextCheck = w.time + StoryRules.CheckSeconds;
            LastLook = w.time;
            int inView = w.Counted;
            StoriesInView = inView;
            if (float.IsNaN(lastSeen)) lastSeen = w.time;
            if (float.IsNaN(lastStart)) lastStart = w.time; // the first look: a tribe waits its quiet stretch, a camp's floor starts one at once
            if (inView > 0) lastSeen = w.time;
            if (Density == StoryDensity.Off) return Wait("off", float.PositiveInfinity);
            if (w.part == StoryDayPart.Night) return Wait("night: everyone sleeps", float.PositiveInfinity);

            int pop = w.population;
            float interval = StoryRules.IntervalSeconds(pop, Density) * jitter;
            int floor = StoryRules.Floor(pop, Density), cap = StoryRules.Cap(pop, Density);
            float quiet = StoryRules.QuietSeconds(pop, Density);
            float sinceStart = w.time - lastStart, sinceSeen = w.time - lastSeen;
            bool below = inView < floor;
            bool tooQuiet = inView == 0 && sinceSeen >= quiet;
            bool due = inView < cap && sinceStart >= interval;
            if (!below && !tooQuiet && !due)
            {
                float next = inView >= cap ? float.PositiveInfinity
                    : Math.Max(nextCheck - w.time, Math.Min(interval - sinceStart, inView == 0 ? quiet - sinceSeen : float.PositiveInfinity));
                return Wait(inView >= cap ? $"{inView} in view (enough)" + (w.momentsInView > 0 ? $", {w.momentsInView} moment(s) as one" : "") : "not yet", next);
            }

            var plan = new StoryPlan();
            string why = "nobody free near the camera";
            foreach (var kind in StoryRules.Order)
            {
                if (w.part == StoryDayPart.Evening && kind != StoryKind.HandOver) continue;
                if (kind == lastKind || w.kindsInView.Contains(kind)) continue;
                if (lastOf.TryGetValue(kind, out float at) && w.time - at < StoryRules.NoRepeatSeconds) continue;
                if (!Cast(w, kind, plan)) continue;
                if (tryStart != null && !tryStart(plan)) { why = $"{kind}: the view couldn't start it"; continue; }
                lastKind = kind;
                lastOf[kind] = w.time;
                lastStart = w.time;
                lastSeen = w.time;
                jitter = 1f + (U01() * 2f - 1f) * StoryRules.Jitter;
                Started++;
                LastWhy = "";
                NextIn = StoryRules.CheckSeconds;
                return plan;
            }
            return Wait(why, StoryRules.CheckSeconds);
        }

        StoryPlan Wait(string why, float next) { LastWhy = why; NextIn = next; return null; }

        /// <summary>Who plays <paramref name="kind"/> now, nearest the camera's focus first; false when it can't be cast.</summary>
        bool Cast(StoryWorld w, StoryKind kind, StoryPlan plan)
        {
            plan.members.Clear();
            plan.kind = kind;
            plan.seconds = StoryRules.Seconds(kind);
            plan.bushCell = -1;
            plan.seed = (int)(Draw() & 0x7fffffff);
            float need = plan.seconds - StoryRules.MaxBorrowSeconds;
            pool.Clear();
            foreach (var a in w.free)
            {
                if (a.freeSeconds < need) continue; // their idle stretch ends too soon: they'd be late for work or a meal
                if (StoryRules.IsKidGame(kind) ? !a.kid : kind == StoryKind.HandOver ? a.kid : !a.adult) continue;
                if (w.part == StoryDayPart.Evening && !a.atFire) continue;
                pool.Add(a);
            }
            if (pool.Count == 0) return false;
            switch (kind)
            {
                case StoryKind.ChaseRabbits:
                    if (!w.rabbits || w.huntOn) return false;
                    return Group(plan, w.rabbitX, w.rabbitZ, StoryRules.SiteReach, 1, PlayGroups.MaxChase);
                case StoryKind.Splash:
                    if (!w.splash) return false;
                    return Group(plan, w.splashX, w.splashZ, StoryRules.SiteReach, 2, 3);
                case StoryKind.Ball:
                case StoryKind.Tag:
                {
                    SortBy(w.focusX, w.focusZ);
                    // The kid nearest the focus starts it, with the nearest friends round them (a hashed pick of the first two, so it isn't always the same one).
                    int first = pool.Count > 1 && U01() < 0.35f ? 1 : 0;
                    var lead = pool[first];
                    return Group(plan, lead.x, lead.z, StoryRules.GameReach, kind == StoryKind.Tag ? 2 : 1, kind == StoryKind.Tag ? 4 : PlayGroups.MaxBall);
                }
                case StoryKind.WaterRun:
                    if (!w.water || !w.waterHours || w.part != StoryDayPart.Day) return false;
                    return One(plan, w.waterX, w.waterZ);
                case StoryKind.Pick:
                    if (!w.bush || w.bushCell < 0 || w.part != StoryDayPart.Day) return false;
                    if (!One(plan, w.bushX, w.bushZ)) return false;
                    plan.bushCell = w.bushCell;
                    return true;
                case StoryKind.HandOver:
                {
                    SortBy(w.focusX, w.focusZ);
                    float best = StoryRules.HandReach * StoryRules.HandReach;
                    int gi = -1, ti = -1;
                    for (int i = 0; i < pool.Count; i++)
                        for (int j = 0; j < pool.Count; j++)
                        {
                            if (i == j) continue;
                            float dx = pool[i].x - pool[j].x, dz = pool[i].z - pool[j].z, d = dx * dx + dz * dz;
                            if (d < best - 1e-6f) { best = d; gi = i; ti = j; }
                        }
                    if (gi < 0) return false;
                    if (U01() < 0.5f) (gi, ti) = (ti, gi); // either of the two gives
                    plan.members.Add(pool[gi].key); plan.members.Add(pool[ti].key);
                    plan.x = pool[ti].x; plan.z = pool[ti].z;
                    return true;
                }
            }
            return false;
        }

        void SortBy(float x, float z)
        {
            pool.Sort((a, b) =>
            {
                float da = (a.x - x) * (a.x - x) + (a.z - z) * (a.z - z), db = (b.x - x) * (b.x - x) + (b.z - z) * (b.z - z);
                return da != db ? da.CompareTo(db) : a.key.CompareTo(b.key);
            });
        }

        /// <summary>The <paramref name="most"/> nearest to (x, z) within <paramref name="reach"/>; at least <paramref name="least"/>.</summary>
        bool Group(StoryPlan plan, float x, float z, float reach, int least, int most)
        {
            SortBy(x, z);
            foreach (var a in pool)
            {
                if (plan.members.Count >= most) break;
                if ((a.x - x) * (a.x - x) + (a.z - z) * (a.z - z) > reach * reach) break;
                plan.members.Add(a.key);
            }
            if (plan.members.Count < least) { plan.members.Clear(); return false; }
            plan.x = x; plan.z = z;
            return true;
        }

        /// <summary>The one free grown-up nearest the errand's place, within <see cref="StoryRules.ErrandReach"/>.</summary>
        bool One(StoryPlan plan, float x, float z)
        {
            SortBy(x, z);
            var a = pool[0];
            if ((a.x - x) * (a.x - x) + (a.z - z) * (a.z - z) > StoryRules.ErrandReach * StoryRules.ErrandReach) return false;
            plan.members.Add(a.key);
            plan.x = x; plan.z = z;
            return true;
        }
    }
}
