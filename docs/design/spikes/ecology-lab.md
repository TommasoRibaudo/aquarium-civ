# Spike: Ecology Lab, a barren box that grows (#228, branch p8/ecology-lab)

**Question** ([keepers-hands.md](../keepers-hands.md) §2, [research 15](../../research/15-barren-to-living-box.md) §9):
does the Keeper's tending (mist, wipe, plant the one seed, snip) turn a barren box into a living one, in the arc's
order and at a pace that fits a camp era of a few real days? Do the levers matter, and does absence still progress?

## Plan (written before the code)
- **Where:** `Tools/EcologyLab/`, a .NET 8 console runner plus its own NUnit tests. It compiles the game's
  `Sim/Settle/Terrain.cs` read-only (with a one-line `BoxSetting` shim), so the land is the real river-valley land
  (presets 0, 1, 2: stylebox, mirror, backpond) on the real 60 × 44 site grid. Nothing in `Assets/` changes.
- **Step:** one game season. Everything random is a hash of seed + cell + season, so a run is a pure function of
  (terrain, strategy).
- **The model:** cell layers (soil, moisture, cover), one box humidity, a glass with grime and droplets, one box-wide
  water table, basins from a priority flood (their spill level = the people's flood line), the one tree (5 stages,
  water + light + weeds + seasons, the people learning), the forest spread by birds, rabbits and birds as counts,
  natural dew and rare showers, the camp gate and the village gate.
- **Time:** 1 real hour = 1 game year, so one season is 15 real minutes. The scripted players visit on a real-day
  clock: awake 16 h, asleep 8 h. (c) and (d) check in every hour they're awake, (b) every 4 hours, (a) never.
  The nights are the absences.
- **Players:** (a) does nothing; (b) mists now and then, plants, never weeds or wipes; (c) mists steadily, plants,
  full tree care (mist, wipe, snip); (d) like (c) but over-mists (3× the misting).
- **Output:** a frame every 2 game years (cover per cell, water, the tree, the camp, a cross-section strip with the
  water table), a contact sheet per run, CSV curves, and the key years. Runs go on to year 100 for the key years only,
  so a late gate still gets a year.
- **Tune** by eye until the arc reads in order, then tests, then this write-up.
