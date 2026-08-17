<!-- era-sweep-2026-08-17 -->
> ### ⚠ AGED 2026-08-17 — still READY, but unverified since 2026-06-22
> The 2026-08-17 era sweep found **no evidence** that this WO's subject was deleted or superseded, so its **Status stays READY** and nothing else was changed. It is simply OLD (git first-add 2026-06-22) and has not been re-verified against current canon (`CANON_GROUND_TRUTH_*.md`, CLAUDE.md §7). **Re-verify before pulling it.**

# WORK_ORDER_310 — Companion renders wrong color (green tint)

**Status: READY TO IMPLEMENT**
**Branch:** feat/tower-core-loop · **Lane:** 2 (Combat/AI) · **Origin:** owner playtest 2026-06-06 (screenshot)

## Problem
The story companion model renders **green** (missing/placeholder material or wrong diffuse), like the earlier
Ranger-green issue (WO-286 fixed Ranger via `HeroBodySwapper` diffuse repoint). The companion's material/texture
isn't resolving, so it falls back to a green/untextured tint.

## Goal
The companion renders with its correct material/texture, matching the other heroes.

## Likely cause / where to look
- `StoryCompanionInjector` / `HeroBodySwapper` material + diffuse assignment for the companion's class.
- A texture path that doesn't resolve (mirror the WO-286 Ranger fix: repoint to an existing loadable diffuse).
- Confirm it's not the gear/primitive path (WO-302/GearVisualApplier already disabled) and not a shader fallback.

## Acceptance criteria
- [ ] Companion renders with correct skin/material — no green/untextured tint — in village + combat.
- [ ] Root cause identified (missing texture path vs material assignment) and fixed at source, not tinted over.
- [ ] No regression to the other heroes' materials.
- [ ] Brace check; CompileGate OK; Windows build SUCCESS; verify in a play session.

## Root cause (triage 2026-06-06)
**Confidence: Confirmed.** The green companion is the **Ranger (Sylas)** and the green is a fallback tint, not
a shader fault:
- `StoryCompanionInjector.BindClassDiffuse` loads the Ranger diffuse from Resources path
  `"Heroes/Ranger_tex/remesh_12_combined_Bake_Diffuse"` (`Assets/_Modules/Village/NPCs/StoryCompanionInjector.cs:312`).
- **That path does not exist.** The actual Ranger textures live at `Resources/Heroes/Ranger.fbm/`
  (`archer_basecolor.PNG`, `archerv2_basecolor.JPEG`, …) — there is no `Heroes/Ranger_tex/` folder.
- `Resources.Load<Texture2D>(...)` therefore returns null → the method's else branch paints the signature
  class tint `TintFor(Ranger)` = `new Color(0.41f, 0.74f, 0.48f)` = **wood-green** (`:245`, `:342-347`). That is
  exactly the green the owner saw. (The Ranger companion spawns when the player is Knight, via the
  Knight→Ranger(Sylas) mapping, `:390`.)

**Suggested minimal fix:** repoint the Ranger `texPath` (`:312`) to a real loadable diffuse (e.g. the
`Heroes/Ranger.fbm/archer_basecolor` family, mirroring the WO-286 HeroBodySwapper repoint). While here,
verify the Mage/Knight (`Heroes/Textures/...`) and Cleric (`Heroes/Cleric_tex/...`) paths at `:310-313` also
resolve, or they will hit the same green/grey fallback. Don't fork HeroBodySwapper.

## Do NOT touch
- No `.unity` edits. Reuse the WO-286 diffuse-repoint pattern; don't fork HeroBodySwapper.
