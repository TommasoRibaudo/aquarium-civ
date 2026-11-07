// #359 Kids' play 2 (docs/design/animation-specs.md B3): the director's side of the splash, the woods games, hide and seek and the
// snowball fight. It finds the real things each game plays with in the land (the pond's real edge or the puddle, a real grown tree from
// BoxView.TreeSpots, the bushes from BoxView.BushSpots, the tents, the stash pile), tells PlayGroups.Pick what a spot offers, spawns
// each game's puffs and shakes through HitFx when its clock passes them, draws the snowballs, and runs the panel's #359 cheats.
using System.Collections.Generic;
using AquariumCiv.Life;
using AquariumCiv.Sim.Settle;
using UnityEngine;

namespace AquariumCiv.View
{
    public sealed partial class KidPlayDirector
    {
        /// <summary>A grown tree this near a group's spot is a woods game's (the kids run over to it); cover this near the base hides a kid.</summary>
        public const float WoodsReach = 1.6f, CoverReach = HideSeekGame.Reach;

        readonly List<PlayFx> fxBuf = new();
        readonly List<Vector2> usedTrees = new();

        /// <summary>Tests and the panel: puffs and shakes the B3 games asked for so far.</summary>
        public int FxFired { get; private set; }

        BoxView BoxNow => pv.BoxForPlay;

        /// <summary>The pond's surface (box y) when it has water: the eco pond's level, or the fixed one without the ecology layer.</summary>
        public bool PondLevel(out float level)
        {
            level = Land.WaterY;
            var box = BoxNow;
            if (box == null) return false;
            var eco = box.layout.eco;
            if (!eco.on) return true;
            level = eco.waterY;
            return eco.pondCells > 0;
        }

        static bool IsWaterPlace(PlaceKind k) => k is PlaceKind.Pond or PlaceKind.Puddle or PlaceKind.Nudge;

        /// <summary>What a spot offers the groups at a place (PlayGroups.Pick).</summary>
        PlaySites SitesAt(LifePlanner planner, ViewTime viewNow, Place real, Vector2 at, bool rabbitsOk, bool roomy, bool snow)
        {
            var s = PlaySites.None;
            if (rabbitsOk && HerdNear(planner, viewNow, at).HasValue) s |= PlaySites.Rabbits;
            if (IsWaterPlace(real.kind) && WaterSite(planner, real, at, out _)) s |= PlaySites.Water;
            if (!roomy) return s;
            if (snow) s |= PlaySites.Snow;
            if (NearestTree(at, WoodsReach, null, out _, out _)) s |= PlaySites.Tree;
            var hs = HideSite(planner, at);
            if (HideSeekGame.Enough(hs, 3)) s |= PlaySites.Cover;
            return s;
        }

        /// <summary>The site a game plays on (null for the B2 games), and where its group stands.</summary>
        PlaySite SiteFor(LifePlanner planner, PlayGame game, Place real, ref Vector2 centre, ViewTime viewNow)
        {
            var season = LifeClock.SeasonOf(viewNow.day);
            switch (game)
            {
                case PlayGame.Splash:
                    return WaterSite(planner, real, centre, out var w) ? w : null;
                case PlayGame.Woods:
                {
                    if (!NearestTree(centre, WoodsReach, usedTrees, out var trunk, out _)) return null;
                    usedTrees.Add(trunk);
                    centre = trunk;
                    return new PlaySite { tree = true, trunk = trunk, autumn = season == Season.Autumn, snow = season == Season.Winter };
                }
                case PlayGame.HideSeek:
                    return HideSite(planner, centre);
                default:
                    return null;
            }
        }

        /// <summary>
        /// The water a splash plays at: a puddle (its middle and size), or the pond's real edge (B4's way: from the pond's middle out towards
        /// the group's spot to the first dry point, with the eco pond's own level) and the way to dry land. False: no water there now
        /// (a dry pond in the mist years, a puddle gone, winter).
        /// </summary>
        bool WaterSite(LifePlanner planner, Place real, Vector2 at, out PlaySite site)
        {
            site = null;
            var land = pv.LandForPlay;
            if (land == null) return false;
            if (pv.Now.day >= 0 && LifeClock.SeasonOf(pv.Now.day) == Season.Winter) return false;
            if (real.kind == PlaceKind.Pond)
            {
                if (!PondLevel(out float level)) return false;
                var dir = at - land.pond;
                if (dir.sqrMagnitude < 1e-4f) dir = land.centre - land.pond;
                if (dir.sqrMagnitude < 1e-6f) dir = Vector2.down;
                dir.Normalize();
                var edge = land.pond;
                int i = 0;
                for (; i < 200 && land.Height(edge.x, edge.y) < level; i++) edge += dir * 0.02f;
                if (i == 0) return false; // the middle isn't water: no pond there
                site = new PlaySite { water = true, edge = edge, toLand = dir };
                return true;
            }
            // A puddle: a mist puddle's cell, or the Keeper's rain puddle (the nudge spot).
            if (!planner.Positions.TryGetPosition(real, out float px, out float pz)) return false;
            float r = 0.08f;
            var surf = EcoSurface.Instance;
            if (real.kind == PlaceKind.Puddle && surf != null)
            {
                float shown = surf.PuddleShown(real.index);
                if (shown < EcoSurface.MinPuddle) return false;
                r = 0.03f + 0.044f * Mathf.Sqrt(Mathf.Min(1.6f, shown));
                var pp = surf.PuddlePoint(real.index);
                px = pp.x; pz = pp.z;
            }
            var along = at - new Vector2(px, pz);
            if (along.sqrMagnitude < 1e-4f) along = Vector2.right;
            site = new PlaySite { water = true, puddle = true, mud = true, edge = new Vector2(px, pz), toLand = new Vector2(-along.y, along.x).normalized, puddleR = r };
            return true;
        }

        /// <summary>The nearest real grown tree (BoxView.TreeSpots, still grown) within <paramref name="reach"/>, not one in <paramref name="not"/>.</summary>
        bool NearestTree(Vector2 at, float reach, List<Vector2> not, out Vector2 trunk, out float crown)
        {
            trunk = default; crown = 0f;
            var box = BoxNow;
            if (box == null || !box.layout.eco.on) return false;
            float best = reach * reach;
            bool found = false;
            var eco = box.layout.eco;
            lock (box.TreeSpots)
                foreach (var kv in box.TreeSpots)
                {
                    if (kv.Key != eco.treeCell && eco.At(kv.Key) != EcoLayer.Tree) continue; // felled or burnt since it was drawn
                    if (kv.Key == eco.treeCell && eco.treeStage < 4) continue;
                    var p = new Vector2(kv.Value.pos.x, kv.Value.pos.z);
                    if (!InsideBox(p, 0.3f)) continue;
                    bool taken = false;
                    if (not != null) foreach (var u in not) if ((u - p).sqrMagnitude < 0.01f) taken = true;
                    if (taken) continue;
                    float d = (p - at).sqrMagnitude;
                    if (d < best) { best = d; trunk = p; crown = kv.Value.r; found = true; }
                }
            return found;
        }

        static bool InsideBox(Vector2 p, float margin) => Mathf.Abs(p.x) <= Land.HX - margin && Mathf.Abs(p.y) <= Land.HZ - margin;

        /// <summary>
        /// Hide and seek's site near a spot: the base (the nearest tent to it within 1.2 u, else the nearest grown tree, else the stash
        /// pile) and the cover round it (bushes, grown trees and the other tents, never in the water).
        /// </summary>
        PlaySite HideSite(LifePlanner planner, Vector2 at)
        {
            var site = new PlaySite();
            var box = BoxNow;
            var tents = new List<PlayCover>();
            var sim = AquariumCiv.Game.GameClock.Instance != null ? AquariumCiv.Game.GameClock.Instance.Sim : null;
            if (sim != null)
                foreach (var b in sim.State.buildings)
                    if (b != null && b.lotId != 0 && b.type == AquariumCiv.Sim.BuildingType.Hut)
                        tents.Add(new PlayCover(new Vector2(b.x, b.z), Mathf.Max(0.08f, Mathf.Min(b.w, b.d) * 0.4f), CoverKind.Tent));
            float best = 1.2f * 1.2f;
            foreach (var t in tents) { float d = (t.pos - at).sqrMagnitude; if (d < best) { best = d; site.home = t; site.hasBase = true; } }
            if (!site.hasBase && NearestTree(at, 1.2f, null, out var trunk, out _)) { site.home = new PlayCover(trunk, 0.05f, CoverKind.Tree); site.hasBase = true; }
            if (!site.hasBase && planner != null && planner.Positions.TryGetPosition(planner.GatherPlace, out float gx, out float gz) && (new Vector2(gx, gz) - at).magnitude < 1.4f)
            { site.home = new PlayCover(new Vector2(gx, gz), 0.1f, CoverKind.Pile); site.hasBase = true; }
            if (!site.hasBase) return site;
            var land = pv.LandForPlay;
            void Add(PlayCover c)
            {
                if ((c.pos - site.home.pos).sqrMagnitude < 0.0025f) return; // the base itself
                if (!InsideBox(c.pos, 0.2f) || (land != null && land.IsWater(c.pos.x, c.pos.y))) return;
                site.covers.Add(c);
            }
            foreach (var t in tents) Add(t);
            if (box != null)
            {
                var eco = box.layout.eco;
                lock (box.BushSpots) foreach (var kv in box.BushSpots) if (!eco.on || eco.At(kv.Key) == EcoLayer.Bush) Add(new PlayCover(new Vector2(kv.Value.pos.x, kv.Value.pos.z), kv.Value.r, CoverKind.Bush));
                lock (box.TreeSpots) foreach (var kv in box.TreeSpots) if (eco.At(kv.Key) == EcoLayer.Tree) Add(new PlayCover(new Vector2(kv.Value.pos.x, kv.Value.pos.z), 0.04f, CoverKind.Tree));
            }
            return site;
        }

        // ---------------------------------------------------------------- puffs and shakes

        /// <summary>Each B3 game's puffs and shakes that came due since the last frame, through HitFx (in the box's space under <paramref name="root"/>).</summary>
        void FireEffects(Transform root, float time)
        {
            if (root == null) return;
            foreach (var g in groups)
            {
                if (float.IsNaN(g.fxT)) { g.fxT = time; continue; }
                if (time <= g.fxT) continue;
                fxBuf.Clear();
                g.game.Effects(g.fxT, time, fxBuf);
                g.fxT = time;
                foreach (var fx in fxBuf) { Fire(root, g, fx); FxFired++; }
            }
        }

        float SurfaceAt(Group g, float x, float z, bool wet)
        {
            float ground = pv.GroundAt(x, z);
            if (!wet) return ground;
            if (g.site != null && g.site.puddle) return ground + 0.003f;
            return PondLevel(out float level) ? Mathf.Max(ground, level) : ground;
        }

        void Fire(Transform root, Group g, in PlayFx fx)
        {
            if (!HitFx.Enabled) return;
            float y0 = SurfaceAt(g, fx.pos.x, fx.pos.z, fx.wet);
            var world = root.TransformPoint(new Vector3(fx.pos.x, y0 + fx.pos.y, fx.pos.z));
            var o = PuffOptions.Default;
            o.groundY = root.TransformPoint(new Vector3(fx.pos.x, y0, fx.pos.z)).y;
            o.mud = fx.mud;
            o.count = fx.count;
            float scale = fx.scale > 0f ? fx.scale : 1f;
            switch (fx.kind)
            {
                case PlayFxKind.Droplets:
                    if (fx.aimed)
                    {
                        o.hasAim = true;
                        o.aim = root.TransformPoint(new Vector3(fx.aim.x, SurfaceAt(g, fx.aim.x, fx.aim.z, fx.wet) + fx.aim.y, fx.aim.z));
                        o.groundY = Mathf.Min(o.groundY, root.TransformPoint(new Vector3(fx.aim.x, SurfaceAt(g, fx.aim.x, fx.aim.z, true), fx.aim.z)).y);
                    }
                    HitFx.Puff(PuffKind.Droplets, world, Vector3.up, scale, o);
                    break;
                case PlayFxKind.Ring: HitFx.Puff(PuffKind.Ring, world, Vector3.up, scale, o); break;
                case PlayFxKind.Leaves: HitFx.Puff(PuffKind.Leaves, world, root.TransformDirection(fx.aim), scale, o); break;
                case PlayFxKind.Sparks: HitFx.Puff(PuffKind.Sparks, world, Vector3.up, scale, o); break;
                case PlayFxKind.Snow: HitFx.Puff(PuffKind.Snow, world, Vector3.up, scale, o); break;
                case PlayFxKind.Grass: HitFx.Puff(PuffKind.Grass, world, Vector3.up, scale, o); break;
                case PlayFxKind.TreeShake: HitFx.Shake(ShakeTarget.Tree, world, root.TransformDirection(fx.aim)); break;
                case PlayFxKind.BushShake: HitFx.Shake(ShakeTarget.Bush, world, root.TransformDirection(fx.aim)); break;
            }
        }

        // ---------------------------------------------------------------- the snowballs

        static Mesh snowMesh;

        void DrawSnowballs(Transform root, Material material, float zoom)
        {
            foreach (var g in groups)
            {
                if (g.game is not SnowballGame sg) continue;
                var b = sg.Ball(now);
                if (g.ball == null)
                {
                    g.ball = new GameObject("Snowball") { hideFlags = HideFlags.DontSave };
                    g.ball.transform.SetParent(root, false);
                    g.ball.AddComponent<MeshFilter>().sharedMesh = SnowMesh();
                    g.ball.AddComponent<MeshRenderer>().sharedMaterial = material;
                }
                Vector3 pos;
                if (b.holder >= 0)
                {
                    var v = pv.ViewOf(sg.KeyOf(b.holder));
                    if (v == null || !v.gameObject.activeInHierarchy) { g.ball.SetActive(false); continue; }
                    pos = root.InverseTransformPoint(v.armR.TransformPoint(new Vector3(0f, -PegDoll.ArmLen * v.scale, 0f)));
                }
                else if (b.stage == ArcStage.Flying) pos = new Vector3(b.position.x, pv.GroundAt(b.position.x, b.position.z) + b.position.y, b.position.z);
                else { g.ball.SetActive(false); continue; }
                g.ball.SetActive(true);
                g.ball.transform.localPosition = pos;
                g.ball.transform.localScale = Vector3.one * zoom;
            }
        }

        static Mesh SnowMesh()
        {
            if (snowMesh != null) return snowMesh;
            var mb = new MeshBuilder(359);
            mb.Ico(Vector3.zero, SnowballGame.BallR, Vector3.one, Color.white, 0.03f, new Color(0.88f, 0.92f, 0.98f));
            snowMesh = mb.ToMesh("Snowball");
            snowMesh.hideFlags = HideFlags.DontSave;
            return snowMesh;
        }

        // ---------------------------------------------------------------- cheats (#359)

        /// <summary>
        /// The panel's "Kids play: splash / woods / hide and seek / snowball": the nearest kids play the game where it can be played (the
        /// pond's edge or a puddle; the nearest grown tree; a tent or tree with cover; the gathering place) for <see cref="ForcedSeconds"/>.
        /// </summary>
        string ForceB3(PlayGame game)
        {
            var planner = pv.Planner;
            var gather = planner.GatherPlace;
            if (!planner.Positions.TryGetPosition(gather, out float gx, out float gz)) return "no gathering place";
            var spot = new Vector2(gx, gz);
            PlaySite site = null;
            Vector2 centre = Open(spot + new Vector2(0.6f, -0.2f), spot);
            Place place = gather;
            switch (game)
            {
                case PlayGame.Splash:
                {
                    var pond = Place.Land(PlaceKind.Pond);
                    var land = pv.LandForPlay;
                    if (land != null && WaterSite(planner, pond, spot, out site)) { place = pond; centre = site.edge; break; }
                    var surf = EcoSurface.Instance;
                    if (surf != null && surf.PuddleCellsShown.Count > 0)
                    {
                        int best = -1; float bd = float.MaxValue;
                        foreach (int c in surf.PuddleCellsShown) { var pp = surf.PuddlePoint(c); float d = (new Vector2(pp.x, pp.z) - spot).sqrMagnitude; if (d < bd) { bd = d; best = c; } }
                        var pl = Place.Land(PlaceKind.Puddle, best);
                        if (WaterSite(planner, pl, spot, out site)) { place = pl; centre = site.edge; break; }
                    }
                    return LifeClock.SeasonOf(pv.Now.day) == Season.Winter ? "winter: no splashing" : "no water: no pond yet and no puddle ('Puddles now' first)";
                }
                case PlayGame.Woods:
                    if (!NearestTree(spot, 99f, null, out var trunk, out _)) return "no grown tree yet";
                    site = new PlaySite { tree = true, trunk = trunk, autumn = LifeClock.SeasonOf(pv.Now.day) == Season.Autumn, snow = LifeClock.SeasonOf(pv.Now.day) == Season.Winter };
                    centre = trunk;
                    spot = trunk;
                    break;
                case PlayGame.HideSeek:
                    site = HideSite(planner, spot);
                    if (!HideSeekGame.Enough(site, 3)) return site.hasBase ? "not enough cover near the base (needs 2 bushes, trees or tents within 1 u)" : "no tent, tree or pile near the gathering place";
                    centre = Vector2.Lerp(site.home.pos, spot, 0.5f);
                    break;
            }
            int want = game switch { PlayGame.Woods => 2, PlayGame.HideSeek => site != null ? Mathf.Min(4, 1 + site.covers.Count) : 3, PlayGame.Splash => 2, _ => 3 };
            var who = new List<int>();
            while (who.Count < want)
            {
                int k = pv.NearestShown(spot, x => planner.StageOf(x) == LifeStage.Kid, who);
                if (k < 0) break;
                who.Add(k);
            }
            int least = game == PlayGame.HideSeek ? 3 : 2;
            if (who.Count < least) return $"only {who.Count} kids on view for {game}";
            foreach (int k in who) if (byKey.TryGetValue(k, out var gi)) { int ix = groups.IndexOf(gi.g); if (ix >= 0) Remove(ix); }
            var g = new Group
            {
                place = place, forced = true, until = now + ForcedLead + ForcedSeconds, site = site,
                game = KidGame.Create(game, (int)PlayGroups.Hash(who[0], (long)(now * 10f), 359) & 0x7fffffff, now + ForcedLead, who, centre, site),
            };
            Add(g);
            return $"{game}: {who.Count} kids for {ForcedSeconds:0} s";
        }
    }
}
