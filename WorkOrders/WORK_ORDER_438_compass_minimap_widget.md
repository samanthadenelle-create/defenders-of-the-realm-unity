# WO-438 — P2 UI: Compass widget — replace broken nine-slice "SE ▲" panel

**Status:** READY TO IMPLEMENT  
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
