# CLI SESSION PLAYBOOK - the exact sequence, with a receipt at every step (owner directive 2026-09-05)

**Owner, verbatim: "create one script that outlines for every CLI the exact steps I had you take and ensure they do not
skip or skim to be useful."** This file is that script. It is EXECUTED, not read. Every step ends with a **RECEIPT**:
a line you post, built from something you measured THIS session (a command's output, a marker on a fresh log, a file
opened at source). A receipt you cannot fill is a STOP, not a box to skip. Summarising this file, or posting a receipt
from memory, is the failure this file exists to catch (CLAUDE.md s11B). Nothing in here carries a number that can rot -
every value is read live from the command beside it (SAMANTHA.md rule).

The night this was forged (2026-09-05): eleven work orders landed on one tree in one evening with zero red commits,
because every step below was run in this order and every claim was measured before it was said.

---

## PHASE 0 - BOOT (before you touch anything; the same every session)

| # | Do exactly this | RECEIPT you post |
|---|---|---|
| 0.1 | Open and READ TO THE END, in this order: `START_HERE.md`, `SAMANTHA.md`, `PREFLIGHT_GATE.md`, `KEY_FACTS.md`, the NEWEST `CANON_GROUND_TRUTH_*.md` (sort by date, do not trust an example), `SESSION_CANON_LOADER.md`, `CLAUDE.md` s0-s16, `docs/HANDOVER*.md` newest, `docs/CLI_OPERATIONS_RUNBOOK.md`, auto-memory `MEMORY.md` index. | `BOOT_READ <n> files, last line of each quoted` - one quoted last line per file. A file whose last line you cannot quote was not read to the end. |
| 0.2 | Post the eight SAMANTHA receipts: HEAD (`git log -1 --oneline`), ahead-of-origin count, `SaveSchema.CurrentVersion` read at source, the WO banner row (`CLI_LANES_WO_NUMBERS.md`, first `RECONCILED` line), EditMode xml stamp, `f8-check-inbox.ps1` output, `git config core.hooksPath`, device `adb shell dumpsys package <pkg> | findstr versionName` (say "no device" if none). | `SAMANTHA_RECEIPTS 8/8` + the eight values. Name every MISMATCH between a doc and the tree as a finding (tonight: the anchor was two days behind HEAD; a WO said "building to the device now" for a build that never existed). |
| 0.3 | `docs/MASTER_CATALOG.md` + the `docs/MASTER_CATALOG/<area>.md` for every area you will touch. Gate A of `PREFLIGHT_GATE.md`: YES + one-line proof per item, out loud. | `GATE_A <n>/<n> YES` with the proofs. A "probably" is a NO. |
| 0.4 | Regenerate the board: `python tools/board_build.py`. Read the READY set from the WO Status lines, not from a doc's count. | `BOARD_CHECK_OK ... 0 status contradictions` line pasted; READY count as the rule of record. |
| 0.5 | STOP. Post the receipts + findings + ONE sentence naming the next mechanical step. **Wait for the owner's go.** | `WAITING_FOR_GO` |

## PHASE 1 - MODEL ROUTING (owner ruling 2026-09-05, every session)
- This seat is the DIRECTOR (Fable): it reads, decides, gates, verifies, commits, writes specs and rulings.
- Every dispatched lane is a **non-fork** `Agent` with `model: "opus"` (a fork ignores the override). Lanes are
  edit-only or read-only; they never run Unity, never commit, never stash.
- Large coding work goes to the **Codex dev lane** as a detailed work order + a `BATCH_STATE.md` PART (Phase 4).
- RECEIPT for every lane you launch: `LANE <name> model=opus files=<disjoint list>`; two lanes never share a file.

## PHASE 2 - SEE THE TRUTH BEFORE YOU DESIGN
| # | Do | RECEIPT |
|---|---|---|
| 2.1 | If the ticket is visual and the phone is on USB: `adb shell screencap -p /sdcard/x.png` + `adb pull`; OPEN the PNG (Read tool) and SEND it to the owner (SendUserFile). The device is the truth, the doc is hearsay. | `DEVICE_FRAME <path> opened, build=<versionName>` |
| 2.2 | Ask the owner the decisions that are hers with `AskUserQuestion` in the SAME turn (target shape, order of work). Never bury a question in prose; never guess a ruling. | `RULINGS <n> recorded` - quote each answer. |
| 2.3 | Explore with READ-ONLY Opus lanes, in parallel, each with ONE question: (a) the code that builds the screen and its VMs, (b) every regression pin and canon ruling on it, (c) reusable precedents (card builders, icon resolvers, bars, formatters). Then ONE design lane that turns the facts into an implementation plan with file:line citations. | `EXPLORE 3/3 back, DESIGN 1/1 back` + the three or four load-bearing line numbers you re-verified yourself with grep. |
| 2.4 | Call the advisor before committing to the plan; write the plan; ExitPlanMode. | `PLAN <path> approved` |

## PHASE 3 - CLOSE THE TREE FIRST (never build on a dirty tree)
| # | Do | RECEIPT |
|---|---|---|
| 3.1 | Inventory everything unfinished: dirty files (`git status --short`), orphan lane patches in the previous session's scratchpad (`git apply --check` each), lane worktrees (`git worktree list`), stashes. Each becomes a lane or a finding. | `INVENTORY dirty=<n> patches=<n> worktrees=<n> stash=<n>` |
| 3.2 | Dispatch Opus lanes to finish each item, file-disjoint. Apply clean patches yourself. **After EVERY `git apply --3way`, run `git reset` at once** (it stages; a later commit sweeps the leftovers in - happened twice on 2026-09-05). | `APPLIED <patch> ; git reset done` |
| 3.3 | Brace + NUL check EVERY dirty `.cs` (the python one-liner in CLAUDE.md s1). | `BRACES <n>/<n> OK NUL 0` |
| 3.4 | Gate, in this order, judging MARKERS on FRESH logs, never exit codes: `.\run-unity-method.ps1 -Method DeNelle.Editor.CompileGate.Run -LogName c<n> -ExpectMarker COMPILE_GATE_OK` (+ grep `error CS` = 0); `... DataRegression.RunAll -LogName r<n>` -> `REGRESSION_OK <n>/<n>`; `... UICaptureLaunch.RunCaptureHeadless -LogName cap<n> -ExpectMarker UI_CAPTURE_OK`; `... RunRegisteredSecondaryCaptureHeadless` -> `REGISTERED_SECONDARY_CAPTURE_OK ... touch=clean`; plus the area capture (`RunManageOperationalCaptureHeadless`, `RunAdaptiveHudCaptureHeadless`, ...) for the screen you changed. | One line per marker with the log name and mtime. A missing marker on a fresh log is a FAIL. |
| 3.5 | On any RED: read the failure text from the log FIRST. Classify: (a) an oracle that must move WITH a ruling -> you re-point it yourself (sanctioned); (b) a code defect -> a fix lane; (c) a stray file (a harness worktree, an encoding sweep hit) -> remove it. Re-gate. | `RED <suite> -> <a/b/c> -> fixed, re-gated <marker>` |
| 3.6 | OPEN the frames for every visual change (Read the PNG). A green marker is not a look. On the night this was written, green captures still showed a spoils line culled by TMP, a chip overflowing at 1920, a raw job id on a player screen, a door under the touch floor. Each became a fix lane. | `FRAME <png> opened: <one sentence of what it shows>` per frame |
| 3.7 | Commit ONE lane per commit by EXPLICIT PATH: `git reset` -> assert `git diff --cached --name-only` is EMPTY -> `git add <exact paths>` -> commit message via a file + `git commit -F` (trailers per CLAUDE.md) -> `git show --stat HEAD` and read the file list. The WO `**Status:**` flip + the `.RESULT.md` go in the SAME commit. `ProjectSettings.asset` never rides a lane commit. | `COMMIT <hash> <n> files - <WO>` with the file count matching your add list |
| 3.8 | Regenerate the board; commit the board/courier files as their own commit. | `BOARD_CHECK_OK` + `COMMIT <hash>` |

## PHASE 4 - THE CODEX DEV LANE (owner couriers; every hand-back is a CLAIM)
| # | Do | RECEIPT |
|---|---|---|
| 4.1 | Mint the WO from the banner (`CLI_LANES_WO_NUMBERS.md`), bump the banner IN THE SAME EDIT. The WO carries: the owner's words verbatim, the measured defect, the target, the architecture ruling (what to copy, what NOT to generalise), the pin list with file:line, the VM field list verbatim, the lane split (file-disjoint), the RED-first suite spec with a revert recipe per case, what is absorbed from other WOs, not-in-scope. | `WO-<n> minted, banner -> <n+1>` |
| 4.2 | Append a PART to `BATCH_STATE.md` (append-only): base commit hash (a CLEAN HEAD, never today's dirty tree), the LOCKED file list from `git status --short -- Assets/`, assignments in priority order, HELD tickets, rules that bite, hand-back format. Record every ruling you make for the lane IN THE FILE (a sub-section), never only in chat. | `PART <n> appended, base=<hash>` |
| 4.3 | When the owner relays a hand-back: read the section in `batch_results_state.md` to the end. Measure each worktree: `git -C <wt> diff --stat HEAD`, brace/NUL on every `.cs`, `git -C <wt> diff HEAD --binary --output=<patch>`, `git apply --check --3way <patch>` on main. Canonical JSON twins: byte-identical, LF/CR counts unchanged except the authored lines, no BOM. | `HANDBACK <WO> stat=<+/-> braces OK NUL 0 apply-check OK twins identical` |
| 4.4 | Dispatch a READ-ONLY Opus reviewer per hand-back against the WO's pin list, the asmdef edges, the colourblind law, ASCII, touch floor, per-frame traces, and every suite that pins the touched files. Its findings are your rework list. | `REVIEW <WO> findings=<n> blockers=<n>` |
| 4.5 | Write the rework request as the next PART sub-section (numbered items, file:line, what you accept as-is); the owner couriers it. Repeat 4.3-4.4 on the rework. | `REWORK <WO> <n> items -> recheck all PASS` |
| 4.6 | Apply, `git reset`, add the suite registration lines yourself (`DataRegression.cs` is lead-owned), re-point any oracle that moves WITH a ruling, then Phase 3.3-3.8 in full. Post the landed hash back into the PART so the lane rebases. Send the owner the opened frames. | `LANDED <WO> <hash>; PART updated` |

## PHASE 5 - INSTRUMENT, NEVER GUESS (when a frame or a log is wrong)
- Read the captured `[Flow:*]` lines FIRST (`Builds/cap<n>`), then classify data-empty vs built-but-invisible vs
  threw-and-skipped, then fix THAT. Tonight: the raid spoils line was built AND painted per the log and still absent
  from the PNG - the row was culled by TMP because the band was 22.7 px for a 22 pt line. A static theory would have
  "fixed" the VM.
- RECEIPT: `CAUSE <system> proven by <log>:<line> - <one sentence>` before any edit lands.

## PHASE 6 - SHIP TO THE DEVICE (after the tree is closed and committed)
| # | Do | RECEIPT |
|---|---|---|
| 6.1 | Read the commit charge (`Get-Counter '\Memory\Committed Bytes','\Memory\Commit Limit'`). If it is within ~15 GB of the limit, the player build will be OOM-killed: tell the owner a reboot is needed and STOP here - only she reboots. Write the handover doc BEFORE the reboot (this session dies with it). | `COMMIT_CHARGE <used>/<limit> GB -> <build / reboot first>` |
| 6.2 | `Start-Process powershell -ArgumentList '-File .\overnight-apk-build.ps1 -Tester'` (detached). Watch `Builds\overnight-apk-status.txt`: `SCHEMA_PARITY_OK` -> `APK_OK` -> `R2_PARITY_OK` -> `APK_DONE`. Retry once on a kill. | the four markers, pasted |
| 6.3 | `& .\install-apk-to-seeker.ps1 -Build:$false -Install:$true` (direct call; the default deletes the APK). `adb shell dumpsys package <pkg> | findstr versionName`. | `INSTALLED <versionName>` |
| 6.4 | Screencap every changed screen on the phone; OPEN them; SEND them to the owner. `.\distribute-android.ps1 -Notes "..."`. | `DEVICE_FRAMES <n> sent; FIREBASE <release id>` |
| 6.5 | Flip every landed WO to FIXED with the build number; regenerate the board; commit. **Never push** - the production push is the owner's, via `publishing/SUBMIT_CHECKLIST.md` as written. | `BOARD_CHECK_OK`, `COMMIT <hash>`, `NOT_PUSHED` |

## PHASE 7 - CLOSE THE SESSION
| # | Do | RECEIPT |
|---|---|---|
| 7.1 | Write `docs/HANDOVER_<date>_<slot>.md`: what landed (hash table), what is NOT on the device, the exact resume steps, the dev-lane state, the rulings queue for the owner, the process findings. Commit it. | `HANDOVER <path> <hash>` |
| 7.2 | Write memories for every owner ruling and every process lesson (one file, one fact; index line in `MEMORY.md`). | `MEMORY <n> written` |
| 7.3 | Post the closing summary: hashes, markers, what the owner must do next (reboot / rulings / felt-test). | `PLAYBOOK_OK <receipts posted>/<receipts required>` - if the two numbers differ, say which receipt is missing and why. |

---

## STOP RULES (any one of these means you have NOT earned the next step)
1. A receipt you would have to write from memory instead of from output you can paste.
2. "Probably", "should be", "I believe", "looks like" anywhere in a claim (CLAUDE.md s11B).
3. A marker judged by exit code, or read from a log older than the run.
4. A green capture whose PNG you did not open.
5. `git diff --cached --name-only` non-empty before a `git add`.
6. A commit whose `git show --stat` lists a file you did not name.
7. A Codex hand-back applied without a read-only review, or a review finding fixed without a re-gate.
8. A ruling made in chat and not written into the WO / BATCH_STATE / a memory.
9. A build started with the commit charge near the limit, or with a dirty tree.
10. Skipping a phase because "the owner is waiting" - she said: deliver QUALITY, not fast.
