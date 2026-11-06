using System;
using System.Collections.Generic;
using AquariumCiv.Sim;

namespace AquariumCiv.Life
{
    /// <summary>The wordless symbols a ritual or festival shows over its people (#276; docs/research/12-wordless-communication.md:
    /// bold flat glyphs, no text, no faces on the dolls).</summary>
    public enum RitualSymbol
    {
        /// <summary>The sky: the first festival's speech, a cheer, the crumb tasted.</summary>
        Sun,
        /// <summary>The Keeper doll: carrying it, setting it up, bowing, copying its pose.</summary>
        Doll,
        /// <summary>Looking: watching, listening, wary gazes, the Lens.</summary>
        Eye,
        /// <summary>Music: dancing and singing.</summary>
        Notes,
        /// <summary>Love and new life: the couple at their wedding, the parent lifting the newborn (t1-moments).</summary>
        Heart,
    }

    /// <summary>One symbol bubble over one person for a span of the view day (view hours). The view draws it.</summary>
    public struct RitualBubble
    {
        public PersonRef who;
        public RitualSymbol symbol;
        public float start, end;
        public RelicCast role;
    }

    /// <summary>A "start a ritual now" result: the moment as the planner staged it and the bubbles for it.</summary>
    public sealed class RitualNow
    {
        public RelicMomentInfo moment;
        public List<RitualBubble> bubbles;
    }

    /// <summary>
    /// #276 (pure): which people show which symbol, and when, during a relic moment or a festival evening. Worked out from the
    /// moment's beats (the cast and the beat's activity), no dice, no view. At most <see cref="MaxAlive"/> bubbles at once (the
    /// research's 4 to 6: a column of glyphs is noise), one per person at a time, each at most <see cref="MaxBubbleSeconds"/>
    /// real seconds. The principals (the owner, the second) win the slots, then the crowd in cast order.
    /// </summary>
    public static class RitualBubbles
    {
        public const int MaxAlive = 5;
        public const float MaxBubbleSeconds = 5f;
        /// <summary>The slots a festival evening is cut into: each slot a different few people show a bubble.</summary>
        public const int FestivalSlots = 10;

        /// <summary>The symbol a beat shows, or null when it shows none (walking, sitting out).</summary>
        public static RitualSymbol? SymbolFor(RelicBeat beat)
        {
            if (beat == null) return null;
            if (beat.prop == Prop.Doll) return RitualSymbol.Doll;
            switch (beat.kind)
            {
                case ActivityKind.Dance: case ActivityKind.LopsidedDance: case ActivityKind.Sing:
                    return RitualSymbol.Notes;
                case ActivityKind.Cheer: case ActivityKind.Speech: case ActivityKind.Taste: case ActivityKind.Clap:
                    return RitualSymbol.Sun;
                case ActivityKind.Deliver: case ActivityKind.SetUp: case ActivityKind.Bow: case ActivityKind.CopyPose:
                    return beat.prop == Prop.Crumb ? RitualSymbol.Sun : RitualSymbol.Doll;
                case ActivityKind.Watch: case ActivityKind.Listen: case ActivityKind.ListenStory: case ActivityKind.Gaze:
                case ActivityKind.GazeWary: case ActivityKind.LookThroughLens: case ActivityKind.TurnLens: case ActivityKind.CreepUp:
                case ActivityKind.Freeze: case ActivityKind.Queue:
                    return RitualSymbol.Eye;
                default:
                    return null;
            }
        }

        /// <summary>The bubbles for a relic moment as the planner staged it (<see cref="LifePlanner.StartRelicMoment"/>).
        /// <paramref name="def"/> is the moment's beats (<see cref="RelicMoments.For"/>).</summary>
        public static List<RitualBubble> For(RelicMomentInfo m, RelicMomentDef def)
        {
            if (m == null || def == null || m.end <= m.start) return new List<RitualBubble>();
            float span = m.end - m.start;
            float cap = span * Math.Min(1f, MaxBubbleSeconds / Math.Max(0.1f, def.seconds)); // a bubble's longest stretch, view hours
            int order = 0;
            var cand = new List<(int prio, RitualBubble b)>();
            foreach (var (who, role) in m.cast)
            {
                order++;
                if (who.Equals(PersonRef.None)) continue;
                foreach (var beat in def.For(role))
                {
                    var sym = SymbolFor(beat);
                    if (sym == null) continue;
                    float t0 = m.start + span * beat.from, t1 = m.start + span * beat.to;
                    t1 = Math.Min(t1, t0 + cap);
                    if (t1 - t0 < 1e-4f) continue;
                    cand.Add((Rank(role) * 1000 + order, new RitualBubble { who = who, symbol = sym.Value, start = t0, end = t1, role = role }));
                }
            }
            return Pick(cand);
        }

        /// <summary>The bubbles for a festival's gathering (<paramref name="from"/>..<paramref name="to"/> view hours): the
        /// first slot a sun over a few, then music notes over the dancers, an eye now and then over the listeners.</summary>
        public static List<RitualBubble> ForFestival(float from, float to, IReadOnlyList<PersonRef> people)
        {
            var cand = new List<(int prio, RitualBubble b)>();
            if (people == null || people.Count == 0 || to <= from) return new List<RitualBubble>();
            float slot = (to - from) / FestivalSlots;
            int n = people.Count;
            for (int s = 0; s < FestivalSlots; s++)
            {
                var sym = s == 0 ? RitualSymbol.Sun : s % 4 == 3 ? RitualSymbol.Eye : RitualSymbol.Notes;
                int show = Math.Min(n, MaxAlive);
                for (int k = 0; k < show; k++)
                {
                    int who = (s * show + k) % n; // a different few each slot, everyone gets a turn
                    cand.Add((s * 100 + k, new RitualBubble { who = people[who], symbol = sym, start = from + s * slot, end = from + (s + 1) * slot, role = RelicCast.Onlookers }));
                }
            }
            return Pick(cand);
        }

        /// <summary>#370: a staged moment's symbols (<see cref="LifePlanner.StageHeldMoment"/>): music notes over the dancers, a sun
        /// over the vow, the speech, the lift and the pop, an eye over the kid who spotted the Keeper. Long steps are cut into
        /// <see cref="MaxBubbleSeconds"/> slices that go round the cast; at most <see cref="MaxAlive"/> at once, one per person.</summary>
        public static List<RitualBubble> ForHeldMoment(HeldMomentScene sc)
        {
            var cand = new List<(int prio, RitualBubble b)>();
            if (sc?.plan == null) return new List<RitualBubble>();
            var start = new ViewTime(sc.day, sc.start);
            float Hour(float secs) { var t = LifeClock.Advance(start, secs); return t.day > sc.day ? 24f : t.hour; }
            var members = sc.plan.members;
            for (int i = 0; i < members.Count; i++)
            {
                var m = members[i];
                var who = m.key > 0 ? PersonRef.Notable(m.key) : PersonRef.Crowd(-m.key - 1);
                bool lead = m.role is MomentRole.Couple or MomentRole.Parent or MomentRole.Chief or MomentRole.Leader or MomentRole.Spotter;
                foreach (var s in m.steps)
                {
                    RitualSymbol? sym = s.beat switch
                    {
                        StageBeat.Ring or StageBeat.Centre => RitualSymbol.Notes,
                        StageBeat.Vow or StageBeat.Speech or StageBeat.LiftBaby or StageBeat.GoldPop => RitualSymbol.Sun,
                        StageBeat.Point => RitualSymbol.Eye,
                        _ => null,
                    };
                    if (m.role == MomentRole.Couple && s.beat is StageBeat.WalkIn or StageBeat.Vow or StageBeat.Join or StageBeat.Centre) sym = RitualSymbol.Heart; // the wedding says so
                    else if (m.role == MomentRole.Parent && s.beat == StageBeat.LiftBaby) sym = RitualSymbol.Heart; // the newborn too
                    if (sym == null) continue;
                    int slices = Math.Max(1, (int)Math.Ceiling((s.t1 - s.t0) / MaxBubbleSeconds - 1e-3f));
                    for (int k = 0; k < slices; k++)
                    {
                        float t0 = s.t0 + k * MaxBubbleSeconds, t1 = Math.Min(s.t1, t0 + MaxBubbleSeconds);
                        float h0 = Hour(t0), h1 = Hour(t1);
                        if (h1 - h0 < 1e-4f) continue;
                        int turn = members.Count > 0 ? (i + members.Count - (k * MaxAlive) % members.Count) % members.Count : i; // a different few each slice
                        cand.Add((k * 10000 + (lead ? 0 : 1000) + turn, new RitualBubble { who = who, symbol = sym.Value, start = h0, end = h1, role = lead ? RelicCast.Owner : RelicCast.Grown }));
                    }
                }
            }
            return Pick(cand);
        }

        static int Rank(RelicCast c) => c switch
        {
            RelicCast.Owner => 0, RelicCast.Second => 1, RelicCast.Holder => 1, RelicCast.Heir => 1,
            RelicCast.Grown => 2, RelicCast.Kids => 3, _ => 4,
        };

        /// <summary>Highest priority first: keep a bubble when its person has none then and fewer than
        /// <see cref="MaxAlive"/> are alive at any moment of it. Returned in start order.</summary>
        static List<RitualBubble> Pick(List<(int prio, RitualBubble b)> cand)
        {
            cand.Sort((a, b) => a.prio != b.prio ? a.prio.CompareTo(b.prio) : a.b.start.CompareTo(b.b.start));
            var kept = new List<RitualBubble>();
            foreach (var (_, b) in cand)
            {
                bool ok = true;
                foreach (var k in kept)
                    if (k.end > b.start + 1e-5f && k.start < b.end - 1e-5f && k.who.Equals(b.who)) { ok = false; break; }
                if (ok && !WouldOverflow(kept, b)) kept.Add(b);
            }
            kept.Sort((a, b) => a.start != b.start ? a.start.CompareTo(b.start) : a.symbol.CompareTo(b.symbol));
            return kept;
        }

        /// <summary>True when adding <paramref name="b"/> would put more than <see cref="MaxAlive"/> bubbles alive at once
        /// (checked at b's start and every start inside b's span, where the count can peak).</summary>
        static bool WouldOverflow(List<RitualBubble> kept, RitualBubble b)
        {
            var pts = new List<float> { b.start };
            foreach (var k in kept) if (k.start > b.start && k.start < b.end - 1e-5f) pts.Add(k.start);
            foreach (var p in pts)
            {
                int alive = 1;
                foreach (var k in kept) if (k.start <= p + 1e-5f && k.end > p + 1e-5f) alive++;
                if (alive > MaxAlive) return true;
            }
            return false;
        }

        /// <summary>The pure hook for the cheat "start a ritual now": gives the relic when the box hasn't found it, stages its
        /// moment at <paramref name="at"/> and returns the moment and its bubbles. Null when nobody can play it.</summary>
        public static RitualNow StartNow(SimCore sim, LifePlanner planner, ViewTime at, string relicId = RelicId.KeeperDoll)
        {
            if (sim == null || planner == null) return null;
            var r = sim.FindRelic(relicId);
            if (r == null || !r.Found) sim.DebugGiveRelic(relicId);
            var m = planner.StartRelicMoment(relicId, at);
            if (m == null) return null;
            return new RitualNow { moment = m, bubbles = For(m, RelicMoments.For(relicId)) };
        }
    }
}
