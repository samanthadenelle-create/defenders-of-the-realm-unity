# Lana Studio — Casual RPG VFX Notes

Stylized casual-RPG particle prefabs (slashes, bursts, orbs, shields, heals, loot,
status states) — a lighter, cuter VFX set alongside Spells Pack / Mirza Beig.
Root: `Assets/Lana Studio/Casual RPG VFX/`. Source read: `.../Readme.txt`.

## Key paths (~128 prefabs)
`Assets/Lana Studio/Casual RPG VFX/Prefabs/<category>/`, categories:
`Area_generic, Backlight_resources, Burst, Fire, Fog, Loot, Orbs, Range_attack,
Regeneration, Shields, Slash, States, Top_down_attack`.
Materials `.../Materials/`, Textures `.../Textures/`, demo scenes `.../Demo/Scenes/`,
helper scripts `.../Scripts/` and `.../Demo/Scripts/`.

## How to use from code
Plain ParticleSystem prefabs — `Instantiate` at the target and destroy after duration
(same pattern as the other VFX packs). `Slash/` + `Top_down_attack/` suit melee hits,
`Range_attack/` for projectiles, `Regeneration/` + `States/` for buffs/heals,
`Loot/` for pickup pops, `Shields/` for guards.

## Gotchas
- **Ships Built-in Render Pipeline shaders.** For URP you MUST run the bundled upgrade:
  folder `Assets/Lana Studio/Casual RPG VFX/Upgrade for URP/` → run **"Upgrade for URP"**.
  Without it the prefabs render magenta.
- Readme says the demos were authored for **Gamma** color space; this project runs
  **Linear**, so colors/brightness will read slightly differently than the demo video
  (fine for gameplay, just don't expect a pixel match).
- Scale via the Mirza Beig Particle Scaler tool, not raw Transform scale
  (see `docs/MIRZABEIG_VFX_NOTES.md`).

## Doc sources
- `Assets/Lana Studio/Casual RPG VFX/Readme.txt` (support: Glowinghuman@gmail.com)
