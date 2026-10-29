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

## Design documents
| File | Topic |
|------|-------|
| [design/game-definition.md](design/game-definition.md) | Pillars, loops, care mechanic, progression, concept variants (draft) |
| [design/prototype-plan.md](design/prototype-plan.md) | Prototype hypotheses, A/B pairs, metrics, test protocol, order (draft) |
| [game-design-practices.md](game-design-practices.md) | Mobile/idle design checklist the tester agent judges against (draft) |

## Working docs
| File | Topic |
|------|-------|
| [tooling.md](tooling.md) | Unity CLI/Pipeline facts, stuck-dialog fixes, lessons log (append as you learn) |
| [ci.md](ci.md) | CI checks, `main` ruleset, Actions security measures |
| [workflow.md](workflow.md) | How slice work is defined (issues), tracked (Slice board) and run (lanes, Editor lock, multi-agent loop) |
| [testflight.md](testflight.md) | iOS builds via CI, app identity, tester groups, secrets |
| [feedback.md](feedback.md) | In-app feedback and surveys → GitHub issues → Playtest Feedback project; issue format |

## Open questions
See [decisions.md](decisions.md) (to be filled in as we answer them).
