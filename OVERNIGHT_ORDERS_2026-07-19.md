# OVERNIGHT ORDERS — 2026-07-19 (autonomous fix → regress → verify loop)

**Owner directive (2026-07-19 evening, BINDING for the night):** run the audit-remediation loop
autonomously overnight until **every issue is resolved, tested, and backed by a regression script that
validates it**. The owner is away; the CLI runs the whole loop solo (memory: `owner-office-autonomy-web-loop`).

## OWNER EXECUTIVE DECISIONS (2026-07-19 evening — BINDING for the night)
1. **Cadence = PUSH + BUILD all 3 on green.** On a batch whose regressions all pass: commit by lane,
   push to `wip`, and build Seeker + Windows + Web so the owner wakes to testable builds. **Prod untouched.**
2. **Echo copy = APPLY the approved rewrite.** Ship the creative-signed-off essence copy + named souls
   (Aldwin/Elowen/Corvin/Bran/Doran/Maren) + awaken header across all 6 echoes (WO-752 Part A).
3. **Scope = WIRE seams + regressions, DEFER content.** Fix reachable P1 wiring + prove with regressions +
   iterate. Defer L-effort CONTENT (dungeon dressing/generation, dual-economy balance, WO-752 pet tutorial)
   to morning follows — do NOT greenfield content overnight.

## THE LOOP (repeat until the backlog is dry)
For each batch of findings (P1s first, then next-level P1/P2s from `docs/reference/MASTER_BACKLOG_2026-07-19.md`):
1. **Architect the plan** — for each finding: the correct wiring fix (reuse, not greenfield) + the EXACT new
   regression script that proves it (real input -> assert -> `*_OK` marker), written FAIL-BY-DESIGN.
2. **Fire fix lanes in parallel** (edit-only, file-disjoint, §11) + author the regression suites.
3. **Batch-gate once** — editor closed: run the SFX mirror if pending, `CompileGate.Run` -> `COMPILE_GATE_OK`.
4. **Run the NEW regressions** — `DataRegression.RunAll`. Read which suites are GREEN (proven fixed) vs
   FAIL-BY-DESIGN-RED (not yet). **Regression cases must have real detail that matches the fix + tests
   against it** — a green marker must mean the P1 is actually fixed, not a stub.
5. **Iterate** — for any P1 whose regression is still RED, the fix is incomplete: fix + re-verify. Two
   failed attempts on one item -> escalate/log (`logs/debug/`), don't spin a third blind.
6. **When every regression in the batch is GREEN** -> commit by lane (explicit path) + push.
7. **Next batch** — same process on the next-level P1s, then the P2 tiers. Continue overnight.

## GUARDRAILS (do not drop these overnight)
- Every fix ships WITH its regression; no fix is "done" until its `*_OK` marker is green.
- Gate discipline: `COMPILE_GATE_OK` + brace/NUL on every `.cs`; DataRegression baseline stays at 0 true reds
  (FAIL-BY-DESIGN reds are expected transiently and must flip green as fixes land).
- Sole committer, explicit paths, never `git add -A`. Push on green (owner standing OK for the branch).
- L-effort CONTENT (dungeon dressing/generation, dual-economy balance, WO-752 pet tutorial) = flag as
  follows, NOT overnight build-blockers. Wire the reachable seams; don't greenfield content at 3am.
- Builds (Seeker/Windows/Web) are the owner's call at the END, after the regressions are green — not per-batch.
- Canon updates ride the same commit (§15). Morning report: what's green, what iterated, what's flagged.

## MORNING REPORT TEMPLATE
Batches run · P1s resolved (with their green `*_OK` markers) · anything that iterated 2x -> escalated ·
follows deferred (content) · current DataRegression verdict · commits + push state · what the owner must decide.

Boot: this file is the night's execution authority after the START_HERE boot sequence (START_HERE §7).
