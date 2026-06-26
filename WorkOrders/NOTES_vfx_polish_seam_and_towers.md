# NOTES — VFX polish (gate-crossing + tower upgrade/firing)

> Status: **QUEUED POLISH — not built.** Captured 2026-06-26 from Grok asset/VFX notes.
> These are *after* the functional four-side seam + the Pi V1 loop. Logged so they're not lost.

## ⚠️ Caveat before implementing (verify, don't paste)
Grok produced **two inconsistent VFX catalogs** in the same session (one with `Impact_Fire/Explosion_Fire`,
another with `Impact_Aether` + a `VFXType.cs` master enum). Treat every specific prefab/enum NAME it
listed as **unverified** — Grok has already named non-existent APIs three times this session
(`CreateForGate`, `TargetManager` for towers, `CreateGateForRecipe`). The VFX **stack is real**
(verified files below); the individual enum entries it cited are NOT confirmed. Read the real
`VFXType.cs` + `VFXCatalog.asset` before adding anything, and **reuse before adding**.

## Real VFX stack (verified to exist 2026-06-26)
- `Assets/_Modules/Village/Vfx/`: `VFXType.cs`, `VFXManager.cs`, `VFXCatalog.cs`, `VfxPool.cs`,
  `VFXHandle.cs`, `SpellVfxFactory.cs`, `EnvironmentVFX.cs`, `PetAuraVFX.cs`, `DungeonVFXSettings.cs`
- `Assets/Resources/VFX/VFXCatalog.asset` (the SO wiring enum→prefab)
- `Assets/_Modules/Village/Hero/AbilityVfxKit.cs` (procedural fallback), `RangedAttackVFX.cs`
- `Assets/_Modules/Village/Buildings/ProjectileVFXCatalog.cs`
- `Assets/_Modules/Village/Dungeon/PortalVFXController.cs` + `PortalVFXInjector.cs`
  → **CHECK THIS FIRST for the seam:** a portal VFX controller already exists. The four-side gate
  "warp swirl / travel flash / arrival dust" should likely REUSE/extend PortalVFXController, not add
  4 new prefabs (memory: reuse built systems; `VFXManager` is the single pooled/quality-gated owner).
- Art packs present (gitignored, large): `Assets/Spells Pack` (2039 files), `Assets/Quaternius/Medieval
  Village MegaKit` (1932), `Assets/Supercyan` (1446). NOTE: Grok's "Assets/_Modules/VFX" path does NOT
  exist — the module is `Assets/_Modules/Village/Vfx`.

## Ask 1 — gate-crossing VFX (four-side seam polish)
On hero cross (`RuntimeRegionGate` / `SceneTransitionTrigger.Cross`), play a warp swirl at the gate +
an arrival puff at the landing, via `VFXManager.Play(...)`. Prefer reusing `PortalVFXController` /
existing `Env_DungeonPortal`-class effects over net-new prefabs. URP-safe, mobile/WebGL-cheap.
**Depends on:** the functional four-side seam landing first (in flight).

## Ask 2 — tower upgrade + firing VFX
- Firing: in `TowerCombat` fire path, play an element-coded shot at the muzzle + impact at the hit point.
  Note: tower targeting is `TowerCombat.FindNearestTarget` over `WaveManager.LiveEnemies` (NOT
  `TargetManager`, per this session's verification) — wire the VFX where `FireAt` runs.
- Upgrade: on tier-up (the WO-432 building-upgrade path), play a flash + persistent aura on the tower;
  tier-3 overcharge burst. Use procedural fallback (`AbilityVfxKit`) where a prefab isn't assigned.
- Verify the real `VFXType` enum + `TowerElement`/tower-data fields exist before writing any switch.

## Sequencing
Functional four-side seam (in flight) → Pi V1 loop (the deadline) → **then** these VFX polish passes.
Promote to a numbered WO via the numbering authority (`CLI_LANES_WO_NUMBERS.md`) when scheduled.
