# WORK ORDER 941 — RumorBoard + RealmMap: controls overlap text (16 UI_GEOMETRY assertions)

**Status:** DONE - 2026-08-21 CLI, gate-green (COMPILE_GATE_OK + REGRESSION_OK 234/234).
**Minted:** 2026-08-09 (number from the `CLI_LANES_WO_NUMBERS.md` banner; banner bumped 941 -> 943 in the SAME edit as this mint and WO-942's)
**Lane:** HUD/UI (panels). No game logic, no scenes.
**Provenance:** `UICaptureLaunch` geometry oracle, runs of 2026-08-09 20:41 (`Builds/ui-capture2.log`) and 22:04 (`Builds/ui-capture-wave2.log`) — identical failures in both, so these PRE-EXIST tonight's WO-1010 wave (attributed before ticketing, SUNDAY_HOUSEKEEPING §3.4).

---

## 1. The defects (verbatim oracle lines, `[UICap-GEO] BUTTON OVER TEXT`)

**RumorBoard — 14 assertions, both PORTRAIT sizes (1080x2340 + 1200x2670):**
- `CloseButton` (x -180..180, y ~-763..-631) covers the `DetailRewardRow` chip labels ("Food", "Magic", "Rel...") AND the `DetailCta/ObsBtn_Accept` / `ObsBtn_Track` labels.
- Conversely `ObsBtn_Accept` (x ~-340..14) and `ObsBtn_Track` (x ~41..340) each cover `CloseButton/Label`.
- Shape of the bug: the detail pane's reward row + CTA row and the shared Close all land in the SAME bottom band at portrait aspect — three separately-anchored clusters, one band. The playbook's reserved-band rule (§8) applies: publish/stack fixed-px bands, don't let two surfaces claim one.

**RealmMap — 2 assertions, both LANDSCAPE sizes (2340x1080 + 2670x1200):**
- `Nodes/Node_starfall-reach/Disc` (a tappable node) covers map text. Likely a node seated over a label at wide aspects; may need a label-avoidance offset or the label re-seated.

## 2. Acceptance criteria

- [ ] `UI_GEOMETRY_OK` over all 62 canvases (the 16 assertions above at zero; no new ones).
- [ ] Fixed-pixel band fix per `docs/UI_PLAYBOOK.md` §3/§8 — no fraction slices, no independent clamps into one band.
- [ ] `RunCaptureHeadless` re-run; the RumorBoard portrait PNGs and RealmMap landscape PNGs OPENED and readable (labels not under buttons).
- [ ] `COMPILE_GATE_OK` + `REGRESSION_OK <n>/<n>` (read off the markers).

## 3. What NOT to touch

- The build-mode surfaces (WO-1010 lane) and the tutorial surfaces (WO-1012 lane).
- The geometry oracle itself — it is the thing that caught this; it stays as-is.

> **AUDIT 2026-08-21 (agent fleet, read-only):** OPEN — STILL VALID. Evidence: `no WO-941 marker` — 16 UI_GEOMETRY overlaps unfixed. Status left at READY deliberately: this work is real and unbuilt. Verified against HEAD 2f0b97bb5, not against the ticket's own claims.

> **OWNER RULING 2026-08-21 (verbal, this session):** Owner: still to do.

> **CLI 2026-08-21:** a2162f17d - CloseReserveTopFraction MEASURES the Close top; captures still owed
