// #358 Kids' play 1 (docs/design/animation-specs.md B2): the play groups in the box. PeopleView owns one; every frame it gathers the
// kids playing at each place (and the dancers at a wedding or the festival), splits them into groups with the planner side's
// PlayGroups (a game each, the neighbour's never shared, 1 u apart), and runs each group's game (View/Beats/KidPlay.cs): a member's
// cue gives PeopleView.Drive the spot, the facing, the move and the pose; the director draws each ball game's ball. The debug panel's
// "Kids play" cheats force a game at the gathering place. B3 (#359): the water, the woods, hide and seek and snowballs, in KidPlayDirector.Sites.cs.
using System.Collections.Generic;
using AquariumCiv.Life;
using AquariumCiv.Sim.Settle;
using UnityEngine;

namespace AquariumCiv.View
{
    public sealed partial class KidPlayDirector
    {
        /// <summary>A place's kids must stay the same this long before their groups are split again (arrivals come in one by one).</summary>
        public const float Settle = 1.2f;
        /// <summary>How long a cheat's game lasts, and how long its kids get to run there before it starts.</summary>
        public const float ForcedSeconds = 45f, ForcedLead = 3f;
        /// <summary>Rabbits this near a group's spot can be chased (B2.c).</summary>
        public const float RabbitReach = 1.2f;

        sealed class Group
        {
            public KidGame game;
            public Place place;
            public bool forced;
            public float until = float.MaxValue;
            public GameObject ball;
            public int ballShot = -1;
            public ArcShot flight;
            public Vector3 lastBall;
            public float lastT = float.NegativeInfinity;
            public PlaySite site;              // #359: the real water, tree or cover a B3 game plays with
            public float fxT = float.NaN;      // #359: its puffs and shakes are fired up to here
        }

        readonly PeopleView pv;
        readonly List<Group> groups = new();
        readonly Dictionary<int, (Group g, int i)> byKey = new();
        readonly HashSet<int> candidates = new();
        readonly Dictionary<Place, List<int>> atPlace = new();
        readonly Dictionary<Place, long> sigOf = new();
        readonly Dictionary<Place, (long sig, float since)> pending = new();
        readonly Dictionary<Place, float> noRabbitsUntil = new();
        readonly List<PlayGroupPlan> plans = new();
        readonly List<Place> placesBuf = new();
        float now;

        public KidPlayDirector(PeopleView pv) { this.pv = pv; }

        public int GroupCount => groups.Count;
        /// <summary>Tests and the panel: the games playing now.</summary>
        public IEnumerable<PlayGame> Games { get { foreach (var g in groups) yield return g.game.game; } }

        /// <summary>The cue for a person this frame, if a play group has them (the game, their index in it).</summary>
        public bool TryCue(int key, float time, out PlayCue cue, out KidGame game, out int index)
        {
            cue = default; game = null; index = -1;
            if (!byKey.TryGetValue(key, out var gi)) return false;
            if (!gi.g.forced && !candidates.Contains(key)) return false; // left the play (a walk home): the planner has them again
            game = gi.g.game; index = gi.i;
            cue = game.Cue(index, time);
            return true;
        }

        public bool IsForced(int key) => byKey.TryGetValue(key, out var gi) && gi.g.forced;

        /// <summary>
        /// Once a frame, before the dolls are driven: who plays where, and the groups (re-split when a place's kids have changed and
        /// stayed changed for <see cref="Settle"/> s, or a 2-hour block turned).
        /// </summary>
        public void Step(LifePlanner planner, IReadOnlyList<PersonActivity> acts, ViewTime viewNow, float time, Transform root)
        {
            now = time;
            candidates.Clear();
            foreach (var l in atPlace.Values) l.Clear();
            for (int i = groups.Count - 1; i >= 0; i--)
                if (groups[i].forced && time > groups[i].until) Remove(i);
            for (int i = 0; i < acts.Count; i++)
            {
                var a = acts[i];
                bool two = twoUntil > time && twoAt.ContainsKey(a.who.Key); // #541 cheat: this kid is one of the two
                if (!two && !IsCandidate(planner, a)) continue;
                if (IsForced(a.who.Key)) continue;
                candidates.Add(a.who.Key);
                var key = two ? twoAt[a.who.Key] : DanceKey(a);
                if (!atPlace.TryGetValue(key, out var list)) atPlace[key] = list = new List<int>();
                list.Add(a.who.Key);
            }
            GatherLoneKids(planner); // #541
            int block = Mathf.FloorToInt(viewNow.hour / PlayGroups.BlockHours);
            placesBuf.Clear();
            foreach (var kv in atPlace) placesBuf.Add(kv.Key);
            foreach (var g in groups) if (!g.forced && !atPlace.ContainsKey(g.place)) placesBuf.Add(g.place);
            foreach (var place in placesBuf)
            {
                atPlace.TryGetValue(place, out var kids);
                int n = kids?.Count ?? 0;
                long sig = Sig(kids, block, viewNow.day);
                if (n == 0) { RemoveAt(place); sigOf.Remove(place); pending.Remove(place); continue; }
                bool lost = ChaseLostItsRabbits(place, time);
                if (sigOf.TryGetValue(place, out long have) && have == sig && !lost) { pending.Remove(place); continue; }
                if (sigOf.ContainsKey(place) && !lost)
                {
                    // The kids here changed: wait until they stay changed (arrivals come one by one), the old groups play on meanwhile.
                    if (!pending.TryGetValue(place, out var p) || p.sig != sig) { pending[place] = (sig, time); continue; }
                    if (time - p.since < Settle) continue;
                }
                pending.Remove(place);
                sigOf[place] = sig;
                Split(planner, place, kids, viewNow, time, root);
            }
            foreach (var g in groups) for (int i = 0; i < g.game.Count; i++) if (pv.ScaleOf(g.game.KeyOf(i), out float s)) g.game.scales[i] = s;
            FireEffects(root, time); // #359
        }

        readonly List<PlayGroups.KidSpot> spotsBuf = new();
        readonly List<Place> spotPlaces = new();

        /// <summary>#541: a lone free kid joins the nearest other kids' group (PlayGroups.Gather) instead of playing a solo copy beside them.</summary>
        void GatherLoneKids(LifePlanner planner)
        {
            spotsBuf.Clear(); spotPlaces.Clear();
            foreach (var kv in atPlace)
            {
                if (kv.Value.Count == 0 || IsDance(kv.Key)) continue;
                var real = RealPlace(kv.Key);
                if (!planner.Positions.TryGetPosition(real, out float px, out float pz)) continue;
                spotsBuf.Add(new PlayGroups.KidSpot(real.GetHashCode(), px, pz, kv.Value.Count, real.kind == PlaceKind.Home ? 1 : 0));
                spotPlaces.Add(kv.Key);
            }
            if (spotsBuf.Count < 2) return;
            var host = PlayGroups.Gather(spotsBuf);
            for (int i = 0; i < host.Length; i++)
            {
                if (host[i] == i) continue;
                var from = atPlace[spotPlaces[i]]; var to = atPlace[spotPlaces[host[i]]];
                to.AddRange(from); from.Clear();
            }
        }

        /// <summary>#541: the games the other groups in view play now (a group here avoids them: two copies of one game look like a glitch).</summary>
        void GamesElsewhere(Place place, List<PlayGame> into)
        {
            into.Clear();
            foreach (var g in groups) if (g.place != place) into.Add(g.game.game);
        }

        readonly List<PlayGame> inViewBuf = new();

        static bool IsCandidate(LifePlanner planner, in PersonActivity a)
        {
            if (a.moving || a.enRoute || a.hunt != HuntPhase.None || a.tent != TentPhase.None) return false;
            if (a.kind is ActivityKind.Play or ActivityKind.Swim or ActivityKind.Snowball) // #359: the water (Swim) and the snow are play groups too
            {
                if (planner.StageOf(a.who) != LifeStage.Kid) return false;
                // The hill is a run up and down, as before.
                return a.place.kind is not (PlaceKind.Stream or PlaceKind.Hill or PlaceKind.Glass);
            }
            return a.kind == ActivityKind.Dance && !a.facesGlass && planner.StageOf(a.who) is LifeStage.Kid or LifeStage.Adult or LifeStage.Elder;
        }

        /// <summary>Dancers ring up apart from the kids playing at the same place.</summary>
        static Place DanceKey(in PersonActivity a) => a.kind == ActivityKind.Dance ? new Place(a.place.kind, a.place.buildingId, 1000 + a.place.index) : a.place;
        static bool IsDance(Place p) => p.index >= 999;
        static Place RealPlace(Place p) => IsDance(p) ? new Place(p.kind, p.buildingId, p.index - 1000) : p;

        static long Sig(List<int> kids, int block, long day)
        {
            unchecked
            {
                long h = block * 7919L + day * 104729L;
                if (kids != null) { kids.Sort(); foreach (int k in kids) h = h * 1000003L + k; }
                return h;
            }
        }

        bool ChaseLostItsRabbits(Place place, float time)
        {
            foreach (var g in groups)
                if (!g.forced && g.place == place && g.game is RabbitChaseGame c && c.NoHerd(time))
                {
                    noRabbitsUntil[place] = time + 30f;
                    return true;
                }
            return false;
        }

        void Split(LifePlanner planner, Place place, List<int> kids, ViewTime viewNow, float time, Transform root)
        {
            RemoveAt(place);
            var real = RealPlace(place);
            if (!planner.Positions.TryGetPosition(real, out float px, out float pz)) return;
            if (IsDance(place))
            {
                if (kids.Count < 3) return; // two dancers: their partner dance (the Hop fallback) until more come
                var sorted = new List<int>(kids); sorted.Sort();
                Add(new Group { place = place, game = KidGame.Create(PlayGame.DanceRing, PlayGroups.Seed(real.GetHashCode(), viewNow.day, viewNow.hour, 9), time, sorted, Open(new Vector2(px, pz), new Vector2(px, pz))) });
                return;
            }
            bool rabbitsOk = !noRabbitsUntil.TryGetValue(place, out float until) || time > until;
            bool roomy = real.kind != PlaceKind.Home; // by a home: a ball in the yard, where the kids stand (tag would run through the houses)
            bool snow = planner.WeatherOn(viewNow.day) == Weather.Snow; // #359
            PlayGroups.Split(kids, real.GetHashCode(), viewNow.day, viewNow.hour, px, pz,
                (x, z) => SitesAt(planner, viewNow, real, new Vector2(x, z), rabbitsOk && roomy, roomy, snow), // #359: what each spot offers
                (x, z) => { var o = Open(new Vector2(x, z), new Vector2(px, pz)); return (o.x, o.y); }, plans, roomy);
            usedTrees.Clear();
            foreach (var p in plans)
            {
                var centre = new Vector2(p.x, p.z);
                if (!roomy && pv.MeanDrawn(p.members, out var mean)) centre = mean;
                var game = p.game;
                if (game is PlayGame.Tag or PlayGame.Ball) { GamesElsewhere(place, inViewBuf); game = PlayGroups.Differ(game, p.members.Length, inViewBuf, roomy); } // #541
                var site = SiteFor(planner, game, real, ref centre, viewNow); // #359
                if (site == null && game is PlayGame.Splash or PlayGame.Woods or PlayGame.HideSeek) game = PlayGroups.Fits(PlayGame.Tag, p.members.Length) ? PlayGame.Tag : PlayGame.Ball;
                var g = MakeGroup(planner, viewNow, place, game, p.seed, p.members, centre, time, site);
                Add(g);
            }
        }

        Group MakeGroup(LifePlanner planner, ViewTime viewNow, Place place, PlayGame game, int seed, IReadOnlyList<int> members, Vector2 centre, float t0, PlaySite site = null)
        {
            var g = new Group { place = place, site = site, game = KidGame.Create(game, seed, t0, members, centre, site) };
            if (g.game is RabbitChaseGame chase) chase.HerdAt = _ => HerdNear(pv.Planner, pv.Now, centre);
            return g;
        }

        /// <summary>The nearest rabbit to a spot within <see cref="RabbitReach"/>, not one a hunt is after, and no hunt on near its hour (B2.c).</summary>
        static Vector2? HerdNear(LifePlanner planner, ViewTime viewNow, Vector2 at)
        {
            var rv = RabbitView.Instance;
            if (rv == null || planner == null) return null;
            int id = rv.NearestShown(at.x, at.y);
            if (id < 0 || !rv.TryGetPosition(id, out var p)) return null;
            var pos = new Vector2(p.x, p.z);
            if ((pos - at).sqrMagnitude > RabbitReach * RabbitReach) return null;
            var hunts = planner.HuntsOn(viewNow.day);
            if (hunts != null)
                foreach (var h in hunts)
                {
                    if (h.rabbitId == id) return null;
                    if (viewNow.hour > h.start - 0.5f && viewNow.hour < h.resolveHour + 0.5f) return null;
                }
            return pos;
        }

        /// <summary>A group's spot moved onto dry, open ground inside the box (toward the place's own spot when it's wet).</summary>
        Vector2 Open(Vector2 p, Vector2 home)
        {
            var land = pv.LandForPlay;
            for (int k = 0; k < 6; k++)
            {
                p.x = Mathf.Clamp(p.x, -Land.HX + 0.45f, Land.HX - 0.45f);
                p.y = Mathf.Clamp(p.y, -Land.HZ + 0.45f, Land.HZ - 0.45f);
                if (land == null || !land.IsWater(p.x, p.y)) break;
                p = Vector2.Lerp(p, home, 0.4f);
            }
            // Batch 2: open ground, not among the houses: the nearest centre whose play ring (PlayRingR) keeps off the lots.
            if (pv.LotHitsNear(p, PlayRingR) == 0) return p;
            Vector2 best = p; int bestHits = int.MaxValue;
            for (int ring = 1; ring <= 5 && bestHits > 0; ring++)
                for (int k = 0; k < 12; k++)
                {
                    float a = k * Mathf.PI / 6f;
                    var q = p + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (0.2f * ring);
                    if (Mathf.Abs(q.x) > Land.HX - 0.45f || Mathf.Abs(q.y) > Land.HZ - 0.45f || (land != null && land.IsWater(q.x, q.y))) continue;
                    int h = pv.LotHitsNear(q, PlayRingR);
                    if (h < bestHits) { bestHits = h; best = q; }
                }
            return bestHits <= pv.LotHitsNear(p, PlayRingR) ? best : p;
        }

        /// <summary>Batch 2: the ground a play group's game covers round its centre, kept off the lots by <see cref="Open"/>.</summary>
        const float PlayRingR = 0.5f;

        void Add(Group g)
        {
            if (g.game == null) return;
            groups.Add(g);
            for (int i = 0; i < g.game.Count; i++)
            {
                int k = g.game.KeyOf(i);
                if (byKey.TryGetValue(k, out var old) && old.g != g) RemoveMember(old.g, k);
                byKey[k] = (g, i);
            }
        }

        void RemoveMember(Group g, int key) { if (byKey.TryGetValue(key, out var gi) && gi.g == g) byKey.Remove(key); }

        void RemoveAt(Place place)
        {
            for (int i = groups.Count - 1; i >= 0; i--) if (!groups[i].forced && groups[i].place == place) Remove(i);
        }

        void Remove(int i)
        {
            var g = groups[i];
            for (int k = 0; k < g.game.Count; k++) RemoveMember(g, g.game.KeyOf(k));
            if (g.ball != null) Kill(g.ball);
            groups.RemoveAt(i);
        }

        public void Clear() { for (int i = groups.Count - 1; i >= 0; i--) Remove(i); sigOf.Clear(); pending.Clear(); byKey.Clear(); }

        static void Kill(Object o) { if (o == null) return; if (Application.isPlaying) Object.Destroy(o); else Object.DestroyImmediate(o); }

        // ---------------------------------------------------------------- the ball (B2.b)

        /// <summary>Batch 2 (#358 look): the ball is never drawn smaller than this across, in screen pixels (it was ~4 px at the full-box view).</summary>
        public static float BallMinPixels = 9f;

        /// <summary>The ball's radius (root-local units) that makes it <see cref="BallMinPixels"/> across on the main camera at <paramref name="local"/>.</summary>
        static float MinBallRadius(Transform root, Vector3 local)
        {
            var cam = Camera.main;
            if (cam == null || cam.pixelHeight <= 0) return 0f;
            var world = root.TransformPoint(local);
            float depth = Vector3.Dot(world - cam.transform.position, cam.transform.forward);
            if (depth <= 0f) return 0f;
            float perPx = cam.orthographic ? 2f * cam.orthographicSize / cam.pixelHeight : 2f * depth * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) / cam.pixelHeight;
            float s = Mathf.Max(1e-5f, root.lossyScale.x);
            return 0.5f * BallMinPixels * perPx / s;
        }

        /// <summary>After the dolls are placed: each ball game's ball in its holder's hands, or on its arc (from where the hands let it go).</summary>
        public void DrawBalls(Transform root, Material material, float zoom)
        {
            DrawSnowballs(root, material, zoom); // #359
            foreach (var g in groups)
            {
                if (!g.game.HasBall || g.game is SnowballGame) continue;
                if (g.ball == null)
                {
                    g.ball = new GameObject("Play Ball") { hideFlags = HideFlags.DontSave };
                    g.ball.transform.SetParent(root, false);
                    g.ball.AddComponent<MeshFilter>().sharedMesh = PegDoll.PropMesh(PropKind.Ball);
                    g.ball.AddComponent<MeshRenderer>().sharedMaterial = material;
                }
                var b = g.game.Ball(now);
                Vector3 pos;
                if (!b.Free)
                {
                    var v = pv.ViewOf(g.game.KeyOf(b.holder));
                    if (v == null || !v.gameObject.activeInHierarchy) { g.ball.SetActive(false); continue; }
                    var hands = (v.armL.TransformPoint(new Vector3(0f, -PegDoll.ArmLen * v.scale, 0f)) + v.armR.TransformPoint(new Vector3(0f, -PegDoll.ArmLen * v.scale, 0f))) * 0.5f;
                    pos = root.InverseTransformPoint(hands);
                }
                else
                {
                    if (b.shotIndex != g.ballShot)
                    {
                        // It leaves the hands where they were last frame; the arc is above the ground under it.
                        g.ballShot = b.shotIndex;
                        g.flight = b.shot;
                        g.flight.start = new Vector3(g.lastBall.x, g.lastBall.y - pv.GroundAt(g.lastBall.x, g.lastBall.z), g.lastBall.z);
                    }
                    var s = Arc.Sample(g.flight, now - g.game.t0);
                    float ground = pv.GroundAt(s.position.x, s.position.z);
                    pos = new Vector3(s.position.x, ground + Mathf.Max(s.position.y, BallGame.BallR * 0.8f), s.position.z);
                    if (g.flight.landing == Landing.BounceRoll && Arc.LandedBetween(g.flight, g.lastT - g.game.t0, now - g.game.t0))
                        HitFx.Puff(PuffKind.Dust, root.TransformPoint(new Vector3(pos.x, ground, pos.z)), Vector3.up, 0.7f); // B2.b: a small dust where it lands
                }
                g.ball.SetActive(true);
                g.ball.transform.localPosition = pos;
                g.ball.transform.localRotation = b.Free ? b.rotation : Quaternion.identity;
                g.ball.transform.localScale = Vector3.one * (Mathf.Max(zoom * BallGame.BallR, MinBallRadius(root, pos)) / PegDoll.BallMeshR);
                g.lastBall = pos;
                g.lastT = now;
            }
        }

        // ---------------------------------------------------------------- cheats (#358)

        /// <summary>The panel's "Kids play: tag / ball / chase rabbits", "Toddle" and "Dance ring now": gathers people at the gathering place
        /// and plays the game there for <see cref="ForcedSeconds"/>. Returns a status line.</summary>
        public string Force(PlayGame game)
        {
            var planner = pv.Planner;
            if (planner == null) return "no planner";
            if (game is PlayGame.Splash or PlayGame.Woods or PlayGame.HideSeek) return ForceB3(game); // #359
            var gather = planner.GatherPlace;
            if (!planner.Positions.TryGetPosition(gather, out float gx, out float gz)) return "no gathering place";
            var spot = new Vector2(gx, gz);
            var centre = Open(spot + new Vector2(0.6f, -0.2f), spot);
            Vector2? herd = null;
            if (game == PlayGame.ChaseRabbits)
            {
                var rv = RabbitView.Instance;
                int id = rv != null ? rv.NearestShown(gx, gz) : -1;
                if (id < 0 || !rv.TryGetPosition(id, out var rp)) return "no rabbits shown: 'Spawn rabbits here' first";
                var r = new Vector2(rp.x, rp.z);
                var away = (spot - r).sqrMagnitude > 1e-4f ? (spot - r).normalized : Vector2.right;
                centre = r + away * 0.8f; // the kids gather near the rabbits, out of their alert radius
                herd = r;
            }
            int want = game switch { PlayGame.Tag => 4, PlayGame.Ball => 3, PlayGame.ChaseRabbits => 3, PlayGame.Toddle => 2, PlayGame.Snowball => 3, _ => 7 };
            var who = new List<int>();
            if (game == PlayGame.Toddle)
            {
                int adult = pv.NearestShown(spot, k => planner.StageOf(k) == LifeStage.Adult, who);
                if (adult < 0) return "no grown-up on view";
                who.Add(adult);
                int kid = pv.NearestShown(spot, k => planner.StageOf(k) == LifeStage.Kid, who);
                if (kid < 0) return "no kid on view";
                who.Add(kid);
            }
            else
            {
                bool any = game == PlayGame.DanceRing;
                while (who.Count < want)
                {
                    int k = pv.NearestShown(spot, x => any ? planner.StageOf(x) is LifeStage.Kid or LifeStage.Adult or LifeStage.Elder : planner.StageOf(x) == LifeStage.Kid, who);
                    if (k < 0) break;
                    who.Add(k);
                }
                int least = game == PlayGame.DanceRing ? 3 : game == PlayGame.Tag ? 2 : 1;
                if (who.Count < least) return $"only {who.Count} on view for {game}";
            }
            // Out of whatever group they were in.
            foreach (int k in who) if (byKey.TryGetValue(k, out var gi)) { int ix = groups.IndexOf(gi.g); if (ix >= 0) Remove(ix); }
            var g = new Group
            {
                place = gather, forced = true, until = now + ForcedLead + ForcedSeconds,
                game = KidGame.Create(game, (int)PlayGroups.Hash(who[0], (long)(now * 10f), 358) & 0x7fffffff, now + ForcedLead, who,
                    game == PlayGame.DanceRing || game == PlayGame.Toddle ? Open(spot + new Vector2(0.5f, 0.3f), spot) : centre),
            };
            if (g.game is RabbitChaseGame chase) { var c = centre; chase.HerdAt = cyc => cyc == 0 ? herd : HerdNear(pv.Planner, pv.Now, c); }
            Add(g);
            return $"{game}: {who.Count} at the gathering place for {ForcedSeconds:0} s";
        }

        // fb4 #541 cheat: "two kids play": two free kids at once, each at its own place (the camp's fire and its pile), through the real
        // grouping path: they must end up in ONE together game, never two solo copies.
        readonly Dictionary<int, Place> twoAt = new();
        float twoUntil;

        public string ForceTwo()
        {
            var planner = pv.Planner;
            if (planner == null) return "no planner";
            var a = planner.GatherPlace; var b = new Place(PlaceKind.Stash, -1, 1);
            if (!planner.Positions.TryGetPosition(a, out float ax, out float az) || !planner.Positions.TryGetPosition(b, out _, out _)) return "no gathering place or pile";
            var spot = new Vector2(ax, az);
            var who = new List<int>();
            for (int n = 0; n < 2; n++)
            {
                int k = pv.NearestShown(spot, x => planner.StageOf(x) == LifeStage.Kid, who);
                if (k < 0) return $"only {who.Count} kid(s) on view";
                who.Add(k);
            }
            foreach (int k in who) if (byKey.TryGetValue(k, out var gi)) { int ix = groups.IndexOf(gi.g); if (ix >= 0) Remove(ix); }
            twoAt.Clear(); twoAt[who[0]] = a; twoAt[who[1]] = b; twoUntil = now + ForcedSeconds;
            return "two kids play: 2 free kids at once, 45 s";
        }

        /// <summary>The game(s) the kids in <paramref name="keys"/> are in now, one entry per distinct group (tests: two kids must share one).</summary>
        public int GroupsOf(IEnumerable<int> keys)
        {
            var seen = new HashSet<object>();
            foreach (int k in keys) if (byKey.TryGetValue(k, out var gi) && (gi.g.forced || candidates.Contains(k))) seen.Add(gi.g);
            return seen.Count;
        }

        public static string DebugTwo()
        {
            var pv = PeopleView.Find();
            return pv == null ? "no people view" : pv.KidPlay.ForceTwo();
        }

        public static string Debug(PlayGame game)
        {
            var pv = PeopleView.Find();
            return pv == null ? "no people view" : pv.KidPlay.Force(game);
        }

        public string Status()
        {
            if (groups.Count == 0) return "kids' play: no groups";
            var sb = new System.Text.StringBuilder("kids' play:");
            foreach (var g in groups) sb.Append(' ').Append(g.game.game).Append('×').Append(g.game.Count).Append(g.forced ? "(cheat)" : "");
            return sb.ToString();
        }
    }
}
