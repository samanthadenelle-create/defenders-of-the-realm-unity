# WO-374: Character Selection UI — Card Layout & Framing Fix

**Status:** READY TO IMPLEMENT  
**Estimated Effort:** P0 (0.5 days — layout + styling)  
**Priority:** HIGH (blocking character selection flow)  
**Lane:** 4 UI/HUD

---

## Overview

Character selection screen has **layout issues**:
1. Card images not properly framed
2. Card data (name, class, description) overlaps image instead of sitting below
3. Cards feel cramped and unprofessional
4. Console error appearing on-screen (SignalContentComplete)

**Target:** Clean card layout with framed image above, data below. Professional, readable.

---

## Issues to Fix

### Issue #1: Missing Image Frame

**Current:** Images are raw, no border/frame treatment

**Target:** Each character image should have:
- Ornate gold/bronze border (matches title screen aesthetic)
- Shadow/depth effect
- Clear separation from data below
- Consistent frame style across all cards

### Issue #2: Overlapping Data Layout

**Current:** Text overlaps the image area, creating visual confusion

**Target:** Clean vertical stack:
```
┌─────────────────┐
│                 │
│  [Framed Image] │  ← Image with ornate border
│                 │
├─────────────────┤
│  Name (Sylas)   │  ← Below the frame
│  Class (Title)  │
│  Description... │
└─────────────────┘
```

### Issue #3: Console Error (SignalContentComplete)

**Error:** `InvalidOperationException: SignalContentComplete can only be called when a command is being dispatched.`

**Likely cause:** UI canvas or layout group trying to signal completion outside of proper context (threading/event issue)

**Fix:** 
- Check layout group initialization
- Ensure GridLayoutGroup or VerticalLayoutGroup is properly configured
- Verify all UI elements are children of correct parent
- Remove any manual signal calls outside of event dispatch context

---

## Card Layout Specification

### Card Structure

```
Card (Root Panel)
├── ImageFrame (Panel with border + shadow)
│   └── CharacterImage (RawImage or Image)
├── DataPanel (Panel below image)
│   ├── NameText (e.g., "Sylas")
│   ├── ClassText (e.g., "Wood Warden")
│   └── DescriptionText (multi-line, scrollable if needed)
└── SelectionHighlight (Optional glow/border on hover)
```

### Image Frame Styling

**Frame properties:**
- **Border:** Gold #D4AF37 (3–4 px, double-line like title buttons)
- **Background:** Dark transparent (#000000, opacity 0.3) behind image
- **Shadow:** Drop shadow (4 px offset, blur 8 px, #000000, opacity 0.5)
- **Corner detail:** Optional ornate corners (small flourishes)
- **Aspect ratio:** Fixed (e.g., 3:4 for portrait characters)

**Sizing:**
- Image width: ~200–240 px (on desktop)
- Image height: ~300–360 px (portrait ratio)
- Responsive: Scale down on mobile (maintain ratio)

### Data Panel Styling

**Below image frame:**
- **Background:** Slightly lighter than cards (e.g., #1a1a1a for contrast)
- **Padding:** 16 px (breathing room)
- **Border:** Bottom part of card border (continues frame aesthetic)

**Text elements:**
1. **Name (e.g., "Sylas")**
   - Font: Serif, bold (matches title screen)
   - Size: 20–24 px
   - Color: Gold #D4AF37 (like title buttons)
   - Alignment: Center

2. **Class (e.g., "Wood Warden")**
   - Font: Serif, italic or lighter weight
   - Size: 14–16 px
   - Color: Cream #F5DEB3
   - Alignment: Center

3. **Description (multi-line)**
   - Font: Sans-serif or serif (readable, not fancy)
   - Size: 12–14 px
   - Color: Light gray #CCCCCC
   - Alignment: Left or center
   - Max lines: 3 (with ellipsis or scroll)
   - Line height: 1.4 (readable spacing)

### Hover/Selection State

**Card on hover:**
- **Glow:** Bright gold glow around entire card (outer shadow)
- **Image frame:** Border brightens to #FFD700
- **Name text:** Becomes bright gold #FFD700
- **Scale:** Slight grow (1.02–1.05)
- **Transition:** 0.2 seconds smooth

**Card on select (clicked):**
- **Glow:** Intense gold glow (opacity 0.8)
- **Scale:** Full size (1.0, back from hover grow)
- **Effect:** Button press animation (slight shrink + release)
- **Result:** Character selected, proceed to next screen

---

## Layout Implementation (Unity UI)

### Hierarchy Example

```
Canvas
└── CharacterSelectPanel
    ├── HeaderText ("CHOOSE YOUR HERO")
    └── CardGrid (GridLayoutGroup)
        ├── CardPrefab (Button)
        │   ├── ImageFrame (Panel)
        │   │   ├── Image (RawImage, character portrait)
        │   │   └── FrameBorder (Image, gold border graphic)
        │   └── DataPanel (VerticalLayoutGroup)
        │       ├── NameText (Text)
        │       ├── ClassText (Text)
        │       └── DescriptionText (Text)
        ├── CardPrefab (Button) ← Drom
        ├── CardPrefab (Button) ← Thrain
        └── CardPrefab (Button) ← Elara
```

### GridLayoutGroup Settings

| Setting | Value | Notes |
|---|---|---|
| Child Force Expand | Width: Yes, Height: No | Cards fill horizontal space, vary height |
| Child Control Size | Width: Yes, Height: Yes | Explicit sizing |
| Spacing | X: 20 px, Y: 20 px | Gap between cards |
| Padding | L: 40, R: 40, T: 20, B: 20 | Margin from edges |
| Constraint | Flexible (or Fixed Column Count: 4) | 4 cards in row on desktop |

### VerticalLayoutGroup (DataPanel)

| Setting | Value |
|---|---|
| Spacing | 4 px (tight, compact) |
| Child Force Expand | Height: No |
| Child Control Size | Height: No |
| Layout Fit | Preferred Size (grows to fit content) |

### Image Component

| Setting | Value |
|---|---|
| Source Image | Character portrait (1024×1024 or similar) |
| Color | White (neutral, no tint) |
| Preserve Aspect | Yes (maintains portrait ratio) |
| Set Native Size | Yes (crisp at native resolution) |

---

## Console Error Fix

### SignalContentComplete Error

**Root cause:** Layout event being signaled outside of proper context

**Fixes to try (in order):**

1. **Check LayoutGroup initialization:**
   ```csharp
   // Don't manually call SignalContentComplete
   // LayoutGroup handles this internally
   
   // ❌ BAD:
   LayoutUtility.GetPreferredHeight(rectTransform);
   SignalContentComplete();  // Error!
   
   // ✅ GOOD:
   LayoutRebuilder.ForceRebuildLayoutImmediate(rectTransform);
   // (LayoutGroup signals completion automatically)
   ```

2. **Ensure proper parent/child setup:**
   - All UI elements must have LayoutElement or be in proper layout hierarchy
   - No mixing of different layout group types

3. **Disable/Enable LayoutGroup if needed:**
   ```csharp
   public void RefreshLayout()
   {
       var layoutGroup = GetComponent<GridLayoutGroup>();
       layoutGroup.enabled = false;
       layoutGroup.enabled = true;  // Force rebuild
   }
   ```

4. **Check for recursive layout updates:**
   - If cards are being instantiated/destroyed during layout, this can cause issues
   - Use `WaitForEndOfFrame` coroutine to rebuild layout after updates:
   ```csharp
   StartCoroutine(RebuildLayoutNextFrame());
   
   IEnumerator RebuildLayoutNextFrame()
   {
       yield return new WaitForEndOfFrame();
       LayoutRebuilder.ForceRebuildLayoutImmediate(GetComponent<RectTransform>());
   }
   ```

5. **Remove any custom SignalContentComplete calls:**
   - Search codebase for `SignalContentComplete`
   - Remove unless absolutely necessary
   - LayoutGroup should handle it

---

## CSS (Web Version, if applicable)

```css
.character-card {
  background: linear-gradient(135deg, #2a2a2a 0%, #1a1a1a 100%);
  border: 1px solid #8B7355;
  border-radius: 4px;
  padding: 0;
  overflow: hidden;
  transition: all 0.3s ease;
  cursor: pointer;
  flex: 1 1 calc(25% - 20px);
  min-width: 200px;
}

.character-card:hover {
  border-color: #FFD700;
  box-shadow: 
    0 0 20px rgba(212, 175, 55, 0.6),
    inset 0 0 20px rgba(212, 175, 55, 0.1);
  transform: translateY(-4px) scale(1.02);
}

.character-card:active {
  transform: translateY(0) scale(0.98);
}

.image-frame {
  position: relative;
  border: 3px solid #D4AF37;
  border-bottom: none;  /* Continue into data */
  background: #000000;
  aspect-ratio: 3 / 4;
  overflow: hidden;
}

.character-image {
  width: 100%;
  height: 100%;
  object-fit: cover;
}

.data-panel {
  border: 3px solid #D4AF37;
  border-top: 1px solid #8B7355;  /* Lighter inner line */
  padding: 16px;
  background: #1a1a1a;
}

.character-name {
  font-family: 'Cinzel', serif;
  font-size: 20px;
  font-weight: bold;
  color: #D4AF37;
  text-align: center;
  margin-bottom: 4px;
}

.character-class {
  font-family: 'Cinzel', serif;
  font-size: 14px;
  color: #F5DEB3;
  text-align: center;
  font-style: italic;
  margin-bottom: 8px;
}

.character-description {
  font-family: Arial, sans-serif;
  font-size: 12px;
  color: #CCCCCC;
  line-height: 1.4;
  text-align: center;
}
```

---

## Testing Checklist

- [ ] Cards are evenly spaced in a grid
- [ ] Image is framed with gold border
- [ ] Data sits cleanly below image (no overlap)
- [ ] All text is readable
- [ ] Hover effect is smooth (glow + grow)
- [ ] Selected card stands out (bright glow)
- [ ] Cards scale properly on mobile (responsive)
- [ ] No console errors on screen
- [ ] Layout rebuilds correctly when cards change
- [ ] Images load without stretching/squashing
- [ ] Card aspect ratio consistent

---

## Files to Modify/Create

### New Files
- `Assets/UI/CharacterSelect/CharacterCardPrefab.prefab` (if not exists)
- `Assets/UI/CharacterSelect/CharacterSelectScreen.cs` (if not exists)

### Existing Files
- `Assets/UI/Panels/CharacterSelectPanel.cs` — Fix layout/error
- Scene: Character selection screen — Verify hierarchy

---

## What NOT to Touch

- Character models (images are assets, don't modify)
- Navigation flow (just fix UI display)
- Character stats/mechanics (UI only)

---

## Acceptance Sign-Off

- [ ] Card images are properly framed
- [ ] Card data is below image, not overlapping
- [ ] Console error (SignalContentComplete) is gone
- [ ] Cards look professional and match game aesthetic
- [ ] Hover/selection states are clear and smooth
- [ ] Layout is responsive (desktop + mobile)
- [ ] Ready for character selection flow

---

## Related Work Orders

- WO-334: Battle ready HUD (similar UI issues, fixed)
- Title screen button styling (consistent aesthetic)

---

## Notes

- Use same gold/bronze/cream color palette as title screen
- Keep serif font consistent (Cinzel or equivalent)
- Character portraits should be high-quality (at least 1024×1024)
- Consider adding character class icons/badges on cards
- Optional: Add "Select" button below description, or make entire card clickable

**Priority:** Fix console error and layout — game flow is blocked on this.
