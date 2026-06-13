# HANDOVER — the one sheet a new session reads to be productive now

> **Read order for a new session:** this sheet → `docs/MASTER_CATALOG.md` (mandatory, be the SME) →
> `docs/ARCHITECTURE.md` (the architecture hub) → the relevant `docs/MASTER_CATALOG/<area>.md` for
> what you're about to touch. This sheet is the *operator's manual*; those are the depth. The code
> wins on truth — comments lie (the catalog is verified from source).

---

## 1. HOW WE WORK — the orchestrator / CLI-gatekeeper model

Three roles (CLAUDE.md §2, §11):

- **UI (Claude):** writes work orders + specs, does the flow-first triage / RCA, makes creative
  calls, and writes `.cs` (Windows path, Write/Edit only — see §2 below).
- **CLI (this seat / lead):** the **sole committer + gatekeeper**. Owns batchmode (gates, bakes,
  builds), reconciles every session's diffs by explicit path, commits, pushes **only on owner OK**.
- **Owner (Samantha):** PM; final creative + sequencing decisions; runs the editor for felt/playtest.

The loop:

1. **Flow-first triage** — what *should* happen given the state ("is this state even expected?"),
   NOT culprit-hunting a stack trace. Ambiguous tickets (no repro/screen/stack) bounce back.
2. **Fan out agents** — each does ONE focused task. Read-only **diagnosis/verify** agents are
   gate-free → fan out many. **Edit-only** implementation agents run on **file-disjoint silos**
   (the §9 lanes; same-file work = one agent), told NOT to gate/commit.
3. **Batch-gate ONCE** — the orchestrator runs the compile gate over the combined tree
   (`COMPILE_GATE_OK`), then **commits each lane by explicit path** (never `git add -A`).
4. **Push only after** the owner retests/confirms (felt/gameplay) or a regression passes
   (data/logic) — "push the ones that passed."

**Notion is the live WO board** — *Defenders of the Realm — Pipelines* "Work Orders" DB
(data source `5f66b263-c732-4075-b94a-f5f4de9f8087`). Full WO spec files stay in the repo
(`WORK_ORDER_NNN_*.md`). WO-numbering authority = `MASTER_PIPELINES_BACKLOG_2026-06-06.md`, **not**
the filesystem max. Migrated off Linear; see `NOTION_SOURCE_OF_TRUTH.md`.

---

## 2. THE NON-NEGOTIABLE RULES (binding — condensed)

1. **UI never touches code; CLI writes ALL code.** (Owner 2026-06-13, binding.) The UI session does
   RCA / specs / narrative / screenshots / board grooming — it does NOT write or edit `.cs`. Only CLI
   writes code, on the **Windows path with Write/Edit only** — never `cat >`/`echo >>` via the §0 Linux
   mount (it does NOT sync reliably; redirects truncate/duplicate/interleave). If a file is broken on
   Windows, only CLI fixes it. The
   **NUL-byte gate now enforces this**: `CompileGate.Run` scans every `Assets/**/*.cs` for embedded
   NUL bytes and withholds `COMPILE_GATE_OK` if any are found (catches mount-garble that looks clean).
2. **§1 Quality gate on every `.cs` you touch** — brace balance + leak-scan (no stray
   `</content>`/`</invoke>` junk from agent Writes) + NUL-scan. **`DeNelle.Editor.CompileGate.Run`
   is the authoritative gate** — its `COMPILE_GATE_OK` marker is the only proof a tree compiles clean.
3. **Reconcile, don't replace.** WO specs predate the branch — treat as intent, add additively,
   never blind-replace a file.
4. **Stage by explicit path — never `git add -A`.** LFS-clean textures show as ~132-byte pointer
   diffs; a blanket add mass-converts them. Stage each path you reviewed.
5. **Never hand-edit `.unity` scenes.** `Village.unity` is corruption-cursed and ABANDONED
   (`Village2` is canonical). Rebuild via the builder (`VillageSceneBuilder.BuildVillage`,
   `CastleHubBuilder` — but do NOT regen the hand-dialed castle, it reverts owner offsets).
6. **One committer.** Two committers duel on `.git/index.lock` → stale locks + false "pushed."
   Other sessions write + signal "ready"; the one committer reconciles.
7. **Unity editor must be CLOSED for any batchmode gate/bake/build** — project lock otherwise.

---

## 3. RULES WE ADDED THIS SESSION (the new canon)

- **INSTRUMENT-FIRST debugging (CLAUDE.md §12, BINDING).** We do **not** guess at bugs — we
  instrument the flow and let the data say where it dies. Four `DeNelle.Core` helpers in
  `Assets/_Modules/Core/Diagnostics/`:
  - **`FlowTrace`** — `Step/Warn/Fail/Throttle/Once/Measure`, `[Flow:<system>]`-tagged. Trace flow
    entry, every branch *taken*, every fallback, service resolution, and the render/commit seam.
  - **`Guard`** — `Try`/`TryEach`; **one bad object must never blank a whole list/screen** (list
    population uses `Guard.TryEach`). Never compile-stripped (it changes control flow).
  - **`BreakCaptureHarness`** — F8 flight recorder → `break-log.jsonl` + screenshots.
  - **`DataRegression`** — headless "real object in → assert → one marker" gate.
  - **No silent failures:** a `catch` that swallows without logging is forbidden; every fallback is
    a `Warn`, every real failure a `Fail` (error-level → lands in the recorder). Method =
    `docs/INSTRUMENTATION_STANDARD.md`.
- **The AutoPilot bot / fleet.** A headless player bot (`Assets/_Modules/DevTools/AutoPilot*`,
  `Assets/Editor/AutoPilot/`) drives the game and emits ranked tickets. The **player .exe needs no
  Unity license**, so `run-autopilot-fleet.ps1 -Count N` runs dozens of instances in parallel (each
  a distinct `--seed`/`--run`); `AutoPilotTickets.Emit` dedupes + ranks by how many runs reproduced
  each break. `-nographics` → logic/flow/crash coverage only (UITK picking won't resolve headless).
- **Confirm-to-cross seam + WarpTo.** Two-scene navmeshes don't auto-connect. `SceneTransitionTrigger`
  disables → warps → re-enables the hero's `NavMeshAgent` across the seam. Debug "can't cross/exit"
  as a **navmesh bake** issue, not colliders. The hero returns to a **return-point** (`ReturnScene`
  in `BattleParams` for combat; the seam warp for world crossings).
- **Hero tag = `Player` (one tag, now declared).** Locomotion/camera/HUD/triggers all
  `FindWithTag("Player")` (set in `HeroControlEnsurer.Ensure`). **Enemy AI finds the hero by
  COMPONENT** (`FindFirstObjectByType<HeroLocomotion>()`), NOT a `HeroTarget` tag — that tag was
  never declared and a GameObject has only one tag (CLAUDE.md §7).
- **Vendor-stock contract.** `Assets/_Modules/Village/Hero/VendorStockContract.cs` is the single
  source of truth for what each store TYPE sells (armorer=armor, etc.). Two consumers read the same
  `AllowedFor()` mapping: `ShopPanel.ShowBuy` filters stock; the AutoPilot bot asserts the built
  stock matches — so the bot checks intent, not a duplicate.
- **Seam radius / nav lesson.** The seam is a **proximity** trigger; the hero (a `NavMeshAgent`)
  stops at the **navmesh edge**, so the trigger radius must overlap the walkable surface or the hero
  never reaches it. Tune the seam against the bake, not the visual mesh.
- **Pet-from-shop flow.** Pets are acquired through the shop flow (not only PetSelect onboarding) —
  trace via `[Flow:*]` if a purchased pet doesn't appear.
- **OnboardingPanelGuard.** The "dev tools / UI dead after Yarn" bug: a UIDocument backed by the
  shared `OnboardingPanelSettings` leaked into a gameplay scene and its raycaster sat on top of the
  click stack, eating every click. `Assets/_Modules/Onboarding/OnboardingPanelGuard.cs` enforces the
  invariant (that panel may only intercept input in Title/HeroSelect/PetSelect) on every scene load.
  **Fixed.**

---

## 4. THE BUILD / GATE / BAKE CYCLE

All batchmode runs through `run-unity-method.ps1` (handles the relaunch-fork quirk — poll for the
exe/marker, not the wrapper exit code; the 505 license line is transient/non-fatal). **Editor must
be closed.**

| Task | Invocation |
|---|---|
| **Compile gate (authoritative)** | `run-unity-method.ps1 -Method DeNelle.Editor.CompileGate.Run` → `COMPILE_GATE_OK` (brace + leak + NUL scan) |
| **Data/logic regression** | `run-unity-method.ps1 -Method DeNelle.Editor.DataRegression.RunAll -LogName data-regression.log` → `REGRESSION_OK` / `REGRESSION_FAIL` |
| **Castle rebake** | `BatchRebuildCastleFromRecipeAndBake` (do NOT regen the hand-dialed hub geometry) |
| **Outpost wiring** | `BatchWireOutpostsAndSave` |
| **Village rebuild** | `DeNelle.Editor.VillageSceneBuilder.BuildVillage` (never hand-edit the scene) |
| **Windows player build** | `build-windows.ps1` |
| **AutoPilot fleet** | `run-autopilot-fleet.ps1 -Count N` (player exe; no license needed) |
| **WebGL ship** | `ship-webgl.ps1` / `build-webgl-isolated.ps1 -Ship` → butler → itch |

- **F8 break-logs land in `break-log.jsonl`** (+ screenshots) via `BreakCaptureHarness`; fleet runs
  namespace theirs per `--run`. `Fail`/`LogError` lines are what the recorder captures.
- **exe-stub quirk (load-bearing):** incremental player builds skip re-emitting the exe stub → stale
  exe vs fresh scenes → `level3 corrupted` native crash. **ALWAYS delete `Builds/Windows` before
  `build-windows.ps1`.** Also: build via the Defenders→Build menu / `build-windows.ps1`, NOT the
  Build Profile "Build" button (it skips the Static-Batching-off mitigation).

---

## 5. CURRENT STATE + RESUME POINTS

**Playable loop:** Title → HeroSelect → PetSelect → `MainCastle_Hall` (home hub) with `OuterWorld`
streaming additively; south-gate seam → OuterWorld; raids via `RaidOutpostSystem` (4 in-world
outposts, ~10s delay) and additive `Garrison_*` scenes; `Village2` = TD raid target; ATB battles
return to `ReturnScene`. Store ~70% built (do NOT greenfield — `PackStore` exists; scene-wiring
disabled pending its own PanelSettings). Build mode wired end-to-end for towers (~70%).

**Recently fixed this session:**
- Dev-tools-dead-after-Yarn → `OnboardingPanelGuard` (§3).
- Archer/blast tower behavior — fixed.
- Vendor stock leakage (armorer selling weapons/potions) → `VendorStockContract` (§3).
- Raid outpost never found — 3-min spawn delay cut to 10s.

**Known-open / watch:**
- **South-gate ~34m nav reach** — verify the seam trigger radius overlaps the walkable navmesh
  (the hero stops at the navmesh edge; §3 seam lesson). Test in Play/build, not batchmode
  (`NavMesh.SamplePosition` fakes a complete path in headless).
- Remaining AutoPilot audit findings — work the ranked tickets from the latest fleet run.
- Cross-zone *AI* pathing across the seam is deferred (off-mesh links when raids walk between zones).

**Pointers:** `docs/ARCHITECTURE.md` (architecture hub) · `docs/MASTER_CATALOG.md` (verified-from-code
SME catalog) · `docs/INSTRUMENTATION_STANDARD.md` (the §12 method) · `docs/MODEL_CATALOG.md` +
`docs/polyperfect-asset-catalog.md` / `docs/kaykit-asset-catalog.md` (check before referencing a
prefab) · Notion "Work Orders" DB (live board) · `PIPELINE_STATE.md` (full pipeline detail).

---

*Maintenance: keep §3 and §5 current as the canon and the loop move. This sheet is the entry point —
depth stays in the deep-dives it points to.*
