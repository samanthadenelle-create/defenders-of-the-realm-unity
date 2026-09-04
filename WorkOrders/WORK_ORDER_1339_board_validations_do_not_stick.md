# WORK ORDER 1339 - Owner validations do not survive a rebuild

**Status:** CLOSED 2026-09-04 - owner felt-test PASS (validated 2026-09-04T14:32:02, build 2026.09.04.354315). PRIOR STATUS: FIXED 2026-09-03 - committed. The record now lives in `proof/owner-validations.json`,
keyed by WO FILENAME (this repo has duplicate WO numbers, so a friendly label would make two unrelated
files share one sign-off), one ticket per line with keys sorted so two seats marking different tickets
merge without a human. `--ingest` is the ONLY writer; a rebuild has no write path to the record at all,
and an UNREADABLE record ABORTS the rebuild rather than rendering "0 verified" over corrupt bytes.
⚠ ONE MANUAL STEP REMAINS AND THE OWNER MUST KNOW IT: on the board, **Owner Validation -> "Export for
the CLI" -> Copy**, then paste to the CLI, which runs `python tools/board_build.py --ingest -`. It needs
no server, no auth and no network, works on a phone over `file://`, and cannot lose a mark - but nothing
reaches the durable record until she exports. AWAITING HER FIRST EXPORT.
**Silo / Lane:** Board tooling / evidence
**Type:** EXISTING board, defect in how a sign-off was stored
**Minted:** 2026-09-03 (CLI) - ⚠ RETROACTIVELY, see provenance.
**Severity:** P2 - it silently discards the owner's felt-test sign-offs, which is the one input only
she can produce.

> ### ⚠ PROVENANCE - MINTED AFTER THE WORK, AND THAT IS A PROCESS MISS
> The work was dispatched, implemented and committed on 2026-09-03 without a WO file ever being
> written, so for most of a day the board showed nothing for a change to the board's own evidence
> store. Recorded here on her instruction that **tickets live from the board** and **every new issue
> gets a ticket**. Do not backdate the commit; this note is the pointer.

## The defect

`BOARD.html` stored each validation in `localStorage` under a key scoped to the BUILD:

```js
validationKey = 'eoa-owner-validation:2026.09.03.353742:d706b430b875c42d978eba29e46af2eabc3a0299'
saveValidation() { localStorage.setItem(validationKey, JSON.stringify(validation)); renderValidation() }
```

**Every rebuild minted a new key, orphaning every mark she had made.** She marked tickets Passed and
Validated; the next `python tools/board_build.py` made them invisible. Nothing was corrupted - it was
simply never read again.

## The ruling that shaped the fix - NOT build-scoped

The old comment was right about a *measurement* and wrong about a *sign-off*. What she records is
"the wolf routes correctly now" - a judgement about a **fix**, and it does not stop being true because
the CLI committed a doc change. Re-testing 66 tickets per commit is exactly the cost that makes a
person stop marking, and **a mechanism nobody uses closes zero tickets**. Provenance (`at` + `build`)
is kept per-entry instead, so "was this signed off before the current APK?" stays answerable per
ticket rather than being force-answered "all stale" hourly.

## Proof

```
VALIDATIONS_OK 0 recorded, 0 validated, preserved across rebuild - proof/owner-validations.json
BOARD_CHECK_OK 0 unlabeled, 0 status contradictions, mint numbers readable
VALIDATION_ROUNDTRIP_OK
```

Proven RED: stage 4 stubs the record loader back to always-empty (the old behaviour) and REQUIRES
stage 1's assertions to fail - 4 caught. The record is asserted byte-identical after three rebuilds
and after a full `board_build` run. Wired into `tools/regression/checkin_gate.ps1` as stage 1c, and
that file was parse-checked under PowerShell 5.1 (`PS_PARSE_OK`) given its history of not parsing at
all.

## Colourblind-safe by construction

A validated row is marked THREE ways, all server-rendered so they show with JS off: the word
`[X] VALIDATED`, the button flipping to `Validated`, and the row **sinking to the bottom of its group**.
Never hue.

## Also changed

`tools/board_close_validated.py` now reads the durable record FIRST. It previously existed only to copy
Chrome's LevelDB out of the user profile and regex-salvage JSON from raw bytes - which works on one
desktop browser and never on her phone. LevelDB is now a fallback that runs only when the record is
empty. Its close stamp was also hardcoded to `2026-08-27`; it now uses the current date.

## Acceptance

- [x] A mark survives a rebuild; the record is the source of truth.
- [x] An unreadable record aborts the rebuild rather than silently showing zero.
- [x] Oracle proven RED first; roundtrip wired into the check-in gate.
- [x] Colourblind-safe, phone-first, no hue-carried state.
- [ ] ⛔ **Owner exports her marks once** so the record holds real data, then the close pass runs.
