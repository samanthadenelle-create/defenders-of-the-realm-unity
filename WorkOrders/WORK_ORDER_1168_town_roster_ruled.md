# WORK ORDER 1168 — THE TOWN ROSTER, RULED: twelve non-defensive buildings, and what each one is for

**Status:** PARTIALLY LANDED 2026-08-24 — steps 1, 2 and 5 done; ⛔ **step 4 OVERTURNED the same day** (see §0); steps 3 and 6 still open.

## 0. ⛔ CORRECTION — §4 (Cathedral as crystal producer) IS CIRCULAR AND WAS REVERSED

Ruled in the morning, overturned by evening (`936da0c3b`), and the reason is arithmetic, not preference:

```
Cathedral of Magic   costs 240 CRYSTALS   <- the proposed crystal producer
Crystal Mine         costs 320 wood + 200 iron, ZERO crystals
```

**You would need crystals to build the thing that makes crystals.** `mine_crystal` is the only row that can open the faucet from a standing start, and it was simply locked out of the palette — the same defect as the iron producer, on the resource that matters most. It is now unlocked, and **the Cathedral keeps spells and magic research but NOT the faucet**.

⚠ The table and prose below still describe the original §4. They are left as written because this ticket is the record of a ruling, and the correction belongs beside it rather than erasing it — but §4 is dead. Owner trigger: *"the only crystal faucet is way past when you need them, by then you are onto dungeons and raids"*.

**What landed:** step 1 (iron unlock + rename, `96b43f89c`) · step 2 (roles, `a9134a567`) · step 5 (palette groups, WO-1167). **Still open:** step 3 (Smithy merge) · step 6 (WO-1163 renames).

**Minted:** 2026-08-24 (CLI), banner bumped 1168 → 1169 in the same edit.
**Provenance:** owner, 2026-08-24 — *"let's go ahead and iron out what's supposed to be there so we
have that list"* · *"we should have three producers three collectors and then the storefront and I
don't know, I think arcane tower doesn't fit any of those"* · *"Cathedral, magic magic, and
unlocking the spells"*. Three rulings taken in the same sitting (§5).

⭐ **THIS TICKET IS THE ANSWER TO "WHAT SHOULD BE IN TOWN", not "what is in town."** Every prior
list in this repo enumerated the catalog. This one is the intended roster, and the catalog is now
measured against it.

---

## 1. The roster — 12 buildings

| # | Building | catalog id | Verb | Δ |
|---|---|---|---|---|
| **PRODUCERS — the nodes Echoes harvest from** |
| 1 | Lumber Mill | `collector_lumbermill` | wood node | — |
| 2 | Quarry | `collector_farm` | stone node | rename (WO-1163) |
| 3 | **Iron Mine** | `collector_forge` | iron node | ⛔ **unlock + rename** |
| 4 | **Cathedral of Magic** | `arcane-tower` | crystals + magic research + **spell unlocks** | absorb `mine_crystal` |
| **STORAGE — capacity ceilings above the 1k base** |
| 5 | Lumberyard | `lumberyard` | wood ceiling | — |
| 6 | Stone Yard | `silo` | stone ceiling | rename (WO-1163) |
| 7 | Foundry | `foundry` | iron ceiling | — |
| **COMMERCE** |
| 8 | Store | `market` | the ONE storefront | — |
| **CRAFT** |
| 9 | **Smithy** | `forge` (survivor) | weapons + armor + jewelry, tabbed | ⛔ **merge 3 → 1** |
| 10 | Crafting Station | `workshop` | crafting + misc; absorbed the potions shop | — |
| **CIVIC** |
| 11 | Barracks | `barracks` | troop training — pure gold sink (WO-1163) | — |
| 12 | Echo Hollow | `pet-house` | Echo home + wardrobe (WO-1166) | — |

**Retired from the PALETTE, rows KEPT:** `mine_crystal`, `armorer`, `jeweler`, `mill`, `lumbermill`.

## 2. ⛔ THE CONSTRAINT THAT GOVERNS EVERY LINE ABOVE: ids are FROZEN SAVE KEYS

`everBuiltStructureIds`, `BaseLayout`, `vendors.json` and `dialogues.json` all **join on catalog
id**. So:

> **Retiring a building means FILTERING IT OUT OF THE PALETTE. It never means deleting its row.**

A town that already placed an Armorer must still load after the Smithy merge. `BaseLayoutLoader`
resolves through `CatalogRegistry` and **never consults `lockedIds`** — which is exactly why palette
filtering is safe and row deletion is not. **Do not "clean up" the retired rows.**

⚠ The same applies to `mine_crystal`: the Cathedral absorbs its *verb*, not its id.

## 3. What was actually wrong, measured against the roster

**The 3+3 grid already exists in data.** It reads as broken for two reasons, both small:

1. ⛔ **`collector_forge` — the iron node — is LOCKED** (`build-categories.json` Town `lockedIds`).
   **This is the whole "Iron — NEEDS: Forge" dead end the owner hit in play:** the game names a
   building the palette will not offer. Iron has no town faucet at all today.
2. **It is also called "Forge", which is the Weaponsmith's name.** Four ids answering to two words
   (WO-1161). Unlocking without renaming ships two buildings called Forge.

## 4. Why the Cathedral survived the cut, and what it must NOT become

The owner's instinct was right — it is not a producer, a store or a shop. Read at source:
`behaviorId: GameplayBuilding`, `singleton: true`, baked twin `ArcaneTower_MagicUpgrades`, cost
**240 iron + 240 crystals**, footprint **8.4 — the largest in town**.

⚠ **And "research building" alone would have COLLIDED with the owner's own 2026-08-23 design** —
*"lumber mill - upgrade facility: 1 capacity, 2 unlock next attack defense perk, 3 research
unlock"*. If every producer owns its research ladder, a generic research building teaches the same
verb twice.

**The ruling gives it a verb nothing else has: SPELLS.** *"Cathedral, magic magic, and unlocking the
spells."* Plus the crystal line, which was sitting locked and sourceless in `mine_crystal`
(`behaviorId: CrystalMine`, maxLevel 3 — a real, working producer nobody could reach).

So: **producers own their own perks/research; the Cathedral owns SPELLS and crystals.** Two ladders,
two homes, no overlap. ⛔ Do not move per-producer perks into the Cathedral.

## 5. The three rulings, verbatim

| Q | Ruling |
|---|---|
| Cathedral's job | **Crystal producer + magic research + spell unlocks.** Absorbs `mine_crystal`'s verb. |
| Three crafters or one | **MERGE into one Smithy**, tabbed. Kills the crossed-naming cluster and frees two slots. |
| Iron node | **Unlock AND rename to Iron Mine.** Renaming is not optional — it is what stops two Forges. |

## 6. Order of work

1. **Unlock + rename the iron node** — smallest change, fixes a live dead end the owner hit.
2. Fill the **six missing `role` values** (WO-1167 §4) using this roster's groups.
3. **Smithy merge** — pick `forge` as survivor, retire `armorer`/`jeweler` from the palette, tab the
   UI. ⚠ Check `vendors.json`: it holds exactly four vendors and three of them are merging.
4. **Cathedral** — absorb the crystal producer verb; retire `mine_crystal` from the palette.
5. WO-1167 palette groups, now that every row has a role.
6. WO-1163 renames (Farm→Quarry, Silo→Stone Yard) with the pack copy in the SAME change (WO-1165 §7).

## 7. Acceptance

- [ ] Palette offers exactly these 12; no id deleted from the catalog
- [ ] A save containing a placed `armorer` / `jeweler` / `mine_crystal` still loads and sells back
- [ ] Iron is producible in town; no two buildings share a display name
- [ ] `vendors.json` consistent with the merge
- [ ] `REGRESSION_OK` + palette PNG opened (layout is not provable by compile)
