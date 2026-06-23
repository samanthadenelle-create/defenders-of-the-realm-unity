# WO-358: Yarn Spinner Welcome Dialogue Auto-Load

**Status:** READY TO IMPLEMENT  
**Estimated Effort:** P1 (1 day)  
**Priority:** Medium (first-time narrative flow)  
**Lane:** Narrative/Dialogue

---

## Overview

Set up Yarn Spinner dialogue to auto-load on first village entry. DialogueService is already implemented but the DialogueSystem prefab (Resources/Dialogue/DialogueSystem) is missing, causing silent failures. Create the prefab with a code-built ClassicRPGDialoguePresenter (WebGL-safe), hook it into TutorialDirector, and wire the welcome dialogue node to play on entry.

**Why:** New players see nothing on first village load; dialogue system fails silently. Yarn Spinner is compiled and ready but the runtime presenter is missing.

---

## Acceptance Criteria

- [ ] Create `Resources/Dialogue/DialogueSystem` prefab with DialogueRunner + ClassicRPGDialoguePresenter (code-built, no UXML)
- [ ] DialogueService.Play("WelcomeToElarion") successfully instantiates and displays dialogue
- [ ] Welcome dialogue plays automatically on first village entry (via TutorialDirector or OnboardingIntegrator)
- [ ] Dialogue renders in WebGL build (no UXML, no Resources.Load failures)
- [ ] Players can advance with click/tap (LineAdvancer already configured)
- [ ] Dialogue dismisses and village gameplay resumes after completion
- [ ] Console logs (not errors) on missing nodes or prefab

---

## Files to Modify

### New Files
- `Assets/Resources/Dialogue/DialogueSystem.prefab` — DialogueRunner + presenter UI
- Create `Assets/Resources/Dialogue/` folder if it doesn't exist

### Existing Files
- `Assets/_Modules/Village/Tutorial/TutorialDirector.cs` — Add welcome dialogue call on tutorial start or end
- Alternatively: `Assets/_Modules/Village/Onboarding/OnboardingIntegrator.cs` — If using existing onboarding seam

### No Changes Required
- DialogueService (ready to use)
- Yarn project (assumed compiled in DefendersDialogue.yarnproject)

---

## Design Spec

### DialogueSystem Prefab Structure

```
DialogueSystem (Root GameObject)
├─ DialogueRunner (Component)
│  ├─ Yarn Project: DefendersDialogue (scriptable object)
│  ├─ Variable Storage: InMemoryVariableStorage
│  ├─ Dialogue Views: [ ClassicRPGDialoguePresenter ]
│  └─ Line Provider: TextLineProvider
├─ DialogueCommandBridge (Component, runtime-added by DialogueService)
│  └─ Wires Yarn <<commands>> to gameplay hooks
└─ ClassicRPGDialoguePresenter (Component)
   ├─ Canvas + UIDocument (code-built, no .uxml)
   ├─ DialogueAdvancer (click/tap to advance)
   └─ DialogueContainer (displays lines + character name)
```

### Prefab Setup Instructions

1. **Create empty GameObject** named "DialogueSystem"
2. **Add DialogueRunner component:**
   - Yarn Project → DefendersDialogue (from Assets)
   - Variable Storage → Create InMemoryVariableStorage
   - Dialogue Views → [ 1 item ] ClassicRPGDialoguePresenter
   - Line Provider → TextLineProvider
3. **Add ClassicRPGDialoguePresenter component** (from Yarn Spinner Classic RPG addon)
   - Configure for code-built UI (check addon docs)
4. **Save prefab** to `Assets/Resources/Dialogue/DialogueSystem.prefab`

Alternatively, use the Yarn Spinner addon's built-in DialogueSystem prefab if it exists, then move it to Resources/Dialogue/.

### Welcome Dialogue Node

Ensure this node exists in DefendersDialogue.yarnproject:

```yarn
title: WelcomeToElarion
---
NARRATOR: Welcome to Elarion, brave defender.
NARRATOR: The Heart of the village beats strong, but dark forces gather at the gates.
NARRATOR: Build towers, train your party, and lead the village through the coming waves.
NARRATOR: Your journey begins now.
===
```

Node must be compiled into DefendersDialogue.yarnproject before play.

---

## Implementation Notes

### TutorialDirector Hook (Option 1)

Add to TutorialDirector.Start() or after tutorial completion:

```csharp
private void OnTutorialComplete()
{
    // After 7-scene tutorial, play welcome
    DialogueService.Play("TutorialComplete");
    
    // Or, if tutorial is skipped:
    // DialogueService.Play("WelcomeToElarion");
}
```

### OnboardingIntegrator Hook (Option 2)

Use existing onboarding flow. In OnboardingFlow:

```csharp
private void OnOnboardingStart()
{
    // Play welcome before coach marks
    DialogueService.Play("WelcomeToElarion");
}
```

### DialogueService Usage

```csharp
// Somewhere in village load sequence (TutorialDirector / OnboardingIntegrator)
if (DialogueService.Play("WelcomeToElarion"))
{
    Debug.Log("Welcome dialogue started.");
}
else
{
    Debug.LogError("Welcome dialogue failed — check console above.");
}
```

---

## Testing Checklist

- [ ] DialogueSystem prefab exists at correct path
- [ ] DialogueService.Play("WelcomeToElarion") returns true (no errors)
- [ ] Dialogue UI appears on screen (canvas renders)
- [ ] Click/tap advances lines
- [ ] All lines display correctly (text reads clearly)
- [ ] Dialogue completes and dismisses (no stuck UI)
- [ ] Works in editor play mode
- [ ] Works in WebGL build (test on device or emulator)
- [ ] Console shows no errors, only logs
- [ ] Non-existent node gracefully logs error (not crash)
- [ ] Welcome plays on first village entry

---

## What NOT to Touch

- DialogueService logic (already robust)
- Yarn project compilation (handled by Yarn Spinner addon)
- LineAdvancer configuration (already tuned)
- Other dialogue nodes (StructureMenu, etc.)

---

## Dependencies

- **Depends on:** Yarn Spinner addon installed, DefendersDialogue.yarnproject compiled
- **Unblocks:** Narrative FTUE, character intros
- **Parallel:** None (1-day task)

---

## Troubleshooting

**"Node 'WelcomeToElarion' is not in the compiled Yarn program"**
→ Add the node to DefendersDialogue.yarn, save, let Yarn addon recompile

**"DialogueSystem prefab not found"**
→ Check path is exactly `Assets/Resources/Dialogue/DialogueSystem.prefab`

**UI doesn't render in WebGL**
→ Ensure ClassicRPGDialoguePresenter uses code-built UI (no .uxml files)
→ Check PanelSettings are correct (see Yarn Spinner addon docs)

**Line doesn't advance on click/tap**
→ Check DialogueAdvancer component is attached
→ Verify input is not blocked by BuildMode or other UI

---

## Acceptance Sign-Off

- [ ] Prefab created and path verified
- [ ] Welcome dialogue plays and displays correctly
- [ ] Works in WebGL build
- [ ] Console clean (no errors, only logs)
- [ ] Player can dismiss and resume gameplay
