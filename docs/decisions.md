# Decisions Log

| Date | Topic | Decision | Notes |
|------|-------|----------|-------|
| 2026-10-28 | Platform | Mobile (iOS/Android) | Primary target |
| 2026-10-28 | Team | Solo developer, knows Unity/C# | Unity is the frontrunner; engine still to be confirmed after research |
| 2026-10-28 | Visual style | 3D diorama | Tiny rotatable world in glass (Tiny Glade / Townscaper feel) |
| 2026-10-28 | Setting | "Aquarium" is a metaphor | Glass terrarium on land; "aquarium" describes the care relationship |

| 2026-10-28 | Engine | Unity (confirmed) | Version pending: 6.3 LTS recommended (6.6 installed) |
| 2026-10-28 | Workflow | Fully agentic | Claude drives the Editor via Unity CLI + Pipeline (primary), CoplayDev MCP for Unity (fallback), batchmode scripts |
| 2026-10-28 | Hosting/CI | GitHub, private repo | GameCI builds; Play internal testing + TestFlight deploy |
| 2026-10-28 | Assets | Asset pack + AI for minor things | Kit choice pending (06-assets.md) |

| 2026-10-28 | Unity version | 6000.3.27f1 (6.3 LTS) | Matches GameCI images; project at `AquariumCiv/` |
| 2026-10-28 | Budget | Free options only for now | KayKit free packs (CC0); no paid packs or AI subscriptions yet |

Phase C interview (details in [design/game-definition.md](design/game-definition.md); "H" = hypothesis pending a prototype):

| Date | Topic | Decision | Notes |
|------|-------|----------|-------|
| 2026-10-28 | Neglect feel | Comic chaos + gentle guilt | Visible, funny, reversible mess, plus a slight output drop (H: size tested in P2). Capped at ~48 h; stock never lost |
| 2026-10-28 | Death | Old age only | Neglect never kills; the civ never collapses |
| 2026-10-28 | Control | Caretaker + nudges | Player tends the environment and nudges; the people decide what to build |
| 2026-10-28 | Care types | Glass grime/fog, waste/trash, overgrowth/weather | Disease out of scope for now. The glass is always the player's job |
| 2026-10-28 | Check-in (3 min) | Chronicle → clean → nudge | |
| 2026-10-28 | Deep session (20 min) | Live era transitions + watcher events, and terraforming/decorating | |
| 2026-10-28 | Progression | Eras inside a box → the box grows → new box on a shelf | Old boxes stay alive as museums (H: P6) |
| 2026-10-28 | Time | 1 real hour ≈ 1 game year | Tunable; mess runs on real hours. H: attachment vs lifespan (P3) |
| 2026-10-28 | Citizens | Crowd + 5–12 named notables | Pin a favorite; legends roster |
| 2026-10-28 | History | Saved and shown as a headline feature | Reference: WorldBox. Presentation tested in P4 |
| 2026-10-28 | Anti-references | FarmVille/Godus timers, Tamagotchi guilt, menu idle civs, F2P ad-farms | |
| 2026-10-28 | Monetization (hypothesis) | Free first era + one-time unlock (~$4.99–9.99), then cosmetics and box expansions | No ads, no skip currency, no gacha, never sell care |
| 2026-10-28 | Concept variants | All four (Keeper's Glass, Chronicle Box, Gardener God, Shelf of Worlds) go through prototyping | Plan approved; P0 started |
| 2026-10-28 | Notable aging | Notables age at half speed | ~5 real days of life at 1 h = 1 y |
| 2026-10-28 | Test speed | 1 real hour = 1 game year | Faster speeds are dev-only debug presets |
| 2026-10-28 | Testers | iPhone via TestFlight | User has an Apple developer account. iOS builds via CI (no Mac) |
| 2026-10-29 | iOS CI | GitHub Actions: GameCI export on Linux → Xcode 26 cloud-managed signing + upload on macOS | No fastlane `match`, no Mac. Setup: [testflight.md](testflight.md) |
| 2026-10-29 | App identity | "Aquarium Civ", `com.tommasoribaudo.aquariumciv`, portrait only | Placeholder icon rendered from the look-test diorama |
| 2026-10-29 | P1 cleaning gesture | **Drag-to-wipe (variant A)** wins: the user played both, "that one felt amazing" | P2 uses the wipe; friends' lab data still to come and can only reshape, not reverse. [results/P1.md](design/results/P1.md) |
| 2026-10-28 | Prototyping | Throwaway scenes under `Assets/Prototypes/`, within-subject A/B with the user + 3–5 friends | Plan: [design/prototype-plan.md](design/prototype-plan.md) |
| 2026-10-28 | Agent workflow | Main session implements; a `tester` sub-agent plays and judges visual/gameplay changes (never fixes); tooling lessons logged in `docs/tooling.md` | Adapted from a shared Unity workflow doc. Issue tracker, board and branch-per-issue deferred until production |
| 2026-10-28 | Repo visibility | Public | Free Actions minutes (iOS builds now cost $0). Full-history gitleaks scan was clean before going public |
| 2026-10-28 | License | Copyleft: code GPL-3.0-or-later + Unity/app-store permissions; original art, audio and docs CC BY-SA 4.0 | Third-party packs keep their own licenses. The user owns all copyright so far. See [README](../README.md) |
| 2026-10-28 | CI | Every PR and push to `main`: workflow lint, secret scan, Unity EditMode tests; `main` only via PR with green checks | Setup and security measures: [ci.md](ci.md) |

## Still open
- AI tools for minor assets (free tiers only for now)
- Offline simulation: P0 uses a coarse aggregate catch-up; whether civ progress needs any offline cap (Q7)
