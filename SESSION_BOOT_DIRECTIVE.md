# ⛔ SESSION BOOT — DO THIS BEFORE YOUR FIRST REPLY (auto-injected every session)

**This is auto-injected by a SessionStart hook so the owner NEVER has to tell you to read the canon
again. Her #1 recurring pain (every session, for weeks): a fresh CLI starts working without reading the
docs / being the SME / instrumenting first. The docs already exist and scream this — the failure is not
reading them, it's NOT ACTING. So ACT, turn one, unprompted. Reading this is not doing it.**

## 1. READ THESE NOW (in order) — be the SME from the CODE, not comments (comments lie)
1. `SESSION_CANON_LOADER.md` — the at-a-glance primer
2. The current `CANON_GROUND_TRUTH_<date>.md` (newest dated one at repo root) — live state anchor
3. The newest `RESUME_*.md` at repo root — **the live work thread + exact next steps** (start here for current work)
4. `docs/HANDOVER.md` (newest session block) + `OVERNIGHT_AUTOPILOT_LOG.md`
5. `docs/MASTER_CATALOG.md` (mandatory) + the relevant `docs/MASTER_CATALOG/<area>.md` for what you'll touch
6. `docs/ARCHITECTURE.md` (hub) + `docs/ARCHITECTURE_PRINCIPLES.md` (the 4 laws) + the relevant `docs/*ARCHITECTURE*`
7. `PREFLIGHT_GATE.md` — answer Gate A before ANY code, Gate B before ANY debugging, Gate C before "done"
8. `CLAUDE.md` + `MEMORY.md` are auto-loaded — obey them.

## 2. THEN OPERATE THIS WAY (this is the part CLIs skip — don't)
- **INSTRUMENT, DON'T GUESS (CLAUDE.md §12, hard gate).** No code edit on a real bug until you can cite the
  CAPTURED data line (F8 break-log / Player.log / FlowTrace / a headless fleet run) that PROVES the cause.
  Static code-reading LOCATES candidates; it never CONCLUDES. Read the already-harvested data FIRST.
- **ORCHESTRATE + VERIFY + DELEGATE.** Your hands = gates + commits. Fan out read-only agents for deep digging.
- **CONFIRM BEFORE REVERTING CANON.** If a move contradicts/reverts a settled decision, STOP + name it + get OK.
  Stale code ≠ intent. Don't band-aid or pivot because it's hard — find the root fix.
- **Sole committer, by EXPLICIT PATH** (never `git add -A`). Never hand-edit `.unity`. Push ONLY on owner OK.
- **Update canon + memory IN THE SAME BREATH** as any state change (§15). Persist in BOTH the doc and MEMORY.

## 3. CURRENT LIVE THREAD (as of 2026-07-03)
**Read `CANON_GROUND_TRUTH_2026-07-03.md` (the live anchor) + `RESUME_2026-07-03_morning.md` first.**
In one line: the 07-02→03 convergence session (feel arc, south slice 6/6, Tutorial V2 built OFF) sits
uncommitted awaiting the owner's felt-pass; then commit lanes → seam un-stack (WO-453) → WO-545 Addressables.

> If you catch yourself about to edit, debug, or answer without having done §1 + §2 → STOP. The owner having
> to remind you is the failure this file exists to end.
