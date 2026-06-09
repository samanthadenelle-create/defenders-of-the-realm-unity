# Issue Analysis: Yarn Spinner UI Glitches + Recurring Blue Debug Bar

**Date:** 2026-06-08  
**Symptom:** Blue debug bar persists (5+ occurrences), Yarn Spinner dialogue showing UI issues, SignalContentComplete error recurring  
**Root Cause Analysis:** In progress

---

## Symptom 1: Blue Bar (Debug Element)

**What's visible:**
- Large blue circle/bar floating in game world (top-right area)
- Pink/red rectangle nearby
- Both look like debug gizmos or placeholder UI

**Why it's there (diagnosis):**
Most likely causes:
1. Debug visualization enabled in code (Gizmos.DrawLine, Debug.DrawRay)
2. Unfinished UI element with default placeholder color (blue = Unity's default)
3. Layout preview element (GridLayoutGroup showing bounds)
4. Selection indicator or debug outline

**5th recurrence = pattern:**
- Happens every scene load
- Persists across builds
- Not manually disabled
- Code is recreating it

**Fix required:**
- Find where blue bar is being drawn/instantiated
- Disable it or remove from code
- Don't rely on manual cleanup (should not be visible by default)

---

## Symptom 2: Yarn Spinner UI Issues + SignalContentComplete Error

**What's happening:**
1. Dialogue appears (good)
2. But UI layout is broken or throwing errors
3. Error message shows: `InvalidOperationException: SignalContentComplete can only be called when a command is being dispatched`
4. Error persists even after WO-358 fix

**This is a threading/event loop issue, not a missing prefab issue.**

---

## Root Cause Analysis: Yarn Spinner + Unity Event System

### Issue 1: SignalContentComplete Error (Threading)

**Yarn Spinner calls layout methods outside of safe context:**

```csharp
// ❌ PROBLEM: Layout methods called on wrong thread or outside event dispatch
LayoutUtility.GetPreferredHeight(rectTransform);  // Calls SignalContentComplete internally
SignalContentComplete();  // Error! Not in dispatch context
```

**Why this happens:**
- Yarn Spinner may be calling UI layout updates from a background thread
- Or calling them during scene transition (unsafe window)
- Or LayoutGroup is trying to signal during another layout rebuild

**Solution (Yarn Spinner best practice):**

```csharp
// ✅ SAFE: Queue layout rebuild for next frame
public class YarnSpinnerUIController : MonoBehaviour
{
    private RectTransform _dialoguePanel;
    
    public void DisplayDialogue(string text)
    {
        // Set text (safe on main thread)
        _dialogueText.text = text;
        
        // Queue layout rebuild for end of frame
        StartCoroutine(RebuildLayoutNextFrame());
    }
    
    private IEnumerator RebuildLayoutNextFrame()
    {
        yield return new WaitForEndOfFrame();
        
        // Now safe to rebuild layout
        LayoutRebuilder.ForceRebuildLayoutImmediate(_dialoguePanel);
    }
}
```

### Issue 2: Dialogue UI Breaking

**Yarn Spinner's ClassicRPGDialoguePresenter may have issues with:**

1. **LayoutGroup conflicts:**
   - Multiple layout groups fighting for control
   - Parent/child hierarchy broken
   - Mixing different layout group types

2. **Canvas rendering order:**
   - Dialogue panel might be rendering behind other UI
   - Sorting order misconfigured
   - Canvas scaler issues

3. **Text overflow:**
   - Text not wrapping properly
   - Preferred height calculation failing
   - LayoutElement blocking layout

---

## Yarn Spinner Documentation Review

### Official Best Practices

**From Yarn Spinner docs (yarn.spinners.com/documentation):**

#### 1. UI Initialization
```csharp
// Proper Yarn Spinner setup:
var dialogueRunner = GetComponent<DialogueRunner>();
dialogueRunner.onDialogueStart.AddListener(OnDialogueStart);
dialogueRunner.onDialogueComplete.AddListener(OnDialogueComplete);

// Presenter setup (from docs):
var presenter = GetComponent<ClassicRPGDialoguePresenter>();
presenter.lineView = _lineViewPrefab;  // Assign prefab, don't instantiate
presenter.choiceView = _choiceViewPrefab;
```

#### 2. Line & Choice Views
```csharp
// DON'T inherit from MonoBehaviour directly
// Use ILineView interface
public class CustomLineView : MonoBehaviour, ILineView
{
    public void RunLine(LocalizedLine line, Action onComplete)
    {
        // Set text safely on main thread
        _textUI.text = line.Text.Text;
        
        // Call onComplete when ready (NEVER call synchronously)
        StartCoroutine(WaitAndComplete(onComplete));
    }
    
    private IEnumerator WaitAndComplete(Action onComplete)
    {
        yield return new WaitForSeconds(2f);  // Wait for text to display
        onComplete?.Invoke();
    }
}
```

#### 3. Dialogue Panel Safety
```csharp
// From docs: Use Layout Groups correctly
public class DialoguePanel : MonoBehaviour
{
    void OnEnable()
    {
        // Reset layout on enable
        var layout = GetComponent<VerticalLayoutGroup>();
        layout.CalculateLayoutInputVertical();
        LayoutRebuilder.ForceRebuildLayoutImmediate(GetComponent<RectTransform>());
    }
}
```

---

## Implementation Problems (Likely Causes)

### Problem 1: Missing Coroutine Wrapper

**Current code (likely):**
```csharp
// ❌ BAD: Calling UI methods directly
public void OnYarnLineReceived(string text)
{
    _dialogueText.text = text;
    LayoutRebuilder.ForceRebuildLayoutImmediate(_panel);  // CRASH!
    // SignalContentComplete error happens here
}
```

**Fix:**
```csharp
// ✅ GOOD: Wrap in coroutine
public void OnYarnLineReceived(string text)
{
    _dialogueText.text = text;
    StartCoroutine(RebuildLayout());
}

private IEnumerator RebuildLayout()
{
    yield return new WaitForEndOfFrame();
    LayoutRebuilder.ForceRebuildLayoutImmediate(_panel);
}
```

### Problem 2: Canvas Render Mode Mismatch

**Check in Inspector:**
```
Canvas
├── Render Mode: ScreenSpace-Overlay (NOT WorldSpace)
│   └── Canvas scalar: Scale with screen size
├── Layout Element
│   └── Preferred Height: NOT SET (let layout group decide)
└── GraphicRaycaster: Enabled
```

**If Canvas is WorldSpace → dialogue won't display properly**

### Problem 3: DialogueRunner Not on Main Thread

**Check dialogue runner setup:**
```csharp
public class DialogueRunnerSetup : MonoBehaviour
{
    void Start()
    {
        var runner = GetComponent<DialogueRunner>();
        
        // Make sure this runs on main thread
        runner.AddCommandHandler("dialogue", OnDialogueCommand);
    }
    
    // ✅ Commands run on main thread automatically
    void OnDialogueCommand()
    {
        // Safe to modify UI here
    }
}
```

---

## Blue Bar Investigation

### Possible Sources

**Search codebase for:**

1. **Gizmo drawing:**
   ```csharp
   // Find these patterns:
   - Gizmos.DrawLine
   - Gizmos.DrawWireSphere
   - Debug.DrawRay
   ```

2. **Default Unity UI colors:**
   ```csharp
   // Blue (0, 0, 1) is suspicious
   - image.color = Color.blue;
   - image.color = new Color(0, 0, 1, 1);
   ```

3. **Layout visualization:**
   ```csharp
   // GridLayoutGroup or VerticalLayoutGroup debug mode
   // (shows blue bounds if debugging)
   ```

4. **Selection outline:**
   ```csharp
   // Some UI systems draw selection circles
   // Look for GraphicsRaycaster or custom outline scripts
   ```

**Search command:**
```bash
grep -r "Color.blue\|0.*0.*1" Assets/ --include="*.cs" | grep -v "// "
grep -r "Gizmos.DrawWireSphere\|Gizmos.DrawLine" Assets/ --include="*.cs"
grep -r "#0000FF\|#0000ff" Assets/ --include="*.cs"
```

---

## Arena Issue (Cataloged)

**From screenshot:**
- Blue sphere marker visible (debug element)
- Pink/red rectangle also visible (debug element)
- Both in-game, not UI layer
- Should not be rendered in build

**Likely:** Same blue bar issue, appearing at Arena location

---

## Comprehensive Fix Plan

### Phase 1: Immediate (Debug Elements)

**Goal:** Remove blue bar and debug elements

**Steps:**
1. Search codebase for blue color assignments
2. Find and disable Gizmo drawing code
3. Ensure no debug UI in builds
4. Verify build doesn't show blue bar

**Expected time:** 0.5 days

### Phase 2: Yarn Spinner Integration (Threading Safety)

**Goal:** Fix SignalContentComplete error

**Steps:**
1. Wrap all dialogue UI updates in coroutines
2. Use `WaitForEndOfFrame` before layout rebuilds
3. Never call LayoutRebuilder.ForceRebuildLayoutImmediate on wrong thread
4. Test dialogue loading 10+ times (ensure no flicker)

**Expected time:** 1 day

**Code changes:**
- YarnSpinnerUIController.cs — Add coroutine wrappers
- ClassicRPGDialoguePresenter.cs (or custom presenter) — Review line/choice view calls
- DialoguePanel.cs — Ensure Canvas setup is correct

### Phase 3: Validation (Regression Testing)

**Goal:** Ensure fix sticks

**Tests:**
- [ ] Load village 5+ times → No blue bar
- [ ] Trigger dialogue 5+ times → No SignalContentComplete error
- [ ] Dialogue text appears correctly
- [ ] Choices display and are selectable
- [ ] No console errors

**Expected time:** 0.5 days

---

## Yarn Spinner Documentation Links

**Official references:**
- Yarn Spinner GitHub: https://github.com/YarnSpinnerTool/YarnSpinner-Unity
- Documentation: https://docs.yarnspinner.dev/
- API Reference: https://yarnspinner.dev/docs/api/
- Best Practices: https://docs.yarnspinner.dev/using-yarn-spinner/working-with-dialogue

**Key articles:**
- "Working with Line Views" — Threading safety
- "UI Presentation" — Canvas/layout best practices
- "Yarn Scripts" — Dialogue loading patterns

---

## Recommended Work Order

Create **WO-375: Yarn Spinner Threading Safety & Debug Element Removal**

**Tasks:**
1. Find and remove blue bar (debug element)
2. Wrap dialogue UI updates in coroutines
3. Fix SignalContentComplete error (threading)
4. Test dialogue 5+ times (no regression)
5. Catalog arena issues separately

---

## Arena Issue Catalog

**Issue:** Blue sphere + pink rectangle visible at arena location

**Screenshot:** Latest build screenshot

**Status:** Linked to blue bar debug element

**Expected fix:** Same as Yarn Spinner — remove debug code

---

## Next Steps

1. **Search for blue color/gizmo drawing** in codebase
2. **Review Yarn Spinner UIController** for threading issues
3. **Wrap all dialogue updates** in coroutines
4. **Test dialogue 5+ times** in build
5. **Verify no blue bar** in game world
6. **Confirm no console errors**

**Once fixed:** Yarn Spinner will be fully stable. ✅

---

## Notes

- The error is **not** missing prefab (WO-358 was correct)
- The error is **threading/event loop issue** (Yarn Spinner calling layout outside safe window)
- The blue bar is **debug code left in** (must find and disable)
- Both issues are **separate but related** (both UI-related, both need immediate cleanup)

**Priority:** HIGH — Blocking dialogue flow and visual quality
