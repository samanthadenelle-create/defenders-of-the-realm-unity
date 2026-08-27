# WORK ORDER 1252 - "All builders busy" tells the player they are blocked, not what to do about it

**Status:** FIXED 2026-08-27 — implemented; awaiting owner felt-verify to CLOSE.
**Silo:** UI / HUD (build placement) + Manage/Queues
**Severity:** P2. Hit constantly in normal play - two free builder slots means saturation is the
common case, not the edge case.
**Origin:** Owner, on device, 2026-08-27: *"if you go to place a build and all the builders are busy
should say wait or compltee under manage something like that"*.

---

## The ask

When the player tries to place a building and every builder is occupied, the refusal must name the
**next step**, not just the state. The owner's two examples:

- **wait** (a build finishes on its own), or
- **complete it under Manage** (go finish one now).

⭐ **The refusal is not the problem - the dead end is.** The player is not confused about being
blocked; they are given nothing to do about it.

## The facts the message can draw on (verified canon - do NOT re-derive or restate them elsewhere)

- **`BuildTimerConfig.freeBuildSlots` = 2.** That is CONCURRENCY - how many jobs run at once.
- **`BuildTimerConfig.queueDepthPerLine` = 5.** A different axis: how deep the queue goes per line.
  ⛔ **Never implement a depth cap by raising concurrency, or vice versa.**
- **`BuildTimerService.TryBuySlot`** - an EXTRA QUEUE SLOT is Echo-gated and crystal-priced: each Echo
  above 2 unlocks the RIGHT to buy one, and crystals complete the purchase (WO-911 Q6). So for some
  players a third real option exists and the message should offer it **only when they actually
  qualify** - dangling an unavailable purchase is worse than not mentioning it.

  > ⚠ **SUPERSEDED IN PART, 2026-08-27, WHILE THIS TICKET WAS BEING WRITTEN.** The owner ruled that
  > the Manage screen's buy-slot affordance becomes a **store SKU for a PERMANENT BUILDER** -
  > **WO-1253**. That is CONCURRENCY (a builder), not DEPTH (a queue slot), and it is real money, not
  > crystals. Whether the crystal queue-slot sink survives alongside it is an open owner ruling in
  > that ticket.
  >
  > **So do not hardcode "buy a slot with crystals" into this message.** Ask the service what the
  > player's actual options are and name those. If WO-1253 lands first, the third option is "get
  > another builder in the store" for everyone, not "buy a slot" for the Echo-qualified few.
  >
  > This is exactly the drift CLAUDE.md keeps naming: a ruling recorded in one ticket while a second
  > ticket quietly carries the old one. Both now point at each other.
- **The single Queues entry is the `Upgrade` bar face, re-pointed to `PanelId.Manage`**
  (`ManageScreenPanel`), always applicable in town. The right-column Builders chip is a **status
  glance only** - its double-tap door is retired. ⛔ So "under Manage" means that bar face; do not
  invent a second route to the queue.

⚠ **Read those values off the config at implementation time, not out of this ticket.** A number
copied into a second place is the failure mode this repo keeps hitting.

## Required

1. The saturated-builders refusal names a concrete next step.
2. If the player qualifies for `TryBuySlot`, offer it; if not, do not mention it.
3. Route to `PanelId.Manage` - the existing single entry.

## Constraints

- **ASCII-only strings.** The tofu oracle fails the regression on characters the UI font cannot
  render - CJK brackets already cost a gate run this week.
- ⛔ **Watch the width.** Three truncation defects have landed in seven days (WO-1245 banner,
  PROD-014 toast, WO-1248 "Pr..."). A longer, more helpful sentence is exactly the thing that gets
  cut in half. **Measure it**, and if it needs to wrap, wrap it deliberately.
- The owner is **red/green colourblind** - never carry "blocked" by hue alone.
- **UXML does not work in builds** - code-built UI only.

## Acceptance

1. `COMPILE_GATE_OK` + `REGRESSION_OK <n>/<n> suites` on fresh logs, counts read off the marker.
2. ⭐ **A screenshot of the message at a real resolution, fully readable.** This ticket is about a
   sentence; a sentence that truncates fails it.
3. A regression covering both branches - qualifies-to-buy and does-not - and asserting the message is
   non-empty and mentions a route. Prove RED first (WO-1138).
4. Owner felt-verifies by filling both builder slots and trying to place.

## What NOT to touch

- ⛔ `freeBuildSlots` (2) and `queueDepthPerLine` (5). This ticket changes the MESSAGE, not the
  economy. If the caps feel wrong that is a separate owner ruling.
- ⛔ The retired Builders-chip double-tap door. It stays a status glance.
- ⛔ `TryBuySlot`'s Echo gate and crystal price.
