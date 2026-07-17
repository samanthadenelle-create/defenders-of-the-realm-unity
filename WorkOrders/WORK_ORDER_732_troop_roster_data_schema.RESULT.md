# WORK ORDER 732 — RESULT

**Status:** IMPLEMENTED (not gated/committed — orchestrator batch-gates per CLAUDE.md §11)
**Date:** 2026-07-16
**Program:** `WORK_ORDER_PROGRAM_732_736_barracks_troop_roster.md`

---

## 1. Schema — `TroopDef.cs`

File: `Assets/_Modules/Village/Troops/TroopDef.cs` — 3 additive fields appended after `buildSeconds` (lines ~71-80, block "Barracks progression (WO-732)"):

```csharp
[JsonProperty("unlockBarracksTier")] public int UnlockBarracksTier = 1;
[JsonProperty("shortDescription")]   public string ShortDescription;
[JsonProperty("iconId")]             public string IconId;
```

- Exact JSON property names as specified. Additive; safe defaults (`UnlockBarracksTier = 1`, strings null) so pre-existing troops.json without the fields still hydrate as day-one troops. No existing field touched.
- Brace balance: **open=2 / close=2**. NUL bytes: **0**.

## 2. Roster — `troops.json` (7 ids, `version` bumped 1 -> 2)

All stats/costs copied verbatim from the authoritative program economy table (lines 40-48) + product table (lines 28-36). `_comment` rewritten to document `unlockBarracksTier` semantics + the BINDING dual-copy rule.

| id | tier | model | modelYaw | slots | maxHp | dmg | cd | range | move | hunt | wood | iron | food | build |
|----|------|-------|----------|-------|-------|-----|----|-------|------|------|------|------|------|-------|
| troop-footman | 1 | SC_Footman | 0 | 1 | 100 | 12 | 1.0 | 2.5 | 4.0 | 14 | 40 | 10 | 5 | 30 |
| troop-archer | 1 | SC_Archer | 0 | 1 | 60 | 29 | 1.2 | 14 | 4.0 | 18 | 30 | 20 | 5 | 45 |
| troop-spearman | 2 | SC_Footman | 0 | 1 | 90 | 16 | 1.1 | 3.5 | 4.0 | 15 | 50 | 25 | 10 | 50 |
| troop-shieldguard | 3 | Knight | -90 | 2 | 180 | 10 | 1.3 | 2.2 | 3.2 | 12 | 60 | 40 | 15 | 70 |
| troop-outrider | 4 | Ranger | -90 | 2 | 95 | 18 | 0.9 | 2.5 | 5.5 | 16 | 80 | 50 | 20 | 90 |
| troop-battlemage | 5 | Mage | -90 | 2 | 55 | 42 | 1.8 | 16 | 3.5 | 20 | 40 | 80 | 25 | 100 |
| troop-echo-legionnaire | 6 | Knight | -90 | 3 | 160 | 28 | 1.0 | 2.8 | 4.2 | 16 | 100 | 100 | 40 | 150 |

Every value matches the program table exactly. Display names, roles, and slots match the product table. `element: "None"` on all (Step-1 starter convention, carried from existing entries).

### modelYaw choices
- `SC_*` (footman/archer/spearman) -> `0` (Supercyan humanoids face +Z).
- `Knight` / `Ranger` / `Mage` (shieldguard/outrider/battlemage/echo-legionnaire) -> `-90` (Tripo/AccuRIG bodies face +X), matching the TroopDef doc-comment convention and program line 62.
- **Shieldguard model:** chose **`Knight`** over `SC_Footman` for the larger/tankier silhouette read the program table calls for ("prefer larger/tanker read").
- **Echo Legionnaire model:** `Knight` (elite read, per table).

### Model availability note (for WO-735)
`SC_Footman.prefab`, `SC_Archer.prefab`, `Knight.fbx` exist under `Assets/Resources/Heroes/`. **`Ranger` and `Mage` have only `.controller` + `.tripo-extracted` stubs (no loadable mesh yet)** — `TroopFactory.Build` will log a warning and fall back to a tinted capsule for outrider/battlemage until real art lands. This is expected day-one placeholder behavior and is WO-735's scope, not a WO-732 blocker.

## 3. Dual-copy verification (BINDING)

Both paths written byte-identical:
- `Assets/StreamingAssets/Data/Canonical/troops.json`
- `Assets/Resources/Data/Canonical/troops.json`

**md5 (both): `e0020baf988bbd0fa2354aca26537bdf`** — MATCH.

## 4. Data regression — DEFERRED to WO-736

Grepped `Assets/Editor/Regression/*` and all editor code: **no existing troop/TroopCatalog regression** (no file references `TroopCatalog` or `troops.json`). Per WO instruction, did **not** stand up a new framework — **WO-736 owns the full troop-roster oracle** (catalog >=7, exact 7 ids, footman/archer tier==1, legionnaire tier==6, no dup ids). The 7 target assertions are pre-verified by hand below.

## 5. Stability / deviation note (needs PO awareness)

- Footman/Archer **ids unchanged** (saves reference `PlayerTroop.TroopDefId`). Stats are not save-serialized.
- **One value aligned to the authoritative table:** existing footman/archer had `costFood: 0`; the program economy table (authoritative, "do not invent costs") specifies **food = 5** for both. Aligned to `5` so the JSON is table-conformant and internally consistent. This is a tuning-value change only (not save-referenced) and PO may retune. Flagged here for visibility — revert to `0` if PO prefers the pre-table value.

## Verification summary

| Check | Result |
|-------|--------|
| JSON valid, version==2, 7 troops, 0 dup ids | PASS |
| tiers 1,1,2,3,4,5,6 | PASS |
| footman/archer tier==1; legionnaire tier==6 | PASS |
| stats/costs == program table | PASS |
| StreamingAssets == Resources (md5) | PASS (`e0020baf...`) |
| TroopDef.cs braces open==close (2/2), NUL=0 | PASS |

## Not gated/committed
Per CLAUDE.md §11 the orchestrator batch-gates (`COMPILE_GATE_OK`) and commits by explicit path. Files changed:
- `Assets/_Modules/Village/Troops/TroopDef.cs`
- `Assets/StreamingAssets/Data/Canonical/troops.json`
- `Assets/Resources/Data/Canonical/troops.json`
