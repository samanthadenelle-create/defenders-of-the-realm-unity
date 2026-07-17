# WORK ORDER 734 — Barracks Tier Copy Announces Unit Unlocks

**Status:** READY TO IMPLEMENT  
**Priority:** P1  
**Silo:** Data / Progression copy  
**Depends on:** WO-732 (roster ids stable)  
**Blocks:** WO-736 (felt copy QA)  
**Program:** `WORK_ORDER_PROGRAM_732_736_barracks_troop_roster.md`  
**Effort:** S–M  
**Audience:** Claude + CLI  

---

## Goal

When the player upgrades the Barracks, the **upgrade panel text** clearly says which **new troop type** unlocks (CoC: “unlocks Giant” energy). Stat mults stay; unlock is the **headline** of each tier where a unit opens.

---

## Current state (verified)

`Assets/StreamingAssets/Data/Canonical/building-tiers.json` — building id `"barracks"`, 6 tiers:

| Tier | Name today | Effect today | Unit unlock (target) |
|------|------------|--------------|----------------------|
| 1 | Muster the Barracks | Opens recruitment drills | Footman + Archer (defaults) |
| 2 | Drill Yard | Troop health +8% | **+ Spearman** |
| 3 | War College | Troop damage +12%, health +10% | **+ Shieldguard** (+ existing army-cap perk track) |
| 4 | Standing Army | Troop damage +18%, health +18% | **+ Outrider** |
| 5 | Warhost | Troop damage +26%, health +26% | **+ Battlemage** |
| 6 | Legion of Elarion | Troop damage +38%, health +38% | **+ Echo Legionnaire** |

Perks (T1–T3 gold research) remain **stat/capacity** side-track — do not replace them with unit unlocks.

---

## Deliverables

### 1. Dual-copy `building-tiers.json`

Edit barracks tier **effect** (and optionally **name** if clearer) so unlock is first-class. Example pattern:

```json
{
  "tier": 2,
  "name": "Drill Yard",
  "effect": "Unlocks Spearman. Troop health +8%",
  ...
}
```

```json
{
  "tier": 3,
  "name": "War College",
  "effect": "Unlocks Shieldguard. Troop damage +12%, health +10%",
  ...
}
```

Continue through T6 with the program unit names.  
T1: e.g. `"Opens recruitment — Footman and Archer available"`.

**Do not change** costWood/costFood/costCrystal/requiresVillageTier/modifiers numbers unless a bug forces it — this WO is **copy + clarity**, not rebalance.

### 2. Optional data hook (only if cheap)

If the upgrade UI only shows `effect` string, JSON copy is enough.  
If a structured field is easy and already patterned elsewhere, you may add:

```json
"unlocksTroopIds": ["troop-spearman"]
```

Only if `BuildingTierDef` + catalog loader support extension without breaking DataRegression. **Not required** if copy-only path works.

### 3. Dialogue beat (light)

In barracks dialogue (`dialogues.json` structure id `barracks`), add **one** Drillmaster line that teaches the ladder, e.g.:

> "Footmen and archers today. Grow this yard and I'll train spears, shields, riders — even the Legion."

Keep short; no multi-node quest. Dual-copy dialogue if Resources mirror exists.

### 4. Dual-copy rule

- `Assets/StreamingAssets/Data/Canonical/building-tiers.json`  
- `Assets/Resources/Data/Canonical/building-tiers.json` (if present — **must** mirror)

Same for dialogue if mirrored.

---

## Tasks

1. Read barracks block + how `BuildingUpgradePanelMvvm` / VM surfaces `effect`.  
2. Rewrite effects for T1–T6 with unlock names.  
3. Dual-copy.  
4. Optional dialogue line.  
5. RESULT lists before/after effect strings.

---

## Acceptance

- [ ] Upgrade panel for Barracks T2+ clearly names the newly unlocked troop.  
- [ ] T1 names Footman + Archer as available.  
- [ ] Stat mult modifiers **unchanged** numerically.  
- [ ] StreamingAssets + Resources in sync.  
- [ ] No compile break if only JSON changed.

---

## Not in scope

- Implementing unlock math (WO-733).  
- Changing gold perk icons/art.  
- Repricing tiers.

---

## Key files

| Action | Path |
|--------|------|
| EDIT | `Assets/StreamingAssets/Data/Canonical/building-tiers.json` |
| EDIT | `Assets/Resources/Data/Canonical/building-tiers.json` (mirror) |
| MAY EDIT | `Assets/StreamingAssets/Data/Canonical/dialogue/dialogues.json` (+ Resources mirror) |
| READ | `BuildingUpgradeVM.cs`, `BuildingTierCatalog.cs` |

---

## Claude implementation notes

- Player-facing names match program table exactly (Shieldguard not “Shield Guard” unless owner renames later).  
- Canon: **Elarion**.  
- If Resources file missing for building-tiers, create mirror from StreamingAssets (CanonicalJson law).

---

## RESULT

`WorkOrders/WORK_ORDER_734_barracks_tier_unlock_copy.RESULT.md`
