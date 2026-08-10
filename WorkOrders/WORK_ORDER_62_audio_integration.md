# WORK ORDER 62 — Audio Integration (AbilityAudioBridge + VFX Sync)

**Status:** DONE (reconciled 2026-08-09 from the tree, NOT felt-verified — Assets/_Modules/Village/Hero/AbilityAudioBridge.cs in tree)
**Date:** 2026-05-28
**Priority:** High
**Scope:** Medium — AudioService extension + VFXManager sound flag + mobile rules
**Depends on:** WO-50 (VFXManager), WO-56 (key VFX call sites)

---

## Goal

Every major VFX has a matching, satisfying sound. Audio is triggered from the
same call site as VFX so they're always in sync. Mobile audio is lighter (pooled
sources, no reverb, shorter clips).

---

## 1. Extend `VFXManager.Play()` with optional sound

**Edit** `Assets/_Modules/VFX/VFXManager.cs`:

```csharp
// Extend Play() signature:
public GameObject Play(VFXType type, Vector3 position,
                       Quaternion rotation = default, bool playSound = true)
{
    var instance = PlayVisual(type, position, rotation);   // existing pool logic

    if (playSound)
        AudioService.Instance?.PlaySfxAtPosition(
            VfxToSfx(type), position);

    return instance;
}

// Mapping VFXType → SFX id (extend as you add sounds):
private static SfxId VfxToSfx(VFXType type) => type switch
{
    VFXType.Impact_ExplosionFire    => SfxId.FireExplosion,
    VFXType.Impact_ExplosionAether  => SfxId.ArcaneExplosion,
    VFXType.Impact_ShockwaveRing    => SfxId.Shockwave,
    VFXType.Impact_Heal             => SfxId.Heal,
    VFXType.Casting_WizardCharge    => SfxId.WizardCast,
    VFXType.Projectile_FlameArrow   => SfxId.FlameArrowLaunch,
    VFXType.Death_EnemyExplosion    => SfxId.EnemyDeath,
    VFXType.WaveClear_Celebration   => SfxId.WaveClear,
    VFXType.LevelUp_Celebration     => SfxId.LevelUp,
    VFXType.Combo_Tier1             => SfxId.ComboSmall,
    VFXType.Combo_Tier2             => SfxId.ComboBig,
    VFXType.Pet_Aura_Fire           => SfxId.PetFireAura,
    VFXType.Pet_Attack              => SfxId.PetAttack,
    _                               => SfxId.None,
};
```

Rename the existing `Play()` internals to `PlayVisual()` to keep the pool logic intact.

---

## 2. Update `AbilityAudioBridge.cs` — remove reflection

Replace the reflection block with a direct call where possible:

```csharp
// Before (reflection):
var method = AudioService.GetType().GetMethod("PlaySfx", ...);
method?.Invoke(AudioService.Instance, new object[] { clipName });

// After (direct call):
AudioService.Instance?.PlaySfx(SfxId.WizardCast);
```

Keep reflection **only** as a fallback for dynamically-named clips not yet
mapped in `SfxId`. Add a `// TODO: map to SfxId` comment on any remaining
reflection calls so they're easy to find.

---

## 3. Key audio moments

| Moment | SfxId | Notes |
|---|---|---|
| Wizard cast | `WizardCast` | Plays at staff tip |
| Flame arrow launch | `FlameArrowLaunch` | Short whoosh |
| Flame arrow impact | `FireExplosion` | Boom + crackle |
| Tower shot (every type) | `TowerShot` | Short, punchy |
| Flame tower L3+ shot | `FlameArrowLaunch` | Reuse flame whoosh |
| Pet attack | `PetAttack` | Pitched per pet type if possible |
| Enemy death | `EnemyDeath` | Quick squash sound |
| Wave clear | `WaveClear` | Victory sting |
| Level-up | `LevelUp` | Rising chime |
| Kill combo T1 | `ComboSmall` | Quick reward sting |
| Kill combo T2 | `ComboBig` | Bigger fanfare |

---

## 4. Mobile audio rules

Add to `AudioService.cs` (or a new `MobileAudioConfig`):

```csharp
private void ApplyMobilePlatformRules()
{
    if (!Application.isMobilePlatform) return;

    // Reduce master SFX volume slightly.
    AudioMixer.SetFloat("SFXVolume", -4f);   // dB

    // Pool AudioSources instead of AddComponent per call.
    // (Ensure AudioService already uses a pool — if not, implement now.)

    // Disable reverb send on mobile.
    AudioMixer.SetFloat("ReverbSend", -80f);
}
```

Call `ApplyMobilePlatformRules()` from `AudioService.Awake()`.

---

## Files to Create / Edit

| File | Action |
|---|---|
| `Assets/_Modules/VFX/VFXManager.cs` | **Edit** — add `playSound` param + `VfxToSfx` mapping |
| `Assets/_Modules/Audio/AbilityAudioBridge.cs` | **Edit** — remove reflection, direct `AudioService` calls |
| `Assets/_Modules/Audio/AudioService.cs` | **Edit** — add `PlaySfxAtPosition`, mobile rules |
| `Assets/_Modules/Audio/SfxId.cs` (enum) | **Edit/Create** — add all new SFX ids |

---

## Acceptance Criteria

- [ ] Every listed VFXType plays a corresponding sound on `Play()` by default
- [ ] Passing `playSound: false` suppresses audio without affecting VFX
- [ ] No reflection calls remain in `AbilityAudioBridge` for mapped SFX ids
- [ ] Mobile devices have no reverb and slightly lower SFX volume
- [ ] AudioSource pool prevents per-frame component creation
- [ ] No audio glitches or stutters during 10+ enemy waves on mid-range mobile
