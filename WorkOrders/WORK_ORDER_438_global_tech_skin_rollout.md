<!-- era-sweep-2026-08-17 -->
> ### ⚠ AGED 2026-08-17 — still READY, but unverified since 2026-06-22
> The 2026-08-17 era sweep found **no evidence** that this WO's subject was deleted or superseded, so its **Status stays READY** and nothing else was changed. It is simply OLD (git first-add 2026-06-22) and has not been re-verified against current canon (`CANON_GROUND_TRUTH_*.md`, CLAUDE.md §7). **Re-verify before pulling it.**

> ⚠ **UNRESOLVED NUMBER COLLISION — WO-438 is claimed by more than one file and OWNERSHIP IS NOT DECIDED.**
> Co-claimants: `WORK_ORDER_438_global_tech_skin_rollout.md` (06-13, first-on-disk), `WORK_ORDER_438_base_loop_rca_fixes.md` (06-17), `WORK_ORDER_438_compass_minimap_widget.md` (07-04)
> **This is one of a four-number group (WO-437 / 438 / 439 / 440) that collided the same way.** The June
> files are **first-on-disk**; the 2026-07-04 files are the ones **git history says shipped** — commit
> `0b0e0915c` reads *"UI-100% wave 1 — shared-kit parchment fix, WO-437/438/439/440, per-screen match"*,
> which names the 07-04 UI batch, and `aa931577b` separately records *"WO-437/438 landed"*. First-on-disk
> and referenced-by-commit point at DIFFERENT files, so the project rule resolves to neither.
> Flagged (not resolved) by the 2026-08-16 Sunday board-grooming pass — needs an **owner ruling**, ideally
> one ruling for all four at once. Nothing renumbered or deleted. Cite by FILENAME, never by bare number.

# WORK ORDER 438 — Global "Tech hud elements" styling rollout (all screens)

**Status: READY TO IMPLEMENT (after WO-437)** · Lane 4 UI/HUD · P2 · Owner directive 2026-06-12
**Depends On:** WO-437 (combat skin + catalog roles land first). Coordinates with WO-405/411/415.
Felt change — **push only after owner retest per screen batch.**

## Owner directive
After combat (WO-437), apply the Tech hud elements design language to ALL styling across
everything. Same single seam: RpgUiCatalog roles from `Resources/RpgUi/` — never a second
sprite path, never UXML.

## Screen map (batch per row; each batch = one owner-retest gate)
| Screen | Pack source | Notes |
|---|---|---|
| Town HUD chrome (WO-411 list) | `Menu Bars 1–7`, `Tab icons`, `Level badage` | resolves WO-411 deviations via 405 kit tokens |
| Dialogue/companion panels | `D1`/`D5`/`D6` dialogue + `Ribbion` speaker banner | AFTER WO-391 layout fix lands — skin, don't re-layout |
| Vendor/shop (WO-415) | `D3` (tabbed) + `D2` buttons + `Exit` | unblocks WO-415; coordinate with WO-431 fix |
| Inventory/crafting | `D4` (tabs) + `Ui Elements/Dialogue` grid | keep 1c87a4e inventory wiring |
| Quest log/tracker | `Ui Elements/Quest log`, `tab dialogue_` | QuestTrackerHud is code-built — reskin only |
| Confirm dialogs | `Are you sure/*` (+ Design 2 paper variant) | one shared ConfirmDialog skin |
| Title / hero select | `Model selection.png`, `Play buttons`, `Background 1` | hero-pick stat cards keep 237310c layout |
| Settings/Dev panels (WO-417) | `D8` tabs + `Tab icons` | reskin the now-populated rows |
| Compass/minimap chrome | `Badges` ring + `Tab icons/icon 2` | CompassHud a61a7c0 is code-built uGUI |
| Build mode UI | `D2` buttons + `banner` chips | placement menu skin (WO-334 path, NOT BuildPreviewModal — orphaned) |
| Pets/companion select | `GreenUielements` Profiletabs + `Magic bottles` | Echo Warden flow after WO-422 dedupe |

## Rules
- Additive RpgUiCatalog keys only; existing keys stable; null lookup keeps fallback.
- Presentation never touches gameplay objects (ARCHITECTURE_PRINCIPLES §2).
- WO-405 kit = token/layout authority; this WO = skin source unification. Any layout
  change belongs to the 405 chain (403→404→400), not here.
- Batch-gate: brace check per file, `UiSpriteRefValidator.Run` clean per batch,
  COMPILE_GATE_OK on combined tree before commit; commit per batch by explicit path.

## Acceptance
- Every player-facing screen draws chrome from the pack via RpgUiCatalog; zero glyph/
  procedural fallbacks visible anywhere; validator clean; owner sign-off per batch.
