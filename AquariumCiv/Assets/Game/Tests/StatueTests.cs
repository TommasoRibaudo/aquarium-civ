using System.Collections.Generic;
using System.Linq;
using AquariumCiv.Sim;
using AquariumCiv.Sim.Settle;
using NUnit.Framework;
using UnityEngine;
using Terrain = AquariumCiv.Sim.Settle.Terrain;
using static AquariumCiv.Tests.SimCoreTests;

namespace AquariumCiv.Tests
{
    /// <summary>
    /// #776 (animation-specs B81.5, approved 2026-11-26): the founder's statue, the pure tests. The rule (<see cref="StatueRules.Who"/> and
    /// <see cref="StatueRules.Ready"/>: the most pinned over the founder, a year after the death, a village of 12 people, never for the living,
    /// spring or summer), the spot finder (never on a path, the 0.2 u clear ring, the same spot every time, the 1.4 u fallback), and the sim's side
    /// (the line and the Story moment, once a box, the save).
    /// </summary>
    public class StatueTests
    {
        static Notable N(int id, int parent = -1, bool alive = true, long died = -1) =>
            new Notable { id = id, name = "N" + id, parentId = parent, alive = alive, diedMonth = died };

        const float Legend = 24f;

        // ---------- who ----------

        [Test]
        public void Who_TheMostPinned_BeatsTheFounder_AndTheLowerIdWinsATie()
        {
            var list = new List<Notable> { N(1), N(2), N(3), N(4) };
            var hours = new Dictionary<int, float> { { 2, 30f }, { 3, 80f }, { 4, 80f } };
            float H(int id) => hours.TryGetValue(id, out float h) ? h : 0f;
            Assert.AreEqual(3, StatueRules.Who(list, H, Legend).id, "the most pinned hours, the lower id on a tie");
            hours[4] = 90f;
            Assert.AreEqual(4, StatueRules.Who(list, H, Legend).id);
        }

        [Test]
        public void Who_NobodyPinnedADay_IsTheFounder_TheFirstOfTheFirstGeneration()
        {
            // 5 is a child of 3; 3 and 7 are the first generation: the founder is 3, never a child with a lower id than... (ids rise with births)
            var list = new List<Notable> { N(7), N(5, parent: 3), N(3), N(9) };
            float H(int id) => id == 7 ? 23.9f : 0f; // pinned, but not a day
            Assert.AreEqual(3, StatueRules.Who(list, H, Legend).id);
            Assert.AreEqual(3, StatueRules.Founder(list).id);
            Assert.AreEqual(7, StatueRules.Who(list, id => id == 7 ? 24f : 0f, Legend).id, "exactly a day is the legend rule");
            Assert.IsNull(StatueRules.Who(new List<Notable>(), H, Legend), "an empty box has nobody");
            Assert.AreEqual(2, StatueRules.Founder(new List<Notable> { N(4, 1), N(2, 1) }).id, "with no first generation left, the lowest id");
        }

        [Test]
        public void Who_CountsTheLivingAndTheDead_ReadyAsksWhetherTheyHaveDied()
        {
            var alive = N(1); var dead = N(2, alive: false, died: 10);
            Assert.AreSame(alive, StatueRules.Who(new List<Notable> { alive, dead }, id => 0f, Legend));
            Assert.AreSame(dead, StatueRules.Who(new List<Notable> { alive, dead }, id => id == 2 ? 50f : 0f, Legend));
        }

        // ---------- when ----------

        [Test]
        public void Ready_OnlyAYearAfterTheDeath_InAGrownVillage_InSpringOrSummer_NeverForTheLiving()
        {
            var dead = N(1, alive: false, died: 100);
            Assert.IsFalse(StatueRules.Ready(dead, 100 + 11, Era.Village, 12, 5, false), "11 months after: too soon");
            Assert.IsTrue(StatueRules.Ready(dead, 100 + 12, Era.Village, 12, 5, false), "a year after");
            Assert.IsTrue(StatueRules.Ready(dead, 100 + 90, Era.Town, 40, 2, false), "a town, the first of spring");
            Assert.IsFalse(StatueRules.Ready(dead, 112, Era.Camp, 30, 5, false), "a camp has no green");
            Assert.IsFalse(StatueRules.Ready(dead, 112, Era.Village, 11, 5, false), "11 people is not a grown village");
            Assert.IsTrue(StatueRules.Ready(dead, 112, Era.Village, 12, 5, false), "12 people is");
            foreach (int winterOrAutumn in new[] { 0, 1, 8, 9, 10, 11 })
                Assert.IsFalse(StatueRules.Ready(dead, 112, Era.Village, 12, winterOrAutumn, false), "calendar month " + winterOrAutumn);
            foreach (int ok in new[] { 2, 3, 4, 5, 6, 7 })
                Assert.IsTrue(StatueRules.Ready(dead, 112, Era.Village, 12, ok, false), "calendar month " + ok);
            Assert.IsFalse(StatueRules.Ready(dead, 112, Era.Village, 12, 5, true), "once a box");
            Assert.IsFalse(StatueRules.Ready(N(1), 500, Era.Town, 40, 5, false), "never for the living");
            Assert.IsFalse(StatueRules.Ready(null, 500, Era.Town, 40, 5, false));
            Assert.IsFalse(StatueRules.Ready(N(1, alive: false, died: -1), 500, Era.Town, 40, 5, false), "dead with no month of death is a malformed person");
        }

        [Test]
        public void Tell_TheLeadersCrook_TheJobsTool_TheKindHand_ElseNothing()
        {
            Assert.AreEqual(StatueRules.Tell.Crook, StatueRules.TellOf(JobType.Fisher, "the kind", true), "a leader first");
            Assert.AreEqual(StatueRules.Tell.Axe, StatueRules.TellOf(JobType.Woodcutter, "the loud", false));
            Assert.AreEqual(StatueRules.Tell.Axe, StatueRules.TellOf(JobType.Toolmaker, "", false));
            Assert.AreEqual(StatueRules.Tell.Hammer, StatueRules.TellOf(JobType.Mason, "", false));
            Assert.AreEqual(StatueRules.Tell.Rod, StatueRules.TellOf(JobType.Fisher, "", false));
            Assert.AreEqual(StatueRules.Tell.Basket, StatueRules.TellOf(JobType.Gatherer, "", false));
            Assert.AreEqual(StatueRules.Tell.Hoe, StatueRules.TellOf(JobType.Farmer, "", false));
            Assert.AreEqual(StatueRules.Tell.Hand, StatueRules.TellOf(JobType.Elder, "the kind", false), "no tool: the kind hand");
            Assert.AreEqual(StatueRules.Tell.Hand, StatueRules.TellOf("", "the kind", false));
            Assert.AreEqual(StatueRules.Tell.Nothing, StatueRules.TellOf(JobType.Elder, "the loud", false));
            Assert.AreEqual(StatueRules.Tell.Nothing, StatueRules.TellOf(null, null, false));
        }

        // ---------- where ----------

        static SiteMap Map()
        {
            var st = new SettleState { terrain = 0 };
            return new SiteMap(st, true, eco: true);
        }

        static void PathAcross(SiteMap m, int j, int i0, int i1) { for (int i = i0; i <= i1; i++) m.tier[SiteMap.Index(i, j)] = PathNet.PathTier; }

        [Test]
        public void Spot_IsOnTheRing_TowardTheFrontGlass_AndTheSameEveryTime()
        {
            var m = Map();
            Assert.IsTrue(StatueSpot.Find(m, null, out float x, out float z), "an open green has a spot");
            float d = Terrain.Dist(x, z, m.greenX, m.greenZ);
            Assert.GreaterOrEqual(d, StatueSpot.RingNear - 0.06f, "in the ring");
            Assert.LessOrEqual(d, StatueSpot.RingFar + 0.06f);
            float ux = (x - m.greenX) / d, uz = (z - m.greenZ) / d;
            Assert.Greater(ux * StatueSpot.FrontX + uz * StatueSpot.FrontZ, 0.9f, "the side the default camera looks in from");
            Assert.IsTrue(StatueSpot.Find(Map(), null, out float x2, out float z2));
            Assert.AreEqual(x, x2); Assert.AreEqual(z, z2); // a reload: the same settlement, the same spot
        }

        [Test]
        public void Spot_NeverOnAPath_AndKeepsTheClearRingOffOne()
        {
            var m = Map();
            Assert.IsTrue(StatueSpot.Find(m, null, out float x0, out float z0));
            // a path laid through the first choice: the statue moves, and no path cell is in its footprint or its 0.2 u ring
            int cj = SiteMap.J(SiteMap.CellAt(x0, z0));
            PathAcross(m, cj, 2, SiteMap.NX - 3);
            Assert.IsTrue(StatueSpot.Find(m, null, out float x, out float z), "another spot on the ring");
            float reach = StatueSpot.Footprint / 2f + StatueSpot.Clear;
            for (int c = 0; c < SiteMap.N; c++)
            {
                if (m.tier[c] < PathNet.PathTier) continue;
                float dx = SiteMap.CXc(c) - x, dz = SiteMap.CZc(c) - z;
                Assert.Greater(System.Math.Max(System.Math.Abs(dx), System.Math.Abs(dz)), reach - 0.001f, $"path cell {c} is within the statue's clear ring");
            }
            Assert.IsFalse(x == x0 && z == z0, "it moved off the path");
            // a faint trail is still grass underfoot: it does not move the statue
            var t = Map();
            t.tier[SiteMap.CellAt(x0, z0)] = PathNet.Trail;
            StatueSpot.Find(t, null, out float tx, out float tz);
            Assert.AreEqual(x0, tx); Assert.AreEqual(z0, tz);
        }

        [Test]
        public void Spot_KeepsOffTheWellTheFireADoorAndTheNameStone()
        {
            var m = Map();
            StatueSpot.Find(m, null, out float x0, out float z0);
            var keep = new List<(float x, float z)> { (x0 + 0.1f, z0) }; // a well 0.1 u from the first choice
            Assert.IsTrue(StatueSpot.Find(m, keep, out float x, out float z));
            Assert.GreaterOrEqual(Terrain.Dist(x, z, keep[0].x, keep[0].z), StatueSpot.KeepFrom - 1e-3f, "0.25 u from it");
            Assert.IsFalse(StatueSpot.Fits(m, x0, z0, keep), "the first choice no longer fits");
            // water, a lot and a yard are not open ground
            var w = Map();
            StatueSpot.Find(w, null, out float wx, out float wz);
            w.pond[SiteMap.CellAt(wx + 0.2f, wz)] = true;
            Assert.IsFalse(StatueSpot.Fits(w, wx, wz, null), "water within the clear ring");
            var l = Map();
            l.lot[SiteMap.CellAt(x0, z0 - 0.2f)] = 5;
            Assert.IsFalse(StatueSpot.Fits(l, x0, z0, null), "a lot within the clear ring");
        }

        [Test]
        public void Spot_WithNoRoomOnTheRing_GoesToTheNearestOpenGroundWithin14_ElseNowhere()
        {
            var m = Map();
            // houses all round: every cell within 1.15 u of the centre is a lot
            for (int c = 0; c < SiteMap.N; c++)
            {
                float d = Terrain.Dist(SiteMap.CXc(c), SiteMap.CZc(c), m.greenX, m.greenZ);
                if (d < 1.15f) m.lot[c] = 9;
            }
            bool found = StatueSpot.Find(m, null, out float x, out float z);
            Assert.IsTrue(found, "open ground beyond the houses, within 1.4 u");
            float dist = Terrain.Dist(x, z, m.greenX, m.greenZ);
            Assert.LessOrEqual(dist, StatueSpot.FallbackReach + 1e-3f);
            Assert.Greater(dist, 1.15f, "past the houses");
            for (int c = 0; c < SiteMap.N; c++) if (Terrain.Dist(SiteMap.CXc(c), SiteMap.CZc(c), m.greenX, m.greenZ) < 1.4f) m.lot[c] = 9;
            Assert.IsFalse(StatueSpot.Find(m, null, out _, out _), "no open ground at all: no statue");
            Assert.IsFalse(StatueSpot.Find(null, null, out _, out _));
        }

        [Test]
        public void Face_TowardTheGreensCentre_ElseTurnedTo45DegreesSoItsFaceShows()
        {
            float front = (float)(System.Math.Atan2(StatueSpot.FrontX, StatueSpot.FrontZ) * 180.0 / System.Math.PI);
            // in front of the green looking back at the centre: it faces the centre, which is away from the camera... so turned to 45 deg
            float yawFront = StatueSpot.FaceYaw(0.5f * StatueSpot.FrontX, 0.5f * StatueSpot.FrontZ, 0f, 0f);
            float away = System.Math.Abs(yawFront - front); if (away > 180f) away = 360f - away;
            Assert.AreEqual(45f, away, 0.01f, "the centre is away from the camera: 45 deg off the front");
            // behind the green the centre is toward the camera: it faces it straight
            float yawBack = StatueSpot.FaceYaw(-0.5f * StatueSpot.FrontX, -0.5f * StatueSpot.FrontZ, 0f, 0f);
            Assert.AreEqual(front, yawBack, 0.01f);
        }

        // ---------- the sim ----------

        static SimCore Village(int seed)
        {
            var sim = NewSim(seed, new SimConfig { dilemmas = true, moments = true, ecology = true, historyPruneYears = 0 });
            for (int m = 0; m < 12; m++) sim.Advance(1.0 / 12);
            sim.ForceNextEra(); sim.ForceNextEra();
            sim.DebugAddKids(10, 30f); // 12 or more people for sure
            return sim;
        }

        static void Months(SimCore sim, int n) { for (int i = 0; i < n; i++) sim.Advance(1.0 / 12); }

        /// <summary>A spring or summer month in the calendar the sim tests use (shift 0: month 5 of the year).</summary>
        static void ToCalendarMonth(SimCore sim, int cal) { int guard = 0; while (sim.CalendarMonth != cal && guard++ < 24) Months(sim, 1); }

        static ChronicleEntry[] Lines(SimCore sim) => sim.State.chronicle.Where(e => e.kind == StoryEvent.Kind(StoryEvent.FounderStatue)).ToArray();

        [Test]
        public void TheStatue_IsRaisedAYearAfterTheFoundersDeath_ForTheFounder_WithTheLineAndAStoryMoment_OnceABox()
        {
            var sim = Village(81);
            Assert.GreaterOrEqual(sim.State.population, StatueRules.MinPeople);
            sim.Config.pinLegendHours = 1e6f; // nobody is pinned a day: the founder
            var founder = StatueRules.Founder(sim.State.notables);
            Assert.AreSame(founder, sim.StatueCandidate);
            Months(sim, 24);
            Assert.IsEmpty(Lines(sim), "nothing for the living");
            Assert.IsFalse(sim.StatueStands);
            founder.alive = false; founder.diedMonth = sim.State.month;
            ToCalendarMonth(sim, 5);
            Assert.Less(sim.State.month - founder.diedMonth, 12 + 12, "set up within a couple of years");
            while (sim.State.month - founder.diedMonth < 11) Months(sim, 1);
            Assert.IsEmpty(Lines(sim), "not within the year of the death (the quiet visit comes first)");
            int guard = 0;
            while (!Lines(sim).Any() && guard++ < 36) Months(sim, 1);
            var line = Lines(sim).Single();
            Assert.GreaterOrEqual(line.month - founder.diedMonth, 12, "a year after");
            Assert.IsTrue(StatueRules.SpringOrSummer(SeasonCalendar.OfMonth(line.month, sim.State.seasonShift)), "in spring or summer");
            Assert.AreEqual($"{founder.name} is made of stone now, on the green.", line.text);
            Assert.AreEqual(founder.id, line.notableId);
            Assert.AreEqual(founder.id, sim.State.statueFor);
            Assert.AreEqual(line.month, sim.State.statueMonth);
            Assert.IsTrue(sim.StatueStands);
            Assert.AreSame(founder, sim.StatueNotable);
            var m = sim.MomentOfEntry(line.id);
            Assert.IsNotNull(m, "a held moment queues");
            Assert.AreEqual(MomentKind.Story, m.kind);
            Assert.AreEqual(StoryEvent.FounderStatue, sim.StoryOfEntry(line.id), "the view can tell which event it is");
            // once a box: the pinned person changing later, and dying, raises nothing more
            var other = sim.State.notables.Where(n => n.alive && n.id != founder.id).OrderBy(n => n.id).First();
            sim.Config.pinLegendHours = 0f; sim.State.pinnedNotableId = other.id;
            Months(sim, 3);
            other.alive = false; other.diedMonth = sim.State.month;
            Months(sim, 40);
            Assert.AreEqual(1, Lines(sim).Length, "told once");
            Assert.AreEqual(founder.id, sim.State.statueFor, "the first statue stands");
        }

        [Test]
        public void TheStatue_IsForTheMostPinned_NotTheFounder_AndWaitsUntilTheyDie()
        {
            var sim = Village(82);
            sim.Config.pinLegendHours = 0.5f;
            var founder = StatueRules.Founder(sim.State.notables);
            var pinned = sim.State.notables.Where(n => n.alive && n.id != founder.id).OrderBy(n => n.id).First();
            sim.State.pinnedNotableId = pinned.id;
            Months(sim, 12); // 12 months x 1/12 h = 1 h pinned at the default pace: over the legend of 0.5 h
            Assert.GreaterOrEqual(sim.PinnedHours(pinned.id), 0.5f);
            Assert.AreSame(pinned, sim.StatueCandidate);
            founder.alive = false; founder.diedMonth = sim.State.month - 30; // the founder died long ago
            Months(sim, 40);
            Assert.IsEmpty(Lines(sim), "the pinned person lives: no statue of the living, and none of the founder either");
            pinned.alive = false; pinned.diedMonth = sim.State.month;
            Months(sim, 11);
            Assert.IsEmpty(Lines(sim), "a year after their death, not before");
            int guard = 0;
            while (!Lines(sim).Any() && guard++ < 36) Months(sim, 1);
            Assert.AreEqual(pinned.id, Lines(sim).Single().notableId);
            Assert.AreEqual(pinned.id, sim.State.statueFor);
        }

        [Test]
        public void TheStatue_NeedsAVillage_AndTwelvePeople()
        {
            var sim = NewSim(83, new SimConfig { dilemmas = true, moments = true, ecology = true, historyPruneYears = 0 });
            for (int m = 0; m < 12; m++) sim.Advance(1.0 / 12);
            sim.Config.pinLegendHours = 1e6f;
            var founder = StatueRules.Founder(sim.State.notables);
            founder.alive = false; founder.diedMonth = sim.State.month - 40;
            Months(sim, 36);
            Assert.AreEqual(Era.Camp, sim.State.era);
            Assert.IsEmpty(Lines(sim), "a camp has no green");
            sim.ForceNextEra();
            sim.State.population = 11;
            ToCalendarMonth(sim, 5);
            sim.Advance(1.0 / 12); sim.Advance(1.0 / 12);
            Assert.IsEmpty(Lines(sim), "11 people: not a grown village");
        }

        [Test]
        public void TheStatue_IsSaved_AsTwoNumbers_AndAnOldSaveHasNone()
        {
            var sim = Village(84);
            sim.Config.pinLegendHours = 1e6f;
            Assert.IsNotNull(sim.DebugStory(StoryEvent.FounderStatue, out string why), why);
            var loaded = new SimCore(sim.Config, JsonUtility.FromJson<SimState>(JsonUtility.ToJson(sim.State)));
            Assert.AreEqual(sim.State.statueFor, loaded.State.statueFor);
            Assert.AreEqual(sim.State.statueMonth, loaded.State.statueMonth);
            Assert.IsTrue(loaded.StatueStands);
            // an old save has no such fields: none stands
            string json = JsonUtility.ToJson(sim.State);
            json = System.Text.RegularExpressions.Regex.Replace(json, "\"statueFor\":-?\\d+,?", "");
            json = System.Text.RegularExpressions.Regex.Replace(json, "\"statueMonth\":-?\\d+,?", "");
            var old = new SimCore(sim.Config, JsonUtility.FromJson<SimState>(json));
            Assert.IsFalse(old.StatueStands);
            Assert.AreEqual(-1, old.State.statueFor);
        }

        [Test]
        public void TheStatue_Cheats_RaiseStandSkipAndRemove()
        {
            var sim = Village(85);
            sim.Config.pinLegendHours = 1e6f;
            Months(sim, 30); // so that a death two years back is still a month of the box (a month below 0 is no death)
            var founder = StatueRules.Founder(sim.State.notables);
            Assert.IsTrue(founder.alive);
            var e = sim.DebugStory(StoryEvent.FounderStatue, out string why);
            Assert.IsNotNull(e, why);
            Assert.IsFalse(founder.alive, "a living person is marked dead a year back");
            Assert.GreaterOrEqual(sim.State.month - founder.diedMonth, 12);
            Assert.AreEqual(founder.id, sim.State.statueFor);
            sim.DebugStatueRemove();
            Assert.IsFalse(sim.StatueStands);
            Assert.IsFalse(sim.StoryHappened(StoryEvent.FounderStatue), "the raising can be replayed");
            Assert.AreSame(founder, sim.DebugStatueStand(out _), "stand it: no line");
            Assert.IsTrue(sim.StatueStands);
            Assert.AreEqual(1, Lines(sim).Length, "only the raise-now line");
            Assert.IsNotNull(sim.DebugStatueMakeAnniversary());
            Assert.IsTrue(sim.StatueAnniversary, "the death day, a year on");
            sim.DebugStatueSkipYear();
            Assert.IsTrue(sim.StatueAnniversary, "two years on is the day as well");
            Months(sim, 1);
            Assert.IsFalse(sim.StatueAnniversary, "the next day is not");
            sim.DebugStatueRemove();
            Assert.IsNull(sim.StatueNotable);
            Assert.IsFalse(sim.StatueAnniversary);
        }

        [Test]
        public void TheStatuesSite_OnARealVillage_IsOffThePaths_AndTheSameOnEveryLoad()
        {
            var sim = Village(86);
            Months(sim, 6 * SimConfig.MonthsPerYear);
            Assert.IsTrue(sim.FindStatueSite(out float x, out float z, out float yaw), "a village has open ground on its green");
            var map = sim.Settlement.Map;
            Assert.Less(Terrain.Dist(x, z, map.greenX, map.greenZ), StatueSpot.FallbackReach + 0.06f);
            Assert.Less(sim.Settlement.TierAt(x, z), PathNet.PathTier, "never on a path");
            Assert.IsTrue(StatueSpot.OpenCell(map, SiteMap.CellAt(x, z)));
            // a reload (a new sim from the same state) works the same spot out
            var loaded = new SimCore(sim.Config, JsonUtility.FromJson<SimState>(JsonUtility.ToJson(sim.State)));
            if (loaded.FindStatueSite(out float x2, out float z2, out float yaw2))
            { Assert.AreEqual(x, x2, 1e-4f); Assert.AreEqual(z, z2, 1e-4f); Assert.AreEqual(yaw, yaw2, 1e-3f); }
        }

        [Test]
        public void TheStatue_EventListsKnowIt()
        {
            Assert.Contains(StoryEvent.FounderStatue, StoryEvent.All);
            Assert.Contains(StoryEvent.FounderStatue, StoryEvent.OncePerBox);
            Assert.IsTrue(StoryEvent.Staged(StoryEvent.FounderStatue), "a held Story moment of its own");
            Assert.AreEqual("story_founder_statue", StoryEvent.Kind(StoryEvent.FounderStatue));
            Assert.AreEqual(StoryEvent.FounderStatue, StoryEvent.OfKind("story_founder_statue"));
        }
    }
}
