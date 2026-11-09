using System.Collections.Generic;
using AquariumCiv.Lighting;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace AquariumCiv.View
{
    /// <summary>
    /// A tool that owns a drag on the box (the wipe cleaner, #37). Register it with <see cref="BoxCamera.AddGate"/>; when a
    /// press starts where <see cref="ClaimsPress"/> says yes, the camera ignores that press (no orbit, pan or zoom) until
    /// every finger lifts. The decision is made once, at touch-down, so a drag never turns into an orbit half way.
    /// </summary>
    public interface ICameraGate
    {
        /// <summary>True when a press at this screen position belongs to the tool, not the camera.</summary>
        bool ClaimsPress(Vector2 screenPos);
    }

    /// <summary>
    /// The slice's camera on the box. One meaning per gesture: one finger orbits, two fingers pan and zoom together, a tap is
    /// left to selection (a touch that stays within a small slop never moves the camera). In the Editor, left drag orbits,
    /// right or middle drag pans, scroll zooms towards the pointer. The default distance fits the whole box across the
    /// screen and is refitted whenever the aspect changes (the Game view size lands a few frames into play mode,
    /// docs/tooling.md). Input only in play mode. The numbers live in <see cref="BoxCameraMath"/> (unit-tested).
    /// <list type="bullet">
    /// <item>Orbit speed is a fraction of the screen height (110 degrees of yaw, 50 of pitch per screen height), so it feels the same on every phone.</item>
    /// <item>The pitch floor depends on zoom (24 degrees at the full box down to 14 close in), the ceiling is 68. Limits are soft: past one the drag counts less and less, and on release the camera springs back.</item>
    /// <item>Release flings with the velocity of the last 80 ms, decaying over about 0.2 s; the shown angles follow the targets smoothly.</item>
    /// <item>Pinch zooms by ratio towards the fingers; the pan grabs the ground (the point under the fingers stays under them), bounded by zoom so the full box stays centred, with a rubber band.</item>
    /// </list>
    /// </summary>
    [ExecuteAlways]
    public class BoxCamera : MonoBehaviour
    {
        public Vector3 target = new(0, 1.0f, 0);
        public float yaw = 28f, pitch = 34f;
        [Tooltip("Orbit stays in front of the box, so the shelf and wall behind it stay in view (tester #31).")]
        public float minYaw = -30f, maxYaw = 55f;
        [Tooltip("Zoom: 1 = the whole box fits the width; < 1 is closer.")]
        public float zoom = 1f, minZoom = CameraReach.MinZoom, maxZoom = CameraReach.MaxZoom;
        [Tooltip("How much of the screen width the box takes at zoom 1 (about).")]
        public float fill = 1f;

        Camera cam;

        // The angles the drag moves; yaw / pitch follow them smoothly.
        float targetYaw, targetPitch, lastYaw, lastPitch;
        bool synced;
        Vector2 velocity; // degrees per second (yaw, pitch)
        readonly List<(float t, Vector2 d)> samples = new();

        enum Mode { None, Orbit, TwoFinger, Locked }
        Mode mode;
        Vector2 pressPos, lastPos, prevMid;
        float prevDist;
        bool orbiting, moved;
        bool pressOnUi, pressClaimed;
        readonly List<ICameraGate> gates = new();

        // Fly-to: a smooth move of target and zoom that any touch or scroll interrupts.
        Vector3 flyFrom, flyTo;
        float flyZoomFrom, flyZoomTo, flyT, flyDur;
        public bool IsFlying { get; private set; }
        /// <summary>A flight no touch or scroll stops (<see cref="EaseTo"/>'s firm): the way back from a moment, which the player can't redo by hand.</summary>
        bool firmFlight;
        /// <summary>Raised when a flight ends: true = it arrived, false = a touch (or a new flight / Cancel) interrupted it.</summary>
        public event System.Action<bool> FlightEnded;

        /// <summary>Camera mode (#70): after <see cref="AutoOrbitIdleSeconds"/> without a touch the box drifts slowly left and right. Toggle in the debug panel.</summary>
        public static bool AutoOrbitInCameraMode = true;
        public const float AutoOrbitIdleSeconds = 10f, AutoOrbitDegPerSecond = 2.5f; // 10 s: user 2026-10-30 (was 4)
        float lastInputTime;
        float autoDir = 1f;
        bool wasCameraMode;

        public void AddGate(ICameraGate g) { if (g != null && !gates.Contains(g)) gates.Add(g); }
        public void RemoveGate(ICameraGate g) => gates.Remove(g);
        /// <summary>True while a press is owned by a gate (a tool) and the camera ignores it.</summary>
        public bool PressClaimed => pressClaimed;

        /// <summary>A one-finger press doesn't start orbiting before this <c>Time.unscaledTime</c>: a tool that tells a flick from a drag
        /// by its speed (the herd, #145) gets that long to see which it is, and a drag then orbits from where the finger is.</summary>
        public float HoldOrbitUntil { get; set; }

        /// <summary>True from the moment a press has moved past the slop (or a second finger came down) until all fingers lift:
        /// selection should ignore the release as a tap.</summary>
        public bool GestureMoved => moved;
        /// <summary>True while a finger or button is down on the camera.</summary>
        public bool Touching => mode != Mode.None;

        /// <summary>Closer than this share of the fitted view, or panned, and the HUD's ink no longer has a calm background behind it.</summary>
        const float HomeZoom = 0.92f, HomeOffset = 0.3f;
        Vector3 homeTarget;
        float homeYaw, homePitch, homeFit;
        bool homeKnown;

        /// <summary>Eases back to the view the box opened on.</summary>
        public void GoHome(float seconds = BoxCameraMath.FlyDefaultSeconds, bool firm = false)
        {
            if (homeKnown) EaseTo(homeTarget, homeYaw, homePitch, homeFit, seconds, firm);
        }

        /// <summary>Is the camera off its home view? The HUD backs the box name and top icons while it is (spec §4).</summary>
        public bool OffHome => homeKnown && (zoom < HomeZoom || (target - homeTarget).magnitude > HomeOffset);

        // ---------------- the camp frame (decisions 2026-11-06, "The default camera") ----------------
        Vector3 boxTarget;
        float boxYaw, boxPitch, boxZoom;

        /// <summary>The camp frame is home now: <see cref="GoHome"/> (the evening talk's way back, the debug panel's "Go home") returns there.
        /// The view the box opened on (the whole box) stays as <see cref="GoWholeBox"/>.</summary>
        public void SetHome(Vector3 worldTarget, float yawAt, float pitchAt, float zoomAt)
        {
            TellHud(); // the opening view is remembered first
            homeTarget = worldTarget; homeYaw = yawAt; homePitch = pitchAt; homeFit = zoomAt;
        }

        /// <summary>Eases out to the view the box opened on: the whole box (a pinch out gets there too).</summary>
        public void GoWholeBox(float seconds = BoxCameraMath.FlyDefaultSeconds)
        {
            TellHud();
            EaseTo(boxTarget, boxYaw, boxPitch, boxZoom, seconds);
        }

        /// <summary>The home pose (target, yaw, pitch, zoom): the camp frame once one was framed, else the opening view.</summary>
        public (Vector3 target, float yaw, float pitch, float zoom) Home => (homeTarget, homeYaw, homePitch, homeFit);

        void TellHud()
        {
            if (!homeKnown) { homeTarget = boxTarget = target; homeYaw = boxYaw = yaw; homePitch = boxPitch = pitch; homeFit = boxZoom = zoom; homeKnown = true; }
            AquariumCiv.GameUI.GameHud.Instance?.SetOffHome(OffHome || DayLightDriver.HudNeedsBacking);
        }

        public float FitDistance()
        {
            if (cam == null) cam = GetComponent<Camera>();
            float aspect = cam != null ? cam.aspect : 9f / 16f;
            float fov = cam != null ? cam.fieldOfView : 22f;
            float halfH = Mathf.Atan(Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad) * Mathf.Min(1f, aspect));
            return new Vector3(Land.HX + 0.3f, 1.7f, Land.HZ + 0.3f).magnitude / Mathf.Tan(halfH) / Mathf.Max(0.1f, fill);
        }

        void OnEnable()
        {
            synced = false; mode = Mode.None; velocity = Vector2.zero; IsFlying = firmFlight = false; pressClaimed = false;
            minZoom = CameraReach.MinZoom; maxZoom = CameraReach.MaxZoom; // the scene file still holds the old limits
            glide.Cancel(); glideNext = false;
        }

        void Awake()
        {
            if (!Application.isPlaying) return;
            cam = GetComponent<Camera>();
            RenderPipelineManager.beginCameraRendering += FitDepthBeforeRender;
        }

        void OnDestroy()
        {
            RenderPipelineManager.beginCameraRendering -= FitDepthBeforeRender;
            RestoreDepth();
        }

        // ---------------- fly-to ----------------
        /// <summary>
        /// Flies to a point on the ground (world position; only x and z count) and optionally zooms in. Smooth (ease in and out),
        /// clamped to the pan bounds of the end zoom, interrupted by any touch or scroll (the camera then stays where it is and
        /// the gesture takes over). A new call replaces a flight in progress. Returns the target actually flown to.
        /// <paramref name="zoomTo"/> &lt;= 0 keeps the current zoom.
        /// </summary>
        public Vector3 FlyTo(Vector3 worldPoint, float zoomTo = -1f, float seconds = BoxCameraMath.FlyDefaultSeconds)
        {
            if (IsFlying) EndFlight(false);
            if (!glideNext) glide.Cancel(); // a flight of its own, the way back or a moment's: it has the camera now
            glideNext = false;
            float z1 = zoomTo > 0f ? Mathf.Clamp(zoomTo, flyFloor > 0f ? flyFloor : minZoom, maxZoom) : zoom;
            flyFrom = target; flyZoomFrom = zoom; flyZoomTo = z1;
            // #273: the end pose's own range (the flight keeps yaw and pitch), so a flight never ends where the spring would pull it back or the camera sits in a pane.
            flyTo = BoxCameraMath.ClampFocus(new Vector3(worldPoint.x, target.y, worldPoint.z), GlassBounds(z1));
            HoldByCode(z1);
            flyDur = Mathf.Max(0f, seconds); flyT = 0f;
            firmFlight = false;
            velocity = Vector2.zero;
            if (flyDur <= 1e-4f) { target = flyTo; zoom = z1; Apply(); return flyTo; }
            IsFlying = true;
            return flyTo;
        }

        /// <summary>Flies to a person's doll (notable or crowd). False when they have no doll in the box now.</summary>
        public bool FlyToPerson(AquariumCiv.Life.PersonRef who, float zoomTo = BoxCameraMath.FlyPersonZoom, float seconds = BoxCameraMath.FlyDefaultSeconds)
        {
            var pv = FindAnyObjectByType<PeopleView>();
            if (pv == null || !pv.TryGetDollPosition(who, out var p)) return false;
            FlyTo(pv.transform.TransformPoint(new Vector3(p.x, 0f, p.y)), zoomTo, seconds);
            return true;
        }

        /// <summary>Flies to a place (the fire, a building by sim id, a field...) as the planner knows it.</summary>
        public bool FlyToPlace(AquariumCiv.Life.Place place, float zoomTo = BoxCameraMath.FlyPlaceZoom, float seconds = BoxCameraMath.FlyDefaultSeconds)
        {
            var pv = FindAnyObjectByType<PeopleView>();
            var pos = pv != null && pv.Planner != null ? pv.Planner.Positions : null;
            if (pos == null || !pos.TryGetPosition(place, out float x, out float z)) return false;
            FlyTo(pv.transform.TransformPoint(new Vector3(x, 0f, z)), zoomTo, seconds);
            return true;
        }

        public bool FlyToBuilding(int buildingId, float zoomTo = BoxCameraMath.FlyPlaceZoom, float seconds = BoxCameraMath.FlyDefaultSeconds)
            => buildingId >= 0 && FlyToPlace(new AquariumCiv.Life.Place(AquariumCiv.Life.PlaceKind.Home, buildingId), zoomTo, seconds);

        /// <summary>Eases back to a whole pose (target, yaw, pitch, zoom) over <paramref name="seconds"/>: the end of an era
        /// moment hands the camera back where the player had it (tester batch 2). Touch interrupts it like any flight, unless it's
        /// <paramref name="firm"/>: then only a new flight or a Cancel does, and a finger held through it starts fresh once it lands.</summary>
        public void EaseTo(Vector3 worldPoint, float yawTo, float pitchTo, float zoomTo, float seconds = BoxCameraMath.FlyDefaultSeconds, bool firm = false)
        {
            FlyTo(worldPoint, zoomTo, seconds);
            firmFlight = firm && IsFlying;
            targetYaw = Mathf.Clamp(yawTo, minYaw, maxYaw);
            targetPitch = pitchTo;
            lastYaw = yaw; lastPitch = pitch;
            synced = true;
        }

        /// <summary>A gentle move toward something happening (a relic moment, a tap-back; user 2026-10-30, #92): only when it's
        /// off-screen or the camera is too far out to see it, and never against a finger. Same flight as any other: a touch
        /// stops it. True when the camera moves.</summary>
        public bool NudgeToward(Vector3 worldPoint, float zoomTo = NudgeZoom, float seconds = NudgeSeconds)
        {
            if (cam == null) cam = GetComponent<Camera>();
            if (cam == null || !isActiveAndEnabled || Touching || pressClaimed) return false;
            var v = cam.WorldToViewportPoint(worldPoint);
            bool framed = v.z > 0f && v.x > 0.15f && v.x < 0.85f && v.y > 0.2f && v.y < 0.85f;
            if (framed && zoom <= zoomTo + 0.02f && pitch >= NudgePitch - 3f) return false;
            ReturnAfterFlight(); // tester run 9: a wish's or a crumb's nudge used to leave the camera there
            EaseTo(worldPoint, yaw, Mathf.Max(pitch, NudgePitch), Mathf.Min(zoom, zoomTo), seconds);
            return true;
        }

        /// <summary>A nudge looks down from at least <see cref="NudgePitch"/> degrees: from the usual angle the land behind the
        /// front pane is lost in its haze (Show me does the same).</summary>
        public const float NudgeZoom = 0.7f, NudgeSeconds = 1.8f, NudgePitch = 50f;

        // ---------------- the lean (P9, user 2026-11-08) ----------------
        System.Func<bool> leanHold;
        bool leanFlight;

        /// <summary>A lean is on: the camera went in close for something small (placed life) and stays while <c>hold</c> says so, then glides
        /// back to where the player had it.</summary>
        public bool Leaning => leanHold != null && (glide.Armed || IsFlying);
        /// <summary>Leans the player cancelled (a two-finger move, an orbit drag or a scroll while a lean held the camera).</summary>
        public int LeanCancels { get; private set; }
        /// <summary>A lean looks down from at least this pitch (degrees), a little steeper than a nudge so a small group on the ground spreads out.</summary>
        public const float LeanPitch = 52f, LeanSeconds = 1.4f;
        /// <summary>User 2026-11-08: a lean may go past the player's zoom floor (<see cref="CameraReach.MinZoom"/>) to this, for the smallest
        /// groups (rabbits, ducks, fish); it still eases back to the player's zoom, and a cancelled lean eases up to the floor.</summary>
        public const float LeanMinZoom = 0.15f;
        /// <summary>How fast a zoom left under the player's floor (a cancelled lean) eases back up to it (zoom per second).</summary>
        const float FloorEase = 0.3f;
        float flyFloor = -1f;

        /// <summary>
        /// P9 (user 2026-11-08, "the camera leans in"): eases in close to <paramref name="worldPoint"/> at <paramref name="zoomTo"/> so a small
        /// group reads at phone size, keeps the view there while <paramref name="hold"/> is true, then glides back to where the player had the
        /// camera (the first lean of a chain keeps the way home). Unlike a nudge it leans even when the spot is framed, and a one-finger tap (the
        /// life tool's own taps) doesn't stop it; a two-finger move, an orbit drag or a scroll does (<see cref="LeanCancels"/>). Never against a
        /// finger already moving the camera. True when the camera moves.
        /// </summary>
        public bool LeanIn(Vector3 worldPoint, float zoomTo, System.Func<bool> hold, float seconds = LeanSeconds)
        {
            if (cam == null) cam = GetComponent<Camera>();
            if (cam == null || !isActiveAndEnabled || (Touching && (moved || mode == Mode.TwoFinger))) return false;
            float floor = Mathf.Min(minZoom, LeanMinZoom);
            float z1 = Mathf.Min(zoom, Mathf.Clamp(zoomTo, floor, maxZoom));
            ReturnAfterFlight();
            flyFloor = floor;
            try { EaseTo(worldPoint, yaw, Mathf.Max(pitch, LeanPitch), z1, seconds); }
            finally { flyFloor = -1f; }
            leanHold = hold;
            leanFlight = IsFlying;
            return true;
        }

        /// <summary>Ends a lean now: the camera glides back after the usual short wait.</summary>
        public void ReleaseLean() { leanHold = null; }

        bool LeanHeld()
        {
            if (leanHold == null) return false;
            bool h;
            try { h = leanHold(); } catch { h = false; }
            if (!h) leanHold = null;
            return h;
        }

        // ---------------- the glide back (#146) ----------------
        readonly GlideBack glide = new();
        bool glideNext;
        Vector3 glideTarget;
        float glideZoom, glideYaw, glidePitch;

        /// <summary>True from a fly-in until the camera has glided back or a touch cancelled it.</summary>
        public bool GlideArmed => glide.Armed;
        public bool Gliding => glide.Gliding;

        /// <summary>Call just before a fly-in the player didn't ask to keep (a person's card, a nudge): when it ends and nothing holds the view,
        /// the camera waits <see cref="GlideBack.Hold"/> s and glides to where it is now. Chained fly-ins keep the first place to come back to.</summary>
        public void ReturnAfterFlight()
        {
            if (!glide.Armed) { glideTarget = target; glideZoom = zoom; glideYaw = yaw; glidePitch = pitch; }
            glide.Remember(glideTarget.x, glideTarget.z, glideZoom, glideYaw, glidePitch);
            glideNext = true;
        }

        /// <summary>Something is using the view the flight went to: its card is up, a moment plays, camera mode. The wait restarts when that ends.</summary>
        bool GlideHeld()
        {
            if (LeanHeld()) return true;
            var hud = AquariumCiv.GameUI.GameHud.Instance;
            if (IsFollowing || (hud != null && (hud.CameraMode || hud.CleanMode || hud.SheetOpen))) return true;
            var card = PersonCardView.Instance;
            if (card != null && (card.AnyOpen || card.BuildingOpen)) return true;
            var show = ShowMeDirector.Instance;
            if (show != null && show.Busy) return true;
            var moment = MomentPlayer.Instance;
            if (moment != null && moment.Playing) return true;
            var box = BoxCompleteController.Instance;
            if (box != null && box.Playing) return true;
            var lapse = TimeLapseView.Find();
            if (lapse != null && lapse.Active) return true;
            var shape = ShapingView.Instance;
            return shape != null && shape.ShapeMode;
        }

        /// <summary>One frame of the glide; true while it has the camera.</summary>
        bool StepGlide(float dt, int touches, float scroll)
        {
            if (!glide.Armed) { leanHold = null; return false; }
            // Tester run 10: with a card up a press may be the tap that closes it, so only a real drag or pinch forgets the way home.
            bool held = GlideHeld();
            if (Mathf.Abs(scroll) > 0.01f || (touches > 0 && (!held || moved || touches >= 2)))
            {
                if (leanHold != null) { LeanCancels++; leanHold = null; }
                glide.Cancel(); return false;
            }
            if (held) { glide.Remember(glideTarget.x, glideTarget.z, glideZoom, glideYaw, glidePitch); return false; }
            float x = target.x, z = target.z, zm = zoom, y = yaw, p = pitch;
            if (!glide.Step(dt, ref x, ref z, ref zm, ref y, ref p)) return false;
            target.x = x; target.z = z; zoom = zm;
            yaw = targetYaw = lastYaw = y;
            pitch = targetPitch = lastPitch = p;
            velocity = Vector2.zero;
            Apply();
            return true;
        }

        /// <summary>Debug: a fly-in to a world point at this zoom, gliding back after, for the look at the closest zoom (#146).</summary>
        public void DebugFlyIn(Vector3 worldPoint, float zoomTo)
        {
            ReturnAfterFlight();
            FlyTo(worldPoint, zoomTo);
        }

        // ---------------- the depth around the box (#193) ----------------
        float shadowWas = -1f;

        /// <summary>Tester run 10: this hangs off the render, not off this component's own LateUpdate. Moments, the last moment and the shelf turn the rig off and move the
        /// camera themselves, and the near plane stayed where the rig left it, in front of the whole box. Now whoever moves the camera, the frame it draws is fitted.</summary>
        void FitDepthBeforeRender(ScriptableRenderContext context, Camera rendering)
        {
            if (rendering == cam) FitDepthNow();
        }

        /// <summary>The camera sits 8 to 53 units from the box (a 22 degree lens), so no one shadow distance fits, and a fixed one spreads the shadow map over
        /// all the empty air in front of the box: that is the saw-tooth. Near plane and shadow distance follow the camera instead (<see cref="BoxCameraMath.FitNear"/>).
        /// The asset's own value is put back when the camera goes. A render request (a still, a snapshot, the book's cover) raises no begin-camera event: call this first.</summary>
        public void FitDepthNow()
        {
            if (cam == null) cam = GetComponent<Camera>();
            if (cam == null) return;
            var t = transform;
            cam.nearClipPlane = BoxCameraMath.FitNear(t.position, t.forward, t.up, Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad), ShelfRowTop());
            float d = Mathf.Max(Vector3.Distance(t.position, new Vector3(0f, 1f, 0f)), Vector3.Distance(t.position, target));
            cam.farClipPlane = Mathf.Max(cam.farClipPlane, d + BoxCameraMath.FarPastFocus);
            var urp = UniversalRenderPipeline.asset;
            if (urp == null) return;
            if (shadowWas < 0f) shadowWas = urp.shadowDistance;
            urp.shadowDistance = d + BoxCameraMath.DepthBack;
        }

        /// <summary>How tall what stands along the shelf gets, or -1 with only this box there. The new box comes down from above in the last moment.</summary>
        static float ShelfRowTop()
        {
            var done = BoxCompleteController.Instance;
            if (done != null && done.Playing) return Land.GlassTop + StagedDrop;
            var shelf = AquariumCiv.Game.GameClock.Instance != null ? AquariumCiv.Game.GameClock.Instance.Shelf : null;
            return (done != null && done.ShelfOpen) || (shelf != null && shelf.boxes.Count > 1) ? Land.GlassTop + 0.1f : -1f;
        }

        const float StagedDrop = 2.4f;

        void RestoreDepth()
        {
            if (shadowWas < 0f) return;
            var urp = UniversalRenderPipeline.asset;
            if (urp != null) urp.shadowDistance = shadowWas;
            shadowWas = -1f;
        }

        /// <summary>Stops a flight where it is.</summary>
        public void CancelFlight() { if (IsFlying) EndFlight(false); }

        void EndFlight(bool arrived) { IsFlying = false; firmFlight = false; leanFlight = false; FlightEnded?.Invoke(arrived); }

        /// <summary>One frame of flight (public for tests): <paramref name="touches"/> fingers or buttons down, or a scroll, interrupt it.
        /// Returns true while the camera is still flying after this frame.</summary>
        public bool TickFlight(float dt, int touches = 0, float scroll = 0f)
        {
            if (!IsFlying) return false;
            // a lean's flight gives way to a camera gesture (two fingers, a scroll), not to the life tool's one-finger taps
            if (!firmFlight && (touches > (leanFlight && leanHold != null ? 1 : 0) || Mathf.Abs(scroll) > 0.01f))
            {
                if (leanFlight && leanHold != null) { LeanCancels++; leanHold = null; glide.Cancel(); }
                EndFlight(false); return false;
            }
            flyT += dt;
            float k = BoxCameraMath.FlyEase(flyDur > 0f ? flyT / flyDur : 1f);
            target = Vector3.Lerp(flyFrom, flyTo, k);
            zoom = Mathf.Lerp(flyZoomFrom, flyZoomTo, k);
            if (flyT >= flyDur) { target = flyTo; zoom = flyZoomTo; EndFlight(true); }
            Apply();
            return IsFlying;
        }

        void Update()
        {
            if (cam == null) cam = GetComponent<Camera>();
            if (Application.isPlaying) Step(Mathf.Min(Time.unscaledDeltaTime, 0.1f), Time.unscaledTime);
            else { targetYaw = lastYaw = yaw; targetPitch = lastPitch = pitch; synced = true; }
            Apply();
        }

        /// <summary>Reads the input and moves the camera by one frame.</summary>
        void Step(float dt, float now)
        {
            // Something else set yaw / pitch (a debug cheat, a test): take that as the new target.
            if (!synced || Mathf.Abs(yaw - lastYaw) > 1e-3f) targetYaw = yaw;
            if (!synced || Mathf.Abs(pitch - lastPitch) > 1e-3f) targetPitch = pitch;
            synced = true;
            HookHud();

            ReadPointers(out int n, out Vector2 a, out Vector2 b, out bool mousePan);
            // A press that starts on the HUD belongs to the HUD until every finger lifts.
            if (n == 0) pressOnUi = false;
            else if (mode == Mode.None && AquariumCiv.Game.UiInput.Over(a)) pressOnUi = true;
            if (pressOnUi) { n = 0; mode = Mode.None; orbiting = false; }
            // A press a tool (the wipe) claims at touch-down is not ours until every finger lifts.
            if (n == 0) pressClaimed = false;
            else if (mode == Mode.None && !pressClaimed && ClaimedByGate(a)) pressClaimed = true;
            float scrollY = Mouse.current != null && n == 0 ? Mouse.current.scroll.ReadValue().y : 0f;
            // A firm flight has the camera: the finger's press counts from where the camera lands, so nothing jumps.
            if (firmFlight && n > 0) { mode = Mode.None; orbiting = moved = false; velocity = Vector2.zero; samples.Clear(); }
            if (pressClaimed)
            {
                // The tool keeps the touch; a flight still gives way to it.
                if (IsFlying && TickFlight(dt, n, scrollY)) FollowAngles(dt);
                // a second finger on a tool's press is the player taking the camera: it ends a lean (user 2026-11-08)
                if (n >= 2 && leanHold != null) { LeanCancels++; leanHold = null; glide.Cancel(); }
                n = 0; mode = Mode.None; orbiting = false; velocity = Vector2.zero;
                if (!(glide.Armed && LeanHeld())) glide.Cancel(); // a lean stays through the life tool's taps
                return;
            }
            if (IsFlying && TickFlight(dt, n, scrollY)) { FollowAngles(dt); return; }
            if (!IsFlying && StepGlide(dt, n, scrollY)) return;
            float H = Mathf.Max(1f, Screen.height);

            if (n == 0) EndPress(now);
            else if (n >= 2 || mousePan) PanZoom(n, a, b, mousePan);
            else Orbit(a, H, now);

            if (Mouse.current != null && n == 0)
            {
                float scroll = Mouse.current.scroll.ReadValue().y;
                if (Mathf.Abs(scroll) > 0.01f) ZoomAbout(Mathf.Clamp(zoom * (1f - scroll * 0.0008f), Mathf.Min(minZoom, zoom), maxZoom), Mouse.current.position.ReadValue());
            }

            bool dragging = mode == Mode.Orbit && orbiting;
            float lo = BoxCameraMath.PitchFloor(zoom), hi = BoxCameraMath.PitchCeiling;
            if (!dragging)
            {
                if (velocity != Vector2.zero)
                {
                    targetYaw = BoxCameraMath.SoftStep(targetYaw, velocity.x * dt, minYaw, maxYaw);
                    targetPitch = BoxCameraMath.SoftStep(targetPitch, velocity.y * dt, lo, hi);
                    velocity = BoxCameraMath.InertiaStep(velocity, dt);
                }
                targetYaw = BoxCameraMath.Spring(targetYaw, minYaw, maxYaw, dt);
                targetPitch = BoxCameraMath.Spring(targetPitch, lo, hi, dt);
            }
            if (mode != Mode.TwoFinger) { StepFollow(dt); SpringPan(dt); }
            AutoOrbit(n, dragging, dt, now);

            float f = 1f - Mathf.Exp(-BoxCameraMath.FollowRate * dt);
            yaw += (targetYaw - yaw) * f;
            pitch += (targetPitch - pitch) * f;
            lastYaw = yaw; lastPitch = pitch;
            // a held lean may sit under the player's floor (LeanMinZoom); once it is cancelled the zoom eases up to the floor, no jump
            if (zoom < minZoom) zoom = leanHold != null ? Mathf.Max(zoom, Mathf.Min(minZoom, LeanMinZoom)) : Mathf.MoveTowards(zoom, minZoom, FloorEase * dt);
            zoom = Mathf.Min(zoom, maxZoom);
        }

        void FollowAngles(float dt)
        {
            float f = 1f - Mathf.Exp(-BoxCameraMath.FollowRate * dt);
            yaw += (targetYaw - yaw) * f; pitch += (targetPitch - pitch) * f;
            lastYaw = yaw; lastPitch = pitch;
        }

        void AutoOrbit(int touches, bool dragging, float dt, float now)
        {
            var hud = AquariumCiv.GameUI.GameHud.Instance;
            bool cm = hud != null && hud.CameraMode;
            if (cm != wasCameraMode) { wasCameraMode = cm; lastInputTime = now; if (!cm) Follow(AquariumCiv.Life.PersonRef.None); }
            if (touches > 0 || dragging || IsFlying || velocity != Vector2.zero) lastInputTime = now;
            if (!cm || !AutoOrbitInCameraMode || now - lastInputTime < AutoOrbitIdleSeconds) return;
            float lo = minYaw + 6f, hi = maxYaw - 6f;
            if (targetYaw >= hi) autoDir = -1f; else if (targetYaw <= lo) autoDir = 1f;
            targetYaw += autoDir * AutoOrbitDegPerSecond * dt;
        }


        // ---------------- camera mode: follow the person you were following (#70, user 2026-10-30) ----------------
        /// <summary>The person the camera follows in camera mode (<see cref="PersonRef.None"/> = nobody): picked when camera
        /// mode starts from what the player was following (an open person card, a Show me on a person, the pinned notable,
        /// wherever they are). The target eases after their doll; one finger still orbits round them and a pinch
        /// still zooms, a two-finger pan of more than <see cref="FollowReleaseFraction"/> of the screen height lets go.</summary>
        public AquariumCiv.Life.PersonRef Following { get; private set; }
        public bool IsFollowing => !Following.IsNone;
        public const float FollowRate = 3f, FollowReleaseFraction = 0.08f;
        AquariumCiv.GameUI.GameHud hookedHud;
        float panTravel;
        int followMissing;
        PeopleView followPeople;

        /// <summary>Follow a person's doll (None stops). Camera mode sets it by itself; tests and the debug panel call it.</summary>
        public void Follow(AquariumCiv.Life.PersonRef who)
        {
            Following = who;
            followMissing = 0;
            var hud = AquariumCiv.GameUI.GameHud.Instance;
            if (hud != null) hud.SetCameraNote(who.IsNone ? null : "Following " + FollowName(who));
        }

        static string FollowName(AquariumCiv.Life.PersonRef who)
        {
            var clock = AquariumCiv.Game.GameClock.Instance;
            var n = who.IsNotable && clock != null && clock.Sim != null ? clock.Sim.FindNotable(who.notableId) : null;
            return n != null ? n.name : "a villager";
        }

        void HookHud()
        {
            var hud = AquariumCiv.GameUI.GameHud.Instance;
            if (hud == hookedHud) return;
            if (hookedHud != null) hookedHud.CameraModeChanged -= OnCameraMode;
            hookedHud = hud;
            if (hud != null) hud.CameraModeChanged += OnCameraMode;
        }

        void OnCameraMode(bool on)
        {
            // Called inside SetCameraMode, before the person card notices camera mode and closes: it is still open here.
            Follow(on ? PickFollow() : AquariumCiv.Life.PersonRef.None);
            if (!on || !IsFollowing) return;
            Debug.Log("[BoxCamera] camera mode follows " + Following);
            // The pinned person may be anywhere (user 2026-10-30): go to them, closer in if the view is wide.
            if (IsPinned(Following)) FlyToPerson(Following, Mathf.Min(zoom, BoxCameraMath.FlyPersonZoom));
        }

        static bool IsPinned(AquariumCiv.Life.PersonRef who)
        {
            var clock = AquariumCiv.Game.GameClock.Instance;
            var pin = clock != null ? clock.Sim?.Pinned : null;
            return pin != null && pin.alive && who.IsNotable && who.notableId == pin.id;
        }

        /// <summary>Who the player was following when camera mode starts (None = nobody).</summary>
        public AquariumCiv.Life.PersonRef PickFollow()
        {
            var card = PersonCardView.Instance;
            if (card != null && card.IsOpen) return AquariumCiv.Life.PersonRef.Notable(card.OpenId);
            var sm = ShowMeDirector.Instance;
            if (sm != null && sm.Busy && sm.Current != null && sm.Current.target.followId >= 0)
                return AquariumCiv.Life.PersonRef.Notable(sm.Current.target.followId);
            var sim = AquariumCiv.Game.GameClock.Instance != null ? AquariumCiv.Game.GameClock.Instance.Sim : null;
            var pin = sim?.Pinned;
            // Always the pinned person, on screen or not (user 2026-10-30): OnCameraMode flies there.
            return pin != null && pin.alive ? AquariumCiv.Life.PersonRef.Notable(pin.id) : AquariumCiv.Life.PersonRef.None;
        }

        void StepFollow(float dt)
        {
            if (!IsFollowing) return;
            if (followPeople == null) followPeople = PeopleView.Find();
            if (followPeople == null || !followPeople.TryGetDollPosition(Following, out var p))
            {
                if (++followMissing > 90 && !IsPinned(Following)) Follow(AquariumCiv.Life.PersonRef.None); // gone (indoors for good, died); the pinned are waited for
                return;
            }
            followMissing = 0;
            var w = followPeople.transform.TransformPoint(new Vector3(p.x, 0f, p.y));
            var bound = GlassBounds(zoom); // #273: a follow may centre anyone inside the glass; the spring stands down while code holds the camera
            HoldByCode(zoom);
            float gx = Mathf.Clamp(w.x, -bound.x, bound.x), gz = Mathf.Clamp(w.z, -bound.y, bound.y);
            float k = 1f - Mathf.Exp(-FollowRate * dt);
            target.x += (gx - target.x) * k;
            target.z += (gz - target.z) * k;
        }
        // ---------------- input ----------------
        /// <summary>The first two live fingers (or the mouse). <paramref name="mousePan"/>: a right or middle button is down.</summary>
        static void ReadPointers(out int n, out Vector2 a, out Vector2 b, out bool mousePan)
        {
            n = 0; a = b = default; mousePan = false;
            var ts = Touchscreen.current;
            if (ts != null)
            {
                foreach (var t in ts.touches)
                {
                    if (!t.isInProgress) continue;
                    if (n == 0) a = t.position.ReadValue(); else if (n == 1) b = t.position.ReadValue();
                    n++;
                }
                if (n > 0) return;
            }
            var m = Mouse.current;
            if (m == null) return;
            a = m.position.ReadValue();
            if (m.rightButton.isPressed || m.middleButton.isPressed) { n = 1; mousePan = true; }
            else if (m.leftButton.isPressed) n = 1;
        }

        bool ClaimedByGate(Vector2 pos)
        {
            for (int i = 0; i < gates.Count; i++) if (gates[i] != null && gates[i].ClaimsPress(pos)) return true;
            return false;
        }

        void EndPress(float now)
        {
            if (mode == Mode.Orbit && orbiting) velocity = BoxCameraMath.CapFling(BoxCameraMath.Velocity(samples, now));
            // A two-finger pan that moved away from the person followed lets them go (a pinch on the spot keeps them).
            if (IsFollowing && panTravel > FollowReleaseFraction * Mathf.Max(1f, Screen.height)) Follow(AquariumCiv.Life.PersonRef.None);
            panTravel = 0f;
            mode = Mode.None; orbiting = false; moved = false; samples.Clear();
        }

        void Orbit(Vector2 pos, float H, float now)
        {
            if (mode == Mode.TwoFinger || mode == Mode.Locked) { mode = Mode.Locked; return; } // one finger left of a two-finger gesture: ignore until all lift
            if (mode == Mode.None)
            {
                mode = Mode.Orbit; pressPos = lastPos = pos; orbiting = moved = false; velocity = Vector2.zero; samples.Clear();
                return;
            }
            if (!orbiting)
            {
                if (!BoxCameraMath.PastSlop(pos - pressPos, H) || now < HoldOrbitUntil) return;
                orbiting = moved = true; lastPos = pos; // the slop itself is swallowed: no jump when it is crossed
                return;
            }
            var d = pos - lastPos; lastPos = pos;
            float dYaw = BoxCameraMath.YawDegrees(d.x, H), dPitch = -BoxCameraMath.PitchDegrees(d.y, H);
            targetYaw = BoxCameraMath.SoftStep(targetYaw, dYaw, minYaw, maxYaw);
            targetPitch = BoxCameraMath.SoftStep(targetPitch, dPitch, BoxCameraMath.PitchFloor(zoom), BoxCameraMath.PitchCeiling);
            samples.Add((now, new Vector2(dYaw, dPitch)));
            if (samples.Count > 32) samples.RemoveAt(0);
        }

        /// <summary>Two fingers (or a right / middle mouse drag): pinch zoom towards the fingers and pan grabbing the ground.</summary>
        void PanZoom(int n, Vector2 a, Vector2 b, bool mousePan)
        {
            Vector2 mid = n >= 2 ? (a + b) * 0.5f : a;
            float dist = n >= 2 ? Vector2.Distance(a, b) : 0f;
            if (mode != Mode.TwoFinger)
            {
                mode = Mode.TwoFinger; codeHold = false; orbiting = false; moved = true; velocity = Vector2.zero; samples.Clear();
                prevMid = mid; prevDist = dist; panTravel = 0f;
                return;
            }
            panTravel += (mid - prevMid).magnitude;
            if (n >= 2 && dist > 1f && prevDist > 1f) ZoomAbout(Mathf.Clamp(zoom * prevDist / dist, Mathf.Min(minZoom, zoom), maxZoom), mid);
            if (GroundAt(prevMid, out var g0) && GroundAt(mid, out var g1))
            {
                var bound = ViewBounds(zoom);
                var d = g0 - g1;
                target.x = BoxCameraMath.GlassStep(target.x, d.x, -bound.x, bound.x);
                target.z = BoxCameraMath.GlassStep(target.z, d.z, -bound.y, bound.y);
            }
            StayOnLand();
            Apply();
            prevMid = mid; prevDist = dist;
        }

        /// <summary>Changes the zoom and moves the target (1 - z1/z0) of the way to the ground under <paramref name="screenPoint"/>.</summary>
        void ZoomAbout(float z1, Vector2 screenPoint)
        {
            float z0 = zoom;
            if (Mathf.Approximately(z0, z1)) return;
            if (GroundAt(screenPoint, out var g)) target = BoxCameraMath.ZoomTarget(target, new Vector3(g.x, target.y, g.z), z0, z1);
            zoom = z1;
            LiftPitchToFloor();
            StayOnLand();
            Apply();
        }

        /// <summary>#377 (playtest #336): the pitch floor rises as you zoom out (14 close, 24 at the full box). Left to the spring, a pinch out kept tilting the camera up for a second
        /// after the fingers stopped (height still climbing 4 units after the zoom hit its limit). The floor now lifts the pitch in the same frame, so the pitch is a plain function of the zoom.</summary>
        void LiftPitchToFloor()
        {
            float floor = BoxCameraMath.PitchFloor(zoom);
            if (targetPitch < floor) targetPitch = floor;
            if (pitch < floor) { pitch = floor; lastPitch = pitch; }
        }

        /// <summary>The rubber band and a pinch towards the wall both stop at the land's edge.</summary>
        void StayOnLand()
        {
            var stop = BoxCameraMath.PanHardStop;
            target.x = Mathf.Clamp(target.x, -stop.x, stop.x);
            target.z = Mathf.Clamp(target.z, -stop.y, stop.y);
        }

        bool GroundAt(Vector2 screenPoint, out Vector3 p)
        {
            p = default;
            return cam != null && BoxCameraMath.GroundPoint(cam.ScreenPointToRay(screenPoint), target.y, out p);
        }

        /// <summary>Whatever the target is past the pan bounds (they shrink as you zoom out) eases back.</summary>
        void SpringPan(float dt)
        {
            if (codeHold && zoom > holdZoom + 0.05f) codeHold = false;
            var bound = codeHold ? GlassBounds(zoom) : ViewBounds(zoom); // held by a flight or a follow: only the glass and the hull limit it
            target.x = BoxCameraMath.Spring(target.x, -bound.x, bound.x, dt);
            target.z = BoxCameraMath.Spring(target.z, -bound.y, bound.y, dt);
        }

        // #273: a flight or a follow may put the pivot past the view's limit (a person near a wall is centred); the spring waits until the player pans or zooms out.
        bool codeHold; float holdZoom;
        void HoldByCode(float z) { if (!codeHold) { codeHold = true; holdZoom = z; } else holdZoom = Mathf.Max(holdZoom, z); }
        Vector2 GlassBounds(float z) => BoxCameraMath.PanBoundsGlassFor(yaw, pitch, FitDistance() * z, target.y);

        /// <summary>#273: the pan range at a zoom, from the frustum at this camera's fov and aspect.</summary>
        Vector2 ViewBounds(float z)
        {
            if (cam == null) cam = GetComponent<Camera>();
            float fov = cam != null ? cam.fieldOfView : 22f, aspect = cam != null ? cam.aspect : 9f / 16f;
            return BoxCameraMath.PanBoundsFor(yaw, pitch, FitDistance() * z, target.y, Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad), aspect);
        }

        public void Apply()
        {
            transform.position = target + Quaternion.Euler(pitch, yaw, 0) * Vector3.back * (FitDistance() * zoom);
            TellHud();
            transform.LookAt(target);
        }
    }
}
