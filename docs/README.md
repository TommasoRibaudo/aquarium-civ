# Aquarium Civilization — Discovery Phase

Working concept: an idle civilization builder in a glass box. The civ lives and grows on its own
(including offline). You log in, watch what the little people are doing, and — like a real
aquarium — you have to look after it (clean up, keep it healthy) or it degrades.

## Phase order
1. **Research** (done): gather documentation on technology, idle design and reference games.
2. **Working method**: pick engine/stack, tools, repo layout, workflow.
3. **Game definition** (current): pillars, core loop, what the player sees, what "cleaning" means, scope.
4. **Planning**: prototype goals, milestones.

## Research documents
| # | File | Topic |
|---|------|-------|
| 00 | [research/00-synthesis.md](research/00-synthesis.md) | **Start here**: cross-cutting conclusions |
| 01 | [research/01-technology.md](research/01-technology.md) | Engines (Unity / Godot / web), agent simulation, offline progress, saves, desktop overlay |
| 02 | [research/02-idle-design.md](research/02-idle-design.md) | Idle/incremental design theory, pacing, good vs shallow idle games, monetization |
| 03 | [research/03-aquarium-references.md](research/03-aquarium-references.md) | Caretaking/neglect loops, god games, emergent narrative, competitors |
| 04 | [research/04-agentic-unity.md](research/04-agentic-unity.md) | Unity CLI + Pipeline, MCP bridges, batchmode, agent workflow |
| 05 | [research/05-github-cicd.md](research/05-github-cicd.md) | GameCI builds, Play/TestFlight deploy, LFS, costs |
| 06 | [research/06-assets.md](research/06-assets.md) | Asset kits (KayKit/Synty/Quaternius/Kenney), crowd animation, AI asset tools |
| 11 | [research/11-music-playback.md](research/11-music-playback.md) | When and how background music starts and pauses (Minecraft-style sparse music), proposal for the game |
| 12 | [research/12-wordless-communication.md](research/12-wordless-communication.md) | How the people talk without words: symbol bubbles, babble, gesture; recommendation for #144 and the knock `!` (#161) |
| 13 | [research/13-lighting.md](research/13-lighting.md) | Lighting today, a real night and dusk under URP on a phone, glass/frost/UI at night; recommendation and variants for #175 and #113 |
| 14 | [research/14-dilemmas-and-living-land.md](research/14-dilemmas-and-living-land.md) | Meaningful play: the Keeper's dilemmas, and a living land (biomes, species, generation, expansion); prototypes P7/P8 |
| 15 | [research/15-barren-to-living-box.md](research/15-barren-to-living-box.md) | P8 groundwork: a box that starts barren and grows (rain → grass → rabbits → wood → ponds), real-ecology rules, the Ecology Lab spike |

## Design documents
| File | Topic |
|------|-------|
| [design/game-definition.md](design/game-definition.md) | Pillars, loops, care mechanic, progression, concept variants (draft) |
| [design/prototype-plan.md](design/prototype-plan.md) | Prototype hypotheses, A/B pairs, metrics, test protocol, order (draft) |
| [design/keepers-hands.md](design/keepers-hands.md) | The Keeper's hands: a box that starts barren, misting/planting/snipping/digging, care rebuilt on the land, the kit button (draft) |
| [design/one-handed-controls.md](design/one-handed-controls.md) | One-handed play: Clean button so one finger always moves (built) |
| [design/ui-inventory.md](design/ui-inventory.md) | Every UI piece: objects, connections, states, flows, docs against code (#105) |
| [game-design-practices.md](game-design-practices.md) | Mobile/idle design checklist the tester agent judges against (draft) |

## Working docs
| File | Topic |
|------|-------|
| [tooling.md](tooling.md) | Unity CLI/Pipeline facts, stuck-dialog fixes, lessons log (append as you learn) |
| [ci.md](ci.md) | CI checks, `main` ruleset, Actions security measures |
| [workflow.md](workflow.md) | How slice work is defined (issues), tracked (Slice board) and run (lanes, Editor lock, multi-agent loop) |
| [open-questions.md](open-questions.md) | Everything waiting on a decision by the user, kept current |
| [testflight.md](testflight.md) | iOS builds via CI, app identity, tester groups, secrets |
| [feedback.md](feedback.md) | In-app feedback and surveys → GitHub issues → Playtest Feedback project; issue format |

## Open questions
See [decisions.md](decisions.md) (to be filled in as we answer them).
