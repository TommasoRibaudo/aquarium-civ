using System;
using System.Collections.Generic;
using System.Linq;
using AquariumCiv.Sim;

namespace AquariumCiv.View.Shaping
{
    /// <summary>The tray's two rows (#84): gifts to the people, and landscaping (hill and hollow come after the slice).</summary>
    public enum TrayRow { Gifts, Landscaping }

    /// <summary>The one gesture a tool is used with (#84): rain is rubbed, a grove tapped, a channel dragged, a herd flicked
    /// in from the box's edge.</summary>
    public enum ToolGesture { Rub, Tap, Drag, FlickFromEdge }

    /// <summary>Where the tray is: closed, open (tools to pick from), or a tool in hand (shaping mode: banner and Cancel).</summary>
    public enum TrayMode { Closed, Open, Armed }

    /// <summary>Who a touch belongs to in shaping mode: one finger is the tool, two or more are the camera.</summary>
    public enum FingerRoute { None, Tool, Camera }

    /// <summary>One tool icon of the tray, everything the UI needs to draw it (#84).</summary>
    public struct TrayTool
    {
        public string kind;
        public TrayRow row;
        /// <summary>The tool's name at the box's era ("shower", "rain", "storm"); the icon is picked by <see cref="kind"/>.</summary>
        public string name;
        /// <summary>1 camp, 2 village, 3 town: the tool's level, grows with the era.</summary>
        public int level;
        /// <summary>The tool can be armed now.</summary>
        public bool enabled;
        /// <summary>Why not (None when enabled).</summary>
        public SimCore.ShapeFail fail;
        /// <summary>Short player text for the disabled state, empty when enabled.</summary>
        public string reason;
        /// <summary>This tool is the one in hand.</summary>
        public bool armed;
        /// <summary>How many of this tool stand in the box, and the most that can (the cap shown on the icon).</summary>
        public int standing, cap;
        public ToolGesture gesture;
    }

    /// <summary>The charge display: whole charges, the most, and the refill progress toward the next one.</summary>
    public struct ChargeDisplay
    {
        public int whole, max;
        /// <summary>0..1 toward the next charge; 0 when full.</summary>
        public float refill;
        public bool full;
        /// <summary>"2 of 3", for the icon's badge.</summary>
        public string text;
    }

    public enum TrayFeedbackKind { None, Placed, Failed, TakenBack, Info }

    /// <summary>The line the tray says last: a toast (placed, why a tool didn't work, taken back). <see cref="seq"/> goes up with
    /// every new one so the UI can restart its fade.</summary>
    public struct TrayFeedback
    {
        public TrayFeedbackKind kind;
        public string text;
        public int seq;
        /// <summary>For Failed: the cue to play (<see cref="ShapingShow.FailCue"/>); for Placed: the tool's cue, or null.</summary>
        public string cue;
        public int nudgeId;
    }

    /// <summary>One entry of the "Take back" list: a shaping the people haven't used yet.</summary>
    public struct TakeBackItem
    {
        public int nudgeId;
        public string kind;
        /// <summary>The name it was placed under ("shower"), for the list.</summary>
        public string name;
        /// <summary>World spot, to anchor the chip and frame the camera.</summary>
        public float x, z;
        /// <summary>Whole game months it has waited.</summary>
        public long ageMonths;
        /// <summary>"Take back the shower".</summary>
        public string label;
    }

    /// <summary>
    /// The shaping tray's model (#84; first-session-and-check-in §6, practices §4): pure C#, no Unity, no look. The UI
    /// (plain #36 HUD style now, restyled by #108 later) reads the properties and calls the verbs:
    /// <list type="bullet">
    /// <item><see cref="Tools"/> and <see cref="Charges"/>: what to draw.</item>
    /// <item><see cref="Open"/>, <see cref="Close"/>, <see cref="Select"/>, <see cref="Cancel()"/>: the modes.</item>
    /// <item><see cref="RouteFingers"/>, <see cref="BeginGesture"/>, <see cref="EndGesture"/>: one finger = tool, two = camera.</item>
    /// <item><see cref="PlaceAt"/>: the tool lands (through <see cref="ShapingShow.Place"/>); <see cref="Feedback"/> says what happened.</item>
    /// <item><see cref="TakeBackList"/>, <see cref="TakeBack"/>: undo while the people haven't used it.</item>
    /// <item><see cref="ArmForWish"/>, <see cref="Confirm"/>: the Wish card's Give, pre-armed at the wished spot.</item>
    /// </list>
    /// It writes to the sim only through the show/sim placing and take-back calls, never draws dice, and never changes the
    /// economy. It holds no clock: the UI fades toasts itself.
    /// </summary>
    public sealed class ShapingTray
    {
        /// <summary>A placed tool leaves shaping mode (one gift per arming) unless this is true.</summary>
        public bool StayArmedAfterPlace;

        public TrayMode Mode { get; private set; }
        /// <summary>The tool in hand ("" when none).</summary>
        public string Armed { get; private set; } = "";
        /// <summary>True while the finger is mid-gesture with the tool (a rub, a drag, a flick).</summary>
        public bool GestureActive { get; private set; }
        public TrayFeedback Feedback { get; private set; }

        /// <summary>A wish's spot the tool is pre-armed at (the Wish card's Give), or none.</summary>
        public bool HasPending { get; private set; }
        public float PendingX { get; private set; }
        public float PendingZ { get; private set; }
        public int PendingWishId { get; private set; } = -1;
        /// <summary>The notable the pending gift will be credited to (-1 = none).</summary>
        public int PendingNotableId { get; private set; } = -1;

        int seq;

        // ================================================================== what to draw

        /// <summary>The era a tool comes in at (the sim's <see cref="SimCore.ShapingUnlocked"/> says it for the box's era).</summary>
        public static Era UnlockEra(string kind) => kind == SimCore.NudgeKind.Channel ? Era.Village : Era.Camp;

        public static TrayRow RowOf(string kind) => TrayRow.Gifts; // hill and hollow (landscaping) come after the slice

        public static ToolGesture GestureOf(string kind) => kind switch
        {
            SimCore.NudgeKind.Rain => ToolGesture.Rub,
            SimCore.NudgeKind.Channel => ToolGesture.Drag,
            SimCore.NudgeKind.Herd => ToolGesture.FlickFromEdge,
            _ => ToolGesture.Tap,
        };

        /// <summary>The banner line while the tool is in hand: what the finger does.</summary>
        public static string GestureHint(string kind) => GestureOf(kind) switch
        {
            ToolGesture.Rub => "Rub the sky over the land you want wet",
            ToolGesture.Drag => "Drag from the water to where the channel should run",
            ToolGesture.FlickFromEdge => "Flick in from the edge to send them",
            _ => "Tap the ground where it should grow",
        };

        /// <summary>Short player text for why a tool is disabled (practices §4: say why, in the player's words).</summary>
        public static string ReasonText(string kind, SimCore.ShapeFail fail)
        {
            switch (fail)
            {
                case SimCore.ShapeFail.Locked: return $"Comes with the {UnlockEra(kind).ToString().ToLowerInvariant()}";
                case SimCore.ShapeFail.NoCharge: return "No charge left. Clean the glass for more";
                case SimCore.ShapeFail.AtCap:
                    int cap = SimCore.ShapingCap(kind);
                    return cap == 1 ? "Already out there. Wait for the people" : $"The box holds {cap} at once";
                case SimCore.ShapeFail.Unknown: return "Can't be used here";
                default: return "";
            }
        }

        /// <summary>The tray's tools in order (rain, grove, herd, channel), each with its name, level, state and why.
        /// Locked tools are listed, greyed with their reason, so the player sees what is coming.</summary>
        public IReadOnlyList<TrayTool> Tools(SimCore sim)
        {
            var list = new List<TrayTool>();
            foreach (var kind in SimCore.SliceTools) list.Add(ToolOf(sim, kind));
            return list;
        }

        public TrayTool ToolOf(SimCore sim, string kind)
        {
            var fail = sim.CanShape(kind);
            return new TrayTool
            {
                kind = kind,
                row = RowOf(kind),
                name = sim.ShapingToolName(kind),
                level = (int)sim.State.era + 1,
                enabled = fail == SimCore.ShapeFail.None,
                fail = fail,
                reason = ReasonText(kind, fail),
                armed = Mode == TrayMode.Armed && Armed == kind,
                standing = sim.ShapingStanding(kind),
                cap = SimCore.ShapingCap(kind),
                gesture = GestureOf(kind),
            };
        }

        public ChargeDisplay Charges(SimCore sim)
        {
            int whole = sim.ShapingChargesWhole;
            int max = (int)Math.Floor(sim.ShapingMaxCharges + 1e-4f);
            bool full = whole >= max;
            float frac = full ? 0f : Math.Max(0f, Math.Min(1f, sim.ShapingCharges - whole));
            return new ChargeDisplay { whole = whole, max = max, refill = frac, full = full, text = $"{whole} of {max}" };
        }

        /// <summary>The banner while a tool is in hand ("Shower: rub the sky..."), or empty.</summary>
        public string Banner(SimCore sim) =>
            Mode == TrayMode.Armed && Armed.Length > 0
                ? Cap(sim.ShapingToolName(Armed)) + ": " + (HasPending ? "tap to give it here, or drag it elsewhere" : GestureHint(Armed))
                : "";

        // ================================================================== modes

        /// <summary>The HUD's shaping button opens the tray (from closed; an armed tool stays in hand).</summary>
        public void Open() { if (Mode == TrayMode.Closed) Mode = TrayMode.Open; }

        /// <summary>Shut the tray; a tool in hand is put down.</summary>
        public void Close() { Cancel(false); Mode = TrayMode.Closed; }

        /// <summary>Pick a tool: shaping mode starts (banner and Cancel). A tool that can't be used says why and isn't armed.</summary>
        public bool Select(SimCore sim, string kind)
        {
            var fail = sim.CanShape(kind);
            if (fail != SimCore.ShapeFail.None) { Say(TrayFeedbackKind.Failed, ReasonText(kind, fail), ShapingShow.FailCue); return false; }
            Mode = TrayMode.Armed;
            Armed = kind;
            GestureActive = false;
            ClearPendingSpot();
            return true;
        }

        /// <summary>Cancel: the tool goes back to the tray (nothing was spent). A pending wish stays open in the sim.</summary>
        public void Cancel() => Cancel(true);

        void Cancel(bool toTray)
        {
            bool had = Mode == TrayMode.Armed;
            Armed = "";
            GestureActive = false;
            ClearPendingSpot();
            if (had && toTray) Mode = TrayMode.Open;
        }

        void ClearPendingSpot() { HasPending = false; PendingWishId = -1; PendingNotableId = -1; PendingX = PendingZ = 0f; }

        // ================================================================== fingers

        /// <summary>One finger on the ground is the tool while one is in hand; two or more are the camera, always. The UI asks on
        /// every touch count change. A second finger landing mid-gesture abandons the gesture (nothing is placed).</summary>
        public FingerRoute RouteFingers(int fingers)
        {
            if (fingers <= 0) return FingerRoute.None;
            if (fingers >= 2) { GestureActive = false; return FingerRoute.Camera; }
            return Mode == TrayMode.Armed ? FingerRoute.Tool : FingerRoute.None;
        }

        /// <summary>The finger went down with the tool. False when no tool is in hand.</summary>
        public bool BeginGesture() { if (Mode != TrayMode.Armed) return false; GestureActive = true; return true; }

        /// <summary>The finger lifted. True if the gesture was still the tool's (not taken by the camera); then place it.</summary>
        public bool EndGesture() { bool was = GestureActive; GestureActive = false; return was; }

        // ================================================================== placing

        /// <summary>The tool lands at a point (box coordinates). Says what happened in <see cref="Feedback"/>; on success the tray
        /// leaves shaping mode (unless <see cref="StayArmedAfterPlace"/>). On a fail the tool is put down and the reason said.</summary>
        public SimCore.ShapeResult PlaceAt(SimCore sim, ShapingShow show, float x, float z)
        {
            if (Mode != TrayMode.Armed || Armed.Length == 0) return new SimCore.ShapeResult { fail = SimCore.ShapeFail.Unknown };
            string kind = Armed;
            int credit = HasPending ? PendingNotableId : -1;
            var r = show.Place(sim, kind, x, z, credit);
            Finish(sim, kind, r);
            return r;
        }

        void Finish(SimCore sim, string kind, SimCore.ShapeResult r)
        {
            if (!r.Ok)
            {
                Say(TrayFeedbackKind.Failed, ReasonText(kind, r.fail), ShapingShow.FailCue);
                Cancel(true); // the tool can't be used now: back to the tray with the reason showing
                return;
            }
            int left = sim.ShapingChargesWhole;
            Say(TrayFeedbackKind.Placed, $"{Cap(sim.ShapingToolName(kind))} placed. {(left == 1 ? "1 charge" : left + " charges")} left", ShapingShow.PlaceCue(kind), r.nudge.id);
            if (StayArmedAfterPlace && left > 0 && sim.CanShape(kind) == SimCore.ShapeFail.None) { GestureActive = false; ClearPendingSpot(); }
            else { Armed = ""; GestureActive = false; ClearPendingSpot(); Mode = TrayMode.Closed; }
        }

        // ================================================================== take back

        /// <summary>The shapings the people haven't used yet, newest first: the "Take back" list.</summary>
        public IReadOnlyList<TakeBackItem> TakeBackList(SimCore sim)
        {
            var list = new List<TakeBackItem>();
            foreach (var n in sim.ShapingsWaiting().OrderByDescending(n => n.id))
            {
                if (!sim.CanTakeBack(n.id) || Array.IndexOf(SimCore.SliceTools, n.kind) < 0) continue;
                string name = SimCore.ShapingToolName(n.kind, (Era)n.eraAtPlace);
                list.Add(new TakeBackItem
                {
                    nudgeId = n.id, kind = n.kind, name = name, x = n.x, z = n.z, ageMonths = sim.State.month - n.month,
                    label = $"Take back the {name}",
                });
            }
            return list;
        }

        /// <summary>Take one back (<see cref="ShapingShow.TakeBack"/>); the charge returns. False once the people used it.</summary>
        public bool TakeBack(SimCore sim, ShapingShow show, int nudgeId)
        {
            var n = sim.State.nudges.Find(x => x.id == nudgeId);
            string name = n != null ? SimCore.ShapingToolName(n.kind, (Era)n.eraAtPlace) : "gift";
            if (!show.TakeBack(sim, nudgeId))
            {
                Say(TrayFeedbackKind.Info, "The people have already used it", null, nudgeId);
                return false;
            }
            Say(TrayFeedbackKind.TakenBack, $"The {name} is taken back. Charge returned", null, nudgeId);
            return true;
        }

        // ================================================================== the wish hook

        /// <summary>The Wish card's Give: opens shaping mode with the wish's tool in hand, pre-armed at the wished spot
        /// (<see cref="PendingX"/>, <see cref="PendingZ"/>). The player confirms with <see cref="Confirm"/>, or Cancels (the wish
        /// stays open). A tool that can't be used says why. False for a wish that isn't open or can't be given.</summary>
        public bool ArmForWish(SimCore sim, int wishId)
        {
            var w = sim.FindWish(wishId);
            if (w == null || w.state != WishState.Open) return false;
            var fail = sim.CanGiveWish(wishId);
            if (fail != SimCore.ShapeFail.None) { Say(TrayFeedbackKind.Failed, ReasonText(w.kind, fail), ShapingShow.FailCue); return false; }
            Mode = TrayMode.Armed;
            Armed = w.kind;
            GestureActive = false;
            HasPending = true;
            PendingWishId = wishId;
            PendingNotableId = w.notableId;
            PendingX = w.x;
            PendingZ = w.z;
            return true;
        }

        /// <summary>The player drags the pre-armed spot elsewhere.</summary>
        public void MovePending(float x, float z) { if (HasPending) { PendingX = x; PendingZ = z; } }

        /// <summary>Confirm the pre-armed gift at <see cref="PendingX"/>/<see cref="PendingZ"/>: the sim's own wish call when the
        /// spot is the wish's (the wish becomes Given), else a plain placing credited to the wisher (the sim meets the wish
        /// when a gift of its tool stands). The wish's own call is not routed through the show, so its next <see cref="ShapingShow.Sync"/> picks the shaping up (and plays its Placed event).</summary>
        public SimCore.ShapeResult Confirm(SimCore sim, ShapingShow show)
        {
            if (!HasPending) return new SimCore.ShapeResult { fail = SimCore.ShapeFail.Unknown };
            var w = sim.FindWish(PendingWishId);
            string kind = Armed;
            if (w != null && Math.Abs(w.x - PendingX) < 1e-4f && Math.Abs(w.z - PendingZ) < 1e-4f)
            {
                var r = sim.GiveWish(PendingWishId);
                Finish(sim, kind, r);
                return r;
            }
            return PlaceAt(sim, show, PendingX, PendingZ);
        }

        // ================================================================== debug

        /// <summary>One line for the debug panel: the mode, the tool in hand, the charges, each tool's state.</summary>
        public string Describe(SimCore sim)
        {
            var c = Charges(sim);
            var tools = string.Join(", ", Tools(sim).Select(t => $"{t.kind}:{(t.enabled ? "ok" : t.fail.ToString())} {t.standing}/{t.cap}"));
            return $"tray {Mode} armed={(Armed.Length == 0 ? "-" : Armed)}{(HasPending ? $" wish#{PendingWishId}@{PendingX:0.0},{PendingZ:0.0}" : "")} charges {c.text} +{c.refill:0.00} | {tools} | takeback {TakeBackList(sim).Count}";
        }

        // ================================================================== helpers

        void Say(TrayFeedbackKind kind, string text, string cue, int nudgeId = -1) =>
            Feedback = new TrayFeedback { kind = kind, text = text, cue = cue, seq = ++seq, nudgeId = nudgeId };

        static string Cap(string s) => string.IsNullOrEmpty(s) ? s : char.ToUpperInvariant(s[0]) + s.Substring(1);
    }
}
