# Defenders of the Realm — Agent Rules & Project Memory

This file is read by every agent (UI and CLI) before starting work.
Rules here are **non-negotiable**. Do not skip them to ship faster.

**⛔ PREFLIGHT GATE (owner directive 2026-06-28, BINDING):** before you TOUCH ANY CODE or START
DEBUGGING — unprompted, or whenever the owner says "answer the preflight gate" — open
`PREFLIGHT_GATE.md` (repo root) and answer **YES + a one-line proof** to every applicable item.
A single NO / "I think so" / unproven YES = STOP; you have not earned the edit. The owner should never
again have to remind a CLI to read canon, be the SME, instrument first, or update canon — the gate makes
those a hard yes/no checklist.

**READ FIRST, EVERY SESSION (owner directive 2026-06-20, BINDING):** load
`SESSION_CANON_LOADER.md` (repo root) — the at-a-glance SME primer (core rules +
current state + key files) the owner pastes to start a session. It is the fast path;
the depth below + the deep-dive docs remain binding. THEN do the mandatory catalog read.

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
to the Windows **repo root**.

**⚠ THE REPO ROOT IS MACHINE-DEPENDENT — never hardcode it (owner ruling 2026-08-09).** It is
`C:\eoa` on one machine and `D:\eoa` on another. This line used to name `C:\EoA\` as "the project's
home", which is why a seat on the other machine could follow canon to a path that does not exist.
Write paths **relative to the repo root** (`Assets/...`, `logs/debug/...`, `tools/...`) in every doc,
work order and script; resolve the absolute root at runtime, never from a doc.
*(History: cloned fresh from GitHub `feat/tower-core-loop`, with the gitignored `Assets/polyperfect`
and `Assets/Quaternius` packs copied in from the old `Documents\defenders-unity` location.)*
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
- Save to `WorkOrders/WORK_ORDER_NNN_short_name.md` (moved out of root 2026-06-22 to declutter; the numbering authority `CLI_LANES_WO_NUMBERS.md` + `WO_AUDIT_*.md` stay at root)
- Mark **Status: READY TO IMPLEMENT** when spec is complete
- Include files to edit, acceptance criteria, and what NOT to touch
- **WO-numbering authority = the `CLI_LANES_WO_NUMBERS.md` banner. SOLE authority — nothing else.** Not the filesystem max, not `MASTER_PIPELINES_BACKLOG_2026-06-06.md` (that is a lane/backlog doc, NOT a number source — treating it as a second authority is how numbers collide), and **never a number copied into any other doc** (every copy goes stale; point at the banner instead).
  - **TWO DISJOINT BLOCKS are in use** (2026-08-02, after FIVE two-seat collisions in one day): **main line → CLI**, and **a reserved block → the UI seat**. The blocks are disjoint so both seats mint in parallel without reading each other's state. **⚠ THE BLOCK RANGES ARE DELIBERATELY NOT WRITTEN HERE — read them off the banner.** This line used to name "860–899" for the UI seat; that block was CLOSED (full at 899) and the seat moved on, so the number sat here stale and kept re-seeding the very collision it was meant to prevent. A number copied into a second doc is the bug, even when it was right the day it was written.
  - **THE RULE: each seat bumps ITS OWN banner row in the SAME edit as the mint.** A mint written to disk without bumping the banner is the collision — that is what broke 5× on 08-02, including by the CLI. Collisions resolve **first-on-disk-and-referenced-wins**.
  - Still slot every new WO into a lane in the master backlog doc — but take the NUMBER from the banner.
- **LIVE BOARD = `BOARD.html` (repo root), GENERATED from the repo** — run `python tools/board_build.py`
  (2 s; parses `WorkOrders/*.md` statuses + RESULT markers + the numbering banner). The repo IS the
  source of truth; the board is a derived view and cannot drift. Regenerate at session boot and before
  any board read. **Notion is RETIRED as the board (owner ruling 2026-08-08):** the old mirror's
  workspace was reachable by NO seat (CLI has no MCP auth; the UI connector authed to a different
  personal workspace — the DB 404s), which is how items were getting lost. Do not hand-mirror to
  Notion; `NOTION_SOURCE_OF_TRUTH.md` is superseded. (History: Linear → Notion → derived board.)

### Completing work orders
- CLI saves a `WorkOrders/WORK_ORDER_NNN_short_name.RESULT.md` when done and verified
- **The WO's own `**Status:**` line is flipped to DONE in the SAME COMMIT as the work** — the board
  is DERIVED from it (`python tools/board_build.py`), so there is no second system to mark.
  **⚠ LINEAR IS RETIRED and so is Notion (owner ruling 2026-08-09 / 2026-08-08).** The history is
  Linear → Notion → the derived board; this line used to say "UI marks the matching Linear issue as
  Done" and that instruction was dead. Do not mark, mirror, or read either. See `docs/BOARD.md`.

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

> ### ⚠ THE TABLE ABOVE IS A SUBSET, NOT THE MAP (corrected 2026-08-09, owner ruling on RULES.md C-6)
> There are **19 `.asmdef` under `Assets/_Modules/`** plus `Assets/Data/DeNelle.Data.asmdef`.
> **READ THE `.asmdef` — it is the authority on what may reference what.** The six rows above are a
> convenience list of the ones you touch most; they are not the dependency graph.

**Cross-assembly rule — ONE invariant, and it is the one that is actually enforced:**

> ## ⛔ `DeNelle.HUD` NEVER REFERENCES `DeNelle.Village`, in either direction.
> `Assets/_Modules/HUD/DeNelle.HUD.asmdef` references `DeNelle.Core` + `DeNelle.Data` ONLY.
> `Assets/_Modules/HUD/AdminOverlay.cs` reaches a Village type by **reflection precisely because the
> asmdef forbids the reference** — that reflection is evidence of the rule, not a violation of it.
> Cross-module calls go through `CoreServices.Hud` / `CoreServices.Audio`, always with `?.`.

**⚠ THE OLD RULE HERE — *"Village → Core only. HUD → Core only"* — WAS FALSE AND IS RETIRED.**
`DeNelle.Village.asmdef` legitimately references `DeNelle.BattleATB`, `DeNelle.AI`,
`DeNelle.Cosmetics`, `DeNelle.Data`, `DeNelle.Pets`, `DeNelle.Wallet` and `DeNelle.Audio` besides
`DeNelle.Core`. A seat reading the old line literally would have rejected working code as a
violation, or "fixed" references the project depends on. It was accurate when written and the project
outgrew it — the same duplicated-state drift that produced the stale WO number block (§2) and the
hardcoded repo root (§0). **Do not restore a hand-maintained dependency table here; point at the
`.asmdef` instead.**

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
- **Home hub scene = `Main_Castle_Overworld`** (MergedWorld ON, one navmesh). ⚠ `MainCastle_Hall.unity`
  still exists on disk as a LEGACY file — it is NOT the hub; that ambiguity keeps re-seeding stale docs.
  `Village.unity` + `OuterWorld.unity` are DELETED from the tree.
- Player-facing renames (canon-strings.json): tagline = **"Echoes of a Forgotten Civilization"**
  (retired "Hold the last light", 2026-07-24); HUD "Pets" → **"Echoes"**; HUD "Work" → **"Queues"**.
- **BOTTOM ACTION BAR — the 08-01 retirement is REVERSED (owner ruling WO-911 Q10+Q13, 2026-08-06).**
  The 2026-08-01 rule ("the bar Queues BUTTON is RETIRED; the Builders chip is the one Queues entry")
  **no longer holds as written.** The rule it protected — **exactly ONE Queues entry** — still holds; the
  entry MOVED. Current canon:
  - The **`Upgrade` bar face is RE-POINTED** to the unified **Manage/Queues** screen (`PanelId.Manage`,
    `ManageScreenPanel`). It is **RE-POINTED, NOT ADDED**: `ActionBarButtonId.Upgrade = 6` keeps its
    value, its widget id (`upgradeButton`) and its `hud-areas.json` row, which is what dissolves the
    8th-face problem entirely. It is now **always applicable in town**, not context-gated on a focused
    building.
  - **Map LEFT the bar** and is a **tab inside Bag**, **feature-flagged OFF** (`FeatureFlags.MapTab`,
    PlayerPrefs `ff.maptab`) because realm travel is a WO-827 stub and the areas do not connect yet.
    `ActionBarButtonId.Map` stays **dormant at ordinal 4** — never renumber it, the face arrays are
    indexed by ordinal.
  - **calm(town) bar = SIX faces: Build, Talk, Bag, Raids, Quests, Manage.** *(The old "6 faces" line
    here was stale for a different reason — it was 7 once `upgradeButton` landed. Six is now correct
    for the new membership.)* `HudActionBarModel.ButtonCount` stays **7** (enum-identity / array bound);
    the number that went 7 → 6 is `HudActionBarModel.MaxVisibleFaces`, which the View's slot geometry
    derives from.
  - The right-column **Builders chip SURVIVES as a STATUS GLANCE ONLY** (count/timer + the inline peek
    rail). Its old **double-tap door is retired**, so the bar face is the single entry.
- **Echo harvest affinity is a MATCH BONUS, NEVER a lock** (owner ruling WO-830, 2026-08-02): each of the
  six Echoes carries a unique affinity, but **the player picks each Echo's harvest resource** from a
  picker — matching that Echo's affinity **doubles** the yield. Never gate an Echo to one resource.
  **Maren harvests Crystals, not Repairs.** Persisted token grammar = **`<resource>:<level>`**
  (e.g. `harvest:3` → `wood:3`); read-migrated, no schema bump.

---

## 8. Pipeline State Quick Reference

See `KEY_FACTS.md` + the newest `CANON_GROUND_TRUTH_<date>.md` for full detail (PIPELINE_STATE.md lags). Key facts *(refreshed 2026-08-02)*:
- Defend-the-Tower (PatriciaLight): **REMOVED (2026-06-09)** — module + scene gone; only `Resources/PatriciaLight/tower2` kept
- Home hub: **`Main_Castle_Overworld`** (merged world, one navmesh); **Village2** = raid target; `Village.unity`/`OuterWorld.unity` DELETED
- Village wave loop: WIRED — **`waves.json` `enemies[]` batches are INERT** (`_smartComposition:1` → WaveManager generates rosters). **The WO-783 D1 ruling is CLOSED** (owner, 2026-07-30): both `WaveDataTest` cases were rewritten to assert the batches are EMPTY, so a re-add now FAILS. Any doc calling this "open" is stale.
- Save schema **v37** (v35 = WO-773 Obsidian queue; v36 = WO-834 `everBuiltStructureIds`, the blank-town baked standdown; **v37 = WO-911 the per-job PAID BASKET** — `paidWood/paidFood/paidIron/paidCrystals/paidMagic` on `BuildJobData`, the precondition for cancel refunding **100% of what was paid, flat** (ruling Q1); a pre-v37 job refunds ZERO and says so); the **Obsidian multi-channel queue** (Builder/Train/Research) is the single home for ALL timed work, now with a **DEPTH cap of 5 PER LINE** (`BuildTimerConfig.queueDepthPerLine` — a different axis from `freeBuildSlots`, which stays 2; **never** implement the cap by raising concurrency) and an **Echo-gated, crystal-priced extra slot** (`BuildTimerService.TryBuySlot`, ruling Q6: each Echo above 2 unlocks the RIGHT to buy, crystals complete it); Realm Map (WO-826) shipped
- Store / monetization: the live model = **player-built town** (strategic placement ALWAYS ON — the flag was removed; movable functional storefronts + vendor NPCs). PackStore/packs.json exist — do NOT greenfield — but the old Village.unity store scene-wiring is a dead path
- Pre-ship gates = `COMPILE_GATE_OK` + `REGRESSION_OK <n>/<n> suites` + `UI_CAPTURE_OK` (open the PNGs).
  **The markers are now DISTINCT per entry point (2026-08-02)** — `DataRegression.RunAll` emits
  `REGRESSION_OK <n>/<n> suites` (101 registered suites + 26 inline groups), `RegressionSuite.RunAll`
  emits `CHECKIN_SUITE_OK`, `SessionRegression.RunAll` emits `SESSION_GUARDS_OK`. Until today all three
  printed the identical `REGRESSION_OK`, so a 22-case suite's pass read as the full suite's pass — which
  is exactly how the check-in gate ran the wrong one unnoticed. **Never restate the count here**; read it
  off the marker. (Also fixed today: `tools/regression/checkin_gate.ps1` did not PARSE under PowerShell
  5.1, so no stage of it had been running at all.)
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

**THE PIPELINE NEVER IDLES (owner directive 2026-08-10):** *"we should always have a agent assigned
and queued... so the dev work continues."* While READY tickets exist, the agent pool stays loaded —
on any lane/workflow completion, the orchestrator immediately tops up with the next disjoint-lane
READY ticket(s). Pin-blocked tickets park with their pins surfaced so unblocking is one owner word.
Gate + commit cadence stays singular (one gate, one committer) — the parallelism lives in the lanes.

**Multi-session reconciliation (FIRM RULE — always followed):** multiple sessions/agents edit the
SAME working tree. There is exactly **ONE committer** (the lead/CLI). When another session or an agent
worktree leaves changes in the tree, the committer **SAVES their work and merges only the diffs into
the correct branch by EXPLICIT PATH** — review each diff (guard the mount-sync/garble risk, §0), stage
by path, never `git add -A`, never blind-replace a file, never let a second session commit/push (two
committers duel on `.git/index.lock` → stale locks + false "pushed", see memory `sole-git-committer`).
Other sessions write + signal "ready"; the one committer reconciles. This is non-negotiable.

---

## 12. Debugging Directive — INSTRUMENT, don't guess (BINDING)

> ### ★ THE HARD GATE (owner 2026-06-21 — BINDING on EVERY agent + CLI, forever) ★
> **No code edit on a non-trivial bug until you can cite CAPTURED DATA that proves the cause.**
> Instrument FIRST — loggers that step IN and OUT of each item (`FlowTrace.Enter/Step/Warn/Fail`,
> `Guard`), run it (prefer **HEADLESS** to self-serve — the AutoPilot fleet + `break-log.jsonl` +
> on-load state dumps), read the trace, let the data **PINPOINT the dead step**, then fix THAT.
> - **Static code-reading LOCATES candidates; it NEVER CONCLUDES the cause.** An inferred root is a guess.
> - **Never inference-fix. Never guess-and-ship.** A "plausible fix" *feels* like progress and is the
>   slow path (N blind cycles); instrumenting *feels* slow and is the fast path (one read).
> - This is the **OPENING MOVE, unprompted** — not a fallback after a guess fails. If you can't point
>   to the data line that proves it, you have not earned the edit.
> - The methodology is to be **passed to every agent ever spawned, every CLI, every session.** Propagate it.
>
> *Lesson forged 2026-06-21:* the castle "pink floor" was guessed at for 3 cycles (a terrain theory that
> was WRONG); one headless FloorDiag dump then named the real cause — colorless URP/Lit floor tiles — in
> a single read. Memory: `never-inference-fix`.

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
`BreakCaptureHarness.cs`).

**⛔ NEVER STRIP FLOWTRACE (owner ruling 2026-08-09, BINDING).** Instrumentation is PERMANENT. Once a
system is proven stable you may eventually **FLAG IT OFF** (`FlowTrace.Enabled=false`) — the calls
**STAY IN THE CODE**. This line used to read *"set `FlowTrace.Enabled=false` **(or strip calls)**"*,
which licensed deleting the very net §12 exists to build: a stripped `Warn`/`Fail`/`Guard` turns a
logged failure back into a SILENT one, and the next regression in that system starts from zero
evidence instead of a trace. Removing instrumentation is never "cleanup" — it discards the only asset
that makes the NEXT bug cheap. Leave traces in while a system is being stabilised, and leave them in
afterwards. See `docs/INSTRUMENTATION_STANDARD.md` §1.4.

**Authoring standard:** how to write code to this directive from the first line —
toggle/lifecycle, where-to-instrument checklist, Guard usage, regression authoring,
conventions — lives in `docs/INSTRUMENTATION_STANDARD.md`. §12 is the *rule*; that doc is
the *method*.

---

## 13. Ticket Pipeline — QA → CLI → PO (BINDING)

Owner directive (2026-06-20): the playtest/bug backlog runs through a **role-separated**
pipeline. **Full spec: `docs/TICKET_PIPELINE.md` (BINDING — read it before working tickets).**
In one breath:

- **PO** (the owner) pulls a ticket from the QUEUE (F8 `break-log` flags), sets its **SILO**,
  routes to QA; and **after deploy felt-verifies + CLOSES** it (headless can't judge feel).
- **QA Triage = read-only agents.** Classify **NEW FEATURE vs EXISTING** first. NEW (not built)
  → back to PO as a spec/WO — never RCA-fix the unbuilt. EXISTING → read-only RCA → push to CLI.
- **CLI** (this seat, **sole committer**) validates + implements + **headless-verifies**
  (`CompileGate` + AutoPilot fleet / `DataRegression`) + deploys → hands to PO. Never claims
  fixed on faith (§5/§12).
- **Shared board = `BOARD.html`, DERIVED from `WorkOrders/*.md`** (`python tools/board_build.py`).
  The hand-off log is the WO / ticket markdown itself — stage + silo + who-has-it live in the file,
  so the record and the work are the same artifact. **Log every hand-off there.**
  **⚠ THE TASK LIST IS RETIRED (owner ruling 2026-08-09)**, as are Notion (08-08) and Linear (08-09).
  This line used to name the Task list as the shared board. There is now exactly ONE board and it is
  derived — nothing to mirror, nothing to fall behind. See `docs/BOARD.md`.
- **Role separation is non-negotiable:** QA doesn't write, CLI doesn't classify-triage, PO closes
  (not CLI). Read-only constraint on early triage.

---

## 14. F8 Live-Triage Watcher (BINDING — every CLI session, forever)

Owner directive (2026-06-23): **the owner is NEVER the bug detector** (memory
`never-dragdrop-or-manual-playtest`). Whenever the owner is (or is about to start) felt-testing,
every F8 flag / error / softlock the harness records must surface on the CLI **without the owner
saying "rearm" or "watch"** — the CLI **triages it LIVE** (RCA from the captured line + screenshot).

**Claude Code seats: the listener is HOOK-ENFORCED (owner directive 2026-08-10).** `.claude/settings.json`
(committed, all seats) arms three hooks: SessionStart auto-starts the daemon; UserPromptSubmit injects any
un-acked capture at turn start; a Stop-hook background poller (`.claude/hooks/f8-poll-rewake.ps1`, 10 s
cadence, single-instance across seats via a repo lock) REWAKES the idle seat the moment a capture lands.
The old per-turn-poll discipline (`.cursor/rules/f8-auto-triage.mdc`) stopped being followed within a
month — the harness now executes it instead of trusting the seat to.

**Persistent daemon (primary — no manual re-arm):**
- **Start once:** `powershell -File .claude\skills\run-defenders\f8-watch-start.ps1` (idempotent).
  Runs `f8-watch-daemon.ps1` hidden; watches `break-log.jsonl` + Editor/Player logs forever.
- **Inbox:** `logs/f8-inbox/` — daemon writes `LATEST_CAPTURE.md` + bumps `PING.json` on each capture.
- **Agent poll:** `.cursor/rules/f8-auto-triage.mdc` (alwaysApply) — every turn run `f8-check-inbox.ps1`
  FIRST; session start also launches background `f8-watch-poll.ps1` (notify on `F8 INBOX PING`).
- **After triage:** `f8-ack.ps1`, then re-launch `f8-watch-poll.ps1`.
- **Stop:** `f8-watch-stop.ps1` when done for the day.

Fires ONLY on real captures (F8 `flagged` / error / exception / softlock); `session_start` +
`scene_loaded` startup noise is filtered; re-baselines on a fresh Play session.

On a fire the daemon **AUTO-HARVESTS** the recent `[Flow:*]` / `[FeatureFlags]` / Guard / exception
lines into `LATEST_CAPTURE.md`. **TRIAGE FROM THOSE LINES — read the already-captured data FIRST,
before any code-read, any agent, any theory.** Spawning a code-reading agent before reading the
harvested trace is the banned failure (memory `never-inference-fix`). The owner must NEVER have
to ask "did the data show that" — the look is structural.

Then RCA from the data + screenshot, and route per §13 (CLI implements + headless-verifies; PO
felt-verifies + closes). The owner just plays and F8s.

**Legacy fallback:** `bash .claude/skills/run-defenders/f8-watch.sh` (one-shot; re-arm after each fire).

---

## 15. Canon Maintenance — keep the docs from going stale (WO-520, BINDING)

Owner directive (2026-06-26): we did one painful fleet-scale audit of all 1090 `.md` files
(`CANON_READINESS_LEDGER_2026-06-26.md`) because the canon had drifted weeks behind reality. **Never
again at that scale.** The standing rule, so canon updates stay 5-minute tasks:

- **The single live anchor = `CANON_GROUND_TRUTH_<date>.md` at repo root.** It states current reality
  (branch, hero rig, combat model, world/seam, in-flight status). Keep exactly ONE current; supersede the
  old one by date. Every session and every agent checks docs against it.
- **Update canon in the same breath as the change.** Any commit that changes architecture/state (branch,
  hero rig, a pillar's scope, a scene's role, a removed/added system, a creative-canon decision) MUST update
  the relevant load-bearing doc in the SAME commit/PR — or, if deferred, add a one-line `STALE:` flag at the
  top of that doc naming what's now wrong. A state change with no canon update is an incomplete change.
- **Load-bearing set (the read-first canon) — keep these green:** `SESSION_CANON_LOADER.md`,
  `docs/HANDOVER.md`, `PIPELINE_STATE.md`, `docs/MASTER_CATALOG.md`, `PROJECT_INDEX.md`, the relevant
  `docs/*ARCHITECTURE*` / `docs/COMBAT_PIVOT_NORTHSTAR.md`, and this file.
- **Frozen, never rewrite:** dated point-in-time ledgers (OVERNIGHT_*, MORNING_*, dated HANDOVER_*,
  RESULT files, dated session reports). If one reads as current, add a `⚠ SUPERSEDED <date>` banner — do
  not rewrite the body. Backlog WOs are frozen by their date; an UNDATED WO asserting current state
  (branch / "#1 priority now" / "fix before go-live") is STALE and must be banner-fixed or dated.
- **Weekly 5-minute audit:** skim the load-bearing set above against the ground-truth anchor; fix or flag.
- **Never guess** — every canon update is sourced from HEAD commits / working tree / the live auto-memory
  index / verified summaries, never from assumption (§12 discipline applies to docs too).
