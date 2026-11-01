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

        /// <summary>#185: the doll carried to its post, and on it, at default zoom and close. #154: the stalls from above. Wish puffs, by day and asleep.</summary>
        [UnityTest, Timeout(240000)]
        public IEnumerator DollAndPuffs()
        {
            yield return Grown(10f, 20f);
            MomentPlayer.DebugGrowVillage(4f);
            yield return Frames(3);
            StringAssert.Contains("people until", GlassVoice.DebugPlayMoment(RelicId.KeeperDoll));
            PegDollView carrier = null;
            for (float t = 0f; t < 45f && carrier == null; t += 0.25f)
            {
                yield return Wait(0.25f);
                carrier = Object.FindObjectsByType<PegDollView>(FindObjectsSortMode.None).FirstOrDefault(d => d.propKind == PropKind.Doll);
            }
            Debug.Log($"[shots] doll carried: {carrier != null}");
            if (carrier != null)
            {
                var c = carrier.transform.parent.InverseTransformPoint(carrier.transform.position);
                Look(new Vector2(c.x, c.z), 0.3f);
                yield return Wait(0.7f);
                var again = carrier.transform.parent.InverseTransformPoint(carrier.transform.position);
                Look(new Vector2(again.x, again.z), 0.3f);
                yield return Wait(0.4f);
                yield return Shot("bundle2-185-carried");
            }
            var props = GlassVoice.Instance.GetComponent<RelicProps>();
            yield return Until(() => props.Made.ContainsKey(RelicId.KeeperDoll), 30f);
            Assert.IsTrue(props.Made.ContainsKey(RelicId.KeeperDoll), "the post");
            var post = props.Made[RelicId.KeeperDoll].localPosition;
            Cam.CancelFlight();
            Cam.GoHome(0.1f);
            yield return Wait(0.4f);
            Debug.Log($"[shots] post at default {Camera.main.WorldToScreenPoint(props.Made[RelicId.KeeperDoll].position)}");
            yield return Shot("bundle2-185-post-default");
            Look(new Vector2(post.x, post.z), 0.3f);
            yield return Wait(0.6f);
            yield return Shot("bundle2-185-post-close");
            Cam.GoHome(0.1f);
            yield return Wait(0.4f);

            var mp = MomentPlayer.Instance;
            mp.GiftPuffs(0.4f, 0.2f, 1.4f, 8);
            yield return Wait(0.5f);
            yield return Shot("bundle2-191-puffs-day");
            PeopleView.DebugSetHour(23f);
            NightLamps.DebugSetLevel(1f);
            yield return Wait(0.5f);
            WishPayoffView.DebugGlow();
            yield return Wait(1.6f);
            yield return Shot("bundle2-191-glow-night");
            AssertNoErrors("doll and puffs");
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
