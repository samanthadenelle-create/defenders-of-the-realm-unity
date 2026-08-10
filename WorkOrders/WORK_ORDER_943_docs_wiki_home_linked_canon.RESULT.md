# WORK ORDER 943 — RESULT (delivered + verified same night)

**Date:** 2026-08-09 late evening  **Seat:** CLI (implementation by edit agent; verification by the lead seat)
**Status:** DELIVERED — verified by the lead seat's own generator run. Owner felt-tour pending (she asked for it; the page exists for her to click through).

## What shipped

- `tools/home_build.py` — stdlib-only generator, board_build.py house style, ~1s.
- `HOME.html` (generated; repo root) — 7 sections: Rules (RULES.md primary — WO-938 landed) /
  Architectural canon / North star (CANON_GROUND_TRUTH resolved NEWEST-BY-DATE at generation
  time, never hardcoded) / Board / Component catalogs / Asset organization / Operator docs.
  Titles + one-line descriptions + links ONLY — zero copied bodies.
- `docs/reference/VFX_ORGANIZATION.md` + `docs/reference/SOUND_ORGANIZATION.md` — GENERATED
  source-cited registries (tree walks + enum parses from C# source), regenerated on every run;
  generated-by headers forbid hand edits. The owner's by-name ask ("how the VFX are organized...
  how the sounds are organized").
- Index rows: `PROJECT_INDEX.md` + `docs/README.md` (one line each). The docs/README stale
  Notion banner was corrected in the same wave (points at newest-by-date + the derived board).

## Verification (lead seat, own hands)

- `python tools/home_build.py` → exit 0, summary: **41 links, all live** (rules:4 canon:2
  northstar:3 board:1 catalogs:20 assets:3 operator:8) + VFX org (10 folders, 146 files) +
  sound org (17 SfxId, 19 MusicTrack values). Run twice, idempotent.
- Dead-link gate proven by dry test (agent run, output on record): a renamed target →
  `HOME_BUILD_FAIL 1 dead link(s)` + exit 1, BEFORE HOME.html is written.

## Findings recorded

- `MusicTrack` exists as TWO enums (`DeNelle.Core.Audio` 9 values / `DeNelle.Audio` 10 values)
  — the sound registry lists both, namespace-cited. `SfxId` lives in `DeNelle.Audio`
  (`Assets/_Modules/Audio/SfxId.cs`), not `DeNelle.Core` as CLAUDE.md §5's table implies —
  the generator tree-walks so it is immune; the table drift is noted, not edited (§5 already
  carries the subset-not-map banner).

## Open

- Optional polish (WO §3): per-doc HTML rendering; a BOARD.html nav rail back-link.
- Owner click-through — the acceptance's "the owner can go home -> rules -> architecture ->
  a specific catalog entry without grepping" is hers to feel.
