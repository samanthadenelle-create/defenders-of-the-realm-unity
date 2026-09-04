# PROD-020 — Trade Build collection shows Store only; Weaponsmith + Armorer stuck under Crafting

**Status:** CLOSED 2026-09-04 - owner felt-test PASS (validated 2026-09-04T14:37:29, build 2026.09.04.354315). PRIOR STATUS: FIXED — awaiting clean-build + Seeker felt verification (2026-08-30)
**Minted:** 2026-08-29 (CLI seat) — banner bumped with PROD-018/019 → PROD-021  
**Priority:** HIGH — wrong shelf teaches the wrong town model  
**Provenance:** owner, 2026-08-29: *"Trade: broken—the route exposes only Store, not Armorer and Weaponsmith. those still show under crafting."*  
**Related:** WO-1254 notes the same Trade gap on Bag/nav; **this PROD fixes Build Collections membership** (the live Build → Trade / Crafting cards). Do not invent a second menu authority.

---

## 1. Defect

Build → **Trade** → only **Store** (`market`).  
**Weaponsmith** (`forge`, Borin) and **Armorer** (`armorer`, Halvard) appear under **Crafting** instead.

Vendor talk is fine (`OpenShop forge` / `OpenShop armorer`). Town `build-categories.json` Trade group already lists weaponsmith + armorer. **Build Collections JSON disagrees.**

---

## 2. Root cause (data, verified)

Dual-copy `card-collections.json`:

| Collection | Today | Should be |
|---|---|---|
| `build-trade` | `market` only | `market`, `forge`, `armorer` |
| `build-crafting` | `workshop`, `forge`, `armorer`, `jeweler` | `workshop`, `jeweler` only |

Cite: `Assets/StreamingAssets/Data/Canonical/card-collections.json` (and Resources mirror) — Crafting ~items forge/armorer; Trade ~market only.  
Canon roles in `structures-catalog.json`: forge = Weaponsmith shop, armorer = Armorer shop, workshop = Crafting Station, jeweler stays Crafting (owner pin on WO-1254).

---

## 3. Fix

1. Edit **both** copies of `card-collections.json` (byte-identical):
   - **Trade** items: `market` order 10, `forge` 20, `armorer` 30  
   - **Crafting** items: `workshop` 10, `jeweler` 20 — **remove** forge + armorer  
2. Optional: broaden Trade subtitle beyond “realm store only” so it reads as shops, not Coppin-only.  
3. Regression: Trade contains `{market,forge,armorer}` excludes `{workshop,jeweler}`; Crafting contains `{workshop,jeweler}` excludes `{forge,armorer,market}`. Extend `BuildCollectionPlayerRegression` or a tiny membership case.

**Do not touch:** dialogues, `OpenShop`, `vendors.json`, PartyShop, Manage tabs, NPC seating.

**Pin (separate):** Town `build-categories.json` Trade still lists `jeweler` + `crafting_station` — different UI; **out of scope** unless owner expands.

---

## 4. Acceptance

1. Build → Trade: **Store, Weaponsmith, Armorer** visible (authored order).  
2. Build → Crafting: **Crafting Station + Jeweler** only — no Weaponsmith/Armorer.  
3. Talk to Borin / Halvard still opens their shops unchanged.  
4. Dual-copy match; regression green; `COMPILE_GATE_OK` if any C# touched (data-only preferred).

## 5. Not in scope

Bag Market tab redesign, Jeweler unlock FTUE, aligning Town paletteGroups, PROD-018 browse restore.
