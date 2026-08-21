**Status:** CLOSED — owner range sweep 2026-08-21 (WO 0-800): completed or immaterial.

<!-- era-sweep-2026-08-17 -->
> ### ⚠ AGED 2026-08-17 — still READY, but unverified since 2026-06-22
> The 2026-08-17 era sweep found **no evidence** that this WO's subject was deleted or superseded, so its **Status stays READY** and nothing else was changed. It is simply OLD (git first-add 2026-06-22) and has not been re-verified against current canon (`CANON_GROUND_TRUTH_*.md`, CLAUDE.md §7). **Re-verify before pulling it.**

# WORK_ORDER_471 — UITK panel z-order audit (PanelSettings.sortingOrder, project-wide)

**Status: READY TO IMPLEMENT** · Drafted by read-only agent (2026-06-21), reconciled to code.

## Problem
Root cause already PROVEN (commit 0692b7e7, dev-tools fix): every UITK panel sets
`UIDocument.sortingOrder` but **input dispatch reads `PanelSettings.sortingOrder`** — which most panels
never set, so they collapse to sort 0 and a backdrop can pick over the intended top panel. The dev-tools
trio (AdminOverlay/HelpMenu/DevBootstrap) is fixed. **This WO sweeps the rest** so no other panel has the
same latent z-order/input bug (owner's "panel z-order / World Seam" report, ticket #14).

## Scope
- Audit EVERY `UIDocument.sortingOrder = …` site under `Assets/_Modules/` and ensure the matching
  `PanelSettings.sortingOrder` is set to the same value — **but only on an OWNED PanelSettings**. If the
  document resolves/adopts a possibly-shared PanelSettings (the DevBootstrap pattern), CLONE before mutating.
- Candidate sites to verify (grep `sortingOrder`): ShopPanel, DialogueView, the modal/chrome layer,
  ArenaDefensePaletteUI, any quest/build/upgrade panel.

## Acceptance
- Every audited panel: `PanelSettings.sortingOrder == UIDocument.sortingOrder`, on an owned (never shared) asset.
- No panel mutates a shared/Resources PanelSettings asset (clone-first rule).
- Owner felt-verify: opening any two stacked panels, the top one receives taps (no dead backdrop pick).

## NOT to touch
The render order of correctly-working single panels; PanelSettings theme/scale; the dev-tools trio (already fixed).

## INSTRUMENT-FIRST (§12 hard gate)
Reuse the `[POINTER-DUMP]` / `[Flow:DevTapDiag]` instrumentation that proved the dev-tools root cause:
on a tap that misses, dump the live panel stack with each panel's PanelSettings.sortingOrder. Headless can
build the stack + assert sortingOrder parity; the actual top-panel-receives-tap is owner felt-verify (no headless tap).

> **OWNER RULING 2026-08-21 (verbal, this session):** CLOSED by an explicit owner sweep of the WHOLE 0-800 RANGE: "I've eyeballed them many times, and all of them are already completed. or immaterial." This is a RANGE close on the owner's direct review, not a per-ticket verdict.
