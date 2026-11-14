using System.Collections;
using System.Linq;
using AquariumCiv.Game;
using AquariumCiv.Life;
using AquariumCiv.Sim;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AquariumCiv.View.PlayTests
{
    /// <summary>P8 polish #285/#267 (view half): the cards say "of the tribe" before the camp gate (#656: the job's own name, no "root digger"); a bucket is muddy while the
    /// pond is dry and clean once it has water; with a puddle and no pond someone scoops at the puddle.</summary>
    [Category("EcoWaterJobs")]
    public class EcoWaterJobsPlayTests : PlayTestBase
    {
        const string Dir = "p8/polish/";
        static SimCore Sim => GameClock.Instance.ActiveSim;

        static IEnumerator Bucket(WaterKind want, PlaceKind from, string shot)
        {
            var pv = PeopleView.Find();
            pv.timeScale = 8f;
            PersonRef who = PersonRef.None;
            PersonRef ew = PersonRef.None;
            var seen = new System.Collections.Generic.HashSet<string>();
            for (int attempt = 0; attempt < 10 && who.IsNone; attempt++)
            {
                if (attempt > 0) { SimLayoutSource.DebugSkipYears(0.02f); yield return Frames(5); PeopleView.DebugSetHour(5f); yield return Frames(5); }
                pv.timeScale = 8f;
            yield return Until(() =>
            {
                foreach (var a in pv.Activities) { seen.Add(a.kind + "/" + a.prop + "/" + a.water); if (a.prop == Prop.EmptyBucket) ew = a.who; }
                foreach (var a in pv.Activities)
                    if (a.prop == Prop.Buckets && a.kind == ActivityKind.FetchWater && pv.TryGetDollPosition(a.who, out _)) { who = a.who; Assert.AreEqual(want, a.water, "the bucket's water"); Assert.AreEqual(from, a.from.kind, "drawn from"); return true; }
                return false;
            }, 30f);
            }
            pv.timeScale = 0.02f;
            if (who.IsNone && !ew.IsNone)
            {
                var day = new System.Collections.Generic.List<PersonActivity>();
                pv.Planner.DaySchedule(ew, 0, day);
                Debug.Log("[wj] " + string.Join(" | ", day.Select(a => $"{a.start:0.0}-{a.end:0.0} {a.kind} {a.prop} {a.water} {(a.moving ? a.from.kind + ">" : "")}{a.place.kind}")));
            }
            Assert.IsFalse(who.IsNone, "somebody carries a full bucket; seen " + string.Join(" ", seen) + " hour " + pv.Now + " scale " + pv.timeScale);
            var cam = Camera.main.GetComponent<BoxCamera>();
            cam.FlyToPerson(who, 0.3f, 0.5f);
            yield return new WaitForSecondsRealtime(0.9f);
            System.IO.Directory.CreateDirectory(Shots + Dir);
            yield return Shot(Dir + shot);
            pv.timeScale = 1f;
        }

        [UnityTest, Timeout(900000)]
        public IEnumerator Cards_MuddyBucket_CleanBucket_PuddleScoop()
        {
            UseLivingLand();
            yield return OpenSlice(seed: 7531);
            var sim = Sim;
            Assert.IsTrue(sim.EcologyOn);
            ShowMeDirector.BubblesOn = false;
            yield return Frames(10);

            // cards
            Assert.IsTrue(sim.BeforeCamp, "before the camp gate");
            var n = sim.State.notables.First(x => x.alive && x.age >= SimCore.AdultAge);
            n.job = JobType.Gatherer;
            // #656: the root digger is gone (no root dig); the card says the job's own name
            Assert.That(sim.CardRoleLine(n), Does.StartWith("gatherer"));
            Assert.AreEqual("gatherer, chief of the tribe", sim.CardRoleLine(n, RoleType.Chief));
            Assert.That(PersonCardView.DebugOpen(n.id), Does.StartWith("card:"));
            yield return Frames(30);
            Assert.That(PersonCardView.Instance.Card.roleLine, Does.StartWith("gatherer"));
            if (PersonCardView.Instance.Card.roleLine.Contains("chief")) StringAssert.EndsWith("of the tribe", PersonCardView.Instance.Card.roleLine);
            System.IO.Directory.CreateDirectory(Shots + Dir);
            yield return Shot(Dir + "285_card");
            PersonCardView.Instance.Close();
            yield return Frames(10);

            // dry: muddy from the wet low ground
            Assert.AreEqual(0, sim.EcoPondCells);
            PeopleView.DebugSetHour(5f);
            yield return Bucket(WaterKind.Muddy, PlaceKind.Stream, "267_bucket_muddy");

            // a puddle and no pond: the scoop is at the puddle
            Assert.Greater(sim.DebugPuddlesNow(0f, 0f, 1.2f), 0, "puddles stand");
            Assert.IsTrue(sim.EcoHasPuddles);
            PeopleView.DebugSetHour(5f);
            yield return Frames(5);
            var pv = PeopleView.Find();
            bool atPuddle = false;
            for (int attempt = 0; attempt < 10 && !atPuddle; attempt++)
            {
                SimLayoutSource.DebugSkipYears(0.02f); yield return Frames(5); PeopleView.DebugSetHour(5f); yield return Frames(5);
                pv.timeScale = 8f;
                yield return Until(() => { foreach (var a in pv.Activities) if (a.kind == ActivityKind.FetchWater && (a.place.kind == PlaceKind.Puddle || a.from.kind == PlaceKind.Puddle)) { atPuddle = true; Assert.AreEqual(WaterKind.Muddy, a.water); } return atPuddle; }, 30f);
            }
            pv.timeScale = 1f;
            Assert.IsTrue(atPuddle, "someone scooped at the puddle");

            // pond has water: clean
            EcoDebug.WaterBreaksNow();
            yield return Frames(30);
            Assert.Greater(sim.EcoPondCells, 0);
            PeopleView.DebugSetHour(5f);
            yield return Bucket(WaterKind.Clean, PlaceKind.Pond, "267_bucket_clean");
            AssertNoErrors("water jobs");
        }
    }
}
