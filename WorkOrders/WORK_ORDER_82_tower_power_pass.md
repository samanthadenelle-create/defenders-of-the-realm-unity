# WORK ORDER 82 — Tower Power & Satisfaction Pass (Phase 2)

**Status:** READY TO IMPLEMENT
**Date:** 2026-05-28
**Priority:** High
**Scope:** Medium — one new script + edits to tower scripts + prefab wiring
**Depends on:** WO-50 (VFXManager), WO-56 (VFXManager integration), WO-61 (CameraShakeManager)

---

## Goal

Make every tower shot feel like it matters. Strong muzzle flash, trail + impact
VFX, screen feedback on powerful shots. Upgrades are a big moment — scale burst,
glow, and floating level text. Even basic towers should feel satisfying from
round 1.

---

## 1. `TowerVFXController.cs`

**Path:** `Assets/_Modules/Village/Buildings/TowerVFXController.cs`

Add this to every tower prefab. Call `OnShoot()` when the tower fires,
`OnUpgrade(newLevel)` when the tower levels up.

```csharp
using UnityEngine;
using System.Collections;
using TMPro;

public class TowerVFXController : MonoBehaviour
{
    [Header("Shot Feedback")]
    public Transform  muzzlePoint;              // Empty GO at the barrel tip
    public VFXType    muzzleFlashVFX     = VFXType.Projectile_ArcaneBolt;
    public VFXType    impactVFX          = VFXType.Impact_ExplosionFire;
    public ShakeTier  shotShakeTier      = ShakeTier.Light;
    public bool       shakeOnEveryShot   = false;   // Only shake on heavy/Level3+ shots

    [Header("Upgrade Feedback")]
    public VFXType    upgradeVFX         = VFXType.LevelUp_Celebration;
    public float      upgradePunchScale  = 0.22f;   // Extra scale added on upgrade burst
    public float      upgradePunchTime   = 0.15f;
    public GameObject upgradeGlowObject;            // Extra glow mesh — enable at Level 2+
    public TMP_Text   levelFloatingText;            // Assign a world-space TMP on the tower

    [Header("Level Glow Materials")]
    public Renderer   towerRenderer;
    public Material   level1Material;
    public Material   level2Material;
    public Material   level3Material;

    [Header("Mobile")]
    public float mobileShakeScale = 0.55f;

    private Vector3 _baseScale;

    private void Awake()
    {
        _baseScale = transform.localScale;
    }

    // ── Called by TowerCombat when a shot fires ────────────────────────────────

    public void OnShoot(Vector3 targetPosition, int towerLevel)
    {
        // Muzzle flash at barrel
        if (muzzlePoint != null)
            VFXManager.Instance?.Play(muzzleFlashVFX, muzzlePoint.position);

        // Screen shake only on Level 3+ or if flagged
        if (shakeOnEveryShot || towerLevel >= 3)
        {
            float scale = 1f;
#if UNITY_ANDROID || UNITY_IOS
            scale = mobileShakeScale;
#endif
            // ShakeTier doesn't scale, so we only call on qualifying shots
            CameraShakeManager.Instance?.Shake(shotShakeTier);
        }
    }

    /// <summary>Call this from the projectile's OnImpact() method.</summary>
    public void OnProjectileImpact(Vector3 worldPos)
    {
        VFXManager.Instance?.Play(impactVFX, worldPos);
    }

    // ── Called by TowerUpgradeManager when the tower levels up ────────────────

    public void OnUpgrade(int newLevel)
    {
        StartCoroutine(UpgradeBurst(newLevel));
    }

    private IEnumerator UpgradeBurst(int newLevel)
    {
        // VFX burst
        VFXManager.Instance?.Play(upgradeVFX, transform.position + Vector3.up * 0.5f);
        CameraShakeManager.Instance?.Shake(ShakeTier.Light);

        // Scale punch
        transform.localScale = _baseScale * (1f + upgradePunchScale);
        yield return new WaitForSeconds(upgradePunchTime);

        // Bounce back with overshoot
        float elapsed = 0f;
        float settle  = 0.25f;
        while (elapsed < settle)
        {
            elapsed += Time.deltaTime;
            transform.localScale = Vector3.Lerp(
                _baseScale * (1f + upgradePunchScale), _baseScale,
                Mathf.SmoothStep(0f, 1f, elapsed / settle));
            yield return null;
        }
        transform.localScale = _baseScale;

        // Material upgrade
        if (towerRenderer != null)
        {
            towerRenderer.material = newLevel switch
            {
                1 => level1Material,
                2 => level2Material,
                _ => level3Material
            };
        }

        // Enable glow mesh at Level 2+
        if (upgradeGlowObject != null)
            upgradeGlowObject.SetActive(newLevel >= 2);

        // Floating level text
        if (levelFloatingText != null)
            StartCoroutine(ShowFloatingLevelText(newLevel));
    }

    private IEnumerator ShowFloatingLevelText(int level)
    {
        levelFloatingText.text = $"Level {level}!";
        levelFloatingText.gameObject.SetActive(true);

        float elapsed = 0f, duration = 1.4f;
        Vector3 startPos = levelFloatingText.transform.localPosition;
        Vector3 endPos   = startPos + Vector3.up * 1.2f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            levelFloatingText.transform.localPosition = Vector3.Lerp(startPos, endPos, t);
            levelFloatingText.alpha = Mathf.Lerp(1f, 0f, Mathf.Pow(t, 2f));
            yield return null;
        }

        levelFloatingText.gameObject.SetActive(false);
        levelFloatingText.transform.localPosition = startPos;
    }
}
```

---

## 2. Wire into `TowerCombat.cs`

In the tower's shoot method, add:

```csharp
private void Shoot(Transform target, int towerLevel)
{
    _animator?.SetTrigger("Shoot");

    // Spawn projectile
    var proj = Instantiate(projectilePrefab, muzzlePoint.position, Quaternion.identity);
    proj.GetComponent<TowerProjectile>()?.Init(target, towerDamage, this);

    // VFX
    GetComponent<TowerVFXController>()?.OnShoot(target.position, towerLevel);

    // AudioService.Instance?.PlaySfx(SfxId.TowerShoot);
}
```

---

## 3. `TowerProjectile.cs` — impact callback

On hit:

```csharp
private void OnTriggerEnter(Collider other)
{
    if (other.TryGetComponent<EnemyHealth>(out var health))
    {
        health.TakeDamage(_damage);
        _sourceTower?.GetComponent<TowerVFXController>()
            ?.OnProjectileImpact(transform.position);
        Destroy(gameObject);
    }
}
```

---

## 4. Tower VFX per type — recommended mapping

| Tower type | muzzleFlashVFX | impactVFX | shotShakeTier |
|---|---|---|---|
| Basic Ballista | `Projectile_ArcaneBolt` | `Impact_Physical` | Light (Level 3+ only) |
| Fire Tower | `Impact_ExplosionFire` | `Impact_ExplosionFire` | Light (always) |
| Ice Tower | `Projectile_ArcaneBolt` | `Impact_Physical` | None |
| Lightning Tower | `Impact_ExplosionFire` | `Impact_ExplosionFire` | Medium |
| Boss Cannon | `Impact_ExplosionFire` | `Impact_ExplosionFire` | Medium (always) |

---

## Files to Create / Edit

| File | Action |
|---|---|
| `Assets/_Modules/Village/Buildings/TowerVFXController.cs` | **Create** |
| `Assets/_Modules/Village/Buildings/TowerCombat.cs` | **Edit** — call `OnShoot()` |
| `Assets/_Modules/Village/Buildings/TowerProjectile.cs` | **Edit** — call `OnProjectileImpact()` |
| `Assets/_Modules/Village/Buildings/TowerUpgradeManager.cs` | **Edit** — call `OnUpgrade(newLevel)` |
| All tower prefabs | **Edit** — add `TowerVFXController`, assign muzzle point, materials |

---

## Acceptance Criteria

- [ ] Every tower shot spawns muzzle flash VFX at the barrel tip
- [ ] Projectile impact plays the correct VFX for that tower type
- [ ] Level 3+ towers or flagged towers trigger camera shake on every shot
- [ ] Upgrading a tower plays burst VFX, scale punch, material change
- [ ] "Level 2!" / "Level 3!" floats up and fades after upgrade
- [ ] Upgrade glow mesh appears at Level 2+
- [ ] No performance regression — 60 FPS during a 10-enemy wave with 4 towers firing
