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

## Still open
- AI tools for minor assets (free tiers only for now)
- Monetization model
- What "cleaning/care" concretely means in-game
- Offline simulation approach (full catch-up sim vs abstracted)
- Session cadence: how often the player is expected to check in
