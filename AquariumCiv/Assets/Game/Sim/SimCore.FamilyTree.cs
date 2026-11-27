using System;
using System.Collections.Generic;
using System.Linq;

namespace AquariumCiv.Sim
{
    /// <summary>One face in a family tree (#774, tree A).</summary>
    public sealed class TreeNode
    {
        public int id;
        public string name = "", fullName = "";
        public bool alive;
        public int bornYear, diedYear = -1;
        public int gen;                 // 1 = the founding couple's row
        public int row;                 // the row it is drawn in, 0 at the top (empty generations drop out)
        public float x;                 // its centre, in cells from the tree's left edge
        public bool blood;              // false: married in, drawn beside their partner
        public int mateOf = -1;         // married in: the partner they stand beside
    }

    /// <summary>Two faces side by side, joined by the couple's line.</summary>
    public sealed class TreeCouple
    {
        public int a, b;                // a is the family's own, b married in
    }

    /// <summary>A household's children hanging from its line: the drop from the couple, the bus, a short line to each child.</summary>
    public sealed class TreeBranch
    {
        public int ownerId;             // the family's own person whose household this is
        public float fromX;             // where the drop leaves the couple (cells)
        public int fromRow;
        public List<float> childX = new(); // the children's centres (a folded branch: the chip's)
        public bool folded;
        public int hidden;              // folded: everyone below it
    }

    /// <summary>A generation's row and its gutter label ("G2 · Yr 30").</summary>
    public sealed class TreeGen
    {
        public int gen, row, year;
    }

    /// <summary>
    /// The whole family of one founding couple (#774, family tree A, approved 2026-11-26): founders at the top, then each generation in
    /// its row, the family's own with their partners beside them, the dead kept for everyone. Built from the sim on demand, never saved;
    /// pure reads, no dice. Positions are in cells (one face's width); the view scales them.
    /// </summary>
    public sealed class FamilyTree
    {
        public int rootId;
        public string familyName = "", key = "";
        public List<TreeNode> nodes = new();
        public List<TreeCouple> couples = new();
        public List<TreeBranch> branches = new();
        public List<TreeGen> gens = new();
        public float width;             // cells
        public int rows;
        public int people;              // everyone in the family, folded or not
        public TreeNode Node(int id) => nodes.FirstOrDefault(n => n.id == id);
    }

    public partial class SimCore
    {
        /// <summary>Tree pages in the book at most: the biggest families first.</summary>
        public const int BookTreeCap = 4;

        /// <summary>The book's page key for a family tree.</summary>
        public static string BookTreeKey(int rootId) => "tree:" + rootId;

        /// <summary>Everyone this person is a couple with, living or dead: their partner, anyone who has them as partner, and the
        /// other parent of any of their children. Oldest tie first (by the first child, then by id).</summary>
        public List<int> MatesOf(int id)
        {
            var me = FindNotable(id);
            var list = new List<int>();
            if (me == null) return list;
            void Add(int o) { if (o >= 0 && o != id && FindNotable(o) != null && !list.Contains(o)) list.Add(o); }
            foreach (var c in AllChildrenOf(id))
            {
                if (c.parentId != id) Add(c.parentId);
                int other = OtherParentOf(c);
                if (other != id) Add(other);
            }
            Add(me.partnerId);
            foreach (var o in State.notables) if (o.partnerId == id) Add(o.id);
            return list;
        }

        /// <summary>The founding people of each family: someone with no known parents who didn't marry into another family, with
        /// a partner or a child to draw. The biggest families first, at most <see cref="BookTreeCap"/>.</summary>
        public List<int> FamilyTreeRoots()
        {
            var claimed = new HashSet<int>();
            var trees = new List<(int root, int size)>();
            foreach (var n in State.notables.OrderBy(x => x.bornMonth).ThenBy(x => x.id))
            {
                if (claimed.Contains(n.id) || ParentsOf(n).Count > 0) continue;
                var mates = MatesOf(n.id);
                if (mates.Any(m => ParentsOf(FindNotable(m)).Count > 0)) continue; // married into a family with a tree
                var tree = FamilyTreeOf(n.id);
                if (tree == null) continue;
                foreach (var t in tree.nodes) claimed.Add(t.id);
                trees.Add((n.id, tree.people));
            }
            return trees.OrderByDescending(t => t.size).ThenBy(t => t.root).Take(BookTreeCap).Select(t => t.root).ToList();
        }

        /// <summary>The tree that shows this person (as one of the family or married in), or -1.</summary>
        public int FamilyTreeRootOf(int notableId)
        {
            foreach (int r in FamilyTreeRoots())
                if (FamilyTreeOf(r).Node(notableId) != null) return r;
            return -1;
        }

        /// <summary>
        /// The tree under a founding person (<paramref name="rootId"/> and their partners). Each of the family's own is placed once,
        /// under the first household that reaches them; a partner who is already in the tree is joined by no second face. Children
        /// sit oldest first; a household is centred over its children. <paramref name="folded"/>: households whose children fold to a
        /// chip ("11 more"); a generation with nobody left to draw drops out. Null if there is nobody to draw but the root.
        /// </summary>
        public FamilyTree FamilyTreeOf(int rootId, ICollection<int> folded = null)
        {
            var root = FindNotable(rootId);
            if (root == null) return null;
            var t = new FamilyTree { rootId = rootId, key = BookTreeKey(rootId) };
            var placed = new HashSet<int>();
            var kids = new Dictionary<int, List<Notable>>();
            var mates = new Dictionary<int, List<int>>();
            // the shape first: who is under whom (no positions yet); order holds the family's own, as walked
            var order = new List<int>();
            var gens = new Dictionary<int, int>();
            void Walk(Notable who, int gen)
            {
                placed.Add(who.id);
                order.Add(who.id);
                gens[who.id] = gen;
                var mine = new List<int>();
                foreach (int m in MatesOf(who.id))
                    if (placed.Add(m)) { mine.Add(m); gens[m] = gen; }
                mates[who.id] = mine;
                var house = new List<int> { who.id };
                house.AddRange(mine);
                var children = State.notables.Where(c => !placed.Contains(c.id) && ParentsOf(c).Any(house.Contains))
                    .OrderBy(c => c.bornMonth).ThenBy(c => c.id).ToList();
                foreach (var c in children) placed.Add(c.id);
                kids[who.id] = children;
                foreach (var c in children) { placed.Remove(c.id); Walk(c, gen + 1); }
            }
            Walk(root, 1);
            t.people = placed.Count;
            if (t.people <= 1) return null;

            // the family's name: what most of the founders' children carry
            var named = kids[rootId].Select(c => c.family).Where(f => !string.IsNullOrEmpty(f)).GroupBy(f => f)
                .OrderByDescending(g => g.Count()).ThenBy(g => g.Key == root.family ? 0 : 1).Select(g => g.Key).FirstOrDefault();
            t.familyName = named ?? root.family ?? "";

            int Below(int id) => kids[id].Sum(c => 1 + mates[c.id].Count + Below(c.id));
            bool Folded(int id) => folded != null && folded.Contains(id) && kids[id].Count > 0;
            float House(int id) => 1 + mates[id].Count;
            var widthOf = new Dictionary<int, float>();
            float Width(int id)
            {
                float under = Folded(id) ? 1f : kids[id].Sum(c => Width(c.id));
                return widthOf[id] = Math.Max(House(id), under);
            }
            Width(rootId);
            var visibleGens = new SortedSet<int>();
            void Place(int id, float left, int gen)
            {
                var n = FindNotable(id);
                float w = widthOf[id];
                float houseW = House(id);
                float start = left + (w - houseW) / 2f;
                Add(n, start + 0.5f, gen, true, -1);
                visibleGens.Add(gen);
                float x = start + 1.5f;
                foreach (int m in mates[id]) { Add(FindNotable(m), x, gen, false, id); t.couples.Add(new TreeCouple { a = id, b = m }); x += 1f; }
                if (kids[id].Count == 0) return;
                var b = new TreeBranch { ownerId = id, fromX = mates[id].Count > 0 ? start + 1f : start + 0.5f, folded = Folded(id) };
                t.branches.Add(b);
                if (b.folded)
                {
                    b.hidden = Below(id);
                    b.childX.Add(left + w / 2f);
                    visibleGens.Add(gen + 1);
                    return;
                }
                float under = kids[id].Sum(c => widthOf[c.id]);
                float cx = left + (w - under) / 2f;
                foreach (var c in kids[id])
                {
                    Place(c.id, cx, gen + 1);
                    b.childX.Add(t.Node(c.id).x);
                    cx += widthOf[c.id];
                }
            }
            void Add(Notable n, float x, int gen, bool blood, int mateOf) => t.nodes.Add(new TreeNode
            {
                id = n.id, name = n.name, fullName = n.FullName, alive = n.alive, bornYear = YearNo(n.bornMonth),
                diedYear = n.alive || n.diedMonth < 0 ? -1 : YearNo(n.diedMonth), gen = gen, x = x, blood = blood, mateOf = mateOf,
            });
            Place(rootId, 0f, 1);
            t.width = widthOf[rootId];

            // rows: the generations with someone (or a chip) to draw; the gutter says the year the generation began
            var rowOf = new Dictionary<int, int>();
            foreach (int g in visibleGens)
            {
                rowOf[g] = rowOf.Count;
                var born = order.Where(id => gens[id] == g).Select(id => FindNotable(id).bornMonth); // the family's own (partners join later)
                t.gens.Add(new TreeGen { gen = g, row = rowOf[g], year = YearNo(born.DefaultIfEmpty(0).Min()) });
            }
            foreach (var n in t.nodes) n.row = rowOf[n.gen];
            foreach (var b in t.branches) b.fromRow = t.Node(b.ownerId).row;
            t.rows = rowOf.Count;
            return t;
        }

        /// <summary>Debug (#774, the debug panel): a deep family to look at. From the biggest tree's founder (or the first founder), each
        /// generation weds if it has no partner and has three children, the youngest already gone (a stone in the tree); the eldest carries
        /// on. Draws on the main dice (names), as the other birth cheats do. Returns the founder's id, or -1.</summary>
        public int DebugDeepFamily(int generations = 3)
        {
            var s = State;
            var roots = FamilyTreeRoots();
            var p = roots.Count > 0 ? FindNotable(roots[0]) : s.notables.Where(n => n.parentId < 0).OrderBy(n => n.bornMonth).ThenBy(n => n.id).FirstOrDefault();
            if (p == null) return -1;
            int head = p.id;
            for (int g = 0; g < generations; g++)
            {
                if (MatesOf(p.id).Count == 0)
                {
                    var mate = AddNotable(Math.Max(AdultAge, p.age), -1, null);
                    p.partnerId = mate.id; mate.partnerId = p.id;
                    Chronicle("wedding", $"{p.FullName} and {mate.FullName} were wed.", 0.5f, p.id, mate.id);
                }
                Notable eldest = null;
                for (int k = 0; k < 3; k++)
                {
                    var child = AddNotable(k == 0 ? AdultAge + 4f : k == 1 ? 12f : 2f, p.id, p.family);
                    if (k == 2) { child.alive = false; child.diedMonth = s.month; }
                    eldest ??= child;
                }
                p = eldest;
            }
            return head;
        }
    }
}
