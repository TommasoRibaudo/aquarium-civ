using System.Collections.Generic;
using System.Linq;
using AquariumCiv.Sim;
using AquariumCiv.Sim.Settle;
using Terrain = AquariumCiv.Sim.Settle.Terrain;
using NUnit.Framework;
using UnityEngine;
using static AquariumCiv.Tests.SimCoreTests;

namespace AquariumCiv.Tests
{
    /// <summary>
    /// #371 (visit loop C4; visit-loop.md 2.5): every option leaves a thing in the box within a game year, the thing is saved, a
    /// callback is a live moment (a card only if the moment becomes one), and the person card says "Chosen by the Keeper". Pure C#,
    /// so it also runs in Tools/SimTests.
    /// </summary>
    public class ConsequencePropTests
    {
        static SimConfig Cfg() => new SimConfig { dilemmas = true, moments = true };

        static DilemmaDef Def(string id, string prop, PropAnchor at, string building = "") => new DilemmaDef
        {
            id = id, kind = DilemmaKind.WhichWay, cast = DilemmaCast.None, question = "The fields, or the woods?",
            options = new[]
            {
                new DilemmaOptionDef { label = "the fields", hint = "h", prop = prop, propAt = at, propBuilding = building, callbacks = new[] { new DilemmaCallbackDef { id = "c", text = "the granary stands full." } } },
                new DilemmaOptionDef { label = "the woods", hint = "h", prop = prop, propAt = at, propBuilding = building },
            },
        };

        static SimCore Raised(int seed, DilemmaDef def)
        {
            var sim = NewSim(seed, Cfg());
            sim.DilemmaCatalog = new[] { def };
            for (int m = 0; m < 12; m++) sim.Advance(1.0 / 12);
            Assert.IsNotNull(sim.DebugRaiseDilemma(def.id), "the def raises");
            return sim;
        }

        [Test]
        public void EveryOptionOfEveryChoiceHasAProp()
        {
            var all = DilemmaDefs.All.Concat(SmallChoices.All).ToList();
            Assert.AreEqual(11, all.Count); // t4-fixes: + the story and supper small choices
            foreach (var def in all)
                foreach (var o in def.options)
                {
                    CollectionAssert.Contains(ConsequenceKind.All, o.prop, $"{def.id}: option '{o.label}' leaves a thing");
                    if (!string.IsNullOrEmpty(o.prop2)) CollectionAssert.Contains(ConsequenceKind.All, o.prop2, $"{def.id}: option '{o.label}' leaves a second thing");
                }
            var kinds = DilemmaDefs.All.SelectMany(d => d.options).Select(o => o.prop).Distinct().ToList();
            Assert.GreaterOrEqual(kinds.Count, 14, "the big choices' options each leave their own thing");
            Assert.AreEqual(16, DilemmaDefs.All.Sum(d => d.options.Length));
        }

        [Test]
        public void AnAnsweredChoiceLeavesItsThingWithinAYearAtAnOpenSpot()
        {
            foreach (var at in new[] { PropAnchor.Home, PropAnchor.Building, PropAnchor.Fire, PropAnchor.Edge, PropAnchor.Pond })
                for (int seed = 1; seed <= 5; seed++)
                {
                    var sim = Raised(seed, Def("p", ConsequenceKind.Feather, at, BuildingType.Hut));
                    var d = sim.OpenDilemma;
                    Assert.IsTrue(sim.AnswerDilemma(d.id, 0, DilemmaPath.Card));
                    for (int m = 0; m < SimCore.PropWithinMonths; m++) sim.Advance(1.0 / 12);
                    var p = sim.ConsequencePropFor(d.id, 0);
                    Assert.IsNotNull(p, $"{at} seed {seed}");
                    Assert.IsTrue(p.placed, $"{at} seed {seed}: within a game year");
                    Assert.LessOrEqual(p.placedMonth - d.endMonth, SimCore.PropWithinMonths, $"{at} seed {seed}");
                    Assert.LessOrEqual(System.Math.Abs(p.x), Terrain.HX - Terrain.GlassMargin + 0.001f);
                    Assert.IsTrue(Terrain.InZ(p.z, Terrain.GlassMargin - 0.001f), "inside the glass front and back");
                    Assert.IsNull(sim.ConsequencePropFor(d.id, 1), "only the option taken leaves its thing");
                    Assert.IsTrue(p.keeper);
                }
        }

        [Test]
        public void ALapsedChoiceLeavesItsThingToo()
        {
            var sim = Raised(3, Def("p", ConsequenceKind.Baskets, PropAnchor.Fire));
            var d = sim.OpenDilemma;
            Assert.IsTrue(sim.DebugLapseDilemma());
            var p = sim.ConsequencePropFor(d.id, d.option);
            Assert.IsNotNull(p);
            Assert.IsFalse(p.keeper, "the people's choice, not the Keeper's");
        }

        [Test]
        public void LetThemDecideLeavesTheDefaultOptionsThing()
        {
            var sim = Raised(4, Def("p", ConsequenceKind.Staff, PropAnchor.Fire));
            var d = sim.OpenDilemma;
            Assert.IsTrue(sim.LetThemDecide(d.id));
            var p = sim.ConsequencePropFor(d.id, d.option);
            Assert.IsNotNull(p);
            Assert.IsTrue(p.keeper, "handed back by the Keeper: credited");
        }

        [Test]
        public void EveryRealOptionsPropCanBePlacedInOneBox()
        {
            var sim = NewSim(9, Cfg());
            for (int m = 0; m < 24; m++) sim.Advance(1.0 / 12);
            int n = sim.DebugPlaceEveryProp();
            Assert.AreEqual(23, n, "16 big options and 2 options each of naming, story and supper (t4-fixes), and the pond family's goat pen beside their boat (2026-11-16)");
            Assert.AreEqual(n, sim.ConsequenceProps.Count(p => p.placed));
            foreach (var p in sim.ConsequenceProps)
            {
                Assert.LessOrEqual(System.Math.Abs(p.x), Terrain.HX - Terrain.GlassMargin + 0.001f, p.kind);
                Assert.IsTrue(Terrain.InZ(p.z, Terrain.GlassMargin - 0.001f), p.kind);
            }
        }

        [Test]
        public void ThePropsPersistInTheSave()
        {
            var sim = Raised(5, Def("p", ConsequenceKind.WoodpileRoof, PropAnchor.Building, BuildingType.Hut));
            var d = sim.OpenDilemma;
            sim.AnswerDilemma(d.id, 1, DilemmaPath.Card);
            for (int m = 0; m < 6; m++) sim.Advance(1.0 / 12);
            var json = JsonUtility.ToJson(sim.State);
            var back = JsonUtility.FromJson<SimState>(json);
            Assert.AreEqual(json, JsonUtility.ToJson(back));
            Assert.AreEqual(1, back.consequenceProps.Count);
            Assert.AreEqual(ConsequenceKind.WoodpileRoof, back.consequenceProps[0].kind);
            Assert.AreEqual(sim.State.consequenceProps[0].x, back.consequenceProps[0].x);
            var old = JsonUtility.FromJson<SimState>("{\"seed\":7,\"month\":5}");
            Assert.IsTrue(old.consequenceProps == null || old.consequenceProps.Count == 0, "an old save has none");
        }

        [Test]
        public void ThePlacementIsDeterministic()
        {
            var a = Raised(6, Def("p", ConsequenceKind.Feather, PropAnchor.Fire));
            var b = Raised(6, Def("p", ConsequenceKind.Feather, PropAnchor.Fire));
            a.AnswerDilemma(a.OpenDilemma.id, 0, DilemmaPath.Card);
            b.AnswerDilemma(b.OpenDilemma.id, 0, DilemmaPath.Card);
            Assert.AreEqual(JsonUtility.ToJson(a.State.consequenceProps[0]), JsonUtility.ToJson(b.State.consequenceProps[0]));
        }

        // ---------- the callback as a live moment ----------

        [Test]
        public void ACallbackIsAMomentNotACardUntilItBecomesOne()
        {
            Assert.IsFalse(MomentRules.AlsoACard(MomentKind.Callback));
            var sim = Raised(7, Def("p", ConsequenceKind.Feather, PropAnchor.Fire));
            sim.AnswerDilemma(sim.OpenDilemma.id, 0, DilemmaPath.Card);
            var e = sim.DebugFireCallbackNow();
            Assert.IsNotNull(e);
            var m = sim.MomentOfEntry(e.id);
            Assert.IsNotNull(m, "the callback queued a moment");
            Assert.AreEqual(MomentKind.Callback, m.kind);
            StringAssert.StartsWith("Because the Keeper chose", e.text, "the moment's caption is the credit");
            Assert.IsTrue(sim.MomentHidesCard(e), "waiting as a moment: not a card");
            Assert.IsTrue(sim.MomentStillHeld(e));
            for (int i = 0; i < 8 * 12 + 2; i++)
            {
                sim.State.population = 30;
                sim.Advance(1.0 / 12);
                sim.State.moments.RemoveAll(x => x != m);
            }
            Assert.AreEqual(HeldMomentState.Carded, m.state);
            Assert.IsFalse(sim.MomentHidesCard(e), "now it is a card, with the credit on it");
        }

        // ---------- the person card ----------

        [Test]
        public void ThePersonCardSaysChosenByTheKeeperOnlyForTheKeepersPick()
        {
            var cb = new DilemmaCallbackDef { id = "c", text = "x." };
            var def = new DilemmaDef
            {
                id = "who", kind = DilemmaKind.Who, cast = DilemmaCast.TwoCandidates, role = DilemmaDef.Leader, replacesHolder = true, question = "{a} or {b}?",
                options = new[]
                {
                    new DilemmaOptionDef { label = "{a}", hint = "h", takesRole = true, prop = ConsequenceKind.Feather, propAt = PropAnchor.Home, callbacks = new[] { cb } },
                    new DilemmaOptionDef { label = "{b}", hint = "h", takesRole = true, prop = ConsequenceKind.Feather, propAt = PropAnchor.Home, callbacks = new[] { cb } },
                },
            };
            SimCore Make(int seed)
            {
                var s = NewSim(seed, Cfg()); s.DilemmaCatalog = new[] { def };
                for (int m = 0; m < 36; m++) s.Advance(1.0 / 12);
                return s.State.era == Era.Camp && s.DebugRaiseDilemma() != null ? s : null;
            }
            SimCore answered = null, lapsed = null, letGo = null;
            for (int seed = 1; seed < 80 && letGo == null; seed++)
            {
                var s = Make(seed);
                if (s == null) continue;
                if (answered == null) answered = s;
                else if (lapsed == null) lapsed = s;
                else letGo = s;
            }
            Assert.IsNotNull(letGo, "three camps with two candidates");

            var d = answered.OpenDilemma;
            answered.AnswerDilemma(d.id, 0, DilemmaPath.Box);
            Assert.AreEqual("Chosen by the Keeper.", answered.PersonCardOf(d.personA).keeperLine);
            Assert.AreEqual("", answered.PersonCardOf(d.personB).keeperLine, "the unchosen one is not marked");

            var dl = lapsed.OpenDilemma;
            lapsed.DebugLapseDilemma();
            Assert.AreEqual("", lapsed.PersonCardOf(dl.PersonOf(dl.option)).keeperLine, "the people chose: no mark");

            var dg = letGo.OpenDilemma;
            letGo.LetThemDecide(dg.id);
            Assert.AreEqual("Chosen by the Keeper.", letGo.PersonCardOf(dg.PersonOf(dg.option)).keeperLine, "handed back by the Keeper: still the Keeper's");
        }

        // ---------- 2026-11-16 "Bring a pen back": the pond family's goat pen ----------

        static readonly DilemmaDef PondFamily = DilemmaDefs.All.Single(d => d.id == "village_hill_family");
        static int HomeHere => System.Array.FindIndex(PondFamily.options, o => o.label == "a home here");

        /// <summary>The pond family raised in the slice (the first seeds that raise it), not answered yet.</summary>
        static List<SimCore> PondFamilyRaised(int want)
        {
            var list = new List<SimCore>();
            for (int s = 1; s <= 12 && list.Count < want; s++)
            {
                var sim = DilemmaContentTests.RaisedWithin40Years(PondFamily, s);
                if (sim != null) list.Add(sim);
            }
            Assert.IsNotEmpty(list, "the pond family is raised in some seed");
            return list;
        }

        [Test]
        public void AHomeHereLeavesTheBoatAndAGoatPenBesideItWithOneGoat()
        {
            int option = HomeHere;
            Assert.AreEqual(ConsequenceKind.ShoreBoat, PondFamily.options[option].prop);
            Assert.AreEqual(ConsequenceKind.GoatPen, PondFamily.options[option].prop2);
            foreach (var sim in PondFamilyRaised(3))
            {
                var d = sim.OpenDilemma;
                Assert.IsFalse(sim.HasGoatPen);
                Assert.AreEqual(0, sim.PenGoats, "no pen, no goat");
                Assert.IsTrue(sim.AnswerDilemma(d.id, option, DilemmaPath.Card));
                Assert.AreEqual("The Keeper gave the pond family a home here. They pulled their boat up by the fire and penned their goat beside it.",
                    sim.State.chronicle.Single(e => e.id == d.answerEntryId).text);
                var boat = sim.ConsequenceProps.Single(p => p.kind == ConsequenceKind.ShoreBoat);
                var pen = sim.GoatPenProp;
                Assert.IsNotNull(pen);
                Assert.AreSame(boat, sim.ConsequencePropFor(d.id, option), "the boat is still the option's first thing");
                Assert.IsTrue(boat.placed && pen.placed, "both stand at once (the fire is always there)");
                Assert.IsTrue(sim.HasGoatPen && sim.PenGoat);
                Assert.AreEqual(1, sim.PenGoats, "one goat");
                Assert.AreEqual(PropAnchor.Fire, pen.anchor);
                Assert.That(pen.yaw == 0 || pen.yaw == 90, "square to the box's lines");
                // near the fire, out past the pot and the seats
                sim.Settlement.Fire(out float fx, out float fz, out _);
                float fromFire = Dist(pen.x, pen.z, fx, fz);
                Assert.That(fromFire, Is.GreaterThanOrEqualTo(SimCore.PenFromFire - 1e-3f), "past the pot and the seats");
                Assert.That(fromFire, Is.LessThan(SimCore.PenFromFire + 0.9f + 0.6f + 0.05f), "by the fire");
                // clear of the boat, the tents, the water and the glass
                Assert.That(Dist(pen.x, pen.z, boat.x, boat.z), Is.GreaterThanOrEqualTo(0.22f + SimCore.BoatRoom + SimCore.PenRoom - 1e-3f), "clear of the boat");
                var map = sim.Settlement.Map;
                int c = SiteMap.CellAt(pen.x, pen.z);
                Assert.IsFalse(map.pond[c], "not in the water");
                Assert.That(map.waterDist[c], Is.GreaterThanOrEqualTo(SimCore.PenFromWater), "not on the shore");
                Assert.AreEqual(0, map.lot[c], "not on a tent's lot");
                foreach (var b in sim.State.buildings.Where(b => b.lotId != 0))
                    Assert.That(Dist(pen.x, pen.z, b.x, b.z), Is.GreaterThan(0.2f), $"clear of the {b.type}");
                Assert.That(System.Math.Abs(pen.x), Is.LessThanOrEqualTo(Terrain.HX - Terrain.GlassMargin - SimCore.PenFromGlass + 1e-3f), "clear of the glass");
                Assert.That(System.Math.Abs(pen.z - Terrain.CZ), Is.LessThanOrEqualTo(Terrain.HZ - Terrain.GlassMargin - SimCore.PenFromGlass + 1e-3f), "clear of the glass");
                foreach (var q in sim.ConsequenceProps.Where(q => q != pen && q.placed))
                    Assert.That(Dist(pen.x, pen.z, q.x, q.z), Is.GreaterThanOrEqualTo(0.22f + SimCore.PenRoom - 1e-3f), q.kind);
            }
            Assert.AreEqual("a little goat pen with their goat", KeeperChoices.PropName(ConsequenceKind.GoatPen));
        }

        static float Dist(float ax, float az, float bx, float bz) => (float)System.Math.Sqrt((ax - bx) * (ax - bx) + (az - bz) * (az - bz));

        [Test]
        public void TheOwnShoreAnswerLeavesNoPenAndNoGoat()
        {
            int option = 1 - HomeHere;
            foreach (var sim in PondFamilyRaised(1))
            {
                var d = sim.OpenDilemma;
                Assert.IsTrue(sim.AnswerDilemma(d.id, option, DilemmaPath.Card));
                Assert.IsFalse(sim.HasGoatPen);
                Assert.AreEqual(0, sim.PenGoats);
                ChronicleEntry e;
                while ((e = sim.DebugFireCallbackNow()) != null) StringAssert.DoesNotContain("goat", e.text);
            }
        }

        [Test]
        public void TheGoatsChewedCornerIsToldOnlyWithThePen()
        {
            int option = HomeHere;
            var cb = PondFamily.options[option].callbacks.Single(c => c.id == "hill_joined_goat");
            Assert.AreEqual(ConsequenceKind.GoatPen, cb.needsProp);
            Assert.AreEqual(2, cb.minYears);
            Assert.AreEqual("", cb.fallback, "no pen: dropped quietly");
            foreach (bool keepPen in new[] { true, false })
                foreach (var sim in PondFamilyRaised(1))
                {
                    var d = sim.OpenDilemma;
                    Assert.IsTrue(sim.AnswerDilemma(d.id, option, DilemmaPath.Card));
                    if (!keepPen) sim.State.consequenceProps.RemoveAll(p => p.kind == ConsequenceKind.GoatPen);
                    var told = new List<string>();
                    ChronicleEntry e;
                    while ((e = sim.DebugFireCallbackNow()) != null) told.Add(e.text);
                    var goat = told.Where(t => t.Contains("goat")).ToList();
                    if (keepPen)
                    {
                        Assert.AreEqual(1, goat.Count, string.Join("\n", told));
                        Assert.AreEqual("Because the Keeper chose a home here, the pond family's goat has chewed the pen's corner again.", goat[0]);
                        var line = sim.State.chronicle.Single(x => x.text == goat[0]);
                        Assert.AreSame(sim.GoatPenProp, sim.ConsequencePropOfEntry(line), "the goat's line is about the pen, not the boat");
                    }
                    else Assert.IsEmpty(goat, "no pen, no goat line");
                }
        }

        [Test]
        public void EveryThingStandsWithThePenOnBothLands()
        {
            foreach (bool living in new[] { false, true })
                for (int seed = 1; seed <= 3; seed++)
                {
                    var cfg = new SimConfig { dilemmas = true, ecology = living };
                    var sim = new SimCore(cfg, SimCore.NewWorld(seed, cfg));
                    for (int m = 0; m < 24; m++) sim.Advance(1.0 / 12);
                    int n = sim.DebugPlaceEveryProp();
                    string where = (living ? "living" : "slice") + " seed " + seed;
                    Assert.AreEqual(sim.ConsequenceProps.Count, n, where + ": every thing stands");
                    var pen = sim.GoatPenProp;
                    Assert.IsNotNull(pen, where + ": the debug cheat leaves the pen too");
                    Assert.IsTrue(sim.PenGoat);
                    float mx = Terrain.HX - Terrain.GlassMargin - SimCore.PenFromGlass + 1e-3f, mz = Terrain.HZ - Terrain.GlassMargin - SimCore.PenFromGlass + 1e-3f;
                    Assert.That(System.Math.Abs(pen.x) <= mx && System.Math.Abs(pen.z - Terrain.CZ) <= mz, where + ": clear of the glass");
                    var boat = sim.ConsequenceProps.First(p => p.kind == ConsequenceKind.ShoreBoat);
                    Assert.That(Dist(pen.x, pen.z, boat.x, boat.z), Is.GreaterThanOrEqualTo(0.22f + SimCore.BoatRoom + SimCore.PenRoom - 1e-3f), where + ": clear of the boat");
                    var map = sim.Settlement?.Map;
                    if (map != null) Assert.IsFalse(map.pond[SiteMap.CellAt(pen.x, pen.z)], where + ": not in the water");
                }
        }

        [Test]
        public void TheCardAlsoSaysNamedByTheKeeper()
        {
            var sim = NewSim(3, Cfg());
            var n = sim.State.notables.First(x => x.alive);
            Assert.AreEqual("", sim.KeeperHandLine(n));
            n.namedByKeeper = true;
            Assert.AreEqual("Named by the Keeper.", sim.KeeperHandLine(n));
            n.chosenByKeeper = true;
            Assert.AreEqual("Chosen and named by the Keeper.", sim.KeeperHandLine(n));
            Assert.AreEqual("Chosen and named by the Keeper.", sim.PersonCardOf(n.id).keeperLine);
        }
    }
}
