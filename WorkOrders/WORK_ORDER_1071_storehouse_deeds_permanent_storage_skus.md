# WORK ORDER 1071 — Storehouse Deeds I→III: the permanent storage SKU ladder

**Status:** SPEC — needs one owner ruling (§4), then READY.
**Minted:** 2026-08-24 (UI seat), banner header bumped with the 1069–1074 block.
**Provenance:** WO-1165 §3 (*"a permanent storage upgrade is the single best missing SKU"*) + the
external review the owner ADOPTED 2026-08-24 (*"Storehouse Deed I → II → III … value that never
expires"*).

---

## 1. RCA — why this SKU shape wins where resource packs lose

Wood/iron/food are capped (2,000 base → 34,000 ceiling via containers), the faucet clears every
one-time sink in ~4 hours, overflow above cap is **discarded** (`EconomyService.cs:463-466`), and
paid grants **bypass** the cap — so a resource purchase near cap makes the player's own production
worth zero (WO-1165 §3). A permanent capacity increase is the opposite product: it *fixes* the
problem the packs cause, never expires, stacks with play, and sells three times (early / midgame /
completion) without ever touching combat.

## 2. The product

**Storehouse Deed I / II / III** — each a one-time, account-permanent, all-resources capacity
increase, applied **on top of** whatever the player's containers provide.

- **Shape: a percentage multiplier on the total cap** (base + containers), e.g. +10% / +10% / +10%
  cumulative — exact numbers are a balance pass at implementation, authored in data.
- **Deeds are ADDITIVE TO, never a replacement for, the container ladder.** The WO-966 six-level
  containers stay the in-game progression; the Deed is the premium layer above it. A Deed must never
  be the cheaper substitute for upgrading a container, or it cannibalises the game's own sink.
- One-time each → requires WO-1176 §3 server-side purchase limits. Sequenced after it.
- Wallet-keyed entitlement (`purchase_entitlements`), same restore semantics as every one-time SKU.

## 3. ⛔ Constraints

1. **The [sink-cap] oracle is the tripwire, in the right direction.** `EconomySinkCapRegression`
   pins *"no cost may exceed the max bankable amount"* against the FREE ceiling. Deeds raise the
   ceiling for buyers — fine — but **authored costs must remain completable WITHOUT any Deed**: the
   oracle must keep asserting against the unpaid ceiling, never the Deed-inflated one. State this in
   the oracle's comment or the first cost pass after launch quietly makes a Deed mandatory.
2. **Crystals/coins stay uncapped** (`TownBankCapacity.UncappableResources`) — Deeds name the capped
   trio only.
3. Covenant: capacity is convenience. No production rate, no combat stat.
4. Save/schema: the entitlement is server-side; any client mirror lands in the save as a read-only
   projection (schema bump only if a field is actually added — CLI judges at HEAD).

## 4. ⚠ OPEN — owner ruling

**Percentage multiplier vs a fourth container slot?** Multiplier is invisible-but-clean; an extra
physical container is visible in town (better "estate" feel, per the adopted review's "max the
estate" instinct) but touches placement/BaseLayout and the container singleton rules. One ruling.

## 5. Acceptance

- [ ] Deed applies on top of live container levels; upgrading a container afterwards still raises cap
- [ ] Every authored cost remains ≤ the NO-Deed maximum bankable ([sink-cap] unchanged in meaning)
- [ ] One-time per wallet, server-refused on repeat, survives reinstall
- [ ] Overflow-discard behaviour unchanged for non-buyers (no silent economy change)
- [ ] Ladder pricing respects the WO-1072 valuation discipline (no dominated rung)
