<!-- era-sweep-2026-08-17 -->
> ### ⚠ AGED 2026-08-17 — still READY, but unverified since 2026-06-22
> The 2026-08-17 era sweep found **no evidence** that this WO's subject was deleted or superseded, so its **Status stays READY** and nothing else was changed. It is simply OLD (git first-add 2026-06-22) and has not been re-verified against current canon (`CANON_GROUND_TRUTH_*.md`, CLAUDE.md §7). **Re-verify before pulling it.**

# WORK ORDER 334 — Tower Placement Rotate Menu (Preview & Rotate)

**Status:** CLOSED — DEPRECATED, audit-verified obsolete (2026-08-21 backlog audit).
**Lane:** 11 (Build Mode / Player Base) — code-only, parallel-safe
**Priority:** MEDIUM — polish; placement currently snaps towers with no rotation preview
**Visual spec:** Interactive HTML mockup reviewed and approved by owner (2026-06-07)

---

## What This Is

A modal UIElements panel that opens when the player initiates tower placement.
It shows a live 3D preview of the selected tower and lets them dial in
X/Y/Z rotation before committing the placement.

---

## Visual Design (approved mockup)

```
┌─ runic top strip ──────────────────────────────────────────┐
│ ┤ [Preview & Rotate]  TOWER PLACEMENT              [🔨]   │
│   ┌──────────────────────────────────────────────────┐     │
│   │            Z                                     │     │
│   │     −45°  🏰tower  15°     (3D viewport)        │     │
│   │  perspective grid floor · starfield bg           │     │
│   └──────────────────────────────────────────────────┘     │
│  X Axis (Pitch) ────●────────────────── [35°] [↺]         │
│  Y Axis (Yaw)  ────────●────────────── [−45°] [↺]         │
│  Z Axis (Roll) ────●────────────────── [15°]  [↺]         │
│  [🏰 Stonebelly Troll Outpost · TIER III]     125 SKR     │
│  Snap: [45° ▾]                    [Confirm Placement]      │
│  [Cancel]                          [Reset Rotation]        │
└─ runic bottom strip ───────────────────────────────────────┘
```

Colour palette:
- Panel bg: #0c1625   Rune border: #9a7420   Title gold: #eec848
- Viewport bg: #050c18  Grid: #102010  Star particles: #d4b840
- Confirm btn: #9a6e0c bg, #d4a028 border, #fff8e0 text (Cinzel font)
- Cancel: #1a0c06 bg, #b07838 text    Reset: #06101a bg, #4878a8 text
- X/Pitch accent: #d04040    Y/Yaw accent: #38b838    Z/Roll accent: #3878c0
- Deg readout boxes: #050c18 bg, #38280e border, #eec848 text

---

## API

```csharp
// Open from TowerBuildSystem when player picks a tower to place
public void Open(
    TowerData       towerData,        // prefab ref, display name, tier
    double          costSkr,          // displayed cost
    Quaternion      initialRotation,  // current snap-to-grid rotation
    Action<Quaternion> onConfirm,     // called with final rotation; caller places the tower
    Action          onCancel          // caller cancels placement
)
public void Close()
```

---

## Implementation Notes

### Procedural UIElements only — NO UXML
Per CLAUDE.md §8. Build every VisualElement in C#.

### 3D Viewport
Use a **RenderTexture** pipeline:

```csharp
// 1. Spawn an off-screen camera on layer "TowerPreview" (add layer if missing)
// 2. Instantiate towerData.Prefab on that layer at a fixed world position
// 3. new RenderTexture(512, 512, 16) → assign to previewCam.targetTexture
// 4. Convert to Texture2D each frame via ReadPixels, OR use
//    StyleBackground with a RenderTexture directly (Unity 6 supports it)
// 5. Apply as ve.style.backgroundImage = new StyleBackground(rt)
// 6. On slider change: previewGO.transform.rotation = Euler(x, y, z)
// 7. On Close(): Destroy previewGO + previewCam + rt
```

If RenderTexture→UIElements proves unreliable in this Unity 6 build,
fall back to a plain `Label` showing the tower name + icon sprite.
Leave a `// TODO: live preview` comment so it's easy to upgrade later.

### Rotation Axes

| Axis | Label | Slider range | Accent colour |
|------|-------|-------------|---------------|
| X    | Pitch | −180 → +180 | #d04040       |
| Y    | Yaw   | −180 → +180 | #38b838       |
| Z    | Roll  | −180 → +180 | #3878c0       |

Initial values come from `initialRotation` (decomposed to Euler).

### Snap Logic

```csharp
private int _snapDegrees = 45; // 0 = off, 15, 45, 90

private float SnapAngle(float raw) =>
    _snapDegrees == 0 ? raw
    : Mathf.Round(raw / _snapDegrees) * _snapDegrees;
```

Snap applied on slider `RegisterValueChangedCallback` before updating
the preview rotation and degree-readout label.

### Per-Axis Reset Buttons
Small [↺] button beside each degree readout. Resets that axis to
the component value from `initialRotation`. Does NOT reset other axes.

### Runic Border Decoration
Four thin `VisualElement` strips (top, bottom, left, right) containing a
`Label` with Elder Futhark–style characters:
`ᚨ ᚠ ᛗ ᚱ ᛞ ᛊ ᚲ ᛚ ᛈ ᚺ ᛜ ᛒ ᛖ ᚾ ᚢ ᛁ`
Repeated to fill. Font-size 8px, colour #6a4e14, letter-spacing 3px.
Left/right strips use a 90° rotation via `transform.rotation`.

### Tower Info Bar
```
[thumbnail 36×36]  [name + tier label]             [cost SKR]
```
Thumbnail: load from `towerData.PreviewSprite` (Sprite asset).
If null, show a fallback tower SVG or a coloured square.
Cost: formatted as `$"{costSkr:F0} SKR"` in #eec848.

### Confirm Button
Gold flat button (no gradient). Use Cinzel font via `style.unityFont`
(pre-import Cinzel-Regular.ttf into `Assets/_Modules/Village/Fonts/`
or fall back to the default serif — do not download at runtime).

### Confirm flow
```csharp
private void OnConfirmClicked()
{
    var finalRotation = Quaternion.Euler(
        SnapAngle(_xDeg), SnapAngle(_yDeg), SnapAngle(_zDeg));
    Close();
    _onConfirm?.Invoke(finalRotation);
}
```

### Cancel / Reset Rotation
- **Cancel**: `Close(); _onCancel?.Invoke();`
- **Reset Rotation**: all three sliders → initial Euler values; preview updates.

---

## Files to Create / Edit

```
Assets/_Modules/Village/UI/TowerPlacementRotateMenu.cs  ← NEW (main panel)
Assets/_Modules/Village/UI/TowerPreviewCamera.cs        ← NEW (RT pipeline helper)
Assets/_Modules/Village/Fonts/                          ← add Cinzel-Regular.ttf if available
```

Do NOT edit:
- Village.unity, VillageSceneBuilder, TowerSwapMenu, TowerSwapService
- Any monetization code

---

## Acceptance Criteria

- [ ] Panel opens when `Open()` is called; dark navy + gold rune-border aesthetic
- [ ] 3D viewport shows the tower (live rotation OR static icon fallback, clearly labelled)
- [ ] Sliders update degree readouts in real-time; preview rotates to match
- [ ] Per-axis reset buttons restore that axis to `initialRotation` component
- [ ] Snap dropdown (Off/15°/45°/90°) snaps readouts and preview rotation
- [ ] Tower name, tier label, and SKR cost display correctly from `TowerData`
- [ ] Confirm closes panel and calls `onConfirm(finalQuaternion)`
- [ ] Cancel closes panel and calls `onCancel()`
- [ ] Reset Rotation restores all axes to `initialRotation` values
- [ ] No UXML files referenced anywhere in the implementation
- [ ] Brace-balance check passes on all edited .cs files

> **AUDIT 2026-08-21 (agent fleet, read-only):** DEPRECATED. Evidence: `BuildModeController.cs:137-144 (WO-673)` — in-place rotate shipped instead. Status was flipped from READY by the AUDIT, not by an implementation pass. The body below is left intact; if this call is wrong, the evidence cited here is what to challenge.
