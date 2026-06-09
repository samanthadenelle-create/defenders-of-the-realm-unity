# Defenders of the Realm — Agent Rules & Project Memory

This file is read by every agent (UI and CLI) before starting work.
Rules here are **non-negotiable**. Do not skip them to ship faster.

**Navigation:** before grepping or exploring, check the README system —
`PROJECT_INDEX.md` (root files), `Assets/README.md` (asset folders),
`Assets/_Modules/README.md` (code module map; each module has its own README),
`docs/README.md` (docs index). Keep these updated when you add/move files.

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

---

## 2. Work Order Protocol

- **UI (Claude):** writes work orders, specs, routes tasks, makes creative calls.
- **CLI:** writes + build-verifies all code. Owns batchmode execution.
- **Owner (Samantha):** PM, catalogs, routes, makes all final creative decisions.

### Creating work orders
- Save to project root as `WORK_ORDER_NNN_short_name.md`
- Mark **Status: READY TO IMPLEMENT** when spec is complete
- Include files to edit, acceptance criteria, and what NOT to touch
- **WO-numbering authority = `MASTER_PIPELINES_BACKLOG_2026-06-06.md` + `CLI_LANES_WO_NUMBERS.md`, NOT the filesystem max.** Reserved new block **290–305** (minted 2026-06-06: quests, crafting, pets, persistence, HUD). 287–288 also used; 289 free. **Next free WO = 389** (through 388 used). Always slot a new WO into a lane in the master doc.
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
- Hero tags: `HeroTarget` (for enemy AI), `Player` (for locomotion/trigger detection)
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
