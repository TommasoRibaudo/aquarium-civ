using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using AquariumCiv.Prototypes.Sim;
using UnityEngine;
using NudgeKind = AquariumCiv.Prototypes.Sim.SimCore.NudgeKind;

namespace AquariumCiv.Prototypes.P5
{
    /// <summary>
    /// P5 Keeper's Nudge (prototype-plan.md §5): do indirect nudges make players feel they shaped the history, without
    /// micromanagement or helplessness? One variable, the form of the nudge:
    ///   A "omens": place a harvest totem, a craft totem or a faith shrine; the people lean that way.
    ///   B "shape the land": rain on a spot, plant a grove, dig a channel from the pond; the people adapt.
    /// Both: three nudges, each usable again after 3 game years; the people answer within 1–3 game years with a
    /// building beside it, and the chronicle credits the Keeper. Lab runs at ×60 (1 real minute = 1 game year).
    /// </summary>
    public class KeepersNudge : PrototypeBase, IDebugCheats
    {
        [Serializable]
        public class Option
        {
            public string kind, title, line;
            public GameObject prop;
            public float scale = 1f;
            [Tooltip("Takes a free tile (grove, channel) instead of standing on the rim of one.")] public bool takesTile;
        }

        public DioramaView village;
        public Option[] omens, land;
        public GameObject cloudPrefab;
        public GameObject riverTile;
        [Tooltip("World height of the tile tops above the tile pivots (DioramaBuilder surfaceY).")] public float tileTop;
        public float labSpeed = 60f;
        public float gatherSeconds = 20f;

        Option[] Options => Cfg.Variant == "A" ? omens : land;
        bool trayOpen;
        Option placing;
        Func<Vector2, Collider, bool> savedClaim;
        readonly Dictionary<int, GameObject> props = new();
        int generation = -1, checkinNudges;
        GUIStyle titleStyle, lineStyle;

        protected override void Awake()
        {
            base.Awake();
            Hook();
        }

        /// <summary>Tell the sim where buildings may go: beside nudges, never on a grove or channel tile.</summary>
        void Hook()
        {
            clock.Sim.SlotNear = NearestFreeSlot;
            clock.Sim.ReservedSlots = () => (clock.Sim.State.nudges ?? new List<Nudge>()).Where(n => Find(n.kind)?.takesTile == true).Select(n => n.slot);
        }

        protected override void ApplyConfig(SimConfig cfg)
        {
            base.ApplyConfig(cfg);
            cfg.storyEvents = true;
        }

        protected override void Update()
        {
            base.Update();
            if (clock.Sim.SlotNear == null) Hook();
            clock.speed = Lab ? labSpeed : 1f;
            if (hud != null) hud.hidden = trayOpen || placing != null || NotableCardPanel.IsOpen;
            SyncProps();
            WatchAnswers();
        }

        protected override void FreshWorld()
        {
            base.FreshWorld();
            Hook();
        }

        protected override void OnVariantStart(bool switched)
        {
            CancelPlacing();
            trayOpen = false;
            checkinNudges = 0;
            string what = Cfg.Variant == "A" ? "place a totem or a shrine" : "send rain, plant a grove or dig a channel";
            if (Lab) HudToast.ShowBanner($"Lab: time runs fast here (a minute is a year). Tap \"Nudge\" to {what}, then watch what the people do.", 9f);
            else if (!PlayerPrefs.HasKey("p5_hinted")) { PlayerPrefs.SetInt("p5_hinted", 1); wipe.ShowHint(); }
        }

        protected override void OnReturn(double hours, float years, long sinceMonth)
        {
            CancelPlacing();
            WatchAnswers(quiet: true); // answers that came while away are logged here and told in the welcome line
            var credited = clock.Sim.State.chronicle.Where(e => e.month > sinceMonth && e.keeper).OrderBy(e => e.month).ToList();
            string line = $"Welcome back, Keeper! {StatsLine(hours, years)}";
            if (credited.Count > 0) line += $"\n{credited[^1].text}";
            HudToast.ShowBanner(line, 7f);
            EventLog.Instance?.Log("checkin", ("nudges_last", checkinNudges), ("credited_while_away", credited.Count));
            checkinNudges = 0;
        }

        // ---------- placing ----------

        void Choose(Option o)
        {
            trayOpen = false;
            placing = o;
            savedClaim = OrbitCamera.TapClaim;
            OrbitCamera.TapClaim = PlaceTap;
            HudToast.ShowBanner(o.takesTile ? $"{o.title}: tap an empty spot in the village." : $"{o.title}: tap a spot in the village.", 6f);
            EventLog.Instance?.Log("nudge", ("action", "choose"), ("kind", o.kind));
        }

        void CancelPlacing()
        {
            if (placing == null) return;
            placing = null;
            OrbitCamera.TapClaim = savedClaim;
        }

        bool PlaceTap(Vector2 screen, Collider hit)
        {
            if (placing == null) return false;
            var ray = OrbitCamera.Instance.Cam.ScreenPointToRay(screen);
            if (hit == null || !Physics.Raycast(ray, out var info, 500f))
            {
                HudToast.ShowBanner("Tap inside the village to place it.", 3f);
                return true;
            }
            var o = placing;
            var s = clock.Sim.State;
            int nearest = NearestSlot(info.point);
            var off = village.SlotPosition(nearest) - info.point; off.y = 0;
            if (off.magnitude > village.slotRadius * 2.2f)
            {
                HudToast.ShowBanner("Tap inside the village to place it.", 3f);
                return true;
            }
            var used = s.buildings.Select(b => b.slot).ToHashSet();
            int slot = o.takesTile ? NearestFreeSlot(NearestSlot(info.point), used) : NearestSlot(info.point);
            if (slot < 0) { HudToast.ShowBanner("No empty spot left for that.", 3f); return true; }
            var n = clock.Sim.PlaceNudge(o.kind, slot);
            n.placedUnixS = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            checkinNudges++;
            EventLog.Instance?.Log("nudge", ("action", "place"), ("kind", o.kind), ("slot", slot), ("year", s.Year));
            EventLog.Instance?.Count("nudges");
            CancelPlacing();
            Juice.Chime();
            StartCoroutine(Gather(slot));
            var label = village.SlotPosition(slot) + Vector3.up * 0.7f;
            HudToast.Show(o.title, label, 5f); // name the new thing where it stands
            HudToast.ShowBanner(Cfg.Variant == "A" ? "The people have noticed your sign..." : "The people look up at the change...", 4f);
            return true;
        }

        IEnumerator Gather(int slot)
        {
            var at = village.SlotPosition(slot);
            var all = crowd.Agents.Concat(notables != null ? notables.All : Enumerable.Empty<Wanderer>()).Where(a => a != null).ToList();
            var crowdAgents = all.OrderBy(a => (a.transform.position - at).sqrMagnitude).Take(Mathf.Max(4, all.Count * 2 / 3)).ToList();
            foreach (var a in crowdAgents) a.gatherAt = at;
            yield return new WaitForSecondsRealtime(gatherSeconds);
            foreach (var a in crowdAgents) if (a != null) a.gatherAt = null;
        }

        int NearestSlot(Vector3 world)
        {
            int best = -1; float bd = float.MaxValue;
            for (int i = 0; i < village.slotPositions.Length; i++)
            {
                var d = (village.slotPositions[i] - world); d.y = 0;
                if (d.sqrMagnitude < bd) { bd = d.sqrMagnitude; best = i; }
            }
            return best;
        }

        /// <summary>The free slot closest in the world to <paramref name="near"/> (tiles taken by groves and channels count as used).</summary>
        int NearestFreeSlot(int near, HashSet<int> used)
        {
            var taken = new HashSet<int>(used);
            foreach (var n in clock.Sim.State.nudges ?? new List<Nudge>())
                if (Find(n.kind)?.takesTile == true) taken.Add(n.slot);
            var from = village.SlotPosition(Mathf.Max(0, near));
            int best = -1; float bd = float.MaxValue;
            for (int i = 0; i < Mathf.Min(village.slotPositions.Length, clock.config.buildingSlots); i++)
            {
                if (taken.Contains(i)) continue;
                float d = (village.slotPositions[i] - from).sqrMagnitude;
                if (d < bd) { bd = d; best = i; }
            }
            return best;
        }

        readonly List<GameObject> hiddenTiles = new();

        /// <summary>The ground tile under a slot (the channel replaces it with a river tile).</summary>
        Transform GroundTileAt(Vector3 slotPos)
        {
            var ground = village.transform.parent.Find("Ground");
            if (ground == null) return null;
            Transform best = null; float bd = float.MaxValue;
            foreach (Transform t in ground)
            {
                var d = t.position - slotPos; d.y = 0;
                if (d.sqrMagnitude < bd) { bd = d.sqrMagnitude; best = t; }
            }
            return best;
        }

        Option Find(string kind) => omens.Concat(land).FirstOrDefault(o => o.kind == kind);

        // ---------- the world shows it ----------

        void SyncProps()
        {
            if (clock.Generation != generation)
            {
                generation = clock.Generation;
                foreach (var go in props.Values) if (go != null) Destroy(go);
                props.Clear();
                foreach (var t in hiddenTiles) if (t != null) t.SetActive(true);
                hiddenTiles.Clear();
            }
            foreach (var n in clock.Sim.State.nudges ?? new List<Nudge>())
            {
                if (props.ContainsKey(n.id)) continue;
                props[n.id] = Spawn(n);
            }
        }

        GameObject Spawn(Nudge n)
        {
            var o = Find(n.kind);
            var root = new GameObject($"Nudge_{n.kind}_{n.id}");
            root.transform.SetParent(village.transform, false);
            var slotPos = village.SlotPosition(n.slot);
            bool onRim = !o.takesTile;
            var pos = onRim ? slotPos + new Vector3(0.6f, 0, -0.6f).normalized * village.slotRadius * 1.05f : slotPos;
            root.transform.position = pos;
            if (n.kind == NudgeKind.Channel && riverTile != null)
            {
                var grass = GroundTileAt(slotPos);
                if (grass != null) { grass.gameObject.SetActive(false); hiddenTiles.Add(grass.gameObject); }
                var tile = Instantiate(riverTile, grass != null ? grass.position : slotPos - Vector3.up * tileTop, grass != null ? grass.rotation : Quaternion.identity, root.transform);
                tile.name = "Channel";
            }
            if (o?.prop != null)
            {
                var go = Instantiate(o.prop, pos, Quaternion.Euler(0, n.id * 53 % 360, 0), root.transform);
                go.transform.localScale = Vector3.one * o.scale;
            }
            if (n.kind == NudgeKind.Rain && clock.Sim.State.month - n.month < 3) StartCoroutine(RainCloud(pos));
            DioramaView.AddBoundsCollider(root);
            LogCategory.Tag(root, "nudge", n.kind);
            StartCoroutine(PopIn(root.transform));
            return root;
        }

        IEnumerator RainCloud(Vector3 at)
        {
            if (cloudPrefab == null) yield break;
            var cloud = Instantiate(cloudPrefab, at + Vector3.up * 1.1f, Quaternion.identity);
            cloud.transform.localScale = Vector3.one * 0.8f;
            var ps = MakeRain(cloud.transform);
            yield return new WaitForSecondsRealtime(7f);
            ps.Stop();
            for (float t = 0; t < 1f; t += Time.unscaledDeltaTime)
            {
                cloud.transform.localScale = Vector3.one * 0.8f * (1f - t);
                yield return null;
            }
            Destroy(cloud);
        }

        static ParticleSystem MakeRain(Transform parent)
        {
            var go = new GameObject("Rain");
            go.transform.SetParent(parent, false);
            go.transform.localRotation = Quaternion.Euler(90, 0, 0);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.startLifetime = 0.6f;
            main.startSpeed = 2.2f;
            main.startSize = 0.025f;
            main.startColor = new Color(0.75f, 0.85f, 1f, 0.8f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = ps.emission;
            emission.rateOverTime = 70;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.35f;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Stretch;
            r.lengthScale = 4f;
            if (Juice.SoftMaterial != null) r.sharedMaterial = Juice.SoftMaterial;
            ps.Play();
            return ps;
        }

        static IEnumerator PopIn(Transform t)
        {
            var target = t.localScale;
            for (float k = 0; k < 1f; k += Time.unscaledDeltaTime * 2.5f)
            {
                float e = 1f + 2.2f * Mathf.Pow(k - 1f, 3) + 1.2f * Mathf.Pow(k - 1f, 2);
                t.localScale = target * Mathf.Max(0.01f, e);
                yield return null;
            }
            t.localScale = target;
        }

        /// <summary>When the people answer a nudge: a banner with the credited line, a cheer at the new building, a log line.</summary>
        void WatchAnswers(bool quiet = false)
        {
            foreach (var n in clock.Sim.State.nudges ?? new List<Nudge>())
            {
                if (!n.credited || n.reported) continue;
                n.reported = true;
                var e = clock.Sim.State.chronicle.LastOrDefault(c => c.keeper && c.buildingId == n.buildingId);
                // Wall-clock seconds from placing to the answer (survives app restarts; lab "Step away" adds no real time).
                long realS = n.placedUnixS > 0 ? System.DateTimeOffset.UtcNow.ToUnixTimeSeconds() - n.placedUnixS : -1;
                EventLog.Instance?.Log("nudge_effect", ("kind", n.kind), ("game_months", n.answerMonth - n.month), ("real_s", realS),
                    ("seen_live", !quiet), ("building", clock.Sim.State.buildings.FirstOrDefault(b => b.id == n.buildingId)?.type ?? "-"));
                if (quiet || !started || e == null) continue; // answered while away: the welcome line tells it
                HudToast.ShowBanner(e.text, 6f);
                pendingLabels[n.buildingId] = Time.unscaledTime + 3f;
                Juice.Chime();
            }
            // "The Keeper's doing!" once the answering building has actually appeared (it spawns a frame or two later).
            foreach (var id in pendingLabels.Keys.ToList())
            {
                var b = village.Find(id);
                if (b != null) { HudToast.Show("The Keeper's doing!", b.transform.position + Vector3.up * 0.6f, 5f); pendingLabels.Remove(id); }
                else if (Time.unscaledTime > pendingLabels[id]) pendingLabels.Remove(id);
            }
        }

        readonly Dictionary<int, float> pendingLabels = new();

        // ---------- UI ----------

        void OnGUI()
        {
            if (!started || !SurveyPanel.IsPlaying || NotableCardPanel.IsOpen || FeedbackPanel.IsOpen) return;
            ProtoGui.Begin();
            titleStyle ??= new GUIStyle(ProtoGui.Title) { fontSize = 26 };
            lineStyle ??= new GUIStyle(ProtoGui.Hint) { fontSize = 20 };
            float w = ProtoGui.Width, h = ProtoGui.Height;
            foreach (var (id, go) in props)
            {
                if (go == null) continue;
                var p = ProtoGui.WorldToCanvas(go.transform.position + Vector3.up * 0.55f);
                if (p.x > -100) ProtoGui.Dot(new Rect(p.x - 9, p.y - 9, 18, 18), new Color(1f, 0.83f, 0.4f, 0.95f)); // gold: the Keeper's mark
            }
            if (placing != null)
            {
                var c = new Rect(w / 2 - 100, h - 112 - ProtoGui.SafeBottom, 200, 88);
                ProtoGui.Block(c);
                if (GUI.Button(c, "Cancel")) { EventLog.Instance?.Log("nudge", ("action", "cancel"), ("kind", placing.kind)); CancelPlacing(); }
                return;
            }
            if (!trayOpen)
            {
                var r = new Rect(w / 2 - 100, h - 112 - ProtoGui.SafeBottom, 200, 88);
                ProtoGui.Block(r);
                if (GUI.Button(r, "Nudge", ProtoGui.Primary)) { trayOpen = true; EventLog.Instance?.Log("nudge", ("action", "tray")); }
                return;
            }
            const float rowH = 104;
            var opts = Options;
            float panelH = 70 + opts.Length * (rowH + 10) + 100;
            var outer = new Rect(16, h - ProtoGui.SafeBottom - 112 - 12 - panelH, w - 32, panelH);
            ProtoGui.Block(outer);
            ProtoGui.Panel(outer);
            var inner = new Rect(outer.x + 20, outer.y + 16, outer.width - 40, outer.height - 32);
            GUI.Label(new Rect(inner.x, inner.y, inner.width, 36), Cfg.Variant == "A" ? "Leave a sign for the people" : "Change their world a little", titleStyle);
            float y = inner.y + 50;
            foreach (var o in opts)
            {
                int cd = clock.Sim.NudgeCooldown(o.kind);
                var row = new Rect(inner.x, y, inner.width, rowH);
                bool ready = cd == 0;
                if (GUI.Button(row, GUIContent.none, ready ? GUI.skin.button : ProtoGui.Disabled) && ready) Choose(o);
                GUI.Label(new Rect(row.x + 20, row.y + 14, row.width - 40, 34), o.title, titleStyle);
                GUI.Label(new Rect(row.x + 20, row.y + 52, row.width - 40, 40), ready ? o.line : $"Again in {Mathf.CeilToInt(cd / 12f)} {(cd > 12 ? "years" : "year")}. The people are still taking in the last one.", lineStyle);
                y += rowH + 10;
            }
            if (GUI.Button(new Rect(inner.x, inner.yMax - 88, inner.width, 88), "Close")) trayOpen = false;
        }

        public void DrawCheats()
        {
            var sim = clock.Sim;
            GUILayout.Label("<b>P5</b>  nudges (lab speed x" + labSpeed + ")");
            GUILayout.BeginHorizontal();
            foreach (var o in omens.Concat(land))
                if (GUILayout.Button(o.kind.Replace('_', ' '), GUILayout.Height(54)))
                {
                    var n = sim.PlaceNudge(o.kind, NearestFreeSlot(0, sim.State.buildings.Select(b => b.slot).ToHashSet()));
                    n.placedUnixS = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                }
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Answer now (+3 y quiet)", GUILayout.Height(54))) sim.Advance(3 / clock.config.yearsPerRealHour);
            if (GUILayout.Button("Reset cooldowns", GUILayout.Height(54)))
                foreach (var n in sim.State.nudges) n.month -= SimCore.NudgeMonths;
            GUILayout.EndHorizontal();
            DrawTimeCheats();
        }
    }
}
