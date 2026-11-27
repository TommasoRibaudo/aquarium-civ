using System;
using System.Collections.Generic;
using AquariumCiv.Lighting;
using AquariumCiv.Sim;
using UnityEngine;

namespace AquariumCiv.View
{
    /// <summary>
    /// #783 (docs/design/783-story-camera.md, option A, user 2026-11-26: every held scene): the camera leans in at a held scene's start and
    /// eases back after. It reuses the arrivals' lean (<see cref="BoxCamera.LeanIn"/>: 1.4 s smoother-step, pitch to 52 at most, yaw untouched,
    /// the way home is the player's own pose saved at the start, <see cref="BoxCamera.ReleaseLean"/> then the glide back) and adds only the
    /// scene's own aim: <see cref="CampLife.FocusNow"/> for a story, the cast's middle for the staged ceremonies.
    /// <list type="bullet">
    /// <item>Only when <see cref="GlassVoice.CameraFree"/> (no sheet, card, wipe, tool, photo mode, Show me), never while following or while a
    /// finger moves the camera, never for the box-wide festival (no focus) or a scene with no cast drawn. Hunts are not held moments (#426).</item>
    /// <item>The zoom puts a standing doll at 5 % of the screen's height (<see cref="StoryLean.ZoomFor"/>), never closer than the player's floor and
    /// never farther out than the player's own zoom; a scene already framed does not move the camera.</item>
    /// <item>The player wins: a two-finger move, a pan or a scroll cancels (<see cref="BoxCamera.LeanCancels"/>), nothing returns the camera and the
    /// scene is not leaned on again.</item>
    /// <item>One lean per scene; 20 s cooldown after a lean, except the next of two queued scenes chains on it (one way home).</item>
    /// </list>
    /// </summary>
    public static class HeldMomentLean
    {
        /// <summary>Tests that count dolls at the default view switch the lean off (PlayTestBase does; a test of the lean turns it back on).</summary>
        public static bool Enabled = true;
        /// <summary>Leans made, scenes the player took the camera from, scenes that needed no move, scenes refused (tests, the panel).</summary>
        public static int Leans, Cancelled, Framed, Refused;
        /// <summary>Why the last scene did or did not lean.</summary>
        public static string LastWhy = "";
        /// <summary>The last lean's aim (world) and the zoom it was sent to (tests).</summary>
        public static Vector3 LastAim;
        public static float LastZoomTo, LastDollPx, LastExtX, LastExtY;
        /// <summary>The last look of a held lean (tests, the panel): where the scene sat and what the lean did.</summary>
        public static string LastLook = "";
        /// <summary>A lean holds the camera for a scene now.</summary>
        public static bool Holding => cur != null;

        sealed class Pending { public MomentStaging staging; public float until, next; public bool force; }
        sealed class Active { public int id; public int cancelsSeen; public float nextLook; public bool dead; public MomentStaging staging; }

        static Pending pending;
        static Active cur;
        static float lastRelease = float.NegativeInfinity;
        /// <summary>How long a scene is given to have its cast drawn (real seconds), and how often a held lean looks at where the scene went.</summary>
        const float WaitForCast = 4f, LookEvery = 1f, TightenBy = 0.02f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset() { pending = null; cur = null; lastRelease = float.NegativeInfinity; Leans = Cancelled = Framed = Refused = 0; LastWhy = ""; Enabled = true; }

        /// <summary>The stage has started <paramref name="st"/>: lean on it once its cast is drawn (tried for a few seconds).</summary>
        public static void Begin(MomentStaging st, bool force = false)
        {
            if (st == null || st.Moment == null) return;
            if (!Enabled && !force) { LastWhy = "the lean is off"; return; }
            if (st.Moment.kind == MomentKind.Festival) { LastWhy = "a box-wide festival has no focus"; return; }
            if (st.Focus == null && st.Cast == null) { LastWhy = "no cast to frame"; return; }
            pending = new Pending { staging = st, until = Time.unscaledTime + WaitForCast, force = force };
            TryPending();
        }

        /// <summary>Every frame (HeldMomentStage.Update).</summary>
        public static void Tick()
        {
            if (pending != null) TryPending();
            if (cur == null) return;
            var cam = Cam;
            if (cam == null) { cur = null; return; }
            if (cam.LeanCancels != cur.cancelsSeen) { Cancelled++; LastWhy = "the player took the camera"; cur.dead = true; cur = null; return; } // nothing returns it
            if (!Playing(cur.id)) { Release(cam); return; }
            if (Time.unscaledTime < cur.nextLook) return;
            cur.nextLook = Time.unscaledTime + LookEvery;
            if (!Look(cur.staging, cam, out var world, out float zoomTo, out float vx, out float vy)) return;
            if (StoryLean.LeftTheFrame(vx, vy)) { LastWhy = "the scene left the frame"; Release(cam); return; }
            bool closer = zoomTo < cam.zoom - TightenBy; // the cast has gathered (a lead walked in from far): the lean comes closer, never farther out
            bool follow = StoryLean.NeedsFollow(vx, vy), re = false;
            if ((follow || closer) && !cam.Touching)
            {
                var c = cur;
                if (cam.LeanIn(world, closer ? zoomTo : cam.zoom, () => !c.dead && Playing(c.id), 0.9f)) { c.cancelsSeen = cam.LeanCancels; LastAim = world; re = true; }
            }
            LastLook = $"t {Time.unscaledTime:0.0}: at ({vx:0.00}, {vy:0.00}) follow {follow} closer {closer} touching {cam.Touching} re-aimed {re}";
        }

        static void Release(BoxCamera cam)
        {
            lastRelease = Time.time;
            cur = null;
            cam.ReleaseLean();
        }

        static bool Playing(int id) { var s = HeldMomentStage.Instance; return s != null && s.IsPlaying(id); }
        static BoxCamera Cam => Camera.main != null ? Camera.main.GetComponent<BoxCamera>() : null;

        static void TryPending()
        {
            var p = pending;
            if (p == null) return;
            if (!Playing(p.staging.Moment.id)) { pending = null; return; }
            if (Time.unscaledTime < p.next) return;
            p.next = Time.unscaledTime + 0.25f;
            var cam = Cam;
            string why = cam == null || !cam.isActiveAndEnabled ? "no camera" : TryLean(cam, p);
            if (why == null) { pending = null; return; } // leaned, or no move was needed
            LastWhy = why;
            if (Time.unscaledTime >= p.until) { pending = null; Refused++; }
        }

        /// <summary>The checks and the lean. Null = leaned (or framed: no move needed); else why not (tried again until the wait runs out).</summary>
        static string TryLean(BoxCamera cam, Pending p)
        {
            if (!p.force)
            {
                if (!GlassVoice.CameraFree()) return "the player has the camera (a card, a sheet, a tool, photo mode)";
                if (cam.IsFollowing) return "following a person";
                if (cam.Leaning && cur == null) return "another lean holds the camera";
                float? since = float.IsNegativeInfinity(lastRelease) ? (float?)null : Time.time - lastRelease;
                if (cur == null && !StoryLean.MayLean(since)) { pending = null; Refused++; return "cooling down after the last lean"; }
            }
            if (cam.Touching) return "a finger is on the camera";
            if (!Look(p.staging, cam, out var world, out float zoomTo, out float vx, out float vy)) return "no cast drawn yet";
            if (!StoryLean.NeedsMove(cam.zoom, zoomTo, vx, vy)) { Framed++; return "the scene is already framed"; }
            var a = new Active { id = p.staging.Moment.id, staging = p.staging };
            if (!cam.LeanIn(world, zoomTo, () => !a.dead && Playing(a.id))) return "the camera refused (a finger moving it)";
            a.cancelsSeen = cam.LeanCancels;
            a.nextLook = Time.unscaledTime + LookEvery;
            cur = a;
            Leans++;
            LastAim = world; LastZoomTo = zoomTo;
            LastWhy = "leaned to zoom " + zoomTo.ToString("0.00");
            return null;
        }

        // ---------------------------------------------------------------- the aim

        static readonly List<Vector2> cast = new List<Vector2>();

        /// <summary>Where the scene is and how close the camera should be: the focus in the world, the zoom for <see cref="StoryLean.ScenePxShare"/>,
        /// the focus's viewport point. False when nothing of it is drawn.</summary>
        static bool Look(MomentStaging st, BoxCamera cam, out Vector3 world, out float zoomTo, out float vx, out float vy)
        {
            world = default; zoomTo = cam.zoom; vx = vy = 0.5f;
            var box = UnityEngine.Object.FindAnyObjectByType<BoxView>();
            var c = Camera.main;
            if (box == null || c == null) return false;
            cast.Clear();
            try { st.Cast?.Invoke(cast); } catch (Exception exc) { Debug.LogException(exc); }
            Vector2? focus = null;
            try { focus = st.Focus?.Invoke(); } catch (Exception exc) { Debug.LogException(exc); }
            Vector2? anchor = focus; // the spec's aim: the scene's thing pulled toward its lead (a story), else the cast's own middle (below)
            if (cast.Count > 0 && (focus == null || st.Cast != null && st.Focus != null))
            {   // the middle of the cast's box (a story: the thing and its people), so a lead walking in does not pull the aim off the scene
                Vector2 lo = cast[0], hi = cast[0];
                foreach (var q in cast) { lo = Vector2.Min(lo, q); hi = Vector2.Max(hi, q); }
                focus = (lo + hi) * 0.5f;
                if (anchor == null) anchor = Median(cast);
            }
            if (focus == null) return false;
            if (cast.Count == 0) cast.Add(focus.Value);
            float h = CampFrame.DollHeight * CameraReach.DollScale(cam.zoom, PeopleView.DollScaleAmount);
            Vector3 At(Vector2 p, float up) => box.Root.TransformPoint(new Vector3(p.x, box.SurfaceHeight(p.x, p.y) + up, p.y));
            if (!Extent(focus.Value, out var fv, out float ex, out float ey)) return false;
            // the cast doesn't fit the view and the lean never zooms out to make it fit (a lead still walking in from far, somebody at the back
            // of the box): the box's middle would be empty ground between them, so the lean frames the scene itself (its thing, else the
            // middle of where most of its people are) and the straggler walks into the frame.
            if (StoryLean.CastExtent(ex, ey) > 1f && anchor is Vector2 a && (a - focus.Value).sqrMagnitude > 1e-4f && Extent(a, out var av, out float ax, out float ay))
            { focus = a; fv = av; ex = ax; ey = ay; }
            var f = focus.Value;
            world = At(f, 0.06f);
            vx = fv.x; vy = fv.y;
            var foot = c.WorldToScreenPoint(At(f, 0f)); var head = c.WorldToScreenPoint(At(f, h));
            float dollPx = Mathf.Abs(head.y - foot.y);
            LastDollPx = dollPx; LastExtX = ex; LastExtY = ey;
            zoomTo = StoryLean.ZoomFor(dollPx, c.pixelHeight, cam.zoom, StoryLean.CastExtent(ex, ey), cam.pitch, Mathf.Max(cam.pitch, BoxCamera.LeanPitch), cam.minZoom, PeopleView.DollScaleAmount);
            return true;

            // the viewport point of an aim and the cast's spread round it (both ways, the whole height of a doll)
            bool Extent(Vector2 at, out Vector3 v0, out float exOut, out float eyOut)
            {
                exOut = eyOut = 0f;
                v0 = c.WorldToViewportPoint(At(at, 0.06f));
                if (v0.z <= 0f) return false;
                foreach (var p in cast)
                    foreach (float up in new[] { 0f, h })
                    {
                        var v = c.WorldToViewportPoint(At(p, up));
                        if (v.z <= 0f) continue;
                        exOut = Mathf.Max(exOut, Mathf.Abs(v.x - v0.x) * 2f); eyOut = Mathf.Max(eyOut, Mathf.Abs(v.y - v0.y) * 2f);
                    }
                return true;
            }
        }

        static readonly List<float> sortX = new List<float>(), sortZ = new List<float>();

        /// <summary>Where most of the cast stands: the median of their x and of their z (one far straggler does not move it).</summary>
        static Vector2 Median(List<Vector2> pts)
        {
            sortX.Clear(); sortZ.Clear();
            foreach (var p in pts) { sortX.Add(p.x); sortZ.Add(p.y); }
            sortX.Sort(); sortZ.Sort();
            int n = pts.Count;
            float Mid(List<float> l) => n % 2 == 1 ? l[n / 2] : (l[n / 2 - 1] + l[n / 2]) * 0.5f;
            return new Vector2(Mid(sortX), Mid(sortZ));
        }

        // ---------------------------------------------------------------- debug (#783)

        /// <summary>Cheat: lean in on the scene playing now, whatever the cooldown and the player's busy-ness say (a finger on the camera still
        /// stops it).</summary>
        public static string DebugLeanNow()
        {
            var s = HeldMomentStage.Instance;
            var st = s == null ? null : s.AnyPlayingStaging();
            if (st == null) return "no held scene is playing";
            var cam = Cam;
            if (cur != null && cam != null) Release(cam);
            lastRelease = float.NegativeInfinity;
            Begin(st, force: true);
            return LastWhy;
        }

        /// <summary>Cheat: the lean on or off for every held scene.</summary>
        public static string DebugToggle() { Enabled = !Enabled; return "held-scene lean " + (Enabled ? "on" : "off"); }

        /// <summary>Tests: forget the cooldown and any lean held (the camera is left as it is).</summary>
        public static void DebugForget() { pending = null; cur = null; lastRelease = float.NegativeInfinity; }
    }
}
