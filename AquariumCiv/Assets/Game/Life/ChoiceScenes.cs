using System;
using System.Collections.Generic;
using AquariumCiv.Sim;

namespace AquariumCiv.Life
{
    /// <summary>What marks one side of a choice scene (#224) besides the people: nothing, a marker stake at a site (which way), or an
    /// offering on the shrine or gathering stone (tradition).</summary>
    public enum ChoiceProp { None, Stake, Offering }

    /// <summary>Who stands for one side of a choice scene (#224).</summary>
    public enum ChoiceStand
    {
        /// <summary>Nobody: the prop alone.</summary>
        Nobody,
        /// <summary>The person the option points at (a <i>who</i> dilemma's candidate).</summary>
        OptionPerson,
        /// <summary>The nearest grown-up who is up and can walk there (a worker beside a stake).</summary>
        Nearest,
        /// <summary>The one who asked (the era's leader), else the leader, else the eldest grown-up: the chief at the fire.</summary>
        Asker,
        /// <summary>The strangers at the box's edge: not people of the box yet, so the view stands them there itself.</summary>
        Travellers,
    }

    /// <summary>Where a side of a choice scene is anchored.</summary>
    public enum ChoiceAnchor
    {
        /// <summary>The fire (the camp), else the leader's seat, else the gathering place.</summary>
        FireOrSeat,
        /// <summary>The option's building (the gathering grounds, the woodpile, the mill), else the gathering place.</summary>
        Building,
        /// <summary>The shrine, else the gathering stone (the fire, the green).</summary>
        ShrineOrGather,
        /// <summary>The fire, else the gathering place (the strangers' "here").</summary>
        Fire,
        /// <summary>The box's edge, out from the other side's anchor (the strangers' "over there").</summary>
        Edge,
    }

    /// <summary>One side of a choice scene as a rule: where, what marks it, who stands for it.</summary>
    public readonly struct ChoiceSideRule
    {
        public readonly ChoiceAnchor anchor;
        public readonly ChoiceProp prop;
        public readonly ChoiceStand stand;
        public ChoiceSideRule(ChoiceAnchor anchor, ChoiceProp prop, ChoiceStand stand) { this.anchor = anchor; this.prop = prop; this.stand = stand; }
    }

    /// <summary>
    /// The choice scene in the box (#224; research 14 §2.3; P7 rules): the people put their ask to the Keeper as a moment in the world,
    /// not a menu. Each dilemma kind is staged its own way: <i>who</i>, the two candidates on either side of the fire (or the seat);
    /// <i>which way</i>, two marker stakes at the two sites with a worker by each; <i>tradition</i>, two offerings on the shrine or the
    /// gathering stone with the asker behind; <i>strangers</i>, the chief at the fire and the travellers at the box's edge. They look up
    /// at the front glass and wait (an idle loop, never frozen); a touch on one answers. Pure rules and numbers: who stands where, which
    /// kind takes which props, the timing and the reactions. <see cref="LifePlanner.StageChoice"/> stages them.
    /// </summary>
    public static class ChoiceScenes
    {
        /// <summary>Real seconds a choice scene is staged for while the Keeper looks (the view stages it again before it runs out) [est].</summary>
        public const float WaitSeconds = 75f;
        /// <summary>Left without an answer: the people wait this long, then drift back to their day (the issue: ~20 s).</summary>
        public const float DriftSeconds = 20f;
        /// <summary>Real seconds of the reaction after a touch: the chosen one cheers, the others shrug or sulk, people nearby gather [est].</summary>
        public const float ReactSeconds = 8f;
        /// <summary>Share of the reaction the gatherers have to come over before they cheer.</summary>
        public const float GatherShare = 0.55f;
        /// <summary>A worker beside a stake is only fetched from this many real seconds' walk away; further, the stake stands alone [est].</summary>
        public const float MaxFetchSeconds = 14f;
        /// <summary>The looking-up and the small gesture in a wait loop, real seconds [est].</summary>
        public const float LookSeconds = 3.6f, FidgetSeconds = 1.3f;

        /// <summary>Box units: half the gap between a <i>who</i>'s two candidates, and how far in front of the fire they stand.</summary>
        public const float WhoSpread = 0.36f, WhoForward = 0.12f;
        /// <summary>Box units: half the gap between the two offerings, and how far in front of the stone.</summary>
        public const float OfferingSpread = 0.26f, OfferingForward = 0.36f;
        /// <summary>Box units: a stake stands this far in front of its site; its worker this far beside it (outward).</summary>
        public const float StakeForward = 0.2f, StakeBeside = 0.13f;
        /// <summary>Box units: the asker stands this far behind the middle of a shared anchor (who); in a tradition, between the two
        /// offerings and a step behind them, this far out from the stone (clear of the fire in a camp).</summary>
        public const float AskerBack = 0.24f, AskerOffering = 0.25f;
        /// <summary>Box units: when both sides of a <i>which way</i> fall on one place (a site missing), the stakes stand this far apart each way.</summary>
        public const float SharedStakeSpread = 0.42f;
        /// <summary>Box units: the travellers stand at least this far, at most this far, from the fire, and keep this far from the walls.</summary>
        public const float EdgeMin = 0.75f, EdgeMax = 1.5f, EdgeMargin = 0.3f, TravellerGap = 0.13f;
        /// <summary>Box units: the chief stands this far in front of the fire in a <i>strangers</i> scene.</summary>
        public const float ChiefForward = 0.26f;

        /// <summary>The traits that sulk when not chosen (the sim's proud traits); the rest shrug and clap.</summary>
        static readonly string[] Proud = { "the stubborn", "the grump", "the loud", "the brave", "the clever", "the tinkerer", "the worrier" };

        /// <summary>The two sides of a dilemma kind, option 0 then option 1.</summary>
        public static (ChoiceSideRule a, ChoiceSideRule b) Rules(DilemmaKind kind) => kind switch
        {
            DilemmaKind.Who => (new ChoiceSideRule(ChoiceAnchor.FireOrSeat, ChoiceProp.None, ChoiceStand.OptionPerson),
                                new ChoiceSideRule(ChoiceAnchor.FireOrSeat, ChoiceProp.None, ChoiceStand.OptionPerson)),
            DilemmaKind.WhichWay => (new ChoiceSideRule(ChoiceAnchor.Building, ChoiceProp.Stake, ChoiceStand.Nearest),
                                     new ChoiceSideRule(ChoiceAnchor.Building, ChoiceProp.Stake, ChoiceStand.Nearest)),
            DilemmaKind.Tradition => (new ChoiceSideRule(ChoiceAnchor.ShrineOrGather, ChoiceProp.Offering, ChoiceStand.Nobody),
                                      new ChoiceSideRule(ChoiceAnchor.ShrineOrGather, ChoiceProp.Offering, ChoiceStand.Nobody)),
            _ => (new ChoiceSideRule(ChoiceAnchor.Fire, ChoiceProp.None, ChoiceStand.Asker),
                  new ChoiceSideRule(ChoiceAnchor.Edge, ChoiceProp.None, ChoiceStand.Travellers)),
        };

        /// <summary>Does the asker stand in the scene apart from the two sides (behind the middle)? Who: the old leader by the fire;
        /// tradition: the one who asked, at the stone. Which way: the workers speak for the sites; strangers: the chief is a side.</summary>
        public static bool AskerBehind(DilemmaKind kind) => kind == DilemmaKind.Who || kind == DilemmaKind.Tradition;

        /// <summary>The two points either side of <paramref name="c"/>: left then right as the Keeper sees them (<paramref name="r"/> is
        /// the screen's right on the ground, <paramref name="t"/> towards the viewer, both unit), <paramref name="forward"/> towards the glass.</summary>
        public static ((float x, float z) left, (float x, float z) right) Flank((float x, float z) c, (float x, float z) r, (float x, float z) t, float spread, float forward)
        {
            float fx = c.x + t.x * forward, fz = c.z + t.z * forward;
            return ((fx - r.x * spread, fz - r.z * spread), (fx + r.x * spread, fz + r.z * spread));
        }

        /// <summary>A point <paramref name="d"/> along a direction.</summary>
        public static (float x, float z) Along((float x, float z) p, (float x, float z) dir, float d) => (p.x + dir.x * d, p.z + dir.z * d);

        /// <summary>A unit vector (the ground's x, z); a zero vector stays the screen's right in the default view.</summary>
        public static (float x, float z) Unit(float x, float z)
        {
            float l = (float)Math.Sqrt(x * x + z * z);
            return l < 1e-5f ? (1f, 0f) : (x / l, z / l);
        }

        /// <summary>How far one can go from <paramref name="p"/> along <paramref name="dir"/> before the walls (half extents
        /// <paramref name="hx"/>, <paramref name="hz"/>, less the margin).</summary>
        public static float RoomAlong((float x, float z) p, (float x, float z) dir, float hx, float hz, float margin)
        {
            float best = float.MaxValue;
            float lx = hx - margin, lz = hz - margin;
            if (dir.x > 1e-5f) best = Math.Min(best, (lx - p.x) / dir.x);
            if (dir.x < -1e-5f) best = Math.Min(best, (-lx - p.x) / dir.x);
            if (dir.z > 1e-5f) best = Math.Min(best, (lz - p.z) / dir.z);
            if (dir.z < -1e-5f) best = Math.Min(best, (-lz - p.z) / dir.z);
            return Math.Max(0f, best);
        }

        /// <summary>Where the strangers wait: out from the fire towards the box's edge on the Keeper's right (the chief left of them),
        /// else on the left when the right has no room; at most <see cref="EdgeMax"/> off, so both fit one look.
        /// <paramref name="onRight"/> says which side they ended up on.</summary>
        public static (float x, float z) EdgePoint((float x, float z) fire, (float x, float z) r, float hx, float hz, out bool onRight)
        {
            float right = RoomAlong(fire, r, hx, hz, EdgeMargin), left = RoomAlong(fire, (-r.x, -r.z), hx, hz, EdgeMargin);
            onRight = right >= EdgeMin || right >= left;
            float room = onRight ? right : left;
            float d = Math.Min(EdgeMax, Math.Max(Math.Min(room, EdgeMin), room * 0.85f));
            return Along(fire, onRight ? r : (-r.x, -r.z), d);
        }

        /// <summary>The strangers' own spots around <paramref name="c"/>, a little group (<paramref name="n"/> of them), facing the glass.</summary>
        public static List<(float x, float z)> TravellerSpots((float x, float z) c, (float x, float z) r, (float x, float z) t, int n)
        {
            var list = new List<(float x, float z)>();
            for (int i = 0; i < n; i++)
            {
                float across = (i - (n - 1) * 0.5f) * TravellerGap;
                float back = (i % 2 == 1) ? -0.05f : 0.02f;
                list.Add((c.x + r.x * across + t.x * back, c.z + r.z * across + t.z * back));
            }
            return list;
        }

        /// <summary>The small gesture in a wait loop, by who stands: a candidate's hopeful little wave, a worker pointing at the site, the
        /// asker gesturing as they put it.</summary>
        public static ActivityKind Fidget(ChoiceStand stand, bool asker) => asker ? ActivityKind.Speech
            : stand == ChoiceStand.Nearest ? ActivityKind.Point : ActivityKind.WaveWary;

        /// <summary>
        /// A wait as beats (real seconds from the start): first coming over (<paramref name="comeSeconds"/>, looking up on arrival), then a
        /// loop of looking up at the glass and a small gesture, offset by <paramref name="salt"/> so two people never move as one. The loop
        /// never holds one pose for the whole wait (an idle loop, not frozen) and the beats cover the wait with no gap.
        /// </summary>
        public static List<(float from, float to, ActivityKind kind)> WaitBeats(float seconds, float comeSeconds, ActivityKind fidget, int salt)
        {
            var list = new List<(float, float, ActivityKind)>();
            if (seconds <= 0f) return list;
            float t = Math.Min(seconds, Math.Max(0f, comeSeconds));
            if (t > 0f) list.Add((0f, t, ActivityKind.LookUp));
            float look = LookSeconds + (Math.Abs(salt) % 5) * 0.35f;
            bool first = true;
            while (t < seconds - 1e-3f)
            {
                float l = first ? look * (0.4f + (Math.Abs(salt) % 3) * 0.2f) : look;
                first = false;
                float e = Math.Min(seconds, t + l);
                if (list.Count > 0 && list[list.Count - 1].Item3 == ActivityKind.LookUp && Math.Abs(list[list.Count - 1].Item2 - t) < 1e-4f)
                    list[list.Count - 1] = (list[list.Count - 1].Item1, e, ActivityKind.LookUp); // one look, not two
                else list.Add((t, e, ActivityKind.LookUp));
                t = e;
                if (t >= seconds - 1e-3f) break;
                e = Math.Min(seconds, t + FidgetSeconds);
                list.Add((t, e, fidget));
                t = e;
            }
            return list;
        }

        /// <summary>Does this trait sulk when not chosen (else it shrugs and claps)?</summary>
        public static bool Sulks(string trait) => trait != null && Array.IndexOf(Proud, trait) >= 0;

        /// <summary>
        /// The reaction as beats (shares of <see cref="ReactSeconds"/>) for one who stands in the scene: the chosen side cheers the whole
        /// time; the other side, by trait, sulks (a look, then sitting down with their back to it) or shrugs (a small wave, then claps);
        /// the asker claps.
        /// </summary>
        public static List<(float from, float to, ActivityKind kind)> ReactBeats(bool chosen, bool asker, string trait)
        {
            if (chosen) return new List<(float, float, ActivityKind)> { (0f, 1f, ActivityKind.Cheer) };
            if (asker) return new List<(float, float, ActivityKind)> { (0f, 0.2f, ActivityKind.LookUp), (0.2f, 1f, ActivityKind.Clap) };
            if (Sulks(trait)) return new List<(float, float, ActivityKind)> { (0f, 0.25f, ActivityKind.LookUp), (0.25f, 1f, ActivityKind.SitOut) };
            return new List<(float, float, ActivityKind)> { (0f, 0.35f, ActivityKind.WaveWary), (0.35f, 1f, ActivityKind.Clap) };
        }

        /// <summary>The gatherers' beats (relic-moment data): grown-ups and kids near enough come over to the chosen side and cheer.</summary>
        public static RelicMomentDef GatherDef(int maxGrown = 3, int maxKids = 2)
        {
            var d = new RelicMomentDef { relicId = null, seconds = ReactSeconds, maxGrown = maxGrown, maxKids = maxKids, maxQueue = 3, standIns = false };
            d.beats.Add(new RelicBeat { id = "come over", cast = RelicCast.Grown, from = 0f, to = GatherShare, kind = ActivityKind.Point, where = RelicWhere.Spot });
            d.beats.Add(new RelicBeat { id = "cheer", cast = RelicCast.Grown, from = GatherShare, to = 1f, kind = ActivityKind.Cheer, where = RelicWhere.Spot });
            d.beats.Add(new RelicBeat { id = "come over", cast = RelicCast.Kids, from = 0f, to = GatherShare, kind = ActivityKind.RunOver, where = RelicWhere.Spot });
            d.beats.Add(new RelicBeat { id = "jump", cast = RelicCast.Kids, from = GatherShare, to = 1f, kind = ActivityKind.JumpAndPoint, where = RelicWhere.Spot });
            // The strangers sent on: those near the fire wave them off from where they stand.
            d.beats.Add(new RelicBeat { id = "wave off", cast = RelicCast.Onlookers, from = 0.15f, to = 1f, kind = ActivityKind.Wave, where = RelicWhere.Here, facesGlass = false });
            return d;
        }
    }

    /// <summary>One side of a staged choice scene (#224): the option, its points and who stands for it. Box units (the land's x, z).</summary>
    public sealed class ChoiceSide
    {
        public int option;
        public ChoiceSideRule rule;
        /// <summary>The place the side's people are staged at (their walk ends there; the view stands them on their own points).</summary>
        public Place place;
        /// <summary>The side's own point: where its prop stands, or the middle of its people.</summary>
        public float x, z;
        /// <summary>Who stands for it and where each one stands (the view's pin).</summary>
        public readonly List<(PersonRef who, float x, float z)> people = new List<(PersonRef, float, float)>();
        /// <summary>The strangers' spots (<see cref="ChoiceStand.Travellers"/>): drawn by the view, not people of the box yet.</summary>
        public readonly List<(float x, float z)> travellers = new List<(float, float)>();
        public ChoiceProp Prop => rule.prop;
    }

    /// <summary>A choice scene as staged (#224): the dilemma, its two sides (option 0 then 1), the asker behind when there is one, and
    /// when it runs (view hours of <see cref="day"/>).</summary>
    public sealed class ChoiceStage
    {
        public int dilemmaId;
        public DilemmaKind kind;
        public string defId = "";
        public readonly ChoiceSide[] sides = { new ChoiceSide { option = 0 }, new ChoiceSide { option = 1 } };
        public PersonRef asker;
        public float askerX, askerZ;
        public long day;
        public float start, end;
        /// <summary>The screen's right and the way to the viewer on the ground when it was staged (unit vectors).</summary>
        public float rx = 1f, rz, tx, tz = -1f;
        /// <summary>The answer it is reacting to (-1 while it waits).</summary>
        public int chosen = -1;

        /// <summary>Everyone standing in it, with their point (the sides' people, then the asker).</summary>
        public IEnumerable<(PersonRef who, float x, float z)> Pins()
        {
            foreach (var s in sides) foreach (var p in s.people) yield return p;
            if (!asker.IsNone) yield return (asker, askerX, askerZ);
        }
    }
}
