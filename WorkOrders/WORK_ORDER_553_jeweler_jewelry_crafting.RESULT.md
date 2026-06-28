# WORK ORDER 553 — RESULT (Jeweler: Gem + Jewelry Crafting Station)

**Status: IMPLEMENTED (pending CLI gate + headless DataRegression verify + owner felt-test).**
Date: 2026-06-28. Implemented in worktree `agent-ab3709b05206e2329` (ff-merged to branch tip
`wip/village2-and-f8-tickets` @ 2a6bac4e before work).

## What was built (mirrors the Apothecary lane exactly)

- **Recipes data** (own lane file, Resources + StreamingAssets in sync, JSON-valid):
  `Assets/Resources/Data/Canonical/jeweler-recipes.json` (+ StreamingAssets copy). 6 recipes,
  TIER-UP model (base accessory + gems -> higher-rarity accessory).
- **Catalog:** `Assets/_Modules/Village/Crafting/JewelerRecipeCatalog.cs` (mirrors
  GearCraftingRecipeCatalog: Newtonsoft + CanonicalJson, Find/All/Reload; graceful empty).
- **Service:** `Assets/_Modules/Village/Crafting/JewelerCraftingService.cs` (mirrors
  GearCraftingService: shared Evaluate() for CanCraft/WhyCannotCraft/Craft; atomic verify ->
  TrySpend once -> consume base + gems -> grant output accessory; full rollback on any
  consume failure; optional QuestService gate; OnCrafted event).
- **VM:** `Assets/_Modules/Village/Items/JewelerVM.cs` (mirrors CraftingVM; reuses
  CraftIngredientVM; output/base via GearCatalog.FindAccessory, gems via MaterialCatalog;
  cost line; subscribes VillageInventory.Changed).
- **View:** `Assets/_Modules/Village/Items/JewelerPanelMvvm.cs` (shared Obsidian chrome
  BuildObsidianPanel; 3-col card grid; "Set Gems"/"Need Gems" button; registers
  PanelId.JewelerCrafting).
- **Bootstrap:** `Assets/_Modules/Village/Items/JewelerPanelBootstrap.cs` (sibling of
  CraftingPanelBootstrap; hub-gated via HubScenes.SuppressTownHud; global dedupe).
- **Station:** `Assets/_Modules/Village/Items/JewelerStationInjector.cs` (mirrors
  CraftingStationInjector; DDOL singleton, navmesh-snap, VisualFactory.Skin "Structures/jeweler"
  + placeholder-cube fallback; Building type JewelersBench id "jewelers-bench" at courtyard
  west (-11,0,2); opens PanelId.JewelerCrafting directly).
- **Routing:** PanelRouter `JewelerCrafting = 10`; Building `BuildingType.JewelersBench = 9`;
  BuildingInteractable TryPanelFor + LabelFor("Jeweler"); StructureHookIdFor returns null for
  it (falls through to the panel, no Yarn).
- **Regression:** `Assets/Editor/Regression/DataRegression.cs` `CheckJewelerChain` — HARD per
  recipe (output/base resolve as accessories; gems resolve in MaterialCatalog), SOFT gem
  droppability, + a HARD simulated craft (seed -> Craft -> assert consume/debit/grant + no-funds
  rollback) on the first iron-only recipe (no GameState dependency).

## Recipes authored (all output + base ids verified present in accessories.json)

| id | base | gems | output | cost |
|---|---|---|---|---|
| jewel_ring_steadfast | ring_iron | ing_ember_crystal x2 | ring_steadfast | iron 30 |
| jewel_ring_embercoil | ring_steadfast | ing_ember_crystal x2, ing_aether_shard x1 | ring_embercoil | iron 60, crystals 10 |
| jewel_ring_heartward | ring_embercoil | ing_aether_shard x2, ing_heartstone_crystal x1 | ring_heartward | iron 100, crystals 25 |
| jewel_amulet_oathward | amulet_travelers | ing_ember_crystal x2 | amulet_oathward | iron 40 |
| jewel_amulet_lastpressing | amulet_oathward | ing_aether_shard x2, ing_ember_crystal x1 | amulet_lastpressing | crystals 20 |
| jewel_amulet_elarion | amulet_lastpressing | ing_aether_shard x2, ing_heartstone_crystal x1 | amulet_elarion | iron 80, crystals 40 |

Gems = existing crystal ingredients (owner decision 2026-06-28): `ing_ember_crystal`,
`ing_aether_shard`, `ing_heartstone_crystal` (no new gem family authored; loot-tables.json NOT
touched — boss gem-drops owned by a separate agent).

## Owner-decision flags
- **Gating:** station ships UNFLAGGED (matches Apothecary). Confirm if an `ff.jeweler` gate is wanted.
- **Gems per craft:** 2-3 per recipe; consume model (not sockets).
- **Currency cost:** iron + crystals (unified wallet). Legendary outputs (ring_firstlight /
  amulet_heartstone) deferred to a follow-up behind a quest gate.
- **Gem droppability is SOFT in regression** (logs, never fails) since boss-drop wiring is a
  parallel lane — flip to HARD once that lands.

## Gate results (local)
- Brace balance: all 10 touched .cs files OK.
- JSON: jeweler-recipes.json valid, Resources/StreamingAssets copies byte-identical.
- CLI to run CompileGate + DataRegression headless before commit.
