# SESSION START HERE — read this first, don't re-digest the repo

**Last updated:** 2026-05-31 (UI lane)
**Purpose:** Single entry point so a new session is productive in ~2 minutes without
reading 90 docs and 180 work orders. This is the **map + living log + handoff rules**.
Update the [Order Log](#order-log--living-status) as orders close, break, or arrive.

> If you read only one file, read this one. It tells you what's true *now*, what to do
> *next*, and points to the 3 canonical docs for depth. Everything else is detail.

---

## 1. The 30-second picture

- **Tree is GREEN and playable.** Baseline restore points: `00b1662` / `8e4fd35`.
  A Windows exe and a 186 MB WebGL build exist in `Builds/`. We always move forward
  from a green build — never ship red.
- **What's BUILT:** Defend-the-Tower pillar (polished), enemy+animation pipeline,
  dragon boss, village wave loop, store/economy backend (~70%, not scene-wired —
  UXML doesn't render in builds, code-built UI only).
- **The vision** (`docs/NORTH_STAR.md`): mobile-first base-builder + tower-defense +
  offline-idle. Loop: BUILD → HARVEST → UPGRADE → DEFEND → grow while offline.
  Keystone gap = **WO-108 player build mode** (hand the player VillageSceneBuilder's power).
  Owner's north star as of 05-31: **fun first, monetization second.**
- **Where we're at:** a playtest bug-batch (DEF-93..112 in Linear / WO-15x–17x) is the
  active work. Closing it → cut a fresh WebGL build → tester link. That web build IS the deliverable.

---

## 2. Canonical docs (the only ones to open for depth)

| Read when you need… | File |
|---|---|
| **What's built vs stub vs missing** (ground truth) | `PIPELINE_STATE.md` |
| **How bugs flow + the lane rules** (the process) | `BUG_WORKFLOW.md` |
| **The live open-bug board** (the WHAT) | `BUG_LIST.md` |
| Product vision / business / GTM | `docs/NORTH_STAR.md` |
| Per-bug spec (root cause, file, fix, acceptance) | `WORK_ORDER_NNN_*.md` |
| CLI's completion record per order | `WORK_ORDER_NNN_*.RESULT.md` |
| The 12-agent lane map / per-agent briefs | `PARALLEL_LANES.md`, `AGENT_OPENERS.md` |

**Do NOT** open the old `ORCHESTRATION_PLAN.md` for current state — it's the stale
05-28 VFX-sprint map (kept for history only). This file supersedes it as the entry point.

**Stale metadata to ignore:** `CLAUDE.md §2` says "highest WO is 105." Real highest is
**WO-180**. New work orders start at **WO-181**. (25 `.RESULT.md` files closed so far.)

---

## 3. The rules that shape everything (non-negotiable)

1. **One check-in at a time, only on green.** Each story → its own commit → must compile/
   build green before the next story is released. No batch commits across stories.
2. **Parallel threads, ONE committer.** Many agents can write code in parallel, but exactly
   **one** session is the git committer + baker (the CLI). Two committers duel on
   `.git/index.lock` → stale locks + false "pushed."
3. **Validate each story before moving forward.** A story isn't "done" until its
   `*.RESULT.md` is filed AND it passes (compile/brace gate, and owner re-screenshot for visuals).
4. **Roles** (CLAUDE.md §2):
   - **UI (this lane / Claude in-app):** writes specs/WOs, routes, makes creative calls,
     maintains this file + `BUG_LIST.md` + Linear. **Never edits `.cs`/scenes, never bakes.**
   - **CLI (Claude Code on Windows):** the implementer — writes/edits code, brace-gates,
     compile-verifies, **sole committer**, runs bakes (editor closed), files `*.RESULT.md`.
   - **Owner (Samantha):** PM, playtests, priority, final creative calls.
5. **Village Builder is single-writer.** `VillageSceneBuilder.cs` = ONE agent, ONE
   sequential pass, ONE rebake. Never two agents in that file (serialization corruption).
6. **Don't bash-edit build files / commit the whole tree.** Mount-sync corrupts code
   writes; `git add -A` mass-converts textures to LFS. Commit by **explicit path**.

---

## 4. The plan — four lanes + a polish lane

**The hard constraint that shapes everything:** `VillageSceneBuilder.cs` is **single-writer** —
every village/castle/gate/terrain fix serializes through it (one thread, one rebake).
Everything else parallelizes. So the team is **four lanes that run at the same time**, plus an
independent polish/animation lane. Biggest visual wins flagged 🌟.

### Lane A — Village Scene (SERIAL, top priority) — the critical path to a clean playtest
ONE agent, ONE sequential pass, ONE rebake. Order:
1. 🌟 **WO-173 (P0)** — world is a black void → exterior terrain back. *Unblocks everything visual.* **(DONE `ee0d6ae`)**
2. **WO-177** wall lean/walk-through/south-gate → **WO-158** gates passable (+north=4) →
   **WO-167** pillar clip → **WO-168** navmesh unseal **(DONE `5834479`)**.
3. **WO-157** strip magenta crystal veins. *(WO-176 tower / WO-179 moat are P2 — fold in if cheap, else defer.)*
4. **Rebake village** (WO-137 pattern, editor closed) → re-verify the whole cluster.

### Lane B — Combat / AI (parallel, own files: `DeNelle.BattleATB` + combat)
- **WO-125** dragon unhittable / no lose-condition → **WO-132** real village lose state →
  🌟 **WO-169** ATB FF party battle (capsules→models, flip layout enemies-LEFT, surface party,
  Skills/Item menus + targeting, dynamic HUD) → **WO-170** retro 2D spell VFX.
- Plus **WO-135** P1 bug-triage fixes.

### Lane C — Core data / economy (parallel)
- **WO-164** zone foundation (read by Lanes B & D — **do first**) → **WO-131** wallet
  unification → **WO-108 player build mode** *(the keystone — hand the player VillageSceneBuilder's power)*.

### Lane D — World / economy content (parallel)
- **WO-117** worker auto-collect *(owner's #1 demoable)* → **WO-153 / 159 / 160**
  nodes / settlements / tribes.

### Polish / Animation lane (independent of all four)
- **WO-174 + WO-163 + pet T-pose = ONE animator-param-contract fix** (don't implement three times) →
  **WO-156** camera over walls (*decide authoritative camera first*: HeroCinemachineRig pri 100 vs
  SmartMobileCamera) → **WO-178** health-bar styling → **WO-175** store theming.

### Release gate (not parallel — after the visible-bug lanes are green)
All bug lanes green + clean editor playtest → **cut WebGL build** (reuse WO-123 path, Brotli,
`vercel.json` placed) → host (itch.io for the ~186 MB) → re-verify on web → tester link.
The clean web build IS the deliverable (`docs/webgl-hosting-notes.md`, `BUG_WORKFLOW.md §AFTER`).

---

## 5. Order Log — living status

> **The point of this section:** a new agent reads this table instead of re-deriving state.
> **Keep it current.** When an order closes, breaks, or a new bug arrives, add/edit a row
> with a one-line comment on *what was applied* or *what changed*. Newest notes at the top of a cell.
> Status: `OPEN` · `IN PROGRESS` · `DONE` (RESULT filed) · `BROKE` (regressed, needs re-open) · `BLOCKED`.

### Lane A — Village Scene (SERIAL — one agent, one rebake)
> **2026-05-31 rebake + Windows build GREEN (`8f4c6f3`).** Reconcile call confirmed: the village
> geometry was already correct in committed code — the owner's screenshots were a **stale scene**.
> Rebake re-synced `Village.unity` (4 cardinal gates, 42 wall sections, 0 placeholder primitives,
> level3=2MB healthy). NEXT: owner playtest tells us which (if any) bugs are genuinely still open.
| WO | Status | What's been applied / notes |
|---|---|---|
| 173 (P0) | **DONE ✓built** | Exterior terrain `ee0d6ae`; PLUS `8f4c6f3` implemented the missing `EnsureTerrainMaterial()` (CS0103 had broken compile) so the terrain surface actually renders (URP TerrainLit). Built green. |
| 177 | **RECONCILED — playtest** | Visible perimeter wall rotates yaw-only (can't lean); leaning KayKit ring has renderers OFF. Likely already fixed; **playtest to confirm** south-gate prefab + wall walk-through collision. |
| 158 | **RECONCILED — playtest** | 4 cardinal gates baked (north gate present in perimeter + WallBarrier-North-W/E + moat north band). ⚠ RESIDUAL: bake logged only **3 drawbridges** — north gate may lack a moat drawbridge (exitable wall gap, water beyond). Confirm in playtest. |
| 167 | **OPEN — playtest** | Gatehouse pillar clip — couldn't confirm from code; check in the fresh build (gatehouse = Gate_Medieval_Medium @ target 10). |
| 168 | **DONE** | Unseal cardinal gate openings — `5834479`; NavMesh rebaked `cb7e0eb` + this rebake. |
| 157 | **DONE (rebake)** | Crystal veins were already disabled in code (1103, WO-136); the stale scene still showed them — this rebake removed them. |
| 176 | OPEN (P2) | Tower ugly — swap to stylized polyperfect mesh + fix materials. |
| 179 | OPEN (P2) | Moat water sits on ground — drop below grade + style water material (gated on VFX pass). |
| 166 | **DONE** | Base gates + walk-anim + pet + rampart stairs. Stairs hug wall `071e478`. |
| (rebake) | **DONE** | BuildVillage rebake + Windows build green (`8f4c6f3`, 2026-05-31). |

> ⚠ **Hero-in-village = violet capsule** (bake log: `Wizard.fbx not found`). Runtime Resources hero
> load may override it; confirm in playtest. Separate from the Lane lanes — note if it shows.
> ⚠ **ProjectSettings.asset** has uncommitted WebGL config (Brotli OFF, `webGLCompressionFormat 0`) from
> another session — left untouched; whoever tunes WebGL should confirm before the next web build.

### Lane B — Combat / AI (parallel)
| WO | Status | What's been applied / notes |
|---|---|---|
| 125 | OPEN (P1) | Dragon unhittable / no lose-condition. |
| 132 | OPEN (P1) | No real village lose state — wire a defeat condition. |
| 169 | OPEN 🌟 | FF party battle: capsules→models, flip layout (enemies LEFT), surface party, Skills/Item menus + targeting, dynamic HUD, data-driven. Start order is inside the WO. |
| 170 | OPEN (P2) | Retro 2D spell VFX / sprite anims — pairs with 169. |
| 135 | OPEN | P1 bug-triage fixes (batch). |

### Lane C — Core data / economy (parallel)
| WO | Status | What's been applied / notes |
|---|---|---|
| 164 | OPEN | Zone foundation — read by Lanes B & D, **do first**. (Zone-depth scaffolding partly in `8248d39`.) |
| 131 | OPEN | Wallet unification. |
| 108 | OPEN ⭐ | **Player build mode keystone** — hand the player VillageSceneBuilder's power. The vision linchpin. |

### Lane D — World / economy content (parallel)
| WO | Status | What's been applied / notes |
|---|---|---|
| 117 | OPEN 🌟 | Worker auto-collect — owner's #1 demoable. |
| 153 | OPEN | Resource nodes. |
| 159 | OPEN | Settlements. |
| 160 | OPEN | Tribes. |

### Polish / Animation lane (parallel — ONE param-contract fix covers 174/163/pet)
| WO | Status | What's been applied / notes |
|---|---|---|
| 174 | OPEN | Hero travels backwards (orientation) + no walk anim (Speed param). |
| 163 | OPEN | 3,351 console errors — AmbientNPC drives missing animator param every frame; + AudioMixer exposed-param. **Guard SetFloat with cached HasParameter** (memory: animator-param-guard). |
| pet | OPEN | Pet T-pose — same fix, fold into the anim pass. |
| 156 | OPEN | Camera over walls — **decide HeroCinemachineRig (pri 100) vs SmartMobileCamera FIRST**, then over-wall framing + orbit + wall-fade. |
| 178 | OPEN | Health bars flat → restyle to match themed quest-panel HUD. |
| 175 | OPEN | Store generic dark box → themed frame, real icons, themed buttons/scrollbar (code-built UI, not UXML). |

### Other open / urgent (loose Linear issues not yet folded into a lane WO)
| ID | Status | Note |
|---|---|---|
| DEF-102 | OPEN (Urgent) | Hero does nothing at 0 HP — no death/respawn/game-over. Overlaps WO-132; fold in or spec a WO. |
| DEF-97 | OPEN | "Avalon" → "Elarion" string sweep (canon). |
| DEF-100 | OPEN | Portal interior glow VFX (repurpose existing spell VFX). |

*(Full Linear board: DEF-93..112. This table tracks only what's actively in the plan —
update it; don't mirror all of Linear here.)*

---

## 6. Handoff to CLI — explicit order + reason

When code for a story is **written and brace/compile-clean**, UI hands it to CLI for the
**merge/commit/bake** with an explicit, self-contained order. CLI is the **sole committer**
and the only one who bakes. Template:

```
ORDER → CLI: WO-NNN <short title>
REASON: <why now — what it unblocks / which lane / P-level>
TYPE: <CREATE | ADDITIVE | TARGETED EDIT | REPLACE | SCENE EDIT | BAKE>
FILES (explicit paths only — no `git add -A`):
  - Assets/.../Foo.cs
BAKE NEEDED: <none | "run X via run-unity-method.ps1" | "rebake village (editor closed)">
GATE BEFORE COMMIT: brace-balance every .cs; compile-verify; bake success marker (not exit code)
ON GREEN: commit by explicit path → file WORK_ORDER_NNN_*.RESULT.md → report back
ON RED: bounce the file back as a fresh order + Linear note; do NOT fix-forward garbage
```

**Rules for the handoff:**
- One order = one story = one commit. CLI does not batch unrelated stories.
- CLI judges bake success by the **success marker/artifact**, not the exit code (505 line is
  transient; Unity forks on launch — poll for the exe, not the wrapper exit).
- **Only after** the `*.RESULT.md` is filed and the story validates does UI release the next
  order and mark Linear `Done` + this log `DONE`.
- Village-lane orders are **batched into one pass + one rebake** before the bake order goes to CLI.

---

## 7. Keeping this file useful (for the next session)

- Update the **Order Log** every time something closes/breaks/arrives — that's the whole point.
- Keep it a **hub**: depth lives in the canonical docs; don't duplicate their content here.
- If a rule or the plan changes, edit §3/§4 — don't append a contradicting note elsewhere.
- This file + `BUG_WORKFLOW.md` + `BUG_LIST.md` + `PIPELINE_STATE.md` = the full operating picture.

🤖 Maintained by the UI lane as the session entry point.
