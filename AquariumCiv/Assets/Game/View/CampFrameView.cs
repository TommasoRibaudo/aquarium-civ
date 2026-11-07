using System.Collections.Generic;
using AquariumCiv.Game;
using AquariumCiv.GameUI;
using AquariumCiv.Life;
using AquariumCiv.Lighting;
using AquariumCiv.Sim;
using UnityEngine;

namespace AquariumCiv.View
{
    /// <summary>
    /// The default camera frames the camp (decisions.md 2026-11-06, "The default camera"; tester run 1, items 1 and 2). On an open
    /// (not a new box: the first run's framing is approved and stays) and on every return (a resume from the background, the debug
    /// Away), once the dolls stand, the camera eases to <see cref="CampFrame"/>: the camp and the people round it, dolls ~40 px on a
    /// 2556-px phone, the near glass faded (<see cref="NearGlassFade.CampCloseness"/>). It's an ordinary flight: a touch stops it, and
    /// a pinch out still reaches the whole box. It never moves while something else has the view (a moment, Show me, the choice, a
    /// sheet, a card, camera mode...): it waits a little, then lets the chance go. The frame becomes the camera's home.
    /// </summary>
    public class CampFrameView : MonoBehaviour
    {
        /// <summary>Off in play tests that stand still (PlayTestBase turns it off; its own tests turn it on).</summary>
        public static bool Auto = true;
        public static CampFrameView Instance { get; private set; }

        /// <summary>After the trigger: at least this long (the catch-up's replan places the dolls), at most this long for dolls to stand,
        /// and at most this long waiting for the view to be free before the chance is let go.</summary>
        public const float SettleSeconds = 0.6f, DollWaitSeconds = 4f, GiveUpSeconds = 8f;

        bool pending;
        float since;
        GameClock hooked;

        /// <summary>The last frame eased to (or computed by the debug panel), and why the last trigger did nothing.</summary>
        public CampFrame.Result Last { get; private set; }
        public float LastZoom { get; private set; }
        public int Framed { get; private set; }
        public string LastWhy { get; private set; } = "";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (Instance == null && FindAnyObjectByType<GameClock>() != null) new GameObject("CampFrameView").AddComponent<CampFrameView>();
        }

        void OnEnable()
        {
            if (Instance != null && Instance != this) { enabled = false; return; }
            Instance = this;
        }

        void OnDisable()
        {
            if (Instance == this) Instance = null;
            if (hooked != null) hooked.CaughtUp -= OnCaughtUp;
            hooked = null;
        }

        void Start()
        {
            // The open: a box that was saved, not a new one (the first run frames itself).
            var c = GameClock.Instance;
            if (c != null && c.Session != null && !c.Session.newWorld) Trigger();
        }

        void OnCaughtUp(CatchUpResult r) => Trigger();

        void Trigger() { pending = true; since = 0f; }

        void Update()
        {
            var c = GameClock.Instance;
            if (c != hooked)
            {
                if (hooked != null) hooked.CaughtUp -= OnCaughtUp;
                hooked = c;
                if (c != null) c.CaughtUp += OnCaughtUp;
            }
            if (!pending) return;
            since += Time.unscaledDeltaTime;
            if (!Auto) { pending = false; LastWhy = "off (tests)"; return; }
            string busy = Busy();
            if (busy != null)
            {
                if (since > GiveUpSeconds) { pending = false; LastWhy = "let go: " + busy; }
                return;
            }
            if (since < SettleSeconds) return;
            var pv = PeopleView.Find();
            if (pv == null || pv.Planner == null) { if (since > GiveUpSeconds) pending = false; return; }
            pv.StandingNow(who, xs, zs);
            if (who.Count == 0 && since < DollWaitSeconds) return;
            pending = false;
            LastWhy = Go(CampFrame.EaseSeconds);
        }

        /// <summary>What has the view now (null = free).</summary>
        static string Busy()
        {
            var first = FirstRunView.Instance;
            if (first != null && first.Active) return "the first run";
            string held = HeldMomentStage.BusyReason();
            if (held != null) return held;
            var hud = GameHud.Instance;
            if (hud != null && (hud.CameraMode || hud.CleanMode)) return "camera or clean mode";
            var card = PersonCardView.Instance;
            if (card != null && (card.AnyOpen || card.BuildingOpen)) return "a card";
            var cam = Cam;
            if (cam == null) return "no camera";
            if (!cam.isActiveAndEnabled) return "the camera is held";
            if (cam.Touching || cam.IsFlying || cam.GlideArmed || cam.IsFollowing) return "the camera is in use";
            return null;
        }

        static BoxCamera Cam => Camera.main != null ? Camera.main.GetComponent<BoxCamera>() : null;

        readonly List<PersonRef> who = new();
        readonly List<float> xs = new(), zs = new();
        readonly List<CampFrame.Point> camp = new(), dolls = new();

        /// <summary>The frame for the box as it stands now (box units), from the camp's fire, tents and stash and the dolls standing.</summary>
        public static CampFrame.Result Compute(PeopleView pv, List<CampFrame.Point> camp, List<CampFrame.Point> dolls, List<PersonRef> who, List<float> xs, List<float> zs)
        {
            camp.Clear(); dolls.Clear();
            var planner = pv.Planner;
            var sim = GameClock.Instance != null ? GameClock.Instance.Sim : null;
            var at = planner?.Positions;
            bool hasCamp = false;
            if (sim != null && at != null)
                foreach (var b in sim.State.buildings)
                    if (b.type == BuildingType.Camp || b.type == BuildingType.Hut) { hasCamp = true; break; }
            if (hasCamp)
            {
                // The fire (the gathering place) first and heaviest, then the stash and the tents.
                if (at.TryGetPosition(planner.GatherPlace, out float fx, out float fz)) camp.Add(new CampFrame.Point(fx, fz, 3f));
                if (at.TryGetPosition(CampStash.Stand, out float sx, out float sz)) camp.Add(new CampFrame.Point(sx, sz));
                foreach (var b in sim.State.buildings)
                    if ((b.type == BuildingType.Hut || b.type == BuildingType.Camp) && at.TryGetPosition(new Place(b.type == BuildingType.Camp ? PlaceKind.Fire : PlaceKind.Home, b.id), out float hx, out float hz))
                        camp.Add(new CampFrame.Point(hx, hz));
            }
            pv.StandingNow(who, xs, zs);
            for (int i = 0; i < who.Count; i++) dolls.Add(new CampFrame.Point(xs[i], zs[i]));
            return CampFrame.Frame(camp, dolls, Land.HX, Land.HZ);
        }

        /// <summary>Eases the camera to the camp frame now and makes it home. Returns what it did.</summary>
        public string Go(float seconds)
        {
            var cam = Cam;
            var pv = PeopleView.Find();
            if (cam == null || pv == null || pv.Planner == null) return "no camera or people";
            var f = Compute(pv, camp, dolls, who, xs, zs);
            var c = cam.GetComponent<Camera>();
            float tan = Mathf.Tan((c != null ? c.fieldOfView : 22f) * 0.5f * Mathf.Deg2Rad), aspect = c != null ? c.aspect : 9f / 16f;
            float fit = cam.FitDistance();
            float zoom = Mathf.Clamp(CampFrame.Zoom(f.radius, fit, tan, aspect, CampFrame.Pitch, PeopleView.DollScaleAmount), cam.minZoom, 1f);
            float pitch = Mathf.Max(CampFrame.Pitch, BoxCameraMath.PitchFloor(zoom));
            var world = pv.transform.TransformPoint(new Vector3(f.x, 0f, f.z));
            world.y = cam.target.y;
            cam.EaseTo(world, CampFrame.Yaw, pitch, zoom, seconds);
            cam.SetHome(new Vector3(world.x, cam.target.y, world.z), CampFrame.Yaw, pitch, zoom);
            Last = f; LastZoom = zoom; Framed++;
            // t3-hunt: the planner prefers hunts inside what this view really shows (read from the frame's own maths, never a hard-coded radius)
            pv.Planner.SetHuntFrame(f.x, f.z, Mathf.Clamp(Mathf.Min(f.radius, CampFrame.VisibleRadius(fit * zoom, tan, aspect)), CampFrame.MinRadius * 0.5f, CampFrame.MaxRadius));
            float px = CampFrame.DollPx(fit * zoom, pitch, Screen.height, tan, CameraReach.DollScale(zoom, PeopleView.DollScaleAmount));
            string s = $"camp frame {f}: zoom {zoom:0.00}, doll {px:0} px of {Screen.height}";
            Debug.Log("[CampFrame] " + s);
            return s;
        }

        // ---------------- debug (t1-camera) ----------------
        public static string DebugCampFrame()
        {
            var v = Instance;
            return v == null ? "no CampFrameView" : v.Go(0.8f);
        }

        public static string DebugWholeBox()
        {
            var cam = Cam;
            if (cam == null) return "no camera";
            cam.GoWholeBox();
            return "whole box";
        }

        public static string Status()
        {
            var v = Instance;
            return v == null ? "camp frame: none" : $"camp frame: {v.Framed} framed, last {v.Last} zoom {v.LastZoom:0.00}{(v.LastWhy.Length > 0 ? " · " + v.LastWhy : "")}";
        }
    }
}
