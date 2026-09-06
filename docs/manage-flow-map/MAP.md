# Manage - the flow map, measured

**Captured** 2026-09-06 09:17 by `RunManageFlowMapCaptureHeadless` (`Builds/flowmap1`), 21 PNGs at 2670x1200,
all in this folder. Numbers below are read off that run's `MANAGE_FLOW_INVENTORY` lines, not estimated.
Reviewed against the images 2026-09-06; corrections are marked and the pre-review copy is `MAP.original.md`.

---

## The number you asked about

| Area | Rows in the rail | Visible at once | Content height | Share of the list you can see |
|---|---|---|---|---|
| **Research** | **17** | ~2.0 | 2170 | **13%** |
| **Defense** | **11** | ~2.0 | 1462 | 20% |
| **Troops** | **9** | ~2.2 | 1072 | 24% |
| **Buildings** | **6** | ~1.8 | 872 | 37% |
| **Total** | **43** | | | |

Viewport is 260 in the same units. **The rail shows about two rows, whatever the area holds.** Research asks the
player to scroll through **eight viewports** of rail to see what they own.

> **Correction, unit.** The original wrote a flat **2.2** for all four areas. Divide each content height by its row
> count and the implied per-row heights come out 128 / 133 / 119 / 145, which gives 2.04 / 1.96 / 2.18 / 1.79 -
> not one constant. Either row heights genuinely vary by area or content height includes padding the row count does
> not. Say which in the next capture, and state the unit: 260 and 2170 are layout units, not pixels of the
> 2670x1200 image, where the rail viewport measures ~318 device px.

> **Correction, arithmetic.** The original said Manage "shows 5% of it at a time." 2.2/43 is 5%, but the player
> never sees rows from two areas at once - the four lists live on four screens. Within an area the honest number is
> the last column above: 13% at worst, 37% at best.

That is the disconnect, and it survives the correction: the viewport is 260 against as much as 2170 of content, so
the player never forms a picture of the whole.

## The second disconnect, which the numbers miss

A rail row carries **name, level, and state** - "Barracks / Level 3 . Building", "Battlemage / Locked . T5".
Cost, build time and whether the player can actually afford the thing appear **only on the selected card**.

So even a rail tall enough to show all 43 rows would not let a player answer *what can I do right now?* They would
still have to click each row in turn to find out. Height is necessary and not sufficient - the row template has to
carry the decision.

## The flow

```
                          MANAGE (hub)
                     "Choose a path" - 4 tiles
                               |
       +---------------+-------+--------+----------------+
       |               |                |                |
   DEFENSE         BUILDINGS         TROOPS          RESEARCH
   11 rows          6 rows           9 rows          17 rows
       |               |                |                |
   [rail | card | NOW band]  ... same shape on all four ...
       |               |                |                |
       +---------------+-------+--------+----------------+
                               |
                        QUEUE drawer (per channel)
                Builders 2/2 . Training 2/2 . Research 2/2
                      5 queued on each, cap 5
```

Every destination is the same three regions: a scrolling **rail** on the left (~26% width), one **selected card**,
and a **NOW band** with the live job. The queue drawer slides over the lower half.

The header chips read `Builders 2/2 . 5 queued`, `Training 2/2 . 5 queued`, `Research 2/2 . 5 queued` on every
frame - two slots running, five waiting, on all three channels.

> **Correction, queue counts.** The original had a "Queue rows" column reading 5 / 5 / 7 / 6 (5 + queue). That
> mixed jobs with UI rows and did not match the chips, which say 5 on all three channels everywhere, and the Troops
> card, which says `Training line full . 5/5 queued`. Column dropped. If a per-area row count is wanted, define
> whether it counts jobs, drawer rows, or drawer rows plus header.

## Frames in this folder

| File | What it shows |
|---|---|
| `ManageFlow_Hub_2670x1200.png` | the four-tile chooser |
| `ManageFlow_<Tab>_railtop_*.png` | the rail as the player first sees it - about two rows |
| `ManageFlow_<Tab>_railbottom_*.png` | **the same rail scrolled to the end** - what is hidden today |
| `ManageFlow_<Tab>_queue_*.png` | the queue drawer open on that tab |
| `ManageFlow_<Tab>_locked_*.png` | a locked/blocked card |
| `ManageFlow_<Tab>_max_*.png` | a finished card |

for `<Tab>` in Defense, Buildings, Troops, Research.

> **Correction, coverage. 21 files, 18 unique images.** Three `railtop` frames are byte-identical (md5) to a state
> frame on the same tab:
>
> - `Defense_railtop` == `Defense_locked`
> - `Research_railtop` == `Research_max`
> - `Troops_railtop` == `Troops_max`
>
> Only `Buildings_railtop` is an independent capture. Whether that is a capture bug or simply the same scroll
> position and selection appearing twice, the set does not carry four independent first-sight frames, and the
> filenames imply it does.

> **Correction, naming.** `Defense_locked` does not show a locked card. It shows Archer Tower L1 mid-upgrade,
> status **Building**, with a BUILDING action bar - which is what the state table below already says. Either
> rename the slot or capture an actually-locked Defense card.

**Which item each state frame selected** (from `MANAGE_FLOW_STATE`):

- Defense: `tower_ground_archer` **Building** / `tower_catapult` **Max**
- Buildings: `lumbermill` **Locked** / `forge` **Max**
- Troops: `troop-outrider` **not unlocked** / `troop-footman` **Max**
- Research: `armorer:blacksmith-sturdy-shields` **Locked** / `arcane-tower:arcane-basics` **Researched**

## Capture note, so nobody chases it

The run wrote all 21 frames and reported `MANAGE_FLOW_MAP_FAIL frames=21/21 fidelity=0 geometry=5 touch=5`. The
five geometry and touch failures are on the **scrolled** frames, where rows sit outside the viewport by design -
the geometry auditor was written for unscrolled panels. Judge these images by eye, not by that marker.

By eye, though, one of those scrolled frames is showing a real bug, not an auditor artifact. See below.

## What the map says about the four areas

1. **Research is the outlier at 17 rows in one flat list** - more than Buildings and Troops combined. It is also
   the only area whose rows are not things you own but things you could buy. Every row already names its source
   building (`Available . Cathedral of Magic`, `Armorer - Troop health +10%`), so the grouping key exists.
2. **Defense at 11 rows includes the storage containers and the crystal mine**, which are there because they carry
   an upgrade ladder, not because they defend. That is the Buildings/Defense overlap.
3. **Buildings is the smallest at 6.** The area the player thinks of as the heart of the town is the shortest list.
4. All four share the same three queues shown as chips at the top - and Defense and Buildings share ONE of them
   (the Builder line), which is why upgrading a wall and upgrading a lumber mill compete for the same two workers.
   The chips never say *who* is in that line, so the conflict is invisible until a build is refused.

## Defects visible in the frames

Found on review of the images, not in the inventory numbers.

1. **The rail scrolls past its own content.** On `Defense`, `Buildings` and `Research` railbottom, the final row
   sits alone at the top of the viewport with roughly two rows of empty rail beneath it - measured as flat
   background across the lower 60% of the rail region. `Troops_railbottom` ends correctly with two rows. Clamp
   scroll to content height.
2. **Four different states share one green.** `Locked`, `Building`, `Max` and `Researched` all render as a pill on
   the same green. Green on a locked item reads as ready.
3. **Troops offers an action it cannot take.** The Footman card reads `Training line full . 5/5 queued` directly
   above a `TRAIN 1 FOOTMAN` button styled as available, beside a correctly-disabled `MAX LEVEL`.
4. **The queue drawer has the same disease as the rail.** It covers the lower half, clips the rail mid-row and
   hides the selected card's action bar, and shows about 1.5 of the 5 queued jobs. The thing built to reveal the
   queue reveals a third of it.
5. **Duration shown on things that cannot start.** The locked Sturdy Shields card shows `49m 0s . Short`. Also
   `Short` restates the absolute time next to it on every card.
6. **Rail labels truncate at roughly 14 characters** - `Cathedral of M...`, `Echo Legionna...`, `Mana Attunem...` -
   in a 26%-wide rail on a 2670-wide screen.
7. **The hub carries no counts.** Four tiles, roughly 60% of the screen empty, and not one of them says how many
   rows are behind it or whether anything is running there.

See `FIX.md` for what to do about all of this.
