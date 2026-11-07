// #365 AnimBench: the meal and the fire in a clip. The clip's "meal" block stages, from the game's own pure code (View/Beats/MealBeats.cs):
//   - "pass": the bowls going round (MealStage.Pass: a chain of B7 hand-overs from the pot; each taker stays seated and waits in Sit until its
//     bowl is in hand, then eats from it), and
//   - "pokes": a doll pokes the fire at a time (MealBeats.Poke over its seated pose; it holds a Stick for the beat).
// Eating itself needs no block: a doll with activity Eat draws DollStyle.EatMeal from DollMotion; a segment with "bowl": true holds the bowl.
// The cook stirs with activity Cook (DollStyle.Stir, which tastes every fourth stir); a "pot" object with "follows": "<cook>" puffs steam
// at the top of each of that cook's stirs (act "steam n" for 1.2 s).
//
//   "meal": { "pass": { "giver": "cook", "takers": ["a", "b", "c"], "t0": 1.0, "fire": [0, 0], "pot": [0.1, -0.09] },
//             "pokes": [ { "doll": "p", "t": 3.0 } ] }
using System.Collections.Generic;
using System.Text.Json.Nodes;
using AquariumCiv.Life;
using AquariumCiv.View;
using UnityEngine;

namespace AnimBench
{
    public sealed class MealSpec
    {
        public string Giver; public List<string> Takers = new List<string>();
        public float T0; public Vector2 Fire, Pot; public bool HasPass;
        public int Seed = 365;
        public readonly List<(string doll, float t)> Pokes = new List<(string, float)>();

        public static MealSpec Parse(JsonObject o)
        {
            var m = new MealSpec();
            if (o["pass"] is JsonObject p)
            {
                m.HasPass = true;
                m.Giver = Clip.Str(p, "giver") ?? throw new ClipException("meal.pass needs a giver (doll id)");
                if (p["takers"] is JsonArray ta) foreach (var t in ta) m.Takers.Add(t.GetValue<string>());
                m.T0 = Clip.Num(p, "t0", 0f);
                m.Fire = p["fire"] != null ? Clip.XZ(p["fire"], "meal fire") : Vector2.zero;
                m.Pot = p["pot"] != null ? Clip.XZ(p["pot"], "meal pot") : m.Fire + MealPotOffset;
                m.Seed = Clip.Int(p, "seed", 365);
            }
            if (o["pokes"] is JsonArray pk)
                foreach (var n in pk) { var po = (JsonObject)n; m.Pokes.Add((Clip.Str(po, "doll"), Clip.Num(po, "t", 0f))); }
            return m;
        }

        /// <summary>MealDirector.PotOffset (View/MealDirector.cs uses UnityEngine types the bench does not compile).</summary>
        public static readonly Vector2 MealPotOffset = new Vector2(0.10f, -0.09f);
    }

    public sealed partial class Simulator
    {
        readonly List<PairEvent> mealEvents = new List<PairEvent>();
        readonly Dictionary<int, float> servedAt = new Dictionary<int, float>();

        void SetUpMeal()
        {
            var ms = clip.Meal;
            if (ms == null || !ms.HasPass) return;
            var giver = clip.Doll(ms.Giver) ?? throw new ClipException($"meal: '{ms.Giver}' is not a doll");
            var keys = new List<int>(); var pos = new List<Vector2>();
            foreach (var id in ms.Takers)
            {
                var d = clip.Doll(id) ?? throw new ClipException($"meal: '{id}' is not a doll");
                keys.Add(d.Seed); pos.Add(d.At(ms.T0).From);
            }
            mealEvents.AddRange(MealStage.Pass(ms.Seed, ms.T0, giver.Seed, ms.Pot, ms.Fire, ms.Pot, keys, pos, servedAt));
        }

        /// <summary>The meal's bowl chain event that has this doll in it now (or null).</summary>
        PairEvent MealEventFor(int seed, float t)
        {
            foreach (var e in mealEvents)
                if (e.group.IndexOf(seed) >= 0 && t >= e.start && !e.IsOver(t)) return e;
            return null;
        }

        /// <summary>PeopleView: a seated eater waits (Sit, empty hands) until its bowl is in hand.</summary>
        bool MealWaiting(DollState d, float t) => servedAt.TryGetValue(d.Spec.Seed, out float at) && t < at;

        bool MealPoking(DollState d, float t, out float since)
        {
            since = 0f;
            if (clip.Meal == null) return false;
            foreach (var (doll, at) in clip.Meal.Pokes)
                if (doll == d.Spec.Id && t - at >= -0.05f && t - at < MealBeats.Poke.Duration) { since = t - at; return true; }
            return false;
        }
    }
}
