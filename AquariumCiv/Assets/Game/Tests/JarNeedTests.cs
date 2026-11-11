using System.Collections.Generic;
using AquariumCiv.Sim;
using AquariumCiv.Sim.Eco;
using NUnit.Framework;

namespace AquariumCiv.Tests
{
    /// <summary>#570 what is missing, in words: a shut jar's need follows the same conditions as its readiness, and the camp's and the
    /// village's line lists exactly the pieces the gate still lacks and counts down.</summary>
    public class JarNeedTests
    {
        static SimCore World(int seed = 7)
        {
            var cfg = new SimConfig { ecology = true };
            return new SimCore(cfg, SimCore.NewWorld(seed, cfg));
        }

        // the sim's own model: SimCore.EcoM builds one over the same state; a second one over the state reads the same counts
        static EcoModel Model(SimCore sim) => new EcoModel(sim.State.eco, sim.EcoGround, sim.Config.eco);

        static void SetPlaced(EcoModel m, LifeSpecies s, long month = 0)
        {
            var j = m.Jar(s);
            j.state = (int)JarState.Placed; j.placings = 1; j.placedMonth = month;
        }

        [Test]
        public void EveryJarSaysWhatItWaitsFor_OnABareBox()
        {
            var sim = World(); var m = Model(sim);
            Assert.AreEqual("Bushes need a meadow", m.JarNeed(LifeSpecies.Thicket, 0));
            Assert.AreEqual("Rabbits need the bushes first", m.JarNeed(LifeSpecies.Rabbits, 0));
            Assert.AreEqual("Deer need the rabbits first", m.JarNeed(LifeSpecies.Deer, 0));
            Assert.AreEqual("Ducks need the rabbits first", m.JarNeed(LifeSpecies.Ducks, 0));
            Assert.AreEqual("Fish need the ducks first", m.JarNeed(LifeSpecies.Fish, 0));
            Assert.AreEqual("Wolves need the deer first", m.JarNeed(LifeSpecies.Wolves, 0));
        }

        [Test]
        public void TheLandNeeds_WithThePlacedRungsBelow()
        {
            var sim = World(); var m = Model(sim); var st = sim.State.eco;
            foreach (var s in new[] { LifeSpecies.Thicket, LifeSpecies.Rabbits, LifeSpecies.Deer, LifeSpecies.Ducks }) SetPlaced(m, s, 0);
            Assert.AreEqual(JarState.Locked, m.JarStateOf(LifeSpecies.Fish));
            Assert.AreEqual("Fish need reeds round the pond (0/6)", m.JarNeed(LifeSpecies.Fish, 0));
            st.cReeds = 4;
            Assert.AreEqual("Fish need reeds round the pond (4/6)", m.JarNeed(LifeSpecies.Fish, 100));
            st.cReeds = 6;
            Assert.AreEqual("Fish wait for the ducks to settle in", m.JarNeed(LifeSpecies.Fish, 3), "the ducks were placed at month 0");
            Assert.IsNull(m.JarNeed(LifeSpecies.Fish, 6));
            // the deer and the ducks were placed, so their jars are not shut: no need
            Assert.IsNull(m.JarNeed(LifeSpecies.Deer, 0));
            // wolves: the herd first, then the wood
            Assert.AreEqual("Wolves need a herd of 5 deer", m.JarNeed(LifeSpecies.Wolves, 0));
            st.deerFiveSince = 0; st.cTrees = 12;
            Assert.AreEqual("Wolves need a deeper wood (12/20 trees)", m.JarNeed(LifeSpecies.Wolves, 100));
        }

        [Test]
        public void ARoomierMeadow_AndAPond_ReadBigger()
        {
            var sim = World(); var m = Model(sim); var st = sim.State.eco;
            SetPlaced(m, LifeSpecies.Thicket); SetPlaced(m, LifeSpecies.Rabbits);
            Assert.AreEqual("Ducks need a pond", m.JarNeed(LifeSpecies.Ducks, 0));
            st.cPond = 5;
            Assert.AreEqual("Ducks need a bigger pond", m.JarNeed(LifeSpecies.Ducks, 0));
            st.cPond = sim.Config.eco.jarDucksPond;
            Assert.IsNull(m.JarNeed(LifeSpecies.Ducks, 0));
            Assert.AreEqual("Deer need more trees (0/5)", m.JarNeed(LifeSpecies.Deer, 0));
            st.cTrees = 4;
            Assert.AreEqual("Deer need more trees (4/5)", m.JarNeed(LifeSpecies.Deer, 0));
            st.cTrees = 5;
            Assert.IsNull(m.JarNeed(LifeSpecies.Deer, 0));
            st.cGrass = 1;
            Assert.AreEqual("Rabbits need a bigger meadow", RabbitsShutNeed(m), "a patch of grass is a meadow, only too small");
        }

        static string RabbitsShutNeed(EcoModel m)
        {
            var j = m.Jar(LifeSpecies.Rabbits); j.state = (int)JarState.Locked;
            return m.JarNeed(LifeSpecies.Rabbits, 0);
        }

        /// <summary>The need is null exactly when the land condition holds (and says something when it does not): the words cannot drift
        /// from <see cref="EcoModel.LandReady"/>. Every species over a spread of counts, with the rungs below placed or not.</summary>
        [Test]
        public void TheNeed_IsNullExactlyWhenTheLandIsReady()
        {
            int checkedStates = 0;
            var sim = World(); var m = Model(sim); var st = sim.State.eco;
            foreach (bool rungs in new[] { false, true })
                foreach (int grass in new[] { 0, 1, 40, 400, 4000 })
                    foreach (int trees in new[] { 0, 4, 5, 19, 20 })
                        foreach (int pond in new[] { 0, 11, 12, 30 })
                            foreach (int reeds in new[] { 0, 5, 6 })
                                foreach (long month in new long[] { 0, 5, 6, 20 })
                                {
                                    st.cGrass = grass; st.cTall = 0; st.cTrees = trees; st.cPond = pond; st.cReeds = reeds;
                                    st.deerFiveSince = month >= 20 ? 0 : -1;
                                    foreach (var s in LifeJar.All)
                                    {
                                        var j = m.Jar(s);
                                        j.state = (int)JarState.Locked; j.placings = 0; j.placedMonth = -1;
                                        if (rungs && s != LifeSpecies.Wolves) { j.placings = 1; j.placedMonth = 0; } // placed (the rung below reads IsPlaced), and still shut here
                                    }
                                    foreach (var s in LifeJar.All)
                                    {
                                        string need = m.JarNeed(s, month);
                                        bool ready = m.LandReady(s, month);
                                        Assert.AreEqual(ready, need == null, $"{s} grass {grass} trees {trees} pond {pond} reeds {reeds} month {month} rungs {rungs}: '{need}'");
                                        if (need != null) StringAssert.StartsWith(EcoModel.PlainName(s), need);
                                        checkedStates++;
                                    }
                                }
            Assert.Greater(checkedStates, 1000);
        }

        [Test]
        public void AJarThatIsNotShut_NeedsNothing()
        {
            var sim = World(); var m = Model(sim);
            m.Jar(LifeSpecies.Thicket).state = (int)JarState.Ready;
            Assert.IsNull(m.JarNeed(LifeSpecies.Thicket, 0));
            SetPlaced(m, LifeSpecies.Thicket);
            Assert.IsNull(m.JarNeed(LifeSpecies.Thicket, 0));
        }

        [Test]
        public void TheVillageLine_ListsTheMissingPieces_CountsDown_AndGoesWhenTheVillageStands()
        {
            var sim = World(); var m = Model(sim); var st = sim.State.eco;
            // before the camp: the camp's pieces
            Assert.AreEqual("The camp waits for: a meadow · rabbits in the meadow", sim.VillageWaitsLine);
            st.cGrass = 1;
            Assert.AreEqual("The camp waits for: more meadow · rabbits in the meadow", sim.VillageWaitsLine);
            st.cGrass = (int)(sim.Config.eco.campMeadow * EcoLand.N) + 1;
            SetPlaced(m, LifeSpecies.Rabbits); st.rabbits = 2f;
            Assert.AreEqual("", sim.VillageWaitsLine, "every piece of the camp is there: the gate opens at the next step");
            // the camp stands: the village's pieces, in the gate's order
            st.campGateMonth = 0;
            Assert.AreEqual("The village waits for: more trees (0/8) · a pond · ducks on the pond · fish in the pond", sim.VillageWaitsLine);
            st.cTrees = 3; st.cPond = 4;
            Assert.AreEqual("The village waits for: more trees (3/8) · a bigger pond · ducks on the pond · fish in the pond", sim.VillageWaitsLine);
            st.cTrees = 8; st.cPond = sim.Config.eco.pondGateCells;
            Assert.AreEqual("The village waits for: ducks on the pond · fish in the pond", sim.VillageWaitsLine);
            SetPlaced(m, LifeSpecies.Ducks);
            Assert.AreEqual("The village waits for: fish in the pond", sim.VillageWaitsLine);
            SetPlaced(m, LifeSpecies.Fish);
            Assert.AreEqual("", sim.VillageWaitsLine, "every piece is there");
            st.cTrees = 7;
            Assert.AreEqual("The village waits for: more trees (7/8)", sim.VillageWaitsLine);
            st.villageGateMonth = 12;
            Assert.AreEqual("", sim.VillageWaitsLine, "the village stands: the line is gone");
        }

        [Test]
        public void TheLine_IsEmptyOffTheEcology()
        {
            var cfg = new SimConfig();
            var sim = new SimCore(cfg, SimCore.NewWorld(7, cfg));
            Assert.AreEqual("", sim.VillageWaitsLine);
            Assert.IsNull(sim.JarNeed(LifeSpecies.Rabbits));
            var shut = new List<LifeSpecies>(); sim.LockedJars(shut);
            Assert.AreEqual(0, shut.Count);
        }

        [Test]
        public void LockedJars_ListShutOnesInLadderOrder_AndTheCheatShutsReadyOnes()
        {
            var sim = World();
            var shut = new List<LifeSpecies>(); sim.LockedJars(shut);
            CollectionAssert.AreEqual(LifeJar.All, shut, "a new box: every jar shut, in ladder order");
            sim.DebugJarsReady();
            sim.LockedJars(shut);
            Assert.AreEqual(0, shut.Count);
            Assert.AreEqual(LifeJar.All.Length, sim.DebugLockJars());
            sim.LockedJars(shut);
            Assert.AreEqual(LifeJar.All.Length, shut.Count);
        }
    }
}
