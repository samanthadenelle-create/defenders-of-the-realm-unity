# ⛔ SESSION BOOT GATE — paste this first. Nothing happens until it is answered.

**You are a fresh CLI session inheriting a live, mid-flight operation.**
**Do NOT write, edit, build, commit, spawn an agent, or "just check one thing" until you have
pasted the RECEIPTS below.** Skipping ahead is not a shortcut — it IS the failure being measured.

---

## WHY THIS FILE CHANGED (read once — it is the whole point)

The owner has had to repeat *"did you actually read it?"* **every single session.** Telling a
session to "read carefully" has never worked, because skimming is invisible from the outside: a
session that read nothing and a session that read everything open with the same confident message.

Worse, the previous version of this file was a **frozen 2026-07-03 snapshot** that asked you to
verify commits which no longer exist. So the gate *trained* skimming — you'd check a dead sha, find
it missing, and learn the gate was noise. That is fixed: this file now contains **no literals that
can rot**.

This gate no longer asks you to *confirm* you read. It asks you to **quote facts you could only know
by opening the file** — and those facts change every day. You cannot answer them from memory, from a
previous session, from this file's own text, or by pattern-matching.

> **Any example value written anywhere in this repo's routers is DELIBERATELY treated as stale.**
> If you paste a number that you found printed in a doc rather than looked up at its source, you
> have proven you did not look it up.

---

## THE RECEIPTS — paste all 8. Value + where you got it. One line each.

| # | Question | Where the answer actually lives |
|---|---|---|
| 1 | The **newest** `CANON_GROUND_TRUTH_<date>.md`, and the **HEAD sha it claims** | repo root — **sort by date, open the newest.** Never trust a date printed in another doc |
| 2 | The **actual** HEAD sha now — and does it MATCH #1? | `git log -1 --oneline`. If it does not match, the anchor is behind and you say so |
| 3 | The current **save schema version** | `Assets/_Modules/Core/State/SaveSchema.cs` — quote the line, not a doc's claim about it |
| 4 | The **next free WO number**, and which block | `CLI_LANES_WO_NUMBERS.md` banner — the SOLE authority. Two disjoint blocks are in use |
| 5 | The **EditMode test count**, and whether ANY are red | `Builds/test-results-EditMode.xml` (`total=` / `failed=`) or the newest run log |
| 6 | **One open item** from the newest `docs/HANDOVER.md` ★★ block | that block only |
| 7 | **One thing specced but NOT built** | the newest anchor's OPEN section, or a WO whose Status is READY |
| 8 | The **F8 inbox state** — any unacknowledged capture? | `.claude/skills/run-defenders/f8-check-inbox.ps1` |

**Then** state in ONE sentence what you believe the next priority is — and **WAIT** for the owner
to confirm before touching anything.

---

## THE FOUR WAYS SESSIONS ACTUALLY BREEZE (all four have happened here)

1. **Reading the index instead of the file.** `PROJECT_INDEX.md` naming a doc is not the doc.
   If you did not open it, you did not read it.
2. **Trusting a version/date printed in a router.** `START_HERE.md` once hard-named an anchor that
   was a day stale and **five downstream docs inherited the error**. Sort by date; open the newest.
   A doc naming a specific version is a hint, never a source.
3. **Copying a WO number into a doc.** The banner is the only authority. Copying the number caused
   **five numbering collisions in one day (2026-08-02)**. Point at the banner; never restate it.
   If you mint, bump the banner **in the same edit** — that is the rule that was broken all five times.
4. **Believing a confident claim.** Docs rot between commits; **two files both declared themselves
   the "live anchor" for a full day.** When a doc and the tree disagree, **the tree wins** — and you
   fix the doc in the same breath (CLAUDE.md §15).

---

## THE RULES YOU ARE BOUND BY (answer YES + one line of proof each)

1. **§12 instrument-don't-guess.** No code edit on a non-trivial bug until you can cite a CAPTURED
   line proving the cause. Static reading LOCATES; it never CONCLUDES. No data line = no edit.
2. **§14 F8 first.** While the owner tests, read `logs/f8-inbox/LATEST_CAPTURE.md` BEFORE any
   code-read, agent, or theory. Spawning a code-reading agent before reading the harvested trace is
   the banned failure.
3. **§11 orchestrate, don't solo.** You are the ORCHESTRATOR: fan out file-disjoint agents, then
   batch-gate ONCE and commit by explicit path. You are the SOLE committer and SOLE batchmode hands.
   Agents never gate, never commit.
4. **Gates are MARKERS, not exit codes.** `run-unity-method.ps1` exits 0 on refusals and FAILs.
   Judge by `COMPILE_GATE_OK` / `REGRESSION_OK` / `TESTS_OK` / `UI_CAPTURE_OK` + log freshness.
5. **Open the PNGs.** `UI_CAPTURE_OK` proves a panel RENDERED, never that it looks right. Two broken
   panels reached the owner this week behind green markers.
6. **Never hand-edit a `.unity` scene** (resave-corruption history). Use runtime injectors.
7. **Push only on the owner's word.** She felt-verifies and CLOSES tickets. You do not close them.
8. **Creative picks are HERS.** Names, art, music, balance intent, which recipe/loot/story. You may
   implement a ruling; you may not invent one and bury it in a commit.

---

## WHEN YOU FINISH A PIECE OF WORK

- Update canon **in the same breath** as the change (§15). A state change with no canon update is an
  incomplete change.
- Write `WorkOrders/WORK_ORDER_NNN_*.RESULT.md`. This protocol **collapsed on 2026-08-02** — 31 WOs
  sat marked IMPLEMENTED with no RESULT file. Do not add to that debt.
- Report honestly: a red gate is reported with its output; a skipped step is named. "Done" means
  gated and verified, not "the edit compiled".

---

*This gate is deliberately short — a long gate gets skimmed, which is the problem it exists to solve.
If you are reading this line, answer the 8 receipts NOW. Do not start work and answer later.*
