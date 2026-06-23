# WORK ORDER 81 — Hero Combat Feel Overhaul (Phase 1 — Priority #1)

**Status:** READY TO IMPLEMENT
**Date:** 2026-05-28
**Priority:** Critical
**Scope:** Large — four scripts + HeroHealth edit + prefab wiring
**Depends on:** WO-70 (HeroHealth), WO-61 (CameraShakeManager, HitStopManager), WO-56 (VFXManager)

---

## Goal

Make every hero action feel weighty, responsive, and satisfying. No floaty
movement, no mushy combat. Every attack has wind-up → impact → follow-through.
Every hit the hero takes has a clear "I felt that" reaction.

---

## 1. Update `HeroLocomotion.cs` — momentum + grounded feel

**Path:** `Assets/_Modules/Village/Hero/HeroLocomotion.cs`

Replace the raw velocity assignment with an acceleration model so the hero
decelerates naturally instead of stopping on a frame.

```csharp
using UnityEngine;

[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(Animator))]
public class HeroLocomotion : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed          = 5.5f;
    public float acceleration       = 18f;    // Higher = snappier start
    public float deceleration       = 22f;    // Higher = tighter stop
    public float rotationSpeed      = 720f;   // Degrees per second

    [Header("Ground Feel")]
    public float gravity            = -18f;
    public Transform dustSpawnPoint;          // Assign to feet bone

    // ── Internal ───────────────────────────────────────────────────────────────
    private CharacterController _cc;
    private Animator            _animator;
    private Vector3             _velocity;    // XZ only — we handle Y separately
    private float               _verticalVel;
    private bool                _isMoving;

    // Input buffer — set externally by input handler or touch system
    [HideInInspector] public Vector2 inputDir;

    private void Awake()
    {
        _cc       = GetComponent<CharacterController>();
        _animator = GetComponent<Animator>();
    }

    private void Update()
    {
        Move();
        ApplyGravity();
        UpdateAnimator();
        SpawnFootstepDust();
    }

    private void Move()
    {
        var desiredVelocity = new Vector3(inputDir.x, 0f, inputDir.y) * moveSpeed;
        float rate = desiredVelocity.magnitude > 0.01f ? acceleration : deceleration;
        _velocity = Vector3.MoveTowards(_velocity, desiredVelocity, rate * Time.deltaTime);

        // Rotate toward movement direction
        if (_velocity.sqrMagnitude > 0.01f)
        {
            var targetRot = Quaternion.LookRotation(_velocity);
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation, targetRot, rotationSpeed * Time.deltaTime);
        }

        _cc.Move((_velocity + Vector3.up * _verticalVel) * Time.deltaTime);
        _isMoving = _velocity.magnitude > 0.3f;
    }

    private void ApplyGravity()
    {
        if (_cc.isGrounded && _verticalVel < 0f)
            _verticalVel = -2f;
        else
            _verticalVel += gravity * Time.deltaTime;
    }

    private void UpdateAnimator()
    {
        float speed = _velocity.magnitude / moveSpeed;   // 0–1 normalised
        _animator.SetFloat("Speed", speed, 0.08f, Time.deltaTime);
        _animator.SetBool("IsMoving", _isMoving);
    }

    private void SpawnFootstepDust()
    {
        // Delegate to FootstepDustController (WO-61) if present.
        // FootstepDustController reads IsMoving directly from Animator.
    }
}
```

---

## 2. `HeroCombatController.cs` — attacks, input buffer, ability cooldowns

**Path:** `Assets/_Modules/Village/Hero/HeroCombatController.cs`

```csharp
using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(HeroLocomotion))]
public class HeroCombatController : MonoBehaviour
{
    [Header("Basic Attack")]
    public int   attackDamage       = 15;
    public float attackRange        = 2.2f;
    public float attackCooldown     = 0.65f;
    public float attackWindupTime   = 0.12f;    // Seconds before damage is dealt

    [Header("Input Buffer")]
    public float inputBufferWindow  = 0.25f;    // Seconds — queue an attack slightly early

    [Header("Feedback")]
    public float heavyAttackShake   = 0.18f;    // CameraShakeManager intensity
    public float heavyHitStopTime   = 0.06f;    // HitStopManager duration
    public float lightAttackShake   = 0.08f;
    public float lightHitStopTime   = 0.03f;

    [Header("Mobile")]
    [Tooltip("Reduce shake on mobile to protect framerate.")]
    public float mobileShakeScale   = 0.55f;

    // ── State ─────────────────────────────────────────────────────────────────
    private Animator   _animator;
    private float      _nextAttackTime;
    private bool       _attackQueued;
    private float      _queueExpiry;
    private bool       _isAttacking;

    private static readonly int _attackTrigger = Animator.StringToHash("Attack");

    private void Awake()
    {
        _animator = GetComponent<Animator>();
    }

    private void Update()
    {
        // Drain the input buffer
        if (_attackQueued && Time.time > _queueExpiry)
            _attackQueued = false;

        if (_attackQueued && !_isAttacking && Time.time >= _nextAttackTime)
        {
            _attackQueued = false;
            StartCoroutine(PerformAttack());
        }
    }

    // ── Called by input handler / UI button ───────────────────────────────────

    public void RequestAttack()
    {
        if (!_isAttacking && Time.time >= _nextAttackTime)
            StartCoroutine(PerformAttack());
        else
        {
            // Buffer the request
            _attackQueued = true;
            _queueExpiry  = Time.time + inputBufferWindow;
        }
    }

    // ── Attack coroutine ──────────────────────────────────────────────────────

    private IEnumerator PerformAttack()
    {
        _isAttacking    = true;
        _nextAttackTime = Time.time + attackCooldown;

        _animator.SetTrigger(_attackTrigger);

        // Wind-up: hero is committed but damage hasn't landed yet
        yield return new WaitForSeconds(attackWindupTime);

        // Deal damage
        var hits = Physics.OverlapSphere(
            transform.position + transform.forward * attackRange * 0.6f,
            attackRange * 0.55f,
            LayerMask.GetMask("Enemy"));

        bool hitAnything = false;

        foreach (var hit in hits)
        {
            if (hit.TryGetComponent<EnemyHealth>(out var health))
            {
                health.TakeDamage(attackDamage);
                hitAnything = true;
            }
        }

        // Feedback on impact
        if (hitAnything)
        {
            float shakeIntensity = lightAttackShake;
            float hitStopDuration = lightHitStopTime;

#if UNITY_ANDROID || UNITY_IOS
            shakeIntensity *= mobileShakeScale;
#endif

            CameraShakeManager.Instance?.Shake(ShakeTier.Light);
            HitStopManager.Instance?.TriggerHitStop(hitStopDuration);

            VFXManager.Instance?.Play(VFXType.Impact_Physical,
                transform.position + transform.forward * attackRange * 0.6f + Vector3.up * 0.8f);
        }

        // Follow-through — wait for animation recovery
        yield return new WaitForSeconds(attackCooldown - attackWindupTime - 0.05f);
        _isAttacking = false;
    }

    // ── Ability callbacks (called from WizardAbilityController, etc.) ─────────

    /// <summary>Call this after any heavy ability lands — heavy feedback burst.</summary>
    public void OnHeavyAbilityImpact(Vector3 impactPoint)
    {
        float shake = heavyAttackShake;
#if UNITY_ANDROID || UNITY_IOS
        shake *= mobileShakeScale;
#endif
        CameraShakeManager.Instance?.Shake(ShakeTier.Medium);
        HitStopManager.Instance?.TriggerHitStop(heavyHitStopTime);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(
            transform.position + transform.forward * attackRange * 0.6f,
            attackRange * 0.55f);
    }
}
```

---

## 3. `HeroHitReaction.cs` — damage feedback (extends WO-70 HeroHealth)

**Path:** `Assets/_Modules/Village/Hero/HeroHitReaction.cs`

Add this component alongside `HeroHealth` on the hero prefab. Wire
`HeroHealth.onTakeDamage → HeroHitReaction.OnHit()`.

```csharp
using UnityEngine;
using System.Collections;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class HeroHitReaction : MonoBehaviour
{
    [Header("Screen Vignette / Flash")]
    public Volume postProcessVolume;            // Assign Global Volume in scene
    public float  flashDuration    = 0.18f;
    public float  vignetteStrength = 0.55f;     // Peak vignette intensity on hit

    [Header("Shake")]
    public float hitShakeIntensity = 0.22f;

    [Header("Death")]
    public float deathSlowMoDuration = 1.2f;    // Real seconds
    public float deathTimeScale      = 0.22f;   // How slow on death

    [Header("Mobile")]
    public float mobileShakeScale = 0.55f;

    private Vignette _vignette;
    private bool     _vignetteAvailable;

    private void Awake()
    {
        if (postProcessVolume != null &&
            postProcessVolume.profile.TryGet(out Vignette v))
        {
            _vignette          = v;
            _vignetteAvailable = true;
        }

        // Wire to HeroHealth
        if (TryGetComponent<HeroHealth>(out var health))
        {
            health.onTakeDamage.AddListener(OnHit);
            health.onDeath.AddListener(OnDeath);
        }
    }

    // ── Hit ───────────────────────────────────────────────────────────────────

    public void OnHit()
    {
        float shake = hitShakeIntensity;
#if UNITY_ANDROID || UNITY_IOS
        shake *= mobileShakeScale;
#endif
        CameraShakeManager.Instance?.Shake(ShakeTier.Light);
        StartCoroutine(VignetteFlash());
        // AudioService.Instance?.PlaySfx(SfxId.HeroHit);
    }

    private IEnumerator VignetteFlash()
    {
        if (!_vignetteAvailable) yield break;

        float elapsed = 0f;
        while (elapsed < flashDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / flashDuration;
            // Spike in, fade out
            float intensity = Mathf.Lerp(vignetteStrength, 0f, t * t);
            _vignette.intensity.Override(intensity);
            yield return null;
        }
        _vignette.intensity.Override(0f);
    }

    // ── Death ─────────────────────────────────────────────────────────────────

    public void OnDeath()
    {
        CameraShakeManager.Instance?.Shake(ShakeTier.Heavy);
        StartCoroutine(DeathSlowMo());
        // AudioService.Instance?.PlaySfx(SfxId.HeroDeath);
    }

    private IEnumerator DeathSlowMo()
    {
        Time.timeScale = deathTimeScale;
        float elapsed  = 0f;

        while (elapsed < deathSlowMoDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            // Gradually restore time scale
            Time.timeScale = Mathf.Lerp(deathTimeScale, 1f, elapsed / deathSlowMoDuration);
            yield return null;
        }

        Time.timeScale = 1f;
        // TODO: Trigger game-over screen
    }
}
```

---

## 4. `AbilityCooldownUI.cs` — visual cooldown on ability buttons

**Path:** `Assets/_Modules/Village/Hero/AbilityCooldownUI.cs`

```csharp
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Attach to each ability button. Call StartCooldown(seconds) when the ability fires.
/// The button's Image fill drains from full back to empty as cooldown expires.
/// </summary>
public class AbilityCooldownUI : MonoBehaviour
{
    [Header("References")]
    public Image    cooldownFill;   // Radial fill image over the button icon
    public TMP_Text cooldownText;   // Optional — shows remaining seconds
    public Button   button;

    private float _totalCooldown;
    private float _remaining;
    private bool  _onCooldown;

    private void Update()
    {
        if (!_onCooldown) return;

        _remaining -= Time.deltaTime;

        if (_remaining <= 0f)
        {
            _remaining = 0f;
            _onCooldown = false;
            if (button != null) button.interactable = true;
            if (cooldownFill != null) cooldownFill.fillAmount = 0f;
            if (cooldownText != null) cooldownText.gameObject.SetActive(false);
            return;
        }

        float t = _remaining / _totalCooldown;
        if (cooldownFill != null) cooldownFill.fillAmount = t;
        if (cooldownText != null) cooldownText.text = Mathf.CeilToInt(_remaining).ToString();
    }

    public void StartCooldown(float seconds)
    {
        _totalCooldown = seconds;
        _remaining     = seconds;
        _onCooldown    = true;

        if (button != null) button.interactable = false;
        if (cooldownText != null) cooldownText.gameObject.SetActive(true);
    }
}
```

**Usage:** In each ability script, after firing:

```csharp
GetComponent<AbilityCooldownUI>()?.StartCooldown(abilityCooldown);
```

---

## 5. Ability Wind-up + Impact Pattern

Apply this pattern to **every** hero ability (Wizard, Ranger, Knight):

```csharp
private IEnumerator FireAbility()
{
    // 1. Wind-up: play anticipation animation + subtle VFX
    _animator.SetTrigger("AbilityWindup");
    VFXManager.Instance?.Play(VFXType.Ability_Windup, transform.position);
    // AudioService.Instance?.PlaySfx(SfxId.AbilityChargeUp);

    yield return new WaitForSeconds(windupDuration);    // 0.15–0.25s depending on ability

    // 2. Release: fire projectile / deal damage
    _animator.SetTrigger("AbilityRelease");
    // ... spawn projectile or apply damage here ...

    // 3. Impact: when projectile hits or AoE lands
    VFXManager.Instance?.Play(VFXType.Impact_ExplosionFire, impactPoint);
    GetComponent<HeroCombatController>()?.OnHeavyAbilityImpact(impactPoint);
    // AudioService.Instance?.PlaySfx(SfxId.AbilityImpact);

    // 4. Cooldown UI
    GetComponentInChildren<AbilityCooldownUI>()?.StartCooldown(abilityCooldown);
}
```

---

## 6. Mobile-Specific Tuning Checklist

| Setting | Mobile Value | Desktop Value |
|---|---|---|
| Camera shake intensity | × 0.55 | × 1.0 |
| Hit-stop duration | 0.03 s | 0.06 s |
| Vignette intensity | 0.35 | 0.55 |
| Attack input buffer window | 0.3 s (larger for touch) | 0.25 s |
| Touch area for attack button | Min 80 × 80 dp | n/a |

In all scripts, guard mobile scaling with:

```csharp
#if UNITY_ANDROID || UNITY_IOS
    value *= mobileScale;
#endif
```

---

## Files to Create / Edit

| File | Action |
|---|---|
| `Assets/_Modules/Village/Hero/HeroLocomotion.cs` | **Edit** — replace velocity with acceleration model |
| `Assets/_Modules/Village/Hero/HeroCombatController.cs` | **Create** |
| `Assets/_Modules/Village/Hero/HeroHitReaction.cs` | **Create** |
| `Assets/_Modules/Village/Hero/AbilityCooldownUI.cs` | **Create** |
| `Assets/_Modules/Village/Hero/HeroHealth.cs` | **Edit** — ensure `onTakeDamage` and `onDeath` UnityEvents are wired |
| Wizard/Ranger/Knight ability scripts | **Edit** — add wind-up → impact pattern + `AbilityCooldownUI` call |
| Hero prefab | **Edit** — add `HeroCombatController`, `HeroHitReaction`, `AbilityCooldownUI` |
| Global Volume in scene | **Edit** — ensure Vignette override is present |

---

## Acceptance Criteria

- [ ] Hero decelerates smoothly to a stop — no instant snap or slide
- [ ] Pressing attack 0.25 s before cooldown expires still fires (input buffer)
- [ ] Every basic attack: plays `Attack` trigger → OverlapSphere hits enemies → light shake + hit-stop
- [ ] Every heavy ability: wind-up VFX → release → impact shake (Medium tier) + hit-stop
- [ ] `AbilityCooldownUI` radial fill drains correctly; button is non-interactable during cooldown
- [ ] Hero taking damage: vignette flashes red, camera shakes (Light)
- [ ] Hero death: time slows to 0.22×, recovers over 1.2 s, then game-over trigger fires
- [ ] All shake values are scaled by `mobileShakeScale` on Android/iOS
- [ ] 60 FPS maintained during a 10-enemy wave with full VFX active
