// #779: nobody stands inside anybody in the story scenes. The pure part (no scene): BodySpots' free spot, and for the two camp scenes that
// stage watchers (1.2 the two marks, 2.1 the tinkerer's bucket) every member's spot over the scene: when two members stand still (they have
// arrived at a part's spot) they are a body width apart, and clear of the others in the box and of the ground nobody stands on. The wedding (5.2)
// is the wedding moment's plan: Life/Tests/StagedMomentTests has its spots; StoryEventShots films 1.2, 2.1 and 5.2 in the real box.
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using static AquariumCiv.View.Tests.CampScenesTests;

namespace AquariumCiv.View.Tests
{
    public class StorySpotsTests
    {
        static readonly Vector2 Tree = new Vector2(0.4f, 0.9f), Fire = new Vector2(-0.8f, 0.5f);
        const float W = BodySpots.BodyW;

        // ---------------------------------------------------------------- where a scene's members stand over time

        /// <summary>Member <paramref name="m"/>'s spot at scene second <paramref name="u"/>: from <paramref name="start"/> toward each part's spot at the
        /// part's pace (the planner's walk, straight); <paramref name="standing"/>: they have arrived at the part's spot. False before their first part.</summary>
        static bool SpotAt(CampScene s, int m, Vector2 start, float u, out Vector2 at, out bool standing)
        {
            at = start; standing = true;
            if (u < s.FirstFrom(m)) return false;
            foreach (var p in s.parts[m].OrderBy(p => p.from))
            {
                if (p.keepSpot) continue;
                if (u < p.from) break;
                float dt = Mathf.Min(u, p.to) - p.from, dist = (p.pos - at).magnitude;
                at = dist > 1e-5f ? at + (p.pos - at) / dist * Mathf.Min(dist, p.speed * dt) : at;
                standing = (p.pos - at).magnitude < 0.01f;
                if (u < p.to) break;
            }
            return true;
        }

        /// <summary>The closest two members of <paramref name="s"/> stand, over the whole scene, while both stand still (the scene second, and who).</summary>
        static float ClosestStanding(CampScene s, IList<Vector2> starts, out string where)
        {
            float best = float.MaxValue; where = "";
            for (float u = 0f; u <= s.Length; u += 0.05f)
                for (int i = 0; i < s.Count; i++)
                    for (int j = i + 1; j < s.Count; j++)
                    {
                        if (!SpotAt(s, i, starts[i], u, out var a, out bool sa) || !SpotAt(s, j, starts[j], u, out var b, out bool sb) || !sa || !sb) continue;
                        float d = (a - b).magnitude;
                        if (d < best) { best = d; where = $"members {i} and {j} at {u:0.00} s: {a} and {b}"; }
                    }
            return best;
        }

        // ---------------------------------------------------------------- BodySpots

        [Test]
        public void Free_AClearSpotIsKept_ATakenOneMovesToTheNearestFreeOnTheWantedSide_AndIsTaken()
        {
            var taken = new List<Vector2> { Vector2.zero };
            Assert.AreEqual(new Vector2(0.3f, 0f), BodySpots.Free(new Vector2(0.3f, 0f), Vector2.up, taken), "a clear spot stays");
            var moved = BodySpots.Free(new Vector2(0.02f, 0f), Vector2.up, taken);
            Assert.GreaterOrEqual((moved - Vector2.zero).magnitude, W - 1e-3f, "off the first");
            Assert.Greater(moved.y, 0.04f, "on the wanted side (up)");
            Assert.Less((moved - new Vector2(0.02f, 0f)).magnitude, W + 0.04f, "the nearest free spot, not a far one");
            Assert.AreEqual(3, taken.Count, "each spot is taken for the next");
            var again = BodySpots.Free(new Vector2(0.02f, 0f), Vector2.up, taken);
            Assert.GreaterOrEqual((again - moved).magnitude, W - 1e-3f, "the next one keeps clear of it");
            Assert.AreEqual(BodySpots.Free(new Vector2(0.02f, 0f), Vector2.up, new List<Vector2> { Vector2.zero }), moved, "always the same answer");
        }

        [Test]
        public void Free_KeepsClearOfTheOthersInTheBox_AndOfTheWater()
        {
            var room = new StoryRoom { blocked = p => p.x > 0.5f };
            room.others.Add(new Vector2(0.2f, 0f));
            var at = BodySpots.Free(new Vector2(0.21f, 0f), Vector2.right, new List<Vector2>(), room);
            Assert.GreaterOrEqual((at - room.others[0]).magnitude, W - 1e-3f, "off the person standing there");
            var edge = BodySpots.Free(new Vector2(0.55f, 0f), Vector2.right, new List<Vector2>(), room);
            Assert.IsFalse(room.Blocked(edge), "out of the water");
            Assert.Less((edge - new Vector2(0.55f, 0f)).magnitude, 0.2f, "at the shore");
        }

        [Test]
        public void Free_AHeapOfPeopleOnOneSpotFansOutIntoARing()
        {
            var taken = new List<Vector2>();
            var spots = Enumerable.Range(0, 7).Select(_ => BodySpots.Free(Vector2.zero, Vector2.down, taken)).ToList();
            Assert.GreaterOrEqual(BodySpots.Closest(spots, out _, out _), W - 1e-3f, "seven people, seven spots, a body width apart");
        }

        // ---------------------------------------------------------------- 1.2 the two marks

        [Test]
        public void B78b_TwoMarks_NobodyStandsInsideAnybody_EvenWithTheKidsStartingOnTheCouple()
        {
            var starts = new[] { Fire + Vector2.right * 0.3f, Fire + Vector2.left * 0.3f, Fire, Fire };
            var s = StoryScenes.TwoMarks(1, 2, starts[0], starts[1], new[] { 3, 4 }, new[] { starts[2], starts[3] }, Tree, 4, 0, 0f);
            Sane(s);
            Assert.GreaterOrEqual(ClosestStanding(s, starts, out string where), W - 1e-3f, where);
        }

        [Test]
        public void B78b_TwoMarks_TheKidsKeepClearOfThePeopleAlreadyAtTheTree()
        {
            var starts = new[] { Fire + Vector2.right * 0.3f, Fire + Vector2.left * 0.3f, Fire, Fire };
            var probe = StoryScenes.TwoMarks(1, 2, starts[0], starts[1], new[] { 3, 4 }, new[] { starts[2], starts[3] }, Tree, 4, 0, 0f);
            var room = new StoryRoom();
            // someone stands on each kid's spot, another on the spot the first kid walks off to
            foreach (int m in new[] { 2, 3 }) room.others.Add(probe.parts[m].First().pos + new Vector2(0.01f, 0f));
            room.others.Add(probe.parts[2].Last().pos);
            var s = StoryScenes.TwoMarks(1, 2, starts[0], starts[1], new[] { 3, 4 }, new[] { starts[2], starts[3] }, Tree, 4, 0, 0f, room);
            Sane(s);
            Assert.GreaterOrEqual(ClosestStanding(s, starts, out string where), W - 1e-3f, where);
            for (int m = 2; m < 4; m++)
                foreach (var p in s.parts[m])
                    foreach (var o in room.others) Assert.GreaterOrEqual((p.pos - o).magnitude, W - 1e-3f, $"kid {m} moved off the one standing at {o}");
        }

        [Test]
        public void B78b_TwoMarks_ForEveryTreeStage_TheCoupleAndKidsKeepABodyWidthApart()
        {
            for (int stage = 1; stage <= 4; stage++)
            {
                var starts = new[] { Fire + Vector2.right * 0.3f, Fire + Vector2.left * 0.3f, Fire + Vector2.up * 0.3f, Fire + Vector2.down * 0.3f };
                var s = StoryScenes.TwoMarks(1, 2, starts[0], starts[1], new[] { 3, 4 }, new[] { starts[2], starts[3] }, Tree, stage, 0, 0f);
                Assert.GreaterOrEqual(ClosestStanding(s, starts, out string where), W - 1e-3f, $"stage {stage}: {where}");
            }
        }

        [Test]
        public void B78b_TwoMarks_TheyStepBackApartWhicheverSideTheyCameFrom_AndMeetOnGroundTheCrowdLeavesThem()
        {
            // the Editor shot's arrangement (the couple fused at their step back when the tree side swapped them), and the mirror of it
            var tree = new Vector2(-1.2f, -0.5f);
            var first = new Vector2(-0.74f, -0.01f); var second = new Vector2(-0.27f, -0.15f);
            foreach (var (a0, b0) in new[] { (first, second), (second, first) })
            {
                var starts = new[] { a0, b0, Fire, Fire };
                var s = StoryScenes.TwoMarks(1, 2, a0, b0, new[] { 3, 4 }, new[] { Fire, Fire }, tree, 4, 0, 0f);
                Assert.GreaterOrEqual(ClosestStanding(s, starts, out string where), W - 1e-3f, where);
            }
            // a solid (the stash, a prop) where they would meet: they meet beside it, not in it
            var probe = StoryScenes.TwoMarks(1, 2, first, second, new[] { 3, 4 }, new[] { Fire, Fire }, tree, 4, 0, 0f);
            var meetAt = probe.parts[1].First().pos;
            var room = new StoryRoom { blocked = p => (p - meetAt).magnitude < 0.08f };
            var s2 = StoryScenes.TwoMarks(1, 2, first, second, new[] { 3, 4 }, new[] { Fire, Fire }, tree, 4, 0, 0f, room);
            Assert.GreaterOrEqual((s2.parts[1].First().pos - meetAt).magnitude, 0.08f - 1e-3f, "they meet off the blocked spot");
            Assert.GreaterOrEqual((s2.parts[1].First().pos - first).magnitude, W - 1e-3f, "and a body width from the one waiting");
        }

        // ---------------------------------------------------------------- 2.1 the tinkerer's bucket

        [Test]
        public void B80c_Tinkerer_TheWatchersGetSpotsOfTheirOwn_AlongTheShore_NotOnTheTinkerer()
        {
            var edge = new Vector2(0.3f, 0.1f); var toWater = Vector2.right;
            var starts = new[] { Fire + Vector2.right * 0.2f, edge, edge }; // the watchers were standing on the very spot
            var s = StoryScenes.TinkererBucket(1, starts[0], new[] { 2, 3 }, new[] { starts[1], starts[2] }, Fire, edge, toWater, Vector2.down, 0f);
            Sane(s);
            Assert.GreaterOrEqual(ClosestStanding(s, starts, out string where), W - 1e-3f, where);
        }

        [Test]
        public void B80c_Tinkerer_TheWatchersKeepClearOfWhoElseStandsAtThePond_AndOutOfTheWater()
        {
            var edge = new Vector2(0.3f, 0.1f); var toWater = Vector2.right;
            var starts = new[] { Fire + Vector2.right * 0.2f, edge + Vector2.up * 0.1f, edge + Vector2.up * 0.2f };
            var probe = StoryScenes.TinkererBucket(1, starts[0], new[] { 2, 3 }, new[] { starts[1], starts[2] }, Fire, edge, toWater, Vector2.down, 0f);
            // more people stand where the watchers would, and the water is the right of the shore line
            var room = new StoryRoom { blocked = p => p.x > edge.x + 0.05f };
            foreach (int m in new[] { 1, 2 })
                foreach (var p in probe.parts[m])
                    if (!room.others.Any(o => (o - p.pos).magnitude < 0.02f)) room.others.Add(p.pos + new Vector2(0.015f, 0f));
            var s = StoryScenes.TinkererBucket(1, starts[0], new[] { 2, 3 }, new[] { starts[1], starts[2] }, Fire, edge, toWater, Vector2.down, 0f, room);
            Sane(s);
            Assert.GreaterOrEqual(ClosestStanding(s, starts, out string where), W - 1e-3f, where);
            for (int m = 1; m < 3; m++)
                foreach (var p in s.parts[m])
                {
                    Assert.IsFalse(room.Blocked(p.pos), $"watcher {m} stands in the water at {p.pos}");
                    foreach (var o in room.others) Assert.GreaterOrEqual((p.pos - o).magnitude, W - 1e-3f, $"watcher {m} at {p.pos} is inside the one at {o}");
                }
        }
    }
}
