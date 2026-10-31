using System;
using System.Collections.Generic;
using System.Linq;
using AquariumCiv.Life;
using AquariumCiv.Sim;

namespace AquariumCiv.View.ShowMe
{
    /// <summary>
    /// "Show me" for a chronicle card or entry (#76; chronicle-and-history §2.5): where the camera goes and how it frames it
    /// (<see cref="Target"/>), the echo the people play (<see cref="Echo"/>), and both with the echo's line
    /// (<see cref="Plan"/>). Pure and dice-free: it reads the sim and never writes it, so the economy and the ids never move.
    /// <para>The view (phase 2) and the card stack (#75) call it like this:</para>
    /// <code>
    /// var plan = showMe.Plan(card);                       // or Plan(entry)
    /// camera.FlyTo(plan.target ...);                      // FlyToPerson(followId) first, the point if no doll
    /// // when the flight lands, after plan.echoDelay:
    /// if (plan.HasEcho) planner.StageEcho(plan.echo, now); // bubbles: planner.EchoLinesAt
    /// // when the stack closes: planner.EndEchoes(); camera back to the default view
    /// </code>
    /// </summary>
    public sealed class ShowMeCore
    {
        /// <summary>Fly-to: at most 1.2 s, eased (§2.5); short hops take less.</summary>
        public const float MaxFlySeconds = 1.2f, MinFlySeconds = 0.5f;
        /// <summary>End zooms (BoxCamera: &lt; 1 closer). A person close, a place a little wider, a shaping wider, the box whole.</summary>
        public const float PersonZoom = 0.45f, PlaceZoom = 0.6f, ShapingZoom = 0.7f, YearZoom = 0.8f, BoxZoom = 1f;
        /// <summary>The point sits at 60 % of the screen height: the card covers the bottom ~40 %, the speech bubble sits at
        /// ~70 % (MomentPlayer), so the people stand between them.</summary>
        public const float AnchorAboveCard = 0.6f;
        /// <summary>A beat between landing and the echo (the people notice).</summary>
        public const float EchoDelay = 0.25f;

        readonly SimCore sim;
        readonly IPlacePositions positions;

        /// <summary><paramref name="positions"/>: the planner's (LifePlanner.Positions) so the camera and the dolls agree;
        /// default the settlement's.</summary>
        public ShowMeCore(SimCore sim, IPlacePositions positions = null)
        {
            this.sim = sim ?? throw new ArgumentNullException(nameof(sim));
            this.positions = positions ?? new SettlePlacePositions(sim);
        }

        /// <summary>The fly time for a hop of <paramref name="distance"/> world units (≤ <see cref="MaxFlySeconds"/>).</summary>
        public static float FlySeconds(float distance) => Math.Max(MinFlySeconds, Math.Min(MaxFlySeconds, 0.5f + 0.15f * Math.Max(0f, distance)));

        // ------------------------------------------------------------------ the plan

        /// <summary>"Show me" on a card: its lead entry, with the card's building when the lead has none.</summary>
        public ShowMePlan Plan(ChronicleCard card)
        {
            if (card?.lead == null) return Plan((ChronicleEntry)null);
            var plan = Plan(card.lead);
            if (plan.target.subject != ShowMeSubject.Building && card.buildingId >= 0 && card.buildingId != card.lead.buildingId
                && TryBuilding(card.buildingId, out float bx, out float bz) && plan.target.subject != ShowMeSubject.Relic
                && plan.target.subject != ShowMeSubject.Remembering && plan.target.subject != ShowMeSubject.Person)
            {
                var t = plan.target;
                t.subject = ShowMeSubject.Building; t.fallback = ShowMeFallback.None; t.buildingId = card.buildingId;
                t.hasPoint = true; t.x = bx; t.z = bz; t.zoom = PlaceZoom; t.place = new Place(PlaceKind.Home, card.buildingId);
            }
            foreach (int id in card.people)
                if (Alive(id) && !plan.target.people.Contains(id)) plan.target.people.Add(id);
            return plan;
        }

        /// <summary>"Show me" on an entry: the target, the echo, and the echo's line in the people's voice.</summary>
        public ShowMePlan Plan(ChronicleEntry e)
        {
            var plan = new ShowMePlan { entry = e, target = Target(e), echo = Echo(e) };
            return plan;
        }

        /// <summary>The echo (§2.5's table), with its line: the entry's quote, else the lead's own words
        /// (<see cref="PeopleVoice.StoryLine"/>) when the bank allows.</summary>
        public EchoScene Echo(ChronicleEntry e)
        {
            var echo = EchoScenes.For(sim, e);
            if (echo.Plays && string.IsNullOrEmpty(echo.line))
            {
                var said = PeopleVoice.StoryLine(sim, e);
                if (said != null && Alive(said.speakerId) && echo.def.beats.Count > 0 && (said.speakerId == echo.leadId || said.speakerId == echo.otherId))
                { echo.line = said.text; echo.lineSpeakerId = said.speakerId; }
            }
            return echo;
        }

        // ------------------------------------------------------------------ the target

        /// <summary>
        /// Where "Show me" flies for <paramref name="e"/> (§2.5): the entry's building, else the person's current spot (the view
        /// follows the doll), else where it happened. A death goes to the remembering stone; a relic to the relic; a Keeper's
        /// gift with no building to the shaping; a story whose person is gone to where it happened; a story with no place to
        /// the year's spot; the weather and an era to the whole box.
        /// </summary>
        public ShowMeTarget Target(ChronicleEntry e)
        {
            var t = new ShowMeTarget { entry = e };
            if (e == null || string.IsNullOrEmpty(e.kind)) return t;
            string group = SimCore.GroupOf(e.kind);
            AddPeople(t, e);

            // A death: the remembering stone and a flower (no echo).
            if (e.kind == "death" || e.kind == "role_death")
            {
                if (Remembering(t)) return t;
                if (Stored(e, t, ShowMeSubject.Spot)) return t;
                return YearOrBox(t);
            }
            // The weather and the box's big days: where it was recorded, else the whole box.
            if (group == "world" || group == "era" || (group == "milestone" && e.buildingId < 0))
            {
                if (Stored(e, t, ShowMeSubject.Spot)) { t.zoom = YearZoom; return t; }
                return Box(t);
            }
            // A relic: the relic in the world.
            if (!string.IsNullOrEmpty(e.relicId) && (e.kind == "relic" || e.kind == "lost" || e.kind == "found"))
            {
                var r = sim.FindRelic(e.relicId);
                if (r != null && r.Found && r.takenMonth < 0)
                {
                    t.subject = ShowMeSubject.Relic; t.relicId = e.relicId; t.hasPoint = true; t.x = r.x; t.z = r.z;
                    t.zoom = PlaceZoom; t.place = LifePlanner.RelicPlace(e.relicId);
                    return t;
                }
                if (e.kind == "relic") t.fallback = ShowMeFallback.RelicGone;
            }
            // The entry's building.
            if (e.buildingId >= 0)
            {
                if (TryBuilding(e.buildingId, out float bx, out float bz))
                {
                    t.subject = ShowMeSubject.Building; t.buildingId = e.buildingId; t.hasPoint = true; t.x = bx; t.z = bz;
                    t.zoom = PlaceZoom; t.place = new Place(PlaceKind.Home, e.buildingId);
                    t.followId = -1;
                    return t;
                }
                if (t.fallback == ShowMeFallback.None) t.fallback = ShowMeFallback.BuildingGone;
            }
            // A Keeper's gift with no building: the shaping.
            if (e.kind == "keeper" && Shaping(e, t)) return t;
            // The person, wherever they are now.
            int lead = LeadOf(e);
            if (lead >= 0 && Alive(lead))
            {
                t.subject = ShowMeSubject.Person; t.followId = lead; t.zoom = PersonZoom;
                if (!Home(lead, t) && !Stored(e, t, ShowMeSubject.Person)) YearPoint(t);
                t.subject = ShowMeSubject.Person;
                return t;
            }
            if (lead >= 0 && t.fallback == ShowMeFallback.None) t.fallback = ShowMeFallback.PersonDied;
            // Where it happened.
            if (Stored(e, t, ShowMeSubject.Spot)) return t;
            if (t.fallback == ShowMeFallback.BuildingGone && Stood(e.buildingId, t)) return t;
            if (t.fallback == ShowMeFallback.PersonDied && Remembering(t)) { t.fallback = ShowMeFallback.PersonDied; return t; }
            if (t.fallback == ShowMeFallback.None) t.fallback = ShowMeFallback.NoPlace;
            return YearOrBox(t);
        }

        /// <summary>The story's lead: the parent for a birth (the baby is in their arms), the master for an apprenticeship,
        /// the relic's holder for a relic, else the entry's person.</summary>
        int LeadOf(ChronicleEntry e)
        {
            if (e.kind == "birth")
            {
                var child = sim.FindNotable(e.notableId);
                if (child != null && Alive(child.parentId)) return child.parentId;
            }
            if (e.kind == "apprentice" && Alive(e.otherNotableId)) return e.otherNotableId;
            if (e.notableId >= 0) return e.notableId;
            return e.otherNotableId;
        }

        void AddPeople(ShowMeTarget t, ChronicleEntry e)
        {
            int lead = LeadOf(e);
            foreach (int id in new[] { lead, e.notableId, e.otherNotableId })
                if (Alive(id) && !t.people.Contains(id)) t.people.Add(id);
        }

        bool Alive(int id)
        {
            if (id < 0) return false;
            var n = sim.FindNotable(id);
            return n != null && n.alive;
        }

        bool TryBuilding(int id, out float x, out float z)
        {
            x = z = 0f;
            // Standing: in the box on a lot that isn't retired (a building that moved lots keeps its id).
            var b = sim.State.buildings.FirstOrDefault(q => q.id == id);
            if (b == null) return false;
            var lot = b.lotId > 0 ? sim.State.settle?.lots?.Find(l => l.id == b.lotId) : null;
            if (lot != null && lot.retired) return false;
            if (positions.TryGetPosition(new Place(PlaceKind.Home, id), out x, out z)) return true;
            if (b.lotId > 0) { x = b.x; z = b.z; return true; }
            return false;
        }

        /// <summary>Where a building that's gone stood: its last retired lot.</summary>
        bool Stood(int id, ShowMeTarget t)
        {
            var lot = id < 0 ? null : sim.State.settle?.lots?.LastOrDefault(l => l.buildingId == id && l.retired);
            if (lot == null) return false;
            t.subject = ShowMeSubject.Spot; t.hasPoint = true; t.x = lot.x; t.z = lot.z; t.zoom = PlaceZoom;
            return true;
        }

        bool Stored(ChronicleEntry e, ShowMeTarget t, ShowMeSubject subject)
        {
            var h = sim.State.history;
            var p = h?.positions?.FirstOrDefault(q => q.id == e.id);
            if (p == null) return false;
            t.subject = subject; t.hasPoint = true; t.x = p.px / 100f; t.z = p.pz / 100f;
            if (subject != ShowMeSubject.Person) t.zoom = PlaceZoom;
            return true;
        }

        bool Home(int notableId, ShowMeTarget t)
        {
            var n = sim.FindNotable(notableId);
            var lot = n != null ? sim.Settlement?.HomeOf(n) : null;
            if (lot == null) return false;
            t.hasPoint = true; t.x = lot.x; t.z = lot.z;
            return true;
        }

        bool Shaping(ChronicleEntry e, ShowMeTarget t)
        {
            var n = EchoScenes.NudgeOf(sim, e);
            if (n != null)
            {
                t.subject = ShowMeSubject.Shaping; t.nudgeId = n.id; t.hasPoint = true; t.x = n.x; t.z = n.z; t.zoom = ShapingZoom;
                t.place = Place.Land(PlaceKind.Nudge, n.id);
                return true;
            }
            // Taken back since: where it was (the time-lapse's record).
            var rec = sim.State.history?.shapes?.Where(s => s.nudgeId != 0 && s.month <= e.month && (s.endMonth < 0 || s.endMonth >= e.month))
                .OrderByDescending(s => s.month).FirstOrDefault();
            if (rec == null) return false;
            t.subject = ShowMeSubject.Shaping; t.nudgeId = rec.nudgeId; t.hasPoint = true; t.x = rec.x; t.z = rec.z; t.zoom = ShapingZoom;
            return true;
        }

        bool Remembering(ShowMeTarget t)
        {
            bool church = sim.State.buildings.Any(b => b.type == BuildingType.Church);
            var place = Place.Land(PlaceKind.Remembering, church ? 1 : 0);
            if (positions.TryGetPosition(place, out float x, out float z))
            {
                t.subject = ShowMeSubject.Remembering; t.hasPoint = true; t.x = x; t.z = z; t.zoom = PlaceZoom; t.place = place;
                return true;
            }
            var stone = sim.State.history?.shapes?.LastOrDefault(s => s.kind == "stone");
            if (stone == null) return false;
            t.subject = ShowMeSubject.Remembering; t.hasPoint = true; t.x = stone.x; t.z = stone.z; t.zoom = PlaceZoom;
            return true;
        }

        ShowMeTarget YearOrBox(ShowMeTarget t)
        {
            if (YearPoint(t)) { t.subject = ShowMeSubject.YearSpot; return t; }
            return Box(t);
        }

        /// <summary>The year's spot: the most salient story of the entry's year that has a stored place, else the gathering
        /// place (the fire), else nothing.</summary>
        bool YearPoint(ShowMeTarget t)
        {
            var e = t.entry;
            long y0 = e.month / SimConfig.MonthsPerYear * SimConfig.MonthsPerYear, y1 = y0 + SimConfig.MonthsPerYear;
            var stored = sim.State.history?.positions;
            if (stored != null && stored.Count > 0)
            {
                var ids = new Dictionary<int, EntryPos>();
                foreach (var p in stored) ids[p.id] = p;
                ChronicleEntry best = null;
                foreach (var c in sim.State.chronicle)
                {
                    if (c.month < y0 || c.month >= y1 || c.id == e.id || !ids.ContainsKey(c.id)) continue;
                    if (best == null || c.salience > best.salience || (c.salience == best.salience && c.id < best.id)) best = c;
                }
                if (best != null)
                {
                    var p = ids[best.id];
                    t.hasPoint = true; t.x = p.px / 100f; t.z = p.pz / 100f; t.zoom = YearZoom;
                    return true;
                }
            }
            if (positions.TryGetPosition(Place.Land(PlaceKind.Fire), out float fx, out float fz))
            {
                t.hasPoint = true; t.x = fx; t.z = fz; t.zoom = YearZoom; t.place = Place.Land(PlaceKind.Fire);
                return true;
            }
            return false;
        }

        static ShowMeTarget Box(ShowMeTarget t)
        {
            t.subject = ShowMeSubject.Box; t.hasPoint = true; t.x = 0f; t.z = 0f; t.zoom = BoxZoom; t.followId = -1;
            return t;
        }
    }
}
