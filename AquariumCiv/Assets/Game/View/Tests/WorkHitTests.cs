using System.Collections.Generic;
using AquariumCiv.Life;
using NUnit.Framework;
using UnityEngine;

namespace AquariumCiv.View.Tests
{
    /// <summary>#355: <see cref="PeopleView.WorkHit"/> in a live box (EditMode): once per stroke for the woodcutters and the hammerers,
    /// at the tool's end, near the doll.</summary>
    public class WorkHitTests
    {
        /// <summary>Seeds tried in turn (30 years each): the first whose box has someone chopping or hammering from 8:30 is checked. 2026-11-24:
        /// seed 7's box had no one at it any more after the deeper box (#658) changed its land; a list keeps the test on what it checks
        /// (one hit per stroke), not on one seed's luck.</summary>
        static readonly int[] Seeds = { 7, 3, 11, 42, 4242, 1 };

        [Test]
        public void WorkHit_FiresOncePerStroke_ForChopAndHammerDolls()
        {
            var tried = new List<string>();
            foreach (int seed in Seeds)
            {
                if (Run(seed, out string why)) return;
                tried.Add($"seed {seed}: {why}");
            }
            Assert.Fail("no seed had anyone chop or hammer from 8:30 for five view hours (30 years): " + string.Join("; ", tried));
        }

        /// <summary>One box: false (and why) when no one chopped or hammered, so the next seed is tried; the checks assert inside.</summary>
        static bool Run(int seed, out string why)
        {
            why = "";
            var go = new GameObject("TestWorkHitBox");
            go.SetActive(false);
            var box = go.AddComponent<BoxView>();
            box.bakeAO = false;
            box.logRebuilds = false;
            var src = go.AddComponent<SimLayoutSource>();
            src.previewSeed = seed;
            src.previewYears = 30f;
            var people = go.AddComponent<PeopleView>();
            go.SetActive(true);
            try
            {
                people.HandDriven = true;
                people.timeScale = 1f;
                people.Tick(0f);
                people.SetHour(8.5f);
                var hits = new List<(PersonRef who, DollStyle style, float t, Vector3 at)>();
                float now = 0f;
                people.WorkHit += (who, place, style, at) => hits.Add((who, style, now, at));

                // 30 fps for up to 90 s of real time (about five work hours on the view clock), until someone has struck a dozen times.
                const float dt = 1f / 30f;
                int strokes = 0;
                for (int f = 0; f < 2700 && strokes < 12; f++)
                {
                    people.Tick(dt);
                    now += dt;
                    strokes = 0;
                    foreach (var h in hits) if (h.style is DollStyle.Chop or DollStyle.Hammer) strokes++;
                }
                if (strokes == 0) { why = "nobody chopped or hammered"; return false; }

                // Once per stroke: one person's chop/hammer hits are a stroke apart (the fastest is a hammer at pace 1.5: 0.5 s), never two
                // on one stroke (a hit window lasts ~4 frames at 30 fps; 0.2 s leaves room for a change of pace between two blocks).
                var last = new Dictionary<int, float>();
                int checkedGaps = 0;
                foreach (var h in hits)
                {
                    Assert.IsFalse(float.IsNaN(h.at.x + h.at.y + h.at.z));
                    Assert.LessOrEqual(Mathf.Abs(h.at.x), Land.HX + 0.3f, "the hit is in the box");
                    Assert.IsTrue(Land.InZ(h.at.z, -0.3f), "the hit is in the box");
                    if (h.style is not (DollStyle.Chop or DollStyle.Hammer)) continue;
                    if (last.TryGetValue(h.who.Key, out float prev) && h.t - prev < 3f)
                    {
                        Assert.GreaterOrEqual(h.t - prev, 0.2f, $"seed {seed}, {h.who}: two WorkHits {h.t - prev:0.000} s apart on one stroke");
                        checkedGaps++;
                    }
                    last[h.who.Key] = h.t;
                }
                if (checkedGaps == 0) { why = $"{strokes} stroke(s), no one struck twice in a row"; return false; }

                // The hit is at the tool's end, near the doll that swung it.
                foreach (var d in go.GetComponentsInChildren<PegDollView>(false))
                {
                    if (!people.TryGetActivity(d, out _)) continue;
                    var p = WorkHitPoint.HitPoint(d);
                    Assert.Less(Vector3.Distance(p, d.transform.position), 0.5f, $"seed {seed}, {d.name}: the hit point is on the doll's reach");
                }
                return true;
            }
            finally { Object.DestroyImmediate(go); }
        }
    }
}
