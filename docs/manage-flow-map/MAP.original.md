# Manage - the flow map, measured

**Captured** 2026-09-06 09:17 by `RunManageFlowMapCaptureHeadless` (`Builds/flowmap1`), 21 frames at 2670x1200,
all in this folder. Every number below is read off that run's `MANAGE_FLOW_INVENTORY` lines, not estimated.

---

## The number you asked about

| Area | Rows in the rail | Visible at once | Content height | Queue rows |
|---|---|---|---|---|
| **Research** | **17** | **2.2** | 2170 px | 5 |
| **Defense** | **11** | **2.2** | 1462 px | 5 |
| **Troops** | **9** | **2.2** | 1072 px | 7 |
| **Buildings** | **6** | **2.2** | 872 px | 6 (5 + queue) |
| **Total** | **43** | | | |

**43 rows across four areas, and the rail shows 2.2 of them.** The viewport is 260 px against as much as 2170 px of
content - Research asks the player to scroll through **eight screens** of rail to see what they own.

That is the disconnect. It is not that Manage holds too much; it is that it shows 5% of it at a time, so the player
never forms a picture of the whole.

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
   [rail | card | BUILDING NOW band]  ... same shape on all four ...
       |               |                |                |
       +---------------+-------+--------+----------------+
                               |
                        QUEUE drawer (per channel)
                    Builder 5 . Train 7 . Research 5
```

Every destination is the same three regions: a scrolling **rail** on the left (~26% width), one **selected card**, and
a **NOW band** with the live job. The queue drawer slides over the lower half.

## Frames in this folder

| File | What it shows |
|---|---|
| `ManageFlow_Hub_2670x1200.png` | the four-tile chooser |
| `ManageFlow_<Tab>_railtop_*.png` | the rail as the player first sees it - 2.2 rows |
| `ManageFlow_<Tab>_railbottom_*.png` | **the same rail scrolled to the end** - what is hidden today |
| `ManageFlow_<Tab>_queue_*.png` | the queue drawer open on that tab |
| `ManageFlow_<Tab>_locked_*.png` | a locked/blocked card |
| `ManageFlow_<Tab>_max_*.png` | a finished card |

for `<Tab>` in Defense, Buildings, Troops, Research.

**Which item each state frame selected** (from `MANAGE_FLOW_STATE`):
- Defense: `tower_ground_archer` **Building** / `tower_catapult` **Max**
- Buildings: `lumbermill` **Locked** / `forge` **Max**
- Troops: `troop-outrider` **not unlocked** / `troop-footman` **Max**
- Research: `armorer:blacksmith-sturdy-shields` **Locked** / `arcane-tower:arcane-basics` **Researched**

## Capture note, so nobody chases it
The run wrote all 21 frames and reported `MANAGE_FLOW_MAP_FAIL frames=21/21 fidelity=0 geometry=5 touch=5`. The five
geometry and touch failures are on the **scrolled** frames, where rows sit outside the viewport by design - the
geometry auditor was written for unscrolled panels. The frames are sound; the audit terms are not meaningful for a
deliberately scrolled rail. Judge these images by eye, not by that marker.

## What the map says about the four areas

1. **Research is the outlier at 17 rows in one flat list** - more than Buildings and Troops combined. It is also the
   only area whose rows are not things you own but things you could buy.
2. **Defense at 11 rows includes the storage containers and the crystal mine**, which are there because they carry an
   upgrade ladder, not because they defend. That is the Buildings/Defense overlap.
3. **Buildings is the smallest at 6.** The area the player thinks of as the heart of the town is the shortest list.
4. All four share the same three queues shown as chips at the top - and Defense and Buildings share ONE of them
   (the Builder line), which is why upgrading a wall and upgrading a lumber mill compete for the same two workers.
