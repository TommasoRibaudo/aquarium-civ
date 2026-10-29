using System.Collections;
using System.Collections.Generic;
using System.Linq;
using AquariumCiv.Prototypes.Sim;
using UnityEngine;

namespace AquariumCiv.Prototypes.P6
{
    /// <summary>
    /// P6 Eras & Shelf (prototype-plan.md §5): is an era transition witnessed live worth waiting for, and which growth
    /// form motivates more? Eras are plateaus: when the people are ready for the next one, the world waits until you
    /// watch. Camp → Hamlet is the same in both variants; at Village the box is full and it grows. One variable:
    ///   A "the box grows": an annex box rises and joins the tank, and the village spreads into it.
    ///   B "new box on the shelf": the camera pulls back to a shelf; a new box starts beside the old one, which stays
    ///     alive as a museum (its banner carried over as a relic). Tap a box to visit it.
    /// Lab only, at ×60 (a real minute is a game year).
    /// </summary>
    public class ErasAndShelf : PrototypeBase, IDebugCheats
    {
        [System.Serializable]
        public class EraLook
        {
            public string name;
            public DioramaView.BuildingPrefabs[] prefabs; // overrides by building type for this era
        }

        public DioramaView village;
        public EraLook[] eras;                    // 0 Camp, 1 Hamlet, 2 Village
        public int[] eraPopulation = { 0, 10, 16 };
        public float labSpeed = 60f;
        [Header("Second box (annex in A, new box in B)")]
        public Transform box2;
        public DioramaView village2;
        public CrowdView crowd2;
        public Vector3[] box2LocalSlots;
        public float boxWidth = 4f;
        public float shelfGap = 1.2f;
        public Transform shelf;
        public GameObject relicPrefab;
        public int newBoxSeed = 4321;

        int era, readyFor = -1;
        bool watching, grown;
        float watchStart, readyAt, focusSince;
        string focus = "old";
        Vector3[] baseSlots;
        DioramaView.BuildingPrefabs[] basePrefabs;
        int baseBuildingSlots, baseSeed;
        SimCore museum;
        GameObject relic;
        GUIStyle bigStyle;

        bool Annex => Cfg.Variant == "A";
        string Place => string.IsNullOrEmpty(clock.Sim.State.settlementName) ? "the camp" : clock.Sim.State.settlementName;
        static string Cap(string t) => string.IsNullOrEmpty(t) ? t : char.ToUpperInvariant(t[0]) + t.Substring(1);
        string MuseumPlace => museum == null || string.IsNullOrEmpty(museum.State.settlementName) ? "the old box" : museum.State.settlementName;

        protected override void Awake()
        {
            baseSlots = village.slotPositions.ToArray();
            basePrefabs = village.prefabs;
            baseBuildingSlots = SimClock.Instance.config.buildingSlots;
            baseSeed = SimClock.Instance.seed;
            base.Awake();
        }

        protected override void ApplyConfig(SimConfig cfg)
        {
            base.ApplyConfig(cfg);
            cfg.storyEvents = true;
        }

        protected override void Update()
        {
            base.Update();
            if (!started) return;
            clock.speed = watching || readyFor >= 0 ? 0f : labSpeed; // eras are plateaus: the world waits for you
            if (hud != null) hud.hidden = watching || readyFor >= 0;
            if (!watching && readyFor < 0 && era + 1 < eras.Length && clock.Sim.State.population >= eraPopulation[era + 1]) Ready(era + 1, "live");
        }

        protected override void FreshWorld()
        {
            // Back to one box, Camp era, the original seed: the same start line for both variants.
            StopAllCoroutines();
            watching = grown = false;
            readyFor = -1;
            era = 0;
            museum = null;
            clock.seed = baseSeed;
            clock.config.buildingSlots = baseBuildingSlots;
            village.source = null;
            crowd.source = null;
            if (notables != null) notables.source = null;
            village.slotPositions = baseSlots.ToArray();
            box2.gameObject.SetActive(false);
            if (shelf != null) shelf.gameObject.SetActive(false);
            if (relic != null) Destroy(relic);
            OrbitCamera.Instance?.SetWide(1f, null);
            base.FreshWorld();
            ApplyEraLook(0, false);
        }

        protected override void OnVariantStart(bool switched)
        {
            string how = Annex ? "the box itself will grow" : "a new box will join your shelf";
            HudToast.ShowBanner($"Lab: a young camp. Time runs fast (a minute is a year). When the people are ready for a new era, it waits for you to watch. Later, {how}.", 10f);
        }

        protected override void OnReturn(double hours, float years, long sinceMonth)
        {
            HudToast.ShowBanner($"Welcome back, Keeper! {StatsLine(hours, years)}", 6f);
            // Crossed a threshold while away: it waited.
            if (readyFor < 0 && era + 1 < eras.Length && clock.Sim.State.population >= eraPopulation[era + 1]) Ready(era + 1, "return");
        }

        void Ready(int next, string how)
        {
            readyFor = next;
            readyAt = Time.unscaledTime;
            EventLog.Instance?.Log("era", ("action", "ready"), ("era", eras[next].name), ("how", how), ("year", clock.Sim.State.Year));
            HudToast.ShowBanner(how == "return"
                ? $"{Cap(Place)} waited for you: the people are ready to become a {eras[next].name.ToLowerInvariant()}!"
                : $"Something is stirring in {Place}... the people are ready to become a {eras[next].name.ToLowerInvariant()}!", 8f);
            Juice.Chime();
        }

        // ---------- the live moment ----------

        void Watch()
        {
            if (readyFor < 0 || watching) return;
            EventLog.Instance?.Log("era", ("action", "watch"), ("era", eras[readyFor].name), ("waited_s", Time.unscaledTime - readyAt));
            StartCoroutine(Transition(readyFor));
        }

        IEnumerator Transition(int next)
        {
            watching = true;
            watchStart = Time.unscaledTime;
            var cam = OrbitCamera.Instance;
            if (!grown) cam.ResetView();
            // 1. The people gather in the middle.
            HudToast.ShowBanner("The people gather in the square...", 4f);
            var mid = village.SlotPosition(0);
            var all = crowd.Agents.Concat(notables != null ? notables.All : Enumerable.Empty<Wanderer>()).ToList();
            foreach (var a in all) a.gatherAt = mid;
            yield return new WaitForSecondsRealtime(4f);
            // 2. The village changes in kind: buildings rebuilt in the new style, one by one.
            Juice.Whoosh();
            Juice.Glints(mid + Vector3.up * 0.4f, 40, 1.2f);
            ApplyEraLook(next, true);
            yield return new WaitForSecondsRealtime(2.5f);
            // 3. The cheer.
            era = next;
            readyFor = -1;
            foreach (var a in all) { a.gatherAt = null; a.Cheer(3f); }
            foreach (var a in all.Take(16)) Juice.Confetti(a.transform.position + Vector3.up * 0.3f, 10);
            Juice.Chime();
            clock.Sim.Chronicle("era", $"{Cap(Place)} became a {eras[next].name.ToLowerInvariant()}. Everyone remembers where they stood that day.", 0.95f);
            HudToast.ShowBanner($"{Cap(Place)} is now a {eras[next].name.ToLowerInvariant()}!", 5f);
            yield return new WaitForSecondsRealtime(4f);
            // 4. At Village the box is full: it grows.
            if (next == eras.Length - 1)
            {
                float grow = Time.unscaledTime;
                EventLog.Instance?.Log("growth", ("action", "start"), ("form", Annex ? "annex" : "shelf"));
                yield return Annex ? GrowAnnex() : NewBoxOnShelf();
                EventLog.Instance?.Log("growth", ("action", "end"), ("form", Annex ? "annex" : "shelf"), ("seconds", Time.unscaledTime - grow));
            }
            EventLog.Instance?.Log("era", ("action", "done"), ("era", eras[next].name), ("watch_s", Time.unscaledTime - watchStart));
            EventLog.Instance?.Count("transition_s", Time.unscaledTime - watchStart);
            watching = false;
        }

        DioramaView.BuildingPrefabs[] EraPrefabs(int e)
        {
            var map = basePrefabs.ToDictionary(p => p.type, p => p.prefabs);
            foreach (var o in eras[e].prefabs) map[o.type] = o.prefabs;
            return map.Select(kv => new DioramaView.BuildingPrefabs { type = kv.Key, prefabs = kv.Value }).ToArray();
        }

        void ApplyEraLook(int e, bool animate)
        {
            village.prefabs = EraPrefabs(e);
            village.Respawn(animate);
        }

        IEnumerator GrowAnnex()
        {
            HudToast.ShowBanner($"{Cap(Place)} has filled the glass... and the glass grows!", 6f);
            var origin = village.transform.parent.position;
            box2.position = origin + Vector3.right * boxWidth - Vector3.up * 3f;
            box2.gameObject.SetActive(true);
            village2.gameObject.SetActive(false); // the annex is part of the one village: its slots join the main view
            crowd2.gameObject.SetActive(false);
            OrbitCamera.Instance.SetWide(1.55f, origin + Vector3.right * boxWidth * 0.5f + Vector3.up * 0.3f);
            Juice.Whoosh();
            yield return Rise(box2, origin + Vector3.right * boxWidth, 3f);
            Juice.Chime();
            var annexSlots = box2LocalSlots.Select(p => box2.TransformPoint(p)).ToArray();
            village.slotPositions = baseSlots.Concat(annexSlots).ToArray();
            clock.config.buildingSlots = baseBuildingSlots + annexSlots.Length;
            clock.Sim.Chronicle("annex", $"The glass grew: an annex opened to the east, and the first families of {Place} crossed into it.", 0.9f);
            crowd.CheerAll(3f);
            HudToast.ShowBanner("An annex joined the tank. Watch the people spread into it.", 7f);
            grown = true;
            yield return new WaitForSecondsRealtime(2f);
        }

        IEnumerator NewBoxOnShelf()
        {
            HudToast.ShowBanner($"{Cap(Place)} has filled the glass... a new box arrives on your shelf!", 6f);
            var origin = village.transform.parent.position;
            // The old world becomes a museum: a frozen copy that its own views keep showing (people still wander).
            museum = new SimCore(clock.config, JsonUtility.FromJson<SimState>(JsonUtility.ToJson(clock.Sim.State)));
            village.source = museum;
            crowd.source = museum;
            if (notables != null) notables.source = museum;
            if (shelf != null) shelf.gameObject.SetActive(true);
            // The new box: a fresh camp, five settlers who carried the old banner across. Reset first, so the new box
            // never shows the old village, and place its slots where the box is now (they ride up with it).
            string oldName = MuseumPlace;
            clock.seed = newBoxSeed;
            clock.config.buildingSlots = box2LocalSlots.Length;
            clock.ResetWorld();
            var to = origin + Vector3.right * (boxWidth + shelfGap);
            box2.position = to - Vector3.up * 3f;
            village2.slotPositions = box2LocalSlots.Select(p => box2.TransformPoint(p)).ToArray();
            village2.prefabs = EraPrefabs(0); // a fresh camp: tents again
            box2.gameObject.SetActive(true);
            village2.gameObject.SetActive(true);
            crowd2.gameObject.SetActive(true);
            OrbitCamera.Instance.SetWide(1.9f, origin + Vector3.right * (boxWidth + shelfGap) * 0.5f + Vector3.up * 0.3f);
            Juice.Whoosh();
            yield return Rise(box2, to, 3f);
            village2.slotPositions = box2LocalSlots.Select(p => box2.TransformPoint(p)).ToArray(); // risen: new buildings land here
            clock.Sim.Chronicle("relic", $"Five settlers from {oldName} carried its old banner into the new box and planted it by their camp.", 0.9f);
            if (relicPrefab != null)
            {
                relic = Instantiate(relicPrefab, village2.slotPositions[0] + new Vector3(0.35f, 0, -0.35f), Quaternion.identity, box2);
                relic.transform.localScale = Vector3.one * 1.6f;
                LogCategory.Tag(relic, "relic");
            }
            Juice.Chime();
            HudToast.ShowBanner($"A new box! {oldName} lives on next door as a museum. Tap \"Old box\" or \"New box\" to visit.", 8f);
            grown = true;
            focus = "both";
            focusSince = Time.unscaledTime;
            yield return new WaitForSecondsRealtime(2f);
        }

        static IEnumerator Rise(Transform t, Vector3 to, float seconds)
        {
            var from = t.position;
            for (float k = 0; k < 1f; k += Time.unscaledDeltaTime / seconds)
            {
                float e = 1f - Mathf.Pow(1f - k, 3);
                t.position = Vector3.LerpUnclamped(from, to, e);
                yield return null;
            }
            t.position = to;
        }

        void Focus(string which)
        {
            if (which == focus) return;
            EventLog.Instance?.Log("shelf", ("action", "focus"), ("from", focus), ("to", which), ("dwell_s", Time.unscaledTime - focusSince));
            if (focus == "old") EventLog.Instance?.Count("old_box_s", Time.unscaledTime - focusSince);
            focus = which;
            focusSince = Time.unscaledTime;
            var origin = village.transform.parent.position;
            var cam = OrbitCamera.Instance;
            if (which == "old") cam.SetWide(1f, origin + Vector3.up * 0.3f);
            else if (which == "new") cam.SetWide(1f, box2.position + Vector3.up * 0.3f);
            else cam.SetWide(1.9f, origin + Vector3.right * (boxWidth + shelfGap) * 0.5f + Vector3.up * 0.3f);
        }

        // ---------- UI ----------

        void OnGUI()
        {
            if (!started || watching || !SurveyPanel.IsPlaying || FeedbackPanel.IsOpen || NotableCardPanel.IsOpen) return;
            ProtoGui.Begin();
            float w = ProtoGui.Width, h = ProtoGui.Height;
            bigStyle ??= new GUIStyle(ProtoGui.Primary) { fontSize = 30 };
            if (readyFor >= 0)
            {
                var r = new Rect(60, h - ProtoGui.SafeBottom - 112 - 24 - 120, w - 120, 120);
                ProtoGui.Block(r);
                if (GUI.Button(r, $"Watch it become a {eras[readyFor].name.ToLowerInvariant()}", bigStyle)) Watch();
                return;
            }
            if (grown && !Annex)
            {
                var row = new Rect(20, h - ProtoGui.SafeBottom - 112 - 12 - 100, w - 40, 100);
                ProtoGui.Block(row);
                ProtoGui.Panel(row);
                GUILayout.BeginArea(new Rect(row.x + 10, row.y + 6, row.width - 20, 88));
                GUILayout.BeginHorizontal();
                if (ProtoGui.ChipButton("Old box", focus == "old", GUILayout.Height(88))) Focus("old");
                if (ProtoGui.ChipButton("Shelf", focus == "both", GUILayout.Height(88))) Focus("both");
                if (ProtoGui.ChipButton("New box", focus == "new", GUILayout.Height(88))) Focus("new");
                GUILayout.EndHorizontal();
                GUILayout.EndArea();
            }
        }

        public void DrawCheats()
        {
            var s = clock.Sim.State;
            GUILayout.Label($"<b>P6</b>  era {eras[era].name}{(readyFor >= 0 ? $", ready for {eras[readyFor].name}" : "")}{(grown ? ", grown" : "")}");
            GUILayout.BeginHorizontal();
            if (era + 1 < eras.Length && GUILayout.Button($"Threshold: {eras[era + 1].name}", GUILayout.Height(54)))
            {
                s.population = Mathf.Max(s.population, eraPopulation[era + 1]);
                Ready(era + 1, "debug");
            }
            if (readyFor >= 0 && GUILayout.Button("Watch now", GUILayout.Height(54))) Watch();
            if (!grown && GUILayout.Button("Skip to Village + grow", GUILayout.Height(54)))
            {
                ApplyEraLook(1, false);
                era = 1;
                s.population = Mathf.Max(s.population, eraPopulation[2]);
                Ready(2, "debug");
                Watch();
            }
            GUILayout.EndHorizontal();
            DrawTimeCheats();
        }
    }
}
