# WO-375: Yarn Spinner Threading Safety & Debug Element Removal

**Status:** READY TO IMPLEMENT  
**Estimated Effort:** P0 (1–1.5 days — debugging + fix + validation)  
**Priority:** CRITICAL (dialogue broken, blue bar recurring 5+ times)  
**Lane:** 4 UI/HUD

---

## Overview

Two related issues blocking dialogue system:

1. **Blue debug bar** appears in game world (5+ recurrences) — should never be visible
2. **Yarn Spinner threading error** — `SignalContentComplete` called on wrong thread
3. **Dialogue UI breaks** — Layout glitches, text doesn't display correctly

**Root cause:** Debug code left in + Yarn Spinner calls UI layout methods from unsafe context (threading issue)

---

## Issue #1: Blue Bar (Debug Element)

### Symptom
- Large blue sphere/circle visible in game world
- Pink/red rectangle nearby (also debug)
- Both look like gizmos or placeholder UI
- Appears at spawn point and arena location
- Returns after scene reload (5+ times)

### Root Cause
- Gizmo drawing code left enabled (Debug.DrawRay, Gizmos.DrawWireSphere)
- OR: Default UI element with Color.blue, not hidden
- OR: Layout visualization showing (GraphicsRaycaster debug mode)

### Fix Required

**Search codebase:**
```csharp
// Find all instances of:
- Gizmos.Draw* (any color)
- Debug.DrawRay
- Debug.DrawLine
- Color.blue or #0000FF assignments
- GraphicRaycaster debug mode
```

**Example searches:**
```bash
grep -r "Gizmos\|Debug\.Draw" Assets/ --include="*.cs"
grep -r "Color\.blue\|0.*0.*1" Assets/ --include="*.cs" | grep -v "//"
```

**Disable or remove:**
- Any Gizmo drawing in non-editor code
- Default UI colors (should use stylesheet, not hardcoded blue)
- Debug visualization systems

**Never ship with:**
- Gizmos enabled in builds
- Debug UI elements
- Placeholder colors

---

## Issue #2: Yarn Spinner Threading Error

### Symptom
```
InvalidOperationException: SignalContentComplete can only be called 
when a command is being dispatched.
```

### Root Cause (Yarn Spinner best practices violation)

Yarn Spinner's dialogue system is calling layout methods from unsafe context:

```csharp
// ❌ PROBLEM: Called on wrong thread or during scene transition
LayoutUtility.GetPreferredHeight(rectTransform);
SignalContentComplete();  // ERROR: Not in event dispatch context
```

**Why it fails:**
- Yarn Spinner runs dialogue updates on callback
- Callbacks may not be on main thread
- Or called during scene transition (unsafe window)
- Or LayoutGroup is rebuilding while another rebuild happens
- Layout methods internally call SignalContentComplete

### Yarn Spinner Best Practice (from documentation)

```csharp
// ✅ SAFE: Wrap layout calls in coroutine
public class YarnSpinnerUIController : MonoBehaviour
{
    public void DisplayLine(LocalizedLine line)
    {
        // Text update is safe
        _dialogueText.text = line.Text.Text;
        
        // Layout rebuild MUST be queued for next frame
        StartCoroutine(RebuildLayoutSafely());
    }
    
    private IEnumerator RebuildLayoutSafely()
    {
        // Wait until end of current frame
        yield return new WaitForEndOfFrame();
        
        // NOW it's safe to rebuild layout
        LayoutRebuilder.ForceRebuildLayoutImmediate(_dialoguePanel);
    }
}
```

---

## Fix Implementation

### Step 1: Find and Remove Blue Bar

**Search for:**
```csharp
// In all scripts:
- Gizmos.DrawWireSphere (likely drawing blue sphere)
- Gizmos.DrawLine (drawing pink rectangle?)
- Color.blue or new Color(0, 0, 1)
- Debug.DrawRay
```

**Example pattern (likely culprit):**
```csharp
// ❌ BAD: Left in code
void OnDrawGizmos()
{
    Gizmos.color = Color.blue;
    Gizmos.DrawWireSphere(transform.position, 5f);  // ← BLUE SPHERE
}
```

**Fix:**
```csharp
// ✅ GOOD: Either remove entirely or wrap in #if UNITY_EDITOR
#if UNITY_EDITOR
void OnDrawGizmos()
{
    Gizmos.color = Color.blue;
    Gizmos.DrawWireSphere(transform.position, 5f);
}
#endif
```

**Action items:**
- [ ] Search for all Gizmos.Draw* calls
- [ ] Wrap in `#if UNITY_EDITOR` or remove
- [ ] Search for hardcoded Color.blue
- [ ] Replace with proper color (use ColorPalette if exists)
- [ ] Verify no blue elements in build

---

### Step 2: Fix Yarn Spinner Threading

**Files to review:**
- `Assets/_Modules/Core/Dialogue/YarnSpinnerUIController.cs` (or equivalent)
- `Assets/_Modules/Core/Dialogue/LineView.cs` (custom implementation)
- `Assets/_Modules/Core/Dialogue/ChoiceView.cs` (custom implementation)
- Any script inheriting `ILineView`

**Pattern to fix:**
```csharp
// ❌ BEFORE: Direct layout call
public void DisplayLine(string text)
{
    _textUI.text = text;
    LayoutRebuilder.ForceRebuildLayoutImmediate(_panel);  // CRASH!
}

// ✅ AFTER: Queued layout rebuild
public void DisplayLine(string text)
{
    _textUI.text = text;
    StartCoroutine(RebuildLayout());
}

private IEnumerator RebuildLayout()
{
    yield return new WaitForEndOfFrame();
    LayoutRebuilder.ForceRebuildLayoutImmediate(_panel);
}
```

**Apply to:**
- Line display code
- Choice display code
- Dialogue panel setup
- Any UI update from Yarn Spinner callbacks

---

### Step 3: Verify Canvas Setup (Dialogue Panel)

**Check these settings:**

| Setting | Should Be | Current |
|---|---|---|
| Canvas Render Mode | ScreenSpace-Overlay | ? |
| Canvas Scaler | Scale with screen | ? |
| Layout Element | (None — let group decide) | ? |
| VerticalLayoutGroup | Child Force Expand: No | ? |
| GraphicsRaycaster | Enabled (for input) | ? |

**If wrong:**
- WorldSpace Canvas → Dialogue won't appear correctly
- Conflicting LayoutElements → SignalContentComplete errors
- Missing GraphicsRaycaster → Choices won't be clickable

---

## Testing Checklist

### Phase 1: Blue Bar Removal
- [ ] No Gizmos.Draw* calls in builds
- [ ] No Color.blue hardcoded UI elements
- [ ] Load game 5+ times → No blue sphere visible
- [ ] Check arena location → No blue/pink debug elements
- [ ] Build and verify (WebGL if applicable)

### Phase 2: Yarn Spinner Threading
- [ ] Wrap all dialogue UI updates in coroutines
- [ ] Test dialogue trigger 10+ times → No SignalContentComplete errors
- [ ] Console shows no threading errors
- [ ] Dialogue text displays correctly
- [ ] Choices appear and are selectable
- [ ] No UI glitches or layout breaks

### Phase 3: Regression
- [ ] WO-373 gates still pass (tree at 0,0,0, movement works)
- [ ] Dialogue loads on village entry
- [ ] Yarn Spinner dialogue fully functional
- [ ] No lingering console errors

---

## Code Changes Required

### File: YarnSpinnerUIController.cs (or equivalent)

```csharp
using System.Collections;
using Yarn.Unity;
using UnityEngine;
using UnityEngine.UI;

public class YarnSpinnerUIController : MonoBehaviour
{
    [SerializeField] private Text _dialogueText;
    [SerializeField] private RectTransform _dialoguePanel;
    
    private DialogueRunner _dialogueRunner;
    
    void Start()
    {
        _dialogueRunner = GetComponent<DialogueRunner>();
        // Yarn Spinner will call our methods when dialogue runs
    }
    
    // Called by Yarn Spinner
    public void DisplayDialogue(LocalizedLine line)
    {
        // Safe: Set text directly
        _dialogueText.text = line.Text.Text;
        
        // IMPORTANT: Queue layout rebuild for next frame
        StartCoroutine(RebuildLayoutNextFrame());
    }
    
    // Queue layout rebuild to avoid threading issues
    private IEnumerator RebuildLayoutNextFrame()
    {
        yield return new WaitForEndOfFrame();
        LayoutRebuilder.ForceRebuildLayoutImmediate(_dialoguePanel);
    }
    
    // Never call SignalContentComplete manually
    // LayoutGroup handles it in safe context
}
```

---

## Yarn Spinner Documentation References

**Key sections:**
- "Working with Line Views" (threading safety)
- "UI Presentation" (Canvas/layout)
- "Yarn Scripts" (dialogue loading)

**Docs:** https://docs.yarnspinner.dev/using-yarn-spinner/working-with-dialogue

---

## What NOT to Do

- ❌ Don't call layout methods directly in callbacks
- ❌ Don't use Gizmos in non-editor code
- ❌ Don't hardcode debug colors (Color.blue)
- ❌ Don't ignore threading warnings
- ❌ Don't manually call SignalContentComplete

---

## Acceptance Criteria

- [ ] Blue debug bar is gone (no blue elements in world)
- [ ] Arena location has no debug visualization
- [ ] SignalContentComplete error doesn't appear
- [ ] Dialogue displays correctly (text, choices)
- [ ] Dialogue loading is smooth (no flicker)
- [ ] Console is clean (no threading errors)
- [ ] Works in 5+ scene loads (regression check)
- [ ] WO-373 gates still pass

---

## Blockers Until Fixed

- [ ] Character selection can't proceed (needs dialogue)
- [ ] Story progression blocked
- [ ] Yarn Spinner system unreliable

---

## Related Work Orders

- WO-358: Yarn Spinner Prefab (DONE — but threading issue remains)
- WO-374: Character Selection UI (blocked by dialogue)
- WO-373: Regression Gates (ensure no new breaks)

---

## Priority

**CRITICAL.** Yarn Spinner is core narrative system. Threading bug breaks dialogue on every load. Blue bar is visual glitch. Both must be fixed before any build ships.

---

## Notes

- This is NOT a missing prefab issue (that was WO-358)
- This IS a threading/event loop safety issue
- Yarn Spinner docs explicitly warn about this pattern
- Fix is straightforward (wrap in coroutine)
- Blue bar is likely separate debug code (search first)

**Once fixed:** Dialogue system will be stable and production-ready. ✅
