# Mirza Beig VFX Suite — Notes

A bundle of 300+ general-purpose particle prefabs (fire, explosions, storms, titles,
shockwaves) plus runtime particle-effect components and editor tools. Root:
`Assets/Mirza Beig/`. **All scripts are in namespace `MirzaBeig`.**
Sources read: the 6 READMEs in `Assets/Mirza Beig/_DOCS/` + on-disk script tree.

## Modules
| Module | What it gives you | Where |
|---|---|---|
| **Ultimate VFX** | The core 300+ prefab library (fire/smoke/explosion/galaxy/etc.) | `Particle Systems/Ultimate VFX/Prefabs/` |
| **Action VFX** (XP) | Combat hit/slash/spark prefabs | `Particle Systems/Ultimate VFX/Expansions/XP - ACTION/` |
| **Storm VFX** (XP) | Rain/lightning/snow/terrain-rain prefabs + wet shaders | `Particle Systems/Ultimate VFX/Expansions/XP - STORM/` |
| Shockwaves / Titles / Constr. Kit (XP) | More expansion prefabs | `.../Expansions/XP - SHOCKWAVES`, `XP - TITLES`, `XP - CONSTR. KIT` |
| **Advanced Particle Scaler** | Editor tool to correctly scale particle hierarchies | `Window ▸ Mirza Beig ▸ Particle Scaler` |
| **Particle Force Fields** | Runtime attract/vortex/turbulence components | `Scripting/Effects/Particle Force Fields/` |
| **Particle Plexus** | Runtime "connecting lines between particles" effect | `Scripting/Effects/Particle Plexus/` |

Other on-disk runtime script folders: `Scripting/Effects/{Particle Affectors, Particle
Flocking, Particle Lights}`. Custom particle shaders live in `Assets/Mirza Beig/Shaders/`.

## How to use from code (components — all `using MirzaBeig...`)
- **Force fields** — `AddComponent` to a particle GameObject (or via
  *Component ▸ Effects ▸ Particle Force Fields*):
  `AttractionParticleForceField`, `VortexParticleForceField`, `TurbulenceParticleForceField`.
- **Plexus** — `ParticlePlexus` component on a particle system; exposed
  `AlphaOverNormalizedDistance`, mesh-triangle support.
- Prefabs are plain ParticleSystems: `Instantiate` + destroy after duration (same as
  Spells Pack). Some demo prefabs ship **disabled** — `SetActive(true)` after instantiate.

## Advanced Particle Scaler — important
- It is an **editor tool, not a runtime API** (`Window ▸ Mirza Beig ▸ Particle Scaler`;
  source `Editor Extensions/Utilities/Particle Scaler/Editor/`). Select a particle
  GameObject (or a whole hierarchy), set a scale, hit Apply — it scales start size,
  velocity, gravity, etc. consistently across the hierarchy.
- **Use this to resize ANY particle pack** (Spells Pack, Lana, Mirza) — scaling the
  Transform alone leaves emission/velocity wrong. Bake the scaled prefab as a new variant.

## Gotchas
- These force-field components are Mirza's OWN, NOT Unity's Shuriken force modules —
  distinct, more capable; don't confuse the two.
- URP: prefabs use Mirza's custom particle shaders (under `MirzaBeig/Particles/...`).
  If any prefab shows magenta, reassign its material/shader; most render in URP as-is.
- Many demo prefabs are disabled by default for the demo scene — enable before use.

## Doc sources
- `Assets/Mirza Beig/_DOCS/README - *.txt` (6 files)
- Official: http://www.mirzabeig.com/products/ (ultimate-vfx, particle-plexus, particle-force-fields)
