// #359 AnimBench: the B3 kids' games (View/Beats/KidPlay2.cs) in a clip: their real things come from the clip's objects (src/Play.cs's
// "water", "tree", "base", "covers"), their puffs and shakes are logged (PlayFxLog), each splash's aimed droplets become an arc event and
// drive the "spray" object, and the water and the tree answer a contact aimed at them.
using System.Collections.Generic;
using AquariumCiv.View;
using UnityEngine;

namespace AnimBench
{
    public sealed partial class Simulator
    {
        /// <summary>#359: every puff and shake the play group asked for (time, kind, who).</summary>
        public readonly List<PlayFx> PlayFxLog = new List<PlayFx>();
        readonly List<PlayFx> fxStep = new List<PlayFx>();
        float fxT = float.NaN;
        bool sprayOn;
        PlayFx spray;

        PlaySite SiteOf(PlaySpec ps)
        {
            var site = new PlaySite { autumn = ps.Autumn };
            if (ps.Water != null)
            {
                var w = clip.Objects.Find(o => o.Id == ps.Water) ?? throw new ClipException($"play: water '{ps.Water}' is not an object");
                var mid = new Vector2(w.Pos.x, w.Pos.z);
                site.water = true;
                if (w.Kind == "puddle")
                {
                    site.puddle = true; site.mud = true; site.edge = mid; site.puddleR = w.Radius;
                    var along = ps.Centre - mid;
                    site.toLand = along.sqrMagnitude > 1e-4f ? new Vector2(-along.y, along.x).normalized : new Vector2(1f, 0f);
                }
                else
                {
                    // The pond's edge: from its middle out towards the group's centre, at the drawn ellipse's rim.
                    var dir = ps.Centre - mid;
                    if (dir.sqrMagnitude < 1e-6f) dir = new Vector2(0f, -1f);
                    dir = dir.normalized;
                    float rx = w.Size.x > 0f ? w.Size.x * 0.5f : w.Radius, rz = w.Size.y > 0f ? w.Size.y * 0.5f : w.Radius;
                    float r = 1f / Mathf.Sqrt(dir.x * dir.x / (rx * rx) + dir.y * dir.y / (rz * rz));
                    site.edge = mid + dir * r;
                    site.toLand = dir;
                }
            }
            if (ps.Tree != null)
            {
                var t = clip.Objects.Find(o => o.Id == ps.Tree) ?? throw new ClipException($"play: tree '{ps.Tree}' is not an object");
                site.tree = true; site.trunk = new Vector2(t.Pos.x, t.Pos.z);
            }
            if (ps.Base != null)
            {
                var b = clip.Objects.Find(o => o.Id == ps.Base) ?? throw new ClipException($"play: base '{ps.Base}' is not an object");
                site.hasBase = true; site.home = new PlayCover(new Vector2(b.Pos.x, b.Pos.z), b.Radius, KindOf(b.Kind));
            }
            foreach (var id in ps.Covers)
            {
                var c = clip.Objects.Find(o => o.Id == id) ?? throw new ClipException($"play: cover '{id}' is not an object");
                site.covers.Add(new PlayCover(new Vector2(c.Pos.x, c.Pos.z), c.Kind == "tree" ? 0.04f : c.Radius, KindOf(c.Kind)));
            }
            return site;
        }

        static CoverKind KindOf(string kind) => kind switch { "tree" => CoverKind.Tree, "tent" => CoverKind.Tent, "stash" => CoverKind.Pile, _ => CoverKind.Bush };

        /// <summary>Once a step, after the dolls: the game's puffs and shakes that came due; aimed droplets become arc events.</summary>
        void StepPlayFx(float t)
        {
            if (play == null) return;
            if (float.IsNaN(fxT)) { fxT = t; return; }
            if (t <= fxT) return;
            fxStep.Clear();
            play.Effects(fxT, t, fxStep);
            fxT = t;
            foreach (var fx in fxStep)
            {
                PlayFxLog.Add(fx);
                if (fx.kind == PlayFxKind.Droplets && fx.aimed)
                {
                    spray = fx; sprayOn = true;
                    if (clip.Play.Spray != null && fx.by >= 0 && fx.on >= 0)
                        ArcEvents.Add(new ArcEvent { Object = clip.Play.Spray, By = playIds[fx.by], Target = playIds[fx.on], T0 = t, T1 = fx.at + PlayFx.DropletFlight }); // T0: the frame it is first seen on
                }
            }
        }

        /// <summary>The "spray" object rides the last splash's droplets while they fly (hidden otherwise).</summary>
        bool StepSpray(ObjectState o, ref ObjectFrame f, float t)
        {
            if (play == null || clip.Play.Spray == null || o.Spec.Id != clip.Play.Spray) return false;
            if (!sprayOn || t > spray.at + PlayFx.DropletFlight + 0.05f) { f.Visible = false; f.Act = ""; return true; }
            var shot = spray.Shot;
            var s = Arc.Sample(shot, Mathf.Min(t, shot.LandAt - 1e-4f));
            f.Pos = s.position;
            f.Act = "spray";
            return true;
        }

        /// <summary>A play doll's contact aimed at this object (the water, the tree) on this frame.</summary>
        bool PlayHitOn(string id)
        {
            foreach (var d in dolls) if (d.Placed && d.Last.Playing && d.Last.Pose.hit > 0.5f && d.Last.HitTarget == id) return true;
            return false;
        }
    }
}
