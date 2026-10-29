using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace AquariumCiv.Prototypes
{
    /// <summary>
    /// Visible crowd: up to maxVisible KayKit characters wander between building slots.
    /// The first agents stand in for living notables (tappable, named); the rest are anonymous.
    /// </summary>
    public class CrowdView : MonoBehaviour
    {
        public DioramaView diorama;
        public GameObject[] characterPrefabs;
        public AnimationClip walkClip, idleClip, cheerClip;
        public float characterScale = 0.3f;
        [Tooltip("World units per second.")] public float walkSpeed = 0.35f;
        public int maxVisible = 30;
        [Tooltip("0 = content, 1 = grumpy: citizens trudge slower and stand around longer (P2 variant B).")]
        [Range(0, 1)] public float grumpiness;

        readonly List<Wanderer> agents = new();
        public IReadOnlyList<Wanderer> Agents => agents;

        void Update()
        {
            var state = SimClock.Instance.Sim.State;
            int want = Mathf.Min(state.population, maxVisible);
            while (agents.Count < want) agents.Add(SpawnAgent(agents.Count));
            while (agents.Count > want) { Destroy(agents[^1].gameObject); agents.RemoveAt(agents.Count - 1); }

            var notables = state.notables.Where(n => n.alive && n.age >= 6f).ToList();
            for (int i = 0; i < agents.Count; i++)
            {
                var n = i < notables.Count ? notables[i] : null;
                agents[i].SetNotable(n?.id ?? -1, n?.FullName);
            }
        }

        /// <summary>Everyone stops and cheers (e.g. after the Keeper cleans the glass).</summary>
        public void CheerAll(float seconds = 1.7f)
        {
            foreach (var a in agents) a.Cheer(seconds);
        }

        Wanderer SpawnAgent(int index)
        {
            var prefab = characterPrefabs[index % characterPrefabs.Length];
            var start = RandomPoint();
            var go = Instantiate(prefab, start, Quaternion.Euler(0, Random.Range(0, 360f), 0), transform);
            go.transform.localScale = Vector3.one * characterScale;
            go.name = $"Citizen_{index}";
            var col = go.AddComponent<CapsuleCollider>();
            col.center = new Vector3(0, 1.2f, 0);
            col.height = 2.6f;
            col.radius = 0.7f;
            var w = go.AddComponent<Wanderer>();
            w.Init(this);
            return w;
        }

        public Vector3 RandomPoint()
        {
            var slot = diorama.slotPositions[Random.Range(0, diorama.slotPositions.Length)];
            var off = Random.insideUnitCircle * diorama.slotRadius;
            return slot + new Vector3(off.x, 0, off.y);
        }

        internal PlayableGraph BuildGraph(Animator animator, out AnimationMixerPlayable mixer, out AnimationClipPlayable[] clips)
        {
            var graph = PlayableGraph.Create($"Crowd_{animator.name}");
            graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
            var output = AnimationPlayableOutput.Create(graph, "anim", animator);
            mixer = AnimationMixerPlayable.Create(graph, 3);
            clips = new[] { walkClip, idleClip, cheerClip }.Select(c => AnimationClipPlayable.Create(graph, c)).ToArray();
            for (int i = 0; i < clips.Length; i++)
            {
                clips[i].SetTime(Random.Range(0f, 1f)); // desync the crowd
                graph.Connect(clips[i], 0, mixer, i);
            }
            output.SetSourcePlayable(mixer);
            graph.Play();
            return graph;
        }
    }

    public class Wanderer : MonoBehaviour, ITappable
    {
        public int NotableId { get; private set; } = -1;
        public string DisplayName { get; private set; }

        CrowdView crowd;
        PlayableGraph graph;
        AnimationMixerPlayable mixer;
        AnimationClipPlayable[] clips;
        Vector3 target;
        float idleUntil, cheerUntil;
        LogCategory logTag;

        public void Init(CrowdView owner)
        {
            crowd = owner;
            if (!TryGetComponent<Animator>(out var animator)) animator = gameObject.AddComponent<Animator>();
            animator.applyRootMotion = false;
            graph = crowd.BuildGraph(animator, out mixer, out clips);
            logTag = LogCategory.Tag(gameObject, "citizen");
            target = crowd.RandomPoint();
        }

        public void SetNotable(int id, string fullName)
        {
            if (id == NotableId) return;
            NotableId = id;
            DisplayName = fullName;
            logTag.category = id >= 0 ? "notable" : "citizen";
            logTag.id = id >= 0 ? id.ToString() : null;
        }

        public void Cheer(float seconds)
        {
            if (Time.time >= cheerUntil) clips[2].SetTime(0);
            cheerUntil = Time.time + seconds * Random.Range(0.8f, 1.2f);
            if (Camera.main != null)
            {
                var look = Camera.main.transform.position - transform.position;
                look.y = 0;
                if (look.sqrMagnitude > 0.001f) transform.rotation = Quaternion.LookRotation(look);
            }
        }

        public void OnTap(RaycastHit hit)
        {
            if (NotableId >= 0) EventLog.Instance?.Log("notable", ("action", "tap"), ("id", NotableId));
            HudToast.Show(NotableId >= 0 ? DisplayName : "A villager", transform.position + Vector3.up * 0.6f);
        }

        void Update()
        {
            int state; // 0 walk, 1 idle, 2 cheer
            if (Time.time < cheerUntil) state = 2;
            else if (Time.time < idleUntil) state = 1;
            else
            {
                state = 0;
                var to = target - transform.position;
                to.y = 0;
                float pace = 1f - 0.45f * crowd.grumpiness;
                float step = crowd.walkSpeed * pace * Time.deltaTime;
                clips[0].SetSpeed(pace); // feet match the slower walk
                if (to.magnitude <= step)
                {
                    transform.position = new Vector3(target.x, transform.position.y, target.z);
                    idleUntil = Time.time + Random.Range(1f, 4f) * (1f + 2f * crowd.grumpiness);
                    target = crowd.RandomPoint();
                }
                else
                {
                    transform.position += to.normalized * step;
                    transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(to), 8f * Time.deltaTime);
                }
            }
            for (int i = 0; i < 3; i++)
            {
                double len = clips[i].GetAnimationClip().length;
                if (len > 0 && clips[i].GetTime() > len) clips[i].SetTime(clips[i].GetTime() % len); // loop regardless of import settings
                float w = mixer.GetInputWeight(i);
                mixer.SetInputWeight(i, Mathf.MoveTowards(w, i == state ? 1f : 0f, 6f * Time.deltaTime));
            }
        }

        void OnDestroy()
        {
            if (graph.IsValid()) graph.Destroy();
        }
    }
}
