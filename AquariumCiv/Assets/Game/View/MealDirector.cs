using System;
using System.Collections.Generic;
using AquariumCiv.Life;
using AquariumCiv.Sim;
using UnityEngine;

namespace AquariumCiv.View
{
    /// <summary>
    /// #365 B9 Meals and the fire, the game side (the poses and the staging are the pure <see cref="MealBeats"/> and <see cref="MealStage"/>):
    /// <list type="bullet">
    /// <item><b>Bowls</b>: when three or more are seated at the fire to eat, the cook (else a kid, else the lowest key) walks round the ring from the pot
    /// and hands each a bowl (B7's hand over, 1.5 s a person); a seated eater without its bowl yet only waits. A meal anywhere else starts with the bowl.</item>
    /// <item><b>The fire</b>: every 5 to 8 s one of the grown-ups sitting at the fire (not eating) stands, pokes it with a stick, and sits again; the contact
    /// is the doll's <c>hit</c>, so <see cref="PeopleView.WorkHit"/> and HitFx make the sparks (every third poke a bigger burst).</item>
    /// <item><b>The pot</b>: a code-built stew pot on its stones beside the fire; the cook stands at it and stirs; steam puffs at each stir, and every
    /// fourth stir she tastes.</item>
    /// </list>
    /// </summary>
    public sealed class MealDirector
    {
        /// <summary>Where the pot stands from the fire's middle (box units): to the front-right, so the flames and the cook both show.</summary>
        public static readonly Vector2 PotOffset = new Vector2(0.10f, -0.09f);
        /// <summary>The cook stands this far from the pot's middle (arm 0.075 u + the paddle reach the stew).</summary>
        public const float CookStandOff = 0.16f;
        /// <summary>The pot's rim above the ground (the steam rises from here).</summary>
        public const float PotTop = 0.12f;

        public bool HasFire { get; private set; }
        public Vector2 Fire { get; private set; }
        /// <summary>The pot's middle now: on its cooking stones beside the fire, or where #692 (B30) has it (cold and set out, carried).</summary>
        public Vector2 Pot => PotAt ?? PotHotSpot;
        /// <summary>#692 (B30): the pot's cooking spot (on the fire's stones) and its cold spot (on three stones 0.15 u further out).</summary>
        public Vector2 PotHotSpot => BigPotAt ?? Fire + PotOffset;
        /// <summary>#693 (B54.h): on a "one big pot" night the supper is cooked and ladled at the big stew pot the travellers' choice left
        /// (CampLife sets it): the camp's own pot is not drawn, and every B30 beat plays at this spot.</summary>
        public Vector2? BigPotAt;
        public Vector2 PotColdSpot => CampScenes1.ColdPot(Fire, PotOffset);

        // ---- #692 (B30): the pot's day, set each frame by CampLife before DrawPot
        /// <summary>Where the pot stands (null: its cooking spot).</summary>
        public Vector2? PotAt;
        /// <summary>How high it is carried (box units over the ground).</summary>
        public float PotLift;
        /// <summary>On the fire and simmering: steam and bubbles (B30.e: none while cold).</summary>
        public bool PotHot = true;
        /// <summary>What is in it: 0 empty (a dark disc), 1 water, 2 the stew.</summary>
        public int PotContent = 2;
        /// <summary>The bowls stacked beside the cold pot (B30.e).</summary>
        public bool BowlStack;

        readonly Dictionary<int, float> servedAt = new Dictionary<int, float>();
        readonly HashSet<int> fireEaters = new HashSet<int>();
        readonly List<int> seatedKeys = new List<int>();
        readonly List<Vector2> seatedPos = new List<Vector2>();
        readonly List<int> candidates = new List<int>();
        readonly Dictionary<int, int> stirSeen = new Dictionary<int, int>();
        bool passed, pending;
        float firstSeatedAt = -1f, lastEaterAt = -1f;
        int pokeKey = -1, lastPokeSlot = -1;
        float pokeStart;
        bool pokeBig;
        int fireSeed;
        GameObject potGo;
        Mesh steamMesh;
        readonly Mesh[] potMeshes = new Mesh[StewKindCount], bubbleMeshes = new Mesh[StewKindCount];   // #676: one per kind, built the first time it shows
        MeshFilter potFilter;
        readonly MeshFilter[] bubbleFilters = new MeshFilter[3];
        /// <summary>#676: the kind the pot shows now.</summary>
        public StewKind Stew { get; private set; } = StewKind.Thin;
        const int StewKindCount = 4;
        readonly Transform[] bubbles = new Transform[3];
        GameObject steamGo;
        Material steamMat;
        bool steamWinter;

        static readonly HashSet<ActivityKind> Pokers = new HashSet<ActivityKind>
        {
            ActivityKind.SitOut, ActivityKind.Chat, ActivityKind.Listen, ActivityKind.ListenStory, ActivityKind.Sing, ActivityKind.SitTogether,
        };

        /// <summary>Clears everything (a new box, a time jump).</summary>
        public void Reset()
        {
            servedAt.Clear(); fireEaters.Clear(); stirSeen.Clear();
            passed = pending = false; firstSeatedAt = -1f; pokeKey = -1; lastPokeSlot = -1;
            PairStage.Clear();
        }

        /// <summary>Once a frame, before the dolls are driven. <paramref name="dollAt"/>: where a doll stands now (null if not placed).</summary>
        public void Step(IReadOnlyList<PersonActivity> acts, float time, Func<Place, Vector2?> placeAt, Func<PersonRef, LifeStage> stageOf,
                         Func<PersonRef, string> jobOf, Func<int, Vector2?> dollAt)
        {
            fireEaters.Clear(); seatedKeys.Clear(); seatedPos.Clear(); candidates.Clear();
            HasFire = false;
            CookAtFire = VillageScenes.Nobody;
            int eaters = 0, giverKey = -1, kidKey = -1, cookKey = -1;
            for (int i = 0; i < acts.Count; i++)
            {
                var a = acts[i];
                if (a.place.kind != PlaceKind.Fire) continue;
                if (!HasFire && placeAt(a.place) is Vector2 f) { Fire = f; fireSeed = a.place.buildingId + 365; HasFire = true; }
                int key = a.who.Key;
                bool seated = !a.moving && !a.enRoute;
                if (seated && a.kind == ActivityKind.Cook) CookAtFire = key; // #692 (B30)
                if (a.kind == ActivityKind.Eat)
                {
                    eaters++; fireEaters.Add(key);
                    if (seated && dollAt(key) is Vector2 p) { seatedKeys.Add(key); seatedPos.Add(p); }
                    var st = stageOf(a.who);
                    if (jobOf(a.who) == "cook") cookKey = key;
                    else if (st == LifeStage.Kid && kidKey < 0) kidKey = key;
                }
                else if (seated && Pokers.Contains(a.kind) && stageOf(a.who) is LifeStage.Adult or LifeStage.Elder) candidates.Add(key);
            }
            Eaters = eaters;
            if (!HasFire) { passed = pending = false; return; }
            candidates.Sort();

            // ---- bowls
            if (eaters > 0) lastEaterAt = time;
            if (passed && (eaters == 0 || time - lastEaterAt > 5f)) { passed = false; servedAt.Clear(); firstSeatedAt = -1f; }
            if (!passed)
            {
                if (seatedKeys.Count >= 3)
                {
                    if (firstSeatedAt < 0f) firstSeatedAt = time;
                    pending = true;
                    if (seatedKeys.Count >= eaters * 0.6f || time - firstSeatedAt > 3f)
                    {
                        giverKey = cookKey >= 0 && seatedKeys.Contains(cookKey) ? cookKey : kidKey >= 0 && seatedKeys.Contains(kidKey) ? kidKey : seatedKeys[0];

                        servedAt.Clear();
                        var takers = new List<int>(seatedKeys); var tpos = new List<Vector2>(seatedPos);
                        Dips.Clear(); // #692 (B30.b): a dip at the pot before each hand-over
                        DipStand = PotHotSpot + (PotHotSpot - Fire).normalized * CampLife.DipStandOff; /* #692 review: right at the pot to dip */ DipGiver = giverKey; DipVersion++;
                        int gi = seatedKeys.IndexOf(giverKey);
                        float toStand = gi >= 0 ? (seatedPos[gi] - DipStand).magnitude / PairStage.GiverPace : 0f;
                        var events = MealStage.Pass(fireSeed ^ (int)(time * 10f), time + 0.3f + toStand, giverKey, DipStand, Fire, Pot, takers, tpos, servedAt, Dips, PropKind.Bowl, CampScenes1.DipSeconds);
                        if (Dips.Count > 0) Dips[0] = new Vector2(time + 0.3f, Dips[0].y);
                        foreach (var e in events) PairStage.Add(e);
                        passed = true; pending = false;
                    }
                }
                else { pending = false; firstSeatedAt = -1f; }
            }

            // ---- the poke
            if (pokeKey >= 0 && (time >= pokeStart + MealBeats.Poke.Duration || !candidates.Contains(pokeKey))) pokeKey = -1;
            if (pokeKey < 0 && candidates.Count > 0 && forcePoke)
            {
                forcePoke = false; pokeKey = candidates[BeatHash.Pick(candidates.Count, fireSeed, (int)time, 3654)]; pokeStart = time; pokeBig = true;
            }
            if (pokeKey < 0 && candidates.Count > 0)
            {
                int n = MealBeats.PokeSlotAt(fireSeed, time, out float start, out bool big);
                if (n != lastPokeSlot && time >= start && time < start + 0.3f)
                {
                    lastPokeSlot = n;
                    pokeKey = candidates[MealBeats.PokerIndex(fireSeed, n, candidates.Count)];
                    pokeStart = start; pokeBig = big;
                }
            }
        }

        // ---- #692: what the camp uses read
        /// <summary>The grown-ups sitting at the fire, not eating (sorted keys): the pokers, the warm hands, the spit's turner.</summary>
        public IReadOnlyList<int> Sitters => candidates;
        /// <summary>How many eat at the fire now (a meal on), and the cook standing at the pot (−1 none).</summary>
        public int Eaters { get; private set; }
        public int CookAtFire { get; private set; } = VillageScenes.Nobody; // #695: no cook (crowd keys are negative)
        /// <summary>B30.b: the dip windows (scene seconds, x from, y to) of the last bowl round, its giver, where the giver dips, and a
        /// counter that moves on with each new round.</summary>
        public readonly List<Vector2> Dips = new List<Vector2>();
        public int DipGiver = -1, DipVersion;
        public Vector2 DipStand;
        /// <summary>The doll is in a poke now (another use leaves them be).</summary>
        public bool PokingNow(int key) => key == pokeKey;

        /// <summary>#692: nobody at the fire this frame, but the camp's fire burns: the pot and the uses still know where it is.</summary>
        public void FireAt(Vector2 at) { if (HasFire) return; Fire = at; HasFire = true; fireSeed = 365; }

        bool forcePoke;
        /// <summary>The debug cheat: the next <see cref="Step"/> that finds a grown-up sitting at the fire makes it poke at once (a big one).</summary>
        public string ForcePoke(float time) { forcePoke = true; return HasFire && candidates.Count > 0 ? "poke: " + candidates.Count + " at the fire" : "poke: nobody sits at the fire yet, it will start when someone does"; }

        /// <summary>True while the doll is asked to wait for its bowl (seated at the fire, the bowls not yet round).</summary>
        public bool Waiting(int key, float time)
        {
            if (!fireEaters.Contains(key)) return false;
            if (passed) return servedAt.TryGetValue(key, out float at) && time < at;
            return pending;
        }

        /// <summary>The doll is poking the fire now: seconds into the beat (a breath before it: negative), and whether it is the big one.</summary>
        public bool Poking(int key, float time, out float since, out bool big)
        {
            since = time - pokeStart; big = pokeBig;
            return key == pokeKey && since >= -0.05f && since < MealBeats.Poke.Duration;
        }

        /// <summary>The cook's spot: at the pot, on the side of the ring slot the plan gave.</summary>
        public Vector2 CookSpot(Vector2 slot)
        {
            var d = slot - Pot;
            if (d.sqrMagnitude < 1e-6f) d = new Vector2(0f, -1f);
            return Pot + d.normalized * CookStandOff;
        }

        /// <summary>True on the frame a new stir begins for this cook (steam rises); <paramref name="clock"/> is the doll's pose clock.</summary>
        public bool StirBegan(int key, float clock, float pace)
        {
            int idx = MealBeats.StirIndex(clock, pace, out _, out _);
            bool had = stirSeen.TryGetValue(key, out int last);
            stirSeen[key] = idx;
            return had && idx > last;
        }

        /// <summary>The pot, on its stones beside the fire (camp only). Call once a frame after <see cref="Step"/>. #676: while it shows it
        /// simmers: a slow wisp of steam (more in <paramref name="winter"/>) and a few bubbles that swell and pop on the stew.</summary>
        public void DrawPot(Transform parent, Material material, bool show, Func<float, float, float> heightAt, bool winter = false, StewKind stew = StewKind.Berries)
        {
            show &= HasFire && BigPotAt == null; // #693 (B54.h): the big pot is the pot tonight
            if (!show) { if (potGo != null) potGo.SetActive(false); return; }
            if (potGo == null)
            {
                potGo = new GameObject("Camp stew pot") { hideFlags = HideFlags.DontSave };
                potGo.transform.SetParent(parent, false);
                potFilter = potGo.AddComponent<MeshFilter>();
                var mr = potGo.AddComponent<MeshRenderer>();
                mr.sharedMaterial = material;
                for (int i = 0; i < bubbles.Length; i++)
                {
                    var b = new GameObject("Stew bubble") { hideFlags = HideFlags.DontSave };
                    b.transform.SetParent(potGo.transform, false);
                    bubbleFilters[i] = b.AddComponent<MeshFilter>();
                    var br = b.AddComponent<MeshRenderer>();
                    br.sharedMaterial = material;
                    br.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    bubbles[i] = b.transform;
                }
            }
            Stew = stew;   // #676: the look follows what the camp has
            potFilter.sharedMesh = PotContent == 2 ? PotMeshFor(stew) : PotContent == 1 ? (waterPot ??= BuildPot(WaterLook)) : (emptyPot ??= BuildPot(EmptyLook)); // #692 (B30)
            var bm = BubbleMeshFor(stew);
            for (int i = 0; i < bubbleFilters.Length; i++) bubbleFilters[i].sharedMesh = bm;
            var p = Pot;
            potGo.transform.localPosition = new Vector3(p.x, heightAt(p.x, p.y) + PotLift, p.y);
            potGo.SetActive(true);
            bool simmer = PotHot && PotContent == 2; // #692 (B30.e): cold and set out: no steam, no bubbles
            float t = Time.time;
            for (int i = 0; i < bubbles.Length; i++)
            {
                float k = BubbleAt(t, i, out Vector3 at);
                bubbles[i].localPosition = at;
                bubbles[i].localScale = new Vector3(k, k * 0.8f, k);
                bubbles[i].gameObject.SetActive(simmer);
            }
            if (simmer) DrawSteam(winter);
            else if (steamGo != null) steamGo.SetActive(false);
            DrawBurst();
            DrawLadleAndBowls(material, heightAt);
        }

        // ---------------------------------------------------------------- the user's review of #692 part 1: a swell of steam you can read
        /// <summary>A puff of steam in a face (B30.c), a sniff (B30.d), a ladle's dip (B30.b): the pot's own soft steam swells for
        /// <see cref="BurstSeconds"/>: <see cref="BurstPuffs"/> bigger white wisps rising faster and fading (the HitFx steam puff read as a flat grey disc).</summary>
        public const int BurstPuffs = 7;
        public const float BurstSeconds = 1.8f, BurstPeriod = 1.3f, BurstRise = 0.24f, BurstSizeTo = 0.05f, BurstAlpha = 0.6f;
        float burstAt = -99f, burstScale = 1f;
        GameObject burstGo; Material burstMat; Mesh burstMesh;

        public void SteamBurst(float scale = 1f) { burstAt = Time.time; burstScale = Mathf.Clamp(scale, 0.3f, 1.5f); }
        /// <summary>Tests: a burst is drawn now.</summary>
        public bool BurstShown => burstGo != null && burstGo.activeInHierarchy;

        void DrawBurst()
        {
            float k = (Time.time - burstAt) / BurstSeconds;
            if (k < 0f || k >= 1f) { if (burstGo != null) burstGo.SetActive(false); return; }
            if (burstGo == null)
            {
                var sh = Shader.Find("AquariumCiv/View/FireSmoke");
                if (sh == null) return;
                burstMat = new Material(sh) { name = "Camp_StewSteamBurst", hideFlags = HideFlags.DontSave };
                burstMat.SetFloat("_Rise", BurstRise);
                burstMat.SetFloat("_SizeFrom", SteamSizeFrom * 1.6f);
                burstMat.SetFloat("_SizeTo", BurstSizeTo);
                burstMat.SetFloat("_Drift", SteamDrift * 1.4f);
                burstMat.SetFloat("_NightFade", 0.2f);
                var verts = new List<Vector3>(); var cols = new List<Color>(); var corner = new List<Vector2>(); var data = new List<Vector2>(); var tris = new List<int>();
                var col = MeshBuilder.Linear(new Color(1f, 1f, 0.98f));
                Vector2[] corners = { new(-1f, -1f), new(-1f, 1f), new(1f, 1f), new(1f, -1f) };
                for (int i = 0; i < BurstPuffs; i++)
                {
                    var at = SteamStart(i, BurstPuffs, out float phase);
                    int o = verts.Count;
                    foreach (var c in corners) { verts.Add(at); cols.Add(col); corner.Add(c); data.Add(new Vector2(phase, BurstPeriod)); }
                    tris.AddRange(new[] { o, o + 1, o + 2, o, o + 2, o + 3 });
                }
                burstMesh = new Mesh { name = "Camp_StewSteamBurst", hideFlags = HideFlags.DontSave };
                burstMesh.SetVertices(verts); burstMesh.SetColors(cols); burstMesh.SetUVs(0, corner); burstMesh.SetUVs(1, data); burstMesh.SetTriangles(tris, 0);
                burstMesh.bounds = new Bounds(new Vector3(0f, PotTop + BurstRise * 0.5f, 0f), new Vector3(0.4f, BurstRise + 0.15f, 0.4f));
                burstGo = new GameObject("Stew steam burst") { hideFlags = HideFlags.DontSave };
                burstGo.transform.SetParent(potGo.transform, false);
                burstGo.AddComponent<MeshFilter>().sharedMesh = burstMesh;
                var r = burstGo.AddComponent<MeshRenderer>();
                r.sharedMaterial = burstMat; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
            }
            // swells in fast, holds, fades out over the last half
            float a = Mathf.Clamp01(k / 0.14f) * (1f - Mathf.Clamp01((k - 0.5f) / 0.5f));
            burstMat.SetFloat("_Alpha", BurstAlpha * burstScale * a);
            burstGo.SetActive(true);
        }

        // ---------------------------------------------------------------- the user's review of #692 part 1 (B30.b): the ladle in the giver's hand
        bool ladleHeld;
        /// <summary>This frame the ladle is in a hand: its foot (the scoop) at <paramref name="footWorld"/>, turned <paramref name="rotWorld"/>. Call after
        /// <see cref="DrawPot"/> on every frame it is held; the next DrawPot puts it back in the pot.</summary>
        public void HoldLadle(Vector3 footWorld, Quaternion rotWorld)
        {
            if (ladleGo == null) return;
            ladleGo.transform.SetPositionAndRotation(footWorld, rotWorld);
            ladleHeld = true;
        }
        /// <summary>Tests: the ladle was put in a hand since the last check.</summary>
        public bool TakeLadleHeld() { bool h = ladleHeld; ladleHeld = false; return h; }

        // ---------------------------------------------------------------- #692 (B30.b, B30.e): the ladle and the stacked bowls
        Mesh waterPot, emptyPot, ladleMesh, stackMesh;
        GameObject ladleGo, stackGo;

        /// <summary>The ladle belongs to the pot (Hard in code 8: one prop per doll): it stands in the stew while it cooks and leans against the
        /// cold pot. The bowls are stacked beside the cold pot after supper.</summary>
        void DrawLadleAndBowls(Material material, Func<float, float, float> heightAt)
        {
            if (ladleGo == null)
            {
                ladleMesh = BuildLadle(); stackMesh = BuildStack();
                ladleGo = Child("Pot ladle", ladleMesh, material, potGo.transform);
                stackGo = Child("Stacked bowls", stackMesh, material, potGo.transform.parent);
            }
            bool inStew = PotHot && PotContent > 0;
            ladleGo.transform.localPosition = inStew ? new Vector3(0.012f, 0.085f, -0.008f) : new Vector3(0.056f, 0.0f, -0.03f);
            ladleGo.transform.localRotation = inStew ? Quaternion.Euler(-12f, 0f, -14f) : Quaternion.Euler(-24f, 30f, -26f);
            ladleGo.SetActive(true);
            stackGo.SetActive(BowlStack && !PotHot);
            if (BowlStack)
            {
                var d = PotOffset.normalized; var side = new Vector2(-d.y, d.x);
                var q = PotColdSpot + d * 0.1f + side * 0.06f;
                stackGo.transform.localPosition = new Vector3(q.x, heightAt(q.x, q.y), q.y);
            }
        }

        /// <summary>#692: the ladle drawn now (tests).</summary>
        public bool LadleShown => ladleGo != null && ladleGo.activeInHierarchy;
        public bool StackShown => stackGo != null && stackGo.activeInHierarchy;

        static GameObject Child(string name, Mesh mesh, Material material, Transform parent)
        {
            var go = new GameObject(name) { hideFlags = HideFlags.DontSave };
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>(); r.sharedMaterial = material; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }

        /// <summary>B30.b: a ladle of dark wood, 0.09 u: a handle and a small bowl at its foot (the foot at the origin).</summary>
        static Mesh BuildLadle()
        {
            var mb = new MeshBuilder(3653);
            var wood = new Color(0.30f, 0.20f, 0.12f);
            mb.Box(new Vector3(0f, 0.05f, 0f), new Vector3(0.008f, 0.09f, 0.008f), wood);
            mb.Prism(new Vector3(0f, 0f, 0f), 0.008f, 0.016f, 0.012f, 7, wood, topCol: new Color(0.22f, 0.14f, 0.08f));
            var m = mb.ToMesh("Camp_Ladle"); m.hideFlags = HideFlags.DontSave; return m;
        }

        /// <summary>B30.e: four bowls stacked, each a little crooked.</summary>
        static Mesh BuildStack()
        {
            var mb = new MeshBuilder(3654);
            for (int i = 0; i < 4; i++)
                mb.Prism(new Vector3(0.002f * (i % 2), i * 0.011f, -0.002f * (i % 3)), 0.018f, 0.034f, 0.013f, 8, Palette.Terracotta, topCol: new Color(0.62f, 0.38f, 0.20f));
            var m = mb.ToMesh("Camp_BowlStack"); m.hideFlags = HideFlags.DontSave; return m;
        }

        /// <summary>#692 (B30.e): the cold, empty pot's inside (a flat dark disc under the rim), and the water before the food goes in (B30.a).</summary>
        static readonly StewLook EmptyLook = new StewLook { mid = new Color(0.09f, 0.07f, 0.06f), edge = new Color(0.07f, 0.05f, 0.05f), bubble = Color.black, bits = new Color[0] };
        static readonly StewLook WaterLook = new StewLook { mid = new Color(0.42f, 0.55f, 0.62f), edge = new Color(0.30f, 0.40f, 0.46f), bubble = Color.white, bits = new Color[0] };

        // ---------------------------------------------------------------- #676 (tester): steam that reads as steam
        /// <summary>#676: the soft puffs over the stew (more in winter). Each rises <see cref="SteamRise"/> over <see cref="SteamPeriod"/> s,
        /// widening <see cref="SteamSizeFrom"/> to <see cref="SteamSizeTo"/> and fading from <see cref="SteamAlpha"/> to 0, drifting aside;
        /// their phases are spread, so they never stack in a column. Drawn by the FireSmoke shader (alpha-blended, soft edged, animated on the
        /// GPU) at a light steam colour; it fades only a little at night (the cook works at dusk).</summary>
        public const int SteamPuffs = 4, SteamPuffsWinter = 7;
        public const float SteamPeriod = 2.6f, SteamRise = 0.16f, SteamSizeFrom = 0.007f, SteamSizeTo = 0.03f, SteamAlpha = 0.3f, SteamDrift = 0.035f;

        /// <summary>#676: puff <paramref name="i"/> of <paramref name="n"/>: its start over the stew (pot space, off the middle) and its phase.</summary>
        public static Vector3 SteamStart(int i, int n, out float phase)
        {
            phase = (i + 0.37f * ((i * 7) % 3)) / n;
            float a = i * 2.39996f + 0.4f, r = 0.012f + 0.018f * ((i * 5) % 4) / 3f;   // golden-angle ring: never two on one spot
            return new Vector3(Mathf.Cos(a) * r, PotTop - 0.004f, Mathf.Sin(a) * r);
        }

        void DrawSteam(bool winter)
        {
            if (steamGo != null && steamWinter == winter) { steamGo.SetActive(true); return; }
            if (steamMat == null)
            {
                var sh = Shader.Find("AquariumCiv/View/FireSmoke");
                if (sh == null) return;
                steamMat = new Material(sh) { name = "Camp_StewSteam", hideFlags = HideFlags.DontSave };
                steamMat.SetFloat("_Alpha", SteamAlpha);
                steamMat.SetFloat("_Rise", SteamRise);
                steamMat.SetFloat("_SizeFrom", SteamSizeFrom);
                steamMat.SetFloat("_SizeTo", SteamSizeTo);
                steamMat.SetFloat("_Drift", SteamDrift);
                steamMat.SetFloat("_NightFade", 0.4f);
            }
            steamWinter = winter;
            int n = winter ? SteamPuffsWinter : SteamPuffs;
            var verts = new List<Vector3>(); var cols = new List<Color>(); var corner = new List<Vector2>(); var data = new List<Vector2>(); var tris = new List<int>();
            var col = MeshBuilder.Linear(new Color(0.97f, 0.96f, 0.93f));
            Vector2[] corners = { new(-1f, -1f), new(-1f, 1f), new(1f, 1f), new(1f, -1f) };
            for (int i = 0; i < n; i++)
            {
                var at = SteamStart(i, n, out float phase);
                int o = verts.Count;
                foreach (var k in corners) { verts.Add(at); cols.Add(col); corner.Add(k); data.Add(new Vector2(phase, SteamPeriod)); }
                tris.AddRange(new[] { o, o + 1, o + 2, o, o + 2, o + 3 });
            }
            if (steamMesh != null) UnityEngine.Object.Destroy(steamMesh);
            steamMesh = new Mesh { name = "Camp_StewSteam", hideFlags = HideFlags.DontSave };
            steamMesh.SetVertices(verts); steamMesh.SetColors(cols); steamMesh.SetUVs(0, corner); steamMesh.SetUVs(1, data); steamMesh.SetTriangles(tris, 0);
            steamMesh.bounds = new Bounds(new Vector3(0f, PotTop + SteamRise * 0.5f, 0f), new Vector3(0.3f, SteamRise + 0.1f, 0.3f));
            if (steamGo == null)
            {
                steamGo = new GameObject("Stew steam") { hideFlags = HideFlags.DontSave };
                steamGo.transform.SetParent(potGo.transform, false);
                steamGo.AddComponent<MeshFilter>();
                var r = steamGo.AddComponent<MeshRenderer>();
                r.sharedMaterial = steamMat;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
            }
            steamGo.GetComponent<MeshFilter>().sharedMesh = steamMesh;
            steamGo.SetActive(true);
        }

        /// <summary>#676: the steam puffs drawn now (tests).</summary>
        public int SteamShown => steamGo != null && steamGo.activeInHierarchy && steamMesh != null ? steamMesh.vertexCount / 4 : 0;
        /// <summary>#676: one bubble's life on the stew: it swells, holds a moment, pops, and comes back elsewhere after a rest.</summary>
        public const float BubbleCycle = 1.4f;

        /// <summary>#676: bubble <paramref name="i"/>'s size (0 between pops) and spot on the stew (pot space) at time <paramref name="t"/>.</summary>
        public static float BubbleAt(float t, int i, out Vector3 at)
        {
            float c = t / BubbleCycle + i * 0.37f + i * i * 0.11f;
            int n = Mathf.FloorToInt(c);
            float f = c - n;
            uint h = unchecked((uint)(n * 73856093) ^ (uint)((i + 1) * 19349663));
            h ^= h >> 13; h = unchecked(h * 0x5bd1e995); h ^= h >> 15;
            float a = (h & 0xffff) / 65535f * Mathf.PI * 2f, r = 0.006f + ((h >> 16) & 0xff) / 255f * 0.026f;
            at = new Vector3(Mathf.Cos(a) * r, StewTop, Mathf.Sin(a) * r);
            // swell over 0..0.45, hold to 0.55, a last puff up to 0.6 (the pop), then rest till the next cycle
            return f < 0.45f ? Mathf.SmoothStep(0f, 1f, f / 0.45f) : f < 0.55f ? 1f : f < 0.6f ? 1f + (f - 0.55f) * 6f : 0f;
        }

        /// <summary>The pot's rim in the parent's space (the steam starts here).</summary>
        public Vector3 PotTopLocal(Func<float, float, float> heightAt)
        {
            var p = Pot;
            return new Vector3(p.x, heightAt(p.x, p.y) + PotTop, p.y);
        }

        public void Destroy()
        {
            if (potGo != null) UnityEngine.Object.Destroy(potGo);
            if (stackGo != null) UnityEngine.Object.Destroy(stackGo);
            foreach (var m in new[] { waterPot, emptyPot, ladleMesh, stackMesh }) if (m != null) UnityEngine.Object.Destroy(m);
            waterPot = emptyPot = ladleMesh = stackMesh = null; ladleGo = stackGo = null;
            for (int i = 0; i < StewKindCount; i++)
            {
                if (potMeshes[i] != null) UnityEngine.Object.Destroy(potMeshes[i]);
                if (bubbleMeshes[i] != null) UnityEngine.Object.Destroy(bubbleMeshes[i]);
                potMeshes[i] = null; bubbleMeshes[i] = null;
            }
            if (steamMesh != null) UnityEngine.Object.Destroy(steamMesh);
            if (steamMat != null) UnityEngine.Object.Destroy(steamMat);
            potGo = null; potFilter = null; steamMesh = null; steamMat = null; steamGo = null;
        }

        /// <summary>The stew's middle above the pot's foot, where the bubbles sit (AddStewPot's dome for the camp pot).</summary>
        const float StewTop = 0.108f;

        /// <summary>A soot-dark clay pot on three legs over a ring of stones (0.14 tall: bigger than life a little, like every prop).</summary>
        Mesh PotMeshFor(StewKind k) => potMeshes[(int)k] != null ? potMeshes[(int)k] : (potMeshes[(int)k] = BuildPot(LookOf(k)));
        Mesh BubbleMeshFor(StewKind k) => bubbleMeshes[(int)k] != null ? bubbleMeshes[(int)k] : (bubbleMeshes[(int)k] = BuildBubble(LookOf(k)));

        static Mesh BuildPot(StewLook look)
        {
            var mb = new MeshBuilder(3650) { jitter = 0.02f };
            for (int i = 0; i < 5; i++)
            {
                float a = i / 5f * Mathf.PI * 2f + 0.3f;
                mb.Ico(new Vector3(Mathf.Cos(a), 0.01f, Mathf.Sin(a)) * 0.062f, 0.02f, new Vector3(1f, 0.7f, 1f), Palette.Rock, 0.2f);
            }
            for (int i = 0; i < 3; i++)
            {
                float a = i * 2.094f + 0.5f;
                mb.Prism(new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 0.06f, 0.006f, 0.005f, 0.075f, 5, Palette.Bark, // #676: up to the belly, not through the stew
                    Quaternion.AngleAxis(12f, new Vector3(-Mathf.Sin(a), 0f, Mathf.Cos(a))));
            }
            AddStewPot(mb, 0.045f, 0.04f, 0.052f, 0.06f, 3651, look);
            var mesh = mb.ToMesh("Camp_StewPot");
            mesh.hideFlags = HideFlags.DontSave;
            return mesh;
        }

        static Mesh BuildBubble(StewLook look)
        {
            var mb = new MeshBuilder(3652);
            mb.Ico(Vector3.zero, 0.008f, new Vector3(1f, 0.7f, 1f), look.bubble, 0.05f);   // #676 (tester): big and light enough to read up close
            var mesh = mb.ToMesh("Camp_StewBubble");
            mesh.hideFlags = HideFlags.DontSave;
            return mesh;
        }

        /// <summary>#676 (playtest: "a light clay pot with a flat orange disc"): the soot-dark pot body (black at the foot, iron-brown up top),
        /// a lip that catches the light, and a domed, uneven brown stew with bits of root, meat and greens. Shared with the left-behind stew pot
        /// (ConsequenceProps), so the two read as the same pot. The body runs from <paramref name="baseY"/> up <paramref name="height"/>.</summary>
        public static void AddStewPot(MeshBuilder mb, float baseY, float rBottom, float rTop, float height, int seed, StewLook look = null)
        {
            look ??= Legacy;
            var rng = new System.Random(seed);
            const int sides = 11;
            float midR = (rBottom + rTop) * 0.5f, h2 = height * 0.5f, top = baseY + height;
            mb.Prism(new Vector3(0f, baseY, 0f), rBottom, midR, h2, sides, PotSoot, capTop: false, capBottom: true, topCol: PotBody, ramp: true);
            mb.Prism(new Vector3(0f, baseY + h2, 0f), midR, rTop, h2, sides, PotBody, capTop: false, topCol: PotUpper, ramp: true);
            // the lip: a short flare and a flat ring on top, lighter where it catches the light
            float lipOut = rTop * 1.1f, lipH = height * 0.09f, inR = rTop * 0.92f, stewEdge = top - height * 0.07f;
            mb.Prism(new Vector3(0f, top - lipH * 0.4f, 0f), rTop, lipOut, lipH, sides, PotUpper, capTop: false, topCol: PotLip, ramp: true);
            float lipTop = top + lipH * 0.6f;
            var below = new Vector3(0f, lipTop - 1f, 0f);
            for (int i = 0; i < sides; i++)
            {
                float a0 = i / (float)sides * Mathf.PI * 2f, a1 = (i + 1) / (float)sides * Mathf.PI * 2f;
                Vector3 d0 = new(Mathf.Cos(a0), 0f, Mathf.Sin(a0)), d1 = new(Mathf.Cos(a1), 0f, Mathf.Sin(a1));
                var up = Vector3.up * lipTop;
                mb.QuadOut(up + d0 * inR, up + d1 * inR, up + d1 * lipOut, up + d0 * lipOut, below, PotLip);
                // the inner wall from the lip down to the stew, facing in
                var low = Vector3.up * stewEdge;
                mb.QuadOut(up + d0 * inR, up + d1 * inR, low + d1 * inR, low + d0 * inR, (d0 + d1) * rTop * 4f + up, PotSoot);
            }
            // the stew: a low dome, uneven at the edge, dark broth toward the rim and a richer brown in the middle
            var mid = new Vector3(0f, stewEdge + height * 0.12f, 0f);
            var ring = new Vector3[sides];
            for (int i = 0; i < sides; i++)
            {
                float a = i / (float)sides * Mathf.PI * 2f;
                ring[i] = new Vector3(Mathf.Cos(a) * inR, stewEdge + (float)rng.NextDouble() * height * 0.05f, Mathf.Sin(a) * inR);
            }
            for (int i = 0; i < sides; i++)
                mb.TriC(mid, ring[(i + 1) % sides], ring[i], look.mid, look.edge, look.edge);
            // bits: root, meat, greens and a pale tuber, half sunk in the broth
            var bits = look.bits;
            for (int i = 0; i < bits.Length; i++)
            {
                float a = i * 2.39996f + 0.3f, r = inR * (0.2f + 0.6f * (i / (float)bits.Length));
                float br = rTop * (look.bitSize + (float)rng.NextDouble() * 0.06f);
                float y = Mathf.Lerp(mid.y, stewEdge, r / inR) - br * 0.15f;
                mb.Ico(new Vector3(Mathf.Cos(a) * r, y, Mathf.Sin(a) * r), br, new Vector3(1f, 0.6f, 1f), bits[i], 0.25f);
            }
        }

        static readonly Color PotSoot = new Color(0.10f, 0.08f, 0.07f), PotBody = new Color(0.17f, 0.12f, 0.09f),
            PotUpper = new Color(0.25f, 0.18f, 0.13f), PotLip = new Color(0.47f, 0.35f, 0.25f),
            StewMid = new Color(0.50f, 0.27f, 0.12f), StewEdge = new Color(0.33f, 0.17f, 0.08f), StewBubble = new Color(0.80f, 0.62f, 0.42f),
            StewRoot = new Color(0.90f, 0.50f, 0.16f), StewMeat = new Color(0.45f, 0.22f, 0.16f), StewGreen = new Color(0.36f, 0.55f, 0.20f),
            StewTuber = new Color(0.86f, 0.77f, 0.56f);

        /// <summary>#676: how a stew looks: the broth (middle, toward the rim), the lumps that float in it (one colour each, in ring order), their
        /// size (fraction of the rim radius), and the bubbles' colour.</summary>
        public sealed class StewLook
        {
            public Color mid, edge, bubble;
            public Color[] bits;
            public float bitSize = 0.14f;
        }

        static readonly StewLook Legacy = new StewLook { mid = StewMid, edge = StewEdge, bubble = StewBubble, bits = new[] { StewRoot, StewMeat, StewGreen, StewTuber, StewRoot, StewMeat, StewGreen } };

        static readonly StewLook[] Looks =
        {
            // Thin: pale watery broth, two greens
            new StewLook { mid = new Color(0.58f, 0.52f, 0.38f), edge = new Color(0.40f, 0.35f, 0.25f), bubble = new Color(0.82f, 0.78f, 0.66f), bits = new[] { StewGreen, StewTuber } },
            // Berries: a purple-red broth with round dark berries and a few orange roots
            new StewLook { mid = new Color(0.52f, 0.15f, 0.26f), edge = new Color(0.34f, 0.09f, 0.18f), bubble = new Color(0.78f, 0.40f, 0.52f), bitSize = 0.11f,
                bits = new[] { new Color(0.30f, 0.07f, 0.22f), new Color(0.62f, 0.12f, 0.20f), StewRoot, new Color(0.30f, 0.07f, 0.22f), new Color(0.62f, 0.12f, 0.20f), StewTuber, new Color(0.30f, 0.07f, 0.22f), StewRoot } },
            // Fish: a pale golden-grey broth with pinkish-white chunks, silver skin and a sprig of green
            new StewLook { mid = new Color(0.66f, 0.58f, 0.38f), edge = new Color(0.46f, 0.41f, 0.28f), bubble = new Color(0.90f, 0.86f, 0.70f), bitSize = 0.15f,
                bits = new[] { new Color(0.92f, 0.82f, 0.74f), new Color(0.62f, 0.67f, 0.70f), new Color(0.92f, 0.82f, 0.74f), StewGreen, new Color(0.62f, 0.67f, 0.70f), new Color(0.92f, 0.82f, 0.74f), new Color(0.92f, 0.82f, 0.74f), StewTuber } },
            // Rabbit: a deep brown gravy with dark meat chunks, orange roots and a green
            new StewLook { mid = new Color(0.40f, 0.20f, 0.10f), edge = new Color(0.26f, 0.12f, 0.06f), bubble = new Color(0.70f, 0.50f, 0.34f), bitSize = 0.16f,
                bits = new[] { new Color(0.58f, 0.32f, 0.22f), StewRoot, new Color(0.58f, 0.32f, 0.22f), StewGreen, new Color(0.58f, 0.32f, 0.22f), StewRoot, new Color(0.50f, 0.26f, 0.18f) } },
        };

        /// <summary>#676: the look of each stew kind (the camp pot reads it; the EditMode test checks they differ).</summary>
        public static StewLook LookOf(StewKind k) => Looks[(int)k];

    }
}
