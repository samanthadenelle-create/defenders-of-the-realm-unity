<!-- era-sweep-2026-08-17 -->
> ### ⚠ AGED 2026-08-17 — still READY, but unverified since 2026-06-22
> The 2026-08-17 era sweep found **no evidence** that this WO's subject was deleted or superseded, so its **Status stays READY** and nothing else was changed. It is simply OLD (git first-add 2026-06-22) and has not been re-verified against current canon (`CANON_GROUND_TRUTH_*.md`, CLAUDE.md §7). **Re-verify before pulling it.**

# WO-376: Hero Pose Initialization — Idle State on Scene Load & Dialogue

**Status:** READY TO IMPLEMENT  
**Estimated Effort:** P0 (0.25 days — state initialization)  
**Priority:** HIGH (visual polish, immersion breaking)  
**Lane:** 4 UI/HUD

---

## Overview

**Issue:** Hero is in **combat pose** (weapon drawn, swinging arms aggressively) during:
- Village scene load
- Yarn Spinner dialogue intro
- Any non-battle context

**Should be:** Hero in **idle pose** (weapon sheathed, relaxed, natural breathing)

**Result:** Visual disconnect — hero looks ready for battle during story dialogue.

---

## Root Cause

**HeroPoseController (WO-365) is not being initialized to idle state:**

```csharp
// Missing initialization
public class VillageController : MonoBehaviour
{
    void Start()
    {
        // ❌ MISSING: Set hero to idle pose on scene load
        // Hero spawns in default state (likely combat pose)
    }
}
```

**Expected behavior:**
- Scene load → Hero enters village → SetPose(Idle)
- Dialogue trigger → SetPose(Idle) again (ensure state)
- Battle start → SetPose(Combat)
- Battle end → SetPose(Idle)

---

## Fix Required

### Step 1: Ensure HeroPoseController Exists

**Verify file exists:**
```
Assets/_Modules/Village/Hero/HeroPoseController.cs
```

**If missing:** Create it per WO-365 spec.

### Step 2: Initialize Hero to Idle on Scene Load

**In VillageController.cs or scene startup:**

```csharp
public class VillageController : MonoBehaviour
{
    private HeroPoseController _heroPoseController;
    
    void Start()
    {
        // Find hero pose controller
        _heroPoseController = FindObjectOfType<HeroPoseController>();
        
        if (_heroPoseController != null)
        {
            // ✅ CRITICAL: Set hero to idle pose when entering village
            _heroPoseController.SetPose(HeroPoseController.PoseState.Idle);
        }
        
        // ... rest of village initialization
    }
}
```

### Step 3: Ensure Idle Pose on Dialogue Start

**In Yarn Spinner UI setup:**

```csharp
public class YarnSpinnerUIController : MonoBehaviour
{
    private HeroPoseController _heroPoseController;
    
    void Start()
    {
        _heroPoseController = FindObjectOfType<HeroPoseController>();
        _dialogueRunner = GetComponent<DialogueRunner>();
        
        // Listen for dialogue events
        _dialogueRunner.onDialogueStart.AddListener(OnDialogueStart);
        _dialogueRunner.onDialogueComplete.AddListener(OnDialogueComplete);
    }
    
    void OnDialogueStart()
    {
        // ✅ Ensure hero is in idle pose during dialogue
        if (_heroPoseController != null)
        {
            _heroPoseController.SetPose(HeroPoseController.PoseState.Idle);
        }
    }
    
    void OnDialogueComplete()
    {
        // ✅ Keep idle pose after dialogue (unless battle follows)
        if (_heroPoseController != null)
        {
            _heroPoseController.SetPose(HeroPoseController.PoseState.Idle);
        }
    }
}
```

### Step 4: Verify Pose State Flow

**State machine should be:**

```
Village Load
    ↓
SetPose(Idle)  ← Hero relaxed, weapon sheathed
    ↓
[Player explores / Dialogue]
    ↓
SetPose(Idle)  ← Maintained during narrative
    ↓
Wave Starts / Battle Triggered
    ↓
SetPose(Combat)  ← Hero crouched, weapon drawn
    ↓
[Combat]
    ↓
Wave Victory
    ↓
SetPose(Idle)  ← Back to relaxed
```

---

## Animation Requirements

**Idle pose animation:**
- Hero standing naturally
- Weapon invisible/sheathed
- Breathing animation (subtle chest movement)
- No aggressive stance
- Relaxed shoulders

**Combat pose animation:**
- Hero crouched slightly
- Weapon visible/drawn
- Ready stance (feet wide)
- Weight forward
- Aggressive posture

**Transition between poses:**
- 0.3 second crossfade (smooth, not jarring)
- No popping or clipping

---

## Testing Checklist

- [ ] Load village scene
- [ ] Hero spawns in idle pose (weapon sheathed, relaxed)
- [ ] Hero does NOT have weapon drawn
- [ ] Hero does NOT swing arms aggressively
- [ ] Yarn Spinner dialogue triggers
- [ ] Hero stays in idle pose during dialogue
- [ ] Dialogue completes
- [ ] Hero remains in idle pose
- [ ] Wave trigger
- [ ] Hero smoothly transitions to combat pose
- [ ] Battle victory
- [ ] Hero returns to idle pose
- [ ] Repeat 3+ times (no state stuck)

---

## Files to Modify

### New/Ensure Exists
- `Assets/_Modules/Village/Hero/HeroPoseController.cs` (from WO-365)

### Modify
- `Assets/_Modules/Village/VillageController.cs` — Add initialization
- `Assets/_Modules/Core/Dialogue/YarnSpinnerUIController.cs` — Add dialogue hooks
- `Assets/_Modules/Village/Waves/WaveManager.cs` — Ensure SetPose(Combat) on wave start

---

## WO-365 Dependency

**This work order depends on WO-365 (Character Idle Pose States):**

If WO-365 is not implemented yet:
1. Implement WO-365 first (HeroPoseController + animations)
2. Then implement WO-376 (initialization)

If WO-365 is partially implemented:
- Verify HeroPoseController.SetPose() method exists
- Verify idle/combat animations are wired in Animator
- Verify weapon visibility toggle works

---

## Integration Checklist

- [ ] HeroPoseController exists and works
- [ ] VillageController calls SetPose(Idle) on Start
- [ ] YarnSpinnerUIController calls SetPose(Idle) on dialogue events
- [ ] WaveManager calls SetPose(Combat) on wave start
- [ ] WaveManager calls SetPose(Idle) on victory
- [ ] All pose transitions smooth (0.3s)
- [ ] No state gets stuck

---

## What NOT to Do

- ❌ Don't force SetPose(Combat) by default
- ❌ Don't skip initialization (hope hero defaults correctly)
- ❌ Don't transition poses too fast (looks jerky)
- ❌ Don't forget to reset pose after dialogue

---

## Acceptance Sign-Off

- [ ] Hero spawns in idle pose (visual check)
- [ ] Dialogue plays with hero in idle pose
- [ ] No weapon visible during dialogue
- [ ] No aggressive stance during story
- [ ] Combat poses correctly trigger on battle start
- [ ] Smooth state transitions
- [ ] Works in WebGL build

---

## Related Work Orders

- WO-365: Character Idle Pose States (prerequisite)
- WO-366: Idle Routines (plays within idle pose)
- WO-371: Combat Audio SFX (audio feedback on pose change)

---

## Priority

**HIGH.** Character animation state is part of immersion. Hero in combat pose during dialogue breaks the narrative feel. Quick fix — just initialize state correctly.

---

## Notes

- This is likely a simple "forgot to initialize" bug
- Hero defaults to last state (probably combat from dev testing)
- Setting pose on scene load + dialogue start = safety net
- No code complexity required — just state management
