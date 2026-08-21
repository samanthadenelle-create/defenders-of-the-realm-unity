<!-- era-sweep-2026-08-17 -->
> ### ⚠ AGED 2026-08-17 — still READY, but unverified since 2026-06-22
> The 2026-08-17 era sweep found **no evidence** that this WO's subject was deleted or superseded, so its **Status stays READY** and nothing else was changed. It is simply OLD (git first-add 2026-06-22) and has not been re-verified against current canon (`CANON_GROUND_TRUTH_*.md`, CLAUDE.md §7). **Re-verify before pulling it.**

# WO-370: Arena Monument VFX Aura — Magical Spell Effects & Glow

**Status:** DONE — audit-verified as shipped (2026-08-21 backlog audit).
**Estimated Effort:** P1 (1–1.5 days — particles + shaders + tweaking)  
**Priority:** HIGH (visual polish, makes monument iconic)  
**Lane:** 9 VFX/Audio

---

## Overview

Add magical **aura and spell effects** to the Arena Monument (WO-369) to make it visually stunning and otherworldly.

The monument should glow, pulse, and emanate magical energy — making it feel like the true holy grail of endgame power.

---

## Acceptance Criteria

- [ ] Statue has glowing aura (soft magical light)
- [ ] Floating particles around statue (not distracting, ethereal)
- [ ] Torches have enhanced glow/light (magical rather than mundane)
- [ ] Siege trophies show conquest aura (darker, more ominous)
- [ ] Ground has subtle light/glow pattern (sanctified ground)
- [ ] Periodic spell cast effect (aura pulses/flares 5-10s cycle)
- [ ] Magical shimmer/distortion effect (optional, if performance allows)
- [ ] All effects loop seamlessly (no stuttering)
- [ ] Performance: No FPS hit on standard hardware (WebGL target)
- [ ] Visible from distance (aura glows from far away)
- [ ] Doesn't interfere with gameplay (non-intrusive, atmospheric)

---

## Design: Layered Aura System

### Layer 1: Statue Aura (Gold/Holy Light)
- **Type:** Glow + particle system
- **Color:** Gold/amber with subtle white glow
- **Effect:** Soft radial glow around `Statue_Knight`
- **Particles:** Gentle floating sparkles rising from statue base
- **Material:** Additive particles (blend with environment)
- **Frequency:** Continuous, slow rise (1-2 m/s)
- **Density:** ~5-8 particles visible at once

**Shader/Material:**
```
Stat Knight → Add material with:
  - Self-illumination (gold tint)
  - Glow map (brighter on face/sword)
  - Rim light (edges glow brighter)
```

### Layer 2: Magical Torches (Ethereal Flame)
- **Type:** Particle system overlay
- **Current:** Standard torches from WO-369
- **Addition:** Magical fire particles (blue/purple wisps rising from torches)
- **Effect:** Makes torches feel enchanted, not mundane
- **Blend:** Mix with original torch visuals (additive)

### Layer 3: Trophy Aura (Ominous/Captured)
- **Type:** Dark purple/blue glow
- **Applies to:** Catapult, Ballista, Stakes
- **Effect:** Conquered trophies glow with dark energy (captured power)
- **Particles:** Occasional dark wisps circling weapons
- **Vibe:** More sinister than holy (enemies defeated)

### Layer 4: Ground Sanctification (Sacred Circle)
- **Type:** Light glow pattern on ground
- **Location:** Circular area under monument
- **Effect:** Subtle light radiating outward from statue base
- **Particles:** Optional - gentle dust motes rising from circle edge
- **Fade:** Soft edge (no hard boundary)

### Layer 5: Spell Cast Pulse (5-10s Cycle)
- **Type:** Periodic bright flare/pulse
- **Frequency:** Every 5-10 seconds
- **Duration:** 0.5-1s bright flash
- **Effect:** Monument briefly brightens (casting spell? channeling power?)
- **Audio:** Optional - magical "hum" or chime sound on pulse
- **Impact:** Makes monument feel alive and powerful

---

## Particle Systems Specification

### `ParticleSystem_StatueAura`
| Parameter | Value | Notes |
|-----------|-------|-------|
| Duration | Looping | Continuous |
| Emission Rate | 2-3 particles/sec | Light, ethereal |
| Lifetime | 3-5 seconds | Slow drift up |
| Start Speed | 0.5-1.0 m/s | Gentle rise |
| Start Size | 0.2-0.4m | Visible but not huge |
| Start Color | Gold (255, 200, 0, 150) | Additive blend |
| Gravity | -0.1 m/s² | Slight down drift |
| Damping | 0.2 | Slow air resistance |
| Renderer | Additive material | Glows on top of scene |

**Texture:** Soft sparkle/orb (provided or search for free particle texture)

### `ParticleSystem_TorchMagic`
| Parameter | Value | Notes |
|-----------|-------|-------|
| Duration | Looping | On each torch |
| Emission Rate | 1-2 particles/sec | Sparse wisps |
| Lifetime | 2-3 seconds | Short rise |
| Start Speed | 1-2 m/s | Fast rise |
| Start Size | 0.3-0.5m | Wisp-like |
| Start Color | Blue/Purple (150, 100, 255, 120) | Magical fire |
| Gravity | -0.3 m/s² | Some rise, some fall |
| Renderer | Additive material | Blends with torch |

**Texture:** Wispy flame (same as statue, or blue variant)

### `ParticleSystem_TrophyAura`
| Parameter | Value | Notes |
|-----------|-------|-------|
| Duration | Looping | Around trophies |
| Emission Rate | 1 particle/sec | Sparse dark wisps |
| Lifetime | 4-6 seconds | Longer dwell |
| Start Speed | 0.2-0.5 m/s | Slow circulation |
| Start Size | 0.2-0.3m | Subtle |
| Start Color | Dark Purple (100, 50, 150, 100) | Ominous energy |
| Gravity | 0 m/s² | Circular drift (no fall) |
| Damping | 0.5 | Air resistance |
| Renderer | Additive (dark) | Dark glow |

**Texture:** Smoke/wisp (desaturated flame texture)

### `ParticleSystem_GroundGlow`
| Parameter | Value | Notes |
|-----------|-------|-------|
| Duration | Looping | Under monument |
| Emission Rate | 0.5-1 particle/sec | Very light |
| Lifetime | 5-8 seconds | Long hang time |
| Start Speed | 0.3 m/s | Slight rise |
| Start Size | 1-2m | Large, diffuse |
| Start Color | Light gold (200, 180, 100, 80) | Very transparent |
| Gravity | -0.05 m/s² | Minimal |
| Renderer | Additive | Soft glow |

**Texture:** Soft gradient orb (diffuse light)

---

## Shader Modifications

### StatueKnight Material Enhancement

**Current:** Polyperfect standard material  
**Add:** Glow pass with these channels:

```
Material: "StatueKnight_Holy"
  Base Color: Original texture
  Emission Map: Hand-painted glow map (brighter on sword, face)
  Emission Intensity: 2-3 (visible glow)
  Rim Light: Gold color, 45° threshold
  Rim Intensity: 1.5
  Fresnel: Subtle (edges brighter)
```

**Result:** Statue appears to radiate divine light without changing base appearance.

### Torch Glow Enhancement

**Current:** Polyperfect torch model  
**Add:** Additive overlay particles (no material change needed)

---

## VFX Implementation Steps

1. **Create particle prefabs:**
   - `ParticleSystem_StatueAura` → Assign to statue base
   - `ParticleSystem_TorchMagic` → Assign to each torch (×6-8)
   - `ParticleSystem_TrophyAura` → Assign to weapons (×2)
   - `ParticleSystem_GroundGlow` → Assign to ground circle

2. **Create materials:**
   - `Material_Sparkle` (gold, additive)
   - `Material_MagicFire` (blue/purple, additive)
   - `Material_DarkWisp` (dark purple, additive)
   - `Material_Glow` (light gold, additive, large diffuse)

3. **Enhance statue shader:**
   - Add glow map to `Statue_Knight` material
   - Enable rim light on statue
   - Test in scene (match surrounding lighting)

4. **Create pulse effect:**
   - Coroutine that brightens all particle systems every 5-10s
   - Fade in over 0.2s, out over 0.5s
   - Optional audio cue (magical hum)

5. **Test & optimize:**
   - Verify FPS (target: 60+ on WebGL)
   - Adjust particle count if needed
   - Tweak colors to match village lighting
   - Confirm visibility from distance

---

## Visual Reference

```
Monument at night (glowing effect):

            ✨ ✨ ✨         Gold aura
             |\  |  /|       sparkles
             | \ | / |
           [🎖️ Statue 🎖️]    Glowing statue
             |   |   |       with rim light
          ✨ Torches ✨       Blue magical flames
             |   |   |
        [⚔️ Trophy ⚔️]     Dark trophy auras
             💜  💜  💜
            Sacred ground glow

Monument pulses every 5-10s:
  → All particles 2x brightness
  → Light auras flare out
  → Optional: magical "chime" sound
```

---

## Performance Budget

**Target: 60 FPS on standard WebGL hardware**

| Particle System | Particles/Frame | GPU Cost |
|---|---|---|
| Statue Aura | 3-5 | Low |
| Torches (×8) | 8 | Low |
| Trophy Aura | 4 | Low |
| Ground Glow | 1 | Very Low |
| **Total** | **~16 particles** | **Very Low** |

**Notes:**
- Polyperfect textures are pre-atlased (batch-friendly)
- Additive particles don't require z-sort (fast)
- No geometry changes (pure visual effect)
- Estimated 1-2% GPU overhead

---

## Audio (Optional Companion)

**WO-359 (Combat Feedback) may handle this, but arena monument could have:**
- Subtle magical hum (ambient loop) — 20 dB, low frequency
- Spell cast chime (on pulse) — 60 dB, brief (0.2s)
- Place audio trigger on `ArenaMonument` parent

---

## Customization/Tuning

All values adjustable in Inspector:

```csharp
public class ArenaMonumentAura : MonoBehaviour
{
    [SerializeField] private ParticleSystem[] _particleSystems;
    [SerializeField] private Light[] _auraLights;
    [SerializeField] private float _pulseIntervalSeconds = 5f;
    [SerializeField] private float _pulseIntensity = 2f;
    [SerializeField] private float _pulseFadeInDuration = 0.2f;
    [SerializeField] private float _pulseFadeOutDuration = 0.5f;
    
    // Coroutine handles pulsing
}
```

---

## Testing Checklist

- [ ] Statue aura visible and not overpowering
- [ ] Torch effects add mystical feel (not distracting)
- [ ] Trophy auras feel ominous/conquered (dark energy)
- [ ] Ground glow is subtle (not overwhelming)
- [ ] Pulse effect triggers every 5-10s smoothly
- [ ] No frame rate drop (60+ FPS WebGL)
- [ ] Effects visible from 30+ meters away
- [ ] Auras fade smoothly in/out (no pop-in)
- [ ] Works in day/night lighting
- [ ] Particle count reasonable (no GPU stutter)
- [ ] Audio cue (if added) doesn't repeat excessively
- [ ] Monument feels truly magical/iconic

---

## Dependency on WO-369

**Requires:** Arena Monument placed and positioned (WO-369)

**Unblocks:** Monument is now visually striking endgame destination

**Parallel:** Can be built alongside WO-369 or immediately after

---

## Assets Needed

**Particle textures (search/create):**
- Soft sparkle orb (for gold aura)
- Wispy flame (for blue/purple effects)
- Smoke/gradient (for dark wisps and glow)

**If not available:** Can use unity particle system defaults (smooth round gradient) as placeholder.

---

## What NOT to Touch

- Gameplay (VFX is cosmetic only)
- Monument geometry (particles only, no mesh changes)
- Torch lighting (particle effects layer on top)
- Monument collision (VFX doesn't affect collision)
- Other buildings (isolated to arena only)

---

## Future Enhancements

- [ ] Animated armor on statue (faint glow pulsing)
- [ ] Summoning circle on ground (more elaborate glow pattern)
- [ ] Spell cast animation (aura shoots upward periodically)
- [ ] Victory flare (big glow burst when player collects reward)
- [ ] Seasonal variant (red/orange for winter, blue for frost)
- [ ] Boss presence indicator (aura changes color on boss spawn)

---

## Acceptance Sign-Off

- [ ] Statue aura is visually striking
- [ ] VFX complements monument (doesn't cheapen it)
- [ ] All effects loop seamlessly
- [ ] Performance acceptable (no FPS drop)
- [ ] Monument feels truly iconic and magical
- [ ] Endgame destination feels powerful and unique
- [ ] Ready for monetization (WO-361) to tie rewards to this location

> **AUDIT 2026-08-21 (agent fleet, read-only):** FIXED. Evidence: `ArenaHeraldSpawner.cs:324,347-365,455` — persistent aura + glow. Status was flipped from READY by the AUDIT, not by an implementation pass. The body below is left intact; if this call is wrong, the evidence cited here is what to challenge.
