<!-- era-sweep-2026-08-17 -->
> ### ⛔ ERA SWEEP 2026-08-17 — STALE (undated current-state assertion, CLAUDE.md §15)
> **Git first-add:** 2026-06-22 (the WO itself carries no date at all).
> **Evidence:** undated; asserts `**Branch:** feat/tower-core-loop` (live branch is `wip/village2-and-f8-tickets`). Part of the single WO-290→305 authoring burst.
> Only the `**Status:**` line was rewritten. The body below is UNTOUCHED — CLAUDE.md §15, *"frozen, never rewrite"*. This is a DATING problem, not a verdict on the design — the content may well still be wanted.
> **TO REVIVE:** nothing was deleted and not one line of the body below was changed. If this work is still wanted, re-date the WO (add a `**Minted:** <today>` line), re-point it at the live scene/system, and set `**Status:** READY TO IMPLEMENT`.

# WORK_ORDER_302 — Fix oversized floating health bar (giant green pill over enemies)

**Status:** CLOSED — STALE: undated current-state assertion, needs re-dating (era sweep 2026-08-17)
**Branch:** feat/tower-core-loop · **Lane:** 4 (UI/HUD) · **Depends on:** none

## Context
Targeting an enemy reveals its `FloatingHealthBar` (full-HP = green fill, gold rim, "slim rounded chip").
On some enemies (scaled Humanoid orc/troll family) it renders **massively oversized** — a big green
oval/pill floating above the head instead of a small bar. The code already anticipates this failure mode
("HUGE green bar") via `canvasGo.transform.localScale = Vector3.one / hostScale` (~line 191–193 of
`FloatingHealthBar.cs`), but the host-scale cancel is computing wrong for non-uniform / large `lossyScale`.

## Goal
The bar always renders as a small, consistent slim chip a fixed visual size above any unit, regardless of
the host transform's scale (uniform or non-uniform).

## Files to edit
- `Assets/_Modules/Village/Combat/FloatingHealthBar.cs` (only)

## Scope / approach
- Replace the `Vector3.one / hostScale` compensation with a robust one: compute world-space size from the
  bar's intended **world size in metres**, dividing by the host's `lossyScale` per-axis (guard against
  zero/!finite components), so the bar's on-screen size is constant.
- Re-evaluate `_heightOffset` (2.4) and clamp (0.5–4.0) against the corrected scale so the bar sits just
  above the head, not at a scaled-up height.
- Verify on a normal-scale enemy AND a scaled enemy (People-orc) that the bar is identical size.

## Acceptance criteria
- [ ] On a scaled Humanoid orc/troll, the targeted health bar is a small slim chip near the head (no giant green pill/oval).
- [ ] On a normal enemy and a hero, the bar is the same on-screen size as the scaled enemy.
- [ ] Full HP shows green, mid amber, low red (unchanged); gold rim unchanged.
- [ ] No NaN/zero-scale exceptions; bar still hides at full HP unless targeted (DEF-206 behavior intact).
- [ ] Brace balance check passes on the file (CLAUDE.md §1); CompileGate `COMPILE_GATE_OK`; Windows build SUCCESS.

## Do NOT touch
- HeroTargetIndicator reticle (separate, working). No `.unity` edits. No other HUD files.
