<!-- era-sweep-2026-08-17 -->
> ### ⚠ AGED 2026-08-17 — still READY, but unverified since 2026-06-22
> The 2026-08-17 era sweep found **no evidence** that this WO's subject was deleted or superseded, so its **Status stays READY** and nothing else was changed. It is simply OLD (git first-add 2026-06-22) and has not been re-verified against current canon (`CANON_GROUND_TRUTH_*.md`, CLAUDE.md §7). **Re-verify before pulling it.**

# WORK ORDER 182 — Avalon → Elarion Canon Purge (doc hygiene)

**Status:** READY TO IMPLEMENT
**Lane:** none (docs only — no code, no build, no bake). Low priority, do between builds.
**Owner decision (2026-05-31):** Elarion / Stone Choir is canon. Purge "Avalon" from LIVE specs.

## Intent
"Avalon" was retconned to "Elarion." It still appears in 25+ markdown files. Replace it in
**active specs that drive implementation**, so NPC/dialogue work (WO-116) and any builder can't bake
the wrong name. **Do NOT rewrite historical records** that intentionally document the rename.

## Rule of thumb
- **EDIT** (replace Avalon→Elarion): live layout/design/port specs an implementer would follow.
- **LEAVE** (history — keep as-is): changelog/decision/QA-log entries that record the rename happened.
  Rewriting these erases the audit trail.

## Files — EDIT (active specs)
- `docs/avalon-village-layout-spec.md` (10 hits) — also rename file → `elarion-village-layout-spec.md`
- `docs/dungeons-3d-unity-layout-spec.md` (9)
- `docs/v2-unity-port-spec.md` (5)
- `docs/v2-unity-port-backend-spec.md` (1)
- `docs/port-notes/realm-map-data.md` (4)
- `docs/port-notes/canon-data.md` (3)
- `docs/port-notes/wall-prop-fixes.md` (2)
- `docs/port-notes/dragon-wave-wiring.md` (1)
- `docs/port-notes/dragon-boss.md` (2)
- `docs/enemy-codex.md` (1)
- `docs/PARTY_OF_FOUR_STORYLINE.md` (1)
- `docs/ECHOES_OF_ELARION_NARRATIVE.md` (17) — narrative; replace unless a line is explicitly "formerly Avalon"
- `docs/village-review-suggestions.md` (2)

## Files — LEAVE (history / audit — do not edit)
- `CLAUDE.md` (the §7 line that *bans* Avalon — keep, it's the rule)
- `COHESION_AUDIT_AND_DECISIONS.md`, `docs/DESIGN-DECISIONS.md`, `docs/unity-decisions.md` (record the decision)
- `docs/qa/*` (uat-script, qa-test-plan, po-validation, bug-log — historical test records)
- `.claude/worktrees/**` (transient branch copies — never edit)

## Acceptance
- Active specs read "Elarion"; no "Avalon" left in the EDIT list (except explicit "formerly Avalon" notes).
- History files untouched.
- `avalon-village-layout-spec.md` renamed; update any links pointing to it.
- Append a CHANGELOG line to ORCHESTRATION_LIVE.md noting the purge.

## Note
Editing many .md via the Linux mount has minor sync risk — prefer Windows-side edits, verify a couple files after.
