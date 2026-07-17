# WORK ORDER 732 — Troop Roster Data + `unlockBarracksTier` Schema

**Status:** READY TO IMPLEMENT  
**Priority:** P0 (content spine for barracks progression)  
**Silo:** Data / Core catalog  
**Depends on:** —  
**Blocks:** 733, 734, 735, 736  
**Program:** `WORK_ORDER_PROGRAM_732_736_barracks_troop_roster.md`  
**Related:** WO-724 (Barracks live), CoC program 723–731  
**Effort:** M  
**Audience:** Claude + CLI  

---

## Goal

Author the **full 7-troop collection** in canonical JSON and extend `TroopDef` so each type declares **which Barracks building tier unlocks it**. No UI work in this WO (except compile-safe model fields).

---

## Current state (verified)

| Item | Reality |
|------|---------|
| Catalog | `Assets/StreamingAssets/Data/Canonical/troops.json` (+ Resources mirror) |
| Types | Only `troop-footman`, `troop-archer` |
| Loader | `TroopCatalog` via `CanonicalJson` |
| Typed def | `Assets/_Modules/Village/Troops/TroopDef.cs` — **no unlock field** |
| Training UI | Lists **all** `TroopCatalog.All` (no filter) |

---

## Deliverables

### 1. Schema — `TroopDef` + JSON

Add field (name exact):

```csharp
/// <summary>
/// Minimum Barracks building tier required to train this troop.
/// 1 = default/day-one. Compared to ModifierService.TierOf("barracks")
/// (0 if barracks never upgraded — treat as tier 1 once barracks exists;
/// see WO-733 for the exact tier resolution rule).
/// </summary>
[JsonProperty("unlockBarracksTier")] public int UnlockBarracksTier = 1;
```

Optional but recommended for UI (WO-733/735):

```csharp
[JsonProperty("shortDescription")] public string ShortDescription; // one line for detail pane
[JsonProperty("iconId")] public string IconId; // Resources icon key; may be empty day-one
```

JSON property names: camelCase `unlockBarracksTier`, `shortDescription`, `iconId`.

### 2. Full `troops.json` content

Replace/extend `troops` array to include **exactly** these 7 ids (stable forever — do not rename after ship):

1. `troop-footman` — unlock 1  
2. `troop-archer` — unlock 1  
3. `troop-spearman` — unlock 2  
4. `troop-shieldguard` — unlock 3  
5. `troop-outrider` — unlock 4  
6. `troop-battlemage` — unlock 5  
7. `troop-echo-legionnaire` — unlock 6  

**Copy stats/costs from the program table** in `WORK_ORDER_PROGRAM_732_736_barracks_troop_roster.md` (authoritative).  
Bump `"version"` to **2**.  
Update `_comment` to document unlock field + dual-copy.

### 3. Dual-copy (BINDING)

Write **identical** content to:

- `Assets/StreamingAssets/Data/Canonical/troops.json`  
- `Assets/Resources/Data/Canonical/troops.json`  

WebGL loads Resources first — a single-side edit = silent wrong roster in builds.

### 4. Loader / model safety

- `TroopCatalog.Find` / `All` continue to return all defs (including locked) — **filtering is UI/train (WO-733)**.  
- Missing optional fields default safely (`UnlockBarracksTier` → 1).  
- Existing Footman/Archer ids **must not change** (saves with `PlayerTroop.TroopDefId` already reference them).

### 5. DataRegression (minimum in this WO)

Extend or add assertions (can finish hardening in WO-736):

- Catalog loads ≥7 troops.  
- Exactly the 7 ids present.  
- Footman/Archer `unlockBarracksTier == 1`.  
- Legionnaire `unlockBarracksTier == 6`.  
- No duplicate ids.

---

## Tasks (ordered)

1. Read `TroopDef.cs`, `TroopCatalog.cs`, both `troops.json` paths.  
2. Add `UnlockBarracksTier` (+ optional desc/icon) to `TroopDef`.  
3. Author full JSON roster; dual-copy.  
4. Brace check on `.cs`; run DataRegression if already wired for troops — else note “WO-736 owns full oracle.”  
5. Write RESULT with final JSON excerpt + any modelYaw choices.

---

## Acceptance

- [ ] 7 troop defs load in Editor and via `TroopCatalog.All`.  
- [ ] StreamingAssets and Resources JSON **byte-identical** (or proven same content).  
- [ ] Schema version 2; old saves still resolve footman/archer.  
- [ ] No compile errors; no NUL/brace issues.  
- [ ] RESULT documents any deviation from the program table (owner must have approved deviation).

---

## Not in scope

- Training panel lock UI (WO-733).  
- `building-tiers.json` copy (WO-734).  
- New FBX/prefab art beyond existing model keys (WO-735).  
- Flipping `ff.barracks` ON.  
- Deploy/raid loop (WO-726).

---

## Key files

| Action | Path |
|--------|------|
| EDIT | `Assets/_Modules/Village/Troops/TroopDef.cs` |
| EDIT | `Assets/StreamingAssets/Data/Canonical/troops.json` |
| EDIT | `Assets/Resources/Data/Canonical/troops.json` |
| READ | `Assets/_Modules/Village/Troops/TroopCatalog.cs` |
| READ | `Assets/_Modules/Village/Troops/TroopFactory.cs` |
| MAY EDIT | `Assets/Editor/Regression/*` troop/catalog regression if exists |

---

## Claude implementation notes

- Prefer **additive** JSON fields so older tooling does not break.  
- Do **not** hard-code the roster in C# — JSON is source of truth.  
- Display names: player-facing English as in the table (Footman, Archer, Spearman, Shieldguard, Outrider, Battlemage, Echo Legionnaire).  
- Canon village name: **Elarion** (never Avalon).  
- ASCII-only runtime strings if any logs added.

---

## RESULT

`WorkOrders/WORK_ORDER_732_troop_roster_data_schema.RESULT.md`
