# WORK ORDER 430 — City/Building Upgrades → Persistent GameModifiers

**Status: READY TO IMPLEMENT**
Owner directive 2026-06-14. Design source: `Desktop/city upgades.txt`. Integration map from Explore sweep (this session).

## Goal
4-tier upgrade ladder for 5 buildings (Arcane Tower, Armorer, Forge, Lumber Mill, Windmill), bought through
Yarn dialogue, cost-gated, that **compile into one persistent `GameModifiers` contract** which every scene
creation (castle **and raids**) reads to apply perks. Upgrades must **persist and assist in raids** (owner:
"these need to persist any modifiers"). All scene creation accepts an **override modifier JSON** (owner) so the
dev menu / test harness can force-apply perks at start.

## Scope discipline
This IS the agreed economy sink — "simple flat-% upgrade-buildings, CoC-style" ([[scope-discipline-not-an-mmo]]).
Flat-% modifiers + tier-4 unique abilities. NOT an inventory shop. Wood/Food build, Crystals at higher tiers
([[resource-economy-model]]).

## Architecture — the keystone: GameModifiers as a data contract

```
BuildingTiers (save, per-building int)  --ModifierService.Compute()-->  GameModifiers (flat JSON)
                                                                              |
   dev menu / test override JSON  ----------------------------------> (override wins)
                                                                              |
                 consumed at SCENE CREATION + by live systems:               v
   DefenseTower/ArcaneTower (towerDamageMult, towerRangeMult)
   TroopController/TroopDeployer (troopDamageMult, troopHealthMult, battleForged)
   ResourceBuildingState (productionMult, offlineBonusMult)  [raids read troop mults]
   RaidGarrisonSpawner / raid scene creation (apply player troop mults on deploy)
```

### 1. `GameModifiers` (new, `DeNelle.Core.State`) — JSON-serializable flat contract
Fields (all default 1.0 / 0 = no-op so an empty contract changes nothing):
`towerDamageMult, towerRangeMult, troopDamageMult, troopHealthMult, woodProductionMult, foodProductionMult,
offlineBonusMult` + unique-ability flags `arcaneOverload, battleForged, forgefire, eternalGrove, windsOfPlenty
(bool)`. Serializable via CanonicalJson ([[webgl-canonical-json-loader]]).

### 2. `ModifierService` (new, Core or Village) — single source of active modifiers
- `GameModifiers Active { get; }` — computed from `GameState.BuildingTiers` via the tier→bonus table.
- `void SetOverride(GameModifiers or json)` / `void ClearOverride()` — override wins when set (dev/test).
- `event Action Changed` — towers/troops/harvest re-read on change.
- Tier→bonus table: a data table (reuse `ResourceBuildingProgression` style, or a new `building-tiers.json`
  CanonicalJson catalog) holding per-building per-tier cost + bonus, so it's data-authored not hard-coded.

### 3. Save v23 (append-only, ONE field) — `SaveSchema.cs` + `SaveMigrator.MigrateToV23`
`[JsonProperty("buildingTiers")] public Dictionary<string,int> BuildingTiers;` (e.g. {"armorer":2}).
MigrateToV23 seeds empty dict on old saves. **Append at end, one field, mirror v22 `army` pattern.** Do NOT
reorder existing fields. (Resource-building levels currently in PlayerPrefs — migrate those into BuildingTiers
in the same pass so there's ONE source of truth.)

### 4. Yarn command — `DialogueCommandBridge.RegisterCommands()`
`Reg("TryUpgradeBuilding", (Action<string,int>)CmdTryUpgradeBuilding);`
`CmdTryUpgradeBuilding(id, tier)`: look up tier cost → `EconomyService.TrySpend(cost)` → on success set
`GameState.BuildingTiers[id]=tier`, save, `ModifierService` recompute+fire Changed, set Yarn `$upgradeOk`;
on fail set `$upgradeOk=false`. Also set the per-building `$<id>_Level` Yarn var from BuildingTiers on
dialogue start (so the creative-authored `<<if>>` gates read correctly). Reuse `DialogueService.CurrentStructureId`.

### 5. Application hooks (reuse existing — Explore map)
- **Tower:** `DefenseTower.Damage` / `ArcaneTower.Damage` → multiply by `ModifierService.Active.towerDamageMult`
  at fire (add `GetEffectiveDamage()`); range similarly.
- **Troops:** `TroopDeployer.SpawnFromArmy` already applies `PlayerTroop.DamageMultiplier` → fold in
  `troopDamageMult`; add `TroopController.ApplyHealthMultiplier(troopHealthMult)`. **These are what carry into
  raids** — `TroopDeployer` runs in the raid scene, so reading `ModifierService.Active` there = upgrades assist
  raids automatically.
- **Production:** `ResourceBuildingState.CurrentEffectiveYield` × productionMult; offline × offlineBonusMult.

### 6. Scene-creation modifier override (owner: "all scene creations accept an override modifier JSON")
Scene builders/generators (`RaidBaseGenerator`, `RaidGarrisonSpawner`, castle/`VillageSceneBuilder`,
`SceneConfig`) gain an OPTIONAL modifier-JSON parameter/field; when present, `ModifierService.SetOverride(json)`
for that scene so the perks apply as authored/tested. Default = null → use the live `Active` (player's real
upgrades). Wire into `SceneConfigCatalog` (the raid contract) so a raid config can carry a modifier override.

### 7. Dev menu (owner: "unlock dev menu tools to control upgrades activated so i can modify at start")
Add to the dev panel (`DevPanelController` / `AdminOverlay`, [[playable-loop-exists-but-scene-gated]]):
- Per-building tier setter (0-4) — sets `GameState.BuildingTiers`, recompute.
- "Apply modifier JSON override" (paste/load a GameModifiers JSON) + "Clear override".
- "Max all" / "Reset all" buttons.

## Yarn
Authored by the creative agent (dispatched this session): 5 building node trees, 4 tiers, evolving flavor,
`<<command>>TryUpgradeBuilding("<id>",<tier>)<</command>>`, `$<id>_Level` gates, Back→structure menu. CLI wires
the command + the `$..._Level` var injection to match the agent's naming convention.

### 8. Leveled dialogue title (owner 2026-06-14: "let players know what level they're at")
`PlayStructure` seeds the Yarn title var `$structureName` from the sign label (DialogueService.cs:214).
Extend it: if `GameState.BuildingTiers` has a tier for the structureId, set
`$structureName = "<label> — Level <N>"` (Level 0 → plain label or "— Locked"; non-upgradable → unchanged).
So EVERY building dialogue header reads e.g. "Lumbermill — Level 2" and updates the instant a tier is bought.
Village→Core read is allowed.

## Acceptance criteria
- [ ] Building dialogue title shows current level (e.g. "Lumbermill — Level 2"); Level 0 shows name/Locked; updates after a purchase.
- [ ] Upgrading a building in Yarn deducts the correct Wood/Food/Crystal and bumps its tier (persists across save/load).
- [ ] `GameModifiers` computed from tiers; an empty contract is a no-op (1.0 mults).
- [ ] Tower damage, troop damage+health, and resource production visibly change with the relevant tier.
- [ ] **Troop modifiers apply inside a raid scene** (deploy in a raid → troops carry the Armorer/Forge bonuses).
- [ ] Scene creation accepts an override JSON; passing one forces those perks; null = live player upgrades.
- [ ] Dev menu can set any building tier + apply/clear a modifier-JSON override at start.
- [ ] Save migrates v22→v23 cleanly (empty BuildingTiers on old saves; no field reorder).
- [ ] WebGL-safe (CanonicalJson for the tier table + any modifier JSON).

## What NOT to touch
- Do NOT reorder existing SaveSchema fields (append v23 only). One field this pass.
- Do NOT greenfield a new economy/spend path — reuse `EconomyService.TrySpend` / `ResourceBuildingState`.
- Do NOT hand-edit `Village.unity` or bake with the editor open.
- Keep tier costs/bonuses in DATA (catalog), not hard-coded constants.
