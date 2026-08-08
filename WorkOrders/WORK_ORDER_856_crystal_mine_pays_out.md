> ## RECONCILED 2026-08-08 - true status is DONE
> Audit `docs/reference/WO_TRUE_STATUS_2026-08-08.md`. Evidence: 9a43e83a; CrystalMine.cs rewritten and buildings.json carries the yield curve [2,4,7].
> The previous Status line read "Status: READY TO IMPLEMENT" and was wrong; the board understated this.

# WORK ORDER 856 — Crystal Mine actually pays out (level authority + authored yield curve)

**Status:** DONE
**Author:** read-only RCA agent (§13 triage), orchestrated by CLI, 2026-08-04
**Classification (§13):** EXISTING — built system, silently dead. NOT a new feature.
**Lane (§9):** Economy / Buildings — code + data only, no `.unity` scene files.
**WO#:** main-line block; banner bumped 856 → 857 in the same edit as this mint.

---

## 1. Goal

The Crystal Mine (`mine_crystal`, 80 Wood + 50 Iron) is the only structure whose job is to relieve the
crystal wall that WO-855 made the real bottleneck. **It has never paid out a single crystal, and cannot.**
Make it pay from level 1, on a curve authored in data, reading the one per-structure level the rest of the
game already persists.

---

## 2. RCA — three independent defects, each verified at source

### D1 — the payout is gated at L3, and L3 is unreachable

`Assets/_Modules/Village/Buildings/CrystalMine.cs:188`
```csharp
if (_currentLevel < MaxLevel) return;
```
`MaxLevel = 3` (`:69`), `_currentLevel = 1` (`:78`) — a plain private field with **no save, no load, no
external writer**. The only writers are `:280` (`_currentLevel++`, the Coins path) and the regression's
reflection (D3).

### D2 — the BuildMode upgrade verb refuses the mine at the first check

`mine_crystal` authors **no `maxLevel`** and **no `upgradeCost`** (full `repo` block is
`behaviorId/buildCost/cost/navSurface/placement`). Contrast `tower_wall_wizard`, `tower_arcane_spire`,
`fountain_healing`, `lumberyard`, `foundry`, `silo`, both walls and all four towers — all author
`"maxLevel": 3`.

`Assets/_Modules/Core/Catalog/RepoProps.cs:62` — `public int maxLevel = 1;`
`BuildModeController.cs:2453-2458` — `MaxLevelFor` returns `Mathf.Clamp(repo.maxLevel, 1, 3)`.

Upgrade click path (`BuildModeController.cs:2202-2252`):
- `:2203-2204` → `level = 1`, `maxLevel = 1`
- `:2220-2222` → `IsUpgradable("mine_crystal") || IsResourceBuilding("mine_crystal")` → **false**
  (`building-tiers.json` has six ladders: `arcane-tower, armorer, barracks, forge, lumbermill, farm`;
  `ResourceBuildingProgression.OrderedIds` is `{farm, lumbermill, forge}`)
- falls to the inline tower path `:2247` → `if (level >= maxLevel)` → **`1 >= 1`**

**The player is told "Max tier reached." on a freshly-built mine.** This is the proving line, and the
FlowTrace string to grep for in a capture.

### D3 — the oracle that should have caught this cheats past it

`Assets/Editor/Regression/CrystalProductionRegression.cs:63-66`
```csharp
// Reach max level (guaranteed +1 crystal per cleared wave).
var lvlField = typeof(CrystalMine).GetField("_currentLevel", BindingFlags.NonPublic | BindingFlags.Instance);
if (lvlField != null) lvlField.SetValue(mine, CrystalMine.MaxLevel);
```
Its own header (`:6`) claims it proves *"a crystal PRODUCER yields > 0 at a **reachable** level."* It
reflectively writes a private field into a state no player can ever reach. **That single line is why this
shipped.**

### Non-defects — checked and ruled out, do NOT "fix" these

- **The wallet is fine.** `CrystalEconomy` has been a thin façade over `GameState.Resources.Crystals`
  since save v18 (`CrystalEconomy.cs:14-19`, `:90-97`, `:140-157`).
- **The yield is already data-driven.** `crystalsPerWave: 1` is authored on `crystal-mine` in both copies
  of `buildings.json`; `DefaultCrystalsPerWave` is only the null-safe fallback. The *value* is the problem.
- **`repo.maxLevel` IS consumed** — `MaxLevelFor` (`:2453`), called at `:2113` and `:2204`. The mine simply
  never authored it.

---

## 3. How a BuildMode structure's level persists (the fix hangs on this)

| Step | file:line |
|---|---|
| Persisted record | `Core/State/PlacedStructureData.cs:52` — `public int level;` |
| Live component | `Village/BuildMode/PlacedStructure.cs:46` — `public int level = 1;` |
| Written on upgrade | `BuildModeController.cs:2346`, then `:2359` `UpdateLayoutLevel(...)` (impl `:3123`) |
| Written by timer path | `Buildings/Progression/CompletedUpgradeApplier.cs:90` |
| Restored on load | `BuildMode/BaseLayoutLoader.cs:342` |
| Re-asserted on load | `BaseLayoutLoader.cs:375-376` → `ApplyTierStats` |

**Conclusion: the game already has a persisted, save-round-tripped, per-instance level. `CrystalMine`
invented a second one that persists nowhere.**

**Three level authorities already exist. This WO must not add a fourth:**
1. `PlacedStructureData.level` — per instance, in the save. **(The one to use.)**
2. `ResourceBuildingState` — per building-id, in **PlayerPrefs**. Farm/Lumbermill/Forge only.
3. `GameState.BuildingTiers` — per building-id, in the save, for the six `building-tiers.json` ladders.

---

## 4. Ruling: `CrystalMine` must NOT keep `_currentLevel`

**Delete `_currentLevel`. Pull the level from the `PlacedStructure` the mine is a component of.**

Rationale (`docs/ARCHITECTURE_PRINCIPLES.md`):
- §1 one authority per concern — a behaviour component holding a private shadow of a persisted fact IS a
  second authority. Same failure mode `ModifierService.cs:57-72` records for the `windmill`/`farm` split.
- §2b every system is a READER — the mine should ask "what level am I?", never answer it.
- §3 right-sized now — **pull, don't push.** `GetComponentInParent<PlacedStructure>()?.level ?? 1` at
  payout time is correct on BOTH the upgrade and load paths, with zero new interface and no change to
  `ApplyTierStats`.

Fallback: a scene-baked mine with no `PlacedStructure` reads level 1 and pays the L1 rung. Honest, never
zero, never throws.

---

## 5. The payout curve (authored data, not C#)

Author on `crystal-mine` in **both** copies of `buildings.json`, replacing the scalar `1`:

```json
"crystalsPerWave": [ 2, 4, 7 ]
```

Indexed by `level - 1`, clamped into range.

**Read-migration (required):** `CrystalsPerWave(int level)` must accept a bare scalar as a flat curve
(`1` → `[1,1,1]`) so a hand-edit back to a number degrades instead of throwing. Mirrors the read-migrate
discipline in CLAUDE.md §7.

### Sanity check

Wave cycle from `waves.json`: avg **401 s** → **~9 waves/hr**.

| Level | /wave | /hr | vs 1 crystal-Echo (126/hr) | vs 6 Echoes (1,550/hr) |
|---|---|---|---|---|
| L1 | 2 | 18 | 14% | 1.2% |
| L2 | 4 | 36 | 29% | 2.3% |
| L3 | 7 | 63 | **50%** | **4%** |

A maxed mine ≈ half of ONE echo, and **only while the player actually fights waves** — echoes accrue
offline, the mine does not. Crystals stay the slowest faucet (`echoes-balance.json` `_authoringNotes`:
*"crystals remain the slowest faucet (monetization guard, WO-830 Sec.3b)"*).

### Upgrade costs — author on `mine_crystal` in both copies of `structures-catalog.json`

```json
"maxLevel": 3,
"upgradeCost": [
  { "wood": 240, "food": 0, "iron": 150, "crystals": 0 },
  { "wood": 560, "food": 0, "iron": 350, "crystals": 0 }
]
```

Schema matches `tower_wall_wizard` / `tower_arcane_spire` verbatim; consumed by
`BuildModeController.UpgradeCostFor` (`:2461-2469`).

**Deliberately ZERO crystals.** Charging crystals to unlock crystal income inverts the loop the mine
exists to relieve. 240W is ~20 min of an L1 lumbermill — a real but fair early gate.

### ⚠ OWNER PIN — mine stacking

`mine_crystal` does **not** author `"singleton": true` (unlike `jeweler` and `fountain_healing`). N mines
stack linearly: three maxed = 189/hr = 1.5 Echoes. **Recommendation: leave it non-singleton** — multiple
mines is the genre-correct CoC shape and 80W+50I each is the brake. Flagged, not decided silently.

---

## 6. Ruling: RETIRE the legacy Coins F-key path (do not convert it)

Retire `TryUpgrade()` (`CrystalMine.cs:250-288`), `OpenUpgradeUI`/`CloseUpgradeUI`/`InjectUpgradePanel`/
`ShowSimpleUpgradePrompt`/`ConfirmSimpleUpgrade`, `_costL1toL2`/`_costL2toL3`, `_upgradeUiRoot`,
`_awaitingSimpleConfirm`, and the `MobileInteractButton.Request(...)` registration.

1. **It cannot survive the fix.** Its only effect is `_currentLevel++` — the field being deleted.
2. **Wrong currency lane.** Coins is Gold, the shop/sell wallet. The mine costs Wood+Iron and yields
   Crystals. (Precision: Coins and Crystals are two currencies in the SAME store, `GameState.Resources` —
   a lane error, not a persistence error. Same conclusion.)
3. **Converting it creates a second charging path.** `fountain_healing` is the live proof: it authors
   `maxLevel: 3` **and** carries the identical Coins F-key path (`HealingFountain.cs:298-313`), so today
   two independent systems can each level one building.
4. **§2 presentation law.** The mine builds its own world-space bubble with hard-coded colors and a
   `"[ Tap / F ] Confirm Upgrade — {cost} Coins"` string (`:435`, `:526`) —
   `ARCHITECTURE_PRINCIPLES.md:63-65` names this exact shape as the canonical violation.

**Keep:** the wave subscription (`:596-616`), `CrystalsPerWave()`, `ApplyVisual`/`BuildPlaceholder`,
`_useExternalVisual`. The file should shrink ~300 lines, all dead-by-construction.

---

## 7. Files to edit

| File | Change |
|---|---|
| `Assets/_Modules/Village/Buildings/CrystalMine.cs` | Delete `_currentLevel` + `MaxLevel` + the Coins/UI block. Add `private int CurrentLevel => GetComponentInParent<PlacedStructure>()?.level ?? 1;` clamped. `OnWaveCleared` drops the gate, awards `CrystalsPerWave(CurrentLevel)`. Keep the `Guard`/warn-on-parse-failure shape and the cache. |
| `Assets/Resources/Data/Canonical/buildings.json` | `crystal-mine.crystalsPerWave`: `1` → `[2, 4, 7]` |
| `Assets/StreamingAssets/Data/Canonical/buildings.json` | identical — **byte-identical** |
| `Assets/Resources/Data/Canonical/structures-catalog.json` | `mine_crystal.repo`: add `maxLevel` + `upgradeCost` |
| `Assets/StreamingAssets/Data/Canonical/structures-catalog.json` | identical — **byte-identical** |
| `Assets/_Modules/Village/Catalog/StructureFactory.cs:754-760` | Comment only — header still says "banks +1/wave at L3" |
| `Assets/Editor/Regression/CrystalProductionRegression.cs` | Remove the reflection cheat `:63-66`; add cases 1-3, 5 |
| `Assets/Editor/Regression/BuildingUpgradeRegression.cs` | Add case 4 |

---

## 8. Regression cases

**Home:** both suites exist and are already wired into `DataRegression.RunAll`
(`DataRegression.cs:363` and `:302`). **No new suite, no new registration.**

### `CrystalProductionRegression` (flips RED → GREEN; that is its stated design, `:14`)

1. **`[l1-pays]`** — real `CrystalMine` under a real `PlacedStructure` at `level = 1`, drive
   `OnWaveCleared(1)` over a real `CrystalEconomy`, assert crystals rose. **MUST NOT reflect any private
   field** — that is the whole point.
2. **`[level-round-trip]`** — `PlacedStructureData{ itemId="mine_crystal", level=3 }` → save → load →
   assert `PlacedStructure.level == 3` AND payout equals the L3 rung (7), not L1.
3. **`[curve-monotonic]`** — the curve parses as an array of length == `repo.maxLevel`, every entry `> 0`,
   strictly non-decreasing.
5. **`[single-level-authority]`** — `typeof(CrystalMine).GetField("_currentLevel", NonPublic|Instance)
   == null`. Turns yesterday's cheat into today's guard.

### `BuildingUpgradeRegression` — the GENERIC guard

4. **`[yield-reachable-at-founding]`** — sweep every catalog row with a per-wave/per-tick yield curve:
   - first rung must be `> 0` — **no structure may deliver nothing at its founding level**
   - `Clamp(repo.maxLevel,1,3) >= curve.Length` — **no structure may author rungs it cannot reach**
   - if `curve.Length > 1`, the row must author `repo.upgradeCost` with `curve.Length - 1` entries, or be
     in `BuildingTierCatalog` / `ResourceBuildingProgression` — some upgrade path must exist

   Same family as `[upgrader-reaches-receiver]` (committed 2026-08-04): *authored data with no reachable
   consumer is resources the player spends for nothing.* Failure message should say so in that voice.

---

## 9. What NOT to touch

- **`ResourceBuildingProgression` / `ResourceBuildingState`** — adding the mine there creates a FOURTH
  level authority and re-commits the original sin.
- **`CrystalEconomy.cs`** — already the correct thin façade. Only call it.
- **`WaveManager.AwardWaveCrystals`** (`:2519+`) — a separate boss-drop faucet. Do not merge.
- **`ApplyTierStats` (`BuildModeController.cs:2401`)** — do NOT generalize the three-branch switch here.
  Real leverage, but structural; §3 forbids smuggling it into a player-facing fix. **Own WO:** *extract a
  generic `IStructureLevelReceiver` seam so `ApplyTierStats` stops hard-coding DefenseTower/ArcaneTower/
  WallSegment.*
- **`HealingFountain.cs`** — identical bug and worse. **Own WO.**
- **`storageCapacity` / wallet caps** — verified dead (`IsStorageContainer` has zero callers). Out of scope.
- **Any `.unity` scene file.** `CrystalEconomy` self-bootstraps (`:54-60`); no bake required.
- **The `jeweler` ladder** — §11, separate WO.

---

## 10. Acceptance criteria

- [ ] A freshly-placed mine at level 1 awards **2** crystals on the next cleared wave, into
      `GameState.Resources.Crystals` (the balance the HUD shows and the Ballista spends).
- [ ] Tapping Upgrade on a level-1 mine opens the BuildMode charge (240W/150I) — **not** the toast
      `"Max tier reached."`. Verify via `[Flow:BuildUpgrade]` showing `lvl=1/3`, not `lvl=1/1`.
- [ ] L2 pays 4/wave, L3 pays 7/wave. `"Max tier reached."` appears only at L3.
- [ ] The level survives quit → relaunch: a level-3 mine reloads at 3 and still pays 7.
- [ ] `grep -rn "_currentLevel" CrystalMine.cs` returns nothing.
- [ ] No F-key/Coins upgrade prompt at the mine, desktop or mobile.
- [ ] `CRYSTAL_PRODUCTION_OK` — green **without** the reflection.
- [ ] `REGRESSION_OK <n>/<n> suites` (read n off the marker) + `COMPILE_GATE_OK` + brace/NUL gate.
- [ ] Both JSON copies byte-identical, both files.
- [ ] **PO felt-verifies the wave-clear crystal tell before close (§13 — CLI does not close).**

---

## 11. FOLLOW-ON WO (separate — do NOT fold in): Jeweler as the crystal upgrader

Owner ruling 2026-08-04: the jeweler is the crystal lane's upgrade hook, mirroring
`lumbermill`→wood / `farm`→food / `forge`→efficiency. **This is a NEW FEATURE (§13) and must not be
attached to a bug fix.**

**Verified:** `jeweler` IS placeable — 50W/40I/30C, `behaviorId: GameplayBuilding`,
**`"singleton": true`** (good: exactly one upgrader), `npcModel: Tiefling`. It has **no tier ladder**.
**`GameModifiers` has NO crystal key** (22 fields, none crystal).

**Five pieces, in this order — order matters:**

1. **Add the field FIRST.** `GameModifiers.crystalProductionMult` + compound it in `ModifierService.Apply`
   (`:161-194`). Must land before the ladder: `ModifierKeyCoverageRegression.cs:153-177` reflects over
   `GameModifiers` and **fails the build** if `building-tiers.json` authors a key with no matching field.
2. **Wire the RECEIVER — the step that gets skipped.** Adding `case "jeweler"` to `ProductionMultFor`
   would do **nothing**: that method's only runtime call site is `ResourceBuildingState
   .CurrentEffectiveYield` (`:102`), and the mine is not a `ResourceBuildingProgression` building. The
   real receiver is `CrystalMine.OnWaveCleared`, reading `ModifierService.Active.CrystalProductionMult`.
3. **Author the ladder** in both `building-tiers.json` copies under `id: "jeweler"`.
   **CUMULATIVE-ABSOLUTE IS LAW** — `ModifierService.Compute` (`:137`) applies only one tier def, never a
   sum, so every tier must restate the full kit of every tier below it. Enforced by
   `CathedralCumulativeRegression`.
4. **The id handshake — three spellings must agree, with no shared constant.** `BuildingTierCatalog.Find`
   matches by bare ordinal `==` (`:112`). `StructureFactory.cs:790` gives the placed jeweler
   `Building.Id == "jeweler"`. But `BuildingInteractable`'s hook-id derivation (`:350-380`) is a
   hard-coded substring table with **no `jeweler` case** — without one the upgrade panel will not route.
   `buildings.json.jeweler` also needs `isUpgradable: true` + a non-empty `upgradeType` or
   `BuildingCatalogTest.cs:185-189` fails.
5. **Guard it** — extend `[upgrader-reaches-receiver]` to cover the crystal receiver.

**⚠ THE TRAP, NAMED.** This is the exact failure fixed on 2026-08-04: the food ladder sat under
`"windmill"` while the collector was `"farm"`, so **+45% of paid-for food perks silently did nothing**.
`building-tiers.json:8` records the same for `tower_arcane_spire`. **A ladder authored under an id nothing
consumes is bought with real resources and does nothing. Do not author step 3 before 1, 2 and 4 are green.**

**Crystals stay UNCAPPED (owner ruling, 2026-08-04).** Premium/bottleneck currency; CoC precedent — gems
uncapped, gold/elixir capped by storages. When the cap system is wired, crystals must be **explicitly
exempt by design**, with a named constant and a regression that fails if a crystal cap is introduced.
Nothing is capped today (`IsStorageContainer` has zero callers), so the exemption must be written down,
not merely un-implemented. With no cap, **the production RATE is the only brake** — size the ladder
modestly.

---

## 12. §15 canon flags (fix in the same commit)

- `Village/Waves/WaveManager.cs:2513-2516` — claims a *"separate AetherCrystals empower pool"*.
  **STALE since save v18**; `CrystalEconomy.cs:14-19` folded it into `Resources.Crystals`. This comment is
  why "two wallets" reads as true.
- `WorkOrders/WORK_ORDER_679_crystal_economy_faucets.md:18` — *"CrystalMine — LIVE, passive accrual, the
  only steady faucet."* **False.** Add a `STALE:` banner naming this WO.
- `Assets/Resources/Data/Canonical/building-tiers.json:3` — top-level `_comment` still describes a
  `windmill` block and *"windmill foodProductionMult at x1.45"*; that block moved to `farm` on 2026-08-04.
- `Village/Catalog/StructureFactory.cs:754-757` — "banks +1/wave at L3".

---

**Verification note:** every `file:line` above was opened at source. Nothing is asserted from a comment, a
doc, or inference. Three claims in the originating brief were corrected against the code: (a) `repo.maxLevel`
DOES have a live consumer — the mine simply never authored it; (b) the Coins path is a different currency
in the same store, not a different wallet; (c) the deeper cause is not the missing catalog row but
`CrystalProductionRegression.cs:63-66`, the oracle that reflectively reached a state no player can.
