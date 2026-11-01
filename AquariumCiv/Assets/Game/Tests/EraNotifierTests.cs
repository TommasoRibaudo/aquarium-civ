using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using AquariumCiv.Game;
using AquariumCiv.Sim;
using NUnit.Framework;
using UnityEngine;
using static AquariumCiv.Tests.SimCoreTests;

namespace AquariumCiv.Tests
{
    /// <summary>#97: the close. The run ahead on its worker lands where it lands on the main thread, one note goes, an open beats it, and the reset's shelf.</summary>
    public class EraNotifierTests
    {
        sealed class FakePhone : ILocalNotifications
        {
            public readonly List<string> log = new();
            public TimeSpan after;
            public string title, body;
            public void Prepare() => log.Add("prepare");
            public IEnumerator Ask(Action<bool> answered) { answered(true); yield break; }
            public void Schedule(int id, TimeSpan a, string t, string b) { lock (log) { log.Add("schedule"); after = a; title = t; body = b; } }
            public void CancelAll() { lock (log) log.Add("cancel"); }
            public int Count(string what) { lock (log) return log.FindAll(l => l == what).Count; }
            public string Tail { get { lock (log) return log[log.Count - 1]; } }
        }

        static SimState Copy(SimState s) => JsonUtility.FromJson<SimState>(JsonUtility.ToJson(s));

        [Test]
        public void TheWorkerFindsWhatTheMainThreadFinds()
        {
            var sim = NewSim(1701);
            sim.Advance(2.5);
            var here = sim.RunAhead(Copy);
            Assert.IsTrue(here.Found);
            var phone = new FakePhone();
            var n = new EraNotifier(phone);
            var close = DateTime.UtcNow;
            n.Close(sim, close, true);
            Assert.IsTrue(n.WaitForRun(30000));
            var r = n.Last.Value.result;
            Assert.AreEqual(here.month, r.month);
            Assert.AreEqual(here.realHours, r.realHours, 1e-9);
            Assert.AreEqual(1, phone.Count("schedule"), "one notification");
            Assert.AreEqual(here.realHours, phone.after.TotalHours, 2.0 / 60, "at close + the real hours");
            Assert.IsNotNull(n.Last.Value.ToString());
            Debug.Log("[notifytest] " + n.Last.Value);
        }

        [Test]
        public void TwoRunsAtOnceDontTouch()
        {
            // The live box and the worker may both be placing lots: the sim's scratch is per thread.
            var a = NewSim(1702);
            var b = NewSim(1702);
            a.Advance(1); b.Advance(1);
            var alone = a.RunAhead(Copy);
            var phone = new FakePhone();
            var n = new EraNotifier(phone, 0);
            n.Close(b, DateTime.UtcNow, true);
            for (int i = 0; i < 30 && n.Running; i++) NewSim(1703 + i).Advance(2);
            Assert.IsTrue(n.WaitForRun(30000));
            Assert.AreEqual(alone.month, n.Last.Value.result.month, "the same month with the main thread busy in another sim");
        }

        [Test]
        public void OffItSchedulesNothing()
        {
            var phone = new FakePhone();
            var n = new EraNotifier(phone);
            n.Close(NewSim(1704), DateTime.UtcNow, false);
            Assert.IsFalse(n.Running);
            Assert.AreEqual(0, phone.Count("schedule"));
        }

        [Test]
        public void AnEraAlreadyWaitingHasNothingToSay()
        {
            var sim = NewSim(1705);
            sim.State.eraReady = true;
            var phone = new FakePhone();
            var n = new EraNotifier(phone);
            n.Close(sim, DateTime.UtcNow, true);
            Assert.IsTrue(n.WaitForRun(30000));
            Assert.AreEqual(RunAheadMiss.AlreadyWaiting, n.Last.Value.result.miss);
            Assert.AreEqual(0, phone.Count("schedule"));
        }

        [Test]
        public void AnOpenBeatsTheRunInFlight()
        {
            var phone = new FakePhone();
            var n = new EraNotifier(phone, 0);
            n.Close(NewSim(1706), DateTime.UtcNow, true);
            n.Open();
            Assert.IsFalse(n.Running, "dropped");
            System.Threading.Thread.Sleep(50);
            Assert.AreEqual("cancel", phone.Tail, "nothing goes after the open's cancel");
        }

        [Test]
        public void ATimeAlreadyGoneStillGivesThePhoneAMoment()
        {
            var sim = NewSim(1707);
            sim.Advance(2);
            var phone = new FakePhone();
            var n = new EraNotifier(phone);
            n.Close(sim, DateTime.UtcNow.AddDays(-20), true);
            Assert.IsTrue(n.WaitForRun(30000));
            Assert.AreEqual(1, phone.Count("schedule"));
            Assert.AreEqual(EraNotifier.SoonestSeconds, phone.after.TotalSeconds, 0.001);
        }

        [Test]
        public void StoppingARunAheadStopsItBeforeTheFirstMonth()
        {
            var r = NewSim(1708).RunAhead(Copy, () => true);
            Assert.AreEqual(RunAheadMiss.Stopped, r.miss);
            Assert.AreEqual(0, r.monthsRun);
            Assert.IsFalse(r.Found);
        }

        [Test]
        public void TheWordsAreWarmAndAskForNothing()
        {
            foreach (var target in new[] { RunAheadTarget.NextEra, RunAheadTarget.Peak })
            foreach (var name in new[] { "Mossbarrow", "" })
            {
                EraNotifier.Words(new RunAheadResult { target = target }, name, out string title, out string body);
                Assert.AreEqual(name == "" ? "Your box" : name, title);
                foreach (var nag in new[] { "miss", "hurry", "come back", "don't", "now", "!", "days", "hours" })
                    StringAssert.DoesNotContain(nag, body.ToLowerInvariant());
                foreach (char c in body) Assert.IsFalse(char.IsDigit(c), "no counts");
            }
        }

        [Test]
        public void ResetTakesTheActiveBoxAndLeavesTheShelf()
        {
            var cfg = new SimConfig();
            var dir = Path.Combine(Path.GetTempPath(), "aqciv-reset-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, "box.json");
            try
            {
                var session = ClockLogic.Start(path, DateTime.UtcNow, cfg, 1709);
                session.sim.Advance(2);
                session.shelf.boxes.Add(new ShelfBox { index = 1, museum = true, state = Copy(session.sim.State), plate = "Old" });
                var fresh = ClockLogic.ResetActive(session, cfg, 1710);
                Assert.AreSame(fresh, session.sim);
                Assert.AreEqual(1710, fresh.State.seed);
                Assert.IsTrue(session.newWorld, "the first run comes again");
                Assert.AreSame(fresh.State, session.shelf.Active.state);
                Assert.AreEqual(2, session.shelf.boxes.Count, "the museum stays");
                Assert.IsTrue(ClockLogic.Save(path, session, DateTime.UtcNow));
                Assert.AreEqual(SaveStore.LoadResult.Loaded, SaveStore.Read(path, out var e));
                Assert.AreEqual(1710, e.state.seed);
            }
            finally { Directory.Delete(dir, true); }
        }
    }
}
