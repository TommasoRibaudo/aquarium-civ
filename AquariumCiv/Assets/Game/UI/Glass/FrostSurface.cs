using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace AquariumCiv.GameUI
{
    /// <summary>
    /// The one switch from frosted to flat sheets (decisions.md 2026-10-31, spec §2). If frost fails on a phone, change
    /// <see cref="Default"/> to false and nothing else: every surface goes flat, no copy of the frame, no filter, no pass. It also
    /// owns the frame copy: it runs only while a frosted surface is really on screen, and one surface is frosted at a time.
    /// A second one shown takes the frost and the first waits on flat.
    /// </summary>
    public static class FrostSwitch
    {
        /// <summary>THE setting. false = flat everywhere.</summary>
        public const bool Default = true;
        public const int CopyDivisor = 4;
        /// <summary>The board's 14 pt. limits.md's "blur 14" is 14 panel px, about 5 pt. Both wait for the phone test.</summary>
        public const float BlurLarge = 38.5f, BlurSmall = 14f;

        static bool on = Default;
        static bool suspended, owned;
        static float blur = BlurLarge;
        static Background copy;
        static readonly List<FrostSurface> shown = new();

        /// <summary>Frost in force. The debug panel flips this for a session; a build ships <see cref="Default"/>.</summary>
        public static bool On
        {
            get => on;
            set { if (on != value) { on = value; Refresh(); } }
        }

        /// <summary>Panel px of blur. The debug panel trades the two sizes.</summary>
        public static float Blur
        {
            get => blur;
            set { if (!Mathf.Approximately(blur, value)) { blur = value; Refresh(); } }
        }

        /// <summary>The phone frost test owns the copy while it is open: surfaces stay flat under it.</summary>
        public static bool Suspended
        {
            get => suspended;
            set { if (suspended != value) { suspended = value; Refresh(); } }
        }

        /// <summary>Surfaces on screen now.</summary>
        public static int ShownCount => shown.Count;
        /// <summary>The surface holding the frost, or null.</summary>
        public static FrostSurface Frosted { get; private set; }

        internal static void Add(FrostSurface s)
        {
            shown.Remove(s);
            shown.Add(s);
            Refresh();
        }

        internal static void Remove(FrostSurface s)
        {
            if (shown.Remove(s) || Frosted == s) Refresh();
        }

        static void Refresh()
        {
            var top = on && !suspended && shown.Count > 0 ? shown[shown.Count - 1] : null;
            if (top != null && !owned)
            {
                copy = Background.FromRenderTexture(FrostBackdrop.Start(CopyDivisor));
                owned = true;
            }
            Frosted = top;
            foreach (var s in shown) s.Paint(s == top, copy, blur);
            if (top == null && owned)
            {
                owned = false;
                copy = default;
                if (!suspended || FrostBackdrop.Active) FrostBackdrop.Stop();
            }
        }
    }

    /// <summary>
    /// A surface of the Keeper's Glass: bottom to top paper, the sharp copy, the blurred copy, the tint. A blur fades to nothing at its
    /// own edge, which here is the screen's, so without the sharp copy a light band shows along the sheet's sides. Frost uses
    /// <c>.surface</c>, flat swaps it for <c>.surface--flat</c>. Layout never changes. It asks for frost only between
    /// <see cref="Show"/> and <see cref="Hide"/>, and only while it and its ancestors are displayed.
    /// </summary>
    [UxmlElement]
    public partial class FrostSurface : VisualElement
    {
        public const string FrostClass = "surface", FlatClass = "surface--flat";
        const float Bleed = 48f; // the blur's element reaches this far above the sheet, so its top edge stays soft
        const int WatchMs = 150;

        readonly VisualElement baseImage, inner, image;
        bool wanted, registered;
        IVisualElementScheduledItem watch, follow;
        float lastBlur = -1f;

        public bool IsFrosted { get; private set; }
        /// <summary>Stays flat whatever the switch says: the reset confirm, over its scrim (question 32).</summary>
        public bool KeepFlat { get; set; }

        public FrostSurface()
        {
            pickingMode = PickingMode.Ignore;
            AddToClassList(FlatClass);
            baseImage = Layer("kg-frost-image");
            inner = Layer("kg-frost-inner");
            image = new VisualElement { pickingMode = PickingMode.Ignore };
            image.AddToClassList("kg-frost-image");
            inner.Add(image);
            Layer("kg-frost-tint");
            RegisterCallback<GeometryChangedEvent>(_ => Align());
            RegisterCallback<DetachFromPanelEvent>(_ => Unregister());
            RegisterCallback<AttachToPanelEvent>(_ => { if (wanted) Check(); });
        }

        VisualElement Layer(string cls)
        {
            var e = new VisualElement { pickingMode = PickingMode.Ignore };
            e.AddToClassList(cls);
            Add(e);
            return e;
        }

        /// <summary>This surface is on screen: frost it if it can be.</summary>
        public void Show()
        {
            wanted = true;
            Check();
            watch ??= schedule.Execute(Check).Every(WatchMs);
            watch.Resume();
        }

        /// <summary>Gone from the screen: no copy on its account.</summary>
        public void Hide()
        {
            wanted = false;
            watch?.Pause();
            Unregister();
        }

        /// <summary>Keeps the copy lined up with the world for <paramref name="ms"/>, while the surface is moving.</summary>
        public void Follow(int ms)
        {
            follow?.Pause();
            follow = schedule.Execute(Align).Every(0).ForDuration(ms + 60);
        }

        bool Displayed()
        {
            if (panel == null) return false;
            for (VisualElement e = this; e != null; e = e.parent)
                if (e.resolvedStyle.display == DisplayStyle.None || e.style.display == DisplayStyle.None) return false;
            return true;
        }

        void Check()
        {
            bool show = wanted && !KeepFlat && Displayed();
            if (show == registered) return;
            registered = show;
            if (show) FrostSwitch.Add(this); else FrostSwitch.Remove(this);
            if (!show) Paint(false, default, 0f);
        }

        void Unregister()
        {
            bool was = registered;
            registered = false;
            Paint(false, default, 0f);
            if (was) FrostSwitch.Remove(this);
        }

        /// <summary>Called by the switch. Swaps the class in this one place.</summary>
        internal void Paint(bool frost, Background copy, float blur)
        {
            IsFrosted = frost;
            RemoveFromClassList(frost ? FlatClass : FrostClass);
            AddToClassList(frost ? FrostClass : FlatClass);
            var bg = frost ? new StyleBackground(copy) : new StyleBackground(StyleKeyword.None);
            image.style.backgroundImage = bg;
            baseImage.style.backgroundImage = bg;
            if (!frost) return;
            var size = new BackgroundSize(Length.Percent(100f), Length.Percent(100f));
            image.style.backgroundSize = size;
            baseImage.style.backgroundSize = size;
            if (!Mathf.Approximately(lastBlur, blur))
            {
                lastBlur = blur;
                var f = new FilterFunction(FilterFunctionType.Blur);
                f.AddParameter(new FilterParameter(blur));
                inner.style.filter = new List<FilterFunction> { f };
            }
            Align();
        }

        /// <summary>The whole-screen copy laid under the surface, so it lines up with the tank.</summary>
        void Align()
        {
            if (!IsFrosted || panel == null) return;
            var screen = panel.visualTree.worldBound;
            var me = worldBound;
            image.style.left = -me.x;
            image.style.top = Bleed - me.y;
            image.style.width = screen.width;
            image.style.height = screen.height;
            baseImage.style.left = -me.x;
            baseImage.style.top = -me.y;
            baseImage.style.width = screen.width;
            baseImage.style.height = screen.height;
        }
    }
}
