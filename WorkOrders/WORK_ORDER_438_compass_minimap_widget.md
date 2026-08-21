<!-- era-sweep-2026-08-17 -->
> ### ⚠ AGED 2026-08-17 — still READY, but unverified since 2026-07-04
> The 2026-08-17 era sweep found **no evidence** that this WO's subject was deleted or superseded, so its **Status stays READY** and nothing else was changed. It is simply OLD (git first-add 2026-07-04) and has not been re-verified against current canon (`CANON_GROUND_TRUTH_*.md`, CLAUDE.md §7). **Re-verify before pulling it.**

> ⚠ **UNRESOLVED NUMBER COLLISION — WO-438 is claimed by more than one file and OWNERSHIP IS NOT DECIDED.**
> Co-claimants: `WORK_ORDER_438_global_tech_skin_rollout.md` (06-13, first-on-disk), `WORK_ORDER_438_base_loop_rca_fixes.md` (06-17), `WORK_ORDER_438_compass_minimap_widget.md` (07-04)
> **This is one of a four-number group (WO-437 / 438 / 439 / 440) that collided the same way.** The June
> files are **first-on-disk**; the 2026-07-04 files are the ones **git history says shipped** — commit
> `0b0e0915c` reads *"UI-100% wave 1 — shared-kit parchment fix, WO-437/438/439/440, per-screen match"*,
> which names the 07-04 UI batch, and `aa931577b` separately records *"WO-437/438 landed"*. First-on-disk
> and referenced-by-commit point at DIFFERENT files, so the project rule resolves to neither.
> Flagged (not resolved) by the 2026-08-16 Sunday board-grooming pass — needs an **owner ruling**, ideally
> one ruling for all four at once. Nothing renumbered or deleted. Cite by FILENAME, never by bare number.

# WO-438 — P2 UI: Compass widget — replace broken nine-slice "SE ▲" panel

**Status:** DONE — audit-verified as shipped (2026-08-21 backlog audit).
**Priority:** P2  
**Lane:** 4 UI/HUD  
**Minted:** 2026-07-03

---

## Bug

The top-center HUD element shows "SE ▲" in a wide tan/golden nine-sliced panel that
renders incorrectly — the nine-slice stretches awkwardly, the content is sparse, and
it clashes with the Obsidian design language.

## Blink Obsidian research result

No dedicated Compass prefab exists in the Blink Obsidian pack.
The closest available widget is:
**`Assets/Blink/Art/UI/Obsidian_UI/Prefabs_Obsidian/Minimap.prefab`**

CLI should read `Minimap.prefab` in full before implementing:
- If it contains a compass rose or directional indicator sub-component → extract and
  use it for the HUD compass element
- If it's purely a map viewport → build a minimal code-built compass instead (see below)

## Fallback: code-built compass

If `Minimap.prefab` has no usable compass sub-component, build a compact compass
indicator in `ElarionUiKit`:

```csharp
public static CompassHandle BuildCompass(RectTransform parent,
    Vector2 anchorMin, Vector2 anchorMax)
// Returns: CompassHandle { TMP_Text DirectionLabel, RectTransform Needle }
```

Visual spec:
- Dark Obsidian-style round or octagon frame (~80×80px), centered at top of screen
- Cardinal direction label (N/NE/E/SE/S/SW/W/NW) in white TMP, auto-size
- Optional: a thin needle Image that rotates to face north in world space
- Uses `UiStyle.Theme` colors, not hardcoded

Wire to the existing compass update call in `HudKitController` that currently drives
the "SE ▲" label. Remove the current nine-sliced Panel container entirely.

## Files to touch
- `Assets/_Modules/Core/UI/ElarionUiKit.cs` — add `BuildCompass()` if Minimap has
  no usable sub-component
- `Assets/_Modules/HUD/Kit/HudKitController.cs` — replace Panel + label with
  `BuildCompass()` call; wire existing direction update to `CompassHandle.DirectionLabel`

## Do NOT touch
- Any scene files, `VillageHudController.cs`

## Acceptance criteria
- [ ] Cardinal direction displays correctly (N/NE/E etc.) in compact Obsidian-styled widget
- [ ] No nine-sliced stretching artifact visible
- [ ] Widget is visually consistent with rest of Obsidian HUD
- [ ] Headless smoke run passes

> **AUDIT 2026-08-21 (agent fleet, read-only):** FIXED. Evidence: `HudCompassWidget.cs:28,186` — nine-slice; later reshaped WO-899. Status was flipped from READY by the AUDIT, not by an implementation pass. The body below is left intact; if this call is wrong, the evidence cited here is what to challenge.
