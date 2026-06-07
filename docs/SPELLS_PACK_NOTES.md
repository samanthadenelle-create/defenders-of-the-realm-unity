# Spells Pack (Zakhanfx) — VFX Notes

Stylized elemental spell/projectile/shield/aura particle prefabs — the magic-FX
library for the Mage hero, towers, and ability casts. Root: `Assets/Spells Pack/`.
Source read: `Assets/Spells Pack/Documentation/Documentation.txt` + on-disk tree.

## Key paths
- Prefabs root: `Assets/Spells Pack/Particles/Prefabs/`
  - `Spells/` — ground/burst spell bursts, e.g. `Spell_Fire_8.prefab`, `Spell_Arcane_4.prefab`
  - `Projectiles/Casting/` — cast windup (`Casting_Fire_2`, `Casting_Arcane_3`, `Casting_Storm`)
  - `Projectiles/Projectiles/` — the flying bolt (`Projectile_Fire`, `Projectile_Storm`, `Projectile_Light_2`)
  - `Projectiles/Explosion/` — impact burst (`Explosion_Arcane_3`)
  - `Shields/` (`Shield_Fire`, `Shield_Light`), `Auras/` (`Aura_Fire`, `Aura_Ice`),
    `Buffs/`, `Tomes/`
  - `Variations/Spells/<Element>/` — recolored variants (`..._Blue/Red/Yellow/Purple/Green Variant`)
- **7 elements:** `Arcane, Dark, Fire, Ice, Light, Nature, Storm` (naming `Spell_<Element>_<n>`).
- Materials: `Assets/Spells Pack/Particles/Materials/`; Textures: `.../Particles/Textures/`.

## How to use from code
These are **plain ParticleSystem prefabs — no bundled controller script.** Standard pattern:
```csharp
var fx = Object.Instantiate(prefab, hitPoint, Quaternion.identity);
Object.Destroy(fx, 3f); // one-shots don't self-destroy — destroy after the system's duration
```
A cast = `Casting_*` at the caster → spawn `Projectile_*` that travels → `Explosion_*` at
impact. Wire that sequencing through the project's own spell/projectile system; the project
already has VFX bridges (`Assets/_Modules/Village/Hero/RangedAttackVFX.cs`, `AbilityVfxKit.cs`,
`HeroChargeVFX.cs`) and `docs/MAGIC_VFX_LIBRARY.md` catalogs the mapping.

## Gotchas
- **URP fix required.** The pack imports as Built-in/Standard → **magenta**. Run the
  bundled upgrade package: double-click `Assets/Spells Pack/Packages/URP (6000.3.14f1+).unitypackage`
  (also an HDRP variant present). This swaps in URP-optimized materials. (Doc text still
  references old `URP (2020.3.33+)` paths — use the 6000.x package actually on disk.)
- **Scaling:** these are particle systems — do NOT just scale the transform (start size /
  velocity / gravity won't follow). Use the Mirza Beig **Particle Scaler** tool
  (`docs/MIRZABEIG_VFX_NOTES.md`) to scale them correctly.
- Lifetime: prefabs are looping or one-shot per system; destroy after the longest
  system duration so trails/sub-emitters finish.
- Optional better look: Linear color space (project already on Linear).

## Doc sources
- `Assets/Spells Pack/Documentation/Documentation.txt` (support: Zakhanfx@hotmail.com)
- `docs/MAGIC_VFX_LIBRARY.md` (project's VFX→ability mapping)
