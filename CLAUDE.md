# Defenders of the Realm — Agent Rules & Project Memory

This file is read by every agent (UI and CLI) before starting work.
Rules here are **non-negotiable**. Do not skip them to ship faster.

**MANDATORY FIRST STEP (BINDING — read before ANY work, every session):** read
`docs/MASTER_CATALOG.md` — the exhaustive, file-by-file catalog of every
class/method/asset/scene/data-file/doc, with the architecture map + a prioritized
stale/risk ledger. Then read the relevant `docs/MASTER_CATALOG/<area>.md` section
for whatever you're about to touch. It is verified **from the actual code, NOT from
comments** (comments lie — e.g. `HeroLocomotion`'s "pure transform" header hides that
it's a `NavMeshAgent`; trusting it mis-diagnoses every movement bug). **Be the
subject-matter expert BEFORE you change anything.** No fixing, building, or
claiming-fixed on assumptions — this supersedes guess-and-grep. Keep the catalog
current when systems change.

**Architecture law (BINDING):** read `docs/ARCHITECTURE_PRINCIPLES.md` first. The
project follows the owner's **HP B2B** architecture — bounded context per component,
scope deliberately limited; **presentation is a separate layer that never touches the
objects**; queue by **player-felt vs. holistic leverage**, never smuggle structural
refactors into player-facing work. **Decision lens: what is right, not what is easy** —
when they diverge, name it explicitly. We stay true-north by **holding each other
accountable to the real goal: deliver QUALITY, not fast.**

**Navigation:** new session? read `docs/HANDOVER.md` first — the single operator's manual
(how we work + the binding rules + this-session's new canon + the build/gate/bake cycle + resume points).
Then, before grepping or exploring, check the README system —
`PROJECT_INDEX.md` (root files), `Assets/README.md` (asset folders),
`Assets/_Modules/README.md` (code module map; each module has its own README),
`docs/README.md` (docs index). Keep these updated when you add/move files.
For architecture orientation, **`docs/ARCHITECTURE.md` is the single authoritative hub**
(HP B2B lens + assembly map + world/scene + data/catalog + save + build mode + instrumentation;
it indexes the per-area deep-dives). Read it before the individual `*_ARCHITECTURE.md` docs.

---

## 0. CRITICAL: Linux Mount ↔ Windows Sync Is Unreliable

**UI (Claude) must NEVER edit `.cs` files via bash or the Linux mount path.**

The Linux mount (`/sessions/.../mnt/defenders-unity/`) does NOT sync reliably
to the Windows project path (`C:\Users\Kayden-Laptop\Documents\defenders-unity\`).
Writes via bash can truncate, duplicate, or interleave — producing garbled Windows
files that fail to compile even though the mount shows them as correct.

**Rules:**
- UI writes `.cs` files using the **Write / Edit tools only** (Windows path)
- UI never uses `cat >`, `echo >>`, or any bash redirect to fix `.cs` files
- If a `.cs` file is broken on Windows, **only CLI fixes it** (directly on Windows)
- UI "fixing" a file on the mount will corrupt the Windows version further

**Symptom:** brace check passes on mount (`69/69`) but CLI sees a garbled file
(`82/95`). This means the mount and Windows are out of sync. Stop writing, tell CLI.

---

## 1. MANDATORY: C# File Quality Gate

After editing **any** `.cs` file, run this check before reporting done:

```python
python3 -c "
import sys
path = 'Assets/path/to/File.cs'
content = open(path).read()
opens  = content.count('{')
closes = content.count('}')
if opens != closes:
    print(f'BRACE MISMATCH in {path}: {opens} open vs {closes} close')
    sys.exit(1)
print(f'Braces balanced ({opens}) ✓')
"
```

Run for **every file you touched**. If the check fails, fix before returning.
**Never ship a C# file with mismatched braces.** CLI will revert it and the
work order goes back to the queue.

**NUL-byte guard (WO-434):** the compile gate (`DeNelle.Editor.CompileGate.Run`)
now also scans every `.cs` under `Assets/` for embedded/trailing NUL bytes (`\x00`)
and REJECTS the gate (withholds the `COMPILE_GATE_OK` marker) if any are found —
this catches §0 mount-garbled files that look byte-clean on Windows/HEAD but carry
NULs that poison a commit and break compilation.

---

## 2. Work Order Protocol

- **UI (Claude) — NEVER touches code (BINDING, owner 2026-06-13).** UI does RCA, work
  orders/specs, narrative, screenshots/images/mockups, board grooming. It does **not** write
  or edit `.cs` (no exceptions — supersedes any prior "UI writes code with a gate" reversal).
  Code it wants written goes to CLI as a spec/work order.
- **CLI (me):** writes + build-verifies **ALL** code. Sole git committer. Owns batchmode execution.
- **Owner (Samantha):** PM, catalogs, routes, makes all final creative decisions.

### Creating work orders
- Save to project root as `WORK_ORDER_NNN_short_name.md`
- Mark **Status: READY TO IMPLEMENT** when spec is complete
- Include files to edit, acceptance criteria, and what NOT to touch
- **WO-numbering authority = `MASTER_PIPELINES_BACKLOG_2026-06-06.md` + `CLI_LANES_WO_NUMBERS.md`, NOT the filesystem max.** Reserved new block **290–305** (minted 2026-06-06: quests, crafting, pets, persistence, HUD). 287–288 also used; 289 free. **Next free WO = 430** (through 429 used; 344–351 reserved, do not mint; 412–428 minted on-board 06-11/12; 429 = ex-repo-414 store-stock spec, renumbered). Always slot a new WO into a lane in the master doc.
- **LIVE BOARD (source of truth mirror) = Notion "Work Orders" DB** in *Defenders of the Realm — Pipelines*: https://app.notion.com/p/f3115f05ecf940cf8968bd82bbbdff9f (data source `5f66b263-c732-4075-b94a-f5f4de9f8087`). The git docs + Notion are kept in sync; full WO spec files stay in the repo. We migrated off Linear (free-tier 250-issue cap). See `NOTION_SOURCE_OF_TRUTH.md`.

### Completing work orders
- CLI saves a `WORK_ORDER_NNN_short_name.RESULT.md` when done and verified
- UI marks the matching Linear issue as Done

---

## 3. Scene Files — Hard Rules

- **NEVER hand-edit `Village.unity`** — corruption-on-resave history.
  Always rebuild via `Defenders > Week 3 > Build Village Scene`
  (batchmode: `DeNelle.Editor.VillageSceneBuilder.BuildVillage`)
- **Never run a bake if the Unity editor is open** — project lock.
- Bake commands go in a work order for CLI. UI does not fire batchmode.

---

## 4. Polyperfect Assets

- Pack location: `Assets/polyperfect/Low Poly Ultimate Pack/`
- **Gitignored** — re-import on fresh clone via `Defenders/Art/Fix Polyperfect URP Materials`
- Always use `_M` quality tier prefabs: `_M/Prefabs_M/<Category>_M/`
- Catalog: `docs/polyperfect-asset-catalog.md` — check before referencing any prefab name
- On missing prefab: `Debug.LogWarning` (not error) — pack may not be imported

---

## 5. Assembly Structure

| Assembly | Namespace | Contents |
|---|---|---|
| `DeNelle.Core` | `DeNelle.Core.*` | Interfaces, enums, pure data (IDamageableStructure, IVillageHud, IAudioService, CoreServices, MusicTrack, SfxId) |
| `DeNelle.Village` | `DeNelle.Village` | Enemy, EnemyBrain, WaveManager, HeartController, HeroHealth, buildings, gates |
| `DeNelle.HUD` | `DeNelle.HUD` | VillageHudController — passive display only, never references Village |
| `DeNelle.Audio` | `DeNelle.Audio` | AudioService, SfxClipLibrary |
| `DeNelle.BattleATB` | `DeNelle.BattleATB` | ATBCombatManager, BattleController |
| `DeNelle.Editor` | `DeNelle.Editor` | VillageSceneBuilder, AnimatorSetup — editor-only |

**Cross-assembly rule:** Village → Core only. HUD → Core only. Never Village ↔ HUD directly.
Use `CoreServices.Hud` and `CoreServices.Audio` for cross-module calls.

---

## 6. Key Interfaces (all in `DeNelle.Core.Combat` / `DeNelle.Core.*`)

- `IDamageableStructure` → `Assets/_Modules/Core/Combat/IDamageableStructure.cs`
  Implemented by: HeartController, HeroHealth, Building, Tower, Gate, WallSegment
- `IVillageHud` → `Assets/_Modules/Core/HUD/IVillageHud.cs`
  Implemented by: VillageHudController. Resolved via `CoreServices.Hud`
- `IAudioService` → `Assets/_Modules/Core/Audio/IAudioService.cs`
  Implemented by: AudioService. Resolved via `CoreServices.Audio`

---

## 7. Naming & Canon

- Village name: **Elarion** (never "Avalon" — retired, DESIGN-DECISIONS.md #1)
- Heart of Elarion: the world tree / stone reliquary at scene centre (0,0,0)
- No Keep building — removed (DESIGN-DECISIONS.md #3)
- Hero tag: **`Player`** (one tag per GameObject — locomotion, camera, HUD, triggers all `FindWithTag("Player")`; set in `HeroControlEnsurer.Ensure`, WO-450)
- Enemy AI finds the hero by **component** (`FindFirstObjectByType<HeroLocomotion>()`), NOT a `HeroTarget` tag — that tag was never declared; a GameObject has only one tag
- Enemy spawn tags: `SpawnPoint` — placed 12m outside each gate

---

## 8. Pipeline State Quick Reference

See `PIPELINE_STATE.md` for full detail. Key facts:
- Defend-the-Tower (PatriciaLight): **REMOVED (2026-06-09)** — module + scene gone; only `Resources/PatriciaLight/tower2` kept
- Home hub: **MainCastle_Hall** (start), **OuterWorld** streams in additively; **Village2** = raid target; **Village.unity** abandoned
- Village wave loop: **WIRED, gaps remain**
- Store / monetization: **~70% BUILT** — do NOT greenfield (PackStore exists)
- Store scene-wiring: DISABLED — needs own PanelSettings before re-enabling
- UXML in builds: does NOT work — always use code-built UI (learned the hard way)

---

## 9. Parallel Lane Rules

These lanes never conflict — run them simultaneously:
- **World/Environment** (VillageSceneBuilder, art) — architect lane
- **VFX/Audio** (VFXManager, AudioService) — no gameplay dependencies
- **Monetization/Backend** (WO-72–80) — fully isolated
- **Combat/AI** (EnemyBrain, ATB) — code only, no scene files

VillageSceneBuilder.cs is a **serialization bottleneck** — only ONE agent/branch
touches it at a time. Coordinate through work orders.

---

## 10. Before Reporting Done — Checklist

- [ ] Brace balance check passed on every `.cs` file edited
- [ ] No `.unity` scene files hand-edited
- [ ] No new `System.Reflection` usage introduced in bridge scripts
- [ ] `using DeNelle.Core.Combat;` present in any file implementing `IDamageableStructure`
- [ ] Null-conditional operators (`?.`) used on all cross-module service calls
- [ ] Work order acceptance criteria reviewed line by line

---

## 11. Orchestration & Delegation (how we work — non-negotiable)

The lead session is the **ORCHESTRATOR**, not a solo worker. Division of labor:

- **Orchestrator (lead session):** triage each issue + form the hypothesis **flow-first** — what
  *should* happen given the state (is this state even expected?), NOT culprit-hunting through stack
  traces. Route each focused task to its own agent. **Batch-gate**, **sole-commit** by explicit path,
  push only on owner OK. Hold the through-line + roundtable with the owner. Do **NOT** do the deep
  digging yourself — orchestrate; let the agents go deep.
- **Agents:** each does **ONE** focused task (one diagnosis OR one fix). Run them in **parallel**.

**Parallelize despite the single Unity gate + sole-committer:**
- Read-only **diagnosis/verify** agents are gate-free → fan out many at once.
- For **implementation**, fan out **edit-only** agents on **file-disjoint silos** (§9 lanes; same-file
  work = one agent). Tell them NOT to gate/commit. Then the orchestrator **batch-gates the combined
  tree once** (`COMPILE_GATE_OK`) and **commits each lane by explicit path**. The single Unity gate +
  the one committer are the coordination point, kept by the orchestrator — so no agent stands idle.

**Discipline:** flow-first triage → an agent verifies AND does it. Commit local; **push only after the
owner retests/confirms** (felt/gameplay) or a regression passes (data/logic) — "push the ones that
passed." Ambiguous tickets (no repro / screen / stack) **bounce back for detail** — never work blind.

**Multi-session reconciliation (FIRM RULE — always followed):** multiple sessions/agents edit the
SAME working tree. There is exactly **ONE committer** (the lead/CLI). When another session or an agent
worktree leaves changes in the tree, the committer **SAVES their work and merges only the diffs into
the correct branch by EXPLICIT PATH** — review each diff (guard the mount-sync/garble risk, §0), stage
by path, never `git add -A`, never blind-replace a file, never let a second session commit/push (two
committers duel on `.git/index.lock` → stale locks + false "pushed", see memory `sole-git-committer`).
Other sessions write + signal "ready"; the one committer reconciles. This is non-negotiable.

---

## 12. Debugging Directive — INSTRUMENT, don't guess (BINDING)

Owner mandate (2026-06-13, from B2B-scale practice): **we do not guess at bugs — we
instrument the flow and let the data tell us where it dies.** Repeated symptom-patching
on a hypothesis wastes credits and owner time. The standing loop:

1. **Trace the flow first.** Add `DeNelle.Core.Diagnostics.FlowTrace` calls at every
   meaningful step/branch of the failing system (request → resolve → fallback → render).
   `Step` (reached it), `Warn` (fallback/anomaly), `Fail` (hard stop), `Throttle` (hot
   loop, ~1/sec), `Once` (first hit), `Measure` (perf scope: `using var t = FlowTrace.
   Measure("Sys","what", warnAboveMs:16f)`). Every line is `[Flow:<system>]`-tagged and
   captured by the F8 `BreakCaptureHarness` (break-log.jsonl + Player.log).
2. **Guard every risky object op.** Wrap parse / list-build / service-lookup / UI
   construction in `Guard.Try(...)` / `Guard.TryEach(...)` — one bad object logs (via
   `FlowTrace.Fail`) and is skipped, never silently blanks a screen. **No silent failures:**
   a catch that swallows without logging is forbidden.
3. **Capture real data, then look.** A run produces `[Flow:*]` lines that pinpoint the
   dead step — fix THAT, don't re-theorize. Split every "shows nothing" into *data-empty*
   vs *built-but-invisible* vs *threw-and-skipped* using the trace, before touching code.
4. **Prefer headless capture.** These traces run in a headless batchmode play session
   (no owner playtest needed) — use it to self-serve diagnosis on passive flows
   (spawns, roster, seam-online, catalog load) before asking the owner to retest.

Helpers live in `Assets/_Modules/Core/Diagnostics/` (`FlowTrace.cs`, `Guard.cs`,
`BreakCaptureHarness.cs`). Set `FlowTrace.Enabled=false` (or strip calls) once a system
is proven stable — leave traces in while a system is being stabilised.

**Authoring standard:** how to write code to this directive from the first line —
toggle/lifecycle, where-to-instrument checklist, Guard usage, regression authoring,
conventions — lives in `docs/INSTRUMENTATION_STANDARD.md`. §12 is the *rule*; that doc is
the *method*.
