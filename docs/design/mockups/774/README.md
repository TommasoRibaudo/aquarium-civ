# #774 mock-ups: person card and History family tree

Static HTML, invented people, no game code. Open `index.html`. PNGs are Edge headless renders at 375x667 and 393x852 frames.

## What changes from today
- Person card today: tabs About / Recent / Best; About holds the tie chips, then roles, relics and one story. Recent and Best are two lists of up to 3 stories each.
- New: family and relations come first, each relation with a one-line story; "Recent" and "Best" become one story timeline (a star marks what used to be Best), or the Story tab.
- History: a new Family tree section next to the small book's Family pages (those draw a 3-level tree for up to 4 families). A: top-down tree of a founding couple; B: pick a branch and follow one line down the generations.

## Data
- Already in the sim: parentId, partnerId, born/died month, alive, friend/rival/mentor links, PersonCard ties and stories, BookFamily.
- Missing: a stored second parent (derived from the parent's partner at the birth, wrong after a remarriage), a generation number (walk parentId), per-branch counts, a one-line story per relation (derive from the chronicle entries that name both).

## Open questions for the user
1. Card: one scroll (A) or tabs (B)? On the 30 % card, keep the family chips as the only row?
2. Tree: whole-family diagram (A), follow-a-line (B), or A with B's line view when you tap a branch chip?
3. Do the dead stay in the tree (greyed, a stone) for everyone, or only legends?
4. Replace the small book's Family pages with the tree, or keep both?
5. Should Pin stay on the card header in both takes?
