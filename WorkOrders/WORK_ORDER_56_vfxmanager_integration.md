<!-- era-sweep-2026-08-17 -->
> ### ⚠ AGED 2026-08-17 — still READY, but unverified since 2026-06-22
> The 2026-08-17 era sweep found **no evidence** that this WO's subject was deleted or superseded, so its **Status stays READY** and nothing else was changed. It is simply OLD (git first-add 2026-06-22) and has not been re-verified against current canon (`CANON_GROUND_TRUTH_*.md`, CLAUDE.md §7). **Re-verify before pulling it.**

# WORK ORDER 56 — Full VFXManager Integration (Heroes, Towers, Pets, Environment)

**Status:** READY TO IMPLEMENT
**Date:** 2026-05-28
**Priority:** High
**Scope:** Large — touches Hero, Tower, Pet, and Environment scripts
**Depends on:** WO-50 (VFXManager + VFXCatalog must be complete first)

---

## Goal

Wire `VFXManager` into every major gameplay system so all effects come from
real asset-pack prefabs. `AbilityVfxKit` becomes a thin wrapper; all call sites
stay unchanged.

---

## 1. Update `AbilityVfxKit.cs` — thin forwarding wrapper

Replace every `Play*` method body with a `VFXManager` call. Keep the old
procedural code behind `#if UNITY_EDITOR` as a fallback:

```csharp
// ── Pattern to apply to every Play* method ───────────────────────────────────
public void PlayFireImpact(Vector3 pos)
{
    if (VFXManager.Instance != null)
    {
        VFXManager.Instance.Play(VFXType.Impact_ExplosionFire, pos);
        return;
    }
#if UNITY_EDITOR
    PlayProceduralFireImpactFallback(pos);   // old procedural code stays here
#endif
}
```

Apply the same pattern to all methods. Map old method names → `VFXType`:

| Old method | VFXType |
|---|---|
| `PlayFireImpact` | `Impact_ExplosionFire` |
| `PlayArcaneImpact` | `Impact_ExplosionAether` |
| `PlayShockwave` | `Impact_ShockwaveRing` |
| `PlayHeal` | `Impact_Heal` |
| `PlayWizardCharge` | `Casting_WizardCharge` |
| `PlayFlameArrow` | `Projectile_FlameArrow` |

---

## 2. Hero integration

### Wizard
```csharp
// In WizardAbilityController.cs — on cast start:
VFXManager.Instance.Play(VFXType.Casting_WizardCharge, staffTipTransform.position);

// On projectile impact:
VFXManager.Instance.Play(VFXType.Impact_ExplosionAether, hitPosition);
```

### Ranger
```csharp
// On arrow fire:
var arrowVfx = VFXManager.Instance.Play(
    VFXType.Projectile_FlameArrow, muzzleTransform.position,
    Quaternion.LookRotation(direction));

// On arrow impact:
VFXManager.Instance.Play(VFXType.Impact_ExplosionFire, hitPosition);
```

### Knight
```csharp
// On heavy ground strike:
VFXManager.Instance.Play(VFXType.Impact_ShockwaveRing, transform.position);
```

---

## 3. Tower integration

In the base `Tower.cs` or `TowerCombat.cs` `FireProjectile()` method:

```csharp
// Muzzle flash on every shot:
VFXManager.Instance.Play(VFXType.Projectile_ArcaneBolt, muzzleTransform.position,
    Quaternion.LookRotation(targetDirection));

// Level 3+ flame tower — in the level-up check:
if (CurrentLevel >= 3 && towerType == TowerType.Flame)
    VFXManager.Instance.Play(VFXType.Impact_ExplosionFire, targetPosition);
```

---

## 4. Pet integration

In `PetBrain.cs` or wherever pet attacks are resolved:

```csharp
// Persistent aura — spawn on pet enable, parent to pet:
private GameObject _auraVfx;
private void OnEnable()
{
    _auraVfx = VFXManager.Instance.Play(
        petType == PetType.Fire ? VFXType.Pet_Aura_Fire : VFXType.Pet_Aura_Ice,
        transform.position);
    if (_auraVfx != null)
        _auraVfx.transform.SetParent(transform, worldPositionStays: false);
}

// On pet attack hit:
VFXManager.Instance.Play(VFXType.Impact_ExplosionFire, hitPosition);
```

> Aura scaling by level is handled in WO-58 (`AuraController`).

---

## 5. Environmental integration (Lana Studio)

`TorchFireController` (WO-55) drives torch fire. For any remaining environment
points (bonfire, spell circles):

```csharp
// On scene load, in an EnvironmentVFXSetup.cs bootstrap:
VFXManager.Instance.Play(VFXType.Environment_TorchFire, torchTransform.position);
```

---

## 6. Recommended prefab assignments (VFXCatalog)

Exact prefab paths depend on your imports. Browse the installed packs:

| VFXType | Pack | Search term |
|---|---|---|
| `Projectile_ArcaneBolt` | Mirza Beig / Ultimate VFX | "orb", "bolt", "magic" |
| `Projectile_FlameArrow` | Spells Pack | "fire arrow", "flame arrow" |
| `Impact_ExplosionFire` | Lana Studio Casual RPG VFX | "fire explosion", "burst" |
| `Impact_ExplosionAether` | Mirza Beig | "aether", "arcane burst" |
| `Impact_ShockwaveRing` | Mirza Beig | "shockwave", "ring" |
| `Impact_Heal` | Lana Studio | "heal", "restore" |
| `Casting_WizardCharge` | Mirza Beig | "charge up", "cast" |
| `Pet_Aura_Fire` | Lana Studio | "fire aura", "flame ring" |
| `Pet_Aura_Ice` | Lana Studio | "ice aura", "frost ring" |
| `Environment_TorchFire` | Lana Studio | "torch", "fire loop" |
| `Death_EnemyExplosion` | Spells Pack / Mirza Beig | "death", "explosion" |

---

## Files to Create / Edit

| File | Action |
|---|---|
| `Assets/_Modules/VFX/AbilityVfxKit.cs` | **Edit** — all Play* → VFXManager.Play |
| `Assets/_Modules/Village/Hero/*AbilityController.cs` | **Edit** — add VFX calls |
| `Assets/_Modules/Village/Buildings/TowerCombat.cs` | **Edit** — muzzle flash + impact |
| `Assets/_Modules/Pets/PetBrain.cs` | **Edit** — aura spawn + attack VFX |
| `Assets/Resources/VFX/VFXCatalog.asset` | **Edit** — fill all entries |

---

## Acceptance Criteria

- [ ] Wizard casting spawns a charge-up VFX at staff tip
- [ ] Flame arrow prefab trails from muzzle to target, then impact explosion
- [ ] All tower shots show a muzzle flash
- [ ] Pet auras persist and are parented to the pet (follow movement)
- [ ] `AbilityVfxKit` has zero procedural particle instantiation in non-Editor builds
- [ ] VFXCatalog has zero null entries (no LogWarning spam at runtime)
