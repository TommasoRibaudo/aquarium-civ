using System.Collections;
using UnityEngine;

namespace AquariumCiv.Prototypes.P1
{
    /// <summary>
    /// P1 Glass &amp; Mess: the A/B cleaning gesture (prototype-plan.md §5). Same mess, same juice; only the gesture differs.
    /// A "wipe": a one-finger drag on the tank wipes the glass it crosses; a fast swipe across trash or weeds flicks
    ///   them out; tapping mess only wiggles it. Two fingers, or a drag beside the tank, turn the tank.
    /// B "tap": tap a smudge and a patch of glass pops clean; tap trash or weeds to pop them; drag turns the tank.
    /// </summary>
    public class CleaningGestures : MonoBehaviour, IDragTool
    {
        public GlassGrime glass;
        public MessView mess;

        [Header("A: wipe")]
        [Tooltip("Wipe radius as a share of screen height (about a fingertip and a half).")] public float wipeRadius = 0.065f;
        [Tooltip("Share of grime removed per stamp at the center (stamps overlap along a stroke).")] public float wipeStrength = 0.5f;
        [Tooltip("Swipe speed (screen heights per second) that flicks a piece of mess.")] public float flickSpeed = 0.5f;

        [Header("B: tap")]
        [Tooltip("Patch radius as a share of screen height.")] public float tapRadius = 0.1f;
        public float tapStrength = 0.97f;
        [Tooltip("Below this local grime a tap on the glass goes through to the world (focus, citizens).")] public float tapMinGrime = 0.06f;

        [Header("Lab")]
        [Tooltip("Lab tests start each variant from the same messy tank.")] public bool messyOnVariantStart = true;

        public bool WipeMode => PrototypeConfig.Current.Variant == "A";

        GlassGrime.Pane lastPane;
        Vector2 lastUv, lastScreen;
        bool hasLast;
        float strokeRemoved, strokeMessBefore, strokeGrimeBefore, strokeStart, glintBudget;

        Camera Cam => OrbitCamera.Instance.Cam;

        void OnEnable()
        {
            OrbitCamera.DragTool = this;
            OrbitCamera.TapClaim = OnTap;
        }

        void OnDisable()
        {
            if (ReferenceEquals(OrbitCamera.DragTool, this)) OrbitCamera.DragTool = null;
            OrbitCamera.TapClaim = null;
        }

        IEnumerator Start()
        {
            PrototypeConfig.Current.VariantChanged += _ => StartVariant();
            mess.PropTapRefused += piece => HudToast.Show("Swipe across it to flick it out", piece.transform.position + Vector3.up * 0.3f);
            glass.PaneCleared += (pane, center) => { Juice.Chime(); Juice.Glints(center, 30, 0.5f); };
            yield return null; // after SimClock's welcome-back banner, so the gesture hint wins
            StartVariant();
        }

        void StartVariant()
        {
            mess.tapCleansProps = !WipeMode;
            if (PrototypeConfig.Current.testMode == TestMode.Lab && messyOnVariantStart) mess.MakeMessy();
            HudToast.ShowBanner(WipeMode
                ? "Drag across the glass to wipe it clean. Swipe across junk and weeds to flick them out. Two fingers turn the tank."
                : "Tap a smudge to pop it clean. Tap junk and weeds to clear them. Drag to turn the tank.", 8f);
        }

        // ---------- A: wipe ----------

        public bool BeginDrag(Vector2 screenPos)
        {
            if (!WipeMode) return false;
            var ray = Cam.ScreenPointToRay(screenPos);
            // Any one-finger drag that starts on the tank (glass, or the world through the open top) is a wipe/flick.
            hasLast = glass.Raycast(ray, out var pane, out var uv, out _);
            if (!hasLast && !Physics.Raycast(ray, 500f)) return false;
            lastPane = pane;
            lastUv = uv;
            lastScreen = screenPos;
            strokeRemoved = 0;
            strokeMessBefore = SimClock.Instance.Sim.MessLevel;
            strokeGrimeBefore = SimClock.Instance.Sim.State.grime;
            strokeStart = Time.unscaledTime;
            return true;
        }

        public void Drag(Vector2 screenPos, Vector2 delta)
        {
            float speed = delta.magnitude / Screen.height / Mathf.Max(Time.unscaledDeltaTime, 1f / 120f); // screen heights / s
            FlickAlong(lastScreen, screenPos, delta, speed);
            lastScreen = screenPos;

            if (!glass.Raycast(Cam.ScreenPointToRay(screenPos), out var pane, out var uv, out var point)) { hasLast = false; return; }
            float radius = wipeRadius * Juice.WorldPerScreen(point);
            float grimeUnder = glass.GrimeAt(pane, uv, radius);
            float removed = hasLast && pane == lastPane
                ? glass.BrushLine(pane, lastUv, uv, radius, wipeStrength)
                : glass.Brush(pane, uv, radius, wipeStrength);
            strokeRemoved += removed;
            Juice.Rub(Mathf.Clamp01(speed / 1.2f), grimeUnder);
            glintBudget = Mathf.Min(glintBudget + removed * 3000f, 6f);
            int n = Mathf.FloorToInt(glintBudget);
            if (n > 0) { glintBudget -= n; Juice.Glints(point, n, radius * 0.6f); }
            lastPane = pane;
            lastUv = uv;
            hasLast = true;
        }

        public void EndDrag(Vector2 screenPos)
        {
            hasLast = false;
            if (strokeRemoved <= 0.0005f) return;
            EventLog.Instance?.Clean("glass", strokeRemoved, "wipe", strokeGrimeBefore); // voluntary = glass already nearly clean
            EventLog.Instance?.Count("wipe_strokes");
            EventLog.Instance?.Count("wipe_s", Time.unscaledTime - strokeStart);
            mess.AfterClean(strokeMessBefore);
        }

        /// <summary>Fast swipes flick any trash or weed the finger crossed this frame (sampled along the segment).</summary>
        void FlickAlong(Vector2 from, Vector2 to, Vector2 delta, float speed)
        {
            if (speed < flickSpeed) return;
            int samples = Mathf.Clamp(Mathf.CeilToInt(Vector2.Distance(from, to) / 6f), 1, 24);
            for (int i = 1; i <= samples; i++)
            {
                var p = Vector2.Lerp(from, to, i / (float)samples);
                if (!MessUnder(p, out var piece, out var hit)) continue;
                var dir = (Cam.transform.right * delta.x + Cam.transform.up * delta.y).normalized;
                float s = Juice.WorldPerScreen(hit.point);
                piece.Flick((dir * 0.5f + Vector3.up * 0.35f) * s);
            }
        }

        /// <summary>The nearest uncleaned trash/weed along the ray, even behind a building (so nothing is unreachable).</summary>
        bool MessUnder(Vector2 screenPos, out TapToClean piece, out RaycastHit hit)
        {
            piece = null; hit = default;
            float best = float.MaxValue;
            foreach (var h in Physics.RaycastAll(Cam.ScreenPointToRay(screenPos), 500f))
            {
                var t = h.collider.GetComponentInParent<TapToClean>();
                if (t == null || t.Done || h.distance >= best) continue;
                best = h.distance; piece = t; hit = h;
            }
            return piece != null;
        }

        // ---------- B: tap ----------

        bool OnTap(Vector2 screenPos, Collider worldHit)
        {
            if (WipeMode) return false;
            if (worldHit != null && worldHit.GetComponentInParent<TapToClean>() != null) return false; // props pop themselves
            if (MessUnder(screenPos, out var piece, out var pieceHit)) // mess hidden behind a building still pops
            {
                EventLog.Instance?.Tap(piece.Kind);
                piece.OnTap(pieceHit);
                return true;
            }
            if (!glass.Raycast(Cam.ScreenPointToRay(screenPos), out var pane, out var uv, out var point)) return false;
            float radius = tapRadius * Juice.WorldPerScreen(point);
            if (glass.GrimeAt(pane, uv, radius) < tapMinGrime) return false;
            EventLog.Instance?.Tap("glass");
            StartCoroutine(PopPatch(pane, uv, point, radius));
            return true;
        }

        /// <summary>The patch clears outward over a few frames, with a pop and a ring of glints.</summary>
        IEnumerator PopPatch(GlassGrime.Pane pane, Vector2 uv, Vector3 point, float radius)
        {
            var sim = SimClock.Instance.Sim;
            float before = sim.MessLevel, grimeBefore = sim.State.grime, removed = 0;
            Juice.Pop(1.25f);
            Juice.GlintRing(point, (Cam.transform.position - point).normalized, radius, 14);
            foreach (float k in new[] { 0.45f, 0.75f, 1f })
            {
                removed += glass.Brush(pane, uv, radius * k, tapStrength);
                yield return null;
            }
            EventLog.Instance?.Clean("glass", removed, "tap", grimeBefore);
            EventLog.Instance?.Count("glass_taps");
            mess.AfterClean(before);
        }
    }
}
