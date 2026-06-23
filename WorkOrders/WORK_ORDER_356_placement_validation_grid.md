# WO-356: Placement Validation Messages & Grid Toggle

**Status:** READY TO IMPLEMENT  
**Estimated Effort:** P1 (1–2 days)  
**Priority:** High (UX clarity)  
**Lane:** HUD/UI (parallel to WO-352–355)

---

## Overview

Replace silent red/green ghost feedback with explicit validation messages. Add optional grid visualization toggle (G key). Add rotation indicator showing current yaw (0°/90°/180°/270°). Add camera pan hints (fade after 4s on entering build mode).

**Why:** Red means "blocked" but doesn't say why. "Gate clearance violation" is actionable. Grid overlay helps align structures. Rotation display prevents accidental 45° placements. Hints educate new players passively.

---

## Acceptance Criteria

- [ ] Ghost red → validation message appears ("Overlaps tower" / "Gate clearance required" / "Out of bounds")
- [ ] Ghost green → "Valid placement" message (or synergy preview from WO-354)
- [ ] Grid toggle: G key shows/hides checkerboard overlay on viewport
- [ ] Grid overlay: 2×2m cells, visible as subtle checkerboard, auto-hides after 2s if no key press
- [ ] Rotation indicator: Shows current yaw (0° / 90° / 180° / 270°) while armed
- [ ] Camera pan hints: "WASD to pan • Scroll to zoom • R to rotate • G to toggle grid" fades after 4s
- [ ] All feedback messages fit in one line (mobile-safe)
- [ ] Grid overlay toggles without pausing placement
- [ ] Messages update in real-time as ghost moves
- [ ] Zero allocations per frame (cache strings, reuse containers)

---

## Files to Modify

### New Files
- None (integrate into existing BuildModeController feedback system)

### Existing Files
- `Assets/_Modules/Village/BuildMode/BuildModeController.cs` — Validation message logic, grid toggle, hints
- `Assets/_Modules/Village/BuildMode/GhostPreview.cs` — Update visual state on validation
- `Assets/_Modules/Village/BuildMode/BuildPaletteUI.cs` — Display validation message below viewport

### No Changes Required
- PlacementGrid (validation logic unchanged)

---

## Design Spec

### Validation Messages

**Invalid placements (red ghost):**
- "Out of bounds" — ghost outside map area
- "Overlaps tower" — cell occupied by another structure
- "Gate clearance violation" — too close to enemy spawn lane
- "Floating" — no ground support (if applicable)

**Valid placements (green ghost):**
- "Valid placement" (basic)
- "Valid placement • No overlap • Gate clearance OK" (verbose)
- + synergies from WO-354: "Valid • +8% DPS (Lumbermill)"

**Message display:**
- Below viewport, 24px tall
- Green background for valid, red/orange for invalid
- White bold text, 12px
- Updates every frame as ghost moves

### Grid Toggle

**G key behavior:**
- On press: Show checkerboard grid overlay
- On release: Hide grid (or auto-hide after 2s idle)
- Visual: 2×2m cells, subtle grey (#444 dark mode, #ccc light mode), 0.5px line weight
- Drawn on top of viewport, behind ghost

**Grid display:**
```
Grid on:  ┌─┬─┬─┬─┐
          ├─┼─┼─┼─┤
          ├─┼─┼─┼─┤
          └─┴─┴─┴─┘

Grid off: Viewport clear
```

### Rotation Indicator

**Position:** Bottom-right corner of viewport, 12px font

**Display:**
```
Yaw: 0°    (when armed, before rotating)
Yaw: 90°   (while rotating)
Yaw: 180°  (after releasing R key)
```

**Update frequency:** Once per yaw step (0/90/180/270 only, not continuous)

### Camera Pan Hints

**Display:** Bottom-left of viewport, small muted text, 12px

**Text:**
```
💡 WASD to pan • Scroll to zoom • R to rotate • G to toggle grid
```

**Behavior:**
- Fade in on entering build mode (0.5s)
- Display for 4 seconds
- Fade out after 4s (0.3s)
- Can be manually dismissed with ESC
- Never shows again if player interacts with keyboard (assume they learned it)

---

## Implementation Notes

### Validation Message Logic (BuildModeController.cs)

```csharp
private Label _validationLabel;
private float _validationMessageTime = 0;
private bool _lastGhostValid = false;

private void UpdateValidationMessage()
{
    bool isValid = IsPlacementValid();
    string message = GetValidationMessage(isValid);
    
    if (_validationLabel != null)
    {
        _validationLabel.text = message;
        _validationLabel.style.backgroundColor = isValid 
            ? ElarionUi.ColorSuccess 
            : ElarionUi.ColorDanger;
    }
    
    _lastGhostValid = isValid;
}

private string GetValidationMessage(bool isValid)
{
    if (!isValid)
    {
        // Query PlacementGrid for specific reason
        Vector2Int cell = _grid.WorldToCell(_ghost.transform.position);
        
        if (!_grid.IsInBounds(cell))
            return "Out of bounds";
        
        if (_grid.IsOccupied(cell, _armedYawSteps))
            return "Overlaps existing structure";
        
        if (!_grid.HasGateClearance(cell))
            return "Gate clearance violation (keep 3m from spawn)";
        
        return "Invalid placement";
    }
    
    // Valid placement — include synergies if WO-354 active
    var bonuses = SynergyCalculator.CalculateBonusesAtCell(cell, _grid, _grid.Occupancy);
    if (bonuses.Count > 0)
    {
        var bonusStr = string.Join(" • ", bonuses.Select(b => $"{b.displayName}"));
        return $"Valid placement • {bonusStr}";
    }
    
    return "Valid placement";
}
```

### Grid Toggle (BuildModeController.cs)

```csharp
private bool _gridVisible = false;
private VisualElement _gridOverlay;
private float _gridAutoHideTimer = 0;

private void Update()
{
    // ... existing logic ...
    
    // Grid toggle
    if (Input.GetKeyDown(KeyCode.G))
    {
        _gridVisible = !_gridVisible;
        _gridAutoHideTimer = 2f;  // Auto-hide after 2s idle
        
        if (_gridOverlay != null)
            _gridOverlay.style.display = _gridVisible ? DisplayStyle.Flex : DisplayStyle.None;
    }
    
    if (_gridVisible)
    {
        _gridAutoHideTimer -= Time.deltaTime;
        if (_gridAutoHideTimer <= 0)
        {
            _gridVisible = false;
            if (_gridOverlay != null)
                _gridOverlay.style.display = DisplayStyle.None;
        }
    }
}

private void BuildGridOverlay()
{
    _gridOverlay = new VisualElement { name = "grid-overlay" };
    _gridOverlay.style.position = Position.Absolute;
    _gridOverlay.style.left = 0;
    _gridOverlay.style.top = 0;
    _gridOverlay.style.right = 0;
    _gridOverlay.style.bottom = 0;
    _gridOverlay.style.display = DisplayStyle.None;
    
    // Draw grid lines using repeated background pattern or DrawTexture
    // For simplicity: overlay with semi-transparent checkerboard SVG background
    _gridOverlay.style.backgroundImage = new StyleBackground(gridCheckerboardTexture);
    
    _viewportElement.Add(_gridOverlay);
}
```

### Rotation Indicator (BuildModeController.cs)

```csharp
private Label _rotationLabel;

private void UpdateRotationIndicator()
{
    if (!string.IsNullOrEmpty(_armed?.id))
    {
        float yaw = _armedYawSteps * 90f;  // 0, 90, 180, 270
        if (_rotationLabel != null)
        {
            _rotationLabel.text = $"Yaw: {yaw}°";
            _rotationLabel.style.display = DisplayStyle.Flex;
        }
    }
    else
    {
        if (_rotationLabel != null)
            _rotationLabel.style.display = DisplayStyle.None;
    }
}
```

### Camera Pan Hints (BuildModeController.cs)

```csharp
private Label _hintsLabel;
private float _hintsFadeTimer = 0;
private bool _playerInteractedWithKeyboard = false;

public void Enter()
{
    // ... existing logic ...
    
    ShowHints();
}

private void ShowHints()
{
    if (_hintsLabel == null) return;
    
    _hintsLabel.style.opacity = 1;
    _hintsFadeTimer = 4f;
    _playerInteractedWithKeyboard = false;
}

private void Update()
{
    // ... existing logic ...
    
    // Hints fade-out timer
    if (_hintsFadeTimer > 0)
    {
        _hintsFadeTimer -= Time.deltaTime;
        if (_hintsFadeTimer <= 0)
        {
            FadeOutHints();
        }
    }
    
    // Dismiss hints if player uses keyboard
    if (!_playerInteractedWithKeyboard && 
        (Input.anyKey && !Input.GetMouseButton(0) && !Input.GetMouseButton(1)))
    {
        _playerInteractedWithKeyboard = true;
        FadeOutHints();
    }
}

private void FadeOutHints()
{
    // Fade out over 0.3s
    var fadeAnim = new StylePropertyAnimationEvent { ... };
    // Or: _hintsLabel.style.opacity = 0 (instant)
}
```

---

## Testing Checklist

- [ ] Ghost red → message says why (overlap/clearance/bounds)
- [ ] Ghost green → "Valid placement" appears
- [ ] Message updates real-time as ghost moves
- [ ] G key toggles grid on/off
- [ ] Grid auto-hides after 2s idle (hold G to keep visible)
- [ ] Rotation indicator shows 0°/90°/180°/270° (not intermediate angles)
- [ ] Hints appear on entering build mode
- [ ] Hints fade after 4s or on first keyboard press
- [ ] All text fits one line (mobile safe, <300px width)
- [ ] Zero allocations per frame (profile)
- [ ] Works in WebGL

---

## What NOT to Touch

- PlacementGrid validation logic (use existing checks)
- Ghost red/green coloring (visual feedback already exists)
- GhostPreview tinting (keep as-is)

---

## Dependencies

- **Depends on:** WO-108 (BuildModeController, GhostPreview), WO-354 (optional synergy display)
- **Unblocks:** None
- **Parallel:** WO-352–355

---

## Acceptance Sign-Off

- [ ] Brace balance check passed
- [ ] All feedback messages clear & actionable
- [ ] Grid toggle responsive (no lag)
- [ ] Rotation display accurate
- [ ] Hints educational & non-intrusive
- [ ] Works in WebGL build
