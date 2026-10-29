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

        float DiaryCard(float x, float y, float cw)
        {
            var diary = DiaryProto;
            var e = diary != null ? TestCatalog.Find(diary) : null;
            float hgt = e != null ? 236 : 120;
            var r = new Rect(x, y, cw, hgt);
            ProtoGui.Panel(r);
            float ix = r.x + 20, iw = r.width - 40, iy = r.y + 16;
            if (e == null)
            {
                GUI.Label(new Rect(ix, iy, iw, 36), "No diary running", cardTitle);
                GUI.Label(new Rect(ix, iy + 40, iw, 56), "A diary runs on real time over a few days. Start one from a test below when the guide says so.", small);
                return r.yMax;
            }
            GUI.Label(new Rect(ix, iy, iw, 36), $"Your diary: {e.id} · {e.title}", cardTitle);
            GUI.Label(new Rect(ix, iy + 40, iw, 30), $"Real time, {e.diaryDays} days. {(e.diaryNote ?? "The version changes by day.")}", small);
            if (GUI.Button(new Rect(ix, iy + 80, iw * 0.62f, 88), $"{e.id} diary (continue)", ProtoGui.Primary)) OpenDiary(e.id, "button");
            bool auto = AutoOpen;
            if (GUI.Button(new Rect(ix + iw * 0.62f + 10, iy + 80, iw * 0.38f - 10, 88), auto ? "Open on launch: on" : "Open on launch: off", auto ? ProtoGui.ChipOn : ProtoGui.Chip))
            {
                PlayerPrefs.SetInt(AutoOpenKey, auto ? 0 : 1);
                PlayerPrefs.Save();
            }
            GUI.Label(new Rect(ix, iy + 176, iw, 30), auto ? "The app opens straight into this diary. Come back here from the thank-you screen." : "The app opens on this hub.", small);
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
                    ProtoGui.Dot(new Rect(br.x + 6, by + 152, 14, 14), ProtoGui.VariantColor(v));
                    GUI.Label(new Rect(br.x + 26, by + 146, 160, 26), "played", pill);
                }
            }
            if (e.diaryDays > 0 && GUI.Button(new Rect(ix, by + 180, iw, 72), $"Start the {e.diaryDays}-day diary"))
                OpenDiary(e.id, "start");
            return r.yMax;
        }
    }
}
