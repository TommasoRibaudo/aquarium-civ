using System.Collections;
using System.Collections.Generic;
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
        [Tooltip("Below this local grime a tap on the glass goes through to the world (focus, citizens).")] public float tapMinGrime = 0.02f;

        [Header("Mess picking and hints")]
        [Tooltip("Junk within this screen-height share of the finger counts as touched (about 44 pt).")] public float pickRadius = 0.035f;
        [Tooltip("Show the gesture hint again after this many seconds without any cleaning (while the tank is messy).")] public float hintIdleSeconds = 25f;
        public int maxHintRepeats = 3;

        [Header("Lab")]
        [Tooltip("Lab tests start each variant from the same messy tank.")] public bool messyOnVariantStart = true;

        public bool WipeMode => PrototypeConfig.Current.Variant == "A";

        GlassGrime.Pane lastPane;
        Vector2 lastUv, lastScreen;
        bool hasLast;
        float strokeRemoved, strokeMessBefore, strokeStart, glintBudget, lastCleanTime;
        int hintRepeats;
        readonly Dictionary<GlassGrime.Pane, float> strokePanes = new(); // pane -> its grime when the stroke first touched it

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
            mess.Cleaned += () => lastCleanTime = Time.unscaledTime;
            yield return null; // after SimClock's welcome-back banner, so the gesture hint wins
            StartVariant();
        }

        void StartVariant()
        {
            mess.tapCleansProps = !WipeMode;
            if (PrototypeConfig.Current.testMode == TestMode.Lab && messyOnVariantStart) mess.MakeMessy();
            hintRepeats = 0;
            ShowHint();
        }

        void ShowHint()
        {
            lastCleanTime = Time.unscaledTime;
            HudToast.ShowBanner(WipeMode
                ? "Drag across the glass to wipe it clean. Swipe across junk and weeds to flick them out. Two fingers turn the tank."
                : "Tap a smudge to pop it clean. Tap junk and weeds to clear them. Drag to turn the tank.", 8f);
        }

        void Update()
        {
            // A tester who missed the hint and isn't cleaning a messy tank gets it again (a few times at most).
            if (hintRepeats < maxHintRepeats && Time.unscaledTime - lastCleanTime > hintIdleSeconds && SimClock.Instance.Sim.MessLevel > 0.15f)
            {
                hintRepeats++;
                ShowHint();
            }
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
            strokePanes.Clear();
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
            if (!strokePanes.ContainsKey(pane)) strokePanes[pane] = pane.Mean;
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
            float paneGrime = 0;
            foreach (var g in strokePanes.Values) paneGrime = Mathf.Max(paneGrime, g);
            EventLog.Instance?.Clean("glass", strokeRemoved, "wipe", paneGrime); // voluntary = the wiped pane was already nearly clean
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
                if (!PickMess(p, out var piece)) continue;
                var dir = (Cam.transform.right * delta.x + Cam.transform.up * delta.y).normalized;
                float s = Juice.WorldPerScreen(piece.Center);
                piece.Flick((dir * 0.5f + Vector3.up * 0.35f) * s);
            }
        }

        /// <summary>
        /// The uncleaned trash/weed nearest the finger on screen, within a touch-sized radius, even behind a building.
        /// A piece the ray hits directly (big weeds) always counts. Nearest wins, so neighbours don't steal taps.
        /// </summary>
        bool PickMess(Vector2 screenPos, out TapToClean piece)
        {
            piece = null;
            float radius = pickRadius * Screen.height, best = float.MaxValue;
            if (Physics.Raycast(Cam.ScreenPointToRay(screenPos), out var hit, 500f))
            {
                var direct = hit.collider.GetComponentInParent<TapToClean>();
                if (direct != null && !direct.Done) { piece = direct; return true; }
            }
            foreach (var t in TapToClean.Active)
            {
                if (t.Done) continue;
                var sp = Cam.WorldToScreenPoint(t.Center);
                if (sp.z <= 0) continue;
                float d = Vector2.Distance(screenPos, sp);
                if (d < radius && d < best) { best = d; piece = t; }
            }
            return piece != null;
        }

        // ---------- B: tap ----------

        bool OnTap(Vector2 screenPos, Collider worldHit)
        {
            if (PickMess(screenPos, out var piece)) // A: wiggle + hint; B: pop (also behind a building)
            {
                EventLog.Instance?.Tap(piece.Kind);
                piece.OnTap(default);
                return true;
            }
            if (WipeMode) return false;
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
            float before = sim.MessLevel, paneGrime = pane.Mean, removed = 0;
            Juice.Pop(1.25f);
            Juice.GlintRing(point, (Cam.transform.position - point).normalized, radius, 14);
            foreach (float k in new[] { 0.45f, 0.75f, 1f })
            {
                removed += glass.Brush(pane, uv, radius * k, tapStrength);
                yield return null;
            }
            EventLog.Instance?.Clean("glass", removed, "tap", paneGrime);
            EventLog.Instance?.Count("glass_taps");
            mess.AfterClean(before);
        }
    }
}
