# WORK ORDER 718 — Kit-law regression oracle

**Status:** READY TO IMPLEMENT  
**Priority:** P1 (stops re-bleeding)  
**Phase:** 1 (Bleed)  
**Effort:** S  
**Depends on:** none  
**Program:** Grok-03 · **Guidance:** Grok-02 §4 factory API  

---

## Goal

Make “hand-rolled wallets / tabs / closes / Filled bars without sprites” a **machine-visible failure**, so every later WO cannot quietly invent a second UI system.

---

## Tasks

1. Add or extend an **EditMode / DataRegression-style** check (prefer `Assets/Editor/Regression/`):
   - **Forbidden patterns** in UI consumer assemblies (configurable allowlist for kit itself):
     - New `Currency`/`Crystals:` string formatters outside kit CompactNumber paths (heuristic).  
     - Direct `Image.Type.Filled` assignments outside `ElarionUiKit*` / known bar helpers.  
     - Panel files that construct Close buttons without kit close naming convention (best-effort).  
   - **Required patterns** for new panels: optional softer warn list.
2. Print marker **`KIT_LAW_OK`** / **`KIT_LAW_FAIL`** with file:line hits.  
3. Wire into DataRegression or CompileGate satellite so CI/local gate sees it.  
4. Document allowlist process in RESULT (how to add a legitimate exception).

---

## Acceptance

- [ ] Running the oracle on HEAD produces a stable report.  
- [ ] At least one known bad pattern (if present) is flagged OR clean `KIT_LAW_OK` with empty hits.  
- [ ] Kit sources (`ElarionUiKit*.cs`) are allowlisted.  
- [ ] RESULT explains how 719/720/722 must stay green.

---

## Not in scope

- Fixing all hits (that’s 717/720/722) — this WO ships the **gate**, then optionally fixes gate-blockers only.

---

## RESULT

`WorkOrders/WORK_ORDER_718_kit_law_regression.RESULT.md`
