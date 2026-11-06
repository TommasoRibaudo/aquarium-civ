using System.Linq;
using AquariumCiv.Sim;
using NUnit.Framework;

namespace AquariumCiv.Life.Tests
{
    /// <summary>#371: a choice's callback is staged by the echo machinery at the thing the choice left (a pure data check).</summary>
    public class ConsequenceEchoTests
    {
        static SimCore NewSim(int seed)
        {
            var cfg = new SimConfig { dilemmas = true, moments = true };
            return new SimCore(cfg, SimCore.NewWorld(seed, cfg));
        }

        [Test]
        public void ACallbackEchoGoesToTheThingTheChoiceLeft()
        {
            var sim = Raised();
            sim.AnswerDilemma(sim.OpenDilemma.id, 0, DilemmaPath.Card);
            for (int m = 0; m < 12; m++) sim.Advance(1.0 / 12);
            var cp = sim.ConsequenceProps.Single();
            Assert.IsTrue(cp.placed);
            var who = sim.State.notables.First(n => n.alive);
            var e = sim.Chronicle(SimCore.CallbackKind, "Because the Keeper chose the fields, the granary stands full.", 0.7f, who.id);
            sim.State.dilemmaCallbacks.Add(new DilemmaCallback { id = 99, dilemmaId = cp.dilemmaId, option = cp.option, state = CallbackState.Fired, entryId = e.id });
            var echo = EchoScenes.For(sim, e);
            Assert.IsTrue(echo.Plays, echo.skip.ToString());
            Assert.AreEqual(EchoWhere.Prop, echo.where);
            Assert.AreEqual(cp.id, echo.propId);
            Assert.AreEqual(e.text, echo.line, "the credit is said in the scene");
            Assert.IsTrue(echo.def.beats.Any(b => b.kind == ActivityKind.Point), "points at it");
            Assert.IsTrue(echo.def.beats.Any(b => b.kind == ActivityKind.HoldUp), "holds up a hand");
            for (int m = 0; m < 7 * 12; m++) sim.Advance(1.0 / 12);
            Assert.IsTrue(EchoScenes.For(sim, e).Plays, "a held callback is not too old to stage");
        }

        static SimCore Raised()
        {
            var def = new DilemmaDef
            {
                id = "p", kind = DilemmaKind.WhichWay, cast = DilemmaCast.None, question = "The fields, or the woods?",
                options = new[]
                {
                    new DilemmaOptionDef { label = "the fields", hint = "h", prop = ConsequenceKind.Feather, propAt = PropAnchor.Fire, callbacks = new[] { new DilemmaCallbackDef { id = "c", text = "the granary stands full." } } },
                    new DilemmaOptionDef { label = "the woods", hint = "h", prop = ConsequenceKind.Feather, propAt = PropAnchor.Fire },
                },
            };
            var sim = NewSim(8);
            sim.DilemmaCatalog = new[] { def };
            for (int m = 0; m < 12; m++) sim.Advance(1.0 / 12);
            Assert.IsNotNull(sim.DebugRaiseDilemma("p"));
            return sim;
        }
    }
}
