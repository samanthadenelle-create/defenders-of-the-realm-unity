# WORK ORDER 1071 — Storehouse Deeds I→III: the permanent storage SKU ladder

**Status:** READY — §4 RULED by the owner 2026-08-24 (percentage multiplier + cosmetic evolution). Implementation still sequences behind WO-1176 §3 purchase limits. ⚠ The cosmetic half is NEW work in its own ticket (the lead is minting it) — do not absorb it here.
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

---

## ⭐ OWNER RULING 2026-08-24

§4 is answered, and the owner **added scope**. This ticket moves **SPEC → READY**.

### §4 — **PERCENTAGE MULTIPLIER.** Not a fourth container slot.

Authored ladder (data, tunable at implementation):

| SKU | Effect on total cap (base + containers) |
|---|---|
| **Storehouse Deed I** | **+25%** |
| **Storehouse Deed II** | a further **+25–50%** |
| **Storehouse Deed III** | a further increase above II |

- ⛔ **No fourth container object.** Placement, `BaseLayout`, and the container-singleton rules are
  **untouched** — that is the whole reason the multiplier wins.
- The multiplier applies **on top of** live container levels, so upgrading a container after buying a
  Deed still raises the cap (§5 acceptance box 1 unchanged).
- The **[sink-cap] oracle keeps asserting against the NO-Deed ceiling** (§3.1 stands, unmodified).

### ⭐ ADDED SCOPE — **cosmetic evolution of the EXISTING storage buildings**

The owner paired the multiplier with visible change, because *"permanent purchases should feel
permanent."* An invisible cap number does not read as permanence; the building does.

Each Deed visibly upgrades the storage buildings the player already owns:

- upgraded props on and around the building
- extra crates / carts / stone piles
- reinforced doors
- banners and signage
- a larger surrounding yard

⛔ **WITH NO EXTRA CONTAINER OBJECT.** The evolution is **dressing on the existing structure's
visual** — never a new placeable, never a second building, never an added `BaseLayout` footprint.
That constraint is what keeps the added scope free of the placement/singleton risk the rejected
"fourth container" option carried. If an implementation finds itself adding an object the layout
owns, it has left the ruling.

⚠ **THE COSMETIC HALF IS NEW WORK AND IS TRACKED SEPARATELY.** The lead is minting its own ticket for
it. Do **not** implement it inside this WO, and do not let this WO's acceptance depend on it — the
multiplier ships on its own timeline (behind WO-1176 §3), the visual evolution on the cosmetic rail's.
Cross-reference, do not merge.

### Additional acceptance (from this ruling)

- [ ] Deed effect is a **percentage of the computed total cap**, authored in data, never a flat add
- [ ] No new placeable, no `BaseLayout` change, no container-singleton change — asserted
- [ ] Cosmetic evolution is scoped to the companion ticket and referenced, not duplicated here
