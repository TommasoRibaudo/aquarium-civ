# UI hierarchy (#163, draft, 2026-10-31)

What each moment must show, and how loud. Structure only: no colour, type or icon choices (those start at #106).
Built on [../ui-inventory.md](../ui-inventory.md). **Waiting for the user's sign-off.** Recommended options are marked ★.

## 1. Method

The same piece can matter a lot in one moment and not at all in another, so ranking runs per moment. Each piece
answers three questions, in order:

1. **Can the world show this instead?** Pillar 5 comes first. If the box can say it, the screen doesn't.
2. **How often is it needed, and how urgent is it right now?**
3. **Who does it serve?** Player, tester or developer.

| Tier | Meaning | Budget |
|---|---|---|
| **World** | Shown by the box, the people or the glass | As much as reads at phone size |
| **Always on** | On screen for this whole moment | At most 4 pieces, and only 1 can be the primary |
| **One tap** | Behind a visible control | One tap to reach |
| **Deep** | Two taps or more, or a screen of its own | No limit |

## 2. Rules

1. **One primary per moment.** Only one thing on screen gets the strongest treatment. Today Shape holds that look
   all the time, so Watch has to compete with it.
2. **The moment owns the screen.** When a prompt appears, tools step back. They don't disappear if the player might
   still want them.
3. **One message at a time.** Prompts outrank toasts, and toasts queue. Toast and prompt lanes stay apart
   (`GameUiPanel`).
4. **Test and dev tools never match the game's weight.** Feedback, skips and the recall prompt stay quieter than any
   player control.
5. **Panels never hide their subject.** Anything that talks about the world keeps the subject in view. The target is
   30 % of the height from chronicle-and-history §7; a panel that needs more scrolls or pages.
6. **One object, one look.** A person, a story, an era looks like itself on every surface (inventory §2).
7. **The state of a mode is always visible.** Clean, shaping and camera mode each show that they're on and how to
   leave.
8. **No counters of what you've missed.** The dot means "something good to read", never a number or a debt
   (pillars 4 and 6).

## 3. The moments

### Glance (under 60 s, a few times a day)
The question: *what's new, in one look?*

| Piece | Tier | Note |
|---|---|---|
| People wave, mess, what they built | World | Under 15 minutes away, no wave and no cards |
| **Peek: top story, one line** | Always on, **primary** | Doesn't cover the box |
| Teaser line | Always on | Under the peek |
| Box name, year | Always on, quiet | |
| Clean, Shape, History | Always on, secondary | |
| Photo, Feedback, Settings | One tap | See D3 and D4 |

### Check-in (about 3 minutes)
The question: *what happened, and what do I want to do?*

| Piece | Tier | Note |
|---|---|---|
| The story's place, shown above the card | World | Fly-to, echo |
| **Card: headline** | Always on, **primary** | Good news first |
| Page dots, Next, Skip all | Always on, secondary | Skip all is never the loudest |
| Show me, Give, Remember, Watch | Always on, secondary | One per card; Give or Watch can take primary on its card |
| Teaser line on a payoff card | Always on, quiet | |
| Body, quote | Always on, quiet | |
| HUD tools | Step back | Rule 2 |
| Person card from a portrait | One tap | |
| Also… | Deep | Last page |

### Free play (between beats)
The question: *what's going on in there?*

| Piece | Tier | Note |
|---|---|---|
| People, bubbles, mess, buildings | World | Most of what the player reads |
| Pinned person | World + always on | Gold name in the world; the chip changes, see D2 |
| Clean, Shape | Always on, no primary | The grime says clean me, the button doesn't glow |
| History | Always on, secondary | |
| Box name, year | Always on, quiet | |
| Person card | One tap | A tap on the person |
| Photo, Settings, Feedback | One tap | |
| Book | Deep | Through history |

### Acting: person card, shaping, clean mode
| Piece | Tier | Note |
|---|---|---|
| The person, kept in view | World | Rule 5 |
| **Name, job, role** | Always on, **primary** | |
| Trait, age, Pin | Always on | |
| Best and Recent stories | One tap | A short preview, then scroll |
| Family, friends, rivals | World + one tap | Tie lines in the world |
| **The ground under the finger** | World, **primary** | Shaping: footprint, valid or not |
| Tool in hand, charges, Cancel | Always on | Replaces the sheet once a tool is picked |
| Tool list with locks and caps | One tap | Slim, so the box stays visible (60–80 % today) |
| Take back | World + always on | Near the shaping, gone after a timer (#149) |
| Clean mode's state and exit | Always on | Rule 7 |

### The close
The question: *why come back?*

| Piece | Tier | Note |
|---|---|---|
| People gather, speaker, camera to the shelf | World | |
| **Teaser caption** | Always on, **primary** | Or "They'll wait for you" |
| Everything else | Hidden | Nothing competes with the last line |
| Notification ask | Always on | Only at the first era; never at the close of a normal day |

### Era moment
| Piece | Tier | Note |
|---|---|---|
| The waiting signs (baskets, leader at the fire) | World | |
| **Watch** | Always on, **primary** | Small, warm, no counter, no timer |
| HUD tools | Always on, secondary | The player may still play first |
| During the moment: everything | World | The UI slides away |
| Hold to hurry | Always on, first 3 s only | |
| Caption at the cheer, bubbles | Always on | |
| New tool glow | Always on, briefly | Then the new tool's hint |

### History
| Piece | Tier | Note |
|---|---|---|
| The box at that year | World, **primary** | Clean glass on purpose |
| Timeline, playhead | Always on | The one control that matters |
| Year line | Always on | One year on screen, not two |
| Play, speed, Now | Always on, secondary | |
| Pin caption | One tap | Small; 45 % of the height today |
| Snapshot, Close | Always on, quiet | |
| Book | One tap | Ribbon on new pages |
| Person card | One tap | From the caption |

### First run
Teach one thing at a time. Each step has one primary and no other tools: wipe, name, rain, first year, first card.
The HUD arrives piece by piece, as each tool is first needed. Skips are for testers (rule 4).

## 4. Decisions for the user

**D1. What does History open?** It does two jobs, which causes the "Skip all, then History" bug.
★ Always the time-lapse. Unread stories live on the peek, and the dot moves there.
Alternative: History opens a small menu with Stories and Time-lapse.

**D2. What should the pin chip become (#136)?**
★ A small portrait with the name, sitting apart from the tools. A tap flies to the person, a second tap opens the
card. Details wait for the references (#104, P5).
Alternative: no HUD chip at all, with only the gold name and a marker at the screen's edge when they're off camera.

**D3. Box name and year.** Today the pill looks like a button but hides Settings.
★ Quiet text, not a pill, with a small settings mark next to it.
Alternative: keep tap-on-name, with no mark.

**D4. Photo and Feedback.**
★ Photo becomes a small icon. Feedback becomes a small, quieter icon, in test builds only, and also stays in Settings.
Alternative: Feedback only in Settings, one tap further for testers.

**D5. The year stays always on?**
★ Yes, quiet: time passing is the game's story. Era gets no HUD sign, because the box shows it.
Alternative: year only in Settings and History.

**D6. Does Shape keep the primary look?**
★ No. Tools share one calm look, and the strong look is kept for the moment's main thing (rule 1).
Alternative: Shape stays strong in free play only.

## 5. Still to test

- **Reach:** the bottom row sits at the very bottom. On tall phones held one-handed, the lowest corners can be harder
  than the middle of the screen. Check on the user's phone with the wireframes.
- **Legibility:** every wireframe over dirty glass, at night and at 375×667.
- **Blur test:** with the wireframe blurred, the primary must still be the first thing seen.

## 6. Wireframes

[The live page](https://claude.ai/artifact/EcnzmRQisy5Vi5mGYSZMmD) (source: [wireframes/index.html](wireframes/index.html)) draws all thirteen screens with the ★ options, with switches for phone size, world (clean, village, dirty) and the blur test. The tablet uses the phone capture cropped to fit, because no tablet capture exists yet.
