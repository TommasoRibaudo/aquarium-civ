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
    /// <summary>The fixes-and-world bundle's looks at 1080x1920, into Temp/agent: not a check, the pictures a person judges. Explicit.</summary>
    [Category("BundleTwoShots"), Explicit]
    public class BundleTwoShots : PlayTestBase
    {
        const int W = 1080, H = 1920;
        static SimCore Sim => GameClock.Instance.ActiveSim;
        static PeopleView People => PeopleView.Find();
        static BoxCamera Cam => Camera.main.GetComponent<BoxCamera>();

        static IEnumerator Wait(float s) { yield return new WaitForSecondsRealtime(s); }

        IEnumerator Grown(float hour, float years = 14f)
        {
            yield return OpenSlice(W, H, 1234);
            SimLayoutSource.DebugSkipYears(years);
            yield return null;
            Sim.CleanAll();
            PeopleView.DebugSetHour(hour);
            yield return Wait(0.3f);
            yield return Until(() => GameUiPanel.Toasts.Current == null && GameUiPanel.Toasts.Waiting == 0, 6f);
            ShowMeDirector.BubblesOn = false;
        }

        void Look(Vector2 box, float zoom)
        {
            Cam.CancelFlight();
            var w = People.transform.TransformPoint(new Vector3(box.x, 0f, box.y));
            Cam.EaseTo(w, Cam.yaw, 52f, zoom, 0.3f);
        }

        /// <summary>Tester run 6 finding 5: arms and back, at default zoom (the camera stays home) and at 0.3.</summary>
        [UnityTest, Timeout(240000)]
        public IEnumerator CarriedBaby()
        {
            yield return Grown(10f);
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
                        string tag = a.carryOnBack ? "back" : "arms";
                        Cam.GoHome(0.1f);
                        yield return Wait(0.3f);
                        People.TryGetDollTop(a.who, out var top);
                        var s = Camera.main.WorldToScreenPoint(top);
                        Debug.Log($"[shots] baby-{tag} default at {s.x:0} {H - s.y:0}");
                        yield return Shot($"bundle2-baby-{tag}-default");
                        Look(p, 0.3f);
                        yield return Wait(0.7f);
                        yield return Shot($"bundle2-baby-{tag}-03");
                        Cam.GoHome(0.1f);
                        yield return Wait(0.3f);
                        if (a.carryOnBack) back = true; else arms = true;
                        break;
                    }
                }
                if (!(arms && back)) SimLayoutSource.DebugSkipYears(0.09f);
            }
            Debug.Log($"[shots] baby arms {arms} back {back}");
            AssertNoErrors("baby shots");
        }
    }
}
