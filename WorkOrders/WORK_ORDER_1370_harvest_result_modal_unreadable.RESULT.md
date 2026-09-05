# RESULT - WORK ORDER 1370 - The HARVEST RESULT modal does not say what 3000 is, or that anything was lost

**Filed:** 2026-09-04 (board agent, from `docs/reference/READY_RCA_LEDGER_2026-09-04.md` + the WO's appended `RCA re-verified 2026-09-04` block)
**WO status:** FIXED - on the Seeker in build 2026.09.05.355872, awaiting owner felt-test. PO closes (CLAUDE.md s13).
**Caveat:** the WO says the final wording is the OWNER's; the commit shipped wording without recording her OK.

## What shipped

- Commit `f6540db88` (2026-09-04 12:47) - ancestor of HEAD and of `32af7767c` (base of build 2026.09.05.355872).
  Body: "WO-1370 - the harvest modal now puts the resource name and its figure on ONE line and says the word LOST."
- `Assets/_Modules/Core/UI/HarvestOverflowModal.cs:88 public static string BuildBody(...)`; `:100-102`
  `$"{name} storage: {s.Current} / {s.Max}{state}"` (name + figure on one line, `(full)` as text, not tint);
  `:108-110` "...was not added to storage - it is lost." with singular/plural branches; `:114-116` over-cap vs
  full wording; `:120-123` no trailing summary. `:46-63` emits a per-resource FlowTrace with the actual numbers.
- `GameState.cs:59-61` Stone-rides-Food comment untouched, as ordered.

## Suites that pin it

- `[harvest-result-copy]` (`Assets/Editor/Regression/HarvestResultCopyRegression.cs`, new in `f6540db88`), cases
  `[name-with-figure]`, `[loss-is-named]`, `[no-list-tail]` (`:26-30`). Registered `DataRegression.cs:669`.
- `TownBankCapRegression.cs` still pins the `Collected/Uncollected/was not added to storage` literals (preserved at
  `:102`, `:108-110`).
- `Builds/regression.log` (2026-09-04 22:44) line 113715: `REGRESSION_OK 377/377 suites`.

## Device build evidence

- Build 2026.09.05.355872 on the Seeker (installed 22:22); base `32af7767c` has `f6540db88` as an ancestor.
- `logs/f8-inbox/device/live-20260904-095525.png` (Sep 4 09:55) is the PRE-fix screen; no post-fix 2670x1200
  capture exists.

## Owner felt-test (3-5 taps)

1. On build 355872+, let a collector fill past what the store can hold (or fill a store, then harvest).
2. Tap the collector / claim the harvest so the result modal opens.
3. Read the body: each resource is one line - `<Name> storage: <current> / <max>` - with `(full)` as words.
4. Confirm the line "...was not added to storage - it is lost." is present when anything overflowed.
5. Say whether the wording is approved as-is or give the words you want - the copy is yours (WO acceptance).

## Gaps the RCA block names

- A device screenshot of the NEW copy is still owed.
- "Owner has read the new copy and approved the wording" is unrecorded - the ruling stands open.
