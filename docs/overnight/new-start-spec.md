# New start: #656 + #677 (spec for the night run, 2026-11-14)

Read both issues first (`gh issue view 656 --comments`, `gh issue view 677 --comments`). The user's calls (2026-11-14), in order:
- No root dig (the gatherers never dig roots at a "stream").
- The people arrive with a small carried stockpile that must READ as theirs, brought in: a basket of food and a bundle of sticks, set down where they settle, not only numbers. HUD stock numbers show only after they set it down.
- #677 option 1: usable wood starts at 0; monthly wood comes only from grown WILD trees (the woods), never from the Keeper's tree (not even dropped branches). What wood buys waits on it. "Before that the fire burns what the land gives (dry grass, twigs from bushes), shown as such, never as wood."
- The carried sticks are kept as a visible bundle; they are NOT spendable wood (no building, the fire is not lit from them).
- Carried food is the start's food stock; it runs down until the land gives (berries, rabbits); the first refill is shown at the pile.
- Hunger is shown, never punished: nobody dies of it (the box never fails); they're slower and look at the sky.

Measured (offline pacing lab, current dev): camp founds at year ~2-5 (active player), the first wild tree grows at year ~15-23 (active) / ~21-31 (one visit every 4.5 h). So "fire only with the first wild wood" would mean 12-20 real hours of camp with no fire. RECONCILED CALL (mine, logged for the user's sign-off): 
- A small fire of grass and twigs from the time there is something to burn (dry grass/twigs: from the first bushes/thicket; before any bushes, no fire — the wanderers huddle). It looks like a grass-and-twig fire: low, quick flames, a heap of grass and thin twigs, no logs. Tending it = gathering an armful of dry grass/twigs at the bushes or the meadow edge (never "chop kindling", never "wood").
- The first wild wood (first grown wild tree's first logs) is a moment the Keeper sees: the first logs carried home, set on the pile beside the carried bundle, and the fire is rebuilt as a log fire (bigger, warmer light). From then on the camp's usual wood chores apply (#651 rules stay: no kindling chop without wild wood).
- Alternative the user may pick in the walkthrough: no fire at all until the first wild wood.

## Numbers (defaults, tune if pacing fails)
- New box: food = carried basket (start 12, was 20); wood = 0 (was 4). Same for the museum's new box.
- Monthly wood: 0 while no grown wild tree stands; then income per grown wild (woods) tree, capped by woodcutters (and lumbermills later). Keep the village reachable at year 25 ± 3 for the active player (PacingLabTests) and the other pacing tests green. If a gate needs wood before trees, move that cost, don't add wood.
- Food before berries/rabbits: the basket runs down; at 0 nobody starves (clamp, mood/slowness only).
- Gatherer job opens with real food on the land (berries or rabbits), not on bare land.

## Done when
- Sim tests (dotnet, Tools/SimTests + Unity Sim/Life EditMode): wood income 0 before the first grown wild tree; never from the Keeper's tree; no root-dig activity planned; food never below 0 and no hunger death; pacing tests green; a test that the stock appears only after the arrival set-down.
- View (Editor): the arrival with basket + bundle, set down at the camp spot; the grass-and-twig fire vs the log fire; the first-logs moment; the pile showing basket, bundle and later logs; HUD numbers appear after the set-down. Shots at 1179x2556 in Temp/agent/night/newstart_*.png.
- Debug panel cheats: "first wild wood now", "grass fire / log fire", "empty the basket", "arrival again".
- The tester plays a FRESH box start to camp with the real UI (no cheats for the path) and the first-wood moment (cheat allowed for that jump).
