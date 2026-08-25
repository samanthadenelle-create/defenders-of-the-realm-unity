# WORK ORDER 1187 - fifteen PowerShell scripts are one stray character from silently not running

**Status:** FIXED - all 14 files converted to pure ASCII (Phase 1 by the implementing seat, Phase 2 ship chain by the lead), oracle built and registered. Owner felt-close owed; gate evidence below.
**Minted:** 2026-08-25 (CLI lead, main line; banner bumped 1187 -> 1188 in the same edit)
**Silo:** Tooling / gates
**Parent finding:** the 2026-08-25 morning pass, while fixing `tools/verify-dungeons.ps1`

---

## The finding

`tools/verify-dungeons.ps1` has never run. It was recorded as "does not parse - string is missing the
terminator", pre-existing at HEAD. **The script was never wrong.**

Root cause, captured rather than inferred:
- The file is **UTF-8 with NO BOM** (first three bytes `35,32,61`) and contained multi-byte characters
  (`U+2500`, `U+2550`, `U+26A0`, `U+26D4`).
- **Windows PowerShell 5.1 reads a BOM-less file as ANSI**, so those bytes decode into stray
  characters and the tokenizer breaks - reporting the fault at a line far from the real cause.
- Parsed with the bytes decoded as UTF-8, the same file **parses clean**. Proof:
  `Parser::ParseInput($utf8Text)` returns 0 errors while `Parser::ParseFile($path)` returns 2.

Fixed 2026-08-25 by making that one file **pure ASCII** (not by adding a BOM - a BOM is removable by
the next tool that writes the file; ASCII content cannot break).

## ⛔ THE REAL TICKET: it is a class, and five of them are the ship chain

A sweep of every `.ps1` in the repo (excluding `Library/`, `Builds/`, `node_modules/`, `.git/`):

| | count |
|---|---|
| `.ps1` containing non-ASCII | **15** |
| ...of those, carrying a BOM | **0** |
| ...currently failing to parse | 1 (`tools/verify-dungeons.ps1`, now fixed) |
| ...**latent** - parse today, by luck | **14** |

The latent 14 include the **CLAUDE.md section 16 ship chain**:
`tools/r2-ship.ps1` - `morning-ship-chain.ps1` - `overnight-apk-build.ps1` -
`install-apk-to-seeker.ps1` - `distribute-android.ps1`
plus `run-autopilot-fleet.ps1`, `overnight-webgl-deploy.ps1`, `tools/art/verify-runtime-art.ps1`,
`Export-StructureParts.ps1`, `tools/AudioGen/generate-sfx.ps1`, `tools/AudioGen/rip-clips.ps1`,
`.claude/skills/f8-watcher-auto-alert.ps1`,
`.claude/skills/run-defenders/websig-watch-daemon.ps1`,
`.claude/skills/run-defenders/websig-watch-start.ps1`.

⭐ **Why this is a gate problem, not a style problem.** A script that does not parse **never runs**,
and a step that never runs is indistinguishable from a step that passed. That is exactly how
`tools/regression/checkin_gate.ps1` looked like a gate for months without executing a single stage,
and how this script sat dead. ⛔ On the ship chain the failure mode is worse: section 16 exists
because a missed R2 push produces **capsule enemies with no error on screen**, and the push is
performed by one of these scripts.

## Acceptance criteria

1. **All 15 files are pure ASCII** (max byte <= 127), OR carry a UTF-8 BOM - prefer ASCII, and say
   per-file which was chosen and why.
2. ⛔ **Each converted file is proven to still PARSE, non-vacuously.** The check must assert the
   parsed statement count is **> 0** as well as error count 0.
   ⚠ **This criterion is written this way because I broke it this morning**: a truncated file parses
   clean with zero errors and zero statements, and my check reported `PARSES CLEAN` on an EMPTY FILE.
   An assertion that cannot fail on the broken state is decoration
   (`docs/INSTRUMENTATION_STANDARD.md` section 1.4b).
3. ⛔ **File sizes must not shrink materially** during conversion - assert it, do not eyeball it.
4. **A NEW ORACLE makes the class mechanically detectable**, so this cannot silently return:
   it walks every `.ps1` in the repo, and FAILS on any file that is (non-ASCII AND has no BOM) OR
   fails to parse. Emit a distinct marker. Register it so it actually executes.
   ⚠ **An unregistered oracle never runs** - `WandererBubbleLegibilityRegression` sat unregistered
   from 08-14 and was never once executed. Registration is part of this ticket, not a follow-up.
5. Report per-file BEFORE and AFTER: byte count, max byte, parse error count, parsed statement count.

## What NOT to touch

- ⛔ Do NOT change any script's LOGIC. This ticket is encoding only. A behaviour change smuggled into
  an encoding pass would be invisible in the diff noise.
- ⛔ `tools
2-ship.ps1`, `morning-ship-chain.ps1`, `overnight-apk-build.ps1` are contended by
  **WO-1173 and WO-1159 section 5** (held, one sequential seat). ⚠ Coordinate before editing those
  three, or split them into a second phase.
- ⛔ Do not add a BOM to any file that is currently pure ASCII - it is unnecessary and it churns.

## Note for whoever writes the oracle

The repo standard is ASCII-only for TMP strings because non-ASCII renders as tofu on device. This
ticket is the same discipline arriving at the tooling layer for a different reason: not how it
renders, but **whether it runs at all**.

---

## LANDED 2026-08-25

**Phase 1 (9 files) + Phase 2 (5 ship-chain files) - all 14 now pure ASCII, zero BOMs added.**
ASCII was chosen over a BOM in every case: a BOM is removable by the next tool that writes the file;
ASCII content cannot break.

Phase 2, taken by the lead because the five are contended by WO-1173 / WO-1159 section 5 (held).
Encoding only, no logic touched, line counts unchanged:

| file | bytes | max byte | parse errors | statements |
|---|---|---|---|---|
| `tools/r2-ship.ps1` | 7339 -> 7342 | 226 -> 125 | 0 | 24 -> 24 |
| `morning-ship-chain.ps1` | 9774 -> 9775 | 226 -> 126 | 0 | 29 -> 29 |
| `overnight-apk-build.ps1` | 7713 -> 7714 | 226 -> 125 | 0 | 14 -> 14 |
| `install-apk-to-seeker.ps1` | 6864 -> 6860 | 226 -> 126 | 0 | 11 -> 11 |
| `distribute-android.ps1` | 2402 -> 2398 | 226 -> 125 | 0 | 14 -> 14 |

## STOP THE FINDING THAT MAKES THIS BIGGER THAN "A SCRIPT DID NOT PARSE"

**A mis-decoded file can swallow whole statements while reporting ZERO parse errors.**

`.claude/skills/f8-watcher-auto-alert.ps1` read as **3 top-level statements at HEAD and 13 after
conversion**, with **0 parse errors in both readings**. Captured, not inferred, on a four-line repro:

```
ParseFile        errors=1 statements=2
ParseInput(utf8) errors=0 statements=4
```

**Mechanism:** U+1F4CC is `F0 9F 93 8C`, and CP1252 maps `0x93` to **U+201C, a LEFT DOUBLE QUOTATION
MARK** - which PowerShell accepts as a string delimiter. Everything after it is swallowed into a
string literal. `=` (`E2 95 90`) and `||` (`E2 95 91`) inject similar characters.

!! **This is why the statement-count criterion is load-bearing and not pedantic.** An error-count-only
check would have certified that file clean while three quarters of its body was inert - the same
shape as a gate that reports success without proving anything.

!! **And it was aimed straight at section 16:** `STOP` (the block emoji) is `E2 9B 94`, and `0x94` is
**U+201D, a RIGHT double quote**. It sat in `r2-ship.ps1`, `morning-ship-chain.ps1` and
`overnight-apk-build.ps1` - the three scripts that prove content reached the CDN. Those five measured
0 errors and unchanged statement counts, so nothing was being lost there yet; it was latent, and it is
now removed rather than left to chance.

## The oracle

`Assets/Editor/Regression/PowerShellEncodingRegression.cs`, markers
`POWERSHELL_ENCODING_OK` / `POWERSHELL_ENCODING_FAIL`, registered in `DataRegression.cs` (log tag
`[ps1-encoding]`).

Three groups, and the first one is the point: **`[self]` runs FIRST and proves the classifier FAILS a
known-bad byte pattern and PASSES two good ones**, so a broken classifier cannot certify the repo
clean. `[encoding]` walks every `.ps1`; `[parse]` fails on `errors > 0` **or** `statements == 0`.
A zero-file walk is itself a failure, and a launch failure on Windows is a failure rather than a skip.

## Still open

- **`tools/verify-dungeons.ps1` has still never actually RUN.** It now parses (fixed earlier today),
  but parsing is not running. Its first real execution is owed.
- The oracle is compile-proven (`COMPILE_GATE_OK`, 0 `error CS`) but its runtime behaviour is proven
  only by the regression run recorded in the commit that lands this.
