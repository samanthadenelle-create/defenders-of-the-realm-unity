# WORK ORDER 1005 — Dungeon UI cohesion: Descend button + EXIT label to the Obsidian kit

**Status:** READY TO IMPLEMENT · **Silo:** Dungeons/UI · **For:** CLAUDE CLI · **Date:** 2026-08-07
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
