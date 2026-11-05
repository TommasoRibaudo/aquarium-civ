using System.Collections;
using System.IO;
using System.Reflection;
using System.Text;
using AquariumCiv.Game;
using AquariumCiv.Life;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AquariumCiv.View.PlayTests
{
    /// <summary>#327 phase 1: base frames for the people-talk mock-ups (PNGs + a numbers file into Temp/agent). Explicit.</summary>
    [Category("PeopleTalkShots"), Explicit]
    public class PeopleTalkShots : PlayTestBase
    {
        static BoxCamera Cam => Camera.main.GetComponent<BoxCamera>();
        readonly StringBuilder info = new();

        static object Get(object o, string f)
        {
            for (var t = o.GetType(); t != null; t = t.BaseType)
            {
                var fi = t.GetField(f, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly);
                if (fi != null) return fi.GetValue(o);
            }
            return null;
        }
        static void Call(object o, string m) => o.GetType().GetMethod(m, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)?.Invoke(o, null);

        static IEnumerator Png(string name)
        {
            yield return new WaitForEndOfFrame();
            var tex = ScreenCapture.CaptureScreenshotAsTexture();
            Directory.CreateDirectory(Shots);
            File.WriteAllBytes(Shots + name + ".png", tex.EncodeToPNG());
            Object.Destroy(tex);
        }
        static IEnumerator Wait(float s) { yield return new WaitForSecondsRealtime(s); }

        static void Look(Vector3 world, float zoom)
        {
            var rig = Cam;
            rig.CancelFlight();
            rig.target = world; rig.zoom = zoom;
            rig.Apply();
        }

        static bool Px(PeopleView pv, PersonRef who, out Vector2 head, out Vector2 feet, out float h)
        {
            head = feet = default; h = 0;
            if (!pv.TryGetDollTop(who, out var t) || !pv.TryGetDollFeet(who, out var f)) return false;
            var c = Camera.main;
            float k = 2556f / Screen.height;
            var a = c.WorldToScreenPoint(t); var b = c.WorldToScreenPoint(f);
            head = new Vector2(a.x * k, (Screen.height - a.y) * k); feet = new Vector2(b.x * k, (Screen.height - b.y) * k);
            h = (a - b).magnitude * k;
            return true;
        }

        void Rec(string tag, PeopleView pv, PersonRef who)
        {
            if (Px(pv, who, out var hd, out var ft, out var h))
                info.AppendLine($"{tag}: head=({hd.x:0},{hd.y:0}) feet=({ft.x:0},{ft.y:0}) doll={h:0.0}px zoom={Cam.zoom:0.00}");
            else info.AppendLine($"{tag}: person not placeable");
        }

        [UnityTest]
        public IEnumerator TakeShots()
        {
            UseLivingLand();
            yield return OpenSlice(1179, 2556, 1234);
            var pv = PeopleView.Find();
            Debug.Log("[talkshots] " + EcoDebug.SkipSeasons(18));
            yield return Wait(2f);
            PeopleView.DebugSetHour(10f);
            yield return Wait(2f);
            var rig = Cam;
            rig.CancelFlight();
            float homeZoom = rig.zoom; var homeTarget = rig.target;
            info.AppendLine($"home zoom={homeZoom:0.00} target={homeTarget}");
            PersonRef P = PersonRef.None; float best = 1e9f;
            foreach (var a in pv.Activities)
            {
                if (!a.who.IsNotable || !Px(pv, a.who, out _, out var ft, out _)) continue;
                if (ft.x < 200 || ft.x > 980 || ft.y < 700 || ft.y > 2100) continue;
                float score = Mathf.Abs(ft.x - 590f) + Mathf.Abs(ft.y - 1400f) * 0.5f - (a.moving ? 300f : 0f);
                if (score < best) { best = score; P = a.who; }
            }
            Assert.IsFalse(P.IsNone, "no notable mid-screen");
            var sim = GameClock.Instance.Sim;
            info.AppendLine("speaker " + sim.FindNotable(P.notableId)?.name);
            pv.timeScale = 0f;
            yield return Wait(0.5f);
            Rec("home", pv, P);
            pv.TryGetDollFeet(P, out var feetW);
            Look(feetW, 0.45f); yield return null; yield return null; Rec("z0.45", pv, P);
            Look(homeTarget, 1.25f); yield return null; yield return null; Rec("z1.25", pv, P);
            Look(homeTarget, 1f); yield return null; yield return null; Rec("z1.0", pv, P);
            var d = ShowMeDirector.Instance;
            rig.enabled = false;
            string line = "Mind the mud on the way, it's deep.";

            yield return Wait(0.5f);
            Look(homeTarget, 1f); yield return Png("clean_z1"); Rec("clean_z1", pv, P);
            info.AppendLine("0a: " + ShowMeDirector.DebugTreeLine());
            yield return Wait(1.5f); Look(homeTarget, 1f);
            yield return Png("today_0a");
            Call(d, "HideBubble"); Call(d, "HideCaption"); yield return Wait(1.2f);
            info.AppendLine("0b: " + ShowMeDirector.DebugSayOver(P, line));
            yield return Wait(1.5f); Rec("0b", pv, P);
            yield return Png("today_0b");
            Call(d, "HideBubble"); Call(d, "HideCaption"); yield return Wait(1.2f);

            Look(feetW, 0.45f); yield return Wait(0.3f); Look(feetW, 0.45f);
            yield return Png("clean_z045"); Rec("clean_z045", pv, P);
            info.AppendLine("A2: " + ShowMeDirector.DebugSayOver(P, line));
            yield return Wait(1.5f); Look(feetW, 0.45f); yield return null;
            yield return Png("A2_real");
            Call(d, "HideBubble"); Call(d, "HideCaption"); yield return Wait(1.2f);

            var right = Camera.main.transform.right; right.y = 0; right.Normalize();
            for (float s = 0.4f; s < 6f; s += 0.2f)
            {
                Look(homeTarget + right * s, 1f); yield return null;
                if (Px(pv, P, out _, out var ft2, out _) && ft2.x < -40f) break;
            }
            yield return Wait(0.3f); Rec("A3", pv, P);
            yield return Png("clean_A3_left");

            Look(homeTarget, 1f); yield return Wait(0.3f);
            foreach (AquariumCiv.View.ShowMe.WantCase w in System.Enum.GetValues(typeof(AquariumCiv.View.ShowMe.WantCase)))
            {
                string r = ShowMeDirector.DebugWant(w);
                info.AppendLine("want " + w + ": " + r);
                if (r.StartsWith("want") || r.StartsWith("caption") || r.StartsWith("bubble")) break;
            }
            yield return Wait(1.5f);
            var who = (PersonRef)Get(d, "bubbleWho");
            Rec("A4 speaker (" + (who.IsNone ? "none" : who.IsNotable ? "notable" : "crowd") + ")", pv, who);
            Look(homeTarget, 1f);
            yield return Png("A4_today");
            Call(d, "HideBubble"); Call(d, "HideCaption"); yield return Wait(1.2f);
            yield return Png("clean_A4");

            Look(homeTarget, 1.25f); yield return Wait(0.3f); Look(homeTarget, 1.25f);
            yield return Png("clean_z125"); Rec("clean_z125", pv, P);

            File.WriteAllText(Shots + "327_info.txt", info.ToString());
            Debug.Log("[talkshots]\n" + info);
            rig.enabled = true; pv.timeScale = 1f;
        }
    }
}
