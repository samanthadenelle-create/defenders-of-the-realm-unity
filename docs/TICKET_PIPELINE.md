# Ticket Pipeline — QA → CLI → PO (BINDING)

**Status:** BINDING. Established by the owner 2026-06-20. Every CLI/orchestrator session
runs the playtest/bug backlog through THIS pipeline. Role separation is non-negotiable:
no bot does another bot's job.

This is the operating model for processing tickets (F8 playtest captures, reported bugs,
backlog items). It is a realistic end-to-end lifecycle with logged hand-offs for observability.

---

## The roles (clear separation, no overlap)

### ① PO Bot (Product Owner / Verifier) — the bookends
- **Intake:** pull a ticket from the QUEUE, determine its **SILO** (component/team area), and
  route it to QA for analysis.
- **Closure (after deploy):** confirm the reported issue is actually fixed, then **notate the
  ticket CLOSED** with notes.
- In practice the **owner is the PO** — especially the felt/visual confirmation, because
  headless verification cannot judge "feels right." The orchestrator may do PO *intake/routing*,
  but a felt-dependent ticket is only CLOSED on the owner's confirmation.

### ② QA Triage Bot — read-only
- Pull the ticket; **classify NEW FEATURE vs EXISTING (regression/bug).** This gate is critical:
  - **NEW FEATURE** (function not built yet) → it is **NOT a bug-fix**. Route back to PO for a
    spec / Work Order to the dev silo. Do NOT RCA-fix something that was never built.
  - **EXISTING** (built, now broken) → read-only triage: gather details, logs, repro steps →
    conduct **RCA (root-cause analysis)** → push to the CLI Bot.
- **READ-ONLY constraint:** QA/RCA never edit code, never gate, never commit. They read +
  diagnose + hand back the proven cause + bounded fix. (They fan out in parallel — gate-free.)

### ③ CLI Bot — the only writer + gatekeeper
- **Validate** the ticket (reproduce where possible).
- Implement the bounded fix (Write/Edit on the Windows path only — §0).
- **Headless-verify** if feasible: `CompileGate.Run` (`COMPILE_GATE_OK`) + a headless AutoPilot
  fleet / `DataRegression` oracle. Read the data; do not claim fixed on faith (§5/§12).
- **Deploy:** build, commit by explicit path, push (push only on PO/owner OK).
- If validation passes → send the ticket to PO for final verification. CLI is the **sole
  committer** (one `.git` writer — §11).

---

## Ticket lifecycle (the states a ticket moves through)

```
QUEUED → TRIAGED(silo)
   ├─ NEW-FEATURE → ROUTED-TO-PO (spec/WO)            [exits the bug-fix lane]
   └─ EXISTING → RCA'd → IN-CLI → HEADLESS-VERIFIED → DEPLOYED → PO-VERIFIES → CLOSED
```

At every arrow, **log the hand-off** (who → who, why) for observability.

---

## Tooling map (how the roles run here)

| Pipeline element | Concrete tool |
|---|---|
| **Queue** | F8 `break-log.jsonl` flags (the F8 watcher feeds new ones in) + reported bugs |
| **Shared ticket board** | ⛔ **STALE 2026-09-04 - THE TASK LIST IS RETIRED** (owner ruling 2026-08-09), as are Notion (08-08) and Linear (08-09). The live board is **`BOARD.html`, DERIVED from `WorkOrders/*.md` via `python tools/board_build.py`** - the repo IS the source of truth and the board cannot drift. The hand-off log is the WO markdown itself: stage + silo + who-has-it live in the file, so the record and the work are the same artifact. See `CLAUDE.md` §13 and `docs/BOARD.md`. *(Original row, kept per §15: "The Task list (`TaskCreate`/`TaskUpdate`). One task per ticket; metadata `{ticket, type, silo, stage, handoffLog}` IS the hand-off log".)* |
| **QA Triage + RCA** | Read-only agents fanned out in parallel (Explore / general-purpose, no Edit/gate/commit) |
| **CLI** | This orchestrator seat: Write/Edit + `DeNelle.Editor.CompileGate.Run` + `run-autopilot-fleet.ps1` + git |
| **PO** | The owner: intake/route + felt-verify-after-deploy + close |

**Ticket metadata `stage` values:** `queued-needs-QA` · `triaged-NEW-route-to-PO` ·
`rca-blocked-<reason>` · `needs-headless-name` · `in-cli-queue` · `headless-verifying` ·
`deployed-awaiting-PO` · `closed`.

---

## Key principles (binding)

0. **RCA proof SHOWN by data, on every ticket (owner directive 2026-07-08).** Every hand-off —
   board metadata, RESULT file, or a dated RCA doc for anything non-trivial — carries a PROOF
   section: the verbatim captured line(s) with source (Player.log:NNNN / break-log entry /
   FlowTrace tag / .meta field / screenshot name) and one sentence on what each proves. If the
   data doesn't exist yet, instrument first and capture it; the RCA is not done until the proving
   line is quotable. The owner judges the fix's grounding herself — never a narrative-only RCA.
   (Templates: docs/RCA_WEAPON_OFFSETS_2026-07-07.md, docs/RCA_DIALOGUE_DOUBLE_FRAME_2026-07-07.md.)
1. **Role separation — no overlap.** QA doesn't write; CLI doesn't triage-classify; PO doesn't RCA.
2. **Read-only early triage.** QA/RCA agents are gate-free and never mutate the tree.
3. **New-vs-old gate is mandatory.** Classify before any fix. New function → feature WO, not a patch.
4. **Hand-offs go through the board** (⛔ **NOT the Task list - retired 2026-08-09; the board is `BOARD.html`, derived from the WO markdown**) and are **logged** in the WO file itself.
5. **Headless-verify before PO.** Gate + fleet/regression where possible; never "works on my machine."
6. **PO closes, not CLI.** A felt-dependent ticket is CLOSED only on the owner's post-deploy confirm.
7. **One committer** (CLI). Push only on owner OK.

---

*Cross-refs:* `CLAUDE.md` §11 (orchestration), §12 (instrument-don't-guess),
`docs/ARCHITECTURE_PRINCIPLES.md` (HP B2B lens), `docs/INSTRUMENTATION_STANDARD.md`.
