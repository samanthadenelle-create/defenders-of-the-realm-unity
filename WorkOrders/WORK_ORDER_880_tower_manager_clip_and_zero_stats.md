# WORK ORDER 880 — Tower Manager: row clipped mid-height + towers show rng 0 / dmg 0

**Status:** READY. **Lane:** HUD/UI + data — `TowerManagerPanel.cs` / `PlacedTowerListVM`. **WO#:** UI-seat block; **880**.
**Source:** `docs/ui-review/screens-2026-08-04/TowerManagerPanel_2340x1080.png`.

## 1. Bad (from the capture)
- **Layout:** the third row ("Tower 3 …") is **clipped mid-height** by the list well's bottom edge — a hard cut, not a
  clean scroll boundary. The list well is sized so a row lands half-off.
- **Data:** every row reads **`(rng 0, dmg 0)`** — the towers report range 0 / damage 0, which looks broken.

## 2. Fix — two layers, each in its lane (MVVM law)
- **Layout (View):** size the list well to whole rows (fixed-pixel row height × N) so a row is never half-clipped, OR
  give it a clean scroll boundary (RectMask2D + row-height snapping). Presentation only — no logic.
- **Data (VM/common, NOT the View):** `rng 0 / dmg 0` is a **VM/data defect** — `PlacedTowerListVM` is reading zero
  range/damage for placed towers. Fix it at the source (the VM reads the tower's real `CurrentRange`/`CurrentDamage`
  from the tower/catalog), so the View just renders the real numbers. **The View must NOT compute stats — it renders
  what the VM provides.** (If the towers genuinely ARE 0/0 at L1, that's a separate balance bug — flag it; but the
  `FormatManagerRow` path should show the live values.)

## 3. Acceptance
- [ ] On-device: no row is clipped mid-height (whole rows / clean scroll); rows show the towers' REAL range + damage
      (not 0/0). The stat values come from the VM, not View math. `CompileGate` green. Verify on Seeker.

## 4. Do NOT
- Do NOT patch the 0/0 in the View — fix the VM/data source. No fraction bands. No scene edits.
