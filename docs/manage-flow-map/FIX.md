# Manage - what to change

Companion to `MAP.md`. Written against the 2026-09-06 capture in this folder.

---

## The diagnosis, in two parts

**Throughput.** The rail viewport is pinned to the height of the card beside it. Both come out around 260 units,
so the rail shows ~2 rows while the card - which holds a title, one line of description, a cost, and a button -
runs mostly empty. On the Researched card it is a gold-bordered box with three lines of text and half its height
blank. **Space is allocated backwards:** the region with 6-17 items got the same height as the region with one.

**Decidability.** A rail row carries name, level, state. Cost, time and affordability live only on the card. A
rail tall enough for all 43 rows still would not answer *what can I do right now* - the player would click 43
times to find out. Fixing height without fixing the row template just makes the shrug faster.

Everything below serves one or the other.

---

## Tier 1 - hours, no new UI

**1. Unpin the rail from the card.** Let the rail run from the queue chips down to the NOW band instead of
stopping at the card's bottom edge. In the captured layout that is roughly 2.4x the current height: **~5 rows
instead of 2.** Buildings becomes a one-and-a-bit-screen list. Research drops from 8 viewports to 3.4. This is
the single highest ratio of result to work in the whole document.

**2. Clamp scroll to content.** `Defense`, `Buildings` and `Research` currently scroll past the end, leaving the
last row alone above two rows of empty rail. Whoever reaches the bottom of Research - the players who most need
the list - is rewarded with a blank box.

**3. Split the state colour.** Four states share one green today. Suggested split:

| State | Colour | Reads as |
|---|---|---|
| Researched, Max | green | done, nothing to do |
| Building, Training | amber | running, has a timer |
| Available and affordable | blue | **act now** |
| Available, cannot afford | blue, dimmed | want it, short |
| Locked | grey | blocked, see requirement |

Green on `Locked` is the one that is actively wrong - it reads as ready.

**4. Disable actions that cannot fire.** `TRAIN 1 FOOTMAN` is styled as available directly beneath
`Training line full . 5/5 queued`. Either disable it or relabel to `Queue full - 5/5`.

**5. Drop duration from things that cannot start,** and drop the `Short` bucket where an absolute time is already
shown next to it. `49m 0s . Short` on a locked item is two redundancies in one line.

---

## Tier 2 - the row template

**6. Put the decision on the row.** Today: `Barracks / Level 3 . Building`. Proposed:

```
[icon]  Barracks                    Lv 3     [12.3k] [9860]     9m 36s   * Building
[icon]  Quarry                      Lv 0     [ 450] [  200]     2m 10s   * Ready
[icon]  Ancient Sawmill             --       [3.2k] [ 1800]     --       * Needs Lumber Mill L2
```

Cost chips, time, and one status word. Now the rail answers the question the card was answering, and the card
becomes what it should be - detail and confirmation for the row you already chose.

**7. Dim, do not hide.** Locked and unaffordable rows stay in the list, dimmed. The player learns the shape of
the tree by scrolling it. Hiding them makes the list shorter and the game smaller.

**8. Two-column rail.** The card uses ~1100 px to render one line of description. Give the rail a second column:
**5 rows x 2 = 10 visible.** Buildings (6), Troops (9) and Defense (11) become one-screen lists. Research goes
from eight viewports to under two. Truncation at 14 characters goes away with the extra width.

---

## Tier 3 - information architecture, needs your call

**9. Group Research by source building.** 17 flat rows is the outlier, and it is the only list of things the
player does not own yet. Every row already names its building - `Available . Cathedral of Magic`,
`Armorer - Troop health +10%` - so the key exists in the data. Collapsed, that is 5-6 group headers on one
screen; expand one to see its 3-4 items. **Research stops being the longest list and becomes the shortest.**
It also makes the dependency legible: the reason you cannot take Sturdy Shields is that the Armorer is the wrong
tier, and grouping puts that fact at the top of the group instead of inside a card.

**10. Resolve Defense/Buildings by queue, not by theme.** Storage containers and the crystal mine sit in Defense
because they have an upgrade ladder, and they compete with lumber mills for the same two builders. Two ways out:

- **(a) Move them.** Defense 11 -> ~8, Buildings 6 -> ~9. Balanced, cheap, and Defense finally means towers,
  walls and gates.
- **(b) Merge into one Town area** with a Defense / Economy filter. Honest to the economy - one builder queue,
  one area - and Manage becomes three tiles: Town (17), Troops (9), Research (17). Fewer, fatter, more equal.

(b) is the better game. (a) is the safer patch. Either beats explaining the overlap to players.

**11. Make the shared builder line visible where it bites.** The chips say `Builders 2/2 . 5 queued` on every
screen and never say who is in the line. On a Defense or Buildings card, when the line is full, the primary
button should say so *before* the click and name what is ahead of it: `Builders busy - Tower Ground Archer, 7m`.

**12. Rebuild the queue drawer as a peer, not an overlay.** It currently covers the lower half, clips the rail
mid-row, hides the selected card's action bar, and shows about 1.5 of 5 jobs. Five rows of
name / time left / cancel / finish is ~400 units - it fits in a full-height right panel with room over. While it
is there: `Refund: nothing` is a true sentence written at the player. `No refund before 25%` tells them what to
do instead.

**13. Put counts on the hub tiles.** Four tiles, ~60% of the screen empty, and not one says what is behind it.
`BUILDINGS - 6 structures . 1 building now . 2 ready to upgrade` turns the chooser from a menu into a status
board, and it is the cheapest place in the whole flow to answer *where should I go*.

---

## Order of work

| | Change | Cost | Effect |
|---|---|---|---|
| 1 | Unpin rail height | hours | 2 rows -> 5 |
| 2 | Clamp scroll | hours | removes the empty-rail bug |
| 3 | State colours | hours | four states stop lying |
| 4 | Disable dead actions | hours | one less false affordance |
| 5 | Duration cleanup | hours | less noise per card |
| 6 | Row template with cost/time/status | days | rail becomes decidable |
| 7 | Dim not hide | with 6 | tree stays legible |
| 8 | Two-column rail | days | 5 rows -> 10 |
| 9 | Group Research | days + design | 17 -> ~6 headers |
| 10 | Defense/Buildings resolution | design call | removes the overlap |
| 11 | Builder-line attribution | days | conflict becomes visible |
| 12 | Queue drawer as panel | days | 1.5 jobs -> 5 |
| 13 | Hub counts | days | chooser becomes a status board |

Items 1-5 are worth doing before the next capture, so the next `MANAGE_FLOW_INVENTORY` measures the layout you
actually intend to ship.
