# WORK ORDER 947 — Cost-basket separation: regular structures = wood+iron; magical/ethereal = crystal-based; never all three

**Status:** SPEC — needs owner classification pins (§4), then READY
**Minted:** 2026-08-10 (CLI seat, main line — banner bumped 947 → 948 in the same edit)
**Silo:** Data (structures-catalog.json dual-copy) + one regression gate — no code-lane conflicts
**Type:** owner ECONOMY RULING, applied to data + enforced by a gate

---

## 1. The ruling (owner, 2026-08-10, verbatim intent)

> "The lens... is what are we building? If we are building regular structures, then it makes sense
> that they only cost... iron and wood. However, if it's magical based or ethereal based, then yes,
> it's crystal... Let's make a separation. So it doesn't touch all three."

Operationalized:
- **Regular structures** cost **wood + iron** (± food where already used). **Never crystals.**
- **Magical/ethereal structures** are **crystal-based**: **crystals + iron** (mapping the owner's
  "crystal and stone" to iron, the mineral — ⚠ owner may amend to crystals+wood). **Never wood+iron+
  crystals together.**
- **Invariant (gate-enforced): no structure's cost basket contains wood AND iron AND crystals.**

## 2. Audit of the live catalog (v15, both copies identical — run 2026-08-10)

Violators of the invariant / crystal-in-regular today:

| id | cost today | proposed side | proposed basket |
|---|---|---|---|
| `tower_wall_wizard` | wood:60, iron:30, crystals:70 | MAGICAL | crystals + iron (drop wood) |
| `tower_arcane_spire` | wood:40, iron:40, crystals:85 | MAGICAL | crystals + iron (drop wood) |
| `tower_siege_tower` | wood:160, iron:90, crystals:20 | REGULAR (mechanical) | wood + iron (drop crystals:20) |
| `tower_healer` | wood:110, iron:70, food:40, crystals:30 | ⚠ OWNER PIN — is healing magical? | if magical: crystals+iron(+food); if regular: drop crystals |
| `healing_caravan` | wood:150, iron:100, food:60, crystals:40 | ⚠ OWNER PIN — same question | same rule as tower_healer |
| `jeweler` | wood:50, iron:40, crystals:30 | ⚠ OWNER PIN — trade uses crystals as material, but it is a regular shop | recommend REGULAR: wood+iron (drop crystals) |

Also flagged for the pin:
- `arcane-tower` (the magic-upgrades building) costs **wood:60, iron:60 — no crystals at all.** Under
  the ruling it reads MAGICAL and should arguably become crystals+iron. Owner call.
- `mine_crystal` costs wood+iron — correct as-is (a regular mine that *produces* crystals).

All 21 remaining entries already conform (wood/iron ± food only).

## 3. Implementation (mechanical once §4 is pinned)

1. Edit `structures-catalog.json` cost baskets per the pinned table — **BOTH copies**
   (`Assets/Resources/Data/Canonical/` + `Assets/StreamingAssets/Data/Canonical/`), byte-identical,
   **version 15 → 16** in the same edit (the change-bumps-version discipline canon §10.3 wants).
2. Rebalance totals when dropping a component so the basket's rough value holds (e.g.
   `tower_arcane_spire` wood:40 folds into iron, not vanishes) — keep first-cost feel unchanged;
   numbers are owner-tunable afterward.
3. **New regression case** in the build-economy suite: parse the live catalog, FAIL on any entry whose
   basket contains wood AND iron AND crystals; additionally FAIL on crystals in any entry not on the
   pinned MAGICAL list (the list lives in the regression as the ruling's registry, dated + cited).
4. WO-911 v37 paid-basket + refund flow are unaffected (refunds return what was PAID, flat — basket
   composition is orthogonal). First-build FREE + timer grace unaffected.

## 4. Owner pins needed

1. Arcane pairing: crystals + **iron** (recommended — mineral flavor) or crystals + **wood**?
2. `tower_healer` / `healing_caravan`: magical or regular?
3. `jeweler`: recommend regular (wood+iron); confirm?
4. `arcane-tower`: move to crystal-based, or keep wood+iron as the one "mundane school of magic" shop?

## 5. What NOT to touch

- Affinity/harvest math, Echo systems (WO-811 lane is live), the crystal SINKS (instant-finish,
  queue-slot pricing — WO-911 rulings), Gold/Coins, pack pricing.
