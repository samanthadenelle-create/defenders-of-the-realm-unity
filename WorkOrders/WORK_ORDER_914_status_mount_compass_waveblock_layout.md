> ## RECONCILED 2026-08-08 - true status is NOT STARTED
> Audit `docs/reference/WO_TRUE_STATUS_2026-08-08.md`. Evidence: the `hud-areas.json` calm(town) `status` area still lists `compass` and `waveBlock` co-occupying it; no re-anchor has landed.
> The previous Status line read "READY TO IMPLEMENT" and was wrong.

# WORK ORDER 914 — Status mount: compass strip vs waveBlock layout (no collision)

**Status: NOT STARTED** (reconciled 2026-08-08, see banner)  
**Minted:** 2026-08-07 (CLI / Grok — residual after WO-899 heading strip)  
**Silo:** HUD / UI  
**Roles:** CLI implement + headless UI capture; PO felt-closes appearance  
**Depends on:** WO-899 strip landed (`a35163e1`) — compass is full width of the status mount; vertical band deliberately left at y 0.34→1.00 “so it cannot NEWLY collide with waveBlock”  
**Related:** `hud-areas.json` calm(town) posture co-occupies `status` with `compass` + `waveBlock`

---

## 0. One-line truth

Calm-town **status** area hosts **two** widgets at once: the new **wide compass strip** (WO-899) and **waveBlock** (wave label / countdown / Start Wave). The strip commit **did not** re-layout waveBlock and **did not** take a UI capture. Appearance is unverified; residual risk is **overlap, glyph cull, or unreadable stacking** on Seeker (2670×1200) and on phone.

---

## 1. Grounded layout today

| Piece | Where | Notes |
|-------|--------|--------|
| Mount | `hud-areas.json` → posture calm → area `"status"` → widgets `compass`, `waveBlock` | Both registered into the same area host |
| Compass strip | `HudCompassWidget.Build` — strip anchors `(0.00, 0.34)–(1.00, 1.00)` of the widget root | Full width of mount; top band; frees “lower half” by claim only |
| Wave block | `HudKitController.BuildWaveBlock` — root fills parent `0–1`; internal stack labels/progress/button | No awareness of strip height; assumes whole mount |
| Prior cull bug | F8 2026-07-08 “0 visible glyphs, rect 333×25” on Start Wave | Fixed by giving the button ~33% of **block** height — still relative to full mount |

**Risk:** strip occupies top ~66% of the widget’s own root; waveBlock’s labels sit at y 0.70–0.99 of **its** root — if both roots fill the same mount rect, **wave label + strip plate occupy the same band**.

---

## 2. Product intent

1. **Compass** = always-readable heading strip (cardinals + enemy pips + objective).  
2. **Wave chrome** = calm-only; countdown + Start Wave only when real (existing wave law).  
3. **Neither may cull glyphs** or paint over the other.  
4. Colourblind / shape rules from WO-899 stay (enemy = red apex-up, objective = gold diamond, caret = gold).

---

## 3. Scope

### Phase A — Measure (instrument first — §12)

Before editing layout:

1. Headless or Editor play: calm posture on `Main_Castle_Overworld`.  
2. Log (or temporary FlowTrace once) **screen-space rects** for:
   - status mount host  
   - `CompassStrip`  
   - `WaveBlock` root + `_waveLabel` + `_waveCountdown` + `_startWaveButton`  
3. Capture PNG (`UI_CAPTURE` path used by the project).  
4. Decide from data: **overlap / no overlap / near-miss**.

If data shows **no overlap and readable** on Seeker aspect + phone aspect → document proof in RESULT, still complete Phase C capture acceptance, skip layout surgery.

### Phase B — Layout fix (only if Phase A shows collision or cull)

Preferred minimal options (pick one after data; do not stack all three):

| Option | Idea | When |
|--------|------|------|
| **B1 Split vertical** | Compass strip top band only (e.g. strip y 0.55–1.00); waveBlock bottom (y 0–0.52) via anchors on each widget root, not full-stretch | Clear collision |
| **B2 Wave under strip** | Keep strip; re-anchor waveBlock children so label/countdown/button live entirely below strip bottom | Collision limited to labels |
| **B3 Posture rows** | calm: status = compass only; move waveBlock to another area / dedicated row in `hud-areas.json` | Only if owner prefers separation |

Constraints:

- Touch floor for Start Wave stays ≥ project min (prior F8 cull fix).  
- Autosize text floors stay.  
- Do not break combat/dungeon postures (status often compass-only).  
- No UXML; code-built only.  
- Do not smuggle dual-compass deletion (`CompassHud` vs kit widget) into this WO — note only.

### Phase C — Proof (always)

- [ ] UI capture calm town: strip + wave chrome both legible; open the PNGs (do not self-certify from code).  
- [ ] At least one wide device aspect (Seeker-class) and one phone portrait/landscape if the capture harness supports it.  
- [ ] `COMPILE_GATE_OK` + `REGRESSION_OK`.  
- [ ] Optional: small source-lint or layout-regression that fails if both widgets claim full 0–1 height of the same mount **and** their content bands overlap in authored fractions (only if stable enough; do not flake on resolution).

---

## 4. Files (likely)

| File | Action |
|------|--------|
| `Assets/_Modules/HUD/Kit/HudCompassWidget.cs` | Strip vertical band if B1/B2 |
| `Assets/_Modules/HUD/Kit/HudKitController.cs` | `BuildWaveBlock` anchors / internal stack |
| `Assets/Resources/Data/Canonical/hud-areas.json` (+ dual-copy if any) | Only if B3 posture change |
| Capture output under project’s UI capture path | Proof PNGs |

**Do not touch:** locomotion, combat arc, action bar faces, VillageSceneBuilder.

---

## 5. Acceptance

- [ ] Phase A rect evidence recorded in RESULT (overlap yes/no).  
- [ ] If overlap: layout fixed so compass strip and waveBlock content do not share the same vertical band.  
- [ ] Start Wave glyphs never 0-visible (regression of F8 2026-07-08).  
- [ ] Calm posture: both widgets usable; non-calm postures unchanged.  
- [ ] PNGs opened and described in RESULT.  
- [ ] `COMPILE_GATE_OK` + `REGRESSION_OK`.

---

## 6. RESULT

`WorkOrders/WORK_ORDER_914_status_mount_compass_waveblock_layout.RESULT.md`
