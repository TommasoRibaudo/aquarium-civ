// #367 Visit loop B11: the background-life director (View/StoryDirectorView.cs) starts a kids' game with the kids it picked, where
// they are: the same forced group the "Kids play" cheats make, but with its own kids, spot and length.
using System.Collections.Generic;
using AquariumCiv.Life;
using UnityEngine;

namespace AquariumCiv.View
{
    public sealed partial class KidPlayDirector
    {
        /// <summary>The kids get this long to run to the spot before a directed game starts [est].</summary>
        public const float DirectedLead = 2f;

        /// <summary>Where a splash would play near <paramref name="near"/>: the pond's real edge (no pond with water, or winter: false).</summary>
        public bool SplashSpot(Vector2 near, out Vector2 edge)
        {
            edge = default;
            var planner = pv.Planner;
            if (planner == null || !WaterSite(planner, Place.Land(PlaceKind.Pond), near, out var site)) return false;
            edge = site.edge;
            return true;
        }

        /// <summary>
        /// #367: play <paramref name="game"/> (tag, ball, the rabbit chase, a splash) with these kids for <paramref name="seconds"/>, round
        /// <paramref name="centre"/> (the rabbits for a chase, the water for a splash). They leave any group they were in. Null when it
        /// can't (no rabbit or water there, too few kids); else the end time on the director's clock (<see cref="PeopleView.PoseClock"/>).
        /// </summary>
        public float? Direct(PlayGame game, IReadOnlyList<int> who, Vector2 centre, float seconds, int seed)
        {
            var planner = pv.Planner;
            if (planner == null || who == null || who.Count == 0 || !PlayGroups.Fits(game, who.Count)) return null;
            float t = pv.PoseClock;
            PlaySite site = null;
            Vector2? herd = null;
            var spot = centre;
            switch (game)
            {
                case PlayGame.ChaseRabbits:
                {
                    var rv = RabbitView.Instance;
                    int id = rv != null ? rv.NearestShown(centre.x, centre.y) : -1;
                    if (id < 0 || !rv.TryGetPosition(id, out var rp)) return null;
                    var r = new Vector2(rp.x, rp.z);
                    pv.MeanDrawn(ToArray(who), out var kids);
                    var away = (kids - r).sqrMagnitude > 1e-4f ? (kids - r).normalized : Vector2.right;
                    centre = r + away * 0.8f; // out of their alert radius, as the cheat does
                    herd = r;
                    break;
                }
                case PlayGame.Splash:
                    if (!WaterSite(planner, Place.Land(PlaceKind.Pond), centre, out site)) return null;
                    centre = site.edge;
                    break;
                case PlayGame.Ball:
                case PlayGame.Tag:
                    centre = Open(centre, spot);
                    break;
                default:
                    return null;
            }
            var list = new List<int>(who);
            foreach (int k in list) if (byKey.TryGetValue(k, out var gi)) { int ix = groups.IndexOf(gi.g); if (ix >= 0) Remove(ix); }
            var g = new Group
            {
                place = planner.GatherPlace, forced = true, until = t + DirectedLead + seconds, site = site,
                game = KidGame.Create(game, seed & 0x7fffffff, t + DirectedLead, list, centre, site),
            };
            if (g.game == null) return null;
            if (g.game is RabbitChaseGame chase) { var c = centre; chase.HerdAt = cyc => cyc == 0 ? herd : HerdNear(pv.Planner, pv.Now, c); }
            Add(g);
            return g.until;
        }

        /// <summary>The game a person plays now (null: none), for the director's count of stories in view.</summary>
        public object GameOf(int key, out PlayGame game)
        {
            game = PlayGame.None;
            if (!TryCue(key, now, out _, out var kg, out _) || kg == null) return null;
            game = kg.game;
            return kg;
        }

        static int[] ToArray(IReadOnlyList<int> l) { var a = new int[l.Count]; for (int i = 0; i < a.Length; i++) a[i] = l[i]; return a; }
    }
}
