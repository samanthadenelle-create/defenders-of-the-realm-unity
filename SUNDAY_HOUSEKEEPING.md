# ⛭ SUNDAY HOUSEKEEPING — the weekly full-sweep ritual

**Owner directive (2026-07-19, BINDING/standing):** *"Every Sunday we go through everything and make
sure we are not missing things, do all housekeeping and get organized — this way we truly know what is
done and everything stays clean and known."*

This is CLAUDE.md §15's "weekly 5-minute audit" elevated into a full, institutionalized sweep, run as
an agent fleet. Memory: `sunday-housekeeping-ritual`, `audit-outputs-as-known-dictionaries`.

---

## 1. The ritual (run every Sunday, or on owner "run the Sunday sweep")

| # | Step | Output |
|---|------|--------|
| 1 | **Boot canon sweep** — the START_HERE load-bearing set green vs the newest `CANON_GROUND_TRUTH_<date>.md` anchor. | list of stale doc lines |
| 2 | **Git / WO reconciliation** — HEAD; local vs origin; WO next-free (from `CLI_LANES_WO_NUMBERS.md` banner, NOT filesystem max); EVERY WO spec Status vs git reality; RESULT files present; working tree clean/known. | WO ledger refreshed |
| 3 | **Full silo audit (fleet)** — one read-only auditor per silo hunts anything overlooked/missed that week, going BEYOND the existing gap-audit. | findings per silo |
| 4 | **Regression-coverage proof** — map EVERY finding to a regression suite; uncovered → queue a new regression. **Owner's bar: we must KNOW our regressions cover everything found.** | coverage matrix + uncovered list |
| 5 | **Refresh the KNOWN DICTIONARIES** (§2 below). | dictionaries current |
| 6 | **Mint `CANON_GROUND_TRUTH_<date>.md`**; banner the prior SUPERSEDED; update the load-bearing docs in the SAME breath (§15). | new anchor |
| 7 | **Housekeeping** — stale-doc banners, dead-file cleanup, index/README/memory refresh, frozen-ledger banners. | tree organized |
| 8 | **Report to owner** — what's DONE, what's open, what's newly known; push held for owner OK. | the Sunday report |

**Reusable engine:** the `silo-audit-regression-coverage` and `hero-animation-audit` workflow fleets
(built 2026-07-19). Promote them to saved `.claude/workflows/` so each Sunday reuses the same harness.

---

## 2. KNOWN DICTIONARIES — the stored registries (keep current every Sunday)

Durable, canonical registries so state stays KNOWN. Each is refreshed by the Sunday sweep and updated
in the same breath as any change that invalidates it.

| Dictionary | What it stores | Location |
|---|---|---|
| **Hero Animation / Action map** | every hero animation → action; Right ActionBar (Attack + 3 named skills + clips); Hot-Swap bar actions + mappings | `docs/reference/HERO_ANIMATION_DICTIONARY.md` (+ dual-copy JSON if runtime-read) |
| **Regression-coverage matrix** | every audit finding → covering regression suite, or "needs a new one" | `docs/reference/REGRESSION_COVERAGE_MATRIX.md` |
| **WO ledger** | next-free number + every WO's real status (spec/READY/done/RESULT) | `CLI_LANES_WO_NUMBERS.md` banner (authority) |
| **Feature-flag registry** | every `ff.*` flag + default + meaning | `Assets/_Modules/Core/FeatureFlags.cs` (source of truth) |
| **Save-schema field map** | every persisted field → version added → migrator step | `SaveSchema.cs` / `SaveMigrator.cs` |
| **Regression-oracle inventory** | every suite `[tag]` + what it locks + current known-red baseline | this file / the newest `CANON_GROUND_TRUTH` |

New audit that produces a durable answer → add its dictionary here and store it (memory:
`audit-outputs-as-known-dictionaries`).

**Every dictionary entry is a well-known, easily-confirmed FACT** — each row carries its source
citation (`file:line` / asset path), so any single fact can be re-verified at a glance rather than
re-derived. That is the whole point: the activity becomes a known, confirmable fact, not a memory.

---

*Maintained by the CLI/orchestrator. The Sunday sweep is the guarantee that "what is done" is always
answerable from stored, known state — never reconstructed from memory.*
