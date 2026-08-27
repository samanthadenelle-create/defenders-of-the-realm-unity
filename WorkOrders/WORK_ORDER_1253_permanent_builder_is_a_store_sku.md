# WORK ORDER 1253 - The Manage screen's "buy slot" becomes a store SKU for a PERMANENT BUILDER

**Status:** CLOSED 2026-08-27 — owner Pass (felt-validated, APK 2026.08.27.343878).
**Silo:** Monetization (store / SKU) + Manage/Queues UI + build timers
**Severity:** P1. It is a live money surface, and the current button sells the wrong thing.
**Origin:** Owner, on device, 2026-08-27: *"on manage screen still says buy slot. that should drop to
the store with a SKU to buy a permanent builder"*.

---

## ⛔ THIS SUPERSEDES WO-911 Q6/Q7 AS THE MANAGE-SCREEN AFFORDANCE

**What is there today, verified at source:**
- `BuildTimerConfig.cs:221` — `[Header("Extra queue slot (Echo-gated crystal sink, WO-911 Q6/Q7)")]`
- `extraSlotBaseCrystals = 250`, escalating: each further slot on a channel costs
  `base x (1 + slots already bought)`
- Echo-gated: each Echo above 2 unlocks the RIGHT to buy one; crystals complete it
- Surfaced from: `BuildTimerService.cs`, `BuildingUpgradePanelMvvm.cs`, `BuildingUpgradeVM.cs`,
  `ObsidianQueueHud.cs`

**What the owner ruled:** that button should **drop to the store**, and what is sold there is a
**PERMANENT BUILDER**.

## ⭐ THE LOAD-BEARING DESIGN POINT - DO NOT GET THIS WRONG

> ## A "permanent builder" is CONCURRENCY. An "extra queue slot" is DEPTH. They are different axes.

CLAUDE.md is explicit and this ticket does not relax it:
- `freeBuildSlots` = **2** — how many jobs run AT ONCE. **This is what a builder is.**
- `queueDepthPerLine` = **5** — how many jobs may be QUEUED per line.
- ⛔ *"**never** implement a depth cap by raising concurrency"* — and the inverse holds here: **never
  implement a permanent builder by raising queue depth.** A player who buys a builder and gets a
  longer queue has been sold the wrong product, with real money.

So the SKU grants **+1 concurrent builder**, persisted. The old crystal purchase granted a **queue
slot**. They are not the same thing renamed, and the migration question below follows from that.

## ⚠ THE ONE OWNER RULING NEEDED FIRST

**Does the crystal queue-slot sink survive, or is it removed entirely?**

⭐ **Recommendation: KEEP BOTH, because they sell different things.** Crystals buy DEPTH (queue more
work); real money buys a BUILDER (do more work at once). That is coherent, it preserves an existing
crystal sink, and it matches the genre — in Clash of Clans an extra builder is the real-money
purchase. If the owner would rather the crystal path go away entirely, that is a clean ruling too,
but it is hers to make and it is NOT what "drop to the store" necessarily implies.

⛔ Do not silently delete the crystal path while implementing this. If it goes, it goes on a ruling.

## ⛔ MONEY IS REAL - the constraints that bind this

Mainnet sales and SKR are live as of 2026-08-27.

- **Never delete or renumber a SKU id that has ever been sold.** Ids are live keys in
  `purchase_entitlements`. Withdraw by making a SKU unpurchasable, never by removing a row a
  settlement points at.
- **The grant must be idempotent and server-verified.** A permanent builder is permanent; a
  double-grant from a retry, or a grant that a reinstall loses, are both serious. Settle it the way
  every other entitlement settles — do not invent a second path.
- **`purchase_quotes.currency` CHECKs to `'SKR'` alone.** Price in USD, paid in SKR, like every other
  real pack. Do not add a currency.
- ⛔ Never render or log a wallet address, an email, or a real name.
- Related: **WO-1246** (SKU visibility) — this ADDS a SKU, so make sure it is not born into the
  hidden set for one of the four reasons that ticket catalogues.

## Required

1. The Manage-screen affordance routes to the store, not to a crystal spend.
2. A SKU that grants **+1 permanent concurrent builder**, server-settled and idempotent.
3. The purchase persists across reinstall (it is an entitlement, not a save flag).
4. Whatever the owner rules about the crystal sink, implemented deliberately.
5. Instrument the grant (section 12): a player reporting "I bought a builder and don't have it" must
   be triageable from a log line, never from a theory.

## Acceptance

1. `COMPILE_GATE_OK` + `REGRESSION_OK <n>/<n> suites` on fresh logs, counts read off the marker.
2. A regression proving the SKU raises **concurrency** and not queue depth, that the grant is
   idempotent under a repeated settle, and that a player without the entitlement is unaffected.
   Prove each RED first (WO-1138).
3. ⛔ No hollow passes: a guard that cannot resolve the entitlement must assert or emit
   `RegressionOutcome.Skip`/`PartialSkip`, never return quietly green.
4. ⭐ A screenshot of the Manage screen's new affordance and of the store row. ASCII-only; no meaning
   by hue alone (the owner is red/green colourblind); **measure the label width** — three truncation
   defects landed this week.
5. Owner felt-verifies the route on device.

## What NOT to touch

- ⛔ `freeBuildSlots` (2) and `queueDepthPerLine` (5) as DEFAULTS. The SKU adds on top; it does not
  re-tune the baseline.
- ⛔ The purchase/settlement rail itself. Add a SKU through the existing catalog; do not fork it.
- ⛔ The Obsidian queue's single-home invariant — all timed work stays in one queue system.
