# Game design practices checklist

What the **tester** agent (`.claude/agents/tester.md`) judges against, and what to check when designing or
implementing anything a player sees or touches. [game-definition.md](design/game-definition.md) wins over this file;
the user is the final judge of look and feel.

Adapted for a **portrait mobile idle game** from a PC checklist (sources: Nielsen's usability heuristics, Celia
Hodent's *The Gamer's Brain*, Valve's TF2 illustrative-rendering paper, Swink's *Game Feel*, "Juice it or lose it",
Apple HIG / Material touch-target guidance, Game Accessibility Guidelines). Draft, **pending the user's approval**.

When citing a finding, name the section: "§2 readability", "§11 return".

---

1. **Intuitiveness and discoverability.** The player can guess what to do from the look of things. Interactive things
   look touchable and nothing else does. The same look means the same thing everywhere. Touch conventions are kept:
   tap = act/select, drag = rotate or wipe, pinch = zoom, back gesture/button closes panels. Teach by doing: a hint
   at the moment it matters, never a wall of text up front.
2. **Readability of the world at phone size.** The tank is small on a phone: citizens, buildings, mess and notables
   are identifiable at a glance by silhouette, shape and value contrast first, colour second. State shows on the
   object (working, idle, happy, grumpy, notable). Decoration and post-processing (tilt-shift, fog, glass tint) never
   hide gameplay. Check it at the default zoom, fully zoomed out, at night or under dark grime, and on a small phone.
3. **The glass is the interface (pillar 5).** The tank's state is readable from the glass and the world itself, not
   from HUD bars. If you have to read a number to know the tank needs care, that's a finding.
4. **Interaction and feedback.** Every touch gets visible (and, where it fits, audio/haptic) feedback within a frame
   or two. Cleaning especially: the grime visibly responds under the finger, trash pops, people react. Nothing is a
   silent no-op: if an action can't happen, say why. Gestures don't fight each other (a wipe never rotates the camera
   by accident, and vice versa).
5. **Touch targets and reach.** Tappable things are at least **44 pt / 48 dp** (about 9 mm). Small world objects get
   generous hit areas. Main actions sit in the thumb zone (lower half of the screen). Nothing important is under the
   notch, the home indicator or the system gesture areas (respect the **safe area**).
6. **UI and HUD.** Sparse: the world is the screen. No clipped, overflowing or overlapping text or elements. Panels
   and toasts never cover what they're talking about. Works in portrait on a small phone (~375x667 pt, e.g.
   1080x1920 px), a tall phone (~1080x2400 px) and a tablet (~1640x2360 px).
7. **Performance as felt.** Steady frame rate on a mid-range phone (target 30 fps minimum and steady, 60 where
   cheap). No hitch when opening the app, during catch-up, on spawns or when panels open. Catch-up after long absences
   finishes quickly or shows progress. Watch heat and battery: an idle game is left open.
8. **Pacing and balance (idle).** Something visibly changes in every short session. Offline progress feels generous
   and bounded (caps are stated, not discovered). The early game teaches before it asks. Tuning numbers live in data
   or config, not scattered literals.
9. **Care is a toy, not a chore (pillar 2), absence is welcomed (pillar 4).** Mess is comic and reversible, never
   punishing or guilt-inducing. No deaths or collapse from neglect. Cleaning is satisfying even when barely needed.
   Any wording, sound or visual that reads as a guilt trip is a finding.
10. **Game feel and audio.** Responsive input. Distinct cues for success, discovery and "look at this". A consistent
    audio language (same event, same sound; UI sounds apart from world sounds). Sound off must still work (many
    people play muted), so never put information in audio only.
11. **The return.** Opening the app answers "what happened while I was away?" in seconds (people wave, the top story
    is visible). The welcome back is warm and never a list of failures.
12. **Accessibility basics.** Colour is never the only signal. Text is readable at default size and respects system
    text size where possible. No flicker or harsh repetition. Haptics and sound can be turned off. Everything can be
    done one-handed.
13. **Onboarding and docs.** A new mechanic gets a hint when the player first meets it, not before. Player-facing text
    matches what is actually built.
14. **Fair to time and wallet (pillar 6).** No timers built to be skipped, no FOMO, never sell relief from neglect.
    In prototypes this mostly means: don't design anything that would only make sense with those.
