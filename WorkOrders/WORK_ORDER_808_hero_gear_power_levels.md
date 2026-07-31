# WO-808 — Hero weapon / armor power levels (Forge & Armorer ladder)

**Status:** READY TO IMPLEMENT (Claude designs UI; CLI implements — model **LOCKED**)  
**Minted:** 2026-07-30  
**Program:** `docs/WC3_COC_EXPERIENCE_ANALYSIS.md` §2A  
**Lane:** Gear / hero progression (Forge + Armorer + Inventory)  
**Roles:** Claude = READ-ONLY UI/flow mockups under locked model; CLI = data + service + combat apply  

## ★ OWNER RULING (2026-07-30) — Option A LOCKED

**Instance levels on owned gear (reforge the same item).**

- Same weapon/armor **instance** stays in inventory / equipped; **level climbs** (L1 → max).  
- **Not** B (consume → next catalog tier).  
- **Not** C (rarity-only; power only from finding better drops).  
- Fantasy: “improve *this* sword,” CoC Heroes / ARPG style.  
- V1 economy: **resources only** (no premium required to level).  
- **Rarity** stays identity/frame; **level** is the power ladder.

## Why
**Troops** already have type-level ladders (`TroopLevels` + `TroopStatResolver`). **Hero gear** is buy/equip + rarity with no instance power climb. Option A mirrors troop L: same item, higher level, stronger stats.

## Code baseline (truth)

| Exists | Gap |
|--------|-----|
| `weapons.json` / `armor.json` + Shop buy/equip | No persistent `itemInstance.level` |
| Inventory rarity frames | Rarity ≠ power level |
| Forge/Armorer as vendors | Shop, not reforge-level loop |
| Hero level / XP / skill tree | Keep **orthogonal** to gear levels |
| Troop L on Research channel | Mirror pattern (level + cost), not shared ids |

## Scope

### Claude (read-only — model fixed to A)
1. UI: Forge “Improve weapon” / Armorer “Improve armor” + Inventory “Improve” on owned gear.  
2. Card: **Lcurrent → Lnext**, damage/defense **before → after**, resource cost, optional short timer.  
3. Propose max L + soft cost curve (numbers retunable).  
4. Image pairs for Improve (equipped + bag).  
5. Copy: “Improve” / “Reforge” — never “Obsidian”; not “troop upgrade.”  

### CLI (implement A)
1. Persist **per-instance level** on owned gear (schema bump only if required; document in RESULT).  
2. Dual-copy `gear-levels.json` (or equivalent): cost/time/mult by slot and/or rarity band.  
3. Improve: spend resources → bump level. Default lean **instant V1** (no new channel); optional short Research job only if owner wants timers.  
4. Pure resolver: catalog def × level multipliers → combat damage/defense.  
5. Inventory + equip UI show `Lv N`.  
6. Oracles: clamp, cost monotonic, save round-trip, combat uses level.  

## Acceptance
- [ ] Improve equipped weapon/armor **in place**; instance identity preserved  
- [ ] Stats scale with level; hub combat feels stronger  
- [ ] Inventory shows level  
- [ ] Save/load keeps levels  
- [ ] Felt on Seeker  

## Do NOT
- Implement B or C without a new owner ruling  
- Merge troop L and gear L into one bar  
- Crypto / SKR pricing  
- Full crafting minigame  
- Break equip loadouts / rarity presentation  

## Files (expected)
- Gear inventory state, Shop/Inventory/Forge UI, hero combat stat path, new dual-copy catalog JSON  
