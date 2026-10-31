using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace AquariumCiv.Prototypes.Hub
{
    /// <summary>
    /// The Test Hub (first scene in the build): one card per prototype in test order, each with its hypothesis in plain
    /// words and an A (teal) and B (sand) button that say what each version does. Ticks show what was played this
    /// session. A diary in progress (P2's by default) opens straight away on launch, so the diary flow is unchanged;
    /// prototypes come back here through their lab strip, the thank-you screen or the debug panel.
    /// </summary>
    public class TestHub : MonoBehaviour
    {
        const string DiaryKey = "hub_diary", AutoOpenKey = "hub_autoopen";
        static readonly string[] LabOrder = { "P3", "P4", "P5", "P6" };

        Vector2 scroll;
        float contentHeight = 1600f, pressY, lastY;
        bool dragging, pressed;
        int dragEndFrame = -1;
        string confirmDiary;
        float confirmAt;
        GUIStyle cardTitle, small, gate, pill;

        /// <summary>The prototype whose diary runs on this device (null = none). P2's diary counts if its save exists.</summary>
        static string DiaryProto
        {
            get
            {
                var d = PlayerPrefs.GetString(DiaryKey, "");
                if (d.Length > 0) return d;
                return HubLink.HasSave("P2") ? "P2" : null;
            }
        }

        static bool AutoOpen => PlayerPrefs.GetInt(AutoOpenKey, 1) == 1;

        static bool InBuild(string proto)
        {
            var e = TestCatalog.Find(proto);
            return e != null && Application.CanStreamedLevelBeLoaded(e.scene);
        }

        void Start()
        {
            EventLog.Instance?.Log("hub", ("action", "show"), ("diary", DiaryProto ?? "-"));
            if (HubLink.LaunchHandled) return;
            HubLink.LaunchHandled = true;
            var diary = DiaryProto;
            if (diary != null && AutoOpen && InBuild(diary)) OpenDiary(diary, "launch");
        }

        static void OpenDiary(string proto, string why)
        {
            EventLog.Instance?.Log("hub", ("action", "diary"), ("proto", proto), ("why", why));
            PlayerPrefs.SetString(DiaryKey, proto);
            PlayerPrefs.Save();
            if (proto == "P2") SceneManager.LoadScene(TestCatalog.Find("P2").scene); // exactly as P2 runs today
            else LabLaunch.Open(proto, -1, TestMode.Diary);
        }

        static void OpenLab(string proto, int variant)
        {
            EventLog.Instance?.Log("hub", ("action", "lab"), ("proto", proto), ("variant", TestCatalog.Letter(variant)));
            LabLaunch.Open(proto, variant, TestMode.Lab);
        }

        // Touch-drag scrolling (IMGUI scroll views only scroll with bars or the wheel).
        void Update()
        {
            var p = Pointer.current;
            if (p == null) return;
            float y = p.position.ReadValue().y / ProtoGui.Scale;
            // A drag keeps buttons off through its release frame (so lifting the finger presses nothing), then ends.
            if (dragging && !p.press.isPressed && dragEndFrame >= 0 && Time.frameCount > dragEndFrame) { dragging = false; dragEndFrame = -1; }
            if (p.press.wasReleasedThisFrame && dragging) dragEndFrame = Time.frameCount;
            if (p.press.wasPressedThisFrame) { pressed = true; dragging = false; pressY = lastY = y; }
            else if (pressed && p.press.isPressed)
            {
                if (!dragging && Mathf.Abs(y - pressY) > 12f) dragging = true;
                if (dragging) scroll.y = Mathf.Clamp(scroll.y + (y - lastY), 0, Mathf.Max(0, contentHeight - ProtoGui.Height + 40));
                lastY = y;
            }
            else pressed = false;
            var m = Mouse.current;
            if (m != null) scroll.y = Mathf.Clamp(scroll.y - m.scroll.ReadValue().y, 0, Mathf.Max(0, contentHeight - ProtoGui.Height + 40));
        }

        void OnGUI()
        {
            ProtoGui.Begin();
            cardTitle ??= new GUIStyle(ProtoGui.Title) { fontSize = 28 };
            small ??= new GUIStyle(ProtoGui.Hint) { fontSize = 19 };
            gate ??= new GUIStyle(ProtoGui.Hint) { fontSize = 20, fontStyle = FontStyle.Bold };
            pill ??= new GUIStyle(ProtoGui.Hint) { fontSize = 18, alignment = TextAnchor.MiddleLeft, normal = { textColor = new Color(0.65f, 1f, 0.85f) } };

            float w = ProtoGui.Width, h = ProtoGui.Height;
            float safeTop = (Screen.height - Screen.safeArea.yMax) / ProtoGui.Scale;
            var view = new Rect(0, safeTop, w, h - safeTop - ProtoGui.SafeBottom);
            bool wasDragging = dragging;
            if (wasDragging) GUI.enabled = false; // a drag never presses a button
            scroll = GUI.BeginScrollView(view, scroll, new Rect(0, 0, w - 1, contentHeight), false, false, GUIStyle.none, GUIStyle.none);
            float y = 20, x = 20, cw = w - 40;

            GUI.Label(new Rect(x, y, cw, 44), "Aquarium Civ · Test Hub", ProtoGui.Title); y += 46;
            GUI.Label(new Rect(x, y, cw, 56), $"Pick what to test. Each test has two versions: <color={ProtoGui.VariantHex(0)}><b>A</b></color> and <color={ProtoGui.VariantHex(1)}><b>B</b></color>. Play one, answer the survey, then play the other.", ProtoGui.Wrap);
            y += 74;

            y = DiaryCard(x, y, cw) + 18;
            string lastGate = null;
            foreach (var id in LabOrder)
            {
                if (!InBuild(id)) continue;
                var e = TestCatalog.Find(id);
                if (e.gate != lastGate)
                {
                    lastGate = e.gate;
                    GUI.Label(new Rect(x + 4, y, cw, 30), e.gate == "Gate 2" ? "GATE 2 · HISTORY" : "GATE 3 · CONTROL AND PROGRESSION", gate);
                    y += 34;
                }
                y = LabCard(e, x, y, cw) + 16;
            }
            GUI.Label(new Rect(x, y, cw, 30), $"Build {Application.version}  ·  tester {(Tester.Nickname.Length > 0 ? Tester.Nickname : "(no nickname yet)")}", small);
            y += 40;
            contentHeight = y;
            GUI.EndScrollView();
            GUI.enabled = true;
        }

        /// <summary>Diaries with a saved world on this phone, in catalog order (P2 first). Every one stays reachable.</summary>
        static List<TestCatalog.Entry> Diaries() =>
            TestCatalog.All.Where(e => e.diaryDays > 0 && InBuild(e.id) && HubLink.HasSave(e.id)).ToList();

        float DiaryCard(float x, float y, float cw)
        {
            var current = DiaryProto;
            var all = Diaries();
            var e = current != null ? TestCatalog.Find(current) : null;
            if (e != null && !all.Contains(e)) all.Insert(0, e);
            float hgt = all.Count == 0 ? 120 : 196 + all.Count * 98;
            var r = new Rect(x, y, cw, hgt);
            ProtoGui.Panel(r);
            float ix = r.x + 20, iw = r.width - 40, iy = r.y + 16;
            if (all.Count == 0)
            {
                GUI.Label(new Rect(ix, iy, iw, 36), "No diary running", cardTitle);
                GUI.Label(new Rect(ix, iy + 40, iw, 56), "A diary runs on real time over a few days. Start one from a test below when the guide says so.", small);
                return r.yMax;
            }
            GUI.Label(new Rect(ix, iy, iw, 36), all.Count == 1 ? "Your diary" : "Your diaries", cardTitle);
            float by = iy + 46;
            foreach (var d in all)
            {
                bool isCurrent = d == e;
                if (GUI.Button(new Rect(ix, by, iw, 88), $"{d.id} · {d.title} diary (continue)", isCurrent ? ProtoGui.Primary : GUI.skin.button)) OpenDiary(d.id, "button");
                by += 98;
            }
            GUI.Label(new Rect(ix, by, iw, 30), $"Real time, a few days each. {(e?.diaryNote ?? "The version changes by day.")}", small);
            bool auto = AutoOpen;
            var toggle = new Rect(ix, by + 36, iw, 72);
            if (GUI.Button(toggle, auto ? $"On launch: open the {e?.id ?? "diary"} diary" : "On launch: show this hub", ProtoGui.Chip))
            {
                PlayerPrefs.SetInt(AutoOpenKey, auto ? 0 : 1);
                PlayerPrefs.Save();
            }
            return r.yMax;
        }

        float LabCard(TestCatalog.Entry e, float x, float y, float cw)
        {
            float hgt = e.diaryDays > 0 ? 372 : 288;
            var r = new Rect(x, y, cw, hgt);
            ProtoGui.Panel(r);
            float ix = r.x + 20, iw = r.width - 40, iy = r.y + 16;
            GUI.Label(new Rect(ix, iy, iw, 36), $"{e.id} · {e.title}", cardTitle);
            GUI.Label(new Rect(ix, iy + 38, iw, 56), e.hypothesis, ProtoGui.Wrap);
            float bw = (iw - 12) / 2, by = iy + 100;
            for (int v = 0; v < 2; v++)
            {
                var br = new Rect(ix + v * (bw + 12), by, bw, 88);
                if (GUI.Button(br, $"{TestCatalog.Letter(v)} · {e.names[v]}", ProtoGui.VariantButton(v))) OpenLab(e.id, v);
                GUI.Label(new Rect(br.x + 4, by + 92, bw - 8, 52), e.lines[v], small);
                if (LabSession.WasPlayed(e.id, v))
                {
                    bool surveyed = LabSession.WasSurveyed(e.id, TestCatalog.Letter(v));
                    ProtoGui.Dot(new Rect(br.x + 6, by + 152, 14, 14), ProtoGui.VariantColor(v));
                    GUI.Label(new Rect(br.x + 26, by + 146, 260, 26), surveyed ? "played, survey sent" : "played, no survey yet", pill);
                }
            }
            if (e.diaryDays > 0)
            {
                bool has = HubLink.HasSave(e.id);
                bool confirming = confirmDiary == e.id && Time.unscaledTime - confirmAt < 5f;
                string other = DiaryProto;
                string label = has ? $"Continue the {e.id} diary" : confirming ? $"Tap again to start it ({other} stays in Your diaries)" : $"Start the {e.diaryDays}-day diary";
                if (GUI.Button(new Rect(ix, by + 180, iw, 72), label, confirming ? ProtoGui.Primary : GUI.skin.button))
                {
                    // Starting a new diary while another runs takes a second tap; the old one stays listed above.
                    if (has || confirming || other == null || other == e.id) OpenDiary(e.id, has ? "button" : "start");
                    else { confirmDiary = e.id; confirmAt = Time.unscaledTime; }
                }
            }
            return r.yMax;
        }
    }
}
