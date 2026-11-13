// #359 Kids' play 2 (docs/design/animation-specs.md B3): splashing at the water, games round a real tree, hide and seek, and (the B2
// stretch) a snowball fight, as more KidGame subclasses on B2's play group (KidPlay.cs). Pure (Mathf, Vector2/3, Quaternion, DollMotion
// and Life types only), so Tools/AnimBench steps exactly this code. The owner (KidPlayDirector in the game, the bench's Simulator) hands
// each game a PlaySite: the real water edge or puddle, the real grown tree, the real cover (bushes, trees, tents) and the base. Puffs and
// target shakes come out of Effects (the owner spawns them through HitFx); the contacts still set DollPose.hit (PeopleView.WorkHit).
using System.Collections.Generic;
using AquariumCiv.Life;
using UnityEngine;

namespace AquariumCiv.View
{
    /// <summary>What a play game's puff or shake is (the owner maps it onto HitFx: §0.4, B1).</summary>
    public enum PlayFxKind { Droplets, Ring, Leaves, TreeShake, BushShake, Sparks, Snow, Grass }

    /// <summary>One puff or shake a game asks for at <see cref="at"/> (the owner's clock), fired once when the clock passes it.</summary>
    public struct PlayFx
    {
        public PlayFxKind kind;
        public float at;
        /// <summary>Box x, z; y above the ground (above the water's surface when <see cref="wet"/>).</summary>
        public Vector3 pos;
        /// <summary>Aimed droplets: where they land (box x, z; y above the ground). Shakes: the way the target leans.</summary>
        public Vector3 aim;
        public bool aimed, wet, mud;
        /// <summary>Bits (0: the table's).</summary>
        public int count;
        /// <summary>Who made it and on whom it lands (member indexes; −1 none).</summary>
        public int by, on;
        public float scale;

        /// <summary>Aimed droplets fly like HitFx draws them: reached in <see cref="DropletFlight"/> s, apex g·T²/8 (gravity 4 u/s²).</summary>
        public const float DropletFlight = 0.3f, DropletApex = 4f * DropletFlight * DropletFlight / 8f;
        public ArcShot Shot => ArcShot.Throw(pos, aim, DropletApex, DropletFlight, Landing.Splash, at, 0f, 0.02f);
    }

    /// <summary>What a cover is (where the hiders crouch, where the seeker counts).</summary>
    public enum CoverKind { Bush, Tree, Tent, Pile }

    public struct PlayCover
    {
        public Vector2 pos;
        public float r;
        public CoverKind kind;
        public PlayCover(Vector2 pos, float r, CoverKind kind) { this.pos = pos; this.r = r; this.kind = kind; }
    }

    /// <summary>
    /// The real things a B3 game plays with (the owner finds them in the land: KidPlayDirector; a clip's objects in AnimBench).
    /// Water: a point on the pond's real edge and the way to dry land, or a puddle. Woods: one real grown tree. Hide and seek: the base
    /// (a tent, a tree or the pile) and the cover round it.
    /// </summary>
    public sealed class PlaySite
    {
        public bool water, puddle, mud;
        /// <summary>The pond: the first dry point of the real shore; a puddle: its middle.</summary>
        public Vector2 edge;
        /// <summary>Unit, from the water towards dry land (the pond); a puddle's own "along" direction.</summary>
        public Vector2 toLand = new Vector2(0f, -1f);
        public float puddleR = 0.12f;
        public bool tree, autumn;
        public Vector2 trunk;
        public float trunkR = 0.035f;
        public bool hasBase;
        public PlayCover home;
        public readonly List<PlayCover> covers = new List<PlayCover>();
        public bool snow;
        /// <summary>#618: the ice slide: run up to <see cref="slideA"/>, glide to <see cref="slideB"/> (both on inner ice).</summary>
        public bool ice;
        public Vector2 slideA, slideB;
    }

    // ==================================================================== B3.a Splash
    /// <summary>
    /// Splash (B3.a): 2–4 kids at the real water, in pairs (or a three) facing each other 0.25 u apart along the bank, ankle-deep (sunk
    /// 0.03 u at the pond; on a puddle). Turn about: crouch, arms back, then the sweep forward and up; the hands hit the water (the
    /// Contact: a ring at the hands and droplets aimed at the partner); the partner is soaked when they land (jolts back, arms up), laughs,
    /// and splashes back. Every few exchanges one jumps in (squash, a hop out into the water, lands sunk 0.06 in two rings and ten
    /// droplets; the others squeal), stands a moment, wades back. Then they all wade out and shake off (the end beat), and wade in again.
    /// </summary>
    public sealed class SplashGame : KidGame
    {
        public const float Apart = 0.25f, Wade = 0.03f, Deep = 0.06f, JumpIn = 0.13f, PairGap = 0.5f, WadeOut = 0.16f,
            ShakeFor = 0.6f, StayIn = 1.0f, WadeBack = 0.7f, JumpHop = 0.11f, Squeal = 0.05f, LaughFor = 0.6f;

        /// <summary>The splash: crouch with both arms down and back (0.25 s), the sweep (Snap) through the water (Contact at x 60) and up to
        /// x 120, held, back.</summary>
        static readonly Beat Throw = new Beat(
            Beat.Step(BeatPhase.Antic, 0.25f, Ease.Out, PoseKey.Hold.Squash(0.85f).Lean(14f).Arms(-30f, 12f)),
            Beat.Step(BeatPhase.Act, 0.06f, Ease.Snap, PoseKey.Hold.Squash(0.9f).Lean(26f).Arms(60f, 10f)),
            Beat.Step(BeatPhase.Contact, Beat.MinContact, Ease.Linear, PoseKey.Hold),
            Beat.Step(BeatPhase.Follow, 0.08f, Ease.Snap, PoseKey.Hold.Squash(1.04f).Lean(2f).Offset(0f, 0.025f, 0f).Arms(120f, 16f)),
            Beat.Step(BeatPhase.Hold, 0.15f, Ease.Linear, PoseKey.Hold),
            Beat.Step(BeatPhase.Settle, 0.35f, Ease.Smooth, PoseKey.Base));

        /// <summary>Soaked: jolts back 0.04, squashes, arms up to the face; held; back (then the laugh).</summary>
        static readonly Beat Soaked = new Beat(
            Beat.Step(BeatPhase.Act, 0.12f, Ease.Out, PoseKey.Hold.Offset(0f, 0f, 0.045f).Squash(0.88f).Arms(75f, 30f).Lean(-12f)),
            Beat.Step(BeatPhase.Hold, 0.3f, Ease.Linear, PoseKey.Hold),
            Beat.Step(BeatPhase.Settle, 0.25f, Ease.Smooth, PoseKey.Base));

        /// <summary>The jump in: squash (0.2 s), the hop (arms flung up; the cue carries it out over the water), the landing (the Contact),
        /// sunk and squashed, held, up.</summary>
        static readonly Beat Jump = new Beat(
            Beat.Step(BeatPhase.Antic, 0.2f, Ease.Out, PoseKey.Hold.Squash(0.8f).Lean(10f).Arms(-25f, 30f)),
            Beat.Step(BeatPhase.Act, 0.5f, Ease.Smooth, PoseKey.Hold.Squash(1.08f).Lean(-4f).Arms(165f, 30f)),
            Beat.Step(BeatPhase.Contact, Beat.MinContact, Ease.Linear, PoseKey.Hold.Squash(0.82f).Arms(110f, 60f)),
            Beat.Step(BeatPhase.Hold, 0.2f, Ease.Linear, PoseKey.Hold),
            Beat.Step(BeatPhase.Settle, 0.4f, Ease.Back, PoseKey.Hold.Squash(1f).Arms(150f, 40f)));

        static DollPose ContactPose { get { var p = DollPose.Rest; p.squash = 0.9f; p.lean = 26f; p.armL = new Vector3(60f, 0f, -10f); p.armR = new Vector3(60f, 0f, 10f); return p; } }

        public enum ExKind { Splash, Jump, Shake }

        public struct Ex
        {
            public ExKind kind;
            public int by, on;
            public float start, contact, end;
        }

        sealed class Sub
        {
            public int[] m;
            public Vector2[] spot;
            public float[] sink;
            public Vector2[] jumpTo;
            public int seed, perSet;
            public float delay;
            public readonly List<Ex> ex = new List<Ex>();
        }

        readonly PlaySite site;
        readonly Sub[] subs;
        readonly int[] subOf, slotOf;

        public SplashGame(int seed, float t0, IReadOnlyList<int> members, Vector2 centre, PlaySite site) : base(PlayGame.Splash, seed, t0, members, centre)
        {
            this.site = site ?? new PlaySite { water = true, edge = centre };
            int n = Count;
            subOf = new int[n]; slotOf = new int[n];
            int nSub = n >= 4 ? 2 : 1;
            subs = new Sub[nSub];
            var along = this.site.puddle ? this.site.toLand : new Vector2(-this.site.toLand.y, this.site.toLand.x);
            if (along.sqrMagnitude < 1e-6f) along = new Vector2(1f, 0f);
            along = along.normalized;
            var land = this.site.toLand.sqrMagnitude > 1e-6f ? this.site.toLand.normalized : new Vector2(0f, -1f);
            int at = 0;
            for (int s = 0; s < nSub; s++)
            {
                int size = n / nSub + (s < n % nSub ? 1 : 0);
                var sub = new Sub { m = new int[size], spot = new Vector2[size], sink = new float[size], jumpTo = new Vector2[size], seed = seed * 31 + s * 7 + 359 };
                sub.perSet = 4 + BeatHash.Pick(2, sub.seed, 1, 359);
                sub.delay = s * 0.55f + BeatHash.Range(0f, 0.3f, sub.seed, 2, 359);
                float off = (s - (nSub - 1) * 0.5f) * PairGap;
                var mid = this.site.puddle ? this.site.edge + along * off * 0.6f : this.site.edge + along * off;
                for (int k = 0; k < size; k++)
                {
                    int i = at + k;
                    sub.m[k] = i; subOf[i] = s; slotOf[i] = k;
                    Vector2 p;
                    if (size == 2) p = mid + along * (k == 0 ? -Apart * 0.5f : Apart * 0.5f);
                    else
                    {
                        // A three: two along the bank, the third out in the water facing them (on a puddle: a small triangle).
                        p = k < 2 ? mid + along * (k == 0 ? -Apart * 0.5f : Apart * 0.5f) : mid - land * (Apart * 0.866f);
                    }
                    if (!this.site.puddle) p -= land * (k < 2 ? 0.02f : 0f); // a step in off the shore: ankle-deep
                    sub.spot[k] = p;
                    sub.sink[k] = this.site.puddle ? 0f : (k < 2 ? Wade : Deep);
                    sub.jumpTo[k] = this.site.puddle ? Vector2.Lerp(p, this.site.edge, 0.7f) - along * 0.02f : p - land * JumpIn;
                }
                at += size;
                subs[s] = sub;
            }
        }

        public PlaySite Site => site;

        /// <summary>Exchange <paramref name="j"/> of member <paramref name="i"/>'s pair or three (grown on demand).</summary>
        public Ex ExOf(int i, int j) { var s = subs[subOf[i]]; Grow(s, j); return s.ex[j]; }

        void Grow(Sub s, int j)
        {
            int size = s.m.Length;
            while (s.ex.Count <= j)
            {
                int k = s.ex.Count;
                float start = k == 0 ? 0.4f + s.delay : s.ex[k - 1].end;
                int inSet = k % (s.perSet + 2);
                var e = new Ex { start = start, by = -1, on = -1 };
                if (inSet == s.perSet + 1)
                {
                    e.kind = ExKind.Shake;
                    e.contact = start + WadeBack;
                    e.end = start + WadeBack + ShakeFor + 0.3f + WadeBack;
                }
                else
                {
                    // Turn about: the one splashed last splashes back (in a three it goes round).
                    int splashes = k - (k / (s.perSet + 2)) * 2;
                    int by = (splashes + BeatHash.Pick(size, s.seed, 3, 359)) % size;
                    e.by = by; e.on = (by + 1) % size;
                    if (inSet == s.perSet)
                    {
                        e.kind = ExKind.Jump;
                        e.contact = start + Jump.ContactAt;
                        e.end = e.contact + StayIn + WadeBack + 0.3f;
                    }
                    else
                    {
                        e.kind = ExKind.Splash;
                        e.contact = start + Throw.ContactAt;
                        e.end = start + BeatHash.Range(1.6f, 2.2f, s.seed, k, 359);
                    }
                }
                s.ex.Add(e);
            }
        }

        int ExIndexAt(Sub s, float local)
        {
            int k = 0;
            Grow(s, 0);
            while (true) { Grow(s, k + 1); if (s.ex[k + 1].start > local) return k; k++; }
        }

        /// <summary>Where member slot <paramref name="k"/> stands to shake off: out of the puddle, or a step up the bank.</summary>
        Vector2 Out(Sub s, int k)
        {
            if (!site.puddle) return s.spot[k] + site.toLand.normalized * WadeOut;
            var d = s.spot[k] - site.edge;
            if (d.sqrMagnitude < 1e-6f) d = site.toLand;
            return site.edge + d.normalized * (site.puddleR + 0.08f);
        }

        public override PlayCue Cue(int i, float t)
        {
            var s = subs[subOf[i]];
            int k = slotOf[i], size = s.m.Length;
            float local = Mathf.Max(0f, t - t0);
            var cue = new PlayCue { role = GroupRole.Splasher, hitOn = NoHit, under = DollStyle.Stand, style = DollStyle.Stand, pos = s.spot[k], sink = s.sink[k] };
            cue.face = s.spot[(k + 1) % size];
            if (local < s.delay + 0.4f) { cue.since = -1f; return cue; }
            int j = ExIndexAt(s, local);
            var e = s.ex[j];
            float w = local - e.start;
            cue.round = j;
            cue.since = w;
            if (e.kind == ExKind.Splash)
            {
                if (k == e.by)
                {
                    cue.style = DollStyle.PlaySplash;
                    cue.face = s.spot[e.on];
                    cue.hitOn = Water;
                    cue.endBeat = local >= e.contact + 0.1f; // it sees its splash land
                }
                else if (k == e.on)
                {
                    float soak = local - (e.contact + PlayFx.DropletFlight);
                    cue.face = s.spot[e.by];
                    if (soak >= 0f && soak < Soaked.Duration) { cue.style = DollStyle.Flinch; cue.since = soak; cue.endBeat = true; }
                    else if (soak >= Soaked.Duration && soak < Soaked.Duration + LaughFor) { cue.style = DollStyle.Laugh; cue.under = DollStyle.Laugh; cue.since = soak - Soaked.Duration; cue.endBeat = true; }
                    else { cue.since = soak; cue.style = DollStyle.Stand; }
                }
                else cue.face = s.spot[e.by]; // the third watches the splasher
            }
            else if (e.kind == ExKind.Jump)
            {
                if (k == e.by)
                {
                    cue.style = DollStyle.PlayJump;
                    cue.hitOn = Water;
                    float landAt = Jump.ContactAt, hop0 = Jump[0].seconds;
                    if (w < hop0) { cue.face = s.jumpTo[k]; }
                    else if (w < landAt)
                    {
                        float u = Easing.Apply(Ease.Smooth, (w - hop0) / (landAt - hop0));
                        cue.pos = Vector2.Lerp(s.spot[k], s.jumpTo[k], u);
                        cue.face = s.jumpTo[k] + (s.jumpTo[k] - s.spot[k]);
                        cue.sink = site.puddle ? 0f : Mathf.Max(0.001f, Mathf.Lerp(s.sink[k], 0f, Mathf.Clamp01(u * 3f))); // up out of the water as it springs (still "in" it: never pushed ashore mid-hop)
                        cue.speed = (s.jumpTo[k] - s.spot[k]).magnitude / (landAt - hop0);
                    }
                    else if (w < landAt + StayIn)
                    {
                        cue.pos = s.jumpTo[k];
                        cue.sink = site.puddle ? 0f : Deep;
                        cue.face = s.spot[(k + 1) % size];
                        cue.endBeat = true;
                        if (w >= landAt + Jump.Duration - Jump.ContactAt) { cue.style = DollStyle.Laugh; cue.under = DollStyle.Laugh; }
                    }
                    else
                    {
                        float u = Mathf.Clamp01((w - landAt - StayIn) / WadeBack);
                        cue.pos = Vector2.Lerp(s.jumpTo[k], s.spot[k], Easing.Apply(Ease.Smooth, u));
                        cue.sink = Mathf.Lerp(site.puddle ? 0f : Deep, s.sink[k], u);
                        cue.face = s.spot[k] + (s.spot[k] - s.jumpTo[k]);
                        cue.style = u < 1f ? DollStyle.Walk : DollStyle.Stand;
                        cue.under = u < 1f ? DollStyle.Walk : DollStyle.Stand;
                        cue.speed = u < 1f ? (s.jumpTo[k] - s.spot[k]).magnitude / WadeBack : 0f;
                    }
                }
                else
                {
                    // The others watch it go and squeal when it lands (a jolt, arms up), then laugh.
                    float sq = local - e.contact - Squeal;
                    cue.face = w < Jump.ContactAt ? Vector2.Lerp(s.spot[e.by], s.jumpTo[e.by], 0.5f) : s.jumpTo[e.by];
                    if (sq >= 0f && sq < Soaked.Duration) { cue.style = DollStyle.Flinch; cue.since = sq; cue.endBeat = true; }
                    else if (sq >= Soaked.Duration && sq < Soaked.Duration + LaughFor) { cue.style = DollStyle.Laugh; cue.under = DollStyle.Laugh; cue.since = sq - Soaked.Duration; cue.endBeat = true; }
                }
            }
            else
            {
                // Wade out, shake off (the end beat), wade back in.
                var dry = Out(s, k);
                float a = WadeBack, b = a + ShakeFor + 0.3f;
                if (w < a)
                {
                    float u = Easing.Apply(Ease.Smooth, w / a);
                    cue.pos = Vector2.Lerp(s.spot[k], dry, u); cue.sink = Mathf.Lerp(s.sink[k], 0f, u);
                    cue.face = dry + (dry - s.spot[k]);
                    cue.under = cue.style = DollStyle.Walk; cue.speed = (dry - s.spot[k]).magnitude / a;
                }
                else if (w < b)
                {
                    cue.pos = dry; cue.sink = 0f; cue.face = Mid(s);
                    cue.style = DollStyle.PlayShake; cue.since = w - a; cue.endBeat = true;
                }
                else
                {
                    float u = Easing.Apply(Ease.Smooth, Mathf.Clamp01((w - b) / WadeBack));
                    cue.pos = Vector2.Lerp(dry, s.spot[k], u); cue.sink = Mathf.Lerp(0f, s.sink[k], u);
                    cue.face = u < 1f ? s.spot[k] + (s.spot[k] - dry) : s.spot[(k + 1) % size];
                    cue.under = cue.style = u < 1f ? DollStyle.Walk : DollStyle.Stand; cue.speed = u < 1f ? (dry - s.spot[k]).magnitude / WadeBack : 0f;
                }
            }
            return cue;
        }

        static Vector2 Mid(Sub s) { var m = Vector2.zero; foreach (var p in s.spot) m += p; return m / s.spot.Length; }

        public override void Pose(int i, float t, in PlayCue c, ref DollPose p)
        {
            float tau = Mathf.PI * 2f;
            switch (c.style)
            {
                case DollStyle.PlaySplash: Throw.Apply(ref p, c.since); break;
                case DollStyle.Flinch: Soaked.Apply(ref p, c.since); break;
                case DollStyle.PlayJump:
                {
                    var s = subs[subOf[i]];
                    var e = s.ex[Mathf.Clamp(c.round, 0, s.ex.Count - 1)];
                    float w = Mathf.Max(0f, t - t0) - e.start;
                    Jump.Apply(ref p, w);
                    float hop0 = Jump[0].seconds, land = Jump.ContactAt;
                    if (w > hop0 && w < land) { float u = (w - hop0) / (land - hop0); p.offset.y += 4f * JumpHop * u * (1f - u); }
                    break;
                }
                case DollStyle.PlayShake:
                {
                    // Shaking off like a wet pup: roll ±12° at 8 Hz, arms flung out, a little bounce; fading out at the end.
                    float u = Mathf.Clamp01(c.since / ShakeFor), fade = u < 0.85f ? 1f : (1f - u) / 0.15f;
                    float sw = Mathf.Sin(c.since * 8f * tau);
                    p.roll = sw * 12f * fade;
                    p.yaw += sw * 10f * fade;
                    p.armL = new Vector3(10f, 0f, -70f + sw * 25f); p.armR = new Vector3(10f, 0f, 70f + sw * 25f);
                    p.offset.y += Mathf.Abs(sw) * 0.015f * fade;
                    if (c.since >= ShakeFor) { p.roll = 0f; }
                    break;
                }
                case DollStyle.Stand:
                    // Standing in the water between splashes: a wade from foot to foot, arms ready.
                    if (c.sink > 0f) { float w = Mathf.Sin(t * 4f + i * 1.3f); p.roll = w * 4f; p.armL = new Vector3(20f, 0f, -40f); p.armR = new Vector3(20f, 0f, 40f); }
                    break;
            }
        }

        public override void Effects(float from, float to, List<PlayFx> into)
        {
            foreach (var s in subs)
            {
                float a = from - t0, b = to - t0;
                if (b <= 0f) continue;
                int j0 = ExIndexAt(s, Mathf.Max(0f, a)), j1 = ExIndexAt(s, b);
                for (int j = Mathf.Max(0, j0 - 1); j <= j1; j++)
                {
                    var e = s.ex[j];
                    if (e.kind == ExKind.Shake)
                    {
                        float at = e.start + WadeBack + 0.1f;
                        if (at > a && at <= b)
                            for (int k = 0; k < s.m.Length; k++)
                            {
                                var dry = Out(s, k);
                                into.Add(new PlayFx { kind = PlayFxKind.Droplets, at = t0 + at, pos = new Vector3(dry.x, 0.08f, dry.y), count = 4, by = s.m[k], on = -1, scale = 0.8f, mud = site.mud });
                            }
                        continue;
                    }
                    if (!(e.contact > a && e.contact <= b)) continue;
                    int by = e.by;
                    if (e.kind == ExKind.Splash)
                    {
                        var from2 = s.spot[by]; var onP = s.spot[e.on];
                        var hands = HandsMid(from2, YawTo(from2, onP), ContactPose, scales[s.m[by]]);
                        float wet = s.sink[by];
                        var hp = new Vector3(hands.x, Mathf.Max(0.005f, hands.y - wet), hands.z);
                        var chest = new Vector3(onP.x, KidScale * BodyH * 0.6f - s.sink[e.on], onP.y);
                        into.Add(new PlayFx { kind = PlayFxKind.Ring, at = t0 + e.contact, pos = new Vector3(hands.x, 0f, hands.z), wet = true, by = s.m[by], on = -1, scale = 0.8f, mud = site.mud });
                        into.Add(new PlayFx { kind = PlayFxKind.Droplets, at = t0 + e.contact, pos = hp, aim = chest, aimed = true, wet = true, count = 8, by = s.m[by], on = s.m[e.on], scale = 1f, mud = site.mud });
                    }
                    else
                    {
                        var land = s.jumpTo[by];
                        into.Add(new PlayFx { kind = PlayFxKind.Ring, at = t0 + e.contact, pos = new Vector3(land.x, 0f, land.y), wet = true, by = s.m[by], on = -1, scale = 1.2f, mud = site.mud });
                        into.Add(new PlayFx { kind = PlayFxKind.Droplets, at = t0 + e.contact, pos = new Vector3(land.x, 0.02f, land.y), wet = true, count = 10, by = s.m[by], on = -1, scale = 1.1f, mud = site.mud });
                    }
                }
            }
        }
    }

    // ==================================================================== B3.b Woods games
    /// <summary>
    /// Woods games (B3.b), two kids at one real grown tree, in turns. <b>Round the tree</b>: the "it" bounces twice, then both run round
    /// the trunk (r 0.14) on opposite sides; the runner stops dead with a hand on the trunk (the Contact: the tree shakes, a leaf falls),
    /// both peek round it (lean sideways, roll 20°), the runner dashes back the other way and the chaser cuts it off: a tag (B2.a's touch
    /// and jolt); they swap. <b>Stick duel</b>: beside the tree, 0.18 u apart, sticks in hand; turn about one raises its stick and strikes,
    /// the other blocks, the sticks cross (the Contact: a flash of sparks) and both jolt back; after five or six the two hold their sticks
    /// high (the end beat). Autumn: the runners kick up leaves.
    /// </summary>
    public sealed class WoodsGame : KidGame
    {
        public const float R = 0.14f, Split = 1.2f, Chase = 2.0f, Bump = 0.15f, Peek = 0.6f, Dash = 1.6f, After = 0.65f, Omega = 2.0f,
            CloseTo = 0.6f, TagReach = 0.09f, Cornered = 0.3f, DuelGap = 0.24f, StrikeEvery = 1.2f, HighFor = 1.0f, Walk = 1.0f, LeafEvery = 0.8f;
        public const int Rounds = 3;
        public readonly int Strikes;

        public float RoundSeconds => Split + Chase + Bump + Peek + Dash + After;
        public float CircleSeconds => Rounds * RoundSeconds;
        public float DuelSeconds => Walk + Strikes * StrikeEvery + HighFor;
        public float CycleSeconds => CircleSeconds + DuelSeconds;

        static readonly Beat Touch = new Beat(
            Beat.Step(BeatPhase.Act, 0.1f, Ease.Snap, PoseKey.Hold.ArmR(95f, 0f, 8f).ArmL(40f, 0f, -14f).Lean(20f)),
            Beat.Step(BeatPhase.Contact, Beat.MinContact, Ease.Linear, PoseKey.Hold),
            Beat.Step(BeatPhase.Hold, 0.25f, Ease.Linear, PoseKey.Hold),
            Beat.Step(BeatPhase.Settle, 0.24f, Ease.Smooth, PoseKey.Base));

        static readonly Beat Tagged = new Beat(
            Beat.Step(BeatPhase.Act, 0.12f, Ease.Out, PoseKey.Hold.Offset(0f, 0f, 0.04f).Squash(0.9f).Arms(70f, 30f).Lean(-10f)),
            Beat.Step(BeatPhase.Hold, 0.35f, Ease.Linear, PoseKey.Hold),
            Beat.Step(BeatPhase.Settle, 0.18f, Ease.Smooth, PoseKey.Base));

        /// <summary>The runner's stop: a hand slapped on the trunk (the Contact: the tree shakes), held as it turns.</summary>
        static readonly Beat Slap = new Beat(
            Beat.Step(BeatPhase.Act, 0.09f, Ease.Snap, PoseKey.Hold.ArmR(85f, 0f, 30f).Lean(12f)),
            Beat.Step(BeatPhase.Contact, Beat.MinContact, Ease.Linear, PoseKey.Hold),
            Beat.Step(BeatPhase.Hold, 0.12f, Ease.Linear, PoseKey.Hold),
            Beat.Step(BeatPhase.Settle, 0.2f, Ease.Smooth, PoseKey.Base));

        /// <summary>A strike: the stick up (x 130), down (Snap) onto the other's stick (the Contact), the jolt back, settle.</summary>
        static readonly Beat Strike = new Beat(
            Beat.Step(BeatPhase.Antic, 0.25f, Ease.Out, PoseKey.Hold.ArmR(135f, 0f, 12f).Lean(-8f).Squash(1.04f).Extend(true)),
            Beat.Step(BeatPhase.Act, 0.1f, Ease.Snap, PoseKey.Hold.ArmR(88f, 0f, 4f).Lean(14f).Squash(0.94f)), // level: the sticks meet at chest height
            Beat.Step(BeatPhase.Contact, Beat.MinContact, Ease.Linear, PoseKey.Hold),
            Beat.Step(BeatPhase.Hold, 0.14f, Ease.Out, PoseKey.Hold.Offset(0f, 0f, 0.03f).Lean(4f)),
            Beat.Step(BeatPhase.Settle, 0.4f, Ease.Smooth, PoseKey.Base.Extend(true)));

        /// <summary>The block: the stick across (x 100, turned in), braced; at the cross a jolt back; settle.</summary>
        static readonly Beat Block = new Beat(
            Beat.Step(BeatPhase.Antic, 0.2f, Ease.Out, PoseKey.Hold.ArmR(100f, 0f, -25f).Lean(-6f).Squash(0.94f).Extend(true)),
            Beat.Step(BeatPhase.Hold, 0.2f, Ease.Linear, PoseKey.Hold),
            Beat.Step(BeatPhase.Follow, 0.12f, Ease.Out, PoseKey.Hold.Offset(0f, 0f, 0.04f).Lean(-12f).Squash(0.9f)),
            Beat.Step(BeatPhase.Settle, 0.4f, Ease.Smooth, PoseKey.Base.Extend(true)));

        sealed class Round
        {
            public int chaser, runner;
            public float d, thC0, thR0, gap0;
            public Vector2 fromC, fromR;
            public bool fromDuel;
        }

        readonly PlaySite site;
        readonly Vector2 trunk;
        readonly float a0;
        readonly int first;
        readonly Vector2 duelMid, duelAxis;
        readonly List<Round> rounds = new List<Round>();

        public WoodsGame(int seed, float t0, IReadOnlyList<int> members, Vector2 centre, PlaySite site) : base(PlayGame.Woods, seed, t0, members, centre)
        {
            this.site = site ?? new PlaySite { tree = true, trunk = centre };
            trunk = this.site.tree ? this.site.trunk : centre;
            a0 = BeatHash.U01(seed, 1, 359) * Mathf.PI * 2f;
            first = BeatHash.Pick(Mathf.Max(1, Count), seed, 2, 359);
            Strikes = 5 + BeatHash.Pick(2, seed, 3, 359);
            var side = Dir(a0 + 0.9f);
            duelMid = trunk + side * 0.3f;
            duelAxis = new Vector2(-side.y, side.x);
        }

        public Vector2 Trunk => trunk;

        Vector2 OnRing(float th) => trunk + Dir(th) * R;
        Vector2 DuelSpot(int i) => duelMid + duelAxis * (i == 0 ? -DuelGap * 0.5f : DuelGap * 0.5f);

        /// <summary>Where the cycle is: the circle set (round, seconds into it) or the duel (seconds into it).</summary>
        void Where(float t, out int cycle, out bool duel, out int round, out float w)
        {
            float s = Mathf.Max(0f, t - t0);
            cycle = Mathf.FloorToInt(s / CycleSeconds);
            float c = s - cycle * CycleSeconds;
            if (c < CircleSeconds) { duel = false; round = cycle * Rounds + Mathf.Min(Rounds - 1, Mathf.FloorToInt(c / RoundSeconds)); w = c - (round - cycle * Rounds) * RoundSeconds; }
            else { duel = true; round = cycle * Rounds + Rounds - 1; w = c - CircleSeconds; }
        }

        Round RoundOf(int r)
        {
            while (rounds.Count <= r)
            {
                int k = rounds.Count;
                var o = new Round();
                bool fromDuel = k % Rounds == 0;
                if (Count < 2) { o.chaser = 0; o.runner = 0; }
                else if (k == 0) { o.chaser = first; o.runner = 1 - first; }
                else { var p = rounds[k - 1]; o.chaser = p.runner; o.runner = p.chaser; }
                o.d = BeatHash.U01(seed, k, 31) < 0.5f ? 1f : -1f;
                if (fromDuel)
                {
                    o.fromDuel = true;
                    o.fromC = k == 0 ? OnRing(a0) : DuelSpot(o.chaser);
                    o.fromR = k == 0 ? OnRing(a0 + Mathf.PI) : DuelSpot(o.runner);
                    var dc = o.fromC - trunk; o.thC0 = Mathf.Atan2(dc.y, dc.x);
                    o.thR0 = o.thC0 + Mathf.PI; o.gap0 = Mathf.PI;
                }
                else
                {
                    var p = rounds[k - 1];
                    // The new chaser is the old runner where it was tagged; the new runner is the old chaser just behind it.
                    Angles(p, RoundSeconds, out _, out float thRend);
                    o.thC0 = thRend;
                    o.thR0 = thRend + p.d * CloseTo; // the old chaser caught up from behind, on the side it came from
                    o.gap0 = CloseTo;
                    o.d = p.d; // the new runner runs on round the way it faces
                }
                rounds.Add(o);
            }
            return rounds[r];
        }

        /// <summary>The angles of the chaser and the runner at seconds <paramref name="w"/> into round <paramref name="o"/>.</summary>
        void Angles(Round o, float w, out float thC, out float thR)
        {
            // Split: the runner gets to the far side (gap pi); the chaser winds up where it stands.
            float thRs = o.thC0 + o.d * Mathf.PI;
            float runner0 = o.fromDuel ? thRs : o.thR0;
            if (w < Split)
            {
                float u = Easing.Apply(Ease.Smooth, w / Split);
                thC = o.thC0;
                // Gap opens from gap0 to pi the way d runs.
                thR = o.fromDuel ? thRs : o.thR0 + o.d * (Mathf.PI - o.gap0) * u;
                return;
            }
            float x = w - Split;
            float thC1 = o.thC0, thR1 = o.fromDuel ? runner0 : o.thR0 + o.d * (Mathf.PI - o.gap0);
            if (x < Chase) { thC = thC1 + o.d * Omega * x; thR = thR1 + o.d * Omega * x; return; }
            float thC2 = thC1 + o.d * Omega * Chase, thR2 = thR1 + o.d * Omega * Chase;
            x -= Chase;
            if (x < Bump + Peek) { thC = thC2; thR = thR2; return; }
            x -= Bump + Peek;
            float run = Mathf.Min(x, Dash), flee = Dash - Cornered;
            // Back the other way: the runner at Omega until it sees it's cut off and stops dead (Cornered s before the touch); the chaser
            // cuts round faster (Smooth) and arrives on the touch.
            float sweep = Omega * flee + Mathf.PI - CloseTo;
            thR = thR2 - o.d * Omega * Mathf.Min(run, flee);
            thC = thC2 - o.d * sweep * Easing.Apply(Ease.Smooth, run / Dash);
        }

        Vector2 CirclePos(Round o, int i, float w)
        {
            Angles(o, w, out float thC, out float thR);
            bool isC = i == o.chaser;
            if (o.fromDuel && w < Split)
            {
                float u = Easing.Apply(Ease.Smooth, w / Split);
                var from = isC ? o.fromC : o.fromR;
                return Vector2.Lerp(from, OnRing(isC ? thC : thR), u);
            }
            return OnRing(isC ? thC : thR);
        }

        public override PlayCue Cue(int i, float t)
        {
            Where(t, out int cyc, out bool duel, out int r, out float w);
            var cue = new PlayCue { role = GroupRole.Circler, hitOn = NoHit, under = DollStyle.Stand, style = DollStyle.Stand, round = r, since = w };
            if (Count < 2) { cue.pos = OnRing(a0); cue.face = trunk; return cue; }
            if (duel) return DuelCue(i, w, cue, cyc);
            var o = RoundOf(r);
            cue.pos = CirclePos(o, i, w);
            var oo = o;
            cue.speed = SpeedOf(x => CirclePos(oo, i, x), w);
            bool isC = i == o.chaser;
            float tChase = Split, tBump = Split + Chase, tPeek = tBump + Bump, tDash = tPeek + Peek, tTag = tDash + Dash;
            var other = CirclePos(o, isC ? o.runner : o.chaser, w);
            if (w < tChase)
            {
                cue.face = isC ? trunk : cue.pos + Tangent(cue.pos, o.d);
                cue.style = isC ? DollStyle.PlayCircle : DollStyle.PlayRunner;
                cue.under = isC || cue.speed < 0.03f ? DollStyle.Stand : DollStyle.Run;
                if (isC) cue.role = GroupRole.Chaser;
            }
            else if (w < tBump)
            {
                cue.face = cue.pos + Tangent(cue.pos, o.d);
                cue.style = isC ? DollStyle.PlayChaser : DollStyle.PlayRunner;
                cue.under = DollStyle.Run;
            }
            else if (w < tDash)
            {
                // The stop: the runner's hand on the trunk (it shakes), then both peek round it at the other.
                cue.face = trunk;
                cue.style = w < tPeek && !isC ? DollStyle.PlayCircle : DollStyle.PlayPeek;
                cue.since = w - tBump;
                if (!isC && w < tPeek + 0.05f) { cue.hitOn = TreeHit; cue.style = DollStyle.PlayCircle; }
                cue.endBeat = w >= tPeek; // the peek is the joke: they see each other
            }
            else if (w < tTag)
            {
                cue.face = isC ? other : cue.pos + Tangent(cue.pos, -o.d);
                cue.style = isC ? DollStyle.PlayChaser : DollStyle.PlayRunner;
                cue.under = DollStyle.Run;
                cue.since = w - tTag;
                if (isC) { cue.hitOn = o.runner; cue.role = GroupRole.Chaser; }
            }
            else
            {
                cue.face = other;
                cue.since = w - tTag;
                cue.endBeat = true;
                cue.style = isC ? DollStyle.PlayChaser : DollStyle.Flinch;
                if (isC) { cue.hitOn = o.runner; cue.role = GroupRole.Chaser; }
            }
            return cue;
        }

        Vector2 Tangent(Vector2 pos, float d) { var r = pos - trunk; return new Vector2(-r.y, r.x).normalized * d * 0.2f; }

        PlayCue DuelCue(int i, float w, PlayCue cue, int cycle)
        {
            cue.stick = true;
            cue.role = GroupRole.Circler;
            var spot = DuelSpot(i);
            var last = RoundOf(cycle * Rounds + Rounds - 1);
            Angles(last, RoundSeconds, out float thC, out float thR);
            var from = OnRing(i == last.chaser ? thC : thR);
            if (w < Walk)
            {
                float u = Easing.Apply(Ease.Smooth, w / Walk);
                cue.pos = Vector2.Lerp(from, spot, u);
                cue.face = spot + (spot - from);
                cue.under = cue.style = DollStyle.Walk;
                cue.speed = (spot - from).magnitude / Walk;
                return cue;
            }
            cue.pos = spot;
            cue.face = DuelSpot(1 - i);
            float x = w - Walk;
            int k = Mathf.FloorToInt(x / StrikeEvery);
            if (k >= Strikes)
            {
                cue.style = DollStyle.Cheer; cue.under = DollStyle.Stand; cue.since = x - Strikes * StrikeEvery; cue.endBeat = true;
                cue.round = -1;
                return cue;
            }
            int attacker = (k + first) % 2;
            float s = x - k * StrikeEvery;
            cue.round = k;
            cue.since = s;
            if (i == attacker) { cue.style = DollStyle.PlayDuel; cue.hitOn = 1 - i; cue.endBeat = s >= Strike.ContactAt; }
            else { cue.style = s < Strike.ContactAt ? DollStyle.PlayBlock : DollStyle.Flinch; cue.endBeat = s >= Strike.ContactAt; } // the jolt at the cross is its answer
            return cue;
        }

        public override void Pose(int i, float t, in PlayCue c, ref DollPose p)
        {
            float tau = Mathf.PI * 2f;
            switch (c.style)
            {
                case DollStyle.PlayCircle:
                    if (c.hitOn == TreeHit) { Slap.Apply(ref p, c.since); break; }
                    if (c.role == GroupRole.Chaser)
                    {
                        // The new "it": two bounces on the spot, arms out, looking at the trunk.
                        p.offset.y += Mathf.Abs(Mathf.Sin(c.since / 0.3f * Mathf.PI)) * 0.03f * (c.since < 1.2f ? 1f : 0f);
                        p.armL = new Vector3(20f, 0f, -60f); p.armR = new Vector3(20f, 0f, 60f);
                    }
                    else Slap.Apply(ref p, c.since);
                    break;
                case DollStyle.PlayPeek:
                {
                    // Peeking round the trunk: a lean sideways (roll 20°) and a step out (0.04), held, back.
                    float u = Mathf.Clamp01((c.since - Bump) / Peek);
                    float k = Mathf.Sin(u * Mathf.PI);
                    float sideSign = (i % 2 == 0) ? 1f : -1f;
                    p.roll = 20f * k * sideSign;
                    p.offset.x += 0.045f * k * sideSign;
                    p.lean = 6f * k;
                    p.armL = new Vector3(40f, 0f, -20f); p.armR = new Vector3(40f, 0f, 20f);
                    break;
                }
                case DollStyle.PlayChaser:
                    if (c.hitOn >= 0 && c.since >= -Touch[0].seconds) Touch.Apply(ref p, c.since + Touch[0].seconds);
                    else { float wv = Mathf.Sin(t * 9f); p.armL = new Vector3(75f + wv * 14f, 0f, -12f); p.armR = new Vector3(75f - wv * 14f, 0f, 12f); p.lean = 16f; }
                    break;
                case DollStyle.PlayRunner:
                { float wv = Mathf.Sin(t * 9f); p.armL = new Vector3(30f + wv * 30f, 0f, -70f); p.armR = new Vector3(30f - wv * 30f, 0f, 70f); p.lean = 6f; break; }
                case DollStyle.Flinch: if (c.stick) Block.Apply(ref p, c.since); else Tagged.Apply(ref p, c.since); break;
                case DollStyle.PlayDuel: Strike.Apply(ref p, c.since); break;
                case DollStyle.PlayBlock: Block.Apply(ref p, c.since); break;
                case DollStyle.Cheer:
                    if (c.round == -1)
                    {
                        // The end beat: both sticks held high, a hop.
                        float u = Easing.Apply(Ease.Back, Mathf.Clamp01(c.since / 0.25f));
                        p.armR = Vector3.LerpUnclamped(new Vector3(40f, 0f, 10f), new Vector3(170f, 0f, 12f), u);
                        p.armL = new Vector3(0f, 0f, -60f);
                        p.offset.y = Mathf.Abs(Mathf.Sin(c.since * 2.5f * tau)) * 0.025f;
                        p.lean = -8f;
                    }
                    break;
            }
        }

        public override void Effects(float from, float to, List<PlayFx> into)
        {
            if (Count < 2 || to <= t0) return;
            // Walk the rounds and the strikes that may hold a contact in (from, to].
            float a = Mathf.Max(0f, from - t0), b = to - t0;
            int c0 = Mathf.FloorToInt(a / CycleSeconds), c1 = Mathf.FloorToInt(b / CycleSeconds);
            for (int cyc = c0; cyc <= c1; cyc++)
            {
                float cs = cyc * CycleSeconds;
                for (int k = 0; k < Rounds; k++)
                {
                    int r = cyc * Rounds + k;
                    var o = RoundOf(r);
                    float rs = cs + k * RoundSeconds;
                    float bumpAt = rs + Split + Chase + Slap.ContactAt;
                    if (bumpAt > a && bumpAt <= b)
                    {
                        Angles(o, Split + Chase, out _, out float thR);
                        var rp = OnRing(thR);
                        var away = (trunk - rp).normalized;
                        into.Add(new PlayFx { kind = PlayFxKind.TreeShake, at = t0 + bumpAt, pos = new Vector3(trunk.x, 0f, trunk.y), aim = new Vector3(away.x, 0f, away.y), by = o.runner, on = -1, scale = 1f });
                        if (!site.snow) into.Add(new PlayFx { kind = PlayFxKind.Leaves, at = t0 + bumpAt, pos = new Vector3(trunk.x, 0.22f, trunk.y), aim = new Vector3(away.x, 0f, away.y), count = 2, by = o.runner, on = -1, scale = 1f });
                    }
                    if (site.autumn)
                        for (float lt = rs + Split + 0.2f; lt < rs + RoundSeconds - After; lt += LeafEvery)
                            if (lt > a && lt <= b)
                            {
                                float w = lt - rs;
                                for (int i = 0; i < 2; i++)
                                {
                                    var p = CirclePos(o, i, w);
                                    into.Add(new PlayFx { kind = PlayFxKind.Leaves, at = t0 + lt, pos = new Vector3(p.x, 0.02f, p.y), aim = Vector3.up, count = 3, by = i, on = -1, scale = 0.8f });
                                }
                            }
                }
                float ds = cs + CircleSeconds + Walk;
                for (int k = 0; k < Strikes; k++)
                {
                    float at = ds + k * StrikeEvery + Strike.ContactAt;
                    if (!(at > a && at <= b)) continue;
                    int attacker = (k + first) % 2;
                    var mid = Vector2.Lerp(DuelSpot(attacker), DuelSpot(1 - attacker), 0.5f);
                    into.Add(new PlayFx { kind = PlayFxKind.Sparks, at = t0 + at, pos = new Vector3(mid.x, 0.1f, mid.y), aim = Vector3.up, by = attacker, on = 1 - attacker, scale = 0.6f });
                }
            }
        }
    }

    // ==================================================================== B3.c Hide and seek
    /// <summary>
    /// Hide and seek (B3.c), 3–4 kids. The seeker counts at the base (a real tent, a tree or the pile), facing it, eyes covered, bobbing;
    /// the others run to real cover within 1 u (a bush, a trunk, a tent), each to the far side from the base, and crouch; now and then one
    /// peeks out. Counting done, the seeker turns and walks round, looking left and right, to the covers nearest first; beside a hider it
    /// points and hops (the Contact): the hider pops up and races back to the base and touches it. The last found, the seeker runs back
    /// too, and all cheer at the base (the end beat). The first found counts next.
    /// </summary>
    public sealed class HideSeekGame : KidGame
    {
        public const float CountFor = 4f, Run = 0.45f, SeekWalk = 0.38f, Find = 0.12f, PointFor = 0.75f, Turn = 0.4f, CheerFor = 1.1f,
            BaseGap = 0.08f, HideGap = 0.06f, PeekFor = 0.5f, Reach = 1f;

        static readonly Beat Point = new Beat(
            Beat.Step(BeatPhase.Act, 0.15f, Ease.Snap, PoseKey.Hold.ArmR(92f, 0f, 8f).Lean(10f).Offset(0f, 0.045f, 0f)),
            Beat.Step(BeatPhase.Contact, Beat.MinContact, Ease.Linear, PoseKey.Hold),
            Beat.Step(BeatPhase.Hold, 0.3f, Ease.Linear, PoseKey.Hold.Offset(0f, 0f, 0f)),
            Beat.Step(BeatPhase.Settle, 0.24f, Ease.Smooth, PoseKey.Base));

        /// <summary>Found: pops up out of the crouch (a stretch, a hop, arms up), held, down.</summary>
        static readonly Beat Pop = new Beat(
            Beat.Step(BeatPhase.Act, 0.12f, Ease.Back, PoseKey.Hold.Squash(1.15f).Offset(0f, 0.05f, 0f).Arms(160f, 30f).Lean(-8f)),
            Beat.Step(BeatPhase.Hold, 0.3f, Ease.Linear, PoseKey.Hold),
            Beat.Step(BeatPhase.Settle, 0.2f, Ease.Smooth, PoseKey.Base));

        static readonly DollStyle[] Joy = { DollStyle.Cheer, DollStyle.Laugh, DollStyle.Wave };

        public sealed class Seek
        {
            public int seeker;
            public int[] order;          // hiders in the order they are found
            public Vector2 countAt;
            public Vector2[] hide, way, find, home;
            public float[] arrive, foundAt, back;
            public float seekerBack, cheer, end;
        }

        readonly PlaySite site;
        readonly PlayCover baseC;
        readonly List<Seek> seeks = new List<Seek>();
        readonly List<PlayCover> cover = new List<PlayCover>();

        public HideSeekGame(int seed, float t0, IReadOnlyList<int> members, Vector2 centre, PlaySite site) : base(PlayGame.HideSeek, seed, t0, members, centre)
        {
            this.site = site ?? new PlaySite();
            baseC = this.site.hasBase ? this.site.home : new PlayCover(centre, 0.1f, CoverKind.Tent);
            foreach (var c in this.site.covers)
                if ((c.pos - baseC.pos).magnitude <= Reach + c.r && (c.pos - baseC.pos).magnitude > baseC.r + c.r + 0.12f) cover.Add(c);
        }

        public PlayCover Base => baseC;
        public int CoverCount => cover.Count;
        public static bool Enough(PlaySite s, int size)
        {
            if (s == null || !s.hasBase) return false;
            int n = 0;
            foreach (var c in s.covers) { float d = (c.pos - s.home.pos).magnitude; if (d <= Reach + c.r && d > s.home.r + c.r + 0.12f) n++; }
            return n >= size - 1;
        }

        Vector2 HomeSpot(int i, int n)
        {
            float a = BeatHash.U01(seed, 4, 359) * Mathf.PI * 2f + i * Mathf.PI * 2f / Mathf.Max(1, n);
            return baseC.pos + Dir(a) * (baseC.r + BaseGap + 0.04f);
        }

        public Seek SeekOf(int k)
        {
            int n = Count;
            while (seeks.Count <= k)
            {
                int j = seeks.Count;
                var s = new Seek();
                s.seeker = j == 0 ? BeatHash.Pick(n, seed, 5, 359) : seeks[j - 1].order[0];
                float start = j == 0 ? 0f : seeks[j - 1].end;
                var toMid = centre - baseC.pos;
                if (toMid.sqrMagnitude < 1e-4f) toMid = Dir(BeatHash.U01(seed, 6, 359) * Mathf.PI * 2f);
                // The first found counts next, from where it waited (a short step in to the base).
                if (j > 0) toMid = seeks[j - 1].home[s.seeker] - baseC.pos;
                if (toMid.sqrMagnitude < 1e-4f) toMid = new Vector2(1f, 0f);
                s.countAt = baseC.pos + toMid.normalized * (baseC.r + BaseGap);
                s.home = new Vector2[n]; s.hide = new Vector2[n]; s.way = new Vector2[n]; s.find = new Vector2[n];
                s.arrive = new float[n]; s.foundAt = new float[n]; s.back = new float[n];
                for (int i = 0; i < n; i++) s.home[i] = j == 0 ? HomeSpot(i, n) : seeks[j - 1].home[i];
                s.home[s.seeker] = s.countAt;
                // Each hider to its own cover, picked in a hashed order (a different hiding place each round).
                var hiders = new List<int>();
                for (int i = 0; i < n; i++) if (i != s.seeker) hiders.Add(i);
                var free = new List<int>();
                for (int c = 0; c < cover.Count; c++) free.Add(c);
                int rot = BeatHash.Pick(Mathf.Max(1, cover.Count), seed, j, 37);
                foreach (int i in hiders)
                {
                    PlayCover c;
                    if (free.Count > 0)
                    {
                        // The nearest free cover to where it starts, after a hashed turn of the list (not always the same bush).
                        int best = -1; float bd = float.MaxValue;
                        for (int q = 0; q < free.Count; q++)
                        {
                            var cc = cover[free[(q + rot) % free.Count]];
                            float d = (cc.pos - s.home[i]).sqrMagnitude + BeatHash.U01(seed * 7 + j, i, q) * 0.3f;
                            if (d < bd) { bd = d; best = (q + rot) % free.Count; }
                        }
                        c = cover[free[best]]; free.RemoveAt(best);
                    }
                    else c = new PlayCover(baseC.pos + Dir(i * 2.1f) * 0.6f, 0.05f, CoverKind.Bush);
                    var away = c.pos - baseC.pos;
                    if (away.sqrMagnitude < 1e-6f) away = new Vector2(1f, 0f);
                    away = away.normalized;
                    s.hide[i] = c.pos + away * (c.r + HideGap);
                    var side = new Vector2(-away.y, away.x) * (BeatHash.U01(seed, j * 13 + i, 38) < 0.5f ? 1f : -1f);
                    s.way[i] = c.pos + side * (c.r + 0.1f);
                    s.find[i] = s.hide[i] + side * Find;
                    s.arrive[i] = start + (s.hide[i] - s.home[i]).magnitude / Run;
                }
                // The seeker goes round, nearest first (greedy from where it is).
                var left = new List<int>(hiders);
                var orderL = new List<int>();
                var at = s.countAt;
                float tt = start + CountFor + Turn;
                while (left.Count > 0)
                {
                    int best = 0; float bd = float.MaxValue;
                    for (int q = 0; q < left.Count; q++) { float d = (s.way[left[q]] - at).magnitude; if (d < bd) { bd = d; best = q; } }
                    int i = left[best]; left.RemoveAt(best);
                    tt += ((s.way[i] - at).magnitude + (s.find[i] - s.way[i]).magnitude) / SeekWalk;
                    s.foundAt[i] = tt + Point.ContactAt;
                    tt = s.foundAt[i] + PointFor - Point.ContactAt;
                    s.back[i] = s.foundAt[i] + 0.55f + (s.home[i] - s.hide[i]).magnitude / Run;
                    at = s.find[i];
                    orderL.Add(i);
                }
                s.order = orderL.ToArray();
                if (s.order.Length == 0) s.order = new[] { s.seeker };
                s.seekerBack = tt + (s.countAt - at).magnitude / Run;
                float allBack = s.seekerBack;
                foreach (int i in hiders) allBack = Mathf.Max(allBack, s.back[i]);
                s.cheer = allBack + 0.3f;
                s.end = s.cheer + CheerFor + 0.6f;
                // The next round's home spots: where they are at the end (the first found counts next, so it moves to the count spot).
                seeks.Add(s);
            }
            return seeks[k];
        }

        public int SeekIndexAt(float local)
        {
            int k = 0;
            SeekOf(0);
            while (true) { SeekOf(k + 1); if (seeks[k].end > local) return k; k++; }
        }

        Vector2 SeekerPos(Seek s, float x)
        {
            // Along its route: count spot → (beside each cover, then beside its hider) → back.
            var at = s.countAt;
            float tt = SeekStart(s) + CountFor + Turn;
            if (x < tt) return at;
            foreach (int i in s.order)
            {
                if (i == s.seeker) break;
                float l1 = (s.way[i] - at).magnitude, l2 = (s.find[i] - s.way[i]).magnitude;
                float walkEnd = tt + (l1 + l2) / SeekWalk;
                if (x < walkEnd)
                {
                    float d = (x - tt) * SeekWalk;
                    return d < l1 ? Vector2.Lerp(at, s.way[i], l1 > 0f ? d / l1 : 1f) : Vector2.Lerp(s.way[i], s.find[i], l2 > 0f ? (d - l1) / l2 : 1f);
                }
                tt = walkEnd + PointFor;
                at = s.find[i];
                if (x < tt) return at;
            }
            if (x < s.seekerBack) return Vector2.Lerp(at, s.countAt, Local01(x, tt, s.seekerBack));
            return s.countAt;
        }

        float SeekStart(Seek s) { int k = seeks.IndexOf(s); return k <= 0 ? 0f : seeks[k - 1].end; }

        Vector2 HiderPos(Seek s, int i, float x)
        {
            float start = SeekStart(s);
            if (x < s.arrive[i]) return Vector2.Lerp(s.home[i], s.hide[i], Local01(x, start, s.arrive[i]));
            float go = s.foundAt[i] + 0.55f;
            if (x < go) return s.hide[i];
            if (x < s.back[i]) return Vector2.Lerp(s.hide[i], s.home[i], Local01(x, go, s.back[i]));
            return s.home[i];
        }

        public override PlayCue Cue(int i, float t)
        {
            float local = Mathf.Max(0f, t - t0);
            int k = SeekIndexAt(local);
            var s = seeks[k];
            float start = SeekStart(s);
            var cue = new PlayCue { hitOn = NoHit, round = k, under = DollStyle.Stand, style = DollStyle.Stand };
            if (i == s.seeker)
            {
                cue.role = GroupRole.Seeker;
                cue.pos = SeekerPos(s, local);
                cue.speed = SpeedOf(x => SeekerPos(s, x), local);
                float seekAt = start + CountFor;
                if (local < seekAt) { cue.style = DollStyle.PlayCount; cue.face = baseC.pos; cue.since = local - start; return cue; }
                if (local < seekAt + Turn) { cue.style = DollStyle.PlaySeek; cue.face = Vector2.Lerp(baseC.pos, s.order.Length > 0 ? s.way[s.order[0]] : centre, Easing.Apply(Ease.Smooth, (local - seekAt) / Turn)); cue.since = -1f; return cue; }
                // Seeking: walking with a look about; at each hider the point and hop.
                foreach (int h in s.order)
                {
                    if (h == s.seeker) break;
                    float pointStart = s.foundAt[h] - Point.ContactAt;
                    if (local >= pointStart - 0.05f && local < pointStart + PointFor)
                    {
                        cue.style = DollStyle.Point; cue.under = DollStyle.Stand; cue.face = s.hide[h]; cue.hitOn = h; cue.since = local - s.foundAt[h];
                        cue.endBeat = local >= s.foundAt[h];
                        return cue;
                    }
                }
                if (local < s.seekerBack - 0.02f)
                {
                    var ahead = SeekerPos(s, local + 0.25f);
                    cue.face = (ahead - cue.pos).sqrMagnitude > 1e-5f ? ahead : cue.pos + (cue.pos - SeekerPos(s, local - 0.25f));
                    bool back = local > s.foundAt[s.order[s.order.Length - 1]] + 0.3f;
                    cue.style = back ? DollStyle.Run : DollStyle.PlaySeek;
                    cue.under = cue.speed > 0.03f ? (back ? DollStyle.Run : DollStyle.Walk) : DollStyle.Stand;
                    cue.since = local - seekAt;
                    return cue;
                }
            }
            else
            {
                cue.role = GroupRole.Hider;
                cue.pos = HiderPos(s, i, local);
                cue.speed = SpeedOf(x => HiderPos(s, i, x), local);
                if (local < s.arrive[i]) { cue.style = DollStyle.Run; cue.under = DollStyle.Run; cue.face = s.hide[i]; cue.since = local - start; return cue; }
                if (local < s.foundAt[i])
                {
                    cue.style = DollStyle.PlayHide; cue.under = DollStyle.Crouch; cue.face = baseC.pos; cue.since = local - s.arrive[i];
                    return cue;
                }
                if (local < s.foundAt[i] + 0.55f) { cue.style = DollStyle.PlayFound; cue.face = SeekerPos(s, local); cue.since = local - s.foundAt[i]; cue.endBeat = true; return cue; }
                if (local < s.back[i]) { cue.style = DollStyle.Run; cue.under = DollStyle.Run; cue.face = s.home[i]; cue.since = local - s.foundAt[i]; return cue; }
            }
            // At the base: the touch on arrival, waiting, then everyone's cheer.
            cue.pos = s.home[i];
            cue.face = baseC.pos;
            float back2 = i == s.seeker ? s.seekerBack : s.back[i];
            float cheerAt = s.cheer + Desync.InGroup(seed + k * 7, i, Count).delay; // not in step (P6): the cheer goes round them
            if (local >= cheerAt && local < cheerAt + CheerFor)
            {
                // Everyone at the base: a cheer, a laugh, a wave, round them (never three doing one move, P6).
                var joy = Joy[(i + k) % Joy.Length];
                cue.style = joy; cue.under = joy; cue.since = local - cheerAt; cue.endBeat = true; cue.role = GroupRole.Cheerer;
            }
            else if (local >= back2 && local < back2 + 0.45f) { cue.style = DollStyle.PlayTouch; cue.since = local - back2; cue.endBeat = true; }
            else { cue.since = local - back2; cue.endBeat = local >= back2; }
            return cue;
        }

        public override void Pose(int i, float t, in PlayCue c, ref DollPose p)
        {
            float tau = Mathf.PI * 2f;
            switch (c.style)
            {
                case DollStyle.PlayCount:
                    // Face to the base, lean in, both hands over the eyes, bobbing as it counts (6 in 4 s).
                    p.lean = 20f;
                    p.armL = new Vector3(150f, 0f, -40f); p.armR = new Vector3(150f, 0f, 40f);
                    p.offset.y = Mathf.Abs(Mathf.Sin(c.since * 1.5f * Mathf.PI)) * 0.02f;
                    break;
                case DollStyle.PlaySeek:
                    // Looking left and right as it goes (yaw ±35), one hand shading its eyes.
                    p.yaw += Mathf.Sin(t * 1.4f * tau * 0.5f + i) * 35f;
                    p.armR = new Vector3(140f, 0f, 30f);
                    break;
                case DollStyle.Point: Point.Apply(ref p, c.since + Point.ContactAt); break;
                case DollStyle.PlayHide:
                {
                    // Crouched low behind the cover; every 2–3 s a peek out to one side (0.04 u, roll 15°) for 0.5 s.
                    p.squash = 0.7f; p.lean = 10f; p.offset.y = 0f;
                    p.armL = new Vector3(30f, 0f, -25f); p.armR = new Vector3(30f, 0f, 25f);
                    float every = BeatHash.Range(2f, 3f, seed, i, 39);
                    float ph = Mathf.Repeat(c.since - 0.8f, every);
                    if (c.since > 0.8f && ph < PeekFor)
                    {
                        float k = Mathf.Sin(ph / PeekFor * Mathf.PI);
                        float sgn = (i & 1) == 0 ? 1f : -1f;
                        p.offset.x += 0.045f * k * sgn; p.roll += 15f * k * sgn; p.squash = Mathf.Lerp(0.7f, 0.8f, k);
                    }
                    break;
                }
                case DollStyle.PlayFound: Pop.Apply(ref p, c.since); break;
                case DollStyle.PlayTouch:
                {
                    float k = Mathf.Sin(Mathf.Clamp01(c.since / 0.45f) * Mathf.PI);
                    p.armR = Vector3.Lerp(p.armR, new Vector3(90f, 0f, 8f), k); p.lean = 10f * k;
                    break;
                }
            }
        }

        public override void Effects(float from, float to, List<PlayFx> into)
        {
            if (to <= t0) return;
            float a = Mathf.Max(0f, from - t0), b = to - t0;
            int k0 = SeekIndexAt(a), k1 = SeekIndexAt(b);
            for (int k = Mathf.Max(0, k0 - 1); k <= k1; k++)
            {
                var s = seeks[k];
                for (int i = 0; i < Count; i++)
                {
                    if (i == s.seeker) continue;
                    // The bush rustles as a kid dives in behind it, and again as it pops up.
                    var c = CoverAt(s.hide[i]);
                    if (c.kind != CoverKind.Bush && c.kind != CoverKind.Tree) continue;
                    var kind = c.kind == CoverKind.Bush ? PlayFxKind.BushShake : PlayFxKind.TreeShake;
                    var away = (c.pos - baseC.pos).normalized;
                    foreach (float at in new[] { s.arrive[i], s.foundAt[i] })
                        if (at > a && at <= b) into.Add(new PlayFx { kind = kind, at = t0 + at, pos = new Vector3(c.pos.x, 0f, c.pos.y), aim = new Vector3(away.x, 0f, away.y), by = i, on = -1, scale = 1f });
                }
            }
        }

        PlayCover CoverAt(Vector2 hide)
        {
            PlayCover best = default; float bd = float.MaxValue;
            foreach (var c in cover) { float d = (c.pos - hide).magnitude - c.r; if (d < bd) { bd = d; best = c; } }
            return best;
        }
    }

    // ==================================================================== Snowball (the B2 stretch)
    /// <summary>
    /// A snowball fight (B2 stretch, §0.6 Snowball), 2–4 kids on a ring 0.4–0.5 u across: one bends and scoops snow, winds its arm back,
    /// throws (the release: a snowball on an arc, h 0.12), it bursts on the target in a puff of snow, the target flinches, then scoops and
    /// throws back. After five throws they all laugh (the end beat). Replaces the last Hop of kids' play.
    /// </summary>
    public sealed class SnowballGame : KidGame
    {
        public const float Scoop = 0.45f, Antic = 0.25f, Act = 0.12f, Speed = 1.1f, Apex = 0.12f, BallR = 0.02f, LaughFor = 1.2f;

        static readonly Beat Throw = new Beat(
            Beat.Step(BeatPhase.Approach, Scoop * 0.5f, Ease.Smooth, PoseKey.Hold.Lean(34f).Squash(0.9f).Arms(40f, 10f)),
            Beat.Step(BeatPhase.Approach, Scoop * 0.5f, Ease.Smooth, PoseKey.Hold.Lean(0f).Squash(1f).ArmL(30f, 0f, -20f).ArmR(40f, 0f, 14f)),
            Beat.Step(BeatPhase.Antic, Antic, Ease.Out, PoseKey.Hold.ArmR(-40f, 0f, 20f).Lean(-8f).Squash(0.92f)),
            Beat.Step(BeatPhase.Act, Act, Ease.Snap, PoseKey.Hold.ArmR(150f, 0f, 10f).Lean(14f).Squash(1.04f)),
            Beat.Step(BeatPhase.Contact, Beat.MinContact, Ease.Linear, PoseKey.Hold),
            Beat.Step(BeatPhase.Hold, 0.15f, Ease.Linear, PoseKey.Hold),
            Beat.Step(BeatPhase.Settle, 0.35f, Ease.Smooth, PoseKey.Base));

        static readonly Beat Hit = new Beat(
            Beat.Step(BeatPhase.Act, 0.1f, Ease.Out, PoseKey.Hold.Offset(0f, 0f, 0.045f).Squash(0.88f).Arms(75f, 30f).Lean(-12f).Yaw(25f)),
            Beat.Step(BeatPhase.Hold, 0.3f, Ease.Linear, PoseKey.Hold),
            Beat.Step(BeatPhase.Settle, 0.3f, Ease.Smooth, PoseKey.Base));

        static DollPose ReleasePose { get { var p = DollPose.Rest; p.lean = 14f; p.squash = 1.04f; p.armR = new Vector3(150f, 0f, 10f); p.armL = new Vector3(30f, 0f, -20f); return p; } }

        public struct Shot { public int from, to; public float start, launch, land, end; public ArcShot arc; }

        readonly Vector2[] spot;
        readonly List<Shot> shots = new List<Shot>();
        readonly int every;

        public SnowballGame(int seed, float t0, IReadOnlyList<int> members, Vector2 centre) : base(PlayGame.Snowball, seed, t0, members, centre)
        {
            int n = Count;
            spot = new Vector2[n];
            float a0 = BeatHash.U01(seed, 1, 360) * Mathf.PI * 2f, r = n <= 2 ? 0.22f : 0.25f;
            for (int i = 0; i < n; i++) spot[i] = centre + Dir(a0 + i * Mathf.PI * 2f / Mathf.Max(1, n)) * r;
            every = 5;
        }

        public override bool HasBall => true;
        public Vector2 SpotOf(int i) => spot[i];

        public Shot ShotAt(int k)
        {
            int n = Count;
            while (shots.Count <= k)
            {
                int j = shots.Count;
                var s = new Shot();
                var prev = j > 0 ? shots[j - 1] : default;
                s.from = j == 0 ? BeatHash.Pick(n, seed, 2, 360) : prev.to;
                s.to = n <= 1 ? s.from : (s.from + 1 + BeatHash.Pick(Mathf.Max(1, n - 1), seed, j, 361)) % n;
                if (s.to == s.from && n > 1) s.to = (s.from + 1) % n;
                s.start = j == 0 ? 0.5f : prev.end;
                s.launch = s.start + Throw.ContactAt;
                var from = spot[s.from]; var to = spot[s.to];
                var start = Hand1(from, YawTo(from, to), ReleasePose, scales[s.from]);
                var end = new Vector3(to.x, KidScale * BodyH * 0.7f, to.y);
                float dist = (to - from).magnitude;
                s.arc = ArcShot.Throw(start, end, Apex, Arc.FlightFor(dist, Speed), Landing.Burst, s.launch, 360f, BallR);
                s.land = s.arc.LandAt;
                bool laugh = (j + 1) % every == 0;
                s.end = s.land + Hit.Duration + 0.2f + (laugh ? LaughFor + 0.2f : 0f) + BeatHash.Range(0f, 0.4f, seed, j, 362);
                shots.Add(s);
            }
            return shots[k];
        }

        /// <summary>The right hand (the snowball is thrown one-handed).</summary>
        static Vector3 Hand1(Vector2 pos, float yawDeg, in DollPose p, float s)
        {
            var yawQ = Quaternion.Euler(0f, yawDeg + p.yaw, 0f);
            var rot = yawQ * Quaternion.Euler(-p.lean, 0f, p.roll);
            var root = new Vector3(pos.x, 0f, pos.y) + yawQ * (p.offset * s);
            var local = new Vector3(ShoulderX * s, BodyH * 0.92f * s, 0f) + Quaternion.Euler(p.armR) * new Vector3(0f, -ArmLen * s, 0f);
            return root + rot * new Vector3(local.x, local.y * p.squash, local.z);
        }

        public int ShotIndexAt(float local)
        {
            int k = 0;
            ShotAt(0);
            while (true) { ShotAt(k + 1); if (shots[k + 1].start > local) return k; k++; }
        }

        public override PlayCue Cue(int i, float t)
        {
            float local = Mathf.Max(0f, t - t0);
            int k = ShotIndexAt(local);
            var s = shots[k];
            var cue = new PlayCue { hitOn = NoHit, round = k, pos = spot[i], under = DollStyle.Stand, style = DollStyle.Stand, role = GroupRole.Watcher, face = spot[s.from] };
            if (local < s.start) { cue.face = centre; return cue; }
            if (i == s.from && local < s.launch + 0.5f)
            {
                cue.role = GroupRole.Thrower; cue.style = DollStyle.PlaySnowball; cue.face = spot[s.to]; cue.hitOn = Release; cue.since = local - s.start;
                cue.endBeat = local >= s.launch;
                return cue;
            }
            if (i == s.to && local >= s.land && local < s.land + Hit.Duration)
            {
                cue.role = GroupRole.Catcher; cue.style = DollStyle.Flinch; cue.face = spot[s.from]; cue.since = local - s.land; cue.endBeat = true;
                return cue;
            }
            bool laugh = (k + 1) % every == 0;
            if (laugh && local >= s.land + Hit.Duration + 0.2f)
            {
                // The end beat: they laugh, cheer, wave, round them (never three doing one move, P6).
                var joy = (i + k) % 3 == 0 ? DollStyle.Laugh : (i + k) % 3 == 1 ? DollStyle.Cheer : DollStyle.Wave;
                cue.style = cue.under = joy; cue.since = local - s.land - Hit.Duration - 0.2f; cue.endBeat = true; cue.face = centre; return cue;
            }
            if (i == s.to) cue.face = spot[s.from];
            return cue;
        }

        public override void Pose(int i, float t, in PlayCue c, ref DollPose p)
        {
            if (c.style == DollStyle.PlaySnowball) Throw.Apply(ref p, c.since);
            else if (c.style == DollStyle.Flinch) Hit.Apply(ref p, c.since);
        }

        public override BallState Ball(float t)
        {
            float local = Mathf.Max(0f, t - t0);
            int k = ShotIndexAt(local);
            var s = shots[k];
            var b = new BallState { shotIndex = k, shot = s.arc, holder = -1, stage = ArcStage.Gone };
            if (local < s.start + Scoop) return b;      // not made yet
            if (local < s.launch) { b.holder = s.from; b.stage = ArcStage.Resting; return b; }
            if (local < s.land)
            {
                var a = Arc.Sample(s.arc, local);
                b.position = a.position; b.rotation = a.rotation; b.stage = ArcStage.Flying;
            }
            return b;
        }

        public override void Effects(float from, float to, List<PlayFx> into)
        {
            if (to <= t0) return;
            float a = Mathf.Max(0f, from - t0), b = to - t0;
            int k0 = ShotIndexAt(a), k1 = ShotIndexAt(b);
            for (int k = Mathf.Max(0, k0 - 1); k <= k1; k++)
            {
                var s = shots[k];
                if (s.land > a && s.land <= b)
                    into.Add(new PlayFx { kind = PlayFxKind.Snow, at = t0 + s.land, pos = s.arc.end, by = s.from, on = s.to, scale = 1f });
                float scoop = s.start + Scoop * 0.5f;
                if (scoop > a && scoop <= b)
                {
                    var p = spot[s.from] + (spot[s.to] - spot[s.from]).normalized * 0.06f;
                    into.Add(new PlayFx { kind = PlayFxKind.Snow, at = t0 + scoop, pos = new Vector3(p.x, 0.01f, p.y), by = s.from, on = -1, scale = 0.5f });
                }
            }
        }
    }
}
