using System.Collections;
using System.Linq;
using AquariumCiv.Game;
using AquariumCiv.GameUI;
using AquariumCiv.Life;
using AquariumCiv.Sim;
using AquariumCiv.View.ShowMe;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AquariumCiv.View.PlayTests
{
    /// <summary>The people bundle's looks at both phone sizes, into Temp/agent: not a check, the pictures a person judges. Explicit.</summary>
    [Category("PeopleShots"), Explicit]
    public class PeopleBundleShots : PlayTestBase
    {
        static SimCore Sim => GameClock.Instance.ActiveSim;
        static PeopleView People => PeopleView.Find();
        static BoxCamera Cam => Camera.main.GetComponent<BoxCamera>();

        static IEnumerator Wait(float s) { yield return new WaitForSecondsRealtime(s); }

        IEnumerator Grown(int w, int h, float hour, float years = 14f)
        {
            yield return OpenSlice(w, h, 1234);
            SimLayoutSource.DebugSkipYears(years);
            yield return null;
            Sim.CleanAll();
            PeopleView.DebugSetHour(hour);
            yield return Wait(0.3f);
            yield return Until(() => GameUiPanel.Toasts.Current == null && GameUiPanel.Toasts.Waiting == 0, 6f);
        }

        void Look(Vector2 box, float zoom)
        {
            Cam.CancelFlight();
            var w = People.transform.TransformPoint(new Vector3(box.x, 0f, box.y));
            Cam.EaseTo(w, Cam.yaw, 52f, zoom, 0.3f);
        }

        Vector2 GroupMiddle(PlaceKind kind)
        {
            var pv = People;
            var ps = pv.Activities.Where(a => a.place.kind == kind).Select(a => pv.TryGetDollPosition(a.who, out var p) ? p : Vector2.zero).ToList();
            return ps.Count == 0 ? Vector2.zero : ps.Aggregate(Vector2.zero, (s, p) => s + p) / ps.Count;
        }

        IEnumerator Glass(int w, int h, string tag, int count)
        {
            yield return Grown(w, h, 10f, count > 6 ? 90f : 14f);
            StringAssert.Contains("at the glass", GlassVoice.DebugGather(count));
            yield return Until(() => People.Activities.Count(a => a.place.kind == PlaceKind.Glass && !a.moving) >= count, 24f);
            yield return Wait(3f);
            Debug.Log($"[shots] glass{count} at {w}: {People.Activities.Count(a => a.place.kind == PlaceKind.Glass)} people");
            Look(GroupMiddle(PlaceKind.Glass), 0.5f);
            yield return Wait(0.6f);
            yield return Shot($"bundle-{tag}-glass{count}");
        }

        IEnumerator Rest(int w, int h, string tag)
        {
            // A baby on someone, in the arms and on the back; no bubble in the way.
            yield return Grown(w, h, 10f);
            ShowMeDirector.BubblesOn = false;
            StringAssert.StartsWith("born", PeopleView.DebugBaby());
            var baby = Sim.State.notables.OrderByDescending(x => x.id).First();
            int key = PersonRef.Notable(baby.id).Key;
            bool arms = false, back = false;
            for (int day = 0; day < 24 && !(arms && back); day++)
            {
                for (float hour = 8f; hour < 17.5f && !(arms && back); hour += 0.75f)
                {
                    PeopleView.DebugSetHour(hour);
                    yield return Wait(0.25f);
                    foreach (var a in People.Activities.ToList())
                    {
                        if (a.carrying.Key != key || (a.carryOnBack ? back : arms) || !People.TryGetDollPosition(a.who, out var p)) continue;
                        Debug.Log($"[shots] carried: {People.Planner.Describe(a)}");
                        Look(p, 0.3f);
                        yield return Wait(0.7f);
                        People.TryGetDollTop(a.who, out var top);
                        var s = Camera.main.WorldToScreenPoint(top);
                        Debug.Log($"[shots] baby-{(a.carryOnBack ? "back" : "arms")} at {s.x:0} {s.y:0} of {Screen.height}");
                        yield return Shot($"bundle-{tag}-baby-{(a.carryOnBack ? "back" : "arms")}");
                        if (a.carryOnBack) back = true; else arms = true;
                        break;
                    }
                }
                if (!(arms && back)) SimLayoutSource.DebugSkipYears(0.09f);
            }
            Cam.GoHome(0.2f);
            yield return Wait(0.6f);

            // A herd at the default view, its chip gone.
            Sim.DebugShape(SimCore.NudgeKind.Herd, 1.2f, -0.2f);
            yield return Wait(22f);
            Cam.GoHome(0.2f);
            yield return Wait(0.6f);
            yield return Shot($"bundle-{tag}-herd");
            ShowMeDirector.BubblesOn = true;
        }

        [UnityTest, Timeout(300000)] public IEnumerator A_Tall() => Rest(1179, 2556, "tall");
        [UnityTest, Timeout(300000)] public IEnumerator B_Short() => Rest(750, 1334, "short");
        [UnityTest, Timeout(120000)] public IEnumerator C_Glass4Tall() => Glass(1179, 2556, "tall", 4);
        [UnityTest, Timeout(120000)] public IEnumerator D_Glass10Tall() => Glass(1179, 2556, "tall", 10);
        [UnityTest, Timeout(120000)] public IEnumerator E_Glass4Short() => Glass(750, 1334, "short", 4);
        [UnityTest, Timeout(120000)] public IEnumerator F_Glass10Short() => Glass(750, 1334, "short", 10);
    }
}
