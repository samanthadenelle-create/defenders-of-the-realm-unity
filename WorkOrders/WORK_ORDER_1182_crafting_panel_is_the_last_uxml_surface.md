# WO-1182 - The dungeon crafting modal is the LAST UXML player-facing surface, and UXML is blank in builds

**Status:** READY TO IMPLEMENT. **Silo:** Dungeons/UI.
**Origin:** split out of **WO-1005** on 2026-08-24. ⚠ It was living as a **follow-up sentence inside a
ticket bucketed Done** (`IMPLEMENTED - PENDING GATE ... crafting panel UXML rebuild remains as
follow-up`) - so the board rendered it green and the slice was unhandable. That is exactly the
self-contradicting-status class filed as **WO-1181**; splitting it out is the fix, not a scope note
bolted onto a Done row.

## ⛔ Why this is not cosmetic

**UXML DOES NOT WORK IN PLAYER BUILDS** (CLAUDE.md §8, learned the hard way). It renders in the
editor and comes up **BLANK on device** - which is how the lantern-oil meter shipped invisible and
why `7c103775a` exists.

Verified at HEAD: `Assets/_Modules/Dungeons/UI/CraftingPanelController.cs:30` is still
`[RequireComponent(typeof(UIDocument))]`, with `CraftingPanel.uxml` + `.uss` beside it. **So dungeon
crafting is blank on the owner's Seeker right now.**

## ⭐ The pattern is proven - copy it, do not invent one

**`7c103775a`** (*"the lantern oil meter was BLANK in every player build"*) rebuilt
`DungeonHudController` the same way: **one file, 205 insertions**, self-builds its own Canvas at
runtime, and **tolerates the legacy `UIDocument` seat** rather than requiring a scene edit.
`DungeonHudController.cs:210` already reserves the crafting sub-tree for exactly this follow-up.

## Scope

1. Rebuild `CraftingPanelController` as **code-built Obsidian-kit uGUI** - `ElarionUiKit`, the same
   kit every other player surface uses.
2. ⛔ **KEEP THE MVVM SEAM.** `DungeonCraftVM` (`Assets/_Modules/Dungeons/UI/DungeonCraftVM.cs`) and
   `CraftRecipeVM` (`Core/UI/Mvvm/CraftRecipeVM.cs`) already exist and stay. **The View reads NO game
   state** - it binds to the VM and nothing else. A View that reaches for game state is how the
   presentation layer starts touching the objects (`ARCHITECTURE_PRINCIPLES.md`).
3. Drop `[RequireComponent(typeof(UIDocument))]`; self-build the Canvas at runtime.
4. Retire `CraftingPanel.uxml` and `CraftingPanel.uss` **and their `.meta` files together** - §4:
   moving or deleting assets without their meta is its own class of breakage.

## ⛔ Do NOT

- ⛔ **No scene or prefab edits.** The pattern deliberately tolerates the existing seat.
- ⛔ Do not redesign the crafting flow. **Same recipes, same actions, same VM** - this is a rendering
  substrate change and nothing else.
- ⛔ Do not add a second crafting entry point.

## Acceptance

- [ ] Zero `UIDocument` / `.uxml` references remain under `Assets/_Modules/Dungeons/UI/`
- [ ] `DungeonCraftVM` is unchanged; the View touches no game state (assert by source lint)
- [ ] ⚠ **Proven by a CAPTURED PNG that is actually opened** - `UI_CAPTURE_OK` plus eyes. A compile
      cannot prove a panel renders, and this exact bug class survived every gate last time.
- [ ] The `.uxml`/`.uss` and their `.meta` files are removed together
