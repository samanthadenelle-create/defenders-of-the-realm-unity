# WO-1541: Manage names the camp you are training for, renders it as a label, and composes that copy in a second producer

**Status:** READY TO IMPLEMENT — **owner ruling 2026-09-06: "Named camp + door."** (was: BLOCKED)
**Priority:** P1
**Silo:** `Assets/_Modules/Village/UI/Manage/ManageScreenVM.cs` + `ManageScreenPanel.cs` +
`Assets/_Modules/Core/HudModel/PostureSignals.cs` (+ `JourneyDeckSubtitleVM.cs` as the reader).
**Parent:** WO-1534 §A1. **Source:** read-only review 2026-09-06 (CLI seat), re-read at source.
**Minted** from the banner (`CLI_LANES_WO_NUMBERS.md`, renumbered to the banner's hundred-and-second-pass reconciliation, 2026-09-06 22:12).

---

## 1. EVIDENCE

`ManageScreenVM.BuildTroopArmySummary()` (`:1947-1986`) composes what is arguably the most motivating
sentence in the game:

```
Army 8 / 10 - The Forsaken Camp fields 12
```

`ManageScreenPanel.cs:3950` renders it with `ElarionUiKit.Label(...)` — **a label, not a button.** The
game names your enemy, counts their garrison, and offers nothing to press.

**The thread is one-way.** The reverse door exists: `RaidDeployScreen.cs:857` opens
`PanelId.Manage, "Troops"` when you are short on troops (WO-1403). Nothing goes the other way.

### ⛔ And the copy is composed TWICE — this is the actual defect

| Producer | Output | Second clause derived from |
|---|---|---|
| `JourneyDeckSubtitleVM.cs:22` | `Army 8 / 10 . 2 camps open` | `PostureSignals.RaidOpenCampCount` |
| `ManageScreenVM.cs:1978-1984` | `Army 8 / 10 - The Forsaken Camp fields 12` | its **own** `RaidSelectionVM` walk (`:1965`) |

Different separators, different second clauses, two independent derivations of "which camp is next". The
comment twelve lines above the first names this exact failure — `PlayerDeckWorkspace.cs:719-723`:

> *"ONE rule, TWO surfaces, one of them ignoring it: the duplicated-state class this repo keeps getting
> burned by. The fix is to read the EXISTING predicate, never to write a second check here — a second
> check would drift from the first, and the drift is the actual defect."*

And `PostureSignals.cs:333-336` states why the publish-a-count pattern exists at all: *"Publish the
already-projected count here beside army fill rather than making the Journey card reach across the
assembly boundary or duplicate the camp predicate."*

⚠ **PRECISION, so nobody fixes the wrong thing:** `ManageScreenVM` lives in `DeNelle.Village`, so
constructing a `RaidSelectionVM` is **NOT an assembly violation.** It breaks the **one-producer rule**,
not the boundary. Do not "fix" this by moving code across assemblies.

### The same line is typographically ranked last

It is drawn at `ElarionUi.FontMicro` (**32**) — which `ElarionUi.cs:115` reserves for *"hotkey badge, rune
strip"*, the smallest authored role in the kit — in `ParchmentDim`, in a 26 px band.

⛔ **Do NOT pay for a bigger line by shrinking a neighbour.** `ManageScreenPanel.cs:3941-3949` records that
this band was already starved to 18.2 px once, which made TMP cull the entire line (the "bare plate"
class), and ends: *"Never re-shrink a text band below ~24px on this card."*

## 2. ⚠ WHY THIS IS NOT A ONE-LINE FIX

`PostureSignals` publishes only a **count** (`:337` `RaidOpenCampCount`) — **never a named camp.** So the
duplication cannot be removed by pointing Manage at the existing authority; the authority does not carry
the fact Manage is using.

## 3. THE TWO SHAPES

- **(a) The authority gains a published *next camp* fact** — name + garrison — and **both** surfaces read
  it. Producer stays Village-side (`BuildTimerService.PublishArmyStatus`, the existing relay), consumers
  are HUD and Manage. **Stronger game:** a named enemy with a garrison count is a far better reason to
  train than *"2 camps open"*. More work, and it puts raid projection on the posture rail.
- **(b) Manage stops naming a camp** and reads `RaidOpenCampCount`, matching the Journey deck exactly.
  Cheap, immediately removes the second producer, and loses the best sentence on the screen.

## 4. THE RULING

> **Owner, 2026-09-06: "Named camp + door."** — i.e. **shape (a), and yes to the door.**

Two things follow, and they must land together:

1. **`PostureSignals` gains a published NEXT CAMP fact** — the camp's name and its garrison count —
   published by the existing Village-side relay (`BuildTimerService.PublishArmyStatus`, which already
   feeds `SetRaidOpenCampCount`). **Both** Manage and the Journey deck read it. `ManageScreenVM` stops
   constructing its own `RaidSelectionVM` (`:1965`) to re-derive which camp is next.
2. **Manage / ARMY's army line becomes a door to the raid grid.** The player can act on the sentence that
   names their target.

⚠ **The Journey deck's own copy is NOT being redesigned.** It keeps saying what it says
(`Army 8 / 10 . 2 camps open`); the point is that both surfaces now derive from **one** published fact
instead of two independent walks. If the deck's wording should change to use the named camp too, that is
a separate call — **do not change it unasked.**

⚠ **Sequenced with WO-1542, which the owner ruled the same day** (`LOCKED` becomes a warning, not a gate).
Because the word is now advice rather than a lock, this door is a motivator rather than an escape hatch —
but it is still the route from *"you are outmatched"* to the place you fix it. **The two tickets touch
different files and can run in parallel; read WO-1542 before writing the door's copy** so the two screens
do not describe readiness in contradictory words.

## 5. ACCEPTANCE (once ruled)

1. Exactly **ONE** producer composes army-vs-raid readiness copy; the other reads it.
2. An oracle **FAILS the build if a second producer appears** — the same shape as the WO-1521
   `ClaimableCount` fix that closed this class for quests. **Proven RED before green**, both runs recorded.
3. The Manage / ARMY line is a door to the raid grid, **or deliberately is not**, per the ruling — and the
   decision is recorded in the file either way.
4. The line is not rendered at `FontMicro`, and no neighbouring band is shrunk below ~24 px to pay for it.
5. Fresh captures of Manage / ARMY. ⛔ No frame in the repo postdates the current code.
6. `COMPILE_GATE_OK` + `REGRESSION_OK <n>/<n>` on **fresh** logs, judged by the marker.

## 6. WHAT NOT TO TOUCH

- The `LOCKED - needs Army N` word and the raid door's conditions — **WO-1542**.
- `ManageWorkspacePanel.cs` — **WO-1563**; the research picker and queue labels — **WO-1564**.
- Heartfire, and any second "when may you raid" gate — **WO-1379** / `HeartfireRegression` PIN F.
