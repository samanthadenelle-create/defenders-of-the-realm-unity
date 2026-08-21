# Dungeons — `DeNelle.Dungeons`

3D dungeon gameplay (Healer's Cottage, Folk's Granary scenes).

`dg_starter_loop` quality baseline (2026-08-21): the single-floor graph contains no
stair traversal ports, closes its main route back into the junction, and carries a
side reward/oil room plus one grate hazard. Room encounters use smaller escalating
families and `OutpostEnemyGroupSpawner`'s deterministic staggered formation; challenge
comes from role composition and positioning rather than a large centre-ring pile.

`GraphDungeonComposer` writes generated layouts to both canonical locations in the
same operation. `Resources` is the runtime-winning copy, so a compose that updates
only `StreamingAssets` is invalid.

## Mobile refinement guidance

The graph/socket/bake foundation is production-worthy. Refine content through explicit
bake budgets rather than adding more runtime systems:

- Keep ordinary encounters at 2-4 actors and reserve 5-6 for large chambers. Difficulty
  should come from melee/caster/support composition, staggered seats, hazards, and wake
  timing. Never use a seven-actor centre pile as the default challenge knob.
- Keep the existing Forward-URP limit of four additional realtime lights per room.
  Prefer baked emissive materials, pooled flame VFX, and fog for atmosphere; additional
  shadow-casting point lights are prohibited on the mobile target.
- Cap cosmetic floor props per one-cell room at four, strip their colliders, and preserve
  doorway/socket clearance. Prefer room archetype-specific token palettes over raising
  the prop count.
- Treat visibility as gameplay. Oil provides a continuous visual gauge; its last thirty
  seconds shrink/flicker the light and pull fog inward. Empty oil leaves only a tight
  safety halo, while darkness enables the existing ambush pressure.
- Every content bake must prove: dual-copy layout parity, zero mate failures, a complete
  entry-to-deepest NavMesh path, no unintended stair ports/extracts, encounter seats inside
  room bounds, and declared light/enemy/prop counts in the summary.
- For longer dungeons, alternate pressure, navigation, reward, and recovery rooms. A
  branch should offer a meaningful choice (oil, treasure, lore, shortcut), not merely add
  walking distance.

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
