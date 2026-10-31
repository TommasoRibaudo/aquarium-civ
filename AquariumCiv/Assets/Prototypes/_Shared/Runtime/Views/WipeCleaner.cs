using System.Collections.Generic;
using UnityEngine;

namespace AquariumCiv.Prototypes
{
    /// <summary>
    /// The cleaning gesture P1 picked (drag-to-wipe, results/P1.md), with one change from P1: the first touch decides.
    ///   A drag that starts on glass is a wipe. It never flicks junk; junk it crosses only wobbles.
    ///   A drag that starts on a piece of junk or a weed is a toss: judged at lift-off, a fast flick (release speed and
    ///   a minimum travel, like platform fling detection) throws it out; a slow one drops it back with a hint.
    ///   Junk behind dirty glass is out of reach: a drag there wipes the glass, and only clean glass lets you grab it.
    /// Tapping mess only wiggles it. Two fingers, or a drag beside the tank, turn the tank.
    /// </summary>
    public class WipeCleaner : MonoBehaviour, IDragTool
    {
        public GlassGrime glass;
        public MessView mess;

        [Tooltip("Wipe radius as a share of screen height (about a fingertip and a half).")] public float wipeRadius = 0.065f;
        [Tooltip("Share of grime removed per stamp at the center (stamps overlap along a stroke).")] public float wipeStrength = 0.5f;
        [Tooltip("Release speed (screen heights per second, over the last 0.1 s) that tosses a grabbed piece out.")] public float flickSpeed = 0.6f;
        [Tooltip("Minimum travel (share of screen height) for a toss, so a press-and-lift never counts.")] public float flickMinTravel = 0.03f;
        [Tooltip("Junk within this screen-height share of the finger counts as touched (about 44 pt).")] public float pickRadius = 0.035f;
        [Tooltip("Grime on the glass in front of a piece (fingertip-sized patch) above which you can't reach it: wipe first.")] public float blockingGrime = 0.15f;

        [Header("Hints")]
        [Tooltip("Show the gesture hint again after this many seconds without any cleaning (while the tank is messy).")] public float hintIdleSeconds = 25f;
        public int maxHintRepeats = 2;
        [Tooltip("Taps go to the nearest named person (within the pick radius) before junk (P3+).")] public bool peopleFirst;
        public string hint = "Drag across the glass to wipe it clean. Then touch junk or weeds behind clean glass and flick them out. Two fingers turn the tank.";

        GlassGrime.Pane lastPane;
        Vector2 lastUv, lastScreen;
        bool hasLast;
        float strokeRemoved, strokeMessBefore, strokeStart, glintBudget, lastCleanTime;
        int hintRepeats;
        readonly Dictionary<GlassGrime.Pane, float> strokePanes = new(); // pane -> its grime when the stroke first touched it
        TapToClean grabbed;  // this stroke started on a piece of mess: it's a toss, not a wipe
        Vector2 grabStart;
        readonly List<(float t, Vector2 p)> samples = new(); // recent finger positions, for the release velocity

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

        void Start()
        {
            mess.tapCleansProps = false;
            mess.PropTapRefused += piece => HudToast.Show("Touch it and flick it out", piece.transform.position + Vector3.up * 0.3f);
            glass.PaneCleared += (pane, center) => { Juice.Chime(); Juice.Glints(center, 30, 0.5f); };
            mess.Cleaned += () => lastCleanTime = Time.unscaledTime;
            SimClock.Instance.Returned += (_, _) => lastCleanTime = Time.unscaledTime; // time away isn't idling: let the welcome-back read first
            lastCleanTime = Time.unscaledTime;
        }

        /// <summary>Show the gesture hint now (and allow the idle repeats again).</summary>
        public void ShowHint(float seconds = 8f)
        {
            hintRepeats = 0;
            lastCleanTime = Time.unscaledTime;
            HudToast.ShowBanner(hint, seconds);
        }

        void Update()
        {
            // A player who missed the hint and isn't cleaning a messy tank gets it again (a few times at most).
            if (hintRepeats < maxHintRepeats && Time.unscaledTime - lastCleanTime > hintIdleSeconds && SimClock.Instance.Sim.MessLevel > 0.15f)
            {
                hintRepeats++;
                lastCleanTime = Time.unscaledTime;
                HudToast.ShowBanner(hint, 8f);
            }
        }

        public bool BeginDrag(Vector2 screenPos)
        {
            if (PickMess(screenPos, out var piece) && !BehindDirtyGlass(piece))
            {
                grabbed = piece;
                grabbed.SetHeld(true); // lifts a little: it's in your fingers now
                Juice.Grab();
                grabStart = screenPos;
                samples.Clear();
                samples.Add((Time.unscaledTime, screenPos));
                return true;
            }
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
            if (grabbed != null)
            {
                samples.Add((Time.unscaledTime, screenPos));
                while (samples.Count > 2 && Time.unscaledTime - samples[0].t > 0.1f) samples.RemoveAt(0);
                return;
            }
            float speed = delta.magnitude / Screen.height / Mathf.Max(Time.unscaledDeltaTime, 1f / 120f); // screen heights / s
            WobbleAlong(lastScreen, screenPos);
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
            if (grabbed != null) { Toss(screenPos); return; }
            hasLast = false;
            if (strokeRemoved <= 0.0005f) return;
            float paneGrime = 0;
            foreach (var g in strokePanes.Values) paneGrime = Mathf.Max(paneGrime, g);
            EventLog.Instance?.Clean("glass", strokeRemoved, "wipe", paneGrime); // voluntary = the wiped pane was already nearly clean
            EventLog.Instance?.Count("wipe_strokes");
            EventLog.Instance?.Count("wipe_s", Time.unscaledTime - strokeStart);
            mess.AfterClean(strokeMessBefore);
        }

        /// <summary>End of a stroke that started on a piece of mess: a fast flick throws it out, a slow drag drops it back.</summary>
        void Toss(Vector2 screenPos)
        {
            var piece = grabbed;
            grabbed = null;
            if (piece == null || piece.Done) return;
            piece.SetHeld(false);
            if (OrbitCamera.DragCancelled) return; // a second finger turned this into a camera turn: no toss, no hint
            samples.Add((Time.unscaledTime, screenPos));
            var (t0, p0) = samples[0];
            float dt = Mathf.Max(Time.unscaledTime - t0, 1f / 120f);
            var v = (screenPos - p0) / Screen.height / dt; // release velocity, screen heights / s
            float travel = Vector2.Distance(screenPos, grabStart) / Screen.height;
            // P3+: a press-and-lift near a named person is a tap on them (OnTap opens their card): no toss, no hint, no count.
            if (peopleFirst && travel < flickMinTravel && NotableView.Instance != null && NotableView.Instance.Pick(screenPos, pickRadius * Screen.height, out _)) return;
            EventLog.Instance?.Count("toss_attempts");
            if (v.magnitude >= flickSpeed && travel >= flickMinTravel)
            {
                var dir = (Cam.transform.right * v.x + Cam.transform.up * v.y).normalized;
                float s = Juice.WorldPerScreen(piece.Center);
                piece.Flick((dir * 0.5f + Vector3.up * 0.35f) * s, Mathf.InverseLerp(flickSpeed, flickSpeed * 4f, v.magnitude));
                return;
            }
            piece.Wobble();
            HudToast.Show(travel < flickMinTravel ? "Flick it out" : "Flick it faster to toss it out", piece.transform.position + Vector3.up * 0.3f);
        }

        /// <summary>A wipe never cleans junk, but reachable junk the finger crosses wobbles, so it reads as touchable.</summary>
        void WobbleAlong(Vector2 from, Vector2 to)
        {
            int samplesN = Mathf.Clamp(Mathf.CeilToInt(Vector2.Distance(from, to) / 12f), 1, 12);
            for (int i = 1; i <= samplesN; i++)
                if (PickMess(Vector2.Lerp(from, to, i / (float)samplesN), out var piece) && !BehindDirtyGlass(piece)) piece.Wobble();
        }

        /// <summary>Is the glass between the camera and this piece still dirty (a fingertip patch where the sight line crosses it)?</summary>
        bool BehindDirtyGlass(TapToClean piece)
        {
            var from = Cam.transform.position;
            var ray = new Ray(from, piece.Center - from);
            if (!glass.Raycast(ray, out var pane, out var uv, out var point)) return false; // seen through the open top
            if ((point - from).sqrMagnitude >= (piece.Center - from).sqrMagnitude) return false; // that pane is behind it
            float radius = wipeRadius * 0.5f * Juice.WorldPerScreen(point);
            return glass.GrimeAt(pane, uv, radius) > blockingGrime;
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

        /// <summary>Taps on junk wiggle it and hint at the flick (or at wiping first); everything else goes through to the world.</summary>
        bool OnTap(Vector2 screenPos, Collider worldHit)
        {
            // P3+: a tap meant for a named person reaches them even with junk nearby (taps on junk only wiggle it;
            // tossing is a drag, which this doesn't touch). Off in P2, which stays as tested.
            if (peopleFirst && NotableView.Instance != null && NotableView.Instance.Pick(screenPos, pickRadius * Screen.height, out var person))
            {
                EventLog.Instance?.Tap("notable", person.NotableId.ToString());
                person.OnTap(default);
                return true;
            }
            if (!PickMess(screenPos, out var piece)) return false;
            EventLog.Instance?.Tap(piece.Kind);
            if (BehindDirtyGlass(piece))
            {
                HudToast.Show("Wipe the glass clean first", piece.transform.position + Vector3.up * 0.3f);
                return true;
            }
            piece.OnTap(default);
            return true;
        }
    }
}
