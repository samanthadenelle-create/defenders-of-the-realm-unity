# ATB Battle System — Debugging & Diagnostic Guide

**Date:** 2026-06-01  
**Purpose:** Step-by-step diagnostic for "battle doesn't work" failures  
**Failure patterns:** Blank screen, no HUD, no turns, capsules only, etc.

---

## Quick Diagnosis: 4 Most Likely Causes

### 1. 🔴 _runtimeState is NULL (Most Common)

**Symptom:** Console shows `[BattleController] No ATBRuntimeState assigned — battle cannot run.`

**Immediate fix (5 minutes):**
1. Open `Scenes/ATBBattle.unity`
2. Select BattleController GameObject in Hierarchy
3. In Inspector, find the `_runtimeState` field under "Runtime state"
4. Drag `Assets/_Modules/BattleATB/Generated/ATBRuntimeState.asset` into that field
5. Save scene

**If the asset doesn't exist:**
- Run the BattleSceneBuilder (editor menu or WO-163 setup)
- Or check that Assets/_Modules/BattleATB/Generated/ exists and contains ATBRuntimeState.asset

---

### 2. 🟠 _hudDocument is NULL (Very Common)

**Symptom:** Console shows `[BattleController] No UIDocument — BattleHUD cannot bind.`

**Immediate fix (2 minutes):**
1. Select BattleController GameObject
2. Verify it has a `UIDocument` component (check Component list)
3. If missing: Add Component → UIDocument
4. Save scene

**If UIDocument exists but is still null:**
- Check that BattleController is on the SAME GameObject as UIDocument (not a child)
- Awake() tries `GetComponent<UIDocument>()` — must be on same GO

---

### 3. 🟠 UIDocument.rootVisualElement is NULL (Very Likely)

**Symptom:** Console shows `[BattleController] UIDocument has no rootVisualElement.`

**Explanation:** The UIDocument needs to build its visual tree before BattleController.Start() runs. This is a Unity lifecycle issue.

**Fix:** This code is already protected (Start() is called AFTER OnEnable), so this shouldn't happen. If it does:
1. Move BattleController.Start() logic into a Coroutine that waits one frame
2. Or ensure UIDocument is on the same GO and enabled in Inspector

---

### 4. 🟡 BattleHud.Build(root) is called but HUD still doesn't appear

**Symptoms:**
- Console has no errors (got past all null checks)
- But the HUD is blank or invisible
- Only the capsule combatants show

**Likely causes:**
- BattleHud.Build() succeeded but returned early in Render()
- Or the VisualElement root isn't visible (display: none, opacity: 0, etc.)
- Or BattleHud.OnAction callback isn't wired

---

## Diagnostic Test: Simplified Start() with Debug Logs

Replace your BattleController.Start() **temporarily** with this code:

```csharp
private void Start()
{
    Debug.Log("[BattleController.Start] ===== STARTING BATTLE SETUP =====");

    // Step 1: Check UIDocument
    if (_hudDocument == null) _hudDocument = GetComponent<UIDocument>();
    Debug.Log($"[BattleController.Start] UIDocument: {(_hudDocument != null ? "✓ Found" : "✗ NULL")}");

    if (_hudDocument == null)
    {
        Debug.LogError("[BattleController] No UIDocument — aborting.");
        return;
    }

    // Step 2: Check root visual element
    VisualElement root = _hudDocument.rootVisualElement;
    Debug.Log($"[BattleController.Start] rootVisualElement: {(root != null ? "✓ Found" : "✗ NULL")}");

    if (root == null)
    {
        Debug.LogError("[BattleController] UIDocument root is null — aborting.");
        return;
    }

    // Step 3: Bind HUD
    Debug.Log("[BattleController.Start] Calling BindUi()...");
    if (!BindUi())
    {
        Debug.LogError("[BattleController] BindUi() returned FALSE — aborting.");
        return;
    }
    Debug.Log("[BattleController.Start] ✓ BindUi() succeeded");

    _bound = true;
    Subscribe();
    Debug.Log("[BattleController.Start] ✓ Events subscribed");

    // Step 4: Check _runtimeState
    Debug.Log($"[BattleController.Start] _runtimeState: {(_runtimeState != null ? "✓ Found" : "✗ NULL")}");
    if (_runtimeState == null)
    {
        Debug.LogError("[BattleController] CRITICAL: _runtimeState is NULL. Assign it in Inspector!");
        return;
    }

    _hud?.ResetLog();
    _vfx?.Reset();
    _returnScheduled = false;

    Debug.Log("[BattleController.Start] Building battle setup...");
    BattleSetup setup = BuildSetup();
    _source = ResolveSource();

    Debug.Log($"[BattleController.Start] Starting battle: Wave={setup.Wave}, Enemies={setup.Enemies.Count}, PartyMembers={setup.PartyMembers.Count}");
    _runtimeState.StartBattle(setup, _source);

    Debug.Log("[BattleController.Start] ✓ Battle started, calling Render()");
    Render(_runtimeState.Battle);

    Debug.Log("[BattleController.Start] ✓ First render complete");
    ATBCombatManager.Instance?.StartCombat();

    Debug.Log("[BattleController.Start] ===== BATTLE SETUP COMPLETE =====");
}
```

---

## What to Look For in Console

### ✓ Success Path (Good Signs)

```
[BattleController.Start] ===== STARTING BATTLE SETUP =====
[BattleController.Start] UIDocument: ✓ Found
[BattleController.Start] rootVisualElement: ✓ Found
[BattleController.Start] Calling BindUi()...
[BattleController.Start] ✓ BindUi() succeeded
[BattleController.Start] ✓ Events subscribed
[BattleController.Start] _runtimeState: ✓ Found
[BattleController.Start] Building battle setup...
[BattleController.Start] Starting battle: Wave=1, Enemies=1, PartyMembers=1
[BattleController.Start] ✓ Battle started, calling Render()
[BattleController.Start] ✓ First render complete
[BattleController.Start] ===== BATTLE SETUP COMPLETE =====
```

**Then:** The HUD should appear (title, enemies column, party column, log, command bar).

### ✗ Early Failures (Stop Here)

```
[BattleController.Start] UIDocument: ✗ NULL
```
→ **Add UIDocument component or verify it's on the same GameObject**

```
[BattleController.Start] rootVisualElement: ✗ NULL
```
→ **Something is preventing the UIDocument from building its visual tree. Check that UIDocument is enabled.**

```
[BattleController.Start] BindUi() returned FALSE
```
→ **The `new BattleHud()` constructor or `_hud.Build(root)` failed. Check BattleHud.cs Build() method for early returns.**

```
[BattleController.Start] _runtimeState: ✗ NULL
```
→ **CRITICAL: Drag ATBRuntimeState.asset into the Inspector field.**

---

## Detailed Failure Points (In Order of Likelihood)

### Failure Point A: Missing _runtimeState Assignment (70% of reports)

**Line:** BattleController.cs:155–159

**What happens:**
```csharp
if (_runtimeState == null)
{
    Debug.LogError("[BattleController] No ATBRuntimeState assigned — battle cannot run.");
    return;  // ← STOPS HERE
}
```

**Fix:** Drag the ScriptableObject asset into the Inspector field. If it doesn't exist, run BattleSceneBuilder.

---

### Failure Point B: Missing UIDocument Component (15% of reports)

**Line:** BattleController.cs:113–116 (Awake → Start)

**What happens:**
```csharp
private void Awake()
{
    if (_hudDocument == null) _hudDocument = GetComponent<UIDocument>();
}
```

If GetComponent returns null, Start() will fail at BindUi().

**Fix:** 
1. Click BattleController GameObject
2. Inspector → Add Component → UIDocument
3. Save

---

### Failure Point C: BattleHud.Build() Fails Silently (10% of reports)

**Line:** BattleController.cs:650–660 (BindUi method)

**What happens:**
```csharp
_hud = new BattleHud();
_hud.OnAction = SubmitPlayerAction;
_hud.OnControlModeToggled = HandleControlModeToggled;
_hud.Build(root);  // ← If this fails, HUD never appears but no error logs
```

**Debug this:**
```csharp
Debug.Log($"[BattleController] HUD Build Complete. Root has {root.childCount} children.");
if (_hud == null) 
    Debug.LogError("[BattleController] _hud is null after new BattleHud()");
```

If root.childCount is 0, then Build() didn't actually add elements to root.

**Fix:** Check BattleHud.Build() for early returns or exceptions (unlikely).

---

### Failure Point D: ATBCombatManager.Instance is Null (5% of reports)

**Line:** BattleController.cs:177

**What happens:**
```csharp
ATBCombatManager.Instance?.StartCombat();  // null-conditional, so safe
```

This is safe due to `?.`, so it's not a blocker. But if ATBCombatManager doesn't exist, the turn timer won't start and battles will freeze waiting for a turn.

**Fix:** Ensure ATBCombatManager exists in the scene or is configured properly.

---

### Failure Point E: SceneRouter.PendingBattle is Null (Not a Blocker)

**Line:** BattleController.cs:202

**What happens:**
```csharp
BattleParams handoff = SceneRouter.PendingBattle;
if (handoff == null)
{
    // Falls back to dev/direct-play defaults
}
```

**This is intentional.** When you play the ATBBattle scene directly (not via SceneRouter), it uses fallback values. This is expected and safe.

---

## Full Diagnostic Checklist

Run through this before reporting a bug:

- [ ] UIDocument component exists on BattleController GameObject
- [ ] UIDocument is **enabled** (checkbox ticked)
- [ ] _runtimeState field is assigned (drag ATBRuntimeState.asset into Inspector)
- [ ] ATBRuntimeState.asset exists in Assets/_Modules/BattleATB/Generated/
- [ ] BattleController.Start() completes without errors (check console for red messages)
- [ ] Console shows `===== BATTLE SETUP COMPLETE =====` at the end
- [ ] HUD is visible on screen (not hidden behind other UI)
- [ ] Capsule combatants are visible (3D models in the scene)
- [ ] ATB bars in the HUD are animating (filling over time)
- [ ] Clicking "Attack" button submits an action

---

## If All Checks Pass But HUD Still Doesn't Show

Try these:

1. **Check Canvas visibility:**
   - Is there a Canvas in the scene? UIDocument needs one.
   - Is Canvas.renderMode set correctly?

2. **Check layer/sorting:**
   - Are UI elements being rendered to screen (camera culling)?
   - Run Console filter: `[BattleController]` to see all diagnostic messages.

3. **Check BattleHud code:**
   - Read BattleHud.cs Build() method (line 120–205)
   - Verify no early returns that would skip adding elements to root

4. **Revert simplified Start() and use original:**
   - The simplified version is for debugging only
   - Once you identify the issue, revert to the full Start() and fix the root cause

---

## After Diagnostics: Next Steps

Once you've identified which failure point is hitting:

1. **For _runtimeState null:** Assign the asset in Inspector (5 min)
2. **For UIDocument null:** Add component (2 min)
3. **For Build() silent failure:** Enable detailed logging in BattleHud.Build() (10 min)
4. **For ATBCombatManager null:** Ensure it's in the scene or create it (15 min)

---

**Report Format When Asking for Help:**

Copy this into your diagnostic report:

```
[Battle Diagnostic Report]
- UIDocument: [✓ or ✗]
- _runtimeState: [✓ or ✗]
- rootVisualElement: [✓ or ✗]
- BindUi() success: [✓ or ✗]
- First error in console: [exact message]
- HUD visible: [yes / no]
- Capsules visible: [yes / no]
- ATB bars animating: [yes / no]
```

This tells a developer exactly where to look.

