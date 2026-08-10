# WORK ORDER 37 — Hero Attack VFX + Sound Overhaul (Creative Pass)

**Status:** CLOSED — SUPERSEDED (owner-approved sweep 2026-08-09: VFX flow is now the owner-tag pipeline + WO-195 spell VFX factory)
**Date:** 2026-05-26
**Author:** Creative pass — owner playtest feedback
**Priority:** High — current attack feedback is unconvincing; owner wants
              polished, class-specific VFX and audio before soft launch

---

## Problem

> "Animations are horrible. Please have creative come up with solutions for
> VFX visuals and sound on attack."

Three layers of feedback issues:

1. **Animation**: The "Cast" trigger plays a single generic animation for
   all classes and all abilities. A Knight bashing with a shield and a Mage
   launching a meteor use identical arm-wave gestures.

2. **VFX**: `AbilityVfxKit` provides per-effect-type particles (Strike →
   tracer, Aoe → nova, etc.) but the **visual language is uniform magic**
   regardless of class. Knight's melee Shield Bash shouldn't look like an
   arcane bolt; it should feel like physical iron impact.

3. **Sound**: `ProceduralSfx` generates synthesized tones — a `Strike` sounds
   like "zippy pew" on every class. A Knight's Bash should be a heavy iron
   clang; a Ranger's Quick Shot a bowstring snap; a Mage's Arcane Bolt a
   crackling arc.

---

## Creative Direction per Hero Class

### Knight — Physical / Impact
- **Visual language**: Shockwaves, dust clouds, iron sparks, shield-bash impact
  rings. NO magic sparkles. Colors: warm amber / grey / white impact flash.
- **Sound**: Heavy metallic clang, stone impact boom, deep reverb tail.
- **Animation feel**: Wide, grounded swing — low body, high shield.

### Ranger — Ranged / Nature
- **Visual language**: Arrow tracer (tight line, no glow), leaf-burst on impact,
  frost-blue shimmer on snare. Colors: forest green / ice blue / leaf gold.
- **Sound**: Bowstring snap, whistling flight, soft thud on impact,
  crystalline chime on freeze.
- **Animation feel**: Draw-back, lean, snap-forward release.

### Mage — Arcane / Elemental
- **Visual language**: Pulsing arcane rings, meteor fire streak, frost nova
  crystal shard burst. Colors: violet / ice blue / orange-red / warm gold heal.
  (Already largely implemented in AbilityVfxKit.)
- **Sound**: Crackling arc, ice shatter, warm chime heal, descending roar meteor.
- **Animation feel**: Upward hand gesture, wide arm cast.

---

## Fix — Part 1: Per-Class Animation Variants

### Approach: multiple AnimatorController states, selected at swap time

`HeroAnimatorSetup` currently builds a single `Idle / Walk / Cast` machine.
Extend to support **two cast animations** if the FBX ships them, OR use
**animation speed + offset** to differentiate the single cast clip visually.

For now (Week 5 unblock) use **blend tree weight** to vary the cast look:

```csharp
// HeroBodySwapper — after wiring the controller, set a class-specific param:
// New Animator float: "CastStyle"
//   0.0 = wide mage cast
//   0.5 = forward ranger snap
//   1.0 = low knight bash
float castStyle = cls switch
{
    HeroClass.Knight => 1.0f,
    HeroClass.Ranger => 0.5f,
    _                => 0.0f,  // Mage
};
if (anim.parameters.Any(p => p.name == "CastStyle"))
    anim.SetFloat("CastStyle", castStyle);
```

`HeroAnimatorSetup` builds the controller with a `Cast` blend tree on
`CastStyle` — three directional states (lean-back / neutral / lean-forward).
The same Walk clip drives all three for now; each state can be replaced with
a real hero-specific clip when Tripo exports individual attack animations.

**Long-term (Week 7)**: Request per-class attack animations from Tripo
(each hero: idle, walk, attack-primary, attack-aoe). Drop into
`Resources/Heroes/<slug>_attack.fbx` and `HeroAnimatorSetup.Setup()` will pick
them up by name convention.

---

## Fix — Part 2: Class-Aware VFX

Modify `HeroAbilities.SpawnVfx()` to pass the hero class into `AbilityVfxKit`:

```csharp
// HeroAbilities.SpawnVfx — new signature:
private void SpawnVfx(Vector3 at, AbilityDef def, float radius,
                      Vector3? targetHint = null)
{
    AbilityAudioBridge.PlayForClassAndKind(_heroClass, def.EffectEnum);
    ...
    AbilityVfxKit.SpawnAbilityVfx(def.EffectEnum, def.UnityColor, at,
                                  Mathf.Max(0.6f, radius), targetHint ?? at,
                                  _heroClass);   // new param
}
```

### `AbilityVfxKit` — new entry point

```csharp
/// <summary>
/// Hero-class-aware VFX dispatch. Same effect shapes as before but
/// visual treatment varies per class:
///   Knight  → physical impact (dust, sparks, shockwave ground ring)
///   Ranger  → natural + cold (arrow tracer, leaf scatter, ice ring)
///   Mage    → arcane + elemental (existing AbilityVfxKit behavior)
/// </summary>
public static void SpawnAbilityVfx(AbilityEffect kind, Color color,
                                   Vector3 position, float radius,
                                   Vector3 targetHint, string heroClass)
{
    switch (heroClass)
    {
        case "knight": SpawnKnightVfx(kind, position, radius, targetHint); break;
        case "ranger": SpawnRangerVfx(kind, color, position, radius, targetHint); break;
        default:       SpawnMageVfx(kind, color, position, radius, targetHint);   break;
    }
}
```

### Knight VFX treatments

| Ability (slot) | VFX |
|---|---|
| Shield Bash (Q) | Impact ring on the ground + 12 white/grey sparks burst outward + 1-frame white flash. No tracer. |
| Bulwark Slam (W) | Heavy dust cloud (40 dark-grey particles, hemisphere up) + ground crack ring (amber/white) |
| Oath Ward (E) | Warm amber rising column (same as Mage heal but amber, not gold) + shield-glyph overlay ring |
| Lantern Charge (R) | Orange-amber forward sweep of particles (cone angle 15°, speed 8 m/s) → impact nova with embers |

```csharp
private static void SpawnKnightVfx(AbilityEffect kind, Vector3 at, float r, Vector3 target)
{
    var host = new GameObject("KnightVFX_" + kind);
    host.transform.position = at;
    Color amber = new Color(1f, 0.72f, 0.12f);
    Color grey  = new Color(0.65f, 0.62f, 0.58f);
    Color white = Color.white;

    switch (kind)
    {
        case AbilityEffect.Strike:
            // Impact ring — no tracer, pure physical bash
            BuildGroundRing(host, white, amber, at, 0.6f, 0.3f, 14, 0f);
            BuildSparks(host, white, grey, at, 12);
            FlashLight(host, white, at, 8f, 2f, 0.08f);
            break;
        case AbilityEffect.Cleave:
            // Dust + ground crack
            BuildDustCloud(host, grey, at, r);
            BuildGroundRing(host, amber, grey, at, r * 0.8f, 0.5f, 28, r * 2f);
            FlashLight(host, amber, at, 6f, r + 2f, 0.2f);
            break;
        case AbilityEffect.Heal:
            BuildHeal(host, amber, new Color(1f, 0.85f, 0.4f), at, r);
            break;
        case AbilityEffect.Meteor:  // Lantern Charge
            BuildChargeBeam(host, amber, white, at, target, r);
            FlashLight(host, amber, target, 10f, r + 3f, 0.35f);
            break;
    }
    foreach (var ps in host.GetComponentsInChildren<ParticleSystem>()) ps.Play();
    Object.Destroy(host, 2.6f);
}
```

### Ranger VFX treatments

| Ability (slot) | VFX |
|---|---|
| Quick Shot (Q) | Tight green-white tracer line to target (stretch render, 0.04f vel scale) + small leaf burst at impact |
| Snare Trap (W) | Blue-white tracer + lingering frost ring at target feet (3-second lifetime, fading blue) |
| Mending Salve (E) | Leaf shower falling from above (–Y gravity) + warm green ring pulse |
| Storm of Arrows (R) | Multiple tracer lines in a cone (8 tracers, fan spread ±20°) → leaf + impact at each |

---

## Fix — Part 3: Class-Aware Sound Design

### `AbilityAudioBridge` — new entry point

```csharp
public static void PlayForClassAndKind(string heroClass, AbilityEffect kind)
{
    Resolve();
    if (s_instanceProp == null || s_playSfx == null) return;
    object inst = s_instanceProp.GetValue(null);
    if (inst == null) return;
    AudioClip clip = ProceduralSfx.ForClassAndKind(heroClass, kind);
    if (clip == null) return;
    try { s_playSfx.Invoke(inst, new object[] { clip, VolumeFor(kind) }); }
    catch { /* best-effort */ }
}
```

### `ProceduralSfx.ForClassAndKind` — class-differentiated waveforms

```csharp
public static AudioClip ForClassAndKind(string heroClass, AbilityEffect kind)
{
    var key = (heroClass ?? "mage", kind);
    if (s_classCache.TryGetValue(key, out var cached) && cached != null) return cached;
    // Prefer drop-in authored clip: Resources/Sfx/<heroClass>_<kind>
    var clip = Resources.Load<AudioClip>($"Sfx/{heroClass}_{kind}")
            ?? GenerateForClass(heroClass, kind);
    s_classCache[key] = clip;
    return clip;
}

private static AudioClip GenerateForClass(string heroClass, AbilityEffect kind)
{
    // Synthesis parameters per class × effect:
    // Knight Strike: heavy iron clang (low f0=180, rapid decay, high noise)
    // Knight Cleave: stone impact boom (f0=90, long tail, very high noise)
    // Knight Heal:   warm deep chime (f0=330, clean sine, no noise)
    // Ranger Strike: bowstring snap (f0=2400, very short dur=0.06, low noise)
    // Ranger Snare:  crystalline freeze (f0=1800, f1=3200 shimmer, med noise)
    // Ranger Heal:   soft forest chime (f0=660, f1=1100, no noise)
    // Mage *:        existing ProceduralSfx.Generate(kind) behavior

    (float dur, float f0, float f1, float noise, float amp) p =
        (heroClass, kind) switch
        {
            ("knight", AbilityEffect.Strike)  => (0.18f,  180f,   80f, 0.75f, 0.70f),
            ("knight", AbilityEffect.Cleave)  => (0.30f,   90f,   40f, 0.85f, 0.80f),
            ("knight", AbilityEffect.Heal)    => (0.70f,  330f,  440f, 0.00f, 0.50f),
            ("knight", AbilityEffect.Meteor)  => (0.40f,  120f,   50f, 0.80f, 0.85f),
            ("ranger", AbilityEffect.Strike)  => (0.06f, 2400f, 1200f, 0.10f, 0.45f),
            ("ranger", AbilityEffect.Snare)   => (0.25f, 1800f, 3200f, 0.35f, 0.50f),
            ("ranger", AbilityEffect.Heal)    => (0.65f,  660f, 1100f, 0.00f, 0.45f),
            ("ranger", AbilityEffect.Aoe)     => (0.50f,  400f,  200f, 0.30f, 0.60f),
            _ => default  // fall through to existing Mage synthesis
        };
    if (p == default) return Generate(kind);  // Mage uses existing path
    return Synthesize("sfx_" + heroClass + "_" + kind, p.dur, p.f0, p.f1, p.noise, p.amp);
}
```

### Authored SFX drop-in path (Week 7+)

Place CC0 / licensed clips at:
```
Assets/Resources/Sfx/knight_Strike.wav   → sword-on-shield clang
Assets/Resources/Sfx/knight_Cleave.wav   → heavy stone slam
Assets/Resources/Sfx/knight_Heal.wav     → deep holy bell
Assets/Resources/Sfx/ranger_Strike.wav   → bowstring snap
Assets/Resources/Sfx/ranger_Snare.wav    → ice crystallisation
Assets/Resources/Sfx/ranger_Heal.wav     → forest wind chime
```

`ProceduralSfx.ForClassAndKind` prefers `Resources/Sfx/<class>_<kind>` and
falls back to synthesized audio when the file is absent, so authored clips
drop in with no code change.

**Recommended free source**: freesound.org (CC0/CC-BY) or Kenney Assets
(CC0 Impact / UI Sound packs). Filter for short one-shot clips < 0.5 s.

---

## Files to Edit / Create

| File | Change |
|---|---|
| `Assets/_Modules/Village/Hero/AbilityVfxKit.cs` | Add `SpawnAbilityVfx(..., string heroClass)` overload; implement `SpawnKnightVfx`, `SpawnRangerVfx`; rename existing logic to `SpawnMageVfx` |
| `Assets/_Modules/Village/Hero/AbilityAudioBridge.cs` | Add `PlayForClassAndKind(string, AbilityEffect)`; add `s_classCache` to `ProceduralSfx`; implement class-differentiated synthesis params |
| `Assets/_Modules/Village/Hero/HeroAbilities.cs` | Pass `_heroClass` into `AbilityAudioBridge` and `AbilityVfxKit` calls |
| `Assets/Editor/HeroAnimatorSetup.cs` | Add `CastStyle` float param to generated controller; add blend tree on Cast state |
| `Assets/_Modules/Village/Hero/HeroBodySwapper.cs` | Set `CastStyle` Animator param after swap |

---

## Acceptance Criteria

- [ ] Knight's Shield Bash (1-key) produces a metallic clang + white impact ring + sparks — no magic tracer
- [ ] Knight's Bulwark Slam (2-key) produces a heavy boom + dust cloud + amber ground ring
- [ ] Ranger's Quick Shot produces a bowstring snap audio + tight arrow tracer + leaf burst at target
- [ ] Ranger's Snare Trap produces ice-crystal sound + blue frost ring at target feet
- [ ] Mage visuals and sounds unchanged from current AbilityVfxKit behavior
- [ ] All three heroes have distinguishably different attack animations (CastStyle blend visible)
- [ ] All VFX self-destroy cleanly (Destroy(host, 2.6f) unchanged)
- [ ] No scene re-bake required
- [ ] Drop-in audio path (`Resources/Sfx/<class>_<kind>`) works when authored clips are added
