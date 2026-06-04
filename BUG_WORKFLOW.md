# Bug Workflow — how bugs flow (for CLI)

> How playtest bugs move from sighting → fix → verified. Read this so the bug list + Linear issues make
> sense as a process, not just a pile. Owner: Samantha. UI lane: writes specs/bugs. **CLI: implements,
> compile-gates, commits, bakes.**

## The roles (who does what)
- **Owner (Samantha)** — playtests, reports bugs (screenshots), makes creative calls, sets priority.
- **UI (the design/spec lane)** — turns each sighting into a **WO spec** + a **bug-list entry** + a
  **Linear issue** (tagged to a user story). Does NOT edit `.cs`/scenes or bake. *(Note: Linear has no
  "Claude/CLI" user, so issues are assigned to Samantha with "**Implemented by: Claude (CLI)**" in the
  body — CLI owns the actual work.)*
- **CLI (Claude Code)** — **the implementer**: writes/edits code, brace-gates, compile-verifies, commits
  by explicit path, and runs bakes (editor closed). Files a `*.RESULT.md` per WO when done.

## The flow (per bug)
```
1. SIGHTING   owner screenshots a bug
2. SPEC       UI writes WORK_ORDER_NNN_*.md (root cause + file + fix + acceptance) + adds to BUG_LIST.md + creates a Linear issue (user story + lane + "Implemented by: CLI")
3. CLAIM      CLI picks the issue (top of its lane, P0 first); marks Linear "In Progress"
4. FIX        CLI implements in code → brace-gate every .cs → compile-verify (editor closed)
5. BAKE       if a scene change: CLI bakes (editor closed); never UI
6. RESULT     CLI writes WORK_ORDER_NNN_*.RESULT.md + commits by explicit path
7. VERIFY     owner re-screenshots; UI marks BUG_LIST.md ✅ + Linear "Done"
```

## The LANES (critical — this is the flow that prevents collisions)
Bugs are grouped into **4 parallel lanes** (see `PARALLEL_LANES.md`). The rule:

| Lane | File scope | Parallel? | How CLI works it |
|---|---|---|---|
| **Village Builder** | `VillageSceneBuilder.cs` + bakes | ❌ **SINGLE-WRITER** | **ONE agent, ONE sequential pass, ONE rebake.** Never two agents in this file. Do P0 (WO-173) first, then the whole village cluster (177/158/167/168/157/176/179) in that one pass, then bake once. |
| **ATB** | `DeNelle.BattleATB` | ✅ parallel | own files; WO-169 has its own start-order |
| **Animation** | hero/pet/NPC animator code | ✅ parallel | WO-174/163/pet = **ONE param-contract fix**, not three |
| **UI** | `SmartMobileCamera`, HUD, store (code-built) | ✅ parallel | own files; camera (156) = decide authoritative camera first |

**Why this matters:** the Village Builder is the bottleneck. Batching all its bugs into one pass + one
rebake (instead of fix-bake-fix-bake per bug) is far faster and avoids the corruption/desync traps. The
other three lanes run **at the same time** on different files — assign them to different agents.

## The DON'Ts (traps to avoid — from project history)
- **Don't** put two agents on `VillageSceneBuilder.cs` (serialization corruption — CLAUDE.md §9).
- **Don't** fix the animator-param bug three separate times (hero/pet/NPC = one contract fix).
- **Don't** bake per-bug in the village lane — batch + one rebake.
- **Don't** commit the whole tree (EOL churn) — commit by explicit path.
- **Don't** edit the wrong camera (decide HeroCinemachineRig vs SmartMobileCamera first).
- **UI doesn't** touch `.cs`/scenes/bakes; **CLI doesn't** wait on UI for code.

## Where everything lives
- **`BUG_LIST.md`** — the live board (bugs by lane, user story, status, P0).
- **`WORK_ORDER_NNN_*.md`** — the per-bug spec (root cause, file, fix, acceptance).
- **`*.RESULT.md`** — CLI's completion record per WO.
- **`PARALLEL_LANES.md` / `AGENT_OPENERS.md`** — the 12-agent lane map + per-agent briefs.
- **Linear** — DEF-108 (P0 world), DEF-109 (village pass), DEF-110 (ATB), + Animation/UI issues — each
  tagged to a user story, "Implemented by: CLI".

## AFTER bugs close → wire it all to the WEB UI BUILD (owner 2026-05-31)
Once the bug lanes are green, **the whole thing gets wired into the web (WebGL) build** — the playable
link for testers (the NORTH_STAR "show, don't tell" pitch artifact). This is the **release gate after
fixes**, not a parallel task:

- **Gate:** the P0 + Village pass + ATB + Animation + UI lanes are **closed/green** (clean playtest: world
  renders, castle solid + exitable, hero/pet animate, ATB is a real battle, HUD/store/camera polished, no
  error spam) → THEN cut the web build.
- **Build:** WebGL build (reuse the WO-123 WebGL path — Brotli, `Builds/WebGL/`, the `vercel.json` headers
  already placed) → host (itch.io recommended for the ~186 MB size; Vercel may reject the large `.data.br`
  — see `docs/webgl-hosting-notes.md`).
- **Verify on web:** the fixes that passed in-editor must also pass in the **WebGL player** (UXML render
  trap, audio, camera, perf on the web target) — re-check the bug list against the hosted build, since
  WebGL can behave differently than the editor.
- **Then:** relay the URL to testers; pairs with the F1 dev portal for QA. New web bugs loop back through
  this same workflow.

> So the flow's end state is: **bugs close (editor-verified) → WebGL build + host → re-verify on web →
> tester link.** The clean web build IS the deliverable.

## Current priority (the critical path to a clean playtest)
**1. DEF-108 / WO-173 (P0 — world void)** → **2. the Village Builder pass + rebake** → in parallel: **ATB
(169→170), Animation (174/163/pet), UI (156/175/178)**. Biggest visual wins: world exists (173) + enemy
capsules→models (169 step 1).

🤖 Workflow doc by UI lane. The bug list is the WHAT; this is the HOW.
