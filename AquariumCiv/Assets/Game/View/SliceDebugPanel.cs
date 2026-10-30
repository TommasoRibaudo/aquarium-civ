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
        public bool open;
        string last = "";

        void OnGUI()
        {
            float s = Mathf.Max(1f, Screen.height / 1100f);
            GUI.matrix = Matrix4x4.Scale(new Vector3(s, s, 1f));
            if (!open)
            {
                if (GUI.Button(new Rect(8, 8, 56, 32), "dbg")) open = true;
                return;
            }
            var src = SimLayoutSource.Find();
            GUILayout.BeginArea(new Rect(8, 8, 300, 640), GUI.skin.box);
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
            if (GUILayout.Button("close")) open = false;
            GUILayout.EndArea();
        }
    }
}
