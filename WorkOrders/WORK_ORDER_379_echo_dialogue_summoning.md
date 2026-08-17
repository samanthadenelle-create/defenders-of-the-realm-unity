<!-- era-sweep-2026-08-17 -->
> ### ⚠ AGED 2026-08-17 — still READY, but unverified since 2026-06-22
> The 2026-08-17 era sweep found **no evidence** that this WO's subject was deleted or superseded, so its **Status stays READY** and nothing else was changed. It is simply OLD (git first-add 2026-06-22) and has not been re-verified against current canon (`CANON_GROUND_TRUTH_*.md`, CLAUDE.md §7). **Re-verify before pulling it.**

# WO-379: Echo Auto-Summoning on Yarn Spinner Dialogue — Story Integration

**Status:** READY TO IMPLEMENT  
**Estimated Effort:** P0 (0.5 days — Yarn Spinner hooks + Echo deploy)  
**Priority:** HIGH (narrative integration, companion introduction)  
**Lane:** 12 Narrative/Quests

---

## Overview

**Feature:** Echo (pet companion) automatically summons when:
1. **Option A:** Yarn Spinner dialogue starts (first dialogue ever), OR
2. **Option B:** Specific dialogue line plays (when character says "you caught someone's attention")

**Goal:** Introduce Echo narratively — pet appears during story, not as random event.

---

## Two Implementation Approaches

### Approach 1: Deploy on Dialogue Start

**Trigger:** Any Yarn Spinner dialogue begins

```csharp
public class YarnSpinnerUIController : MonoBehaviour
{
    private EchoCompanion _echo;
    private DialogueRunner _dialogueRunner;
    
    void Start()
    {
        _dialogueRunner = GetComponent<DialogueRunner>();
        _echo = FindObjectOfType<EchoCompanion>();
        
        // Listen for dialogue start
        _dialogueRunner.onDialogueStart.AddListener(OnDialogueStart);
    }
    
    void OnDialogueStart()
    {
        // ✅ Deploy Echo when dialogue begins
        if (_echo != null && !_echo.IsDeployed)
        {
            _echo.Deploy();
            Debug.Log("[Echo] Summoned at dialogue start");
        }
    }
}
```

**When:** Dialogue loads on scene entry → Echo appears → Story begins

### Approach 2: Deploy on Specific Dialogue Line

**Trigger:** When specific line is spoken ("you caught someone's attention")

```csharp
public class YarnSpinnerUIController : MonoBehaviour
{
    private EchoCompanion _echo;
    private DialogueRunner _dialogueRunner;
    
    void Start()
    {
        _dialogueRunner = GetComponent<DialogueRunner>();
        _echo = FindObjectOfType<EchoCompanion>();
        
        // Listen for dialogue line events
        _dialogueRunner.onLineStart.AddListener(OnLineStart);
    }
    
    void OnLineStart(LocalizedLine line)
    {
        string text = line.Text.Text;
        
        // Check if this is the "caught attention" line
        if (text.Contains("caught someone's attention") && !_echo.IsDeployed)
        {
            _echo.Deploy();
            Debug.Log("[Echo] Summoned at story beat: caught attention");
        }
    }
}
```

**When:** Dialogue reaches specific narrative moment → Echo appears → Story continues

---

## Recommended: Hybrid Approach

**Best practice:** Deploy Echo on dialogue start, but add flourish when reaching the story beat.

```csharp
public class EchoDynamicSummoning : MonoBehaviour
{
    private EchoCompanion _echo;
    private DialogueRunner _dialogueRunner;
    private bool _hasDeployed = false;
    
    void Start()
    {
        _dialogueRunner = GetComponent<DialogueRunner>();
        _echo = FindObjectOfType<EchoCompanion>();
        
        _dialogueRunner.onDialogueStart.AddListener(OnDialogueStart);
        _dialogueRunner.onLineStart.AddListener(OnLineStart);
        _dialogueRunner.onDialogueComplete.AddListener(OnDialogueComplete);
    }
    
    void OnDialogueStart()
    {
        // ✅ Deploy Echo at dialogue start (player sees companion early)
        if (_echo != null && !_hasDeployed)
        {
            _echo.Deploy(quietly: true);  // Subtle entry
            _hasDeployed = true;
        }
    }
    
    void OnLineStart(LocalizedLine line)
    {
        string text = line.Text.Text;
        
        // ✅ Add emphasis when reaching the story beat
        if (text.Contains("caught someone's attention") && _echo.IsDeployed)
        {
            // Play special animation, sound, or visual effect
            _echo.PlayAttentionAnimation();
            CoreServices.Audio.PlaySfx(SfxId.EchoTheme, _echo.transform.position);
        }
    }
    
    void OnDialogueComplete()
    {
        // Keep Echo visible after dialogue (companion stays in world)
        // Optional: Play Echo animation as dialogue ends
    }
}
```

---

## Echo Deployment Implementation

### EchoCompanion.cs Method

```csharp
public class EchoCompanion : MonoBehaviour
{
    public bool IsDeployed { get; private set; } = false;
    
    public void Deploy(bool quietly = false)
    {
        if (IsDeployed) return;
        
        // Show Echo in world
        gameObject.SetActive(true);
        
        // Play summoning animation
        if (!quietly)
        {
            PlaySummonAnimation();
        }
        
        // Play audio cue
        CoreServices.Audio.PlaySfx(SfxId.EchoSummon, transform.position);
        
        // Tutorial: Show Echo interaction tooltip
        ShowEchoTutorial();
        
        IsDeployed = true;
        Debug.Log("[Echo] Deployed successfully");
    }
    
    public void PlayAttentionAnimation()
    {
        // Special animation when story mentions Echo
        _animator.CrossFade("Attention", 0.3f);
    }
    
    private void PlaySummonAnimation()
    {
        // Fade in or appear with particle effect
        _animator.CrossFade("Summon", 0.5f);
    }
    
    private void ShowEchoTutorial()
    {
        // Display tooltip: "Meet Echo — your companion"
        // Or show interaction hint
    }
}
```

---

## Yarn Spinner Integration Points

### Option 1: Hook in YarnSpinnerUIController

```csharp
public class YarnSpinnerUIController : MonoBehaviour
{
    void Start()
    {
        var runner = GetComponent<DialogueRunner>();
        
        // Method 1: Deploy on any dialogue start
        runner.onDialogueStart.AddListener(() =>
        {
            var echo = FindObjectOfType<EchoCompanion>();
            if (echo != null) echo.Deploy();
        });
        
        // Method 2: Deploy on specific line
        runner.onLineStart.AddListener((line) =>
        {
            if (line.Text.Text.Contains("caught someone's attention"))
            {
                var echo = FindObjectOfType<EchoCompanion>();
                if (echo != null) echo.PlayAttentionAnimation();
            }
        });
    }
}
```

### Option 2: Separate Script (Cleaner)

Create dedicated class:

```csharp
public class DialogueEchoIntegration : MonoBehaviour
{
    [SerializeField] private string _attentionLineKeyword = "caught someone's attention";
    private EchoCompanion _echo;
    
    void Start()
    {
        _echo = FindObjectOfType<EchoCompanion>();
        var dialogueRunner = GetComponent<DialogueRunner>();
        
        dialogueRunner.onDialogueStart.AddListener(OnDialogueStart);
        dialogueRunner.onLineStart.AddListener(OnLineStart);
    }
    
    void OnDialogueStart()
    {
        if (_echo != null && !_echo.IsDeployed)
        {
            _echo.Deploy(quietly: true);
        }
    }
    
    void OnLineStart(LocalizedLine line)
    {
        if (_echo != null && line.Text.Text.Contains(_attentionLineKeyword))
        {
            _echo.PlayAttentionAnimation();
        }
    }
}
```

---

## Story Flow

```
Player loads village
    ↓
Yarn Spinner dialogue triggers
    ↓
[Option A] Echo deploys immediately (quiet summoning)
    ↓
NPC talks about village...
    ↓
NPC says: "You caught someone's attention..."
    ↓
[Option B] Echo plays attention animation (emphasizes moment)
    ↓
Story continues
    ↓
Dialogue ends
    ↓
Echo stays visible in world
    ↓
Player can interact with Echo (tutorial)
```

---

## Visual Integration

### Summoning Effects

**Quiet summon (dialogue start):**
- Echo fades in gradually (0.5s)
- Minimal animation
- No sound (or soft chime)
- Player notices, but doesn't interrupt dialogue

**Attention moment (story beat):**
- Echo plays special animation (nod, bounce, excitement)
- Audio cue (Echo theme excerpt or magical sound)
- Visual sparkle or light effect
- Emphasizes narrative importance

---

## Configuration

**In Inspector or script:**

```csharp
public class DialogueEchoIntegration : MonoBehaviour
{
    [Header("Dialogue Line Triggers")]
    [SerializeField] private string _attentionLineKeyword = "caught someone's attention";
    [SerializeField] private bool _deployOnDialogueStart = true;
    [SerializeField] private bool _playSoundOnAttention = true;
    
    [Header("Echo Summoning")]
    [SerializeField] private bool _quietSummon = true;
    [SerializeField] private float _summonDuration = 0.5f;
}
```

---

## Testing Checklist

- [ ] Load village, trigger dialogue
- [ ] Echo appears automatically (or at specified moment)
- [ ] Echo summoning animation plays smoothly
- [ ] Dialogue continues uninterrupted
- [ ] Specific line triggers attention animation (if using Option 2)
- [ ] Echo remains visible after dialogue ends
- [ ] Player can interact with Echo (tutorial)
- [ ] Audio cues play correctly
- [ ] No conflicts with WO-376 (hero pose) or WO-377 (input blocking)
- [ ] Works in 5+ dialogue runs (no regression)

---

## Files to Create/Modify

### New Files
- `Assets/_Modules/Companion/DialogueEchoIntegration.cs` (recommended)

### Modify
- `Assets/_Modules/Companion/EchoCompanion.cs` — Add Deploy() method + animations
- `Assets/_Modules/Core/Dialogue/YarnSpinnerUIController.cs` — Add hooks (if not separate script)

### Already Exists (from WO-360)
- `Assets/_Modules/Companion/EchoCompanion.cs` (should already have IsDeployed check)

---

## Dependencies

**Requires:**
- Yarn Spinner dialogue system (WO-358) ✓
- Echo companion (WO-360) ✓
- Echo animation rig and summon animation

**Blocked by:**
- WO-375 (threading error must be fixed first)
- WO-377 (input blocking must work)

---

## Related Work Orders

- WO-360: Companion Echo Outpost (Echo introduction)
- WO-358: Yarn Spinner Prefab (dialogue system)
- WO-364: Companion Gear Setup (Echo interactions)
- WO-375: Threading fix (must be fixed first)
- WO-377: Input blocking (must work correctly)

---

## Recommendation

**Use Hybrid Approach:**
1. Deploy Echo on dialogue start (quietly, no interruption)
2. Emphasize with attention animation at story beat
3. Keep Echo visible in world after dialogue

**Why:** Introduces companion naturally, emphasizes narrative moment, allows player interaction.

---

## Acceptance Criteria

- [ ] Echo deploys when Yarn Spinner dialogue starts
- [ ] Echo responds to specific story line (attention moment)
- [ ] Summoning is smooth and non-disruptive
- [ ] Audio/VFX cues work correctly
- [ ] Echo stays in world after dialogue
- [ ] No input conflicts during dialogue
- [ ] Works in 5+ dialogue runs
- [ ] Matches WO-360 companion introduction flow

---

## Priority

**HIGH.** Nice narrative integration — companion appears during story rather than as separate event. Elevates storytelling and immersion.

---

## Notes

- Echo should deploy quietly at dialogue start (player focus on story)
- Special animation at "caught attention" line adds drama
- Echo stays in world after dialogue (for tutorial/interaction)
- Consider Echo as NPC during dialogue (can be referenced in story)
- Optional: Add Echo dialogue reactions (head turns, animations) during NPC speech
