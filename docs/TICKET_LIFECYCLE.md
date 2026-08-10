# TICKET LIFECYCLE — the runbook, from "someone reported a problem" to "the ticket is closed"

**Audience:** every seat — PO, QA, UI, CLI, every spawned agent.
**Status:** practice doc, sourced from the tree 2026-08-09. **Part 2 is a RECOMMENDATION, not canon.**

**This file owns NO rules.** Every step below is a pointer to the doc that owns it. If a step and its
source ever disagree, **the source wins and this page is the bug** — fix it in the same breath
(`CLAUDE.md` §15). This is the same law `RULES.md` states about itself, and for the same reason: this
repo's most-proven failure mode is a **stale copy**. Five canon conflicts were closed on 2026-08-09
and *every one of them was a copy* — a copied WO number, three competing boards, a hardcoded root, a
six-row assembly table, a stale deploy banner. So this page copies nothing it can point at.

What `RULES.md` is to the rules, this page is to the **sequence**: `RULES.md` answers *"what am I not
allowed to do?"*; this answers *"what do I do next, and how do I know I did it?"*

---

# PART 1 — THE RUNBOOK

41 steps in 8 phases (0–7). **Every step is answerable YES or NO.** "Probably" is NO. "I think so" is NO.
A step you cannot answer YES to has not been done — go back and do it, or name the NO and stop.

Where a step has a command, the exact command is given. Run PowerShell from the repo root
(`D:\eoa` on this machine — **the root is machine-dependent, never hardcode it into a doc**;
`RULES.md` C-3 has two binding docs naming a root that does not exist here).

---

## PHASE 0 — SESSION PRECONDITIONS (once per session, before any ticket)

**1. Canon loaded.** `SESSION_CANON_LOADER.md`, the newest `CANON_GROUND_TRUTH_<date>.md`, and the
`docs/MASTER_CATALOG/<area>.md` for the area the ticket touches — read from the CODE, not comments.
→ `CLAUDE.md` read-first + mandatory-first-step · `RULES.md` A6–A10

**2. Board regenerated.** Never read a stale `BOARD.html`; never hand-edit it.
→ `docs/BOARD.md` §2.1
```
python tools/board_build.py
```

**3. F8 watcher live and its inbox polled.** The owner is never the bug detector, so captures must
arrive without her asking. → `CLAUDE.md` §14 · `RULES.md` QR-2.11
```
powershell -File .claude\skills\run-defenders\f8-watch-start.ps1
powershell -File .claude\skills\run-defenders\f8-check-inbox.ps1
```

---

## PHASE 1 — INTAKE (PO owns this; the orchestrator may do intake/routing)

**4. The report is captured with an artifact.** A repro, a screen, a stack, an F8 capture, or a log
line — something a second person could act on. **An ambiguous ticket BOUNCES BACK for detail. Never
work blind, and never open a WO to hold a vague complaint.**
→ `CLAUDE.md` §11 · `RULES.md` rule 16

**5. Duplicate / prior-art check done.** Does a WO, a RESULT, or a built system already cover this?
Extend it; never greenfield a duplicate, and never open a second WO for a live one.
→ `PREFLIGHT_GATE.md` A3 · `RULES.md` rule 13
```
Select-String -Path D:\eoa\WorkOrders\*.md -Pattern '<keyword>' -List | Select-Object Filename
```

**6. SILO assigned** — which component/lane owns it (the `CLAUDE.md` §9 parallel lanes).
→ `docs/TICKET_PIPELINE.md` §1

**7. Routed to QA, and the hand-off is LOGGED** (who → who, why).
→ `docs/TICKET_PIPELINE.md` §4, principle 4

---

## PHASE 2 — TRIAGE (QA, READ-ONLY — never edits, never gates, never commits)

**8. CLASSIFIED FIRST: NEW FEATURE vs EXISTING BUG.** This is the gate, and it comes before any
diagnosis. Write the answer as one line **with its evidence** (the id/class/scene that does or does
not exist). → `docs/TICKET_PIPELINE.md` §2, principle 3 · `CLAUDE.md` §13

**9. If NEW FEATURE — STOP HERE.** It is not a bug-fix. It goes back to PO as a spec/WO for the dev
silo. **Do NOT RCA-"fix" something that was never built.** (Skip to Phase 3 as a feature WO.)
→ `docs/TICKET_PIPELINE.md` §2

**10. If EXISTING — the already-harvested capture was read FIRST.** `logs/f8-inbox/LATEST_CAPTURE.md`,
the break-log, the screenshots — **before** any code read, any agent, any theory. Spawning a
code-reader before reading the harvest is the named banned failure.
→ `CLAUDE.md` §14 · `RULES.md` QR-2.12, rule 47

**11. If the proving line does not exist yet, INSTRUMENT and RUN to capture it.** Prefer headless so
you self-serve rather than asking the owner to retest. `FlowTrace` / `Guard` helpers live in
`Assets/_Modules/Core/Diagnostics/`. → `CLAUDE.md` §12 · `docs/INSTRUMENTATION_STANDARD.md`

**12. The proving line is QUOTABLE.** The RCA carries a PROOF section: the verbatim captured line,
its source (`Player.log:NNNN` / break-log entry / `[Flow:*]` tag / screenshot name), and one sentence
on what it proves.
> ⚠ **A WO that says "probably X" or "likely caused by Y" HAS NOT BEEN TRIAGED.** Static code-reading
> LOCATES candidates; it never CONCLUDES. An inferred root is a guess, and a guess does not earn the
> edit. Send it back for data.

→ `docs/TICKET_PIPELINE.md` §0 · `CLAUDE.md` §12 · `PREFLIGHT_GATE.md` B9

**13. The failure is CLASSIFIED from the trace** — *data-empty* vs *built-but-invisible* vs
*threw-and-skipped* — before anyone touches code. → `CLAUDE.md` §12.3 · `PREFLIGHT_GATE.md` B10

**14. Two failed fix attempts on the same issue → STOP and escalate with logs** to `logs/debug/`.
Do not solo-iterate a third time. → `PREFLIGHT_GATE.md` B11 · `RULES.md` rule 51

---

## PHASE 3 — MINT THE WORK ORDER

**15. The number came from the `CLI_LANES_WO_NUMBERS.md` banner. Nothing else.** Not the filesystem
max, not a backlog doc, not a handover, **not a number copied from any other file** — every copy goes
stale. Two disjoint blocks are in use so the seats mint in parallel; **read your seat's block and its
next-free number off the banner** (do not trust a range restated elsewhere — that restatement is open
conflict `RULES.md` C-1). → `CLAUDE.md` §2 · `RULES.md` rules 62–64

**16. Your seat's banner row was bumped in the SAME EDIT as the mint.**
> ⚠ **The mint written to disk without the banner bump IS the collision.** That is what broke five
> times in one day on 2026-08-02, including by the CLI. Collisions resolve
> first-on-disk-and-referenced-wins.

→ `CLAUDE.md` §2 · `RULES.md` ★ #2

**17. The file exists at `WorkOrders/WORK_ORDER_<n>_<short_name>.md`.** One WO per file; the number in
the filename is what the board joins on. → `CLAUDE.md` §2 · `docs/BOARD.md` §3a

**18. The FIRST `**Status:**` line carries exactly one CANONICAL KEYWORD.** The parser reads the first
`**Status:**` line by keyword priority, left to right. **The keyword vocabulary is owned by
`docs/BOARD.md` §3b — read it there; it is not restated here, because a second copy of a vocabulary is
how a parser and a doc start disagreeing.**
> ⚠ **`Unlabeled` is a DEFECT in the WO file, not a category.** It means the row cannot be bucketed
> and silently drops out of every real query. Nuance goes *after* the keyword, never instead of it —
> `DONE (pending felt-verify)` buckets correctly; `DELIVERED — defect pass open` is Unlabeled.

→ `docs/BOARD.md` §3b · `RULES.md` rule 66

**19. The header carries provenance.** Follow the shape the live WOs already use
(`WORK_ORDER_936`, `_937`, `_1010`, `_1011` are the reference examples):
`**Minted:** <date> (<seat>) — number from the CLI_LANES_WO_NUMBERS.md banner (bumped <n> → <n+1> in
the same edit)`, plus `**Lane:**`, `**Provenance:**` (who reported it / which ruling), and
`**Depends on / anchors:**`. The Minted line is the seat's public attestation that step 16 happened.

---

## PHASE 4 — WRITE THE BODY (what makes a WO implementable by someone else)

**20. FILES TO TOUCH are named by path.** Not "the build HUD" — `Assets/_Modules/Village/BuildMode/
BuildHudController.cs`. This is also what makes file-disjoint lane assignment possible in Phase 5.
→ `CLAUDE.md` §2

**21. ACCEPTANCE CRITERIA are checkboxes, each independently verifiable, and each names the thing that
PROVES it** — a marker, a named regression, a capture PNG, or "the owner felt-verifies". A criterion
no one can check is a wish. → `CLAUDE.md` §2, §10

**22. WHAT NOT TO TOUCH is stated explicitly** — adjacent files, other WOs' scope, frozen catalog rows,
existing assertions. This is what keeps parallel lanes from colliding. → `CLAUDE.md` §2, §9

**23. Binding constraints are POINTED AT, not copied.** UI work → `docs/UI_PLAYBOOK.md`. Instrumentation
→ `docs/INSTRUMENTATION_STANDARD.md`. Architecture → `docs/ARCHITECTURE_PRINCIPLES.md`. Quote the rule
number, not the rule.

**24. The board was regenerated and the new row landed in the bucket you intended.**
→ `docs/BOARD.md` §5
```
python tools/board_build.py --check
```

---

## PHASE 5 — IMPLEMENT

**25. Roles are respected.** **UI never writes or edits `.cs` — no exceptions**; it produces RCA, specs,
narrative, mockups. **CLI writes and build-verifies ALL code, owns batchmode, and is the SOLE
COMMITTER.** **PO closes** (Phase 7). → `CLAUDE.md` §2 · `docs/TICKET_PIPELINE.md` §1–§3 ·
`RULES.md` rules 84–89

**26. The deep work was DELEGATED, on FILE-DISJOINT LANES.** Read-only diagnosis agents are gate-free —
fan out many. Edit-only implementation agents get disjoint silos (`CLAUDE.md` §9); same-file work is
ONE agent. `VillageSceneBuilder.cs` is a serialization bottleneck — one toucher at a time. The lead
orchestrates; it does not solo-charge. → `CLAUDE.md` §11 · `PREFLIGHT_GATE.md` A4

**27. Agents were told NOT to gate and NOT to commit.** The single Unity gate and the single committer
are the coordination point, held by the orchestrator — that is *why* no agent stands idle.
→ `CLAUDE.md` §11

**28. Instrumentation was written IN as the code was authored** — flow entry, every branch taken, every
fallback, every resolve, the render/commit seam; `Guard.TryEach` on anything building N items; no
`catch` that swallows without logging. → `docs/INSTRUMENTATION_STANDARD.md` §2, §3 · `RULES.md` rules 35–37

**29. Brace balance + NUL scan passed on every `.cs` touched**, before reporting anything.
→ `CLAUDE.md` §1

---

## PHASE 6 — GATES (nothing is "done" before this phase completes)

**30. Unity is CLOSED.** Batchmode takes the project lock; both runners refuse with exit 3 otherwise.
→ `CLAUDE.md` §3 · `RULES.md` QR-3.2
```
Get-Process Unity -ErrorAction SilentlyContinue
```

**31. COMPILE GATE — run it, then verify the MARKER *and* grep the log for `error CS`.**
> ★ **THE MARKER IS THE EVIDENCE, NEVER THE EXIT CODE.** `run-unity-method.ps1` judges from log text,
> not from a marker, so it **can exit 0 on a run that refused or FAILED**. Check the marker, the log's
> freshness, and its size.
> ⚠ **KNOWN DEFECT (2026-08-09, fix in flight): `COMPILE_GATE_OK` can print a FALSE GREEN over a broken
> runtime assembly.** Until that fix lands, the marker alone is NOT sufficient — **both** commands below
> must be satisfied: the marker present, and zero `error CS` lines.

→ `CLAUDE.md` §8 · `RULES.md` QR-3.1, QR-3.3, rule 79 · memory `gates-report-success-without-proving-it`
```
powershell -ExecutionPolicy Bypass -File .\run-unity-method.ps1 -Method DeNelle.Editor.CompileGate.Run -LogName gate.log
Select-String -Path .\Builds\gate.log -Pattern 'COMPILE_GATE_OK'
Select-String -Path .\Builds\gate.log -Pattern 'error CS' | Select-Object -First 20
Get-Item .\Builds\gate.log | Select-Object LastWriteTime, Length
```

**32. REGRESSION — the full oracle set, read off its own DISTINCT marker.** The three entry points emit
three different markers on purpose; do not read one as another, and **never restate a suite count from
a doc — read it off the marker.** → `CLAUDE.md` §8 · `RULES.md` QR-3.4, QR-3.8, rules 80–81
```
powershell -ExecutionPolicy Bypass -File .\run-unity-method.ps1 -Method DeNelle.Editor.DataRegression.RunAll -LogName data-regression.log
Select-String -Path .\Builds\data-regression.log -Pattern 'REGRESSION_OK|REGRESSION_FAIL'
```
Full pre-commit battery (static gate → compile → DataRegression → check-in battery → EditMode →
PlayMode), when the change warrants it:
```
powershell -ExecutionPolicy Bypass -File .\tools\regression\checkin_gate.ps1
```

**33. UI WORK — capture it, then OPEN THE PNGs.**
> ★ **Compile-green never proves a panel looks right, and neither does a green regression count.**
> `WORK_ORDER_1010_build_ui_carousel_minimize.RESULT.md` §3: **nine defects, found only by looking at
> pixels, none by any gate**, with 132 suites green throughout. Counting the PNGs is not looking at
> them. Check that file sizes DIFFER between states that should look different — the identical-size
> tell is visible in the directory listing before the wrong picture is.

Three markers must all be green, and the method matters (`RunCapture()` writes zero PNGs in batchmode).
The failure modes of a capture — stale canvas, unhydrated data, wrong geometry, `-nographics` black
rectangles — are catalogued in `docs/UI_PLAYBOOK.md` §13. Read it before trusting a green capture.
→ `docs/UI_PLAYBOOK.md` §13 · `RULES.md` QR-3.7, rule 83 · memory `headless-screenshot-verify-ui-before-build`
```
powershell -ExecutionPolicy Bypass -File .\run-unity-method.ps1 -Method DeNelle.Editor.UICaptureLaunch.RunCaptureHeadless -LogName ui-capture.log
Select-String -Path .\Builds\ui-capture.log -Pattern 'UI_CAPTURE_OK|UI_CAPTURE_FIDELITY_OK|UI_GEOMETRY_OK'
Get-ChildItem .\Builds\ui-capture\*.png | Select-Object Name, Length, LastWriteTime
```
Then **open every PNG, at every target size.**

---

## PHASE 7 — CLOSE

**34. The `.RESULT.md` is written by the seat that VERIFIED the work** — never fabricated to clear
debt — at `WorkOrders/WORK_ORDER_<n>_<short_name>.RESULT.md`. It states, honestly and per criterion,
what is DONE / PARTIAL / NOT VERIFIED; the commits; the gate markers as printed; and the PROOF lines.
`WORK_ORDER_1010_*.RESULT.md` is the reference example — including its §5 "corrections the author owes
the record", which is what an honest RESULT looks like.
> ⚠ **A `.RESULT.md` forces the Done bucket regardless of the status line.** If the work is partial,
> say so loudly in **both** the status line and the RESULT body — the board will call it Done either
> way, so the file has to carry the caveat the bucket cannot.

→ `docs/BOARD.md` §3b · `RULES.md` rule 68

**35. The `**Status:**` line was flipped IN THE SAME COMMIT as the work and the RESULT.**
> A completed WO whose file still says READY is **a lie the board will faithfully render**. Status
> hygiene is not paperwork — it IS the board. A deferred flip is a board that lies until later.

→ `docs/BOARD.md` §2.2 · `RULES.md` rule 67

**36. Canon was updated in the SAME BREATH** — the load-bearing doc that the change makes wrong, or a
one-line dated `STALE:` flag naming what is now wrong. A state change with no canon update is an
incomplete change. → `CLAUDE.md` §15 · `RULES.md` ★ #5, rule 94

**37. Committed by EXPLICIT PATH, by the ONE committer.** Never `git add -A`; never blind-replace a
file; review every diff (the tree is shared and mount-garble is live, `CLAUDE.md` §0). Write the commit
message to a file and use `-F` — here-strings and `Set-Content -Encoding utf8` both corrupt it
(memory `powershell-commit-message-encoding`). → `CLAUDE.md` §11 · `RULES.md` rules 72–76
```
git add -- <path1> <path2>
git commit -F <message-file>
```

**38. Board regenerated and `--check` exits 0.** → `docs/BOARD.md` §5
```
python tools/board_build.py --check
```

**39. PUSHED ONLY AFTER** the owner felt-verifies (felt/gameplay) or a regression proves it
(data/logic) — "push the ones that passed." → `CLAUDE.md` §11 · `PREFLIGHT_GATE.md` C15

**40. THE PO CLOSES — NOT THE CLI.** The ticket is CLOSED when the owner confirms post-deploy that the
reported problem is actually gone. **Headless markers cannot judge feel, geometry or orientation** —
that class of defect needs eyes. `WORK_ORDER_1010_*.RESULT.md` states it exactly: *"the WO closes when
the testers who raised it say 'I understood it without help' — not before."*
→ `docs/TICKET_PIPELINE.md` §1, principle 6 · `RULES.md` rules 83, 86

**41. SUPERSESSION — a later ruling marks the earlier item SUPERSEDED IN PLACE. It is never deleted.**
Add a dated `⚠ SUPERSEDED <date>` / `⚠ CORRECTED <date>` banner or an inline note naming what changed
and pointing at the ruling that replaced it, and leave the original text as history. Dated
point-in-time ledgers (RESULT files, dated handovers, dated audits) are **frozen — banner only, never
rewrite the body.** The live examples of the pattern done right: `WORK_ORDER_937` §1's ⚠ CORRECTED
block (which preserves the wrong framing *and* names the 5 defects it would have laundered), and
`WORK_ORDER_1010` §1's ⚠ SUPERSEDED BY D14 (*"Original spec kept for history"*).
> Deleting the superseded text destroys the only record of **why** the ruling exists — and this repo's
> conflicts are almost always resolved by reading which of two statements is older.

→ `CLAUDE.md` §15 · `RULES.md` rules 97–98

---

## THE CHECKLIST (copy-paste)

```
TICKET ______________________________  WO-______  SEAT ______  DATE __________

PHASE 0 — SESSION
[ ]  1. Canon loaded (SESSION_CANON_LOADER + newest CANON_GROUND_TRUTH + MASTER_CATALOG/<area>)
[ ]  2. python tools/board_build.py
[ ]  3. F8 watcher started + inbox polled

PHASE 1 — INTAKE (PO)
[ ]  4. Report has a repro/screen/stack/capture   (ambiguous -> BOUNCE, do not open a WO)
[ ]  5. Duplicate / prior-art checked (extend, never greenfield)
[ ]  6. SILO assigned
[ ]  7. Routed to QA, hand-off LOGGED

PHASE 2 — TRIAGE (QA, read-only)
[ ]  8. CLASSIFIED: NEW FEATURE vs EXISTING BUG, with evidence
[ ]  9. If NEW: STOP -> back to PO as a spec. No RCA, no fix.
[ ] 10. Harvested F8 capture read FIRST (before any code read / agent / theory)
[ ] 11. Instrumented + ran (prefer headless) if no proving line existed
[ ] 12. PROOF: verbatim line + source + what it proves    ("probably X" = NOT TRIAGED)
[ ] 13. Failure classified: data-empty / built-but-invisible / threw-and-skipped
[ ] 14. Two failed attempts -> STOPPED and escalated to logs/debug/

PHASE 3 — MINT
[ ] 15. Number read off the CLI_LANES_WO_NUMBERS.md banner (SOLE authority; never a copy)
[ ] 16. Banner row bumped IN THE SAME EDIT as the mint
[ ] 17. File at WorkOrders/WORK_ORDER_<n>_<short_name>.md
[ ] 18. First **Status:** line carries ONE canonical keyword (docs/BOARD.md §3b). Unlabeled = defect
[ ] 19. Header: Minted(date/seat/bump) + Lane + Provenance + Depends

PHASE 4 — BODY
[ ] 20. Files to touch, by path
[ ] 21. Acceptance criteria as checkboxes, each naming what proves it
[ ] 22. What NOT to touch
[ ] 23. Constraints POINTED AT (UI_PLAYBOOK / INSTRUMENTATION_STANDARD / ARCHITECTURE_PRINCIPLES)
[ ] 24. python tools/board_build.py --check   -> row in the intended bucket

PHASE 5 — IMPLEMENT
[ ] 25. Roles respected (UI writes no .cs; CLI writes + gates + sole committer)
[ ] 26. Delegated on FILE-DISJOINT lanes; same-file work = one agent
[ ] 27. Agents told NOT to gate, NOT to commit
[ ] 28. Instrumentation written IN as authored; no silent catch
[ ] 29. Brace balance + NUL scan on every .cs touched

PHASE 6 — GATES
[ ] 30. Unity CLOSED  (Get-Process Unity)
[ ] 31. COMPILE_GATE_OK marker present  AND  zero 'error CS' in the log  AND log fresh/non-trivial
[ ] 32. REGRESSION_OK <n>/<n> suites   (count read OFF THE MARKER, never from a doc)
[ ] 33. UI: UI_CAPTURE_OK + UI_CAPTURE_FIDELITY_OK + UI_GEOMETRY_OK  ... AND I OPENED THE PNGs
        (file sizes differ between states that should look different)

PHASE 7 — CLOSE
[ ] 34. .RESULT.md written by the seat that VERIFIED, honest per criterion, PROOF included
[ ] 35. **Status:** flipped IN THE SAME COMMIT as the work + RESULT
[ ] 36. Canon updated in the same breath (or dated STALE: flag)
[ ] 37. git add -- <explicit paths>  ;  git commit -F <file>   (one committer, never -A)
[ ] 38. python tools/board_build.py --check  exits 0
[ ] 39. Pushed ONLY after owner felt-verify or a passing regression
[ ] 40. PO felt-verified and CLOSED it  (the CLI does not close)
[ ] 41. Anything this ruling overturned is marked SUPERSEDED IN PLACE, not deleted
```

---
---

# PART 2 — WHAT THE STRUCTURE NEEDS, AND HOW IT SHOULD BE ARRANGED

**Recommendations only. Nothing here is implemented, and several items need an owner ruling — those
are flagged ⚑.** Each recommendation names the observation that motivates it.

---

## 2.1 The layering is already right. It is just not enforced.

The intended shape is sound and worth stating as law:

```
RULES.md            INDEX     one checkable line + a pointer. NO rule text may live here.
    ↓
CLAUDE.md           SOURCE    the binding rules and the deep docs that own each area.
docs/*.md                     Exactly one owner per rule.
    ↓
BOARD.html          DERIVED   generated in 2s from the data. NEVER hand-edited.
    ↓
WorkOrders/*.md     DATA      the unit of work. Frozen after its date; status line stays live.
```

Every failure observed today is a **layer violation**: SOURCE content copied into an INDEX or a DATA
file, where it goes stale with nothing to detect it.

**Recommendation R1 — stamp the layer on every load-bearing doc** (a one-line `**LAYER:** INDEX |
SOURCE | DERIVED | DATA` header), and make the layer imply its rule:
- **INDEX** may contain pointers and one-line statements only — never an explanation, a range, a
  table, or a count.
- **SOURCE** is the single owner of its topic; if two SOURCE docs cover one topic, one of them is a copy.
- **DERIVED** is never hand-edited (already true of `BOARD.html`).
- **DATA** is never rewritten after its date — banner in place (step 41).

The cheap version, if a header on 280 docs is too much: state the four layers once in `RULES.md` and
apply the rule when a doc is touched.

---

## 2.2 ★ Top recommendation: MECHANIZE the anti-copy rule (`tools/canon_check.py`)

**Observation.** Five canon conflicts were closed on 2026-08-09; every one was a stale copy. They were
found because a seat did a full manual read of the corpus — 110 `.md` at the repo root, 174 in `docs/`,
1007 files in `WorkOrders/`. `CLAUDE.md` §15 exists *because* one fleet-scale audit of 1090 files
already had to happen once, and it says "never again at that scale". But the only thing currently
preventing the next one is discipline, and discipline is exactly what failed five times in one day on
the numbering banner alone.

**Recommendation R2 — a 30-line grep-based linter over the load-bearing set, run in the check-in gate
next to `board_build.py --check`.** It does not judge prose; it fails on **retired tokens** — strings
that are known-dead and can therefore only appear in a stale copy:

| Retired token | Why it is dead | Where it still appears |
|---|---|---|
| `Notion` as the live board | owner ruling 2026-08-08 | `docs/HANDOVER.md` §1, `WorkOrders/README.md` |
| `MASTER_PIPELINES_BACKLOG` as numbering authority | `CLAUDE.md` §2 — banner is SOLE | `docs/HANDOVER.md` §1, `WorkOrders/README.md` |
| `Linear` issue as the done-marker | superseded twice over | `CLAUDE.md` §2 |
| `MainCastle_Hall` as the hub | `CLAUDE.md` §7 — hub is `Main_Castle_Overworld` | `docs/HANDOVER.md` §1/§5, `WorkOrders/README.md` |
| `Village.unity` / `OuterWorld.unity` as live | deleted from the tree | `CLAUDE.md` §3, `docs/HANDOVER.md` §2/§4 |
| `C:\EoA` / `C:\eoa` as the repo root | does not exist on this machine | `CLAUDE.md` §0, `PREFLIGHT_GATE.md` B11 |

A doc that legitimately needs a dead token (a frozen ledger, this table) opts out with an explicit
`<!-- canon-check: historical -->` marker — an *unexplained* exemption being indistinguishable from an
oversight is the same principle `UiCaptureCoverageRegression` already applies to `KnownUncapturable`.

**Why this over "be more careful":** it converts the repo's most expensive recurring failure from a
periodic 1090-file audit into a 2-second check that fails at the moment the copy is written. It is the
same trade the board already made against Notion.

---

## 2.3 ★ Second recommendation: enforce the WO header at CREATION, not by sweep

**Observation.** There is **no WO template in the repo** (the only `*TEMPLATE*` file is
`docs/UI_BLINK_TEMPLATE_CANON.md`, unrelated). Consequences, both currently being cleaned up by hand:
- **73 real work orders carry no canonical status keyword** (`WORK_ORDER_937` §1) — swept one at a time.
- **~516 WOs claimed READY** on first board generation (`WORK_ORDER_1011` §4) — swept in slices of 50–100.
- **Five two-seat numbering collisions in one day** (`CLAUDE.md` §2), all from a mint that skipped the
  banner bump.

The good WOs already converge on one header — `936`, `937`, `1010`, `1011` all carry
Status / Minted-with-bump-attestation / Lane / Provenance / Depends, then Findings → Deliverables →
Acceptance criteria → What NOT to touch. That convention exists; it is just not written down or
checkable.

**Recommendation R3 — `WorkOrders/_TEMPLATE.md`, plus two lines of validation in `board_build.py`:**
1. The template is the four live WOs' header, with the Minted line pre-shaped so the banner-bump
   attestation is a fill-in-the-blank rather than an act of memory.
2. `--check` additionally fails when a `WORK_ORDER_*.md` has **no `**Status:**` line at all**
   (distinct from the current Unlabeled defect, which is a *bad* keyword) — so the 73 can never recur.
3. ⚑ **Owner ruling wanted:** should `--check` also fail on a **duplicate WO number**? Duplicates
   exist today (136 and 482 each appear twice, `WORK_ORDER_937` §2), and `WorkOrders/README.md`
   explicitly says *"Number/filename collisions across this folder are expected, not defects"* —
   which directly contradicts `CLAUDE.md` §2's collision rule and WO-937's own acceptance criterion
   that duplicates be **reported**. One of those two statements has to go.

---

## 2.4 ★ Third recommendation: ONE Definition of Done, and one conflict-resolution ledger

### (a) The definition of done is currently in five places

`PREFLIGHT_GATE.md` Gate C (4 items) · `CLAUDE.md` §10 (6 items) · `docs/UI_PLAYBOOK.md` "Before you
say it's done" (~25 items) · `docs/BOARD.md` §2.2 (status + RESULT in the same commit) ·
`docs/TICKET_PIPELINE.md` §5–§6 (headless-verify then PO closes). None of them is wrong; none of them
is complete; and a seat that satisfies any one of them can honestly believe it is done.

**Recommendation R4 — Phase 6 + Phase 7 of Part 1 becomes THE definition of done, and the other four
point at it** rather than restating their fragment. `docs/UI_PLAYBOOK.md`'s list stays as the *UI
specialisation* (it is domain-specific and earns its length), but its **Gates** block should become a
pointer.
⚑ **Owner ruling wanted:** whether this file is allowed to be the DoD anchor, or whether the DoD
belongs in `PREFLIGHT_GATE.md` Gate C with this file pointing there. Either is fine — but it must be
exactly one.

### (b) There is no escalation path when two OWNER statements conflict

`RULES.md` precedence #1 says *"the owner's live ruling beats every document"*. It is silent on the
case that actually occurs: **two owner statements, both live, in conflict** — which happened twice on
2026-08-09. `CLAUDE.md` §7's action-bar section shows the failure mode: a 2026-08-01 owner rule
retired the Queues button, a 2026-08-06 owner ruling reversed it, and the section now has to spend a
paragraph explaining *why the older line was stale for a different reason than you'd think*.

**Recommendation R5 — name the mechanism that already works and make it the standing path.**
`RULES.md`'s `⚠ CONFLICTS BETWEEN SOURCES` ledger is the right instrument, and C-6 / C-7 show the
right shape: the conflict is **stated with both sources**, **no winner is picked by the seat**, and
when the owner rules, the entry is amended in place with `✅ RULED <date> (owner): …` **naming which
document was the stale loser and what was changed**. Formalise exactly that:

1. Any seat that finds two binding statements in conflict **files a C-entry and does not choose**.
   Choosing silently is how a losing statement survives to contradict someone else next week.
2. The owner's ruling is recorded **in the ledger entry**, not only in the doc that was fixed — the
   ledger is the audit trail of *why* the doc says what it says.
3. **Newer beats older only after the ruling is written down.** Until then the conflict is open and
   the work bounces (same rule as an ambiguous ticket, Part 1 step 4).
4. ⚑ **Open C-entries needing an owner ruling today: C-1** (which block the UI seat mints from —
   `CLAUDE.md` §2 says 860–899, the banner says that block is CLOSED and the seat moved to
   1000–1099), **C-2** (which board is *the* board, and whether tickets and WOs share one board or
   two), **C-3** (the repo root), **C-4** (§3 names a deleted scene), **C-5** (what may be stripped
   when a system stabilises). C-1 and C-2 both bear directly on this runbook.

---

## 2.5 What is duplicated and should point instead of copy

Ranked by blast radius. Every one is a **SOURCE-layer fact living in a non-SOURCE file**.

1. **`docs/HANDOVER.md` §1–§5 is the worst offender, and it is in the read-first set.** §1 states
   *"Notion is the live WO board"* and *"WO-numbering authority = `MASTER_PIPELINES_BACKLOG_2026-06-06.md`,
   not the filesystem max"* — **two retired authorities in one paragraph**, both contradicting
   `CLAUDE.md` §2 and `docs/BOARD.md` §1. §1 also says the **UI seat writes `.cs`**, contradicting
   `CLAUDE.md` §2 and `RULES.md` rule 84 (*"UI NEVER writes or edits `.cs` — no exceptions"*). §2/§4/§5
   still describe `Village.unity` rebuilds, `MainCastle_Hall` as the hub and `OuterWorld` streaming.
   **These sections are not dated ledgers** — they present as the current operator's manual, so §15's
   frozen-ledger protection does not apply and they should be **rewritten as pointers**: "how we work →
   `CLAUDE.md` §11 · the board → `docs/BOARD.md` · the gates → `RULES.md` QR-3".
2. **`WorkOrders/README.md`** repeats the same three errors (Notion, `MASTER_PIPELINES_BACKLOG`,
   `MainCastle_Hall`) and adds a fourth that is now actively harmful: *"Number/filename collisions
   across this folder are expected, not defects."* It is dated 2026-06-26 and should either be
   bannered as frozen or reduced to a pointer at `docs/BOARD.md` + the banner.
3. **The board status vocabulary now lives in three places** — `bucket_of()` in
   `tools/board_build.py`, `docs/BOARD.md` §3b, and `WORK_ORDER_1011` §2.4 (which specified it be
   copied *"verbatim"*). `docs/BOARD.md` §4 already binds the first two together in one commit; the WO
   copy is an unprotected orphan that will drift. It is DATA-layer and frozen, so: **banner it**,
   pointing at §3b.
4. **`CLAUDE.md` §2's `860–899` range** is a copied number inside the very rule that forbids copying
   numbers (`RULES.md` C-1). It should say "read your seat's block off the banner" and name no range.
5. **`CLI_LANES_WO_NUMBERS.md`'s own WO-937 entry says "20 are not work orders"**, while the WO it
   describes was corrected to *18 companion docs + 18 legacy unnumbered work orders* — and the disk
   today holds **19** non-`WORK_ORDER_` files. A summary copied into the numbering authority went
   stale against its own source within a day. **The banner should carry the number and the filename,
   and nothing else** — the one-paragraph summaries under each entry are a second board.

---

## 2.6 What is misfiled

**`WorkOrders/` holds 19 files that are not work orders** (18 companion docs + 1 orphan). WO-937
already solved the *counting* problem correctly — the `Doc` bucket keeps them discoverable while
excluding them from `--check`, and `docs/BOARD.md` §3a is explicit that dropping them would trade a
miscount for a discoverability hole. **I would not move most of them.** But three groups are genuinely
misfiled:

1. **The orphan RESULT: `WorkOrders/TROOP_GEAR_WIRING_2026-08-09.RESULT.md`** — a `.RESULT.md` with no
   parent `WORK_ORDER_*` file. It can never attach to a board row, so the work it records is invisible
   to every query. ⚑ **Owner ruling wanted:** mint it a number retroactively (and its WO), or move it
   to `docs/reference/` as an audit note. It should not stay a floating RESULT.
2. **Frozen dated ledgers that are not work-order material at all** —
   `BACKLOG_STATUS_2026-06-24.md`, `QA_CHECKLIST_2026-06-28.md`,
   `MAINCASTLE_REFERENCES_AUDIT_2026-07-04.md`. These are point-in-time history sitting in the live
   work folder. → `_archive/` (bannered, bodies frozen).
3. **Live reference documents that other docs cite by name** — `DESIGN_CONNECTOR_IS_THE_ONLY_CONTRACT.md`,
   `DESIGN_GENERIC_DUNGEON_MAPPER.md`, `STAIR_PREFAB_SCRIPT_CONTRACT.md`, `WO541_MODEL_API.md`,
   `RESEARCH_BRIEF_GROK_NAVMESH_STITCHING.md`. These are SOURCE-layer docs filed in the DATA layer.
   → `docs/reference/` (which already exists and holds `AUDIT_2026-08-09.md`, `DATA_CLASS_MAP.md`,
   `MONETIZATION_ACTIVATION_LADDER.md`). Leave `README.md` and `DUNGEON_WO_INDEX.md` in place — they
   index the folder they live in.

**Separately: 14 `CANON_GROUND_TRUTH_<date>.md` files sit at the repo root**, alongside 110 root
`.md` files total. `CLAUDE.md` §15 says *"keep exactly ONE current"* and supersede by date — which the
dates technically satisfy, but "the newest anchor" is now a **directory listing sorted by name**
rather than a pointer, in the one file every seat is told to read first. Recommend archiving all but
the newest two to `_archive/canon/`, or ⚑ adding a `CANON_GROUND_TRUTH.md` that is *always* the current
one (with the dated files as history) so the read-first instruction resolves without a lookup.

---

## 2.7 What is missing, beyond the above

- **A triage rubric with a decision test.** `docs/TICKET_PIPELINE.md` §2 makes NEW-vs-EXISTING the
  critical gate but gives no procedure, so the classification is a judgement call at exactly the point
  where being wrong is most expensive (RCA-ing something that was never built). Two or three
  questions would do it: *does an id / class / scene exist that implements this? does a regression or
  capture case reference it? does a `.RESULT.md` claim it shipped?* — three lookups, not an opinion.
  The failure it prevents is documented: `WORK_ORDER_1010_*.RESULT.md` §5 and `WORK_ORDER_936` §4 both
  record the same seat mis-stating what was live **twice in one session**, because a display name was
  read as an id.
- **A stated bridge between tickets and work orders.** Tickets live in the Task list with a `stage`
  field; work orders live in files with a `**Status:**` line. Nothing says when a ticket becomes a WO,
  whether a WO can exist without a ticket, or which one the owner closes. `RULES.md` C-2 flags this as
  unresolved and it is ⚑ **an owner ruling**, not something a seat should settle.
- **A "how to write a good acceptance criterion" line.** The live WOs already model it — `936`'s
  criteria each name the oracle that proves them, and `1010`'s RESULT could report honestly *because*
  its criteria were individually checkable. One sentence in the template captures it.
- **Nothing in this repo currently gates on the false-green risk named in Part 1 step 31.** Until the
  in-flight `COMPILE_GATE_OK` fix lands, the two-part check (marker **and** `error CS` grep) is a
  human step that nothing enforces. When the fix lands, ⚑ it should be paired with a regression that
  proves the gate goes RED on a deliberately-broken assembly — otherwise the next false green is
  equally silent.

---

*Maintained per `CLAUDE.md` §15. If you change a step's rule at its source, fix the one line here in
the same commit. If you find yourself pasting an explanation into this file, stop — that content
belongs in the source doc, and the copy is the drift.*
