# PREREQUISITE REGISTRY - every buildable and upgradable thing, and every gate on it

**Measured:** 2026-09-06, working tree on branch `feat/synty-art-retheme` at HEAD `ec6026015`.
**Companion to:** `WorkOrders/WORK_ORDER_1427_why_cant_i_every_refusal_names_its_blocker_and_shows_the_steps.md`
(this registry is the CONTENT that feature surfaces; nothing reads this file at runtime).
**Owner ask, verbatim (2026-09-06):** *"i want an audit of all items buildable upgradable. I want the user to be
able to see the steps need to get there."*

## THE BINDING RULE OF THIS DOCUMENT

Every number, id, string and verdict below was read **at source this session** - from the canonical JSON under
`Assets/Resources/Data/Canonical/` or from the `.cs` file at the cited line. **Nothing here is copied from another
doc, a comment's summary, or a prior session's memory.** Where a fact could not be proven from here it says
**NOT VERIFIED** rather than guessing (CLAUDE.md section 11B).

**HEAD vs WORKING TREE.** Four lanes (WO-1423 village-tier gate, WO-1425 cap-aware refusal, WO-1424, WO-1426) are
**uncommitted in the working tree** as of this measurement. Every claim that differs is tagged `[HEAD]` or
`[TREE]`. `git status` at measurement time listed 18 modified `.cs` files including `BuildingPerkService.cs`,
`BuildingTierCatalog.cs`, `TownBankCapacity.cs`, `ManageScreenVM.cs`, `ManageScreenPanel.cs`,
`BuildingUpgradeVM.cs`, plus the untracked `Assets/Editor/Regression/ProgressionReachabilityRegression.cs`.

---

## 1. SUMMARY COUNTS

| Thing | Count | Source |
|---|---|---|
| Catalog entries authored | **28** | `structures-catalog.json` `entries[]` |
| ...offered to the player by the live browser | **23** | `card-collections.json`, 7 collections |
| ...**buildable on a fresh save** | **17** | 23 minus 4 permanently hidden, minus 2 wave-unlocked |
| ...unlockable later by a player action | **2** | `tower_arcane_spire` (3 waves), `healing_caravan` (7 waves) |
| ...**listed but permanently unbuildable** | **4** | `gate_stone`, `jeweler`, `tower_catapult`, `tower_siege_tower` |
| ...authored but in no collection at all | **5** | `mill`, `lumbermill`, `wall_stone`, `deco_torch`, `repair_default` |
| Placed-structure LEVEL rungs authored | **30** | sum of `repo.maxLevel - 1` |
| ...on ids the player can actually own | **26** | excludes the 2 hidden towers' 4 rungs |
| Building TIER rungs (`building-tiers.json`) | **26** | 6 ladders: 4+4+6+4+4+4 |
| ...carrying `requiresVillageTier >= 1` | **20** | every T2/T3/T4 (18) + barracks T5/T6 |
| Research perks | **17** | perks nested in tier rows |
| ...carrying a village gate `>= 1` `[TREE]` | **12** | the 12 sitting on tier rows 2-4 |
| Village Tier rungs | **3** | `VillageTierService.MaxTier = 3` (`VillageTierService.cs:27`) |
| Barracks LEVEL rungs (troop unlocks) | **5** | `barracks.json` levels 2-6 |
| ...**reachable** | **0** | see 2.0 - the ladder has no live door |
| Troops authored | **9** | `troops.json` `troops[]` |
| ...**trainable in play** | **2** | Footman, Archer - see 2.0 |
| Troop LEVEL rungs | **54** | 6 per troop x 9 (curves are 7 long) |
| ...reachable | **12** | 6 each on Footman and Archer |
| Gear upgrade rungs | **20** | `gear-levels.json`, 4 per rarity x 5 rarities |
| Craft recipes | **14** | 8 gear + 6 jeweler |
| **Capped-resource cost values (core scope)** | **132** | `structures-catalog` + `building-tiers`, charged lane |
| ...**that exceed the base bank and need a storage container** | **47** | see section 4 |
| Capped-resource cost values (whole corpus) | **205** | adds `barracks.json`, `gear-levels`, both recipe files |
| ...needing a container above level 0 | **70** | by required level: L1 x17, L2 x12, L3 x15, L4 x14, L5 x9, L6 x3 |

> The brief's figures "54 of 143" were **not reproduced** under any scope I measured. My scope is stated with each
> number above and the script is reproducible from the cited JSON keys. Treat the measured numbers as authoritative
> and the brief's as superseded.

**Storage ladder used throughout** (`storage-caps.json` `baseCap` 2000 + `levelCapacityMultipliers`
`[1,2,4,8,16,32]` x `storageCapacity` 1000 on the three container rows; arithmetic in
`TownBankCapacity.MaxOf` `:435-442` / `CapacityAtLevel` `:424-429` / `BaseCapOf` `:409-421`):

| One container at level | 0 | 1 | 2 | 3 | 4 | 5 | 6 |
|---|---|---|---|---|---|---|---|
| Bank ceiling | 2000 | 3000 | 4000 | 6000 | 10000 | 18000 | 34000 |

Containers are `lumberyard` (wood), `foundry` (iron), `silo` (display name **"Stoneyard"**, stone).
**Crystals and Coins are UNCAPPED** - `TownBankCapacity.UncappableResources` `:265`, `IsCapped` `:317-321`.
`MaxOf` sums **every built container** of that resource (`TownBankCapacity.cs:435-442`, slot walk `:972-1005`), so a
second container also raises the ceiling. That is real but **undiscoverable**, and this registry uses the
one-container bound throughout, exactly as `EconomySinkCapRegression` does.

---

## 2. UNREACHABLE RUNGS - the list that needs fixing

### 2.0 SEVEN OF THE NINE TROOPS CAN NEVER BE TRAINED - the biggest finding in this audit

**`GameState.BarracksLevel` is pinned at 1 for the life of every save, because the only UI that raises it has no
entry point.** Verified end to end this session:

1. `GameState.cs:506` - `public int BarracksLevel = 1;`. New game sets 1 (`GameStateService.cs:1235`).
2. The training gate is `BarracksService.EnqueueTraining` `:310-313`:
   `if (!IsTroopUnlocked(troopId)) { stopReason = "Locked - unlocks at Barracks Level N."; return 0; }`
3. `BarracksService.IsTroopUnlocked` `:63-64` -> `BarracksProgression.IsTroopUnlocked(troopId, BarracksLevel)`
   `:100-116`. **It reads `BarracksLevel` only** - the `building-tiers.json` barracks ladder is not consulted.
4. `BarracksLevel` is raised only by `BarracksProgression.ApplyBarracksUpgrade`, reached only from the
   `BarracksUpgrade` job enqueued only by `BarracksService.UpgradeBarracks` `:162-194`, whose only caller is
   `BarracksPanelVM.UpgradeBarracks` `:238-247`, whose only caller is `BarracksPanel.cs:389`, inside a panel whose
   only entry point is `BarracksPanel.ShowBarracksUI()` (`BarracksPanel.cs:74`).
5. **`ShowBarracksUI` has ZERO callers.** A ripgrep for the symbol across all of `Assets/` - every `.cs`, `.unity`,
   `.prefab`, `.yarn` - returns three hits, all inside `BarracksPanel.cs` itself (`:21` its own header comment,
   `:74` the definition, `:78` a trace string). No dev tool writes `BarracksLevel` either.

**Therefore UNREACHABLE:** Spearman, Field Cleric, Shieldguard, Outrider, Siege Catapult, Battlemage,
Echo Legionnaire (7 troops), all **5** `barracks.json` level rungs, the **42** troop-level rungs on those seven,
and every ability they unlock at levels 3/5/7.

**The `building-tiers.json` barracks ladder does NOT open this door.** Its tier `effect` strings say
*"Unlocks Spearman"*, *"Unlocks Shieldguard and Field Cleric"*, *"Unlocks Battlemage"*, *"Unlocks Echo
Legionnaire"*. **No code parses `effect`.** The tier NUMBER is read at exactly one place -
`TroopUnlock.EffectiveBarracksTier` `:42-45`, `max(BarracksService.BarracksLevel, ModifierService.TierOf("barracks"))` -
which feeds `TroopUnlock.IsTrainable` `:53-57`, the **display** authority used by `TroopTrainingVM.cs:333` and
`Assets/_Modules/Village/Troops/TroopDialogueCommands.cs:103`. `EnqueueTraining` does not use it.

**Two live contradictions it produces, and the second is worse:**
- With the Barracks building at tier 2 or above, the `TroopTrainingPanel` renders an unlocked, **tappable**
  Spearman row (`TroopTrainingVM.cs:333` gates on `IsTrainable` alone) and the tap is refused with
  *"Locked - unlocks at Barracks Level 2."*
- **Manage > Troops shows all seven locked troops as permanent dead cards.** It ANDs both authorities for the
  CTA (`ManageScreenVM.cs:1866-1867`, `Unlocked = unlocked && trainable`) but it still **adds the card**
  (`TroopChoices.Add(choice)` `:1886`) with `StateWord = "Locked"` `:1891` and
  `Requirement = "Requires Barracks Tier " + def.UnlockBarracksTier` `:1881-1883`. `DoorLabel` stays null by WO-1422
  ruling 3.5 (*"there is no troop skill/perk panel to open"*), so the screen **names a requirement that no player
  action can satisfy and offers no door to it** - the exact shape of the owner's complaint, applied to seven cards
  at once.

**No suite catches it.** `DataRegression.cs:3011-3012` asserts `IsTroopUnlocked(t.Id, t.UnlockBarracksTier)` -
true by construction. Nothing tests that the level is reachable.

**No scene or prefab reaches it either, and this was checked the only way that proves anything.** Unity serialises
a component by its **script GUID**, not by class name, so a name grep over `.unity` files is worthless here.
`Assets/_Modules/Village/Hero/BarracksPanel.cs.meta` carries
`guid: b245a5682900ee14cbff23be363845d3`, and a repo-wide search for that GUID returns **exactly one file - its own
`.meta`**. Nothing instantiates the component from a scene, a prefab or a ScriptableObject. The only construction
site in the whole repo is `BarracksPanelVM.ResolveOrCreateHost` `:183-188`
(`new GameObject("BarracksPanelHost").AddComponent<BarracksPanel>()`), which is itself called only from
`ShowBarracksUI`.

Three independent checks agree: the `.cs` caller chain, an exhaustive all-file-type grep for `ShowBarracksUI`
(3 hits, all inside `BarracksPanel.cs`), and the GUID search above. **This finding is proven, not inferred.**

**The fix is one entry point**, not a data change: wire `ShowBarracksUI`, or fold the barracks-level ladder into
the Manage > Troops tab, which already owns the surface and already renders the seven locked cards.

### 2.1 Four ids are advertised in the build browser and can never be built

All four sit in `card-collections.json` and in some verb's `lockedIds` in `build-categories.json`.
`BuildCollectionBrowser.IsCollectionItemVisible` (`BuildCollectionBrowser.cs:355-380`) hides an id in
`lockedIds` unless `ProgressionUnlocks.IsUnlocked(id)` (`:365-367`). **Nothing anywhere writes the unlock flag for
these four.** `ProgressionUnlocks.Unlock` (`ProgressionUnlocks.cs:57-74`) is called from exactly three
player-action sites - `CastleDefensePlansPickup.cs:116`, `RewardedProgression.cs:26`, `RewardedProgression.cs:54` -
plus a server-entitlement restore (`RewardedProgressionEntitlementService.RestoreIfProgressionGrant`,
`RewardedProgression.cs:128-131`) hard-limited to `gate_stone` and `healing_caravan`. **NOT VERIFIED:** whether any
live SKU grants either.

| id | Display | Why unreachable | Rungs lost |
|---|---|---|---|
| `gate_stone` | Stone Gate | **Hard-coded hide that precedes every unlock check.** `BuildCollectionBrowser.cs:31` `HiddenUntilFinishedArtId = "gate_stone"`, applied at `:357` before any flag is read. Reason in-code: unfinished card art. The unlock flag DOES flip (`RewardedProgression.TryUnlockStoneGate` `:51-57`, called from `BuildModeController.cs:2602` on `wall_wood` reaching L2) - it just never matters. | 1 build |
| `jeweler` | Jeweler | Town `lockedIds`; no writer for `unlock.jeweler`. | 1 build; **and it strands all 6 `jeweler-recipes.json` recipes** |
| `tower_catapult` | Catapult | Defense `lockedIds`; no writer. | 1 build + 2 upgrade rungs |
| `tower_siege_tower` | Sky Ballista (Anti-Air) | Defense `lockedIds`; no writer. | 1 build + 2 upgrade rungs |

**Proof of the asymmetry:** the two ids that ARE reachable have writers. `tower_arcane_spire` is unlocked by
walking into the Castle Defense Plans prop, which spawns at `WavesCompleted >= 3`
(`CastleDefensePlansService.cs:81` `RequiredWavesSurvived = 3`, `:131-133`, pickup at
`CastleDefensePlansPickup.cs:116`). `healing_caravan` is unlocked at `WavesCompleted >= 7`
(`RewardedProgression.cs:18` `HealingCaravanPlansWave = 7`, granted by `HealingCaravanPlansService.Update`
`:74-85`).

### 2.2 Three catalog ids exist but are in no collection - not offerable at all

`mill`, `lumbermill` (the Resource-type row), `wall_stone`. None appears in any of the 7 `card-collections.json`
collections. `wall_stone` is by design (WO-948: stone is reached by upgrading `wall_wood` to L2, whose
`upgradeVisualPath[0]` is the stone mesh). `mill` and `lumbermill` are orphans.
`deco_torch` and `repair_default` are internal rows, not player content.

### 2.3 Wall levels 2 and 3 are authored and unreachable

`walls.json` authors four tiers - Wooden Fence (0), Stone Wall (1), Steel Wall (2), Spiked Steel Wall (3, 9 spike
DPS). `WallDefense.MaxReachableWallLevel = 1` (`WallTierData.cs:155`), documented at `:149-154` as WO-948:
*"Steel/Spiked (levels 2..3) are WO-904's, gated behind raid-steal."* **Two authored defensive tiers, with real
`heartDamageMultiplier` values, that no player action reaches today.**

### 2.4 `[HEAD]` One research perk is gated above the ceiling - fixed in the working tree

`[HEAD]` `BuildingPerkService.CanResearch` compares `PerkUnlockTier` - a **BUILDING** tier number - against
`VillageTierService.Current` at `BuildingPerkService.cs:183` (read via `git show HEAD:`). `lumber-ancient-sawmill`
sits on `lumbermill` tier 4, so it demands **Village Tier 4** against `MaxTier = 3`
(`VillageTierService.cs:27`): **unreachable by any player action, forever.**

`[TREE]` The comparison now reads `BuildingTierCatalog.PerkRequiredVillageTier` - the perk's own tier row's
authored `requiresVillageTier` - at `BuildingPerkService.cs:194-195`, accessor at `BuildingTierCatalog.cs:199-212`.
`lumber-ancient-sawmill`'s row authors `requiresVillageTier: 3`, so it becomes reachable.

**Which rungs change, exactly.** All 17 perks shift by one authored value. Village gate `[HEAD]` = the perk's
building tier; `[TREE]` = its tier row's `requiresVillageTier`:

| Building tier the perk sits on | Perks | `[HEAD]` village gate | `[TREE]` village gate | Effect of the fix |
|---|---|---|---|---|
| 1 | 5 | 1 | **0** | The first perk of every ladder stopped being village-locked on a fresh save |
| 2 | 6 | 2 | **1** | one tier cheaper |
| 3 | 5 | 3 | **2** | one tier cheaper |
| 4 | 1 (`lumber-ancient-sawmill`) | **4 - IMPOSSIBLE** | **3** | UNREACHABLE becomes REACHABLE |

Pinned by the untracked `Assets/Editor/Regression/ProgressionReachabilityRegression.cs`
(`[tier-gate-reachable]` `:82-95`, `[perk-gate-reachable]` `:103-137`), wired into `DataRegression.cs:1023`.
Both the suite and the wire are **uncommitted**. **NOT VERIFIED:** the suite has not been run this session
(read-only audit; no Unity).

### 2.5 No arithmetic dead end exists in the cost data

**Positive finding, stated because it is the question everyone asks first.** Every authored capped-resource cost
in the corpus fits under the one-container L6 ceiling of 34000. The largest is `barracks` tier 6 at **28,350**
(charged as Iron - see 3.2). `EconomySinkCapRegression` case `[ceiling]` pins this across
`structures-catalog`, `troops`, `barracks`, `building-tiers`, `gear-levels`, `gear-recipes`, `jeweler-recipes`
(`EconomySinkCapRegression.cs:128-146`, `:278-362`). **There is no rung a player cannot in principle pay for.**
Every storage problem in this document is therefore a **discoverability** problem, not a balance dead end.

**But that oracle has a real gap** (`EconomySinkCapRegression.cs:319-336`): it scans `building-tiers.json` keys
`costWood` and `costFood` and explicitly skips `costCrystal` as *"UNCAPPED and deliberately not checked"*. That is
wrong on two axes given section 3.2 - `costCrystal` IS charged against a capped resource, and `costWood` is not
always charged as wood. The pass still holds (every value is under 34000), but the oracle's attribution is wrong
and `costFood` is a key **no tier row authors**. Worth a follow-up ticket; not a live failure.

---

## 3. THE TWO STRUCTURAL DEFECTS BEHIND MOST OF THE CONFUSION

### 3.1 The Village Tier is never shown, and its control only appears once you are already blocked

- `VillageTierService.TryUpgrade` has **exactly one production caller**: `BuildingUpgradeVM.cs:1045`, inside
  `Select(tierId)` guarded by `tierId == VillageTierRowId` (`"villagetier"`).
- The only widget that reaches it is the upgrade panel's action band in its `VillageGated` state -
  `BuildingUpgradePanelMvvm.cs:1323-1338`, tap at `:1332`, label `"Raise Village Tier"`
  (`BuildingUpgradePanelMvvm.cs:425`) rendered with `current + 1` appended at `:1327`.
- `ResolveActionState` returns `VillageGated` **only when `requiresVillageTier > villageTierNow`**
  (`BuildingUpgradeVM.cs:415-426`). Every ladder's tier-1 row authors `requiresVillageTier: 0`, so **on a fresh
  save the button does not exist anywhere in the game.** It appears only after the player has bought some
  building's tier 1 and its tier 2 turns gated.
- **Nothing displays the player's current Village Tier.** Every authored string names a *required* tier. There is
  no Heart-of-Elarion panel: `BuildingInteractable.cs` has zero `Heart` hits, and `ManageScreenVM.cs:2197` says so
  in-code. `[TREE]` the new copy `"Needs Village Tier N - raise it at the Heart."` (`ManageScreenVM.cs:1232`)
  points the player at a world object with no interaction.
- `[HEAD]` The one card that names the gate has **no door**: `ManageScreenPanel.cs:2166-2168` paints a disabled
  face `"UNLOCKS AT VILLAGE LEVEL " + n` and then `return`s before any button is built. `[TREE]` adds a live
  `"UPGRADE THE HEART"` door (`ManageScreenVM.cs:1234`, rendered `ManageScreenPanel.cs:2196-2214`).
- **The Quarry is the worst case even in the tree.** The `farm` ladder authors **zero perks**, so its `DoorLabel`
  is null and the second door is hidden by design (`ManageScreenVM.cs:1250`, WO-1422 ruling 3.5). A player whose
  only tier-bearing building is a Quarry sees a gated card and, at HEAD, no route from it at all.

### 3.2 A building tier's cost is charged in a resource the JSON never names

**This is the single most surprising thing in the audit.** `building-tiers.json` authors `costWood`, `costGold`
and `costCrystal`. The spend does **not** use those lanes. `BuildingUpgradeService.TierCost`
(`BuildingUpgradeService.cs:190-199`) picks the lane **by tier number** and the amount from
`BuildingTierDef.PrimaryMaterialCost = Max(CostWood, CostCrystal)` (`BuildingTierCatalog.cs:68`):

```
BuildingUpgradeService.cs:195   if (def.Tier == 1 ...) -> HarvestResource.Wood
BuildingUpgradeService.cs:196   else if (def.Tier == 2 ...) -> HarvestResource.Food     // == the STONE bank slot
BuildingUpgradeService.cs:197   else if (def.Tier >= 3 ...) -> HarvestResource.Iron
```
`HarvestResource.Food` is `GameState.Resources.Food` (`ResourceBuildingProgression.cs:485`), which
`TownBankCapacity` presents to the player as **"Stone"**. The same tier-index mapping is repeated in the job
basket at `BuildingUpgradeService.cs:133-135`, in the Manage card at `ManageScreenVM.cs:1204-1207`, and in the
upgrade panel's cost lines at `BuildingUpgradeVM.cs:1503-1505`.

Consequences, all measured:
1. **Every ladder's tier 2 is a STONE cost.** Nothing in the JSON says "stone". Stone is produced only by
   `collector_farm` (Quarry, `role: stone_producer`) and stored only by the `silo` / **Stoneyard**.
   A fresh save holds 80 stone (`ResourceBalance.Starter` = crystals 250 / food 80 / coins 15,
   `NestedTypes.cs:55`).
2. **Every ladder's tiers 3+ are an IRON cost**, including the whole barracks 4/5/6 capstone run.
3. **The Cathedral of Magic ladder authors `costWood: 0` and `costCrystal: 1280/2560/5440/11200`, and none of it
   is charged in crystals.** `Max(0, 1280) = 1280` is charged as **Wood** at T1, 2560 as **Stone** at T2, 5440 and
   11200 as **Iron** at T3/T4. This is already flagged in-code as an open data/service disagreement -
   `BuildingUpgradeVM.cs:1512-1519` emits a `FlowTrace.Once` "lane-mismatch" line and the comment states it
   *"needs a catalog ruling"* (WO-1391).

**The UI is honest; the data is not.** Both the panel and the Manage card show the CHARGED lane
(`BuildingUpgradeVM.cs:1501-1505` comment: *"The page shows what will be CHARGED"*). So a player reading the
screen is fine. Anyone reading the JSON, a work order, or a balance doc is not.

---

## 4. REACHABLE BUT UNSIGNPOSTED - the list the new UI is built from

Every row here is reachable. Nothing in the game names the step that clears it.

### 4.1 Storage-ceiling blocks - 47 of 132 core cost values

A cost above the bank ceiling reads identically to "you have not saved up yet": the bar sits full and the refusal
says `"Not enough Wood (3150)"`. `[TREE]` WO-1425 fixes this **only in build mode** -
`TownBankCapacity.TryDescribeStorageBlock` / `StorageBlockMessage` (`TownBankCapacity.cs:485-711`) is wired at
exactly three sites: `BuildModeController.cs:3296`, `StructureCardVM.cs:131`, `StructureCardVM.cs:152`.

**Not wired, and therefore still opaque:**
- the **building-tier** upgrade (Manage > Buildings). Refusal is the generic
  `"Could not start that upgrade - check requirements and resources."` (`ManageScreenVM.cs:2495`);
  `BuildingUpgradeService.TryUpgrade` returns a bare `false` at `:160-166`.
- the **placed-structure** upgrade (Manage > Defense), via `PlacedStructureUpgradeService.TryStart`.
- the upgrade panel's own shortfall copy, `"- need N more"` (`BuildingUpgradePanelMvvm.cs:1268`, from
  `BuildingUpgradeVM.NextShortfallSentence` `:407`), which **cannot distinguish the two situations at all.**

Every cap-blocked rung, by the container level that clears it (one container; charged lane; base 2000):

**Wood (Lumberyard)**
| Rung | Wood | Needs Lumberyard |
|---|---|---|
| `lumberyard` L2->L3 | 3000 | L1 |
| `foundry` L2->L3, `silo` L2->L3 | 2700 | L1 |
| `tower_ballista` L2->L3 | 2100 | L1 |
| `tower_ground_archer` L2->L3 | **3150** | **L2** |
| `tower_catapult` L2->L3 (unreachable id) | 3500 | L2 |
| `foundry` L3->L4, `silo` L3->L4 | 4000 | L2 |
| `lumberyard` L3->L4 | 4800 | L3 |
| `tower_siege_tower` L2->L3 (unreachable id) | 5600 | L3 |
| `mine_crystal` L2->L3 | 7840 | L4 |
| `lumberyard` L4->L5 | 7800 | L4 |
| `foundry` L4->L5, `silo` L4->L5 | 6800 | L4 |
| `lumberyard` L5->L6 | 14400 | L5 |
| `foundry` L5->L6, `silo` L5->L6 | 12000 | L5 |

**Stone (Stoneyard / `silo`)** - every one of these is a building-TIER 2, charged as stone by section 3.2
| Rung | Stone charged | Needs Stoneyard |
|---|---|---|
| `farm` T2 (Quarry) | 1870 | L0 |
| `armorer` T2 | 2100 | L1 |
| `forge` T2 | 2210 | L1 |
| `arcane-tower` T2 | 2560 | L1 |
| `lumbermill` T2 | 2600 | L1 |
| `barracks` T2 | 3260 | **L2** |
| `barracks.json` level 6 (troop unlock) | 4480 | L3 |

**Iron (Foundry)** - every building-TIER 3+ plus the container ladders
| Rung | Iron charged | Needs Foundry |
|---|---|---|
| `tower_catapult` L2->L3 (unreachable id) | 2800 | L1 |
| `foundry` L3->L4 | 2880 | L1 |
| `silo` L4->L5 | 2340 | L1 |
| `lumberyard` L4->L5 | 3120 | L2 |
| `tower_ballista` L2->L3 | 3500 | L2 |
| `tower_siege_tower` L2->L3 (unreachable id) | 3850 | L2 |
| `barracks.json` level 5 | 3580 | L2 |
| `farm` T3, `armorer` T3, `forge` T3 | 4210 / 4450 / 4550 | L3 |
| `lumbermill` T3, `arcane-tower` T3 | 5310 / 5440 | L3 |
| `mine_crystal` L2->L3 | 4900 | L3 |
| `silo` L5->L6 | 4320 | L3 |
| `foundry` L4->L5 | 4680 | L3 |
| `lumberyard` L5->L6 | 5760 | L3 |
| `barracks` T3 | 6600 | L4 |
| `barracks.json` level 6 | 8000 | L4 |
| `armorer` T4, `forge` T4, `farm` T4 | 8570 / 8790 / 9250 | L4 |
| `foundry` L5->L6 | 8640 | L4 |
| `lumbermill` T4, `arcane-tower` T4 | 10190 / 11200 | L5 |
| `barracks` T4 | 12330 | L5 |
| `barracks` T5 | **18650** | **L6** |
| `barracks` T6 | **28350** | **L6** |

Gear (`gear-levels.json`) adds 20 more rungs on the same ceilings: legendary L4->L5 needs **22400 wood
(Lumberyard L6)** and 11200 iron (Foundry L5); epic L4->L5 needs 12600 wood (L5) and 6300 iron (L4).

### 4.2 Gates with no explanation anywhere

| Gate | Enforced at | Told to the player? |
|---|---|---|
| Storage ceiling on a building-tier upgrade | `BuildingUpgradeService.cs:160-166` | **No.** Generic refusal only. |
| Storage ceiling on a placed-structure upgrade | `PlacedStructureUpgradeService.cs:194` | **No.** |
| Current Village Tier | n/a | **Never displayed.** |
| Which building has the "Raise Village Tier" door | `BuildingUpgradeVM.cs:415-426` | **No.** It silently appears/disappears. |
| A tier-2 cost is STONE, not the authored `costWood`/`costCrystal` | `BuildingUpgradeService.cs:196` | Screen shows the charged lane, but nothing explains that stone comes from a Quarry and is stored in a Stoneyard. |
| The barracks TIER ladder does **not** unlock troops (section 6.4) | - | **No.** Its `effect` strings say it does. |
| `lockedIds` ids that will never unlock | `BuildCollectionBrowser.cs:365-367` | Hidden entirely - no tease, but also no explanation. |
| `wall_wood` L2 unlocked `gate_stone` | `BuildModeController.cs:2602` | The card is then hidden anyway (`BuildCollectionBrowser.cs:357`). |

### 4.3 Doc drift found while measuring

- `build-categories.json` `_paletteGroupsNote` says the Arcane Spire card is lifted by *"the wave-2 Castle
  Defense Plans drop"*. The constant is **3** (`CastleDefensePlansService.cs:81`).
- `build-categories.json` describes `visibleLockedIds` as rendering *"a visible card (normal cost shown) that
  cannot be armed"*. In the shipping browser it **hides** (`BuildCollectionBrowser.cs:373-375`). The
  greyed-with-reason presentation survives only in the retired carousel (`BuildPaletteVM.cs:364-373`).
- `building-tiers.json` gives the `forge` ladder `displayName: "Forge"` while `structures-catalog.json` gives id
  `forge` the display name **"Weaponsmith"**. The file's own v7 note declares the catalog the single naming
  authority, so the ladder row is stale.
- `building-tiers.json` `farm` ladder tier names read "Rebuild the Windmill" / "Wind Harnessing" / "Grand Mill" /
  "Winds of Plenty" while the building is the **Quarry** and the effects say "Stone production". Player-facing
  flavour, owner's call.

---

## 5. THE THREE WORST CHAINS

### Chain A - Cathedral of Magic, Tier 2
Authored as **"2,560 Crystals"**. Actually charged as **2,560 STONE** against a stone ceiling of 2,000, on a save
that starts with 80 stone and no stone producer.

1. Place a **Quarry** (`collector_farm`) - 240 wood, 80 iron (`structures-catalog.json` `collector_farm.repo.cost`).
   The first placement of each id is free (`BuildModeController.cs:2066-2090`, one-free-total; per-id freebies for
   `pet-house`, `collector_lumbermill`, `tower_ground_archer` at `:2983-2997`).
2. Place a **Stoneyard** (`silo`) - 960 wood, 240 iron. Stone ceiling becomes 2,000 + 1,000 = **3,000**.
3. Place the **Cathedral of Magic** (`arcane-tower`) - 240 iron, 240 crystals.
4. Buy Cathedral **Tier 1** - 800 Gold + 1,280 charged as **Wood**. No village gate (`requiresVillageTier: 0`).
5. Cathedral Tier 2 now reads **VillageGated**. Tap **"Raise Village Tier 1"** on its upgrade panel - **250
   Crystals** (`VillageTierService.NextCost` `:42-47`). A fresh save holds exactly 250.
6. Gather 2,560 stone from the Quarry.
7. Buy Cathedral **Tier 2** - 1,440 Gold + 2,560 **Stone**.

Nothing in the game names steps 1, 2 or 5. Step 5's control does not exist until step 4 is complete.

### Chain B - Archer Tower, Level 3
3,150 wood against a 3,000 ceiling at Lumberyard L1. This is the owner's own reported case.

1. Place an **Archer Tower** - free (founding-kit per-id freebie, `BuildModeController.cs:2996`).
2. Upgrade L1 -> L2 - 540 wood, 240 iron. Both under the 2,000 base ceiling.
3. Place a **Lumberyard** - 800 wood, 320 iron. Wood ceiling 2,000 -> **3,000**. Still short of 3,150.
   Lumberyard is deliberately NOT a founding freebie (`BuildModeController.cs:2988-2995`, WO-837).
4. Upgrade **Lumberyard L1 -> L2** - 1,200 wood, 480 iron. Wood ceiling -> **4,000**.
5. Gather 3,150 wood and 1,400 iron.
6. Upgrade Archer Tower **L2 -> L3**.

Step 4 is the whole answer and the game never says it. `[TREE]` build mode would now say it; the Manage > Defense
tab, which is where this upgrade is actually bought, still would not.

### Chain C - Barracks Tier 6 (the longest chain in the game)
`barracks` T6 costs 23,030 Gold + **28,350 charged as IRON**. Iron ceiling must reach 28,350, i.e. **Foundry L6
(34,000)**. T5 at 18,650 iron also needs L6 (L5 tops out at 18,000).

**The storage half is not circular.** Each container's own upgrade fits inside the ceiling its *current* level
already provides: L2->L3 costs 3,000 against L2's 4,000; L3->L4 costs 4,800 against L3's 6,000; L4->L5 costs 7,800
against L4's 10,000; L5->L6 costs 14,400 against L5's 18,000. The cross-resource terms interleave the same way -
Foundry L5->L6 needs 12,000 wood, which requires **Lumberyard L5** (18,000), and 8,640 iron, which fits Foundry
L5's own 18,000.

**The Village Tier half must interleave with the barracks ladder, because the control does not exist otherwise**
(section 3.1: the "Raise Village Tier" band appears only when the selected building's *next* tier is gated).
So the tier cannot be pre-bought to 3 in one go from a fresh save; the real order is:

1. Build **Lumberyard** (800w/320i) and **Foundry** (960w/480i) and **Stoneyard** (960w/240i).
2. Climb Lumberyard to **L5** and Foundry to **L6**, interleaving as above. Iron ceiling reaches **34,000**;
   wood reaches 18,000; take Stoneyard to **L2** (4,000 stone) for the barracks T2 charge of 3,260.
3. Place a **Barracks**; buy **T1** - 1,240 Gold + 1,490 **Wood**. No village gate.
4. T2 is now gated -> **"Raise Village Tier 1"** appears on the Barracks upgrade panel - **250 Crystals**.
   Buy **T2** - 2,740 Gold + 3,260 **Stone**.
5. T3 gated -> **raise to Village Tier 2** - **500 Crystals**. Buy **T3** - 5,560 Gold + 6,600 **Iron**.
6. T4 gated -> **raise to Village Tier 3** - **750 Crystals** (1,500 across the three rungs,
   `VillageTierService.cs:46`). Buy **T4** - 9,860 Gold + 12,330 **Iron**.
7. **T5** - 15,100 Gold + 18,650 **Iron** (needs the Foundry at L6; L5's 18,000 is 650 short).
   **T6** - 23,030 Gold + 28,350 **Iron**.

Total Gold across the barracks ladder alone: **57,530**, from a fresh save's 200 (`StartingBudget.StrategicGold`,
`NestedTypes.cs:101`). Gold is earned per enemy kill (`Enemy.cs:3199-3212`, `EnemyDef.CoinReward` with an
XP-derived fallback). **NOT VERIFIED:** whether the kill rate makes 57,530 Gold a reasonable grind - that is a
balance question this audit did not measure.

**No RESOURCE chain was found to be circular.** Every storage ladder interleaves cleanly and every authored cost
fits the L6 ceiling, so every row in section 4 is a signposting job rather than a balance dead end.

**But two chains terminate in something unreachable, and neither is a signposting job:**
- **The troop chain (2.0)** - the ladder that unlocks 7 of 9 troops has no UI entry point at all. This is the
  worst chain in the game by content lost, and it is a wiring fix, not a copy fix.
- **The four orphaned build ids (2.1)** - listed in the browser's data, filtered out by a lock flag nothing ever
  writes.

---

## 6. THE FULL REGISTRY

### 6.0 Fresh-save baseline (`GameStateService.ResetToNewGame`)

| Field | Value | Source |
|---|---|---|
| VillageTier | **0** | `GameStateService.cs:1254`; default `GameState.cs:390` |
| BuildingTiers | **empty dict** (every building at tier 0 = placed, never upgraded) | `GameStateService.cs:1253` |
| OwnedBuildingPerks | empty | `GameStateService.cs:1255` |
| BarracksLevel | **1** | `GameStateService.cs:1235`; field `GameState.cs:506` |
| Wood | **0** | `GameStateService.cs:1185` -> `StartingBudget.StrategicWood = 0` (`NestedTypes.cs:78`) |
| Iron | **0** | `GameStateService.cs:1184` -> `StrategicIron = 0` (`NestedTypes.cs:80`) |
| Stone (`Resources.Food`) | **80** | `ResourceBalance.Starter` (`NestedTypes.cs:55`) |
| Crystals | **250** | same - exactly the Village Tier 1 price |
| Gold (`Resources.Coins`) | **200** | `GameStateService.cs:1194` -> `StrategicGold` (`NestedTypes.cs:101`), overrides Starter's 15 |
| BaseLayout | empty | `GameStateService.cs:1164` |

**Founding freebies.** The first placement of each of `pet-house`, `collector_lumbermill`,
`tower_ground_archer` is free per-id (`BuildModeController.cs:2983-2997`), plus ONE free non-founding placement
total (`:2066-2090`). `lumberyard` was deliberately removed from the kit (`:2988-2995`, WO-837).

**Starter town.** `starter-settlement-layout.json` places `workshop`, `collector_forge`, `lumberyard`, `foundry`,
`silo` and four `tower_ground_archer`. It is **conditional**: `StarterSettlementCompletion.cs:68` requires the
`founding.default_town_selected` flag, written only by `FoundingChoiceController.cs:340` (`OnDefaultTown`). A
player who picks **"Build Your Own"** (`FoundingChoiceController.cs:288`) gets **none** of it - and therefore
starts with no containers at all. Both chains in section 5 assume the Build-Your-Own start.

### 6.1 Placed structures - build cost and level ladder

Surface: **Build browser** (`BuildCollectionBrowser`, opened from `BuildModeController.Enter` ->
`BuildPaletteUI.Show` `:322-334`, which deactivates the legacy carousel). Upgrades: **Manage > Defense** for any
id with `repo.maxLevel > 1` (`ManageScreenVM.cs:1339`, CTA `:1445` -> `PlacedStructureUpgradeService.TryStart`
`:2173`), and the build-mode selection panel (`BuildModeController.cs:2402`).
Cost source: `repo.cost` for the build; `BuildModeController.UpgradeCostFor` `:2746-2768` uses
`repo.upgradeCost[level-1]` when authored, else falls back to `build cost x level`.

`W`/`St`/`I`/`Cr` = Wood / Stone (`food` key) / Iron / Crystals.

| id | Display | Type | Build cost | maxLevel | Upgrade rungs | Verdict |
|---|---|---|---|---|---|---|
| `collector_lumbermill` | Lumber Mill | Collector | 160W 80St 120I | 1 | - | REACHABLE (founding freebie; drives the `lumbermill` TIER ladder) |
| `collector_farm` | Quarry | Collector | 240W 80I | 1 | - | REACHABLE (drives the `farm` ladder; the only stone producer) |
| `collector_forge` | Iron Mine | Collector | 240W 240I | 1 | - | REACHABLE (drives the `forge` ladder) |
| `mine_crystal` | Crystal Mine | Resource | 320W 200I | 3 | L2: 480W 300I / L3: 7840W 4900I | REACHABLE; **L3 needs Lumberyard L4 + Foundry L3** |
| `barracks` | Barracks | Resource | 600W 320I | 1 | - | REACHABLE; opens Manage > Troops |
| `pet-house` | Echo Hollow | Resource | 320W 120I | 1 | - | REACHABLE (founding freebie) |
| `arcane-tower` | Cathedral of Magic | Resource | 240I 240Cr | 1 | - | REACHABLE |
| `workshop` | Crafting Station | Resource | 240W 160I | 1 | - | REACHABLE |
| `market` | Store | Resource | 280W 120I | 1 | - | REACHABLE |
| `forge` | Weaponsmith | Resource | 240W 280I | 1 | - | REACHABLE |
| `armorer` | Armorer | Resource | 240W 280I | 1 | - | REACHABLE |
| `lumberyard` | Lumberyard | Resource (wood store, cap 1000) | 800W 320I | 6 | 1200W480I / 3000W1200I / 4800W1920I / 7800W3120I / 14400W5760I | REACHABLE; **L3+ each need the previous level's ceiling** |
| `foundry` | Foundry | Resource (iron store, cap 1000) | 960W 480I | 6 | 1440W720I / 2700W1800I / 4000W2880I / 6800W4680I / 12000W8640I | REACHABLE; **L6 needs Lumberyard L5** |
| `silo` | **Stoneyard** | Resource (stone store, cap 1000) | 960W 240I | 6 | 1440W360I / 2700W900I / 4000W1440I / 6800W2340I / 12000W4320I | REACHABLE; **required for every ladder's tier 2** |
| `tower_ground_archer` | Archer Tower | Tower | 360W 160I | 3 | L2: 540W 240I / L3: **3150W** 1400I | REACHABLE; **L3 needs Lumberyard L2** - chain B |
| `tower_ballista` | Ballista | Tower | 240W 400I | 3 | L2: 360W 600I / L3: 2100W 3500I | REACHABLE; L3 needs Lumberyard L1 + Foundry L2 |
| `wall_wood` | Wooden Palisade | Wall | 80W | 2 | L2: 120W | REACHABLE; L2 is the "Stone Wall" look and flips `unlock.gate_stone` (which is then hidden) |
| `tower_arcane_spire` | Arcane Spire | Tower | 360I | 3 | L2: 540I / L3: 1400I 800Cr | **UNLOCKS at WavesCompleted >= 3** (`CastleDefensePlansService.cs:81`) |
| `healing_caravan` | Healing Caravan | Support | 240St 400I 760Cr | 3 | **no `upgradeCost` authored** -> fallback `cost x level`: L2 = 240St/400I/760Cr, L3 = 480St/800I/1520Cr (`BuildModeController.cs:2758-2768`) | **UNLOCKS at WavesCompleted >= 7** (`RewardedProgression.cs:18`) |
| `gate_stone` | Stone Gate | Gate | 240W 200I | - | - | **UNREACHABLE** - hard hide, `BuildCollectionBrowser.cs:31,:357` |
| `jeweler` | Jeweler | Resource | 200W 280I | 1 | - | **UNREACHABLE** - no unlock writer |
| `tower_catapult` | Catapult | Tower | 400W 320I | 3 | L2: 600W 480I / L3: 3500W 2800I | **UNREACHABLE** - no unlock writer |
| `tower_siege_tower` | Sky Ballista (Anti-Air) | Tower | 640W 440I | 3 | L2: 960W 660I / L3: 5600W 3850I | **UNREACHABLE** - no unlock writer |
| `mill` | Mill | Resource | 280W 80I | 1 | - | **UNREACHABLE** - in no collection |
| `lumbermill` | Lumber Mill (Resource row) | Resource | 200W 160I | 1 | - | **UNREACHABLE** - in no collection + Town `lockedIds` |
| `wall_stone` | Stone Wall | Wall | 120W 240I | 1 | - | **UNREACHABLE by design** - reached as `wall_wood` L2 |
| `deco_torch` | Wall Torch | Decoration | 20W | - | - | not player content |
| `repair_default` | Repair Default | Decoration | 120W 60I | - | - | internal repair-cost row |

**Placement gates.** Grepping `BuildModeController.cs` for `requiresVillageTier` / `VillageTier` / `population`
returns **zero hits**. Placing is gated only by: enemy-owned scene (`:494-497`), affordability (`:1973-1978`),
Builder-line depth (`:1983-1995`), singleton-already-built (`:2289-2290`), grid footprint (`:1706`) and town-bank
capacity (`:3290`, `:3316`). **No wave, quest, tier or population precondition gates placement itself.**

### 6.2 Building TIER ladders - `building-tiers.json`

Surface: **Manage > Buildings** (`ManageScreenVM.BuildBuildingsBrowse` `:1115`, CTA "UPGRADE TO L{n}" ->
`UpgradeBuilding` `:1242`) and the **Building Enhancements** panel (`BuildingUpgradePanelMvvm`,
`PanelId.BuildingUpgrade`), reachable from the Manage card's second door, the world building tap
(`BuildingInteractable.cs:387`), build mode (`BuildModeController.cs:2549`), vendor NPCs
(`CastleVendorNpcInjector.cs:1463,:1482`) and Yarn (`DialogueCommandSink.cs:83`).

**How a placed building finds its ladder.** `CatalogRegistry.ResolveUpgradeId(id)`
(`CatalogRegistry.cs:85-90`) returns `repo.collectorBuildingId` when authored, else the id unchanged. Authored
mappings: `collector_farm -> farm`, `collector_lumbermill -> lumbermill`, `collector_forge -> forge`.
Used at `ManageScreenVM.cs:1074`, `BuildingUpgradeVM.cs:176`, `BuildModeController.cs:2512`. The tier itself is
`GameState.BuildingTiers[ladderId]` (`ModifierService.TierOf` `:42-48`), written only by
`BuildingUpgradeService.ApplyTier` `:232` (plus the dev panel).

> **This resolves the obvious alarm.** The `farm` ladder has no catalog entry and the `lumbermill` ladder's
> catalog entry is locked out of the palette - but both ladders are reached through their COLLECTOR ids, which
> are freely buildable. **Both are REACHABLE.**
>
> **One genuine ambiguity:** the `forge` ladder is shared by TWO buildings - the Iron Mine (`collector_forge`,
> via `collectorBuildingId`) and the Weaponsmith (`forge`, by pass-through). Placing either advances the same
> smelting ladder. Flagged, not resolved; needs an owner ruling.

Gate on every rung: `BuildingUpgradeService.cs:54` -
`if (def.RequiresVillageTier > villageTier) { ...trace...; return false; }`. Plus a busy-building check
(`:72-77`) and a full-Builder-line refusal (`:91-98`). The service shows the player **nothing** - only a
`FlowTrace.Step`. Player copy comes from the VM: `"Requires Village Tier N (you have M)."`
(`BuildingUpgradeVM.cs:832-833`) and the tile lock reason `"Requires Village Tier N"` (`:1184`).

**Cost is charged by tier index, not by the authored key - see section 3.2.** The table shows both.

| Ladder | Display | Tier | Authored | Gold | CHARGED as | reqVillageTier | Container needed | Verdict |
|---|---|---|---|---|---|---|---|---|
| `arcane-tower` | Cathedral of Magic | 1 | cry 1280 | 800 | **1280 Wood** | 0 | - | REACHABLE |
| | | 2 | cry 2560 | 1440 | **2560 Stone** | 1 | Stoneyard L1 | UNSIGNPOSTED |
| | | 3 | cry 5440 | 2880 | **5440 Iron** | 2 | Foundry L3 | UNSIGNPOSTED |
| | | 4 | cry 11200 | 5600 | **11200 Iron** | 3 | Foundry L5 | UNSIGNPOSTED |
| `armorer` | Armorer | 1 | wood 1000 | 670 | 1000 Wood | 0 | - | REACHABLE |
| | | 2 | wood 2100 | 1400 | **2100 Stone** | 1 | Stoneyard L1 | UNSIGNPOSTED |
| | | 3 | wood 4450 | 2850 | **4450 Iron** | 2 | Foundry L3 | UNSIGNPOSTED |
| | | 4 | wood 8570 | 5720 | **8570 Iron** | 3 | Foundry L4 | UNSIGNPOSTED |
| `barracks` | Barracks | 1 | wood 1490 | 1240 | 1490 Wood | 0 | - | REACHABLE |
| | | 2 | wood 3260 | 2740 | **3260 Stone** | 1 | Stoneyard L2 | UNSIGNPOSTED |
| | | 3 | wood 6600 | 5560 | **6600 Iron** | 2 | Foundry L4 | UNSIGNPOSTED |
| | | 4 | wood 12330 | 9860 | **12330 Iron** | 3 | Foundry L5 | UNSIGNPOSTED |
| | | 5 | wood 18650 | 15100 | **18650 Iron** | 3 | **Foundry L6** | UNSIGNPOSTED - chain C |
| | | 6 | wood 28350 | 23030 | **28350 Iron** | 3 | **Foundry L6** | UNSIGNPOSTED - chain C |
| `forge` | Forge / **Weaponsmith** | 1 | wood 1060 | 680 | 1060 Wood | 0 | - | REACHABLE |
| | | 2 | wood 2210 | 1440 | **2210 Stone** | 1 | Stoneyard L1 | UNSIGNPOSTED |
| | | 3 | wood 4550 | 2970 | **4550 Iron** | 2 | Foundry L3 | UNSIGNPOSTED |
| | | 4 | wood 8790 | 5800 | **8790 Iron** | 3 | Foundry L4 | UNSIGNPOSTED |
| `lumbermill` | Lumber Mill | 1 | wood 1370 | 460 | 1370 Wood | 0 | - | REACHABLE |
| | | 2 | wood 2600 | 970 | **2600 Stone** | 1 | Stoneyard L1 | UNSIGNPOSTED |
| | | 3 | wood 5310 | 1990 | **5310 Iron** | 2 | Foundry L3 | UNSIGNPOSTED |
| | | 4 | wood 10190 | 4250 | **10190 Iron** | 3 | Foundry L5 | UNSIGNPOSTED |
| `farm` | **Quarry** | 1 | wood 820 | 1150 | 820 Wood | 0 | - | REACHABLE |
| | | 2 | wood 1870 | 2380 | **1870 Stone** | 1 | - | UNSIGNPOSTED (village gate only; **no perks, so no second door**) |
| | | 3 | wood 4210 | 4910 | **4210 Iron** | 2 | Foundry L3 | UNSIGNPOSTED |
| | | 4 | wood 9250 | 7470 | **9250 Iron** | 3 | Foundry L4 | UNSIGNPOSTED |

### 6.3 Research perks - `building-tiers.json` `perks[]`, Gold-priced, timed

Surface: **Manage > Research** (`ManageScreenVM.BuildResearchBrowse` `:2106`, CTA -> `Research(bId, pId)` `:2504`)
and the upgrade panel's perk grid. Gate: `BuildingPerkService.CanResearch`
(`[TREE] :178-197` / `[HEAD] :170-185`) - not owned, not already researching, **building tier >= the perk's tier**
(`:190`, reason `"Upgrade the building to Tier N first."`), and **village tier >= the gate** (`:195`, reason
`"Locked - needs Village Tier N."`). Duration = `60 + gold * 0.6` seconds, capped at 24 h
(`BuildTimerConfig.ResearchSecondsForGold` `:380-385`, `researchBaseSeconds = 60` `:312`,
`researchSecondsPerGold = 0.6` `:316`). Charged from `Resources.Coins`; the job runs on the Research channel.

> **KNOWN GAP, flagged in-code** (`BuildingPerkService.cs:29-41`): **cancelling a research job does not refund the
> Gold.** `JobCost` has no coins lane. Only the enqueue-refused path refunds (`RefundGold` `:280-293`).

| Building | Perk id | Name | Gold | Time | Bldg tier | Village gate `[HEAD]` | Village gate `[TREE]` |
|---|---|---|---|---|---|---|---|
| `lumbermill` | `lumber-improved-logging` | Improved Logging | 1000 | 11m | 1 | 1 | **0** |
| `arcane-tower` | `arcane-basics` | Arcane Basics | 1200 | 13m | 1 | 1 | **0** |
| `armorer` | `blacksmith-reinforced-plating` | Reinforced Plating | 1200 | 13m | 1 | 1 | **0** |
| `forge` | `forge-efficient-smelting` | Efficient Smelting | 1200 | 13m | 1 | 1 | **0** |
| `barracks` | `barracks-swift-recruitment` | Conditioning Drills | 1600 | 17m | 1 | 1 | **0** |
| `lumbermill` | `lumber-efficient-processing` | Efficient Processing | 2000 | 21m | 2 | 2 | **1** |
| `arcane-tower` | `arcane-mana-attunement` | Mana Attunement | 2400 | 25m | 2 | 2 | **1** |
| `armorer` | `blacksmith-sharpened-edges` | Sharpened Edges | 2400 | 25m | 2 | 2 | **1** |
| `forge` | `forge-quality-forging` | Quality Forging | 2400 | 25m | 2 | 2 | **1** |
| `arcane-tower` | `arcane-wellspring` | Wellspring of Elarion | 3200 | 33m | 2 | 2 | **1** |
| `barracks` | `barracks-combat-drill` | Basic Combat Drill | 3200 | 33m | 2 | 2 | **1** |
| `lumbermill` | `lumber-construction-aid` | Construction Aid | 4000 | 41m | 3 | 3 | **2** |
| `arcane-tower` | `arcane-warding-runes` | Warding Runes | 4800 | 49m | 3 | 3 | **2** |
| `armorer` | `blacksmith-sturdy-shields` | Sturdy Shields | 4800 | 49m | 3 | 3 | **2** |
| `forge` | `forge-resource-conservation` | Resource Conservation | 4800 | 49m | 3 | 3 | **2** |
| `barracks` | `barracks-expanded-capacity` | Expanded Capacity (**army cap +5**) | 6400 | 65m | 3 | 3 | **2** |
| `lumbermill` | `lumber-ancient-sawmill` | Ancient Sawmill | 8000 | 81m | 4 | **4 - UNREACHABLE** | **3** |

`farm` (Quarry) authors **zero perks** - it is the one ladder with no Research entries and, by
`ManageScreenVM.cs:1250`, no second door on its Manage card.

### 6.4 Troops - `troops.json`, `barracks.json`, `troop-upgrades.json`

Surface: **Manage > Troops** (`ManageScreenVM.BuildTroopsBrowse` `:1840`; train -> `BarracksService.EnqueueTraining`
at `:2545`; muster -> `OpenMuster` `:2095`). The tab is visible only once a placed id contains "barracks"
(`ManageScreenVM.cs:615-616`).

**TWO LADDERS SHARE THE WORD "BARRACKS", ONLY ONE UNLOCKS TROOPS, AND THAT ONE HAS NO DOOR - see 2.0.**
- **`GameState.BarracksLevel`** (default 1, `GameStateService.cs:1235`) driven by `barracks.json` is the **real**
  troop gate: `BarracksProgression.IsTroopUnlocked` `:100-116` checks `TroopDef.UnlockBarracksTier <= barracksLevel`
  OR membership of a reached level's `unlocksTroopIds`. Raised by `BarracksService.UpgradeBarracks` `:162`, gated
  by `CanUpgradeBarracks` `:143-155` (**no village-tier gate, no requirement that a Barracks building be placed**)
  - and **unreachable**, because `BarracksPanel.ShowBarracksUI` has no callers.
- **`building-tiers.json` `barracks` tiers 1-6** grant troop damage/health multipliers, structure HP and the one
  `armyCapBonus` perk. Their `effect` strings advertise troop unlocks they do not grant. **No code reads
  `effect`.** Decorative duplicated state that restates `barracks.json` and has already drifted from it.

**Barracks LEVEL ladder** (`barracks.json`) - the one that matters, and the one with no door.
Costs go through `ResourceLedger.TrySpend` (`BarracksService.cs:179`); a `coins` term would be warned and dropped
(`LedgerCost` `:85-93`).

| Level | Cost | Time | Unlocks | Container needed | Verdict |
|---|---|---|---|---|---|
| 1 | free | 0 | Footman, Archer | - | REACHABLE (day one default) |
| 2 | 300W 80St 120I | 2m | Spearman | - | **UNREACHABLE - no door (2.0)** |
| 3 | 1040W 290St 520I | 12m | Shieldguard, Field Cleric | - | **UNREACHABLE - no door** |
| 4 | 2900W 860St 1540I 100Cr | 1h | Outrider, Siege Catapult | Lumberyard L1 | **UNREACHABLE - no door** |
| 5 | 6400W 2040St 3580I 380Cr | 3h | Battlemage | Lumberyard L4, Stoneyard L1, Foundry L2 | **UNREACHABLE - no door** |
| 6 | 14400W 4480St 8000I 1120Cr | 8h | Echo Legionnaire | **Lumberyard L5, Stoneyard L3, Foundry L4** | **UNREACHABLE - no door** |

The container columns are recorded so the ladder is costed the day the door is wired; they are not today's blocker.

**Troops.** Training charges **nothing** - time only. `BarracksProgression.TroopUpgradeCost` returns an empty
basket (`:164-167`) and `EnqueueTraining` charges nothing (`BarracksService.cs:284-291`), both per the owner
ruling of 2026-09-04 recorded at `BarracksProgression.cs:24-29`. `TroopDef.costGold` **stays on the row as the
raid-reward / mercenary-hire anchor and is never charged**. Gold is spent only via
`BuildTimerService.TryInstantFinish` to skip the clock.

| Troop | Slots | `costGold` (NOT charged) | Train seconds | `maxOwned` | Barracks level | Verdict |
|---|---|---|---|---|---|---|
| Footman | 1 | 550 | 45 | - | 1 | **REACHABLE** |
| Archer | 1 | 550 | 60 | - | 1 | **REACHABLE** |
| Spearman | 1 | 850 | 120 | - | 2 | **UNREACHABLE (2.0)** |
| Field Cleric | 2 | 205 | 240 | - | 3 | **UNREACHABLE (2.0)** |
| Shieldguard | 2 | 1150 | 180 | - | 3 | **UNREACHABLE (2.0)** |
| Outrider | 2 | 1500 | 270 | - | 4 | **UNREACHABLE (2.0)** |
| Siege Catapult | 4 | 3400 | 600 | **1** | 4 | **UNREACHABLE (2.0)** |
| Battlemage | 2 | 1450 | 360 | - | 5 | **UNREACHABLE (2.0)** |
| Echo Legionnaire | 3 | 2400 | 600 | - | 6 | **UNREACHABLE (2.0)** |

Other enforced training gates, all reachable-side: per-type ownership cap (`BarracksService.cs:343-357`, only the
catapult authors `maxOwned`), army slot cap (`:358-362`, *"Army is full."*), Train-line depth cap of 5
(`:365-372`), and the feature gate `BarracksUnlock.IsUnlocked = FeatureFlags.Barracks && state.Onboarded`
(`BarracksUnlock.cs:61`; the flag is `defaultOn: true` at `FeatureFlags.cs:1099`, so the "default OFF" comment at
`BarracksUnlock.cs:58` is stale).

**Free starter squad:** 3x Footman, once per save (`StarterArmyGrant.cs:81`, `:89`, latched at `:95`).

**Troop LEVEL upgrades** - 7 levels per troop (curve length in `troop-upgrades.json`,
`BarracksProgression.MaxTroopLevel` `:140-149`), so **6 rungs x 9 troops = 54 authored, 12 reachable** (Footman and
Archer only). Cost is **time only**: `TroopUpgradeSeconds = max(15, buildSeconds * targetLevel * 2)`
(`BarracksProgression.cs:174-180`); `TroopUpgradeCost` returns an empty basket (`:164-167`), pinned by
`TrainingCostsTimeOnlyRegression.cs:147-163`. Runs on the **Research** channel. Gate:
`BarracksService.CanUpgradeTroop` `:205-217` - troop unlocked (`"Unlocks at Barracks Level N."`), not at max, not
already upgrading. **No resource, village-tier or storage gate.** Abilities unlock at levels 3 / 5 / 7 per
`specialAbilities[].levelThreshold` (Field Cleric authors only two, at L3 and L5).

Footman rungs: 180 / 270 / 360 / 450 / 540 / 630 s. Archer rungs: 240 / 360 / 480 / 600 / 720 / 840 s.

**Army capacity.** Base **10** (`ArmyStorage.DefaultMaxArmySize`, used at `ArmyStorage.cs:57-71`) plus the summed
`armyCapBonus` (`ModifierService.cs:171`). Exactly one thing in the whole data set grants it: the
`barracks-expanded-capacity` perk (+5), so the ladder is **10 -> 15** and 15 is the ceiling. The cap is in
**slots**, not units (`ArmyReadiness.cs:132` -> `TroopDef.Slots`) - a Siege Catapult would eat 4 of 10, if it could
be trained.

### 6.5 Village Tier

| Rung | Cost | Gate | Where bought | Verdict |
|---|---|---|---|---|
| 0 -> 1 | 250 Crystals | none | the `VillageGated` action band of any gated building's upgrade panel (`BuildingUpgradePanelMvvm.cs:1332`) | REACHABLE - a fresh save holds exactly 250 |
| 1 -> 2 | 500 Crystals | none | same | REACHABLE |
| 2 -> 3 | 750 Crystals | none | same | REACHABLE |

`NextCost = 250 * next` (`VillageTierService.cs:46`); `MaxTier = 3` (`:27`); `TryUpgrade` `:54-73` refuses at max
and spends atomically through `EconomyService`. **Crystals are uncapped**, so no storage gate applies.
**The tier is never displayed and the control is conditional - see 3.1.** This is the single highest-value target
for WO-1427.

### 6.6 Gear upgrade ladder - `gear-levels.json`

Surface: the **Party Shop / gear panel** - `PartyShopVM.cs:618` (`CanImprove` decides the button), `:652`
(refusal), `:662` (`GearProgression.Improve`). **Deliberately not on the Manage screen**: `ManageScreenVM.cs:29`
records that weapons and armour are absent because `GearProgression.Improve` is instant, not a queued job.

Gate: `GearProgression.CanImprove` `:359-370` - has a next level for the rarity band
(`HasNextLevel` / `MaxLevelFor` `:319-326`), and affordability via `ResourceLedger.CanAfford` `:367`.
**No village-tier, barracks, quest or storage gate.** Level is stored per gear instance in
`GameState.GearLevels` (`GameState.cs:529`; fresh save = empty dict, `GameStateService.cs:1237`).
The refusal string is `MissingOf(cost)` - **an ordinary shortfall sentence with no cap awareness**, so the
legendary L4->L5 rung (22,400 wood against an 18,000 ceiling at Lumberyard L5) reads exactly like the Archer Tower
defect. Add it to the section 7 item 1 list.

5 rarity bands x 5 levels (index 0 is free), so **20 rungs**. Costs are Wood + Iron, both capped.

| Rarity | L1->L2 | L2->L3 | L3->L4 | L4->L5 | Worst container needed |
|---|---|---|---|---|---|
| common | 180W 90I | 450W 230I | 1080W 540I | 2520W 1260I | Lumberyard L1 |
| uncommon | 300W 150I | 750W 380I | 1800W 900I | 4200W 2100I | Lumberyard L3, Foundry L1 |
| rare | 520W 260I | 1300W 650I | 3120W 1560I | 7200W 3600I | Lumberyard L4, Foundry L2 |
| epic | 900W 450I | 2200W 1100I | 5400W 2700I | 12600W 6300I | Lumberyard L5, Foundry L4 |
| legendary | 1600W 800I | 4000W 2000I | 9600W 4800I | **22400W** 11200I | **Lumberyard L6**, Foundry L5 |

### 6.7 Crafting recipes

`gear-recipes.json` - 8 recipes, all cheap (20-80 Wood/Iron). Five carry
`requiresQuestId: "forgemasters_act4"`: `aegis_emberbrand_legendary`, `aegis_longbow_legendary`,
`aegis_aetherstaff_legendary`, `aegis_censer_legendary`, `aegis_plate_legendary`. **NOT VERIFIED:** whether
`forgemasters_act4` is a completable quest - not measured this session.

`jeweler-recipes.json` - 6 recipes, 0-500 Iron, no quest gates. **All 6 are practically stranded** by 2.1: the
`jeweler` building can never be built. **NOT VERIFIED:** whether jewel crafting has a second, non-building entry
point.

Both files are scanned by `EconomySinkCapRegression` (`:350-361`); no recipe cost approaches a bank ceiling.

### 6.7b The legacy collector LEVEL ladder is dead code - checked so nobody re-derives it

`ResourceBuildingProgression` carries a second, PlayerPrefs-backed level ladder
(`dotr.resbuilding.level.*`) with its own harvest-interval curve and a Magic-gated "arcane" top tier
(`ResourceBuildingProgression.cs:320-334`). **No id can reach it.**
`UpgradeFamilyResolver.Resolve` `:57-64` applies a fixed precedence - placed-structure key, then
`BuildingTierCatalog.IsUpgradable` -> **City (wins on overlap)**, then `IsResourceBuilding` -> Resource. The
resource catalog's entire id set is `OrderedIds = { farm, lumbermill, forge }` (`:173-175`, `:223`), and all
three have city ladders in `building-tiers.json` - so `Resolve` returns `City` for every one of them and
`UpgradeFamily.Resource` is unreachable. `BuildingUpgradeVM._isResource` is set from that resolver
(`BuildingUpgradeVM.cs:184-186`), so the `ResourceBuildingState.TryUpgrade` branch at `:965` is dead.
The resolver exists precisely because the two ladders used to be resolved in opposite orders on the start and
completion sides, which dead-ended every lumbermill upgrade (header comment `UpgradeFamilyResolver.cs:1-30`).
**No rungs are lost** - the city ladder is the live one and is fully audited in 6.2. Recorded so a future reader
does not count the legacy curve as content or "restore" it.

### 6.8 Adjacent ladders NOT audited here

Named so a later reader knows they were seen and deliberately scoped out: hero talents
(`hero-talents.json`, Wisdom-priced), Echo levels and lane assignment (`echoes-balance.json`), consumables and
crafting stations (`consumable-recipes.json`, `crafting-recipes.json`), cosmetics (`cosmetics.json`),
Realm Map nodes (`realm-map.json`), population / Echo-slot milestones (`population-milestones.json`, which gates
Echo slots 2-5 on `xp` / `questsCompleted` / `outpostsCleared` / `villageLevel` - and `villageLevel` there IS the
Village Tier, `PopulationService.cs:286`, `:297`).

---

## 7. GATES ENFORCED IN CODE, NEVER EXPLAINED - and where the explanation belongs

Ordered by how much player confusion each one causes.

| # | Gate | Enforced at | Where the explanation must be added |
|---|---|---|---|
| 0 | **The barracks LEVEL ladder has no entry point, so 7 of 9 troops are unreachable** | `BarracksService.cs:310-313` refuses; `BarracksPanel.ShowBarracksUI` `:74` has no callers | Not an explanation problem - **wire an entry point**. The natural home is the Manage > Troops tab, which already owns the surface and already ANDs both authorities (`ManageScreenVM.cs:1866-1867`). Also worth a suite: `DataRegression.cs:3011-3012` asserts unlock at the troop's own tier, which is true by construction and can never catch this. |
| 1 | **Storage ceiling on a building-tier upgrade, and on a gear improve** | `BuildingUpgradeService.cs:160-166` (bare `false`); `GearProgression.CanImprove:367` (`MissingOf(cost)`) | `ManageScreenVM.cs:2495` (replace the generic refusal), `BuildingUpgradeVM.NextShortfallSentence` `:407`, and `GearProgression.MissingOf` - all three must call `TownBankCapacity.TryDescribeStorageBlock` `[TREE] :633` instead of subtracting balances. |
| 1b | **Manage > Troops paints 7 dead cards naming an impossible requirement** | `ManageScreenVM.cs:1881-1891` (`Requirement = "Requires Barracks Tier N"`, `StateWord = "Locked"`, `DoorLabel` null) | Fixed by item 0. Until then it is the most visible instance of the owner's complaint - a named gate with no door, seven times over. |
| 2 | **Storage ceiling on a placed-structure upgrade** | `PlacedStructureUpgradeService.cs:194` | The Manage > Defense CTA path, `ManageScreenVM.cs:1445` / `:2173`. |
| 3 | **The player's current Village Tier is invisible** | n/a - nothing renders it | A persistent readout. The Manage screen header (`ManageScreenPanel`) is the natural home; the HUD resource strip is the alternative. |
| 4 | **The "Raise Village Tier" control only exists on an already-gated building** | `BuildingUpgradeVM.cs:415-426` (`VillageGated` only when `requiresVillageTier > now`) | A tier control that does not depend on a gated building being selected. Until then, every gated card needs its own door - `[TREE]` `ManageScreenPanel.cs:2196-2214` adds one, **except on the Quarry**, whose door is hidden because its ladder authors no perks (`ManageScreenVM.cs:1250`). |
| 5 | **Tier 2 costs STONE; tiers 3+ cost IRON, regardless of the authored key** | `BuildingUpgradeService.cs:195-197` | Not a UI bug - the screens already show the charged lane. It needs a **catalog ruling** (already flagged as WO-1391 at `BuildingUpgradeVM.cs:1512-1519`) so the JSON stops saying "crystal" for an iron cost. Until then, every doc reading `building-tiers.json` will be wrong. |
| 6 | **Four buildable-looking ids can never be built** | `BuildCollectionBrowser.cs:31,:357,:365-367` | Not an explanation problem - either write the unlock (a wave / quest / plans drop, as `tower_arcane_spire` has) or remove the rows from `card-collections.json` and `lockedIds`. |
| 7 | **Wall levels 2-3 are authored and unreachable** | `WallTierData.cs:155` | Either ship WO-904's raid-steal path or mark the tiers as content-not-yet-reachable so no balance pass counts them. |
| 8 | **`[HEAD]` a locked Manage card names its gate and offers no route** | `ManageScreenPanel.cs:2166-2168` (`return` before any door) | Already fixed `[TREE]`. **Do not land a build from HEAD without the WO-1423 tree.** |
| 9 | **Research gold is not refunded on cancel** | `BuildingPerkService.cs:29-41` (`JobCost` has no coins lane) | Either a `paidCoins` lane on `BuildJobData` (a save-schema decision, owner's call) or a refusal to offer cancel on a research job. |
| 10 | **`EconomySinkCapRegression` mis-attributes `building-tiers` costs** | `EconomySinkCapRegression.cs:319-336` (scans `costWood`/`costFood`, skips `costCrystal`) | Route the oracle through the same tier-index lane `BuildingUpgradeService.TierCost` uses, so the resource it checks is the resource that gets charged. The `[ceiling]` pass is currently correct by luck, not by construction. |

---

## 8. HOW TO REPRODUCE THIS AUDIT

Every table above comes from the canonical JSON under `Assets/Resources/Data/Canonical/` plus the cited `.cs`
lines. The cap arithmetic is `baseCap 2000` (`storage-caps.json`) plus
`1000 * levelCapacityMultipliers[level-1]`, and the charged lane for `building-tiers.json` is
`Max(costWood, costCrystal)` routed to Wood at tier 1, Food/Stone at tier 2 and Iron at tier 3+
(`BuildingUpgradeService.cs:190-199`). Re-run the census against those keys after any catalog edit; if a number
here disagrees with the file, **the file wins and this document is stale.**
