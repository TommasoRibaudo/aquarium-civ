using System.Linq;
using AquariumCiv.Prototypes.Sim;
using UnityEngine;

namespace AquariumCiv.Prototypes.P3
{
    /// <summary>
    /// P3 While You Were Away (prototype-plan.md §5): does a curated chronicle with named notables make the return the
    /// highlight, and do players remember their civ's story? One variable, the return presentation:
    ///   A "chronicle cards": 3–7 cards ranked by salience, with the person's portrait and a "Show me" fly-to.
    ///   B "plain return":    the changed village plus one stats line.
    /// Both: the story generator (~30 templates), 5–12 notables with families who age at half speed and walk the village
    /// as themselves, tap a person → their card, pin a favourite. Care layer = shared wipe + mess, no consequences.
    /// </summary>
    public class WhileYouWereAway : PrototypeBase, IDebugCheats
    {
        public ChronicleCards cards;

        bool CardsVariant => Cfg.Variant == "A";

        protected override void ApplyConfig(SimConfig cfg)
        {
            base.ApplyConfig(cfg);
            cfg.storyEvents = true;
        }

        protected override void Update()
        {
            base.Update();
            if (hud != null) hud.hidden = cards.IsOpen || NotableCardPanel.IsOpen;
        }

        protected override void OnVariantStart(bool switched)
        {
            cards.Close();
            NotableCardPanel.Instance?.Close("variant");
            if (Lab)
                HudToast.ShowBanner(CardsVariant
                    ? "Lab: tap \"Step away\" below, then come back and see what happened. Tap a person to meet them."
                    : "Lab: tap \"Step away\" below, then come back and look around. Tap a person to meet them.", 8f);
            else if (!PlayerPrefs.HasKey("p3_hinted")) { PlayerPrefs.SetInt("p3_hinted", 1); wipe.ShowHint(); }
        }

        protected override void OnReturn(double hours, float years, long sinceMonth)
        {
            var s = clock.Sim.State;
            if (CardsVariant)
            {
                var picked = ChronicleCards.Rank(s, sinceMonth, cards.minCards, cards.maxCards, cards.keepAbove, cards.pinnedBoost);
                HudToast.ShowBanner(picked.Count > 0
                    ? $"Welcome back, Keeper! {years:0.#} years passed in the glass. Here's what happened."
                    : $"Welcome back, Keeper! {years:0.#} years passed. All quiet in the glass.", 5f);
                cards.Show(picked);
                EventLog.Instance?.Log("chronicle_open", ("cards", picked.Count), ("stories", s.chronicle.Count(e => e.month > sinceMonth)));
            }
            else HudToast.ShowBanner($"Welcome back, Keeper! {StatsLine(hours, years)}", 6f);
        }

        public void DrawCheats()
        {
            var sim = clock.Sim;
            GUILayout.Label("<b>P3</b>  stories, notables, return");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Story now", GUILayout.Height(54))) sim.ForceStory();
            if (GUILayout.Button("Notable dies", GUILayout.Height(54))) sim.DebugKillOldest();
            if (GUILayout.Button("Notable born", GUILayout.Height(54))) sim.DebugBirth();
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Cards: last 10 y", GUILayout.Height(54)))
                cards.Show(ChronicleCards.Rank(sim.State, sim.State.month - 120, cards.minCards, cards.maxCards, cards.keepAbove, cards.pinnedBoost));
            if (GUILayout.Button("Pin someone", GUILayout.Height(54)))
                sim.State.pinnedNotableId = sim.State.notables.Where(n => n.alive && n.age >= 16f).Select(n => n.id).DefaultIfEmpty(-1).First();
            if (GUILayout.Button("Unpin", GUILayout.Height(54))) sim.State.pinnedNotableId = -1;
            GUILayout.EndHorizontal();
            DrawTimeCheats();
        }
    }
}
