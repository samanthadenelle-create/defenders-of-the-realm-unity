# WO-1564: the research picker orphans a fifth school across a dead well, and the queue drawer prints raw internal ids

**Status:** IMPLEMENTED - 2026-09-06 uncommitted, awaiting gate
**Priority:** P2
**Silo:** `Assets/_Modules/Village/UI/Manage/ManageScreenVM.cs` +
`Assets/_Modules/Village/Buildings/BuildTimerService.cs`. Both halves live in the VM, so they are **one
lane, one agent** (CLAUDE.md §9: same-file work is never split).
⚠ `ManageScreenVM.cs` **is DIRTY** in the shared tree (uncommitted edits at 20:47). **LANDS AFTER** the
wave-two gate; build on those edits, never revert them.
**Parent:** WO-1534 §B3 + §B4. **Source:** read-only review 2026-09-06 (CLI seat), re-read at source.
**Minted** from the banner (`CLI_LANES_WO_NUMBERS.md`, renumbered to the banner's hundred-and-second-pass reconciliation, 2026-09-06 22:12).

---

## PART 1 — the research picker wastes over half the panel and orphans a school

### Evidence

`ManageScreenVM.cs:3502-3507` authors the picker as `GridColumns = 4`, `GridRows = 1`, with the comment
*"four research BUILDINGS in ONE row"*. **Five schools exist.**

`Builds/ui-capture/ManageFlow_RESEARCH_gridtop_2670x1200.png` (18:39) shows the result: four across, the
**Lumber Mill alone on row 2 beside three empty cells**, and roughly **60% of the well black**.

**Not covered by WO-2010**, whose acceptance is *"all schools visible without scrolling"* — which
**passes** on this frame. The ticket is satisfied while the screen reads as broken. Neither the ragged
orphan nor the dead well is named by WO-2010 or WO-2015.

### What to do

Author the picker's capacity **from the live school count**, not from a literal. The model owns school
membership (canon §5: *"The model owns school membership. The UI does not infer it from IDs or names"*),
so it already knows the count — the geometry must follow it.

⚠ **Pick the shape deliberately and record why:** a 3+2 split and a 5×1 row are both defensible, and the
cells should **grow into the well** rather than leaving 60% of it black. This is a layout judgement, not a
hue choice, so it is yours to make — **but state the reasoning in the RESULT** so the next seat does not
undo it.

⛔ **Do not hardcode 5 either.** A literal 5 is the same defect as the literal 4, one school later. Derive.

---

## PART 2 — the queue drawer prints raw internal ids and developer arrow notation

### Evidence

`Builds/ui-capture/ManageFlow_BUILD_queue_2670x1200.png` rows read:

```
Tower Ground Archer -> L2
Barracks -> L4
```

Composed in the **model**, not the View — `ManageScreenVM.cs:842`:
`label = name + " -> L" + job.TargetTier`. On a catalog miss it falls through to
`BuildTimerService.PrettyJobLabel` (`:2328-2340`), which title-cases the id's own tokens —
`tower_ground_archer` -> `Tower Ground Archer` — and whose comment concedes *"no catalog lookup"*.

**Not covered by WO-1491**, the copy ticket, which enumerates exactly five artifacts (`12 MORE - SCROLL`,
`stragglers. .`, a triple space, the `<-` literal, and CLOSE on five panels). This is none of them.
**WO-1418 lane B claimed *"`-> T` labels gone"*** — true of the card, untrue of the queue drawer.

⚠ **The interesting part, and it is a rule-shaped finding:** Manage canon §9 forbids the **UI** parsing
ids. Here the **VM** does it — so the dumb-UI rule is *technically* honoured while the player still reads
an identifier. The rule needs to bind wherever the string is made, not just in the View.

### What to do

1. Queue rows name the structure and the level **in words**, from the catalog display name.
2. A catalog miss becomes a **traced failure** (`FlowTrace.Fail` / `Guard`, CLAUDE.md §12) — not a
   title-cased id quietly presented to the player as a name. A missing catalog row is a data defect and
   must be loud.

⛔ **`PrettyJobLabel` has other callers — check them before changing its behaviour.** If it is load-bearing
elsewhere, add the honest path rather than repurposing it, and say which you did.

---

## ACCEPTANCE

1. The research picker's capacity is derived from the live school count; no school is orphaned on a ragged
   row; the cells occupy the well rather than leaving most of it empty.
2. No literal column/row count for the picker survives in the VM.
3. Queue drawer rows read as display names and levels in words. No `tower_ground_archer`-shaped string can
   reach the player.
4. A catalog miss is traced as a failure, never silently prettified.
5. Oracles for both halves, each **proven RED before green**, both runs recorded in the RESULT.
6. **Fresh** captures of the research picker and the queue drawer. ⛔ The 18:39 frames predate commit
   `949e848a0` (18:51) and the uncommitted Manage edits (20:47) — **no frame in the repo shows the current
   code.** Do not judge against them.
7. `COMPILE_GATE_OK` + `REGRESSION_OK <n>/<n>` on **fresh** logs, judged by the marker, never the exit code.

## WHAT NOT TO TOUCH

- `ManageWorkspacePanel.cs` — **WO-1563** owns it this wave.
- The queue drawer's row overlap, clipped timer and the `X` affordance — **WO-1488**, fixed at source and
  awaiting the wave-two gate.
- The BUILD grid's 10 tiles and five chips — CORRECT (the mockup). See **WO-1560**.
- The activity strip that `FillActiveTab:3755` hard-hides while WO-2012 still requires it — that conflict
  belongs to **WO-2012**; surface it there, do not resolve it here.
- RUSH / SPEED-UP behaviour — already ruled at `OWNER_RULINGS_LOCKED.md:430-495`.
