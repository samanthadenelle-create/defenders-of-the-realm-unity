# ⛔ PREFLIGHT GATE — answer YES to every question, OUT LOUD, before you touch code or debug

**Owner directive (2026-06-28, BINDING on EVERY CLI / agent / session, forever).**
The owner may "call out the gate" at any time. When called — or **unprompted before any code edit or
any debugging** — you MUST post the matching checklist below and answer **YES** (with a one-line proof)
to **every** item. **A single NO, "I think so", or unproven YES = STOP. You have not earned the edit.**

This file exists because the owner should NEVER again have to remind a CLI to read the docs, be the SME,
instrument first, orchestrate, or update canon. The reminder being needed at all is the failure. These
answers are not a formality — each one is a rule already in `CLAUDE.md` / the memory index, made into a
hard yes/no gate.

---

## GATE A — Before you TOUCH ANY CODE (build, edit, refactor, "quick fix")

1. **SME first?** — Have I READ `SESSION_CANON_LOADER.md` + the relevant `docs/MASTER_CATALOG/<area>.md`
   section + the relevant `docs/*ARCHITECTURE*` for exactly what I'm about to touch — verified from the
   CODE, not the comments? *(CLAUDE.md §0 mandatory-first-step; comments lie.)*
2. **Ground truth checked?** — Have I read the current `CANON_GROUND_TRUTH_<date>.md` anchor and the
   `MEMORY.md` index, and confirmed nothing there contradicts what I'm about to do?
3. **Reuse, not reinvent?** — Have I confirmed this system does NOT already exist (PackStore, BattleArena,
   DungeonComposer, EnemyStrongholdBuilder, RegionGate, the master UI factory, etc.)? Am I extending the
   built system, not greenfielding a duplicate? *(memory: read-embedded-canon-first; dont-greenfield.)*
4. **Orchestrating, not solo-charging?** — Is deep work delegated to agents while MY hands stay on
   gate + commit? Am I NOT about to solo-dig something an agent should go deep on? *(CLAUDE.md §11.)*
5. **Right layer / lane?** — Is this the correct bounded context, presentation-vs-logic respected, on a
   §9 lane that won't collide with another agent's silo? Am I NOT smuggling a structural refactor into
   player-facing work? *(ARCHITECTURE_PRINCIPLES — HP B2B.)*
6. **Scene/asset rules?** — If this touches a `.unity` scene, am I rebuilding via the builder (NOT
   hand-editing)? If `.cs`, am I using Write/Edit on the Windows path (NEVER a bash redirect)? *(§0, §3.)*

## GATE B — Before you DEBUG / diagnose / "fix a bug" (the HARD GATE, §12)

7. **Captured data in hand?** — Have I INSTRUMENTED and run it (F8 break-log / Editor.log / Player.log /
   FlowTrace / a headless AutoPilot run) so I have REAL captured data — not a static code-read?
8. **Read the harvest FIRST?** — Did I read the already-harvested capture context (F8 watcher auto-harvest
   / break-log / the screenshots) BEFORE spawning a code-reader or forming a theory? *(§14 — "you have the
   answers yet choose not to look" is the banned failure.)*
9. **Can I cite the proving line?** — Can I point to the EXACT captured line that PROVES the root cause?
   An inferred/"plausible" root from static reading is a GUESS — not allowed. *(memory: never-inference-fix.)*
10. **Classified the failure?** — Have I split it into *data-empty* vs *built-but-invisible* vs
    *threw-and-skipped* from the trace, before touching code?
11. **Two strikes → escalate?** — If two fix attempts on this same issue have failed, am I STOPPING to
    write logs to `C:\eoa\logs\debug\` for Grok review instead of solo-iterating a third time? *(memory:
    two-failure-escalate-to-grok.)*

## GATE C — Before you say DONE / commit / hand off

12. **Gate green?** — `COMPILE_GATE_OK` AND brace-balance + NUL check passed on EVERY `.cs` I touched?
13. **Verified, not faith?** — Did I prove it works with captured data / headless / a regression — NOT
    claim-fixed on assumption? *(memory: deliver-complete-verified; no-doesnt-stop-the-demo.)*
14. **Canon updated in the same breath?** — Did I update the relevant load-bearing doc / memory IN THIS
    change (or add a dated `STALE:` banner)? A state change with no canon update is incomplete. *(§15.)*
15. **Sole committer, explicit path?** — Am I committing by EXPLICIT PATH as the one committer (no
    `git add -A`, no second committer), and pushing ONLY after the owner felt-verifies / a regression
    passes? *(§11; memory: sole-git-committer.)*

---

### How the owner uses this
> Owner: **"Answer the preflight gate."** → CLI posts Gate A (and B if debugging) and answers YES + proof
> to each, or names the NO and stops. No code, no debugging, no "done" until the relevant gate is all-YES.

### How a CLI uses this (unprompted)
Run Gate A before your first edit of a session; run Gate B the moment a bug appears; run Gate C before you
report done. Don't wait to be asked — being asked is the failure this file exists to end.
