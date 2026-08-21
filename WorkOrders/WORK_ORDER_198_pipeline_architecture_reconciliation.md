<!-- era-sweep-2026-08-17 -->
> ### ⚠ AGED 2026-08-17 — still READY, but unverified since 2026-06-22
> The 2026-08-17 era sweep found **no evidence** that this WO's subject was deleted or superseded, so its **Status stays READY** and nothing else was changed. It is simply OLD (git first-add 2026-06-22) and has not been re-verified against current canon (`CANON_GROUND_TRUTH_*.md`, CLAUDE.md §7). **Re-verify before pulling it.**

# WORK ORDER 129 — Pipeline Reconciliation Against the Engine Architecture

**Status:** CLOSED — DEPRECATED, audit-verified obsolete (2026-08-21 backlog audit).
**Owner directive (2026-05-30):** "ask UI to re-look at ALL items in the pipeline and refactor based
on the architect designs." Before the engine refactor is built, the *entire existing queue* must be
reconciled against the new architecture — or half of it gets built the old (hand/AI-driven) way.

---

## Why
A unified, data-driven engine was just designed (4 docs below). It changes *how* almost everything
should be built — a new enemy/structure/biome is now `def + dispatcher`, not a bespoke builder. So
every pending pipeline item needs re-evaluation: does it still make sense, should it change shape,
be retired, or be absorbed into an engine WO? **This is the planning pass that makes "build the
foundation first" real.**

## Read first (the architecture — the reconciliation baseline)
- `docs/ENGINE_MASTER_PLAN.md` — consolidated scope (~5 domains/~18 systems) + **foundation-first** order.
- `docs/CHARACTER_REFACTOR_PLAN.md` — character engine, WO-106…118.
- `docs/WORLD_ENGINE_ARCHITECTURE.md` — world/generic dispatch, NavSurface, WO-119…128.
- `docs/CHARACTER_ARCHITECTURE.md` — vision + catalog/repo + input-scheme + camera.
- `docs/NORTH_STAR.md` — business/product guardrails.

## Scope — audit EVERY pending pipeline item
All `WORK_ORDER_*.md` not yet RESULT'd, the in-session task queue, and any backlog. For **each item**,
classify and note WHY:

| Verdict | Meaning |
|---|---|
| **KEEP** | Still correct as-is; independent of the engine refactor (e.g., a pure bugfix, a content/art task). |
| **REFACTOR** | Still wanted, but should be re-shaped to route through the engine (e.g., "add enemy X" → "author `CharacterDef` X"). Rewrite the WO to the new shape. |
| **ABSORB** | Superseded by an engine WO (106–128); fold it in + retire the standalone. |
| **RETIRE** | No longer needed under the new architecture. |
| **BLOCKED-BY-FOUNDATION** | Wanted, but can't start until Phase 0 (contracts/skeleton) lands. |

## Deliverables
1. `docs/PIPELINE_RECONCILIATION.md` — the per-item verdict table + rationale.
2. **Updated WO statuses** (rewrite REFACTOR items in place; mark ABSORB/RETIRE).
3. **The reconciled build queue** — ordered, **foundation-first**: Phase 0 (WO-106 + WO-119 contracts/
   skeletons) → adapt existing entities/world → features. This is the queue UI then codes + CLI
   compile-gates + commits.

## Guardrails (CRITICAL)
- **This is a PLANNING/reconciliation pass — do NOT start coding the refactor yet.** Output is docs +
  WO edits, not `.cs` changes. (The foundation build kicks off *after* the queue is reconciled.)
- **Foundation-first** (ENGINE_MASTER_PLAN): nothing builds against contracts that don't exist yet.
- **Never break the running game** — the castle/loop work today; the refactor is additive + phased.
- **Mount-sync rule (CLAUDE.md §0):** UI does NOT bash-edit build `.cs`; coding lands via the
  compile-gate → CLI sole-commit lane. (This WO is markdown only — safe.)
- Honor the per-plan guardrails: `DoAction` VFX through `VFXManager` only; cosmetics = catalog not
  repo; `VillageSceneBuilder.cs` single-touch; runtime edits carve NavMeshObstacle.

## Acceptance
- [ ] Every pending pipeline item has a verdict + rationale in `PIPELINE_RECONCILIATION.md`.
- [ ] REFACTOR items rewritten to their engine-aligned shape; ABSORB/RETIRE marked.
- [ ] A single ordered, foundation-first build queue exists, ready for the coding lane.
- [ ] No `.cs` changed (planning pass only).

🤖 Drafted by the build-connected CLI per owner directive.

> **AUDIT 2026-08-21 (agent fleet, read-only):** DEPRECATED. Evidence: `board_build.py + BOARD.html; era-sweep banner` — superseded by derived board. Status was flipped from READY by the AUDIT, not by an implementation pass. The body below is left intact; if this call is wrong, the evidence cited here is what to challenge.
