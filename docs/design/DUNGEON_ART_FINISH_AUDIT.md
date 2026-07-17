# Dungeon Interior Art-Finish Audit — Healer's Cottage (D1)

Date: 2026-07-16
Owner felt-test (F8 in `Dungeon_HealersCottage`): "inside dungeon is bad, thats why
dungen pack maybe and a dungeon kit for assets."

## Verdict (honest, per CLAUDE.md sec 12)

This is an **art-finish** problem, not a layout problem. The dungeon is built,
flag-ON, and gameplay-complete (12 rooms / 3 levels / lore stones / checkpoints /
scripted encounters / mini-boss / chests / crafting all wired). What reads as "bad"
is **placeholder primitives** (labelled cubes + capsules) standing in for props the
build hadn't finished dressing, plus two NPC/enemy capsules.

**DunGen (procedural LAYOUT) would NOT fix this** — the layout is authored and fine.
The fix is finishing the interior with the **KayKit kit the project already owns**.
Confirming the owner's own conclusion: yes, they have the kit. The Dungeon Remastered
1.1 pack is already the Cottage skeleton (floors/walls/stairs/props), and two adjacent
owned packs (RPG Tools Bits, Halloween Bits) supply the few meshes Dungeon Remastered
lacks. Nothing needs buying for the item-props; only a couple of atmosphere items
(fireplace, water, rug) have no owned mesh.

Source of truth for this audit = the actual filesystem under
`Assets/Models/KayKit/**` (verified fbx + .meta present), cross-checked against
`docs/kaykit-asset-catalog.md` / `docs/asset-inventory/01_kaykit.md`.

Builder: `Assets/Editor/DungeonSceneBuilder.cs`
Layout data: `Assets/Resources/Data/Canonical/dungeons/healers-cottage.json`
Pack root the builder loads from (`PackRoot`):
`Assets/Models/KayKit/KayKit Dungeon Remastered 1.1/Assets/fbx(unity)/`

---

## Placeholder census + resolution

`Placeholder(...)` = a tinted **cube** primitive (see `DungeonSceneBuilder.cs`
`Placeholder()` helper). `CreatePrimitive(Capsule/Cube)` = actor/VFX primitives.

### A. Dressing placeholders (cubes) — REPLACED with owned meshes

| # | Placeholder | Room | Builder site (pre-edit) | -> Owned KayKit mesh | Status |
|---|---|---|---|---|---|
| 1 | `lantern_post` | garden-approach | `DressRoom` case `garden-approach` | **RPG Tools Bits** `lantern.fbx` | REPLACED |
| 2 | `trapdoor` (lantern-revealed) | entrance-room | `DressRoom` case `entrance-room` | Dungeon Remastered `floor_tile_big_grate.fbx` (iron hatch) | REPLACED |
| 3 | `ladder up to the Loft` | main-room | `DressRoom` case `main-room` | Dungeon Remastered `scaffold_frame_large.fbx` (climb frame; approx) | REPLACED |
| 4 | `stair down` (cellar entry) | root-cellar | `DressRoom` case `root-cellar` | Dungeon Remastered `stairs_walled.fbx` | REPLACED |
| 5 | `stone sarcophagus` | crypt-sublevel | `DressRoom` case `crypt-sublevel` | **Halloween Bits** `coffin_decorated.fbx` | REPLACED |

RPG Tools + Halloween assets loaded via a new cross-pack helper `PropFrom(parent,
packRoot, fbx, ...)` and two new path consts `RpgToolsRoot` / `HalloweenRoot`. Both
FBX confirmed imported (`.meta` present) so they instantiate rather than fall back.
`scaffold_frame_large` is an APPROXIMATION (the Dungeon pack ships no true ladder);
it pairs with the existing `stairs_wood` Main->Loft vertical connector.

### B. Dressing placeholders (cubes) — KEPT (no owned mesh) => NEEDS ASSET

| # | Placeholder | Room | Why kept | NEEDS ASSET |
|---|---|---|---|---|
| 6 | `hearth fireplace` | main-room | No fireplace mesh in Dungeon Remastered / RPG Tools / Halloween / Furniture Bits | Source a fireplace mesh, OR fake with `wall_inset_candles.fbx` + a fire VFX (not a clean 1:1 swap) |
| 7 | `water puddle` | root-cellar | No water mesh; current thin blue quad reads acceptably as water | Real water plane / URP water shader / decal (owner call) |
| 8 | `rug over hidden trapdoor` | entrance-room | No rug/carpet mesh in any owned KayKit pack (Furniture Bits has none); current thin brown quad reads acceptably as a rug | Optional — source a rug/carpet mesh if a crisper read is wanted |

These three are the ONLY remaining NEEDS-ASSET items, all atmosphere (not gameplay),
and all currently render as flat tinted quads that read passably. Low priority.

### C. Actor placeholders (capsules) — RECOMMEND (do NOT guess a character mesh)

| # | Placeholder | Builder site | Recommendation (owner decides) | Status |
|---|---|---|---|---|
| 9 | `HeroBody` capsule (Keeper) | `BuildHeroRig` | **No action needed.** `HeroBodySwapper` (reflection-added) swaps this for the player's real animated Mage/Knight/Ranger FBX at runtime; the capsule is only the in-editor stand-in + collision body. | OK as-is |
| 10 | `BrynBody` capsule (the "pill NPC", WO-324) | `BuildBryn` | KayKit Adventurers 2.0 `Rogue_Hooded.fbx` (a hooded wanderer) or `Druid.fbx`. NOT swapped here: a character mesh needs rig/animator/scale/facing decisions and the playable build is meant to supply Bryn's prefab. Flag for owner. | RECOMMEND |
| 11 | `BossBody` capsule (Hollow-One mini-boss) | `BuildMiniBoss` | KayKit Skeletons 1.1 `Skeleton_Staff.fbx` (apothecary-apprentice = caster) or `Skeleton_Mace.fbx`. NOT swapped: the enemy prefab is supplied by the battle/enemy module at runtime, not this scene builder. Flag for owner. | RECOMMEND |

### D. Intentional stylized primitives — LEAVE (by design, not "bad art")

These are deliberate emissive VFX props, not unfinished placeholders:

- Checkpoint `Crystal` cube (emissive violet, `BuildCheckpoints`)
- Ingredient `MoteMesh` cube (emissive, `BuildIngredientPickups`)
- Crafting `Shard` cube (emissive violet, `BuildCraftingPedestal`)
- `InteractPrompt` cube (world-space UI cue, `BuildCraftingPedestal`)

### E. WO-324 "2-circle exit"

Not present as a primitive in the current builder. The Workshop exit is a data hook
only (`healers-cottage.json` workshop wall `leadsTo: "exit"`) — no cube/circle exit
marker is generated. Already resolved; nothing to replace.

---

## Gameplay anchors — untouched (confirmed)

Every replacement above changes ONLY a decorative `DressRoom` visual. The gameplay
wiring is built by separate methods and was not modified:
`BuildLoreStones`, `BuildCheckpoints`, `BuildEncounters`, `BuildChests`,
`BuildBryn`, `BuildMiniBoss`, `BuildCraftingPedestal`, `BuildIngredientPickups`,
`WireController`. Lore/checkpoint/encounter/crafting anchors, colliders, and the
`DungeonController` reflection wiring are intact.

Note: the checkpoint pedestal (`pillar.fbx`) and crafting pedestal
(`pillar_decorated.fbx`) already use real KayKit meshes — no change needed.

---

## Rebuild required (CLI)

`DungeonSceneBuilder` is an **editor build step** — the edits do NOT change the saved
`Dungeon_HealersCottage.unity` until the builder is re-run. The scene must be
regenerated (idempotent — it nukes + rebuilds `DungeonRoot`):

- Menu: `Defenders > Dungeons > Build Healer's Cottage (D1)`
- Batchmode: `-executeMethod DeNelle.Editor.DungeonSceneBuilder.BuildHealersCottage`

Per CLAUDE.md sec 3 / owner-pref-scenes, **CLI runs the rebuild** (agents/UI do not
fire batchmode; never hand-edit the .unity). After rebuild, the summary log's
"Placeholder primitives" count should drop by 5 (items 1-5 above).

---

## Bottom line

- The fix is the **owned KayKit kit + art finish**, NOT DunGen.
- 5 cube placeholders replaced with owned meshes (Dungeon Remastered + RPG Tools +
  Halloween Bits).
- 3 atmosphere placeholders remain (fireplace / water / rug) — NEEDS ASSET, low
  priority, currently read passably.
- 2 character capsules (Bryn, Hollow-One) — RECOMMEND owned meshes, flagged for the
  owner rather than guessed (they belong to the runtime prefab pipeline).
- Hero capsule is already swapped at runtime — no action.
- CLI must re-run the dungeon builder for the swaps to land in the scene.
