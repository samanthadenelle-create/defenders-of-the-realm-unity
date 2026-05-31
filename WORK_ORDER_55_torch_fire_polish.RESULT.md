# WORK ORDER 55 — Torch & Environmental Fire Polish — RESULT

**Status:** DONE
**Implemented:** 2026-05-29

## Files Created

| File | Braces |
|---|---|
| `Assets/_Modules/Environment/TorchFireController.cs` | 10/10 BALANCED |

## What Was Done

Created `TorchFireController.cs` exactly as specced:

- Auto-finds child `ParticleSystem` (fire) and child `Light` in `Awake()` if not wired via Inspector.
- `_flickerOffset = Random.Range(0f, 100f)` in `Awake()` — each torch gets an independent Perlin noise phase, preventing synchronised pulsing.
- `UpdateFlicker()` drives `pointLight.intensity` via `Mathf.PerlinNoise` scaled by `flickerSpeed` and `flickerAmplitude`.
- `UpdateCombatReaction()` uses `Physics.OverlapSphere` to check for "Enemy" or "EnemyProjectile" tags within `combatRadius` (default 12 m). Smoothly drives `_currentIntensityMultiplier` toward `combatIntensityMultiplier` (default 1.6×) via `Mathf.MoveTowards`.
- Ember emission rate scales with the combat multiplier: `8f * _currentIntensityMultiplier`.
- `OnDrawGizmosSelected()` draws an orange wire sphere when `reactToCombat` is true — visible in Scene view.
- No `NullReferenceException` possible: every field access is null-checked before use.

## Namespace

Global (no namespace) — matches the WO spec and the existing project pattern for environment utility scripts (e.g. `PortalVFXController`, `WeatherManager`).

## Assembly Note

This file lives in `Assets/_Modules/Environment/` which has no `.asmdef`. It compiles into the default assembly. If a `DeNelle.Environment` asmdef is added later, move this file in.

## Prefab Wiring (Inspector task for Samantha / art team)

1. Browse `Assets/Lana Studio/Casual RPG VFX/` for torch / brazier prefabs.
2. Add `TorchFireController` to the root of each prefab.
3. Wire Fire Particles, Ember Particles, Point Light slots.
4. Set `Base Light Intensity` 1.2–1.8 depending on torch size.
5. If no ember sub-system exists, duplicate the fire PS, reduce emission to ~8/s, golden colour, upward velocity.

## Acceptance Criteria Check

- [x] Flicker driven by Perlin noise — not uniform constant light
- [x] Each torch has independent `_flickerOffset` — no synchronised pulsing
- [x] `emberParticles` plays on `OnEnable`; emission rate set in `UpdateCombatReaction`
- [x] `pointLight.intensity` multiplied by `combatIntensityMultiplier` when enemies within 12 m
- [x] Ember emission rate increases during combat
- [x] Combat radius gizmo visible in Scene view
- [x] All null-checks in place — no NRE when Light or Ember PS missing
