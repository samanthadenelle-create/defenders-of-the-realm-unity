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

## 3. GATE HYGIENE — how to read a gate run without being lied to

Standing hygiene for every seat, not just Sunday. Each line below has a proving run behind it; do not
soften them back into "check the marker" shorthand.

| # | Rule | The proving line |
|---|------|------------------|
| 1 | **Never trust the exit code.** `run-unity-method.ps1` exits 0 on refusals and FAILs, and non-zero on runs that did the work. | memory `gates-report-success-without-proving-it` |
| 2 | **Never trust the MARKER alone either — grep the log for `error CS` as well.** A gate can print its OK marker on a log that also carries compiler errors. | 2026-08-09: `Builds/rail-compile.log` printed `COMPILE_GATE_OK :: scripts compiled clean` at line 4226 while five `error CS0103` lines sat at 3157–3895, and Unity then logged *"Scripts have compiler errors"* and exited 1. Marker present, tree red. |
| 3 | **Check the marker, the `error CS` count, the log's mtime and its size — all four.** A stale log from the previous run reads exactly like a pass. | same run: the second invocation produced NO marker at all, which is what a genuinely failing gate looks like |
| 4 | **Attribute every error to a PATH before triaging it as yours.** Parallel lanes share one working tree, so a red gate is often another lane's half-landed file, not your change. `git status --short` first. | 2026-08-09: the rail lane's gate failed twice on `BuildPaletteUI.cs` (`_orientBtn`, then `ShowQuickTabs`) — a different agent mid-edit. Zero diagnostics in the lane's own file. |
| 5 | **A red tree does not disprove YOUR file.** Roslyn compiles the whole assembly and reports every file's errors; if your file draws no diagnostic while its assembly-mates do, it compiled clean. Say exactly that rather than claiming a green gate you did not get. | same run |
| 6 | **One batchmode at a time — the project lock is real.** On *"A 'Unity' editor process is already running"*, WAIT for it. Never kill Unity to take the lock (see the `.git/index.lock` duel lesson, `CLAUDE.md` §11). | same session |
| 7 | **The markers are DISTINCT per entry point.** `DataRegression.RunAll` → `REGRESSION_OK <n>/<n> suites`; `RegressionSuite.RunAll` → `CHECKIN_SUITE_OK`; `SessionRegression.RunAll` → `SESSION_GUARDS_OK`. Read the count off the marker; never restate it. | `CLAUDE.md` §8 |

**Sunday addition to §1:** step 2 (git/WO reconciliation) also re-runs the gates from a QUIET tree — no
other lane mid-edit — so the week's green is a real green and not one lane's errors masking another's.

---

## 4. THE ONE-WEEK VALIDITY THRESHOLD (owner, 2026-08-09, BINDING)

**Verbatim:** *"Anytime we are considering validity of ticket, please use time frame as more of the
reason to validate if its true"* + *"anything more than a week out i would verify before taking at
face value."*

**The weekly cadence in §1 is what makes this threshold meaningful.** Docs are brought up to date every
Sunday, so anything dated more than a week back predates the last refresh — it was written against a
tree that has since been swept.

| Age of the artifact | How to treat it |
|---|---|
| **Within 7 days** | May be taken at face value. |
| **Older than 7 days** | **VERIFY AT SOURCE** before relying on, quoting, or acting on it. |

Rules of application:

- **Older than a week means UNPROVEN, not wrong.** The threshold is a tripwire for a re-read.
  **Nothing is ever closed on age alone.**
- **The date beats the `Status:` line.** A `Status: READY TO IMPLEMENT` written weeks ago is the
  weakest signal in the file — it is written once and almost never updated.
- **Always state the age.** Cite dated artifacts as "*`<path>`, dated `<date>` — N days old*", so the
  reader can apply the threshold themselves. Give BOTH the doc's own dated header and its last
  git-commit date (`git log -1 --format=%ad -- <path>`) when they disagree.
- **When two docs conflict, the NEWER wins by default.** Banner the older; do not re-litigate contents.
- **Verified means opened.** Mark every aged claim explicitly `VERIFIED` (naming the file:line checked)
  or `NOT VERIFIED`. Never launder an unverified aged claim as fact.
- **Caveat that costs us if forgotten:** a ticket can be old because it was DEPRIORITISED, not because
  it was overtaken. Age is strong evidence about *state* ("this is how the system works") and weak
  evidence about an *unfixed defect* — defects do not expire, they wait. Verify, do not discard.

**This is also step 1 of the sweep:** anything in the §1 load-bearing set that is more than 7 days old
is, by definition, due for verification this week — that list IS the week's refresh queue, oldest
first, whether or not a specific contradiction has been spotted in it yet. Refresh **the most important
flow** alongside it, so the docs describing how the game is actually played never fall behind the code.

*Proof this was needed (2026-08-09): 14 `CANON_GROUND_TRUTH_<date>.md` sat at repo root against a
"keep exactly ONE" rule (`RULES.md:564`) — one of them documented as actively INVERTED
(`SESSION_CANON_LOADER.md:396`) yet sitting beside the live anchor; and
`docs/BUILD_HUD_RECONCILED_SPEC_2026-07-14.md` still read `Status: READY TO IMPLEMENT` while
specifying a build screen deleted that same day. Date alone resolves every one of them.*

---

*Maintained by the CLI/orchestrator. The Sunday sweep is the guarantee that "what is done" is always
answerable from stored, known state — never reconstructed from memory.*
