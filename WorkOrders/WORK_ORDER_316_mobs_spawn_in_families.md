**Status:** CLOSED — owner range sweep 2026-08-21 (WO 0-800): completed or immaterial.

<!-- era-sweep-2026-08-17 -->
> ### ⚠ AGED 2026-08-17 — still READY, but unverified since 2026-06-22
> The 2026-08-17 era sweep found **no evidence** that this WO's subject was deleted or superseded, so its **Status stays READY** and nothing else was changed. It is simply OLD (git first-add 2026-06-22) and has not been re-verified against current canon (`CANON_GROUND_TRUTH_*.md`, CLAUDE.md §7). **Re-verify before pulling it.**

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

## Root cause (triage 2026-06-06)
**Confidence: Likely.** The family/role data exists but nothing composes a group at spawn:
- `EnemyFactory` maps enemy id/role → model **grouped by family** (Orc Warband, Hollow/Skeleton, Troll, with
  Tank/DPS/Healer) — but only for MODEL/silhouette selection per single id
  (`Assets/_Modules/Village/Enemies/EnemyFactory.cs:104-135`).
- `WaveManager` spawns per-id batches via `EnemyFactory.Build(...)` (`Assets/_Modules/Village/Waves/WaveManager.cs:282`).
  There IS a group path — `EnemyGroupSpawner` + a per-wave `WaveEnemyGroup` asset list `_waveGroupSequence`
  (`:113-124`, spawned at `:548-558`) — but it only fires when those assets are authored and assigned. By
  default they are empty, so waves emit loose singletons.

**Suggested minimal fix (two options):** (a) author/assign `WaveEnemyGroup` assets to `_waveGroupSequence`
(data-only, no code) so the existing DEF-21 group path composes families; OR (b) add a small data-driven
family→role-count composer that `WaveManager` calls to spawn a cohesive group at a spawn point. Reuse
EnemyFactory family/role defs + EnemyBrain role behaviour — don't fork. Coordinate with WO-146/WO-155.

## Do NOT touch
- No `.unity` edits. Don't fork EnemyFactory/WaveManager — extend. Coordinate with WO-146 (formations) and WO-155 (region spawn).

> **OWNER RULING 2026-08-21 (verbal, this session):** CLOSED by an explicit owner sweep of the WHOLE 0-800 RANGE: "I've eyeballed them many times, and all of them are already completed. or immaterial." This is a RANGE close on the owner's direct review, not a per-ticket verdict.
