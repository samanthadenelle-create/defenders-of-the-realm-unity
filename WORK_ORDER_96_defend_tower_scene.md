# WORK ORDER 96 — Defend the Tower Scene: Placeholder Geometry, Pet UI, Cooldown HUD, World-Space Numbers

**Status:** BUG FIX — READY TO IMPLEMENT
**Date:** 2026-05-28
**Priority:** High
**Scope:** Medium — four separate fixes in the Defend the Tower scene
**Observed:** Screenshot (Wave 3/5) —
  • Giant dark hexagonal prisms fill the scene instead of actual geometry
  • Pet action buttons (left side) are unstyled red debug boxes
  • Floating world-space numbers "18", "19", "9" — same bug class as WO-91
  • Ability buttons (Snare Trap, Mending Salve, Storm of Arrows) have no
    cooldown fill/timer visible on the HUD

---

## Bug 1 — Placeholder Hexagonal Geometry

### Root Cause

The scene uses default Unity primitive shapes (scaled cylinders/prisms) as
stand-in geometry. No actual tower environment art has been assigned.

### Fix

This is a scene art / placeholder replacement task. Until final assets exist,
replace the placeholder prisms with a simple but readable layout:

#### Option A — Simple environment primitives (immediate fix)

Replace each large hexagonal prism with a combination of standard primitives
that read as "dungeon corridor / arena":

```
Floor plane:
    GameObject → 3D → Plane
    Scale:  (30, 1, 30)
    Material: a simple stone/dirt URP Lit material (color #4A3B2A)

Wall segments (4 sides):
    GameObject → 3D → Cube
    Scale per wall: (30, 6, 1)
    Arrange at edges of floor plane
    Material: same stone material

Central tower object (placeholder):
    GameObject → 3D → Cylinder
    Scale: (2, 4, 2)
    Position: (0, 2, 0)  — scene centre
    Material: URP Lit #8B7355 (sandstone)
    Tag: HeartTarget  (so enemies path toward it — see WO-90/92)
```

Name the root object `DefendTowerEnvironment` and tag it so it can be easily
replaced with final art assets later.

#### Option B — Disable existing prisms, enable hidden environment

If a hidden or disabled environment already exists in the scene hierarchy
under a `[PLACEHOLDER]` or `[DISABLED]` parent, simply:
1. Disable the placeholder parent GO
2. Enable the real environment parent GO

Search:
```
Hierarchy search: "placeholder" / "environment" / "arena" / "dungeon"
```

---

## Bug 2 — Pet Action Buttons Are Unstyled Red Debug Boxes

### Root Cause

The pet action buttons on the left side of the screen are either:
1. Spawned as `UI.Button` with a default red Debug material (no sprite/style applied)
2. Driven by a script that creates buttons at runtime via `new GameObject()` without
   assigning a UI skin
3. The Canvas containing them is set to World Space instead of Screen Space Overlay

### Fix

#### Step 1 — Find the pet button script

```
grep -r "aether-sprite\|flame-pup\|ice-wolf\|PetAction\|PetButton" \
    Assets/ --include="*.cs" -l
```

Likely: `PetActionUI.cs`, `PetHUDController.cs`, or `CombatHUDManager.cs`.

#### Step 2 — Canonical `PetActionUI.cs`

**Path:** `Assets/_Modules/DefendTower/UI/PetActionUI.cs`

```csharp
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class PetActionUI : MonoBehaviour
{
    public static PetActionUI Instance { get; private set; }

    [Header("Button Template")]
    public GameObject petButtonPrefab;   // Prefab with Image + TMP_Text + Button

    [Header("Container")]
    public Transform buttonContainer;    // Vertical layout group in Screen-Space Canvas

    private readonly List<GameObject> _activeButtons = new();

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    /// <summary>Called on scene load with list of active pets.</summary>
    public void BuildPetButtons(List<PetData> pets)
    {
        // Clear existing
        foreach (var b in _activeButtons) Destroy(b);
        _activeButtons.Clear();

        foreach (var pet in pets)
        {
            if (petButtonPrefab == null)
            {
                Debug.LogError("[PetActionUI] petButtonPrefab not assigned!", this);
                return;
            }

            var btn = Instantiate(petButtonPrefab, buttonContainer);
            _activeButtons.Add(btn);

            // Set button label
            var label = btn.GetComponentInChildren<TMP_Text>();
            if (label != null)
                label.text = $"{pet.displayName}: Attack";

            // Set icon if PetData has one
            var icon = btn.transform.Find("Icon")?.GetComponent<Image>();
            if (icon != null && pet.icon != null)
                icon.sprite = pet.icon;

            // Wire button action
            var petRef = pet;
            btn.GetComponent<Button>().onClick.AddListener(() => OnPetActionPressed(petRef));
        }
    }

    private void OnPetActionPressed(PetData pet)
    {
        Debug.Log($"[PetAction] {pet.displayName} attacks!");
        VFXManager.Instance?.Play(VFXType.Impact_Physical,
            Vector3.zero + Vector3.up * 1f);
        // TODO: route to pet ability system
    }
}
```

#### Step 3 — Pet button prefab setup

Create `Assets/_Modules/DefendTower/UI/PetActionButton.prefab`:
```
GameObject: PetActionButton
├── Image (Background)
│     Sprite: UI/Rounded_Button  (or any rounded rect sprite)
│     Color:  #3A2D5A  (dark purple — distinct from ability buttons)
│     Size:   160 × 44
├── Icon (Image, left side, 32×32)
└── Label (TMP_Text, right of icon)
      Font Size: 14
      Color: #F0E8D0
      Text: "Pet Name: Attack"
```

#### Step 4 — Canvas check

Confirm the pet button container is on a **Screen Space — Overlay** canvas
(not World Space). If it is on a World Space canvas, change it:
```
Canvas → Render Mode → Screen Space — Overlay
```

---

## Bug 3 — World-Space Numbers (18, 19, 9)

### Root Cause

Same root cause as WO-91: countdown / enemy number text is on a world-space
TextMesh/TextMeshPro GameObject instead of a screen-space HUD canvas.
These are present in the Defend the Tower scene independently of the Village
scene fix.

The large "18" and "19" are likely enemy HP or wave enemy count labels
rendered in world space above enemies.

### Fix — Two separate sub-issues

#### Sub-issue A — Enemy HP/number labels in world space

If enemies show their HP or an ID number floating in world space:
1. Find the TextMesh component on the enemy prefab (likely a child GO named
   "HPLabel" or "EnemyNumber")
2. **Disable** or **delete** this component — enemy HP is tracked in
   `EnemyHealth.currentHealth` and shown on the HUD, not above the enemy
3. If it must be shown, use a Billboard Canvas with `Render Mode = World Space`
   and keep it very small (font size 8–12, scale 0.01)

```
grep -r "TextMesh\|TMP_Text\|worldText\|enemyLabel" \
    Assets/_Modules/Village/Enemies/ --include="*.cs" -l
```

#### Sub-issue B — Wave countdown numbers in world space

Apply the same fix as WO-91 to the Defend the Tower scene:
1. Find and delete the world-space countdown GameObject
2. Add `WaveCountdownText` TMP to the Defend the Tower HUD Canvas
3. Confirm `WaveCountdownUI.Instance` is present in this scene
   (add `WaveCountdownUI` component to the HUD Canvas root if not)
4. `WaveManager` in this scene calls:
   ```csharp
   WaveCountdownUI.Instance?.StartCountdown(wave.prewaveDelay, callback);
   ```

---

## Bug 4 — No Cooldown Indicators on Ability Buttons

### Root Cause

The ability buttons (Snare Trap, Mending Salve, Storm of Arrows) display their
labels but have no radial fill or timer showing cooldown state. Either:
1. `AbilityCooldownUI.cs` components are not attached to the button GameObjects
2. The ability scripts are not calling `AbilityCooldownUI.StartCooldown(cooldown)`
3. The fill Image child of each button is missing or has `fillMethod` not set
   to `Radial360`

### Fix

#### Step 1 — Button prefab structure

Each ability button needs an overlay Image for the cooldown fill:

```
AbilityButton (Button)
├── Icon (Image — ability art)
├── Label (TMP_Text — "Snare Trap")
├── CooldownFill (Image)
│     Image Type: Filled
│     Fill Method: Radial360
│     Fill Origin: Top
│     Fill Amount: 0  (starts transparent)
│     Color: #00000088  (semi-transparent dark overlay)
│     Raycast Target: false
└── CooldownTimer (TMP_Text, optional)
      Font Size: 20
      Alignment: Centre
      Color: White
```

#### Step 2 — `AbilityCooldownUI.cs`

**Path:** `Assets/_Modules/DefendTower/UI/AbilityCooldownUI.cs`

```csharp
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class AbilityCooldownUI : MonoBehaviour
{
    [Header("References")]
    public Image    cooldownFill;    // CooldownFill Image on this button
    public TMP_Text cooldownTimer;   // Optional countdown text
    public Button   button;

    private void Awake()
    {
        if (button == null) button = GetComponent<Button>();
        if (cooldownFill != null) cooldownFill.fillAmount = 0f;
    }

    public void StartCooldown(float duration)
    {
        if (duration <= 0f) return;
        StartCoroutine(CooldownRoutine(duration));
    }

    private IEnumerator CooldownRoutine(float duration)
    {
        button.interactable = false;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float remaining = duration - elapsed;

            if (cooldownFill  != null)
                cooldownFill.fillAmount = 1f - (elapsed / duration);

            if (cooldownTimer != null)
                cooldownTimer.text = remaining > 1f
                    ? Mathf.CeilToInt(remaining).ToString()
                    : "";

            yield return null;
        }

        if (cooldownFill  != null) cooldownFill.fillAmount  = 0f;
        if (cooldownTimer != null) cooldownTimer.text        = "";
        button.interactable = true;
    }
}
```

#### Step 3 — Wire in each ability

Attach `AbilityCooldownUI` to each of the three ability buttons. Assign:
- `cooldownFill` → the CooldownFill Image child
- `cooldownTimer` → the CooldownTimer TMP_Text child (optional)
- `button` → the Button component

In each ability's fire method, call:

```csharp
// Snare Trap
GetComponent<AbilityCooldownUI>()?.StartCooldown(snareAbility.cooldown);

// Mending Salve (already in WO-89 SalveAbility.Use())
GetComponent<AbilityCooldownUI>()?.StartCooldown(cooldown);

// Storm of Arrows
GetComponent<AbilityCooldownUI>()?.StartCooldown(stormAbility.cooldown);
```

---

## Scene Wiring Checklist

| Step | Action |
|---|---|
| Placeholder geometry | Disable prisms; add floor plane + wall cubes + central cylinder |
| `DefendTowerEnvironment` | Tag central structure as `HeartTarget` |
| Pet button Canvas | Confirm Render Mode = Screen Space Overlay |
| `PetActionUI` | Attach to HUD Canvas; assign `petButtonPrefab` and `buttonContainer` |
| World-space text | Find and delete/disable all TextMesh GOs on enemy prefabs and scene |
| `WaveCountdownUI` | Add to Defend Tower HUD Canvas if not present |
| CooldownFill Images | Add to each ability button (Radial360, fillAmount=0) |
| `AbilityCooldownUI` | Attach to Snare Trap, Mending Salve, Storm of Arrows buttons |
| Ability scripts | Call `AbilityCooldownUI.StartCooldown()` after each use |

---

## Files to Create / Edit

| File | Action |
|---|---|
| Defend the Tower scene | **Edit** — replace placeholder geometry; add `WaveCountdownUI` to HUD Canvas |
| Enemy prefabs | **Edit** — disable world-space HP/number TextMesh components |
| `Assets/_Modules/DefendTower/UI/PetActionUI.cs` | **Create** |
| `Assets/_Modules/DefendTower/UI/PetActionButton.prefab` | **Create** |
| `Assets/_Modules/DefendTower/UI/AbilityCooldownUI.cs` | **Create** (or reuse from WO-81) |
| Snare Trap, Mending Salve, Storm of Arrows button prefabs | **Edit** — add CooldownFill Image child |
| All three ability scripts | **Edit** — call `AbilityCooldownUI.StartCooldown()` |

---

## Acceptance Criteria

- [ ] Scene has a readable floor, walls, and central tower object — no dark hexagonal prisms
- [ ] Pet buttons (aether-sprite, flame-pup, ice-wolf) render with styled backgrounds and legible labels
- [ ] Pet buttons are inside a Screen Space — Overlay canvas (not World Space)
- [ ] No floating numbers ("18", "19", "9") render in world space
- [ ] Wave countdown renders as screen-space overlay (matching WO-91 WaveCountdownUI)
- [ ] Snare Trap, Mending Salve, Storm of Arrows buttons each show a radial fill draining during cooldown
- [ ] Buttons are non-interactable while on cooldown
- [ ] Cooldown fill resets to 0 (fully clear) when ability is ready
- [ ] Optional countdown timer number visible on fill during cooldown
- [ ] Tower Integrity bar (82/100) remains wired and visible — not affected by these changes
