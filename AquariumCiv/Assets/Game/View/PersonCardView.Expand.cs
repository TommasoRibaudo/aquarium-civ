using AquariumCiv.GameUI;
using UnityEngine;
using UnityEngine.UIElements;

namespace AquariumCiv.View
{
    /// <summary>
    /// #403 (playtest note #384): the person card can grow. At 30 % its scroll shows two or three lines, too few to read a life. A grab
    /// handle on the card's top edge, tapped, or a swipe up or down on the handle or the card's head row, switches the card between 30 %
    /// and <see cref="ExpandedShare"/>. Rule 5 still holds: while it's tall the camera stands the person at <see cref="ExpandedAnchorY"/>,
    /// above the card. Every card opens at 30 % (spec §2).
    /// </summary>
    public partial class PersonCardView
    {
        /// <summary>The tall card's height (share of the screen): the spec's 59 % sheet.</summary>
        public const float ExpandedShare = 0.59f;
        /// <summary>Where the person stands while the card is tall (fraction of the height from the bottom), under the HUD's top row.</summary>
        public const float ExpandedAnchorY = 0.8f;
        /// <summary>How far (panel px) a finger moves on the handle or head before it's a swipe, not a tap: about 16 pt.</summary>
        public const float SwipePx = 44f;
        /// <summary>How far above the tall card's edge (share of the screen) the doll's middle must stand: half a doll and a little.</summary>
        public const float ClearShare = 0.05f;
        const string TallClass = "pc--tall";

        public bool Expanded { get; private set; }
        public VisualElement Grab => grab;

        VisualElement grab;
        Vector2 pressAt;
        int pressId = -1;
        /// <summary>#484: while the camera flies for the tall card, the card grows under the person (never over them): when that began, and
        /// its height now (share of the screen).</summary>
        float growSince = -1f, growShare;
        /// <summary>#484: how long the card may take to reach its full height behind the flight (real seconds) before it snaps.</summary>
        public const float GrowSeconds = BoxCameraMath.FlyDefaultSeconds + 0.4f;
        /// <summary>The card is growing after the camera now (tests).</summary>
        public bool Growing => growSince >= 0f;

        /// <summary>Called once from EnsureUi: the handle on the card's top edge, and the swipe on the head row.</summary>
        void BuildExpandUi(VisualElement head)
        {
            grab = new VisualElement { name = "person-card-grab" };
            grab.AddToClassList("pc-grab");
            var pill = new VisualElement { pickingMode = PickingMode.Ignore };
            pill.AddToClassList("pc-grab-pill");
            grab.Add(pill);
            sheet.hierarchy.Add(grab); // over the content box, in the sheet's top padding
            Swipeable(grab, true);
            Swipeable(head, false);
        }

        void Swipeable(VisualElement e, bool tapToggles)
        {
            e.RegisterCallback<PointerDownEvent>(ev =>
            {
                if (ev.target != e) return; // a button in the head keeps its own press
                pressAt = ev.position;
                pressId = ev.pointerId;
                e.CapturePointer(ev.pointerId);
                ev.StopPropagation();
            });
            e.RegisterCallback<PointerUpEvent>(ev =>
            {
                if (ev.pointerId != pressId || !e.HasPointerCapture(ev.pointerId)) return;
                e.ReleasePointer(ev.pointerId);
                pressId = -1;
                float dy = ev.position.y - pressAt.y; // panel space: down is +
                if (dy < -SwipePx) SetExpanded(true);
                else if (dy > SwipePx) SetExpanded(false);
                else if (tapToggles && Mathf.Abs(ev.position.x - pressAt.x) < SwipePx) SetExpanded(!Expanded);
                ev.StopPropagation();
            });
            e.RegisterCallback<PointerCaptureOutEvent>(_ => pressId = -1);
        }

        /// <summary>Make the open card tall or bring it back to 30 %. A tall card that would now cover its person flies the camera.</summary>
        public void SetExpanded(bool on)
        {
            if (sheet == null || on == Expanded || (on && !IsOpen)) return;
            Expanded = on;
            UiSound.Play(UiRole.Toggle);
            bool fly = on && IsOpen && PersonUnder(ExpandedShare);
            if (fly) flownForCard = FlyTo(Who);
            // #484: with a flight, the card grows behind the camera, kept under the person, instead of snapping over them for a second
            if (fly && flownForCard) { growSince = Time.unscaledTime; growShare = CardMaxShare; }
            else growSince = -1f;
            ApplyExpanded();
        }

        /// <summary>#484, every frame from Update: while growing, the card's top stays under the person's head (and their margin), rising
        /// as the camera lifts them, until it's full height or the flight has had its time.</summary>
        void TickGrow()
        {
            if (growSince < 0f || sheet == null) return;
            if (!Expanded || !IsOpen) { EndGrow(); return; }
            float limit = ExpandedShare;
            var pv = PeopleView.Find();
            var cam = Camera.main;
            if (pv != null && cam != null && cardRoot?.panel != null && pv.TryGetDollTop(Who, out var top))
            {
                var at = ToPanel(cam, top, out bool ok);
                float h = cardRoot.worldBound.height;
                if (ok && h > 1f) limit = Mathf.Clamp((h - at.y - KeepClear) / h, CardMaxShare, ExpandedShare);
            }
            growShare = Mathf.Max(growShare, limit); // it never shrinks back while it grows
            if (growShare >= ExpandedShare - 0.002f || Time.unscaledTime - growSince > GrowSeconds) { EndGrow(); return; }
            sheet.style.height = Length.Percent(growShare * 100f);
            sheet.Surface.Follow(60);
        }

        void EndGrow()
        {
            growSince = -1f;
            if (sheet == null) return;
            sheet.style.height = StyleKeyword.Null; // the class's 59 % (or 30 %) again
            sheet.Surface.Follow(GlassMotion.SlideMs);
        }

        void ApplyExpanded()
        {
            if (sheet == null) return;
            if (growSince >= 0f) sheet.style.height = Length.Percent(CardMaxShare * 100f); // #484: it starts at 30 % and grows in TickGrow
            else sheet.style.height = StyleKeyword.Null;
            sheet.EnableInClassList(GlassSheet.HeightClass(SheetHeight.Standard), !Expanded);
            sheet.EnableInClassList(GlassSheet.HeightClass(SheetHeight.Tall), Expanded);
            sheet.EnableInClassList(TallClass, Expanded);
            sheet.Surface.Follow(GlassMotion.SlideMs);
        }

        /// <summary>Back to 30 % with no sound (the card closed).</summary>
        void ResetExpanded()
        {
            growSince = -1f;
            if (!Expanded) return;
            Expanded = false;
            ApplyExpanded();
        }

        /// <summary>The camera target that stands the doll's middle at <paramref name="anchorY"/> of the screen, through the real camera's
        /// projection (the closed form in <see cref="ShowMe.ShowMeDirector.AboveCard"/> landed the tall card's person at 0.56 for 0.8). The
        /// target slides toward the viewer along the ground, at the camera's own target height, as <see cref="BoxCamera.FlyTo"/> keeps it.</summary>
        public static Vector3 AboveCardExact(BoxCamera cam, Vector3 feet, Vector3 mid, float zoom, float anchorY, out float reached)
        {
            reached = anchorY;
            var c = cam.GetComponent<Camera>();
            if (c == null) return feet;
            float dist = cam.FitDistance() * Mathf.Clamp(zoom, cam.minZoom, cam.maxZoom);
            var back = Quaternion.Euler(cam.pitch, cam.yaw, 0f) * Vector3.back * dist;
            var fwd = Quaternion.Euler(0f, cam.yaw, 0f) * Vector3.forward;
            var proj = c.projectionMatrix;
            Vector3 At(float s) => new(feet.x - fwd.x * s, cam.target.y, feet.z - fwd.z * s);
            float ViewY(float s)
            {
                var t = At(s);
                var pos = t + back;
                var view = Matrix4x4.TRS(pos, Quaternion.LookRotation(t - pos), new Vector3(1f, 1f, -1f)).inverse;
                var clip = proj * view * new Vector4(mid.x, mid.y, mid.z, 1f);
                return clip.w <= 1e-5f ? 0f : 0.5f + 0.5f * clip.y / clip.w;
            }
            // Never past the pan bounds, and along the line through the person: a clamp on one axis alone slides them sideways.
            var b = BoxCameraMath.PanBounds(zoom);
            float most = dist;
            if (Mathf.Abs(fwd.x) > 1e-4f) most = Mathf.Min(most, (feet.x + Mathf.Sign(fwd.x) * b.x) / fwd.x);
            if (Mathf.Abs(fwd.z) > 1e-4f) most = Mathf.Min(most, (feet.z + Mathf.Sign(fwd.z) * b.y) / fwd.z);
            float lo = 0f, hi = Mathf.Max(0f, most); // the doll rises on screen as the target slides toward the viewer
            reached = ViewY(hi);
            if (reached < anchorY) return At(hi); // as high as fits
            for (int i = 0; i < 24; i++)
            {
                float m = 0.5f * (lo + hi);
                if (ViewY(m) < anchorY) lo = m; else hi = m;
            }
            return At(0.5f * (lo + hi));
        }

        /// <summary>The tall card's flight: as <see cref="FlyTo"/>, to <see cref="AboveCardExact"/>.</summary>
        bool FlyAboveTallCard()
        {
            var cam = Cam;
            var pv = PeopleView.Find();
            if (cam == null || pv == null || !pv.TryGetDollFeet(Who, out var feet) || !pv.TryGetDollTop(Who, out var top)) return false;
            float zoom = Mathf.Min(cam.zoom, BoxCameraMath.FlyPersonZoom);
            var mid = (feet + top) * 0.5f;
            var focus = AboveCardExact(cam, feet, mid, zoom, ExpandedAnchorY, out float reached);
            // Near the front glass the pan stops short and the person would stay under the card: a closer camera lifts them (they stand
            // past its target), down to the closest zoom.
            for (int i = 0; i < 8 && reached < ExpandedShare + ClearShare && zoom > cam.minZoom + 1e-3f; i++)
            {
                zoom = Mathf.Max(cam.minZoom, zoom * 0.85f);
                focus = AboveCardExact(cam, feet, mid, zoom, ExpandedAnchorY, out reached);
            }
            cam.ReturnAfterFlight();
            lastFlight = cam.FlyTo(focus, zoom, BoxCameraMath.FlyDefaultSeconds);
            return true;
        }

        /// <summary>The open person would stand on or under a card of this share of the screen: #485, their middle (not only their head) must
        /// stand <see cref="ClearShare"/> above its edge, the clearance the flight gives a flown person (a back person's feet touched the edge).</summary>
        bool PersonUnder(float share)
        {
            var pv = PeopleView.Find();
            var cam = Camera.main;
            if (pv == null || cam == null || cardRoot?.panel == null || !pv.TryGetDollTop(Who, out var top) || !pv.TryGetDollFeet(Who, out var feet)) return false;
            var at = ToPanel(cam, (top + feet) * 0.5f, out bool ok);
            float h = cardRoot.worldBound.height;
            float edge = h * (1f - share);
            return !ok || at.y > edge - ClearShare * h;
        }
    }
}
