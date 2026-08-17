<!-- era-sweep-2026-08-17 -->
> ### ⚠ AGED 2026-08-17 — still READY, but unverified since 2026-06-22
> The 2026-08-17 era sweep found **no evidence** that this WO's subject was deleted or superseded, so its **Status stays READY** and nothing else was changed. It is simply OLD (git first-add 2026-06-22) and has not been re-verified against current canon (`CANON_GROUND_TRUTH_*.md`, CLAUDE.md §7). **Re-verify before pulling it.**

# WO-357: Mobile Touch Gestures & Accessibility

**Status:** READY TO IMPLEMENT  
**Estimated Effort:** P1–P2 (2–4 days)  
**Priority:** High (mobile core experience)  
**Lane:** Gameplay/Input (parallel to HUD work)

---

## Overview

Implement mobile touch gestures for build mode camera control (tap-drag to pan, two-finger pinch to zoom). Ensure full accessibility compliance: safe area respect, accessibility settings on first launch, proper focus order, keyboard fallbacks, and screen reader support.

**Why:** Mobile players expect familiar gesture controls (like map apps). Accessibility ensures players with disabilities can build effectively. Focus order & keyboard fallbacks benefit all players.

---

## Acceptance Criteria

- [ ] Tap-drag on viewport pans camera (matches middle-mouse drag on desktop)
- [ ] Two-finger pinch zooms camera (matches scroll on desktop)
- [ ] Single-finger pan doesn't trigger UI buttons (separate touch zones)
- [ ] Safe area respected on all sides (env(safe-area-inset-*))
- [ ] Accessibility settings prompt on first launch (after splash screen)
- [ ] Focus order: status bar → game viewport → palette
- [ ] Keyboard fallbacks: WASD pan, Scroll zoom, R rotate, G grid, Q/W/E/R abilities (if battle HUD active)
- [ ] Color not the only indicator (red/green validated by text message WO-356)
- [ ] All text ≥11px, high contrast (≥4.5:1 WCAG AA)
- [ ] Screen reader announces placement state ("Valid placement, two synergies detected")
- [ ] No flickering on focus (≥3 Hz, WCAG 2.4.3)

---

## Files to Modify

### New Files
- `Assets/_Modules/Village/BuildMode/BuildModeTouchInput.cs` — Gesture detection (pinch, pan)
- `Assets/_Modules/Village/BuildMode/AccessibilitySettings.cs` — First-launch prompt, settings UI

### Existing Files
- `Assets/_Modules/Village/BuildMode/BuildModeController.cs` — Subscribe to touch input, route to camera
- `Assets/_Modules/Village/BuildMode/DesktopBuildInput.cs` — Existing keyboard/mouse logic (no changes, for reference)
- `Assets/_Modules/Village/BuildMode/LeanTouchBuildDriver.cs` (if exists) — Integrate or replace with new gestures
- `Assets/_Modules/Village/BuildMode/BuildPaletteUI.cs` — Add ARIA labels, focus groups

### No Changes Required
- Gameplay logic (input abstraction unchanged)

---

## Design Spec

### Touch Gestures

**Pan (Single-finger drag):**
- Start: Tap on viewport (not on UI buttons)
- Action: Drag to move camera in world space
- Visual feedback: Cursor becomes dragging hand (CSS cursor: grab/grabbing)
- End: Release finger or move to UI button (cancels pan)
- Equivalent to: WASD on desktop, middle-mouse drag

**Zoom (Two-finger pinch):**
- Start: Place two fingers on viewport
- Action: Spread to zoom out, pinch to zoom in
- Rate: 1× pinch distance = 1m camera height change
- Clamp: Respect height min/max (14m–60m from WO-108)
- End: Lift second finger
- Equivalent to: Scroll wheel on desktop

**Rotation (Single-finger tap):**
- Tap R button → armed structure rotates (existing behavior)
- No gesture-based rotation (conflicts with pan)

### Safe Area Handling

Apply safe area insets to ALL UI containers:
```csharp
public static void ApplySafeArea(VisualElement element)
{
    var safeArea = Screen.safeArea;
    float leftPct = (safeArea.xMin / Screen.width) * 100f;
    float topPct = ((Screen.height - safeArea.yMax) / Screen.height) * 100f;
    float rightPct = ((Screen.width - safeArea.xMax) / Screen.width) * 100f;
    float bottomPct = (safeArea.yMin / Screen.height) * 100f;
    
    element.style.paddingLeft = new StyleLength(new Length(leftPct, LengthUnit.Percent));
    element.style.paddingTop = new StyleLength(new Length(topPct, LengthUnit.Percent));
    element.style.paddingRight = new StyleLength(new Length(rightPct, LengthUnit.Percent));
    element.style.paddingBottom = new StyleLength(new Length(bottomPct, LengthUnit.Percent));
}
```

### Accessibility Settings (First Launch)

**Prompt screen:**
- Appears after splash screen, before first game scene
- Title: "Accessibility Settings"
- Options:
  - Text size: Small / Normal / Large (affects UI font sizes)
  - Color blindness: None / Protanopia (red-green) / Deuteranopia (green) / Tritanopia (blue-yellow)
  - High contrast: On / Off (increases border widths, boldens text)
  - Screen reader: On / Off (enables ARIA labels, verbose feedback)
- Buttons: "Continue" (saves settings), "Reset to Defaults"
- Settings saved to PlayerPrefs (persisted across sessions)

**In-game accessibility menu:**
- Settings → Accessibility
- Same options as first-launch prompt
- "Test" buttons to preview changes

### Focus Order (Keyboard Navigation)

```
Tab sequence:
1. Status bar (crystal display, filter tabs)
2. Game viewport (tap to focus, receive arrow keys for pan)
3. Rotate button (R)
4. Cancel button (Esc)
5. Armed card (Place button)
6. Palette cards (arrow keys to navigate)
7. Orient / Done buttons
```

**Implementation:**
```csharp
element.tabIndex = 0;  // Focusable
var focusable = GetComponent<Focusable>();
focusable.focusable = true;
```

### Keyboard Fallbacks

| Key | Action | Context |
|-----|--------|---------|
| WASD | Pan camera | Build mode active |
| Scroll / +/- | Zoom camera | Build mode active |
| R | Rotate armed structure | Structure armed |
| G | Toggle grid | Build mode active |
| Esc | Cancel placement / dismiss panel | Build mode active |
| Enter | Confirm placement / place structure | Structure armed |
| Q/W/E/R | Cast abilities | Battle HUD active (future) |
| Tab | Navigate focus | Any mode |
| Space | Activate focused button | Any mode |

### Screen Reader Support

Add ARIA labels to key elements:
```csharp
var statusElement = new VisualElement();
statusElement.AddToClassList("sr-only");  // Visually hidden
statusElement.text = "Crystal balance: 240. Build mode active. " +
                     "Stone Tower armed. Valid placement detected. " +
                     "Two synergies: 8% DPS boost from Lumbermill, " +
                     "15% Range boost from Watchtower.";
```

**Messages announced:**
- "Build mode entered"
- "Stone Tower armed"
- "Placement valid" / "Placement invalid: overlaps existing structure"
- "Synergies detected: ..." (list active bonuses)
- "Structure rotated 90 degrees"

---

## Implementation Notes

### BuildModeTouchInput.cs

```csharp
public sealed class BuildModeTouchInput : MonoBehaviour
{
    public event System.Action<Vector2> OnPan;       // Pan delta
    public event System.Action<float> OnZoom;        // Zoom distance
    
    private Vector2 _touchStartPos;
    private float _initialPinchDistance;
    private bool _isPanning = false;
    private bool _isPinching = false;
    
    private void Update()
    {
        HandlePan();
        HandlePinch();
    }
    
    private void HandlePan()
    {
        if (Input.touchCount == 1)
        {
            Touch touch = Input.GetTouch(0);
            
            if (touch.phase == TouchPhase.Began)
            {
                // Check if touch is on viewport (not UI)
                if (!IsPointerOverUI(touch.position))
                {
                    _isPanning = true;
                    _touchStartPos = touch.position;
                }
            }
            else if (touch.phase == TouchPhase.Moved && _isPanning)
            {
                Vector2 delta = touch.position - _touchStartPos;
                OnPan?.Invoke(delta);
                _touchStartPos = touch.position;
            }
            else if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
            {
                _isPanning = false;
            }
        }
    }
    
    private void HandlePinch()
    {
        if (Input.touchCount == 2)
        {
            Touch touch0 = Input.GetTouch(0);
            Touch touch1 = Input.GetTouch(1);
            
            float distance = Vector2.Distance(touch0.position, touch1.position);
            
            if (touch0.phase == TouchPhase.Began || touch1.phase == TouchPhase.Began)
            {
                _initialPinchDistance = distance;
                _isPinching = true;
            }
            else if (_isPinching)
            {
                float deltaDist = distance - _initialPinchDistance;
                OnZoom?.Invoke(deltaDist);
                _initialPinchDistance = distance;
            }
            
            if (touch0.phase == TouchPhase.Ended && touch1.phase == TouchPhase.Ended)
            {
                _isPinching = false;
            }
        }
    }
    
    private bool IsPointerOverUI(Vector2 screenPos)
    {
        return EventSystem.current.IsPointerOverGameObject(
            Input.touchCount > 0 ? Input.GetTouch(0).fingerId : -1);
    }
}
```

### AccessibilitySettings.cs

```csharp
public sealed class AccessibilitySettings : MonoBehaviour
{
    [System.Serializable]
    public struct Settings
    {
        public int textSizeLevel;          // 0 = Small, 1 = Normal, 2 = Large
        public int colorBlindMode;         // 0 = None, 1 = Protanopia, 2 = Deuteranopia, 3 = Tritanopia
        public bool highContrast;
        public bool screenReaderEnabled;
    }
    
    public static Settings LoadSettings()
    {
        var s = new Settings();
        s.textSizeLevel = PlayerPrefs.GetInt("a11y_textSize", 1);
        s.colorBlindMode = PlayerPrefs.GetInt("a11y_colorBlind", 0);
        s.highContrast = PlayerPrefs.GetInt("a11y_highContrast", 0) == 1;
        s.screenReaderEnabled = PlayerPrefs.GetInt("a11y_screenReader", 0) == 1;
        return s;
    }
    
    public static void SaveSettings(Settings s)
    {
        PlayerPrefs.SetInt("a11y_textSize", s.textSizeLevel);
        PlayerPrefs.SetInt("a11y_colorBlind", s.colorBlindMode);
        PlayerPrefs.SetInt("a11y_highContrast", s.highContrast ? 1 : 0);
        PlayerPrefs.SetInt("a11y_screenReader", s.screenReaderEnabled ? 1 : 0);
        PlayerPrefs.Save();
    }
    
    public static void ApplySettings(Settings s)
    {
        // Adjust font sizes based on textSizeLevel
        float scaleFactor = s.textSizeLevel == 0 ? 0.85f : 
                           s.textSizeLevel == 2 ? 1.15f : 1.0f;
        
        // Adjust colors for color blindness
        if (s.colorBlindMode > 0)
        {
            // Apply color correction shader or palette swap
        }
        
        // Adjust contrast
        if (s.highContrast)
        {
            // Increase border widths, bold text, etc.
        }
        
        // Enable/disable screen reader announcements
        screenReaderEnabled = s.screenReaderEnabled;
    }
}
```

---

## Testing Checklist

- [ ] Pan gesture works on viewport (camera moves correctly)
- [ ] Pinch gesture zooms (respects min/max height)
- [ ] Safe area respected on iPhone notch + Android rounded corners
- [ ] Accessibility settings prompt appears on first launch
- [ ] Settings persist after closing app
- [ ] Focus order navigable with Tab key
- [ ] Keyboard shortcuts (WASD, R, G, Esc) work
- [ ] Screen reader announces placement state (test with TalkBack/VoiceOver)
- [ ] High contrast mode increases readability
- [ ] Text size adjustments apply to all UI
- [ ] Color blind mode distinctions work (red/green cards visible)
- [ ] No flickering (profile for frame-rate stability)
- [ ] Works in WebGL on mobile device

---

## Accessibility Compliance

- **WCAG 2.1 Level AA** minimum
- **1.4.3 Contrast (Minimum):** Text ≥4.5:1 ratio
- **2.1.1 Keyboard:** All functionality available via keyboard
- **2.4.3 Focus Order:** Logical, intuitive order
- **2.4.7 Focus Visible:** Focus ring always visible (minimum 3px)
- **4.1.3 Status Messages:** Announced to screen readers

---

## What NOT to Touch

- Combat/ability system (touch input is camera-only in build mode)
- PlacementGrid logic
- StructureFactory placement

---

## Dependencies

- **Depends on:** WO-108 (BuildModeController, input abstraction), WO-355 (portrait layout)
- **Unblocks:** None
- **Parallel:** WO-352–356

---

## Acceptance Sign-Off

- [ ] Brace balance check passed
- [ ] All gestures responsive (no lag)
- [ ] Safe area correct on notched devices
- [ ] Accessibility settings functional
- [ ] Screen reader announces key state changes
- [ ] Works in WebGL on mobile
