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
        Mesh potMesh;

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

        /// <summary>The pot, on its stones beside the fire (camp only). Call once a frame after <see cref="Step"/>.</summary>
        public void DrawPot(Transform parent, Material material, bool show, Func<float, float, float> heightAt)
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
            }
            var p = Pot;
            potGo.transform.localPosition = new Vector3(p.x, heightAt(p.x, p.y), p.y);
            potGo.SetActive(true);
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
            potGo = null; potMesh = null;
        }

        /// <summary>A clay pot on three legs over a ring of stones (0.14 tall: bigger than life a little, like every prop).</summary>
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
                mb.Prism(new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 0.06f, 0.006f, 0.005f, 0.13f, 5, Palette.Bark,
                    Quaternion.AngleAxis(12f, new Vector3(-Mathf.Sin(a), 0f, Mathf.Cos(a))));
            }
            mb.Prism(new Vector3(0f, 0.045f, 0f), 0.04f, 0.052f, 0.06f, 9, Palette.Terracotta, topCol: Palette.Terracotta);
            mb.Prism(new Vector3(0f, 0.101f, 0f), 0.048f, 0.048f, 0.004f, 9, new Color(0.80f, 0.52f, 0.20f)); // the stew
            var mesh = mb.ToMesh("Camp_StewPot");
            mesh.hideFlags = HideFlags.DontSave;
            return mesh;
        }
    }
}
