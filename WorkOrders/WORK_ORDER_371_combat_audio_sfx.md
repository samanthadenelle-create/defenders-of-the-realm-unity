<!-- era-sweep-2026-08-17 -->
> ### ⚠ AGED 2026-08-17 — still READY, but unverified since 2026-06-22
> The 2026-08-17 era sweep found **no evidence** that this WO's subject was deleted or superseded, so its **Status stays READY** and nothing else was changed. It is simply OLD (git first-add 2026-06-22) and has not been re-verified against current canon (`CANON_GROUND_TRUTH_*.md`, CLAUDE.md §7). **Re-verify before pulling it.**

# WO-371: Combat Audio SFX — Tower Fire, Clatter, Sword Clash, Impact Sounds

**Status:** READY TO IMPLEMENT  
**Estimated Effort:** P1 (1–1.5 days — audio integration + tuning)  
**Priority:** HIGH (combat feel, audio feedback layer)  
**Lane:** 9 VFX/Audio

---

## Overview

Add snappy, responsive **sound effects** to combat events:
- Towers firing (mechanical click + whoosh)
- Enemies attacking (metal clatter, grunt)
- Sword clashes (impact, parry sounds)
- Hit feedback ("omph", pain grunt)
- General battle atmosphere

**Goal:** Combat feels alive. Every action has audio confirmation. Not immersion-breaking — short, punchy, clear.

---

## Acceptance Criteria

- [ ] Tower firing plays sound (mechanical + projectile whoosh)
- [ ] Enemy melee attack plays clatter (armor/weapon strike)
- [ ] Enemy hit grunt (take damage sound)
- [ ] Hero sword clash on parry (distinct sound)
- [ ] Hero hit feedback (impact "omph" sound)
- [ ] Enemy death sound (brief, not grotesque)
- [ ] All SFX are short (0.1–0.8 seconds, no long loops)
- [ ] Audio doesn't overlap excessively (mixing/priority works)
- [ ] Volume balanced (not too loud, not too quiet)
- [ ] Works on WebGL (no platform-specific audio)
- [ ] Can toggle SFX volume separately from music
- [ ] No audio pops/clicks on playback start/end

---

## Sound Catalog

### Tower Sounds

| Event | Sound | Duration | Tone | File Path |
|-------|-------|----------|------|-----------|
| **Tower fires** | Click (mechanism engage) + Whoosh (projectile launch) | 0.3s | Mechanical, satisfying | `SFX_TowerFire_Click.wav` + `SFX_TowerFire_Whoosh.wav` |
| **Arrow/projectile hits** | Sharp impact (thunk) | 0.2s | Crisp, direct | `SFX_ProjectileHit.wav` |
| **Tower reload** | Wind-down rattle (optional) | 0.4s | Mechanical | `SFX_TowerReload.wav` (optional) |

### Enemy Sounds

| Event | Sound | Duration | Tone | File Path |
|-------|-------|----------|------|-----------|
| **Enemy melee attack** | Metal clatter + swish | 0.3s | Sharp, aggressive | `SFX_EnemyAttack_Clatter.wav` |
| **Enemy hit** | Grunt/pain sound (short) | 0.2s | Pained but brief | `SFX_EnemyHit_Grunt.wav` |
| **Enemy death** | Final grunt (descending tone) | 0.3s | Defeated but not grotesque | `SFX_EnemyDeath.wav` |

### Hero Combat Sounds

| Event | Sound | Duration | Tone | File Path |
|-------|-------|----------|------|-----------|
| **Hero parry/block** | Sword clash (metal ring) | 0.2s | Metallic, satisfying | `SFX_Parry_Clash.wav` |
| **Hero sword swing** | Swoosh (attack animation) | 0.2s | Whoosh, air-cutting | `SFX_Swing.wav` |
| **Hero hit (take damage)** | "Omph" grunt + impact | 0.2s | Pained but not whiny | `SFX_HeroHit_Omph.wav` |
| **Hero dodge/roll** | Quick shuffle + landing | 0.3s | Movement, agility | `SFX_Dodge_Roll.wav` |

### Environment/Battle Sounds

| Event | Sound | Duration | Tone | File Path |
|-------|-------|----------|------|-----------|
| **Wave start** | Horn blast (rally cry) | 0.5s | Urgent, exciting | `SFX_WaveStart_Horn.wav` |
| **Wave victory** | Bell/chime (celebration) | 0.4s | Triumphant | `SFX_Victory_Bell.wav` |
| **Building hit** | Stone crunch or wood crack | 0.2s | Solid impact | `SFX_BuildingHit.wav` |

---

## Implementation: SfxClipLibrary Integration

### Enum Extension

Add to `Assets/_Modules/Core/Audio/SfxId.cs`:

```csharp
public enum SfxId
{
    // Existing...
    
    // Tower Attacks
    TowerFire_Click,
    TowerFire_Whoosh,
    TowerReload,
    ProjectileHit,
    
    // Enemy Combat
    EnemyAttack_Clatter,
    EnemyHit_Grunt,
    EnemyDeath,
    
    // Hero Combat
    ParryClash,
    Swing,
    HeroHit_Omph,
    DodgeRoll,
    
    // Battle Events
    WaveStart_Horn,
    Victory_Bell,
    BuildingHit,
}
```

### SfxClipLibrary Assignment

In `Assets/_Modules/Core/Audio/SfxClipLibrary.cs`:

```csharp
[SerializeField] private AudioClip[] _clips = new AudioClip[/* match enum count */];

// Set in Inspector:
// [0] TowerFire_Click → drag clip
// [1] TowerFire_Whoosh → drag clip
// [2] TowerReload → drag clip
// ... etc
```

### Call Sites (Examples)

**Tower firing (in tower attack script):**
```csharp
CoreServices.Audio.PlaySfx(SfxId.TowerFire_Click, transform.position);
CoreServices.Audio.PlaySfx(SfxId.TowerFire_Whoosh, transform.position, volumeMultiplier: 0.8f);
```

**Enemy attacking (in EnemyBrain):**
```csharp
if (_currentAction == ActionType.Attack)
{
    CoreServices.Audio.PlaySfx(SfxId.EnemyAttack_Clatter, transform.position);
}
```

**Hero parry (in HeroHealth or BattleController):**
```csharp
public void OnParry()
{
    CoreServices.Audio.PlaySfx(SfxId.ParryClash, transform.position);
    // ... other parry logic
}
```

**Enemy hit (in IDamageableStructure implementation):**
```csharp
public void TakeDamage(int amount)
{
    CoreServices.Audio.PlaySfx(SfxId.EnemyHit_Grunt, transform.position);
    _health -= amount;
    // ... rest of damage logic
}
```

---

## Audio Design Principles

### Keep It Short
- Tower fire: 0.3s (not 2s loops)
- Enemy grunt: 0.2s (snappy feedback)
- Parry: 0.2s (instant impact)
- **Why:** Combat is fast. Long SFX feel sluggish.

### Balanced Volume
- Tower fire: Loud (announces action)
- Enemy attack: Medium (visible threat)
- Hero hit: Medium (feedback without whining)
- Ambient: Quiet (background, not intrusive)

### Clarity
- Sword clash ≠ Sword swing (distinct sounds)
- Enemy attack ≠ Enemy hit (tell them apart)
- Tower fire = mechanical feeling (satisfying)
- Victory bell = celebratory (uplifting)

### Avoid Repetition Fatigue
- If a sound plays frequently (parry, swing), keep it short + slightly varied
- Randomize pitch (±10%) on repeated sounds
- Don't loop SFX (one-shot only)

---

## Files to Modify/Create

### Core Audio (Minimal Changes)

**`Assets/_Modules/Core/Audio/SfxId.cs`**
- Add enum values (listed above)

**`Assets/_Modules/Core/Audio/SfxClipLibrary.cs`**
- Add clip array entries
- Assign in Inspector

### Integration Points (Add SFX Calls)

**`Assets/_Modules/Village/Towers/TowerController.cs`** (or equivalent)
```csharp
// On fire:
CoreServices.Audio.PlaySfx(SfxId.TowerFire_Click, _firePoint.position);
CoreServices.Audio.PlaySfx(SfxId.TowerFire_Whoosh, _firePoint.position);
```

**`Assets/_Modules/Village/Enemy/EnemyBrain.cs`** (or equivalent)
```csharp
// On attack:
CoreServices.Audio.PlaySfx(SfxId.EnemyAttack_Clatter, transform.position);

// On hit:
CoreServices.Audio.PlaySfx(SfxId.EnemyHit_Grunt, transform.position);

// On death:
CoreServices.Audio.PlaySfx(SfxId.EnemyDeath, transform.position);
```

**`Assets/_Modules/BattleATB/BattleController.cs`** (or hero combat)
```csharp
// On hero parry:
CoreServices.Audio.PlaySfx(SfxId.ParryClash, _heroTransform.position);

// On hero swing:
CoreServices.Audio.PlaySfx(SfxId.Swing, _heroTransform.position);

// On hero hit:
CoreServices.Audio.PlaySfx(SfxId.HeroHit_Omph, _heroTransform.position);
```

**`Assets/_Modules/Village/Waves/WaveManager.cs`**
```csharp
// On wave start:
CoreServices.Audio.PlaySfx(SfxId.WaveStart_Horn, _waveOrigin.position);

// On wave victory:
CoreServices.Audio.PlaySfx(SfxId.Victory_Bell, _village.position);
```

---

## Audio Asset Requirements

**Need to provide (or source):**

| Sound | Type | Characteristics | Source Options |
|-------|------|---|---|
| Tower click | SFX | Mechanical, 0.1s | Freesound, Zapsplat, or record |
| Tower whoosh | SFX | Projectile launch, 0.2s | Freesound, Zapsplat |
| Enemy clatter | SFX | Metal armor/weapon, 0.3s | Game SFX library, Freesound |
| Enemy grunt | SFX | Brief pain/impact, 0.2s | Game SFX library |
| Enemy death | SFX | Defeated, descending, 0.3s | Game SFX library |
| Sword clash | SFX | Metal on metal ring, 0.2s | Game SFX library |
| Sword swing | SFX | Whoosh (air cutting), 0.2s | Game SFX library |
| Hero hit | SFX | "Omph" grunt, 0.2s | Game SFX library |
| Dodge roll | SFX | Shuffle + landing, 0.3s | Game SFX library |
| Wave horn | SFX | Rally cry, 0.5s | Game SFX library |
| Victory bell | SFX | Celebratory, 0.4s | Game SFX library |
| Building hit | SFX | Stone/wood impact, 0.2s | Game SFX library |

**Free libraries to source from:**
- Freesound.org (CC licensed)
- Zapsplat.com (royalty-free)
- Pixabay.com (CC)
- Game Dev SFX packs (itch.io)

---

## Volume/Mixing Recommendations

**Master SFX volume:** 0.7 (loud enough to hear, not overwhelming)
**Distance attenuation:** Default (3D spatial audio)

| Sound | Volume (0–1) | Distance | Notes |
|-------|---|---|---|
| Tower fire | 0.8 | 50m radius | Loud, announces action |
| Enemy attack | 0.6 | 20m radius | Medium, clear but not overwhelming |
| Sword clash | 0.7 | 15m radius | Satisfying impact |
| Hero hit | 0.6 | Hero-centric | Feedback for player |
| Victory bell | 0.8 | Village-wide | Celebrates victory |
| Enemy death | 0.5 | 10m radius | Brief, not grotesque |

---

## Testing Checklist

- [ ] Tower fires: Click + Whoosh play in sequence (not overlapping)
- [ ] Enemy attack: Clatter plays when enemy swings
- [ ] Enemy hit: Grunt plays on damage taken
- [ ] Hero parry: Clash plays distinctly
- [ ] Hero hit: "Omph" feedback on damage
- [ ] Enemy death: Brief sound on kill
- [ ] Wave start: Horn announces wave (not too loud)
- [ ] Victory: Bell plays on wave victory
- [ ] No audio pops/clicks at start/end
- [ ] Volume balanced (not too quiet in WebGL)
- [ ] Spatial audio works (closer = louder)
- [ ] Can toggle SFX volume in settings
- [ ] No audio lag (plays immediately on event)
- [ ] Works in WebGL build

---

## Parallel with WO-359

**WO-359 (Combat Feedback):** Screen shake, parry slowmo, impact VFX  
**WO-371 (Combat Audio):** Sound effects for same events

These can run **in parallel** — audio and VFX teams don't block each other.

---

## Optional Enhancements

- [ ] Randomize pitch ±10% on repeated sounds (avoid repetition fatigue)
- [ ] Layered sounds (tower fire = click + whoosh + impact)
- [ ] Audio stingers on critical events (crit hit, special ability)
- [ ] Enemy voice variety (different grunts per enemy type)
- [ ] Ambient battle sounds (crowd, wind, distant warfare)
- [ ] Sword/weapon variety (blunt weapon ≠ sword clash)

---

## No Changes Required

- Music layer (separate from SFX)
- AudioService (use existing)
- CoreServices (use existing)
- Battle logic (audio is cosmetic)

---

## Acceptance Sign-Off

- [ ] All combat events have audio feedback
- [ ] Sounds are short, snappy, responsive
- [ ] Volume balanced and not fatiguing
- [ ] No audio pops or glitches
- [ ] Enhances combat feel significantly
- [ ] Ready to integrate with WO-359 VFX layer
- [ ] Works in WebGL build

---

## Notes

**DependsOn:** Audio infrastructure (CoreServices.Audio, SfxClipLibrary already in place)

**Unblocks:** Combat feels alive and responsive

**Parallel:** WO-359 (VFX) — both audio and visual feedback run simultaneously
