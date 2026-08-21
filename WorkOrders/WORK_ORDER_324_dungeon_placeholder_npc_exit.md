**Status:** CLOSED — owner range sweep 2026-08-21 (WO 0-800): completed or immaterial.

<!-- era-sweep-2026-08-17 -->
> ### ⚠ AGED 2026-08-17 — still READY, but unverified since 2026-06-22
> The 2026-08-17 era sweep found **no evidence** that this WO's subject was deleted or superseded, so its **Status stays READY** and nothing else was changed. It is simply OLD (git first-add 2026-06-22) and has not been re-verified against current canon (`CANON_GROUND_TRUTH_*.md`, CLAUDE.md §7). **Re-verify before pulling it.**

# WORK_ORDER_324 — Dungeon: placeholder pill for lantern NPC + 2 circles for exit

**Status: READY TO IMPLEMENT**
**Branch:** feat/tower-core-loop · **Lane:** 5 (World/Exploration) · **Origin:** owner playtest 2026-06-06
**Reconcile with:** Dungeon scene builder / `DungeonEntranceBootstrap`, NPC/exit prefab spawn, portal VFX (WO-272/250)

## Problem
The dungeon loads with **placeholder primitives**: the **lantern NPC renders as a capsule/pill**, and the
**exit shows as two circles** instead of a proper portal/exit visual. Real prefabs/visuals aren't wired.

## Goal
The dungeon's lantern NPC uses a real character/lantern prefab, and the exit uses a proper portal visual
(single readable exit) — no placeholder pill/circles.

## Scope
- Replace the lantern-NPC capsule with the intended NPC (character pack) + lantern prop; correct scale/anchor.
- Replace the "2 circles" exit with a proper exit portal visual (reuse the portal/glow VFX path — WO-250/272 /
  `PortalVFXController`); ensure it's a single clear interactable exit with the right trigger.
- Place via the dungeon builder/bootstrap (not hand-edited scene); missing prefab → LogWarning, not error.

## Acceptance criteria
- [ ] Lantern NPC renders as a real model (no capsule/pill), correct scale/position.
- [ ] Exit is a single proper portal visual (no stray double circles), interactable to leave the dungeon.
- [ ] Wired via dungeon builder/bootstrap; reuses existing portal VFX (no fork).
- [ ] Brace check; CompileGate OK; build SUCCESS; verify in a play session.

## Root cause (triage 2026-06-06)
**Confidence: Likely (where-to-look correct).** Placeholder primitives never swapped for real prefab/VFX — the
systems to reuse already exist: `DungeonEntranceBootstrap` (`Assets/_Modules/Village/Dungeons/DungeonEntranceBootstrap.cs`)
and `PortalVFXController` (`Assets/_Modules/Village/Dungeon/PortalVFXController.cs`). The lantern NPC renders as
a capsule and the exit as two circles because the dungeon builder/bootstrap is emitting primitive stand-ins
rather than instantiating the character-pack NPC + the portal VFX path. Additive wiring, not a logic bug.
**Suggested minimal fix:** in the dungeon builder/bootstrap, replace the lantern-NPC capsule with the intended
character prefab + lantern prop, and replace the "2 circles" exit with a single `PortalVFXController` portal
visual + correct exit trigger. Missing prefab → LogWarning, not error. Reuse the existing portal VFX (no fork).

## Do NOT touch
- No hand-edited `.unity`. Reuse the character pack + PortalVFXController; don't greenfield a new portal system.

> **OWNER RULING 2026-08-21 (verbal, this session):** CLOSED by an explicit owner sweep of the WHOLE 0-800 RANGE: "I've eyeballed them many times, and all of them are already completed. or immaterial." This is a RANGE close on the owner's direct review, not a per-ticket verdict.
