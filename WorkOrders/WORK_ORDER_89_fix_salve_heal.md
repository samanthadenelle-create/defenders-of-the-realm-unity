<!-- era-sweep-2026-08-17 -->
> ### ⚠ AGED 2026-08-17 — still READY, but unverified since 2026-06-22
> The 2026-08-17 era sweep found **no evidence** that this WO's subject was deleted or superseded, so its **Status stays READY** and nothing else was changed. It is simply OLD (git first-add 2026-06-22) and has not been re-verified against current canon (`CANON_GROUND_TRUTH_*.md`, CLAUDE.md §7). **Re-verify before pulling it.**

# WORK ORDER 89 — Fix Salve Ability (E Key): Animation Plays, No Healing

**Status:** DONE — audit-verified as shipped (2026-08-21 backlog audit).
**Date:** 2026-05-28
**Priority:** Critical
**Scope:** Small — targeted edit to one ability script
**Observed:** Screenshot — hero HP does not increase after pressing E (Salve).
             Animation plays correctly. HeroHealth.Heal() is never reached.

---

## Root Cause

`SalveAbility.cs` (or whichever script drives the E key) triggers the
`"Heal"` animator trigger but does not call `HeroHealth.Heal(amount)`.
The animation plays as a stub with no functional code behind it.

Common causes in priority order:
1. `GetComponent<HeroHealth>()` returns null because `HeroHealth` is on a
   parent or child GameObject — reference not cached correctly.
2. `Heal()` call exists but is inside an `if (false)` guard or a
   commented-out block.
3. The ability fires from a different GameObject than the one with
   `HeroHealth` and the reference was never wired in the Inspector.
4. `healAmount` field is 0 — the call fires but adds nothing.

---

## Fix

### Step 1 — Find the Salve ability script

Search the project for the E-key binding:

```
grep -r "KeyCode.E\|\"E\"\|Salve\|salve\|Heal\|heal" Assets/ --include="*.cs" -l
```

Likely file: `SalveAbility.cs`, `HeroAbilityController.cs`,
`WizardAbilityController.cs`, or a generic `AbilitySlot.cs`.

---

### Step 2 — Canonical Salve ability implementation

**Path:** wherever the Salve ability logic currently lives.

Replace or patch the `Use()` / `Activate()` / `OnAbilityFired()` method:

```csharp
using UnityEngine;
using System.Collections;

public class SalveAbility : MonoBehaviour
{
    [Header("Heal Settings")]
    public int   healAmount     = 25;           // Amount restored per use
    public float cooldown       = 8f;           // Seconds between uses

    [Header("References")]
    public HeroHealth heroHealth;               // Assign in Inspector OR cache in Awake

    [Header("VFX / Audio")]
    public VFXType healVFX      = VFXType.Impact_Heal;

    private Animator _animator;
    private float    _nextUseTime;

    private static readonly int _healTrigger = Animator.StringToHash("Heal");

    private void Awake()
    {
        _animator = GetComponentInParent<Animator>();   // hero animator may be on parent

        // Auto-find HeroHealth on self, parent, or children
        if (heroHealth == null)
            heroHealth = GetComponentInParent<HeroHealth>()
                      ?? GetComponentInChildren<HeroHealth>();

        if (heroHealth == null)
            Debug.LogError("[SalveAbility] HeroHealth not found! Assign in Inspector.", this);
    }

    /// <summary>
    /// Called by input handler when E is pressed, or by the ability button's onClick.
    /// </summary>
    public void Use()
    {
        if (Time.time < _nextUseTime) return;
        if (heroHealth == null) return;

        _nextUseTime = Time.time + cooldown;

        // 1. Play animation
        _animator?.SetTrigger(_healTrigger);

        // 2. Actually heal — THIS IS THE FIX
        heroHealth.Heal(healAmount);

        // 3. VFX + audio
        VFXManager.Instance?.Play(healVFX, transform.position + Vector3.up * 0.8f);
        // AudioService.Instance?.PlaySfx(SfxId.Heal);

        // 4. Cooldown UI
        GetComponent<AbilityCooldownUI>()?.StartCooldown(cooldown);

        Debug.Log($"[SalveAbility] Healed {healAmount} HP. Current: {heroHealth.currentHealth}");
    }
}
```

---

### Step 3 — Verify `HeroHealth.Heal()` implementation

Confirm `HeroHealth.cs` (WO-70) has a working `Heal()` method:

```csharp
public void Heal(int amount)
{
    if (currentHealth <= 0) return;   // Can't heal a dead hero
    currentHealth = Mathf.Min(currentHealth + amount, maxHealth);
    VFXManager.Instance?.Play(VFXType.Impact_Heal, transform.position);
    // AudioService.Instance?.PlaySfx(SfxId.Heal);
    Debug.Log($"[HeroHealth] Healed {amount}. HP: {currentHealth}/{maxHealth}");
}
```

If this method is missing, add it.

---

### Step 4 — Wire the input

Confirm E key calls `SalveAbility.Use()`. In your input handler:

```csharp
// Input handler (InputManager.cs or HeroInputController.cs)
if (Input.GetKeyDown(KeyCode.E))
    salveAbility?.Use();
```

Or, if using the New Input System:

```csharp
// In the ability action callback
private void OnSalve(InputAction.CallbackContext ctx)
{
    if (ctx.performed)
        salveAbility?.Use();
}
```

---

### Step 5 — Prefab wiring checklist

On the Hero prefab:

| Field | Should point to |
|---|---|
| `SalveAbility.heroHealth` | `HeroHealth` component on the Hero root |
| `SalveAbility.healAmount` | 25 (or tuned value — must be > 0) |
| `SalveAbility.cooldown` | 8 |
| `AbilityCooldownUI` (on E button) | wired to `cooldownFill` Image |

---

## Files to Edit

| File | Action |
|---|---|
| `SalveAbility.cs` (or equivalent) | **Edit** — add `heroHealth.Heal(healAmount)` call |
| `HeroHealth.cs` | **Verify** — `Heal()` method exists and is not a no-op |
| Hero prefab | **Edit** — assign `heroHealth` reference, ensure `healAmount > 0` |
| Input handler | **Verify** — E key routes to `SalveAbility.Use()` |

---

## Acceptance Criteria

- [ ] Pressing E while hero HP < 100 increases `heroHealth.currentHealth` by `healAmount`
- [ ] Hero health bar visually updates immediately after pressing E
- [ ] Heal VFX (`Impact_Heal`) plays at the hero's position on use
- [ ] Ability goes on cooldown after use — button non-interactable / fill drains
- [ ] Pressing E while on cooldown does nothing (no double-heal)
- [ ] Pressing E at full HP does not overheal (clamped to `maxHealth`)
- [ ] `Debug.Log` in Console confirms heal amount and new HP value
- [ ] Animation still plays correctly (not broken by this fix)

> **AUDIT 2026-08-21 (agent fleet, read-only):** FIXED. Evidence: `HeroAbilities.cs:1226-1230` — salve heals. Status was flipped from READY by the AUDIT, not by an implementation pass. The body below is left intact; if this call is wrong, the evidence cited here is what to challenge.
