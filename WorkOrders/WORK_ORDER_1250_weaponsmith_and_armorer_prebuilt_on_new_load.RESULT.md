# WO-1250 RESULT — Weaponsmith and Armorer pre-built on a new load

**Status:** IMPLEMENTED (not committed; Unity gate not run — task forbade Unity)
**Silo:** Village / Buildings + save state
**Ids (catalog, not display names):**
- Weaponsmith = `forge` (displayName "Weaponsmith", role `weaponsmith`, art `Structures/Forge`)
- Armorer = `armorer` (displayName "Armorer", role `armorer`, art `Structures/armorer`)

Do **not** confuse with `workshop` (Crafting Station) or `collector_forge` (Iron Mine).

---

## What a brand-new save SHOULD own

From existing blank-town canon (WO-834 / WO-703 / `ResetToNewGame`), **not** from the scene:

- `StrategicPlacementMigrated = true`
- `EverBuiltStructureIds = []`
- `BaseLayout = []`
- Scene shell only: Heart / well / walls+gates
- **Zero** storefronts. The player places `forge` and `armorer`. They are not founding kit (`FoundingKit` is pet-house, collector_lumbermill, tower_ground_archer).

If `forge` or `armorer` is in `everBuilt` or `MayBakedTwinSurface` is open for them, those two "show as built".

---

## Proving branch (named from the code path the traces now print)

Three cooperating holes, one visible symptom:

### 1. Wrong id on the standdown allow-list (primary)

The 2026-08-19 upright bake skins:

| Scene host | Child mesh | Catalog id that SHOULD own it | Catalog id that DID own it |
|---|---|---|---|
| `Blacksmith_Weapons_Storefront` | `Forge(Clone)` | `forge` (Weaponsmith) | `workshop` (Crafting Station) |
| `Forge_Armor_Storefront` | `armorer(Clone)` | `armorer` (Armorer) | `forge` (Weaponsmith) |

`armorer` authored **no** `bakedTwins`, so `EnforceAll('armorer')` was a no-op. The Armorer visual only hid as a side-effect of standing down `forge`.

The Default-Town / WO-673 writer granted `workshop` + `forge` (the retired pair). That opened `MayBakedTwinSurface` for those ids and **resurfaced the Weaponsmith and Armorer visuals**. That is the owner's "weaponsmith and armorer show as buil on new load".

Trace line after the fix (hub load, blank save):

```
[Flow:Singleton] blank-town 'forge': migrated=True everBuilt=False maySurface=False twins=[Blacksmith_Weapons_Storefront] → Suppressed
[Flow:Singleton] blank-town 'armorer': migrated=True everBuilt=False maySurface=False twins=[Forge_Armor_Storefront] → Suppressed
[Flow:Singleton] StandDownBakedTwins('forge'): baked twin 'Blacksmith_Weapons_Storefront' stood down - 'forge' never player-built on this save (blank-town gate, WO-834).
[Flow:Singleton] StandDownBakedTwins('armorer'): baked twin 'Forge_Armor_Storefront' stood down - 'armorer' never player-built on this save (blank-town gate, WO-834).
```

If either `maySurface=True` or `twins=[<none>]` on a new load, that is the branch.

### 2. Missing-save boot treated as a legacy pre-v30 town

`GameState.StrategicPlacementMigrated` defaults **false**. `Load()` with no PlayerPrefs key left that default. First hub load then ran `StrategicPlacementMigration.RunIfNeeded`, wrote BaseLayout records for every BakedRows id, and granted them ever-built. Palette `IsPlayerBuilt` went true (records exist) **and** the bake stayed up for that session (WO-673 latch).

`ResetToNewGame` already set the marker true. First APK launch / hub reach **without** Title "Start New" did not.

New trace:

```
[Flow:Save] brand-new game (no save key present) — WO-1250 blank founding seeded: StrategicPlacementMigrated=true everBuilt=[] BaseLayout=[] (Weaponsmith/Armorer baked twins stay down).
```

### 3. `MayBakedTwinSurface` fail-OPEN when GameState was null

The live overload returned **true** when `GameStateService.State` was null ("preserve pre-WO-834"). `CastleVendorNpcInjector` then called `ResurfaceStorefront` on the bake hosts **before** the save existed. Fail-CLOSED now; `FlowTrace.Once("may-surface-no-state", ...)`.

---

## Fix (no schema bump, no scene edit)

1. **Census + catalog twins remapped** (both JSON copies, byte-identical):
   - `forge.bakedTwins = ["Blacksmith_Weapons_Storefront"]`
   - `armorer.bakedTwins = ["Forge_Armor_Storefront"]`
   - `workshop` no longer authors a twin (Crafting Station has no bake)
   - `StrategicPlacementMigration.BakedRows` matches
2. **`SeedBlankFoundingOnMissingSave`** on `Load()` when the save key is missing or empty.
3. **`MayBakedTwinSurface` live overload fails CLOSED** with no save.
4. **Legacy Default-Town self-heal** (no migrator): a save that still carries the old `workshop`+`forge` pair and has never built `armorer` gets `MarkEverBuilt("armorer")` so existing Default Towns keep the Armorer visual. Traced. Not a schema bump.
5. **`CatalogFallbackData.g.cs` regenerated** from the catalog (SHA `c0cd487ab76217770787fa3ac4cc1e516e12048f78ec0f5ec3cd09344768703e`, 95311 bytes) so `[fallback-parity]` stays honest.

Instrumentation stays in (`TraceBlankTownDecision` per singleton per hub load; `StandDownBakedTwins` logs stood / already-inactive / absent / no-twins).

---

## Regression (RED shape)

- `BlankTownGateTests`: brand-new save does not surface `forge`/`armorer`; seeding them into everBuilt **would** open the gate (the assertion is live); BakedRows maps the two hosts to `forge`/`armorer` not the retired crossing.
- `DataRegression.CheckBlankTownGate` WO-1250 block: same pins + catalog twin authors. If the catalog cannot be resolved → `RegressionOutcome.PartialSkip`, never quiet green. Lint for `SeedBlankFoundingOnMissingSave`.
- `BlankStartCensusRegression` section 2 inverted from stale Lever-1 "must PRE-STAND" to WO-834 "must STAND DOWN". Absent bake host → PartialSkip, not a green "stood down".

A revert of the census mapping (workshop→Blacksmith / forge→Forge_Armor) **fails** those asserts. That is the bug.

---

## What was NOT touched

- `SaveSchema.CurrentVersion` (still 41)
- `.unity` scenes
- `RepoProps.MaxStructureLevel`
- Founders Monument / `FeatureFlags.FoundersMonument`
- WO **Status** line
- No commit

## Gates

Brace-balanced + no NULs on every edited `.cs`. `COMPILE_GATE_OK` / `REGRESSION_OK` **not run** (task: do not fire Unity). Orchestrator batch-gates.

## Owner felt-verify

Fresh save on device: town is tree/well/walls; Weaponsmith and Armorer are **not** standing; their build cards are not "Built".
