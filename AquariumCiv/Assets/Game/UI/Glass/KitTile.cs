using System;
using UnityEngine.UIElements;

namespace AquariumCiv.GameUI
{
    /// <summary>
    /// #668 (kit-redesign.md §4, mock-up A "tile shelf"): one control for everything in the Keeper's kit: the tools, the seed, the bushes
    /// and each life group. A square tile (the Shape tray's tile, radius 16): the icon top left, the name, and a short line under it
    /// (what it does, where it goes, or what a shut one needs). States are classes: <see cref="NewClass"/> (a brass ring and the New tag),
    /// <see cref="HandClass"/> (a primary ring and a check), <see cref="ShutClass"/> (#662: a thin timber edge, a paler fill, a lock and
    /// full-strength words, never a fade), <see cref="PickedClass"/> (the shut tile the note speaks of). A count badge ("×2") and a dot.
    /// Built on <see cref="GlassButton"/>, so taps, sounds and the reach stay as they are.
    /// </summary>
    [UxmlElement]
    public partial class KitTile : GlassButton
    {
        public const string TileClass = "kit-tile", SquareClass = "kit-tile--square", NewClass = "kit-tile--new", HandClass = "kit-tile--hand",
            ShutClass = "kit-tile--shut", PickedClass = "kit-tile--picked", EndClass = "kit-tile--end";

        readonly GlassIcon glyph;
        readonly Label nameLabel, lineLabel, countLabel;
        readonly VisualElement lockBadge, check, dot;
        readonly GlassNewTag tag;
        int count;

        public KitTile() : this("", "", "", null) { }

        public KitTile(string name, string icon, string line, Action tapped) : base("", tapped, ButtonKind.Secondary, UiRole.Toggle)
        {
            AddToClassList(TileClass);
            AddToClassList(SquareClass);
            Add(glyph = new GlassIcon(icon, "kit-tile-glyph"));
            Add(nameLabel = Part(name, "kit-tile-name"));
            Add(lineLabel = Part(line, "kit-tile-line"));
            Add(countLabel = Part("", "kit-tile-count"));
            countLabel.style.display = DisplayStyle.None;
            lockBadge = new VisualElement { pickingMode = PickingMode.Ignore };
            lockBadge.AddToClassList("kit-tile-lock");
            lockBadge.Add(new GlassIcon("lock", "kit-tile-lock-icon"));
            Add(lockBadge);
            check = new VisualElement { pickingMode = PickingMode.Ignore };
            check.AddToClassList("kit-tile-check");
            check.Add(new GlassIcon("check", "kit-tile-check-icon"));
            Add(check);
            Add(tag = new GlassNewTag());
            tag.AddToClassList("kit-tile-tag");
            dot = new VisualElement { pickingMode = PickingMode.Ignore };
            dot.AddToClassList("kg-dot");
            dot.AddToClassList("kit-tile-dot");
            Add(dot);
        }

        static Label Part(string s, string cls)
        {
            var l = new Label(s) { pickingMode = PickingMode.Ignore };
            l.AddToClassList(cls);
            return l;
        }

        /// <summary>The name on the tile ("Mister", "Rabbits").</summary>
        public string Name { get => nameLabel.text; set { if (nameLabel.text != value) nameLabel.text = value; } }
        /// <summary>The line under the name ("wets the land", "for the meadow", "needs deer").</summary>
        public string Line { get => lineLabel.text; set { if (lineLabel.text != value) lineLabel.text = value; } }
        /// <summary>The icon's shape (Icons/*.svg).</summary>
        public string Glyph { get => glyph.Shape; set { if (glyph.Shape != value) glyph.Shape = value; } }

        /// <summary>The count badge ("×2", #726); 0 hides it.</summary>
        public int Count
        {
            get => count;
            set
            {
                count = value;
                countLabel.text = value > 0 ? "×" + value : "";
                countLabel.style.display = value > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }

        /// <summary>The count badge's words (tests).</summary>
        public string CountText => count > 0 ? countLabel.text : "";

        public bool New { get => ClassListContains(NewClass); set => EnableInClassList(NewClass, value); }
        public bool Hand { get => ClassListContains(HandClass); set => EnableInClassList(HandClass, value); }
        /// <summary>#662: shut (a jar still to come). Never a fade: the look is the spec's unavailable look, and a tap still answers (the need).</summary>
        public bool Shut { get => ClassListContains(ShutClass); set => EnableInClassList(ShutClass, value); }
        public bool Picked { get => ClassListContains(PickedClass); set => EnableInClassList(PickedClass, value); }
        /// <summary>The last tile of its row of four: no right margin (the USS has no :nth-child).</summary>
        public bool End { set => EnableInClassList(EndClass, value); }
        public bool Dot { get => ClassListContains("kit-tile--dot"); set => EnableInClassList("kit-tile--dot", value); }
    }
}
