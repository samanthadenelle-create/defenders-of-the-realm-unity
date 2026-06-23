# WO-377: Dialogue Input Blocking — Prevent Hero Attacks During Yarn Spinner

**Status:** READY TO IMPLEMENT  
**Estimated Effort:** P0 (0.5 days — input system fix)  
**Priority:** CRITICAL (dialogue is broken, player can attack during story)  
**Lane:** 4 UI/HUD

---

## Overview

**Issue:** During Yarn Spinner dialogue:
- Hero is in combat posture (weapon drawn, ready to fight)
- Player clicks dialogue area
- Click passes THROUGH dialogue UI to game world
- Hero attacks instead of dialogue handling click
- Story sequence breaks

**Root cause:** Dialogue UI is not blocking input events. GraphicsRaycaster ordering or event system misconfigured.

---

## What's Happening

**Current flow (BROKEN):**
```
Player clicks dialogue box
    ↓
Click event sent to EventSystem
    ↓
EventSystem checks raycasts
    ↓
Game world raycast hits hero (in front of UI!)
    ↓
HeroController receives click → Attacks
    ↓
Dialogue ignores click → Story breaks
```

**Expected flow (FIXED):**
```
Player clicks dialogue box
    ↓
Click event sent to EventSystem
    ↓
Dialogue panel (GraphicsRaycaster) intercepts click
    ↓
DialogueUI consumes click event
    ↓
Dialogue choice selected or text continues
    ↓
NO event sent to game world
    ↓
Hero does NOT attack
```

---

## Root Causes

### Problem 1: GraphicsRaycaster Order

**Dialogue Canvas must be on top (higher sort order):**

```
Canvas Settings (Dialogue)
├── Render Mode: ScreenSpace-Overlay
│   └── Sort Order: 100 (MUST be higher than game world canvas)
├── GraphicsRaycaster (enabled)
└── BlockRaycasts: ✓ ENABLED (critical!)
```

**Check:**
- Is dialogue Canvas set to ScreenSpace-Overlay?
- Sort Order > game world canvas (game world = 0, dialogue = 100+)?
- GraphicsRaycaster enabled on dialogue Canvas?

### Problem 2: BlockRaycasts Not Set

**Dialogue panel image must block raycasts:**

```
DialoguePanel (Image component)
├── Image enabled
├── BlockRaycasts: ✓ ENABLED (CRITICAL)
└── Interactable: ✓ ENABLED
```

**Check:**
- Does DialoguePanel have Image component?
- Is BlockRaycasts enabled on Image?
- Is the Image filling the screen (covering game world)?

### Problem 3: Player Input Not Disabled

**Hero attack input (clicks) should be disabled during dialogue:**

```csharp
// ❌ BROKEN: Input always active
void Update()
{
    if (Input.GetMouseButtonDown(0))
    {
        Attack();  // Fires during dialogue!
    }
}

// ✅ FIXED: Input disabled during dialogue
void Update()
{
    if (!IsDialogueActive && Input.GetMouseButtonDown(0))
    {
        Attack();  // Only fires when no dialogue
    }
}
```

### Problem 4: EventSystem Not Configured

**EventSystem must prioritize UI over game world:**

```
EventSystem
├── Raycast Target: ✓ Enabled
├── First Selected: Set to dialogue panel
├── Send Navigation Events: ✓ Enabled
└── Drag Threshold: 5 (normal)
```

---

## Fix Implementation

### Fix 1: Dialogue Canvas Setup

**In Inspector (or via code):**

```csharp
public class YarnSpinnerUIController : MonoBehaviour
{
    private Canvas _dialogueCanvas;
    
    void Start()
    {
        _dialogueCanvas = GetComponent<Canvas>();
        
        // ✅ Set canvas to overlay mode (on top)
        _dialogueCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        
        // ✅ High sort order (above game world)
        _dialogueCanvas.sortingOrder = 100;
        
        // ✅ Ensure GraphicsRaycaster exists
        if (_dialogueCanvas.GetComponent<GraphicsRaycaster>() == null)
        {
            _dialogueCanvas.gameObject.AddComponent<GraphicsRaycaster>();
        }
    }
}
```

### Fix 2: Dialogue Panel BlockRaycasts

**Ensure panel blocks input:**

```csharp
public class DialoguePanel : MonoBehaviour
{
    void Start()
    {
        var panelImage = GetComponent<Image>();
        
        // ✅ CRITICAL: Block raycasts so clicks don't reach game world
        panelImage.raycastTarget = true;
        
        // ✅ Ensure panel is interactable
        var layoutElement = GetComponent<LayoutElement>();
        if (layoutElement != null)
        {
            layoutElement.ignoreLayout = false;
        }
    }
}
```

### Fix 3: Disable Player Input During Dialogue

**Prevent hero attacks during dialogue:**

```csharp
public class HeroController : MonoBehaviour
{
    private bool _isDialogueActive = false;
    
    void Start()
    {
        // Listen for dialogue events
        var dialogueRunner = FindObjectOfType<DialogueRunner>();
        if (dialogueRunner != null)
        {
            dialogueRunner.onDialogueStart.AddListener(OnDialogueStart);
            dialogueRunner.onDialogueComplete.AddListener(OnDialogueComplete);
        }
    }
    
    void Update()
    {
        // ✅ Only process input if NO dialogue active
        if (_isDialogueActive)
            return;
        
        // Normal input handling (attacks, movement, etc.)
        if (Input.GetMouseButtonDown(0))
        {
            Attack();  // Safe — dialogue is done
        }
    }
    
    void OnDialogueStart()
    {
        _isDialogueActive = true;
        Debug.Log("[Hero] Dialogue started — input disabled");
    }
    
    void OnDialogueComplete()
    {
        _isDialogueActive = false;
        Debug.Log("[Hero] Dialogue ended — input enabled");
    }
}
```

### Fix 4: Ensure Pose Disabled During Dialogue

**Hero should also be visually idle (WO-376):**

```csharp
public class YarnSpinnerUIController : MonoBehaviour
{
    private HeroPoseController _heroPoseController;
    
    void OnDialogueStart()
    {
        // ✅ Disable combat pose during dialogue
        if (_heroPoseController != null)
        {
            _heroPoseController.SetPose(HeroPoseController.PoseState.Idle);
        }
        
        // ✅ Disable hero attacks
        var heroController = FindObjectOfType<HeroController>();
        if (heroController != null)
        {
            heroController.SetInputEnabled(false);
        }
    }
    
    void OnDialogueComplete()
    {
        // ✅ Re-enable input after dialogue
        var heroController = FindObjectOfType<HeroController>();
        if (heroController != null)
        {
            heroController.SetInputEnabled(true);
        }
    }
}
```

---

## Canvas Hierarchy (Correct Setup)

```
Canvas (Dialogue)
├── Render Mode: ScreenSpace-Overlay
├── Sort Order: 100 (TOP LAYER)
├── GraphicsRaycaster (enabled)
└── DialoguePanel (Image)
    ├── raycastTarget: true
    ├── BlockRaycasts: true (✓ ENABLED)
    ├── DialogueText (TextMeshProUGUI)
    ├── ChoicesPanel
    │   └── ChoiceButtons (Buttons)
    └── ContinueButton

Canvas (Game World HUD)
├── Render Mode: ScreenSpace-Camera
├── Sort Order: 0 (BELOW dialogue)
├── GraphicsRaycaster (enabled)
└── GameHUD elements

(Game world 3D)
├── Hero (should NOT receive clicks during dialogue)
├── Towers
└── Enemies
```

---

## EventSystem Configuration

**In scene or via code:**

```csharp
public class EventSystemSetup : MonoBehaviour
{
    void Start()
    {
        var eventSystem = EventSystem.current;
        
        // ✅ Ensure EventSystem is active
        eventSystem.enabled = true;
        
        // ✅ Make sure dialogue is checked before game world
        // (GraphicsRaycaster order on Canvas determines this)
    }
}
```

---

## Testing Checklist

### Before Fix
- [ ] Load village, trigger dialogue
- [ ] Click dialogue box
- [ ] Hero attacks ❌ (BROKEN)
- [ ] Dialogue ignores click ❌

### After Fix
- [ ] Load village, trigger dialogue
- [ ] Dialogue appears with hero in IDLE pose
- [ ] Click dialogue box
- [ ] Dialogue handles click (choice selected or text advances) ✓
- [ ] Hero does NOT attack ✓
- [ ] Dialogue completes
- [ ] Hero input re-enabled ✓
- [ ] Repeat 5+ times (no regression)

---

## Debug Checklist

**If input still passes through:**

1. **Check Canvas Setup:**
   ```csharp
   var canvas = GetComponent<Canvas>();
   Debug.Log($"Canvas RenderMode: {canvas.renderMode}");
   Debug.Log($"Canvas SortOrder: {canvas.sortingOrder}");
   Debug.Log($"GraphicsRaycaster exists: {canvas.GetComponent<GraphicsRaycaster>() != null}");
   ```

2. **Check Panel BlockRaycasts:**
   ```csharp
   var panelImage = GetComponent<Image>();
   Debug.Log($"Panel raycastTarget: {panelImage.raycastTarget}");
   ```

3. **Check EventSystem:**
   ```csharp
   var eventSystem = EventSystem.current;
   Debug.Log($"EventSystem enabled: {eventSystem.enabled}");
   Debug.Log($"Current selected: {eventSystem.currentSelectedGameObject}");
   ```

4. **Test Raycasting:**
   ```csharp
   var pointerData = new PointerEventData(EventSystem.current);
   pointerData.position = Input.mousePosition;
   var results = new List<RaycastResult>();
   EventSystem.current.RaycastAll(pointerData, results);
   
   Debug.Log($"Raycast hits: {results.Count}");
   foreach (var result in results)
   {
       Debug.Log($"  - {result.gameObject.name}");
   }
   ```

---

## Files to Modify

- `Assets/_Modules/Core/Dialogue/YarnSpinnerUIController.cs` — Canvas setup + dialogue events
- `Assets/_Modules/Village/Hero/HeroController.cs` — Input disable during dialogue
- Scene: Dialogue Canvas Inspector — Verify sort order + settings

---

## Related Work Orders

- WO-375: Yarn Spinner Threading (console error)
- WO-376: Hero Pose Initialization (idle pose during dialogue)
- WO-374: Character Selection UI (similar UI blocking issue)

---

## Acceptance Criteria

- [ ] Dialogue Canvas has sort order > game world (100+)
- [ ] DialoguePanel has BlockRaycasts enabled
- [ ] GraphicsRaycaster on dialogue Canvas
- [ ] Hero input disabled during dialogue
- [ ] Hero in idle pose during dialogue
- [ ] Clicks on dialogue = dialogue handles input
- [ ] Clicks during dialogue do NOT attack
- [ ] Input re-enabled after dialogue completes
- [ ] Works in 5+ test runs (no regression)

---

## Priority

**CRITICAL.** Player can break story by attacking during dialogue. Game is unplayable until fixed.

---

## Notes

- This is an event system / UI layering issue
- Most common cause: Canvas sort order not set correctly
- Second common cause: Image.raycastTarget = false (not blocking clicks)
- Third common cause: Player input not checking for dialogue state

**Once fixed:** Dialogue will be fully functional and story won't be interrupted by accidental attacks. ✅
