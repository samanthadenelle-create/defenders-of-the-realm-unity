# Dungeons — `DeNelle.Dungeons`

3D dungeon gameplay (Healer's Cottage, Folk's Granary scenes).

## Layout

- **Root:** `DungeonController`, `DungeonHero`, `DungeonLayout`, `DungeonCameraRig`,
  `Checkpoint`, `EncounterTrigger`, `RandomEncounterTable`, `Lantern`,
  `LoreStone`/`LoreFragments`, stub encounter/return
- **`Crafting/`:** `CraftingPedestal`, `CraftingData`, `DungeonInventory`, `IngredientPickup`
- **`UI/`:** `DungeonHudController`, `CraftingPanelController`
- **`Wanderer/`:** Bryn the wandering NPC (`Bryn`, `WandererBubble`, `WandererDialogue`)
- **`State/`:** `DungeonRuntimeState`

Scenes: `Dungeon_HealersCottage.unity`, `Dungeon_FolksGranary.unity`.
Design docs: `docs/DUNGEON_DESIGNS.md`, `docs/dungeon-3d-healers-cottage-design.md`,
`docs/dungeons-storyline.md`.

> Maintenance: update this README when files are added/removed.
