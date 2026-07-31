# WO-808 — Hero weapon / armor power levels (Forge & Armorer ladder)

**Status:** READY TO IMPLEMENT (Claude designs first; CLI after owner sign-off)  
**Minted:** 2026-07-30  
**Program:** `docs/WC3_COC_EXPERIENCE_ANALYSIS.md` §2A  
**Lane:** Gear / hero progression (Forge + Armorer + Inventory)  
**Roles:** Claude = READ-ONLY product + UI; CLI = data + service + UI after sign-off  

## Why
**Troops** have a real type-level ladder (Lab analog). **Hero gear** today is mostly **buy/equip + rarity presentation** — no first-class “raise this sword to L4” path. CoC Heroes and many ARPGs use equipment levels for power fantasy; design copy (Forge/Armorer guide) promises upgrades, but there is no single owned ladder wired like troop L.

## Code baseline (truth)

| Exists | Gap |
|--------|-----|
| `weapons.json` / `armor.json` + Shop buy/equip | No persistent `itemInstance.level` upgrade job |
| Inventory rarity frames | Rarity ≠ power level |
| Forge/Armorer as vendors | Shop, not smith-level queue |
| Hero level / XP / skill tree | Orthogonal to gear levels |
| Troop L on Research channel | Pattern to **mirror**, not share ids |

## Scope

### Claude (read-only)
1. Product decision board for owner:
   - **A.** Instance levels on owned gear (reforge same item)  
   - **B.** Tier swaps (consume item → next catalog tier)  
   - **C.** Rarity-only (document “no gear levels”; power = find better drops) — only if rejecting levels  
   CLI lean: **A** (CoC/ARPG feel) with max L and soft cost curve.  
2. Where UI lives: Forge panel “Improve weapon” / Armorer “Improve armor” + Inventory “Improve” CTA.  
3. Power readout: damage/defense before→after; optional “Hero combat power” contribution.  
4. Economy: resources only for V1 (no premium required).  
5. Image pairs for chosen option.  

### CLI (after sign-off of A or B)
1. Persist instance level (save field / gear inventory extension — schema bump only if required; document version).  
2. Cost/time curve data file (dual-copy canonical JSON).  
3. Optional timed job on Research or a **Smith** channel — prefer **reuse Research or Builder** only if owner agrees; default lean: **instant or short Research job** to avoid fourth channel.  
4. Combat: hero damage/defense reads level multipliers (one resolver, pure, testable).  
5. Oracles: level clamp, cost monotonic, save round-trip.  

## Acceptance
- [ ] Owner chose A/B/C  
- [ ] If A/B: player can improve equipped weapon/armor and feel stronger in hub combat  
- [ ] Inventory shows level  
- [ ] Felt on Seeker  

## Do NOT
- Merge troop L with gear L into one confusing bar  
- Crypto / SKR pricing  
- Full crafting minigame  
- Break equip loadouts  

## Files (expected)
- Gear inventory state, Shop/Inventory UI, hero combat stat apply path, new catalog JSON  
