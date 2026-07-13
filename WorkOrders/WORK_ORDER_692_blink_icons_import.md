> **SOURCE: Grok execution package 2026-07-12** (owner-relayed, built from the docs/SME dossier fleet). Slotted into the WO numbering by CLI; reconcile against docs/SME/WO677_PHASE0_APPLICABILITY.md (the code-verified assessment).

# 🛠️ Work Order: Import Blink 608 Spell/Class Icons (Backlog Item #6)

**Priority:** P1  
**Effort:** Low  
**Impact:** Medium–High (massive UI quality jump)

---

## Goal
The Blink pack contains **608 high-quality spell/class icons** (25 classes × 20 icons + emblems + themed action-bar slots). Our existing importer pipeline can already consume them. Bring them into the project and make them available to the UI.

## What’s already there
- 608 icons
- Existing importer pipeline that can consume them
- Currently unused

---

## Tasks for Claude

1. **Run / update the importer** so all 608 icons are properly imported as sprites (or Addressables if that is the current pattern).

2. **Organize them cleanly**:
   - By class (if the folders allow)
   - Or into a simple icon catalog / ScriptableObject / addressable group

3. **Expose them to the UI system**
   - Make it trivial for any ability / skill / building to pick an icon by key or ID.
   - Update any placeholder icons currently used in the skill bar, build menu, etc. with real Blink icons where it makes sense.

4. **Validation**
   - Create a simple icon browser window (or debug view) so we can quickly see all 608 icons.
   - Confirm they look correct at the sizes we use in the game (skill bar, tooltips, etc.).

---

## Deliverables
- All 608 icons imported and addressable/catalogued
- Easy way for code and designers to reference them
- A few example UI elements updated to use real icons instead of placeholders

Keep the existing UI architecture intact — just feed it better icons.