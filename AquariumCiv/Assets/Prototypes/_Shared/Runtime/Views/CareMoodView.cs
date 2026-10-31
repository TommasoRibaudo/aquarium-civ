using AquariumCiv.Prototypes.Sim;
using UnityEngine;

namespace AquariumCiv.Prototypes
{
    /// <summary>
    /// Gate 1's care layer, view side (see <see cref="CareConfig"/>): the crowd walks slower as mess grows, someone says a
    /// wistful or comic line now and then, and someone cheers when cleaning brings the sun back. Quiet for a while
    /// after a return, so the welcome comes first. Not used by P2–P6 (P2 keeps its own copy as tested); the vertical
    /// slice adds it with <c>care = CareConfig.Gate1</c>.
    /// </summary>
    public class CareMoodView : MonoBehaviour
    {
        public CareConfig care = CareConfig.Gate1;
        public CrowdView crowd;
        public MessView mess;
        public Vector2 linesEverySeconds = new(9f, 16f);
        [Tooltip("No lines this long after a return: the welcome comes first.")] public float quietAfterReturn = 12f;
        [Tooltip("At most one cheer this often.")] public float cheerEverySeconds = 20f;

        SimClock clock;
        float nextLine, lastCheer = -99f;

        void Awake()
        {
            // Before SimClock.Start runs the launch catch-up, so the care rules apply to the time away.
            clock = SimClock.Instance;
            care.ApplyTo(clock.config);
            clock.Returned += (_, _) => nextLine = Time.unscaledTime + quietAfterReturn;
        }

        void OnEnable() { if (mess != null) mess.Cleaned += OnCleaned; }
        void OnDisable() { if (mess != null) mess.Cleaned -= OnCleaned; }

        void Update()
        {
            if (!care.enabled || crowd == null) return;
            var sim = clock.Sim;
            crowd.grumpiness = Mathf.MoveTowards(crowd.grumpiness, care.Grumpiness(sim), Time.unscaledDeltaTime * 0.5f);
            if (!care.MoodShows(sim) || Time.unscaledTime < nextLine || crowd.Agents.Count == 0) return;
            nextLine = Time.unscaledTime + Random.Range(linesEverySeconds.x, linesEverySeconds.y);
            Say(CareConfig.MoodLines);
            EventLog.Instance?.Count("grumbles");
        }

        void OnCleaned()
        {
            if (!care.enabled || crowd == null || crowd.Agents.Count == 0) return;
            if (!care.Cheers(clock.Sim) || Time.unscaledTime - lastCheer < cheerEverySeconds) return;
            lastCheer = Time.unscaledTime;
            nextLine = Time.unscaledTime + linesEverySeconds.y;
            Say(CareConfig.CheerLines);
        }

        void Say(string[] lines)
        {
            var a = crowd.Agents[Random.Range(0, crowd.Agents.Count)];
            HudToast.Show(lines[Random.Range(0, lines.Length)], a.transform.position + Vector3.up * crowd.characterScale * 3f, 3f);
        }
    }
}
