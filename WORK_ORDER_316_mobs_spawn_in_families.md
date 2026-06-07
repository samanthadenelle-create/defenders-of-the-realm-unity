# WORK_ORDER_316 — Mobs not spawning in families / role groups

**Status: READY TO IMPLEMENT**
**Branch:** feat/tower-core-loop · **Lane:** 2 (Combat/AI) · **Origin:** owner playtest 2026-06-06 (screenshot)
**Reconcile with:** `EnemyFactory` (family/role comments from WO-106), `WaveManager`, region spawner (WO-155), formations (WO-146)

## Problem
Enemies spawn as loose singletons (e.g. lone wizards) instead of coherent **families/warbands** with mixed
roles. The family + role data/comments exist in `EnemyFactory` (Orc Warband, Skeleton Legion/Hollow,
Stonebelly Trolls, etc. + Tank/DPS/Healer), but spawning doesn't actually compose groups.

## Goal
Waves/region spawns produce **family groups** — a themed family spawning together with a role mix
(e.g. Tank + DPS + Healer/caster), not random individuals.

## Scope
- Define family compositions (data-driven: family → role counts/weights) building on the existing
  `EnemyFactory` family/role definitions (don't fork them).
- `WaveManager` / region spawner: spawn a chosen family as a cohesive group at a spawn point, with the role
  mix; optionally hand the group to formation movement (WO-146) so they travel together.
- Scale family choice + size by threat/region (ties WO-155/WO-164 ThreatLevel where available).

## Acceptance criteria
- [ ] A wave/region spawn produces a recognizable family group with a role mix (not all identical singletons).
- [ ] Family + size scale with wave/threat; multiple families can appear across a wave.
- [ ] Uses existing EnemyFactory family/role defs + EnemyBrain role behavior (no duplicate systems).
- [ ] Brace check; CompileGate `COMPILE_GATE_OK`; Windows build SUCCESS; verify in a play session.

## Do NOT touch
- No `.unity` edits. Don't fork EnemyFactory/WaveManager — extend. Coordinate with WO-146 (formations) and WO-155 (region spawn).
