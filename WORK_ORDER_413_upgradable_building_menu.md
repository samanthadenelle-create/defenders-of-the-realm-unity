# WORK ORDER 413 — Upgradable buildings wrongly offer the shop menu

**Priority:** P1
**Status:** READY TO IMPLEMENT
**Lane:** 6 — Economy / Progression
**Filed:** 2026-06-11 (owner, via Notion). Canon rule saved to memory: `isupgradable-isshoppable-building-rule`.
**Related:** WO-411 #9 ("Talk: <building>" prompt) · buildings-collection capability model (ARCHITECTURE §2b).

---

## Problem
Interacting with an **upgradable** building (windmill, lumbermill, armorer, forge) wrongly
offers a **shop (Buy/Sell)** menu. Classification is currently **hardcoded by building
type + id-string matching**, not data (verified — see Root cause).

## The rule (owner canon — data-driven, per building, NOT name-matching)
- **`isUpgradable`** building (windmill, lumbermill, armorer, forge — per the Forgemasters
  doc) → menu = **Upgrade · Add Perks · Talk · Leave** (NO Buy/Sell).
- **`isShoppable`** building → menu = **Buy · Sell · Talk · Leave**.
- A building's class is a **data property on its catalog entry**, read by the interaction
  layer — never decided by `BuildingType` switch or id-substring matching.

## Root cause (verified via capability-model audit)
The generic Building model does **not** follow the §2b capability-on-entry pattern:
- `Building.cs` exposes Type/Id/Hp/Cost/Level — **no capability flags.**
- `BuildingDef` / `buildings.json` carry art/placement/cost/order — **no capability flags.**
- `BuildingInteractable.cs` decides everything by **hardcode**:
  - Upgradable: `BuildingType` switch (lines ~282–300) + id-string fallback (lines ~304–314).
  - Storefront/vendor: id-string matching in `StructureHookIdFor()` (lines ~251–275).
  - Interactable: just component presence (every building shows a prompt).

## Implementation
1. **Add capability flags to `BuildingDef` + `buildings.json`** (data): at minimum
   `isUpgradable`, `isShoppable` (extend toward the §2b set — `isInteractable`, etc. — as the
   buildings-collection model formalizes). Cover the §2c test: extend `BuildingCatalogTest`
   to assert the flags hydrate.
2. **`BuildingInteractable` reads the flags** — replace the `BuildingType` switch + id-string
   matching with `def.isUpgradable` / `def.isShoppable`. Build the menu from the flags:
   upgradable → Upgrade/Add Perks/Talk/Leave; shoppable → Buy/Sell/Talk/Leave.
3. **Panels-exist check (owner directive):** if the Upgrade / Add-Perks panels don't exist
   yet, **wire the menu correctly now and LOG A FOLLOW-UP WO for the panels — do NOT ship
   dead buttons** (gate or stub-with-toast, not silent no-ops).
4. Talk routes to the building's NPC/vendor dialogue (reconcile with WO-411 #9 — buildings
   should not show a bare "Talk: <building>" prompt).

## Implemented 2026-06-11 (live, editor-compiled)
- `BuildingDef.IsUpgradable` / `IsShoppable` added (+ both buildings.json copies: crystal-mine/farm/
  lumbermill/forge = upgradable). `BuildingCatalogTest` asserts the classification (§2c gate).
- `BuildingInteractable.Interact` now routes an `IsUpgradable` building to its **upgrade panel
  first** — never the Buy/Sell shop dialogue. Data-driven; the old type/id matching is bypassed for
  these. (No `market`/shop building exists in this catalog, so nothing is flagged shoppable yet.)
- **FOLLOW-UP NEEDED (log as a new WO):** the upgrade flow currently opens the existing
  `BuildingUpgrade` panel (Upgrade only). The full menu — **Add Perks · Talk · Leave** — is NOT built
  yet; wire those when the perk/talk UI exists. Not shipped as dead buttons (per the directive).

## What NOT to touch
- No name/type matching for classification — flags only (the whole point).
- Reconcile additively with existing `BuildingUpgradePanel` / vendor dialogue; don't greenfield.
