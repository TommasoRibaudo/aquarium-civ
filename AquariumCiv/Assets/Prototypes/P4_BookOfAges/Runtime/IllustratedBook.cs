using System;
using System.Collections.Generic;
using System.Linq;
using AquariumCiv.Prototypes.Sim;
using UnityEngine;
using UnityEngine.InputSystem;

namespace AquariumCiv.Prototypes.P4
{
    /// <summary>
    /// Variant B: an illustrated Book of Ages. A cover, then one chapter per 25 years (its biggest stories, with
    /// portraits), a Legends gallery of every notable who ever lived, and family trees. Swipe or tap the arrows to turn
    /// pages. Paper and ink, so it reads as a keepsake rather than a menu.
    /// </summary>
    public class IllustratedBook : MonoBehaviour, IHistoryView
    {
        public int yearsPerChapter = 25;
        public int storiesPerChapter = 7;
        public int legendsPerPage = 6;

        public bool IsOpen { get; private set; }
        public event Action<string> Snapshot;

        static readonly string[] Roman = { "I", "II", "III", "IV", "V", "VI", "VII", "VIII", "IX", "X", "XI", "XII" };
        static readonly Color Paper = new(0.96f, 0.92f, 0.82f, 1f), PaperEdge = new(0.55f, 0.42f, 0.28f, 1f);
        static readonly Color Ink = new(0.22f, 0.15f, 0.09f), InkSoft = new(0.42f, 0.32f, 0.22f);

        readonly List<(string kind, Action<Rect> draw)> pages = new();
        int page, pageTurns;
        float openedAt, pageAt;
        float swipeStartX;
        bool swiping;
        GUIStyle paper, title, heading, body, meta, center;

        SimCore Sim => SimClock.Instance.Sim;
        SimState S => Sim.State;

        public void Open()
        {
            IsOpen = true;
            openedAt = pageAt = Time.unscaledTime;
            pageTurns = 0;
            page = 0;
            BuildPages();
        }

        public void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;
            EventLog.Instance?.Log("history", ("action", "close"), ("view", "book"), ("dwell_s", Time.unscaledTime - openedAt),
                ("page_turns", pageTurns), ("pages", pages.Count), ("last_page", pages.Count > 0 ? pages[page].kind : "-"));
            EventLog.Instance?.Count("history_s", Time.unscaledTime - openedAt);
            EventLog.Instance?.Count("page_turns", pageTurns);
        }

        void Turn(int dir, string how)
        {
            int to = Mathf.Clamp(page + dir, 0, pages.Count - 1);
            if (to == page) return;
            EventLog.Instance?.Log("history", ("action", "page"), ("from", pages[page].kind), ("to", pages[to].kind), ("from_page", page + 1), ("to_page", to + 1), ("dwell_s", Time.unscaledTime - pageAt), ("how", how));
            page = to;
            pageAt = Time.unscaledTime;
            pageTurns++;
            Juice.Whoosh();
        }

        void Update()
        {
            if (!IsOpen) return;
            var p = Pointer.current;
            if (p == null) return;
            float x = p.position.ReadValue().x / ProtoGui.Scale;
            if (p.press.wasPressedThisFrame) { swiping = true; swipeStartX = x; }
            if (swiping && p.press.wasReleasedThisFrame)
            {
                swiping = false;
                float dx = x - swipeStartX;
                if (Mathf.Abs(dx) > 90f) Turn(dx < 0 ? 1 : -1, "swipe");
            }
        }

        // ---------- pages ----------

        void BuildPages()
        {
            pages.Clear();
            pages.Add(("cover", Cover));
            int years = (int)(S.month / SimConfig.MonthsPerYear);
            for (int start = 0, ch = 0; start <= years; start += yearsPerChapter, ch++)
            {
                int from = start, to = Mathf.Min(years, start + yearsPerChapter - 1), n = ch;
                if (years - to < yearsPerChapter / 2) to = years; // a short tail joins the last chapter (no "years 100–100")
                pages.Add(("chapter", r => Chapter(r, n, from, to)));
                if (to == years) break;
            }
            var legends = S.notables.OrderBy(n => n.bornMonth).ThenBy(n => n.id).ToList();
            for (int i = 0; i < legends.Count; i += legendsPerPage)
            {
                var slice = legends.Skip(i).Take(legendsPerPage).ToList();
                int first = i;
                pages.Add(("legends", r => Legends(r, slice, first, legends.Count)));
            }
            var roots = S.notables.Where(n => n.parentId < 0).OrderBy(n => n.bornMonth).ThenBy(n => n.id).ToList();
            var withKids = roots.Where(n => Sim.ChildrenOf(n.id).Any()).ToList();
            for (int i = 0; i < withKids.Count; i += 2)
            {
                var pair = withKids.Skip(i).Take(2).ToList();
                pages.Add(("family", r => Families(r, pair)));
            }
        }

        void Cover(Rect r)
        {
            string place = string.IsNullOrEmpty(S.settlementName) ? "the Glass" : S.settlementName;
            GUI.Label(new Rect(r.x, r.y + 40, r.width, 50), "The Book of Ages", center);
            GUI.Label(new Rect(r.x, r.y + 96, r.width, 40), $"of {place}", new GUIStyle(center) { fontSize = 30, fontStyle = FontStyle.Italic });
            var founders = S.notables.Where(n => n.bornMonth == 0).Take(5).ToList();
            float size = 104, gap = 14, total = founders.Count * size + (founders.Count - 1) * gap;
            for (int i = 0; i < founders.Count; i++)
                PortraitStudio.Draw(new Rect(r.center.x - total / 2 + i * (size + gap), r.y + 170, size, size), founders[i], !founders[i].alive);
            GUI.Label(new Rect(r.x, r.y + 290, r.width, 30), "The five who settled the glass", new GUIStyle(meta) { alignment = TextAnchor.MiddleCenter });
            int ever = S.notables.Count, years = (int)(S.month / SimConfig.MonthsPerYear);
            GUI.Label(new Rect(r.x + 30, r.y + 360, r.width - 60, 200),
                $"{years} years in the glass.\n{S.population} people live here today, in {S.buildings.Count} buildings.\n{ever} names are written in this book.\n\n<i>Swipe or use the arrows to turn the page.</i>",
                new GUIStyle(body) { alignment = TextAnchor.UpperCenter });
        }

        void Chapter(Rect r, int n, int from, int to)
        {
            var stories = S.chronicle.Where(e => e.month / SimConfig.MonthsPerYear >= from && e.month / SimConfig.MonthsPerYear <= to && e.kind != "grumble")
                .OrderByDescending(e => e.salience).Take(storiesPerChapter).OrderBy(e => e.month).ToList();
            var startSnap = S.years.FirstOrDefault(s => s.year >= from);
            var endSnap = S.years.LastOrDefault(s => s.year <= to);
            GUI.Label(new Rect(r.x, r.y, r.width, 30), $"Chapter {Roman[Mathf.Min(n, Roman.Length - 1)]}  ·  years {from}–{to}", meta);
            GUI.Label(new Rect(r.x, r.y + 30, r.width, 44), ChapterName(n, stories), heading);
            if (startSnap != null && endSnap != null)
                GUI.Label(new Rect(r.x, r.y + 76, r.width, 28), $"From {startSnap.population} to {endSnap.population} people, {startSnap.buildings} to {endSnap.buildings} buildings.", meta);
            float y = r.y + 118;
            foreach (var e in stories)
            {
                var who = Sim.FindNotable(e.notableId);
                float tx = r.x;
                float hgt = Mathf.Max(who != null ? 92 : 0, body.CalcHeight(new GUIContent(e.text), r.width - (who != null ? 104 : 0)) + 30);
                if (y + hgt > r.yMax) break;
                if (who != null) { PortraitStudio.Draw(new Rect(r.x, y, 88, 88), who, !who.alive); tx = r.x + 104; }
                GUI.Label(new Rect(tx, y, r.xMax - tx, 26), $"Year {e.month / SimConfig.MonthsPerYear}", meta);
                GUI.Label(new Rect(tx, y + 24, r.xMax - tx, hgt - 24), e.text, body);
                y += hgt + 14;
            }
            if (stories.Count == 0) GUI.Label(new Rect(r.x, y, r.width, 60), "Quiet years. The people worked, and the glass kept them.", body);
        }

        static string ChapterName(int n, List<ChronicleEntry> stories)
        {
            if (n == 0) return "The Founding";
            var top = stories.OrderByDescending(e => e.salience).FirstOrDefault();
            return top?.kind switch
            {
                "wedding" => "Years of Weddings", "death" => "The Passing of Elders", "naming" => "A Name for Home",
                "first_building" => "The Builders", "festival" => "Feasts and Dancing", "storm" => "The Stormy Years",
                "milestone" => "The Growing Years", "anniversary" => "An Old Village Now", "invention" => "The Clever Years",
                "rivalry" => "Old Grudges", "lean_winter" => "The Hungry Winters", _ => "Ordinary Days",
            };
        }

        void Legends(Rect r, List<Notable> slice, int first, int total)
        {
            GUI.Label(new Rect(r.x, r.y, r.width, 30), $"Legends  ·  {first + 1}–{first + slice.Count} of {total}", meta);
            GUI.Label(new Rect(r.x, r.y + 30, r.width, 44), "The People of the Glass", heading);
            float cw = (r.width - 20) / 2, ch = (r.height - 100) / 3;
            for (int i = 0; i < slice.Count; i++)
            {
                var n = slice[i];
                var cell = new Rect(r.x + (i % 2) * (cw + 20), r.y + 90 + (i / 2) * ch, cw, ch - 12);
                float ps = Mathf.Min(110, cell.height - 70);
                PortraitStudio.Draw(new Rect(cell.center.x - ps / 2, cell.y, ps, ps), n, !n.alive);
                GUI.Label(new Rect(cell.x, cell.y + ps + 4, cell.width, 28), n.FullName, new GUIStyle(body) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold });
                var centered = new GUIStyle(meta) { alignment = TextAnchor.MiddleCenter };
                GUI.Label(new Rect(cell.x, cell.y + ps + 30, cell.width, 24), n.trait, centered);
                GUI.Label(new Rect(cell.x, cell.y + ps + 54, cell.width, 24), Life(n), centered);
            }
        }

        void Families(Rect r, List<Notable> roots)
        {
            GUI.Label(new Rect(r.x, r.y, r.width, 30), "Family trees", meta);
            float y = r.y + 34;
            foreach (var root in roots)
            {
                GUI.Label(new Rect(r.x, y, r.width, 44), $"The {root.family} line", heading);
                y += 50;
                y = Branch(r, root, 0, y);
                y += 18;
            }
        }

        float Branch(Rect r, Notable n, int depth, float y)
        {
            if (y > r.yMax - 60) return y;
            float x = r.x + depth * 44;
            if (depth > 0) // a branch: down from the parent, then across
            {
                var c = GUI.color;
                GUI.color = InkSoft;
                GUI.DrawTexture(new Rect(x - 30, y - 8, 2, 38), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(x - 30, y + 28, 24, 2), Texture2D.whiteTexture);
                GUI.color = c;
            }
            PortraitStudio.Draw(new Rect(x, y, 56, 56), n, !n.alive);
            var partner = n.partnerId >= 0 ? Sim.FindNotable(n.partnerId) : null;
            string life = Life(n);
            GUI.Label(new Rect(x + 66, y + 2, r.xMax - x - 66, 28), $"<b>{n.FullName}</b>{(partner != null ? $"  &  {partner.name}" : "")}", body);
            GUI.Label(new Rect(x + 66, y + 30, r.xMax - x - 66, 24), $"{n.trait}, {life}", meta);
            y += 64;
            foreach (var kid in Sim.ChildrenOf(n.id).OrderBy(k => k.bornMonth))
                y = Branch(r, kid, Mathf.Min(depth + 1, 4), y);
            return y;
        }

        /// <summary>One way of saying a life everywhere in the book, in the same ages the chronicle uses
        /// (notables age at half speed, so "died at 62" and the age here always agree).</summary>
        string Life(Notable n)
        {
            int age = Mathf.FloorToInt(n.age);
            string came = n.parentId < 0 ? $"came in year {n.bornMonth / SimConfig.MonthsPerYear}" : $"born year {n.bornMonth / SimConfig.MonthsPerYear}";
            return n.alive ? $"{came}, now {age}" : $"died year {n.diedMonth / SimConfig.MonthsPerYear}, aged {age}";
        }

        string flash;
        float flashUntil;

        /// <summary>A short message drawn on the page itself (banners sit under the book).</summary>
        public void Flash(string text, float seconds = 2.5f) { flash = text; flashUntil = Time.unscaledTime + seconds; }

        // ---------- frame ----------

        void Styles()
        {
            if (paper != null) return;
            paper = new GUIStyle { normal = { background = ProtoGui.RoundedTexture(Paper, PaperEdge, 18) }, border = new RectOffset(20, 20, 20, 20) };
            body = new GUIStyle(ProtoGui.Wrap) { fontSize = 22, normal = { textColor = Ink }, richText = true };
            meta = new GUIStyle(ProtoGui.Hint) { fontSize = 19, normal = { textColor = InkSoft }, richText = true };
            title = new GUIStyle(ProtoGui.Title) { fontSize = 40, normal = { textColor = Ink } };
            heading = new GUIStyle(ProtoGui.Title) { fontSize = 30, normal = { textColor = Ink } };
            center = new GUIStyle(title) { alignment = TextAnchor.MiddleCenter };
        }

        void OnGUI()
        {
            if (!IsOpen || !SurveyPanel.IsPlaying) return;
            GUI.depth = -5;
            ProtoGui.Begin();
            Styles();
            float w = ProtoGui.Width, h = ProtoGui.Height;
            float safeTop = (Screen.height - Screen.safeArea.yMax) / ProtoGui.Scale;
            float top = Mathf.Max(20, safeTop + 8) + LabStrip.Reserved;
            var outer = new Rect(14, top, w - 28, h - ProtoGui.SafeBottom - 112 - 12 - top);
            ProtoGui.Block(outer);
            GUI.Box(outer, GUIContent.none, paper);
            var inner = new Rect(outer.x + 30, outer.y + 26, outer.width - 60, outer.height - 26 - 112);
            if (pages.Count > 0) pages[page].draw(inner);
            if (flash != null && Time.unscaledTime < flashUntil)
            {
                var fr = new Rect(outer.x + 40, outer.yMax - 104 - 70, outer.width - 80, 56);
                GUI.Box(fr, flash, new GUIStyle(ProtoGui.Box) { alignment = TextAnchor.MiddleCenter });
            }

            var row = new Rect(outer.x + 18, outer.yMax - 104, outer.width - 36, 88);
            GUILayout.BeginArea(row);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("‹", GUILayout.Height(88), GUILayout.Width(96))) Turn(-1, "button");
            GUILayout.Label($"{page + 1} / {pages.Count}", new GUIStyle(meta) { alignment = TextAnchor.MiddleCenter }, GUILayout.Height(88), GUILayout.Width(96));
            if (GUILayout.Button("›", GUILayout.Height(88), GUILayout.Width(96))) Turn(1, "button");
            if (GUILayout.Button("Snapshot", GUILayout.Height(88))) Snapshot?.Invoke("book");
            if (GUILayout.Button("Close", ProtoGui.Primary, GUILayout.Height(88))) Close();
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }
    }
}
