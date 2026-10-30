using AquariumCiv.GameUI;
using UnityEngine;

namespace AquariumCiv.View
{
    /// <summary>
    /// TEMPORARY on-screen debug for the slice (until the debug panel arrives with the UI): skip years, cross the era
    /// with its rebuild wave, replay the wave, build a hut or a farm, flip day and night, so camp, village and town can
    /// be looked at in play mode. A small "dbg" button opens it (top left). Everything goes through the
    /// <see cref="SimLayoutSource"/> debug hooks, which also work from `script eval` in edit mode.
    /// </summary>
    [AddComponentMenu("AquariumCiv/TEMPORARY Slice Debug Panel")]
    public class SliceDebugPanel : MonoBehaviour
    {
        public bool open { get => DebugDock.Active == DebugDock.Dbg; set => DebugDock.Active = value ? DebugDock.Dbg : DebugDock.None; }
        string last = "";
        float contentH = 800f;
        Vector2 scroll;

        void OnGUI()
        {
            var safe = DebugDock.Begin(out float sc);
            if (!DebugDock.DrawBar(safe, sc)) return;
            if (DebugDock.Active != DebugDock.Dbg) { if (DebugDock.Active == DebugDock.None) DebugDock.SetPanel(default); return; }
            var src = SimLayoutSource.Find();
            var area = DebugDock.PanelArea(safe, 300f, contentH + 16f);
            DebugDock.SetPanel(area);
            GUILayout.BeginArea(area, GUI.skin.box);
            GUILayout.BeginVertical();
            scroll = GUILayout.BeginScrollView(scroll);
            GUILayout.Label(src != null ? src.Status() : "no SimLayoutSource");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("+1 yr")) last = SimLayoutSource.DebugSkipYears(1);
            if (GUILayout.Button("+10 yr")) last = SimLayoutSource.DebugSkipYears(10);
            if (GUILayout.Button("+40 yr")) last = SimLayoutSource.DebugSkipYears(40);
            GUILayout.EndHorizontal();
            if (GUILayout.Button("Next era (wave)")) last = SimLayoutSource.DebugNextEra(true);
            if (GUILayout.Button("Replay era wave")) last = SimLayoutSource.DebugReplayWave();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Build hut")) last = SimLayoutSource.DebugBuild("Hut");
            if (GUILayout.Button("Build farm")) last = SimLayoutSource.DebugBuild("Farm");
            GUILayout.EndHorizontal();
            if (GUILayout.Button(NightLamps.Level > 0.5f ? "Day" : "Night")) last = NightLamps.DebugSetLevel(NightLamps.Level > 0.5f ? 0f : 1f);
            if (GUILayout.Button("Send test feedback")) last = FeedbackSender.DebugSendTest();
            if (GameHud.Instance != null && GUILayout.Button(GameHud.Instance.IsVisible ? "Hide HUD" : "Show HUD"))
            {
                GameHud.Instance.SetVisible(!GameHud.Instance.IsVisible);
                last = GameHud.Instance.IsVisible ? "HUD shown" : "HUD hidden";
            }
            if (GameHud.Instance != null)
            {
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Camera mode")) { GameHud.Instance.SetCameraMode(true); last = "camera mode on (tap to leave)"; }
                if (GUILayout.Button("Auto-orbit: " + (BoxCamera.AutoOrbitInCameraMode ? "on" : "off")))
                    BoxCamera.AutoOrbitInCameraMode = !BoxCamera.AutoOrbitInCameraMode;
                GUILayout.EndHorizontal();
            }
            // The era moment (#88): era ready now, the wait's pile size, watch / replay / leave, hurry speed.
            GUILayout.Label(MomentPlayer.Status());
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Grow 12y")) last = MomentPlayer.DebugGrow(12f);
            if (GUILayout.Button("Village +40y")) last = MomentPlayer.DebugGrowVillage(40f);
            if (GUILayout.Button("Era ready")) last = MomentPlayer.DebugEraReady();
            if (GUILayout.Button("Waited 1y")) last = MomentPlayer.DebugWaited(12);
            if (GUILayout.Button("Waited 1d")) last = MomentPlayer.DebugWaited(288);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Festival")) last = MomentPlayer.DebugFestival();
            if (GUILayout.Button("End festival")) last = MomentPlayer.DebugEndFestival();
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Watch")) last = MomentPlayer.DebugWatch(false);
            if (GUILayout.Button("Replay last")) last = MomentPlayer.DebugReplay();
            if (GUILayout.Button("Leave")) last = MomentPlayer.DebugLeave();
            GUILayout.EndHorizontal();
            if (MomentPlayer.Instance != null)
            {
                GUILayout.BeginHorizontal();
                float hf = MomentPlayer.Instance.Director.HurryFactor;
                if (GUILayout.Button("Hurry ×" + (hf > 2f ? "4" : "2"))) last = MomentPlayer.DebugHurry(hf > 2f ? 2f : 4f);
                if (GUILayout.Button(MomentPlayer.DebugHoldHurry ? "Hold: on" : "Hold: off")) MomentPlayer.DebugHoldHurry = !MomentPlayer.DebugHoldHurry;
                GUILayout.EndHorizontal();
            }
            GUILayout.Label(MusicDirector.Instance != null ? MusicDirector.Instance.Status : "no MusicDirector");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Music now")) last = MusicDirector.DebugPlayNow();
            if (GUILayout.Button("Stop music")) last = MusicDirector.DebugStop();
            if (GUILayout.Button("Music: " + GameSettings.MusicFrequency))
            {
                GameSettings.MusicFrequency = (MusicFrequency)(((int)GameSettings.MusicFrequency + 1) % 3);
                last = "music frequency " + GameSettings.MusicFrequency;
            }
            GUILayout.EndHorizontal();
            // The people's view clock (#30): jump to a time of day, speed the day up, list what people are doing.
            var people = PeopleView.Find();
            if (people != null)
            {
                GUILayout.Label(people.Status());
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("8h")) last = PeopleView.DebugSetHour(8f);
                if (GUILayout.Button("12h")) last = PeopleView.DebugSetHour(12.2f);
                if (GUILayout.Button("18h")) last = PeopleView.DebugSetHour(18f);
                if (GUILayout.Button("22h")) last = PeopleView.DebugSetHour(22f);
                if (GUILayout.Button("+1h")) last = PeopleView.DebugSkipHours(1f);
                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal();
                if (GUILayout.Button(people.timeScale > 1f ? "Day ×1" : "Day ×10")) last = PeopleView.DebugSpeed(people.timeScale > 1f ? 1f : 10f);
                if (GUILayout.Button("Who's doing what")) last = PeopleView.DebugReport(6);
                GUILayout.EndHorizontal();
            }
            GUILayout.Label(last);
            if (src != null && !string.IsNullOrEmpty(src.lastWaveInfo)) GUILayout.Label(src.lastWaveInfo);
            GUILayout.EndScrollView();
            GUILayout.EndVertical();
            if (Event.current.type == EventType.Repaint) contentH = GUILayoutUtility.GetLastRect().height;
            GUILayout.EndArea();
        }
    }
}
