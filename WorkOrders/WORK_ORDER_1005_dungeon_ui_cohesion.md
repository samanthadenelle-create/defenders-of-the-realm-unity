> ## RECONCILED 2026-08-08 - true status is NOT STARTED
> Audit `docs/reference/WO_TRUE_STATUS_2026-08-08.md`. Evidence: no descend-prompt UI exists at all;
> `DungeonExitInteractable.cs:249` still sets `tm.text = "EXIT"` and the beacon billboard is unchanged.
> The previous Status line read "READY TO IMPLEMENT" and was CORRECT - this WO was checked on
> 2026-08-08 and confirmed accurate, not skipped.

# WORK ORDER 1005 — Dungeon UI cohesion: Descend button + EXIT label to the Obsidian kit

**Status:** FIXED 2026-08-16 - awaiting owner felt-verify. ⭐ The crafting-panel UXML rebuild that this line used to carry as a trailing "remains as follow-up" is now **WO-1182**, because a slice buried inside a Done-bucketed status is invisible and unhandable (the WO-1181 class).
>  PRIOR: **Status:** IMPLEMENTED - PENDING GATE (2026-08-16; fixes 1+2 had already landed via other WOs, fix 3's last live gap - the UXML oil HUD - rebuilt on the Obsidian kit this pass; crafting panel UXML rebuild remains as follow-up) · **Silo:** Dungeons/UI · **For:** CLAUDE CLI · **Date:** 2026-08-07

> ## RECONCILED 2026-08-16 - state of the three breaks at implementation time
> 1. **Descend purple panel: ALREADY FIXED** by commit 16cefd72c ("the purple interact plate goes -
>    Obsidian kit, town and dungeon alike") - the Descend prompt IS the shared MobileInteractButton
>    (DungeonPortLink routes through it), which now wears the Obsidian kit face.
> 2. **Mirrored EXIT: MOOT + FIXED** - the "EXIT" word was REMOVED entirely (owner ruling 2026-08-14,
>    commit 64ebf6658 "unlabelled"); the surviving world labels (WO-957 leave pads, WandererBubble)
>    billboard with the correct handedness (`LookRotation(pos - cam.pos)`, forward away from camera).
> 3. **One Obsidian theme: the last off-kit player-facing surface was the UXML lantern-oil HUD**
>    (DungeonHudController + DungeonHud.uxml - also blank in player builds, the sec.8 UXML landmine).
>    Rebuilt 2026-08-16 as code-built kit uGUI (obsidian card + ObsidianBar + ToastCard low-oil pill).
>    The WandererBubble parchment speech bubble matches the house TownsfolkBubble idiom - cohesive as-is.
>    REMAINS: CraftingPanelController is still UXML (cottage-only crafting modal) - follow-up slice.
**PO:** Samantha (owner) · **Author:** UI seat · **UI-seat block:** 1000–1099
**Owner (felt-test 2026-08-07):** "needs cohesion." The composed dungeon's UI/labels don't match the game's obsidian+gold theme — they read as placeholders.
**Complements:** WO-1004 (dungeon art/materials/enclose), WO-899 (HUD), WO-1001 (descent mechanic).

## 0. The cohesion breaks (from the screenshot)
1. **"Descend" button = a flat bright-PURPLE panel.** The multi-level descent prompt (WO-1001 stair mechanic) is a raw violet rectangle — completely off the game's **obsidian + gold** UI language (every other button, the action bar, the Manage screen use the ElarionUiKit obsidian style). It reads as debug/placeholder.
2. **"EXIT" text renders MIRRORED** ("TIX3" / reversed "MOZE"). The world-space exit label is flipped — a facing/scale bug (likely a negative-scale parent, or a billboard that faces *away* from camera, or text on a back-face).
3. (Carried from WO-1004: the purple/magenta wall-top markers + rainbow + open-sky greybox — fixed there. This WO is the UI/label cohesion layer on top.)

## 1. Fixes
1. **Descend button → the Obsidian kit.** Rebuild the descend prompt with `ElarionUiKit` (obsidian dark plate + gold rim/text — the same `BuildObsidianButton`/kit style the action bar, Manage, and CTAs use). Find the descend-UI build site (the stair/descent interactable prompt from the WO-1001 mechanic) and route it through the kit. **No flat purple panel** — it should look like it belongs to the game.
2. **Fix the mirrored EXIT label.** Find the world-space "EXIT" text (likely `DungeonExitInteractable` / the exit portal label). Make it **face the camera correctly and read left-to-right** — billboard toward the camera with correct handedness (no negative-scale flip, no back-face). Same for any other mirrored world label (the reversed text behind the Descend panel).
3. **One theme for ALL dungeon overlays.** Any dungeon-specific label/prompt/marker the player sees (descend, exit, interact prompts, room callouts) uses the **Obsidian kit** (dark + gold, ASCII-safe text, colourblind shape-first) — never a raw coloured box or a bare TMP string. Establish this so future dungeon UI is cohesive by default.

## 2. Acceptance
- [ ] The **Descend** prompt is an obsidian+gold kit button (matches the action bar / Manage / CTAs), not a flat purple panel.
- [ ] The **EXIT** label (and any other world label) reads **correctly forward-facing**, not mirrored/backwards.
- [ ] All player-facing dungeon prompts/labels use the Obsidian kit theme — one consistent look.
- [ ] `COMPILE_GATE_OK` + `REGRESSION_OK` + `UI_CAPTURE_OK` — headless-capture the dungeon with the Descend prompt + the exit, open the PNG, confirm cohesion.
**Owner felt-close:** the dungeon UI reads as one game — obsidian+gold throughout, no placeholder purple, no backwards text.

## 3. RESULT
`WorkOrders/WORK_ORDER_1005_dungeon_ui_cohesion.RESULT.md` — the descend-button reskin, the mirrored-text fix (what caused it), and a before/after screenshot.
