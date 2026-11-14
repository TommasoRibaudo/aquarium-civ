using System;
using System.Collections.Generic;
using AquariumCiv.Life;
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
        public Vector2 Pot => Fire + PotOffset;

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
        Mesh potMesh, bubbleMesh;
        readonly Transform[] bubbles = new Transform[3];
        float steamWait;

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
            int eaters = 0, giverKey = -1, kidKey = -1, cookKey = -1;
            for (int i = 0; i < acts.Count; i++)
            {
                var a = acts[i];
                if (a.place.kind != PlaceKind.Fire) continue;
                if (!HasFire && placeAt(a.place) is Vector2 f) { Fire = f; fireSeed = a.place.buildingId + 365; HasFire = true; }
                int key = a.who.Key;
                bool seated = !a.moving && !a.enRoute;
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
                        var events = MealStage.Pass(fireSeed ^ (int)(time * 10f), time + 0.3f, giverKey, Pot, Fire, Pot, takers, tpos, servedAt);
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
        public void DrawPot(Transform parent, Material material, bool show, Func<float, float, float> heightAt, bool winter = false)
        {
            show &= HasFire;
            if (!show) { if (potGo != null) potGo.SetActive(false); return; }
            if (potGo == null)
            {
                potMesh = BuildPot();
                potGo = new GameObject("Camp stew pot") { hideFlags = HideFlags.DontSave };
                potGo.transform.SetParent(parent, false);
                potGo.AddComponent<MeshFilter>().sharedMesh = potMesh;
                var mr = potGo.AddComponent<MeshRenderer>();
                mr.sharedMaterial = material;
                bubbleMesh = BuildBubble();
                for (int i = 0; i < bubbles.Length; i++)
                {
                    var b = new GameObject("Stew bubble") { hideFlags = HideFlags.DontSave };
                    b.transform.SetParent(potGo.transform, false);
                    b.AddComponent<MeshFilter>().sharedMesh = bubbleMesh;
                    var br = b.AddComponent<MeshRenderer>();
                    br.sharedMaterial = material;
                    br.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    bubbles[i] = b.transform;
                }
            }
            var p = Pot;
            potGo.transform.localPosition = new Vector3(p.x, heightAt(p.x, p.y), p.y);
            potGo.SetActive(true);
            float t = Time.time;
            for (int i = 0; i < bubbles.Length; i++)
            {
                float k = BubbleAt(t, i, out Vector3 at);
                bubbles[i].localPosition = at;
                bubbles[i].localScale = new Vector3(k, k * 0.8f, k);
            }
            steamWait -= Time.deltaTime;
            if (steamWait <= 0f)
            {
                steamWait = winter ? SteamEveryWinter : SteamEvery;
                HitFx.Puff(PuffKind.Steam, parent.TransformPoint(PotTopLocal(heightAt)), Vector3.up, winter ? 1f : 0.65f);
            }
        }

        /// <summary>#676: a wisp of steam rises this often while the pot shows (the stir still puffs its own, bigger one); winter shows more.</summary>
        public const float SteamEvery = 1.1f, SteamEveryWinter = 0.55f;
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
            if (potMesh != null) UnityEngine.Object.Destroy(potMesh);
            if (bubbleMesh != null) UnityEngine.Object.Destroy(bubbleMesh);
            potGo = null; potMesh = null; bubbleMesh = null;
        }

        /// <summary>The stew's middle above the pot's foot, where the bubbles sit (AddStewPot's dome for the camp pot).</summary>
        const float StewTop = 0.108f;

        /// <summary>A soot-dark clay pot on three legs over a ring of stones (0.14 tall: bigger than life a little, like every prop).</summary>
        static Mesh BuildPot()
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
            AddStewPot(mb, 0.045f, 0.04f, 0.052f, 0.06f, 3651);
            var mesh = mb.ToMesh("Camp_StewPot");
            mesh.hideFlags = HideFlags.DontSave;
            return mesh;
        }

        static Mesh BuildBubble()
        {
            var mb = new MeshBuilder(3652);
            mb.Ico(Vector3.zero, 0.0055f, new Vector3(1f, 0.7f, 1f), StewBubble, 0.05f);
            var mesh = mb.ToMesh("Camp_StewBubble");
            mesh.hideFlags = HideFlags.DontSave;
            return mesh;
        }

        /// <summary>#676 (playtest: "a light clay pot with a flat orange disc"): the soot-dark pot body (black at the foot, iron-brown up top),
        /// a lip that catches the light, and a domed, uneven brown stew with bits of root, meat and greens. Shared with the left-behind stew pot
        /// (ConsequenceProps), so the two read as the same pot. The body runs from <paramref name="baseY"/> up <paramref name="height"/>.</summary>
        public static void AddStewPot(MeshBuilder mb, float baseY, float rBottom, float rTop, float height, int seed)
        {
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
                mb.TriC(mid, ring[(i + 1) % sides], ring[i], StewMid, StewEdge, StewEdge);
            // bits: root, meat, greens and a pale tuber, half sunk in the broth
            Color[] bits = { StewRoot, StewMeat, StewGreen, StewTuber, StewRoot, StewMeat, StewGreen };
            for (int i = 0; i < bits.Length; i++)
            {
                float a = i * 2.39996f + 0.3f, r = inR * (0.2f + 0.6f * (i / (float)bits.Length));
                float br = rTop * (0.14f + (float)rng.NextDouble() * 0.06f);
                float y = Mathf.Lerp(mid.y, stewEdge, r / inR) - br * 0.15f;
                mb.Ico(new Vector3(Mathf.Cos(a) * r, y, Mathf.Sin(a) * r), br, new Vector3(1f, 0.6f, 1f), bits[i], 0.25f);
            }
        }

        static readonly Color PotSoot = new Color(0.10f, 0.08f, 0.07f), PotBody = new Color(0.17f, 0.12f, 0.09f),
            PotUpper = new Color(0.25f, 0.18f, 0.13f), PotLip = new Color(0.47f, 0.35f, 0.25f),
            StewMid = new Color(0.50f, 0.27f, 0.12f), StewEdge = new Color(0.33f, 0.17f, 0.08f), StewBubble = new Color(0.62f, 0.38f, 0.20f),
            StewRoot = new Color(0.90f, 0.50f, 0.16f), StewMeat = new Color(0.45f, 0.22f, 0.16f), StewGreen = new Color(0.36f, 0.55f, 0.20f),
            StewTuber = new Color(0.86f, 0.77f, 0.56f);
    }
}
